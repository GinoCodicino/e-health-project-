from dataclasses import dataclass
import json
import re

from pipeline.Interview import GenerationRecord, clean_answer, without_thinking
from core.interaction import EXAMPLE_ANSWER, build_input_text
from core.LLM import Persona


@dataclass
class Belief:
    id: str
    dimension: str
    ethan_line: str
    query: str


class BeliefLoader:
    def load(self, file):
        """Load the persona's beliefs (id, dimension, what he says, words of the report that answer it)."""
        with open(file, "r") as f:
            return [Belief(**data) for data in json.load(f)]


def beliefs_from_records(records):
    """The beliefs the team kept (or edited) after the review of the generated ones."""
    return [
        Belief(id=record.id, dimension=record.data["dimension"], ethan_line=record.edited_text or record.text, query=record.data["query"])
        for record in records if record.status in ("kept", "edited")
    ]


RESEARCHER = Persona(number=0, data={
    "role": "climate researcher who knows the IPCC Sixth Assessment Report (AR6) well",
    "task": "write the keywords to search in the report to find the facts that answer a claim",
    "rules": ["write only keywords, with the words used in the IPCC report", "no sentence, no explanation"],
})


class BeliefWriter:
    def __init__(self, ethan, analyst, retriever, variants=3, fact_count=2, max_new_tokens=300, temperature=0.8):
        """ethan: LLMGeneration with Ethan's persona, who says what he believes;
        analyst: LLMGeneration with the RESEARCHER persona, who writes the words to search in the IPCC report."""
        self.ethan = ethan
        self.analyst = analyst
        self.retriever = retriever
        self.variants = variants
        self.fact_count = fact_count
        self.max_new_tokens = max_new_tokens
        self.temperature = temperature

    def belief_input_text(self, question):
        return (
            "A player of a video game meets you at the base camp of a mountain whose glacier has disappeared "
            "and asks you a question. Say what you really think, as yourself, in the first person, "
            "in one or two short sentences, the way you would say it to a stranger. "
            "Stay consistent with your biography, your quote and your climate behaviour.\n\n"
            f"Question: {question.text}\n"
            "You:"
        )

    def query_input_text(self, line):
        return (
            f"A climate skeptic says: \"{line}\"\n"
            "Write 8 to 12 keywords, with the words used in the IPCC report, to find the facts of the report "
            "that answer this claim. Write only the keywords, on one line, separated by spaces."
        )

    def write(self, question):
        """Several variants of what the persona believes about one question, each with its search words."""
        prompt = self.belief_input_text(question)
        records = []
        for number in range(1, self.variants + 1):
            line = clean_answer(without_thinking(self.ethan, prompt, max_new_tokens=self.max_new_tokens, temperature=self.temperature))
            query = clean_answer(without_thinking(self.analyst, self.query_input_text(line), max_new_tokens=self.max_new_tokens, temperature=0.3))
            facts = self.retriever.search(query or line, count=self.fact_count)
            records.append(GenerationRecord(
                id=f"{question.id}-V{number}", source=question.id, text=line, prompt=prompt,
                data={"dimension": question.topic, "query": query or line, "facts": [statement.id for statement, score in facts]},
            ))
        return records

    def run(self, questions):
        return [record for question in questions for record in self.write(question)]


# the three kinds of answers the player can choose
CHOICES = {
    "fact": "The player answers with one precise fact from the list, in simple words.",
    "question": "The player asks one short question that makes Ethan think about what he just said.",
    "empathy": "The player first shows understanding for Ethan's doubt, then gently adds one fact from the list.",
}
# how Ethan reacts to each kind of answer
REACTIONS = {
    "fact": "The fact is hard to deny: you hesitate a little, but you keep one doubt.",
    "question": "The question makes you think: you answer it honestly, a bit less sure of yourself.",
    "empathy": "The player is kind to you: you relax a little and you really listen.",
}
# Ethan's last words before the climb, depending on how skeptical he still is
ENDINGS = {
    "high": "You are still not convinced by the player.",
    "medium": "The player made you doubt a little, you are less sure than before.",
    "low": "You admit the player has a point, even if it is hard for you to say it.",
}
CLIMB = "The player is about to climb the mountain: wind from 30 m, rain from 60 m, a storm with lightning from 90 m, falling rocks all the way up."

SCRIPTWRITER = Persona(number=0, data={
    "role": "scriptwriter of a video game that raises awareness about climate change",
    "task": "write what the PLAYER says to Ethan Brooks, a confident climate skeptic",
    "rules": [
        "write only the words the player says, with no label like 'The player says:' and no quotes",
        "the player speaks to Ethan in the first person and calls him 'you', in one or two short sentences",
        "the player never repeats what Ethan just said and never agrees with a claim that contradicts the IPCC report",
        "rely only on the given facts of the IPCC report, never invent figures",
        "be respectful, never mock or lecture Ethan",
        "stay in the conversation: never answer like an assistant ('I can help with something else')",
    ],
})

