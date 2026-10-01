param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[a-z0-9-]+$')]
  [string]$Name,
  [string]$InputPath,
  [string]$OutputDirectory,
  [string]$AsepritePath = 'C:\Users\User\Desktop\Aseprite\Aseprite.exe',
  [ValidateRange(2, 128)]
  [int]$Colors = 64,
  [switch]$Opaque
)
$ErrorActionPreference = 'Stop'
$projectArt = Split-Path -Parent $PSScriptRoot
if (-not $InputPath) { $InputPath = Join-Path $projectArt "Candidates\$Name.png" }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectArt 'Processed' }
$InputPath = [IO.Path]::GetFullPath($InputPath)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) { throw "Candidate not found: $InputPath" }
if (-not (Test-Path -LiteralPath $AsepritePath -PathType Leaf)) { throw "Aseprite not found: $AsepritePath" }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$workRoot = Join-Path $OutputDirectory ('.build-' + [guid]::NewGuid().ToString('N'))
$userFolder = Join-Path $workRoot 'aseprite-user'
New-Item -ItemType Directory -Path $userFolder -Force | Out-Null
$scriptPath = Join-Path $PSScriptRoot 'pixelize_layer.lua'
$reportPath = Join-Path $OutputDirectory "$Name.verify.txt"
$stdoutPath = Join-Path $workRoot 'build.log'
$stderrPath = Join-Path $workRoot 'build-errors.log'
$started = [DateTime]::UtcNow
$priorUserFolder = $env:ASEPRITE_USER_FOLDER
$env:ASEPRITE_USER_FOLDER = $userFolder
try {
  $processArguments = @(
    '-b',
    '--script-param', ('input="' + $InputPath.Replace('\', '/') + '"'),
    '--script-param', ('output="' + $OutputDirectory.Replace('\', '/') + '"'),
    '--script-param', ('name=' + $Name),
    '--script-param', ('colors=' + $Colors),
    '--script-param', ('opaque=' + ([string][bool]$Opaque).ToLowerInvariant()),
    '--script', ('"' + $scriptPath.Replace('\', '/') + '"')
  )
  $process = Start-Process -FilePath $AsepritePath -ArgumentList $processArguments -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
} finally {
  if ($null -eq $priorUserFolder) { Remove-Item Env:ASEPRITE_USER_FOLDER -ErrorAction SilentlyContinue }
  else { $env:ASEPRITE_USER_FOLDER = $priorUserFolder }
}
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
  throw "Aseprite processing failed for $Name. Check $stdoutPath and $stderrPath"
}
if ((Get-Item -LiteralPath $reportPath).LastWriteTimeUtc -lt $started -or
    (Get-Content -LiteralPath $reportPath -Raw) -notmatch '^VERIFIED:') {
  throw "Aseprite did not produce a fresh verified output for $Name. Check $stdoutPath and $stderrPath"
}
foreach ($path in @((Join-Path $OutputDirectory "$Name.aseprite"), (Join-Path $OutputDirectory "$Name.png"))) {
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing output: $path" }
}
Get-Content -LiteralPath $reportPath
$resolvedWorkRoot = [IO.Path]::GetFullPath($workRoot)
if (-not $resolvedWorkRoot.StartsWith($OutputDirectory.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
    -not (Split-Path -Leaf $resolvedWorkRoot).StartsWith('.build-', [StringComparison]::Ordinal)) {
  throw "Unexpected temporary build path: $resolvedWorkRoot"
}
Remove-Item -LiteralPath $resolvedWorkRoot -Recurse -Force
