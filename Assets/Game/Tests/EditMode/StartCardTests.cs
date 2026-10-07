using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The duel start card's timeline (<see cref="LegacyStartCard"/>): bars in, figures and names, the swords
    /// closing, a hold, then an exit that fades it all while the bars slide away; a short card (a retry's) plays the same
    /// moves faster; a skip ends it at once. Real seconds throughout.</summary>
    public sealed class StartCardTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void TheCard_ComesIn_Holds_ThenFadesOutAndEndsOnItsLastFrame()
        {
            var card = new LegacyStartCard();
            Assert.That(card.IsShowing || card.IsLeaving, Is.False);
            Assert.That(card.BarsAmount + card.FiguresOpacity + card.NamesOpacity + card.MarkOpacity + card.Presence, Is.Zero);

            card.Begin(1.6f);
            Assert.That(card.IsShowing, Is.True);
            Assert.That(card.Seconds, Is.EqualTo(1.6f));
            Assert.That(card.BarsAmount, Is.Zero, "The bars start out of sight…");
            Assert.That(card.FiguresOpacity + card.NamesOpacity + card.MarkOpacity, Is.Zero, "…and nothing else shows yet.");
            Assert.That(card.FiguresAmount, Is.Zero, "The figures start out at the sides.");

            Assert.That(card.Advance(1.6f * LegacyStartCard.BarsShare), Is.False);
            Assert.That(card.BarsAmount, Is.EqualTo(1f).Within(Tolerance), "The bars are in first.");
            Assert.That(card.FiguresOpacity, Is.GreaterThan(0f).And.LessThan(1f), "The figures are on their way.");
            Assert.That(card.MarkOpacity, Is.Zero, "The swords come after them.");

            card.Advance(1.6f * (LegacyStartCard.MarkEnd - LegacyStartCard.BarsShare));
            Assert.That(card.FiguresAmount, Is.EqualTo(1f).Within(Tolerance), "The figures have slid in…");
            Assert.That(card.FiguresOpacity, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(card.NamesOpacity, Is.EqualTo(1f).Within(Tolerance), "…with their names…");
            Assert.That(card.MarkAmount, Is.EqualTo(1f).Within(Tolerance), "…and the swords have closed.");
            Assert.That(card.MarkOpacity, Is.EqualTo(1f).Within(Tolerance));

            card.Advance(1.6f * (.7f - LegacyStartCard.MarkEnd));
            Assert.That(card.IsLeaving, Is.False, "It holds…");
            Assert.That(card.Presence, Is.EqualTo(1f));
            Assert.That(card.BarsAmount, Is.EqualTo(1f).Within(Tolerance));

            card.Advance(1.6f * .2f);
            Assert.That(card.IsLeaving, Is.True, "…then leaves over its last share…");
            Assert.That(card.Presence, Is.EqualTo(.5f).Within(Tolerance), "…half gone halfway through it.");
            Assert.That(card.BarsAmount, Is.EqualTo(.5f).Within(Tolerance), "The bars slide away with it.");
            Assert.That(card.FiguresOpacity, Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(card.MarkOpacity, Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(card.FiguresAmount, Is.EqualTo(1f).Within(Tolerance), "The figures fade where they stand.");

            Assert.That(card.Advance(1.6f * .099f), Is.False, "Not over before its length…");
            Assert.That(card.IsShowing, Is.True);
            Assert.That(card.Advance(.01f), Is.True, "…and over on the frame that reaches it.");
            Assert.That(card.IsShowing || card.IsLeaving, Is.False);
            Assert.That(card.BarsAmount + card.FiguresOpacity + card.NamesOpacity + card.MarkOpacity + card.Presence, Is.Zero);
            Assert.That(card.Advance(1f), Is.False, "Only that frame reports the end.");
        }

        [Test]
        public void AShortCard_PlaysTheSameMovesFaster()
        {
            var full = new LegacyStartCard();
            var brief = new LegacyStartCard();
            full.Begin(1.6f);
            brief.Begin(.8f);
            for (int step = 1; step <= 19; step++)
            {
                full.Advance(.08f);
                brief.Advance(.04f);
                Assert.That(brief.Progress, Is.EqualTo(full.Progress).Within(Tolerance));
                Assert.That(brief.BarsAmount, Is.EqualTo(full.BarsAmount).Within(Tolerance), "Bars at step " + step);
                Assert.That(brief.FiguresAmount, Is.EqualTo(full.FiguresAmount).Within(Tolerance));
                Assert.That(brief.MarkOpacity, Is.EqualTo(full.MarkOpacity).Within(Tolerance));
            }
            Assert.That(brief.Advance(.05f), Is.True, "Both end on the same share.");
            Assert.That(full.Advance(.1f), Is.True);
        }

        [Test]
        public void Skip_EndsItAtOnce_AndNoLengthPutsNoneUp()
        {
            var card = new LegacyStartCard();
            card.Begin(1.6f);
            card.Advance(.5f);
            card.Skip();
            Assert.That(card.IsShowing, Is.False);
            Assert.That(card.BarsAmount, Is.Zero, "No bar is left behind.");
            Assert.That(card.Advance(.1f), Is.False, "A skipped card does not end again.");

            foreach (float none in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                card.Begin(none);
                Assert.That(card.IsShowing, Is.False, "No card for a length of " + none);
                Assert.That(card.Advance(1f), Is.False);
            }

            card.Begin(1f);
            Assert.That(card.Advance(0f) || card.Advance(-1f) || card.Advance(float.NaN), Is.False);
            Assert.That(card.Elapsed, Is.Zero, "No time, or bad time, moves nothing.");
            Assert.That(card.Advance(float.PositiveInfinity), Is.True, "An endless frame ends it.");
        }
    }
}
