namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Accepted manual reservations allowed from each Q/W/E lane in one turn.
    /// Cancellation and return do not restore them; breathing, lane cycling and counters do not spend them.</summary>
    public static class LegacyLaneLimit
    {
        public const int MaxReservations = 3;
    }
}
