using System;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private const float MaximumGroundPlantTravel = .18f;
        private float groundDepthSpeed;
        private int depthLocalPlayer;
        private bool depthMovementActive, depthPlanningRequested, depthWasVisibleForCutscene;

        /// <summary>Only X is mirrored in versus. Canonical fighters keep their floor depth on both screens.</summary>
        public void SetDepthPerspective(int localPlayer)
        {
            if (localPlayer < 0 || localPlayer > 1) throw new ArgumentOutOfRangeException(nameof(localPlayer));
            depthLocalPlayer = localPlayer;
        }

        private void ResetDepthMovement()
        {
            depthLocalPlayer = 0;
            nextDodgeDepthSide = 1f;
            stepDodgeDepthSide = 0f;
            stepDodgeSideDistance = GroundDodgeSideDistance;
            depthMovementActive = depthPlanningRequested = depthWasVisibleForCutscene = false;
            groundDepthSpeed = settings.CombatDepthTravelSpeed * settings.MovementSpeedMultiplier;
            player.GroundVelocity = enemy.GroundVelocity = Vector2.zero;
            player.GroundTravelRequested = enemy.GroundTravelRequested = false;
            SetDepthPresentationVisible(false);
            RefreshDepthPresentation();
        }

        private void RequestPlanningDepth()
        {
            if (suspension != null) return;
            depthPlanningRequested = true;
            SetDepthPresentationVisible(true);
            if (!depthMovementActive)
                groundDepthSpeed = settings.CombatDepthTravelSpeed * settings.MovementSpeedMultiplier;
            depthMovementActive = true;
        }

        private void BeginDepthApproach()
        {
            groundDepthSpeed = settings.CombatDepthTravelSpeed * settings.MovementSpeedMultiplier;
            SetDepthPresentationVisible(true);
            depthMovementActive = true;
        }

        private void BeginDepthSlot()
        {
            groundDepthSpeed = settings.CombatDepthTravelSpeed * settings.MovementSpeedMultiplier;
            SetDepthPresentationVisible(true);
            depthMovementActive = true;
        }

        private void BeginDepthReturn()
        {
            groundDepthSpeed = settings.CombatDepthTravelSpeed * settings.MovementSpeedMultiplier;
            SetDepthPresentationVisible(true);
        }

        private void TickDepthMovement(float scaledDelta, float realDelta)
        {
            if (suspension != null) return;
            bool planning = depthPlanningRequested && !resolving;
            depthPlanningRequested = false;
            float delta = planning ? realDelta : scaledDelta;
            BrakeUnrequestedGroundTravel(player, delta);
            BrakeUnrequestedGroundTravel(enemy, delta);
        }

        // Horizontal contact never pulls fighters onto a shared depth. A genuine lateral step may still have a target.
        private void TravelOnGround(Actor actor, Vector2 error, float maxSpeed, float delta,
            float speedScale = 1f, bool keepDepth = false)
        {
            if (suspension != null || actor == enemy && EnemyIsDummy) return;
            actor.GroundTravelRequested = true;
            actor.GroundBrakeArmed = false;
            if (!settings.AutoDepthEnabled)
            {
                error.y = 0f;
                actor.GroundVelocity.y = 0f;
            }
            float depthBraking = -1f;
            if (keepDepth && settings.AutoDepthEnabled)
            {
                if (!actor.GroundDepthBrakeArmed)
                {
                    float velocity = Mathf.Abs(actor.GroundVelocity.y);
                    float duration = Mathf.Min(DuelGroundTravel.BrakingSeconds,
                        2f * MaximumGroundPlantTravel / Mathf.Max(.0001f, velocity));
                    actor.GroundDepthBrakeAcceleration = velocity / duration;
                    actor.GroundDepthBrakeArmed = true;
                }
                depthBraking = actor.GroundDepthBrakeAcceleration;
            }
            else actor.GroundDepthBrakeArmed = false;
            Vector2 shift = DuelGroundTravel.Advance(ref actor.GroundVelocity, error,
                maxSpeed * speedScale, groundDepthSpeed * speedScale, delta, depthBraking);
            ApplyGroundTravel(actor, shift);
        }

        private void BrakeUnrequestedGroundTravel(Actor actor, float delta)
        {
            bool requested = actor.GroundTravelRequested;
            actor.GroundTravelRequested = false;
            if (requested || actor.Pushing || actor == player && stepping || actor == enemy && EnemyIsDummy) return;
            if (!settings.AutoDepthEnabled) actor.GroundVelocity.y = 0f;
            if (!actor.GroundBrakeArmed)
            {
                // Plant the feet within a short stride. Cache this deceleration for the whole stop: recalculating it
                // from a falling velocity each frame would weaken the brake and lengthen the glide.
                float duration = Mathf.Min(DuelGroundTravel.BrakingSeconds,
                    2f * MaximumGroundPlantTravel / Mathf.Max(.0001f, actor.GroundVelocity.magnitude));
                actor.GroundBrakeAcceleration = new Vector2(Mathf.Abs(actor.GroundVelocity.x),
                    Mathf.Abs(actor.GroundVelocity.y)) / duration;
                if (actor.GroundDepthBrakeArmed)
                    actor.GroundBrakeAcceleration.y = actor.GroundDepthBrakeAcceleration;
                actor.GroundBrakeArmed = true;
            }
            Vector2 shift = DuelGroundTravel.Brake(ref actor.GroundVelocity, actor.GroundBrakeAcceleration, delta);
            ApplyGroundTravel(actor, shift);
        }

        private void ApplyGroundTravel(Actor actor, Vector2 shift)
        {
            // Coasting also obeys the contact barrier; neither a missed request nor a long frame crosses the opponent.
            float separation = actor == player
                ? bulletTimeWasActive && !resolving && enemyFigures.Count > 1 ? ClosestEnemySeparation : Separation
                : actor.Renderer.transform.localPosition.x - player.Renderer.transform.localPosition.x;
            float closingRoom = Mathf.Max(0f, separation - ContactDistance);
            if (actor == player && shift.x > closingRoom)
            {
                shift.x = closingRoom;
                actor.GroundVelocity.x = 0f;
            }
            else if (actor != player && -shift.x > closingRoom)
            {
                shift.x = -closingRoom;
                actor.GroundVelocity.x = 0f;
            }
            float y = actor.Renderer.transform.localPosition.y;
            float minY = Mathf.Min(y, ActorGroundY - MaximumGroundDepth);
            float maxY = Mathf.Max(y, ActorGroundY + MaximumGroundDepth);
            float limitedY = Mathf.Clamp(y + shift.y, minY, maxY) - y;
            if (Mathf.Abs(limitedY - shift.y) > .000001f) actor.GroundVelocity.y = 0f;
            shift.y = limitedY;
            Vector3 displacement = new Vector3(shift.x, shift.y, 0f);
            actor.Renderer.transform.localPosition += displacement;
            if (actor.Pushing)
            {
                actor.PushStart += displacement;
                actor.PushEnd += displacement;
            }
        }
    }
}
