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

        public static SkillInfoContent For(LegacySkill skill, bool enemy, bool enemyActRecoveryIsPlayerOnly = true)
        {
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(skill);
            LegacySkillInfo info = definition?.Text.Info;
            bool actRecovery = (LegacySkillRoles.Get(skill) & LegacySkillRole.ActRecovery) != 0;
            bool enemyMain = enemy && (!actRecovery || enemyActRecoveryIsPlayerOnly);
            SkillInfoContent content = info != null
                ? new SkillInfoContent(enemyMain && info.EnemyMain != null ? info.EnemyMain : info.Main, info.MainSymbol, info.MainTone,
                    enemy && info.EnemySecondary != null ? info.EnemySecondary : info.Secondary, info.SecondarySymbol,
                    info.SecondaryTone, info.Description)
                : Generic(skill);
            if (definition?.Effect.HasBuff == true)
                content = WithStatusNames(content, definition.Effect);
            if (definition?.Effect.HasActSteal == true)
            {
                LegacySkillEffect effect = definition.Effect;
                bool alreadyNamed = content.Main == LegacySkillLabels.ActStealKeyword;
                content = new SkillInfoContent(LegacySkillLabels.ActStealKeyword, LegacySkillSymbol.Act, LegacySkillTone.Reduction,
                    alreadyNamed ? content.Secondary : content.Main,
                    alreadyNamed ? content.SecondarySymbol : content.MainSymbol,
                    alreadyNamed ? content.SecondaryTone : content.MainTone,
                    info != null ? content.Description : "사용 시 상대 ACT가 " + effect.ActStealThreshold + " 이상이면\n" +
                        LegacySkillLabels.ActStealKeyword + " " + effect.ActStealAmount);
            }
            // Shared effect keywords take precedence over authored copy: queue removal first, then cycle. An authored
            // first badge naming an effect that reads the queue (예약 효과, 사용 효과: 숨고르기 +1, 앞 순환 +1, 일회성, 자동 회피,
            // 다음 턴 제한시간, …) comes next, so a forced keyword pushes it to the second badge instead of hiding it; the
            // authored second badge only fills what is left. A reservation's time cost alone is no such keyword: 두블레's
            // first badge is its 반환.
            LegacyQueueRemovalMode removal = definition?.Effect.QueueRemovalMode ?? LegacyQueueRemovalMode.None;
            bool hasCycle = definition?.Effect.HasCycle == true;
            bool keyword = definition?.Effect.HasActSteal == true ||
                info != null && (definition.Effect.HasReserveGain || definition.Effect.HasUseEffect);
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
            if (enemy && enemyActRecoveryIsPlayerOnly && actRecovery)
                content = new SkillInfoContent(content.Main, content.MainSymbol, content.MainTone, content.Secondary,
                    content.SecondarySymbol, content.SecondaryTone, PlayerOnlyActDescription(content.Description));
            return content;
        }

        private static SkillInfoContent WithStatusNames(SkillInfoContent content, LegacySkillEffect effect)
        {
            string power = effect.BuffPowerPercent > 0
                ? LegacyStatusNames.Label(LegacyStatusKind.PowerUp, effect.BuffPowerPercent, effect.BuffSlots) : null;
            bool vulnerable = effect.BuffProtectionPercent < 0;
            string protection = effect.BuffProtectionPercent != 0
                ? LegacyStatusNames.Label(vulnerable ? LegacyStatusKind.Vulnerability : LegacyStatusKind.Protection,
                    Math.Abs(effect.BuffProtectionPercent), effect.BuffSlots) : null;
            if (power == null && protection == null) return content;
            string granted = power == null ? protection : protection == null ? power : power + ", " + protection;
            // These simple status grants use the same short sentence as the sheet, with live tuned percentages/counts.
            // More elaborate combinations retain their authored description so unrelated conditions are not hidden.
            bool simple = !effect.HasCycle && effect.QueueRemovalMode == LegacyQueueRemovalMode.None &&
                !effect.HasOpponentCondition && !effect.HasOpponentStateCondition && !effect.HasReserveEffect &&
                !effect.HasUseEffect && effect.OpponentResistanceReduction == 0 && effect.ResistanceRecoveryPercent == 0 &&
                effect.BrokenTargetDamagePercent == 0 && !effect.BreaksOpponent && effect.QueuePowerMultiplierCap == 0;
            string description = simple ? "자신에게 " + granted + " 부여" : content.Description;
            if (simple && effect.ActGain > 0)
                description = "- 다음 턴 ACT 회복 +" + effect.ActGain + "\n- " + description;
            LegacySkillSymbol protectionSymbol = vulnerable ? LegacySkillSymbol.Reduction : LegacySkillSymbol.Guard;
            LegacySkillTone protectionTone = vulnerable ? LegacySkillTone.Reduction : LegacySkillTone.Defence;
            if (effect.ActGain > 0)
                return new SkillInfoContent(content.Main, content.MainSymbol, content.MainTone,
                    granted, power != null ? LegacySkillSymbol.Followup : protectionSymbol,
                    power != null ? LegacySkillTone.Followup : protectionTone, description);
            return new SkillInfoContent(power ?? protection,
                power != null ? LegacySkillSymbol.Followup : protectionSymbol,
                power != null ? LegacySkillTone.Followup : protectionTone,
                power != null && protection != null ? protection : string.Empty,
                protectionSymbol, protectionTone, description);
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
