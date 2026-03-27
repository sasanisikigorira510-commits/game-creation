param(
    [int]$TimeoutSec = 90
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BridgeStatePath = Join-Path $ScriptDir "unity_bridge_state.json"
$compileHealthScript = Join-Path $ScriptDir "unity_compile_health.ps1"

function Resolve-BaseUrl {
    if (Test-Path $BridgeStatePath) {
        try {
            $payload = Get-Content -Path $BridgeStatePath -Raw | ConvertFrom-Json
            $candidate = [string]$payload.baseUrl
            if (-not [string]::IsNullOrWhiteSpace($candidate)) {
                return $candidate.TrimEnd("/")
            }
        }
        catch {
        }
    }

    return "http://127.0.0.1:8765"
}

$baseUrl = Resolve-BaseUrl
$refresh = $null
try {
    $refresh = Invoke-RestMethod -Uri ($baseUrl + "/refresh-assets") -Method Post -ContentType "application/json" -Body "{}" -TimeoutSec 20
}
catch {
    $refresh = [ordered]@{
        ok = $false
        message = $_.Exception.Message
    }
}
$deadline = (Get-Date).AddSeconds($TimeoutSec)
$lastHealth = $null

do {
    $lastHealth = PowerShell -ExecutionPolicy Bypass -File $compileHealthScript | ConvertFrom-Json
    $editorStale = [int]$lastHealth.staleSummary.editorStaleCount
    $runtimeStale = [int]$lastHealth.staleSummary.runtimeStaleCount
    if ($editorStale -eq 0 -and $runtimeStale -eq 0) {
        break
    }

    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $deadline)

[ordered]@{
    ok = ($null -ne $lastHealth -and [int]$lastHealth.staleSummary.editorStaleCount -eq 0 -and [int]$lastHealth.staleSummary.runtimeStaleCount -eq 0)
    baseUrl = $baseUrl
    refresh = $refresh
    compileHealth = $lastHealth
} | ConvertTo-Json -Depth 8
