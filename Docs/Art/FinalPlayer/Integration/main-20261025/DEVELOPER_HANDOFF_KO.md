# 개발자 인계: 아트 통합 보고와 실패 테스트 정리

기준일: 2026-10-25. 대상: `art`의 아트 통합 커밋 `f6fea1a`, 포함된 main `773dec6`. 이 문서는 수정 과제 목록이며, 아래 문제를 수정했다는 보고가 아닙니다. 사용자의 최종 지시에 따라 main 통합과 원격 푸시는 하지 않습니다.

## 판단 범위

**이번 main 병합으로 추가된 실패 17개는 아트 없는 최신 main에서도 같은 메시지로 재현됐습니다.** 이 17개를 이번 아트 병합만의 회귀로 볼 근거는 확인되지 않았습니다.

그러나 **전체 실패 102개가 모두 main 개발 중 발생한 게임 버그라는 뜻은 아닙니다.** 기존 art 기준에서도 실패하던 85개에는 오래된 테스트 기대값, 새 아트 적용에 따른 테스트 호환 작업, 원인을 추가 조사해야 할 연출·입력·레이아웃 문제가 섞여 있습니다. 아트 호환 테스트의 보완은 통합 측에서 함께 정리할 과제이며 main 개발자만의 결함으로 넘기지 않습니다.

| 구분 | 검증 결과 |
|---|---|
| EditMode 전체 | 병합 전 2,257/2,257 통과 → 병합 후 2,300/2,300 통과 |
| PlayMode 전체 | 병합 전 880 통과·85 실패·1 건너뜀 → 병합 후 913 통과·102 실패·1 건너뜀 |
| 추가 실패 | 17개. 최신 main 단독에서 해당 17개만 실행해 모두 동일한 메시지로 실패 |
| 아트·신규 연출 관련 통과 | FinalPlayer 16/16, 로비 10/10, MobStudent 5/5, 무기 잔상 2/2, 기술 템포 연출 5/5, 고속 기술 50/50 |
| 리소스 보존 | 보호 대상 268개 모두 SHA-256 동일. PNG 124장 포함 |

## 우선 수정·조사 목록

우선순위는 **인계용 제안**입니다. 실패 수는 버그 수가 아닙니다. 하나의 데이터 변경이 여러 테스트를 실패시킬 수 있습니다.

### 1. main 신규 실패 17개: 밸런스 변경과 테스트 시나리오 동기화

대부분 실제 값이 바뀐 스킬 시트를 따라가는데 테스트는 이전 수치를 기대합니다. 의도한 밸런스가 맞는지 먼저 확정하고, 표시가 실제 규칙과 일치하면 테스트를 고칩니다. 테스트를 통과시키려고 게임 수치를 이전 값으로 돌리지 않습니다.

| 과제 | 해당 테스트 | 확인된 사실 / 수정 방향 |
|---|---|---|
| 쿠페의 붕괴 테스트 조건 재설계 | BreakPresentation 2개 + BreakQueueCancellation 1개 | 붕괴 여부를 True로 기대하나 False. 두 BreakPresentation 시나리오는 적 저항 15를 설정하며 현재 쿠페(ID 42)의 직접 감소는 10. 적 저항·준비 기술·효과 조건을 검토해 실제 붕괴를 만드는 시나리오와 붕괴하지 않는 시나리오를 분리. 연출 고장으로 단정하지 말 것 |
| 위력 표시와 숙련 보정 | CampaignLoadoutHud 1개 + SkillInfoView 3개 | 기대 5–6/3–4/9–10, 실제 4–5/2–3/8–9. 현재 베기 기본 위력은 2–3. 기본값과 숙련 보정값의 기준을 갱신하고 선택 해제 후 오래된 표시가 남지 않는 원래 검증 목적 유지 |
| 저항 직접 감소와 상한 | DuelResistanceFeedback 1개 + SkillActivationCue 2개 | 기대 감소 12/13/15, 실제 10. 현재 쿠페 감소 10에 맞춰 초기 저항·조건·실제 감소량을 확인. 저항을 초과해 감소하지 않음, 0 효과에서는 연출 없음 검증 유지 |
| 호흡 임계 횟수 | KeywordHud 1개 | 기대 숨 0/4, 실제 0/3. 현재 규칙을 확인해 배지·설명·발동 테스트를 함께 동기화 |
| 돌파 위력 보너스 | KeywordHud 1개 + StatusStrip 1개 | 기대 +10, 실제 +16. 현재 돌파(ID 60) 시트에 16 명시. 보호 소모 시점과 취약 상태 유지 검증은 그대로 유지 |
| 추격 발동 횟수·보너스 | KeywordHud 2개 | 기대 총타수 0/5·보너스 12, 실제 0/4·보너스 14. 현재 추격(ID 61)은 총타수 4 이상·보너스 14. 취소된 예약의 반영 여부는 별도 확인 |
| 방어 대응 감소량 | SkillInfoView 1개 | 기대 저항 -15, 실제 -18. 허점 노리기(ID 6)의 현재 감소 18과 일치하는지 확인 후 표시 테스트 동기화 |
| 순환 위력·비용 표시 | SkillLaneStyle 1개 | 기대 10–15, 실제 11–16. 사용 횟수 누적에 따른 위력·ACT 비용·배지를 현재 정의와 함께 검증 |

