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
    }

    /// <summary>Presentation metadata for implemented rules, never a new combat authority.</summary>
    public static class LegacySkillRoles
    {
        private const string AttackDamageDetail =
            "상대 공격과 대결: 저항 피해 / 방어·빈 행동: 체력 피해 (방어 수치 차감)";

        public static LegacySkillRole Get(LegacySkill skill)
        {
            if (skill == null) return LegacySkillRole.None;
            LegacySkillRole roles = LegacySkillRole.None;
            if (skill.Kind == LegacySkillKind.Attack)
            {
                // Slash/Hit/Penetrate share this routing; there is no property-specific
                // resistance multiplier or guard bypass in LegacyQueuedDuel.
                roles |= LegacySkillRole.ResistanceOnClash;
                if (skill.AttackCount > 1) roles |= LegacySkillRole.MultiHit;
                switch (skill.Id)
                {
                    case 2:
                    case 4:
                    case 6:
                    case 21:
                        roles |= LegacySkillRole.HighPower;
                        break;
                    case 16:
                        roles |= LegacySkillRole.VariablePower;
                        break;
                }
            }

            // Combat's unique effects use real skill IDs, not the name or artwork ID.
            switch (skill.Id)
            {
                case 1:
                case 7:
                    roles |= LegacySkillRole.ActRecovery;
                    break;
                case 3:
                case 9:
                case 10:
                    roles |= LegacySkillRole.FollowupPower;
                    break;
                case 8:
                    roles |= LegacySkillRole.DamageReduction;
                    break;
                case 12:
                    roles |= LegacySkillRole.ActRecovery | LegacySkillRole.Vulnerability;
                    break;
                case 19:
                    roles |= LegacySkillRole.ResistanceRecovery;
                    break;
                case 42:
                    roles |= LegacySkillRole.ActRecovery | LegacySkillRole.DirectResistanceDamage;
                    break;
            }
            return roles;
        }

        public static string GetShortLabel(LegacySkill skill)
        {
            if (skill == null) return string.Empty;
            switch (skill.Id)
            {
                case 1: return "ACT 회복";
                case 2: return "고화력·연타";
                case 3: return "후속 위력";
                case 4: return "고화력";
                case 5: return "연타";
                case 6: return "고화력·연타";
                case 7: return "조건부 ACT";
                case 8: return "피해 감소";
                case 9: return "후속 위력";
                case 10: return "다음 칸 강화";
                case 12: return "ACT 회복·취약";
                case 14: return "단타";
                case 15: return "연타";
                case 16: return "변동 한방";
                case 17: return "방어";
                case 19: return "저항 회복";
                case 21: return "고화력";
                case 32: return "방어";
                case 42: return "방어 대응·저항 감소";
                default:
                    return skill.Kind == LegacySkillKind.Defence ? "방어" : skill.AttackCount > 1 ? "연타" : "단타";
            }
        }

        public static string GetDetail(LegacySkill skill)
        {
            if (skill == null) return string.Empty;
            switch (skill.Id)
            {
                case 1: return "다음 턴 ACT 회복량 +1.\n" + AttackDamageDetail;
                case 2: return "총 위력을 2회로 나누는 강한 공격.\n" + AttackDamageDetail;
                case 3: return "이번 턴의 이후 3슬롯의 공격·방어 위력 +10%.\n" + AttackDamageDetail;
                case 4: return "한 번에 위력을 싣는 단타 공격.\n" + AttackDamageDetail;
                case 5: return "총 위력을 2회로 나누어 공격.\n" + AttackDamageDetail;
                case 6: return "총 위력을 3회로 나누는 강한 공격.\n" + AttackDamageDetail;
                case 7: return "방어. 상대가 타격 기술이면 다음 턴 ACT 회복량 +2.";
                case 8: return "방어. 이번 턴의 이후 슬롯에서 받는 피해 -30% (최대 10슬롯).";
                case 9: return "방어. 이번 턴의 이후 슬롯 공격·방어 위력 +3% (최대 10슬롯).";
                case 10: return "이번 턴의 바로 다음 1슬롯 공격·방어 위력 +30%.\n" + AttackDamageDetail;
                case 12: return "사용하면 다음 턴 ACT 회복량 +3 (플레이어 전용). 이번 턴의 바로 다음 1슬롯에서 받는 피해 배율 +50%. 총 위력을 2회로 나누어 공격.\n" + AttackDamageDetail;
                case 14: return "부가 효과 없는 단타 공격.\n" + AttackDamageDetail;
                case 15: return "부가 효과 없는 2연타 공격.\n" + AttackDamageDetail;
                case 16: return "위력 편차가 큰 단타 공격. 높은 위력이 보장되지는 않습니다.\n" + AttackDamageDetail;
                case 17:
                case 32:
                    return "부가 효과 없이 방어 수치로 상대 공격을 줄입니다.";
                case 19: return "방어. 기술 시작 시 최대 저항의 10%를 반올림해 회복합니다 (최대치까지).";
                case 21: return "한 번에 위력을 싣는 강한 찌르기.\n" + AttackDamageDetail;
                case 42: return "기술 시작 시 같은 슬롯 상대가 방어이면 저항을 직접 20 감소시키고 다음 턴 ACT 회복량 +3 (ACT는 플레이어 전용). 초과 저항 감소는 체력 피해로 이어지지 않습니다.\n" + AttackDamageDetail;
                default:
                    return skill.Kind == LegacySkillKind.Defence ? "방어 수치로 상대 공격을 줄입니다." : AttackDamageDetail;
            }
        }
    }
}
