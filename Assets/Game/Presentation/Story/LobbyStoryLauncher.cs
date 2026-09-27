using TurnLimbo.Runtime.Dialogue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DuelPrototypeController))]
    public sealed class LobbyStoryLauncher : MonoBehaviour
    {
        public const string DialogueResourcePath = "Dialogue/dialogue";
        private const string FontResourcePath = "LegacyDuel/Font/neodgm";

        private DuelPrototypeController controller;
        private RectTransform root;
        private Button storyButton;
        private Text caption;
        private bool initialized;
        private bool storyReady;
        private bool wasHomeEligible;
        private string availabilityMessage = string.Empty;

        public GameObject Root => root == null ? null : root.gameObject;
        public Button StoryButton => storyButton;
        public bool IsVisible => root != null && root.gameObject.activeSelf;
        public bool IsStoryReady => storyReady;
        public string AvailabilityMessage => availabilityMessage;

        private void Start()
        {
            controller = GetComponent<DuelPrototypeController>();
            if (controller == null || controller.LobbyHud == null)
            {
                Debug.LogError("로비 스토리 버튼을 초기화할 컨트롤러가 없습니다.");
                enabled = false;
                return;
            }

            CreateView();
            RefreshVisibility(true);
        }

        private void OnEnable()
        {
            if (initialized) RefreshVisibility(true);
        }

        private void Update()
        {
            if (initialized) RefreshVisibility(false);
        }

        private void OnDisable()
        {
            wasHomeEligible = false;
            if (root != null) root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (storyButton != null) storyButton.onClick.RemoveListener(OpenStory);
            if (root == null) return;
            if (Application.isPlaying) Destroy(root.gameObject);
            else DestroyImmediate(root.gameObject);
        }

        public void RefreshAvailability()
        {
            storyReady = TryValidateStory(out availabilityMessage);
            if (storyButton == null || caption == null) return;
            storyButton.interactable = storyReady;
            caption.text = storyReady ? "스토리  ·  열기" : "스토리 준비 중";
            caption.color = storyReady ? DuelVisualTheme.Ink : DuelVisualTheme.Muted;
            if (!storyReady) Debug.LogWarning(availabilityMessage);
        }

        private void CreateView()
        {
            var rootObject = new GameObject("Lobby Story Entry", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            rootObject.transform.SetParent(controller.LobbyHud.Root.transform, false);
            root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-58f, 58f);
            root.sizeDelta = new Vector2(280f, 64f);

            Image background = rootObject.GetComponent<Image>();
            background.color = DuelVisualTheme.Accent;
            background.raycastTarget = true;
            DuelVisualTheme.DressPanel(background);

            storyButton = rootObject.GetComponent<Button>();
            storyButton.targetGraphic = background;
            storyButton.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(storyButton, true);
            storyButton.onClick.AddListener(OpenStory);

            var captionObject = new GameObject("Lobby Story Entry Caption", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            captionObject.transform.SetParent(root, false);
            RectTransform captionRect = captionObject.GetComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(10f, 6f);
            captionRect.offsetMax = new Vector2(-10f, -6f);
            caption = captionObject.GetComponent<Text>();
            caption.font = Resources.Load<Font>(FontResourcePath);
            caption.fontSize = 23;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            caption.raycastTarget = false;
            caption.supportRichText = false;

            root.SetAsLastSibling();
            initialized = true;
        }

        private bool TryValidateStory(out string message)
        {
            TextAsset source = Resources.Load<TextAsset>(DialogueResourcePath);
            if (source == null)
            {
                message = $"스토리 문서를 찾을 수 없습니다: Resources/{DialogueResourcePath}.txt";
                return false;
            }
            if (string.IsNullOrWhiteSpace(source.text))
            {
                message = $"스토리 문서가 비어 있습니다: Resources/{DialogueResourcePath}.txt";
                return false;
            }

            try
            {
                DialogueScriptParser.Parse(DialogueResourcePath, source.text);
                message = string.Empty;
                return true;
            }
            catch (DialogueParseException exception)
            {
                message = exception.Message;
                return false;
            }
        }

        private void OpenStory()
        {
            if (!storyReady || !ShouldShow()) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (controller.StartDialogue(DialogueResourcePath))
            {
                root.gameObject.SetActive(false);
                return;
            }
            RefreshAvailability();
        }

        private void RefreshVisibility(bool force)
        {
            if (root == null) return;
            bool visible = ShouldShow();
            if (visible && !wasHomeEligible) RefreshAvailability();
            wasHomeEligible = visible;
            if (force || root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        }

        private bool ShouldShow()
            => controller != null && controller.IsInLobby && controller.LobbyHud != null &&
               controller.LobbyHud.IsVisible && controller.LobbyHud.CurrentTab == LobbyTab.Home;
    }
}
