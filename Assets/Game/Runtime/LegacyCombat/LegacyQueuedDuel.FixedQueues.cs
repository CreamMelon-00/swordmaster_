using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public sealed partial class LegacyQueuedDuel
    {
        /// <summary>Replaces a planning turn with an already arranged, free queue on each side. No reservation,
        /// lane rotation, insertion or removal effects run here. Commit and the usual slot/hit rules still apply.</summary>
        public bool TrySetFixedQueues(IReadOnlyList<LegacySkill> player, IReadOnlyList<LegacySkill> enemy)
        {
            if (Phase != LegacyDuelPhase.Planning || enemyStates.Count != 1 ||
                player == null || player.Count == 0 || enemy == null || enemy.Count == 0) return false;
            LegacySkill[] playerCopy = CopyFixedQueue(player);
            LegacySkill[] enemyCopy = CopyFixedQueue(enemy);
            if (playerCopy == null || enemyCopy == null) return false;

            playerQueue.Clear();
            playerQueue.AddRange(playerCopy);
            playerQueueActCosts.Clear();
            for (int index = 0; index < playerCopy.Length; index++) playerQueueActCosts.Add(0);
            enemyQueue.Clear();
            enemyQueue.AddRange(enemyCopy);
            enemyQueueOwners.Clear();
            for (int index = 0; index < enemyCopy.Length; index++) enemyQueueOwners.Add(0);
            queuePowerSourceSlotIndex = -1;
            queuePowerRemovedSkillCount = queuePowerMultiplierCap = 0;
            BreathsQueuedThisTurn = 0;
            ReserveEffectsUsedThisTurn = LegacyReserveEffect.None;
            AutoBreathSlotIndex = LastQueuedSlotIndex = -1;
            return true;
        }

        private static LegacySkill[] CopyFixedQueue(IReadOnlyList<LegacySkill> source)
        {
            var copy = new LegacySkill[source.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                LegacySkill skill = source[index];
                if (skill == null) return null;
                copy[index] = new LegacySkill(skill.Id, skill.Name, skill.Cost, skill.MinPower, skill.MaxPower,
                    skill.Kind, skill.Property, skill.AttackCount, skill.LaneIndex, skill.Description,
                    skill.AnimationName, skill.IconId);
            }
            return copy;
        }
    }
}
