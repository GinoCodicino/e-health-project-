# LLM Level 2 – Dialogo Mia e Player

Script che genera con un LLM (Qwen3-0.6B) il dialogo tra **Mia** e il **Player**
prima del minigioco delle corde (il cervo intrappolato sotto il tronco).

## Struttura del progetto

```
LLM/
├── core/
│   └── LLM.py              # PersonaLoader, Persona, LLMLoader, LLMGeneration
├── LLM_level2/
│   ├── __init__.py
│   ├── scene.py            # scene, turni, impostazioni (modello, limiti, tentativi)
│   ├── generation.py       # costruzione prompt, pulizia e validazione delle battute
│   └── Mia_dialogue.py     # script principale
└── output_level2/          # dialoghi generati (creata in automatico)
```

## 1. Attivare l'ambiente virtuale

Apri il Terminale e vai nella cartella `LLM`:

```bash
cd /Users/marti/ProgettiUnity/e-health-project-/LLM
```

Poi attiva l'ambiente, a seconda di come è stato creato.

**Se è un ambiente conda:**

```bash
conda activate llm-level2
```

**Se è un venv:**

```bash
source llm-level2/bin/activate
```

(sostituisci `llm-level2` con il percorso reale della cartella dell'ambiente, se diverso).

Se l'attivazione è andata a buon fine, il prompt inizia con `(llm-level2)`.

Per uscire dall'ambiente:

```bash
deactivate          # venv
conda deactivate    # conda
```

## 2. Far partire lo script

Lo script va lanciato **come modulo**, dalla cartella `LLM` (non dalla cartella `LLM_level2`):

```bash
python -m LLM_level2.Mia_dialogue --persona 1
```

> Non usare `python LLM_level2/Mia_dialogue.py`: gli import relativi
> (`from .scene import ...`) funzionano solo con `-m`.

Al primo avvio il modello `Qwen/Qwen3-0.6B` viene scaricato da Hugging Face,
quindi serve una connessione a Internet e può volerci qualche minuto.
Dalle volte successive parte dalla cache.

## 3. Opzioni

| Opzione | Descrizione |
|---|---|
| `--persona 1` / `2` / `3` | Profilo del Player da usare (default: 1) |
| `--preview` | Genera solo i primi due turni e li mostra nel terminale, senza salvare |
| `--seed 42` | Fissa il seed casuale, utile per riprodurre lo stesso risultato durante il debug |

Esempi:

```bash
# Dialogo completo con la persona 2
python -m LLM_level2.Mia_dialogue --persona 2

# Anteprima veloce (2 battute)
python -m LLM_level2.Mia_dialogue --persona 1 --preview

# Risultato riproducibile
python -m LLM_level2.Mia_dialogue --persona 3 --seed 42
```

Senza `--seed` ogni esecuzione produce un dialogo diverso.

## 4. Dove trovare l'output

I file vengono salvati in:

```
output_level2/mia/persona_<N>/before_ropes_<data_ora>.txt
output_level2/mia/persona_<N>/before_ropes_<data_ora>.json
```

- il `.txt` contiene il dialogo, una battuta per riga (`Mia: ...`, `Player: ...`);
- il `.json` contiene le stesse battute con modello, persona, seed e un campo
  `fallback` che indica se la battuta è stata scritta a mano perché il modello non
  è riuscito a produrne una valida.

A fine esecuzione, se alcune battute sono di fallback, lo script lo segnala nel terminale.

## 5. Modificare il comportamento

Tutto si regola in `LLM_level2/scene.py`:

| Impostazione | Cosa controlla |
|---|---|
| `MODEL_NAME` | Modello usato (es. `Qwen/Qwen3-1.7B` per una qualità maggiore) |
| `MAX_WORDS` | Lunghezza massima di una battuta (parole) |
| `MAX_ATTEMPTS` | Tentativi per battuta prima di usare il fallback |
| `HISTORY_TURNS` | Quante battute precedenti vede il modello |
| `MAX_OVERLAP` | Quanto una battuta può ripetere quella precedente |
| `TURNS` | Ogni turno: chi parla, scena, direzione, starter, parole obbligatorie/vietate, fallback |

## Problemi comuni

| Errore | Causa e soluzione |
|---|---|
| `ImportError: attempted relative import with no known parent package` | Lo script è stato lanciato direttamente. Usa `python -m LLM_level2.Mia_dialogue` dalla cartella `LLM` |
| `ModuleNotFoundError: No module named 'core'` | Non sei nella cartella `LLM`. Fai `cd` nella cartella giusta |
| `NameError` o `ImportError` su `MIA_DATA` | Manca la costante in `scene.py` o l'import in `Mia_dialogue.py` |
| Molte battute di fallback | Allarga i gruppi `required` in `scene.py`, alza `MAX_OVERLAP` a 0.7 o prova un modello più grande |
