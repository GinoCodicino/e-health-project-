
from dataclasses import dataclass
import json
import math
import os
import re


@dataclass
class Statement:
    id: str
    text: str
    page: int
    sources: str = ""

    def is_headline(self):
        """A.1 is a headline statement, A.1.1 is one of its detailed statements."""
        return self.id.count(".") == 1


HEADER = "Summary for Policymakers"
STATEMENT_START_REGEX = re.compile(r"^([A-C]\.\d(?:\.\d)?)\s*(.*)$")
SECTION_TITLE_REGEX = re.compile(r"^[A-D]\.\s+[A-Z]")
FIGURE_START_REGEX = re.compile(r"^(Figure|Box|Table) SPM")
BARE_NUMBER_REGEX = re.compile(r"^\d{1,3}$")
# a footnote starts with its number and a word: "5 Ranges given throughout the SPM ..."
FOOTNOTE_START_REGEX = re.compile(r"^\d{1,2} [A-Z][a-z]{2,}")
SOURCES_REGEX = re.compile(r"\{[^{}]*\}")
# the PDF glues footnote numbers to the previous word: "1850-19006", "1.2°C5", "GtCO2-eq9", "per capita11"
FOOTNOTE_AFTER_YEAR_REGEX = re.compile(r"\b((?:18|19|20)\d{2})\d\b")
FOOTNOTE_AFTER_UNIT_REGEX = re.compile(r"(?<=°C)\d{1,2}\b|(?<=-eq)\d{1,2}\b")
FOOTNOTE_AFTER_WORD_REGEX = re.compile(r"(?<=[a-z]{3})\d{1,2}\b")
# sometimes with a space: "1.09°C 5 higher", "than 1850–1900 6, with", "incremental 18, sector"
END_OF_FOOTNOTE = r"(?=[\s,;)]|\.(?!\d))"  # a space or a punctuation, but not the dot of 1.5
FOOTNOTE_AFTER_SPACE_REGEX = re.compile(r"(?:(?<=°C)|(?<=\d{4})) \d{1,2}" + END_OF_FOOTNOTE)
FOOTNOTE_AFTER_LOWERCASE_WORD_REGEX = re.compile(r"\b([a-z]{3,}) \d{1,2}(?=[,;)]|\.(?!\d))")


def remove_footnote_marks(text):
    """Remove the footnote numbers glued to years and units (best effort, check the PDF page)."""
    text = FOOTNOTE_AFTER_YEAR_REGEX.sub(r"\1", text)
    text = FOOTNOTE_AFTER_UNIT_REGEX.sub("", text)
    text = FOOTNOTE_AFTER_WORD_REGEX.sub("", text)
    text = FOOTNOTE_AFTER_SPACE_REGEX.sub("", text)
    return FOOTNOTE_AFTER_LOWERCASE_WORD_REGEX.sub(r"\1", text)


def tidy_spaces(text):
    """Fix the spaces added by the PDF reader: '( high confidence )' and 'CO 2'."""
    text = re.sub(r"\(\s+", "(", text)
    text = re.sub(r"\s+\)", ")", text)
    return text.replace("CO 2", "CO2")


class PDFReader:
    def read(self, pdf_file):
        """Return the text of each page of the PDF."""
        import logging

        from pypdf import PdfReader  # only needed to build the fact base, not to search it

        logging.getLogger("pypdf").setLevel(logging.ERROR)  # it warns about fonts, the text is fine

        return [page.extract_text() for page in PdfReader(pdf_file).pages]


