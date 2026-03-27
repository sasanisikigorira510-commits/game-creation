import json
import os
import sys
import traceback
import urllib.error
import urllib.request
from pathlib import Path


PROTOCOL_VERSION = "2024-11-05"
DEFAULT_BASE_URL = "http://127.0.0.1:8765"
BRIDGE_STATE_PATH = Path(__file__).resolve().parent / "unity_bridge_state.json"
REPORTS_DIR = Path(__file__).resolve().parent / "reports"


def load_bridge_state():
    if not BRIDGE_STATE_PATH.exists():
        return {"exists": False}

    try:
        payload = json.loads(BRIDGE_STATE_PATH.read_text(encoding="utf-8-sig"))
        payload["exists"] = True
        return payload
    except Exception as exc:
        return {"exists": True, "parseError": str(exc)}


def resolve_base_url():
    env_url = os.environ.get("UNITY_MCP_BASE_URL")
    if env_url:
        return env_url.rstrip("/")

    payload = load_bridge_state()
    base_url = str(payload.get("baseUrl", "")).strip()
    if base_url:
        return base_url.rstrip("/")

    return DEFAULT_BASE_URL


TOOLS = [
    {
        "name": "unity_bridge_status",
        "description": "Read local Unity bridge state and try a direct ping without requiring the main MCP Unity tools to succeed.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_latest_report",
        "description": "Read the newest saved Unity smoke or bridge-status report from tools/reports.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "kind": {
                    "type": "string",
                    "enum": ["any", "smoke", "bridge-status"],
                    "description": "Which family of saved reports to read.",
                }
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "unity_list_reports",
        "description": "List recent saved Unity smoke or bridge-status reports from tools/reports.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "kind": {
                    "type": "string",
                    "enum": ["any", "smoke", "bridge-status"],
                    "description": "Which family of saved reports to list.",
                },
                "limit": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 20,
                    "description": "Maximum number of report files to return.",
                },
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "unity_report_summary",
        "description": "Return a compact summary of the newest saved Unity smoke or bridge-status report.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "kind": {
                    "type": "string",
                    "enum": ["smoke", "bridge-status"],
                    "description": "Which family of saved report to summarize.",
                }
            },
            "required": ["kind"],
            "additionalProperties": False,
        },
    },
    {
        "name": "unity_status_overview",
        "description": "Return a combined overview of bridge status, latest smoke summary, latest bridge-status summary, and recent reports.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_compare_reports",
        "description": "Compare the two most recent saved smoke or bridge-status reports.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "kind": {
                    "type": "string",
                    "enum": ["smoke", "bridge-status"],
                    "description": "Which family of saved reports to compare.",
                }
            },
            "required": ["kind"],
            "additionalProperties": False,
        },
    },
    {
        "name": "unity_status_brief",
        "description": "Return a short human-readable summary of current bridge reachability and latest smoke status.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_recovery_hint",
        "description": "Return the current bridge diagnosis code and the most likely recovery action.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_ping",
        "description": "Check whether the Unity Editor bridge is reachable.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_project_info",
        "description": "Get current Unity project information from the open Editor.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_list_scenes",
        "description": "List all scene assets known to the current Unity project.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_list_components",
        "description": "List MonoBehaviour components in the currently open scenes.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_list_text",
        "description": "List TMP text components and their current text in the open scenes.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_list_buttons",
        "description": "List Unity UI buttons, including active/interactable state and label text.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_console",
        "description": "Read recent Unity console log entries captured by the bridge.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_clear_console",
        "description": "Clear the in-memory Unity console log buffer captured by the bridge.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_battle_debug",
        "description": "Read a live snapshot of battle scene state, simulator stats, and UI text.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_home_debug",
        "description": "Read current player profile, upgrades, missions, and equipped items from Home/runtime state.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_open_scene",
        "description": "Open a scene in the Unity Editor by asset path.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "Unity asset path such as Assets/Scenes/TitleScene.unity",
                }
            },
            "required": ["path"],
            "additionalProperties": False,
        },
    },
    {
        "name": "unity_refresh_assets",
        "description": "Refresh the Unity AssetDatabase.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "unity_execute_menu_item",
        "description": "Execute a Unity Editor menu item by path.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "menuPath": {
                    "type": "string",
                    "description": "Unity menu path such as Assets/Refresh or Tools/MCP/Restart Bridge",
                }
            },
            "required": ["menuPath"],
            "additionalProperties": False,
        },
    },
    {
        "name": "unity_play_mode",
        "description": "Enter, exit, or toggle Unity play mode.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "enum": ["enter", "exit", "toggle"],
                    "description": "Requested play mode transition.",
                }
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "unity_invoke_method",
        "description": "Invoke a public parameterless MonoBehaviour method in the currently open scene.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "componentType": {
                    "type": "string",
                    "description": "Component class name or full type name, such as TitleSceneController or WitchTower.Core.TitleSceneController",
                },
                "methodName": {
                    "type": "string",
                    "description": "Public instance method name with no parameters.",
                },
            },
            "required": ["componentType", "methodName"],
            "additionalProperties": False,
        },
    },
    {
        "name": "unity_simulate_idle_reward",
        "description": "Simulate elapsed idle time and generate pending idle reward for the current player profile.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "minutes": {
                    "type": "integer",
                    "minimum": 1,
                    "description": "How many minutes of idle time to simulate.",
                }
            },
            "required": ["minutes"],
            "additionalProperties": False,
        },
    },
]


