param(
    [switch]$FreshStart,
    [switch]$UnlockProgression,
    [string]$ReportDir = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BridgeStatePath = Join-Path $ScriptDir "unity_bridge_state.json"

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

$BaseUrl = Resolve-BaseUrl

if ([string]::IsNullOrWhiteSpace($ReportDir)) {
    $ReportDir = Join-Path $ScriptDir "reports"
}

function Invoke-UnityGet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [int]$TimeoutSec = 20
    )

    return Invoke-RestMethod -Uri ($BaseUrl + $Path) -Method Get -TimeoutSec $TimeoutSec
}

function Invoke-UnityPost {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [hashtable]$Payload = @{},
        [int]$TimeoutSec = 20
    )

    $body = $Payload | ConvertTo-Json
    return Invoke-RestMethod -Uri ($BaseUrl + $Path) -Method Post -ContentType "application/json" -Body $body -TimeoutSec $TimeoutSec
}

function Invoke-UnityPostWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [hashtable]$Payload = @{},
        [int]$TimeoutSec = 20,
        [int]$Attempts = 2
    )

    $lastError = $null
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            return Invoke-UnityPost -Path $Path -Payload $Payload -TimeoutSec $TimeoutSec
        }
        catch {
            $lastError = $_
            if ($attempt -lt $Attempts) {
                Start-Sleep -Seconds 2
            }
        }
    }

    throw $lastError
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Wait-ForPlayState {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Expected,
        [int]$TimeoutSec = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        $info = Invoke-UnityGet "/project-info"
        if ($info.isPlaying -eq $Expected) {
            return $info
        }

        Start-Sleep -Milliseconds 1500
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for isPlaying=$Expected"
}

function Enter-PlayModeWithRetry {
    param(
        [int]$Attempts = 2
    )

    $lastEnter = $null
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        $lastEnter = Invoke-UnityPost "/play-mode" @{ action = "enter" }

        try {
            $bridgeReady = Wait-ForBridgeReady -TimeoutSec 30
            $playInfo = Wait-ForPlayState -Expected $true -TimeoutSec 30
            return [ordered]@{
                enterPlay = $lastEnter
                bridgeReadyInPlay = $bridgeReady
                playInfo = $playInfo
                attempt = $attempt
            }
        }
        catch {
            if ($attempt -ge $Attempts) {
                throw
            }

            try {
                Invoke-UnityPost "/play-mode" @{ action = "exit" } | Out-Null
            }
            catch {
            }

            Start-Sleep -Seconds 2
        }
    }

    throw "Could not enter play mode"
}

function Wait-ForScene {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScenePath,
        [int]$TimeoutSec = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        $info = Invoke-UnityGet "/project-info"
        if ($info.activeScenePath -eq $ScenePath) {
            return $info
        }

        Start-Sleep -Milliseconds 1500
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for scene $ScenePath"
}

function Wait-ForBridgeReady {
    param(
        [int]$TimeoutSec = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        try {
            $ping = Invoke-UnityGet "/ping" -TimeoutSec 5
            if ($ping.ok -eq $true) {
                return $ping
            }
        }
        catch {
        }

        Start-Sleep -Milliseconds 1500
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for Unity bridge to become ready"
}

function Get-ActiveTextsByName {
    $texts = Invoke-UnityGet "/list-text"
    $map = @{}
    foreach ($entry in $texts.texts) {
        if ($entry.active) {
            $map[$entry.gameObjectName] = $entry
        }
    }

    return $map
}

function Get-ActiveButtonsByName {
    $buttons = Invoke-UnityGet "/list-buttons"
    $map = @{}
    foreach ($entry in $buttons.buttons) {
        if ($entry.active) {
            $map[$entry.gameObjectName] = $entry
        }
    }

    return $map
}

function Assert-ActiveButtonLabel {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Buttons,
        [Parameter(Mandatory = $true)]
        [string]$ButtonName,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedLabel
    )

    Assert-True ($Buttons.Contains($ButtonName)) ($ButtonName + " missing from active buttons")
    Assert-True ($Buttons[$ButtonName].label -eq $ExpectedLabel) ($ButtonName + " should show label '" + $ExpectedLabel + "'")
}

function Assert-ActiveButtonLabelStartsWith {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Buttons,
        [Parameter(Mandatory = $true)]
        [string]$ButtonName,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedPrefix
    )

    Assert-True ($Buttons.Contains($ButtonName)) ($ButtonName + " missing from active buttons")
    Assert-True ($Buttons[$ButtonName].label.StartsWith($ExpectedPrefix)) ($ButtonName + " should start with '" + $ExpectedPrefix + "'")
}

function Assert-ActiveTextStartsWith {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Texts,
        [Parameter(Mandatory = $true)]
        [string]$TextName,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedPrefix
    )

    Assert-True ($Texts.Contains($TextName)) ($TextName + " missing from active texts")
    Assert-True ($Texts[$TextName].text.StartsWith($ExpectedPrefix)) ($TextName + " should start with '" + $ExpectedPrefix + "'")
}

function Find-ActiveTextByPrefix {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Texts,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedPrefix
    )

    foreach ($entry in $Texts.GetEnumerator()) {
        if ($entry.Value.text.StartsWith($ExpectedPrefix)) {
            return $entry.Value
        }
    }

    return $null
}

function Get-HomeDebug {
    $payload = Invoke-UnityGet "/home-debug"
    if ($payload -is [string]) {
        return ($payload | ConvertFrom-Json)
    }

    if ($payload -is [pscustomobject] -and $payload.PSObject.Properties["value"] -and $payload.value -is [string]) {
        return ($payload.value | ConvertFrom-Json)
    }

    return $payload
}

function Get-ConsoleErrors {
    $console = Invoke-UnityGet "/console"
    $errors = @()
    foreach ($entry in $console.entries) {
        $type = [string]$entry.type
        if ($type -in @("Error", "Exception", "Assert")) {
            $errors += $entry
        }
    }

    return @{
        console = $console
        errors = $errors
    }
}

