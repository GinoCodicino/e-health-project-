"""Generate Mia and Player's Level 2 dialogue."""


import argparse
import json
from datetime import datetime
from pathlib import Path
from core.LLM import LLMGeneration, LLMLoader, Persona, PersonaLoader
from .scene import MIA_DATA, MODEL_NAME, TURNS
from .generation import describe_persona, generate_valid_line


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--persona",
        type=int,
        choices=(1, 2, 3),
        default=1,
        help="Player profile to use. Default: persona 1.",
    )
    parser.add_argument(
        "--preview",
        action="store_true",
        help="Generate only the first two turns and show them in the terminal.",
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=None,
        help="Optional random seed, useful to reproduce a result while debugging.",
    )
    args = parser.parse_args()

    if args.seed is not None:
        try:
            import torch

            torch.manual_seed(args.seed)
        except ImportError:
            print("torch non trovato: --seed ignorato.", flush=True)

    player = PersonaLoader().load(args.persona)
    mia = Persona(number=0, data=MIA_DATA)

    print(f"Player profile: {player.number} - {player.data['name']}", flush=True)
    print(f"Caricamento di {MODEL_NAME}...", flush=True)

    # Il modello viene caricato una sola volta e condiviso dai due personaggi.
    model = LLMLoader().load(MODEL_NAME)
    generators = {
        "Mia": LLMGeneration(model, mia),
        "Player": LLMGeneration(model, player),
    }
    profiles = {
        "Mia": describe_persona(mia),
        "Player": describe_persona(player),
    }

    turns = TURNS[:2] if args.preview else TURNS
    conversation = []
    records = []

    for number, turn in enumerate(turns, start=1):
        speaker = turn["speaker"]
        print(f"[{number}/{len(turns)}] Generazione: {speaker}...", flush=True)

        line, used_fallback = generate_valid_line(
            generator=generators[speaker],
            turn=turn,
            conversation=conversation,
            profile=profiles[speaker],
        )

        conversation.append(f"{speaker}: {line}")
        records.append(
            {
                "turn": number,
                "speaker": speaker,
                "line": line,
                "fallback": used_fallback,
            }
        )
        print(conversation[-1], flush=True)

    if args.preview:
        print("\nAnteprima completata. Le due battute sono visibili nel terminale.")
        return

    folder = Path("output_level2") / "mia" / f"persona_{args.persona}"
    folder.mkdir(parents=True, exist_ok=True)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S_%f")

    draft = folder / f"before_ropes_{timestamp}.txt"
    draft.write_text("\n".join(conversation) + "\n", encoding="utf-8")

    data_file = folder / f"before_ropes_{timestamp}.json"
    data_file.write_text(
        json.dumps(
            {
                "model": MODEL_NAME,
                "persona": {"number": player.number, "name": player.data["name"]},
                "created": datetime.now().isoformat(timespec="seconds"),
                "seed": args.seed,
                "dialogue": records,
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    fallback_count = sum(r["fallback"] for r in records)
    print(f"\nSaved to {draft}")
    print(f"Saved to {data_file}")
    if fallback_count:
        print(f"Attenzione: {fallback_count} battute su {len(records)} sono di fallback.")


if __name__ == "__main__":
    main()
