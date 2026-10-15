# Group C new skill icons

Five bitmap icons generated through the built-in image_gen tool. Existing unified icons are style references only. No runtime assets or existing unified sources are changed here.

| Role | Skill ID | Name | Frame / main silhouette |
| --- | --- | --- | --- |
| role31 | 55 | 콩트르탕 | Brass square; one broad blade with gathered breath waves |
| role32 | 56 | 두블레 | Teal hexagon; horizontal blade, two return arrows, lavender forward chevron |
| role33 | 57 | 잔걸음 | Copper diamond; one light slash with two small step marks |
| role34 | 58 | 숫돌 | Olive shield frame; broad gray whetstone and silver blade |
| role35 | 59 | 갈아내기 | Copper diamond; keen blade, one short arc and clear lower-left folded consumable card |

`Generated/role31.png` through `role35.png` are selected raw transparent generations. `role32-v1.png` retains the first version; its violet tip was lost in 48-color reduction, so `role32-refinement.json` records the targeted revision selected as `role32.png`. `role35-v1.png` retains the original small corner glyph; `role35-refinement.json` records the selected revision with a clear lower-left folded consumable card, removing the scattered lower chips that resembled footwork.

`48/` is editable logical PNG art. `Runtime192/` is exact nearest-neighbor 4× output. `contact.png` includes native 48px samples and 192px enlargement. `prompts.json`, `prompts.md`, and `role32-refinement.json` / `role35-refinement.json` preserve every prompt and source location. `manifest.json` records source/output hashes, alpha bounds and color counts.

`build_group.py` imports the established `Unified/build_icons.normalize` recipe: 48×48 logical canvas, silhouette maximum 44×44, 48 colors without dithering, binary alpha. It does not procedurally draw any creative icon content. All five icons pass size/bounds/color/alpha checks and exact 4× output equivalence.

Visual review at 48px: swords remain primary for roles31/32/33/35; the gray stone and blade remain distinct for role34. Return/step/consumable marks stay subordinate. Role32 uses the lavender chevron revision so the forward cue remains visible after palette reduction.
