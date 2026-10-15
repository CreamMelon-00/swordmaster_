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
        internal readonly List<LegacySkill> QueueSkills = new List<LegacySkill>();
        internal readonly List<int> QueueCosts = new List<int>();
        internal readonly Dictionary<int, int> Cycles = new Dictionary<int, int>();
        internal readonly List<LocalVersusBuff> Buffs = new List<LocalVersusBuff>();
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
            }
            QueueSkills.AddRange(source.QueueSkills);
            QueueCosts.AddRange(source.QueueCosts);
            foreach (KeyValuePair<int, int> uses in source.Cycles) Cycles.Add(uses.Key, uses.Value);
            foreach (LocalVersusBuff buff in source.Buffs) Buffs.Add(new LocalVersusBuff(buff.Power, buff.Protection, buff.SlotsRemaining));
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

        public IReadOnlyList<LegacySkill> GetLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex > 2) throw new ArgumentOutOfRangeException(nameof(laneIndex));
            return laneViews[laneIndex];
        }

        public int CycleUses(int skillId) => Cycles.TryGetValue(skillId, out int uses) ? uses : 0;

        public int EffectiveCost(LegacySkill skill)
        {
            if (skill == null) return 0;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            return effect != null && effect.HasCycle
                ? checked(skill.Cost + CycleUses(skill.Id) * effect.CycleCostPerUse) : skill.Cost;
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
                for (int i = BoostSource + 1; i < QueueSkills.Count; i++)
                    if (!QueueSkills[i].IsWait) return i;
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
            QueueSkills.Clear();
            QueueCosts.Clear();
            Buffs.Clear();
            BoostSource = -1;
            BoostRemoved = BoostCap = 0;
            BreathsQueuedThisTurn = LaneCyclesThisTurn = 0;
            ReserveEffectsUsedThisTurn = LegacyReserveEffect.None;
            AutoBreathSlotIndex = LastQueuedSlotIndex = -1;
        }

        /// <summary>The turn's queue is final: 순환 +1 raises the counts it feeds (<see cref="LegacyKeywordLinks.ApplyCycleFeed"/>).</summary>
        internal void CommitTurn() => LegacyKeywordLinks.ApplyCycleFeed(QueueSkills, Cycles);

        // The 순환 count a cast reads (its power bonus), before the cast records its use; 0 for a skill that does not cycle.
        internal int CycleUsesRead(LegacySkill skill)
            => skill != null && !skill.IsWait && LegacySkillDefinitions.Find(skill)?.Effect.HasCycle == true ? CycleUses(skill.Id) : 0;

        /// <summary>Whether <see cref="QueueLane"/> would reserve this lane's front skill now: the lane has one, it is not a
        /// wait, and ACT covers its cost.</summary>
        internal bool CanQueueLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex > 2 || lanes[laneIndex].Count == 0) return false;
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
            // that reserved the skill reserves it, so the reservation turn passes once.
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
            for (int i = BoostSource + 1; i < QueueSkills.Count; i++)
                if (!QueueSkills[i].IsWait)
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

    internal sealed class LocalVersusBuff
    {
        internal LocalVersusBuff(int power, int protection, int slots)
        {
            Power = power;
            Protection = protection;
            SlotsRemaining = slots;
        }
        internal readonly int Power, Protection;
        internal int SlotsRemaining;
    }

    public sealed class LocalVersusSlot
    {
        internal LocalVersusSlot(int index, LegacySkill left, LegacySkill right, int leftPower, int rightPower,
            double leftReceived, double rightReceived, LocalVersusFighter leftFighter, LocalVersusFighter rightFighter)
        {
            SlotIndex = index;
            LeftSkill = left;
            RightSkill = right;
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
        internal int LeftPower { get; }
        internal int RightPower { get; }
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
            bool leftCondition, bool rightCondition, LocalVersusOutcome outcome)
        {
            SlotIndex = slot.SlotIndex;
            LeftSkill = slot.LeftSkill;
            RightSkill = slot.RightSkill;
            HitIndex = index;
            LeftAttacked = leftAttack;
            RightAttacked = rightAttack;
            LeftHealthDamage = leftHealth;
            RightHealthDamage = rightHealth;
            LeftResistanceDamage = leftResistance;
            RightResistanceDamage = rightResistance;
            LeftPushPower = leftImpact.PushPower;
            RightPushPower = rightImpact.PushPower;
            LeftDisplayedDamage = leftImpact.DisplayedDamage;
            RightDisplayedDamage = rightImpact.DisplayedDamage;
            LeftAttackConditionMet = leftCondition;
            RightAttackConditionMet = rightCondition;
            IsFinalHit = slot.IsResolved;
            Outcome = outcome;
        }
        public int SlotIndex { get; }
        public LegacySkill LeftSkill { get; }
        public LegacySkill RightSkill { get; }
        public int HitIndex { get; }
        public bool LeftAttacked { get; }
        public bool RightAttacked { get; }
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
            Outcome = outcome;
        }
        public int SlotIndex { get; }
        public LegacySkill LeftSkill { get; }
        public LegacySkill RightSkill { get; }
        public int LeftHealthDamage { get; }
        public int RightHealthDamage { get; }
        public int LeftResistanceDamage { get; }
        public int RightResistanceDamage { get; }
        public LocalVersusOutcome Outcome { get; }
    }

    /// <summary>Public alternating reservations for two players (the online friend duel: both peers run the same match from
    /// one seed); no dodge, pressure or meshing.</summary>
    public sealed class LocalVersusMatch
    {
        public const int MaximumBreathsPerTurn = 3;
        private readonly int randomSeed, initialOpeningPlayer;
        private Random random;
        private int consecutivePasses, nextSlot, slotCount;
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
            consecutivePasses = source.consecutivePasses;
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
        /// that fighter cannot queue the lane's front (a wait or short of ACT).</summary>
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
                    slots.Add(LegacySlotForecast.Unreached(index, index < self.Queue.Count ? self.Queue[index] : null,
                        index < other.Queue.Count ? other.Queue[index] : null, PerspectiveOutcome(Outcome, perspectivePlayer)));
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
                    PerspectiveOutcome(result.Outcome, perspectivePlayer), nextTurnSeconds: self.NextPlanningSeconds - secondsBefore));
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
            IReadOnlyList<LegacySkill> queue = fighter.Queue;
            if (slotIndex < 0 || slotIndex >= queue.Count) return 0;
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
        public int ConsecutivePasses => consecutivePasses;
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
            LegacyClashInput left = LegacyClashInput.FromSkills(slot.SlotIndex, slot.LeftSkill, slot.LeftPower,
                slot.RightSkill, slot.RightPower,
                playerBroken: slot.LeftStartingResistance > 0 && Left.State.IsResistanceBroken,
                enemyBroken: slot.RightStartingResistance > 0 && Right.State.IsResistanceBroken,
                decided: slot.HitsResolved > 0);
            return perspectivePlayer == 0 ? left : left.Mirrored();
        }

        public int ResolutionSlotCount => slotCount;
        public bool IsTurnResolved => Phase == LegacyDuelPhase.Resolving && CurrentSlot == null && nextSlot >= slotCount;
        public bool IsFinished => Phase == LegacyDuelPhase.Finished;

        public bool TryQueueLane(int playerIndex, int laneIndex)
        {
            if (!IsCurrentPlanner(playerIndex) || !GetFighter(playerIndex).QueueLane(laneIndex)) return false;
            NextPlannerAfterAction();
            return true;
        }

        public bool TryQueueBreath(int playerIndex)
        {
            if (!IsCurrentPlanner(playerIndex) || !GetFighter(playerIndex).QueueBreath()) return false;
            NextPlannerAfterAction();
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

        /// <summary>One pass yields; two consecutive passes start resolving the visible queues.</summary>
        public bool TryPass(int playerIndex)
        {
            if (!IsCurrentPlanner(playerIndex)) return false;
            LastPassPlayer = playerIndex;
            if (++consecutivePasses == 2) Commit();
            else CurrentPlanner = 1 - CurrentPlanner;
            return true;
        }

        public LocalVersusSlot BeginNextSlot()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot != null || nextSlot >= slotCount)
                throw new InvalidOperationException("An unresolved committed slot is required.");
            LegacySkill leftSkill = nextSlot < Left.Queue.Count ? Left.Queue[nextSlot] : null;
            LegacySkill rightSkill = nextSlot < Right.Queue.Count ? Right.Queue[nextSlot] : null;
            int leftPower = RollPower(Left, leftSkill, nextSlot, out int leftBuff, out int leftProtection);
            int rightPower = RollPower(Right, rightSkill, nextSlot, out int rightBuff, out int rightProtection);
            double leftReceived = ReceivedMultiplier(Left.Buffs);
            double rightReceived = ReceivedMultiplier(Right.Buffs);
            var slot = new LocalVersusSlot(nextSlot, leftSkill, rightSkill, leftPower, rightPower,
                leftReceived, rightReceived, Left, Right);
            CurrentSlot = slot;
            slot.LeftCycleUses = Left.CycleUsesRead(leftSkill);
            slot.RightCycleUses = Right.CycleUsesRead(rightSkill);
            slot.LeftPlanningSecondsGained = Left.GainPlanningSeconds(leftSkill, nextSlot);
            slot.RightPlanningSecondsGained = Right.GainPlanningSeconds(rightSkill, nextSlot);
            Left.RecordUse(leftSkill);
            Right.RecordUse(rightSkill);
            TickBuffs(Left.Buffs);
            TickBuffs(Right.Buffs);
            ApplySlotEffects(slot, leftBuff, leftProtection, rightBuff, rightProtection);
            return slot;
        }

        public LocalVersusHit ResolveNextHit()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot == null || CurrentSlot.IsResolved)
                throw new InvalidOperationException("There is no unresolved hit.");
            LocalVersusSlot slot = CurrentSlot;
            int index = slot.HitsResolved;
            int leftHealth = Left.State.Health, rightHealth = Right.State.Health;
            int leftResistance = Left.State.Resistance, rightResistance = Right.State.Resistance;
            bool leftAttack = HasAttackHit(slot.LeftSkill, index);
            bool rightAttack = HasAttackHit(slot.RightSkill, index);
            bool leftCondition = false, rightCondition = false;
            // What each fighter receives, kept for presentation only (damage numbers and knockback).
            LegacyHitImpact leftImpact = default, rightImpact = default;
            // Both attacks at this hit index land before defeat is checked; later hits stop after a KO.
            if (leftAttack)
                rightImpact = ResolveHit(slot.LeftSkill, slot.RightSkill, slot.LeftPower, slot.RightPower,
                    Right.State, slot.RightReceivedMultiplier, out leftCondition);
            if (rightAttack)
                leftImpact = ResolveHit(slot.RightSkill, slot.LeftSkill, slot.RightPower, slot.LeftPower,
                    Left.State, slot.LeftReceivedMultiplier, out rightCondition);
            slot.HitsResolved++;
            LocalVersusOutcome outcome = DetermineOutcome();
            if (outcome != LocalVersusOutcome.InProgress) slot.HitCount = slot.HitsResolved;
            return new LocalVersusHit(slot, index, leftAttack, rightAttack,
                leftHealth - Left.State.Health, rightHealth - Right.State.Health,
                leftResistance - Left.State.Resistance, rightResistance - Right.State.Resistance,
                leftImpact, rightImpact, leftCondition, rightCondition, outcome);
        }

        public LocalVersusSlotResult CompleteCurrentSlot()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot == null || !CurrentSlot.IsResolved)
                throw new InvalidOperationException("Every hit must resolve before completing a slot.");
            LocalVersusSlot slot = CurrentSlot;
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

        private void NextPlannerAfterAction()
        {
            consecutivePasses = 0;
            LastPassPlayer = -1;
            CurrentPlanner = 1 - CurrentPlanner;
        }

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
            nextSlot = slotCount = consecutivePasses = 0;
            LastPassPlayer = -1;
            CurrentPlanner = OpeningPlayer;
            Phase = LegacyDuelPhase.Planning;
        }

        private int RollPower(LocalVersusFighter fighter, LegacySkill skill, int slotIndex,
            out int powerBuffPercent, out int protectionBuffPercent)
        {
            powerBuffPercent = 0;
            foreach (LocalVersusBuff buff in fighter.Buffs) powerBuffPercent += buff.Power;
            protectionBuffPercent = 0;
            foreach (LocalVersusBuff buff in fighter.Buffs) protectionBuffPercent += buff.Protection;
            if (skill == null || skill.IsWait) return 0;
            // 사용 효과 앞 X마다 위력 beside 순환, as LegacyQueuedDuel.BeginNextSlot.
            int bonus = checked(fighter.PowerBonus(skill) + LegacyKeywordLinks.AheadPowerBonus(fighter.Queue, slotIndex, skill));
            int roll = averageRolls
                ? LegacySlotForecast.AveragePower(checked(skill.MinPower + bonus), checked(skill.MaxPower + bonus))
                : random.Next(checked(skill.MinPower + bonus), checked(skill.MaxPower + bonus + 1));
            double multiplier = (100 + powerBuffPercent) / 100d * fighter.PowerMultiplierForSlot(slotIndex);
            return Math.Max(1, (int)Math.Floor(roll * multiplier / skill.AttackCount));
        }

        private static double ReceivedMultiplier(List<LocalVersusBuff> buffs)
        {
            int percent = 100;
            foreach (LocalVersusBuff buff in buffs) percent -= buff.Protection;
            return Math.Max(0, percent) / 100d;
        }

        private static void TickBuffs(List<LocalVersusBuff> buffs)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
                if (--buffs[i].SlotsRemaining == 0) buffs.RemoveAt(i);
        }

        private void ApplySlotEffects(LocalVersusSlot slot, int leftPowerBuff, int leftProtectionBuff,
            int rightPowerBuff, int rightProtectionBuff)
        {
            LegacySkillEffect leftEffect = LegacySkillDefinitions.Find(slot.LeftSkill)?.Effect ?? LegacySkillEffect.None;
            LegacySkillEffect rightEffect = LegacySkillDefinitions.Find(slot.RightSkill)?.Effect ?? LegacySkillEffect.None;
            // Match both sides against the pre-effect state, so declaration order does not decide eligibility.
            bool leftState = leftEffect.OpponentStateHolds(Right.State);
            bool rightState = rightEffect.OpponentStateHolds(Left.State);
            bool leftOpponent = !leftEffect.HasOpponentCondition || LegacySkillConditions.MatchesOpponent(slot.LeftSkill, slot.RightSkill);
            bool rightOpponent = !rightEffect.HasOpponentCondition || LegacySkillConditions.MatchesOpponent(slot.RightSkill, slot.LeftSkill);
            int leftRestored = leftEffect.ResistanceRecoveryPercent > 0 && (!leftEffect.HasOpponentStateCondition || leftState)
                ? Left.State.RestoreResistance(LegacySkillConditions.ResistanceRecoveryAmount(Left.State, leftEffect.ResistanceRecoveryPercent)) : 0;
            int rightRestored = rightEffect.ResistanceRecoveryPercent > 0 && (!rightEffect.HasOpponentStateCondition || rightState)
                ? Right.State.RestoreResistance(LegacySkillConditions.ResistanceRecoveryAmount(Right.State, rightEffect.ResistanceRecoveryPercent)) : 0;
            int rightReduced = leftOpponent ? Right.State.ReduceResistance(leftEffect.OpponentResistanceReduction) : 0;
            int leftReduced = rightOpponent ? Left.State.ReduceResistance(rightEffect.OpponentResistanceReduction) : 0;
            int rightBrokenAmount = leftOpponent && leftEffect.BreaksOpponent ? Right.State.BreakResistance() : 0;
            int leftBrokenAmount = rightOpponent && rightEffect.BreaksOpponent ? Left.State.BreakResistance() : 0;
            if (leftOpponent) Left.NextActGain = checked(Left.NextActGain + leftEffect.ActGain);
            if (rightOpponent) Right.NextActGain = checked(Right.NextActGain + rightEffect.ActGain);
            if (leftEffect.HasBuff) Left.Buffs.Add(new LocalVersusBuff(leftEffect.BuffPowerPercent,
                leftEffect.BuffProtectionPercent, leftEffect.BuffSlots));
            if (rightEffect.HasBuff) Right.Buffs.Add(new LocalVersusBuff(rightEffect.BuffPowerPercent,
                rightEffect.BuffProtectionPercent, rightEffect.BuffSlots));
            slot.LeftFeedback = Feedback(slot.LeftSkill, leftEffect, leftPowerBuff, leftProtectionBuff,
                leftRestored, rightReduced + rightBrokenAmount, rightBrokenAmount > 0, leftOpponent, leftState);
            slot.RightFeedback = Feedback(slot.RightSkill, rightEffect, rightPowerBuff, rightProtectionBuff,
                rightRestored, leftReduced + leftBrokenAmount, leftBrokenAmount > 0, rightOpponent, rightState);
            slot.LeftFeedback = slot.LeftFeedback.WithConditionalDamage(
                ArmedConditionalDamage(slot.LeftSkill, slot.RightSkill, slot.LeftPower, slot.RightPower, Right.State));
            slot.RightFeedback = slot.RightFeedback.WithConditionalDamage(
                ArmedConditionalDamage(slot.RightSkill, slot.LeftSkill, slot.RightPower, slot.LeftPower, Left.State));
        }

        private static LegacySkillFeedback Feedback(LegacySkill skill, LegacySkillEffect effect, int powerBuff,
            int protectionBuff, int restored, int reduced, bool broken, bool opponentMatched, bool stateMatched)
        {
            if (skill == null || skill.IsWait)
                return new LegacySkillFeedback(skill, false, false, powerBuff, protectionBuff);
            bool condition = restored > 0 || effect.HasOpponentCondition && opponentMatched ||
                effect.ConditionalDamagePercent > 0 && stateMatched;
            bool activated = restored > 0 || reduced > 0 || opponentMatched && effect.ActGain > 0 || effect.HasBuff;
            return new LegacySkillFeedback(skill, condition, activated, powerBuff, protectionBuff,
                effect.BuffPowerPercent, effect.BuffProtectionPercent, effect.BuffSlots,
                opponentMatched ? effect.ActGain : 0, restored, reduced, broken);
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

        private static bool HasAttackHit(LegacySkill skill, int hitIndex)
            => skill != null && skill.Kind == LegacySkillKind.Attack && hitIndex < skill.AttackCount;

        private static LegacyHitImpact ResolveHit(LegacySkill attack, LegacySkill defence, int attackPower, int defencePower,
            LegacyFighterState target, double received, out bool conditionMet)
        {
            int power = attackPower;
            if (defence != null && defence.Kind == LegacySkillKind.Defence)
                power = Math.Max(0, attackPower - defencePower / attack.AttackCount);
            LegacySkillEffect effect = LegacySkillDefinitions.Find(attack)?.Effect;
            int brokenBonus = target.IsResistanceBroken ? effect?.BrokenTargetDamagePercent ?? 0 : 0;
            conditionMet = effect != null && effect.ConditionalDamagePercent > 0 && effect.OpponentStateHolds(target);
            return target.ReceiveHit(power, defence != null && defence.Kind == LegacySkillKind.Attack,
                received, false, brokenBonus, conditionMet ? effect.ConditionalDamagePercent : 0);
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

