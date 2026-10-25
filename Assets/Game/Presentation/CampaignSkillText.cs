using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    /// <summary>Shared, combat-accurate skill explanations for lobby selection panels.</summary>
    public static class CampaignSkillText
    {
        public static string Effect(LegacySkill skill)
            => LegacyPowerBreakRules.PrototypeEnabled && HasRetiredEffect(skill)
                ? SkillInfoContent.ForPrototype(skill, false).Description
                : LegacySkillDefinitions.Find(skill)?.Text.Effect
                ?? (skill.Kind == LegacySkillKind.Defence
                    ? "같은 칸의 상대 공격 피해를 방어 수치만큼 줄입니다."
                    : skill.AttackCount > 1 ? string.Empty : "표시된 위력으로 한 번 공격합니다.");

        public static string Power(LegacySkill skill) => LegacySkillDefinitions.Find(skill)?.Effect.MirrorOpponentMaxPower == true
            ? "상대 최대" : skill.MinPower == skill.MaxPower ? skill.MinPower.ToString()
            : skill.MinPower + "–" + skill.MaxPower;

        public static string DamageHint(LegacySkill skill) => skill.Kind == LegacySkillKind.Attack
            ? LegacyPowerBreakRules.PrototypeEnabled
                ? "공격끼리 위력 차이 " + LegacyPowerBreakRules.MinimumPowerDifference + " 이상이면 약한 쪽 붕괴\n다음 기술이 파괴된 빈칸에 들어오는 피해는 2배"
                : "공격과 맞붙으면 저항, 그 외에는 체력 피해\n방어 수치만큼 체력 피해가 줄어듭니다" : string.Empty;

        private static bool HasRetiredEffect(LegacySkill skill)
        {
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            return effect != null && (effect.ResistanceRecoveryPercent > 0 || effect.OpponentResistanceReduction > 0 ||
                effect.BreaksOpponent || effect.AheadPowerTarget == LegacyAheadTarget.ResistanceReduction ||
                effect.AheadTimeTarget == LegacyAheadTarget.ResistanceReduction);
        }
    }
}
