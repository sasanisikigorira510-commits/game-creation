#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
WORKSPACE_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

: "${UNITY_APP:=/Applications/Unity/Unity-6000.3.11f1/Unity.app}"
if [[ -d "/Users/andou/Desktop/Unity Hub.app" ]]; then
  : "${UNITY_HUB_APP:=/Users/andou/Desktop/Unity Hub.app}"
else
  : "${UNITY_HUB_APP:=/Applications/Unity Hub.app}"
fi
: "${PROJECT_DIR:=$WORKSPACE_DIR/WitchTowerGame}"
: "${REPORTS_DIR:=$SCRIPT_DIR/reports}"
: "${UNITY_CLOUD_ENVIRONMENT:=production}"

UNITY_BIN="$UNITY_APP/Contents/MacOS/Unity"
UNITY_HUB_BIN="$UNITY_HUB_APP/Contents/MacOS/Unity Hub"
UNITY_HUB_RESOURCES_DIR="$UNITY_HUB_APP/Contents/Resources"

if [[ ! -d "$PROJECT_DIR" ]]; then
  echo "Project directory not found: $PROJECT_DIR" >&2
  exit 1
fi

if [[ ! -x "$UNITY_BIN" ]]; then
  echo "Unity binary not found: $UNITY_BIN" >&2
  exit 1
fi

if [[ ! -x "$UNITY_HUB_BIN" ]]; then
  echo "Unity Hub binary not found: $UNITY_HUB_BIN" >&2
  exit 1
fi

mkdir -p "$REPORTS_DIR"
