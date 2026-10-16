using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Attack pose sampling and visible displacement. The combat clock, hit events and clip duration stay unchanged.</summary>
    public static class LegacyAttackRhythm
    {
        public const float DefaultStrength = 1f;
        public const float DefaultLungeDistance = .6f;

        /// <param name="phase">Elapsed share of one attack clip. Contact remains at 0.5.</param>
        /// <param name="strength">Blend from the original linear poses (0) to the full rhythm (1).</param>
        public static float PosePhase(float phase, float strength)
        {
            if (float.IsNaN(phase) || phase <= 0f) return 0f;
            if (phase >= 1f) return 1f;
            if (phase == .5f) return .5f;
            strength = float.IsNaN(strength) || float.IsInfinity(strength)
                ? DefaultStrength : Math.Max(0f, Math.Min(1f, strength));
            if (strength == 0f) return phase;

            // Raise early, gather, then strike into contact. Brief contact gives way to a quick follow-through and settle.
            // Clock -> pose: 0->0, .16->.28, .38->.34, .5->.5, .56->.53, .74->.86, 1->1.
            float pose;
            if (phase <= .16f) pose = Segment(phase, 0f, .16f, 0f, .28f);
            else if (phase <= .38f) pose = Segment(phase, .16f, .38f, .28f, .34f);
            else if (phase <= .5f) pose = Segment(phase, .38f, .5f, .34f, .5f);
            else if (phase <= .56f) pose = Segment(phase, .5f, .56f, .5f, .53f);
            else if (phase <= .74f) pose = Segment(phase, .56f, .74f, .53f, .86f);
            else pose = Segment(phase, .74f, 1f, .86f, 1f);
            return strength == 1f ? pose : phase + (pose - phase) * strength;
        }

        /// <summary>Signed share of a visible lunge: pull back, strike into contact, hold briefly, then recover.
        /// Multiply by a world distance, strength and facing direction; never apply it to the combat position.</summary>
        /// <param name="phase">Original elapsed share of one attack clip, before <see cref="PosePhase"/> retimes its poses.</param>
        public static float LungeShare(float phase)
        {
            if (float.IsNaN(phase) || phase <= 0f || phase >= 1f) return 0f;

            // Clock -> lunge: 0->0, .12->-1/6, .34->-.3, .46->.7, .5->1, .58->1, .72->11/30, 1->0.
            float share;
            if (phase <= .12f) share = Segment(phase, 0f, .12f, 0f, -.1666667f);
            else if (phase <= .34f) share = Segment(phase, .12f, .34f, -.1666667f, -.3f);
            else if (phase <= .46f) share = Segment(phase, .34f, .46f, -.3f, .7f);
            else if (phase < .5f) share = Segment(phase, .46f, .5f, .7f, 1f);
            else if (phase <= .58f) return 1f;
            else if (phase <= .72f) share = Segment(phase, .58f, .72f, 1f, .3666667f);
            else share = Segment(phase, .72f, 1f, .3666667f, 0f);
            return Math.Max(-.3f, Math.Min(1f, share));
        }

        private static float Segment(float phase, float start, float end, float from, float to)
            => from + (to - from) * ((phase - start) / (end - start));
    }
}
