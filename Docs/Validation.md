# 첫 전투·연출·컴팩트 HUD 검증

최근 검증일: 2026-10-02. 환경: Windows / Unity 6000.5.9f1 / Universal 2D / Input System 1.20.0.

열려 있는 작업용 에디터와 구 프로젝트를 건드리지 않도록 Assets, Packages, ProjectSettings를 임시 복사한 프로젝트에서 검증했다. 레거시 프로젝트의 컴파일 복구나 패키지 수정은 하지 않았다.

## 최신 변경: 발동 배지 축소와 캐릭터 옆 이펙트 (2026-10-02)

화면 좌우 상단에 312×68 크기의 배지를 두어 캐릭터를 가리지 않게 하고, 실제 발동 순간에는 해당 캐릭터의 바깥쪽에 0.65초짜리 이펙트를 띄운다. 아군 막기는 방패 호, 상대 저항 감소는 검흔, 저항 회복은 빛 조각, 상대 발동은 붉은 경고 이펙트로 구분한다. 배지는 1.05초 뒤 사라지며 전투 수치와 음향은 그대로다.

- Unity 6000.5.9f1 격리 사본의 `SkillActivationCuePlayModeTests;SkillFeedbackIntegrationPlayModeTests` PlayMode **11/11 통과**, 실패·건너뜀 0이다. 배지 크기·위치와 캐릭터 비가림, 네 효과 양식, 실제 효과량, 소멸 시간, 재사용 및 입력 통과를 확인했다. 결과는 `Logs/SkillActivationLayoutPlayModeFinal2.xml`, 실행 로그는 `Logs/SkillActivationLayoutPlayModeFinal2.log`다.
- 격리 사본 전용 캡처 **1/1 통과**. `Logs/SkillActivationLayoutPreview-640x360.png`에서 양측 배지와 캐릭터 바깥쪽의 청록 방패 호·붉은 경고 호를 확인했다. 밝기와 두께를 조정한 최종 결과는 `Logs/SkillActivationLayoutCapture-Contrast.xml`이다. 배치 실행의 640×360 화면을 사용했으며, 실제 플레이 중의 체감은 별도로 확인할 필요가 있다.
- `TurnLimbo.Presentation.csproj`와 `TurnLimbo.Presentation.Tests.csproj`의 C# 빌드는 모두 오류 0개였다. 경고는 기존 필드 및 Unity API 관련 항목이다.

## 이전 변경: 기술 조건 성립 순간의 발동 표시 (2026-10-02)

검증 상태: **Ready with limitations**. 실제 적용된 조건 효과를 아군·상대 패널에 짧게 표시한다. 아군 막기의 다음 턴 ACT 회복 +2, 적 막기의 조건 대응 경고(적 ACT 보상 없음), 적 발검의 실제 저항 감소량을 구분한다. 새 패널은 다음 슬롯·턴 전환·재시작에 정리하며 입력을 가로채지 않는다. 전투 규칙과 음향은 바꾸지 않았다. 세부 계약은 `SkillConditionFeedback.md`를 따른다.

- Unity 6000.5.9f1 격리 사본의 `LegacySkillFeedbackTests` EditMode **52/52 통과**, 실패·건너뜀 0이다. 결과는 `Logs/SkillActivationEditMode.xml`, 실행 로그는 `Logs/SkillActivationEditModeFinal.log`다.
- `SkillActivationCuePlayModeTests;SkillFeedbackIntegrationPlayModeTests` PlayMode **9/9 통과**, 실패·건너뜀 0이다. 조건 일치·불일치, 적 막기 경고, 적 발검 저항 상한, 저항 0 효과 억제, 실제 Controller 연결, 다음 슬롯·턴·재시작 정리, 패널 재사용·입력 비차단을 확인했다. 결과는 `Logs/SkillActivationPlayMode.xml`, 실행 로그는 `Logs/SkillActivationPlayMode.log`다.
- 격리 사본 전용 오프스크린 시각 확인 **1/1 통과**. `Logs/SkillActivationPreview.png`를 1600×900으로 렌더링해 양측 패널의 색상·간격·문구 잘림을 육안 확인했다. 실제 전투 플레이 중의 주관적 타격감과 모든 화면 비율은 아직 평가하지 않았다.
- `TurnLimbo.Presentation.csproj`와 신규 테스트 파일을 포함한 임시 PlayMode 프로젝트의 C# 빌드에서 오류 0개였다. Unity 검증 전 초기 사본 가져오기에서는 테스트가 시작되지 않아 재실행했고, 최종 XML의 통과 수치만 위에 기록했다.

## 이전 변경: 타격 동작의 머리 방향 교정 (2026-09-27)

검증 상태: **Ready with limitations**. 검날이 뒤쪽으로 향하는 타격 3종의 머리 방향을 교정한 `head-turn-v2` 그림을 적용했다. 준비·회수인 F1/F4는 오른쪽, 중간 동작·접촉인 F2/F3는 왼쪽을 보는 그림이다. 이번 변경은 타격 상반신 PNG 12개 교체이며 런타임 C#, 스킬 판정·명중 대상·이동·상하반신 재생 시계는 변경하지 않았다. 그림 제작 기록은 `Art/Animations/MobStudentAttacks/head-turn-v2/creation-notes.md`를 따른다.

- Unity 6000.5.9f1 격리 사본의 graphics PlayMode 대상 **6/6 통과**, 실패·건너뜀 0, 1.0160196초, 종료 코드 0이다. 필터는 `MobStudentAnimationPlayModeTests;MobStudentHeadTurnCapturePlayModeTests`이며 기존 모브 검사 5개와 사본 전용 카메라 촬영 1개를 실행했다. 결과는 `Logs/MobStudentHeadTurnPlayModeResults.xml`, 실행 로그는 `Logs/MobStudentHeadTurnPlayMode.log`다. 아래의 이전 44/44 기록은 최초 주인공 통합 검증이며, 이번 그림 교체에서 동일 44개 전체를 재실행한 것은 아니다.
- 실제 전장 카메라의 1600×900 타격 접촉 사진 `Logs/MobStudent-HeadTurn-blunt-1.png`, `MobStudent-HeadTurn-blunt-2.png`, `MobStudent-HeadTurn-blunt-3.png`에서 왼쪽으로 돌린 머리가 반영된 것을 육안 확인했다. 상하반신 연결·발의 그림자 접지·검끝도 확인했다. 같은 촬영 검사에서 대기·참격·관통을 포함한 총 10장을 `MobStudent-HeadTurn-*` 이름으로 저장하고 이전 `MobStudent-Applied-*` 사진을 보존했다. 이 사진은 HUD를 포함하지 않으며 전투 명중 방향이나 전체 이동 시스템을 재설계·검증한 근거가 아니다.
- 작업본과 검사 사본의 C#/metadata 및 모브 PNG/metadata **324파일 SHA256 불일치 0**이다. 이전 통합 스냅샷 대비 변경된 파일은 타격 PNG 12개뿐이며 C#, 나머지 PNG 37개와 모든 Sprite metadata 49개는 동일하다. `Logs/MobStudentHeadTurnSnapshot.json`에 기존 `MobStudentIntegrationSnapshot.json`과의 대조를 저장했다. 이미지 규격 1024×768, PPU 150, 발 피벗 (448,64), GUID는 보존한다. 공통 검날 300px·클리핑 없음·원본 v1 보존은 `Art/Animations/MobStudentAttacks/head-turn-v2/package-validation.json`을 함께 따른다.
- 사용자 Editor·씬·프리팹·설정은 조작하지 않았다. 촬영 코드는 검증 사본에만 추가했다. 이번 실행에서 게임 컴파일 오류·게임 예외는 관찰하지 않았으며, 전체 테스트·출시 빌드·자연 전투 중 시선 전환의 최종 평가는 이번 범위에서 수행하지 않았다.

## 직전 변경: 모브 주인공과 9개 상반신 공격 (2026-09-27)

검증 상태: **Ready with limitations**. 플레이어를 새 모브 캐릭터로 교체하고 참격·관통·타격 각각 3타, 총 9개의 4프레임 공격을 연결했다. 상반신 공격과 하반신 이동은 독립적으로 표시하며 기존 전투 판정·피해·ACT·이동 거리·속도·적과 HUD는 유지한다. 구현 규약은 `MobStudentAnimationIntegration.md`, 그림과 생성 기록은 `Art/Animations/MobStudentAttacks/v1/creation-notes.md`를 따른다.

- Unity 6000.5.9f1 격리 사본에서 최종 graphics PlayMode **44/44 통과**, 실패·건너뜀 0, 42.5500251초, 종료 코드 0이다. 단일 최종 결과는 `Logs/MobStudentFinalPlayModeResults.xml`, 실행 로그는 `Logs/MobStudentFinalPlayMode.log`이다. 필터는 `MobStudentAnimationPlayModeTests;LegacyAnimationPlayModeTests;CombatTempoPlayModeTests;PressureAttackTrailPlayModeTests;StepMotionPlayModeTests;DuelPrototypePlayModeTests;ReadableCameraPlayModeTests;MobStudentCapturePlayModeTests`이다. 프로젝트의 모든 PlayMode 또는 EditMode 테스트를 실행했다는 뜻은 아니다.
- 새 검사 5개는 49개 실제 Sprite의 크기·공통 피벗·PPU·Point 필터, 9개 공격의 실제 36프레임과 세 번째 프레임 접촉, 4타 이상의 변형 반복, 공격 접촉에서 상반신을 유지하면서 하반신 8프레임 독립 재생, 짧은 접근·스텝의 실제 하반신 프레임 변화, 히트 스톱·정지·재시작 및 상·하반신 잔상을 확인한다. 기존 검사는 원본 적의 8프레임 대기·공격, 단타/연타 시계·접촉·회수·간격·느린 재생·긴 프레임, 실제 Controller 판정, 스텝 거리·밀림·카메라와 유한한 잔상 풀을 계속 검사한다.
- 최초 실행은 **42/44 통과**였다. 실패 2개는 기존 잔상 검사에서 모든 자식 SpriteRenderer를 한 캐릭터로 세고, 하반신 Sprite도 상반신 공격 Sprite와 같아야 한다고 기대한 전제였다. 잔상 풀은 12개 캐릭터 쌍이며 상·하반신 24개 렌더러를 재사용한다. 검사 fixture가 각 쌍의 루트 상반신 12개를 선택하도록 갱신했고 새 검사에서 하반신 복제도 확인했다. 변경 후 잔상 대상 **7/7 통과**(`Logs/MobStudentTrailFinalResults.xml`, `MobStudentTrailFinal.log`)와 최종 동일 44개 전체 통과를 확인했다. 최초 결과와 로그는 `Logs/MobStudentPlayModeResults.xml`, `MobStudentPlayMode.log`에 보존한다.
- 실제 전장 카메라로 1600×900의 대기 1장과 9개 공격 접촉 9장을 저장했다. `Logs/MobStudent-Applied-Idle.png`, `MobStudent-Applied-slash-1..3.png`, `MobStudent-Applied-pierce-1..3.png`, `MobStudent-Applied-blunt-1..3.png`이다. 새 캐릭터 크기·상하반신 연결·발의 그림자 접지·검끝을 육안 확인했다. 카메라 캡처는 HUD를 포함하지 않으며 전체 자연 플레이 평가도 아니다. 첫 대기 캡처는 URP에 재활성화된 Sprite 배치가 등록되기 전이라 잘못 나왔고, 검증 전용 촬영 코드의 첫 렌더 요청 후 한 프레임을 준비하여 재촬영했다. 제품 렌더링 코드는 이 촬영 문제 때문에 변경하지 않았다.
- 49개 PNG는 최종 1024×768, PPU 150, 아래쪽 기준 피벗 (448,64)로 공통 정렬한다. 검끝을 위한 투명 여백만 추가하며 그림 크기를 프레임마다 변경하지 않는다. 플레이어의 기존 중심 transform/HUD 원점을 보존하고 새 발 피벗을 기존 지면에 맞추는 Sprite 래퍼를 한 번 생성한다. 모든 래퍼는 원본 텍스처를 공유하며 전장 해제 시 정리한다. 씬·프리팹 수동 연결 없이 Play 재시작으로 적용된다. 원래 플레이어 이미지·클립은 보존한다.
- 작업본과 검증 사본의 `Assets/Game` C#/metadata 및 모브 49 PNG/metadata **324파일 SHA256 불일치 0**이다. `Logs/MobStudentIntegrationSnapshot.json`에 파일별 근거를 저장했다. 새 스크립트와 검사 metadata는 Unity가 생성한 GUID를 복사하고 기존 metadata는 보존했다. 검증용 촬영 코드는 `.validation/mob-combat-unity` 사본에만 있으며 게임 Assets에 넣지 않았다.
- 검증 사본 컴파일은 `Logs/MobStudentCompileRetry.log`에서 종료 코드 0이다. 이번 실행에서 게임 C# 컴파일 오류와 게임 예외는 관찰하지 않았다. 처음의 검증 프로세스는 라이선스 IPC 초기화 대기 때문에 해당 프로세스만 종료하고 권한을 허용해 재시작했다. 사용자 Editor와 기존 LicenseClient는 종료하지 않았다. 최종 로그에는 Editor의 라이선스 토큰 갱신/D3D 진단 메시지가 있으므로 Console 전체가 깨끗하다고 주장하지 않는다.
- 전체 EditMode, 전체 PlayMode, 출시 빌드, 다른 화면 비율·장치와 성능 프로파일은 이번 범위에서 실행하지 않았다. 하반신 표시를 기존 이동 진행률에 맞추지만 발 접지와 월드 이동을 맞추는 루트 모션은 아직 도입하지 않았다. 0.08초 스텝에서는 실제 렌더 프레임 수 때문에 원본 8장 전부가 화면에 나타나지 않을 수 있다. 공격·이동의 손맛과 자연 전투 중 연속 자세의 최종 평가는 남아 있다.

## 이전 변경: 회피·압박 전용 사운드와 원 광채 (2026-09-27)

검증 상태: **Ready with limitations**. 스텝 성공/실패 판정 뒤 전용 소리를 연결하고, 성공한 쪽의 얇은 반원에 넓은 흰 halo·짧은 바깥 확산 pulse를 더했다. 이동·카메라·잔상·성공 구간·피해·ACT 규칙은 유지한다. 기능 계약과 음량/밝기 조절은 `StepPrototype.md`와 `PresentationTuning.md`를 따른다.

- Unity6000.5.9f1 격리 사본 graphics PlayMode **26/26 통과**, 실패/건너뜀0,0.9685391초. 필터 `StepAudioPlayModeTests;StepRingHudPlayModeTests;StepInputPlayModeTests`: 소리4/원11/실제 Controller 입력11개. 클립 재사용·유한한 파형/피크/끝 감쇠·게임 난수 불변·2개 고정 음원·실패32% 음량·Mute/Stop/Dispose, 얇은 속빈 원·좌우·성공 구간·밝기0/최대/비정상 값·실제 시간 확산/만료, 성공/실패 음원 연결과 로비 정리를 확인했다. 기존 입력 규칙 검사도 포함했다. 근거는 `Logs/StepFeedback-PlayModeFinalResults.xml`/`StepFeedback-PlayModeFinal.log`다. 전체 PlayMode나 출시 빌드를 실행했다는 뜻은 아니다.
- 최초25/26 통과의 실패1개는 신규 사운드 연결 검사에서 너무 이른 회피로 거리를 벌린 뒤 같은 슬롯의 성공 구간을 기다리던 상황이다. 기존 이동/판정은 수정하지 않고 조기 압박의 실패 소리를 확인한 뒤 창 안에서 회피·압박을 각각 성공시키도록 검사 상황을 보완했다. 두 성공음·ACT/타격 미해결·로비 정리 단언은 유지했다. 최초 `Logs/StepFeedback-PlayModeResults.xml`/`StepFeedback-PlayMode.log`를 보존했다.
- 실제1600×900 Game View의 발동 예고·압박 성공·회피 성공3장과1280×720 회피 성공1장을 육안 확인했다. 흰 광채는 원주에 집중하고 몸통은 채우지 않으며 기존 배경 암전·이동 잔상·A왼/D오른·얇은 중심선을 유지한다. `Logs/StepFeedback-Cue-1600.png`, `StepFeedback-Pressure-1600.png`, `StepFeedback-Dodge-1600.png`, `StepFeedback-Dodge-720.png`가 근거다. 촬영은 비활성 Controller·수동 시계·고정 약한 슬롯의 표시 fixture이며 전체 자연 플레이/반응 타이밍 평가가 아니다. 최초720 촬영은 크기 변경 전에 투영된 좌표가 남아 원/글자가 어긋났다. 화면 크기가 정착한 뒤 사본 도구의 수동 갱신을 한 번 더 적용해 재촬영했다. 생산 UI는 이 촬영 수정으로 바꾸지 않았다. 최초 `StepFeedback-First-Dodge-720.png`/`StepFeedback-Visual.log`와 최종 `StepFeedback-VisualFinal.log`/`StepFeedback-CaptureTool.txt`를 보존하며 최종 로그의 `STEP FEEDBACK VISUAL COMPLETE`가 완료 근거다.
- 실제 성공 처리 뒤 캐시 클립을 `Logs/StepFeedback-Dodge.wav`/`StepFeedback-Pressure.wav`로 추출했다. 파형과 재생 경로는 검사했지만 실기기에서 귀로 들은 음색·믹스의 최종 품질은 검증하지 않았다. 외부 음원이나 기존 타격 클립을 재사용하지 않고 생성 시4클립만 합성한다. 고정2음원을 사용하고 반복 실패가 같은 액션의 재생 중 성공음을 끊지 않는다. 스텝 소리/원 밝기는 전용 설정으로 조절하며 기존 타격음 피치를 바꾸지 않는다.
- 생산/검사/metadata10파일과 전체 `Assets/Game` C#/metadata368파일은 검사 사본과 SHA256 일치(불일치0). 시작 Runtime C#17개 해시도 동일하다. `Logs/StepFeedback-Snapshot.json`에 검사 범위·해시·전체 비교를 기록했다. Assets metadata269개의 잘못된GUID/중복0. 그림·씬·프리팹·기존 클립·파티클·재질·렌더링·Resources 설정 자산·패키지·ProjectSettings·레거시 프로젝트는 편집하지 않았다. 열린 작업용 Editor를 조작하지 않고 사본에서만 실행했다.
- 관련 실행에서 게임 C# 컴파일 오류·게임 예외·새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 SearchDatabase 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 남았으며 Console 전체가 깨끗하다는 주장은 아니다. 최초 시작의 라이선스 채널 재연결 뒤 검사와 촬영은 완료됐다. 전체 테스트·출시 빌드·다른 비율/장치·성능 프로파일·자연 전투 손맛/밸런스·실기기 청감은 생략했다. XML/log/PNG/WAV/도구·해시는 보존하고 이번 격리 사본만 정리한다. Play를 다시 시작하면 별도 씬 연결 없이 적용된다.

## 이전 변경: 강조 가시성·양측 버프 수치 (2026-09-27)

검증 상태: **Ready with limitations**. 조건 후보를 기존 금색 장식과 구분되는 밝은 청록색2px 테두리/코너로 바꾸고, 성립·실제 발동에3.5px 이중 테두리/반경9px 외곽 표식/6% 내부색을 추가했다. 실제 효과는 금색으로 구분하고 은은한 버프 입자6개를 유지한다. 양쪽 상태창에 현재 적용 위력/피해 감소와 새로 받은 다음 칸 버프의 수치/지속을 표시한다. 판정·버프 계산/수명·난수·상점 강화·체인 범위는 바꾸지 않는다. 계약은 `SkillConditionFeedback.md`를 따른다.

- 새 격리 사본의 기준 전체 EditMode **314/314 통과**, 실패/건너뜀0,0.6483215초. 최종 전체 EditMode **329/329 통과**, 실패/건너뜀0,0.5598546초. 추가15개 규칙 사례는 적/아군 찌르기·흘리기·쳐내기·준비의 실제 획득 수치/초기 사용 칸, 다음 칸 적용과 부여 스냅샷 불변, 보호 재부여/현재 합계 분리, 취약·ACT/회복·Wait/빈 슬롯 제외를 검사한다. `Logs/VisibleSkillBuffs-2d1fdc63-BaselineEditModeResults.xml`과 `FinalEditModeResults.xml`/`FinalEditMode.log`가 근거다(모두 같은 작업 접두사).
- 최종 graphics PlayMode **93/93 통과**, 실패/건너뜀0,44.8640817초. 기존16개 fixture를 유지하고 새 HUD 검사4개를 추가했다. 적 버프 획득→다음 기술 적용→준비 만료, 양측 독립 표시, 취약/숨고르기 제외, 복합3행의164px 높이·게이지 하단 고정·문구 폭, 비차단 배경/글자, 반복40프레임 객체 재사용·초기화와 기존 키 해제·스텝/연타 동작을 검사한다. 메시 움직임/재사용 단언을 유지하고 새 장식에 맞게 상한100 vertices를 적용했다. `Logs/VisibleSkillBuffs-2d1fdc63-FinalPlayModeResults.xml`과 `FinalPlayMode.log`가 근거다. 전체 PlayMode·성능 프로파일 검증은 아니다.
- 최종1600×900 10장과1280×720 2장을 촬영하고12장 모두 육안 확인했다. 발검 후보0/2·다음 칸0, 예약 후 막기 후보/다음 칸1, 실제 발검 강조, 적 찌르기/흘리기/준비의 새 버프 수치/지속과 다음 슬롯 현재 수치/입자를 확인한다. 획득 슬롯은 현재 버프0/입자0, 적용 슬롯은 위력+10/+30 또는 피해 감소30/입자6개다. 양측 실제3→8→8의 현재 위력10/피해 감소30/다음10칸 피해 감소30의3행,164px 높이·상태창/라벨/현재 큐의 화면 경계를 두 해상도에서 검사했다. 청록 후보/큰 표식과 금색 실제 효과가 구분되고 새 문구의 잘림/새 정보 겹침을 발견하지 못했다. 최종 PNG는 `Logs/VisibleSkillBuffs-Hold-Draw-Candidates-1600.png`, `Hold-Block-AfterReservation-1600.png`, `Actual-Draw-Triggered-1600.png`, `Enemy-Pierce/Flow/Ready-Granted/Applied-1600.png` 각6장, `Both-Combined-Buffs-1600.png`, `Hold-Draw-Candidates-720.png`, `Both-Combined-Buffs-720.png`이며 모두 `VisibleSkillBuffs-` 접두사다. `Logs/VisibleSkillBuffs-2d1fdc63-VisualRetry.log`의 `VISIBLE SKILL BUFFS VISUAL COMPLETE`와 `CaptureTool.txt`가 근거다.
- 촬영은 원본 리소스·비활성 Controller·수동 시계/홀드 시간 설정·실제 가상 Q 입력과 실제 슬롯 스냅샷을 사용한다. 실행 중 적 설명은 HUD API로 표시한다. 자연 전투 손맛/난이도 검증은 아니다. 첫 촬영은 이미 완료해 비활성화한 큐0/1의 정상 null anchor를 화면 경계 검사에 전달해 실패했다. 촬영 도구만 활성 anchor를 검사하도록 바꾸고 현재2번 anchor는 반드시 존재하도록 유지해 전체12장을 재촬영했다. 최초 `Visual.log`와9장/도구 `FirstVisual/`를 같은 작업 접두사로 보존했다. 생산 게임 코드와329/93 검사 결과는 이 수정으로 바꾸지 않았다.
- 생산 변경은 Runtime의 `LegacySkillFeedback/LegacyQueuedDuel`, Presentation의 `LegacyCombatHud/SkillCardFeedbackGraphic`4개와 기존 규칙/화면 검사2개, 총6파일이다. 새 버프 값은 실제 생성한 `SkillBuff`에서 복사하고 HUD는 전투 상태를 소비하지 않는다. 상태창마다4개 줄을 만들고 슬롯별 값 변경에서만 문구/높이를 갱신한다. C#/metadata364개와 생산 metadata267개를 유지했다. GUID 형식 오류/중복0개, 원본/검증 사본 전체 해시 차이0개, 변경6파일과 보존 원문 해시 차이0개를 확인했다. 최종 범위·전체 해시·보존 원문은 `Logs/VisibleSkillBuffs-2d1fdc63-FinalSnapshot.json`, `FinalHashEvidence.json`, `FinalTestedSnapshot/`을 따른다(같은 작업 접두사).
- 최종 관련 실행에서 게임 컴파일 오류·게임 예외는 관찰하지 않았다. 촬영 Editor의 기존 SearchDatabase 시작 인덱싱 예외/종료 JobTempAlloc 경고는 별도로 남으며 Console 전체가 깨끗하다는 주장은 아니다. 원본 이미지/파티클·씬/프리팹·패키지/ProjectSettings·레거시 프로젝트·열린 작업용 Editor는 이번 작업에서 편집/조작하지 않았다. 출시 빌드·전체 PlayMode·다른 화면 비율/장치·성능·자연 전투 밸런스는 검증하지 않았다. 로그/XML/PNG/촬영 도구/해시 근거를 보존하고 정확한 이번 격리 사본만 정리했다. 별도 Inspector 연결 없이 Play 재시작으로 적용한다.

