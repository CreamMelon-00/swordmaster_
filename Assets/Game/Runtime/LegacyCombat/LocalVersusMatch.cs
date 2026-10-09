using System;
using System.Collections.Generic;

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

        public LegacyFighterState State { get; private set; }
        public int Act { get; internal set; }
        public int NextActGain { get; internal set; }
        public int BaseActGain => 3;
        public int MaximumAct => 10;
        public int BreathsQueuedThisTurn { get; internal set; }
        public int BreathsRemainingThisTurn => LocalVersusMatch.MaximumBreathsPerTurn - BreathsQueuedThisTurn;
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

        internal void Reset()
        {
            State = new LegacyFighterState(maxHealth, maxResistance);
            Act = 0;
            NextActGain = BaseActGain;
            Cycles.Clear();
            for (int i = 0; i < 3; i++) lanes[i].Clear();
            foreach (LegacySkill skill in initialSkills) lanes[skill.LaneIndex].Add(skill);
            BeginTurn();
        }

        internal void BeginTurn()
        {
            Act = Math.Min(MaximumAct, checked(Act + NextActGain));
            NextActGain = BaseActGain;
            State.BeginTurn();
            QueueSkills.Clear();
            QueueCosts.Clear();
            Buffs.Clear();
            BoostSource = -1;
            BoostRemoved = BoostCap = 0;
            BreathsQueuedThisTurn = LaneCyclesThisTurn = 0;
        }

        internal bool QueueLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex > 2 || lanes[laneIndex].Count == 0) return false;
            LegacySkill skill = lanes[laneIndex][0];
            if (skill.IsWait) return false;
            int cost = EffectiveCost(skill);
            if (Act < cost) return false;
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
                }
            }
            Act = Math.Min(MaximumAct, Act - cost + returned);
            QueueSkills.Add(skill);
            QueueCosts.Add(cost);
            lanes[laneIndex].RemoveAt(0);
            lanes[laneIndex].Add(skill);
            if (removal != LegacyQueueRemovalMode.None)
            {
                BoostCap = effect?.QueuePowerMultiplierCap ?? 0;
                BoostSource = BoostCap > 0 ? QueueSkills.Count - 1 : -1;
                BoostRemoved = BoostCap > 0 ? removed : 0;
            }
            return true;
        }

        internal bool QueueBreath()
        {
            if (BreathsQueuedThisTurn >= LocalVersusMatch.MaximumBreathsPerTurn) return false;
            QueueSkills.Add(LegacyCommonActions.Breathe);
            QueueCosts.Add(0);
            BreathsQueuedThisTurn++;
            return true;
        }

        internal bool CycleLanes()
        {
            bool turned = false;
            foreach (List<LegacySkill> lane in lanes)
            {
                if (lane.Count < 2) continue;
                LegacySkill front = lane[0];
                lane.RemoveAt(0);
                lane.Add(front);
                turned = true;
            }
            if (turned) LaneCyclesThisTurn++;
            return turned;
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

    /// <summary>Public alternating reservations for two local players; no dodge, pressure or meshing.</summary>
    public sealed class LocalVersusMatch
    {
        public const int MaximumBreathsPerTurn = 3;
        private readonly int randomSeed, initialOpeningPlayer;
        private Random random;
        private int consecutivePasses, nextSlot, slotCount;

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

        public LocalVersusFighter Left { get; }
        public LocalVersusFighter Right { get; }
        public LocalVersusFighter GetFighter(int playerIndex) => playerIndex == 0 ? Left
            : playerIndex == 1 ? Right : throw new ArgumentOutOfRangeException(nameof(playerIndex));
        public int RoundNumber { get; private set; }
        public int RoundLimit { get; }
        public int OpeningPlayer { get; private set; }
        public int CurrentPlanner { get; private set; }
        public int ConsecutivePasses => consecutivePasses;
        public int LastPassPlayer { get; private set; }
        public LegacyDuelPhase Phase { get; private set; }
        public LocalVersusOutcome Outcome { get; private set; }
        public LocalVersusSlot CurrentSlot { get; private set; }
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
            // Both attacks at this hit index land before defeat is checked; later hits stop after a KO.
            if (leftAttack)
                ResolveHit(slot.LeftSkill, slot.RightSkill, slot.LeftPower, slot.RightPower,
                    Right.State, slot.RightReceivedMultiplier, out leftCondition);
            if (rightAttack)
                ResolveHit(slot.RightSkill, slot.LeftSkill, slot.RightPower, slot.LeftPower,
                    Left.State, slot.LeftReceivedMultiplier, out rightCondition);
            slot.HitsResolved++;
            LocalVersusOutcome outcome = DetermineOutcome();
            if (outcome != LocalVersusOutcome.InProgress) slot.HitCount = slot.HitsResolved;
            return new LocalVersusHit(slot, index, leftAttack, rightAttack,
                leftHealth - Left.State.Health, rightHealth - Right.State.Health,
                leftResistance - Left.State.Resistance, rightResistance - Right.State.Resistance,
                leftCondition, rightCondition, outcome);
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
            int bonus = fighter.PowerBonus(skill);
            int roll = random.Next(checked(skill.MinPower + bonus), checked(skill.MaxPower + bonus + 1));
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

        private static void ResolveHit(LegacySkill attack, LegacySkill defence, int attackPower, int defencePower,
            LegacyFighterState target, double received, out bool conditionMet)
        {
            int power = attackPower;
            if (defence != null && defence.Kind == LegacySkillKind.Defence)
                power = Math.Max(0, attackPower - defencePower / attack.AttackCount);
            LegacySkillEffect effect = LegacySkillDefinitions.Find(attack)?.Effect;
            int brokenBonus = target.IsResistanceBroken ? effect?.BrokenTargetDamagePercent ?? 0 : 0;
            conditionMet = effect != null && effect.ConditionalDamagePercent > 0 && effect.OpponentStateHolds(target);
            target.ReceiveHit(power, defence != null && defence.Kind == LegacySkillKind.Attack,
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

