import re

from core.LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader

SITUATION = "You are at the base camp of a mountain whose glacier has disappeared. You came here by helicopter."
# the scene and the rules are given once, in the system prompt: each turn only holds what the player says,
# so that it looks like the turns of the history (otherwise the model repeats itself)
CHAT_RULES = (
    f"\n{SITUATION} A visitor (the player) talks to you.\n"
    "Speak naturally in first person, directly to the player, like in a real spoken conversation.\n"
    "Answer the player's last message first: what they just said, not the beginning of the conversation.\n"
    "Each answer must bring something new: a new argument, a personal story, a doubt, a concession or a feeling. "
    "Never repeat or rephrase a sentence or a question you already said.\n"
    "If the player insists or says you repeat yourself, change your angle: give in a little, get annoyed, or defend yourself differently.\n"
    "If the player is angry or insults you, react with emotion (defensive, hurt or irritated), as a real person would.\n"
    "Don't end every answer with a question: most answers end with a statement, ask a question only now and then.\n"
    "Stay in character and keep your opinions: you may admit a small point, but you don't change your mind "
    "because of one argument. Never invent facts or sources. "
    "Never analyse the conversation or talk about the player in third person.\n"
    "Never use more than three short sentences.\n"
)
NEXT_TURN_REGEX = re.compile(r"^\s*(Player|You)\s*:", flags=re.MULTILINE)
SENTENCE_REGEX = re.compile(r"(?<=[.!?])\s+")
MAX_SENTENCES = 3
# kind of reply the player gives to the persona, one branch of the tree each
STANCES = {1: "agree", 2: "respectful_disagreement", 3: "angry_disagreement"}
MAX_TURNS = 5


def keep_first_turn(answer, max_sentences=MAX_SENTENCES):
    """Cut the answer where the model starts writing the next lines of the dialogue itself,
    and after max_sentences sentences (the model does not always respect the length asked)."""
    answer = NEXT_TURN_REGEX.split(answer, maxsplit=1)[0].strip().strip('"')
    return " ".join(SENTENCE_REGEX.split(answer)[:max_sentences])


def interact(generation, player_text, max_new_tokens=600, temperature=0.7, history=None):
    """Send the player's sentence to the persona.
    history: previous messages of the conversation, given to the model before this sentence."""
    answer = generation.generate(
        player_text, max_new_tokens=max_new_tokens, temperature=temperature, history=history,
        instructions=CHAT_RULES, thinking=False,
    )
    return keep_first_turn(answer)


def find_node(tree, node_id):
    """Ids are paths: "0" is the root, "0.2" its child with stance 2, "0.2.3"..."""
    node = tree
    for stance in node_id.split(".")[1:]:
        node = next(child for child in node["children"] if child["stance"] == STANCES[int(stance)])
    return node


def branch_history(tree, node_id):
    """The messages from the root down to node_id: the persona only knows what was said in its own branch."""
    parts = node_id.split(".")
    history = []
    for depth in range(1, len(parts) + 1):
        node = find_node(tree, ".".join(parts[:depth]))
        history += [
            {"role": "user", "content": node["player"]},
            {"role": "assistant", "content": node["persona"]},
        ]
    return history


def add_reply(generation, tree, parent_id, stance, player_text, temperature=0.7):
    """The persona answers player_text after the branch of parent_id; the new node is added under it."""
    if len(parent_id.split(".")) >= MAX_TURNS:
        raise ValueError(f"The branch {parent_id} already has {MAX_TURNS} exchanges.")
    answer = interact(generation, player_text, temperature=temperature, history=branch_history(tree, parent_id))
    node = {"id": f"{parent_id}.{stance}", "stance": STANCES[stance], "player": player_text, "persona": answer, "children": []}
    find_node(tree, parent_id)["children"].append(node)
    return node


def simulate_interaction(model_name, persona_number, player_text):
    llm = LLMLoader().load(model_name)
    persona = PersonaLoader().load(persona_number)
    store = AnswerStore.for_run(model_name, persona_number)
    generation = LLMGeneration(llm, persona, store)
    return interact(generation, player_text)


if __name__ == "__main__":
    model_name = "Qwen/Qwen3-0.6B"
    persona_number = 1
    player_text = "It's stupid to come in the base camp of the moutain with an helicopter with what's happening to this moutain. It's used to be a glacier."
    print(simulate_interaction(model_name, persona_number, player_text))