def read_message():
    headers = {}
    while True:
        line = sys.stdin.buffer.readline()
        if not line:
            return None
        if line in (b"\r\n", b"\n"):
            break
        decoded = line.decode("utf-8").strip()
        if ":" in decoded:
            key, value = decoded.split(":", 1)
            headers[key.strip().lower()] = value.strip()

    length = int(headers.get("content-length", "0"))
    if length <= 0:
        return None

    body = sys.stdin.buffer.read(length)
    if not body:
        return None

    return json.loads(body.decode("utf-8"))


def send_message(payload):
    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    header = f"Content-Length: {len(body)}\r\n\r\n".encode("ascii")
    sys.stdout.buffer.write(header)
    sys.stdout.buffer.write(body)
    sys.stdout.buffer.flush()


def send_response(request_id, result):
    send_message({"jsonrpc": "2.0", "id": request_id, "result": result})


def send_error(request_id, code, message):
    send_message(
        {
            "jsonrpc": "2.0",
            "id": request_id,
            "error": {"code": code, "message": message},
        }
    )


def call_unity(path, method="GET", payload=None):
    base_url = resolve_base_url()
    bridge_state = load_bridge_state()
    data = None
    headers = {}

    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"

    request = urllib.request.Request(f"{base_url}{path}", data=data, headers=headers, method=method)

    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            raw = response.read().decode("utf-8")
            return json.loads(raw)
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(
            f"Unity bridge returned HTTP {exc.code} from {base_url}: {raw}. "
            f"Bridge state file: {BRIDGE_STATE_PATH} -> {json.dumps(bridge_state, ensure_ascii=False)}"
        )
    except urllib.error.URLError as exc:
        raise RuntimeError(
            f"Could not reach Unity bridge at {base_url}. "
            f"Bridge state file: {BRIDGE_STATE_PATH} -> {json.dumps(bridge_state, ensure_ascii=False)}. "
            f"Open the Unity project and wait for compilation to finish. Details: {exc}"
        )


def get_bridge_status():
    state = load_bridge_state()
    base_url = resolve_base_url()

    result = {
        "ok": True,
        "bridgeStatePath": str(BRIDGE_STATE_PATH),
        "state": state,
        "resolvedBaseUrl": base_url,
    }

    try:
        request = urllib.request.Request(f"{base_url}/ping", method="GET")
        with urllib.request.urlopen(request, timeout=5) as response:
            raw = response.read().decode("utf-8")
            result["ping"] = {"ok": True, "response": json.loads(raw)}
    except Exception as exc:
        result["ping"] = {"ok": False, "error": str(exc)}

    return result


