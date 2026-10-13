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
        OpponentBreak = 1 << 11,
        /// <summary>조건 피해 배율: more health damage to a target in the row's 상대 상태 조건.</summary>
        ConditionalDamage = 1 << 12,
        /// <summary>Removes earlier queued skills without refunding their ACT.</summary>
        QueueCancel = 1 << 13,
        /// <summary>Removes earlier queued skills and refunds their ACT.</summary>
        QueueReturn = 1 << 14,
        /// <summary>The skill grows in power and ACT cost each time it is used in a battle.</summary>
        Cycle = 1 << 15,
        /// <summary>예약 효과 순환 +1: raises the 순환 count of the cycling skills reserved ahead of it.</summary>
        CycleFeed = 1 << 16,
        /// <summary>예약 효과 숨고르기 +1: reserves a 숨고르기 right after itself, once per turn.</summary>
        AutoBreath = 1 << 17,
        /// <summary>사용 효과 앞 X마다 위력: gains power per breath or kind of skill reserved ahead of it.</summary>
        AheadPower = 1 << 18,
        /// <summary>사용 효과 자동 회피: dodges the same slot's opposing attack without a step.</summary>
        AutoDodge = 1 << 19,
        /// <summary>예약 효과 일회성: sets copies of a 일회성 skill at the front of a lane, once per turn.</summary>
        TemporarySpawn = 1 << 20,
        /// <summary>예약 효과 제한시간 -n초: reserving it spends planning time while the clock runs.</summary>
        PlanningTimeCost = 1 << 21,
        /// <summary>사용 효과 앞 X마다 다음 턴 제한시간: adds next turn's planning time per breath or kind ahead of it.</summary>
        NextTurnTime = 1 << 22,
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
