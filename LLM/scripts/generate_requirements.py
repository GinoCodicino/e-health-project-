"""Generate functional requirements from a persona's saved interview."""
import torch

from core.LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from pipeline.Interview import RecordStore, RequirementWriter, run_info
from pipeline.prompts import ANALYST
from scripts.options import DEFAULT_MODEL, argument_parser, load_interview, output_folder


def generate_requirements(model_name=DEFAULT_MODEL, persona_number=1, folder=None, seed=42):
    # Get the interview we already generated 
    folder = output_folder(persona_number, folder)
    questions, answers = load_interview(folder, persona_number) # also checks that the saved interview belongs to this persona.
    persona = PersonaLoader().load(persona_number)

    # The seed helps us reproduce a run. Create the log folder if it doesn't exist yet.
    torch.manual_seed(seed)
    (folder / "log").mkdir(parents=True, exist_ok=True)

    # Load the model and give it the ANALYST role (defined in pipeline/prompts.py).
    # Here the LLM analyses the persona's answers; it is not acting as the persona.
    # AnswerStore keeps the prompts and raw responses in a text log so we can debug easily 
    model = LLMLoader().load(model_name)
    analyst = LLMGeneration(model, ANALYST, AnswerStore(folder / "log/requirements.txt"))

    # Tell the analyst who the interview is about
    player = f"{persona.data['name']} ({persona.data.get('archetype', 'target player')})"
    writer = RequirementWriter(analyst, player=player)

    # Beginning of the generation thanks to RequirementWriter in Interview.py
    # For each saved answer, run() finds its question and calls write().
    requirements = writer.run(questions, answers)

    # Save the results together with the model, temperature, seed, date and persona.
    info = {**run_info(analyst, writer.temperature, seed), "persona": persona_number}
    destination = folder / "answers/requirements.json"
    RecordStore().save(requirements, destination, info)
    print(f"{len(requirements)} requirements saved in {destination}")
    return requirements


if __name__ == "__main__":
    # Entry point when we run: python -m scripts.generate_requirements
    # The shared parser reads --model, --persona, --folder and --seed.
    parser = argument_parser(__doc__)
    args = parser.parse_args()
    # Turn the parsed options into named arguments for the function above.
    try:
        generate_requirements(**vars(args))
    except (FileNotFoundError, ValueError) as error:
        parser.exit(1, f"{error}\n")
