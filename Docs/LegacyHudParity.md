# Original first-duel HUD parity

Historical reference for the original presentation port. The current compact HUD
keeps its interaction contract but intentionally replaces this visual design;
see `CompactHudDesign.md` for the active layout and styling.

`LegacyCombatHud` reconstructs the visual hierarchy in
`Turn_Limbo/Assets/Scenes/IngameDevelop/___UIManager___.prefab` and the positioning
in the original `UIManager`, `UnitUI`, and `Controller`. It does not instantiate
the old prefab or import any of its gameplay scripts.

## Resource boundary

The 22 PNG files in `Assets/Game/Resources/LegacyHud` are unchanged original
visual assets. Their `.meta` files preserve pixel filtering and sprite-sheet
slice names/rectangles, but receive fresh GUIDs in this project. The existing
`LegacyDuelArt` supplies the original nine skill icons and `neodgm` font.
No DOTween, TextMeshPro, legacy singleton, or old scene dependency is required
for the HUD. The presentation assembly needs `Unity.ugui` and the already-used
`Unity.InputSystem`.

## Visible layout

Coordinates are the source Canvas's 1920 × 1080 reference coordinates. The
Canvas scales with screen width, matching the original CanvasScaler.

| Element | Source layout retained |
| --- | --- |
| Input backdrop | Original bottom-panel image, full width, 352 px high |
| Q/W/E current cards | 150 × 180 frames, centers (749,145), (979,145), (1209,145) from bottom-left |
| Q/W/E next cards | 150 × 180 frames, 54 px right and 41 px up from current cards |
| Skill images | Original icons at 130 × 130; original Q/W/E frame overlays and cost labels |
| ACT coin gauge | Original three-layer strip, 933.63 × 17.2 fill, value ACT / 10; no new ACT text |
| Commit | Original 260 × 260 A image at (1760.3,159.7) |
| Timer | Original backdrop/fill/outline sprites, center (0,437), 1212 × 184; fill remaining / 10, red-to-yellow |
| Round | Original small yellow round count inside the timer artwork; no countdown digits |
| Health/resistance | Original fan-shaped pixel images and Radial90 stencil masks, not numerical status cards or horizontal bars |
| Queues | 100 × 100 original icons, 10 px spacing; player grows left, enemy right; no action names or sequence numbers |
| Log access | Original 260 × 260 log-button sprite at (161,160), not an empty portrait circle; popup hidden until clicked |
| Resolution | Input artwork shrinks (OutQuad) and timer rises (OutCubic) over .5 sec; original up/down AttackView hierarchy; active icon grows to 1.5 then disappears |

Status and queue positions follow the fighters through the active camera.
Status offsets are the original ±2 world units; planning queues use
`(camera.orthographicSize - 3) * sign` and 2 world units up, and scale
`1 + (5 - orthographicSize) * .2`. Combat queues use the original y=900 in
1920 × 1080 reference Canvas units, and scale 1.5. The source's literal device
pixel y=900 is not reused at smaller resolutions, where it would move icons
above the display. World-linked screen positions are converted to Canvas coordinates so they
remain attached to the fighters at non-reference resolutions.

The original status transforms are retained, including `EnemyStatus` scale
`(1,-1,1)`. All player HP/resistance masks rotate +112.3° and their ring children
rotate -112.3°; enemy masks rotate -67.7° and their rings +67.7°. Several original
Euler hint values are stale, so the serialized quaternions, not those hints,
are the source of truth. Mask sizes, anchors, pivots, Radial90 origin and layer
order also follow the original prefab.

AttackView is not merely two 100-high black rectangles. Each original top/bottom
parent has height zero and scale `(1.5,1,1)`. Its active 700-high box is gray
`(.1509434,.1509434,.1509434,1)` and sits at -100/+100, leaving 100 visible pixels
at the screen edge; the separate 100-high black `line` child is inactive.
The bars slide 100→0 and -100→0 in .3 real-time seconds with OutQuad after the
first .5-second approach, then slide out at the start of the .5-second return.
Fatal attacks rotate AttackView to -8°/+8° in .15 combat-time seconds; the next
slot and return reset its angle over .15 seconds.

