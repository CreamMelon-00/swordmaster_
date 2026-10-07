using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The forest's ambience bed (<see cref="LegacyAmbienceMix"/>: where it plays, how a black screen and the story's
    /// own loop quiet it, how it fades) and the planning clock's pressure on the camera (<see cref="LegacyTimePressure"/>:
    /// nothing above its share of the time, rising as the time runs out, eased in and out on real time).</summary>
    public sealed class SoundAndCameraCueTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void TheForestBed_PlaysInTheForest_QuietsWithABlackScreen_AndGivesWayToTheStorysLoop()
        {
            Assert.That(LegacyAmbienceMix.Target(false, .4f, 1f, 0f, .7f), Is.Zero, "Title, lobby and briefing: silent.");
            Assert.That(LegacyAmbienceMix.Target(true, .4f, 1f, 0f, .7f), Is.EqualTo(.4f).Within(Tolerance), "A battle: its volume.");
            Assert.That(LegacyAmbienceMix.Target(true, .4f, 0f, 0f, .7f), Is.Zero, "A black screen hides the forest's sound too…");
            Assert.That(LegacyAmbienceMix.Target(true, .4f, .5f, 0f, .7f), Is.EqualTo(.2f).Within(Tolerance), "…and fades it with the picture.");
            Assert.That(LegacyAmbienceMix.Target(true, .4f, 1f, 1f, .7f), Is.EqualTo(.12f).Within(Tolerance),
                "A scene's loop at full ducks the bed by its share…");
            Assert.That(LegacyAmbienceMix.Target(true, .4f, 1f, .5f, .7f), Is.EqualTo(.26f).Within(Tolerance), "…and by less while it fades.");
            Assert.That(LegacyAmbienceMix.Target(true, .4f, 1f, 1f, 1f), Is.Zero, "A full duck replaces the bed.");
            Assert.That(LegacyAmbienceMix.Target(true, .4f, 1f, 1f, 0f), Is.EqualTo(.4f).Within(Tolerance), "No duck leaves it be.");
            Assert.That(LegacyAmbienceMix.Target(true, 2f, 3f, -1f, .7f), Is.EqualTo(1f).Within(Tolerance), "Out-of-range values clamp.");
            Assert.That(LegacyAmbienceMix.Target(true, float.NaN, 1f, 0f, .7f), Is.Zero);
        }

        [Test]
        public void TheForestBed_FadesAWholeSwingOverItsSeconds_AndTurningItDownFadesAsFast()
        {
            // A whole swing of the volume (0.4) over 1.2 s: a third of it per 0.4 s.
            float volume = LegacyAmbienceMix.Step(0f, .4f, .6f, 1.2f, .4f);
            Assert.That(volume, Is.EqualTo(.2f).Within(Tolerance), "Half way in after half the fade.");
            volume = LegacyAmbienceMix.Step(volume, .4f, 1f, 1.2f, .4f);
            Assert.That(volume, Is.EqualTo(.4f).Within(Tolerance), "Never past the target.");
            Assert.That(LegacyAmbienceMix.Step(.4f, 0f, .6f, 1.2f, .4f), Is.EqualTo(.2f).Within(Tolerance), "Out at the same pace.");
            Assert.That(LegacyAmbienceMix.Step(.4f, .12f, .42f, 1.2f, .4f), Is.EqualTo(.26f).Within(Tolerance), "A duck glides down too.");
            Assert.That(LegacyAmbienceMix.Step(.8f, .2f, .3f, 1f, .2f), Is.EqualTo(.56f).Within(Tolerance),
                "Turned down from louder, it moves at the louder level's pace.");
            Assert.That(LegacyAmbienceMix.Step(0f, .4f, .1f, 0f, .4f), Is.EqualTo(.4f), "No fade time: at once.");
            Assert.That(LegacyAmbienceMix.Step(.1f, .4f, 0f, 1.2f, .4f), Is.EqualTo(.1f).Within(Tolerance), "No time, no change.");
            Assert.That(LegacyAmbienceMix.Step(.1f, .4f, float.NaN, 1.2f, .4f), Is.EqualTo(.1f).Within(Tolerance));
            Assert.That(LegacyAmbienceMix.Step(.1f, .4f, float.PositiveInfinity, 1.2f, .4f), Is.EqualTo(.4f).Within(Tolerance));
        }

        [Test]
        public void TheClocksPressure_StartsBelowItsShare_AndRisesAsTheTimeRunsOut()
        {
            Assert.That(LegacyTimePressure.Target(10f, 10f, .3f), Is.Zero, "A fresh turn: none.");
            Assert.That(LegacyTimePressure.Target(3f, 10f, .3f), Is.Zero, "Just at the share: still none…");
            Assert.That(LegacyTimePressure.Target(1.5f, 10f, .3f), Is.EqualTo(.5f).Within(Tolerance), "…half of it halfway down…");
            Assert.That(LegacyTimePressure.Target(0f, 10f, .3f), Is.EqualTo(1f).Within(Tolerance), "…all of it as the time runs out.");
            Assert.That(LegacyTimePressure.Target(-2f, 10f, .3f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(LegacyTimePressure.Target(3f, 12f, .3f), Is.EqualTo(1f / 6f).Within(Tolerance), "A longer turn, the same share.");
            Assert.That(LegacyTimePressure.Target(0f, 10f, 0f), Is.Zero, "A share of 0 switches it off.");
            Assert.That(LegacyTimePressure.Target(0f, 0f, .3f), Is.Zero, "No planning time, no pressure.");
            Assert.That(LegacyTimePressure.Target(float.NaN, 10f, .3f), Is.Zero);
            Assert.That(LegacyTimePressure.Target(9f, 10f, 2f), Is.EqualTo(.1f).Within(Tolerance), "A share past the whole is the whole.");
        }

        [Test]
        public void TheClocksPressure_EasesInAndBackOutOnRealTime()
        {
            float push = LegacyTimePressure.Step(0f, 1f, LegacyTimePressure.EaseInSeconds * .5f);
            Assert.That(push, Is.EqualTo(.5f).Within(Tolerance), "Half in after half its ease…");
            push = LegacyTimePressure.Step(push, 1f, LegacyTimePressure.EaseInSeconds);
            Assert.That(push, Is.EqualTo(1f), "…and never past the target.");
            push = LegacyTimePressure.Step(push, 0f, LegacyTimePressure.EaseOutSeconds * .5f);
            Assert.That(push, Is.EqualTo(.5f).Within(Tolerance), "Back out faster than in.");
            Assert.That(LegacyTimePressure.EaseOutSeconds, Is.LessThan(LegacyTimePressure.EaseInSeconds));
            Assert.That(LegacyTimePressure.Step(.3f, .3f, 1f), Is.EqualTo(.3f).Within(Tolerance));
            Assert.That(LegacyTimePressure.Step(.3f, 1f, 0f), Is.EqualTo(.3f).Within(Tolerance), "No time, no change.");
            Assert.That(LegacyTimePressure.Step(.3f, 1f, float.PositiveInfinity), Is.EqualTo(1f));
            Assert.That(LegacyTimePressure.Step(2f, -1f, .01f), Is.LessThanOrEqualTo(1f), "Out-of-range values clamp.");
        }
    }
}
