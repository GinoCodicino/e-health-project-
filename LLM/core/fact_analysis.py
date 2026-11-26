"""Find the IPCC facts a conversation talks about: the facts of each line, then the ones that come back most often."""
from dataclasses import dataclass, field
import json
import os

from core.RAG import Statement


@dataclass
class Line:
    node_id: str
    speaker: str  # "player" or "persona"
    text: str


class ConversationReader:
    def read(self, file):
        """Return the lines of a conversation tree saved by scripts.interact, each node once."""
        with open(file, "r") as f:
            tree = json.load(f)["tree"]
        return self.lines(tree)

    def lines(self, node):
        own = [Line(node["id"], "player", node["player"]), Line(node["id"], "persona", node["persona"])]
        return own + [line for child in node["children"] for line in self.lines(child)]


@dataclass
class FactMatch:
    statement: Statement
    score: float
    rank: int  # 1 = the best fact for the line


class LineFactFinder:
    def __init__(self, retriever, count=3):
        self.retriever = retriever
        self.count = count

    def find(self, line):
        """The facts that best match one line, with their rank."""
        found = self.retriever.search(line.text, count=self.count)
        return [FactMatch(statement, score, rank) for rank, (statement, score) in enumerate(found, start=1)]


@dataclass
class RankedFact:
    statement: Statement
    count: int = 0
    total_rank: int = 0
    total_score: float = 0.0
    speakers: dict = field(default_factory=lambda: {"player": 0, "persona": 0})
    lines: list = field(default_factory=list)

    @property
    def mean_rank(self):
        return self.total_rank / self.count

    def add(self, line, match):
        self.count += 1
        self.total_rank += match.rank
        self.total_score += match.score
        self.speakers[line.speaker] += 1
        self.lines.append(f"{line.node_id}:{line.speaker}")


class FactRanker:
    def __init__(self, top=5):
        self.top = top

    def rank(self, matches_by_line):
        """The facts found for the most lines; ties go to the best ranked, then to the best scored."""
        facts = {}
        for line, matches in matches_by_line:
            for match in matches:
                facts.setdefault(match.statement.id, RankedFact(match.statement)).add(line, match)
        ordered = sorted(facts.values(), key=lambda fact: (-fact.count, fact.mean_rank, -fact.total_score))
        return ordered[:self.top]


class AnalysisStore:
    def save(self, file, matches_by_line, ranked):
        """Save the facts of each line and the facts kept for the whole conversation."""
        os.makedirs(os.path.dirname(file) or ".", exist_ok=True)
        content = {
            "per_line": [
                {
                    "node": line.node_id, "speaker": line.speaker, "text": line.text,
                    "facts": [{"id": match.statement.id, "score": round(match.score, 3), "rank": match.rank} for match in matches],
                }
                for line, matches in matches_by_line
            ],
            "top_facts": [
                {
                    "id": fact.statement.id, "text": fact.statement.text, "page": fact.statement.page,
                    "count": fact.count, "mean_rank": round(fact.mean_rank, 2), "total_score": round(fact.total_score, 3),
                    "speakers": fact.speakers, "lines": fact.lines,
                }
                for fact in ranked
            ],
        }
        with open(file, "w") as f:
            json.dump(content, f, indent=1, ensure_ascii=False)
