"""Entry point for python -I; use only from an operator-provisioned code directory."""
from pathlib import Path
import sys

sys.path.insert(0,str(Path(__file__).resolve().parent))
from apple_revocation_runtime import main

if __name__ == '__main__':
    try: raise SystemExit(main())
    except Exception:
        print('APPLE_WORKER_STOPPED: check private configuration and service status; no secrets printed.')
        raise SystemExit(1)
