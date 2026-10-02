# Architecture

## 목표

전투 규칙과 Unity 표현 계층을 분리해 규칙을 빠르게 테스트하고, 화면·애니메이션·입력 변경이 판정 코드에 영향을 주지 않도록 한다.

## 의존 방향

```text
Presentation  ->  Runtime  ->  Core
Core.Tests    ->  Core / Runtime
Presentation.Tests -> Presentation / Runtime
```

- `Core`: 순수 C# 전투 상태와 규칙. `UnityEngine`을 참조하지 않는다.
- `Content`: 추후 ScriptableObject와 검증된 로컬 데이터로 스킬·적·스테이지를 정의할 영역이다. 현재 기술 19종의 데이터는 CSV 기술 시트 `Resources/Skills/skills.csv`에 있고, Runtime의 `LegacySkillSheet`가 읽는다(`SkillSheet.md`).
- `Runtime`: 현재 결투의 상태·기술열·ACT·큐·타격 판정·턴 전환·승패를 소유한다. `CampaignRun`은 별도로 8스테이지 진행·재화(지금은 화면에 표시하지 않음)·커리큘럼 진행·보유 기술을 소유하고, 스토리가 연 전투 기능과 스테이지 제한(`Features`/`StageLimit`)을 받아 스테이지 전투에 적용한다(`Curriculum.md`, `StoryUnlocks.md`). `Dialogue`는 Unity 비의존 텍스트 파서와 선형 재생 세션을 소유한다. `Save`(`GameSave`, `GameSaveCodec`)는 스토리(서막·수련) 진행과 캠페인 저장 상태를 묶는 임시 자동 저장 형식과 규칙 검사를 소유하고, 파일 입출력은 Presentation의 `GameSaveStore`가 맡는다(`SaveSystem.md`).
- `Presentation`: 입력, UI, 애니메이션, 사운드, VFX를 Runtime의 전투 상태와 타격 결과에 연결한다. `DialogueHud`는 독립 모달 Canvas로 현재 대사를 표시하며 `DuelPrototypeController.StartDialogue`가 Resources 텍스트 로드와 입력 우선순위를 담당한다.
- `Editor/Dialogue`: 원문 `.txt`를 기준 데이터로 유지하는 Editor 전용 작성 창이다. 같은 Runtime 파서로 실시간 검사하고 파일 생성·자동 저장·빠른 입력·미리보기와 공용 화자 이미지 지정을 제공한다.
- `Editor`: 상단 `Turn Limbo` 메뉴는 `연출 튜닝 열기`(10, `PresentationTuning.md`), `다이얼로그 편집기`(20, `DialogueEditor.md`), `세이브 삭제`(30)·`세이브 폴더 열기`(31, `SaveSystem.md`), `기술 시트`(40, `SkillSheet.md`) 순서다. 다이얼로그 편집기 밖의 항목은 어셈블리 정의 없는 `Assets/Game/Editor/*.cs`(Assembly-CSharp-Editor)에 있다. `기술 시트` 창은 구글 시트를 CSV로 받아 같은 Runtime 파서와 코드 대조(`CampaignSheetCheck`)로 검사하고 바뀐 기술을 확인받은 뒤 시트 파일을 덮어쓴다. 같은 파일의 `AssetPostprocessor`가 시트를 가져올 때 편집 모드의 기술 표를 다시 읽게 한다. 창의 주소 설정 `SkillSheetSettings`(ScriptableSingleton)는 저장 값을 다시 읽으려면 클래스 이름과 같은 파일이 필요해 `SkillSheetSettings.cs`에 따로 있다.
- `Tests`: 전투 규칙과 데이터 검증을 우선 EditMode에서 실행한다.

현재 수직 슬라이스는 다음 어셈블리까지 구현한다.

- `TurnLimbo.Core`: 초기 단일 행동 실험용 판정. 현재 플레이 화면에서는 사용하지 않는다.
- `TurnLimbo.Runtime`: `LegacyCombat`의 원본 기반 큐 전투, 기술열, ACT, 체력·저항력, 승패와 재시작. UnityEngine 참조 없이 테스트한다. 이전 `Combat/DuelMatchSession`은 초기 실험 코드로 남아 있으며 현재 화면에 연결되지 않는다.
- `TurnLimbo.Presentation`: 월드 공간의 원본 전장, uGUI HUD, 키보드·마우스 입력, 수동 재생 시계와 자동 부트스트랩
- `TurnLimbo.Core.Tests`: Core와 Runtime의 EditMode 테스트
- `TurnLimbo.Presentation.Tests`: 한 판과 재대결을 검증하는 PlayMode 테스트

Play 시 `DuelPrototypeBootstrap`이 활성 씬에 컨트롤러가 없으면 생성한다. `DuelPrototypeController`는 전장·HUD·오디오를 구성하고 입력과 재생 흐름을 조정한다. 현재 화면은 `LegacyArenaView`의 카메라·`SpriteRenderer`·파티클과 `LegacyCombatHud`의 Canvas·Image·Text·Button으로 표현한다. 초기 IMGUI 데모는 현재 플레이 흐름에 사용하지 않는다.

