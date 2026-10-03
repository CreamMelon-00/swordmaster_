# 단일 달리기 이동 자세

SwordGirl과 EnemyStudent는 위치가 변하는 동안 각 캐릭터의 **새로 그린 전신 달리기 자세 한 장**을 보여준다. 앞발로 지면을 딛고 뒷다리를 들어 올린 자세로, 상체·머리카락·치마·검손을 함께 다시 그렸다. 걷기 프레임 반복은 사용하지 않는다. 정지하면 대기 자세로 돌아간다. 공격·방어·피격 자세는 이동 자세보다 우선한다.

| 캐릭터 | 게임 리소스 | Aseprite 원본 | 비교 미리보기 |
| --- | --- | --- | --- |
| SwordGirl | `Assets/Game/Resources/SwordGirl/Animations/move/frame-01.png` | `SwordGirl-move.aseprite` | `SwordGirl-move-preview.png` |
| EnemyStudent | `Assets/Game/Resources/EnemyStudent/Animations/move/frame-01.png` | `EnemyStudent-move.aseprite` | `EnemyStudent-move-preview.png` |

전투의 접근·추격·재접근·계획 이동과 컷신 이동에 적용한다. 고정된 훈련 허수아비는 이동하지 않는다. 넉백과 스텝 잔상은 기존 포즈를 사용한다.

두 PNG는 기존 캐릭터와 동일한 256×224 캔버스, 색상, 접지선, 피벗, PPU 40, Point 필터와 무압축 설정을 사용한다. 이번 달리기 자세의 이미지 생성 시안과 Aseprite 수정 기록은 `RunPose`에 있다. 이전의 8프레임 걷기(`Revision2`)와 단순 기울이기(`SinglePose`)는 제작 기록으로만 보관하며 게임에서는 로드하지 않는다.

C# 프로젝트 컴파일과 리소스 구조를 확인했다. 이 환경에서는 Unity Editor 라이선스가 없어 PlayMode 화면은 실행하지 못했다.
