using System;

namespace TurnLimbo.Core.Combat
{
    public sealed class RoundResolution
    {
        public RoundResolution(
            DuelistState leftAfter,
            DuelistState rightAfter,
            int damageTakenByLeft,
            int damageTakenByRight)
        {
            LeftAfter = leftAfter ?? throw new ArgumentNullException(nameof(leftAfter));
            RightAfter = rightAfter ?? throw new ArgumentNullException(nameof(rightAfter));
            DamageTakenByLeft = damageTakenByLeft;
            DamageTakenByRight = damageTakenByRight;
        }

        public DuelistState LeftAfter { get; }

        public DuelistState RightAfter { get; }

        public int DamageTakenByLeft { get; }

        public int DamageTakenByRight { get; }

        public bool IsDraw => LeftAfter.IsDefeated && RightAfter.IsDefeated;

        public bool IsFinished => LeftAfter.IsDefeated || RightAfter.IsDefeated;
    }
}
