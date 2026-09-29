# Enemy attack variants and hurt

Adds two authored motions for each attack type and a hurt pose to the existing school-uniform enemy. The original 45 cels are pixel-identical to the prior native source.

## Native artwork

`Enemy_AllAttacks3_Hurt.aseprite`: 256 x 224; 118 RGBA cels; 12 tags.

| Motion | Frames | Description |
| --- | --- | --- |
| Idle | 1-8 | Existing breathing loop |
| Slash | 9-20 | Existing overhead cut |
| Pierce | 21-32 | Existing thrust |
| Blunt | 33-44 | Existing forward pommel strike |
| Block | 45 | Existing held guard |
| slash-2 | 46-57 | Rising cut |
| slash-3 | 58-69 | Horizontal cut |
| pierce-2 | 70-81 | Upright high thrust |
| pierce-3 | 82-93 | Low extended lunge |
| blunt-2 | 94-105 | Rising pommel strike |
| blunt-3 | 106-117 | Downward pommel strike |
| Hurt | 118 | Body-hit recoil |

Each new attack has four generated key poses plus the approved ready pose, assembled into twelve timed cels with holds. These are not twelve independently drawn poses. `extract_poses.lua` applies the approved palette, normalized head sizing, planted ground, and full sword reconstruction. `weapon_axes.lua` supplies a single pommel-to-guard axis for the handle, perpendicular crossguard and straight 84px blade. Original bent weapon fragments are removed, fingers remain above the grip, and detached remnants are discarded.

## Playback

Every enemy hit independently selects one of the three variants for its attack type. Repetition is allowed. The selected variant is cached for that hit, so clock holds and rewinds do not reroll. A new slot clears the cache. Enemy draws use a separate System.Random instance, without consuming the player's cosmetic or combat random streams.

Hurt displays for the existing 0.16-second reaction interval on the combat clock. Successful guard/blade block uses Block; HP damage, guard penetration and fatal body hits use Hurt. Mutual blade clashes keep each actor's attack contact pose. New slots, turn end and reset clear reactions. No combat damage or timing rule is changed.

## Outputs and validation

Built-in image-generation prompts: `PROMPTS.md`. Reproducible native assembly: `build_expansion.lua`, `extract_poses.lua`, `pose_specs.lua`, `weapon_axes.lua`.

Aseprite checks original-cel equality, 118 frames, 12 tags, opaque foreground alpha and no occupied canvas-edge pixels. Intended blade tips are also checked against canvas bounds. Play Mode results and camera captures are produced in the separate Unity validation project. Preview GIFs use slower authored timing; gameplay keeps its configured combat clock.

The selected Play Mode suite passed 98/98 tests (43.67 seconds). After a final removal of residual source-guard pixels, the four EnemyVariantsPlayModeTests were rerun with refreshed camera captures; see `final-art-tests.xml`. Runtime code was unchanged for that final art-only revision.
