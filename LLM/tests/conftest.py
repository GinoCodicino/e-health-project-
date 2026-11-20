"""Lets the tests import the modules of the LLM folder (RAG, LLM, Interview...)."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
