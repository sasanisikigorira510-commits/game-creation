param()

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ReportsDir = Join-Path $ScriptDir "reports"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$latestPath = Join-Path $ReportsDir "unity-compile-health-latest.json"
$historyPath = Join-Path $ReportsDir ("unity-compile-health-" + $timestamp + ".json")
$healthScript = Join-Path $ScriptDir "unity_compile_health.ps1"

New-Item -ItemType Directory -Force -Path $ReportsDir | Out-Null
$payload = PowerShell -ExecutionPolicy Bypass -File $healthScript | ConvertFrom-Json
$json = $payload | ConvertTo-Json -Depth 8

Set-Content -Path $latestPath -Value $json -Encoding UTF8
Set-Content -Path $historyPath -Value $json -Encoding UTF8

[ordered]@{
    ok = $true
    latestPath = $latestPath
    historyPath = $historyPath
    payload = $payload
} | ConvertTo-Json -Depth 8