위 표는 정확히 17개 테스트를 묶은 것입니다. 전체 테스트 이름, 개별 실패 메시지와 코드 위치는 [실패 목록 CSV](failure-inventory.csv)의 `최신 main 단독 재현` 행을 확인합니다.

### 2. 기존 실패: 예외와 상호작용부터 조사 — 우선순위 높음 제안

- `HiddenQueueHudPlayModeTests.OpponentCards_KeepOnlyProperty_AndTheSameHudRestoresOwnAndSinglePlayerArt`: `AssertRevealed`에서 NullReferenceException. 현재 테스트의 `card.Find("Icon")` 결과를 바로 사용하는 부분이 있어 카드 계층 변경과 조회 실패를 먼저 조사. 실제 게임 크래시라고 확정한 결과는 아닙니다.
- `SkillInfoViewPlayModeTests.RecoverySteal_ShowsItsAttackConditionNextTurnTimingAndOncePerTurnBadge`: 테스트 helper가 `Missing authored skill 62` 예외를 발생시킵니다. 현재 skills.csv에는 ID 62 행이 있으므로 파일 누락으로 단정하지 말고, 로더의 포함 조건·구분 필터와 테스트 조회 대상을 확인합니다.
- `SkillLaneStylePlayModeTests.CombatInputs_KeepStandaloneQweKeysAndAddNonBlockingSchoolNames`: 콜백 인덱스 기대 0, 실제 -1. `StatusStripPlayModeTests.IiaJealousy_CanBeExplainedAtZeroBeforeCombatAndAfterConsumption`: 상태 아이콘 마우스 적중 수 0. 실제 클릭 전달·가림·raycast 조건과 테스트 좌표를 확인합니다.
- `StepInputPlayModeTests.PressureMultiHit_AfterFirstImpactWaitsForContactAgainWhileKnockbackIsMoving`: 기대 True, 실제 False. 다단 공격 첫 타 이후 재접촉 대기와 넉백 중 타격 진행을 조사합니다.

이 항목들은 이번 머지 전에도 실패했습니다. 최신 main 단독에서는 위 17개 외 전체 검사를 실행하지 않았으므로, 해당 항목의 현재 main 재현 여부와 원인은 별도 확인해야 합니다.

### 3. 기존 실패: 개발 데이터·UI 사양과 낡은 기대값 정리

- CampaignFlow/Curriculum/Loadout/Result/SkillLaneStyle: 시작·보상·수련 기술 수 9↔11, 10↔12, 11↔13 등의 불일치. 시험 기술 601·602의 지급 의도와 저장 호환을 확인하고, 초기 보유 목록·미편성 목록·리셋·보상 시나리오를 동기화합니다.
- Curriculum: 연결선 25↔33, 트리 높이 1080↔1752, 스크롤 크기 620↔1292. 현재 트리 구조를 기준으로 갱신하되 카드 겹침·스크롤 범위 검증을 유지합니다.
- HiddenQueue/QueueComparison: 상대 카드의 대기/속성 표시, 공개 시작 시점, 숨김 배지, 공개된 상대 정보 범위가 기대와 다름. 현재 공개 규칙을 확인하고 숨긴 정보가 노출되지 않는 검증을 우선합니다.
- SkillInfo/KeywordHover/CompactHud: 변경된 설명·회복 조건·ACT 가격·아이콘 범위의 기대값 불일치. 규칙 원문과 실제 UI를 비교합니다. 기존 설명 문자열에 맞추기 위해 최신 설명을 무조건 되돌리지 않습니다.

