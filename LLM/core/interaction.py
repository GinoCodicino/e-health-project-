from core.LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader
from core.RAG import FactBase, Retriever

FACT_FILE = "facts/ipcc_ar6_syr_spm.json"
SITUATION = "You are at the base camp of a mountain whose glacier has disappeared. You came here by helicopter."
EXAMPLE_PLAYER_TEXT = "Why do you take the plane for every trip, with what is happening to the planet?"
EXAMPLE_ANSWER = "Because I have clients to see, that's my job. One flight won't change the weather, and nobody is going to ground me over it."


def build_input_text(player_text, statements, said="", hint=""):
    """Put the scene, the facts of the report and what the player says in one prompt for the persona.
    said: what the persona said just before; hint: how the persona should react this time."""
    facts = "\n".join(f"[{statement.id}] {statement.text}" for statement in statements)
    before = f'You just said: "{said}"\n' if said else ""
    reaction = f"{hint}\n" if hint else ""
    prompt = (
    f"{SITUATION}\n{before}"
    f"Facts from the IPCC report (you may mention them):\n{facts}\n\n"
    "Speak naturally in first person ('I', 'you'). "
    "React to what the player says with emotion, curiosity or disagreement. ask a question to the player to keep the conversation going. "
    "If criticised, respond to the criticism first. "
    "Occasionally ask a relevant question to keep the conversation going. "
    "Stay in character, keep your doubts and never invent facts or sources. "
    "Never refer to the player in third person or analyse the conversation. "
    "Use at most three short sentences.\n"

    f"{reaction}\n"
    "Example\n"
    f"Player: \"{EXAMPLE_PLAYER_TEXT}\"\n"
    f"You: \"{EXAMPLE_ANSWER}\"\n\n"
    f"Player: \"{player_text}\"\n"
    "You:"
)
    return prompt


def interact(generation, retriever, player_text, query=None, fact_count=2, max_new_tokens=600, temperature=0.7):
    """Send the player's sentence to the persona with the facts that match the query (default: the sentence)."""
    statements = [statement for statement, score in retriever.search(query or player_text, count=fact_count)]
    input_text = build_input_text(player_text, statements)
    return generation.generate(input_text, max_new_tokens=max_new_tokens, temperature=temperature)


def simulate_interaction(model_name, persona_number, player_text):
    llm = LLMLoader().load(model_name)
    persona = PersonaLoader().load(persona_number)
    store = AnswerStore.for_run(model_name, persona_number)
    generation = LLMGeneration(llm, persona, store)
    retriever = Retriever(FactBase.load(FACT_FILE).statements)
    return interact(generation, retriever, player_text)


if __name__ == "__main__":
    model_name = "Qwen/Qwen3-0.6B"
    persona_number = 1
    player_text = "It's stupid to come in the base camp of the moutain with an helicopter with what's happening to this moutain. It's used to be a glacier."
    print(simulate_interaction(model_name, persona_number, player_text))