다이얼로그 원문은 `Assets/Game/Resources/Dialogue/*.txt`에 두고 `DialogueScriptParser`가 순수 C# 데이터로 변환한다. 선택적인 `DialoguePortraitCatalog`는 Presentation 계층의 ScriptableObject로 Sprite 직접 참조만 소유하며, 정확한 화자 이름으로 기본 초상화를 찾는다. `DialogueHud`는 카드 뒤의 재사용 Image 하나를 좌·우로 옮기고 내레이션·미지정 화자에서는 숨긴다. 대화 중에는 로비·전투보다 먼저 입력을 소비하며, 닫힌 같은 프레임의 키가 뒤쪽 화면으로 전달되지 않는다. 작성 문법은 `DialogueAuthoring.md`, 도구 사용법은 `DialogueEditor.md`를 따른다. 자동 시작 지점은 서막 임무의 시작/종료 대사(`Dialogue/mission-NN-intro`/`-outro`, 지금은 `테스트` 한 줄)뿐이다. 로비의 대사는 버튼으로만 연다.

컷신(2026-10-01)은 `Assets/Game/Resources/Cutscene/*.txt`에 대사 문법과 연출 명령(페이드·레터박스·카메라·인물·그림·기다리기)을 함께 쓴다. Runtime의 `CutsceneScriptParser`가 연출 줄을 읽고 나머지를 `DialogueScriptParser`에 그대로 맡겨 줄 순서로 합치며, `CutscenePlayback`이 단계 순서·기다림·대사 넘김을 순수 C#으로 소유한다. Presentation의 `CutsceneDirector`는 전투가 멈춘 숲 전장의 두 인물과 카메라를 직접 움직이고, `CutsceneHud`(정렬 450)가 그림·띠·페이드를, 공용 `DialogueHud`(시네마틱 모드)가 대사를 맡는다. 컨트롤러의 `AdvancePresentation`은 컷신을 가장 먼저 처리하고, 화면 정리 경로가 컷신을 멈춘다. 지금 자동으로 나오는 것은 타이틀의 새 게임 뒤의 깨어남 오프닝뿐이다. 상세는 `Cutscene.md`를 따른다.

## 현재 전투 사이클

기술 조건 표시는 `LegacySkillConditions`의 판정을 전투와 공유한다. `LegacyCurrentSlot.PlayerFeedback/EnemyFeedback`는 실제 조건 성립·고유 효과 발동·위력 계산에 사용된 버프와 이번에 실제 부여한 다음 칸 버프의 불변 스냅샷을 제공한다. Controller는 슬롯 시작에 HUD로 전달하고, HUD는 상대 조건 후보와 다음 예약 칸을 밝은 청록 선·이중 테두리·큰 외곽 표식으로 구분한다. `SkillCardFeedbackGraphic`의 재사용 메시 상한은100 vertices/6개 버프 입자다. 양측 `StatusView`는 현재 위력/보호와 새 버프의 수치/초기 지속4개 줄을 재사용하고, 활성 줄만 위로 확장하되 게이지는 아래에 고정한다. HUD가 전투 상태나 난수를 바꾸지 않으며, 키 해제·턴 전환·재시작에 표시만 정리한다. 전투 중 버프만 입자 대상이며 자세한 계약은 `SkillConditionFeedback.md`를 따른다.

기술 이름·수치·효과·조건·역할·문구는 `LegacySkillDefinitions`의 정의 표 하나에서 읽는다. 표의 데이터는 CSV 기술 시트(`Resources/Skills/skills.csv`)이고, 처음 쓸 때 `LegacySkillSheet.Parse`가 모든 문제를 모아 검사한 뒤 읽는다. 시트가 커리큘럼과 코드가 쓰는 ID를 갖췄는지는 `CampaignSheetCheck`가 따로 대조한다. Play마다 Presentation의 `SkillSheetLoader`가 Resources 원본을 설치한다. 코드는 기술을 행 위치가 아니라 ID로 찾고(`LegacySkillDefinitions.Skill(id)`), 기술 이름을 쓰는 문구는 `{기술:ID}` 토큰을 읽을 때 바꾼다(`LegacySkillNames`). 새 기술은 시트에 행을 추가하며 기술 ID 분기를 새로 만들지 않는다(`SkillDefinitions.md`, `SkillSheet.md`). 준비/플레슈의 한 칸 버프는 기존 `SkillBuff` 수명을 사용하고, 르프리즈/쿠페의 직접 저항력 회복·감소는 `LegacyFighterState`만 변경한다. Controller는 슬롯 시작 전후의 실제 변화량을 `DuelResistanceFeedback`의 재사용 Text 2개에 전달한다. 이 표시는 실제 시간으로 갱신하고 피해·넉백을 만들지 않는다. 별도 매니저·씬 연결·체인 상태를 만들지 않는다. 일반 기술 4종 추가와 표시 계약은 `ImportedNonChainSkills.md`를 따른다.

피해 숫자·이동 연출(2026-09-27)은 기존 HUD/전장/설정에 확장했다. `LegacyCombatHud`는 월드 타격점과 화면 단위 상승·연타 간격, 실시간 등장/정착/유지/퇴장, 붉은 테두리·그림자와 최대32개 재사용 숫자를 소유한다. Controller는 강조 타격(저항 붕괴, 마무리 일격 또는 표시 피해12 이상) 플래그만 넘기고 입력받은 실제 시계를 HUD에 직접 전달한다. `LegacyArenaView`는 거리1.4/속도1.2 배율을 동작 시작 때 고정하며 사거리4/최소간격2.8/미완료밀림상한6/시계 구분을 유지한다. 기존 설정 자산 경로/필드를 보존하고 배율·기본 글자 크기168/강조 크기1.3을 추가한다. 순수 Runtime의 피해/ACT/큐·클립·카메라·파티클/빛은 변경하지 않는다. 아래 기존 거리/속도 설명보다 이 단락을 우선하며 사용법은 `PresentationTuning.md`를 따른다.

