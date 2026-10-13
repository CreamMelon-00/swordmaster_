using System;
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
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(skill);
            LegacySkillInfo info = definition?.Text.Info;
            SkillInfoContent content = info != null
                ? new SkillInfoContent(enemy && info.EnemyMain != null ? info.EnemyMain : info.Main, info.MainSymbol, info.MainTone,
                    enemy && info.EnemySecondary != null ? info.EnemySecondary : info.Secondary, info.SecondarySymbol,
                    info.SecondaryTone, info.Description)
                : Generic(skill);
            // Shared effect keywords take precedence over authored copy: queue removal first, then cycle. An authored
            // first badge naming an effect that reads the queue (예약 효과, 사용 효과: 숨고르기 +1, 앞 순환 +1, 일회성, 자동 회피,
            // 다음 턴 제한시간, …) comes next, so a forced keyword pushes it to the second badge instead of hiding it; the
            // authored second badge only fills what is left. A reservation's time cost alone is no such keyword: 두블레's
            // first badge is its 반환.
            LegacyQueueRemovalMode removal = definition?.Effect.QueueRemovalMode ?? LegacyQueueRemovalMode.None;
            bool hasCycle = definition?.Effect.HasCycle == true;
            bool keyword = info != null && (definition.Effect.HasReserveGain || definition.Effect.HasUseEffect);
            string keptText = keyword ? content.Main : content.Secondary;
            LegacySkillSymbol keptSymbol = keyword ? content.MainSymbol : content.SecondarySymbol;
            LegacySkillTone keptTone = keyword ? content.MainTone : content.SecondaryTone;
            if (removal != LegacyQueueRemovalMode.None)
                content = new SkillInfoContent(removal == LegacyQueueRemovalMode.Cancel ? "취소" : "반환",
                    removal == LegacyQueueRemovalMode.Cancel ? LegacySkillSymbol.Reduction : LegacySkillSymbol.Recovery,
                    removal == LegacyQueueRemovalMode.Cancel ? LegacySkillTone.Reduction : LegacySkillTone.Recovery,
                    hasCycle ? "순환" : keptText,
                    hasCycle ? LegacySkillSymbol.Cycle : keptSymbol,
                    hasCycle ? LegacySkillTone.Cycle : keptTone, content.Description);
            else if (hasCycle)
                content = new SkillInfoContent("순환", LegacySkillSymbol.Cycle, LegacySkillTone.Cycle,
                    keptText, keptSymbol, keptTone, content.Description);
            if (enemy && (LegacySkillRoles.Get(skill) & LegacySkillRole.ActRecovery) != 0)
                content = new SkillInfoContent(content.Main, content.MainSymbol, content.MainTone, content.Secondary,
                    content.SecondarySymbol, content.SecondaryTone, PlayerOnlyActDescription(content.Description));
            return content;
        }

        private static string PlayerOnlyActDescription(string description)
        {
            const string result = "- 다음 턴 ACT 회복 +";
            int start = description.IndexOf(result, StringComparison.Ordinal);
            if (start < 0) return description + "\nACT 회복은 플레이어 전용";
            int end = description.IndexOfAny(new[] { '\r', '\n' }, start);
            if (end < 0) end = description.Length;
            return description.Insert(end, " (플레이어 전용)");
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
            // The top row already states ACT, power, type and hit count. Basic skills add no separate rule.
            string description = string.Empty;
            return new SkillInfoContent(main, mainSymbol, mainTone, secondary, LegacySkillSymbol.Hits, secondaryTone, description);
        }
    }
}
