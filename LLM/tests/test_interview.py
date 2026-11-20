"""
Tests of Interview.py.

No real model is loaded: the classes receive a fake LLMGeneration.
The tests run in a temporary folder.

Run with: pytest tests/test_interview.py -v
"""
import json
from unittest.mock import MagicMock

import pytest

from pipeline.Interview import (
    GenerationRecord, InterviewQuestion, PersonaInterview, QuestionLoader, RecordStore, RequirementWriter,
    SideQuestWriter, clean_answer, parse_side_quest, run_info,
)
from core.RAG import Retriever, Statement

QUESTIONS = [
    InterviewQuestion("Q1", "attitude", "Would you play a game about climate change?"),
    InterviewQuestion("Q10", "quests", "What small challenge at the base camp would make you think?"),
]


@pytest.fixture
def workdir(tmp_path, monkeypatch):
    monkeypatch.chdir(tmp_path)
    return tmp_path


def fake_generation(*answers):
    generation = MagicMock()
    generation.generate.side_effect = list(answers)
    generation.llm.name = "Qwen/Qwen3-0.6B"
    return generation


def test_question_loader_reads_json(workdir):
    (workdir / "questions.json").write_text(json.dumps([{"id": "Q1", "topic": "attitude", "text": "Why?"}]))

    assert QuestionLoader().load("questions.json") == [InterviewQuestion("Q1", "attitude", "Why?")]


def test_clean_answer_removes_quotes_labels_and_new_lines():
    assert clean_answer('  "Answer: I drive a lot.\n\nAnd I fly."  ') == "I drive a lot. And I fly."


def test_clean_answer_drops_the_reasoning_of_the_model():
    assert clean_answer("<think>Let me see</think>\nIt's natural.") == "It's natural."
    # reasoning cut before its end: there is no answer
    assert clean_answer("<think>Okay, the user wants me to") == ""


def test_the_thinking_of_qwen3_is_switched_off():
    generation = fake_generation("answer")

    PersonaInterview(generation, answers_per_question=1).run(QUESTIONS[:1])

    assert generation.generate.call_args.args[0].endswith(" /no_think")


def test_interview_asks_several_answers_per_question():
    generation = fake_generation("first", "second", "third", "fourth")

    answers = PersonaInterview(generation, answers_per_question=2, temperature=0.8).run(QUESTIONS)

    assert [answer.id for answer in answers] == ["Q1-A1", "Q1-A2", "Q10-A1", "Q10-A2"]
    assert [answer.source for answer in answers] == ["Q1", "Q1", "Q10", "Q10"]
    assert answers[1].text == "second"
    assert answers[0].status == "to_review"
    # the persona answers as itself, to this question
    prompt = generation.generate.call_args_list[0].args[0]
    assert "Question: Would you play a game about climate change?" in prompt
    assert "first person" in prompt
    assert generation.generate.call_args_list[0].kwargs["temperature"] == 0.8


def test_requirement_writer_keeps_only_the_requirements_linked_to_the_answer():
    answer = GenerationRecord("Q1-A1", "Q1", "Only if it does not lecture me.", "prompt")
    generation = fake_generation(
        "Here are the requirements:\n1. The system shall never lecture the player.\n"
        "- The system shall let the player choose his answers.\nThanks"
    )

    requirements = RequirementWriter(generation).run(QUESTIONS, [answer])

    assert [requirement.text for requirement in requirements] == [
        "The system shall never lecture the player.",
        "The system shall let the player choose his answers.",
    ]
    assert [requirement.id for requirement in requirements] == ["Q1-A1-FR1", "Q1-A1-FR2"]
    assert requirements[0].source == "Q1-A1"
    assert requirements[0].data == {"question": "Q1"}
    assert "Only if it does not lecture me." in generation.generate.call_args.args[0]
    assert "Goal of the game:" in generation.generate.call_args.args[0]


def test_requirement_writer_splits_two_requirements_on_one_line():
    answer = GenerationRecord("Q1-A1", "Q1", "No lecture.", "prompt")
    generation = fake_generation("The system shall show sources. The system shall let the player ask questions.")

    requirements = RequirementWriter(generation).run(QUESTIONS, [answer])

    assert [requirement.text for requirement in requirements] == [
        "The system shall show sources.",
        "The system shall let the player ask questions.",
    ]


def test_parse_side_quest_reads_the_fields():
    text = "**Title:** Last snow\nGoal: find the snow patches\nSteps: walk, count\nMechanic: collect objects\nClimate message: the glacier is gone"

    quest = parse_side_quest(text)

    assert quest == {
        "Title": "Last snow", "Goal": "find the snow patches", "Steps": "walk, count",
        "Mechanic": "collect objects", "Climate message": "the glacier is gone",
    }


def test_parse_side_quest_steps_on_several_lines():
    text = "Title: Last ice\nSteps:\n1. Walk to the old glacier.\n2. Count the snow patches.\nMechanic: walk to a place"

    quest = parse_side_quest(text)

    assert quest["Steps"] == "1. Walk to the old glacier. 2. Count the snow patches."
    assert quest["Mechanic"] == "walk to a place"


def test_parse_side_quest_missing_fields_stay_empty():
    assert parse_side_quest("Title: Only a title")["Goal"] == ""


def test_side_quest_writer_only_uses_quest_answers_and_links_an_ipcc_fact():
    answers = [
        GenerationRecord("Q1-A1", "Q1", "No.", "prompt"),
        GenerationRecord("Q10-A1", "Q10", "Something with the ice.", "prompt"),
    ]
    retriever = Retriever([
        Statement("A.2.3", "Losses driven by the retreat of glaciers in mountain ecosystems", 12),
        Statement("C.2", "Mitigation this decade reduces losses", 30),
    ])
    generation = fake_generation("Title: Last ice\nGoal: g\nSteps: s\nMechanic: walk to a place\nClimate message: glaciers retreat")

    quests = SideQuestWriter(generation, retriever).run(QUESTIONS, answers)

    assert [quest.id for quest in quests] == ["Q10-A1-SQ"]
    assert quests[0].data["Title"] == "Last ice"
    assert quests[0].data["ipcc_fact"] == "A.2.3"
    assert "collect objects" in generation.generate.call_args.args[0]


def test_record_store_saves_records_with_run_info(workdir):
    generation = fake_generation()
    info = run_info(generation, 0.8, 42)
    records = [GenerationRecord("Q1-A1", "Q1", "text", "prompt")]

    RecordStore().save(records, "answers/interview.json", info)
    loaded_info, loaded = RecordStore().load("answers/interview.json")

    assert loaded == records
    assert (loaded_info["model"], loaded_info["temperature"], loaded_info["seed"]) == ("Qwen/Qwen3-0.6B", 0.8, 42)
    assert "generated_at" in loaded_info and "prompt_version" in loaded_info
