namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>An internal classification of fights. It is never shown to the player and changes no rule, number or
    /// timing; only the presentation reads it.</summary>
    public enum EncounterKind
    {
        /// <summary>결투: the original turn presentation. Both figures stand still while the player plans.</summary>
        Duel,
        /// <summary>전투: the planning phase is bullet time. The figures edge toward each other in a held pose under a
        /// cold grade, and the existing approach and resolution run at normal speed after the commit.</summary>
        Battle,
    }
}
