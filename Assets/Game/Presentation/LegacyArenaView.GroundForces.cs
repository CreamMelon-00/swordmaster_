using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private const float MaximumGroundPushSlope = .35f;
        private const float GroundDodgeSideDistance = .28f;
        private const float MaximumGroundDepth = 1.2f;
        private float nextDodgeDepthSide = 1f;
        private float stepDodgeDepthSide;
        private float stepDodgeSideDistance = GroundDodgeSideDistance;

        // Push integration uses the actors' shared arena-local coordinates. Keep the legacy outward X sign, but carry
        // the actual difference in floor depth into the impulse. Mirroring X therefore leaves the canonical Y intact.
        private Vector3 GroundPushDirection(Actor attacker, Actor target, bool playerAttacks)
        {
            float side = playerAttacks ? 1f : -1f;
            if (!settings.AutoDepthEnabled) return Vector3.right * side;
            Vector3 difference = target.Renderer.transform.localPosition - attacker.Renderer.transform.localPosition;
            float slope = Mathf.Clamp(difference.y / Mathf.Max(.001f, Mathf.Abs(difference.x)),
                -MaximumGroundPushSlope, MaximumGroundPushSlope);
            if (float.IsNaN(slope)) slope = 0f;
            return new Vector3(side, slope, 0f).normalized;
        }

        private float ChooseDodgeDepthSide()
        {
            if (!settings.AutoDepthEnabled || stepAction != LegacyStepAction.Dodge ||
                Mathf.Abs(stepDistance) <= .0001f) return 0f;
            float perspective = depthLocalPlayer == 0 ? 1f : -1f;
            float side = nextDodgeDepthSide * perspective;
            float y = player.Renderer.transform.localPosition.y;
            float upperRoom = ActorGroundY + MaximumGroundDepth - y;
            float lowerRoom = y - (ActorGroundY - MaximumGroundDepth);
            float room = side > 0f ? upperRoom : lowerRoom;
            float otherRoom = side > 0f ? lowerRoom : upperRoom;
            if (room < stepDodgeSideDistance && otherRoom > room) side = -side;
            // Advance only for an accepted sideways gesture, using the side actually taken at an edge.
            // Latch it once: trail sampling, hit stop and later skill/turn boundaries cannot pick it again.
            nextDodgeDepthSide = -side * perspective;
            return side;
        }

        // Desired total sideways travel for this step, not its per-frame increment. Subtracting the previous applied
        // offset recovers the moving step origin, so pauses, sampled afterimages and carried push endpoints stay coherent.
        private float GroundDodgeLateral(float xOffset)
        {
            if (!settings.AutoDepthEnabled || stepAction != LegacyStepAction.Dodge ||
                Mathf.Abs(stepDistance) <= .0001f) return 0f;
            float progress = Mathf.Clamp01(Mathf.Abs(xOffset) / Mathf.Abs(stepDistance));
            float origin = player.Renderer.transform.localPosition.y - stepYApplied;
            float desired = stepDodgeSideDistance * progress * stepDodgeDepthSide;
            return Mathf.Clamp(origin + desired, ActorGroundY - MaximumGroundDepth,
                ActorGroundY + MaximumGroundDepth) - origin;
        }
    }
}
