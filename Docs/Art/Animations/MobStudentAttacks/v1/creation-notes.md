# 모브 주인공 공격 애니메이션 v1

참격·관통·타격 각 3타, 총 9개 애니메이션을 만들었다. 각 공격은 준비·가속·접촉·회수 4프레임이다. 기존 작고 둥근 신체 비율, 눈을 덮는 금발 앞머리, 갈색 교복과 두 손 검 파지를 참조했다.

## 파일과 적용

완성 그림·원본·검날/몸 레이어·프레임 시트·검토용 GIF는 이 폴더의 각 공격 디렉터리에 있다. 실제 게임 리소스는 `Assets/Game/Resources/MobStudent/Animations`에 복사되어 현재 리뉴얼 프로젝트의 주인공으로 사용된다. 실행 및 검증 기록은 `Docs/MobStudentAnimationIntegration.md`를 따른다.

[전체 9개 동작 미리보기](all-attacks-preview.gif)는 참격·관통·타격을 세 열로 놓고 각 1→2→3타를 반복한다. [9개 접촉 자세](contact-pose-grid.png)에서는 각 동작의 세 번째 프레임을 한 번에 비교할 수 있다. 최종 36포즈에서 검끝 잘림, 손과 손잡이 분리, 상·하반신 연결 끊김이 없는지 시각 검토했다.

[실제 전장 공격 화면](applied-arena.png)과 [대기 화면](applied-idle.png)은 Unity PlayMode에서 전장 카메라가 렌더링한 결과다. HUD가 없는 전장 카메라만 캡처했다.

| 디렉터리 | 동작 |
|---|---|
| slash-1 | 오른쪽 아래로 사선 베기 |
| slash-2 | 아래에서 오른쪽 위로 올려 베기 |
| slash-3 | 높은 준비 자세에서 내려 베는 마무리 |
| pierce-1 | 중단 수평 찌르기 |
| pierce-2 | 낮은 손 위치에서 찌르기 |
| pierce-3 | 높은 선으로 찌르기 |
| blunt-1 | 손잡이 끝을 앞으로 밀어 치기 |
| blunt-2 | 높은 준비에서 근거리로 내려 치기 |
| blunt-3 | 손잡이를 아래·앞으로 강하게 치고 표준 가드로 회수 |

주인공은 오른쪽 3/4 방향을 유지한다. 검을 뒤로 돌리는 타격에서는 가드와 퍼멀 가까운 손의 화면 좌우 위치가 바뀔 수 있다. 양손의 파지 역할과 손목 연결을 기준으로 검토했다.

검날은 내장 image_gen으로 만든 별도 한 장을 모든 36프레임에서 재사용한다. 중심 축의 길이는 300px로 고정하며, 회전·배치만 달라진다. 실제 가드 막대에 수직인 축을 측정해 검날 방향을 맞췄다. 몸과 손잡이·가드는 생성된 그림을 사용하고 프레임마다 팔이나 손을 따로 옮기지 않았다. 자세에 따른 가드와 파지의 미세한 형태 차이는 남을 수 있다.

공통 캔버스는 1024×768, 허리 기준 (434,520), 발 기준 (448,704), PPU150이다. 기존 800×704 좌표에 투명 여백을 왼쪽128px·위64px 추가했다. 그림과 피벗을 같이 옮겨 게임에서 캐릭터 위치·크기를 보존하면서, 앞쪽 찌르기와 뒤로 젖힌 검끝이 잘리지 않게 했다.

검토용 GIF는 실제 게임보다 천천히 재생해 그림을 살펴보기 쉽게 했다. 게임에서는 기존 한 타 길이 1/6초, 타격 시점 1/12초를 유지하며 세 번째 프레임이 접촉이다. 실제 연타 순서에 따라 1→2→3타를 선택하고 단타는 1타를 사용한다. 이동 하반신은 별도 재생하고, 검을 들고 있는 상반신과 조합된다. 발의 세계 좌표 접지를 완벽히 고정하는 루트 모션을 새로 구현한 것은 아니다.

## 제작 방식

모든 새 그림 생성·수정은 내장 image_gen 기본 도구를 사용했다. CLI/API 모드는 사용하지 않았다. 셀 추출, 공통 크기 정규화, 투명 여백, 고정 검날 레이어 회전·합성, PNG/GIF 변환과 Unity import metadata 작성은 파일 처리로 수행했다. 손·팔·머리의 픽셀을 별도로 그리거나 개별 부위를 이동하지 않았다.

