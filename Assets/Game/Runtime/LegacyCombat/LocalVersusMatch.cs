using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public enum LocalVersusOutcome { InProgress, LeftVictory, RightVictory, Draw }

    /// <summary>One of two player-controlled fighters sharing the same rules.</summary>
    public sealed class LocalVersusFighter
    {
        private readonly LegacySkill[] initialSkills;
        private readonly int maxHealth, maxResistance;
        private readonly List<LegacySkill>[] lanes = { new List<LegacySkill>(), new List<LegacySkill>(), new List<LegacySkill>() };
        private readonly IReadOnlyList<LegacySkill>[] laneViews = new IReadOnlyList<LegacySkill>[3];
        private readonly int[] laneReservationsThisTurn = new int[3];
        internal readonly List<LegacySkill> QueueSkills = new List<LegacySkill>();
        private LegacySkill[] committedQueue;
        internal readonly List<int> QueueCosts = new List<int>();
        internal readonly Dictionary<int, int> Cycles = new Dictionary<int, int>();
        internal readonly HashSet<int> ActRecoveryStealUses = new HashSet<int>();
        internal LegacyStatusCollection StatusEffects = new LegacyStatusCollection();
        public IReadOnlyList<LegacyStatusSnapshot> Statuses => StatusEffects.Statuses;
        /// <summary>Whether this technique has successfully stolen next-turn recovery in the current turn.</summary>
        public bool HasUsedActRecoverySteal(int skillId) => ActRecoveryStealUses.Contains(skillId);
        internal int BoostSource = -1, BoostRemoved, BoostCap;

        internal LocalVersusFighter(IReadOnlyList<LegacySkill> skills, int health, int resistance)
        {
            if (skills == null || skills.Count == 0) throw new ArgumentException("Each player needs skills.", nameof(skills));
            if (health <= 0 || resistance < 0) throw new ArgumentOutOfRangeException(nameof(health));
            maxHealth = health;
            maxResistance = resistance;
            initialSkills = new LegacySkill[skills.Count];
            for (int i = 0; i < skills.Count; i++)
                initialSkills[i] = skills[i] ?? throw new ArgumentException("Skills cannot be null.", nameof(skills));
            for (int i = 0; i < 3; i++) laneViews[i] = lanes[i].AsReadOnly();
            Queue = QueueSkills.AsReadOnly();
        }

        // A forecast's copy (LocalVersusMatch.ForecastTurn): the same skills and numbers on lists and a state of its own.
        private LocalVersusFighter(LocalVersusFighter source)
        {
            initialSkills = source.initialSkills;
            maxHealth = source.maxHealth;
            maxResistance = source.maxResistance;
            for (int i = 0; i < 3; i++)
            {
                lanes[i].AddRange(source.lanes[i]);
                laneViews[i] = lanes[i].AsReadOnly();
                laneReservationsThisTurn[i] = source.laneReservationsThisTurn[i];
            }
            QueueSkills.AddRange(source.QueueSkills);
            QueueCosts.AddRange(source.QueueCosts);
            foreach (KeyValuePair<int, int> uses in source.Cycles) Cycles.Add(uses.Key, uses.Value);
            foreach (int id in source.ActRecoveryStealUses) ActRecoveryStealUses.Add(id);
            StatusEffects = source.StatusEffects.Copy();
            BoostSource = source.BoostSource;
            BoostRemoved = source.BoostRemoved;
            BoostCap = source.BoostCap;
            Queue = QueueSkills.AsReadOnly();
            State = source.State.Copy();
            Act = source.Act;
            NextActGain = source.NextActGain;
            BreathsQueuedThisTurn = source.BreathsQueuedThisTurn;
            ReserveEffectsUsedThisTurn = source.ReserveEffectsUsedThisTurn;
            NextPlanningSeconds = source.NextPlanningSeconds;
            PlanningSecondsBonus = source.PlanningSecondsBonus;
            AutoBreathSlotIndex = source.AutoBreathSlotIndex;
            LastQueuedSlotIndex = source.LastQueuedSlotIndex;
            LaneCyclesThisTurn = source.LaneCyclesThisTurn;
        }

        internal LocalVersusFighter Copy() => new LocalVersusFighter(this);

        public LegacyFighterState State { get; private set; }
        public int Act { get; internal set; }
        public int NextActGain { get; internal set; }
        public int BaseActGain => 3;
        public int MaximumAct => 10;
        /// <summary>숨고르기 reserved by hand this turn; an automatic one (예약 효과 숨고르기 +1) does not count.</summary>
        public int BreathsQueuedThisTurn { get; internal set; }
        public int BreathsRemainingThisTurn => LocalVersusMatch.MaximumBreathsPerTurn - BreathsQueuedThisTurn;
        /// <summary>The once-per-turn 예약 효과 already used this turn (<see cref="LegacyQueuedDuel.ReserveEffectsUsedThisTurn"/>).</summary>
        public LegacyReserveEffect ReserveEffectsUsedThisTurn { get; internal set; }
        /// <summary>Planning seconds this turn's skills have earned for this fighter's next turn so far
        /// (<see cref="LegacyQueuedDuel.NextPlanningSeconds"/>).</summary>
        public int NextPlanningSeconds { get; internal set; }
        /// <summary>Planning seconds the last turn's skills earned for this turn: this player's clock starts that much later,
        /// within <see cref="LegacyPlanningClock.VersusCeilingSeconds"/> (<see cref="LegacyPlanningClock.VersusTurnStart"/>).</summary>
        public int PlanningSecondsBonus { get; internal set; }
        /// <summary>The slot holding this turn's automatic 숨고르기, or -1 (<see cref="LegacyQueuedDuel.AutoBreathSlotIndex"/>).</summary>
        public int AutoBreathSlotIndex { get; internal set; } = -1;
        /// <summary>The slot of the skill last reserved from a lane this turn, or -1 (<see cref="LegacyQueuedDuel.LastQueuedSlotIndex"/>).</summary>
        public int LastQueuedSlotIndex { get; internal set; } = -1;
        public int LaneCyclesThisTurn { get; internal set; }
        public IReadOnlyList<LegacySkill> Queue { get; }
        /// <summary>First reservation canceled by a new resistance break this turn, or -1. Original queue entries stay
        /// readable for presentation; the committed execution copy has empty slots from this index onwards.</summary>
        public int BreakCanceledFromSlotIndex { get; private set; } = -1;
        internal IReadOnlyList<LegacySkill> ExecutionQueue => committedQueue ?? (IReadOnlyList<LegacySkill>)QueueSkills;

        public IReadOnlyList<LegacySkill> GetLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex > 2) throw new ArgumentOutOfRangeException(nameof(laneIndex));
            return laneViews[laneIndex];
        }

        public int CycleUses(int skillId) => Cycles.TryGetValue(skillId, out int uses) ? uses : 0;

        /// <summary>Accepted manual reservations from this lane, including techniques later canceled or returned.</summary>
        public int LaneReservationsThisTurn(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex > 2) throw new ArgumentOutOfRangeException(nameof(laneIndex));
            return laneReservationsThisTurn[laneIndex];
        }

        public int LaneReservationsRemaining(int laneIndex)
            => GetLane(laneIndex).Count == 0 ? 0 : LegacyLaneLimit.MaxReservations - LaneReservationsThisTurn(laneIndex);

        public int EffectiveCost(LegacySkill skill)
        {
            if (skill == null) return 0;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            if (effect == null || !effect.HasCycle || effect.CycleCostPerUse == 0) return skill.Cost;
            int uses = CycleUses(skill.Id);
            // Once committed, actual casts advance Cycles; do not count those reservations a second time.
            if (committedQueue == null)
                foreach (LegacySkill queued in QueueSkills)
                    if (uses < effect.CycleMaxCount && queued != null && !queued.IsWait && queued.Id == skill.Id) uses++;
            return checked(skill.Cost + uses * effect.CycleCostPerUse);
        }

        public void EffectivePowerRange(LegacySkill skill, out int min, out int max)
        {
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            int bonus = skill != null && effect != null && effect.HasCycle
                ? checked(CycleUses(skill.Id) * effect.CyclePowerPerUse) : 0;
            min = skill == null ? 0 : checked(skill.MinPower + bonus);
            max = skill == null ? 0 : checked(skill.MaxPower + bonus);
        }

        /// <summary>The cancel boost, read as <see cref="LegacyQueuedDuel"/>'s for the duel HUD: the queued technique
        /// whose removal effect armed it, or -1.</summary>
        public int QueuePowerSourceSlotIndex => BoostSource;

        /// <summary>Skills that technique removed (common actions such as breathing do not count); 0 without a source.</summary>
        public int QueuePowerRemovedSkillCount => BoostSource >= 0 ? BoostRemoved : 0;

        /// <summary>The first technique queued after the source, skipping common actions, or -1 if none is queued.</summary>
        public int QueuePowerTargetSlotIndex
        {
            get
            {
                if (BoostSource < 0) return -1;
                IReadOnlyList<LegacySkill> queue = ExecutionQueue;
                for (int i = BoostSource + 1; i < queue.Count; i++)
                    if (queue[i] != null && !queue[i].IsWait) return i;
                return -1;
            }
        }

        /// <summary>The boost's whole-skill power multiplier for a slot of this queue; 1 for any slot but the target.</summary>
        public int QueuePowerMultiplierForSlot(int slotIndex) => PowerMultiplierForSlot(slotIndex);

        internal void Reset()
        {
            State = new LegacyFighterState(maxHealth, maxResistance);
            Act = 0;
            NextActGain = BaseActGain;
            NextPlanningSeconds = 0;
            Cycles.Clear();
            StatusEffects.Clear();
            for (int i = 0; i < 3; i++) lanes[i].Clear();
            foreach (LegacySkill skill in initialSkills) lanes[skill.LaneIndex].Add(skill);
            BeginTurn();
        }

        internal void BeginTurn()
        {
            // 일회성 copies last one turn (LegacyQueuedDuel's StartPlanningTurn).
            LegacyKeywordLinks.ClearTemporary(lanes);
            PlanningSecondsBonus = NextPlanningSeconds;
            NextPlanningSeconds = 0;
            Act = Math.Min(MaximumAct, checked(Act + NextActGain));
            NextActGain = BaseActGain;
            State.BeginTurn();
            ActRecoveryStealUses.Clear();
            Array.Clear(laneReservationsThisTurn, 0, laneReservationsThisTurn.Length);
            QueueSkills.Clear();
            QueueCosts.Clear();
            committedQueue = null;
            BreakCanceledFromSlotIndex = -1;
            BoostSource = -1;
            BoostRemoved = BoostCap = 0;
            BreathsQueuedThisTurn = LaneCyclesThisTurn = 0;
            ReserveEffectsUsedThisTurn = LegacyReserveEffect.None;
            AutoBreathSlotIndex = LastQueuedSlotIndex = -1;
        }

        /// <summary>The turn's queue is final: 순환 +1 raises the counts it feeds (<see cref="LegacyKeywordLinks.ApplyCycleFeed"/>).</summary>
        internal void CommitTurn()
        {
            committedQueue = QueueSkills.ToArray();
            LegacyKeywordLinks.ApplyCycleFeed(committedQueue, Cycles);
        }

        internal void CancelAfterBreak(int breakingSlot)
        {
            if (BreakCanceledFromSlotIndex >= 0) return;
            BreakCanceledFromSlotIndex = breakingSlot + 1;
            for (int i = BreakCanceledFromSlotIndex; i < committedQueue.Length; i++) committedQueue[i] = null;
        }

        // The 순환 count a cast reads (its power bonus), before the cast records its use; 0 for a skill that does not cycle.
        internal int CycleUsesRead(LegacySkill skill)
            => skill != null && !skill.IsWait && LegacySkillDefinitions.Find(skill)?.Effect.HasCycle == true ? CycleUses(skill.Id) : 0;

        /// <summary>Whether <see cref="QueueLane"/> would reserve this lane's front skill now: the lane has one, it is not a
        /// wait, ACT covers its cost and the lane has a manual reservation left this turn.</summary>
        internal bool CanQueueLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex > 2 || lanes[laneIndex].Count == 0 ||
                laneReservationsThisTurn[laneIndex] >= LegacyLaneLimit.MaxReservations) return false;
            LegacySkill skill = lanes[laneIndex][0];
            return !skill.IsWait && Act >= EffectiveCost(skill);
        }

        /// <summary>Whether a breath may still be queued this turn.</summary>
        internal bool CanQueueBreath() => BreathsQueuedThisTurn < LocalVersusMatch.MaximumBreathsPerTurn;

        /// <summary>Whether a lane rotation would turn anything: some lane holds two skills or more.</summary>
        internal bool CanCycleLanes()
        {
            foreach (List<LegacySkill> lane in lanes)
                if (lane.Count >= 2) return true;
            return false;
        }

        internal bool QueueLane(int laneIndex)
        {
            if (!CanQueueLane(laneIndex)) return false;
            LegacySkill skill = lanes[laneIndex][0];
            int cost = EffectiveCost(skill);
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            LegacyQueueRemovalMode removal = effect?.QueueRemovalMode ?? LegacyQueueRemovalMode.None;
            int removed = 0, returned = 0;
            if (removal != LegacyQueueRemovalMode.None)
            {
                for (int i = QueueSkills.Count - 1; i >= 0; i--)
                {
                    if (QueueSkills[i].IsWait) continue;
                    removed++;
                    if (removal == LegacyQueueRemovalMode.Return) returned += QueueCosts[i];
                    QueueSkills.RemoveAt(i);
                    QueueCosts.RemoveAt(i);
                    if (i < AutoBreathSlotIndex) AutoBreathSlotIndex--;
                }
            }
            Act = Math.Min(MaximumAct, Act - cost + returned);
            QueueSkills.Add(skill);
            QueueCosts.Add(cost);
            LastQueuedSlotIndex = QueueSkills.Count - 1;
            lanes[laneIndex].RemoveAt(0);
            // A 일회성 skill is used up, as in the campaign's duel.
            if (!LegacySkillDefinitions.IsTemporary(skill)) lanes[laneIndex].Add(skill);
            if (removal != LegacyQueueRemovalMode.None)
            {
                BoostCap = effect?.QueuePowerMultiplierCap ?? 0;
                BoostSource = BoostCap > 0 ? LastQueuedSlotIndex : -1;
                BoostRemoved = BoostCap > 0 ? removed : 0;
            }
            // 예약 효과 숨고르기 +1, as the campaign's: one free breath right after the skill, once per turn. The same action
            // that reserved the skill reserves it, without ending this fighter's planning phase.
            if (effect != null && effect.ReserveBreath && (ReserveEffectsUsedThisTurn & LegacyReserveEffect.Breath) == 0)
            {
                QueueSkills.Add(LegacyCommonActions.Breathe);
                QueueCosts.Add(0);
                AutoBreathSlotIndex = QueueSkills.Count - 1;
                ReserveEffectsUsedThisTurn |= LegacyReserveEffect.Breath;
            }
            // 예약 효과 일회성 <ID> ×n, as the campaign's: every versus lane is open.
            LegacyReserveEffect used = ReserveEffectsUsedThisTurn;
            LegacyKeywordLinks.SpawnTemporary(skill, lanes, open => true, ref used);
            ReserveEffectsUsedThisTurn = used;
            laneReservationsThisTurn[laneIndex]++;
            return true;
        }

        // 사용 효과 앞 X마다 다음 턴 제한시간 as this fighter's queued skill starts its slot; what was actually added.
        internal int GainPlanningSeconds(LegacySkill skill, int slotIndex)
        {
            int next = NextPlanningSeconds;
            int gained = LegacyPlanningClock.AddBonus(ref next, LegacyKeywordLinks.AheadTimeBonus(QueueSkills, slotIndex, skill));
            NextPlanningSeconds = next;
            return gained;
        }

        internal bool QueueBreath()
        {
            if (!CanQueueBreath()) return false;
            QueueSkills.Add(LegacyCommonActions.Breathe);
            QueueCosts.Add(0);
            BreathsQueuedThisTurn++;
            return true;
        }

        internal bool CycleLanes()
        {
            if (!CanCycleLanes()) return false;
            foreach (List<LegacySkill> lane in lanes)
            {
                if (lane.Count < 2) continue;
                LegacySkill front = lane[0];
                lane.RemoveAt(0);
                lane.Add(front);
            }
            LaneCyclesThisTurn++;
            return true;
        }

        internal int PowerMultiplierForSlot(int slotIndex)
        {
            if (BoostSource < 0 || slotIndex <= BoostSource) return 1;
            IReadOnlyList<LegacySkill> queue = ExecutionQueue;
            for (int i = BoostSource + 1; i < queue.Count; i++)
                if (queue[i] != null && !queue[i].IsWait)
                    return i == slotIndex ? Math.Max(1, Math.Min(BoostRemoved, BoostCap)) : 1;
            return 1;
        }

        internal int PowerBonus(LegacySkill skill)
        {
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            return effect != null && effect.HasCycle ? checked(CycleUses(skill.Id) * effect.CyclePowerPerUse) : 0;
        }

        internal void RecordUse(LegacySkill skill)
        {
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            if (effect == null || !effect.HasCycle) return;
            int uses = CycleUses(skill.Id);
            if (uses < effect.CycleMaxCount) Cycles[skill.Id] = uses + 1;
        }
    }

    public sealed class LocalVersusSlot
    {
        internal LocalVersusSlot(int index, LegacySkill left, LegacySkill right, int leftPower, int rightPower,
            double leftReceived, double rightReceived, LocalVersusFighter leftFighter, LocalVersusFighter rightFighter)
        {
            SlotIndex = index;
            LeftSkill = left;
            RightSkill = right;
            LeftAttackCount = left?.Kind == LegacySkillKind.Attack ? left.AttackCount : 0;
            RightAttackCount = right?.Kind == LegacySkillKind.Attack ? right.AttackCount : 0;
            HitCount = Math.Max(1, Math.Max(left?.AttackCount ?? 0, right?.AttackCount ?? 0));
            LeftPower = leftPower;
            RightPower = rightPower;
            LeftReceivedMultiplier = leftReceived;
            RightReceivedMultiplier = rightReceived;
            LeftStartingHealth = leftFighter.State.Health;
            RightStartingHealth = rightFighter.State.Health;
            LeftStartingResistance = leftFighter.State.Resistance;
            RightStartingResistance = rightFighter.State.Resistance;
        }
        public int SlotIndex { get; }
        public LegacySkill LeftSkill { get; }
        public LegacySkill RightSkill { get; }
        public int LeftAttackCount { get; internal set; }
        public int RightAttackCount { get; internal set; }
        public int HitCount { get; internal set; }
        public int HitsResolved { get; internal set; }
        public bool IsResolved => HitsResolved >= HitCount;
        public LegacySkillFeedback LeftFeedback { get; internal set; } = LegacySkillFeedback.None;
        public LegacySkillFeedback RightFeedback { get; internal set; } = LegacySkillFeedback.None;
        /// <summary>The 순환 count each side's skill read as this slot started; 0 when it does not cycle.</summary>
        public int LeftCycleUses { get; internal set; }
        public int RightCycleUses { get; internal set; }
        /// <summary>The next turn's planning seconds each side's skill added as this slot started; 0 for none.</summary>
        public int LeftPlanningSecondsGained { get; internal set; }
        public int RightPlanningSecondsGained { get; internal set; }
        /// <summary>The most health each side has lost to a single hit of this slot so far (presentation only, never in the
        /// state hash: <see cref="LegacySlotFacts.PlayerLargestHitTaken"/>).</summary>
        public int LeftLargestHitTaken { get; internal set; }
        public int RightLargestHitTaken { get; internal set; }
        public int LeftBleedingHealthDamage { get; internal set; }
        public int RightBleedingHealthDamage { get; internal set; }
        public int LeftStartingBleeding { get; internal set; }
        public int RightStartingBleeding { get; internal set; }
        public int LeftBleedingDamagePerHit { get; internal set; }
        public int RightBleedingDamagePerHit { get; internal set; }
        public bool LeftBleedingConsumed { get; internal set; }
        public bool RightBleedingConsumed { get; internal set; }
        public int LeftStartingTension { get; internal set; }
        public int RightStartingTension { get; internal set; }
        public bool LeftTensionActive { get; internal set; }
        public bool RightTensionActive { get; internal set; }
        public bool LeftTensionConsumed { get; internal set; }
        public bool RightTensionConsumed { get; internal set; }
        internal int LeftTensionBoundary, RightTensionBoundary;
        /// <summary>Each side's skill's 상대 상태 조건 has held against the other where the rules checked it this slot (the
        /// recovery's gate, the slot's start for a damage multiplier, or one of its hits; presentation only, never in the
        /// state hash: <see cref="LegacySlotFacts.PlayerStateConditionHeld"/>).</summary>
        public bool LeftStateConditionHeld { get; internal set; }
        public bool RightStateConditionHeld { get; internal set; }
        internal int LeftPower { get; }
        internal int RightPower { get; }
        internal int LeftStatusBoundary, RightStatusBoundary;
        internal bool LeftIncomingStatusConsumed, RightIncomingStatusConsumed;
        internal double LeftReceivedMultiplier { get; }
        internal double RightReceivedMultiplier { get; }
        internal int LeftStartingHealth { get; }
        internal int RightStartingHealth { get; }
        internal int LeftStartingResistance { get; }
        internal int RightStartingResistance { get; }
    }

    public sealed class LocalVersusHit
    {
        internal LocalVersusHit(LocalVersusSlot slot, int index, bool leftAttack, bool rightAttack,
            int leftHealth, int rightHealth, int leftResistance, int rightResistance,
            LegacyHitImpact leftImpact, LegacyHitImpact rightImpact,
            bool leftCondition, bool rightCondition, LocalVersusOutcome outcome,
            int leftBleedingHealthDamage = 0, int rightBleedingHealthDamage = 0)
        {
            SlotIndex = slot.SlotIndex;
            LeftSkill = slot.LeftSkill;
            RightSkill = slot.RightSkill;
            LeftAttackCount = slot.LeftAttackCount;
            RightAttackCount = slot.RightAttackCount;
            HitIndex = index;
            LeftAttacked = leftAttack;
            RightAttacked = rightAttack;
            LeftHealthDamage = leftHealth;
            RightHealthDamage = rightHealth;
            LeftBleedingHealthDamage = leftBleedingHealthDamage;
            RightBleedingHealthDamage = rightBleedingHealthDamage;
            LeftResistanceDamage = leftResistance;
            RightResistanceDamage = rightResistance;
            LeftPushPower = leftImpact.PushPower;
            RightPushPower = rightImpact.PushPower;
            LeftDisplayedDamage = leftImpact.DisplayedDamage;
            RightDisplayedDamage = rightImpact.DisplayedDamage;
            LeftDamageReasons = leftImpact.DamageReasons;
            RightDamageReasons = rightImpact.DamageReasons;
            LeftAttackConditionMet = leftCondition;
            RightAttackConditionMet = rightCondition;
            IsFinalHit = slot.IsResolved;
            Outcome = outcome;
        }
        public int SlotIndex { get; }
        public LegacySkill LeftSkill { get; }
        public LegacySkill RightSkill { get; }
        public int HitIndex { get; }
        public int LeftAttackCount { get; }
        public int RightAttackCount { get; }
        public bool LeftAttacked { get; }
        public bool RightAttacked { get; }
        public int LeftBleedingHealthDamage { get; }
        public int RightBleedingHealthDamage { get; }
        public int LeftHealthDamage { get; }
        public int RightHealthDamage { get; }
        public int LeftResistanceDamage { get; }
        public int RightResistanceDamage { get; }
        /// <summary>The knockback the left fighter takes from this hit (<see cref="LegacyHitResult.PlayerPushPower"/>'s
        /// rule); 0 when the right fighter did not attack.</summary>
        public int LeftPushPower { get; }
        public int RightPushPower { get; }
        /// <summary>The damage number this hit shows on the left fighter, as a single-player hit's
        /// (<see cref="LegacyHitResult.PlayerDisplayedDamage"/>): the health damage before health stops at zero, or the
        /// resistance taken when the resistance held. 0 when the right fighter did not attack.</summary>
        public int LeftDisplayedDamage { get; }
        public int RightDisplayedDamage { get; }
        /// <summary>Modifiers on the hit received by this fighter; presentation only.</summary>
        public LegacyDamageReason LeftDamageReasons { get; }
        public LegacyDamageReason RightDamageReasons { get; }
        public bool LeftAttackConditionMet { get; }
        public bool RightAttackConditionMet { get; }
        public bool IsFinalHit { get; }
        public LocalVersusOutcome Outcome { get; }
    }

    public sealed class LocalVersusSlotResult
    {
        internal LocalVersusSlotResult(LocalVersusSlot slot, LocalVersusFighter left, LocalVersusFighter right,
            LocalVersusOutcome outcome)
        {
            SlotIndex = slot.SlotIndex;
            LeftSkill = slot.LeftSkill;
            RightSkill = slot.RightSkill;
            LeftHealthDamage = slot.LeftStartingHealth - left.State.Health;
            RightHealthDamage = slot.RightStartingHealth - right.State.Health;
            LeftResistanceDamage = slot.LeftStartingResistance - left.State.Resistance;
            RightResistanceDamage = slot.RightStartingResistance - right.State.Resistance;
            LeftAttackCount = slot.LeftAttackCount;
            RightAttackCount = slot.RightAttackCount;
            LeftBleedingHealthDamage = slot.LeftBleedingHealthDamage;
            RightBleedingHealthDamage = slot.RightBleedingHealthDamage;
            Outcome = outcome;
        }
        public int SlotIndex { get; }
        public LegacySkill LeftSkill { get; }
        public LegacySkill RightSkill { get; }
        public int LeftHealthDamage { get; }
        public int RightHealthDamage { get; }
        public int LeftAttackCount { get; }
        public int RightAttackCount { get; }
        public int LeftBleedingHealthDamage { get; }
        public int RightBleedingHealthDamage { get; }
        public int LeftResistanceDamage { get; }
        public int RightResistanceDamage { get; }
        public LocalVersusOutcome Outcome { get; }
    }

    /// <summary>Public queue planning for two players: the opening player finalizes a whole queue, then the other plans a
    /// response before both queues resolve. Both online peers run the same match from one seed; no dodge, pressure or meshing.</summary>
    public sealed class LocalVersusMatch
    {
        public const int MaximumBreathsPerTurn = 3;
        private readonly int randomSeed, initialOpeningPlayer;
        private Random random;
        private int committedPlannerCount, nextSlot, slotCount;
        // A forecast's copy (ForecastTurn) rolls every power at its range's average and never draws from the generator.
        private readonly bool averageRolls;

        public LocalVersusMatch(IReadOnlyList<LegacySkill> leftSkills, IReadOnlyList<LegacySkill> rightSkills,
            int leftHealth = 100, int leftResistance = 50, int rightHealth = 100, int rightResistance = 50,
            int randomSeed = 1, int roundLimit = int.MaxValue, int openingPlayer = 0)
        {
            if (roundLimit < 1) throw new ArgumentOutOfRangeException(nameof(roundLimit));
            if (openingPlayer < 0 || openingPlayer > 1) throw new ArgumentOutOfRangeException(nameof(openingPlayer));
            Left = new LocalVersusFighter(leftSkills, leftHealth, leftResistance);
            Right = new LocalVersusFighter(rightSkills, rightHealth, rightResistance);
            RoundLimit = roundLimit;
            this.randomSeed = randomSeed;
            initialOpeningPlayer = openingPlayer;
            Reset();
        }

        // A forecast's copy of a planning match: fighters of its own, the same rules, power at each range's average. Nothing
        // it does reaches the source, whose generator (and so its state hash) it never touches.
        private LocalVersusMatch(LocalVersusMatch source)
        {
            if (source.Phase != LegacyDuelPhase.Planning)
                throw new InvalidOperationException("Only a planning turn can be forecast.");
            averageRolls = true;
            Left = source.Left.Copy();
            Right = source.Right.Copy();
            RoundLimit = source.RoundLimit;
            randomSeed = source.randomSeed;
            initialOpeningPlayer = source.initialOpeningPlayer;
            random = new Random(source.randomSeed);
            RoundNumber = source.RoundNumber;
            OpeningPlayer = source.OpeningPlayer;
            CurrentPlanner = source.CurrentPlanner;
            LastPassPlayer = source.LastPassPlayer;
            committedPlannerCount = source.committedPlannerCount;
            Outcome = source.Outcome;
            Phase = LegacyDuelPhase.Planning;
        }

        /// <summary>편성 예상 from one player's side (<paramref name="perspectivePlayer"/>: 0 Left, 1 Right, read as the forecast's
        /// player): the turn's slots as both visible queues would resolve now, in order, into <paramref name="slots"/> (cleared
        /// first; empty outside planning). The queues resolve on a copy of the match with every power roll at its range's
        /// average, as <see cref="LegacyQueuedDuel.ForecastTurn(List{LegacySlotForecast})"/> does. The match, its queues,
        /// fighters and random generator never change.</summary>
        public void ForecastTurn(int perspectivePlayer, List<LegacySlotForecast> slots)
        {
            if (perspectivePlayer < 0 || perspectivePlayer > 1) throw new ArgumentOutOfRangeException(nameof(perspectivePlayer));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            slots.Clear();
            if (Phase != LegacyDuelPhase.Planning) return;
            new LocalVersusMatch(this).ResolveForecast(perspectivePlayer, slots);
        }

        /// <summary>The slot the front skill of <paramref name="laneIndex"/> would take if <paramref name="perspectivePlayer"/>
        /// queued it next, whoever holds the reservation turn now, forecast with the whole turn. Null outside planning or when
        /// that fighter cannot queue the lane's front (a wait, short of ACT or a lane at its reservation limit).</summary>
        public LegacySlotForecast ForecastIfQueued(int perspectivePlayer, int laneIndex)
        {
            if (perspectivePlayer < 0 || perspectivePlayer > 1) throw new ArgumentOutOfRangeException(nameof(perspectivePlayer));
            if (Phase != LegacyDuelPhase.Planning) return null;
            var copy = new LocalVersusMatch(this);
            LocalVersusFighter fighter = copy.GetFighter(perspectivePlayer);
            if (!fighter.QueueLane(laneIndex)) return null;
            // Not the queue's last entry: an automatic 숨고르기 may follow the skill.
            int slotIndex = fighter.LastQueuedSlotIndex;
            var slots = new List<LegacySlotForecast>();
            copy.ResolveForecast(perspectivePlayer, slots);
            return slotIndex < slots.Count ? slots[slotIndex] : null;
        }

        // Only on a forecast's copy: commits both queues and resolves every slot, read from the perspective player's side.
        private void ResolveForecast(int perspectivePlayer, List<LegacySlotForecast> slots)
        {
            LocalVersusFighter self = GetFighter(perspectivePlayer), other = GetFighter(1 - perspectivePlayer);
            Commit();
            for (int index = 0; index < slotCount; index++)
            {
                if (Phase != LegacyDuelPhase.Resolving)
                {
                    slots.Add(LegacySlotForecast.Unreached(index, index < self.ExecutionQueue.Count ? self.ExecutionQueue[index] : null,
                        index < other.ExecutionQueue.Count ? other.ExecutionQueue[index] : null, PerspectiveOutcome(Outcome, perspectivePlayer)));
                    continue;
                }
                bool selfWhole = !self.State.IsResistanceBroken, otherWhole = !other.State.IsResistanceBroken;
                int secondsBefore = self.NextPlanningSeconds;
                LocalVersusSlotResult result = ResolveNextSlot();
                bool left = perspectivePlayer == 0;
                slots.Add(new LegacySlotForecast(result.SlotIndex,
                    left ? result.LeftSkill : result.RightSkill, left ? result.RightSkill : result.LeftSkill,
                    left ? result.LeftHealthDamage : result.RightHealthDamage, left ? result.RightHealthDamage : result.LeftHealthDamage,
                    left ? result.LeftResistanceDamage : result.RightResistanceDamage,
                    left ? result.RightResistanceDamage : result.LeftResistanceDamage,
                    selfWhole && self.State.IsResistanceBroken, otherWhole && other.State.IsResistanceBroken,
                    PerspectiveOutcome(result.Outcome, perspectivePlayer), nextTurnSeconds: self.NextPlanningSeconds - secondsBefore)
                {
                    PlayerAttackCount = left ? result.LeftAttackCount : result.RightAttackCount,
                    EnemyAttackCount = left ? result.RightAttackCount : result.LeftAttackCount,
                    PlayerActionAttackCount = left ? result.LeftAttackCount : result.RightAttackCount,
                    EnemyActionAttackCount = left ? result.RightAttackCount : result.LeftAttackCount
                });
            }
        }

        private static DuelMatchOutcome PerspectiveOutcome(LocalVersusOutcome outcome, int perspectivePlayer)
        {
            switch (outcome)
            {
                case LocalVersusOutcome.LeftVictory:
                    return perspectivePlayer == 0 ? DuelMatchOutcome.PlayerVictory : DuelMatchOutcome.EnemyVictory;
                case LocalVersusOutcome.RightVictory:
                    return perspectivePlayer == 1 ? DuelMatchOutcome.PlayerVictory : DuelMatchOutcome.EnemyVictory;
                case LocalVersusOutcome.Draw: return DuelMatchOutcome.Draw;
                default: return DuelMatchOutcome.InProgress;
            }
        }

        public LocalVersusFighter Left { get; }
        public LocalVersusFighter Right { get; }
        public LocalVersusFighter GetFighter(int playerIndex) => playerIndex == 0 ? Left
            : playerIndex == 1 ? Right : throw new ArgumentOutOfRangeException(nameof(playerIndex));

        /// <summary>The 순환 count <paramref name="playerIndex"/>'s skill in a queue slot reads as that slot starts
        /// (<see cref="LegacyQueuedDuel.CycleUsesAtSlot"/>): while planning, its count plus the commit's 순환 +1 plus one per
        /// earlier copy, within the cap; while resolving, what the current slot read and, for later slots, the count now plus
        /// one per earlier copy not yet cast. 0 for a slot without a cycling skill.</summary>
        public int CycleUsesAtSlot(int playerIndex, int slotIndex)
        {
            LocalVersusFighter fighter = GetFighter(playerIndex);
            IReadOnlyList<LegacySkill> queue = fighter.ExecutionQueue;
            if (slotIndex < 0 || slotIndex >= queue.Count || queue[slotIndex] == null) return 0;
            int usesNow = fighter.CycleUses(queue[slotIndex].Id);
            if (Phase == LegacyDuelPhase.Planning) return LegacyKeywordLinks.PlannedCycleUses(queue, slotIndex, usesNow);
            if (CurrentSlot != null && CurrentSlot.SlotIndex == slotIndex)
                return playerIndex == 0 ? CurrentSlot.LeftCycleUses : CurrentSlot.RightCycleUses;
            return LegacyKeywordLinks.UpcomingCycleUses(queue, slotIndex, CurrentSlot != null ? CurrentSlot.SlotIndex + 1 : nextSlot,
                usesNow);
        }
        public int RoundNumber { get; private set; }
        public int RoundLimit { get; }
        public int OpeningPlayer { get; private set; }
        public int CurrentPlanner { get; private set; }
        /// <summary>Players who have finalized their queues this turn; a later reservation never clears a commitment.</summary>
        public int CommittedPlannerCount => committedPlannerCount;
        /// <summary>Compatibility name for <see cref="CommittedPlannerCount"/>.</summary>
        public int ConsecutivePasses => CommittedPlannerCount;
        public int LastPassPlayer { get; private set; }
        public LegacyDuelPhase Phase { get; private set; }
        public LocalVersusOutcome Outcome { get; private set; }
        public LocalVersusSlot CurrentSlot { get; private set; }

        /// <summary>The current slot as the clash motion reads it (<see cref="ILegacyDuelView.CurrentClash"/>) from
        /// <paramref name="perspectivePlayer"/>'s side (0 Left, 1 Right, read as the player): each side's rolled power per hit
        /// and a break since the slot began. Versus has no dodge or 반격. <see cref="LegacyClashInput.None"/> while no slot
        /// resolves. Reading never changes the match or its state hash.</summary>
        public LegacyClashInput CurrentClash(int perspectivePlayer)
        {
            if (perspectivePlayer < 0 || perspectivePlayer > 1) throw new ArgumentOutOfRangeException(nameof(perspectivePlayer));
            LocalVersusSlot slot = CurrentSlot;
            if (slot == null) return LegacyClashInput.None;
            LegacyClashInput left = new LegacyClashInput(slot.SlotIndex, LegacyClashMotion.ActionOf(slot.LeftSkill), slot.LeftPower, Math.Max(1, slot.LeftAttackCount),
                LegacyClashMotion.ActionOf(slot.RightSkill), slot.RightPower, Math.Max(1, slot.RightAttackCount),
                playerBroken: slot.LeftStartingResistance > 0 && Left.State.IsResistanceBroken,
                enemyBroken: slot.RightStartingResistance > 0 && Right.State.IsResistanceBroken,
                decided: slot.HitsResolved > 0);
            return perspectivePlayer == 0 ? left : left.Mirrored();
        }

        /// <summary>The current slot as the presentation director and the 간파 cue read it
        /// (<see cref="ILegacyDuelView.CurrentSlotFacts"/>) from <paramref name="perspectivePlayer"/>'s side (0 Left, 1 Right,
        /// read as the player): <see cref="CurrentClash"/>, both skills' ids, both fighters' maximum health, the health each
        /// lost this slot and its largest single hit, a fall, each side's met 상대 속성/종류 조건 and 상대 상태 조건. Versus never
        /// meshes. <see cref="LegacySlotFacts.None"/> while no slot resolves. Reading never changes the match or its state
        /// hash.</summary>
        public LegacySlotFacts CurrentSlotFacts(int perspectivePlayer)
        {
            if (perspectivePlayer < 0 || perspectivePlayer > 1) throw new ArgumentOutOfRangeException(nameof(perspectivePlayer));
            LocalVersusSlot slot = CurrentSlot;
            if (slot == null) return LegacySlotFacts.None;
            var left = new LegacySlotFacts(CurrentClash(0), LegacyQueuedDuel.SkillId(slot.LeftSkill),
                LegacyQueuedDuel.SkillId(slot.RightSkill), Left.State.MaxHealth, Right.State.MaxHealth,
                Math.Max(0, slot.LeftStartingHealth - Left.State.Health), Math.Max(0, slot.RightStartingHealth - Right.State.Health),
                slot.LeftLargestHitTaken, slot.RightLargestHitTaken,
                slot.LeftStartingHealth > 0 && Left.State.IsDefeated, slot.RightStartingHealth > 0 && Right.State.IsDefeated,
                LegacySkillConditions.MatchesOpponent(slot.LeftSkill, slot.RightSkill),
                LegacySkillConditions.MatchesOpponent(slot.RightSkill, slot.LeftSkill),
                slot.LeftStateConditionHeld, slot.RightStateConditionHeld,
                playerResistanceLost: slot.LeftStartingResistance - Left.State.Resistance,
                enemyResistanceLost: slot.RightStartingResistance - Right.State.Resistance);
            return perspectivePlayer == 0 ? left : left.Mirrored();
        }

        public int ResolutionSlotCount => slotCount;
        public bool IsTurnResolved => Phase == LegacyDuelPhase.Resolving && CurrentSlot == null && nextSlot >= slotCount;

        // Derived from existing execution progress: starting a slot exposes only the techniques actually executing
        // there. A break-canceled reservation remains in the public queue but its execution copy is null, so skipping
        // that slot cannot reveal it. No presentation-specific state changes the match or its deterministic hash.
        internal bool IsQueuedSkillRevealed(int playerIndex, int slotIndex)
        {
            LocalVersusFighter fighter = GetFighter(playerIndex);
            if (slotIndex < 0 || slotIndex >= fighter.Queue.Count) return false;
            if (slotIndex < LegacyQueueVisibility.InitiallyRevealedSlots) return true;
            bool started = slotIndex < nextSlot || CurrentSlot != null && CurrentSlot.SlotIndex == slotIndex;
            return started && slotIndex < fighter.ExecutionQueue.Count && fighter.ExecutionQueue[slotIndex] != null;
        }
        public bool IsFinished => Phase == LegacyDuelPhase.Finished;

        public bool TryQueueLane(int playerIndex, int laneIndex)
        {
            if (!IsCurrentPlanner(playerIndex) || !GetFighter(playerIndex).QueueLane(laneIndex)) return false;
            return true;
        }

        public bool TryQueueBreath(int playerIndex)
        {
            if (!IsCurrentPlanner(playerIndex) || !GetFighter(playerIndex).QueueBreath()) return false;
            return true;
        }

        /// <summary>Free lane rotation leaves the current player's reservation turn in place.</summary>
        public bool TryCycleLanes(int playerIndex)
            => IsCurrentPlanner(playerIndex) && GetFighter(playerIndex).CycleLanes();

        /// <summary>Whether <see cref="TryQueueLane"/> would take this command now. Like the other Can... reads it never
        /// changes the match: the guest asks its own copy before it sends a request to the host.</summary>
        public bool CanQueueLane(int playerIndex, int laneIndex)
            => IsCurrentPlanner(playerIndex) && GetFighter(playerIndex).CanQueueLane(laneIndex);

        /// <summary>Whether <see cref="TryQueueBreath"/> would take this command now.</summary>
        public bool CanQueueBreath(int playerIndex)
            => IsCurrentPlanner(playerIndex) && GetFighter(playerIndex).CanQueueBreath();

        /// <summary>Whether <see cref="TryCycleLanes"/> would turn a lane now.</summary>
        public bool CanCycleLanes(int playerIndex)
            => IsCurrentPlanner(playerIndex) && GetFighter(playerIndex).CanCycleLanes();

        /// <summary>Whether this player can finalize their current queue, including an empty one.</summary>
        public bool CanCommit(int playerIndex) => IsCurrentPlanner(playerIndex);

        /// <summary>Finalize this player's entire queue. The first commitment gives the other player the planning phase;
        /// the second starts resolving. A committed player cannot reserve again until the next combat turn.</summary>
        public bool TryCommit(int playerIndex)
        {
            if (!IsCurrentPlanner(playerIndex)) return false;
            LastPassPlayer = playerIndex;
            if (++committedPlannerCount == 2) Commit();
            else CurrentPlanner = 1 - CurrentPlanner;
            return true;
        }

        /// <summary>Compatibility command for <see cref="TryCommit"/> (the online Pass command keeps its wire value).</summary>
        public bool TryPass(int playerIndex) => TryCommit(playerIndex);

        public LocalVersusSlot BeginNextSlot()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot != null || nextSlot >= slotCount)
                throw new InvalidOperationException("An unresolved committed slot is required.");
            LegacySkill leftSkill = nextSlot < Left.ExecutionQueue.Count ? Left.ExecutionQueue[nextSlot] : null;
            LegacySkill rightSkill = nextSlot < Right.ExecutionQueue.Count ? Right.ExecutionQueue[nextSlot] : null;
            // As in the campaign, both fighters spend protection before this slot snapshots received damage.
            int leftProtectionSpend = Left.StatusEffects.SpendProtectionForPower(leftSkill);
            int rightProtectionSpend = Right.StatusEffects.SpendProtectionForPower(rightSkill);
            int leftPower = RollPower(Left, leftSkill, nextSlot, out int leftBuff, out int leftProtection, leftProtectionSpend);
            int rightPower = RollPower(Right, rightSkill, nextSlot, out int rightBuff, out int rightProtection, rightProtectionSpend);
            double leftReceived = Left.StatusEffects.ReceivedMultiplier;
            double rightReceived = Right.StatusEffects.ReceivedMultiplier;
            var slot = new LocalVersusSlot(nextSlot, leftSkill, rightSkill, leftPower, rightPower,
                leftReceived, rightReceived, Left, Right);
            CurrentSlot = slot;
            slot.LeftStartingBleeding = Left.StatusEffects.BleedingCount;
            slot.RightStartingBleeding = Right.StatusEffects.BleedingCount;
            slot.LeftStartingTension = Left.StatusEffects.TensionCount;
            slot.RightStartingTension = Right.StatusEffects.TensionCount;
            slot.LeftTensionBoundary = Left.StatusEffects.GrantSerial;
            slot.RightTensionBoundary = Right.StatusEffects.GrantSerial;
            slot.LeftStatusBoundary = Left.StatusEffects.GrantSerial;
            slot.RightStatusBoundary = Right.StatusEffects.GrantSerial;
            slot.LeftCycleUses = Left.CycleUsesRead(leftSkill);
            slot.RightCycleUses = Right.CycleUsesRead(rightSkill);
            slot.LeftPlanningSecondsGained = Left.GainPlanningSeconds(leftSkill, nextSlot);
            slot.RightPlanningSecondsGained = Right.GainPlanningSeconds(rightSkill, nextSlot);
            Left.RecordUse(leftSkill);
            Right.RecordUse(rightSkill);
            if (leftSkill != null && !leftSkill.IsWait) Left.StatusEffects.ConsumeTechnique();
            if (rightSkill != null && !rightSkill.IsWait) Right.StatusEffects.ConsumeTechnique();
            ApplySlotEffects(slot, leftBuff, leftProtection, rightBuff, rightProtection, leftProtectionSpend, rightProtectionSpend);
            return slot;
        }

        /// <summary>Whether the first hit of the current slot reaches only both blades, without health loss or a new
        /// break. Uses stored powers on copied fighters, leaving match state and random rolls untouched. It is identical
        /// from either player's perspective; the director separately checks the power gap and time allowance.</summary>
        public bool CanBindCurrentHit()
        {
            LocalVersusSlot slot = CurrentSlot;
            if (Phase != LegacyDuelPhase.Resolving || slot == null || slot.HitsResolved != 0 || slot.IsResolved ||
                !HasAttackHit(slot.LeftSkill, 0) || !HasAttackHit(slot.RightSkill, 0)) return false;
            LegacyFighterState left = Left.State.Copy(), right = Right.State.Copy();
            LegacyHitImpact rightImpact = ResolveHit(slot.LeftSkill, slot.RightSkill, slot.LeftPower, slot.RightPower,
                right, slot.RightReceivedMultiplier, out _);
            LegacyHitImpact leftImpact = ResolveHit(slot.RightSkill, slot.LeftSkill, slot.RightPower, slot.LeftPower,
                left, slot.LeftReceivedMultiplier, out _);
            left.ReceiveBleedingDamage(slot.LeftBleedingDamagePerHit);
            right.ReceiveBleedingDamage(slot.RightBleedingDamagePerHit);
            return LegacyQueuedDuel.IsResistanceOnlyHit(Left.State, left, leftImpact) &&
                LegacyQueuedDuel.IsResistanceOnlyHit(Right.State, right, rightImpact);
        }

        public LocalVersusHit ResolveNextHit()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot == null || CurrentSlot.IsResolved)
                throw new InvalidOperationException("There is no unresolved hit.");
            LocalVersusSlot slot = CurrentSlot;
            int index = slot.HitsResolved;
            int leftHealth = Left.State.Health, rightHealth = Right.State.Health;
            int leftResistance = Left.State.Resistance, rightResistance = Right.State.Resistance;
            bool leftAttack = HasAttackHit(slot.LeftSkill, index, slot.LeftAttackCount);
            bool rightAttack = HasAttackHit(slot.RightSkill, index, slot.RightAttackCount);
            bool leftCondition = false, rightCondition = false;
            // What each fighter receives, kept for presentation only (damage numbers and knockback).
            LegacyHitImpact leftImpact = default, rightImpact = default;
            // Both attacks at this hit index land before defeat is checked; later hits stop after a KO.
            if (leftAttack)
            {
                if (!slot.RightIncomingStatusConsumed)
                {
                    Right.StatusEffects.ConsumeIncomingAttack(slot.RightStatusBoundary);
                    slot.RightIncomingStatusConsumed = true;
                }
                rightImpact = ResolveHit(slot.LeftSkill, slot.RightSkill, slot.LeftPower, slot.RightPower,
                    Right.State, slot.RightReceivedMultiplier, out leftCondition);
            }
            if (rightAttack)
            {
                if (!slot.LeftIncomingStatusConsumed)
                {
                    Left.StatusEffects.ConsumeIncomingAttack(slot.LeftStatusBoundary);
                    slot.LeftIncomingStatusConsumed = true;
                }
                leftImpact = ResolveHit(slot.RightSkill, slot.LeftSkill, slot.RightPower, slot.LeftPower,
                    Left.State, slot.LeftReceivedMultiplier, out rightCondition);
            }
            int leftBleeding = leftAttack ? Left.State.ReceiveBleedingDamage(slot.LeftBleedingDamagePerHit) : 0;
            int rightBleeding = rightAttack ? Right.State.ReceiveBleedingDamage(slot.RightBleedingDamagePerHit) : 0;
            slot.LeftBleedingHealthDamage += leftBleeding;
            slot.RightBleedingHealthDamage += rightBleeding;
            if (leftResistance > 0 && Left.State.IsResistanceBroken) Left.CancelAfterBreak(slot.SlotIndex);
            if (rightResistance > 0 && Right.State.IsResistanceBroken) Right.CancelAfterBreak(slot.SlotIndex);
            slot.HitsResolved++;
            // Presentation only (LegacySlotFacts), outside the state hash: the largest single hits and a met state condition.
            slot.LeftLargestHitTaken = Math.Max(slot.LeftLargestHitTaken, leftHealth - Left.State.Health);
            slot.RightLargestHitTaken = Math.Max(slot.RightLargestHitTaken, rightHealth - Right.State.Health);
            if (leftCondition) slot.LeftStateConditionHeld = true;
            if (rightCondition) slot.RightStateConditionHeld = true;
            LocalVersusOutcome outcome = DetermineOutcome();
            if (outcome != LocalVersusOutcome.InProgress) slot.HitCount = slot.HitsResolved;
            ConsumeFinishedBleeding(slot);
            return new LocalVersusHit(slot, index, leftAttack, rightAttack,
                leftHealth - Left.State.Health, rightHealth - Right.State.Health,
                leftResistance - Left.State.Resistance, rightResistance - Right.State.Resistance,
                leftImpact, rightImpact, leftCondition, rightCondition, outcome, leftBleeding, rightBleeding);
        }

        private void ConsumeFinishedBleeding(LocalVersusSlot slot)
        {
            if (slot.HitsResolved == 0) return;
            if (!slot.LeftBleedingConsumed && slot.LeftBleedingDamagePerHit > 0 && slot.LeftSkill?.Kind == LegacySkillKind.Attack &&
                (slot.IsResolved || slot.HitsResolved >= slot.LeftAttackCount))
            {
                Left.StatusEffects.ConsumeBleedingAttack(slot.LeftStatusBoundary);
                slot.LeftBleedingConsumed = true;
            }
            if (!slot.RightBleedingConsumed && slot.RightBleedingDamagePerHit > 0 && slot.RightSkill?.Kind == LegacySkillKind.Attack &&
                (slot.IsResolved || slot.HitsResolved >= slot.RightAttackCount))
            {
                Right.StatusEffects.ConsumeBleedingAttack(slot.RightStatusBoundary);
                slot.RightBleedingConsumed = true;
            }
            if (!slot.LeftTensionConsumed && slot.LeftTensionActive && slot.LeftSkill?.Kind == LegacySkillKind.Attack &&
                (slot.IsResolved || slot.HitsResolved >= slot.LeftAttackCount))
            {
                Left.StatusEffects.ConsumeTensionAttack(slot.LeftTensionBoundary);
                slot.LeftTensionConsumed = true;
            }
            if (!slot.RightTensionConsumed && slot.RightTensionActive && slot.RightSkill?.Kind == LegacySkillKind.Attack &&
                (slot.IsResolved || slot.HitsResolved >= slot.RightAttackCount))
            {
                Right.StatusEffects.ConsumeTensionAttack(slot.RightTensionBoundary);
                slot.RightTensionConsumed = true;
            }
        }

        public LocalVersusSlotResult CompleteCurrentSlot()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot == null || !CurrentSlot.IsResolved)
                throw new InvalidOperationException("Every hit must resolve before completing a slot.");
            LocalVersusSlot slot = CurrentSlot;
            ConsumeFinishedBleeding(slot);
            nextSlot++;
            Outcome = DetermineOutcome();
            if (Outcome == LocalVersusOutcome.InProgress && nextSlot >= slotCount && RoundNumber >= RoundLimit)
                Outcome = LocalVersusOutcome.Draw;
            if (Outcome != LocalVersusOutcome.InProgress) Phase = LegacyDuelPhase.Finished;
            CurrentSlot = null;
            return new LocalVersusSlotResult(slot, Left, Right, Outcome);
        }

        public LocalVersusSlotResult ResolveNextSlot()
        {
            if (CurrentSlot == null) BeginNextSlot();
            while (!CurrentSlot.IsResolved) ResolveNextHit();
            return CompleteCurrentSlot();
        }

        public void BeginNextTurn()
        {
            if (!IsTurnResolved) throw new InvalidOperationException("Resolve all slots before the next turn.");
            RoundNumber++;
            OpeningPlayer = 1 - OpeningPlayer;
            StartPlanningTurn();
        }

        public void Reset()
        {
            random = new Random(randomSeed);
            Left.Reset();
            Right.Reset();
            RoundNumber = 1;
            OpeningPlayer = initialOpeningPlayer;
            Outcome = LocalVersusOutcome.InProgress;
            StartPlanningTurn(clearFighters: false);
        }

        private bool IsCurrentPlanner(int playerIndex)
            => Phase == LegacyDuelPhase.Planning && playerIndex >= 0 && playerIndex <= 1 && CurrentPlanner == playerIndex;

        private void Commit()
        {
            // 순환 +1 from each side's committed queue, as LegacyQueuedDuel.Commit.
            Left.CommitTurn();
            Right.CommitTurn();
            slotCount = Math.Max(Left.Queue.Count, Right.Queue.Count);
            nextSlot = 0;
            CurrentSlot = null;
            CurrentPlanner = -1;
            Phase = LegacyDuelPhase.Resolving;
            if (slotCount == 0 && RoundNumber >= RoundLimit)
            {
                Outcome = LocalVersusOutcome.Draw;
                Phase = LegacyDuelPhase.Finished;
            }
        }

        private void StartPlanningTurn(bool clearFighters = true)
        {
            if (clearFighters)
            {
                Left.BeginTurn();
                Right.BeginTurn();
            }
            CurrentSlot = null;
            nextSlot = slotCount = committedPlannerCount = 0;
            LastPassPlayer = -1;
            CurrentPlanner = OpeningPlayer;
            Phase = LegacyDuelPhase.Planning;
        }

        private int RollPower(LocalVersusFighter fighter, LegacySkill skill, int slotIndex,
            out int powerBuffPercent, out int protectionBuffPercent, int protectionSpendBonus = 0)
        {
            powerBuffPercent = fighter.StatusEffects.PowerPercent;
            protectionBuffPercent = fighter.StatusEffects.ProtectionPercent;
            if (skill == null || skill.IsWait) return 0;
            // 사용 효과 앞 X마다 위력 beside 순환, as LegacyQueuedDuel.BeginNextSlot.
            int bonus = checked(fighter.PowerBonus(skill) + LegacyKeywordLinks.AheadPowerBonus(fighter.ExecutionQueue, slotIndex, skill) +
                protectionSpendBonus + (skill.Kind == LegacySkillKind.Attack
                    ? (LegacySkillDefinitions.Find(skill)?.Effect.BleedingPowerBonus(fighter.StatusEffects.BleedingCount,
                        (ReferenceEquals(fighter, Left) ? Right : Left).StatusEffects.BleedingCount) ?? 0) +
                      (LegacySkillDefinitions.Find(skill)?.Effect.TensionPowerBonus(fighter.StatusEffects.TensionCount,
                        (ReferenceEquals(fighter, Left) ? Right : Left).StatusEffects.TensionCount) ?? 0) : 0));
            int roll = averageRolls
                ? LegacySlotForecast.AveragePower(checked(skill.MinPower + bonus), checked(skill.MaxPower + bonus))
                : random.Next(checked(skill.MinPower + bonus), checked(skill.MaxPower + bonus + 1));
            double multiplier = (100 + powerBuffPercent) / 100d * fighter.PowerMultiplierForSlot(slotIndex);
            return Math.Max(1, (int)Math.Floor(roll * multiplier / skill.AttackCount));
        }

        private void ApplySlotEffects(LocalVersusSlot slot, int leftPowerBuff, int leftProtectionBuff,
            int rightPowerBuff, int rightProtectionBuff, int leftProtectionSpend, int rightProtectionSpend)
        {
            LegacySkillEffect leftEffect = LegacySkillDefinitions.Find(slot.LeftSkill)?.Effect ?? LegacySkillEffect.None;
            LegacySkillEffect rightEffect = LegacySkillDefinitions.Find(slot.RightSkill)?.Effect ?? LegacySkillEffect.None;
            int leftTensionRemoved = Left.StatusEffects.ReduceTension(leftEffect.TensionReduction);
            int rightTensionRemoved = Right.StatusEffects.ReduceTension(rightEffect.TensionReduction);
            slot.LeftTensionActive = Left.StatusEffects.TensionActiveThrough(slot.LeftTensionBoundary);
            slot.RightTensionActive = Right.StatusEffects.TensionActiveThrough(slot.RightTensionBoundary);
            slot.LeftAttackCount = slot.LeftSkill?.Kind == LegacySkillKind.Attack
                ? Math.Max(1, slot.LeftSkill.AttackCount - (slot.LeftTensionActive ? LegacyStatusCollection.TensionHitReduction : 0)) : 0;
            slot.RightAttackCount = slot.RightSkill?.Kind == LegacySkillKind.Attack
                ? Math.Max(1, slot.RightSkill.AttackCount - (slot.RightTensionActive ? LegacyStatusCollection.TensionHitReduction : 0)) : 0;
            slot.HitCount = Math.Max(1, Math.Max(slot.LeftAttackCount, slot.RightAttackCount));
            // Hemostasis applies to the existing state before either fighter receives this slot's new bleeding.
            int leftRemoved = Left.StatusEffects.ReduceBleeding(leftEffect.BleedingReduction);
            int rightRemoved = Right.StatusEffects.ReduceBleeding(rightEffect.BleedingReduction);
            slot.LeftBleedingDamagePerHit = Left.StatusEffects.BleedingDamageThrough(slot.LeftStatusBoundary);
            slot.RightBleedingDamagePerHit = Right.StatusEffects.BleedingDamageThrough(slot.RightStatusBoundary);
            // Both casts read the same pre-slot ACT. Applying one first would let the other steal newly gained ACT,
            // or miss a threshold it had already met. The simultaneous loss also opens room below each ACT cap.
            int leftAct = Left.Act, rightAct = Right.Act;
            int leftStolen = ActToSteal(slot.LeftSkill, Left.State, leftEffect, rightAct);
            int rightStolen = ActToSteal(slot.RightSkill, Right.State, rightEffect, leftAct);
            int leftReceived = Math.Min(leftStolen, Left.MaximumAct - (leftAct - rightStolen));
            int rightReceived = Math.Min(rightStolen, Right.MaximumAct - (rightAct - leftStolen));
            Left.Act = leftAct - rightStolen + leftReceived;
            Right.Act = rightAct - leftStolen + rightReceived;
            // Match both sides against the pre-effect state, so declaration order does not decide eligibility.
            bool leftState = leftEffect.OpponentStateHolds(Right.State);
            bool rightState = rightEffect.OpponentStateHolds(Left.State);
            bool leftOpponent = !leftEffect.HasOpponentCondition || LegacySkillConditions.MatchesOpponent(slot.LeftSkill, slot.RightSkill);
            bool rightOpponent = !rightEffect.HasOpponentCondition || LegacySkillConditions.MatchesOpponent(slot.RightSkill, slot.LeftSkill);
            int leftRestored = leftEffect.ResistanceRecoveryPercent > 0 && (!leftEffect.HasOpponentStateCondition || leftState)
                ? Left.State.RestoreResistance(LegacySkillConditions.ResistanceRecoveryAmount(Left.State, leftEffect.ResistanceRecoveryPercent)) : 0;
            int rightRestored = rightEffect.ResistanceRecoveryPercent > 0 && (!rightEffect.HasOpponentStateCondition || rightState)
                ? Right.State.RestoreResistance(LegacySkillConditions.ResistanceRecoveryAmount(Right.State, rightEffect.ResistanceRecoveryPercent)) : 0;
            bool leftWhole = !Left.State.IsResistanceBroken, rightWhole = !Right.State.IsResistanceBroken;
            int rightReduced = leftOpponent ? Right.State.ReduceResistance(leftEffect.OpponentResistanceReduction) : 0;
            int leftReduced = rightOpponent ? Left.State.ReduceResistance(rightEffect.OpponentResistanceReduction) : 0;
            int rightBrokenAmount = leftOpponent && leftEffect.BreaksOpponent ? Right.State.BreakResistance() : 0;
            int leftBrokenAmount = rightOpponent && rightEffect.BreaksOpponent ? Left.State.BreakResistance() : 0;
            if (leftWhole && Left.State.IsResistanceBroken) Left.CancelAfterBreak(slot.SlotIndex);
            if (rightWhole && Right.State.IsResistanceBroken) Right.CancelAfterBreak(slot.SlotIndex);
            if (leftOpponent) Left.NextActGain = checked(Left.NextActGain + leftEffect.ActGain);
            if (rightOpponent) Right.NextActGain = checked(Right.NextActGain + rightEffect.ActGain);
            // Ordinary gains apply first. Both transfers then read one snapshot, so neither can steal the other's
            // newly stolen recovery or be denied a valid use by declaration order. Each target retains at least one.
            int leftRecovery = Left.NextActGain, rightRecovery = Right.NextActGain;
            int leftRecoveryStolen = RecoveryToSteal(slot.LeftSkill, slot.RightSkill, Left, leftEffect, rightRecovery);
            int rightRecoveryStolen = RecoveryToSteal(slot.RightSkill, slot.LeftSkill, Right, rightEffect, leftRecovery);
            Left.NextActGain = checked(leftRecovery - rightRecoveryStolen + leftRecoveryStolen);
            Right.NextActGain = checked(rightRecovery - leftRecoveryStolen + rightRecoveryStolen);
            if (leftRecoveryStolen > 0) Left.ActRecoveryStealUses.Add(slot.LeftSkill.Id);
            if (rightRecoveryStolen > 0) Right.ActRecoveryStealUses.Add(slot.RightSkill.Id);
            Left.StatusEffects.Grant(leftEffect);
            Right.StatusEffects.Grant(rightEffect);
            int leftInflicted = leftEffect.BleedingToInflict(slot.RightStartingBleeding);
            int rightInflicted = rightEffect.BleedingToInflict(slot.LeftStartingBleeding);
            Left.StatusEffects.GrantBleeding(leftEffect.SelfBleedingCount);
            Right.StatusEffects.GrantBleeding(leftInflicted);
            Right.StatusEffects.GrantBleeding(rightEffect.SelfBleedingCount);
            Left.StatusEffects.GrantBleeding(rightInflicted);
            Left.StatusEffects.GrantTension(leftEffect.SelfTensionCount);
            Right.StatusEffects.GrantTension(leftEffect.OpponentTensionCount);
            Right.StatusEffects.GrantTension(rightEffect.SelfTensionCount);
            Left.StatusEffects.GrantTension(rightEffect.OpponentTensionCount);
            slot.LeftFeedback = Feedback(slot.LeftSkill, leftEffect, leftPowerBuff, leftProtectionBuff,
                leftRestored, rightReduced + rightBrokenAmount, rightBrokenAmount > 0, leftOpponent, leftState,
                leftStolen, leftReceived, leftRecoveryStolen);
            slot.RightFeedback = Feedback(slot.RightSkill, rightEffect, rightPowerBuff, rightProtectionBuff,
                rightRestored, leftReduced + leftBrokenAmount, leftBrokenAmount > 0, rightOpponent, rightState,
                rightStolen, rightReceived, rightRecoveryStolen);
            slot.LeftFeedback = slot.LeftFeedback.WithUsePowerBonuses(leftProtectionSpend,
                LegacyKeywordLinks.AheadAttackHitsPowerBonus(Left.ExecutionQueue, slot.SlotIndex, slot.LeftSkill),
                leftEffect.HasAheadHitPower ? LegacyKeywordLinks.CountAheadAttackHits(Left.ExecutionQueue, slot.SlotIndex, slot.LeftSkill) : 0);
            slot.RightFeedback = slot.RightFeedback.WithUsePowerBonuses(rightProtectionSpend,
                LegacyKeywordLinks.AheadAttackHitsPowerBonus(Right.ExecutionQueue, slot.SlotIndex, slot.RightSkill),
                rightEffect.HasAheadHitPower ? LegacyKeywordLinks.CountAheadAttackHits(Right.ExecutionQueue, slot.SlotIndex, slot.RightSkill) : 0);
            slot.LeftFeedback = slot.LeftFeedback.WithBleedingEffects(leftEffect.SelfBleedingCount, leftInflicted, leftRemoved,
                leftEffect.SelfBleedingRequired > 0 && slot.LeftStartingBleeding >= leftEffect.SelfBleedingRequired ? leftEffect.SelfBleedingPowerBonus : 0,
                leftEffect.OpponentBleedingRequired > 0 && slot.RightStartingBleeding >= leftEffect.OpponentBleedingRequired ? leftEffect.OpponentBleedingPowerBonus : 0);
            slot.RightFeedback = slot.RightFeedback.WithBleedingEffects(rightEffect.SelfBleedingCount, rightInflicted, rightRemoved,
                rightEffect.SelfBleedingRequired > 0 && slot.RightStartingBleeding >= rightEffect.SelfBleedingRequired ? rightEffect.SelfBleedingPowerBonus : 0,
                rightEffect.OpponentBleedingRequired > 0 && slot.LeftStartingBleeding >= rightEffect.OpponentBleedingRequired ? rightEffect.OpponentBleedingPowerBonus : 0);
            slot.LeftFeedback = slot.LeftFeedback.WithTensionEffects(leftEffect.SelfTensionCount, leftEffect.OpponentTensionCount, leftTensionRemoved,
                leftEffect.SelfTensionRequired > 0 && slot.LeftStartingTension >= leftEffect.SelfTensionRequired ? leftEffect.SelfTensionPowerBonus : 0,
                leftEffect.OpponentTensionRequired > 0 && slot.RightStartingTension >= leftEffect.OpponentTensionRequired ? leftEffect.OpponentTensionPowerBonus : 0);
            slot.RightFeedback = slot.RightFeedback.WithTensionEffects(rightEffect.SelfTensionCount, rightEffect.OpponentTensionCount, rightTensionRemoved,
                rightEffect.SelfTensionRequired > 0 && slot.RightStartingTension >= rightEffect.SelfTensionRequired ? rightEffect.SelfTensionPowerBonus : 0,
                rightEffect.OpponentTensionRequired > 0 && slot.LeftStartingTension >= rightEffect.OpponentTensionRequired ? rightEffect.OpponentTensionPowerBonus : 0);
            slot.LeftFeedback = slot.LeftFeedback.WithConditionalDamage(
                ArmedConditionalDamage(slot.LeftSkill, slot.RightSkill, slot.LeftPower, slot.RightPower, Right.State));
            slot.RightFeedback = slot.RightFeedback.WithConditionalDamage(
                ArmedConditionalDamage(slot.RightSkill, slot.LeftSkill, slot.RightPower, slot.LeftPower, Left.State));
            // 간파 (presentation only, outside the state hash): each side's 상대 상태 조건 as the rules read it here, the
            // recovery's gate before any effect, a damage multiplier's once both sides' effects are in.
            slot.LeftStateConditionHeld = leftEffect.ResistanceRecoveryPercent > 0 && leftEffect.HasOpponentStateCondition && leftState ||
                LegacySkillConditions.DamageStateHolds(slot.LeftSkill, Right.State);
            slot.RightStateConditionHeld = rightEffect.ResistanceRecoveryPercent > 0 && rightEffect.HasOpponentStateCondition && rightState ||
                LegacySkillConditions.DamageStateHolds(slot.RightSkill, Left.State);
        }

        private static int ActToSteal(LegacySkill skill, LegacyFighterState owner, LegacySkillEffect effect, int targetAct)
            => skill != null && !skill.IsWait && !owner.IsDefeated && effect.HasActSteal && targetAct >= effect.ActStealThreshold
                ? Math.Min(targetAct, effect.ActStealAmount) : 0;

        private static int RecoveryToSteal(LegacySkill skill, LegacySkill opponent, LocalVersusFighter owner,
            LegacySkillEffect effect, int targetRecovery)
            => skill != null && !skill.IsWait && !owner.State.IsDefeated && effect.HasActRecoverySteal &&
                opponent?.Kind == LegacySkillKind.Attack && !owner.HasUsedActRecoverySteal(skill.Id)
                ? Math.Min(effect.ActRecoveryStealAmount, Math.Max(0, targetRecovery - 1)) : 0;

        private static LegacySkillFeedback Feedback(LegacySkill skill, LegacySkillEffect effect, int powerBuff,
            int protectionBuff, int restored, int reduced, bool broken, bool opponentMatched, bool stateMatched,
            int actStolen, int actReceived, int actRecoveryStolen)
        {
            if (skill == null || skill.IsWait)
                return new LegacySkillFeedback(skill, false, false, powerBuff, protectionBuff);
            bool condition = actStolen > 0 || actRecoveryStolen > 0 || restored > 0 || effect.HasOpponentCondition && opponentMatched ||
                effect.ConditionalDamagePercent > 0 && stateMatched;
            bool activated = actStolen > 0 || actRecoveryStolen > 0 || restored > 0 || reduced > 0 || opponentMatched && effect.ActGain > 0 || effect.HasBuff;
            return new LegacySkillFeedback(skill, condition, activated, powerBuff, protectionBuff,
                effect.BuffPowerPercent, effect.BuffProtectionPercent, effect.BuffSlots,
                opponentMatched ? effect.ActGain : 0, restored, reduced, broken, actStolen, actReceived)
                .WithActRecoverySteal(actRecoveryStolen);
        }

        private static int ArmedConditionalDamage(LegacySkill skill, LegacySkill opposingSkill,
            int power, int opposingPower, LegacyFighterState target)
        {
            if (skill == null || skill.Kind != LegacySkillKind.Attack) return 0;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            if (effect == null || effect.ConditionalDamagePercent <= 0 || !effect.OpponentStateHolds(target)) return 0;
            if (opposingSkill != null && opposingSkill.Kind == LegacySkillKind.Attack && !target.IsResistanceBroken) return 0;
            if (opposingSkill != null && opposingSkill.Kind == LegacySkillKind.Defence &&
                power <= opposingPower / skill.AttackCount) return 0;
            return effect.ConditionalDamagePercent;
        }

        private static bool HasAttackHit(LegacySkill skill, int hitIndex, int attackCount = -1)
            => skill != null && skill.Kind == LegacySkillKind.Attack && hitIndex < (attackCount >= 0 ? attackCount : skill.AttackCount);

        private static LegacyHitImpact ResolveHit(LegacySkill attack, LegacySkill defence, int attackPower, int defencePower,
            LegacyFighterState target, double received, out bool conditionMet)
        {
            int power = attackPower;
            if (defence != null && defence.Kind == LegacySkillKind.Defence)
                power = Math.Max(0, attackPower - defencePower / attack.AttackCount);
            LegacySkillEffect effect = LegacySkillDefinitions.Find(attack)?.Effect;
            int brokenBonus = target.IsResistanceBroken ? effect?.BrokenTargetDamagePercent ?? 0 : 0;
            conditionMet = effect != null && effect.ConditionalDamagePercent > 0 && effect.OpponentStateHolds(target);
            LegacyHitImpact impact = target.ReceiveHit(power, defence != null && defence.Kind == LegacySkillKind.Attack,
                received, false, brokenBonus, conditionMet ? effect.ConditionalDamagePercent : 0);
            return power < attackPower ? impact.WithReasons(LegacyDamageReason.GuardReduction) : impact;
        }

        private LocalVersusOutcome DetermineOutcome()
        {
            if (Left.State.IsDefeated && Right.State.IsDefeated) return LocalVersusOutcome.Draw;
            if (Right.State.IsDefeated) return LocalVersusOutcome.LeftVictory;
            if (Left.State.IsDefeated) return LocalVersusOutcome.RightVictory;
            return LocalVersusOutcome.InProgress;
        }
    }
}

