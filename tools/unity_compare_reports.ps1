param(
    [ValidateSet("smoke", "bridge-status")]
    [string]$Kind
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ReportsDir = Join-Path $ScriptDir "reports"

if (-not (Test-Path $ReportsDir)) {
    [ordered]@{
        ok = $false
        kind = $Kind
        message = "Reports directory not found."
        reportsDir = $ReportsDir
    } | ConvertTo-Json -Depth 10
    exit 0
}

$pattern = switch ($Kind) {
    "smoke" { "unity-smoke-*.json" }
    "bridge-status" { "unity-bridge-status-*.json" }
}

$reportFiles = Get-ChildItem -Path $ReportsDir -Filter $pattern -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 2

if (@($reportFiles).Count -lt 2) {
    [ordered]@{
        ok = $false
        kind = $Kind
        message = "Need at least 2 reports to compare."
        reportsDir = $ReportsDir
    } | ConvertTo-Json -Depth 10
    exit 0
}

function Get-Summary {
    param(
        [string]$ReportKind,
        [string]$ReportPath
    )

    $report = Get-Content -Path $ReportPath -Raw | ConvertFrom-Json
    $summary = [ordered]@{
        ok = $true
        kind = $ReportKind
        reportPath = $ReportPath
    }

    if ($ReportKind -eq "bridge-status") {
        $summary.bridgeStateExists = [bool]$report.state.exists
        $summary.resolvedBaseUrl = $report.resolvedBaseUrl
        $summary.pingOk = [bool]$report.ping.ok
        $summary.pingError = [string]$report.ping.error
        $summary.portListeningCount = if ($null -ne $report.portStatus) { [int]$report.portStatus.listeningCount } else { 0 }
        $summary.portCloseWaitCount = if ($null -ne $report.portStatus) { [int]$report.portStatus.closeWaitCount } else { 0 }
        $summary.ownerPid = if ($null -ne $report.portOwner) { $report.portOwner.pid } else { $null }
        $summary.ownerProcessName = if ($null -ne $report.portOwner) { [string]$report.portOwner.processName } else { $null }
        $summary.ownerResponding = if ($null -ne $report.portOwner) { $report.portOwner.responding } else { $null }
        $summary.compileStale = if ($null -ne $report.compileStatus) { [bool]$report.compileStatus.stale } else { $null }
        $summary.compileScriptAheadSeconds = if ($null -ne $report.compileStatus) { $report.compileStatus.scriptAheadSeconds } else { $null }
        $summary.running = $report.state.running
        $summary.processId = $report.state.processId
        $summary.projectPath = $report.state.projectPath
        $summary.timestamp = $report.state.timestamp
        return $summary
    }

    $steps = @($report.steps)
    $stepNames = @($steps | ForEach-Object { $_.name })
    $finalInfo = $steps | Where-Object { $_.name -eq "final_info" } | Select-Object -Last 1
    $battleResult = $steps | Where-Object { $_.name -like "*battle_result*" } | Select-Object -Last 1
    $summary.scenario = $report.scenario
    $summary.reportOk = [bool]$report.ok
    $summary.generatedAt = $report.generatedAt
    $summary.error = [string]$report.error
    $summary.stepCount = $steps.Count
    $summary.hasBattleResult = $null -ne $battleResult
    $summary.finalScene = if ($null -ne $finalInfo) { [string]$finalInfo.data.activeScenePath } else { $null }
    $summary.isPlaying = if ($null -ne $finalInfo) { $finalInfo.data.isPlaying } else { $null }
    $summary.lastStep = if ($stepNames.Count -gt 0) { [string]$stepNames[-1] } else { "" }
    return $summary
}

$latest = Get-Summary -ReportKind $Kind -ReportPath $reportFiles[0].FullName
$previous = Get-Summary -ReportKind $Kind -ReportPath $reportFiles[1].FullName
$diff = [ordered]@{
    kind = $Kind
}

if ($Kind -eq "bridge-status") {
    $diff.pingChanged = ($latest.pingOk -ne $previous.pingOk)
    $diff.baseUrlChanged = ($latest.resolvedBaseUrl -ne $previous.resolvedBaseUrl)
    $diff.runningChanged = ($latest.running -ne $previous.running)
    $diff.ownerPidChanged = ($latest.ownerPid -ne $previous.ownerPid)
    $diff.closeWaitChanged = ($latest.portCloseWaitCount -ne $previous.portCloseWaitCount)
    $diff.compileStaleChanged = ($latest.compileStale -ne $previous.compileStale)
    $diff.previousPingOk = $previous.pingOk
    $diff.latestPingOk = $latest.pingOk
    $diff.previousPingError = $previous.pingError
    $diff.latestPingError = $latest.pingError
    $diff.previousOwnerPid = $previous.ownerPid
    $diff.latestOwnerPid = $latest.ownerPid
    $diff.previousCloseWaitCount = $previous.portCloseWaitCount
    $diff.latestCloseWaitCount = $latest.portCloseWaitCount
    $diff.previousCompileStale = $previous.compileStale
    $diff.latestCompileStale = $latest.compileStale
}
else {
    $diff.reportOkChanged = ($latest.reportOk -ne $previous.reportOk)
    $diff.errorChanged = ($latest.error -ne $previous.error)
    $diff.lastStepChanged = ($latest.lastStep -ne $previous.lastStep)
    $diff.previousReportOk = $previous.reportOk
    $diff.latestReportOk = $latest.reportOk
    $diff.previousError = $previous.error
    $diff.latestError = $latest.error
    $diff.previousLastStep = $previous.lastStep
    $diff.latestLastStep = $latest.lastStep
}

[ordered]@{
    ok = $true
    kind = $Kind
    latest = $latest
    previous = $previous
    diff = $diff
} | ConvertTo-Json -Depth 10
