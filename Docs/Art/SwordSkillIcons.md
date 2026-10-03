# 검 전용 세부 역할 아이콘 제작

> 2026-10-03 추가: 탐색(그림 16)과 몰아치기(그림 17)는 기존 15개 아틀라스를 유지하고 각각 `Assets/Game/Resources/SkillRoles/role16.png`, `role17.png`로 로드한다. 탐색은 사용자 요청에 따라 검 대신 눈을 중심 문양으로 쓴다. 편집 가능한 Aseprite 원본과 제작 조건은 `SkillIcons/README.md`에 있다.

> 2026-10-03 기본 기술 재설계 이후의 실제 그림 매핑은 `../StarterSkillDesign.md`와 `../SkillRoleLanguage.md`가 우선한다. 이 문서의 아래 제작 프롬프트와 당시 1~9번 기술 대응은 제작 기록이다. 현재 Q2 연속 베기는 그림 11, W3 깊은 찌르기는 그림 14를 쓰며, E5·E6은 저항 직접 감소를 표현하는 전용 그림이 아직 없다.

내장 이미지 생성 도구(built-in)를 사용해 이전 `SkillRoles/skill-role-atlas.png`를 편집했다. 무기를 모두 검으로 통일하고9기본+6추가 스킬의 실제 효과를 구분한다. 원본 생성 결과는 `C:/Users/User/.codex/generated_images/01a0dd3b-1ec7-7262-8afd-d810dd15286a/exec-9ee8703f-6ad3-4e6a-910d-c82bab709711.png`, 게임 사용 파일은 `Assets/Game/Resources/SkillRoles/skill-role-atlas.png`다. 이전9개 버전은 `Docs/Art/Archive/role-icons-v1.png`에 보존한다.

## 파일·임포트

실제 출력971×1619 PNG의 모서리/셀 사이 표본 alpha는0이다. 그림은3열5행이지만 AI 출력의 실제 행 간격은 일정하지 않았다. 처음 균등323×323 slicing으로 검수하니 다음 줄의 테두리가 일부 아이콘에 섞였다. alpha 연결 영역15개를 읽기 전용으로 측정하고, 각 실제 문양의 경계에6픽셀 투명 여백을 더한 개별 Sprite rect로 보정했다. 원본 픽셀은 편집하지 않고 AI 출력 alpha를 그대로 복사했다. 중앙pivot와 preserveAspect로 실제 버튼에는 같은 최대 크기로 그린다. 15개 Sprite의 폭265~308/높이265~317이며, 잘못된 격자 조각을 아이콘 의미로 사용하지 않는다.

Point / Clamp / 무압축 / Mipmap 없음 / 최대2048 / Full Rect / 중앙pivot. Texture GUID, Sprite1~9의 spriteID/internalID를 유지하고10~15만 추가했다. Resources 경로도 그대로다. 현재/다음/큐/기록/로비가 같은 IconId를 사용한다. 새로운6변형은 역할 효과를 공유하지 않는 획득 기술을 구분하기 위한 것이며 신규 전투 효과를 추가하지 않는다.

모든 중심 무기는 검이다. 녹색 방패형은 버튼 테두리이며 실물 방패를 들고 있다는 의미가 아니다. 청록 순환 표식은 ACT 회복, 보라 ↑ 표식은 후속 위력, 옅은 청색 보호 곡선은 피해 감소, 큰 검끝 섬광은 고화력이다. 실제 의미/조건과15기술 대응은 `../SkillRoleLanguage.md`를 따른다. AI 그림이 완벽한48px 논리 격자나 정확한 제한 색상 수를 보장하지 않으므로40~56px 실제 UI에서 판독을 확인한다. 아직 없는 저항 전용/방어 무시/회피/치명타 효과를 표식으로 약속하지 않는다.

## 최종 편집 프롬프트

