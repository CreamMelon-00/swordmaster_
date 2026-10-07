using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The planning clock's pressure on the camera (presentation only; the combat rules never read it). Once the
    /// time left falls below a share of the planning time, the camera starts to push in, a touch more the less time is
    /// left; it eases in and back out on real time so a sudden change (넘기기's spent second, the commit, a new turn) never
    /// jolts the picture.</summary>
    public static class LegacyTimePressure
    {
        /// <summary>The real seconds a full push takes to ease in, and to ease back out.</summary>
        public const float EaseInSeconds = .6f, EaseOutSeconds = .35f;

        /// <summary>How hard the clock presses now, 0 to 1: nothing while more than <paramref name="share"/> of the
        /// <paramref name="duration"/> is left, rising evenly to 1 as the last of it runs out. A share or duration of zero
        /// (or less) means none.</summary>
        public static float Target(float remaining, float duration, float share)
        {
            if (!(duration > 0f) || !(share > 0f) || float.IsInfinity(duration) || float.IsNaN(remaining)) return 0f;
            float left = Math.Max(0f, remaining) / duration;
            share = Math.Min(1f, share);
            return left >= share ? 0f : 1f - left / share;
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> over <paramref name="realDelta"/>: a
        /// whole push eases in over <see cref="EaseInSeconds"/> and back out over <see cref="EaseOutSeconds"/>.</summary>
        public static float Step(float current, float target, float realDelta)
        {
            current = Clamp01(current);
            target = Clamp01(target);
            if (!(realDelta > 0f)) return current;
            if (float.IsInfinity(realDelta)) return target;
            return target >= current ? Math.Min(target, current + realDelta / EaseInSeconds)
                : Math.Max(target, current - realDelta / EaseOutSeconds);
        }

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
