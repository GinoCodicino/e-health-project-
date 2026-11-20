
from dataclasses import dataclass
from datetime import datetime
import json
import os
import re

from transformers import AutoModelForCausalLM, AutoTokenizer


@dataclass
class LoadedLLM:
    name: str
    model: object
    tokenizer: object


@dataclass
class Persona:
    number: int
    data: dict

    def system_prompt(self):
        return f"here the persona's description you have to embody : \n{json.dumps(self.data)}\n"


class LLMLoader:
    def load(self, model_name):
        """Load the model and tokenizer from the given model name."""
        model = AutoModelForCausalLM.from_pretrained(model_name)
        tokenizer = AutoTokenizer.from_pretrained(model_name)
        return LoadedLLM(name=model_name, model=model, tokenizer=tokenizer)


class PersonaLoader:
    def __init__(self, folder="personas"):
        self.folder = folder

    def load(self, persona_number):
        """Load personas/persona_{persona_number}.json."""
        file = os.path.join(self.folder, f"persona_{persona_number}.json")
        with open(file, "r") as f:
            return Persona(number=persona_number, data=json.load(f))


class AnswerStore:
    SEPARATOR = ":) :) :) ---------------------------------------"

    def __init__(self, output_file):
        self.output_file = output_file

    @classmethod
    def for_run(cls, model_name, persona_number):
        """Store in output_{persona_number}/output_{model_name}.txt."""
        folder = f"output_{persona_number}"
        os.makedirs(folder, exist_ok=True)
        file_name = f"output_{model_name.replace('/', '_')}.txt"
        return cls(os.path.join(folder, file_name))

    def save(self, input_text, answer):
        with open(self.output_file, "a") as f:
            f.write(datetime.now().strftime("%Y-%m-%d %H:%M:%S") + "\n")
            f.write(input_text + "\n")
            f.write(answer + "\n")
            f.write(self.SEPARATOR + "\n")


THINK_REGEX = re.compile(r"<think>.*?</think>", flags=re.DOTALL)


def remove_thinking(text):
    return THINK_REGEX.sub("", text)


class LLMGeneration:
    def __init__(self, llm: LoadedLLM, persona: Persona, store: AnswerStore = None):
        self.llm = llm
        self.persona = persona
        self.store = store

    def generate(self, input_text, max_new_tokens=200, temperature=0.7):
        """Return the answer (without <think> block) and store it if a store is given."""
        model, tokenizer = self.llm.model, self.llm.tokenizer
        model.eval()
        messages = [
            {"role": "system", "content": self.persona.system_prompt()},
            {"role": "user", "content": input_text},
        ]
        inputs = tokenizer.apply_chat_template(
            messages,
            add_generation_prompt=True,
            return_tensors="pt",
            return_dict=True,
        ).to(model.device)

        output = model.generate(
            **inputs,
            max_new_tokens=max_new_tokens,
            temperature=temperature,
            do_sample=True,
        )
        answer = tokenizer.decode(
            output[0][inputs["input_ids"].shape[-1]:],
            skip_special_tokens=True,
        )
        answer = remove_thinking(answer)

        if self.store is not None:
            self.store.save(input_text, answer)
        return answer
