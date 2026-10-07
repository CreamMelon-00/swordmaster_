# Cadet A — short charcoal-bob opponent

The approved design is `../Concepts/cadet-a-idle-concept-256x224.png` (and its editable Aseprite source). Runtime frames are in `Assets/Game/Resources/EnemyVariants/CadetA/Animations`.

`Tools/build_cadet_a.lua` produces all 121 frames from the existing combat pose library. The approved head replaces the original long hair and ribbon in every motion. A short bob, narrow brass pin, slate scarf and pewter details distinguish this student from Iia; the sword arm is preserved where it crosses the head during upper cuts. Idle uses the approved full-body concept with a subtle jacket/scarf shading rhythm that keeps the silhouette connected. The library contains 8 idle frames, 1 move, 4 guard/hurt poses and nine 12-frame slash/pierce/blunt sequences.

`CadetA-AllAnimations.aseprite` is the single editable 121-frame source, with 15 tagged clips. The runtime PNGs and the Aseprite frames match pixel-for-pixel. Unity importer `.meta` files use the original character's single-sprite, 40 PPU, Point-filtered, no-mipmap settings and custom foot pivot `(0.4765625, 0.09821428571428571)`.

Run `C:/Users/User/Desktop/Aseprite/aseprite.exe -b --script Tools/build_cadet_a.lua` from this directory to rebuild, then run `-b --script Tools/validate_cadet_a.lua`. Validation checks every native frame against its PNG, dimensions, binary alpha, palette limit, weapon extent and foot baseline. `cadet-a-motion-contact.png` shows representative poses at 2× integer scale.

The head transfer recognizes the connected blade before putting foreground sword pixels back; dark pixels from Iia's old hair cannot reappear as stray points around the new bob. Export removes newly severed one-to-three-pixel fragments, closes new enclosed one-pixel head/neck holes, and reconnects body or weapon sections exposed by the shorter hair. The validator compares all 121 frames with their corresponding original poses for new isolated pixels, pinholes and larger detached sections. It also checks weapon extent, foot baseline and native Aseprite/PNG equality.

The recoil pose receives a small cap cleanup: the donor hair crown occupied pixels above the approved bob and appeared as a dark floating dash. The validator checks that this upper strip remains clear after regeneration.