`manifest.json`에는 선택한 원본 검날의 SHA256, 300px 축 길이, 프레임별 배치·회전·클리핑 검사 기록이 있다. `attack-spec.json`은 원본 셀 좌표를 기록한다. `generated-upper-source.png`와 `blade-source.png`는 선택한 생성 원본이다.

## 최종 그림으로 이어진 프롬프트

### 공통 고정 검날

```text
Use case: background-extraction
Asset type: one reusable rigid pixel-art sword BLADE layer for a game animation.
Input image: approved pixel-art character is the exact sword design/style reference.
Primary request: Extract/reproduce ONLY the exposed STEEL BLADE of this character's longsword as a separate transparent graphic. It is a straight narrow double-edge steel blade with pointed tip, dark pixel outline, pale silver edge and muted gray-violet center ridge, exactly the same coarse pixel style as the reference. No crossguard, hilt, handle, hands or character. Keep the blade's original taper and width-to-length ratio, no new ornament.
Orient the blade HORIZONTALLY RIGHT: perfectly vertical flat base at LEFT (where it would insert into the gold crossguard), straight center line, pointed tip at RIGHT. This will be rotated as a single rigid image in every attack; preserve clear hard-edged square pixel clusters. One blade only, no perspective foreshortening, no curvature, no smear, no slash effect.
Composition: blade centered with generous transparent margin on all sides. True transparent alpha. No text, grid, glow, shadow, background or additional objects.
```

### slash-1

```text
Use case: identity-preserve
Asset type: four-frame modular UPPER-BODY pixel-art attack sprite sheet, exactly2 columns by2rows.
Input images: Image1 is the authoritative approved full character for exact identity, compact body proportions, palette and coarse pixel style. Image2 is the existing modular upper-body layer, authoritative waist cut and head/torso scale.
Subject: same beige-blonde bob-haired girl with dark headband, bangs COMPLETELY covering both eyes, tiny mouth, brown uniform blazer, white collar/cuffs, gold bow and brass buttons. Small squat proportions, chunky outlined pixel clusters, NOT taller/slimmer or smooth detailed illustration.
IMPORTANT LAYER ASSEMBLY: draw head, torso, BOTH arms, BOTH hands grasping the SAME two-handed dark-brown sword HILT and golden CROSSGUARD, but OMIT THE SILVER BLADE ENTIRELY. A separate rigid blade sprite will be added later at the crossguard blade socket to preserve exact length. The empty socket indicates the blade axis. Keep handle, gold guard, pommel and both hands consistent in size. Two hands never release the hilt: leading hand immediately behind guard, rear hand near pommel; plausible wrapped fingers, opposing palm/back views consistent with rotation. Guard is perpendicular to the blade axis. No duplicate hands. Draw NO legs, knee socks, feet or skirt.
Layout: ONE transparent2x2 sprite sheet, four equally sized cells, row-major F1,F2 / F3,F4. Same head size, waist-center X, waist-cut Y and camera/facing in all four cells. Only head/torso twist, arms and hilt move. Waist/pelvis anchor and overall body proportions remain FIXED to attach to separate moving lower body. Full hair/arms/hilt within cells with ample clear margin. The blazer ends at the same nearly horizontal waist cut in all cells and slightly overlaps skirt in final assembly.
Facing: character ALWAYS faces three-quarter RIGHT; target at RIGHT. Torso may twist slightly but head does not turn left or expose eyes. No whole-body translation or vertical bob.
Angles use screen coordinates: RIGHT=0deg, DOWN=90deg, LEFT=180deg, UP=-90deg. This angle is the forward blade-socket axis, away from hands; do not put a steel blade there.
True transparent alpha, no shadows/glow/background/text/grid/effects/ghost trails. Clean chunky pixel art.
Attack: diagonal descending cut, combo strike 1.
FOUR DISTINCT consecutive poses:
F1: Raise both hands near the right shoulder; hilt leads diagonally up-right. Blade socket angle -45deg.
F2: Drive both arms forward, beginning the diagonal cut. Blade socket angle -5deg.
F3: Extend arms toward the RIGHT, wrists around lower-chest height, hilt direction25deg down-right, contacting with blade edge. Blade socket angle 25deg.
F4: Fold elbows slightly, hilt stays low/right at35deg, ready for an ascending return. Blade socket angle 35deg.
F3 must clearly read as CONTACT. F4 is recovery, not an instant identical idle. Keep all four upper bodies joined down to the waist, no cropped forearms/hilt. No steel blade: hilt only for deterministic blade-layer assembly.
```

