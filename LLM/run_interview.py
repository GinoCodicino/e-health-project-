import os

import torch

from Interview import ANALYST, PersonaInterview, QuestionLoader, RecordStore, RequirementWriter, SideQuestWriter, run_info
from LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from RAG import FactBase, Retriever

FOLDER = "Ethan"
FACT_FILE = "facts/ipcc_ar6_syr_spm.json"


def load_models(model_name, persona_number):
    llm = LLMLoader().load(model_name)
    persona = PersonaLoader().load(persona_number)
    interviewee = LLMGeneration(llm, persona, AnswerStore(os.path.join(FOLDER, "log", "interview.txt")))
    analyst = LLMGeneration(llm, ANALYST, AnswerStore(os.path.join(FOLDER, "log", "analyst.txt")))
    return interviewee, analyst


def run_interview(interviewee, question_ids=None, answers_per_question=3, seed=42):
    """The persona answers the interview questions."""
    torch.manual_seed(seed)
    questions = QuestionLoader().load(os.path.join(FOLDER, "questions", "interview.json"))
    if question_ids:
        questions = [question for question in questions if question.id in question_ids]
    interview = PersonaInterview(interviewee, answers_per_question)
    answers = interview.run(questions)
    RecordStore().save(answers, os.path.join(FOLDER, "answers", "interview.json"), run_info(interviewee, interview.temperature, seed))
    return answers


def run_analysis(analyst, seed=42):
    """Turn the saved answers of the interview into functional requirements and side quest ideas."""
    torch.manual_seed(seed)
    store = RecordStore()
    questions = QuestionLoader().load(os.path.join(FOLDER, "questions", "interview.json"))
    run, answers = store.load(os.path.join(FOLDER, "answers", "interview.json"))

    writer = RequirementWriter(analyst)
    requirements = writer.run(questions, answers)
    store.save(requirements, os.path.join(FOLDER, "answers", "requirements.json"), run_info(analyst, writer.temperature, seed))

    quest_writer = SideQuestWriter(analyst, Retriever(FactBase.load(FACT_FILE).statements))
    side_quests = quest_writer.run(questions, answers)
    store.save(side_quests, os.path.join(FOLDER, "answers", "side_quests.json"), run_info(analyst, quest_writer.temperature, seed))
    return requirements, side_quests


if __name__ == "__main__":
    model_name = "Qwen/Qwen3-0.6B"
    persona_number = 1
    os.makedirs(os.path.join(FOLDER, "log"), exist_ok=True)
    interviewee, analyst = load_models(model_name, persona_number)
    answers = run_interview(interviewee)
    requirements, side_quests = run_analysis(analyst)
    print(f"{len(answers)} answers, {len(requirements)} requirements, {len(side_quests)} side quest ideas saved in {FOLDER}/answers")
