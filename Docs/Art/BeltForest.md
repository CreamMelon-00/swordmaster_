# 벨트스크롤 숲길 개편

이 문서는 초기 벨트스크롤 개편 기록이다. 최신 중경 픽셀 아트 편집과 원경 추가 축소는 `PixelBeltForest.md`를 따른다.

## 의도와 범위

평면적인 옆모습 지면을, 살짝 위에서 내려다보는 넓은 숲길 바닥으로 교체했다. 원본 교문 배경의 앞뒤 깊이감을 참고하지만 실제 3D 메시·깊이 이동·스텝 판정은 추가하지 않는다. 기존 캐릭터 크기/애니메이션/그림자/큐 전투/피해/카메라/HUD와 지속 교전 이동은 그대로다.

## 자산과 배치

- 내장 이미지 생성 도구 사용. 새 최종 자산은 `Assets/Game/Resources/ForestArena/forest-belt-mid.png`(2172×724, 실제 alpha PNG)다. 이전 `forest-mid.png`와 교문 PNG는 수정하지 않았다.
- 기존 `forest-far.png`, `forest-near.png`를 원경·전경으로 재사용한다. Point / Sprite Single / Tight Mesh / PPU128 / Clamp / 최대4096 / mipmap·압축 없음. 불투명 바닥이 좌우·하단 끝까지 닿아 타일의 전체 폭/높이를 유지한다.
- 원경: 높이16, 중심Y=-0.5, 패럴랙스0.24, sorting=-6. 기존18의88.9%로 약11% 축소한다. 원경은 카메라Y에 붙이지 않고 월드에 고정하여 확대/흔들림에도 무대와 분리되지 않게 한다.
- 중경: 높이12.6, 중심Y=-1.2, 패럴랙스1.0, sorting=-5. 기존18의70%로30% 축소. 바닥 뒤쪽 경계는 그림 높이 약47%(worldY≈-0.82), 앞쪽은worldY=-7.5이며 발/그림자worldY≈-2.73은 그 안쪽에 놓인다. 바닥의 돌·나무 그림자·전후 크기 차이로 얕은 원근감을 준다.
- 전경: 높이12.6, 중심Y=-0.75, 패럴랙스1.35, sorting=1. 낮은 식생이worldY≈-3.9부터 앞쪽에 보인다. 캐릭터 다리를 덮는 큰 풀밭 대신 하단 깊이 장식이다.
- 배경만 줄이고 카메라를 확대 축소하거나 배우를 작게 만들지는 않았다. AI 이미지의 도트 격자 일관성을 완전히 복원하는 작업은 아니며, 작은 표시로 거친 클러스터가 덜 두드러지게 하는 조정이다.
- 수평 반복은 기존 절대 인덱스 교대 반전과 재사용 풀을 유지한다. 무한 좌우 이동 중 경계가 이어진다. 실제카메라의 원경 세로coverage와 화면 검수는 `../Validation.md`를 참고한다. 극단적인 확대/회전/흔들림/모든비율을 보장하지 않는다.

## 실제 최종 프롬프트

Image1은 이전 `forest-mid.png`(녹색팔레트/도트매체 참고), Image2는 원본교문PNG(시점/지면깊이 참고)다. 둘 다 편집 대상이 아니라 새 숲길 그림의 참고 이미지다.

```text
Use case: stylized-concept
Asset type: production PNG middle parallax layer for a 2.5D belt-scrolling pixel-art sword-duel arena.
Input images: Image 1 is a reference ONLY for green forest palette and pixel medium. Image 2 is a reference ONLY for slightly elevated camera angle and the visible DEPTH of its ground plane. Neither image is an edit target. Create a new forest asset, no school gate, no pink.
Primary request: a long horizontal emerald-green forest trail, viewed at a shallow elevated beat-em-up camera angle, with a genuinely BROAD walkable ground plane extending from back to front. NOT a flat side-on platformer ledge. The duel sprites will stand IN THE MIDDLE of the floor, not on its back edge.
Composition: very wide 3:1 canvas. A horizontal rear boundary at 47% down from the top: small wooded tree bases, low moss banks and distant stones. From 47% down to the bottom is a large uninterrupted dirt-and-moss forest FLOOR seen from above at about 25 degrees, readable depth like a classic 2.5D belt-scroll brawler. Sparse flat stones and soft diagonally slanted tree shadows are finer/compressed at the back and a little larger toward the viewer. Clear unobstructed fighting band at 60–82% height. No front-facing cliff face, grass platform wall, vertical terrain cross section or stage steps.
Upper 47%: medium-distance slim tree trunks and restrained canopy mainly along top, with large truly transparent gaps between trees to see a separate far forest layer. Trees start at the REAR boundary of the floor, not by the fighters' feet. No enormous close trunks blocking center. Top-middle gaps MUST have genuine PNG alpha transparency, not a painted checkerboard, black or solid sky.
Style/medium: restrained crisp retro pixel-art clusters; coherent small dot scale; minimal irregular AI microtexture. Use a finer dot pattern than Image 1. Flat color clusters, small controlled palette. Not a watercolor, smooth illustration, fake pixels on blurred paint, 3D render or diagonal isometric diamond map.
Colors/lighting: quiet moss and muted emerald greens, neutral olive-brown dirt floor, deep blue-green shade, mint light from the back; never pink/purple, neon saturation or huge glare.
Loop: continuous horizontally traversable forest lane; both left and right image edges have the exact same rear boundary height, ground depth bands and palette. No singular central vanishing point or road leading away into the distance. Use gentle local perspective/foreshortening within the strip while the trail extends sideways infinitely.
Constraints: no characters, enemies, weapons, UI, labels, text, watermark, border or panels. Full-bleed landscape. Preserve open floor for the characters and alpha between trees. Make the floor's back-to-front depth immediately unmistakable.
```
