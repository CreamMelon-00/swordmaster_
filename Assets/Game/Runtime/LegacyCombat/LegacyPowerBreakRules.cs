using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>One opt-in prototype rule shared by campaign and versus. Change this value to tune the experiment.</summary>
    public static class LegacyPowerBreakRules
    {
        public const bool PrototypeEnabled = true;
        public const int MinimumPowerDifference = 5;

        public static bool Overwhelms(int strongerPower, int weakerPower)
            => (long)strongerPower - weakerPower >= MinimumPowerDifference;

        internal static int TotalPower(LegacySkill skill, int perHitPower, int attackCount)
            => skill == null || skill.IsWait ? 0 : skill.Kind == LegacySkillKind.Attack
                ? checked(perHitPower * attackCount) : perHitPower;

        internal static int AheadPowerBonus(bool enabled, IReadOnlyList<LegacySkill> queue, int index, LegacySkill skill)
            => enabled && LegacySkillDefinitions.Find(skill)?.Effect.AheadPowerTarget == LegacyAheadTarget.ResistanceReduction
                ? 0 : LegacyKeywordLinks.AheadPowerBonus(queue, index, skill);

        internal static int AheadTimeBonus(bool enabled, IReadOnlyList<LegacySkill> queue, int index, LegacySkill skill)
            => enabled && LegacySkillDefinitions.Find(skill)?.Effect.AheadTimeTarget == LegacyAheadTarget.ResistanceReduction
                ? 0 : LegacyKeywordLinks.AheadTimeBonus(queue, index, skill);
    }
}
