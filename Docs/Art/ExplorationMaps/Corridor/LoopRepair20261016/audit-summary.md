# Background loop resource audit — 2026-10-16

Read-only audit before resource repair. Diagnostic previews only were written under this Temp directory. The resource hash baseline is `baseline-resources.json` (48 PNG and PNG.meta files).

## Consumers and decisions

| Resource family | Current consumer and placement | Audit result / decision |
| --- | --- | --- |
| `ExplorationMaps/Corridor/corridor-far.png`, `corridor-near.png` | `ExplorationFieldBackdrop.cs:52-54, 110-139`; one finite SpriteRenderer per layer, camera-relative parallax `.12` / `.35` | **Repair.** An internal splice at x=2172 copies source x=96 into the panorama, so unrelated sky/tree columns meet inside a window. The far layer is additionally transparent from x=3052 through the right end. This is an image construction defect even though the map itself is finite. The exterior layers should cover the panorama with continuous reflection; architecture should retain its authored exit layout. |
| `ExplorationMaps/Corridor/corridor-interior.png` | Same finite map; one world-fixed layer | Initial inspection kept it unchanged. Follow-up actual rendering found a second vertical splice in the floor at x=2172; locally repair that floor while retaining the walls, windows and exit. See the follow-up finding below. |
| `ExplorationMaps/Forest/forest-*.png` (4) | `ExplorationFieldBackdrop.cs:56-60`; one finite SpriteRenderer per layer; camera is clamped within authored bounds (`:86-108`) | Keep unchanged. 4096×724 layers cover the finite map. Existing x=2172 reflection has identical adjacent columns on all four planes. New entrance joins along an irregular boundary at x≈1200–1360, with no straight discontinuity found in the composite. Different left/right map endpoints are intentional: school entrance vs fence. |
| `ForestArena/forest-far-mist`, `forest-far-trees`, `forest-belt-mid`, `forest-near` | `ForestParallaxBackdrop.cs:34-38, 72-87`; pooled repeated tiles, alternate `flipX` by absolute index | Keep unchanged. Reflected neighbors share the identical source edge. Direct comparison of left/right source pixels is not the runtime joining rule. Mirrored-pair preview shows no abrupt color or alpha seam. |
| `SchoolCorridor/corridor-far`, `corridor-near`, `corridor-interior` | `SchoolCorridorBackdrop.cs:34-37, 79`; repeated battle layers with alternate `flipX` | Keep unchanged. Same reflected-edge rule as battle forest. 2172×916 exports preserve common geometry/padding. These images do not contain the finite-map splice under repair. |
| `SchoolCorridor/corridor-composite.png` | `CampaignLobbyHud.cs:526-527`; one UI stage-preview Image, 392×170 | Keep unchanged. Static preview, never repeated. |
| `LobbyRoom/{morning,day,evening,night}.png` | `TitleHud.cs:262-274` chooses one variant. `CampaignLobbyHud.cs:102,120` places one UI Image, 1920×1080; default Image type is Simple | Keep unchanged. Four fixed cabin views, no side-by-side or scrolling tiles. |
| `LobbyRoom/room.png` | Retained legacy constant `TitleHud.BackgroundResource` (`TitleHud.cs:15`); no current production image consumer was found in the audited scope | Keep unchanged. No repeat consumer. |
| `AcademyUI/title-academy.png` | `AcademyUiSkin.TitleBackground` (`AcademyUiSkin.cs:25-26`), preferred by `TitleHud.cs:54-61`; one full-screen Image with EnvelopeParent | Keep unchanged. Static title illustration, never repeated. |
| `LegacyDuel/Background/pa_background_-_school_in_game.png` | Loaded by `LegacyDuelArt.cs:49-50`; retained `DrawBackground` (`:75-79`) uses `GUI.DrawTexture(...ScaleAndCrop...)`; no current DrawBackground callers found | Keep unchanged. No repeating consumer. The legacy sprite/texture is retained but not used by the active Forest/School battle backdrop classes. |
| `ForestArena/forest-far.png` | `PrologueMissions.cs:18`; static picture through `CutsceneDirector.RunImage` and a single `CutsceneHud` Image using EnvelopeParent | Keep unchanged. Still image; no tile consumer. |
| `ForestArena/forest-mid.png` | No current production resource consumer found; old art retained | Keep unchanged. Legacy unused layer. Its old soft alpha is outside this repair scope. |

No additional repeated background family was found in current `Assets/Game/Presentation` consumers. UI decorative panels may use Sliced images, but are not panorama background resources and do not present the reported seam.

## Editable sources and build path

- Finite corridor: `Docs/Art/ExplorationMaps/Corridor/build_corridor.py` → `Authored/*.png`, `Runtime/*.png`, `Preview/*.png`; `build_master.lua` → `corridor-master.aseprite` (4096×724, 3 layers). `Package-For-Unity.ps1` copies the Runtime files into Resources without replacing their existing metas.
- Finite forest: `Docs/Art/ExplorationMaps/Forest/forest-master.aseprite` (4096×724, 4 layers), `build_forest.py`, Source/Runtime folders.
- Battle corridor: `Docs/Art/SchoolCorridor/Applied/*.aseprite` plus `Applied/Runtime/*.aseprite`; generation tools `Tools/reframe_layers.lua`, `pad_runtime_layers.lua`.
- Battle forest: `Docs/Art/DawnForest/Processed/*.aseprite`, regeneration `Tools/Build-Layer.ps1`.

## Evidence

- `report.json`: dimensions, source left/right differences, alpha sets and resource hashes. Left/right difference is diagnostic only; it does not itself prove an in-game seam.
- `SchoolCorridor_{straight,mirror}_full.png`, `ForestArena_{straight,mirror}_full.png`: source composites repeated without interpolation.
- `forest_scores.py`: finite forest adjacent-column diagnostics; x=2172 is identical across all four layers.
- `baseline-resources.json`: repair baseline and `expected_unchanged` flags. The three finite corridor PNGs are intended to change after the follow-up floor finding; all metas, forest, battle corridor, lobby and legacy background resources must remain identical.

No Unity GameView screenshot was captured by this read-only audit. Findings come from production asset pixels and their current consumer code.


## Follow-up finding: interior floor at the same splice

The rendered after-capture `Temp/BackgroundLoopValidation/Captures/after/finite-corridor_x10p0.png` exposed an additional cut in the fixed interior floor at authored x=2172. `finite-corridor-floor-splice.png` is a diagnostic crop spanning x=1960..2415, y=400..723 of the authored interior PNG. The floor's source-column splice (logical 542 to source 24) breaks sunlight and marble at a vertical line. The strongest dark-to-light jump occurs at authored y=436..467 (runtime y=596..627), RGB L1 differences 138..167. Lower marble grain is also mismatched, although less conspicuous.

This supersedes the initial decision to keep the entire interior unchanged. Keep all walls, windows, panels and the exit unchanged; a local floor-only repair near x=2172, authored y>=436 is within the user's requested seam repair. Preserve the original hash for comparison, but the interior PNG is now an intended change. Existing meta files and all other background families must remain unchanged. A nonrectangular minimum-error join using existing source pixels can retain the diagonal sunlight and avoid a new vertical blend. The exact final repair extent should be recorded by the implementation agent.
