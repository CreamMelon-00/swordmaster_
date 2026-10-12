param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
)

$ErrorActionPreference = 'Stop'
$artRoot = $PSScriptRoot
$resourceRoot = Join-Path $ProjectRoot 'Assets\Game\Resources\ExplorationMaps'
$templatePath = Join-Path $ProjectRoot 'Assets\Game\Resources\SchoolCorridor\corridor-interior.png.meta'
$textureTemplate = Get-Content -LiteralPath $templatePath -Raw

function New-GuidText {
    return [guid]::NewGuid().ToString('N')
}

function Ensure-FolderMeta([string]$folderPath) {
    New-Item -ItemType Directory -Path $folderPath -Force | Out-Null
    $metaPath = "$folderPath.meta"
    if (Test-Path -LiteralPath $metaPath) { return }
    $guid = New-GuidText
    @"
fileFormatVersion: 2
guid: $guid
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@ | Set-Content -LiteralPath $metaPath -Encoding utf8NoBOM
}

function Copy-ArtLayer([string]$scene, [string]$name) {
    $source = Join-Path $artRoot "$scene\Runtime\$name.png"
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Missing finished art layer: $source"
    }
    $header = [System.IO.File]::ReadAllBytes($source)
    if ($header.Length -lt 24 -or [System.Text.Encoding]::ASCII.GetString($header, 1, 3) -ne 'PNG') {
        throw "Invalid PNG: $source"
    }
    $widthBytes = [byte[]]$header[16..19]
    $heightBytes = [byte[]]$header[20..23]
    [System.Array]::Reverse($widthBytes)
    [System.Array]::Reverse($heightBytes)
    $width = [System.BitConverter]::ToUInt32($widthBytes, 0)
    $height = [System.BitConverter]::ToUInt32($heightBytes, 0)
    $requiredHeight = if ($scene -eq 'Corridor') { 916 } else { 724 }
    if ($width -ne 4096 -or $height -ne $requiredHeight) {
        throw "Unexpected canvas size in $source`: ${width}x${height}; expected 4096x$requiredHeight"
    }
    $destinationFolder = Join-Path $resourceRoot $scene
    $destination = Join-Path $destinationFolder "$name.png"
    Copy-Item -LiteralPath $source -Destination $destination -Force
    $metaPath = "$destination.meta"
    if (-not (Test-Path -LiteralPath $metaPath)) {
        $guid = New-GuidText
        $meta = $textureTemplate -replace '(?m)^guid: [0-9a-f]{32}$', "guid: $guid"
        $meta = $meta -replace '(?m)^    spriteID: [0-9a-f]{32}$', "    spriteID: $guid"
        Set-Content -LiteralPath $metaPath -Value $meta -Encoding utf8NoBOM -NoNewline
    }
    Write-Host "$scene/$name.png"
}

Ensure-FolderMeta $resourceRoot
Ensure-FolderMeta (Join-Path $resourceRoot 'Corridor')
Ensure-FolderMeta (Join-Path $resourceRoot 'Forest')

foreach ($name in @('corridor-far', 'corridor-near', 'corridor-interior')) {
    Copy-ArtLayer 'Corridor' $name
}
foreach ($name in @('forest-far-mist', 'forest-far-trees', 'forest-belt-mid', 'forest-near')) {
    Copy-ArtLayer 'Forest' $name
}
