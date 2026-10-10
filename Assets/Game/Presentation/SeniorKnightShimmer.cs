using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>A brief wavering echo around the senior knight's layered cutscene figure. Four faint copies of the
    /// current upper and lower cels shift and breathe around the stationary original. The director owns and clocks it;
    /// it creates no materials or textures and leaves the figure's pose, place and power aura untouched.</summary>
    public sealed class SeniorKnightShimmer : IDisposable
    {
        private const float FadeInSeconds = .14f;
        private const float FadeOutSeconds = .22f;
        private readonly SpriteRenderer upper;
        private readonly SpriteRenderer lower;
        private readonly GameObject root;
        private readonly SpriteRenderer[] echoes = new SpriteRenderer[4];
        private float elapsed;
        private float duration;
        private bool disposed;

        public SeniorKnightShimmer(SpriteRenderer upper, SpriteRenderer lower)
        {
            this.upper = upper != null ? upper : throw new ArgumentNullException(nameof(upper));
            this.lower = lower != null ? lower : throw new ArgumentNullException(nameof(lower));
            root = new GameObject("Senior Knight Shimmer") { layer = upper.gameObject.layer };
            root.transform.SetParent(upper.transform, false);
            for (int index = 0; index < echoes.Length; index++)
            {
                SpriteRenderer source = index % 2 == 0 ? upper : lower;
                var node = new GameObject(index % 2 == 0 ? "Upper Echo" : "Lower Echo")
                {
                    layer = upper.gameObject.layer,
                };
                node.transform.SetParent(root.transform, false);
                SpriteRenderer echo = node.AddComponent<SpriteRenderer>();
                echo.sortingLayerID = source.sortingLayerID;
                echo.sortingOrder = source.sortingOrder + (index < 2 ? -1 : 1);
                if (source.sharedMaterial != null) echo.sharedMaterial = source.sharedMaterial;
                echoes[index] = echo;
            }
            root.SetActive(false);
        }

        public bool IsActive => !disposed && duration > 0f;
        public Transform Root => disposed ? null : root.transform;

        /// <summary>A new shimmer replaces one in progress. Its duration is independent of dialogue advance.</summary>
        public void Start(float seconds)
        {
            if (disposed) return;
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            duration = seconds;
            elapsed = 0f;
            root.SetActive(true);
        }

        public void Tick(float seconds)
        {
            if (!IsActive) return;
            if (seconds > 0f && !float.IsInfinity(seconds) && !float.IsNaN(seconds))
                elapsed = Mathf.Min(duration, elapsed + seconds);
            if (elapsed >= duration) Stop();
        }

        /// <summary>Copies the figure's current animation cels after the director has applied its pose for this frame.</summary>
        public void Render()
        {
            if (!IsActive) return;
            if (upper == null || lower == null || upper.sprite == null || lower.sprite == null)
            {
                Stop();
                return;
            }
            float envelope = Mathf.Min(1f, Mathf.Min(elapsed / FadeInSeconds, (duration - elapsed) / FadeOutSeconds));
            envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(envelope));
            for (int index = 0; index < echoes.Length; index++)
            {
                SpriteRenderer source = index % 2 == 0 ? upper : lower;
                SpriteRenderer echo = echoes[index];
                float phase = index < 2 ? .5f : 2.8f;
                float sway = Mathf.Sin(elapsed * 17f + phase) * .055f +
                    Mathf.Sin(elapsed * 31f + phase * .7f) * .024f;
                float breathe = Mathf.Sin(elapsed * 12f + phase) * .012f;
                echo.sprite = source.sprite;
                echo.flipX = source.flipX;
                echo.transform.localPosition = (source == upper ? Vector3.zero : source.transform.localPosition) +
                    new Vector3(sway, Mathf.Sin(elapsed * 9f + phase) * .012f, 0f);
                echo.transform.localScale = new Vector3(1f + breathe, 1f - breathe * .4f, 1f);
                float alpha = (index < 2 ? .23f : .11f) * envelope;
                echo.color = index < 2
                    ? new Color(.78f, .91f, 1f, alpha)
                    : new Color(1f, 1f, 1f, alpha);
            }
        }

        public void Stop()
        {
            if (disposed) return;
            duration = elapsed = 0f;
            root.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
        }
    }
}