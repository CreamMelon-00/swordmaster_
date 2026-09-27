# Original duel assets reused by the playable port

These files are copied byte-for-byte from the legacy project `Turn_Limbo/Assets` into
`Turn_Limbo_Next/Assets/Game/Resources/LegacyDuel`. The corresponding `.meta` files are
also copied unchanged to retain original sprite slicing, pivots, filtering and import settings.
They are separate Unity projects, so the legacy GUIDs do not duplicate another asset in Next.

This manifest lists the first duel's foundational art. The active presentation now
uses world-space SpriteRenderers, original animation clips and clash particles,
plus a uGUI HUD. Additional arena and HUD resource mappings are documented in
`LegacyPresentationAssets.md` and `LegacyHudParity.md`; those resources extend the
foundational list below.

## Asset list

| New resource folder | Legacy source | Files | Texture / frame dimensions |
| --- | --- | --- | --- |
| `Background` | `Resource/Sprite/UI` | `pa_background_-_school_in_game.png` | 320×180 |
| `Player` | `Resource/Sprite/Player` | `pa_player_idle-Sheet.png` | 288×320 sheet; eight 144×80 frames |
| `Player` | `Resource/Sprite/Player` | `pa_player_slash-Sheet.png`, `pa_player_sting1-Sheet.png`, `pa_player_nike-Sheet.png`, `pa_player_g-Sheet.png` | Each 288×80 sheet; two 144×80 frames |
| `Enemy0` | `Resource/Sprite/Enemy/Enemy0` | `pa_enemy_1_i-Sheet.png` | 480×240 sheet; eight 160×80 frames |
| `Enemy0` | `Resource/Sprite/Enemy/Enemy0` | `pa_enemy_1_s-Sheet.png`, `pa_enemy_1_st-Sheet.png`, `pa_enemy_1_n-Sheet.png`, `pa_enemy_1_g-Sheet.png` | Each 288×80 sheet; two 144×80 frames |
| `Icons` | `Resources/Icon` | `skill1.png` through `skill9.png` | Each 106×106 |
| `Font` | `Resource/Font` | `neodgm.ttf` | NeoDunggeunmo original Korean pixel font |
| `Audio` | `Audio/Sword` | `hit1.mp3`, `hit2.mp3`, `Parry.mp3`, `Critical.mp3` | Original clash and critical-focus sounds |
| `Audio` | `Audio` | `add_skill_1.mp3`, `add_skill_2.mp3`, `add_skill_3.mp3` | Original queue-selection sounds |

## Animation mapping

Frame order and timing were checked against `Resources/Animation/Player/*.anim`
and `Resources/Animation/Enemy0/*.anim` in the legacy project. Every clip runs at
12 fps, and references sprite names in the order `_0`, `_1`, ... . Idle loops for
eight frames (2/3 second); all other clips contain two frames (1/6 second).
The original attack event occurs at the second frame, 1/12 second after clip start.

| View action | Player sheet | Enemy0 sheet | Legacy clip |
| --- | --- | --- | --- |
| `idle` | `pa_player_idle-Sheet` | `pa_enemy_1_i-Sheet` | `Idle.anim` |
| `slash` | `pa_player_slash-Sheet` | `pa_enemy_1_s-Sheet` | `Slash.anim` |
| `thrust` / `Penetrate` | `pa_player_sting1-Sheet` | `pa_enemy_1_st-Sheet` | `Penetrate.anim` |
| `hit` | `pa_player_nike-Sheet` | `pa_enemy_1_n-Sheet` | `Hit.anim` |
| `defence` / `Defense` | `pa_player_g-Sheet` | `pa_enemy_1_g-Sheet` | `Defense.anim` |

`LegacyDuelArt` loads and caches the original sliced sprites by name, preserving
pivots including Enemy0 idle's custom x=.65 pivot. The active `LegacyArenaView`
loads selected original Player and Enemy0 `.anim` files from
`Resources/LegacyArena/Animation` and samples their sprite curves onto
SpriteRenderers. It does not import the legacy character controllers or combat
scripts. The session handles attack events through `BeginNextSlot()`,
`ResolveNextHit()` and `CompleteCurrentSlot()` at the verified original timestamps.
Each repeated hit updates health and resistance when it occurs; the view does not
apply a whole multi-hit slot's damage on its first frame.

The legacy Player and Enemy0 have no authored hurt or death clips. Those actions
reuse the original idle pose with a tint; they are not newly drawn sprites.

## View API

Construct `LegacyDuelArt` once after resource import. `UIFont` supplies the Korean
font, `BackgroundSprite` and actor sprite getters supply the initial world art,
and `GetSkillIcon(skillId)` supplies the skill images used by the uGUI HUD.
`PlaySelection(AudioSource, lane)` accepts lanes 0–2;
`PlayClash(AudioSource, parried, alternateHit)` plays the reused clash sounds.
The older `Draw*` IMGUI helpers remain in the art class but are not the active
playable presentation.

Only selected art, clips, UI, a font and effects used by the first duel were imported. The legacy
project remains the source of record; no original files were changed. This is
local prototype reuse, not a claim that external asset redistribution rights have
been verified for a commercial release.
