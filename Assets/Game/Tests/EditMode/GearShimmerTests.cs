using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>엘리사's eye over the enemy's revealed queue (<see cref="LegacyGearShimmer"/>): the gear comes up, turns one
    /// tooth and is gone at the end; the glint crosses the row from its first card outward inside its window; and the gear
    /// drawn in code has its teeth, rim, spokes, hub and axle hole.</summary>
    public sealed class GearShimmerTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void TheGear_ComesUp_TurnsOneTooth_AndIsGoneAtTheEnd()
        {
            Assert.That(LegacyGearShimmer.GearAlpha(0f), Is.Zero, "Nothing at the reveal itself…");
            Assert.That(LegacyGearShimmer.GearAlpha(LegacyGearShimmer.RiseShare), Is.EqualTo(1f).Within(Tolerance), "…up quickly…");
            Assert.That(LegacyGearShimmer.GearAlpha(LegacyGearShimmer.FadeStart), Is.EqualTo(1f).Within(Tolerance), "…held…");
            float previous = 1f;
            for (float t = LegacyGearShimmer.FadeStart; t <= 1f; t += .05f)
            {
                float alpha = LegacyGearShimmer.GearAlpha(t);
                Assert.That(alpha, Is.LessThanOrEqualTo(previous + Tolerance), "…then only fading, at " + t);
                previous = alpha;
            }
            Assert.That(LegacyGearShimmer.GearAlpha(.98f), Is.LessThan(.01f));
            Assert.That(LegacyGearShimmer.GearAlpha(1f), Is.Zero, "Gone when the effect ends.");
            Assert.That(LegacyGearShimmer.GearAlpha(float.NaN), Is.Zero);

            Assert.That(LegacyGearShimmer.TurnDegrees, Is.EqualTo(36f).Within(Tolerance), "One tooth of ten.");
            Assert.That(LegacyGearShimmer.GearTurn(0f), Is.Zero);
            Assert.That(LegacyGearShimmer.GearTurn(1f), Is.EqualTo(LegacyGearShimmer.TurnDegrees).Within(Tolerance));
            Assert.That(LegacyGearShimmer.GearTurn(.5f), Is.GreaterThan(.8f * LegacyGearShimmer.TurnDegrees),
                "It turns most of the way while it shows, settling as it fades.");
            float turned = 0f;
            for (float t = 0f; t <= 1f; t += .05f)
            {
                float turn = LegacyGearShimmer.GearTurn(t);
                Assert.That(turn, Is.GreaterThanOrEqualTo(turned - Tolerance), "It never turns back.");
                turned = turn;
            }
            Assert.That(LegacyGearShimmer.GearTurn(3f), Is.EqualTo(LegacyGearShimmer.TurnDegrees).Within(Tolerance), "Never past a tooth.");

            Assert.That(LegacyGearShimmer.GearScale(0f), Is.EqualTo(LegacyGearShimmer.StartScale).Within(Tolerance));
            Assert.That(LegacyGearShimmer.GearScale(LegacyGearShimmer.SettleShare), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(LegacyGearShimmer.GearScale(1f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void TheGlint_CrossesTheRowFromItsFirstCardOutward_WithinItsWindow()
        {
            float start = LegacyGearShimmer.GlintStart, end = LegacyGearShimmer.GlintEnd, middle = (start + end) * .5f;
            Assert.That(start, Is.GreaterThan(0f).And.LessThan(end));
            Assert.That(end, Is.LessThan(1f), "It has crossed before the effect ends.");
            foreach (float outside in new[] { 0f, start * .5f, start, end, (end + 1f) * .5f, 1f })
                Assert.That(LegacyGearShimmer.GlintAlpha(outside), Is.Zero, "No glint outside its window, at " + outside);
            Assert.That(LegacyGearShimmer.GlintAlpha(middle), Is.EqualTo(1f).Within(Tolerance), "Brightest halfway across.");

            Assert.That(LegacyGearShimmer.GlintPosition(0f), Is.Zero, "It waits at the first card…");
            Assert.That(LegacyGearShimmer.GlintPosition(start), Is.Zero);
            Assert.That(LegacyGearShimmer.GlintPosition(middle), Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(LegacyGearShimmer.GlintPosition(end), Is.EqualTo(1f).Within(Tolerance), "…and has passed the last after.");
            Assert.That(LegacyGearShimmer.GlintPosition(1f), Is.EqualTo(1f).Within(Tolerance));
            float previous = 0f;
            for (float t = 0f; t <= 1f; t += .02f)
            {
                float position = LegacyGearShimmer.GlintPosition(t);
                Assert.That(position, Is.GreaterThanOrEqualTo(previous - Tolerance), "It only moves outward.");
                previous = position;
            }
        }

        [Test]
        public void Progress_RunsOverTheLength_AndAnEffectWithoutOneIsOver()
        {
            Assert.That(LegacyGearShimmer.Progress(0f, .55f), Is.Zero);
            Assert.That(LegacyGearShimmer.Progress(.275f, .55f), Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(LegacyGearShimmer.Progress(2f, .55f), Is.EqualTo(1f));
            Assert.That(LegacyGearShimmer.Progress(-1f, .55f), Is.Zero);
            Assert.That(LegacyGearShimmer.Progress(.1f, 0f), Is.EqualTo(1f));
            Assert.That(LegacyGearShimmer.Progress(.1f, float.PositiveInfinity), Is.EqualTo(1f));
        }

        [Test]
        public void TheGearShape_HasTeethOnARim_SpokesToAHub_AndAnAxleHole()
        {
            const float pixel = 2f / 128f;
            float pitch = (float)(Math.PI * 2.0 / LegacyGearShimmer.Teeth);
            // Tooth centres sit at half a pitch from the x axis; the gaps between them on the axis.
            Assert.That(Cover(.9f, pitch * .5f, pixel), Is.EqualTo(1f), "A tooth reaches past the rim…");
            Assert.That(Cover(.9f, 0f, pixel), Is.Zero, "…with a gap between teeth.");
            Assert.That(Cover(.9f, pitch * 3.5f, pixel), Is.EqualTo(1f), "Every tooth.");
            Assert.That(Cover(1f, pitch * .5f, pixel), Is.Zero, "Nothing past the tips.");
            Assert.That(Cover(.7f, 0f, pixel), Is.EqualTo(1f), "The rim is solid all round…");
            Assert.That(Cover(.7f, 1.3f, pixel), Is.EqualTo(1f));
            Assert.That(Cover(.45f, (float)(Math.PI / 2.0), pixel), Is.EqualTo(1f), "…a spoke runs up to it…");
            float betweenSpokes = (float)(Math.PI / 2.0 + Math.PI / LegacyGearShimmer.Spokes);
            Assert.That(Cover(.45f, betweenSpokes, pixel), Is.Zero, "…and between the spokes it is open.");
            Assert.That(Cover(.18f, betweenSpokes, pixel), Is.EqualTo(1f), "The hub is solid…");
            Assert.That(Cover(.05f, .4f, pixel), Is.Zero, "…round its axle hole.");
            for (float x = -1.2f; x <= 1.2f; x += .07f)
                for (float y = -1.2f; y <= 1.2f; y += .07f)
                {
                    float coverage = LegacyGearShimmer.GearCoverage(x, y, pixel);
                    Assert.That(coverage, Is.InRange(0f, 1f));
                }
            Assert.That(LegacyGearShimmer.GearCoverage(.9f, 0f, 0f), Is.Zero, "A zero pixel still draws a hard edge.");
        }

        private static float Cover(float radius, float angle, float pixel)
            => LegacyGearShimmer.GearCoverage(radius * (float)Math.Cos(angle), radius * (float)Math.Sin(angle), pixel);
    }
}
