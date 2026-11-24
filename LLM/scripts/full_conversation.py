"""Chat with a persona in the terminal: the model is loaded once and the conversation history is kept between turns.
Type 'reset' to start a new conversation, 'quit' to leave."""
import re

import torch

from core.LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from core.RAG import FactBase, Retriever
from core.interaction import EXAMPLE_ANSWER, EXAMPLE_PLAYER_TEXT, SITUATION
from scripts.options import DEFAULT_MODEL, FACT_FILE, argument_parser, output_folder

TEMPERATURE = 1
MAX_TURNS = 6
FACT_COUNT = 2
MAX_NEW_TOKENS = 600
QUIT_WORDS = {"quit", "exit", "q"}
NEXT_TURN_REGEX = re.compile(r"^\s*(Player|You)\s*:", flags=re.MULTILINE)
# the scene and the rules are given once, in the system prompt: each turn of the chat only holds facts and
# what the player says, so that it looks like the turns of the history (otherwise the model repeats itself)
CHAT_RULES = (
    f"\n{SITUATION}\n"
    "The player talks to you. A message of the player may start with what the IPCC report says on the topic. "
    "When it does, build your answer on one of these facts: say what the IPCC says ('the IPCC says...'), "
    "then give your own take on it, through your opinions and interests: doubt it, downplay it or turn it to your advantage, "
    "never just agree because the report says so.\n"
    "Speak naturally in first person ('I', 'you'). "
    "React to the player's last message with emotion, curiosity or disagreement. "
    "If criticised, respond to the criticism first. "
    "Occasionally ask a relevant question to keep the conversation going. "
    "Stay in character, keep your doubts and never invent facts or sources. "
    "Never repeat what you already said in the conversation. "
    "Use at most three short sentences.\n\n"
    "Example\n"
    f"Player: \"{EXAMPLE_PLAYER_TEXT}\"\n"
    f"You: \"{EXAMPLE_ANSWER}\"\n"
)


def build_turn_text(player_text, statements):
    """The facts that match, then what the player says."""
    if not statements:
        return player_text
    facts = "\n".join(f"- {statement.text}" for statement in statements)
    return f"(What the IPCC report says, build your answer on one of these facts:\n{facts})\n\n{player_text}"


def keep_first_turn(answer):
    """Cut the answer where the model starts writing the next lines of the dialogue itself."""
    return NEXT_TURN_REGEX.split(answer, maxsplit=1)[0].strip().strip('"')


class History:
    """Previous exchanges, kept short (the facts of each turn are not stored, only what was said)."""

    def __init__(self, max_turns=MAX_TURNS):
        self.max_turns = max_turns
        self.messages = []
        self.last_query = None
        self.given_facts = set()

    def add(self, player_text, answer):
        self.messages += [
            {"role": "user", "content": player_text},
            {"role": "assistant", "content": answer},
        ]
        self.messages = self.messages[-2 * self.max_turns:]

    def fact_query(self, retriever, player_text):
        """Search the facts with the previous sentence of the player too, so that a follow-up keeps its topic.
        If nothing matches ("why?"), use the last query that found facts."""
        previous = [message["content"] for message in self.messages if message["role"] == "user"][-1:]
        query = " ".join(previous + [player_text])
        if retriever.search(query, count=1):
            self.last_query = query
            return query
        return self.last_query or player_text

    def new_facts(self, retriever, query, count=FACT_COUNT):
        """The best facts for the query that were not given yet: the same facts twice make the model repeat itself."""
        found = retriever.search(query, count=count + len(self.given_facts))
        statements = [statement for statement, score in found if statement.id not in self.given_facts][:count]
        self.given_facts.update(statement.id for statement in statements)
        return statements

    def reset(self):
        self.messages = []
        self.last_query = None
        self.given_facts = set()


def run_conversation(model_name=DEFAULT_MODEL, persona_number=1, folder=None, seed=42, max_turns=MAX_TURNS):
    folder = output_folder(persona_number, folder)
    persona = PersonaLoader().load(persona_number)
    retriever = Retriever(FactBase.load(FACT_FILE).statements)
    torch.manual_seed(seed)
    (folder / "log").mkdir(parents=True, exist_ok=True)

    print(f"Loading {model_name}...")
    model = LLMLoader().load(model_name)
    generation = LLMGeneration(model, persona, AnswerStore(folder / "log/conversation.txt"))
    history = History(max_turns)
    print("Ready. Type 'reset' to start over, 'quit' to leave.\n")

    while True:
        try:
            player_text = input("You: ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            break
        if not player_text:
            continue
        if player_text.lower() in QUIT_WORDS:
            break
        if player_text.lower() == "reset":
            history.reset()
            print("(new conversation)\n")
            continue

        query = history.fact_query(retriever, player_text)
        statements = history.new_facts(retriever, query)
        answer = generation.generate(
            build_turn_text(player_text, statements),
            max_new_tokens=MAX_NEW_TOKENS, temperature=TEMPERATURE,
            history=history.messages, instructions=CHAT_RULES, thinking=False,
        )
        answer = keep_first_turn(answer)
        history.add(player_text, answer)
        print(f"Persona: {answer}\n")


if __name__ == "__main__":
    parser = argument_parser(__doc__)
    parser.add_argument("--max-turns", type=int, default=MAX_TURNS, help="Exchanges remembered by the persona")
    run_conversation(**vars(parser.parse_args()))
