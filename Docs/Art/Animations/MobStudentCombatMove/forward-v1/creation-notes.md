# 모브 캐릭터 하반신 전진 보법 v1

## 결과

오른쪽을 바라보는 전투 자세에서 앞발을 먼저 짧게 내딛고 뒷발이 따라오는 6프레임 이동 시안이다. 다리를 교차하지 않고 앞발의 선행 위치를 유지한다. 각 프레임 150ms, 총 900ms 반복이다.

허리와 치마는 기존 그림을 동일하게 재사용하고 다리만 움직인다. 같은 상반신을 얹은 확인용 영상에서는 머리·몸통·양손·검이 고정된다. 나중에 상반신 공격 프레임을 교체해도 하반신 이동 시트를 별도로 재생할 수 있도록 캔버스와 허리 연결점을 맞췄다.

그림 생성과 투명 배경 수정에는 내장 image_gen 기본 도구를 사용했다. 생성된 다리를 같은 비율로 한 번 정규화하고, 접지점에 맞춰 다리 레이어를 배치한 뒤 기존 치마와 상반신을 조립했다.

## 파일

- `lower-body-move-preview.gif`: 하반신 단독 미리보기, 400×232.
- `combined-move-preview.gif`: 동일한 상반신을 붙인 확인용 미리보기, 800×704.
- `lower-body-move-6frames.png`: 하반신 시트. 왼쪽부터 1–6번, 4800×704.
- `combined-move-6frames.png`: 확인용 전체 몸 시트.
- `lower-1.png`–`lower-6.png`: 독립 재생할 하반신 프레임, 각 800×704.
- `upper-body-fixed.png`: 연결 확인에 사용한 동일한 상반신 그림.
- `waist-and-skirt-fixed.png`: 재사용한 허리·치마.
- `movement-profile.json`: 공통 피벗, 허리 위치, 접지 구간, 이동량.
- `frame-info.json`: 정렬 및 검증 기록.
- `generated-lower-6frames-3x2.png`: 도구의 최종 원본 시트.

## 동작

| 프레임 | 하반신 | 뒷발 접지 | 앞발 접지 |
|---|---|---|---|
| 1 | 기본 경계 자세 | 예 | 예 |
| 2 | 앞발을 낮게 들어 전진 | 예 | 아니오 |
| 3 | 앞발 착지, 넓어진 자세 | 예 | 예 |
| 4 | 앞발로 지지하고 뒷발 회수 | 아니오 | 예 |
| 5 | 뒷발 착지 | 예 | 예 |
| 6 | 자세를 정리해 다음 보폭에 연결 | 예 | 예 |

