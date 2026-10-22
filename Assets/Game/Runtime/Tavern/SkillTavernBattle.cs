using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Tavern
{
    /// <summary>A snapshot of one shop round's formation, fought by the existing duel engine.</summary>
    public sealed class SkillTavernBattle
    {
        public const int FighterHealth = 60;
        public const int FighterResistance = 30;
        public const int MaximumTurns = 12;
        private readonly SkillTavernRun run;
        private bool recorded;

        public SkillTavernBattle(SkillTavernRun run, int seed = 0)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            PlayerQueue = Array.AsReadOnly(run.BuildQueue());
            EnemyQueue = Array.AsReadOnly(CreateEnemyQueue(run.RoundNumber));
            EnemyName = "선술집 도전자";
            Duel = new LegacyQueuedDuel(FighterHealth, FighterResistance, FighterHealth, FighterResistance,
                new[] { LegacyCommonActions.Breathe }, EnemyQueue, new[] { SkillTavernRun.SlotCount },
                randomSeed: seed, features: CombatFeature.LaneQ, roundLimit: MaximumTurns, meshPercent: 0);
        }

        public LegacyQueuedDuel Duel { get; }
        public IReadOnlyList<LegacySkill> PlayerQueue { get; }
        public IReadOnlyList<LegacySkill> EnemyQueue { get; }
        public string EnemyName { get; }

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

        public bool TryBeginNextTurn()
        {
            if (!Duel.IsTurnResolved || Duel.Phase == LegacyDuelPhase.Finished) return false;
            Duel.BeginNextTurn();
            if (!Duel.TrySetFixedQueues(PlayerQueue, EnemyQueue))
                throw new InvalidOperationException("The battle formation could not be restored for its next turn.");
            Duel.Commit();
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
    }
}