## Interaction contract

Gameplay state belongs to `LegacyQueuedDuel`. The HUD only reads it and invokes
the supplied `queue(lane)`, `commit()`, and `restart()` callbacks. Controller
keyboard handling remains authoritative.

- `Refresh(session, remaining, resolving, slot, camera, player, enemy, delta)`
  updates state, world-linked positions, transitions and floating damage.
- `BeginTurn()` restores planning presentation; `EndTurn()` starts the original
  input/timer hide transition at commit.
- `BeginCombat()` opens only the cinematic bars after initial approach;
  `BeginReturn()` closes only those bars at return start, keeping the input/timer
  hidden until `BeginTurn()`. The optional `realDelta` argument of `Refresh`
  preserves the original bars' unscaled timing during slow motion.
- `FatalAttack(playerAttacks)` reproduces the original -8°/+8° AttackView tilt.
- `SetCurrentSkills(player, enemy, duration)` starts the active queue-icon
  animation. It does not introduce a new action-name or clash-number panel.
- `SetHoldProgress(lane, progress)` updates the original green hold fill;
  a negative lane clears all fills.
- `ShowExplanation(skill, enemy)` is only called for long-hold or Tab inspection.
  Repeated calls with the same skill reuse the explanation text.
- `SetInspectedSlot(slot)` supplies the original yellow enemy queue highlight.
- `ShowHitDamage(targetPlayer, damage, worldPosition)` displays a temporary
  original-font damage number, with the original damage-dependent scale and
  lifetime. It does not retain a combat log.
- `RecordResolvedSlot(player, enemy, playerDamageDealt, enemyDamageDealt)` appends
  one paired log row after a slot. Damage is provided by the controller using
  the source's last displayed-hit overwrite semantics, not a newly summed value.
- `OpenLog()`/`CloseLog()` back the `LogButton` and `Log View/Close Log` click
  paths. `LogOpen` and `LogCount` expose read-only presentation state for tests.
- `ShowOutcome(outcome, turn)` shows Victory/Defeat and a retry callback using
  original button artwork; `Reset()` and `Dispose()` clean up owned UI state.

An Input System EventSystem is created only if the scene has none, and only
that owned instance is destroyed by `Dispose`.

## On-demand log

The original left circle contains a log button, whose old persistent callback
was `LogView.StartPanel`. The rebuilt popup uses the original black .8-alpha
1920 × 1080 backdrop, y=1080 initial hidden placement, .5-second OutQuad open to
y=0 and close to y=1100, and an 873.3-high vertical scroll area. Two 800-wide
columns hold paired 750 × 200 gray rows, skill names at font 50, red damage at
font 113, and 150 × 150 icons. Content follows the original minimum height 1000
and row-count × 220 rule, with new rows at the bottom. ScrollRect retains elastic
movement, inertia, .135 deceleration and sensitivity 1. The close button is at
(-829,-439), size 200 × 150.

Original rows used fighter portraits; this bounded first-duel port instead uses
the already-imported original skill icons for this first-duel slice, and leaves null-skill
icons/names empty. It does not import the old LogView/LogPanel scripts. Opening
the log does not add always-visible text or pause the combat clock. Committing
closes it; match reset clears all rows. The close caption is `닫기` using neodgm.

## First-duel scope and validation

This is the original combat HUD for the first enemy and nine starting skills.
Stage-selection, shops, progression rewards and roguelike menus are intentionally
outside this playable combat slice. The source's effect description text is
displayed only in inspection, using the runtime's verified first-nine descriptions.

Static checks cover sprite slice names, unique resource GUIDs, original layout
values, callback ownership and cleanup. Unity import, PlayMode interaction and
visual validation are performed by the integrating controller task; this document
does not claim those checks passed merely because the HUD source exists.
