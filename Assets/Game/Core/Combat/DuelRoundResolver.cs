using System;

namespace TurnLimbo.Core.Combat
{
    public sealed class DuelRoundResolver
    {
        public RoundResolution Resolve(
            DuelistState left,
            CombatMove leftMove,
            DuelistState right,
            CombatMove rightMove)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (leftMove == null)
            {
                throw new ArgumentNullException(nameof(leftMove));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (rightMove == null)
            {
                throw new ArgumentNullException(nameof(rightMove));
            }

            int damageTakenByLeft = CalculateDamage(rightMove, leftMove);
            int damageTakenByRight = CalculateDamage(leftMove, rightMove);

            return new RoundResolution(
                left.TakeDamage(damageTakenByLeft),
                right.TakeDamage(damageTakenByRight),
                damageTakenByLeft,
                damageTakenByRight);
        }

        private static int CalculateDamage(CombatMove attackerMove, CombatMove defenderMove)
        {
            if (attackerMove.Type != CombatMoveType.Attack)
            {
                return 0;
            }

            int guard = defenderMove.Type == CombatMoveType.Guard ? defenderMove.Power : 0;
            return Math.Max(0, attackerMove.Power - guard);
        }
    }
}
