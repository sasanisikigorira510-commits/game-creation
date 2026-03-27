param()

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$overviewScript = Join-Path $ScriptDir "unity_status_overview.ps1"
$overview = PowerShell -ExecutionPolicy Bypass -File $overviewScript | ConvertFrom-Json

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("Unity Status Brief")
$lines.Add("Bridge: " + ($(if ($overview.bridge.ping.ok) { "reachable" } else { "unreachable" })))
$lines.Add("Base URL: " + [string]$overview.bridge.resolvedBaseUrl)
$lines.Add(
    "Port 8765: listening=" + [string]$overview.bridge.portStatus.listeningCount +
    ", close_wait=" + [string]$overview.bridge.portStatus.closeWaitCount
)

if ($overview.bridge.portOwner.ok) {
    $lines.Add(
        "Port Owner: pid=" + [string]$overview.bridge.portOwner.pid +
        ", process=" + [string]$overview.bridge.portOwner.processName +
        ", responding=" + [string]$overview.bridge.portOwner.responding
    )
}

if ($overview.bridge.compileStatus.ok) {
    $lines.Add(
        "Compile: stale=" + [string]$overview.bridge.compileStatus.stale +
        ", script_ahead_seconds=" + [string]$overview.bridge.compileStatus.scriptAheadSeconds
    )
}

if ($overview.bridge.ping.ok) {
    try {
        $lines.Add(
            "Compile Watch: editor_stale=" + [string]$overview.compileHealth.staleSummary.editorStaleCount +
            ", runtime_stale=" + [string]$overview.compileHealth.staleSummary.runtimeStaleCount
        )
    }
    catch {
    }
}

if ($overview.diagnosis) {
    $lines.Add("Diagnosis: " + [string]$overview.diagnosis.code)
    $lines.Add("Diagnosis Summary: " + [string]$overview.diagnosis.summary)
    if ($overview.diagnosis.staleFiles -and @($overview.diagnosis.staleFiles).Count -gt 0) {
        $lines.Add("Stale Files: " + ((@($overview.diagnosis.staleFiles)) -join ", "))
    }
}

if (-not $overview.bridge.ping.ok) {
    $lines.Add("Bridge Error: " + [string]$overview.bridge.ping.error)
}

$lines.Add("Latest Smoke: " + ($(if ($overview.latestSmoke.reportOk) { "ok" } else { "failed" })))
$lines.Add("Smoke Error: " + [string]$overview.latestSmoke.error)
$lines.Add("Smoke Last Step: " + [string]$overview.latestSmoke.lastStep)
$lines.Add("Latest Bridge Report: " + [string]$overview.latestBridgeReport.reportPath)
$lines.Add("Recent Smoke Reports: " + [string]$overview.recentSmokeReports.count)
$lines.Add("Recent Bridge Reports: " + [string]$overview.recentBridgeReports.count)

$lines -join [Environment]::NewLine
