import os

from Interview import REVIEW_STATUSES, RecordStore

FOLDER = "Ethan"
FILES = ("interview.json", "requirements.json", "side_quests.json", "dialogue.json", "playtest.json")


def review_stats(folder):
    """For each file of answers: how many texts the team kept, edited, rejected or did not review yet."""
    stats = {}
    for name in FILES:
        file = os.path.join(folder, "answers", name)
        if not os.path.exists(file):
            continue
        run, records = RecordStore().load(file)
        counts = {status: 0 for status in REVIEW_STATUSES}
        for record in records:
            counts[record.status] = counts.get(record.status, 0) + 1
        stats[name] = counts
    return stats


if __name__ == "__main__":
    for name, counts in review_stats(FOLDER).items():
        total = sum(counts.values())
        print(f"{name}: {total} texts | " + " | ".join(f"{status} {count}" for status, count in counts.items()))