class StatementParser:
    MIN_WORDS = 10

    def parse(self, pages):
        """Return the numbered statements (A.1, A.1.1 ...) found in the pages, in order."""
        found = []
        current = None
        for page_number, page in enumerate(pages, start=1):
            lines = self.body_lines(page)
            for index, line in enumerate(lines):
                match = STATEMENT_START_REGEX.match(line)
                if match:
                    current = Statement(id=match.group(1), text=match.group(2), page=page_number)
                    found.append(current)
                elif SECTION_TITLE_REGEX.match(line) or FIGURE_START_REGEX.match(line) or self.is_title(lines, index):
                    current = None
                elif current is not None:
                    current.text += " " + line
        return self.keep_real_statements([self.finish(statement) for statement in found])

    def body_lines(self, page):
        """The lines of a page without header, page number and footnotes (they start at the first bare number)."""
        lines = [line.strip() for line in page.splitlines() if line.strip()]
        body = [line for index, line in enumerate(lines) if line != HEADER and not self.is_page_number(lines, index)]
        for index, line in enumerate(body):
            if BARE_NUMBER_REGEX.match(line) or FOOTNOTE_START_REGEX.match(line):
                return body[:index]
        return body

    def is_page_number(self, lines, index):
        """A bare number next to the header or to a statement id is the page number, not a footnote."""
        if not BARE_NUMBER_REGEX.match(lines[index]):
            return False
        before = lines[index - 1] if index > 0 else ""
        after = lines[index + 1] if index + 1 < len(lines) else ""
        next_to_header = HEADER in (before, after)
        return next_to_header or bool(STATEMENT_START_REGEX.match(before)) or bool(STATEMENT_START_REGEX.match(after))

    def keep_real_statements(self, found):
        """A figure can quote an id (B.1) with no text: keep the first occurrence of each id that has a real text."""
        statements = {}
        for statement in found:
            if len(statement.text.split()) >= self.MIN_WORDS:
                statements.setdefault(statement.id, statement)
        return sorted(statements.values(), key=lambda statement: [int(n) if n.isdigit() else n for n in re.split(r"[.]", statement.id)])

    def is_title(self, lines, index):
        """A short line without final dot, right before a headline statement, is a subsection title."""
        line = lines[index]
        if len(line) > 80 or line.endswith((".", "}", ")")):
            return False
        following = STATEMENT_START_REGEX.match(lines[index + 1]) if index + 1 < len(lines) else None
        return bool(following) and following.group(1).count(".") == 1

    def finish(self, statement):
        """The statement stops at its sources {2.1.1}: what comes after is a figure or a box."""
        text = re.sub(r"\s+", " ", statement.text).strip()
        sources = SOURCES_REGEX.search(text)
        if sources:
            statement.sources = sources.group(0)
            text = text[:sources.start()]
        statement.text = tidy_spaces(remove_footnote_marks(text)).strip()
        return statement


class FactBase:
    def __init__(self, statements):
        self.statements = statements

    @classmethod
    def from_pdf(cls, pdf_file):
        """Read the PDF and keep its numbered statements."""
        return cls(StatementParser().parse(PDFReader().read(pdf_file)))

    @classmethod
    def load(cls, file):
        """Load a fact base saved with save()."""
        with open(file, "r") as f:
            return cls([Statement(**data) for data in json.load(f)])

    def save(self, file):
        os.makedirs(os.path.dirname(file) or ".", exist_ok=True)
        with open(file, "w") as f:
            json.dump([statement.__dict__ for statement in self.statements], f, indent=1)

    def get(self, statement_id):
        return next(statement for statement in self.statements if statement.id == statement_id)


STOP_WORDS = {
    "a", "an", "and", "are", "as", "at", "be", "been", "by", "for", "from", "has", "have", "in", "is", "it",
    "its", "of", "on", "or", "that", "the", "their", "this", "to", "was", "were", "which", "with",
    # words of a conversation, they say nothing about the topic
    "about", "all", "but", "can", "come", "do", "does", "i", "if", "me", "my", "no", "not", "so", "than",
    "they", "we", "what", "when", "will", "would", "you", "your", "used", "stupid", "really", "just",
}


def tokenize(text):
    """Lowercase words without stop words, with a plural 's' removed (warming, emissions -> emission)."""
    words = re.findall(r"[a-z0-9]{2,}", text.lower())
    return [word[:-1] if len(word) > 3 and word.endswith("s") else word for word in words if word not in STOP_WORDS]


class Retriever:
    # core parameters for the RAG !!!
    K1 = 1.5
    B = 0.75

    def __init__(self, statements):
        """Index the statements (BM25: the words rare in the report weigh more)."""
        self.statements = statements
        self.words = [tokenize(statement.text) for statement in statements]
        self.average_length = sum(len(words) for words in self.words) / max(len(self.words), 1)
        self.frequency = {}
        for words in self.words:
            for word in set(words):
                self.frequency[word] = self.frequency.get(word, 0) + 1

    def search(self, query, count=3):
        """Return the (statement, score) pairs that best match the query, best first."""
        scores = [(statement, self.score(tokenize(query), words)) for statement, words in zip(self.statements, self.words)]
        scores = [pair for pair in scores if pair[1] > 0]
        return sorted(scores, key=lambda pair: pair[1], reverse=True)[:count]

    def score(self, query_words, words):
        score = 0.0
        for word in set(query_words):
            if word not in self.frequency:
                continue
            idf = math.log(1 + (len(self.statements) - self.frequency[word] + 0.5) / (self.frequency[word] + 0.5))
            count = words.count(word)
            length = 1 - self.B + self.B * len(words) / self.average_length
            score += idf * count * (self.K1 + 1) / (count + self.K1 * length)
        return score