### slash-2

```text
Use case: identity-preserve
Asset type: four-frame modular UPPER-BODY pixel-art attack sprite sheet, exactly2 columns by2rows.
Input images: Image1 is the authoritative approved full character for exact identity, compact body proportions, palette and coarse pixel style. Image2 is the existing modular upper-body layer, authoritative waist cut and head/torso scale.
Subject: same beige-blonde bob-haired girl with dark headband, bangs COMPLETELY covering both eyes, tiny mouth, brown uniform blazer, white collar/cuffs, gold bow and brass buttons. Small squat proportions, chunky outlined pixel clusters, NOT taller/slimmer or smooth detailed illustration.
IMPORTANT LAYER ASSEMBLY: draw head, torso, BOTH arms, BOTH hands grasping the SAME two-handed dark-brown sword HILT and golden CROSSGUARD, but OMIT THE SILVER BLADE ENTIRELY. A separate rigid blade sprite will be added later at the crossguard blade socket to preserve exact length. The empty socket indicates the blade axis. Keep handle, gold guard, pommel and both hands consistent in size. Two hands never release the hilt: leading hand immediately behind guard, rear hand near pommel; plausible wrapped fingers, opposing palm/back views consistent with rotation. Guard is perpendicular to the blade axis. No duplicate hands. Draw NO legs, knee socks, feet or skirt.
Layout: ONE transparent2x2 sprite sheet, four equally sized cells, row-major F1,F2 / F3,F4. Same head size, waist-center X, waist-cut Y and camera/facing in all four cells. Only head/torso twist, arms and hilt move. Waist/pelvis anchor and overall body proportions remain FIXED to attach to separate moving lower body. Full hair/arms/hilt within cells with ample clear margin. The blazer ends at the same nearly horizontal waist cut in all cells and slightly overlaps skirt in final assembly.
Facing: character ALWAYS faces three-quarter RIGHT; target at RIGHT. Torso may twist slightly but head does not turn left or expose eyes. No whole-body translation or vertical bob.
Angles use screen coordinates: RIGHT=0deg, DOWN=90deg, LEFT=180deg, UP=-90deg. This angle is the forward blade-socket axis, away from hands; do not put a steel blade there.
True transparent alpha, no shadows/glow/background/text/grid/effects/ghost trails. Clean chunky pixel art.
Attack: ascending return cut, combo strike 2.
FOUR DISTINCT consecutive poses:
F1: Hands low in front of waist/right, hilt points35deg down-right. Blade socket angle 35deg.
F2: Lift the two-handed hilt forward/up from low guard. Blade socket angle 10deg.
F3: Arms reach forward at chest height; hilt direction30deg up-right, strong rising edge contact. Blade socket angle -30deg.
F4: Bend elbows, hands near the right shoulder, hilt points40deg up-right. Blade socket angle -40deg.
F3 must clearly read as CONTACT. F4 is recovery, not an instant identical idle. Keep all four upper bodies joined down to the waist, no cropped forearms/hilt. No steel blade: hilt only for deterministic blade-layer assembly.
```

### slash-3

