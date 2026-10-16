# Background loop rendering validation — 2026-10-16 to 2026-10-17

## Scope

Isolated Unity 6000.5.9f1 project; copied production `ExplorationFieldBackdrop.cs` and `SchoolCorridorBackdrop.cs` without edits. Copied the 3 finite corridor, 4 finite forest and 4 battle corridor PNG resources with existing metadata. No production code or project settings were modified by this validation project. The before source hashes were recorded before the production resource fix.

The rendering conditions were Direct3D11, NVIDIA GeForce RTX 5060 Ti, Linear color space, Built-in pipeline and `Sprites/Default`. These captures validate the actual copied sprite renderer classes and their parallax transforms, but are not a full production URP/gameplay/UI test or a player build.

## Commands

Run from `C:/Users/User/Documents/swordmaster_`, with the same approved execution context used for both successful launches:

```powershell
$taskRoot=Join-Path (Get-Location) 'Temp/BackgroundLoopValidation'
$taskProcess=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-force-d3d11','-projectPath',$taskRoot,'-executeMethod','BackgroundLoopRenderValidation.Run','-capturePhase','before','-logFile',"$taskRoot/before-unity-elevated.log") -WindowStyle Hidden -PassThru
```

After the before process had exited, `corridor-far.png` and `corridor-near.png` were replaced with the final production resource copies. The later narrow floor repair updated only `corridor-interior.png`. All metadata and other inputs remained unchanged. The original before set was preserved; the intermediate after was archived as `Captures/after-scenery-only/`. The final after was rendered once with all three corrected PNGs:

```powershell
$taskRoot=Join-Path (Get-Location) 'Temp/BackgroundLoopValidation'
$taskProcess=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-force-d3d11','-projectPath',$taskRoot,'-executeMethod','BackgroundLoopRenderValidation.Run','-capturePhase','after','-logFile',"$taskRoot/after-final-unity.log") -WindowStyle Hidden -PassThru
& 'C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' 'Temp/BackgroundLoopValidation/summarize_validation.py'
```

## Results

- Both successful launches compiled the isolated code and logged `BACKGROUND RENDER VALIDATION PASS: 15 actual D3D11 RenderTexture captures`.
- The validation harness calls `EditorApplication.Exit(0)` after each PASS. Both processes were verified terminated after normal engine cleanup. An independent operating system exit code was not retained because the launches were asynchronous.
- 15 before + 15 after captures: finite corridor and forest at camera X −24.4, −12, 0, 10, 24.4 in 16:9, plus X −13.7 in 32:9; battle corridor at X −18.6, 0, 18.6.
- All 15 final images had zero uncovered magenta camera-background pixels.
- Before finite corridor X −13.7 / 32:9 had 8,348 uncovered pixels. After has zero.
- Before finite corridor X −12 exposed the abrupt original far-sky cut in a window; the same after view was visually inspected and the cut is repaired.
- The final after X 10 and 32:9 X −13.7 captures were also visually inspected for the narrow floor repair at the former vertical cut. Both now preserve continuous floor grain/light at that boundary.
- Forest 6/6 and battle corridor 3/3 before/after captures are pixel-identical. Finite corridor X 24.4 was also unchanged (the changed scenery is hidden by the authored solid end wall there).
- Only the three expected PNG input hashes changed (`corridor-far`, `corridor-near`, `corridor-interior`); all copied metadata and other PNGs remained identical.
- No C# compiler errors, shader errors or exception entries were found in either successful run log. Each successful log contains the same three licensing-client handshake/access-token diagnostics; fallback initialization completed and actual rendering executed afterward.

## Artifacts

- `Captures/before/`, `Captures/after/`: 15 PNGs + report JSON each.
- `Captures/before-input-hashes.json`, `Captures/after-input-hashes.json`: frozen input hash records.
- `Captures/summary.json`: machine-checked final results.
- `Captures/unity-before-after.png`: window detail comparison.
- `before-unity-elevated.log`, `after-final-unity.log`: original before and final after successful Unity logs; `after-unity.log` is the intermediate scenery-only render.

The first sandbox launch could not initialize the licensing channel and was terminated only after its own process ID and project path were checked. A premature approved retry encountered that same project's lock and exited before import. Those unsuccessful startup logs are retained separately; no other Unity process was stopped or modified.
