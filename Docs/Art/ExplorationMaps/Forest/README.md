# 숲 탐색 필드 리소스

학교 복도 오른쪽 출구와 이어지는 **유한한 숲 필드**입니다. 기존 승인된 새벽 숲의 노란 길과 평범한 활엽수 느낌을 유지하고, 왼쪽에 19세기 석조 학교의 외벽·출입문·낮은 담장을 그렸습니다. 오른쪽 끝에서는 기존 뒤편 철책이 길을 가로질러 꺾여 필드의 끝을 보여 줍니다.

## 파일

| 경로 | 내용 |
| --- | --- |
| `Source/forest-{far-mist,far-trees,belt-mid,near}.png` | 편집·재출력 기준 4레이어 PNG |
| `forest-master.aseprite` | 네 레이어가 분리된 Aseprite 원본 |
| `Runtime/forest-{far-mist,far-trees,belt-mid,near}.png` | Unity에 그대로 가져갈 PNG. Source와 픽셀 완전 동일 |
| `Preview/forest-composite.png` | 전체 필드 합성 시안 |
| `Preview/forest-view-{1,2,3}.png` | 실제 화면 크기에 가까운 3구간 미리보기 |
| `build_forest.py`, `build_master.lua`, `verify_master.lua`, `build_forest.ps1` | 결정론적 생성·Aseprite 마스터 제작·일치 검증 |

## 레이어와 좌표

- 각 레이어의 원화와 런타임 크기는 **4096×724px**입니다. 1024×181 논리 픽셀을 정확히 4배 확대한 결과라 모든 도트는 4×4 격자에 놓이고 반투명 픽셀은 없습니다.
- 아래에서 위로 `far-mist` → `far-trees` → `belt-mid` → `near` 순서로 합성합니다. 원래 숲의 시차 사용 시 기준값은 각각 0.10, 0.24, 1.0, 1.35입니다. 학교 건물·문·문턱·끝 철책은 시차 없는 `belt-mid`에 있습니다.
- 학교 출입문 중심은 원화 **x=352px**, 양문은 x=232–472px, 석재 아치는 x=184–520px입니다. 문턱은 y≈436–496px에서 포장 접근로와 연결됩니다. 대응되는 복도 출입문 중심은 x=3720px, 폭 240px, 문턱 y≈432–499px입니다. 숲 출입구에는 복도의 실제 월넛 양문·아치 픽셀을 옮겨 양쪽 크기와 장식을 일치시켰습니다.
- 오른쪽 길 끝의 철책과 기둥은 대략 x=3740–4096px에 있습니다. 전체 길이는 기존 전투 화면 약 3개 분량입니다. 미리보기는 폭 1360px씩 잘라 확인할 수 있습니다.
- Unity 가져오기 권장 설정: **Pixels Per Unit 128, Point, Clamp, Full Rect, 밉맵 없음, 압축 없음, 최대 텍스처 4096**. 네 레이어는 같은 앵커·스케일로 정렬해야 합니다.

## 재생성

`build_forest.ps1` 실행 시 기존 `DawnForest/Processed` 4레이어와 `ExplorationMaps/Corridor/Authored/corridor-interior.png`의 출입문을 읽어 PNG·미리보기·Aseprite 마스터를 재생성합니다. 스크립트는 Aseprite에서 네 레이어를 다시 내보내 Source PNG와 픽셀 완전 일치하는지 검사합니다. 복도 원화의 출입문을 수정했다면 먼저 복도 리소스를 다시 만든 다음 이 숲 리소스를 재생성하세요.

## 확인 결과

`verification.txt`에 실제 색상 수와 알파 범위를 기록했습니다. 각 레이어의 불투명 색상은 64색 이하이며, 최종 합성본에는 빈 픽셀이 없습니다. 필드 이동·문 전환·충돌·카메라 제한은 이 리소스에 포함되지 않습니다.
