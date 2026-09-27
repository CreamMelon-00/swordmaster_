# 중경 픽셀 아트 정리와 원경 추가 축소

## 변경 범위

- 내장 이미지 생성 도구의 편집 모드로 `Assets/Game/Resources/ForestArena/forest-belt-mid.png`를 수정했다. 자잘한 흙·이끼 질감을 연결된 색 덩어리로 단순화하고 나무/돌/식물 윤곽을 선명한 계단형 도트로 정리했다. 바닥 폭, 얕은 원근, 뒤쪽 경계 약47%, 주요 나무 위치와 투명 구멍은 유지했다.
- 결과는 2172×724 PNG이며 기존 파일 경로·GUID·임포트 설정을 유지한다. 이전 그림은 `Sources/forest-belt-mid-depth-v1.png`에 보관했다. 초기 벨트 구성은 `BeltForest.md`를 참고한다.
- 원경은 높이16→14.4, 즉 직전 표시 크기의90%로 추가 축소한다. 첫 높이18 대비80%다. 중심Y=-0.5와 패럴랙스0.24는 동일하다.
- 중경 높이12.6/Y=-1.2, 전경 높이12.6/Y=-0.75는 그대로다. 카메라·배우·그림자·큐·ACT·피해·교전 이동·HUD·씬·설정은 수정하지 않았다.
- 생성에서 4×4 도트/제한 팔레트를 지시했지만 AI 결과의 완전한 고정 격자·색상 개수를 보장하지 않는다. 실제 알파를 보존했으며 표본상 나무 사이에 완전 투명 픽셀이 존재한다. 바닥 알파는 약245~253으로 거의 불투명하지만 모두255인 결과는 아니다. 육안으로 레이어 합성 결과를 확인한다.
- 32:9/size3.5/17도 원경 세로 여유는 약0.215씩이다. 임의20도 회전·극단적 충격흔들림·모든 비율을 보장하지 않는다. 관련 검사와 화면 기록은 `../Validation.md`를 따른다.

## 실제 편집 프롬프트

Image1은 이전 `forest-belt-mid.png` 편집 대상, Image2는 기존 `forest-near.png`의 식물 도트 표현 참고다. 내장 도구를 사용했으며 CLI/API 모드가 아니다.

```text
+Use case: style-transfer
Asset type: production transparent PNG middle parallax layer for an existing 2.5D belt-scroll sword-duel game.
Input images: Image 1 is the EDIT TARGET, the current forest-belt-mid.png. Image 2 is a SUPPORTING STYLE REFERENCE for readable crisp plant silhouettes and limited-color pixel clusters only; do not copy its foreground-only composition.
Primary request: restyle ONLY Image 1 into visibly cleaner hand-pixelled retro game art. Keep its composition, broad shallow top-down floor, tree positions, 3:1 aspect ratio, 2172 x 724 canvas, muted green/olive palette and all transparent openings.
Style/medium: deliberate low-resolution pixel art on a consistent virtual 543 x 181 pixel grid enlarged 4x with hard nearest-neighbor edges. Clearly visible square pixel steps, coherent 4x4 pixel blocks, flat connected color clusters, restrained roughly 24-32 colors, 3-4 tones per material, clean stepped trunk/leaf/stone silhouettes. Simplify the mottled dirt and noisy moss into larger readable patches. No smooth gradients, antialiasing, painterly smearing, blur, subpixel microtexture, or random tiny speckling.
Composition invariants: rear tree-base/floor boundary stays horizontally at about 47% of canvas height; the entire lower 53% stays a wide walkable floor seen from a shallow elevated camera. Preserve belt-scroll depth and compressed smaller stones toward the back, larger flatter stones toward the front. Keep the middle fighting band open. Do not turn it into a flat side-on platform ledge or a road vanishing into the distance. Keep main trunks where they are, no new foreground obstacles or props.
Transparency: preserve genuine PNG alpha transparency in every opening between the trees above the floor. Do not fill it with sky, black, a checkerboard or a far forest; this layer will overlay a separate far forest. Keep the floor fully opaque to the lower and left/right edges.
Loop/invariants: preserve the same floor-boundary height, horizontal traversability and edge palette at both sides. No characters, enemies, UI, text, symbols, borders or watermark. Same scene and geometry; the sole visual change is crisper, simpler and more consistently pixel-art rendering.
```
