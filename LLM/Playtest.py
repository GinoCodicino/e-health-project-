from dataclasses import dataclass
import json
import re

from Interview import GenerationRecord, InterviewQuestion, clean_answer, without_thinking


@dataclass
class RatingStatement:
    id: str
    dimension: str
    text: str


class PlaytestLoader:
    def load(self, file):
        """Return the summary of the play, the test questions and the statements to rate."""
        with open(file, "r") as f:
            data = json.load(f)
        questions = [InterviewQuestion(**question) for question in data["questions"]]
        statements = [RatingStatement(**statement) for statement in data["statements"]]
        return data["play_summary"], questions, statements


def read_score(text):
    """The first number from 1 to 7 of the answer, or None."""
    match = re.search(r"\b([1-7])\b", text)
    return int(match.group(1)) if match else None


def experience_text(play_summary, dialogue_lines):
    """What the persona lived in the game: the climb and the conversation at the base camp."""
    conversation = "\n".join(dialogue_lines)
    return f"{play_summary}\n\nBefore the climb, at the base camp, you had this conversation with the player:\n{conversation}"


class Playtest:
    def __init__(self, generation, ratings_per_statement=3, max_new_tokens=300, temperature=0.7):
        """generation is an LLMGeneration with the persona who tests the game."""
        self.generation = generation
        self.ratings_per_statement = ratings_per_statement
        self.max_new_tokens = max_new_tokens
        self.temperature = temperature

    def rating_input_text(self, statement, experience=""):
        played = f"{experience}\n\n" if experience else ""
        return (
            f"{played}Do you agree with this sentence? \"{statement.text}\"\n"
            "Answer as yourself with one number from 1 (strongly disagree) to 7 (strongly agree), "
            "then one short sentence to explain."
        )

    def rate(self, statements, moment, experience=""):
        """Several ratings of each statement, before or after playing (moment = 'before' or 'after')."""
        records = []
        for statement in statements:
            prompt = self.rating_input_text(statement, experience)
            for number in range(1, self.ratings_per_statement + 1):
                text = clean_answer(without_thinking(self.generation, prompt, max_new_tokens=self.max_new_tokens, temperature=self.temperature))
                records.append(GenerationRecord(
                    id=f"{statement.id}-{moment}-R{number}", source=statement.id, text=text, prompt=prompt,
                    data={"moment": moment, "dimension": statement.dimension, "score": read_score(text)},
                ))
        return records

    def question_input_text(self, question, experience):
        return (
            f"{experience}\n\n"
            "The team who makes the game asks you about what you just played. "
            "Answer as yourself, in the first person, honestly, in at most four sentences.\n\n"
            f"Question: {question.text}\n"
            "Your answer:"
        )

    def ask(self, questions, experience):
        records = []
        for question in questions:
            prompt = self.question_input_text(question, experience)
            text = clean_answer(without_thinking(self.generation, prompt, max_new_tokens=self.max_new_tokens, temperature=self.temperature))
            records.append(GenerationRecord(id=f"{question.id}-A1", source=question.id, text=text, prompt=prompt, data={"score": read_score(text)}))
        return records


def compare(ratings):
    """Average score of each statement before and after playing (higher = more skeptical)."""
    averages = {}
    for record in ratings:
        if record.data["score"] is not None:
            averages.setdefault(record.source, {}).setdefault(record.data["moment"], []).append(record.data["score"])
    return {
        statement: {moment: round(sum(scores) / len(scores), 2) for moment, scores in moments.items()}
        for statement, moments in averages.items()
    }
