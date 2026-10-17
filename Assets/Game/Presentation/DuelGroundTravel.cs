using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Bounded kinematic footwork on the arena floor. Only a requested reach error or existing momentum moves
    /// an actor; animation phases never manufacture a destination. No physics world or random stream is involved.</summary>
    internal static class DuelGroundTravel
    {
        internal const float AccelerationSeconds = .10f, BrakingSeconds = .08f;
        internal const float MaximumStep = 1f / 120f, MaximumDelta = .25f;
        private const float Epsilon = .000001f;

        internal static Vector2 Advance(ref Vector2 velocity, Vector2 error, float maxSpeed, float maxDepthSpeed, float delta,
            float depthBraking = -1f)
        {
            Sanitize(ref velocity);
            if (!Finite(error.x) || !Finite(error.y)) return Vector2.zero;
            float remainingTime = Duration(delta);
            maxSpeed = Speed(maxSpeed);
            maxDepthSpeed = Speed(maxDepthSpeed);
            Vector2 displacement = Vector2.zero;
            while (remainingTime > Epsilon)
            {
                float step = Mathf.Min(MaximumStep, remainingTime);
                Vector2 moved = new Vector2(
                    ApproachAxis(ref velocity.x, error.x - displacement.x, maxSpeed, step),
                    depthBraking >= 0f ? BrakeAxis(ref velocity.y, depthBraking, step)
                        : ApproachAxis(ref velocity.y, error.y - displacement.y, maxDepthSpeed, step));
                displacement += moved;
                remainingTime -= step;
            }
            return displacement;
        }

        internal static Vector2 Brake(ref Vector2 velocity, Vector2 deceleration, float delta)
        {
            Sanitize(ref velocity);
            float remainingTime = Duration(delta);
            Vector2 displacement = Vector2.zero;
            while (remainingTime > Epsilon)
            {
                float step = Mathf.Min(MaximumStep, remainingTime);
                displacement += new Vector2(BrakeAxis(ref velocity.x, deceleration.x, step),
                    BrakeAxis(ref velocity.y, deceleration.y, step));
                remainingTime -= step;
            }
            return displacement;
        }

        private static float BrakeAxis(ref float velocity, float braking, float delta)
        {
            if (!(braking > 0f)) { velocity = 0f; return 0f; }
            // Integrate only until rest if this substep spans the stop; the unused interval travels no extra distance.
            float duration = Mathf.Min(delta, Mathf.Abs(velocity) / braking);
            float next = Mathf.MoveTowards(velocity, 0f, braking * duration);
            float moved = (velocity + next) * (.5f * duration);
            velocity = next;
            return moved;
        }

        private static float ApproachAxis(ref float velocity, float error, float speed, float delta)
        {
            if (!(speed > 0f) || Mathf.Abs(error) <= Epsilon)
            {
                velocity = 0f;
                return 0f;
            }
            float acceleration = speed / AccelerationSeconds, braking = speed / BrakingSeconds;
            float desired = Mathf.Sign(error) * Mathf.Min(speed, Mathf.Sqrt(2f * braking * Mathf.Abs(error)));
            bool slowing = velocity * desired < 0f || Mathf.Abs(desired) < Mathf.Abs(velocity);
            float next = Mathf.MoveTowards(velocity, desired, (slowing ? braking : acceleration) * delta);
            float moved = (velocity + next) * (.5f * delta);
            if (moved * error > 0f && Mathf.Abs(moved) >= Mathf.Abs(error))
            {
                velocity = 0f;
                return error;
            }
            velocity = next;
            return moved;
        }

        private static float Duration(float value) => Finite(value) ? Mathf.Clamp(value, 0f, MaximumDelta) : 0f;
        private static float Speed(float value) => Finite(value) ? Mathf.Clamp(value, 0f, 1000f) : 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Sanitize(ref Vector2 velocity)
        {
            if (!Finite(velocity.x)) velocity.x = 0f;
            if (!Finite(velocity.y)) velocity.y = 0f;
        }
    }
}
