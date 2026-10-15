using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class CombatTempoTests
    {
        private const float Tolerance = .00001f;
        private const LegacyClashAction Attack = LegacyClashAction.Attack, Guard = LegacyClashAction.Guard,
            Breath = LegacyClashAction.Breath, Empty = LegacyClashAction.Empty;
        private static readonly LegacyCombatTempoTuning DoubleSpeed = new LegacyCombatTempoTuning(true, .1f, 1.8f, 2f);

        [Test]
        public void EveryLaterSlot_GainsSpeed_RegardlessOfTheActions_UpToTheGeneralCap()
        {
            float[] expected = { 1f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f, 1.8f };
            for (int index = 0; index < expected.Length; index++)
            {
                AssertSpeed(Facts(index, Attack, Attack), expected[index]);
                AssertSpeed(Facts(index, Guard, Guard), expected[index]);
                AssertSpeed(Facts(index, Breath, Breath), expected[index]);
            }
            AssertSpeed(Facts(int.MaxValue, Attack, Attack), 1.8f);
        }

        [Test]
        public void AnExplicitExhaustedMultiplier_DoublesTheFirstEmptySlot_AndKeepsTheProgression()
        {
            AssertSpeed(Facts(0, Attack, Empty), 2f, DoubleSpeed);
            AssertSpeed(Facts(3, Attack, Empty), 2.6f, DoubleSpeed);
            AssertSpeed(Facts(8, Empty, Attack), 3.6f, DoubleSpeed);
            AssertSpeed(Facts(int.MaxValue, Empty, Attack), 3.6f, DoubleSpeed);
            Assert.That(DoubleSpeed.ExhaustedGapScale, Is.EqualTo(1f), "Four-argument tuning preserves the original gap.");
        }

        [Test]
        public void DefaultTempo_PreservesTheAttackSpeed_AndShortensOnlyTheGapToAQuarter()
        {
            AssertSpeed(Facts(0, Attack, Empty), 1f);
            AssertSpeed(Facts(3, Attack, Empty), 1.3f);
            AssertSpeed(Facts(8, Empty, Attack), 1.8f);
            Assert.That(LegacyCombatTempo.GapScale(Facts(3, Attack, Empty), LegacyCombatTempoTuning.Default), Is.EqualTo(.25f));
        }

        [Test]
        public void BreathAndGuard_AreSkills_AndBothEmptyIsNotOneExhaustedSide()
        {
            AssertSpeed(Facts(2, Attack, Breath), 1.2f);
            AssertSpeed(Facts(2, Attack, Guard), 1.2f);
            AssertSpeed(Facts(2, Empty, Empty), 1.2f);
            AssertSpeed(Facts(2, Empty, Breath), 1.2f);
            AssertSpeed(Facts(2, Guard, Empty), 1.2f);
        }

        [Test]
        public void EitherPerspective_AndDecisiveSlotFacts_HaveTheSameTempo()
        {
            foreach (LegacyClashAction player in new[] { Empty, Breath, Guard, Attack })
            foreach (LegacyClashAction enemy in new[] { Empty, Breath, Guard, Attack })
            {
                LegacySlotFacts facts = Facts(5, player, enemy, defeated: true);
                Assert.That(LegacyCombatTempo.Speed(facts, LegacyCombatTempoTuning.Default),
                    Is.EqualTo(LegacyCombatTempo.Speed(facts.Mirrored(), LegacyCombatTempoTuning.Default)).Within(Tolerance));
                bool emptyAttack = player == Attack && enemy == Empty || player == Empty && enemy == Attack;
                float expectedGap = emptyAttack ? .25f : 1f;
                Assert.That(LegacyCombatTempo.GapScale(facts, LegacyCombatTempoTuning.Default), Is.EqualTo(expectedGap));
                Assert.That(LegacyCombatTempo.GapScale(facts.Mirrored(), LegacyCombatTempoTuning.Default), Is.EqualTo(expectedGap));
            }
            AssertSpeed(Facts(5, Attack, Empty, defeated: true), 1.5f);
        }

        [Test]
        public void DisabledOrMissingSlot_AlwaysUsesNormalSpeed()
        {
            Assert.That(LegacyCombatTempo.Speed(Facts(6, Attack, Empty), LegacyCombatTempoTuning.Off), Is.EqualTo(1f));
            Assert.That(LegacyCombatTempo.Speed(LegacySlotFacts.None, LegacyCombatTempoTuning.Default), Is.EqualTo(1f));
            Assert.That(LegacyCombatTempo.Speed(Facts(6, Attack, Empty), default), Is.EqualTo(1f));
            Assert.That(LegacyCombatTempo.GapScale(Facts(6, Attack, Empty), LegacyCombatTempoTuning.Off), Is.EqualTo(1f));
            Assert.That(LegacyCombatTempo.GapScale(LegacySlotFacts.None, LegacyCombatTempoTuning.Default), Is.EqualTo(1f));
        }

        [Test]
        public void Tuning_ClampsToFiniteSafeRanges_AndUsesDefaultsForNaN()
        {
            var low = new LegacyCombatTempoTuning(true, float.NegativeInfinity, -1f, 0f, float.NegativeInfinity);
            Assert.That((low.StepPerSlot, low.MaximumSpeed, low.ExhaustedSpeed, low.ExhaustedGapScale), Is.EqualTo((0f, 1f, 1f, 0f)));
            var high = new LegacyCombatTempoTuning(true, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity,
                float.PositiveInfinity);
            Assert.That((high.StepPerSlot, high.MaximumSpeed, high.ExhaustedSpeed, high.ExhaustedGapScale), Is.EqualTo((.5f, 3f, 3f, 1f)));
            var missing = new LegacyCombatTempoTuning(true, float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.That((missing.StepPerSlot, missing.MaximumSpeed, missing.ExhaustedSpeed, missing.ExhaustedGapScale),
                Is.EqualTo((.1f, 1.8f, 1f, .25f)));
            Assert.That(LegacyCombatTempo.Speed(Facts(int.MaxValue, Attack, Empty), high), Is.EqualTo(9f));
        }

        [Test]
        public void Director_CachesTheFinalSlotSpeedForItsGap_AndResetsAtNextSlotAndTurn()
        {
            LegacyShowDirector director = Director();
            director.BeginSlot(Facts(3, Attack, Empty));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(1.3f).Within(Tolerance));
            Assert.That(director.PlaybackGapSpeed, Is.EqualTo(1f));
            Assert.That(director.PlaybackGapScale, Is.EqualTo(1f));
            director.EndSlot();
            Assert.That(director.PlaybackSpeed, Is.EqualTo(1f));
            Assert.That(director.PlaybackGapSpeed, Is.EqualTo(1.3f).Within(Tolerance));
            Assert.That(director.PlaybackGapScale, Is.EqualTo(.25f));
            director.BeginSlot(Facts(4, Guard, Guard));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(1.4f).Within(Tolerance));
            Assert.That(director.PlaybackGapSpeed, Is.EqualTo(1f));
            Assert.That(director.PlaybackGapScale, Is.EqualTo(1f));
            director.EndSlot();
            director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default);
            Assert.That(director.Tempo.Enabled, Is.True, "An older overload preserves the chosen tempo.");
            Assert.That((director.PlaybackSpeed, director.PlaybackGapSpeed), Is.EqualTo((1f, 1f)));
            Assert.That(director.PlaybackGapScale, Is.EqualTo(1f));
            director.BeginSlot(Facts(0, Attack, Attack));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(1f));
            director.EndSlot();
            director.Clear();
            Assert.That((director.PlaybackSpeed, director.PlaybackGapSpeed), Is.EqualTo((1f, 1f)));
            Assert.That(director.PlaybackGapScale, Is.EqualTo(1f));
            Assert.That((director.Saved, director.Spent), Is.EqualTo((0f, 0f)), "Tempo savings never fund emphasis.");
        }

        [Test]
        public void Director_AFilledCounterRemovesOnlyTheExhaustedMultiplier()
        {
            LegacyShowDirector director = Director(DoubleSpeed);
            director.BeginSlot(Facts(4, Empty, Attack));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(2.8f).Within(Tolerance));
            director.Observe(Facts(4, Attack, Attack));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(1.4f).Within(Tolerance));
            director.EndSlot();
            Assert.That(director.PlaybackGapSpeed, Is.EqualTo(1.4f).Within(Tolerance));
        }

        [Test]
        public void Director_MajorAndClimax_KeepTheProgressiveSpeed_WithoutStackingTheLegacyStreak()
        {
            LegacyShowDirector director = Director(DoubleSpeed);
            director.BeginSlot(Facts(0, Attack, Empty));
            director.BeginSlot(Facts(1, Attack, Empty));
            Assert.That(director.StreakSpeed, Is.EqualTo(1.15f).Within(Tolerance));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(2.2f).Within(Tolerance));
            director.Observe(Facts(1, Attack, Empty, broken: true));
            Assert.That(director.SlotRank.Tier, Is.EqualTo(LegacySlotTier.Major));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(2.2f).Within(Tolerance));
            director.Observe(Facts(1, Attack, Empty, defeated: true));
            Assert.That(director.SlotRank.Tier, Is.EqualTo(LegacySlotTier.Climax));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(2.2f).Within(Tolerance));
        }

        [Test]
        public void Director_TempoOffKeepsTheLegacyStreak_AndDirectorOffNeverAccelerates()
        {
            var director = new LegacyShowDirector();
            Assert.That(director.Tempo.Enabled, Is.False);
            director.BeginSlot(Facts(0, Attack, Empty));
            director.BeginSlot(Facts(1, Attack, Empty));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(1.15f).Within(Tolerance));
            director.EndSlot();
            Assert.That(director.PlaybackGapSpeed, Is.EqualTo(1.15f).Within(Tolerance));
            director.BeginTurn(LegacyShowPacingTuning.Off, LegacySlotImportanceTuning.Default, LegacyStreakTuning.Default,
                LegacyCombatTempoTuning.Default);
            director.BeginSlot(Facts(7, Attack, Empty));
            Assert.That(director.PlaybackSpeed, Is.EqualTo(1f));
            director.EndSlot();
            Assert.That(director.PlaybackGapSpeed, Is.EqualTo(1f));
            Assert.That(director.PlaybackGapScale, Is.EqualTo(1f));
        }

        [Test]
        public void Director_UsesTheCompletedSlotsFinalActions_ForItsGap_WithoutFundingEmphasis()
        {
            LegacyShowDirector director = Director();
            director.BeginSlot(Facts(3, Attack, Empty));
            director.EndSlot();
            Assert.That(director.PlaybackGapScale, Is.EqualTo(.25f));
            Assert.That(director.ShortenGap(.28f), Is.EqualTo(.28f));
            Assert.That((director.Saved, director.Spent), Is.EqualTo((0f, 0f)), "The empty-side gap saves no ledger credit.");

            director.BeginSlot(Facts(4, Empty, Attack));
            director.Observe(Facts(4, Attack, Attack));
            director.EndSlot();
            Assert.That(director.PlaybackGapScale, Is.EqualTo(1f), "A counter filled the empty side.");

            director.BeginSlot(Facts(5, Empty, Attack));
            director.EndSlot();
            Assert.That(director.PlaybackGapScale, Is.EqualTo(.25f), "The enemy's empty-side attack works identically.");
            director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default);
            Assert.That(director.PlaybackGapScale, Is.EqualTo(1f));
            director.BeginSlot(Facts(0, Attack, Empty));
            director.EndSlot();
            director.Clear();
            Assert.That(director.PlaybackGapScale, Is.EqualTo(1f));
            director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default, LegacyStreakTuning.Default,
                LegacyCombatTempoTuning.Off);
            director.BeginSlot(Facts(6, Attack, Empty));
            director.EndSlot();
            Assert.That(director.PlaybackGapScale, Is.EqualTo(1f));
        }

        private static LegacyShowDirector Director(LegacyCombatTempoTuning? tempo = null)
        {
            var director = new LegacyShowDirector();
            director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default, LegacyStreakTuning.Default,
                tempo ?? LegacyCombatTempoTuning.Default);
            return director;
        }

        private static void AssertSpeed(LegacySlotFacts facts, float expected, LegacyCombatTempoTuning? tempo = null)
            => Assert.That(LegacyCombatTempo.Speed(facts, tempo ?? LegacyCombatTempoTuning.Default), Is.EqualTo(expected).Within(Tolerance));

        private static LegacySlotFacts Facts(int index, LegacyClashAction player, LegacyClashAction enemy,
            bool broken = false, bool defeated = false)
            => new LegacySlotFacts(new LegacyClashInput(index, player, 10, 1, enemy, 10, 1,
                enemyBroken: broken, decided: broken || defeated), playerMaxHealth: 100, enemyMaxHealth: 100,
                enemyDefeated: defeated);
    }
}
