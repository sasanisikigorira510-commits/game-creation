import argparse
import json
import shutil
import sys
import tempfile
import time
import urllib.error
import urllib.request
from pathlib import Path


DEFAULT_BASE_URL = "http://127.0.0.1:8765"
SCRIPT_DIR = Path(__file__).resolve().parent
WORKSPACE_DIR = SCRIPT_DIR.parent
PROJECT_DIR = WORKSPACE_DIR / "WitchTowerGame"
REPORTS_DIR = SCRIPT_DIR / "reports"
BRIDGE_STATE_PATH = SCRIPT_DIR / "unity_bridge_state.json"
DEFAULT_MONSTER_IDS = [
    "monster_flare_drake",
    "monster_dragon_whelp",
    "monster_abyss_dragon",
]


class UnityBridgeError(RuntimeError):
    pass


def resolve_base_url():
    if BRIDGE_STATE_PATH.exists():
        try:
            payload = json.loads(BRIDGE_STATE_PATH.read_text(encoding="utf-8-sig"))
            base_url = str(payload.get("baseUrl", "")).strip()
            if base_url:
                return base_url.rstrip("/")
        except Exception:
            pass

    return DEFAULT_BASE_URL


BASE_URL = resolve_base_url()


def call(path, method="GET", payload=None, timeout=20):
    data = None
    headers = {}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"

    request = urllib.request.Request(f"{BASE_URL}{path}", data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise UnityBridgeError(f"{path} returned HTTP {exc.code}: {body}") from exc
    except urllib.error.URLError as exc:
        raise UnityBridgeError(f"Could not reach Unity bridge at {BASE_URL}: {exc}") from exc


def post(path, payload=None, timeout=20):
    return call(path, method="POST", payload=payload or {}, timeout=timeout)


def wait_for(predicate, timeout_sec, interval_sec=0.5, label="condition"):
    deadline = time.time() + timeout_sec
    last_value = None
    last_error = None
    while time.time() < deadline:
        try:
            last_value = predicate()
            if last_value:
                return last_value
        except Exception as exc:
            last_error = exc
        time.sleep(interval_sec)

    suffix = f"; last value: {last_value}" if last_value is not None else ""
    if last_error is not None:
        suffix += f"; last error: {last_error}"
    raise UnityBridgeError(f"Timed out waiting for {label}{suffix}")


def wait_for_bridge(timeout_sec=45):
    return wait_for(lambda: call("/ping", timeout=5).get("ok"), timeout_sec, 1.0, "Unity bridge ping")


def wait_for_play_state(expected, timeout_sec=30):
    def probe():
        info = call("/project-info")
        return info if info.get("isPlaying") == expected else None

    return wait_for(probe, timeout_sec, 0.75, f"isPlaying={expected}")


def wait_for_scene(scene_path, timeout_sec=30):
    def probe():
        info = call("/project-info")
        return info if info.get("activeScenePath") == scene_path else None

    return wait_for(probe, timeout_sec, 0.75, f"scene {scene_path}")


def wait_for_battle_floor(floor, timeout_sec=45):
    def probe():
        debug = call("/battle-debug")
        if not debug.get("ok"):
            return None

        if debug.get("flowState") == "Fighting" and debug.get("currentFloor") == floor:
            return debug

        return None

    return wait_for(probe, timeout_sec, 0.5, f"battle floor {floor}")


def parse_project_identity():
    settings_path = PROJECT_DIR / "ProjectSettings" / "ProjectSettings.asset"
    company = "DefaultCompany"
    product = "WitchTowerGame"
    if not settings_path.exists():
        return company, product

    for line in settings_path.read_text(encoding="utf-8", errors="replace").splitlines():
        stripped = line.strip()
        if stripped.startswith("companyName:"):
            company = stripped.split(":", 1)[1].strip() or company
        elif stripped.startswith("productName:"):
            product = stripped.split(":", 1)[1].strip() or product

    return company, product


def discover_save_paths():
    company, product = parse_project_identity()
    app_support = Path.home() / "Library" / "Application Support"
    candidates = [
        app_support / company / product / "save.json",
        app_support / "DefaultCompany" / product / "save.json",
    ]
    deduped = []
    for path in candidates:
        if path not in deduped:
            deduped.append(path)
    return deduped


def backup_saves():
    backup_root = Path(tempfile.mkdtemp(prefix="witchtower-save-backup-"))
    records = []
    for index, save_path in enumerate(discover_save_paths()):
        backup_path = backup_root / f"save_{index}.json"
        existed = save_path.exists()
        if existed:
            backup_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(save_path, backup_path)
        records.append({
            "savePath": str(save_path),
            "backupPath": str(backup_path),
            "existed": existed,
        })
    return records


def restore_saves(records):
    restored = []
    for record in records:
        save_path = Path(record["savePath"])
        backup_path = Path(record["backupPath"])
        if record.get("existed") and backup_path.exists():
            save_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(backup_path, save_path)
            restored.append({"savePath": str(save_path), "restored": True})
        elif not record.get("existed") and save_path.exists():
            save_path.unlink()
            restored.append({"savePath": str(save_path), "removed": True})
        else:
            restored.append({"savePath": str(save_path), "restored": False})
    return restored


def compact_battle_sample(debug):
    return {
        "time": round(time.time(), 3),
        "flowState": debug.get("flowState"),
        "floor": debug.get("currentFloor"),
        "target": debug.get("enemyCountTarget"),
        "remaining": debug.get("remainingEnemyCount"),
        "spawned": debug.get("spawnedEnemyCount"),
        "active": debug.get("activeEnemyCount"),
        "waveHudFill": debug.get("waveHudFillAmount"),
        "waveHudText": debug.get("waveHudEnemyCountText"),
        "enemyHpFill": debug.get("enemyHpFillAmount"),
        "displayedWaveEnemyRatio": debug.get("displayedWaveEnemyRatio"),
        "floatingDamageTextCount": debug.get("floatingDamageTextCount"),
        "activeHpTrailCount": debug.get("activeHpTrailCount"),
    }


def validate_wave_hud(sample, tolerance):
    target = sample.get("target")
    remaining = sample.get("remaining")
    fill = sample.get("waveHudFill")
    text = sample.get("waveHudText") or ""
    if not isinstance(target, int) or target <= 0 or not isinstance(remaining, int):
        return []

    errors = []
    expected_fill = max(0.0, min(1.0, remaining / target))
    if isinstance(fill, (int, float)) and abs(fill - expected_fill) > tolerance:
        errors.append(
            f"wave HUD fill mismatch: remaining={remaining} target={target} fill={fill} expected={expected_fill:.3f}"
        )

    expected_text = f"残り {remaining} / {target}"
    if text and text != expected_text:
        errors.append(f"wave HUD text mismatch: text={text!r} expected={expected_text!r}")

    return errors


def observe_battle_ui(args, report):
    payload = {
        "monsterIds": args.monsters,
        "floor": args.floor,
        "level": args.level,
        "restartBattle": True,
    }
    debug_party = post("/set-debug-party", payload, timeout=30)
    if not debug_party.get("ok"):
        raise UnityBridgeError(f"set-debug-party failed: {debug_party}")
    report["steps"].append({"name": "set_debug_party", "data": debug_party})

    first_debug = wait_for_battle_floor(args.floor, timeout_sec=45)
    report["steps"].append({"name": "battle_ready", "data": compact_battle_sample(first_debug)})

    samples = []
    errors = []
    hp_increases = []
    saw_remaining_drop = False
    saw_damage_text = False
    saw_hp_trail = False
    baseline_remaining = None
    previous_enemy_hp_fill = None
    previous_displayed_wave_ratio = None
    deadline = time.time() + args.duration

    while time.time() < deadline:
        debug = call("/battle-debug")
        sample = compact_battle_sample(debug)
        samples.append(sample)

        if debug.get("ok") and sample.get("flowState") == "Fighting" and sample.get("floor") == args.floor:
            errors.extend(validate_wave_hud(sample, args.fill_tolerance))
            remaining = sample.get("remaining")
            if baseline_remaining is None and isinstance(remaining, int):
                baseline_remaining = remaining
            if isinstance(remaining, int) and baseline_remaining is not None and remaining < baseline_remaining:
                saw_remaining_drop = True

            enemy_hp_fill = sample.get("enemyHpFill")
            if isinstance(enemy_hp_fill, (int, float)) and enemy_hp_fill >= 0:
                if (
                    previous_enemy_hp_fill is not None
                    and enemy_hp_fill > previous_enemy_hp_fill + args.hp_increase_tolerance
                ):
                    hp_increases.append({
                        "field": "enemyHpFill",
                        "previous": previous_enemy_hp_fill,
                        "current": enemy_hp_fill,
                        "sample": sample,
                    })
                previous_enemy_hp_fill = enemy_hp_fill

            displayed_ratio = sample.get("displayedWaveEnemyRatio")
            if isinstance(displayed_ratio, (int, float)) and displayed_ratio >= 0:
                if (
                    previous_displayed_wave_ratio is not None
                    and displayed_ratio > previous_displayed_wave_ratio + args.hp_increase_tolerance
                ):
                    hp_increases.append({
                        "field": "displayedWaveEnemyRatio",
                        "previous": previous_displayed_wave_ratio,
                        "current": displayed_ratio,
                        "sample": sample,
                    })
                previous_displayed_wave_ratio = displayed_ratio

            saw_damage_text = saw_damage_text or sample.get("floatingDamageTextCount", 0) > 0
            saw_hp_trail = saw_hp_trail or sample.get("activeHpTrailCount", 0) > 0

            if saw_remaining_drop and saw_damage_text and len(samples) >= args.min_samples:
                break

        if sample.get("flowState") == "Result":
            break

        time.sleep(args.interval)

    report["samples"] = samples
    report["uiChecks"] = {
        "sawRemainingDrop": saw_remaining_drop,
        "sawDamageText": saw_damage_text,
        "sawHpTrail": saw_hp_trail,
        "baselineRemaining": baseline_remaining,
        "lastSample": samples[-1] if samples else None,
        "waveHudErrors": errors,
        "hpIncreases": hp_increases,
    }

    if not samples:
        raise UnityBridgeError("No battle-debug samples collected.")
    if errors:
        raise UnityBridgeError(errors[0])
    if hp_increases:
        first = hp_increases[0]
        raise UnityBridgeError(
            f"{first['field']} increased from {first['previous']} to {first['current']} during battle UI check."
        )
    if not saw_remaining_drop:
        raise UnityBridgeError("Remaining enemy count did not decrease during the observation window.")
    if not saw_damage_text:
        raise UnityBridgeError("Floating damage text was not observed during the battle UI check.")


def verify_debug_reward_overlay(report):
    result = post(
        "/invoke-method",
        {
            "componentType": "WitchTower.Battle.BattleSceneController",
            "methodName": "ShowDebugRewardResult",
        },
        timeout=30,
    )
    if not result.get("ok"):
        raise UnityBridgeError(f"ShowDebugRewardResult failed: {result}")
    report["steps"].append({"name": "show_debug_reward_result", "data": result})
    time.sleep(0.5)

    texts = call("/list-text")
    buttons = call("/list-buttons")
    active_texts = [
        entry.get("text", "")
        for entry in texts.get("texts", [])
        if entry.get("active") and entry.get("text")
    ]
    active_buttons = [
        entry
        for entry in buttons.get("buttons", [])
        if entry.get("active")
    ]
    joined_text = "\n".join(active_texts)
    expected_terms = ["勝利", "青銅の刃", "ヒナドラ", "レベルアップ"]
    missing_terms = [term for term in expected_terms if term not in joined_text]
    labels = [entry.get("label", "") for entry in active_buttons]
    report["rewardOverlay"] = {
        "expectedTerms": expected_terms,
        "missingTerms": missing_terms,
        "activeButtonLabels": labels,
        "activeTextCount": len(active_texts),
    }

    if missing_terms:
        raise UnityBridgeError("Reward overlay missing expected text: " + ", ".join(missing_terms))
    if not any("ホーム" in label for label in labels):
        raise UnityBridgeError("Reward overlay home button label was not found.")


def write_report(report, name="unity-battle-ui-check"):
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    timestamp = time.strftime("%Y%m%d-%H%M%S")
    path = REPORTS_DIR / f"{name}-{timestamp}.json"
    latest = REPORTS_DIR / f"{name}-latest.json"
    text = json.dumps(report, ensure_ascii=False, indent=2)
    path.write_text(text, encoding="utf-8")
    latest.write_text(text, encoding="utf-8")
    return path, latest


def main():
    parser = argparse.ArgumentParser(description="Run a short Unity battle UI verification through the local bridge.")
    parser.add_argument("--floor", type=int, default=7)
    parser.add_argument("--level", type=int, default=35)
    parser.add_argument("--duration", type=float, default=35.0)
    parser.add_argument("--interval", type=float, default=0.25)
    parser.add_argument("--min-samples", type=int, default=8)
    parser.add_argument("--fill-tolerance", type=float, default=0.035)
    parser.add_argument("--hp-increase-tolerance", type=float, default=0.025)
    parser.add_argument("--monster", dest="monsters", action="append", default=[])
    args = parser.parse_args()
    if not args.monsters:
        args.monsters = DEFAULT_MONSTER_IDS

    report = {
        "ok": False,
        "baseUrl": BASE_URL,
        "floor": args.floor,
        "level": args.level,
        "monsters": args.monsters,
        "steps": [],
    }
    entered_play_mode = False
    save_backups = backup_saves()
    report["saveBackups"] = save_backups

    try:
        wait_for_bridge()
        ping = call("/ping")
        report["steps"].append({"name": "ping", "data": ping})

        try:
            refresh = post("/refresh-assets", timeout=60)
            report["steps"].append({"name": "refresh_assets", "data": refresh})
        except Exception as exc:
            report["steps"].append({"name": "refresh_assets_warning", "data": str(exc)})
            wait_for_bridge()

        info = call("/project-info")
        if info.get("isPlaying"):
            exit_play = post("/play-mode", {"action": "exit"}, timeout=20)
            report["steps"].append({"name": "pre_exit_play", "data": exit_play})
            wait_for_play_state(False, timeout_sec=30)

        open_battle = post("/open-scene", {"path": "Assets/Scenes/BattleScene.unity"}, timeout=30)
        if not open_battle.get("ok"):
            raise UnityBridgeError(f"Could not open BattleScene: {open_battle}")
        report["steps"].append({"name": "open_battle_scene", "data": open_battle})
        wait_for_scene("Assets/Scenes/BattleScene.unity", timeout_sec=30)

        enter_play = post("/play-mode", {"action": "enter"}, timeout=20)
        report["steps"].append({"name": "enter_play", "data": enter_play})
        wait_for_play_state(True, timeout_sec=30)
        entered_play_mode = True
        wait_for(lambda: call("/battle-debug").get("ok"), 45, 0.5, "battle-debug availability")

        observe_battle_ui(args, report)
        verify_debug_reward_overlay(report)

        report["ok"] = True
        return 0
    except Exception as exc:
        report["error"] = str(exc)
        return 1
    finally:
        cleanup_steps = []
        try:
            info = call("/project-info")
            if entered_play_mode or info.get("isPlaying"):
                cleanup_exit = post("/play-mode", {"action": "exit"}, timeout=20)
                cleanup_steps.append({"name": "exit_play", "data": cleanup_exit})
                wait_for_play_state(False, timeout_sec=30)
        except Exception as exc:
            cleanup_steps.append({"name": "exit_play_warning", "data": str(exc)})

        try:
            cleanup_steps.append({"name": "restore_saves", "data": restore_saves(save_backups)})
        except Exception as exc:
            cleanup_steps.append({"name": "restore_saves_error", "data": str(exc)})

        try:
            boot = post("/open-scene", {"path": "Assets/Scenes/BootScene.unity"}, timeout=30)
            cleanup_steps.append({"name": "open_boot_scene", "data": boot})
        except Exception as exc:
            cleanup_steps.append({"name": "open_boot_scene_warning", "data": str(exc)})

        report["cleanup"] = cleanup_steps
        path, latest = write_report(report)
        print(json.dumps({
            "ok": report["ok"],
            "reportPath": str(path),
            "latestReportPath": str(latest),
            "error": report.get("error", ""),
        }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    sys.exit(main())