## 이전 변경: 기술 조건 강조·전투 버프 입자 (2026-09-27)

검증 상태: **Ready with limitations**. 기술 확인 중 상대 조건 후보와 다음 예약 칸을 구분해 표시하며, 실행 중 실제 조건 성립·효과 발동·위력/보호 버프를 강조한다. 파티클은 전투 중 적용된 버프에만 표시하고 상점 +1~+3 강화만으로 켜지 않는다. 구현·조건·수명 계약은 `SkillConditionFeedback.md`를 따른다. 기존 판정·수치·난수·체인 기술 범위는 유지한다.

- 변경 전 전체 EditMode **277/277 통과**, 실패/건너뜀0,0.5439377초. 최종 전체 EditMode **314/314 통과**, 실패/건너뜀0,0.5703318초. 신규 규칙 검사37개는 막기/발검의 실제 상대 조건·플레이어 전용 ACT, 투지의 실제 회복·상한·반올림, 위력 가산/보호 순효과·사용 칸, 숨고르기/빈 슬롯·상점 강화 제외, 불변 슬롯 스냅샷·재시작을 검사한다. 근거는 `Logs/SkillFeedback-315beb46-BaselineEditModeResults.xml`과 `CompleteEditModeResults.xml`/`CompleteEditMode.log`(모두 같은 접두사)다.
- 최종 graphics PlayMode **89/89 통과**, 실패/건너뜀0,44.8035514초. 기존 상점·편성·설명·카드·연타·저항 표시·Controller·스텝/숨고르기 입력과 신규 표시/통합 검사8개를 포함한16개 fixture를 실행했다. 후보와 다음 칸·자기 회복 조건, 실제 조건과 효과 분리, 키 해제/다른 홀드 키 유지, 반복 메시 재사용·입력 비차단, 실제 시간 입자 이동·연타 유지·슬롯 종료/재시작 정리를 확인한다. 실제 Q/W 상태를 사용하되 홀드 경과는 수동으로 설정한 Controller 검사다. 근거는 `Logs/SkillFeedback-315beb46-CompletePlayModeResults.xml`과 `CompletePlayMode.log`다. 전체 PlayMode를 실행했다는 뜻은 아니다.
- 실제1600×900 Game View7장과1280×720 1장을 최종 재촬영하고 모두 육안 확인했다. 발검의 방어 후보0/2와 다음 칸0, 발검을 하나 예약한 뒤 막기의 타격 후보/다음 칸1, 발검의 일치/불일치, 준비 뒤 위력+30/입자6개, 흘리기 뒤 보호+30/입자6개, 전진 뒤 취약/입자0개를 검사했다. 최종 PNG는 `Logs/SkillFeedback-Hold-Draw-Candidates-1600.png`, `Hold-Block-AfterReservation-1600.png`, `Hold-Draw-Candidates-720.png`, `Actual-Draw-Triggered-1600.png`, `Actual-Draw-Unmatched-1600.png`, `Actual-PowerBuff-1600.png`, `Actual-ProtectionBuff-1600.png`, `Actual-Vulnerability-NoParticles-1600.png`이며 모두 `SkillFeedback-` 접두사다. 새 표시의 잘림·정보 겹침을 발견하지 못했다. `Logs/SkillFeedback-315beb46-VisualRetry.log`의 `SKILL FEEDBACK VISUAL COMPLETE`와 `CaptureTool.txt`가 근거다.
- 화면 촬영은 원본 리소스·비활성 Controller·수동 시계를 사용한 결정적 fixture다. Q 홀드는 경과1.1초를 설정한 가상 키 입력이며, 실행 중 설명은 HUD API로 표시했다. 실제 슬롯 조건/버프·예약 개수·첫 타격 전 무넉백/무히트 스톱을 단언했지만 자연 전투 손맛/난이도 근거는 아니다. 최초 촬영은 이전 Q 해제를 새 세션의 짧은 입력으로 읽어 발검 예약이 하나 더 생겼다. Setup 갱신에 입력을 전달하지 않고 실제 예약1개/버프 fixture2개를 검사한 뒤 전부 재촬영했다. 첫 로그 `Visual.log`와 첫 PNG/도구 `FirstVisual/`는 같은 작업 접두사로 보존했다. 게임 입력이나 판정은 이 촬영 수정으로 바꾸지 않았다.
- 최초 최종 EditMode 실행은 신규 화면 검사에서 Unity6000.5의 `CanvasRenderer.GetMesh()`를 잘못 호출해 컴파일에 실패했고 검사는 실행되지 않았다. 반환 Mesh API로 검사만 수정해 실제 정점 수/입자 움직임 단언을 유지하고 renderer 소유 Mesh는 파괴하지 않는다. 실패 `Logs/SkillFeedback-315beb46-FinalEditMode.log`와 중간314/314 통과 `FinalEditRetryResults.xml`/`FinalEditRetry.log`는 보존한다. 이후 실제 `ConditionMet` 강조 보완까지 포함해 위 최종314/89를 재실행했다.
- 최종 사본은 이번 생산/검사/metadata14파일과 병행 변경된 `SkillLaneStyle` 표시 이름 및 검사2파일을 포함한다. 병행 명칭 변경은 이번 기능의 성과로 간주하지 않는다. `Assets/Game` C#/metadata364개(C#109/meta255)의 원본/실행 사본 비교에서 불일치0이다. `Logs/SkillFeedback-315beb46-FinalSnapshot.json`과 `FinalHashEvidence.json`에 범위·전체 비교·해시를 기록하고, 변경16파일 원문은 `FinalTestedSnapshot/`에 보존했다(모두 같은 작업 접두사). 보존본 비교도 불일치0이다. 생산 Assets metadata267개는 형식 오류/중복GUID0이다. 촬영 사본에서만 생성한 Editor 도구와 그 metadata는 생산 수치에서 제외한다. Runtime/Core C#23개에 UnityEngine/UnityEditor 참조0이며 두 asmdef의 `noEngineReferences=true`를 유지한다.
- 최종 관련 실행에서 게임 컴파일 오류·게임 예외는 관찰하지 않았다. 그래픽 Editor 촬영의 기존 `UnityEditor.Search.SearchDatabase` 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 별도로 남았으며 Console 전체가 깨끗하다는 주장은 아니다. 레거시 프로젝트·열린 작업용 Editor·씬/프리팹·그림/클립·원본 파티클/렌더링·패키지·프로젝트 설정은 이번 작업에서 편집/조작하지 않았다.
- 출시 빌드·전체 PlayMode·성능 프로파일·다른 화면 비율/장치·자연 전투 밸런스는 검증하지 않았다. XML/log/PNG/촬영 도구/소스 근거를 보존하고 이번 격리 검사 사본만 정리한다. 별도 Inspector 연결 없이 Play를 다시 시작하면 적용된다.

## 일반 기술 4종 이식 검증 (2026-09-27)

검증 상태: **Ready with limitations**. 준비(10)·전진(12)·투지(19)·발검(42)을 상점·편성·전투에 추가했다. 체인 기술 35~40은 제외했다. 정확한 수치·발동 시점·원본 설명과 코드의 차이·3스테이지부터의 적 방어 패턴은 `ImportedNonChainSkills.md`를 따른다. 기존 구매 6종의 미이식 고유 효과는 추가하지 않았다.

- 최종 통합 사본의 전체 EditMode **277/277 통과**, 실패/건너뜀0, 0.3914188초. 신규 일반 기술 검사48개는 구매·같은 열 편성·강화, 준비/전진의 다음 칸 적용·턴 종료 만료·가산, 전진의 기존 저항 넘침 배율, 투지의 최대 저항 기준·반올림·상한·예약된 자동 회복, 발검의 방어 조건·직접 감소·체력 넘침 없음·한 번 발동·플레이어 전용 ACT 보상을 검증한다. 병행 추가된 숨고르기와의 상호작용3개도 포함한다. 근거는 `Logs/NonChainSkills-15d164b7-IntegratedEditRetryResults.xml`과 `NonChainSkills-15d164b7-IntegratedEditRetry.log`다.
- 최종 graphics PlayMode **81/81 통과**, 실패/건너뜀0, 43.8644941초. 상점·편성·역할·스킬 정보·카드 배치·애니메이션·실제 Controller·스텝/숨고르기 입력·저항 피드백14개 fixture를 실행했다. 저항 변화 검사4개는 실제 회복 +2/감소 -15, 첫 타격 전 적용, 무넉백·무히트 스톱, 실제 시간 만료·초기화, 라벨2개 재사용·몸/카메라 추적·화면 경계·입력 비차단을 확인한다. 근거는 `Logs/NonChainSkills-15d164b7-IntegratedPlayModeResults.xml`과 `NonChainSkills-15d164b7-IntegratedPlayMode.log`다. 전체 PlayMode를 실행했다는 뜻은 아니다.
- 1600×900 Game View에서 발검·투지 상점 상세와 전장의 저항 -15/+2 표시4장을 촬영하고 육안 확인했다. 조건·위력·타입·가격 문구와 상태창/표시의 잘림·겹침을 확인했다. `Logs/ImportedSkills-Shop-DrawSword.png`, `ImportedSkills-Shop-FightingSpirit.png`, `ImportedSkills-Battle-DrawSword-ResistanceMinus15.png`, `ImportedSkills-Battle-FightingSpirit-ResistancePlus2.png` 및 `NonChainSkills-15d164b7-Visual.log`의 `IMPORTED SKILL VISUAL COMPLETE`가 근거다. 촬영은 숨고르기 통합 이전의 스킬 이식 사본에서 실제 리소스와 비활성 Controller에 수동 시계를 적용한 fixture이며, 자연 전투·전체 밸런스 근거가 아니다. 촬영 도구 원문은 `Logs/ImportedSkills-CaptureTool.txt`에 보존한다.
- 최초 관련 PlayMode는74/77, 다음은76/77이었다. 늘어난10종을 모두 사는 검사 예산을8회 승리로 늘렸고, 카테고리 전환 직후 삭제 예약된 비활성 버튼 대신 현재 활성 버튼을 눌러 실제 선택 이름과 기존 높이 단언을 함께 검사했다. 기존 연타 검사는 복제 설정이 Controller에만 적용돼 전장의 새 이동 배율을 사용하던 문제였다. 같은 복제본을 전장에도 연결·원복하고 해당 검사만 원래 거리/속도1을 지정해 기존 사거리·넉백·타격 수·시계 단언을 유지했다. 게임 이동 코드는 바꾸지 않았다. `FinalPlayMode`, `VerifiedPlayMode`, `FocusedStepInput`, `ControlledMotionStep`, `FixtureBoundStep`, `CompletePlayMode` XML/log는 `NonChainSkills-15d164b7-` 접두사로 보존했다.
- 통합 첫 실행은 병행 추가된 숨고르기 테스트가 다른 어셈블리의 비공개 위력을 직접 읽어 컴파일에 실패했고 검사는 실행되지 않았다. 기존 검사 방식인 reflection으로 같은0 위력 단언을 유지한 뒤 재실행했다. 실패 로그는 `NonChainSkills-15d164b7-IntegratedEditMode.log`에 남겼다. 생산 API를 공개하거나 검사를 제외하지 않았다.
- 초기 전체 EditMode198/198, 스킬 이식 사본253/253 및 관련 PlayMode77/77과 최종 통합 결과를 구분해 보존했다. 최종 통합은 현재 `Assets/Game`의 C#·metadata 스냅샷에 한정하며, 별도 작업의 숨고르기·Dialogue·HUD 변경을 포함해 컴파일/관련 실행을 검사했다. 이 병행 기능의 구현을 이번 스킬 이식 작업의 성과로 간주하지 않는다. 종료 시 캡처한354개 C#·metadata는 원본/실행 사본/기록 해시가 모두 같고 불일치0이다. Assets metadata262개는 잘못된GUID/중복GUID0이다. 스냅샷 시점·SHA256·검증 원문은 `Logs/NonChainSkills-15d164b7-IntegratedSnapshot.json`, `Logs/NonChainSkills-15d164b7-IntegratedHashEvidence.json`, `Logs/NonChainSkills-15d164b7-IntegratedTestedSnapshot/`에 보관했다. 이후 별도 작업의 변경까지 검증됐다고 주장하지 않는다.
- 최종 관련 실행에서 게임 C# 컴파일 오류나 게임 예외는 관찰하지 않았다. 기존 obsolete API 경고와 native Editor의 SearchDatabase 시작 인덱싱 예외·종료 JobTempAlloc 경고는 별도로 남아 있으므로 Console 전체가 깨끗하다는 주장은 아니다. 레거시 프로젝트·열린 작업용 에디터·씬·프리팹·그림·클립·파티클·렌더링·패키지·프로젝트 설정은 이번 작업에서 편집/조작하지 않았다.
- 출시 빌드·전체 PlayMode·성능·다른 화면 비율/장치·8단계 자연 난이도/손맛은 검증하지 않았다. 기존 강화/가격식과 원본 레벨0을 사용한 초기 밸런스다. XML/log/PNG와 검증 원문은 보존하고 이번 검증 사본만 정리한다. Play를 다시 시작하면 상점에서 구매하고 편성에 넣어 사용할 수 있다.

## 최신 변경: W 강공·E 기교 명칭 교환 (2026-09-27)

Q 정공을 유지하고 W 강공·E 기교로 표시 이름만 바꿨다. 생산 변경은 `SkillLaneStyle`의 Names/FullNames 두 배열뿐이며 나머지 코드가 동일함을 정적 비교로 확인했다. 전투·편성·상점의 이름 사용처를 확인하고 명시적인 검사 기대값6곳과 현재 기획 문서를 갱신했다. 키/색/기술 소속/효과/편성/전투 규칙은 변경하지 않았다. 이번 문구 변경에서는 Unity 컴파일/PlayMode/빌드를 재실행하지 않았으며 아래 실행 결과는 각 이전 변경 시점의 기록이다.

## 이전 변경: ACT 없는 공통 행동 숨고르기 (2026-09-27)

검증 상태: **Ready with limitations**. 준비 단계에서 S 또는 별도 버튼으로 ACT 없이 큐에 Wait 슬롯을 최대3회 추가한다. 버튼에 휴지 문양/이름/ACT0/남은 N/3/S와 순서 조절 안내를 표시하며, 큐·현재 행동·기록도 같은 문양을 사용한다. ACT0에서도 가능하고 길게 누르기/네 번째 입력은 추가하지 않는다. QWE 기술열/상점의 호흡(ID17)/적 패턴은 바꾸지 않는다. 상대 공격은 무방비 체력 피해이며 회복/공격/방어/새 피해 배율은 없다. 한 슬롯의 상대 연타 전체를 기다리고 기존 버프 수명도 한 슬롯 소비한다. A 회피는 가능하고 내 Wait에 D 압박 성공은 없으며 기존 스텝 시도 ACT 페널티는 유지한다. 상세는 `BreathingAction.md`를 따른다.

- Unity6000.5.9f1 격리 사본 EditMode **16/16 통과**, 실패/건너뜀0,0.0533941초. `LegacyBreathingActionTests`: 횟수/ACT0/기술열 불변/혼합 큐·상대 방어를 넘기는 순번 변경/일반·붕괴 저항의 체력 피해/연타·빈 슬롯/위력 난수 비소모/일반 ACT 회복·Reset/압박 실패·스텝 페널티/회피/슬롯별 버프 수명/기술열 우회 방지/치명타 슬롯 종료를 확인했다. `Logs/BreathingEditModeResults.xml`와 `BreathingEditModeFinal.log`가 근거다.
- 관련 graphics PlayMode **26/26 통과**, 실패/건너뜀0,0.742849초. 필터 `BreathingInputPlayModeTests;BreathingHudPlayModeTests;StepInputPlayModeTests;MultiHitQueueHudPlayModeTests;LegacyAnimationPlayModeTests`: 신규 입력4/신규HUD6/기존 스텝입력10/연타큐2/애니메이션4개. 실제 S 입력·길게 누름 비반복/ACT0·혼합 순서/Controller 연타 슬롯·체력 피해·다음 턴 초기화/Tab·로비·튜토리얼 입력 차단과 버튼 raycast·동일 프레임4회 클릭 차단/실제 세션 횟수 동기화/확정·전투 비활성/큐·로그 문양·잘못된 기술 설명 방지/아이콘 재사용·소유자 해제를 확인했다. `BreathingPlayModeResults.xml`와 `BreathingPlayModeFinal.log`가 근거다.
- 실제1600×900 Game View에서 준비3/3·혼합6칸/ACT0·횟수0/3,1280×720에서 같은 혼합 큐와 진행 중 Wait 슬롯을 촬영하고4장 모두 육안 확인했다. QWE·숨고르기·기록·확정이 겹치지 않고 남은 횟수와 S가 표시된다. Wait 슬롯에서는 플레이어가 대기하며 A 원은 유지하고 D 원은 없다. `BreathingVisual.log`의 설정 자산 확인과 `BREATHING VISUAL COMPLETE`, `Breathing-Ready.png`, `Breathing-Mixed.png`, `Breathing-720-Mixed.png`, `Breathing-WaitSlot.png`가 근거다. 촬영은 실제 Controller에 수동 시계를 적용한 결정적 표시 fixture이며 자연 한 판의 손맛/난이도 검증은 아니다.
- 최초 두 실행은 신규 입력 검사에서 다른 어셈블리의 internal `PlayerPower`에 직접 접근해 컴파일 실패했고 검사는 실행되지 않았다(`BreathingEditMode.log`, `BreathingPlayMode.log`). 검사만 비공개 속성 reflection으로 바꾸어 위력0 단언을 유지한 뒤 재실행했다. 생산 규칙을 바꾸거나 검사를 생략하지 않았다. 최종 관련 실행에서 게임 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 SearchDatabase 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐으나 화면 확인을 막지 않았다. Console 전체가 깨끗하다는 주장은 아니다.
- 생산7개·신규검사3개·metadata3개 총13파일은 실행 사본과 SHA256 일치한다(`Breathing-TestedSnapshot.json`). Assets metadata262개: 잘못된GUID0/중복GUID0. 기존 enum 값/직렬화 필드/GUID를 유지하고 Wait/None은 끝에 추가했다. 생성 문양만 Art에서 정리하고 공유 Resources는 파괴하지 않는다. 이미지/atlas/씬/프리팹/설정 자산/패키지/ProjectSettings/레거시 프로젝트/열린 작업용 에디터는 편집·조작하지 않았다. 촬영 코드는 사본에만 있으며 `Breathing-CaptureTool.txt`에 보존한다.
- 전체 검사·출시 빌드·다른 화면 비율/장치·성능·밸런스 검사는 생략했다. 로그/XML/PNG/촬영 도구·해시는 보존하고 이 작업의 임시 복사본만 정리한다. 별도 Inspector 연결 없이 Play 재시작으로 적용한다. 기존 기초 튜토리얼은 순서를 유지하기 위해 숨고르기를 숨기며 별도 튜토리얼 추가는 이번 범위가 아니다.

## 이전 변경: 읽기 쉬운 피해 숫자와 빠른 지속 이동 (2026-09-27)

검증 상태: **Ready with limitations**. 피해 숫자를 기본168의 굵은 상아색 글자·붉은 테두리·검은 그림자로 바꿨다. 기존 강조 조건(저항 붕괴 또는 표시 피해12 이상)은 금색·추가1.3배 크기를 사용하며 새 치명타 판정/피해 배율은 추가하지 않았다. 실제0.10초 팝업·0.22초 정착·상승·마지막30% 페이드로 일반1.05초/강조1.25초 동안 표시한다. 원래 월드 타격점은 카메라에 재투영하되 상승/연타 간격은 화면 단위로 분리한다. 최대32개를 재사용하고 재대결에 초기화한다. 기본 이동 거리1.4배/속도1.2배를 추가해 피격 밀림·회피·압박을 크게, 접근·추격·밀림·스텝을 빠르게 표현한다. 캐릭터 크기/공격 프레임/스킬 간격/판정 사거리4/최소 간격2.8/미완료 넉백 상한6은 이번 변경으로 바꾸지 않았다. 튜닝은 `PresentationTuning.md`를 따른다.

- Unity6000.5.9f1 격리 사본 graphics PlayMode **79/79 통과**, 실패/건너뜀0,5.4535434초. 필터 `DamageNumberPlayModeTests;MovementPunchPlayModeTests;ReadableCameraPlayModeTests;ForestDuelPlayModeTests;StepMotionPlayModeTests;StepFeedbackPlayModeTests;StepFocusIntegrationPlayModeTests;CombatTempoPlayModeTests;PressureAttackTrailPlayModeTests;LegacyAnimationPlayModeTests;FixedHudPlayModeTests;PresentationTuningPlayModeTests`: 피해숫자8/이동배율6/카메라7/숲이동9/스텝이동5/스텝연출7/집중통합6/템포7/압박잔상7/애니메이션4/고정HUD7/튜닝6개. 표시0·음수/테두리·그림자/강조 크기/팝업·유지·페이드/실제 시계/연타 간격/카메라 재투영/32개 풀·재사용·Reset/튜닝 안전값과 다음 동작 적용을 검사했다. 기존 검사 단언은 새 거리·시계·숫자 배치 계약에 맞춰 유지했다. `Logs/HitMotionPlayModeResults.xml`와 `HitMotionCurrentPlayMode.log`가 최종 실행 근거다.
- 실제1600×900 Game View에서 Pop/Hold/MultiHit/Travel4장,1280×720에서 Hold1장을 촬영해5장 모두 육안 확인했다. 상아색3/금색18의 크기·테두리와 연타5/6/7의 분리, 양쪽 전신·기존 HUD의 표시를 확인했다. 10 피해 fixture에서 양쪽 지속 이동3.22/접촉 간격4를 기록했다. 기본 회피1.96, 압박 최대1.54(간격4에서는 최소 간격 때문에1.2), 밀림0.0833 전투초/스텝0.0667 실제초를 검사했다. `Logs/HitMotionVisualFinal.log`의 설정 자산 확인/`HIT MOTION VISUAL COMPLETE`와 `HitMotion-Pop.png`, `HitMotion-Hold.png`, `HitMotion-MultiHit.png`, `HitMotion-Travel.png`, `HitMotion-720-Hold.png`가 근거다.
- 최초 화면에서 연타 숫자 간격이 좁아 겹치는 것을 확인하고 대상 바깥쪽3칸의 가로 간격을150 화면 단위로 넓혔다. 변경 전5장은 `HitMotion-*-BeforeSpacing.png`와 최초 `HitMotionVisual.log`에 남겼다. 촬영은 Controller를 비활성화하고 원본 뷰/스프라이트에 수동 시계를 적용한 표시 fixture다. 자연 한 판의 입력/손맛/밸런스 근거가 아니며 기존 파티클·발광·잔상을 줄이지 않았다.
- 최초 검사는 신규 검사에서 Unity6.5의 obsolete `GetInstanceID()`를 호출해 컴파일 실패했고, 객체 참조 동일성 검사로 고쳐 재사용 단언을 유지했다(`HitMotionPlayMode.log`). 다음72/79 결과는 복사한 Library의 설정 연결6개와 신규 잔상 종료 fixture의0.22초 수명 대기 부족1개였다(`HitMotionBeforeRebindResults.xml`, `HitMotionPlayModeFinal.log`). 사본에서만 설정 Script/Resources 자산을 다시 가져오고 다음 에디터 프로세스에서 연결을 확인했다. 잔상은 기존 수명만큼 기다려 검사했으며 생산 잔상 코드는 바꾸지 않았다. 재가져오기 단독 호출은 컴파일 대기 때문에 자산을 읽지 못한 기록이며, 그 자체의 성공이라고 주장하지 않는다(`HitMotionSettingsRebind.log`).
- 간격 보완 뒤 중간 재실행은 동시 작업의 새 Dialogue 의존 파일이 사본에 없어서 Controller 컴파일에 실패했고 검사는 실행되지 않았다(`HitMotionVerifiedPlayMode.log`). 당시 `Assets/Game` 전체를 다시 복사해 동시 변경을 보존한 뒤 최종79개를 실행했다. **79개 결과는 해당 검증 시점 사본에 한정된다.** 이후 원본에서 별도로 진행된 Dialogue/저항 시스템 변경은 이번 구현의 변경이나 검증 완료로 간주하지 않는다. 13:39 증거 스냅샷에서 이번 생산/검사/metadata11개 중 Controller를 제외한10개가 실행 사본과 일치했으며, Controller의 새 저항 피드백 통합과 Runtime 변경은 보존했다. 검증된 Controller/Runtime 원문과 파일 해시는 `HitMotion-TestedController.txt`, `HitMotion-TestedRuntime.txt`, `HitMotion-TestedSnapshot.json`, 촬영 도구는 `HitMotion-CaptureTool.txt`에 남겼다.
- 이번 작업 기준 설정 자산·발광 코드·잔상 코드·발광 셰이더·패키지 manifest/lock6개는 시작 해시와 동일하다. 씬·프리팹·그림·클립·파티클 자산·공유 렌더 설정·레거시 프로젝트·열린 작업용 에디터는 편집/조작하지 않았다. 최종 관련 실행에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 SearchDatabase 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐으나 촬영을 막지 않았다. Console 전체가 깨끗하다는 주장은 아니다.
- 전체 PlayMode·출시 빌드·다른 화면 비율/장치·성능·자연 전투 손맛 검사는 생략했다. 한 프레임의 실제 시간이 숫자 수명 이상으로 길어지는 기존 생성 프레임 만료 경계는 별도 개선 범위로 남긴다. XML/log/PNG/검증 원문은 보존하고 이 작업의 검증 사본만 정리한다. 신규 설정은 클래스 기본값으로 적용하므로 별도 Inspector 연결 없이 Play 재시작으로 확인할 수 있다.

