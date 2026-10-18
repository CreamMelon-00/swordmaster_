using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class ContactCameraTests
    {
        [Test]
        public void AwaitingTheFirstImpactCannotExpireByTimeAlone()
        {
            var window = new LegacyContactReadWindow();
            Assert.That(window.IsReading, Is.False);
            window.AwaitImpact(); window.Advance(10d); window.FrameShown();
            Assert.That(window.IsReading, Is.True);
            window.ReleasePending();
            Assert.That(window.IsReading, Is.False);
        }

        [Test]
        public void ALongFrameStillShowsTheContactBeforeAllowingACloseup()
        {
            var window = new LegacyContactReadWindow();
            window.ObserveImpact(); window.Advance(2d);
            Assert.That(window.IsReading, Is.True);
            window.FrameShown();
            Assert.That(window.IsReading, Is.False);
        }

        [Test]
        public void SlotCompletionDoesNotDiscardTheResultAndRepeatedHitsRefreshRatherThanStack()
        {
            var window = new LegacyContactReadWindow();
            window.ObserveImpact(); window.ReleasePending(); window.Advance(.03); window.FrameShown();
            Assert.That(window.IsReading, Is.True);
            window.ObserveImpact(); window.ObserveImpact(); window.Advance(.15); window.FrameShown();
            Assert.That(window.IsReading, Is.False);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void PausedOrInvalidTimeDoesNotConsumeTheReadingWindow(double delta)
        {
            var window = new LegacyContactReadWindow();
            window.ObserveImpact(); window.FrameShown(); window.Advance(delta); window.Advance(.10);
            Assert.That(window.IsReading, Is.True);
            window.Advance(.05);
            Assert.That(window.IsReading, Is.False);
        }

        [Test]
        public void ANewSlotAndResetDoNotKeepAnOldResultWindow()
        {
            var window = new LegacyContactReadWindow();
            window.ObserveImpact(); window.AwaitImpact(); window.ReleasePending();
            Assert.That(window.IsReading, Is.False);
            window.ObserveImpact(); window.Clear();
            Assert.That(window.IsReading, Is.False);
        }

        [Test]
        public void SafeWideFramingIsUnchangedAndRepeatedFitDoesNotDrift()
        {
            LegacyContactFrame frame = LegacyContactCamera.Fit(1.2, -.3, 8d, 16d / 9d, -4d, 5d, -1d, 3d, .1, 7d);
            Assert.That((frame.X, frame.Y, frame.Size), Is.EqualTo((1.2, -.3, 8d)));
            LegacyContactFrame next = LegacyContactCamera.Fit(frame.X, frame.Y, frame.Size, 16d / 9d, -4d, 5d, -1d, 3d, .1, 7d);
            Assert.That((next.X, next.Y, next.Size), Is.EqualTo((frame.X, frame.Y, frame.Size)));
        }

        [TestCase(16d / 9d, 0d, 0d)]
        [TestCase(1d, .35d, 12d)]
        [TestCase(.5d, .4d, -19d)]
        [TestCase(2.4d, .2d, 90d)]
        public void BothBodiesAndFeetFitAboveTheHudEvenWithRoll(double aspect, double hud, double roll)
        {
            LegacyContactFrame frame = LegacyContactCamera.Fit(6d, 2d, 1d, aspect, -5d, 7d, -3d, 2d, hud, roll);
            Assert.That(frame.Size, Is.GreaterThanOrEqualTo(1d));
            AssertVisible(frame, aspect, -5d, 7d, -3d, 2d, hud, roll);
            LegacyContactFrame next = LegacyContactCamera.Fit(frame.X, frame.Y, frame.Size, aspect, -5d, 7d, -3d, 2d, hud, roll);
            Assert.That(next.X, Is.EqualTo(frame.X).Within(1e-10));
            Assert.That(next.Y, Is.EqualTo(frame.Y).Within(1e-10));
            Assert.That(next.Size, Is.EqualTo(frame.Size));
        }

        [Test]
        public void MirroringTheViewMirrorsTheFraming()
        {
            LegacyContactFrame a = LegacyContactCamera.Fit(7d, 3d, 1d, 1.4d, -6d, 4d, -1d, 4d, .3d, 23d);
            LegacyContactFrame b = LegacyContactCamera.Fit(-7d, 3d, 1d, 1.4d, -4d, 6d, -1d, 4d, .3d, -23d);
            Assert.That(b.X, Is.EqualTo(-a.X).Within(1e-10));
            Assert.That(b.Y, Is.EqualTo(a.Y).Within(1e-10));
            Assert.That(b.Size, Is.EqualTo(a.Size).Within(1e-10));
        }

        [Test]
        public void InvalidInputsAndReversedBoundsStayFinite()
        {
            LegacyContactFrame a = LegacyContactCamera.Fit(double.NaN, double.PositiveInfinity, -1d, 0d,
                double.NaN, double.PositiveInfinity, double.NaN, double.NegativeInfinity, double.NaN, double.NaN);
            Assert.That((a.X, a.Y, a.Size), Is.EqualTo((0d, 0d, 5d)));
            LegacyContactFrame normal = LegacyContactCamera.Fit(4d, 2d, 1d, 1.8d, -3d, 5d, -2d, 1d);
            LegacyContactFrame reversed = LegacyContactCamera.Fit(4d, 2d, 1d, 1.8d, 5d, -3d, 1d, -2d);
            Assert.That((reversed.X, reversed.Y, reversed.Size), Is.EqualTo((normal.X, normal.Y, normal.Size)));
            LegacyContactFrame huge = LegacyContactCamera.Fit(double.MaxValue, -double.MaxValue, double.MaxValue,
                double.Epsilon, -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 100d, 19d);
            Assert.That(double.IsInfinity(huge.Size) || double.IsNaN(huge.Size), Is.False);
        }

        private static void AssertVisible(LegacyContactFrame frame, double aspect, double minX, double maxX,
            double minY, double maxY, double hud, double roll)
        {
            double angle = roll * Math.PI / 180d, cosine = Math.Cos(angle), sine = Math.Sin(angle);
            foreach (double x in new[] { minX, maxX })
            foreach (double y in new[] { minY, maxY })
            {
                double dx = x - frame.X, dy = y - frame.Y;
                double px = .5 + (cosine * dx + sine * dy) / (2d * frame.Size * aspect);
                double py = .5 + (-sine * dx + cosine * dy) / (2d * frame.Size);
                Assert.That(px, Is.InRange(.05 - 1e-9, .95 + 1e-9));
                Assert.That(py, Is.InRange(hud + .03 - 1e-9, .95 + 1e-9));
            }
        }
    }
}
