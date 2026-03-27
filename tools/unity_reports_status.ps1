param(
    [ValidateSet("any", "smoke", "bridge-status")]
    [string]$Kind = "any",
    [int]$Limit = 10
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
    } | ConvertTo-Json -Depth 6
    exit 0
}

$pattern = switch ($Kind) {
    "smoke" { "unity-smoke-*.json" }
    "bridge-status" { "unity-bridge-status-*.json" }
    default { "*.json" }
}

$resolvedLimit = [Math]::Min([Math]::Max($Limit, 1), 20)
$reports = Get-ChildItem -Path $ReportsDir -Filter $pattern -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First $resolvedLimit

$result = [ordered]@{
    ok = $true
    kind = $Kind
    count = @($reports).Count
    reportsDir = $ReportsDir
    reports = @(
        foreach ($report in $reports) {
            [ordered]@{
                name = $report.Name
                path = $report.FullName
                size = $report.Length
                modifiedAt = $report.LastWriteTime.ToString("o")
            }
        }
    )
}

$result | ConvertTo-Json -Depth 6