## 이전 변경: 모션을 읽기 위한 안정적인 교전 카메라 (2026-09-27)

검증 상태: **Ready with limitations**. 일반 타격의 즉시 크기2 확대/무작위1 위치 누적을 제거하고 기본3.5의 고정 교전 화면·가로0.35 데드존·실제 시간 반응6의 보간으로 바꿨다. 일반 흔들림은0.08의 방향성 펄스이며 실제0.10초에 감쇠하고 동시에 겹친 타격에도 누적하지 않는다. 크기/반응/데드존/흔들림은 기존 설정에서 직접 조절한다. 스텝 집중/저항 붕괴의 대상·기울기·우선순위와 준비/Tab 구도는 유지한다. 저항 붕괴는 반응 최소10으로 빠르게 집중한다. 선택적 일반 회전만 최대2.5도로 줄였다. 판정·이동·클립·템포·파티클·빛·Bloom·노출·잔상·HUD는 변경하지 않았다. 사용법은 `PresentationTuning.md`를 따른다.

- Unity6000.5.9f1 격리 사본 graphics PlayMode **60/60 통과**, 실패/건너뜀0,5.061792초. 필터 `ReadableCameraPlayModeTests;StepFeedbackPlayModeTests;StepFocusIntegrationPlayModeTests;FixedHudPlayModeTests;CombatTempoPlayModeTests;PressureAttackTrailPlayModeTests;LegacyAnimationPlayModeTests;PresentationTuningPlayModeTests;ForestDuelPlayModeTests`: 신규카메라7/스텝연출7/집중통합6/고정HUD7/템포7/압박잔상7/애니메이션4/튜닝6/숲이동9개. 즉시 카메라 변경 없음,40회 반복 비누적, 전투 시계0 중 펄스 소멸, 데드존/연속 이동, 실제 여러 포즈/연타의 배율 안정, 기본값/범위/NaN·Infinity 안전값/실행 중 적용, 단계 전환/Reset, 기존 스텝·저항 붕괴·UI·타격 시계·파티클 동작을 검사했다. `Logs/ReadableCameraPlayModeResults.xml`/`ReadableCameraPlayModeFinal.log`가 근거다. 기존 검사 파일은 수정하지 않았다.
- 실제1600×900 Game View에서 변경 전·후의 같은 두 번째 베기 포즈/위치/두 발광을 비교했다. 이전 카메라 크기2.091889에서는 몸통·발이 화면 밖으로 잘렸고, 변경 후3.505836에서는 양쪽 전신과 검이 보였다. 스텝 집중3.157306/암전/잔상도 유지됐으며1280×720의 일반 구도도 확인했다. `Logs/ReadableCameraVisual.log`의 `READABLE CAMERA SETTINGS SCRIPT TurnLimbo.Presentation.DuelPresentationSettings ASSET True`/`READABLE CAMERA VISUAL COMPLETE`, `ReadableCamera-Before-Impact.png`, `ReadableCamera-After-Impact.png`, `ReadableCamera-After-Step.png`, `ReadableCamera-720-Impact.png`4장을 모두 육안 확인했다. 비교는 이전 전장 코드의 복제 클래스와 현재 전장 클래스를 사용하며 과거 코드/촬영 도구는 `ReadableCamera-BeforeArena.txt`/`ReadableCamera-CaptureTool.txt`에 보존했다.
- 촬영은 Controller를 비활성화하고 수동 시계를 적용한 표시 fixture다. 현재 화면에만 기존 HUD를 추가했으며 자연 한 판의 입력/전체 손맛·난이도 검증은 아니다. 접촉점의 강한 빛이 검 일부를 가리는 현상은 여전히 보인다. VFX를 줄여 좋아 보이는 비교를 만들지 않았고, 카메라 변경만으로 모든 모션 읽기 문제가 해결됐다고 보장하지 않는다.
- 최초 실행은 임시 촬영 도구가 Presentation의 internal 포즈 샘플러를 직접 호출하여 Editor 도구 어셈블리만 컴파일 실패했다. 게임 코드나 검사 단언은 바꾸지 않고 사본 도구만 reflection 호출로 보완했다. 검사 미실행의 최초 기록은 `Logs/ReadableCameraPlayMode.log`에 보존하고 별도 Final 기록으로 재실행했다. 최종 관련 실행에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 SearchDatabase 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐으나 촬영을 막지 않았다. Console 전체가 깨끗하다는 주장은 아니다.
- 생산2개/신규검사1개/신규metadata1개 총4파일은 실행 사본과 SHA256 일치한다. 시작 기준 Runtime16개·Controller·튜닝 자산·발광·잔상·발광 셰이더 총21개는 모두 같은 해시다. Assets metadata254개: 잘못된GUID0/중복GUID0. 씬·프리팹·그림·재질·클립·파티클·패키지·ProjectSettings·레거시 프로젝트는 편집하지 않았으며 열린 작업용 에디터도 조작하지 않았다. 촬영용 C#는 사본에만 존재한다. XML/log/PNG/이전 코드·도구 기록은 보존하고 검증용 사본만 정리한다. 새 카메라 값은 클래스 기본값을 사용하므로 별도 Inspector 연결 없이 Play 재시작으로 적용된다.
- 전체 PlayMode/플레이어 출시 빌드/다른 화면 비율·장치/성능·밸런스는 생략했다. 화면 근거는16:9의 두 해상도와 수동 표시 비교에 한정된다.

## 이전 변경: Q 정공·W 기교·E 강공 검술 분류 (2026-09-27)

검증 상태: **Ready with limitations**. 기존 기술열에 선택한 검술 이름과 황동/청록/적동 종이 표식을 붙였다. 전투 QWE 버튼·양측 설명·편성 제목/필터/선택 상세·상점 상세를 공유 매핑으로 통일하고 적 설명에는 입력 키를 숨긴다. 분류는 실제 `LegacySkill.LaneIndex`, 공격 타입은 별도 `Property`를 따른다. 획득16 일도양단은 W 기교·참격이다. 기술 효과/위력/키/큐/가격/강화/각열3개 저장 규칙은 유지한다. ‘공격·방어 모두 적용’은 후속 위력 강화의 적용 범위이며 모든 기술의 공통 효과가 아니다. 이번 변경은 해당 문구나 판정을 바꾸지 않는다. 상세 계약은 `SkillInformationDesign.md`의 검술 분류를 따른다.

- Unity6000.5.9f1 격리 사본 graphics PlayMode **56/56 통과**, 실패/건너뜀0,9.8908968초. 필터 `SkillLaneStylePlayModeTests;CompactSkillCardPlayModeTests;SkillCardAttachmentsPlayModeTests;SkillInfoViewPlayModeTests;SwordSkillRoleHudPlayModeTests;DuelVisualThemePlayModeTests;LobbyScreensPlayModeTests;CampaignLoadoutHudPlayModeTests;CampaignShopHudPlayModeTests;CompactHudPlayModeTests`: 신규6/컴팩트정보5/외부표식6/정보6/역할3/테마4/화면6/편성7/상점5/컴팩트HUD8개. 전체15기술·양측·강화·같은id/icon의다른liveLane, 스타일/타입 분리, 적 키 숨김, 배지 재사용·Clear·비차단 장식, 편성 수량/순서·저장 상태·거래/재화 불변, 버튼 콜백을 확인했다. `Logs/SkillLaneStylePlayModeResults.xml`/`SkillLaneStylePlayModeFinal.log`가 근거다.
- 실제1600×900 Game View에서 내W찌르기·적Q방어·편성 세분류와W선택·상점W참격4장,1280×720에서W설명1장을 촬영해5장 모두 육안 확인했다. 헤더 색/키/분류 이름·편성 강화 단계·외부 타입 리본·ACT·본문의 겹침이나 잘림이 없었다. `Logs/SkillLaneStyleVisual.log`의 `LANE STYLE SETTINGS SCRIPT TurnLimbo.Presentation.DuelPresentationSettings ASSET True`/`SKILL LANE STYLE VISUAL COMPLETE`, `SkillLaneStyle-Player-W.png`, `SkillLaneStyle-Enemy-Q.png`, `SkillLaneStyle-Loadout.png`, `SkillLaneStyle-Shop-W-Slash.png`, `SkillLaneStyle-720-W.png`가 근거다. 실제 뷰의 결정적 표시 fixture이며 전체 자연 플레이/손맛 검증은 아니다.
- 최초 사본 에디터 실행은 초기 어셈블리 로드에서 진행이 멈춰 테스트 결과를 생성하지 못했다. 해당 사본 프로세스만 종료하고 기록 `Logs/SkillLaneStylePlayMode.log`를 보존했다. 종료 직후 기록 이동도 파일 잠금으로 실패해, 기존 기록을 덮어쓰지 않는 별도 `SkillLaneStylePlayModeFinal.log`로 다시 실행했다. 재실행은 검사를 완료했으며 설정 재가져오기/소스 설정 변경은 필요하지 않았다.
- 생산4개/신규검사1개/metadata2개 총7파일은 실행 사본과 SHA256 일치한다. Runtime16개는 시작 해시와 동일하다. Assets metadata251개: 잘못된GUID0/중복GUID0. 씬·프리팹·그림·재질·파티클·튜닝 자산·패키지·ProjectSettings·레거시 프로젝트를 편집하지 않았고 열린 작업용 에디터를 조작하지 않았다. 캡처 도구는 임시 사본에만 있으며 XML/log/PNG를 보존하고 사본만 정리한다.
- 최종 관련 실행에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. 기존 native Editor의 SearchDatabase 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐으나 촬영을 막지 않았다. Console 전체가 깨끗하다는 주장은 아니다. 전체 PlayMode·출시 빌드·다른 화면 비율/장치·성능/밸런스 검사는 생략했다. 별도 Inspector 연결 없이 Play를 다시 시작하면 적용된다.

## 이전 변경: 공백을 줄인 스킬 설명판 (2026-09-27)

검증 상태: **Ready with limitations**. 외부 표식·키워드 색·설명·조작은 유지하면서 ACT/타입 표식을 제목 양옆으로 올리고 위력 표식을 본문 옆에 붙였다. 고정224px 본문 대신 실제 폰트16의 `preferredHeight`를 읽어 설명판 높이를 정한다. 키워드1개는 전폭으로 펼치며 전투 양측 팝업·편성·상점 상세판을 함께 압축했다. 로비 상세는 상단을 고정하고 아래쪽만 늘어난다. 구매에서는 빈 강화 미리보기를 접고 강화에서는 실제 줄 수로 복원한다. 가격과 보유 재화는 한 줄에 모았다. 상세 계약은 `SkillInformationDesign.md`에 기록했다.

- Unity6000.5.9f1 격리 사본 graphics PlayMode 최종 **50/50 통과**, 실패/건너뜀0,9.5249306초. 필터 `CompactSkillCardPlayModeTests;SkillCardAttachmentsPlayModeTests;SkillInfoViewPlayModeTests;SwordSkillRoleHudPlayModeTests;DuelVisualThemePlayModeTests;LobbyScreensPlayModeTests;CampaignLoadoutHudPlayModeTests;CampaignShopHudPlayModeTests;CompactHudPlayModeTests`: 신규 압축5/표식6/정보6/역할3/테마4/화면6/편성7/상점5/컴팩트 HUD8개. 전체15기술·양측·334/416폭의 실제 본문/키워드 높이와 정보 보존, 장단 설명 전환, 빈 안내, 한 개 키워드 전폭, 뷰 재사용·비차단 장식, 실제 전투의 제목·표식·본문·닫기 안내, 편성 상세 상단 고정·해제 버튼, 상점 미리보기 토글·거래 영역 경계·재화/편성 불변을 확인했다. `Logs/CompactSkillCardPlayModeResults.xml`/`CompactSkillCardPlayMode.log`가 근거다.
- 최초49/50(9.4063078초)의 실패1개는 기존 테마 검사가 구매 화면에서 빈 `Shop Upgrade Preview`도 항상 활성이라고 가정한 부분이다. 구매에서는 숨김을 명시적으로 확인하고 강화로 전환한 뒤 미리보기의 가시성·본문·잉크색·대비4.5:1을 다시 검사하도록 계약을 보완했다. 검사를 생략하거나 색 대비 단언을 제거하지 않았다. 최초 결과는 `Logs/CompactSkillCardPlayModeFirstResults.xml`/`CompactSkillCardPlayModeFirst.log`에 보존했다.
- 실제1600×900 Game View의 참격·타격·관통·적 방어·편성 관통·상점 구매·상점 강화7장과1280×720 관통1장을 캡처하고8장 모두 육안 확인했다. 짧은 설명의 빈 영역 축소, 긴3행의 마지막 줄, 제목과 표식 간 간격, 본문·버튼 분리, 구매 미리보기의 공백 제거를 확인했다. `Logs/CompactSkillCardVisual.log`의 `COMPACT SKILL CARD VISUAL COMPLETE`, `CompactSkillCard-Player-Slash.png`, `CompactSkillCard-Player-Hit.png`, `CompactSkillCard-Player-Penetrate.png`, `CompactSkillCard-Enemy-Defence.png`, `CompactSkillCard-Loadout-Penetrate.png`, `CompactSkillCard-Shop-Variance.png`, `CompactSkillCard-Shop-Upgrade.png`, `CompactSkillCard-720-Penetrate.png`가 근거다. 캡처는 실제 뷰를 구성한 결정적 표시 fixture이며 전체 자연 플레이나 손맛 검증은 아니다.
- 최초 촬영 준비는 복사한 Library에서 Resources 설정 확인이 실패했다. 실패 로그는 `CompactSkillCardVisualFirst.log`에 남겼다. 설정 Script와 자산을 **사본에서만** 다시 가져오고 컴파일 이후 별도 프로세스에서 촬영 단계부터 재개했다. 최종 `COMPACT SETTINGS SCRIPT TurnLimbo.Presentation.DuelPresentationSettings ASSET True`와8장 생성이 근거다. 소스/사본의 설정 자산·스크립트·metadata SHA256은 동일하며 원본 설정을 변경하지 않았다.
- 생산4개/신규검사1개/metadata1개/기존 테마검사1개 총7파일은 최종 실행 사본과 SHA256 일치한다. Runtime16개는 시작 해시와 동일하다. Assets metadata246개: 누락0/잘못된GUID0/중복GUID0. 그림·셰이더·재질·씬·프리팹·패키지·ProjectSettings·튜닝 자산·레거시 프로젝트를 편집하지 않았다. 작업용 에디터를 조작하지 않았고 캡처 도구는 임시 사본에만 있다. 로그/XML/PNG를 보존하고 검증용 임시 사본만 정리했다.
- 최종 관련 실행에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐으나 촬영을 막지 않았다. Console 전체가 깨끗하다는 주장은 아니다. 전체 PlayMode·출시 플레이어 빌드·다른 화면 비율/플랫폼·성능/밸런스 검증은 생략했다. 별도 Inspector 연결 없이 Play를 다시 시작하면 적용된다.

## 이전 변경: 스킬 정보의 외부 인장·타입 리본 (2026-09-27)

검증 상태: **Ready with limitations**. 내부 ACT/Power/Hits 3칸 표를 제거하고 왼쪽 ACT 인장·위력 금속판·오른쪽 타입 리본을 종이 설명판 가장자리에 붙였다. 실제 `LegacySkill.Property`의 참격/타격/관통/방어를 글자와 염색 종이 색으로 명시한다. 공격의 타격 수와 방어의 같은 칸 대응도 리본에 표시한다. 전투 양측 팝업·편성·상점이 같은 뷰를 사용하고 기존 키워드 색, 조건 설명, 입력·거래·편성 규칙을 유지한다. 상세 계약은 `SkillInformationDesign.md`에 기록했다.

- Unity6000.5.9f1 격리 사본 graphics PlayMode 최종 **45/45 통과**, 실패/건너뜀0,9.3912378초. 필터 `SkillCardAttachmentsPlayModeTests;SkillInfoViewPlayModeTests;SwordSkillRoleHudPlayModeTests;DuelVisualThemePlayModeTests;LobbyScreensPlayModeTests;CampaignLoadoutHudPlayModeTests;CampaignShopHudPlayModeTests;CompactHudPlayModeTests`: 신규6/정보6/역할3/테마4/화면6/편성7/상점5/컴팩트 HUD8개. 전체15기술·적·강화 기술의 실제 타입, 아이콘/이름과 독립된 타입 판독, 불투명 타입 색 구분, 두 카드 폭의 실제 돌출 경계, 방어 수치와 공격의 분리, 반복 선택·Clear·비차단 장식, 실제 HUD 양측의 네 화면 모서리 clamp, 실제 편성/상점 선택과 여정 불변을 확인했다. `Logs/SkillCardPlayModeResults.xml`/`SkillCardPlayMode.log`가 근거다. 초기45/45(9.309141초)는 `BeforeFooter-SkillCardPlayModeResults.xml`/`BeforeFooter-SkillCardPlayMode.log`에 보존했다.
- 실제1600×900 Game View에서 참격·타격·관통·적 방어·편성 관통·상점 변동 위력6장,1280×720에서 관통1장을 캡처했다. 최종7장 모두 육안 확인했으며 외부 표식/키워드/본문/기존 버튼의 겹침과 잘림이 없었다. 첫 화면에서 리본 아래 글자의 여백이 부족해 리본 높이와 텍스트 위치만 조정했다. 캡처 도구의 저장 대기 순서도 수정해 각 파일이 해당 선택을 표시하도록 재촬영했다. `Logs/SkillCardVisual.log`의 `SKILL CARD ATTACHMENTS VISUAL COMPLETE`와 `SkillCard-Player-Slash.png`, `SkillCard-Player-Hit.png`, `SkillCard-Player-Penetrate.png`, `SkillCard-Enemy-Defence.png`, `SkillCard-Loadout-Penetrate.png`, `SkillCard-Shop-Variance.png`, `SkillCard-720-Penetrate.png`가 최종 근거다. 이전 그림/로그는 `*-BeforeCaptureSpacing.png`/`BeforeFooter-SkillCardVisual.log`에 보존한다. 캡처는 결정적 표시 fixture이며 전체 자연 플레이의 검증은 아니다.
- 생산3개/metadata1개/신규검사1개/metadata1개 총6파일은 최종 실행 사본과 SHA256 일치한다. Runtime16개는 시작 해시와 동일하다. Assets metadata244개: 누락0/잘못된GUID0/중복GUID0. 새 표식은 캐시된 점 기반 uGUI 메시이며 이미지·셰이더·재질·씬·프리팹·패키지·ProjectSettings·튜닝 자산·레거시 프로젝트를 편집하지 않았다. 다른 다이얼로그 작업은 보존하고 이 검증 범위에 포함하지 않는다. 작업용 에디터를 조작하지 않았고 캡처 도구는 임시 사본에만 있다. 로그/XML/PNG를 보존하고 검증용 임시 사본만 정리했다.
- 관련 실행에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐으나 캡처를 막지 않았다. Console 전체가 깨끗하다는 주장은 아니다.
- 전체 PlayMode, 출시 플레이어 빌드, 다른 화면 비율·플랫폼, 성능 및 밸런스 검증은 생략했다. 별도 Inspector 연결 없이 Play를 다시 시작하면 적용된다.

## 이전 변경: 압박 공격의 지속 잔상 (2026-09-27)

검증 상태: **Ready with limitations**. 압박의 짧은 이동 뒤에도 현재 공격 Sprite를 실제0.035초 간격으로 기존12개 풀에 기록한다. 내 기술의 마지막 공격 클립 종료에 생성을 멈추고 남은 잔상은 기존0.22초 수명으로 사라진다. 연타의 Idle 간격에는 생성하지 않으며 다음 타격에서 재개한다. 컨트롤러의 준비/접촉 프레임 보정 뒤 최종 표시 자세를 사용하고, 슬롯 시작에 고정한 재생 속도와 타격 간격을 따른다. 성공/실패 모두 동작 피드백이며 회피·방어 압박의 짧은 이동, 피해·ACT 규칙은 그대로다. 상세 계약은 `StepPrototype.md`에 기록했다.

- Unity6000.5.9f1 격리 사본 graphics PlayMode **35/35 통과**, 실패/건너뜀0,0.8044537초. 필터 `PressureAttackTrailPlayModeTests;StepMotionPlayModeTests;StepFeedbackPlayModeTests;StepFocusIntegrationPlayModeTests;StepInputPlayModeTests`: 신규7/이동5/집중7/집중통합6/입력10개. 이동·기존 수명 종료 뒤 현재 공격 프레임 추적, 연타 간격 중단/재개, 느린 자기 클립 종료와 상대 긴 기술의 비확장, 준비 생성 차단, 회피/방어 유지, 슬롯·턴·Reset·Dispose·로비 정리, 긴 프레임 한 번 생성과12개 풀 재사용, 실제 컨트롤러 연결을 확인했다. `Logs/PressureTrailPlayModeResults.xml`/`PressureTrailPlayMode.log`; 로그의 종료 코드0.
- 실제1600×900 Game View의 결정적3타격 fixture에서 초기 이동, 마지막 타격, 종료 뒤 화면을 기록했다. 초기 이동과0.22초 잔상 수명을 지난 세 번째 공격(슬롯0.6334698초)에서 새 잔상6개가 활성이고 이동은 끝난 상태였다. 마지막 자기 공격 종료 뒤 생성 상태를 해제하고0.25초 후 잔상0개를 단언했다. `Logs/PressureTrailVisual.log`의 `PRESSURE ATTACK TRAIL VISUAL COMPLETE`와 `PressureTrail-Dash.png`, `PressureTrail-Third-Attack.png`, `PressureTrail-After-Attack.png`가 근거다. 초기 이동과 마지막 타격 그림을 육안 확인했다. 해당 압박의 기존 체력 타격 집중/색 변화도 유지된다. 정지 Controller에 수동 시계를 적용한 fixture이며 전체 손맛·밸런스 검증은 아니다.
- 생산2개/신규검사1개/metadata1개는 실행 사본과 SHA256 일치한다. Runtime의 LegacyCombat5개는 시작 해시와 같다. Assets metadata242개: 누락0/잘못된GUID0/중복GUID0. 이번 변경에서 그림·셰이더·재질·씬·프리팹·패키지·ProjectSettings·튜닝 자산·레거시 프로젝트를 편집하지 않았다. 별도로 진행된 다이얼로그 편집기 변경은 보존하며 압박 검증 범위에 포함하지 않는다. 열려 있는 작업용 에디터를 조작하지 않았고 검증용 도구는 임시 사본에만 있다. 로그/XML/PNG를 보존하고 임시 사본은 정리했다.
- 자동 검사에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor 시작의 기존 `UnityEditor.Search.SearchDatabase` 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐으며 캡처를 막지 않았다. Console 전체가 깨끗하다는 주장은 아니다.
- 전체 PlayMode, 출시 플레이어 빌드, 다른 화면 비율·플랫폼, 성능 측정 및 밸런스 검증은 생략했다. 별도 Inspector 연결 없이 Play를 다시 시작하면 적용된다.

