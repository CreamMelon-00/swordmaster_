using System;
using TurnLimbo.Runtime.Dialogue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A modal, code-built dialogue view. Dialogue order remains owned by DialogueSession.</summary>
    public sealed class DialogueHud : IDisposable
    {
        private readonly LegacyDuelArt art;
        private readonly Action advance, close;
        private readonly RectTransform root, cardBorder;
        private readonly RectTransform nameplate;
        private readonly Image leftPortrait, rightPortrait;
        private readonly Text speakerName, speakerRole, body, progress, inputHint, closeCaption;
        private readonly Image backdropImage;
        private static readonly Color BackdropColor =
            new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .76f);
        /// <summary>The body text of a monologue line (<see cref="DialogueLine.IsMonologue"/>): a cool, quieter tone
        /// set apart from the warm paper colour of speech.</summary>
        public static readonly Color MonologueColor = new Color(.66f, .78f, .86f, 1f);
        private readonly Button backdropButton, nextButton, closeButton, choiceAButton, choiceBButton;
        private readonly RectTransform choicePanel;
        private readonly Text choiceALabel, choiceBLabel;
        private Action<int> choose;
        private int selectedChoiceIndex = -1;
        private string displayText;
        private int visibleCharacters;
        private float revealElapsed;
        private const float CharactersPerSecond = 42f;
        private bool disposed;

        public DialogueHud(Transform parent, LegacyDuelArt art, Action advance, Action close)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.advance = advance;
            this.close = close;

            root = Rect("Dialogue HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 500;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();

            var backdrop = Panel("Dialogue Backdrop", root, Vector2.zero, Vector2.zero, BackdropColor);
            backdropImage = backdrop;
            // Cinematic mode makes it fully transparent; it must still be drawn so a click anywhere advances.
            backdrop.canvasRenderer.cullTransparentMesh = false;
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.navigation = new Navigation { mode = Navigation.Mode.None };
            backdropButton.onClick.AddListener(InvokeAdvance);

            leftPortrait = CreatePortrait("Dialogue Portrait Left", -620f);
            rightPortrait = CreatePortrait("Dialogue Portrait Right", 620f);

            var border = Panel("Dialogue Card Border", root, Vector2.zero, new Vector2(1604f, 334f), DuelVisualTheme.Border);
            cardBorder = border.rectTransform;
            AnchorToBottom(cardBorder, 48f);
            var card = Panel("Dialogue Card", border.transform, Vector2.zero, new Vector2(1600f, 330f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(card);

            var plate = Panel("Dialogue Nameplate", card.transform, new Vector2(-490f, 119f),
                new Vector2(500f, 62f), DuelVisualTheme.RaisedSurface);
            DuelVisualTheme.Frame(plate);
            nameplate = plate.rectTransform;
            speakerName = Label("Dialogue Speaker", nameplate, string.Empty, new Vector2(-92f, 0f),
                new Vector2(284f, 48f), 28, DuelVisualTheme.Foreground, TextAnchor.MiddleLeft);
            speakerRole = Label("Dialogue Role", nameplate, string.Empty, new Vector2(152f, 0f),
                new Vector2(174f, 44f), 19, DuelVisualTheme.Muted, TextAnchor.MiddleRight);

            body = Label("Dialogue Body", card.transform, string.Empty, new Vector2(0f, 20f),
                new Vector2(1460f, 132f), 34, DuelVisualTheme.Foreground, TextAnchor.UpperLeft);
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 21;
            body.resizeTextMaxSize = 34;
            body.lineSpacing = 1.12f;

            progress = Label("Dialogue Progress", card.transform, string.Empty, new Vector2(-650f, -116f),
                new Vector2(170f, 38f), 18, DuelVisualTheme.Muted, TextAnchor.MiddleLeft);
            inputHint = Label("Dialogue Input Hint", card.transform, HintText,
                new Vector2(-80f, -116f), new Vector2(700f, 38f), 18, DuelVisualTheme.Muted, TextAnchor.MiddleCenter);
            closeButton = ActionButton("Dialogue Close", card.transform, CloseText,
                new Vector2(500f, -116f), new Vector2(180f, 48f), InvokeClose);
            closeCaption = closeButton.GetComponentInChildren<Text>();
            nextButton = ActionButton("Dialogue Next", card.transform, "다음",
                new Vector2(680f, -116f), new Vector2(150f, 48f), InvokeAdvance, true);
            choicePanel = Rect("Dialogue Choices", root, new Vector2(0f, 270f), new Vector2(1000f, 216f));
            Panel("Dialogue Choices Backdrop", choicePanel, Vector2.zero, new Vector2(1000f, 216f),
                new Color(BackdropColor.r, BackdropColor.g, BackdropColor.b, .96f));
            choiceAButton = ActionButton("Dialogue Choice A", choicePanel, string.Empty,
                new Vector2(0f, 48f), new Vector2(920f, 72f), () => SelectChoice(0));
            choiceBButton = ActionButton("Dialogue Choice B", choicePanel, string.Empty,
                new Vector2(0f, -48f), new Vector2(920f, 72f), () => SelectChoice(1));
            choiceALabel = choiceAButton.GetComponentInChildren<Text>();
            choiceBLabel = choiceBButton.GetComponentInChildren<Text>();
            Hide();
        }

        private const string HintText = "클릭 / Enter / Space로 다음  ·  Esc로 닫기";
        private const string CloseText = "닫기  [Esc]";
        private const string CinematicHintText = "클릭 / Enter / Space로 다음  ·  Esc로 건너뛰기";
        private const string CinematicCloseText = "건너뛰기  [Esc]";

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        /// <summary>A cutscene's lines: the scene is not dimmed (a click anywhere still advances) and closing skips it.</summary>
        public bool IsCinematic { get; private set; }
        public bool MaskElisaName { get; set; }
        public bool IsRevealing => IsCinematic && CurrentLine != null && visibleCharacters < displayText.Length;
        public bool IsChoosing => choicePanel.gameObject.activeSelf;

        public void SetCinematic(bool cinematic)
        {
            if (disposed) return;
            IsCinematic = cinematic;
            backdropImage.color = cinematic ? new Color(BackdropColor.r, BackdropColor.g, BackdropColor.b, 0f) : BackdropColor;
            inputHint.text = cinematic ? CinematicHintText : HintText;
            closeCaption.text = cinematic ? CinematicCloseText : CloseText;
            if (!cinematic) SetCinematicShake(Vector2.zero);
        }
        public void SetCinematicHint(string caption)
        {
            if (!disposed && IsCinematic)
                inputHint.text = string.IsNullOrEmpty(caption) ? CinematicHintText : caption;
        }

        public DialogueLine CurrentLine { get; private set; }

        public void Show(DialogueLine line, int currentIndex, int lineCount)
            => Show(line, currentIndex, lineCount, null, null);

        public void Show(DialogueLine line, int currentIndex, int lineCount, Sprite speakerPortrait)
            => Show(line, currentIndex, lineCount,
                line != null && line.Side == DialogueSide.Left ? speakerPortrait : null,
                line != null && line.Side == DialogueSide.Right ? speakerPortrait : null);

        public void Show(DialogueLine line, int currentIndex, int lineCount,
            Sprite leftSpeakerPortrait, Sprite rightSpeakerPortrait)
        {
            if (disposed) throw new ObjectDisposedException(nameof(DialogueHud));
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (lineCount < 1) throw new ArgumentOutOfRangeException(nameof(lineCount));
            if (currentIndex < 0 || currentIndex >= lineCount) throw new ArgumentOutOfRangeException(nameof(currentIndex));

            HideChoices();
            CurrentLine = line;
            root.gameObject.SetActive(true);
            displayText = MaskElisaName ? line.Text.Replace("엘리사", "???") : line.Text;
            revealElapsed = 0f;
            visibleCharacters = IsCinematic ? 0 : displayText.Length;
            body.text = IsCinematic ? string.Empty : displayText;
            // A thought in parentheses keeps them and reads in its own colour.
            body.color = line.IsMonologue ? MonologueColor : DuelVisualTheme.Foreground;
            progress.text = $"{currentIndex + 1:00} / {lineCount:00}";
            bool narrator = !line.ShowsNameplate;
            nameplate.gameObject.SetActive(!narrator);
            SetPortrait(leftPortrait, line.Stage.Left, leftSpeakerPortrait,
                line.Side == DialogueSide.Left);
            SetPortrait(rightPortrait, line.Stage.Right, rightSpeakerPortrait,
                line.Side == DialogueSide.Right);
            body.alignment = narrator ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
            body.rectTransform.anchoredPosition = narrator ? new Vector2(0f, 40f) : new Vector2(0f, 20f);
            body.rectTransform.sizeDelta = narrator ? new Vector2(1460f, 178f) : new Vector2(1460f, 132f);

            if (!narrator)
            {
                bool right = line.Side == DialogueSide.Right;
                nameplate.anchoredPosition = new Vector2(right ? 490f : -490f, 119f);
                speakerName.text = MaskElisaName && line.SpeakerName == "엘리사" ? "???" : line.SpeakerName;
                speakerRole.text = line.SpeakerRole;
                speakerName.alignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                speakerRole.alignment = right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                speakerName.rectTransform.anchoredPosition = new Vector2(right ? 92f : -92f, 0f);
                speakerRole.rectTransform.anchoredPosition = new Vector2(right ? -152f : 152f, 0f);
                speakerRole.gameObject.SetActive(line.SpeakerRole.Length > 0);
                speakerName.rectTransform.sizeDelta = line.SpeakerRole.Length > 0
                    ? new Vector2(284f, 48f) : new Vector2(448f, 48f);
                if (line.SpeakerRole.Length == 0) speakerName.rectTransform.anchoredPosition = Vector2.zero;
            }
            ClearSelection();
        }

        public void Tick(float realDelta)
        {
            if (!IsRevealing) return;
            revealElapsed += Mathf.Max(0f, realDelta);
            int count = Mathf.Min(displayText.Length, Mathf.FloorToInt(revealElapsed * CharactersPerSecond));
            if (count == visibleCharacters) return;
            visibleCharacters = count;
            body.text = displayText.Substring(0, visibleCharacters);
        }

        public void CompleteReveal()
        {
            if (!IsRevealing) return;
            visibleCharacters = displayText.Length;
            body.text = displayText;
        }

        public void ShowChoices(string labelA, string labelB, Action<int> onChoose)
        {
            if (disposed) return;
            CompleteReveal();
            choose = onChoose;
            choiceALabel.text = labelA;
            choiceBLabel.text = labelB;
            choicePanel.gameObject.SetActive(true);
            nextButton.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(false);
            ClearSelection();
            inputHint.text = "↑ / ↓로 고르고 Enter / Space로 결정";
            SelectChoiceIndex(0);
        }

        /// <summary>Keyboard selection follows the visible vertical order, without exposing internal branch ids.</summary>
        public void MoveChoiceSelection(int direction)
        {
            if (!IsChoosing || direction == 0) return;
            SelectChoiceIndex(Mathf.Clamp(selectedChoiceIndex + Math.Sign(direction), 0, 1));
        }

        public void ConfirmChoiceSelection()
        {
            if (IsChoosing && selectedChoiceIndex >= 0) SelectChoice(selectedChoiceIndex);
        }

        /// <summary>Offsets the dialogue card and choices together for a short cinematic tremor.</summary>
        public void SetCinematicShake(Vector2 offset)
        {
            if (disposed) return;
            Vector2 applied = IsCinematic ? offset : Vector2.zero;
            cardBorder.anchoredPosition = new Vector2(0f, 48f) + applied;
            choicePanel.anchoredPosition = new Vector2(0f, 270f) + applied;
        }

        private void SelectChoiceIndex(int index)
        {
            selectedChoiceIndex = index;
            choiceAButton.image.color = index == 0 ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface;
            choiceBButton.image.color = index == 1 ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface;
            choiceALabel.color = index == 0 ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground;
            choiceBLabel.color = index == 1 ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground;
        }

        public void HideChoices()
        {
            if (choicePanel.gameObject.activeSelf)
                inputHint.text = IsCinematic ? CinematicHintText : HintText;
            choose = null;
            selectedChoiceIndex = -1;
            choicePanel.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(true);
            closeButton.gameObject.SetActive(true);
        }

        private void SelectChoice(int index)
        {
            Action<int> action = choose;
            HideChoices();
            ClearSelection();
            action?.Invoke(index);
        }

        public void RequestAdvance()
        {
            if (!IsVisible) return;
            InvokeAdvance();
        }

        public void Hide()
        {
            if (disposed) return;
            SetCinematicShake(Vector2.zero);
            CurrentLine = null;
            displayText = null;
            HideChoices();
            ClearPortrait(leftPortrait);
            ClearPortrait(rightPortrait);
            root.gameObject.SetActive(false);
            ClearSelection();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            backdropButton.onClick.RemoveAllListeners();
            nextButton.onClick.RemoveAllListeners();
            closeButton.onClick.RemoveAllListeners();
            choiceAButton.onClick.RemoveAllListeners();
            choiceBButton.onClick.RemoveAllListeners();
            CurrentLine = null;
            displayText = null;
            HideChoices();
            ClearPortrait(leftPortrait);
            ClearPortrait(rightPortrait);
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private void InvokeAdvance()
        {
            ClearSelection();
            if (IsChoosing) return;
            advance?.Invoke();
        }

        private void InvokeClose()
        {
            ClearSelection();
            if (IsChoosing) return;
            close?.Invoke();
        }

        private Image CreatePortrait(string name, float x)
        {
            var image = Rect(name, root, Vector2.zero, new Vector2(520f, 860f)).gameObject.AddComponent<Image>();
            AnchorToTop(image.rectTransform, 32f);
            image.rectTransform.anchoredPosition = new Vector2(x, -32f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            return image;
        }

        private static void SetPortrait(Image image, DialogueStageSlot slot, Sprite sprite, bool isActiveSpeaker)
        {
            bool visible = slot != null && sprite != null;
            image.sprite = visible ? sprite : null;
            image.color = isActiveSpeaker ? Color.white : new Color(.42f, .42f, .42f, 1f);
            image.gameObject.SetActive(visible);
        }

        private static void ClearPortrait(Image image)
        {
            image.sprite = null;
            image.color = Color.white;
            image.gameObject.SetActive(false);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position,
            Vector2 size, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size,
                primary ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label("Button Label", image.transform, caption, Vector2.zero, size - new Vector2(12f, 8f), 20,
                primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => action?.Invoke());
            return button;
        }

        private Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size,
            int fontSize, Color color, TextAnchor alignment)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = art.UIFont;
            label.text = value;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.supportRichText = false;
            return label;
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

        private static void AnchorToBottom(RectTransform rect, float bottom)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, bottom);
        }

        private static void AnchorToTop(RectTransform rect, float top)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
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
