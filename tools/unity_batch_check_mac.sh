#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "$SCRIPT_DIR/unity_mac_env.sh"

timestamp="$(date '+%Y%m%d-%H%M%S')"
log_path="$REPORTS_DIR/unity-batch-check-$timestamp.log"

echo "Unity: $UNITY_BIN"
echo "Project: $PROJECT_DIR"
echo "Log: $log_path"

"$UNITY_BIN" -batchmode -quit -projectPath "$PROJECT_DIR" -logFile "$log_path" "$@"

echo "Batch check finished."
echo "Saved log to: $log_path"
