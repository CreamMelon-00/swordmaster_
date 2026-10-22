using System;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.Tavern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A small spectator strip while the existing arena plays, then the tavern's own settled result.
    /// The owner executes exits and round changes; the view never awards income or records a result.</summary>
    public sealed class SkillTavernBattleHud : IDisposable
    {
        public const int SortingOrder = 420;
        private readonly LegacyDuelArt art;
        private readonly RectTransform root;
        private readonly GameObject strip, resultBackdrop, ownedEventSystem;
        private readonly Text battleCaption, resultHeading, resultCaption, turns, playerHealth, enemyHealth, record;
        private readonly Button formationButton, nextRoundButton, titleButton;
        private readonly Action returnToFormation, nextRound, returnToTitle;
        private SkillTavernRun shownRun;
        private SkillTavernBattle shownBattle;
        private bool actionPending, disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public bool IsShowingResult => IsVisible && resultBackdrop.activeSelf;

        public SkillTavernBattleHud(Transform parent, LegacyDuelArt art, Action returnToFormation,
            Action nextRound, Action returnToTitle)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.returnToFormation = returnToFormation;
            this.nextRound = nextRound;
            this.returnToTitle = returnToTitle;
            root = Rect("Skill Tavern Battle HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Skill Tavern Battle EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(parent, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            Image battleStrip = Panel("Tavern Battle Strip", root, new Vector2(0f, -42f),
                new Vector2(1740f, 60f), new Color(DuelVisualTheme.Surface.r, DuelVisualTheme.Surface.g, DuelVisualTheme.Surface.b, .95f));
            battleStrip.rectTransform.anchorMin = battleStrip.rectTransform.anchorMax = new Vector2(.5f, 1f);
            DuelVisualTheme.Frame(battleStrip);
            strip = battleStrip.gameObject;
            battleCaption = Label("Tavern Battle Caption", strip.transform, new Vector2(-160f, 0f),
                new Vector2(1350f, 46f), 25, DuelVisualTheme.Foreground);
            formationButton = ActionButton("Tavern Battle Return", strip.transform, "편성으로  [Esc]",
                new Vector2(700f, 0f), new Vector2(264f, 44f), () => Request(false, this.returnToFormation));

            Image veil = Panel("Tavern Result Backdrop", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .79f));
            Stretch(veil.rectTransform);
            veil.raycastTarget = true;
            resultBackdrop = veil.gameObject;
            Image card = Panel("Tavern Result Card", veil.transform, Vector2.zero, new Vector2(900f, 600f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(card);
            resultHeading = Label("Tavern Result Heading", card.transform, new Vector2(0f, 221f),
                new Vector2(820f, 90f), 68, DuelVisualTheme.Accent, TextAnchor.MiddleCenter);
            resultCaption = Label("Tavern Result Caption", card.transform, new Vector2(0f, 162f),
                new Vector2(820f, 42f), 24, DuelVisualTheme.Muted, TextAnchor.MiddleCenter);
            Panel("Tavern Result Rule", card.transform, new Vector2(0f, 126f), new Vector2(802f, 2f), DuelVisualTheme.Border);
            turns = Statistic(card.transform, "Tavern Result Turns", "전투 턴", -265f);
            playerHealth = Statistic(card.transform, "Tavern Result Player HP", "내 남은 HP", 0f);
            enemyHealth = Statistic(card.transform, "Tavern Result Enemy HP", "상대 남은 HP", 265f);
            record = Label("Tavern Result Record", card.transform, new Vector2(0f, -46f),
                new Vector2(802f, 48f), 26, DuelVisualTheme.Foreground, TextAnchor.MiddleCenter);
            Label("Tavern Result Round Hint", card.transform, new Vector2(0f, -124f),
                new Vector2(802f, 82f), 21, DuelVisualTheme.Muted, TextAnchor.MiddleCenter).text =
                "다음 라운드에는 자원을 지급하고 상점을 새로 채웁니다.\n편성과 보관함은 유지되며, 남은 자원은 이월되지 않습니다.";
            titleButton = ActionButton("Tavern Result Title", card.transform, "타이틀로",
                new Vector2(-176f, -231f), new Vector2(280f, 60f), () => Request(true, this.returnToTitle));
            nextRoundButton = ActionButton("Tavern Result Next Round", card.transform, "다음 라운드",
                new Vector2(176f, -231f), new Vector2(320f, 60f), () => Request(true, this.nextRound), true);
            Hide();
        }

        public void ShowBattle(SkillTavernRun run, SkillTavernBattle battle)
        {
            if (!Show(run, battle, false)) return;
            battleCaption.text = "선술집 " + run.RoundNumber + "라운드 · 자동 전투 · " + battle.EnemyName;
            formationButton.interactable = !actionPending && returnToFormation != null;
        }

        public void ShowResult(SkillTavernRun run, SkillTavernBattle battle)
        {
            if (disposed) return;
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            if (!battle.Duel.IsFinished) throw new InvalidOperationException("Only a settled tavern battle has a result.");
            if (!Show(run, battle, true)) return;
            DuelMatchOutcome outcome = battle.Duel.Outcome;
            resultHeading.text = outcome == DuelMatchOutcome.PlayerVictory ? "승리"
                : outcome == DuelMatchOutcome.EnemyVictory ? "패배" : "무승부";
            resultHeading.color = outcome == DuelMatchOutcome.PlayerVictory ? DuelVisualTheme.Accent
                : outcome == DuelMatchOutcome.EnemyVictory ? DuelVisualTheme.Danger : DuelVisualTheme.Foreground;
            resultCaption.text = "선술집 " + run.RoundNumber + "라운드 · " + battle.EnemyName;
            turns.text = battle.Duel.RoundNumber.ToString();
            playerHealth.text = Mathf.Max(0, battle.Duel.Player.Health) + " / " + SkillTavernBattle.FighterHealth;
            enemyHealth.text = Mathf.Max(0, battle.Duel.Enemy.Health) + " / " + SkillTavernBattle.FighterHealth;
            record.text = "누적 " + run.BattlesPlayed + "전 · " + run.Wins + "승 · " + run.Losses + "패 · " + run.Draws + "무";
            nextRoundButton.interactable = !actionPending && nextRound != null;
            titleButton.interactable = !actionPending && returnToTitle != null;
        }

        private bool Show(SkillTavernRun run, SkillTavernBattle battle, bool result)
        {
            if (disposed) return false;
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            if (!IsVisible || !ReferenceEquals(shownRun, run) || !ReferenceEquals(shownBattle, battle) || IsShowingResult != result)
                actionPending = false;
            shownRun = run;
            shownBattle = battle;
            root.gameObject.SetActive(true);
            resultBackdrop.SetActive(result);
            strip.SetActive(!result);
            ClearSelection();
            return true;
        }

        private void Request(bool resultOnly, Action action)
        {
            if (!IsVisible || IsShowingResult != resultOnly || actionPending || action == null) return;
            actionPending = true;
            formationButton.interactable = nextRoundButton.interactable = titleButton.interactable = false;
            ClearSelection();
            action();
        }

        public void Hide()
        {
            if (disposed) return;
            root.gameObject.SetActive(false);
            ClearSelection();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            root.gameObject.SetActive(false);
            formationButton.onClick.RemoveAllListeners();
            nextRoundButton.onClick.RemoveAllListeners();
            titleButton.onClick.RemoveAllListeners();
            shownRun = null;
            shownBattle = null;
            Destroy(root.gameObject);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
        }

        private Text Statistic(Transform parent, string name, string caption, float x)
        {
            RectTransform cell = Rect(name + " Cell", parent, new Vector2(x, 55f), new Vector2(246f, 98f));
            Label(name + " Label", cell, new Vector2(0f, 31f), new Vector2(246f, 30f), 19,
                DuelVisualTheme.Muted, TextAnchor.MiddleCenter).text = caption;
            Text value = Label(name, cell, new Vector2(0f, -16f), new Vector2(246f, 58f), 42,
                DuelVisualTheme.Foreground, TextAnchor.MiddleCenter);
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 22;
            value.resizeTextMaxSize = 42;
            return value;
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 center, Vector2 size,
            Action action, bool primary = false)
        {
            Image image = Panel(name, parent, center, size, primary ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label("Button Label", image.transform, Vector2.zero, size - new Vector2(12f, 4f), 23,
                primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground, TextAnchor.MiddleCenter).text = caption;
            button.onClick.AddListener(() => { if (!disposed) action?.Invoke(); });
            return button;
        }

        private Image Panel(string name, Transform parent, Vector2 center, Vector2 size, Color color)
        {
            Image image = Rect(name, parent, center, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(string name, Transform parent, Vector2 center, Vector2 size, int points, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text label = Rect(name, parent, center, size).gameObject.AddComponent<Text>();
            label.font = art.UIFont;
            label.fontSize = points;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 center, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = rect.anchoredPosition = Vector2.zero;
        }

        private static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static void Destroy(GameObject target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
