# 모브 캐릭터 하반신 전진 보법 v2

## 결과와 변경점

오른쪽을 바라보며 오른쪽으로 이동하는 전투 보법 시안이다. 앞발은 화면 오른쪽을 향하고, 뒷발은 오른쪽·화면 앞쪽 대각선을 향한다. 무릎과 발끝이 같은 방향을 따르고, 다리를 교차하지 않는다.

v1에서 다리를 따로 이동하고 허벅지 윗부분을 잘라 조립했던 방법을 교체했다. 이번에는 골반·치마·양쪽 허벅지·종아리·신발이 이어진 하반신 그림 전체를 사용한다. 허리 위의 의상만 상반신에 가려지고 다리와 발은 캔버스에 모두 들어온다.

8프레임, 프레임당 140ms, 총 1.12초의 제자리 반복이다. 고유 자세는 6개이며 마지막 두 프레임에서 기존 착지 자세와 기본 자세를 재사용한다. 8번과 1번은 같은 그림이라 반복 경계에서 접지한 발이 갑자기 바뀌지 않는다. 확인용 상반신·양손·검은 기존 v3 그림을 고정 재사용했다.

그림 생성과 수정에는 내장 image_gen 도구를 사용했다. 프레임 추출·최근접 크기 정규화·상하반신 레이어 합성·GIF 변환은 별도 파일 처리로 수행했다. 양쪽 다리를 서로 분리하거나 개별 이동하지 않았다.

## 파일

- lower-body-move-preview.gif: 하반신 단독 미리보기, 432×244.
- combined-move-preview.gif: 동일 상반신을 붙인 미리보기, 800×704.
- lower-body-move-8frames.png: 하반신 8프레임 가로 시트, 6400×704.
- combined-move-8frames.png: 전체 몸 확인 시트, 6400×704.
- lower-1.png–lower-8.png: 독립 하반신 PNG, 각 800×704.
- combined-1.png–combined-8.png: 확인용 합성 PNG.
- upper-body-fixed.png: 고정 상반신과 검.
- generated-connected-lower-4x2.png: 선택한 하반신 원본 시트.
- generated-recovery-frame-6.png: 자세와 발 방향을 유지하며 도트 질감을 맞춘 회수 자세 원본.
- frame-info.json: 좌표, 프레임 순서, 알파·반복 검사 기록.
- movement-profile.json: 방향 및 동작 단계. 전진 루트 값은 미확정.

## 동작

| 프레임 | 동작 |
|---|---|
| 1 | 기본 경계 자세 |
| 2 | 앞발 뒤꿈치를 풀고 출발 준비 |
| 3 | 앞발을 낮게 들어 내딛기 |
| 4 | 앞발 착지 |
| 5 | 앞발로 지지하고 뒷발 뒤꿈치 들기 |
| 6 | 뒷무릎을 굽혀 뒷발 회수 |
| 7 | 기존 착지 자세를 재사용해 접지 |
| 8 | 기본 자세를 재사용해 정리 |

## 연결 기준과 확인 범위

이미지 좌표는 왼쪽 위가 (0,0)이다. 캔버스는 800×704, 허리 연결 기준은 (306,456), 지면은 y=640, 공통 피벗은 (320,640)이다. 아래쪽 원점의 정규화 피벗은 (0.4,0.090909)이다. 상반신을 같은 캔버스 위치에 겹친다. v1의 허리 기준 y=464와 다르므로 해당 상반신 절단본을 섞어 사용하지 않는다.

8프레임 GIF의 알파·140ms 지연·무한 반복, 상반신의 고정 픽셀, 다리와 신발의 캔버스 여백, 8→1 동일 그림을 확인했다. 그림 검토에서 기존의 잘린 허벅지와 큰 발목 방향 전환은 해소됐다.

이 파일은 방향과 관절 동작을 검토하는 시안이다. 6번에서 앞신발이 주변 자세보다 약 6–8px 움직이는 잔여 변화가 있고, 뒷신발 크기도 완전히 동일하지 않다. 실제 전진 속도와 세계 좌표의 접지는 아직 맞추지 않았다. v1의 프레임별 24px 이동 자료를 재사용하지 않는다. 상반신 공격과 게임 이동에 연결할 때 발의 접지 구간에 맞춰 전진량을 조정해야 한다.

## 생성 프롬프트

아래는 최종 그림으로 이어진 생성·수정 프롬프트다.

### 연결된 하반신 8자세 생성

