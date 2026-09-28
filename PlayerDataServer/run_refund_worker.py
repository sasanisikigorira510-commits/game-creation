"""Isolated Python entry point; imports only from its deployed code directory."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from refund_runtime import main

if __name__ == '__main__': raise SystemExit(main())
