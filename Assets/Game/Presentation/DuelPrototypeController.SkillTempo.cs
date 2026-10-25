using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPrototypeController
    {
        private float tempoWindupDuration;

        /// <summary>The fast technique's scenery/opponent clock scale. Its own pose and authoritative contacts
        /// use the ordinary slot clock. Stronger step, impact and finale slow motion still affects both sides.</summary>
        public float FastSkillPresentationSpeed => DuelFastSkillSlowMotion.Speed(session?.CurrentSlot,
            viewPhase == ViewPhase.PlayingSlot);

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

    internal static class DuelFastSkillSlowMotion
    {
        public const float Scale = .4f;

        public static float Speed(LegacyCurrentSlot slot, bool playing)
            => playing && slot != null &&
                ((slot.PlayerActionSkill ?? slot.PlayerSkill)?.IsFast == true ||
                 (slot.EnemyActionSkill ?? slot.EnemySkill)?.IsFast == true) ? Scale : 1f;

        public static float Limit(float combatSpeed, LegacyCurrentSlot slot, bool playing)
        {
            float scale = Speed(slot, playing);
            return scale < 1f ? System.Math.Min(combatSpeed, scale) : combatSpeed;
        }

        public static void SplitDelta(float liveTime, float slotSpeed, LegacyCurrentSlot slot, bool playing,
            out float worldDelta, out float actionDelta)
        {
            actionDelta = liveTime * slotSpeed;
            worldDelta = liveTime * Limit(slotSpeed, slot, playing);
        }

        private static LegacySkill Action(LegacyCurrentSlot slot, bool player)
            => slot == null ? null : player ? slot.PlayerActionSkill ?? slot.PlayerSkill
                : slot.EnemyActionSkill ?? slot.EnemySkill;

        public static bool IsFast(LegacyCurrentSlot slot, bool player) => Action(slot, player)?.IsFast == true;

        private static bool SlowCompletion(LegacyCurrentSlot slot, bool player)
        {
            LegacySkill action = Action(slot, player);
            return action != null && !action.IsWait && !action.IsFast && IsFast(slot, !player) &&
                (player ? slot.PlayerActionCompletes : slot.EnemyActionCompletes);
        }

        public static bool HasSlowCompletion(LegacyCurrentSlot slot)
            => SlowCompletion(slot, true) || SlowCompletion(slot, false);

        private static int Hits(LegacySkill skill) => System.Math.Max(1, skill?.AttackCount ?? 1);

        // Contacts shared with the fast action retain its original timing. After the final shared contact the
        // opponent's recovery and any remaining strikes can slow without delaying a fast strike or skipping a cel.
        private static float Anchor(LegacyCurrentSlot slot, bool player, float clip, float gap)
            => clip * .5f + (System.Math.Min(Hits(Action(slot, player)), Hits(Action(slot, !player))) - 1) * (clip + gap);

        private static float ToSlowTime(float elapsed, float anchor)
            => elapsed <= anchor ? elapsed : anchor + (elapsed - anchor) * Scale;

        private static float FromSlowTime(float elapsed, float anchor)
            => elapsed <= anchor ? elapsed : anchor + (elapsed - anchor) / Scale;

        public static float ActionElapsed(LegacyCurrentSlot slot, bool player, float elapsed, float clip, float gap)
            => SlowCompletion(slot, player) ? ToSlowTime(elapsed, Anchor(slot, player, clip, gap)) : elapsed;

        public static float HitTime(LegacyCurrentSlot slot, int hitIndex, float clip, float gap)
        {
            float contact = clip * .5f + hitIndex * (clip + gap);
            for (int side = 0; side < 2; side++)
            {
                bool player = side == 0;
                if (SlowCompletion(slot, player) && hitIndex >= Hits(Action(slot, !player)))
                    return FromSlowTime(contact, Anchor(slot, player, clip, gap));
            }
            return contact;
        }

        public static float ClipEnd(LegacyCurrentSlot slot, int hitIndex, float clip, float gap)
        {
            float end = clip * (hitIndex + 1) + hitIndex * gap;
            for (int side = 0; side < 2; side++)
            {
                bool player = side == 0;
                if (SlowCompletion(slot, player) && hitIndex < Hits(Action(slot, player)))
                    end = System.Math.Max(end, FromSlowTime(clip * (hitIndex + 1) + hitIndex * gap,
                        Anchor(slot, player, clip, gap)));
            }
            return end;
        }

        public static float ContactClipEnd(LegacyCurrentSlot slot, int hitIndex, float clip, float gap)
        {
            float end = float.PositiveInfinity;
            for (int side = 0; side < 2; side++)
            {
                bool player = side == 0;
                LegacySkill skill = player ? slot?.PlayerSkill : slot?.EnemySkill;
                if (skill?.Kind != LegacySkillKind.Attack || hitIndex >= skill.AttackCount) continue;
                float ownEnd = clip * (hitIndex + 1) + hitIndex * gap;
                if (SlowCompletion(slot, player)) ownEnd = FromSlowTime(ownEnd, Anchor(slot, player, clip, gap));
                end = System.Math.Min(end, ownEnd);
            }
            return float.IsPositiveInfinity(end) ? ClipEnd(slot, hitIndex, clip, gap) : end;
        }
    }
}