## 이전 변경: Unity 다이얼로그 편집기·화자 이미지 (2026-09-27)

검증 상태: **Ready for authoring with limitations**. `Turn Limbo → 다이얼로그 편집기`에서 Dialogue 폴더의 빈 텍스트 생성, 열기·저장·다른 이름 저장, 0.8초 지연 자동 저장, 외부 변경 충돌 보호, 원문 편집, 빠른 블록 입력, 실시간 런타임 문법 검사, 오류 줄 번호, 화자 목록과 Sprite 지정, 현재 줄 미리보기를 제공한다. 원문을 기준 데이터로 유지해 주석과 수동 서식을 재직렬화하지 않는다. 화자 이미지는 처음 지정할 때만 공용 `DialoguePortraitCatalog.asset`을 생성하며 실제 HUD에서 좌·우로 표시하고 내레이션·미지정 화자에서는 숨긴다.

- 격리 사본 EditMode **28/28 통과**, 실패/건너뜀 0, 0.3570594초. 기존 `DialogueScriptParserTests` 19개와 신규 `DialogueAuthoringUtilityTests` 9개로 빈 초안, 런타임 파서 공유, 화자 추출, 오류 줄, 빠른 입력 문법·이스케이프, 잘못된 입력 거부, 경로 이탈 차단, 원자 교체 방식의 BOM 없는 UTF-8/LF 저장, Unity 재시작 뒤에도 발견 가능한 자체 설명형 Library 복구본, 초상화 지정·교체·해제를 확인했다. `Logs/DialogueEditorEditModeResults.xml`/`DialogueEditorEditMode.log`.
- 격리 사본 PlayMode 회귀 **21/21 통과**, 실패/건너뜀 0, 48.9710724초. `DialogueHudPlayModeTests` 6개, `CampaignLobbyHudPlayModeTests` 7개, `DuelPrototypePlayModeTests` 8개다. 기존 다이얼로그·로비·전투 흐름과 함께 화자 이미지의 좌·우 이동, 내레이션 숨김, 이미지 없는 기존 문서 호환을 확인했다. `Logs/DialogueEditorPlayModeResults.xml`/`DialogueEditorPlayMode.log`.
- Unity 6000.5.9f1에서 새 Editor 전용 asmdef와 플레이어 어셈블리 분리를 컴파일했다. Runtime은 계속 UnityEngine 비참조이며 Sprite/ScriptableObject는 Presentation에만 둔다. Assets metadata 누락 0, 중복 GUID 0이다.
- 예시 스토리 내용, 자동 시작 지점, 로비 버튼, 씬·프리팹·패키지·ProjectSettings·입력 설정·레거시 프로젝트는 변경하지 않았다. 작업 시작 전에 존재한 0바이트 `chapter-01-intro.txt`의 내용도 변경하지 않았다.
- 자동 저장은 쓰기 직전 파일 시각을 다시 확인하며, 외부 변경·삭제 또는 저장 실패 상태로 창이 닫히면 `Library/TurnLimbo/DialogueRecovery/`에 복구본을 남긴다. 문서를 바꾸면 해당 창의 Undo 기록을 비워 다른 파일에 이전 원문이 적용되지 않게 한다.
- 초상화 연결은 대소문자를 포함한 정확한 화자 이름과 프로젝트 공용 기본 이미지 한 장을 사용한다. 문서별 표정 변화, 화자 ID, 선택지·분기, 실제 EditorWindow 수동 시각 점검과 출시 빌드는 이번 검사 범위가 아니다.

## 이전 변경: 텍스트 작성형 다이얼로그 기반 (2026-09-27)

검증 상태: **Ready for authoring with limitations**. 한 줄을 한 번의 진행 단위로 사용하는 텍스트 파서와 진행 세션, 좌·우 화자/내레이션을 표시하는 모달 HUD, 로비에서 명시적으로 호출할 수 있는 시작·진행·닫기 API를 추가했다. 사용자가 스토리를 정하기 전이므로 예시 스토리 내용, 자동 시작 지점, 로비 버튼은 추가하지 않았다.

- EditMode `DialogueScriptParserTests` **19/19 통과**, 실패/건너뜀 0, 0.0339548초. UTF-8 한글과 문장부호, 좌·우 화자, 내레이션, 화자 재사용, 주석·이스케이프, BOM 및 CRLF/LF/CR, 원본 줄 번호가 포함된 오류, 빈 스크립트 및 240자 초과 표시 줄 거부, 진행 세션을 확인했다. `Logs/DialogueEditModeResults.xml`/`DialogueEditMode.log`.
- PlayMode `DialogueHudPlayModeTests` **5/5 통과**, 실패/건너뜀 0, 0.1570774초. 좌·우·내레이션 배치, 진행 표시, 입력 차단 배경과 버튼 설정, 로비 차단 및 복구, Escape 닫기, 실제 Enter 진행과 마지막 줄 종료 프레임의 로비 입력 비전달을 확인했다. `Logs/DialoguePlayModeResults.xml`/`DialoguePlayMode.log`.
- 다이얼로그 5개와 기존 `CampaignLobbyHudPlayModeTests`, `DuelPrototypePlayModeTests`를 함께 실행한 회귀 PlayMode **20/20 통과**, 실패/건너뜀 0, 41.2953379초. `Logs/DialogueRegressionPlayModeResults.xml`/`DialogueRegressionPlayMode.log`.
- Unity 6000.5.9f1 격리 사본에서 컴파일과 검사를 수행했으며, 다이얼로그 입력이 열린 프레임 및 닫힌 프레임에 로비/전투로 새지 않도록 우선 처리한다. 씬·프리팹·패키지·ProjectSettings·입력 설정·레거시 프로젝트는 변경하지 않았다. Assets metadata 누락 0, 중복 GUID 0을 확인했다.
- 당시 첫 버전은 선형 진행만 지원했고 선택지, 분기, 변수, 이벤트 명령, 초상화, 타자 효과, 저장 지점을 범위에서 제외했다. 초상화와 Unity 작성 도구는 위 최신 변경에서 추가했으며 나머지 범위는 유지한다.

## 이전 변경: 몸 중심 타이밍 원·스텝 카메라 집중 (2026-09-27)

검증 상태: **Ready with limitations**. A가 오른쪽/ D가 왼쪽이던 스킬 부착 원을 플레이어 몸 중심의 왼쪽 A 회피/오른쪽 D 압박 반원으로 교체했다. 원은 얇은 고정 선·다중 발광과 고정 성공 밴드/점선/눈금을 사용한다. 프레임별 Sprite.bounds 대신 기존 검 포함 그림의 기준점에서 고정 몸 오프셋(-.882,-.35)을 투영해 이동·확대·회전을 따라간다. 기본 이동은1.4/1.1, 잔상은 더 진하게0.22초, 성공 시 실제0.28초의0.25배 전투 시계와0.36초의 플레이어 카메라 집중/추가0.5 줌/배경55% 암전을 추가했다. 실패에는 약한 집중만 주며 같은 스킬의 반복 입력으로 성공 연출을 연장하지 않는다. 피해·ACT·큐·성공 판정 규칙은 그대로다. `StepPrototype.md`/`PresentationTuning.md`에 계약과 Inspector 항목을 기록한다.

- Unity6000.5.9f1 격리 사본 graphics PlayMode 최종 **57/57 통과**, 실패/건너뜀0,5.4986793초. 필터 `StepRingHudPlayModeTests;StepInputPlayModeTests;StepMotionPlayModeTests;StepFeedbackPlayModeTests;StepFocusIntegrationPlayModeTests;FixedHudPlayModeTests;MultiHitQueueHudPlayModeTests;CombatTempoPlayModeTests;PresentationTuningPlayModeTests`. 링7/입력10/이동5/집중7/집중통합6/고정HUD7/연타큐2/템포7/튜닝6개. 몸 투영·A/D 순서·줌에도 일정한 선/글자·성공 밴드·연타 숨김, 이동/추격/넉백 경계·풀 재사용, 실제 키 성공의 전투 시계 감속·Shift 최소배율·전역 Time.timeScale 불변·동시 A+D·반복/실패·턴/재시작/로비 정리, 배경만 암전·검사 tint와 비누적 결합·치명타 카메라 우선, 설정 기본값/범위와 기존 타격 간격을 확인했다. `Logs/StepFocusPlayModeResults.xml`/`StepFocusPlayMode.log`.
- 최초54/57의3개 실패를 구분해 수정했다. 로비 복귀의 스텝 상태 잔류는 생산 `ShowLobby`가 전장/HUD를 초기화하도록 고쳤다. 동시 A+D 검사는 수동 InputSystem.Update 뒤 다음 프레임을 기다려 입력 edge가 소비된 fixture 문제로, 하나의 키 이벤트 후 실제 입력 프레임을 기다리고 두 edge 및 두 성공 단언을 유지했다. Resources 설정 참조 실패는 복사한 Library의 stale MonoScript 연결이었다. 관련6파일의 원본/복사본 해시·유일한32자GUID·스크립트 경로·컴파일을 확인하고 **복사본에서만** 스크립트→설정 자산을 강제 재가져왔다. `StepFocusVisual.log`의 `FOCUS SETTINGS SCRIPT TurnLimbo.Presentation.DuelPresentationSettings ASSET True`와 후속 통과가 근거다. 설정/metadata를 변경하거나 검사를 생략하지 않았다. 최초 XML/log는 `StepFocusPlayModeFirstResults.xml`/`StepFocusPlayModeFirst.log`에 보존한다.
- 몸 오프셋 보정 전57/57(5.4659699초) 통과도 `StepFocusBeforeBodyCenterResults.xml`/`StepFocusBeforeBodyCenter.log`에 남기고, 최신 결과와 구분한다. 기존 후속 연타 사거리의 정확한.04/.05초 검사에는 새 실제 시간 집중만 만료시킨 뒤 원래 타격/밀림 단언을 유지했다. 새 통합 검사가 성공 슬로모션의 실제 슬롯 시계를 별도로 검증한다.
- 실제1600×900/1280×720 Game View에서 왼쪽 A/오른쪽 D·얇은 발광·고정 성공 구간·몸 정렬, 성공 후 배경 암전/카메라 집중·더 큰 양방향 이동/잔상, 회피 연타 전체 무피해·방어 압박 절반·다음 ACT 페널티를 확인했다. `Logs/StepFocusVisual.log`의 `STEP FOCUS VISUAL COMPLETE`와 최종 `StepFocus-*.png`12장(이전 몸 정렬1장 제외)이 근거다. 첫 화면에서 검 포함 Sprite pivot이 몸 오른쪽에 있음을 확인해 고정 오프셋으로 정리했다. 변경 전 로그/그림은 `StepFocusVisualBeforeBodyCenter.log`/`StepFocus-TimingBeforeBodyCenter.png`에 보존한다. 카메라 프레임·배경 밝기·키 위치를 실제 그림으로 확인했다.
- 캡처는 정지한 Controller에 수동 시계를 적용한 결정적 fixture다. 전체 손맛/자연 난이도/밸런스의 검증은 아니며 실제 키 입력은 위 PlayMode 검사가 근거다. 이전 EditMode 결과는 재실행하지 않았다. Runtime13개는 시작 해시와 모두 같고 생산7/검사5/metadata2 총14파일은 최종 실행 복사본과 SHA256 일치한다. 설정 자산도 복사본과 일치하며 기존 튜닝값/필드명/GUID를 보존한다. 신규 연출 항목은 안전한 필드 기본값으로 추가했으며 별도 씬 연결이 필요 없다. Assets metadata224개: 누락/잘못된GUID/중복GUID0.
- 이미지·씬·프리팹·클립·파티클 자산·공유 렌더 파이프라인·패키지·프로젝트 설정·레거시 프로젝트·열린 작업용 에디터는 변경하지 않았다. 최종 자동 검사에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류/설정 스크립트 연결 실패는 관찰하지 않았다. native Editor의 기존 SearchDatabase 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐다. Console 전체가 깨끗하다는 주장은 아니다.
- 전체 PlayMode·출시 빌드·다른 비율/장치·성능·밸런스 검사는 생략했다. 검증 도구는 임시 복사본에만 있고 XML/log/PNG는 보존한다. Play 재시작으로 적용하며 상단 `Turn Limbo → 연출 튜닝 열기`의 스텝 연출에서 강도를 조절한다.

## 이전 변경: 스킬 아이콘에 수축하는 흰 원 (2026-09-27)

검증 상태: **Ready with limitations**. 하단 발동 게이지를 제거하고 현재 스킬 아이콘에 붙는 흰 수축 원으로 교체했다. 상대 공격은 A 회피, 내 기술은 D 압박이다. 큰 원이 기준 원에 수렴하고 기존 성공 구간에서 밝고 두꺼워지며 성공 시 짧은 섬광을 남긴다. 실제 큐 카드 위치·회전·펄스를 따라간다. 입력·피해·ACT 페널티·성공 구간·전투 재생 시계는 유지했다.

- Unity6000.5.9f1 격리 사본 graphics PlayMode 최종 **32/32 통과**, 실패/건너뜀0,5.1786475초. 필터 `StepRingHudPlayModeTests;StepInputPlayModeTests;FixedHudPlayModeTests;MultiHitQueueHudPlayModeTests;CombatTempoPlayModeTests`: 신규 링6/입력10/고정HUD7/연타큐2/템포7개. 수축 반지름90→40, 속이 빈 흰 mesh·어두운 가장자리, 카드 이동·크기 추적, 빈 슬롯·상대 방어 비표시, 첫 타격 뒤 연타 재표시 금지, 성공 표시 수명·다음 슬롯·Reset, 라벨/원 비겹침과 비차단 입력을 확인했다. 기존 실제 키 입력·카메라/HUD·큐·클립/타격 간격 단언도 유지했다. `Logs/StepRingPlayModeResults.xml`/`StepRingPlayMode.log`.
- 최초31/32는 검사 도구의 reflection이 `OnPopulateMesh` overload를 구분하지 못한 실패였다. `VertexHelper` 시그니처를 명시해 검사만 고치고 모든 mesh 단언을 유지했다. 최초 증거는 `StepRingBeforeReflectionResults.xml`/`StepRingBeforeReflection.log`에 보존한다.
- 실제1600×900/1280×720 Game View에서 예고·밝은 성공 구간·양쪽 잔상·성공 섬광·연타 전체 무피해·내 방어 압박 피해 절반·다음 ACT 페널티를 확인했다. `Logs/StepRingVisual.log`의 `STEP RING VISUAL COMPLETE`와 최종 `StepRing-*.png`12장(이전 간격 캡처2장 제외)이 근거다. 최초 화면에서 원이 키 글자를 지나는 것을 확인해 키/상태를 최대 원 바깥에 배치하고 하단 안내는 연출 바 안쪽으로 옮겼다. 변경 전 로그/그림은 `StepRingVisualBeforeLabelSpacing.log`, `StepRing-WindupBeforeLabelSpacing.png`, `StepRing-TimingBeforeLabelSpacing.png`에 보존한다.
- 캡처는 Controller를 정지하고 수동 시계를 적용한 결정적 fixture다. 실제 키 입력은 위 PlayMode 검사가 근거이며 전체 손맛·자연 난이도·밸런스 근거는 아니다. 이전 스텝 EditMode159/159 결과는 아래 기록으로 남기고 이번에 재실행했다고 주장하지 않는다. 이번 Runtime13개 파일은 시작 해시와 모두 같고 생산4/검사2/metadata2 총8파일은 최종 실행 사본과 SHA256이 일치한다. Assets metadata222개: 누락/잘못된GUID/중복GUID0.
- 씬·프리팹·이미지·애니메이션·파티클·패키지·설정·튜닝·레거시 프로젝트·열린 작업용 에디터는 변경하지 않았다. 별도 Inspector 연결 없이 Play 재시작으로 적용한다. 관련 최종 자동 검사에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 SearchDatabase 시작 인덱싱 예외·종료 JobTempAlloc 경고는 재현됐고 화면 확인을 막지 않았다. Console 전체가 깨끗하다는 주장은 아니다.
- 전체 PlayMode·출시 빌드·다른 화면 비율/입력장치·성능·밸런스 검사는 생략했다. 검증 도구는 임시 사본에만 있고 XML/log/PNG는 보존한다.

## 이전 변경: 연타 큐 표시·전투 스텝 (2026-09-27)

검증 상태: **Ready with limitations**. 일반 전투에 발동 예고/A 회피/D 압박/잔상과 다음 턴 ACT 자연 회복 페널티를 추가했다. 확정은 Space/Enter, 느리게 보기는 왼쪽 Shift다. 기존 기초 튜토리얼에는 스텝을 넣지 않고 키 안내만 바꿨다. 최신 사용자 정정에 따라 내 방어 압박은 기존 방어 뒤 받는 피해 절반이며 상대 방어에 공격 2배 특례는 없다. 계약은 `StepPrototype.md`를 따른다.

- 아이콘 소실은 실제 미완료/다음 슬롯 대기 중 HUD 펄스 타이머가 크기를 0으로 만드는 원인이다. 검증 사본에 이전 한 줄을 적용하자 신규 회귀 2개가 모두 예상 실패했다(실제 scale 0, 0.0763893초). `Logs/StepIconBeforeResults.xml`/`StepIconBefore.log`. 생산 파일은 실제 슬롯 소비만 카드를 제거하고 펄스는 크기 1을 유지한다.
- Unity 6000.5.9f1 격리 사본 EditMode **159/159 통과**, 실패/건너뜀 0, 0.0890988초. 신규 스텝 26개와 기존 규칙 검사를 함께 실행했다. `Logs/StepEditModeResults.xml`/`StepEditMode.log`.

- 관련 graphics PlayMode **59/59 통과**, 실패/건너뜀 0, 47.6895079초. 필터: `MultiHitQueueHudPlayModeTests;StepInputPlayModeTests;StepMotionPlayModeTests;CombatTempoPlayModeTests;DuelPrototypePlayModeTests;FixedHudPlayModeTests;LegacyAnimationPlayModeTests;ParticleReusePlayModeTests;PresentationTuningPlayModeTests;BattleResultTutorialFlowPlayModeTests;CampaignFlowPlayModeTests`. `Logs/StepPlayModeResults.xml`/`StepPlayMode.log`. 수정 전 실패한 큐 표시 2개가 모두 통과했으며 원본 동작/재생 간격/파티클 재사용/튜토리얼/결과/캠페인을 함께 확인했다.
- 사거리 우회를 성공한 첫 타격에만 한정하고 후속 연타는 재접촉을 기다린다. UI 피드백은 슬롯 변경 시 지운다. 해당 경계 사례를 보강한 최종 `StepInputPlayModeTests` **10/10 통과**, 실패/건너뜀 0, 0.4501384초. `Logs/StepInputFinalResults.xml`/`StepInputFinal.log`. 총 PlayMode 고유 커버리지는60개이며 보강 후 전체60개를 한 번에 재실행하지는 않았다. 기존 템포 검사 fixture는 신규 예고만 0으로 두고 기존 클립/임팩트/연타 간격을 독립 검증하며, 신규 입력 검사에서 기본 예고/성공 구간을 확인한다.
- Assets metadata220개: 누락/잘못된 GUID/중복GUID 0. 생산10개·검사7개·metadata7개 총24파일은 실행 사본과 SHA256 일치한다. 씬·프리팹·기존 이미지/클립/파티클·패키지·프로젝트 설정·기존 튜닝값·레거시 프로젝트는 변경하지 않았다. 새 예고/타이밍 값은 설정 클래스의 기본값으로 추가되며 별도 Inspector 연결이 필요 없다.
- 실제1600×900/1280×720 Game View에서 첫 예고/성공 구간, 양쪽 이동 잔상, 공격 압박 임팩트, 연타 전체 회피 무피해, 내 방어 압박 피해 절반(3회×기존5 피해→2 피해, HP1000→994), 다음 ACT3(잔여2+베기 보상1, 자연 회복0)를 확인했다. `Logs/StepVisual.log`의 `STEP VISUAL COMPLETE`와 `Step-*.png`12장이 근거다. 첫720 캡처는 검증 도구가 CanvasScaler 크기 변경 뒤 머리 위 HUD를 재투영하지 않아 위치가 어긋났다. 도구만 보완해 다시 캡처했고 초기 증거는 `StepVisualFirst.log`/`Step-720-TimingFirst.png`에 보존했다.
- 캡처는 정지한 Controller에 수동 시계를 적용한 결정적 전투 fixture로 키 입력/전체 손맛/자연 난이도 근거가 아니다. 실제 키 입력은 PlayMode 검사가 근거다. 기존 native Editor 시작의 `UnityEditor.Search.SearchDatabase` 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐다. 관련 실행에서 게임 C# 컴파일/게임 예외/새 셰이더 오류는 관찰하지 않았다. Console 전체가 깨끗하다는 주장은 아니다.
- 실제 화면에서 Space가 기존36폭에 두 줄로 표시되어 힌트를84폭/16pt 한 줄로 보완했다. 최종 큐·고정 HUD 검사 **9/9 통과**, 4.5954602초(`Logs/StepHudFinalResults.xml`/`StepHudFinal.log`). 최초 단독 검사는8/9였으며 첫 활성 렌더 전 실제 raycast를 수행한 fixture 오류였다. 렌더 한 프레임을 기다린 뒤 기존 클릭/크기/배치 단언과 키 문자열의 preferredWidth 검사를 유지해 통과했다. 실패 증거는 `StepHudBeforeRenderFrameResults.xml`/`StepHudBeforeRenderFrame.log`에 보존한다. `StepSpaceHintVisual.log`의 `STEP SPACE HINT VISUAL COMPLETE`와 최신 `Step-Planning-Space.png`에서 한 줄 Space를 확인했다. 이 마지막 변경 뒤 전체60개를 한 번에 재실행하지는 않았다.
- 전체 PlayMode·플레이어 출시 빌드·모든 화면 비율/입력장치·성능/밸런스는 생략했다. 검증용 캡처 도구는 임시 사본에만 있고 로그/XML/PNG는 보존한다.

## 이전 변경: 의미별 키워드 색·전체 로비 페이지 (2026-09-27)

검증 상태: **Ready with limitations**. 키워드는 ACT 회복/후속 강화/피해 감소/고화력/연타/편차/방어/기본 단타에 서로 다른 불투명 염색 종이와 잉크색을 사용한다. 기호·라벨도 함께 남겨 색에만 의존하지 않는다. 공격끼리/방어·빈칸과 대결할 때의 공통 피해 규칙은 개별 설명에서 삭제했다. 홈·스테이지·편성·상점은 고정 상단 메뉴 아래의 전체 페이지로 교체했다. 홈은 방과 세로 준비 메뉴, 스테이지는4×2 카드와 오른쪽 출정 상세, 편성·상점은 확장 콘텐츠다. 새 Unity Scene이 아니라 현재 씬의 UI 페이지이며 결과·전투 설명 팝업은 유지한다. 자세한 계약은 `LobbyAndLoadout.md`와 `SkillInformationDesign.md`를 따른다.

