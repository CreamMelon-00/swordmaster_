using System;
using TurnLimbo.Runtime.Campaign;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public enum LobbyTab { Home, Stages, Loadout, Shop }

    /// <summary>Full-screen lobby pages. CampaignRun owns progression; view states survive page changes.</summary>
    public sealed class CampaignLobbyHud : IDisposable
    {
        private const float TabWidth = 1300f;
        private const float TabHeight = 850f;
        private readonly LegacyDuelArt art;
        private readonly Action<int> acquire, upgrade, unequip, startStage;
        private readonly Action<int, int, int> placeLoadoutSkill;
        private readonly Action restartJourney, saveLoadout, resetLoadout;
        private readonly CampaignLoadoutHud.ViewState loadoutState = new CampaignLoadoutHud.ViewState();
        private readonly CampaignShopHud.ViewState shopState = new CampaignShopHud.ViewState();
        private CampaignLoadoutHud loadoutHud;
        private CampaignShopHud shopHud;
        private readonly RectTransform root, dynamicRoot;
        private readonly LobbyScreenTransition screenTransition;
        private RectTransform pageRoot;
        private readonly Sprite roomSprite;
        private readonly bool assetsAvailable;
        private CampaignRun currentRun;
        private bool disposed, resetArmed, awaitingStageOutcome, lastOutcomeFailed;
        private int lastOutcomeReward;
        private int selectedStageNumber = 1;
        private LobbyTab currentTab;

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
        public RectTransform CurrentPage => pageRoot;
        public bool IsTransitioning => !disposed && screenTransition.IsTransitioning;

        public CampaignLobbyHud(Transform parent, LegacyDuelArt art, Action<int> acquire,
            Action<int> upgrade, Action<int> equip, Action<int> unequip, Action<int, int> move,
            Action<int> startStage, Action restartJourney,
            Action<int, int, int> placeLoadoutSkill = null, Action saveLoadout = null,
            Action resetLoadout = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.acquire = acquire;
            this.upgrade = upgrade;
            this.unequip = unequip;
            this.startStage = startStage;
            this.restartJourney = restartJourney;
            this.placeLoadoutSkill = placeLoadoutSkill;
            this.saveLoadout = saveLoadout;
            this.resetLoadout = resetLoadout;

            roomSprite = Resources.Load<Sprite>("LobbyRoom/room");
            bool complete = roomSprite != null && art.UIFont != null;
            for (int icon = 1; icon <= LegacyDuelArt.SkillIconCount; icon++)
                complete &= art.GetSkillIcon(icon) != null;
            assetsAvailable = complete;

            root = Rect("Campaign Lobby HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            screenTransition = root.gameObject.AddComponent<LobbyScreenTransition>();

            var room = Image("Bedroom Background", root, roomSprite, Vector2.zero, new Vector2(1920f, 1080f));
            room.preserveAspect = false;
            var fitter = room.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = roomSprite != null && roomSprite.rect.height > 0f
                ? roomSprite.rect.width / roomSprite.rect.height : 16f / 9f;
            Color shadeColor = DuelVisualTheme.Track;
            shadeColor.a = .16f;
            var shade = Panel("Bedroom Shade", root, Vector2.zero, Vector2.zero, shadeColor);
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
                awaitingStageOutcome = true;
                Hide();
                return;
            }

            ClearSelection();
            bool changedRun = currentRun != null && !ReferenceEquals(currentRun, run);
            currentRun = run;
            if (changedRun)
            {
                selectedStageNumber = 1;
                awaitingStageOutcome = lastOutcomeFailed = false;
                lastOutcomeReward = 0;
                resetArmed = false;
                loadoutState.Reset();
                shopHud?.Dispose();
                shopHud = null;
                shopState.Reset();
            }
            selectedStageNumber = Mathf.Clamp(selectedStageNumber, 1, Mathf.Max(1, run.HighestUnlockedStage));
            if (awaitingStageOutcome)
            {
                lastOutcomeReward = run.LastReward;
                lastOutcomeFailed = run.LastReward <= 0;
                awaitingStageOutcome = false;
            }
            else if (run.LastReward > 0)
            {
                lastOutcomeReward = run.LastReward;
                lastOutcomeFailed = false;
            }

            root.gameObject.SetActive(true);
            Rebuild();
        }

        public void ShowTab(LobbyTab tab)
        {
            if (disposed) throw new ObjectDisposedException(nameof(CampaignLobbyHud));
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
            lastOutcomeFailed = false;
            lastOutcomeReward = 0;
            resetArmed = false;
            selectedStageNumber = 1;
            loadoutState.Reset();
            shopHud?.Dispose();
            shopHud = null;
            shopState.Reset();
            currentTab = LobbyTab.Home;
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
            if (currentRun != null && currentRun.Phase == CampaignPhase.Battle) awaitingStageOutcome = true;
            ClearSelection();
            loadoutHud?.Dispose();
            loadoutHud = null;
            shopHud?.Dispose();
            shopHud = null;
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
            ClearDynamicRoot();
            BuildHeader(currentRun);
            pageRoot = Rect("Lobby Page", dynamicRoot, Vector2.zero, Vector2.zero);
            Stretch(pageRoot);
            pageRoot.sizeDelta = new Vector2(0f, -100f);
            pageRoot.anchoredPosition = new Vector2(0f, -50f);
            if (currentTab != LobbyTab.Home)
            {
                var background = Panel("Page Background", pageRoot, Vector2.zero, Vector2.zero, Surface);
                Stretch(background.rectTransform);
            }
            switch (currentTab)
            {
                case LobbyTab.Stages: BuildStages(currentRun); break;
                case LobbyTab.Loadout: BuildLoadout(currentRun); break;
                case LobbyTab.Shop: BuildShop(currentRun); break;
                default: BuildHome(currentRun); break;
            }
            Canvas.ForceUpdateCanvases();
            if (animate) screenTransition.Play(pageRoot, direction);
        }

        private void BuildHeader(CampaignRun run)
        {
            var header = Panel("Lobby Header", dynamicRoot, Vector2.zero, new Vector2(0f, 100f), Header);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = Vector2.one;
            header.rectTransform.pivot = new Vector2(.5f, 1f);
            DuelVisualTheme.DressPanel(header);
            Label("Lobby Title", header.transform, "검술 클럽", new Vector2(-895f, 17f), new Vector2(270f, 48f), 34);
            Label("Campaign Progress", header.transform,
                $"진행  {run.ClearedStageCount}/{run.StageCount}   ·   개방  {run.HighestUnlockedStage}/{run.StageCount}",
                new Vector2(-895f, -24f), new Vector2(420f, 30f), 18, Muted);

            BuildTabButton(header.transform, LobbyTab.Home, "홈", -290f);
            BuildTabButton(header.transform, LobbyTab.Stages, "스테이지", -80f);
            BuildTabButton(header.transform, LobbyTab.Loadout, "편성", 130f);
            BuildTabButton(header.transform, LobbyTab.Shop, "상점", 340f);
            Label("Wallet", header.transform, $"재화  {run.Currency}", new Vector2(620f, 10f),
                new Vector2(285f, 58f), 28, Gold, TextAnchor.MiddleRight);

            if (currentTab == LobbyTab.Loadout || currentTab == LobbyTab.Shop) return;
            if (lastOutcomeReward > 0)
                Label("Reward Banner", header.transform, $"승리 보상  +{lastOutcomeReward}", new Vector2(620f, -27f),
                    new Vector2(285f, 28f), 17, Gold, TextAnchor.MiddleRight);
            else if (lastOutcomeFailed)
                Label("Reward Banner", header.transform, "전투 종료  ·  획득 보상 없음", new Vector2(580f, -27f),
                    new Vector2(365f, 28f), 16, Muted, TextAnchor.MiddleRight);
        }

        private void BuildTabButton(Transform parent, LobbyTab tab, string caption, float x)
        {
            bool selected = currentTab == tab;
            Button("Tab " + tab, parent, caption, new Vector2(x, 0f), new Vector2(190f, 58f), true,
                () => ShowTab(tab), selected, selected ? DuelVisualTheme.Ink : Foreground);
        }

        private void BuildHome(CampaignRun run)
        {
            var inner = Panel("Home Sidebar", pageRoot, new Vector2(195f, 0f), new Vector2(390f, 0f), Surface);
            inner.rectTransform.anchorMin = Vector2.zero;
            inner.rectTransform.anchorMax = new Vector2(0f, 1f);
            DuelVisualTheme.DressPanel(inner);
            Label("Home Heading", inner.transform, "오늘의 준비", new Vector2(-153f, 350f), new Vector2(306f, 54f), 31);
            Label("Home Stage Summary", inner.transform,
                $"개방 스테이지  {run.HighestUnlockedStage}/{run.StageCount}\n클리어  {run.ClearedStageCount}/{run.StageCount}",
                new Vector2(-153f, 278f), new Vector2(306f, 76f), 20, Muted);
            Rule("Home Rule", inner.transform, 215f, 306f);
            Label("Home Hint", inner.transform, "방을 나서기 전에\n도전할 길과 기술 순서를 정하세요.",
                new Vector2(-153f, 154f), new Vector2(306f, 72f), 19, Foreground);
            Button("Home Open Stages", inner.transform, "출정", new Vector2(0f, 65f), new Vector2(304f, 62f), true,
                () => ShowTab(LobbyTab.Stages), true);
            Button("Home Open Loadout", inner.transform, "스킬 편성", new Vector2(0f, -25f), new Vector2(304f, 62f), true,
                () => ShowTab(LobbyTab.Loadout));
            Button("Home Open Shop", inner.transform, "상점", new Vector2(0f, -115f), new Vector2(304f, 62f), true,
                () => ShowTab(LobbyTab.Shop));
            if (restartJourney != null)
                Button("Reset Journey", inner.transform, resetArmed ? "정말 초기화" : "여정 초기화",
                    new Vector2(0f, -290f), new Vector2(304f, 42f), true, ResetJourneyClicked, false, Muted);
            Label("Home Welcome", pageRoot, "다음 결투를 준비하세요", new Vector2(80f, 365f),
                new Vector2(700f, 62f), 36, DuelVisualTheme.Paper, TextAnchor.MiddleCenter);
            Label("Home Welcome Hint", pageRoot, "기술을 정비하고, 숲길 너머의 상대에게 도전합니다.", new Vector2(80f, 311f),
                new Vector2(820f, 36f), 20, DuelVisualTheme.Paper, TextAnchor.MiddleCenter);
        }

        private void BuildStages(CampaignRun run)
        {
            var panel = Rect("Stages Panel", pageRoot, new Vector2(0f, -24f), new Vector2(1800f, 850f));
            Label("Tab Heading", panel, "출정 지도", new Vector2(-850f, 370f), new Vector2(700f, 54f), 40);
            Label("Tab Subtitle", panel, "도전할 길을 고르고 오른쪽에서 상대와 보상을 확인하세요.",
                new Vector2(-850f, 323f), new Vector2(1180f, 36f), 20, Muted);
            Rule("Tab Rule", panel, 289f, 1700f);
            const float cardWidth = 252f, cardHeight = 210f, gapX = 20f, gapY = 28f;
            for (int number = 1; number <= run.StageCount; number++)
            {
                int column = (number - 1) % 4;
                int row = (number - 1) / 4;
                float x = -724f + column * (cardWidth + gapX);
                float y = 143f - row * (cardHeight + gapY);
                BuildStageCard(panel, run, number, new Vector2(x, y), new Vector2(cardWidth, cardHeight));
            }

            CampaignStage stage = run.GetStage(selectedStageNumber);
            var previewBorder = Panel("Selected Stage Preview Border", panel, new Vector2(630f, -40f),
                new Vector2(444f, 568f), selectedStageNumber <= run.HighestUnlockedStage ? Accent : Border);
            var preview = Panel("Selected Stage Preview", previewBorder.transform, Vector2.zero,
                new Vector2(440f, 564f), SurfaceInner);
            DuelVisualTheme.DressPanel(preview);
            var landscape = Image("Selected Stage Landscape", preview.transform,
                Resources.Load<Sprite>("ForestArena/forest-belt-mid"), new Vector2(0f, 161f), new Vector2(392f, 170f));
            // The landscape is decorative; missing optional scenery does not affect stage selection.
            landscape.gameObject.SetActive(landscape.sprite != null);
            Label("Selected Stage Name", preview.transform, $"{stage.Number}. {stage.Name}",
                new Vector2(-192f, 43f), new Vector2(384f, 54f), 30);
            Label("Selected Stage State", preview.transform, run.HasLoadoutChanges
                ? "편성 변경 미저장 · 편성에서 저장 또는 되돌리기 후 출정"
                : StageState(run, selectedStageNumber),
                new Vector2(-192f, -7f), new Vector2(384f, 48f), 18,
                run.IsStageCleared(selectedStageNumber) ? Accent : Muted);
            Label("Selected Stage Stats", preview.transform,
                $"적 체력  {stage.EnemyHealth}     저항  {stage.EnemyResistance}\n위력  +{stage.EnemyPowerBonus}",
                new Vector2(-192f, -76f), new Vector2(384f, 62f), 22, Foreground);
            Label("Selected Stage Reward", preview.transform,
                $"승리 보상  {run.GetStageReward(selectedStageNumber)}",
                new Vector2(-192f, -137f), new Vector2(384f, 34f), 23, Gold);
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
                var marker = Panel("Selected Marker", card.transform, new Vector2(0f, size.y * .5f - 3f),
                    new Vector2(size.x, 6f), Accent);
                marker.raycastTarget = false;
            }
            CampaignStage stage = run.GetStage(number);
            float left = -size.x * .5f + 18f;
            Label("Stage Number", card.transform, number.ToString("00"), new Vector2(left, 61f),
                new Vector2(80f, 40f), 27, unlocked ? Gold : Muted);
            Label("Stage Name", card.transform, stage.Name, new Vector2(left, 10f),
                new Vector2(size.x - 36f, 56f), 22, unlocked ? Foreground : Muted);
            Label("Stage State " + number, card.transform,
                !unlocked ? "잠김" : cleared ? "클리어" : selected ? "선택됨" : "도전 가능",
                new Vector2(left, -62f), new Vector2(size.x - 36f, 32f), 18,
                cleared || selected ? Accent : Muted);
        }

        private void BuildLoadout(CampaignRun run)
        {
            RectTransform panel = TabPanel("Loadout Panel", "스킬 편성",
                "각 열 3개 · 위부터 사용 · 클릭은 설명 선택, 배치는 드래그");
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
                }, loadoutState);
        }

        private void BuildShop(CampaignRun run)
        {
            RectTransform panel = TabPanel("Shop Panel", "상점",
                "기술을 선택해 살펴본 뒤 구매하거나 강화하세요.");
            shopHud = new CampaignShopHud(panel, art, run,
                id =>
                {
                    if (acquire != null) acquire.Invoke(id);
                    else if (run.TryAcquireSkill(id)) Show(run);
                },
                id =>
                {
                    if (upgrade != null) upgrade.Invoke(id);
                    else if (run.TryUpgradeSkill(id)) Show(run);
                }, shopState);
        }

        private RectTransform TabPanel(string name, string heading, string subtitle)
        {
            // These are page content coordinates, not a window, dimmer or modal backdrop.
            var panel = Rect(name, pageRoot, new Vector2(0f, -24f), new Vector2(TabWidth, TabHeight));
            panel.localScale = Vector3.one * 1.2f;
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
            Label("Button Label", image.transform, caption, Vector2.zero, size - new Vector2(10f, 4f),
                primary ? 22 : 17, captionColor ?? (primary && enabled ? DuelVisualTheme.Ink : Foreground), TextAnchor.MiddleCenter);
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
            lastOutcomeReward = 0;
            lastOutcomeFailed = awaitingStageOutcome = false;
            restartJourney?.Invoke();
        }


        private void ClearDynamicRoot()
        {
            screenTransition.Finish();
            pageRoot = null;
            loadoutHud?.Dispose();
            loadoutHud = null;
            shopHud?.Dispose();
            shopHud = null;
            for (int index = dynamicRoot.childCount - 1; index >= 0; index--)
            {
                GameObject child = dynamicRoot.GetChild(index).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static string StageState(CampaignRun run, int number)
            => number > run.HighestUnlockedStage ? "잠긴 스테이지"
                : run.IsStageCleared(number) ? "클리어 완료  ·  재도전 보상은 절반" : "첫 도전";


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
