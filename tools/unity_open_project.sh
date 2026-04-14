#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "$SCRIPT_DIR/unity_mac_env.sh"

find_hub_licensing_pipe() {
  ps -ax -o command | sed -n 's/.*UnityLicensingClient_V1\.app\/Contents\/MacOS\/Unity\.Licensing\.Client --namedPipe \([^ ]*\).*/\1/p' | tail -n 1
}

launch_hub_if_needed() {
  if pgrep -f "Unity Hub.app/Contents/MacOS/Unity Hub" >/dev/null 2>&1; then
    return
  fi

  if [[ -d "$UNITY_HUB_APP" ]]; then
    open -a "$UNITY_HUB_APP"
  fi
}

project_editor_version() {
  local version_file="$PROJECT_DIR/ProjectSettings/ProjectVersion.txt"
  if [[ ! -f "$version_file" ]]; then
    return
  fi

  sed -n 's/^m_EditorVersion: //p' "$version_file" | head -n 1
}

fetch_hub_user_info() {
  UNITY_HUB_RESOURCES_DIR="$UNITY_HUB_RESOURCES_DIR" \
    env ELECTRON_RUN_AS_NODE=1 "$UNITY_HUB_BIN" "$SCRIPT_DIR/unity_hub_ipc_user_info.js"
}

extract_json_field() {
  local field_name="$1"
  python3 -c 'import json, sys
payload = json.load(sys.stdin)
value = payload.get(sys.argv[1], "")
if isinstance(value, bool):
    print("true" if value else "false", end="")
else:
    print(value, end="")
' "$field_name"
}

launch_hub_if_needed

licensing_pipe=""
for _ in {1..20}; do
  licensing_pipe="$(find_hub_licensing_pipe)"
  if [[ -n "$licensing_pipe" ]]; then
    break
  fi

  sleep 1
done

if [[ -z "$licensing_pipe" ]]; then
  echo "Unity Hub licensing pipe was not detected." >&2
  echo "Open Unity Hub, confirm you are signed in, then run this script again." >&2
  exit 1
fi

editor_licensing_pipe="$licensing_pipe"
if [[ "$editor_licensing_pipe" == Unity-* ]]; then
  editor_licensing_pipe="${editor_licensing_pipe#Unity-}"
fi

user_info_json="$(fetch_hub_user_info)"
access_token="$(printf '%s' "$user_info_json" | extract_json_field accessToken)"
token_valid="$(printf '%s' "$user_info_json" | extract_json_field valid)"

if [[ -z "$access_token" || "$token_valid" != "true" ]]; then
  echo "Unity Hub access token could not be retrieved." >&2
  echo "Open Unity Hub, confirm you are signed in, then run this script again." >&2
  exit 1
fi

project_version="$(project_editor_version)"
installed_version="$("$UNITY_BIN" -version 2>/dev/null | head -n 1)"
if [[ -n "$project_version" && -n "$installed_version" && "$project_version" != "$installed_version" ]]; then
  echo "Project expects Unity $project_version but UNITY_APP points to $installed_version." >&2
  echo "Update UNITY_APP or open the matching editor before running this script." >&2
  exit 1
fi

editor_log_path="${UNITY_EDITOR_LOG_PATH:-$REPORTS_DIR/unity_editor_live.log}"

launch_args=(
  -projectpath "$PROJECT_DIR"
  -acceptSoftwareTermsForThisRunOnly
  -useHub
  -hubIPC
  -cloudEnvironment "$UNITY_CLOUD_ENVIRONMENT"
  -licensingIpc "$editor_licensing_pipe"
  -hubSessionId "${UNITY_HUB_SESSION_ID:-$(uuidgen)}"
  -accessToken "$access_token"
  -logFile "$editor_log_path"
)

launch_cmd=("$UNITY_BIN" "${launch_args[@]}" "$@")
if [[ "$(uname -m)" == "arm64" ]] && command -v arch >/dev/null 2>&1; then
  launch_cmd=(arch -arm64 "$UNITY_BIN" "${launch_args[@]}" "$@")
fi

if [[ "${UNITY_FOREGROUND:-0}" == "1" ]]; then
  "${launch_cmd[@]}"
  exit $?
fi

nohup "${launch_cmd[@]}" >/dev/null 2>&1 &
disown

echo "Started Unity with Hub-authenticated launch arguments."
echo "Project: $PROJECT_DIR"
echo "Licensing pipe: $editor_licensing_pipe"
echo "Editor log: $editor_log_path"
echo "Wait for compile to finish, then run:"
echo "  zsh $SCRIPT_DIR/unity_smoke_check_mac.sh"
