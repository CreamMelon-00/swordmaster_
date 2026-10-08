using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Presentation-independent motion for the planning clock's travelling gear. The clock always turns while it
    /// runs, then gathers speed only through the last part of its time. Keeping this curve here lets the HUD and its tests
    /// agree without making combat rules depend on presentation.</summary>
    public static class LegacyTimerGear
    {
        public const float DangerShare = .3f;
        public const float CriticalShare = .1f;
        public const float BaseDegreesPerSecond = 54f;
        public const float MaximumDegreesPerSecond = 300f;
        public const float DangerVentInterval = .8f;
        public const float CriticalVentInterval = .28f;

        /// <summary>0 before the dangerous last <see cref="DangerShare"/>, rising smoothly to 1 at the deadline.</summary>
        public static float Urgency(float remaining, float duration)
        {
            if (!(duration > 0f) || float.IsInfinity(duration) || float.IsNaN(remaining)) return 0f;
            float left = Math.Max(0f, remaining) / duration;
            if (left >= DangerShare) return 0f;
            float urgency = 1f - left / DangerShare;
            return urgency * urgency * (3f - 2f * urgency);
        }

        public static float DegreesPerSecond(float remaining, float duration)
            => BaseDegreesPerSecond + (MaximumDegreesPerSecond - BaseDegreesPerSecond) * Urgency(remaining, duration);

        /// <summary>How often the valve breathes once time is dangerous. Infinity means no repeating steam yet.</summary>
        public static float VentInterval(float remaining, float duration)
        {
            float urgency = Urgency(remaining, duration);
            if (!(urgency > 0f)) return float.PositiveInfinity;
            return DangerVentInterval + (CriticalVentInterval - DangerVentInterval) * urgency;
        }

        /// <summary>True only on the frame the remaining share crosses <paramref name="share"/> downward.</summary>
        public static bool Crossed(float previousRemaining, float remaining, float duration, float share)
        {
            if (!(duration > 0f) || !(share > 0f) || float.IsInfinity(duration) || float.IsNaN(previousRemaining) ||
                float.IsNaN(remaining)) return false;
            float threshold = duration * Math.Min(1f, share);
            return previousRemaining > threshold && remaining <= threshold;
        }
    }
}
