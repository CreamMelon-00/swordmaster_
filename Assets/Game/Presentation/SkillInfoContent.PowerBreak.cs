using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    public readonly partial struct SkillInfoContent
    {
        public static SkillInfoContent ForPrototype(LegacySkill skill, bool enemy,
            bool enemyActRecoveryIsPlayerOnly = true)
            => For(skill, enemy, enemyActRecoveryIsPlayerOnly, LegacyPowerBreakRules.PrototypeEnabled);

        // Keep authored sheet data available for reverting the experiment. These retired effects have no
        // combat authority in power-break mode, so their old badges and descriptions must not promise them.
        private static SkillInfoContent WithoutResistanceEffects(LegacySkill skill,
            LegacySkillDefinition definition, SkillInfoContent content)
        {
            LegacySkillEffect effect = definition?.Effect;
            if (effect == null || !(effect.ResistanceRecoveryPercent > 0 || effect.OpponentResistanceReduction > 0 ||
                effect.BreaksOpponent || effect.AheadPowerTarget == LegacyAheadTarget.ResistanceReduction ||
                effect.AheadTimeTarget == LegacyAheadTarget.ResistanceReduction)) return content;

            SkillInfoContent basic = Generic(skill);
            if (!effect.HasCycle) return basic;
            string description = "순환 1회당 기본 위력 +" + effect.CyclePowerPerUse +
                " (최대 " + effect.CycleMaxCount + "회)";
            if (effect.CycleCostPerUse > 0)
                description += "\n순환 1회당 ACT +" + effect.CycleCostPerUse;
            return new SkillInfoContent("순환", LegacySkillSymbol.Cycle, LegacySkillTone.Cycle,
                basic.Main, basic.MainSymbol, basic.MainTone, description);
        }
    }
}
