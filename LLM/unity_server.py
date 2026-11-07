"""Bridge Unity /ask. Reuses main.LLM without editing the model author's main.py.

python unity_server.py --demo  -> explicit scripted replies, no model download
python unity_server.py         -> real local Qwen model and existing persona
"""
from __future__ import annotations

import argparse
from collections import OrderedDict
from contextlib import asynccontextmanager
import json
import logging
import os
from pathlib import Path
import re
import threading
from typing import Any, Callable

from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field
import uvicorn

BASE_DIR = Path(__file__).resolve().parent
MODEL_NAME = os.environ.get("NPC_MODEL", "Qwen/Qwen3-0.6B")
MAX_QUESTION_CHARACTERS = 4000
MAX_HISTORY_TURNS = 3
GAME_CONTEXT = (
    "You are an NPC speaking to a traveler in a fictional forest game. "
    "Embody the supplied persona, including their climate skepticism when present. "
    "The player is meant to examine and discuss this character's beliefs. "
    "Express personal doubts as personal views, not as established scientific facts. "
    "Be willing to engage with evidence; do not invent local weather measurements. "
    "Answer the player's latest question directly, in the language of that question, "
    "in at most three short sentences. Do not write an inner monologue or reasoning. "
    "The current quest is Find Mushrooms: the player selects five mushroom pictures "
    "among leaves, rocks and flowers. There is no time limit; each mushroom counts once. "
    "The forest story says the soil and ancient roots have lost nourishment and "
    "mushrooms are disappearing. This is a fictional story. "
    "Unity handles the quest counter and the fixed-choice assessment: never grade answers "
    "or decide whether the player has completed the quest."
)
log = logging.getLogger("npc_server")


class QuestionRequest(BaseModel):
    question: str = Field(..., min_length=1, max_length=MAX_QUESTION_CHARACTERS)
    session_id: str = Field(default="legacy", min_length=1, max_length=64, pattern=r"^[A-Za-z0-9_-]+$")


class ConversationMemory:
    """Bounded, process-local dialogue memory, separated by Unity NPC session."""
    def __init__(self, max_sessions=16, max_turns=MAX_HISTORY_TURNS, max_characters=8000):
        self.sessions = OrderedDict()
        self.max_sessions = max_sessions
        self.max_turns = max_turns
        self.max_characters = max_characters

    def get(self, session_id):
        return [dict(message) for message in self.sessions.get(session_id, [])]

    def add(self, session_id, question, answer):
        messages = self.get(session_id)
        messages.extend([{"role": "user", "content": question}, {"role": "assistant", "content": answer}])
        while len(messages) > 2 * self.max_turns or sum(len(x["content"]) for x in messages) > self.max_characters:
            del messages[:2]
        self.sessions[session_id] = messages
        self.sessions.move_to_end(session_id)
        while len(self.sessions) > self.max_sessions:
            self.sessions.popitem(last=False)


def final_answer(text: str) -> str:
    """Return only the answer, including when an incomplete think block slips through."""
    text = re.sub(r"<think>.*?</think>", "", text, flags=re.DOTALL | re.IGNORECASE)
    # An unclosed think block has no safe final answer to show in the game.
    if re.search(r"<think>", text, flags=re.IGNORECASE):
        text = re.split(r"<think>", text, maxsplit=1, flags=re.IGNORECASE)[0]
    if re.search(r"</think>", text, flags=re.IGNORECASE):
        text = re.split(r"</think>", text, flags=re.IGNORECASE)[-1]
    return text.strip()


def load_real_model(persona_number: int) -> Any:
    """Keep main.py intact; only resolve its relative file paths for this server."""
    persona_path = BASE_DIR / "personas" / f"persona_{persona_number}.json"
    if not persona_path.is_file():
        raise FileNotFoundError(f"Missing persona: {persona_path}")
    from main import LLM

    class GameLLM(LLM):
        def load_persona(self, number):
            with (BASE_DIR / "personas" / f"persona_{number}.json").open(encoding="utf-8") as handle:
                return json.load(handle)

        def generate_name_output_file(self, model_name, number):
            folder = BASE_DIR / f"output_{number}"
            folder.mkdir(exist_ok=True)
            return str(folder / ("output_" + model_name.replace("/", "_") + ".txt"))

    return GameLLM(model_name=MODEL_NAME, persona_number=persona_number)


