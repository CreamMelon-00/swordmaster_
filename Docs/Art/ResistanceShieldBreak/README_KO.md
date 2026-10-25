# 저항 붕괴 방패 파괴 이펙트 — 2026-10-25

## 적용

초기 작업 당시 main `483fcac`에 art의 마지막 커밋 `e5e64e4`가 이미 포함돼 있음을 조상 관계로 확인했습니다. 중복 머지는 수행하지 않고 병합된 로컬 art 브랜치만 삭제했습니다. 원격 art 브랜치는 삭제하지 않았습니다.

이후 사용자의 pull·커밋·push 요청에 따라 원격 main `cd06932`(긴장·신경 집중 추가와 고속 연출 보완)를 fast-forward로 가져왔습니다. 방패 작업 21개 파일은 별도 복사본과 stash로 보존한 뒤 충돌 없이 복원했습니다. 아래 최신 통합 검증을 통과한 소스·리소스·문서를 함께 커밋합니다.

PixelLab VFX Workbench에서 투명 금빛 방패가 갈라지고 여섯 테두리 조각이 바깥으로 날아가며 떨어져 소멸하는 효과를 제작했습니다. 중앙은 비워 캐릭터를 가리지 않도록 했습니다.

- 128×128 프레임 14장, 24fps, 약 0.583초, 1회 재생. 마지막 프레임은 투명합니다.
- 기존 카메라·붕괴 자세·붕괴 문구·화면 가장자리 강조는 유지합니다.
- 캠페인과 로컬 대전이 공유하는 `DuelBreakImpactCue.Show`에서 함께 시작합니다. 실제 붕괴 전환 신호를 그대로 이용하므로 이미 붕괴된 대상의 추가 피격에는 재시작하지 않습니다.
- 실제 시간으로 재생해 히트 스톱 중에도 마무리됩니다. 일시정지에서는 컨트롤러의 기존 정지 정책을 따릅니다. 양쪽 효과는 독립적으로 재생하고 전투 초기화 시 모두 정리합니다.
- 카메라의 현재 투영으로 몸통 위치와 표시 크기를 매번 갱신합니다. 새 전신 도트의 중앙 피벗과 구형 상체 도트의 피벗 차이를 구분합니다. Image의 raycastTarget은 꺼두었습니다.
- 방패와 기존 원형 파동은 같은 몸통 기준점을 공유합니다. 파동만 화면 안쪽으로 이동시키던 가장자리 보정은 제거해, 화면 가장자리와 카메라 이동·확대 중에도 두 중심이 분리되지 않도록 했습니다. 사용자 지적 후 양쪽 중심 일치 검사를 추가하고 전장 렌더를 갱신했습니다.
- 실제 HUD는 1920×1080 기준이지만 최초 미리보기는 960×540 기준이라, 고정 UI 크기였던 파동이 게임에서는 작아져 몸통에 묻히는 문제가 있었습니다. 파동 배율을 방패의 카메라 투영 크기에 연결했습니다. 현재 미리보기는 간이 Canvas가 아닌 실제 LegacyCombatHud를 사용합니다.
- 캐릭터 PNG는 수정하지 않습니다. 기존 보호 대상 268개는 현재 main의 Git 내용과 동일합니다(텍스트 줄바꿈 정규화 비교). PNG 124장은 이전 아트 해시와도 바이트 단위로 같습니다.

## 파일

- 게임 시트: `Assets/Game/Resources/DuelVFX/ShieldBreak/shield-shatter.png`.
- 프레임 좌표: 같은 폴더 `shield-shatter-frames.json`. 시트와 JSON은 PixelLab 원본 그대로입니다.
- 로더: `Assets/Game/Presentation/DuelBreakShieldClip.cs`.
- 적용: `Assets/Game/Presentation/DuelBreakImpactCue.cs`.
- 제작 레시피: [pixellab-recipe.json](pixellab-recipe.json), [generation.json](generation.json).
- [PixelLab에서 다시 편집](https://pixellab.ai/create-vfx?effect=22fe6b44-52e6-4d9e-9bc4-ba5843ba81d0).
- 독립 이펙트 미리보기: [effect-preview.gif](effect-preview.gif). 최근접 3배 확대이며 게임에는 원본 해상도를 사용합니다.
- 전장 렌더 미리보기: [unity-preview.gif](unity-preview.gif), [파괴 순간](unity-shatter.png). 실제 Arena와 UI를 사용한 테스트 렌더이며 양쪽 붕괴를 동시에 표시하도록 만든 검수 장면입니다. GIF 마지막에 반복 관찰을 위한 정지 시간을 더했습니다. 수동 전체 게임 플레이 영상은 아닙니다.
- 외부 원본 보관: `C:/Users/user/Documents/SwordMasterStory/Resistance_Shield_Shatter_20261025`.

## 검증

Unity 6000.5.9f1의 열린 에디터에서 관련 PlayMode 20개를 실행해 전부 통과했습니다.

| 검사 | 통과 |
|---|---:|
| BreakShieldPlayModeTests | 4/4 |
| BreakPresentationPlayModeTests | 7/7 |
| BreakQueueCancellationPlayModeTests | 5/5 |
| LocalVersusPlayModeTests | 4/4 |

위치 추적, 카메라 이동·확대에 따른 크기, 카메라 뒤 대상 숨김, 잘못된 시간 입력, 양쪽 독립 재생·만료·초기화, 실제 컨트롤러의 신규 붕괴와 추가 피격 시 비반복을 확인했습니다. 검수용 전장 14프레임을 렌더해 시각 확인도 했습니다. 첫 렌더에서 발견한 플레이어 수평 치우침을 보정하고 최종 검사를 다시 통과했습니다. 검증용 임시 Editor 실행 스크립트는 제거했습니다.

[검증 요약](validation.json), [원본 테스트 결과](playmode.xml). 전체 프로젝트 테스트나 출시 빌드 검증을 새로 수행한 결과는 아닙니다.

파동 크기 수정 전에는 실제 HUD 배율·카메라 확대 회귀 검사 1개가 실패했고([수정 전 결과](wave-scaling-before.xml)), 수정 후 통과했습니다. 실제 HUD 렌더에서 파동 켜짐/꺼짐의 픽셀 차이도 검사합니다. [수정 전 화면](wave-scaling-before.png)과 [수정 후 화면](unity-shatter.png)을 보존합니다. 수동 전체 플레이 영상이 아닌 실제 HUD를 사용하는 자동 검증 장면입니다.

## 최신 main pull 이후 검증

- 전체 EditMode: **2,391 통과 / 0 실패**.
- 관련 PlayMode: **123 통과 / 0 실패**. 붕괴·방패·예약 취소·로컬 대전과 새 main의 고속 집중·실제 공격 그림·긴장 표시, 기존 고속 시계·MobStudent 애니메이션·무기 잔상 검사를 포함합니다. 전체 PlayMode를 다시 실행한 결과는 아닙니다.
- 기존 보호 대상 리소스 268개는 pull한 main과 동일합니다. 텍스트는 줄바꿈을 정규화해 비교했으며 새 방패 리소스는 별도로 추가했습니다.
- 결과와 실행 클래스 목록: [pull-validation.json](pull-validation.json).
- 원본 테스트 XML 보관: `C:/Users/user/Documents/SwordMasterStory/Shield_Pull_Integration_20261025/test-reports.zip`.
- 검증용 임시 Editor 실행 스크립트는 커밋하지 않습니다. stash는 복구용으로 유지합니다.
