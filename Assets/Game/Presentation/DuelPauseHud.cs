using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A small modal over the held battle picture. Battle timing belongs to the controller.</summary>
    public sealed class DuelPauseHud : IDisposable
    {
        public const int SortingOrder = 500;

        private readonly LegacyDuelArt art;
        private readonly RectTransform root;
        private readonly Button resumeButton, retryButton, abandonButton;
        private bool disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;

        public DuelPauseHud(Transform parent, LegacyDuelArt art, Action resume, Action retry, Action abandon)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));

            root = Rect("Duel Pause HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();

            Color shade = DuelVisualTheme.Track;
            shade.a = .72f;
            Image backdrop = Panel("Pause Backdrop", root, Vector2.zero, Vector2.zero, shade);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            Image border = Panel("Pause Card Border", root, Vector2.zero, new Vector2(426f, 346f),
                DuelVisualTheme.Border);
            Image card = Panel("Pause Card", border.transform, Vector2.zero, new Vector2(420f, 340f),
                DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(card);

            Label("Pause Title", card.transform, new Vector2(0f, 120f), new Vector2(360f, 48f),
                32, DuelVisualTheme.Foreground).text = "일시정지";
            Image rule = Panel("Pause Rule", card.transform, new Vector2(0f, 88f),
                new Vector2(336f, 2f), DuelVisualTheme.Border);
            rule.raycastTarget = false;

            resumeButton = ActionButton("Pause Resume", card.transform, "계속하기",
                new Vector2(0f, 42f), DuelVisualTheme.Accent, DuelVisualTheme.Ink,
                resume, true);
            retryButton = ActionButton("Pause Retry", card.transform, "재도전",
                new Vector2(0f, -28f), DuelVisualTheme.RaisedSurface, DuelVisualTheme.Foreground,
                retry);
            abandonButton = ActionButton("Pause Abandon", card.transform, "포기",
                new Vector2(0f, -98f), new Color(.35f, .22f, .19f, 1f), DuelVisualTheme.Danger,
                abandon);
            Hide();
        }

        public void Show()
        {
            if (disposed) return;
            root.gameObject.SetActive(true);
            ClearSelection();
        }

        public void Hide()
        {
            if (disposed) return;
            root.gameObject.SetActive(false);
            ClearSelection();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            resumeButton.onClick.RemoveAllListeners();
            retryButton.onClick.RemoveAllListeners();
            abandonButton.onClick.RemoveAllListeners();
            root.gameObject.SetActive(false);
            ClearSelection();
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position,
            Color faceColor, Color labelColor, Action action, bool primary = false)
        {
            var face = Panel(name, parent, position, new Vector2(320f, 54f), faceColor);
            face.raycastTarget = true;
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label(name + " Label", face.transform, Vector2.zero, new Vector2(292f, 42f), 23,
                labelColor).text = caption;
            button.onClick.AddListener(() =>
            {
                if (!IsVisible) return;
                ClearSelection();
                action?.Invoke();
            });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = art.UIFont;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.supportRichText = false;
            return label;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
