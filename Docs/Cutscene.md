# 컷신

2026-10-01. 숲 전장 위에서 인물·카메라·화면을 움직여 장면을 보여 준다. 대사와 같은 텍스트 파일로 쓰고, 코드는 고치지 않는다. 새 그림 없이 지금 있는 숲 배경, 엘리제, 떠돌이 기사, 허수아비를 쓴다. 일러스트가 생기면 `@image`로 한 장씩 띄운다.

## 어디서 나오나

- **깨어남 오프닝**(`Assets/Game/Resources/Cutscene/opening.txt`, 로드 경로 `Cutscene/opening`). 타이틀의 **새 게임**을 고르면 새 저장을 쓴 직후에 재생하고, 끝나거나 건너뛰면 임무 1 브리핑이 열린다.
- 이어하기와 직접 부른 `StartNewGame`에서는 나오지 않는다. 테스트도 `NewGameFromTitle`이나 타이틀의 새 게임 버튼을 거치면 오프닝이 나오므로 `SkipCutscene()`으로 넘긴다. 오프닝 도중 게임을 끄고 이어하면 오프닝 없이 임무 1 브리핑부터 시작한다.
- 파일이 없거나 형식이 틀리면 경고를 남기고 컷신 없이 브리핑으로 간다.
- 지금 들어 있는 오프닝은 **연출 견본**이다. 글은 모두 `테스트`이고, 장면의 내용은 작가가 정한다(`Narrative.md`). 칼을 처음 쥐는 것은 임무 1(`처음 쥔 검`)이므로 견본에서는 베지 않는다. 쓰지 않은 명령은 파일 끝에 `#` 예로 남겨 두었다.

## 조작

- 대사가 떠 있을 때: 클릭(화면 어디든), Enter, Space로 다음 줄. 대사 상자의 **다음** 버튼도 같다.
- Escape나 대사 상자의 **건너뛰기** 버튼: 컷신 전체를 건너뛰고 다음 화면으로 간다.
- 시간이 흐르는 연출(페이드, 이동, 기다리기 등)은 넘길 수 없다. 기다리거나 건너뛴다.
- 컷신을 연 프레임의 키(예: 타이틀의 Enter)는 무시한다. 컷신을 끝낸 키도 다음 화면에 닿지 않는다.
- 오른쪽 위에 `Esc 건너뛰기`가 늘 보인다.

## 쓰는 법

한 파일이 한 장면이다. 위에서 아래로 순서대로 실행한다.

- **대사**는 `DialogueAuthoring.md`의 문법을 그대로 쓴다(`@narrator`, `@left 이름 | 역할`, `@right`, `@show`, `@hide`, `@move`, 일반 문장, `#` 메모, `\@`·`\#`). 한 줄이 플레이어가 한 번 넘기는 단위다.
- **연출 명령**은 아래 표다. 모두 소문자로 쓴다. 숫자는 `1.5`처럼 점을 쓴다.
- 시간이 걸리는 명령은 끝날 때까지 다음 줄로 가지 않는다. 줄 끝에 ` &`를 붙이면 기다리지 않고 바로 다음 명령을 시작한다. 여러 움직임을 동시에 하려면 앞의 것에 `&`를 붙인다.
- 대사 상자는 줄과 줄 사이에는 그대로 있다. 기다리는(`&` 없는) 시간 명령이 시작되면 사라졌다가 다음 대사에서 다시 뜬다. `&`를 붙인 명령은 대사 상자를 그대로 둔다(대사를 띄운 채 인물이 움직이게 할 때 쓴다).
- 컷신 중의 대사는 화면을 어둡게 덮지 않는다. 대화 상자만 아래에 뜬다. 화자 그림은 대사와 같은 화자 이미지 목록을 쓴다.
- 시작할 때 무대는 비어 있다. 인물은 없고, 화면은 밝고, 띠와 그림도 없으며, 카메라는 전투를 시작할 때의 위치다.

