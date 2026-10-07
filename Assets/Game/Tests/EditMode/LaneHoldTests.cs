using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>Holding a lane in planning (<see cref="LegacyLaneHold"/>): a tap of up to 0.2 s queues, a hold opens the
    /// skill's explanation at 0.35 s with the bar filling in between, a release after it opened never queues, and while the
    /// player reads it the planning clock runs at 0.3× (Tab's inspection keeps its 0.2×; nothing slows outside planning).</summary>
    public sealed class LaneHoldTests
    {
        private const float Tolerance = 1e-5f;
        private const float Tap = LegacyLaneHold.DefaultTapSeconds, Explain = LegacyLaneHold.DefaultExplainSeconds;

        [Test]
        public void Defaults_AreTheAuthorsQuickerTimes()
        {
            Assert.That(LegacyLaneHold.DefaultTapSeconds, Is.EqualTo(.2f), "A tap is up to 0.2 s (was 0.3 s)…");
            Assert.That(LegacyLaneHold.DefaultExplainSeconds, Is.EqualTo(.35f), "…and the explanation opens at 0.35 s (was 1 s).");
            Assert.That(LegacyLaneHold.DefaultExplanationTimeScale, Is.EqualTo(.3f), "Reading it, the clock runs at 0.3×.");
            Assert.That(LegacyLaneHold.InspectionTimeScale, Is.EqualTo(.2f), "Tab's inspection keeps its 0.2×.");
        }

        [Test]
        public void ATap_Queues_ALongerPress_DoesNot_AndAHold_OpensTheExplanation()
        {
            Assert.That(LegacyLaneHold.ReleaseQueues(0f, false, Tap), Is.True, "A press and release within one frame is a tap.");
            Assert.That(LegacyLaneHold.ReleaseQueues(.1f, false, Tap), Is.True);
            Assert.That(LegacyLaneHold.ReleaseQueues(Tap, false, Tap), Is.True, "The tap's limit still queues.");
            Assert.That(LegacyLaneHold.ReleaseQueues(.25f, false, Tap), Is.False, "Past it, a release queues nothing…");
            Assert.That(LegacyLaneHold.OpensExplanation(.25f, Tap, Explain), Is.False, "…and nothing has opened yet.");
            Assert.That(LegacyLaneHold.OpensExplanation(.349f, Tap, Explain), Is.False);
            Assert.That(LegacyLaneHold.OpensExplanation(Explain, Tap, Explain), Is.True, "At 0.35 s the explanation opens.");
            Assert.That(LegacyLaneHold.ReleaseQueues(.1f, true, Tap), Is.False,
                "A spent hold (explained, cycled away, let go off the window) never queues, however short.");
        }

        [Test]
        public void AReleaseAfterTheExplanationOpened_NeverQueues_HoweverItIsTuned()
        {
            foreach (float tap in new[] { .05f, .2f, .5f })
                foreach (float explain in new[] { .1f, .35f, 1.5f })
                {
                    float opensAt = LegacyLaneHold.ExplainSeconds(tap, explain);
                    Assert.That(opensAt, Is.GreaterThanOrEqualTo(tap), "It never opens while a release would still be a tap.");
                    Assert.That(opensAt, Is.EqualTo(System.Math.Max(tap, explain)).Within(Tolerance));
                    foreach (float held in new[] { opensAt, opensAt + .01f, 3f })
                    {
                        bool opened = LegacyLaneHold.OpensExplanation(held, tap, explain);
                        Assert.That(opened, Is.True, tap + "/" + explain + " held " + held);
                        // The controller marks an opened hold spent: its release only closes the explanation.
                        Assert.That(LegacyLaneHold.ReleaseQueues(held, opened, tap), Is.False);
                    }
                }
            Assert.That(LegacyLaneHold.ExplainSeconds(.3f, .1f), Is.EqualTo(.3f).Within(Tolerance), "Tuned below the tap: at the tap.");
            Assert.That(LegacyLaneHold.OpensExplanation(5f, Tap, float.PositiveInfinity), Is.False, "An endless hold time never opens.");
            Assert.That(LegacyLaneHold.ReleaseQueues(0f, false, float.NaN), Is.True, "No tap time: only an instant press queues…");
            Assert.That(LegacyLaneHold.ReleaseQueues(.01f, false, -1f), Is.False, "…and nothing held longer.");
        }

        [Test]
        public void TheHoldBar_FillsFromTheTapsLimitToTheExplanation()
        {
            Assert.That(LegacyLaneHold.Progress(0f, Tap, Explain), Is.Zero);
            Assert.That(LegacyLaneHold.Progress(Tap, Tap, Explain), Is.Zero, "Empty through a tap…");
            Assert.That(LegacyLaneHold.Progress((Tap + Explain) * .5f, Tap, Explain), Is.EqualTo(.5f).Within(1e-4f), "…filling…");
            Assert.That(LegacyLaneHold.Progress(Explain, Tap, Explain), Is.EqualTo(1f).Within(1e-4f), "…full as it opens…");
            Assert.That(LegacyLaneHold.Progress(4f, Tap, Explain), Is.EqualTo(1f), "…and after.");
            float last = 0f;
            for (float held = 0f; held <= .5f; held += .01f)
            {
                float progress = LegacyLaneHold.Progress(held, Tap, Explain);
                Assert.That(progress, Is.GreaterThanOrEqualTo(last).And.InRange(0f, 1f));
                last = progress;
            }
            Assert.That(LegacyLaneHold.Progress(.21f, Tap, .1f), Is.EqualTo(1f), "Opening at the tap's limit: full just past it.");
            Assert.That(LegacyLaneHold.Progress(float.NaN, Tap, Explain), Is.Zero);
            Assert.That(LegacyLaneHold.Progress(1f, Tap, float.PositiveInfinity), Is.Zero, "Never opening: never fills.");
        }

        [Test]
        public void ReadingAnExplanation_SlowsThePlanningClock_TabStillWins_AndNothingSlowsOutsidePlanning()
        {
            const float reading = LegacyLaneHold.DefaultExplanationTimeScale;
            Assert.That(LegacyLaneHold.PlanningTimeScale(true, false, false, reading), Is.EqualTo(1f), "Planning runs at full pace…");
            Assert.That(LegacyLaneHold.PlanningTimeScale(true, false, true, reading), Is.EqualTo(.3f), "…slower while reading…");
            Assert.That(LegacyLaneHold.PlanningTimeScale(true, true, false, reading), Is.EqualTo(LegacyLaneHold.InspectionTimeScale),
                "…Tab's inspection as before…");
            Assert.That(LegacyLaneHold.PlanningTimeScale(true, true, true, reading), Is.EqualTo(LegacyLaneHold.InspectionTimeScale),
                "…and Tab takes over from an explanation.");
            foreach (bool inspecting in new[] { false, true })
                foreach (bool explaining in new[] { false, true })
                    Assert.That(LegacyLaneHold.PlanningTimeScale(false, inspecting, explaining, reading), Is.EqualTo(1f),
                        "Nothing slows outside planning.");
            Assert.That(LegacyLaneHold.PlanningTimeScale(true, false, true, 1f), Is.EqualTo(1f), "1 switches the slow off.");
            Assert.That(LegacyLaneHold.PlanningTimeScale(true, false, true, 3f), Is.EqualTo(1f), "It never speeds the clock up…");
            Assert.That(LegacyLaneHold.PlanningTimeScale(true, false, true, -1f), Is.Zero, "…or runs it backwards.");
            Assert.That(LegacyLaneHold.PlanningTimeScale(true, false, true, float.NaN), Is.EqualTo(1f));
        }
    }
}
