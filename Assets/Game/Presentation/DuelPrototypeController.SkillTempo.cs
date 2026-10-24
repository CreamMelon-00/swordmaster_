using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPrototypeController
    {
        private float tempoWindupDuration;

        private void ConfigureTempoPlayback(LegacyCurrentSlot slot)
        {
            tempoWindupDuration = 0f;
            if (!LegacyArenaView.UsesTempoPresentation(slot)) return;
            int shortest = 2;
            if (slot.PlayerActionSkill != null) shortest = Mathf.Min(shortest, slot.PlayerActionDurationHalfTicks);
            if (slot.EnemyActionSkill != null) shortest = Mathf.Min(shortest, slot.EnemyActionDurationHalfTicks);
            // Fast techniques retain their normal clip speed; the longer opposing action carries its windup
            // across both clips. Two normal techniques against a charge retain their existing playback speed.
            float lengthScale = (float)Mathf.Max(1, slot.DurationHalfTicks) / Mathf.Max(1, shortest);
            // An uninterrupted charge gets a distinct gathering interval before its ordinary strike clip.
            if (slot.DurationHalfTicks > 2)
            {
                tempoWindupDuration = OriginalClipDuration / slotPlaybackSpeed * (lengthScale - 1f);
                lengthScale = 1f;
            }
            slotPlaybackSpeed /= lengthScale;
            slotAttackInterval *= lengthScale;
        }

        private LegacySkill PlayerSkillMeetingEnemy(int enemyQueueIndex)
        {
            if (!session.HasTempoSkills)
                return enemyQueueIndex < session.PlayerQueue.Count ? session.PlayerQueue[enemyQueueIndex] : null;
            var enemy = LegacySkillTiming.Build(session.EnemyQueue);
            var player = LegacySkillTiming.Build(session.PlayerQueue);
            int completion = -1;
            for (int i = 0; i < enemy.Count; i++)
                if (enemy[i].QueueIndex == enemyQueueIndex) { completion = enemy[i].EndHalfTick; break; }
            for (int i = 0; i < player.Count; i++)
                if (player[i].EndHalfTick == completion) return player[i].Skill;
            return null;
        }
    }
}
