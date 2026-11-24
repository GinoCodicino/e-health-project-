from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime
import json
import logging
import os
import re

from pipeline import prompts
from pipeline.prompts import NO_THINK, PROMPT_VERSION

REVIEW_STATUSES = ("to_review", "kept", "edited", "rejected")
logger = logging.getLogger(__name__)


@dataclass
class InterviewQuestion:
    id: str
    topic: str
    text: str


class QuestionLoader:
    def load(self, file):
        """Load a list of questions saved as JSON (id, topic, text)."""
        with open(file, "r") as f:
            return [InterviewQuestion(**data) for data in json.load(f)]


@dataclass
class GenerationRecord:
    """One text written by the LLM, with what is needed to report on it and to review it."""
    id: str
    source: str
    text: str
    prompt: str
    data: dict = field(default_factory=dict)
    status: str = "to_review"
    edited_text: str = ""
    note: str = ""


class RecordStore:
    def save(self, records, file, run_info):
        """Save the records with the information of the run (model, temperature, date ...)."""
        os.makedirs(os.path.dirname(file) or ".", exist_ok=True)
        content = {"run": run_info, "items": [record.__dict__ for record in records]}
        with open(file, "w") as f:
            json.dump(content, f, indent=1)

    def load(self, file):
        with open(file, "r") as f:
            content = json.load(f)
        return content["run"], [GenerationRecord(**data) for data in content["items"]]


def run_info(generation, temperature, seed):
    """What the report needs to know about a run of the LLM."""
    return {
        "model": generation.llm.name,
        "temperature": temperature,
        "seed": seed,
        "prompt_version": PROMPT_VERSION,
        "generated_at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
    }


def without_thinking(generation, input_text, **parameters):
    """Generate an answer with Qwen3's thinking switched off."""
    return generation.generate(input_text + NO_THINK, **parameters)


def clean_answer(text):
    """Keep the answer only: no reasoning left, no surrounding quotes, no 'Answer:' label, no empty lines."""
    if "<think>" in text:
        text = text.split("</think>")[-1] if "</think>" in text else ""  # a reasoning cut before its end: no answer
    text = text.strip().strip('"“”').strip()
    text = re.sub(r"^(Your answer|Answer|Ethan Brooks|Ethan|Chloe Bennett|Chloe|Rachel Morgan|Rachel)\s*:\s*", "", text, flags=re.IGNORECASE)
    return " ".join(line.strip() for line in text.splitlines() if line.strip())


class Writer(ABC):
    """Common LLM settings and generation; subclasses define their prompt."""
    def __init__(self, generation, max_new_tokens, temperature):
        self.generation = generation
        self.max_new_tokens = max_new_tokens
        self.temperature = temperature

    @abstractmethod
    def build_prompt(self, question, answer=None):
        """Build the prompt from a question and, when needed, an interview answer."""
        pass

    def generate(self, question, answer=None):
        prompt = self.build_prompt(question, answer)
        output = without_thinking(
            self.generation, prompt,
            max_new_tokens=self.max_new_tokens, temperature=self.temperature,
        )
        return prompt, output


class PersonaInterview(Writer):
    def __init__(self, generation, answers_per_question=1, max_new_tokens=600, temperature=0.8):
        """generation is an LLMGeneration with the persona to interview."""
        super().__init__(generation, max_new_tokens, temperature)
        self.answers_per_question = answers_per_question

    def build_prompt(self, question, answer=None):
        return prompts.interview_prompt(question.text)

    def ask(self, question):
        """Return several answers of the persona to one question."""
        records = []
        for number in range(1, self.answers_per_question + 1):
            logger.info(
                "[%s] Variante %d/%d — %s",
                question.id, number, self.answers_per_question, question.text,
            )
            input_text, answer = self.generate(question)
            records.append(GenerationRecord(id=f"{question.id}-A{number}", source=question.id, text=clean_answer(answer), prompt=input_text))
        return records

    def run(self, questions):
        return [record for question in questions for record in self.ask(question)]


class RequirementWriter(Writer):
    def __init__(self, generation, player="a target player", max_new_tokens=400, temperature=0.3):
        """generation is an LLMGeneration with the ANALYST persona."""
        super().__init__(generation, max_new_tokens, temperature)
        self.player = player

    def build_prompt(self, question, answer=None):
        return prompts.requirements_prompt(question.text, answer.text, self.player)

    def write(self, question, answer):
        """Return the requirements that follow from one answer, linked to it."""
        input_text, output = self.generate(question, answer)
        requirements = re.findall(r"The system shall .+?(?=\s*The system shall|\n|$)", output)
        return [
            GenerationRecord(id=f"{answer.id}-FR{number}", source=answer.id, text=text.strip(), prompt=input_text, data={"question": question.id})
            for number, text in enumerate(requirements, start=1)
        ]

    def run(self, questions, answers):
        by_id = {question.id: question for question in questions}
        return [record for answer in answers for record in self.write(by_id[answer.source], answer)]
