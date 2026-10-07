using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>One coached beat as the player met it: its mission, its place in the coach's count and its copy.</summary>
    public sealed class TutorialRecallBeat
    {
        internal TutorialRecallBeat(int missionNumber, int stepNumber, int stepCount, MissionGuideBeat beat)
        {
            MissionNumber = missionNumber;
            StepNumber = stepNumber;
            StepCount = stepCount;
            Beat = beat ?? throw new ArgumentNullException(nameof(beat));
        }

        public int MissionNumber { get; }
        /// <summary>One-based, as the coach counts it.</summary>
        public int StepNumber { get; }
        public int StepCount { get; }
        public MissionGuideBeat Beat { get; }
        /// <summary>What a captured screen of this beat is kept under (<see cref="TutorialRecall.Key"/>).</summary>
        public string Key => TutorialRecall.Key(MissionNumber, StepNumber);
    }

    /// <summary>The 서막's tutorial recall (the cutscene command <c>@recall</c>): in mission 3 엘리사 knows the voice in
    /// her head for the coach of her first missions, and one of the lessons she was shown flashes up. Presentation
    /// captures the screen while a coached beat of the missions up to <see cref="LastMission"/> is on it, and keeps at
    /// most <see cref="MaximumScreens"/> for the session (<see cref="Slot"/>). When it holds none (a game continued from a
    /// save, captures that failed, batch mode) it redraws one of <see cref="Beats"/> as a coach card instead. Every
    /// choice comes from a <see cref="Random"/> the caller supplies, so a seed fixes it.</summary>
    public static class TutorialRecall
    {
        /// <summary>The last mission whose coached beats are remembered: the lessons before mission 3 recalls them.</summary>
        public const int LastMission = 2;
        /// <summary>The most captured screens kept at once.</summary>
        public const int MaximumScreens = 6;

        private static TutorialRecallBeat[] beats;

        /// <summary>Whether <paramref name="mission"/>'s coached beats are remembered: the 서막's first missions only.</summary>
        public static bool Remembers(PrologueMission mission)
            => mission != null && mission.RequiredClearedStage == 0 && mission.Number <= LastMission;

        /// <summary>The key a coached beat's screen is kept under, one per beat.</summary>
        public static string Key(int missionNumber, int stepNumber)
        {
            if (missionNumber < 1) throw new ArgumentOutOfRangeException(nameof(missionNumber));
            if (stepNumber < 1) throw new ArgumentOutOfRangeException(nameof(stepNumber));
            return missionNumber + ":" + stepNumber;
        }

        /// <summary>Every coached beat of the remembered missions, in the order they are met: what a recall without a
        /// captured screen redraws. The copy is read from the sheet when it is shown, as the coach reads it.</summary>
        public static IReadOnlyList<TutorialRecallBeat> Beats => beats ?? (beats = Collect());

        /// <summary>One of <paramref name="count"/> choices, at random from <paramref name="random"/>.</summary>
        public static int Pick(Random random, int count)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            return random.Next(count);
        }

        /// <summary>Where the <paramref name="offered"/>-th screen of a session goes in a set that keeps at most
        /// <paramref name="capacity"/>: in order while there is room, then into a random place or nowhere (-1), so that
        /// every screen offered so far has the same chance to be kept (reservoir sampling).</summary>
        public static int Slot(Random random, int offered, int capacity)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (offered < 1) throw new ArgumentOutOfRangeException(nameof(offered));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (offered <= capacity) return offered - 1;
            int place = random.Next(offered);
            return place < capacity ? place : -1;
        }

        private static TutorialRecallBeat[] Collect()
        {
            var collected = new List<TutorialRecallBeat>();
            foreach (PrologueMission mission in PrologueMissions.All)
            {
                MissionGuide guide = Remembers(mission) ? mission.CreateGuide() : null;
                if (guide == null) continue;
                for (int index = 0; index < guide.StepCount; index++)
                    collected.Add(new TutorialRecallBeat(mission.Number, index + 1, guide.StepCount, guide.Beats[index]));
            }
            return collected.ToArray();
        }
    }
}