```text
Use case: precise-object-edit
Asset type: transparent pixel-art sword skill icon atlas used at 40–56 pixels inside a Unity duel game's buttons.
Input image 1: EDIT TARGET, the existing 3x3 atlas. Keep the existing dark navy keycap interiors, chunky pixel outlines, consistent small-button readability, and role-family border colors/silhouettes. Replace all weapon/defense props with SWORDS ONLY and expand the atlas for precise per-skill utility distinctions.
Output composition: portrait canvas with aspect ratio exactly 3:5, ideally 1200x2000. EXACTLY THREE COLUMNS AND FIVE ROWS, fifteen equal SQUARE cells, row-major top to bottom. Each icon centered in its cell, same scale, roughly 80% cell occupancy, transparent margin around it. No grid lines. True alpha transparency outside all icon silhouettes and in gutters, NOT a black/white/checkerboard backdrop.
Style: deliberately simplified crisp low-resolution pixel art enlarged with nearest-neighbor-looking steps, small palette, broad silver-white blades with clear crossguard and short brown hilt, opaque navy keycap interior, no thin hairline filigree or tiny unreadable ornament. Every weapon is unmistakably a SWORD, not a spear, rapier without guard, hammer, axe, staff, mace, arrow weapon, or physical shield.
Invariant border families: orange diamond for SLASH, cyan tall hexagon for THRUST, gold/yellow notched-square for SWORD SMASH, green shield-SHAPED BORDER for SWORD DEFENCE. A green shield outline is a UI silhouette only, not a physical shield prop. All centre subjects are swords with abstract action trails or effect glyphs.
Semantic effect glyph system: ACT recovery = small clearly separate cyan circular return-arrow around a diamond energy pip at lower-right of icon; follow-up power utility = small violet doubled UP chevrons at lower-right; damage reduction = small pale-blue nested protective arcs at lower-right; pure high power = large warm-white impact burst at sword tip, no utility glyph. Normal attacks/guards must have NO utility glyph. Never use a plus sign, cross, medical symbol, heart, letter, number, label, words, or invented readable text.
Cells, EXACT row-major order:
ROW 1:
1. Orange diamond, a single sweeping silver sword and one curved orange slash trail, with the cyan energy return-arrow/pip utility glyph (ACT recovery).
2. Orange diamond, two crossed silver swords and two clean slash trails, bright broad impact burst (high-power two-hit slash), NO recovery or support glyph.
3. Cyan hexagon, a silver crossguard sword thrust horizontally with straight cyan motion trails, violet upward double-chevron utility glyph (following skill power).
ROW 2:
4. Cyan hexagon, one long silver SWORD thrust horizontally with an exact small star impact at tip (powerful precision single hit), NO utility glyph.
5. Gold notched square, an unmistakable broad sword striking diagonally DOWNWARD, two short impact streaks, small abstract cracked DIAMOND at impact point (sword smash two hits). Not a hammer, not a shield, no exclusive armor-breaking promise.
6. Gold notched square, a broad silver sword striking down hard with a LARGE warm-white impact burst and three thick rays (highest-power three-hit sword smash), NO utility glyph.
ROW 3:
7. Green shield-shaped BORDER, two actual swords touching at right angles in a firm block, cyan energy return-arrow/pip utility glyph (conditional ACT recovery). No physical shield.
8. Green shield-shaped BORDER, a sword deflecting a second blade sideways with a curved green motion trail, pale-blue nested protective arc utility glyph (damage reduction). No physical shield.
9. Green shield-shaped BORDER, two angled swords in a parry, violet upward double-chevron utility glyph (following skill power). No physical shield.
ROW 4:
10. Orange diamond, one sword making a simple HORIZONTAL slash with one flat orange trail, NO utility glyph and NO huge burst. Distinct from cell1 which has ACT utility.
11. Orange diamond, two diagonal sword slash trails, NO utility glyph and NO huge burst. Distinct from high-power cell2.
12. Orange diamond, a SINGLE tall sword cleaving downward with a wide orange slash arc, two opposed small abstract triangular marks indicating VARIABLE power. NO letters or question mark. ONE sword, not crossed swords, not a guaranteed burst.
ROW 5:
13. Green shield-shaped BORDER, one stable horizontal sword guarding against one incoming blade, NO energy recovery glyph and NO utility glyph.
14. Cyan hexagon, a bold single silver sword lunging forward with a stronger broad warm-white tip burst, NO utility glyph. High-power single sword thrust, no spear or shield.
15. Green shield-shaped BORDER, one silver sword angled to deflect an incoming blade with a simple green curve, NO protective arc badge, NO energy recovery, NO utility glyph.
Important: Each of the fifteen icons must be individually centered within its cell; don't crop the bottom row or invent additional rows/cells. Keep sword shapes readable at button size. No UI screenshots, no typography, no room background, no decorative title, no watermark. Real alpha transparency and the 3-column by 5-row atlas are essential.
```
