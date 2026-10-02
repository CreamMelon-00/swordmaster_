using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>What the enemy queues, written turn by turn: an opening played once from turn 1, then a loop that
    /// repeats for the rest of the fight. Each turn's queue is shown before the player plans, as always.</summary>
    public sealed class EnemyScript
    {
        private readonly LegacySkill[][] opening, loop;

        public EnemyScript(IReadOnlyList<IReadOnlyList<LegacySkill>> loop, IReadOnlyList<IReadOnlyList<LegacySkill>> opening = null)
        {
            this.loop = CopyTurns(loop, nameof(loop));
            if (this.loop.Length == 0) throw new ArgumentException("An enemy script needs at least one looping turn.", nameof(loop));
            this.opening = opening == null ? Array.Empty<LegacySkill[]>() : CopyTurns(opening, nameof(opening));
            var all = new List<LegacySkill>();
            var sizes = new List<int>();
            foreach (LegacySkill[] turn in this.opening) { all.AddRange(turn); sizes.Add(turn.Length); }
            foreach (LegacySkill[] turn in this.loop) { all.AddRange(turn); sizes.Add(turn.Length); }
            AllSkills = all.AsReadOnly();
            TurnSizes = sizes.AsReadOnly();
        }

        /// <summary>Turns played once from turn 1.</summary>
        public int OpeningLength => opening.Length;
        /// <summary>Turns that repeat after the opening.</summary>
        public int LoopLength => loop.Length;
        /// <summary>Every skill the script uses, opening first.</summary>
        public IReadOnlyList<LegacySkill> AllSkills { get; }
        /// <summary>How many actions each written turn has, opening first.</summary>
        public IReadOnlyList<int> TurnSizes { get; }

        /// <summary>The skills queued on turn <paramref name="round"/> (1-based).</summary>
        public IReadOnlyList<LegacySkill> Turn(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            return round <= opening.Length ? opening[round - 1] : loop[(round - 1 - opening.Length) % loop.Length];
        }

        /// <summary>The same script with every skill replaced by <paramref name="adjust"/>(skill), e.g. a stage's power.</summary>
        public EnemyScript Select(Func<LegacySkill, LegacySkill> adjust)
        {
            if (adjust == null) throw new ArgumentNullException(nameof(adjust));
            return new EnemyScript(Map(loop, adjust), opening.Length == 0 ? null : Map(opening, adjust));
        }

        private static LegacySkill[][] CopyTurns(IReadOnlyList<IReadOnlyList<LegacySkill>> turns, string name)
        {
            if (turns == null) throw new ArgumentNullException(name);
            var copy = new LegacySkill[turns.Count][];
            for (int index = 0; index < turns.Count; index++)
            {
                IReadOnlyList<LegacySkill> turn = turns[index];
                if (turn == null || turn.Count == 0) throw new ArgumentException("Every enemy turn needs at least one action.", name);
                copy[index] = new LegacySkill[turn.Count];
                for (int action = 0; action < turn.Count; action++)
                    copy[index][action] = turn[action] ?? throw new ArgumentException("Enemy actions cannot be null.", name);
            }
            return copy;
        }

        private static IReadOnlyList<IReadOnlyList<LegacySkill>> Map(LegacySkill[][] turns, Func<LegacySkill, LegacySkill> adjust)
        {
            var mapped = new IReadOnlyList<LegacySkill>[turns.Length];
            for (int index = 0; index < turns.Length; index++)
            {
                var turn = new LegacySkill[turns[index].Length];
                for (int action = 0; action < turn.Length; action++) turn[action] = adjust(turns[index][action]);
                mapped[index] = turn;
            }
            return mapped;
        }
    }
}
