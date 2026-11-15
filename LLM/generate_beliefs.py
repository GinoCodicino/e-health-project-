import os

import torch

from Dialogues import RESEARCHER, BeliefWriter
from Interview import QuestionLoader, RecordStore, run_info
from LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from RAG import FactBase, Retriever

FOLDER = "Ethan"
FACT_FILE = "facts/ipcc_ar6_syr_spm.json"


def generate_beliefs(model_name, persona_number, variants=3, seed=42):
    """The persona says what he believes (one question per dimension), the researcher writes the words to search in the report."""
    torch.manual_seed(seed)
    os.makedirs(os.path.join(FOLDER, "log"), exist_ok=True)
    llm = LLMLoader().load(model_name)
    ethan = LLMGeneration(llm, PersonaLoader().load(persona_number), AnswerStore(os.path.join(FOLDER, "log", "beliefs.txt")))
    researcher = LLMGeneration(llm, RESEARCHER, AnswerStore(os.path.join(FOLDER, "log", "beliefs_queries.txt")))
    questions = QuestionLoader().load(os.path.join(FOLDER, "questions", "beliefs.json"))

    writer = BeliefWriter(ethan, researcher, Retriever(FactBase.load(FACT_FILE).statements), variants)
    records = writer.run(questions)
    RecordStore().save(records, os.path.join(FOLDER, "answers", "beliefs.json"), run_info(ethan, writer.temperature, seed))
    return records


if __name__ == "__main__":
    model_name = "Qwen/Qwen3-0.6B"
    persona_number = 1
    records = generate_beliefs(model_name, persona_number)
    print(f"{len(records)} beliefs saved in {FOLDER}/answers/beliefs.json, review them before generating the dialogue")
