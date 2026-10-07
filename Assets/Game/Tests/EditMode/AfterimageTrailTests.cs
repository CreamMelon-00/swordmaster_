using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The timing behind 이아's 수훈 afterimages: a ghost every interval while her aura is up, on whatever clock the
    /// trail is given (stopped by hit stop or a freeze frame, slowed by slow motion), one at most per frame however long it
    /// was, none while the aura is down, opacity that follows the aura's fade, the slow breath of the echo that keeps
    /// a still figure's trail visible, and a fade kept within what the pool drawing the ghosts holds.</summary>
    public sealed class AfterimageTrailTests
    {
        private const float Tolerance = 1e-4f;
        // Binary fractions, so the clock adds up exactly.
        private const float Interval = .25f;
        private const float Frame = .0625f;

        [Test]
        public void WhileThePowerIsUp_AGhostIsDueEveryInterval()
        {
            var trail = new LegacyAfterimageTrail();
            int due = 0;
            for (int frame = 1; frame <= 16; frame++)
            {
                bool now = trail.Advance(Frame, Interval, 1f);
                Assert.That(now, Is.EqualTo(frame % 4 == 0), "Frame " + frame + ": one every four frames of a sixteenth.");
                if (now) due++;
            }
            Assert.That(due, Is.EqualTo(4), "A second of power leaves four ghosts at a quarter-second interval.");
        }

        [Test]
        public void WhileThePowerIsDown_NoGhostIsDue_AndTheCountStartsAfreshWhenItReturns()
        {
            var trail = new LegacyAfterimageTrail();
            for (int frame = 0; frame < 40; frame++)
                Assert.That(trail.Advance(Frame, Interval, 0f), Is.False, "No aura, no trail.");
            Assert.That(trail.Advance(1f, Interval, -1f) || trail.Advance(1f, Interval, float.NaN), Is.False);

            Assert.That(trail.Advance(Frame * 3f, Interval, 1f), Is.False, "Three sixteenths into the power…");
            Assert.That(trail.Advance(Frame, Interval, 0f), Is.False, "…it goes down…");
            Assert.That(trail.Advance(Frame, Interval, 1f), Is.False, "…and back up: the count starts afresh…");
            Assert.That(trail.Advance(Frame * 2f, Interval, 1f), Is.False);
            Assert.That(trail.Advance(Frame, Interval, 1f), Is.True, "…a whole interval after it came back.");
        }

        [Test]
        public void AStoppedClock_HoldsTheTrail_AndASlowOne_SpacesItOut()
        {
            var trail = new LegacyAfterimageTrail();
            Assert.That(trail.Advance(Frame * 3f, Interval, 1f), Is.False);
            float breath = trail.Breath;
            for (int frame = 0; frame < 100; frame++)
            {
                // Hit stop, a cut-in's hold or the finishing blow's freeze frame: the battle's clock does not move.
                Assert.That(trail.Advance(0f, Interval, 1f), Is.False, "A held clock leaves no ghost…");
                Assert.That(trail.Advance(-Frame, Interval, 1f) || trail.Advance(float.NaN, Interval, 1f), Is.False);
            }
            Assert.That(trail.Breath, Is.EqualTo(breath), "…and does not breathe.");
            Assert.That(trail.Advance(Frame, Interval, 1f), Is.True, "It picks up exactly where it stopped.");

            // Slow motion at a quarter speed: the same real frames give the trail a quarter of the time.
            int due = 0;
            for (int frame = 0; frame < 16; frame++)
                if (trail.Advance(Frame * .25f, Interval, 1f)) due++;
            Assert.That(due, Is.EqualTo(1), "A quarter of the ghosts in slow motion.");
        }

        [Test]
        public void ALongFrame_BringsOneGhost_NotABurst()
        {
            var trail = new LegacyAfterimageTrail();
            Assert.That(trail.Advance(1f, Interval, 1f), Is.True, "A hitch of four intervals still leaves one ghost…");
            Assert.That(trail.Advance(Frame, Interval, 1f), Is.False, "…and does not owe the others.");
            Assert.That(trail.Advance(Frame, Interval, 1f), Is.False);
            Assert.That(trail.Advance(Frame, Interval, 1f), Is.False);
            Assert.That(trail.Advance(Frame, Interval, 1f), Is.True, "The rhythm carries on from it.");

            var hitch = new LegacyAfterimageTrail();
            Assert.That(hitch.Advance(.3125f, Interval, 1f), Is.True);
            Assert.That(hitch.Advance(Frame * 3f, Interval, 1f), Is.True, "What a long frame leaves over is kept.");
            Assert.That(new LegacyAfterimageTrail().Advance(float.PositiveInfinity, Interval, 1f), Is.True, "An endless frame: one.");
        }

        [Test]
        public void AnIntervalOfNothing_LeavesOneEveryFrame_AndAnEndlessOne_None()
        {
            var trail = new LegacyAfterimageTrail();
            for (int frame = 0; frame < 5; frame++) Assert.That(trail.Advance(Frame, 0f, 1f), Is.True);
            Assert.That(trail.Advance(0f, 0f, 1f), Is.False, "Still only when time passes.");
            for (int frame = 0; frame < 5; frame++) Assert.That(trail.Advance(Frame, float.PositiveInfinity, 1f), Is.False);
        }

        [Test]
        public void AGhostsOpacity_FollowsTheAurasFade()
        {
            Assert.That(LegacyAfterimageTrail.Opacity(.45f, 1f), Is.EqualTo(.45f).Within(Tolerance), "The set opacity at full power…");
            Assert.That(LegacyAfterimageTrail.Opacity(.45f, .5f), Is.EqualTo(.225f).Within(Tolerance), "…half of it half faded in…");
            Assert.That(LegacyAfterimageTrail.Opacity(.45f, 0f), Is.Zero, "…none once it is gone.");
            Assert.That(LegacyAfterimageTrail.Opacity(0f, 1f), Is.Zero, "An opacity of 0 switches them off.");
            Assert.That(LegacyAfterimageTrail.Opacity(2f, 3f), Is.EqualTo(1f), "Never more than opaque…");
            Assert.That(LegacyAfterimageTrail.Opacity(-1f, 1f) + LegacyAfterimageTrail.Opacity(float.NaN, 1f) +
                LegacyAfterimageTrail.Opacity(.5f, float.NaN), Is.Zero, "…nor less than nothing.");
        }

        [Test]
        public void TheEcho_BreathesSlowly_BetweenItsFloorAndItsFullAmount()
        {
            var trail = new LegacyAfterimageTrail();
            const float amount = .3f;
            Assert.That(trail.Breath, Is.Zero.Within(Tolerance), "It starts at the bottom of a breath…");
            Assert.That(trail.Breathe(amount), Is.EqualTo(amount * LegacyAfterimageTrail.BreathFloor).Within(Tolerance),
                "…with the floor's share of the drift or swell…");
            trail.Advance(LegacyAfterimageTrail.BreathPeriod * .5f, Interval, 0f);
            Assert.That(trail.Breath, Is.EqualTo(1f).Within(Tolerance), "…all of it half a breath later…");
            Assert.That(trail.Breathe(amount), Is.EqualTo(amount).Within(Tolerance));
            trail.Advance(LegacyAfterimageTrail.BreathPeriod * .5f, Interval, 0f);
            Assert.That(trail.Breath, Is.Zero.Within(Tolerance), "…and back down after a whole one.");

            float lowest = float.MaxValue, highest = float.MinValue;
            for (int frame = 0; frame < 200; frame++)
            {
                trail.Advance(.0137f, Interval, 1f);
                float breathed = trail.Breathe(amount);
                lowest = System.Math.Min(lowest, breathed);
                highest = System.Math.Max(highest, breathed);
            }
            Assert.That(lowest, Is.GreaterThanOrEqualTo(amount * LegacyAfterimageTrail.BreathFloor - Tolerance),
                "A still figure always shows some of the echo…");
            Assert.That(highest, Is.LessThanOrEqualTo(amount + Tolerance), "…never more than the set amount…");
            Assert.That(highest - lowest, Is.GreaterThan(amount * .3f), "…and it visibly breathes.");
            Assert.That(trail.Breathe(0f) + trail.Breathe(-1f) + trail.Breathe(float.NaN) + trail.Breathe(float.PositiveInfinity),
                Is.Zero, "No drift or swell set, none breathed.");
        }

        [Test]
        public void AGhostsFade_StaysWithinWhatThePoolHolds()
        {
            Assert.That(LegacyAfterimageTrail.FadeSeconds(.5f, Interval, 5), Is.EqualTo(.5f), "A fade the pool holds is kept…");
            Assert.That(LegacyAfterimageTrail.FadeSeconds(2f, Interval, 5), Is.EqualTo(1f),
                "…a longer one is cut to four intervals for a pool of five…");
            Assert.That(LegacyAfterimageTrail.FadeSeconds(float.PositiveInfinity, Interval, 5), Is.EqualTo(1f));
            Assert.That(LegacyAfterimageTrail.FadeSeconds(1.5f, .02f, 76), Is.EqualTo(1.5f).Within(Tolerance),
                "…and the settings' longest fade at their shortest interval fits the 수훈 pool (76) whole.");
            Assert.That(LegacyAfterimageTrail.FadeSeconds(1f, Interval, 1) + LegacyAfterimageTrail.FadeSeconds(1f, Interval, -3) +
                LegacyAfterimageTrail.FadeSeconds(0f, Interval, 5) + LegacyAfterimageTrail.FadeSeconds(-1f, Interval, 5) +
                LegacyAfterimageTrail.FadeSeconds(float.NaN, Interval, 5), Is.Zero, "None for a pool of one or no fade.");
            Assert.That(LegacyAfterimageTrail.FadeSeconds(.5f, 0f, 5), Is.EqualTo(.5f), "One every frame: no bound to keep…");
            Assert.That(LegacyAfterimageTrail.FadeSeconds(.5f, float.NaN, 5), Is.EqualTo(.5f));
            Assert.That(LegacyAfterimageTrail.FadeSeconds(.5f, float.PositiveInfinity, 5), Is.EqualTo(.5f), "…nor for none at all.");
        }

        [Test]
        public void WithThatFade_ThePoolNeverTakesBackAGhostThatStillShows()
        {
            const int pool = 5;
            float fade = LegacyAfterimageTrail.FadeSeconds(10f, Interval, pool);
            // Steady frames; uneven ones; and a long frame whose leftover brings the next ghost a frame later.
            float[] steady = { Frame };
            float[] uneven = { Frame, Frame * 3f, Frame * 5f, Frame * 2f, Frame, Frame * 7f, Frame * 3f, Frame * 3f, Frame * 6f };
            float[] hitch = { Frame * 7f, Frame, Frame * 4f, Frame * 4f, Frame * 4f, Frame * 4f };
            Assert.That(MostShowing(fade, steady), Is.EqualTo(pool - 1), "Steady frames leave one fewer than the pool…");
            Assert.That(MostShowing(fade, uneven), Is.InRange(1, pool), "…uneven ones never more than it…");
            Assert.That(MostShowing(fade, hitch), Is.InRange(1, pool), "…nor a hitch's early ghost…");
            Assert.That(MostShowing(fade + Interval, hitch), Is.GreaterThan(pool), "…which a fade any longer would overflow.");
        }

        // Ages the ghosts the way the arena's pool does (those whose fade is over go before the new one is left) and returns
        // the most that show at once.
        private static int MostShowing(float fade, float[] frames)
        {
            var trail = new LegacyAfterimageTrail();
            var ages = new System.Collections.Generic.List<float>();
            int most = 0;
            for (int frame = 0; frame < 400; frame++)
            {
                float seconds = frames[frame % frames.Length];
                for (int index = ages.Count - 1; index >= 0; index--)
                {
                    ages[index] += seconds;
                    if (ages[index] >= fade) ages.RemoveAt(index);
                }
                if (trail.Advance(seconds, Interval, 1f)) ages.Add(0f);
                most = System.Math.Max(most, ages.Count);
            }
            return most;
        }

        [Test]
        public void Reset_StartsOver()
        {
            var trail = new LegacyAfterimageTrail();
            Assert.That(trail.Advance(Frame * 3f, Interval, 1f), Is.False);
            trail.Reset();
            Assert.That(trail.Breath, Is.Zero.Within(Tolerance));
            Assert.That(trail.Advance(Frame, Interval, 1f), Is.False, "No ghost is owed from before the reset…");
            Assert.That(trail.Advance(Frame * 2f, Interval, 1f), Is.False);
            Assert.That(trail.Advance(Frame, Interval, 1f), Is.True, "…the next comes a whole interval later.");
        }
    }
}
