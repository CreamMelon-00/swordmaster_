using System;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class QueueInsertionCueTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void InitialEnemyQueue_AndANewBaseline_StayStill()
        {
            var cue = new LegacyQueueInsertionCue();
            var queue = new[] { Technique(1), Technique(503) };
            cue.Refresh(queue, true, 0f);
            Rest(cue.Pose(0));
            Rest(cue.Pose(1));
            cue.Refresh(new[] { queue[0], queue[1], Technique(504) }, false, 0f);
            Rest(cue.Pose(2));
        }

        [Test]
        public void ANewReservation_DropsFromAbove_LandsAndSettles()
        {
            var cue = EmptyBaseline();
            var queue = new[] { Technique(1) };
            cue.Refresh(queue, true, 0f);
            Start(cue.Pose(0));
            cue.Refresh(queue, true, .13f);
            LegacyQueueInsertionPose landed = cue.Pose(0);
            Assert.That(landed.Height, Is.EqualTo(-.04f).Within(Tolerance));
            Assert.That(landed.ScaleX, Is.EqualTo(1.06f).Within(Tolerance));
            Assert.That(landed.ScaleY, Is.EqualTo(.92f).Within(Tolerance));
            cue.Refresh(queue, true, LegacyQueueInsertionCue.Duration - .13f);
            Rest(cue.Pose(0));
        }

        [Test]
        public void ANewReservation_KeepsItsFirstPose_EvenOnALongFrame()
        {
            var cue = EmptyBaseline();
            LegacySkill first = Technique(1), second = Technique(2);
            cue.Refresh(new[] { first }, true, float.MaxValue);
            Start(cue.Pose(0));
            cue.Refresh(new[] { first, second }, true, float.MaxValue);
            Rest(cue.Pose(0));
            Start(cue.Pose(1));
        }

        [Test]
        public void RepeatingTheSameSkillObject_AnimatesOnlyItsNewOccurrence()
        {
            var cue = new LegacyQueueInsertionCue();
            LegacySkill skill = Technique(1);
            cue.Refresh(new[] { skill }, false, 0f);
            cue.Refresh(new[] { skill, skill }, true, 0f);
            Rest(cue.Pose(0));
            Start(cue.Pose(1));
            cue.Refresh(new[] { skill, skill, skill }, true, .05f);
            Rest(cue.Pose(0));
            Assert.That(cue.Pose(1).Height, Is.LessThan(.8f));
            Start(cue.Pose(2));
        }

        [Test]
        public void IiaInsertsBeforeJealousy_WithoutReplayingTheExistingLastCard()
        {
            var cue = new LegacyQueueInsertionCue();
            LegacySkill first = Technique(503), jealousy = Technique(506), added = Technique(504);
            cue.Refresh(new[] { first, jealousy }, false, 0f);
            cue.Refresh(new[] { first, added, LegacyCommonActions.Breathe, jealousy }, true, 0f);
            Rest(cue.Pose(0));
            Start(cue.Pose(1));
            Start(cue.Pose(2));
            Rest(cue.Pose(3));
        }

        [Test]
        public void InsertingBeforeAnActiveLastCard_PreservesItsProgress()
        {
            var cue = EmptyBaseline();
            LegacySkill existing = Technique(506), added = Technique(504);
            cue.Refresh(new[] { existing }, true, 0f);
            cue.Refresh(new[] { existing }, true, .04f);
            var control = EmptyBaseline();
            control.Refresh(new[] { existing }, true, 0f);
            control.Refresh(new[] { existing }, true, .06f);
            cue.Refresh(new[] { added, existing }, true, .02f);
            Start(cue.Pose(0));
            EqualPose(cue.Pose(1), control.Pose(0));
        }

        [Test]
        public void CancelOrReturn_CompactsExistingBreaths_WithoutReplayingThem()
        {
            var cue = new LegacyQueueInsertionCue();
            LegacySkill first = Technique(1), second = Technique(3), replacement = Technique(10);
            LegacySkill breath = LegacyCommonActions.Breathe;
            cue.Refresh(new[] { first, breath, second, breath }, false, 0f);
            cue.Refresh(new[] { first, breath, second, breath, breath }, true, 0f);
            cue.Refresh(new[] { first, breath, second, breath, breath }, true, .03f);
            var control = EmptyBaseline();
            control.Refresh(new[] { breath }, true, 0f);
            control.Refresh(new[] { breath }, true, .04f);
            cue.Refresh(new[] { breath, breath, breath, replacement }, true, .01f, true);
            Rest(cue.Pose(0));
            Rest(cue.Pose(1));
            EqualPose(cue.Pose(2), control.Pose(0));
            Start(cue.Pose(3));
        }

        [Test]
        public void AnIdenticalLookingReplacement_ReplaysTheLastTechnique_WithoutItsTrailingBreath()
        {
            var cue = new LegacyQueueInsertionCue();
            var queue = new[] { Technique(10), LegacyCommonActions.Breathe };
            cue.Refresh(queue, false, 0f);
            cue.Refresh(queue, true, .5f, true);
            Start(cue.Pose(0));
            Rest(cue.Pose(1));
            cue.Refresh(queue, true, .05f);
            Assert.That(cue.Pose(0).Height, Is.LessThan(.8f));
        }

        [Test]
        public void AReplayedTechnique_WithANewTrailingBreath_PreservesTheOldBreathBeforeIt()
        {
            var cue = new LegacyQueueInsertionCue();
            LegacySkill removal = Technique(10), breath = LegacyCommonActions.Breathe;
            cue.Refresh(new[] { removal, breath }, false, 0f);
            cue.Refresh(new[] { breath, removal, breath }, true, 0f, true);
            Rest(cue.Pose(0));
            Start(cue.Pose(1));
            Start(cue.Pose(2));
        }

        [Test]
        public void AnotherManualBreath_AnimatesOnlyTheAppendedBreath()
        {
            var cue = new LegacyQueueInsertionCue();
            LegacySkill breath = LegacyCommonActions.Breathe;
            cue.Refresh(new[] { breath, breath }, false, 0f);
            cue.Refresh(new[] { breath, breath, breath }, true, .2f);
            Rest(cue.Pose(0));
            Rest(cue.Pose(1));
            Start(cue.Pose(2));
        }

        [Test]
        public void TheQueueSnapshot_IsIndependentOfItsLiveReadOnlyWrapper()
        {
            var cue = new LegacyQueueInsertionCue();
            var live = new List<LegacySkill> { Technique(1) };
            IReadOnlyList<LegacySkill> view = live.AsReadOnly();
            cue.Refresh(view, false, 0f);
            live.Add(Technique(3));
            cue.Refresh(view, true, 0f);
            Rest(cue.Pose(0));
            Start(cue.Pose(1));
            Assert.That(live.Count, Is.EqualTo(2), "Presentation never removes or inserts a reservation.");
        }

        [Test]
        public void AReplacementObjectWithTheSameId_IsANewCard()
        {
            var cue = new LegacyQueueInsertionCue();
            cue.Refresh(new[] { Technique(1) }, false, 0f);
            cue.Refresh(new[] { Technique(1) }, true, 0f);
            Start(cue.Pose(0));
        }

        [Test]
        public void AHiddenOrResolvingBaseline_ImmediatelySettlesAnyRunningDrop()
        {
            var cue = EmptyBaseline();
            var queue = new[] { Technique(1) };
            cue.Refresh(queue, true, 0f);
            Start(cue.Pose(0));
            cue.Refresh(queue, false, 0f, true);
            Rest(cue.Pose(0));
            cue.Refresh(queue, true, 0f);
            Rest(cue.Pose(0));
        }

        [Test]
        public void Clear_ForgetsDrops_AndTheNextRefreshStartsWithAStillBaseline()
        {
            var cue = EmptyBaseline();
            var queue = new[] { Technique(1) };
            cue.Refresh(queue, true, 0f);
            cue.Clear();
            Rest(cue.Pose(0));
            cue.Refresh(queue, true, 0f);
            Rest(cue.Pose(0));
            cue.Refresh(null, true, 0f);
            Rest(cue.Pose(0));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidOrNegativeDelta_DoesNotAdvanceOrPoisonThePose(float delta)
        {
            var cue = EmptyBaseline();
            var queue = new[] { Technique(1) };
            cue.Refresh(queue, true, 0f);
            cue.Refresh(queue, true, delta);
            Start(cue.Pose(0));
        }

        [Test]
        public void MissingSlots_AreAlwaysAtRest()
        {
            var cue = EmptyBaseline();
            cue.Refresh(new LegacySkill[] { null, Technique(1) }, true, 0f);
            Rest(cue.Pose(-1));
            Rest(cue.Pose(0));
            Start(cue.Pose(1));
            Rest(cue.Pose(2));
        }

        [Test]
        public void BufferGrowth_PreservesExistingOccurrences_AndStartsOnlyNewOnes()
        {
            var cue = EmptyBaseline();
            LegacySkill skill = Technique(1);
            var queue = new List<LegacySkill>();
            for (int index = 0; index < 31; index++) queue.Add(skill);
            cue.Refresh(queue, true, 0f);
            cue.Refresh(queue, true, .04f);
            for (int index = 31; index < 40; index++) queue.Add(skill);
            cue.Refresh(queue, true, .01f);
            var control = EmptyBaseline();
            control.Refresh(new[] { skill }, true, 0f);
            control.Refresh(new[] { skill }, true, .05f);
            for (int index = 0; index < 31; index++) EqualPose(cue.Pose(index), control.Pose(0));
            for (int index = 31; index < 40; index++) Start(cue.Pose(index));
        }

        private static LegacyQueueInsertionCue EmptyBaseline()
        {
            var cue = new LegacyQueueInsertionCue();
            cue.Refresh(Array.Empty<LegacySkill>(), false, 0f);
            return cue;
        }

        private static LegacySkill Technique(int id)
            => new LegacySkill(id, "Skill " + id, 1, 1, 2, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");

        private static void Start(LegacyQueueInsertionPose pose)
        {
            Assert.That(pose.Height, Is.EqualTo(.8f).Within(Tolerance));
            Assert.That(pose.ScaleX, Is.EqualTo(.96f).Within(Tolerance));
            Assert.That(pose.ScaleY, Is.EqualTo(1.06f).Within(Tolerance));
        }

        private static void Rest(LegacyQueueInsertionPose pose) => EqualPose(pose, LegacyQueueInsertionPose.Rest);

        private static void EqualPose(LegacyQueueInsertionPose actual, LegacyQueueInsertionPose expected)
        {
            Assert.That(actual.Height, Is.EqualTo(expected.Height).Within(Tolerance));
            Assert.That(actual.ScaleX, Is.EqualTo(expected.ScaleX).Within(Tolerance));
            Assert.That(actual.ScaleY, Is.EqualTo(expected.ScaleY).Within(Tolerance));
        }
    }
}
