# Files
## main.py

Contains the `LLM` class, which:
- loads a Hugging Face model and its tokenizer (default: `Qwen/Qwen3-0.6B` you can change, i took it bc it was not heavy);
- loads a persona from `personas/persona_<n>.json` and turns it into the system prompt;
- generates an answer to a user prompt, removing the `<think>` reasoning block;
- appends each prompt and answer, with a timestamp, to `output_<model>_<persona>.txt` in the right folder.

Edit `model_name`, `persona_number` and `input_text` at the bottom of the file, then run `python main.py`.
## requirements.txt

first, create a virtual environment and install the dependencies with the following command:

```bash 
cd LLM
python -m venv venv
source venv/bin/activate  # On Windows use `venv\Scripts\activate`
pip install -r requirements.txt
```
## RAG.py

Gives the LLM real facts from the IPCC report (AR6 Synthesis Report, Summary for Policymakers). It contains:
- `PDFReader`: reads the text of each page of the PDF (with `pypdf`);
- `StatementParser`: keeps the numbered statements of the report (`A.1` is a headline statement, `A.1.1` one of its detailed statements) and removes the page numbers, headers and footnotes;
- `FactBase`: the list of statements, saved to and loaded from a JSON file;
- `Retriever`: finds the statements that best match a sentence (BM25, the words rare in the report weigh more).

Each statement keeps its `id`, its `text`, its `page` in the PDF and its `sources` (the chapters of the full report, for example `{2.1.1, Figure 2.1}`), so every fact can be cited.

To search the facts (run it from the `LLM` folder):

```python
from RAG import FactBase, Retriever

fact_base = FactBase.load("facts/ipcc_ar6_syr_spm.json")
retriever = Retriever(fact_base.statements)
for statement, score in retriever.search("human activities cause global warming", count=3):
    print(statement.id, statement.page, statement.text)
```
## build_fact_base.py

Builds `facts/ipcc_ar6_syr_spm.json` from the PDF. You only need it if the PDF or the parsing changes, the JSON is already there.

```bash
cd LLM
python build_fact_base.py
```

It reads `../data/raw/IPCC_AR6_SYR_SPM.pdf` and prints the number of statements saved (101 expected: 18 headline statements and 83 detailed ones).

The text is cleaned automatically, but a few footnote numbers can stay in a sentence (for example `incremental 18,`). Before using a statement in the game, compare it with the page given in its `page` field.
## test_rag.py

Tests of `RAG.py` on fake pages (no PDF, no model). Run them with:

```bash
cd LLM
pytest test_rag.py -v
```