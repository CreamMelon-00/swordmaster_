using System;
using System.Collections.Generic;
using TurnLimbo.Core.Combat;

namespace TurnLimbo.Runtime.Combat
{
    public sealed class DuelMatchSession
    {
        private readonly int maxHealth;
        private readonly CombatMove[] enemyPattern;
        private readonly DuelRoundResolver resolver = new DuelRoundResolver();
        private int enemyPatternIndex;

        public DuelMatchSession(int maxHealth, IReadOnlyList<CombatMove> enemyPattern)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "Maximum health must be greater than zero.");
            }

            if (enemyPattern == null)
            {
                throw new ArgumentNullException(nameof(enemyPattern));
            }

            if (enemyPattern.Count == 0)
            {
                throw new ArgumentException("At least one enemy move is required.", nameof(enemyPattern));
            }

            this.enemyPattern = new CombatMove[enemyPattern.Count];
            for (int i = 0; i < enemyPattern.Count; i++)
            {
                this.enemyPattern[i] = enemyPattern[i] ??
                    throw new ArgumentException("Enemy moves cannot contain null values.", nameof(enemyPattern));
            }

            this.maxHealth = maxHealth;
            Reset();
        }

        public DuelistState Player { get; private set; }

        public DuelistState Enemy { get; private set; }

        public int RoundNumber { get; private set; }

        public DuelMatchOutcome Outcome { get; private set; }

        public bool IsFinished => Outcome != DuelMatchOutcome.InProgress;

        public CombatMove EnemyIntent => enemyPattern[enemyPatternIndex];

        public DuelTurnResult ResolveRound(CombatMove playerMove)
        {
            if (playerMove == null)
            {
                throw new ArgumentNullException(nameof(playerMove));
            }

            if (IsFinished)
            {
                throw new InvalidOperationException("A finished match must be reset before resolving another round.");
            }

            int resolvedRound = RoundNumber;
            CombatMove enemyMove = EnemyIntent;
            RoundResolution resolution = resolver.Resolve(Player, playerMove, Enemy, enemyMove);

            Player = resolution.LeftAfter;
            Enemy = resolution.RightAfter;
            Outcome = DetermineOutcome(resolution);
            RoundNumber++;

            if (!IsFinished)
            {
                enemyPatternIndex = (enemyPatternIndex + 1) % enemyPattern.Length;
            }

            return new DuelTurnResult(resolvedRound, playerMove, enemyMove, resolution, Outcome);
        }

        public void Reset()
        {
            Player = new DuelistState(maxHealth, maxHealth);
            Enemy = new DuelistState(maxHealth, maxHealth);
            RoundNumber = 1;
            Outcome = DuelMatchOutcome.InProgress;
            enemyPatternIndex = 0;
        }

        private static DuelMatchOutcome DetermineOutcome(RoundResolution resolution)
        {
            if (resolution.IsDraw)
            {
                return DuelMatchOutcome.Draw;
            }

            if (resolution.RightAfter.IsDefeated)
            {
                return DuelMatchOutcome.PlayerVictory;
            }

            return resolution.LeftAfter.IsDefeated
                ? DuelMatchOutcome.EnemyVictory
                : DuelMatchOutcome.InProgress;
        }
    }
}
