using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The duel HUD's two facing queue panels (<see cref="LegacyQueuePanelLayout"/>): one shared spacing that tightens
    /// only when the longer row would not fit, slot 1 innermost and the rest outward, the slide toward the seam after each
    /// resolved slot, and the camera ceiling that keeps the fighters' feet over the panels.</summary>
    public sealed class QueuePanelLayoutTests
    {
        private const float Tolerance = 1e-4f;
        private const float Card = LegacyQueuePanelLayout.DefaultCardSize, Full = LegacyQueuePanelLayout.DefaultCardSpacing;
        private const float Room = LegacyQueuePanelLayout.DefaultPanelWidth - 2f * LegacyQueuePanelLayout.Padding;

        [Test]
        public void TheDefaultPanel_HoldsAtLeastSixCardsAtFullSpacing()
        {
            for (int count = 0; count <= 6; count++)
                Assert.That(LegacyQueuePanelLayout.Spacing(Room, Card, count, Full), Is.EqualTo(Full), count + " cards");
            Assert.That(Card + 5 * Full, Is.LessThanOrEqualTo(Room), "Six cards side by side fit one default panel.");
        }

        [Test]
        public void ALongerRow_Tightens_DownToTheFloor_AndFillsItsRoomExactly()
        {
            float previous = Full;
            for (int count = 8; count <= 40; count++)
            {
                float spacing = LegacyQueuePanelLayout.Spacing(Room, Card, count, Full);
                Assert.That(spacing, Is.LessThanOrEqualTo(previous + Tolerance), "More cards never spread out, at " + count);
                Assert.That(spacing, Is.GreaterThanOrEqualTo(LegacyQueuePanelLayout.MinimumSpacing - Tolerance));
                if (spacing > LegacyQueuePanelLayout.MinimumSpacing + Tolerance && spacing < Full - Tolerance)
                    Assert.That(Card + (count - 1) * spacing, Is.EqualTo(Room).Within(.01f), "A tightened row fills its panel.");
                previous = spacing;
            }
            Assert.That(LegacyQueuePanelLayout.Spacing(Room, Card, 400, Full), Is.EqualTo(LegacyQueuePanelLayout.MinimumSpacing));
        }

        [Test]
        public void Spacing_SurvivesNonsense()
        {
            Assert.That(LegacyQueuePanelLayout.Spacing(float.NaN, Card, 5, Full), Is.EqualTo(LegacyQueuePanelLayout.MinimumSpacing));
            Assert.That(LegacyQueuePanelLayout.Spacing(Room, Card, 3, float.NaN), Is.EqualTo(Full));
            Assert.That(LegacyQueuePanelLayout.Spacing(Room, Card, 3, 2f), Is.EqualTo(LegacyQueuePanelLayout.MinimumSpacing));
        }

        [Test]
        public void SlotOne_IsInnermost_AndLaterSlotsGoOutwardEvenly()
        {
            const float inner = 66f;
            Assert.That(LegacyQueuePanelLayout.CardDistance(0, inner, Full, 0f), Is.EqualTo(inner));
            for (int index = 1; index < 8; index++)
                Assert.That(LegacyQueuePanelLayout.CardDistance(index, inner, Full, 0f) -
                    LegacyQueuePanelLayout.CardDistance(index - 1, inner, Full, 0f), Is.EqualTo(Full).Within(Tolerance));
            // Sliding in, the whole row stands further out by the slots still to go.
            Assert.That(LegacyQueuePanelLayout.CardDistance(0, inner, Full, 1f), Is.EqualTo(inner + Full).Within(Tolerance));
            Assert.That(LegacyQueuePanelLayout.CardDistance(2, inner, Full, .5f), Is.EqualTo(inner + 2.5f * Full).Within(Tolerance));
            Assert.That(LegacyQueuePanelLayout.CardDistance(-3, inner, Full, -1f), Is.EqualTo(inner), "Never inside the seam.");
        }

        [Test]
        public void TheSlide_StartsWhereTheCardsWere_EasesHome_AndEndsAtRest()
        {
            const float seconds = LegacyQueuePanelLayout.DefaultSlideSeconds;
            Assert.That(LegacyQueuePanelLayout.Slide(1f, 0f, seconds), Is.EqualTo(1f), "The next pair starts one step out…");
            float previous = 1f;
            for (float elapsed = .01f; elapsed < seconds; elapsed += .01f)
            {
                float slide = LegacyQueuePanelLayout.Slide(1f, elapsed, seconds);
                Assert.That(slide, Is.LessThan(previous), "…moves in all the way…");
                Assert.That(slide, Is.GreaterThan(0f));
                previous = slide;
            }
            Assert.That(LegacyQueuePanelLayout.Slide(1f, seconds, seconds), Is.Zero, "…and rests at the seam.");
            Assert.That(LegacyQueuePanelLayout.Slide(1f, 99f, seconds), Is.Zero);
            Assert.That(LegacyQueuePanelLayout.Slide(1f, seconds * .25f, seconds), Is.LessThan(.6f), "It moves most of the way early.");
            Assert.That(LegacyQueuePanelLayout.Slide(1f, 0f, 0f), Is.Zero, "A slide of no time is home at once.");
            Assert.That(LegacyQueuePanelLayout.Slide(float.NaN, .1f, seconds), Is.Zero);
            Assert.That(LegacyQueuePanelLayout.Slide(1f, float.NaN, seconds), Is.EqualTo(1f));
        }

        [Test]
        public void ASlideStart_AddsAStepPerConsumedSlot_CapsIt_AndSnapsHomeOnANewTurn()
        {
            Assert.That(LegacyQueuePanelLayout.SlideStart(0f, 0, 1), Is.EqualTo(1f));
            Assert.That(LegacyQueuePanelLayout.SlideStart(.3f, 1, 2), Is.EqualTo(1.3f).Within(Tolerance),
                "A slot that ends mid-slide carries on from where the cards are.");
            Assert.That(LegacyQueuePanelLayout.SlideStart(0f, 0, 5), Is.EqualTo(LegacyQueuePanelLayout.MaximumSlide));
            Assert.That(LegacyQueuePanelLayout.SlideStart(.7f, 3, 3), Is.EqualTo(.7f).Within(Tolerance), "Nothing new, nothing changes.");
            Assert.That(LegacyQueuePanelLayout.SlideStart(.7f, 3, 0), Is.Zero, "A new turn snaps the rows home.");
        }

        [Test]
        public void TheCameraCeiling_KeepsTheGroundOverTheFloor_AtEveryZoom()
        {
            foreach (float size in new[] { 3.5f, 4.8f, 5f, 6f })
                foreach (float floor in new[] { .05f, .2f, .33f })
                {
                    const float ground = -3.68f;
                    float ceiling = LegacyQueuePanelLayout.CameraCeiling(ground, size, floor);
                    // The ground's share of the screen's height from the bottom with the camera at the ceiling…
                    float share = (ground - (ceiling - size)) / (2f * size);
                    Assert.That(share, Is.EqualTo(floor).Within(Tolerance), size + " / " + floor);
                    // …and any lower camera shows it higher still.
                    float lower = (ground - (ceiling - .5f - size)) / (2f * size);
                    Assert.That(lower, Is.GreaterThan(floor));
                }
            Assert.That(float.IsPositiveInfinity(LegacyQueuePanelLayout.CameraCeiling(-3f, 5f, 0f)), Is.True, "No floor asks nothing.");
            Assert.That(float.IsPositiveInfinity(LegacyQueuePanelLayout.CameraCeiling(float.NaN, 5f, .3f)), Is.True);
        }
    }
}
