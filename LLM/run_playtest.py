import os

import torch

from Interview import RecordStore, run_info
from LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from Playtest import Playtest, PlaytestLoader, compare, experience_text

FOLDER = "Ethan"


def kept_dialogue_lines(folder):
    """The lines of the dialogue the team kept, in order: Ethan's belief, the player's answer, Ethan's reaction."""
    run, records = RecordStore().load(os.path.join(folder, "answers", "dialogue.json"))
    kept = [record for record in records if record.status in ("kept", "edited")]
    lines = []
    for record in kept:
        if record.source == "ending":
            lines.append(f"Ethan: {record.edited_text or record.text}")
        else:
            lines += [f"Ethan: {record.data['ethan_line']}", f"Player: {record.edited_text or record.text}", f"Ethan: {record.data['ethan_reaction']}"]
    return lines


def run_playtest(model_name, persona_number, ratings_per_statement=3, seed=42):
    """The persona rates the statements, plays (summary + kept dialogue), answers the questions and rates again."""
    lines = kept_dialogue_lines(FOLDER)
    if not lines:
        raise ValueError(f"No dialogue kept yet: set the status of the chosen variants to 'kept' in {FOLDER}/answers/dialogue.json")
    torch.manual_seed(seed)
    os.makedirs(os.path.join(FOLDER, "log"), exist_ok=True)
    tester = LLMGeneration(LLMLoader().load(model_name), PersonaLoader().load(persona_number), AnswerStore(os.path.join(FOLDER, "log", "playtest.txt")))
    play_summary, questions, statements = PlaytestLoader().load(os.path.join(FOLDER, "questions", "playtest.json"))
    experience = experience_text(play_summary, lines)

    playtest = Playtest(tester, ratings_per_statement)
    ratings = playtest.rate(statements, "before") + playtest.rate(statements, "after", experience)
    answers = playtest.ask(questions, experience)
    info = run_info(tester, playtest.temperature, seed)
    info["comparison"] = compare(ratings)
    RecordStore().save(ratings + answers, os.path.join(FOLDER, "answers", "playtest.json"), info)
    return info["comparison"]


if __name__ == "__main__":
    model_name = "Qwen/Qwen3-0.6B"
    persona_number = 1
    for statement, scores in run_playtest(model_name, persona_number).items():
        print(statement, scores)