```text
Use case: precise-object-edit
Asset type: Corrected EIGHT-frame pixel-art LOWER-BODY combat advance sprite sheet, 4 columns by 2 rows.
Input images: Image 1 is the approved full character for exact body proportions, camera/facing, outfit, colours and pixel style. Image 2 shows her complete hip/skirt/legs/shoes. These are appearance references. The old rear shoe's pointing direction must be corrected as described below.
Primary request: Redraw a natural small sword-fighting advance to SCREEN-RIGHT. Draw hips, pleated skirt, BOTH COMPLETE THIGHS, knees, calves, socks, ankles and shoes together as ONE anatomically connected lower body in each frame. This must fix the previously chopped thigh attachment and unnatural foot flipping.
FACING / TRAVEL / FOOT DIRECTION LOCK:
The upper body faces three-quarter RIGHT and travels RIGHT; the lower body matches it. The screen-right leg is ALWAYS the leading leg. Its loafer toe points RIGHT (+X), heel on the left. The screen-left leg is ALWAYS the rear leg. Its toe points diagonally RIGHT with a small toward-camera component, consistent with a rear-foot angle about 30–45 degrees to the forward line. It must NEVER point left/backward. Both knees bend in the direction of their own toes. Preserve these two specific shoe orientations through all EIGHT frames. Lifting a foot permits a small ankle roll, NOT turning the shoe to face the camera, spinning it down vertically, mirroring it or changing its length.
ADVANCE:
Lead foot moves first, rear foot follows, feet do NOT cross or exchange lead/rear roles. Short stride, bent but comfortable knees, low 1–2 logical-pixel shoe clearance, calm weight transfer. Foot shape/width/length and the two leg lengths stay constant; joints articulate rather than a whole leg sliding sideways from its hip. No extreme squat, knee kink, backward-facing knee, exaggerated lift or running.
Eight continuous keyframes:
1 stable guard, both soles down;
2 rear leg supports, lead heel begins to release slightly;
3 lead foot moves a short distance right, just off the floor, toe still right;
4 lead heel-to-sole lands, leading knee accepts weight;
5 lead foot supports, rear heel gently releases, rear toe still diagonally right;
6 rear foot gathers low along the floor toward the lead foot without crossing it, no huge ankle bend;
7 rear heel/sole settles, original balanced staggered spacing returns;
8 soft knee settling and support-foot backward movement relative to the waist, ready for a seamless 8-to-1 loop.
This is an in-place locomotion clip intended to accompany forward character-root travel. Grounded support feet shift subtly back relative to the fixed waist, never drift forward while planted. Keep stance changes small and smooth; include true intermediate joint positions, not sudden pose swaps.
MODULAR WAIST:
Keep the same flat waist cut, top width, hip orientation and local waist-centre position in every cell so a separately animated upper body can attach. Freeze only the uppermost connector rows. Let skirt pleats/hem respond subtly to leg motion. Draw the complete tops of thighs emerging naturally from beneath the skirt; include generous hidden thigh/hip continuation behind it. No horizontal cut across thigh skin, rectangular skin patches, stepped notches, missing skin, white/transparent thigh gaps or separate floating limbs. The ONLY intended cut is at the WAIST, above the skirt.
IDENTITY:
Same compact stylized proportions as input, brown pleated school skirt with muted gold hem, exposed skin between skirt and dark knee socks, brown loafers. Keep the reference skirt-to-leg length ratio and coarse square stepped pixel clusters/outline/limited palette; no smoother illustration or more realistic adult long legs.
LAYOUT:
ONE 4x2 sheet, exactly eight equal-size cells, top row1–4 then bottom row5–8. Full lower body in every cell, same drawing scale, same waist-centre/cut height, shared floor baseline, generous padding around ALL shoes including moving toes. No cropped shoes, chopped knees or thigh clipping. True alpha transparency, NO glow, shadow, floor, background, labels, frame numbers, borders or grid.
Output one coherent eight-frame lower-body-only sprite sheet. NO head, torso, arms, hands or sword in the artwork.
```

### 발 방향과 회수 자세 수정

