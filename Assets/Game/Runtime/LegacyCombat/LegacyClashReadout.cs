namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The short explanation above the current pair of queue cards. It names actual events after the first hit,
    /// rather than claiming a winner from rolled power: both attacks can deal damage in the same exchange.</summary>
    public static class LegacyClashReadout
    {
        public const string Pending = "맞붙는 중";

        public static string Pair(LegacySkill player, LegacySkill enemy, string opponentLabel = "상대")
            => "내 " + (player?.Name ?? "빈자리") + "  ↔  " + opponentLabel + " " + (enemy?.Name ?? "빈자리");

        /// <summary>The actual opponent of a live slot. An enemy who just fell remains the slot's target until completion;
        /// querying EnemyIndexForSlot again after that hit could already return the next living opponent instead.</summary>
        public static string OpponentLabel(ILegacyDuelView session)
        {
            if (!(session is ILegacyEnemyRosterView roster) || roster.Enemies.Count <= 1) return "상대";
            int index = session.CurrentSlot != null ? roster.ActiveEnemyIndex
                : roster.EnemyIndexForSlot(session.CurrentClash.SlotIndex);
            if (index < 0 || index >= roster.Enemies.Count) return "상대";
            return "상대 " + (index + 1) + "(" + roster.EnemyName(index) + ")";
        }

        public static string Result(LegacySlotFacts facts)
        {
            LegacyClashInput input = facts.Clash;
            if (!input.HasSlot) return string.Empty;
            if (!input.Decided) return Pending;
            // The breaking hit can deal zero health damage; the next hit may then take health. Preserve both stages.
            if (input.PlayerBroken && input.EnemyBroken)
            {
                if (facts.PlayerHealthLost > 0 && facts.EnemyHealthLost > 0) return "서로 저항 붕괴·체력 손실";
                return Join("서로 저항 붕괴", Join(Loss("내", facts.PlayerHealthLost, 0),
                    Loss("상대", facts.EnemyHealthLost, 0)));
            }
            if (input.EnemyBroken)
                return Join(facts.EnemyHealthLost > 0 ? "상대 저항 붕괴·체력 손실" : "상대 저항 붕괴",
                    Loss("내", facts.PlayerHealthLost, facts.PlayerResistanceLost));
            if (input.PlayerBroken)
                return Join(facts.PlayerHealthLost > 0 ? "내 저항 붕괴·체력 손실" : "내 저항 붕괴",
                    Loss("상대", facts.EnemyHealthLost, facts.EnemyResistanceLost));

            bool playerDodged = input.PlayerDodged && input.EnemyAction == LegacyClashAction.Attack;
            bool enemyDodged = input.EnemyDodged && input.PlayerAction == LegacyClashAction.Attack;
            if (playerDodged && enemyDodged) return "서로 회피 성공";
            if (playerDodged)
                return Join("내 회피 성공", Loss("상대", facts.EnemyHealthLost, facts.EnemyResistanceLost));
            if (enemyDodged)
                return Join("상대 회피 성공", Loss("내", facts.PlayerHealthLost, facts.PlayerResistanceLost));

            LegacyClash clash = LegacyClashMotion.Classify(input);
            if (clash.Kind == LegacyClashKind.GuardPierced)
            {
                bool playerAttacks = input.PlayerAction == LegacyClashAction.Attack;
                bool landed = playerAttacks ? facts.EnemyHealthLost > 0 || facts.EnemyResistanceLost > 0
                    : facts.PlayerHealthLost > 0 || facts.PlayerResistanceLost > 0;
                if (!landed) return "피해 없음";
                return playerAttacks ? "상대 방어 관통" : "내 방어 관통";
            }
            if (clash.Kind == LegacyClashKind.GuardHeld)
                return input.PlayerAction == LegacyClashAction.Guard ? "내 방어 성공" : "상대 방어 성공";

            string playerLoss = Loss("내", facts.PlayerHealthLost, facts.PlayerResistanceLost);
            string enemyLoss = Loss("상대", facts.EnemyHealthLost, facts.EnemyResistanceLost);
            if (playerLoss.Length > 0 && enemyLoss.Length > 0)
            {
                if ((facts.PlayerHealthLost > 0) == (facts.EnemyHealthLost > 0) &&
                    (facts.PlayerResistanceLost > 0) == (facts.EnemyResistanceLost > 0))
                    return Loss("서로", facts.PlayerHealthLost, facts.PlayerResistanceLost);
                return Join(playerLoss, enemyLoss);
            }
            if (playerLoss.Length > 0 || enemyLoss.Length > 0) return Join(playerLoss, enemyLoss);
            if (facts.PlayerResistanceLost < 0 && facts.EnemyResistanceLost < 0) return "서로 저항 회복";
            if (facts.PlayerResistanceLost < 0) return "내 저항 회복";
            if (facts.EnemyResistanceLost < 0) return "상대 저항 회복";
            if (input.PlayerAction == LegacyClashAction.Attack || input.EnemyAction == LegacyClashAction.Attack)
                return "피해 없음";
            return "기술 효과 적용";
        }

        private static string Loss(string side, int health, int resistance)
            => health > 0 ? side + (resistance > 0 ? " 체력·저항 손실" : " 체력 손실")
                : resistance > 0 ? side + " 저항 손실" : string.Empty;
        private static string Join(string first, string second)
            => first.Length == 0 ? second : second.Length == 0 ? first : first + " · " + second;
    }
}
