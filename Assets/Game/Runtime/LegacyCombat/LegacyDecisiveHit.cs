namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Which hits earn the decisive close-up: the slow motion, close-up camera, tilt, grade pulse and critical
    /// sound that presentation keeps for big moments. A hit is decisive when it breaks the target's resistance, when it
    /// is the finishing blow, or when on its own it takes at least a set share of the target's maximum health as health
    /// (<see cref="DefaultHealthDamagePercent"/>). Each hit is judged alone, in either direction; a multi-hit slot's total
    /// does not count. The combat rules never read this.</summary>
    public static class LegacyDecisiveHit
    {
        /// <summary>The share of the target's maximum health, in percent, that one hit must take for the close-up.</summary>
        public const int DefaultHealthDamagePercent = 25;

        /// <param name="resistanceBroke">The hit took the target's resistance from above zero to zero.</param>
        /// <param name="finishingBlow">The hit took the target's health from above zero to zero.</param>
        /// <param name="healthDamage">The health the target lost to this hit alone (<see cref="LegacyHitResult.EnemyHealthDamage"/>
        /// or <see cref="LegacyHitResult.PlayerHealthDamage"/>).</param>
        /// <param name="targetMaxHealth">The target's maximum health.</param>
        /// <param name="healthDamagePercent">The share for <see cref="IsHeavy"/>; zero or less switches that trigger off and
        /// leaves breaks and finishing blows.</param>
        public static bool IsDecisive(bool resistanceBroke, bool finishingBlow, int healthDamage, int targetMaxHealth,
            int healthDamagePercent = DefaultHealthDamagePercent) =>
            resistanceBroke || finishingBlow || IsHeavy(healthDamage, targetMaxHealth, healthDamagePercent);

        /// <summary>Whether one hit's health damage reaches <paramref name="healthDamagePercent"/>% of the target's maximum
        /// health. The comparison is exact, in whole numbers, with no rounding: 22 of 90 health is 24.4%, short of 25%, and 23
        /// reaches it. Health damage is what the target actually lost: a breaking hit counts only its overflow, a hit the
        /// resistance absorbs counts nothing, and health a floor or death kept the target from losing does not count.</summary>
        public static bool IsHeavy(int healthDamage, int targetMaxHealth, int healthDamagePercent = DefaultHealthDamagePercent) =>
            healthDamagePercent > 0 && healthDamage > 0 && targetMaxHealth > 0 &&
            (long)healthDamage * 100 >= (long)healthDamagePercent * targetMaxHealth;
    }
}
