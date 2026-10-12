using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    /// <summary>The lanes' holds while planning, read the same way in the campaign's battles
    /// (<see cref="DuelPrototypeController"/>) and in versus (<see cref="LocalVersusController"/>): each lane's key and the
    /// pointer on its gear's window (<see cref="LegacyCombatHud.ReadLanePointer"/>), on real time, by
    /// <see cref="LegacyLaneHold"/>'s rules. A tap queues on release; releasing while the bar fills safely cancels with a
    /// brief cue; held to <see cref="DuelPresentationSettings.ExplanationHoldSeconds"/> it opens the front skill's
    /// explanation (<see cref="ExplainedSkill"/>), and that release only closes it. A press on another lane closes an open
    /// explanation.</summary>
    internal sealed class DuelLaneHolds
    {
        public const int LaneCount = 3;
        // How long each lane has been held (real time), whether it is held now, and whether its release must not queue.
        private readonly float[] holdTimes = new float[LaneCount];
        private readonly bool[] laneHeld = new bool[LaneCount];
        private readonly bool[] holdConsumed = new bool[LaneCount];
        private int explainedLane = -1;

        /// <summary>The skill whose explanation a hold has open, or null.</summary>
        public LegacySkill ExplainedSkill { get; private set; }

        /// <summary>A lane's hold this frame. <paramref name="laneSkills"/> is the lane's order (its front skill is the one
        /// explained); <paramref name="queue"/> queues the lane when a tap is released.</summary>
        public void Read(int lane, bool keyHeld, bool keyPressed, bool keyReleased, float realDelta, LegacyCombatHud hud,
            DuelPresentationSettings settings, IReadOnlyList<LegacySkill> laneSkills, Func<int, bool> queue)
        {
            if (lane < 0 || lane >= LaneCount) throw new ArgumentOutOfRangeException(nameof(lane));
            if (hud == null) throw new ArgumentNullException(nameof(hud));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            bool pointerHeld = hud.ReadLanePointer(lane, out bool pointerPressed, out bool pointerReleased, out bool releasedAway);
            bool held = keyHeld || pointerHeld;
            bool pressed = keyPressed || pointerPressed;
            // Let go of both; or a hold that ended with no release seen (its window went away mid-press), which never queues.
            bool letGo = keyReleased || pointerReleased;
            bool vanished = !held && !letGo && laneHeld[lane];
            // A new press is a new hold, a tap until it is held long enough, whatever ended the one before (Clear closes a
            // key still down from then).
            if (pressed)
            {
                holdConsumed[lane] = false;
                hud.ClearHoldCancel(lane);
            }
            if (releasedAway || vanished) holdConsumed[lane] = true;
            float tap = settings.LaneTapSeconds, explain = settings.ExplanationHoldSeconds;
            if (pressed && ExplainedSkill != null)
            {
                ExplainedSkill = null;
                explainedLane = -1;
            }
            laneHeld[lane] = held;
            if (held)
            {
                // The frame a press is first seen counts nothing: it went down somewhere within it, and a hitch before the
                // press (a battle being set up) is no time held.
                if (!pressed) holdTimes[lane] += realDelta;
                hud.SetHoldProgress(lane, LegacyLaneHold.Progress(holdTimes[lane], tap, explain));
                // An open lane a fixture duel left empty has nothing to explain.
                if (LegacyLaneHold.OpensExplanation(holdTimes[lane], tap, explain) && laneSkills != null && laneSkills.Count > 0)
                {
                    ExplainedSkill = laneSkills[0];
                    explainedLane = lane;
                    holdConsumed[lane] = true;
                }
            }
            else if (letGo || vanished)
            {
                bool queues = LegacyLaneHold.ReleaseQueues(holdTimes[lane], holdConsumed[lane], tap);
                bool safelyCancelled = letGo && !held && !releasedAway && !holdConsumed[lane] && !queues &&
                    holdTimes[lane] < LegacyLaneHold.ExplainSeconds(tap, explain);
                if (queues) queue?.Invoke(lane);
                if (explainedLane == lane)
                {
                    ExplainedSkill = null;
                    explainedLane = -1;
                }
                holdTimes[lane] = 0f;
                holdConsumed[lane] = false;
                hud.SetHoldProgress(lane, 0f);
                if (safelyCancelled) hud.ShowHoldCancel(lane);
            }
        }

        /// <summary>Closes the explanation a hold opened; the hold goes on (a lane still held past the explanation's time
        /// opens its front skill's again on the next frame, which is how one picks up the skill 넘기기 brought forward).</summary>
        public void DropExplanation() => ExplainedSkill = null;

        /// <summary>Every hold that is going on or still counting ends without queuing when it is let go (넘기기 turned the
        /// lanes under it: the press was for the skill that left).</summary>
        public void ConsumeActive()
        {
            for (int lane = 0; lane < LaneCount; lane++)
                if (laneHeld[lane] || holdTimes[lane] > 0f) holdConsumed[lane] = true;
        }

        /// <summary>This lane's hold, if one goes on, ends without queuing (its key was down before a menu opened).</summary>
        public void Consume(int lane)
        {
            if (lane >= 0 && lane < LaneCount) holdConsumed[lane] = true;
        }

        /// <summary>Forgets every hold and the explanation (a turn starts or is committed, a menu opens or closes). A key
        /// (or the pointer on a window) still down was pressed before: its hold is over, so letting go of it never queues;
        /// the next press starts a new one. One let go meanwhile ends on the vanished path. Presses on the windows from
        /// before are not taken for taps later.</summary>
        public void Clear(LegacyCombatHud hud)
        {
            ExplainedSkill = null;
            explainedLane = -1;
            for (int i = 0; i < LaneCount; i++)
            {
                holdTimes[i] = 0f;
                holdConsumed[i] = laneHeld[i];
                hud?.SetHoldProgress(i, 0f);
            }
            hud?.ClearLanePointers();
            hud?.ClearHoldCancel();
        }
    }
}
