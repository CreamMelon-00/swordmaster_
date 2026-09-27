using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    /// <summary>Shared, combat-accurate skill explanations for lobby selection panels.</summary>
    public static class CampaignSkillText
    {
        public static string Purpose(LegacySkill skill)
        {
            switch (skill.Id)
            {
                case 1: return "다음 턴 ACT 회복";
                case 3: case 9: return "뒤에 놓인 행동 강화";
                case 7: return "방어 + 조건부 ACT 회복";
                case 8: return "방어 + 이후 피해 감소";
                case 10: return "바로 다음 행동 강화";
                case 12: return "ACT 회복 + 다음 칸 취약";
                case 19: return "방어 + 저항 회복";
                case 42: return "방어 대응 + 저항 감소·ACT 회복";
                default: return LegacySkillRoles.GetShortLabel(skill);
            }
        }

        public static string Effect(LegacySkill skill)
        {
            switch (skill.Id)
            {
                case 1: return "사용하면 다음 턴 ACT를 1 더 회복합니다.";
                case 2: case 5: case 15: return "총 위력을 두 번에 나누어 공격합니다. 위력이 2배가 되는 것은 아닙니다.";
                case 3: return "이 기술 뒤의 행동 3개는 공격·방어 위력이 10% 높아집니다. 이번 턴만 적용됩니다.";
                case 6: return "총 위력을 세 번에 나누어 공격합니다. 위력이 3배가 되는 것은 아닙니다.";
                case 7: return "같은 순서의 상대 공격이 타격 속성이면, 다음 턴 ACT를 2 더 회복합니다.";
                case 8: return "이 기술 뒤에서 받는 피해가 30% 줄어듭니다. 이번 턴의 이후 행동 최대 10개에 적용됩니다.";
                case 9: return "이 기술 뒤 행동의 공격·방어 위력이 3% 높아집니다. 이번 턴의 이후 행동 최대 10개에 적용됩니다.";
                case 10: return "이번 턴의 바로 다음 칸에서 공격·방어 위력이 30% 높아집니다. 턴을 넘겨 유지되지 않습니다.";
                case 12: return "사용하면 다음 턴 ACT를 3 더 회복합니다. 이번 턴의 바로 다음 칸에서 받는 피해 배율이 50% 늘어납니다. 총 위력을 두 번에 나누어 공격합니다.";
                case 16: return "한 번 공격하며 위력 차이가 큽니다. 높은 위력이 항상 나오지는 않습니다.";
                case 19: return "기술 시작 시 최대 저항의 10%를 반올림해 회복합니다. 최대치를 넘지 않으며, 방어 수치로 같은 칸의 상대 공격을 줄입니다.";
                case 42: return "기술 시작 시 같은 칸의 상대가 방어이면 저항을 직접 20 줄이고, 다음 턴 ACT를 3 더 회복합니다. 초과 감소는 체력 피해로 이어지지 않습니다.";
                default: return skill.Kind == LegacySkillKind.Defence
                    ? "방어 수치만큼 같은 순서의 상대 공격 피해를 줄입니다."
                    : skill.AttackCount > 1 ? "총 위력을 " + skill.AttackCount + "번에 나누어 공격합니다."
                    : "표시된 위력으로 한 번 공격합니다.";
            }
        }

        public static string Power(LegacySkill skill) => skill.MinPower == skill.MaxPower ? skill.MinPower.ToString()
            : skill.MinPower + "–" + skill.MaxPower;

        public static string DamageHint(LegacySkill skill) => skill.Kind == LegacySkillKind.Attack
            ? "공격과 맞붙으면 저항, 그 외에는 체력 피해\n방어 수치만큼 체력 피해가 줄어듭니다" : string.Empty;
    }
}