def get_latest_report(kind):
    if not REPORTS_DIR.exists():
        return {
            "ok": False,
            "message": f"Reports directory not found: {REPORTS_DIR}",
        }

    patterns = {
        "any": "*.json",
        "smoke": "unity-smoke-*.json",
        "bridge-status": "unity-bridge-status-*.json",
    }
    pattern = patterns.get(kind or "any", "*.json")
    candidates = sorted(REPORTS_DIR.glob(pattern), key=lambda path: path.stat().st_mtime, reverse=True)
    if not candidates:
        return {
            "ok": False,
            "message": f"No reports matched {pattern} in {REPORTS_DIR}",
        }

    latest = candidates[0]
    try:
        payload = json.loads(latest.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        return {
            "ok": False,
            "message": f"Could not parse report {latest}: {exc}",
        }

    return {
        "ok": True,
        "reportPath": str(latest),
        "kind": kind or "any",
        "report": payload,
    }


def list_reports(kind, limit):
    if not REPORTS_DIR.exists():
        return {
            "ok": False,
            "message": f"Reports directory not found: {REPORTS_DIR}",
        }

    patterns = {
        "any": "*.json",
        "smoke": "unity-smoke-*.json",
        "bridge-status": "unity-bridge-status-*.json",
    }
    resolved_kind = kind or "any"
    pattern = patterns.get(resolved_kind, "*.json")
    resolved_limit = max(1, min(int(limit or 10), 20))
    candidates = sorted(REPORTS_DIR.glob(pattern), key=lambda path: path.stat().st_mtime, reverse=True)

    reports = []
    for path in candidates[:resolved_limit]:
        stat = path.stat()
        reports.append(
            {
                "name": path.name,
                "path": str(path),
                "size": stat.st_size,
                "modifiedAt": stat.st_mtime,
            }
        )

    return {
        "ok": True,
        "kind": resolved_kind,
        "count": len(reports),
        "reports": reports,
    }


def summarize_report(kind):
    latest = get_latest_report(kind)
    if not latest.get("ok"):
        return latest

    return summarize_payload(kind, latest.get("reportPath"), latest.get("report", {}))


def summarize_payload(kind, report_path, report):
    summary = {
        "ok": True,
        "kind": kind,
        "reportPath": report_path,
    }

    if kind == "bridge-status":
        ping = report.get("ping", {})
        state = report.get("state", {})
        port_status = report.get("portStatus", {})
        port_owner = report.get("portOwner", {})
        summary.update(
            {
                "bridgeStateExists": state.get("exists", False),
                "resolvedBaseUrl": report.get("resolvedBaseUrl"),
                "pingOk": ping.get("ok", False),
                "pingError": ping.get("error", ""),
                "portListeningCount": port_status.get("listeningCount", 0),
                "portCloseWaitCount": port_status.get("closeWaitCount", 0),
                "ownerPid": port_owner.get("pid"),
                "ownerProcessName": port_owner.get("processName"),
                "ownerResponding": port_owner.get("responding"),
                "running": state.get("running"),
                "processId": state.get("processId"),
                "projectPath": state.get("projectPath"),
                "timestamp": state.get("timestamp"),
            }
        )
        return summary

    steps = report.get("steps", [])
    step_names = [step.get("name", "") for step in steps]
    final_info = next((step.get("data") for step in reversed(steps) if step.get("name") == "final_info"), None)
    battle_result = next((step.get("data") for step in reversed(steps) if "battle_result" in step.get("name", "")), None)
    summary.update(
        {
            "scenario": report.get("scenario"),
            "reportOk": report.get("ok", False),
            "generatedAt": report.get("generatedAt"),
            "error": report.get("error", ""),
            "stepCount": len(steps),
            "hasBattleResult": battle_result is not None,
            "finalScene": final_info.get("activeScenePath") if isinstance(final_info, dict) else None,
            "isPlaying": final_info.get("isPlaying") if isinstance(final_info, dict) else None,
            "lastStep": step_names[-1] if step_names else "",
        }
    )
    return summary


def get_status_overview():
    bridge = get_bridge_status()
    latest_smoke = summarize_report("smoke")
    latest_bridge_report = summarize_report("bridge-status")
    recent_smoke_reports = list_reports("smoke", 3)
    recent_bridge_reports = list_reports("bridge-status", 3)
    return {
        "ok": True,
        "bridge": bridge,
        "latestSmoke": latest_smoke,
        "latestBridgeReport": latest_bridge_report,
        "recentSmokeReports": recent_smoke_reports,
        "recentBridgeReports": recent_bridge_reports,
        "diagnosis": diagnose_bridge(bridge, latest_smoke),
    }


def diagnose_bridge(bridge, latest_smoke):
    ping_ok = bool(bridge.get("ping", {}).get("ok"))
    port_status = bridge.get("portStatus", {})
    port_owner = bridge.get("portOwner", {})
    compile_status = bridge.get("compileStatus", {})
    listening_count = int(port_status.get("listeningCount", 0) or 0)
    close_wait_count = int(port_status.get("closeWaitCount", 0) or 0)
    owner_ok = bool(port_owner.get("ok"))
    compile_stale = bool(compile_status.get("stale"))

    if ping_ok:
        return {
            "code": "healthy",
            "summary": "Bridge is reachable.",
            "likelyCause": "No bridge connectivity issue is visible.",
        }

    if listening_count > 0 and compile_stale:
        return {
            "code": "compile_stale",
            "summary": "Unity is listening, but the editor assembly is older than UnityMcpBridge.cs.",
            "likelyCause": "Unity has not recompiled recent bridge changes yet.",
        }

    if listening_count > 0 and close_wait_count > 0 and owner_ok:
        return {
            "code": "listener_stuck",
            "summary": "Unity owns the port, but HTTP requests are timing out and CLOSE_WAIT sockets are accumulating.",
            "likelyCause": "The bridge listener is wedged or not draining connections.",
        }

    if listening_count > 0:
        return {
            "code": "listener_unresponsive",
            "summary": "A process is listening on the Unity bridge port, but ping is still failing.",
            "likelyCause": "The listener exists but is not responding.",
        }

    if (not ping_ok) and latest_smoke.get("error"):
        return {
            "code": "bridge_down",
            "summary": "Bridge is unreachable and the latest smoke failed on connectivity.",
            "likelyCause": "Unity bridge is not active or not bound to the expected URL.",
        }

    return {
        "code": "unknown",
        "summary": "Bridge is unreachable.",
        "likelyCause": "No stronger diagnosis was available.",
    }


def compare_reports(kind):
    listing = list_reports(kind, 2)
    if not listing.get("ok"):
        return listing

    reports = listing.get("reports", [])
    if len(reports) < 2:
        return {
            "ok": False,
            "kind": kind,
            "message": f"Need at least 2 {kind} reports to compare.",
        }

    latest_path = Path(reports[0]["path"])
    previous_path = Path(reports[1]["path"])

    try:
        latest_payload = json.loads(latest_path.read_text(encoding="utf-8-sig"))
        previous_payload = json.loads(previous_path.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        return {
            "ok": False,
            "kind": kind,
            "message": f"Could not parse reports: {exc}",
        }

    latest_summary = summarize_payload(kind, str(latest_path), latest_payload)
    previous_summary = summarize_payload(kind, str(previous_path), previous_payload)

    diff = {"kind": kind}
    if kind == "bridge-status":
        diff.update(
            {
                "pingChanged": latest_summary.get("pingOk") != previous_summary.get("pingOk"),
                "baseUrlChanged": latest_summary.get("resolvedBaseUrl") != previous_summary.get("resolvedBaseUrl"),
                "runningChanged": latest_summary.get("running") != previous_summary.get("running"),
                "ownerPidChanged": latest_summary.get("ownerPid") != previous_summary.get("ownerPid"),
                "closeWaitChanged": latest_summary.get("portCloseWaitCount") != previous_summary.get("portCloseWaitCount"),
                "previousPingOk": previous_summary.get("pingOk"),
                "latestPingOk": latest_summary.get("pingOk"),
                "previousPingError": previous_summary.get("pingError"),
                "latestPingError": latest_summary.get("pingError"),
                "previousOwnerPid": previous_summary.get("ownerPid"),
                "latestOwnerPid": latest_summary.get("ownerPid"),
                "previousCloseWaitCount": previous_summary.get("portCloseWaitCount"),
                "latestCloseWaitCount": latest_summary.get("portCloseWaitCount"),
            }
        )
    else:
        diff.update(
            {
                "reportOkChanged": latest_summary.get("reportOk") != previous_summary.get("reportOk"),
                "errorChanged": latest_summary.get("error") != previous_summary.get("error"),
                "lastStepChanged": latest_summary.get("lastStep") != previous_summary.get("lastStep"),
                "previousReportOk": previous_summary.get("reportOk"),
                "latestReportOk": latest_summary.get("reportOk"),
                "previousError": previous_summary.get("error"),
                "latestError": latest_summary.get("error"),
                "previousLastStep": previous_summary.get("lastStep"),
                "latestLastStep": latest_summary.get("lastStep"),
            }
        )

    return {
        "ok": True,
        "kind": kind,
        "latest": latest_summary,
        "previous": previous_summary,
        "diff": diff,
    }


def get_status_brief():
    overview = get_status_overview()
    bridge = overview.get("bridge", {})
    latest_smoke = overview.get("latestSmoke", {})
    recent_smoke_reports = overview.get("recentSmokeReports", {})
    recent_bridge_reports = overview.get("recentBridgeReports", {})

    lines = [
        "Unity Status Brief",
        "Bridge: " + ("reachable" if bridge.get("ping", {}).get("ok") else "unreachable"),
        "Base URL: " + str(bridge.get("resolvedBaseUrl", "")),
    ]

    port_status = bridge.get("portStatus", {})
    lines.append(
        "Port 8765: listening="
        + str(port_status.get("listeningCount", 0))
        + ", close_wait="
        + str(port_status.get("closeWaitCount", 0))
    )

    port_owner = bridge.get("portOwner", {})
    if port_owner.get("ok"):
        lines.append(
            "Port Owner: pid="
            + str(port_owner.get("pid"))
            + ", process="
            + str(port_owner.get("processName"))
            + ", responding="
            + str(port_owner.get("responding"))
        )

    compile_status = bridge.get("compileStatus", {})
    if compile_status.get("ok"):
        lines.append(
            "Compile: stale="
            + str(compile_status.get("stale"))
            + ", script_ahead_seconds="
            + str(compile_status.get("scriptAheadSeconds"))
        )

    diagnosis = overview.get("diagnosis")
    if diagnosis:
        lines.append("Diagnosis: " + str(diagnosis.get("code", "")))
        lines.append("Diagnosis Summary: " + str(diagnosis.get("summary", "")))

    if not bridge.get("ping", {}).get("ok"):
        lines.append("Bridge Error: " + str(bridge.get("ping", {}).get("error", "")))

    lines.extend(
        [
            "Latest Smoke: " + ("ok" if latest_smoke.get("reportOk") else "failed"),
            "Smoke Error: " + str(latest_smoke.get("error", "")),
            "Smoke Last Step: " + str(latest_smoke.get("lastStep", "")),
            "Recent Smoke Reports: " + str(recent_smoke_reports.get("count", 0)),
            "Recent Bridge Reports: " + str(recent_bridge_reports.get("count", 0)),
        ]
    )

    return {
        "ok": True,
        "text": "\n".join(lines),
        "overview": overview,
    }


def get_recovery_hint():
    overview = get_status_overview()
    diagnosis = overview.get("diagnosis", {})
    bridge = overview.get("bridge", {})
    code = str(diagnosis.get("code", "unknown"))

    result = {
        "ok": True,
        "diagnosis": diagnosis,
        "recommendedAction": "",
        "notes": [],
    }

    if code == "healthy":
        result["recommendedAction"] = "No recovery action is needed."
        result["notes"].append("Bridge is already reachable.")
    elif code == "compile_stale":
        result["recommendedAction"] = "Let Unity finish recompiling or trigger an editor refresh/recompile."
        result["notes"].append("UnityMcpBridge.cs is newer than Assembly-CSharp-Editor.dll.")
        result["notes"].append("Bridge requests are still hitting the old listener instance.")
    elif code == "listener_stuck":
        result["recommendedAction"] = "Restart the Unity bridge or reload scripts in the editor."
        result["notes"].append("Port 8765 is still owned by Unity.")
        result["notes"].append("CLOSE_WAIT sockets suggest the listener is not draining completed connections.")
    elif code == "listener_unresponsive":
        result["recommendedAction"] = "Check the editor for a blocked compile/domain reload and restart the bridge if needed."
        result["notes"].append("A process is listening, but ping still fails.")
    elif code == "bridge_down":
        result["recommendedAction"] = "Open the project in Unity and wait for the bridge to start."
        result["notes"].append("No responsive bridge was found at the resolved base URL.")
    else:
        result["recommendedAction"] = "Inspect Unity editor state and rerun bridge diagnostics."
        result["notes"].append("The bridge is unreachable, but the cause was not classified.")

    port_owner = bridge.get("portOwner", {})
    if port_owner.get("ok"):
        result["notes"].append(
            f"Current owner PID: {port_owner.get('pid')} ({port_owner.get('processName')})"
        )

    return result


def tool_result(data):
    pretty = json.dumps(data, ensure_ascii=False, indent=2)
    return {"content": [{"type": "text", "text": pretty}], "structuredContent": data, "isError": not data.get("ok", True)}


def handle_tool_call(name, arguments):
    arguments = arguments or {}

    if name == "unity_bridge_status":
        return tool_result(get_bridge_status())
    if name == "unity_latest_report":
        return tool_result(get_latest_report(arguments.get("kind", "any")))
    if name == "unity_list_reports":
        return tool_result(list_reports(arguments.get("kind", "any"), arguments.get("limit", 10)))
    if name == "unity_report_summary":
        return tool_result(summarize_report(arguments["kind"]))
    if name == "unity_status_overview":
        return tool_result(get_status_overview())
    if name == "unity_compare_reports":
        return tool_result(compare_reports(arguments["kind"]))
    if name == "unity_status_brief":
        return tool_result(get_status_brief())
    if name == "unity_recovery_hint":
        return tool_result(get_recovery_hint())
    if name == "unity_ping":
        return tool_result(call_unity("/ping"))
    if name == "unity_project_info":
        return tool_result(call_unity("/project-info"))
    if name == "unity_list_scenes":
        return tool_result(call_unity("/list-scenes"))
    if name == "unity_list_components":
        return tool_result(call_unity("/list-components"))
    if name == "unity_list_text":
        return tool_result(call_unity("/list-text"))
    if name == "unity_list_buttons":
        return tool_result(call_unity("/list-buttons"))
    if name == "unity_console":
        return tool_result(call_unity("/console"))
    if name == "unity_clear_console":
        return tool_result(call_unity("/clear-console", method="POST", payload={}))
    if name == "unity_battle_debug":
        return tool_result(call_unity("/battle-debug"))
    if name == "unity_home_debug":
        return tool_result(call_unity("/home-debug"))
    if name == "unity_open_scene":
        return tool_result(call_unity("/open-scene", method="POST", payload={"path": arguments["path"]}))
    if name == "unity_refresh_assets":
        return tool_result(call_unity("/refresh-assets", method="POST", payload={}))
    if name == "unity_execute_menu_item":
        return tool_result(
            call_unity("/execute-menu-item", method="POST", payload={"menuPath": arguments["menuPath"]})
        )
    if name == "unity_play_mode":
        return tool_result(call_unity("/play-mode", method="POST", payload={"action": arguments.get("action", "toggle")}))
    if name == "unity_invoke_method":
        return tool_result(
            call_unity(
                "/invoke-method",
                method="POST",
                payload={"componentType": arguments["componentType"], "methodName": arguments["methodName"]},
            )
        )
    if name == "unity_simulate_idle_reward":
        return tool_result(
            call_unity(
                "/simulate-idle-reward",
                method="POST",
                payload={"minutes": arguments["minutes"]},
            )
        )

    raise RuntimeError(f"Unknown tool: {name}")


def handle_request(message):
    method = message.get("method")
    request_id = message.get("id")

    if method == "initialize":
        send_response(
            request_id,
            {
                "protocolVersion": PROTOCOL_VERSION,
                "capabilities": {"tools": {}},
                "serverInfo": {"name": "unity-mcp-bridge", "version": "0.1.0"},
            },
        )
        return

    if method == "notifications/initialized":
        return

    if method == "ping":
        send_response(request_id, {})
        return

    if method == "tools/list":
        send_response(request_id, {"tools": TOOLS})
        return

    if method == "tools/call":
        params = message.get("params", {})
        name = params.get("name")
        arguments = params.get("arguments", {})
        try:
            send_response(request_id, handle_tool_call(name, arguments))
        except Exception as exc:
            send_response(
                request_id,
                {
                    "content": [{"type": "text", "text": str(exc)}],
                    "isError": True,
                },
            )
        return

    if request_id is not None:
        send_error(request_id, -32601, f"Method not found: {method}")


def main():
    while True:
        try:
            message = read_message()
            if message is None:
                break
            handle_request(message)
        except Exception as exc:
            request_id = None
            try:
                request_id = message.get("id")  # type: ignore[name-defined]
            except Exception:
                request_id = None

            details = "".join(traceback.format_exception_only(type(exc), exc)).strip()
            if request_id is not None:
                send_error(request_id, -32000, details)
            else:
                print(details, file=sys.stderr)


if __name__ == "__main__":
    main()
