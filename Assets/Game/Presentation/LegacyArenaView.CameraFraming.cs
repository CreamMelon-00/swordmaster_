using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private void LiftOrdinaryCameraFraming(ref Vector3 pivot, ref float size)
        {
            float lift = settings.CameraFramingLift;
            if (lift <= 0f || suspension != null || IsFatalFocus || cutInAmount > .001f || StepFocusAmount > .001f) return;
            // Raising a camera already resting against the HUD ceiling would otherwise be canceled by its final fit.
            // Start from that safe, unraised shot; widen only when the same feet/HUD envelope needs more room above it.
            bool fitContact = resolving;
            Rect bounds = default;
            float roll = ArenaCamera.transform.localEulerAngles.z;
            if (fitContact)
            {
                bounds = ContactReadBounds;
                LegacyContactFrame baseline = LegacyContactCamera.Fit(pivot.x, pivot.y, size, ArenaCamera.aspect,
                    bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax, hudFloor, roll);
                pivot.x = (float)baseline.X; pivot.y = (float)baseline.Y; size = (float)baseline.Size;
            }
            // A HUD occupying half the image has no positive lower half in which widening can expose the feet.
            // Preserve its existing safe framing instead of trading legibility for the requested lift.
            if (hudFloor >= .5f - (float)LegacyContactCamera.BottomMargin) return;
            pivot.y += lift;
            if (hudFloor > 0f)
            {
                float lowest = FeetY, inset = hudFloor;
                if (depthPresentationVisible && !IsReadingContact)
                {
                    lowest = Mathf.Min(lowest, LowestGroundVisualY);
                    inset += .01f;
                }
                size = Mathf.Max(size, (pivot.y - lowest) / Mathf.Max(.02f, 1f - 2f * inset));
            }
            if (!fitContact) return;

            // The existing contact fit also allows camera roll. Size the same rotated envelope around the raised
            // target, so its final safety pass need not lower the camera again. No body/foot margins are removed.
            float angle = roll * Mathf.Deg2Rad, cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            Vector2 relative = bounds.center - new Vector2(pivot.x, pivot.y);
            float centerY = -sine * relative.x + cosine * relative.y;
            float halfHeight = Mathf.Abs(sine) * bounds.width * .5f + Mathf.Abs(cosine) * bounds.height * .5f;
            float topShare = 1f - 2f * (float)LegacyContactCamera.TopMargin;
            float bottomShare = 1f - 2f * (hudFloor + (float)LegacyContactCamera.BottomMargin);
            size = Mathf.Max(size, Mathf.Max((centerY + halfHeight) / topShare, (halfHeight - centerY) / bottomShare));
        }
    }
}
