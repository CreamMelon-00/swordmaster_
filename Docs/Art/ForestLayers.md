# 녹빛 숲길 레이어

첫 녹빛 숲 배경의 제작 기록이다. 현재 전장은 평면 중경을 벨트스크롤 숲길로 교체하고 레이어를 줄였으며, 최신 자산·배치·프롬프트는 `BeltForest.md`를 따른다. 이 문서의 18 높이/잔디선 배치는 이전 버전이다. 원본 교문 PNG와 첫 숲 PNG는 변경하지 않고 보존한다.

## 결과와 사용

- 제작 방식: 내장 이미지 생성 도구, 원본 교문 그림은 도트 양식 참고 이미지로만 사용했다.
- 최종 PNG는 모두 2172×724(3:1)이다. 원경은 불투명, 중경·근경은 실제 알파가 있는 PNG이며 알파를 수정하지 않았다.
- 파일: `Assets/Game/Resources/ForestArena/forest-far.png`, `forest-mid.png`, `forest-near.png`.
- Sprite Single / Full Rect / PPU 128 / Point / Clamp / 최대 4096 / mipmap·압축 없음.
- `ForestParallaxBackdrop`가 높이 18 월드 단위의 타일을 카메라 주변에 재사용한다. 카메라 이동 대비 원경 0.24, 숲길 1.0, 앞쪽 식물 1.35 속도로 흐른다. 숲길은 월드 위치에 고정되어 발과 지면의 상대 위치를 유지한다.
- 카메라 폭·회전 코너까지 계산해 최소 3개씩 필요한 수만 배치한다. 원점에서 멀어지거나 음수 X로 이동해도 타일이 이어진다. 매 프레임 리소스를 다시 읽거나 타일을 생성하지 않는다(최초 구성/화각 확장 때만 풀 증가).
- 인접 타일을 절대 인덱스의 홀짝에 따라 좌우 반전하여 양쪽 경계가 같은 원본 픽셀을 공유하게 한다. 따라서 단순 원본 좌우 끝의 그림 차이가 반복 경계의 빈틈을 만들지 않는다. 반복 형태는 정방향/반전 한 쌍이다.
- 중경의 나무·바닥은 그림자 뒤, 근경의 낮은 풀은 캐릭터 앞에 놓인다. 배경이 스킬·체력 HUD를 덮지는 않는다.
- 실제 출력의 평평한 잔디 시작선(510/724 높이)을 원본 발·그림자의 y=-2.73에 맞춰 중경 중심 y=0.95로 배치했다. 근경은 y=0.9로 낮춰 발과 숲길이 보이게 했다. 이미지의 알파나 픽셀은 수정하지 않았다.
- 고정 높이 18은 현재 플레이 카메라(편성 크기 5~6/회전 0, 교전 크기 2~3.5/일반 회전 최대 20도와 집중 효과)용이다. 임의의 크기·세로 확대·모든 화면 비율을 보장하는 자동 세로 배경 시스템은 아니다. 폭·회전 스트레스 검사는 수평 반복만 검사한다.

## 최종 제작 프롬프트

다음은 실제 요청한 최종 프롬프트다. 각 요청의 Image 1은 원본 `LegacyDuel/Background/pa_background_-_school_in_game.png`이며 편집 대상이 아니다. 요청 해상도는 3072×1024였으나 도구 최종 출력은 위의 2172×724다.

### forest-far

```text
Use case: stylized-concept
Asset type: production 2D side-view game background tile for a Unity pixel-art sword duel.
Input images: Image 1 is ONLY a pixel-art style reference, not an edit target. Do not retain its pink palette, school, gate, buildings or paving.
Primary request: a very wide horizontal green woodland path background, as one layer of a 3-layer parallax set.
Style/medium: clean handcrafted low-resolution pixel art matching the reference's hard square pixel clusters, restrained shading and readable silhouettes. Imagine a 768x256 pixel tile enlarged with nearest-neighbor pixels. No smooth painting, no 3D, no blur or antialiasing.
Composition: 3:1 horizontal canvas (3072x1024). Flat side-on forest path, NOT a path receding to a central vanishing point. Horizontally seamless repeat: left and right edge terrain/foliage meet at exactly the same heights and color bands. No central focal object. Uniform consistent pixel scale.
Palette: quiet emerald, moss green, deep blue-green shadow, muted mint haze, olive earth. Calm neutral-green daylight. No pink, purple, intense neon, orange autumn or huge white glare.
Constraints: no people, creatures, swords, UI, text, labels, frames, watermark. Full-bleed tile, no border, no empty canvas margin. Layer: FAR, fully opaque. Soft-valued distant forest with many slender dark teal tree silhouettes, pale muted-green light filtering through canopy, layered hazy treelines. No large dark trunks close to camera, no foreground vegetation. Broad unobstructed middle around 35–65% of image height for readable characters. Distant flat mossy ground begins at 64% down from top and fills lower portion. Restrained detail and low contrast so the fighting sprites stand out.
```

