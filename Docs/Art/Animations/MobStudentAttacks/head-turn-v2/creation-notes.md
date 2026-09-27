# 모브 타격: 뒤돌아보는 고개 수정

사용자가 검이 뒤쪽으로 움직이는 동안 얼굴이 앞쪽에 고정돼 있다고 지적했다. 타격 3종의 상반신 12프레임에서 고개와 목·옷깃의 연결을 수정했다. 출력은 1프레임에서 오른쪽을 보고, 2·3프레임에서 왼쪽 뒤를 돌아보며, 4프레임에서 오른쪽 가드 방향으로 회수한다. 눈은 계속 앞머리에 완전히 가려진다.

모든 실제 그림 수정은 내장 image_gen 기본 도구로 수행했다. CLI/API 모드는 사용하지 않았다. 생성 결과 전체 상반신을 공통 캔버스에 정렬하고 동일한 검날 원본을 회전·합성했다. 기존 비율, 1024×768 캔버스, 공통 발 피벗 (448,64), PPU150과 300px 검날 축을 유지한다. 개별 얼굴 픽셀을 직접 그리거나 머리를 잘라 옮기지 않았다.

기존 v1 그림과 기록은 보존한다. 새 버전의 그림·프레임 시트·GIF·생성 원본·검날 레이어는 이 폴더에 저장한다. 검은 뒤쪽에 놓이지만 손잡이 끝은 앞쪽으로 밀리는 기존 타격 구성이며, 이번 수정은 고개 방향의 변화다. 공격 대상·타격 판정·이동·재생 시계는 바꾸지 않는다.

[9개 전체 미리보기](all-attacks-preview.gif) · [수정된 타격 3타](blunt-combo-preview.gif) · [고개 수정 실제 전장 화면](applied-blunt-3.png)

## 최종 프롬프트

blunt-3의 최초 요청에서는 1프레임에서도 왼쪽으로 돌리도록 기술했으나, 생성 결과는 오른쪽에서 시작해 왼쪽으로 돌아본 다음 오른쪽으로 회수한다. 이 시작 자세를 채택하여 다른 두 타격에도 같은 방향 순서를 명시했다. blunt-1·2의 두 번째 입력은 새 blunt-3 원본이며, 머리 방향만 참조하고 각자의 팔과 검 손잡이 자세는 첫 번째 기존 원본을 따른다.

### blunt-1

```text
Use case: identity-preserve
Asset type: precise revision of the existing 4-frame modular pixel-art upper-body sprite sheet.
Input image1 is the EDIT TARGET. Input image2 is ONLY a head-direction reference for the same character: take its left-facing side/back head at F2/F3 and right-facing recovery head as reference. DO NOT take image2's arms, hilt, poses or torso. Keep image1's exact 2-by-2 cell layout and transparent alpha.
Primary request: correct the head and neck direction for this backward sword swing. Change ONLY the girl's head/hair/neck orientation and the minimal collar/shoulder connection needed for a plausible neck turn. Preserve every existing attack pose, waist attachment, proportions, palette, coarse pixel style, both hands, sword hilt and gold guard geometry and positions. Do not add a metal blade: the original fixed-length blade is composited separately.
CURRENT ERROR: her face is facing the viewer identically in all four cells although the sword is swinging toward screen LEFT behind her.
Required four-frame head motion, row-major top-left F1, top-right F2, bottom-left F3, bottom-right F4:
F1: preparation begins with a three-quarter RIGHT-facing head before the backward turn; narrow cheek/profile rather than a broad symmetric camera-front face.
F2: turn farther toward screen LEFT, following the backward rotation; show more side and back of the bob haircut.
F3: at the backward swing/contact, clearly look to screen LEFT, nose/mouth/profile direction left, hair back mass on the RIGHT, one-sided face silhouette. Keep front bangs covering all eyes, no exposed eye. A natural head-and-neck turn following the shoulder, no disconnected neck or head floating.
F4: recover to the normal right-facing guard, head now turned three-quarter RIGHT rather than camera front.
Maintain the SAME compact short head size, beige-blonde bob, dark headband, eyes completely hidden by bangs. No taller or thinner body, no changed arms, hands, grip, hilt, crossguard, bow or waist seam. Keep all existing non-head pixels as unchanged as possible. No legs, skirt, text, guide lines, effects, extra objects or background. True transparency.
This is not a mirrored full body. The hands and weapon stay in their current individual cell positions. This edit target is Blunt strike1; preserve its compact forward-pushing arm movement exactly.
```

### blunt-2

