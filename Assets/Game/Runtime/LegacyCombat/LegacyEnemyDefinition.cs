using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>One immutable enemy's stats and turn script. Fight state belongs to LegacyQueuedDuel.</summary>
    public sealed class LegacyEnemyDefinition
    {
        public LegacyEnemyDefinition(string name, int health, int resistance, EnemyScript script, LegacyCounter counter = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An enemy needs a name.", nameof(name));
            if (health <= 0 || resistance < 0) throw new ArgumentOutOfRangeException(nameof(health));
            Name = name;
            Health = health;
            Resistance = resistance;
            Script = script ?? throw new ArgumentNullException(nameof(script));
            Counter = counter;
        }

        public string Name { get; }
        public int Health { get; }
        public int Resistance { get; }
        public EnemyScript Script { get; }
        public LegacyCounter Counter { get; }
    }
}
