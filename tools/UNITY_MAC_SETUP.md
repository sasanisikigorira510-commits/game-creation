# Unity Mac Setup

## Current editor

- `Unity.app`: `/Applications/Unity/Unity-6000.3.11f1/Unity.app`
- `Project`: `/Users/andou/Desktop/あ/game-creation/WitchTowerGame`

## Scripts

- Open the project in the editor:
  - `zsh /Users/andou/Desktop/あ/game-creation/tools/unity_open_project.sh`
- Run a batch load/compile check:
  - `zsh /Users/andou/Desktop/あ/game-creation/tools/unity_batch_check_mac.sh`
- Run the Unity bridge smoke check after the editor is open:
  - `zsh /Users/andou/Desktop/あ/game-creation/tools/unity_smoke_check_mac.sh`

## Notes

- `unity_open_project.sh` now does all of the following automatically:
  - starts Unity Hub if needed
  - reads the current Hub login state over Hub IPC
  - fetches the current Hub access token
  - launches the editor with Hub-compatible flags
  - writes the live editor log to `/Users/andou/Desktop/あ/game-creation/tools/reports/unity_editor_live.log`
- The launch flags include:
  - `-acceptSoftwareTermsForThisRunOnly`
  - `-useHub`
  - `-hubIPC`
  - `-cloudEnvironment production`
  - `-licensingIpc <detected pipe without the leading Unity->`
  - `-accessToken <retrieved from the running Unity Hub session>`
- The script waits for a running Unity Hub licensing pipe. If it cannot find one, open Unity Hub and confirm you are signed in first.
- If you want to watch the editor boot in the current terminal for debugging, run:
  - `UNITY_FOREGROUND=1 zsh /Users/andou/Desktop/あ/game-creation/tools/unity_open_project.sh`
- Optional env vars:
  - `UNITY_HUB_SESSION_ID` to override the generated Hub session id
  - `UNITY_EDITOR_LOG_PATH` to override the editor log destination
- `unity_smoke_check_mac.sh` depends on the in-editor Unity bridge responding on `http://127.0.0.1:8765`.
- Batch logs are written under `/Users/andou/Desktop/あ/game-creation/tools/reports`.
