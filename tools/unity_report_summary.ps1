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
        message = "Reports directory not found."
        reportsDir = $ReportsDir
    } | ConvertTo-Json -Depth 8
    exit 0
}

$pattern = switch ($Kind) {
    "smoke" { "unity-smoke-*.json" }
    "bridge-status" { "unity-bridge-status-*.json" }
}

$reportFile = Get-ChildItem -Path $ReportsDir -Filter $pattern -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $reportFile) {
    [ordered]@{
        ok = $false
        kind = $Kind
        message = "No matching report was found."
        reportsDir = $ReportsDir
    } | ConvertTo-Json -Depth 8
    exit 0
}

$report = Get-Content -Path $reportFile.FullName -Raw | ConvertFrom-Json
$summary = [ordered]@{
    ok = $true
    kind = $Kind
    reportPath = $reportFile.FullName
}

if ($Kind -eq "bridge-status") {
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
}
else {
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
}

$summary | ConvertTo-Json -Depth 8