- `LobbyScreenTransition` 한 개가 신규 페이지를0.22초 unscaled fade/32px 좌우 이동으로 표시한다. 상단 메뉴는 이동하지 않고 새 페이지 입력만 일시 차단한다. 교체/숨김/Dispose 시 원래 위치·alpha·입력을 복원한다. 거래/선택 갱신은 이동 연출을 재시작하지 않는다. 편성 초안/선택/상점 분류·스크롤을 보존하며 판정·저장·거래 규칙은 변경하지 않았다.
- Unity6000.5.9f1 격리 복사본 graphics PlayMode 필터 `LobbyScreensPlayModeTests;SkillInfoViewPlayModeTests;SwordSkillRoleHudPlayModeTests;CampaignLobbyHudPlayModeTests;CampaignLoadoutHudPlayModeTests;CampaignShopHudPlayModeTests;DuelVisualThemePlayModeTests;CampaignFlowPlayModeTests;BattleResultTutorialFlowPlayModeTests;CompactHudPlayModeTests;FixedHudPlayModeTests`: 최종 **62/62 통과**, 실패/건너뜀0,12.5710934초. 화면·전환6/공용정보6/역할3/로비7/편성7/상점5/테마4/진행6/결과·연습3/컴팩트8/고정7개. 전체 페이지와 모달 테두리 부재, 헤더 고정, 연속 전환·숨김·파괴·timeScale0, 위치/alpha/입력 복구, 의미별 배지 색·기호 동색·대표7종 대비4.5 이상, 양쪽 전투/편성/상점 공통 문구 부재, 임시 편집·선택·스크롤 보존, 기존 실제 raycast/drag/drop/홀드/저장·거래·진행을 검사했다. `Logs/LobbyScreensPlayModeResults.xml`/`LobbyScreensPlayMode.log`.
- 최초 실행은60/62 통과했다. 이전 테마 검사 두 곳이 삭제된 활성 `Damage Hint`를 찾거나0.22초 전환 중 입력 차단을 오인했다. 공통 설명의 빈 문자열·비활성·노드 삭제를 적극 검사하고 실제 전환 완료/입력 복구 뒤 기존 raycast·drag/drop 단언을 유지하도록 검사 계약을 갱신했다. 최초 증거는 `LobbyScreensPlayModeBeforeFixtureResults.xml`/`LobbyScreensPlayModeBeforeFixture.log`에 보존했다. 이후62/62 통과(12.4547862초) 후 실제 화면에서 스테이지 위력 보너스만 어색하게 줄바꿈되는 것을 확인해 의도적인 두 줄로 정리하고 최종 소스를 재검사했다. 직전 결과는 `LobbyScreensBeforeStatsSpacingResults.xml`/`LobbyScreensBeforeStatsSpacing.log`에 남긴다.
- 실제1600×900/1280×720 Game View에서 홈/스테이지/편성/상점, 후속·회복·피해 감소·편차 배지, 구매/재화 부족/강화 수치, 두 전투 설명을 캡처했다. EventSystem raycast로 상점14 구매→보유 기술14를Q3에 드래그→유효9칸 저장→강화→선택한2단계 출정→로비 복귀를 확인했다. `Logs/LobbyScreensVisual.log`의 `LOBBY SCREENS VISUAL COMPLETE`와 `LobbyScreens-*.png`18장이 근거다. 첫 native 로그는 `LobbyScreensVisualFirst.log`에 보존한다. 컨트롤러 정지·Runtime 승리 호출의 funding fixture이며 자연 승리나 전체 손맛·밸런스 근거는 아니다. 전투 팝업은 표시 API를 직접 호출했고 실제 키 홀드/Tab은 PlayMode 검사가 근거다.
- 생산3개/신규·수정검사4개/신규metadata2개 총9파일은 실행 복사본과 SHA256 일치한다. Runtime12개는 이전 기준 해시와 모두 같으며 Assets metadata213개: 누락0/잘못된GUID0/중복GUID0. 이미지/씬/프리팹/패키지/설정/튜닝/레거시 프로젝트/작업용 에디터는 변경하지 않았다. 임시 캡처 도구는 복사본에만 존재하고 XML/log/PNG는 보존한다. 별도 Inspector 연결 없이 Play 재시작으로 적용한다.
- 첫 sandbox 실행은 Unity 라이선스 IPC 채널 거부로 검사를 시작하지 못했다(`LobbyScreensSandboxAttempt.log`). 확인된 검증 프로세스만 종료한 뒤 허용된 실행 환경에서 완료했다. 중간 프로젝트 잠금 재시도 로그는 `LobbyScreensLockedRetry.log`에 보존했다. 최종 자동 검사에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 시작 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐고 캡처를 막지 않았다. Console 전체가 깨끗하다는 주장은 아니다.
- 전체 PlayMode/출시 플레이어 빌드/모든 비율·장치/성능·밸런스·디스크 저장은 이번 범위에서 생략했다. 실제 화면 확인은16:9의 두 해상도이며 다른 비율까지 새 전체 페이지 레이아웃을 보장하지 않는다. 저장은 현재 Play 세션 안의 적용이다.

## 이전 변경: 키워드·각인 기호를 사용하는 스킬 정보 (2026-09-27)

검증 상태: **Ready with limitations**. 상점·편성 선택 상세와 전투의 키 홀드/적 확인 팝업을 큰 기술 그림/이름→ACT·위력 또는 방어·타격 또는 같은 칸 대응의3타일→핵심 배지2개→짧은 조건·시점→공통 피해 경로 순서로 정리했다. 기존 종이/잉크/황동 테마, 선택·드래그·각열3개 저장·거래·키 홀드/Tab 조작은 유지했다. 긴 문단의 역할을 `SkillInfoView`로 통일하고 `SkillInfoGlyph`로 라벨을 동반하는 작은 각인 기호를 그린다. 상세 의미/범위는 `SkillInformationDesign.md` 참조.

- 3번은 실제3회 공격과 후속3칸 위력+10%를 함께 표시하며 연타의 위력 분할/버림/한 타 최소1도 안내한다. 7번은 같은 슬롯의 상대 타격 조건/다음 턴 ACT+2, 8/9는 이번 턴 뒤 최대10칸의 피해 감소/공격·방어 위력 지원을 표시한다. 적 정보에는 ACT 고유 회복이 플레이어 전용임을 명시한다. 강화 수치는 실제 현재 Skill 복사본에서 읽으며 추가14/17/32에 미이식 유틸리티를 붙이지 않는다. 위력은 총 피해나 확정 피해로 표기하지 않는다.
- Unity6000.5.9f1 격리 복사본 graphics PlayMode 필터 `SkillInfoViewPlayModeTests;CampaignShopHudPlayModeTests;CampaignLoadoutHudPlayModeTests;SwordSkillRoleHudPlayModeTests;DuelVisualThemePlayModeTests;CompactHudPlayModeTests;FixedHudPlayModeTests`: 최종 **40/40 통과**, 실패/건너뜀0,11.5282126초. 신규6/상점5/편성7/역할3/테마4/컴팩트8/고정7개다.15종의 실제ACT·위력·타격/방어 대응, 조건과칸범위·강화·null/빈선택·노드재사용·비차단glyph/CanvasRenderer, 선택만으로 거래/편성변경 없음, 기존 raycast·drag/drop, 키 홀드로 예약하지 않음/해제, 팝업 화면 경계, 원래 거래·저장 계약을 검사했다. `Logs/SkillInfoPlayModeResults.xml`/`SkillInfoPlayMode.log`.
- 최초도40/40 통과(11.6261563초)했으나 정적 교차검토에서 편성 기술 그림과 첫 수치 타일의5px 중첩, 상세판과 보유 목록 안내의2px 영역 중첩을 확인했다. 공용뷰를10px 아래로, 보유 안내를10px 아래로 옮기고 높이를 줄여 간격을 보완한 최종 소스로 재검사했다. 검사 문구는 새 정보 구획에 맞춰 분리해 확인하며 드래그/거래/홀드 입력 단언을 약화하지 않았다. 첫 결과는 `SkillInfoPlayModeBeforeSpacingResults.xml`/`SkillInfoPlayModeBeforeSpacing.log`에 보존했다.
- 실제1600×900과1280×720 Game View에서 EventSystem raycast로 편성의 찌르기/막기/흘리기 선택, 상점 일도양단 선택·강화 카테고리·베기 실제 강화와 숫자 갱신, 출정과 양쪽 전투 설명 표시/닫기를 확인했다. `Logs/SkillInfoVisual.log`의 `SKILL INFO VISUAL COMPLETE`와 `SkillInfo-*.png`13장이 근거다. 실제 캡처를 열어 세 타일·키워드·조건·위력 분할/피해 경로·강화 단계·버튼 간격과 좁은 화면 클리핑을 확인했다. 캡처의 팝업은 수동 ShowExplanation 호출이고 실제 키 홀드/Tab 입력은 위 PlayMode 검사가 근거다. 재화는 승리 호출 funding fixture이며 Controller를 정지해 캡처했으므로 난이도/전체손맛/속도 근거가 아니다.
- 첫 native 흐름은 유지된 강화 카테고리에서 아직 미보유인16번 구매 카드를 찾은 검사 도구 오류로 마지막720상점 단계만 종료됐다. 구매 카테고리 전환을 명시한 임시 도구로 최종 완료했다. 그 수정은 게임 코드가 아니다. `SkillInfoVisualFirst.log`에 처음 기록을 보존했다.
- 최종 생산4개/신규·수정검사4개/신규metadata2개 총10파일은 실행 복사본과 SHA256이 일치했다. Runtime12개는 이전 기준 해시와 모두 같으며 Assets metadata211개: 누락0/잘못된GUID0/중복GUID0. 이미지는 새로 만들거나 변경하지 않았다. 레거시 프로젝트·열린 작업용 에디터·씬·프리팹·패키지·설정·튜닝·판정은 변경하지 않았다. 캡처 도구는 임시 복사본에만 존재한다. 검증 복사본만 정리하고 XML/log/PNG는 보존한다. 별도 Inspector 연결 없이 Play 재시작으로 적용한다.
- 최종 검사에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐고 licensing 초기 handshake 경고 이후 entitlement 갱신과 실행은 성공했다. Console 전체가 깨끗하다는 주장은 아니다. 전체 PlayMode/출시 빌드/모든 비율·장치·성능/디스크 저장·밸런스 검증은 생략했다. 작은 각인 기호는 단독 의미 전달 대신 짧은 라벨과 함께 사용하며 극단적으로 작은 화면의 읽기성까지 보장하지 않는다.

## 이전 변경: 19세기 검술 클럽 공용 비주얼 (2026-09-27)

검증 상태: **Ready with limitations**. 로비·상점·편성·전투·결과·튜토리얼·이전 정비 화면을 호두나무/가죽/황동/장부 종이의 공용 테마로 맞췄다. 가스등이 있는 방, 금속 기술 배지15개, 숲 중경, 기록/확정 아이콘을 새로 제작했다. 캐릭터·클립·VFX·게임 규칙·배경 크기/Y/패럴랙스·튜닝·조작과 화면 레이아웃은 유지했다. 제작 방식/실제 프롬프트/그림 크기/보존 경로는 `Art/VictorianDuelTheme.md` 참조.

- `DuelVisualTheme`는 공용 팔레트와 얇은 황동 프레임을 제공한다. `DuelPanelTrim`은 CanvasRenderer를 갖는 비차단 단일 uGUI mesh이며 장식 중복 생성, 기존 Image의 Sprite/색/치수 변경, 입력 차단을 피한다. 선택 상세는 종이 바탕/잉크 본문으로 바꾸고 재화·가격·부족 이유·비활성 버튼의 가독성도 맞췄다. 기술 Sprite15개의 이름/GUID/SpriteID/internalID와 Resource 경로는 유지하고 새 그림 경계에 맞춰 rect만 갱신했다. 적용 전 PNG5개와 importer5개는 `Art/Sources/Victorian-Before/`에 보존했다.
- Unity6000.5.9f1 격리 복사본의 관련 PlayMode12개 fixture를 실행했다. 2차 전체 결과는 **72/73 통과**, 건너뜀0, 47.4501744초이며 실패1개는 새 테마 raycast 검사가 새 UI의 첫 렌더 프레임보다 먼저 실행된 경우였다. 기존 검사69개는 전부 통과했고 새 테마 검사3개도 통과했다. `Logs/VictorianPlayModeSecondResults.xml`/`VictorianPlayModeSecond.log` 및 동일한 `VictorianPlayModeResults.xml`/`VictorianPlayMode.log`가 근거다. 이 XML 자체를73/73 성공이라고 해석하지 않는다.
- 렌더 프레임을 기다리도록 새 검사만 보완하고 실제 graphics 실행에서 테마4개를 재검사했다. 최종 **4/4 통과**, 실패/건너뜀0, 0.1796599초. 장식 재사용/CanvasRenderer/원본 Image·레이아웃 보존, 실제 여러 화면의 공용 테마, GraphicRaycaster를 통한 버튼·드래그·드롭 입력 경로, 선택 상세의 대비4.5 이상을 확인했다. `Logs/VictorianThemeFinalResults.xml`/`VictorianThemeFinal.log`. 최종 커버리지는 기존69개와 새4개이며 테스트 보완 이후 전체73개를 한 번 더 묶어 실행하지는 않았다.
- 첫 전체 실행의6/73 결과는 공용 장식에 CanvasRenderer를 명시하지 않은 결함을 드러냈다. 생성 시 컴포넌트와 RequireComponent를 추가해 고쳤고 2차 실행으로 확인했다. 첫 테마 단독 재검사의3/4 결과는 실패 진단에서 NUnit이 Unity IndexedSet의 미구현 non-generic enumerator를 사용한 문제였다. 진단을 인덱스 순회로 고쳤으며 실제 raycast 성공 주장은 삭제하거나 약화하지 않았다. 첫/두 번째 전체 및 첫 단독 XML/log를 각각 `VictorianPlayModeFirst*`, `VictorianPlayModeSecond*`, `VictorianThemeFirst*`로 보존했다.
- 실제1600×900 Game View에서 EventSystem raycast로 상점 선택→구매→재화 부족 차단→강화, 편성 클릭 선택만 유지→보유14를Q3에 드래그→저장→해제 후 저장 차단→취소, 스테이지 출정→Q/W/E 예약→확정→파티클이 있는 실제 타격→다음 턴→전투 기록 열기/닫기→승리 결과→로비→연습 시작/예약/적 확인을 진행했다. `Logs/VictorianVisual.log`의 `VICTORIAN THEME VISUAL COMPLETE`와 `Victorian-*.png`16장이 근거다. 방/상점/불완전 편성/전투 기록/결과/튜토리얼 화면을 직접 열어 테두리·설명 대비·아이콘 크롭을 확인했다. 재화와 일반 승리는 funding/작은 적HP fixture이므로 자연 난이도나 밸런스 근거가 아니다. Controller를 정지하고 연출 시간을 수동 전진한 캡처이므로 전체 손맛/속도의 완전한 검증도 아니다.
- 초기 native 캡처 실패2개는 새 상점 생성과 카드 클릭을 같은 callback에 진행한 검사 도구, 정지한 Controller 때문에 기록 창의0.5초 진입 애니메이션이 아직 끝나지 않은 검사 도구 문제였다. 클릭 사이에 실제 Unity 프레임을 기다리고 기록 연출 시간을 전진한 임시 도구로 최종 흐름을 완료했다. 첫/두 번째 native 로그는 `VictorianVisualFirst.log`/`VictorianVisualSecond.log`에 보존했다. 이 구간에서 게임 코드를 변경하지 않았다.
- 변경 자산/소스/검사/metadata19개는 최종 실행 복사본과 SHA256이 일치한다. Runtime12개는 시작 시 해시와 모두 같고 Assets metadata209개: 누락0/잘못된GUID0/중복GUID0. 캡처 도구는 임시 프로젝트에만 존재하며 source Editor에는 추가하지 않았다. 레거시 프로젝트·열린 작업용 에디터·씬·프리팹·패키지·프로젝트 설정은 변경하지 않았다. 검증 복사본만 정리하고 그림 원본 백업·XML/log/PNG는 보존한다.
- 최종 관련 실행에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱싱 예외와 종료 JobTempAlloc 경고는 재현됐다. 초기 licensing handshake 경고 뒤 entitlement/access token 갱신과 실행은 성공했다. Console 전체가 깨끗하다는 주장은 아니다. 전체 PlayMode/출시 빌드/모든 해상도·입력장치/성능·밸런스 검증은 생략했다. 생성 그림은 고정 도트 격자나 정확한 제한색 수를 보장하지 않는다. Play를 재시작하면 별도 수동 연결 없이 새 테마가 적용된다.

## 이전 변경: 드래그 전용 편성·선택형 상점 (2026-09-27)

검증 상태: **Ready with limitations**. 편성의 카드 클릭은 설명 선택만 수행하고, 순서 변경과 보유 기술 교체는 드래그로만 요청한다. 상점은 구매/강화 목록과 선택 상세·단일 거래 버튼으로 개편했다. 각 열 정확히3개 저장 계약과 기존 가격·강화 수치·전투 판정은 유지했다.

- `CampaignLoadoutHud.ClickSlot`/`SelectOwned`는 선택 상태만 갱신한다. 카드·보유 기술·열 필터를 여러 번 클릭해도 초안9칸/저장9칸/변경 여부가 그대로다. 같은 열의 드래그로만 교환·교체하며 해제/저장/취소는 명시적 버튼으로 수행한다. 빈칸이 있는 편성은 여전히 저장하지 못한다.
- 새 `CampaignShopHud`는 구매/보유 기술 강화 탭, 간결한2열 목록, 선택한 기술1개의 목적·실제 효과·ACT·위력·타격 수·가격·강화 전후 위력과 거래 버튼을 표시한다. 목록 클릭은 재화를 쓰지 않는다. 획득 완료/재화 부족/최대3단계에서는 거래를 차단하고 이유를 보여준다. 구매는 자동 편성하지 않으며 거래·탭 이동·뷰 재생성 후에도 카테고리별 선택과 스크롤을 보존한다. 로비가 상태와 Dispose 수명을 소유하고 공통 실제 효과 문구는 `CampaignSkillText`로 분리했다. Runtime 소스는 변경하지 않았다.
- Unity6000.5.9f1 격리 복사본 EditMode **133/133 통과**, 실패/건너뜀0, 0.1341381초. 기존 초안/원자적 저장·캠페인·전투 계약 회귀 검사다. `Logs/ShopEditModeResults.xml` / `ShopEditMode.log`.
- 최종 PlayMode 필터 `CampaignShopHudPlayModeTests;CampaignLoadoutHudPlayModeTests;CampaignLobbyHudPlayModeTests;CampaignFlowPlayModeTests;SwordSkillRoleHudPlayModeTests;BattleResultTutorialFlowPlayModeTests`: **31/31 통과**, 실패/건너뜀0, 0.8923018초. 상점5/편성7/로비7/진행6/역할3/결과·연습3개가 실제 포함됐다. 순수 선택·드래그 교환/교체·빈칸 저장 차단·중복 거래 방지·강화 실제 수치·최대 단계·부족 재화·전투 중 거래 차단·선택/스크롤 보존·Dispose·구매→편성→출정을 검사했다. `Logs/ShopPlayModeResults.xml` / `ShopPlayMode.log`.
- 실제1600×900 Game View의 EventSystem raycast로 목록 선택→구매→강화 부족 표시→실제1~3단계 강화→최대 단계 차단을 확인했다. 편성 슬롯/보유 기술을 클릭해도 배치가 바뀌지 않으며 보유14를Q3에 드래그한 뒤 유효9칸을 저장했다. 상점으로 돌아가도 강화 카테고리/선택1/최대 단계 표시가 유지됐다. 재화는 Runtime 승리 호출을 사용한 funding fixture이므로 자연 난이도 근거는 아니다. `ShopVisual.log`의 `SHOP CLICK DRAG VISUAL COMPLETE`와 `Shop-Purchase.png`, `Shop-Purchased.png`, `Shop-Insufficient.png`, `Shop-Upgraded.png`, `Shop-Maximum.png`, `Loadout-SelectionOnly.png`, `Loadout-DragOnly.png`가 근거다. 첫 캡처 후 비활성 거래 버튼의 글자 대비를 보완하고 최종 소스로 다시 실행했다. 첫 실행 로그는 `ShopVisualFirst.log`에 남긴다.
- 최종 변경 소스/검사9개와 신규metadata3개는 실행 복사본과 SHA256이 일치한다. Assets metadata207개: 누락0/잘못된GUID0/중복GUID0. 캡처 도구는 임시 프로젝트에만 존재한다. XML/log/PNG는 보존하고 검증용 복사본만 정리한다. 레거시 프로젝트·열린 작업용 에디터·씬·프리팹·그림·클립·VFX·튜닝·패키지·프로젝트 설정은 변경하지 않았으며 별도 수동 연결은 없다.
- 관련 실행에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱싱 예외 및 종료 JobTempAlloc 경고는 재현됐다. 초기 licensing handshake 경고 이후 entitlement/access token 갱신과 검사는 성공했다. Console 전체가 깨끗하다는 주장은 아니다. 전체 PlayMode/출시 빌드/모든 해상도·입력장치/성능·밸런스 검증은 생략했다. 편성 저장은 현재 Play 세션 안의 적용이며 디스크 저장은 아니다.

## 이전 변경: 간결한 편성 UI·각 열 3개 저장 계약 (2026-09-27)

검증 상태: **Ready with limitations**. 기존 열별1~3개 즉시 적용 대신 Q/W/E 각각 정확히3개를 저장하는 임시 편집을 구현했다. 전투 판정·수치·아이콘·독립된 튜토리얼2/2/3 기술열은 변경하지 않았다. 편성 저장은 현재 Play 세션 안의 적용이며 디스크 저장은 아니다.

- `CampaignLoadoutHud`: 순번/이름/아이콘/ACT만 있는 고정9슬롯, 선택 기술의 짧은 상세1개, 선택한 열의 보유 목록. ▲▼ 대신 같은 열 드래그 교환 또는 기술 선택→슬롯 클릭을 사용한다. 보유 기술은 목적 슬롯을 교체한다. 반복 안내와 카드별 역할/상세 수치를 제거했다.
- `CampaignRun`: 저장 편성과 nullable 고정3×3 초안을 따로 소유한다. 해제해도 저장 편성은 유지한다. 소유권·고정열·중복·각3개를 전부 검사한 뒤에만 원자적으로 저장한다. 부족한 열/개수를 표시하고 저장을 비활성화한다. 취소는 마지막 저장 상태 복구, 탭 이동은 초안 보존, 미저장 변경은 출정 차단이다. 전투는 저장된9개만 복사한다.
- Unity6000.5.9f1 격리 복사본 EditMode **133/133 통과**, 실패/건너뜀0, 0.1112703초. 기존116개와 신규 초안/저장17케이스다. 빈 초안/열 교환/교체/소유권·열·인덱스/중복 방지/저장 실패 원자성/취소/편집 차단 상태/전투 스냅샷을 검사했다. `Logs/LoadoutEditModeResults.xml` / `LoadoutEditMode.log`.
- 최종 PlayMode 필터 `CampaignLoadoutHudPlayModeTests;CampaignLobbyHudPlayModeTests;CampaignFlowPlayModeTests;SwordSkillRoleHudPlayModeTests;BattleResultTutorialFlowPlayModeTests`: **24/24 통과**, 실패/건너뜀0, 0.6924882초. 신규 편성UI5개와 기존 로비7/진행6/역할3/결과·연습3개다. 9슬롯/상세/필터/선택 보존/저장 활성화/부족 표시/취소/빈칸·다른열 드래그 거절/잔상 정리 및 구매→편성 저장→출정을 확인했다. `Logs/LoadoutPlayModeResults.xml` / `LoadoutPlayMode.log`.
- 최초 PlayMode19/19는 신규 검사 파일의 metadata GUID 오타(33자리) 때문에 기존 검사만 실행됐다. 32자리 고유GUID로 바로잡고 누락된5개가 실제 포함된24/24를 확인했다. 최초 XML/log는 `LoadoutPlayModeFirstResults.xml` / `LoadoutPlayModeFirst.log`에 보존했다. 최종 Assets metadata204개: 누락0/잘못된GUID0/중복GUID0. 변경 소스/검사13개와 신규metadata3개는 실행 복사본과 SHA256이 일치한다.
- 실제1600×900 Game View에서 EventSystem raycast로 상점 구매→편성→Q1/Q3드래그 교환→해제→저장 거절→탭 이동 후 초안 보존→보유14선택/빈슬롯클릭→저장→다른열 드래그 거절→W열 교환→취소→출정을 진행했다. 출정의 실제 기술열은 저장한7/2/14였다. 재화는 Runtime 승리 호출을 사용한 funding fixture이므로 자연 난이도 근거가 아니다. `LoadoutVisual.log`의 `LOADOUT VISUAL COMPLETE`와 `Loadout-Clean.png`, `Loadout-Incomplete.png`, `Loadout-ReadyToSave.png`, `Loadout-Saved.png`가 근거다. 카드/상세/보유/푸터가 겹치지 않고 부족 이유·저장 상태가 읽히는 것을 캡처에서 확인했다.
- 캡처 도구는 임시 프로젝트에만 존재한다. 검증용 복사본은 정리하고 XML/log/PNG는 보존한다. 레거시 프로젝트·작업용 에디터·씬·프리팹·그림·클립·VFX·튜닝·패키지·프로젝트 설정은 변경하지 않았다. 별도 수동 연결은 없다. 최종 관련 실행에서 게임 C# 컴파일 오류/게임 예외/새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱싱 예외 및 종료 JobTempAlloc 경고는 재현됐다. 초기 licensing handshake 경고 이후 entitlement/access token 갱신과 실행은 성공했다. Console 전체가 깨끗하다는 주장은 아니다. 전체 PlayMode/출시 빌드/모든 해상도·입력장치/성능·밸런스 검증은 생략했다.

