"""Prompts for the interview and functional requirements."""
from core.LLM import Persona


# Interview and analysis
PROMPT_VERSION = "6"

NO_THINK = " /no_think"

GAME_GOAL = (
    "The game raises awareness about climate change: it helps players examine their views "
    "with facts from the IPCC report, without lecturing them."
)


ANALYST = Persona(number=0, data={
    "role": "requirements analyst for a video game that raises awareness about climate change",
    "task": "turn what a target player said in an interview into functional requirements of the game",
    "game_goal": GAME_GOAL,
    "rules": [
        "a functional requirement describes what the game does, not how it is coded",
        "each requirement starts with 'The system shall' and fits on one line",
        "a requirement serves the goal of the game: it never spreads a claim that contradicts the IPCC report",
        "use what the player said to find how to reach this player (tone, trust, interest), not to repeat their opinions",
    ],
})


def interview_prompt(question):
    return (
        "You are being interviewed by the team that makes a video game about climate change. "
        "They want to understand players like you. "
        "Answer as yourself, in the first person, honestly and in your own words, in at most four sentences. "
        "Your answer must be consistent with your biography, your quote and your climate behaviour. "
        "Do not change your opinions to please them, do not describe yourself in the third person "
        "and never use the word 'persona'.\n\n"
        f"Question: {question}\n"
        "Your answer:"
    )


def requirements_prompt(question, answer, player):
    return (
        f"Interview question: {question}\n"
        f"Answer of {player} (a target player of the game): {answer}\n\n"
        f"Goal of the game: {GAME_GOAL}\n"
        "Write one or two functional requirements that help the game reach this player, given their answer. "
        "Each requirement starts with \"The system shall\" and fits on one line. "
        "Write only the requirements, one per line."
    )


def integration_prompt(fact, requirements):
    """requirements: (id, text) pairs of the functional requirements of the game."""
    listed = "\n".join(f"[{requirement_id}] {text}" for requirement_id, text in requirements)
    return (
        f"Goal of the game: {GAME_GOAL}\n\n"
        f"Fact from the IPCC report that came up in a conversation with a target player:\n{fact}\n\n"
        f"Functional requirements of the game:\n{listed}\n\n"
        "Propose 2 to 3 ideas to bring this fact to the player in the game (a scene, a line of a character, "
        "an object, a choice to make...). Stay faithful to the fact and don't lecture the player. "
        "Write one numbered line per idea, ending with the ids of the requirements it serves, like: "
        "1. <idea> (FR: Q1-A1-FR1, Q3-A1-FR2). Write only the ideas."
    )
