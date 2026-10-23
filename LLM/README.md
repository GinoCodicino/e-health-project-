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