using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Selects flinches from the direct damage exchanged in one hit, including resistance damage.</summary>
    public static class LegacyHitReaction
    {
        /// <summary>Call after an incoming attack resolves, with self-inflicted damage excluded. Being broken
        /// permits a reaction only when that hit actually dealt damage; it does not create an idle flinch.</summary>
        public static bool ShouldHurt(bool targetAttacked, bool targetBroken, int healthTaken, int resistanceTaken,
            int healthDealt, int resistanceDealt)
        {
            long taken = (long)Math.Max(0, healthTaken) + Math.Max(0, resistanceTaken);
            if (taken == 0) return false;

            long dealt = (long)Math.Max(0, healthDealt) + Math.Max(0, resistanceDealt);
            return targetBroken || !targetAttacked || dealt * 2 <= taken;
        }
    }
}
