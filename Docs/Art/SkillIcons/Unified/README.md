# Unified skill icons

This directory stages the 20 in-game skill-role icons for one visual scale. The Unity runtime files are applied separately.

## Art specification

- Each editable icon is **48 × 48 logical pixels**, with the visible shape fitting within **44 × 44 pixels** and hard alpha edges.
- The base reduction uses up to 48 colors per icon without dithering. Existing sword, shield, motion, and skill colors remain recognizable.
- `role18` and `role20` use hand-refined 48px overrides from `../GoldRefinement/`. The override PNGs must have a 48px canvas, binary alpha, and a visible shape no larger than 44px.
- Runtime candidates are **192 × 192**, produced by exact nearest-neighbor 4× scaling. The 32px set exists only to compare legibility and is not intended for the game.
- Roles 1–15 come from the atlas Sprite rectangles recorded in the preserved source `.meta`; roles 16–20 come from standalone PNGs. `Sources/` is a frozen copy of the originals and their metadata. Do not replace it with the new runtime art.

`Output/contact-48-native.png` shows all icons at their logical size. `Output/gold-display-40-48-52.png` shows roles 18 and 20 at 40, 48, and 52 displayed pixels. The latter is a useful final UI check, especially for the gold glints.

## Regenerate and check

Run from the project root in PowerShell:

`& 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' 'Docs/Art/SkillIcons/Unified/build_icons.py' --override-dir 'Docs/Art/SkillIcons/GoldRefinement'`

`& 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' 'Docs/Art/SkillIcons/Unified/validate_icons.py'`

`& 'C:\Users\User\Desktop\Aseprite\aseprite.exe' -b --script-param 'root=C:/Users/User/Documents/swordmaster_' --script 'Docs/Art/SkillIcons/Unified/build_aseprite_master.lua'`

`& 'C:\Users\User\Desktop\Aseprite\aseprite.exe' -b --script-param 'root=C:/Users/User/Documents/swordmaster_' --script 'Docs/Art/SkillIcons/Unified/validate_aseprite_master.lua'`

The Aseprite validator must print **ICON MASTER PASS**. It checks that all 20 frames in `UnifiedSkillIcons-48.aseprite` match `Output/48/role1.png` through `role20.png` pixel for pixel, with one `roleN` tag per frame. Rebuild the master whenever a 48px PNG changes.

`Output/manifest.json` records the original source hashes, old Sprite IDs, output bounds, gold override hashes, and proposed atlas coordinates. The generator always reads `Sources/`, so rerunning it after the game art changes does not repeatedly rescale already reduced icons.

## Runtime integration

Before changing game assets, run:

`& 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' 'Docs/Art/SkillIcons/Unified/apply_runtime.py' --check`

After visual approval, the same script with `--apply` copies the staged 192px atlas and roles 16–20 into `Assets/Game/Resources/SkillRoles/`. It changes **only** the x/y/width/height of the 15 atlas Sprite rectangles in `skill-role-atlas.png.meta`; the atlas GUID, spriteID, internalID, `nameFileIdTable`, filter/compression settings, and standalone `.meta` files stay intact. Preflight refuses unknown source or runtime changes, and errors during copying restore the previous runtime bytes. Re-run `--check` afterward: it should report `APPLIED`.

The 192px atlas uses a 3 × 5 arrangement with 16px gutters; exact bottom-origin Sprite rectangles are in the manifest. Inspect roles 18 and 20 inside the actual in-game button at approximately 40, 48, and 52 displayed pixels. The 48px art should preserve distinct sword and glint silhouettes; if a button uses a different scale, review that UI element rather than changing the source snapshot.

`Output/32-comparison/`, `Output/Runtime192/`, and the staged atlas PNGs are reproducible scratch outputs and may be ignored or removed after runtime application. Keep `Sources/`, the generator and validators, `Output/48/`, contact/manifest, and the Aseprite master as the editable handoff.

