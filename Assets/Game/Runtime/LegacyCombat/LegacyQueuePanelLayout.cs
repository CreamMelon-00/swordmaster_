using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Where the duel HUD's two queue panels put their cards (presentation only; the combat rules never read it). The
    /// panels face each other just above the command dock: the player's queue (the viewer's, in versus) on the left, the
    /// opponent's on the right, mirrored round the centre seam. Slot 1 is the card nearest the seam on both sides and later
    /// slots go outward, so slot k stands at the same distance from the centre on both sides and the pairs read as mirror
    /// images. Both rows share one readable card size and spacing; a long queue shows a shared window of slots. After
    /// each resolved slot both rows slide one step toward the seam, so the pair being resolved is always the innermost. The
    /// panels can keep the fighters' feet clear of them by moving the arena camera down (<see cref="CameraCeiling"/>). Lengths
    /// are HUD units at the 1920x1080 reference; slides are in slots.</summary>
    public static class LegacyQueuePanelLayout
    {
        /// <summary>The defaults the presentation settings start from: one panel's width, a card's side, the full spacing
        /// between neighbouring cards, the gap between the two panels at the seam, and a slide's battle seconds.</summary>
        public const float DefaultPanelWidth = 560f, DefaultCardSize = 48f, DefaultCardSpacing = 54f, DefaultSeamGap = 56f,
            DefaultSlideSeconds = .22f;
        /// <summary>Long queues shrink together only this far; a window then keeps every shown icon readable and separate.</summary>
        public const float MinimumCardSize = 40f, MinimumCardGap = 4f;
        /// <summary>The panels' gap over the dock while planning (clear of the timer gear riding its top edge), their bottom
        /// over the screen's while the dock is folded away (a resolving turn: they settle over the lower letterbox band, just
        /// above the step hint's line at 19 to 41), and the room kept between their top and the fighters' feet.</summary>
        public const float DefaultDockGap = 8f, DefaultResolvingBottom = 46f, DefaultFighterClearance = 12f;
        /// <summary>The closest neighbouring cards ever come: a row too long for its panel overlaps rather than leave it.</summary>
        public const float MinimumSpacing = 12f;
        /// <summary>A panel's margin round its cards.</summary>
        public const float Padding = 6f;
        /// <summary>The most slots one slide carries: several slots consumed at once (a skipped stretch) snap the rest.</summary>
        public const float MaximumSlide = 2f;

        /// <summary>The shared card side for a turn's full queue. It fits as many as possible at the requested proportions,
        /// down to <see cref="MinimumCardSize"/>; longer queues use <see cref="Capacity"/> rather than overlap.</summary>
        public static float CardSize(float room, int count, float preferredSize, float preferredSpacing)
        {
            preferredSize = Math.Max(MinimumCardSize, Finite(preferredSize, DefaultCardSize));
            room = Math.Max(1f, Finite(room, preferredSize));
            if (count < 2) return Math.Min(room, preferredSize);
            float ratio = Math.Max(1f + MinimumCardGap / preferredSize,
                Finite(preferredSpacing, DefaultCardSpacing) / preferredSize);
            float fitted = room / (1f + (count - 1) * ratio);
            return Math.Min(room, Math.Min(preferredSize, Math.Max(MinimumCardSize, fitted)));
        }

        /// <summary>The readable spacing for a card size: the configured proportions with at least a small clear gap.</summary>
        public static float ReadableSpacing(float cardSize, float preferredSize, float preferredSpacing)
        {
            cardSize = Math.Max(1f, Finite(cardSize, DefaultCardSize));
            preferredSize = Math.Max(1f, Finite(preferredSize, DefaultCardSize));
            return Math.Max(cardSize + MinimumCardGap,
                Math.Max(0f, Finite(preferredSpacing, DefaultCardSpacing)) * cardSize / preferredSize);
        }

        /// <summary>The number of separate cards that fit the panel. A partial last card is never included.</summary>
        public static int Capacity(float room, float cardSize, float spacing)
        {
            room = Math.Max(1f, Finite(room, cardSize));
            cardSize = Math.Max(1f, Finite(cardSize, DefaultCardSize));
            spacing = Math.Max(cardSize + MinimumCardGap, Finite(spacing, DefaultCardSpacing));
            return Math.Max(1, 1 + (int)Math.Floor(Math.Max(0f, room - cardSize) / spacing + .0001f));
        }

        /// <summary>A shared absolute slot window: follows the current resolution, inspected slot, or newest reservation.
        /// Earlier consumed slots never return, and both sides keep identical logical indices opposite each other.</summary>
        public static int WindowStart(int count, int consumed, int capacity, int focus)
        {
            count = Math.Max(0, count);
            consumed = Math.Min(count, Math.Max(0, consumed));
            capacity = Math.Max(1, capacity);
            if (count - consumed <= capacity) return consumed;
            focus = Math.Max(consumed, Math.Min(count - 1, focus));
            return Math.Min(count - capacity, Math.Max(consumed, focus - capacity + 1));
        }

        /// <summary>The spacing both rows use for the longer row's <paramref name="count"/> cards of
        /// <paramref name="cardSize"/> in <paramref name="room"/> (a panel's width less its padding): the full
        /// <paramref name="fullSpacing"/> while they fit, tighter down to <see cref="MinimumSpacing"/> when not.</summary>
        public static float Spacing(float room, float cardSize, int count, float fullSpacing)
        {
            fullSpacing = Finite(fullSpacing, DefaultCardSpacing);
            if (fullSpacing < MinimumSpacing) fullSpacing = MinimumSpacing;
            if (count < 2) return fullSpacing;
            float fitted = (Finite(room, 0f) - Finite(cardSize, 0f)) / (count - 1);
            return fitted >= fullSpacing ? fullSpacing : fitted <= MinimumSpacing ? MinimumSpacing : fitted;
        }

        /// <summary>How far from the seam the centre of a row's <paramref name="index"/>-th shown card stands (positive; the
        /// player's row is the mirror image, to the left): the first card at <paramref name="inner"/>, each later one a
        /// <paramref name="spacing"/> further out, and the whole row <paramref name="slide"/> slots further out while it is
        /// still sliding in.</summary>
        public static float CardDistance(int index, float inner, float spacing, float slide)
            => inner + (Math.Max(0, index) + Math.Max(0f, Finite(slide, 0f))) * spacing;

        /// <summary>The slots a row has still to slide, <paramref name="elapsed"/> battle seconds into a slide that began
        /// <paramref name="from"/> slots out and takes <paramref name="seconds"/>: it eases out to 0 (at once when the slide
        /// takes no time).</summary>
        public static float Slide(float from, float elapsed, float seconds)
        {
            from = Math.Max(0f, Finite(from, 0f));
            if (from <= 0f || !(seconds > 0f) || float.IsInfinity(seconds)) return 0f;
            float t = Finite(elapsed, 0f) / seconds;
            if (t <= 0f) return from;
            if (t >= 1f) return 0f;
            return from * (1f - t) * (1f - t);
        }

        /// <summary>Where a slide starts when the slots consumed so far go from <paramref name="consumedBefore"/> to
        /// <paramref name="consumedNow"/> while the rows are still <paramref name="currentSlide"/> slots out: one more slot
        /// out for each slot just consumed (at most <see cref="MaximumSlide"/>), or nothing when the count went back (a new
        /// turn, a reset): the rows snap home.</summary>
        public static float SlideStart(float currentSlide, int consumedBefore, int consumedNow)
        {
            if (consumedNow <= consumedBefore) return consumedNow == consumedBefore ? Math.Max(0f, Finite(currentSlide, 0f)) : 0f;
            float start = Math.Max(0f, Finite(currentSlide, 0f)) + (consumedNow - consumedBefore);
            return start > MaximumSlide ? MaximumSlide : start;
        }

        /// <summary>The highest an orthographic camera of half-height <paramref name="size"/> may centre itself (world y) and
        /// still show <paramref name="groundY"/> at or above <paramref name="floorShare"/> of the screen's height from its
        /// bottom. A share of 0 or less asks nothing (positive infinity).</summary>
        public static float CameraCeiling(float groundY, float size, float floorShare)
        {
            if (!(floorShare > 0f) || !(size > 0f) || float.IsNaN(groundY)) return float.PositiveInfinity;
            if (floorShare > 1f) floorShare = 1f;
            return groundY + size * (1f - 2f * floorShare);
        }

        private static float Finite(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
