using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>How the dodge/pressure success window narrows with use: every attempt this turn, hit or miss,
    /// dodge or pressure, tightens the next window down to a human floor. The count restarts each turn
    /// (<see cref="LegacyQueuedDuel.StepAttemptsThisTurn"/>). Any accepted attempt limits the next turn's natural
    /// ACT recovery to 1; misses also reset the success streak.</summary>
    public static class LegacyStepTiming
    {
        public const float DefaultDecay = 0.65f;
        public const float DefaultMinimumWindow = 0.04f;

        /// <param name="baseWindow">The untouched window, in combat-clock seconds.</param>
        /// <param name="attemptsThisTurn">Attempts already made this turn.</param>
        /// <param name="decay">Factor applied per attempt, from 0 (exclusive) to 1.</param>
        /// <param name="minimumWindow">The floor; never above the base window.</param>
        public static float Window(float baseWindow, int attemptsThisTurn, float decay = DefaultDecay,
            float minimumWindow = DefaultMinimumWindow)
        {
            if (baseWindow <= 0f || float.IsNaN(baseWindow)) return 0f;
            float floor = Math.Min(baseWindow, Math.Max(0f, minimumWindow));
            if (attemptsThisTurn <= 0) return baseWindow;
            decay = decay > 0f && decay <= 1f ? decay : DefaultDecay;
            return Math.Max(floor, baseWindow * (float)Math.Pow(decay, attemptsThisTurn));
        }
    }
}