## 이전 변경: 전투 결과 창·직접 조작하는 연습 전투 (2026-09-27)

검증 상태: **Ready with limitations**. 전투 마지막 타격/동작/마무리 뒤 전장 위 결과 창을 표시하고 로비/재도전/개방된 다음 스테이지를 직접 선택한다. 보상은 기존 Runtime에서 한 번만 지급한다. 전용 튜토리얼은 여정과 독립된 전투로 예약·회전·방어·적 확인·확정·실제 교전·ACT 회복을 차례로 연습하고 자유 전투로 끝낸다. 기존 전투 판정·수치·연출과 일반 8스테이지 진행은 유지했다.

- `BattleResult`는 승패·단계·턴·남은 HP·획득/보유 재화·첫 클리어·새 해금·다음 단계 가능 여부의 불변 표시 스냅샷이다. `FinishStage`가 기존 `CampaignRun.TryCompleteBattle`을 한 번 호출한 후 이를 생성한다. 결과 표시/재표시/닫기는 보상을 지급하지 않는다. 결과 중 거래/편성/큐/일반 출정은 차단되고 Enter/Escape는 로비로 돌아간다. 패배는 다음 단계 버튼이 없고 현재 단계 재도전은 구매·강화·편성을 보존한다. 마지막 단계와 재클리어 규칙도 유지한다.
- 홈/스테이지 탭의 기초 연습 버튼은 `TutorialStage`의 전용 전투를 연다. 기존 기술/숲/캐릭터/아이콘/클립/VFX를 재사용하고 연습 전체에서 제한시간/자동 확정을 끈다. 처음 Q→W→Q는 베기/찌르기/막기를 실제로 예약하고, Tab 또는 마우스 확인 버튼으로 적 큐를 본 뒤 확정한다. 셋째 상대 내려치기는 실제 타격 속성/2타로 막기의 회복 조건을 충족한다. 실제 첫 턴 뒤 ACT6을 확인하고 자유 전투로 진행한다. 잘못된 예약/확정은 단계에 맞춰 거절되며 비활성 카드/확정은 .4 alpha로 표시한다. 계속 버튼으로 실제 조작 단계를 건너뛰지 않는다.
- 연습은 현재 캠페인의 재화/편성/소유/강화/단계/해금/클리어를 변경하지 않는다. 종료와 Escape는 무보상 로비 복귀이며 결과의 다시 연습은 안내와 전투를 모두 초기화한다. 새 여정도 결과/안내/검사 시간/입력 홀드 상태를 지운다. 디스크 저장이나 첫 실행 강제 튜토리얼을 추가하지 않았다. 사용법/소유 관계는 `BattleResultsAndTutorial.md` 참조.
- Unity6000.5.9f1 임시 복사본 / EditMode **116/116 통과**, 실패/건너뜀0, 0.1428162초. 기존88개와 신규 튜토리얼16개/결과12개다. 초기 지정 큐 비용3, 첫 턴 생존과 저항 피해, 다음 ACT6, 자유 예약으로 승리, 연습과 여정의 독립성, 결과 스냅샷 정합성을 확인했다. `Logs/ResultTutorialEditModeResults.xml`/`ResultTutorialEditMode.log`.
- 최종 PlayMode 필터 `BattleResultTutorialFlowPlayModeTests;ResultTutorialHudPlayModeTests;CampaignFlowPlayModeTests;CampaignLobbyHudPlayModeTests;CompactHudPlayModeTests;CombatTempoPlayModeTests`: **37/37 통과**, 실패/건너뜀0, 7.9770706초. 신규 실제 결과/연습 진행3개·결과/코치/HUD6개와 기존 진행6개·로비7개·컴팩트 HUD8개·템포7개다. 결과 보상 일회성과 입력 차단·상위 진입·패배 재도전, 실제 연습 첫 턴/회복/자유 승리/재연습/중단·여정 초기화, UI 재표시 중 계층 중복 없음, 버튼 활성화/밝기 복원, 안내 배치 경계를 검사했다. `Logs/ResultTutorialPlayModeResults.xml`/`ResultTutorialPlayMode.log`.
- 최초 PlayMode는35/37이었다. 실패2개는 검사에서 `InputTestFixture.Press` 직후 같은 iterator 프레임의 수동 호출로 `wasPressedThisFrame`을 확인한 Enter/Escape 구간이었다. 기존 입력 검사처럼 실제 Controller.Update가 실행되는 Unity 프레임을 기다리도록 검사만 수정했다. 최종 검사는 Enter 결과 닫기/held Enter 다음 프레임 재출정 방지/Escape 연습 중단까지 통과했다. 최초 결과/로그는 `ResultTutorialPlayModeFirstResults.xml`/`ResultTutorialPlayModeFirst.log`에 보존했다.
- 그래픽 Editor의 실제1600×900 Game View에서 결과/연습을 확인했다. 실제 EventSystem raycast로 승리 결과의 다음 단계→패배 결과의 재도전, 홈 연습→시작→Q/W/Q 카드→적 확인→확정→실제 첫 교전→ACT6 확인→자유 연타→실제 연습 승리→다시 연습→종료를 진행했다. 재화60/현재2단계/첫 클리어1/개방2는 연습 전후 그대로였다. 일반 승패 경로는 작은 적/작은 플레이어 HP fixture이며 자연 난이도 근거가 아니다.
- 처음 상단 중앙 코치가 가까워진 캐릭터의 머리 위 큐/상태바를 덮는 것을 화면에서 발견했다. 도크 위 빈 지면으로 이동하고900×200 기준으로 줄여 본문을 유지했으며, 최종 캡처에서 양쪽 큐/상태바·ACT·QWE·확정이 보이는 것을 확인했다. 최종 `Logs/ResultTutorialVisualFinal.log`의 `RESULT TUTORIAL VISUAL COMPLETE`와 `ResultTutorial-Lobby.png`, `ResultTutorial-Victory.png`, `ResultTutorial-Defeat.png`, `ResultTutorial-Welcome.png`, `ResultTutorial-QueueFocus.png`, `ResultTutorial-Inspect.png`, `ResultTutorial-Commit.png`, `ResultTutorial-Clash.png`, `ResultTutorial-Recovery.png`, `ResultTutorial-FreeBattle.png`, `ResultTutorial-Complete.png`, `ResultTutorial-LobbyAfterPractice.png`가 근거다. 초기 배치 기록은 `ResultTutorialVisual.log`에 남긴다. 캡처에서는 실제 전투를 수동 시계로 진행한 뒤 해당 프레임의 Controller만 정지했으므로 전체 손맛/카메라 집중/모든 비율 보장은 아니다.
- Assets metadata201개, 누락0/중복GUID0. 최종 변경 소스/검사13개는 실행 복사본과 SHA256이 일치한다. 검증용 캡처 도구는 임시 프로젝트에만 존재한다. 레거시 프로젝트·열린 작업용 에디터·씬·프리팹·기존 그림·클립·파티클·튜닝·패키지·프로젝트 설정은 변경하지 않았다. 검증용 임시 복사본만 정리하며 결과 XML·로그·PNG는 보존한다.
- 관련 실행에서 게임 C# 컴파일 오류·게임 예외·새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱싱 예외와 종료 JobTempAlloc 경고는 재현되어 Console 전체가 깨끗하다는 주장은 아니다. 전체 PlayMode·출시 빌드·모든 화면 비율/입력 장치·성능·자연 캠페인 난이도는 미검증이다. 별도 Inspector/씬 수동 연결은 없다.

## 이전 변경: 검 전용 15아이콘·실제 효과에 맞춘 세부 역할 (2026-09-27)

검증 상태: **Ready with limitations**. 기본9개와 획득6개에 검만 사용하는 개별 아이콘을 적용하고 ACT 회복·후속 위력·피해 감소·고화력·연타·변동 위력을 구분했다. 전투 수치·큐·피해 판정·애니메이션·캠페인 규칙은 변경하지 않았다. 신규 저항 특화 효과나 디스크 저장·출시 빌드는 이번 범위가 아니다.

- `LegacySkillRoles`는 실제 구현된 Skill.Id/종류/타격 횟수만 사용하는 읽기 전용 표시 메타데이터다. 현재 모든 공격은 상대 공격과 대결하는 슬롯에서 저항 피해를 주므로 특정 공격을 저항 파괴 전용으로 표시하지 않는다. 상점/편성의 공통 안내와 전투 요청형 설명에서 조건을 알려 준다. 미이식 고유 효과가 없는 획득14/17/32에는 ACT 회복/피해 감소 문양을 붙이지 않았으며 16은 확정 극딜이 아닌 변동 단타다. `SkillRoleLanguage.md` 참조.
- 이미지 생성으로 이전 아틀라스를 편집하여15개 검 문양을 만들었다. 원본 alpha 픽셀은 그대로 사용하고 개별 Sprite rect만 실제 문양 경계에 맞췄다. 균등 격자에서 다음 줄 테두리가 섞이는 문제를 발견하여15개 alpha 영역의 경계+6px 여백으로 수정했다. 원본 Texture GUID와 Sprite1~9 ID, Point/무압축/중앙pivot를 유지했다. 추가6개는 별도 IconId10~15로 현재/다음/큐/기록/로비에서 일관되게 사용한다. 제작 프롬프트·원본 경로·이전 그림 보존 기록은 `Art/SwordSkillIcons.md` 참조.
- Unity6000.5.9f1 임시 복사본에서 EditMode **88/88 통과**, 실패/건너뜀0, 0.1016668초. 기존62개와 신규 역할26개다. `Logs/SwordRolesEditModeResults.xml`/`SwordRolesEditMode.log`.
- PlayMode 필터 `SwordSkillRoleHudPlayModeTests;CompactHudPlayModeTests;CampaignFlowPlayModeTests;CampaignLobbyHudPlayModeTests`: **24/24 통과**, 실패/건너뜀0, 7.750434초. 아이콘3개·HUD8개·진행6개·로비7개다. 획득 기술의 실제 Sprite 공유와 다음 카드 회전, 효과 조건·강화 후 정체성·툴팁을 확인했다. `Logs/SwordRolesPlayModeResults.xml`/`SwordRolesPlayMode.log`. 첫 실행22/24의 두 실패는 검사에서 큐 회전 뒤의 현재 아이콘과 이전 선택 아이콘을 비교한 점과 표시 공백의 차이였다. 선택 당시 Sprite를 보존하고 공백을 정규화하여 검사만 수정했으며 첫 결과는 `SwordRolesPlayModeFirstResults.xml`에 보존했다.
- 마지막 Sprite rect 보정 후 `SwordSkillRoleHudPlayModeTests;CompactHudPlayModeTests`를 다시 실행하여 **11/11 통과**, 실패/건너뜀0, 7.2615056초. 다양한 실제 Sprite 비율·중앙pivot·15개 임포트와 HUD 공유 계약을 확인했다. 앞선24개의 부분집합이며 별도11개 신규 검사라는 의미가 아니다. `Logs/SwordRolesFinalTargetedResults.xml`/`SwordRolesFinalTargeted.log`.
- 그래픽 Editor의 실제1600×900 Game View에서 작은 버튼/역할 라벨/요청형 설명을 확인했다. `Logs/SwordRoles-Loadout.png`, `SwordRoles-Shop.png`, `SwordRoles-Equipped.png`, `SwordRoles-Battle.png`, `SwordRoles-ACT-Detail.png`, `SwordRoles-Support-Detail.png`, `SwordRoles-Conditional-ACT.png`, `SwordRoles-Plain-Detail.png`. 최종 `SwordRolesVisualFinal.log`의 `SWORD ROLE VISUAL COMPLETE`가 완료 근거다. 실제 EventSystem raycast로 구매→해제→새 기술 장착→순서 변경→2스테이지 출정을 확인했다. 작은 적 fixture의 실제 예약→타격→마무리→보상 연결을 사용했으며 자연 난이도·손맛 검증은 아니다.
- Assets metadata191개, 누락0/중복GUID0. 최종 변경 소스/검사/PNG/meta13개는 검증 복사본과 SHA256이 일치한다. 캡처 도구는 임시 복사본에만 존재한다. 레거시 프로젝트·열린 사용자 에디터·씬·프리팹·튜닝·패키지·프로젝트 설정은 변경하지 않았다. 검증용 임시 복사본만 정리하고 XML·로그·화면 캡처는 보존한다.
- 관련 실행에서 게임 C# 컴파일 오류·게임 예외·새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱싱 예외와 종료 JobTempAlloc 경고는 다시 관찰되어 Console 전체가 깨끗하다는 주장은 아니다. 전체 PlayMode·모든 화면 비율/입력 장치·성능·출시 빌드는 검사하지 않았다.

## 이전 변경: 방 로비·선택형 스테이지·편성·역할 아이콘 (2026-09-27)

검증 상태: **Ready with limitations**. Play 시작부터 방 로비에 진입하고 상점/편성/스테이지 선택을 오간다. 기존 전투와 연출은 유지했다. 세션 저장만 지원하며 디스크 저장·출시 빌드·8단계 자연 난이도 검증은 이번 범위가 아니다.

- 레거시 `OutGame`의 방 PNG를 바이트 그대로 복사하고 Point/무압축 설정으로 사용한다. 홈은 방이 보이는 좁은 준비 패널, 나머지 탭은 배경 위의 중립색 패널이다. 새 씬이나 패키지는 추가하지 않았다. `LobbyAndLoadout.md`/`LobbyRoomAssets.md` 참조.
- 모델이 소유와 편성을 구분한다. 구매는 자동 편성하지 않으며 Q/W/E 각각1~3개에서 해제·장착·순서 변경을 한다. 전투 스냅샷은 편성 목록만 전달하고 전투 중 거래/편성은 거절한다. 해금한 단계를 직접 선택해 출정하며 최초 클리어 보상과 다음 단계 개방, 재클리어 절반 보상, 패배/포기 무보상을 확인했다.
- Unity6000.5.9f1 임시 복사본에서 EditMode **62/62 통과**, 실패/건너뜀0, 0.0698011초. 기존 전투/기반35개 + 기존 캠페인 수정14개 + 신규 로비 모델13개. `Logs/LobbyEditModeResults.xml`/`LobbyEditMode.log`.
- PlayMode **41/41 통과**, 실패/건너뜀0, 43.4113057초. 로비 화면7개/실제 진행6개/이전 정비 호환5개/컴팩트 HUD8개/한 판8개/템포7개. `Logs/LobbyPlayModeResults.xml`/`LobbyPlayMode.log`. 해당 실행의 존재하지 않는 파티클 클래스 필터는 검사를 추가하지 않았으며 파티클3개는 아래 최종 실행에서 정확한 클래스명으로 검사했다.
- 최종 초기화 보완 후 `CampaignLobbyHudPlayModeTests;CampaignFlowPlayModeTests;ParticleReusePlayModeTests`를 다시 실행하여 **16/16 통과**, 실패/건너뜀0, 0.5172165초. `Logs/LobbyFinalTargetedResults.xml`/`LobbyFinalTargeted.log`. 전투 도중 새 여정에서 보상/실패 배너·선택 단계·스크롤 초기화가 이전 상태를 보존하지 않도록 명시적인 `ResetView`를 사용한다.
- 그래픽 Editor의 실제1600×900 Game View에서 홈/스테이지/상점/편성/편성 변경 후/선택한2스테이지/2스테이지 전투를 캡처하고 육안 확인했다. 실제 EventSystem raycast로 구매→해제→스크롤 아래 새 기술 편성→위로 두 번 이동→2스테이지 선택→도전을 눌렀다. 공격 이름/역할 테두리, 강화된 편성 기술의 전투 스냅샷, 무보상 포기, 1단계 재도전30보상, 전투 도중 여정 초기화도 확인했다. `Logs/Lobby-Home.png`, `Lobby-Stages.png`, `Lobby-Shop.png`, `Lobby-Loadout.png`, `Lobby-Equipped.png`, `Lobby-SelectedStage2.png`, `Lobby-Stage2Battle.png`; `LobbyVisual.log`의 `LOBBY VISUAL COMPLETE`가 완료 근거다.
- 화면 검사에서는 작은 적 fixture로 실제 예약→타격→마무리→로비 경로를 진행했다. 모델 승리 강제 호출로 이 연결 검사를 대체하지 않았다. 이 fixture는 진행/거래 확인용이며 자연 플레이의 난이도·손맛 증거가 아니다. AI 생성9아이콘은 역할별 색뿐 아니라 마름모/육각형/홈 사각형/방패 실루엣을 사용하고 전투 현재 카드에 이름도 보인다. 아틀라스의 실제9 Sprite 임포트·같은 IconId의 현재/다음/큐/기록 공유도 검사했다. 생성 프롬프트와 한계는 `Art/RoleSkillIcons.md` 참조.
- Assets metadata188개, 누락0/중복GUID0. 핵심 소스/검사/방/아이콘14개와 실행 복사본의 SHA256이 일치한다. 검증 도구는 임시 복사본에만 존재한다. 레거시 프로젝트·열린 사용자 에디터·기존 씬/프리팹/배경/튜닝/패키지/프로젝트 설정은 변경하지 않았다. 검증 종료 후 임시 복사본만 정리하며 결과 XML·로그·PNG는 보존한다.
- 검사에서 게임 C# 컴파일 오류·게임 예외·새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱싱 예외와 종료 JobTempAlloc 경고는 재현되어 Console 전체가 깨끗하다는 주장은 아니다. 현재 로비는1920×1080 기준 Canvas/1600×900 실제 화면에서 확인했다. 모든 화면 비율/입력 장치, 전체 PlayMode, 성능, 출시 빌드는 검사하지 않았다.

## 이전 변경: 정비·성장과 8스테이지 루프 (2026-09-27)

검증 상태: **Ready with limitations**. 기존 큐 전투 위에 승리 보상→정비 거래→다음 스테이지와 패배 재도전·최종 완료를 연결했다. 저장·전체 난이도·출시 빌드 검증은 범위 밖이다.

- `CampaignRun`은 Unity 비의존 상태로 8스테이지·재화·소유/강화 기술을 관리한다. 전투 규칙은 `LegacyQueuedDuel`에 유지하며 마지막 슬롯/애니메이션/마무리 완료 후에만 보상을 한 번 지급한다. 스테이지 입장은 완전 회복한 별도 전투 스냅샷으로 이루어지고, 다음 단계/재도전은 구매 상태를 보존한다.
- 첫 기본 기술 9개·기존 숲·캐릭터·애니메이션·폰트·효과음·버튼 그림을 유지한다. 추가 획득 6개는 레거시 수치와 기본 동작만 이식하며 미이식 고유 효과를 표시하지 않는다. `IconId`를 분리하여 신규 기술 ID도 선택·다음·큐·전투 기록에서 기존 버튼 그림을 재사용한다. 단계/가격/미이식 범위는 `CampaignLoop.md`에 기록했다.
- Unity6000.5.9f1 / Assets·Packages·ProjectSettings 임시 복사본 / EditMode 전체 **49/49 통과**, 실패·건너뜀0, 0.0596363초. 신규 진행 규칙14개 + 기존 전투·초기 기반35개. `Logs/CampaignEditModeResults.xml`/`CampaignEditMode.log`.
- PlayMode 최종 필터 `CampaignFlowPlayModeTests;CampaignMaintenanceHudPlayModeTests;CombatTempoPlayModeTests;DuelPrototypePlayModeTests;CompactHudPlayModeTests;ParticleReusePlayModeTests`: 36/36 통과, 실패·건너뜀0, 42.9935935초. 새 실제 진행5개/정비 화면5개/템포7개/한 판8개/컴팩트 HUD8개/파티클3개. `Logs/CampaignPlayModeResults.xml`/`CampaignPlayMode.log`.
- 진행 검사에서는 작은 적/낮은 체력의 테스트 전투로 실제 큐 확정→타격→슬롯 완료→마무리→정비 경로를 실행했다. 일회성 지급, 전투 입력 차단, 획득→다음 단계 합류, 강화 위력, 실패→같은 단계 회복 재도전, 8단계→새 여정 초기화와 실제 InputTestFixture Enter 진행을 확인했다. 임의 모델 승리만으로 UI를 열어 실제 연결 검사를 대체하지 않았다. 작은 적은 검사 범위를 루프에 집중하는 fixture이며 자연 난이도/플레이 숙련도 증거는 아니다.
- 첫 PlayMode는35/36 통과였다. 기존 템포 검사가 새 여정 초기화 후에도 주입한 HP1000 fixture를 기대했다. 새 초기화 정책인 1스테이지 HP100/적80·재화0을 검사하도록 기대값을 바꾸고, 템포 전용 주입에는 전투 표현 초기화만 사용했다. 보상·피해 코드를 검사 통과를 위해 바꾸거나 검사를 제거하지 않았다. 첫 실패는 `CampaignPlayModeFirstResults.xml`/`CampaignPlayModeFirst.log`에 보존했다.
- 별도 그래픽 Editor의 실제1600×900 Game View에서 정비·획득 후 재화·강화·다음 단계·재도전·8단계 완료 화면을 육안 확인했다. 실제 EventSystem raycast로 획득, 스크롤 아래쪽 강화, 위쪽 강화, 다음 전투·재도전·새 여정 버튼을 눌렀다. 작은 적 fixture로 실제 전투 마무리를 거쳤고, 3스테이지 전투 스냅샷의 강화 위력/구매 기술과 재도전 보존까지 확인했다. 캡처 `Logs/Campaign-Stage1.png`, `Campaign-Maintenance.png`, `Campaign-Acquired.png`, `Campaign-Stage2.png`, `Campaign-Upgraded.png`, `Campaign-Stage3.png`, `Campaign-Retry.png`, `Campaign-Complete.png`; 기록 `CampaignVisual.log`. 첫 화면 도구는 새 Canvas 생성/스크롤 직후 아직 표시 프레임을 거치지 않은 버튼을 눌러 raycast가 막혔다. 도구만 표시 프레임 이후 누르도록 바꾼 최종 실행이 완료됐으며 생산 UI는 변경하지 않았다. 첫 시도는 `CampaignVisualFirst.log`에 보존한다.
- Assets metadata181개, 누락0/중복GUID0. 핵심 변경 파일과 실행 복사본 10개는 SHA256이 일치한다. 씬·프리팹·패키지·기존 리소스/튜닝·레거시 프로젝트·열린 사용자 에디터는 변경하지 않았다. 검증 프로세스 종료 확인 뒤 임시 복사본만 삭제하며 XML·로그·캡처는 보존했다.
- 최종 검사에서 게임 C# 컴파일 오류·게임 예외·새 셰이더 오류는 관찰하지 않았다. native Editor에는 기존 `UnityEditor.Search.SearchDatabase` 인덱싱 예외와 종료 JobTempAlloc 경고가 남아 있어 Console 전체가 깨끗하다는 주장은 아니다. 진행은 Play 세션에만 남는다. 신규 스킬 고유 효과·덱 편집·진행 저장·8단계 전체 밸런스·모든 비율/입력 장치·전체 PlayMode·성능·출시 빌드는 미검증/미구현이다. 권장 화면은16:9이며 정비 화면은 고정1440×900 기준 배치다.

## 이전 변경: 파티클 반복 재사용 수정·원경 두 층 분리 (2026-09-27)

검증 상태: **Ready with limitations**. 사라지는 불꽃을 재현하고 원인 수정 전후를 비교했으며, 원경 분리와 관련 검사·실제 화면을 확인했다. 전체 검사·출시 빌드·성능 측정은 범위 밖이다.

