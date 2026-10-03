using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A minimal title: 이어하기 (with a one-line summary of the save) and 새 게임. When a save exists,
    /// 새 게임 asks for confirmation first because it overwrites that save. It shows state and forwards requests.</summary>
    public sealed class TitleHud : IDisposable
    {
        public const int SortingOrder = 260;
        public const string GameTitle = "Turn Limbo";
        // Retained for old content that addresses the previous room directly.
        public const string BackgroundResource = "LobbyRoom/room";
        private readonly LegacyDuelArt art;
        private readonly Action continueGame, newGame;
        private readonly RectTransform root;
        private readonly Text summary, notice, continueCaption, newGameCaption;
        private readonly Button continueButton, newGameButton, confirmButton, cancelButton;
        private readonly GameObject confirmPanel;
        private bool confirmNewGame, disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public bool CanContinue => IsVisible && continueButton.interactable;
        public bool IsConfirming => IsVisible && confirmPanel.activeSelf;
        public Button ContinueButton => continueButton;
        public Button NewGameButton => newGameButton;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;

        public TitleHud(Transform parent, LegacyDuelArt art, Action continueGame, Action newGame,
            Sprite selectedRoomSprite = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.continueGame = continueGame;
            this.newGame = newGame;
            root = Rect("Title HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();

            Stretch(Panel("Title Backdrop", root, Vector2.zero, Vector2.zero, DuelVisualTheme.Surface).rectTransform);
            Sprite room = selectedRoomSprite != null ? selectedRoomSprite : LobbyRoomBackdrop.PickRandom();
            var background = Panel("Title Background", root, Vector2.zero, new Vector2(1920f, 1080f), Color.white);
            background.sprite = room;
            background.enabled = room != null;
            var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = room != null && room.rect.height > 0f ? room.rect.width / room.rect.height : 16f / 9f;
            Color shade = DuelVisualTheme.Track;
            shade.a = .62f;
            Stretch(Panel("Title Shade", root, Vector2.zero, Vector2.zero, shade).rectTransform);

            Label("Title Name", root, new Vector2(0f, 210f), new Vector2(1200f, 130f), 104, DuelVisualTheme.Foreground).text = GameTitle;
            Label("Title Subtitle", root, new Vector2(0f, 120f), new Vector2(900f, 40f), 30, DuelVisualTheme.Muted).text = "검술 클럽";

            continueButton = ActionButton("Title Continue", root, "이어하기  [Enter]", new Vector2(0f, -60f), new Vector2(460f, 76f),
                () => this.continueGame?.Invoke(), true);
            summary = Label("Title Save Summary", root, new Vector2(0f, -120f), new Vector2(900f, 32f), 22, DuelVisualTheme.Muted);
            continueCaption = continueButton.GetComponentInChildren<Text>();
            newGameButton = ActionButton("Title New Game", root, "새 게임", new Vector2(0f, -200f), new Vector2(460f, 68f),
                PressNewGame);
            newGameCaption = newGameButton.GetComponentInChildren<Text>();
            notice = Label("Title Notice", root, new Vector2(0f, -280f), new Vector2(1100f, 64f), 20, DuelVisualTheme.Danger);

            var veil = Panel("Title Confirm", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .78f));
            Stretch(veil.rectTransform);
            veil.raycastTarget = true;
            confirmPanel = veil.gameObject;
            var border = Panel("Confirm Border", veil.transform, Vector2.zero, new Vector2(764f, 284f), DuelVisualTheme.Border);
            var card = Panel("Confirm Card", border.transform, Vector2.zero, new Vector2(760f, 280f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(card);
            Label("Confirm Message", card.transform, new Vector2(0f, 52f), new Vector2(680f, 110f), 24, DuelVisualTheme.Foreground)
                .text = "저장된 진행을 지우고\n서막 1 임무부터 새로 시작합니다.";
            confirmButton = ActionButton("Confirm New Game", card.transform, "새로 시작", new Vector2(-132f, -76f),
                new Vector2(224f, 58f), ConfirmNewGame, true);
            cancelButton = ActionButton("Confirm Cancel", card.transform, "취소  [Esc]", new Vector2(132f, -76f),
                new Vector2(224f, 58f), CancelConfirm);
            confirmPanel.SetActive(false);
            Hide();
        }

        /// <param name="continueSummary">One line describing the save to continue, or null when there is none.</param>
        /// <param name="noticeText">A warning such as an unreadable save, or null.</param>
        /// <param name="confirmBeforeNewGame">Whether 새 게임 overwrites a save and must be confirmed first.</param>
        public void Show(string continueSummary, string noticeText, bool confirmBeforeNewGame)
        {
            if (disposed) return;
            root.gameObject.SetActive(true);
            confirmPanel.SetActive(false);
            bool canContinue = continueSummary != null;
            continueButton.interactable = canContinue;
            // Enter continues when it can and otherwise presses 새 게임, so the hint follows it.
            continueCaption.text = canContinue ? "이어하기  [Enter]" : "이어하기";
            newGameCaption.text = canContinue ? "새 게임" : "새 게임  [Enter]";
            summary.text = continueSummary ?? (noticeText != null ? "이어할 수 있는 저장이 없습니다." : "저장된 진행이 없습니다.");
            notice.text = noticeText ?? string.Empty;
            confirmNewGame = confirmBeforeNewGame;
            ClearSelection();
        }

        /// <summary>The 새 게임 action: asks first when a save would be overwritten.</summary>
        public void PressNewGame()
        {
            if (!IsVisible || IsConfirming) return;
            if (confirmNewGame)
            {
                confirmPanel.SetActive(true);
                ClearSelection();
                return;
            }
            newGame?.Invoke();
        }

        public void CancelConfirm()
        {
            if (disposed) return;
            confirmPanel.SetActive(false);
            ClearSelection();
        }

        private void ConfirmNewGame()
        {
            if (!IsConfirming) return;
            confirmPanel.SetActive(false);
            newGame?.Invoke();
        }

        public void Hide()
        {
            if (disposed) return;
            confirmPanel.SetActive(false);
            root.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            continueButton.onClick.RemoveAllListeners();
            newGameButton.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position, Vector2 size,
            Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size, primary ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label("Button Label", image.transform, Vector2.zero, size - new Vector2(16f, 8f), 26,
                primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground).text = caption;
            button.onClick.AddListener(() =>
            {
                ClearSelection();
                action?.Invoke();
            });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = art.UIFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
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

    /// <summary>The four views of the same cabin; choose once when the game UI is created.</summary>
    internal static class LobbyRoomBackdrop
    {
        private static readonly string[] VariantPaths =
        {
            "LobbyRoom/morning",
            "LobbyRoom/day",
            "LobbyRoom/evening",
            "LobbyRoom/night"
        };

        internal static Sprite PickRandom()
            => Resources.Load<Sprite>(VariantPaths[UnityEngine.Random.Range(0, VariantPaths.Length)]);
    }
}
