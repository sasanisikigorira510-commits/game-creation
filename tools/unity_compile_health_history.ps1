param(
    [int]$Limit = 10
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ReportsDir = Join-Path $ScriptDir "reports"
$resolvedLimit = [Math]::Min([Math]::Max($Limit, 1), 20)

if (-not (Test-Path $ReportsDir)) {
    [ordered]@{
        ok = $false
        message = "Reports directory not found."
        reportsDir = $ReportsDir
    } | ConvertTo-Json -Depth 6
    exit 0
}

$reports = Get-ChildItem -Path $ReportsDir -Filter "unity-compile-health-*.json" -File |
    Where-Object { $_.Name -ne "unity-compile-health-latest.json" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First $resolvedLimit

[ordered]@{
    ok = $true
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
} | ConvertTo-Json -Depth 6
