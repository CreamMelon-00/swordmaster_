using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Tests
{
    public sealed class LegacyReengageMotionTests
    {
        [Test]
        public void FirstApproachDoesNotShowSpeedLines()
        {
            var motion = new LegacyReengageMotion();
            Assert.That(motion.Sample(-8f, 8f, .02f, true), Is.Zero);
            Assert.That(motion.Sample(-7f, 7f, .02f, true), Is.Zero);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void FastClosingAfterContactWorksForEitherAttacker(int direction)
        {
            var motion = new LegacyReengageMotion();
            motion.NotifyContact();
            motion.Sample(-4f, 4f, .02f, true);
            float strength = direction > 0 ? motion.Sample(-3f, 4f, .02f, true) : motion.Sample(-4f, 3f, .02f, true);
            Assert.That(strength, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(motion.Direction, Is.EqualTo(direction));
        }

        [TestCase(-4f, 4f, true)]
        [TestCase(-5f, 5f, true)]
        [TestCase(-3f, 4f, false)]
        [TestCase(-3.9f, 4f, true)]
        [TestCase(-3f, 5f, true)]
        public void StationaryRetreatIneligibleSlowAndCommonTranslationStayQuiet(float playerX, float enemyX, bool eligible)
        {
            var motion = new LegacyReengageMotion();
            motion.NotifyContact();
            motion.Sample(-4f, 4f, .02f, true);
            Assert.That(motion.Sample(playerX, enemyX, .02f, eligible), Is.Zero);
            Assert.That(motion.Direction, Is.Zero);
        }

        [Test]
        public void RealMovementSpeedIsIndependentOfFramePartition()
        {
            var first = new LegacyReengageMotion();
            var second = new LegacyReengageMotion();
            first.NotifyContact(); second.NotifyContact();
            first.Sample(-4f, 4f, .02f, true); second.Sample(-4f, 4f, .01f, true);
            Assert.That(first.Sample(-3f, 4f, .02f, true),
                Is.EqualTo(second.Sample(-3.5f, 4f, .01f, true)).Within(.00001f));
        }

        [Test]
        public void ClearRetainsContactButNewBattleResetForgetsIt()
        {
            var motion = new LegacyReengageMotion();
            motion.NotifyContact();
            motion.Clear();
            Assert.That(motion.Sample(-4f, 4f, .02f, true), Is.Zero);
            Assert.That(motion.Sample(-3f, 4f, .02f, true), Is.GreaterThan(0f));
            motion.Reset();
            motion.Sample(-4f, 4f, .02f, true);
            Assert.That(motion.Sample(-3f, 4f, .02f, true), Is.Zero);
        }

        [Test]
        public void TeleportAndInvalidClockDoNotLeaveAnOldMovementSample()
        {
            var motion = new LegacyReengageMotion();
            motion.NotifyContact();
            motion.Sample(-20f, 20f, .02f, true);
            Assert.That(motion.Sample(-3f, 3f, .02f, true), Is.Zero);
            foreach (float delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.That(motion.Sample(-2f, 3f, delta, true), Is.Zero);
                Assert.That(motion.Sample(-2f, 3f, .02f, true), Is.Zero);
            }
            Assert.That(motion.Sample(float.NaN, 3f, .02f, true), Is.Zero);
            Assert.That(motion.Sample(-2f, 3f, .02f, true), Is.Zero);
        }
    }
}