def generate_real_answer(llm: Any, question: str, history=None) -> str:
    """Qwen3 non-thinking chat generation; the supplied LLM's weights/persona/logs are reused."""
    import torch

    messages = [
        {"role": "system", "content": llm.system_prompt + "\n\n" + GAME_CONTEXT},
        *(history or []),
        {"role": "user", "content": question},
    ]
    inputs = llm.tokenizer.apply_chat_template(
        messages,
        add_generation_prompt=True,
        return_tensors="pt",
        return_dict=True,
        enable_thinking=False,
    ).to(llm.model.device)
    llm.model.eval()
    with torch.inference_mode():
        output = llm.model.generate(
            **inputs,
            max_new_tokens=256,
            do_sample=True,
            temperature=0.7,
            top_p=0.8,
            top_k=20,
        )
    text = llm.tokenizer.decode(output[0][inputs["input_ids"].shape[-1]:], skip_special_tokens=True)
    answer = final_answer(text)
    if not answer:
        raise ValueError("The model returned no final answer. Try a shorter question.")
    llm.store_answer(question, answer, llm.output_file)
    return answer


def demo_answer(question: str) -> str:
    q = question.lower()
    if any(word in q for word in ("crescon", "grow", "soil", "terreno", "radici", "roots")):
        return ("In our forest story, the soil and ancient roots have lost their nourishment. "
                "A few mushrooms remain. Select Find Mushrooms to help me find them.")
    return ("Select Find Mushrooms, then click the five mushroom pictures. "
            "Leaves, rocks and flowers do not count. There is no time limit.")


def create_app(
    demo: bool = False,
    persona_number: int = 1,
    loader: Callable[[int], Any] | None = None,
    generator: Callable[[Any, str], str] | None = None,
) -> FastAPI:
    # Injectable backend functions let contract tests run without model weights.
    model_loader = loader or load_real_model
    answer_generator = generator or generate_real_answer

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        app.state.mode = "demo" if demo else "llm"
        app.state.status = "ready" if demo else "loading"
        app.state.detail = "Scripted replies; no LLM is running." if demo else "Loading model and persona."
        app.state.llm = None
        app.state.lock = threading.Lock()
        app.state.memory = ConversationMemory()

        def load():
            try:
                app.state.llm = model_loader(persona_number)
                app.state.detail = "Local model and persona loaded."
                app.state.status = "ready"
                log.info("LLM READY. Open http://127.0.0.1:8000/health to check.")
            except Exception as exc:
                app.state.detail = str(exc)
                app.state.status = "error"
                log.exception("LLM loading failed; inspect /health and Terminal.")

        if demo:
            log.warning("DEMO MODE: scripted replies, no language model.")
        else:
            log.info("Loading %s, persona %s. First launch may download weights.", MODEL_NAME, persona_number)
            threading.Thread(target=load, name="load-npc-model", daemon=True).start()
        yield

    app = FastAPI(title="Find Mushrooms NPC bridge", lifespan=lifespan)

    @app.get("/health")
    def health():
        return JSONResponse(
            status_code=200 if app.state.status == "ready" else 503,
            content={
                "status": app.state.status,
                "mode": app.state.mode,
                "model": None if demo else MODEL_NAME,
                "persona": None if demo else persona_number,
                "detail": app.state.detail,
                "max_question_characters": MAX_QUESTION_CHARACTERS,
                "history_turns": MAX_HISTORY_TURNS,
            },
        )

    @app.post("/ask")
    def ask_npc(data: QuestionRequest):
        question = data.question.strip()
        if not question:
            raise HTTPException(status_code=422, detail="Write a non-empty question.")
        if app.state.status != "ready":
            raise HTTPException(status_code=503, detail=app.state.detail)
        if demo:
            return {"answer": demo_answer(question), "mode": "demo"}
        # Avoid overlapping calls to the same local model.
        if not app.state.lock.acquire(blocking=False):
            raise HTTPException(status_code=503, detail="The model is busy. Try again shortly.")
        try:
            if generator is not None:
                raw_answer = answer_generator(app.state.llm, question)
            else:
                raw_answer = generate_real_answer(app.state.llm, question, history=app.state.memory.get(data.session_id))
            answer = final_answer(raw_answer)
            if not answer:
                raise ValueError("The model returned an empty final answer.")
            app.state.memory.add(data.session_id, question, answer)
            return {"answer": answer, "mode": "llm"}
        except Exception as exc:
            log.exception("Model generation failed.")
            raise HTTPException(status_code=503, detail="Model generation failed. Check Terminal.") from exc
        finally:
            app.state.lock.release()

    return app


app = create_app(demo=os.environ.get("NPC_DEMO", "0") == "1")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--demo", action="store_true", help="Use explicitly labelled scripted replies; no LLM.")
    parser.add_argument("--persona", type=int, default=1)
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")
    uvicorn.run(create_app(demo=args.demo, persona_number=args.persona), host="127.0.0.1", port=args.port)
