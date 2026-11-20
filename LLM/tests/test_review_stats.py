"""
Tests of review_stats.py.

Run with: pytest tests/test_review_stats.py -v
"""
from pipeline.Interview import GenerationRecord, RecordStore
from scripts.review_stats import review_stats


def test_review_stats_counts_the_statuses(tmp_path):
    records = [
        GenerationRecord("Q1-A1", "Q1", "a", "p", status="kept"),
        GenerationRecord("Q1-A2", "Q1", "b", "p", status="rejected"),
        GenerationRecord("Q1-A3", "Q1", "c", "p"),
    ]
    RecordStore().save(records, str(tmp_path / "answers" / "interview.json"), {"model": "m"})

    stats = review_stats(str(tmp_path))

    assert stats == {"interview.json": {"to_review": 1, "kept": 1, "edited": 0, "rejected": 1}}