### forest-mid

```text
Use case: stylized-concept
Asset type: production 2D side-view game background tile for a Unity pixel-art sword duel.
Input images: Image 1 is ONLY a pixel-art style reference, not an edit target. Do not retain its pink palette, school, gate, buildings or paving.
Primary request: a very wide horizontal green woodland path background, as one layer of a 3-layer parallax set.
Style/medium: clean handcrafted low-resolution pixel art matching the reference's hard square pixel clusters, restrained shading and readable silhouettes. Imagine a 768x256 pixel tile enlarged with nearest-neighbor pixels. No smooth painting, no 3D, no blur or antialiasing.
Composition: 3:1 horizontal canvas (3072x1024). Flat side-on forest path, NOT a path receding to a central vanishing point. Horizontally seamless repeat: left and right edge terrain/foliage meet at exactly the same heights and color bands. No central focal object. Uniform consistent pixel scale.
Palette: quiet emerald, moss green, deep blue-green shadow, muted mint haze, olive earth. Calm neutral-green daylight. No pink, purple, intense neon, orange autumn or huge white glare.
Constraints: no people, creatures, swords, UI, text, labels, frames, watermark. Full-bleed tile, no border, no empty canvas margin. Layer: MIDDLE, genuinely transparent PNG background, preserve alpha (do not paint a checkerboard). Only medium-distance mossy tree trunks, sparse upper canopy along top edge, low woodland shrubs, and the continuous playable forest floor. The floor surface is a perfectly horizontal line at 64% down from top, lower 36% filled with olive-brown earth, scattered subtle small moss stones and leaf texture, so feet can stand on it. Main trunks stand behind the fighters and extend upward from this floor. Leave wide transparent gaps between trees in middle 25–64% down from top to see the distant forest. Ground tile must meet both horizontal edges at the identical 64% height. Do NOT draw a sky or hazy background between trunks.
```

### forest-near

```text
Use case: stylized-concept
Asset type: production 2D side-view game background tile for a Unity pixel-art sword duel.
Input images: Image 1 is ONLY a pixel-art style reference, not an edit target. Do not retain its pink palette, school, gate, buildings or paving.
Primary request: a very wide horizontal green woodland path background, as one layer of a 3-layer parallax set.
Style/medium: clean handcrafted low-resolution pixel art matching the reference's hard square pixel clusters, restrained shading and readable silhouettes. Imagine a 768x256 pixel tile enlarged with nearest-neighbor pixels. No smooth painting, no 3D, no blur or antialiasing.
Composition: 3:1 horizontal canvas (3072x1024). Flat side-on forest path, NOT a path receding to a central vanishing point. Horizontally seamless repeat: left and right edge terrain/foliage meet at exactly the same heights and color bands. No central focal object. Uniform consistent pixel scale.
Palette: quiet emerald, moss green, deep blue-green shadow, muted mint haze, olive earth. Calm neutral-green daylight. No pink, purple, intense neon, orange autumn or huge white glare.
Constraints: no people, creatures, swords, UI, text, labels, frames, watermark. Full-bleed tile, no border, no empty canvas margin. Layer: NEAR, genuinely transparent PNG background, preserve alpha (do not paint a checkerboard). A low foreground strip of deep blue-green ferns, little mossy roots and grass tufts occupying ONLY bottom 18% of canvas, tallest leaf tips no higher than 80% down from top. Top 80% must be completely transparent with NO canopy, trees, ground, sky or background scene. Fern strip continues horizontally to both edges and is horizontally seamlessly tileable. Same crisp block-pixel scale as other layers, more contrast than the distant layers but no black wall and no tall plants obscuring fighters.
```
