from dataclasses import dataclass, field
from datetime import datetime
import json
import os
import re

from core.LLM import Persona

PROMPT_VERSION = "3"
NO_THINK = " /no_think"  # Qwen3 answers directly, without its <think> reasoning (faster, never cut in the middle)
REVIEW_STATUSES = ("to_review", "kept", "edited", "rejected")


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
    text = re.sub(r"^(Your answer|Answer|Ethan|Ethan Brooks)\s*:\s*", "", text, flags=re.IGNORECASE)
    return " ".join(line.strip() for line in text.splitlines() if line.strip())


class PersonaInterview:
    def __init__(self, generation, answers_per_question=3, max_new_tokens=500, temperature=0.8):
        """generation is an LLMGeneration with the persona to interview."""
        self.generation = generation
        self.answers_per_question = answers_per_question
        self.max_new_tokens = max_new_tokens
        self.temperature = temperature

    def build_input_text(self, question):
        return (
            "You are being interviewed by the team that makes a video game about climate change. "
            "They want to understand players like you. "
            "Answer as yourself, in the first person, honestly and in your own words, in at most four sentences. "
            "Your answer must be consistent with your biography, your quote and your climate behaviour. "
            "Do not change your opinions to please them, do not describe yourself in the third person "
            "and never use the word 'persona'.\n\n"
            f"Question: {question.text}\n"
            "Your answer:"
        )

    def ask(self, question):
        """Return several answers of the persona to one question."""
        input_text = self.build_input_text(question)
        records = []
        for number in range(1, self.answers_per_question + 1):
            answer = without_thinking(self.generation, input_text, max_new_tokens=self.max_new_tokens, temperature=self.temperature)
            records.append(GenerationRecord(id=f"{question.id}-A{number}", source=question.id, text=clean_answer(answer), prompt=input_text))
        return records

    def run(self, questions):
        return [record for question in questions for record in self.ask(question)]


GAME_GOAL = (
    "The game raises awareness about climate change: it helps skeptical players reconsider their views "
    "with facts from the IPCC report, without lecturing them."
)

ANALYST = Persona(number=0, data={
    "role": "requirements analyst for a video game that raises awareness about climate change",
    "task": "turn what a target player said in an interview into functional requirements of the game",
    "game_goal": GAME_GOAL,
    "rules": [
        "a functional requirement describes what the game does, not how it is coded",
        "each requirement starts with 'The system shall' and fits on one line",
        "a requirement serves the goal of the game: it never spreads a claim that contradicts the IPCC report",
        "use what the player said to find how to reach him (tone, trust, interest), not to repeat his opinions",
    ],
})


class RequirementWriter:
    def __init__(self, generation, player="Ethan Brooks, a confident climate skeptic", max_new_tokens=400, temperature=0.3):
        """generation is an LLMGeneration with the ANALYST persona."""
        self.generation = generation
        self.player = player
        self.max_new_tokens = max_new_tokens
        self.temperature = temperature

    def build_input_text(self, question, answer):
        return (
            f"Interview question: {question.text}\n"
            f"Answer of {self.player} (a target player of the game): {answer.text}\n\n"
            f"Goal of the game: {GAME_GOAL}\n"
            "Write one or two functional requirements that help the game reach this player, given his answer. "
            "Each requirement starts with \"The system shall\" and fits on one line. "
            "Write only the requirements, one per line."
        )

    def write(self, question, answer):
        """Return the requirements that follow from one answer, linked to it."""
        input_text = self.build_input_text(question, answer)
        output = without_thinking(self.generation, input_text, max_new_tokens=self.max_new_tokens, temperature=self.temperature)
        requirements = re.findall(r"The system shall .+?(?=\s*The system shall|\n|$)", output)
        return [
            GenerationRecord(id=f"{answer.id}-FR{number}", source=answer.id, text=text.strip(), prompt=input_text, data={"question": question.id})
            for number, text in enumerate(requirements, start=1)
        ]

    def run(self, questions, answers):
        by_id = {question.id: question for question in questions}
        return [record for answer in answers for record in self.write(by_id[answer.source], answer)]


SIDE_QUEST_FIELDS = ("Title", "Goal", "Steps", "Mechanic", "Climate message")
EXISTING_MECHANICS = "collect objects, choose items from a list, talk to a character, answer a quiz, walk to a place, the climbing mini-game"


def parse_side_quest(text):
    """Read the 'Field: value' lines of a side quest idea; the lines without a field continue the previous one."""
    quest = {name: "" for name in SIDE_QUEST_FIELDS}
    current = None
    for line in text.splitlines():
        match = re.match(r"^\W*(" + "|".join(SIDE_QUEST_FIELDS) + r")\W*:\s*(.*)$", line.strip(), flags=re.IGNORECASE)
        if match:
            current = next(field for field in SIDE_QUEST_FIELDS if field.lower() == match.group(1).lower())
            quest[current] = match.group(2).strip(" *_")  # the model often writes **Title:** in markdown
        elif current is not None and line.strip():
            quest[current] = (quest[current] + " " + line.strip()).strip()
    return quest


class SideQuestWriter:
    def __init__(self, generation, retriever, max_new_tokens=500, temperature=0.7):
        """generation is an LLMGeneration with the ANALYST persona, retriever finds the IPCC fact of the quest."""
        self.generation = generation
        self.retriever = retriever
        self.max_new_tokens = max_new_tokens
        self.temperature = temperature

    def build_input_text(self, question, answer):
        return (
            f"Interview question: {question.text}\n"
            f"Answer of a target player (a confident climate skeptic): {answer.text}\n\n"
            "Propose one small and easy side quest at the base camp of a mountain whose glacier has disappeared, "
            "that this player would enjoy. "
            f"It must only use mechanics the game already has: {EXISTING_MECHANICS}.\n"
            "Write exactly these five lines and nothing else:\n"
            + "\n".join(f"{name}: ..." for name in SIDE_QUEST_FIELDS)
        )

    def write(self, question, answer):
        input_text = self.build_input_text(question, answer)
        output = without_thinking(self.generation, input_text, max_new_tokens=self.max_new_tokens, temperature=self.temperature)
        quest = parse_side_quest(output)
        found = self.retriever.search(quest["Climate message"] or output, count=1)
        quest["ipcc_fact"] = found[0][0].id if found else ""
        return GenerationRecord(id=f"{answer.id}-SQ", source=answer.id, text=output.strip(), prompt=input_text, data=quest)

    def run(self, questions, answers, topic="quests"):
        by_id = {question.id: question for question in questions}
        return [self.write(by_id[answer.source], answer) for answer in answers if by_id[answer.source].topic == topic]
