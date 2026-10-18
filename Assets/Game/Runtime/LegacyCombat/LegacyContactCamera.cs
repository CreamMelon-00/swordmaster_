using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Presentation only. A contact gets real time to read and at least one camera sample, without delaying combat.</summary>
    public struct LegacyContactReadWindow
    {
        public const double Duration = .14;
        private double remaining;
        private bool pending, needsFrame;
        public bool IsReading => pending || needsFrame || remaining > 0d;
        public void AwaitImpact() { pending = true; needsFrame = false; remaining = 0d; }
        public void ObserveImpact() { pending = false; needsFrame = true; remaining = Duration; }
        public void Advance(double realDelta)
        {
            if (!double.IsNaN(realDelta) && !double.IsInfinity(realDelta) && realDelta > 0d)
                remaining = Math.Max(0d, remaining - realDelta);
        }
        public void FrameShown() => needsFrame = false;
        public void ReleasePending() => pending = false;
        public void Clear() { pending = needsFrame = false; remaining = 0d; }
    }

    public readonly struct LegacyContactFrame
    {
        public LegacyContactFrame(double x, double y, double size) { X = x; Y = y; Size = size; }
        public double X { get; }
        public double Y { get; }
        public double Size { get; }
    }

    /// <summary>Fits the active pair's authored body/foot envelope, not the sprites' transparent frame rectangles.
    /// The safe viewport leaves five percent at the sides/top and three percent over the HUD.</summary>
    public static class LegacyContactCamera
    {
        public const double SideMargin = .05, TopMargin = .05, BottomMargin = .03;
        public const double MaximumHudInset = .87;

        public static LegacyContactFrame Fit(double cameraX, double cameraY, double desiredSize, double aspect,
            double minX, double maxX, double minY, double maxY, double hudInset = 0d, double rollDegrees = 0d)
        {
            cameraX = Coordinate(cameraX, 0d);
            cameraY = Coordinate(cameraY, 0d);
            desiredSize = Finite(desiredSize) && desiredSize > 0d ? Clamp(desiredSize, 1e-6, 1e30) : 5d;
            aspect = Finite(aspect) && aspect > 0d ? Clamp(aspect, 1e-4, 1e4) : 16d / 9d;
            minX = Coordinate(minX, cameraX); maxX = Coordinate(maxX, cameraX);
            minY = Coordinate(minY, cameraY); maxY = Coordinate(maxY, cameraY);
            Order(ref minX, ref maxX); Order(ref minY, ref maxY);
            hudInset = Finite(hudInset) ? Clamp(hudInset, 0d, MaximumHudInset) : 0d;
            double angle = (Finite(rollDegrees) ? rollDegrees % 360d : 0d) * Math.PI / 180d;
            double cosine = Math.Cos(angle), sine = Math.Sin(angle);
            double centerX = (minX + maxX) * .5d, centerY = (minY + maxY) * .5d;
            double halfX = (maxX - minX) * .5d, halfY = (maxY - minY) * .5d;
            double extentX = Math.Abs(cosine) * halfX + Math.Abs(sine) * halfY;
            double extentY = Math.Abs(sine) * halfX + Math.Abs(cosine) * halfY;
            double horizontalShare = 1d - 2d * SideMargin;
            double verticalShare = 1d - TopMargin - BottomMargin - hudInset;
            double size = Math.Max(desiredSize, Math.Max(extentX / (aspect * horizontalShare), extentY / verticalShare));
            double offsetX = cameraX - centerX, offsetY = cameraY - centerY;
            double localX = cosine * offsetX + sine * offsetY, localY = -sine * offsetX + cosine * offsetY;
            double lowerY = -1d + 2d * (hudInset + BottomMargin), upperY = 1d - 2d * TopMargin;
            double fittedX = Nearest(localX, extentX - size * aspect * horizontalShare, -extentX + size * aspect * horizontalShare);
            double fittedY = Nearest(localY, extentY - size * upperY, -extentY - size * lowerY);
            if (fittedX == localX && fittedY == localY) return new LegacyContactFrame(cameraX, cameraY, size);
            return new LegacyContactFrame(centerX + cosine * fittedX - sine * fittedY,
                centerY + sine * fittedX + cosine * fittedY, size);
        }

        private static double Nearest(double value, double min, double max) => min > max ? (min + max) * .5d : Clamp(value, min, max);
        private static double Coordinate(double value, double fallback) => Finite(value) ? Clamp(value, -1e30, 1e30) : fallback;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
        private static void Order(ref double min, ref double max)
        {
            if (min <= max) return;
            double previous = min; min = max; max = previous;
        }
    }
}