- 버그 기준 재현: 새 전장에서 초기화 없이80회 `PresentHit(true,1,0,false,false,1) → Tick(.1,.1) → Tick(1,1)` 실행. 수정 전 첫24회만 방출되고25~80회는0입자였으며, 종료된 이펙트가 비활성화되지 않고24개/하위 시스템48개로 차 있었다. PlayMode **0/1 통과**(의도한 실패), `Logs/ParticleReuseBaselineResults.xml`/`ParticleReuseBaseline.log`에 보존했다. 뒤 전투에서 새 방출을 막는 최대24개 분기에 도달하는 증상과 일치한다.
- 원인: 수동 `ParticleSystem.Simulate` 뒤의 `IsAlive(true)`로 반환을 판단하여 빈 일회성 이펙트도 계속 점유되었다. 수정은 생성 때 하위 시스템 배열과 최대 방출 종료 시점0.55초를 캐시하고, 수동 전투 시계가 종료 시점을 지난 뒤 모든 하위 입자가0이면 정지·반환하도록 한 것뿐이다. 풀 제한24개와 원본 prefab/재질/색·발광·슬로모션/히트 스톱 시계는 유지했다. 한계: 이 정책은 현재의 비반복 시스템2개/단일 burst/하위 emitter 없음에 맞춘다.
- 같은80회 검사에서 수정 후 **80회 모두 방출**, 반환 누락0, 재사용 파티클 시스템2개(불꽃+링 한 세트)였다. 추가로24개 풀 포화 뒤 회복, 실제 시간만 흐를 때 미반환, 재대결 정리, ±100/±500 월드 이동 후 입자 위치와 재사용을 확인했다.
- 원경을 내장 이미지 편집으로 불투명한 안개 숲과 실제 alpha의 나무 실루엣으로 분리했다. `forest-far-mist.png`/`forest-far-trees.png`는각2172×724, 원경 이동0.10/0.24, 중경1.0/근경1.35와 기존 크기/Y는 유지한다. 기존 `farHeight/farY` 튜닝은 나무 층이 이어받으며 `farMistHeight/farMistY`만 추가했다. 원본 원경 PNG/meta와 기존 자산은 보존했다. 최종 경로/alpha 표본/프롬프트는 `Art/SplitFarForest.md`, 설정 사용법은 `PresentationTuning.md`에 있다.
- 최종 Unity6000.5.9f1 / 임시 복사본 / PlayMode 필터 `ParticleReusePlayModeTests;PresentationTuningPlayModeTests;ForestDuelPlayModeTests;ImpactGlowPlayModeTests;CombatTempoPlayModeTests;DuelPrototypePlayModeTests`: **36/36 통과**, 실패·건너뜀0,34.9508635초. 결과 `Logs/ParticleFarLayerResults.xml`, 기록 `ParticleFarLayerTests.log`. 반복 파티클3개/배경 튜닝5개/숲9개/빛4개/템포7개/기존 한 판8개다. 원경 두 층의 독립 실시간 조절·기존 튜닝 보존·이동 비율·장거리 반복과 전투/발광/큐 동작을 확인했다.
- 별도 그래픽 Editor에서 재대결 초기화 없이 실제 파티클40회를 먼저 반복시켜 **40/40 방출, 시스템2개 재사용**을 확인하고 이어 편성→큐3개 예약→타격→다음 편성을 진행했다. 실제1600×900 Game View에서 두 원경의 투명 합성과 반복 사용 이후의 불꽃·기존 HUD를 육안 확인했다. 캡처 `Logs/ParticleFar-Planning.png`, `ParticleFar-Queued.png`, `ParticleFar-Combat.png`, `ParticleFar-NextTurn.png`; 기록 `ParticleFarVisual.log`. 타격 캡처용 Controller 일시 정지만 했으며 생산 카메라를 바꾸지 않았다. 정지 화면이 전체 플레이 손맛을 보장하지는 않는다.
- Assets metadata174개/GUID174개, 누락0/중복0. 수정 소스·설정·검사·신규 PNG/meta와 원본 원경/파티클 prefab/Controller15개는 실행 복사본과 SHA256이 일치했다. 캡처 도구는 임시 복사본에만 존재하며 작업용 에디터를 조작하지 않았다. 씬·프리팹·패키지·프로젝트 설정·구 레거시 프로젝트와 기존 튜닝값을 변경하지 않았다.
- 게임 C# 컴파일 오류·게임 예외·새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱스 예외와 종료 JobTempAlloc 경고는 다시 관찰했다. 전체 Console이 깨끗하다는 주장은 아니다. 전체 EditMode/PlayMode·출시 빌드·임의 배경 축소에서의 세로 coverage·모든 그래픽 API/비율·성능은 미검증이다. 검증 Unity 종료를 확인한 뒤 임시 복사본만 삭제하고 로그·XML·캡처는 보존했다.

## 이전 변경: 직접 조절하는 배경·전투 템포와 타격 발광 (2026-09-26)

검증 상태: **Ready with limitations**. 요청한 조절값·전투 연출을 구현하고 관련 검사와 실제 화면을 확인했다. 전체 검사·출시 빌드·성능 측정은 하지 않았다.

- 지속 설정 자산 `Resources/DuelPresentationSettings.asset`과 **Turn Limbo → 연출 튜닝 열기** 메뉴를 추가했다. 배경 3층의 높이/Y, 애니메이션 재생 배율, 연타 간격, 스킬 간격, 실제 시간 히트 스톱, 발광/Bloom/화면 섬광을 Inspector에서 조절한다. 배경/빛은 즉시 적용하고 공격 속도/연타 간격은 다음 스킬에서 함께 반영한다. 사용법과 적용 시점은 `PresentationTuning.md`에 있다.
- 기본 배율1로 칼 동작의 빠른 재생을 유지하면서 연타 사이0.10초, 스킬 사이0.28초, 타격 뒤 실제0.04초의 히트 스톱을 넣었다. 임팩트는 배율에 맞춘 원본 타격 프레임을 따른다. 동시 공격 정지를 중복 합산하지 않고 마지막 큐 뒤에는 추가 대기를 넣지 않는다. 긴 프레임도 여러 타격을 한 번에 해결하거나 연타 대기 포즈에서 타격하지 않도록 제한했다. 큐·ACT·피해 규칙·추격/밀림·재시작·기존 슬로모션·머리 위 HUD는 유지한다.
- 새 URP HDR 섬광은 일반/방어/치명타 색을 달리하며 실제 시간으로0.14초 감쇠한다. 공유 메시/재질과 최대16개 풀을 사용한다. Bloom은 전장 카메라의 런타임 Volume에만 추가하며 공유 렌더 파이프라인/프로젝트 설정은 변경하지 않았다.
- 첫 native 타격 화면에서 레거시 ray의 HDR 약30배와 분홍 입자색이 Bloom에 크게 번지는 것을 관찰했다. 원본 파티클 자산은 수정하지 않고 런타임 Bloom 입력만 최대4로 제한했다. 최종 실제 타격 화면에서는 밝은 광선/링이 남고 큰 마젠타 번짐은 억제되어 배우/HUD/피해 숫자가 읽힌다. 원본 타격 순간 size2/jolt는 그대로여서 순간 구도와 신체 일부의 잘림은 유지된다.
- 최종 임시 복사본 / Unity6000.5.9f1 / PlayMode 필터 `CombatTempoPlayModeTests;PresentationTuningPlayModeTests;ImpactGlowPlayModeTests;LegacyAnimationPlayModeTests;ForestDuelPlayModeTests;DuelPrototypePlayModeTests`: **36/36 통과**, 실패·건너뜀0, 34.9514316초. 결과 `Logs/TuningTempoFinalResults2.xml`, 기록 `TuningTempoFinalTests3.log`. 새 템포7개/배경 조절4개/빛4개와 기존 애니메이션4개/숲 교전9개/한 판8개다. 영속 자산을 바꾸지 않고 복제 설정으로 실시간 튜닝·타격 포즈 동기화·대기·히트 스톱·저프레임·재대결·발광 풀·수명·Bloom 상한/켜기/끄기를 검사했다.
- 앞선35/35 결과 `TuningTempoResults.xml`은 Bloom 상한 회귀를 추가하기 전 기록이다. 검사 코드의 Unity6 `GetInstanceID` 사용 오류를 풀 객체 참조 비교로 고쳤고, 마지막 Bloom 검사에서 빠진 URP/Core 테스트 어셈블리 참조도 보완한 뒤 최종 검사가 실행됐다. 최초 Bloom 검사에서는 초기화 직후의 입자 수를 너무 일찍 확인하여1개가 실패했다. 일반 시뮬레이션0.05초 뒤 검사하도록 보완하고 원래 불꽃 발생 코드는 변경하지 않았다. 실패 시도는 `TuningTempoTests.log`, `TuningTempoFinalTests.log`, `TuningTempoFinalTests2.log`/`TuningTempoFinalResults.xml`에 남겼다. 최종 실행에서 게임/검사 C# 컴파일 오류·게임 예외·셰이더 오류는 관찰하지 않았다.
- 최종 별도 그래픽 Editor의 실제1600×900 Game View에서 메뉴가 설정 자산을 선택함을 확인하고, 편성→3개 예약→실제 타격→다음 편성을 캡처했다. 화면 `Logs/TuningTempoFinal-Planning.png`, `TuningTempoFinal-Queued.png`, `TuningTempoFinal-Combat.png`, `TuningTempoFinal-NextTurn.png`; 기록 `TuningTempoFinalVisual.log`. 캡처를 위해 실제 타격이 발생한 프레임의 Controller만 잠깐 정지했으며 생산 코드의 카메라/구도는 바꾸지 않았다. 정지 화면과 자동 검사는 플레이 체감/손맛 전체의 보장이 아니다.
- Assets metadata171개/GUID171개, 누락0/중복0. 최종 변경 소스·셰이더·설정·메뉴·검사·검사 어셈블리12개는 실행 복사본과 SHA256이 일치한다. 캡처 도구는 임시 복사본에만 추가했다. 구 레거시 프로젝트·씬·프리팹·기존 이미지·공유 재질·패키지·프로젝트 설정과 열린 사용자 에디터는 수정하지 않았다.
- native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱스 예외와 종료 JobTempAlloc 경고는 다시 관찰했다. Console 전체가 깨끗하다는 주장은 아니다. 배경을 임의로 작게 줄일 때의 세로 coverage·모든 비율/회전·전체 EditMode/PlayMode·출시 빌드·성능은 미검증이다. 검증 Unity 종료를 확인한 뒤 임시 복사본만 삭제하고 XML·로그·캡처는 보존했다.

## 이전 변경: 원경 추가 축소·중경 픽셀 아트 정리 (2026-09-26)

검증 상태: **Ready with limitations**. 요청한 배경 표현만 변경하고 관련 검사와 실제 화면을 확인했다. 출시 빌드·전체 검사·성능 측정은 범위 밖이다.

- 원경 높이16→14.4(직전의90%)만 추가 축소했다. 중경/전경 크기·중심·패럴랙스와 배우·카메라·전투·HUD·씬·설정은 그대로다. 내장 이미지 편집으로 중경의 미세 얼룩과 흐린 질감을 단순한 도트 덩어리로 정리했다. 실제 프롬프트·자산·표현 한계는 `Art/PixelBeltForest.md`에 기록했다.
- 중경은 동일한 `forest-belt-mid.png`(2172×724) 경로·GUID·임포트를 유지한다. 이전 그림은 `Art/Sources/forest-belt-mid-depth-v1.png`에 보존했다. alpha 표본9408개 중 완전 투명1224개이며, 바닥은 거의 불투명하나 모든 픽셀이255인 결과는 아니다. 실제 합성 화면으로 확인했다.
- 임시 복사본 / Unity 6000.5.9f1 / PlayMode 필터 `ForestDuelPlayModeTests`: **9/9 통과**, 실패·건너뜀0, 0.1016546초. 결과 `Logs/PixelBeltResults.xml`, 기록 `PixelBeltTests.log`. 새 검사를 추가하지 않고 원경 크기 기대값만14.4로 갱신했다. 자산 연결·실제 크기/발 위치·누적 이동·재시작·타일 반복·피해 숫자 및16:9/32:9의 편성/교전17도 범위를 확인했다.
- 별도 그래픽 Editor의 실제1600×900 Game View에서 편성·3개 예약·타격·다음 턴을 확인했다. 작은 원경/선명한 중경·넓은 바닥과 기존 파티클·머리 위 HUD가 함께 렌더링된다. 캡처 `Logs/PixelBelt-Planning.png`, `PixelBelt-Queued.png`, `PixelBelt-Combat.png`, `PixelBelt-NextTurn.png`; 기록 `PixelBeltVisual.log`. 정지 프레임이며 전투 손맛 전체를 검증한 것은 아니다.
- Assets metadata161개/GUID161개, 누락0/중복0. 수정 배경 소스·PNG/meta·관련 검사와 기존 Arena/Controller/HUD는 검증 복사본과 SHA256이 일치했다. 원본 작업 에디터는 조작하지 않았으며 캡처 도구는 임시 복사본에만 존재했다.
- 게임 컴파일 오류·게임 예외·새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱스 예외와 종료 JobTempAlloc 경고는 다시 관찰했으며 Console 전체가 깨끗하다는 주장은 아니다.
- 32:9/size3.5/17도 원경 세로 여유는 약0.215씩이다. 같은 비율의20도 회전에는 높이가 부족할 수 있어 임의 회전·충격흔들림·모든 비율을 보장하지 않는다. 전체 PlayMode/EditMode·플랫폼 빌드·완벽한 고정 도트 격자·색상 수 보장은 미검증이다. 검증 Unity 종료 확인 뒤 임시 복사본만 삭제하고 결과는 보존한다. Play 재시작 시 새 배경이 구성된다.

## 이전 변경: 벨트스크롤 숲길과 배경 축소 (2026-09-26)

검증 상태: **Ready with limitations**. 이번에는 배경 자산·배치만 변경했다. 캐릭터/카메라/교전 이동/큐/ACT/피해/HUD는 변경하지 않았고, 관련 검사와 실제 Game View만 가볍게 확인했다.

- 내장 이미지 생성으로 넓은 바닥과 얕은 원근을 가진 `Resources/ForestArena/forest-belt-mid.png`를 새로 제작했다(2172×724, 실제 alpha). 이전 중경·원본 교문은 보존했다. 생성 모드·실제 프롬프트·임포트/배치는 `Art/BeltForest.md`에 있다. 실제 3D나 앞뒤 이동/스텝 판정은 추가하지 않았다.
- 중경/전경 높이를 18→12.6(70%), 원경을 18→16(88.9%)으로 줄였다. 새 중경 중심 y=-1.2, 뒤쪽 경계 약 y=-0.82와 바닥 하단 y=-7.5 사이에 발/그림자 y≈-2.73을 배치했다. 축소는 AI 도트 격자의 완전한 복원이 아니라 과한 확대를 줄이는 조정이다.
- 임시 복사본 / Unity 6000.5.9f1 / PlayMode 필터 `ForestDuelPlayModeTests`: **9/9 통과**, 실패·건너뜀 0, 0.0809343초. 결과 `Logs/BeltForestResults.xml`, 기록 `BeltForest.log`. 기존 7개와 신규 2개로, 실제 Sprite/레이어 높이·폭·발 위치·배우 크기 불변 및 현재 카메라의 편성/교전 구도를 확인한다.
- 현재 카메라 범위 검사는 편성 size6/y=-1.5/회전0, 교전 size3.5/y=-0.5/회전17도, 16:9·32:9 및 양·음수 장거리 X에서 수평 타일과 원경 세로 coverage를 확인한다. size20 검사는 수평 반복 스트레스만 확인하며 모든 확대·회전·충격 흔들림의 세로 coverage를 보장하지 않는다.
- 별도 그래픽 Editor의 실제 1600×900 Game View에서 편성·3개 예약·타격·다음 턴을 캡처해 넓은 바닥/발 위치/작아진 배경과 기존 머리 위 HUD·파티클을 육안 확인했다. 실제 교전 후 player x=0.90/enemy x=4.90, 다음 편성 카메라 중심 x=2.90이며 위치 유지 동작도 보인다. 정지 프레임이므로 손맛·속도의 완전한 검증은 아니다.
- 화면: `Logs/BeltForest-Planning.png`, `BeltForest-Queued.png`, `BeltForest-Combat.png`, `BeltForest-NextTurn.png`. 실행 기록: `BeltForestVisual.log`. 이전 숲길 캡처와 구분한다.
- Assets metadata 161개, GUID 161개, 누락 0, 중복 GUID 0. 배경 소스·신규 PNG/meta·관련 검사 및 기존 Arena/Controller/HUD 소스는 검증 복사본과 SHA256이 일치했다. 검증 도구는 임시 복사본에만 추가했으며 원본 씬·프리팹·패키지·설정과 열린 사용자 에디터는 수정하지 않았다.
- 게임 컴파일 오류·게임 예외·새 셰이더 오류는 관찰하지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱스 예외와 종료 JobTempAlloc 경고는 다시 관찰했으며 Console 전체가 깨끗하다는 주장은 아니다.
- 전체 PlayMode/EditMode 재실행·플랫폼 빌드·성능 측정·모든 비율은 미실행이다. 검증 프로세스 종료 확인 후 임시 복사본만 삭제하며 XML·로그·캡처는 보존한다. Play를 종료하고 재시작하면 새 배경과 축소 배치가 구성된다.

## 이전 변경: 녹빛 숲길과 이동형 교전 (2026-09-26)

검증 상태: **Ready with limitations**. 이번 요청의 배경·이동·연관 HUD를 컴파일/PlayMode/실제 화면으로 확인했다. 출시 빌드·전체 EditMode·모든 화면 비율은 이번 범위가 아니다.

- 내장 이미지 생성으로 원경/중경/근경 PNG 3개를 제작하여 `Resources/ForestArena`에 저장했다. 모두 2172×724, Point Sprite이며 중경·근경의 실제 알파를 보존했다. 원본 교문 그림은 수정하지 않았다. 실제 프롬프트와 임포트/배치 사양은 `Art/ForestLayers.md`에 있다.
- 피해 기반 넉백(0.1초), 방어 시 감소, 공격자 추격, 접촉 간격 4와 교차 방지, 미완료 밀림 누적 제한을 구현했다. 타격 또는 턴 종료 뒤 초기 위치 복귀는 없으며 재대결만 초기화한다. 원본 큐·피해·ACT Runtime은 변경하지 않았다. 저프레임/사거리 밖에서는 타격을 일괄 해결하지 않고 접촉 후 순서대로 재생한다.
- 카메라의 과거 중심 중복 합산을 제거하고 현재 중심/피격 대상을 따라간다. 3층은 0.24/1.0/1.35 수평 이동 비율로 반복한다. 피해 숫자도 발생한 월드 지점을 저장하여 카메라 이동·확대에 맞춰 매번 재투영한다(움직이는 캐릭터 Transform에 붙이는 방식은 아님).
- 첫 관련 PlayMode 필터 `ForestDuelPlayModeTests;DuelPrototypePlayModeTests;LegacyAnimationPlayModeTests;FixedHudPlayModeTests`: **25/25 통과**, 실패·건너뜀 0, 28.90초. `Logs/ForestDuelResults.xml`, `ForestDuel.log`. 피해/가드·누적 밀림·방향·턴 사이 위치·장거리 카메라·재시작·실제 3 Sprite·패럴랙스·수평 반복 풀 재사용과 기존 큐·한 판·입력·애니메이션·머리 위 HUD를 확인했다.
- 화면 검수 후 실제 잔디 시작선(510/724)을 발/그림자 높이 y=-2.73에 맞춰 중경 중심 y=0.95로 조정하고 근경을 y=0.9로 내려 숲길을 드러냈다. 피해 숫자 재투영 회귀를 추가한 최종 `ForestDuelPlayModeTests`: **7/7 통과**, 실패·건너뜀 0, 0.073초. `Logs/ForestDuelFinalTargetedResults.xml`, `ForestDuelFinalTargeted.log`. 앞 검사와 6개가 겹치며 신규 숫자 검사 1개가 추가된 재검증이지 서로 다른 32개 검사가 아니다.
- 별도 그래픽 Editor의 실제 1600×900 Game View에서 편성·3개 예약·실제 타격·다음 턴을 확인했다. 원본 캐릭터/파티클·발 위치·머리 큐/상태바·피해 숫자가 새 숲에 함께 렌더링된다. 마지막 실행의 첫 교전 후 위치는 player x=0.18/enemy x=4.18, 카메라 중심 x=2.18로, 초기 위치로 돌아가지 않고 다음 편성을 이어갔다. 재시작 초기화는 자동 검사에서 확인했다.
- 최종 화면: `Logs/ForestDuel-Planning-Final.png`, `ForestDuel-Queued-Final.png`, `ForestDuel-Combat-Final.png`, `ForestDuel-NextTurn-Final.png`. 기록: `ForestDuelVisualFinal.log`. 캡처는 실제 전투의 잠깐 정지한 프레임이므로 손맛/실제 속도의 완전한 검증은 아니다. 원본 타격 순간 size=2와 위치 jolt는 유지되어 그 짧은 확대에서는 신체 일부가 잘릴 수 있다.
- 수평 반복 스트레스는 32:9/size20/17도 및 양·음수 X에서 여러 타일 폭 이동을 검사하지만, 고정 높이 18의 세로 전체를 보장하는 검사가 아니다. 실제 지원은 현재 편성/교전 카메라 범위와 권장 16:9 구도다. 임의의 확대/모든 비율·회전은 미검증이다.
- Assets metadata 160개, 누락 0, 중복 GUID 0. 최종 4개 Presentation 소스·3개 관련 테스트·3개 PNG는 검증 복사본과 SHA256이 모두 일치했다. 원본 프로젝트·씬·프리팹·패키지·설정은 수정하지 않았으며 캡처 도구는 임시 복사본에만 존재했다.
- 게임 C# 컴파일 오류·게임 예외·새 셰이더 오류는 관찰하지 않았다. 테스트 시작의 라이선스 IPC handshake 경고 뒤 실행은 정상 완료했다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` 인덱스 예외와 종료 JobTempAlloc 경고는 별도로 관찰했으며 Console 전체가 깨끗하다는 주장은 아니다.
- 직접 조작하는 스텝/회피 판정, 출시 플레이어 빌드, 플랫폼별 실행·성능 프로파일링은 미실행이다. 검증 프로세스 종료를 확인한 뒤 임시 복사본만 삭제하고 XML·로그·캡처는 보존했다. Play를 재시작하면 자동 구성된 새 숲과 이동 연출이 적용된다.

## 이전 변경: 머리 위 스킬 큐와 하단 상태바 묶음 (2026-09-26)

검증 상태: **Ready with limitations**. 사용자 요청에 맞춰 컴파일과 관련 HUD 검사만 한 번 실행했다. 전체 검사·플랫폼 빌드·추가 화면 캡처는 하지 않았다.

- 각 캐릭터 머리 위에 큐를 상단, 체력·저항 바를 그 아래에 배치했다. 같은 local 머리 기준점을 한 번 투영하며, 별도 추적 보간 없이 함께 갱신한다. 카메라 확대에 따른 UI 크기 보정은 없다.
- 큐는 64×64 카드/48×48 아이콘, 72 간격, 최대 5칸마다 줄바꿈하며 각 행은 가운데 정렬한다. 기존 플레이어 왼쪽/적 오른쪽 성장 순서, 해결된 카드 숨김, 현재 슬롯 확대·검사 강조는 유지한다. 큐 아래에는 12 단위 간격으로 기존 236×92 상태바를 둔다.
- 화면 경계에서는 큐 전체와 상태바의 공통 경계를 제한한다. 따라서 카메라가 한 캐릭터에 집중하거나 큐가 길어져도 두 요소를 따로 이동시켜 상하 순서가 뒤집히지 않는다.
- 확인한 Tab 경로는 기존 `IsInspecting`→`SetPlanningState`→`Arena.Tick`→`Hud.Refresh`이다. 적 중심으로 이동·확대(목표 3.5)하고 놓으면 전체 구도로 복귀한다. 기존 카메라 연출·입력·전투 규칙·리소스는 변경하지 않았다.
- 임시 복사본 / Unity 6000.5.9f1 / PlayMode 필터 `FixedHudPlayModeTests`: **7/7 통과**, 실패·건너뜀 0, 검사 시간 3.98초. 결과: `Logs/QueueHeadHudResults.xml`; 기록: `QueueHeadHud.log`.
- 검사는 캐릭터/카메라 이동·회전 후 공통 추적과 경계 제한, 실제 편성→접근→해결→복귀의 큐 위/상태바 아래 구조, delta=0 즉시 추적·크기 유지, 긴 큐의 부분 행 중앙 정렬·화면 경계·비겹침, 기존 L/A 및 포인터 전달을 확인한다. 추가 검사는 실제 Tab 누름/놓음으로 적 확대/복귀·설명 표시·큐/상태바의 상하 관계·화면 크기 유지·ACT/큐 불변을 확인했다.
- 게임 컴파일 오류·게임 예외는 관찰되지 않았다. 초기 라이선스 IPC handshake 실패 메시지 뒤 검사 실행은 exit 0으로 완료됐다. 검사에 사용한 HUD·테스트 소스는 작업본과 SHA256이 일치한다. 열린 작업 에디터·레거시 프로젝트·씬·패키지·설정은 변경하지 않았다.
- 별도 native Game View 육안 검수, 전체 PlayMode 27개, EditMode, 플랫폼 빌드는 미실행이다. 아래 6개/25개 결과와 기존 화면 캡처는 이전 배치의 자료이며 현재 배치의 시각 검증으로 간주하지 않는다. Play를 재시작한 뒤 Tab을 누르고 놓아 현재 머리 위 간격을 확인한다.

