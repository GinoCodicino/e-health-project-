import re

from pipeline import prompts
from pipeline.Interview import GenerationRecord, Writer, clean_answer

# "1. Ethan shows ... (FR: Q3-A1-FR2, Q4-A1-FR1)"
IDEA_REGEX = re.compile(r"^\s*\d+[.)]\s*(.+)$", flags=re.MULTILINE)
REQUIREMENT_ID_REGEX = re.compile(r"Q\d+-A\d+-FR\d+")


def usable_requirements(requirements):
    """The requirements that were not rejected, as (id, text) with the reviewer's text when it was edited."""
    return [
        (requirement.id, requirement.edited_text or requirement.text)
        for requirement in requirements if requirement.status != "rejected"
    ]


class IntegrationWriter(Writer):
    def __init__(self, generation, max_ideas=3, max_new_tokens=600, temperature=0.3):
        """generation is an LLMGeneration with the ANALYST persona."""
        super().__init__(generation, max_new_tokens, temperature)
        self.max_ideas = max_ideas

    def build_prompt(self, question, answer=None):
        """question: the IPCC fact, answer: the requirements of the game."""
        return prompts.integration_prompt(question.text, usable_requirements(answer))

    def write(self, fact, requirements):
        """Return a few ideas to bring the fact into the game, each linked to the requirements it serves."""
        input_text, output = self.generate(fact, requirements)
        known = {requirement.id for requirement in requirements}
        ideas = IDEA_REGEX.findall(clean_answer_lines(output))[:self.max_ideas]
        return [
            GenerationRecord(
                id=f"{fact.id}-I{number}", source=fact.id, text=idea.strip(), prompt=input_text,
                data={"fact_text": fact.text, "requirements": [i for i in REQUIREMENT_ID_REGEX.findall(idea) if i in known]},
            )
            for number, idea in enumerate(ideas, start=1)
        ]


def clean_answer_lines(text):
    """Like clean_answer, but keeps one idea per line."""
    return "\n".join(clean_answer(line) for line in text.split("</think>")[-1].splitlines() if line.strip())
