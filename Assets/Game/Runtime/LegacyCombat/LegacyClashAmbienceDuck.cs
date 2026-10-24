using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A brief real-time dip for the ambience bed. Repeated clashes refresh one envelope rather than stacking.</summary>
    public sealed class LegacyClashAmbienceDuck
    {
        public const float DefaultStrength = .7f;
        public const float AttackSeconds = .02f;
        public const float HoldSeconds = .05f;
        public const float ReleaseSeconds = .15f;
        public const float Duration = AttackSeconds + HoldSeconds + ReleaseSeconds;

        private float age, startAmount;

        public bool IsActive { get; private set; }
        public float Amount { get; private set; }

        public float Gain(float strength) => 1f - Clamp01(strength) * Amount;

        public void Trigger()
        {
            startAmount = Amount;
            age = 0f;
            IsActive = true;
        }

        public void Tick(float realDelta)
        {
            if (!IsActive || !(realDelta > 0f) || float.IsInfinity(realDelta)) return;
            age += realDelta;
            if (age >= Duration) { Clear(); return; }
            if (age < AttackSeconds)
            {
                Amount = startAmount + (1f - startAmount) * Smooth(age / AttackSeconds);
                return;
            }
            if (age < AttackSeconds + HoldSeconds) { Amount = 1f; return; }
            Amount = 1f - Smooth((age - AttackSeconds - HoldSeconds) / ReleaseSeconds);
        }

        public void Clear()
        {
            age = startAmount = Amount = 0f;
            IsActive = false;
        }

        private static float Smooth(float value) => value * value * (3f - 2f * value);
        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
    }
}
