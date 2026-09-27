# 모브 캐릭터 대기 모션 v1

- 기준 이미지: `../../../Concepts/mob-student-pixel-design-v3-grip.png`
- 제작 방식: 내장 image_gen 도구로 기준 이미지를 참조하여 4프레임 생성.
- 재생: 1 → 2 → 3 → 4 → 1, 프레임당 250ms, 4fps, 1초 무한 반복.
- `mob-student-idle-preview.gif`: 투명 배경 애니메이션 미리보기.
- `mob-student-idle-4frames.png`: 왼쪽부터 1–4번 프레임, 3200×704 PNG.
- `idle-1.png`부터 `idle-4.png`: 각 800×704 PNG.
- `generated-source-2x2.png`: 생성 도구의 원본 시트.

## 정렬 및 확인

생성된 그림을 수정하거나 확대·축소하지 않고 각 프레임을 분리하여 같은 크기의 투명 캔버스에 배치했다. 발바닥의 바닥선을 모든 프레임에서 y=640으로 맞췄으며 그림이 잘리지 않았음을 확인했다. 몸이 조금 올라갔다 내려오는 호흡과 머리카락·옷의 작은 변화를 표현한다. 두 손의 파지와 앞손의 손바닥, 뒷손의 손등을 유지한다.

이 버전은 첫 애니메이션 시안이다. 프레임마다 검끝, 머리카락, 신발의 세부 픽셀이 조금 달라 약한 흔들림이 남는다. PNG는 확대된 도트 표현의 시안이며, 실제 게임 해상도에 맞춘 픽셀 정리와 Unity 연결은 아직 수행하지 않았다.

## 최종 생성 프롬프트

```text
Use case: precise-object-edit
Asset type: ONE four-frame pixel-art idle animation sprite sheet, 2 columns × 2 rows.
Primary request: Animate the exact sword-holding mob character in the reference into a subtle seamless breathing idle. Four frames total, ready for frame-by-frame preview. The reference is the approved identity, proportions, costume, sword, stance and hand-grip design, not inspiration for a redesign.
Layout: A clean 2×2 sheet with FOUR IDENTICAL rectangular cells, equal width/height and identical transparent padding. Exactly one complete full-body sword-holding sprite per cell. Top-left frame 1, top-right frame 2, bottom-left frame 3, bottom-right frame 4. NO drawn grid, guides, frame borders, text, numbers or labels. Entire shoes and sword tip visible in every cell. Give the long forward-pointing sword enough margin in each cell, particularly at right.
Motion choreography: frame 1 neutral resting guard; frame 2 gentle inhale, upper torso/head rises ONE logical pixel; frame 3 inhale crest, upper torso/head rises TWO logical pixels from neutral, subtly fuller chest; frame 4 gentle exhale, torso returns ONE logical pixel above neutral, with hair tips and bow settling by one logical pixel. Then frame 4 loops seamlessly to frame 1. Motion is very small, soft breathing, suitable for 4 fps and a 1-second cycle.
Alignment and continuity are critical: the exact same two shoes are planted at exactly the same local pixel coordinates/baseline in ALL four cells. Legs keep the same staggered bent-knee guard with only minute compression needed for breathing. Character scale, facing, width, head size, eye-covering fringe shape, costume lengths, outline thickness, palette and lighting remain identical. No drifting along x, sliding feet, torso leaning, jumping, step, attack, strike, blinking or visible eyes.
Weapon/hand coherence: both hands, hilt, brass guard and straight steel blade form one rigid group that follows the breathing chest by the same tiny vertical translation; no independent sword wobble or rotation, no blade bending, no length or thickness changes. Every frame keeps the SAME authentic hand orientation from the reference: guard-side screen-right hand shows PALM/curled finger pads around the handle; pommel-side screen-left hand shows the BACK/knuckles. Preserve the closed grip and exact thumb/finger contact, and the short visible handle between hands. No hand swapping, hand shape regeneration, open hands or changes to wrist angle.
Identity: beige-blonde bob, eyes COMPLETELY covered by straight bangs, dark brown headband, tiny neutral mouth, chocolate brown blazer and pleated skirt, muted gold piping/buttons/hem and large ribbon, white collar/cuffs, dark brown knee socks and loafers. Same compact approximately 2.8–3-head game proportions as reference.
Pixel rendering: authentic crisp low-resolution coarse square pixel lattice, stepped edges, hard limited-palette colour clusters, same pixel density and outline as reference. Each sprite looks like the same drawing moved locally by 1–2 logical pixels, NOT four new illustrations. Clean enlarged nearest-neighbour-like pixel preview. No anti-aliasing, gradients, blur, painterly texture or extra high-resolution detail.
Background: genuine full alpha transparency across the entire sheet, no painted checkerboard, environment, ground shadow, UI, comparison image or watermark.
Output: ONE evenly spaced transparent 2×2 sprite sheet with exactly four coherent consecutive idle frames.
```

