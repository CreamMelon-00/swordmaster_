# 버튼형 기술 아이콘

> 이 문서는 첫 9개 `SkillButtons/skill1.png`~`skill9.png`의 제작 기록이다. 현재 전투 화면은 `SkillRoles/skill-role-atlas.png`의 검 전용 그림을 쓴다. 2026-10-03 기본 기술 이름·역할은 `../StarterSkillDesign.md`를 따른다.

## 적용 범위

제작 당시 첫 결투에서 사용하던 9개 기술의 아이콘을 새 키캡형 버튼으로 제작했다. 당시 전투 규칙, 비용, 키 배치, 기술열 순서, HUD 레이아웃은 변경하지 않았다.
이미지 생성 스킬의 기본 내장 도구(image_gen)를 사용했다. 생성 후 픽셀을 별도 스크립트로 그리거나 수정하지 않고 최종 PNG를 프로젝트에 복사했다.

최종 파일: `Assets/Game/Resources/SkillButtons/skill1.png`부터 `skill9.png`.
원본 퍼즐 PNG는 `Assets/Game/Resources/LegacyDuel/Icons/`에 그대로 보존한다.
당시 `LegacyDuelArt.GetSkillIcon`이 이 세트를 로드했고 현재·다음·플레이어/적 큐·전투 기록이 같은 매핑을 사용했다. 지금은 위에 적은 검 전용 atlas가 이 역할을 맡는다.

## 시각 규칙

- 공통 정사각 키캡, 잘린 네 모서리, 밝은 테두리, 어두운 버튼 면, 도트풍 문양.
- 베기는 호박색 곡선, 찌르기는 청록색 직선, 부수기는 주황색 수직 충격, 방어는 연녹색 방패/방향 전환/교차 칼.
- 기본/강화는 색만으로 구분하지 않고 쌍곡선·정밀 표적·큰 충격 문양으로 구분한다.
- Q/W/E, ACT 비용, 이름은 그림에 넣지 않는다. 기존 HUD가 실제 세션 값으로 표시한다.
- 처음 생성한 투명 버전은 버튼 면 내부에 의도하지 않은 알파 구멍이 있어 채택하지 않았다. 최종 세트는 전체 불투명 사각 타일이며, 배경은 중립적인 짙은 남색이다.
- 원본은 모두 1254×1254 PNG. Unity에는 Sprite Single, 중앙 pivot, 최대 256px, Bilinear, Clamp, mipmap 없음, 압축 없음으로 가져온다. 원본 고해상도 PNG는 보존한다.
- 신규 스프라이트는 106px 원본 퍼즐 전용 HUD crop 대상이 아니다. 공유 imported Sprite의 소유권은 Resources에 남으며 HUD Dispose가 폐기하지 않는다.

| ID / 파일 | 기술 | ACT | 구분 문양 |
| --- | --- | --- | --- |
| 1 / skill1.png | 베기 | 1 | 칼과 한 개의 베기 호 |
| 2 / skill2.png | 예리한 베기 | 2 | 칼과 두 개의 베기 호 |
| 3 / skill3.png | 찌르기 | 1 | 오른쪽으로 나아가는 직선 칼 |
| 4 / skill4.png | 정교한 찌르기 | 2 | 직선 칼과 정밀 표적 |
| 5 / skill5.png | 부수기 | 1 | 아래로 찍는 칼과 작은 충격 |
| 6 / skill6.png | 강력한 부수기 | 3 | 아래로 찍는 칼과 큰 충격 |
| 7 / skill7.png | 막기 | 1 | 가로 가드가 있는 방패 |
| 8 / skill8.png | 흘리기 | 1 | 방패를 우회하는 곡선 |
| 9 / skill9.png | 쳐내기 | 1 | 교차 칼과 튕겨내는 불꽃 |

## 제작 프롬프트와 출처

각 기술마다 별도 생성 호출을 했다. 기술 1의 최종 이미지를 공통 스타일/편집 기준으로 사용하여 외곽 버튼 형태를 유지했다.
아래는 실제 전달한 전체 프롬프트다. CLI/API 키를 사용하는 fallback은 사용하지 않았다.

### 기술 1: 최초 생성

