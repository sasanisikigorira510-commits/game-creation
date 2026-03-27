param()

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$overviewScript = Join-Path $ScriptDir "unity_status_overview.ps1"
$overview = PowerShell -ExecutionPolicy Bypass -File $overviewScript | ConvertFrom-Json

$diagnosis = $overview.diagnosis
$bridge = $overview.bridge

$result = [ordered]@{
    ok = $true
    diagnosis = $diagnosis
    recommendedAction = ""
    notes = @()
}

switch ([string]$diagnosis.code) {
    "healthy" {
        $result.recommendedAction = "No recovery action is needed."
        $result.notes += "Bridge is already reachable."
    }
    "compile_stale" {
        $result.recommendedAction = "Run a refresh/recompile and wait for stale counts to reach zero."
        $result.notes += "Recent scripts are newer than the loaded editor/runtime assemblies."
        $result.notes += "Use unity_refresh_and_wait_compile.cmd to force refresh and poll compile health."
    }
    "compile_stale_restart_recommended" {
        $result.recommendedAction = "Restart Unity, reopen the project, and wait for compile stale counts to reach zero."
        $result.notes += "Recent scripts have stayed newer than the loaded assemblies for long enough that restart is likely faster."
        $result.notes += "After restart, use unity_refresh_and_wait_compile.cmd before rerunning smoke."
    }
    "listener_stuck" {
        $result.recommendedAction = "Restart the Unity bridge or reload scripts in the editor."
        $result.notes += "Port 8765 is still owned by Unity."
        $result.notes += "CLOSE_WAIT sockets suggest the listener is not draining completed connections."
    }
    "listener_unresponsive" {
        $result.recommendedAction = "Check the editor for a blocked compile/domain reload and restart the bridge if needed."
        $result.notes += "A process is listening, but ping still fails."
    }
    "bridge_down" {
        $result.recommendedAction = "Open the project in Unity and wait for the bridge to start."
        $result.notes += "No responsive bridge was found at the resolved base URL."
    }
    default {
        $result.recommendedAction = "Inspect Unity editor state and rerun bridge diagnostics."
        $result.notes += "The bridge is unreachable, but the cause was not classified."
    }
}

if ($bridge.portOwner.ok) {
    $result.notes += ("Current owner PID: " + [string]$bridge.portOwner.pid + " (" + [string]$bridge.portOwner.processName + ")")
}

$result | ConvertTo-Json -Depth 8
