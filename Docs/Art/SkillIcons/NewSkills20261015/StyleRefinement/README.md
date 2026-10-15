# Native skill icon style refinement

This folder owns the revised15 icons only. GroupA/B/C raw generations and48px candidates remain frozen, and no Unity assets are modified by this script.

The earlier generated candidates met canvas/color limits but did not match the existing game artwork: thick rounded rims, stronger colors and flat backgrounds differed from the established thin metal badges. This revision uses the actual existing48px bitmap frames rather than asking generation to approximate them.

## Canonical native templates

| Family | Template | Metal / leather |
| --- | --- | --- |
| Slash | Unified/Output/48/role10.png | Copper diamond / dark warm leather |
| Thrust | Unified/Output/48/role4.png | Blue-green hexagon / dark blue leather |
| Hit | Unified/Output/48/role5.png | Notched brass square / dark brown leather |
| Defence | Unified/Output/48/role15.png | Olive shield-shaped frame / dark green leather |

`refine_icons.py` copies the native frame RGBA, removes the old center and composites selected pixels from the new generated symbols. The quiet leather uses the three most frequent native dark colors close to the dominant leather color, avoiding the previous dotted/grainy first draft. Final symbol colors map to existing native steel/frame colors and restrained native teal/crimson accents. Existing enemy gold18–20 is not changed or copied as a new general effect.

For29/31, the effective input is the generated transparent symbol-only `CenterRefinement` PNG. The generated23/24 center-only variants proved too thin and dim at48px, so the final23 uses the actual approved native4 blade/crossguard/hilt pixels at their original thickness and brightness, with the generated two trails secondary. Final24 uses the actual approved native5 broad blade/pommel pixel clusters at their original size, adding the generated thin opposing sword/contact as a secondary. Other symbols come from the preserved Group48px candidates. The report records the source/effective-center hashes and these native weapon references.

## Frame identity mask and cleanup exceptions

`Masks/*-editable-interior.png` identifies the replaceable center. The default starts from the canonical alpha eroded by a3px square radius. Hit uses the complete inner rectangle(9,9)–(38,38) so no original hilt fragments survive. `Masks/roleN-protected-frame.png` is the exact per-output comparison mask; every protected RGBA pixel must remain equal to its canonical template.

The old Thrust weapon crosses the frame: preserving its whole alpha would keep a second hilt and tip outside the new weapon. The full two contact strips x0–11 and38–47,y18–30 are therefore restored from the same native template's clean vertical metal row16, including transparent pixels. These strips are explicitly excluded from frame identity. Slash's few ivory old-weapon contacts in the outer band are restored from neighboring native copper pixels and listed individually when present. This cleanup prioritizes an actual clean frame over preserving the old weapon's alpha protrusions.

The native23 blade and generated29's point receive a small native ivory taper, with any point drawn over the editable-center boundary recorded in `authored_blade_tip_overlay`. Those coordinates are excluded from that output's protected-frame mask. No entire replacement icon is procedurally drawn. Role30's broken secondary attack stroke is reconnected at its original angle;28's detached lower-right glint is removed.

`CleanBases/` saves the empty native frame/leather result for inspection. `report.json` records template/source/output hashes, protected opaque frame counts, zero frame mismatches, repaired contact coordinates and blade overlay exceptions. Clean-frame alpha is checked against the repaired native base, not against old weapon protrusions.

## Outputs and verification

`48/role21.png`–`role35.png` are final logical bitmap candidates; `Runtime192/` is exact nearest4× output. `contact-48-native.png` is the actual48px preview, `contact-144.png` is3× enlargement, and `before-after-family-comparison.png` shows old native reference / first generated candidate / refined candidate / actual48px.

The build verifies48px dimensions, hard alpha, at most48 colors, exact4× output and per-role protected-frame RGBA identity. Visual inspection additionally checks that the old hilt/tip and detached frame pieces are gone, the primary weapon remains clear, the small cadence/breath/card/step cues remain secondary, and the family frames match the existing game.

Run from the project root with the bundled Python:

`python Docs/Art/SkillIcons/NewSkills20261015/StyleRefinement/refine_icons.py`

`--samples` rebuilds only22/26/28/34 and `family-samples.png`; the full build writes all15 and `report.json`.
