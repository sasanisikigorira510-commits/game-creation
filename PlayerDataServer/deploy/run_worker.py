"""Installed into root-owned /usr/local/lib/nasus-deletion, not the API tree."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from deploy.deletion_worker import main

if __name__ == '__main__': main()
