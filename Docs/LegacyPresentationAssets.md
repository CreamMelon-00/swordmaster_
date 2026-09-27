# Original world presentation port

`LegacyArenaView` stages Player and Enemy0 as world-space SpriteRenderers, preserving
the existing sprite metadata (18 pixels/unit, scale 1, original pivots). It loads
copies of the actual legacy animation clips and samples them for presentation only.
Each actor has an Animator with no RuntimeAnimatorController. Unity 6000.5.9f1
requires this component to apply the non-legacy clips' SpriteRenderer object-reference
curves via `SampleAnimation`; without it the sprite stays on the initial Idle frame.
The Animator does not own transitions or timing: the view samples the original
clips on the controller's custom combat clock, including slow motion and repeated hits.
All six `Attacking` animation-event lists were removed from the copied clips so
sampling cannot call legacy combat code. Idle and Defense clips are unchanged.

## Current forest-duel changes

The table below records the historical legacy port, not all current movement values.
The current arena replaces only scenery and movement staging: a three-layer green
forest, faster approach/pursuit, damage-weighted push, and persistent positions
between turns. Actors, source animation clips, shadows, sparks, audio and combat
rules remain inherited. See `Art/ForestLayers.md` and `Architecture.md` for the
current contract. The school PNG is preserved but is no longer rendered in the arena.

## Original values (historical baseline)

| Behavior | Original evidence | Ported value |
| --- | --- | --- |
| Starting actors | `Scenes/Ingame.unity`, `Prefab/Stage/Enemy/Enemy0.prefab` | (-5,-0.5), (5,-0.5), scale 1 |
| Character size | Original texture metadata | 18 pixels/unit, 80-pixel frame height = 4.444 world units |
| First approach | `Controller.FirstAttackMove` | midpoint ±2, 0.5 seconds, OutCubic |
| Reapproach | `Controller.Attack` | both actors approach at 15 units/second when distance ≥5 |
| Attack clips | `Resources/Animation/Player` and `Enemy0` | 12fps, 2 frames, 1/6 second; strike at 1/12 second |
| Queue resolution wait | `Controller.Attack` | longest clip × attackCount +0.01 seconds; controller owns this wait |
| Return | `Controller.Attack` | controller waits 0.5 seconds, then view moves to x ±3.5 over 0.5 seconds, InOutSine |
| Knockback | `Unit.DamagePush` | pre-double damage ×0.3 units over 0.2 seconds |
| Damage tint | `Unit.Update`, `Unit.Attacking` | 0.25-second red/yellow/cyan flash |
| Planning camera | `UIManager.Update` | size 6→5 as timer decreases, y=-1.5; Tab size 3.5 and enemy focus |
| Resolving camera | `UIManager.Update`, `Unit.Attacking` | size 3.5, strike impulse size 2 +1-unit random positional jolt |
| Fatal camera | `UIManager.FatalDamageTimeSlow` | size 3, target focus and ±5–10° angle for 0.75 real seconds |
| Camera rotation default | `Scenes/IngameDevelop/___UIManager___.prefab` | isCamRotate=0; optional `CameraRotate` remains false |
| School scenery | `Prefab/Stage/BG/BG.prefab` | scale 4, y=0.49, repeated at x ±28.64 |
| Ground shadow | Player scene and Enemy0 prefab | scale (3,0.7), y=-2.23, black alpha 0.6117647 |

## Copied resources and adaptations

`Resources/LegacyArena/Animation/{Player,Enemy0}` contains the five original clips
per character (Idle, Slash, Penetrate, Hit, Defense). Existing sprite sheet GUIDs
resolve to the previously imported `LegacyDuel` assets.

`Resources/LegacyArena/VFX/DefaultParticle.prefab` retains the original two particle
systems, curves, ring data streams, mesh, gradients and burst settings. The root
scale is 2. The ring is a single burst with lifetime 0.3 and initial size 1.4; the
ray burst contains 50 particles with lifetime 0.4, speed 14, size 0.015 and a 0.05
second delay. Impact position is the attacking actor plus two world units toward
the opponent, exactly as in `Unit.Attacking`.

Only the original ring mesh, ray texture, two materials and the three CFXR shader
include files are copied. The package-specific URP light-data component is removed
from the copied prefab; the unused black point light is disabled at runtime.
The obsolete custom shader importer is not imported. `LegacyRing.shader` wraps
the original CFXR procedural ring shader include in a standard unlit pass; the
CFXR copyright notices remain in the original includes. `LegacySparks.shader`
uses the original single-channel ray texture and HDR color. Materials are redirected
to these two shaders. Quality settings select URP despite a null default pipeline
in GraphicsSettings. Shader selection therefore checks current, quality and default
pipeline assets. The generic unlit VFX passes are intended for both pipelines;
actual shader compilation and visual validation remain integration checks.

The source post-processing profile is `Scenes/SampleScene/BackGround.asset`, as
referenced by `Scenes/IngameDevelop/Controller.prefab`. The view creates an owned
runtime URP VolumeProfile with its original vignette (black, intensity 0.4,
smoothness 0.2, center 0.5/0.5), ChromaticAberration and ColorAdjustments. Fatal
hits set aberration intensity 1, postExposure 1, and saturation -80 for player
damage / +25 for enemy damage. They decay toward zero at 0.75, 0.75 and 60 per
scaled second, respectively, as in `Controller.Update`. The camera enables URP
post-processing and restricts its volume layer mask to the owned layer 30.
The original profile has no Bloom and its MotionBlur intensity is zero, so neither
is introduced. Depth of field is omitted because its orthographic support has not
been validated. The original separated background/effect cameras are combined
in this port, so grading affects the whole arena; the overlay HUD is unaffected.

`Resources/LegacyArena/Shadow/Circle.png` and its sprite metadata provide the ground
shadow from the legacy project. Actor, shadow and VFX bitmaps were not redrawn;
the new forest PNGs are separate resources.

## Integration contract

Create once with `LegacyArenaView.Create(parent, art)`. `BeginApproach` starts the
first approach (minimum 0.18 seconds, finite speed 32); wait for `ApproachComplete`. Before later slots, call
`CloseDistance(delta)` until `IsInRange`. `BeginSlot(playerSkill, enemySkill)` starts
the original clips and repeats their frames for each skill's attack count.
`PresentHit(playerAttacks, hpDamage, resistanceDamage, guarded, fatal, pushPower)`
only presents an authoritative hit. Current knockback prioritizes actual HP plus
resistance damage and uses `pushPower` only as a fallback/contact hint. Guarding
reduces movement; unfinished push accumulates outward rather than snapping back.

After the controller's 0.12-second post-resolution wait, `EndTurn` starts
a 0.18-second cleanup with no return movement; wait for `ReturnComplete` before `BeginTurn`.
`SetPlanningState(timeRemaining, inspecting)` controls the planning camera around
the current duel midpoint. Call `Tick(scaledDelta, realDelta)` every frame. `IsFatalFocus` requests
the controller's 0.15 slow-motion factor; the view uses 0.75 real seconds for focus.
Particles are manually simulated on the same scaled clock, and camera interpolation
uses the real frame clock like the original `UIManager`.

`ArenaCamera`, `PlayerRenderer`, `EnemyRenderer` expose the world objects for the
HUD. `PlayerScreenAnchor` and `EnemyScreenAnchor` use top-left screen coordinates.
`Reset` clears pooled effects and restores starting positions; `Dispose` destroys
all view objects and its runtime sprite material. Effects reuse at most 24 original
instances so repeated clashes do not leave destroyed-prefab references or accumulate
unbounded particles. Sounds are owned by the main controller and are not duplicated
by this view.
