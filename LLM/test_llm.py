"""
Tests des classes de LLM.py.

Aucun vrai modèle n'est chargé : LLMGeneration reçoit un faux LoadedLLM.
Les tests tournent dans un dossier temporaire (les vrais output_* ne bougent pas).

Lancer avec : pytest test_llm.py -v
"""
import json
from unittest.mock import MagicMock

import pytest
import torch

import LLM
from LLM import AnswerStore, LLMGeneration, LLMLoader, LoadedLLM, Persona, PersonaLoader

PERSONA_DATA = {"id": "persona_99", "name": "Test Person"}


@pytest.fixture(autouse=True)
def workdir(tmp_path, monkeypatch):
    monkeypatch.chdir(tmp_path)
    return tmp_path


def test_llm_loader_loads_model_and_tokenizer(monkeypatch):
    model_loader = MagicMock(return_value="model")
    tokenizer_loader = MagicMock(return_value="tokenizer")
    monkeypatch.setattr(LLM.AutoModelForCausalLM, "from_pretrained", model_loader)
    monkeypatch.setattr(LLM.AutoTokenizer, "from_pretrained", tokenizer_loader)

    llm = LLMLoader().load("Qwen/Qwen3-0.6B")

    assert llm == LoadedLLM(name="Qwen/Qwen3-0.6B", model="model", tokenizer="tokenizer")
    model_loader.assert_called_once_with("Qwen/Qwen3-0.6B")
    tokenizer_loader.assert_called_once_with("Qwen/Qwen3-0.6B")


def test_persona_loader_reads_json(workdir):
    (workdir / "personas").mkdir()
    (workdir / "personas" / "persona_99.json").write_text(json.dumps(PERSONA_DATA))

    assert PersonaLoader().load(99) == Persona(number=99, data=PERSONA_DATA)


def test_persona_loader_missing_file_raises():
    with pytest.raises(FileNotFoundError):
        PersonaLoader().load(12345)


def test_persona_system_prompt():
    assert Persona(1, PERSONA_DATA).system_prompt() == (
        "here the persona's description you have to embody : \n"
        f"{json.dumps(PERSONA_DATA)}\n"
    )


def test_answer_store_for_run_and_save(workdir):
    store = AnswerStore.for_run("Qwen/Qwen3-0.6B", 99)
    store.save("q1", "a1")
    store.save("q2", "a2")

    out = workdir / "output_99" / "output_Qwen_Qwen3-0.6B.txt"
    lines = out.read_text().splitlines()
    assert lines[1:4] == ["q1", "a1", AnswerStore.SEPARATOR]
    assert lines[5:8] == ["q2", "a2", AnswerStore.SEPARATOR]
    assert len(lines) == 8


def test_generate():
    model = MagicMock()
    model.device = "cpu"
    model.generate.return_value = torch.tensor([[1, 2, 3, 10, 11]])
    tokenizer = MagicMock()
    tokenizer.apply_chat_template.return_value.to.return_value = {
        "input_ids": torch.tensor([[1, 2, 3]]),
        "attention_mask": torch.ones(1, 3),
    }
    tokenizer.decode.return_value = "<think>secret</think>visible"
    persona = Persona(99, PERSONA_DATA)
    store = MagicMock()

    generation = LLMGeneration(LoadedLLM("m", model, tokenizer), persona, store)
    answer = generation.generate("hello", max_new_tokens=50, temperature=0.1)

    # réponse sans le bloc <think>, et sauvegardée
    assert answer == "visible"
    store.save.assert_called_once_with("hello", "visible")
    # prompt : persona en system, input en user
    assert tokenizer.apply_chat_template.call_args.args[0] == [
        {"role": "system", "content": persona.system_prompt()},
        {"role": "user", "content": "hello"},
    ]
    # paramètres transmis au modèle
    kwargs = model.generate.call_args.kwargs
    assert (kwargs["max_new_tokens"], kwargs["temperature"], kwargs["do_sample"]) == (50, 0.1, True)
    # seuls les nouveaux tokens sont décodés
    assert torch.equal(tokenizer.decode.call_args.args[0], torch.tensor([10, 11]))
