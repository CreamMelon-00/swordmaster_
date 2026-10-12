using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>What the duel HUD (<c>LegacyCombatHud</c>) reads from a duel, seen from the player's side: the player's
    /// desk (lanes, ACT, 숨고르기, 순환), both queues, both fighters, and the rules only some duels have (반격, 맞물림, the
    /// cancel boost). <see cref="LegacyQueuedDuel"/> is one as it stands; <see cref="LocalVersusPerspective"/> shows a
    /// versus match from one player's side, so the same HUD serves both. Reading never changes the duel.</summary>
    public interface ILegacyDuelView
    {
        /// <summary>What the duel allows: closed lanes are not drawn, closed 숨고르기 and 넘기기 hide their buttons.</summary>
        CombatFeature Features { get; }
        LegacyDuelPhase Phase { get; }
        int RoundNumber { get; }
        /// <summary>The player's ACT now and its cap.</summary>
        int Act { get; }
        int PlayerMaximumAct { get; }
        int BreathsRemainingThisTurn { get; }
        LegacyFighterState Player { get; }
        LegacyFighterState Enemy { get; }
        /// <summary>The player's reservations: live while planning, the committed queue while it resolves.</summary>
        IReadOnlyList<LegacySkill> PlayerQueue { get; }
        IReadOnlyList<LegacySkill> EnemyQueue { get; }
        /// <summary>The slot resolving now, or null. The same object for as long as that slot plays.</summary>
        ILegacySlotView CurrentSlot { get; }
        /// <summary>One of the player's lanes, its front skill first; empty for a lane the duel does not have.</summary>
        IReadOnlyList<LegacySkill> GetLane(int laneIndex);
        /// <summary>ACT the player pays if this skill is reserved now.</summary>
        int EffectiveCost(LegacySkill skill);
        /// <summary>The player's power range for the skill's next use, before buffs and hit division.</summary>
        void EffectivePowerRange(LegacySkill skill, out int min, out int max);
        /// <summary>The player's uses of a cycling skill so far.</summary>
        int CycleUses(int skillId);
        /// <summary>The enemy's side of the same, for its skills' explanation (Tab): its uses of a cycling skill so far, the
        /// ACT its next use costs and that use's power range before buffs and hit division. A duel whose enemy never cycles
        /// reads 0 and the skill's own cost and range.</summary>
        int EnemyCycleUses(int skillId);
        int EnemyEffectiveCost(LegacySkill skill);
        void EnemyEffectivePowerRange(LegacySkill skill, out int min, out int max);
        /// <summary>Each side's 반격 and its uses left this turn; null and 0 where the duel has none.</summary>
        LegacyCounter PlayerCounter { get; }
        LegacyCounter EnemyCounter { get; }
        int PlayerCountersRemaining { get; }
        int EnemyCountersRemaining { get; }
        /// <summary>The player's queued attacks the enemy's counter will answer; clears <paramref name="slots"/> first.</summary>
        void ForecastEnemyCounterSlots(List<int> slots);
        /// <summary>The enemy's queued attacks the player's counter will answer; clears <paramref name="slots"/> first.</summary>
        void ForecastPlayerCounterSlots(List<int> slots);
        /// <summary>The enemy counter that would meet <paramref name="skill"/> if the player queued it next, or null.</summary>
        LegacySkill EnemyCounterFacing(LegacySkill skill);
        /// <summary>맞물림's percent per chained skill; 0 where the duel does not mesh.</summary>
        int MeshPercent { get; }
        LegacyMeshSlot PlayerMesh(int slotIndex);
        LegacyMeshSlot PlayerMeshIfQueued(int laneIndex);
        /// <summary>The player's cancel boost: the technique whose removal armed it (or -1), the skills it removed, the
        /// technique it boosts (or -1), and that slot's multiplier (1 for any other).</summary>
        int QueuePowerSourceSlotIndex { get; }
        int QueuePowerRemovedSkillCount { get; }
        int QueuePowerTargetSlotIndex { get; }
        int QueuePowerMultiplierForSlot(int slotIndex);
        /// <summary>편성 예상: this planning turn's slots as both queues would resolve now, from the player's side, into
        /// <paramref name="slots"/> (cleared first; empty outside planning). The duel's own rules resolve a copy of it with
        /// average power rolls and no steps (<see cref="LegacySlotForecast"/>); the duel itself never changes.</summary>
        void ForecastTurn(List<LegacySlotForecast> slots);
        /// <summary>The slot the front skill of <paramref name="laneIndex"/> would take if the player queued it next, forecast
        /// with the whole turn; null when it cannot be queued (or outside planning).</summary>
        LegacySlotForecast ForecastIfQueued(int laneIndex);
    }

    /// <summary>A resolving slot as the duel HUD reads it, seen from the player's side: both skills and the feedback of
    /// their initial effects and buffs (<see cref="LegacySkillFeedback"/>).</summary>
    public interface ILegacySlotView
    {
        int SlotIndex { get; }
        LegacySkill PlayerSkill { get; }
        LegacySkill EnemySkill { get; }
        LegacySkillFeedback PlayerFeedback { get; }
        LegacySkillFeedback EnemyFeedback { get; }
    }
}
