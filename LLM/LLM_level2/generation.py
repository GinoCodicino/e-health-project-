"""Generate and validate dialogue lines."""


import re

from .scene import (HISTORY_TURNS, MAX_ATTEMPTS, MAX_NEW_TOKENS, MAX_OVERLAP,
                    MAX_WORDS, PROFILE_MAX_CHARS, BASE_TEMPERATURE, TEMPERATURE_STEP)

def describe_persona(persona):
    """Short text profile of a character, to remind the model who is speaking."""
    parts = []
    for key, value in persona.data.items():
        if isinstance(value, (str, int, float)):
            parts.append(f"{key}: {value}")
        elif isinstance(value, (list, tuple)):
            parts.append(f"{key}: {', '.join(str(v) for v in value)}")
    return "; ".join(parts)[:PROFILE_MAX_CHARS]


def build_prompt(turn, conversation, profile):
    """Build the prompt for one turn, including the recent conversation."""
    speaker = turn["speaker"]
    history = "\n".join(conversation[-HISTORY_TURNS:]) or "(No one has spoken yet.)"
    starter = turn.get("starter")

    if starter:
        # Stile "completamento": il modello continua la battuta iniziata.
        return (
            f"Character: {profile}\n\n"
            f"Scene: {turn['scene']}\n\n"
            f"Conversation so far:\n{history}\n\n"
            f"What {speaker} needs to communicate: {turn['direction']}\n\n"
            f"Complete {speaker}'s line in one or two short sentences. "
            "Do not repeat what was already said.\n"
            f"{speaker}: {starter}"
        )

    return f"""Write one line of spoken English for {speaker}.

Character: {profile}

Scene: {turn['scene']}

Conversation so far:
{history}

What {speaker} needs to communicate now:
{turn['direction']}

Speak directly to the other person. Add new information or answer their
question. Do not repeat what was already said in the conversation.
Use at most {MAX_WORDS} words, short enough for one game dialogue box.
Return only the spoken words.
"""


def clean_output(raw, starter=None):
    """Turn raw model output into one clean spoken line (or '' if unusable)."""
    text = raw

    # Blocchi di ragionamento di Qwen3 (anche chiusi male).
    text = re.sub(r"<think>.*?</think>", "", text, flags=re.DOTALL | re.IGNORECASE)
    text = re.sub(r"<think>.*", "", text, flags=re.DOTALL | re.IGNORECASE)
    text = text.replace("</think>", "").strip()

    # Etichette iniziali (Mia:, Player:, Spoken line:).
    text = re.sub(
        r"^(?:(?:Spoken line|Mia|Player)\s*:\s*)+",
        "",
        text,
        flags=re.IGNORECASE,
    )

    # Se il modello scrive anche il turno dell'altro, tieni solo il primo.
    text = re.split(
        r"\s*\n?\s*(?:Mia|Player)\s*:",
        text,
        maxsplit=1,
        flags=re.IGNORECASE,
    )[0]

    # Didascalie tra parentesi e asterischi (*she gasps*).
    text = re.sub(r"\([^()]*\)|\[[^\[\]]*\]|\*[^*]*\*", "", text)

    line = " ".join(text.split()).strip(' "“”')

    # Evita duplicati tipo "Quick, Quick, follow me".
    if starter and line:
        core = starter.strip().rstrip(",.!?:").lower()
        if not line.lower().startswith(core):
            line = f"{starter} {line}"

    return line


def trim_to_complete_sentences(line, max_words=MAX_WORDS):
    """Cut a truncated line at the last full sentence and enforce the word limit."""
    if not line:
        return line

    sentences = re.findall(r"[^.!?]+[.!?]+|[^.!?]+$", line)
    sentences = [s.strip() for s in sentences if s.strip()]

    kept = []
    words = 0
    for sentence in sentences:
        n = len(sentence.split())
        if words + n > max_words:
            break
        kept.append(sentence)
        words += n

    if not kept:
        return ""  # già la prima frase supera il limite: da rigenerare

    # Se l'ultima frase è troncata e ce n'è una completa prima, scartiamo la troncata.
    if kept[-1][-1] not in ".!?":
        complete = [s for s in kept if s[-1] in ".!?"]
        if complete:
            kept = complete
        else:
            kept[-1] += "."

    return " ".join(kept)


def content_words(text):
    """Lowercase words longer than 3 letters (ignores 'the', 'and', ...)."""
    return {w for w in re.findall(r"[a-z']+", text.lower()) if len(w) > 3}


def overlap_ratio(line, previous):
    """Share of this line's content words already present in the previous line."""
    words = content_words(line)
    if not words or not previous:
        return 0.0
    return len(words & content_words(previous)) / len(words)


def is_valid(line, turn, previous_line):
    """Check the line exists, has the mandatory ideas and is not a copy."""
    if not line:
        return False

    lowered = line.lower()

    if not all(any(word in lowered for word in group) for group in turn["required"]):
        return False

    if any(phrase in lowered for phrase in turn["forbidden"]):
        return False

    if overlap_ratio(line, previous_line) > MAX_OVERLAP:
        return False

    return True


def generate_line(generator, turn, conversation, profile, temperature):
    """Ask Qwen for one spoken turn and return a cleaned line ('' if unusable)."""
    raw = generator.generate(
        build_prompt(turn, conversation, profile),
        max_new_tokens=MAX_NEW_TOKENS,
        temperature=temperature,
        thinking=False,
    )
    line = clean_output(raw, starter=turn.get("starter"))
    return trim_to_complete_sentences(line)


def generate_valid_line(generator, turn, conversation, profile):
    """Retry until the line is valid; fall back to the handwritten line."""
    previous_line = conversation[-1].split(": ", 1)[-1] if conversation else ""

    for attempt in range(MAX_ATTEMPTS):
        # Più tentativi falliscono, più alziamo un po' la temperatura per variare.
        temperature = BASE_TEMPERATURE + TEMPERATURE_STEP * attempt
        line = generate_line(generator, turn, conversation, profile, temperature)

        if is_valid(line, turn, previous_line):
            return line, False

        print(
            f"   tentativo {attempt + 1}/{MAX_ATTEMPTS} scartato: {line!r}",
            flush=True,
        )

    print("   uso la battuta di fallback.", flush=True)
    return turn["fallback"], True
