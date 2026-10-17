using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private const float SuccessfulSlowStepTravel = 1.45f;
        private const float SlowPlayerLungeExtra = .7f;
        private float slowMovementAmount;

        /// <summary>The player's extra commitment during a cinematic slow motion. Scenes hide and freeze it.</summary>
        public float SlowMotionMovementAmount => suspension == null ? slowMovementAmount : 0f;
        private bool IsCinematicSlowMotion => suspension == null && resolving && !approaching && !returning &&
            (StepPresentationSpeed < .999f || IsFatalFocus);

        private void TickSlowMovement(float realDelta)
        {
            if (suspension != null || realDelta <= 0f || IsBladeBinding) return;
            bool active = IsCinematicSlowMotion;
            float target = active ? 1f : 0f;
            float rate = active ? 20f : 14f;
            slowMovementAmount = Mathf.Lerp(slowMovementAmount, target, 1f - Mathf.Exp(-rate * realDelta));
            if (!active && slowMovementAmount < .0001f) slowMovementAmount = 0f;
        }

        // Reuse the same distance for the body, sword's contact trail and culling bounds. Pose re-sampling only reads it.
        private float AttackLungeDistanceFor(Actor actor) => slotAttackLungeDistance *
            (actor == player ? 1f + SlowPlayerLungeExtra * slowMovementAmount : 1f);

        private void ClearSlowMovement() => slowMovementAmount = 0f;
    }
}
