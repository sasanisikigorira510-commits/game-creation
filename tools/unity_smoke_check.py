import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


DEFAULT_BASE_URL = "http://127.0.0.1:8765"
BRIDGE_STATE_PATH = Path(__file__).resolve().parent / "unity_bridge_state.json"


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


class UnityBridgeError(RuntimeError):
    pass


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


def wait_for(predicate, timeout_sec, interval_sec=1.5, label="condition"):
    deadline = time.time() + timeout_sec
    last_value = None
    while time.time() < deadline:
        last_value = predicate()
        if last_value:
            return last_value
        time.sleep(interval_sec)
    raise UnityBridgeError(f"Timed out waiting for {label}")


def wait_for_scene(scene_path, timeout_sec=30):
    def probe():
        info = call("/project-info")
        return info if info_matches_scene(info, scene_path) else None

    return wait_for(
        probe,
        timeout_sec,
        label=f"scene {scene_path}",
    )


def info_matches_scene(info, scene_path):
    return info.get("activeScenePath") == scene_path


def wait_for_play_state(expected, timeout_sec=30):
    def probe():
        info = call("/project-info")
        return info if info.get("isPlaying") == expected else None

    return wait_for(
        probe,
        timeout_sec,
        label=f"isPlaying={expected}",
    )


def wait_for_battle_state(expected_state, timeout_sec=60):
    def probe():
        debug = call("/battle-debug")
        return debug if debug.get("ok") and debug.get("flowState") == expected_state else None

    return wait_for(
        probe,
        timeout_sec,
        label=f"battle state {expected_state}",
    )


def text_map(texts):
    return {entry["gameObjectName"]: entry for entry in texts.get("texts", []) if entry.get("active")}


def button_map(buttons):
    return {entry["gameObjectName"]: entry for entry in buttons.get("buttons", []) if entry.get("active")}


def assert_true(condition, message):
    if not condition:
        raise UnityBridgeError(message)


def main():
    report = {"steps": []}

    def record(name, data):
        report["steps"].append({"name": name, "data": data})

    try:
        ping = call("/ping")
        assert_true(ping.get("ok"), "Ping failed")
        record("ping", ping)

        refresh = post("/refresh-assets")
        assert_true(refresh.get("ok"), "Asset refresh failed")
        record("refresh", refresh)

        rebuild = post("/execute-menu-item", {"menuPath": "Tools/MCP/Rebuild Minimal Home Scene"})
        assert_true(rebuild.get("ok"), "Home scene rebuild failed")
        record("rebuild_home", rebuild)

        open_home = post("/open-scene", {"path": "Assets/Scenes/HomeScene.unity"})
        assert_true(open_home.get("ok"), "Could not open HomeScene")
        record("open_home", open_home)

        enter_play = post("/play-mode", {"action": "enter"})
        record("enter_play", enter_play)
        play_info = wait_for_play_state(True, timeout_sec=30)
        record("play_info", play_info)

        show_equipment = post(
            "/invoke-method",
            {"componentType": "WitchTower.Home.PanelSwitcher", "methodName": "ShowEquipment"},
        )
        assert_true(show_equipment.get("ok"), "Could not open Equipment panel")
        record("show_equipment", show_equipment)
        time.sleep(2)

        texts = call("/list-text")
        buttons = call("/list-buttons")
        active_texts = text_map(texts)
        active_buttons = button_map(buttons)
        record("equipment_texts", active_texts)
        record("equipment_buttons", active_buttons)

        assert_true("WeaponText" in active_texts, "WeaponText missing from active equipment panel")
        assert_true("ArmorText" in active_texts, "ArmorText missing from active equipment panel")
        assert_true("AccessoryText" in active_texts, "AccessoryText missing from active equipment panel")
        assert_true("BronzeBladeButton" in active_buttons, "BronzeBladeButton missing from active equipment panel")
        assert_true("IronSwordButton" in active_buttons, "IronSwordButton missing from active equipment panel")

        battle = post(
            "/invoke-method",
            {"componentType": "WitchTower.Home.HomeSceneController", "methodName": "StartBattle"},
        )
        assert_true(battle.get("ok"), "Could not start battle")
        record("start_battle", battle)

        battle_scene = wait_for_scene("Assets/Scenes/BattleScene.unity", timeout_sec=30)
        record("battle_scene", battle_scene)

        fighting = wait_for_battle_state("Fighting", timeout_sec=30)
        record("battle_fighting", fighting)
        assert_true(fighting.get("simulatorRunning") is True, "Battle simulator did not start")

        exit_play = post("/play-mode", {"action": "exit"})
        record("exit_play", exit_play)
        stopped = wait_for_play_state(False, timeout_sec=30)
        record("stopped", stopped)

        boot = post("/open-scene", {"path": "Assets/Scenes/BootScene.unity"})
        assert_true(boot.get("ok"), "Could not open BootScene")
        record("open_boot", boot)

        final_info = call("/project-info")
        record("final_info", final_info)

        print(json.dumps({"ok": True, "report": report}, ensure_ascii=False, indent=2))
        return 0
    except Exception as exc:
        print(json.dumps({"ok": False, "error": str(exc), "report": report}, ensure_ascii=False, indent=2))
        return 1


if __name__ == "__main__":
    sys.exit(main())
