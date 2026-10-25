using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public sealed partial class LegacyQueuedDuel
    {
        private bool[] playerDestroyedSkills = Array.Empty<bool>(), enemyDestroyedSkills = Array.Empty<bool>();
        public bool UsePowerBreak { get; }

        public bool IsPlayerSkillDestroyed(int queueIndex) => IsSkillDestroyed(true, queueIndex);
        public bool IsEnemySkillDestroyed(int queueIndex) => IsSkillDestroyed(false, queueIndex);

        private bool IsSkillDestroyed(bool player, int queueIndex)
        {
            bool[] destroyed = player ? playerDestroyedSkills : enemyDestroyedSkills;
            return UsePowerBreak && queueIndex >= 0 && queueIndex < destroyed.Length && destroyed[queueIndex];
        }

        // The immutable time map survives removing the executable skill. A half-cell of padding is not a broken gap.
        private LegacyTimedSkill OriginalActionAt(bool player, int segmentIndex)
            => segmentIndex < 0 || segmentIndex >= timeSegments.Length ? null
                : player ? timeSegments[segmentIndex].Player : timeSegments[segmentIndex].Enemy;

        private bool IsBrokenGapAt(bool player, int segmentIndex)
        {
            int queueIndex = OriginalActionAt(player, segmentIndex)?.QueueIndex ?? -1;
            if (!IsSkillDestroyed(player, queueIndex)) return false;
            if (player) return !Player.IsDefeated;
            int owner = EnemyOwnerAtSlot(queueIndex);
            return owner >= 0 && !enemyStates[owner].IsDefeated;
        }

        private void PreparePowerBreakGap(int segmentIndex)
        {
            if (!UsePowerBreak) return;
            Player.SetBrokenGap(IsBrokenGapAt(true, segmentIndex));
            int originalEnemy = EnemyOwnerAtSlot(OriginalActionAt(false, segmentIndex)?.QueueIndex ?? -1);
            Enemy.SetBrokenGap(originalEnemy == activeEnemyIndex && IsBrokenGapAt(false, segmentIndex));
        }

        private void SetPowerBreakTiming(LegacyCurrentSlot slot)
        {
            if (!UsePowerBreak) return;
            slot.PlayerBrokenGap = Player.IsBrokenGap;
            slot.EnemyBrokenGap = Enemy.IsBrokenGap;
            if (slot.PlayerBrokenGap) slot.PlayerQueueIndex = OriginalActionAt(true, slot.SlotIndex).QueueIndex;
            if (slot.EnemyBrokenGap) slot.EnemyQueueIndex = OriginalActionAt(false, slot.SlotIndex).QueueIndex;
        }

        private void ResolvePowerBreak(LegacyCurrentSlot slot)
        {
            // A technique compares once, using all its executable hits. A dodge never counts as meeting blades.
            if (!IsAttack(slot.PlayerSkill) || !IsAttack(slot.EnemySkill) || slot.DodgeSucceeded) return;
            if (LegacyPowerBreakRules.Overwhelms(slot.PlayerTotalPower, slot.EnemyTotalPower))
            {
                slot.EnemyBroken = true;
                slot.EnemyDestroyedQueueIndex = DestroyNextPowerBreakSkill(false, slot.SlotIndex);
            }
            else if (LegacyPowerBreakRules.Overwhelms(slot.EnemyTotalPower, slot.PlayerTotalPower))
            {
                slot.PlayerBroken = true;
                slot.PlayerDestroyedQueueIndex = DestroyNextPowerBreakSkill(true, slot.SlotIndex);
            }
        }

        private int DestroyNextPowerBreakSkill(bool player, int segmentIndex)
        {
            int after = BreakQueueIndex(player, segmentIndex);
            LegacySkill[] execution = player ? committedPlayerQueue : committedEnemyQueue;
            for (int index = after + 1; index < execution.Length; index++)
            {
                // A shared queue interleaves several fighters. A broken enemy cannot destroy an ally's reservation.
                if (!player && EnemyOwnerAtSlot(index) != activeEnemyIndex) continue;
                if (IsSkillDestroyed(player, index)) return -1;
                if (execution[index] == null) return -1;
                (player ? playerDestroyedSkills : enemyDestroyedSkills)[index] = true;
                execution[index] = null;
                if (player && PlayerBreakCanceledFromSlotIndex < 0) PlayerBreakCanceledFromSlotIndex = index;
                if (!player && EnemyBreakCanceledFromSlotIndex < 0) EnemyBreakCanceledFromSlotIndex = index;
                return index;
            }
            return -1;
        }
    }
}
