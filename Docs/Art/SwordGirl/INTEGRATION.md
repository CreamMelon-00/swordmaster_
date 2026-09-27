# SwordGirl animation integration

The player now uses the full-body SwordGirl artwork in `Assets/Game/Resources/SwordGirl/Animations`. The enemy and authoritative combat rules remain unchanged. Original MobStudent assets are retained for reference.

## Assets

- 44 transparent PNG cels: idle (8), slash (12), pierce (12), blunt (12).
- 256 × 224 pixels per cel, Point filtering, no mipmaps or compression, 40 pixels per world unit.
- Shared source pivot at (101, 22) pixels from the bottom left. Runtime sprites retain the arena's existing -2.23 ground offset.
- Only the Character layer is exported; the game's impact effects provide the combat VFX.
- Editable source: `Docs/Art/SwordGirl/SwordGirl.aseprite`. Generation prompts and authored timing are archived alongside it.

## Playback

`MobStudentAnimationSet` retains its historical public name but loads SwordGirl assets. Its former upper-body API now returns full-body frames. The old lower-body renderer is disabled, including on step afterimages.

Idle follows the authored per-frame durations. The four source motions each last 1.26 seconds. Combat attacks are resampled onto the existing gameplay attack cycle (1/6 second at playback speed 1). Source contact frame 5 is aligned with `LegacyArenaView.OriginalImpactTime` (1/12 second at speed 1). Hit stop, attack gaps, speed controls, and damage timing use the existing combat clock.

There is one animation per attack type, reused for every hit in a combo. No separate walk or defense motion was authored: existing displacement continues with the new idle/guard pose; movement during a held strike preserves its contact pose and facing.

## Verification

`MobStudentAnimationPlayModeTests` checks all imports and pivots, variable idle timing, every attack frame, contact alignment, full-body step/afterimage behavior, and arena rendering. Existing tempo, legacy animation, and pressure-trail tests are updated to the new frame names while retaining their gameplay assertions.

For optional camera captures, set `SWORDGIRL_VALIDATION_OUTPUT` before running PlayMode tests. `SwordGirl_RealArenaRendersAllThreeContactPosesAndIdle` writes four PNGs there. Validation is performed in a separate project copy so the user's open Unity editor remains available.

### Verified on 2026-09-27

Unity 6000.5.9f1: 51/51 selected PlayMode tests passed (0 failed), including SwordGirl imports/timing/rendering, legacy animation, combat tempo, pressure trails, duel prototype, forest grounding, step motion, and step feedback. Results are stored in `playmode-results.xml`.

The validation copy and actual project were hash-compared across 103 changed source/resource files with no differences. The open editor also imported the new assets and compiled without C# errors. Four arena-camera renders were inspected for idle and all attack contact poses; `unity-idle.png` records the integrated appearance. These captures use the actual arena camera, without the HUD overlay. Native GUI interaction was unavailable, so PlayMode verification used Unity's automated runner in the separate copy.
