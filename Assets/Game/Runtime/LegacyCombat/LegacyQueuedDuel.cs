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
        private readonly EnemyScript enemyScript;
        private readonly int[] enemyActionCounts;
        private readonly List<LegacySkill>[] lanes = { new List<LegacySkill>(), new List<LegacySkill>(), new List<LegacySkill>() };
        private readonly IReadOnlyList<LegacySkill>[] laneViews;
        private readonly List<LegacySkill> playerQueue = new List<LegacySkill>();
        private readonly List<LegacySkill> enemyQueue = new List<LegacySkill>();
        private readonly List<SkillBuff> playerBuffs = new List<SkillBuff>();
        private readonly List<SkillBuff> enemyBuffs = new List<SkillBuff>();
        private readonly int playerMaxHealth;
        private readonly int playerMaxResistance;
        private readonly int enemyMaxHealth;
        private readonly int enemyMaxResistance;
        private readonly int randomSeed;
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

        public LegacyQueuedDuel(int playerHealth, int playerResistance, int enemyHealth, int enemyResistance,
            IReadOnlyList<LegacySkill> playerSkills, IReadOnlyList<LegacySkill> enemySkills,
            IReadOnlyList<int> enemyTurnActionCounts, int randomSeed = 1,
            LegacyCounter playerCounter = null, LegacyCounter enemyCounter = null,
            CombatFeature features = CombatFeature.All)
            : this(playerHealth, playerResistance, enemyHealth, enemyResistance, playerSkills, enemySkills,
                enemyTurnActionCounts, null, randomSeed, playerCounter, enemyCounter, features) { }

        /// <summary>A duel whose enemy follows <paramref name="enemyScript"/> turn by turn.</summary>
        public LegacyQueuedDuel(int playerHealth, int playerResistance, int enemyHealth, int enemyResistance,
            IReadOnlyList<LegacySkill> playerSkills, EnemyScript enemyScript, int randomSeed = 1,
            LegacyCounter playerCounter = null, LegacyCounter enemyCounter = null,
            CombatFeature features = CombatFeature.All)
            : this(playerHealth, playerResistance, enemyHealth, enemyResistance, playerSkills,
                (enemyScript ?? throw new ArgumentNullException(nameof(enemyScript))).AllSkills, enemyScript.TurnSizes,
                enemyScript, randomSeed, playerCounter, enemyCounter, features) { }

        private LegacyQueuedDuel(int playerHealth, int playerResistance, int enemyHealth, int enemyResistance,
            IReadOnlyList<LegacySkill> playerSkills, IReadOnlyList<LegacySkill> enemySkills,
            IReadOnlyList<int> enemyTurnActionCounts, EnemyScript enemyScript, int randomSeed,
            LegacyCounter playerCounter, LegacyCounter enemyCounter, CombatFeature features)
        {
            this.enemyScript = enemyScript;
            PlayerCounter = playerCounter;
            EnemyCounter = enemyCounter;
            if (!features.HasLane(0) && !features.HasLane(1) && !features.HasLane(2))
                throw new ArgumentException("A duel needs at least one open lane.", nameof(features));
            Features = features;
            if (playerHealth <= 0 || enemyHealth <= 0 || playerResistance < 0 || enemyResistance < 0)
                throw new ArgumentOutOfRangeException(nameof(playerHealth));
            initialPlayerSkills = CopySkills(playerSkills, nameof(playerSkills));
            enemyPattern = CopySkills(enemySkills, nameof(enemySkills));
            if (enemyTurnActionCounts == null || enemyTurnActionCounts.Count == 0)
                throw new ArgumentException("At least one enemy turn action count is required.", nameof(enemyTurnActionCounts));
            enemyActionCounts = new int[enemyTurnActionCounts.Count];
            for (int i = 0; i < enemyActionCounts.Length; i++)
            {
                if (enemyTurnActionCounts[i] < 1) throw new ArgumentOutOfRangeException(nameof(enemyTurnActionCounts));
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
        public int Act { get; private set; }
        public int NextActGain { get; private set; }
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
        /// <summary>Whether any step missed this turn. A miss, not an attempt, costs the next turn's natural ACT
        /// recovery: success is free, so skilled play can step without limit but guessing is not free.</summary>
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
        }

        public bool TryStep(LegacyStepAction action, bool timingSuccessful, out bool success)
        {
            success = false;
            if (Phase != LegacyDuelPhase.Resolving ||
                (action != LegacyStepAction.Dodge && action != LegacyStepAction.Pressure)) return false;
            // A closed step is not an attempt: it neither narrows the window nor costs ACT.
            if (!Features.AllowsStep(action)) return false;

            // Every attempt, including gaps between skills and repeated inputs, narrows the
            // next window. Any miss (mistimed, no target, repeated, or in a gap) breaks the
            // streak and costs the next turn's natural ACT recovery; successes cost nothing.
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
            double playerAttackMultiplier = AttackMultiplier(playerBuffs, out int playerPowerBuffPercent);
            double enemyAttackMultiplier = AttackMultiplier(enemyBuffs, out int enemyPowerBuffPercent);
            double playerReceivedMultiplier = ReceivedMultiplier(playerBuffs, out int playerProtectionBuffPercent);
            double enemyReceivedMultiplier = ReceivedMultiplier(enemyBuffs, out int enemyProtectionBuffPercent);
            int playerPower = RollPower(playerSkill, playerAttackMultiplier, out int playerTotalPower);
            int enemyPower = RollPower(enemySkill, enemyAttackMultiplier, out _);

            CurrentSlot = new LegacyCurrentSlot(nextSlot, playerSkill, enemySkill,
                playerPower, enemyPower, playerTotalPower,
                playerReceivedMultiplier, enemyReceivedMultiplier, Player, Enemy);
            CurrentSlot.EnemyCountered = enemyCounters;
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
            // Both actions remain valid throughout the slot, including after a
            // lethal hit. Original Controller checks death after its full wait.
            if (playerAttacked)
            {
                enemyImpact = ResolveSingleHit(slot.PlayerSkill, slot.EnemySkill,
                    slot.PlayerPower, slot.EnemyPower, Enemy, slot.EnemyReceivedMultiplier);
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
                    slot.EnemyPower, slot.PlayerPower, Player, slot.PlayerReceivedMultiplier, halveDamage);
            }
            slot.HitsResolved++;
            return new LegacyHitResult(slot, hitIndex, playerAttacked, enemyAttacked,
                playerHealth - Player.Health, enemyHealth - Enemy.Health,
                playerResistance - Player.Resistance, enemyResistance - Enemy.Resistance,
                playerImpact, enemyImpact,
                slot.IsResolved ? DetermineOutcome() : DuelMatchOutcome.InProgress,
                enemyAttacked && slot.DodgeSucceeded, slot.PressureSucceeded);
        }

        public LegacySlotResult CompleteCurrentSlot()
        {
            if (Phase != LegacyDuelPhase.Resolving || !IsCurrentSlotResolved)
                throw new InvalidOperationException("Every hit must resolve before completing the current slot.");
            LegacyCurrentSlot slot = CurrentSlot;
            LastResolvedSlot = slot.SlotIndex;
            nextSlot++;
            Outcome = DetermineOutcome();
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

        public void Reset()
        {
            random = new Random(randomSeed);
            Player = new LegacyFighterState(playerMaxHealth, playerMaxResistance);
            Enemy = new LegacyFighterState(enemyMaxHealth, enemyMaxResistance);
            for (int i = 0; i < lanes.Length; i++) lanes[i].Clear();
            foreach (LegacySkill skill in initialPlayerSkills)
                if (Features.HasLane(skill.LaneIndex)) lanes[skill.LaneIndex].Add(skill);
            enemyPatternIndex = 0;
            RoundNumber = 1;
            Act = 0;
            NextActGain = BaseActGain;
            // A miss in the abandoned match must not cost the new match its opening ACT.
            StepMissedThisTurn = false;
            StepAttemptsThisTurn = StepSuccessStreak = 0;
            Outcome = DuelMatchOutcome.InProgress;
            StartPlanningTurn();
        }

        private void StartPlanningTurn()
        {
            Act = Math.Min(MaximumAct, Act + NextActGain - (StepMissedThisTurn ? BaseActGain : 0));
            NextActGain = BaseActGain;
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
            if (enemyScript != null) enemyQueue.AddRange(enemyScript.Turn(RoundNumber));
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
            int defenderPower, LegacyFighterState target, double receivedMultiplier, bool halveDamage = false)
        {
            bool resistanceHit = defender != null && defender.Kind == LegacySkillKind.Attack;
            int power = attackerPower;
            if (defender != null && defender.Kind == LegacySkillKind.Defence)
                power = Math.Max(0, attackerPower - defenderPower / attacker.AttackCount);
            return target.ReceiveHit(power, resistanceHit, receivedMultiplier, halveDamage);
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
            bool effectActivated = false;
            int actGainGranted = 0, resistanceRestored = 0, opponentResistanceReduced = 0;
            SkillBuff grantedBuff = null;
            if (effect.ResistanceRecoveryPercent > 0)
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
                actGainGranted, resistanceRestored, opponentResistanceReduced);
        }

        private static double AttackMultiplier(List<SkillBuff> buffs, out int powerBuffPercent)
        {
            int percent = 100;
            foreach (SkillBuff buff in buffs) percent += buff.AttackPercent;
            powerBuffPercent = percent - 100;
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

        private static LegacySkill[] FirstSixSkills()
        {
            var result = new LegacySkill[6];
            for (int i = 0; i < result.Length; i++) result[i] = LegacyInitialSkills.All[i];
            return result;
        }

        private static LegacySkill[] CopySkills(IReadOnlyList<LegacySkill> source, string argument)
        {
            if (source == null || source.Count == 0) throw new ArgumentException("At least one skill is required.", argument);
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
