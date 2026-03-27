param(
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$bridgeStatusScript = Join-Path $ScriptDir "unity_bridge_status.ps1"
$bridge = PowerShell -ExecutionPolicy Bypass -File $bridgeStatusScript | ConvertFrom-Json
$compile = $bridge.compileStatus
$portOwner = $bridge.portOwner
$WorkspaceDir = Split-Path -Parent $ScriptDir
$EditorAssemblyPath = Join-Path $WorkspaceDir "WitchTowerGame\Library\ScriptAssemblies\Assembly-CSharp-Editor.dll"
$RuntimeAssemblyPath = Join-Path $WorkspaceDir "WitchTowerGame\Library\ScriptAssemblies\Assembly-CSharp.dll"
$WatchedFiles = @(
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Editor\UnityMcp\UnityMcpBridge.cs"); assembly = $EditorAssemblyPath; lane = "editor" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Editor\UnityMcp\UnityMcpSceneBuilder.cs"); assembly = $EditorAssemblyPath; lane = "editor" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Scripts\Battle\BattleHudController.cs"); assembly = $RuntimeAssemblyPath; lane = "runtime" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Scripts\Battle\BattleEncounterAdvisor.cs"); assembly = $RuntimeAssemblyPath; lane = "runtime" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Scripts\Battle\PlayerBattleStatsFactory.cs"); assembly = $RuntimeAssemblyPath; lane = "runtime" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Scripts\Home\HomeActionAdvisor.cs"); assembly = $RuntimeAssemblyPath; lane = "runtime" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Scripts\Home\HomePanelController.cs"); assembly = $RuntimeAssemblyPath; lane = "runtime" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Scripts\Home\EnhancePanelController.cs"); assembly = $RuntimeAssemblyPath; lane = "runtime" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Scripts\Home\EquipmentPanelController.cs"); assembly = $RuntimeAssemblyPath; lane = "runtime" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Scripts\UI\PlayerStatusView.cs"); assembly = $RuntimeAssemblyPath; lane = "runtime" },
    @{ path = (Join-Path $WorkspaceDir "WitchTowerGame\Assets\Scripts\UI\UpgradeStatusView.cs"); assembly = $RuntimeAssemblyPath; lane = "runtime" }
)

function Get-WatchedCompileStates {
    $states = @()

    foreach ($entry in $WatchedFiles) {
        $fileExists = Test-Path $entry.path
        $assemblyExists = Test-Path $entry.assembly
        $fileTime = $null
        $assemblyTime = $null
        $aheadSeconds = $null
        $stale = $false

        if ($fileExists) {
            $fileTime = (Get-Item -Path $entry.path).LastWriteTime
        }

        if ($assemblyExists) {
            $assemblyTime = (Get-Item -Path $entry.assembly).LastWriteTime
        }

        if ($null -ne $fileTime -and $null -ne $assemblyTime) {
            $aheadSeconds = [math]::Round(($fileTime - $assemblyTime).TotalSeconds, 1)
            $stale = $fileTime -gt $assemblyTime
        }

        $states += [ordered]@{
            path = $entry.path
            lane = $entry.lane
            assemblyPath = $entry.assembly
            fileExists = $fileExists
            assemblyExists = $assemblyExists
            fileLastWriteTime = $fileTime
            assemblyLastWriteTime = $assemblyTime
            fileAheadSeconds = $aheadSeconds
            stale = $stale
        }
    }

    return $states
}

$watchedStates = @(Get-WatchedCompileStates)
$editorStaleCount = @($watchedStates | Where-Object { $_.lane -eq "editor" -and $_.stale }).Count
$runtimeStaleCount = @($watchedStates | Where-Object { $_.lane -eq "runtime" -and $_.stale }).Count
$now = Get-Date
$staleFiles = @($watchedStates | Where-Object { $_.stale } | ForEach-Object {
    $staleDurationSeconds = $null
    if ($_.fileLastWriteTime -and $_.assemblyLastWriteTime) {
        $staleDurationSeconds = [math]::Round(($now - $_.fileLastWriteTime).TotalSeconds, 1)
    }

    [ordered]@{
        path = $_.path
        lane = $_.lane
        fileAheadSeconds = $_.fileAheadSeconds
        staleDurationSeconds = $staleDurationSeconds
    }
})

$result = [ordered]@{
    ok = $true
    generatedAt = (Get-Date).ToString("o")
    bridgeReachable = [bool]$bridge.ping.ok
    diagnosis = if ($editorStaleCount -gt 0 -or $runtimeStaleCount -gt 0) { "compile_stale" } else { "compile_fresh_or_unknown" }
    compileStatus = $compile
    watchedStates = $watchedStates
    staleSummary = [ordered]@{
        editorStaleCount = $editorStaleCount
        runtimeStaleCount = $runtimeStaleCount
    }
    staleFiles = $staleFiles
    portOwner = $portOwner
    resolvedBaseUrl = $bridge.resolvedBaseUrl
}

$json = $result | ConvertTo-Json -Depth 8

if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $reportDirectory = Split-Path -Parent $ReportPath
    if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
        New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    }

    Set-Content -Path $ReportPath -Value $json -Encoding UTF8
}

$json
