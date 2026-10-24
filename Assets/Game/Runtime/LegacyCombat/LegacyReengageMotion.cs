using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Measures actual closing travel after the first contact. Camera and attack-pose offsets are not inputs.</summary>
    public sealed class LegacyReengageMotion
    {
        public const float MinimumClosingSpeed = 18f;
        private const float FullClosingSpeed = 54f;
        private const float MaximumActorSpeed = 160f;
        private bool contacted, sampled;
        private float playerX, enemyX;

        public int Direction { get; private set; }

        public void NotifyContact() => contacted = true;

        /// <summary>Stops the current picture while retaining this battle's contact history.</summary>
        public void Clear()
        {
            sampled = false;
            Direction = 0;
        }

        public void Reset()
        {
            contacted = false;
            Clear();
        }

        public float Sample(float nextPlayerX, float nextEnemyX, float realDelta, bool eligible)
        {
            if (!Finite(nextPlayerX) || !Finite(nextEnemyX) || !Finite(realDelta) || realDelta <= 0f)
            {
                Clear();
                return 0f;
            }
            float playerTravel = nextPlayerX - playerX, enemyTravel = nextEnemyX - enemyX;
            float closed = Math.Abs(enemyX - playerX) - Math.Abs(nextEnemyX - nextPlayerX);
            bool hadSample = sampled;
            playerX = nextPlayerX;
            enemyX = nextEnemyX;
            sampled = true;
            Direction = 0;
            if (!contacted || !eligible || !hadSample || closed <= .0001f ||
                Math.Abs(playerTravel) > MaximumActorSpeed * realDelta ||
                Math.Abs(enemyTravel) > MaximumActorSpeed * realDelta) return 0f;
            float speed = closed / realDelta;
            if (speed < MinimumClosingSpeed) return 0f;
            float dominantTravel = Math.Abs(playerTravel) >= Math.Abs(enemyTravel) ? playerTravel : enemyTravel;
            Direction = dominantTravel > 0f ? 1 : -1;
            return Math.Min(1f, speed / FullClosingSpeed);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