### 4. 기존 실패: 새 아트와 테스트 호환 작업

- LegacyAnimation, LocalVersus, EnemyVariants, BladeBindDamage, BattleBulletTime, HurtReactionArena, ResistanceImpactArena, PressureAttackTrail 등의 테스트가 `idle-frame-*`, `slash-*-frame-*`, `block`, `move-frame-*` 같은 이전 리소스명을 기대하지만 실제는 `final-*`입니다.
- DuelPrototype/ForestDuel의 PPU 기대 40과 실제 36 불일치도 새 아트 스케일 계약을 먼저 확인해야 합니다. 원래 크기로 되돌릴 근거가 아닙니다.
- BladeExchangeContact/BladeExchangePose의 절차적 잔상 개수 기대와 실제 개수가 다릅니다. 플레이어는 PNG에 그려진 잔상을 쓰고 구형 절차적 잔상 중복을 억제하도록 의도돼 있습니다. 두 표현 경로를 구분해 검증합니다.
- 이름 비교만 지워 테스트를 무력화하지 말고, 현재 FinalPlayer의 공격 속성·접촉 프레임·반복 타격·가드·피격·정지/재생/초기화 동작을 검사하도록 갱신합니다. 구형 경로 자체를 검증할 테스트라면 해당 경로를 명시적으로 선택합니다.

### 5. 기존 실패: 카메라·레이아웃·반응 연출 조사

- BattleIntro/SlowMotionFraming/StepFeedback/QueuePanel/EnemyRosterHud: 카메라 거리, 플레이어 화면 위치, 발과 HUD 간격, 상대 상태창 위치 기대값 이탈. 대표 화면비에서 렌더링하고 실제 가림·잘림 여부를 확인합니다.
- CampaignLobby/CompactSkillCard/BreathingHud/FixedHud: 작은 텍스트 높이 차이부터 큐가 두 줄이 되는 위치 차이까지 섞여 있습니다. 폰트 측정 오차와 실제 줄 잘림·레이아웃 실패를 구분합니다.
- AttackRhythm: 상대 cadet-a 참격 기대 프레임 02, 실제 03. 현재 준비/마무리 시점과 리듬 곡선을 기준으로 확인합니다.
- HurtReactionController/EnemyReaction/DuelResistanceFeedback/BattleBarks: 붕괴 상태의 피격 조건, 가드/피격 우선순위, 초기 저항 변화 표시, 컷신 시작 타격의 말풍선 처리를 확인합니다.
- ForestDuel.NextTurn의 위치 오차는 기대 ≤0.181, 실제 약 39.979로 큽니다. 단순 허용오차 확장 대신 좌표계·초기 위치·재시작/다음 턴 이동을 조사합니다.

## 전체 실패 목록과 재현 자료

- [실패 목록 CSV](failure-inventory.csv): 실패 102개를 누락 없이 수록. 구분, 인계용 우선순위, 제안 조치, 소스 위치, 원본 실패 메시지를 포함합니다. 분류·우선순위는 원인 확정이 아닙니다.
- [병합 보고서](README_KO.md), [검증 원본 요약](summary.json).
- 최신 main 단독에서 검증한 범위는 **추가 실패 17개**뿐입니다. CSV에는 지난 10월 24일 main에서 재현됐던 기존 항목도 별도 표기하되 최신 main 재확인으로 간주하지 않습니다.
- 원본 XML·로그: `C:/Users/user/Documents/SwordMasterStory/Integration_ArtMain_20261025/test-reports.zip`.
- 기존 실패 85개도 기능별로 필터링해 수정 전후 비교합니다. 정적 코드 검사·테스트 실패만으로 실제 게임의 모든 증상을 확인했다고 간주하지 않습니다.
- 수정 완료 조건: 의도한 데이터/공개 규칙을 확정하고 관련 EditMode·PlayMode를 재실행, 실제 화면·입력 문제가 의심되는 항목은 에디터에서 재현/해결 확인. 남은 실패를 목록에서 추적합니다.

## 아트 유지 조건

보호 대상은 이번에 승인된 FinalPlayer 리소스, 피벗·PPU, 프레임 시간표와 연결입니다. 오래된 테스트를 맞추기 위해 플레이어를 이전 이미지로 되돌리거나, 현재 PNG 잔상과 구형 절차적 잔상을 중복 출력하지 않습니다. 변경이 필요한 경우 새 아트의 기준을 유지하며 검증합니다.
