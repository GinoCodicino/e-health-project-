from fastapi import FastAPI
from pydantic import BaseModel
import uvicorn
from LLM import AnswerStore, LLMGeneration, LLMLoader, PersonaLoader

app = FastAPI()

# Carica il modello all'avvio del server
print("Caricamento modello LLM in corso...")
model_name = "Qwen/Qwen3-0.6B"
persona_number = 1  # O il numero della persona creata per l'NPC
generation = LLMGeneration(
    LLMLoader().load(model_name),
    PersonaLoader().load(persona_number),
    AnswerStore.for_run(model_name, persona_number),
)
print("Modello pronto!")

class QuestionRequest(BaseModel):
    question: str

@app.post("/ask")
def ask_npc(data: QuestionRequest):
    response_text = generation.generate(data.question)
    return {"answer": response_text}

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8000)
