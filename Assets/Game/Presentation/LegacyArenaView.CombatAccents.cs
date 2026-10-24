using System;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private float impactFlashStrength = 1f;

        /// <summary>A substantial blade collision, shared by both combat controllers.</summary>
        public event Action StrongClashPresented;
        public event Action CombatAccentsCleared;
        public float PlayerImpactStrength { get; private set; } = 1f;
        public float EnemyImpactStrength { get; private set; } = 1f;

        /// <summary>Clears transient audio and travel cues without changing movement or the next impact.</summary>
        public void ClearCombatAccents()
        {
            ClearReengageSpeedLines();
            CombatAccentsCleared?.Invoke();
        }

        private void ResetCombatAccents()
        {
            ResetReengageSpeedLines();
            impactFlashStrength = PlayerImpactStrength = EnemyImpactStrength = 1f;
            CombatAccentsCleared?.Invoke();
        }

        private void ObserveStrongClash(bool guarded, HitExchange exchange, int healthDamage, float power)
        {
            if (suspension == null && healthDamage <= 0 && (guarded || exchange != HitExchange.None) && power >= 12f)
                StrongClashPresented?.Invoke();
        }

        private static float ImpactStrength(float strength)
            => float.IsNaN(strength) || float.IsInfinity(strength) ? 1f : Mathf.Clamp01(strength);
    }
}