## 이전 변경: 캐릭터 위 체력·저항 바 (2026-09-26)

검증 상태: **Ready with limitations**. 사용자 요청에 맞춰 이번에는 컴파일과 관련 HUD PlayMode 검사 6개만 한 번 실행했다. 전체 검사·플랫폼 빌드·추가 화면 캡처는 실행하지 않았다.

- 상태바만 각 캐릭터의 고정 local 머리 위 기준점에 즉시 붙였다. 애니메이션 Sprite bounds·추적 보간·카메라 확대값 보정은 사용하지 않는다. Overlay Canvas에서 기존 236×92 크기를 유지하며 화면 경계에서는 16 단위 여백으로 제한한다. 큐 고정 배치·기록/확정 입력·전투 규칙·애니메이션 연출은 유지한다.
- 임시 복사본 / Unity 6000.5.9f1 / PlayMode 필터 `FixedHudPlayModeTests`: **6/6 통과**, 실패·건너뜀 0, 검사 시간 2.86초. 결과: `Logs/HeadStatusHudResults.xml`; 기록: `HeadStatusHud.log`.
- 신규 검사는 캐릭터 이동 후 delta=0에서도 머리 위 위치가 즉시 갱신되는지, 머리 기준점과 패널 하단의 12 Canvas 단위 간격, 카메라 확대·이동 후 실제 화면 크기 및 localScale 유지, 스킬 큐 고정 좌표를 확인했다.
- 기존 고정 위치 검사 2개는 상태바가 아닌 큐만 대상으로 갱신했다. 실제 편성→접근→전투→복귀에서 큐 고정, 긴 큐 줄바꿈, L/A 키, 동작 아이콘과 부모 클릭 전달 검사도 통과했다.
- 게임 스크립트 컴파일 오류·게임 예외는 관찰되지 않았다. 초기 라이선스 IPC handshake 실패 메시지 후 실행은 정상 완료(exit 0)됐다. 열린 작업 에디터·레거시 프로젝트·씬·패키지·설정은 변경하지 않았다. 검사 HUD 및 테스트 소스는 작업본과 SHA256이 일치한다.
- 이번 추적 배치는 별도 native Game View 육안 검수를 하지 않았다. 아래 25개 검사와 `FixedHud-*.png`는 직전 화면 고정 배치의 기록이므로 현재 상태바의 시각 검증 자료가 아니다. Play를 종료하고 재시작하여 실제 머리 위 간격의 취향을 확인한다.

## 직전 변경: 기록·확정 아이콘과 화면 고정 HUD (2026-09-26)

검증 상태: **Ready with limitations** — 요청한 동작 아이콘·키 힌트·체력바/큐 고정 배치의 컴파일, PlayMode 검사, 실제 Game View 확인을 완료했다. 출시 빌드나 모든 플랫폼 검증은 이번 범위가 아니다.

- 원인: 기존 `PositionFighter`가 캐릭터의 세계 좌표와 카메라 확대값을 매 `Refresh`에서 화면 좌표로 투영했다. 큐는 전투 단계별 위치 전환과 추적 보간도 수행하여 카메라·캐릭터 움직임이 HUD 좌표에 반영되었다.
- 수정 전 임시 복사본에서 `FixedStatusAndQueueAnchors_DoNotFollowCameraOrActors`를 실행해 실패를 재현했다. 카메라 확대·이동·회전과 캐릭터 이동 후 PlayerStatus 좌표 차이 305.615234 Canvas 단위. 기록: `Logs/FixedHudBaselineResults.xml`, `FixedHudBaseline.log`.
- 체력·저항은 상단 좌우 모서리, 큐는 상단 중앙의 양쪽 고정 앵커로 변경했다. 카메라/캐릭터 투영·추적 보간과 단계별 루트 이동을 제거했다. 남은 큐 카드는 같은 시작점에서 표시하고 긴 큐는 화면 절반 너비에 맞춰 줄바꿈한다. 피해 숫자의 세계 좌표 추적과 전투 카메라/캐릭터 연출은 유지한다.
- 내장 이미지 생성으로 책/체크 동작 PNG 2개를 새로 제작했다. `Resources/HudActions/combat-log`, `confirm-turn`; 1254×1254 원본을 최대 256px Sprite Single, Bilinear, Clamp, mipmap/압축 없음으로 가져온다. 실제 프롬프트와 확정 PNG 불투명도 보정 기록은 `Art/HudActionIcons.md`에 보관한다.
- 각 버튼은 96×112, Icon 64×64, 아이콘 아래 이름과 `L`/`A` 키 힌트를 표시한다. 편성 중 L은 기록 토글, A/Enter는 기존 큐 전체 확정이다. Tab 검사 중 L과 전투 중 기록 열기는 막으며 로그에서 타이머가 계속 흐르는 기존 규칙을 유지한다. 기술 ID·ACT·피해·큐 전투·애니메이션 판정은 변경하지 않았다.
- 수정 후 전체 PlayMode **25/25 통과**, 실패·건너뜀 0, 실행 시간 48.71초. 기존 20개와 신규 5개 검사다. 최종 결과: `Logs/FixedHudFinalPlayModeResults.xml`, `FixedHudFinalPlayMode.log`. 긴 큐 검사를 추가하기 전 24/24 기록도 `FixedHudPlayModeResults.xml`에 남긴다.
- 신규 검사는 카메라/캐릭터 변화 후 4개 HUD 루트 불변, 실제 3슬롯 편성→접근→해결→복귀→다음 턴에서 좌표 불변, L 토글의 ACT/큐 불변 및 A 확정/로그 닫기/전투 중 차단, 실제 EventSystem raycast와 부모 버튼 클릭 전달, 아이콘/이름/키 패널 내 배치·비겹침을 확인한다.
- 긴 큐 검사는 양쪽에 한 행보다 많은 기술을 넣어 아래쪽 줄바꿈, 좌우 성장 방향, 화면 경계와 카드 비겹침을 확인한다. 원본 데이터와 실제 매치 대신 0비용 기술 복사본을 사용하는 시험용 세션이며 실제 게임 ACT 규칙은 바꾸지 않는다.
- 실제 Game View에서 1600×900 편성·예약·실제 접근 후 전투·슬롯 로그, 1280×720 편성/예약, 1280×960 예약/강제 카메라 확대를 캡처해 육안 확인했다. 책/체크 문양과 이름/하단 키가 보이고, 카메라가 확대되어 캐릭터가 커져도 체력바·큐는 같은 위치에 유지된다. 4:3의 전장 가장자리 잘림은 기존 구도이며 카메라 재설계는 하지 않았다.
- 캡처: `Logs/FixedHud-Planning-1600.png`, `FixedHud-Queued-1600.png`, `FixedHud-Combat-1600.png`, `FixedHud-Log-1600.png`, `FixedHud-Planning-720.png`, `FixedHud-Queued-720.png`, `FixedHud-Queued-960.png`, `FixedHud-Zoom-960.png`. 실행 기록: `FixedHudVisual.log`.
- 신규 자산/test metadata와 GUID를 확인했다. Assets metadata 154개에서 중복 GUID 0, 자산 파일의 누락 metadata 0. 검증 복사본의 HUD/컨트롤러/신규 테스트 및 두 PNG의 SHA256은 작업본과 모두 일치한다. 테스트 코드나 캡처 전용 도구는 게임 규칙을 변경하지 않았다. 캡처 도구는 임시 복사본에만 두었고, 검증 Unity 종료를 확인한 뒤 해당 임시 복사본만 삭제했다. 결과 XML·로그·캡처는 보존했다.
- 게임 컴파일 오류·게임 예외·누락 아이콘은 관찰되지 않았다. native Editor의 기존 `UnityEditor.Search.SearchDatabase` Index 예외와 종료 시 JobTempAlloc 경고는 별도로 관찰했으며 Console 전체가 깨끗하다고 주장하지 않는다.
- 이번 변경에서는 EditMode 35개와 플랫폼 실행 파일 빌드를 다시 실행하지 않았다. 아래의 과거 결과와 구분한다. 모든 화면 비율·입력 장치·긴 설명·모바일 safe area는 미검증이다. 열린 사용자 에디터·레거시 프로젝트·씬·패키지·설정은 변경하지 않았다. Play를 종료 후 다시 시작하면 새 UI가 구성된다.

## 직전 변경: 버튼형 기술 아이콘 (2026-09-26)

검증 상태: **Ready with limitations** — 현재 첫 결투의 9개 신규 아이콘 연결·상호작용·화면 확인은 통과했다. 출시 빌드나 모든 플랫폼 검증은 이번 범위가 아니다.

- 내장 이미지 생성 도구(image_gen)로 9개 키캡형 버튼 PNG를 제작하고 `Assets/Game/Resources/SkillButtons/skill1..9.png`에 저장했다. 실제 프롬프트·기술별 문양·출처는 `Art/SkillButtonIcons.md`에 기록했다.
- 기본/강화 공격과 3개 방어는 색뿐 아니라 단일/이중 베기 호, 직선/표적, 작은/큰 충격, 방패/곡선/교차 칼의 실루엣으로 구분한다.
- `LegacyDuelArt`의 아이콘 로드 경로만 새 세트로 교체했다. HUD 레이아웃, 기술 ID, ACT 비용, 기술열 순환, 전투·애니메이션·입력 규칙은 이번 변경에서 수정하지 않았다.
- 신규 PNG 9개는 1254×1254이며 표본 검사에서 알파 구멍이 없었다. Unity에는 최대 256px Sprite Single, 중앙 pivot, Bilinear, Clamp, mipmap 없음, 압축 없음으로 가져온다. 신규 폴더·PNG의 metadata가 존재하고 중복 GUID가 없다.
- 보존한 `LegacyDuel/Icons/skill1..9.png`는 구 프로젝트 `Assets/Resources/Icon/` 원본과 SHA256이 모두 일치한다.
- 별도 임시 복사본에서 Unity 6000.5.9f1 전체 PlayMode **20/20 통과**, 실패·건너뜀 0. 기존 18개 회귀 검사와 신규 2개 검사다. 결과: `Logs/ButtonIconsPlayModeResults.xml`, 실행 로그: `ButtonIconsPlayMode2.log`.
- 신규 검사는 9개 독립 Sprite·Texture 로드, 중앙 pivot·정사각·import 설정, invalid ID의 null 반환, 로더 매핑을 확인한다. 현재/다음 전체 9개, 플레이어/적 큐, 실제 3슬롯 전투 후 양쪽 로그 및 빈 적 슬롯의 아이콘 없음도 검증했다. 기존 ACT·기술열·포인터·키보드·애니메이션·재대결 검사는 그대로 통과했다.
- 이번 변경에서 EditMode 35개를 다시 실행하지는 않았다. 위 전체 53개 및 최종 7개 기록은 직전 컴팩트 HUD 변경의 결과이며, 이번 신규 아이콘 실행 결과는 PlayMode 20개다.
- 실제 Game View에서 1600×900 편성·예약 후 ACT 0/강화 비용·실제 슬롯 해결 후 로그, 1280×720 편성을 캡처하여 확인했다. `ButtonIcons-AllNine.png`는 검증 복사본에서 만든 아이콘 비교용 화면이며 제품에 추가한 메뉴가 아니다.
- 캡처: `Logs/ButtonIcons-Planning-1600.png`, `ButtonIcons-Queued-1600.png`, `ButtonIcons-Log-1600.png`, `ButtonIcons-Planning-720.png`, `ButtonIcons-AllNine.png`. 기록: `ButtonIconsVisual.log`.
- 게임 스크립트 컴파일 오류·아이콘 누락·게임 예외는 없었다. native Editor 시작의 기존 `UnityEditor.Search.SearchDatabase` Index 예외와 종료 시 Unity JobTempAlloc 경고는 별도로 관찰되었으며 Console 전체가 깨끗하다고 주장하지 않는다. 게임 화면 캡처와 테스트는 완료됐다.
- 첫 검증 프로세스는 라이선스 IPC 연결이 지연되었다. 해당 임시 프로세스만 종료하고 재실행한 뒤 정상 실행·20개 통과를 확인했다. 실패한 환경 실행 로그는 `ButtonIconsPlayMode.log`에 남겨두었다. 열린 사용자 에디터·기존 씬·프로젝트 설정·패키지는 변경하지 않았다.
- 검사에 사용한 `LegacyDuelArt.cs`는 작업본과 SHA256 `05B13EEC2F35C49463B410201BDE7FDC2995342B9699FDC9F62D3C3A660E8F16`이 일치한다. 검증 전용 캡처 스크립트는 임시 복사본에만 두었다.
- 플랫폼별 빌드, 모든 화면 비율, 모든 입력 장치, 전체 49개 기술·성장 아이콘은 미검증/미제작이다. Play를 종료하고 다시 시작하면 현재 결투에서 새 9개 버튼 세트를 사용한다.

## 자동 검증: 직전 컴팩트 HUD 변경

- Unity 스크립트 컴파일 성공.
- EditMode: 35/35 통과, 실패·건너뜀 0. 원본 큐 전투 테스트 24개와 초기 기반 테스트 11개.
  - 다중 타격을 한 타씩 처리하고, 슬롯 시작에만 위력을 정한다.
  - 앞 타격의 사망 후에도 슬롯의 남은 타격을 처리하며, 슬롯 완료 시 승패를 확정한다.
  - 실제 체력 차감량과 원본 오버킬 피해 숫자·두 배 피해 전 넉백값을 분리한다.
- PlayMode: 18/18 통과, 실패·건너뜀 0. 기존 전투·HUD 검사 8개, 실제 애니메이션 검사 4개와 컴팩트 HUD 검사 6개.
  - 편성 중 피해 없음, ACT 소비·기술열 회전, 확정 후 전체 큐 해결, 다음 턴 갱신.
  - 실제 한 판 종료 후 체력·ACT·덱·적 예고·타이머 재대결 초기화.
  - 빈 플레이어 큐도 10초 후 자동 확정되어 적 전체 큐 해결.
  - 공식 InputTestFixture로 Q 누름/놓음 및 A 확정 경로 검증.
  - 원본 PPU 18·scale 1·시작 위치, 접근 중 피해 없음, 턴 종료 후 ±3.5 복귀.
  - 원본 클립 로딩과 레거시 공격 이벤트 제거로 중복 판정 방지.
  - 두 캐릭터의 대기 8프레임·반복, 기본 9개 기술의 준비/타격 프레임·다단 반복을 실제 SpriteRenderer 값으로 확인.
  - 재생 시계 정지, 공격 중 직접 Reset, 턴 종료 시 대기 복귀, 1회/3회 비대칭 공격의 독립적 복귀 및 타격 시점 전후 확인.
  - 불꽃 생성, 넉백, 카메라 확대, 실제 시간 기준 집중 효과 종료 및 재대결 시 정리.
  - 평소 설명 숨김, Tab 설명·Tab+A 확정, 전투 중 입력 UI 숨김.
  - 새 수평 HP/저항 상태바의 크기·정방향·값, 기록 버튼 열기·닫기·슬롯 기록·재대결 초기화.
  - 어두운 재대결 버튼 본문과 밝은 글자의 대비(강조색 테두리와 본문을 구분).
  - 현재/다음 기술·비용·ACT 숫자/게이지의 정확한 매핑, ACT 부족 시 예약 거부·자동 확정 없음.
  - 아이콘/비용/ACT의 화면 내 배치·겹침 방지, 실제 EventSystem raycast를 통한 세 기술 버튼과 확정 버튼 클릭.
  - Q 길게 누르기에서 설명을 열되 예약하지 않음, 화면 네 모서리에서 설명창 clamp, Tab 적 설명.
- 최종 자동 테스트 로그에는 스크립트 오류, 게임 예외, VFX 셰이더 오류가 없다.
- 전체 53개 통과 후 최종 아이콘 캐시·정리 코드에서 HUD 관련 7개도 재검증해 통과했다. 기록은 `Logs/CompactHudFinalTargetedResults.xml`, `CompactHudFinalTargeted.log`이다.

## 리소스 및 실제 화면

- 기본 캐릭터·배경·기술 아이콘·폰트·음원 28개와 원본 임포트 설정을 재사용한다.
- 캐릭터의 원본 애니메이션 10개, 그림자, 원본 두 ParticleSystem 및 필요한 렌더링 의존성을 추가했다.
- 이전 HUD PNG 22개는 원본과 바이트가 일치하며 참고 리소스로 보존한다. 현재 장식은 중립색 uGUI 패널로 교체하고 원본 기술 아이콘·폰트를 계속 사용한다.
- Assets 내 누락된 metadata 0개, 중복 GUID 0개를 확인했다.
- 그래픽을 활성화한 별도 Unity Editor의 실제 Game View에서 1600×900 화면을 캡처했다.
- 편성, 접근, 피격, 다음 턴, 선택형 기록 화면과 승패 화면을 육안 확인했다.
- 피격 캡처에서 원본 0.05초 지연 뒤 불꽃 100개가 생성되고, 원형 파동·스파크·타격색과 캐릭터 밀림이 함께 렌더링됨을 확인했다.
- 화면 검수로 찾은 HP 반전·회전 누락, 빈 기록 버튼, 전투 화면 전환 시점 및 재대결 글자 대비를 보정했다.

최신 전체 자동 테스트 XML은 `Logs/CompactHudEditModeResults.xml`, `Logs/CompactHudPlayModeResults.xml`에 보관한다. 실행 로그는 `CompactHudEditMode.log`, `CompactHudPlayMode.log`이다. 이전 애니메이션 검증 XML은 `AnimationEditModeResults.xml`, `AnimationPlayModeResults.xml`, 원본 연출 검증 로그는 `PresentationVisual.log`에 보존한다. `Presentation-*.png`는 이전 원본 HUD 화면이다. Logs 폴더는 버전 관리에서 제외된다.

## 컴팩트 HUD 교체

요청 범위는 UI 디자인과 정렬이다. Runtime 전투·ACT 소비·입력·애니메이션·전장 코드, 씬·프리팹·패키지·프로젝트 설정은 변경하지 않았다.

원인 증거: 원본 아이콘 9개는 106×106이지만 알파 영역의 X가 13~105로 치우치고 Y 범위도 기술별로 다르다. 원본 ACT 프레임과 fill은 크기(1004.5×70 / 933.63×17.2)·중심(950,40 / 951,27)이 달랐다. 비용 Text도 카드 밖에 놓일 수 있는 200×200 영역이었다. 새 디자인은 PNG를 보존하고 알파 영역만 UI 전용 Sprite로 정리하며, ACT track/fill은 동일 520×8 Rect와 숫자·10칸 구분선을 사용한다. 도크는 352→200으로 축소했다. 현재 사양은 `CompactHudDesign.md`, `LegacyHudParity.md`는 역사적 원본 참고 문서이다.

기존 부채꼴 회전 테스트는 새 수평 상태바 계약으로 명시적으로 교체했다. 첫 검증에서 재대결 대비 검사가 밝은 테두리를 배경으로 잘못 읽어 실패했다. 실제 글자 뒤의 어두운 `Surface`를 검사하도록 교정했으며, 검사를 제거하거나 실패를 무시하지 않았다. 나머지 전투·한 판·재대결·타이머·키보드·애니메이션 검사는 유지했다.

별도 실제 Game View에서 편성, ACT=0, 요청형 설명, 전투, 기록, 결과 화면을 확인했다. 해상도는 1280×720, 1600×900, 1920×1080, 1280×960(4:3), 1920×800(와이드)이며 밝은 단색 배경에서도 패널과 글자가 구분됐다. 화면 검수로 상태창이 캐릭터 머리를 덮는 위치를 조정했다. 캡처는 `Logs/CompactHud-*.png`, 실행 기록은 `CompactHudVisual.log`이다. 밝은 배경 및 결과 화면은 검증용으로 기존 표시 API를 직접 호출한 것이며 실제 게임 콘텐츠를 추가한 것이 아니다.

## 애니메이션 고정 회귀 수정

사용자 보고 후 실제 프레임 검사를 추가했다. 수정 전 재생 검사 3개가 모두 실패했으며 대기 첫 프레임에 고정됐다. 기존 검사는 클립 로딩·이벤트 제거까지만 확인해 실제 재생 실패를 놓쳤다.

별도 객체에서 실제 임포트 곡선과 Sprite 참조가 정상임을 확인했다. SpriteRenderer만 있는 객체에는 `SampleAnimation`이 그림을 적용하지 않았지만, 같은 객체에 Animator를 추가하면 두 캐릭터 모두 공격 첫/둘째 프레임이 정상 적용됐다. 캐릭터 생성에 Controller 없는 Animator만 추가해 기존 클립과 수동 시계를 유지했다. 원인·진단 기록·회귀 방지 범위는 `AnimationPlaybackFix.md`를 참고한다.

애니메이션 수정 당시 EditMode 35개 + PlayMode 12개, 총 47개가 모두 통과했다. 별도 그래픽 에디터의 1600×900 Game View에서 베기 준비 자세와 타격 자세·칼 궤적이 두 캐릭터 모두 달라지는 것을 확인했다. 컨트롤러를 멈춘 채 여러 렌더 프레임을 기다려도 지정 자세가 유지돼 별도 Animator 시계가 재생을 진행하지 않는 것도 확인했다. 재대결 후 대기 첫 프레임으로 복원됐다. 화면은 `Logs/Animation-Slash-0.png`, `Animation-Slash-1.png`, `Animation-Reset.png`, 기록은 `AnimationVisual.log`이다. 정지 캡처로 프레임별 모습을 검수한 것이며 출시 빌드 검증은 아니다.

배치 테스트에서는 실제 Game View 캡처가 생성되지 않아 화면 확인을 별도로 수행했다. 원본 대응과 조정점은 `LegacyAssetManifest.md`, `LegacyPresentationAssets.md`, `LegacyHudParity.md` 참고.

## 범위·남은 확인

첫 적 한 명과 기본 9개 기술의 전투를 검증한 것이다. 전체 기술·체인·성장·시나리오, 스텝 추가, 출시용 플레이어 빌드나 다른 플랫폼 검증은 아니다. Q 길게 누르기의 설명·예약 방지와 경계 처리는 자동 검증했고, Space와 전반적인 손맛·최대 길이 큐·기록 스크롤 감각은 실제 수동 플레이에서 추가 확인한다.

UI의 4:3 화면 내 배치는 확인했지만 전장 카메라 구도는 이번 범위에서 바꾸지 않았다. 그 비율에서 캐릭터 무기 끝이 화면 밖으로 나갈 수 있으므로 전장 구도까지 최적화됐다고 주장하지 않는다. 현재 플레이 권장 비율은 16:9이다.

원본의 별도 배경·이펙트 카메라는 하나로 합쳤고, orthographic 환경의 DOF는 미이식이다. 기록 화면은 캐릭터 초상화 대신 기존 기술 아이콘을 쓴다. 한 판 결과·재대결 흐름은 캠페인 진행을 대신하는 새 연결이므로 원본 전체와 완전히 동일하다고 주장하지 않는다.

별도 화면 검증 에디터의 시작 인덱싱에서 UnityEditor.Search 내부 예외가 재현됐다. 스택은 게임 코드가 아니며 캡처와 전투를 막지 않았다. 최종 자동 테스트에서는 발생하지 않았다. 모든 에디터 기능이나 출시 환경의 무결성을 의미하지 않는다.
