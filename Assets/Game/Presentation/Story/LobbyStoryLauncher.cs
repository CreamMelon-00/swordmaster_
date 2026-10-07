using System.Collections.Generic;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed class StoryReplayEntry
    {
        public string Title { get; }
        public string ResourcePath { get; }
        public int MissionNumber { get; }

        internal StoryReplayEntry(string title, string resourcePath, int missionNumber)
        {
            Title = title;
            ResourcePath = resourcePath;
            MissionNumber = missionNumber;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(DuelPrototypeController))]
    public sealed class LobbyStoryLauncher : MonoBehaviour
    {
        private const string FontResourcePath = "LegacyDuel/Font/neodgm";
        private const int LastAuthoredMission = 4;

        private readonly List<StoryReplayEntry> availableScenes = new List<StoryReplayEntry>();
        private DuelPrototypeController controller;
        private RectTransform root;
        private RectTransform replayListRoot;
        private Button storyButton;
        private Text caption;
        private Font font;
        private bool initialized;
        private bool storyReady;
        private bool wasHomeEligible;
        private string availabilityMessage = string.Empty;

        public GameObject Root => root == null ? null : root.gameObject;
        public Button StoryButton => storyButton;
        public bool IsVisible => root != null && root.gameObject.activeSelf;
        public bool IsStoryReady => storyReady;
        public bool IsReplayListOpen => replayListRoot != null && replayListRoot.gameObject.activeSelf;
        public IReadOnlyList<StoryReplayEntry> AvailableScenes => availableScenes;
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

            font = Resources.Load<Font>(FontResourcePath);
            CreateView();
            RefreshVisibility(true);
        }

        private void OnEnable()
        {
            if (initialized) RefreshVisibility(true);
        }

        private void Update()
        {
            if (!initialized) return;
            if (IsReplayListOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseReplayList();
            RefreshVisibility(false);
        }

        private void OnDisable()
        {
            wasHomeEligible = false;
            CloseReplayList();
            if (root != null) root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (storyButton != null) storyButton.onClick.RemoveListener(OpenReplayList);
            DestroyView(root);
            DestroyView(replayListRoot);
        }

        public void RefreshAvailability()
        {
            availableScenes.Clear();
            availabilityMessage = string.Empty;
            AddScene("깨어남", DuelPrototypeController.OpeningCutscene, 0);
            if (controller?.Prologue != null)
            {
                for (int number = 1; number <= LastAuthoredMission; number++)
                {
                    if (!controller.Prologue.IsCleared(number)) continue;
                    PrologueMission mission = PrologueMissions.Get(number);
                    AddScene("시작", mission.IntroCutscene, number);
                    if (mission.Empowerment != null) AddScene("수훈", mission.Empowerment.Scene, number);
                    AddScene("끝", mission.OutroCutscene, number);
                }
            }

            storyReady = availableScenes.Count > 0;
            if (!storyReady && availabilityMessage.Length == 0) availabilityMessage = "다시 볼 장면이 없습니다.";
            if (storyButton != null) storyButton.interactable = storyReady;
            if (caption != null)
            {
                caption.text = storyReady ? "스토리 다시보기" : "스토리 준비 중";
                caption.color = storyReady ? DuelVisualTheme.Ink : DuelVisualTheme.Muted;
            }
            if (availabilityMessage.Length > 0) Debug.LogWarning(availabilityMessage);
            if (IsReplayListOpen)
            {
                CreateReplayList();
                replayListRoot.gameObject.SetActive(true);
            }
        }

        public void CloseReplayList()
        {
            if (replayListRoot != null) replayListRoot.gameObject.SetActive(false);
        }

        public bool ReplayScene(string resourcePath)
        {
            if (!IsReplayListOpen || !ShouldShow()) return false;
            StoryReplayEntry chosen = null;
            foreach (StoryReplayEntry scene in availableScenes)
                if (scene.ResourcePath == resourcePath) { chosen = scene; break; }
            if (chosen == null) return false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (!controller.ReplayStoryCutscene(chosen.ResourcePath, chosen.MissionNumber))
            {
                RefreshAvailability();
                return false;
            }

            CloseReplayList();
            root.gameObject.SetActive(false);
            return true;
        }

        private void AddScene(string title, string resourcePath, int missionNumber)
        {
            TextAsset source = Resources.Load<TextAsset>(resourcePath);
            if (source == null || string.IsNullOrWhiteSpace(source.text))
            {
                if (availabilityMessage.Length == 0)
                    availabilityMessage = $"스토리 장면을 찾을 수 없습니다: Resources/{resourcePath}.txt";
                return;
            }
            try
            {
                if (missionNumber == 0) CutsceneScriptParser.Parse(resourcePath, source.text);
                else CutsceneScriptParser.Parse(resourcePath, source.text, PrologueMissions.Get(missionNumber).SceneCast);
                availableScenes.Add(new StoryReplayEntry(title, resourcePath, missionNumber));
            }
            catch (CutsceneParseException exception)
            {
                if (availabilityMessage.Length == 0) availabilityMessage = exception.Message;
            }
        }

        private void CreateView()
        {
            var rootObject = new GameObject("Lobby Story Entry", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            rootObject.transform.SetParent(controller.LobbyHud.Root.transform, false);
            root = rootObject.GetComponent<RectTransform>();
            // Keep the global action under the header note and above the destination area.
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-24f, -112f);
            root.sizeDelta = new Vector2(280f, 64f);

            Image background = rootObject.GetComponent<Image>();
            background.color = DuelVisualTheme.Accent;
            background.raycastTarget = true;
            DuelVisualTheme.DressPanel(background);

            storyButton = rootObject.GetComponent<Button>();
            storyButton.targetGraphic = background;
            storyButton.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(storyButton, true);
            storyButton.onClick.AddListener(OpenReplayList);

            caption = MakeText("Lobby Story Entry Caption", root, "", 23, DuelVisualTheme.Ink);
            RectTransform captionRect = caption.rectTransform;
            Stretch(captionRect);
            captionRect.offsetMin = new Vector2(10f, 6f);
            captionRect.offsetMax = new Vector2(-10f, -6f);

            root.SetAsLastSibling();
            initialized = true;
        }

        private void OpenReplayList()
        {
            if (!storyReady || !ShouldShow()) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            RefreshAvailability();
            if (!storyReady) return;
            CreateReplayList();
            replayListRoot.gameObject.SetActive(true);
            replayListRoot.SetAsLastSibling();
        }

        private void CreateReplayList()
        {
            if (replayListRoot != null)
            {
                replayListRoot.gameObject.SetActive(false);
                DestroyView(replayListRoot);
            }

            var overlay = new GameObject("Story Replay Overlay", typeof(RectTransform));
            overlay.transform.SetParent(controller.LobbyHud.Root.transform, false);
            replayListRoot = overlay.GetComponent<RectTransform>();
            Stretch(replayListRoot);

            var shade = MakePanel("Story Replay Shade", replayListRoot, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, .74f));
            Stretch(shade);
            Button shadeButton = shade.gameObject.AddComponent<Button>();
            shadeButton.targetGraphic = shade.GetComponent<Image>();
            shadeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            shadeButton.onClick.AddListener(CloseReplayList);

            bool hasOpening = HasMissionScene(0);
            int rows = hasOpening ? 1 : 0;
            for (int number = 1; number <= LastAuthoredMission; number++)
                if (HasMissionScene(number)) rows++;
            float height = 180f + rows * 82f;
            RectTransform panel = MakePanel("Story Replay Ledger", replayListRoot, Vector2.zero,
                new Vector2(850f, height), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(panel.GetComponent<Image>());

            float top = height * .5f;
            MakeText("Story Replay Heading", panel, "이야기 기록", 32, DuelVisualTheme.Foreground,
                new Vector2(-150f, top - 61f), new Vector2(430f, 46f), TextAnchor.MiddleLeft);
            MakeText("Story Replay Hint", panel, "지나온 장면을 골라 다시 봅니다.", 17, DuelVisualTheme.Muted,
                new Vector2(-80f, top - 104f), new Vector2(570f, 28f), TextAnchor.MiddleLeft);
            RectTransform rule = MakePanel("Story Replay Rule", panel, new Vector2(0f, top - 131f),
                new Vector2(770f, 2f), DuelVisualTheme.Border);
            rule.GetComponent<Image>().raycastTarget = false;
            MakeButton("Story Replay Close", panel, "닫기  ×", new Vector2(347f, top - 62f),
                new Vector2(106f, 40f), CloseReplayList, false);

            int row = 0;
            if (hasOpening) AddReplayRow(panel, "깨어남", 0, top - 178f - 82f * row++);
            for (int number = 1; number <= LastAuthoredMission; number++)
                if (HasMissionScene(number))
                    AddReplayRow(panel, $"{number:00}  ·  {PrologueMissions.Get(number).Title}", number,
                        top - 178f - 82f * row++);
            overlay.SetActive(false);
            replayListRoot.SetAsLastSibling();
        }

        private bool HasMissionScene(int number)
        {
            foreach (StoryReplayEntry scene in availableScenes)
                if (scene.MissionNumber == number) return true;
            return false;
        }

        private void AddReplayRow(RectTransform panel, string heading, int missionNumber, float y)
        {
            RectTransform row = MakePanel("Story Replay Row " + missionNumber, panel, new Vector2(0f, y),
                new Vector2(770f, 68f), missionNumber % 2 == 0 ? DuelVisualTheme.RaisedSurface : DuelVisualTheme.Card);
            DuelVisualTheme.Frame(row.GetComponent<Image>());
            Text label = MakeText("Story Replay Row Title", row, heading, 21, DuelVisualTheme.Foreground,
                new Vector2(-198.5f, 0f), new Vector2(305f, 46f), TextAnchor.MiddleLeft);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 17;
            label.resizeTextMaxSize = 21;

            if (missionNumber == 0)
            {
                AddSceneButton(row, DuelPrototypeController.OpeningCutscene, "다시 보기  ▶", 255f, 188f);
                return;
            }

            PrologueMission mission = PrologueMissions.Get(missionNumber);
            if (mission.Empowerment == null)
            {
                AddSceneButton(row, mission.IntroCutscene, "시작  ▶", 113f, 135f);
                AddSceneButton(row, mission.OutroCutscene, "끝  ▶", 270f, 135f);
            }
            else
            {
                AddSceneButton(row, mission.IntroCutscene, "시작", 66f, 90f);
                AddSceneButton(row, mission.Empowerment.Scene, "수훈", 175f, 90f);
                AddSceneButton(row, mission.OutroCutscene, "끝  ▶", 284f, 90f);
            }
        }

        private void AddSceneButton(RectTransform row, string path, string label, float x, float width)
        {
            StoryReplayEntry scene = null;
            foreach (StoryReplayEntry available in availableScenes)
                if (available.ResourcePath == path) { scene = available; break; }
            if (scene == null) return;
            MakeButton("Story Replay " + path, row, label, new Vector2(x, 0f),
                new Vector2(width, 46f), () => ReplayScene(path), true);
        }

        private Text MakeText(string name, Transform parent, string value, int size, Color color,
            Vector2? position = null, Vector2? dimensions = null, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchoredPosition = position ?? Vector2.zero;
            rect.sizeDelta = dimensions ?? Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private Button MakeButton(string name, Transform parent, string label, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction action, bool primary)
        {
            RectTransform rect = MakePanel(name, parent, position, size,
                primary ? DuelVisualTheme.Accent : DuelVisualTheme.Selected);
            Image image = rect.GetComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            button.onClick.AddListener(action);
            MakeText(name + " Caption", rect, label, 18,
                primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground,
                Vector2.zero, new Vector2(size.x - 14f, size.y - 6f));
            return button;
        }

        private static RectTransform MakePanel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = panel.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void DestroyView(RectTransform view)
        {
            if (view == null) return;
            if (Application.isPlaying) Destroy(view.gameObject);
            else DestroyImmediate(view.gameObject);
        }

        private void RefreshVisibility(bool force)
        {
            if (root == null) return;
            bool visible = ShouldShow();
            if (visible && !wasHomeEligible) RefreshAvailability();
            wasHomeEligible = visible;
            if (!visible) CloseReplayList();
            if (force || root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        }

        private bool ShouldShow()
            => controller != null && controller.IsInLobby && controller.LobbyHud != null &&
               controller.LobbyHud.IsVisible && controller.LobbyHud.CurrentTab == LobbyTab.Home;
    }
}