붕괴 표시(2026-09-30)는 Runtime을 바꾸지 않고 공개 상태 `IsResistanceBroken`만 읽는다. `LegacyArenaView`가 캐릭터마다 `DuelBreakAura`(별도 렌더러의 붉은 윤곽과 그림자 위 금 간 고리)를 소유하고 Controller가 매 프레임 상태를 넘긴다. Controller는 규칙 호출 전후를 비교해 붕괴 순간("붕괴 ×2")과 턴 시작 자동 회복("저항 회복")을 `LegacyCombatHud`의 별도 `State Callout` 풀로 알리고, 저항 피해만 입힌 타격 숫자를 강철색으로 표시한다. 기존 강조 타격·카메라 규칙은 그대로다. 자세한 내용은 `BreakPresentation.md`를 따른다.

교전 카메라(2026-09-27)는 `LegacyArenaView`가 현재 교전 중심의 가로 데드존과 실제 시간 보간을 소유한다. 일반 타격의 즉시 확대/무작위 위치 누적 대신 고정 화면 크기와 제한된 방향성 펄스를 사용하며 연타/포즈 변경은 구도를 재설정하지 않는다. 화면 크기·추적 반응·데드존·흔들림은 기존 `DuelPresentationSettings`에 추가했다. 스텝 집중과 저항 붕괴의 대상 추적/우선순위는 유지한다. 전투 Runtime·클립·VFX·배경·머리 위 HUD와 Inspector 자산 연결은 바꾸지 않는다. 아래 기존 카메라 설명보다 이 단락을 우선하며 기본값과 튜닝은 `PresentationTuning.md`를 따른다.

검술 분류는 기존 `LegacySkill.LaneIndex`를 Q 정공·W 강공·E 기교로 표시한다. `SkillLaneStyle`의 이름/키/색 매핑과 `SkillLaneBadge`의 재사용 비차단 표식은 Presentation만 소유한다. 전투 QWE 버튼·전투 양측 설명·편성 제목/필터/선택 상세·커리큘럼 상세에서 공유하며 적 설명에는 플레이어 입력 키를 숨긴다. 공격 타입 `Property`와는 별개이며 편성/피해 규칙은 바꾸지 않는다.

현재 스킬 정보(2026-09-27)는 커리큘럼·편성·전투 설명이 `SkillInfoView`를 공유한다. 실제 Skill의 ACT/위력/공격 타입·타격 수는 카드 테두리의 인장·금속판·리본으로, 고유 효과는 각인 기호+의미별 색의 키워드/조건·시간 범위로 표시한다. 배지 바탕은 불투명하고 기호와 라벨은 같은 잉크색을 사용한다. ACT/타입은 제목 양옆, 위력은 본문 옆에 붙고 본문 실제 글자 높이에 따라 설명판을 압축한다. 키워드1개는 전폭으로 펼친다. 공통 공격 피해 경로는 개별 설명에서 제거하며 전투 판정은 바꾸지 않는다. `SkillInfoGlyph`/`SkillInfoAttachment`는 비차단 uGUI 메시이며 텍스트 라벨을 함께 사용한다. 선택/홀드 정보 표시는 상태를 변경하지 않는다. 기존 `CampaignSkillText`의 목적/위력 표시는 재사용하되 긴 효과 문단 대신 공용 카드가 실제 판정에 맞는 짧은 정보를 구성한다. 세부 의미와 배치는 `SkillInformationDesign.md`를 따른다.

로비의 네 탭은 현재 씬의 전체 페이지를 교체하며 별도 Scene 로드나 모달 창을 만들지 않는다. 고정 헤더와 `Lobby Page`는 형제이며 페이지는 헤더 100px을 제외한 전체 viewport를 채운다. 홈은 방과 전체 높이 준비 rail, 스테이지는 4×2 카드와 오른쪽 상세, 편성은 기존 조작을 유지한 확장 콘텐츠, 커리큘럼은 과정 트리와 오른쪽 상세다. `LobbyScreenTransition` 한 개가 신규 페이지를 0.22초 unscaled fade/32px 방향 이동으로 표시한다. 페이지 입력만 일시 차단하며 헤더는 계속 활성이다. 교체/숨김/Dispose 전에 `Finish()`로 위치·alpha·입력 상태를 복원한다. 과정 진행·편성 갱신은 이동 애니메이션을 재시작하지 않는다. 선택/스크롤은 기존 `ViewState`에 남기고 전환 객체는 진행 데이터나 전투 시계를 소유하지 않는다.

현재 비주얼(2026-09-27)은 `DuelVisualTheme`의19세기 검술 클럽 팔레트로 통일한다. 브리핑·로비·편성·커리큘럼·전투·결과 화면은 같은 목재/황동/종이/잉크 색을 공유한다. 코드 기반 `DuelPanelTrim`은 각 이미지에 하나만 붙는 비차단 uGUI mesh이며 `CanvasRenderer`를 명시적으로 요구한다. 메인 Sprite·색·치수·입력 핸들러를 바꾸지 않고 Image의 수명/마스킹을 따른다. 방/스킬15배지/기록·확정/숲 중경 PNG5개를 같은 Resource 경로·GUID로 편집했고 실제 문양 경계에 맞춰 아이콘의 rect만 바꿨다. 원경2층과전경·배경 튜닝·캐릭터·전투 Runtime은 그대로다. 최신 지침과 제작 기록은 `Art/VictorianDuelTheme.md`를 따른다. 아래 초기 중립 HUD/네온 역할색 설명보다 이 단락을 우선한다.

