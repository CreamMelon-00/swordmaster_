using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class AttackRhythmTests
    {
        private const float Tolerance = .00001f;

        [TestCase(0f, 0f)]
        [TestCase(.16f, .28f)]
        [TestCase(.38f, .34f)]
        [TestCase(.5f, .5f)]
        [TestCase(.56f, .53f)]
        [TestCase(.74f, .86f)]
        [TestCase(1f, 1f)]
        public void FullStrength_ReachesTheAuthoredAnchors(float phase, float pose)
            => Assert.That(LegacyAttackRhythm.PosePhase(phase, LegacyAttackRhythm.DefaultStrength),
                Is.EqualTo(pose).Within(Tolerance));

        [TestCase(0f)]
        [TestCase(.4f)]
        [TestCase(1f)]
        public void Sampling_IsBoundedMonotonic_AndCannotCrossContactEarly(float strength)
        {
            float previous = 0f;
            for (int index = 0; index <= 1000; index++)
            {
                float phase = index / 1000f;
                float pose = LegacyAttackRhythm.PosePhase(phase, strength);
                Assert.That(pose, Is.InRange(0f, 1f));
                Assert.That(pose, Is.GreaterThanOrEqualTo(previous));
                if (phase < .5f) Assert.That(pose, Is.LessThan(.5f));
                else if (phase > .5f) Assert.That(pose, Is.GreaterThan(.5f));
                previous = pose;
            }
            Assert.That(LegacyAttackRhythm.PosePhase(0f, strength), Is.EqualTo(0f));
            Assert.That(LegacyAttackRhythm.PosePhase(.5f, strength), Is.EqualTo(.5f));
            Assert.That(LegacyAttackRhythm.PosePhase(1f, strength), Is.EqualTo(1f));
        }

        [Test]
        public void SegmentJunctions_AreContinuous()
        {
            const float epsilon = .00001f;
            foreach (float strength in new[] { .4f, 1f })
            foreach (float phase in new[] { .16f, .38f, .5f, .56f, .74f })
            {
                float before = LegacyAttackRhythm.PosePhase(phase - epsilon, strength);
                float at = LegacyAttackRhythm.PosePhase(phase, strength);
                float after = LegacyAttackRhythm.PosePhase(phase + epsilon, strength);
                Assert.That(at - before, Is.InRange(0f, .000025f));
                Assert.That(after - at, Is.InRange(0f, .000025f));
            }
        }

        [Test]
        public void Rhythm_RaisesEarly_GathersBeforeContact_AndFollowsThroughQuickly()
        {
            float raised = LegacyAttackRhythm.PosePhase(.16f, 1f);
            float gathered = LegacyAttackRhythm.PosePhase(.38f, 1f);
            Assert.That(raised, Is.GreaterThan(.16f), "The weapon reaches its preparation pose early.");
            Assert.That(gathered, Is.LessThan(.38f), "The gathered pose waits before the stroke.");
            Assert.That(gathered - raised, Is.LessThan(.38f - .16f), "The gather moves more slowly than the clock.");
            Assert.That(.5f - gathered, Is.GreaterThan(.5f - .38f), "The last stroke catches up into exact contact.");
            Assert.That(LegacyAttackRhythm.PosePhase(.56f, 1f), Is.LessThan(.56f), "Contact stays visible briefly.");
            float through = LegacyAttackRhythm.PosePhase(.74f, 1f);
            Assert.That(through, Is.GreaterThan(.74f), "The blade follows through ahead of the clock.");
            Assert.That(1f - through, Is.LessThan(1f - .74f), "The final pose settles for the remaining time.");
        }

        [Test]
        public void StrengthZero_IsLinear_AndPartialStrengthBlendsTheSameRhythm()
        {
            for (int index = 0; index <= 1000; index++)
            {
                float phase = index / 1000f;
                Assert.That(LegacyAttackRhythm.PosePhase(phase, 0f), Is.EqualTo(phase));
            }
            Assert.That(LegacyAttackRhythm.PosePhase(.38f, .5f), Is.EqualTo(.36f).Within(Tolerance));
            Assert.That(LegacyAttackRhythm.PosePhase(.74f, .5f), Is.EqualTo(.8f).Within(Tolerance));
            Assert.That(LegacyAttackRhythm.PosePhase(.38f, -10f), Is.EqualTo(.38f));
            Assert.That(LegacyAttackRhythm.PosePhase(.38f, 10f),
                Is.EqualTo(LegacyAttackRhythm.PosePhase(.38f, 1f)));
        }

        [Test]
        public void InvalidInputs_UseFiniteDefaults_AndPhasesClampAtTheEndpoints()
        {
            foreach (float strength in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
                Assert.That(LegacyAttackRhythm.PosePhase(.74f, strength),
                    Is.EqualTo(LegacyAttackRhythm.PosePhase(.74f, LegacyAttackRhythm.DefaultStrength)));
            foreach (float phase in new[] { float.NaN, float.NegativeInfinity, -10f })
                Assert.That(LegacyAttackRhythm.PosePhase(phase, 1f), Is.EqualTo(0f));
            foreach (float phase in new[] { float.PositiveInfinity, 10f })
                Assert.That(LegacyAttackRhythm.PosePhase(phase, 1f), Is.EqualTo(1f));
        }

        [TestCase(0f, 0f)]
        [TestCase(.12f, -.1666667f)]
        [TestCase(.34f, -.3f)]
        [TestCase(.46f, .7f)]
        [TestCase(.5f, 1f)]
        [TestCase(.58f, 1f)]
        [TestCase(.72f, .3666667f)]
        [TestCase(1f, 0f)]
        public void Lunge_ReachesTheAuthoredPullbackContactAndRecoveryAnchors(float phase, float share)
            => Assert.That(LegacyAttackRhythm.LungeShare(phase), Is.EqualTo(share).Within(Tolerance));

        [Test]
        public void Lunge_PullsBack_StrikesQuickly_HoldsContact_AndRecoversToNeutral()
        {
            Assert.That(LegacyAttackRhythm.DefaultLungeDistance, Is.EqualTo(.6f));
            Assert.That(LegacyAttackRhythm.LungeShare(.08f), Is.LessThan(0f));
            Assert.That(LegacyAttackRhythm.LungeShare(.34f), Is.LessThan(LegacyAttackRhythm.LungeShare(.12f)));
            Assert.That(LegacyAttackRhythm.LungeShare(.4f), Is.GreaterThan(0f));
            Assert.That(LegacyAttackRhythm.LungeShare(.46f) - LegacyAttackRhythm.LungeShare(.34f),
                Is.GreaterThan(.9f), "The last windup rapidly turns the pullback into a visible forward strike.");
            Assert.That(LegacyAttackRhythm.LungeShare(.5f), Is.EqualTo(1f));
            Assert.That(LegacyAttackRhythm.LungeShare(.54f), Is.EqualTo(1f));
            Assert.That(LegacyAttackRhythm.LungeShare(.58f), Is.EqualTo(1f));
            Assert.That(LegacyAttackRhythm.LungeShare(.72f), Is.LessThan(.4f));
            Assert.That(LegacyAttackRhythm.LungeShare(.9f), Is.InRange(0f, .2f));
            Assert.That(LegacyAttackRhythm.LungeShare(1f), Is.Zero);
        }

        [Test]
        public void Lunge_SegmentsAreContinuous_AndAllSamplesRemainWithinTheVisibleRange()
        {
            const float epsilon = .00001f;
            foreach (float phase in new[] { .12f, .34f, .46f, .5f, .58f, .72f })
            {
                float at = LegacyAttackRhythm.LungeShare(phase);
                Assert.That(LegacyAttackRhythm.LungeShare(phase - epsilon), Is.EqualTo(at).Within(.0001f));
                Assert.That(LegacyAttackRhythm.LungeShare(phase + epsilon), Is.EqualTo(at).Within(.0001f));
            }
            for (int index = 0; index <= 1000; index++)
                Assert.That(LegacyAttackRhythm.LungeShare(index / 1000f), Is.InRange(-.3f, 1f));
        }

        [Test]
        public void Lunge_InvalidAndOutOfRangePhasesReturnNeutralAtTheClampedEndpoints()
        {
            foreach (float phase in new[] { float.NaN, float.NegativeInfinity, -10f, float.PositiveInfinity, 10f })
                Assert.That(LegacyAttackRhythm.LungeShare(phase), Is.Zero);
        }
    }
}
