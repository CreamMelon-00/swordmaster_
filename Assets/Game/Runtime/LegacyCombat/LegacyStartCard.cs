using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The duel start card's timeline (presentation only; the combat rules never read it). At the start of a
    /// battle a short title card holds it: letterbox bars slide in, the two fighters' silhouettes slide in from the sides
    /// with their names, the crossed swords between them close and settle, everything holds, then it all fades out while
    /// the bars slide away, and the first planning turn begins. Every stage is a share of the card's length, so a short
    /// card (a retry's) plays the same moves faster. It runs on the real seconds the caller hands it; <see cref="Skip"/>
    /// ends it at once.</summary>
    public sealed class LegacyStartCard
    {
        /// <summary>The share of the card over which the bars slide in.</summary>
        public const float BarsShare = .14f;
        /// <summary>When the silhouettes start to slide in and when they are in, as shares of the card.</summary>
        public const float FiguresStart = .04f, FiguresEnd = .3f;
        /// <summary>When the names start to show and when they are fully shown.</summary>
        public const float NamesStart = .16f, NamesEnd = .36f;
        /// <summary>When the crossed swords start to close and when they have settled.</summary>
        public const float MarkStart = .2f, MarkEnd = .4f;
        /// <summary>The closing share of the card: everything fades out and the bars slide away.</summary>
        public const float ExitShare = .2f;

        private float seconds, elapsed;

        /// <summary>Whether the card is up (holding the battle).</summary>
        public bool IsShowing => seconds > 0f && elapsed < seconds;
        /// <summary>The card's whole length in real seconds (0 when none is up).</summary>
        public float Seconds => IsShowing ? seconds : 0f;
        /// <summary>The real seconds the card has been up.</summary>
        public float Elapsed => IsShowing ? elapsed : 0f;
        /// <summary>Where the card is, 0 to 1 (0 when none is up).</summary>
        public float Progress => IsShowing ? elapsed / seconds : 0f;
        /// <summary>Whether the card has begun its exit: what it covers may come back underneath it.</summary>
        public bool IsLeaving => IsShowing && Progress >= 1f - ExitShare;
        /// <summary>1 until the exit begins, then easing to 0 as the card ends.</summary>
        public float Presence => IsShowing ? 1f - Smooth((Progress - (1f - ExitShare)) / ExitShare) : 0f;
        /// <summary>How far the letterbox bars are in, 0 to 1.</summary>
        public float BarsAmount => Smooth(Progress / BarsShare) * Presence;
        /// <summary>How far the silhouettes have slid in, 0 to 1 (their opacity follows <see cref="FiguresOpacity"/>).</summary>
        public float FiguresAmount => EaseOut(Window(FiguresStart, FiguresEnd));
        public float FiguresOpacity => Smooth(Window(FiguresStart, FiguresEnd)) * Presence;
        public float NamesOpacity => Smooth(Window(NamesStart, NamesEnd)) * Presence;
        /// <summary>How far the crossed swords have closed and settled, 0 to 1.</summary>
        public float MarkAmount => EaseOut(Window(MarkStart, MarkEnd));
        public float MarkOpacity => Smooth(Window(MarkStart, MarkStart + (MarkEnd - MarkStart) * .5f)) * Presence;

        /// <summary>Puts the card up for <paramref name="length"/> real seconds; zero (or less) puts none up.</summary>
        public void Begin(float length)
        {
            seconds = length > 0f && !float.IsInfinity(length) ? length : 0f;
            elapsed = 0f;
        }

        /// <summary>Lets <paramref name="realSeconds"/> pass. Returns true on the call that ends the card.</summary>
        public bool Advance(float realSeconds)
        {
            if (!IsShowing || !(realSeconds > 0f)) return false;
            elapsed = float.IsInfinity(realSeconds) ? seconds : Math.Min(seconds, elapsed + realSeconds);
            if (elapsed < seconds) return false;
            seconds = elapsed = 0f;
            return true;
        }

        /// <summary>Ends the card at once (skipped, or the battle left).</summary>
        public void Skip() => seconds = elapsed = 0f;

        private float Window(float start, float end) => IsShowing ? Clamp01((Progress - start) / (end - start)) : 0f;

        private static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float EaseOut(float t)
        {
            t = Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
