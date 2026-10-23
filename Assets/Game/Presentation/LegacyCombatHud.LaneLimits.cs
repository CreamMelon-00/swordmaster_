using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Each school can reserve three skills per turn. Its remaining count is separate from the current
    /// skill's ACT/time cost, and an exhausted school still accepts a hold to inspect its next skill.</summary>
    public sealed partial class LegacyCombatHud
    {
        // Below the gears' clipped edge, clear of the hub's ACT plate and hold bar above it.
        private const float LaneReservationRowY = GearCentreY - 14f;
        private readonly Text[] laneReservationCounts = new Text[3];
        private readonly int[] shownLaneReservationsRemaining = { -1, -1, -1 };

        private void BuildLaneReservations()
        {
            for (int lane = 0; lane < laneReservationCounts.Length; lane++)
            {
                Text count = Text("Lane Reservations", gears[lane].Window, Vector2.zero,
                    new Vector2(148f, 18f), 14, TextAnchor.MiddleCenter);
                count.supportRichText = false;
                count.verticalOverflow = VerticalWrapMode.Truncate;
                count.resizeTextForBestFit = true;
                count.resizeTextMinSize = 11;
                count.resizeTextMaxSize = 14;
                count.text = "남은 " + LegacyLaneLimit.MaxReservations + "/" + LegacyLaneLimit.MaxReservations;
                laneReservationCounts[lane] = count;
            }
        }

        private void UpdateLaneReservations(ILegacyDuelView session)
        {
            for (int lane = 0; lane < laneReservationCounts.Length; lane++)
            {
                Text count = laneReservationCounts[lane];
                count.rectTransform.anchoredPosition = new Vector2(0f,
                    LaneReservationRowY - gears[lane].Window.anchoredPosition.y);
                int remaining = session.LaneReservationsRemaining(lane);
                if (shownLaneReservationsRemaining[lane] == remaining) continue;
                shownLaneReservationsRemaining[lane] = remaining;
                count.text = "남은 " + remaining + "/" + LegacyLaneLimit.MaxReservations +
                    (remaining == 0 ? " · 다음 턴" : string.Empty);
                count.color = remaining == 0 ? DuelVisualTheme.Danger : Foreground;
            }
        }

        private bool LaneHasReservations(int lane)
            => displayedSession != null && displayedSession.LaneReservationsRemaining(lane) > 0;

        private void QueueLaneFromControl(int lane, Action<int> queue)
        {
            // Read the view again: several clicks in one frame must not use the button's old enabled state.
            // LanePointerHold reads pointer presses independently of Button.interactable, so a spent lane can still explain.
            if (disposed || queue == null || lane < 0 || lane >= laneButtons.Length || !lastPlanning || inputsHeld ||
                inspectedSlot >= 0 || !laneAffordable[lane] || !LaneHasReservations(lane) ||
                guide != null && !guide.AllowsQueue(lane)) return;
            queue.Invoke(lane);
            UpdateLaneReservations(displayedSession);
            ApplyInputAvailability();
        }
    }
}