원본 충돌 파티클은 최대24개를 재사용한다. 수동 `Simulate` 뒤의 `IsAlive(true)`는 빈 시스템도 향후 방출 가능 상태로 남길 수 있으므로 풀 반환에 사용하지 않는다. 각 인스턴스의 하위 시스템 배열과 `max(startDelay+duration)`(원본0.55초)을 생성 때 캐시한다. 수동 전투 시계가 방출 종료 지점을 지나고 모든 하위 `particleCount`가0인 경우에만 멈추고 비활성화하여 반환한다. 원본은 비반복 시스템2개/단일 burst/하위 emitter 없음이며, 실제 시간 timeout이나 임의 파티클 자산 변경은 넣지 않는다. 따라서 슬로모션·히트 스톱 중에도 수명 시계를 유지하며 링을 조기 삭제하지 않는다.

- `Planning`: 적 큐가 공개된다. 기술 예약은 ACT 소비와 기술열 회전만 수행한다. 10초 또는 Space 입력으로 편성을 확정한다.
- `Resolving`: 확정한 큐를 변경할 수 없다. 같은 순번의 기술 한 쌍씩 준비한 뒤 실제 타격마다 해결한다. 짧은 쪽의 남은 슬롯은 빈 행동으로 처리한다.
- 전체 슬롯 완료: 턴을 올리고 저항력 회복, 잔여 ACT + 기본/기술 보상, 적 패턴 진행을 처리한 뒤 다시 편성한다.
- `Finished`: 현재 슬롯의 모든 타격·동작을 끝낸 뒤 승패를 확인한다. 이후 슬롯과 턴은 진행하지 않는다. 재대결은 기술열, 적 패턴, 상태와 타이머를 초기화한다.

`LegacyQueuedDuel`이 규칙과 상태를 소유하고 `DuelPrototypeController`는 입력·10초 제한·접근·재생·턴 마무리 흐름을 조정한다. 공통 `숨고르기`는 `TryQueueBreath()`로 준비 큐에 한 칸을 추가하며 ACT 없이 턴당3회만 허용한다. QWE 기술열을 회전하지 않는 Wait 행동이고 상대 공격은 무방비 체력 피해로 처리한다. HUD의 남은 횟수와 S/버튼 입력은 같은 세션 상태를 읽으며, 전투의 `Features`에 숨고르기가 없으면(서막, 수련 6 임무 전의 스테이지) 버튼을 숨기고 입력을 거부한다. 커리큘럼으로 얻는 방어 기술 호흡(ID17)과는 별개다. 상세는 `BreathingAction.md`를 따른다. 넘기기(Shift)는 `TryCycleLanes()`로 열린 모든 열의 맨 앞 기술을 쓰지 않고 뒤로 보낸다(ACT 대신 편성 시간 1초, 시계가 멈춰 있으면 무료, 횟수 제한 없음, 준비 단계만, `LaneCycle.md`). Windows에서는 `StickyKeysShortcutGuard`가 게임에 포커스가 있는 동안 Shift 다섯 번의 고정 키 단축키를 끈다. 슬롯 처리는 다음 API로 나눈다.

- `BeginNextSlot()`: 기술 한 쌍을 준비하고 랜덤 위력과 기존 버프를 한 번 계산한다. 기술 효과를 반영하되 피해는 아직 발생하지 않는다.
- `ResolveNextHit()`: 이번 타격의 체력·저항력 피해만 반영한다. 타격 수가 다른 경우 남은 쪽만 공격하며, 방어끼리는 피해 없는 동작을 처리한다. 피해 숫자와 밀림에는 실제 체력 차감량과 별도로 원본의 오버킬 전 피해·두 배 피해 전 밀림값을 제공한다.
- `CompleteCurrentSlot()`: 합산 슬롯 결과와 승패를 확정한다. 앞 타격에서 쓰러져도 그 슬롯의 남은 타격은 원본처럼 처리한다.
- `ResolveNextSlot()`: 테스트·비시각적 호출용으로 위 절차를 한 번에 실행한다.
- `TryStep(action, timingSuccessful, out success)`: 컨트롤러가 예고/첫 임팩트 직전 타이밍을 넘기고 Runtime은 해당 기술의 회피·압박 플래그를 적용하고 이번 턴 시도 수(성공 구간 조이기)와 연속 성공을 센다. 빗나간 시도가 있으면 다음 턴 자연 ACT 회복을 막는다. 상세는 `StepPrototype.md`를 따른다.

일반 전투의 표시 상태에는 `SkillWindup`을 추가했다. 슬롯/효과 초기화 뒤 기본0.24초 예고 동안 클립0 프레임을 유지하고 기존 `PlayingSlot` 시계를 시작한다. 성공 구간은 첫 타격 직전0.10초에서 시작해 이번 턴 시도마다 좁아진다(`LegacyStepTiming`, `StepPrototype.md` 판정 조이기). 키 입력을 프레임 진행·타격보다 먼저 판정하며, 같은 프레임에 누른 키는 그 프레임의 구간으로 함께 판정한다. 전투의 `Features`에 스텝이 하나도 없으면(서막, 수련 7 임무 전의 스테이지) 예고/스텝을 사용하지 않고, 닫힌 한쪽 스텝은 원을 그리지 않으며 시도로 세지 않는다(`CombatFeature`, `StoryUnlocks.md`).

