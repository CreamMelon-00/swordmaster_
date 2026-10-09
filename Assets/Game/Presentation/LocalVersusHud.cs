using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The local duel's shared command desk. Match rules and keyboard input stay in the controller.</summary>
    public sealed partial class LocalVersusHud : IDisposable
    {
        public const int SortingOrder = 110;
        private readonly LegacyDuelArt art;
        private readonly RectTransform root;
        private readonly GameObject ownedEventSystem;
        private readonly Text roundLabel, activeLabel, phaseLabel, commandHeading;
        private readonly Text[] timeLabels = new Text[2];
        private readonly Text[] healthLabels = new Text[2];
        private readonly Text[] resistanceLabels = new Text[2];
        private readonly Text[] actLabels = new Text[2];
        private readonly Image[] healthFills = new Image[2];
        private readonly Image[] resistanceFills = new Image[2];
        private readonly Image[] actFills = new Image[2];
        private readonly Image[] statusSurfaces = new Image[2];
        private readonly QueueRow[] queues = new QueueRow[2];
        private readonly Button[] laneButtons = new Button[3];
        private readonly Image[] laneIcons = new Image[3];
        private readonly Text[] laneNames = new Text[3];
        private readonly Text[] laneCosts = new Text[3];
        private readonly Button cycleButton, breathButton, passButton, titleButton;
        private readonly RectTransform detailPanel;
        private readonly Image detailIcon;
        private readonly Text detailName;
        private readonly SkillInfoView detailInfo;
        private readonly Text breathDescription;
        private int hoveredLane = -1;
        private int hoveredQueuePlayer = -1, hoveredQueueIndex = -1;
        private readonly Image commandActFill;
        private readonly Text passLabel, breathLabel, resultHeading, rematchCaption;
        private readonly GameObject resultOverlay, exitOverlay;
        private readonly Button rematchButton, resultTitleButton, confirmExitButton, cancelExitButton;
        private LocalVersusMatch match;
        private float leftClock, rightClock;
        private bool disposed;
        private int localPlayer = -1;

        public GameObject Root => root.gameObject;
        public bool IsExitConfirming => !disposed && exitOverlay.activeSelf;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;

        public LocalVersusHud(Transform parent, LegacyDuelArt art, Action<int> queueLane,
            Action cycle, Action breath, Action pass, Action rematch, Action exit)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            root = Rect("Local Versus HUD", parent, Vector2.zero, Vector2.zero);
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
                ownedEventSystem = new GameObject("Local Versus EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(parent, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            // The corner cards and low command desk leave the actors and their clash unobscured.
            roundLabel = Label("Versus Round", root, new Vector2(0f, 502f), new Vector2(240f, 34f),
                24, DuelVisualTheme.Accent);
            activeLabel = Label("Versus Active Player", root, new Vector2(0f, 454f),
                new Vector2(380f, 54f), 36, DuelVisualTheme.Foreground);
            phaseLabel = Label("Versus Phase", root, new Vector2(0f, 411f), new Vector2(600f, 30f),
                18, DuelVisualTheme.Muted);
            for (int player = 0; player < 2; player++)
            {
                BuildStatus(player);
                queues[player] = new QueueRow(player, art, root, ShowQueuedSkillDetail, HideSkillDetail);
            }

            var dock = Panel("Versus Command Desk", root, new Vector2(0f, -443f),
                new Vector2(1290f, 180f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(dock);
            commandDock = dock.rectTransform;
            commandHeading = Label("Versus Command Heading", dock.transform,
                new Vector2(-446f, 65f), new Vector2(300f, 26f), 19,
                DuelVisualTheme.Accent, TextAnchor.MiddleLeft);
            Image commandFill;
            Bar(dock.transform, "Versus Active ACT", new Vector2(98f, 65f), 570f,
                DuelVisualTheme.Accent, out commandFill);
            commandActFill = commandFill;

            cycleButton = ActionButton("Versus Cycle", dock.transform, new Vector2(-548f, -22f),
                new Vector2(150f, 100f), "넘기기\n[Shift]", cycle);
            for (int lane = 0; lane < 3; lane++)
            {
                int selectedLane = lane;
                float x = -335f + lane * 200f;
                laneButtons[lane] = ActionButton("Versus Lane " + LaneKey(lane), dock.transform,
                    new Vector2(x, -22f), new Vector2(190f, 100f), null,
                    () => queueLane?.Invoke(selectedLane));
                var icon = Icon("Versus Lane Icon", laneButtons[lane].transform, null,
                    new Vector2(-56f, 0f), new Vector2(54f, 54f));
                icon.preserveAspect = true;
                laneIcons[lane] = icon;
                Label("Versus Lane Key", laneButtons[lane].transform, new Vector2(61f, 30f),
                    new Vector2(36f, 26f), 18, DuelVisualTheme.Accent).text = LaneKey(lane);
                laneNames[lane] = Label("Versus Lane Skill", laneButtons[lane].transform,
                    new Vector2(28f, 3f), new Vector2(102f, 30f), 19, DuelVisualTheme.Foreground);
                laneNames[lane].resizeTextForBestFit = true;
                laneNames[lane].resizeTextMinSize = 13;
                laneNames[lane].resizeTextMaxSize = 19;
                laneCosts[lane] = Label("Versus Lane ACT", laneButtons[lane].transform,
                    new Vector2(28f, -31f), new Vector2(100f, 20f), 14, DuelVisualTheme.Muted);
                var hover = laneButtons[lane].gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => ShowSkillDetail(selectedLane));
                hover.triggers.Add(enter);
                var leave = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                leave.callback.AddListener(_ => HideSkillDetail());
                hover.triggers.Add(leave);
            }
            breathButton = ActionButton("Versus Breath", dock.transform, new Vector2(278f, -22f),
                new Vector2(174f, 100f), "숨고르기\n[S]", breath);
            breathLabel = breathButton.GetComponentInChildren<Text>();
            passButton = ActionButton("Versus Pass", dock.transform, new Vector2(515f, -22f),
                new Vector2(170f, 100f), null, pass, true);
            passLabel = Label("Button Label", passButton.transform, Vector2.zero,
                new Vector2(158f, 92f), 20, DuelVisualTheme.Ink);

            titleButton = ActionButton("Versus Exit", root, new Vector2(0f, 361f),
                new Vector2(172f, 38f), "타이틀  [Esc]", ToggleExitConfirmation);

            var details = Panel("Versus Skill Detail", root, new Vector2(0f, -141f),
                new Vector2(440f, 318f), DuelVisualTheme.Paper);
            DuelVisualTheme.DressPanel(details);
            detailPanel = details.rectTransform;
            detailIcon = Icon("Versus Detail Icon", details.transform, null,
                new Vector2(-166f, 109f), new Vector2(60f, 60f));
            detailIcon.preserveAspect = true;
            detailName = Label("Versus Detail Name", details.transform, new Vector2(37f, 112f),
                new Vector2(300f, 42f), 26, DuelVisualTheme.Ink, TextAnchor.MiddleLeft);
            detailName.resizeTextForBestFit = true;
            detailName.resizeTextMinSize = 18;
            detailName.resizeTextMaxSize = 26;
            detailInfo = new SkillInfoView(details.transform, art.UIFont,
                new Vector2(0f, -34f), 408f, "Versus");
            breathDescription = Label("Versus Breath Description", details.transform,
                new Vector2(0f, -21f), new Vector2(366f, 174f), 24,
                DuelVisualTheme.Ink, TextAnchor.UpperLeft);
            breathDescription.text = "ACT 0\n\n한 박자 쉬어 기술 순서를 조절합니다.";
            breathDescription.gameObject.SetActive(false);
            detailPanel.gameObject.SetActive(false);
            BuildOnlinePresentation();

            var resultVeil = Panel("Versus Result Overlay", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .8f));
            Stretch(resultVeil.rectTransform);
            resultVeil.raycastTarget = true;
            resultOverlay = resultVeil.gameObject;
            var resultCard = Panel("Versus Result Card", resultVeil.transform, Vector2.zero,
                new Vector2(720f, 320f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(resultCard);
            resultHeading = Label("Versus Result Heading", resultCard.transform,
                new Vector2(0f, 66f), new Vector2(620f, 78f), 56, DuelVisualTheme.Foreground);
            Label("Versus Result Detail", resultCard.transform, new Vector2(0f, 6f),
                new Vector2(600f, 40f), 22, DuelVisualTheme.Muted).text = "대전 종료";
            rematchButton = ActionButton("Versus Rematch", resultCard.transform,
                new Vector2(-136f, -91f), new Vector2(234f, 58f), "다시 겨루기", rematch, true);
            rematchCaption = rematchButton.GetComponentInChildren<Text>();
            resultTitleButton = ActionButton("Versus Result Exit", resultCard.transform,
                new Vector2(136f, -91f), new Vector2(234f, 58f), "타이틀로", exit);
            resultOverlay.SetActive(false);

            var exitVeil = Panel("Versus Exit Confirmation", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .83f));
            Stretch(exitVeil.rectTransform);
            exitVeil.raycastTarget = true;
            exitOverlay = exitVeil.gameObject;
            var exitCard = Panel("Versus Exit Card", exitVeil.transform, Vector2.zero,
                new Vector2(660f, 260f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(exitCard);
            Label("Versus Exit Question", exitCard.transform, new Vector2(0f, 49f),
                new Vector2(590f, 86f), 27, DuelVisualTheme.Foreground).text =
                "대전을 끝내고 타이틀로 돌아갈까요?";
            confirmExitButton = ActionButton("Versus Confirm Exit", exitCard.transform,
                new Vector2(-133f, -74f), new Vector2(230f, 56f), "타이틀로", exit, true);
            cancelExitButton = ActionButton("Versus Cancel Exit", exitCard.transform,
                new Vector2(133f, -74f), new Vector2(230f, 56f), "계속하기  [Esc]",
                ToggleExitConfirmation);
            exitOverlay.SetActive(false);
        }

        public void Refresh(LocalVersusMatch current, float leftClock, float rightClock, int localPlayer = -1,
            bool requestPending = false)
        {
            if (disposed || current == null) return;
            match = current;
            this.leftClock = leftClock;
            this.rightClock = rightClock;
            this.localPlayer = localPlayer >= 0 && localPlayer <= 1 ? localPlayer : -1;
            this.requestPending = requestPending;
            ConfigurePresentation(this.localPlayer);
            bool planning = current.Phase == LegacyDuelPhase.Planning && current.CurrentPlanner >= 0;
            bool resolving = current.Phase == LegacyDuelPhase.Resolving;
            roundLabel.text = current.RoundLimit == int.MaxValue ? "제 " + current.RoundNumber + " 턴" : "제 " + current.RoundNumber + " / " + current.RoundLimit + " 턴";
            activeLabel.text = planning
                ? this.localPlayer >= 0
                    ? current.CurrentPlanner == this.localPlayer ? "내 차례" : "상대 차례"
                    : (current.CurrentPlanner + 1) + "P 차례"
                : resolving ? "판정 중" : "승부 종료";
            phaseLabel.text = planning
                ? this.localPlayer >= 0 && current.CurrentPlanner != this.localPlayer
                    ? "상대가 기술을 고르는 중입니다"
                    : current.ConsecutivePasses > 0
                        ? "한 번 더 패스하면 판정합니다" : "번갈아 기술을 예약하세요 · 연속 패스로 판정"
                : resolving ? "예약한 기술을 순서대로 판정합니다" : string.Empty;
            int activeSlot = resolving && current.CurrentSlot != null ? current.CurrentSlot.SlotIndex : -1;
            for (int player = 0; player < 2; player++)
            {
                var fighter = current.GetFighter(player);
                var state = fighter.State;
                healthLabels[player].text = "HP  " + state.Health + " / " + state.MaxHealth;
                resistanceLabels[player].text = state.IsResistanceBroken ? "붕괴" :
                    "저항  " + state.Resistance + " / " + state.MaxResistance;
                actLabels[player].text = "ACT  " + fighter.Act + " / " + fighter.MaximumAct;
                healthFills[player].fillAmount = state.MaxHealth > 0 ?
                    state.Health / (float)state.MaxHealth : 0f;
                resistanceFills[player].fillAmount = state.MaxResistance > 0 ?
                    state.Resistance / (float)state.MaxResistance : 0f;
                actFills[player].fillAmount = fighter.MaximumAct > 0 ?
                    fighter.Act / (float)fighter.MaximumAct : 0f;
                statusSurfaces[player].color = planning && current.CurrentPlanner == player ?
                    DuelVisualTheme.Selected : DuelVisualTheme.Surface;
                float clock = player == 0 ? leftClock : rightClock;
                timeLabels[player].text = Mathf.Max(0f, clock).ToString("0.0") + "s";
                timeLabels[player].color = planning && current.CurrentPlanner == player && clock <= 5f
                    ? DuelVisualTheme.Danger : DuelVisualTheme.Accent;
                queues[player].Refresh(fighter.Queue, activeSlot);
            }
            if (onlineLayout) RefreshOnlinePresentation(current, planning);

            bool canInput = planning && !IsExitConfirming && !this.requestPending &&
                (this.localPlayer < 0 || current.CurrentPlanner == this.localPlayer);
            int active = this.localPlayer >= 0 ? this.localPlayer : planning ? current.CurrentPlanner : 0;
            var currentFighter = current.GetFighter(active);
            commandHeading.text = planning
                ? (this.localPlayer >= 0 ? "내 기술" : (active + 1) + "P") + "  ·  ACT " +
                    currentFighter.Act + " / " + currentFighter.MaximumAct
                : "대기열 판정";
            commandActFill.fillAmount = currentFighter.MaximumAct > 0 ?
                currentFighter.Act / (float)currentFighter.MaximumAct : 0f;
            for (int lane = 0; lane < 3; lane++)
            {
                IReadOnlyList<LegacySkill> skills = currentFighter.GetLane(lane);
                LegacySkill skill = skills != null && skills.Count > 0 ? skills[0] : null;
                laneIcons[lane].sprite = skill != null ? art.GetSkillIcon(skill.IconId) : null;
                laneIcons[lane].enabled = laneIcons[lane].sprite != null;
                laneNames[lane].text = skill?.Name ?? "기술 없음";
                int cost = skill != null ? currentFighter.EffectiveCost(skill) : 0;
                laneCosts[lane].text = skill != null ? "ACT " + cost : string.Empty;
                if (onlineLayout) RefreshOnlineLane(lane, skills, cost);
                laneButtons[lane].interactable = canInput && skill != null && cost <= currentFighter.Act;
            }
            bool canCycle = false;
            for (int lane = 0; lane < 3; lane++)
                if (currentFighter.GetLane(lane).Count > 1) canCycle = true;
            cycleButton.interactable = canInput && canCycle;
            breathButton.interactable = canInput && currentFighter.BreathsRemainingThisTurn > 0;
            breathLabel.text = "숨고르기 " + currentFighter.BreathsRemainingThisTurn +
                "/" + LocalVersusMatch.MaximumBreathsPerTurn + "\n[S]";
            passButton.interactable = canInput;
            passLabel.text = planning && this.localPlayer >= 0 && this.requestPending
                ? "전송 중" : planning && this.localPlayer >= 0 && !canInput
                    ? "상대 차례" : planning ? "패스\n[Space]" : "판정 중";
            if (hoveredLane >= 0 || hoveredQueuePlayer >= 0)
            {
                if (!planning || IsExitConfirming) HideSkillDetail();
                else if (hoveredLane >= 0) ShowSkillDetail(hoveredLane);
                else ShowQueuedSkillDetail(hoveredQueuePlayer, hoveredQueueIndex);
            }
            resultOverlay.SetActive(current.Outcome != LocalVersusOutcome.InProgress);
            if (current.Outcome == LocalVersusOutcome.InProgress) return;
            resultHeading.text = current.Outcome == LocalVersusOutcome.Draw ? "무승부" :
                this.localPlayer >= 0 ?
                    (current.Outcome == (this.localPlayer == 0 ? LocalVersusOutcome.LeftVictory :
                        LocalVersusOutcome.RightVictory) ? "승리" : "패배") :
                    current.Outcome == LocalVersusOutcome.LeftVictory ? "1P 승리" : "2P 승리";
            bool lostOnline = this.localPlayer >= 0 && current.Outcome != LocalVersusOutcome.Draw &&
                current.Outcome != (this.localPlayer == 0 ? LocalVersusOutcome.LeftVictory :
                    LocalVersusOutcome.RightVictory);
            resultHeading.color = current.Outcome == LocalVersusOutcome.Draw ?
                DuelVisualTheme.Foreground : lostOnline ? DuelVisualTheme.Danger : DuelVisualTheme.Accent;
        }

        /// <summary>Opens the active player's front skill information, including its current cost and power.</summary>
        public void ShowSkillDetail(int lane)
        {
            if (disposed || match == null || match.Phase != LegacyDuelPhase.Planning ||
                match.CurrentPlanner < 0 || lane < 0 || lane > 2 || IsExitConfirming) return;
            var fighter = match.GetFighter(localPlayer >= 0 ? localPlayer : match.CurrentPlanner);
            IReadOnlyList<LegacySkill> laneSkills = fighter.GetLane(lane);
            if (laneSkills.Count == 0) { HideSkillDetail(); return; }
            hoveredLane = lane;
            hoveredQueuePlayer = hoveredQueueIndex = -1;
            PresentSkill(fighter, laneSkills[0]);
        }

        /// <summary>Public reservations can be read before choosing a counter skill.</summary>
        public void ShowQueuedSkillDetail(int player, int queueIndex)
        {
            if (disposed || match == null || match.Phase != LegacyDuelPhase.Planning ||
                player < 0 || player > 1 || IsExitConfirming) return;
            var fighter = match.GetFighter(player);
            if (queueIndex < 0 || queueIndex >= fighter.Queue.Count) { HideSkillDetail(); return; }
            hoveredLane = -1;
            hoveredQueuePlayer = player;
            hoveredQueueIndex = queueIndex;
            PresentSkill(fighter, fighter.Queue[queueIndex]);
        }

        private void PresentSkill(LocalVersusFighter fighter, LegacySkill skill)
        {
            detailIcon.sprite = art.GetSkillIcon(skill.IconId);
            detailIcon.enabled = detailIcon.sprite != null;
            detailName.text = skill.Name;
            bool breathe = skill.IsWait;
            detailInfo.Root.SetActive(!breathe);
            breathDescription.gameObject.SetActive(breathe);
            if (!breathe)
            {
                fighter.EffectivePowerRange(skill, out int min, out int max);
                string power = min == max ? min.ToString() : min + "–" + max;
                detailInfo.SetSkill(skill, cycleUses: fighter.CycleUses(skill.Id),
                    effectiveCost: fighter.EffectiveCost(skill), effectivePower: power);
            }
            detailPanel.gameObject.SetActive(true);
        }

        public void HideSkillDetail()
        {
            hoveredLane = hoveredQueuePlayer = hoveredQueueIndex = -1;
            detailPanel.gameObject.SetActive(false);
        }

        public void SetRematchPending(bool pending)
        {
            if (disposed) return;
            rematchButton.interactable = !pending;
            rematchCaption.text = pending ? "상대 기다리는 중" : "다시 겨루기";
        }

        public void ToggleExitConfirmation()
        {
            if (disposed || !root.gameObject.activeSelf) return;
            exitOverlay.SetActive(!exitOverlay.activeSelf);
            HideSkillDetail();
            ClearSelection();
            if (match != null) Refresh(match, leftClock, rightClock, localPlayer, requestPending);
        }

        public void Hide()
        {
            if (disposed) return;
            SetRematchPending(false);
            exitOverlay.SetActive(false);
            HideSkillDetail();
            root.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (disposed) return;
            root.gameObject.SetActive(true);
            exitOverlay.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Button button in laneButtons) button.onClick.RemoveAllListeners();
            cycleButton.onClick.RemoveAllListeners();
            breathButton.onClick.RemoveAllListeners();
            passButton.onClick.RemoveAllListeners();
            titleButton.onClick.RemoveAllListeners();
            rematchButton.onClick.RemoveAllListeners();
            resultTitleButton.onClick.RemoveAllListeners();
            confirmExitButton.onClick.RemoveAllListeners();
            cancelExitButton.onClick.RemoveAllListeners();
            DestroyObject(root.gameObject);
            if (ownedEventSystem != null) DestroyObject(ownedEventSystem);
        }

        private void BuildStatus(int player)
        {
            float x = player == 0 ? -692f : 692f;
            var panel = Panel("Versus " + (player + 1) + "P Status", root,
                new Vector2(x, 423f), new Vector2(468f, 176f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(panel);
            statusSurfaces[player] = panel;
            statusPanels[player] = panel.rectTransform;
            float direction = player == 0 ? 1f : -1f;
            statusNames[player] = Label("Versus Player Name", panel.transform,
                new Vector2(-155f * direction, 65f),
                new Vector2(110f, 30f), 24, DuelVisualTheme.Foreground);
            statusNames[player].text = (player + 1) + "P";
            timeLabels[player] = Label("Versus Player Clock", panel.transform,
                new Vector2(153f * direction, 65f), new Vector2(122f, 34f), 26, DuelVisualTheme.Accent);
            healthLabels[player] = Label("Versus HP", panel.transform, new Vector2(0f, 28f),
                new Vector2(410f, 22f), 17, DuelVisualTheme.Foreground, TextAnchor.MiddleLeft);
            Bar(panel.transform, "Versus HP", new Vector2(0f, 8f), 410f,
                DuelVisualTheme.Health, out healthFills[player]);
            resistanceLabels[player] = Label("Versus Resistance", panel.transform, new Vector2(0f, -24f),
                new Vector2(410f, 22f), 17, DuelVisualTheme.Foreground, TextAnchor.MiddleLeft);
            Bar(panel.transform, "Versus Resistance", new Vector2(0f, -44f), 410f,
                DuelVisualTheme.Steel, out resistanceFills[player]);
            actLabels[player] = Label("Versus ACT", panel.transform, new Vector2(-110f, -65f),
                new Vector2(180f, 20f), 16, DuelVisualTheme.Accent, TextAnchor.MiddleLeft);
            Bar(panel.transform, "Versus ACT", new Vector2(94f, -65f), 222f,
                DuelVisualTheme.Accent, out actFills[player]);
        }

        private static void Bar(Transform parent, string name, Vector2 position, float width,
            Color fillColor, out Image fill)
        {
            Panel(name + " Track", parent, position, new Vector2(width, 7f), DuelVisualTheme.Track);
            fill = Panel(name + " Fill", parent, position, new Vector2(width, 7f), fillColor);
            fill.sprite = Resources.Load<Sprite>("LegacyHud/white");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
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
            if (caption != null)
                Label("Button Label", image.transform, Vector2.zero, size - new Vector2(12f, 8f),
                    20, primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground).text = caption;
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
            text.supportRichText = false;
            return text;
        }

        private static Image Icon(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            Image result = Panel(name, parent, position, size, Color.white);
            result.sprite = sprite;
            result.enabled = sprite != null;
            return result;
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
            Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
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

        private static string LaneKey(int index) => index == 0 ? "Q" : index == 1 ? "W" : "E";
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

        private sealed class QueueRow
        {
            private const float Room = 532f;
            private readonly int player;
            private readonly LegacyDuelArt art;
            private readonly RectTransform root;
            private readonly Image background;
            private readonly Text heading;
            private bool online;
            private float onlineRoom;
            public RectTransform Root => root;
            private readonly Action<int, int> showDetail;
            private readonly Action hideDetail;
            private readonly List<Image> cardBorders = new List<Image>();
            private readonly List<Image> icons = new List<Image>();
            private readonly List<Text> names = new List<Text>();

            public QueueRow(int player, LegacyDuelArt art, Transform parent,
                Action<int, int> showDetail, Action hideDetail)
            {
                this.player = player;
                this.art = art;
                this.showDetail = showDetail;
                this.hideDetail = hideDetail;
                root = Panel("Versus " + (player + 1) + "P Queue", parent,
                    new Vector2(player == 0 ? -630f : 630f, -288f),
                    new Vector2(580f, 108f), DuelVisualTheme.Surface).rectTransform;
                background = root.GetComponent<Image>();
                DuelVisualTheme.Frame(background);
                heading = Rect("Queue Heading", root, new Vector2(0f, 40f),
                    new Vector2(530f, 26f)).gameObject.AddComponent<Text>();
                heading.font = art.UIFont;
                heading.fontSize = 17;
                heading.color = DuelVisualTheme.Accent;
                heading.alignment = TextAnchor.MiddleLeft;
                heading.raycastTarget = false;
                heading.text = (player + 1) + "P 예약";
            }

            public void SetOnline(bool enabled, int localPlayer)
            {
                online = enabled;
                background.enabled = !enabled;
                Transform trim = root.Find("Brass Trim");
                if (trim != null) trim.gameObject.SetActive(!enabled);
                heading.text = enabled ? (player == localPlayer ? "나 예약" : "상대 예약") :
                    (player + 1) + "P 예약";
                heading.gameObject.SetActive(!enabled);
                if (!enabled)
                    root.anchoredPosition = new Vector2(player == 0 ? -630f : 630f, -288f);
            }

            public void PositionOnline(Vector2 statusCenter, float halfCanvasWidth)
            {
                if (!online) return;
                root.anchoredPosition = statusCenter + new Vector2(player == 0 ? -266f : 266f, 90f);
                onlineRoom = player == 0 ? statusCenter.x + halfCanvasWidth - 54f :
                    halfCanvasWidth - 54f - statusCenter.x;
            }

            public void Refresh(IReadOnlyList<LegacySkill> queue, int activeSlot)
            {
                int count = queue?.Count ?? 0;
                while (icons.Count < count) AddChip();
                float room = online ? Mathf.Min(Room, Mathf.Max(54f, onlineRoom)) : Room;
                float pitch = count < 2 ? 54f : Mathf.Min(54f, room / (count - 1));
                for (int index = 0; index < icons.Count; index++)
                {
                    Image icon = icons[index];
                    RectTransform chip = (RectTransform)icon.transform.parent;
                    bool visible = index < count;
                    chip.gameObject.SetActive(visible);
                    if (!visible) continue;
                    chip.anchoredPosition = new Vector2((player == 0 ? 1f : -1f) *
                        (Room * .5f - index * pitch), -11f);
                    LegacySkill skill = queue[index];
                    icon.sprite = art.GetSkillIcon(skill.IconId);
                    icon.enabled = icon.sprite != null;
                    names[index].text = skill.IsWait ? "쉼" : skill.Name;
                    cardBorders[index].color = index == activeSlot ? DuelVisualTheme.Accent : DuelVisualTheme.Card;
                }
            }

            private void AddChip()
            {
                int chipIndex = icons.Count;
                Image card = Panel("Queued Skill", root, Vector2.zero,
                    new Vector2(50f, 62f), DuelVisualTheme.Card);
                card.raycastTarget = true;
                DuelVisualTheme.Frame(card);
                var hover = card.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => showDetail?.Invoke(player, chipIndex));
                hover.triggers.Add(enter);
                var leave = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                leave.callback.AddListener(_ => hideDetail?.Invoke());
                hover.triggers.Add(leave);
                Image icon = Icon("Icon", card.transform, null, new Vector2(0f, 8f),
                    new Vector2(36f, 36f));
                icon.preserveAspect = true;
                Text name = Rect("Skill Name", card.transform, new Vector2(0f, -23f),
                    new Vector2(46f, 16f)).gameObject.AddComponent<Text>();
                name.font = art.UIFont;
                name.fontSize = 12;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 9;
                name.resizeTextMaxSize = 12;
                name.color = DuelVisualTheme.Foreground;
                name.alignment = TextAnchor.MiddleCenter;
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                name.raycastTarget = false;
                cardBorders.Add(card);
                icons.Add(icon);
                names.Add(name);
            }
        }
    }
}





