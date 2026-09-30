using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    /// <summary>The two keyword badges and the effect summary of a skill detail, free of any UI object.
    /// Skills with a definition use its copy; the rest use the generic text for their kind and hits.</summary>
    public readonly struct SkillInfoContent
    {
        public readonly string Main, Secondary, Description;
        public readonly LegacySkillSymbol MainSymbol, SecondarySymbol;
        public readonly LegacySkillTone MainTone, SecondaryTone;

        private SkillInfoContent(string main, LegacySkillSymbol mainSymbol, LegacySkillTone mainTone,
            string secondary, LegacySkillSymbol secondarySymbol, LegacySkillTone secondaryTone, string description)
        {
            Main = main; MainSymbol = mainSymbol; MainTone = mainTone;
            Secondary = secondary; SecondarySymbol = secondarySymbol; SecondaryTone = secondaryTone;
            Description = description;
        }

        public static SkillInfoContent For(LegacySkill skill, bool enemy)
        {
            LegacySkillInfo info = LegacySkillDefinitions.Find(skill)?.Text.Info;
            SkillInfoContent content = info != null
                ? new SkillInfoContent(enemy && info.EnemyMain != null ? info.EnemyMain : info.Main, info.MainSymbol, info.MainTone,
                    enemy && info.EnemySecondary != null ? info.EnemySecondary : info.Secondary, info.SecondarySymbol,
                    info.SecondaryTone, info.Description)
                : Generic(skill);
            if (enemy && (LegacySkillRoles.Get(skill) & LegacySkillRole.ActRecovery) != 0)
                content = new SkillInfoContent(content.Main, content.MainSymbol, content.MainTone, content.Secondary,
                    content.SecondarySymbol, content.SecondaryTone, content.Description + "\nACT 회복은 플레이어 전용");
            return content;
        }

        private static SkillInfoContent Generic(LegacySkill skill)
        {
            bool defence = skill.Kind == LegacySkillKind.Defence;
            bool strong = (LegacySkillRoles.Get(skill) & LegacySkillRole.HighPower) != 0;
            string main = defence ? "수치 방어" : strong ? "고화력" : skill.AttackCount > 1 ? "분할 연타" : "단타";
            LegacySkillSymbol mainSymbol = defence ? LegacySkillSymbol.Guard : strong ? LegacySkillSymbol.Sword : LegacySkillSymbol.Hits;
            LegacySkillTone mainTone = defence ? LegacySkillTone.Defence : strong ? LegacySkillTone.HighPower
                : skill.AttackCount > 1 ? LegacySkillTone.MultiHit : LegacySkillTone.Neutral;
            string secondary = string.Empty;
            LegacySkillTone secondaryTone = LegacySkillTone.Neutral;
            if (strong)
            {
                secondary = skill.AttackCount > 1 ? "분할 연타" : "단타";
                secondaryTone = skill.AttackCount > 1 ? LegacySkillTone.MultiHit : LegacySkillTone.Neutral;
            }
            string description = defence ? "같은 순서의 상대 공격 피해를\n방어 수치만큼 줄입니다."
                : skill.AttackCount > 1 ? "위력을 " + skill.AttackCount + "회로 나눠 공격\n소수점 버림 · 한 타 최소 1"
                : "표시 위력으로 1회 공격";
            return new SkillInfoContent(main, mainSymbol, mainTone, secondary, LegacySkillSymbol.Hits, secondaryTone, description);
        }
    }
}
