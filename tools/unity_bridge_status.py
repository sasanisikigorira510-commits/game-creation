import json
import sys
import urllib.error
import urllib.request
from pathlib import Path


DEFAULT_BASE_URL = "http://127.0.0.1:8765"
SCRIPT_DIR = Path(__file__).resolve().parent
BRIDGE_STATE_PATH = SCRIPT_DIR / "unity_bridge_state.json"


def load_state():
    if not BRIDGE_STATE_PATH.exists():
        return {"exists": False}

    try:
        payload = json.loads(BRIDGE_STATE_PATH.read_text(encoding="utf-8-sig"))
        payload["exists"] = True
        return payload
    except Exception as exc:
        return {"exists": True, "parseError": str(exc)}


def ping(base_url):
    try:
        with urllib.request.urlopen(f"{base_url.rstrip('/')}/ping", timeout=5) as response:
            return {"ok": True, "response": json.loads(response.read().decode("utf-8"))}
    except Exception as exc:
        return {"ok": False, "error": str(exc)}


def main():
    state = load_state()
    base_url = str(state.get("baseUrl") or DEFAULT_BASE_URL)
    result = {
        "ok": True,
        "bridgeStatePath": str(BRIDGE_STATE_PATH),
        "state": state,
        "resolvedBaseUrl": base_url,
        "ping": ping(base_url),
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