`LegacyDuelArt`는 선별한 스프라이트·아이콘·폰트·효과음을 캐시한다. `LegacyArenaView`는 원본 12fps 클립의 스프라이트 곡선을 수동 샘플링하며, 전투 판정은 레거시 애니메이션 이벤트가 아닌 Runtime 타격 API를 통해 수행한다. 비레거시 클립의 SpriteRenderer 곡선 연결을 위해 각 캐릭터에 Controller 없는 Animator를 둔다. Animator의 자동 상태 전환 대신 전장의 수동 시계로만 재생한다. 원본 충돌 파티클은 필요한 렌더링 의존성만 복사하고 수동으로 시뮬레이션한다. 캐릭터의 기존 전투 스크립트와 Animator Controller·전체 씬을 가져오지는 않는다.

싸움은 안에서만 결투(서막)와 전투(그 밖의 임무·스테이지)로 나뉜다(`EncounterKind`, 2026-10-01). 규칙·수치·시간은 같고, 전투의 편성만 불릿타임이다. 컨트롤러가 편성 프레임마다 `SetPlanningState`로 요청하면 전장이 두 사람을 (가까이 붙어 있으면 먼저 물러서게 한 뒤) 천천히 다가가게 하고 대기 자세를 붙잡고 색을 가라앉힌다. 확정하면 바로 풀린다. 상세는 `DuelAndBattle.md`를 따른다.

화면의 기본 시계는 `Time.unscaledDeltaTime`이다. 컨트롤러가 Tab 검사 중 0.2배, 치명타 집중(저항 붕괴·마무리 일격) 중 0.15배를 적용해 편성 시간·접근·클립·타격·파티클·HUD 전환을 진행한다. 치명타 집중의 지속 시간은 원본처럼 실제 시간 0.75초이며, 카메라 보간도 실제 시간을 사용한다. 전역 `Time.timeScale`을 변경하지 않아 활성 씬의 다른 시스템에 속도 변경을 전달하지 않는다.

첫 접근은 최소 0.18초와 속도 32로, 이후 추격은 속도 54로 간격 4까지 진행한다. 공격 동작의 원본 길이는1/6초이며 원본 임팩트는1/12초다. `DuelPresentationSettings`의 재생 배율로 두 시간을 함께 나눠 타격 프레임을 유지한다. 스킬 시작에 배율/연타 간격을 함께 고정하며, 기본은 원본 속도1/연타 사이0.1초/다음 스킬 사이0.28초다. 연타 간격은 대기 동작으로 표현하고 마지막 큐 뒤에는 스킬 간격을 넣지 않는다. 타격 후 실제0.04초의 히트 스톱을 기본으로 적용하며 동시에 양쪽이 공격해도 중첩하지 않는다. 사거리 밖에서는 슬롯 시계를 임팩트에 고정하고 추격해 접촉한 뒤 처리한다. 긴 프레임이 공격 동작 끝까지 넘어가면 실제 임팩트 포즈로 되돌려 한 타만 처리한다. 피해·타격 횟수·ACT 규칙은 바꾸지 않는다.

설정은 `Resources/DuelPresentationSettings.asset`에서 읽으며, 상단 `Turn Limbo/연출 튜닝 열기`로 선택한다. 원경·중경·전경 크기와Y, 공격 재생 속도·연타/스킬 간격·히트 스톱·발광/Bloom을 Inspector에서 튜닝할 수 있다. 배경 변경은 다음Tick에 기존 타일의 크기/폭/배치를 바꾸며 풀을 재사용한다. 새 `DuelImpactGlow`는 단일 공유 메시/재질과 최대16개의 재사용 뷰로 HDR halo/core/star를 그린다. 기존 파티클은 보존하고 전장 런타임 Volume에만 Bloom을 추가한다. Bloom 입력은 최대4로 제한하여 레거시 HDR 불꽃이 과하게 번지지 않도록 한다. 새 빛·짧은 화면 섬광은 실제 시간으로 사라져 슬로모션에서도 오래 화면을 덮지 않는다. 씬·프로젝트의 공유 렌더링 설정은 수정하지 않는다. 사용법·기본값·저장은 `PresentationTuning.md`를 따른다.

밀림은 실제 체력+저항 피해를 우선으로 0.7~3 월드 단위로 계산하여 0.1초에 표현한다. 방어 중 밀림은 0.35배이며 피해 없는 완전 방어에도 0.18~0.35의 작은 접촉 밀림을 남긴다. 남은 밀림에 다음 타격을 더하되 미완료 밀림 합계는 6으로 제한한다. 밀린 대상은 원위치로 보간하지 않으며 공격자는 0.04초 후 추격한다. 양쪽 위치를 동시에 계산해 교차 없이 최소 교전 간격을 유지한다. 모든 슬롯이 끝나면 0.12초 대기와 0.18초 마무리만 거쳐 해당 위치에서 다음 편성 또는 결과 화면으로 전환한다. `EndTurn`/`ReturnComplete` 명칭은 기존 HUD 연결을 위해 남겼지만 위치 복귀 기능은 없다. `Reset`(재대결)만 처음 ±5 위치로 돌린다.

