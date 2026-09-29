using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A fighter's counter: the skill that answers a one-sided attack on an empty slot,
    /// usable a limited number of times per turn. It costs no ACT, rotates no lane and is never queued.</summary>
    public sealed class LegacyCounter
    {
        public LegacyCounter(LegacySkill skill, int usesPerTurn = 1)
        {
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            if (skill.IsWait) throw new ArgumentException("Breathing cannot be a counter.", nameof(skill));
            if (usesPerTurn < 1) throw new ArgumentOutOfRangeException(nameof(usesPerTurn));
            UsesPerTurn = usesPerTurn;
        }

        public LegacySkill Skill { get; }
        public int UsesPerTurn { get; }
    }
}
