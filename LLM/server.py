from fastapi import FastAPI
from pydantic import BaseModel
import uvicorn
from main import LLM  # Importa la classe creata dalla tua amica

app = FastAPI()

# Carica il modello all'avvio del server
print("Caricamento modello LLM in corso...")
model_name = "Qwen/Qwen3-0.6B"
persona_number = 1  # O il numero della persona creata per l'NPC
llm_instance = LLM(model_name=model_name, persona_number=persona_number)
print("Modello pronto!")

class QuestionRequest(BaseModel):
    question: str

@app.post("/ask")
def ask_npc(data: QuestionRequest):
    # Genera la risposta usando il metodo della classe LLM
    response_text = llm_instance.generate_response(data.question)
    return {"answer": response_text}

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8000)