using System;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The cutscene's screen layers over the arena: a full-screen image, letterbox bars, a black fade and the
    /// skip hint. It sits above the duel and result screens and below the dialogue box (500), so lines can be read
    /// on black. It never takes clicks.</summary>
    public sealed class CutsceneHud : IDisposable
    {
        public const int SortingOrder = 450;
        public const float BarHeight = 110f;
        private readonly RectTransform root;
        private readonly Image image, fade;
        private readonly RectTransform topBar, bottomBar;
        private readonly AspectRatioFitter imageFitter;
        private bool disposed;

        public CutsceneHud(Transform parent, LegacyDuelArt art)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (art == null) throw new ArgumentNullException(nameof(art));
            root = Rect("Cutscene HUD", parent);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            var imageFrame = Rect("Cutscene Image Frame", root);
            Stretch(imageFrame);
            image = Graphic<Image>("Cutscene Image", imageFrame);
            image.preserveAspect = false;
            imageFitter = image.gameObject.AddComponent<AspectRatioFitter>();
            imageFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            topBar = Bar("Cutscene Bar Top", true);
            bottomBar = Bar("Cutscene Bar Bottom", false);

            fade = Graphic<Image>("Cutscene Fade", root);
            Stretch(fade.rectTransform);
            fade.color = Color.black;

            var hint = Graphic<Text>("Cutscene Skip Hint", root);
            hint.font = art.UIFont;
            hint.text = "Esc  건너뛰기";
            hint.fontSize = 20;
            hint.alignment = TextAnchor.MiddleRight;
            hint.color = new Color(1f, 1f, 1f, .55f);
            hint.supportRichText = false;
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = Vector2.one;
            hintRect.anchoredPosition = new Vector2(-36f, -28f);
            hintRect.sizeDelta = new Vector2(300f, 36f);
            Hide();
        }

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        /// <summary>0 = the scene shows, 1 = black.</summary>
        public float FadeAmount => fade.color.a;
        /// <summary>0 = no bars, 1 = fully in.</summary>
        public float BarsAmount { get; private set; }
        public Sprite ImageSprite => image.sprite;
        public float ImageAmount => image.sprite == null ? 0f : image.color.a;

        public void Show()
        {
            if (disposed) return;
            SetFade(0f);
            SetBars(0f);
            SetImage(null, 0f);
            root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (disposed) return;
            SetImage(null, 0f);
            root.gameObject.SetActive(false);
        }

        public void SetFade(float amount)
        {
            if (disposed) return;
            fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(amount));
            fade.enabled = fade.color.a > 0f;
        }

        public void SetBars(float amount)
        {
            if (disposed) return;
            BarsAmount = Mathf.Clamp01(amount);
            float offset = BarHeight * (1f - BarsAmount);
            topBar.anchoredPosition = new Vector2(0f, offset);
            bottomBar.anchoredPosition = new Vector2(0f, -offset);
            topBar.gameObject.SetActive(BarsAmount > 0f);
            bottomBar.gameObject.SetActive(BarsAmount > 0f);
        }

        /// <summary>Shows <paramref name="sprite"/> filling the screen at <paramref name="amount"/> opacity, or nothing.</summary>
        public void SetImage(Sprite sprite, float amount)
        {
            if (disposed) return;
            image.sprite = sprite;
            if (sprite != null && sprite.rect.height > 0f) imageFitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            image.color = new Color(1f, 1f, 1f, sprite == null ? 0f : Mathf.Clamp01(amount));
            image.enabled = sprite != null && image.color.a > 0f;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            image.sprite = null;
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private RectTransform Bar(string name, bool top)
        {
            var bar = Graphic<Image>(name, root);
            bar.color = Color.black;
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(.5f, top ? 1f : 0f);
            rect.sizeDelta = new Vector2(0f, BarHeight);
            return rect;
        }

        private static T Graphic<T>(string name, Transform parent) where T : MaskableGraphic
        {
            var graphic = Rect(name, parent).gameObject.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
