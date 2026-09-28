"""Pinned, separately installed runtime; no API route changes."""
from pathlib import Path
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from deploy.inventory_worker import main

if __name__ == '__main__': main()
