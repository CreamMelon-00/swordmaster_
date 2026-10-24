using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A versus match seen from one player's side, so the single-player duel HUD and its presentation can show
    /// it: that player is always the HUD's player (its desk, its queue row, the arena's left) and the other the enemy,
    /// whichever side of the match each one is. The host plays Left and the guest Right, so the guest's view is the match
    /// mirrored. Versus has no 반격, 맞물림 or steps, so those read empty; every lane, 숨고르기 and 넘기기 are open
    /// (<see cref="Features"/>). Both exact queues remain live data; <see cref="IsEnemySkillRevealed"/> decides which
    /// opposing identities presentation may show. Reading never changes the match, its rules or its state hash.</summary>
    public sealed class LocalVersusPerspective : ILegacyDuelView
    {
        private LocalVersusSlot shownSource;
        private LocalVersusPerspectiveSlot shownSlot;

        /// <param name="localPlayer">The match player shown as the HUD's player: 0 (Left, the host) or 1 (Right, the guest).</param>
        public LocalVersusPerspective(LocalVersusMatch match, int localPlayer)
        {
            Match = match ?? throw new ArgumentNullException(nameof(match));
            if (localPlayer < 0 || localPlayer > 1) throw new ArgumentOutOfRangeException(nameof(localPlayer));
            LocalPlayer = localPlayer;
        }

        public LocalVersusMatch Match { get; }
        /// <summary>The match player shown as the HUD's player (0 = Left, 1 = Right).</summary>
        public int LocalPlayer { get; }
        public int OpponentPlayer => 1 - LocalPlayer;
        public LocalVersusFighter Me => Match.GetFighter(LocalPlayer);
        public LocalVersusFighter Opponent => Match.GetFighter(OpponentPlayer);
        /// <summary>Planning, and this player holds the reservation turn.</summary>
        public bool IsMyTurn => Match.Phase == LegacyDuelPhase.Planning && Match.CurrentPlanner == LocalPlayer;
        /// <summary>Planning, and the opponent holds the reservation turn.</summary>
        public bool IsOpponentsTurn => Match.Phase == LegacyDuelPhase.Planning && Match.CurrentPlanner == OpponentPlayer;
        /// <summary>The match's result as single play reads it: this player's win is <see cref="DuelMatchOutcome.PlayerVictory"/>.</summary>
        public DuelMatchOutcome Outcome => ToDuelOutcome(Match.Outcome);

        /// <summary>Whether a match player (0 = Left, 1 = Right) is shown on the player's side: the arena's left, the HUD's
        /// own desk and queue row.</summary>
        public bool IsPlayerSide(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex > 1) throw new ArgumentOutOfRangeException(nameof(playerIndex));
            return playerIndex == LocalPlayer;
        }

        /// <summary>The match player (0 = Left, 1 = Right) shown on the player's side, or on the enemy's.</summary>
        public int PlayerIndex(bool playerSide) => playerSide ? LocalPlayer : OpponentPlayer;

        /// <summary>Of a pair kept per match side (Left, Right), the one that belongs to the player's side: a clock, a slot
        /// result's damage, a skill.</summary>
        public T ForPlayer<T>(T left, T right) => LocalPlayer == 0 ? left : right;

        /// <summary>Of a pair kept per match side (Left, Right), the one that belongs to the enemy's side.</summary>
        public T ForEnemy<T>(T left, T right) => LocalPlayer == 0 ? right : left;

        public DuelMatchOutcome ToDuelOutcome(LocalVersusOutcome outcome)
        {
            switch (outcome)
            {
                case LocalVersusOutcome.InProgress: return DuelMatchOutcome.InProgress;
                case LocalVersusOutcome.Draw: return DuelMatchOutcome.Draw;
                case LocalVersusOutcome.LeftVictory:
                    return LocalPlayer == 0 ? DuelMatchOutcome.PlayerVictory : DuelMatchOutcome.EnemyVictory;
                case LocalVersusOutcome.RightVictory:
                    return LocalPlayer == 1 ? DuelMatchOutcome.PlayerVictory : DuelMatchOutcome.EnemyVictory;
                default: throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }

        /// <summary>A match slot seen from this side; the same object for as long as that slot is the one asked about, so
        /// the HUD keeps a slot's feedback while it plays (<see cref="ILegacyDuelView.CurrentSlot"/>). Null for null.</summary>
        public LocalVersusPerspectiveSlot Slot(LocalVersusSlot slot)
        {
            if (slot == null) return null;
            if (!ReferenceEquals(shownSource, slot))
            {
                shownSource = slot;
                shownSlot = new LocalVersusPerspectiveSlot(slot, LocalPlayer);
            }
            return shownSlot;
        }

        /// <summary>A resolved hit seen from this side, named as a single-player hit is (<see cref="LegacyHitResult"/>).</summary>
        public LocalVersusPerspectiveHit Hit(LocalVersusHit hit)
            => hit == null ? null : new LocalVersusPerspectiveHit(hit, LocalPlayer, ToDuelOutcome(hit.Outcome));

        // What the duel HUD reads (ILegacyDuelView): this player's desk, both fighters and both queues.
        public CombatFeature Features => CombatFeature.All;
        public LegacyDuelPhase Phase => Match.Phase;
        public int RoundNumber => Match.RoundNumber;
        public int Act => Me.Act;
        public int PlayerMaximumAct => Me.MaximumAct;
        public int BreathsRemainingThisTurn => Me.BreathsRemainingThisTurn;
        public int LaneReservationsThisTurn(int laneIndex) => Me.LaneReservationsThisTurn(laneIndex);
        public int LaneReservationsRemaining(int laneIndex) => Me.LaneReservationsRemaining(laneIndex);
        public LegacyFighterState Player => Me.State;
        public LegacyFighterState Enemy => Opponent.State;
        public IReadOnlyList<LegacyStatusSnapshot> PlayerStatuses => Me.Statuses;
        public IReadOnlyList<LegacyStatusSnapshot> EnemyStatuses => Opponent.Statuses;
        public IReadOnlyList<LegacySkill> PlayerQueue => Me.Queue;
        public IReadOnlyList<LegacySkill> EnemyQueue => Opponent.Queue;
        public bool IsEnemySkillRevealed(int slotIndex) => Match.IsQueuedSkillRevealed(OpponentPlayer, slotIndex);
        public int PlayerBreakCanceledFromSlotIndex => Me.BreakCanceledFromSlotIndex;
        public int EnemyBreakCanceledFromSlotIndex => Opponent.BreakCanceledFromSlotIndex;
        public LocalVersusPerspectiveSlot CurrentSlot => Slot(Match.CurrentSlot);
        ILegacySlotView ILegacyDuelView.CurrentSlot => CurrentSlot;
        /// <summary>The current slot's clash from this side (<see cref="LocalVersusMatch.CurrentClash"/>): the guest's view is
        /// the host's mirrored.</summary>
        public LegacyClashInput CurrentClash => Match.CurrentClash(LocalPlayer);
        /// <summary>The current slot's facts from this side (<see cref="LocalVersusMatch.CurrentSlotFacts"/>): the guest's view
        /// is the host's mirrored, so each client rates and cues the slot for its own player.</summary>
        public LegacySlotFacts CurrentSlotFacts => Match.CurrentSlotFacts(LocalPlayer);
        public IReadOnlyList<LegacySkill> GetLane(int laneIndex) => Me.GetLane(laneIndex);
        public int EffectiveCost(LegacySkill skill) => Me.EffectiveCost(skill);
        public void EffectivePowerRange(LegacySkill skill, out int min, out int max) => Me.EffectivePowerRange(skill, out min, out max);
        public int CycleUses(int skillId) => Me.CycleUses(skillId);
        // The opponent cycles too: its skills' explanation (Tab) reads its own uses, cost and power.
        public int EnemyCycleUses(int skillId) => Opponent.CycleUses(skillId);
        public int EnemyEffectiveCost(LegacySkill skill) => Opponent.EffectiveCost(skill);
        public void EnemyEffectivePowerRange(LegacySkill skill, out int min, out int max)
            => Opponent.EffectivePowerRange(skill, out min, out max);
        public int CycleUsesAtSlot(int slotIndex) => Match.CycleUsesAtSlot(LocalPlayer, slotIndex);
        public int EnemyCycleUsesAtSlot(int slotIndex) => Match.CycleUsesAtSlot(OpponentPlayer, slotIndex);
        public LegacyReserveEffect ReserveEffectsUsedThisTurn => Me.ReserveEffectsUsedThisTurn;
        public int AutoBreathSlotIndex => Me.AutoBreathSlotIndex;
        public int EnemyAutoBreathSlotIndex => Opponent.AutoBreathSlotIndex;

        // Versus has no 반격.
        public LegacyCounter PlayerCounter => null;
        public LegacyCounter EnemyCounter => null;
        public int PlayerCountersRemaining => 0;
        public int EnemyCountersRemaining => 0;
        public void ForecastEnemyCounterSlots(List<int> slots) => ClearSlots(slots);
        public void ForecastPlayerCounterSlots(List<int> slots) => ClearSlots(slots);
        public LegacySkill EnemyCounterFacing(LegacySkill skill) => null;

        // Nor 맞물림: nothing meshes.
        public int MeshPercent => 0;
        public LegacyMeshSlot PlayerMesh(int slotIndex) => LegacyMeshSlot.Unmeshed(slotIndex);
        public LegacyMeshSlot PlayerMeshIfQueued(int laneIndex) => LegacyMeshSlot.Unmeshed(Me.Queue.Count);

        // The cancel boost is versus's too (LocalVersusFighter).
        public int QueuePowerSourceSlotIndex => Me.QueuePowerSourceSlotIndex;
        public int QueuePowerRemovedSkillCount => Me.QueuePowerRemovedSkillCount;
        public int QueuePowerTargetSlotIndex => Me.QueuePowerTargetSlotIndex;
        public int QueuePowerMultiplierForSlot(int slotIndex) => Me.QueuePowerMultiplierForSlot(slotIndex);

        // 편성 예상 from this side: the match forecasts on a copy of itself (LocalVersusMatch.ForecastTurn), so reading it
        // never changes the match or its state hash.
        public void ForecastTurn(List<LegacySlotForecast> slots) => Match.ForecastTurn(LocalPlayer, slots);
        public LegacySlotForecast ForecastIfQueued(int laneIndex) => Match.ForecastIfQueued(LocalPlayer, laneIndex);

        private static void ClearSlots(List<int> slots)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            slots.Clear();
        }
    }

    /// <summary>A versus slot seen from one player's side (<see cref="LocalVersusPerspective.Slot"/>); it reads the match's
    /// slot as it goes.</summary>
    public sealed class LocalVersusPerspectiveSlot : ILegacySlotView
    {
        private readonly bool leftIsPlayer;

        internal LocalVersusPerspectiveSlot(LocalVersusSlot source, int localPlayer)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            leftIsPlayer = localPlayer == 0;
        }

        public LocalVersusSlot Source { get; }
        public int SlotIndex => Source.SlotIndex;
        public LegacySkill PlayerSkill => leftIsPlayer ? Source.LeftSkill : Source.RightSkill;
        public LegacySkill EnemySkill => leftIsPlayer ? Source.RightSkill : Source.LeftSkill;
        public LegacySkillFeedback PlayerFeedback => leftIsPlayer ? Source.LeftFeedback : Source.RightFeedback;
        public LegacySkillFeedback EnemyFeedback => leftIsPlayer ? Source.RightFeedback : Source.LeftFeedback;
        public int HitCount => Source.HitCount;
        public int HitsResolved => Source.HitsResolved;
        public bool IsResolved => Source.IsResolved;
    }

    /// <summary>A resolved versus hit seen from one player's side (<see cref="LocalVersusPerspective.Hit"/>), named as
    /// <see cref="LegacyHitResult"/>: a Player… value is about the player's fighter (the damage it took, whether it
    /// attacked), an Enemy… value about the enemy's.</summary>
    public sealed class LocalVersusPerspectiveHit
    {
        internal LocalVersusPerspectiveHit(LocalVersusHit hit, int localPlayer, DuelMatchOutcome outcome)
        {
            Source = hit ?? throw new ArgumentNullException(nameof(hit));
            bool left = localPlayer == 0;
            SlotIndex = hit.SlotIndex;
            HitIndex = hit.HitIndex;
            PlayerSkill = left ? hit.LeftSkill : hit.RightSkill;
            EnemySkill = left ? hit.RightSkill : hit.LeftSkill;
            PlayerAttacked = left ? hit.LeftAttacked : hit.RightAttacked;
            EnemyAttacked = left ? hit.RightAttacked : hit.LeftAttacked;
            PlayerHealthDamage = left ? hit.LeftHealthDamage : hit.RightHealthDamage;
            EnemyHealthDamage = left ? hit.RightHealthDamage : hit.LeftHealthDamage;
            PlayerBleedingHealthDamage = left ? hit.LeftBleedingHealthDamage : hit.RightBleedingHealthDamage;
            EnemyBleedingHealthDamage = left ? hit.RightBleedingHealthDamage : hit.LeftBleedingHealthDamage;
            PlayerResistanceDamage = left ? hit.LeftResistanceDamage : hit.RightResistanceDamage;
            EnemyResistanceDamage = left ? hit.RightResistanceDamage : hit.LeftResistanceDamage;
            PlayerPushPower = left ? hit.LeftPushPower : hit.RightPushPower;
            EnemyPushPower = left ? hit.RightPushPower : hit.LeftPushPower;
            PlayerDisplayedDamage = left ? hit.LeftDisplayedDamage : hit.RightDisplayedDamage;
            EnemyDisplayedDamage = left ? hit.RightDisplayedDamage : hit.LeftDisplayedDamage;
            PlayerDamageReasons = left ? hit.LeftDamageReasons : hit.RightDamageReasons;
            EnemyDamageReasons = left ? hit.RightDamageReasons : hit.LeftDamageReasons;
            PlayerAttackConditionMet = left ? hit.LeftAttackConditionMet : hit.RightAttackConditionMet;
            EnemyAttackConditionMet = left ? hit.RightAttackConditionMet : hit.LeftAttackConditionMet;
            IsFinalHit = hit.IsFinalHit;
            Outcome = outcome;
        }

        public LocalVersusHit Source { get; }
        public int SlotIndex { get; }
        public int HitIndex { get; }
        public LegacySkill PlayerSkill { get; }
        public LegacySkill EnemySkill { get; }
        public bool PlayerAttacked { get; }
        public bool EnemyAttacked { get; }
        public int PlayerBleedingHealthDamage { get; }
        public int EnemyBleedingHealthDamage { get; }
        public int PlayerHealthDamage { get; }
        public int EnemyHealthDamage { get; }
        public int PlayerResistanceDamage { get; }
        public int EnemyResistanceDamage { get; }
        public int PlayerPushPower { get; }
        public int EnemyPushPower { get; }
        public int PlayerDisplayedDamage { get; }
        public int EnemyDisplayedDamage { get; }
        public LegacyDamageReason PlayerDamageReasons { get; }
        public LegacyDamageReason EnemyDamageReasons { get; }
        public bool PlayerAttackConditionMet { get; }
        public bool EnemyAttackConditionMet { get; }
        public bool IsFinalHit { get; }
        public DuelMatchOutcome Outcome { get; }
    }
}
