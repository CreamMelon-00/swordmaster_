using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed partial class LocalVersusHud
    {
        // The online view keeps the single-player composition. The shared-keyboard layout
        // remains available when localPlayer is -1.
        private readonly RectTransform[] statusPanels = new RectTransform[2];
        private readonly Text[] statusNames = new Text[2];
        private readonly Image[] onlineOwnPlates = new Image[2];
        private readonly RectTransform[] onlineTurnFrames = new RectTransform[2];
        private readonly Image[,] onlineTurnRails = new Image[2, 4];
        private readonly Image[] onlineTurnBadges = new Image[2];
        private readonly Text[] onlineTurnCaptions = new Text[2];
        private readonly Transform[] actors = new Transform[2];
        private readonly RectTransform[] onlineGears = new RectTransform[3];
        private readonly Text[] onlineLaneNames = new Text[3];
        private readonly Text[] onlineLaneKeys = new Text[3];
        private readonly Text[] onlineLaneCosts = new Text[3];
        private readonly Image[] onlineCostPlates = new Image[3];
        private readonly Image[] onlineNextIcons = new Image[3];
        private readonly Image[] onlineUsedIcons = new Image[3];
        private readonly Image[] onlineActDividers = new Image[9];
        private Image onlinePassIcon, onlineBreathIcon;
        private Text cycleLabel;
        private RectTransform commandDock;
        private RectTransform onlineTurnHeader, onlineTurnStatePlate, onlineStagePlate;
        private Text onlineTurnNumber;
        private Image onlineTimerTrack, onlineTimerFill;
        private Text onlineTimerText;
        private Camera actorCamera;
        private bool onlineLayout, requestPending;
        private int lastLayoutPlayer = -2;

        public void SetActorAnchors(Camera camera, Transform leftActor, Transform rightActor)
        {
            actorCamera = camera;
            actors[0] = leftActor;
            actors[1] = rightActor;
        }

        private void BuildOnlinePresentation()
        {
            var statePlate = Panel("Versus Online Turn State", root, Vector2.zero,
                new Vector2(220f, 40f), new Color(DuelVisualTheme.Surface.r,
                    DuelVisualTheme.Surface.g, DuelVisualTheme.Surface.b, .94f));
            DuelVisualTheme.Frame(statePlate);
            onlineTurnStatePlate = statePlate.rectTransform;
            Anchor(onlineTurnStatePlate, new Vector2(.5f, 1f), Vector2.one * .5f);
            onlineTurnStatePlate.anchoredPosition = new Vector2(0f, -158f);
            onlineTurnStatePlate.SetAsFirstSibling();
            onlineTurnStatePlate.gameObject.SetActive(false);
            var stagePlate = Panel("Versus Online Stage", root, Vector2.zero,
                new Vector2(396f, 48f), DuelVisualTheme.Surface);
            DuelVisualTheme.Frame(stagePlate);
            onlineStagePlate = stagePlate.rectTransform;
            Anchor(onlineStagePlate, new Vector2(0f, 1f), new Vector2(0f, 1f));
            onlineStagePlate.anchoredPosition = new Vector2(24f, -12f);
            Label("Versus Online Stage Name", stagePlate.transform, Vector2.zero,
                new Vector2(372f, 32f), 18, DuelVisualTheme.Muted,
                TextAnchor.MiddleLeft).text = "온라인 대전";
            onlineStagePlate.gameObject.SetActive(false);
            onlineTurnHeader = Rect("Versus Turn Header", root, new Vector2(0f, 468f),
                new Vector2(DuelCrossedFlags.PreferredWidth, DuelCrossedFlags.PreferredHeight));
            Rect("Crossed Flags", onlineTurnHeader, Vector2.zero,
                onlineTurnHeader.sizeDelta).gameObject.AddComponent<DuelCrossedFlags>();
            var badge = Panel("Versus Turn Badge", onlineTurnHeader, new Vector2(0f, 4f),
                new Vector2(76f, 56f), DuelVisualTheme.Surface);
            DuelVisualTheme.Frame(badge);
            Label("Turn Label", badge.transform, new Vector2(0f, -18f),
                new Vector2(64f, 16f), 12, DuelVisualTheme.Accent).text = "턴";
            onlineTurnNumber = Label("Versus Turn Number", badge.transform, new Vector2(0f, 4f),
                new Vector2(64f, 32f), 27, DuelVisualTheme.Foreground);
            onlineTurnHeader.gameObject.SetActive(false);

            // The timer and ACT gauges ride the same two rails as the single-player dock.
            Bar(commandDock, "Versus Online Time", new Vector2(0f, 95f), 520f,
                DuelVisualTheme.Accent, out onlineTimerFill);
            onlineTimerTrack = commandDock.Find("Versus Online Time Track").GetComponent<Image>();
            onlineTimerText = Label("Versus Online Clock", commandDock,
                new Vector2(210f, 79f), new Vector2(92f, 24f), 22, DuelVisualTheme.Foreground);
            onlineTimerTrack.gameObject.SetActive(false);
            onlineTimerFill.gameObject.SetActive(false);
            onlineTimerText.gameObject.SetActive(false);
            for (int unit = 1; unit < 10; unit++)
            {
                onlineActDividers[unit - 1] = Panel("Versus Online ACT Divider " + unit,
                    commandDock, new Vector2(-260f + unit * 52f, 60f), new Vector2(2f, 8f),
                    DuelVisualTheme.Surface);
                onlineActDividers[unit - 1].gameObject.SetActive(false);
            }
            cycleLabel = cycleButton.GetComponentInChildren<Text>();
            onlineBreathIcon = Icon("Versus Online Breath Icon", breathButton.transform,
                art.GetSkillIcon(LegacyCommonActions.Breathe.IconId),
                new Vector2(0f, 25f), new Vector2(36f, 36f));
            onlineBreathIcon.preserveAspect = true;
            onlineBreathIcon.gameObject.SetActive(false);
            onlinePassIcon = Icon("Versus Online Pass Icon", passButton.transform,
                Resources.Load<Sprite>("HudActions/confirm-turn"),
                new Vector2(0f, 18f), new Vector2(64f, 64f));
            onlinePassIcon.preserveAspect = true;
            onlinePassIcon.gameObject.SetActive(false);

            for (int lane = 0; lane < onlineGears.Length; lane++)
            {
                float x = (lane - 1) * 184f;
                RectTransform gear = Rect("Versus Skill Gear " + LaneKey(lane), commandDock,
                    new Vector2(x, -80f), new Vector2(176f, 176f));
                gear.gameObject.AddComponent<DuelVersusGearGraphic>().raycastTarget = false;
                gear.SetAsFirstSibling();
                gear.gameObject.SetActive(false);
                onlineGears[lane] = gear;
                onlineLaneKeys[lane] = Label("Versus Online Key", commandDock,
                    new Vector2(x, 37f), new Vector2(70f, 18f), 17, DuelVisualTheme.Accent);
                onlineLaneKeys[lane].text = LaneKey(lane);
                onlineLaneNames[lane] = Label("Versus Online Skill Name", commandDock,
                    new Vector2(x, 17f), new Vector2(150f, 22f), 18, DuelVisualTheme.Foreground);
                onlineLaneNames[lane].resizeTextForBestFit = true;
                onlineLaneNames[lane].resizeTextMinSize = 12;
                onlineLaneNames[lane].resizeTextMaxSize = 18;
                onlineCostPlates[lane] = Panel("Versus Online Cost Plate", commandDock,
                    new Vector2(x, -78f), new Vector2(74f, 22f), DuelVisualTheme.Surface);
                DuelVisualTheme.Frame(onlineCostPlates[lane]);
                onlineLaneCosts[lane] = Label("Versus Online Skill Cost", commandDock,
                    new Vector2(x, -78f), new Vector2(68f, 18f), 15, DuelVisualTheme.Accent);
                onlineNextIcons[lane] = Icon("Versus Next Skill", commandDock, null,
                    new Vector2(x - 57f, -63f), new Vector2(32f, 32f));
                onlineUsedIcons[lane] = Icon("Versus Used Skill", commandDock, null,
                    new Vector2(x + 57f, -63f), new Vector2(28f, 28f));
                onlineUsedIcons[lane].color = new Color(.55f, .52f, .47f, 1f);
                onlineLaneKeys[lane].gameObject.SetActive(false);
                onlineLaneNames[lane].gameObject.SetActive(false);
                onlineLaneCosts[lane].gameObject.SetActive(false);
                onlineCostPlates[lane].gameObject.SetActive(false);
                onlineNextIcons[lane].gameObject.SetActive(false);
                onlineUsedIcons[lane].gameObject.SetActive(false);
            }

            for (int side = 0; side < 2; side++)
            {
                string prefix = "Versus " + (side + 1) + "P ";
                RectTransform panel = statusPanels[side];
                onlineOwnPlates[side] = Panel(prefix + "Identity Plate", panel,
                    new Vector2(61f, 33f), new Vector2(102f, 24f), DuelVisualTheme.Steel);
                onlineOwnPlates[side].rectTransform.SetAsFirstSibling();
                onlineOwnPlates[side].gameObject.SetActive(false);

                var frame = Rect(prefix + "Turn Frame", panel, Vector2.zero,
                    new Vector2(236f, 92f));
                onlineTurnFrames[side] = frame;
                onlineTurnRails[side, 0] = Panel(prefix + "Turn Top", frame,
                    new Vector2(0f, 44f), new Vector2(232f, 4f), DuelVisualTheme.Accent);
                onlineTurnRails[side, 1] = Panel(prefix + "Turn Bottom", frame,
                    new Vector2(0f, -44f), new Vector2(232f, 4f), DuelVisualTheme.Accent);
                onlineTurnRails[side, 2] = Panel(prefix + "Turn Left", frame,
                    new Vector2(-115f, 0f), new Vector2(4f, 88f), DuelVisualTheme.Accent);
                onlineTurnRails[side, 3] = Panel(prefix + "Turn Right", frame,
                    new Vector2(115f, 0f), new Vector2(4f, 88f), DuelVisualTheme.Accent);
                frame.gameObject.SetActive(false);

                onlineTurnBadges[side] = Panel(prefix + "Turn Badge", panel,
                    new Vector2(-56f, 33f), new Vector2(104f, 24f), DuelVisualTheme.Accent);
                onlineTurnCaptions[side] = Label(prefix + "Turn Caption",
                    onlineTurnBadges[side].transform, Vector2.zero,
                    new Vector2(100f, 20f), 15, DuelVisualTheme.Ink);
                onlineTurnCaptions[side].text = "행동 중";
                onlineTurnBadges[side].gameObject.SetActive(false);
                statusNames[side].transform.SetAsLastSibling();
            }
            BuildTurnMarkers();
        }

        private void ConfigurePresentation(int player)
        {
            bool online = player >= 0;
            if (onlineLayout == online && lastLayoutPlayer == player) return;
            onlineLayout = online;
            lastLayoutPlayer = player;
            SetTurnMarkersEnabled(online);
            onlineTurnHeader.gameObject.SetActive(online);
            onlineTurnStatePlate.gameObject.SetActive(online);
            onlineStagePlate.gameObject.SetActive(online);
            roundLabel.gameObject.SetActive(!online);
            phaseLabel.gameObject.SetActive(!online);
            onlineTimerTrack.gameObject.SetActive(online);
            onlineTimerFill.gameObject.SetActive(online);
            onlineTimerText.gameObject.SetActive(online);
            foreach (Image divider in onlineActDividers) divider.gameObject.SetActive(online);
            onlineBreathIcon.gameObject.SetActive(online);
            onlinePassIcon.gameObject.SetActive(online);
            foreach (RectTransform gear in onlineGears) gear.gameObject.SetActive(online);
            for (int lane = 0; lane < 3; lane++)
            {
                onlineLaneKeys[lane].gameObject.SetActive(online);
                onlineLaneNames[lane].gameObject.SetActive(online);
                onlineLaneCosts[lane].gameObject.SetActive(online);
                onlineCostPlates[lane].gameObject.SetActive(online);
                if (!online)
                {
                    onlineNextIcons[lane].gameObject.SetActive(false);
                    onlineUsedIcons[lane].gameObject.SetActive(false);
                }
            }

            if (online)
            {
                Anchor(commandDock, new Vector2(.5f, 0f), new Vector2(.5f, 0f));
                Place(commandDock, new Vector2(0f, 14f), new Vector2(980f, 200f));
                Anchor(onlineTurnHeader, new Vector2(.5f, 1f), Vector2.one * .5f);
                onlineTurnHeader.anchoredPosition = new Vector2(0f, -36f);
                Anchor(activeLabel.rectTransform, new Vector2(.5f, 1f), Vector2.one * .5f);
                Place(activeLabel.rectTransform, new Vector2(0f, -158f), new Vector2(280f, 34f));
                activeLabel.fontSize = 23;
                Anchor(titleButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one);
                Place(titleButton.GetComponent<RectTransform>(), new Vector2(-26f, -18f),
                    new Vector2(172f, 38f));
                Place(commandHeading.rectTransform, new Vector2(-145f, 79f),
                    new Vector2(270f, 22f));
                commandHeading.fontSize = 18;
                Place(commandDock.Find("Versus Active ACT Track") as RectTransform,
                    new Vector2(0f, 60f), new Vector2(520f, 8f));
                Place(commandDock.Find("Versus Active ACT Fill") as RectTransform,
                    new Vector2(0f, 60f), new Vector2(520f, 8f));
                Place(cycleButton.GetComponent<RectTransform>(), new Vector2(-360f, -4f),
                    new Vector2(136f, 112f));
                Place(breathButton.GetComponent<RectTransform>(), new Vector2(360f, -4f),
                    new Vector2(136f, 112f));
                Place(passButton.GetComponent<RectTransform>(), new Vector2(550f, -4f),
                    new Vector2(96f, 112f));
                Place(passLabel.rectTransform, new Vector2(0f, -36f), new Vector2(88f, 38f));
                passLabel.fontSize = 16;
                passLabel.color = DuelVisualTheme.Foreground;
                (passButton.targetGraphic as Image).color = DuelVisualTheme.RaisedSurface;
                Place(cycleLabel.rectTransform, Vector2.zero, new Vector2(124f, 104f));
                cycleLabel.fontSize = 16;
                cycleLabel.text = "넘기기\n맨 앞 기술 뒤로\n[Shift]";
                Place(breathLabel.rectTransform, new Vector2(0f, -23f), new Vector2(124f, 52f));
                breathLabel.fontSize = 16;
                for (int lane = 0; lane < 3; lane++)
                {
                    Place(laneButtons[lane].GetComponent<RectTransform>(),
                        new Vector2((lane - 1) * 184f, -28f), new Vector2(66f, 66f));
                    Place(laneIcons[lane].rectTransform, Vector2.zero,
                        new Vector2(48f, 48f));
                    laneButtons[lane].transform.Find("Versus Lane Key").gameObject.SetActive(false);
                    laneNames[lane].gameObject.SetActive(false);
                    laneCosts[lane].gameObject.SetActive(false);
                }
            }
            else
            {
                Anchor(commandDock, Vector2.one * .5f, Vector2.one * .5f);
                Place(commandDock, new Vector2(0f, -443f), new Vector2(1290f, 180f));
                Anchor(activeLabel.rectTransform, Vector2.one * .5f, Vector2.one * .5f);
                Place(activeLabel.rectTransform, new Vector2(0f, 454f),
                    new Vector2(380f, 54f));
                activeLabel.fontSize = 36;
                Anchor(titleButton.GetComponent<RectTransform>(), Vector2.one * .5f,
                    Vector2.one * .5f);
                Place(titleButton.GetComponent<RectTransform>(), new Vector2(0f, 361f),
                    new Vector2(172f, 38f));
                Place(commandHeading.rectTransform, new Vector2(-446f, 65f),
                    new Vector2(300f, 26f));
                commandHeading.fontSize = 19;
                Place(commandDock.Find("Versus Active ACT Track") as RectTransform,
                    new Vector2(98f, 65f), new Vector2(570f, 7f));
                Place(commandDock.Find("Versus Active ACT Fill") as RectTransform,
                    new Vector2(98f, 65f), new Vector2(570f, 7f));
                Place(cycleButton.GetComponent<RectTransform>(), new Vector2(-548f, -22f),
                    new Vector2(150f, 100f));
                Place(breathButton.GetComponent<RectTransform>(), new Vector2(278f, -22f),
                    new Vector2(174f, 100f));
                Place(passButton.GetComponent<RectTransform>(), new Vector2(515f, -22f),
                    new Vector2(170f, 100f));
                Place(passLabel.rectTransform, Vector2.zero, new Vector2(158f, 92f));
                passLabel.fontSize = 20;
                passLabel.color = DuelVisualTheme.Ink;
                (passButton.targetGraphic as Image).color = DuelVisualTheme.Accent;
                Place(cycleLabel.rectTransform, Vector2.zero, new Vector2(138f, 92f));
                cycleLabel.fontSize = 20;
                cycleLabel.text = "넘기기\n[Shift]";
                Place(breathLabel.rectTransform, Vector2.zero, new Vector2(162f, 92f));
                breathLabel.fontSize = 20;
                for (int lane = 0; lane < 3; lane++)
                {
                    laneButtons[lane].transform.Find("Versus Lane Key").gameObject.SetActive(true);
                    laneNames[lane].gameObject.SetActive(true);
                    laneCosts[lane].gameObject.SetActive(true);
                    Place(laneButtons[lane].GetComponent<RectTransform>(),
                        new Vector2(-335f + lane * 200f, -22f), new Vector2(190f, 100f));
                    Place(laneIcons[lane].rectTransform, new Vector2(-56f, 0f),
                        new Vector2(54f, 54f));
                    Place(laneNames[lane].rectTransform, new Vector2(28f, 3f),
                        new Vector2(102f, 30f));
                    Place(laneCosts[lane].rectTransform, new Vector2(28f, -31f),
                        new Vector2(100f, 20f));
                    Place(laneButtons[lane].transform.Find("Versus Lane Key") as RectTransform,
                        new Vector2(61f, 30f), new Vector2(36f, 26f));
                    laneNames[lane].fontSize = 19;
                    laneNames[lane].resizeTextMaxSize = 19;
                }
            }

            for (int side = 0; side < 2; side++)
            {
                ConfigureStatus(side, online, player);
                queues[side].SetOnline(online, player);
            }
            if (online) PositionOnlineHeadHud();
        }

        private void ConfigureStatus(int side, bool online, int player)
        {
            RectTransform panel = statusPanels[side];
            float direction = side == 0 ? 1f : -1f;
            bool ownSide = online && side == player;
            statusNames[side].text = online ? (ownSide ? "내 캐릭터" : "상대") :
                (side + 1) + "P";
            onlineOwnPlates[side].gameObject.SetActive(ownSide);
            if (!online)
            {
                onlineTurnFrames[side].gameObject.SetActive(false);
                onlineTurnBadges[side].gameObject.SetActive(false);
            }
            timeLabels[side].gameObject.SetActive(!online);
            actLabels[side].gameObject.SetActive(!online);
            panel.Find("Versus ACT Track").gameObject.SetActive(!online);
            panel.Find("Versus ACT Fill").gameObject.SetActive(!online);
            if (online)
            {
                panel.sizeDelta = new Vector2(236f, 92f);
                Place(statusNames[side].rectTransform, new Vector2(61f, 33f),
                    new Vector2(94f, 20f));
                statusNames[side].alignment = TextAnchor.MiddleCenter;
                statusNames[side].fontSize = ownSide ? 17 : 16;
                statusNames[side].color = ownSide ? DuelVisualTheme.Ink :
                    DuelVisualTheme.Muted;
                Place(healthLabels[side].rectTransform, new Vector2(0f, 15f),
                    new Vector2(196f, 18f));
                healthLabels[side].fontSize = 14;
                Place(resistanceLabels[side].rectTransform, new Vector2(0f, -13f),
                    new Vector2(196f, 18f));
                resistanceLabels[side].fontSize = 14;
                Place(panel.Find("Versus HP Track") as RectTransform,
                    new Vector2(0f, 3f), new Vector2(196f, 9f));
                Place(panel.Find("Versus HP Fill") as RectTransform,
                    new Vector2(0f, 3f), new Vector2(196f, 9f));
                Place(panel.Find("Versus Resistance Track") as RectTransform,
                    new Vector2(0f, -27f), new Vector2(196f, 6f));
                Place(panel.Find("Versus Resistance Fill") as RectTransform,
                    new Vector2(0f, -27f), new Vector2(196f, 6f));
            }
            else
            {
                Place(panel, new Vector2(side == 0 ? -692f : 692f, 423f),
                    new Vector2(468f, 176f));
                Place(statusNames[side].rectTransform, new Vector2(-155f * direction, 65f),
                    new Vector2(110f, 30f));
                statusNames[side].alignment = TextAnchor.MiddleCenter;
                statusNames[side].fontSize = 24;
                statusNames[side].color = DuelVisualTheme.Foreground;
                Place(healthLabels[side].rectTransform, new Vector2(0f, 28f),
                    new Vector2(410f, 22f));
                healthLabels[side].fontSize = 17;
                Place(resistanceLabels[side].rectTransform, new Vector2(0f, -24f),
                    new Vector2(410f, 22f));
                resistanceLabels[side].fontSize = 17;
                Place(panel.Find("Versus HP Track") as RectTransform,
                    new Vector2(0f, 8f), new Vector2(410f, 7f));
                Place(panel.Find("Versus HP Fill") as RectTransform,
                    new Vector2(0f, 8f), new Vector2(410f, 7f));
                Place(panel.Find("Versus Resistance Track") as RectTransform,
                    new Vector2(0f, -44f), new Vector2(410f, 7f));
                Place(panel.Find("Versus Resistance Fill") as RectTransform,
                    new Vector2(0f, -44f), new Vector2(410f, 7f));
            }
        }

        private void PositionOnlineHeadHud()
        {
            if (!onlineLayout) return;
            Vector2 half = root.rect.size * .5f;
            if (half.x < 1f || half.y < 1f) half = new Vector2(960f, 540f);
            for (int side = 0; side < 2; side++)
            {
                Vector2 center = new Vector2(side == 0 ? -430f : 430f, 145f);
                Transform actor = actors[side];
                if (actorCamera != null && actor != null)
                {
                    Vector3 anchor = side == 0 ? new Vector3(-.6f, 1.1f, 0f) :
                        new Vector3(-.4f, 1f, 0f);
                    Vector3 screen = actorCamera.WorldToScreenPoint(actor.TransformPoint(anchor));
                    if (screen.z > 0f &&
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            root, screen, null, out Vector2 projected))
                        center = projected + Vector2.up * 58f;
                }
                center.x = side == 0 ? Mathf.Clamp(center.x, -half.x + 128f, -210f) :
                    Mathf.Clamp(center.x, 210f, half.x - 128f);
                center.y = Mathf.Clamp(center.y, -175f, half.y - 240f);
                statusPanels[side].anchoredPosition = center;
                queues[side].PositionOnline(center, half.x);
            }
        }

        private void RefreshOnlineLane(int lane, System.Collections.Generic.IReadOnlyList<LegacySkill> skills,
            int cost)
        {
            LegacySkill current = skills != null && skills.Count > 0 ? skills[0] : null;
            onlineLaneNames[lane].text = current?.Name ?? "기술 없음";
            onlineLaneCosts[lane].text = current != null ? cost + " ACT" : string.Empty;
            bool hasNext = skills != null && skills.Count > 1;
            onlineNextIcons[lane].gameObject.SetActive(hasNext);
            onlineUsedIcons[lane].gameObject.SetActive(hasNext);
            if (hasNext)
            {
                onlineNextIcons[lane].sprite = art.GetSkillIcon(skills[1].IconId);
                onlineNextIcons[lane].enabled = onlineNextIcons[lane].sprite != null;
                onlineUsedIcons[lane].sprite = art.GetSkillIcon(skills[skills.Count - 1].IconId);
                onlineUsedIcons[lane].enabled = onlineUsedIcons[lane].sprite != null;
            }
        }

        private void RefreshOnlinePresentation(LocalVersusMatch current, bool planning)
        {
            PositionOnlineHeadHud();
            onlineTurnNumber.text = current.RoundNumber.ToString();
            int planner = current.CurrentPlanner;
            float clock = planner == 0 ? leftClock : planner == 1 ? rightClock : 0f;
            onlineTimerText.text = planning ? Mathf.Max(0f, clock).ToString("0.0") + "s" : "-";
            onlineTimerText.color = planning && clock <= 5f ?
                DuelVisualTheme.Danger : DuelVisualTheme.Foreground;
            onlineTimerFill.fillAmount = planning ? Mathf.Clamp01(clock / LocalVersusController.PlanningSeconds) : 0f;
            onlineTimerFill.color = planning && clock <= 5f ?
                DuelVisualTheme.Danger : DuelVisualTheme.Accent;
            for (int side = 0; side < 2; side++)
            {
                bool acting = planning && planner == side;
                onlineTurnFrames[side].gameObject.SetActive(acting);
                onlineTurnBadges[side].gameObject.SetActive(acting);
                if (!acting) continue;
                Color cue = side == localPlayer ? DuelVisualTheme.Accent :
                    DuelVisualTheme.Danger;
                onlineTurnBadges[side].color = cue;
                for (int edge = 0; edge < 4; edge++)
                    onlineTurnRails[side, edge].color = cue;
            }
            RefreshTurnMarkers(current, planning);
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }

    /// <summary>A quiet brass wheel behind the online skill windows, matching the single-player gear dock.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelVersusGearGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            float radius = Mathf.Min(bounds.width, bounds.height) * .48f;
            if (radius < 12f) return;
            Vector2 center = bounds.center;
            const int segments = 48;
            Color shadow = DuelVisualTheme.Ink * color;
            Color brass = DuelVisualTheme.Border * color;
            Color highlight = DuelVisualTheme.Accent * color;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Ring(vertices, center, radius * .76f, radius * .86f, a, b, shadow);
                Ring(vertices, center, radius * .82f, radius * .91f, a, b, brass);
                if (i % 4 == 0)
                    Ring(vertices, center, radius * .86f, radius, a, b, brass);
                if (i % 8 == 0)
                    Ring(vertices, center, radius * .24f, radius * .82f, a, b, brass);
                Ring(vertices, center, radius * .18f, radius * .25f, a, b, highlight);
            }
        }

        private static void Ring(VertexHelper vertices, Vector2 center, float inner, float outer,
            float a, float b, Color tint)
        {
            Vector2 ai = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner;
            Vector2 ao = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer;
            Vector2 bi = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * inner;
            Vector2 bo = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * outer;
            int index = vertices.currentVertCount;
            vertices.AddVert(ai, tint, Vector2.zero);
            vertices.AddVert(bi, tint, Vector2.zero);
            vertices.AddVert(bo, tint, Vector2.zero);
            vertices.AddVert(ao, tint, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }
    }
}
