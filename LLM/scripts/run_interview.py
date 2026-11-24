"""Interview a persona and save its answers."""
import torch

from core.LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from pipeline.Interview import PersonaInterview, QuestionLoader, RecordStore, run_info
from scripts.options import DEFAULT_MODEL, QUESTIONS_FILE, argument_parser, output_folder
import logging

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)s | %(message)s",
    datefmt="%H:%M:%S"
)



def run_interview(model_name=DEFAULT_MODEL, persona_number=1, folder=None, seed=42):
    logging.info(f"Running interview for persona {persona_number} using model {model_name} with seed {seed}")
    logging.info(f"Loading questions from {QUESTIONS_FILE}")    
    folder = output_folder(persona_number, folder)
    logging.info(f"Output folder set to {folder}")
    questions = QuestionLoader().load(QUESTIONS_FILE)
    persona = PersonaLoader().load(persona_number)
    torch.manual_seed(seed)
    (folder / "log").mkdir(parents=True, exist_ok=True)

    model = LLMLoader().load(model_name)
    generation = LLMGeneration(model, persona, AnswerStore(folder / "log/interview.txt"))
    interview = PersonaInterview(generation)
    answers = interview.run(questions)

    info = {**run_info(generation, interview.temperature, seed), "persona": persona_number}
    destination = folder / "answers/interview.json"
    RecordStore().save(answers, destination, info)
    print(f"{len(answers)} interview answers saved in {destination}")
    return answers


if __name__ == "__main__":
    args = argument_parser(__doc__).parse_args()
    run_interview(**vars(args))
