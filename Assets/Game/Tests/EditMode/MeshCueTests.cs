using System;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>맞물림's cues (<see cref="LegacyMeshCue"/>): the pair of gears pops up, bites, turns two teeth against each
    /// other with its teeth meshed and fades; sparks fly out from the seam and are gone before the gears; a longer chain
    /// gives a bigger burst, more sparks and a higher sound; a meshed icon's mark reads its bonus and pops.</summary>
    public sealed class MeshCueTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void TheGears_PopUp_Bite_TurnTwoTeeth_AndFade()
        {
            Assert.That(LegacyMeshCue.GearScale(0f), Is.EqualTo(LegacyMeshCue.PopScale).Within(Tolerance), "Small as they appear…");
            Assert.That(LegacyMeshCue.GearScale(LegacyMeshCue.OvershootShare),
                Is.EqualTo(LegacyMeshCue.OvershootScale).Within(Tolerance), "…past their size as they bite…");
            Assert.That(LegacyMeshCue.OvershootScale, Is.GreaterThan(1f));
            Assert.That(LegacyMeshCue.GearScale(LegacyMeshCue.SettleShare), Is.EqualTo(1f).Within(Tolerance), "…then settled…");
            Assert.That(LegacyMeshCue.GearScale(1f), Is.EqualTo(1f).Within(Tolerance));

            Assert.That(LegacyMeshCue.GearAlpha(0f), Is.Zero);
            Assert.That(LegacyMeshCue.GearAlpha(LegacyMeshCue.RiseShare), Is.EqualTo(1f).Within(Tolerance), "…whole at once…");
            Assert.That(LegacyMeshCue.GearAlpha(LegacyMeshCue.FadeStart), Is.EqualTo(1f).Within(Tolerance), "…held…");
            float previous = 1f;
            for (float t = LegacyMeshCue.FadeStart; t <= 1f; t += .05f)
            {
                float alpha = LegacyMeshCue.GearAlpha(t);
                Assert.That(alpha, Is.LessThanOrEqualTo(previous + Tolerance), "…then only fading, at " + t);
                previous = alpha;
            }
            Assert.That(LegacyMeshCue.GearAlpha(1f), Is.Zero, "Gone when the effect ends.");
            Assert.That(LegacyMeshCue.GearAlpha(float.NaN), Is.Zero);

            Assert.That(LegacyMeshCue.GearGlow(0f), Is.EqualTo(1f).Within(Tolerance), "White as the teeth bite…");
            Assert.That(LegacyMeshCue.GearGlow(LegacyMeshCue.GlowShare), Is.Zero, "…brass again soon after.");
            Assert.That(LegacyMeshCue.GlowShare, Is.LessThan(LegacyMeshCue.FadeStart));

            Assert.That(LegacyMeshCue.GearTurn(0f), Is.Zero);
            Assert.That(LegacyMeshCue.GearTurn(1f), Is.EqualTo(LegacyMeshCue.TurnDegrees).Within(Tolerance));
            Assert.That(LegacyMeshCue.TurnDegrees, Is.EqualTo(2f * 360f / LegacyMeshCue.GearTeeth).Within(Tolerance),
                "Two teeth, so the pair ends looking as it began, a notch on.");
            float turned = 0f;
            for (float t = 0f; t <= 1f; t += .05f)
            {
                float turn = LegacyMeshCue.GearTurn(t);
                Assert.That(turn, Is.GreaterThanOrEqualTo(turned - Tolerance), "It never turns back.");
                turned = turn;
            }
            Assert.That(LegacyMeshCue.GearTurn(.5f), Is.GreaterThan(.8f * LegacyMeshCue.TurnDegrees), "Most of the turn as they bite.");
        }

        [Test]
        public void ThePair_StaysMeshed_TeethInTheOthersGaps()
        {
            // Centre to centre, in gear sizes (diameters): past the roots, short of the tips, so only the teeth overlap.
            float centres = 2f * LegacyMeshCue.MeshOffset;
            Assert.That(centres, Is.GreaterThan(LegacyGearShimmer.RootRadius).And.LessThan(LegacyGearShimmer.TipRadius));
            Assert.That(LegacyMeshCue.LowerPhase, Is.EqualTo(LegacyMeshCue.ToothDegrees * .5f).Within(Tolerance),
                "Half a tooth on: a tooth faces a gap.");
            // A tooth of the upper gear faces the seam (straight down) where the lower gear, turned half a tooth, has a gap
            // facing it (straight up), and both turn by the same amount the other way, so that stays so.
            float pixel = .01f;
            int teeth = LegacyMeshCue.GearTeeth;
            float tip = LegacyGearShimmer.TipRadius - .02f;
            foreach (float turn in new[] { 0f, 10f, 22.5f, 45f, 80f })
            {
                float upperAngle = -90f + turn, lowerAngle = 90f - LegacyMeshCue.LowerPhase - turn;
                float upper = Coverage(upperAngle, tip, pixel, teeth), lower = Coverage(lowerAngle, tip, pixel, teeth);
                Assert.That(upper > .5f && lower > .5f, Is.False, "Two tips never meet at the seam, turned " + turn);
            }
        }

        [TestCase(1, 0)]
        [TestCase(2, 10)]
        [TestCase(3, 14)]
        [TestCase(4, 18)]
        [TestCase(6, 26)]
        [TestCase(50, LegacyMeshCue.MaximumSparks)]
        public void ALongerChain_ThrowsMoreSparks(int chainLength, int sparks)
            => Assert.That(LegacyMeshCue.SparkCount(10, chainLength), Is.EqualTo(sparks));

        [Test]
        public void ALongerChain_GivesABiggerBurst_AndAHigherSound()
        {
            Assert.That(LegacyMeshCue.SparkCount(0, 3), Is.Zero, "Sparks tuned off.");
            Assert.That(LegacyMeshCue.SparkCount(10, int.MaxValue), Is.EqualTo(LegacyMeshCue.MaximumSparks));
            Assert.That(LegacyMeshCue.BurstScale(2), Is.EqualTo(1f));
            Assert.That(LegacyMeshCue.Pitch(2), Is.EqualTo(1f));
            float scale = 1f, pitch = 1f;
            for (int length = 3; length <= 8; length++)
            {
                Assert.That(LegacyMeshCue.BurstScale(length), Is.GreaterThanOrEqualTo(scale), "Bigger, chain " + length);
                Assert.That(LegacyMeshCue.Pitch(length), Is.GreaterThanOrEqualTo(pitch), "Higher, chain " + length);
                scale = LegacyMeshCue.BurstScale(length);
                pitch = LegacyMeshCue.Pitch(length);
            }
            Assert.That(LegacyMeshCue.BurstScale(3), Is.GreaterThan(1f), "Three skills already read bigger…");
            Assert.That(LegacyMeshCue.BurstScale(100), Is.EqualTo(LegacyMeshCue.MaximumBurstScale), "…but only slightly, never huge.");
            Assert.That(LegacyMeshCue.Pitch(100), Is.EqualTo(LegacyMeshCue.MaximumPitch));
        }

        [Test]
        public void TheSparks_FlyOutFromTheSeam_Scattered_AndAreGoneBeforeTheGears()
        {
            const int count = 12;
            var quarters = new HashSet<int>();
            var angles = new List<float>();
            for (int index = 0; index < count; index++)
            {
                Assert.That(LegacyMeshCue.SparkX(index, 0f), Is.Zero, "From the seam…");
                Assert.That(LegacyMeshCue.SparkY(index, 0f), Is.Zero);
                float speed = LegacyMeshCue.SparkSpeed(index);
                Assert.That(speed, Is.InRange(.55f, 1f));
                Assert.That(LegacyMeshCue.SparkAngle(index), Is.EqualTo(LegacyMeshCue.SparkAngle(index)), "The same every time.");
                float angle = LegacyMeshCue.SparkAngle(index);
                Assert.That(angle, Is.InRange(0f, 360f));
                quarters.Add((int)(angle / 90f));
                angles.Add(angle);
                float reach = 0f;
                for (float t = 0f; t <= LegacyMeshCue.SparkEnd; t += .04f)
                {
                    float x = LegacyMeshCue.SparkX(index, t), y = LegacyMeshCue.SparkY(index, t) + LegacyMeshCue.SparkDrop *
                        LegacyMeshCue.SparkFlight(t) * LegacyMeshCue.SparkFlight(t);
                    float distance = (float)Math.Sqrt(x * x + y * y);
                    Assert.That(distance, Is.GreaterThanOrEqualTo(reach - Tolerance), "…ever outward along its line…");
                    reach = distance;
                }
                Assert.That(reach, Is.EqualTo(speed).Within(.01f), "…as far as its speed.");
                Assert.That(LegacyMeshCue.SparkY(index, 1f),
                    Is.EqualTo((float)Math.Sin(angle * Math.PI / 180.0) * speed - LegacyMeshCue.SparkDrop).Within(.001f),
                    "…falling a little on the way.");
            }
            Assert.That(quarters.Count, Is.EqualTo(4), "Every way out of the seam.");
            angles.Sort();
            for (int index = 1; index < angles.Count; index++)
                Assert.That(angles[index] - angles[index - 1], Is.GreaterThan(5f), "No two together.");

            foreach (float before in new[] { 0f, LegacyMeshCue.SparkStart })
                Assert.That(LegacyMeshCue.SparkAlpha(before), Is.Zero, "Nothing before they leave.");
            Assert.That(LegacyMeshCue.SparkAlpha(LegacyMeshCue.SparkStart + .02f), Is.GreaterThan(.8f), "Bright as they leave…");
            Assert.That(LegacyMeshCue.SparkAlpha(LegacyMeshCue.SparkEnd), Is.Zero, "…gone at the end of their flight…");
            Assert.That(LegacyMeshCue.GearAlpha(LegacyMeshCue.SparkEnd), Is.GreaterThan(0f), "…while the gears still show.");
            Assert.That(LegacyMeshCue.SparkAlpha(1f), Is.Zero);

            Assert.That(LegacyMeshCue.SparkTrail(0f), Is.Zero, "The streak never reaches before the burst…");
            Assert.That(LegacyMeshCue.SparkTrail(.5f), Is.EqualTo(.5f - LegacyMeshCue.TrailLag).Within(Tolerance), "…and trails its head.");
        }

        [Test]
        public void AMeshedIcon_ReadsItsBonus_AndPopsWhenItAppearsOrRises()
        {
            Assert.That(LegacyMeshCue.BonusLabel(40), Is.EqualTo("+40%"));
            Assert.That(LegacyMeshCue.BonusLabel(120), Is.EqualTo("+120%"));
            Assert.That(LegacyMeshCue.BonusLabel(0), Is.Empty, "Nothing for an icon that is not meshed.");
            Assert.That(LegacyMeshCue.MarkScale(LegacyMeshCue.MarkPopSeconds), Is.EqualTo(LegacyMeshCue.MarkPopScale).Within(Tolerance));
            Assert.That(LegacyMeshCue.MarkScale(0f), Is.EqualTo(1f));
            Assert.That(LegacyMeshCue.MarkScale(float.NaN), Is.EqualTo(1f));
            float previous = LegacyMeshCue.MarkPopScale;
            for (float remaining = LegacyMeshCue.MarkPopSeconds; remaining >= 0f; remaining -= .02f)
            {
                float scale = LegacyMeshCue.MarkScale(remaining);
                Assert.That(scale, Is.LessThanOrEqualTo(previous + Tolerance), "It settles, never growing again.");
                previous = scale;
            }
        }

        [Test]
        public void Progress_RunsFromStartToEnd_AndAnEffectWithNoLengthIsOver()
        {
            Assert.That(LegacyMeshCue.Progress(0f, .6f), Is.Zero);
            Assert.That(LegacyMeshCue.Progress(.3f, .6f), Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(LegacyMeshCue.Progress(2f, .6f), Is.EqualTo(1f));
            Assert.That(LegacyMeshCue.Progress(0f, 0f), Is.EqualTo(1f));
            Assert.That(LegacyMeshCue.Progress(0f, float.PositiveInfinity), Is.EqualTo(1f));
        }

        // How much of a gear's drawing covers the point at this angle and radius (units of its outer radius).
        private static float Coverage(float degrees, float radius, float pixel, int teeth)
        {
            double angle = degrees * Math.PI / 180.0;
            return LegacyGearShimmer.GearCoverage((float)(radius * Math.Cos(angle)), (float)(radius * Math.Sin(angle)), pixel, teeth,
                LegacyGearShimmer.TipRadius, LegacyGearShimmer.RootRadius, LegacyGearShimmer.RimInnerRadius,
                LegacyGearShimmer.HubRadius, LegacyGearShimmer.AxleRadius, 4);
        }
    }
}
