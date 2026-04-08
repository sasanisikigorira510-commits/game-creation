param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$AssetId,

    [string]$WorkspaceRoot = 'C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成',
    [string]$OutputPrefix = 'eq',
    [string]$UnitySubfolder = 'EquipmentIcons',
    [int]$OutputSize = 128,
    [int]$Padding = 10,
    [double]$BackgroundThreshold = 18.0,
    [double]$NeighborThreshold = 6.0
)

$processedDir = Join-Path $WorkspaceRoot '画像置き場\装備PNG'
$unityDir = Join-Path $WorkspaceRoot ("WitchTowerGame\Assets\Resources\" + $UnitySubfolder)
$outputName = "${OutputPrefix}_${AssetId}_icon.png"
$processedPath = Join-Path $processedDir $outputName
$unityPath = Join-Path $unityDir $outputName

if (-not (Test-Path -LiteralPath $InputPath))
{
    throw "Input file not found: $InputPath"
}

New-Item -ItemType Directory -Force -Path $processedDir | Out-Null
New-Item -ItemType Directory -Force -Path $unityDir | Out-Null

& (Join-Path $WorkspaceRoot 'tools\ExtractEquipmentIcon.ps1') `
    -InputPath $InputPath `
    -OutputPath $processedPath `
    -OutputSize $OutputSize `
    -Padding $Padding `
    -BackgroundThreshold $BackgroundThreshold `
    -NeighborThreshold $NeighborThreshold

Copy-Item -LiteralPath $processedPath -Destination $unityPath -Force

Write-Output "Processed: $processedPath"
Write-Output "Unity: $unityPath"
