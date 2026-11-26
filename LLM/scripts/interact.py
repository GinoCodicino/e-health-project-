"""Build a conversation tree with a persona, one level at a time. The player's lines are written by hand,
each one with a stance (1 = agrees, 2 = disagrees but respectful, 3 = disagrees and angry), the persona answers them.
  start "<first line>"   new tree with the persona's answer to the first line
  add replies.json       [{"parent": "0.1", "stance": 2, "player": "..."}, ...]: the persona answers each one
  show                   print the tree"""
import json

import torch

from core.LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from core.interaction import add_reply, interact
from scripts.options import DEFAULT_MODEL, argument_parser, output_folder

TEMPERATURE = 1
TREE_FILE = "answers/conversation_tree.json"


def print_tree(node, level=0):
    indent = "    " * level
    stance = f" ({node['stance']})" if node["stance"] else ""
    print(f"{indent}[{node['id']}]{stance} Player: {node['player']}")
    print(f"{indent}    Persona: {node['persona']}")
    for child in node["children"]:
        print_tree(child, level + 1)


def load_generation(model_name, persona_number, folder):
    (folder / "log").mkdir(parents=True, exist_ok=True)
    persona = PersonaLoader().load(persona_number)
    model = LLMLoader().load(model_name)
    return LLMGeneration(model, persona, AnswerStore(folder / "log/interaction.txt"))


def run_interaction(command, argument=None, model_name=DEFAULT_MODEL, persona_number=1, folder=None, seed=42):
    folder = output_folder(persona_number, folder)
    tree_file = folder / TREE_FILE
    torch.manual_seed(seed)

    if command == "start":
        generation = load_generation(model_name, persona_number, folder)
        answer = interact(generation, argument, temperature=TEMPERATURE)
        run = {"model": model_name, "persona": persona_number, "seed": seed}
        tree = {"id": "0", "stance": None, "player": argument, "persona": answer, "children": []}
    else:
        with open(tree_file) as f:
            saved = json.load(f)
        run, tree = saved["run"], saved["tree"]
        if command == "add":
            with open(argument) as f:
                replies = json.load(f)
            generation = load_generation(run["model"], run["persona"], folder)
            for reply in replies:
                add_reply(generation, tree, reply["parent"], reply["stance"], reply["player"], temperature=TEMPERATURE)

    if command != "show":
        tree_file.parent.mkdir(parents=True, exist_ok=True)
        with open(tree_file, "w") as f:
            json.dump({"run": run, "tree": tree}, f, indent=2, ensure_ascii=False)
    print_tree(tree)
    return tree


if __name__ == "__main__":
    parser = argument_parser(__doc__)
    parser.add_argument("command", choices=["start", "add", "show"])
    parser.add_argument("argument", nargs="?", help="The first line (start) or the replies file (add)")
    run_interaction(**vars(parser.parse_args()))
