param(
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BridgeStatePath = Join-Path $ScriptDir "unity_bridge_state.json"
$DefaultBaseUrl = "http://127.0.0.1:8765"
$WorkspaceDir = Split-Path -Parent $ScriptDir
$BridgeScriptPath = Join-Path $WorkspaceDir "WitchTowerGame\Assets\Editor\UnityMcp\UnityMcpBridge.cs"
$EditorAssemblyPath = Join-Path $WorkspaceDir "WitchTowerGame\Library\ScriptAssemblies\Assembly-CSharp-Editor.dll"

function Get-BridgeState {
    if (-not (Test-Path $BridgeStatePath)) {
        return [ordered]@{
            exists = $false
        }
    }

    try {
        $payload = Get-Content -Path $BridgeStatePath -Raw | ConvertFrom-Json
        $result = [ordered]@{
            exists = $true
        }

        foreach ($property in $payload.PSObject.Properties) {
            $result[$property.Name] = $property.Value
        }

        return $result
    }
    catch {
        return [ordered]@{
            exists = $true
            parseError = $_.Exception.Message
        }
    }
}

function Resolve-BaseUrl {
    param(
        [hashtable]$State
    )

    if ($State -and $State.Contains("baseUrl")) {
        $candidate = [string]$State["baseUrl"]
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            return $candidate.TrimEnd("/")
        }
    }

    return $DefaultBaseUrl
}

function Invoke-Ping {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl
    )

    try {
        $response = Invoke-RestMethod -Uri ($BaseUrl + "/ping") -Method Get -TimeoutSec 5
        return [ordered]@{
            ok = $true
            response = $response
        }
    }
    catch {
        return [ordered]@{
            ok = $false
            error = $_.Exception.Message
        }
    }
}

function Get-PortStatus {
    $lines = @()
    try {
        $lines = @(netstat -ano -p tcp | Select-String ':8765' | ForEach-Object { $_.ToString().Trim() })
    }
    catch {
        return [ordered]@{
            ok = $false
            error = $_.Exception.Message
            lines = @()
        }
    }

    $listening = @($lines | Where-Object { $_ -match '\sLISTENING\s' })
    $closeWait = @($lines | Where-Object { $_ -match '\sCLOSE_WAIT\s' })

    return [ordered]@{
        ok = $true
        lineCount = $lines.Count
        listeningCount = $listening.Count
        closeWaitCount = $closeWait.Count
        lines = $lines
    }
}

function Get-PortOwner {
    param(
        [hashtable]$PortStatus
    )

    if ($null -eq $PortStatus -or -not $PortStatus.ok -or $PortStatus.listeningCount -lt 1) {
        return [ordered]@{
            ok = $false
            reason = "No listening owner was found."
        }
    }

    $listeningLine = @($PortStatus.lines | Where-Object { $_ -match '\sLISTENING\s' } | Select-Object -First 1)
    if ($listeningLine.Count -lt 1) {
        return [ordered]@{
            ok = $false
            reason = "No LISTENING line was found."
        }
    }

    $match = [regex]::Match([string]$listeningLine[0], '\s+(?<pid>\d+)\s*$')
    if (-not $match.Success) {
        return [ordered]@{
            ok = $false
            reason = "Could not parse owner PID from LISTENING line."
            line = [string]$listeningLine[0]
        }
    }

    $ownerPid = [int]$match.Groups["pid"].Value

    try {
        $process = Get-Process -Id $ownerPid -ErrorAction Stop
        return [ordered]@{
            ok = $true
            pid = $ownerPid
            processName = $process.ProcessName
            responding = $process.Responding
            startTime = $process.StartTime
            mainWindowTitle = $process.MainWindowTitle
        }
    }
    catch {
        return [ordered]@{
            ok = $false
            pid = $ownerPid
            reason = $_.Exception.Message
        }
    }
}

function Get-CompileStatus {
    $bridgeScriptExists = Test-Path $BridgeScriptPath
    $editorAssemblyExists = Test-Path $EditorAssemblyPath

    $bridgeScriptTime = $null
    $editorAssemblyTime = $null
    $bridgeScriptLength = $null
    $editorAssemblyLength = $null
    $secondsBehind = $null
    $stale = $false

    if ($bridgeScriptExists) {
        $bridgeScript = Get-Item -Path $BridgeScriptPath
        $bridgeScriptTime = $bridgeScript.LastWriteTime
        $bridgeScriptLength = $bridgeScript.Length
    }

    if ($editorAssemblyExists) {
        $editorAssembly = Get-Item -Path $EditorAssemblyPath
        $editorAssemblyTime = $editorAssembly.LastWriteTime
        $editorAssemblyLength = $editorAssembly.Length
    }

    if ($null -ne $bridgeScriptTime -and $null -ne $editorAssemblyTime) {
        $secondsBehind = [math]::Round(($bridgeScriptTime - $editorAssemblyTime).TotalSeconds, 1)
        $stale = $bridgeScriptTime -gt $editorAssemblyTime
    }

    return [ordered]@{
        ok = $bridgeScriptExists -or $editorAssemblyExists
        bridgeScriptPath = $BridgeScriptPath
        bridgeScriptExists = $bridgeScriptExists
        bridgeScriptLastWriteTime = $bridgeScriptTime
        bridgeScriptLength = $bridgeScriptLength
        editorAssemblyPath = $EditorAssemblyPath
        editorAssemblyExists = $editorAssemblyExists
        editorAssemblyLastWriteTime = $editorAssemblyTime
        editorAssemblyLength = $editorAssemblyLength
        scriptAheadSeconds = $secondsBehind
        stale = $stale
    }
}

$state = Get-BridgeState
$baseUrl = Resolve-BaseUrl -State $state
$portStatus = Get-PortStatus
$result = [ordered]@{
    ok = $true
    bridgeStatePath = $BridgeStatePath
    state = $state
    resolvedBaseUrl = $baseUrl
    ping = Invoke-Ping -BaseUrl $baseUrl
    portStatus = $portStatus
    portOwner = Get-PortOwner -PortStatus $portStatus
    compileStatus = Get-CompileStatus
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
