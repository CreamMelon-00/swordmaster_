using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A newly reserved card's drop into its queue slot. The height is a share of the card's height;
    /// scales multiply its normal layout. Presentation only: this never changes a reservation or its timing.</summary>
    public readonly struct LegacyQueueInsertionPose
    {
        public static LegacyQueueInsertionPose Rest { get; } = new LegacyQueueInsertionPose(0f, 1f, 1f);

        public LegacyQueueInsertionPose(float height, float scaleX, float scaleY)
        {
            Height = height;
            ScaleX = scaleX;
            ScaleY = scaleY;
        }

        public float Height { get; }
        public float ScaleX { get; }
        public float ScaleY { get; }
    }

    /// <summary>Tracks the visible queue by reference and occurrence. Existing cards keep their age when removals
    /// compact the queue or another card is inserted before them. The HUD supplies a baseline at turn/session changes,
    /// while hidden, and outside planning. Buffers are retained between frames; the combat rules never read this state.</summary>
    public sealed class LegacyQueueInsertionCue
    {
        public const float Duration = .20f;
        private const float LandingSeconds = .13f;
        private const int InitialCapacity = 32;
        private readonly List<LegacySkill> previous = new List<LegacySkill>(InitialCapacity);
        private float[] ages = new float[InitialCapacity], nextAges = new float[InitialCapacity];
        private int[] matches = new int[(InitialCapacity + 1) * (InitialCapacity + 1)];
        private int capacity = InitialCapacity;
        private bool hasBaseline;

        /// <summary>Only unmatched new occurrences enter from above. A new occurrence starts at its first pose even
        /// when this frame's delta is large. <paramref name="replayLastTechnique"/> identifies a successful manual
        /// reservation whose cancel/return effect may replace an identical-looking queue with that same skill again.</summary>
        public void Refresh(IReadOnlyList<LegacySkill> queue, bool animate, float realDelta,
            bool replayLastTechnique = false)
        {
            if (queue == null)
            {
                Clear();
                return;
            }
            EnsureCapacity(queue.Count);
            if (!animate || !hasBaseline)
            {
                CopyQueue(queue);
                for (int index = 0; index < queue.Count; index++) ages[index] = Duration;
                hasBaseline = true;
                return;
            }

            // Invalid clock samples cannot poison a pose or make it run backwards.
            float delta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            int replaySlot = replayLastTechnique ? LastTechnique(queue) : -1;
            if (IsSameQueue(queue))
            {
                for (int index = 0; index < previous.Count; index++)
                    ages[index] = Math.Min(Duration, ages[index] + delta);
            }
            else
            {
                MapSurvivors(queue, delta, replaySlot);
                CopyQueue(queue);
                float[] swap = ages;
                ages = nextAges;
                nextAges = swap;
            }

            if (replaySlot >= 0) ages[replaySlot] = 0f;
        }

        public LegacyQueueInsertionPose Pose(int slot)
        {
            if (slot < 0 || slot >= previous.Count || previous[slot] == null || ages[slot] >= Duration)
                return LegacyQueueInsertionPose.Rest;
            float age = ages[slot];
            if (age <= LandingSeconds)
            {
                float t = age / LandingSeconds;
                float drop = t * t;
                return new LegacyQueueInsertionPose(.8f - .84f * drop, .96f + .10f * drop, 1.06f - .14f * drop);
            }
            float settle = (age - LandingSeconds) / (Duration - LandingSeconds);
            float remaining = 1f - settle;
            remaining = remaining * remaining * remaining;
            return new LegacyQueueInsertionPose(-.04f * remaining, 1f + .06f * remaining, 1f - .08f * remaining);
        }

        /// <summary>Ends all drops. The next refresh establishes a still baseline, even if animation is requested.</summary>
        public void Clear()
        {
            previous.Clear();
            hasBaseline = false;
        }

        private bool IsSameQueue(IReadOnlyList<LegacySkill> queue)
        {
            if (previous.Count != queue.Count) return false;
            for (int index = 0; index < queue.Count; index++)
                if (!ReferenceEquals(previous[index], queue[index])) return false;
            return true;
        }

        private void CopyQueue(IReadOnlyList<LegacySkill> queue)
        {
            previous.Clear();
            for (int index = 0; index < queue.Count; index++) previous.Add(queue[index]);
        }

        private static int LastTechnique(IReadOnlyList<LegacySkill> queue)
        {
            for (int index = queue.Count - 1; index >= 0; index--)
                if (queue[index] != null && !queue[index].IsWait) return index;
            return -1;
        }

        private void MapSurvivors(IReadOnlyList<LegacySkill> queue, float delta, int replaySlot)
        {
            int oldCount = previous.Count, newCount = queue.Count, stride = capacity + 1;
            // An LCS keeps each surviving occurrence, including repeated references, in its relative order. This
            // matches cancellation's retained breaths and Iia's insertion before the already reserved final skill.
            for (int index = 0; index <= oldCount; index++) matches[index * stride + newCount] = 0;
            for (int index = 0; index <= newCount; index++) matches[oldCount * stride + index] = 0;
            for (int old = oldCount - 1; old >= 0; old--)
                for (int current = newCount - 1; current >= 0; current--)
                    matches[old * stride + current] = current != replaySlot && ReferenceEquals(previous[old], queue[current])
                        ? 1 + matches[(old + 1) * stride + current + 1]
                        : Math.Max(matches[(old + 1) * stride + current], matches[old * stride + current + 1]);

            for (int index = 0; index < newCount; index++) nextAges[index] = queue[index] == null ? Duration : 0f;
            int before = 0, after = 0;
            while (before < oldCount && after < newCount)
            {
                // A just reserved technique is new even if it is the same object as a removed one. Excluding it
                // from matching also keeps the old breath before it distinct from a new free breath after it.
                if (after != replaySlot && ReferenceEquals(previous[before], queue[after]))
                {
                    nextAges[after] = Math.Min(Duration, ages[before] + delta);
                    before++;
                    after++;
                }
                else if (matches[(before + 1) * stride + after] >= matches[before * stride + after + 1]) before++;
                else after++;
            }
        }

        private void EnsureCapacity(int count)
        {
            if (count <= capacity) return;
            while (capacity < count) capacity *= 2;
            Array.Resize(ref ages, capacity);
            Array.Resize(ref nextAges, capacity);
            matches = new int[(capacity + 1) * (capacity + 1)];
            previous.Capacity = capacity;
        }
    }
}