카메라는 편성/교전 모두 현재 두 캐릭터의 중심을, 치명타 집중은 실제 피격 대상을 추적한다. 과거 접근 중심을 다시 더하지 않는다. `ForestParallaxBackdrop`는 새 `Resources/ForestArena`의 안개·원경 나무·숲길·앞쪽 식물 Sprite를 캐시해 카메라 이동 대비 0.10/0.24/1.0/1.35의 속도로 수평 반복한다. 풀 타일은 카메라 폭·회전 범위에 맞춰 재배치하며 절대 인덱스의 교대 좌우 반전으로 연결 경계를 맞춘다. 생성·수명은 전장 루트가 소유한다. 최신 중경은 `ForestArena/forest-belt-mid`의 앞뒤 폭이 있는 벨트스크롤 숲길이다. 안개/원경 나무 기본 높이 각각14.4/중심Y=-0.5, 중경 높이12.6/Y=-1.2, 근경 높이12.6/Y=-0.75로 배경만 축소했다. 캐릭터 크기와 카메라 구도는 그대로이며, 발은 뒤쪽 지면 경계(y≈-0.82)보다 앞쪽에 선다. 중경은 자잘한 질감을 단순화한 픽셀 아트로 재편집했으며 경로·GUID·배치는 같다. 현재 새벽 숲의 다섯 리소스와 Aseprite 원본·검증 범위는 `Art/DawnForest.md`를 따른다. 원경 분리 프롬프트는 `Art/SplitFarForest.md`, 중경 편집 프롬프트는 `Art/PixelBeltForest.md`, 초기 벨트 구성은 `Art/BeltForest.md`, 첫 평면 숲 제작 기록은 `Art/ForestLayers.md`를 따른다. A/D 스텝은 컨트롤러가 첫 임팩트 직전 타이밍을, Runtime이 기술 단위 성공·피해·ACT 페널티를 소유한다. 위치 자체는 명중 판정이 아니며 성공한 첫 임팩트는 스텝으로 벌어진 거리 때문에 미루지 않고 후속 연타는 기존 재접촉을 기다린다. `DuelStepHud`는 비차단 예고 표시, `DuelStepAfterimages`는 기존 Sprite의 고정 풀이다. 아래 HUD의 `BeginReturn()`도 복귀 이동이 아니라 이 마무리 시작에 바를 닫는 역할이다.

HUD의 입력 패널과 타이머는 확정 시 숨긴다. `BeginCombat()`은 접근 완료 후 화면의 위·아래 바를 열고, `BeginReturn()`은 턴 마무리 시작에 바를 닫는다. 바 전환만 원본처럼 실제 시간 0.3초를 사용한다. 기록은 슬롯 완료 시 마지막 타격의 표시 피해를 두 열로 저장하며, 평소에는 숨겨지고 편성 중 기록 버튼 또는 L로 열 수 있다. 재대결은 기록과 이펙트도 초기화한다. 현재 HUD는 배경 독립적인 중립색 패널, 작은 카드·수평 상태바와 ACT 숫자/10칸 게이지를 사용한다. 최신 기술 아이콘은 `Resources/SkillRoles`의 역할별 색·모양을 갖춘 검 전용15개 atlas Sprite이며, 현재·다음·큐·로그·로비는 동일 `IconId` 매핑을 공유한다. 이전 `Resources/SkillButtons` 키캡형 PNG와 원본 퍼즐 PNG는 보존한다. 최신 제작 규칙은 `Art/SwordSkillIcons.md`, 이전 제작 기록은 `Art/RoleSkillIcons.md`·`Art/SkillButtonIcons.md`이다. 상세한 현재 스타일은 `CompactHudDesign.md`, 예전 원본 대응은 `LegacyHudParity.md`에 구분해 기록한다.

상태바와 큐는 ScreenSpaceOverlay Canvas에서 캐릭터별 고정 local 머리 위 기준점을 한 번 투영하여 함께 즉시 따라간다. 큐가 위, 상태바가 아래이며 두 요소를 하나의 경계로 clamp한다. 월드 Sprite bounds·추적 보간·카메라 확대값에 따른 크기 보정 없이 일정한 UI 크기를 유지한다. 큐의 각 행은 한 줄로 바깥쪽으로 늘어난다(줄바꿈 없음). 플레이어 큐는 1번 칸이 상태바 오른쪽 끝에 붙고 왼쪽으로, 적 큐는 1번 칸이 상태바 왼쪽 끝에 붙고 오른쪽으로 늘어나 두 1번 칸이 안쪽에서 마주 본다(2026-10-02, `CompactHudDesign.md`). 기존 Tab의 적 중심 카메라 확대·이동, 피해 숫자는 발생한 월드 타격 지점을 저장하고 매 Refresh 재투영하여 이동·확대하는 카메라에서도 화면의 이전 지점에 고정되지 않게 한다. 숫자의 크기·수명·피해 값은 유지한다. 기록·확정은 `Resources/HudActions`의 별도 책/체크 Sprite, 이름, 하단 `L`/`Space` 힌트로 표현한다. 기록은 편성 중 버튼 또는 `L`로 열고 닫으며 타이머를 멈추지 않는다. `Space`/Enter로 확정하며 준비의 A/D는 무효다. 일반 교전에서만 스텝을 허용한다. 연타 큐의 펄스는 정상 크기 1 아래로 축소하지 않고 실제 슬롯 소비만 카드를 제거한다.