```text
Use case: identity-preserve
Asset type: four-frame modular UPPER-BODY pixel-art attack sprite sheet, exactly2 columns by2rows.
Input images: Image1 is the authoritative approved full character for exact identity, compact body proportions, palette and coarse pixel style. Image2 is the existing modular upper-body layer, authoritative waist cut and head/torso scale.
Subject: same beige-blonde bob-haired girl with dark headband, bangs COMPLETELY covering both eyes, tiny mouth, brown uniform blazer, white collar/cuffs, gold bow and brass buttons. Small squat proportions, chunky outlined pixel clusters, NOT taller/slimmer or smooth detailed illustration.
IMPORTANT LAYER ASSEMBLY: draw head, torso, BOTH arms, BOTH hands grasping the SAME two-handed dark-brown sword HILT and golden CROSSGUARD, but OMIT THE SILVER BLADE ENTIRELY. A separate rigid blade sprite will be added later at the crossguard blade socket to preserve exact length. The empty socket indicates the blade axis. Keep handle, gold guard, pommel and both hands consistent in size. Two hands never release the hilt: leading hand immediately behind guard, rear hand near pommel; plausible wrapped fingers, opposing palm/back views consistent with rotation. Guard is perpendicular to the blade axis. No duplicate hands. Draw NO legs, knee socks, feet or skirt.
Layout: ONE transparent2x2 sprite sheet, four equally sized cells, row-major F1,F2 / F3,F4. Same head size, waist-center X, waist-cut Y and camera/facing in all four cells. Only head/torso twist, arms and hilt move. Waist/pelvis anchor and overall body proportions remain FIXED to attach to separate moving lower body. Full hair/arms/hilt within cells with ample clear margin. The blazer ends at the same nearly horizontal waist cut in all cells and slightly overlaps skirt in final assembly.
Facing: character ALWAYS faces three-quarter RIGHT; target at RIGHT. Torso may twist slightly but head does not turn left or expose eyes. No whole-body translation or vertical bob.
Angles use screen coordinates: RIGHT=0deg, DOWN=90deg, LEFT=180deg, UP=-90deg. This angle is the forward blade-socket axis, away from hands; do not put a steel blade there.
True transparent alpha, no shadows/glow/background/text/grid/effects/ghost trails. Clean chunky pixel art.
Attack: overhead finishing cut, combo strike 3.
FOUR DISTINCT consecutive poses:
F1: Raise both hands just above/right of the head, hilt mostly horizontal15deg up-right. Keep arms and head in frame. Blade socket angle -15deg.
F2: Bring arms down and forward in an overhead cut. Blade socket angle 5deg.
F3: Hands descend to lower chest, elbows extending; hilt direction30deg down-right, decisive edge contact. Blade socket angle 30deg.
F4: Short follow-through; fold elbows back toward the original middle guard, hilt20deg down-right. Blade socket angle 20deg.
F3 must clearly read as CONTACT. F4 is recovery, not an instant identical idle. Keep all four upper bodies joined down to the waist, no cropped forearms/hilt. No steel blade: hilt only for deterministic blade-layer assembly.
```

### pierce-1

```text
Use case: identity-preserve
Asset type: four-frame modular UPPER-BODY pixel-art attack sprite sheet, exactly2 columns by2rows.
Input images: Image1 is the authoritative approved full character for exact identity, compact body proportions, palette and coarse pixel style. Image2 is the existing modular upper-body layer, authoritative waist cut and head/torso scale.
Subject: same beige-blonde bob-haired girl with dark headband, bangs COMPLETELY covering both eyes, tiny mouth, brown uniform blazer, white collar/cuffs, gold bow and brass buttons. Small squat proportions, chunky outlined pixel clusters, NOT taller/slimmer or smooth detailed illustration.
IMPORTANT LAYER ASSEMBLY: draw head, torso, BOTH arms, BOTH hands grasping the SAME two-handed dark-brown sword HILT and golden CROSSGUARD, but OMIT THE SILVER BLADE ENTIRELY. A separate rigid blade sprite will be added later at the crossguard blade socket to preserve exact length. The empty socket indicates the blade axis. Keep handle, gold guard, pommel and both hands consistent in size. Two hands never release the hilt: leading hand immediately behind guard, rear hand near pommel; plausible wrapped fingers, opposing palm/back views consistent with rotation. Guard is perpendicular to the blade axis. No duplicate hands. Draw NO legs, knee socks, feet or skirt.
Layout: ONE transparent2x2 sprite sheet, four equally sized cells, row-major F1,F2 / F3,F4. Same head size, waist-center X, waist-cut Y and camera/facing in all four cells. Only head/torso twist, arms and hilt move. Waist/pelvis anchor and overall body proportions remain FIXED to attach to separate moving lower body. Full hair/arms/hilt within cells with ample clear margin. The blazer ends at the same nearly horizontal waist cut in all cells and slightly overlaps skirt in final assembly.
Facing: character ALWAYS faces three-quarter RIGHT; target at RIGHT. Torso may twist slightly but head does not turn left or expose eyes. No whole-body translation or vertical bob.
Angles use screen coordinates: RIGHT=0deg, DOWN=90deg, LEFT=180deg, UP=-90deg. This angle is the forward blade-socket axis, away from hands; do not put a steel blade there.
True transparent alpha, no shadows/glow/background/text/grid/effects/ghost trails. Clean chunky pixel art.
Attack: middle straight thrust, combo strike 1.
FOUR DISTINCT consecutive poses:
F1: Pull the two-hand hilt close to lower chest, elbows folded, horizontal ready. Blade socket angle -5deg.
F2: Both arms start extending RIGHT along a level chest-height thrust axis. Blade socket angle 0deg.
F3: Both arms fully extended RIGHT, hilt exactly horizontal and hands at chest height; point contact. Blade socket angle 0deg.
F4: Retract both hands toward chest along the identical line; elbows fold, middle guard. Blade socket angle -5deg.
F3 must clearly read as CONTACT. F4 is recovery, not an instant identical idle. Keep all four upper bodies joined down to the waist, no cropped forearms/hilt. No steel blade: hilt only for deterministic blade-layer assembly.
```

