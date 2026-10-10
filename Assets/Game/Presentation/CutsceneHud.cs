using System;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The cutscene's screen layers over the arena: a full-screen image, a tutorial recall (<c>@recall</c>),
    /// letterbox bars, a black fade and the skip hint. It sits above the duel and result screens and below the dialogue
    /// box (500), so lines can be read on black. It never takes clicks.</summary>
    public sealed class CutsceneHud : IDisposable
    {
        public const int SortingOrder = 450;
        public const float BarHeight = 110f;
        /// <summary>How much larger than in battle a recalled coach card is drawn.</summary>
        public const float RecallCardScale = 1.45f;
        // A memory is washed pale: this light veil lies over the recalled screen or card.
        private static readonly Color RecallVeil = new Color(.93f, .91f, .86f, .2f);
        private readonly RectTransform root;
        private readonly Image image, fade;
        private readonly Text hint;
        private readonly RectTransform topBar, bottomBar;
        private readonly AspectRatioFitter imageFitter;
        private readonly RectTransform recall;
        private readonly CanvasGroup recallGroup;
        private readonly Image recallBackdrop, recallFlash;
        private readonly RawImage recallScreen;
        private readonly AspectRatioFitter recallScreenFitter;
        private readonly MissionCoachCard recallCard;
        private bool disposed;

        public CutsceneHud(Transform parent, LegacyDuelArt art)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (art == null) throw new ArgumentNullException(nameof(art));
            root = Rect("Cutscene HUD", parent);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
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

            // The recall: a captured screen filling the frame, or a coach card redrawn large and centred on the dimmed
            // scene, under a pale veil; one group fades it all. Its white flash is apart, so it can blaze before the
            // memory shows. The bars and the fade stay above it.
            recall = Rect("Cutscene Recall", root);
            Stretch(recall);
            recallGroup = recall.gameObject.AddComponent<CanvasGroup>();
            recallGroup.interactable = false;
            recallGroup.blocksRaycasts = false;
            recallBackdrop = Graphic<Image>("Recall Backdrop", recall);
            Stretch(recallBackdrop.rectTransform);
            recallBackdrop.color = new Color(0f, 0f, 0f, .72f);
            var screenFrame = Rect("Recall Screen Frame", recall);
            Stretch(screenFrame);
            recallScreen = Graphic<RawImage>("Recall Screen", screenFrame);
            recallScreenFitter = recallScreen.gameObject.AddComponent<AspectRatioFitter>();
            recallScreenFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            recallCard = new MissionCoachCard(recall, art.UIFont);
            recallCard.Frame.anchoredPosition = new Vector2(0f, 40f);
            recallCard.Frame.localScale = Vector3.one * RecallCardScale;
            Image veil = Graphic<Image>("Recall Veil", recall);
            Stretch(veil.rectTransform);
            veil.color = RecallVeil;
            recallFlash = Graphic<Image>("Recall Flash", root);
            Stretch(recallFlash.rectTransform);

            topBar = Bar("Cutscene Bar Top", true);
            bottomBar = Bar("Cutscene Bar Bottom", false);

            fade = Graphic<Image>("Cutscene Fade", root);
            Stretch(fade.rectTransform);
            fade.color = Color.black;

            hint = Graphic<Text>("Cutscene Skip Hint", root);
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
        /// <summary>Whether a tutorial recall is up (a captured screen or a redrawn coach card).</summary>
        public bool IsRecalling => !disposed && recall.gameObject.activeSelf;
        /// <summary>The captured screen being recalled, or null (none up, or a redrawn card instead).</summary>
        public Texture RecallScreen => IsRecalling ? recallScreen.texture : null;
        /// <summary>The coach beat redrawn as a card, or null (none up, or a captured screen instead).</summary>
        public TutorialRecallBeat RecallBeat { get; private set; }
        /// <summary>The card a beat is redrawn on, styled as the coach's.</summary>
        public MissionCoachCard RecallCard => recallCard;
        /// <summary>0 = the recall is gone, 1 = fully shown.</summary>
        public float RecallAmount => IsRecalling ? recallGroup.alpha : 0f;
        /// <summary>The recall's white flash: 1 = a white screen.</summary>
        public float RecallFlash => recallFlash.enabled ? recallFlash.color.a : 0f;

        public void SetSkipHint(string caption)
        {
            if (!disposed) hint.text = caption;
        }

        public void Show()
        {
            if (disposed) return;
            hint.text = "Esc  건너뛰기";
            SetFade(0f);
            SetBars(0f);
            SetImage(null, 0f);
            HideRecall();
            root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (disposed) return;
            SetImage(null, 0f);
            HideRecall();
            root.gameObject.SetActive(false);
        }

        /// <summary>Recalls a captured screen: it fills the frame (its aspect kept, the overflow cut). The texture stays
        /// its owner's (<see cref="TutorialRecallAlbum"/>). Shown at <see cref="SetRecall"/>'s amounts.</summary>
        public void ShowRecall(Texture screen)
        {
            if (disposed) return;
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            RecallBeat = null;
            recallScreen.texture = screen;
            if (screen.height > 0) recallScreenFitter.aspectRatio = screen.width / (float)screen.height;
            recallScreen.enabled = true;
            recallBackdrop.enabled = false;
            recallCard.Frame.gameObject.SetActive(false);
            OpenRecall();
        }

        /// <summary>Recalls a coach beat no screen was captured of: redrawn on the coach's card, larger and centred over
        /// the dimmed scene. Shown at <see cref="SetRecall"/>'s amounts.</summary>
        public void ShowRecall(TutorialRecallBeat beat)
        {
            if (disposed) return;
            RecallBeat = beat ?? throw new ArgumentNullException(nameof(beat));
            recallScreen.texture = null;
            recallScreen.enabled = false;
            recallBackdrop.enabled = true;
            recallCard.Show(beat.Beat, beat.StepNumber, beat.StepCount, MissionCoachCard.MissionLabel(beat.MissionNumber));
            recallCard.Frame.gameObject.SetActive(true);
            OpenRecall();
        }

        /// <param name="amount">How much of the recall shows, 0 to 1.</param>
        /// <param name="flash">The white flash over it, 0 to 1.</param>
        public void SetRecall(float amount, float flash)
        {
            if (disposed) return;
            recallGroup.alpha = Mathf.Clamp01(amount);
            recallFlash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flash));
            recallFlash.enabled = recallFlash.color.a > 0f;
        }

        /// <summary>Takes the recall away at once and lets go of its screen.</summary>
        public void HideRecall()
        {
            if (disposed) return;
            RecallBeat = null;
            recallScreen.texture = null;
            recallScreen.enabled = false;
            SetRecall(0f, 0f);
            recall.gameObject.SetActive(false);
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
            recallScreen.texture = null;
            RecallBeat = null;
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private void OpenRecall()
        {
            recall.gameObject.SetActive(true);
            SetRecall(0f, 1f);
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
