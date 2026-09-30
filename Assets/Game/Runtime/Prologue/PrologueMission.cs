using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>An enemy shown on a mission briefing.</summary>
    public sealed class MissionEnemy
    {
        public MissionEnemy(string name, string silhouetteResource)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An enemy needs a name.", nameof(name));
            Name = name;
            SilhouetteResource = silhouetteResource ?? string.Empty;
        }

        public string Name { get; }
        /// <summary>A Resources path to a sprite drawn as the enemy's silhouette.</summary>
        public string SilhouetteResource { get; }
    }

    /// <summary>One mission of the opening arc: its briefing, its duel and its coached lessons.
    /// Early missions use the Q lane only and keep steps, breathing and the loadout locked.</summary>
    public sealed class PrologueMission
    {
        public const int PlayerHealth = 100;
        public const int PlayerResistance = 50;
        private readonly LegacySkill[] playerSkills, enemySkills;
        private readonly int[] enemyActionCounts;
        private readonly string[] objectives;
        private readonly MissionEnemy[] enemies;
        private readonly MissionGuideBeat[] guide;

        internal PrologueMission(int number, string title, string backgroundResource,
            IReadOnlyList<string> objectives, IReadOnlyList<MissionEnemy> enemies,
            int enemyHealth, int enemyResistance, IReadOnlyList<LegacySkill> enemySkills,
            IReadOnlyList<int> enemyActionCounts, IReadOnlyList<LegacySkill> playerSkills,
            bool planningTimer, IReadOnlyList<MissionGuideBeat> guide)
        {
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A mission needs a title.", nameof(title));
            if (enemyHealth <= 0) throw new ArgumentOutOfRangeException(nameof(enemyHealth));
            // A zero-resistance enemy would stay broken (x2) for the whole fight.
            if (enemyResistance <= 0) throw new ArgumentOutOfRangeException(nameof(enemyResistance));
            Number = number;
            Title = title;
            BackgroundResource = backgroundResource ?? string.Empty;
            this.objectives = Copy(objectives, nameof(objectives));
            this.enemies = Copy(enemies, nameof(enemies));
            EnemyHealth = enemyHealth;
            EnemyResistance = enemyResistance;
            this.enemySkills = Copy(enemySkills, nameof(enemySkills));
            this.enemyActionCounts = Copy(enemyActionCounts, nameof(enemyActionCounts));
            this.playerSkills = Copy(playerSkills, nameof(playerSkills));
            foreach (LegacySkill skill in this.playerSkills)
                if (skill.LaneIndex != 0) throw new ArgumentException("Opening missions use the Q lane only.", nameof(playerSkills));
            PlanningTimer = planningTimer;
            this.guide = guide == null || guide.Count == 0 ? null : Copy(guide, nameof(guide));
        }

        public int Number { get; }
        public string Title { get; }
        /// <summary>A Resources path to the briefing background sprite.</summary>
        public string BackgroundResource { get; }
        public IReadOnlyList<string> Objectives => objectives;
        public IReadOnlyList<MissionEnemy> Enemies => enemies;
        public int EnemyHealth { get; }
        public int EnemyResistance { get; }
        public IReadOnlyList<LegacySkill> EnemySkills => enemySkills;
        public IReadOnlyList<int> EnemyActionCounts => enemyActionCounts;
        public IReadOnlyList<LegacySkill> PlayerSkills => playerSkills;
        /// <summary>Whether the 10-second planning timer runs once the coach leaves the player free.</summary>
        public bool PlanningTimer { get; }
        /// <summary>The Q/W/E lanes the player has; the opening arc has only Q.</summary>
        public int LaneCount => 1;
        public bool StepsEnabled => false;
        public bool BreathEnabled => false;
        public string IntroDialogue => $"Dialogue/mission-{Number:00}-intro";
        public string OutroDialogue => $"Dialogue/mission-{Number:00}-outro";

        public LegacyQueuedDuel CreateDuel(int seed = 1)
            => new LegacyQueuedDuel(PlayerHealth, PlayerResistance, EnemyHealth, EnemyResistance,
                playerSkills, enemySkills, enemyActionCounts, seed);

        /// <summary>A fresh coach for one attempt, or null when the mission has no coached beats.</summary>
        public MissionGuide CreateGuide() => guide == null ? null : new MissionGuide(guide);

        private static T[] Copy<T>(IReadOnlyList<T> source, string name)
        {
            if (source == null || source.Count == 0) throw new ArgumentException("A mission needs " + name + ".", name);
            var copy = new T[source.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                T item = source[i];
                if (item == null) throw new ArgumentException(name + " cannot contain null.", name);
                copy[i] = item;
            }
            return copy;
        }
    }
}