보법은 [Higgins Armory 교본 18쪽](https://www.annarborsword.com/PDF/Higgins_Museum_longsword_handout.pdf)의 sliding step과 [Gem City HEMA의 advance 설명](https://www.gemcityhema.com/basic-longsword-vocabluary)을 참고했다. 허리 높이를 고정한 것은 상하반신 합성을 위한 표현이다.

## 상하반신 연결

모든 게임용 PNG의 캔버스는 800×704다. 아래 좌표는 이미지 왼쪽 위를 (0,0)으로 한다.

- 허리 연결점: (306,464).
- 지면: y=640.
- 공통 기준점: (320,640).
- Unity 방식의 아래쪽 기준 정규화 피벗: (0.4, 0.090909).
- 하반신 위에 상반신을 같은 캔버스 위치로 겹친다. 상반신이 y=464–471 구간을 덮어 연결 부위를 가린다.
- 확인용 상반신은 기존 `MobStudentIdle/v3-weapon-locked/idle-1.png`에서 나눴다.

움직임 동안 허리 연결 띠의 픽셀과 확인용 상반신 픽셀이 동일한 것을 확인했다. 허리 틈, 다리 교차, 무릎·발목 단절은 시각 확인에서 발견하지 않았다.

## 이동량과 접지

미리보기는 제자리 반복이다. 실제 전진에 사용할 이동 정보는 `movement-profile.json`에 있다.

현재 확대된 텍스처 좌표에서 프레임 시작 이동량은 [0,4,8,12,16,20], 다음 주기의 시작은 24다. 매 반복마다 24를 누적한다. 끝에서 캐릭터 이동량을 0으로 돌리면 안 된다. 게임 좌표에는 사용하는 Pixels Per Unit에 맞춰 변환한다.

이 이동량을 더했을 때 프레임 경계의 접지 기준점은 뒷발이 1–3번에서 x=207, 5–6번에서 x=231이고, 앞발이 3–6번에서 x=433으로 유지된다. 다음 반복의 1번도 뒷발 231·앞발 433으로 이어진다. 기준점은 신발의 바닥 부근 윤곽 중심이며, 별도 물리 IK를 적용한 결과는 아니다.

현재 결과는 애니메이션 그림과 연결·이동 자료이며, 게임 실행 코드에는 아직 연결하지 않았다.

## 생성 프롬프트

```text
Use case: precise-object-edit
Asset type: SIX-frame LOWER-BODY-ONLY combat locomotion sprite sheet for independent upper/lower animation.
Input images: Image 1 is the approved complete character, the exact identity/proportion/style reference. Image 2 is the exact cropped LOWER BODY and authoritative skirt/legs/socks/shoes reference. Keep that character. Do not redesign.
Primary request: Create a RIGHT-FACING forward combat advance with the same lead foot throughout. The front (screen-right) foot advances first, then the rear (screen-left) foot gathers. This is a controlled low sword-fighting sliding step, NOT alternating casual walking or running. Legs NEVER cross, front foot remains on screen-right of rear foot in every frame. Mildly bent knees, low lift, stable balance.
Show only the skirt/hips and legs below the waist, with the same chocolate brown pleated skirt, muted gold hem, bare knees/thigh portions, dark knee socks and brown loafers. NO torso, blazer, head, hair, arms, hands, sword or upper body. The horizontal cut at the top is intentional for modular sprite compositing. Full soles visible.
Layout: ONE transparent sprite sheet, exactly 3 equal columns × 2 equal rows, SIX complete lower-body poses. Row-major order: 1,2,3 across top; 4,5,6 across bottom. Every cell must have the exact same scale, canvas/padding, waist-center X and waist-seam Y, and common ground baseline. No numbered labels, text, lines, drawn grid, border, floor, shadow, checkerboard or watermark.
CRITICAL modular attachment: the waist TOP CUT is perfectly flat and the same width/shape/local pixel position in every cell. Freeze the uppermost 2 logical pixel rows of the skirt/hip connector. Waist center, hip size, pelvis orientation and height stay fixed. Only skirt hem/pleats, knees and feet may animate below that seam. No whole-body vertical bob, pelvis drifting, changing leg lengths, or scaling between frames. Upper attack animation will later attach at this fixed waist.
Motion is an IN-PLACE forward stepping cycle: support feet move backward relative to the fixed waist while grounded, so the game can translate the character root forward. Ground contact phases matter; never just wiggle two permanently planted shoes.
Choreography with approximate design coordinates in each nominal 400×240 cell (these are guidance, do not draw coordinates): fixed waist-center (200,24), ground sole y=200. Use the same original body scale as Image 2:
Frame 1: neutral staggered guard, rear shoe center x=78, lead shoe center x=288, both soles down.
Frame 2: rear foot supports/moves backward to x=68; lead shoe moves forward to x=318 with toe/sole only 5–8 pixels above ground. Knees keep balance.
Frame 3: lead heel/sole lands at x=328; rear support shoe x=58, both briefly on ground; wider stance. Lead knee accepts weight.
Frame 4: lead foot supports at x=318; rear shoe gathers forward to x=80, lifted only 5–8 pixels, never crossing the lead leg.
Frame 5: rear shoe lands at x=98; lead support at x=308, balanced stance restored.
Frame 6: both grounded, rear x=88 and lead x=298; knees settle softly. Then next frame 1 follows with support shoes another 10 pixels backward relative to waist. A 60-pixel forward root translation per 6-frame loop makes the foot contacts coherent.
Motion must be visibly six different stepping phases with ankle/knee articulation, not six nearly identical idle stances. Do not substitute another character, change shoes between frames, add soles, add feet, or turn the hips away from the original.
Rendering: preserve the reference's compact stylized game body ratios and coarse crisp stepped pixel-art clusters, identical palette, outline weight, sock lengths, shoe dimensions and lighting. Skirt may have restrained secondary hem movement of one logical pixel, but top connector stays fixed. No detailed smooth illustration, gradients, blur, painterly marks or finer pixel resolution than reference.
Background: genuine alpha transparency.
Output ONE clean 3×2 lower-body-only locomotion sprite sheet, ready to pair with a separately animated fixed upper-body attachment.
```

## 투명 배경 수정 프롬프트

```text
Use case: background-extraction
Edit the provided SIX-frame lower-body sprite sheet. Remove ONLY the entire painted dark brown/black background and ALL soft halos, brown glow, fog and shadows around and between the sprites. Replace that whole backdrop with genuinely transparent alpha (alpha=0), including the empty spaces between the legs and above the waist cut.
Preserve all six actual lower-body sprites EXACTLY: same row-major 3x2 positions, scale, waist cut, skirt, gold hem/buttons, skin, dark socks and brown loafers, stepped pixel outlines, all existing footstep phases. Do NOT redraw, alter poses, rescale, smooth, recolor or remove the dark pixel outlines of the character. Keep dark socks and dark shoes fully intact. The soft luminous brown halo is BACKGROUND, not part of the character, and must be removed completely.
Output the SAME 3x2 sprite sheet with the SAME six lower-body drawings on true alpha transparency. No new painted backdrop, black canvas, checkerboard, floor, glow, shadow, text or watermark.
```

