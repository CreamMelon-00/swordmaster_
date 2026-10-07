using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A camera shake's strength over time (presentation only; the noise that moves the camera is drawn by the
    /// arena). One shake dies away quadratically over its seconds. Shakes that come while one runs never add up: the
    /// stronger of the new one and what is left of the old one wins, so a barrage of hits (라우다레) shakes the camera at
    /// most as hard as one of them, and it settles once they stop.</summary>
    public sealed class LegacyShake
    {
        private float strength, seconds, elapsed;

        /// <summary>The shake's strength now (world units at the combat framing), 0 once it has died away.</summary>
        public float Amplitude
        {
            get
            {
                if (seconds <= 0f || elapsed >= seconds) return 0f;
                float left = 1f - elapsed / seconds;
                return strength * left * left;
            }
        }

        public bool IsShaking => Amplitude > 0f;

        /// <summary>A new shake of <paramref name="strength"/> dying away over <paramref name="durationSeconds"/>. It replaces
        /// the one in progress only if it is at least as strong as what is left of that one; otherwise nothing changes.
        /// A strength or duration of zero (or less) does nothing.</summary>
        public void Add(float strength, float durationSeconds)
        {
            if (!(strength > 0f) || !(durationSeconds > 0f) || float.IsInfinity(strength) || float.IsInfinity(durationSeconds))
                return;
            if (strength < Amplitude) return;
            this.strength = strength;
            seconds = durationSeconds;
            elapsed = 0f;
        }

        /// <summary>Lets <paramref name="realSeconds"/> pass.</summary>
        public void Advance(float realSeconds)
        {
            if (seconds <= 0f || !(realSeconds > 0f)) return;
            elapsed = float.IsInfinity(realSeconds) ? seconds : Math.Min(seconds, elapsed + realSeconds);
        }

        /// <summary>Stops at once.</summary>
        public void Clear() => strength = seconds = elapsed = 0f;
    }
}
