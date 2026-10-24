using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Leaves the last strike at its usual weight by making earlier strikes quieter and less bright.
    /// This presentation multiplier never changes damage, hit counts, animation length or the combat clock.</summary>
    public static class LegacyMultiHitEmphasis
    {
        public const float DefaultMiddleStrength = .65f;

        /// <param name="hitIndex">The resolved contact's zero-based index within the attacking technique.
        /// Each fighter uses its own technique's hit count, even while sharing the same slot.</param>
        /// <param name="decisive">A break, heavy body hit or finishing blow keeps its full existing presentation,
        /// including when it interrupts a technique before its authored last strike.</param>
        public static float Strength(LegacySkill attackingSkill, int hitIndex, bool decisive,
            bool enabled = true, float middleStrength = DefaultMiddleStrength)
        {
            if (!enabled || decisive || attackingSkill?.Kind != LegacySkillKind.Attack ||
                attackingSkill.AttackCount <= 1 || hitIndex < 0 || hitIndex >= attackingSkill.AttackCount - 1)
                return 1f;

            return float.IsNaN(middleStrength) || float.IsInfinity(middleStrength)
                ? DefaultMiddleStrength : Math.Max(0f, Math.Min(1f, middleStrength));
        }
    }
}
