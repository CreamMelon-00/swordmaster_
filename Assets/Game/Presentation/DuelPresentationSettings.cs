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
        private float attackInterval = 0.1f;
        [SerializeField, Range(0f, 2f), Tooltip("완료된 스킬과 다음 큐 스킬 사이 대기 시간(게임 시계 초).")]
        private float skillInterval = 0.28f;
        [SerializeField, Range(0f, 0.15f), Tooltip("타격 직후 잠깐 멈추는 시간(실제 초). 0이면 히트 스톱을 끕니다.")]
        private float hitStopDuration = 0.04f;

        [Header("교전 카메라 — 실행 중 즉시 적용")]
        [SerializeField, Range(2.8f, 6f), Tooltip("일반 교전의 화면 크기. 값이 클수록 두 캐릭터와 검을 넓게 보여줍니다. 일반 타격에는 급확대를 하지 않습니다.")]
        private float combatCameraSize = 3.5f;
        [SerializeField, Range(1f, 20f), Tooltip("교전 중심을 따라가는 반응 속도. 실제 시간을 사용하므로 히트 스톱에도 부드럽게 따라갑니다.")]
        private float cameraFollowSharpness = 6f;
        [SerializeField, Range(0f, 1.5f), Tooltip("작은 넉백마다 구도를 다시 잡지 않는 가로 여유(월드 단위). 이 범위를 넘는 이동만 따라갑니다.")]
        private float cameraFollowDeadZone = 0.35f;
        [SerializeField, Range(0f, 0.35f), Tooltip("일반 타격의 작은 방향성 흔들림(월드 단위). 0으로 끕니다. 동시 타격이 겹쳐도 이동량을 더하지 않습니다.")]
        private float impactCameraShake = 0.08f;

        [Header("이동 연출 — 다음 동작부터 적용")]
        [SerializeField, Range(0.5f, 2.5f), Tooltip("피격 밀림과 회피·압박의 이동 거리 배율. 판정 사거리와 캐릭터 최소 간격은 유지합니다.")]
        private float movementDistanceMultiplier = 1.4f;
        [SerializeField, Range(0.5f, 2f), Tooltip("접근·추격·밀림·스텝 이동 속도 배율. 애니메이션 프레임과 공격 간격은 바꾸지 않습니다.")]
        private float movementSpeedMultiplier = 1.2f;

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
        private float finishingSlowMotionSeconds = 1.6f;
        [SerializeField, Range(0.02f, 1f), Tooltip("마무리 일격 슬로모션의 전투 시계 속도. 일반 결정타는 0.15배입니다. 1이면 느려지지 않습니다.")]
        private float finishingSlowMotionScale = 0.12f;
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
        private float stepAnticipationDuration = 0.24f;
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
        private float stepSlowMotionDuration = 0.28f;
        [SerializeField, Range(0.05f, 1f), Tooltip("성공 순간 전투 시계 속도. 1이면 슬로모션 없음. 이동·카메라·잔상은 실제 시간을 사용합니다.")]
        private float stepSlowMotionScale = 0.25f;
        [SerializeField, Range(0.5f, 1f), Tooltip("이번 턴 연속 성공 한 번마다 성공 슬로모션 속도에 곱하는 값(더 느려짐). 5연속까지 적용하며, 붕괴·마무리 일격의 0.15배보다 느려지지 않습니다.")]
        private float stepStreakSlowScaleFactor = 0.88f;
        [SerializeField, Range(0f, 0.2f), Tooltip("이번 턴 연속 성공 한 번마다 성공 슬로모션에 더하는 시간(실제 초). 5연속까지 적용합니다.")]
        private float stepStreakSlowDurationStep = 0.05f;
        [SerializeField, Range(0.05f, 2f), Tooltip("플레이어 카메라 집중·배경 암전 지속 시간(실제 초).")]
        private float stepFocusDuration = 0.36f;
        [SerializeField, Range(0f, 1.5f), Tooltip("성공 순간 카메라가 추가로 당겨지는 양. 0이면 추가 확대 없음.")]
        private float stepCameraZoom = 0.5f;
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
        [SerializeField, Range(4f, 10f), Tooltip("편성이 시작될 때 이보다 가까이 붙어 있으면, 먼저 정상 속도로 이 간격까지 물러섰다가 다시 다가갑니다. 4면 물러서지 않습니다.")]
        private float battleStagingSeparation = 7f;
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
        public float AttackInterval => Safe(attackInterval, 0f, 1f, 0.1f);
        public float SkillInterval => Safe(skillInterval, 0f, 2f, 0.28f);
        public float HitStopDuration => Safe(hitStopDuration, 0f, 0.15f, 0.04f);
        public float CombatCameraSize => Safe(combatCameraSize, 2.8f, 6f, 3.5f);
        public float CameraFollowSharpness => Safe(cameraFollowSharpness, 1f, 20f, 6f);
        public float CameraFollowDeadZone => Safe(cameraFollowDeadZone, 0f, 1.5f, 0.35f);
        public float ImpactCameraShake => Safe(impactCameraShake, 0f, 0.35f, 0.08f);
        public float MovementDistanceMultiplier => Safe(movementDistanceMultiplier, 0.5f, 2.5f, 1.4f);
        public float MovementSpeedMultiplier => Safe(movementSpeedMultiplier, 0.5f, 2f, 1.2f);
        public int DamageTextFontSize => Mathf.Clamp(damageTextFontSize, 80, 240);
        public float CriticalDamageScale => Safe(criticalDamageScale, 1f, 1.8f, 1.3f);
        /// <summary>The share of the target's maximum health one hit must take as health to be decisive (0..100; 0 is off).</summary>
        public int DecisiveHealthDamagePercent => Mathf.Clamp(decisiveHealthDamagePercent, 0, 100);
        public float FinishingSlowMotionSeconds => Safe(finishingSlowMotionSeconds, 0f, 4f, 1.6f);
        public float FinishingSlowMotionScale => Safe(finishingSlowMotionScale, 0.02f, 1f, 0.12f);
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
        public float ForestAmbienceVolume => Safe(forestAmbienceVolume, 0f, 1f, .4f);
        public float ForestAmbienceFadeSeconds => Safe(forestAmbienceFadeSeconds, 0f, 5f, 1.2f);
        public float ForestAmbienceSceneDuck => Safe(forestAmbienceSceneDuck, 0f, 1f, .7f);
        /// <summary>The share of the planning time left below which the camera starts to push in (0..1; 0 is off).</summary>
        public float TimePressureShare => Safe(timePressureShare, 0f, 1f, .3f);
        /// <summary>How much the camera pushes in when the time is all but out, as a share of its size (0..0.15).</summary>
        public float TimePressureCameraPush => Safe(timePressureCameraPush, 0f, .15f, .04f);
        public float StepAnticipationDuration => Safe(stepAnticipationDuration, 0f, 1f, 0.24f);
        public float StepTimingWindow => Safe(stepTimingWindow, 0.03f, 0.25f, 0.1f);
        public float StepWindowDecay => Safe(stepWindowDecay, 0.3f, 1f, LegacyStepTiming.DefaultDecay);
        public float StepMinimumWindow => Safe(stepMinimumWindow, 0.02f, 0.1f, LegacyStepTiming.DefaultMinimumWindow);
        public float StepDodgeDistance => Safe(stepDodgeDistance, 0f, 3f, 1.4f);
        public float StepPressureDistance => Safe(stepPressureDistance, 0f, 3f, 1.1f);
        public float StepSlowMotionDuration => Safe(stepSlowMotionDuration, 0f, 1f, 0.28f);
        public float StepSlowMotionScale => Safe(stepSlowMotionScale, 0.05f, 1f, 0.25f);
        public float StepStreakSlowScaleFactor => Safe(stepStreakSlowScaleFactor, 0.5f, 1f, 0.88f);
        public float StepStreakSlowDurationStep => Safe(stepStreakSlowDurationStep, 0f, 0.2f, 0.05f);
        public float StepFocusDuration => Safe(stepFocusDuration, 0.05f, 2f, 0.36f);
        public float StepCameraZoom => Safe(stepCameraZoom, 0f, 1.5f, 0.5f);
        public float StepBackdropDarkening => Safe(stepBackdropDarkening, 0f, 0.85f, 0.55f);
        public float StepSoundVolume => Safe(stepSoundVolume, 0f, 1f, 0.7f);
        public float StepRingGlowIntensity => Safe(stepRingGlowIntensity, 0f, 3f, 1.6f);
        public float BattleDriftSpeed => Safe(battleDriftSpeed, 0f, 2f, 0.15f);
        public float BattleStagingSeparation => Safe(battleStagingSeparation, 4f, 10f, 7f);
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

        private void OnValidate()
        {
            farMistHeight = FarMistHeight; farMistY = FarMistY;
            farHeight = FarHeight; farY = FarY;
            midHeight = MidHeight; midY = MidY;
            nearHeight = NearHeight; nearY = NearY;
            animationPlaybackSpeed = AnimationPlaybackSpeed;
            attackInterval = AttackInterval; skillInterval = SkillInterval;
            hitStopDuration = HitStopDuration;
            combatCameraSize = CombatCameraSize; cameraFollowSharpness = CameraFollowSharpness;
            cameraFollowDeadZone = CameraFollowDeadZone; impactCameraShake = ImpactCameraShake;
            movementDistanceMultiplier = MovementDistanceMultiplier; movementSpeedMultiplier = MovementSpeedMultiplier;
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
            battleDriftMinimumSeparation = BattleDriftMinimumSeparation;
            battlePoseSpeed = BattlePoseSpeed; battleDesaturation = BattleDesaturation; battleCoolTint = BattleCoolTint;
            battleCycleReleaseSeconds = BattleCycleReleaseSeconds;
            glowIntensity = GlowIntensity; glowRadius = GlowRadius; glowDuration = GlowDuration;
            bloomIntensity = BloomIntensity; bloomThreshold = BloomThreshold; flashExposure = FlashExposure;
        }
    }
}
