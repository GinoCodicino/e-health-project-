"""
Tests of Dialogues.py.

No real model is loaded: DialogueWriter receives fake LLMGeneration objects.

Run with: pytest test_dialogues.py -v
"""
import json
from unittest.mock import MagicMock

from Dialogues import (
    CHOICES, ENDINGS, Belief, BeliefLoader, BeliefWriter, DialogueWriter, beliefs_from_records, copies_example, in_game,
    numbers_not_in,
)
from LLM import Persona
from Interview import GenerationRecord, InterviewQuestion
from interaction import EXAMPLE_ANSWER
from RAG import Retriever, Statement

BELIEF = Belief("attribution", "attribution", "Nobody can prove it's us.", "human-caused warming")
STATEMENTS = [
    Statement("A.1.2", "The human-caused warming is 1.07°C, natural drivers changed it by -0.1°C to +0.1°C", 10),
    Statement("C.2", "Mitigation this decade reduces losses", 30),
]


def fake_generation(text):
    generation = MagicMock()
    generation.generate.return_value = text
    generation.llm.name = "Qwen/Qwen3-0.6B"
    return generation


def test_belief_loader_reads_json(tmp_path):
    file = tmp_path / "beliefs.json"
    file.write_text(json.dumps([BELIEF.__dict__]))

    assert BeliefLoader().load(str(file)) == [BELIEF]


def test_belief_writer_gets_the_belief_from_the_persona_and_the_query_from_the_analyst():
    ethan = fake_generation('"Nobody can prove the warming is human-caused."')
    analyst = fake_generation("human-caused warming natural drivers")
    question = InterviewQuestion("B2", "attribution", "Who is causing the warming?")

    records = BeliefWriter(ethan, analyst, Retriever(STATEMENTS), variants=2).run([question])

    assert [record.id for record in records] == ["B2-V1", "B2-V2"]
    assert records[0].text == "Nobody can prove the warming is human-caused."
    assert records[0].data == {"dimension": "attribution", "query": "human-caused warming natural drivers", "facts": ["A.1.2"]}
    assert "Question: Who is causing the warming?" in ethan.generate.call_args.args[0]
    assert "Nobody can prove the warming is human-caused." in analyst.generate.call_args.args[0]


def test_only_the_kept_or_edited_beliefs_go_in_the_dialogue():
    records = [
        GenerationRecord("B1-V1", "B1", "It always changed.", "p", data={"dimension": "trend", "query": "q1"}, status="kept"),
        GenerationRecord("B1-V2", "B1", "bad", "p", data={"dimension": "trend", "query": "q2"}, status="rejected"),
        GenerationRecord("B2-V1", "B2", "old", "p", data={"dimension": "attribution", "query": "q3"}, status="edited", edited_text="It's the sun."),
        GenerationRecord("B2-V2", "B2", "x", "p", data={"dimension": "attribution", "query": "q4"}),
    ]

    assert beliefs_from_records(records) == [
        Belief("B1-V1", "trend", "It always changed.", "q1"),
        Belief("B2-V1", "attribution", "It's the sun.", "q3"),
    ]


def test_numbers_not_in_finds_invented_figures():
    assert numbers_not_in("Humans caused 1.07°C, and 95% of it", ["warming is 1.07°C"]) == ["95"]
    assert numbers_not_in("No figure here", ["1.07"]) == []


def test_copies_example_detects_the_prompt_example():
    assert copies_example("Sure. " + EXAMPLE_ANSWER)
    assert not copies_example("I still have doubts about it.")


def test_write_belief_gives_each_kind_of_answer_with_ethan_reaction():
    writer = fake_generation('"Humans caused 1.07°C of warming."')
    ethan = fake_generation("Hmm. Maybe, but 50 years is short.")

    records = DialogueWriter(ethan, writer, Retriever(STATEMENTS), variants=2).write_belief(BELIEF)

    assert [record.id for record in records] == [f"attribution-{kind}-V{n}" for kind in CHOICES for n in (1, 2)]
    record = records[0]
    assert record.text == "Humans caused 1.07°C of warming."
    assert record.data["ethan_reaction"] == "Hmm. Maybe, but 50 years is short."
    assert record.data["facts"][0] == "A.1.2"
    # 1.07 is in the facts, 50 is not
    assert record.data["unknown_numbers"] == ["50"]
    # the player's line answers Ethan's belief, Ethan reacts to the player's line
    assert "Nobody can prove it's us." in writer.generate.call_args_list[0].args[0]
    ethan_prompt = ethan.generate.call_args_list[0].args[0]
    assert 'Player: "Humans caused 1.07°C of warming."' in ethan_prompt
    assert 'You just said: "Nobody can prove it\'s us."' in ethan_prompt


def test_player_advice_from_the_interview_goes_in_the_writer_prompt():
    writer = fake_generation("line")
    dialogue_writer = DialogueWriter(fake_generation("reaction"), writer, Retriever(STATEMENTS), variants=1, player_advice="Don't preach.")

    dialogue_writer.write_belief(BELIEF)

    assert "Don't preach." in writer.generate.call_args.args[0]


def test_endings_have_variants_for_each_level_of_skepticism():
    ethan = fake_generation("Good luck up there.")

    records = DialogueWriter(ethan, fake_generation(""), Retriever(STATEMENTS), variants=2).write_endings()

    assert [record.data["level"] for record in records] == [level for level in ENDINGS for n in (1, 2)]
    assert records[0].source == "ending"
    assert "storm" in ethan.generate.call_args.args[0]


def test_in_game_adds_the_rules_of_a_game_character_to_the_persona():
    persona = Persona(number=1, data={"name": "Ethan Brooks"})

    ethan = in_game(persona)

    assert ethan.data["name"] == "Ethan Brooks"
    assert "never repeat your previous line" in " ".join(ethan.data["in_game_rules"])
    assert "in_game_rules" not in persona.data  # the loaded persona is not changed
