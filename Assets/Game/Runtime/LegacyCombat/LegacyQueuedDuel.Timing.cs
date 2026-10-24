using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public sealed partial class LegacyQueuedDuel
    {
        private LegacyTimeSegment[] timeSegments = Array.Empty<LegacyTimeSegment>();
        private bool tempoExecution;
        public bool HasTempoSkills
        {
            get
            {
                if (Phase != LegacyDuelPhase.Planning) return tempoExecution;
                var player = PlayerQueue;
                var enemy = EnemyQueue;
                if (player != null) for (int i = 0; i < player.Count; i++)
                    if (player[i] != null && player[i].DurationHalfUnits != 2) return true;
                if (enemy != null) for (int i = 0; i < enemy.Count; i++)
                    if (enemy[i] != null && enemy[i].DurationHalfUnits != 2) return true;
                return false;
            }
        }

        /// <summary>The action in motion during an execution segment, including an action not yet striking.</summary>
        public LegacySkill ActionSkillAtSegment(bool player, int segmentIndex) => ActionAt(player, segmentIndex)?.Skill;
        /// <summary>Stable even between impacts, when CurrentSlot has been released. Past the final segment returns
        /// the end of the timeline, so HUD progress never jumps back to zero.</summary>
        public int StartHalfTickAtSegment(int segmentIndex)
        {
            if (Phase == LegacyDuelPhase.Planning || segmentIndex < 0 || timeSegments.Length == 0) return 0;
            return segmentIndex < timeSegments.Length ? timeSegments[segmentIndex].Start : timeSegments[timeSegments.Length - 1].End;
        }
        public int QueueIndexAtSegment(bool player, int segmentIndex)
        {
            if (Phase == LegacyDuelPhase.Planning)
            {
                var segments = LegacySkillTiming.Segments(LegacySkillTiming.Build(PlayerQueue), LegacySkillTiming.Build(EnemyQueue));
                if (segmentIndex < 0 || segmentIndex >= segments.Length) return -1;
                return (player ? segments[segmentIndex].Player : segments[segmentIndex].Enemy)?.QueueIndex ?? -1;
            }
            return ActionAt(player, segmentIndex)?.QueueIndex ?? -1;
        }

        private LegacySkill TimedEnemyCounterFacing(LegacySkill skill)
        {
            if (Phase != LegacyDuelPhase.Planning || !IsAttack(skill)) return null;
            var copy = new LegacyQueuedDuel(this);
            int candidateIndex = copy.playerQueue.Count;
            copy.playerQueue.Add(skill);
            copy.playerQueueActCosts.Add(0);
            copy.Commit();
            while (copy.Phase == LegacyDuelPhase.Resolving && !copy.IsTurnResolved)
            {
                LegacyCurrentSlot slot = copy.BeginNextSlot();
                if (slot.PlayerQueueIndex == candidateIndex && slot.PlayerActionCompletes)
                    return slot.EnemyCountered ? slot.EnemySkill : null;
                while (!slot.IsResolved) copy.ResolveNextHit();
                copy.CompleteCurrentSlot();
            }
            return null;
        }

        private bool ForecastTimedCounterSlots(bool player, List<int> slots)
        {
            if (!HasTempoSkills) return false;
            if (Phase == LegacyDuelPhase.Planning)
            {
                var copy = new LegacyQueuedDuel(this);
                copy.Commit();
                while (copy.Phase == LegacyDuelPhase.Resolving && !copy.IsTurnResolved)
                {
                    LegacyCurrentSlot slot = copy.BeginNextSlot();
                    while (!slot.IsResolved) copy.ResolveNextHit();
                    if (player ? slot.PlayerCountered : slot.EnemyCountered) slots.Add(slot.SlotIndex);
                    copy.CompleteCurrentSlot();
                }
                return true;
            }
            int uses = player ? PlayerCountersRemaining : EnemyCountersRemaining;
            int[] ownerUses = !player && enemyDefinitions != null ? (int[])enemyCountersByOwner.Clone() : null;
            bool canceled = (player ? PlayerBreakCanceledFromSlotIndex : EnemyBreakCanceledFromSlotIndex) >= 0;
            int first = nextSlot;
            if (CurrentSlot != null)
            {
                first++;
                bool pending = player && CurrentSlot.PendingPlayerCounter != null;
                if (pending || (player ? CurrentSlot.PlayerCountered : CurrentSlot.EnemyCountered)) slots.Add(nextSlot);
                if (pending) uses--;
            }
            for (int i = first, found = 0; !canceled && i < timeSegments.Length && (ownerUses != null || found < uses); i++)
                if (ActionAt(player, i) == null && IsAttack(CompletingAt(!player, i)))
                {
                    if (ownerUses != null)
                    {
                        int owner = EnemyIndexForSegment(i);
                        if (enemyStates[owner].IsDefeated || enemyBrokenFrom[owner] >= 0 || ownerUses[owner] <= 0) continue;
                        ownerUses[owner]--;
                    }
                    slots.Add(i); found++;
                }
            return true;
        }

        private int EnemyIndexForSegment(int segmentIndex)
        {
            for (int i = segmentIndex; i >= 0; i--)
            {
                LegacyTimedSkill action = ActionAt(false, i);
                if (action != null) return EnemyIndexForSlot(action.QueueIndex);
            }
            return EnemyIndexForSlot(-1);
        }

        private int TimedIiaMirrorPreview(int enemyQueueIndex)
        {
            if (CurrentSlot != null && CurrentSlot.EnemyQueueIndex == enemyQueueIndex && CurrentSlot.EnemyActionCompletes)
            {
                LegacySkill skill = CurrentSlot.PlayerSkill ?? CurrentSlot.PendingPlayerCounter;
                return IiaMirrorMaximum(skill, CurrentSlot.PendingPlayerCounter != null
                    ? CycleUsesRead(skill) : CurrentSlot.PlayerCycleUses);
            }
            var players = LegacySkillTiming.Build(PlayerQueue);
            var enemies = LegacySkillTiming.Build(EnemyQueue);
            if (enemyQueueIndex < 0 || enemyQueueIndex >= enemies.Count) return 0;
            int end = enemies[enemyQueueIndex].EndHalfTick;
            bool occupied = false;
            for (int i = 0; i < players.Count; i++)
            {
                if (Phase != LegacyDuelPhase.Planning && committedPlayerQueue[i] == null) continue;
                if (players[i].EndHalfTick == end)
                    return IiaMirrorMaximum(players[i].Skill, CycleUsesAtSlot(i));
                if (players[i].StartHalfTick < end && players[i].EndHalfTick > end) occupied = true;
            }
            if (occupied || PlayerCounter == null) return 0;
            var segments = LegacySkillTiming.Segments(players, enemies);
            int segmentIndex = -1;
            for (int i = 0; i < segments.Length; i++) if (segments[i].End == end) { segmentIndex = i; break; }
            if (iiaCounterPreviewSlots == null) iiaCounterPreviewSlots = new List<int>();
            ForecastPlayerCounterSlots(iiaCounterPreviewSlots);
            return iiaCounterPreviewSlots.Contains(segmentIndex)
                ? IiaMirrorMaximum(PlayerCounter.Skill, CycleUsesRead(PlayerCounter.Skill)) : 0;
        }

        private void BuildExecutionTiming()
        {
            tempoExecution = HasTempoSkills;
            timeSegments = LegacySkillTiming.Segments(LegacySkillTiming.Build(committedPlayerQueue),
                LegacySkillTiming.Build(committedEnemyQueue));
        }

        private LegacyTimedSkill ActionAt(bool player, int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= timeSegments.Length) return null;
            LegacyTimedSkill action = player ? timeSegments[segmentIndex].Player : timeSegments[segmentIndex].Enemy;
            LegacySkill[] queue = player ? committedPlayerQueue : committedEnemyQueue;
            return action != null && queue != null && action.QueueIndex < queue.Length && queue[action.QueueIndex] != null ? action : null;
        }

        private LegacySkill CompletingAt(bool player, int segmentIndex)
        {
            LegacyTimedSkill action = ActionAt(player, segmentIndex);
            return action != null && action.EndHalfTick == timeSegments[segmentIndex].End ? action.Skill : null;
        }

        // A break cancels reservations after the action already in motion. During padding it cancels the next action.
        private int BreakQueueIndex(bool player, int segmentIndex)
        {
            if (!tempoExecution) return segmentIndex;
            LegacyTimedSkill action = ActionAt(player, segmentIndex);
            if (action != null) return action.QueueIndex;
            LegacySkill[] queue = player ? committedPlayerQueue : committedEnemyQueue;
            int end = segmentIndex >= 0 && segmentIndex < timeSegments.Length ? timeSegments[segmentIndex].End : 0;
            var entries = LegacySkillTiming.Build(player ? PlayerQueue : EnemyQueue);
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].StartHalfTick >= end) return i - 1;
            return queue.Length - 1;
        }

        private void SetCurrentTiming(LegacyCurrentSlot slot)
        {
            LegacyTimeSegment segment = timeSegments[slot.SlotIndex];
            LegacyTimedSkill player = ActionAt(true, slot.SlotIndex), enemy = ActionAt(false, slot.SlotIndex);
            slot.StartHalfTick = segment.Start; slot.EndHalfTick = segment.End;
            slot.PlayerQueueIndex = player?.QueueIndex ?? -1;
            slot.EnemyQueueIndex = enemy?.QueueIndex ?? -1;
            slot.PlayerActionSkill = player?.Skill; slot.EnemyActionSkill = enemy?.Skill;
            slot.PlayerActionStartHalfTick = player?.StartHalfTick ?? segment.Start;
            slot.EnemyActionStartHalfTick = enemy?.StartHalfTick ?? segment.Start;
            slot.PlayerActionDurationHalfTicks = player?.DurationHalfTicks ?? 0;
            slot.EnemyActionDurationHalfTicks = enemy?.DurationHalfTicks ?? 0;
            slot.PlayerActionStarts = player != null && player.StartHalfTick == segment.Start;
            slot.EnemyActionStarts = enemy != null && enemy.StartHalfTick == segment.Start;
            slot.PlayerActionCompletes = player != null && player.EndHalfTick == segment.End;
            slot.EnemyActionCompletes = enemy != null && enemy.EndHalfTick == segment.End;
            if (player == null && slot.PlayerSkill != null)
            {
                slot.PlayerActionSkill = slot.PlayerSkill;
                slot.PlayerActionDurationHalfTicks = segment.End - segment.Start;
                slot.PlayerActionStarts = slot.PlayerActionCompletes = true;
            }
            if (enemy == null && slot.EnemySkill != null)
            {
                slot.EnemyActionSkill = slot.EnemySkill;
                slot.EnemyActionDurationHalfTicks = segment.End - segment.Start;
                slot.EnemyActionStarts = slot.EnemyActionCompletes = true;
            }
        }

        private void SetForecastTiming(LegacySlotForecast forecast, int segmentIndex)
        {
            LegacyTimeSegment segment = timeSegments[segmentIndex];
            LegacyTimedSkill player = segment.Player, enemy = segment.Enemy;
            forecast.StartHalfTick = segment.Start; forecast.EndHalfTick = segment.End;
            forecast.PlayerQueueIndex = player?.QueueIndex ?? -1;
            forecast.EnemyQueueIndex = enemy?.QueueIndex ?? -1;
            forecast.PlayerActionSkill = player?.Skill; forecast.EnemyActionSkill = enemy?.Skill;
        }
    }
}