Use case: stylized-concept. Asset type: production raster game skill UI icon for a pixel-art 1v1 sword duel; NOT a mockup. Create ONE centered square mechanical keyboard-style push-button / keycap, straight-on orthographic top view, subtly stepped bevel, four clipped corners. Strong coherent 16-bit pixel art: chunky intentional pixel edges, very limited flat shade bands, matte dark graphite navy top surface (#202b38), lighter slate upper-left bevel, deep lower-right bevel, thin off-white rim. The square button occupies exactly the central 88% of the canvas, symmetrical equal transparent margins all four sides, no outside glow, no drop shadow outside the silhouette. Opaque button body with genuinely transparent background outside, PNG alpha. Inner pictogram fills the middle 55%, bright pale ivory and restrained skill-family accent, simple bold silhouette visible at 48px, not thin intricate lines. Keep all details inside the button. No text, no letters, no numerals, no ACT cost, no key binding, no logo, no watermark, no puzzle shapes/tabs/notches, no scene/background, no perspective tilt, no realistic glossy 3D rendering. Output square 1024x1024. Consistency: every skill uses this identical button housing; only center pictogram and restrained accent change. Subject: BASIC SLASH skill. A single sweeping diagonal curved sword-cut arc from upper left to lower right, small straight sword blade integrated into the cut. ONE clear slash only, no starburst/no target. Accent: warm amber (#efbf68). Focus on a crisp, bold single crescent cut glyph, avoiding a complicated weapon illustration.

### 기술 1: 최종 불투명 수정

Use case: precise-object-edit. Image 1 is the EDIT TARGET: a basic slash skill button icon. Keep the exact single sword + amber crescent slash pictogram, the square keycap design, stepped pixel edges, straight-on view, colors and frame proportions. Fix only the unwanted transparent holes and face texture: make the keycap face COMPLETELY SOLID opaque flat dark graphite navy, without stains, mottling, dark holes, gradients or grain. Make THE ENTIRE IMAGE OPAQUE: fill all outside margins with a uniform very dark graphite #101820 (not pure black). Absolutely no alpha transparency anywhere. This is a production square pixel-art button icon, no scene, no typography, no new symbols, no watermark. Flat clean 16-bit pixel art and crisp glyph. Maintain the same centered square frame and equal margins.

### 기술 2: 예리한 베기

Use case: precise-object-edit. Image 1 is the edit target and COMMON STYLE MASTER for a production pixel-art sword-duel skill button. Keep EXACTLY the square keycap housing, border/bevel, size, position, straight-on perspective, corner cuts, palette of the housing, opaque navy face, and outside margins of Image 1 unchanged. REPLACE ONLY the inner amber sword+slash pictogram with the following new glyph; remove all of the old glyph, do not add unrelated motifs. Match the same chunky pixel art, ivory primary silhouette, few flat shades. Icon must be recognizable at 48x48px: bold forms, not tiny lines. Center the glyph in the same inner footprint, keep it inside the frame. Entire image completely opaque: no alpha or transparent holes. No typography, no letters, no QWE, no costs, no puzzle tabs, no scene, no watermark. ENHANCED SLASH: one small diagonal sword with TWO clearly separated sweeping crescent slash arcs alongside it, not one; the twin arcs make it visually different from the basic single slash. Warm amber accent, bright ivory, keep the twin-arc silhouette simple and substantial.

### 기술 3: 찌르기

Use case: precise-object-edit. Image 1 is the edit target and COMMON STYLE MASTER for a production pixel-art sword-duel skill button. Keep EXACTLY the square keycap housing, border/bevel, size, position, straight-on perspective, corner cuts, palette of the housing, opaque navy face, and outside margins of Image 1 unchanged. REPLACE ONLY the inner amber sword+slash pictogram with the following new glyph; remove all of the old glyph, do not add unrelated motifs. Match the same chunky pixel art, ivory primary silhouette, few flat shades. Icon must be recognizable at 48x48px: bold forms, not tiny lines. Center the glyph in the same inner footprint, keep it inside the frame. Entire image completely opaque: no alpha or transparent holes. No typography, no letters, no QWE, no costs, no puzzle tabs, no scene, no watermark. BASIC THRUST: a straight horizontal sword pointing RIGHT, triangular sharp point, two short parallel straight speed trails behind the hilt, no curved slash arc, no target. Pale icy cyan accent. Strong horizontal silhouette and simple pointed blade.

### 기술 4: 정교한 찌르기

Use case: precise-object-edit. Image 1 is the edit target and COMMON STYLE MASTER for a production pixel-art sword-duel skill button. Keep EXACTLY the square keycap housing, border/bevel, size, position, straight-on perspective, corner cuts, palette of the housing, opaque navy face, and outside margins of Image 1 unchanged. REPLACE ONLY the inner amber sword+slash pictogram with the following new glyph; remove all of the old glyph, do not add unrelated motifs. Match the same chunky pixel art, ivory primary silhouette, few flat shades. Icon must be recognizable at 48x48px: bold forms, not tiny lines. Center the glyph in the same inner footprint, keep it inside the frame. Entire image completely opaque: no alpha or transparent holes. No typography, no letters, no QWE, no costs, no puzzle tabs, no scene, no watermark. PRECISE THRUST: a straight horizontal sword pointing RIGHT into a clear small precision bullseye / crosshair at the tip (one thick ring with four short cardinal ticks), two short speed trails behind. Pale icy cyan accent. It must be visibly different from the basic thrust through the target symbol, bold and uncomplicated.

### 기술 5: 부수기

Use case: precise-object-edit. Image 1 is the edit target and COMMON STYLE MASTER for a production pixel-art sword-duel skill button. Keep EXACTLY the square keycap housing, border/bevel, size, position, straight-on perspective, corner cuts, palette of the housing, opaque navy face, and outside margins of Image 1 unchanged. REPLACE ONLY the inner amber sword+slash pictogram with the following new glyph; remove all of the old glyph, do not add unrelated motifs. Match the same chunky pixel art, ivory primary silhouette, few flat shades. Icon must be recognizable at 48x48px: bold forms, not tiny lines. Center the glyph in the same inner footprint, keep it inside the frame. Entire image completely opaque: no alpha or transparent holes. No typography, no letters, no QWE, no costs, no puzzle tabs, no scene, no watermark. BASIC CRUSH / HEAVY BLADE IMPACT: one wide vertical sword blade pointing DOWN into a compact four-point ground impact burst, bold blade and simple impact. Warm coral-orange accent. No hammer, no shield, no shield-breaking symbolism. Visibly different from the diagonal slash and horizontal thrust.

### 기술 6: 강력한 부수기

Use case: precise-object-edit. Image 1 is the edit target and COMMON STYLE MASTER for a production pixel-art sword-duel skill button. Keep EXACTLY the square keycap housing, border/bevel, size, position, straight-on perspective, corner cuts, palette of the housing, opaque navy face, and outside margins of Image 1 unchanged. REPLACE ONLY the inner amber sword+slash pictogram with the following new glyph; remove all of the old glyph, do not add unrelated motifs. Match the same chunky pixel art, ivory primary silhouette, few flat shades. Icon must be recognizable at 48x48px: bold forms, not tiny lines. Center the glyph in the same inner footprint, keep it inside the frame. Entire image completely opaque: no alpha or transparent holes. No typography, no letters, no QWE, no costs, no puzzle tabs, no scene, no watermark. POWERFUL CRUSH / HEAVY BLADE IMPACT: one wide vertical sword blade pointing DOWN into a LARGE angular impact starburst with two thick outward crack lines; noticeably larger heavy impact than the basic crush. Warm coral-orange accent, ivory. No hammer, no shield or shield-breaking symbolism.

### 기술 7: 막기

Use case: precise-object-edit. Image 1 is the edit target and COMMON STYLE MASTER for a production pixel-art sword-duel skill button. Keep EXACTLY the square keycap housing, border/bevel, size, position, straight-on perspective, corner cuts, palette of the housing, opaque navy face, and outside margins of Image 1 unchanged. REPLACE ONLY the inner amber sword+slash pictogram with the following new glyph; remove all of the old glyph, do not add unrelated motifs. Match the same chunky pixel art, ivory primary silhouette, few flat shades. Icon must be recognizable at 48x48px: bold forms, not tiny lines. Center the glyph in the same inner footprint, keep it inside the frame. Entire image completely opaque: no alpha or transparent holes. No typography, no letters, no QWE, no costs, no puzzle tabs, no scene, no watermark. BLOCK: one upright broad shield silhouette, pointed bottom, a thick horizontal guard bar across its face. No arrows, no sword, no attack burst. Pale mint-green accent, ivory. Calm solid shield is the clearest most basic defensive button.

### 기술 8: 흘리기

Use case: precise-object-edit. Image 1 is the edit target and COMMON STYLE MASTER for a production pixel-art sword-duel skill button. Keep EXACTLY the square keycap housing, border/bevel, size, position, straight-on perspective, corner cuts, palette of the housing, opaque navy face, and outside margins of Image 1 unchanged. REPLACE ONLY the inner amber sword+slash pictogram with the following new glyph; remove all of the old glyph, do not add unrelated motifs. Match the same chunky pixel art, ivory primary silhouette, few flat shades. Icon must be recognizable at 48x48px: bold forms, not tiny lines. Center the glyph in the same inner footprint, keep it inside the frame. Entire image completely opaque: no alpha or transparent holes. No typography, no letters, no QWE, no costs, no puzzle tabs, no scene, no watermark. DEFLECT / FLOWING GUARD: a smaller upright shield with one broad curved arrow sweeping around its right side, arriving from upper left then flowing aside rather than striking shield. Pale mint-green accent, ivory. Make the sweeping curved-arrow silhouette obvious and distinct from plain block. No damage cracks or impact burst.

### 기술 9: 쳐내기

Use case: precise-object-edit. Image 1 is the edit target and COMMON STYLE MASTER for a production pixel-art sword-duel skill button. Keep EXACTLY the square keycap housing, border/bevel, size, position, straight-on perspective, corner cuts, palette of the housing, opaque navy face, and outside margins of Image 1 unchanged. REPLACE ONLY the inner amber sword+slash pictogram with the following new glyph; remove all of the old glyph, do not add unrelated motifs. Match the same chunky pixel art, ivory primary silhouette, few flat shades. Icon must be recognizable at 48x48px: bold forms, not tiny lines. Center the glyph in the same inner footprint, keep it inside the frame. Entire image completely opaque: no alpha or transparent holes. No typography, no letters, no QWE, no costs, no puzzle tabs, no scene, no watermark. PARRY / BAT ASIDE: two short sword blades CROSSING in an X, with one clear four-point deflection spark at their contact. Pale mint-green accent, ivory. Strong X silhouette, one spark only, no shield and no curved arrow. Distinct from both plain block and flowing guard.

## 검증

검증 결과와 Game View 캡처는 `../Validation.md`에 기록한다. 아이콘 생성 자체를 게임 연결·가독성 검증 완료로 간주하지 않는다.
