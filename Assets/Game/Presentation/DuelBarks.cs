using System;
using TurnLimbo.Runtime.Barks;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Battle barks (<c>Docs/Barks.md</c>): one-line speech bubbles over a fighter's head during battle, from the
    /// battle's bark file (<c>Resources/Barks/mission-NN.txt</c> or <c>stage-NN.txt</c>; none in training, and a missing file
    /// is simply none). The controller reports the battle's moments, <see cref="BarkTracker"/> decides what is said, and
    /// this draws it. A bubble never pauses the battle and stays its set real time
    /// (<see cref="DuelPresentationSettings.BarkSeconds"/>), so bullet time, slow motion and hit stop never stretch it. It
    /// sits above the fighter's head HUD (status panel and queue row), on screen, never over either fighter's head HUD or
    /// the other bubble (<see cref="BarkLayout"/>); a thought in parentheses reads in the dialogue's monologue colour, with a
    /// trail of beads for a tail. It lives on the duel HUD, so it goes when the HUD does.</summary>
    public sealed class DuelBarks : IDisposable
    {
        /// <summary>A line wider than this (HUD units at the 1920x1080 reference) folds onto a second line.</summary>
        public const float MaximumTextWidth = 460f;
        /// <summary>The room kept between a bubble and a head HUD or the other bubble.</summary>
        public const float Gap = 6f;
        private const float PaddingX = 18f, PaddingY = 9f, MinimumWidth = 64f, ScreenMargin = 16f;
        private const float PopSeconds = .14f, FadeSeconds = .3f;
        // Where the tail may sit along the bubble's lower edge: clear of its corners.
        private const float TailInset = 18f;
        private static readonly Color Surface = new Color(DuelVisualTheme.Surface.r, DuelVisualTheme.Surface.g,
            DuelVisualTheme.Surface.b, .94f);

        private readonly LegacyCombatHud hud;
        private readonly Func<DuelPresentationSettings> settings;
        private readonly RectTransform root;
        private readonly Bubble enemy, player;
        private BarkTracker tracker;
        private bool disposed;

        /// <param name="settings">Read when used, so live tuning (and a test's clone) applies at once.</param>
        public DuelBarks(LegacyCombatHud hud, Font font, Func<DuelPresentationSettings> settings)
        {
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (font == null) throw new ArgumentNullException(nameof(font));
            root = new GameObject("Battle Barks", typeof(RectTransform)).GetComponent<RectTransform>();
            root.gameObject.layer = hud.Root.layer;
            root.SetParent(hud.Root.transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = root.anchoredPosition = Vector2.zero;
            enemy = CreateBubble("Enemy Bark", font);
            player = CreateBubble("Player Bark", font);
        }

        /// <summary>This battle's barks, or null when it has none (no file, training, or between battles).</summary>
        public BarkScript Script => tracker?.Script;
        /// <summary>The rules deciding this battle's lines, or null when it has none.</summary>
        public BarkTracker Tracker => tracker;
        /// <summary>Whether <paramref name="speaker"/>'s bubble is on screen (it may be fading in or out).</summary>
        public bool IsShowing(BarkSpeaker speaker) => !disposed && View(speaker).Root.gameObject.activeSelf;
        /// <summary>What <paramref name="speaker"/>'s bubble says, or null when it is not on screen.</summary>
        public string ShownText(BarkSpeaker speaker) => IsShowing(speaker) ? View(speaker).Label.text : null;
        /// <summary>The bubble's frame (its tail included), for tests and tools.</summary>
        public RectTransform BubbleRect(BarkSpeaker speaker) => View(speaker).Root;
        /// <summary>The bubble's text, for tests and tools.</summary>
        public Text BubbleText(BarkSpeaker speaker) => View(speaker).Label;
        /// <summary>The bubble's tail, for tests and tools.</summary>
        public DuelBarkTail BubbleTail(BarkSpeaker speaker) => View(speaker).Tail;
        /// <summary>The bubble's box last placed, in the duel HUD root's local units (centre origin, y up).</summary>
        public BarkBox PlacedBox(BarkSpeaker speaker) => View(speaker).Box;

        /// <summary>Reads a bark file from Resources: null when there is none (nothing to warn about), and null with a
        /// warning when it cannot be read.</summary>
        public static BarkScript Load(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;
            TextAsset source = Resources.Load<TextAsset>(resourcePath.Trim());
            if (source == null) return null;
            try
            {
                return BarkScriptParser.Parse(resourcePath.Trim(), source.text);
            }
            catch (BarkParseException exception)
            {
                Debug.LogWarning(exception.Message);
                return null;
            }
        }

        /// <summary>A battle attempt begins with the bark file at <paramref name="resourcePath"/> (none: no barks).</summary>
        public void Begin(string resourcePath) => Use(Load(resourcePath));

        /// <summary>A battle attempt begins with <paramref name="script"/>'s barks (null: none). Tests and tools may also
        /// swap a battle's barks this way; <paramref name="pick"/> chooses among a block's lines (random when null).</summary>
        public void Use(BarkScript script, Func<int, int> pick = null)
        {
            if (disposed) return;
            tracker = script != null ? new BarkTracker(script, pick) : null;
            HideViews();
        }

        /// <summary>The battle is over, restarted or left: no bubble stays, and nothing more is said.</summary>
        public void End()
        {
            tracker = null;
            HideViews();
        }

        /// <summary>Something takes the moment over (the mission's event scene, the finishing blow): the bubbles go at once.
        /// What was said stays said.</summary>
        public void Hide()
        {
            tracker?.Hide();
            HideViews();
        }

        /// <summary>The battle's first planning turn is on screen (<c>start</c>; only the first call counts).</summary>
        public void BattleStarted()
        {
            if (Configure()) tracker.BattleStarted();
        }

        /// <summary>The 서막's 수훈 has resumed the battle (<c>sutun</c>).</summary>
        public void Empowered()
        {
            if (Configure()) tracker.Empowered();
        }

        /// <summary>A hit, or a slot's opening effects, that the duel goes on after: what each fighter took.</summary>
        public void Hit(BarkBlow enemyBlow, BarkBlow playerBlow)
        {
            if (Configure()) tracker.Hit(enemyBlow, playerBlow);
        }

        /// <summary>A battle frame, after the duel HUD's layout: the lines age on real time and the bubbles follow the head
        /// HUDs.</summary>
        public void Tick(float realDelta)
        {
            if (disposed) return;
            if (!Configure())
            {
                HideViews();
                return;
            }
            tracker.Advance(Mathf.Max(0f, realDelta));
            bool playerShows = Refresh(player, BarkSpeaker.Player);
            bool enemyShows = Refresh(enemy, BarkSpeaker.Enemy);
            if (!playerShows && !enemyShows) return;
            Rect area = root.rect;
            var screen = new BarkBox(area.xMin + ScreenMargin, area.yMin + ScreenMargin,
                area.width - ScreenMargin * 2f, area.height - ScreenMargin * 2f);
            bool playerLaidOut = hud.TryGetHeadStack(true, out Rect playerRect, out float playerHead);
            bool enemyLaidOut = hud.TryGetHeadStack(false, out Rect enemyRect, out float enemyHead);
            BarkBox playerStack = playerLaidOut ? Box(playerRect) : default, enemyStack = enemyLaidOut ? Box(enemyRect) : default;
            // Whoever stands on the left moves left when there is no room above (the player, unless they have crossed).
            int playerOutward = !enemyLaidOut || playerHead <= enemyHead ? -1 : 1;
            playerShows = Place(player, playerShows && playerLaidOut, playerHead, playerStack, enemyStack, screen, playerOutward);
            enemyShows = Place(enemy, enemyShows && enemyLaidOut, enemyHead, enemyStack, playerStack, screen, -playerOutward);
            if (playerShows && enemyShows)
            {
                BarkBox playerBox = player.Box, enemyBox = enemy.Box;
                if (BarkLayout.Separate(ref playerBox, ref enemyBox, playerStack, enemyStack, screen, Gap))
                {
                    player.Box = playerBox;
                    enemy.Box = enemyBox;
                }
            }
            if (playerShows) Apply(player, playerHead);
            if (enemyShows) Apply(enemy, enemyHead);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            tracker = null;
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root.gameObject);
            else Object.DestroyImmediate(root.gameObject);
        }

        // Hands the live tuning to the rules; false when this battle has no barks.
        private bool Configure()
        {
            if (disposed || tracker == null) return false;
            DuelPresentationSettings tuning = settings();
            tracker.ShowSeconds = tuning.BarkSeconds;
            tracker.HurtCooldownSeconds = tuning.BarkHurtCooldownSeconds;
            tracker.LowHealthPercent = tuning.BarkLowHealthPercent;
            tracker.HeavyHealthPercent = tuning.DecisiveHealthDamagePercent;
            return true;
        }

        private Bubble View(BarkSpeaker speaker) => speaker == BarkSpeaker.Player ? player : enemy;

        // The bubble's words, size, fade and pop for the line on screen; false (and hidden) when there is none.
        private bool Refresh(Bubble view, BarkSpeaker speaker)
        {
            BarkLine line = tracker.Current(speaker);
            if (line == null)
            {
                Hide(view);
                return false;
            }
            int fontSize = settings().BarkFontSize;
            int sequence = tracker.Sequence(speaker);
            if (view.Sequence != sequence || view.FontSize != fontSize)
            {
                view.Sequence = sequence;
                view.FontSize = fontSize;
                view.Label.text = line.Text;
                view.Label.fontSize = fontSize;
                // A thought keeps its parentheses and reads in the dialogue's monologue colour.
                view.Label.color = line.IsMonologue ? DialogueHud.MonologueColor : DuelVisualTheme.Foreground;
                view.Tail.Configure(line.IsMonologue, Surface, DuelVisualTheme.Border);
                view.Size = Measure(view.Label, line.Text);
            }
            float elapsed = tracker.Elapsed(speaker);
            float pop = Mathf.Clamp01(elapsed / PopSeconds);
            view.Group.alpha = pop * Mathf.Clamp01((tracker.ShowSeconds - elapsed) / FadeSeconds);
            // A small rise from the tail as it appears, the size it settles at once it has.
            float ease = 1f - (1f - pop) * (1f - pop);
            view.Root.localScale = Vector3.one * Mathf.Lerp(.86f, 1f, ease);
            return true;
        }

        // The text's size, folded at MaximumTextWidth, plus the bubble's padding and tail. The text gets a little more room
        // than it measures, so rounding never folds a line that fits.
        private static Vector2 Measure(Text label, string text)
        {
            TextGenerator generator = label.cachedTextGeneratorForLayout;
            float pixels = Mathf.Max(.0001f, label.pixelsPerUnit);
            float natural = generator.GetPreferredWidth(text, label.GetGenerationSettings(Vector2.zero)) / pixels;
            float width = Mathf.Ceil(Mathf.Clamp(natural, 1f, MaximumTextWidth)) + 2f;
            float height = generator.GetPreferredHeight(text, label.GetGenerationSettings(new Vector2(width, 0f))) / pixels;
            return new Vector2(Mathf.Max(MinimumWidth, width + PaddingX * 2f),
                Mathf.Ceil(Mathf.Max(height, label.fontSize)) + PaddingY * 2f + DuelBarkTail.Height);
        }

        private bool Place(Bubble view, bool shows, float headX, BarkBox ownStack, BarkBox otherStack, BarkBox screen,
            int outward)
        {
            // Not before its fighter's head HUD has a place (the duel HUD lays it out every battle frame).
            if (!shows)
            {
                view.Group.alpha = 0f;
                return false;
            }
            view.Box = BarkLayout.Place(headX, view.Size.x, view.Size.y, ownStack, otherStack, screen, Gap, outward);
            return true;
        }

        // The tail points at the head from the bubble's lower edge, and the bubble pops from there.
        private static void Apply(Bubble view, float headX)
        {
            BarkBox box = view.Box;
            float tailX = Mathf.Clamp(headX - box.X, Mathf.Min(TailInset, box.Width * .5f),
                Mathf.Max(box.Width - TailInset, box.Width * .5f));
            view.Root.sizeDelta = new Vector2(box.Width, box.Height);
            view.Root.pivot = new Vector2(tailX / Mathf.Max(1f, box.Width), 0f);
            view.Root.anchoredPosition = new Vector2(box.X + tailX, box.Y);
            view.Tail.rectTransform.anchoredPosition = new Vector2(tailX, 0f);
            view.Root.gameObject.SetActive(true);
        }

        private static BarkBox Box(Rect rect) => new BarkBox(rect.xMin, rect.yMin, rect.width, rect.height);

        private void HideViews()
        {
            if (disposed) return;
            Hide(enemy);
            Hide(player);
        }

        private static void Hide(Bubble view)
        {
            view.Sequence = 0;
            view.Box = default;
            view.Group.alpha = 0f;
            view.Root.gameObject.SetActive(false);
        }

        private Bubble CreateBubble(string name, Font font)
        {
            var frame = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            frame.gameObject.layer = root.gameObject.layer;
            frame.SetParent(root, false);
            frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
            frame.pivot = new Vector2(.5f, 0f);
            CanvasGroup group = frame.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var body = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            body.gameObject.layer = frame.gameObject.layer;
            body.rectTransform.SetParent(frame, false);
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.offsetMin = new Vector2(0f, DuelBarkTail.Height);
            body.rectTransform.offsetMax = Vector2.zero;
            body.color = Surface;
            body.raycastTarget = false;
            DuelVisualTheme.Frame(body);

            var label = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            label.gameObject.layer = frame.gameObject.layer;
            label.rectTransform.SetParent(body.rectTransform, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(PaddingX, PaddingY);
            label.rectTransform.offsetMax = new Vector2(-PaddingX, -PaddingY);
            label.font = font;
            label.fontSize = DuelPresentationSettings.DefaultBarkFontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.lineSpacing = 1.05f;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.color = DuelVisualTheme.Foreground;

            // After the bubble, so its wedge draws over the bubble's rim where they join.
            var tail = new GameObject("Tail", typeof(RectTransform), typeof(CanvasRenderer), typeof(DuelBarkTail))
                .GetComponent<DuelBarkTail>();
            tail.gameObject.layer = frame.gameObject.layer;
            tail.rectTransform.SetParent(frame, false);
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = Vector2.zero;
            tail.rectTransform.pivot = new Vector2(.5f, 0f);
            tail.rectTransform.sizeDelta = new Vector2(DuelBarkTail.Width, DuelBarkTail.Height + DuelBarkTail.Overlap);
            tail.raycastTarget = false;
            tail.Configure(false, Surface, DuelVisualTheme.Border);

            frame.gameObject.SetActive(false);
            return new Bubble(frame, group, label, tail);
        }

        private sealed class Bubble
        {
            public readonly RectTransform Root;
            public readonly CanvasGroup Group;
            public readonly Text Label;
            public readonly DuelBarkTail Tail;
            public int Sequence, FontSize;
            public Vector2 Size;
            public BarkBox Box;

            public Bubble(RectTransform root, CanvasGroup group, Text label, DuelBarkTail tail)
            {
                Root = root;
                Group = group;
                Label = label;
                Tail = tail;
            }
        }
    }
}