### pierce-2

```text
Use case: identity-preserve
Asset type: four-frame modular UPPER-BODY pixel-art attack sprite sheet, exactly2 columns by2rows.
Input images: Image1 is the authoritative approved full character for exact identity, compact body proportions, palette and coarse pixel style. Image2 is the existing modular upper-body layer, authoritative waist cut and head/torso scale.
Subject: same beige-blonde bob-haired girl with dark headband, bangs COMPLETELY covering both eyes, tiny mouth, brown uniform blazer, white collar/cuffs, gold bow and brass buttons. Small squat proportions, chunky outlined pixel clusters, NOT taller/slimmer or smooth detailed illustration.
IMPORTANT LAYER ASSEMBLY: draw head, torso, BOTH arms, BOTH hands grasping the SAME two-handed dark-brown sword HILT and golden CROSSGUARD, but OMIT THE SILVER BLADE ENTIRELY. A separate rigid blade sprite will be added later at the crossguard blade socket to preserve exact length. The empty socket indicates the blade axis. Keep handle, gold guard, pommel and both hands consistent in size. Two hands never release the hilt: leading hand immediately behind guard, rear hand near pommel; plausible wrapped fingers, opposing palm/back views consistent with rotation. Guard is perpendicular to the blade axis. No duplicate hands. Draw NO legs, knee socks, feet or skirt.
Layout: ONE transparent2x2 sprite sheet, four equally sized cells, row-major F1,F2 / F3,F4. Same head size, waist-center X, waist-cut Y and camera/facing in all four cells. Only head/torso twist, arms and hilt move. Waist/pelvis anchor and overall body proportions remain FIXED to attach to separate moving lower body. Full hair/arms/hilt within cells with ample clear margin. The blazer ends at the same nearly horizontal waist cut in all cells and slightly overlaps skirt in final assembly.
Facing: character ALWAYS faces three-quarter RIGHT; target at RIGHT. Torso may twist slightly but head does not turn left or expose eyes. No whole-body translation or vertical bob.
Angles use screen coordinates: RIGHT=0deg, DOWN=90deg, LEFT=180deg, UP=-90deg. This angle is the forward blade-socket axis, away from hands; do not put a steel blade there.
True transparent alpha, no shadows/glow/background/text/grid/effects/ghost trails. Clean chunky pixel art.
Attack: low straight thrust, combo strike 2.
FOUR DISTINCT consecutive poses:
F1: Hands near lower ribs, hilt points gently10deg down-right toward opponent abdomen. Blade socket angle 10deg.
F2: Extend arms low/right along the same12deg descending thrust axis. Blade socket angle 12deg.
F3: Both arms extended low/right, hilt12deg down-right, lower-line point contact. Blade socket angle 12deg.
F4: Retract along the low line, elbows fold, hilt5deg down-right. Blade socket angle 5deg.
F3 must clearly read as CONTACT. F4 is recovery, not an instant identical idle. Keep all four upper bodies joined down to the waist, no cropped forearms/hilt. No steel blade: hilt only for deterministic blade-layer assembly.
```

### pierce-3

