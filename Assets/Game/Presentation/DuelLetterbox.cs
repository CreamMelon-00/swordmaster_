using System;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Black letterbox bars over the battle for its cinematic moments (the finishing blow, the start card): they
    /// slide in from the top and bottom edges as <see cref="SetAmount"/> rises. Its own overlay canvas sits above the duel
    /// HUD (100) and the coach (300) and below the result (400) and the cutscene layers (450), so the battle's picture is
    /// framed while its screens still come over it. It never takes clicks.</summary>
    public sealed class DuelLetterbox : IDisposable
    {
        public const int SortingOrder = 350;
        /// <summary>Each bar's height at the 1920x1080 reference, as the cutscene's.</summary>
        public const float BarHeight = CutsceneHud.BarHeight;
        private readonly RectTransform root, top, bottom;
        private bool disposed;

        public DuelLetterbox(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            root = Rect("Duel Letterbox", parent);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            top = Bar("Letterbox Top", true);
            bottom = Bar("Letterbox Bottom", false);
            SetAmount(0f);
        }

        public GameObject Root => root.gameObject;
        /// <summary>0 = no bars, 1 = fully in.</summary>
        public float Amount { get; private set; }
        public bool IsVisible => !disposed && Amount > 0f;

        public void SetAmount(float amount)
        {
            if (disposed) return;
            Amount = float.IsNaN(amount) ? 0f : Mathf.Clamp01(amount);
            float offset = BarHeight * (1f - Amount);
            top.anchoredPosition = new Vector2(0f, offset);
            bottom.anchoredPosition = new Vector2(0f, -offset);
            root.gameObject.SetActive(Amount > 0f);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private RectTransform Bar(string name, bool atTop)
        {
            var bar = Rect(name, root).gameObject.AddComponent<Image>();
            bar.color = Color.black;
            bar.raycastTarget = false;
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, atTop ? 1f : 0f);
            rect.pivot = new Vector2(.5f, atTop ? 1f : 0f);
            rect.sizeDelta = new Vector2(0f, BarHeight);
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            return rect;
        }
    }
}
