using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Optional enemy roster for a campaign battle. Queue positions are shared; their owners remain stable
    /// even after a reservation is canceled. A one-on-one duel exposes one entry through the same contract.</summary>
    public interface ILegacyEnemyRosterView
    {
        IReadOnlyList<LegacyFighterState> Enemies { get; }
        int ActiveEnemyIndex { get; }
        IReadOnlyList<LegacyStatusSnapshot> GetEnemyStatuses(int enemyIndex);
        int EnemyOwnerAtSlot(int slotIndex);
        int EnemyIndexForSlot(int slotIndex);
        bool IsEnemySlotCanceled(int slotIndex);
        string EnemyName(int enemyIndex);
        int TotalEnemyHealth { get; }
        int TotalEnemyMaxHealth { get; }
    }
}