```text
Use case: identity-preserve
Asset type: four-frame modular UPPER-BODY pixel-art attack sprite sheet, exactly2 columns by2rows.
Input images: Image1 is the authoritative approved full character for exact identity, compact body proportions, palette and coarse pixel style. Image2 is the existing modular upper-body layer, authoritative waist cut and head/torso scale.
Subject: same beige-blonde bob-haired girl with dark headband, bangs COMPLETELY covering both eyes, tiny mouth, brown uniform blazer, white collar/cuffs, gold bow and brass buttons. Small squat proportions, chunky outlined pixel clusters, NOT taller/slimmer or smooth detailed illustration.
IMPORTANT LAYER ASSEMBLY: draw head, torso, BOTH arms, BOTH hands grasping the SAME two-handed dark-brown sword HILT and golden CROSSGUARD, but OMIT THE SILVER BLADE ENTIRELY. A separate rigid blade sprite will be added later at the crossguard blade socket to preserve exact length. The empty socket indicates the blade axis. Keep handle, gold guard, pommel and both hands consistent in size. Two hands never release the hilt: leading hand immediately behind guard, rear hand near pommel; plausible wrapped fingers, opposing palm/back views consistent with rotation. Guard is perpendicular to the blade axis. No duplicate hands. Draw NO legs, knee socks, feet or skirt.
Layout: ONE transparent2x2 sprite sheet, four equally sized cells, row-major F1,F2 / F3,F4. Same head size, waist-center X, waist-cut Y and camera/facing in all four cells. Only head/torso twist, arms and hilt move. Waist/pelvis anchor and overall body proportions remain FIXED to attach to separate moving lower body. Full hair/arms/hilt within cells with ample clear margin. The blazer ends at the same nearly horizontal waist cut in all cells and slightly overlaps skirt in final assembly.
Facing: character ALWAYS faces three-quarter RIGHT; target at RIGHT. Torso may twist slightly but head does not turn left or expose eyes. No whole-body translation or vertical bob.
Angles use screen coordinates: RIGHT=0deg, DOWN=90deg, LEFT=180deg, UP=-90deg. This angle is the forward blade-socket axis, away from hands; do not put a steel blade there.
True transparent alpha, no shadows/glow/background/text/grid/effects/ghost trails. Clean chunky pixel art.
Attack: high straight thrust, combo strike 3.
FOUR DISTINCT consecutive poses:
F1: Hands near chest, hilt15deg up-right toward opponent upper chest. Blade socket angle -15deg.
F2: Lean torso subtly forward with the waist fixed and extend both arms on20deg up-right axis. Blade socket angle -20deg.
F3: Both arms extended RIGHT/up at upper-chest level, hilt20deg up-right, high point contact. Blade socket angle -20deg.
F4: Retract hands toward chest, same high line, return toward middle guard. Blade socket angle -15deg.
F3 must clearly read as CONTACT. F4 is recovery, not an instant identical idle. Keep all four upper bodies joined down to the waist, no cropped forearms/hilt. No steel blade: hilt only for deterministic blade-layer assembly.
```

### blunt-1

```text
Use case: identity-preserve
Asset type: four-frame modular UPPER-BODY pixel-art attack sprite sheet, exactly2 columns by2rows.
Input images: Image1 is the authoritative approved full character for exact identity, compact body proportions, palette and coarse pixel style. Image2 is the existing modular upper-body layer, authoritative waist cut and head/torso scale.
Subject: same beige-blonde bob-haired girl with dark headband, bangs COMPLETELY covering both eyes, tiny mouth, brown uniform blazer, white collar/cuffs, gold bow and brass buttons. Small squat proportions, chunky outlined pixel clusters, NOT taller/slimmer or smooth detailed illustration.
IMPORTANT LAYER ASSEMBLY: draw head, torso, BOTH arms, BOTH hands grasping the SAME two-handed dark-brown sword HILT and golden CROSSGUARD, but OMIT THE SILVER BLADE ENTIRELY. A separate rigid blade sprite will be added later at the crossguard blade socket to preserve exact length. The empty socket indicates the blade axis. Keep handle, gold guard, pommel and both hands consistent in size. Two hands never release the hilt: leading hand immediately behind guard, rear hand near pommel; plausible wrapped fingers, opposing palm/back views consistent with rotation. Guard is perpendicular to the blade axis. No duplicate hands. Draw NO legs, knee socks, feet or skirt.
Layout: ONE transparent2x2 sprite sheet, four equally sized cells, row-major F1,F2 / F3,F4. Same head size, waist-center X, waist-cut Y and camera/facing in all four cells. Only head/torso twist, arms and hilt move. Waist/pelvis anchor and overall body proportions remain FIXED to attach to separate moving lower body. Full hair/arms/hilt within cells with ample clear margin. The blazer ends at the same nearly horizontal waist cut in all cells and slightly overlaps skirt in final assembly.
Facing: character ALWAYS faces three-quarter RIGHT; target at RIGHT. Torso may twist slightly but head does not turn left or expose eyes. No whole-body translation or vertical bob.
Angles use screen coordinates: RIGHT=0deg, DOWN=90deg, LEFT=180deg, UP=-90deg. This angle is the forward blade-socket axis, away from hands; do not put a steel blade there.
True transparent alpha, no shadows/glow/background/text/grid/effects/ghost trails. Clean chunky pixel art.
Attack: short pommel shove, combo strike 1.
FOUR DISTINCT consecutive poses:
F1: Reverse the two-hand hilt grip orientation so the BLADE SOCKET points up-left/behind, and pommel points down-right/front; hands at chest height. Blade socket angle -135deg.
F2: Push both hands and the pommel slightly RIGHT, keeping elbows compact. Blade socket angle -150deg.
F3: Pommel visibly foremost at the RIGHT, short close-range contact; blade socket points155deg up-left, not toward target. Blade socket angle -155deg.
F4: Draw hilt back toward chest, keep safe blade behind/up-left, elbows fold. Blade socket angle -135deg.
F3 must clearly read as CONTACT. F4 is recovery, not an instant identical idle. Keep all four upper bodies joined down to the waist, no cropped forearms/hilt. No steel blade: hilt only for deterministic blade-layer assembly.
CRITICAL blunt-strike geometry in F1-F3: the golden CROSSGUARD/empty blade socket must be LEFT/UP-LEFT of the TWO HANDS. The brown grip extends from this LEFT guard toward LOWER-RIGHT, with golden POMMEL at the RIGHT/front of the hands. The pommel or guard makes a short RIGHTward contact, rather than an extended blade slash/thrust. Do not reuse ordinary guard-on-right sword pose. F4 may rotate back to normal guard where specified.
```