```text
Use case: precise-object-edit
Asset type: Targeted foot-direction continuity fix of the provided EIGHT-frame lower-body animation.
Input images: Image 1 is the EDIT TARGET, an8frame4x2sheet. Image2 is a RIGHT-FACING loafer showing a clear heel-left / toe-right shape, used ONLY as a direction and footwear-style guide.
Keep Image1's hips/skirt, connected complete thighs, knee/sock length, overall body proportions, pose sequence, exact4x2layout, scale, frame spacing and transparency. Do not crop or separate the thighs or redraw all body parts.
Fix the REAR (SCREEN-LEFT) foot's direction and ankle continuity:
Across all eight frames, rear toe axis points DIAGONALLY RIGHT/toward the viewer with a clear rounded toe cap extending right/lower-right of the ankle. Leave a distinct small heel behind it toward left/upper-left. Never a left-pointing shoe, ambiguous mirrored heel, completely front-facing shoe or vertical down-pointing boot. Keep the same two shoe lengths, widths, outlines and toe/heel silhouettes.
The main fault is FRAME6, bottom row SECOND cell: its rear shoe currently curls down and shortens. Replace that foot/ankle with the same diagonally-right shoe silhouette as frame1/5/7, lifted only slightly off the floor. Let the shin and ankle meet naturally above the heel; toe angle stays the same as neighboring frames, the sole stays almost level. No hard downward flex, foot spinning, shrinking, break in the calf, cut skin or floating foot.
Frame5 should have a small heel release, frame6 a low forward recovery of that same shoe, frame7 gentle landing. The joints transition smoothly 5-to6-to7. Prefer preserving everything else as exactly as possible.
The SCREEN-RIGHT lead foot always points RIGHT. Do not alter it or mirror the entire character. The torso to be added later facesRIGHT and root travelsRIGHT; feet and knees remain consistent with that.
True alpha background. No halo, glow, labels, text, arrows, grid, shadow or background. Output the same8frame4x2sheet with natural continuous rear foot recovery and clear consistent toe/heel directions.
```

### 단일 회수 자세 생성

```text
Use case: precise-object-edit
Asset type: ONE replacement lower-body recovery frame, NOT a sprite sheet.
Input images: Image1 is the complete lower-body pose just before recovery; Image2 is the next landing pose; Image3 shows a heel-left/toe-right loafer direction guide.
Primary request: Draw the ONE natural intermediate between Image1 and Image2. Keep the hips, skirt and complete thighs connected, same exact compact body proportions, palette, shoe lengths and coarse pixel style. The upper body will faceRIGHT and travelRIGHT.
Change only the rear SCREEN-LEFT leg/foot to a LOW recovery step. Raise that rear shoe only about 5–8 pixels in the reference-image scale and move it slightly RIGHT, toward the leading foot. Gently bend the knee to do so. KEEP THE REAR SHOE'S SHAPE AND TOE AXIS FROM IMAGE1: toe extends diagonally lower-RIGHT of the ankle, heel is upper-LEFT/behind ankle, and the sole remains almost level with the floor. The shoe is a rigid object translated slightly up/right, not rotated downward, shortened, curled into a ball or mirrored left.
The shin meets the ankle above the heel naturally. No chopped thigh, kinked knee, stretched calf, broken ankle, extra toes or foot facing the viewer. Screen-RIGHT front foot stays fully planted and points RIGHT, unchanged from references.
The waist cut, width and placement remain unchanged from Image1. Keep BOTH full thighs beneath the skirt, knee socks and ALL shoes visible. Preserve the skirt response subtly; no torso, hands, sword or background.
Output ONE complete connected lower-body sprite, same canvas/padding and scale as reference if possible, true transparent alpha, no glow, shadow, grid or text. It should look like a small local joint adjustment of Image1, with the lifted rear shoe keeping its identical direction and silhouette.
```

### 회수 자세 도트 질감 수정

```text
Use case: precise-object-edit
Asset type: one corrected lower-body sprite frame, not a sheet.
Input images: Image1 is the EDIT TARGET (the airborne rear-foot recovery pose, 800x704). Image2 is STYLE REFERENCE ONLY (the preceding pose in exactly the same palette and pixel scale).
Primary request: Keep Image1's geometry, waist position, complete thighs, knee positions, ankle positions and screen-RIGHT planted shoe EXACTLY. Adjust only the shading and pixel texture to match Image2. Image1 currently has too-flat skin and smoother, sparse pixels. Add the matching stepped light-peach/pink skin shadows under the skirt and knees, chunky dark-brown outline clusters and angular dark knee-sock/shoe highlights from Image2. Keep the same outline thickness, palette and pixel scale. The screen-LEFT lifted shoe must retain its current almost-level sole and diagonally RIGHT/down toe axis, but keep the full rear-shoe length and width from Image2 (not a miniature shoe). Heel behind/left of ankle, rounded toe lower-right. No twist, no left-facing foot, no blurred smooth curves.
Do not change the screen-RIGHT planted leg or shoe location. Do not change skirt shape, full body proportions, spacing of legs, knee bend or lift height. No body movement. No extra skin, hands or sword. This is a local palette/cluster correction of the existing pose, not redesign or pose interpolation.
Keep output same transparent canvas and art placement as Image1, 800x704 if possible. Flat waist cutoff only where existing, no chopped thighs or socks. True transparent alpha, no background, shadow, glow, grid or text. ONE pose only.
```

