using NUnit.Framework;
using TurnLimbo.Runtime.Barks;

namespace TurnLimbo.Core.Tests
{
    /// <summary>Where the speech bubbles go (presentation only): above the fighter's head HUD, never over a head HUD or the
    /// other bubble, always on screen.</summary>
    public sealed class BarkLayoutTests
    {
        private const float Gap = 6f;
        // A 1920x1080 HUD with 16-unit margins, y up.
        private static readonly BarkBox Screen = new BarkBox(16f, 16f, 1888f, 1048f);
        // The player's status panel (236 wide, centred on the head at 500) with its queue row's room above, the row
        // reaching further left; the enemy's far to the right.
        private static readonly BarkBox PlayerStack = new BarkBox(300f, 700f, 318f, 184f);
        private static readonly BarkBox EnemyStack = new BarkBox(1300f, 700f, 318f, 184f);

        [Test]
        public void ABubble_SitsCentredOverTheHead_JustAboveItsHeadHud()
        {
            BarkBox bubble = BarkLayout.Place(500f, 200f, 60f, PlayerStack, EnemyStack, Screen, Gap, -1);
            Assert.That(bubble.X, Is.EqualTo(400f));
            Assert.That(bubble.Y, Is.EqualTo(PlayerStack.Top + Gap));
            Assert.That(bubble.Width == 200f && bubble.Height == 60f, Is.True);
            Assert.That(bubble.Overlaps(PlayerStack) || bubble.Overlaps(EnemyStack), Is.False);
        }

        [Test]
        public void ABubble_StaysOnScreen_Sideways()
        {
            BarkBox left = BarkLayout.Place(50f, 200f, 60f, new BarkBox(0f, 700f, 150f, 184f), EnemyStack, Screen, Gap, -1);
            Assert.That(left.X, Is.EqualTo(Screen.X));
            BarkBox right = BarkLayout.Place(1900f, 200f, 60f, new BarkBox(1780f, 700f, 140f, 184f), PlayerStack, Screen, Gap, 1);
            Assert.That(right.Right, Is.EqualTo(Screen.Right));
        }

        [Test]
        public void ABubble_ReachingOverTheOtherHeadHud_RisesAboveItToo()
        {
            var closeEnemy = new BarkBox(560f, 760f, 240f, 190f);
            BarkBox bubble = BarkLayout.Place(500f, 200f, 60f, PlayerStack, closeEnemy, Screen, Gap, -1);
            Assert.That(bubble.Y, Is.EqualTo(closeEnemy.Top + Gap));
            Assert.That(bubble.Overlaps(closeEnemy) || bubble.Overlaps(PlayerStack), Is.False);
            // One that does not reach over it ignores it.
            bubble = BarkLayout.Place(400f, 100f, 60f, PlayerStack, closeEnemy, Screen, Gap, -1);
            Assert.That(bubble.Y, Is.EqualTo(PlayerStack.Top + Gap));
        }

        [Test]
        public void WithNoRoomAbove_ABubble_GoesBesideItsHeadHud_OutwardFirst()
        {
            var high = new BarkBox(300f, 900f, 318f, 120f);
            BarkBox bubble = BarkLayout.Place(500f, 200f, 60f, high, EnemyStack, Screen, Gap, -1);
            Assert.That(bubble.Top, Is.EqualTo(Screen.Top), "As high as the screen allows…");
            Assert.That(bubble.Right, Is.EqualTo(high.X - Gap), "…on its outer side.");
            Assert.That(bubble.Overlaps(high), Is.False);

            var highAtTheEdge = new BarkBox(40f, 900f, 318f, 120f);
            bubble = BarkLayout.Place(160f, 200f, 60f, highAtTheEdge, EnemyStack, Screen, Gap, -1);
            Assert.That(bubble.X, Is.EqualTo(highAtTheEdge.Right + Gap), "The inner side when the outer has no room.");
            Assert.That(bubble.Top, Is.EqualTo(Screen.Top));

            // Inward would cover the other fighter's head HUD, outward leaves the screen: it stays over its head.
            var crowding = new BarkBox(380f, 880f, 300f, 184f);
            bubble = BarkLayout.Place(160f, 200f, 60f, highAtTheEdge, crowding, Screen, Gap, -1);
            Assert.That(bubble.CenterX, Is.EqualTo(160f));
            Assert.That(bubble.Top, Is.EqualTo(Screen.Top), "On screen above all.");
        }