### blunt-2

```text
Use case: identity-preserve
Asset type: four-frame modular UPPER-BODY pixel-art attack sprite sheet, exactly2 columns by2rows.
Input images: Image1 is the authoritative approved full character for exact identity, compact body proportions, palette and coarse pixel style. Image2 is the existing modular upper-body layer, authoritative waist cut and head/torso scale.
Subject: same beige-blonde bob-haired girl with dark headband, bangs COMPLETELY covering both eyes, tiny mouth, brown uniform blazer, white collar/cuffs, gold bow and brass buttons. Small squat proportions, chunky outlined pixel clusters, NOT taller/slimmer or smooth detailed illustration.
IMPORTANT LAYER ASSEMBLY: draw head, torso, BOTH arms, BOTH hands grasping the SAME two-handed dark-brown sword HILT and golden CROSSGUARD, but OMIT THE SILVER BLADE ENTIRELY. A separate rigid blade sprite will be added later at the crossguard blade socket to preserve exact length. The empty socket indicates the blade axis. Keep handle, gold guard, pommel and both hands consistent in size. Two hands never release the hilt: leading hand immediately behind guard, rear hand near pommel; plausible wrapped fingers, opposing palm/back views consistent with rotation. Guard is perpendicular to the blade axis. No duplicate hands. Draw NO legs, knee socks, feet or skirt.
Layout: ONE transparent2x2 sprite sheet, four equally sized cells, row-major F1,F2 / F3,F4. Same head size, waist-center X, waist-cut Y and camera/facing in all four cells. Only head/torso twist, arms and hilt move. Waist/pelvis anchor and overall body proportions remain FIXED to attach to separate moving lower body. Full hair/arms/hilt within cells with ample clear margin. The blazer ends at the same nearly horizontal waist cut in all cells and slightly overlaps skirt in final assembly.
Facing: character ALWAYS faces three-quarter RIGHT; target at RIGHT. Torso may twist slightly but head does not turn left or expose eyes. No whole-body translation or vertical bob.
Angles use screen coordinates: RIGHT=0deg, DOWN=90deg, LEFT=180deg, UP=-90deg. This angle is the forward blade-socket axis, away from hands; do not put a steel blade there.
True transparent alpha, no shadows/glow/background/text/grid/effects/ghost trails. Clean chunky pixel art.
Attack: crossguard downward blow, combo strike 2.
FOUR DISTINCT consecutive poses:
F1: Hilt held high in front of right shoulder; blade socket points up-left/behind; crossguard raised. Blade socket angle -140deg.
F2: Bring hands and the gold crossguard down/right in a short compact strike. Blade socket angle -155deg.
F3: Gold guard makes close-range downward/right contact, hands in front of chest; blade socket170deg up-left behind body. Blade socket angle -170deg.
F4: Pull the crossguard up and back, compact recovery. Blade socket angle -140deg.
F3 must clearly read as CONTACT. F4 is recovery, not an instant identical idle. Keep all four upper bodies joined down to the waist, no cropped forearms/hilt. No steel blade: hilt only for deterministic blade-layer assembly.
CRITICAL blunt-strike geometry in F1-F3: the golden CROSSGUARD/empty blade socket must be LEFT/UP-LEFT of the TWO HANDS. The brown grip extends from this LEFT guard toward LOWER-RIGHT, with golden POMMEL at the RIGHT/front of the hands. The pommel or guard makes a short RIGHTward contact, rather than an extended blade slash/thrust. Do not reuse ordinary guard-on-right sword pose. F4 may rotate back to normal guard where specified.
```

