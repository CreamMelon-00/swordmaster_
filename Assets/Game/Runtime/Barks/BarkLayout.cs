using System;

namespace TurnLimbo.Runtime.Barks
{
    /// <summary>A box in HUD units, y up: its lower-left corner and its size.</summary>
    public readonly struct BarkBox
    {
        public BarkBox(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = Math.Max(0f, width);
            Height = Math.Max(0f, height);
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => X + Width;
        public float Top => Y + Height;
        public float CenterX => X + Width * .5f;
        public bool IsEmpty => Width <= 0f || Height <= 0f;

        /// <summary>Whether the two share any area (touching edges do not count).</summary>
        public bool Overlaps(BarkBox other) => !IsEmpty && !other.IsEmpty &&
            X < other.Right && other.X < Right && Y < other.Top && other.Y < Top;

        public BarkBox MovedTo(float x, float y) => new BarkBox(x, y, Width, Height);

        public override string ToString() => $"({X:0.#}, {Y:0.#}, {Width:0.#} x {Height:0.#})";
    }

    /// <summary>Where the speech bubbles go on the HUD (presentation only). A bubble never covers a head HUD — a fighter's
    /// status panel and the queue row's place above it, the fighter's own or the other's — nor the other bubble, and stays
    /// on screen.</summary>
    public static class BarkLayout
    {
        /// <summary>The bubble of the fighter whose head is at <paramref name="headX"/>: centred over it and just above every
        /// head HUD under it (its own, and the other fighter's when the bubble reaches over it), on screen. When the screen
        /// has no room above, it goes as high as it can beside its own head HUD, clear of both: on its outer side
        /// (<paramref name="outward"/>: -1 left, 1 right) if that fits, otherwise the inner. With no room there either it
        /// stays above, as high as the screen allows.</summary>
        /// <param name="screen">The visible area, margins already taken off.</param>
        /// <param name="gap">The space kept between a bubble and what it must not touch.</param>
        public static BarkBox Place(float headX, float width, float height, BarkBox ownStack, BarkBox otherStack,
            BarkBox screen, float gap, int outward)
        {
            float x = ClampX(headX - width * .5f, width, screen);
            float y = RiseAbove(x, width, ownStack, otherStack, gap, true, screen.Y);
            if (y + height <= screen.Top) return new BarkBox(x, y, width, height);
            float top = Math.Max(screen.Y, screen.Top - height);
            int first = outward < 0 ? -1 : 1;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? first : -first;
                float besideX = side < 0 ? ownStack.X - gap - width : ownStack.Right + gap;
                if (besideX < screen.X || besideX + width > screen.Right) continue;
                var beside = new BarkBox(besideX, top, width, height);
                if (!beside.Overlaps(ownStack) && !beside.Overlaps(otherStack)) return beside;
            }
            return new BarkBox(x, top, width, height);
        }

        /// <summary>Two bubbles that would overlap (or come within <paramref name="gap"/> side by side) move apart sideways,
        /// around the middle between them, and stay on screen; each then rises again above any head HUD now under it, as
        /// far as the screen allows. Returns whether they moved.</summary>
        public static bool Separate(ref BarkBox first, ref BarkBox second, BarkBox stackA, BarkBox stackB, BarkBox screen,
            float gap)
        {
            if (first.IsEmpty || second.IsEmpty) return false;
            bool sideBySide = first.X < second.Right + gap && second.X < first.Right + gap;
            bool level = first.Y < second.Top && second.Y < first.Top;
            if (!sideBySide || !level) return false;
            bool firstOnLeft = first.CenterX <= second.CenterX;
            BarkBox left = firstOnLeft ? first : second, right = firstOnLeft ? second : first;
            float middle = (left.CenterX + right.CenterX) * .5f;
            float leftX = middle - gap * .5f - left.Width, rightX = middle + gap * .5f;
            if (left.Width + gap + right.Width > screen.Width)
            {
                // Both cannot fit side by side: as far apart as the screen goes.
                leftX = screen.X;
                rightX = screen.Right - right.Width;
            }
            else if (leftX < screen.X)
            {
                // Kept on screen together: what one edge takes, the pair gives up at the other.
                leftX = screen.X;
                rightX = leftX + left.Width + gap;
            }
            else if (rightX + right.Width > screen.Right)
            {
                rightX = screen.Right - right.Width;
                leftX = rightX - gap - left.Width;
            }
            left = Rise(left.MovedTo(leftX, left.Y), stackA, stackB, screen, gap);
            right = Rise(right.MovedTo(rightX, right.Y), stackA, stackB, screen, gap);
            first = firstOnLeft ? left : right;
            second = firstOnLeft ? right : left;
            return true;
        }

        // Up, never down, to clear the head HUDs under it; never off the top of the screen.
        private static BarkBox Rise(BarkBox bubble, BarkBox stackA, BarkBox stackB, BarkBox screen, float gap)
        {
            float y = RiseAbove(bubble.X, bubble.Width, stackA, stackB, gap, false, bubble.Y);
            return bubble.MovedTo(bubble.X, Math.Max(screen.Y, Math.Min(y, screen.Top - bubble.Height)));
        }

        // The lowest bubble bottom, from floor up, that clears the head HUDs reaching under [x, x + width] (the first one
        // always, when asked).
        private static float RiseAbove(float x, float width, BarkBox own, BarkBox other, float gap, bool alwaysOwn, float floor)
        {
            float bottom = floor;
            if (!own.IsEmpty && (alwaysOwn || Under(x, width, own))) bottom = Math.Max(bottom, own.Top + gap);
            if (!other.IsEmpty && Under(x, width, other)) bottom = Math.Max(bottom, other.Top + gap);
            return bottom;
        }

        private static bool Under(float x, float width, BarkBox stack) => stack.X < x + width && x < stack.Right;

        private static float ClampX(float x, float width, BarkBox screen)
            => Math.Max(screen.X, Math.Min(x, screen.Right - width));
    }
}
