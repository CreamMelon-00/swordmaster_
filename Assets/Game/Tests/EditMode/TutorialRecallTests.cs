using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The 서막's tutorial recall (<c>@recall</c>): which lessons are remembered, what a recall without a captured
    /// screen redraws, and the seeded choices behind both. The captures themselves are Presentation's.</summary>
    public sealed class TutorialRecallTests
    {
        [Test]
        public void TheFirstMissionsAreRemembered_AndTheirCoachedBeatsAreWhatARecallRedraws()
        {
            Assert.That(TutorialRecall.LastMission, Is.EqualTo(2));
            Assert.That(TutorialRecall.Remembers(PrologueMissions.Get(1)) && TutorialRecall.Remembers(PrologueMissions.Get(2)), Is.True);
            Assert.That(TutorialRecall.Remembers(PrologueMissions.Get(3)) || TutorialRecall.Remembers(PrologueMissions.Get(4)), Is.False,
                "Mission 3 is where she recalls them.");
            Assert.That(StoryMissions.All.Skip(PrologueMissions.Count).Any(TutorialRecall.Remembers), Is.False,
                "The lobby's missions are not the 서막's lessons.");
            Assert.That(TutorialRecall.Remembers(null), Is.False);

            var expected = new List<string>();
            for (int number = 1; number <= TutorialRecall.LastMission; number++)
            {
                MissionGuide guide = PrologueMissions.Get(number).CreateGuide();
                for (int index = 0; index < guide.StepCount; index++)
                    expected.Add($"{number} {index + 1}/{guide.StepCount} {guide.Beats[index].Title}");
            }
            IReadOnlyList<TutorialRecallBeat> beats = TutorialRecall.Beats;
            Assert.That(beats.Select(beat => $"{beat.MissionNumber} {beat.StepNumber}/{beat.StepCount} {beat.Beat.Title}"),
                Is.EqualTo(expected), "Every coached beat of missions 1 and 2, in the order they are met.");
            Assert.That(beats.Count, Is.EqualTo(15));
            Assert.That(beats[0].Key, Is.EqualTo("1:1"));
            Assert.That(beats[beats.Count - 1].Key, Is.EqualTo("2:9"));
            Assert.That(beats.Select(beat => beat.Key).Distinct().Count(), Is.EqualTo(beats.Count), "One key per beat.");
            Assert.That(TutorialRecall.Key(2, 3), Is.EqualTo(TutorialRecall.Key(2, 3)).And.Not.EqualTo(TutorialRecall.Key(3, 2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialRecall.Key(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialRecall.Key(1, 0));
        }

        [Test]
        public void Pick_IsFixedByItsSeed_AndReachesEveryChoice()
        {
            var first = new Random(7);
            var second = new Random(7);
            int[] picks = Enumerable.Range(0, 200).Select(_ => TutorialRecall.Pick(first, 15)).ToArray();
            Assert.That(Enumerable.Range(0, 200).Select(_ => TutorialRecall.Pick(second, 15)), Is.EqualTo(picks), "A seed fixes the choice.");
            Assert.That(picks.Distinct().OrderBy(pick => pick), Is.EqualTo(Enumerable.Range(0, 15)));
            Assert.That(TutorialRecall.Pick(new Random(3), 1), Is.Zero);
            Assert.Throws<ArgumentNullException>(() => TutorialRecall.Pick(null, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialRecall.Pick(new Random(1), 0));
        }

        [Test]
        public void Slot_KeepsTheFirstScreensInOrder_ThenEachLaterOneWithTheSameChance()
        {
            const int capacity = TutorialRecall.MaximumScreens;
            var random = new Random(11);
            for (int offered = 1; offered <= capacity; offered++)
                Assert.That(TutorialRecall.Slot(random, offered, capacity), Is.EqualTo(offered - 1), "Kept in order while there is room.");

            // The 12th screen offered to a set of 6 is kept half the time, in any of the six places.
            const int offeredLater = capacity * 2, trials = 6000;
            var kept = new int[capacity];
            int dropped = 0;
            for (int trial = 0; trial < trials; trial++)
            {
                int slot = TutorialRecall.Slot(random, offeredLater, capacity);
                Assert.That(slot, Is.InRange(-1, capacity - 1));
                if (slot < 0) dropped++;
                else kept[slot]++;
            }
            Assert.That(dropped / (double)trials, Is.EqualTo(.5).Within(.03));
            foreach (int count in kept) Assert.That(count / (double)trials, Is.EqualTo(1.0 / offeredLater).Within(.015));

            Assert.That(Enumerable.Range(1, 20).Select(offered => TutorialRecall.Slot(new Random(5), offered, 4)),
                Is.EqualTo(Enumerable.Range(1, 20).Select(offered => TutorialRecall.Slot(new Random(5), offered, 4))), "A seed fixes it.");
            Assert.Throws<ArgumentNullException>(() => TutorialRecall.Slot(null, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialRecall.Slot(new Random(1), 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialRecall.Slot(new Random(1), 1, 0));
        }
    }
}
