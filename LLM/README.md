# Folders

Run every command from the `LLM` folder.

- `core/`: the building blocks: the model and personas (`LLM.py`), the IPCC fact base and its search (`RAG.py`), the in-game prompt of a persona (`interaction.py`);
- `pipeline/`: what each step does: the interview, requirements and side quests (`Interview.py`), Ethan's beliefs and the dialogues (`Dialogues.py`);
- `scripts/`: the commands to run (`python -m scripts.<name>`);
- `tests/`: the tests (`pytest tests -v`);
- `personas/`, `facts/`, `Ethan/`: the data (personas, IPCC fact base, Ethan's questions, answers and logs);
- `server.py`, `unity_server.py`: the servers for Unity (they use `core/LLM.py`).

# Files
## core/LLM.py

The base of every step:
- `LLMLoader` loads a Hugging Face model and its tokenizer (default: `Qwen/Qwen3-0.6B` you can change, i took it bc it was not heavy);
- `PersonaLoader` loads a persona from `personas/persona_<n>.json`, and `Persona` turns it into the system prompt;
- `LLMGeneration` generates an answer to a prompt, in the persona's voice;
- `AnswerStore` appends each prompt and answer, with a timestamp, to a log file.
## requirements.txt

first, create a virtual environment and install the dependencies with the following command:

```bash 
cd LLM
python -m venv venv
source venv/bin/activate  # On Windows use `venv\Scripts\activate`
pip install -r requirements.txt
```
## core/RAG.py

Gives the LLM real facts from the IPCC report (AR6 Synthesis Report, Summary for Policymakers). It contains:
- `PDFReader`: reads the text of each page of the PDF (with `pypdf`);
- `StatementParser`: keeps the numbered statements of the report (`A.1` is a headline statement, `A.1.1` one of its detailed statements) and removes the page numbers, headers and footnotes;
- `FactBase`: the list of statements, saved to and loaded from a JSON file;
- `Retriever`: finds the statements that best match a sentence (BM25, the words rare in the report weigh more).

Each statement keeps its `id`, its `text`, its `page` in the PDF and its `sources` (the chapters of the full report, for example `{2.1.1, Figure 2.1}`), so every fact can be cited.

To search the facts (run it from the `LLM` folder):

```python
from core.RAG import FactBase, Retriever

fact_base = FactBase.load("facts/ipcc_ar6_syr_spm.json")
retriever = Retriever(fact_base.statements)
for statement, score in retriever.search("human activities cause global warming", count=3):
    print(statement.id, statement.page, statement.text)
```
## scripts/build_fact_base.py

Builds `facts/ipcc_ar6_syr_spm.json` from the PDF. You only need it if the PDF or the parsing changes, the JSON is already there.

```bash
cd LLM
python -m scripts.build_fact_base
```

It reads `../data/raw/IPCC_AR6_SYR_SPM.pdf` and prints the number of statements saved (101 expected: 18 headline statements and 83 detailed ones).

The text is cleaned automatically, but a few footnote numbers can stay in a sentence (for example `incremental 18,`). Before using a statement in the game, compare it with the page given in its `page` field.
## tests/

All the tests are in the `tests/` folder (fake pages and fake models: no PDF, no model download). Run them all from the `LLM` folder:

```bash
cd LLM
pytest tests -v
```

Or one file only, for example `pytest tests/test_rag.py -v`. `tests/conftest.py` lets the tests import the modules of the `LLM` folder.
# Ethan Brooks: the LLM in persona mode

Everything about Ethan (questions, answers, logs) is in the `Ethan/` folder:
- `questions/`: the questions we ask him, written by hand (`interview.json`, `beliefs.json`);
- `answers/`: what the LLM generated, one JSON file per step;
- `log/`: every prompt and answer, with a timestamp (`AnswerStore`).

Run the steps from the `LLM` folder, in this order:

```bash
python -m scripts.run_interview       # Ethan answers the interview -> answers/interview.json, requirements.json, side_quests.json
python -m scripts.generate_beliefs    # Ethan says what he believes, the analyst writes the IPCC words to search -> answers/beliefs.json
python -m scripts.generate_dialogues  # the dialogue variants, from the kept beliefs -> answers/dialogue.json
```

## Review

Each generated text has a `status`: `to_review`, `kept`, `edited` or `rejected`. Open the JSON file and change it by hand; if you change the text, put the new one in `edited_text` and set the status to `edited`. `note` is free for the reason.
- `generate_dialogues.py` uses the answer to Q6 (how to talk to Ethan) only if it was kept or edited.
- `generate_dialogues.py` only uses the beliefs that were kept or edited.

To count what was kept, edited and rejected (for the report):

```bash
python -m scripts.review_stats
```

## The files

- `pipeline/Interview.py`: the interview (`PersonaInterview`), the functional requirements (`RequirementWriter`), the side quest ideas (`SideQuestWriter`) and `GenerationRecord`, the generated text with its review status.
- `pipeline/Dialogues.py`: Ethan's beliefs (`BeliefWriter`, written by the LLM in persona mode), then the player's answers (fact, question, empathy) and Ethan's reactions for each kept belief, with the IPCC facts; then Ethan's last words before the climb.
- `tests/test_interview.py`, `tests/test_dialogues.py`: tests with fake models; `tests/test_review_stats.py`: tests of `scripts/review_stats.py` (`pytest tests -v`).