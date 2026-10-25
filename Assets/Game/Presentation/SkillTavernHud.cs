using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Tavern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Local view commands. The caller owns their execution and the authoritative experiment state.</summary>
    public enum SkillTavernAction { Buy, Deploy, SellBoard, SellInventory, NextRound, StartBattle, BuyAndDeploy, RefreshShop }

    /// <summary>The standalone shop and eight-slot preparation experiment. Selecting a card only inspects it;
    /// every economy or board change goes through the caller, so the view can later display a remote authority.</summary>
    public sealed class SkillTavernHud : IDisposable
    {
        private enum Selection { None, Shop, Board, Inventory }

        private sealed class CardView
        {
            public Image Surface;
            public Button Select, Action, DirectAction;
            public Text Name, Facts, ActionLabel, DirectLabel;
            public Image Icon;
        }

        public const int SortingOrder = 285;
        private const float InventoryWidth = 1130f, InventoryPitch = 220f;
        private readonly LegacyDuelArt art;
        private readonly Action returnToTitle, reset;
        private readonly Action<SkillTavernAction, int> execute;
        private readonly RectTransform root, inventoryContent;
        private readonly GameObject ownedEventSystem;
        private readonly CardView[] shop = new CardView[SkillTavernRun.ShopSize];
        private readonly CardView[] board = new CardView[SkillTavernRun.SlotCount];
        private readonly Dictionary<int, CardView> inventory = new Dictionary<int, CardView>();
        private readonly ScrollRect inventoryScroll;
        private readonly Text wallet, inventoryHeading, inventoryEmpty, inventoryHint, selectedName, detailName, opponentHint;
        private readonly Text playerState, enemyState;
        private readonly Image detailIcon;
        private readonly SkillInfoView detailInfo;
        private readonly Button deployButton, sellInventoryButton, nextRoundButton, resetButton, refreshShopButton;
        private Selection selection;
        private int selectedValue;
        private SkillTavernBattle displayedBattle;
        private bool disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public SkillTavernRun Run { get; private set; }

        public SkillTavernHud(Transform parent, LegacyDuelArt art, Action returnToTitle,
            Action reset = null, Action<SkillTavernAction, int> execute = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.returnToTitle = returnToTitle;
            this.reset = reset;
            this.execute = execute;

            root = Rect("Skill Tavern HUD", parent, Vector2.zero, Vector2.zero);
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
                ownedEventSystem = new GameObject("Skill Tavern EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(parent, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            Image backdrop = Panel("Tavern Backdrop", root, Vector2.zero, Vector2.zero, DuelVisualTheme.Surface);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            // The shop is a complete screen. Only the individual skills are cards; there is no framed modal surface.
            RectTransform surface = Rect("Tavern Surface", root, Vector2.zero, Vector2.zero);
            Stretch(surface);
            Label("Tavern Heading", surface, new Vector2(-500f, 480f), new Vector2(730f, 54f), 42,
                DuelVisualTheme.Foreground).text = "선술집 실험";
            wallet = Label("Tavern Wallet", surface, new Vector2(500f, 480f), new Vector2(730f, 54f), 26,
                DuelVisualTheme.Accent, TextAnchor.MiddleRight);
            Image status = Panel("Tavern Fighter Status", surface, new Vector2(0f, 423f),
                new Vector2(1730f, 48f), DuelVisualTheme.RaisedSurface);
            playerState = Label("Tavern Player State", status.transform, new Vector2(-431f, 0f),
                new Vector2(820f, 40f), 23, DuelVisualTheme.Foreground);
            enemyState = Label("Tavern Enemy State", status.transform, new Vector2(431f, 0f),
                new Vector2(820f, 40f), 23, DuelVisualTheme.Steel, TextAnchor.MiddleRight);
            Label("Tavern Rules", surface, new Vector2(0f, 372f), new Vector2(1730f, 48f), 18,
                DuelVisualTheme.Muted).text = "편성은 왼쪽 첫 빈칸으로, 보관은 보관함으로 들어갑니다. 편성한 기술은 판매로만 뺄 수 있습니다.\n"
                + (LegacyPowerBreakRules.PrototypeEnabled
                    ? "빈칸은 숨고르기입니다. 8칸을 자동 실행한 뒤 상점으로 돌아옵니다. HP는 이어지며, 카드 ACT는 예약 비용으로 쓰지 않습니다."
                    : "빈칸은 숨고르기입니다. 8칸을 자동 실행한 뒤 상점으로 돌아옵니다. HP와 저항은 이어지며, 카드 ACT는 예약 비용으로 쓰지 않습니다.");

            Label("Tavern Shop Heading", surface, new Vector2(-305f, 328f), new Vector2(1130f, 36f), 25,
                DuelVisualTheme.Foreground).text = "상점  ·  편성 / 보관 각 " + SkillTavernRun.PurchasePrice + " 자원";
            refreshShopButton = ActionButton("Tavern Refresh Shop", surface, "새로고침 " + SkillTavernRun.RefreshPrice,
                new Vector2(650f, 328f), new Vector2(240f, 36f), () => Dispatch(SkillTavernAction.RefreshShop, 0));
            for (int i = 0; i < shop.Length; i++)
            {
                int index = i;
                shop[i] = BuildShopCard(surface, i, new Vector2(-680f + 375f * i, 150f),
                    () => Select(Selection.Shop, index), () => Dispatch(SkillTavernAction.Buy, index),
                    () => Dispatch(SkillTavernAction.BuyAndDeploy, index));
            }

            Image detail = Panel("Tavern Detail", surface, new Vector2(650f, 150f),
                new Vector2(400f, 300f), DuelVisualTheme.Paper);
            DuelVisualTheme.Frame(detail, true);
            detailIcon = Panel("Tavern Detail Icon", detail.transform, new Vector2(-145f, 110f),
                new Vector2(52f, 52f), Color.white);
            detailIcon.preserveAspect = true;
            detailName = Label("Tavern Detail Name", detail.transform, new Vector2(40f, 110f),
                new Vector2(290f, 46f), 25, DuelVisualTheme.Ink);
            Fit(detailName, 17, 25);
            detailInfo = new SkillInfoView(detail.transform, art.UIFont, new Vector2(0f, -26f), 374f, "Tavern");

            Label("Tavern Board Heading", surface, new Vector2(0f, -35f), new Vector2(1730f, 35f), 25,
                DuelVisualTheme.Foreground).text = "편성  ·  왼쪽부터 실행  ·  판매 " + SkillTavernRun.SellPrice + " 자원";
            for (int i = 0; i < board.Length; i++)
            {
                int index = i;
                board[i] = BuildBoardCard(surface, i, new Vector2(-763f + 218f * i, -145f),
                    () => Select(Selection.Board, index), () => Dispatch(SkillTavernAction.SellBoard, index));
            }

            inventoryHeading = Label("Tavern Inventory Heading", surface, new Vector2(-305f, -255f),
                new Vector2(1130f, 36f), 25, DuelVisualTheme.Foreground);
            var collection = Rect("Tavern Inventory Scroll", surface, new Vector2(-305f, -351f), new Vector2(InventoryWidth, 150f));
            inventoryScroll = collection.gameObject.AddComponent<ScrollRect>();
            Image viewport = Panel("Tavern Inventory Viewport", collection, new Vector2(0f, 9f),
                new Vector2(InventoryWidth, 132f), DuelVisualTheme.Track);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            inventoryContent = Rect("Tavern Inventory Content", viewport.transform, Vector2.zero, new Vector2(InventoryWidth, 132f));
            inventoryContent.anchorMin = inventoryContent.anchorMax = inventoryContent.pivot = new Vector2(0f, 1f);
            inventoryScroll.viewport = viewport.rectTransform;
            inventoryScroll.content = inventoryContent;
            inventoryScroll.horizontal = true;
            inventoryScroll.vertical = false;
            inventoryScroll.inertia = false;
            inventoryScroll.movementType = ScrollRect.MovementType.Clamped;
            inventoryScroll.scrollSensitivity = 50f;
            Image track = Panel("Tavern Inventory Scrollbar", collection, new Vector2(0f, -66f),
                new Vector2(InventoryWidth, 12f), DuelVisualTheme.Card);
            track.raycastTarget = true;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            Image handle = Panel("Tavern Inventory Handle", track.transform, Vector2.zero, Vector2.zero, DuelVisualTheme.Accent);
            Stretch(handle.rectTransform);
            handle.raycastTarget = true;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            inventoryScroll.horizontalScrollbar = scrollbar;
            inventoryEmpty = Label("Tavern Inventory Empty", viewport.transform, Vector2.zero,
                new Vector2(InventoryWidth - 40f, 90f), 22, DuelVisualTheme.Muted, TextAnchor.MiddleCenter);
            inventoryEmpty.text = "상점에서 보관을 선택하면 여기에 들어옵니다.";

            Image actions = Panel("Tavern Inventory Actions", surface, new Vector2(650f, -344f),
                new Vector2(400f, 164f), DuelVisualTheme.Card);
            DuelVisualTheme.Frame(actions);
            selectedName = Label("Tavern Selected Inventory", actions.transform, new Vector2(0f, 53f),
                new Vector2(366f, 32f), 23, DuelVisualTheme.Foreground);
            Fit(selectedName, 16, 23);
            inventoryHint = Label("Tavern Inventory Hint", actions.transform, new Vector2(0f, 10f),
                new Vector2(366f, 52f), 17, DuelVisualTheme.Muted);
            deployButton = ActionButton("Tavern Deploy", actions.transform, "편성", new Vector2(-96f, -51f),
                new Vector2(174f, 42f), () => Dispatch(SkillTavernAction.Deploy, selectedValue), true);
            sellInventoryButton = ActionButton("Tavern Sell Inventory", actions.transform,
                "판매 +" + SkillTavernRun.SellPrice, new Vector2(96f, -51f), new Vector2(174f, 42f),
                () => Dispatch(SkillTavernAction.SellInventory, selectedValue));

            ActionButton("Tavern Return", surface, "타이틀로  [Esc]", new Vector2(-748f, -467f),
                new Vector2(240f, 48f), () => this.returnToTitle?.Invoke());
            opponentHint = Label("Tavern Round Hint", surface, new Vector2(-245f, -466f), new Vector2(720f, 48f), 17,
                DuelVisualTheme.Muted);
            nextRoundButton = ActionButton("Tavern Next Round", surface, "8칸 실행", new Vector2(374f, -467f),
                new Vector2(310f, 48f), () => Dispatch(SkillTavernAction.StartBattle, 0), true);
            resetButton = ActionButton("Tavern Reset", surface, "새 실험", new Vector2(722f, -467f),
                new Vector2(244f, 48f), () => this.reset?.Invoke());
            Hide();
        }

        public void Show(SkillTavernRun run) => Show(run, ReferenceEquals(Run, run) ? displayedBattle : null);

        public void Show(SkillTavernRun run, SkillTavernBattle battle)
        {
            if (disposed) return;
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (!ReferenceEquals(Run, run))
            {
                selection = Selection.None;
                selectedValue = 0;
                inventoryScroll.horizontalNormalizedPosition = 0f;
            }
            Run = run;
            displayedBattle = battle;
            root.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (disposed) return;
            root.gameObject.SetActive(false);
            ClearSelection();
        }

        public void Refresh()
        {
            if (disposed || Run == null) return;
            if (SelectedSkill() == null) { selection = Selection.None; selectedValue = 0; }
            wallet.text = "라운드 " + Run.RoundNumber + "  ·  자원 " + Run.Gold + "  ·  이번 지급 " + Run.RoundIncome;
            playerState.text = FighterState("나", displayedBattle?.Duel.Player);
            enemyState.text = FighterState(displayedBattle?.EnemyName ?? "선술집 도전자", displayedBattle?.Duel.Enemy);
            int enemyTechniques = 0;
            foreach (LegacySkill skill in SkillTavernBattle.CreateEnemyQueue(Run.RoundNumber))
                if (!skill.IsWait) enemyTechniques++;
            opponentHint.text = "상대 기술 " + enemyTechniques + "/" + SkillTavernRun.SlotCount
                + " · 8칸 후 자동으로 다음 상점"
                + "\n이번 라운드 자원 " + Run.RoundIncome + " · 남은 자원 이월 없음";
            bool canExecute = execute != null;
            refreshShopButton.interactable = canExecute && Run.CanRefreshShop;
            for (int i = 0; i < shop.Length; i++)
            {
                LegacySkill skill = Run.ShopOffers[i];
                CardView view = shop[i];
                view.Name.text = skill?.Name ?? "품절";
                view.Facts.text = skill == null ? "새로고침하거나 다음 라운드에 새 기술이 들어옵니다." : Facts(skill);
                view.Icon.sprite = skill == null ? null : art.GetSkillIcon(skill.IconId);
                view.Icon.enabled = view.Icon.sprite != null;
                view.Select.interactable = skill != null;
                view.Action.interactable = canExecute && Run.CanBuy(i);
                view.DirectAction.interactable = canExecute && Run.CanBuyAndDeploy(i);
                view.ActionLabel.text = skill == null ? "품절" : !Run.CanBuy(i)
                    ? "자원 부족" : "보관 " + SkillTavernRun.PurchasePrice;
                view.DirectLabel.text = skill == null ? "품절" : !Run.CanBuy(i)
                    ? "자원 부족" : !Run.CanBuyAndDeploy(i) ? "편성 가득 참" : "편성 " + SkillTavernRun.PurchasePrice;
                view.Surface.color = selection == Selection.Shop && selectedValue == i ? DuelVisualTheme.Selected : DuelVisualTheme.Card;
            }
            for (int i = 0; i < board.Length; i++)
            {
                SkillTavernCard card = Run.Board[i];
                CardView view = board[i];
                view.Name.text = card?.Skill.Name ?? "숨고르기";
                view.Facts.text = card == null ? "빈칸 · ACT 0" : Facts(card.Skill, false);
                view.Action.interactable = canExecute && Run.CanSellBoard(i);
                view.Surface.color = selection == Selection.Board && selectedValue == i ? DuelVisualTheme.Selected : DuelVisualTheme.Card;
            }
            RefreshInventory();
            LegacySkill selected = SelectedSkill();
            detailName.text = selected?.Name ?? "기술 살펴보기";
            detailIcon.sprite = selected == null || selected.IsWait ? null : art.GetSkillIcon(selected.IconId);
            detailIcon.enabled = detailIcon.sprite != null;
            if (selected != null) detailInfo.SetSkill(selected);
            else detailInfo.SetEmptyMessage("상점, 편성, 보관함에서 기술을 선택하면\n자세한 효과를 볼 수 있습니다.");

            SkillTavernCard inventoryCard = selection == Selection.Inventory ? FindInventory(selectedValue) : null;
            selectedName.text = inventoryCard == null ? "보관함에서 기술을 선택하세요" : inventoryCard.Skill.Name + "  (#" + inventoryCard.InstanceId + ")";
            bool canDeploy = inventoryCard != null && Run.CanDeploy(inventoryCard.InstanceId);
            inventoryHint.text = inventoryCard == null ? "보관함의 기술을 선택하면\n추가 비용 없이 편성할 수 있습니다."
                : canDeploy ? "왼쪽 첫 빈칸으로 들어갑니다.\n편성한 뒤에는 판매로만 뺄 수 있습니다."
                : "8칸이 모두 찼습니다.\n편성한 기술을 판매하면 자리가 생깁니다.";
            deployButton.interactable = canExecute && canDeploy;
            sellInventoryButton.interactable = canExecute && inventoryCard != null && Run.CanSellInventory(inventoryCard.InstanceId);
            nextRoundButton.interactable = canExecute;
            resetButton.interactable = reset != null;
        }

        private void RefreshInventory()
        {
            var absent = new List<int>();
            foreach (int id in inventory.Keys) if (FindInventory(id) == null) absent.Add(id);
            foreach (int id in absent)
            {
                CardView removed = inventory[id];
                removed.Select.onClick.RemoveAllListeners();
                removed.Surface.gameObject.SetActive(false);
                Destroy(removed.Surface.gameObject);
                inventory.Remove(id);
            }
            for (int i = 0; i < Run.Inventory.Count; i++)
            {
                SkillTavernCard card = Run.Inventory[i];
                CardView view;
                if (!inventory.TryGetValue(card.InstanceId, out view))
                {
                    int instanceId = card.InstanceId;
                    Image image = Panel("Tavern Inventory Card " + instanceId, inventoryContent,
                        Vector2.zero, new Vector2(210f, 122f), DuelVisualTheme.Card);
                    view = new CardView { Surface = image, Select = SelectButton(image, () => Select(Selection.Inventory, instanceId)) };
                    view.Name = Label("Inventory Card Name", image.transform, new Vector2(0f, 28f),
                        new Vector2(188f, 45f), 22, DuelVisualTheme.Foreground);
                    Fit(view.Name, 15, 22);
                    view.Facts = Label("Inventory Card Facts", image.transform, new Vector2(0f, -20f),
                        new Vector2(188f, 44f), 17, DuelVisualTheme.Muted);
                    inventory.Add(instanceId, view);
                }
                var rect = view.Surface.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(InventoryPitch * i + 110f, -66f);
                view.Name.text = card.Skill.Name;
                view.Facts.text = "#" + card.InstanceId + "  ·  " + Facts(card.Skill, false);
                view.Surface.color = selection == Selection.Inventory && selectedValue == card.InstanceId
                    ? DuelVisualTheme.Selected : DuelVisualTheme.Card;
            }
            inventoryContent.sizeDelta = new Vector2(Mathf.Max(InventoryWidth, InventoryPitch * Run.Inventory.Count), 132f);
            inventoryHeading.text = "보관함 " + Run.Inventory.Count + "  ·  선택 후 편성 또는 판매";
            inventoryEmpty.gameObject.SetActive(Run.Inventory.Count == 0);
        }

        private void Select(Selection kind, int value)
        {
            if (!IsVisible) return;
            selection = kind;
            selectedValue = value;
            Refresh();
        }

        private void Dispatch(SkillTavernAction action, int value)
        {
            if (!IsVisible || Run == null || execute == null) return;
            execute(action, value);
        }

        private LegacySkill SelectedSkill()
        {
            if (Run == null) return null;
            if (selection == Selection.Shop && selectedValue >= 0 && selectedValue < shop.Length) return Run.ShopOffers[selectedValue];
            if (selection == Selection.Board && selectedValue >= 0 && selectedValue < board.Length)
                return Run.Board[selectedValue]?.Skill ?? LegacyCommonActions.Breathe;
            return selection == Selection.Inventory ? FindInventory(selectedValue)?.Skill : null;
        }

        private SkillTavernCard FindInventory(int instanceId)
        {
            foreach (SkillTavernCard card in Run.Inventory) if (card.InstanceId == instanceId) return card;
            return null;
        }

        private CardView BuildShopCard(Transform parent, int index, Vector2 center, Action select, Action buy, Action buyAndDeploy)
        {
            Image image = Panel("Tavern Shop Card " + index, parent, center, new Vector2(350f, 300f), DuelVisualTheme.Card);
            var view = new CardView { Surface = image, Select = SelectButton(image, select) };
            view.Icon = Panel("Shop Card Icon", image.transform, new Vector2(-120f, 93f), new Vector2(62f, 62f), Color.white);
            view.Icon.preserveAspect = true;
            view.Name = Label("Shop Card Name", image.transform, new Vector2(34f, 93f), new Vector2(226f, 64f), 27,
                DuelVisualTheme.Foreground);
            Fit(view.Name, 18, 27);
            view.Facts = Label("Shop Card Facts", image.transform, new Vector2(0f, 12f), new Vector2(304f, 80f), 22,
                DuelVisualTheme.Muted);
            Label("Shop Card Inspect", image.transform, new Vector2(0f, -58f), new Vector2(304f, 34f), 17,
                DuelVisualTheme.Muted).text = "카드를 선택하면 효과를 볼 수 있습니다.";
            view.DirectAction = ActionButton("Tavern Buy And Deploy " + index, image.transform, "편성",
                new Vector2(-79f, -115f), new Vector2(146f, 46f), buyAndDeploy, true);
            view.DirectLabel = view.DirectAction.GetComponentInChildren<Text>();
            view.Action = ActionButton("Tavern Buy " + index, image.transform, "보관", new Vector2(79f, -115f),
                new Vector2(146f, 46f), buy);
            view.ActionLabel = view.Action.GetComponentInChildren<Text>();
            return view;
        }

        private CardView BuildBoardCard(Transform parent, int index, Vector2 center, Action select, Action sell)
        {
            Image image = Panel("Tavern Board Slot " + index, parent, center, new Vector2(204f, 160f), DuelVisualTheme.Card);
            var view = new CardView { Surface = image, Select = SelectButton(image, select) };
            Label("Board Slot Number", image.transform, new Vector2(0f, 59f), new Vector2(180f, 24f), 17,
                DuelVisualTheme.Accent).text = (index + 1).ToString("00");
            view.Name = Label("Board Card Name", image.transform, new Vector2(0f, 20f), new Vector2(180f, 44f), 23,
                DuelVisualTheme.Foreground);
            Fit(view.Name, 15, 23);
            view.Facts = Label("Board Card Facts", image.transform, new Vector2(0f, -18f), new Vector2(180f, 30f), 16,
                DuelVisualTheme.Muted);
            view.Action = ActionButton("Tavern Sell Board " + index, image.transform, "판매 +" + SkillTavernRun.SellPrice,
                new Vector2(0f, -57f), new Vector2(180f, 32f), sell);
            return view;
        }

        private static string Facts(LegacySkill skill, bool power = true)
        {
            string property = skill.Kind == LegacySkillKind.Defence ? "방어" : skill.Property == LegacySkillProperty.Slash ? "참격"
                : skill.Property == LegacySkillProperty.Penetrate ? "관통" : skill.Property == LegacySkillProperty.Hit ? "타격" : "숨고르기";
            string text = property + " · ACT " + skill.Cost;
            return !power || skill.IsWait ? text : text + "\n위력 " + skill.MinPower + "–" + skill.MaxPower
                + (skill.Kind == LegacySkillKind.Attack ? " · " + skill.AttackCount + "회" : string.Empty);
        }

        private static string FighterState(string name, LegacyFighterState fighter)
            => name + " · HP " + (fighter?.Health ?? SkillTavernBattle.FighterHealth)
                + " / " + (fighter?.MaxHealth ?? SkillTavernBattle.FighterHealth)
                + (LegacyPowerBreakRules.PrototypeEnabled ? string.Empty
                    : " · 저항 " + (fighter?.Resistance ?? SkillTavernBattle.FighterResistance)
                        + " / " + (fighter?.MaxResistance ?? SkillTavernBattle.FighterResistance));

        private Button SelectButton(Image image, Action select)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button);
            button.onClick.AddListener(() => { if (IsVisible) { ClearSelection(); select?.Invoke(); } });
            return button;
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
            Text label = Label("Button Label", image.transform, Vector2.zero, size - new Vector2(14f, 4f), 20,
                primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground, TextAnchor.MiddleCenter);
            Fit(label, 15, 20);
            label.text = caption;
            button.onClick.AddListener(() => { if (IsVisible) { ClearSelection(); action?.Invoke(); } });
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

        private static void Fit(Text text, int minimum, int maximum)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimum;
            text.resizeTextMaxSize = maximum;
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

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            root.gameObject.SetActive(false);
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            inventory.Clear();
            Run = null;
            displayedBattle = null;
            Destroy(root.gameObject);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
        }

        private static void Destroy(GameObject target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
