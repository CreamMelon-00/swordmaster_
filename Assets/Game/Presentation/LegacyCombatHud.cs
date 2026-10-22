using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;
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
    public sealed partial class LegacyCombatHud : IDisposable
    {
        private readonly LegacyDuelArt art;
        private readonly DuelPresentationSettings presentationSettings;
        private readonly Canvas canvas;
        private readonly RectTransform root;
        private readonly RectTransform turnHeader;
        // The whole HUD's fade (SetFade): an overlay the arena's grade never reaches fades on its own.
        private readonly CanvasGroup fade;
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
        private static readonly Vector2 TimeSpentHome = new Vector2(340f, -10f);
        private readonly Text cycleCost, cycleEffect, timeSpent;
        private float laneCycleCost;
        private bool laneCycleAffordable = true;
        private float timeSpentRemaining;
        private readonly Text turnText, timeText, actText, stageText;
        private readonly Button[] laneButtons = new Button[3];
        private readonly CanvasGroup[] guideLaneGroups = new CanvasGroup[3];
        private readonly bool[] laneAffordable = new bool[3];
        private readonly Button breathButton;
        private readonly Text breathCount;
        private readonly RectTransform breathNote;
        private readonly Action queueBreath, cycleLanes;
        private Button cycleButton;
        private RectTransform cycleNote;
        private ILegacyDuelView displayedSession;
        private int shownBreathsRemaining = -1;
        private MissionGuide guide;
        private bool lastPlanning;
        private readonly Image[] holdImages = new Image[3];
        private const float HoldCancelSeconds = .45f;
        private readonly RectTransform[] holdCancelPlates = new RectTransform[3];
        private readonly CanvasGroup[] holdCancelGroups = new CanvasGroup[3];
        private readonly float[] holdCancelRemaining = new float[3];
        private readonly Text[] costs = new Text[3];
        private readonly Text[] skillNames = new Text[3];
        private readonly RectTransform[] cycleMarks = new RectTransform[3];
        private readonly Text[] cycleCounts = new Text[3];
        private readonly SkillCardFeedbackGraphic[] laneFeedback = new SkillCardFeedbackGraphic[3];
        private readonly int[] shownSkills = { -1, -1, -1 };
        private readonly Button commitButton;
        private readonly CanvasGroup guideCommitGroup;
        private readonly Outline[] guideLaneFocus = new Outline[3];
        private AcademyControlAnimation cycleAnimation, breathAnimation;
        private readonly Outline guideCommitFocus;
        private readonly RectTransform guideCommitRing;
        private readonly RectTransform guideEnemyFocus, guideActFocus;
        private bool missionMode, missionModeInitialized, missionTimed, missionBreath, trainingMode, highlightEnemyQueue;
        private const CombatFeature AllLanes = CombatFeature.LaneQ | CombatFeature.LaneW | CombatFeature.LaneE;
        // The open lanes' gears are packed in Q, W, E order and centred (LayoutGears). 넘기기 and 숨고르기 keep their places
        // at ±360 whatever is open. Closed lanes stay hidden where they were. Laid out again only when a duel opens a
        // different set (or the gears' tuning changes).
        private CombatFeature laidOutLanes = AllLanes;
        private int stageNumber, stageCount;
        private string stageName;
        // The stage plate's words as given (SetStageText), over a stage's, a mission's or training's; null for those.
        private string stageOverride;
        // The planning inputs wait on someone else (SetInputsHeld): versus's opponent's turn, or a request the host has
        // not confirmed yet.
        private bool inputsHeld;
        private const float HeldInputAlpha = .55f;
        private readonly Text commitLabel;
        /// <summary>The commit seal's word in the campaign's battles (<see cref="SetCommitLabel"/>).</summary>
        public const string CommitLabel = "확정";
        private readonly StatusView playerStatus, enemyStatus;
        private readonly QueueView playerQueue, enemyQueue;
        private const float QueueCancellationSeconds = .64f, QueueCancellationFadeStart = .33f;
        private readonly RectTransform queueCancellationLayer;
        private readonly List<QueueCancellationGhost> queueCancellationGhosts = new List<QueueCancellationGhost>();
        // Each fighter's status panel over its head as last laid out, and the panel's centre (TryGetHeadStack).
        private Rect playerHeadStack, enemyHeadStack;
        private float playerHeadX, enemyHeadX;
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
        public const string BreakCalloutText = "저항 붕괴";
        public const string RecoveryCalloutText = "저항 회복";
        /// <summary>The enemy explanation's badge until the player has opened all three schools.</summary>
        public const string EnemySkillCaption = "상대 기술";
        // The footer marks a named school's skill as the enemy's; the neutral badge already says so.
        private const string EnemyHint = "상대 기술 · Tab / 적 확인", NeutralEnemyHint = "Tab / 적 확인";
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
        // The top of a card kept up over the dock (the coach's), in screen pixels from the bottom; 0 for none.
        private float explanationFloor;
        private LegacySkill conditionPreview;
        private LegacySkill stateHintSkill;
        private string stateHint;
        private ILegacySlotView feedbackSlot;
        private Camera worldCamera;
        private bool disposed;
        private float transition, targetTransition, slotElapsed, slotDuration;
        private float cinematic, cinematicFrom, cinematicTarget, cinematicElapsed = .3f;
        private float combatLetterboxRequest, combatLetterboxAccent;
        private float viewAngle, angleFrom, angleTarget, angleElapsed = .15f;
        private float logFrom = 1080, logTarget = 1080, logElapsed = .5f;
        private bool canOpenLog;
        private int inspectedSlot = -1, shownTurn = -1, shownSeconds = -1, shownAct = -1, shownMaxAct = -1;
        private bool slotAnimation;
        private bool assetsAvailable = true;
        // A compact command desk leaves the arena visible on both sides. Its two end buttons sit on
        // small tabs outside the desk, but remain in the same input group for the planning fade.
        private const float DockWidth = 1240f, DockHeight = 248f, DockBottomInset = 14f;
        private const float EndButtonX = 550f, ToolDividerX = 288f;
        /// <summary>The duel HUD's canvas order: over the arena, under what stands over a battle (versus's overlay at
        /// <see cref="LocalVersusHud.SortingOrder"/>, the bars, the start and result cards).</summary>
        public const int SortingOrder = 100;
        /// <summary>The ACT track's height in the dock ('Input/Keys' space), along its top over the gears; the
        /// <c>n / 10 ACT</c> value sits just above it.</summary>
        public const float ActTrackY = 76f;
        /// <summary>How much larger a held skill's explanation is drawn than the enemy's under Tab (its paper, type and
        /// fittings), so it reads at a glance; it opens above the dock, centred on the held lane.</summary>
        public const float PlayerExplanationScale = 1.25f;
        /// <summary>The gap between the dock's top (or the coach's card over it) and a held skill's explanation (HUD units).</summary>
        public const float ExplanationGap = 10f;
        // The held explanation's drop shadow: it lifts the paper off the arena behind it.
        private static readonly Color ExplanationShade = new Color(.04f, .03f, .02f, .7f);
        private static readonly Vector2 ExplanationShadeOffset = new Vector2(5f, -7f);
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
        /// <summary>How much of the whole HUD shows, 1 (as drawn) to 0 (<see cref="SetFade"/>).</summary>
        public float Fade => disposed ? 1f : fade.alpha;
        public bool LogOpen { get; private set; }

        /// <summary>The additional depth of each normal combat band at full accent (HUD reference pixels). It stays
        /// behind the queue and labels; the finishing blow's separate letterbox keeps its own framing.</summary>
        public const float MaximumCombatLetterboxInset = 36f;
        /// <summary>The combat camera's accent as eased for the bands, from 0 to 1.</summary>
        public float CombatLetterboxAccent => combatLetterboxAccent;
        /// <summary>The additional depth drawn now, over the existing 64-pixel combat band.</summary>
        public float CombatLetterboxInset => MaximumCombatLetterboxInset * combatLetterboxAccent * cinematic;

        /// <summary>Asks the existing combat bands to close in this frame. Refresh consumes the request on its real-time
        /// clock; without another request they ease back out. Planning and Reset clear it.</summary>
        public void SetCombatLetterboxAccent(float amount)
        {
            if (disposed) return;
            combatLetterboxRequest = float.IsNaN(amount) || float.IsInfinity(amount) ? 0f : Mathf.Clamp01(amount);
        }

        /// <summary>Fades the whole HUD, its speech bubbles and step cues included: 1 shows it as drawn, 0 hides it (and it
        /// takes no clicks). The 서막's final fall fades it with the arena's colour, which as an overlay it never gets;
        /// whoever fades it brings it back to 1.</summary>
        public void SetFade(float amount)
        {
            if (disposed) return;
            fade.alpha = Mathf.Clamp01(amount);
            fade.blocksRaycasts = fade.alpha > 0f;
        }
        public int LogCount => logRows.Count;

        /// <summary>The displayed queue card in its panel over the dock (the player's on the left, the opponent's on the right),
        /// including its pulse, slide and clash motion; null while the slot shows no card.</summary>
        public RectTransform GetQueuedSkillAnchor(bool playerSide, int queueIndex)
            => disposed ? null : (playerSide ? playerQueue : enemyQueue).GetAnchor(queueIndex);

        /// <summary>Lets cancelled reservations tumble out from their last visible slots. Gameplay has already
        /// removed them; these non-interactive cards only bridge the queue's immediate visual refresh. Returned ones
        /// (<paramref name="mode"/> 반환, their ACT refunded) wear 반환's green ring, cancelled ones the danger ring.</summary>
        public void PlayQueueCancellation(IReadOnlyList<LegacySkill> queuedBefore,
            LegacyQueueRemovalMode mode = LegacyQueueRemovalMode.Cancel)
        {
            if (disposed || queuedBefore == null) return;
            for (int index = 0; index < queuedBefore.Count; index++)
            {
                LegacySkill skill = queuedBefore[index];
                if (skill == null || skill.IsWait) continue;
                RectTransform source = playerQueue.GetAnchor(index);
                if (source == null) continue;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, source.position);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(queueCancellationLayer,
                    screen, null, out Vector2 start)) continue;

                var card = Panel(mode == LegacyQueueRemovalMode.Return ? "Returned Skill" : "Cancelled Skill",
                    queueCancellationLayer, start, Vector2.one * QueueCardSize, Card,
                    mode == LegacyQueueRemovalMode.Return ? DuelVisualTheme.Health : DuelVisualTheme.Danger);
                var icon = Image("Icon", card.transform, iconFor(skill.IconId), Vector2.zero, Vector2.one * QueueIconSize);
                icon.preserveAspect = true;
                // As large as the card it leaves (the panels' tuned card size).
                card.rectTransform.localScale = Vector3.one * queueCardScale;
                var group = card.gameObject.AddComponent<CanvasGroup>();
                group.interactable = group.blocksRaycasts = false;
                queueCancellationGhosts.Add(new QueueCancellationGhost(card.rectTransform, group, start, index));
            }
        }

        private void AdvanceQueueCancellations(float realDelta)
        {
            for (int index = queueCancellationGhosts.Count - 1; index >= 0; index--)
            {
                QueueCancellationGhost ghost = queueCancellationGhosts[index];
                ghost.Elapsed = Mathf.Min(QueueCancellationSeconds, ghost.Elapsed + Mathf.Max(0f, realDelta));
                float time = ghost.Elapsed;
                // A short upward kick releases the card from its slot; gravity pulls it down as it spins outward.
                ghost.Rect.anchoredPosition = ghost.Start + new Vector2(
                    ghost.OutwardSpeed * time, ghost.UpwardSpeed * time - 625f * time * time);
                ghost.Rect.localRotation = Quaternion.Euler(0f, 0f, ghost.SpinSpeed * time);
                ghost.Group.alpha = 1f - Mathf.Clamp01((time - QueueCancellationFadeStart) /
                    (QueueCancellationSeconds - QueueCancellationFadeStart));
                if (time < QueueCancellationSeconds) continue;
                ghost.Rect.gameObject.SetActive(false);
                Destroy(ghost.Rect.gameObject);
                queueCancellationGhosts.RemoveAt(index);
            }
        }

        private void ClearQueueCancellations()
        {
            foreach (QueueCancellationGhost ghost in queueCancellationGhosts)
            {
                ghost.Rect.gameObject.SetActive(false);
                Destroy(ghost.Rect.gameObject);
            }
            queueCancellationGhosts.Clear();
        }

        public void BeginCombat() => SetCinematic(1);

        public void BeginReturn()
        {
            combatLetterboxRequest = 0f;
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
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            fade = root.gameObject.AddComponent<CanvasGroup>();
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

            var inputImage = Panel("Input", root, new Vector2(0f, DockBottomInset),
                new Vector2(DockWidth, DockHeight));
            BuildCommandDesk(inputImage);
            inputPanel = inputImage.rectTransform;
            inputPanel.anchorMin = inputPanel.anchorMax = new Vector2(.5f, 0f);
            inputPanel.pivot = new Vector2(.5f, 0f);
            var controls = Rect("Keys", inputPanel, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            controls.anchorMin = Vector2.zero; controls.anchorMax = Vector2.one;
            inputs = controls.gameObject.AddComponent<CanvasGroup>();
            Image("Instrument Bed", controls, null, new Vector2(0, 96), new Vector2(468, 56), Track);
            Image("Instrument Bezel", controls, AcademyUiSkin.GaugeBezel, new Vector2(0, 96), new Vector2(544, 72));
            // Thin metal joins make the log and seal feel attached to the desk, without covering their hit areas.
            Text("Desk Heading", controls, new Vector2(-472f, 72f), new Vector2(218f, 24f), 13, TextAnchor.MiddleCenter).text = "전투 준비";
            Color dividerInk = Accent;
            dividerInk.a = .45f;
            // Between the gears' room (±284) and 넘기기/숨고르기 (from ±292).
            Image("Left Tool Divider", controls, white, new Vector2(-ToolDividerX, -4f), new Vector2(1f, 146f), dividerInk);
            Image("Right Tool Divider", controls, white, new Vector2(ToolDividerX, -4f), new Vector2(1f, 146f), dividerInk);
            var logImage = Panel("LogButton", controls, new Vector2(-EndButtonX, -16f), new Vector2(96, 132));
            BuildActionIcon(logImage.transform, "combat-log", "기록", "L");
            var logButton = AddButton(logImage);
            ConfigureCommandControl(logImage, logButton, "HudActions/combat-log", 64f, new Vector2(0f, 20f), logImage.transform.Find("Icon").GetComponent<Image>());
            logButton.onClick.AddListener(() => { ToggleLog(); ClearSelection(logButton.gameObject); });
            // The lanes' gears turn up out of the dock's bottom edge; the ACT track runs along its top.
            BuildSkillGears(controls, white, queue);
            BuildLanePreviews();
            BuildTemporaryMarks();
            var breathe = Panel("BreathButton", controls, new Vector2(360, -4), new Vector2(152, 172), Card);
            var breathIcon = Image("Icon", breathe.transform, art.GetSkillIcon(LegacyCommonActions.Breathe.IconId),
                new Vector2(0f, 20f), Vector2.one * 96f);
            breathIcon.preserveAspect = true;
            Text("Label", breathe.transform, new Vector2(0, -35), new Vector2(140, 24), 19,
                TextAnchor.MiddleCenter).text = "숨고르기";
            var breathCost = Text("ACT Cost", breathe.transform, new Vector2(0, -59), new Vector2(140, 18), 14,
                TextAnchor.MiddleCenter);
            breathCost.text = "ACT 0"; breathCost.color = MutedText;
            breathCount = Text("Remaining", breathe.transform, new Vector2(0, -79), new Vector2(140, 18), 14,
                TextAnchor.MiddleCenter);
            breathCount.color = Foreground;
            var breathKey = Text("KeyHint", breathe.transform, new Vector2(0, 78), new Vector2(140, 22), 17,
                TextAnchor.MiddleCenter);
            breathKey.text = "S"; breathKey.color = Accent;
            var note = Text("Breath Note", controls, new Vector2(360, -81), new Vector2(200, 28), 12,
                TextAnchor.MiddleCenter);
            note.text = "한 박자 쉬어\n기술 순서 조절"; note.color = MutedText;
            breathNote = note.rectTransform;
            breathButton = AddButton(breathe);
            ConfigureCommandControl(breathe, breathButton, "AcademyUI/breath-valve", 96f, new Vector2(0f, 20f), breathIcon);
            breathAnimation = new AcademyControlAnimation(breathIcon, "Valve", .65f);
            breathButton.onClick.AddListener(QueueBreathFromButton);
            BuildAutoBreathCount(breathe.transform);
            // 넘기기 mirrors 숨고르기 on the other side of the lanes.
            var cycle = Panel("CycleButton", controls, new Vector2(-360, -4), new Vector2(152, 172), Card);
            Text("Label", cycle.transform, new Vector2(0, -35), new Vector2(140, 24), 19, TextAnchor.MiddleCenter).text = "넘기기";
            cycleCost = Text("Time Cost", cycle.transform, new Vector2(0, -59), new Vector2(140, 18), 14, TextAnchor.MiddleCenter);
            cycleCost.text = CycleCostText(0f); cycleCost.color = MutedText;
            cycleEffect = Text("Effect", cycle.transform, new Vector2(0, -79), new Vector2(140, 18), 14, TextAnchor.MiddleCenter);
            cycleEffect.text = CycleEffectText(AllLanes); cycleEffect.color = Foreground;
            var cycleKey = Text("KeyHint", cycle.transform, new Vector2(0, 78), new Vector2(140, 22), 17, TextAnchor.MiddleCenter);
            cycleKey.text = "Shift"; cycleKey.color = Accent;
            var cycleHint = Text("Cycle Note", controls, new Vector2(-360, -81), new Vector2(200, 28), 12, TextAnchor.MiddleCenter);
            cycleHint.text = "맨 앞 기술을 쓰지 않고\n뒤로 보냄"; cycleHint.color = MutedText;
            cycleNote = cycleHint.rectTransform;
            cycleButton = AddButton(cycle);
            ConfigureCommandControl(cycle, cycleButton, "AcademyUI/cycle-lever", 96f, new Vector2(0f, 20f));
            cycleAnimation = new AcademyControlAnimation(cycle.transform.Find("Icon").GetComponent<Image>(), "Lever", .45f);
            cycleButton.onClick.AddListener(CycleFromButton);
            Image("Act_BG", controls, AcademyUiSkin.GaugeFill, new Vector2(0, ActTrackY), new Vector2(468, 12), new Color(.28f, .24f, .18f));
            actFill = Image("Act_Gauge", controls, AcademyUiSkin.GaugeFill, new Vector2(0, ActTrackY), new Vector2(468, 12), Color.white);
            Filled(actFill, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            for (int unit = 1; unit < 10; unit++)
                Image("ACT Divider " + unit, controls, white, new Vector2(-234 + unit * 46.8f, ActTrackY), new Vector2(2, 12), Surface);
            actText = Text("Act_Value", controls, new Vector2(0, ActTrackY + 20f), new Vector2(160, 22), 22, TextAnchor.MiddleCenter);
            actText.color = Foreground;
            guideActFocus = GuideFocusFrame("Guide ACT Focus", controls, new Vector2(0f, ActTrackY + 11f), new Vector2(540f, 40f));
            var start = Panel("AButton", controls, new Vector2(EndButtonX, -4f), new Vector2(108, 172), RaisedSurface, Accent);
            BuildActionIcon(start.transform, "confirm-turn", CommitLabel, "Space");
            commitLabel = start.transform.Find("Label").GetComponent<Text>();
            commitButton = AddButton(start, true);
            ConfigureCommandControl(start, commitButton, "AcademyUI/commit-seal", 96f, new Vector2(0f, 18f), start.transform.Find("Icon").GetComponent<Image>());
            commitLabel.rectTransform.anchoredPosition = new Vector2(0f, -43f);
            commitLabel.fontSize = 19;
            start.transform.Find("KeyHint").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -71f);
            guideCommitGroup = start.gameObject.AddComponent<CanvasGroup>();
            guideCommitFocus = AddGuideOutline(start);
            guideCommitFocus.effectColor = Color.clear;
            guideCommitRing = Rect("Command Seal Guide Ring", start.transform, new Vector2(0f, 18f),
                Vector2.one * 104f, Vector2.one * .5f);
            guideCommitRing.gameObject.AddComponent<AcademySealFocus>().raycastTarget = false;
            guideCommitRing.gameObject.SetActive(false);
            commitButton.onClick.AddListener(() => { commit?.Invoke(); ClearSelection(commitButton.gameObject); });
            // First laid out for all three lanes; a duel with fewer packs its own.
            LayoutGears(AllLanes, true);

            // Keep the planning clock on the command desk: the coach and held skill card occupy the space above it.
            // A thin bar rides its inner top edge, while ACT and seconds share the row directly underneath.
            timerPanel = Rect("TimerBG", controls, new Vector2(0f, 108f), new Vector2(540f, 32f), Vector2.one * .5f);
            timerTrack = Image("Timer Track", timerPanel, AcademyUiSkin.GaugeFill, new Vector2(0f, 8f), new Vector2(468f, 10f), new Color(.28f, .24f, .18f));
            timerFill = Image("Timer", timerPanel, AcademyUiSkin.GaugeFill, new Vector2(0f, 8f), new Vector2(468f, 10f), Color.white);
            Filled(timerFill, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            BuildTimerGear(controls);
            timeText = Text("Time Remaining", timerPanel, new Vector2(190f, -5f), new Vector2(88f, 24f), 22,
                TextAnchor.MiddleCenter);
            untimedHint = Text("Untimed Hint", timerPanel, new Vector2(190f, -5f), new Vector2(200f, 24f), 16,
                TextAnchor.MiddleRight);
            untimedHint.text = "임무 · 시간 제한 없음";
            untimedHint.color = Accent;
            // The time cost appears beside the seconds, within the dock even at the top of its rise.
            timeSpent = Text("Time Spent", timerPanel, TimeSpentHome, new Vector2(90, 26), 20, TextAnchor.MiddleLeft);
            timeSpent.color = DuelVisualTheme.Danger;
            timeSpent.gameObject.SetActive(false);

            var stagePanel = Panel("Stage", root, new Vector2(224, -36), new Vector2(396, 48)).rectTransform;
            Pin(stagePanel, new Vector2(0, 1));
            stageText = Text("Stage Label", stagePanel, Vector2.zero, new Vector2(372, 32), 18, TextAnchor.MiddleLeft);
            stageText.color = MutedText;

            // The former top-centre clock position now holds the round throughout planning and resolution.
            // Flags frame the number without turning the emblem into another control.
            Vector2 flagSize = new Vector2(DuelCrossedFlags.PreferredWidth, DuelCrossedFlags.PreferredHeight);
            turnHeader = Rect("Turn Header", root, new Vector2(0f, -36f), flagSize, new Vector2(.5f, 1f));
            var crossedFlags = Rect("Crossed Flags", turnHeader, Vector2.zero, flagSize,
                Vector2.one * .5f).gameObject.AddComponent<DuelCrossedFlags>();
            crossedFlags.raycastTarget = false;
            var turnBadge = Panel("Turn Badge", turnHeader, new Vector2(0f, 4f), new Vector2(76f, 56f), Surface, Border);
            var turnLabel = Text("Turn Label", turnBadge.transform, new Vector2(0f, -18f), new Vector2(64f, 16f), 12,
                TextAnchor.MiddleCenter);
            turnLabel.text = "턴";
            turnLabel.color = Accent;
            turnText = Text("TurnCount", turnBadge.transform, new Vector2(0f, 4f), new Vector2(64f, 32f), 27,
                TextAnchor.MiddleCenter);
            turnText.color = Foreground;

            playerStatus = BuildStatus(false);
            enemyStatus = BuildStatus(true);
            // 맞물림's marks ride the player's queued cards only; the enemy's queue never meshes.
            BuildMeshPictures();
            // The two facing queue panels over the dock (LegacyCombatHud.QueuePanels.cs): their trays and seam first, so the
            // card rows draw over them. The rows keep their names and stay the HUD root's children.
            BuildQueuePanels(white);
            // The next-opponent ring, the socket and the cards leaving the seam draw under the cards; their sparks over them
            // (LegacyCombatHud.Clash.cs).
            BuildQueueCues();
            playerQueue = new QueueView(Rect("Player Requests", root, Vector2.zero, Vector2.zero, Vector2.one * .5f), true, white,
                art.UIFont, meshGearSprite);
            enemyQueue = new QueueView(Rect("Enemy Requests", root, Vector2.zero, Vector2.zero, Vector2.one * .5f), false, white, art.UIFont);
            guideEnemyFocus = GuideFocusFrame("Guide Enemy Queue Focus", enemyQueue.Root, Vector2.zero, Vector2.zero);
            BuildQueueSparks();
            // The 간파 cue over the cards and their sparks (LegacyCombatHud.Reads.cs).
            BuildReadCue();
            BuildCombatReadout();
            queueCancellationLayer = Rect("Queue Cancellation Layer", root, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            queueCancellationLayer.anchorMin = Vector2.zero;
            queueCancellationLayer.anchorMax = Vector2.one;
            BuildBreakQueueFragments();

            // A held skill's explanation: the enemy's card drawn larger (PlayerExplanationScale), on a dark drop shadow.
            const float explain = PlayerExplanationScale;
            var playerExplanationImage = Panel("Skill Explain", root, Vector2.zero, new Vector2(460, 368) * explain, DuelVisualTheme.Paper);
            Dress(playerExplanationImage);
            var shade = playerExplanationImage.gameObject.AddComponent<Shadow>();
            shade.effectColor = ExplanationShade;
            shade.effectDistance = ExplanationShadeOffset;
            playerExplanation = playerExplanationImage.rectTransform;
            playerExplanationIcon = Image("Explanation Skill Icon", playerExplanation, null, new Vector2(-183, 128) * explain, Vector2.one * 68 * explain);
            playerExplanationIcon.preserveAspect = true;
            playerName = Text("Name", playerExplanation, new Vector2(35, 148) * explain, new Vector2(326, 34) * explain,
                ExplanationType(26), TextAnchor.MiddleLeft);
            playerName.color = DuelVisualTheme.Ink;
            FitExplanationLabel(playerName, ExplanationType(16));
            playerStyle = new SkillLaneBadge(playerExplanation, art.UIFont, "Power Cost Property", Vector2.zero,
                144f * explain, 24f * explain, ExplanationType(15));
            playerDetails = playerStyle.Label;
            playerInfo = new SkillInfoView(playerExplanation, art.UIFont, new Vector2(0, -18) * explain, 416 * explain, "Player", explain);
            playerDescription = playerInfo.EffectText;
            playerDescription.name = "Effect";
            playerEffectFeedback = SkillCardFeedbackGraphic.Create(playerDescription.transform, "Current Effect Emphasis", 3f);
            playerExplanationHint = Text("Explanation Hint", playerExplanation, new Vector2(0, -157) * explain, new Vector2(416, 20) * explain,
                ExplanationType(14), TextAnchor.MiddleRight);
            playerExplanationHint.text = "키를 놓으면 닫기"; playerExplanationHint.color = DuelVisualTheme.Ink;
            FitExplanationLabel(playerExplanationHint, ExplanationType(11));
            BuildMeshNote(explain);
            BuildKeywordNote(explain);
            var enemyExplanationImage = Panel("Enemy Skill Explain", root, new Vector2(-480, 160), new Vector2(460, 368), DuelVisualTheme.Paper);
            Dress(enemyExplanationImage);
            enemyExplanation = enemyExplanationImage.rectTransform;
            enemyExplanationIcon = Image("Explanation Skill Icon", enemyExplanation, null, new Vector2(-183, 128), Vector2.one * 68);
            enemyExplanationIcon.preserveAspect = true;
            enemyName = Text("Name", enemyExplanation, new Vector2(35, 148), new Vector2(326, 34), 26, TextAnchor.MiddleLeft);
            enemyName.color = DuelVisualTheme.Ink;
            FitExplanationLabel(enemyName, 16);
            enemyStyle = new SkillLaneBadge(enemyExplanation, art.UIFont, "Power Property", Vector2.zero);
            enemyDetails = enemyStyle.Label;
            enemyInfo = new SkillInfoView(enemyExplanation, art.UIFont, new Vector2(0, -18), 416, "Enemy");
            enemyDescription = enemyInfo.EffectText;
            enemyDescription.name = "Effect";
            enemyEffectFeedback = SkillCardFeedbackGraphic.Create(enemyDescription.transform, "Current Effect Emphasis", 3f);
            enemyExplanationHint = Text("Explanation Hint", enemyExplanation, new Vector2(0, -157), new Vector2(416, 20), 14, TextAnchor.MiddleRight);
            enemyExplanationHint.text = EnemyHint; enemyExplanationHint.color = DuelVisualTheme.Ink;
            FitExplanationLabel(enemyExplanationHint, 11);
            BuildForecastNotes();
            BuildQueueComparison();

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

        /// <summary>The enemy's name at the right of its status panel's HP row (missions name their enemy as the battle
        /// knows it); null or empty shows none, as stages and training do.</summary>
        public void SetEnemyName(string name)
        {
            if (disposed) return;
            enemyStatus.Name.text = name ?? string.Empty;
        }

        /// <summary>The enemy name the status panel shows, or empty.</summary>
        public string EnemyName => enemyStatus.Name.text;

        public void SetStage(int stageNumber, int stageCount, string stageName)
        {
            this.stageNumber = stageNumber;
            this.stageCount = stageCount;
            this.stageName = stageName;
            UpdateStageLabel();
        }

        /// <summary>The stage plate's words as given, instead of a stage's, a mission's or training's (versus names its match
        /// there); null goes back to those. <see cref="Reset"/> keeps them.</summary>
        public void SetStageText(string text)
        {
            if (disposed) return;
            stageOverride = text;
            UpdateStageLabel();
        }

        /// <summary>What the stage plate says now.</summary>
        public string StageText => stageText.text;

        /// <summary>The debug spectator's controls replace the central turn badge.</summary>
        public void SetTurnHeaderVisible(bool visible) => turnHeader.gameObject.SetActive(visible);

        /// <summary>The commit seal's word: <see cref="CommitLabel"/> in the campaign's battles; versus finalizes the whole
        /// queue with 예약 확정. Null or empty goes back to
        /// <see cref="CommitLabel"/>. <see cref="Reset"/> keeps it.</summary>
        public void SetCommitLabel(string label)
        {
            if (disposed) return;
            commitLabel.text = string.IsNullOrEmpty(label) ? CommitLabel : label;
        }

        /// <summary>The commit seal's word now.</summary>
        public string CommitLabelText => commitLabel.text;

        /// <summary>Holds the planning inputs while the duel waits on someone else (versus: the opponent's reservation turn,
        /// or a request the host has not confirmed yet). The dock stays up, its timer showing whoever's clock runs, and a
        /// held lane still opens its explanation; the lanes, 넘기기, 숨고르기 and the commit seal dim and take no clicks.
        /// <see cref="Reset"/> lets them go.</summary>
        public void SetInputsHeld(bool held)
        {
            if (disposed || inputsHeld == held) return;
            inputsHeld = held;
            ApplyInputAvailability();
        }

        /// <summary>Whether the planning inputs are held (<see cref="SetInputsHeld"/>).</summary>
        public bool InputsHeld => inputsHeld;

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
            UpdateTimerPresentation();
            UpdateStageLabel();
            if (!enabled) SetGuideFocus(-1, false, false, false);
        }

        /// <summary>Training keeps normal skill availability, but has no planning clock or stage numbering.</summary>
        public void SetTrainingMode(bool enabled)
        {
            if (disposed || trainingMode == enabled) return;
            trainingMode = enabled;
            UpdateTimerPresentation();
            UpdateStageLabel();
        }

        private void UpdateTimerPresentation()
        {
            bool showTimer = !trainingMode && (!missionMode || missionTimed);
            timerTrack.gameObject.SetActive(showTimer);
            timerFill.gameObject.SetActive(showTimer);
            SetTimerGearVisible(showTimer);
            timeText.gameObject.SetActive(showTimer);
            untimedHint.text = trainingMode ? "수련 · 시간 제한 없음" : "임무 · 시간 제한 없음";
            untimedHint.gameObject.SetActive(!showTimer);
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

        /// <summary>넘기기 is shown when the duel allows it and the coach, if any, has reached its lesson or free play.</summary>
        private bool CycleShown => displayedSession != null && displayedSession.Features.Has(CombatFeature.Cycle) &&
            (guide == null || guide.AllowsCycle);

        private bool CanCycle()
            => !disposed && cycleLanes != null && lastPlanning && !inputsHeld && CycleShown && inspectedSlot < 0 &&
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

        /// <summary>넘기기 (or a reservation's 제한시간 cost) just spent planning time: a short '-N초' rises off the timer.</summary>
        public void ShowTimeSpent(float seconds)
        {
            if (disposed || seconds <= 0f) return;
            timeSpent.text = "-" + seconds.ToString("0.#") + "초";
            timeSpentInk = DuelVisualTheme.Danger;
            timeSpentRemaining = TimeSpentDuration;
            AdvanceTimeSpent(0f);
        }

        /// <summary>Whether the '-N초' from the last 넘기기 (or the turn's '+N초') is still on screen.</summary>
        public bool IsShowingTimeSpent => timeSpentRemaining > 0f;

        /// <summary>The rising time text ('-1초', '+4초') while it shows, or null.</summary>
        public string TimeSpentText => disposed || timeSpentRemaining <= 0f ? null : timeSpent.text;

        private static string CycleCostText(float seconds) => seconds > 0f ? seconds.ToString("0.#") + "초 소모" : "무료";

        /// <summary>With a single lane there is only its front skill to send back; otherwise every open lane turns.</summary>
        private static string CycleEffectText(CombatFeature openLanes) => openLanes.LaneCount() == 1 ? "맨 앞 한 칸" : "모든 열 한 칸";

        private void AdvanceTimeSpent(float realDelta)
        {
            timeSpentRemaining = Mathf.Max(0f, timeSpentRemaining - Mathf.Max(0f, realDelta));
            bool visible = timeSpentRemaining > 0f;
            if (timeSpent.gameObject.activeSelf != visible) timeSpent.gameObject.SetActive(visible);
            if (!visible) return;
            float t = 1f - timeSpentRemaining / TimeSpentDuration;
            timeSpent.rectTransform.anchoredPosition = TimeSpentHome + new Vector2(0f, 12f * t);
            Color color = timeSpentInk;
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
            // The button already names its cost and effect. The tiny repeated note crowded the compact desk.
            cycleNote.gameObject.SetActive(false);
            cycleButton.interactable = CanCycle();
            bool breathShown = BreathShown;
            breathButton.gameObject.SetActive(breathShown);
            breathNote.gameObject.SetActive(false);
            breathButton.interactable = CanQueueBreath();
            bool allowCommit = guide == null || guide.AllowsCommit;
            commitButton.interactable = lastPlanning && !inputsHeld && allowCommit;
            guideCommitGroup.alpha = !allowCommit ? .4f : inputsHeld ? HeldInputAlpha : 1f;
            for (int lane = 0; lane < laneButtons.Length; lane++)
            {
                bool allowLane = guide == null || guide.AllowsQueue(lane);
                laneButtons[lane].interactable = lastPlanning && !inputsHeld && laneAffordable[lane] && allowLane;
                SetLaneFade(lane, !allowLane ? .4f : inputsHeld ? HeldInputAlpha : 1f);
            }
        }

        private bool CanQueueBreath()
            => !disposed && queueBreath != null && lastPlanning && !inputsHeld && BreathShown &&
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
            if (shownBreathsRemaining >= 0 && remaining < shownBreathsRemaining) breathAnimation?.Play();
            shownBreathsRemaining = remaining;
            breathCount.text = $"남은 {remaining}/{LegacyQueuedDuel.MaximumBreathsPerTurn}";
        }

        public void SetGuideFocus(int lane, bool commit, bool enemy, bool act)
        {
            if (disposed) return;
            for (int index = 0; index < guideLaneFocus.Length; index++)
                guideLaneFocus[index].enabled = index == lane;
            guideCommitFocus.enabled = commit;
            guideCommitRing.gameObject.SetActive(commit);
            highlightEnemyQueue = enemy;
            UpdateGuideEnemyFocus();
            guideActFocus.gameObject.SetActive(act);
            actText.color = act ? Accent : Foreground;
        }

        private void UpdateStageLabel()
        {
            stageText.text = stageOverride ?? (trainingMode ? $"수련  ·  {stageName}"
                : missionMode ? $"임무 {stageNumber:00} / {stageCount:00}  ·  {stageName}"
                : $"{stageNumber:00} / {stageCount:00}  ·  {stageName}");
        }

        private void UpdateGuideEnemyFocus()
        {
            Vector2 size = enemyQueue.DisplaySize;
            guideEnemyFocus.gameObject.SetActive(highlightEnemyQueue && size != Vector2.zero);
            guideEnemyFocus.sizeDelta = size + Vector2.one * 16f;
            // Around the row, which grows outward from the centre seam (its root) rather than centring on it.
            Vector2 span = enemyQueue.HorizontalSpan;
            guideEnemyFocus.anchoredPosition = new Vector2((span.x + span.y) * .5f, 0f);
        }

        /// <param name="session">The duel as the player sees it: a <see cref="LegacyQueuedDuel"/>, or a versus match from one
        /// player's side (<see cref="LocalVersusPerspective"/>).</param>
        public void Refresh(ILegacyDuelView session, float timeRemaining, bool isResolving, int currentSlot,
            Camera camera, Transform player, Transform enemy, float delta = 0, float realDelta = -1,
            float planningDuration = 10f)
        {
            if (disposed || session == null) return;
            displayedSession = session;
            worldCamera = camera;
            delta = Mathf.Max(0, delta);
            float actualDelta = realDelta < 0 ? delta : Mathf.Max(0, realDelta);
            transition = Mathf.MoveTowards(transition, targetTransition, delta * 2);
            inputPanel.sizeDelta = new Vector2(DockWidth, DockHeight * (1 - OutQuad(transition)));
            cinematicElapsed = Mathf.Min(.3f, cinematicElapsed + actualDelta);
            cinematic = Mathf.Lerp(cinematicFrom, cinematicTarget, OutQuad(cinematicElapsed / .3f));
            UpdateCombatLetterbox(session.Phase == LegacyDuelPhase.Planning && !isResolving, isResolving, actualDelta);
            LayoutCinematicBands();
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
            float ratio = Mathf.Clamp01(timeRemaining / Mathf.Max(0.01f, planningDuration));
            timerFill.fillAmount = ratio;
            bool timeLow = ratio <= .3f;
            timerFill.color = timeLow ? DuelVisualTheme.Danger : Color.white;
            timeText.color = timeLow ? DuelVisualTheme.Danger : Foreground;
            AdvanceTimerGear(timeRemaining, planningDuration, ratio, planning, actualDelta);
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeRemaining));
            if (shownSeconds != seconds)
            {
                shownSeconds = seconds;
                timeText.text = $"{seconds}s";
            }
            actFill.fillAmount = Mathf.Clamp01(session.Act / (float)session.PlayerMaximumAct);
            if (shownAct != session.Act || shownMaxAct != session.PlayerMaximumAct)
            {
                shownAct = session.Act;
                shownMaxAct = session.PlayerMaximumAct;
                actText.text = $"{shownAct} / {shownMaxAct} ACT";
            }
            if (shownTurn != session.RoundNumber)
            {
                shownTurn = session.RoundNumber;
                turnText.text = shownTurn.ToString();
            }
            LayoutGears(session.Features);
            for (int lane = 0; lane < 3; lane++)
            {
                var sequence = session.GetLane(lane);
                // A lane the duel does not have (e.g. W/E in the Q-only opening missions) is not drawn at all.
                bool present = sequence.Count > 0;
                SetGearPresent(lane, present);
                if (!present)
                {
                    laneAffordable[lane] = false;
                    cycleMarks[lane].gameObject.SetActive(false);
                    HideLanePreview(lane);
                    HideLaneTemporaryMarks(lane);
                    continue;
                }
                var current = sequence[0];
                int currentCost = session.EffectiveCost(current);
                if (shownSkills[lane] != current.Id || skillNames[lane].text != current.Name)
                {
                    shownSkills[lane] = current.Id;
                    skillNames[lane].text = current.Name;
                }
                gears[lane].Role.SetSkill(current);
                // The cost (with a reservation's planning time while the clock runs) and the 일회성 tags; a lane greys when
                // either ACT or the clock cannot pay.
                laneAffordable[lane] = UpdateLaneTemporaryAndTime(lane, sequence, currentCost, session.Act >= currentCost);
                LegacySkillEffect effect = LegacySkillDefinitions.Find(current)?.Effect;
                bool hasCycle = effect?.HasCycle == true;
                cycleMarks[lane].gameObject.SetActive(hasCycle);
                if (hasCycle)
                {
                    string countText = "순환 " + session.CycleUses(current.Id) + "/" + effect.CycleMaxCount;
                    if (cycleCounts[lane].text != countText) cycleCounts[lane].text = countText;
                }
                // What reserving it now would do, for a skill whose effect reads the queue.
                UpdateLanePreview(lane, session, current, hasCycle);
                FollowLaneOrder(lane, sequence, laneAffordable[lane]);
            }
            UpdateIdlerPresence();
            AdvanceLaneTurns(actualDelta);
            cycleAnimation?.Advance(actualDelta);
            breathAnimation?.Advance(actualDelta);
            AdvanceTimeSpent(actualDelta);
            AdvanceLanePops(actualDelta);
            AdvanceHoldCancel(actualDelta);
            UpdateBreathCount();
            UpdateAutoBreathCount();
            ApplyInputAvailability();
            if (slotAnimation) slotElapsed += delta;
            float activeScale = ActiveIconScale();
            // Mark each queued attack the opponent's counter will answer, forecast at planning.
            session.ForecastEnemyCounterSlots(counterForecast);
            playerQueue.SetCounterMarks(counterForecast);
            session.ForecastPlayerCounterSlots(counterForecast);
            enemyQueue.SetCounterMarks(counterForecast);
            playerStatus.SetCounter(session.PlayerCounter, session.PlayerCountersRemaining);
            enemyStatus.SetCounter(session.EnemyCounter, session.EnemyCountersRemaining);
            SetMeshMarks(session);
            SetQueuePowerMarks(session);
            // The player's own schools are always open; the enemy's cards are dyed once all three are (as its Tab badge).
            enemyQueue.SetSchoolsNamed(session.Features.LaneCount() == 3);
            UpdateSlotForecast(session, planning);
            ApplyQueueCardSize();
            // The clash motion reads the resolving slot before the rows hide a slot just completed (its cards' copies
            // play out the rest of their meeting; the turn's last slot stays shown and plays it on its own cards).
            AdvanceQueueClash(session, isResolving, currentSlot, delta);
            playerQueue.Refresh(session.PlayerQueue, iconFor, currentSlot, isResolving,
                queueComparisonVisible ? inspectedSlot : -1, activeScale, session);
            enemyQueue.Refresh(session.EnemyQueue, iconFor, currentSlot, isResolving, inspectedSlot, activeScale, session);
            // Both rows in their panels over the dock, sliding one step toward the seam after each resolved slot.
            LayoutQueuePanels(isResolving, currentSlot, delta);
            RefreshCombatReadout(session);
            if (QueuePanelKeepsFightersClear && CombatReadoutTop > 0f)
                queuePanelFloor = Mathf.Max(queuePanelFloor, Mathf.Clamp01((CombatReadoutTop + QueuePanelFighterClearance) /
                    (root.rect.height > 1f ? root.rect.height : 1080f)));
            // The ring and socket where the next reservation meets the opponent, and the reservation sparks.
            UpdateQueueCues(session, planning, actualDelta);
            // The 간파 cue follows the player's card where the rows now have it, on real time.
            UpdateReadCue(actualDelta);
            UpdateConditionPreview();
            UpdateEffectEmphasis();
            playerQueue.TickFeedback(actualDelta);
            enemyQueue.TickFeedback(actualDelta);
            AdvanceQueueCancellations(actualDelta);
            foreach (var feedback in laneFeedback) feedback.Tick(actualDelta);
            playerEffectFeedback.Tick(actualDelta);
            enemyEffectFeedback.Tick(actualDelta);
            PositionFighterHud(playerStatus, player, camera, new Vector3(-.6f, 1.1f, 0),
                ref playerHeadStack, ref playerHeadX);
            bool rosterShown = RefreshEnemyRoster(session, player, enemy, camera, delta);
            enemyStatus.Root.gameObject.SetActive(!rosterShown);
            if (!rosterShown)
                PositionFighterHud(enemyStatus, enemy, camera, new Vector3(-.4f, 1f, 0),
                    ref enemyHeadStack, ref enemyHeadX);
            // After the rows were laid out in their panel.
            UpdateGuideEnemyFocus();
            playerStatus.Refresh(session.Player, delta);
            enemyStatus.Refresh(session.Enemy, delta);
            RefreshFighterStatuses(session, player, enemy, camera);
            UpdateDamage(actualDelta);
            UpdateLog(delta);
        }

        public void BeginTurn()
        {
            ResetCombatReadout();
            ClearHoldCancel();
            ClearBreakQueueFragments();
            ForgetUsedTemporarySkills();
            targetTransition = transition = 0;
            cinematic = cinematicFrom = cinematicTarget = 0;
            cinematicElapsed = .3f;
            combatLetterboxRequest = combatLetterboxAccent = 0f;
            LayoutCinematicBands();
            viewAngle = angleFrom = angleTarget = 0;
            angleElapsed = .15f;
            slotAnimation = false;
            inspectedSlot = -1;
            ClearSkillFeedback();
            HideExplanation();
            ResetQueueCues();
        }

        public void EndTurn()
        {
            ClearHoldCancel();
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

        /// <summary>Uses the immutable combat snapshot; presentation never evaluates or consumes a buff. The feedback stays
        /// while <paramref name="slot"/> is the duel's current slot (the same object, <see cref="ILegacyDuelView.CurrentSlot"/>).</summary>
        public void SetSkillFeedback(ILegacySlotView slot)
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

        /// <summary>A released explanation attempt between a tap and the full hold was safely cancelled.</summary>
        public void ShowHoldCancel(int lane)
        {
            if (disposed || lane < 0 || lane >= holdCancelGroups.Length || holdCancelGroups[lane] == null) return;
            holdCancelRemaining[lane] = HoldCancelSeconds;
            holdCancelGroups[lane].alpha = 1f;
            holdCancelGroups[lane].gameObject.SetActive(true);
        }

        public void ClearHoldCancel(int lane = -1)
        {
            if (lane >= holdCancelGroups.Length) return;
            int first = lane < 0 ? 0 : lane, last = lane < 0 ? holdCancelGroups.Length : lane + 1;
            for (int i = first; i < last; i++)
            {
                holdCancelRemaining[i] = 0f;
                if (holdCancelGroups[i] != null) holdCancelGroups[i].gameObject.SetActive(false);
            }
        }

        private void AdvanceHoldCancel(float realDelta)
        {
            for (int lane = 0; lane < holdCancelGroups.Length; lane++)
            {
                if (!(holdCancelRemaining[lane] > 0f)) continue;
                holdCancelRemaining[lane] = Mathf.Max(0f, holdCancelRemaining[lane] - realDelta);
                if (holdCancelRemaining[lane] <= 0f) ClearHoldCancel(lane);
                else holdCancelGroups[lane].alpha = Mathf.Clamp01(holdCancelRemaining[lane] / .16f);
            }
        }

        /// <summary>A card the controller keeps up over the dock, by its top on the screen (pixels from the bottom; 0 for
        /// none): the coach's (<see cref="MissionCoachHud.TopEdge"/>). A held skill's explanation then opens above it rather
        /// than under it, so the card never covers its effect text. Read on each <see cref="ShowExplanation"/>.</summary>
        public void SetExplanationFloor(float screenTop)
            => explanationFloor = screenTop > 0f && !float.IsInfinity(screenTop) ? screenTop : 0f;

        public void ShowExplanation(LegacySkill skill, bool enemy, CampaignOwnedSkill owned = null)
        {
            if (disposed || skill == null) return;
            HideQueueComparison();
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
                // Schools are not named until the player has opened all three, so the badges stay alike and never
                // hint at a school still to come: until then the badge only says whose skill it is.
                bool named = displayedSession == null || displayedSession.Features.LaneCount() == 3;
                if (named) enemyStyle.SetLane(skill.LaneIndex, false);
                else enemyStyle.Clear(EnemySkillCaption);
                enemyExplanationHint.text = named ? EnemyHint : NeutralEnemyHint;
                enemyExplanationIcon.sprite = iconFor(skill.IconId);
                SetEnemyInfoSkill(skill);
                UpdateEnemyForecastNote();
                LayoutExplanation(enemyExplanation, enemyExplanationIcon, enemyName, enemyStyle, enemyInfo, enemyExplanationHint, 1f,
                    forecast: enemyForecastNote);
            }
            else if (changed)
            {
                explainedPlayer = skill;
                playerName.text = skill.Name;
                playerStyle.SetLane(skill.LaneIndex);
                playerExplanationIcon.sprite = iconFor(skill.IconId);
                SetPlayerInfoSkill(skill, owned);
                UpdateMeshNote(skill);
                UpdateKeywordNote(skill);
                UpdatePlayerForecastNote(skill);
                LayoutExplanation(playerExplanation, playerExplanationIcon, playerName, playerStyle, playerInfo, playerExplanationHint,
                    PlayerExplanationScale, playerMeshNote, playerForecastNote, playerKeywordNote);
            }
            else if (enemy)
            {
                // A versus opponent's 순환 count, cost and power follow its uses while the card stays open, and the same
                // skill comes back on later turns; the card grows only when its info does.
                // Its 예상 line follows the inspected slot (two slots may hold the same skill).
                float height = enemyInfo.Height;
                SetEnemyInfoSkill(skill);
                if (UpdateEnemyForecastNote() | !Mathf.Approximately(height, enemyInfo.Height))
                    LayoutExplanation(enemyExplanation, enemyExplanationIcon, enemyName, enemyStyle, enemyInfo, enemyExplanationHint, 1f,
                        forecast: enemyForecastNote);
            }
            else
            {
                if (playerName.text != skill.Name) playerName.text = skill.Name;
                SetPlayerInfoSkill(skill, owned);
                // 맞물림's note and the 예상 line follow the queue, ACT and 넘기기 while the skill is held; the card grows only
                // when a row comes or goes.
                if (UpdateMeshNote(skill) | UpdateKeywordNote(skill) | UpdatePlayerForecastNote(skill))
                    LayoutExplanation(playerExplanation, playerExplanationIcon, playerName, playerStyle, playerInfo, playerExplanationHint,
                        PlayerExplanationScale, playerMeshNote, playerForecastNote, playerKeywordNote);
            }
            // Clamp after content sizing, including repeated holds at a moving screen edge.
            Vector2 desired = root.rect.size / 2 + new Vector2(-480, 160);
            if (!enemy)
            {
                // Above the dock and the queue panels over it (with the tabs over their cards), centred on the held lane's
                // window (where its current skill rests, so the panel stays put while the gear turns the icon in): clear of
                // the gears and of the queues it is read against (the enemy card it would face is framed there), and over
                // the fighters' status panels. Above the coach's card too while one is up over the dock: it draws over this
                // HUD and would hide the effect text.
                int lane = Mathf.Clamp(skill.LaneIndex, 0, 2);
                Vector2 window = ScreenPosition(RectTransformUtility.WorldToScreenPoint(null, gears[lane].Window.position));
                float floor = Mathf.Max(Mathf.Max(DockTop, queueRowTop), ScreenPosition(new Vector3(0f, explanationFloor, 0f)).y);
                desired = new Vector2(window.x, floor + ExplanationGap + panel.sizeDelta.y / 2f);
            }
            panel.anchoredPosition = ClampCenter(desired,
                panel.sizeDelta + Vector2.right * ((enemy ? enemyInfo : playerInfo).Overhang * 2f)) - root.rect.size / 2;
            conditionPreview = !enemy && lastPlanning ? skill : null;
            UpdateConditionPreview();
            UpdateEffectEmphasis();
        }

        private void SetPlayerInfoSkill(LegacySkill skill, CampaignOwnedSkill owned)
            => SetPlayerInfoSkill(playerInfo, skill, owned);

        private void SetPlayerInfoSkill(SkillInfoView info, LegacySkill skill, CampaignOwnedSkill owned)
        {
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            if (displayedSession == null || effect?.HasCycle != true)
            {
                info.SetSkill(skill, owned: owned);
                return;
            }

            displayedSession.EffectivePowerRange(skill, out int min, out int max);
            string power = min == max ? min.ToString() : min + "–" + max;
            info.SetSkill(skill, owned: owned, cycleUses: displayedSession.CycleUses(skill.Id),
                effectiveCost: displayedSession.EffectiveCost(skill), effectivePower: power);
        }

        // The enemy's card reads the enemy's own uses of a cycling skill, as the player's reads the player's: a versus
        // opponent cycles too. The campaign's scripted enemy never does, so its card shows the sheet's cost and power.
        private void SetEnemyInfoSkill(LegacySkill skill)
        {
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            bool playerOnlyAct = !(displayedSession is LocalVersusPerspective);
            if (displayedSession == null || effect?.HasCycle != true)
            {
                enemyInfo.SetSkill(skill, true, enemyActRecoveryIsPlayerOnly: playerOnlyAct);
                return;
            }

            displayedSession.EnemyEffectivePowerRange(skill, out int min, out int max);
            string power = min == max ? min.ToString() : min + "–" + max;
            enemyInfo.SetSkill(skill, true, cycleUses: displayedSession.EnemyCycleUses(skill.Id),
                effectiveCost: displayedSession.EnemyEffectiveCost(skill), effectivePower: power,
                enemyActRecoveryIsPlayerOnly: playerOnlyAct);
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
            // A 상대 상태 조건 (붕괴, 체력 N% 이하) reads the enemy as it stands, like the self condition reads the player;
            // the sheet never pairs it with an opponent condition, and it marks no enemy card.
            bool stateCondition = !opponentCondition && LegacySkillConditions.HasOpponentStateCondition(skill);
            bool ready = displayedSession != null && (opponentCondition
                ? opponent != null && LegacySkillConditions.MatchesOpponent(skill, opponent)
                : stateCondition ? LegacySkillConditions.MatchesOpponentState(skill, displayedSession.Player, displayedSession.Enemy)
                : LegacySkillConditions.MatchesSelfCondition(skill, displayedSession.Player));
            for (int lane = 0; lane < laneFeedback.Length; lane++)
                laneFeedback[lane].SetState(false, ready && skill != null && skill.LaneIndex == lane, false);
            playerExplanationHint.text = ready ? opponentCondition ? "지금 예약하면 조건 충족 · 놓으면 닫기"
                    : stateCondition ? "현재 상대 상태: 조건 충족 · 놓으면 닫기" : "현재 저항: 회복 가능 · 놓으면 닫기"
                : opponentCondition ? "테두리 친 상대 기술에 맞춰 예약 · 놓으면 닫기"
                : stateCondition ? StateConditionHint(skill) : "키를 놓으면 닫기";
            if (lastPlanning) playerEffectFeedback.SetState(false, ready, false);
        }

        // "조건: 상대 체력 30% 이하 · 놓으면 닫기", formatted once per inspected skill since the hint refreshes every frame.
        private string StateConditionHint(LegacySkill skill)
        {
            if (!ReferenceEquals(stateHintSkill, skill))
            {
                LegacySkillEffect effect = LegacySkillDefinitions.Find(skill).Effect;
                stateHintSkill = skill;
                stateHint = "조건: 상대 " + LegacySkillLabels.OpponentState(effect.OpponentState, effect.OpponentHealthPercent) +
                    " · 놓으면 닫기";
            }
            return stateHint;
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

        /// <summary>Where a held skill's explanation's bottom may sit: the dock's top, in HUD units from the screen's bottom.</summary>
        private float DockTop => ScreenPosition(RectTransformUtility.WorldToScreenPoint(null,
            inputPanel.TransformPoint(new Vector3(0f, inputPanel.rect.yMax, 0f)))).y;

        // A type size of the held explanation (PlayerExplanationScale times the enemy's).
        private static int ExplanationType(int points) => Mathf.RoundToInt(points * PlayerExplanationScale);

        /// <param name="note">The held explanation's 맞물림 row over the hint (<see cref="MeshNoteText"/>), taking room only while
        /// it shows.</param>
        /// <param name="forecast">The 예상 row (<see cref="ForecastNote"/>) over those, likewise.</param>
        /// <param name="keyword">The keyword row (<see cref="KeywordNote"/>) between the 맞물림 row and the 예상 row, likewise.</param>
        private static void LayoutExplanation(RectTransform panel, Image icon, Text name, SkillLaneBadge style, SkillInfoView info, Text hint,
            float scale, RectTransform note = null, RectTransform forecast = null, RectTransform keyword = null)
        {
            bool noted = note != null && note.gameObject.activeSelf;
            bool forecasted = forecast != null && forecast.gameObject.activeSelf;
            bool keyed = keyword != null && keyword.gameObject.activeSelf;
            float height = (90f + 36f + (noted ? MeshNoteRow : 0f) + (keyed ? KeywordNoteRow : 0f) +
                (forecasted ? ForecastNoteRow : 0f)) * scale + info.Height;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, height);
            float top = height / 2f;
            icon.rectTransform.anchoredPosition = new Vector2(-146f * scale, top - 50f * scale);
            icon.rectTransform.sizeDelta = Vector2.one * 64f * scale;
            name.rectTransform.anchoredPosition = new Vector2(20f * scale, top - 36f * scale);
            name.rectTransform.sizeDelta = new Vector2(244f, 32f) * scale;
            style.Root.anchoredPosition = new Vector2(-30f * scale, top - 65f * scale);
            info.PlaceTop(top - 90f * scale);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -top + 16f * scale);
            if (note != null) note.anchoredPosition = new Vector2(0f, -top + (16f + MeshNoteRow) * scale);
            if (keyword != null)
                keyword.anchoredPosition = new Vector2(0f, -top + (16f + (noted ? MeshNoteRow : 0f) + KeywordNoteRow) * scale);
            if (forecast != null)
                forecast.anchoredPosition = new Vector2(0f, -top + (16f + (noted ? MeshNoteRow : 0f) + (keyed ? KeywordNoteRow : 0f) +
                    ForecastNoteRow) * scale);
        }

        private static void FitExplanationLabel(Text label, int minSize)
        {
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = minSize;
            label.resizeTextMaxSize = label.fontSize;
        }

        public void HideExplanation()
        {
            HideQueueComparison();
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
        public void ShowHitDamage(bool targetPlayer, int damage, Vector3 worldPosition, bool critical = false, bool resistance = false,
            LegacyDamageReason reasons = LegacyDamageReason.None, bool resistanceBroke = false, LegacySkill attacker = null,
            bool mixedResources = false, bool bleeding = false)
        {
            if (disposed || worldCamera == null) return;
            damage = Mathf.Max(0, damage);
            if (bleeding && damage == 0) return;
            if (resistanceBroke && damage == 0) return; // The break callout is the result, rather than a large zero HP hit.
            DamageView view = AcquireDamageView();
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
            if (bleeding) view.Text.fontSize = Mathf.Max(24, Mathf.RoundToInt(view.Text.fontSize * .65f));
            string recipient = targetPlayer ? "내" : "상대";
            if (!targetPlayer && displayedSession is ILegacyEnemyRosterView roster && roster.Enemies.Count > 1)
                recipient += " " + (roster.ActiveEnemyIndex + 1);
            string reason = bleeding ? "출혈" : damage == 0 && (reasons & LegacyDamageReason.GuardReduction) != 0
                ? "완전 방어" : LegacyDamageReasonText.Primary(reasons, attacker);
            view.Detail.text = recipient + " " + (mixedResources ? "체력·저항" : resistance ? "저항" : "체력") +
                (string.IsNullOrEmpty(reason) ? string.Empty : " · " + reason);
            view.Detail.rectTransform.anchoredPosition = new Vector2(0f, -(view.Text.fontSize * .5f + 24f));
            view.Duration = view.Remaining = bleeding ? .9f : critical ? 1.25f : 1.05f;
            view.Scale = (.8f + Mathf.Clamp01(damage / 24f) * .25f) *
                (critical ? (presentationSettings != null ? presentationSettings.CriticalDamageScale : 1.3f) : 1f);
            view.Color = bleeding ? new Color(1f, .36f, .42f) : resistance ? ResistanceDamageInk : critical ? new Color(1f, .87f, .42f) : new Color(1f, .98f, .89f);
            view.Outline.effectColor = bleeding ? new Color(.3f, .015f, .035f) : resistance ? ResistanceDamageOutline
                : critical ? new Color(.78f, .1f, .04f) : new Color(.64f, .055f, .055f);
            view.Text.gameObject.SetActive(true);
            view.Text.transform.SetAsLastSibling();
            ApplyDamageFrame(view);
        }

        /// <summary>Fixed self-inflicted HP loss. It uses the damage pool but never looks like a guarded weapon hit.</summary>
        public void ShowBleedingDamage(bool targetPlayer, int damage, Vector3 worldPosition)
            => ShowHitDamage(targetPlayer, damage, worldPosition, bleeding: true);

        /// <summary>Calls out a new resistance break, distinct from a later doubled HP hit.</summary>
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
            // On the side facing the opponent, below the head status panel and away from
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
                Text detail = null;
                if (name == "Damage")
                {
                    detail = Text("Damage Detail", text.transform, new Vector2(0f, -108f), new Vector2(640f, 48f), 32,
                        TextAnchor.MiddleCenter);
                    detail.supportRichText = false;
                    detail.fontStyle = FontStyle.Bold;
                    detail.resizeTextForBestFit = true;
                    detail.resizeTextMinSize = 24;
                    detail.resizeTextMaxSize = 32;
                    detail.verticalOverflow = VerticalWrapMode.Truncate;
                    var edge = detail.gameObject.AddComponent<Outline>();
                    edge.effectColor = new Color(.08f, .025f, .02f, .95f);
                    edge.effectDistance = new Vector2(1.5f, -1.5f);
                    edge.useGraphicAlpha = true;
                }
                var created = new DamageView(text, outline, detail);
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
            ClearFighterStatuses();
            SetTrainingMode(false);
            SetMissionMode(false);
            SetEnemyName(null);
            SetGuideFocus(-1, false, false, false);
            shownTurn = shownSeconds = shownAct = shownMaxAct = -1;
            displayedSession = null;
            // The next duel may open other lanes, so a skill explained again is shown afresh (its badge may change).
            explainedPlayer = explainedEnemy = null;
            ResetMeshNote();
            ResetKeywordNote();
            ResetTemporaryMarks();
            ResetForecast();
            lastPlanning = false;
            inputsHeld = false;
            shownBreathsRemaining = -1;
            cycleAnimation?.Reset();
            breathAnimation?.Reset();
            UpdateBreathCount();
            ApplyInputAvailability();
            for (int i = 0; i < 3; i++) shownSkills[i] = -1;
            ResetGears();
            ResetTimerGear();
            timeSpentRemaining = 0f;
            AdvanceTimeSpent(0f);
            playerStatus.Reset(); enemyStatus.Reset();
            ResetEnemyRoster();
            ClearQueueCancellations();
            ClearBreakQueueFragments();
            playerQueue.Clear(); enemyQueue.Clear();
            ResetQueuePanels();
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
            ReleaseGearPictures();
            ReleaseMeshPictures();
            ReleaseTimerGearPictures();
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
            if (damage.Detail != null) damage.Detail.color = color;
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

        /// <param name="stack">Set to the status panel laid out (<see cref="TryGetHeadStack"/>); kept when nothing could be.</param>
        /// <param name="headX">Set to the panel's centre, over the fighter's head.</param>
        private void PositionFighterHud(StatusView status, Transform actor, Camera camera, Vector3 headAnchor,
            ref Rect stack, ref float headX)
        {
            if (actor == null || camera == null) return;
            // Stable local attachment points match the inherited idle sprites.
            // Project once, after arena/camera movement; never lag behind the actor.
            var screen = camera.WorldToScreenPoint(actor.TransformPoint(headAnchor));
            Camera hudCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, hudCamera, out var position)) return;
            var halfCanvas = root.rect.size / 2;
            var statusSize = status.Root.sizeDelta;
            // Only the status panel rides the head now (2026-10-15): the queues stand in their panels over the dock. It is
            // clamped on its own, a constant 12 over the head even while Tab focuses an off-centre actor.
            position += halfCanvas + Vector2.up * (statusSize.y / 2 + 12);
            position = ClampCenter(position, statusSize) - halfCanvas;
            status.Root.anchoredPosition = position;
            // What sits above it (the speech bubbles) keeps clear of the panel.
            stack = UnityEngine.Rect.MinMaxRect(position.x - statusSize.x / 2, position.y - statusSize.y / 2,
                position.x + statusSize.x / 2, position.y + statusSize.y / 2);
            headX = position.x;
        }

        /// <summary>Where a fighter's head HUD was last laid out (<see cref="Refresh"/>): its status panel (with any buff rows),
        /// in the HUD root's local units (centre origin, y up), and the panel's centre, over the fighter's head. False before
        /// the first layout. The speech bubbles (<see cref="DuelBarks"/>) keep clear of it. The queues no longer ride the
        /// heads; they stand in their panels over the dock (<see cref="PlayerQueuePanel"/>).</summary>
        public bool TryGetHeadStack(bool playerSide, out Rect stack, out float headX)
        {
            stack = playerSide ? playerHeadStack : enemyHeadStack;
            headX = playerSide ? playerHeadX : enemyHeadX;
            return !disposed && stack.width > 0f;
        }

        /// <summary>A queue card's built size, HUD units at the 1920x1080 reference; the panels draw it at the tuned size
        /// (<see cref="QueueCardWidth"/>, <see cref="DuelPresentationSettings.QueueCardSize"/>).</summary>
        public const float QueueCardSize = 64f;

        /// <summary>The most the resolving slot's card is enlarged over its scale at rest (its pulse is clamped to it).</summary>
        public const float QueueActiveCardMaxScale = 1.2f;

        // A queue card's icon and the width of its dyed ring, at the built card size (a clash copy is built the same).
        private const float QueueIconSize = 48f, QueueCardRing = 3f;

        /// <summary>The enemy's queue row as last laid out (<see cref="Refresh"/>): <paramref name="row"/> is the cards'
        /// parent, at the centre seam over the dock and level with the cards, and <paramref name="span"/> the row's left and
        /// right edges relative to it (slot 1, the next to come, at the left: the inner end, by the seam). False while the
        /// row shows no card. The gear shimmer over a revealed queue (<see cref="DuelGearShimmer"/>) plays beside it.</summary>
        public bool TryGetEnemyQueueRow(out RectTransform row, out Vector2 span)
        {
            row = disposed ? null : enemyQueue.Root;
            span = enemyQueue.HorizontalSpan;
            return !disposed && enemyQueue.DisplaySize.x > 0f;
        }

        private StatusView BuildStatus(bool enemy)
        {
            var status = Panel(enemy ? "EnemyStatus" : "PlayerStatus", root,
                Vector2.zero, new Vector2(StatusWidth, 92)).rectTransform;
            var white = Load("white");
            var result = new StatusView(status);
            var healthLabel = Text("HP Label", status, new Vector2(0, 27), new Vector2(196, 18), 14, TextAnchor.MiddleLeft);
            healthLabel.text = "HP"; healthLabel.color = MutedText;
            var resistanceLabel = Text("Resistance Label", status, new Vector2(0, -4), new Vector2(196, 18), 14, TextAnchor.MiddleLeft);
            resistanceLabel.text = "저항"; resistanceLabel.color = MutedText;
            if (enemy)
            {
                // Who the player fights, across from the HP label (SetEnemyName); stages leave it empty.
                result.Name = Text("Battle Name", status, new Vector2(0, 27), new Vector2(196, 18), 14, TextAnchor.MiddleRight);
                result.Name.color = Foreground;
                result.Name.supportRichText = false;
                result.Name.text = string.Empty;
            }
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

        private void UpdateCombatLetterbox(bool planning, bool isResolving, float realDelta)
        {
            float target = isResolving && cinematicTarget > 0f ? combatLetterboxRequest : 0f;
            combatLetterboxRequest = 0f;
            if (planning)
            {
                combatLetterboxAccent = 0f;
                return;
            }
            // Camera accents can keep unfolding through hit stop. Pause does not call Refresh at all.
            float seconds = target > combatLetterboxAccent ? .12f : .25f;
            float blend = 1f - Mathf.Exp(-3f * realDelta / seconds);
            combatLetterboxAccent = Mathf.Lerp(combatLetterboxAccent, target, blend);
            if (Mathf.Abs(combatLetterboxAccent - target) < .001f) combatLetterboxAccent = target;
        }

        private void LayoutCinematicBands()
        {
            float offset = 64f * (1f - cinematic) - CombatLetterboxInset;
            topBar.anchoredPosition = new Vector2(0f, offset);
            bottomBar.anchoredPosition = new Vector2(0f, -offset);
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

        private const float ScreenMargin = 16f;

        private Vector2 ClampCenter(Vector2 center, Vector2 size)
        {
            var inset = size / 2 + Vector2.one * ScreenMargin;
            return new Vector2(Mathf.Clamp(center.x, inset.x, Mathf.Max(inset.x, root.rect.width - inset.x)),
                Mathf.Clamp(center.y, inset.y, Mathf.Max(inset.y, root.rect.height - inset.y)));
        }

        private static void BuildCommandDesk(Image panel)
        {
            panel.color = Color.clear;
            panel.transform.Find("Surface").gameObject.SetActive(false);
            var node = Rect("Command Desk Housing", panel.transform, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            node.anchorMin = Vector2.zero;
            node.anchorMax = Vector2.one;
            node.SetAsFirstSibling();
            node.gameObject.AddComponent<AcademyCommandHousing>().raycastTarget = false;
        }

        private void ConfigureCommandControl(Image hitArea, Button button, string resource, float size,
            Vector2 position, Image icon = null)
        {
            Sprite sprite = Resources.Load<Sprite>(resource);
            assetsAvailable &= sprite != null;
            hitArea.color = Color.clear;
            // Transparent rectangular hit regions must stay in the raycaster even though their mesh is invisible.
            hitArea.canvasRenderer.cullTransparentMesh = false;
            hitArea.transform.Find("Surface").gameObject.SetActive(false);
            if (icon == null) icon = Image("Icon", hitArea.transform, sprite, position, Vector2.one * size);
            else
            {
                icon.sprite = sprite;
                icon.rectTransform.anchoredPosition = position;
                icon.rectTransform.sizeDelta = Vector2.one * size;
            }
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            button.targetGraphic = icon;
            // Tint only the illustrated control. Labels and keys retain their contrast when hovered.
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.08f, 1.02f);
            colors.pressedColor = new Color(.93f, .89f, .82f);
            colors.disabledColor = new Color(.78f, .76f, .70f);
            button.colors = colors;
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
            /// <summary>The enemy panel's name label; null on the player's.</summary>
            public Text Name;
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
                // The persistent icons at the physical feet own status duration. Keep the old row objects dormant
                // for existing references, rather than duplicating a current-slot snapshot above the HP gauges.
                ActivePowerBuff.transform.parent.gameObject.SetActive(false);
                ActiveProtectionBuff.transform.parent.gameObject.SetActive(false);
                GrantedPowerBuff.transform.parent.gameObject.SetActive(false);
                GrantedProtectionBuff.transform.parent.gameObject.SetActive(false);
                Root.sizeDelta = new Vector2(236, 92);
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

        private const float StatusWidth = 236f;

        /// <summary>The cancellation count above the queued power skill, including before its followup is chosen.</summary>
        public string GetQueuePowerSourceMarkLabel(int queueIndex)
            => disposed ? null : playerQueue.GetQueuePowerSourceMarkLabel(queueIndex);

        /// <summary>The multiplier above the next queued skill, or null when no target has been chosen yet.</summary>
        public string GetQueuePowerTargetMarkLabel(int queueIndex)
            => disposed ? null : playerQueue.GetQueuePowerTargetMarkLabel(queueIndex);

        public RectTransform GetQueuePowerSourceMark(int queueIndex)
            => disposed ? null : playerQueue.GetQueuePowerSourceMark(queueIndex);

        public RectTransform GetQueuePowerTargetMark(int queueIndex)
            => disposed ? null : playerQueue.GetQueuePowerTargetMark(queueIndex);

        private void SetQueuePowerMarks(ILegacyDuelView session)
        {
            int source = session.QueuePowerSourceSlotIndex;
            int target = session.QueuePowerTargetSlotIndex;
            LegacySkillEffect effect = source >= 0 && source < session.PlayerQueue.Count
                ? LegacySkillDefinitions.Find(session.PlayerQueue[source])?.Effect : null;
            playerQueue.SetQueuePowerMarks(source, session.QueuePowerRemovedSkillCount,
                target, target < 0 ? 1 : session.QueuePowerMultiplierForSlot(target),
                effect?.QueueRemovalMode ?? LegacyQueueRemovalMode.None);
        }

        /// <summary>A fighter's queue in its panel over the dock (2026-10-15; it rode the fighter's head before): one row, never
        /// wrapping, growing outward from the centre seam. The two first slots face each other across the seam: the player's
        /// slot 1 is its row's right end and the row grows left (read right to left); the enemy's slot 1 is its row's left
        /// end and the row grows right. The HUD lays both rows out together (<see cref="Layout"/>, one spacing, so slot k
        /// stands at the same distance from the seam on both sides).</summary>
        private sealed class QueueView
        {
            private const float DefaultPitch = LegacyQueuePanelLayout.DefaultCardSpacing;
            private readonly List<RectTransform> visibleItems = new List<RectTransform>();
            private readonly List<int> visibleSlots = new List<int>();
            public bool GrowsLeft => player;
            public readonly RectTransform Root;
            public Vector2 DisplaySize { get; private set; }
            /// <summary>The row's left and right edges, relative to its root (at the centre seam).</summary>
            public Vector2 HorizontalSpan { get; private set; }
            /// <summary>The cards shown now (consumed slots hide), in slot order.</summary>
            public int VisibleCount => visibleItems.Count;
            /// <summary>The queue's slots this turn, shown or consumed (as last refreshed).</summary>
            public int QueueLength => displayedQueue?.Count ?? 0;
            /// <summary>The first slot removed by a break, or the queue's end. The resolving skill keeps its whole slot.</summary>
            public int QueueEndSlot { get; private set; }
            /// <summary>The tuned card size over the built one (<see cref="QueueCardSize"/>): the cards' scale at rest.</summary>
            public float CardScale { get; set; } = 1f;
            private readonly bool player;
            // The first shown card's slot (the slots consumed so far), each card's scale at rest as last refreshed, and the
            // clash motion's pose for one card (LegacyCombatHud.Clash.cs): its slot (-1: none), offset, tilt and scale.
            private int firstShown;
            private readonly List<float> restScales = new List<float>();
            private int poseIndex = -1;
            private Vector2 poseOffset;
            private float poseTilt, poseScale = 1f;
            private readonly Sprite white;
            private readonly Font font;
            private readonly List<Image> icons = new List<Image>(), highlights = new List<Image>();
            // Each card's ring: its school's dye (SkillLaneStyle.Edge), brass for 숨고르기 and while the schools go unnamed.
            private readonly List<Image> frames = new List<Image>();
            private bool schoolsNamed = true;
            // 편성 예상 (the player's row only): each card's mark, its fill and sign, and the verdict each slot should show
            // (-1: none).
            private readonly List<RectTransform> forecastMarks = new List<RectTransform>();
            private readonly List<Image> forecastFills = new List<Image>();
            private readonly List<SlotForecastGlyph> forecastGlyphs = new List<SlotForecastGlyph>();
            private readonly List<int> forecastVerdicts = new List<int>();
            private readonly List<SkillCardFeedbackGraphic> feedback = new List<SkillCardFeedbackGraphic>();
            private readonly List<GameObject> counterMarks = new List<GameObject>();
            private readonly List<int> counterSlots = new List<int>();
            // 맞물림 (the player's row only): each card's mark and label, the bonus it shows and its pop still to settle, and
            // the bonus each slot should show.
            private readonly Sprite meshGear;
            private readonly List<RectTransform> meshMarks = new List<RectTransform>();
            private readonly List<Text> meshLabels = new List<Text>();
            private readonly List<int> shownMeshBonus = new List<int>(), meshBonus = new List<int>();
            private readonly List<float> meshPop = new List<float>();
            private readonly List<RectTransform> queuePowerSourceMarks = new List<RectTransform>(), queuePowerTargetMarks = new List<RectTransform>();
            private readonly List<Text> queuePowerSourceLabels = new List<Text>(), queuePowerTargetLabels = new List<Text>();
            private readonly List<RectTransform> cycleMarks = new List<RectTransform>();
            private readonly List<Text> cycleLabels = new List<Text>();
            private readonly List<RectTransform> ownerMarks = new List<RectTransform>();
            private readonly List<Image> ownerFrames = new List<Image>();
            private readonly List<Text> ownerLabels = new List<Text>();
            // Each card's keyword mark (예약 효과·사용 효과, LegacyKeywordPreview.QueueMark) and what it should show.
            private readonly List<RectTransform> keywordMarks = new List<RectTransform>();
            private readonly List<Text> keywordLabels = new List<Text>();
            private readonly List<LegacyKeywordMark> keywordMarkValues = new List<LegacyKeywordMark>();
            // The enemy's row only: a strike across each card the player's skill will dodge by itself, and those slots.
            private readonly List<GameObject> dodgeStrikes = new List<GameObject>();
            private readonly List<int> dodgedSlots = new List<int>();
            private int queuePowerSourceSlot = -1, queuePowerTargetSlot = -1, queuePowerRemovedCount, queuePowerMultiplier = 1;
            private int shownQueuePowerSourceSlot = -1, shownQueuePowerTargetSlot = -1, shownQueuePowerRemovedCount = -1, shownQueuePowerMultiplier = -1;
            private LegacyQueueRemovalMode queuePowerRemovalMode, shownQueuePowerRemovalMode;
            public bool HasQueuePowerMark => player && (queuePowerSourceSlot >= 0 || queuePowerTargetSlot >= 0);
            private IReadOnlyList<LegacySkill> displayedQueue;
            private LegacySkill preview;
            private LegacySkillFeedback currentFeedback;
            private int previewSlot = -1, feedbackSlot = -1;
            /// <param name="meshGear">맞물림's gear for the marks under meshed cards; null for a row that never meshes.</param>
            public QueueView(RectTransform root, bool player, Sprite white, Font font, Sprite meshGear = null)
            {
                Root = root; this.player = player; this.white = white; this.font = font; this.meshGear = meshGear;
            }
            public void Refresh(IReadOnlyList<LegacySkill> queue, Func<int, Sprite> iconFor, int activeSlot, bool resolving,
                int selected, float scale, ILegacyDuelView cycleSession = null)
            {
                while (icons.Count < queue.Count)
                {
                    var frame = Panel("Queued Skill", Root, Vector2.zero, new Vector2(64, 64), Card);
                    var item = frame.rectTransform;
                    Pin(item, Vector2.one * .5f);
                    // A 3px ring, so the school's dye reads round the icon.
                    PanelSurface(frame).rectTransform.sizeDelta = -Vector2.one * (QueueCardRing * 2f);
                    frames.Add(frame);
                    var highlight = Image("Selected", item, white, Vector2.zero, new Vector2(64, 64), new Color(Accent.r, Accent.g, Accent.b, .4f));
                    var icon = Image("Icon", item, null, Vector2.zero, Vector2.one * QueueIconSize);
                    icon.preserveAspect = true;
                    icons.Add(icon); highlights.Add(highlight);
                    feedback.Add(SkillCardFeedbackGraphic.Create(item, "Skill Condition Feedback", 3f));
                    counterMarks.Add(CreateCounterMark(item));
                    if (meshGear != null) AddMeshMark(item);
                    if (player) AddQueuePowerMarks(item);
                    AddKeywordMark(item);
                    if (!player) AddDodgeStrike(item);
                    AddCycleMark(item);
                    if (!player) AddOwnerMark(item);
                    if (player) AddForecastMark(item);
                    restScales.Add(1f);
                }
                displayedQueue = queue;
                int consumed = resolving ? Mathf.Max(0, activeSlot) : 0;
                int breakFrom = cycleSession == null ? -1 : player
                    ? cycleSession.PlayerBreakCanceledFromSlotIndex : cycleSession.EnemyBreakCanceledFromSlotIndex;
                QueueEndSlot = breakFrom >= 0 ? Mathf.Min(queue.Count, breakFrom) : queue.Count;
                firstShown = consumed;
                visibleItems.Clear();
                visibleSlots.Clear();
                keywordMarkValues.Clear();
                var roster = !player ? cycleSession as ILegacyEnemyRosterView : null;
                bool multipleEnemies = roster != null && roster.Enemies.Count > 1;
                for (int i = 0; i < icons.Count; i++)
                {
                    var item = icons[i].transform.parent.GetComponent<RectTransform>();
                    bool visible = i < QueueEndSlot && i >= consumed && queue[i] != null &&
                        (!multipleEnemies || !roster.IsEnemySlotCanceled(i));
                    item.gameObject.SetActive(visible);
                    if (!player)
                    {
                        int owner = multipleEnemies && i < queue.Count ? roster.EnemyOwnerAtSlot(i) : -1;
                        ownerMarks[i].gameObject.SetActive(visible && owner >= 0);
                        if (visible && owner >= 0)
                        {
                            ownerFrames[i].color = EnemyIdentityColor(owner);
                            ownerLabels[i].text = EnemyIdentityNumber(owner);
                        }
                    }
                    keywordMarkValues.Add(visible ? LegacyKeywordPreview.QueueMark(cycleSession, player, i, !resolving)
                        : LegacyKeywordMark.None);
                    if (!visible) { feedback[i].Clear(); continue; }
                    icons[i].sprite = iconFor(queue[i].IconId);
                    frames[i].color = schoolsNamed && !queue[i].IsWait ? SkillLaneStyle.Edge(queue[i].LaneIndex) : Border;
                    visibleItems.Add(item);
                    visibleSlots.Add(i);
                    restScales[i] = (resolving && i == activeSlot ? Mathf.Min(scale, QueueActiveCardMaxScale) : 1) * CardScale;
                    item.localScale = Vector3.one * restScales[i];
                    highlights[i].enabled = !resolving && i == selected;
                    // Each row counts its own side's uses: a versus opponent cycles too.
                    LegacySkillEffect effect = LegacySkillDefinitions.Find(queue[i])?.Effect;
                    bool cycle = effect?.HasCycle == true;
                    cycleMarks[i].gameObject.SetActive(cycle);
                    if (cycle)
                    {
                        // The count this slot reads as it starts: two copies of one skill show their own, and 순환 +1
                        // already counts while planning, written as its rise (1→2/2), or 최대 at the cap.
                        string count = LegacyKeywordPreview.CycleCount(cycleSession, player, i, !resolving,
                            out bool raised, out bool capped) ?? "0/" + effect.CycleMaxCount;
                        if (cycleLabels[i].text != count) cycleLabels[i].text = count;
                        cycleLabels[i].color = capped ? DuelVisualTheme.Muted : raised ? CycleRaisedInk : Foreground;
                        cycleMarks[i].sizeDelta = new Vector2(raised ? 50f : 36f, 20f);
                        cycleLabels[i].rectTransform.sizeDelta = new Vector2(raised ? 48f : 32f, 18f);
                        cycleMarks[i].Find("Cycle Mark Inset").GetComponent<RectTransform>().sizeDelta =
                            new Vector2(raised ? 48f : 34f, 18f);
                    }
                }
                for (int i = 0; i < counterMarks.Count; i++)
                    counterMarks[i].SetActive(icons[i].transform.parent.gameObject.activeSelf && counterSlots.Contains(i));
                for (int i = 0; i < dodgeStrikes.Count; i++)
                    dodgeStrikes[i].SetActive(icons[i].transform.parent.gameObject.activeSelf && dodgedSlots.Contains(i));
                UpdateMeshMarks();
                UpdateQueuePowerMarks();
                UpdateKeywordMarks();
                UpdateForecastMarks();
                UpdateFeedback();
            }

            /// <summary>The spacing between neighbouring cards now (the tuned one unless the panels had to tighten).</summary>
            public float CurrentPitch { get; private set; } = DefaultPitch;

            /// <summary>Lays the shown cards out from the seam (the row's root): slot 1 at <paramref name="inner"/> from it, the
            /// rest a <paramref name="spacing"/> further out each, the whole row <paramref name="slide"/> slots further out while
            /// it slides in (<see cref="LegacyQueuePanelLayout"/>). <paramref name="cardSize"/> is a card as drawn.</summary>
            public void Layout(float spacing, float inner, float slide, float cardSize, int windowStart, int windowCapacity)
            {
                CurrentPitch = spacing;
                // Refresh restores every live logical slot; this shared window is applied only after both sides have
                // refreshed, so Tab, the newest reservation and resolution all reveal the same range on either side.
                for (int index = visibleItems.Count - 1; index >= 0; index--)
                {
                    int slot = visibleSlots[index];
                    if (slot >= windowStart && slot < windowStart + windowCapacity) continue;
                    visibleItems[index].gameObject.SetActive(false);
                    visibleItems.RemoveAt(index);
                    visibleSlots.RemoveAt(index);
                }
                firstShown = Mathf.Max(firstShown, windowStart);
                int count = visibleItems.Count;
                if (count == 0)
                {
                    DisplaySize = HorizontalSpan = Vector2.zero;
                    return;
                }
                float side = player ? -1f : 1f, half = cardSize / 2f;
                float first = LegacyQueuePanelLayout.CardDistance(visibleSlots[0] - firstShown, inner, spacing, slide);
                float last = LegacyQueuePanelLayout.CardDistance(visibleSlots[count - 1] - firstShown, inner, spacing, slide);
                DisplaySize = new Vector2(last - first + cardSize, cardSize);
                HorizontalSpan = player ? new Vector2(-(last + half), -(first - half)) : new Vector2(first - half, last + half);
                for (int index = 0; index < count; index++)
                {
                    RectTransform item = visibleItems[index];
                    int slot = visibleSlots[index];
                    var position = new Vector2(side * LegacyQueuePanelLayout.CardDistance(slot - firstShown, inner, spacing, slide), 0f);
                    // The clash motion's card is drawn off its place (its row's span keeps the place); every other card
                    // stands upright where the row puts it.
                    if (slot == poseIndex)
                    {
                        item.anchoredPosition = position + poseOffset;
                        item.localRotation = Quaternion.Euler(0f, 0f, poseTilt);
                        item.localScale = Vector3.one * (restScales[poseIndex] * poseScale);
                    }
                    else
                    {
                        item.anchoredPosition = position;
                        if (item.localRotation != Quaternion.identity) item.localRotation = Quaternion.identity;
                    }
                }
            }

            /// <summary>Draws the card of slot <paramref name="index"/> off its place on the next <see cref="Layout"/> (the clash
            /// motion): moved by <paramref name="offset"/>, tilted <paramref name="tilt"/> degrees, its scale at rest times
            /// <paramref name="scale"/>. Laid out again every frame, so a card the pose leaves is back at rest at once.</summary>
            public void SetPose(int index, Vector2 offset, float tilt, float scale)
            {
                poseIndex = index;
                poseOffset = offset;
                poseTilt = tilt;
                poseScale = scale > 0f ? scale : 1f;
            }

            public void ClearPose()
            {
                poseIndex = -1;
                poseOffset = Vector2.zero;
                poseTilt = 0f;
                poseScale = 1f;
            }

            /// <summary>The shown card the last <see cref="Layout"/> drew off its place (<see cref="SetPose"/>): its slot and the
            /// offset added to its place; false while every card stands where the row puts it.</summary>
            public bool TryGetPose(out int index, out Vector2 offset)
            {
                index = IsShown(poseIndex) ? poseIndex : -1;
                offset = index >= 0 ? poseOffset : Vector2.zero;
                return index >= 0;
            }

            /// <summary>Whether slot <paramref name="index"/>'s card is shown (as last refreshed).</summary>
            public bool IsShown(int index)
                => index >= 0 && index < icons.Count && icons[index].transform.parent.gameObject.activeSelf;

            /// <summary>The card of slot <paramref name="index"/> as last laid out: its node, ring ink, icon and scale at rest
            /// (a clash card that leaves the row takes them on); false for a slot that has none.</summary>
            public bool TryGetCardLook(int index, out RectTransform card, out Color ring, out Sprite icon, out float restScale)
            {
                card = null;
                ring = Border;
                icon = null;
                restScale = 1f;
                if (index < 0 || index >= icons.Count) return false;
                card = icons[index].transform.parent.GetComponent<RectTransform>();
                ring = frames[index].color;
                icon = icons[index].sprite;
                restScale = restScales[index];
                return true;
            }

            /// <summary>Whether the cards' rings wear their schools' dyes (the enemy's row waits until all three are open).</summary>
            public void SetSchoolsNamed(bool named) => schoolsNamed = named;

            /// <summary>편성 예상: the verdict each slot's card should show ((int)<see cref="LegacyForecastVerdict"/>, -1 for none).</summary>
            public void SetForecastVerdicts(List<int> verdicts)
            {
                forecastVerdicts.Clear();
                forecastVerdicts.AddRange(verdicts);
            }

            public RectTransform GetForecastMark(int index)
                => index >= 0 && index < forecastMarks.Count && forecastMarks[index].gameObject.activeInHierarchy
                    ? forecastMarks[index] : null;

            public LegacyForecastVerdict? GetForecastVerdict(int index)
                => GetForecastMark(index) == null ? (LegacyForecastVerdict?)null : forecastGlyphs[index].Verdict;

            // A small square on the card's left rim (the 순환 count rides the right): the verdict's fill and its sign in ink.
            private void AddForecastMark(RectTransform item)
            {
                var badge = Image("Forecast Mark", item, white, ForecastMarkCenter, Vector2.one * ForecastMarkSize, DuelVisualTheme.Muted);
                var sign = Rect("Forecast Sign", badge.transform, Vector2.zero, Vector2.one * (ForecastMarkSize - 6f), Vector2.one * .5f);
                sign.gameObject.AddComponent<CanvasRenderer>();
                var glyph = sign.gameObject.AddComponent<SlotForecastGlyph>();
                glyph.raycastTarget = false;
                glyph.color = DuelVisualTheme.Ink;
                badge.gameObject.SetActive(false);
                forecastMarks.Add(badge.rectTransform);
                forecastFills.Add(badge);
                forecastGlyphs.Add(glyph);
            }

            private void UpdateForecastMarks()
            {
                for (int index = 0; index < forecastMarks.Count; index++)
                {
                    bool visible = icons[index].transform.parent.gameObject.activeSelf;
                    int verdict = visible && index < forecastVerdicts.Count ? forecastVerdicts[index] : -1;
                    bool shown = verdict >= 0;
                    if (forecastMarks[index].gameObject.activeSelf != shown) forecastMarks[index].gameObject.SetActive(shown);
                    if (!shown) continue;
                    var value = (LegacyForecastVerdict)verdict;
                    forecastGlyphs[index].SetVerdict(value);
                    forecastFills[index].color = ForecastFill(value);
                }
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
                for (int index = 0; index < meshPop.Count; index++)
                {
                    if (!(meshPop[index] > 0f)) continue;
                    meshPop[index] = Mathf.Max(0f, meshPop[index] - Mathf.Max(0f, realDelta));
                    meshMarks[index].localScale = Vector3.one * LegacyMeshCue.MarkScale(meshPop[index]);
                }
            }

            /// <summary>맞물림: the bonus each slot's card should carry (0: not meshed), read from the duel.</summary>
            public void SetMeshBonuses(List<int> bonuses)
            {
                meshBonus.Clear();
                meshBonus.AddRange(bonuses);
            }

            /// <summary>The cancellation count and selected followup multiplier for the current queue.</summary>
            public void SetQueuePowerMarks(int sourceSlot, int removedCount, int targetSlot, int multiplier, LegacyQueueRemovalMode removalMode)
            {
                queuePowerSourceSlot = sourceSlot;
                queuePowerRemovedCount = removedCount;
                queuePowerTargetSlot = targetSlot;
                queuePowerMultiplier = multiplier;
                queuePowerRemovalMode = removalMode;
            }

            public RectTransform GetQueuePowerSourceMark(int index)
                => index >= 0 && index < queuePowerSourceMarks.Count && queuePowerSourceMarks[index].gameObject.activeInHierarchy
                    ? queuePowerSourceMarks[index] : null;

            public RectTransform GetQueuePowerTargetMark(int index)
                => index >= 0 && index < queuePowerTargetMarks.Count && queuePowerTargetMarks[index].gameObject.activeInHierarchy
                    ? queuePowerTargetMarks[index] : null;

            public string GetQueuePowerSourceMarkLabel(int index)
                => GetQueuePowerSourceMark(index) == null ? null : queuePowerSourceLabels[index].text;

            public string GetQueuePowerTargetMarkLabel(int index)
                => GetQueuePowerTargetMark(index) == null ? null : queuePowerTargetLabels[index].text;

            /// <summary>The mark under a meshed card (its gear and bonus), or null while that card shows none.</summary>
            public RectTransform GetMeshMark(int index)
                => index >= 0 && index < meshMarks.Count && meshMarks[index].gameObject.activeInHierarchy ? meshMarks[index] : null;

            public string GetMeshLabel(int index) => GetMeshMark(index) != null ? meshLabels[index].text : null;

            // A brass-rimmed tab over the card's bottom edge (the counter's tab is over its top).
            // The dark inset and larger light label keep the bonus legible at smaller canvas scales.
            private void AddMeshMark(RectTransform item)
            {
                var badge = Image("Mesh Mark", item, white, new Vector2(0f, -30f), new Vector2(70f, 22f), Accent);
                Image("Mesh Mark Inset", badge.transform, white, Vector2.zero, new Vector2(68f, 20f), DuelVisualTheme.Ink);
                Image("Mesh Gear", badge.transform, meshGear, new Vector2(-26f, 0f), Vector2.one * 12f, Accent);
                var label = Rect("Label", badge.transform, new Vector2(8f, 0f), new Vector2(54f, 22f), Vector2.one * .5f)
                    .gameObject.AddComponent<Text>();
                label.font = font;
                label.fontSize = 20;
                label.fontStyle = FontStyle.Normal;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 18;
                label.resizeTextMaxSize = 20;
                label.supportRichText = false;
                label.raycastTarget = false;
                label.color = Foreground;
                badge.gameObject.SetActive(false);
                meshMarks.Add(badge.rectTransform);
                meshLabels.Add(label);
                shownMeshBonus.Add(0);
                meshPop.Add(0f);
            }

            // This small count sits on the card's right rim, between its top counter and lower mesh tabs.
            // The selected W command shows the full keyword; queued skills only need the compact fraction.
            private void AddCycleMark(RectTransform item)
            {
                var badge = Image("Cycle Mark", item, white, new Vector2(25f, 0f),
                    new Vector2(36f, 20f), new Color32(81, 153, 143, 255));
                Image("Cycle Mark Inset", badge.transform, white, Vector2.zero,
                    new Vector2(34f, 18f), DuelVisualTheme.Ink);
                var label = Rect("Cycle Count", badge.transform, Vector2.zero,
                    new Vector2(32f, 18f), Vector2.one * .5f).gameObject.AddComponent<Text>();
                label.font = font;
                label.fontSize = 13;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Foreground;
                label.raycastTarget = false;
                label.supportRichText = false;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                badge.gameObject.SetActive(false);
                cycleMarks.Add(badge.rectTransform);
                cycleLabels.Add(label);
            }

            // The owner sits inside the lower-left rim, clear of the keyword above, cycle count at right and school's
            // coloured frame. Only multi-enemy battles show it; its numeral also appears over that enemy's head.
            private void AddOwnerMark(RectTransform item)
            {
                var badge = Image("Enemy Owner Mark", item, white, new Vector2(-23f, -23f),
                    Vector2.one * 22f, Accent);
                var label = Rect("Enemy Number", badge.transform, Vector2.zero,
                    Vector2.one * 22f, Vector2.one * .5f).gameObject.AddComponent<Text>();
                label.font = font;
                label.fontSize = 16;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = DuelVisualTheme.Ink;
                label.raycastTarget = false;
                label.supportRichText = false;
                badge.gameObject.SetActive(false);
                ownerMarks.Add(badge.rectTransform);
                ownerFrames.Add(badge);
                ownerLabels.Add(label);
            }

            public RectTransform GetOwnerMark(int index)
                => index >= 0 && index < ownerMarks.Count && ownerMarks[index].gameObject.activeInHierarchy
                    ? ownerMarks[index] : null;

            /// <summary>The enemy's slots the player's skill will dodge by itself (자동 회피), struck through.</summary>
            public void SetDodgedSlots(List<int> slots)
            {
                dodgedSlots.Clear();
                dodgedSlots.AddRange(slots);
            }

            public bool IsDodged(int index)
                => index >= 0 && index < dodgeStrikes.Count && dodgeStrikes[index].activeInHierarchy;

            // A pale bar across the card, corner to corner: its attack will not land.
            private void AddDodgeStrike(RectTransform item)
            {
                var strike = Image("Dodged Strike", item, white, Vector2.zero, new Vector2(78f, 4f),
                    new Color(Foreground.r, Foreground.g, Foreground.b, .85f));
                strike.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -40f);
                strike.gameObject.SetActive(false);
                dodgeStrikes.Add(strike.gameObject);
            }

            // 순환 +1's rise on a card's count: 순환's teal, lightened.
            private static readonly Color CycleRaisedInk = new Color32(150, 222, 204, 255);

            public string GetKeywordMarkLabel(int index)
                => index >= 0 && index < keywordMarks.Count && keywordMarks[index].gameObject.activeInHierarchy
                    ? keywordLabels[index].text : null;

            public string GetCycleCountLabel(int index)
                => index >= 0 && index < cycleMarks.Count && cycleMarks[index].gameObject.activeInHierarchy
                    ? cycleLabels[index].text : null;

            // The keyword mark rides over the card where the 취소/반환 tabs do; a card shows at most one tab up there (a
            // 취소/반환 one first) and 맞물림's under it, so never more than two marks.
            private void AddKeywordMark(RectTransform item)
            {
                var badge = Image("Queue Keyword Mark", item, white, new Vector2(0f, 52f), new Vector2(68f, 20f), Accent);
                Image("Queue Keyword Mark Inset", badge.transform, white, Vector2.zero, new Vector2(66f, 18f), DuelVisualTheme.Ink);
                var label = Rect("Label", badge.transform, Vector2.zero, new Vector2(64f, 18f), Vector2.one * .5f)
                    .gameObject.AddComponent<Text>();
                label.font = font;
                label.fontSize = 14;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 10;
                label.resizeTextMaxSize = 14;
                label.supportRichText = false;
                label.raycastTarget = false;
                label.color = Foreground;
                badge.gameObject.SetActive(false);
                keywordMarks.Add(badge.rectTransform);
                keywordLabels.Add(label);
            }

            private void UpdateKeywordMarks()
            {
                for (int index = 0; index < keywordMarks.Count; index++)
                {
                    LegacyKeywordMark mark = index < keywordMarkValues.Count ? keywordMarkValues[index] : LegacyKeywordMark.None;
                    bool taken = index < queuePowerSourceMarks.Count && (queuePowerSourceMarks[index].gameObject.activeSelf ||
                        queuePowerTargetMarks[index].gameObject.activeSelf);
                    bool shown = !mark.IsEmpty && !taken;
                    if (keywordMarks[index].gameObject.activeSelf != shown) keywordMarks[index].gameObject.SetActive(shown);
                    if (!shown) continue;
                    if (keywordLabels[index].text != mark.Text) keywordLabels[index].text = mark.Text;
                    keywordLabels[index].color = mark.Spent ? DuelVisualTheme.Muted : Foreground;
                    keywordMarks[index].GetComponent<Image>().color = KeywordRim(mark.Kind);
                }
            }

            // The mark's rim in its keyword's colour (the badges' paper): 숨고르기's recovery, 순환's, 회피's guard, 일회성's
            // multi-hit, 제한시간's variance; a count-ahead power payoff keeps the brass of the 취소/반환 tabs' place.
            private static Color KeywordRim(LegacyKeywordMarkKind kind)
            {
                switch (kind)
                {
                    case LegacyKeywordMarkKind.AutoBreath: return SkillToneStyle.Paper(LegacySkillTone.Recovery);
                    case LegacyKeywordMarkKind.CycleFeed: return SkillToneStyle.Paper(LegacySkillTone.Cycle);
                    case LegacyKeywordMarkKind.AutoDodge: return SkillToneStyle.Paper(LegacySkillTone.Defence);
                    case LegacyKeywordMarkKind.Temporary: return SkillToneStyle.Paper(LegacySkillTone.MultiHit);
                    case LegacyKeywordMarkKind.AheadTime: return SkillToneStyle.Paper(LegacySkillTone.Variance);
                    default: return Accent;
                }
            }

            // Tabs rise over the counter mark, leaving the card art and the mesh mark readable.
            private void AddQueuePowerMarks(RectTransform item)
            {
                queuePowerSourceMarks.Add(CreateQueuePowerMark(item, "Queue Cancel Mark", DuelVisualTheme.Danger,
                    out Text sourceLabel));
                queuePowerSourceLabels.Add(sourceLabel);
                queuePowerTargetMarks.Add(CreateQueuePowerMark(item, "Queue Power Mark", Accent,
                    out Text targetLabel));
                queuePowerTargetLabels.Add(targetLabel);
            }

            private RectTransform CreateQueuePowerMark(RectTransform item, string name, Color rim, out Text label)
            {
                var badge = Image(name, item, white, new Vector2(0f, 52f), new Vector2(68f, 20f), rim);
                Image(name + " Inset", badge.transform, white, Vector2.zero, new Vector2(66f, 18f), DuelVisualTheme.Ink);
                label = Rect("Label", badge.transform, Vector2.zero, new Vector2(64f, 18f), Vector2.one * .5f)
                    .gameObject.AddComponent<Text>();
                label.font = font;
                label.fontSize = 16;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 14;
                label.resizeTextMaxSize = 16;
                label.supportRichText = false;
                label.raycastTarget = false;
                label.color = Foreground;
                badge.gameObject.SetActive(false);
                return badge.rectTransform;
            }

            private void UpdateQueuePowerMarks()
            {
                for (int index = 0; index < queuePowerSourceMarks.Count; index++)
                {
                    bool visible = icons[index].transform.parent.gameObject.activeSelf;
                    bool source = visible && index == queuePowerSourceSlot;
                    bool target = visible && index == queuePowerTargetSlot;
                    if (source && (index != shownQueuePowerSourceSlot || queuePowerRemovedCount != shownQueuePowerRemovedCount ||
                        queuePowerRemovalMode != shownQueuePowerRemovalMode))
                    {
                        queuePowerSourceLabels[index].text =
                            (queuePowerRemovalMode == LegacyQueueRemovalMode.Return ? "반환 " : "취소 ") + queuePowerRemovedCount;
                        queuePowerSourceMarks[index].GetComponent<Image>().color = queuePowerRemovalMode == LegacyQueueRemovalMode.Return
                            ? DuelVisualTheme.Health : DuelVisualTheme.Danger;
                    }
                    if (target && (index != shownQueuePowerTargetSlot || queuePowerMultiplier != shownQueuePowerMultiplier))
                        queuePowerTargetLabels[index].text = "다음 " + queuePowerMultiplier + "배";
                    queuePowerSourceMarks[index].gameObject.SetActive(source);
                    queuePowerTargetMarks[index].gameObject.SetActive(target);
                }
                shownQueuePowerSourceSlot = queuePowerSourceSlot;
                shownQueuePowerRemovedCount = queuePowerRemovedCount;
                shownQueuePowerTargetSlot = queuePowerTargetSlot;
                shownQueuePowerMultiplier = queuePowerMultiplier;
                shownQueuePowerRemovalMode = queuePowerRemovalMode;
            }

            // Each shown card carries its slot's bonus; a mark that appears or rises pops, so a chain growing reads at once.
            private void UpdateMeshMarks()
            {
                for (int index = 0; index < meshMarks.Count; index++)
                {
                    bool visible = icons[index].transform.parent.gameObject.activeSelf;
                    int bonus = visible && index < meshBonus.Count ? meshBonus[index] : 0;
                    if (bonus == shownMeshBonus[index]) continue;
                    if (bonus > shownMeshBonus[index]) meshPop[index] = LegacyMeshCue.MarkPopSeconds;
                    else if (bonus == 0) meshPop[index] = 0f;
                    shownMeshBonus[index] = bonus;
                    meshLabels[index].text = LegacyMeshCue.BonusLabel(bonus);
                    meshMarks[index].localScale = Vector3.one * LegacyMeshCue.MarkScale(meshPop[index]);
                    meshMarks[index].gameObject.SetActive(bonus > 0);
                }
            }
            public void Clear()
            {
                DisplaySize = Vector2.zero;
                HorizontalSpan = Vector2.zero;
                visibleItems.Clear();
                visibleSlots.Clear();
                CurrentPitch = DefaultPitch;
                displayedQueue = null;
                QueueEndSlot = 0;
                preview = null;
                previewSlot = feedbackSlot = -1;
                currentFeedback = null;
                counterSlots.Clear();
                foreach (var view in feedback) view.Clear();
                ClearPose();
                firstShown = 0;
                foreach (var icon in icons)
                {
                    icon.transform.parent.localRotation = Quaternion.identity;
                    icon.transform.parent.gameObject.SetActive(false);
                }
                foreach (var mark in counterMarks) mark.SetActive(false);
                queuePowerSourceSlot = queuePowerTargetSlot = -1;
                queuePowerRemovedCount = 0;
                queuePowerMultiplier = 1;
                queuePowerRemovalMode = shownQueuePowerRemovalMode = LegacyQueueRemovalMode.None;
                shownQueuePowerSourceSlot = shownQueuePowerTargetSlot = -1;
                shownQueuePowerRemovedCount = shownQueuePowerMultiplier = -1;
                foreach (var mark in queuePowerSourceMarks) mark.gameObject.SetActive(false);
                foreach (var mark in queuePowerTargetMarks) mark.gameObject.SetActive(false);
                foreach (var mark in cycleMarks) mark.gameObject.SetActive(false);
                foreach (var mark in keywordMarks) mark.gameObject.SetActive(false);
                keywordMarkValues.Clear();
                foreach (var strike in dodgeStrikes) strike.SetActive(false);
                dodgedSlots.Clear();
                forecastVerdicts.Clear();
                foreach (var mark in forecastMarks) mark.gameObject.SetActive(false);
                meshBonus.Clear();
                for (int index = 0; index < meshMarks.Count; index++)
                {
                    shownMeshBonus[index] = 0;
                    meshPop[index] = 0f;
                    meshMarks[index].localScale = Vector3.one;
                    meshMarks[index].gameObject.SetActive(false);
                }
            }
            public RectTransform GetAnchor(int index)
            {
                if (index < 0 || index >= icons.Count) return null;
                var card = icons[index].transform.parent.GetComponent<RectTransform>();
                return card.gameObject.activeInHierarchy ? card : null;
            }
        }

        private sealed class QueueCancellationGhost
        {
            public readonly RectTransform Rect;
            public readonly CanvasGroup Group;
            public readonly Vector2 Start;
            public readonly float OutwardSpeed, UpwardSpeed, SpinSpeed;
            public float Elapsed;

            public QueueCancellationGhost(RectTransform rect, CanvasGroup group, Vector2 start, int slot)
            {
                Rect = rect;
                Group = group;
                Start = start;
                OutwardSpeed = -110f - slot % 3 * 18f;
                UpwardSpeed = 145f + slot % 3 * 12f;
                SpinSpeed = -105f - slot % 3 * 24f;
            }
        }

        private sealed class DamageView
        {
            public readonly Text Text;
            public readonly Outline Outline;
            public readonly Text Detail;
            public Vector3 WorldPosition;
            /// <summary>When set, the view tracks this transform instead of the fixed impact point.</summary>
            public Transform Follow;
            public Vector3 WorldOffset;
            public Vector2 StartOffset;
            public Color Color;
            public float Direction;
            public float Duration, Remaining, Scale;
            public DamageView(Text text, Outline outline, Text detail = null) { Text = text; Outline = outline; Detail = detail; }
        }
    }
}
