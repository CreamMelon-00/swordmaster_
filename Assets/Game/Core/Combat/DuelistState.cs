using System;

namespace TurnLimbo.Core.Combat
{
    public sealed class DuelistState
    {
        public DuelistState(int maxHealth, int health)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "Maximum health must be greater than zero.");
            }

            if (health < 0 || health > maxHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(health), health, "Health must be between zero and maximum health.");
            }

            MaxHealth = maxHealth;
            Health = health;
        }

        public int MaxHealth { get; }

        public int Health { get; }

        public bool IsDefeated => Health == 0;

        public DuelistState TakeDamage(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Damage cannot be negative.");
            }

            return new DuelistState(MaxHealth, Math.Max(0, Health - amount));
        }
    }
}
