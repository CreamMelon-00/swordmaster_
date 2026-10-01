param(
 [Parameter(Mandatory=$true)][string]$AsepritePath,
 [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$packageRoot=Split-Path -Parent $PSScriptRoot
if(-not(Test-Path -LiteralPath $AsepritePath -PathType Leaf)){throw 'Set -AsepritePath to the installed Aseprite executable.'}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $packageRoot 'Rebuilt'}
$destination=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $destination,(Join-Path $destination 'Frames/idle'),(Join-Path $destination 'Frames/hurt') | Out-Null
$rootArgument='root="'+$packageRoot.Replace('\','/')+'"'
$outputArgument='output="'+$destination.Replace('\','/')+'"'
$scriptArgument='"'+(Join-Path $PSScriptRoot 'build_dummy.lua').Replace('\','/')+'"'
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $AsepritePath -ArgumentList @('-b','--script-param',$rootArgument,'--script-param',$outputArgument,'--script',$scriptArgument) -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $destination 'build.log') -RedirectStandardError (Join-Path $destination 'build-errors.log')
$report=Join-Path $destination 'verification.txt'
if($process.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $report)){throw 'Aseprite rebuild failed. Check build.log and build-errors.log.'}
if((Get-Item -LiteralPath $report).LastWriteTimeUtc -lt $started -or (Get-Content -LiteralPath $report -Raw) -notmatch '^VERIFIED:'){throw 'No fresh successful verification report was produced.'}
Get-Content -LiteralPath $report
Write-Output ('Rebuilt files: '+$destination)
