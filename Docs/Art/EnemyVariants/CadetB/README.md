# Cadet B — sandy-haired trouser-uniform opponent

The approved design is `../Concepts/cadet-b-idle-concept-256x224.png` (with its editable Aseprite source). The runtime art is in `Assets/Game/Resources/EnemyVariants/CadetB/Animations`.

The exported set mirrors all 121 frames of `EnemyStudent/Animations`: 8 idle, 1 move, 4 guard/hurt poses, and 12 frames in each of the nine slash, pierce and blunt sequences. Aseprite's `build_cadet_b.lua` transfers the approved short-hair head to the source poses, removes the ribbon and long hair, rebuilds the exposed thighs and lower hem into charcoal trousers, follows the source's raised sword arm in overhead cuts, and maps all colors to the approved 80-color palette. The original pose timing, weapon endpoint and planted feet stay aligned with the existing combat animator. Idle detail changes keep the head, neck and torso continuously joined.

Each runtime PNG is 256×224 with binary alpha. Unity importer `.meta` files copy the corresponding `EnemyStudent` settings: single sprite, 40 pixels per unit, Point filtering, no mipmaps, and pivot `(0.4765625, 0.09821428571428571)`. Every new asset has a unique GUID.

`CadetB-AllAnimations.aseprite` is the single editable animation source. It contains all 121 frames on one layer and 15 tagged clips; every cel matches the runtime PNG pixel-for-pixel.

To regenerate the art, run `C:/Users/User/Desktop/Aseprite/aseprite.exe -b --script build_cadet_b.lua` from this directory, then `-b --script export_native_cadet_b.lua`, then `-b --script validate_cadet_b.lua`. Validation checks every frame's dimensions, alpha, palette limit and agreement with the editable source. `cadet-b-contact.png` presents idle, move, both guard/hurt poses and representative slash, pierce and blunt attack frames at native pixel scale.

The head transfer restores foreground sword and arm pixels beside actual steel colors. Where it severs a contact point, the export reconnects the shortest path through the source silhouette. The trouser conversion preserves opaque pixels around the waist and legs, and a cleanup pass removes the old hurt-pose hair fringe. Validation compares all 121 frames with their original poses for new detached components of any visible size, enclosed transparent pockets, and one-pixel pinholes.
