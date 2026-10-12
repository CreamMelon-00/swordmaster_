$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$python = 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$aseprite = 'C:\Users\User\Desktop\Aseprite\Aseprite.exe'
& $python (Join-Path $scriptRoot 'build_corridor.py')
if ($LASTEXITCODE -ne 0) { throw 'Corridor image build failed.' }
$master = Join-Path $scriptRoot 'corridor-master.aseprite'
$previousMasterWrite = if (Test-Path -LiteralPath $master) { (Get-Item -LiteralPath $master).LastWriteTimeUtc } else { [datetime]::MinValue }
& $aseprite -b --script-param "root=$($scriptRoot.Replace('\', '/'))" --script (Join-Path $scriptRoot 'build_master.lua')
if ($LASTEXITCODE -ne 0) { throw 'Aseprite master build failed.' }
for ($attempt = 0; $attempt -lt 100; $attempt++) {
  if ((Test-Path -LiteralPath $master) -and (Get-Item -LiteralPath $master).LastWriteTimeUtc -gt $previousMasterWrite) { break }
  Start-Sleep -Milliseconds 100
}
if ($attempt -ge 100) { throw 'Aseprite master was not written.' }
# Aseprite may return before the final sprite bytes have been flushed.
for ($attempt = 0; $attempt -lt 100; $attempt++) {
  $before = (Get-Item -LiteralPath $master).Length
  Start-Sleep -Milliseconds 100
  $after = (Get-Item -LiteralPath $master).Length
  if ($before -gt 0 -and $before -eq $after) { break }
}
Start-Sleep -Milliseconds 500
$roundtrip = Join-Path $scriptRoot 'Preview\master-roundtrip.png'
if (Test-Path -LiteralPath $roundtrip) { Remove-Item -LiteralPath $roundtrip }
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $scriptRoot)))
Push-Location $projectRoot
try {
  & $aseprite -b 'Docs\Art\ExplorationMaps\Corridor\corridor-master.aseprite' --save-as 'Docs\Art\ExplorationMaps\Corridor\Preview\master-roundtrip.png'
} finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'Aseprite master roundtrip export failed.' }
& $python (Join-Path $scriptRoot 'verify_master.py')
if ($LASTEXITCODE -ne 0) { throw 'Aseprite master pixels differ from authored composite.' }
