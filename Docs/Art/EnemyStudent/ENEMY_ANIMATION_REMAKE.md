# School-uniform enemy animation remake

The active arena enemy uses the approved auburn-haired school-uniform pixel design. Original LegacyDuel and LegacyArena resources remain intact as fallback/reference assets.

## Artwork

- Native source: `Enemy_Uniform_Animations.aseprite`, 256 x 224, 45 editable RGBA cels and 5 animation tags.
- Idle: frames 1-8. Approved base cel with small breathing motion; feet stay planted.
- Slash: frames 9-20. Overhead diagonal cut.
- Pierce: frames 21-32. Straight forward thrust and retraction.
- Blunt: frames 33-44. Short two-handed pommel strike.
- Block: frame 45. Held two-handed guard.
- Each attack uses 12 timed cels assembled from distinct key poses and holds; these are not 12 independently redrawn key poses.
- Face remains oriented left. Native palette comes from the approved player-matched enemy base. Head sizing and ground line are normalized in Aseprite.
- The initially incorrect raised-sword grip was repaired. Attack blades are reconstructed on one rigid axis with a shared length and remain behind the gripping hands/body where occluded.
- See `ANIMATION_PROMPTS.md` for the built-in image-generation prompts. Aseprite processing and reproducible export are in `build_enemy.lua`.

## Runtime

`EnemyStudentAnimationSet` loads 45 cels under `Resources/EnemyStudent/Animations`. Imports use Point filtering, uncompressed RGBA, no mipmaps, 40 PPU, and ground pivot (122,202). The runtime applies the same ground offset as the player.

The arena samples enemy cels on its existing combat clock. Attack contact cel 5 (authored at 420 ms) maps to the original half-cycle hit. Multi-hit repetition, playback speed, hit stop, attack gaps, guard holds and mutual clashes retain their existing rules. Reset immediately samples the new idle instead of briefly displaying the legacy sprite. Damage still uses the existing flash; no new hurt-pose behavior is introduced.

## Validation

Aseprite export checks 45 frames, 5 tags, fully opaque foreground alpha and no occupied canvas-edge pixels. The native file is reopened after saving. Unity Play Mode tests run in a separate validation project using Unity 6000.5.9f1; results and camera captures are saved alongside this document.

The first selected suite run passed 93 of 94 tests. The sole failure was the forest test's obsolete 18 PPU expectation. It was updated to the shared 40 PPU setting and the ground-band test was rerun separately; see `ground-test-results.xml` for that final result. Runtime code did not change after the 93 successful tests.

Game-camera captures include idle, each attack's contact and guard. These show both characters together at the actual world scale. The separate GIF preview uses authored timing for easier inspection; gameplay continues to use the configured combat speed.
