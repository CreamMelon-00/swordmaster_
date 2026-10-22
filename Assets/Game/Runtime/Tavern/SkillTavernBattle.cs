using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Tavern
{
    /// <summary>One continuing fight. Each shop round prepares one eight-slot turn of the same duel.</summary>
    public sealed class SkillTavernBattle
    {
        public const int FighterHealth = 500;
        public const int FighterResistance = 30;
        public const int MaximumTurns = 24;
        private readonly SkillTavernRun run;
        private readonly int seed;
        private readonly IReadOnlyList<LegacySkill> initialEnemyQueue;
        private readonly List<CompletedTurn> completedTurns = new List<CompletedTurn>();
        private bool recorded;

        public SkillTavernBattle(SkillTavernRun run, int seed = 0)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.seed = seed;
            PlayerQueue = Array.AsReadOnly(run.BuildQueue());
            EnemyQueue = Array.AsReadOnly(CreateEnemyQueue(run.RoundNumber));
            initialEnemyQueue = EnemyQueue;
            EnemyName = "선술집 도전자";
            Duel = CreateDuel();
        }

        public LegacyQueuedDuel Duel { get; private set; }
        public IReadOnlyList<LegacySkill> PlayerQueue { get; private set; }
        public IReadOnlyList<LegacySkill> EnemyQueue { get; private set; }
        public string EnemyName { get; }
        public bool IsTurnActive { get; private set; }

        /// <summary>One technique initially, then one more every two shop rounds; every other position breathes.</summary>
        public static LegacySkill[] CreateEnemyQueue(int shopRound)
        {
            if (shopRound < 1) throw new ArgumentOutOfRangeException(nameof(shopRound));
            var queue = new LegacySkill[SkillTavernRun.SlotCount];
            int count = (int)Math.Min(SkillTavernRun.SlotCount, 1L + (shopRound - 1L) / 2);
            int bonus = Math.Min(3, (shopRound - 1) / 4);
            for (int index = 0; index < queue.Length; index++)
            {
                if (index >= count) { queue[index] = LegacyCommonActions.Breathe; continue; }
                int kind = index % 4;
                LegacySkillProperty property = kind == 0 ? LegacySkillProperty.Slash : kind == 1
                    ? LegacySkillProperty.Penetrate : kind == 2 ? LegacySkillProperty.Hit : LegacySkillProperty.Defence;
                string name = kind == 0 ? "도전자 베기" : kind == 1 ? "도전자 찌르기" : kind == 2 ? "도전자 타격" : "도전자 막기";
                queue[index] = new LegacySkill(-8000 - index, name, 0, 4 + bonus, 6 + bonus,
                    kind == 3 ? LegacySkillKind.Defence : LegacySkillKind.Attack, property, 1, 0,
                    string.Empty, iconId: kind == 0 ? 1 : kind == 1 ? 3 : kind == 2 ? 5 : 7);
            }
            return queue;
        }

        public bool TryStartTurn()
        {
            if (IsTurnActive || Duel.IsFinished ||
                (Duel.Phase != LegacyDuelPhase.Planning && !Duel.IsTurnResolved)) return false;
            // Keep the settled turn unchanged throughout shopping. Recovery and every other normal turn boundary
            // happen once, when the next turn actually begins, rather than when its shop opens.
            if (Duel.IsTurnResolved) Duel.BeginNextTurn();
            PlayerQueue = Array.AsReadOnly(run.BuildQueue());
            EnemyQueue = Array.AsReadOnly(CreateEnemyQueue(run.RoundNumber));
            if (!Duel.TrySetFixedQueues(PlayerQueue, EnemyQueue))
                throw new InvalidOperationException("The battle formation could not be prepared.");
            Duel.Commit();
            IsTurnActive = true;
            return true;
        }

        /// <summary>Settles one played turn. Survivors immediately get their next shop without resetting fighters,
        /// statuses, cycle counts or the random stream. A terminal fight records once and grants no further income.</summary>
        public bool TryCompleteTurn()
        {
            if (!IsTurnActive || (!Duel.IsTurnResolved && !Duel.IsFinished)) return false;
            IsTurnActive = false;
            if (Duel.IsFinished) TryRecordResult();
            else
            {
                completedTurns.Add(new CompletedTurn(PlayerQueue, EnemyQueue));
                run.NextRound();
            }
            return true;
        }

        /// <summary>Abandons the current undecided turn. Replaying at most 23 completed turns with the original seed
        /// restores the last shop's settled state and exact future rolls, without changing the shared engine or its RNG.
        /// Skill definitions are held constant during this session, as they are during a normal battle.</summary>
        public bool TryCancelTurn()
        {
            if (!IsTurnActive || Duel.IsFinished || Duel.IsTurnResolved) return false;
            LegacyQueuedDuel restored = CreateDuel();
            for (int index = 0; index < completedTurns.Count; index++)
            {
                if (index > 0) restored.BeginNextTurn();
                CompletedTurn turn = completedTurns[index];
                if (!restored.TrySetFixedQueues(turn.Player, turn.Enemy))
                    throw new InvalidOperationException("A completed formation could not be restored.");
                restored.Commit();
                while (!restored.IsTurnResolved && !restored.IsFinished) restored.ResolveNextSlot();
                if (restored.IsFinished)
                    throw new InvalidOperationException("The continuing fight changed while restoring its completed turns.");
            }
            Duel = restored;
            IsTurnActive = false;
            return true;
        }

        public bool TryRecordResult()
        {
            if (recorded || Duel.Phase != LegacyDuelPhase.Finished || Duel.Outcome == DuelMatchOutcome.InProgress)
                return false;
            run.RecordBattleResult(Duel.Outcome);
            recorded = true;
            return true;
        }

        private LegacyQueuedDuel CreateDuel()
            => new LegacyQueuedDuel(FighterHealth, FighterResistance, FighterHealth, FighterResistance,
                new[] { LegacyCommonActions.Breathe }, initialEnemyQueue, new[] { SkillTavernRun.SlotCount },
                randomSeed: seed, features: CombatFeature.LaneQ, roundLimit: MaximumTurns, meshPercent: 0);

        private sealed class CompletedTurn
        {
            public CompletedTurn(IReadOnlyList<LegacySkill> player, IReadOnlyList<LegacySkill> enemy)
            {
                Player = player;
                Enemy = enemy;
            }
            public IReadOnlyList<LegacySkill> Player { get; }
            public IReadOnlyList<LegacySkill> Enemy { get; }
        }
    }
}
