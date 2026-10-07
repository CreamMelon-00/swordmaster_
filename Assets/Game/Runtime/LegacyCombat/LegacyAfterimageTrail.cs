using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>When a figure wrapped in power leaves its next afterimage, and how strongly (presentation only; the arena
    /// draws the ghosts). While the power is up (a strength above 0: the aura's fade, 0 to 1) a ghost is due every
    /// interval on whatever clock the caller ticks the trail with: the battle's clock in battle, so hit stop, slow motion
    /// and a freeze frame hold or slow it, and real time in a cutscene. A long frame (a hitch) brings one ghost, never a
    /// catch-up burst of the same pose. A ghost's opacity follows the strength, so the trail fades in and out with the
    /// aura. Each ghost drifts back and swells a little over its life by an amount that breathes slowly
    /// (<see cref="Breathe"/>), so a figure that stands still still shows a faint, living echo around her. A ghost's fade
    /// is kept within what the pool drawing them holds (<see cref="FadeSeconds"/>), so none is taken back while it shows.</summary>
    public sealed class LegacyAfterimageTrail
    {
        /// <summary>One slow breath of the echo, in seconds of the trail's clock.</summary>
        public const float BreathPeriod = 1.6f;
        /// <summary>The share of a ghost's drift and swell at the bottom of a breath; the top of it gives all of it.</summary>
        public const float BreathFloor = .55f;

        private float sinceLast, clock;

        /// <summary>How far into its breath the echo is: 0 at the bottom (where it starts), 1 at the top.</summary>
        public float Breath => .5f - .5f * (float)Math.Cos(2.0 * Math.PI * clock / BreathPeriod);

        /// <summary>Lets <paramref name="seconds"/> pass with the power at <paramref name="strength"/> and returns whether a
        /// ghost is due now: one every <paramref name="interval"/> seconds while the strength is above 0, at most one per
        /// call (what a long frame leaves over is kept, short of the next interval). No time (0, less, or not a number)
        /// changes nothing. With the power down none is due, and the count starts afresh when it comes back. An interval
        /// of 0 (or less, or not a number) makes one due every call that lets time pass; an endless one, never.</summary>
        public bool Advance(float seconds, float interval, float strength)
        {
            if (!(seconds > 0f)) return false;
            if (float.IsInfinity(seconds)) seconds = interval > 0f && !float.IsInfinity(interval) ? interval : BreathPeriod;
            clock = (clock + seconds) % BreathPeriod;
            if (!(strength > 0f))
            {
                sinceLast = 0f;
                return false;
            }
            if (float.IsPositiveInfinity(interval)) return false;
            if (!(interval > 0f)) return true;
            sinceLast += seconds;
            if (sinceLast < interval) return false;
            sinceLast %= interval;
            return true;
        }

        /// <summary><paramref name="amount"/> (a ghost's drift or swell) as the breath has it now: from
        /// <see cref="BreathFloor"/> of it at the bottom of a breath to all of it at the top. 0 for none (or not a
        /// number).</summary>
        public float Breathe(float amount)
            => amount > 0f && !float.IsInfinity(amount) ? amount * (BreathFloor + (1f - BreathFloor) * Breath) : 0f;

        /// <summary>A new ghost's opacity: the set <paramref name="opacity"/> times the power's <paramref name="strength"/>,
        /// each kept within 0 to 1 (not a number counts as 0).</summary>
        public static float Opacity(float opacity, float strength) => Unit(opacity) * Unit(strength);

        /// <summary>How long a ghost may take to fade when a pool of <paramref name="poolSize"/> ghosts holds the trail: the
        /// set <paramref name="seconds"/>, but no longer than <paramref name="poolSize"/> - 1 intervals. One is due every
        /// <paramref name="interval"/> (one a little sooner, from what a long frame leaves over), so a longer fade would
        /// have the pool take back a ghost that still shows, cutting the trail's tail. 0 for no fade (0, less, or not a
        /// number) or a pool of one; an interval of 0 or less (one every frame) or an endless one sets no bound.</summary>
        public static float FadeSeconds(float seconds, float interval, int poolSize)
        {
            if (!(seconds > 0f)) return 0f;
            if (!(interval > 0f) || float.IsPositiveInfinity(interval)) return seconds;
            return Math.Min(seconds, Math.Max(0, poolSize - 1) * interval);
        }

        /// <summary>Starts over: no ghost due until a whole interval has passed, at the bottom of a breath.</summary>
        public void Reset() => sinceLast = clock = 0f;

        private static float Unit(float value) => value > 0f ? Math.Min(1f, value) : 0f;
    }
}
