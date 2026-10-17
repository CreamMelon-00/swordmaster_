using TurnLimbo.Runtime.Barks;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Persistent authoring values; runtime state and combat rules never live in this asset, but for one rule value
    /// the game passes into every new duel: 맞물림's percent (<see cref="MeshPercent"/>; the rule itself stays in Runtime).</summary>
    [CreateAssetMenu(menuName = "Turn Limbo/Presentation Settings", fileName = "DuelPresentationSettings")]
    public sealed class DuelPresentationSettings : ScriptableObject
    {
        [Header("배경 크기·높이 — 실행 중 즉시 적용")]
        [SerializeField, Min(1f), Tooltip("가장 먼 하늘·안개 층의 높이(월드 단위). 작게 줄이면 카메라 밖에 빈틈이 생길 수 있습니다.")]
        private float farMistHeight = 14.4f;
        [SerializeField] private float farMistY = -0.5f;
        [SerializeField, Min(1f), Tooltip("원경 나무 층의 높이(월드 단위). 기존 원경 크기 설정을 이어받습니다.")]
        private float farHeight = 14.4f;
        [SerializeField] private float farY = -0.5f;
        [SerializeField, Min(1f)] private float midHeight = 12.6f;
        [SerializeField] private float midY = -1.2f;
        [SerializeField, Min(1f)] private float nearHeight = 12.6f;
        [SerializeField] private float nearY = -0.75f;

        [Header("전투 템포 — 속도·간격은 다음 스킬부터 적용")]
        [SerializeField, Range(0.1f, 2f), Tooltip("원본 프레임 재생 속도의 배율. 1=원본, 0.8=조금 느리게. 타격 판정도 같은 프레임에 맞춥니다.")]
        private float animationPlaybackSpeed = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("한 스킬의 연타 동작 사이 대기 시간(게임 시계 초). 동작 자체를 늘이지 않고 연타 사이에 여유를 줍니다.")]
        private float attackInterval = 0.025f;
        [SerializeField, Range(0f, 2f), Tooltip("완료된 스킬과 다음 큐 스킬 사이 대기 시간(게임 시계 초).")]
        private float skillInterval = 0.06f;
        [SerializeField, Range(0f, 0.15f), Tooltip("타격 직후 잠깐 멈추는 시간(실제 초). 0이면 히트 스톱을 끕니다.")]
        private float hitStopDuration = 0.04f;

        [Header("교전 동선·전환 대기 — 다음 동작부터 적용")]
        [SerializeField, Range(0f, .3f), Tooltip("접근 후 기다리는 최소 시간. 실제 이동과 남은 밀림이 끝나야 공격을 시작합니다.")]
        private float approachSettleSeconds = .04f;
        [SerializeField, Range(0f, .3f), Tooltip("턴 종료 후 화면을 정리하는 대기. 남은 밀림은 끝까지 표시합니다.")]
        private float returnSettleSeconds = .04f;
        [SerializeField, Range(0f, .3f), Tooltip("마지막 기술이 끝난 뒤 턴 정리를 시작하기까지의 대기.")]
        private float turnCleanupSeconds = .04f;
        [SerializeField, Range(0f, 1f), Tooltip("칼끼리 맞댄 저항 피해와 성공한 방어의 밀림 비율. 체력 피해·붕괴·마무리의 밀림은 유지합니다.")]
        private float bladeRecoilScale = .35f;

        [Header("자동 전투 동선 — 바닥의 앞뒤 이동")]
        [SerializeField, Tooltip("접근·교전·턴 정리 중 바닥을 따라 앞뒤로 이동합니다. 타격 판정과 기술 사용 순서는 바꾸지 않습니다.")]
        private bool autoDepthEnabled = true;
        // Retain the old lane fields for serialized assets; the physical footwork no longer chooses fixed Y bands.
        [SerializeField, HideInInspector]
        private float combatDepthSpread = .45f;
        [SerializeField, Range(.1f, 8f), Tooltip("바닥의 앞뒤 방향 이동 속도(월드 단위/초). 현재 위치에서 이어서 이동합니다.")]
        private float combatDepthTravelSpeed = 2.8f;
        [SerializeField, HideInInspector]
        private float combatDepthExchangeOffset = .28f;

        [Header("공격 강조 — 다음 스킬부터 적용")]
        [SerializeField, Tooltip("준비 자세에서 뒤로 모았다가 타격 순간 몸체가 앞으로 뻗고 짧은 잔상을 남깁니다. 동작 그림의 완급도 함께 적용합니다.")]
        private bool attackRhythmEnabled = true;
        [SerializeField, Range(0f, 1f), Tooltip("0=기존 동작, 1=준비·베기·회수의 완급과 돌진·잔상을 모두 적용. F1의 공격 강조 버튼으로 켜고 끈 결과를 비교할 수 있습니다.")]
        private float attackRhythmStrength = LegacyAttackRhythm.DefaultStrength;
        [SerializeField, Range(0f, 1f), Tooltip("공격 시 몸체가 앞으로 치고 나가는 표시 거리(월드 단위). 준비 때는 조금 뒤로 모으고 타격 순간에 가장 멀리 뻗습니다.")]
        private float attackLungeDistance = LegacyAttackRhythm.DefaultLungeDistance;

        [Header("베기 잔광 — 다음 스킬부터 적용")]
        [SerializeField, Tooltip("공격의 베기·찌르기 방향을 따라 짧은 검광을 남깁니다. F1에서 공격 강조와 별도로 비교할 수 있습니다.")]
        private bool weaponTrailEnabled = true;
        [SerializeField, Range(0f, 1f)]
        private float weaponTrailStrength = 1f;
        [SerializeField, Range(.04f, .25f), Tooltip("검광이 사라질 때까지의 실제 초. 히트 스톱이나 힘겨루기 중에도 감쇠합니다.")]
        private float weaponTrailSeconds = .12f;

        [Header("교전 카메라 — 실행 중 즉시 적용")]
        [SerializeField, Range(2.8f, 6f), Tooltip("일반 교전의 화면 크기. 값이 클수록 두 캐릭터와 검을 넓게 보여줍니다. 일반 타격에는 급확대를 하지 않습니다.")]
        private float combatCameraSize = 3.5f;
        [SerializeField, Range(1f, 20f), Tooltip("교전 중심을 따라가는 반응 속도. 실제 시간을 사용하므로 히트 스톱에도 부드럽게 따라갑니다.")]
        private float cameraFollowSharpness = 6f;
        [SerializeField, Range(0f, 1.5f), Tooltip("작은 넉백마다 구도를 다시 잡지 않는 가로 여유(월드 단위). 이 범위를 넘는 이동만 따라갑니다.")]
        private float cameraFollowDeadZone = 0.35f;
        [SerializeField, Range(0f, 0.35f), Tooltip("일반 타격의 작은 방향성 흔들림(월드 단위). 0으로 끕니다. 동시 타격이 겹쳐도 이동량을 더하지 않습니다.")]
        private float impactCameraShake = 0.08f;
        [SerializeField, Tooltip("기술의 준비·타격·회수를 따라 카메라가 뒤로 모였다가 공격 방향으로 따라갑니다. 전투 판정과 재생 속도는 바꾸지 않습니다.")]
        private bool combatCameraMotionEnabled = true;
        [SerializeField, Range(0f, 1f), Tooltip("교전 카메라의 이동·당김·기울기와 전투 레터박스의 강도. 0이면 기존의 안정된 구도로 표시합니다.")]
        private float combatCameraMotionStrength = 1f;

        [Header("이동 연출 — 다음 동작부터 적용")]
        [SerializeField, Range(0.5f, 2.5f), Tooltip("피격 밀림과 회피·압박의 이동 거리 배율. 판정 사거리와 캐릭터 최소 간격은 유지합니다.")]
        private float movementDistanceMultiplier = 1.4f;
        [SerializeField, Range(0.5f, 2f), Tooltip("접근·추격·밀림·스텝 이동 속도 배율. 애니메이션 프레임과 공격 간격은 바꾸지 않습니다.")]
        private float movementSpeedMultiplier = 1.2f;
        [Header("다인전 교대 — 실행 중 즉시 적용")]
        [SerializeField, Range(1f, 2f), Tooltip("다인전에서 다음 적의 선행 접근·교전 진입·이전 적의 퇴장 속도 배율. 교대 직전의 기술 사이 대기도 같은 비율로 줄입니다. 준비 이동과 공격 재생에는 적용하지 않습니다.")]
        private float enemyHandoffSpeedMultiplier = 1.35f;

        [Header("피해 숫자 — 다음 표시부터 적용")]
        [SerializeField, Range(80, 240), Tooltip("기본 글자 크기. 작은 피해도 읽을 수 있는 크기를 유지합니다.")]
        private int damageTextFontSize = 168;
        [SerializeField, Range(1f, 1.8f), Tooltip("결정타(저항 붕괴·마무리 일격·큰 체력 피해)와 큰 타격(표시 피해 12 이상) 숫자의 추가 크기 배율. 새로운 치명타 판정을 만들지 않습니다.")]
        private float criticalDamageScale = 1.3f;

        [Header("결정타 연출 — 다음 타격부터 적용")]
        [SerializeField, Range(0, 100), Tooltip("한 번의 타격이 대상 최대 체력의 이 비율(%) 이상을 체력 피해로 깎으면 저항 붕괴·마무리 일격처럼 0.15배 슬로·집중 카메라·색 번쩍임·화면 기울기·크리티컬 효과음을 씁니다. 양쪽 타격 모두 해당합니다. 저항 피해는 세지 않고, 연타는 한 타격씩 봅니다. 0이면 이 조건만 끕니다.")]
        private int decisiveHealthDamagePercent = LegacyDecisiveHit.DefaultHealthDamagePercent;

        [Header("마무리 일격 — 다음 마무리 일격부터 적용 (모든 전투, 승패 무관)")]
        [SerializeField, Range(0f, 4f), Tooltip("전투를 끝낸 타격 뒤 슬로모션 시간(실제 초). 쓰러진 쪽을 집중 카메라로 계속 비춥니다. 일반 결정타의 0.75초보다 길게 둡니다. 0이면 슬로 없이 정지 화면으로 갑니다.")]
        private float finishingSlowMotionSeconds = 1.75f;
        [SerializeField, Range(0.02f, 1f), Tooltip("마무리 일격 슬로모션의 전투 시계 속도. 일반 결정타는 0.15배입니다. 1이면 느려지지 않습니다.")]
        private float finishingSlowMotionScale = 0.06f;
        [SerializeField, Range(0f, 2f), Tooltip("레터박스(위아래 검은 띠)가 다 들어오는 시간(실제 초). 0이면 바로 들어옵니다. 결과 창이나 종료 장면이 나오기 전에 걷힙니다.")]
        private float finishingBarSeconds = 0.4f;
        [SerializeField, Range(0f, 2f), Tooltip("슬로모션 뒤 화면이 완전히 멈추는 정지 화면 시간(실제 초). 0이면 정지 화면 없이 넘어갑니다.")]
        private float finishingFreezeSeconds = 0.5f;
        [SerializeField, Range(0f, 3f), Tooltip("서막 4 임무 강제 패배: 정지 화면 뒤 흑백(회상과 같은 색)으로 바래는 시간(실제 초). 그동안 레터박스가 걷힙니다. 0이면 바래지 않고 바로 종료 장면으로 갑니다.")]
        private float finalFallGreySeconds = 0.9f;
        [SerializeField, Range(0f, 3f), Tooltip("서막 4 임무 강제 패배: 흑백으로 넘어간 종료 장면이 다시 색을 찾는 시간(실제 초). 0이면 종료 장면이 처음부터 컬러로 시작합니다.")]
        private float finalFallColourReturnSeconds = 1.2f;

        [Header("서막 4 임무 수훈 — 수훈 이벤트 뒤 전투가 끝날 때까지")]
        [SerializeField, Range(0f, 1.5f), Tooltip("이아의 수훈 기술(시트 구분 '적')이 시작될 때 전투를 멈추고 카메라가 이아 쪽으로 다가갔다 돌아오는 시간(실제 초). 기운이 한 번 번쩍입니다. 기술 이름 자막은 없습니다. 0이면 끕니다.")]
        private float empowermentCutInSeconds = 0.6f;
        [SerializeField, Range(1.5f, 6f), Tooltip("수훈 컷인에서 카메라가 가장 가까이 갔을 때의 화면 크기. 일반 교전 화면 크기보다 작을수록 더 당깁니다.")]
        private float empowermentCutInCameraSize = 2.8f;
        [SerializeField, Range(0f, 1f), Tooltip("수훈 뒤 화면에 씌우는 따뜻한 금빛 색의 세기. 0이면 끕니다.")]
        private float empowermentWarmTint = 0.4f;
        [SerializeField, Range(0f, 0.6f), Tooltip("수훈 뒤 화면 가장자리를 더 어둡게 하는 양(기본 비네트 0.4에 더함). 0이면 끕니다.")]
        private float empowermentVignette = 0.15f;
        [SerializeField, Range(0f, 5f), Tooltip("수훈 장면이 끝난 뒤 금빛 색·비네트·기운 소리가 차오르는 시간(실제 초). 0이면 바로 켜집니다.")]
        private float empowermentAtmosphereSeconds = 1.2f;
        [SerializeField, Range(0f, 1f), Tooltip("수훈 뒤 전투 내내 낮게 깔리는 기운 소리(aura-loop)의 음량. 0이면 끕니다.")]
        private float empowermentAuraLoopVolume = 0.25f;
        [SerializeField, Range(0f, 0.5f), Tooltip("라우다레(수훈 연타 공격)가 한 번 칠 때마다 카메라가 흔들리는 세기(교전 화면 기준 월드 단위). 연타가 겹쳐도 더해지지 않습니다. 0이면 끕니다.")]
        private float laudareShakeStrength = 0.12f;
        [SerializeField, Range(0.05f, 1f), Tooltip("라우다레 한 타격의 흔들림이 잦아드는 시간(실제 초).")]
        private float laudareShakeSeconds = 0.25f;
        [SerializeField, ColorUsage(false), Tooltip("수훈한 이아가 남기는 잔상의 색. 수훈 기운이 켜져 있는 동안(수훈 장면에서 기운이 켜질 때부터 임무 4 종료 장면의 '@aura knight off'까지) 이아의 지금 모습을 이 색 한 가지로 칠한 실루엣이 이아 바로 뒤에 남았다가 사라집니다. 진하기는 아래 '잔상 불투명도'로 정합니다.")]
        private Color empowermentAfterimageColor = DefaultEmpowermentAfterimageColor;
        [SerializeField, Range(0f, 1f), Tooltip("잔상이 처음 남을 때의 불투명도. 사라지는 동안 0까지 옅어지고, 기운이 서서히 켜지고 꺼질 때는 잔상도 기운과 함께 짙어지고 옅어집니다. 0이면 잔상을 끕니다.")]
        private float empowermentAfterimageAlpha = 0.45f;
        [SerializeField, Range(0.02f, 0.5f), Tooltip("잔상이 하나씩 남는 간격(초). 전투에서는 전투 시계를 따라 히트 스톱·정지 화면에 멈추고 슬로모션에 느려지며, 장면(컷신)에서는 실제 시간입니다. 작을수록 촘촘합니다.")]
        private float empowermentAfterimageInterval = 0.05f;
        [SerializeField, Range(0.1f, 1.5f), Tooltip("잔상 하나가 다 사라지기까지의 시간(초, 위 간격과 같은 시계). 길수록 꼬리가 길어집니다.")]
        private float empowermentAfterimageSeconds = 0.4f;
        [SerializeField, Range(0f, 1.5f), Tooltip("잔상이 사라지는 동안 이아의 등 뒤쪽으로 밀려나는 거리(월드 단위). 가만히 서 있어도 잔상이 등 뒤로 번져 보이게 하며, 천천히 숨 쉬듯 조금씩 달라집니다. 0이면 남은 자리에서 사라집니다.")]
        private float empowermentAfterimageDrift = 0.3f;
        [SerializeField, Range(0f, 0.3f), Tooltip("잔상이 사라지는 동안 커지는 비율(0.06 = 6%). 발은 땅에 붙인 채 커져서, 가만히 서 있어도 몸 둘레에 노란 테가 번집니다. 숨 쉬듯 조금씩 달라집니다. 0이면 크기가 그대로입니다.")]
        private float empowermentAfterimageSwell = 0.06f;

        [Header("전투 대사 — 다음 대사부터 적용 (Resources/Barks, Docs/Barks.md)")]
        [SerializeField, Range(0f, 6f), Tooltip("머리 위 말풍선 하나가 떠 있는 시간(실제 초). 불릿타임·슬로모션·히트 스톱에도 늘어나지 않고, 전투를 멈추지 않습니다. 0이면 전투 대사를 끕니다.")]
        private float barkSeconds = BarkTracker.DefaultShowSeconds;
        [SerializeField, Range(0f, 60f), Tooltip("enemy-hurt·player-hurt(한 타격에 큰 체력 피해) 대사가 한 번 나온 뒤 다시 나올 수 있기까지의 시간(실제 초). 큰 피해의 기준은 위 결정타 연출의 비율입니다. 0이면 큰 피해마다 나옵니다.")]
        private float barkHurtCooldownSeconds = BarkTracker.DefaultHurtCooldownSeconds;
        [SerializeField, Range(0, 99), Tooltip("enemy-low·player-low 대사가 나오는 체력 비율(%). 처음 이 비율 이하로 떨어진 타격에 한 번 나옵니다. 0이면 끕니다.")]
        private int barkLowHealthPercent = BarkTracker.DefaultLowHealthPercent;
        [SerializeField, Range(16, 40), Tooltip("말풍선 글자 크기(1920x1080 기준). 폭이 넘치는 대사는 두 줄로 접힙니다.")]
        private int barkFontSize = DefaultBarkFontSize;

        [Header("전투 시작 카드 — 다음 전투부터 적용 (모든 임무·스테이지·수련)")]
        [SerializeField, Range(0f, 4f), Tooltip("전투가 시작될 때(시작 장면 뒤, 첫 편성 전) 레터박스 사이로 엘리사와 상대의 실루엣·이름, 그 사이의 교차한 검을 보여 주는 카드의 시간(실제 초). 클릭·Enter·Space·Escape로 넘깁니다. 글자는 두 이름뿐입니다. 0이면 끕니다.")]
        private float startCardSeconds = 1.6f;
        [SerializeField, Range(0f, 4f), Tooltip("결과 창의 '다시 도전'으로 같은 전투를 다시 시작할 때의 짧은 카드 시간(실제 초). 0이면 다시 도전에는 카드가 없습니다.")]
        private float startCardRetrySeconds = .8f;

        [Header("엘리사의 눈 — 편성마다 상대의 큐가 드러날 때")]
        [SerializeField, Range(0f, 1.5f), Tooltip("상대의 기술 큐가 드러날 때 첫 카드 뒤에서 놋쇠 톱니가 조금 돌며 사라지고, 빛이 카드들을 훑고 지나가는 시간(실제 초). 글자는 없습니다. 0이면 끕니다.")]
        private float gearShimmerSeconds = .55f;
        [SerializeField, Range(0f, 1f), Tooltip("톱니와 빛의 진하기. 0이면 보이지 않습니다.")]
        private float gearShimmerStrength = .6f;

        [Header("기술 톱니 — 실행 중 즉시 적용 (조작대의 Q/W/E 열)")]
        [SerializeField, Range(82f, 92f), Tooltip("열마다 하나인 기술 톱니의 반지름(이빨 끝까지, 1920x1080 기준 HUD 단위). 톱니는 윗부분만 조작대 위로 보입니다. 기술 자리는 이빨 끝에서 16 안쪽에 있어, 지금 기술 창과 그 위 이름·키 줄도 함께 오르내립니다. 기술 그림·창·비용 판의 크기는 그대로라, 82보다 작으면 다음 자리와 창, 창과 비용 판이 겹칩니다.")]
        private float skillGearRadius = LegacySkillGear.DefaultRadius;
        [SerializeField, Range(160f, 200f), Tooltip("열린 열 톱니 사이의 간격(HUD 단위). 세 열이 넘기기·숨고르기 버튼에 닿을 만큼 넓거나, 사이 톱니가 두 톱니에 맞물린 채 조작대 가장자리 위로 다 보이는 간격(기본 크기에서 약 193)보다 넓으면 저절로 좁힙니다.")]
        private float skillGearPitch = LegacySkillGear.DefaultPitch;
        [SerializeField, Range(1, 8), Tooltip("기술 자리 한 칸(60°)마다의 이빨 수. 톱니 전체의 이빨은 그 6배입니다. 그래서 한 칸을 돌아도 이빨 자리가 같아, 멈추면 늘 사이 톱니와 맞물려 있습니다.")]
        private int skillGearTeethPerSlot = LegacySkillGear.DefaultTeethPerSlot;
        [SerializeField, Range(4f, 18f), Tooltip("이빨의 길이(HUD 단위). 기술 톱니와 사이 톱니가 같은 크기의 이빨을 씁니다.")]
        private float skillGearToothDepth = LegacySkillGear.DefaultToothDepth;
        [SerializeField, Range(0f, 40f), Tooltip("열린 두 열 사이의 작은 사이 톱니의 반지름(HUD 단위). 두 기술 톱니에 맞물리는 높이에 저절로 놓이고, 넘기기 때 반대 방향으로 돌아 모든 기술 톱니가 같은 방향으로 돌게 합니다. 0이면 그리지 않습니다.")]
        private float skillGearIdlerRadius = LegacySkillGear.DefaultIdlerRadius;
        [SerializeField, Range(0f, 1f), Tooltip("기술을 예약할 때(키·클릭·숫자 키) 그 열의 톱니가 한 칸(60°) 도는 시간(실제 초). 0이면 바로 넘어갑니다.")]
        private float skillGearTurnSeconds = LegacySkillGear.DefaultTurnSeconds;
        [SerializeField, Range(0f, 1.5f), Tooltip("넘기기(Shift)로 열린 모든 톱니가 함께 한 칸 도는 시간(실제 초). 끝에서 멈춤쇠에 걸리듯 살짝 튕깁니다. 0이면 바로 넘어갑니다.")]
        private float skillGearShiftSeconds = LegacySkillGear.DefaultShiftSeconds;
        [SerializeField, Range(0f, 12f), Tooltip("넘기기의 끝에서 톱니가 한 칸을 지나쳤다가 돌아오는 각도(도). 0이면 튕기지 않습니다.")]
        private float skillGearRatchetBounce = LegacySkillGear.DefaultRatchetBounce;
        [SerializeField, Range(0f, 1f), Tooltip("예약으로 톱니 하나가 돌 때의 짧은 딸깍 소리(Resources/Sfx/gear-tick) 음량. 예약의 선택음과 함께 납니다. 0이면 끕니다.")]
        private float gearTickVolume = LegacySkillGear.DefaultTickVolume;
        [SerializeField, Range(0f, 1f), Tooltip("넘기기로 톱니들이 함께 돌 때의 드르륵 소리(Resources/Sfx/gear-ratchet) 음량. 0이면 끕니다.")]
        private float gearRatchetVolume = LegacySkillGear.DefaultRatchetVolume;

        [Header("맞물림 — 위력은 다음 전투부터, 연출은 다음 물림부터 적용 (Docs/Meshing.md)")]
        [SerializeField, Range(0, 100), Tooltip("맞물림 사슬의 기술 하나가 사슬 길이마다 받는 위력(%). 길이 N인 사슬의 기술은 모두 N배를 받습니다(10이면 사슬 2는 +20%, 사슬 3은 +30%). 연출 값이 아니라 전투 규칙 값으로, 새 전투(임무·스테이지·수련)를 만들 때 넘깁니다. 0이면 맞물림이 꺼져 아무것도 맞물리지 않고 스텝도 막지 않습니다.")]
        private int meshPercent = LegacyMeshing.DefaultPercent;
        [SerializeField, Range(0f, 1.5f), Tooltip("예약으로 사슬이 생기거나 길어질 때, 플레이어 대기열의 두 아이콘 사이에서 작은 놋쇠 톱니 한 쌍이 맞물려 돌고 불꽃이 튀는 시간(실제 초). 0이면 끕니다. 아이콘 아래의 톱니 표시와 보너스 수치는 그대로 남습니다.")]
        private float meshBurstSeconds = LegacyMeshCue.DefaultBurstSeconds;
        [SerializeField, Range(16f, 48f), Tooltip("대기열에서 맞물리는 톱니 하나의 지름(HUD 단위, 1920x1080 기준). 아이콘 칸은 64입니다. 사슬이 길수록 조금 더 커집니다.")]
        private float meshGearSize = LegacyMeshCue.DefaultGearSize;
        [SerializeField, Range(0, 24), Tooltip("사슬 2일 때 튀는 불꽃 수. 사슬이 하나 길어질 때마다 4개씩 늘고(최대 40) 불꽃이 닿는 거리도 조금 늘어납니다. 첫 타격의 톱니 섬광도 같은 수를 씁니다. 0이면 불꽃을 끕니다.")]
        private int meshSparkCount = LegacyMeshCue.DefaultSparkCount;
        [SerializeField, Range(0f, 1f), Tooltip("톱니가 맞물리는 소리(Resources/Sfx/mesh-spark) 음량. 사슬이 길수록 조금 높게 납니다. 맞물린 칸의 첫 타격에는 이 음량의 절반으로 한 번 더 납니다. 0이면 끕니다.")]
        private float meshSoundVolume = LegacyMeshCue.DefaultVolume;
        [SerializeField, Range(0f, 1.5f), Tooltip("맞물린 칸의 첫 타격에 엘리사의 몸 앞에서 톱니 한 쌍이 번쩍이며 도는 시간(실제 초). 그 칸에는 스텝 원 대신 이것이 뜹니다. 0이면 끕니다.")]
        private float meshFlashSeconds = LegacyMeshCue.DefaultFlashSeconds;
        [SerializeField, Range(32f, 160f), Tooltip("첫 타격의 톱니 섬광에서 톱니 하나의 지름(HUD 단위). 불꽃은 이 크기에 맞춰 멀리 튑니다.")]
        private float meshFlashSize = LegacyMeshCue.DefaultFlashSize;

        [Header("길게 눌러 기술 설명 — 실행 중 즉시 적용 (Q/W/E 키·톱니 창 누르기)")]
        [SerializeField, Range(.05f, .5f), Tooltip("Q/W/E(또는 톱니 창)를 누른 지 이 시간(실제 초) 안에 떼면 짧게 누른 것으로 보고 그 열의 기술을 예약합니다. 더 오래 누르다 떼면 예약하지 않습니다.")]
        private float laneTapSeconds = LegacyLaneHold.DefaultTapSeconds;
        [SerializeField, Range(.1f, 1.5f), Tooltip("이 시간(실제 초)만큼 누르고 있으면 그 열의 지금 기술 설명이 열립니다. 홀드 진행선은 짧게 누르기 시간부터 이 시간까지 찹니다. 짧게 누르기 시간보다 짧게 두면 그 시간에 엽니다. 설명이 열린 뒤 떼면 예약하지 않고 닫습니다.")]
        private float explanationHoldSeconds = LegacyLaneHold.DefaultExplainSeconds;
        [SerializeField, Range(.05f, 1f), Tooltip("기술 설명을 읽는 동안 편성 시간이 흐르는 배율. 전투에서는 불릿타임에 다가가는 움직임도 같은 배율로 느려집니다(Tab 확인이 0.2배로 느리게 하듯). 편성 밖에서는 느려지지 않습니다. 1이면 느려지지 않습니다.")]
        private float explanationTimeScale = LegacyLaneHold.DefaultExplanationTimeScale;

        [Header("편성 예상 — 실행 중 즉시 적용 (Docs/SlotForecast.md)")]
        [SerializeField, Tooltip("편성 중 내 큐 카드마다 같은 칸의 상대 기술과 맞붙었을 때의 예상을 작은 표식(유리 ▲ 녹색 · 불리 ▼ 적색 · 비슷 = 회색)으로 보여 주고, 길게 눌러 연 기술 설명과 Tab 확인 카드에 '예상: 상대 저항 -12 · 내 저항 -6' 같은 한 줄을 더합니다. 실제 전투 규칙으로 지금 편성을 복사본에서 끝까지 풀어 본 값입니다(위력은 범위의 평균, 스텝 없음, 버프·맞물림·반격 포함). 끄면 표식과 줄이 모두 사라집니다.")]
        private bool slotForecastEnabled = true;
        [SerializeField, Tooltip("온라인 대전에서도 편성 예상을 보여 줍니다. 두 사람의 큐가 모두 보이므로 같은 방식으로 계산합니다. 끄면 대전에서만 숨깁니다.")]
        private bool slotForecastInVersus = true;
        [SerializeField, Range(0, 100), Tooltip("예상 판정에서 저항 1이 체력 1의 몇 %로 치는지. 50이면 저항 2 = 체력 1. 판정 = (상대가 잃는 체력 − 내가 잃는 체력) + 저항 차이 × 이 비율 ± 붕괴 가치.")]
        private int slotForecastResistanceWeight = LegacySlotForecast.DefaultResistanceWeightPercent;
        [SerializeField, Range(0, 30), Tooltip("그 칸에서 상대(또는 내) 저항이 붕괴하면 체력 몇만큼으로 치는지. 붕괴는 그 턴과 다음 턴의 체력 피해를 두 배로 만들므로 따로 더합니다. 0이면 붕괴를 따로 치지 않습니다.")]
        private int slotForecastBreakBonus = LegacySlotForecast.DefaultBreakBonus;
        [SerializeField, Range(0, 30), Tooltip("판정 값이 체력 이만큼 이상 앞서면 유리, 이만큼 이상 뒤지면 불리, 그 사이는 비슷. 그 칸에서 승부가 나면 이 값과 관계없이 유리·불리입니다.")]
        private int slotForecastMargin = LegacySlotForecast.DefaultMargin;

        [Header("전투 큐 패널 — 실행 중 즉시 적용 (조작대 위 양쪽 큐, Docs/CompactHudDesign.md)")]
        [SerializeField, Range(360f, 760f), Tooltip("조작대 위 양쪽 큐 패널 하나의 너비(1920x1080 기준 HUD 단위). 왼쪽은 내 큐(대전에서는 보는 사람의 큐), 오른쪽은 상대 큐입니다. 화면 끝(여백 16)을 넘지 않게 줄어듭니다. 기본 560은 카드 10장이 원래 크기로 들어갑니다. 긴 큐는 두 줄이 함께 최소 40까지 작아진 뒤, 현재 확인·예약·진행 중인 칸을 따라 같은 범위만 보여 줍니다.")]
        private float queuePanelWidth = LegacyQueuePanelLayout.DefaultPanelWidth;
        [SerializeField, Range(48f, 80f), Tooltip("큐 카드 한 장의 기본 크기(HUD 단위). 긴 큐는 두 줄이 함께 최소 40까지 작아지고, 그 뒤에는 읽을 수 있는 범위만 보여 줍니다. 카드에 붙은 표식(반격·맞물림·예상·순환·키워드)도 함께 작아집니다.")]
        private float queueCardSize = LegacyQueuePanelLayout.DefaultCardSize;
        [SerializeField, Range(48f, 104f), Tooltip("기본 크기에서 이웃한 큐 카드 중심 사이의 간격(HUD 단위). 카드가 작아지면 같은 비율로 줄고 카드 사이에는 최소 4를 남깁니다. 같은 논리 칸이 양쪽에서 늘 같은 거리에 마주 봅니다.")]
        private float queueCardSpacing = LegacyQueuePanelLayout.DefaultCardSpacing;
        [SerializeField, Range(0f, 240f), Tooltip("가운데 이음매에서 두 패널 사이의 간격(HUD 단위). 두 1번 칸이 이 간격을 사이에 두고 마주 봅니다. 코치 카드가 조작대 위에 펼쳐져 있는 동안은 카드를 가리지 않도록 저절로 넓어집니다.")]
        private float queueSeamGap = LegacyQueuePanelLayout.DefaultSeamGap;
        [SerializeField, Range(0f, .6f), Tooltip("한 칸이 끝날 때마다 두 큐가 가운데 쪽으로 한 칸 미끄러지는 시간(전투 시계 초). 불릿타임·슬로모션·히트 스톱을 따르고, 진행을 기다리게 하지 않습니다(다음 칸은 원래 시간에 시작합니다). 0이면 바로 옮겨집니다.")]
        private float queueSlideSeconds = LegacyQueuePanelLayout.DefaultSlideSeconds;
        [SerializeField, Range(0f, 40f), Tooltip("편성 중 패널 아래 가장자리와 조작대 위쪽 가장자리 사이의 간격(HUD 단위). 기본 8이면 조작대 윗변을 따라 도는 시계 톱니에 닿지 않습니다.")]
        private float queuePanelDockGap = LegacyQueuePanelLayout.DefaultDockGap;
        [SerializeField, Range(0f, 120f), Tooltip("진행 중(조작대가 접힌 동안) 패널 아래 가장자리의 높이(화면 아래에서 HUD 단위). 기본 46이면 아래 레터박스 띠(0~64) 위, 스텝 안내 줄(19~41) 바로 위에 앉습니다. 조작대가 접히고 펴질 때 함께 내려가고 올라옵니다.")]
        private float queuePanelResolvingBottom = LegacyQueuePanelLayout.DefaultResolvingBottom;
        [SerializeField, Tooltip("켜면 전장 카메라가 인물의 발을 큐 패널 위(아래 간격만큼 띄워)에 두도록, 필요한 만큼만 아래로 내려갑니다(복도 배경에서 편성 화면 최대 약 60, 진행 화면 약 45, 화면 기준 HUD 단위). 숲처럼 이미 충분히 높으면 움직이지 않고, 결정타 확대·스텝 집중·수훈 컷인은 자기 구도를 지킵니다. 끄면 카메라는 예전 그대로이고 패널이 발을 가릴 수 있습니다.")]
        private bool queuePanelKeepsFightersClear = true;
        [SerializeField, Range(0f, 60f), Tooltip("큐 패널 위쪽 가장자리와 인물의 발 사이에 남기는 간격(HUD 단위).")]
        private float queuePanelFighterClearance = LegacyQueuePanelLayout.DefaultFighterClearance;

        [Header("큐 짝 표시 — 실행 중 즉시 적용 (편성 중 조작대 위 큐 패널, Docs/CompactHudDesign.md)")]
        [SerializeField, Tooltip("편성 중 내가 지금 예약할 수 있을 때, 다음 예약이 마주칠 상대 큐 카드(같은 칸)에 놋쇠 테를 두르고 내 패널의 거울 자리에 옅은 빈 자리를 그립니다. 그 칸에 상대 카드가 없거나, 입력이 묶였거나(대전의 상대 차례), Tab으로 상대를 보는 동안에는 숨깁니다. 끄면 둘 다 그리지 않습니다.")]
        private bool queuePairCuesEnabled = true;
        [SerializeField, Range(0f, 1f), Tooltip("다음 상대 카드 테의 진하기. 아래 맥박 시간마다 이 값과 (1 − 맥박 깊이)배 사이를 오갑니다(기본 60%~100%). 0이면 테가 보이지 않습니다.")]
        private float nextOpponentRingAlpha = LegacyCombatHud.DefaultNextOpponentRingAlpha;
        [SerializeField, ColorUsage(false, false), Tooltip("다음 상대 카드 테의 색(기본: 놋쇠 강조색). 진하기는 위 값이 정합니다.")]
        private Color nextOpponentRingInk = DefaultNextOpponentRingInk;
        [SerializeField, Range(0f, 16f), Tooltip("테가 카드 바깥으로 둘러서는 여백(HUD 단위, 큐 카드 크기에 맞춰 커지고 작아짐).")]
        private float nextOpponentRingMargin = LegacyCombatHud.DefaultNextOpponentRingMargin;
        [SerializeField, Range(0f, 3f), Tooltip("다음 상대 카드 테가 한 번 숨 쉬는 시간(실제 초). 0이면 맥박 없이 그대로 둡니다.")]
        private float nextOpponentRingPulseSeconds = LegacyCombatHud.DefaultNextOpponentRingPulseSeconds;
        [SerializeField, Range(0f, 1f), Tooltip("테 맥박의 깊이. 가장 옅을 때 진하기의 (1 − 이 값)배까지 내려갑니다. 0이면 맥박 없이 그대로 둡니다.")]
        private float nextOpponentRingPulseDepth = LegacyCombatHud.DefaultNextOpponentRingPulseDepth;
        [SerializeField, Range(0f, 1f), Tooltip("내 패널에서 다음 예약이 들어갈 빈 자리(테를 두른 상대 카드의 거울 자리)의 진하기. 0이면 그리지 않습니다.")]
        private float nextReservationSocketAlpha = LegacyCombatHud.DefaultNextReservationSocketAlpha;
        [SerializeField, ColorUsage(true, false), Tooltip("빈 자리 안쪽을 채우는 색(테두리는 놋쇠 테두리색). 알파는 채움의 진하기이고, 위 진하기가 전체에 다시 곱해집니다.")]
        private Color nextReservationSocketInk = DefaultNextReservationSocketInk;
        [SerializeField, Range(0f, 1f), Tooltip("상대도 카드를 둔 칸에 내가 기술(숨고르기 포함)을 예약하면, 내 새 카드와 그 상대 카드에서 같은 순간 짧게 튀는 불꽃의 시간(실제 초). 두 카드를 잇는 선은 없습니다. 0이면 끕니다.")]
        private float reservationSparkSeconds = LegacyCombatHud.DefaultReservationSparkSeconds;
        [SerializeField, Range(12f, 60f), Tooltip("예약 불꽃의 크기(HUD 단위, 큐 카드 크기에 맞춰 커지고 작아짐). 불꽃은 이 크기의 1.6배까지 튑니다. 유리·불리에 따라 아래 배율을 곱합니다.")]
        private float reservationSparkSize = LegacyCombatHud.DefaultReservationSparkSize;
        [SerializeField, Range(0, 24), Tooltip("예약 불꽃 한 번에 튀는 불꽃 수(카드 하나당). 유리·불리 배율만큼 늘고 줄어듭니다. 0이면 끕니다.")]
        private int reservationSparkCount = LegacyCombatHud.DefaultReservationSparkCount;
        [SerializeField, ColorUsage(true, false), Tooltip("편성 예상이 유리(▲)인 칸의 예약 불꽃 색. 불꽃 머리는 이 색에서 흰빛 쪽으로 밝게, 꼬리는 이 색으로 사라집니다. 알파는 불꽃 전체의 진하기입니다.")]
        private Color reservationSparkFavourableInk = DefaultReservationSparkFavourableInk;
        [SerializeField, ColorUsage(true, false), Tooltip("편성 예상이 비슷(=)이거나 예상이 없는(꺼짐·계산 안 됨) 칸의 예약 불꽃 색.")]
        private Color reservationSparkEvenInk = DefaultReservationSparkEvenInk;
        [SerializeField, ColorUsage(true, false), Tooltip("편성 예상이 불리(▼)인 칸의 예약 불꽃 색. 기본은 차갑고 어두운 강철빛입니다.")]
        private Color reservationSparkUnfavourableInk = DefaultReservationSparkUnfavourableInk;
        [SerializeField, Range(1f, 2f), Tooltip("유리(▲)한 칸의 예약 불꽃 크기·수 배율.")]
        private float reservationSparkFavourableScale = LegacyCombatHud.DefaultReservationSparkFavourableScale;
        [SerializeField, Range(.3f, 1f), Tooltip("불리(▼)한 칸의 예약 불꽃 크기·수 배율.")]
        private float reservationSparkUnfavourableScale = LegacyCombatHud.DefaultReservationSparkUnfavourableScale;

        [Header("큐 합 연출 — 실행 중 즉시 적용 (진행 중 가운데 이음매의 카드 한 쌍, Docs/CompactHudDesign.md)")]
        [SerializeField, Tooltip("한 칸이 진행될 때 가운데 이음매의 내 카드와 상대 카드가 그 칸의 결과대로 움직입니다(합: 센 쪽이 밀고 약한 쪽이 튕김, 방어: 공격만 파고들고 방어는 버팀, 회피: 위로 비킴, 붕괴: 흔들림). 전투 시계를 따르고 진행을 기다리게 하지 않으며, 늘 제자리로 돌아옵니다. 끄면 카드는 예전처럼 제자리에 있습니다.")]
        private bool clashMotionEnabled = true;
        [SerializeField, Range(0f, .4f), Tooltip("칸이 시작될 때 공격 카드가 이음매 쪽으로 기우는 시간(전투 시계 초, 이 시간에 약 95%).")]
        private float clashApproachSeconds = LegacyClashPose.DefaultApproachSeconds;
        [SerializeField, Range(0f, 30f), Tooltip("첫 타격 전 공격 카드가 이음매 쪽으로 기우는 거리(HUD 단위). 맞닿을 자리를 넘지 않습니다.")]
        private float clashApproachDistance = LegacyClashPose.DefaultApproachDistance;
        [SerializeField, Range(.1f, 1f), Tooltip("첫 타격부터 두 카드가 맞닿고(이 시간의 14%) 밀고 튕긴 뒤(46%) 잠깐 버티고(56%) 제자리로 돌아오기까지의 시간(전투 시계 초). 칸이 먼저 끝나면 떠난 카드가 나머지를 마저 하며 사라집니다. 진행 시간은 바뀌지 않습니다.")]
        private float clashImpactSeconds = LegacyClashPose.DefaultImpactSeconds;
        [SerializeField, Range(0f, 160f), Tooltip("카드가 맞닿으러 가는 가장 먼 거리(HUD 단위). 합은 두 카드가 이음매에서, 방어 공격은 방어 카드 앞에서 만납니다. 코치 카드로 이음매가 넓어지면 이 거리까지만 갑니다.")]
        private float clashReach = LegacyClashPose.DefaultReach;
        [SerializeField, Range(0f, 40f), Tooltip("합에서 이긴 카드·방어를 뚫은 카드가 맞닿은 자리에서 더 밀고 들어가는 거리(HUD 단위, 차이가 클수록 이 값까지). 비긴 합의 튕김·막힌 공격의 되튐·빗나간 공격의 지나침도 이 값을 기준으로 합니다.")]
        private float clashPushDistance = LegacyClashPose.DefaultPushDistance;
        [SerializeField, Range(0f, 60f), Tooltip("합에서 진 카드·뚫린 방어 카드가 제자리 뒤로 밀려나는 거리(HUD 단위, 차이가 클수록 이 값까지, 가장 작게는 30%).")]
        private float clashKnockbackDistance = LegacyClashPose.DefaultKnockbackDistance;
        [SerializeField, Range(0f, 60f), Tooltip("회피한 쪽 카드가 공격을 비켜 위로 올라서는 거리(HUD 단위).")]
        private float clashSideStepDistance = LegacyClashPose.DefaultSideStepDistance;
        [SerializeField, Range(0f, 25f), Tooltip("밀려난 카드가 뒤로 젖혀지는 가장 큰 각도(도).")]
        private float clashTiltDegrees = LegacyClashPose.DefaultTiltDegrees;
        [SerializeField, Range(0f, 20f), Tooltip("이번 칸에서 저항이 붕괴한 쪽 카드가 흔들리는 폭(HUD 단위). 0이면 흔들지 않습니다.")]
        private float clashBreakShake = LegacyClashPose.DefaultBreakShake;
        [SerializeField, Range(0f, 1f), Tooltip("붕괴 흔들림이 잦아드는 시간(전투 시계 초).")]
        private float clashBreakShakeSeconds = LegacyClashPose.DefaultBreakShakeSeconds;
        [SerializeField, Range(0f, 12f), Tooltip("공격이 없는 칸(방어끼리, 방어·숨고르기 대 빈칸)의 카드가 살짝 내려앉는 거리(HUD 단위). 0이면 움직이지 않습니다.")]
        private float clashSettleDip = LegacyClashPose.SettleDip;
        [SerializeField, Range(0f, .3f), Tooltip("공격을 받는 방어 카드가 순간 움츠러드는 비율(0.06 = 6% 작아짐). 뚫린 방어와 내려앉는 카드는 그 절반입니다. 카드는 쉬는 크기보다 커지지 않습니다.")]
        private float clashBraceSquash = -LegacyClashPose.BraceSquash;
        [SerializeField, Range(0f, 1f), Tooltip("두 카드가 맞닿는 순간(합·방어) 맞닿은 자리에서 한 번 튀는 불꽃의 시간(전투 시계 초). 0이면 끕니다.")]
        private float clashSparkSeconds = LegacyCombatHud.DefaultClashSparkSeconds;
        [SerializeField, Range(12f, 80f), Tooltip("맞닿는 불꽃의 크기(HUD 단위, 큐 카드 크기에 맞춰 커지고 작아짐). 한쪽이 크게 앞설수록 1.2배까지 커집니다.")]
        private float clashSparkSize = LegacyCombatHud.DefaultClashSparkSize;
        [SerializeField, Range(0, 24), Tooltip("맞닿는 불꽃의 수. 0이면 끕니다.")]
        private int clashSparkCount = LegacyCombatHud.DefaultClashSparkCount;
        [SerializeField, ColorUsage(true, false), Tooltip("맞닿는 불꽃 머리의 색(기본: 맞물림 불꽃과 같은 흰 금빛). 알파는 머리의 진하기입니다.")]
        private Color clashSparkHeadInk = DefaultClashSparkHeadInk;
        [SerializeField, ColorUsage(true, false), Tooltip("맞닿는 불꽃 꼬리의 색(기본: 주황, 알파 0이라 꼬리는 머리 쪽으로 사라짐). 알파를 올리면 꼬리가 그 색으로 남습니다.")]
        private Color clashSparkTailInk = DefaultClashSparkTailInk;

        [Header("연출 감독 — 칸의 무게에 따른 속도·강조 (켜기·속도·히트 스톱·상한은 다음 턴부터, 카메라·타격음·이음매 배율은 바로; Docs/PresentationTuning.md)")]
        [SerializeField, Tooltip("진행 중 칸마다 무게(Minor·Normal·Major·Climax, 아래 '칸 등급 기준')를 매겨 Minor 칸은 빨리 넘기고 Major 칸은 카메라를 당기고 히트 스톱을 길게 하며, 위력이 비슷한 합은 팽팽한 합으로 붙잡습니다. 첫 타격 전(스텝 판정 구간)의 시간은 건드리지 않고, 판정도 바꾸지 않습니다. 끄면 칸의 빠르기(Minor 칸·일방 공격 가속), 히트 스톱, 팽팽한 합, Major 칸의 카메라 당김·겹친 타격음·이음매 강조가 예전 그대로입니다(등급은 계속 매깁니다). 간파 신호와 시그니처 기술의 예고 빛·잔상·카메라 당김은 각자의 스위치(간파 신호의 Read Cue Enabled, 시그니처 기술의 Signature Treatment Enabled)로 끄고, 긴장 조명도 자신의 스위치를 따릅니다.")]
        private bool showDirectorEnabled = true;
        [SerializeField, Range(1f, 4f), Tooltip("Minor 칸(양쪽 모두 공격이 없는 칸: 방어끼리·숨고르기·빈칸)의 첫 타격 뒤 남은 시간과 그 뒤 다음 칸까지의 간격을 이 배만큼 짧게 합니다. 줄인 시간은 그 턴의 Major 강조에 씁니다. 1이면 그대로입니다.")]
        private float minorSlotSpeed = LegacyShowPacingTuning.DefaultMinorSpeed;
        [SerializeField, Range(1f, 6f), Tooltip("Major 칸의 첫 타격(맞은 타격이 있을 때)의 히트 스톱을 위 '히트 스톱' 값의 이 배로 늘립니다(기본 0.04초 × 3 = 0.12초). 한 칸에 한 번이며, 마무리 일격(Climax)은 기존 마무리 연출 그대로입니다. 1이면 늘리지 않습니다.")]
        private float majorHitStopScale = LegacyShowPacingTuning.DefaultMajorHitStopScale;
        [SerializeField, Range(0f, .3f), Tooltip("Major 칸이 진행되는 동안(칸 시작에 이미 Major로 알 수 있으면 예고 동안부터) 교전 카메라가 다가가는 양(화면 크기에 대한 비율, 0.1 = 10%). 실제 시간으로 부드럽게 들어오고 빠지며, 결정타 집중 카메라가 켜지면 그쪽을 따릅니다. 0이면 끕니다.")]
        private float majorCameraPush = .1f;
        [SerializeField, Range(0f, 1f), Tooltip("Major 칸의 첫 타격에 겹쳐 울리는 두 번째 타격음의 음량(낮은 음). 결정타의 크리티컬 효과음이 이미 울리는 타격에는 겹치지 않습니다. 0이면 끕니다.")]
        private float majorImpactVolume = .35f;
        [SerializeField, Range(1f, 2f), Tooltip("Major 칸에서 큐 합 연출(가운데 이음매의 카드 한 쌍)의 기울기·밀기·밀려남·비킴 거리와 맞닿는 불꽃 크기에 곱하는 배율. 시간은 바꾸지 않습니다. 한 쌍이 맞부딪치기 시작한 뒤에야 Major가 된 칸(두 번째 타격의 붕괴 등)은 움직이던 카드가 튀지 않도록 그 쌍에 배율을 더하지 않습니다. 1이면 그대로입니다.")]
        private float majorClashScale = 1.35f;
        [SerializeField, Range(0f, 5f), Tooltip("연출 감독이 한 턴에 더할 수 있는 시간의 상한(실제 초). Major 히트 스톱과 팽팽한 합이 쓰는 시간은 '이 값 + 그 턴의 Minor 칸이 줄인 시간'을 넘지 않습니다. 그래서 한 턴은 감독이 없을 때보다 이 값 이상 길어지지 않습니다. 0이면 Minor 칸이 줄인 만큼만 씁니다.")]
        private float turnExtraSecondsCap = LegacyShowPacingTuning.DefaultTurnExtraCap;

        [Header("칸 등급 기준 — 다음 턴부터 적용 (연출 감독이 씀; 큰 체력 피해는 위 '결정타 연출' 비율)")]
        [SerializeField, Range(0f, 1f), Tooltip("합(공격 대 공격)에서 위력 차이의 비율(|내 위력 − 상대 위력| ÷ 큰 쪽)이 이 값 이상이면 Major. 누가 이겼는지는 보지 않고 차이의 크기만 봅니다. 0이면 이 조건을 끕니다.")]
        private float largeClashGap = LegacySlotImportanceTuning.DefaultLargeClashGap;
        [SerializeField, Range(0f, 1f), Tooltip("공격이 방어를 뚫은 몫(공격 중 방어를 지난 비율)이 이 값 이상이면 Major. 0이면 끕니다.")]
        private float deepPierce = LegacySlotImportanceTuning.DefaultDeepPierce;
        [SerializeField, Range(0, 100), Tooltip("상대가 아무것도 없거나 숨고르기인 칸에 들어간 공격의 전체 위력이 대상 최대 체력의 이 비율(%) 이상이면 Major. 0이면 끕니다.")]
        private int bigUnopposedPercent = LegacySlotImportanceTuning.DefaultBigUnopposedPercent;
        [SerializeField, Range(0, 7), Tooltip("맞물림 사슬의 이 번째 기술부터 그 칸을 플레이어의 간파로 보고 Major로 올립니다(2면 사슬의 두 번째 기술부터). 0이면 끕니다.")]
        private int meshChainMinimum = LegacySlotImportanceTuning.DefaultMeshChainMinimum;
        [SerializeField, Tooltip("회피(스텝 회피·자동 회피)한 칸을 Major로 올립니다. 끄면 점수만 더합니다.")]
        private bool dodgeRaisesToMajor = true;
        [SerializeField, Tooltip("상대의 간파(상대 기술의 조건 충족·붕괴)도 플레이어의 간파처럼 칸을 Major로 올립니다. 끄면 점수만 더합니다. 온라인 대전은 두 화면의 연출 길이가 같아야 하므로 이 값과 관계없이 늘 켠 것처럼 셉니다.")]
        private bool enemyReadsRaiseTier;

        [Header("팽팽한 합 — 기준·시간은 다음 턴부터, 흔들림·기댐·튕김·불꽃은 다음 합부터 (위력이 비슷한 공격끼리의 합)")]
        [SerializeField, Range(0f, .5f), Tooltip("합(공격 대 공격)에서 위력 차이의 비율이 이 값 이하이면 팽팽한 합입니다. 첫 타격에 두 칼이 맞닿고 둘 다 몸에 피해가 들어가지 않았으면, 두 사람이 맞닿은 자세 그대로 함께 밀고 밀리다 불꽃을 튀기고 똑같이 튕겨 납니다(둘 다 피해를 입었으니 이긴 쪽이 없습니다). 0이면 위력이 똑같을 때만입니다.")]
        private float bladeBindGap = LegacySlotImportanceTuning.DefaultBladeBindGap;
        [SerializeField, Range(0f, 2f), Tooltip("팽팽한 합이 두 사람을 붙잡는 시간(실제 초). 그동안 전투 시계는 멈춥니다(히트 스톱처럼). 위 상한에서 이 시간의 절반도 남지 않았으면 그 턴에는 붙잡지 않고, 남은 만큼만 붙잡습니다. 0이면 끕니다.")]
        private float bladeBindSeconds = LegacyBladeBind.DefaultSeconds;
        [SerializeField, Range(0f, .6f), Tooltip("맞붙은 두 사람이 함께 한쪽으로 밀렸다 다른 쪽으로 밀리는 폭(월드 단위). 양쪽으로 같은 만큼 흔들려 어느 쪽도 밀어내지 못합니다.")]
        private float bladeBindSway = LegacyBladeBind.DefaultSway;
        [SerializeField, Range(0f, .4f), Tooltip("팽팽한 합 동안 두 사람이 서로에게 기대는 거리(한 사람당, 월드 단위). 둘이 같은 만큼 기댑니다.")]
        private float bladeBindPress = LegacyBladeBind.DefaultPress;
        [SerializeField, Range(0f, 1.5f), Tooltip("팽팽한 합이 끝나면 두 사람은 그 타격의 원래 밀림을 똑같이 나눠 같은 거리만큼 튕겨 납니다(전투 시계가 다시 흐를 때). 이 값은 그 위에 한 사람당 더하는 거리(월드 단위)입니다. 0이면 더하지 않아 다음 타격까지 다가가는 시간도 늘지 않습니다.")]
        private float bladeBindSpring = LegacyBladeBind.DefaultSpring;
        [SerializeField, Range(0, 6), Tooltip("팽팽한 합 동안 맞닿은 칼 사이에서 튀는 불꽃(타격 빛)의 수. 이음매의 두 카드도 같은 때 불꽃을 튀깁니다. 0이면 첫 타격의 불꽃만 남습니다.")]
        private int bladeBindSparks = LegacyBladeBind.DefaultSparks;

        [Header("전투 후반 가속 — 다음 턴부터 적용")]
        [SerializeField, Tooltip("한 턴의 큐를 뒤로 진행할수록 교전·이동·스킬 사이 간격을 점점 빠르게 재생합니다. 다음 턴에는 원래 속도로 돌아옵니다. 한쪽 큐가 소진되면 아래 설정으로 추가 배속과 스킬 사이 대기를 조정합니다. 회피·압박의 입력 구간과 실제 시간의 히트 스톱·슬로모션·마무리 연출은 유지합니다. 끄면 기존 일방 공격 가속을 사용합니다.")]
        private bool combatTempoEnabled = true;
        [SerializeField, Range(0f, .5f), Tooltip("첫 칸 이후 한 칸마다 더하는 재생 배속. 기본 0.1: 1배, 1.1배, 1.2배… 한쪽 큐가 소진되어도 이 증가를 유지합니다.")]
        private float combatTempoStep = LegacyCombatTempoTuning.DefaultStepPerSlot;
        [SerializeField, Range(1f, 3f), Tooltip("턴 후반 가속의 최대 배속. 기본 1.8배. 한쪽 큐가 소진되었을 때의 추가 배속은 이 상한을 적용한 뒤 곱합니다.")]
        private float combatTempoMaxSpeed = LegacyCombatTempoTuning.DefaultMaximumSpeed;
        [SerializeField, Range(1f, 3f), Tooltip("한쪽에 남은 스킬이 없을 때 턴 후반 배속에 추가로 곱하는 값. 기본 1배로 추가 통배속을 주지 않습니다. 2로 바꾸면 이전의 소진 시 2배 배속을 비교할 수 있습니다. 누가 큐를 먼저 소진했든 같습니다.")]
        private float exhaustedSideSpeed = LegacyCombatTempoTuning.DefaultExhaustedSpeed;
        [SerializeField, Range(0f, 1f), Tooltip("한쪽 큐가 비고 남은 쪽이 공격한 뒤, 다음 스킬까지 기다리는 시간의 비율. 기본 0.25: 0.28초 대기가 0.07초가 됩니다. 1이면 기존 대기 시간으로 비교할 수 있습니다. 숨고르기·방어는 빈칸으로 세지 않으며 연타 간격·공격 동작·입력 구간은 그대로입니다.")]
        private float exhaustedGapScale = LegacyCombatTempoTuning.DefaultExhaustedGapScale;

        [Header("기존 일방 공격 가속 — 후반 가속을 끈 경우에 적용")]
        [SerializeField, Tooltip("한쪽만 공격하고 다른 쪽은 빈칸이나 숨고르기인 칸(일방 공격)이 한 턴에 이어지면 그 칸들을 점점 빠르게 재생합니다(누가 공격하든 같음). 양쪽 모두 공격이 없는 칸(방어끼리·숨고르기끼리)은 이어짐을 끊지도 늘리지도 않고, 합·방어·회피·반격처럼 양쪽이 맞서는 칸은 처음부터 다시 셉니다. Major·Climax 칸(붕괴·큰 피해·큰 일방 공격·간파·시그니처·마무리 일격)은 빨라지지 않지만 이어짐은 계속 셉니다. 스텝(회피·압박)을 쓸 수 있는 칸은 첫 타격 전(예고와 성공 구간)을 건드리지 않고 첫 타격 뒤와 그 뒤 간격만 빠르게 합니다. 줄인 시간은 강조에 쓰지 않고 턴이 그만큼 짧아집니다. 연출 감독을 끄면 이것도 꺼집니다.")]
        private bool unopposedStreakEnabled = true;
        [SerializeField, Range(1, 8), Tooltip("일방 공격이 몇 칸째 이어질 때부터 빨라지는지. 2면 두 번째 칸부터 빨라집니다(첫 칸은 그대로).")]
        private int unopposedStreakStartLength = LegacyStreakTuning.DefaultStartLength;
        [SerializeField, Range(0f, .5f), Tooltip("이어지는 일방 공격 한 칸마다 더 빨라지는 양(재생 배속). 기본 0.15: 1.15배, 1.3배, 1.45배… 0이면 빨라지지 않습니다.")]
        private float unopposedStreakStep = LegacyStreakTuning.DefaultStepPerSlot;
        [SerializeField, Range(1f, 3f), Tooltip("일방 공격이 아무리 이어져도 넘지 않는 가장 빠른 재생 배속. 기본 1.6배. 1이면 빨라지지 않습니다.")]
        private float unopposedStreakMaxSpeed = LegacyStreakTuning.DefaultMaximumSpeed;

        [Header("간파 신호 — 실행 중 즉시 적용 (내 간파: 상대 조건 충족·붕괴·맞물림 사슬, 위 '칸 등급 기준')")]
        [SerializeField, Tooltip("내 간파가 맞아떨어진 칸의 첫 타격(또는 붕괴를 낸 타격)에, 이음매의 내 카드에서 금빛 불꽃이 터지고 테가 퍼지며, 내 캐릭터 가슴께가 잠깐 금빛으로 밝아집니다. 칸마다 한 번이고, 상대의 간파에는 없습니다. 시간을 쓰지 않습니다. 끄면 아무것도 나오지 않습니다(칸의 등급은 그대로).")]
        private bool readCueEnabled = true;
        [SerializeField, Tooltip("간파 신호와 함께 이음매의 내 카드 위에 작은 '간파' 도장을 잠깐 찍습니다. 기술 이름이나 효과(기존 'OO 성공!' 신호)는 되풀이하지 않습니다. 끄면 불꽃과 빛만 나옵니다.")]
        private bool readStampEnabled = true;
        [SerializeField, Range(.3f, 2f), Tooltip("'간파' 도장이 떠 있는 시간(실제 초). 처음 0.12초에 작게 튀어나왔다가 마지막 0.3초에 사라집니다. 불꽃과 테는 이 시간의 절반쯤(최대 0.5초)입니다.")]
        private float readCueSeconds = .9f;
        [SerializeField, ColorUsage(false), Tooltip("간파 신호의 색(불꽃·테·도장·캐릭터의 빛). 기본은 밝은 금빛입니다.")]
        private Color readCueInk = DefaultReadCueInk;
        [SerializeField, Range(0, 24), Tooltip("간파 불꽃 줄기의 수. 0이면 불꽃을 끄고 테와 도장만 남깁니다.")]
        private int readSparkCount = 12;
        [SerializeField, Range(12f, 80f), Tooltip("간파 불꽃이 퍼지는 크기(1920x1080 기준 HUD 단위, 카드 크기 설정에 맞춰 함께 커짐).")]
        private float readSparkSize = 40f;
        [SerializeField, Tooltip("간파 때 내 캐릭터 가슴께가 잠깐 금빛으로 밝아집니다(전투 시계로 약 0.35초, 히트 스톱 동안은 밝은 채 머묾). 끄면 화면 아래 신호만 나옵니다.")]
        private bool readFighterAccent = true;

        [Header("시그니처 기술 — 목록과 히트 스톱은 다음 턴부터, 그 밖은 바로(빛·잔상은 다음 칸부터) (어느 쪽이 쓰든, 대전에서도 같은 길이)")]
        [SerializeField, Tooltip("시그니처 기술(정수 기술)의 시트 번호. 어느 쪽이 쓰든 그 칸은 Major이고 아래 연출을 받습니다. 기본: 51 에스토카다, 53 카덴스, 56 두블레. 비우면 시그니처 기술이 없습니다.")]
        private int[] signatureSkillIds = { 51, 53, 56 };
        [SerializeField, Tooltip("시그니처 기술의 연출: 예고 동안 기술 열의 색(정공 금·강공 청록·기교 적갈)으로 힘이 가슴께에 모였다가 타격에 번쩍이고, 공격 동작에 같은 색의 잔상이 남으며, 카메라가 더 다가가고, 첫 타격의 히트 스톱이 더 길고, 타격음이 한 겹 더 울립니다. 끄면 다른 Major 칸과 같습니다(등급은 그대로).")]
        private bool signatureTreatmentEnabled = true;
        [SerializeField, Range(1f, 6f), Tooltip("시그니처 기술 칸의 첫 타격 히트 스톱 배율(위 '히트 스톱' 값의 배수, Major의 배율보다 작지 않음). 기본 4.5배 = 0.18초. 늘어난 시간은 연출 감독의 턴 상한 안에서만 씁니다.")]
        private float signatureHitStopScale = LegacyShowPacingTuning.DefaultSignatureHitStopScale;
        [SerializeField, Range(0f, .3f), Tooltip("시그니처 기술 칸 동안(예고부터) 교전 카메라가 다가가는 양(화면 크기에 대한 비율). Major 당김보다 크면 이 값을 씁니다. 0이면 Major 당김만 씁니다.")]
        private float signatureCameraPush = .16f;
        [SerializeField, Range(0f, 1f), Tooltip("시그니처 기술 칸의 첫 타격에 한 겹 더 울리는 칼날 울림(막기 소리)의 음량. 0이면 끕니다.")]
        private float signatureImpactVolume = .45f;
        [SerializeField, Tooltip("시그니처 기술의 예고 동안 기술 열의 색으로 힘이 모였다가 첫 타격에 번쩍입니다(코드로 그린 빛, 새 그림 없음).")]
        private bool signatureWindupGlow = true;
        [SerializeField, Range(0f, 1f), Tooltip("시그니처 기술의 공격 동작이 남기는 잔상 하나가 사라지는 시간(실제 초). 0이면 잔상을 끕니다.")]
        private float signatureAfterimageSeconds = .3f;
        [SerializeField, Range(0f, 1f), Tooltip("시그니처 잔상이 처음 남을 때의 불투명도.")]
        private float signatureAfterimageAlpha = .55f;

        [Header("긴장 조명 — 실행 중 즉시 적용 (조명만: 판정·시간·스텝과 무관, 수련에는 없음)")]
        [SerializeField, Tooltip("어느 쪽이든 체력이 아래 비율 이하로 떨어지면 배경·화면이 어두워지고 가장자리가 짙은 불씨 색으로 깊어집니다. 내 체력 30% 이하에서는 붉은 가장자리가 심장처럼 맥동하며, 낮을수록 강하고 빨라집니다. 대전의 맥동은 각자 자신의 체력을 따릅니다. 회복·전투 종료·재시작에 걷히고, 끄면 긴장 조명과 저체력 맥동이 함께 꺼집니다(아래 흐름의 빛과는 따로).")]
        private bool tensionLightingEnabled = true;
        [SerializeField, Range(0, 99), Tooltip("긴장 조명이 켜지는 체력 비율(%, 최대 체력 기준). 내 체력이나 상대 체력 중 하나라도 이 비율 이하이면 켜집니다. 0이면 끕니다.")]
        private int tensionHealthPercent = LegacyTensionTuning.DefaultHealthPercent;
        [SerializeField, Range(0f, 3f), Tooltip("긴장 조명이 다 들어오는 시간(실제 초). 히트 스톱·슬로모션에 늘어나지 않습니다. 0이면 바로 바뀝니다.")]
        private float tensionEaseInSeconds = LegacyTensionTuning.DefaultEaseInSeconds;
        [SerializeField, Range(0f, 3f), Tooltip("회복해 비율을 넘었을 때 긴장 조명이 다 빠지는 시간(실제 초). 다시 시작·로비로 가면 바로 빠집니다. 0이면 바로 돌아옵니다.")]
        private float tensionEaseOutSeconds = LegacyTensionTuning.DefaultEaseOutSeconds;
        [SerializeField, Range(0f, .85f), Tooltip("긴장 조명에서 배경과 하늘이 어두워지는 양(스텝 성공의 배경 암전과 같은 방식, 겹치면 곱해짐). 인물·효과·HUD는 어두워지지 않습니다. 0이면 끕니다.")]
        private float tensionBackdropDarkening = DuelTensionLook.DefaultBackdropDarkening;
        [SerializeField, Range(0f, 1f), Tooltip("긴장 조명에서 화면 전체의 노출을 낮추는 양(EV). 작게 둘수록 인물이 잘 보입니다. 0이면 끕니다.")]
        private float tensionDimming = DuelTensionLook.DefaultDimming;
        [SerializeField, Range(0f, .4f), Tooltip("긴장 조명에서 가장자리(비네트)를 더 어둡게 하는 양(기본 비네트 0.4에 더함). 0이면 끕니다.")]
        private float tensionVignette = DuelTensionLook.DefaultVignette;
        [SerializeField, ColorUsage(false), Tooltip("긴장 조명에서 가장자리가 물드는 색(기본: 짙은 불씨 색). 검정이면 색 없이 어두워지기만 합니다.")]
        private Color tensionVignetteInk = DuelTensionLook.DefaultVignetteInk;
        [SerializeField, Tooltip("흐름이 바뀔 때(내가 체력 비율로 뒤지다 앞서거나, 상대 저항을 붕괴시킬 때) 화면이 잠깐 밝고 따뜻해집니다(노출이 오르고 가장자리가 열리며 금빛이 돎). 시간을 쓰지 않고 진행을 멈추지 않습니다. 누가 쓰러진 순간(마무리 일격)에는 마무리 연출이 빛을 맡으므로 없습니다. 대전에서는 각 화면이 자기 플레이어의 흐름에만 냅니다. 끄면 없습니다.")]
        private bool flowSwellEnabled = true;
        [SerializeField, Tooltip("상대 저항을 붕괴시킨 순간에도 흐름의 빛을 냅니다. 끄면 뒤지다 앞설 때만 냅니다.")]
        private bool flowSwellOnBreak = true;
        [SerializeField, Range(0, 30), Tooltip("앞섬과 뒤짐을 가르는 체력 비율의 차이(퍼센트포인트). 두 사람의 체력 비율(각자 최대 체력 기준) 차이가 이 값보다 커야 앞서거나 뒤진 것으로 봅니다. 그 사이는 비김이라, 아주 작은 차이로 오가는 엎치락뒤치락마다 빛나지 않습니다. 0이면 조금이라도 앞서면 앞선 것입니다.")]
        private int flowLeadMarginPercent = LegacyTensionTuning.DefaultLeadMarginPercent;
        [SerializeField, Range(0f, 1f), Tooltip("흐름의 빛이 가장 밝을 때 노출이 오르는 양(EV). 0이면 밝아지지 않습니다.")]
        private float flowSwellExposure = DuelTensionLook.DefaultSwellExposure;
        [SerializeField, Range(0f, .4f), Tooltip("흐름의 빛이 가장 밝을 때 가장자리(비네트)가 열리는 양. 0이면 그대로입니다.")]
        private float flowSwellVignetteRelief = DuelTensionLook.DefaultSwellVignetteRelief;
        [SerializeField, Range(0f, 1f), Tooltip("흐름의 빛이 가장 밝을 때 화면에 도는 금빛의 세기. 0이면 색은 그대로입니다.")]
        private float flowSwellWarmTint = DuelTensionLook.DefaultSwellWarmTint;
        [SerializeField, Range(0f, 3f), Tooltip("흐름의 빛이 차오르는 시간(실제 초).")]
        private float flowSwellRiseSeconds = LegacyTensionTuning.DefaultSwellRiseSeconds;
        [SerializeField, Range(0f, 3f), Tooltip("흐름의 빛이 가장 밝은 채 머무는 시간(실제 초).")]
        private float flowSwellHoldSeconds = LegacyTensionTuning.DefaultSwellHoldSeconds;
        [SerializeField, Range(.05f, 3f), Tooltip("흐름의 빛이 잦아드는 시간(실제 초). 기본은 차오름 0.12 + 머묾 0.1 + 잦아듦 0.7 = 약 0.9초입니다.")]
        private float flowSwellFadeSeconds = LegacyTensionTuning.DefaultSwellFadeSeconds;

        [Header("숲 소리 — 실행 중 즉시 적용 (Resources/Sfx/forest-ambience-loop)")]
        [SerializeField, Range(0f, 1f), Tooltip("전투와 숲 장면(컷신) 내내 낮게 깔리는 숲 소리(바람·잎·먼 새)의 음량. 로비·타이틀·브리핑에서는 잦아들어 멈춥니다. 0이면 끕니다.")]
        private float forestAmbienceVolume = .4f;
        [SerializeField, Range(0f, 5f), Tooltip("숲 소리가 들어오고 나가는 시간(실제 초, 음량 전체를 오르내리는 데 걸리는 시간). 0이면 바로 바뀝니다.")]
        private float forestAmbienceFadeSeconds = 1.2f;
        [SerializeField, Range(0f, 1f), Tooltip("장면의 @ambience나 수훈 기운 소리가 날 때 숲 소리가 물러나는 비율. 1이면 그동안 숲 소리를 다 내리고, 0이면 그대로 둡니다.")]
        private float forestAmbienceSceneDuck = .7f;

        [Header("시간 압박 카메라 — 실행 중 즉시 적용 (편성 제한 시간이 흐를 때만)")]
        [SerializeField, Range(0f, 1f), Tooltip("편성 제한 시간이 이 비율보다 적게 남으면 카메라가 아주 조금 다가갑니다(남은 시간이 줄수록 더). 시간 제한 없는 임무, 코치가 시계를 멈춘 동안, 수련에는 없습니다. 0이면 끕니다.")]
        private float timePressureShare = .3f;
        [SerializeField, Range(0f, .15f), Tooltip("시간이 다 됐을 때 카메라가 더 다가가는 양(화면 크기에 대한 비율, 0.04 = 4%). 확정하거나 새 턴이 시작되면 돌아옵니다.")]
        private float timePressureCameraPush = .04f;

        [Header("스텝 — 다음 스킬부터 적용")]
        [SerializeField, Range(0f, 1f), Tooltip("첫 타격 전 예고 시간(게임 시계 초). 연타의 동작·타격 간격은 유지합니다.")]
        private float stepAnticipationDuration = 0.20f;
        [SerializeField, Range(0.03f, 0.25f), Tooltip("첫 타격 직전 회피·압박이 성공하는 구간(게임 시계 초). 턴의 첫 시도 기준이며 시도할 때마다 좁아집니다. 연타는 기술 전체에 한 번 적용합니다.")]
        private float stepTimingWindow = 0.1f;
        [SerializeField, Range(0.3f, 1f), Tooltip("이번 턴 스텝을 시도할 때마다(회피·압박, 성공·실패 무관) 다음 성공 구간에 곱하는 값. 턴마다 초기화됩니다.")]
        private float stepWindowDecay = LegacyStepTiming.DefaultDecay;
        [SerializeField, Range(0.02f, 0.1f), Tooltip("아무리 연속으로 써도 성공 구간이 이보다 좁아지지 않는 하한(게임 시계 초).")]
        private float stepMinimumWindow = LegacyStepTiming.DefaultMinimumWindow;

        [Header("스텝 연출 — 다음 입력부터 적용")]
        [SerializeField, Range(0f, 3f), Tooltip("회피의 뒤 이동 거리(월드 단위). 전투 판정이 아니라 위치 연출입니다.")]
        private float stepDodgeDistance = 1.4f;
        [SerializeField, Range(0f, 3f), Tooltip("압박의 앞 이동 거리(월드 단위). 캐릭터 최소 간격은 유지합니다.")]
        private float stepPressureDistance = 1.1f;
        [SerializeField, Range(0f, 1f), Tooltip("성공한 스텝의 슬로모션 지속 시간(실제 초). 0으로 끕니다.")]
        private float stepSlowMotionDuration = 0.56f;
        [SerializeField, Range(0.05f, 1f), Tooltip("성공 순간 전투 시계 속도. 1이면 슬로모션 없음. 이동·카메라·잔상은 실제 시간을 사용합니다.")]
        private float stepSlowMotionScale = 0.12f;
        [SerializeField, Range(0.5f, 1f), Tooltip("이번 턴 연속 성공 한 번마다 성공 슬로모션 속도에 곱하는 값(더 느려짐). 5연속까지 적용하며, 붕괴·강타의 0.08배보다 느려지지 않습니다.")]
        private float stepStreakSlowScaleFactor = 0.88f;
        [SerializeField, Range(0f, 0.2f), Tooltip("이번 턴 연속 성공 한 번마다 성공 슬로모션에 더하는 시간(실제 초). 5연속까지 적용합니다.")]
        private float stepStreakSlowDurationStep = 0.05f;
        [SerializeField, Range(0.05f, 2f), Tooltip("플레이어 카메라 집중·배경 암전 지속 시간(실제 초).")]
        private float stepFocusDuration = 0.78f;
        [SerializeField, Range(0f, 1.5f), Tooltip("성공 순간 카메라가 추가로 당겨지는 양. 0이면 추가 확대 없음.")]
        private float stepCameraZoom = 1.1f;
        [SerializeField, Range(0f, 0.85f), Tooltip("성공 순간 배경만 어두워지는 양. 캐릭터·스킬·체력 UI는 어두워지지 않습니다.")]
        private float stepBackdropDarkening = 0.55f;

        [Header("스텝 피드백 — 실행 중 즉시 적용")]
        [SerializeField, Range(0f, 1f), Tooltip("회피의 바람·압박의 금속음 전용 음량. 0이면 끕니다. 타격 사운드의 음량·피치는 바꾸지 않습니다.")]
        private float stepSoundVolume = 0.7f;
        [SerializeField, Range(0f, 3f), Tooltip("캐릭터 주변 스텝 원의 흰 광채 강도. 0이면 광채만 끄고 얇은 판정선과 성공 구간은 유지합니다.")]
        private float stepRingGlowIntensity = 1.6f;

        [Header("전투 불릿타임 — 실행 중 즉시 적용 (결투에는 쓰지 않음)")]
        [SerializeField, Range(0f, 2f), Tooltip("전투의 편성 중 두 사람이 서로에게 다가가는 속도(한 사람당, 초당 전장 단위). Tab을 누르면 편성 시간과 함께 0.2배로 느려집니다. 0이면 다가가지 않습니다.")]
        private float battleDriftSpeed = 0.15f;
        [SerializeField, Tooltip("다수 적 전투의 준비 중 접근 속도 배율. X=첫 번째 적, Y=두 번째 적, Z=세 번째 적. 각 0.1~2이며 기본 접근 속도에 곱합니다. 결투와 공격 재생 속도에는 적용하지 않습니다.")]
        private Vector3 enemyPlanningSpeedFactors = new Vector3(0.8f, 1f, 1.2f);
        [SerializeField, Range(4f, 10f), Tooltip("편성이 시작될 때 이보다 가까이 붙어 있으면, 먼저 정상 속도로 이 간격까지 물러섰다가 다시 다가갑니다. 4면 물러서지 않습니다.")]
        private float battleStagingSeparation = 5.4f;
        [SerializeField, Range(4f, 10f), Tooltip("편성 중 다가가다 멈추는 거리. 교전 거리 4보다 가까워지지 않고, 이미 가까우면 더 다가가지 않습니다. 편성 시작의 물러섬과는 별개입니다.")]
        private float battleDriftMinimumSeparation = 5f;
        [SerializeField, Range(0f, 1f), Tooltip("편성 중 대기 동작의 속도 배율. 0이면 자세를 붙잡아 둡니다(불릿타임). 1이면 결투와 같습니다.")]
        private float battlePoseSpeed = 0f;
        [SerializeField, Range(0f, 100f), Tooltip("편성 중 화면 채도를 낮추는 정도. 확정하면 바로 돌아옵니다. 0이면 끕니다.")]
        private float battleDesaturation = 30f;
        [SerializeField, Range(0f, 1f), Tooltip("편성 중 화면에 씌우는 차가운 색의 세기. 확정하면 바로 돌아옵니다. 0이면 끕니다.")]
        private float battleCoolTint = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("넘기기로 편성 시간을 쓸 때 불릿타임이 잠깐 풀리는 시간(실제 초). 색과 대기 동작이 돌아오고 두 사람이 성큼 다가갑니다. 0이면 풀리지 않습니다.")]
        private float battleCycleReleaseSeconds = 0.3f;

        [Header("타격 빛 — 실행 중 즉시 적용")]
        [SerializeField] private bool glowEnabled = true;
        [SerializeField, Range(0f, 8f), Tooltip("타격점의 HDR 발광 강도. 0이면 새 발광이 보이지 않습니다.")]
        private float glowIntensity = 3f;
        [SerializeField, Range(0.1f, 3f)] private float glowRadius = 1.1f;
        [SerializeField, Range(0.04f, 1f), Tooltip("빛 감쇠 시간(실제 초). 슬로모션이나 히트 스톱과 무관하게 사라집니다.")]
        private float glowDuration = 0.14f;
        [SerializeField, Range(0f, 3f)] private float bloomIntensity = 0.65f;
        [SerializeField, Range(0.5f, 8f)] private float bloomThreshold = 1.2f;
        [SerializeField, Range(0f, 1f), Tooltip("일반 타격의 짧은 화면 노출 섬광. 0이면 화면 섬광을 끕니다.")]
        private float flashExposure = 0.18f;

        public float FarMistHeight => Safe(farMistHeight, 1f, 100f, 14.4f);
        public float FarMistY => Safe(farMistY, -50f, 50f, -0.5f);
        public float FarHeight => Safe(farHeight, 1f, 100f, 14.4f);
        public float FarY => Safe(farY, -50f, 50f, -0.5f);
        public float MidHeight => Safe(midHeight, 1f, 100f, 12.6f);
        public float MidY => Safe(midY, -50f, 50f, -1.2f);
        public float NearHeight => Safe(nearHeight, 1f, 100f, 12.6f);
        public float NearY => Safe(nearY, -50f, 50f, -0.75f);
        public float AnimationPlaybackSpeed => Safe(animationPlaybackSpeed, 0.1f, 2f, 1f);
        public float AttackInterval => Safe(attackInterval, 0f, 1f, 0.025f);
        public float SkillInterval => Safe(skillInterval, 0f, 2f, 0.06f);
        public float HitStopDuration => Safe(hitStopDuration, 0f, 0.15f, 0.04f);
        public float ApproachSettleSeconds => Safe(approachSettleSeconds, 0f, .3f, .04f);
        public float ReturnSettleSeconds => Safe(returnSettleSeconds, 0f, .3f, .04f);
        public float TurnCleanupSeconds => Safe(turnCleanupSeconds, 0f, .3f, .04f);
        public float BladeRecoilScale => Safe(bladeRecoilScale, 0f, 1f, .35f);
        public bool AutoDepthEnabled => autoDepthEnabled;
        public float CombatDepthSpread => Safe(combatDepthSpread, 0f, 1.2f, .45f);
        public float CombatDepthTravelSpeed => Safe(combatDepthTravelSpeed, .1f, 8f, 2.8f);
        public float CombatDepthExchangeOffset => Safe(combatDepthExchangeOffset, 0f, .6f, .28f);
        public bool AttackRhythmEnabled => attackRhythmEnabled;
        public float AttackRhythmStrength => Safe(attackRhythmStrength, 0f, 1f, LegacyAttackRhythm.DefaultStrength);
        public float AttackLungeDistance => Safe(attackLungeDistance, 0f, 1f, LegacyAttackRhythm.DefaultLungeDistance);
        public bool WeaponTrailEnabled => weaponTrailEnabled;
        public float WeaponTrailStrength => Safe(weaponTrailStrength, 0f, 1f, 1f);
        public float WeaponTrailSeconds => Safe(weaponTrailSeconds, .04f, .25f, .12f);
        public float CombatCameraSize => Safe(combatCameraSize, 2.8f, 6f, 3.5f);
        public float CameraFollowSharpness => Safe(cameraFollowSharpness, 1f, 20f, 6f);
        public float CameraFollowDeadZone => Safe(cameraFollowDeadZone, 0f, 1.5f, 0.35f);
        public float ImpactCameraShake => Safe(impactCameraShake, 0f, 0.35f, 0.08f);
        public bool CombatCameraMotionEnabled => combatCameraMotionEnabled;
        public float CombatCameraMotionStrength => Safe(combatCameraMotionStrength, 0f, 1f, 1f);
        public float MovementDistanceMultiplier => Safe(movementDistanceMultiplier, 0.5f, 2.5f, 1.4f);
        public float MovementSpeedMultiplier => Safe(movementSpeedMultiplier, 0.5f, 2f, 1.2f);
        public float EnemyHandoffSpeedMultiplier => Safe(enemyHandoffSpeedMultiplier, 1f, 2f, 1.35f);
        public int DamageTextFontSize => Mathf.Clamp(damageTextFontSize, 80, 240);
        public float CriticalDamageScale => Safe(criticalDamageScale, 1f, 1.8f, 1.3f);
        /// <summary>The share of the target's maximum health one hit must take as health to be decisive (0..100; 0 is off).</summary>
        public int DecisiveHealthDamagePercent => Mathf.Clamp(decisiveHealthDamagePercent, 0, 100);
        public float FinishingSlowMotionSeconds => Safe(finishingSlowMotionSeconds, 0f, 4f, 1.75f);
        public float FinishingSlowMotionScale => Safe(finishingSlowMotionScale, 0.02f, 1f, 0.06f);
        public float FinishingBarSeconds => Safe(finishingBarSeconds, 0f, 2f, 0.4f);
        public float FinishingFreezeSeconds => Safe(finishingFreezeSeconds, 0f, 2f, 0.5f);
        public float FinalFallGreySeconds => Safe(finalFallGreySeconds, 0f, 3f, 0.9f);
        public float FinalFallColourReturnSeconds => Safe(finalFallColourReturnSeconds, 0f, 3f, 1.2f);
        public float EmpowermentCutInSeconds => Safe(empowermentCutInSeconds, 0f, 1.5f, 0.6f);
        public float EmpowermentCutInCameraSize => Safe(empowermentCutInCameraSize, 1.5f, 6f, 2.8f);
        public float EmpowermentWarmTint => Safe(empowermentWarmTint, 0f, 1f, 0.4f);
        public float EmpowermentVignette => Safe(empowermentVignette, 0f, 0.6f, 0.15f);
        public float EmpowermentAtmosphereSeconds => Safe(empowermentAtmosphereSeconds, 0f, 5f, 1.2f);
        public float EmpowermentAuraLoopVolume => Safe(empowermentAuraLoopVolume, 0f, 1f, 0.25f);
        public float LaudareShakeStrength => Safe(laudareShakeStrength, 0f, 0.5f, 0.12f);
        public float LaudareShakeSeconds => Safe(laudareShakeSeconds, 0.05f, 1f, 0.25f);
        /// <summary>The 수훈 afterimages' colour when nothing is tuned: a warm golden yellow that reads on the forest.</summary>
        public static readonly Color DefaultEmpowermentAfterimageColor = new Color(1f, 0.88f, 0.3f, 1f);
        /// <summary>The 수훈 afterimages' colour (opaque; how strongly they show is <see cref="EmpowermentAfterimageAlpha"/>).</summary>
        public Color EmpowermentAfterimageColor => SafeColor(empowermentAfterimageColor, DefaultEmpowermentAfterimageColor);
        /// <summary>A new 수훈 afterimage's opacity at the aura's full strength (0..1; 0 switches them off).</summary>
        public float EmpowermentAfterimageAlpha => Safe(empowermentAfterimageAlpha, 0f, 1f, 0.45f);
        /// <summary>Seconds between 수훈 afterimages, on the aura's clock (the battle's in battle, real time in a scene).</summary>
        public float EmpowermentAfterimageInterval => Safe(empowermentAfterimageInterval, 0.02f, 0.5f, 0.05f);
        /// <summary>Seconds one 수훈 afterimage takes to fade out, on the same clock.</summary>
        public float EmpowermentAfterimageSeconds => Safe(empowermentAfterimageSeconds, 0.1f, 1.5f, 0.4f);
        /// <summary>How far a 수훈 afterimage drifts back behind her over its life (world units), as the breath has it.</summary>
        public float EmpowermentAfterimageDrift => Safe(empowermentAfterimageDrift, 0f, 1.5f, 0.3f);
        /// <summary>How much a 수훈 afterimage grows about her feet over its life (0.06 = 6%), as the breath has it.</summary>
        public float EmpowermentAfterimageSwell => Safe(empowermentAfterimageSwell, 0f, 0.3f, 0.06f);
        /// <summary>The speech bubbles' text size when nothing is tuned.</summary>
        public const int DefaultBarkFontSize = 24;
        /// <summary>Real seconds a battle bark's bubble stays (0..6; 0 switches barks off).</summary>
        public float BarkSeconds => Safe(barkSeconds, 0f, 6f, BarkTracker.DefaultShowSeconds);
        public float BarkHurtCooldownSeconds => Safe(barkHurtCooldownSeconds, 0f, 60f, BarkTracker.DefaultHurtCooldownSeconds);
        public int BarkLowHealthPercent => Mathf.Clamp(barkLowHealthPercent, 0, 99);
        public int BarkFontSize => Mathf.Clamp(barkFontSize, 16, 40);
        /// <summary>Real seconds of a battle's start card (0..4; 0 switches it off), and of a retry's shorter one.</summary>
        public float StartCardSeconds => Safe(startCardSeconds, 0f, 4f, 1.6f);
        public float StartCardRetrySeconds => Safe(startCardRetrySeconds, 0f, 4f, .8f);
        /// <summary>Real seconds of the gear shimmer over the enemy's revealed queue (0..1.5; 0 switches it off).</summary>
        public float GearShimmerSeconds => Safe(gearShimmerSeconds, 0f, 1.5f, .55f);
        public float GearShimmerStrength => Safe(gearShimmerStrength, 0f, 1f, .6f);
        /// <summary>The skill dock's gears (<see cref="LegacySkillGear"/>): a lane gear's tip radius, the open lanes' pitch
        /// and the tooth depth (HUD units), its teeth per slot, and the idlers' tip radius (0: none). The radius stays at 82 or
        /// more: the slots, the window and the cost plate keep their sizes, and a smaller gear crowds them together. The HUD
        /// narrows the pitch further when the idlers would not mesh above the dock's edge (<see cref="LegacySkillGear.MeshedPitch"/>).</summary>
        public float SkillGearRadius => Safe(skillGearRadius, 82f, 92f, LegacySkillGear.DefaultRadius);
        public float SkillGearPitch => Safe(skillGearPitch, 160f, 200f, LegacySkillGear.DefaultPitch);
        public int SkillGearTeethPerSlot => Mathf.Clamp(skillGearTeethPerSlot, 1, 8);
        public float SkillGearToothDepth => Safe(skillGearToothDepth, 4f, 18f, LegacySkillGear.DefaultToothDepth);
        public float SkillGearIdlerRadius => Safe(skillGearIdlerRadius, 0f, 40f, LegacySkillGear.DefaultIdlerRadius);
        /// <summary>Real seconds of a queue's one-slot turn and of 넘기기's (0: at once), and how far 넘기기 runs past the slot
        /// before it springs back (degrees).</summary>
        public float SkillGearTurnSeconds => Safe(skillGearTurnSeconds, 0f, 1f, LegacySkillGear.DefaultTurnSeconds);
        public float SkillGearShiftSeconds => Safe(skillGearShiftSeconds, 0f, 1.5f, LegacySkillGear.DefaultShiftSeconds);
        public float SkillGearRatchetBounce => Safe(skillGearRatchetBounce, 0f, 12f, LegacySkillGear.DefaultRatchetBounce);
        /// <summary>The gears' sounds: a queue's tick and 넘기기's ratchet (0..1; 0 is silent).</summary>
        public float GearTickVolume => Safe(gearTickVolume, 0f, 1f, LegacySkillGear.DefaultTickVolume);
        public float GearRatchetVolume => Safe(gearRatchetVolume, 0f, 1f, LegacySkillGear.DefaultRatchetVolume);
        /// <summary>맞물림 (<see cref="LegacyMeshing"/>): the power percent each skill of a chain gains per skill in it, passed
        /// into every duel the game makes (0..100; 0 switches 맞물림 off). A rule value, the one this asset holds: it applies
        /// from the next duel.</summary>
        public int MeshPercent => Mathf.Clamp(meshPercent, 0, 100);
        /// <summary>맞물림's cues (<see cref="LegacyMeshCue"/>): the queue row's burst (real seconds, 0 off) and its gears' size
        /// (HUD units), the sparks for a two-skill chain (0 off), the mesh sound's volume, and the first hit's flash (real
        /// seconds, 0 off) and its gears' size.</summary>
        public float MeshBurstSeconds => Safe(meshBurstSeconds, 0f, 1.5f, LegacyMeshCue.DefaultBurstSeconds);
        public float MeshGearSize => Safe(meshGearSize, 16f, 48f, LegacyMeshCue.DefaultGearSize);
        public int MeshSparkCount => Mathf.Clamp(meshSparkCount, 0, 24);
        public float MeshSoundVolume => Safe(meshSoundVolume, 0f, 1f, LegacyMeshCue.DefaultVolume);
        public float MeshFlashSeconds => Safe(meshFlashSeconds, 0f, 1.5f, LegacyMeshCue.DefaultFlashSeconds);
        public float MeshFlashSize => Safe(meshFlashSize, 32f, 160f, LegacyMeshCue.DefaultFlashSize);
        /// <summary>Holding a lane (<see cref="LegacyLaneHold"/>): the longest press that still queues and when a hold opens the
        /// skill's explanation (real seconds), and the planning clock's pace while the player reads it (0.05..1).</summary>
        public float LaneTapSeconds => Safe(laneTapSeconds, .05f, .5f, LegacyLaneHold.DefaultTapSeconds);
        public float ExplanationHoldSeconds => Safe(explanationHoldSeconds, .1f, 1.5f, LegacyLaneHold.DefaultExplainSeconds);
        public float ExplanationTimeScale => Safe(explanationTimeScale, .05f, 1f, LegacyLaneHold.DefaultExplanationTimeScale);
        /// <summary>편성 예상 (<see cref="LegacySlotForecast"/>): whether the duel HUD shows it (and in versus), and how a slot's
        /// verdict weighs resistance (percent of a health point), a break (health points) and its margin (health points).</summary>
        public bool SlotForecastEnabled => slotForecastEnabled;
        public bool SlotForecastInVersus => slotForecastInVersus;
        public int SlotForecastResistanceWeight => Mathf.Clamp(slotForecastResistanceWeight, 0, 100);
        public int SlotForecastBreakBonus => Mathf.Clamp(slotForecastBreakBonus, 0, 30);
        public int SlotForecastMargin => Mathf.Clamp(slotForecastMargin, 0, 30);
        /// <summary>The duel HUD's two facing queue panels over the dock (<see cref="LegacyQueuePanelLayout"/>): one panel's
        /// width, a card's size and the full spacing between cards, the gap at the centre seam (HUD units), and the slide
        /// toward the seam after each resolved slot (battle seconds; 0 at once).</summary>
        public float QueuePanelWidth => Safe(queuePanelWidth, 360f, 760f, LegacyQueuePanelLayout.DefaultPanelWidth);
        public float QueueCardSize => Safe(queueCardSize, 48f, 80f, LegacyQueuePanelLayout.DefaultCardSize);
        public float QueueCardSpacing => Safe(queueCardSpacing, 48f, 104f, LegacyQueuePanelLayout.DefaultCardSpacing);
        public float QueueSeamGap => Safe(queueSeamGap, 0f, 240f, LegacyQueuePanelLayout.DefaultSeamGap);
        public float QueueSlideSeconds => Safe(queueSlideSeconds, 0f, .6f, LegacyQueuePanelLayout.DefaultSlideSeconds);
        /// <summary>The panels' gap over the dock while planning, and their bottom over the screen's while the dock is folded
        /// away for a resolving turn (HUD units).</summary>
        public float QueuePanelDockGap => Safe(queuePanelDockGap, 0f, 40f, LegacyQueuePanelLayout.DefaultDockGap);
        public float QueuePanelResolvingBottom => Safe(queuePanelResolvingBottom, 0f, 120f, LegacyQueuePanelLayout.DefaultResolvingBottom);
        /// <summary>Whether the arena camera keeps the fighters' feet over the panels (moving down only as far as that takes),
        /// and the room it leaves between them (HUD units).</summary>
        public bool QueuePanelKeepsFightersClear => queuePanelKeepsFightersClear;
        public float QueuePanelFighterClearance => Safe(queuePanelFighterClearance, 0f, 60f, LegacyQueuePanelLayout.DefaultFighterClearance);
        /// <summary>The planning pair cues on the queue panels: whether the ring on the opponent card the next reservation
        /// would meet and the faint socket at its mirror on the player's panel show, the ring's strength and its pulse (real
        /// seconds; 0 steady), and the socket's strength.</summary>
        public bool QueuePairCuesEnabled => queuePairCuesEnabled;
        public float NextOpponentRingAlpha => Safe(nextOpponentRingAlpha, 0f, 1f, LegacyCombatHud.DefaultNextOpponentRingAlpha);
        public float NextOpponentRingPulseSeconds => Safe(nextOpponentRingPulseSeconds, 0f, 3f, LegacyCombatHud.DefaultNextOpponentRingPulseSeconds);
        public float NextReservationSocketAlpha => Safe(nextReservationSocketAlpha, 0f, 1f, LegacyCombatHud.DefaultNextReservationSocketAlpha);
        /// <summary>The ring's ink (opaque; its strength is <see cref="NextOpponentRingAlpha"/>), how far it stands round the
        /// card (HUD units at the built card size) and how deep its pulse dips (a share of its strength, 0 steady); the
        /// socket's fill (alpha is the fill's own strength, under <see cref="NextReservationSocketAlpha"/>).</summary>
        public static readonly Color DefaultNextOpponentRingInk = DuelVisualTheme.Accent;
        public static readonly Color DefaultNextReservationSocketInk =
            new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .6f);
        public Color NextOpponentRingInk => SafeColor(nextOpponentRingInk, DefaultNextOpponentRingInk);
        public float NextOpponentRingMargin => Safe(nextOpponentRingMargin, 0f, 16f, LegacyCombatHud.DefaultNextOpponentRingMargin);
        public float NextOpponentRingPulseDepth => Safe(nextOpponentRingPulseDepth, 0f, 1f, LegacyCombatHud.DefaultNextOpponentRingPulseDepth);
        public Color NextReservationSocketInk => SafeInk(nextReservationSocketInk, DefaultNextReservationSocketInk);
        /// <summary>The spark at both cards of a slot the player reserves into while the opponent holds it: its real seconds
        /// (0 off), size (HUD units) and sparks per card; its ink and scale by the slot forecast's verdict.</summary>
        public float ReservationSparkSeconds => Safe(reservationSparkSeconds, 0f, 1f, LegacyCombatHud.DefaultReservationSparkSeconds);
        public float ReservationSparkSize => Safe(reservationSparkSize, 12f, 60f, LegacyCombatHud.DefaultReservationSparkSize);
        public int ReservationSparkCount => Mathf.Clamp(reservationSparkCount, 0, 24);
        /// <summary>The reservation spark's inks: 유리 (bright and warm), 비슷 or no forecast, 불리 (cold and dim); alpha is
        /// the spark's strength.</summary>
        public static readonly Color DefaultReservationSparkFavourableInk = new Color(1f, .66f, .22f, 1f);
        public static readonly Color DefaultReservationSparkEvenInk = new Color(.82f, .69f, .45f, .9f);
        public static readonly Color DefaultReservationSparkUnfavourableInk = new Color(.47f, .6f, .74f, .75f);
        public Color ReservationSparkFavourableInk => SafeInk(reservationSparkFavourableInk, DefaultReservationSparkFavourableInk);
        public Color ReservationSparkEvenInk => SafeInk(reservationSparkEvenInk, DefaultReservationSparkEvenInk);
        public Color ReservationSparkUnfavourableInk => SafeInk(reservationSparkUnfavourableInk, DefaultReservationSparkUnfavourableInk);
        public float ReservationSparkFavourableScale => Safe(reservationSparkFavourableScale, 1f, 2f,
            LegacyCombatHud.DefaultReservationSparkFavourableScale);
        public float ReservationSparkUnfavourableScale => Safe(reservationSparkUnfavourableScale, .3f, 1f,
            LegacyCombatHud.DefaultReservationSparkUnfavourableScale);
        /// <summary>The clash motion of the resolving pair at the queue panels' seam (<see cref="LegacyClashPose"/>): whether it
        /// plays, its numbers (<see cref="ClashTuning"/>), and the contact spark's battle seconds (0 off), size and count.</summary>
        public bool ClashMotionEnabled => clashMotionEnabled;
        public float ClashApproachSeconds => Safe(clashApproachSeconds, 0f, .4f, LegacyClashPose.DefaultApproachSeconds);
        public float ClashApproachDistance => Safe(clashApproachDistance, 0f, 30f, LegacyClashPose.DefaultApproachDistance);
        public float ClashImpactSeconds => Safe(clashImpactSeconds, .1f, 1f, LegacyClashPose.DefaultImpactSeconds);
        public float ClashReach => Safe(clashReach, 0f, 160f, LegacyClashPose.DefaultReach);
        public float ClashPushDistance => Safe(clashPushDistance, 0f, 40f, LegacyClashPose.DefaultPushDistance);
        public float ClashKnockbackDistance => Safe(clashKnockbackDistance, 0f, 60f, LegacyClashPose.DefaultKnockbackDistance);
        public float ClashSideStepDistance => Safe(clashSideStepDistance, 0f, 60f, LegacyClashPose.DefaultSideStepDistance);
        public float ClashTiltDegrees => Safe(clashTiltDegrees, 0f, 25f, LegacyClashPose.DefaultTiltDegrees);
        public float ClashBreakShake => Safe(clashBreakShake, 0f, 20f, LegacyClashPose.DefaultBreakShake);
        public float ClashBreakShakeSeconds => Safe(clashBreakShakeSeconds, 0f, 1f, LegacyClashPose.DefaultBreakShakeSeconds);
        /// <summary>How far a settling card dips (HUD units) and how much a guard taking the blow shrinks (a share, 0 to 0.3).</summary>
        public float ClashSettleDip => Safe(clashSettleDip, 0f, 12f, LegacyClashPose.SettleDip);
        public float ClashBraceSquash => Safe(clashBraceSquash, 0f, .3f, -LegacyClashPose.BraceSquash);
        public LegacyClashTuning ClashTuning => new LegacyClashTuning(ClashApproachSeconds, ClashApproachDistance,
            ClashImpactSeconds, ClashReach, ClashPushDistance, ClashKnockbackDistance, ClashSideStepDistance, ClashTiltDegrees,
            ClashBreakShake, ClashBreakShakeSeconds, ClashSettleDip, -ClashBraceSquash);
        public float ClashSparkSeconds => Safe(clashSparkSeconds, 0f, 1f, LegacyCombatHud.DefaultClashSparkSeconds);
        public float ClashSparkSize => Safe(clashSparkSize, 12f, 80f, LegacyCombatHud.DefaultClashSparkSize);
        public int ClashSparkCount => Mathf.Clamp(clashSparkCount, 0, 24);
        /// <summary>The contact spark's head and tail inks (alpha in; the defaults are 맞물림's, <see cref="DuelMeshSparks"/>).</summary>
        public static readonly Color DefaultClashSparkHeadInk = DuelMeshSparks.DefaultHead;
        public static readonly Color DefaultClashSparkTailInk = DuelMeshSparks.DefaultTail;
        public Color ClashSparkHeadInk => SafeInk(clashSparkHeadInk, DefaultClashSparkHeadInk);
        public Color ClashSparkTailInk => SafeInk(clashSparkTailInk, DefaultClashSparkTailInk);
        /// <summary>The presentation director (<see cref="LegacyShowDirector"/>): whether it plays, a Minor slot's speed-up,
        /// a Major hit stop's scale, the turn's cap on added time (real seconds), and its time-free emphasis: the Major camera
        /// push (a share of the combat framing), the layered impact's volume and the seam clash's scale.</summary>
        public bool ShowDirectorEnabled => showDirectorEnabled;
        public float MinorSlotSpeed => Safe(minorSlotSpeed, 1f, LegacyShowPacingTuning.MaximumMinorSpeed,
            LegacyShowPacingTuning.DefaultMinorSpeed);
        public float MajorHitStopScale => Safe(majorHitStopScale, 1f, LegacyShowPacingTuning.MaximumMajorHitStopScale,
            LegacyShowPacingTuning.DefaultMajorHitStopScale);
        public float MajorCameraPush => Safe(majorCameraPush, 0f, .3f, .1f);
        public float MajorImpactVolume => Safe(majorImpactVolume, 0f, 1f, .35f);
        public float MajorClashScale => Safe(majorClashScale, 1f, 2f, 1.35f);
        public float TurnExtraSecondsCap => Safe(turnExtraSecondsCap, 0f, LegacyShowPacingTuning.MaximumTurnExtraCap,
            LegacyShowPacingTuning.DefaultTurnExtraCap);
        /// <summary>The director's pacing as a turn takes it (<see cref="LegacyShowPacingTuning"/>); a signature move's
        /// longer stop only while its treatment is on.</summary>
        public LegacyShowPacingTuning ShowPacing => new LegacyShowPacingTuning(ShowDirectorEnabled, MinorSlotSpeed,
            MajorHitStopScale, BladeBindSeconds, TurnExtraSecondsCap, LegacyShowPacingTuning.DefaultBladeBindMinimumShare,
            SignatureTreatmentEnabled ? SignatureHitStopScale : MajorHitStopScale);
        /// <summary>The slot rating's thresholds (<see cref="LegacySlotImportanceTuning"/>): a 합's gap, a pierce's share, an
        /// unopposed attack's share of the target's health (%), the 맞물림 depth that reads as 간파 (0 off), whether a dodge and
        /// the enemy's 간파 lift a slot, and the signature moves' skill ids. A heavy hit is the decisive close-up's
        /// <see cref="DecisiveHealthDamagePercent"/>.</summary>
        public float LargeClashGap => Safe(largeClashGap, 0f, 1f, LegacySlotImportanceTuning.DefaultLargeClashGap);
        public float DeepPierce => Safe(deepPierce, 0f, 1f, LegacySlotImportanceTuning.DefaultDeepPierce);
        public int BigUnopposedPercent => Mathf.Clamp(bigUnopposedPercent, 0, 100);
        public int MeshChainMinimum => Mathf.Clamp(meshChainMinimum, 0, 7);
        public bool DodgeRaisesToMajor => dodgeRaisesToMajor;
        public bool EnemyReadsRaiseTier => enemyReadsRaiseTier;
        /// <summary>The signature moves' skill ids (empty: none), as a fresh read-only copy.</summary>
        public System.Collections.Generic.IReadOnlyList<int> SignatureSkillIds
            => System.Array.AsReadOnly(signatureSkillIds != null ? (int[])signatureSkillIds.Clone() : new int[0]);
        /// <summary>The rating this game's slots get. <paramref name="symmetric"/> (online versus) counts the enemy's 간파 as
        /// the player's, so both screens give each slot the same tier and play it for the same time.</summary>
        public LegacySlotImportanceTuning SlotImportanceTuning(bool symmetric = false)
            => new LegacySlotImportanceTuning(BladeBindGap, LargeClashGap, DeepPierce, BigUnopposedPercent,
                DecisiveHealthDamagePercent, MeshChainMinimum, DodgeRaisesToMajor, symmetric || EnemyReadsRaiseTier,
                SignatureSkillIds);
        /// <summary>팽팽한 합 (<see cref="LegacyBladeBind"/>): the 합 gap that binds, its real seconds (0 off), the pair's sway,
        /// each one's lean and spring apart (world units), and the sparks it strikes.</summary>
        public float BladeBindGap => Safe(bladeBindGap, 0f, .5f, LegacySlotImportanceTuning.DefaultBladeBindGap);
        public float BladeBindSeconds => Safe(bladeBindSeconds, 0f, LegacyShowPacingTuning.MaximumBladeBindSeconds,
            LegacyBladeBind.DefaultSeconds);
        public float BladeBindSway => Safe(bladeBindSway, 0f, .6f, LegacyBladeBind.DefaultSway);
        public float BladeBindPress => Safe(bladeBindPress, 0f, .4f, LegacyBladeBind.DefaultPress);
        public float BladeBindSpring => Safe(bladeBindSpring, 0f, 1.5f, LegacyBladeBind.DefaultSpring);
        public int BladeBindSparks => Mathf.Clamp(bladeBindSparks, 0, 6);
        /// <summary>A turn's gradual playback acceleration, multiplied when one side's queue is exhausted. The turn takes
        /// these settings when it starts; the presentation clock keeps input windows and real-second effects intact.</summary>
        public bool CombatTempoEnabled => combatTempoEnabled;
        public float CombatTempoStep => Safe(combatTempoStep, 0f, LegacyCombatTempoTuning.MaximumStepPerSlot,
            LegacyCombatTempoTuning.DefaultStepPerSlot);
        public float CombatTempoMaxSpeed => Safe(combatTempoMaxSpeed, 1f, LegacyCombatTempoTuning.MaximumSpeedLimit,
            LegacyCombatTempoTuning.DefaultMaximumSpeed);
        public float ExhaustedSideSpeed => Safe(exhaustedSideSpeed, 1f, LegacyCombatTempoTuning.MaximumExhaustedSpeed,
            LegacyCombatTempoTuning.DefaultExhaustedSpeed);
        public float ExhaustedGapScale => Safe(exhaustedGapScale, 0f, 1f, LegacyCombatTempoTuning.DefaultExhaustedGapScale);
        public LegacyCombatTempoTuning CombatTempo => new LegacyCombatTempoTuning(CombatTempoEnabled, CombatTempoStep,
            CombatTempoMaxSpeed, ExhaustedSideSpeed, ExhaustedGapScale);
        /// <summary>일방 공격 가속 (<see cref="LegacyUnopposedStreak"/>): whether a run of unopposed slots plays faster
        /// (only while the director is on), the streak's length where it starts, the speed each slot adds, and the cap.</summary>
        public bool UnopposedStreakEnabled => unopposedStreakEnabled;
        public int UnopposedStreakStartLength => Mathf.Clamp(unopposedStreakStartLength, 1, LegacyStreakTuning.MaximumStartLength);
        public float UnopposedStreakStep => Safe(unopposedStreakStep, 0f, LegacyStreakTuning.MaximumStepPerSlot,
            LegacyStreakTuning.DefaultStepPerSlot);
        public float UnopposedStreakMaxSpeed => Safe(unopposedStreakMaxSpeed, 1f, LegacyStreakTuning.MaximumSpeedLimit,
            LegacyStreakTuning.DefaultMaximumSpeed);
        /// <summary>The unopposed streak as a turn takes it (<see cref="LegacyStreakTuning"/>).</summary>
        public LegacyStreakTuning UnopposedStreak => new LegacyStreakTuning(UnopposedStreakEnabled, UnopposedStreakStep,
            UnopposedStreakMaxSpeed, UnopposedStreakStartLength);
        /// <summary>간파 신호 (the player's read paying off): whether it shows, its '간파' stamp, its real seconds, its ink,
        /// the burst's sparks and size (HUD units), and the light accent on the player's fighter.</summary>
        public static readonly Color DefaultReadCueInk = new Color(1f, .84f, .4f, 1f);
        public bool ReadCueEnabled => readCueEnabled;
        public bool ReadStampEnabled => readStampEnabled;
        public float ReadCueSeconds => Safe(readCueSeconds, .3f, 2f, .9f);
        public Color ReadCueInk => SafeColor(readCueInk, DefaultReadCueInk);
        public int ReadSparkCount => Mathf.Clamp(readSparkCount, 0, 24);
        public float ReadSparkSize => Safe(readSparkSize, 12f, 80f, 40f);
        public bool ReadFighterAccent => readFighterAccent;
        /// <summary>시그니처 기술: whether a signature move gets its treatment, its longer stop's scale (at least a Major
        /// slot's), the camera's push, the extra steel ring's volume, the windup glow, and its trail's ghosts (real seconds
        /// and opacity; 0 seconds: none).</summary>
        public bool SignatureTreatmentEnabled => signatureTreatmentEnabled;
        public float SignatureHitStopScale => Mathf.Max(MajorHitStopScale, Safe(signatureHitStopScale, 1f,
            LegacyShowPacingTuning.MaximumMajorHitStopScale, LegacyShowPacingTuning.DefaultSignatureHitStopScale));
        public float SignatureCameraPush => Safe(signatureCameraPush, 0f, .3f, .16f);
        public float SignatureImpactVolume => Safe(signatureImpactVolume, 0f, 1f, .45f);
        public bool SignatureWindupGlow => signatureWindupGlow;
        public float SignatureAfterimageSeconds => Safe(signatureAfterimageSeconds, 0f, 1f, .3f);
        public float SignatureAfterimageAlpha => Safe(signatureAfterimageAlpha, 0f, 1f, .55f);
        /// <summary>긴장 조명 (<see cref="LegacyTensionLight"/>, <see cref="DuelTensionLook"/>): low health's tension (on/off,
        /// its health share, its real-second eases, the scenery's darkening, the picture's dimming, the vignette's depth and
        /// ember) and the flow's swell (on/off, on a break too, the lead margin, its exposure, the vignette's opening, its
        /// warmth and its real-second rise, hold and fade).</summary>
        public bool TensionLightingEnabled => tensionLightingEnabled;
        public int TensionHealthPercent => Mathf.Clamp(tensionHealthPercent, 0, LegacyTensionTuning.MaximumHealthPercent);
        public float TensionEaseInSeconds => Safe(tensionEaseInSeconds, 0f, LegacyTensionTuning.MaximumEaseSeconds,
            LegacyTensionTuning.DefaultEaseInSeconds);
        public float TensionEaseOutSeconds => Safe(tensionEaseOutSeconds, 0f, LegacyTensionTuning.MaximumEaseSeconds,
            LegacyTensionTuning.DefaultEaseOutSeconds);
        public float TensionBackdropDarkening => Safe(tensionBackdropDarkening, 0f, DuelTensionLook.MaximumBackdropDarkening,
            DuelTensionLook.DefaultBackdropDarkening);
        public float TensionDimming => Safe(tensionDimming, 0f, 1f, DuelTensionLook.DefaultDimming);
        public float TensionVignette => Safe(tensionVignette, 0f, DuelTensionLook.MaximumVignette, DuelTensionLook.DefaultVignette);
        public Color TensionVignetteInk => SafeColor(tensionVignetteInk, DuelTensionLook.DefaultVignetteInk);
        public bool FlowSwellEnabled => flowSwellEnabled;
        public bool FlowSwellOnBreak => flowSwellOnBreak;
        public int FlowLeadMarginPercent => Mathf.Clamp(flowLeadMarginPercent, 0, LegacyTensionTuning.MaximumLeadMarginPercent);
        public float FlowSwellExposure => Safe(flowSwellExposure, 0f, 1f, DuelTensionLook.DefaultSwellExposure);
        public float FlowSwellVignetteRelief => Safe(flowSwellVignetteRelief, 0f, DuelTensionLook.MaximumVignette,
            DuelTensionLook.DefaultSwellVignetteRelief);
        public float FlowSwellWarmTint => Safe(flowSwellWarmTint, 0f, 1f, DuelTensionLook.DefaultSwellWarmTint);
        public float FlowSwellRiseSeconds => Safe(flowSwellRiseSeconds, 0f, LegacyTensionTuning.MaximumSwellSeconds,
            LegacyTensionTuning.DefaultSwellRiseSeconds);
        public float FlowSwellHoldSeconds => Safe(flowSwellHoldSeconds, 0f, LegacyTensionTuning.MaximumSwellSeconds,
            LegacyTensionTuning.DefaultSwellHoldSeconds);
        public float FlowSwellFadeSeconds => Safe(flowSwellFadeSeconds, LegacyTensionTuning.MinimumSwellFadeSeconds,
            LegacyTensionTuning.MaximumSwellSeconds, LegacyTensionTuning.DefaultSwellFadeSeconds);
        /// <summary>The tension lighting as the arena takes it, frame by frame (<see cref="LegacyArenaView.SetTensionLighting"/>).</summary>
        public DuelTensionLook TensionLighting => new DuelTensionLook(
            new LegacyTensionTuning(TensionLightingEnabled, TensionHealthPercent, TensionEaseInSeconds, TensionEaseOutSeconds,
                FlowSwellEnabled, FlowSwellOnBreak, FlowLeadMarginPercent, FlowSwellRiseSeconds, FlowSwellHoldSeconds,
                FlowSwellFadeSeconds),
            TensionBackdropDarkening, TensionDimming, TensionVignette, TensionVignetteInk, FlowSwellExposure,
            FlowSwellVignetteRelief, FlowSwellWarmTint);
        public float ForestAmbienceVolume => Safe(forestAmbienceVolume, 0f, 1f, .4f);
        public float ForestAmbienceFadeSeconds => Safe(forestAmbienceFadeSeconds, 0f, 5f, 1.2f);
        public float ForestAmbienceSceneDuck => Safe(forestAmbienceSceneDuck, 0f, 1f, .7f);
        /// <summary>The share of the planning time left below which the camera starts to push in (0..1; 0 is off).</summary>
        public float TimePressureShare => Safe(timePressureShare, 0f, 1f, .3f);
        /// <summary>How much the camera pushes in when the time is all but out, as a share of its size (0..0.15).</summary>
        public float TimePressureCameraPush => Safe(timePressureCameraPush, 0f, .15f, .04f);
        public float StepAnticipationDuration => Safe(stepAnticipationDuration, 0f, 1f, 0.20f);
        public float StepTimingWindow => Safe(stepTimingWindow, 0.03f, 0.25f, 0.1f);
        public float StepWindowDecay => Safe(stepWindowDecay, 0.3f, 1f, LegacyStepTiming.DefaultDecay);
        public float StepMinimumWindow => Safe(stepMinimumWindow, 0.02f, 0.1f, LegacyStepTiming.DefaultMinimumWindow);
        public float StepDodgeDistance => Safe(stepDodgeDistance, 0f, 3f, 1.4f);
        public float StepPressureDistance => Safe(stepPressureDistance, 0f, 3f, 1.1f);
        public float StepSlowMotionDuration => Safe(stepSlowMotionDuration, 0f, 1f, 0.56f);
        public float StepSlowMotionScale => Safe(stepSlowMotionScale, 0.05f, 1f, 0.12f);
        public float StepStreakSlowScaleFactor => Safe(stepStreakSlowScaleFactor, 0.5f, 1f, 0.88f);
        public float StepStreakSlowDurationStep => Safe(stepStreakSlowDurationStep, 0f, 0.2f, 0.05f);
        public float StepFocusDuration => Safe(stepFocusDuration, 0.05f, 2f, 0.78f);
        public float StepCameraZoom => Safe(stepCameraZoom, 0f, 1.5f, 1.1f);
        public float StepBackdropDarkening => Safe(stepBackdropDarkening, 0f, 0.85f, 0.55f);
        public float StepSoundVolume => Safe(stepSoundVolume, 0f, 1f, 0.7f);
        public float StepRingGlowIntensity => Safe(stepRingGlowIntensity, 0f, 3f, 1.6f);
        public float BattleDriftSpeed => Safe(battleDriftSpeed, 0f, 2f, 0.15f);
        public float EnemyPlanningSpeedFactor(int enemyIndex)
        {
            switch (enemyIndex)
            {
                case 0: return Safe(enemyPlanningSpeedFactors.x, 0.1f, 2f, 0.8f);
                case 1: return Safe(enemyPlanningSpeedFactors.y, 0.1f, 2f, 1f);
                case 2: return Safe(enemyPlanningSpeedFactors.z, 0.1f, 2f, 1.2f);
                default: return 1f;
            }
        }
        public float BattleStagingSeparation => Safe(battleStagingSeparation, 4f, 10f, 5.4f);
        public float BattleDriftMinimumSeparation => Safe(battleDriftMinimumSeparation, 4f, 10f, 5f);
        public float BattlePoseSpeed => Safe(battlePoseSpeed, 0f, 1f, 0f);
        public float BattleDesaturation => Safe(battleDesaturation, 0f, 100f, 30f);
        public float BattleCoolTint => Safe(battleCoolTint, 0f, 1f, 0.35f);
        public float BattleCycleReleaseSeconds => Safe(battleCycleReleaseSeconds, 0f, 1f, 0.3f);
        public bool GlowEnabled => glowEnabled;
        public float GlowIntensity => Safe(glowIntensity, 0f, 8f, 3f);
        public float GlowRadius => Safe(glowRadius, 0.1f, 3f, 1.1f);
        public float GlowDuration => Safe(glowDuration, 0.04f, 1f, 0.14f);
        public float BloomIntensity => Safe(bloomIntensity, 0f, 3f, 0.65f);
        public float BloomThreshold => Safe(bloomThreshold, 0.5f, 8f, 1.2f);
        public float FlashExposure => Safe(flashExposure, 0f, 1f, 0.18f);

        private static float Safe(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

        // An opaque colour within 0..1 (no HDR); one that is not a number falls back whole.
        private static Color SafeColor(Color value, Color fallback) =>
            float.IsNaN(value.r) || float.IsNaN(value.g) || float.IsNaN(value.b) ||
            float.IsInfinity(value.r) || float.IsInfinity(value.g) || float.IsInfinity(value.b)
                ? fallback : new Color(Mathf.Clamp01(value.r), Mathf.Clamp01(value.g), Mathf.Clamp01(value.b), 1f);

        // A colour within 0..1 that keeps its alpha (no HDR); one that is not a number falls back whole.
        private static Color SafeInk(Color value, Color fallback) =>
            float.IsNaN(value.r) || float.IsNaN(value.g) || float.IsNaN(value.b) || float.IsNaN(value.a) ||
            float.IsInfinity(value.r) || float.IsInfinity(value.g) || float.IsInfinity(value.b) || float.IsInfinity(value.a)
                ? fallback
                : new Color(Mathf.Clamp01(value.r), Mathf.Clamp01(value.g), Mathf.Clamp01(value.b), Mathf.Clamp01(value.a));

        private void OnValidate()
        {
            farMistHeight = FarMistHeight; farMistY = FarMistY;
            farHeight = FarHeight; farY = FarY;
            midHeight = MidHeight; midY = MidY;
            nearHeight = NearHeight; nearY = NearY;
            animationPlaybackSpeed = AnimationPlaybackSpeed;
            attackInterval = AttackInterval; skillInterval = SkillInterval;
            hitStopDuration = HitStopDuration;
            approachSettleSeconds = ApproachSettleSeconds;
            returnSettleSeconds = ReturnSettleSeconds;
            turnCleanupSeconds = TurnCleanupSeconds;
            bladeRecoilScale = BladeRecoilScale;
            combatDepthSpread = CombatDepthSpread;
            combatDepthTravelSpeed = CombatDepthTravelSpeed;
            combatDepthExchangeOffset = CombatDepthExchangeOffset;
            attackRhythmStrength = AttackRhythmStrength;
            attackLungeDistance = AttackLungeDistance;
            weaponTrailStrength = WeaponTrailStrength;
            weaponTrailSeconds = WeaponTrailSeconds;
            combatCameraSize = CombatCameraSize; cameraFollowSharpness = CameraFollowSharpness;
            combatCameraMotionStrength = CombatCameraMotionStrength;
            cameraFollowDeadZone = CameraFollowDeadZone; impactCameraShake = ImpactCameraShake;
            movementDistanceMultiplier = MovementDistanceMultiplier; movementSpeedMultiplier = MovementSpeedMultiplier;
            enemyHandoffSpeedMultiplier = EnemyHandoffSpeedMultiplier;
            damageTextFontSize = DamageTextFontSize; criticalDamageScale = CriticalDamageScale;
            decisiveHealthDamagePercent = DecisiveHealthDamagePercent;
            finishingSlowMotionSeconds = FinishingSlowMotionSeconds; finishingSlowMotionScale = FinishingSlowMotionScale;
            finishingBarSeconds = FinishingBarSeconds; finishingFreezeSeconds = FinishingFreezeSeconds;
            finalFallGreySeconds = FinalFallGreySeconds; finalFallColourReturnSeconds = FinalFallColourReturnSeconds;
            empowermentCutInSeconds = EmpowermentCutInSeconds; empowermentCutInCameraSize = EmpowermentCutInCameraSize;
            empowermentWarmTint = EmpowermentWarmTint; empowermentVignette = EmpowermentVignette;
            empowermentAtmosphereSeconds = EmpowermentAtmosphereSeconds; empowermentAuraLoopVolume = EmpowermentAuraLoopVolume;
            laudareShakeStrength = LaudareShakeStrength; laudareShakeSeconds = LaudareShakeSeconds;
            empowermentAfterimageColor = EmpowermentAfterimageColor; empowermentAfterimageAlpha = EmpowermentAfterimageAlpha;
            empowermentAfterimageInterval = EmpowermentAfterimageInterval; empowermentAfterimageSeconds = EmpowermentAfterimageSeconds;
            empowermentAfterimageDrift = EmpowermentAfterimageDrift; empowermentAfterimageSwell = EmpowermentAfterimageSwell;
            barkSeconds = BarkSeconds; barkHurtCooldownSeconds = BarkHurtCooldownSeconds;
            barkLowHealthPercent = BarkLowHealthPercent; barkFontSize = BarkFontSize;
            startCardSeconds = StartCardSeconds; startCardRetrySeconds = StartCardRetrySeconds;
            gearShimmerSeconds = GearShimmerSeconds; gearShimmerStrength = GearShimmerStrength;
            skillGearRadius = SkillGearRadius; skillGearPitch = SkillGearPitch;
            skillGearTeethPerSlot = SkillGearTeethPerSlot; skillGearToothDepth = SkillGearToothDepth;
            skillGearIdlerRadius = SkillGearIdlerRadius;
            skillGearTurnSeconds = SkillGearTurnSeconds; skillGearShiftSeconds = SkillGearShiftSeconds;
            skillGearRatchetBounce = SkillGearRatchetBounce;
            gearTickVolume = GearTickVolume; gearRatchetVolume = GearRatchetVolume;
            meshPercent = MeshPercent; meshBurstSeconds = MeshBurstSeconds; meshGearSize = MeshGearSize;
            meshSparkCount = MeshSparkCount; meshSoundVolume = MeshSoundVolume;
            meshFlashSeconds = MeshFlashSeconds; meshFlashSize = MeshFlashSize;
            laneTapSeconds = LaneTapSeconds; explanationHoldSeconds = ExplanationHoldSeconds;
            explanationTimeScale = ExplanationTimeScale;
            slotForecastResistanceWeight = SlotForecastResistanceWeight; slotForecastBreakBonus = SlotForecastBreakBonus;
            slotForecastMargin = SlotForecastMargin;
            queuePanelWidth = QueuePanelWidth; queueCardSize = QueueCardSize; queueCardSpacing = QueueCardSpacing;
            queueSeamGap = QueueSeamGap; queueSlideSeconds = QueueSlideSeconds;
            queuePanelDockGap = QueuePanelDockGap; queuePanelResolvingBottom = QueuePanelResolvingBottom;
            queuePanelFighterClearance = QueuePanelFighterClearance;
            nextOpponentRingAlpha = NextOpponentRingAlpha; nextOpponentRingPulseSeconds = NextOpponentRingPulseSeconds;
            nextOpponentRingInk = NextOpponentRingInk; nextOpponentRingMargin = NextOpponentRingMargin;
            nextOpponentRingPulseDepth = NextOpponentRingPulseDepth;
            nextReservationSocketAlpha = NextReservationSocketAlpha; nextReservationSocketInk = NextReservationSocketInk;
            reservationSparkSeconds = ReservationSparkSeconds; reservationSparkSize = ReservationSparkSize;
            reservationSparkCount = ReservationSparkCount;
            reservationSparkFavourableInk = ReservationSparkFavourableInk; reservationSparkEvenInk = ReservationSparkEvenInk;
            reservationSparkUnfavourableInk = ReservationSparkUnfavourableInk;
            reservationSparkFavourableScale = ReservationSparkFavourableScale;
            reservationSparkUnfavourableScale = ReservationSparkUnfavourableScale;
            clashApproachSeconds = ClashApproachSeconds; clashApproachDistance = ClashApproachDistance;
            clashImpactSeconds = ClashImpactSeconds; clashReach = ClashReach; clashPushDistance = ClashPushDistance;
            clashKnockbackDistance = ClashKnockbackDistance; clashSideStepDistance = ClashSideStepDistance;
            clashTiltDegrees = ClashTiltDegrees; clashBreakShake = ClashBreakShake; clashBreakShakeSeconds = ClashBreakShakeSeconds;
            clashSettleDip = ClashSettleDip; clashBraceSquash = ClashBraceSquash;
            clashSparkSeconds = ClashSparkSeconds; clashSparkSize = ClashSparkSize; clashSparkCount = ClashSparkCount;
            clashSparkHeadInk = ClashSparkHeadInk; clashSparkTailInk = ClashSparkTailInk;
            minorSlotSpeed = MinorSlotSpeed; majorHitStopScale = MajorHitStopScale; majorCameraPush = MajorCameraPush;
            majorImpactVolume = MajorImpactVolume; majorClashScale = MajorClashScale; turnExtraSecondsCap = TurnExtraSecondsCap;
            largeClashGap = LargeClashGap; deepPierce = DeepPierce; bigUnopposedPercent = BigUnopposedPercent;
            meshChainMinimum = MeshChainMinimum;
            bladeBindGap = BladeBindGap; bladeBindSeconds = BladeBindSeconds; bladeBindSway = BladeBindSway;
            bladeBindPress = BladeBindPress; bladeBindSpring = BladeBindSpring; bladeBindSparks = BladeBindSparks;
            combatTempoStep = CombatTempoStep; combatTempoMaxSpeed = CombatTempoMaxSpeed;
            exhaustedSideSpeed = ExhaustedSideSpeed; exhaustedGapScale = ExhaustedGapScale;
            unopposedStreakStartLength = UnopposedStreakStartLength; unopposedStreakStep = UnopposedStreakStep;
            unopposedStreakMaxSpeed = UnopposedStreakMaxSpeed;
            readCueSeconds = ReadCueSeconds; readCueInk = ReadCueInk; readSparkCount = ReadSparkCount;
            readSparkSize = ReadSparkSize;
            signatureHitStopScale = Safe(signatureHitStopScale, 1f, LegacyShowPacingTuning.MaximumMajorHitStopScale,
                LegacyShowPacingTuning.DefaultSignatureHitStopScale);
            signatureCameraPush = SignatureCameraPush; signatureImpactVolume = SignatureImpactVolume;
            signatureAfterimageSeconds = SignatureAfterimageSeconds; signatureAfterimageAlpha = SignatureAfterimageAlpha;
            tensionHealthPercent = TensionHealthPercent; tensionEaseInSeconds = TensionEaseInSeconds;
            tensionEaseOutSeconds = TensionEaseOutSeconds; tensionBackdropDarkening = TensionBackdropDarkening;
            tensionDimming = TensionDimming; tensionVignette = TensionVignette; tensionVignetteInk = TensionVignetteInk;
            flowLeadMarginPercent = FlowLeadMarginPercent; flowSwellExposure = FlowSwellExposure;
            flowSwellVignetteRelief = FlowSwellVignetteRelief; flowSwellWarmTint = FlowSwellWarmTint;
            flowSwellRiseSeconds = FlowSwellRiseSeconds; flowSwellHoldSeconds = FlowSwellHoldSeconds;
            flowSwellFadeSeconds = FlowSwellFadeSeconds;
            forestAmbienceVolume = ForestAmbienceVolume; forestAmbienceFadeSeconds = ForestAmbienceFadeSeconds;
            forestAmbienceSceneDuck = ForestAmbienceSceneDuck;
            timePressureShare = TimePressureShare; timePressureCameraPush = TimePressureCameraPush;
            stepAnticipationDuration = StepAnticipationDuration; stepTimingWindow = StepTimingWindow;
            stepWindowDecay = StepWindowDecay; stepMinimumWindow = StepMinimumWindow;
            stepDodgeDistance = StepDodgeDistance; stepPressureDistance = StepPressureDistance;
            stepSlowMotionDuration = StepSlowMotionDuration; stepSlowMotionScale = StepSlowMotionScale;
            stepStreakSlowScaleFactor = StepStreakSlowScaleFactor; stepStreakSlowDurationStep = StepStreakSlowDurationStep;
            stepFocusDuration = StepFocusDuration; stepCameraZoom = StepCameraZoom;
            stepBackdropDarkening = StepBackdropDarkening;
            stepSoundVolume = StepSoundVolume; stepRingGlowIntensity = StepRingGlowIntensity;
            battleDriftSpeed = BattleDriftSpeed; battleStagingSeparation = BattleStagingSeparation;
            enemyPlanningSpeedFactors = new Vector3(EnemyPlanningSpeedFactor(0), EnemyPlanningSpeedFactor(1), EnemyPlanningSpeedFactor(2));
            battleDriftMinimumSeparation = BattleDriftMinimumSeparation;
            battlePoseSpeed = BattlePoseSpeed; battleDesaturation = BattleDesaturation; battleCoolTint = BattleCoolTint;
            battleCycleReleaseSeconds = BattleCycleReleaseSeconds;
            glowIntensity = GlowIntensity; glowRadius = GlowRadius; glowDuration = GlowDuration;
            bloomIntensity = BloomIntensity; bloomThreshold = BloomThreshold; flashExposure = FlashExposure;
        }
    }
}
