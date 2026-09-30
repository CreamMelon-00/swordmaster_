using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    /// <summary>Shared, combat-accurate skill explanations for lobby selection panels.</summary>
    public static class CampaignSkillText
    {
        public static string Purpose(LegacySkill skill)
            => LegacySkillDefinitions.Find(skill)?.Text.Purpose ?? LegacySkillRoles.GetShortLabel(skill);

        public static string Effect(LegacySkill skill)
            => LegacySkillDefinitions.Find(skill)?.Text.Effect
                ?? (skill.Kind == LegacySkillKind.Defence
                    ? "방어 수치만큼 같은 순서의 상대 공격 피해를 줄입니다."
                    : skill.AttackCount > 1 ? "총 위력을 " + skill.AttackCount + "번에 나누어 공격합니다."
                    : "표시된 위력으로 한 번 공격합니다.");

        public static string Power(LegacySkill skill) => skill.MinPower == skill.MaxPower ? skill.MinPower.ToString()
            : skill.MinPower + "–" + skill.MaxPower;

        public static string DamageHint(LegacySkill skill) => skill.Kind == LegacySkillKind.Attack
            ? "공격과 맞붙으면 저항, 그 외에는 체력 피해\n방어 수치만큼 체력 피해가 줄어듭니다" : string.Empty;
    }
}