스텝 예고는 `DuelStepHud.BindActor`로 플레이어의 고정 몸 기준점을 투영하는 흰 수축 원이다. A는 왼쪽, D는 오른쪽 반원이며 카메라 회전에도 키 좌우를 유지한다. `DuelStepRing`은 속이 빈 비차단 uGUI mesh로 얇은 선·다중 발광·현재 성공 구간 밴드(시도마다 좁아짐)·점선·눈금을 그린다. 원둘레 점을 캐시하며 프레임마다 새 객체·Sprite·재질을 만들지 않는다. 첫 타격 후 닫고 짧은 성공 섬광을 표시하며 슬롯 변경·숨김·Reset·Dispose에 잔여 표시를 정리한다. 컨트롤러는 Runtime의 새 성공 여부를 `LegacyArenaView.PerformStep`에 전달하고 기존 전투 시계 속도와 `StepPresentationSpeed` 중 작은 쪽을 사용한다. 이동·잔상·카메라 집중/배경 암전 수명은 실제 시간을 따르고 전역 Time.timeScale을 변경하지 않는다. 같은 스킬의 반복 성공 연출을 중첩하지 않으며 기존 치명타 카메라를 우선한다. 로비 복귀도 스텝 HUD/전장을 초기화한다. `ForestParallaxBackdrop`의 집중 tint는 원래 검사 tint와 곱해 매번 계산하므로 누적 암전이나 캐릭터 tint 변경이 없다. 피해·ACT·큐 판정은 변경하지 않는다.

기술 설명과 원본 실제 동작이 다른 효과는 임의로 개선하지 않고 실제 코드 동작을 따랐다. 기술 9의 버프는 현재 턴의 이후 슬롯에 적용하며, 기술 5는 방어 수치를 제거하지 않는다. 양쪽이 동시에 쓰러지면 원본처럼 플레이어 패배를 우선한다. 전체 49개 기술·체인·성장 시스템의 이식은 아직 아니다. 기술 강화는 잠시 뺐으며, 많이 사용할수록 강해지는 방식을 따로 설계할 예정이다.

Play의 첫 화면은 `TitleHud`(이어하기 / 새 게임, `SaveSystem.md`)다. 새 게임은 깨어남 오프닝 컷신(`Cutscene.md`) 뒤에 서막 1 임무의 `MissionBriefingHud`로 들어가고, 네 임무를 마치면 방 배경의 `CampaignLobbyHud`가 허브가 된다(`PrologueMissions.md`). 그 뒤의 수련 임무는 로비 홈의 임무 배너나 스테이지 탭의 임무 버튼으로 브리핑을 연다(`StoryUnlocks.md`). 홈/스테이지/편성/커리큘럼 탭은 요청만 전달하며 `CampaignRun`이 개방 단계, 첫 클리어와 재도전 보상(화면에 보이지 않는 재화), 커리큘럼 진행, 보유 기술, 고정 Q/W/E 기술열의 편성과 순서를 소유한다. 로비 → 선택한 개방 스테이지의 Battle → 결과 창 → 로비/재도전/개방된 다음 스테이지 순환이며 자동으로 다음 전투에 들어가지 않는다. Runtime의 Maintenance/Failed/Completed는 결과 확정 직후의 상태와 이전 API 호환용으로 남긴다. Controller가 마지막 타격과 마무리 완료 뒤 승패를 한 번 전달하고 불변 `BattleResult`를 `BattleResultHud`로 표시한다. 보상 지급과 커리큘럼 반영은 Runtime에서 한 번만 하며 창의 재표시는 상태를 변경하지 않는다. 로비와 결과 창에서는 전투 시계·전투 입력을 멈춘다. Escape는 일반 전투 포기/결과에서 로비/로비 홈, Enter는 결과에서 로비/스테이지 탭에서 출정한다.

서막(`Runtime/Prologue`)은 로비 전에 한 줄로 이어지는 네 임무다. `PrologueMissions`가 서막의, `LobbyMissions`가 로비 뒤 수련 임무(5~9)의 정적 데이터를 갖고, `StoryMissions`가 둘을 한 사슬로 잇는다. `PrologueRun`이 순서대로 이긴 임무의 진행을 소유하고, 이긴 임무가 연 기능(`UnlockedFeatures`)과 스테이지 제한(`StageLimit`)을 그 수에서 계산한다. `PrologueMission.CreateDuel`은 여정의 편성/진행과 분리한, 임무의 `Features`(서막 1 임무는 Q열 하나, 2~4 임무는 Q열과 넘기기)만 가진 `LegacyQueuedDuel`을 만든다. `MissionGuide`는 성공한 예약/적 확인/넘기기/확정/실제 다음 턴을 받아 코치 단계와 허용 입력을 결정하고 전투 판정은 바꾸지 않는다. Presentation의 `MissionBriefingHud`는 배경·제목·목표·적 실루엣과 시작 버튼 하나를, `MissionCoachHud`는 필요한 카드·확정·적 큐·ACT를 강조하는 비차단 안내를 보여 준다. 컨트롤러는 브리핑 → 시작 대사 → 전투 → (승리 시) 종료 대사 → 결과 순서로 잇는다. 대사의 다음 단계는 플레이어가 끝까지 넘기거나 건너뛸 때만 실행하고, 정리용 `CloseDialogue`는 실행하지 않는다. 임무 보상·일반 단계 해금은 없다. 씬·프리팹 수동 연결 없이 기존 부트스트랩에서 생성한다. 상세는 `PrologueMissions.md`를 따른다.

