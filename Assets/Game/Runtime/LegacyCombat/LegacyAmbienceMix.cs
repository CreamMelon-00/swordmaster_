using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>How loud the forest's ambience bed plays (presentation only). It plays while the forest is the scene (a
    /// battle, its result, a cutscene on the forest arena) and is silent elsewhere (title, lobby, briefing). A black
    /// screen silences it with the picture, and a loop of the story's own (a scene's <c>@ambience</c>, the 수훈 hum)
    /// takes precedence: the bed ducks under it by a share. Changes fade on real time, a whole swing of the bed's volume
    /// taking the fade's seconds, so it never cuts in or out.</summary>
    public static class LegacyAmbienceMix
    {
        /// <summary>The bed's volume wanted now.</summary>
        /// <param name="isForest">The forest is the scene.</param>
        /// <param name="volume">The bed's full volume (0..1).</param>
        /// <param name="visibility">How much of the picture shows, 0 (a black screen) to 1.</param>
        /// <param name="sceneLoop">How loud the story's own loop is, 0 (none) to 1 (full).</param>
        /// <param name="duck">How much of the bed gives way to a full story loop, 0 (none) to 1 (all of it).</param>
        public static float Target(bool isForest, float volume, float visibility, float sceneLoop, float duck)
            => isForest ? Clamp01(volume) * Clamp01(visibility) * (1f - Clamp01(duck) * Clamp01(sceneLoop)) : 0f;

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> over <paramref name="realDelta"/>,
        /// at a pace that carries a whole swing of <paramref name="volume"/> (or of the louder of the two, after the volume
        /// was turned down) in <paramref name="fadeSeconds"/>; 0 seconds moves at once.</summary>
        public static float Step(float current, float target, float realDelta, float fadeSeconds, float volume)
        {
            current = Clamp01(current);
            target = Clamp01(target);
            if (!(fadeSeconds > 0f) || float.IsInfinity(realDelta)) return target;
            if (!(realDelta > 0f)) return current;
            float pace = Math.Max(Clamp01(volume), Math.Max(current, target)) / fadeSeconds;
            return target >= current ? Math.Min(target, current + pace * realDelta) : Math.Max(target, current - pace * realDelta);
        }

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
