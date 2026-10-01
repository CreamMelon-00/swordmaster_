# 연습장 허수아비 — 다른 Codex에게 전달하는 패키지

이 ZIP은 완성된 허수아비 도트와 대기·피격 애니메이션을 다른 Unity 프로젝트에 반영하기 위한 전달본입니다. 기존 리소스를 사용해 연결 작업을 진행하면 됩니다. 다시 그리거나 이미지를 새로 생성할 필요가 없습니다.

## 먼저 확인할 파일

- `Unity/Assets/Game/Art/TrainingDummy/TrainingDummy.prefab`: 배치할 프리팹.
- `Aseprite/TrainingDummy.aseprite`: 편집 가능한 원본. 4개 레이어, 18프레임, Idle/Hurt 태그.
- `Previews/idle-preview.gif`, `Previews/hurt-preview.gif`: 동작 확인.
- `CODEX_HANDOFF.txt`: 다음 Codex에게 그대로 붙여넣을 작업 설명.
- `Docs/README.md`: 제작 규격과 사용 설명.

## 포함한 자료

| 폴더 | 내용 |
| --- | --- |
| Unity | PNG 18장, 임포트 메타 파일, Idle/Hurt 클립, Animator Controller, 프리팹 |
| Aseprite | 완성된 편집 원본 |
| Exports | 투명 PNG 개별 프레임, 스프라이트 시트와 프레임별 시간 JSON |
| Previews | 대기·피격 GIF, 정지 이미지, 주요 피격 프레임, 캐릭터 스타일 비교 |
| Source | 이미지 생성으로 만든 형태 원본 |
| References | 재현 스크립트가 사용하는 플레이어·에너미 팔레트 원본 및 비교용 PNG |
| Tools | 다른 경로에서도 실행할 수 있는 Aseprite 재현 도구, Unity 클립 재생성 도구 |
| Provenance/OriginalScripts | 기존 제작 당시 스크립트. 당시 컴퓨터 경로가 포함되어 있어 참고용으로만 보관 |
| Validation | Aseprite·Unity 검증 결과, 전달본의 재현 검증 기록 |
| Docs | 규격 설명과 실제 생성 프롬프트 |

## Unity로 가져오기

1. 대상 프로젝트의 Unity 버전과 렌더 파이프라인, 연습장 씬, 피해 처리 경로를 먼저 확인합니다. 원본은 Unity **6000.5.9f1**, URP **17.5.0**에서 검증했습니다. 다른 버전과 파이프라인에서의 동작은 별도 확인이 필요합니다.
2. `Unity/Assets/Game/Art/TrainingDummy/`와 그 옆의 `TrainingDummy.meta`를 프로젝트의 대응 위치에 복사합니다. **PNG만 복사하지 말고 모든 `.meta`를 함께 유지**해야 클립과 프리팹 참조가 유지됩니다. 기존 프로젝트의 `Assets`, `Game`, `Art` 폴더 메타를 덮어쓰지 않습니다. 동명의 리소스가 있으면 먼저 내용을 비교합니다.
3. 프리팹의 외부 의존성은 URP의 `Sprite-Unlit-Default` 머티리얼입니다. 패키지 GUID는 `9dfc825aed78fcd4ba02077103263b40`, 패키지 경로는 `Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat`입니다. URP가 다른 버전이거나 머티리얼이 누락되면 대상 프로젝트의 동등한 Unlit Sprite 머티리얼을 직접 지정합니다. Built-in/HDRP 프로젝트는 그 파이프라인에 맞는 머티리얼로 교체해야 합니다. 이를 위해 대상 프로젝트 전체의 렌더 파이프라인을 바꾸지 않습니다.
4. `TrainingDummy.prefab`을 연습장에 배치합니다. 기본 Transform 스케일은 `(1,1,1)`입니다. SpriteRenderer와 Animator만 있으며, 충돌체와 피해 처리 컴포넌트는 없습니다.
5. 대상 게임의 기존 피해 이벤트에서 실제 타격이 성립할 때 다음을 호출합니다.

```csharp
dummyAnimator.SetTrigger("Hurt");
```