        [Test]
        public void TwoBubblesThatWouldOverlap_MoveApartSideways_AroundTheMiddle()
        {
            var first = new BarkBox(400f, 890f, 300f, 60f);
            var second = new BarkBox(600f, 900f, 300f, 60f);
            Assert.That(BarkLayout.Separate(ref first, ref second, PlayerStack, EnemyStack, Screen, Gap), Is.True);
            Assert.That(first.Overlaps(second), Is.False);
            Assert.That(second.X - first.Right, Is.EqualTo(Gap).Within(1e-3f));
            Assert.That((first.CenterX + second.CenterX) * .5f, Is.EqualTo(650f).Within(1e-3f), "Each gives way by half.");
            Assert.That(first.Y, Is.EqualTo(890f), "Nothing under them now: they keep their height.");
            Assert.That(second.Y, Is.EqualTo(900f));

            // The order of the arguments does not matter: the one on the left goes left.
            first = new BarkBox(600f, 900f, 300f, 60f);
            second = new BarkBox(400f, 890f, 300f, 60f);
            BarkLayout.Separate(ref first, ref second, PlayerStack, EnemyStack, Screen, Gap);
            Assert.That(second.Right + Gap, Is.EqualTo(first.X).Within(1e-3f));
        }

        [Test]
        public void SeparatedBubbles_StayOnScreen_AndRiseOverAHeadHudNowUnderThem()
        {
            var first = new BarkBox(1550f, 900f, 300f, 60f);
            var second = new BarkBox(1600f, 905f, 300f, 60f);
            BarkLayout.Separate(ref first, ref second, PlayerStack, EnemyStack, Screen, Gap);
            Assert.That(second.Right, Is.EqualTo(Screen.Right), "Pressed to the edge, the pair shifts together…");
            Assert.That(first.Right + Gap, Is.EqualTo(second.X).Within(1e-3f), "…and still does not overlap.");

            var tallNeighbour = new BarkBox(300f, 700f, 60f, 230f);
            first = new BarkBox(400f, 890f, 300f, 60f);
            second = new BarkBox(600f, 890f, 300f, 60f);
            BarkLayout.Separate(ref first, ref second, tallNeighbour, EnemyStack, Screen, Gap);
            Assert.That(first.Y, Is.EqualTo(tallNeighbour.Top + Gap), "Moved over a head HUD, it rises above it.");
            Assert.That(first.Overlaps(tallNeighbour) || first.Overlaps(second), Is.False);
        }

        [Test]
        public void BubblesThatDoNotOverlap_StayWhereTheyAre()
        {
            var first = new BarkBox(400f, 890f, 300f, 60f);
            var stacked = new BarkBox(500f, 960f, 300f, 60f);
            Assert.That(BarkLayout.Separate(ref first, ref stacked, PlayerStack, EnemyStack, Screen, Gap), Is.False,
                "One above the other is not in the way.");
            var apart = new BarkBox(1200f, 890f, 300f, 60f);
            Assert.That(BarkLayout.Separate(ref first, ref apart, PlayerStack, EnemyStack, Screen, Gap), Is.False);
            Assert.That(first.X == 400f && apart.X == 1200f, Is.True);

            var wide = new BarkBox(0f, 0f, 1200f, 60f);
            var alsoWide = new BarkBox(100f, 0f, 1200f, 60f);
            Assert.That(BarkLayout.Separate(ref wide, ref alsoWide, default, default, Screen, Gap), Is.True);
            Assert.That(wide.X == Screen.X && alsoWide.Right == Screen.Right, Is.True, "Too wide for both: as far apart as it goes.");
        }
    }
}
