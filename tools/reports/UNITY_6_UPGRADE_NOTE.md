# Unity 6 Upgrade Note

## Current State

- Project: `WitchTowerGame`
- Current editor: `2022.3.32f1`
- Target editor: `6000.3.11f1`
- Target release date: `2026-03-11`

## What Was Checked

- `Packages/manifest.json` is simple and only pins `com.unity.textmeshpro` plus built-in modules.
- No heavy third-party Unity packages were found in the manifest.
- Most code is regular runtime code plus custom editor scripts under `Assets/Editor/UnityMcp/`.
- The git worktree already contains many uncommitted changes, so upgrade work should avoid destructive cleanup.

## Known Constraints On This Machine

- Unity Hub is installed.
- Automatic headless install could not be completed from this environment.
- Direct download attempts failed due to local Windows authentication/network constraints.

## Recommended Upgrade Path

1. Open Unity Hub.
2. Install editor `6000.3.11f1`.
3. Keep `2022.3.32f1` installed until the upgraded project opens successfully.
4. Open `WitchTowerGame` in `6000.3.11f1`.
5. Let Unity finish reimporting and package resolution.
6. Review Console errors, especially editor-only scripts under `Assets/Editor/UnityMcp/`.
7. Open `TitleScene`, `HomeScene`, and `BattleScene`.
8. Enter Play Mode for a smoke check.
9. If the project opens cleanly, commit the Unity version upgrade separately from gameplay changes.

## Files To Watch First

- `WitchTowerGame/Assets/Editor/UnityMcp/UnityMcpBridge.cs`
- `WitchTowerGame/Assets/Editor/UnityMcp/UnityMcpSceneBuilder.cs`
- `WitchTowerGame/Assets/Editor/UnityMcp/UnityMcpMasterDataBuilder.cs`
- `WitchTowerGame/Packages/manifest.json`
- `WitchTowerGame/ProjectSettings/ProjectVersion.txt`

## Smoke Test Checklist After Opening In Unity 6

- Console has no compile errors.
- TextMesh Pro assets resolve correctly.
- Main scenes open without missing scripts.
- Buttons still wire up correctly in UI-heavy scenes.
- ScriptableObject master data loads correctly from `Assets/MasterData` and `Resources/MasterData`.
- Play Mode works in `Title`, `Home`, and `Battle`.

## Notes

- Do not manually edit `ProjectVersion.txt` before the Unity 6 editor is actually installed.
- Because this repo already has local modifications, make the Unity version bump easy to isolate when committing.
