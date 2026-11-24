"""Send a message to a persona, with relevant facts from the IPCC report."""
import torch

from core.LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from core.RAG import FactBase, Retriever
from core.interaction import interact
from scripts.options import DEFAULT_MODEL, FACT_FILE, argument_parser, output_folder

TEMPERATURE = 1
def run_interaction(message, model_name=DEFAULT_MODEL, persona_number=1, folder=None, seed=42):
    folder = output_folder(persona_number, folder)
    persona = PersonaLoader().load(persona_number)
    retriever = Retriever(FactBase.load(FACT_FILE).statements)
    torch.manual_seed(seed)
    (folder / "log").mkdir(parents=True, exist_ok=True)

    model = LLMLoader().load(model_name)
    generation = LLMGeneration(model, persona, AnswerStore(folder / "log/interaction.txt"))
    answer = interact(generation, retriever, message, temperature=TEMPERATURE)
    print(answer)
    return answer


if __name__ == "__main__":
    parser = argument_parser(__doc__)
    parser.add_argument("message", help="What you want to say to the persona")
    run_interaction(**vars(parser.parse_args()))
