using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The finishing blow's timeline (<see cref="LegacyFinishingBlow"/>): a long slow motion while the letterbox bars
    /// slide in, a freeze frame of its exact length, and for the 서막's forced loss a fall to black and white while the
    /// bars slide out; then it hands over. Real seconds throughout.</summary>
    public sealed class LegacyFinishingBlowTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void SlowMotion_ThenAFreezeFrameOfItsLength_ThenItHandsOver()
        {
            var blow = new LegacyFinishingBlow();
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None));
            Assert.That(blow.IsRunning || blow.IsFrozen || blow.IsComplete, Is.False);
            Assert.That(blow.CombatSpeed, Is.EqualTo(1f));

            blow.Begin(1.6f, .12f, .4f, .5f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.SlowMotion));
            Assert.That(blow.IsRunning, Is.True);
            Assert.That(blow.IsFrozen, Is.False);
            Assert.That(blow.CombatSpeed, Is.EqualTo(.12f).Within(Tolerance), "The battle clock crawls…");
            Assert.That(blow.BarsAmount, Is.Zero, "…and the bars start out of sight.");

            blow.Advance(.2f);
            Assert.That(blow.BarsAmount, Is.EqualTo(.5f).Within(Tolerance), "Half their time, half in (eased).");
            blow.Advance(.2f);
            Assert.That(blow.BarsAmount, Is.EqualTo(1f).Within(Tolerance));
            blow.Advance(1.19f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.SlowMotion), "The slow motion lasts its 1.6 s.");
            blow.Advance(.02f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.Freeze));
            Assert.That(blow.StageElapsed, Is.EqualTo(.01f).Within(Tolerance), "The frame's rest counts toward the freeze.");
            Assert.That(blow.IsFrozen && blow.IsRunning, Is.True);
            Assert.That(blow.CombatSpeed, Is.Zero, "Nothing moves in the freeze frame.");
            Assert.That(blow.BarsAmount, Is.EqualTo(1f));

            blow.Advance(.48f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.Freeze), "The freeze lasts its half second.");
            blow.Advance(.02f);
            Assert.That(blow.IsComplete, Is.True);
            Assert.That(blow.IsRunning || blow.IsFrozen, Is.False);
            Assert.That(blow.BarsAmount, Is.Zero, "The bars are gone before the result comes.");
            Assert.That(blow.FallAmount, Is.Zero);
            Assert.That(blow.EndsInGrey, Is.False, "Only the forced loss falls into grey.");
            Assert.That(blow.CombatSpeed, Is.EqualTo(1f));
            blow.Advance(5f);
            Assert.That(blow.IsComplete, Is.True, "It stays played out until cleared.");
            Assert.That(blow.BarsAmount, Is.Zero);
        }

        [Test]
        public void TheFinalFall_DrainsToGreyWhileTheBarsSlideOut_AndEndsInGrey()
        {
            var blow = new LegacyFinishingBlow();
            blow.Begin(1f, .1f, .25f, .5f, .8f);
            blow.Advance(1.5f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.Fall));
            Assert.That(blow.IsFrozen, Is.True, "The picture stays still while it drains.");
            Assert.That(blow.CombatSpeed, Is.Zero);
            Assert.That(blow.FallAmount, Is.Zero);
            Assert.That(blow.BarsAmount, Is.EqualTo(1f));

            blow.Advance(.4f);
            Assert.That(blow.FallAmount, Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(blow.BarsAmount, Is.EqualTo(.5f).Within(Tolerance), "The bars leave as the colour goes.");
            blow.Advance(.39f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.Fall));
            blow.Advance(.02f);
            Assert.That(blow.IsComplete, Is.True);
            Assert.That(blow.EndsInGrey, Is.True, "What follows takes over a black and white picture…");
            Assert.That(blow.FallAmount, Is.EqualTo(1f));
            Assert.That(blow.BarsAmount, Is.Zero, "…with no bars.");

            blow.Clear();
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None));
            Assert.That(blow.EndsInGrey || blow.IsComplete, Is.False);
            Assert.That(blow.FallAmount, Is.Zero);
        }

        [Test]
        public void ALongFrame_CrossesSeveralStages_AndASlowBarTimeRunsOnIntoTheFreeze()
        {
            var blow = new LegacyFinishingBlow();
            blow.Begin(1.6f, .12f, .4f, .5f, .9f);
            blow.Advance(100f);
            Assert.That(blow.IsComplete && blow.EndsInGrey, Is.True, "A hitch never leaves it stuck in a stage.");

            blow.Begin(.2f, .5f, .6f, 1f);
            blow.Advance(.3f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.Freeze));
            Assert.That(blow.BarsAmount, Is.EqualTo(.5f).Within(Tolerance), "Bars slower than the slow motion keep sliding in the freeze.");
            Assert.That(blow.StageElapsed, Is.EqualTo(.1f).Within(Tolerance), "The frame's rest counts toward the freeze.");
        }

        [Test]
        public void StagesOfZeroLength_AreSkipped_AndWithNothingToPlayItStaysIdle()
        {
            var blow = new LegacyFinishingBlow();
            blow.Begin(0f, .12f, .4f, .5f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.Freeze), "No slow motion: straight to the freeze.");
            blow.Begin(1f, .12f, 0f, 0f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.SlowMotion));
            Assert.That(blow.BarsAmount, Is.EqualTo(1f), "A bar time of 0 puts them in at once.");
            blow.Advance(1f);
            Assert.That(blow.IsComplete, Is.True, "No freeze: it hands over after the slow motion.");
            blow.Begin(0f, .12f, .4f, 0f, .5f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.Fall), "The fall alone still plays.");

            blow.Begin(0f, .12f, .4f, 0f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None), "Everything off: the battle ends as it did before.");
            Assert.That(blow.IsRunning || blow.IsComplete, Is.False);
            blow.Begin(float.NaN, .12f, float.NaN, -1f, float.PositiveInfinity);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None), "Unusable lengths count as 0.");
        }

        [Test]
        public void TheSlowMotionScale_StaysAboveAStop_AndBadFramesAreIgnored()
        {
            var blow = new LegacyFinishingBlow();
            blow.Begin(1f, 0f, .4f, .5f);
            Assert.That(blow.CombatSpeed, Is.EqualTo(LegacyFinishingBlow.MinimumSlowMotionScale), "Only the freeze stops the clock.");
            blow.Begin(1f, 3f, .4f, .5f);
            Assert.That(blow.CombatSpeed, Is.EqualTo(1f));
            blow.Begin(1f, float.NaN, .4f, .5f);
            Assert.That(blow.CombatSpeed, Is.EqualTo(1f));

            blow.Begin(1f, .12f, .4f, .5f);
            blow.Advance(-1f);
            blow.Advance(float.NaN);
            blow.Advance(float.PositiveInfinity);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.SlowMotion));
            Assert.That(blow.StageElapsed, Is.Zero);
        }

        [Test]
        public void Clear_StopsItMidway_AndANewBlowStartsAfresh()
        {
            var blow = new LegacyFinishingBlow();
            blow.Begin(1f, .12f, .4f, .5f, .9f);
            blow.Advance(1.2f);
            Assert.That(blow.IsFrozen, Is.True);
            blow.Clear();
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None));
            Assert.That(blow.CombatSpeed, Is.EqualTo(1f), "Leaving mid-freeze never leaves the clock stopped…");
            Assert.That(blow.BarsAmount, Is.Zero, "…or the bars up.");
            blow.Advance(1f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None));

            blow.Begin(1f, .12f, .4f, .5f);
            Assert.That(blow.Current, Is.EqualTo(LegacyFinishingBlow.Stage.SlowMotion));
            Assert.That(blow.BarsAmount, Is.Zero);
            Assert.That(blow.StageElapsed, Is.Zero);
        }
    }
}
