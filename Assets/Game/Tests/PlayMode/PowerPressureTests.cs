using System;
using NUnit.Framework;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class PowerPressureTests
    {
        [Test]
        public void BeginningUsesZero_WhileRetainingTheAlreadyRolledTarget()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(28);
            Assert.That(pressure.Target, Is.EqualTo(28));
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.Charge, Is.Zero);
            Assert.That(pressure.IsHolding, Is.True);
            Assert.That(pressure.IsVisible, Is.True);
            Assert.That(pressure.IsReleasing, Is.False);
            Assert.That(pressure.Release, Is.Zero);
            Assert.That(pressure.Stamp, Is.Zero);
        }

        [TestCase(1)]
        [TestCase(28)]
        [TestCase(int.MaxValue)]
        public void CompressionNeverDecreasesOrRevealsTheFinalValueEarly(int target)
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(target);
            int previous = 0;
            float previousCharge = 0f;
            for (int i = 0; i < 40; i++)
            {
                pressure.Advance(.01f);
                Assert.That(pressure.Value, Is.InRange(previous, target - 1));
                Assert.That(pressure.Charge, Is.InRange(previousCharge, 1f));
                Assert.That(pressure.IsReleasing, Is.False);
                previous = pressure.Value;
                previousCharge = pressure.Charge;
            }
            pressure.Advance(DuelPowerPressure.ChargeSeconds - .4f + .00001f);
            Assert.That(pressure.Value, Is.EqualTo(target));
            Assert.That(pressure.Charge, Is.EqualTo(1f));
        }

        [Test]
        public void ReleaseLocksTheFinalNumber_BeforeAttackHoldEnds()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(17);
            pressure.Advance(DuelPowerPressure.ChargeSeconds);
            Assert.That(pressure.Value, Is.EqualTo(17));
            Assert.That(pressure.IsHolding, Is.True, "The final number has a short readable lock before the attack.");
            Assert.That(pressure.IsReleasing, Is.True);
            Assert.That(pressure.Stamp, Is.EqualTo(1f).Within(.0001f));
            pressure.Advance(DuelPowerPressure.LockSeconds);
            Assert.That(pressure.IsHolding, Is.False, "Two exact phase-sized ticks must not add a frame at the boundary.");
            Assert.That(pressure.Value, Is.EqualTo(17));
            Assert.That(pressure.IsReleasing, Is.True, "The steam tail can overlap the attacking animation.");
        }

        [Test]
        public void WholeAndSplitRealTimeBudgetsReleaseAtTheSameBoundary()
        {
            var whole = new DuelPowerPressure();
            var split = new DuelPowerPressure();
            whole.Begin(24); split.Begin(24);
            whole.Advance(DuelPowerPressure.HoldSeconds);
            for (int i = 0; i < 57; i++) split.Advance(.01f);
            Assert.That(whole.IsHolding, Is.False);
            Assert.That(split.IsHolding, Is.False);
            Assert.That(split.Value, Is.EqualTo(whole.Value));
            Assert.That(split.Release, Is.EqualTo(whole.Release).Within(.00001f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void PressureUsesElapsedRealTimeRatherThanFrameCount(int fps)
        {
            var whole = new DuelPowerPressure();
            var split = new DuelPowerPressure();
            whole.Begin(28); split.Begin(28);
            whole.Advance(.3f);
            for (int i = 0; i < fps * 3 / 10; i++) split.Advance(1f / fps);
            Assert.That(split.Value, Is.EqualTo(whole.Value));
            Assert.That(split.Charge, Is.EqualTo(whole.Charge).Within(.00001f));
            Assert.That(split.IsHolding, Is.EqualTo(whole.IsHolding));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void PauseAndInvalidDeltaNeverAdvanceOrCorruptPressure(float delta)
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(20);
            pressure.Advance(.1f);
            int value = pressure.Value;
            float charge = pressure.Charge;
            pressure.Advance(delta);
            Assert.That(pressure.Value, Is.EqualTo(value));
            Assert.That(pressure.Charge, Is.EqualTo(charge));
            Assert.That(pressure.IsHolding, Is.True);
        }

        [TestCase(0)]
        [TestCase(-17)]
        public void NonPositiveTargetsRemainZeroWithoutInvalidGaugeValues(int target)
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(target);
            pressure.Advance(.2f);
            Assert.That(pressure.Target, Is.Zero);
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.Charge, Is.InRange(0f, 1f));
            pressure.Advance(1f);
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.IsHolding, Is.False);
        }

        [Test]
        public void SteamFinishesWithoutDroppingTheFinalNumber()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(17);
            pressure.Advance(DuelPowerPressure.ChargeSeconds + DuelPowerPressure.SteamSeconds);
            Assert.That(pressure.IsVisible, Is.False);
            Assert.That(pressure.IsReleasing, Is.False);
            Assert.That(pressure.IsHolding, Is.False);
            Assert.That(pressure.HasValue, Is.True);
            Assert.That(pressure.Value, Is.EqualTo(17));
            Assert.That(pressure.Charge, Is.EqualTo(1f));
            Assert.That(pressure.Release, Is.EqualTo(1f).Within(.00001f));
        }

        [Test]
        public void ClearDropsEveryTransientState_AndBeginCanStartANewNumber()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(28);
            pressure.Advance(.5f);
            pressure.Clear();
            Assert.That(pressure.HasValue, Is.False);
            Assert.That(pressure.IsVisible || pressure.IsHolding || pressure.IsReleasing, Is.False);
            Assert.That(pressure.Target, Is.Zero);
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.Charge + pressure.Release + pressure.Stamp, Is.Zero);
            pressure.Advance(.1f);
            Assert.That(pressure.HasValue, Is.False);
            pressure.Begin(5);
            Assert.That(pressure.Target, Is.EqualTo(5));
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.IsHolding, Is.True);
        }

        [Test]
        public void BeginningANewValueDiscardsThePreviousSteamTail()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(28);
            pressure.Advance(.6f);
            pressure.Begin(5);
            Assert.That(pressure.IsReleasing, Is.False);
            Assert.That(pressure.Release, Is.Zero);
            Assert.That(pressure.Stamp, Is.Zero);
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.Target, Is.EqualTo(5));
        }

        [Test]
        public void AnOversizedFiniteFrameCompletesSafelyAndStaysCompleted()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(int.MaxValue);
            pressure.Advance(float.MaxValue);
            pressure.Advance(float.MaxValue);
            Assert.That(pressure.Value, Is.EqualTo(int.MaxValue));
            Assert.That(pressure.IsHolding || pressure.IsVisible || pressure.IsReleasing, Is.False);
            Assert.That(pressure.Charge, Is.EqualTo(1f));
            Assert.That(pressure.Release, Is.EqualTo(1f));
        }
    }
}