스토리 해금(2026-09-30)은 Runtime의 `CombatFeature` 플래그(Q/W/E열·숨고르기·회피·압박·넘기기) 하나로 표현한다. `LegacyQueuedDuel`은 생성할 때 받은 `Features`(기본 전부)로 닫힌 열의 기술을 빼고 닫힌 예약·스텝을 거부하며, `CampaignRun.SetProgression`이 스테이지 전투의 `Features`와 `StageLimit`을 정한다. Controller는 타이틀의 이어하기/새 게임 뒤에만(`StoryProgressionEnabled`) `PrologueRun`의 해금을 `CampaignRun`에 넘기고, 그 밖에는 `ClearProgression`으로 모두 연다. HUD는 세션의 `Features`를 읽어 닫힌 열 카드·숨고르기 버튼·넘기기 버튼·스텝 원을 숨기고, 편성·커리큘럼은 `CampaignRun.IsLaneOpen`으로 `임무로 열림`을 표시한다. 저장 형식은 바뀌지 않는다. 상세는 `StoryUnlocks.md`를 따른다.

1 임무의 적은 친구가 만든 연습장 허수아비다(`Art/TrainingDummy/README.md`). Runtime은 `MissionEnemy.Appearance`(`EnemyAppearance.Student`/`TrainingDummy`)만 정하고, Presentation의 `TrainingDummyAnimationSet`이 `Resources/TrainingDummy/Animations`의 대기 8장·피격 10장을 친구가 정한 시간대로 샘플링한다. `LegacyArenaView.SetEnemyAppearance`가 전투마다 외형을 고르며 그림이 없으면 학생으로 대신한다. 허수아비는 대기와 피격만 재생하고(피격은 전투 시계, 공격 재생 배속 미적용), 밀림·접근·추격 이동을 하지 않는다. `HasRequiredAssets`는 허수아비 18장도 요구한다. 전투 판정은 바꾸지 않는다. 재생 규칙은 `PrologueMissions.md`의 허수아비 항목을 따른다.

커리큘럼 과정을 완료해 얻은 기술은 보유 목록에만 추가한다. `CampaignRun`은 저장 편성과 별도의 고정3×3 임시 편집을 소유한다. 편집 중 빈칸은 허용하지만 저장은 각 열 정확히3개·소유권·중복·고정열 검사를 모두 통과해야 원자적으로 적용한다. 미저장 변경은 출정을 차단하고 되돌리기는 마지막 저장 편성을 복구한다. `CampaignLoadoutHud`는 같은 열의 슬롯 교환/보유 기술 교체를 드래그로만 요청하며, 카드 클릭은 설명만 선택하고 초안 슬롯을 변경하지 않는다. 카드에는 이름·ACT만 남기고 선택 기술의 상세만 보여준다. `CampaignCurriculumHud`는 과정 트리와 선택한 과정의 상세(얻는 기술의 공용 정보 카드·선행·택1·상태)를 표시한다. 카드 클릭은 살펴보기만 하며 `이 과정 진행`과 두 번 눌러야 실행되는 `커리큘럼 초기화`만 Controller/Runtime에 요청한다. `CampaignSkillText`의 실제 효과 문구는 편성 상세와 공용 정보 카드가 공유하고 전투 판정에는 관여하지 않는다. 로비가 두 뷰의 선택/스크롤 상태와 수명을 소유한다. 입장마다 편성 순서대로 복사한 기술과 적 수치로 별도 전투 스냅샷을 만들고 HUD·기록·위치·입자를 초기화한다. 커리큘럼의 선행·택1·진행 고정과 전투 중 선택·초기화·편성 차단(로비와 정비 단계에서만 허용)은 Runtime이 검사하고, 결과 창에서 막는 로비 전용 조건은 Controller(`IsInLobby`)가 검사한다. 첫 클리어는 다음 단계 개방/기본 보상을, 재도전은 절반 보상을 지급한다(재화는 화면에 표시하지 않는다). 기술 강화는 잠시 뺐으므로 편성은 정의 표의 불변 기술을 그대로 전투에 넘긴다. 기술 ID는 효과·소유권을, `IconId`는 그림 변형을 식별한다. `Resources/SkillRoles/skill-role-atlas`의 검 전용15개 스프라이트가 베기(주황 마름모), 관통(청록 육각형), 타격(금색 홈 사각형), 방어(녹색 방패)의 테두리와 문양을 제공하고 현재 전투 카드에는 기술 이름도 표시한다. 기존 키캡 자산은 보존한다. 추가6기술은 미이식 유틸 효과를 공유하지 않도록 IconId10~15로 분리한다. `LegacySkillRoles`는 실제 Skill.Id로 찾은 정의 표의 효과·태그와 종류/타격 횟수에서 도출하는 읽기 전용 표시 메타데이터이며 전투 판정에는 관여하지 않는다. 조건부 저항 피해와 표식 의미는 `SkillRoleLanguage.md`를 따른다.

기본 9개·커리큘럼으로 얻는 10개와 같은 적 리소스의 수치 변형 8단계 범위다. 3~8단계의 적은 단계마다 다른 리듬(턴별 대본 `EnemyScript`, `EnemyRhythms.md`)으로 움직인다. 전체 레거시 캠페인·대사·정식 세이브(지금은 임시 자동 저장 하나뿐)·기술열 사이 자유 이동·미이식 고유 효과·완전한 로그라이크 진행은 아직 없으며, 기존 프로젝트 전체 복구를 목표로 하지 않는다. 현재 로비·편성 규칙은 `LobbyAndLoadout.md`, 커리큘럼은 `Curriculum.md`, 삭제한 상점·정비의 이전 기록은 `CampaignLoop.md`, 전장/HUD 대응은 `LegacyPresentationAssets.md`·`CompactHudDesign.md`, 검증 수치와 실행 조건은 `Validation.md`를 따른다.
