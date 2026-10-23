using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        private ILegacyDuelView insertionSession;
        private int insertionRound = -1, insertionReservations;
        private bool insertionWasPlanning;

        private void RefreshReservationQueues(ILegacyDuelView session, int currentSlot, bool resolving,
            bool planning, float activeScale, float realDelta)
        {
            bool visiblePlanning = planning && root.gameObject.activeInHierarchy;
            bool animate = visiblePlanning && insertionWasPlanning && ReferenceEquals(insertionSession, session) &&
                insertionRound == session.RoundNumber;
            int reservations = session.LaneReservationsThisTurn(0) + session.LaneReservationsThisTurn(1) +
                session.LaneReservationsThisTurn(2);
            // A remover may replace the queue with the very same skill references and length.
            bool replayLast = animate && reservations > insertionReservations;
            playerQueue.Refresh(session.PlayerQueue, iconFor, currentSlot, resolving,
                queueComparisonVisible ? inspectedSlot : -1, activeScale, session, animate, realDelta, replayLast);
            enemyQueue.Refresh(session.EnemyQueue, iconFor, currentSlot, resolving, inspectedSlot,
                activeScale, session, animate, realDelta);
            insertionSession = session;
            insertionRound = session.RoundNumber;
            insertionReservations = reservations;
            insertionWasPlanning = visiblePlanning;
        }

        private void ResetQueueInsertions()
        {
            insertionSession = null;
            insertionRound = -1;
            insertionReservations = 0;
            insertionWasPlanning = false;
            playerQueue?.ClearInsertions();
            enemyQueue?.ClearInsertions();
        }
    }
}
