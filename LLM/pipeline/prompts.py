"""Prompts for the interview and functional requirements."""
from core.LLM import Persona


# Interview and analysis
PROMPT_VERSION = "5"

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
