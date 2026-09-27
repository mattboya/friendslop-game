"""Compatibility entry point for the current modular Blender character export."""
from pathlib import Path
import runpy

runpy.run_path(str(Path(__file__).with_name("generate_modular_character.py")), run_name="__main__")
