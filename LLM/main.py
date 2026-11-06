from LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader


def generate_LLM_response_from_input(model_name, persona_number, input_text, max_new_tokens=200, temperature=0.7):
    llm = LLMLoader().load(model_name)
    persona = PersonaLoader().load(persona_number)
    store = AnswerStore.for_run(model_name, persona_number)
    generation = LLMGeneration(llm, persona, store)
    return generation.generate(input_text, max_new_tokens=max_new_tokens, temperature=temperature)


if __name__ == "__main__":
    model_name = "Qwen/Qwen3-0.6B"
    persona_number = 1
    input_text = "You are a PC in a game about climate change and you are considered as the “final boss”. The aim of this game is to raise awarness about climate change and help the player to challenge skepticism. You talk to the player who wants to climb a mountain which changed a lot since his grandpa climbed it. It used to be full of ice now, there is no ice. What can be a interaction between you and him ? Give also the answers it can give. "
    answer = generate_LLM_response_from_input(model_name, persona_number, input_text, max_new_tokens=1200, temperature=0.7)
    print(answer)
