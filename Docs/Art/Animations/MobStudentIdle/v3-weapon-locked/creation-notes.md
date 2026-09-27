# 모브 캐릭터 대기 모션 — 무기 고정 수정본

## 수정 결과

검과 양손·흰 커프스를 하나의 고정된 전경 레이어로 분리했다. 네 프레임 모두 같은 이미지 데이터를 재사용하며, 프레임별 확대·축소나 회전 없이 호흡에 따라 위아래로만 이동한다. 칼날 길이·폭·각도·검끝·가드·손잡이의 형상이 고정된다.

그림의 분리와 제거는 내장 image_gen 도구로 수행했다. 이후 생성된 몸 레이어와 단일 무기/손 레이어를 애니메이션 파일로 조립했다. 전경 레이어는 몸의 기존 크기에 맞춰 최초에 한 번만 크기를 정했다.

## 파일과 재생

- `mob-student-idle-preview.gif`: 투명 배경, 4프레임, 4fps, 1초 무한 반복.
- `mob-student-idle-4frames.png`: 왼쪽부터 1–4번, 3200×704.
- `idle-1.png`–`idle-4.png`: 각 800×704.
- `weapon-and-hands.png`: 네 프레임이 공유하는 동일한 400×214 전경 레이어.
- `layers/generated-weapon-and-hands.png`: 도구가 분리한 원본 전경.
- `layers/generated-body-2x2.png`: 도구가 생성한 몸 레이어 원본.
- `layers/body-1.png`–`layers/body-4.png`: 발바닥을 맞춘 몸 레이어.
- `frame-info.json`: 레이어 크기, 위치, 해시, 픽셀 검증 결과.

전경 레이어 위치는 (319,257), (319,252), (319,254), (319,263)이다. 발바닥 기준선은 y=640으로 유지된다.

## 확인

각 PNG 프레임에서 노출된 칼날 7,656픽셀이 위치 이동을 제외하면 RGBA까지 동일한 것을 확인했다. 최종 GIF를 다시 읽어 나머지 세 프레임과 총 22,968픽셀을 비교했으며 모두 일치했다. 양손의 손바닥/손등 구분과 손목·커프스 연결도 네 장 모두 시각 확인했다.

이 폴더의 파일이 최종 수정본이며, 게임에 연결하는 작업은 포함하지 않는다.

## 최종 생성 프롬프트 1 — 무기와 양손 분리

```text
Use case: background-extraction
Asset type: Transparent fixed foreground layer for an existing pixel-art idle animation.
Input image: edit target and exact authoritative drawing, not inspiration.
Primary request: Extract ONLY the complete sword and the TWO CLOSED HANDS plus their WHITE CUFFS from this exact image onto genuine transparent alpha. Remove the rest of the character entirely: no head, hair, torso, ribbon, blazer sleeves, skirt, legs or shoes. Retain the steel blade, gold crossguard, visible dark handle and pommel, the palm-facing front hand, the dorsal rear hand, both white cuffs and their dark outlines. Preserve these extracted pixels' relative positions, scale, colors and precise pose. Do not redraw or redesign the sword or hands. It should look like this exact foreground grip group was cleanly lifted from the reference.
Composition: Keep the original canvas aspect ratio and original locations if possible, with the same transparent padding. The blade points diagonally up/right exactly as in the source. Keep all blade and hilt dimensions identical, not shortened or enlarged. Both hands remain around the hilt exactly as before. Ends of cuffs are clean attachment seams to sleeves that will exist on a separate body layer.
Pixel rendering: preserve the source coarse stepped pixel cluster style and outlines. Do not add tiny detail, sharpen into a new style, smooth, blur or antialias. No imaginary sleeve remnants or patches from the removed body.
Output: ONE transparent layer containing only the sword, both gripping hands and two white cuffs. No text, background, diagram, labels, extra weapon or frame.
```

## 최종 생성 프롬프트 2 — 몸 프레임 분리

```text
Use case: precise-object-edit
Asset type: Four-frame transparent BODY layer for an existing pixel-art animation.
Input image: existing four-frame idle sheet, edit target. Preserve exact body drawings and their four subtly different breathing phases.
Primary request: Remove only the sword (steel blade, brass crossguard, dark handle and pommel), both hands, and their white cuffs from ALL FOUR characters. Make these removed areas truly transparent, with clean sleeve-end seams. Keep the brown blazer sleeves up to the wrists so a separate foreground layer containing cuffs, hands and sword can later reconnect to them.
Do not replace the sword with another object. Do not change the arm pose, extend the sleeves or move wrists. Empty transparent gaps where the cuffs/hands/weapon used to be are intentional animation layer seams. Preserve the existing face hidden by bangs, hair, headband, brown gold uniform, ribbon, skirt, legs, socks, shoes, compact proportions, palette, lighting, coarse pixel density and outlines. Preserve each existing body's slight breathing motion and grounded foot positions. Avoid regenerating/reinterpreting the body.
Layout: Keep the same 2x2 four equal-cell layout, row-major frames 1–4, same size and spacing. Full body visible in each cell. Same transparent padding and foot coordinates as reference. No labels, text, border, grid, ground shadow, backdrop or watermark.
Background: genuinely transparent alpha throughout, no painted checkerboard.
Output ONE transparent 2x2 sprite sheet containing four body-only breathing frames with the sword/hands/cuffs removed, ready to combine with one shared foreground grip layer.
```

