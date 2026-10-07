using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>엘리사's eye (presentation only; the combat rules never read it): when the enemy's queue comes up at the
    /// start of a planning turn, a brass gear behind its first card turns a little and fades while a soft glint passes
    /// across the cards. These are its curves over the effect's normalised time (0 at the reveal, 1 when it is gone) and
    /// the gear's shape, drawn in code (<see cref="GearCoverage"/>). It shows no text: the gear stays a motif of the
    /// picture, never a word (<c>Docs/Narrative.md</c>).</summary>
    public static class LegacyGearShimmer
    {
        public const int Teeth = 10;
        /// <summary>How far the gear turns over the whole effect (degrees): one tooth's pitch, so it ends looking as it
        /// started, a notch on.</summary>
        public const float TurnDegrees = 360f / Teeth;
        /// <summary>The share of the effect over which the gear comes up, and where it starts to fade.</summary>
        public const float RiseShare = .2f, FadeStart = .5f;
        /// <summary>The share of the effect over which the gear settles from <see cref="StartScale"/> to its size.</summary>
        public const float SettleShare = .4f;
        public const float StartScale = .88f;
        /// <summary>When the glint enters the row and when it has left it, as shares of the effect.</summary>
        public const float GlintStart = .18f, GlintEnd = .82f;

        // The gear's shape, in units of its outer radius: the tooth tips, the rim between the teeth, the rim's inner
        // edge, the hub and its axle hole, and the spokes between the hub and the rim. A tooth narrows from its root to its
        // tip; both are half-widths as shares of one tooth's pitch.
        public const float TipRadius = .96f, RootRadius = .8f, RimInnerRadius = .6f, HubRadius = .26f, AxleRadius = .11f;
        public const int Spokes = 5;
        public const float SpokeHalfWidth = .075f;
        private const float ToothRootHalf = .3f, ToothTipHalf = .17f;
        private const float TwoPi = (float)(Math.PI * 2.0);

        /// <summary>Where the effect is, 0 to 1, after <paramref name="elapsed"/> of its <paramref name="seconds"/>
        /// (1 when it has no length).</summary>
        public static float Progress(float elapsed, float seconds)
            => seconds > 0f && !float.IsInfinity(seconds) ? Clamp01(elapsed / seconds) : 1f;

        /// <summary>The gear's opacity, 0 to 1: up quickly, held, then fading out to nothing at the end.</summary>
        public static float GearAlpha(float t)
        {
            t = Clamp01(t);
            if (t >= 1f) return 0f;
            return Smooth(t / RiseShare) * (1f - Smooth((t - FadeStart) / (1f - FadeStart)));
        }

        /// <summary>How far the gear has turned (degrees, clockwise): quickly at first, settling as it fades.</summary>
        public static float GearTurn(float t) => TurnDegrees * EaseOut(Clamp01(t));

        /// <summary>The gear's size against its full size: it settles from a little smaller as it comes up.</summary>
        public static float GearScale(float t) => StartScale + (1f - StartScale) * EaseOut(Clamp01(t) / SettleShare);

        /// <summary>Where the glint is across the row, 0 at its inner end (the first card) to 1 past its outer end; it
        /// waits at 0 before its window and stays at 1 after.</summary>
        public static float GlintPosition(float t) => Smooth((Clamp01(t) - GlintStart) / (GlintEnd - GlintStart));

        /// <summary>The glint's opacity, 0 to 1: a swell over its crossing, nothing before or after.</summary>
        public static float GlintAlpha(float t)
        {
            t = Clamp01(t);
            if (t <= GlintStart || t >= GlintEnd) return 0f;
            float u = (t - GlintStart) / (GlintEnd - GlintStart);
            return (float)Math.Sin(Math.PI * u);
        }

        /// <summary>How much of the point (<paramref name="x"/>, <paramref name="y"/>) the gear covers, 0 to 1, in units of
        /// its outer radius around its centre (y up); edges soften over <paramref name="pixel"/> (one texture pixel in the
        /// same units). Ten trapezoid teeth on a rim, an open ring inside it crossed by five spokes, a hub with an axle
        /// hole.</summary>
        public static float GearCoverage(float x, float y, float pixel)
        {
            pixel = pixel > 0f ? pixel : 1e-4f;
            float radius = (float)Math.Sqrt(x * x + y * y);
            // The tooth's place within its pitch: 0 at a tooth's centre, .5 halfway to the next.
            float pitch = (float)Math.Atan2(y, x) / TwoPi * Teeth;
            float across = Math.Abs(pitch - (float)Math.Floor(pitch) - .5f);
            float tooth = Clamp01((ToothRootHalf - across) / (ToothRootHalf - ToothTipHalf));
            float outerEdge = RootRadius + (TipRadius - RootRadius) * tooth;
            float body = Edge(outerEdge - radius, pixel);
            float rim = Edge(radius - RimInnerRadius, pixel);
            float axle = Edge(radius - AxleRadius, pixel);
            float hub = Edge(HubRadius - radius, pixel) * axle;
            float spoke = 0f;
            for (int index = 0; index < Spokes; index++)
            {
                double angle = TwoPi * index / Spokes + Math.PI / 2.0;
                float along = x * (float)Math.Cos(angle) + y * (float)Math.Sin(angle);
                if (along <= 0f) continue;
                float side = Math.Abs(-x * (float)Math.Sin(angle) + y * (float)Math.Cos(angle));
                spoke = Math.Max(spoke, Edge(SpokeHalfWidth - side, pixel));
            }
            return body * Math.Max(rim, Math.Max(hub, spoke * axle));
        }

        // 1 well inside an edge (positive distance), 0 well outside, a soft step one pixel wide across it.
        private static float Edge(float distance, float pixel) => Clamp01(distance / pixel + .5f);

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
