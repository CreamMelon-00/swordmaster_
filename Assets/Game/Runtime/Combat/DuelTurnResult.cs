using System;
using TurnLimbo.Core.Combat;

namespace TurnLimbo.Runtime.Combat
{
    public sealed class DuelTurnResult
    {
        public DuelTurnResult(
            int roundNumber,
            CombatMove playerMove,
            CombatMove enemyMove,
            RoundResolution resolution,
            DuelMatchOutcome outcome)
        {
            if (roundNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roundNumber));
            }

            RoundNumber = roundNumber;
            PlayerMove = playerMove ?? throw new ArgumentNullException(nameof(playerMove));
            EnemyMove = enemyMove ?? throw new ArgumentNullException(nameof(enemyMove));
            Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
            Outcome = outcome;
        }

        public int RoundNumber { get; }

        public CombatMove PlayerMove { get; }

        public CombatMove EnemyMove { get; }

        public RoundResolution Resolution { get; }

        public DuelMatchOutcome Outcome { get; }
    }
}