| 명령 | 뜻 |
|---|---|
| `@wait <초>` | 그만큼 기다린다. |
| `@fade out <초>` / `@fade in <초>` | 화면을 검게 덮는다 / 검은 화면에서 밝아진다. `0`초면 바로. 대사는 검은 화면 위에도 보인다. |
| `@bars on [초]` / `@bars off [초]` | 위아래 검은 띠(레터박스)를 넣는다 / 뺀다. 초를 안 쓰면 바로. |
| `@camera <x> <크기> [초]` | 카메라를 x로 옮기고 크기를 바꾼다. 크기는 2~6이다. 6이 전투 화면이고, 작을수록 가깝다. 가까이 갈수록 카메라가 조금 내려가서 발이 화면의 같은 높이에 남는다. |
| `@camera reset [초]` | 전투를 시작할 때의 카메라(x 0, 크기 6)로 돌아간다. |
| `@image <Resources 경로> [초]` | 그림 한 장을 화면 가득(비율 유지, 넘치는 쪽은 잘림) 서서히 띄운다. 다른 그림으로 바꾸면 새 그림이 0에서 다시 떠오른다. |
| `@image off [초]` | 그림을 서서히 거둔다. |
| `@actor <인물> at <x> [left\|right]` | 인물을 x에 세운다. 새로 세우면 대기 자세로 기본 방향(아래)을 본다. 이미 서 있으면 자세와 방향은 그대로 두고 그 자리로 옮긴다. 방향을 쓰면 그쪽을 본다. |
| `@actor <인물> hide` | 무대에서 내린다. |
| `@actor <인물> move <x> <초>` | 걸음 동작 없이 미끄러지듯 옮긴다. |
| `@actor <인물> face left\|right` | 돌아선다. |
| `@actor <인물> pose idle\|hurt\|block` | 자세. `idle`은 숨쉬는 대기 동작이 돈다. `hurt`(맞음)와 `block`(막음)은 다음 자세가 올 때까지 그대로 있다. |
| `@actor <인물> attack slash\|pierce\|blunt` | 베기·찌르기·내려치기를 한 번 하고 대기로 돌아간다. 1.26초 걸리고, 칼은 0.42초에 닿는다. |

- **인물**은 `elise`(엘리제), `knight`(떠돌이 기사, 지금은 상대 그림), `dummy`(허수아비)다. 기사와 허수아비는 같은 자리를 써서 한 번에 한 명만 무대에 선다. 바꾸려면 먼저 `hide`로 내린다.
- **허수아비**는 `idle`과 `hurt`만 있다. `hurt`는 한 번 흔들리고(0.7초) 대기로 돌아간다. 걷거나 공격하지 않는다.
- **위치 x**는 전장의 좌표다. 전투를 시작할 때 엘리제는 -5, 상대는 5에 선다. 크기 6의 화면은 대략 -10~10을 보여 준다. 쓸 수 있는 범위는 -30~30이다. 숲 배경은 어디서나 이어진다.
- **방향**: 엘리제는 처음에 오른쪽, 상대는 왼쪽을 본다.
- 시간은 0~30초다.

### 예

```text
@fade out 0
@actor elise at -4 right
@actor elise pose hurt
@fade in 3
@narrator
<나레이션>
@actor elise pose idle
@camera -1 5 3 &
@actor dummy at 3 left
@left 엘리제
<대사>
@actor elise move 1 1.2
@actor elise attack slash &
@wait 0.42
@actor dummy pose hurt
@wait 1
@fade out 1.5
```

### 오류

형식이 틀리면 `컷신 'Cutscene/opening', 12번째 줄: …`처럼 파일과 줄을 알려 주고, 그 컷신은 열지 않는다. 다음과 같은 경우 오류가 난다.

- 모르는 지시어를 쓰거나 대문자로 썼다.
- 숫자를 읽을 수 없거나 범위를 벗어났다.
- 무대에 없는 인물에게 명령했다.
- 기사와 허수아비를 함께 세웠다.
- 허수아비에게 막기·공격·이동을 시켰다.
- 바로 끝나는 명령(`at`, `hide`, `face`, `pose`)에 `&`를 붙였다. `@wait`에 `&`를 붙여도 오류다(기다리지 않으면 아무 일도 하지 않으므로).
- 보이는 그림이 없는데 `@image off`를 썼다.
- 대사 문법 오류(`DialogueAuthoring.md`)도 같은 형식으로 알린다.

