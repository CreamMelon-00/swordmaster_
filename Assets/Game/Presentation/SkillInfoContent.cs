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
            if (info == null && definition != null)
            {
                LegacySkillEffect effect = definition.Effect;
                if (effect.MirrorOpponentMaxPower)
                    content = new SkillInfoContent("상대 최대 위력", LegacySkillSymbol.Sword, LegacySkillTone.Neutral,
                        string.Empty, LegacySkillSymbol.Sword, LegacySkillTone.Neutral,
                        "같은 칸의 상대 기술의 최대 위력으로 공격합니다.\n상대 기술이 없거나 숨고르기면 위력 0.");
                if (effect.BuffPowerFromPreviousOpponentUses)
                    content = new SkillInfoContent("강화", LegacySkillSymbol.Followup, LegacySkillTone.Followup,
                        string.Empty, LegacySkillSymbol.Followup, LegacySkillTone.Neutral,
                        "직전 턴 상대가 사용한 기술 수만큼\n강화 50% 횟수를 얻습니다.");
                if (effect.ConsumeJealousyOnReserve || effect.InflictFixedTurns > 0)
                    content = new SkillInfoContent(effect.ConsumeJealousyOnReserve ? "질투" : "고정",
                        LegacySkillSymbol.Reduction, LegacySkillTone.Reduction,
                        effect.ConsumeJealousyOnReserve && effect.InflictFixedTurns > 0 ? "고정" : string.Empty,
                        LegacySkillSymbol.Guard, LegacySkillTone.Reduction,
                        (effect.ConsumeJealousyOnReserve ? "예약 시 질투를 모두 소모합니다." : string.Empty) +
                        (effect.InflictFixedTurns > 0 ? "\n적중 시 상대에게 고정 " + effect.InflictFixedTurns + " 부여." : string.Empty));
            }
            if (info == null && definition?.Effect.HasBleedingEffect == true)
            {
                var effect = definition.Effect;
                var lines = new System.Collections.Generic.List<string>();
                if (effect.ActGain > 0) lines.Add("다음 턴 ACT 회복 +" + effect.ActGain);
                if (effect.SelfBleedingCount > 0) lines.Add("자신에게 출혈 " + effect.SelfBleedingCount + " 부여");
                if (effect.OpponentBleedingCount > 0) lines.Add("상대에게 출혈 " + effect.OpponentBleedingCount + " 부여");
                if (effect.OpponentBleedingReplacementCount > 0)
                    lines.Add("사용 시 상대가 출혈 상태면 출혈 " + effect.OpponentBleedingReplacementCount + " 부여로 대체");
                if (effect.BleedingReduction > 0) lines.Add("자신의 출혈 최대 " + effect.BleedingReduction + " 감소");
                if (effect.OpponentBleedingRequired > 0)
                    lines.Add("사용 시 상대 출혈 " + effect.OpponentBleedingRequired + " 이상이면 기본 위력 +" + effect.OpponentBleedingPowerBonus);
                if (effect.SelfBleedingRequired > 0)
                    lines.Add("사용 시 자기 출혈 " + effect.SelfBleedingRequired + " 이상이면 기본 위력 +" + effect.SelfBleedingPowerBonus);
                content = new SkillInfoContent(effect.HasBleedingReduction ? "지혈" : "출혈", LegacySkillSymbol.Reduction,
                    LegacySkillTone.Reduction, string.Empty, LegacySkillSymbol.Reduction, LegacySkillTone.Neutral,
                    string.Join("\n", lines));
            }
            if (info == null && definition?.Effect.HasTensionEffect == true)
            {
                LegacySkillEffect effect = definition.Effect;
                var lines = new System.Collections.Generic.List<string>();
                if (effect.ActGain > 0) lines.Add("다음 턴 ACT 회복 +" + effect.ActGain);
                if (effect.SelfTensionCount > 0) lines.Add("사용 시 자신에게 긴장 " + effect.SelfTensionCount + " 부여");
                if (effect.OpponentTensionCount > 0) lines.Add("사용 시 상대에게 긴장 " + effect.OpponentTensionCount + " 부여");
                if (effect.TensionReduction > 0) lines.Add("사용 시 자신의 긴장 최대 " + effect.TensionReduction + " 감소");
                if (effect.OpponentTensionRequired > 0)
                    lines.Add("사용 시 상대 긴장 " + effect.OpponentTensionRequired + " 이상이면 기본 위력 +" + effect.OpponentTensionPowerBonus);
                if (effect.SelfTensionRequired > 0)
                    lines.Add("사용 시 자신의 긴장 " + effect.SelfTensionRequired + " 이상이면 기본 위력 +" + effect.SelfTensionPowerBonus);
                content = new SkillInfoContent("긴장", LegacySkillSymbol.Reduction, LegacySkillTone.Followup,
                    string.Empty, LegacySkillSymbol.Reduction, LegacySkillTone.Neutral, string.Join("\n", lines));
            }
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
            if (definition?.Effect.HasActRecoverySteal == true)
            {
                LegacySkillEffect effect = definition.Effect;
                content = new SkillInfoContent(LegacySkillLabels.ActRecoveryStealKeyword, LegacySkillSymbol.Act,
                    LegacySkillTone.Reduction, "효과 턴당 1회", LegacySkillSymbol.Act, LegacySkillTone.Recovery,
                    info != null ? content.Description : "같은 칸의 상대가 공격 기술을 쓰면\n- 다음 턴 ACT 회복 " +
                        effect.ActRecoveryStealAmount + " 강탈\n- 효과 턴당 1회");
            }
            // Shared effect keywords take precedence over authored copy: queue removal first, then cycle. An authored
            // first badge naming an effect that reads the queue (예약 효과, 사용 효과: 숨고르기 +1, 앞 순환 +1, 일회성, 자동 회피,
            // 다음 턴 제한시간, …) comes next, so a forced keyword pushes it to the second badge instead of hiding it; the
            // authored second badge only fills what is left. A reservation's time cost alone is no such keyword: 두블레's
            // first badge is its 반환.
            LegacyQueueRemovalMode removal = definition?.Effect.QueueRemovalMode ?? LegacyQueueRemovalMode.None;
            bool hasCycle = definition?.Effect.HasCycle == true;
            bool keyword = definition?.Effect.HasActSteal == true || definition?.Effect.HasActRecoverySteal == true ||
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
            if (skill.IsFast || skill.IsCharge)
            {
                string durationDescription = skill.IsFast
                    ? "연속된 고속 기술 두 개가 한 칸을 나눠 씁니다. 하나만 쓰면 남은 반 칸을 쉽니다."
                    : (skill.DurationHalfUnits / 2) + "칸 동안 준비한 뒤 기술을 실행합니다. 준비 중에는 상대의 공격을 받습니다.";
                content = new SkillInfoContent(skill.IsFast ? "고속" : "차지 " + (skill.DurationHalfUnits / 2) + "칸",
                    skill.IsFast ? LegacySkillSymbol.Followup : LegacySkillSymbol.Sword,
                    skill.IsFast ? LegacySkillTone.Followup : LegacySkillTone.HighPower,
                    content.Main, content.MainSymbol, content.MainTone,
                    durationDescription + (string.IsNullOrEmpty(content.Description) ? string.Empty : "\n" + content.Description));
            }
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
