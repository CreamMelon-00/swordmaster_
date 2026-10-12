$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$python = 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$aseprite = 'C:\Users\User\Desktop\Aseprite\Aseprite.exe'
& $python (Join-Path $scriptRoot 'build_forest.py')
if ($LASTEXITCODE -ne 0) { throw "Forest image build failed: $LASTEXITCODE" }
$rootArgument = "root=$($scriptRoot.Replace('\', '/'))"
function Wait-ForAsepriteFiles([string[]]$paths) {
  for ($attempt = 0; $attempt -lt 100; $attempt++) {
    $sizes = @($paths | ForEach-Object {
      if (Test-Path -LiteralPath $_) { (Get-Item -LiteralPath $_).Length } else { 0 }
    })
    if (@($sizes | Where-Object { $_ -le 0 }).Count -eq 0) {
      Start-Sleep -Milliseconds 250
      $stable = $true
      for ($i = 0; $i -lt $paths.Count; $i++) {
        if ((Get-Item -LiteralPath $paths[$i]).Length -ne $sizes[$i]) { $stable = $false }
      }
      if ($stable) { return }
    }
    Start-Sleep -Milliseconds 100
  }
  throw "Aseprite did not finish writing: $($paths -join ', ')"
}
$master = Join-Path $scriptRoot 'forest-master.aseprite'
if (Test-Path -LiteralPath $master) { Remove-Item -LiteralPath $master }
& $aseprite -b --script-param $rootArgument --script (Join-Path $scriptRoot 'build_master.lua')
if ($LASTEXITCODE -ne 0) { throw "Forest Aseprite master build failed: $LASTEXITCODE" }
Wait-ForAsepriteFiles @($master)
$names = @('forest-far-mist', 'forest-far-trees', 'forest-belt-mid', 'forest-near')
foreach ($name in $names) {
  $check = Join-Path $scriptRoot "Preview/aseprite-check-$name.png"
  if (Test-Path -LiteralPath $check) { Remove-Item -LiteralPath $check }
}
& $aseprite -b --script-param $rootArgument --script (Join-Path $scriptRoot 'verify_master.lua')
if ($LASTEXITCODE -ne 0) { throw "Forest Aseprite master verification failed: $LASTEXITCODE" }
Wait-ForAsepriteFiles @($names | ForEach-Object { Join-Path $scriptRoot "Preview/aseprite-check-$_.png" })
& $python -c "from PIL import Image; from pathlib import Path; p=Path(r'$scriptRoot'); names=['forest-far-mist','forest-far-trees','forest-belt-mid','forest-near']; [(lambda a,b,n: (_ for _ in ()).throw(AssertionError(n)) if a.tobytes()!=b.tobytes() else None)(Image.open(p/'Source'/f'{n}.png').convert('RGBA'),Image.open(p/'Preview'/f'aseprite-check-{n}.png').convert('RGBA'),n) for n in names]; [(p/'Preview'/f'aseprite-check-{n}.png').unlink() for n in names]"
if ($LASTEXITCODE -ne 0) { throw "Forest Aseprite cel export differs from source PNG: $LASTEXITCODE" }
