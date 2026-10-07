using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>The start card at the opening of a battle (after its intro scene, before the first planning turn): letterbox
    /// bars slide in over the dimmed arena, 엘리사's silhouette and name come in on the left and the opponent's (the
    /// briefing's silhouette, the name the battle uses) on the right, each against a soft brass backlight, and two crossed
    /// swords close between them; it holds a moment, then fades out as the bars slide away (<see cref="LegacyStartCard"/>
    /// has the timing). The only words are the two names: the card never says what kind of fight this is
    /// (<c>Docs/DuelAndBattle.md</c>). A click anywhere on it asks to skip it (the controller also takes Enter, Space and
    /// Escape). Its own overlay canvas sits over the duel HUD (100), the coach (300) and its bars
    /// (<see cref="DuelLetterbox"/>, 350), under the result (400) and the cutscene layers (450).</summary>
    public sealed class DuelStartCard : IDisposable
    {
        public const int SortingOrder = 355;
        public const string PlayerName = "엘리사";
        /// <summary>엘리사's silhouette: her first idle frame, drawn dark like the briefing's enemies.</summary>
        public const string PlayerSilhouette = MobStudentAnimationSet.ResourceRoot + "idle/frame-01";
        /// <summary>Each side's silhouette box and how far out it rests (HUD units at the 1920x1080 reference), and how far
        /// further out it starts its slide.</summary>
        public const float FigureWidth = 380f, FigureHeight = 520f, FigureX = 520f, FigureSlide = 170f;
        public const float MarkSize = 230f;
        /// <summary>How wide the swords open before they close to <see cref="DuelCrossedSwords.RestSpread"/>, and how much
        /// larger they start.</summary>
        public const float MarkOpenSpread = 70f, MarkStartScale = 1.25f;
        private const float FigureY = 24f, NameY = -282f, NameWidth = 560f, NameHeight = 64f;
        private const int NameFontSize = 46;
        private static readonly Color Veil = new Color(.04f, .035f, .03f, .62f);
        // A warm glow behind each silhouette, so the dark figure reads against the dimmed forest.
        private static readonly Color Backlight = new Color(DuelVisualTheme.Accent.r, DuelVisualTheme.Accent.g, DuelVisualTheme.Accent.b, .32f);
        private const int BacklightTexels = 64;
        private static readonly Color Rim = new Color(DuelVisualTheme.Accent.r, DuelVisualTheme.Accent.g, DuelVisualTheme.Accent.b, .5f);
        private static readonly Color NameShadow = new Color(0f, 0f, 0f, .85f);

        private readonly LegacyStartCard timeline = new LegacyStartCard();
        private readonly LegacyDuelArt art;
        private readonly RectTransform root;
        private readonly DuelLetterbox letterbox;
        private readonly Image veil;
        private readonly Button skipButton;
        private readonly Side player, opponent;
        private readonly DuelCrossedSwords swords;
        private readonly CanvasGroup swordsGroup;
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly Texture2D backlightTexture;
        private readonly Sprite backlightSprite;
        private bool disposed;

        /// <param name="skip">A click on the card: the controller decides (it skips the card).</param>
        public DuelStartCard(Transform parent, LegacyDuelArt art, Action skip)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            letterbox = new DuelLetterbox(parent);
            root = Rect("Duel Start Card", parent);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();

            // The veil dims the arena and takes every click on the card, so none reaches the duel HUD under it.
            veil = Graphic<Image>("Start Card Veil", root);
            Stretch(veil.rectTransform);
            veil.color = Color.clear;
            veil.raycastTarget = true;
            skipButton = veil.gameObject.AddComponent<Button>();
            skipButton.transition = Selectable.Transition.None;
            skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
            skipButton.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                skip?.Invoke();
            });

            backlightTexture = CreateBacklightTexture();
            backlightSprite = Sprite.Create(backlightTexture, new Rect(0f, 0f, BacklightTexels, BacklightTexels), Vector2.one * .5f,
                100f, 0, SpriteMeshType.FullRect);
            backlightSprite.name = backlightTexture.name;
            backlightSprite.hideFlags = HideFlags.HideAndDontSave;
            player = CreateSide("Start Card Player", -1f);
            opponent = CreateSide("Start Card Opponent", 1f);
            var mark = Rect("Start Card Mark", root);
            mark.anchoredPosition = new Vector2(0f, FigureY);
            mark.sizeDelta = Vector2.one * MarkSize;
            swordsGroup = mark.gameObject.AddComponent<CanvasGroup>();
            swordsGroup.interactable = swordsGroup.blocksRaycasts = false;
            swords = Graphic<DuelCrossedSwords>("Crossed Swords", mark);
            Stretch(swords.rectTransform);
            swords.raycastTarget = false;
            Hide();
        }

        public GameObject Root => root.gameObject;
        public LegacyStartCard Timeline => timeline;
        public DuelLetterbox Letterbox => letterbox;
        /// <summary>Whether the card is up: it holds the battle.</summary>
        public bool IsShowing => !disposed && timeline.IsShowing;
        /// <summary>Whether the card has begun to leave: what it covers may come back under it.</summary>
        public bool IsLeaving => !disposed && timeline.IsLeaving;
        public Button SkipButton => skipButton;
        public Text PlayerLabel => player.Name;
        public Text OpponentLabel => opponent.Name;
        public Image PlayerFigure => player.Figure;
        public Image OpponentFigure => opponent.Figure;
        public DuelCrossedSwords Swords => swords;
        public float SwordsOpacity => swordsGroup.alpha;

        /// <summary>Puts the card up for <paramref name="seconds"/> of real time (0: none) with 엘리사 on the left and
        /// <paramref name="opponentName"/>, drawn from the sprite at <paramref name="opponentSilhouette"/> (Resources), on
        /// the right.</summary>
        public void Show(float seconds, string opponentName, string opponentSilhouette)
        {
            if (disposed) return;
            timeline.Begin(seconds);
            if (!timeline.IsShowing)
            {
                Hide();
                return;
            }
            SetSide(player, PlayerName, PlayerSilhouette);
            SetSide(opponent, opponentName, opponentSilhouette);
            root.gameObject.SetActive(true);
            Apply();
        }

        /// <summary>A frame of real time. Returns true on the frame the card ends by itself.</summary>
        public bool Tick(float realDelta)
        {
            if (!IsShowing) return false;
            if (timeline.Advance(Mathf.Max(0f, realDelta)))
            {
                Hide();
                return true;
            }
            Apply();
            return false;
        }

        /// <summary>Gone at once (skipped, or the battle left or restarted).</summary>
        public void Clear()
        {
            if (disposed) return;
            timeline.Skip();
            Hide();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            timeline.Skip();
            skipButton.onClick.RemoveAllListeners();
            letterbox.Dispose();
            sprites.Clear();
            Release(backlightSprite);
            Release(backlightTexture);
            if (root != null) Release(root.gameObject);
        }

        private void Apply()
        {
            float bars = timeline.BarsAmount;
            letterbox.SetAmount(bars);
            veil.color = new Color(Veil.r, Veil.g, Veil.b, Veil.a * bars);
            float slide = FigureSlide * (1f - timeline.FiguresAmount);
            Place(player, slide);
            Place(opponent, slide);
            float mark = timeline.MarkAmount;
            swords.Spread = Mathf.Lerp(MarkOpenSpread, DuelCrossedSwords.RestSpread, mark);
            swords.rectTransform.localScale = Vector3.one * Mathf.Lerp(MarkStartScale, 1f, mark);
            swordsGroup.alpha = timeline.MarkOpacity;
        }

        private void Place(Side side, float slide)
        {
            side.Frame.anchoredPosition = new Vector2(side.Direction * (FigureX + slide), FigureY);
            side.Group.alpha = timeline.FiguresOpacity;
            Color ink = side.Name.color;
            ink.a = timeline.NamesOpacity;
            side.Name.color = ink;
        }

        private void Hide()
        {
            if (disposed) return;
            letterbox.SetAmount(0f);
            veil.color = Color.clear;
            root.gameObject.SetActive(false);
        }

        private void SetSide(Side side, string name, string silhouette)
        {
            side.Name.text = name ?? string.Empty;
            side.Figure.sprite = LoadSprite(silhouette);
            side.Figure.enabled = side.Figure.sprite != null;
        }

        private Side CreateSide(string name, float direction)
        {
            RectTransform frame = Rect(name, root);
            frame.sizeDelta = new Vector2(FigureWidth, FigureHeight);
            var group = frame.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            var backlight = Graphic<Image>("Backlight", frame);
            backlight.sprite = backlightSprite;
            backlight.rectTransform.sizeDelta = new Vector2(FigureWidth * 1.25f, FigureHeight * 1.1f);
            backlight.color = Backlight;
            backlight.raycastTarget = false;
            var figure = Graphic<Image>("Silhouette", frame);
            Stretch(figure.rectTransform);
            figure.preserveAspect = true;
            figure.color = MissionBriefingHud.SilhouetteColor;
            figure.raycastTarget = false;
            // A thin brass rim around the dark figure, so it reads on the dimmed forest.
            var rim = figure.gameObject.AddComponent<Outline>();
            rim.effectColor = Rim;
            rim.effectDistance = new Vector2(2.5f, -2.5f);
            rim.useGraphicAlpha = true;
            var label = Graphic<Text>("Name", frame);
            label.rectTransform.anchoredPosition = new Vector2(0f, NameY - FigureY);
            label.rectTransform.sizeDelta = new Vector2(NameWidth, NameHeight);
            label.font = art.UIFont;
            label.fontSize = NameFontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.color = DuelVisualTheme.Foreground;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = NameShadow;
            shadow.effectDistance = new Vector2(3f, -3f);
            return new Side(frame, group, figure, label, direction);
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

        // A white disc whose alpha falls off from the centre; the Image tints it.
        private static Texture2D CreateBacklightTexture()
        {
            var pixels = new Color32[BacklightTexels * BacklightTexels];
            float centre = (BacklightTexels - 1) * .5f;
            for (int y = 0; y < BacklightTexels; y++)
                for (int x = 0; x < BacklightTexels; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / (BacklightTexels * .5f);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.6f);
                    pixels[y * BacklightTexels + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            var texture = new Texture2D(BacklightTexels, BacklightTexels, TextureFormat.RGBA32, false)
            {
                name = "Start Card Backlight",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private static T Graphic<T>(string name, Transform parent) where T : Graphic
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            node.layer = 5;
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            return node.GetComponent<T>();
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
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

        private sealed class Side
        {
            public readonly RectTransform Frame;
            public readonly CanvasGroup Group;
            public readonly Image Figure;
            public readonly Text Name;
            /// <summary>-1 on the left, 1 on the right.</summary>
            public readonly float Direction;

            public Side(RectTransform frame, CanvasGroup group, Image figure, Text name, float direction)
            {
                Frame = frame;
                Group = group;
                Figure = figure;
                Name = name;
                Direction = direction;
            }
        }
    }
}
