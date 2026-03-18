import json
import os
import sys
import traceback
import urllib.error
import urllib.request


PROTOCOL_VERSION = "2024-11-05"
BASE_URL = os.environ.get("UNITY_MCP_BASE_URL", "http://127.0.0.1:8765").rstrip("/")


TOOLS = [
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
    url = f"{BASE_URL}{path}"
    data = None
    headers = {}

    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"

    request = urllib.request.Request(url, data=data, headers=headers, method=method)

    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            raw = response.read().decode("utf-8")
            return json.loads(raw)
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Unity bridge returned HTTP {exc.code}: {raw}")
    except urllib.error.URLError as exc:
        raise RuntimeError(
            f"Could not reach Unity bridge at {BASE_URL}. Open the Unity project and wait for compilation to finish. Details: {exc}"
        )


def tool_result(data):
    pretty = json.dumps(data, ensure_ascii=False, indent=2)
    return {"content": [{"type": "text", "text": pretty}], "structuredContent": data, "isError": not data.get("ok", True)}


def handle_tool_call(name, arguments):
    arguments = arguments or {}

    if name == "unity_ping":
        return tool_result(call_unity("/ping"))
    if name == "unity_project_info":
        return tool_result(call_unity("/project-info"))
    if name == "unity_list_scenes":
        return tool_result(call_unity("/list-scenes"))
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
