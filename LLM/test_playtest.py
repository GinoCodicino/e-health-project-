"""
Tests of Playtest.py and review_stats.py.

No real model is loaded: Playtest receives a fake LLMGeneration.

Run with: pytest test_playtest.py -v
"""
import json
from unittest.mock import MagicMock

from Interview import GenerationRecord, InterviewQuestion, RecordStore
from Playtest import Playtest, PlaytestLoader, RatingStatement, compare, experience_text, read_score
from review_stats import review_stats

STATEMENTS = [RatingStatement("S1", "trend", "The climate is not really warming faster than in the past.")]


def fake_generation(*answers):
    generation = MagicMock()
    generation.generate.side_effect = list(answers)
    generation.llm.name = "Qwen/Qwen3-0.6B"
    return generation


def test_playtest_loader_reads_the_file(tmp_path):
    file = tmp_path / "playtest.json"
    file.write_text(json.dumps({
        "play_summary": "You climbed.",
        "questions": [{"id": "T1", "topic": "climb", "text": "Hard?"}],
        "statements": [{"id": "S1", "dimension": "trend", "text": "Not warming."}],
    }))

    summary, questions, statements = PlaytestLoader().load(str(file))

    assert summary == "You climbed."
    assert questions == [InterviewQuestion("T1", "climb", "Hard?")]
    assert statements == [RatingStatement("S1", "trend", "Not warming.")]


def test_read_score_takes_the_first_number_from_1_to_7():
    assert read_score("6. I still think it's natural.") == 6
    assert read_score("I'd say 5 out of 7") == 5
    assert read_score("10 years ago... 3") == 3
    assert read_score("No idea.") is None


def test_rate_before_and_after_with_the_experience_in_the_prompt():
    generation = fake_generation("6. Natural.", "7", "4. Less sure.", "5")
    playtest = Playtest(generation, ratings_per_statement=2)
    experience = experience_text("You climbed.", ["Ethan: It's natural.", "Player: It's faster than ever."])

    before = playtest.rate(STATEMENTS, "before")
    after = playtest.rate(STATEMENTS, "after", experience)

    assert [record.id for record in before + after] == ["S1-before-R1", "S1-before-R2", "S1-after-R1", "S1-after-R2"]
    assert [record.data["score"] for record in before + after] == [6, 7, 4, 5]
    assert "You climbed." not in generation.generate.call_args_list[0].args[0]
    assert "Player: It's faster than ever." in generation.generate.call_args_list[2].args[0]
    assert compare(before + after) == {"S1": {"before": 6.5, "after": 4.5}}


def test_ask_the_questions_about_the_play():
    generation = fake_generation("4, the storm was hard.")

    answers = Playtest(generation).ask([InterviewQuestion("T1", "climb", "How difficult was the climb?")], "You climbed.")

    assert answers[0].id == "T1-A1"
    assert answers[0].data["score"] == 4
    assert "Question: How difficult was the climb?" in generation.generate.call_args.args[0]


def test_review_stats_counts_the_statuses(tmp_path):
    records = [
        GenerationRecord("Q1-A1", "Q1", "a", "p", status="kept"),
        GenerationRecord("Q1-A2", "Q1", "b", "p", status="rejected"),
        GenerationRecord("Q1-A3", "Q1", "c", "p"),
    ]
    RecordStore().save(records, str(tmp_path / "answers" / "interview.json"), {"model": "m"})

    stats = review_stats(str(tmp_path))

    assert stats == {"interview.json": {"to_review": 1, "kept": 1, "edited": 0, "rejected": 1}}
