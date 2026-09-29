# Enemy reactions: two guards and two hurt poses

Current native source: `Enemy_AllAttacks3_Reactions2.aseprite`, 256 x 224, 120 cels, 14 tags.

- Block: existing upright guard, frame 45.
- Block-2: new diagonal high guard, frame 119.
- Hurt: corrected existing abdomen recoil, frame 118.
- Hurt-2: new backward upper-body recoil, frame 120.
- The first 117 frames of the previous native source are pixel-identical, including all nine attack motions.

Each incoming body hit and each successful guard draws once from two poses. Consecutive repeats are allowed. Rendering, clock holds/rewinds and hit stop retain the selected pose. The last selected guard is held between impacts in a defence slot; a new slot begins with the default guard until its first impact. Reaction duration remains 0.16 seconds on the combat clock. HP penetration, broken guards and fatal body hits use hurt; successful blade blocks use guard; mutual blade clashes retain the attack contact pose. Enemy reactions use their own cosmetic random source, independent of both attack sources and player reactions. Combat rules and timing are unchanged.

## Existing hurt size correction

Unity import settings were already identical to idle: 40 PPU, 256 x 224 canvas, pivot (122, 22), point filtering and no compression. The visual discrepancy came from the pose artwork's shorter silhouette and narrower body. The existing generated source was reprocessed in Aseprite: head top 79 -> 73, target head width 52 -> 56, target head height 46 -> 44, gradual additional body width factor 1.05. The foot baseline remains y=201. The grip, pommel, guard and blade were reconstructed on one rigid axis after mapping. `hurt-scale-before-after.png` shows idle / previous hurt / corrected hurt at the same scale.

## Creation and validation

The two new pose sources were generated with built-in image_gen; exact prompts are in `PROMPTS.md`. Final palette mapping, proportion adjustment, weapon reconstruction, PNG exports and native cels were created using Aseprite Lua. Sources, scripts and native files are retained here. Existing originals remain in the sibling Expansion directory.

`verification.txt` records native checks. `playmode-results.xml`: 33/33 selected Play Mode tests passed in the separate Unity validation project using Unity 6000.5.9f1. Tests cover per-impact random draws, repeated choices, reaction holds, reset, slow clock, independent random sources, guard penetration/fatal hits, existing attack behavior, sprite dimensions/pivots/import settings and four camera captures. `Captures/` contains all four reaction poses beside the player. These are automated test-camera captures, not a claim of manual playtesting in the open editor.
