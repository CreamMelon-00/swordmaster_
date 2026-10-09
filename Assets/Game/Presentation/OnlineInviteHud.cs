using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public enum OnlineInvitePhase
    {
        Choice,
        Connecting,
        Lobby,
        Error
    }

    /// <summary>Display-only snapshot supplied by the online session controller.</summary>
    public struct OnlineInviteView
    {
        public OnlineInvitePhase Phase { get; set; }
        public bool Host { get; set; }
        public bool PeerConnected { get; set; }
        public bool LocalReady { get; set; }
        public bool RemoteReady { get; set; }
        public bool Busy { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
    }

    /// <summary>Invite-code entry and two-player ready room. Networking stays with the caller.</summary>
    public sealed class OnlineInviteHud : IDisposable
    {
        public const int SortingOrder = 270;

        private readonly LegacyDuelArt art;
        private readonly RectTransform root;
        private readonly GameObject ownedEventSystem;
        private readonly GameObject choiceGroup, lobbyGroup;
        private readonly InputField codeInput;
        private readonly Text codeDisplay, copyCaption, connectionLabel, localReadyLabel, remoteReadyLabel;
        private readonly Text messageLabel;
        private readonly Button createButton, joinButton, copyButton, configureButton, readyButton, startButton, leaveButton;
        private readonly Action create, ready, start, leave;
        private readonly Action<string> join;
        private OnlineInviteView view;
        private string copiedCode;
        private bool disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public InputField CodeInput => codeInput;
        public Button CreateButton => createButton;
        public Button JoinButton => joinButton;
        public Button CopyButton => copyButton;
        public Button ConfigureButton => configureButton;
        public Button ReadyButton => readyButton;
        public Action ConfigureSkills { get; set; }
        public Button StartButton => startButton;
        public Button LeaveButton => leaveButton;

        public OnlineInviteHud(Transform parent, LegacyDuelArt art, Action create, Action<string> join,
            Action ready, Action start, Action leave)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.create = create;
            this.join = join;
            this.ready = ready;
            this.start = start;
            this.leave = leave;

            root = Rect("Online Invite HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Online Invite EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(parent, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            Image veil = Panel("Invite Backdrop", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .86f));
            Stretch(veil.rectTransform);
            veil.raycastTarget = true;
            var border = Panel("Invite Border", root, Vector2.zero, new Vector2(1110f, 690f),
                DuelVisualTheme.Border);
            var card = Panel("Invite Card", border.transform, Vector2.zero, new Vector2(1104f, 684f),
                DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(card);
            Label("Invite Heading", card.transform, new Vector2(0f, 287f), new Vector2(650f, 58f),
                46, DuelVisualTheme.Foreground).text = "온라인 친구 대전";
            Label("Invite Subtitle", card.transform, new Vector2(0f, 234f), new Vector2(870f, 34f),
                21, DuelVisualTheme.Muted).text = "초대 코드로 친구와 한 판 겨뤄보세요.";
            leaveButton = ActionButton("Invite Leave", card.transform, new Vector2(456f, 288f),
                new Vector2(136f, 44f), "나가기", () => this.leave?.Invoke());

            choiceGroup = Rect("Invite Choice", card.transform, new Vector2(0f, 0f),
                new Vector2(1040f, 420f)).gameObject;
            var hostCard = Panel("Invite Host Choice", choiceGroup.transform,
                new Vector2(-254f, 2f), new Vector2(486f, 356f), DuelVisualTheme.RaisedSurface);
            DuelVisualTheme.DressPanel(hostCard);
            Label("Invite Host Heading", hostCard.transform, new Vector2(0f, 100f),
                new Vector2(440f, 52f), 32, DuelVisualTheme.Foreground).text = "방 만들기";
            Label("Invite Host Help", hostCard.transform, new Vector2(0f, 14f),
                new Vector2(410f, 104f), 22, DuelVisualTheme.Muted).text =
                "새 방을 열고 초대 코드를\n친구에게 보내세요.";
            createButton = ActionButton("Invite Create", hostCard.transform,
                new Vector2(0f, -106f), new Vector2(332f, 66f),
                "방 만들기", () => this.create?.Invoke(), true);

            var joinCard = Panel("Invite Join Choice", choiceGroup.transform,
                new Vector2(254f, 2f), new Vector2(486f, 356f), DuelVisualTheme.RaisedSurface);
            DuelVisualTheme.DressPanel(joinCard);
            Label("Invite Join Heading", joinCard.transform, new Vector2(0f, 100f),
                new Vector2(440f, 52f), 32, DuelVisualTheme.Foreground).text = "코드로 참가";
            Label("Invite Join Help", joinCard.transform, new Vector2(0f, 42f),
                new Vector2(410f, 38f), 19, DuelVisualTheme.Muted).text = "친구에게 받은 초대 코드를 입력하세요.";
            codeInput = BuildCodeInput(joinCard.transform);
            joinButton = ActionButton("Invite Join", joinCard.transform,
                new Vector2(0f, -106f), new Vector2(332f, 66f),
                "참가하기", SubmitJoin, true);
            codeInput.onValueChanged.AddListener(OnCodeInputChanged);

            lobbyGroup = Rect("Invite Lobby", card.transform, Vector2.zero,
                new Vector2(1040f, 420f)).gameObject;
            var lobbyCard = Panel("Invite Lobby Card", lobbyGroup.transform, new Vector2(0f, 2f),
                new Vector2(994f, 356f), DuelVisualTheme.RaisedSurface);
            DuelVisualTheme.DressPanel(lobbyCard);
            Label("Invite Code Heading", lobbyCard.transform, new Vector2(-184f, 111f),
                new Vector2(410f, 36f), 23, DuelVisualTheme.Muted).text = "초대 코드";
            var codeCard = Panel("Invite Code Panel", lobbyCard.transform,
                new Vector2(-184f, 54f), new Vector2(402f, 76f), DuelVisualTheme.Track);
            DuelVisualTheme.Frame(codeCard);
            codeDisplay = Label("Invite Code Display", codeCard.transform, Vector2.zero,
                new Vector2(380f, 66f), 40, DuelVisualTheme.Accent);
            codeDisplay.resizeTextForBestFit = true;
            codeDisplay.resizeTextMinSize = 28;
            codeDisplay.resizeTextMaxSize = 40;
            copyButton = ActionButton("Invite Copy Code", lobbyCard.transform,
                new Vector2(198f, 54f), new Vector2(250f, 66f), "코드 복사", CopyCode);
            copyCaption = copyButton.GetComponentInChildren<Text>();
            connectionLabel = Label("Invite Connection", lobbyCard.transform,
                new Vector2(0f, -17f), new Vector2(890f, 45f), 24, DuelVisualTheme.Foreground);
            localReadyLabel = Label("Invite Local Ready", lobbyCard.transform,
                new Vector2(-223f, -82f), new Vector2(380f, 38f), 22, DuelVisualTheme.Muted);
            remoteReadyLabel = Label("Invite Remote Ready", lobbyCard.transform,
                new Vector2(223f, -82f), new Vector2(380f, 38f), 22, DuelVisualTheme.Muted);
            configureButton = ActionButton("Invite Configure Skills", lobbyCard.transform,
                new Vector2(-302f, -144f), new Vector2(220f, 56f),
                "기술 편성", () => ConfigureSkills?.Invoke());
            readyButton = ActionButton("Invite Ready", lobbyCard.transform,
                new Vector2(0f, -144f), new Vector2(220f, 56f),
                "준비", () => this.ready?.Invoke(), true);
            startButton = ActionButton("Invite Start", lobbyCard.transform,
                new Vector2(302f, -144f), new Vector2(220f, 56f),
                "대전 시작", () => this.start?.Invoke(), true);

            messageLabel = Label("Invite Message", card.transform,
                new Vector2(0f, -272f), new Vector2(1000f, 68f), 21, DuelVisualTheme.Muted);
            Refresh(new OnlineInviteView { Phase = OnlineInvitePhase.Choice });
            Hide();
        }

        public void Show()
        {
            if (disposed) return;
            root.gameObject.SetActive(true);
            codeInput.text = string.Empty;
            copiedCode = null;
            Refresh(view);
        }

        public void Hide()
        {
            if (disposed) return;
            root.gameObject.SetActive(false);
            ClearSelection();
        }

        public void Refresh(OnlineInviteView view)
        {
            if (disposed) return;
            this.view = view;
            bool choice = view.Phase == OnlineInvitePhase.Choice || view.Phase == OnlineInvitePhase.Error;
            bool lobby = view.Phase == OnlineInvitePhase.Lobby;
            bool connecting = view.Phase == OnlineInvitePhase.Connecting;
            choiceGroup.SetActive(choice);
            lobbyGroup.SetActive(lobby || connecting);

            createButton.interactable = choice && !view.Busy;
            codeInput.interactable = choice && !view.Busy;
            joinButton.interactable = choice && !view.Busy &&
                !string.IsNullOrEmpty(SanitizeCode(codeInput.text));
            codeDisplay.text = !string.IsNullOrWhiteSpace(view.Code) ? view.Code : "······";
            copyButton.gameObject.SetActive(lobby && view.Host && !string.IsNullOrWhiteSpace(view.Code));
            copyButton.interactable = !view.Busy;
            copyCaption.text = string.Equals(copiedCode, view.Code, StringComparison.Ordinal)
                ? "복사 완료" : "코드 복사";
            connectionLabel.text = connecting
                ? view.Host ? "방을 만드는 중입니다..." : "친구의 방에 접속하는 중입니다..."
                : view.PeerConnected ? "친구와 연결되었습니다"
                : view.Host ? "친구를 기다리는 중입니다" : "방에 연결하는 중입니다";
            localReadyLabel.text = "나  ·  " + (view.LocalReady ? "준비 완료" : "준비 전");
            remoteReadyLabel.text = "친구  ·  " +
                (view.PeerConnected ? view.RemoteReady ? "준비 완료" : "준비 전" : "접속 대기");
            localReadyLabel.color = view.LocalReady ? DuelVisualTheme.Accent : DuelVisualTheme.Muted;
            remoteReadyLabel.color = view.PeerConnected && view.RemoteReady
                ? DuelVisualTheme.Accent : DuelVisualTheme.Muted;
            configureButton.gameObject.SetActive(lobby);
            configureButton.interactable = lobby && !view.LocalReady && !view.Busy;
            readyButton.gameObject.SetActive(lobby);
            readyButton.interactable = lobby && view.PeerConnected && !view.LocalReady && !view.Busy;
            startButton.gameObject.SetActive(lobby && view.Host);
            startButton.interactable = lobby && view.Host && view.PeerConnected &&
                view.LocalReady && view.RemoteReady && !view.Busy;

            string fallback = choice ? view.Phase == OnlineInvitePhase.Error
                    ? "초대 코드를 확인한 뒤 다시 시도해 주세요."
                    : "대전은 캠페인 진행과 별개로 진행됩니다."
                : connecting ? "연결을 확인하고 있습니다..."
                : view.Host && !view.PeerConnected ? "초대 코드를 친구에게 보내세요."
                : view.Host ? "두 사람이 준비하면 대전을 시작할 수 있습니다."
                : "준비를 누르고 친구의 시작을 기다려 주세요.";
            messageLabel.text = string.IsNullOrWhiteSpace(view.Message) ? fallback : view.Message;
            messageLabel.color = view.Phase == OnlineInvitePhase.Error
                ? DuelVisualTheme.Danger : DuelVisualTheme.Muted;
        }

        private InputField BuildCodeInput(Transform parent)
        {
            var backing = Panel("Invite Code Input", parent, new Vector2(0f, -25f),
                new Vector2(332f, 66f), DuelVisualTheme.Paper);
            backing.raycastTarget = true;
            DuelVisualTheme.Frame(backing);
            var field = backing.gameObject.AddComponent<InputField>();
            field.targetGraphic = backing;
            field.characterLimit = 12;
            field.lineType = InputField.LineType.SingleLine;
            var value = Label("Invite Code Text", backing.transform, Vector2.zero,
                new Vector2(296f, 58f), 28, DuelVisualTheme.Ink, TextAnchor.MiddleLeft);
            value.supportRichText = false;
            var placeholder = Label("Invite Code Placeholder", backing.transform, Vector2.zero,
                new Vector2(296f, 58f), 24, DuelVisualTheme.Muted, TextAnchor.MiddleLeft);
            placeholder.text = "초대 코드";
            field.textComponent = value;
            field.placeholder = placeholder;
            return field;
        }

        private void OnCodeInputChanged(string value)
        {
            string cleaned = SanitizeCode(value);
            if (!string.Equals(value, cleaned, StringComparison.Ordinal))
                codeInput.SetTextWithoutNotify(cleaned);
            joinButton.interactable = !view.Busy &&
                (view.Phase == OnlineInvitePhase.Choice || view.Phase == OnlineInvitePhase.Error) &&
                cleaned.Length > 0;
        }

        private void SubmitJoin()
        {
            string code = SanitizeCode(codeInput.text);
            if (code.Length == 0 || view.Busy) return;
            join?.Invoke(code);
        }

        private void CopyCode()
        {
            if (string.IsNullOrWhiteSpace(view.Code)) return;
            GUIUtility.systemCopyBuffer = view.Code;
            copiedCode = view.Code;
            copyCaption.text = "복사 완료";
        }

        private static string SanitizeCode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var result = new StringBuilder(Mathf.Min(raw.Length, 12));
            foreach (char original in raw)
            {
                char c = char.ToUpperInvariant(original);
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) result.Append(c);
                if (result.Length == 12) break;
            }
            return result.ToString();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            createButton.onClick.RemoveAllListeners();
            joinButton.onClick.RemoveAllListeners();
            copyButton.onClick.RemoveAllListeners();
            configureButton.onClick.RemoveAllListeners();
            readyButton.onClick.RemoveAllListeners();
            startButton.onClick.RemoveAllListeners();
            leaveButton.onClick.RemoveAllListeners();
            codeInput.onValueChanged.RemoveAllListeners();
            DestroyObject(root.gameObject);
            if (ownedEventSystem != null) DestroyObject(ownedEventSystem);
        }

        private Button ActionButton(string name, Transform parent, Vector2 position, Vector2 size,
            string caption, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size,
                primary ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label("Button Label", image.transform, Vector2.zero, size - new Vector2(12f, 8f),
                22, primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground).text = caption;
            button.onClick.AddListener(() => { ClearSelection(); action?.Invoke(); });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize,
            Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = art.UIFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
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
            rect.anchoredPosition = rect.sizeDelta = Vector2.zero;
        }

        private static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
