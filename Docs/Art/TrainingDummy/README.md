# 연습장 허수아비 (반영 기록)

2026-09-30. 친구가 전달한 허수아비 패키지(`허수아비.zip`)를 서막 1 임무 `첫 타격`의 적으로 반영했다. 도트·애니메이션은 새로 그리거나 바꾸지 않았다. 원본 설명은 이 폴더의 `START_HERE.md`와 `Docs/README.md`에 있다.

## 게임에 들어간 것

- 프레임 18장과 친구의 `.meta`를 그대로 옮겼다: `Assets/Game/Resources/TrainingDummy/Animations/idle/frame-01~08.png`, `hurt/frame-01~10.png`. GUID·피벗 `(128,22)`·40 PPU·Point·무압축 설정을 유지했다. 폴더 `.meta`도 친구 것을 썼다.
- 패키지의 `Idle.anim`·`Hurt.anim`·`TrainingDummy.controller`·`TrainingDummy.prefab`은 가져오지 않았다. 이 게임은 씬 프리팹과 Animator를 쓰지 않고 코드에서 Resources의 프레임을 직접 재생하기 때문이다(`TrainingDummyAnimationSet`). 같은 이유로 URP 머티리얼 참조도 필요 없다. 전장이 만드는 Sprite-Unlit 머티리얼을 쓴다.
- 이 폴더에는 편집 원본(`Aseprite/`), 미리보기, 시트, 형태 원본, 재현 도구, 검증 기록을 보관한다. 프레임 PNG 중복본과 Unity 폴더는 게임 쪽 파일과 겹치므로 넣지 않았다.

## 재생 방식

- **대기**: 8프레임 × 180ms = 1.44초 반복.
- **피격**: 10프레임 `[35,45,55,70,65,65,65,80,100,120]ms` = 0.70초 1회, 끝나면 대기 첫 프레임부터 다시 흔들린다(마지막 피격 프레임과 첫 대기 프레임이 같다). 피격 중 다시 맞으면 처음부터 재생한다.
- 두 클립 모두 전투 시계로 돈다. 히트스톱 동안 멈추고, 결정적 순간의 느린 화면에서는 느려진다. 다른 캐릭터에 쓰이는 전체 재생 배속(0.6)은 적용하지 않아 친구가 정한 시간을 지킨다.
- 받침대에 고정된 허수아비이므로 맞아도 밀려나지 않고, 접근 단계에서도 걸어 나오지 않는다. 플레이어만 다가간다.
- 왼쪽에서 맞아 오른쪽으로 젖혀지는 그림이라 반전하지 않는다(플레이어는 항상 왼쪽에서 공격한다).
- 1 임무의 적은 공격·방어를 하지 않으므로 대기와 피격만 쓰인다. 공격·방어 프레임은 없다.
- 브리핑의 적 실루엣도 허수아비 대기 첫 프레임을 쓴다. 적 이름은 `허수아비`.

## 코드

- `Presentation/TrainingDummyAnimationSet.cs`: 프레임 로드(다른 캐릭터와 같은 지면 보정), 대기·피격 샘플링.
- `LegacyArenaView.SetEnemyAppearance`: 전투마다 적 외형을 고른다. 허수아비 아트가 없으면 경고를 남기고 학생으로 대체한다. `HasRequiredAssets`에 허수아비 18장도 포함한다.
- `Runtime/Prologue`: `EnemyAppearance`(Student / TrainingDummy), `MissionEnemy.Appearance`. 1 임무만 `TrainingDummy`이다.

## 확인하지 못한 것

Unity를 실행할 수 없는 환경에서 반영했다. 실제 화면의 크기·바닥 정렬·연속 피격·대기 복귀·다른 캐릭터와의 스타일 차이는 Play Mode에서 확인해야 한다.
