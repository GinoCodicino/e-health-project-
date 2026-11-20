"""
Tests of interaction.py.

No real model is loaded: interact receives a fake LLMGeneration.

Run with: pytest tests/test_interaction.py -v
"""
from unittest.mock import MagicMock

from core.interaction import build_input_text, interact
from core.RAG import Retriever, Statement

STATEMENTS = [
    Statement("A.1", "Human activities have unequivocally caused global warming", 10, "{2.1}"),
    Statement("A.2", "Sea level rise and glacier retreat are observed in every region", 11, "{2.2}"),
    Statement("C.2", "Mitigation actions this decade reduce projected losses and damages", 30, "{4.1}"),
]


def test_build_input_text_has_player_sentence_and_facts_with_their_id():
    text = build_input_text("The glacier is gone", STATEMENTS[:2])

    assert 'Player: "The glacier is gone"' in text
    assert "[A.1] Human activities have unequivocally caused global warming" in text
    assert "[A.2] Sea level rise and glacier retreat are observed in every region" in text
    assert "[C.2]" not in text


def test_interact_sends_the_matching_facts_and_returns_the_answer():
    generation = MagicMock()
    generation.generate.return_value = "Hmm, maybe."

    answer = interact(generation, Retriever(STATEMENTS), "the glacier retreat", fact_count=1, max_new_tokens=50, temperature=0.1)

    assert answer == "Hmm, maybe."
    input_text = generation.generate.call_args.args[0]
    assert "[A.2]" in input_text and "[A.1]" not in input_text
    kwargs = generation.generate.call_args.kwargs
    assert (kwargs["max_new_tokens"], kwargs["temperature"]) == (50, 0.1)


def test_interact_without_matching_fact_still_answers():
    generation = MagicMock()
    generation.generate.return_value = "Whatever."

    assert interact(generation, Retriever(STATEMENTS), "football tournament") == "Whatever."
    assert "Facts from the IPCC report" in generation.generate.call_args.args[0]
