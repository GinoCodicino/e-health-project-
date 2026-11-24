"""Shared command-line options and interview files."""
import argparse
from pathlib import Path
import shlex

from pipeline.Interview import QuestionLoader, RecordStore

DEFAULT_MODEL = "Qwen/Qwen3-1.7B"
QUESTIONS_FILE = "Ethan/questions/interview.json"
FACT_FILE = "facts/ipcc_ar6_syr_spm.json"


def argument_parser(description):
    parser = argparse.ArgumentParser(description=description)
    parser.add_argument("--model", dest="model_name", default=DEFAULT_MODEL)
    parser.add_argument("--persona", dest="persona_number", type=int, default=1)
    parser.add_argument("--folder", help="Folder for answers and logs")
    parser.add_argument("--seed", type=int, default=42)
    return parser


def output_folder(persona_number, folder=None):
    return Path(folder or ("Ethan" if persona_number == 1 else f"output_{persona_number}"))


def load_interview(folder, persona_number):
    file = folder / "answers/interview.json"
    if not file.exists():
        command = shlex.join([
            "python", "-m", "scripts.run_interview",
            "--persona", str(persona_number), "--folder", str(folder),
        ])
        raise FileNotFoundError(
            f"No saved interview for persona {persona_number} in {file}.\n"
            f"Generate its interview first:\n  {command}"
        )
    run, answers = RecordStore().load(file)
    if run.get("persona", 1) != persona_number:
        raise ValueError("This interview belongs to another persona. Choose its folder or generate a new interview.")
    return QuestionLoader().load(QUESTIONS_FILE), answers
