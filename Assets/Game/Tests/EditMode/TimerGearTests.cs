using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Tests
{
    public sealed class TimerGearTests
    {
        [Test]
        public void Rotation_KeepsAMechanicalIdle_ThenAcceleratesTowardTheDeadline()
        {
            Assert.That(LegacyTimerGear.Urgency(10f, 10f), Is.Zero);
            Assert.That(LegacyTimerGear.DegreesPerSecond(10f, 10f),
                Is.EqualTo(LegacyTimerGear.BaseDegreesPerSecond));
            Assert.That(LegacyTimerGear.Urgency(3f, 10f), Is.Zero, "The danger boundary itself is still calm.");
            Assert.That(LegacyTimerGear.DegreesPerSecond(1.5f, 10f),
                Is.GreaterThan(LegacyTimerGear.BaseDegreesPerSecond));
            Assert.That(LegacyTimerGear.DegreesPerSecond(0f, 10f),
                Is.EqualTo(LegacyTimerGear.MaximumDegreesPerSecond));
        }

        [Test]
        public void Steam_BeginsInDanger_BreathesFasterInCriticalTime_AndMarksThresholdCrossingsOnce()
        {
            Assert.That(LegacyTimerGear.VentInterval(4f, 10f), Is.EqualTo(float.PositiveInfinity));
            Assert.That(LegacyTimerGear.VentInterval(.5f, 10f),
                Is.LessThan(LegacyTimerGear.VentInterval(2.5f, 10f)));
            Assert.That(LegacyTimerGear.Crossed(3.1f, 2.9f, 10f, LegacyTimerGear.DangerShare), Is.True);
            Assert.That(LegacyTimerGear.Crossed(2.9f, 2.8f, 10f, LegacyTimerGear.DangerShare), Is.False);
            Assert.That(LegacyTimerGear.Crossed(1.1f, .9f, 10f, LegacyTimerGear.CriticalShare), Is.True);
        }

        [Test]
        public void InvalidClockValues_StayCalm()
        {
            Assert.That(LegacyTimerGear.Urgency(float.NaN, 10f), Is.Zero);
            Assert.That(LegacyTimerGear.Urgency(0f, 0f), Is.Zero);
            Assert.That(LegacyTimerGear.Crossed(1f, 0f, float.PositiveInfinity, .3f), Is.False);
        }
    }
}
