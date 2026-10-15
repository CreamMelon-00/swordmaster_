using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Presentation speed over one resolving turn. The rules and network state never read these numbers.</summary>
    public readonly struct LegacyCombatTempoTuning
    {
        public const float DefaultStepPerSlot = .1f, DefaultMaximumSpeed = 1.8f, DefaultExhaustedSpeed = 1f,
            DefaultExhaustedGapScale = .25f;
        public const float MaximumStepPerSlot = .5f, MaximumSpeedLimit = 3f, MaximumExhaustedSpeed = 3f;

        /// <param name="enabled">Whether this replaces the legacy unopposed-streak speed.</param>
        /// <param name="stepPerSlot">Speed added by each completed queue slot (0 to 0.5).</param>
        /// <param name="maximumSpeed">The general speed's cap, before the exhausted-side multiplier (1 to 3).</param>
        /// <param name="exhaustedSpeed">Extra multiplier when exactly one side has no skill (1 to 3).</param>
        /// <param name="exhaustedGapScale">Share of the wait after an attack against an empty side (0 to 1). Omitted:
        /// preserves the original wait.</param>
        public LegacyCombatTempoTuning(bool enabled, float stepPerSlot, float maximumSpeed, float exhaustedSpeed,
            float exhaustedGapScale = 1f)
        {
            Enabled = enabled;
            StepPerSlot = Clamp(stepPerSlot, 0f, MaximumStepPerSlot, DefaultStepPerSlot);
            MaximumSpeed = Clamp(maximumSpeed, 1f, MaximumSpeedLimit, DefaultMaximumSpeed);
            ExhaustedSpeed = Clamp(exhaustedSpeed, 1f, MaximumExhaustedSpeed, DefaultExhaustedSpeed);
            ExhaustedGapScale = Clamp(exhaustedGapScale, 0f, 1f, DefaultExhaustedGapScale);
        }

        /// <summary>Each later slot adds 0.1 speed, up to 1.8. An attack against an empty side keeps that speed and leaves
        /// only a quarter of the usual wait before the next skill.</summary>
        public static LegacyCombatTempoTuning Default => new LegacyCombatTempoTuning(true, DefaultStepPerSlot,
            DefaultMaximumSpeed, DefaultExhaustedSpeed, DefaultExhaustedGapScale);
        public static LegacyCombatTempoTuning Off => new LegacyCombatTempoTuning(false, 0f, 1f, 1f);

        public bool Enabled { get; }
        public float StepPerSlot { get; }
        public float MaximumSpeed { get; }
        public float ExhaustedSpeed { get; }
        public float ExhaustedGapScale { get; }

        private static float Clamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) ? fallback : value < min ? min : value > max ? max : value;
    }

    /// <summary>The same queue slot always has the same playback speed from either side of the match. Breath and guard
    /// are skills; only an Empty action means a side has exhausted its queue. Slot importance never removes this speed;
    /// controllers retain the real time of input windows, hit stops and cinematic slow motion separately.</summary>
    public static class LegacyCombatTempo
    {
        public static float Speed(LegacySlotFacts facts, LegacyCombatTempoTuning tuning)
        {
            if (!tuning.Enabled || !facts.HasSlot) return 1f;
            double speed = Math.Min(tuning.MaximumSpeed, 1d + Math.Max(0, facts.SlotIndex) * (double)tuning.StepPerSlot);
            bool playerEmpty = facts.Clash.PlayerAction == LegacyClashAction.Empty;
            bool enemyEmpty = facts.Clash.EnemyAction == LegacyClashAction.Empty;
            return (float)(speed * (playerEmpty != enemyEmpty ? tuning.ExhaustedSpeed : 1f));
        }

        /// <summary>The share of the gap left after this slot. Only an attack against a genuinely empty side shortens it;
        /// breath, guard, two empty sides and a missing slot keep the full gap. The result is symmetric between peers.</summary>
        public static float GapScale(LegacySlotFacts facts, LegacyCombatTempoTuning tuning)
        {
            if (!tuning.Enabled || !facts.HasSlot) return 1f;
            LegacyClashAction player = facts.Clash.PlayerAction, enemy = facts.Clash.EnemyAction;
            bool unopposedAttack = player == LegacyClashAction.Attack && enemy == LegacyClashAction.Empty ||
                enemy == LegacyClashAction.Attack && player == LegacyClashAction.Empty;
            return unopposedAttack ? tuning.ExhaustedGapScale : 1f;
        }
    }
}
