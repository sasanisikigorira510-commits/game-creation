import argparse
import json
import sys
import time

from unity_battle_ui_check import (
    call,
    post,
    wait_for,
    wait_for_bridge,
    wait_for_play_state,
    wait_for_scene,
    backup_saves,
    restore_saves,
    write_report,
)


PARTY_PRESETS = {
    "starter": [
        "monster_dragon_whelp",
        "monster_apprentice_swordsman",
        "monster_apprentice_mage",
    ],
    "sturdy": [
        "monster_dragon_whelp",
        "monster_chibi_gear",
        "monster_rock_golem",
    ],
    "fusion": [
        "monster_flare_drake",
        "monster_dragon_whelp",
        "monster_apprentice_swordsman",
    ],
    "dev": [
        "monster_flare_drake",
        "monster_dragon_whelp",
        "monster_abyss_dragon",
    ],
}


def parse_int_list(value):
    return [int(part.strip()) for part in value.split(",") if part.strip()]


def ensure_playing_battle_scene(report):
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
        raise RuntimeError(f"Could not open BattleScene: {open_battle}")
    report["steps"].append({"name": "open_battle_scene", "data": open_battle})
    wait_for_scene("Assets/Scenes/BattleScene.unity", timeout_sec=30)

    enter_play = post("/play-mode", {"action": "enter"}, timeout=20)
    report["steps"].append({"name": "enter_play", "data": enter_play})
    wait_for_play_state(True, timeout_sec=30)
    wait_for(lambda: call("/battle-debug").get("ok"), 45, 0.5, "battle-debug availability")


def run_probe(args, report):
    floors = parse_int_list(args.floors)
    levels = parse_int_list(args.levels)
    party_names = [name.strip() for name in args.parties.split(",") if name.strip()]
    results = []

    for party_name in party_names:
        if party_name not in PARTY_PRESETS:
            raise RuntimeError(f"Unknown party preset: {party_name}")

        monsters = PARTY_PRESETS[party_name]
        for floor in floors:
            for level in levels:
                payload = {
                    "monsterIds": monsters,
                    "floor": floor,
                    "level": level,
                    "trials": args.trials,
                    "deltaTime": args.delta_time,
                    "maxSeconds": args.max_seconds,
                    "seed": args.seed + (floor * 1000) + (level * 10) + len(results),
                }
                started = time.time()
                simulation = post("/simulate-battle", payload, timeout=args.request_timeout)
                elapsed = time.time() - started
                if not simulation.get("ok"):
                    raise RuntimeError(f"simulate-battle failed for {party_name} floor {floor} level {level}: {simulation}")

                summary = simulation.get("summary", {})
                results.append({
                    "party": party_name,
                    "monsters": monsters,
                    "floor": floor,
                    "level": level,
                    "trials": args.trials,
                    "requestSeconds": round(elapsed, 3),
                    "firstClearReward": simulation.get("firstClearReward"),
                    "repeatReward": simulation.get("repeatReward"),
                    "summary": summary,
                })

    report["probe"] = {
        "floors": floors,
        "levels": levels,
        "parties": party_names,
        "trials": args.trials,
        "deltaTime": args.delta_time,
        "maxSeconds": args.max_seconds,
        "results": results,
    }


def main():
    parser = argparse.ArgumentParser(description="Run fast BattleSimulator balance probes through the Unity bridge.")
    parser.add_argument("--floors", default="6,7,8")
    parser.add_argument("--levels", default="8,10,12,15,20")
    parser.add_argument("--parties", default="starter,fusion,dev")
    parser.add_argument("--trials", type=int, default=30)
    parser.add_argument("--delta-time", type=float, default=0.05)
    parser.add_argument("--max-seconds", type=float, default=120.0)
    parser.add_argument("--seed", type=int, default=20260702)
    parser.add_argument("--request-timeout", type=int, default=120)
    args = parser.parse_args()

    report = {
        "ok": False,
        "steps": [],
        "args": vars(args),
    }
    entered_play_mode = False
    save_backups = backup_saves()
    report["saveBackups"] = save_backups

    try:
        ensure_playing_battle_scene(report)
        entered_play_mode = True
        run_probe(args, report)
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
        path, latest = write_report(report, "unity-battle-balance-probe")
        print(json.dumps({
            "ok": report["ok"],
            "reportPath": str(path),
            "latestReportPath": str(latest),
            "error": report.get("error", ""),
        }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    sys.exit(main())