6. 타격당 1회 호출하고 Update에서 매 프레임 호출하지 않습니다. 피격 중 재타격하면 Hurt를 처음부터 다시 재생하도록 Controller가 구성되어 있습니다. 실시간 Animator 기반이므로 전투 전용 시간/히트스톱 시스템이 있으면 그 시간 규칙에 맞춰 연결합니다.
7. 기본 대기, 연속 타격, 반복 피격 후 복귀, 바닥 정렬, 좌우 공격 방향, 카메라 배율, 렌더 순서를 실제 연습장 Play Mode에서 확인합니다. Collider2D 등 판정 영역을 추가할 때는 회전하는 상체 전체가 프레임마다 판정을 바꾸지 않도록 게임의 기존 방식을 따릅니다.

## 규격

- 공통 캔버스: **256×224**, 투명 배경, 28색.
- 임포트: **40 PPU**, Point, 밉맵 없음, 무압축.
- 피벗: PNG 왼쪽 아래 기준 **(128,22)**, 정규화 **(0.5, 0.09821428571428571)**.
- Idle: **8프레임 × 180ms = 1.44초**, 반복.
- Hurt: **10프레임**, 시간 `[35,45,55,70,65,65,65,80,100,120]ms`, 총 **0.70초**.
- 피격 방향: 왼쪽에서 타격을 받아 오른쪽으로 젖혀짐. 반대 방향 공격은 필요에 따라 SpriteRenderer.flipX 등을 적용하되 씬에서 확인합니다.
- 마지막 피격 이미지와 첫 대기 이미지가 같습니다. 받침대는 고정이며 프레임별 크기 변경이 없습니다.
- 시트: **2048×672**, 8열. JSON에 기록된 18칸만 프레임이며 나머지 빈 칸은 사용하지 않습니다. 시트 직접 임포트 시 Max Size를 2048 이상으로 설정하고 Point/무압축을 유지합니다.
- GIF는 비교용 확대 미리보기입니다. 피격 GIF의 반복과 10ms 단위 시간 반올림은 실제 Unity 클립의 1회 재생 및 5ms 단위 타이밍과 구분합니다.

## 선택 사항: Aseprite에서 재현

이미 완성된 리소스를 사용하는 경우 실행할 필요가 없습니다. 수정하거나 재현할 때만 Windows PowerShell에서 다음을 실행합니다.

```powershell
& .\Tools\Rebuild-Aseprite.ps1 -AsepritePath 'C:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe'
```

압축을 푼 위치에 관계없이 포함된 Source/References를 사용합니다. 결과는 기본적으로 `Rebuilt/`에 저장되어 전달된 완성본을 덮어쓰지 않습니다. `-OutputDirectory`로 다른 출력 폴더를 지정할 수 있습니다. 스크립트는 Unity 프로젝트로 자동 복사하지 않습니다.

`Tools/BuildTrainingDummyAssets.cs`는 Unity Editor용 재생성 스크립트입니다. 이미 제공된 `.anim`, `.controller`, `.prefab`을 가져올 때는 필요하지 않습니다. 재생성이 필요하다면 임시 Editor 폴더에 두고 `BuildTrainingDummyAssets.Run`을 실행할 수 있습니다. 같은 `Assets/Game/Art/TrainingDummy` 경로의 클립/Controller/프리팹을 갱신하므로 편집한 파일이 있다면 먼저 비교·보존합니다.

## 검증 범위와 남은 작업

완료: Aseprite 원본 재열기, 투명도·잘림·바닥 고정·복귀 검사, Unity에서 18개 프레임 샘플링, 임포트 규격과 클립 길이 검사, Controller의 전이 구성 검사, 전달 파일 해시 검증.

남은 작업: 대상 게임 연습장 배치, 필요한 충돌·피해 이벤트 연결, 대상 프로젝트 실제 Play Mode 확인. 이 패키지가 이미 대상 게임에 연결되었다고 가정하지 않습니다.

`MANIFEST.sha256`은 전달본 내부 파일의 SHA-256 목록입니다. 패키지에는 Unity Library/Temp, 전체 게임 프로젝트, 계정 관련 로그는 포함하지 않았습니다.
