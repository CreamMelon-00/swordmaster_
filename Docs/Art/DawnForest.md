# 새벽 숲 배경

2026-10-02 승인된 숲 시안을 전투 배경과 게임 시작 연출에 맞춰 정리했다. 짙은 녹색 일변도였던 숲에 옅은 새벽 안개와 따뜻한 노란빛을 더했다. 나무는 평범한 숲의 형태로, 길은 거친 흙 대신 부드러운 황토빛으로 표현하고 길 앞쪽의 덩굴은 낮은 풀로 줄였다. 캐릭터가 서고 싸우는 중앙 공간은 비워 둔다. 기준 시안은 [`DawnForest/approved-concept.png`](DawnForest/approved-concept.png)이다.

| 리소스 (`Assets/Game/Resources/ForestArena/`) | 쓰임 |
| --- | --- |
| `forest-far-mist.png` | 전투의 가장 먼 안개, 시작 컷신과 첫 브리핑 |
| `forest-far-trees.png` | 전투의 먼 나무 층 |
| `forest-belt-mid.png` | 전투의 숲길과 중경, 로비 미리보기 |
| `forest-near.png` | 전투의 낮은 앞쪽 풀 |
| `forest-far.png` | 프롤로그와 로비의 숲 이미지 |

수정 가능한 Aseprite 원본은 `DawnForest/Processed/<리소스 이름>.aseprite`, 대응하는 최종 PNG는 같은 폴더에 둔다. 후보 이미지와 승인 시안은 `DawnForest/Candidates/`와 `DawnForest/approved-concept.png`에 보관한다. 처리 방법과 재생성 명령은 [`DawnForest/Tools/README.md`](DawnForest/Tools/README.md)에 있다.

다섯 PNG는 모두 기존 경로와 2172×724 크기를 유지한다. Unity의 각 `.png.meta`를 유지해 GUID, Point 필터, Clamp 반복, 밉맵 비활성, 압축 비활성, Pixels Per Unit 128 설정을 보존한다. `ForestParallaxBackdrop`의 네 전투 레이어 순서·높이·Y 위치·시차 속도도 그대로 사용한다. `forest-mid.png`는 이 교체 대상이 아니다.

이미지 자체의 검사는 4×4 픽셀 블록의 일치, 최대 64색, 0/255 알파, 레이어별 투명 영역과 바닥 범위를 확인한다. 이 검사는 Unity 화면에서의 합성, 카메라 이동, 컷신과 로비의 실제 구도를 증명하지 않는다. 실제 화면 검사는 전투 시작·이동과 시작 연출에서 별도로 확인한다.
