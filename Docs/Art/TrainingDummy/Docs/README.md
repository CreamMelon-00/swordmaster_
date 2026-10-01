# 연습장 허수아비

기존 플레이어·에너미의 갈색/금색 팔레트에 맞춘 볏짚 훈련용 허수아비입니다. 실제 캐릭터 팔레트에서 가져온 28색으로 정리했습니다.

## Aseprite 원본

- `TrainingDummy.aseprite`: 256×224 RGBA, 18프레임, `Idle`/`Hurt` 태그.
- `Idle`: 1~8프레임, 각 180ms, 총 1.44초 반복. 받침대를 고정한 채 상체가 작게 흔들립니다.
- `Hurt`: 9~18프레임, 총 0.70초. 왼쪽에서 맞아 오른쪽으로 젖혀지고, 반동을 거쳐 대기로 복귀합니다.
- 레이어: 고정 받침대 / 나무 지지대 / 볏짚 몸체 / 피격 시 떨어지는 볏짚.
- 프레임마다 크기를 바꾸지 않습니다. 상체 회전과 지지대 변형으로 반응하며, 마지막 피격 프레임은 첫 대기 프레임과 동일합니다.

## 내보내기

- `TrainingDummy-sheet.png`와 `.json`: 8열, 2048×672, 프레임별 사각형·재생 시간·태그 포함. 마지막 행의 빈 칸은 프레임이 아닙니다.
- 개별 투명 PNG 18장은 Unity의 `Assets/Game/Art/TrainingDummy/Frames/`에 있습니다.
- `idle-preview.gif`, `hurt-preview.gif`: 확대 미리보기입니다. 피격 GIF는 확인을 위해 반복되지만 Unity 피격 클립은 1회 재생입니다.
- `style-comparison.png`: 동일 배율의 플레이어 / 허수아비 / 에너미 비교.

## Unity 사용

`Assets/Game/Art/TrainingDummy/TrainingDummy.prefab`을 원하는 연습장 위치에 배치합니다. 이미지 설정은 Point, 40 PPU, 무압축, 밉맵 없음, 공통 피벗 `(128,22)`입니다. 기본 Transform 크기는 `(1,1,1)`이며, 받침대 하단이 오브젝트의 지면 기준과 맞도록 구성했습니다.

프리팹은 SpriteRenderer와 Animator를 포함합니다. 기존 전투 캐릭터와 같이 URP Sprite-Unlit 머티리얼을 사용합니다. `Idle.anim`이 기본 상태이며, Animator의 `Hurt` 트리거로 `Hurt.anim`을 재생합니다. 피격 중 다시 호출하면 반응을 처음부터 재생합니다.

```csharp
dummyAnimator.SetTrigger("Hurt");
```

이번 작업은 도트·애니메이션 리소스와 독립 프리팹 제작입니다. 연습장 씬 배치, 충돌체, 피해 판정 및 타격 이벤트 연결은 포함하지 않습니다.

## 제작 및 검증

내장 이미지 생성 도구로 형태 원본을 생성한 뒤, Aseprite Lua로 색상 정리·픽셀 편집·레이어 구성·애니메이션 제작 및 내보내기를 수행했습니다. 실제 프롬프트는 `PROMPTS.md`, 재현 스크립트는 `build_dummy.lua`, Unity 클립 제작 스크립트는 `BuildTrainingDummyAssets.cs`입니다.

`verification.txt`: Aseprite 원본 재열기, 프레임·태그·레이어 수, 불투명 전경/투명 배경, 잘림 없음, 받침대 고정 및 피격 후 복귀 검증.

`unity-verification.txt`: 별도 Unity 6000.5.9f1 프로젝트에서 스프라이트 임포트, 18개 프레임의 클립 샘플링, 재생 길이·반복 설정 및 Animator 연결 구조를 검사한 결과입니다. 실제 연습장 전투 플레이 테스트를 뜻하지 않습니다.
