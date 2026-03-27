param()

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$bridgeScript = Join-Path $ScriptDir "unity_bridge_status.ps1"
$compileHealthScript = Join-Path $ScriptDir "unity_compile_health.ps1"
$reportSummaryScript = Join-Path $ScriptDir "unity_report_summary.ps1"
$reportListScript = Join-Path $ScriptDir "unity_reports_status.ps1"

$bridge = PowerShell -ExecutionPolicy Bypass -File $bridgeScript | ConvertFrom-Json
$compileHealth = PowerShell -ExecutionPolicy Bypass -File $compileHealthScript | ConvertFrom-Json
$latestSmoke = PowerShell -ExecutionPolicy Bypass -File $reportSummaryScript -Kind smoke | ConvertFrom-Json
$latestBridgeReport = PowerShell -ExecutionPolicy Bypass -File $reportSummaryScript -Kind bridge-status | ConvertFrom-Json
$recentSmokeReports = PowerShell -ExecutionPolicy Bypass -File $reportListScript -Kind smoke -Limit 3 | ConvertFrom-Json
$recentBridgeReports = PowerShell -ExecutionPolicy Bypass -File $reportListScript -Kind bridge-status -Limit 3 | ConvertFrom-Json

function Get-Diagnosis {
    param(
        $Bridge,
        $CompileHealth,
        $LatestSmoke
    )

    $pingOk = [bool]$Bridge.ping.ok
    $listeningCount = if ($null -ne $Bridge.portStatus) { [int]$Bridge.portStatus.listeningCount } else { 0 }
    $closeWaitCount = if ($null -ne $Bridge.portStatus) { [int]$Bridge.portStatus.closeWaitCount } else { 0 }
    $ownerOk = if ($null -ne $Bridge.portOwner) { [bool]$Bridge.portOwner.ok } else { $false }
    $compileStale = if ($null -ne $CompileHealth -and $null -ne $CompileHealth.staleSummary) {
        ([int]$CompileHealth.staleSummary.editorStaleCount -gt 0) -or ([int]$CompileHealth.staleSummary.runtimeStaleCount -gt 0)
    } elseif ($null -ne $Bridge.compileStatus) {
        [bool]$Bridge.compileStatus.stale
    } else {
        $false
    }

    $staleFiles = @()
    $maxStaleDurationSeconds = 0
    if ($null -ne $CompileHealth.staleFiles) {
        $staleFiles = @($CompileHealth.staleFiles | Select-Object -First 3 | ForEach-Object { Split-Path -Leaf ([string]$_.path) })
        $durations = @($CompileHealth.staleFiles | Where-Object { $null -ne $_.staleDurationSeconds } | ForEach-Object { [double]$_.staleDurationSeconds })
        if ($durations.Count -gt 0) {
            $maxStaleDurationSeconds = ($durations | Measure-Object -Maximum).Maximum
        }
    }

    if ($pingOk -and $compileStale) {
        $code = if ($maxStaleDurationSeconds -ge 300) { "compile_stale_restart_recommended" } else { "compile_stale" }
        $summary = if ($code -eq "compile_stale_restart_recommended") {
            "Bridge is reachable, but runtime/editor compile has been stale long enough that a Unity restart is likely faster."
        } else {
            "Bridge is reachable, but recent editor/runtime scripts are newer than the loaded assemblies."
        }
        $likelyCause = if ($code -eq "compile_stale_restart_recommended") {
            "Unity has not recovered from the latest domain reload or compile pass."
        } else {
            "Unity has not finished recompiling the latest changes yet."
        }
        return [ordered]@{
            code = $code
            summary = $summary
            likelyCause = $likelyCause
            staleFiles = $staleFiles
            staleDurationSeconds = $maxStaleDurationSeconds
        }
    }

    if ($pingOk) {
        return [ordered]@{
            code = "healthy"
            summary = "Bridge is reachable."
            likelyCause = "No bridge connectivity issue is visible."
        }
    }

    if ($listeningCount -gt 0 -and $compileStale) {
        $code = if ($maxStaleDurationSeconds -ge 300) { "compile_stale_restart_recommended" } else { "compile_stale" }
        return [ordered]@{
            code = $code
            summary = "Unity is listening, but recent runtime/editor scripts are still newer than the loaded assemblies."
            likelyCause = if ($code -eq "compile_stale_restart_recommended") { "Unity compile/domain reload appears stuck." } else { "Unity has not recompiled recent bridge changes yet." }
            staleFiles = $staleFiles
            staleDurationSeconds = $maxStaleDurationSeconds
        }
    }

    if ($listeningCount -gt 0 -and $closeWaitCount -gt 0 -and $ownerOk) {
        return [ordered]@{
            code = "listener_stuck"
            summary = "Unity owns the port, but HTTP requests are timing out and CLOSE_WAIT sockets are accumulating."
            likelyCause = "The bridge listener is wedged or not draining connections."
        }
    }

    if ($listeningCount -gt 0) {
        return [ordered]@{
            code = "listener_unresponsive"
            summary = "A process is listening on the Unity bridge port, but ping is still failing."
            likelyCause = "The listener exists but is not responding."
        }
    }

    if (-not $pingOk -and $LatestSmoke.error) {
        return [ordered]@{
            code = "bridge_down"
            summary = "Bridge is unreachable and the latest smoke failed on connectivity."
            likelyCause = "Unity bridge is not active or not bound to the expected URL."
        }
    }

    return [ordered]@{
        code = "unknown"
        summary = "Bridge is unreachable."
        likelyCause = "No stronger diagnosis was available."
    }
}

$result = [ordered]@{
    ok = $true
    bridge = $bridge
    compileHealth = $compileHealth
    latestSmoke = $latestSmoke
    latestBridgeReport = $latestBridgeReport
    recentSmokeReports = $recentSmokeReports
    recentBridgeReports = $recentBridgeReports
    diagnosis = Get-Diagnosis -Bridge $bridge -CompileHealth $compileHealth -LatestSmoke $latestSmoke
}

$result | ConvertTo-Json -Depth 10