`@image`의 그림이 Resources에 없으면 오류가 아니라 경고를 남기고 그림을 바꾸지 않는다. 적은 시간만큼은 그대로 기다린다(대사 상자도 그동안 사라진다). 오프닝의 그림이 모두 있는지는 PlayMode 테스트가 확인한다.

## 알려진 한계

- 걷는 동작이 없어서 `move`는 미끄러지듯 움직인다. 엎드리거나 앉는 자세도 없다.
- 떠돌이 기사는 아직 상대(신입생) 그림을 쓴다.
- 소리 명령은 없다.
- 다이얼로그 편집기(`DialogueEditor.md`)는 `Resources/Dialogue`의 파일만 연다. 컷신은 텍스트 편집기로 쓰고, 게임을 실행하거나 PlayMode 테스트로 확인한다.
- Unity를 실행할 수 없는 환경에서 만들었다. 실제 화면(띠 두께, 줌 정도, 이동 속도)과 PlayMode 실행은 확인하지 않았다.

## 코드

- Runtime `Cutscene/CutsceneStep`: 단계(`CutsceneStepKind`, 인물·자세·공격 열거형, `HoldSeconds`)와 `CutsceneScript`.
- Runtime `Cutscene/CutsceneScriptParser`
  - 연출 줄은 여기서 읽고, 대사 파서에는 메모(`#`)로 가려서 넘긴다. 대사 파서(`DialogueScriptParser`)는 고치지 않았다.
  - 대사 줄의 번호와 화자 상태는 대사 파서가 만든 것을 그대로 쓰고, 연출 단계와 줄 번호 순으로 합친다.
  - 대사가 없는 파일과 마지막 대사 뒤의 무대 명령도 검사한다.
  - 오류는 `CutsceneParseException`(`LineNumber`)으로 낸다.
- Runtime `Cutscene/CutscenePlayback`
  - 단계 순서를 맡는다. 즉시 명령은 한꺼번에 실행하고, 시간 명령은 그만큼 기다리며(`&`면 넘어감), 대사는 `Advance`까지 기다린다.
  - 대사 상자를 언제 감출지도 여기서 정한다.
  - 장면은 `ICutsceneStage`로 넘긴다.
  - EditMode에서 시험한다.
- Presentation `CutsceneDirector`(`ICutsceneStage`)
  - 전투가 멈춘 동안 전장의 두 인물과 카메라를 직접 움직이고, 숲 배경을 카메라에 맞춘다. 인물 그림은 `LegacyArenaView`의 애니메이션 세트(내부 접근자)에서 고른다.
  - 끝날 때 인물의 좌우 반전·표시를 되돌리고, 컨트롤러가 전장을 초기화한다.
- Presentation `CutsceneHud`: 그림, 레터박스, 검은 페이드, 건너뛰기 안내. 정렬 450으로, 결과 창(400) 위, 대사 상자(500) 아래다. 클릭을 받지 않는다.
- Presentation `DialogueHud.SetCinematic`: 컷신 중에는 배경을 어둡게 하지 않고, 닫기 버튼을 **건너뛰기**로 바꾼다.
- `DuelPrototypeController`
  - 추가한 API: `OpeningCutscene`, `IsPlayingCutscene`, `StartCutscene(script)`(로비에서만, 끝나면 로비로), `AdvanceCutscene`, `SkipCutscene`, `StopCutscene`(이어지는 흐름 없이 정리).
  - 컷신 중에는 `IsInTitle`·`IsInBriefing`·`IsInLobby`가 모두 거짓이다. `AdvancePresentation`이 맨 먼저 컷신을 처리한다.
  - 화면 정리(`ClearBattleScreens`, `ResetBattlePresentation`)는 컷신을 멈춘다.
- 테스트
  - EditMode: `CutsceneScriptParserTests`, `CutscenePlaybackTests`.
  - PlayMode: `CutscenePlayModeTests`(오프닝 파일 검사, 새 게임 흐름, 연출 반영, 입력 누수, 정리), 그리고 새 게임 흐름이 바뀐 `TitleSavePlayModeTests`, `MissionUnlockFlowPlayModeTests`.
