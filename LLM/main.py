from transformers import *

class LLM:
    def __init__(self, model_name, persona_number):
        self.model, self.tokenizer = self.load_model(model_name)
        self.persona = self.load_persona(persona_number) 
        self.output_file = f"output_{model_name}_{persona_number}.txt"
    def load_model(self,model_name):
        """
        
        Load the model and tokenizer from the given model name."""
        model = AutoModelForCausalLM.from_pretrained(model_name)
        tokenizer = AutoTokenizer.from_pretrained(model_name)
        return model, tokenizer


    def call_model(self, input_text):
        #TODO 
        pass

    def store_answer(self,answer, output_file):
        #TODO 
        pass

    def load_persona(self,persona_number):
        #TODO
        pass