function Assert-NoConsoleErrors {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $result = Get-ConsoleErrors
    Add-Step ($Label + "_console") $result.console
    Assert-True ($result.errors.Count -eq 0) ($Label + " produced Unity console errors")
}

function Test-ActiveBattleResultText {
    param(
        [Parameter(Mandatory = $true)]
        $BattleDebug,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedText
    )

    foreach ($entry in $BattleDebug.texts) {
        if ($entry.name -eq "TitleText" -and $entry.active -and $entry.text -eq $ExpectedText) {
            return $true
        }
    }

    return $false
}

function Wait-ForBattleState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExpectedState,
        [int]$TimeoutSec = 45
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        $debug = Invoke-UnityGet "/battle-debug"
        if ($debug.ok -and $debug.flowState -eq $ExpectedState) {
            return $debug
        }

        Start-Sleep -Milliseconds 1500
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for battle state $ExpectedState"
}

function Wait-ForBattleResult {
    param(
        [int]$TimeoutSec = 60
    )

    return Wait-ForBattleState -ExpectedState "Result" -TimeoutSec $TimeoutSec
}

$report = [ordered]@{
    ok = $false
    scenario = if ($FreshStart -and $UnlockProgression) { "fresh_unlock_progression" } elseif ($FreshStart) { "fresh_start" } else { "standard" }
    generatedAt = (Get-Date).ToString("o")
    baseUrl = $BaseUrl
    steps = @()
}

function Add-Step {
    param(
        [string]$Name,
        $Data
    )

    $script:report.steps += [ordered]@{
        name = $Name
        data = $Data
    }
}

