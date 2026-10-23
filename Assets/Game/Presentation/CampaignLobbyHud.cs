using System;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public enum LobbyTab { Home, Stages, Loadout, Curriculum }

    /// <summary>Full-screen lobby pages. CampaignRun owns progression; view states survive page changes.
    /// Until <see cref="CampaignRun.IsCurriculumOpen"/> the curriculum does not exist here: no tab, link, 수련 점수 or points
    /// line (they bank unseen until then).</summary>
    public sealed class CampaignLobbyHud : IDisposable
    {
        /// <summary>What a lobby mission that cannot be played yet is called: its title stays hidden until then.</summary>
        public const string HiddenMissionTitle = "???";
        private const float TabWidth = 1300f;
        private const float TabHeight = 850f;
        // The eight stops occupy an uneven, winding route instead of a catalogue grid.
        private static readonly Vector2[] StageRoute =
        {
            new Vector2(-760f, 188f), new Vector2(-486f, 226f),
            new Vector2(-215f, 162f), new Vector2(37f, 54f),
            new Vector2(-226f, -51f), new Vector2(-506f, -18f),
            new Vector2(-758f, -210f), new Vector2(-444f, -269f)
        };
        private readonly LegacyDuelArt art;
        private readonly Action<int> unequip, startStage, equipCounter;
        private readonly Action<string> learnCurriculumNode;
        private readonly Action<int, int, int> placeLoadoutSkill;
        private readonly Action restartJourney, saveLoadout, resetLoadout, resetCurriculum, openMission, startTraining;
        // The next story mission after the 서막, and whether its stage is cleared so it can be played now.
        private PrologueMission nextMission;
        private bool nextMissionAvailable;
        private readonly CampaignLoadoutHud.ViewState loadoutState = new CampaignLoadoutHud.ViewState();
        private readonly CampaignCurriculumHud.ViewState curriculumState = new CampaignCurriculumHud.ViewState();
        private CampaignLoadoutHud loadoutHud;
        private CampaignCurriculumHud curriculumHud;
        private readonly RectTransform root, dynamicRoot;
        private readonly Image roomBackground, bedroomShade;
        private readonly LobbyScreenTransition screenTransition;
        private RectTransform pageRoot;
        private readonly Sprite roomSprite;
        private readonly bool assetsAvailable;
        private CampaignRun currentRun;
        private bool disposed, resetArmed, awaitingStageOutcome, fieldMenuMode;
        // The header note after a stage battle: its outcome. Or a one-time arrival note (the 서막's end: what it opened),
        // which may run to two lines.
        private string outcomeBanner;
        // An arrival note waiting for the next Show (SetArrivalNotice).
        private string arrivalNotice;
        private int selectedStageNumber = 1;
        private LobbyTab currentTab;

        private float LedgerWidth => Mathf.Min(1600f, Mathf.Max(0f, pageRoot.rect.width - 120f));
        private float LedgerHeight => Mathf.Min(currentTab == LobbyTab.Stages ? 760f : 940f,
            Mathf.Max(0f, pageRoot.rect.height - 40f));

        private static Color Header => DuelVisualTheme.Surface;
        private static Color Surface => DuelVisualTheme.Surface;
        private static Color SurfaceInner => DuelVisualTheme.RaisedSurface;
        private static Color Card => DuelVisualTheme.Card;
        private static Color CardSelected => DuelVisualTheme.Selected;
        private static Color Border => DuelVisualTheme.Border;
        private static Color Accent => DuelVisualTheme.Accent;
        private static Color Gold => DuelVisualTheme.Accent;
        private static Color Foreground => DuelVisualTheme.Foreground;
        private static Color Muted => DuelVisualTheme.Muted;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public bool HasRequiredAssets => assetsAvailable;
        public int SelectedStageNumber => selectedStageNumber;
        public LobbyTab CurrentTab => currentTab;
        public bool IsFieldMenuMode => fieldMenuMode;
        public RectTransform CurrentPage => pageRoot;
        public bool IsTransitioning => !disposed && screenTransition.IsTransitioning;

        public CampaignLobbyHud(Transform parent, LegacyDuelArt art, Action<string> learnCurriculumNode,
            Action resetCurriculum, Action<int> equip, Action<int> unequip, Action<int, int> move,
            Action<int> startStage, Action restartJourney,
            Action<int, int, int> placeLoadoutSkill = null, Action saveLoadout = null,
            Action resetLoadout = null, Action openMission = null, Sprite selectedRoomSprite = null,
            Action startTraining = null, Action<int> equipCounter = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.learnCurriculumNode = learnCurriculumNode;
            this.resetCurriculum = resetCurriculum;
            this.unequip = unequip;
            this.startStage = startStage;
            this.restartJourney = restartJourney;
            this.placeLoadoutSkill = placeLoadoutSkill;
            this.saveLoadout = saveLoadout;
            this.resetLoadout = resetLoadout;
            this.openMission = openMission;
            this.startTraining = startTraining;
            this.equipCounter = equipCounter;

            roomSprite = selectedRoomSprite != null ? selectedRoomSprite : LobbyRoomBackdrop.PickRandom();
            bool complete = roomSprite != null && art.UIFont != null;
            for (int icon = 1; icon <= LegacyDuelArt.SkillIconCount; icon++)
                complete &= art.GetSkillIcon(icon) != null;
            assetsAvailable = complete;

            root = Rect("Campaign Lobby HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 200;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            screenTransition = root.gameObject.AddComponent<LobbyScreenTransition>();

            var room = Image("Bedroom Background", root, roomSprite, Vector2.zero, new Vector2(1920f, 1080f));
            roomBackground = room;
            room.preserveAspect = false;
            var fitter = room.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = roomSprite != null && roomSprite.rect.height > 0f
                ? roomSprite.rect.width / roomSprite.rect.height : 16f / 9f;
            Color shadeColor = DuelVisualTheme.Track;
            shadeColor.a = .16f;
            var shade = Panel("Bedroom Shade", root, Vector2.zero, Vector2.zero, shadeColor);
            bedroomShade = shade;
            Stretch(shade.rectTransform);
            dynamicRoot = Rect("Lobby Dynamic UI", root, Vector2.zero, Vector2.zero);
            Stretch(dynamicRoot);
            Hide();
        }

        public void Show(CampaignRun run)
        {
            if (disposed) throw new ObjectDisposedException(nameof(CampaignLobbyHud));
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.Phase == CampaignPhase.Battle)
            {
                currentRun = run;
                awaitingStageOutcome = !run.IsTrainingBattle;
                if (run.IsTrainingBattle) outcomeBanner = null;
                Hide();
                return;
            }

            ClearSelection();
            bool changedRun = currentRun != null && !ReferenceEquals(currentRun, run);
            currentRun = run;
            if (changedRun)
            {
                selectedStageNumber = 1;
                awaitingStageOutcome = false;
                outcomeBanner = null;
                resetArmed = false;
                loadoutState.Reset();
                curriculumHud?.Dispose();
                curriculumHud = null;
                curriculumState.Reset();
            }
            selectedStageNumber = Mathf.Clamp(selectedStageNumber, 1, Mathf.Max(1, run.HighestUnlockedStage));
            if (awaitingStageOutcome)
            {
                outcomeBanner = run.LastOutcome == Runtime.Combat.DuelMatchOutcome.PlayerVictory ? "승리" : "전투 종료";
                awaitingStageOutcome = false;
            }
            if (arrivalNotice != null)
            {
                outcomeBanner = arrivalNotice;
                arrivalNotice = null;
            }

            root.gameObject.SetActive(true);
            Rebuild();
        }

        /// <summary>A one-time note for the lobby's next <see cref="Show"/> (the 서막's end, which has no result screen, says
        /// what it opened). It takes the status slip's outcome line, one or two lines, until a stage battle's outcome
        /// replaces it, a training battle or <see cref="ResetView"/> clears it. It is never saved. Null or empty cancels a
        /// note that has not been shown yet.</summary>
        public void SetArrivalNotice(string notice)
        {
            if (disposed) return;
            arrivalNotice = string.IsNullOrEmpty(notice) ? null : notice;
        }

        /// <summary>The next lobby mission (null when none) and whether it can be played now. Shown on Home and Stages.</summary>
        public void SetNextMission(PrologueMission mission, bool available)
        {
            if (disposed) return;
            nextMission = mission;
            nextMissionAvailable = mission != null && available;
        }

        /// <summary>A node the controller has just learned (its API, which the learn button also reaches): when the
        /// curriculum page is up, the page the next <see cref="Show"/> builds plays its moment (the card's pulse, the banner).
        /// Otherwise nothing waits for a later page.</summary>
        public void PresentCurriculumLearned(string nodeId)
        {
            if (disposed || string.IsNullOrEmpty(nodeId) || !IsVisible || currentTab != LobbyTab.Curriculum) return;
            curriculumState.PendingLearn = nodeId;
        }

        public PrologueMission NextMission => nextMission;
        public bool IsNextMissionAvailable => nextMissionAvailable;

        /// <summary>Use the existing preparation pages as an overlay over the explorable field.</summary>
        public void SetFieldMenuMode(bool enabled)
        {
            if (disposed || fieldMenuMode == enabled) return;
            fieldMenuMode = enabled;
            roomBackground.gameObject.SetActive(!enabled);
            bedroomShade.color = enabled ? new Color(.03f, .02f, .015f, .68f)
                : new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .16f);
            bedroomShade.raycastTarget = enabled;
            if (enabled && (currentTab == LobbyTab.Home || currentTab == LobbyTab.Stages ||
                            currentTab == LobbyTab.Curriculum && currentRun != null && !currentRun.IsCurriculumOpen))
                currentTab = LobbyTab.Loadout;
            else if (!enabled) currentTab = LobbyTab.Home;
            if (IsVisible && currentRun != null) Rebuild();
        }

        public void ShowFieldMenu(CampaignRun run)
        {
            SetFieldMenuMode(true);
            Show(run);
        }

        public void ShowTab(LobbyTab tab)
        {
            if (disposed) throw new ObjectDisposedException(nameof(CampaignLobbyHud));
            if (fieldMenuMode && (tab == LobbyTab.Home || tab == LobbyTab.Stages)) tab = LobbyTab.Loadout;
            // A closed curriculum has no page; return to the current mode's default page.
            if (tab == LobbyTab.Curriculum && currentRun != null && !currentRun.IsCurriculumOpen)
                tab = fieldMenuMode ? LobbyTab.Loadout : LobbyTab.Home;
            ClearSelection();
            bool changed = currentTab != tab;
            int direction = tab.CompareTo(currentTab);
            currentTab = tab;
            resetArmed = false;
            if (IsVisible && currentRun != null) Rebuild(changed, direction);
        }

        public void ResetView()
        {
            if (disposed) return;
            ClearSelection();
            awaitingStageOutcome = false;
            outcomeBanner = arrivalNotice = null;
            resetArmed = false;
            selectedStageNumber = 1;
            loadoutState.Reset();
            curriculumHud?.Dispose();
            curriculumHud = null;
            curriculumState.Reset();
            currentTab = fieldMenuMode ? LobbyTab.Loadout : LobbyTab.Home;
            if (IsVisible && currentRun != null && currentRun.Phase != CampaignPhase.Battle) Rebuild();
        }

        public bool SelectStage(int stageNumber)
        {
            if (disposed) throw new ObjectDisposedException(nameof(CampaignLobbyHud));
            if (currentRun == null || stageNumber < 1 || stageNumber > currentRun.StageCount
                || stageNumber > currentRun.HighestUnlockedStage) return false;
            ClearSelection();
            selectedStageNumber = stageNumber;
            if (IsVisible && currentTab == LobbyTab.Stages) Rebuild();
            return true;
        }

        public void Hide()
        {
            if (disposed) return;
            screenTransition.Finish();
            if (currentRun != null && currentRun.Phase == CampaignPhase.Battle)
            {
                awaitingStageOutcome = !currentRun.IsTrainingBattle;
                if (currentRun.IsTrainingBattle) outcomeBanner = null;
            }
            ClearSelection();
            loadoutHud?.Dispose();
            loadoutHud = null;
            curriculumHud?.Dispose();
            curriculumHud = null;
            root.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            Hide();
            disposed = true;
            Destroy(root.gameObject);
        }

        private void Rebuild(bool animate = false, int direction = 1)
        {
            if (currentRun == null) return;
            // The tab may have been chosen for another run, or before this one's curriculum closed.
            if (fieldMenuMode && (currentTab == LobbyTab.Home || currentTab == LobbyTab.Stages))
                currentTab = LobbyTab.Loadout;
            if (currentTab == LobbyTab.Curriculum && !currentRun.IsCurriculumOpen)
                currentTab = fieldMenuMode ? LobbyTab.Loadout : LobbyTab.Home;
            ClearDynamicRoot();
            BuildHeader(currentRun);
            pageRoot = Rect("Lobby Page", dynamicRoot, Vector2.zero, Vector2.zero);
            Stretch(pageRoot);
            pageRoot.sizeDelta = new Vector2(0f, -100f);
            pageRoot.anchoredPosition = new Vector2(0f, -50f);
            Canvas.ForceUpdateCanvases();
            if (currentTab != LobbyTab.Home)
            {
                Color pageColor = Surface;
                pageColor.a = .46f;
                var background = Panel("Page Background", pageRoot, Vector2.zero, Vector2.zero, pageColor);
                Stretch(background.rectTransform);
                var ledger = Panel("Page Ledger", pageRoot, new Vector2(0f, -8f),
                    new Vector2(LedgerWidth, LedgerHeight), Surface);
                DuelVisualTheme.Frame(ledger);
            }
            switch (currentTab)
            {
                case LobbyTab.Stages: BuildStages(currentRun); break;
                case LobbyTab.Loadout: BuildLoadout(currentRun); break;
                case LobbyTab.Curriculum: BuildCurriculum(currentRun); break;
                default: BuildHome(currentRun); break;
            }
            Canvas.ForceUpdateCanvases();
            if (animate) screenTransition.Play(pageRoot, direction);
        }

        private void BuildHeader(CampaignRun run)
        {
            if (fieldMenuMode)
            {
                var fieldHeader = Panel("Field Menu Header", dynamicRoot, Vector2.zero, new Vector2(820f, 82f), Header);
                fieldHeader.rectTransform.anchorMin = fieldHeader.rectTransform.anchorMax = new Vector2(0f, 1f);
                fieldHeader.rectTransform.pivot = new Vector2(0f, 1f);
                fieldHeader.rectTransform.anchoredPosition = new Vector2(24f, -18f);
                DuelVisualTheme.DressPanel(fieldHeader);
                Label("Field Menu Title", fieldHeader.transform, "준비", new Vector2(-320f, 0f),
                    new Vector2(180f, 38f), 28);
                BuildTabButton(fieldHeader.transform, LobbyTab.Loadout, "편성", -54f);
                if (run.IsCurriculumOpen)
                    BuildTabButton(fieldHeader.transform, LobbyTab.Curriculum, "커리큘럼", 72f);

                Button close = Button("Field Menu Close", dynamicRoot, "닫기  [Esc]", Vector2.zero,
                    new Vector2(180f, 52f), true, Hide);
                RectTransform closeRect = close.GetComponent<RectTransform>();
                closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
                closeRect.anchoredPosition = new Vector2(-24f, -18f);
                return;
            }
            var header = Panel("Lobby Header", dynamicRoot, Vector2.zero, new Vector2(820f, 82f), Header);
            header.rectTransform.anchorMin = header.rectTransform.anchorMax = new Vector2(0f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(24f, -18f);
            DuelVisualTheme.DressPanel(header);
            AcademyUiSkin.AddCrest(header.transform, new Vector2(-360f, 0f), 48f);
            Label("Lobby Title", header.transform, "기사학교", new Vector2(-320f, 14f), new Vector2(180f, 38f), 28);
            Label("Campaign Progress", header.transform,
                $"진행 {run.ClearedStageCount}/{run.StageCount} · 개방 {run.HighestUnlockedStage}/{run.StageCount}",
                new Vector2(-320f, -20f), new Vector2(180f, 24f), 14, Muted);

            BuildTabButton(header.transform, LobbyTab.Home, "방", -54f);
            BuildTabButton(header.transform, LobbyTab.Stages, "출정", 72f);
            BuildTabButton(header.transform, LobbyTab.Loadout, "편성", 198f);
            if (run.IsCurriculumOpen)
                BuildTabButton(header.transform, LobbyTab.Curriculum, "커리큘럼", 324f);

            bool showOutcome = currentTab != LobbyTab.Loadout && currentTab != LobbyTab.Curriculum && outcomeBanner != null;
            if (!run.IsCurriculumOpen && !showOutcome) return;
            var status = Panel("Lobby Status Slip", dynamicRoot, Vector2.zero, new Vector2(450f, 82f), SurfaceInner);
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(1f, 1f);
            status.rectTransform.pivot = new Vector2(1f, 1f);
            status.rectTransform.anchoredPosition = new Vector2(-24f, -18f);
            DuelVisualTheme.Frame(status);
            // A two-line note (the 서막's end) takes more of the slip's height; the slip itself keeps its size.
            bool twoLines = showOutcome && outcomeBanner.IndexOf('\n') >= 0;
            if (run.IsCurriculumOpen)
            {
                // The balance, in brass while it buys a node, muted otherwise.
                Label("Header Curriculum", status.transform, CampaignCurriculumHud.PointsText(run.CurriculumPoints),
                    new Vector2(0f, showOutcome ? twoLines ? 22f : 17f : 0f), new Vector2(402f, 30f), 18,
                    run.AffordableCurriculumNodeCount > 0 ? Gold : Muted, TextAnchor.MiddleRight);
            }
            if (showOutcome)
                Label("Outcome Banner", status.transform, outcomeBanner,
                    new Vector2(0f, !run.IsCurriculumOpen ? 0f : twoLines ? -15f : -19f),
                    new Vector2(402f, !twoLines ? 28f : run.IsCurriculumOpen ? 44f : 64f), 15,
                    Muted, TextAnchor.MiddleRight);
        }

        private void BuildTabButton(Transform parent, LobbyTab tab, string caption, float x)
        {
            bool selected = currentTab == tab;
            Button("Tab " + tab, parent, caption, new Vector2(x, 0f), new Vector2(116f, 48f), true,
                () => ShowTab(tab), selected, selected ? DuelVisualTheme.Ink : Foreground);
        }

        private void BuildHome(CampaignRun run)
        {
            // Keep the middle of the room clear. Preparation and departure live at opposite edges.
            bool hasCurriculum = run.IsCurriculumOpen;
            bool hasTraining = run.IsTrainingUnlocked;
            float upperShift = hasTraining ? 28f : 0f;
            var inner = Panel("Home Sidebar", pageRoot, Vector2.zero,
                new Vector2(300f, (hasCurriculum ? 270f : 214f) + (hasTraining ? 58f : 0f)), Surface);
            AnchorHomePanel(inner.rectTransform, false, 32f, 36f);
            DuelVisualTheme.Frame(inner);
            Label("Home Heading", inner.transform, "출정 준비",
                new Vector2(-126f, (hasCurriculum ? 94f : 67f) + upperShift), new Vector2(252f, 36f), 25);
            Label("Home Hint", inner.transform, "기술을 정비하고 출발하세요.",
                new Vector2(-126f, (hasCurriculum ? 51f : 29f) + upperShift), new Vector2(252f, 30f), 16, Muted);
            Rule("Home Rule", inner.transform, (hasCurriculum ? 24f : 2f) + upperShift, 252f);
            Button("Home Open Loadout", inner.transform, "기술 편성",
                new Vector2(0f, (hasCurriculum ? -20f : -37f) + upperShift), new Vector2(252f, 44f), true,
                () => ShowTab(LobbyTab.Loadout));
            if (hasCurriculum)
                Button("Home Open Curriculum", inner.transform, "커리큘럼", new Vector2(0f, -72f + upperShift), new Vector2(252f, 42f), true,
                    () => ShowTab(LobbyTab.Curriculum));
            if (hasTraining)
                Button("Home Open Training", inner.transform,
                    run.HasLoadoutChanges ? "편성 저장 후 수련" : "허수아비 수련",
                    new Vector2(0f, hasCurriculum ? -96f : -61f), new Vector2(252f, 44f),
                    run.CanStartTraining, () => startTraining?.Invoke());
            if (restartJourney != null)
                Button("Reset Journey", inner.transform, resetArmed ? "정말 초기화" : "여정 초기화",
                    new Vector2(0f, (hasCurriculum ? -114f : -82f) - upperShift), new Vector2(252f, 28f), true,
                    ResetJourneyClicked, false, Muted);

            var journey = Panel("Home Journey", pageRoot, Vector2.zero, new Vector2(510f, 230f), SurfaceInner);
            AnchorHomePanel(journey.rectTransform, true, 32f, 122f);
            DuelVisualTheme.DressPanel(journey);
            CampaignStage destination = run.GetStage(Mathf.Clamp(run.HighestUnlockedStage, 1, run.StageCount));
            Label("Home Welcome", journey.transform,
                run.ClearedStageCount >= run.StageCount ? "완주한 여정" : "다음 목적지", new Vector2(-228f, 76f),
                new Vector2(456f, 32f), 21, Gold);
            var destinationName = Label("Home Destination", journey.transform, $"{destination.Number:00}  ·  {destination.Name}",
                new Vector2(-228f, 26f), new Vector2(456f, 52f), 35, Foreground);
            destinationName.resizeTextForBestFit = true;
            destinationName.resizeTextMinSize = 27;
            destinationName.resizeTextMaxSize = 35;
            Label("Home Welcome Hint", journey.transform, "출정 지도에서 상대를 확인하세요.",
                new Vector2(-228f, -23f), new Vector2(456f, 34f), 16, Muted);
            Panel("Home Journey Rule", journey.transform, new Vector2(0f, -53f), new Vector2(456f, 2f), Border);
            Button("Home Open Stages", journey.transform, "출정 지도  ▶", new Vector2(124f, -84f),
                new Vector2(212f, 52f), true, () =>
                {
                    SelectStage(destination.Number);
                    ShowTab(LobbyTab.Stages);
                }, true);
            if (nextMission != null)
            {
                var mission = BuildMissionBanner(pageRoot, "Home Mission", Vector2.zero);
                // A locked mission is a footnote; an available mission deserves the upper alert position.
                AnchorHomePanel(mission.rectTransform, true, 32f, nextMissionAvailable ? 366f : 36f);
            }
        }

        private static void AnchorHomePanel(RectTransform panel, bool right, float edge, float bottom)
        {
            panel.anchorMin = panel.anchorMax = new Vector2(right ? 1f : 0f, 0f);
            panel.anchoredPosition = new Vector2((right ? -1f : 1f) * (edge + panel.rect.width * .5f),
                bottom + panel.rect.height * .5f);
        }

        /// <summary>The next lobby mission, with a briefing button once its stage is cleared. Until then its title is
        /// hidden (<see cref="HiddenMissionTitle"/>), since the title would give away what it opens.</summary>
        private Image BuildMissionBanner(Transform parent, string name, Vector2 position)
        {
            float height = nextMissionAvailable ? 100f : 72f;
            var border = Panel(name + " Border", parent, position, new Vector2(510f, height), nextMissionAvailable ? Accent : Border);
            var banner = Panel(name + " Banner", border.transform, Vector2.zero, new Vector2(506f, height - 4f), SurfaceInner);
            DuelVisualTheme.Frame(banner);
            var missionTitle = Label(name + " Title", banner.transform, $"{nextMission.Chapter}  ·  임무 {nextMission.Number}  ·  {NextMissionTitle}",
                new Vector2(-226f, nextMissionAvailable ? 22f : 15f), new Vector2(300f, 28f),
                nextMissionAvailable ? 18 : 16, nextMissionAvailable ? Gold : Muted);
            missionTitle.resizeTextForBestFit = true;
            missionTitle.resizeTextMinSize = 14;
            missionTitle.resizeTextMaxSize = nextMissionAvailable ? 18 : 16;
            string requirement = nextMissionAvailable ? "새 임무가 도착했습니다."
                : KoreanParticle.Attach($"스테이지 {nextMission.RequiredClearedStage:00}", "을") + " 클리어하면 열립니다.";
            Label(name + " State", banner.transform, requirement,
                new Vector2(-226f, nextMissionAvailable ? -15f : -13f), new Vector2(300f, 24f),
                nextMissionAvailable ? 15 : 14, Foreground);
            Button(name + " Open", banner.transform, nextMissionAvailable ? "임무 브리핑" : "잠긴 임무", new Vector2(164f, 0f),
                new Vector2(150f, nextMissionAvailable ? 50f : 40f), nextMissionAvailable,
                () => openMission?.Invoke(), nextMissionAvailable);
            return border;
        }

        private void BuildStages(CampaignRun run)
        {
            var panel = Rect("Stages Panel", pageRoot, new Vector2(0f, -24f), new Vector2(1800f, 850f));
            panel.localScale = Vector3.one * Mathf.Min(1f, LedgerWidth / 1800f);
            Label("Tab Heading", panel, "출정 기록", new Vector2(-844f, 357f), new Vector2(730f, 54f), 38);
            Label("Tab Subtitle", panel, "목적지를 고르면 오른쪽에서 상대 정보를 확인할 수 있습니다.",
                new Vector2(-844f, 312f), new Vector2(1130f, 34f), 18, Muted);
            if (nextMission != null && nextMissionAvailable)
                Button("Stages Open Mission", panel, $"새 임무  {nextMission.Title}  ▶", new Vector2(630f, 345f),
                    new Vector2(444f, 54f), true, () => openMission?.Invoke(), true);

            // Draw the route first so that all stage tickets stay in front of its lines.
            for (int number = 2; number <= run.StageCount && number <= StageRoute.Length; number++)
                ConnectStageStops(panel, number - 1, StageRoute[number - 2], StageRoute[number - 1],
                    number <= run.HighestUnlockedStage);
            for (int number = 1; number <= run.StageCount && number <= StageRoute.Length; number++)
            {
                bool isSelected = number == selectedStageNumber;
                bool frontier = number == run.HighestUnlockedStage && !run.IsStageCleared(number);
                Vector2 size = isSelected ? new Vector2(222f, 130f)
                    : frontier ? new Vector2(208f, 120f) : new Vector2(194f, 106f);
                BuildStageCard(panel, run, number, StageRoute[number - 1], size);
            }

            CampaignStage stage = run.GetStage(selectedStageNumber);
            var previewBorder = Panel("Selected Stage Preview Border", panel, new Vector2(630f, -45f),
                new Vector2(444f, 568f), selectedStageNumber <= run.HighestUnlockedStage ? Accent : Border);
            var preview = Panel("Selected Stage Preview", previewBorder.transform, Vector2.zero,
                new Vector2(440f, 564f), SurfaceInner);
            DuelVisualTheme.DressPanel(preview);
            var landscape = Image("Selected Stage Landscape", preview.transform,
                Resources.Load<Sprite>(stage.IsBossEncounter ? "ForestArena/forest-far" : "SchoolCorridor/corridor-composite"),
                new Vector2(0f, 161f), new Vector2(392f, 170f));
            landscape.preserveAspect = true;
            // The landscape is decorative; missing optional scenery does not affect stage selection.
            landscape.gameObject.SetActive(landscape.sprite != null);
            var opponent = Image("Selected Stage Enemy Silhouette", preview.transform,
                Resources.Load<Sprite>(CampaignEnemyVariant.SilhouetteResource(stage.Number)),
                new Vector2(96f, 161f), new Vector2(196f, 170f));
            opponent.preserveAspect = true;
            opponent.color = MissionBriefingHud.SilhouetteColor;
            opponent.enabled = opponent.sprite != null;
            opponent.raycastTarget = false;
            Label("Selected Stage Name", preview.transform, $"{stage.Number}. {stage.Name}",
                new Vector2(-192f, 43f), new Vector2(384f, 54f), 30);
            Label("Selected Stage State", preview.transform, run.HasLoadoutChanges
                ? "편성 변경 사항을 저장하거나 취소한 뒤 출정하세요."
                : run.IsStageWaitingForMission(selectedStageNumber) ? MissionWaitText()
                : StageState(run, selectedStageNumber),
                new Vector2(-192f, -7f), new Vector2(384f, 48f), 18,
                run.IsStageCleared(selectedStageNumber) ? Accent : Muted);
            Label("Selected Stage Stats", preview.transform,
                stage.EnemyCount > 1
                    ? $"상대 {stage.EnemyCount}명  ·  공유 대기열\n각 체력  {stage.EnemyHealth}     저항  {stage.EnemyResistance}"
                    : $"적 체력  {stage.EnemyHealth}     저항  {stage.EnemyResistance}\n위력  +{stage.EnemyPowerBonus}",
                new Vector2(-192f, -76f), new Vector2(384f, 62f), 22, Foreground);
            if (run.IsCurriculumOpen)
            {
                // What a win would earn now: battle and victory, and the first clear while the stage is not cleared.
                int points = run.GetStageVictoryPoints(selectedStageNumber);
                Label("Selected Stage Curriculum", preview.transform,
                    run.IsStageCleared(selectedStageNumber) ? $"승리 시 수련 점수 +{points}" : $"첫 클리어 수련 점수 +{points}",
                    new Vector2(-192f, -137f), new Vector2(384f, 34f), 20, Gold);
            }
            if (stage.FirstClearSkillId != 0)
            {
                string skillName = LegacySkillDefinitions.Skill(stage.FirstClearSkillId).Name;
                Text reward = Label("Selected Stage Skill Reward", preview.transform,
                    run.IsStageCleared(selectedStageNumber) ? $"첫 클리어 기술  {skillName} · 획득 완료"
                        : $"첫 클리어 기술  {skillName}",
                    new Vector2(-192f, -167f), new Vector2(384f, 24f), 20, Gold);
                reward.resizeTextForBestFit = true;
                reward.resizeTextMinSize = 15;
                reward.resizeTextMaxSize = 20;
            }
            int selected = selectedStageNumber;
            Button("Start Selected Stage", preview.transform,
                run.HasLoadoutChanges ? "편성 저장 필요" : "도전  [Enter]", new Vector2(0f, -215f),
                new Vector2(384f, 66f), run.CanStartStage(selected), () => StartStage(selected), true);
        }

        private void BuildStageCard(Transform parent, CampaignRun run, int number, Vector2 position, Vector2 size)
        {
            bool unlocked = number <= run.HighestUnlockedStage;
            bool selected = number == selectedStageNumber;
            bool cleared = run.IsStageCleared(number);
            Color color = selected ? CardSelected : unlocked ? Card : DuelVisualTheme.Track;
            var card = Panel("Stage Card " + number, parent, position, size, color);
            card.raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.interactable = unlocked;
            ConfigureButtonColors(button);
            int stageNumber = number;
            button.onClick.AddListener(() =>
            {
                ClearSelection();
                SelectStage(stageNumber);
            });
            if (selected)
            {
                var marker = Panel("Selected Marker", card.transform, new Vector2(-size.x * .5f + 4f, 0f),
                    new Vector2(6f, size.y - 10f), Accent);
                marker.raycastTarget = false;
            }
            CampaignStage stage = run.GetStage(number);
            float left = -size.x * .5f + 18f;
            Label("Stage Number", card.transform, number.ToString("00"), new Vector2(left, size.y * .5f - 22f),
                new Vector2(64f, 28f), 23, unlocked ? Gold : Muted);
            Label("Stage Name", card.transform, stage.Name, new Vector2(left, size.y < 115f ? -4f : 0f),
                new Vector2(size.x - 36f, 40f), 19, unlocked ? Foreground : Muted);
            Label("Stage State " + number, card.transform,
                !unlocked ? "잠김" : run.IsStageWaitingForMission(number) ? "임무 필요"
                : cleared ? "클리어" : selected ? "선택됨" : "도전 가능",
                new Vector2(left, -size.y * .5f + 20f), new Vector2(size.x - 36f, 24f), 15,
                cleared || selected ? Accent : Muted);
        }

        private static void ConnectStageStops(Transform parent, int fromNumber, Vector2 from, Vector2 to, bool reached)
        {
            Vector2 direction = to - from;
            float distance = direction.magnitude;
            // Leave a gap at each ticket so the line reads as a path between stops.
            float lineLength = Mathf.Max(0f, distance - 154f);
            if (lineLength <= 0f) return;
            Color color = reached ? Accent : Muted;
            color.a = reached ? .75f : .35f;
            var line = Panel("Stage Route " + fromNumber, parent, (from + to) * .5f,
                new Vector2(lineLength, reached ? 4f : 2f), color);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        /// <summary>The shared drag hint, also used by the versus loadout.</summary>
        public const string LoadoutHint = "기술을 끌어 같은 열의 톱니에 끼우세요.";
        private const string CampaignLoadoutHint = "일반 기술은 같은 열의 톱니로 끌어 넣고, 반격은 선택 후 장착하세요.";

        private void BuildLoadout(CampaignRun run)
        {
            RectTransform panel = TabPanel("Loadout Panel", "기술 편성", CampaignLoadoutHint);
            loadoutHud = new CampaignLoadoutHud(panel, art, run,
                (id, lane, slot) =>
                {
                    if (placeLoadoutSkill != null) placeLoadoutSkill.Invoke(id, lane, slot);
                    else if (run.TryPlaceLoadoutSkill(id, lane, slot)) Show(run);
                },
                id =>
                {
                    if (unequip != null) unequip.Invoke(id);
                    else if (run.TryUnequipSkill(id)) Show(run);
                },
                () =>
                {
                    if (saveLoadout != null) saveLoadout.Invoke();
                    else if (run.TrySaveLoadout()) Show(run);
                },
                () =>
                {
                    if (resetLoadout != null) resetLoadout.Invoke();
                    else if (run.TryResetLoadout()) Show(run);
                }, loadoutState, id =>
                {
                    if (equipCounter != null) equipCounter.Invoke(id);
                    else if (run.TryEquipCounter(id)) Show(run);
                });
        }

        private void BuildCurriculum(CampaignRun run)
        {
            RectTransform panel = TabPanel("Curriculum Panel", "커리큘럼",
                "수련 점수로 과정을 익혀 기술과 능력치를 얻으세요.");
            curriculumHud = new CampaignCurriculumHud(panel, art, run,
                id =>
                {
                    if (learnCurriculumNode != null) learnCurriculumNode.Invoke(id);
                    else if (run.TryLearnCurriculumNode(id)) Show(run);
                },
                () =>
                {
                    if (resetCurriculum != null) resetCurriculum.Invoke();
                    else if (run.TryResetCurriculum()) Show(run);
                }, curriculumState);
        }

        private RectTransform TabPanel(string name, string heading, string subtitle)
        {
            // These are page content coordinates, not a window, dimmer or modal backdrop.
            var panel = Rect(name, pageRoot, new Vector2(0f, -24f), new Vector2(TabWidth, TabHeight));
            panel.localScale = Vector3.one * Mathf.Min(1.2f, (LedgerWidth - 64f) / TabWidth);
            Label("Tab Heading", panel, heading, new Vector2(-590f, 368f),
                new Vector2(620f, 48f), 31);
            Label("Tab Subtitle", panel, subtitle, new Vector2(-590f, 329f),
                new Vector2(1160f, 34f), 17, Muted);
            Rule("Tab Rule", panel, 303f, 1180f);
            return panel;
        }


        private Button Button(string name, Transform parent, string caption, Vector2 position, Vector2 size,
            bool enabled, Action action, bool primary = false, Color? captionColor = null)
        {
            var image = Panel(name, parent, position, size, primary ? Accent : DuelVisualTheme.RaisedSurface);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ConfigureButtonColors(button, primary);
            button.interactable = enabled;
            Text captionLabel = Label("Button Label", image.transform, caption, Vector2.zero, size - new Vector2(10f, 4f),
                primary ? 22 : 17, captionColor ?? (primary && enabled ? DuelVisualTheme.Ink : Foreground), TextAnchor.MiddleCenter);
            if (name.StartsWith("Home ", StringComparison.Ordinal) && size.x >= 150f)
            {
                image.sprite = AcademyUiSkin.MenuPlaque;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.color = Color.white;
                Transform trim = image.transform.Find("Brass Trim");
                if (trim != null) trim.gameObject.SetActive(false);
                captionLabel.color = Foreground;
                captionLabel.rectTransform.sizeDelta = size - new Vector2(52f, 4f);
                captionLabel.resizeTextForBestFit = true;
                captionLabel.resizeTextMinSize = 14;
                captionLabel.resizeTextMaxSize = Mathf.Min(20, captionLabel.fontSize);
                if (size.x < 180f)
                {
                    captionLabel.resizeTextMaxSize = 16;
                    captionLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
            }
            button.onClick.AddListener(() =>
            {
                ClearSelection();
                action?.Invoke();
            });
            return button;
        }

        private static void ConfigureButtonColors(Button button, bool primary = false)
            => DuelVisualTheme.StyleButton(button, primary);

        private Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size,
            int fontSize, Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect(name, parent, position, size);
            if (alignment == TextAnchor.MiddleLeft || alignment == TextAnchor.UpperLeft)
                rect.pivot = new Vector2(0f, .5f);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = art.UIFont;
            label.text = value;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color ?? Foreground;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.supportRichText = false;
            return label;
        }


        private void StartStage(int stageNumber)
        {
            awaitingStageOutcome = true;
            startStage?.Invoke(stageNumber);
        }

        private void ResetJourneyClicked()
        {
            if (!resetArmed)
            {
                resetArmed = true;
                Rebuild();
                return;
            }
            resetArmed = false;
            outcomeBanner = null;
            awaitingStageOutcome = false;
            restartJourney?.Invoke();
        }


        private void ClearDynamicRoot()
        {
            screenTransition.Finish();
            pageRoot = null;
            loadoutHud?.Dispose();
            loadoutHud = null;
            curriculumHud?.Dispose();
            curriculumHud = null;
            for (int index = dynamicRoot.childCount - 1; index >= 0; index--)
            {
                GameObject child = dynamicRoot.GetChild(index).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private string MissionWaitText()
            => nextMission != null && nextMissionAvailable ? $"먼저 '{nextMission.Title}' 임무를 완료하세요" : "먼저 다음 임무를 완료하세요";

        /// <summary>The next mission's title once it can be played, <see cref="HiddenMissionTitle"/> before.</summary>
        private string NextMissionTitle => nextMissionAvailable ? nextMission.Title : HiddenMissionTitle;

        private static string StageState(CampaignRun run, int number)
            => number > run.HighestUnlockedStage ? "잠긴 스테이지"
                : run.IsStageCleared(number) ? "클리어 완료" : "첫 도전";


        private static void Rule(string name, Transform parent, float y, float width)
            => Panel(name, parent, new Vector2(0f, y), new Vector2(width, 2f), Border);

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

        private static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static void Destroy(GameObject gameObject)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(gameObject);
            else UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
