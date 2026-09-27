# 캐릭터 애니메이션 고정 수정

환경: Unity 6000.5.9f1 / Windows / 원본 첫 전투 이식.

## 증상과 재현

접근·넉백·카메라·불꽃은 움직이지만 Player와 Enemy0의 스프라이트는 대기 첫 프레임에 고정됐다. 이전 테스트는 클립 로딩과 이벤트 제거만 확인했고, 실제 프레임 변화는 검사하지 않았다.

임시 프로젝트에서 추가한 실제 SpriteRenderer 검사 3개는 수정 전 모두 실패했다. 대기 두 번째 프레임과 공격 첫 프레임을 기대해도 실제 값은 `pa_player_idle-Sheet_0`이었다. 기록은 `Logs/AnimationBaselineResults.xml`이다.

## 원인 증거

진단은 원본 에셋을 바꾸지 않고 별도 GameObject에서 수행했다. `AnimationUtility`로 확인한 클립 바인딩은 루트의 `SpriteRenderer.m_Sprite`이며 두 타격 프레임 참조 모두 null이 아니었다.

| 대상 | SampleAnimation(0) | SampleAnimation(0.1) |
| --- | --- | --- |
| Player, SpriteRenderer만 존재 | idle-Sheet_0 | idle-Sheet_0 |
| 같은 객체 + Animator | slash-Sheet_0 | slash-Sheet_1 |
| Enemy0, SpriteRenderer만 존재 | i-Sheet_0 | i-Sheet_0 |
| 같은 객체 + Animator | s-Sheet_0 | s-Sheet_1 |

따라서 이 환경에서는 비레거시(`m_Legacy=0`) 클립의 스프라이트 참조 곡선을 샘플링할 대상에 Animator가 없는 것이 원인이었다. 파일 경로·GUID·스킬 이름이나 전투 시간이 원인은 아니었다. 실제 프레임 이름과 곡선 값은 `Logs/AnimationBaseline.log`의 `AnimationProbe` 행에 보관한다.

## 수정과 회귀 방지

`LegacyArenaView.CreateActor`에서 캐릭터마다 Animator를 추가한다. RuntimeAnimatorController는 연결하지 않아 기존 전투의 수동 시계가 여전히 재생을 소유한다. 원본 클립·스프라이트·12fps·1/12초 타격·다단 공격 반복·느린 재생·카메라·파티클·UI는 변경하지 않는다. 레거시 공격 이벤트도 다시 붙이지 않는다.

`LegacyAnimationPlayModeTests`는 두 캐릭터의 실제 스프라이트를 검사한다.

- 대기 8프레임 전체와 반복.
- 기본 9개 기술의 준비/타격 프레임 및 매 타격 반복.
- 실제 시간이 지나도 재생 시간이 0이면 프레임 유지.
- 공격 자세에서 직접 Reset 시 대기 첫 프레임 복원, EndTurn 시 대기 복귀.
- 1회/3회 공격에서 각 캐릭터가 독립적으로 대기 복귀, 타격 시점 전후의 프레임 전환.

최종 전체 테스트와 별도 Game View 화면 검증 결과는 `Validation.md`를 따른다. 진단용 임시 스크립트는 게임 Assets에 남기지 않는다.
