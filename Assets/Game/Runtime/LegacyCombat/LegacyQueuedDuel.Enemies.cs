using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public sealed partial class LegacyQueuedDuel
    {
        private readonly LegacyEnemyDefinition[] enemyDefinitions;
        private readonly LegacyCounter singleEnemyCounter;
        private readonly List<LegacyFighterState> enemyStates = new List<LegacyFighterState>();
        private readonly List<int> enemyQueueOwners = new List<int>();
        private IReadOnlyList<LegacyFighterState> enemyStatesView;
        private LegacyStatusCollection[] enemyStatusesByOwner;
        private int[] enemyCountersByOwner, enemyBrokenFrom, enemyDefeatedFrom;
        private int activeEnemyIndex;

        /// <summary>A campaign battle with individual enemies and one interleaved reservation queue. Each enemy's
        /// own script order is preserved; a seeded shuffle chooses their baton order anew each turn.</summary>
        public LegacyQueuedDuel(int playerHealth, int playerResistance, IReadOnlyList<LegacySkill> playerSkills,
            IReadOnlyList<LegacyEnemyDefinition> enemies, int randomSeed = 1, LegacyCounter playerCounter = null,
            CombatFeature features = CombatFeature.All, int playerActGainBonus = 0, int playerActCapacityBonus = 0,
            int roundLimit = int.MaxValue, int meshPercent = LegacyMeshing.DefaultPercent, int playerHealthFloor = 0)
            : this(playerHealth, playerResistance, FirstEnemy(enemies).Health, FirstEnemy(enemies).Resistance,
                playerSkills, FirstEnemy(enemies).Script, randomSeed, playerCounter: playerCounter,
                features: features, playerActGainBonus: playerActGainBonus, playerActCapacityBonus: playerActCapacityBonus,
                roundLimit: roundLimit, meshPercent: meshPercent, playerHealthFloor: playerHealthFloor)
        {
            enemyDefinitions = new LegacyEnemyDefinition[enemies.Count];
            for (int i = 0; i < enemies.Count; i++)
                enemyDefinitions[i] = enemies[i] ?? throw new ArgumentException("Enemies cannot be null.", nameof(enemies));
            Reset();
        }

        public IReadOnlyList<LegacyFighterState> Enemies => enemyStatesView;
        public int ActiveEnemyIndex => activeEnemyIndex;
        public IReadOnlyList<LegacyStatusSnapshot> GetEnemyStatuses(int enemyIndex)
        {
            if (enemyIndex < 0 || enemyIndex >= enemyStatusesByOwner.Length) throw new ArgumentOutOfRangeException(nameof(enemyIndex));
            return enemyStatusesByOwner[enemyIndex].Statuses;
        }
        public int TotalEnemyHealth
        {
            get
            {
                int total = 0;
                foreach (LegacyFighterState enemy in enemyStates) total = checked(total + enemy.Health);
                return total;
            }
        }
        public int TotalEnemyMaxHealth
        {
            get
            {
                int total = 0;
                foreach (LegacyFighterState enemy in enemyStates) total = checked(total + enemy.MaxHealth);
                return total;
            }
        }

        public string EnemyName(int enemyIndex)
        {
            if (enemyIndex < 0 || enemyIndex >= enemyStates.Count) throw new ArgumentOutOfRangeException(nameof(enemyIndex));
            return enemyDefinitions == null ? "상대" : enemyDefinitions[enemyIndex].Name;
        }

        public int EnemyOwnerAtSlot(int slotIndex)
            => slotIndex >= 0 && slotIndex < enemyQueueOwners.Count ? enemyQueueOwners[slotIndex] : -1;

        /// <summary>The actual opponent at a slot. An absent or canceled action leaves the last living opponent in
        /// place; if that opponent fell, the next living enemy takes over. Reading does not select or mutate a fighter.</summary>
        public int EnemyIndexForSlot(int slotIndex)
        {
            if (slotIndex < 0) return LivingEnemyFallback(activeEnemyIndex);
            int owner = EnemyOwnerAtSlot(slotIndex);
            if (owner >= 0 && !IsEnemySlotCanceled(slotIndex) && !enemyStates[owner].IsDefeated) return owner;
            int previous = LivingEnemyFallback(activeEnemyIndex);
            if (Phase != LegacyDuelPhase.Planning && slotIndex == nextSlot) return previous;
            for (int i = Math.Min(slotIndex - 1, enemyQueueOwners.Count - 1); i >= 0; i--)
            {
                int candidate = enemyQueueOwners[i];
                if (!IsEnemySlotCanceled(i) && !enemyStates[candidate].IsDefeated) return candidate;
            }
            return previous;
        }

        public bool IsEnemySlotCanceled(int slotIndex)
        {
            int owner = EnemyOwnerAtSlot(slotIndex);
            if (owner < 0) return false;
            if (enemyDefinitions == null)
                return EnemyBreakCanceledFromSlotIndex >= 0 && slotIndex >= EnemyBreakCanceledFromSlotIndex;
            return (enemyBrokenFrom[owner] >= 0 && slotIndex >= enemyBrokenFrom[owner]) ||
                (enemyDefeatedFrom[owner] >= 0 && slotIndex >= enemyDefeatedFrom[owner]);
        }

        private bool EnemyCannotCounterThisTurn => Enemy.IsDefeated || (enemyDefinitions == null
            ? EnemyBreakCanceledFromSlotIndex >= 0 : enemyBrokenFrom[activeEnemyIndex] >= 0);

        private static LegacyEnemyDefinition FirstEnemy(IReadOnlyList<LegacyEnemyDefinition> enemies)
        {
            if (enemies == null || enemies.Count == 0 || enemies[0] == null)
                throw new ArgumentException("At least one enemy is required.", nameof(enemies));
            return enemies[0];
        }

        private int LivingEnemyFallback(int preferred)
        {
            if (preferred >= 0 && preferred < enemyStates.Count && !enemyStates[preferred].IsDefeated) return preferred;
            for (int i = 0; i < enemyStates.Count; i++) if (!enemyStates[i].IsDefeated) return i;
            return Math.Max(0, Math.Min(preferred, enemyStates.Count - 1));
        }

        private void ResetEnemies()
        {
            enemyStates.Clear();
            int count = enemyDefinitions?.Length ?? 1;
            enemyStatusesByOwner = new LegacyStatusCollection[count];
            enemyCountersByOwner = new int[count];
            enemyBrokenFrom = new int[count];
            enemyDefeatedFrom = new int[count];
            for (int i = 0; i < count; i++)
            {
                LegacyEnemyDefinition definition = enemyDefinitions == null ? null : enemyDefinitions[i];
                enemyStates.Add(new LegacyFighterState(definition?.Health ?? enemyMaxHealth,
                    definition?.Resistance ?? enemyMaxResistance, definition == null ? enemyHealthFloor : 0));
                enemyStatusesByOwner[i] = new LegacyStatusCollection();
                enemyBrokenFrom[i] = enemyDefeatedFrom[i] = -1;
            }
            if (enemyStatesView == null) enemyStatesView = enemyStates.AsReadOnly();
            activeEnemyIndex = 0;
        }

        private void CopyEnemiesFrom(LegacyQueuedDuel source)
        {
            int count = source.enemyStates.Count;
            enemyStatusesByOwner = new LegacyStatusCollection[count];
            enemyCountersByOwner = (int[])source.enemyCountersByOwner.Clone();
            enemyBrokenFrom = (int[])source.enemyBrokenFrom.Clone();
            enemyDefeatedFrom = (int[])source.enemyDefeatedFrom.Clone();
            for (int i = 0; i < count; i++)
            {
                enemyStates.Add(source.enemyStates[i].Copy());
                enemyStatusesByOwner[i] = source.enemyStatusesByOwner[i].Copy();
            }
            enemyStatesView = enemyStates.AsReadOnly();
            enemyQueueOwners.AddRange(source.enemyQueueOwners);
            activeEnemyIndex = source.activeEnemyIndex;
        }

        private void BeginEnemyTurn()
        {
            for (int i = 0; i < enemyStates.Count; i++)
            {
                if (!enemyStates[i].IsDefeated) enemyStates[i].BeginTurn();
                enemyBrokenFrom[i] = enemyDefeatedFrom[i] = -1;
                enemyCountersByOwner[i] = (enemyDefinitions == null ? singleEnemyCounter : enemyDefinitions[i].Counter)?.UsesPerTurn ?? 0;
            }
            activeEnemyIndex = LivingEnemyFallback(activeEnemyIndex);
        }

        private void BuildSharedEnemyQueue()
        {
            var owners = new List<int>(enemyStates.Count);
            for (int i = 0; i < enemyStates.Count; i++) if (!enemyStates[i].IsDefeated) owners.Add(i);
            // Queue ordering has its own generator, so previews and queue lengths cannot consume power rolls.
            var shuffle = new Random(unchecked(randomSeed * 397 ^ RoundNumber * 7919));
            for (int i = owners.Count - 1; i > 0; i--)
            {
                int other = shuffle.Next(i + 1);
                int swap = owners[i]; owners[i] = owners[other]; owners[other] = swap;
            }
            int longest = 0;
            foreach (int owner in owners) longest = Math.Max(longest, enemyDefinitions[owner].Script.Turn(RoundNumber).Count);
            for (int action = 0; action < longest; action++)
                foreach (int owner in owners)
                {
                    IReadOnlyList<LegacySkill> skills = enemyDefinitions[owner].Script.Turn(RoundNumber);
                    if (action >= skills.Count) continue;
                    enemyQueue.Add(skills[action]);
                    enemyQueueOwners.Add(owner);
                }
        }

        private void CancelEnemyAfterBreak(int slotIndex)
        {
            if (enemyBrokenFrom[activeEnemyIndex] >= 0) return;
            enemyBrokenFrom[activeEnemyIndex] = slotIndex + 1;
            CancelFutureEnemyReservations(activeEnemyIndex, slotIndex + 1);
        }

        private void CancelEnemyAfterDeath(int slotIndex)
        {
            if (enemyDefinitions == null || enemyDefeatedFrom[activeEnemyIndex] >= 0) return;
            enemyDefeatedFrom[activeEnemyIndex] = slotIndex + 1;
            enemyCountersByOwner[activeEnemyIndex] = 0;
            CancelFutureEnemyReservations(activeEnemyIndex, slotIndex + 1);
        }

        private void CancelFutureEnemyReservations(int owner, int firstSlot)
        {
            if (committedEnemyQueue == null) return;
            for (int i = firstSlot; i < committedEnemyQueue.Length; i++)
                if (enemyQueueOwners[i] == owner) committedEnemyQueue[i] = null;
        }

        private LegacySkill GroupEnemyCounterFacing(LegacySkill skill)
        {
            if (Phase != LegacyDuelPhase.Planning || !IsAttack(skill) || playerQueue.Count < enemyQueue.Count) return null;
            int owner = EnemyIndexForSlot(playerQueue.Count);
            LegacyCounter counter = enemyDefinitions[owner].Counter;
            if (counter == null) return null;
            int answered = 0;
            for (int i = enemyQueue.Count; i < playerQueue.Count; i++) if (IsAttack(playerQueue[i])) answered++;
            return answered < enemyCountersByOwner[owner] ? counter.Skill : null;
        }

        private void ForecastGroupCounterSlots(bool playerCounter, List<int> slots)
        {
            int start = Phase == LegacyDuelPhase.Planning ? 0 : nextSlot;
            int playerUses = PlayerCountersRemaining;
            int[] enemyUses = (int[])enemyCountersByOwner.Clone();
            if (CurrentSlot != null)
            {
                start++;
                bool pending = playerCounter && CurrentSlot.PendingPlayerCounter != null;
                if (pending || (playerCounter ? CurrentSlot.PlayerCountered : CurrentSlot.EnemyCountered))
                    slots.Add(CurrentSlot.SlotIndex);
                if (pending) playerUses--;
            }
            IReadOnlyList<LegacySkill> defenders = QueueForForecast(playerCounter);
            IReadOnlyList<LegacySkill> attackers = QueueForForecast(!playerCounter);
            for (int i = start; i < attackers.Count; i++)
            {
                if (!IsAttack(attackers[i]) || (i < defenders.Count && defenders[i] != null)) continue;
                if (playerCounter)
                {
                    if (PlayerBreakCanceledFromSlotIndex >= 0 || playerUses <= 0) continue;
                    slots.Add(i);
                    playerUses--;
                }
                else
                {
                    int owner = EnemyIndexForSlot(i);
                    if (enemyStates[owner].IsDefeated || enemyBrokenFrom[owner] >= 0 || enemyUses[owner] <= 0) continue;
                    slots.Add(i);
                    enemyUses[owner]--;
                }
            }
        }
    }
}
