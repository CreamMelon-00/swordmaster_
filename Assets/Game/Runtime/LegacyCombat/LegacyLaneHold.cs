using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Holding a lane in planning (presentation only; the combat rules never read it): its key, or the pointer on its
    /// gear's window. A tap queues the lane's front skill; held a little longer it opens that skill's explanation, the hold
    /// bar filling in between, and a release after it opened never queues. While the player reads an open explanation the
    /// planning clock runs slower (and with it 전투's bullet-time drift), as it does while Tab inspects the enemy's queue;
    /// nothing slows outside planning.</summary>
    public static class LegacyLaneHold
    {
        // The tunables' defaults (DuelPresentationSettings): real seconds, and a factor on the planning clock.
        public const float DefaultTapSeconds = .2f, DefaultExplainSeconds = .35f, DefaultExplanationTimeScale = .3f;
        /// <summary>The planning clock's pace while Tab inspects the enemy's queue.</summary>
        public const float InspectionTimeScale = .2f;

        /// <summary>When a hold opens the explanation: as tuned, but never before a tap could still queue.</summary>
        public static float ExplainSeconds(float tapSeconds, float explainSeconds)
            => Math.Max(Seconds(tapSeconds), Seconds(explainSeconds));

        /// <summary>Whether a lane held <paramref name="heldSeconds"/> (real time) has opened its skill's explanation.</summary>
        public static bool OpensExplanation(float heldSeconds, float tapSeconds, float explainSeconds)
            => heldSeconds >= ExplainSeconds(tapSeconds, explainSeconds);

        /// <summary>Whether releasing a lane held <paramref name="heldSeconds"/> queues its skill: a tap does, unless the hold
        /// was <paramref name="spent"/> (its explanation opened, 넘기기 sent the skill under it to the back, Tab, the coach's
        /// Enter or a commit cut it while it was down, or it was let go off the window).</summary>
        public static bool ReleaseQueues(float heldSeconds, bool spent, float tapSeconds)
            => !spent && heldSeconds <= Seconds(tapSeconds);

        /// <summary>The hold bar, 0 to 1: empty through a tap, filling until the explanation opens, full then.</summary>
        public static float Progress(float heldSeconds, float tapSeconds, float explainSeconds)
        {
            float tap = Seconds(tapSeconds), open = ExplainSeconds(tapSeconds, explainSeconds);
            if (!(heldSeconds > tap)) return 0f;
            if (!(open > tap)) return 1f;
            float share = (heldSeconds - tap) / (open - tap);
            return share > 1f ? 1f : share;
        }

        /// <summary>The planning clock's pace this frame: Tab's inspection's, the explanation's while the player reads one
        /// (<paramref name="explanationTimeScale"/>, 0 to 1), or 1. Outside <paramref name="planning"/> it is always 1: the
        /// slow belongs to reading, not to the battle.</summary>
        public static float PlanningTimeScale(bool planning, bool inspecting, bool explaining, float explanationTimeScale)
        {
            if (!planning) return 1f;
            if (inspecting) return InspectionTimeScale;
            if (!explaining || float.IsNaN(explanationTimeScale)) return 1f;
            return explanationTimeScale < 0f ? 0f : explanationTimeScale > 1f ? 1f : explanationTimeScale;
        }

        // Negative or NaN reads as no time at all; an endless one stays endless (never opens).
        private static float Seconds(float value) => value > 0f ? value : 0f;
    }
}
