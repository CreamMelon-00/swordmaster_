using System;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A mission's non-modal coach: guided beats use the full card; free play keeps a small goal at the edge
    /// of the arena and lets the player reopen the lesson and its controls. Only buttons intercept battle input.</summary>
    public sealed class MissionCoachHud : IDisposable
    {
        private readonly RectTransform root, frame, compactFrame;
        private readonly Text counter, title, description, inputHint, continueCaption;
        private readonly Text compactMission, compactTitle, compactDescription;
        private readonly Button continueButton, skipButton, inspectButton, expandButton, foldButton;
        private int shownStep = -1, shownCount = -1;
        private string shownMission;
        private MissionGuide shownGuide;
        private bool shownFree, expandedFree;
        private bool disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;

        /// <summary>The top of the expanded card on the screen, in pixels from the bottom. The small free-play goal
        /// sits beside the dock, so it does not raise a held skill's explanation.</summary>
        public float TopEdge => IsVisible && frame.gameObject.activeSelf
            ? RectTransformUtility.WorldToScreenPoint(null, frame.TransformPoint(new Vector3(0f, frame.rect.yMax, 0f))).y : 0f;

        public MissionCoachHud(Transform parent, LegacyDuelArt art, Action advance, Action skip, Action inspectEnemy = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (art == null) throw new ArgumentNullException(nameof(art));
            root = MissionCoachCard.Rect("Mission Coach HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 300;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            // Keep the lesson below the actors and above the 200px input dock.
            // The queues and status bars move with the fighters and must remain visible while being taught.
            var card = new MissionCoachCard(root, art.UIFont);
            card.Frame.anchorMin = card.Frame.anchorMax = new Vector2(.5f, 0f);
            card.Frame.anchoredPosition = new Vector2(0f, 320f);
            frame = card.Frame;
            counter = card.Counter;
            title = card.Title;
            description = card.Description;
            inputHint = card.InputHint;
            continueButton = ActionButton(card.Continue, advance, true);
            continueCaption = card.ContinueCaption;
            inspectButton = ActionButton(card.Inspect, inspectEnemy, true);
            skipButton = ActionButton(card.Abandon, skip);

            // The full lesson is still available in free play, but it no longer owns the centre of the battlefield.
            var foldFace = Face("Coach Fold", frame, new Vector2(-344f, -80f), new Vector2(136f, 32f),
                "안내 접기", art.UIFont, false);
            foldButton = ActionButton(foldFace, () => SetFreeExpanded(false));
            foldButton.gameObject.SetActive(false);

            compactFrame = MissionCoachCard.Rect("Coach Compact Border", root, Vector2.zero, new Vector2(436f, 94f));
            compactFrame.anchorMin = compactFrame.anchorMax = compactFrame.pivot = Vector2.zero;
            compactFrame.anchoredPosition = new Vector2(16f, 228f);
            var compactBorder = compactFrame.gameObject.AddComponent<Image>();
            compactBorder.color = DuelVisualTheme.Accent;
            compactBorder.raycastTarget = false;
            var compactCard = MissionCoachCard.Rect("Coach Compact Card", compactFrame, Vector2.zero,
                new Vector2(432f, 90f)).gameObject.AddComponent<Image>();
            compactCard.color = DuelVisualTheme.Surface;
            compactCard.raycastTarget = false;
            DuelVisualTheme.DressPanel(compactCard);
            compactMission = Label("Coach Compact Mission", compactCard.transform, new Vector2(-62f, 32f),
                new Vector2(290f, 17f), 13, DuelVisualTheme.Accent, art.UIFont);
            compactTitle = Label("Coach Compact Goal", compactCard.transform, new Vector2(-62f, 10f),
                new Vector2(290f, 27f), 19, DuelVisualTheme.Foreground, art.UIFont);
            compactTitle.resizeTextForBestFit = true;
            compactTitle.resizeTextMinSize = 16;
            compactTitle.resizeTextMaxSize = 19;
            compactDescription = Label("Coach Compact Detail", compactCard.transform, new Vector2(-62f, -23f),
                new Vector2(290f, 37f), 13, DuelVisualTheme.Muted, art.UIFont);
            compactDescription.resizeTextForBestFit = true;
            compactDescription.resizeTextMinSize = 11;
            compactDescription.resizeTextMaxSize = 13;
            var expandFace = Face("Coach Expand", compactCard.transform, new Vector2(160f, -3f),
                new Vector2(102f, 34f), "안내 보기", art.UIFont, true);
            expandButton = ActionButton(expandFace, () => SetFreeExpanded(true), true);
            compactFrame.gameObject.SetActive(false);
            Hide();
        }

        /// <param name="missionLabel">Shown before the beat counter, e.g. "임무 02" (<see cref="MissionCoachCard.MissionLabel"/>).</param>
        public void Show(MissionGuide guide, string missionLabel)
        {
            if (disposed) return;
            if (guide == null) throw new ArgumentNullException(nameof(guide));
            root.gameObject.SetActive(true);
            // A new attempt or the first free beat starts folded. Repeated refreshes retain the player's open/close choice.
            if (!ReferenceEquals(shownGuide, guide) || guide.IsFree && !shownFree) expandedFree = false;
            shownGuide = guide;
            shownFree = guide.IsFree;
            // Controller may refresh this view frequently. Keep both objects and strings stable until the step changes.
            if (shownStep != guide.StepNumber || shownCount != guide.StepCount || shownMission != missionLabel)
            {
                shownStep = guide.StepNumber;
                shownCount = guide.StepCount;
                shownMission = missionLabel;
                counter.text = MissionCoachCard.CounterText(missionLabel, shownStep, shownCount);
            }
            if (title.text != guide.Title) title.text = guide.Title;
            if (description.text != guide.Description) description.text = guide.Description;
            if (inputHint.text != guide.InputHint) inputHint.text = guide.InputHint;
            continueButton.gameObject.SetActive(guide.CanAdvance);
            string caption = guide.IsOpening ? "시작" : "계속";
            if (continueCaption.text != caption) continueCaption.text = caption;
            inspectButton.gameObject.SetActive(guide.AllowsInspect);
            foldButton.gameObject.SetActive(guide.IsFree);
            if (guide.IsFree)
            {
                string mission = missionLabel + "  ·  자유 전투";
                if (compactMission.text != mission) compactMission.text = mission;
                if (compactTitle.text != guide.Title) compactTitle.text = guide.Title;
                if (compactDescription.text != guide.Description) compactDescription.text = guide.Description;
            }
            frame.gameObject.SetActive(!guide.IsFree || expandedFree);
            compactFrame.gameObject.SetActive(guide.IsFree && !expandedFree);
        }

        private void SetFreeExpanded(bool expanded)
        {
            if (disposed || shownGuide == null || !shownGuide.IsFree) return;
            expandedFree = expanded;
            frame.gameObject.SetActive(expanded);
            compactFrame.gameObject.SetActive(!expanded);
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
            expandButton.onClick.RemoveAllListeners();
            foldButton.onClick.RemoveAllListeners();
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        // The card drew the button's face; this makes it a button.
        private static Button ActionButton(Image face, Action action, bool primary = false)
        {
            face.raycastTarget = true;
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                action?.Invoke();
            });
            return button;
        }

        private static Image Face(string name, Transform parent, Vector2 position, Vector2 size, string caption,
            Font font, bool primary)
        {
            var image = MissionCoachCard.Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = primary ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface;
            var text = Label("Button Label", image.transform, Vector2.zero, size - new Vector2(8f, 2f), 15,
                primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground, font);
            text.text = caption;
            text.alignment = TextAnchor.MiddleCenter;
            return image;
        }

        private static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize,
            Color color, Font font)
        {
            var text = MissionCoachCard.Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }
    }

    /// <summary>The coach's card as the player sees it: the brass border, the beat counter, title, description and input
    /// hint, and the faces of its three buttons (계속/시작, 적 큐 확인, 임무 포기). <see cref="MissionCoachHud"/> makes the
    /// faces work as buttons; the cutscene's tutorial recall (<c>@recall</c>) redraws a remembered beat on the same card
    /// as a picture (<see cref="Show"/>), so it looks as the lesson did. Nothing on it takes clicks by itself.</summary>
    public sealed class MissionCoachCard
    {
        public static readonly Vector2 FrameSize = new Vector2(904f, 204f);
        private static readonly Color Surface = DuelVisualTheme.Surface;
        private static readonly Color Raised = DuelVisualTheme.RaisedSurface;
        private static readonly Color Accent = DuelVisualTheme.Accent;
        private static readonly Color Foreground = DuelVisualTheme.Foreground;
        private static readonly Color Muted = DuelVisualTheme.Muted;
        private readonly Font font;

        /// <summary>Builds the card centred on <paramref name="parent"/>; place it through <see cref="Frame"/>.</summary>
        public MissionCoachCard(Transform parent, Font font)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.font = font;
            Image border = Panel("Coach Border", parent, Vector2.zero, FrameSize, Accent);
            Frame = border.rectTransform;
            var card = Panel("Coach Card", border.transform, Vector2.zero, new Vector2(900f, 200f), Surface);
            DuelVisualTheme.DressPanel(card);
            Counter = Label("Coach Step", card.transform, new Vector2(0f, 76f), new Vector2(848f, 20f), 16, Muted);
            Title = Label("Coach Title", card.transform, new Vector2(0f, 49f), new Vector2(848f, 32f), 26);
            Title.resizeTextForBestFit = true;
            Title.resizeTextMinSize = 18;
            Title.resizeTextMaxSize = 26;
            Description = Label("Coach Description", card.transform, new Vector2(0f, 2f), new Vector2(848f, 56f), 17);
            Description.alignment = TextAnchor.UpperLeft;
            Description.resizeTextForBestFit = true;
            Description.resizeTextMinSize = 16;
            Description.resizeTextMaxSize = 17;
            InputHint = Label("Coach Input Hint", card.transform, new Vector2(0f, -46f), new Vector2(848f, 34f), 16, Accent);
            InputHint.resizeTextForBestFit = true;
            InputHint.resizeTextMinSize = 14;
            InputHint.resizeTextMaxSize = 16;
            Continue = Face("Coach Continue", card.transform, "다음", new Vector2(-344f, -80f), new Vector2(136f, 32f), true);
            ContinueCaption = Continue.GetComponentInChildren<Text>();
            Inspect = Face("Coach Inspect Enemy", card.transform, "적 큐 확인 [Tab]", new Vector2(-276f, -80f), new Vector2(272f, 32f), true);
            Abandon = Face("Coach Abandon", card.transform, "임무 포기", new Vector2(344f, -80f), new Vector2(136f, 32f), false);
        }

        /// <summary>The brass border holding everything: anchor, place or scale the card through it.</summary>
        public RectTransform Frame { get; }
        public Text Counter { get; }
        public Text Title { get; }
        public Text Description { get; }
        public Text InputHint { get; }
        public Image Continue { get; }
        public Text ContinueCaption { get; }
        public Image Inspect { get; }
        public Image Abandon { get; }

        /// <summary>The coach's mission label, e.g. "임무 02".</summary>
        public static string MissionLabel(int missionNumber) => $"임무 {missionNumber:00}";

        /// <summary>The counter line, e.g. "임무 02  ·  안내 01 / 09".</summary>
        public static string CounterText(string missionLabel, int stepNumber, int stepCount)
            => $"{missionLabel}  ·  안내 {stepNumber:00} / {stepCount:00}";

        /// <summary>Draws <paramref name="beat"/> as the coach showed it at <paramref name="stepNumber"/> of
        /// <paramref name="stepCount"/>: the same copy, and the buttons that beat had.</summary>
        public void Show(MissionGuideBeat beat, int stepNumber, int stepCount, string missionLabel)
        {
            if (beat == null) throw new ArgumentNullException(nameof(beat));
            Counter.text = CounterText(missionLabel, stepNumber, stepCount);
            Title.text = beat.Title;
            Description.text = beat.Description;
            InputHint.text = beat.InputHint;
            Continue.gameObject.SetActive(beat.Kind == MissionGuideStepKind.Info);
            ContinueCaption.text = stepNumber == 1 ? "시작" : "계속";
            Inspect.gameObject.SetActive(beat.Kind == MissionGuideStepKind.Inspect);
        }

        private Image Face(string name, Transform parent, string caption, Vector2 position, Vector2 size, bool primary)
        {
            var image = Panel(name, parent, position, size, primary ? Accent : Raised);
            DuelVisualTheme.Frame(image, primary);
            Text label = Label("Button Label", image.transform, Vector2.zero, size - new Vector2(12f, 4f), 17,
                primary ? DuelVisualTheme.Ink : Foreground);
            label.text = caption;
            label.alignment = TextAnchor.MiddleCenter;
            return image;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color? color = null)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color ?? Foreground;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        internal static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
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
