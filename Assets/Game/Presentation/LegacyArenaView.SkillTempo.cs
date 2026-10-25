using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private LegacyCurrentSlot tempoSlot;
        private float playerTempoElapsed, enemyTempoElapsed, tempoLeadIn;

        /// <summary>The timeline keeps an unfinished swing alive while the opponent starts another technique.</summary>
        internal void BeginTempoSlot(LegacyCurrentSlot slot, LegacySkill playerCounter, float leadIn)
        {
            bool timed = UsesTempoPresentation(slot);
            Dictionary<int, int> playerVariants = timed && !slot.PlayerActionStarts
                ? new Dictionary<int, int>(player.AttackVariants) : null;
            Dictionary<int, int> enemyVariants = timed && !slot.EnemyActionStarts
                ? new Dictionary<int, int>(enemy.AttackVariants) : null;
            int playerGuard = player.GuardVariant, enemyGuard = enemy.GuardVariant;
            BeginSlot(slot.PlayerActionSkill ?? playerCounter, slot.EnemyActionSkill ?? slot.EnemySkill);
            if (!timed) return;
            tempoSlot = slot;
            tempoLeadIn = Mathf.Max(0f, leadIn);
            if (playerVariants != null)
            {
                player.AttackVariants.Clear();
                foreach (var pair in playerVariants) player.AttackVariants.Add(pair.Key, pair.Value);
                player.GuardVariant = playerGuard;
            }
            if (enemyVariants != null)
            {
                enemy.AttackVariants.Clear();
                foreach (var pair in enemyVariants) enemy.AttackVariants.Add(pair.Key, pair.Value);
                enemy.GuardVariant = enemyGuard;
            }
            HoldSlotAtTime(0f);
        }

        internal static bool UsesTempoPresentation(LegacyCurrentSlot slot) => slot != null &&
            (slot.DurationHalfTicks != 2 ||
             slot.PlayerActionSkill != null && (slot.PlayerActionDurationHalfTicks != 2 || !slot.PlayerActionCompletes || !slot.PlayerActionStarts) ||
             slot.EnemyActionSkill != null && (slot.EnemyActionDurationHalfTicks != 2 || !slot.EnemyActionCompletes || !slot.EnemyActionStarts));

        private void ClearTempoSlot()
        {
            tempoSlot = null;
            playerTempoElapsed = enemyTempoElapsed = tempoLeadIn = 0f;
        }

        private float AdvanceTempoPose(Actor actor, float delta)
        {
            if (tempoSlot == null || actor != player && actor != enemy) return actor.AnimationTime + delta;
            if (actor == player)
            {
                playerTempoElapsed += Mathf.Max(0f, delta);
                return TempoPoseTime(true, playerTempoElapsed);
            }
            enemyTempoElapsed += Mathf.Max(0f, delta);
            return TempoPoseTime(false, enemyTempoElapsed);
        }

        private float ActorMotionDelta(Actor actor, float worldDelta, float actionDelta)
            => tempoSlot != null && (actor == player || actor == enemy) &&
                DuelFastSkillSlowMotion.IsFast(tempoSlot, actor == player) ? actionDelta : worldDelta;

        private float TempoPoseTime(bool playerSide, float elapsed)
        {
            LegacySkill action = playerSide ? tempoSlot.PlayerActionSkill : tempoSlot.EnemyActionSkill;
            if (action == null) return Mathf.Max(0f, elapsed - tempoLeadIn);
            bool completes = playerSide ? tempoSlot.PlayerActionCompletes : tempoSlot.EnemyActionCompletes;
            int start = playerSide ? tempoSlot.PlayerActionStartHalfTick : tempoSlot.EnemyActionStartHalfTick;
            int duration = playerSide ? tempoSlot.PlayerActionDurationHalfTicks : tempoSlot.EnemyActionDurationHalfTicks;
            float clip = OriginalClipDuration / slotAnimationSpeed;
            if (elapsed > tempoLeadIn)
                elapsed = tempoLeadIn + DuelFastSkillSlowMotion.ActionElapsed(tempoSlot, playerSide,
                    elapsed - tempoLeadIn, clip, slotAttackInterval);
            return DuelSkillTempoPose.Sample(clip, elapsed, tempoLeadIn, tempoSlot.StartHalfTick - start,
                tempoSlot.DurationHalfTicks, duration, completes);
        }

        private bool TempoKeepsSwinging(Actor actor)
        {
            if (tempoSlot == null || actor.Skill?.Kind != LegacySkillKind.Attack) return false;
            return actor == player
                ? tempoSlot.PlayerActionDurationHalfTicks > tempoSlot.DurationHalfTicks || !tempoSlot.PlayerActionStarts
                : tempoSlot.EnemyActionDurationHalfTicks > tempoSlot.DurationHalfTicks || !tempoSlot.EnemyActionStarts;
        }
    }

    internal static class DuelSkillTempoPose
    {
        // Pending intervals only advance the windup. The completing interval reaches the contact cel at its
        // authoritative first impact and then plays the remaining strike cycles without restarting the action.
        public static float Sample(float clip, float elapsed, float leadIn,
            int elapsedHalfTicks, int segmentHalfTicks, int actionHalfTicks, bool completes)
        {
            clip = System.Math.Max(.00001f, clip);
            elapsed = System.Math.Max(0f, elapsed);
            int duration = System.Math.Max(1, actionHalfTicks);
            float startingPhase = .4f * Clamp01((float)elapsedHalfTicks / duration);
            if (!completes)
            {
                float segmentProgress = Clamp01(elapsed / clip);
                float actionProgress = (elapsedHalfTicks + segmentProgress * segmentHalfTicks) / duration;
                return clip * .4f * Clamp01(actionProgress);
            }
            if (leadIn > 0f && elapsed < leadIn)
                return clip * Lerp(startingPhase, .2f, elapsed / leadIn);
            float strikeElapsed = System.Math.Max(0f, elapsed - leadIn);
            float firstContact = clip * .5f;
            if (strikeElapsed < firstContact)
                return Lerp(clip * (leadIn > 0f ? .2f : startingPhase), firstContact, strikeElapsed / firstContact);
            return strikeElapsed;
        }

        private static float Clamp01(float value) => System.Math.Max(0f, System.Math.Min(1f, value));
        private static float Lerp(float from, float to, float progress) => from + (to - from) * Clamp01(progress);
    }
}
