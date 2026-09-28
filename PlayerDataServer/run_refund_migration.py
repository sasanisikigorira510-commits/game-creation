"""Isolated entry point for offline, quarantined refund migration rehearsals."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from refund_migration import main

if __name__ == '__main__': raise SystemExit(main())
