using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Presentation policy only. The match and its exact queue data keep their ordinary simulation rules.</summary>
    public static class LegacyQueueVisibility
    {
        public const int InitiallyRevealedSlots = 2;

        /// <summary>Whether a planning forecast may be shown without reading through an unrevealed opposing slot.
        /// A later hidden 순환 +1 feeder can also change an earlier cycling technique at commit, so forecasts from that
        /// known cycling slot onwards are withheld while an opposing tail remains hidden. Hidden identities are never
        /// inspected to choose whether the forecast is available.</summary>
        public static bool CanForecastSlot(ILegacyDuelView view, int slotIndex)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (slotIndex < 0 || view.Phase != LegacyDuelPhase.Planning) return false;
            int firstHidden = view.EnemyQueue.Count;
            for (int index = 0; index < view.EnemyQueue.Count; index++)
                if (!view.IsEnemySkillRevealed(index)) { firstHidden = index; break; }
            if (firstHidden == view.EnemyQueue.Count) return true;
            if (slotIndex >= firstHidden) return false;
            for (int index = 0; index <= slotIndex; index++)
                if (LegacySkillDefinitions.Find(view.EnemyQueue[index])?.Effect.HasCycle == true) return false;
            return true;
        }
    }
}