```text
Use case: identity-preserve
Asset type: precise revision of the existing 4-frame modular pixel-art upper-body sprite sheet.
Input image1 is the EDIT TARGET. Input image2 is ONLY a head-direction reference for the same character: take its left-facing side/back head at F2/F3 and right-facing recovery head as reference. DO NOT take image2's arms, hilt, poses or torso. Keep image1's exact 2-by-2 cell layout and transparent alpha.
Primary request: correct the head and neck direction for this backward sword swing. Change ONLY the girl's head/hair/neck orientation and the minimal collar/shoulder connection needed for a plausible neck turn. Preserve every existing attack pose, waist attachment, proportions, palette, coarse pixel style, both hands, sword hilt and gold guard geometry and positions. Do not add a metal blade: the original fixed-length blade is composited separately.
CURRENT ERROR: her face is facing the viewer identically in all four cells although the sword is swinging toward screen LEFT behind her.
Required four-frame head motion, row-major top-left F1, top-right F2, bottom-left F3, bottom-right F4:
F1: preparation begins with a three-quarter RIGHT-facing head before the backward turn; narrow cheek/profile rather than a broad symmetric camera-front face.
F2: turn farther toward screen LEFT, following the backward rotation; show more side and back of the bob haircut.
F3: at the backward swing/contact, clearly look to screen LEFT, nose/mouth/profile direction left, hair back mass on the RIGHT, one-sided face silhouette. Keep front bangs covering all eyes, no exposed eye. A natural head-and-neck turn following the shoulder, no disconnected neck or head floating.
F4: recover to the normal right-facing guard, head now turned three-quarter RIGHT rather than camera front.
Maintain the SAME compact short head size, beige-blonde bob, dark headband, eyes completely hidden by bangs. No taller or thinner body, no changed arms, hands, grip, hilt, crossguard, bow or waist seam. Keep all existing non-head pixels as unchanged as possible. No legs, skirt, text, guide lines, effects, extra objects or background. True transparency.
This is not a mirrored full body. The hands and weapon stay in their current individual cell positions. This edit target is Blunt strike2. Preserve its specific high preparation, descending arms, compact contact and raised recovery poses exactly. Do not copy the low contact pose from the other sheet.
```

### blunt-3

```text
Use case: identity-preserve
Asset type: precise revision of the existing 4-frame modular pixel-art upper-body sprite sheet.
Input image1 is the EDIT TARGET. Keep its exact 2-by-2 cell layout and transparent alpha.
Primary request: correct the head and neck direction for this backward sword swing. Change ONLY the girl's head/hair/neck orientation and the minimal collar/shoulder connection needed for a plausible neck turn. Preserve every existing attack pose, waist attachment, proportions, palette, coarse pixel style, both hands, sword hilt and gold guard geometry and positions. Do not add a metal blade: the original fixed-length blade is composited separately.
CURRENT ERROR: her face is facing the viewer identically in all four cells although the sword is swinging toward screen LEFT behind her.
Required four-frame head motion, row-major top-left F1, top-right F2, bottom-left F3, bottom-right F4:
F1: begin looking back over the left shoulder, head turned noticeably toward screen LEFT (three-quarter left), showing a narrow cheek/profile rather than a broad symmetric front face.
F2: turn farther toward screen LEFT, following the backward rotation; show more side and back of the bob haircut.
F3: at the backward swing/contact, clearly look to screen LEFT, nose/mouth/profile direction left, hair back mass on the RIGHT, one-sided face silhouette. Keep front bangs covering all eyes, no exposed eye. A natural head-and-neck turn following the shoulder, no disconnected neck or head floating.
F4: recover to the normal right-facing guard, head now turned three-quarter RIGHT rather than camera front.
Maintain the SAME compact short head size, beige-blonde bob, dark headband, eyes completely hidden by bangs. No taller or thinner body, no changed arms, hands, grip, hilt, crossguard, bow or waist seam. Keep all existing non-head pixels as unchanged as possible. No legs, skirt, text, guide lines, effects, extra objects or background. True transparency.
This is not a mirrored full body. The hands and weapon stay in their current individual cell positions.
```

## 검증

새 PNG의 고정 검날·공통 피벗·캔버스·잘림·기존 37개 PNG 보존 검사와 Unity 재생 검사는 프로젝트의 최신 `Docs/Validation.md`를 따른다.

고개 수정 후 Unity PlayMode의 모브 애니메이션 검사 5개와 실제 카메라 촬영 검사 1개, 총 6/6을 통과했다. 실패·건너뜀 0, 종료 코드 0이다. `Logs/MobStudentHeadTurnPlayModeResults.xml`과 `MobStudentHeadTurnPlayMode.log`에 결과를 저장했다. 실제 타격 2·3 화면에서 뒤쪽 고개, 허리 연결, 발과 검끝을 확인했다. [타격 1 적용](applied-blunt-1.png) · [타격 2 적용](applied-blunt-2.png) · [타격 3 적용](applied-blunt-3.png).