try {
    try {
        $initialInfo = Invoke-UnityGet "/project-info"
        Add-Step "initial_info" $initialInfo
        if ($initialInfo.isPlaying) {
            $cleanupStop = Invoke-UnityPost "/play-mode" @{ action = "exit" }
            Add-Step "pre_cleanup_exit_play" $cleanupStop
            Start-Sleep -Seconds 3
            $preStopped = Wait-ForPlayState -Expected $false
            Add-Step "pre_cleanup_stopped" $preStopped
        }
    }
    catch {
        Add-Step "pre_cleanup_warning" $_.Exception.Message
    }

    $ping = Invoke-UnityGet "/ping"
    Assert-True ($ping.ok -eq $true) "Ping failed"
    Add-Step "ping" $ping

    $refresh = Invoke-UnityPost "/refresh-assets"
    Add-Step "refresh" $refresh

    $bridgeReady = Wait-ForBridgeReady -TimeoutSec 30
    Add-Step "bridge_ready_after_refresh" $bridgeReady

    try {
        $clearConsole = Invoke-UnityPost "/clear-console"
        Add-Step "clear_console" $clearConsole
    }
    catch {
        Add-Step "clear_console_warning" $_.Exception.Message
    }

    $rebuild = Invoke-UnityPostWithRetry "/execute-menu-item" @{ menuPath = "Tools/MCP/Rebuild Minimal Home Scene" }
    Assert-True ($rebuild.ok -eq $true) "Home scene rebuild failed"
    Add-Step "rebuild_home" $rebuild
    $bridgeReadyAfterHomeRebuild = Wait-ForBridgeReady -TimeoutSec 30
    Add-Step "bridge_ready_after_home_rebuild" $bridgeReadyAfterHomeRebuild
    try {
        $rebuildBattle = Invoke-UnityPostWithRetry "/execute-menu-item" @{ menuPath = "Tools/MCP/Rebuild Minimal Battle Scene" }
    }
    catch {
        Start-Sleep -Seconds 2
        $rebuildBattle = Invoke-UnityPostWithRetry "/execute-menu-item" @{ menuPath = "Tools/MCP/Rebuild Minimal Battle Scene" }
    }
    Assert-True ($rebuildBattle.ok -eq $true) "Battle scene rebuild failed"
    Add-Step "rebuild_battle" $rebuildBattle
    $bridgeReadyAfterBattleRebuild = Wait-ForBridgeReady -TimeoutSec 30
    Add-Step "bridge_ready_after_battle_rebuild" $bridgeReadyAfterBattleRebuild

    if ($FreshStart) {
        $openTitle = Invoke-UnityPostWithRetry "/open-scene" @{ path = "Assets/Scenes/TitleScene.unity" }
        Assert-True ($openTitle.ok -eq $true) "Could not open TitleScene"
        Add-Step "open_title" $openTitle
        $titleReady = Wait-ForScene -ScenePath "Assets/Scenes/TitleScene.unity"
        Add-Step "title_ready" $titleReady
    }
    else {
        $openHome = Invoke-UnityPostWithRetry "/open-scene" @{ path = "Assets/Scenes/HomeScene.unity" }
        Assert-True ($openHome.ok -eq $true) "Could not open HomeScene"
        Add-Step "open_home" $openHome
        $homeReady = Wait-ForScene -ScenePath "Assets/Scenes/HomeScene.unity"
        Add-Step "home_ready" $homeReady
    }

    $playTransition = Enter-PlayModeWithRetry
    Add-Step "enter_play" $playTransition.enterPlay
    Add-Step "bridge_ready_in_play" $playTransition.bridgeReadyInPlay
    Add-Step "play_info" $playTransition.playInfo
    Add-Step "play_enter_attempt" @{ attempt = $playTransition.attempt }

    if ($FreshStart) {
        $startNew = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Core.TitleSceneController"; methodName = "StartNewGame" }
        Assert-True ($startNew.ok -eq $true) "Could not start a fresh game"
        Add-Step "start_new_game" $startNew

        $freshHome = Wait-ForScene -ScenePath "Assets/Scenes/HomeScene.unity"
        Add-Step "fresh_home" $freshHome
    }

    $homeTexts = Get-ActiveTextsByName
    $homeButtons = Get-ActiveButtonsByName
    $initialHomeDebug = Get-HomeDebug
    Add-Step "home_active_texts" $homeTexts
    Add-Step "home_active_buttons" $homeButtons
    Add-Step "home_debug_initial" $initialHomeDebug
    Assert-True $homeTexts.Contains("HomeCtaText") "HomeCtaText missing from home panel"
    Assert-True $homeTexts.Contains("ProgressText") "ProgressText missing from home panel"
    Assert-True $homeTexts.Contains("RewardForecastText") "Reward forecast text missing from home panel"
    Assert-True $homeTexts.Contains("ThreatText") "ThreatText missing from home panel"
    Assert-True $homeTexts.Contains("ConfidenceText") "ConfidenceText missing from home panel"
    Assert-True $homeTexts.Contains("LoadoutAlertText") "LoadoutAlertText missing from home panel"
    Assert-True $homeTexts.Contains("GoldRouteText") "GoldRouteText missing from home panel"
    Assert-True $homeTexts.Contains("UpgradeRouteText") "UpgradeRouteText missing from home panel"
    Assert-True $homeTexts.Contains("RewardRouteText") "RewardRouteText missing from home panel"
    Assert-True $homeTexts.Contains("PushWindowText") "PushWindowText missing from home panel"
    Assert-True $homeTexts.Contains("RoiReadText") "RoiReadText missing from home panel"
    Assert-True $homeTexts.Contains("DecisionLineText") "DecisionLineText missing from home panel"
    Assert-True $homeTexts.Contains("DecisionBadgeText") "DecisionBadgeText missing from home panel"
    Assert-True $homeTexts.Contains("CommandStackText") "CommandStackText missing from home panel"
    Assert-True $homeTexts.Contains("MomentumReadText") "MomentumReadText missing from home panel"
    Assert-True $homeTexts.Contains("RunCallText") "RunCallText missing from home panel"
    Assert-True $homeTexts.Contains("RiskBufferText") "RiskBufferText missing from home panel"
    Assert-True $homeTexts.Contains("EnemyTempoText") "EnemyTempoText missing from home panel"
    Assert-True $homeTexts.Contains("DamageRaceText") "DamageRaceText missing from home panel"
    Assert-True $homeTexts.Contains("BurstReadText") "BurstReadText missing from home panel"
    Assert-True $homeTexts.Contains("KillClockText") "KillClockText missing from home panel"
    Assert-True $homeTexts.Contains("CritWindowText") "CritWindowText missing from home panel"
    Assert-True $homeTexts.Contains("SurvivalWindowText") "SurvivalWindowText missing from home panel"
    Assert-True $homeTexts.Contains("ClockEdgeText") "ClockEdgeText missing from home panel"
    Assert-True $homeTexts.Contains("TempoVerdictText") "TempoVerdictText missing from home panel"
    Assert-True $homeTexts.Contains("PressureCallText") "PressureCallText missing from home panel"
    Assert-True $homeTexts.Contains("RewardPaceText") "RewardPaceText missing from home panel"
    Assert-True $homeTexts.Contains("HomeRewardSummaryText") "Home reward summary text missing from home panel"
    Assert-True $homeTexts.Contains("PrepAdviceText") "Prep advice text missing from home panel"
    Assert-True $homeTexts.Contains("BattlePlanText") "Battle plan text missing from home panel"
    Assert-True $homeTexts.Contains("SummaryText") "Player summary text missing from home panel"
    Assert-True $homeTexts.Contains("ActionText") "ActionText missing from home panel"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "HomeCtaText" -ExpectedPrefix "Next Step:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "ProgressText" -ExpectedPrefix "Progress:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "RewardForecastText" -ExpectedPrefix "Reward Forecast:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "ThreatText" -ExpectedPrefix "Threat Read:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "ConfidenceText" -ExpectedPrefix "Confidence:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "LoadoutAlertText" -ExpectedPrefix "Loadout Alert:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "GoldRouteText" -ExpectedPrefix "Gold Route:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "UpgradeRouteText" -ExpectedPrefix "Upgrade Route:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "RewardRouteText" -ExpectedPrefix "Reward Route:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "PushWindowText" -ExpectedPrefix "Push Window:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "RoiReadText" -ExpectedPrefix "ROI Read:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "DecisionLineText" -ExpectedPrefix "Decision Line:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "DecisionBadgeText" -ExpectedPrefix "Decision:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "CommandStackText" -ExpectedPrefix "Command Stack:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "MomentumReadText" -ExpectedPrefix "Momentum Read:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "RunCallText" -ExpectedPrefix "Run Call:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "RiskBufferText" -ExpectedPrefix "Risk Buffer:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "EnemyTempoText" -ExpectedPrefix "Enemy Tempo:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "DamageRaceText" -ExpectedPrefix "Damage Race:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "BurstReadText" -ExpectedPrefix "Burst Read:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "KillClockText" -ExpectedPrefix "Kill Clock:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "CritWindowText" -ExpectedPrefix "Crit Window:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "SurvivalWindowText" -ExpectedPrefix "Survival Window:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "ClockEdgeText" -ExpectedPrefix "Clock Edge:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "TempoVerdictText" -ExpectedPrefix "Tempo Verdict:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "PressureCallText" -ExpectedPrefix "Pressure Call:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "RewardPaceText" -ExpectedPrefix "Reward Pace:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "HomeRewardSummaryText" -ExpectedPrefix "Ready Gold:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "PrepAdviceText" -ExpectedPrefix "Prep Advice:"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "BattlePlanText" -ExpectedPrefix "Battle Plan:"
    Assert-True (($homeTexts["PrepAdviceText"].text -like "*HP *") -or ($homeTexts["PrepAdviceText"].text -like "*(HP *")) "Prep advice should include stat delta detail"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "SummaryText" -ExpectedPrefix "Run State:"
    Assert-True ($homeTexts["SummaryText"].text -like "*Priority Tab:*") "SummaryText should include priority tab guidance"
    Assert-ActiveTextStartsWith -Texts $homeTexts -TextName "ActionText" -ExpectedPrefix "Action Cue:"
    Assert-True ($initialHomeDebug.homeHeadline -eq $homeTexts["HomeCtaText"].text) "Home headline should match Home CTA text"
    Assert-True ($initialHomeDebug.runProgressText -eq $homeTexts["ProgressText"].text) "Run progress text should match home panel"
    Assert-True ($initialHomeDebug.rewardForecastText -eq $homeTexts["RewardForecastText"].text) "Reward forecast text should match home panel"
    Assert-True ($initialHomeDebug.threatReadText -eq $homeTexts["ThreatText"].text) "Threat read text should match home panel"
    Assert-True ($initialHomeDebug.runConfidenceText -eq $homeTexts["ConfidenceText"].text) "Confidence text should match home panel"
    Assert-True ($initialHomeDebug.loadoutAlertText -eq $homeTexts["LoadoutAlertText"].text) "Loadout alert text should match home panel"
    Assert-True ($initialHomeDebug.goldRouteText -eq $homeTexts["GoldRouteText"].text) "Gold route text should match home panel"
    Assert-True ($initialHomeDebug.upgradeRouteText -eq $homeTexts["UpgradeRouteText"].text) "Upgrade route text should match home panel"
    Assert-True ($initialHomeDebug.rewardRouteText -eq $homeTexts["RewardRouteText"].text) "Reward route text should match home panel"
    Assert-True ($initialHomeDebug.pushWindowText -eq $homeTexts["PushWindowText"].text) "Push window text should match home panel"
    Assert-True ($initialHomeDebug.roiReadText -eq $homeTexts["RoiReadText"].text) "ROI read text should match home panel"
    Assert-True ($initialHomeDebug.decisionLineText -eq $homeTexts["DecisionLineText"].text) "Decision line text should match home panel"
    Assert-True ($initialHomeDebug.decisionBadgeText -eq $homeTexts["DecisionBadgeText"].text) "Decision badge text should match home panel"
    Assert-True ($initialHomeDebug.commandStackText -eq $homeTexts["CommandStackText"].text) "Command stack text should match home panel"
    Assert-True ($initialHomeDebug.momentumReadText -eq $homeTexts["MomentumReadText"].text) "Momentum read text should match home panel"
    Assert-True ($initialHomeDebug.runCallText -eq $homeTexts["RunCallText"].text) "Run call text should match home panel"
    Assert-True ($initialHomeDebug.riskBufferText -eq $homeTexts["RiskBufferText"].text) "Risk buffer text should match home panel"
    Assert-True ($initialHomeDebug.enemyTempoText -eq $homeTexts["EnemyTempoText"].text) "Enemy tempo text should match home panel"
    Assert-True ($initialHomeDebug.damageRaceText -eq $homeTexts["DamageRaceText"].text) "Damage race text should match home panel"
    Assert-True ($initialHomeDebug.burstReadText -eq $homeTexts["BurstReadText"].text) "Burst read text should match home panel"
    Assert-True ($initialHomeDebug.killClockText -eq $homeTexts["KillClockText"].text) "Kill clock text should match home panel"
    Assert-True ($initialHomeDebug.critWindowText -eq $homeTexts["CritWindowText"].text) "Crit window text should match home panel"
    Assert-True ($initialHomeDebug.survivalWindowText -eq $homeTexts["SurvivalWindowText"].text) "Survival window text should match home panel"
    Assert-True ($initialHomeDebug.clockEdgeText -eq $homeTexts["ClockEdgeText"].text) "Clock edge text should match home panel"
    Assert-True ($initialHomeDebug.tempoVerdictText -eq $homeTexts["TempoVerdictText"].text) "Tempo verdict text should match home panel"
    Assert-True ($initialHomeDebug.pressureCallText -eq $homeTexts["PressureCallText"].text) "Pressure call text should match home panel"
    Assert-True ($initialHomeDebug.rewardPaceText -eq $homeTexts["RewardPaceText"].text) "Reward pace text should match home panel"
    Assert-True ($initialHomeDebug.runActionText -eq $homeTexts["ActionText"].text) "Run action text should match home panel"
    Assert-True ($initialHomeDebug.battlePlanText -eq $homeTexts["BattlePlanText"].text) "Battle plan text should match home panel"
    Assert-True $homeButtons.Contains("HomeButton") "Home nav button missing"
    Assert-True ($homeButtons["HomeButton"].backgroundColor -eq "2E7AB0FF") "Home nav button should be highlighted as active"
    if ($FreshStart) {
        Assert-True ($initialHomeDebug.enhanceBadgeCount -eq 3) "Fresh start should report three affordable enhance actions"
        Assert-True ($initialHomeDebug.missionBadgeCount -eq 1) "Fresh start should report one mission action"
        Assert-True ($initialHomeDebug.equipmentBadgeCount -eq 0) "Fresh start should report no equipment actions yet"
        Assert-True $homeTexts.Contains("EnhanceNavBadge") "Enhance badge should be visible on fresh start"
        Assert-True ($homeTexts["EnhanceNavBadge"].text -eq "3") "Enhance badge should show 3 affordable upgrades on fresh start"
        Assert-True $homeTexts.Contains("MissionNavBadge") "Mission badge should be visible on fresh start"
        Assert-True ($homeTexts["MissionNavBadge"].text -eq "1") "Mission badge should show the daily claim on fresh start"
        Assert-True (-not $homeTexts.Contains("EquipmentNavBadge")) "Equipment badge should be hidden before unlocks on fresh start"
    }

    $showEnhance = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.PanelSwitcher"; methodName = "ShowEnhance" }
    Assert-True ($showEnhance.ok -eq $true) "Could not open enhance panel"
    Add-Step "show_enhance_initial" $showEnhance
    Start-Sleep -Seconds 1

    $enhanceTexts = Get-ActiveTextsByName
    $enhanceButtons = Get-ActiveButtonsByName
    $enhanceDebug = Get-HomeDebug
    Add-Step "enhance_active_texts" $enhanceTexts
    Add-Step "enhance_active_buttons" $enhanceButtons
    Add-Step "enhance_debug" $enhanceDebug
    Assert-True $enhanceTexts.Contains("EnhanceCtaText") "EnhanceCtaText missing from enhance panel"
    Assert-True $enhanceTexts.Contains("AttackUpgradeImpactText") "Attack impact text missing from enhance panel"
    Assert-True $enhanceTexts.Contains("DefenseUpgradeImpactText") "Defense impact text missing from enhance panel"
    Assert-True $enhanceTexts.Contains("HpUpgradeImpactText") "HP impact text missing from enhance panel"
    Assert-ActiveTextStartsWith -Texts $enhanceTexts -TextName "EnhanceCtaText" -ExpectedPrefix "Upgrade Priority:"
    Assert-ActiveTextStartsWith -Texts $enhanceTexts -TextName "AttackUpgradeImpactText" -ExpectedPrefix "Impact:"
    Assert-ActiveTextStartsWith -Texts $enhanceTexts -TextName "DefenseUpgradeImpactText" -ExpectedPrefix "Impact:"
    Assert-ActiveTextStartsWith -Texts $enhanceTexts -TextName "HpUpgradeImpactText" -ExpectedPrefix "Impact:"
    Assert-True ($enhanceDebug.enhanceHeadline -eq $enhanceTexts["EnhanceCtaText"].text) "Enhance headline should match Enhance CTA text"
    Assert-True $enhanceButtons.Contains("EnhanceButton") "Enhance nav button missing"
    Assert-True ($enhanceButtons["EnhanceButton"].backgroundColor -eq "2E7AB0FF") "Enhance nav button should be highlighted as active"

    $showEquipment = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.PanelSwitcher"; methodName = "ShowEquipment" }
    Assert-True ($showEquipment.ok -eq $true) "Could not open equipment panel"
    Add-Step "show_equipment" $showEquipment
    Start-Sleep -Seconds 2

    $texts = Invoke-UnityGet "/list-text"
    $buttons = Invoke-UnityGet "/list-buttons"
    Add-Step "equipment_texts" $texts
    Add-Step "equipment_buttons" $buttons

    $activeTexts = Get-ActiveTextsByName
    $activeButtons = Get-ActiveButtonsByName
    $equipmentDebug = Get-HomeDebug

    Assert-True $activeTexts.Contains("WeaponText") "WeaponText missing from active equipment panel"
    Assert-True $activeTexts.Contains("ArmorText") "ArmorText missing from active equipment panel"
    Assert-True $activeTexts.Contains("AccessoryText") "AccessoryText missing from active equipment panel"
    Assert-True $activeTexts.Contains("EquipmentCtaText") "EquipmentCtaText missing from active equipment panel"
    Assert-True $activeTexts.Contains("EquipmentMatchupText") "Equipment matchup text missing from active equipment panel"
    Assert-True $activeTexts.Contains("EquipmentImpactText") "Equipment impact text missing from active equipment panel"
    Assert-True $activeButtons.Contains("BronzeBladeButton") "BronzeBladeButton missing from active equipment panel"
    Assert-True $activeButtons.Contains("IronSwordButton") "IronSwordButton missing from active equipment panel"
    Assert-True $activeButtons.Contains("EquipmentButton") "Equipment nav button missing"
    Assert-True ($activeButtons["EquipmentButton"].backgroundColor -eq "2E7AB0FF") "Equipment nav button should be highlighted as active"
    $equipmentSummary = Find-ActiveTextByPrefix -Texts $activeTexts -ExpectedPrefix "Battle Build:"
    Assert-True ($null -ne $equipmentSummary) "Battle Build summary missing from active equipment panel"
    Assert-ActiveTextStartsWith -Texts $activeTexts -TextName "EquipmentCtaText" -ExpectedPrefix "Loadout Focus:"
    Assert-ActiveTextStartsWith -Texts $activeTexts -TextName "EquipmentMatchupText" -ExpectedPrefix "Next Floor Read:"
    Assert-ActiveTextStartsWith -Texts $activeTexts -TextName "EquipmentImpactText" -ExpectedPrefix "Loadout Impact:"
    Assert-True ($equipmentDebug.equipmentHeadline -eq $activeTexts["EquipmentCtaText"].text) "Equipment headline should match Equipment CTA text"

    if ($FreshStart) {
        Assert-True ($activeTexts["WeaponText"].text -eq "Weapon: Bronze Blade") "Fresh start weapon should be Bronze Blade"
        Assert-True ($activeTexts["ArmorText"].text -eq "Armor: Guard Cloth") "Fresh start armor should be Guard Cloth"
        Assert-True ($activeTexts["AccessoryText"].text -eq "Accessory: Ashen Ring") "Fresh start accessory should be Ashen Ring"
        Assert-True ($activeButtons["BronzeBladeButton"].interactable -eq $true) "Starter weapon should be interactable"
        Assert-True ($activeButtons["GuardClothButton"].interactable -eq $true) "Starter armor should be interactable"
        Assert-True ($activeButtons["AshenRingButton"].interactable -eq $true) "Starter accessory should be interactable"
        Assert-True ($activeButtons["IronSwordButton"].interactable -eq $false) "Iron Sword should be locked on fresh start"
        Assert-True ($activeButtons["BoneMailButton"].interactable -eq $false) "Bone Mail should be locked on fresh start"
        Assert-True ($activeButtons["QuickCharmButton"].interactable -eq $false) "Quick Charm should be locked on fresh start"
        Assert-True ($activeTexts["IronSwordStatusText"].text -eq "Unlock at Floor 2") "Fresh start should show Iron Sword unlock condition"
        Assert-True ($activeTexts["BoneMailStatusText"].text -eq "Unlock at Floor 4") "Fresh start should show Bone Mail unlock condition"
        Assert-True ($activeTexts["QuickCharmStatusText"].text -eq "Unlock at Floor 6") "Fresh start should show Quick Charm unlock condition"

        $freshHomeDebug = Get-HomeDebug
        Add-Step "fresh_home_debug" $freshHomeDebug
        Assert-True ($freshHomeDebug.gold -eq 100) "Fresh start gold should be 100"
        Assert-True ($freshHomeDebug.highestFloor -eq 1) "Fresh start highest floor should be 1"

        $showMission = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.PanelSwitcher"; methodName = "ShowMission" }
        Assert-True ($showMission.ok -eq $true) "Could not open mission panel"
        Add-Step "show_mission" $showMission
        Start-Sleep -Seconds 1

    $missionTexts = Get-ActiveTextsByName
    $missionButtons = Get-ActiveButtonsByName
    $missionDebug = Get-HomeDebug
    Add-Step "mission_active_texts" $missionTexts
    Add-Step "mission_active_buttons" $missionButtons
    Add-Step "mission_debug" $missionDebug
    Assert-True $missionTexts.Contains("MissionCtaText") "MissionCtaText missing from mission panel"
    Assert-True $missionTexts.Contains("MissionRewardSummaryText") "Mission reward summary text missing from mission panel"
    Assert-ActiveTextStartsWith -Texts $missionTexts -TextName "MissionCtaText" -ExpectedPrefix "Mission Focus:"
    Assert-ActiveTextStartsWith -Texts $missionTexts -TextName "MissionRewardSummaryText" -ExpectedPrefix "Claimable Rewards:"
    Assert-True ($missionDebug.missionHeadline -eq $missionTexts["MissionCtaText"].text) "Mission headline should match Mission CTA text"
        Assert-True $missionButtons.Contains("MissionButton") "Mission nav button missing"
        Assert-True ($missionButtons["MissionButton"].backgroundColor -eq "2E7AB0FF") "Mission nav button should be highlighted as active"

        $beforeDaily = Get-HomeDebug
        Add-Step "before_daily_debug" $beforeDaily
        $claimDaily = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.MissionPanelController"; methodName = "ClaimDailyReward" }
        Assert-True ($claimDaily.ok -eq $true) "Could not claim daily reward"
        Add-Step "claim_daily" $claimDaily
        Start-Sleep -Seconds 1

        $afterDaily = Get-HomeDebug
        Add-Step "after_daily_debug" $afterDaily
        Assert-True ($afterDaily.gold -eq ($beforeDaily.gold + 50)) "Daily reward should add 50 gold"
        $missionTextsAfterDaily = Get-ActiveTextsByName
        Add-Step "mission_texts_after_daily" $missionTextsAfterDaily
        Assert-True (-not $missionTextsAfterDaily.Contains("MissionNavBadge")) "Mission badge should clear after daily claim when no mission reward is ready"

        $simulateIdle = Invoke-UnityPost "/simulate-idle-reward" @{ minutes = 60 }
        Assert-True ($simulateIdle.ok -eq $true) "Could not simulate idle reward"
        Add-Step "simulate_idle_reward" $simulateIdle

        $showHome = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.PanelSwitcher"; methodName = "ShowHome" }
        Assert-True ($showHome.ok -eq $true) "Could not open home panel"
        Add-Step "show_home" $showHome
        Start-Sleep -Seconds 1

        $beforeIdle = Get-HomeDebug
        Add-Step "before_idle_claim_debug" $beforeIdle
        Assert-True ($beforeIdle.pendingIdleRewardGold -ge 120) "Simulated idle reward should create pending gold"

        $claimIdle = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.HomePanelController"; methodName = "ClaimIdleReward" }
        Assert-True ($claimIdle.ok -eq $true) "Could not claim idle reward"
        Add-Step "claim_idle_reward" $claimIdle
        Start-Sleep -Seconds 1

        $afterIdle = Get-HomeDebug
        Add-Step "after_idle_claim_debug" $afterIdle
        Assert-True ($afterIdle.pendingIdleRewardGold -eq 0) "Idle reward should be cleared after claim"
        Assert-True ($afterIdle.gold -ge ($afterDaily.gold + 120)) "Idle reward claim should increase gold"
        $homeTextsAfterIdle = Get-ActiveTextsByName
        Add-Step "home_texts_after_idle" $homeTextsAfterIdle
        Assert-True (-not $homeTextsAfterIdle.Contains("HomeNavBadge")) "Home badge should clear after idle reward claim"
        Assert-NoConsoleErrors "fresh_home_rewards"
    }

    if ($UnlockProgression) {
        Assert-True $FreshStart "Unlock progression check requires -FreshStart"

        $startBattle = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.HomeSceneController"; methodName = "StartBattle" }
        Assert-True ($startBattle.ok -eq $true) "Could not start battle"
        Add-Step "start_battle" $startBattle

        $battleScene = Wait-ForScene -ScenePath "Assets/Scenes/BattleScene.unity"
        Add-Step "battle_scene" $battleScene

        for ($floor = 1; $floor -le 6; $floor++) {
            $result = Wait-ForBattleResult -TimeoutSec 60
            Add-Step ("battle_result_floor_" + $floor) $result

            if ($floor -lt 6) {
                $nextFloor = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Battle.BattleSceneController"; methodName = "GoToNextFloor" }
                Assert-True ($nextFloor.ok -eq $true) ("Could not advance after floor " + $floor)
                Add-Step ("next_floor_after_" + $floor) $nextFloor
            }
            else {
                $returnHome = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Battle.BattleSceneController"; methodName = "ReturnHome" }
                Assert-True ($returnHome.ok -eq $true) "Could not return home after floor 6"
                Add-Step "return_home_after_floor_6" $returnHome
            }
        }

        $homeAfterProgress = Wait-ForScene -ScenePath "Assets/Scenes/HomeScene.unity"
        Add-Step "home_after_progress" $homeAfterProgress

        $showMissionAfterProgress = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.PanelSwitcher"; methodName = "ShowMission" }
        Assert-True ($showMissionAfterProgress.ok -eq $true) "Could not open mission panel after progression"
        Add-Step "show_mission_after_progress" $showMissionAfterProgress
        Start-Sleep -Seconds 1

        $beforeMissionClaims = Get-HomeDebug
        Add-Step "before_mission_claims_debug" $beforeMissionClaims

        $claimMission1 = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.MissionPanelController"; methodName = "ClaimMissionClear1" }
        $claimMission2 = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.MissionPanelController"; methodName = "ClaimMissionReachFloor3" }
        Add-Step "claim_mission_1" $claimMission1
        Add-Step "claim_mission_2" $claimMission2
        Start-Sleep -Seconds 1

        $afterMissionClaims = Get-HomeDebug
        Add-Step "after_mission_claims_debug" $afterMissionClaims
        Assert-True ($afterMissionClaims.gold -eq ($beforeMissionClaims.gold + 90)) "Mission claims should add 90 gold in total"

        $mission1 = $null
        $mission2 = $null
        foreach ($mission in $afterMissionClaims.missions) {
            if ($mission.missionId -eq "mission_clear_1") { $mission1 = $mission }
            if ($mission.missionId -eq "mission_reach_floor_3") { $mission2 = $mission }
        }
        Assert-True ($mission1 -ne $null -and $mission1.isClaimed -eq $true) "mission_clear_1 should be claimed"
        Assert-True ($mission2 -ne $null -and $mission2.isClaimed -eq $true) "mission_reach_floor_3 should be claimed"

        $showEnhance = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.PanelSwitcher"; methodName = "ShowEnhance" }
        Assert-True ($showEnhance.ok -eq $true) "Could not open enhance panel"
        Add-Step "show_enhance" $showEnhance
        Start-Sleep -Seconds 1

        $beforeUpgrade = Get-HomeDebug
        Add-Step "before_upgrade_debug" $beforeUpgrade
        $upgradeAttack = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.EnhancePanelController"; methodName = "UpgradeAttack" }
        Add-Step "upgrade_attack" $upgradeAttack
        Start-Sleep -Seconds 1

        $afterUpgrade = Get-HomeDebug
        Add-Step "after_upgrade_debug" $afterUpgrade
        Assert-True ($afterUpgrade.attackUpgradeLevel -eq ($beforeUpgrade.attackUpgradeLevel + 1)) "Attack upgrade level should increase by 1"
        Assert-True ($afterUpgrade.gold -eq ($beforeUpgrade.gold - 10)) "First attack upgrade should cost 10 gold"

        $showEquipmentAgain = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.PanelSwitcher"; methodName = "ShowEquipment" }
        Assert-True ($showEquipmentAgain.ok -eq $true) "Could not re-open equipment panel after unlock progression"
        Add-Step "show_equipment_after_progress" $showEquipmentAgain
        Start-Sleep -Seconds 2

        $unlockTexts = Get-ActiveTextsByName
        $unlockButtons = Get-ActiveButtonsByName
        Add-Step "unlock_progression_texts" $unlockTexts
        Add-Step "unlock_progression_buttons" $unlockButtons

        Assert-True $unlockTexts.Contains("EquipmentNavBadge") "Equipment badge should appear after unlock progression"
        Assert-True ($unlockTexts["EquipmentNavBadge"].text -eq "3") "Equipment badge should show three new gear upgrades after unlock progression"
        Assert-True (-not $unlockTexts.Contains("MissionNavBadge")) "Mission badge should clear after claimable missions are collected"
        Assert-True ($unlockButtons["IronSwordButton"].interactable -eq $true) "Iron Sword should be unlocked by floor 2"
        Assert-True ($unlockButtons["BoneMailButton"].interactable -eq $true) "Bone Mail should be unlocked by floor 4"
        Assert-True ($unlockButtons["QuickCharmButton"].interactable -eq $true) "Quick Charm should be unlocked by floor 6"
        Assert-True ($unlockTexts["IronSwordStatusText"].text -eq "Owned") "Iron Sword should be owned after unlock progression"
        Assert-True ($unlockTexts["BoneMailStatusText"].text -eq "Owned") "Bone Mail should be owned after unlock progression"
        Assert-True ($unlockTexts["QuickCharmStatusText"].text -eq "Owned") "Quick Charm should be owned after unlock progression"

        $equipIron = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.EquipmentPanelController"; methodName = "EquipIronSword" }
        $equipBone = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.EquipmentPanelController"; methodName = "EquipBoneMail" }
        $equipQuick = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.EquipmentPanelController"; methodName = "EquipQuickCharm" }
        Add-Step "equip_iron" $equipIron
        Add-Step "equip_bone" $equipBone
        Add-Step "equip_quick" $equipQuick
        Start-Sleep -Seconds 2

        $equippedTexts = Get-ActiveTextsByName
        $equippedButtons = Get-ActiveButtonsByName
        Add-Step "equipped_after_progress_texts" $equippedTexts
        Add-Step "equipped_after_progress_buttons" $equippedButtons

        Assert-True (-not $equippedTexts.Contains("EquipmentNavBadge")) "Equipment badge should clear after equipping all unlocked upgrades"
        Assert-True ($equippedTexts["WeaponText"].text -eq "Weapon: Iron Sword") "Iron Sword should be equipped after explicit equip"
        Assert-True ($equippedTexts["ArmorText"].text -eq "Armor: Bone Mail") "Bone Mail should be equipped after explicit equip"
        Assert-True ($equippedTexts["AccessoryText"].text -eq "Accessory: Quick Charm") "Quick Charm should be equipped after explicit equip"
        Assert-True ($equippedTexts["IronSwordStatusText"].text -eq "Equipped") "Iron Sword should show Equipped after explicit equip"
        Assert-True ($equippedTexts["BoneMailStatusText"].text -eq "Equipped") "Bone Mail should show Equipped after explicit equip"
        Assert-True ($equippedTexts["QuickCharmStatusText"].text -eq "Equipped") "Quick Charm should show Equipped after explicit equip"

        $startBattleAfterUnlocks = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.HomeSceneController"; methodName = "StartBattle" }
        Assert-True ($startBattleAfterUnlocks.ok -eq $true) "Could not start battle after equipping unlocked gear"
        Add-Step "start_battle_after_unlocks" $startBattleAfterUnlocks

        $battleSceneAfterUnlocks = Wait-ForScene -ScenePath "Assets/Scenes/BattleScene.unity"
        Add-Step "battle_scene_after_unlocks" $battleSceneAfterUnlocks

        $battleDebug = Wait-ForBattleState -ExpectedState "Fighting"
        Add-Step "battle_fighting_after_unlocks" $battleDebug
        Assert-True ($battleDebug.simulatorRunning -eq $true) "Battle simulator did not start after unlock progression"
        Assert-True ($battleDebug.playerStats.attack -ge 26) "Unlocked loadout plus enhance should raise attack"
        Assert-True ($battleDebug.playerStats.defense -ge 11) "Unlocked loadout should raise defense"
        Assert-True ($battleDebug.playerStats.maxHp -ge 124) "Unlocked loadout should raise max HP"

        $finalResult = Wait-ForBattleResult -TimeoutSec 60
        Add-Step "battle_result_after_unlocks" $finalResult
        Assert-True (Test-ActiveBattleResultText -BattleDebug $finalResult -ExpectedText "Victory") "Final battle should reach Victory result"
        Assert-NoConsoleErrors "unlock_progression"
    }
    else {
        $startBattle = Invoke-UnityPost "/invoke-method" @{ componentType = "WitchTower.Home.HomeSceneController"; methodName = "StartBattle" }
        Assert-True ($startBattle.ok -eq $true) "Could not start battle"
        Add-Step "start_battle" $startBattle

        $battleScene = Wait-ForScene -ScenePath "Assets/Scenes/BattleScene.unity"
        Add-Step "battle_scene" $battleScene

        $battleDebug = Wait-ForBattleState -ExpectedState "Fighting"
        Assert-True ($battleDebug.simulatorRunning -eq $true) "Battle simulator did not start"
        Add-Step "battle_fighting" $battleDebug

        $battleResult = Wait-ForBattleResult -TimeoutSec 60
        Add-Step "battle_result" $battleResult
        Assert-True (Test-ActiveBattleResultText -BattleDebug $battleResult -ExpectedText "Victory") "Battle should reach Victory result"
        $resultTexts = Get-ActiveTextsByName
        Add-Step "battle_result_texts" $resultTexts
        Assert-ActiveTextStartsWith -Texts $resultTexts -TextName "ResultSummaryText" -ExpectedPrefix "Floor "
        Assert-ActiveTextStartsWith -Texts $resultTexts -TextName "NextRewardForecastText" -ExpectedPrefix "Next Reward Forecast:"
        Assert-ActiveTextStartsWith -Texts $resultTexts -TextName "NextActionText" -ExpectedPrefix "Next Floor "
        $resultButtons = Get-ActiveButtonsByName
        Add-Step "battle_result_buttons" $resultButtons
        Assert-ActiveButtonLabelStartsWith -Buttons $resultButtons -ButtonName "NextFloorButton" -ExpectedPrefix "Next Floor"
        Assert-ActiveButtonLabel -Buttons $resultButtons -ButtonName "ReturnHomeButton" -ExpectedLabel "Return Home"
        Assert-NoConsoleErrors "standard_smoke"
    }

    $exitPlay = Invoke-UnityPost "/play-mode" @{ action = "exit" }
    Add-Step "exit_play" $exitPlay

    $stopped = Wait-ForPlayState -Expected $false
    Add-Step "stopped" $stopped

    $openBoot = Invoke-UnityPost "/open-scene" @{ path = "Assets/Scenes/BootScene.unity" }
    Assert-True ($openBoot.ok -eq $true) "Could not open BootScene"
    Add-Step "open_boot" $openBoot

    $finalInfo = Invoke-UnityGet "/project-info"
    Add-Step "final_info" $finalInfo

    $report.ok = $true
}
catch {
    $report.error = $_.Exception.Message
}
finally {
    try {
        $finalCheck = Invoke-UnityGet "/project-info"
        if ($finalCheck.isPlaying) {
            $cleanupExit = Invoke-UnityPost "/play-mode" @{ action = "exit" }
            Add-Step "finally_exit_play" $cleanupExit
            Start-Sleep -Seconds 3
            $cleanupStopped = Wait-ForPlayState -Expected $false
            Add-Step "finally_stopped" $cleanupStopped
        }

        $cleanupBoot = Invoke-UnityPost "/open-scene" @{ path = "Assets/Scenes/BootScene.unity" }
        Add-Step "finally_open_boot" $cleanupBoot
    }
    catch {
        Add-Step "finally_warning" $_.Exception.Message
    }

    try {
        $bridgeStatusReportPath = Join-Path $ReportDir ("unity-bridge-status-" + $report.scenario + "-latest.json")
        $bridgeStatusScript = Join-Path $ScriptDir "unity_bridge_status.ps1"
        $bridgeStatusJson = PowerShell -ExecutionPolicy Bypass -File $bridgeStatusScript -ReportPath $bridgeStatusReportPath
        if (-not [string]::IsNullOrWhiteSpace($bridgeStatusJson)) {
            $bridgeStatus = $bridgeStatusJson | ConvertFrom-Json
            Add-Step "bridge_status" $bridgeStatus
            $report.bridgeStatusPath = $bridgeStatusReportPath
        }
    }
    catch {
        Add-Step "bridge_status_warning" $_.Exception.Message
    }
}

$reportJson = $report | ConvertTo-Json -Depth 8

try {
    New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $latestPath = Join-Path $ReportDir ("unity-smoke-" + $report.scenario + "-latest.json")
    $historyPath = Join-Path $ReportDir ("unity-smoke-" + $report.scenario + "-" + $timestamp + ".json")
    Set-Content -Path $latestPath -Value $reportJson -Encoding UTF8
    Set-Content -Path $historyPath -Value $reportJson -Encoding UTF8
    $report.reportPath = $latestPath
    $report.historyPath = $historyPath
    $reportJson = $report | ConvertTo-Json -Depth 8
}
catch {
    $report.reportWriteWarning = $_.Exception.Message
    $reportJson = $report | ConvertTo-Json -Depth 8
}

$reportJson
if (-not $report.ok) {
    exit 1
}
