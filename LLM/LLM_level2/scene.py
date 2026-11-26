"""Level 2 scenes, turns and settings."""


MODEL_NAME = "Qwen/Qwen3-0.6B"
MAX_WORDS = 25            # limite per la dialogue box
MAX_ATTEMPTS = 4          # tentativi prima di usare il fallback
HISTORY_TURNS = 4         # quante battute precedenti vede il modello
MAX_OVERLAP = 0.6         # quota massima di parole già dette nella battuta precedente
PROFILE_MAX_CHARS = 300   # lunghezza massima del profilo inserito nel prompt
MAX_NEW_TOKENS = 60
BASE_TEMPERATURE = 0.65
TEMPERATURE_STEP = 0.1

SCENE_PATH = (
    "A forest path. Mia has just met the player and is calling for help. "
    "The injured deer is farther down the trail and the player cannot see it yet."
)


SCENE_DEER = (
    "Mia and the player are standing beside the deer. The deer is pinned UNDER "
    "a fallen log from an ancient tree that heavy rain knocked down. "
    "Both of them can see it."
)


TURNS = [
    {
        "speaker": "Mia",
        "scene": SCENE_PATH,
        "direction": (
            "Say that an injured deer needs help and ask the player to follow "
            "you. Your only request right now is that they follow you."
        ),
        "starter": "Quick,",
        "required": (("deer",), ("follow", "come")),
        "forbidden": (),
        "fallback": "Quick, follow me! A deer is trapped and needs our help!",
    },
    {
        "speaker": "Player",
        "scene": SCENE_PATH,
        "direction": (
            "A stranger has called out for help. You cannot see the animal yet. "
            "Ask what happened to it, in a few words."
        ),
        "starter": "What happened to",
        "required": (),
        "forbidden": (),
        "fallback": "What happened to the deer?",
    },
    {
        "speaker": "Mia",
        "scene": SCENE_DEER,
        "direction": (
            "Answer the player's question. Say that heavy rain knocked down "
            "the old tree, and that a heavy log is now pinning the deer's leg."
        ),
        "required": (("rain", "storm"), ("log", "trunk")),
        "forbidden": ("under heavy rain", "under the rain", "under the heavy rain"),
        "fallback": "Heavy rain knocked down the old tree, and a log is pinning its leg.",
    },
    {
        "speaker": "Player",
        "scene": SCENE_DEER,
        "direction": (
            "You see wood splinters in the deer's wound. Say that the splinters "
            "worry you, and that you are afraid of hurting the deer more while "
            "helping. Do not repeat what Mia said."
        ),
        "starter": "Those splinters",
        "required": (
            ("splinter",),
            ("hurt", "worse", "pain", "afraid", "worry", "careful", "scared"),
        ),
        "forbidden": ("pinned", "fallen tree"),
        "fallback": "Those splinters worry me. What if we hurt it even more?",
    },
    {
        "speaker": "Mia",
        "scene": SCENE_DEER,
        "direction": (
            "Respond to the player's worry about hurting the deer. "
            "Reassure them that you will move the log slowly, together. "
            "A small shift will give the deer room to pull its leg free."
        ),
        "starter": "Don't worry, we'll move the log slowly and give",
        "required": (("leg", "free", "room"),),
        "forbidden": ("tied", "help you"),
        "fallback": "If we move the log just a little, it can pull its leg free.",
    },
    {
        "speaker": "Player",
        "scene": SCENE_DEER,
        "direction": (
            "Mia has answered you. You decide to help. Ask Mia where she "
            "needs you."
        ),
        "starter": "Okay, I'll help. Where do you",
        "required": (("need", "want"),),
        "forbidden": ("let me check", "where is"),
        "fallback": "Okay, I'll help. Where do you need me?",
    },
    {
        "speaker": "Mia",
        "scene": SCENE_DEER,
        "direction": (
            "The player is ready and waiting for your instruction. Tell them to "
            "take the other end of the rope and pull with you to move the log "
            "off the deer's leg."
        ),
        "starter": "Take the other end of the rope and pull with",
        "required": (("rope",), ("pull",), ("with me", "together")),
        "forbidden": ("free the log",),
        "fallback": "Take the other end of the rope and pull with me to move the log!",
    },
]

MIA_DATA = {
    "name": "Mia",
    "role": "A forest guide asking the player to help a trapped deer",
    "voice": (
        "You speak directly to the person beside you. "
        "You sound urgent when calling for help, then calm and practical. "
        "You listen and answer what the other person actually says."
    ),
}
