using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class ClashAmbienceDuckTests
    {
        private const float Tolerance = .00001f;

        [Test]
        public void AClash_DipsQuicklyHoldsBrieflyAndRestoresItsOriginalLevel()
        {
            var duck = new LegacyClashAmbienceDuck();
            Assert.That(duck.Gain(.7f), Is.EqualTo(1f));
            duck.Trigger();
            Assert.That(duck.Gain(.7f), Is.EqualTo(1f), "No discontinuity when the impact starts.");
            duck.Tick(.01f);
            Assert.That(duck.Gain(.7f), Is.EqualTo(.65f).Within(Tolerance));
            duck.Tick(.01f);
            Assert.That(duck.Gain(.7f), Is.EqualTo(.3f).Within(Tolerance));
            duck.Tick(.05f);
            Assert.That(duck.Gain(.7f), Is.EqualTo(.3f).Within(Tolerance));
            duck.Tick(.075f);
            Assert.That(duck.Gain(.7f), Is.EqualTo(.65f).Within(Tolerance));
            duck.Tick(.076f);
            Assert.That(duck.Gain(.7f), Is.EqualTo(1f));
            Assert.That(duck.IsActive, Is.False);
        }

        [Test]
        public void VariableRealTimeSteps_ReachTheSameEnvelopeWithoutAnyBattleClock()
        {
            var whole = new LegacyClashAmbienceDuck();
            var divided = new LegacyClashAmbienceDuck();
            whole.Trigger();
            divided.Trigger();
            whole.Tick(.12f);
            for (int index = 0; index < 12; index++) divided.Tick(.01f);
            Assert.That(divided.Gain(.7f), Is.EqualTo(whole.Gain(.7f)).Within(Tolerance));
        }

        [Test]
        public void RepeatedClashes_RefreshOneBoundedDipAndRecoverAfterTheLastClash()
        {
            var duck = new LegacyClashAmbienceDuck();
            for (int index = 0; index < 100; index++)
            {
                float before = duck.Gain(.7f);
                duck.Trigger();
                Assert.That(duck.Gain(.7f), Is.EqualTo(before), "A refresh never jumps or compounds the attenuation.");
                duck.Tick(.01f);
                Assert.That(duck.Gain(.7f), Is.InRange(.3f - Tolerance, 1f));
            }
            duck.Tick(LegacyClashAmbienceDuck.Duration + .01f);
            Assert.That(duck.Gain(.7f), Is.EqualTo(1f));
            Assert.That(duck.IsActive, Is.False);
        }

        [Test]
        public void AClashDuringRecovery_ContinuesFromTheCurrentLevel()
        {
            var duck = new LegacyClashAmbienceDuck();
            duck.Trigger();
            duck.Tick(.145f);
            float before = duck.Gain(.7f);
            duck.Trigger();
            Assert.That(duck.Gain(.7f), Is.EqualTo(before));
            duck.Tick(.01f);
            Assert.That(duck.Gain(.7f), Is.LessThan(before));
            duck.Tick(.01f);
            Assert.That(duck.Gain(.7f), Is.EqualTo(.3f).Within(Tolerance));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTime_DoesNotPoisonOrSkipTheEnvelope(float delta)
        {
            var duck = new LegacyClashAmbienceDuck();
            duck.Trigger();
            duck.Tick(.01f);
            float before = duck.Gain(.7f);
            duck.Tick(delta);
            Assert.That(duck.Gain(.7f), Is.EqualTo(before));
            duck.Tick(.3f);
            Assert.That(duck.Gain(.7f), Is.EqualTo(1f));
        }

        [Test]
        public void ClearAndZeroStrength_LeaveTheOriginalBedLevel()
        {
            var duck = new LegacyClashAmbienceDuck();
            duck.Trigger();
            duck.Tick(.02f);
            Assert.That(duck.Gain(0f), Is.EqualTo(1f));
            Assert.That(duck.Gain(float.NaN), Is.EqualTo(1f));
            Assert.That(duck.Gain(2f), Is.EqualTo(0f));
            Assert.That(duck.Gain(-1f), Is.EqualTo(1f));
            duck.Clear();
            Assert.That(duck.Gain(.7f), Is.EqualTo(1f));
            Assert.That(duck.IsActive, Is.False);
            duck.Trigger();
            duck.Tick(.01f);
            Assert.That(duck.Gain(.7f), Is.EqualTo(.65f).Within(Tolerance), "Reuse starts from silence-free full gain.");
        }
    }
}
