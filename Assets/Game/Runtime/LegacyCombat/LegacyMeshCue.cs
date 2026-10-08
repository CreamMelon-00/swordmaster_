using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>맞물림's cues (presentation only; the combat rules never read it, <see cref="LegacyMeshing"/> has the rule).
    /// When queueing a skill makes or lengthens a chain, a pair of small brass gears bites and turns on the seam between
    /// its icon and the one before it in the player's queue row, with a burst of sparks from where their teeth meet; a
    /// longer chain gives a slightly bigger burst. When a meshed slot's first hit lands, the same pair flashes larger at the
    /// player, where the step rings would have been. These are their curves over the effect's normalised time (0 when it
    /// starts, 1 when it is gone), the sparks' flight, the gears' meshing and the label a meshed icon carries. No other
    /// text: the gear stays a motif of the picture (<c>Docs/Narrative.md</c>).</summary>
    public static class LegacyMeshCue
    {
        // The tunables' defaults (DuelPresentationSettings): real seconds, HUD units at the 1920x1080 reference.
        public const float DefaultBurstSeconds = .6f, DefaultGearSize = 30f, DefaultFlashSeconds = .45f, DefaultFlashSize = 72f;
        public const int DefaultSparkCount = 10;
        public const float DefaultVolume = .6f;
        /// <summary>The first hit's flash sounds the mesh again, this much quieter than a chain's sound.</summary>
        public const float FlashVolumeShare = .5f;

        /// <summary>Each skill a chain has past two adds this many sparks, up to <see cref="MaximumSparks"/>.</summary>
        public const int SparksPerExtraSkill = 4, MaximumSparks = 40;
        /// <summary>Each skill a chain has past two makes the burst this much bigger, up to <see cref="MaximumBurstScale"/>.</summary>
        public const float GrowthPerExtraSkill = .18f, MaximumBurstScale = 1.6f;
        /// <summary>Each skill a chain has past two raises the mesh sound's pitch this much, up to <see cref="MaximumPitch"/>.</summary>
        public const float PitchPerExtraSkill = .06f, MaximumPitch = 1.24f;

        /// <summary>The pair's teeth (each gear), and how far the pair turns over the whole effect: two teeth.</summary>
        public const int GearTeeth = 8;
        public const float ToothDegrees = 360f / GearTeeth, TurnDegrees = 2f * ToothDegrees;
        /// <summary>How far each gear's centre sits from the seam where their teeth meet, in units of a gear's size: the
        /// two overlap by their teeth (<see cref="LegacyGearShimmer.TipRadius"/>, <see cref="LegacyGearShimmer.RootRadius"/>).</summary>
        public const float MeshOffset = (LegacyGearShimmer.TipRadius + LegacyGearShimmer.RootRadius) * .25f;
        /// <summary>The lower gear starts half a tooth on, so its teeth sit in the upper one's gaps.</summary>
        public const float LowerPhase = ToothDegrees * .5f;

        /// <summary>The gears pop up from <see cref="PopScale"/>, overshoot to <see cref="OvershootScale"/> at
        /// <see cref="OvershootShare"/>, and settle by <see cref="SettleShare"/>.</summary>
        public const float PopScale = .4f, OvershootScale = 1.18f, OvershootShare = .16f, SettleShare = .32f;
        /// <summary>The gears are whole from <see cref="RiseShare"/> and fade from <see cref="FadeStart"/>.</summary>
        public const float RiseShare = .08f, FadeStart = .6f;
        /// <summary>The share of the effect over which the gears glow white as they bite (the flash).</summary>
        public const float GlowShare = .3f;
        /// <summary>The sparks fly between these shares of the effect: a hair after the teeth bite, gone before the gears.</summary>
        public const float SparkStart = .04f, SparkEnd = .72f;
        /// <summary>How far a spark falls over its flight, as a share of its reach.</summary>
        public const float SparkDrop = .35f;
        /// <summary>A spark's streak runs back to where its head was this share of the effect ago, so it trails along its
        /// arc, long as it leaves and shortening as it slows.</summary>
        public const float TrailLag = .08f;
        /// <summary>How far the sparks reach from the seam, in units of a gear's size (before the burst's scale).</summary>
        public const float SparkReach = 1.6f;
        /// <summary>A meshed icon's mark grows to <see cref="MarkPopScale"/> when its bonus appears or rises, then settles
        /// over <see cref="MarkPopSeconds"/> (real seconds).</summary>
        public const float MarkPopScale = 1.35f, MarkPopSeconds = .3f;

        private const float GoldenAngle = 137.50776f;
        private const float InverseGolden = .618034f;

        /// <summary>Where the effect is, 0 to 1, after <paramref name="elapsed"/> of its <paramref name="seconds"/>
        /// (1 when it has no length).</summary>
        public static float Progress(float elapsed, float seconds)
            => seconds > 0f && !float.IsInfinity(seconds) ? Clamp01(elapsed / seconds) : 1f;

        /// <summary>The sparks a chain of <paramref name="chainLength"/> throws: the tuned count for two skills, more for each
        /// skill past two. None for a slot that is not in a chain, or when sparks are tuned off.</summary>
        public static int SparkCount(int baseCount, int chainLength)
        {
            if (chainLength < 2 || baseCount <= 0) return 0;
            long count = baseCount + (long)(chainLength - 2) * SparksPerExtraSkill;
            return (int)Math.Min(MaximumSparks, count);
        }

        /// <summary>How big a chain's burst is against a two-skill chain's: a little bigger for each skill past two.</summary>
        public static float BurstScale(int chainLength)
            => chainLength <= 2 ? 1f : Math.Min(MaximumBurstScale, 1f + (chainLength - 2) * GrowthPerExtraSkill);

        /// <summary>The mesh sound's pitch for a chain: a little higher for each skill past two.</summary>
        public static float Pitch(int chainLength)
            => chainLength <= 2 ? 1f : Math.Min(MaximumPitch, 1f + (chainLength - 2) * PitchPerExtraSkill);

        /// <summary>The gears' opacity, 0 to 1: up at once, held, then fading out to nothing at the end.</summary>
        public static float GearAlpha(float t)
        {
            t = Clamp01(t);
            if (t >= 1f) return 0f;
            return Smooth(t / RiseShare) * (1f - Smooth((t - FadeStart) / (1f - FadeStart)));
        }

        /// <summary>The gears' size against their full size: a pop with an overshoot, then still.</summary>
        public static float GearScale(float t)
        {
            t = Clamp01(t);
            if (t <= OvershootShare) return PopScale + (OvershootScale - PopScale) * EaseOut(t / OvershootShare);
            return OvershootScale + (1f - OvershootScale) * Smooth((t - OvershootShare) / (SettleShare - OvershootShare));
        }

        /// <summary>How far the gears have turned (degrees): quickly as they bite, settling as they fade. The upper gear
        /// turns clockwise by this, the lower counter-clockwise, so their teeth stay meshed.</summary>
        public static float GearTurn(float t) => TurnDegrees * EaseOut(Clamp01(t));

        /// <summary>How white the gears glow, 0 to 1: brightest as they bite, brass again by <see cref="GlowShare"/>.</summary>
        public static float GearGlow(float t)
        {
            t = Clamp01(t);
            return t >= GlowShare ? 0f : 1f - Smooth(t / GlowShare);
        }

        /// <summary>Where spark <paramref name="index"/> flies (degrees, counter-clockwise from the right): spread round the
        /// seam by the golden angle, so any count scatters evenly without a pattern.</summary>
        public static float SparkAngle(int index)
        {
            double angle = (Math.Max(0, index) * (double)GoldenAngle + 23.0) % 360.0;
            return (float)angle;
        }

        /// <summary>How far spark <paramref name="index"/> reaches against the others, .55 to 1.</summary>
        public static float SparkSpeed(int index)
        {
            double share = (Math.Max(0, index) * (double)InverseGolden + .31) % 1.0;
            return (float)(.55 + .45 * share);
        }

        /// <summary>Where a spark is in its own flight, 0 to 1 (0 before it leaves, 1 once it is gone).</summary>
        public static float SparkFlight(float t) => Clamp01((Clamp01(t) - SparkStart) / (SparkEnd - SparkStart));

        /// <summary>Spark <paramref name="index"/>'s head at <paramref name="t"/>, in units of the burst's reach from the
        /// seam (y up): flying out along its angle, slowing, and falling a little.</summary>
        public static float SparkX(int index, float t)
            => (float)Math.Cos(SparkAngle(index) * Math.PI / 180.0) * SparkSpeed(index) * EaseOut(SparkFlight(t));

        public static float SparkY(int index, float t)
        {
            float flight = SparkFlight(t);
            return (float)Math.Sin(SparkAngle(index) * Math.PI / 180.0) * SparkSpeed(index) * EaseOut(flight)
                - SparkDrop * flight * flight;
        }

        /// <summary>A spark's opacity, 0 to 1: bright as it leaves, gone at the end of its flight.</summary>
        public static float SparkAlpha(float t)
        {
            float flight = SparkFlight(t);
            if (flight <= 0f || flight >= 1f) return 0f;
            return (1f - flight) * (1f - flight * .5f);
        }

        /// <summary>When a spark's streak ends behind its head at <paramref name="t"/>: where the head was
        /// <see cref="TrailLag"/> ago (never before the effect began).</summary>
        public static float SparkTrail(float t) => Math.Max(0f, Clamp01(t) - TrailLag);

        /// <summary>A meshed icon's mark scale, <paramref name="remaining"/> real seconds before its pop has settled.</summary>
        public static float MarkScale(float remaining)
        {
            if (!(remaining > 0f)) return 1f;
            float t = 1f - Clamp01(remaining / MarkPopSeconds);
            return MarkPopScale + (1f - MarkPopScale) * EaseOut(t);
        }

        /// <summary>The label under a meshed icon: its bonus, "+30%"; nothing for an icon that is not meshed.</summary>
        public static string BonusLabel(int bonusPercent) => bonusPercent > 0 ? "+" + bonusPercent + "%" : string.Empty;

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
