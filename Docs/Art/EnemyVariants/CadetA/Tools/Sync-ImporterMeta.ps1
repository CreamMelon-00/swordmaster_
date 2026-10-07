$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path
$sourceRoot = Join-Path $projectRoot 'Assets\Game\Resources\EnemyStudent\Animations'
$targetRoot = Join-Path $projectRoot 'Assets\Game\Resources\EnemyVariants\CadetA\Animations'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$guidLine = New-Object System.Text.RegularExpressions.Regex('(?m)^guid: [0-9a-f]{32}[ \t]*$')

$sourcePngs = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.png' -File -Recurse)
if ($sourcePngs.Count -ne 121) { throw "Expected 121 source frames, found $($sourcePngs.Count)." }
foreach ($sourcePng in $sourcePngs) {
    $relative = $sourcePng.FullName.Substring($sourceRoot.Length).TrimStart('\')
    $targetPng = Join-Path $targetRoot $relative
    if (-not (Test-Path -LiteralPath $targetPng)) { throw "Missing target frame: $relative" }
    $sourceMeta = $sourcePng.FullName + '.meta'
    $targetMeta = $targetPng + '.meta'
    $sourceContent = [System.IO.File]::ReadAllText($sourceMeta)
    $guid = if (Test-Path -LiteralPath $targetMeta) {
        $old = [System.IO.File]::ReadAllText($targetMeta)
        $match = [regex]::Match($old, '(?m)^guid: ([0-9a-f]{32})[ \t]*$')
        if ($match.Success) { $match.Groups[1].Value } else { [guid]::NewGuid().ToString('N') }
    } else { [guid]::NewGuid().ToString('N') }
    $content = $guidLine.Replace($sourceContent, ('guid: ' + $guid), 1)
    [System.IO.File]::WriteAllText($targetMeta, $content, $utf8NoBom)
}
Write-Output "Synced $($sourcePngs.Count) CadetA sprite importers."
