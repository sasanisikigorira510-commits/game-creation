#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "$SCRIPT_DIR/unity_mac_env.sh"

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required for smoke checks." >&2
  exit 1
fi

bridge_status="$(python3 "$SCRIPT_DIR/unity_bridge_status.py")"
echo "$bridge_status"

if ! echo "$bridge_status" | python3 -c 'import json, sys; payload = json.load(sys.stdin); sys.exit(0 if payload.get("ping", {}).get("ok") else 1)'; then
  echo "Unity bridge is not reachable yet." >&2
  echo "Start the editor with zsh $SCRIPT_DIR/unity_open_project.sh, wait for compile to settle, then run this script again." >&2
  exit 2
fi

python3 "$SCRIPT_DIR/unity_smoke_check.py"
