using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        public const float ContactReadSeconds = (float)LegacyContactReadWindow.Duration;
        private LegacyContactReadWindow contactRead;
        public bool IsReadingContact => !disposed && suspension == null && contactRead.IsReading;
        /// <summary>The slow clock starts on the hit; the protagonist close-up follows its readable contact.</summary>
        public bool IsCameraCloseUp => IsFatalFocus && !IsReadingContact;

        /// <summary>Stable body, sword and foot margins in arena-local coordinates. Only the player and current enemy
        /// belong to this contact: distant waiting enemies and transparent sprite canvases must not widen it.</summary>
        public Rect ContactReadBounds
        {
            get
            {
                const float side = 2.05f, head = 1.75f, foot = .34f;
                Vector3 p = player.Renderer.transform.localPosition, e = enemy.Renderer.transform.localPosition;
                float bottom = Mathf.Min(arenaRoot.transform.InverseTransformPoint(PlayerGroundPosition).y,
                    arenaRoot.transform.InverseTransformPoint(EnemyGroundPosition).y) - foot;
                return Rect.MinMaxRect(Mathf.Min(p.x, e.x) - side, bottom,
                    Mathf.Max(p.x, e.x) + side, Mathf.Max(p.y, e.y) + head);
            }
        }

        /// <summary>Capture after a resolved hit, including a dodge or non-attack effect, before the slot can complete.</summary>
        public void ObserveCombatContact()
        {
            if (disposed || suspension != null) return;
            contactRead.ObserveImpact();
            // The controllers tick the arena before resolving a hit. Correct an old close-up on this same frame.
            if (resolving || IsFatalFocus)
            {
                RemoveHitShakeOffset();
                FitContactCamera();
            }
        }

        private void FitContactCamera()
        {
            if (disposed || suspension != null || cutInAmount > .001f) return;
            Rect bounds = ContactReadBounds;
            Vector3 position = ArenaCamera.transform.localPosition;
            LegacyContactFrame fitted = LegacyContactCamera.Fit(position.x, position.y, ArenaCamera.orthographicSize,
                ArenaCamera.aspect, bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax, hudFloor,
                ArenaCamera.transform.localEulerAngles.z);
            ArenaCamera.orthographicSize = (float)fitted.Size;
            position.x = (float)fitted.X; position.y = (float)fitted.Y;
            ArenaCamera.transform.localPosition = position;
        }
    }
}
