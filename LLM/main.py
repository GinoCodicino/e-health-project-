from transformers import AutoModelForCausalLM, AutoTokenizer
from datetime import datetime
import json
import os
import re
class LLM:
    def __init__(self, model_name, persona_number):
        self.model, self.tokenizer = self.load_model(model_name)
        self.persona_json = self.load_persona(persona_number) 
        self.output_file = self.generate_name_output_file(model_name, persona_number)
        self.system_prompt = self.generate_system_prompt(self.persona_json)
    def generate_name_output_file(self, model_name, persona_number):
        model_name = model_name.replace("/", "_")
        folder = f"output_{persona_number}"
        os.makedirs(folder, exist_ok=True)
        return os.path.join(folder, f"output_{model_name}.txt")
    def load_model(self,model_name):
        """
        
        Load the model and tokenizer from the given model name."""
        model = AutoModelForCausalLM.from_pretrained(model_name)
        tokenizer = AutoTokenizer.from_pretrained(model_name)
        return model, tokenizer


    def call_model(self, input_text, max_new_tokens=200, temperature=0.7):
        """
        call the model and return the answer, also store the answer in the output file
        """
        self.model.eval()
        messages = [
        {"role": "system", "content": self.system_prompt},
        {"role": "user", "content": input_text},
    ]
        inputs = self.tokenizer.apply_chat_template(
            messages,
            add_generation_prompt=True,
            return_tensors="pt",
            return_dict=True,
        ).to(self.model.device)

        output = self.model.generate(
            **inputs,
            max_new_tokens=max_new_tokens,
            temperature=temperature,
            do_sample=True,
        )
        answer = self.tokenizer.decode(
            output[0][inputs["input_ids"].shape[-1]:],
            skip_special_tokens=True,
        )

        # remove the thinking part if exist 
        regex = r"<think>.*?</think>"
        answer = re.sub(regex, "", answer, flags=re.DOTALL)

        self.store_answer(input_text, answer, self.output_file)
        return answer


    def generate_system_prompt(self, persona_json):
        # generate the system prompt from the persona json file
        system_prompt = f"here the persona's description you have to embody : \n{json.dumps(persona_json)}\n"
        return system_prompt
    def store_answer(self, input_text,answer, output_file):
        # create if not exist and append the answer to the output file
        

        with open(output_file, "a") as f:
            f.write(datetime.now().strftime("%Y-%m-%d %H:%M:%S") + "\n")
            f.write(input_text + "\n")
            f.write(answer + "\n")
            f.write(":) :) :) ---------------------------------------\n")

    def load_persona(self,persona_number):
        """
        load the persona from its json file, the persona json files are stored in the personas folder and are named persona_{persona_number}.json
        """
        folder = "personas"
        file = f"{folder}/persona_{persona_number}.json"
        with open(file, "r") as f:
            persona = json.load(f)
        return persona
    
if __name__ == "__main__":
    model_name = "Qwen/Qwen3-0.6B"
    persona_number = 1
    llm = LLM(model_name, persona_number)
    input_text = """what's your opinion on Climate Change?"""
    answer = llm.call_model(input_text, max_new_tokens=1200, temperature=0.7)
    print(answer)