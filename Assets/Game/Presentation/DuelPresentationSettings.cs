using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Persistent authoring values; runtime state and combat rules never live in this asset.</summary>
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
