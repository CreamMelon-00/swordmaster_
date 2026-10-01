$ErrorActionPreference='Stop'
$out='C:\Users\user\Documents\SwordMasterStory\Aseprite_Character\Training_Dummy'
$validation='C:\Users\user\Documents\SwordMasterStory\UnityIntegration\ValidationProject'
$repo='C:\Fork\swordmaster_'
if(-not(Test-Path -LiteralPath "$out\unity-verification.txt")){throw 'Unity verification report is required.'}
$source="$validation\Assets\Game\Art\TrainingDummy"
$target="$repo\Assets\Game\Art\TrainingDummy"
$art="$repo\Assets\Game\Art"
New-Item -ItemType Directory -Force -Path $art | Out-Null
if(-not(Test-Path -LiteralPath "$art.meta")){Copy-Item -LiteralPath "$validation\Assets\Game\Art.meta" -Destination "$art.meta"}
Copy-Item -LiteralPath $source -Destination $art -Recurse -Force
Copy-Item -LiteralPath "$source.meta" -Destination "$target.meta" -Force
$count=0
foreach($file in Get-ChildItem -LiteralPath $source -File -Recurse){
 $relative=$file.FullName.Substring($source.Length+1)
 if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $relative)).Hash){throw "Installed file differs: $relative"}
 $count++
}
$guids=@{}
foreach($meta in Get-ChildItem -LiteralPath $target -Filter '*.meta' -File -Recurse){
 $match=[regex]::Match([IO.File]::ReadAllText($meta.FullName),'(?m)^guid: ([a-f0-9]{32})')
 if($match.Success){$id=$match.Groups[1].Value;if($guids.ContainsKey($id)){throw 'Duplicate asset GUID'};$guids[$id]=$true}
}
# The shared unlit material is provided by this project's existing URP package.
foreach($meta in Get-ChildItem -LiteralPath "$repo\Library\PackageCache" -Recurse -Filter 'Sprite-Unlit-Default.mat.meta' -File){
 $match=[regex]::Match([IO.File]::ReadAllText($meta.FullName),'(?m)^guid: ([a-f0-9]{32})')
 if($match.Success){$guids[$match.Groups[1].Value]=$true}
}
foreach($asset in Get-ChildItem -LiteralPath $target -File | Where-Object Extension -in @('.prefab','.controller','.anim')){
 foreach($match in [regex]::Matches([IO.File]::ReadAllText($asset.FullName),'guid: ([a-f0-9]{32})')){
  $id=$match.Groups[1].Value
  if(-not $id.StartsWith('0000000000000000') -and -not $guids.ContainsKey($id)){throw ('Missing reference '+$id+' in '+$asset.Name)}
 }
}
$docs="$repo\Docs\Art\TrainingDummy"
New-Item -ItemType Directory -Force -Path $docs | Out-Null
foreach($file in Get-ChildItem -LiteralPath $out -File){
 if($file.Extension -in @('.aseprite','.png','.gif','.json','.md','.lua','.cs','.txt')){Copy-Item -LiteralPath $file.FullName -Destination $docs -Force}
}
Write-Output ('Installed and hash-verified '+$count+' Unity asset files; all custom prefab/clip GUID references resolve.')
Write-Output ('Native source: '+$docs+'\TrainingDummy.aseprite')
