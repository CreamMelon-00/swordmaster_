using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    [Flags]
    public enum LegacySkillRole
    {
        None = 0,
        ResistanceOnClash = 1 << 0,
        ActRecovery = 1 << 1,
        FollowupPower = 1 << 2,
        DamageReduction = 1 << 3,
        MultiHit = 1 << 4,
        HighPower = 1 << 5,
        VariablePower = 1 << 6,
        ResistanceRecovery = 1 << 7,
        Vulnerability = 1 << 8,
        DirectResistanceDamage = 1 << 9,
        BrokenTargetDamage = 1 << 10,
    }

    /// <summary>Presentation metadata for implemented rules, never a new combat authority.</summary>
    public static class LegacySkillRoles
    {
        internal const string AttackDamageDetail =
            "상대 공격과 대결: 저항 피해 / 방어·빈 행동: 체력 피해 (방어 수치 차감)";

        public static LegacySkillRole Get(LegacySkill skill)
        {
            if (skill == null) return LegacySkillRole.None;
            // Combat's unique effects use real skill IDs, not the name or artwork ID.
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(skill);
            LegacySkillRole roles = LegacySkillRole.None;
            if (skill.Kind == LegacySkillKind.Attack)
            {
                // Slash/Hit/Penetrate share this routing; there is no property-specific
                // resistance multiplier or guard bypass in LegacyQueuedDuel.
                roles |= LegacySkillRole.ResistanceOnClash;
                if (skill.AttackCount > 1) roles |= LegacySkillRole.MultiHit;
                if (definition?.HighPower == true) roles |= LegacySkillRole.HighPower;
                if (definition?.VariablePower == true) roles |= LegacySkillRole.VariablePower;
            }
            if (definition != null) roles |= definition.Effect.Roles;
            return roles;
        }

        public static string GetShortLabel(LegacySkill skill)
        {
            if (skill == null) return string.Empty;
            return LegacySkillDefinitions.Find(skill)?.Text.ShortLabel
                ?? (skill.Kind == LegacySkillKind.Defence ? "방어" : skill.AttackCount > 1 ? "연타" : "단타");
        }

        public static string GetDetail(LegacySkill skill)
        {
            if (skill == null) return string.Empty;
            return LegacySkillDefinitions.Find(skill)?.Text.Detail
                ?? (skill.Kind == LegacySkillKind.Defence ? "방어 수치로 상대 공격을 줄입니다." : AttackDamageDetail);
        }
    }
}
