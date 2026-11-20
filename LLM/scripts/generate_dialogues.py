import os

import torch

from pipeline.Dialogues import SCRIPTWRITER, DialogueWriter, beliefs_from_records, in_game
from pipeline.Interview import RecordStore, run_info
from core.LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from core.RAG import FactBase, Retriever

FOLDER = "Ethan"
FACT_FILE = "facts/ipcc_ar6_syr_spm.json"


def player_advice(folder):
    """What Ethan said in the interview about how to talk to him (question Q6), if the team kept an answer."""
    file = os.path.join(folder, "answers", "interview.json")
    if not os.path.exists(file):
        return ""
    run, answers = RecordStore().load(file)
    kept = [answer for answer in answers if answer.source == "Q6" and answer.status in ("kept", "edited")]
    return (kept[0].edited_text or kept[0].text) if kept else ""


def generate_dialogues(model_name, persona_number, variants=3, belief_ids=None, seed=42):
    """Write the variants of the dialogue with Ethan (player's answers, Ethan's reactions, endings)."""
    run, generated = RecordStore().load(os.path.join(FOLDER, "answers", "beliefs.json"))
    beliefs = beliefs_from_records(generated)
    if not beliefs:
        raise ValueError(f"No belief kept yet: set the status of the chosen ones to 'kept' in {FOLDER}/answers/beliefs.json")
    if belief_ids:
        beliefs = [belief for belief in beliefs if belief.id in belief_ids]
    torch.manual_seed(seed)
    os.makedirs(os.path.join(FOLDER, "log"), exist_ok=True)
    llm = LLMLoader().load(model_name)
    ethan = LLMGeneration(llm, in_game(PersonaLoader().load(persona_number)), AnswerStore(os.path.join(FOLDER, "log", "dialogue_ethan.txt")))
    writer = LLMGeneration(llm, SCRIPTWRITER, AnswerStore(os.path.join(FOLDER, "log", "dialogue_player.txt")))

    dialogue_writer = DialogueWriter(ethan, writer, Retriever(FactBase.load(FACT_FILE).statements), variants, player_advice=player_advice(FOLDER))
    records = dialogue_writer.run(beliefs)
    info = run_info(ethan, dialogue_writer.temperature, seed)
    info["player_advice"] = dialogue_writer.player_advice
    RecordStore().save(records, os.path.join(FOLDER, "answers", "dialogue.json"), info)
    return records


if __name__ == "__main__":
    model_name = "Qwen/Qwen3-0.6B"
    persona_number = 1
    records = generate_dialogues(model_name, persona_number)
    print(f"{len(records)} dialogue variants saved in {FOLDER}/answers/dialogue.json")