# added to Ethan's own description when he is a character of the game, not someone interviewed
IN_GAME_RULES = [
    "you are Ethan Brooks, a character of the game, talking with the player at the base camp of the mountain",
    "you speak only as Ethan, in the first person, and you never talk about yourself in the third person",
    "you react to what the player just said: you never repeat your previous line word for word",
    "you stay a confident climate skeptic, but a good argument can make you hesitate",
    "your answers are short, direct and a bit dry: at most three sentences",
    "you are not an assistant, a coach or a therapist: never offer help or comfort",
]


def in_game(persona):
    """Ethan's persona with the rules of a game character added to its description."""
    return Persona(number=persona.number, data={**persona.data, "in_game_rules": IN_GAME_RULES})


def numbers_not_in(text, sources):
    """The numbers of the text that appear in none of the sources (a sign the model invented them)."""
    known = set(re.findall(r"\d+(?:\.\d+)?", " ".join(sources)))
    return sorted(set(re.findall(r"\d+(?:\.\d+)?", text)) - known)


def copies_example(text):
    """True if the answer repeats the example of the prompt instead of answering."""
    return EXAMPLE_ANSWER[:40].lower() in text.lower()


class DialogueWriter:
    def __init__(self, ethan, writer, retriever, variants=3, fact_count=2, player_advice="", max_new_tokens=400, temperature=0.7):
        """ethan: LLMGeneration with Ethan's persona; writer: LLMGeneration with the SCRIPTWRITER persona."""
        self.ethan = ethan
        self.writer = writer
        self.retriever = retriever
        self.variants = variants
        self.fact_count = fact_count
        self.player_advice = player_advice
        self.max_new_tokens = max_new_tokens
        self.temperature = temperature

    def player_input_text(self, belief, kind, statements):
        facts = "\n".join(f"[{statement.id}] {statement.text}" for statement in statements)
        advice = f"Ethan said in an interview how he wants to be talked to: \"{self.player_advice}\"\n" if self.player_advice else ""
        return (
            f"Ethan Brooks just said: \"{belief.ethan_line}\"\n"
            f"{advice}"
            f"Facts from the IPCC report:\n{facts}\n\n"
            f"Write what the player answers. {CHOICES[kind]}\n"
            "Write only the player's line."
        )

    def write_choice(self, belief, kind, statements, number):
        """One variant of a player's answer of this kind, and Ethan's reaction to it."""
        generate = dict(max_new_tokens=self.max_new_tokens, temperature=self.temperature)
        player_prompt = self.player_input_text(belief, kind, statements)
        player_line = clean_answer(without_thinking(self.writer, player_prompt, **generate))
        ethan_prompt = build_input_text(player_line, statements, said=belief.ethan_line, hint=REACTIONS[kind])
        reaction = clean_answer(without_thinking(self.ethan, ethan_prompt, **generate))
        facts_text = [statement.text for statement in statements]
        return GenerationRecord(
            id=f"{belief.id}-{kind}-V{number}",
            source=belief.id,
            text=player_line,
            prompt=player_prompt,
            data={
                "kind": kind,
                "ethan_line": belief.ethan_line,
                "ethan_reaction": reaction,
                "ethan_prompt": ethan_prompt,
                "facts": [statement.id for statement in statements],
                "unknown_numbers": numbers_not_in(player_line + " " + reaction, facts_text + [belief.ethan_line]),
                "copies_example": copies_example(reaction),
            },
        )

    def write_belief(self, belief):
        """All the variants of the three kinds of answers to one belief."""
        statements = [statement for statement, score in self.retriever.search(belief.query, count=self.fact_count)]
        return [self.write_choice(belief, kind, statements, number) for kind in CHOICES for number in range(1, self.variants + 1)]

    def ending_input_text(self, level):
        return (
            f"{CLIMB}\n{ENDINGS[level]}\n"
            "Say goodbye to the player and give one piece of advice for the climb, in your own voice, "
            "in the first person, in at most three short sentences."
        )

    def write_endings(self):
        """Variants of Ethan's last words for each level of skepticism."""
        records = []
        for level in ENDINGS:
            prompt = self.ending_input_text(level)
            for number in range(1, self.variants + 1):
                text = clean_answer(without_thinking(self.ethan, prompt, max_new_tokens=self.max_new_tokens, temperature=self.temperature))
                records.append(GenerationRecord(id=f"ending-{level}-V{number}", source="ending", text=text, prompt=prompt, data={"level": level}))
        return records

    def run(self, beliefs):
        return [record for belief in beliefs for record in self.write_belief(belief)] + self.write_endings()
