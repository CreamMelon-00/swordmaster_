using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Attack pose sampling and visible displacement. The combat clock, hit events and clip duration stay unchanged.</summary>
    public static class LegacyAttackRhythm
    {
        public const float DefaultStrength = 1f;
        public const float DefaultLungeDistance = .6f;

        /// <summary>Every attack shares the same held preparation and finish. Stances and waits keep their original poses.</summary>
        public static float PosePhase(LegacySkill skill, float phase, float strength)
            => PosePhase(phase, skill?.Kind == LegacySkillKind.Attack ? strength : 0f);

        /// <summary>Only attacks advance their drawn bodies; defence and waits stay where they stand.</summary>
        public static float LungeShare(LegacySkill skill, float phase)
            => skill?.Kind == LegacySkillKind.Attack ? LungeShare(phase) : 0f;

        /// <summary>Cosmetic trail onset only. A skipped frame still captures the authoritative contact separately.</summary>
        public static float TrailStartPhase(LegacySkill skill, float strength)
            => strength > 0f && skill?.Kind == LegacySkillKind.Attack ? .48f : .44f;

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

            // Gather early, release quickly into exact contact, then leave the extended finish readable.
            // Clock -> pose: 0->0, .12->.28, .42->.29, .5->.5, .54->.53, .66->.90, .90->.92, 1->1.
            float pose;
            if (phase <= .12f) pose = Segment(phase, 0f, .12f, 0f, .28f);
            else if (phase <= .42f) pose = Segment(phase, .12f, .42f, .28f, .29f);
            else if (phase <= .5f) pose = Segment(phase, .42f, .5f, .29f, .5f);
            else if (phase <= .54f) pose = Segment(phase, .5f, .54f, .5f, .53f);
            else if (phase <= .66f) pose = Segment(phase, .54f, .66f, .53f, .90f);
            else if (phase <= .90f) pose = Segment(phase, .66f, .90f, .90f, .92f);
            else pose = Segment(phase, .90f, 1f, .92f, 1f);
            return strength == 1f ? pose : phase + (pose - phase) * strength;
        }

        /// <summary>Signed share of a visible lunge: pull back, strike into contact, hold briefly, then recover.
        /// Multiply by a world distance, strength and facing direction; never apply it to the combat position.</summary>
        /// <param name="phase">Original elapsed share of one attack clip, before <see cref="PosePhase"/> retimes its poses.</param>
        public static float LungeShare(float phase)
        {
            if (float.IsNaN(phase) || phase <= 0f || phase >= 1f) return 0f;

            // Release at the end of the gathered pose; keep maximum reach and the original clip end.
            float share;
            if (phase <= .12f) share = Segment(phase, 0f, .12f, 0f, -.3f);
            else if (phase <= .42f) return -.3f;
            else if (phase < .5f) share = Segment(phase, .42f, .5f, -.3f, 1f);
            else if (phase <= .66f) return 1f;
            else if (phase <= .78f) share = Segment(phase, .66f, .78f, 1f, .15f);
            else if (phase <= .92f) share = Segment(phase, .78f, .92f, .15f, .12f);
            else share = Segment(phase, .92f, 1f, .12f, 0f);
            return Math.Max(-.3f, Math.Min(1f, share));
        }

        private static float Segment(float phase, float start, float end, float from, float to)
            => from + (to - from) * ((phase - start) / (end - start));
    }
}
