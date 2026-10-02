using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Compact duel HUD. Original interaction flow, background-independent presentation.</summary>
    public sealed class LegacyCombatHud : IDisposable
    {
        private readonly LegacyDuelArt art;
        private readonly DuelPresentationSettings presentationSettings;
        private readonly Canvas canvas;
        private readonly RectTransform root;
        private readonly GameObject ownedEventSystem;
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<int, Sprite> hudIcons = new Dictionary<int, Sprite>();
        private readonly Func<int, Sprite> iconFor;
        private readonly RectTransform inputPanel;
        private readonly CanvasGroup inputs;
        private readonly RectTransform timerPanel;
        private readonly Image timerFill, actFill;
        private readonly Image timerTrack;
        private readonly Text untimedHint;
        // 넘기기 pays with planning time: the button shows the price and a '-1초' rises off the timer when it is spent.
        private const float TimeSpentDuration = .7f;
        private static readonly Vector2 TimeSpentHome = new Vector2(294f, -5f);
        private readonly Text cycleCost, timeSpent;
        private float laneCycleCost;
        private bool laneCycleAffordable = true;
        private float timeSpentRemaining;
        private readonly Text turnText, actText, stageText;
        private readonly Button[] laneButtons = new Button[3];
        private readonly CanvasGroup[] guideLaneGroups = new CanvasGroup[3];
        private readonly bool[] laneAffordable = new bool[3];
        private readonly Button breathButton;
        private readonly Text breathCount;
        private readonly RectTransform breathNote;
        private readonly Action queueBreath, cycleLanes;
        private Button cycleButton;
        private RectTransform cycleNote;
        private LegacyQueuedDuel displayedSession;
        private int shownBreathsRemaining = -1;
        private MissionGuide guide;
        private bool lastPlanning;
        private readonly Image[] currentIcons = new Image[3], nextIcons = new Image[3], holdImages = new Image[3];
        private readonly Text[] costs = new Text[3];
        private readonly Text[] skillNames = new Text[3];
        private readonly SkillCardFeedbackGraphic[] laneFeedback = new SkillCardFeedbackGraphic[3];
        private readonly int[] shownSkills = { -1, -1, -1 }, shownNextSkills = { -1, -1, -1 };
        private readonly Button commitButton;
        private readonly CanvasGroup guideCommitGroup;
        private readonly Outline[] guideLaneFocus = new Outline[3];
        private readonly Outline guideCommitFocus;
        private readonly RectTransform guideEnemyFocus, guideActFocus;
        private bool missionMode, missionModeInitialized, missionTimed, missionBreath, highlightEnemyQueue;
        private readonly GameObject[] laneCards = new GameObject[3], nextPanels = new GameObject[3];
        // 넘기기: each turned lane's next skill drops from the next card into the front card while a new next skill
        // slides in, and the card ticks like a gear. Real time, so it plays the same under Tab or bullet time.
        public const float LaneTurnDuration = .14f;
        private static readonly Vector2 CurrentIconHome = new Vector2(0, 10), NextIconHome = new Vector2(0, 7);
        // Where the next card's icon sits, seen from the front card (next card at x+30, y 40; front card at y -4).
        private static readonly Vector2 LaneTurnFrom = new Vector2(30, 44);
        private static readonly Vector2 NextTurnFrom = new Vector2(26, 0);
        private const float NextIconAlpha = .55f;
        private readonly float[] laneTurnElapsed = { LaneTurnDuration, LaneTurnDuration, LaneTurnDuration };
        private int stageNumber, stageCount;
        private string stageName;
        private readonly StatusView playerStatus, enemyStatus;
        private readonly QueueView playerQueue, enemyQueue;
        private readonly RectTransform attackView, topBar, bottomBar;
        private RectTransform logPanel, logContent, logPlayerColumn, logEnemyColumn;
        private ScrollRect logScroll;
        private readonly List<RectTransform[]> logRows = new List<RectTransform[]>();
        private readonly RectTransform playerExplanation, enemyExplanation;
        private readonly Text playerName, playerDetails, playerDescription, enemyName, enemyDetails, enemyDescription;
        private readonly Text playerExplanationHint, enemyExplanationHint;
        private readonly SkillLaneBadge playerStyle, enemyStyle;
        private readonly SkillInfoView playerInfo, enemyInfo;
        private readonly SkillCardFeedbackGraphic playerEffectFeedback, enemyEffectFeedback;
        private readonly Image playerExplanationIcon, enemyExplanationIcon;
        private readonly RectTransform outcomePanel;
        private readonly Text outcomeText, outcomeTurns;
        private readonly List<DamageView> damageTexts = new List<DamageView>();
        private readonly List<DamageView> calloutTexts = new List<DamageView>();
        private readonly List<DamageView> stateTexts = new List<DamageView>();
        private readonly List<int> counterForecast = new List<int>();
        private const int MaximumDamageTexts = 32;
        private const int MaximumCalloutTexts = 4;
        private const int MaximumStateTexts = 4;
        public const string BreakCalloutText = "붕괴 ×2";
        public const string RecoveryCalloutText = "저항 회복";
        // Resistance loss reads in the resistance gauge's steel, lightened for the battlefield.
        public static readonly Color ResistanceDamageInk = new Color(.70f, .86f, .90f);
        private static readonly Color ResistanceDamageOutline = new Color(.05f, .13f, .17f);
        public static readonly Color BreakCalloutInk = new Color(1f, .42f, .30f);
        // World units below the fighter's pivot (upper body): about the waist. A world offset keeps
        // the callout below the damage numbers' band at every camera zoom, including the break close-up.
        public static readonly Vector3 StateCalloutWorldOffset = new Vector3(0f, -1.3f, 0f);
        private static readonly Color BreakCalloutOutline = new Color(.22f, .03f, .02f);
        private int playerDamageSequence, enemyDamageSequence;
        private LegacySkill explainedPlayer, explainedEnemy;
        private LegacySkill conditionPreview;
        private LegacyCurrentSlot feedbackSlot;
        private Camera worldCamera;
        private bool disposed;
        private float transition, targetTransition, slotElapsed, slotDuration;
        private float cinematic, cinematicFrom, cinematicTarget, cinematicElapsed = .3f;
        private float viewAngle, angleFrom, angleTarget, angleElapsed = .15f;
        private float logFrom = 1080, logTarget = 1080, logElapsed = .5f;
        private bool canOpenLog;
        private int inspectedSlot = -1, shownTurn = -1, shownAct = -1;
        private bool slotAnimation;
        private bool assetsAvailable = true;
        private const float DockHeight = 200;
        private static readonly Color Surface = DuelVisualTheme.Surface;
        private static readonly Color RaisedSurface = DuelVisualTheme.RaisedSurface;
        private static readonly Color Card = DuelVisualTheme.Card;
        private static readonly Color Border = DuelVisualTheme.Border;
        private static readonly Color Track = DuelVisualTheme.Track;
        private static readonly Color Accent = DuelVisualTheme.Accent;
        private static readonly Color Foreground = DuelVisualTheme.Foreground;
        private static readonly Color MutedText = DuelVisualTheme.Muted;

        public bool HasRequiredAssets => assetsAvailable && art.UIFont != null;
        public GameObject Root => root.gameObject;
        public bool LogOpen { get; private set; }
        public int LogCount => logRows.Count;

        /// <summary>The displayed queue card, including its pulse and head attachment.</summary>
        public RectTransform GetQueuedSkillAnchor(bool playerSide, int queueIndex)
            => disposed ? null : (playerSide ? playerQueue : enemyQueue).GetAnchor(queueIndex);

        public void BeginCombat() => SetCinematic(1);

        public void BeginReturn()
        {
            SetCinematic(0);
            SetViewAngle(0);
        }

        public void RecordResolvedSlot(LegacySkill player, LegacySkill enemy, int playerDamageDealt, int enemyDamageDealt)
        {
            if (disposed) return;
            logRows.Add(new[] { CreateLogRow(logPlayerColumn, player, playerDamageDealt, false),
                CreateLogRow(logEnemyColumn, enemy, enemyDamageDealt, true) });
            for (int i = 0; i < logRows.Count; i++)
                for (int side = 0; side < 2; side++)
                    logRows[i][side].anchoredPosition = new Vector2(310,
                        64 + (logRows.Count - 1 - i) * 112);
            logContent.sizeDelta = new Vector2(0, Mathf.Clamp(logRows.Count * 112 + 16, 600, 100000));
            logScroll.verticalNormalizedPosition = 0;
        }

        public void FatalAttack(bool playerAttacks) => SetViewAngle(playerAttacks ? -8 : 8);

        public LegacyCombatHud(Transform parent, LegacyDuelArt art, Action<int> queue, Action commit, Action restart,
            DuelPresentationSettings settings = null, Action queueBreath = null, Action cycleLanes = null)
        {
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.cycleLanes = cycleLanes;
            presentationSettings = settings;
            this.queueBreath = queueBreath;
            iconFor = HudIcon;
            root = Rect("Legacy Combat HUD", parent, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Legacy HUD EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(parent, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var white = Load("white");
            attackView = Rect("AttackView", root, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            attackView.anchorMin = Vector2.zero; attackView.anchorMax = Vector2.one;
            topBar = Rect("AttackView Up", attackView, new Vector2(0, 64), Vector2.zero, Vector2.one * .5f);
            topBar.anchorMin = new Vector2(0, 1); topBar.anchorMax = Vector2.one; topBar.pivot = new Vector2(.5f, 1);
            bottomBar = Rect("AttackView Down", attackView, new Vector2(0, -64), Vector2.zero, Vector2.one * .5f);
            bottomBar.anchorMin = Vector2.zero; bottomBar.anchorMax = new Vector2(1, 0); bottomBar.pivot = new Vector2(.5f, 0);
            BuildCinematicBox(topBar, true, white);
            BuildCinematicBox(bottomBar, false, white);

            var inputImage = Panel("Input", root, Vector2.zero, new Vector2(0, DockHeight));
            Dress(inputImage);
            inputPanel = inputImage.rectTransform;
            inputPanel.anchorMin = Vector2.zero; inputPanel.anchorMax = new Vector2(1, 0); inputPanel.pivot = new Vector2(.5f, 0);
            var controls = Rect("Keys", inputPanel, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            controls.anchorMin = Vector2.zero; controls.anchorMax = Vector2.one;
            inputs = controls.gameObject.AddComponent<CanvasGroup>();
            var logImage = Panel("LogButton", controls, new Vector2(80, 0), new Vector2(96, 112));
            Pin(logImage.rectTransform, new Vector2(0, .5f));
            BuildActionIcon(logImage.transform, "combat-log", "기록", "L");
            var logButton = AddButton(logImage);
            logButton.onClick.AddListener(() => { ToggleLog(); ClearSelection(logButton.gameObject); });
            for (int lane = 0; lane < 3; lane++)
            {
                float x = (lane - 1) * 156;
                var next = Panel("Next " + "QWE"[lane], controls, new Vector2(x + 30, 40), new Vector2(88, 104), RaisedSurface);
                nextIcons[lane] = Image("Next Skill Image", next.transform, null, NextIconHome, new Vector2(56, 56));
                nextIcons[lane].preserveAspect = true;
                nextIcons[lane].color = new Color(1, 1, 1, NextIconAlpha);
                var nextLabel = Text("Next Label", next.transform, new Vector2(0, 41), new Vector2(76, 18), 14, TextAnchor.MiddleLeft);
                nextLabel.text = "다음"; nextLabel.color = MutedText;
                var card = Panel("Current " + "QWE"[lane], controls, new Vector2(x, -4), new Vector2(112, 124), Card, Border);
                currentIcons[lane] = Image("Skill Image", card.transform, null, CurrentIconHome, new Vector2(56, 56));
                currentIcons[lane].preserveAspect = true;
                var key = Text("Key", card.transform, new Vector2(-32, 47), new Vector2(36, 22), 20, TextAnchor.MiddleLeft);
                key.text = "QWE"[lane].ToString(); key.color = Accent;
                var styleName = Text("Style Name", card.transform, new Vector2(17, 47), new Vector2(62, 18), 14, TextAnchor.MiddleRight);
                styleName.text = SkillLaneStyle.Name(lane); styleName.color = Foreground;
                skillNames[lane] = Text("Skill Name", card.transform, new Vector2(0, -28), new Vector2(108, 20), 14, TextAnchor.MiddleCenter);
                skillNames[lane].supportRichText = false;
                skillNames[lane].resizeTextForBestFit = true;
                skillNames[lane].resizeTextMinSize = 10;
                skillNames[lane].resizeTextMaxSize = 14;
                costs[lane] = Text("SkillCost", card.transform, new Vector2(0, -47), new Vector2(92, 22), 18, TextAnchor.MiddleRight);
                costs[lane].color = Foreground;
                holdImages[lane] = Image("KeyHoldImage", card.transform, white, new Vector2(0, -59), new Vector2(104, 3), Accent);
                Filled(holdImages[lane], UnityEngine.UI.Image.FillMethod.Horizontal, 0);
                holdImages[lane].fillAmount = 0;
                var button = AddButton(card);
                button.transition = Selectable.Transition.None;
                int selectedLane = lane;
                button.onClick.AddListener(() => { queue?.Invoke(selectedLane); ClearSelection(button.gameObject); });
                laneButtons[lane] = button;
                guideLaneGroups[lane] = card.gameObject.AddComponent<CanvasGroup>();
                guideLaneFocus[lane] = AddGuideOutline(card);
                laneFeedback[lane] = SkillCardFeedbackGraphic.Create(card.transform, "Lane Condition Feedback", 3f);
                laneCards[lane] = card.gameObject;
                nextPanels[lane] = next.gameObject;
            }
            var breathe = Panel("BreathButton", controls, new Vector2(360, -4), new Vector2(144, 124), RaisedSurface);
            var breathIcon = Image("Icon", breathe.transform, art.GetSkillIcon(LegacyCommonActions.Breathe.IconId),
                new Vector2(-44, 20), Vector2.one * 40);
            breathIcon.preserveAspect = true;
            Text("Label", breathe.transform, new Vector2(20, 30), new Vector2(92, 24), 18,
                TextAnchor.MiddleCenter).text = "숨고르기";
            var breathCost = Text("ACT Cost", breathe.transform, new Vector2(20, 7), new Vector2(92, 18), 14,
                TextAnchor.MiddleCenter);
            breathCost.text = "ACT 0"; breathCost.color = MutedText;
            breathCount = Text("Remaining", breathe.transform, new Vector2(0, -17), new Vector2(132, 18), 14,
                TextAnchor.MiddleCenter);
            breathCount.color = Foreground;
            var breathKey = Text("KeyHint", breathe.transform, new Vector2(0, -45), new Vector2(132, 18), 18,
                TextAnchor.MiddleCenter);
            breathKey.text = "S"; breathKey.color = Accent;
            var note = Text("Breath Note", controls, new Vector2(360, -81), new Vector2(200, 28), 12,
                TextAnchor.MiddleCenter);
            note.text = "한 박자 쉬어\n기술 순서 조절"; note.color = MutedText;
            breathNote = note.rectTransform;
            breathButton = AddButton(breathe);
            breathButton.onClick.AddListener(QueueBreathFromButton);
            // 넘기기 mirrors 숨고르기 on the other side of the lanes.
            var cycle = Panel("CycleButton", controls, new Vector2(-360, -4), new Vector2(144, 124), RaisedSurface);
            Text("Label", cycle.transform, new Vector2(0, 30), new Vector2(132, 24), 18, TextAnchor.MiddleCenter).text = "넘기기";
            cycleCost = Text("Time Cost", cycle.transform, new Vector2(0, 7), new Vector2(132, 18), 14, TextAnchor.MiddleCenter);
            cycleCost.text = CycleCostText(0f); cycleCost.color = MutedText;
            var cycleWhat = Text("Effect", cycle.transform, new Vector2(0, -17), new Vector2(132, 18), 14, TextAnchor.MiddleCenter);
            cycleWhat.text = "모든 열 한 칸"; cycleWhat.color = Foreground;
            var cycleKey = Text("KeyHint", cycle.transform, new Vector2(0, -45), new Vector2(132, 18), 18, TextAnchor.MiddleCenter);
            cycleKey.text = "Shift"; cycleKey.color = Accent;
            var cycleHint = Text("Cycle Note", controls, new Vector2(-360, -81), new Vector2(200, 28), 12, TextAnchor.MiddleCenter);
            cycleHint.text = "맨 앞 기술을 쓰지 않고\n뒤로 보냄"; cycleHint.color = MutedText;
            cycleNote = cycleHint.rectTransform;
            cycleButton = AddButton(cycle);
            cycleButton.onClick.AddListener(CycleFromButton);
            Image("Act_BG", controls, white, new Vector2(0, -90), new Vector2(520, 8), Track);
            actFill = Image("Act_Gauge", controls, white, new Vector2(0, -90), new Vector2(520, 8), Accent);
            Filled(actFill, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            for (int unit = 1; unit < 10; unit++)
                Image("ACT Divider " + unit, controls, white, new Vector2(-260 + unit * 52, -90), new Vector2(2, 8), Surface);
            actText = Text("Act_Value", controls, new Vector2(0, -75), new Vector2(140, 20), 18, TextAnchor.MiddleCenter);
            actText.color = MutedText;
            guideActFocus = GuideFocusFrame("Guide ACT Focus", controls, new Vector2(0f, -81f), new Vector2(540f, 39f));
            var start = Panel("AButton", controls, new Vector2(-80, 0), new Vector2(96, 112), RaisedSurface, Accent);
            Pin(start.rectTransform, new Vector2(1, .5f));
            BuildActionIcon(start.transform, "confirm-turn", "확정", "Space");
            commitButton = AddButton(start, true);
            guideCommitGroup = start.gameObject.AddComponent<CanvasGroup>();
            guideCommitFocus = AddGuideOutline(start);
            commitButton.onClick.AddListener(() => { commit?.Invoke(); ClearSelection(commitButton.gameObject); });

            timerPanel = Panel("TimerBG", root, new Vector2(0, -36), new Vector2(480, 48)).rectTransform;
            Pin(timerPanel, new Vector2(.5f, 1));
            turnText = Text("TurnCount", timerPanel, new Vector2(-184, 3), new Vector2(96, 24), 18, TextAnchor.MiddleCenter);
            turnText.color = MutedText;
            timerTrack = Image("Timer Track", timerPanel, white, new Vector2(48, -5), new Vector2(336, 5), Track);
            timerFill = Image("Timer", timerPanel, white, new Vector2(48, -5), new Vector2(336, 5), Accent);
            Filled(timerFill, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            untimedHint = Text("Untimed Hint", timerPanel, new Vector2(48, 3), new Vector2(336, 26), 18, TextAnchor.MiddleCenter);
            untimedHint.text = "임무 · 시간 제한 없음";
            untimedHint.color = Accent;
            // Just outside the timer panel's right edge, level with the bar, so it never covers the bar or the frame.
            timeSpent = Text("Time Spent", timerPanel, TimeSpentHome, new Vector2(90, 26), 20, TextAnchor.MiddleLeft);
            timeSpent.color = DuelVisualTheme.Danger;
            timeSpent.gameObject.SetActive(false);

            var stagePanel = Panel("Stage", root, new Vector2(224, -36), new Vector2(396, 48)).rectTransform;
            Pin(stagePanel, new Vector2(0, 1));
            stageText = Text("Stage Label", stagePanel, Vector2.zero, new Vector2(372, 32), 18, TextAnchor.MiddleLeft);
            stageText.color = MutedText;

            playerStatus = BuildStatus(false);
            enemyStatus = BuildStatus(true);
            playerQueue = new QueueView(Rect("Player Requests", root, Vector2.zero, Vector2.zero, Vector2.one * .5f), true, white, art.UIFont);
            enemyQueue = new QueueView(Rect("Enemy Requests", root, Vector2.zero, Vector2.zero, Vector2.one * .5f), false, white, art.UIFont);
            guideEnemyFocus = GuideFocusFrame("Guide Enemy Queue Focus", enemyQueue.Root, Vector2.zero, Vector2.zero);

            var playerExplanationImage = Panel("Skill Explain", root, Vector2.zero, new Vector2(460, 368), DuelVisualTheme.Paper);
            Dress(playerExplanationImage);
            playerExplanation = playerExplanationImage.rectTransform;
            playerExplanationIcon = Image("Explanation Skill Icon", playerExplanation, null, new Vector2(-183, 128), Vector2.one * 68);
            playerExplanationIcon.preserveAspect = true;
            playerName = Text("Name", playerExplanation, new Vector2(35, 148), new Vector2(326, 34), 26, TextAnchor.MiddleLeft);
            playerName.color = DuelVisualTheme.Ink;
            playerStyle = new SkillLaneBadge(playerExplanation, art.UIFont, "Power Cost Property", Vector2.zero);
            playerDetails = playerStyle.Label;
            playerInfo = new SkillInfoView(playerExplanation, art.UIFont, new Vector2(0, -18), 416, "Player");
            playerDescription = playerInfo.EffectText;
            playerDescription.name = "Effect";
            playerEffectFeedback = SkillCardFeedbackGraphic.Create(playerDescription.transform, "Current Effect Emphasis", 3f);
            playerExplanationHint = Text("Explanation Hint", playerExplanation, new Vector2(0, -157), new Vector2(416, 20), 14, TextAnchor.MiddleRight);
            playerExplanationHint.text = "키를 놓으면 닫기"; playerExplanationHint.color = DuelVisualTheme.Ink;
            var enemyExplanationImage = Panel("Enemy Skill Explain", root, new Vector2(-480, 160), new Vector2(460, 368), DuelVisualTheme.Paper);
            Dress(enemyExplanationImage);
            enemyExplanation = enemyExplanationImage.rectTransform;
            enemyExplanationIcon = Image("Explanation Skill Icon", enemyExplanation, null, new Vector2(-183, 128), Vector2.one * 68);
            enemyExplanationIcon.preserveAspect = true;
            enemyName = Text("Name", enemyExplanation, new Vector2(35, 148), new Vector2(326, 34), 26, TextAnchor.MiddleLeft);
            enemyName.color = DuelVisualTheme.Ink;
            enemyStyle = new SkillLaneBadge(enemyExplanation, art.UIFont, "Power Property", Vector2.zero);
            enemyDetails = enemyStyle.Label;
            enemyInfo = new SkillInfoView(enemyExplanation, art.UIFont, new Vector2(0, -18), 416, "Enemy");
            enemyDescription = enemyInfo.EffectText;
            enemyDescription.name = "Effect";
            enemyEffectFeedback = SkillCardFeedbackGraphic.Create(enemyDescription.transform, "Current Effect Emphasis", 3f);
            enemyExplanationHint = Text("Explanation Hint", enemyExplanation, new Vector2(0, -157), new Vector2(416, 20), 14, TextAnchor.MiddleRight);
            enemyExplanationHint.text = "상대 기술 · Tab / 적 확인"; enemyExplanationHint.color = DuelVisualTheme.Ink;

            BuildLogPanel(white);

            outcomePanel = Image("FadePanel", root, white, Vector2.zero, Vector2.zero,
                new Color(Track.r, Track.g, Track.b, .84f)).rectTransform;
            outcomePanel.anchorMin = Vector2.zero; outcomePanel.anchorMax = Vector2.one;
            outcomePanel.GetComponent<Image>().raycastTarget = true;
            var resultCard = Panel("Result Card", outcomePanel, Vector2.zero, new Vector2(600, 340));
            Dress(resultCard);
            outcomeText = Text("GameEnd", resultCard.transform, new Vector2(0, 84), new Vector2(560, 88), 60, TextAnchor.MiddleCenter);
            outcomeTurns = Text("UseTurnCount", resultCard.transform, new Vector2(0, 10), new Vector2(540, 44), 22, TextAnchor.MiddleCenter);
            outcomeTurns.color = MutedText;
            var retry = Panel("Retry", outcomePanel, new Vector2(0, -96), new Vector2(240, 60), RaisedSurface, Accent);
            var retryButton = AddButton(retry, true);
            retryButton.onClick.AddListener(() => restart?.Invoke());
            var retryLabel = Text("Retry Label", retry.transform, Vector2.zero, new Vector2(224, 48), 24, TextAnchor.MiddleCenter);
            retryLabel.text = "다시 도전";
            for (int i = 1; i <= LegacyDuelArt.SkillIconCount; i++)
                assetsAvailable &= art.GetSkillIcon(i) != null;
            Reset();
        }

        public void SetStage(int stageNumber, int stageCount, string stageName)
        {
            this.stageNumber = stageNumber;
            this.stageCount = stageCount;
            this.stageName = stageName;
            UpdateStageLabel();
        }

        /// <summary>Mission duels label the stage as a mission and keep breathing closed.</summary>
        /// <param name="timed">Whether the mission runs the planning timer; otherwise an untimed hint replaces it.</param>
        /// <param name="breath">Whether the mission has opened 숨고르기; closed missions hide its button.</param>
        public void SetMissionMode(bool enabled, bool timed = false, bool breath = false)
        {
            if (disposed) return;
            if (!enabled) SetGuide(null);
            if (missionModeInitialized && missionMode == enabled && missionTimed == timed && missionBreath == breath) return;
            missionModeInitialized = true;
            missionMode = enabled;
            missionTimed = timed;
            missionBreath = breath;
            ApplyInputAvailability();
            bool showTimer = !enabled || timed;
            timerTrack.gameObject.SetActive(showTimer);
            timerFill.gameObject.SetActive(showTimer);
            untimedHint.gameObject.SetActive(!showTimer);
            UpdateStageLabel();
            if (!enabled) SetGuideFocus(-1, false, false, false);
        }

        /// <summary>The mission coach whose current beat gates queueing and committing, or null for free play.</summary>
        public void SetGuide(MissionGuide missionGuide)
        {
            if (disposed) return;
            guide = missionGuide;
            ApplyInputAvailability();
        }

        /// <summary>숨고르기 is shown when the duel allows it (a mission must also have opened it) and the coach, if any,
        /// has reached its lesson or free play.</summary>
        private bool BreathShown => (!missionMode || missionBreath) &&
            (displayedSession == null || displayedSession.Features.Has(CombatFeature.Breath)) &&
            (guide == null || guide.AllowsBreath);

        /// <summary>넘기기 just turned the lanes marked in <paramref name="turned"/>: they play the gear-like slide from
        /// the next card into the front card. Call before the Refresh that shows the new front skills.</summary>
        public void PlayLaneTurn(bool[] turned)
        {
            if (disposed || turned == null) return;
            for (int lane = 0; lane < laneTurnElapsed.Length && lane < turned.Length; lane++)
                if (turned[lane]) laneTurnElapsed[lane] = 0f;
            AdvanceLaneTurns(0f);
        }

        /// <summary>Whether a lane's turn slide is still playing.</summary>
        public bool IsLaneTurning(int lane) => lane >= 0 && lane < laneTurnElapsed.Length && laneTurnElapsed[lane] < LaneTurnDuration;

        private void AdvanceLaneTurns(float realDelta)
        {
            for (int lane = 0; lane < laneTurnElapsed.Length; lane++)
            {
                laneTurnElapsed[lane] = Mathf.Min(LaneTurnDuration, laneTurnElapsed[lane] + Mathf.Max(0f, realDelta));
                bool settled = laneTurnElapsed[lane] >= LaneTurnDuration;
                float t = settled ? 1f : OutQuad(laneTurnElapsed[lane] / LaneTurnDuration);
                RectTransform current = currentIcons[lane].rectTransform, nextIcon = nextIcons[lane].rectTransform;
                Color next = nextIcons[lane].color;
                if (settled)
                {
                    // Land exactly on the resting layout.
                    current.anchoredPosition = CurrentIconHome;
                    current.localScale = Vector3.one;
                    nextIcon.anchoredPosition = NextIconHome;
                    next.a = NextIconAlpha;
                    laneCards[lane].transform.localScale = Vector3.one;
                }
                else
                {
                    current.anchoredPosition = Vector2.Lerp(CurrentIconHome + LaneTurnFrom, CurrentIconHome, t);
                    current.localScale = Vector3.one * Mathf.Lerp(.8f, 1f, t);
                    nextIcon.anchoredPosition = Vector2.Lerp(NextIconHome + NextTurnFrom, NextIconHome, t);
                    next.a = NextIconAlpha * t;
                    // A short tick: the card swells a little and settles as the new skill lands.
                    laneCards[lane].transform.localScale = Vector3.one * (1f + .06f * Mathf.Sin(Mathf.PI * t));
                }
                nextIcons[lane].color = next;
            }
        }

        /// <summary>넘기기 is shown when the duel allows it and the coach, if any, has reached its lesson or free play.</summary>
        private bool CycleShown => displayedSession != null && displayedSession.Features.Has(CombatFeature.Cycle) &&
            (guide == null || guide.AllowsCycle);

        private bool CanCycle()
            => !disposed && cycleLanes != null && lastPlanning && CycleShown && inspectedSlot < 0 &&
                displayedSession.Phase == LegacyDuelPhase.Planning && laneCycleAffordable;

        /// <summary>What 넘기기 costs now (planning seconds; 0 while the clock is held) and whether it can be paid.</summary>
        public void SetLaneCycleCost(float seconds, bool affordable)
        {
            if (disposed) return;
            seconds = Mathf.Max(0f, seconds);
            if (laneCycleCost != seconds) cycleCost.text = CycleCostText(seconds);
            laneCycleCost = seconds;
            laneCycleAffordable = affordable;
        }

        /// <summary>넘기기 just spent planning time: a short '-N초' rises off the timer.</summary>
        public void ShowTimeSpent(float seconds)
        {
            if (disposed || seconds <= 0f) return;
            timeSpent.text = "-" + seconds.ToString("0.#") + "초";
            timeSpentRemaining = TimeSpentDuration;
            AdvanceTimeSpent(0f);
        }

        /// <summary>Whether the '-N초' from the last 넘기기 is still on screen.</summary>
        public bool IsShowingTimeSpent => timeSpentRemaining > 0f;

        private static string CycleCostText(float seconds) => seconds > 0f ? seconds.ToString("0.#") + "초 소모" : "무료";

        private void AdvanceTimeSpent(float realDelta)
        {
            timeSpentRemaining = Mathf.Max(0f, timeSpentRemaining - Mathf.Max(0f, realDelta));
            bool visible = timeSpentRemaining > 0f;
            if (timeSpent.gameObject.activeSelf != visible) timeSpent.gameObject.SetActive(visible);
            if (!visible) return;
            float t = 1f - timeSpentRemaining / TimeSpentDuration;
            timeSpent.rectTransform.anchoredPosition = TimeSpentHome + new Vector2(0f, 18f * t);
            Color color = timeSpent.color;
            color.a = 1f - t * t;
            timeSpent.color = color;
        }

        private void CycleFromButton()
        {
            // Check the authoritative session again so a stale enabled button cannot act.
            if (!CanCycle()) return;
            cycleLanes.Invoke();
            ApplyInputAvailability();
            ClearSelection(cycleButton.gameObject);
        }

        private void ApplyInputAvailability()
        {
            bool cycleShown = CycleShown;
            cycleButton.gameObject.SetActive(cycleShown);
            cycleNote.gameObject.SetActive(cycleShown);
            cycleButton.interactable = CanCycle();
            bool breathShown = BreathShown;
            breathButton.gameObject.SetActive(breathShown);
            breathNote.gameObject.SetActive(breathShown);
            breathButton.interactable = CanQueueBreath();
            bool allowCommit = guide == null || guide.AllowsCommit;
            commitButton.interactable = lastPlanning && allowCommit;
            guideCommitGroup.alpha = allowCommit ? 1f : .4f;
            for (int lane = 0; lane < laneButtons.Length; lane++)
            {
                bool allowLane = guide == null || guide.AllowsQueue(lane);
                laneButtons[lane].interactable = lastPlanning && laneAffordable[lane] && allowLane;
                guideLaneGroups[lane].alpha = allowLane ? 1f : .4f;
            }
        }

        private bool CanQueueBreath()
            => !disposed && queueBreath != null && lastPlanning && BreathShown &&
                inspectedSlot < 0 && displayedSession != null && displayedSession.Phase == LegacyDuelPhase.Planning &&
                displayedSession.BreathsRemainingThisTurn > 0;

        private void QueueBreathFromButton()
        {
            // Check the authoritative session again so a fourth click in the
            // same frame cannot act on a stale enabled button.
            if (!CanQueueBreath()) return;
            queueBreath.Invoke();
            UpdateBreathCount();
            ApplyInputAvailability();
            ClearSelection(breathButton.gameObject);
        }

        private void UpdateBreathCount()
        {
            int remaining = displayedSession != null ? displayedSession.BreathsRemainingThisTurn
                : LegacyQueuedDuel.MaximumBreathsPerTurn;
            if (shownBreathsRemaining == remaining) return;
            shownBreathsRemaining = remaining;
            breathCount.text = $"남은 {remaining}/{LegacyQueuedDuel.MaximumBreathsPerTurn}";
        }

        public void SetGuideFocus(int lane, bool commit, bool enemy, bool act)
        {
            if (disposed) return;
            for (int index = 0; index < guideLaneFocus.Length; index++)
                guideLaneFocus[index].enabled = index == lane;
            guideCommitFocus.enabled = commit;
            highlightEnemyQueue = enemy;
            UpdateGuideEnemyFocus();
            guideActFocus.gameObject.SetActive(act);
            actText.color = act ? Accent : MutedText;
        }

        private void UpdateStageLabel()
        {
            stageText.text = missionMode ? $"임무 {stageNumber:00} / {stageCount:00}  ·  {stageName}"
                : $"{stageNumber:00} / {stageCount:00}  ·  {stageName}";
        }

        private void UpdateGuideEnemyFocus()
        {
            Vector2 size = enemyQueue.DisplaySize;
            guideEnemyFocus.gameObject.SetActive(highlightEnemyQueue && size != Vector2.zero);
            guideEnemyFocus.sizeDelta = size + Vector2.one * 16f;
            guideEnemyFocus.anchoredPosition = new Vector2(0f, -(Mathf.Max(64f, size.y) - 64f) * .5f);
        }

        public void Refresh(LegacyQueuedDuel session, float timeRemaining, bool isResolving, int currentSlot,
            Camera camera, Transform player, Transform enemy, float delta = 0, float realDelta = -1)
        {
            if (disposed || session == null) return;
            displayedSession = session;
            worldCamera = camera;
            delta = Mathf.Max(0, delta);
            float actualDelta = realDelta < 0 ? delta : Mathf.Max(0, realDelta);
            transition = Mathf.MoveTowards(transition, targetTransition, delta * 2);
            float ease = 1 - Mathf.Pow(1 - transition, 3);
            inputPanel.sizeDelta = new Vector2(0, DockHeight * (1 - OutQuad(transition)));
            timerPanel.anchoredPosition = new Vector2(0, Mathf.Lerp(-36, 64, ease));
            cinematicElapsed = Mathf.Min(.3f, cinematicElapsed + actualDelta);
            cinematic = Mathf.Lerp(cinematicFrom, cinematicTarget, OutQuad(cinematicElapsed / .3f));
            topBar.anchoredPosition = new Vector2(0, 64 * (1 - cinematic));
            bottomBar.anchoredPosition = new Vector2(0, -64 * (1 - cinematic));
            angleElapsed = Mathf.Min(.15f, angleElapsed + delta);
            viewAngle = Mathf.Lerp(angleFrom, angleTarget, OutQuad(angleElapsed / .15f));
            attackView.localRotation = Quaternion.Euler(0, 0, viewAngle);
            bool planning = session.Phase == LegacyDuelPhase.Planning && !isResolving;
            lastPlanning = planning;
            if (feedbackSlot != null && (!isResolving || !ReferenceEquals(feedbackSlot, session.CurrentSlot)))
                ClearSkillFeedback();
            if (!planning) ClearConditionPreview();
            canOpenLog = planning;
            inputs.alpha = planning ? 1 : 0;
            inputs.interactable = inputs.blocksRaycasts = planning;
            float ratio = Mathf.Clamp01(timeRemaining / 10);
            timerFill.fillAmount = ratio;
            timerFill.color = Color.Lerp(DuelVisualTheme.Danger, Accent, ratio);
            actFill.fillAmount = Mathf.Clamp01(session.Act / 10f);
            if (shownAct != session.Act)
            {
                shownAct = session.Act;
                actText.text = $"{shownAct} / 10 ACT";
            }
            if (shownTurn != session.RoundNumber)
            {
                shownTurn = session.RoundNumber;
                turnText.text = $"턴 {shownTurn}";
            }
            for (int lane = 0; lane < 3; lane++)
            {
                var sequence = session.GetLane(lane);
                laneAffordable[lane] = sequence.Count > 0;
                // A lane the duel does not have (e.g. W/E in the Q-only opening missions) is not drawn at all.
                bool present = sequence.Count > 0;
                if (laneCards[lane].activeSelf != present) laneCards[lane].SetActive(present);
                if (nextPanels[lane].activeSelf != present) nextPanels[lane].SetActive(present);
                if (!present) continue;
                var current = sequence[0];
                var next = sequence[sequence.Count > 1 ? 1 : 0];
                if (shownSkills[lane] != current.Id)
                {
                    shownSkills[lane] = current.Id;
                    currentIcons[lane].sprite = HudIcon(current.IconId);
                    costs[lane].text = $"{current.Cost} ACT";
                    skillNames[lane].text = current.Name;
                }
                if (shownNextSkills[lane] != next.Id)
                {
                    shownNextSkills[lane] = next.Id;
                    nextIcons[lane].sprite = HudIcon(next.IconId);
                }
                bool affordable = session.Act >= current.Cost;
                laneAffordable[lane] = affordable;
                currentIcons[lane].color = affordable ? Color.white
                    : new Color(MutedText.r, MutedText.g, MutedText.b, .62f);
            }
            AdvanceLaneTurns(actualDelta);
            AdvanceTimeSpent(actualDelta);
            UpdateBreathCount();
            ApplyInputAvailability();
            if (slotAnimation) slotElapsed += delta;
            float activeScale = ActiveIconScale();
            // Queues and status share a stable head attachment, but keep UI size.
            int queueColumns = Mathf.Clamp(Mathf.FloorToInt((root.rect.width / 2 - 48) / 72), 1, 5);
            // Mark each queued attack the opponent's counter will answer, forecast at planning.
            session.ForecastEnemyCounterSlots(counterForecast);
            playerQueue.SetCounterMarks(counterForecast);
            session.ForecastPlayerCounterSlots(counterForecast);
            enemyQueue.SetCounterMarks(counterForecast);
            playerStatus.SetCounter(session.PlayerCounter, session.PlayerCountersRemaining);
            enemyStatus.SetCounter(session.EnemyCounter, session.EnemyCountersRemaining);
            playerQueue.Refresh(session.PlayerQueue, iconFor, currentSlot, isResolving, -1, activeScale, queueColumns);
            enemyQueue.Refresh(session.EnemyQueue, iconFor, currentSlot, isResolving, inspectedSlot, activeScale, queueColumns);
            UpdateConditionPreview();
            UpdateEffectEmphasis();
            playerQueue.TickFeedback(actualDelta);
            enemyQueue.TickFeedback(actualDelta);
            foreach (var feedback in laneFeedback) feedback.Tick(actualDelta);
            playerEffectFeedback.Tick(actualDelta);
            enemyEffectFeedback.Tick(actualDelta);
            UpdateGuideEnemyFocus();
            PositionFighterHud(playerStatus, playerQueue, player, camera, new Vector3(-.6f, 1.1f, 0));
            PositionFighterHud(enemyStatus, enemyQueue, enemy, camera, new Vector3(-.4f, 1f, 0));
            playerStatus.Refresh(session.Player, delta);
            enemyStatus.Refresh(session.Enemy, delta);
            UpdateDamage(actualDelta);
            UpdateLog(delta);
        }

        public void BeginTurn()
        {
            targetTransition = transition = 0;
            cinematic = cinematicFrom = cinematicTarget = 0;
            cinematicElapsed = .3f;
            viewAngle = angleFrom = angleTarget = 0;
            angleElapsed = .15f;
            slotAnimation = false;
            inspectedSlot = -1;
            ClearSkillFeedback();
            HideExplanation();
        }

        public void EndTurn()
        {
            targetTransition = 1;
            inputs.alpha = 0;
            inputs.interactable = inputs.blocksRaycasts = false;
            lastPlanning = false;
            breathButton.interactable = false;
            canOpenLog = false;
            CloseLog();
            HideExplanation();
            SetHoldProgress(-1, 0);
        }

        public void SetCurrentSkills(LegacySkill player, LegacySkill enemy, float duration = .5f)
        {
            ClearSkillFeedback();
            // The source enlarges the queue icon; it does not add an action-name panel.
            slotDuration = Mathf.Max(.4f, duration);
            slotElapsed = 0;
            slotAnimation = player != null || enemy != null;
            SetViewAngle(0);
        }

        /// <summary>Retimes the current slot's queue pulse without restarting it, e.g. after a dodge withdraws a counter.</summary>
        public void SetCurrentSlotDuration(float duration) => slotDuration = Mathf.Max(.4f, duration);

        /// <summary>Uses the immutable combat snapshot; presentation never evaluates or consumes a buff.</summary>
        public void SetSkillFeedback(LegacyCurrentSlot slot)
        {
            if (disposed) return;
            ClearSkillFeedback();
            if (slot == null) return;
            feedbackSlot = slot;
            playerQueue.SetCurrentFeedback(slot.SlotIndex, slot.PlayerFeedback);
            enemyQueue.SetCurrentFeedback(slot.SlotIndex, slot.EnemyFeedback);
            playerStatus.SetBuffFeedback(slot.PlayerFeedback);
            enemyStatus.SetBuffFeedback(slot.EnemyFeedback);
            UpdateEffectEmphasis();
        }

        public void ClearSkillFeedback()
        {
            if (disposed) return;
            feedbackSlot = null;
            playerQueue.SetCurrentFeedback(-1, null);
            enemyQueue.SetCurrentFeedback(-1, null);
            playerStatus.SetBuffFeedback(null);
            enemyStatus.SetBuffFeedback(null);
            ClearConditionPreview();
            playerEffectFeedback.Clear();
            enemyEffectFeedback.Clear();
        }

        public void SetInspectedSlot(int slot)
        {
            inspectedSlot = slot;
            ApplyInputAvailability();
        }

        public void SetHoldProgress(int lane, float progress)
        {
            if (lane >= 0 && lane < holdImages.Length) holdImages[lane].fillAmount = Mathf.Clamp01(progress);
            else for (int i = 0; i < holdImages.Length; i++) holdImages[i].fillAmount = 0;
        }

        public void ShowExplanation(LegacySkill skill, bool enemy)
        {
            if (disposed || skill == null) return;
            var panel = enemy ? enemyExplanation : playerExplanation;
            (enemy ? playerExplanation : enemyExplanation).gameObject.SetActive(false);
            // The common idle action has no weapon type or Q/W/E style.
            // Its compact planning control already explains its entire rule.
            if (skill.Kind == LegacySkillKind.Wait)
            {
                panel.gameObject.SetActive(false);
                ClearConditionPreview();
                return;
            }
            panel.gameObject.SetActive(true);
            bool changed = !ReferenceEquals(enemy ? explainedEnemy : explainedPlayer, skill);
            if (changed && enemy)
            {
                explainedEnemy = skill;
                enemyName.text = skill.Name;
                enemyStyle.SetLane(skill.LaneIndex, false);
                enemyExplanationIcon.sprite = iconFor(skill.IconId);
                enemyInfo.SetSkill(skill, true);
                LayoutExplanation(enemyExplanation, enemyExplanationIcon, enemyName, enemyStyle, enemyInfo, enemyExplanationHint);
            }
            else if (changed)
            {
                explainedPlayer = skill;
                playerName.text = skill.Name;
                playerStyle.SetLane(skill.LaneIndex);
                playerExplanationIcon.sprite = iconFor(skill.IconId);
                playerInfo.SetSkill(skill);
                LayoutExplanation(playerExplanation, playerExplanationIcon, playerName, playerStyle, playerInfo, playerExplanationHint);
            }
            // Clamp after content sizing, including repeated holds at a moving screen edge.
            Vector2 desired = root.rect.size / 2 + new Vector2(-480, 160);
            if (!enemy)
            {
                int lane = Mathf.Clamp(skill.LaneIndex, 0, 2);
                // The icon's resting place, so the panel stays put while a 넘기기 turn slides the icon in.
                Transform card = laneCards[lane].transform;
                Vector3 iconHome = card.parent.TransformPoint(card.localPosition + (Vector3)CurrentIconHome);
                Vector2 selected = ScreenPosition(RectTransformUtility.WorldToScreenPoint(null, iconHome));
                desired = selected + new Vector2(0, panel.sizeDelta.y / 2f + 96f);
            }
            panel.anchoredPosition = ClampCenter(desired,
                panel.sizeDelta + Vector2.right * (SkillInfoView.AttachmentOverhang * 2f)) - root.rect.size / 2;
            conditionPreview = !enemy && lastPlanning ? skill : null;
            UpdateConditionPreview();
            UpdateEffectEmphasis();
        }

        private void UpdateConditionPreview()
        {
            LegacySkill skill = lastPlanning ? conditionPreview : null;
            int nextSlot = displayedSession != null ? displayedSession.PlayerQueue.Count : -1;
            bool opponentCondition = LegacySkillConditions.HasOpponentCondition(skill);
            enemyQueue.SetConditionPreview(opponentCondition ? skill : null, nextSlot);
            // Past the enemy's queue, an attack may meet the enemy's counter instead of an empty slot.
            LegacySkill opponent = displayedSession == null || !opponentCondition ? null
                : nextSlot < displayedSession.EnemyQueue.Count ? displayedSession.EnemyQueue[nextSlot]
                : displayedSession.EnemyCounterFacing(skill);
            bool ready = displayedSession != null && (opponentCondition
                ? opponent != null && LegacySkillConditions.MatchesOpponent(skill, opponent)
                : LegacySkillConditions.MatchesSelfCondition(skill, displayedSession.Player));
            for (int lane = 0; lane < laneFeedback.Length; lane++)
                laneFeedback[lane].SetState(false, ready && skill != null && skill.LaneIndex == lane, false);
            playerExplanationHint.text = ready ? opponentCondition
                ? "지금 예약하면 조건 충족 · 놓으면 닫기" : "현재 저항: 회복 가능 · 놓으면 닫기"
                : opponentCondition ? "테두리 친 상대 기술에 맞춰 예약 · 놓으면 닫기" : "키를 놓으면 닫기";
            if (lastPlanning) playerEffectFeedback.SetState(false, ready, false);
        }

        private void ClearConditionPreview()
        {
            conditionPreview = null;
            enemyQueue.SetConditionPreview(null, -1);
            foreach (var feedback in laneFeedback) feedback.Clear();
            playerExplanationHint.text = "키를 놓으면 닫기";
            if (feedbackSlot == null) playerEffectFeedback.Clear();
        }

        private void UpdateEffectEmphasis()
        {
            if (feedbackSlot == null)
            {
                enemyEffectFeedback.Clear();
                return;
            }
            playerEffectFeedback.SetState(false, false, playerExplanation.gameObject.activeSelf &&
                ReferenceEquals(explainedPlayer, feedbackSlot.PlayerSkill) && feedbackSlot.PlayerFeedback.EffectActivated);
            enemyEffectFeedback.SetState(false, false, enemyExplanation.gameObject.activeSelf &&
                ReferenceEquals(explainedEnemy, feedbackSlot.EnemySkill) && feedbackSlot.EnemyFeedback.EffectActivated);
        }

        private static void LayoutExplanation(RectTransform panel, Image icon, Text name, SkillLaneBadge style, SkillInfoView info, Text hint)
        {
            float height = 90f + info.Height + 36f;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, height);
            float top = height / 2f;
            icon.rectTransform.anchoredPosition = new Vector2(-146f, top - 50f);
            icon.rectTransform.sizeDelta = Vector2.one * 64f;
            name.rectTransform.anchoredPosition = new Vector2(20f, top - 36f);
            name.rectTransform.sizeDelta = new Vector2(244f, 32f);
            style.Root.anchoredPosition = new Vector2(-30f, top - 65f);
            info.PlaceTop(top - 90f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -top + 16f);
        }

        public void HideExplanation()
        {
            playerExplanation.gameObject.SetActive(false);
            enemyExplanation.gameObject.SetActive(false);
            bool wasInspecting = inspectedSlot >= 0;
            inspectedSlot = -1;
            // The controller hides the enemy explanation after this frame's Refresh, so release the buttons that
            // inspection held (넘기기, 숨고르기) now rather than a frame late.
            if (wasInspecting && !disposed) ApplyInputAvailability();
            ClearConditionPreview();
            playerEffectFeedback.Clear();
            enemyEffectFeedback.Clear();
        }

        public void ShowOutcome(DuelMatchOutcome outcome, int turn)
        {
            if (outcome == DuelMatchOutcome.InProgress || disposed) return;
            HideExplanation();
            CloseLog(true);
            outcomePanel.gameObject.SetActive(true);
            outcomeText.text = outcome == DuelMatchOutcome.PlayerVictory ? "Victory" : "Defeat";
            outcomeText.color = outcome == DuelMatchOutcome.PlayerVictory ? Accent : DuelVisualTheme.Danger;
            outcomeTurns.text = $"사용 턴 : {turn}";
        }

        /// <param name="resistance">The number is resistance loss, not HP damage: it keeps the steel
        /// resistance colours even when emphasised, so the two kinds never look alike.</param>
        public void ShowHitDamage(bool targetPlayer, int damage, Vector3 worldPosition, bool critical = false, bool resistance = false)
        {
            if (disposed || worldCamera == null) return;
            DamageView view = AcquireDamageView();
            damage = Mathf.Max(0, damage);
            critical &= damage > 0;
            int sequence = targetPlayer ? playerDamageSequence : enemyDamageSequence;
            if (targetPlayer) playerDamageSequence = (sequence + 1) % 3;
            else enemyDamageSequence = (sequence + 1) % 3;
            view.Direction = targetPlayer ? -1f : 1f;
            view.StartOffset = new Vector2(view.Direction * (140f + sequence * 150f), 70f - sequence * 28f);
            view.WorldPosition = worldPosition;
            view.WorldOffset = Vector3.zero;
            view.Follow = null;
            view.Text.rectTransform.anchorMin = view.Text.rectTransform.anchorMax = Vector2.zero;
            view.Text.text = damage.ToString();
            view.Text.fontSize = presentationSettings != null ? presentationSettings.DamageTextFontSize : 168;
            view.Duration = view.Remaining = critical ? 1.25f : 1.05f;
            view.Scale = (.8f + Mathf.Clamp01(damage / 24f) * .25f) *
                (critical ? (presentationSettings != null ? presentationSettings.CriticalDamageScale : 1.3f) : 1f);
            view.Color = resistance ? ResistanceDamageInk : critical ? new Color(1f, .87f, .42f) : new Color(1f, .98f, .89f);
            view.Outline.effectColor = resistance ? ResistanceDamageOutline
                : critical ? new Color(.78f, .1f, .04f) : new Color(.64f, .055f, .055f);
            view.Text.gameObject.SetActive(true);
            view.Text.transform.SetAsLastSibling();
            ApplyDamageFrame(view);
        }

        /// <summary>Calls out that a fighter's resistance just broke: incoming HP damage is doubled.</summary>
        public void ShowBreakCallout(bool playerSide, Vector3 worldPosition, Transform follow = null) =>
            ShowStateCallout(playerSide, worldPosition, follow, BreakCalloutText, BreakCalloutInk, BreakCalloutOutline, 1.05f);

        /// <summary>Calls out the automatic refill of a fighter's resistance at the start of a turn.</summary>
        public void ShowRecoveryCallout(bool playerSide, Vector3 worldPosition, Transform follow = null) =>
            ShowStateCallout(playerSide, worldPosition, follow, RecoveryCalloutText, ResistanceDamageInk, ResistanceDamageOutline, .9f);

        /// <param name="follow">The fighter the callout belongs to; it rides along with its knockback.</param>
        private void ShowStateCallout(bool playerSide, Vector3 worldPosition, Transform follow, string text, Color ink,
            Color outline, float duration)
        {
            if (disposed || worldCamera == null) return;
            // Its own small pool, never counted as damage numbers or counter callouts.
            DamageView view = AcquireDamageView(stateTexts, "State Callout", MaximumStateTexts);
            view.Direction = playerSide ? -1f : 1f;
            // At the fighter's waist: damage numbers rise from its upper body, counter callouts
            // take the inner flank and the status stack the space above the head.
            view.StartOffset = Vector2.zero;
            view.WorldPosition = worldPosition;
            view.WorldOffset = StateCalloutWorldOffset;
            view.Follow = follow;
            view.Text.rectTransform.anchorMin = view.Text.rectTransform.anchorMax = Vector2.zero;
            view.Text.text = text;
            view.Text.fontSize = 80;
            view.Duration = view.Remaining = duration;
            view.Scale = .8f;
            view.Color = ink;
            view.Outline.effectColor = outline;
            view.Text.gameObject.SetActive(true);
            view.Text.transform.SetAsLastSibling();
            ApplyDamageFrame(view);
        }

        /// <summary>Calls out a counter over the fighter who answers, rising like a damage number.</summary>
        public void ShowCounterCallout(bool playerSide, Vector3 worldPosition)
        {
            if (disposed || worldCamera == null) return;
            // A separate small pool: callouts are never counted as damage numbers.
            DamageView view = AcquireDamageView(calloutTexts, "Counter Callout", MaximumCalloutTexts);
            view.Direction = playerSide ? -1f : 1f;
            // On the side facing the opponent, below the head status/queue stack and away from
            // the outward damage numbers, so it covers neither gauges nor cards.
            view.StartOffset = new Vector2(-view.Direction * 170f, 60f);
            view.WorldPosition = worldPosition;
            view.WorldOffset = Vector3.zero;
            view.Follow = null;
            view.Text.rectTransform.anchorMin = view.Text.rectTransform.anchorMax = Vector2.zero;
            view.Text.text = "반격";
            view.Text.fontSize = 96;
            view.Duration = view.Remaining = .9f;
            view.Scale = .8f;
            view.Color = Accent;
            view.Outline.effectColor = new Color(.24f, .1f, .03f);
            view.Text.gameObject.SetActive(true);
            view.Text.transform.SetAsLastSibling();
            ApplyDamageFrame(view);
        }

        private DamageView AcquireDamageView() => AcquireDamageView(damageTexts, "Damage", MaximumDamageTexts);

        private DamageView AcquireDamageView(List<DamageView> pool, string name, int maximum)
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].Remaining <= 0) return pool[i];
            if (pool.Count < maximum)
            {
                var text = Text(name, root, Vector2.zero, new Vector2(640, 280), 168, TextAnchor.MiddleCenter);
                text.fontStyle = FontStyle.Bold;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.supportRichText = false;
                var shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.08f, 0.025f, 0.02f, 0.85f);
                shadow.effectDistance = new Vector2(4f, -5f);
                shadow.useGraphicAlpha = true;
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(3f, -3f);
                outline.useGraphicAlpha = true;
                var created = new DamageView(text, outline);
                pool.Add(created);
                return created;
            }
            // Keep the existing pool bounded even under unusually fast bursts.
            DamageView oldest = pool[0];
            for (int i = 1; i < pool.Count; i++)
                if (pool[i].Remaining < oldest.Remaining) oldest = pool[i];
            return oldest;
        }

        public void Reset()
        {
            if (disposed) return;
            SetMissionMode(false);
            SetGuideFocus(-1, false, false, false);
            shownTurn = shownAct = -1;
            displayedSession = null;
            lastPlanning = false;
            shownBreathsRemaining = -1;
            UpdateBreathCount();
            ApplyInputAvailability();
            for (int i = 0; i < 3; i++) shownSkills[i] = shownNextSkills[i] = -1;
            for (int i = 0; i < laneTurnElapsed.Length; i++) laneTurnElapsed[i] = LaneTurnDuration;
            AdvanceLaneTurns(0f);
            timeSpentRemaining = 0f;
            AdvanceTimeSpent(0f);
            playerStatus.Reset(); enemyStatus.Reset();
            playerQueue.Clear(); enemyQueue.Clear();
            SetHoldProgress(-1, 0);
            outcomePanel.gameObject.SetActive(false);
            foreach (var damage in damageTexts) { damage.Remaining = 0; damage.Text.gameObject.SetActive(false); }
            foreach (var callout in calloutTexts) { callout.Remaining = 0; callout.Text.gameObject.SetActive(false); }
            foreach (var callout in stateTexts) { callout.Remaining = 0; callout.Text.gameObject.SetActive(false); }
            playerDamageSequence = enemyDamageSequence = 0;
            CloseLog(true);
            foreach (var row in logRows)
                foreach (var cell in row) { cell.gameObject.SetActive(false); Destroy(cell.gameObject); }
            logRows.Clear();
            logContent.sizeDelta = new Vector2(0, 600);
            BeginTurn();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Destroy(root.gameObject);
            foreach (var icon in hudIcons.Values)
                if (Application.isPlaying) UnityEngine.Object.Destroy(icon);
                else UnityEngine.Object.DestroyImmediate(icon);
            hudIcons.Clear();
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
        }

        private float ActiveIconScale()
        {
            if (!slotAnimation) return 1;
            if (slotElapsed < .2f) return Mathf.Lerp(1, 1.5f, OutQuint(slotElapsed / .2f));
            if (slotElapsed < slotDuration - .2f) return 1.5f;
            // Pursuit can hold an unfinished multi-hit slot past this visual
            // timer, and the next highlight is set before its animation starts.
            // Only queue consumption removes a card; its pulse must stay visible.
            return Mathf.Lerp(1.5f, 1, OutQuint(Mathf.Clamp01((slotElapsed - slotDuration + .2f) / .2f)));
        }

        private Vector2 ScreenPosition(Vector3 screen) => new Vector2(screen.x, screen.y) / Mathf.Max(.001f, canvas.scaleFactor);

        private void UpdateDamage(float delta)
        {
            delta = delta > 0f && !float.IsInfinity(delta) ? delta : 0f;
            TickDamageViews(damageTexts, delta);
            TickDamageViews(calloutTexts, delta);
            TickDamageViews(stateTexts, delta);
        }

        private void TickDamageViews(List<DamageView> pool, float delta)
        {
            foreach (var damage in pool)
            {
                if (damage.Remaining <= 0) continue;
                damage.Remaining = Mathf.Max(0, damage.Remaining - delta);
                ApplyDamageFrame(damage);
                if (damage.Remaining <= 0) damage.Text.gameObject.SetActive(false);
            }
        }

        private void ApplyDamageFrame(DamageView damage)
        {
            float age = damage.Duration - damage.Remaining;
            float progress = Mathf.Clamp01(age / damage.Duration);
            // A quick pop, short settle, then a readable hold. Never shrink the
            // number to zero during its lifetime; only fade its final 30 percent.
            float pop = age < .1f ? Mathf.Lerp(.78f, 1.2f, OutQuad(age / .1f))
                : Mathf.Lerp(1.2f, 1f, OutQuad(Mathf.Clamp01((age - .1f) / .12f)));
            damage.Text.transform.localScale = Vector3.one * damage.Scale * pop;
            damage.Text.transform.localRotation = Quaternion.identity;
            Color color = damage.Color;
            color.a = Mathf.Clamp01(damage.Remaining / (damage.Duration * .3f));
            damage.Text.color = color;
            // Reproject the original impact, while the rise/stagger stays in UI
            // units so zoom, hit stop and slow motion cannot distort the number.
            if (worldCamera != null)
            {
                Vector2 position = ScreenPosition(worldCamera.WorldToScreenPoint(
                    (damage.Follow != null ? damage.Follow.position : damage.WorldPosition) + damage.WorldOffset));
                position += damage.StartOffset + new Vector2(damage.Direction * 26f * progress, 84f * OutQuad(progress));
                damage.Text.rectTransform.anchoredPosition = position;
            }
        }

        private void PositionFighterHud(StatusView status, QueueView queue, Transform actor, Camera camera, Vector3 headAnchor)
        {
            if (actor == null || camera == null) return;
            // Stable local attachment points match the inherited idle sprites.
            // Project once, after arena/camera movement; never lag behind the actor.
            var screen = camera.WorldToScreenPoint(actor.TransformPoint(headAnchor));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var position)) return;
            var halfCanvas = root.rect.size / 2;
            var statusSize = status.Root.sizeDelta;
            var queueSize = queue.DisplaySize;
            float extraHeight = queueSize.y > 0 ? 12 + queueSize.y + 8 : 0;
            var groupSize = new Vector2(Mathf.Max(statusSize.x, queueSize.x + 16), statusSize.y + extraHeight);
            // Clamp the entire stack, not each element independently. Queue rows
            // sit above the status panel even while Tab focuses an off-centre actor.
            position += halfCanvas + Vector2.up * (statusSize.y / 2 + 12 + extraHeight / 2);
            position = ClampCenter(position, groupSize) - halfCanvas - Vector2.up * (extraHeight / 2);
            status.Root.anchoredPosition = position;
            queue.Root.anchoredPosition = position + Vector2.up * (statusSize.y / 2 + 12 + Mathf.Max(64, queueSize.y) - 32);
        }

        private StatusView BuildStatus(bool enemy)
        {
            var status = Panel(enemy ? "EnemyStatus" : "PlayerStatus", root,
                Vector2.zero, new Vector2(236, 92)).rectTransform;
            var white = Load("white");
            var result = new StatusView(status);
            var healthLabel = Text("HP Label", status, new Vector2(0, 27), new Vector2(196, 18), 14, TextAnchor.MiddleLeft);
            healthLabel.text = "HP"; healthLabel.color = MutedText;
            var resistanceLabel = Text("Resistance Label", status, new Vector2(0, -4), new Vector2(196, 18), 14, TextAnchor.MiddleLeft);
            resistanceLabel.text = "저항"; resistanceLabel.color = MutedText;
            Image("HP Track", status, null, new Vector2(0, 10), new Vector2(196, 10), Track);
            result.HealthLag = Image("HP delayed", status, white, new Vector2(0, 10), new Vector2(196, 10), DuelVisualTheme.Danger);
            result.Health = Image("HP", status, white, new Vector2(0, 10), new Vector2(196, 10), DuelVisualTheme.Health);
            Image("Resistance Track", status, null, new Vector2(0, -19), new Vector2(196, 6), Track);
            result.ResistanceLag = Image("Resistance delayed", status, white, new Vector2(0, -19), new Vector2(196, 6), DuelVisualTheme.Danger);
            result.Resistance = Image("Resistance", status, white, new Vector2(0, -19), new Vector2(196, 6), DuelVisualTheme.Steel);
            foreach (var gauge in new[] { result.Health, result.HealthLag, result.Resistance, result.ResistanceLag })
                Filled(gauge, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            // Below the gauges: this fighter's counter and its remaining uses this turn.
            result.Counter = Text("Counter", status, new Vector2(0, -35), new Vector2(212, 18), 14, TextAnchor.MiddleCenter);
            result.Counter.color = Accent;
            result.Counter.supportRichText = false;
            // Keep gauges at their original height above the actor as buff rows extend the panel upward.
            foreach (RectTransform child in status)
            {
                if (child.name == "Surface") continue;
                child.anchorMin = child.anchorMax = new Vector2(.5f, 0f);
                child.anchoredPosition += Vector2.up * 46f;
            }
            Color powerInk = new Color32(231, 207, 246, 255);
            Color protectionInk = new Color32(140, 238, 233, 255);
            result.ActivePowerBuff = BuildBuffLabel("Active Power Buff", status, powerInk);
            result.ActiveProtectionBuff = BuildBuffLabel("Active Protection Buff", status, protectionInk);
            result.GrantedPowerBuff = BuildBuffLabel("Granted Power Buff", status, powerInk);
            result.GrantedProtectionBuff = BuildBuffLabel("Granted Protection Buff", status, protectionInk);
            return result;
        }

        private Text BuildBuffLabel(string name, RectTransform parent, Color ink)
        {
            Color background = ink;
            background.a = .16f;
            var row = Image(name, parent, null, Vector2.zero, new Vector2(212, 24), background).rectTransform;
            row.anchorMin = row.anchorMax = new Vector2(.5f, 0f);
            var label = Text("Value", row, Vector2.zero, new Vector2(200, 24), 16, TextAnchor.MiddleLeft);
            label.color = ink;
            row.gameObject.SetActive(false);
            return label;
        }

        private static void BuildCinematicBox(RectTransform parent, bool top, Sprite white)
        {
            var box = Image("box", parent, white, new Vector2(0, top ? -64 : 64), new Vector2(0, 700), Surface).rectTransform;
            box.anchorMin = new Vector2(0, top ? 0 : 1);
            box.anchorMax = new Vector2(1, top ? 0 : 1);
            box.pivot = new Vector2(.5f, top ? 0 : 1);
        }

        private void SetCinematic(float target)
        {
            if (cinematicTarget == target) return;
            cinematicFrom = cinematic;
            cinematicTarget = target;
            cinematicElapsed = 0;
        }

        private void SetViewAngle(float target)
        {
            if (angleTarget == target) return;
            angleFrom = viewAngle;
            angleTarget = target;
            angleElapsed = 0;
        }

        private void BuildLogPanel(Sprite white)
        {
            logPanel = Image("Log View", root, white, new Vector2(0, 1080), Vector2.zero,
                new Color(Track.r, Track.g, Track.b, .84f)).rectTransform;
            logPanel.anchorMin = Vector2.zero; logPanel.anchorMax = Vector2.one;
            logPanel.GetComponent<Image>().raycastTarget = true;
            var logCard = Panel("Log Card", logPanel, Vector2.zero, new Vector2(1320, 760));
            Dress(logCard);
            Text("Title", logPanel, new Vector2(-430, 326), new Vector2(360, 44), 28, TextAnchor.MiddleLeft).text = "전투 기록";
            var scrollImage = Image("Log Scroll View", logPanel, white, new Vector2(0, -18), new Vector2(1240, 600), Color.clear);
            scrollImage.raycastTarget = true;
            var scrollRoot = scrollImage.rectTransform;
            var viewport = Rect("Viewport", scrollRoot, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.gameObject.AddComponent<RectMask2D>();
            logContent = Rect("Content", viewport, Vector2.zero, new Vector2(0, 600), new Vector2(.5f, 0));
            logContent.anchorMin = Vector2.zero; logContent.anchorMax = new Vector2(1, 0);
            logContent.pivot = new Vector2(.5f, 0);
            logPlayerColumn = Rect("Player", logContent, Vector2.zero, new Vector2(620, 0), Vector2.zero);
            logPlayerColumn.anchorMin = Vector2.zero; logPlayerColumn.anchorMax = new Vector2(0, 1); logPlayerColumn.pivot = new Vector2(0, .5f);
            logEnemyColumn = Rect("Enemy", logContent, Vector2.zero, new Vector2(620, 0), Vector2.one);
            logEnemyColumn.anchorMin = new Vector2(1, 0); logEnemyColumn.anchorMax = Vector2.one; logEnemyColumn.pivot = new Vector2(1, .5f);
            logScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            logScroll.content = logContent; logScroll.viewport = viewport;
            logScroll.horizontal = false; logScroll.vertical = true;
            logScroll.movementType = ScrollRect.MovementType.Elastic;
            logScroll.elasticity = .1f; logScroll.inertia = true;
            logScroll.decelerationRate = .135f; logScroll.scrollSensitivity = 16;
            var track = Image("Vertical Scrollbar", scrollRoot, white, Vector2.zero, new Vector2(8, 0), Track);
            track.raycastTarget = true;
            track.rectTransform.anchorMin = new Vector2(1, 0); track.rectTransform.anchorMax = Vector2.one;
            track.rectTransform.pivot = new Vector2(1, .5f);
            var handle = Image("Handle", track.transform, white, Vector2.zero, Vector2.zero, Border);
            handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = Vector2.one;
            handle.raycastTarget = true;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            logScroll.verticalScrollbar = scrollbar;
            logScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            var closeImage = Panel("Close Log", logPanel, new Vector2(534, 326), new Vector2(140, 44), RaisedSurface);
            var closeButton = AddButton(closeImage);
            closeButton.onClick.AddListener(() => { CloseLog(); ClearSelection(closeButton.gameObject); });
            Text("Close Label", closeImage.transform, Vector2.zero, new Vector2(128, 36), 20, TextAnchor.MiddleCenter).text = "닫기";
            logPanel.gameObject.SetActive(false);
        }

        private RectTransform CreateLogRow(RectTransform parent, LegacySkill skill, int damage, bool enemy)
        {
            var row = Panel(enemy ? "Log Enemy Skill" : "Log Player Skill", parent, Vector2.zero,
                new Vector2(580, 96), RaisedSurface).rectTransform;
            Pin(row, Vector2.zero);
            var icon = Image("Icon", row, skill != null ? HudIcon(skill.IconId) : null,
                new Vector2(enemy ? 246 : -246, 0), new Vector2(60, 60));
            icon.preserveAspect = true; icon.enabled = skill != null;
            var name = Text("Skill", row, Vector2.zero, new Vector2(280, 76), 22, TextAnchor.MiddleCenter);
            name.text = skill?.Name ?? string.Empty;
            var amount = Text("Damage", row, new Vector2(enemy ? -210 : 210, 0), new Vector2(92, 76), 36, TextAnchor.MiddleCenter);
            amount.text = Mathf.Max(0, damage).ToString();
            amount.color = Accent;
            return row;
        }

        public void ToggleLog()
        {
            if (LogOpen) CloseLog();
            else OpenLog();
        }

        public void OpenLog()
        {
            if (disposed || !canOpenLog || LogOpen) return;
            LogOpen = true;
            HideExplanation();
            logPanel.gameObject.SetActive(true);
            logFrom = logPanel.anchoredPosition.y;
            logTarget = 0;
            logElapsed = 0;
            logScroll.verticalNormalizedPosition = 0;
        }

        public void CloseLog(bool immediate = false)
        {
            if (disposed || logPanel == null) return;
            LogOpen = false;
            logFrom = logPanel.anchoredPosition.y;
            logTarget = Mathf.Max(1080, root.rect.height) + 32;
            logElapsed = immediate ? .5f : 0;
            if (immediate)
            {
                logPanel.anchoredPosition = new Vector2(0, logTarget);
                logPanel.gameObject.SetActive(false);
            }
        }

        private void UpdateLog(float delta)
        {
            if (!logPanel.gameObject.activeSelf) return;
            logElapsed = Mathf.Min(.5f, logElapsed + delta);
            logPanel.anchoredPosition = new Vector2(0, Mathf.Lerp(logFrom, logTarget, OutQuad(logElapsed / .5f)));
            if (!LogOpen && logElapsed >= .5f) logPanel.gameObject.SetActive(false);
        }

        private Sprite HudIcon(int id)
        {
            if (hudIcons.TryGetValue(id, out var cached)) return cached;
            var source = art.GetSkillIcon(id);
            if (source == null) return null;
            // Original 106px icons have asymmetric transparent padding. Crop only
            // the known alpha bounds, keeping the shared PNG and imported Sprite intact.
            if (source.packed || source.rect.size != new Vector2(106, 106)) return source;
            float y = id <= 2 || id == 5 || id == 6 ? 13 : 0;
            float height = id == 3 || id == 4 ? 106 : id == 5 || id == 6 ? 80 : 93;
            var bounds = new Rect(source.rect.x + 13, source.rect.y + y, 93, height);
            var icon = Sprite.Create(source.texture, bounds, Vector2.one * .5f, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            icon.name = source.name;
            hudIcons.Add(id, icon);
            return icon;
        }

        private Vector2 ClampCenter(Vector2 center, Vector2 size)
        {
            var inset = size / 2 + Vector2.one * 16;
            return new Vector2(Mathf.Clamp(center.x, inset.x, Mathf.Max(inset.x, root.rect.width - inset.x)),
                Mathf.Clamp(center.y, inset.y, Mathf.Max(inset.y, root.rect.height - inset.y)));
        }

        private void BuildActionIcon(Transform parent, string iconName, string label, string key)
        {
            var sprite = Resources.Load<Sprite>("HudActions/" + iconName);
            assetsAvailable &= sprite != null;
            var icon = Image("Icon", parent, sprite, new Vector2(0, 18), new Vector2(64, 64));
            icon.preserveAspect = true;
            Text("Label", parent, new Vector2(0, -23), new Vector2(88, 18), 16, TextAnchor.MiddleCenter).text = label;
            var hint = Text("KeyHint", parent, new Vector2(0, -44), new Vector2(84, 18),
                key.Length > 1 ? 16 : 18, TextAnchor.MiddleCenter);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.text = key;
            hint.color = Accent;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size,
            Color? surface = null, Color? border = null)
        {
            var panel = Image(name, parent, null, position, size, border ?? Border);
            var inner = Image("Surface", panel.transform, null, Vector2.zero, new Vector2(-4, -4), surface ?? Surface);
            inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
            DuelVisualTheme.Frame(inner);
            return panel;
        }

        private static Image PanelSurface(Image panel)
        {
            Transform child = panel.transform.Find("Surface");
            return child != null ? child.GetComponent<Image>() : panel;
        }

        private static void Dress(Image panel) => DuelVisualTheme.DressPanel(PanelSurface(panel));

        private static Button AddButton(Image image, bool primary = false)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = PanelSurface(image);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            return button;
        }

        private static Outline AddGuideOutline(Image image)
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;
            outline.enabled = false;
            return outline;
        }

        private static RectTransform GuideFocusFrame(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var frame = Rect(name, parent, position, size, Vector2.one * .5f);
            Color gold = Accent;
            var top = Image("Top", frame, null, Vector2.zero, new Vector2(0f, 3f), gold).rectTransform;
            top.anchorMin = new Vector2(0f, 1f); top.anchorMax = Vector2.one;
            var bottom = Image("Bottom", frame, null, Vector2.zero, new Vector2(0f, 3f), gold).rectTransform;
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1f, 0f);
            var left = Image("Left", frame, null, Vector2.zero, new Vector2(3f, 0f), gold).rectTransform;
            left.anchorMin = Vector2.zero; left.anchorMax = new Vector2(0f, 1f);
            var right = Image("Right", frame, null, Vector2.zero, new Vector2(3f, 0f), gold).rectTransform;
            right.anchorMin = new Vector2(1f, 0f); right.anchorMax = Vector2.one;
            frame.gameObject.SetActive(false);
            return frame;
        }

        private static void Pin(RectTransform rect, Vector2 anchor)
        {
            rect.anchorMin = rect.anchorMax = anchor;
        }

        private Sprite Load(string path)
        {
            if (!sprites.TryGetValue(path, out var sprite))
            {
                sprite = Resources.Load<Sprite>("LegacyHud/" + path);
                sprites[path] = sprite;
            }
            assetsAvailable &= sprite != null;
            return sprite;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var transform = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            transform.gameObject.layer = 5;
            transform.SetParent(parent, false);
            transform.anchorMin = transform.anchorMax = anchor;
            transform.anchoredPosition = position;
            transform.sizeDelta = size;
            return transform;
        }

        private static Image Image(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color? color = null)
        {
            var transform = Rect(name, parent, position, size, Vector2.one * .5f);
            var image = transform.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color ?? Color.white;
            image.raycastTarget = false;
            return image;
        }

        private Text Text(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var transform = Rect(name, parent, position, size, Vector2.one * .5f);
            var text = transform.gameObject.AddComponent<Text>();
            text.font = art.UIFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Foreground;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Filled(Image image, UnityEngine.UI.Image.FillMethod method, int origin)
        {
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = method;
            image.fillOrigin = origin;
            image.fillClockwise = true;
        }

        private static float OutQuint(float value) => 1 - Mathf.Pow(1 - value, 5);
        private static float OutQuad(float value) => 1 - (1 - value) * (1 - value);

        private static void Destroy(GameObject gameObject)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(gameObject);
            else UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private static void ClearSelection(GameObject button)
        {
            // Keyboard commits belong to the controller. A previously clicked
            // skill must not also receive the EventSystem's default Enter submit.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private sealed class StatusView
        {
            public readonly RectTransform Root;
            public Image Health, HealthLag, Resistance, ResistanceLag;
            public Text ActivePowerBuff, ActiveProtectionBuff, GrantedPowerBuff, GrantedProtectionBuff;
            public Text Counter;
            private LegacySkillFeedback shownBuffFeedback;
            private LegacyCounter shownCounter;
            private int shownCounterUses = -1;
            private float delay;
            public StatusView(RectTransform root) { Root = root; }

            public void SetCounter(LegacyCounter counter, int remaining)
            {
                if (ReferenceEquals(shownCounter, counter) && shownCounterUses == remaining) return;
                shownCounter = counter;
                shownCounterUses = remaining;
                Counter.text = counter == null ? string.Empty
                    : $"반격 · {counter.Skill.Name} {remaining}/{counter.UsesPerTurn}";
            }

            public void SetBuffFeedback(LegacySkillFeedback value)
            {
                if (ReferenceEquals(shownBuffFeedback, value)) return;
                shownBuffFeedback = value;
                int rows = 0;
                bool applied = value != null && value.HasBeneficialBuff;
                bool granted = value != null && value.HasGrantedBeneficialBuff;
                SetBuffRow(ActivePowerBuff, applied ? value.PowerBuffPercent : 0, "현재 위력 +", "%", ref rows);
                SetBuffRow(ActiveProtectionBuff, applied ? value.ProtectionBuffPercent : 0, "현재 피해 감소 ", "%", ref rows);
                string next = granted ? $"다음 {value.GrantedBuffSlots}칸 " : string.Empty;
                SetBuffRow(GrantedPowerBuff, granted ? value.GrantedPowerBuffPercent : 0, next + "위력 +", "%", ref rows);
                SetBuffRow(GrantedProtectionBuff, granted ? value.GrantedProtectionBuffPercent : 0, next + "피해 감소 ", "%", ref rows);
                Root.sizeDelta = new Vector2(236, 92 + rows * 24);
            }

            private static void SetBuffRow(Text label, int percent, string prefix, string suffix, ref int rows)
            {
                var row = (RectTransform)label.transform.parent;
                row.gameObject.SetActive(percent > 0);
                if (percent <= 0) { label.text = string.Empty; return; }
                label.text = prefix + percent + suffix;
                row.anchoredPosition = new Vector2(0, 104 + rows * 24);
                rows++;
            }

            public void Refresh(LegacyFighterState state, float delta)
            {
                float health = state.Health / (float)state.MaxHealth;
                float resistance = state.MaxResistance > 0 ? state.Resistance / (float)state.MaxResistance : 0;
                if (health < Health.fillAmount || resistance < Resistance.fillAmount) delay = .5f;
                Health.fillAmount = health;
                Resistance.fillAmount = resistance;
                if (delay <= 0)
                {
                    HealthLag.fillAmount = Mathf.MoveTowards(HealthLag.fillAmount, health, delta);
                    ResistanceLag.fillAmount = Mathf.MoveTowards(ResistanceLag.fillAmount, resistance, delta);
                }
                else delay -= delta;
            }
            public void Reset()
            {
                SetBuffFeedback(null);
                SetCounter(null, 0);
                delay = 0;
                Health.fillAmount = HealthLag.fillAmount = Resistance.fillAmount = ResistanceLag.fillAmount = 1;
            }
        }

        private sealed class QueueView
        {
            public readonly RectTransform Root;
            public Vector2 DisplaySize { get; private set; }
            private readonly bool player;
            private readonly Sprite white;
            private readonly Font font;
            private readonly List<Image> icons = new List<Image>(), highlights = new List<Image>();
            private readonly List<SkillCardFeedbackGraphic> feedback = new List<SkillCardFeedbackGraphic>();
            private readonly List<GameObject> counterMarks = new List<GameObject>();
            private readonly List<int> counterSlots = new List<int>();
            private IReadOnlyList<LegacySkill> displayedQueue;
            private LegacySkill preview;
            private LegacySkillFeedback currentFeedback;
            private int previewSlot = -1, feedbackSlot = -1;
            public QueueView(RectTransform root, bool player, Sprite white, Font font)
            {
                Root = root; this.player = player; this.white = white; this.font = font;
            }
            public void Refresh(IReadOnlyList<LegacySkill> queue, Func<int, Sprite> iconFor, int activeSlot, bool resolving, int selected, float scale, int columns)
            {
                while (icons.Count < queue.Count)
                {
                    var item = Panel("Queued Skill", Root, Vector2.zero, new Vector2(64, 64), Card).rectTransform;
                    Pin(item, Vector2.one * .5f);
                    var highlight = Image("Selected", item, white, Vector2.zero, new Vector2(64, 64), new Color(Accent.r, Accent.g, Accent.b, .4f));
                    var icon = Image("Icon", item, null, Vector2.zero, new Vector2(48, 48));
                    icon.preserveAspect = true;
                    icons.Add(icon); highlights.Add(highlight);
                    feedback.Add(SkillCardFeedbackGraphic.Create(item, "Skill Condition Feedback", 3f));
                    counterMarks.Add(CreateCounterMark(item));
                }
                displayedQueue = queue;
                int consumed = resolving ? Mathf.Max(0, activeSlot) : 0;
                int remaining = Mathf.Max(0, queue.Count - consumed);
                int rows = (remaining + columns - 1) / columns;
                DisplaySize = remaining > 0 ? new Vector2(Mathf.Min(columns, remaining) * 72 - 8, rows * 72 - 8) : Vector2.zero;
                for (int i = 0; i < icons.Count; i++)
                {
                    var item = icons[i].transform.parent.GetComponent<RectTransform>();
                    bool visible = i < queue.Count && i >= consumed;
                    item.gameObject.SetActive(visible);
                    if (!visible) { feedback[i].Clear(); continue; }
                    icons[i].sprite = iconFor(queue[i].IconId);
                    int visibleIndex = i - consumed;
                    int row = visibleIndex / columns;
                    int rowCount = Mathf.Min(columns, remaining - row * columns);
                    item.anchoredPosition = new Vector2((player ? -1 : 1) * (visibleIndex % columns - (rowCount - 1) * .5f) * 72,
                        -row * 72);
                    item.localScale = Vector3.one * (resolving && i == activeSlot ? Mathf.Min(scale, 1.2f) : 1);
                    highlights[i].enabled = !resolving && i == selected;
                }
                for (int i = 0; i < counterMarks.Count; i++)
                    counterMarks[i].SetActive(icons[i].transform.parent.gameObject.activeSelf && counterSlots.Contains(i));
                UpdateFeedback();
            }
            /// <summary>Queue indices whose attack the opponent's counter will answer.</summary>
            public void SetCounterMarks(List<int> slots)
            {
                counterSlots.Clear();
                counterSlots.AddRange(slots);
            }
            private GameObject CreateCounterMark(RectTransform item)
            {
                // A small tab over the card's top edge, readable without opening the detail view.
                var badge = Image("Counter Mark", item, white, new Vector2(0, 30), new Vector2(46, 17),
                    new Color(DuelVisualTheme.Danger.r, DuelVisualTheme.Danger.g, DuelVisualTheme.Danger.b, .92f));
                var label = Rect("Label", badge.transform, Vector2.zero, new Vector2(46, 17), Vector2.one * .5f)
                    .gameObject.AddComponent<Text>();
                label.font = font;
                label.fontSize = 12;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.supportRichText = false;
                label.raycastTarget = false;
                label.color = Foreground;
                label.text = "반격";
                badge.gameObject.SetActive(false);
                return badge.gameObject;
            }
            public void SetConditionPreview(LegacySkill skill, int slot)
            {
                preview = skill;
                previewSlot = slot;
                UpdateFeedback();
            }
            public void SetCurrentFeedback(int slot, LegacySkillFeedback value)
            {
                feedbackSlot = slot;
                currentFeedback = value;
                UpdateFeedback();
            }
            private void UpdateFeedback()
            {
                for (int index = 0; index < feedback.Count; index++)
                {
                    bool visible = icons[index].transform.parent.gameObject.activeSelf &&
                        displayedQueue != null && index < displayedQueue.Count;
                    bool target = visible && LegacySkillConditions.MatchesOpponent(preview, displayedQueue[index]);
                    bool actual = visible && index == feedbackSlot && currentFeedback != null;
                    bool beneficial = actual && currentFeedback.HasBeneficialBuff;
                    feedback[index].SetState(target, (target && index == previewSlot) ||
                        (actual && currentFeedback.ConditionMet),
                        actual && currentFeedback.EffectActivated,
                        beneficial ? currentFeedback.PowerBuffPercent : 0,
                        beneficial ? currentFeedback.ProtectionBuffPercent : 0);
                }
            }
            public void TickFeedback(float realDelta)
            {
                foreach (var view in feedback) view.Tick(realDelta);
            }
            public void Clear()
            {
                DisplaySize = Vector2.zero;
                displayedQueue = null;
                preview = null;
                previewSlot = feedbackSlot = -1;
                currentFeedback = null;
                counterSlots.Clear();
                foreach (var view in feedback) view.Clear();
                foreach (var icon in icons) icon.transform.parent.gameObject.SetActive(false);
                foreach (var mark in counterMarks) mark.SetActive(false);
            }
            public RectTransform GetAnchor(int index)
            {
                if (index < 0 || index >= icons.Count) return null;
                var card = icons[index].transform.parent.GetComponent<RectTransform>();
                return card.gameObject.activeInHierarchy ? card : null;
            }
        }

        private sealed class DamageView
        {
            public readonly Text Text;
            public readonly Outline Outline;
            public Vector3 WorldPosition;
            /// <summary>When set, the view tracks this transform instead of the fixed impact point.</summary>
            public Transform Follow;
            public Vector3 WorldOffset;
            public Vector2 StartOffset;
            public Color Color;
            public float Direction;
            public float Duration, Remaining, Scale;
            public DamageView(Text text, Outline outline) { Text = text; Outline = outline; }
        }
    }
}
