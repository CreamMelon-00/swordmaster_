using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Title entry for the campaign, online friend duel and skill tavern experiment.
    /// Starting a new campaign asks for confirmation when it would overwrite a save.</summary>
    public sealed class TitleHud : IDisposable
    {
        public const int SortingOrder = 260;
        public const string GameTitle = "Turn Limbo";
        // Retained for old content that addresses the previous room directly.
        public const string BackgroundResource = "LobbyRoom/room";
        private readonly LegacyDuelArt art;
        private readonly Action continueGame, newGame, onlineVersus, skillTavern;
        private readonly RectTransform root;
        private readonly Text summary, notice, continueCaption, newGameCaption;
        private readonly Button continueButton, newGameButton, onlineVersusButton, skillTavernButton, confirmButton, cancelButton;
        private readonly GameObject confirmPanel;
        private bool confirmNewGame, disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public bool CanContinue => IsVisible && continueButton.interactable;
        public bool IsConfirming => IsVisible && confirmPanel.activeSelf;
        public Button ContinueButton => continueButton;
        public Button NewGameButton => newGameButton;
        public Button OnlineVersusButton => onlineVersusButton;
        public Button SkillTavernButton => skillTavernButton;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;

        public TitleHud(Transform parent, LegacyDuelArt art, Action continueGame, Action newGame,
            Sprite selectedRoomSprite = null, Action onlineVersus = null, Action skillTavern = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.continueGame = continueGame;
            this.newGame = newGame;
            this.onlineVersus = onlineVersus;
            this.skillTavern = skillTavern;
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
            Sprite room = AcademyUiSkin.TitleBackground;
            if (room == null) room = selectedRoomSprite != null ? selectedRoomSprite : LobbyRoomBackdrop.PickRandom();
            var background = Panel("Title Background", root, Vector2.zero, new Vector2(1920f, 1080f), Color.white);
            background.sprite = room;
            background.enabled = room != null;
            var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = room != null && room.rect.height > 0f ? room.rect.width / room.rect.height : 16f / 9f;
            var veilRect = Rect("Title Shade", root, Vector2.zero, Vector2.zero);
            Stretch(veilRect);
            veilRect.gameObject.AddComponent<CanvasRenderer>();
            veilRect.gameObject.AddComponent<AcademyTitleVeil>().raycastTarget = false;

            var column = Rect("Title Menu Column", root, new Vector2(-550f, 0f), new Vector2(600f, 940f));

            var titleName = Label("Title Name", column, new Vector2(0f, 200f), new Vector2(520f, 230f), 104, DuelVisualTheme.Foreground);
            titleName.text = GameTitle.ToUpperInvariant().Replace(" ", "\n");
            titleName.alignment = TextAnchor.MiddleLeft;
            titleName.lineSpacing = .95f;
            var subtitle = Label("Title Subtitle", column, new Vector2(0f, 46f), new Vector2(520f, 40f), 28, DuelVisualTheme.Muted);
            subtitle.text = "기사학교";
            subtitle.alignment = TextAnchor.MiddleLeft;
            AcademyUiSkin.AddCrest(column, new Vector2(-226f, 392f), 64f);
            var overline = Label("Title Overline", column, new Vector2(36f, 392f), new Vector2(420f, 26f), 16, DuelVisualTheme.Accent);
            overline.alignment = TextAnchor.MiddleLeft;
            overline.text = "KNIGHT ACADEMY";
            Panel("Title Rule", column, new Vector2(0f, 4f), new Vector2(520f, 1f), DuelVisualTheme.Border);

            continueButton = ActionButton("Title Continue", column, "이어하기  [Enter]", new Vector2(0f, -68f), new Vector2(520f, 76f),
                () => this.continueGame?.Invoke(), true);
            summary = Label("Title Save Summary", column, new Vector2(0f, -132f), new Vector2(520f, 40f), 18, DuelVisualTheme.Muted);
            summary.alignment = TextAnchor.MiddleLeft;
            continueCaption = continueButton.GetComponentInChildren<Text>();
            newGameButton = ActionButton("Title New Game", column, "새 게임", new Vector2(0f, -212f), new Vector2(520f, 64f),
                PressNewGame);
            newGameCaption = newGameButton.GetComponentInChildren<Text>();
            // Under 새 게임, in the same column.
            onlineVersusButton = ActionButton("Title Online Versus", column, "온라인 친구 대전", new Vector2(0f, -296f),
                new Vector2(520f, 64f), () => this.onlineVersus?.Invoke());
            skillTavernButton = ActionButton("Title Skill Tavern", column, "선술집 실험", new Vector2(0f, -380f),
                new Vector2(520f, 64f), () =>
                {
                    if (IsVisible && !IsConfirming) this.skillTavern?.Invoke();
                });
            skillTavernButton.gameObject.SetActive(skillTavern != null);
            notice = Label("Title Notice", column, new Vector2(0f, skillTavern != null ? -466f : -390f),
                new Vector2(520f, 76f), 18, DuelVisualTheme.Danger);
            notice.alignment = TextAnchor.MiddleLeft;

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
            // The action the player can actually take owns the warm accent.
            continueButton.targetGraphic.color = Color.white;
            continueButton.transform.Find("Primary Marker").gameObject.SetActive(canContinue);
            newGameButton.targetGraphic.color = Color.white;
            newGameButton.transform.Find("Primary Marker").gameObject.SetActive(!canContinue);
            continueCaption.color = canContinue ? DuelVisualTheme.Foreground : DuelVisualTheme.Muted;
            newGameCaption.color = DuelVisualTheme.Foreground;
            // Enter continues when it can and otherwise presses 새 게임, so the hint follows it.
            continueCaption.text = canContinue ? "이어하기  [Enter]" : "이어하기";
            newGameCaption.text = canContinue ? "새 게임" : "새 게임  [Enter]";
            summary.text = continueSummary ?? (noticeText != null ? "불러올 수 있는 저장 데이터가 없습니다." : "저장된 진행이 없습니다.");
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
            onlineVersusButton.onClick.RemoveAllListeners();
            skillTavernButton.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position, Vector2 size,
            Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size, Color.white);
            image.sprite = AcademyUiSkin.MenuPlaque;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = .5f;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            image.transform.Find("Brass Trim").gameObject.SetActive(false);
            var marker = Panel("Primary Marker", image.transform, new Vector2(-size.x * .5f + 62f, 0), new Vector2(4, size.y - 28), DuelVisualTheme.Accent);
            marker.gameObject.SetActive(primary);
            Label("Button Label", image.transform, Vector2.zero, size - new Vector2(16f, 8f), 26,
                DuelVisualTheme.Foreground).text = caption;
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
