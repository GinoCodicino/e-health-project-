"""Find the IPCC facts a conversation talks about (3 per line, then the 5 that come back most often),
then ask the analyst for 2 to 3 ideas to bring the main fact into the game, given the functional requirements."""
import torch

from core.LLM import AnswerStore, LLMGeneration, LLMLoader
from core.RAG import FactBase, Retriever
from core.fact_analysis import AnalysisStore, ConversationReader, FactRanker, LineFactFinder
from pipeline.Integration import IntegrationWriter
from pipeline.Interview import RecordStore, run_info
from pipeline.prompts import ANALYST
from scripts.options import FACT_FILE, argument_parser, output_folder

MODEL = "Qwen/Qwen3-4B"


def extract_facts(conversation, output, facts_per_line=3, top=5):
    """The facts of each line, crossed to keep the top ones."""
    finder = LineFactFinder(Retriever(FactBase.load(FACT_FILE).statements), count=facts_per_line)
    matches_by_line = [(line, finder.find(line)) for line in ConversationReader().read(conversation)]
    ranked = FactRanker(top=top).rank(matches_by_line)
    AnalysisStore().save(output, matches_by_line, ranked)

    print(f"{len(matches_by_line)} lines analysed, facts saved in {output}")
    for fact in ranked:
        print(f"[{fact.statement.id}] {fact.count} lines (mean rank {fact.mean_rank:.2f}, {fact.speakers}): {fact.statement.text[:100]}...")
    return ranked


def integrate_fact(fact, folder, model_name=MODEL, seed=42):
    """Ideas to bring one fact into the game, linked to the requirements they serve."""
    _, requirements = RecordStore().load(folder / "answers/requirements.json")
    torch.manual_seed(seed)
    (folder / "log").mkdir(parents=True, exist_ok=True)
    analyst = LLMGeneration(LLMLoader().load(model_name), ANALYST, AnswerStore(folder / "log/integration.txt"))
    writer = IntegrationWriter(analyst)
    ideas = writer.write(fact, requirements)

    destination = folder / "answers/fact_integration.json"
    RecordStore().save(ideas, destination, {**run_info(analyst, writer.temperature, seed), "fact": fact.id})
    print(f"\nIdeas for [{fact.id}], saved in {destination}")
    for idea in ideas:
        print(f"- {idea.text}")
    return ideas


def analyse(model_name=MODEL, persona_number=1, folder=None, seed=42, conversation=None, facts_per_line=3, top=5, facts_only=False):
    folder = output_folder(persona_number, folder)
    ranked = extract_facts(conversation or folder / "answers/final_conversation.json", folder / "answers/rag_analysis.json", facts_per_line, top)
    if not facts_only and ranked:
        integrate_fact(ranked[0].statement, folder, model_name, seed)


if __name__ == "__main__":
    parser = argument_parser(__doc__)
    parser.set_defaults(model_name=MODEL)
    parser.add_argument("--conversation", help="Conversation tree (default: <folder>/answers/final_conversation.json)")
    parser.add_argument("--facts-per-line", type=int, default=3)
    parser.add_argument("--top", type=int, default=5, help="Number of facts kept for the whole conversation")
    parser.add_argument("--facts-only", action="store_true", help="Only find the facts, without the LLM")
    analyse(**vars(parser.parse_args()))
