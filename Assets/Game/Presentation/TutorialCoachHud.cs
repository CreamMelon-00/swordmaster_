using System;
using TurnLimbo.Runtime.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A reusable, non-modal coach. Only its actual buttons intercept battle input.</summary>
    public sealed class TutorialCoachHud : IDisposable
    {
        private readonly LegacyDuelArt art;
        private readonly RectTransform root;
        private readonly Text counter, title, description, inputHint, continueCaption;
        private readonly Button continueButton, skipButton, inspectButton;
        private int shownStep = -1, shownCount = -1;
        private bool disposed;
        private static readonly Color Surface = DuelVisualTheme.Surface;
        private static readonly Color Raised = DuelVisualTheme.RaisedSurface;
        private static readonly Color Accent = DuelVisualTheme.Accent;
        private static readonly Color Foreground = DuelVisualTheme.Foreground;
        private static readonly Color Muted = DuelVisualTheme.Muted;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;

        public TutorialCoachHud(Transform parent, LegacyDuelArt art, Action advance, Action skip, Action inspectEnemy = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            root = Rect("Tutorial Coach HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            // Keep the lesson below the actors and above the 200px input dock.
            // The queues and status bars move with the fighters and must remain visible while being taught.
            var border = Panel("Tutorial Coach Border", root, new Vector2(0f, 320f), new Vector2(904f, 204f), Accent);
            border.rectTransform.anchorMin = border.rectTransform.anchorMax = new Vector2(.5f, 0f);
            var card = Panel("Tutorial Coach Card", border.transform, Vector2.zero, new Vector2(900f, 200f), Surface);
            DuelVisualTheme.DressPanel(card);
            counter = Label("Tutorial Step", card.transform, new Vector2(0f, 76f), new Vector2(848f, 20f), 16, Muted);
            title = Label("Tutorial Title", card.transform, new Vector2(0f, 49f), new Vector2(848f, 32f), 26);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 18;
            title.resizeTextMaxSize = 26;
            description = Label("Tutorial Description", card.transform, new Vector2(0f, 2f), new Vector2(848f, 56f), 17);
            description.alignment = TextAnchor.UpperLeft;
            description.resizeTextForBestFit = true;
            description.resizeTextMinSize = 16;
            description.resizeTextMaxSize = 17;
            inputHint = Label("Tutorial Input Hint", card.transform, new Vector2(0f, -41f), new Vector2(848f, 22f), 16, Accent);
            continueButton = ActionButton("Tutorial Continue", card.transform, "다음", new Vector2(-344f, -76f), new Vector2(136f, 32f), advance, true);
            continueCaption = continueButton.GetComponentInChildren<Text>();
            inspectButton = ActionButton("Tutorial Inspect Enemy", card.transform, "적 큐 확인 [Tab]", new Vector2(-276f, -76f), new Vector2(272f, 32f), inspectEnemy, true);
            skipButton = ActionButton("Tutorial Skip", card.transform, "연습 종료", new Vector2(344f, -76f), new Vector2(136f, 32f), skip);
            Hide();
        }

        public void Show(TutorialProgress progress)
        {
            if (disposed) return;
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            root.gameObject.SetActive(true);
            // Controller may refresh this view frequently. Keep both objects and strings stable until the step changes.
            if (shownStep != progress.StepNumber || shownCount != progress.StepCount)
            {
                shownStep = progress.StepNumber;
                shownCount = progress.StepCount;
                counter.text = $"연습 {shownStep:00} / {shownCount:00}";
            }
            if (title.text != progress.Title) title.text = progress.Title;
            if (description.text != progress.Description) description.text = progress.Description;
            if (inputHint.text != progress.InputHint) inputHint.text = progress.InputHint;
            continueButton.gameObject.SetActive(progress.CanAdvance);
            string caption = progress.Step == TutorialStep.Welcome ? "연습 시작" : "계속";
            if (continueCaption.text != caption) continueCaption.text = caption;
            inspectButton.gameObject.SetActive(progress.Step == TutorialStep.InspectEnemy);
        }

        public void Hide()
        {
            if (!disposed) root.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            continueButton.onClick.RemoveAllListeners();
            skipButton.onClick.RemoveAllListeners();
            inspectButton.onClick.RemoveAllListeners();
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position, Vector2 size, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size, primary ? Accent : Raised);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label("Button Label", image.transform, Vector2.zero, size - new Vector2(12f, 4f), 17,
                primary ? DuelVisualTheme.Ink : Foreground)
                .text = caption;
            button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                action?.Invoke();
            });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color? color = null)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = art.UIFont;
            text.fontSize = fontSize;
            text.color = color ?? Foreground;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
