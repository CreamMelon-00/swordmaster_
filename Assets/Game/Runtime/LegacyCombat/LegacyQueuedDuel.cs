using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public sealed class LegacyQueuedDuel
    {
        public const int MaximumBreathsPerTurn = 3;
        private const int BaseActGain = 3;
        private const int MaximumAct = 10;
        private readonly LegacySkill[] initialPlayerSkills;
        private readonly LegacySkill[] enemyPattern;
        // A turn-by-turn enemy (opening, then loop); null for the original skill cycle with per-turn action counts.
        private readonly EnemyScript initialEnemyScript;
        private readonly int[] enemyActionCounts;
        // The script the enemy follows now (ReplaceEnemyScript swaps it at a planning turn), counted from its first round.
        private EnemyScript enemyScript, pendingEnemyScript;
        private int enemyScriptFirstRound;
        private readonly List<LegacySkill>[] lanes = { new List<LegacySkill>(), new List<LegacySkill>(), new List<LegacySkill>() };
        private readonly IReadOnlyList<LegacySkill>[] laneViews;
        private readonly List<LegacySkill> playerQueue = new List<LegacySkill>();
        private readonly List<LegacySkill> enemyQueue = new List<LegacySkill>();
        private readonly List<SkillBuff> playerBuffs = new List<SkillBuff>();
        private readonly List<SkillBuff> enemyBuffs = new List<SkillBuff>();
        private readonly int playerMaxHealth;
        private readonly int playerMaxResistance;
        private readonly int playerBaseActGain;
        private readonly int playerMaximumAct;
        private readonly int enemyMaxHealth;
        private readonly int enemyMaxResistance;
        private readonly int enemyHealthFloor;
        private readonly int enemyHealthThresholdPercent;
        private readonly int roundLimit;
        private readonly int randomSeed;
        // 맞물림's power percent per chained skill (LegacyMeshing); 0 switches it off.
        private readonly int meshPercent;
        private Random random;
        private LegacySkill[] committedPlayerQueue;
        private LegacySkill[] committedEnemyQueue;
        private int enemyPatternIndex;
        private int nextSlot;
        private int slotCount;
        // A pending player counter waits for the first hit, so the slot's buff tick and
        // initial effects wait with it; the multipliers are snapshotted at slot start.
        private bool slotStartDeferred;
        private double deferredPlayerAttackMultiplier;
        private int deferredPlayerPowerBuffPercent, deferredPlayerProtectionBuffPercent;
        private int deferredEnemyPowerBuffPercent, deferredEnemyProtectionBuffPercent;

        public LegacyQueuedDuel() : this(100, 50, 80, 15, LegacyInitialSkills.All,
            FirstSixSkills(), new[] { 2, 3, 2, 1 }, Environment.TickCount) { }

        /// <param name="enemyHealthFloor">Damage never takes the enemy's health below this; above zero the enemy cannot
        /// be defeated (서막 mission 4). Must stay under <paramref name="enemyHealth"/>.</param>
        /// <param name="enemyHealthThresholdPercent">0 for none, otherwise the percent of the enemy's maximum health whose
        /// first crossing is reported (<see cref="LegacyHitResult.EnemyReachedHealthThreshold"/>); below 100.</param>
        /// <param name="meshPercent">맞물림: the power percent each skill of a chain gains per skill in the chain
        /// (<see cref="MeshPercent"/>); 0 switches 맞물림 off.</param>
        public LegacyQueuedDuel(int playerHealth, int playerResistance, int enemyHealth, int enemyResistance,
            IReadOnlyList<LegacySkill> playerSkills, IReadOnlyList<LegacySkill> enemySkills,
            IReadOnlyList<int> enemyTurnActionCounts, int randomSeed = 1,
            LegacyCounter playerCounter = null, LegacyCounter enemyCounter = null,
            CombatFeature features = CombatFeature.All, int playerActGainBonus = 0, int playerActCapacityBonus = 0,
            int roundLimit = int.MaxValue, int enemyHealthFloor = 0, int enemyHealthThresholdPercent = 0,
            int meshPercent = LegacyMeshing.DefaultPercent)
            : this(playerHealth, playerResistance, enemyHealth, enemyResistance, playerSkills, enemySkills,
                enemyTurnActionCounts, null, randomSeed, playerCounter, enemyCounter, features,
                playerActGainBonus, playerActCapacityBonus, roundLimit, enemyHealthFloor, enemyHealthThresholdPercent,
                meshPercent) { }

        /// <summary>A duel whose enemy follows <paramref name="enemyScript"/> turn by turn.</summary>
        public LegacyQueuedDuel(int playerHealth, int playerResistance, int enemyHealth, int enemyResistance,
            IReadOnlyList<LegacySkill> playerSkills, EnemyScript enemyScript, int randomSeed = 1,
            LegacyCounter playerCounter = null, LegacyCounter enemyCounter = null,
            CombatFeature features = CombatFeature.All, int playerActGainBonus = 0, int playerActCapacityBonus = 0,
            int roundLimit = int.MaxValue, int enemyHealthFloor = 0, int enemyHealthThresholdPercent = 0,
            int meshPercent = LegacyMeshing.DefaultPercent)
            : this(playerHealth, playerResistance, enemyHealth, enemyResistance, playerSkills,
                (enemyScript ?? throw new ArgumentNullException(nameof(enemyScript))).AllSkills, enemyScript.TurnSizes,
                enemyScript, randomSeed, playerCounter, enemyCounter, features,
                playerActGainBonus, playerActCapacityBonus, roundLimit, enemyHealthFloor, enemyHealthThresholdPercent,
                meshPercent) { }

        private LegacyQueuedDuel(int playerHealth, int playerResistance, int enemyHealth, int enemyResistance,
            IReadOnlyList<LegacySkill> playerSkills, IReadOnlyList<LegacySkill> enemySkills,
            IReadOnlyList<int> enemyTurnActionCounts, EnemyScript enemyScript, int randomSeed,
            LegacyCounter playerCounter, LegacyCounter enemyCounter, CombatFeature features,
            int playerActGainBonus, int playerActCapacityBonus, int roundLimit,
            int enemyHealthFloor, int enemyHealthThresholdPercent, int meshPercent)
        {
            initialEnemyScript = enemyScript;
            PlayerCounter = playerCounter;
            EnemyCounter = enemyCounter;
            if (!features.HasLane(0) && !features.HasLane(1) && !features.HasLane(2))
                throw new ArgumentException("A duel needs at least one open lane.", nameof(features));
            Features = features;
            if (playerHealth <= 0 || enemyHealth <= 0 || playerResistance < 0 || enemyResistance < 0)
                throw new ArgumentOutOfRangeException(nameof(playerHealth));
            if (playerActGainBonus < 0) throw new ArgumentOutOfRangeException(nameof(playerActGainBonus));
            if (playerActCapacityBonus < 0) throw new ArgumentOutOfRangeException(nameof(playerActCapacityBonus));
            if (roundLimit < 1) throw new ArgumentOutOfRangeException(nameof(roundLimit));
            if (enemyHealthFloor < 0 || enemyHealthFloor >= enemyHealth) throw new ArgumentOutOfRangeException(nameof(enemyHealthFloor));
            if (enemyHealthThresholdPercent < 0 || enemyHealthThresholdPercent >= 100)
                throw new ArgumentOutOfRangeException(nameof(enemyHealthThresholdPercent));
            if (meshPercent < 0) throw new ArgumentOutOfRangeException(nameof(meshPercent));
            this.meshPercent = meshPercent;
            this.enemyHealthFloor = enemyHealthFloor;
            this.enemyHealthThresholdPercent = enemyHealthThresholdPercent;
            this.roundLimit = roundLimit;
            playerBaseActGain = checked(BaseActGain + playerActGainBonus);
            playerMaximumAct = checked(MaximumAct + playerActCapacityBonus);
            initialPlayerSkills = CopySkills(playerSkills, nameof(playerSkills));
            enemyPattern = CopySkills(enemySkills, nameof(enemySkills), allowEmpty: true);
            if (enemyTurnActionCounts == null || enemyTurnActionCounts.Count == 0)
                throw new ArgumentException("At least one enemy turn action count is required.", nameof(enemyTurnActionCounts));
            enemyActionCounts = new int[enemyTurnActionCounts.Count];
            for (int i = 0; i < enemyActionCounts.Length; i++)
            {
                // A passive target has no skills and queues no actions. Other enemies still need at least one.
                if (enemyPattern.Length == 0 ? enemyTurnActionCounts[i] != 0 : enemyTurnActionCounts[i] < 1)
                    throw new ArgumentOutOfRangeException(nameof(enemyTurnActionCounts));
                enemyActionCounts[i] = enemyTurnActionCounts[i];
            }
            playerMaxHealth = playerHealth;
            playerMaxResistance = playerResistance;
            enemyMaxHealth = enemyHealth;
            enemyMaxResistance = enemyResistance;
            this.randomSeed = randomSeed;
            laneViews = new IReadOnlyList<LegacySkill>[3];
            for (int i = 0; i < lanes.Length; i++) laneViews[i] = lanes[i].AsReadOnly();
            PlayerQueue = playerQueue.AsReadOnly();
            EnemyQueue = enemyQueue.AsReadOnly();
            Reset();
        }

        public LegacyFighterState Player { get; private set; }
        public LegacyFighterState Enemy { get; private set; }
        public int RoundNumber { get; private set; }
        public int RoundLimit => roundLimit;
        public int Act { get; private set; }
        public int NextActGain { get; private set; }
        public int PlayerBaseActGain => playerBaseActGain;
        public int PlayerMaximumAct => playerMaximumAct;
        public LegacyDuelPhase Phase { get; private set; }
        public DuelMatchOutcome Outcome { get; private set; }
        public IReadOnlyList<LegacySkill> PlayerQueue { get; }
        public IReadOnlyList<LegacySkill> EnemyQueue { get; }
        public LegacyCurrentSlot CurrentSlot { get; private set; }
        /// <summary>Whether any step was attempted this turn.</summary>
        public bool UsedStepThisTurn => StepAttemptsThisTurn > 0;
        /// <summary>Dodge and pressure attempts this turn, hit or miss; each one narrows the next
        /// success window (<see cref="LegacyStepTiming"/>). Restarts every turn.</summary>
        public int StepAttemptsThisTurn { get; private set; }
        /// <summary>Consecutive successful steps this turn; any miss or a new turn restarts it.</summary>
        public int StepSuccessStreak { get; private set; }
        /// <summary>Whether any step missed this turn, for the miss feedback. Any accepted step attempt, successful
        /// or not, limits the next turn's natural ACT recovery to 1.</summary>
        public bool StepMissedThisTurn { get; private set; }
        public int BreathsQueuedThisTurn { get; private set; }
        /// <summary>넘기기 presses that turned the lanes this planning turn.</summary>
        public int LaneCyclesThisTurn { get; private set; }
        public int BreathsRemainingThisTurn => MaximumBreathsPerTurn - BreathsQueuedThisTurn;
        public int LastResolvedSlot { get; private set; }
        public int ResolutionSlotCount => slotCount;
        public bool IsFinished => Phase == LegacyDuelPhase.Finished;
        public bool IsTurnResolved => Phase == LegacyDuelPhase.Resolving && CurrentSlot == null && nextSlot >= slotCount;
        public bool IsCurrentSlotResolved => CurrentSlot != null && CurrentSlot.IsResolved;
        public LegacyCounter PlayerCounter { get; }
        public LegacyCounter EnemyCounter { get; }
        /// <summary>What this duel allows: skills of a closed lane never reach it, and closed actions are refused.</summary>
        public CombatFeature Features { get; }
        public int PlayerCountersRemaining { get; private set; }
        public int EnemyCountersRemaining { get; private set; }
        /// <summary>The enemy's health never drops below this; above zero the enemy cannot be defeated.</summary>
        public int EnemyHealthFloor => enemyHealthFloor;
        /// <summary>0 for none, otherwise the percent of the enemy's maximum health whose first crossing a hit reports.</summary>
        public int EnemyHealthThresholdPercent => enemyHealthThresholdPercent;
        /// <summary>Whether a hit of this attempt has brought the enemy to <see cref="EnemyHealthThresholdPercent"/> or below
        /// (<see cref="LegacyHitResult.EnemyReachedHealthThreshold"/>). <see cref="Reset"/> clears it.</summary>
        public bool EnemyHealthThresholdReached { get; private set; }
        /// <summary>맞물림: the power percent each skill of a chain gains per skill in the chain, so every skill of an
        /// N-skill chain gains +N × this (<see cref="LegacyMeshing"/>). 0 switches 맞물림 off: nothing meshes and steps
        /// are never held back.</summary>
        public int MeshPercent => meshPercent;
        /// <summary>The current slot is meshed: its power carries the chain's bonus and it takes no steps.</summary>
        public bool IsCurrentSlotMeshed => CurrentSlot != null && CurrentSlot.PlayerMesh.IsMeshed;

        /// <summary>맞물림 for one slot of the player's queue: the live queue while planning (it changes as skills are
        /// queued), the committed queue while resolving. Only the player's queue meshes; a slot past the queue is empty.</summary>
        public LegacyMeshSlot PlayerMesh(int slotIndex) => LegacyMeshing.Find(QueueForForecast(true), slotIndex, meshPercent);

        /// <summary>The chain the front skill of <paramref name="laneIndex"/> would join if it were queued now, as the
        /// next slot (넘기기 changes the fronts, so this changes with it). Unmeshed outside planning or when that lane
        /// cannot queue its front (closed, empty, or short of ACT).</summary>
        public LegacyMeshSlot PlayerMeshIfQueued(int laneIndex)
        {
            int slotIndex = playerQueue.Count;
            if (Phase != LegacyDuelPhase.Planning || laneIndex < 0 || laneIndex >= lanes.Length || !Features.HasLane(laneIndex))
                return LegacyMeshSlot.Unmeshed(slotIndex);
            List<LegacySkill> lane = lanes[laneIndex];
            if (lane.Count == 0 || lane[0].IsWait || Act < lane[0].Cost) return LegacyMeshSlot.Unmeshed(slotIndex);
            return LegacyMeshing.FindIfAppended(PlayerQueue, LegacyMeshing.School(lane[0]), meshPercent);
        }

        /// <summary>From the next planning turn the enemy follows <paramref name="script"/> from its first turn, as when
        /// 이아 receives 수훈 in the 서막's last mission. The turn in progress keeps the queue it showed, even during
        /// planning. A later call before that turn replaces this one; <see cref="Reset"/> brings back the original enemy.</summary>
        public void ReplaceEnemyScript(EnemyScript script)
            => pendingEnemyScript = script ?? throw new ArgumentNullException(nameof(script));

        /// <summary>This turn's slots whose one-sided player attack the enemy's counter answers, in order:
        /// the current slot while its counter plays, then the upcoming ones. The enemy never steps, so this is exact.</summary>
        public IReadOnlyList<int> ForecastEnemyCounterSlots()
        {
            var slots = new List<int>();
            ForecastEnemyCounterSlots(slots);
            return slots;
        }

        /// <summary>Allocation-free form for per-frame presentation; clears <paramref name="slots"/> first.</summary>
        public void ForecastEnemyCounterSlots(List<int> slots) => ForecastCounterSlots(false, slots);

        /// <summary>This turn's slots whose one-sided enemy attack the player's counter answers, in order:
        /// the current slot while its counter is pending or plays, then the upcoming ones.
        /// A dodge attempt keeps that use for the next such slot.</summary>
        public IReadOnlyList<int> ForecastPlayerCounterSlots()
        {
            var slots = new List<int>();
            ForecastPlayerCounterSlots(slots);
            return slots;
        }

        /// <summary>Allocation-free form for per-frame presentation; clears <paramref name="slots"/> first.</summary>
        public void ForecastPlayerCounterSlots(List<int> slots) => ForecastCounterSlots(true, slots);

        /// <summary>The enemy counter that would meet <paramref name="skill"/> if the player queued it next, or null.</summary>
        public LegacySkill EnemyCounterFacing(LegacySkill skill)
        {
            if (Phase != LegacyDuelPhase.Planning || EnemyCounter == null || !IsAttack(skill) ||
                playerQueue.Count < enemyQueue.Count) return null;
            int answered = 0;
            for (int i = enemyQueue.Count; i < playerQueue.Count; i++)
                if (IsAttack(playerQueue[i])) answered++;
            return answered < EnemyCountersRemaining ? EnemyCounter.Skill : null;
        }

        private IReadOnlyList<LegacySkill> QueueForForecast(bool player) => Phase == LegacyDuelPhase.Planning
            ? (player ? PlayerQueue : EnemyQueue)
            : (IReadOnlyList<LegacySkill>)(player ? committedPlayerQueue : committedEnemyQueue) ?? Array.Empty<LegacySkill>();

        private void ForecastCounterSlots(bool playerCounter, List<int> slots)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            slots.Clear();
            if (Phase == LegacyDuelPhase.Finished) return;
            int uses = playerCounter ? PlayerCountersRemaining : EnemyCountersRemaining;
            int start = 0;
            if (Phase != LegacyDuelPhase.Planning)
            {
                start = nextSlot;
                LegacyCurrentSlot current = CurrentSlot;
                if (current != null)
                {
                    start++;
                    bool pending = playerCounter && current.PendingPlayerCounter != null;
                    if (pending || (playerCounter ? current.PlayerCountered : current.EnemyCountered))
                        slots.Add(current.SlotIndex);
                    // A pending counter still holds its use until it settles or a dodge releases it.
                    if (pending) uses--;
                }
            }
            IReadOnlyList<LegacySkill> defenders = QueueForForecast(playerCounter);
            IReadOnlyList<LegacySkill> attackers = QueueForForecast(!playerCounter);
            for (int i = start, found = 0; i < attackers.Count && found < uses; i++)
                if (i >= defenders.Count && IsAttack(attackers[i]))
                {
                    slots.Add(i);
                    found++;
                }
        }

        private static bool IsAttack(LegacySkill skill) => skill != null && skill.Kind == LegacySkillKind.Attack;

        public IReadOnlyList<LegacySkill> GetLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= lanes.Length) throw new ArgumentOutOfRangeException(nameof(laneIndex));
            return laneViews[laneIndex];
        }

        public bool TryQueueLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= lanes.Length || !Features.HasLane(laneIndex)) return false;
            List<LegacySkill> lane = lanes[laneIndex];
            if (Phase != LegacyDuelPhase.Planning || lane.Count == 0 || lane[0].IsWait || Act < lane[0].Cost) return false;
            LegacySkill skill = lane[0];
            Act -= skill.Cost;
            playerQueue.Add(skill);
            lane.RemoveAt(0);
            lane.Add(skill);
            return true;
        }

        /// <summary>넘기기: during planning, every open lane with more than one skill sends its front skill to the back
        /// unused. All lanes turn together, so lining up skills across lanes takes using some of them (which turns
        /// only their own lane). Free and unlimited; the lanes keep the new order into later turns.</summary>
        public bool TryCycleLanes()
        {
            if (Phase != LegacyDuelPhase.Planning || !Features.Has(CombatFeature.Cycle)) return false;
            bool turned = false;
            for (int index = 0; index < lanes.Length; index++)
            {
                List<LegacySkill> lane = lanes[index];
                if (!Features.HasLane(index) || lane.Count < 2) continue;
                LegacySkill front = lane[0];
                lane.RemoveAt(0);
                lane.Add(front);
                turned = true;
            }
            if (turned) LaneCyclesThisTurn++;
            return turned;
        }

        public bool TryQueueBreath()
        {
            if (Phase != LegacyDuelPhase.Planning || !Features.Has(CombatFeature.Breath) ||
                BreathsQueuedThisTurn >= MaximumBreathsPerTurn) return false;
            playerQueue.Add(LegacyCommonActions.Breathe);
            BreathsQueuedThisTurn++;
            return true;
        }

        public void Commit()
        {
            if (Phase != LegacyDuelPhase.Planning) throw new InvalidOperationException("Only a planning turn can be committed.");
            committedPlayerQueue = playerQueue.ToArray();
            committedEnemyQueue = enemyQueue.ToArray();
            nextSlot = 0;
            slotCount = Math.Max(committedPlayerQueue.Length, committedEnemyQueue.Length);
            Phase = LegacyDuelPhase.Resolving;
            if (slotCount == 0 && RoundNumber >= roundLimit)
            {
                Outcome = DuelMatchOutcome.Draw;
                Phase = LegacyDuelPhase.Finished;
            }
        }

        public bool TryStep(LegacyStepAction action, bool timingSuccessful, out bool success)
        {
            success = false;
            if (Phase != LegacyDuelPhase.Resolving ||
                (action != LegacyStepAction.Dodge && action != LegacyStepAction.Pressure)) return false;
            // A closed step is not an attempt: it neither narrows the window nor costs ACT.
            if (!Features.AllowsStep(action)) return false;
            // Nor is a step in a meshed slot (맞물림): its gears take the place of steps, so the input is ignored.
            if (IsCurrentSlotMeshed) return false;

            // Every attempt, including gaps between skills and repeated inputs, narrows the
            // next window and limits the next turn's natural ACT recovery to 1. A miss
            // (mistimed, no target, repeated, or in a gap) also breaks the streak.
            StepAttemptsThisTurn++;
            success = ApplyStep(action, timingSuccessful);
            StepSuccessStreak = success ? StepSuccessStreak + 1 : 0;
            if (!success) StepMissedThisTurn = true;
            return true;
        }

        private bool ApplyStep(LegacyStepAction action, bool timingSuccessful)
        {
            LegacyCurrentSlot slot = CurrentSlot;
            if (action == LegacyStepAction.Dodge && slot != null && slot.HitsResolved == 0)
            {
                // Choosing to evade forgoes the counter, whatever the timing. Its use is kept.
                slot.DodgeAttempted = true;
                if (slot.PendingPlayerCounter != null)
                {
                    slot.PendingPlayerCounter = null;
                    slot.PressureSucceeded = false;
                    slot.HitCount = Math.Max(1, slot.EnemySkill?.AttackCount ?? 0);
                }
            }
            if (!timingSuccessful || slot == null || slot.HitsResolved != 0) return false;
            if (action == LegacyStepAction.Dodge)
            {
                if (slot.EnemySkill == null || slot.EnemySkill.Kind != LegacySkillKind.Attack || slot.DodgeSucceeded)
                    return false;
                slot.DodgeSucceeded = true;
                return true;
            }
            // Pressure may back a pending counter; it applies once the counter strikes.
            LegacySkill skill = slot.PlayerSkill ?? slot.PendingPlayerCounter;
            if (skill == null || skill.IsWait || slot.PressureSucceeded) return false;
            slot.PressureSucceeded = true;
            return true;
        }

        public LegacySlotResult ResolveNextSlot()
        {
            if (CurrentSlot == null) BeginNextSlot();
            while (!IsCurrentSlotResolved) ResolveNextHit();
            return CompleteCurrentSlot();
        }

        public LegacyCurrentSlot BeginNextSlot()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot != null || nextSlot >= slotCount)
                throw new InvalidOperationException("A committed slot must be available and the previous slot must be complete.");
            LegacySkill playerSkill = nextSlot < committedPlayerQueue.Length ? committedPlayerQueue[nextSlot] : null;
            LegacySkill enemySkill = nextSlot < committedEnemyQueue.Length ? committedEnemyQueue[nextSlot] : null;
            // A one-sided attack on a truly empty slot draws that side's counter (breathing is a skill).
            // A counter never answers a counter: each side's trigger requires its own empty slot.
            bool enemyCounters = enemySkill == null && IsAttack(playerSkill) && EnemyCountersRemaining > 0;
            if (enemyCounters)
            {
                enemySkill = EnemyCounter.Skill;
                EnemyCountersRemaining--;
            }
            LegacySkill pendingCounter = playerSkill == null && IsAttack(enemySkill) && PlayerCountersRemaining > 0
                ? PlayerCounter.Skill : null;
            // 맞물림 adds into the player's power percent beside the buffs. A counter fills an empty slot, which never meshes.
            LegacyMeshSlot playerMesh = LegacyMeshing.Find(committedPlayerQueue, nextSlot, meshPercent);
            double playerAttackMultiplier = AttackMultiplier(playerBuffs, playerMesh.BonusPercent, out int playerPowerBuffPercent);
            double enemyAttackMultiplier = AttackMultiplier(enemyBuffs, 0, out int enemyPowerBuffPercent);
            double playerReceivedMultiplier = ReceivedMultiplier(playerBuffs, out int playerProtectionBuffPercent);
            double enemyReceivedMultiplier = ReceivedMultiplier(enemyBuffs, out int enemyProtectionBuffPercent);
            int playerPower = RollPower(playerSkill, playerAttackMultiplier, out int playerTotalPower);
            int enemyPower = RollPower(enemySkill, enemyAttackMultiplier, out _);

            CurrentSlot = new LegacyCurrentSlot(nextSlot, playerSkill, enemySkill,
                playerPower, enemyPower, playerTotalPower,
                playerReceivedMultiplier, enemyReceivedMultiplier, Player, Enemy);
            CurrentSlot.EnemyCountered = enemyCounters;
            CurrentSlot.PlayerMesh = playerMesh;
            if (pendingCounter != null)
            {
                CurrentSlot.PendingPlayerCounter = pendingCounter;
                CurrentSlot.HitCount = Math.Max(CurrentSlot.HitCount, pendingCounter.AttackCount);
                slotStartDeferred = true;
                deferredPlayerAttackMultiplier = playerAttackMultiplier;
                deferredPlayerPowerBuffPercent = playerPowerBuffPercent;
                deferredPlayerProtectionBuffPercent = playerProtectionBuffPercent;
                deferredEnemyPowerBuffPercent = enemyPowerBuffPercent;
                deferredEnemyProtectionBuffPercent = enemyProtectionBuffPercent;
                return CurrentSlot;
            }
            ApplySlotStart(CurrentSlot, playerPowerBuffPercent, playerProtectionBuffPercent,
                enemyPowerBuffPercent, enemyProtectionBuffPercent);
            return CurrentSlot;
        }

        private void ApplySlotStart(LegacyCurrentSlot slot, int playerPowerBuffPercent, int playerProtectionBuffPercent,
            int enemyPowerBuffPercent, int enemyProtectionBuffPercent)
        {
            // Original UseBuff/AttackStart/End/BuffClear initializes power once
            // and applies effects before animation events deal individual hits.
            TickBuffs(playerBuffs);
            TickBuffs(enemyBuffs);
            slot.PlayerFeedback = ApplyInitialSkillEffects(slot.PlayerSkill, slot.EnemySkill, playerBuffs, true,
                playerPowerBuffPercent, playerProtectionBuffPercent);
            slot.EnemyFeedback = ApplyInitialSkillEffects(slot.EnemySkill, slot.PlayerSkill, enemyBuffs, false,
                enemyPowerBuffPercent, enemyProtectionBuffPercent);
            // With both sides' initial effects in, a 조건 피해 배율 whose state condition already holds keeps holding for the
            // slot (nothing restores health or resistance before the slot ends). Report it as armed only when the slot's
            // hits will also reach health, since the multiplier has nothing else to multiply.
            slot.PlayerFeedback = slot.PlayerFeedback.WithConditionalDamage(
                ArmedConditionalDamage(slot.PlayerSkill, slot.EnemySkill, slot.PlayerPower, slot.EnemyPower, Enemy));
            slot.EnemyFeedback = slot.EnemyFeedback.WithConditionalDamage(
                ArmedConditionalDamage(slot.EnemySkill, slot.PlayerSkill, slot.EnemyPower, slot.PlayerPower, Player));
        }

        /// <summary>The 조건 피해 배율 a slot's hits carry from its start: the condition holds against <paramref name="target"/>
        /// and the hits reach health, as <see cref="ResolveSingleHit"/> deals them. That means the target is already broken
        /// or its skill is not an attack (a clash against whole resistance takes resistance only; a breaking hit's overflow
        /// is still multiplied, silently), and a guard does not absorb the hit. A dodge comes later and is not foreseen.</summary>
        private static int ArmedConditionalDamage(LegacySkill skill, LegacySkill opposingSkill, int power, int opposingPower,
            LegacyFighterState target)
        {
            if (skill == null || skill.IsWait || skill.Kind != LegacySkillKind.Attack) return 0;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            if (effect == null || effect.ConditionalDamagePercent <= 0 || !effect.OpponentStateHolds(target)) return 0;
            if (opposingSkill != null && opposingSkill.Kind == LegacySkillKind.Attack && !target.IsResistanceBroken) return 0;
            if (opposingSkill != null && opposingSkill.Kind == LegacySkillKind.Defence &&
                power <= opposingPower / skill.AttackCount) return 0;
            return effect.ConditionalDamagePercent;
        }

        // The last moment a dodge can be attempted has passed: settle the pending counter.
        private void CompleteDeferredSlotStart(LegacyCurrentSlot slot)
        {
            slotStartDeferred = false;
            LegacySkill counter = slot.PendingPlayerCounter;
            if (counter != null)
            {
                slot.PendingPlayerCounter = null;
                slot.PlayerSkill = counter;
                slot.PlayerCountered = true;
                PlayerCountersRemaining--;
                slot.PlayerPower = RollPower(counter, deferredPlayerAttackMultiplier, out int totalPower);
                slot.PlayerTotalPower = totalPower;
            }
            ApplySlotStart(slot, deferredPlayerPowerBuffPercent, deferredPlayerProtectionBuffPercent,
                deferredEnemyPowerBuffPercent, deferredEnemyProtectionBuffPercent);
        }

        public LegacyHitResult ResolveNextHit()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot == null || CurrentSlot.IsResolved)
                throw new InvalidOperationException("There is no unresolved hit in the current slot.");
            LegacyCurrentSlot slot = CurrentSlot;
            if (slotStartDeferred) CompleteDeferredSlotStart(slot);
            int hitIndex = slot.HitsResolved;
            int playerHealth = Player.Health, enemyHealth = Enemy.Health;
            int playerResistance = Player.Resistance, enemyResistance = Enemy.Resistance;
            bool playerAttacked = HasAttackHit(slot.PlayerSkill, hitIndex);
            bool enemyAttacked = HasAttackHit(slot.EnemySkill, hitIndex);
            LegacyHitImpact playerImpact = default;
            LegacyHitImpact enemyImpact = default;
            bool playerConditionMet = false, enemyConditionMet = false;
            // Both actions at this hit index resolve before checking the outcome.
            // Later hits in the slot stop once either fighter falls.
            if (playerAttacked)
            {
                enemyImpact = ResolveSingleHit(slot.PlayerSkill, slot.EnemySkill,
                    slot.PlayerPower, slot.EnemyPower, Enemy, slot.EnemyReceivedMultiplier, false, out playerConditionMet);
                if (slot.PressureSucceeded)
                {
                    // Add one whole rolled skill power, divided across its hits
                    // without guard/resistance/received multipliers.
                    int bonus = PressureBonus(slot, hitIndex);
                    LegacyHitImpact pressureImpact = Enemy.ReceiveFlatHealthDamage(bonus);
                    enemyImpact = new LegacyHitImpact(enemyImpact.PushPower + pressureImpact.PushPower,
                        enemyImpact.DisplayedDamage + pressureImpact.DisplayedDamage);
                }
            }
            if (enemyAttacked && !slot.DodgeSucceeded)
            {
                bool halveDamage = slot.PressureSucceeded && slot.PlayerSkill != null &&
                    slot.PlayerSkill.Kind == LegacySkillKind.Defence;
                playerImpact = ResolveSingleHit(slot.EnemySkill, slot.PlayerSkill,
                    slot.EnemyPower, slot.PlayerPower, Player, slot.PlayerReceivedMultiplier, halveDamage, out enemyConditionMet);
            }
            slot.HitsResolved++;
            DuelMatchOutcome hitOutcome = DetermineOutcome();
            if (hitOutcome != DuelMatchOutcome.InProgress)
                slot.HitCount = slot.HitsResolved;
            // Only while the duel goes on: a hit that also ends it leaves nothing to interrupt.
            bool thresholdReached = enemyHealthThresholdPercent > 0 && !EnemyHealthThresholdReached &&
                hitOutcome == DuelMatchOutcome.InProgress &&
                (long)Enemy.Health * 100 <= (long)enemyHealthThresholdPercent * Enemy.MaxHealth;
            if (thresholdReached) EnemyHealthThresholdReached = true;
            return new LegacyHitResult(slot, hitIndex, playerAttacked, enemyAttacked,
                playerHealth - Player.Health, enemyHealth - Enemy.Health,
                playerResistance - Player.Resistance, enemyResistance - Enemy.Resistance,
                playerImpact, enemyImpact,
                hitOutcome,
                enemyAttacked && slot.DodgeSucceeded, slot.PressureSucceeded, thresholdReached,
                playerConditionMet, enemyConditionMet);
        }

        public LegacySlotResult CompleteCurrentSlot()
        {
            if (Phase != LegacyDuelPhase.Resolving || !IsCurrentSlotResolved)
                throw new InvalidOperationException("Every hit must resolve before completing the current slot.");
            LegacyCurrentSlot slot = CurrentSlot;
            LastResolvedSlot = slot.SlotIndex;
            nextSlot++;
            Outcome = DetermineOutcome();
            if (Outcome == DuelMatchOutcome.InProgress && nextSlot >= slotCount && RoundNumber >= roundLimit)
                Outcome = DuelMatchOutcome.Draw;
            if (Outcome != DuelMatchOutcome.InProgress) Phase = LegacyDuelPhase.Finished;
            CurrentSlot = null;
            return new LegacySlotResult(slot.SlotIndex, slot.PlayerSkill, slot.EnemySkill,
                slot.PlayerStartingHealth - Player.Health, slot.EnemyStartingHealth - Enemy.Health,
                slot.PlayerStartingResistance - Player.Resistance, slot.EnemyStartingResistance - Enemy.Resistance, Outcome);
        }

        public void BeginNextTurn()
        {
            if (!IsTurnResolved) throw new InvalidOperationException("All slots must resolve before starting another turn.");
            RoundNumber++;
            StartPlanningTurn();
        }

        /// <summary>Ends a resolving turn at the last hit that actually landed. Used when a mission event
        /// interrupts combat: the current slot is settled from its applied hits, but later hits and slots
        /// are discarded. The caller may then use <see cref="BeginNextTurn"/> after the event. Returns the
        /// settled slot, or null when the previous slot was already settled.</summary>
        public LegacySlotResult InterruptResolvingTurn()
        {
            if (Phase != LegacyDuelPhase.Resolving || DetermineOutcome() != DuelMatchOutcome.InProgress ||
                RoundNumber >= roundLimit || (CurrentSlot == null && nextSlot == 0) ||
                (CurrentSlot != null && CurrentSlot.HitsResolved == 0))
                throw new InvalidOperationException("A live turn with at least one resolved hit is required to interrupt it.");

            LegacySlotResult settled = null;
            if (CurrentSlot != null)
            {
                // Preserve the impact that triggered the event. Completing the slot must not resolve any
                // remaining strikes or reapply its start effects.
                CurrentSlot.HitCount = CurrentSlot.HitsResolved;
                settled = CompleteCurrentSlot();
            }
            nextSlot = slotCount;
            return settled;
        }

        public void Reset()
        {
            random = new Random(randomSeed);
            Player = new LegacyFighterState(playerMaxHealth, playerMaxResistance);
            Enemy = new LegacyFighterState(enemyMaxHealth, enemyMaxResistance, enemyHealthFloor);
            for (int i = 0; i < lanes.Length; i++) lanes[i].Clear();
            foreach (LegacySkill skill in initialPlayerSkills)
                if (Features.HasLane(skill.LaneIndex)) lanes[skill.LaneIndex].Add(skill);
            enemyPatternIndex = 0;
            enemyScript = initialEnemyScript;
            pendingEnemyScript = null;
            enemyScriptFirstRound = 1;
            EnemyHealthThresholdReached = false;
            RoundNumber = 1;
            Act = 0;
            NextActGain = playerBaseActGain;
            // Attempts in the abandoned match must not cost the new match its opening ACT.
            StepMissedThisTurn = false;
            StepAttemptsThisTurn = StepSuccessStreak = 0;
            Outcome = DuelMatchOutcome.InProgress;
            StartPlanningTurn();
        }

        private void StartPlanningTurn()
        {
            // NextActGain includes natural recovery and skill-granted bonuses. Steps reduce
            // only the natural portion, once per turn regardless of how many were attempted.
            int naturalActGain = UsedStepThisTurn ? 1 : playerBaseActGain;
            Act = Math.Min(playerMaximumAct, Act + NextActGain - playerBaseActGain + naturalActGain);
            NextActGain = playerBaseActGain;
            StepAttemptsThisTurn = StepSuccessStreak = 0;
            StepMissedThisTurn = false;
            BreathsQueuedThisTurn = 0;
            LaneCyclesThisTurn = 0;
            PlayerCountersRemaining = PlayerCounter?.UsesPerTurn ?? 0;
            EnemyCountersRemaining = EnemyCounter?.UsesPerTurn ?? 0;
            slotStartDeferred = false;
            Player.BeginTurn();
            Enemy.BeginTurn();
            playerBuffs.Clear();
            enemyBuffs.Clear();
            playerQueue.Clear();
            enemyQueue.Clear();
            if (pendingEnemyScript != null)
            {
                enemyScript = pendingEnemyScript;
                pendingEnemyScript = null;
                enemyScriptFirstRound = RoundNumber;
            }
            if (enemyScript != null) enemyQueue.AddRange(enemyScript.Turn(RoundNumber - enemyScriptFirstRound + 1));
            else
            {
                int count = enemyActionCounts[(RoundNumber - 1) % enemyActionCounts.Length];
                for (int i = 0; i < count; i++)
                {
                    enemyQueue.Add(enemyPattern[enemyPatternIndex]);
                    enemyPatternIndex = (enemyPatternIndex + 1) % enemyPattern.Length;
                }
            }
            committedPlayerQueue = committedEnemyQueue = null;
            CurrentSlot = null;
            slotCount = nextSlot = 0;
            LastResolvedSlot = -1;
            Phase = LegacyDuelPhase.Planning;
        }

        private int RollPower(LegacySkill skill, double multiplier, out int totalPower)
        {
            totalPower = 0;
            if (skill == null || skill.IsWait) return 0;
            int rolledPower = random.Next(skill.MinPower, skill.MaxPower + 1);
            totalPower = (int)Math.Floor(rolledPower * multiplier);
            return Math.Max(1, (int)Math.Floor(rolledPower * multiplier / skill.AttackCount));
        }

        private static int PressureBonus(LegacyCurrentSlot slot, int hitIndex)
        {
            int hits = slot.PlayerSkill.AttackCount;
            return slot.PlayerTotalPower / hits + (hitIndex < slot.PlayerTotalPower % hits ? 1 : 0);
        }

        private static bool HasAttackHit(LegacySkill skill, int hitIndex)
            => skill != null && skill.Kind == LegacySkillKind.Attack && hitIndex < skill.AttackCount;

        private DuelMatchOutcome DetermineOutcome()
        {
            // Original Controller.PhaseEnd prioritizes the player's death.
            return Player.IsDefeated ? DuelMatchOutcome.EnemyVictory
                : Enemy.IsDefeated ? DuelMatchOutcome.PlayerVictory : DuelMatchOutcome.InProgress;
        }

        private static LegacyHitImpact ResolveSingleHit(LegacySkill attacker, LegacySkill defender, int attackerPower,
            int defenderPower, LegacyFighterState target, double receivedMultiplier, bool halveDamage,
            out bool conditionMet)
        {
            bool resistanceHit = defender != null && defender.Kind == LegacySkillKind.Attack;
            int power = attackerPower;
            if (defender != null && defender.Kind == LegacySkillKind.Defence)
                power = Math.Max(0, attackerPower - defenderPower / attacker.AttackCount);
            LegacySkillEffect effect = LegacySkillDefinitions.Find(attacker)?.Effect;
            // The target must already be broken before this hit. A hit that first breaks resistance
            // keeps its ordinary overflow, while later hits in the same skill receive the bonus.
            int brokenBonus = target.IsResistanceBroken ? effect?.BrokenTargetDamagePercent ?? 0 : 0;
            // 조건 피해 배율: the target's state as this hit lands (its health and resistance before the hit), so a
            // multi-hit attack may meet it partway through; a breaking hit's overflow takes it if the condition held.
            conditionMet = effect != null && effect.ConditionalDamagePercent > 0 && effect.OpponentStateHolds(target);
            return target.ReceiveHit(power, resistanceHit, receivedMultiplier, halveDamage, brokenBonus,
                conditionMet ? effect.ConditionalDamagePercent : 0);
        }

        private LegacySkillFeedback ApplyInitialSkillEffects(LegacySkill skill, LegacySkill opposingSkill,
            List<SkillBuff> buffs, bool player, int powerBuffPercent, int protectionBuffPercent)
        {
            if (skill == null || skill.IsWait)
                return new LegacySkillFeedback(skill, false, false, powerBuffPercent, protectionBuffPercent);
            // The effect is selected by the skill's id (LegacySkillDefinitions) and applied in a fixed order.
            // Skill_Smashing only sets a guard's isAttack flag to false in
            // the source. It does not remove that guard's mitigation power.
            // The table never pairs this self condition with an opponent condition, so one flag reports either.
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect ?? LegacySkillEffect.None;
            bool opponentMatched = LegacySkillConditions.MatchesOpponent(skill, opposingSkill);
            bool conditionMet = opponentMatched;
            bool effectActivated = false, opponentBroken = false;
            int actGainGranted = 0, resistanceRestored = 0, opponentResistanceReduced = 0;
            SkillBuff grantedBuff = null;
            // 상대 상태 조건 gates the recovery: the opponent's state as the recovery applies, first of this side's
            // effects (the player's initial effects come before the enemy's).
            if (effect.ResistanceRecoveryPercent > 0 &&
                (!effect.HasOpponentStateCondition || effect.OpponentStateHolds(player ? Enemy : Player)))
            {
                // A self condition: it reports whether anything was actually restored.
                LegacyFighterState fighter = player ? Player : Enemy;
                resistanceRestored = fighter.RestoreResistance(
                    LegacySkillConditions.ResistanceRecoveryAmount(fighter, effect.ResistanceRecoveryPercent));
                conditionMet = effectActivated = resistanceRestored > 0;
            }
            if (!effect.HasOpponentCondition || opponentMatched)
            {
                if (effect.OpponentResistanceReduction > 0)
                {
                    opponentResistanceReduced = (player ? Enemy : Player).ReduceResistance(effect.OpponentResistanceReduction);
                    if (opponentResistanceReduced > 0) effectActivated = true;
                }
                if (effect.BreaksOpponent)
                {
                    // Before any hit, so every hit of this slot (including broken-target bonuses) meets a broken target.
                    // An opponent already broken loses nothing more.
                    int taken = (player ? Enemy : Player).BreakResistance();
                    if (taken > 0)
                    {
                        opponentResistanceReduced += taken;
                        opponentBroken = effectActivated = true;
                    }
                }
                if (effect.ActGain > 0 && player)
                {
                    actGainGranted = effect.ActGain;
                    NextActGain += actGainGranted;
                    effectActivated = true;
                }
            }
            if (effect.HasBuff)
            {
                grantedBuff = new SkillBuff(effect.BuffPowerPercent, effect.BuffProtectionPercent, effect.BuffSlots);
                buffs.Add(grantedBuff);
                effectActivated = true;
            }
            return new LegacySkillFeedback(skill, conditionMet, effectActivated, powerBuffPercent, protectionBuffPercent,
                grantedBuff?.AttackPercent ?? 0, grantedBuff?.DefencePercent ?? 0, grantedBuff?.SlotsRemaining ?? 0,
                actGainGranted, resistanceRestored, opponentResistanceReduced, opponentBroken);
        }

        /// <param name="meshBonusPercent">맞물림's bonus, added into the same percent as the buffs (100 + buffs + mesh);
        /// <paramref name="powerBuffPercent"/> reports the buffs alone.</param>
        private static double AttackMultiplier(List<SkillBuff> buffs, int meshBonusPercent, out int powerBuffPercent)
        {
            int percent = 100;
            foreach (SkillBuff buff in buffs) percent += buff.AttackPercent;
            powerBuffPercent = percent - 100;
            percent += meshBonusPercent;
            return percent / 100d;
        }

        private static double ReceivedMultiplier(List<SkillBuff> buffs, out int protectionBuffPercent)
        {
            int percent = 100;
            foreach (SkillBuff buff in buffs) percent -= buff.DefencePercent;
            percent = Math.Max(0, percent);
            protectionBuffPercent = Math.Max(0, 100 - percent);
            return percent / 100d;
        }

        private static void TickBuffs(List<SkillBuff> buffs)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
                if (--buffs[i].SlotsRemaining == 0) buffs.RemoveAt(i);
        }

        // The first six starting attacks by ID. Their names and effects come from the skill sheet.
        private static LegacySkill[] FirstSixSkills()
        {
            var result = new LegacySkill[6];
            for (int i = 0; i < result.Length; i++) result[i] = LegacySkillDefinitions.Skill(i + 1);
            return result;
        }

        private static LegacySkill[] CopySkills(IReadOnlyList<LegacySkill> source, string argument, bool allowEmpty = false)
        {
            if (source == null || !allowEmpty && source.Count == 0)
                throw new ArgumentException("At least one skill is required.", argument);
            var copy = new LegacySkill[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i] ?? throw new ArgumentException("Skills cannot be null.", argument);
            return copy;
        }

        private sealed class SkillBuff
        {
            public SkillBuff(int attackPercent, int defencePercent, int slotsRemaining)
            {
                AttackPercent = attackPercent;
                DefencePercent = defencePercent;
                SlotsRemaining = slotsRemaining;
            }
            public int AttackPercent { get; }
            public int DefencePercent { get; }
            public int SlotsRemaining;
        }
    }
}
