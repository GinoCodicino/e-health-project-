"""
Tests of the classes of RAG.py.

No real PDF is read: StatementParser receives the text of fake pages.
The tests run in a temporary folder (the real fact base is not touched).

Run with: pytest tests/test_rag.py -v
"""
import pytest

from core.RAG import FactBase, Retriever, Statement, StatementParser, remove_footnote_marks, tidy_spaces, tokenize

# page with a headline statement (id alone on its line), a detailed statement, and footnotes
PAGE_A = """Summary for Policymakers

A. Current Status and Trends
Observed Warming and its Causes
A.1

Human activities have unequivocally caused global warming, with temperature reaching 1.1°C above
1850-1900 in 2011-2020 (high confidence). {2.1, Figure 2.1}

A.1.1 Global surface temperature was 1.09 [0.95 to 1.20]°C5 higher in 2011–2020 than 1850–19006, with
larger increases over land than over the ocean (high confidence). {2.1.1, Figure 2.1}

4

5

Ranges given throughout the SPM represent very likely ranges."""

# page with the page number right after the header, and a subsection title before the statement
PAGE_B = """Summary for Policymakers
12

B.1

Continued greenhouse gas emissions will lead to increasing global warming, with the best estimate of
reaching 1.5°C in the near term (high confidence). {3.1, Table 3.1} (Figure SPM.2)

Figure SPM.2: some text of the figure that must not be added to B.1

Climate Change Impacts and Climate-Related Risks
B.2

14
"""

# page where the page number is right before the id, and where a figure quotes an id without text
PAGE_C = """Summary for Policymakers

18
C.1
Climate change is a threat to human well-being and planetary health (very high confidence). There is a
rapidly closing window of opportunity to secure a liveable future. {3.1, 4.1}

B.1

B.1.1 Short
"""


def parse(*pages):
    return StatementParser().parse(list(pages))


def test_remove_footnote_marks_after_years_and_units():
    assert remove_footnote_marks("higher than 1850–19006 by 1.09°C5 and 59 GtCO2-eq9 in 2019") == (
        "higher than 1850–1900 by 1.09°C and 59 GtCO2-eq in 2019"
    )
    assert remove_footnote_marks("limiting warming to 2°C40 and 6.9 tCO2-eq per capita11 (excluding") == (
        "limiting warming to 2°C and 6.9 tCO2-eq per capita (excluding"
    )


def test_remove_footnote_marks_separated_by_a_space():
    assert remove_footnote_marks("1.20]°C 5 higher than 1850–1900 6, with responses incremental 18, sector") == (
        "1.20]°C higher than 1850–1900, with responses incremental, sector"
    )


def test_remove_footnote_marks_keeps_decimals_and_labels():
    text = "reaching 1.1°C above, exceed 1.5°C during, see Table 1, by 2050 and 0.28 m"
    assert remove_footnote_marks(text) == text


def test_remove_footnote_marks_keeps_real_numbers():
    text = "CO2 reached 410 parts per million, 2000 years, 1.5°C in 2050, SSP1-1.9, 1.3% yr-1, AR5"
    assert remove_footnote_marks(text) == text


def test_tidy_spaces_fixes_the_pdf_reader_spacing():
    assert tidy_spaces("risks ( very high confidence ) of 9 tCO 2-eq") == "risks (very high confidence) of 9 tCO2-eq"


def test_parse_headline_and_detail():
    statements = {statement.id: statement for statement in parse(PAGE_A)}

    assert list(statements) == ["A.1", "A.1.1"]
    assert statements["A.1"].is_headline()
    assert not statements["A.1.1"].is_headline()
    assert statements["A.1"].sources == "{2.1, Figure 2.1}"
    assert statements["A.1"].page == 1


def test_parse_removes_footnotes_marks_and_page_furniture():
    statements = {statement.id: statement for statement in parse(PAGE_A)}

    assert statements["A.1.1"].text == (
        "Global surface temperature was 1.09 [0.95 to 1.20]°C higher in 2011–2020 than 1850–1900, "
        "with larger increases over land than over the ocean (high confidence)."
    )
    assert "Ranges given" not in statements["A.1.1"].text
    assert "Summary for Policymakers" not in statements["A.1"].text


def test_parse_page_number_after_header_and_figure_text_not_added():
    statements = {statement.id: statement for statement in parse(PAGE_A, PAGE_B)}

    assert statements["B.1"].text.endswith("1.5°C in the near term (high confidence).")
    assert "figure" not in statements["B.1"].text
    # B.2 has no text on this page: it is not kept
    assert "B.2" not in statements


def test_parse_page_number_before_id_and_empty_mentions_ignored():
    statements = {statement.id: statement for statement in parse(PAGE_C)}

    assert list(statements) == ["C.1"]
    assert statements["C.1"].text.startswith("Climate change is a threat")


def test_parse_keeps_the_occurrence_with_a_text_and_sorts_by_id():
    ids = [statement.id for statement in parse(PAGE_C, PAGE_B, PAGE_A)]

    assert ids == ["A.1", "A.1.1", "B.1", "C.1"]


@pytest.fixture
def statements():
    return [
        Statement("A.1", "Human activities have unequivocally caused global warming", 10, "{2.1}"),
        Statement("A.2", "Sea level rise and glacier retreat are observed in every region", 11, "{2.2}"),
        Statement("C.2", "Mitigation actions this decade reduce projected losses and damages", 30, "{4.1}"),
    ]


def test_fact_base_save_and_load(statements, workdir):
    FactBase(statements).save("processed/facts.json")

    loaded = FactBase.load("processed/facts.json")

    assert loaded.statements == statements
    assert loaded.get("A.2").page == 11


def test_tokenize_lowercase_without_stop_words_and_plurals():
    assert tokenize("The Emissions of the glaciers are rising") == ["emission", "glacier", "rising"]


def test_retriever_best_statement_first(statements):
    retriever = Retriever(statements)

    results = retriever.search("who causes global warming? human activities", count=2)

    assert [statement.id for statement, score in results] == ["A.1"]
    assert results[0][1] > 0


def test_retriever_orders_by_relevance_and_limits_count(statements):
    retriever = Retriever(statements)

    results = retriever.search("glacier retreat and sea level, losses", count=2)

    assert [statement.id for statement, score in results] == ["A.2", "C.2"]
    assert results[0][1] > results[1][1]


def test_retriever_no_match_returns_nothing(statements):
    assert Retriever(statements).search("football tournament") == []


@pytest.fixture
def workdir(tmp_path, monkeypatch):
    monkeypatch.chdir(tmp_path)
    return tmp_path
