using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A StarCraft II-style briefing: the mission's background, title, objectives and the silhouettes of
    /// the enemies it brings, with a single start button at the bottom. It shows state and forwards the start request.</summary>
    public sealed class MissionBriefingHud : IDisposable
    {
        public const int SortingOrder = 250;
        public const string ChapterName = "서막";
        private static readonly Color Accent = DuelVisualTheme.Accent;
        private static readonly Color Foreground = DuelVisualTheme.Foreground;
        private static readonly Color Muted = DuelVisualTheme.Muted;
        public static readonly Color SilhouetteColor = new Color(.06f, .05f, .04f, .94f);
        private readonly LegacyDuelArt art;
        private readonly RectTransform root, enemiesRoot;
        private readonly Image background;
        private readonly AspectRatioFitter backgroundFitter;
        private readonly Text chapter, title, objectives;
        private readonly Button startButton;
        private readonly List<GameObject> enemyViews = new List<GameObject>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private bool disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public PrologueMission Mission { get; private set; }
        public Button StartButton => startButton;

        public MissionBriefingHud(Transform parent, LegacyDuelArt art, Action start)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            root = Rect("Mission Briefing HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();

            var backdrop = Panel("Briefing Backdrop", root, Vector2.zero, Vector2.zero, DuelVisualTheme.Surface);
            Stretch(backdrop.rectTransform);
            background = Image("Briefing Background", root, null, Vector2.zero, new Vector2(1920f, 1080f));
            background.preserveAspect = false;
            backgroundFitter = background.gameObject.AddComponent<AspectRatioFitter>();
            backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            Color shadeColor = DuelVisualTheme.Track;
            shadeColor.a = .45f;
            Stretch(Panel("Briefing Shade", root, Vector2.zero, Vector2.zero, shadeColor).rectTransform);
            Color bandColor = DuelVisualTheme.Track;
            bandColor.a = .72f;
            var band = Panel("Briefing Bottom Band", root, new Vector2(0f, 70f), new Vector2(0f, 140f), bandColor);
            band.rectTransform.anchorMin = new Vector2(0f, 0f);
            band.rectTransform.anchorMax = new Vector2(1f, 0f);
            band.rectTransform.sizeDelta = new Vector2(0f, 140f);

            chapter = Label("Briefing Chapter", root, new Vector2(-880f, 440f), new Vector2(900f, 32f), 24, Muted);
            title = Label("Briefing Title", root, new Vector2(-880f, 372f), new Vector2(1100f, 84f), 64, Foreground);

            var objectivesPanel = Panel("Briefing Objectives", root, new Vector2(-520f, 40f), new Vector2(760f, 380f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(objectivesPanel);
            Label("Objectives Heading", objectivesPanel.transform, new Vector2(-340f, 150f), new Vector2(680f, 36f), 28, Accent).text = "목표";
            objectives = Label("Objectives Text", objectivesPanel.transform, new Vector2(-340f, -20f), new Vector2(680f, 280f), 24, Foreground);
            objectives.alignment = TextAnchor.UpperLeft;
            objectives.lineSpacing = 1.25f;

            var enemiesPanel = Panel("Briefing Enemies", root, new Vector2(500f, 40f), new Vector2(680f, 480f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(enemiesPanel);
            Label("Enemies Heading", enemiesPanel.transform, new Vector2(-300f, 200f), new Vector2(600f, 36f), 28, Accent).text = "등장하는 적";
            enemiesRoot = Rect("Enemy Silhouettes", enemiesPanel.transform, new Vector2(0f, -30f), new Vector2(620f, 380f));

            startButton = StartButtonView(root, start);
            Hide();
        }

        public void Show(PrologueMission mission, int missionCount)
        {
            if (disposed) return;
            Mission = mission ?? throw new ArgumentNullException(nameof(mission));
            root.gameObject.SetActive(true);
            Sprite backgroundSprite = LoadSprite(mission.BackgroundResource);
            background.sprite = backgroundSprite;
            background.enabled = backgroundSprite != null;
            backgroundFitter.aspectRatio = backgroundSprite != null && backgroundSprite.rect.height > 0f
                ? backgroundSprite.rect.width / backgroundSprite.rect.height : 16f / 9f;
            chapter.text = $"{ChapterName}  ·  임무 {mission.Number} / {missionCount}";
            title.text = mission.Title;
            var lines = new System.Text.StringBuilder();
            foreach (string objective in mission.Objectives)
            {
                if (lines.Length > 0) lines.Append('\n');
                lines.Append("•  ").Append(objective);
            }
            objectives.text = lines.ToString();
            BuildEnemies(mission.Enemies);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void Hide()
        {
            if (!disposed) root.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            startButton.onClick.RemoveAllListeners();
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private void BuildEnemies(IReadOnlyList<MissionEnemy> enemies)
        {
            foreach (GameObject view in enemyViews)
            {
                view.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(view);
                else UnityEngine.Object.DestroyImmediate(view);
            }
            enemyViews.Clear();
            float width = Mathf.Min(280f, 600f / Mathf.Max(1, enemies.Count));
            for (int index = 0; index < enemies.Count; index++)
            {
                float x = (index - (enemies.Count - 1) * .5f) * width;
                var view = Rect("Enemy " + (index + 1), enemiesRoot, new Vector2(x, 0f), new Vector2(width, 380f));
                var silhouette = Image("Enemy Silhouette", view, LoadSprite(enemies[index].SilhouetteResource),
                    new Vector2(0f, 30f), new Vector2(width - 20f, 300f));
                silhouette.preserveAspect = true;
                silhouette.color = SilhouetteColor;
                silhouette.enabled = silhouette.sprite != null;
                Text enemyName = Label("Enemy Name", view, new Vector2(-(width - 20f) * .5f, -152f),
                    new Vector2(width - 20f, 36f), 24, Foreground);
                enemyName.alignment = TextAnchor.MiddleCenter;
                enemyName.text = enemies[index].Name;
                enemyViews.Add(view.gameObject);
            }
        }

        private Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (sprites.TryGetValue(path, out Sprite cached)) return cached;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Sprite[] all = Resources.LoadAll<Sprite>(path);
                sprite = all.Length > 0 ? all[0] : null;
            }
            sprites[path] = sprite;
            return sprite;
        }

        private Button StartButtonView(Transform parent, Action start)
        {
            var image = Panel("Mission Start", parent, new Vector2(0f, 70f), new Vector2(440f, 72f), Accent);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, 0f);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, true);
            var caption = Label("Button Label", image.transform, new Vector2(-210f, 0f), new Vector2(420f, 64f), 28, DuelVisualTheme.Ink);
            caption.alignment = TextAnchor.MiddleCenter;
            caption.text = "임무 시작  [Enter]";
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                start?.Invoke();
            });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            RectTransform rect = Rect(name, parent, position, size);
            rect.pivot = new Vector2(0f, .5f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = art.UIFont;
            text.fontSize = fontSize;
            text.color = color;
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

        private static Image Image(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
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
    }
}
