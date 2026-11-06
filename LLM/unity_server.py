from fastapi import FastAPI
from pydantic import BaseModel
import uvicorn
from main import LLM

app = FastAPI()

print("Caricamento modello LLM in corso...")
model_name = "Qwen/Qwen3-0.6B"
persona_number = 1
llm_instance = LLM(model_name=model_name, persona_number=persona_number)
print("Modello pronto per ricevere richieste!")

class QuestionRequest(BaseModel):
    question: str

@app.post("/ask")
def ask_npc(data: QuestionRequest):
    response_text = llm_instance.call_model(data.question, max_new_tokens=200, temperature=0.7)
    return {"answer": response_text.strip()}

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8000)