### slash-3 접촉·회수 파지 보정

```text
Use case: precise-object-edit
Asset type: correction of an existing four-frame upper-body pixel-art sword attack sheet.
Input image: EDIT TARGET, exactly 2x2 row-major frames F1 F2 / F3 F4, upper-body with hilt and guard but no steel blade.
Primary request: Keep the character identity, head/torso proportions, all waist cuts, pixel palette and chunky outline, and preserve F1/F2 closely. Correct ONLY the TWO-HAND HILT orientation and connected forearms in BOTTOM-LEFT F3 and BOTTOM-RIGHT F4 so this overhead attack finishes as a DOWN-RIGHT cut.
F3 contact: brown grip axis from rear hand toward leading hand and guard should slope DOWN-RIGHT approximately30deg (screen right=0deg, down=90deg). Leading hand/guard must be LOWER and fartherRIGHT than rear hand. The GOLD CROSSGUARD is perpendicular to this grip: its long bar slopes from UPPER-RIGHT to LOWER-LEFT (axis120deg), rather than from upper-left to lower-right. Empty blade socket faces DOWN-RIGHT away from both hands. Guard is at the front/right end of grip, pommel behind/left. Forearms and both wrists follow this same grip naturally.
F4 recovery: hilt points gently20deg DOWN-RIGHT, elbows folded, gold guard again perpendicular (axis110deg, upper-right to lower-left). Same two-hand wrapped grip, palm/back distinction consistent with rotation, fixed handle/guard/pommel dimensions. No extra hands or detached wrists. Do not simply rotate the crossguard while leaving the grip and hands facing the old direction.
No steel blade: a fixed rigid blade layer will be added to the corrected socket. Keep all upper sprites complete from full hair to flat waist cut, with same registration and transparent margins in every cell. Preserve all clothing/head elements and eye-covering bangs. No legs/skirt, no effects, no background, grid or text. True transparent alpha, same2x2layout.
```

### blunt-3 최종 마무리 동작

blunt-2의 일관된 파지 그림을 참조하여 준비·발동을 계승하고 접촉·회수를 새로 만든 프롬프트다.

```text
Use case: precise-object-edit
Asset type: four-frame upper-body pommel-strike FINISHER derived from the existing correct two-handed hilt sheet.
Input image: edit target, authoritative gold crossguard/dark handle/pommel design and wrapped hands. Preserve the hilt anatomy. The blade is deliberately omitted and will be added as a fixed rigid layer.
Keep TOP-LEFT F1 and TOP-RIGHT F2 as the high raised preparation and descending activation. Their gold CROSSGUARD must remain the SAME perpendicular crossbar immediately ahead of leading hand; pommel at rear, no long gold blade extension.
Change BOTTOM-LEFT F3 to a stronger DOWN-RIGHT POMMEL contact: extend both hands together a little farther RIGHT and lower, pommel is foremost/right at about lower-rib height. Reuse the exact RIGID hand–handle–crossguard shape from this target F3, moving the entire hilt/grip group together with connected forearms. Do not redesign or shrink the golden guard. Blade socket still points LEFT/slightly UP-LEFT; pommel points RIGHT/slightly DOWN-RIGHT. Hilt never points at the character. Both hands keep holding the same dark handle, no release or extra fingers.
Change BOTTOM-RIGHT F4 to a clear recovery toward normal forward sword guard: elbows fold, both hands settle in front of lower chest, gold crossguard at RIGHT/front of the hand pair, empty blade socket points RIGHT/UP-RIGHT about -30deg. Crossguard is perpendicular to grip, bright golden crossbar identical length/thickness to F1-F3, pommel behind/left. Standard two-hand longsword wrapped grip. No steel blade yet.
Freeze compact head/torso size, three-quarter RIGHT facing, eyes fully hidden by bangs, hair/headband, body palette and coarse pixel clusters, all four waist centers and flat bottom cuts. No skirt, legs, feet, blade, effects, labels, grid, background or glow. Full upper bodies, hands and hilt in safe margins of equal2x2cells, true transparent alpha. Four distinct poses, no pose copied onto all cells.
```
