using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A technique's cut-in (presentation only): the battle holds for a moment while the camera eases toward the
    /// fighter and back. This is how far it is in over the hold: easing in over the first <see cref="EaseShare"/> of it,
    /// holding, easing back out over the last share, 0 at both ends.</summary>
    public static class LegacyCutIn
    {
        /// <summary>The share of the hold spent easing in, and again easing back out.</summary>
        public const float EaseShare = .35f;

        /// <param name="elapsed">Real seconds since the cut-in began.</param>
        /// <param name="seconds">The cut-in's whole hold; 0 (or less) means no cut-in.</param>
        public static float CameraAmount(float elapsed, float seconds)
        {
            if (!(seconds > 0f) || !(elapsed > 0f) || elapsed >= seconds) return 0f;
            float t = elapsed / seconds;
            return Math.Min(Ease(t / EaseShare), Ease((1f - t) / EaseShare));
        }

        private static float Ease(float t)
        {
            t = t > 0f ? Math.Min(1f, t) : 0f;
            return t * t * (3f - 2f * t);
        }
    }
}
