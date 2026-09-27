using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Selection first, then one explicit transaction. CampaignRun owns currency, ownership and upgrades.</summary>
    public sealed class CampaignShopHud : IDisposable
    {
        public enum ShopCategory { Purchase, Upgrade }

        public sealed class ViewState
        {
            public ShopCategory Category { get; internal set; }
            public int SelectedSkillId { get; internal set; }
            internal int PurchaseSkillId, UpgradeSkillId;
            internal float PurchaseScroll = 1f, UpgradeScroll = 1f;
            public void Reset()
            {
                Category = ShopCategory.Purchase;
                SelectedSkillId = PurchaseSkillId = UpgradeSkillId = 0;
                PurchaseScroll = UpgradeScroll = 1f;
            }
        }

        private sealed class SkillCard
        {
            public int SkillId;
            public Button Button;
            public Image Background, Icon;
            public Text Name, Act, Status;
        }

        private readonly RectTransform root, listItems;
        private readonly RectTransform detailFrame, detailSurface, detailRule;
        private readonly SkillLaneBadge detailStyle;
        private readonly LegacyDuelArt art;
        private readonly CampaignRun run;
        private readonly Action<int> acquire, upgrade;
        private readonly Button purchaseTab, upgradeTab, primaryAction;
        private readonly Text listCount, detailName, purpose, values, effect, damageHint, preview, price, wallet, availability, actionCaption;
        private readonly SkillInfoView detailInfo;
        private readonly Image detailIcon;
        private readonly ScrollRect skillScroll;
        private readonly List<SkillCard> cards = new List<SkillCard>();
        private ShopCategory builtCategory;
        private int builtCount = -1;
        private bool disposed;
        private static Color CardColor => DuelVisualTheme.Card;
        private static Color Selected => DuelVisualTheme.Selected;
        private static Color Border => DuelVisualTheme.Border;
        private static Color Accent => DuelVisualTheme.Accent;
        private static Color Gold => DuelVisualTheme.Accent;
        private static Color Foreground => DuelVisualTheme.Foreground;
        private static Color Muted => DuelVisualTheme.Muted;
        private static readonly string[] LaneNames = { "Q", "W", "E" };

        public GameObject Root => root.gameObject;
        public ViewState State { get; }

        public CampaignShopHud(RectTransform parent, LegacyDuelArt art, CampaignRun run,
            Action<int> acquire, Action<int> upgrade, ViewState state = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.acquire = acquire;
            this.upgrade = upgrade;
            State = state ?? new ViewState();
            root = Rect("Shop Content", parent, Vector2.zero, Vector2.zero);
            Stretch(root);
            purchaseTab = ActionButton("Shop Category Purchase", root, "기술 구매", new Vector2(-487f, 246f),
                new Vector2(220f, 42f), () => SelectCategory(ShopCategory.Purchase));
            upgradeTab = ActionButton("Shop Category Upgrade", root, "보유 기술 강화", new Vector2(-249f, 246f),
                new Vector2(220f, 42f), () => SelectCategory(ShopCategory.Upgrade));
            listCount = Label("Shop List Count", root, new Vector2(56f, 246f), new Vector2(216f, 30f),
                17, Muted, TextAnchor.MiddleRight);

            var listFrame = Panel("Shop Skill List", root, new Vector2(-216f, -53f), new Vector2(760f, 544f),
                DuelVisualTheme.Track);
            DuelVisualTheme.DressPanel(listFrame);
            listFrame.raycastTarget = true;
            skillScroll = listFrame.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel("Shop Skill Viewport", listFrame.transform, new Vector2(-8f, 0f), new Vector2(738f, 544f), Color.clear);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            listItems = Rect("Shop Skill Items", viewport.transform, Vector2.zero, Vector2.zero);
            listItems.anchorMin = new Vector2(0f, 1f);
            listItems.anchorMax = Vector2.one;
            listItems.pivot = new Vector2(.5f, 1f);
            skillScroll.viewport = viewport.rectTransform;
            skillScroll.content = listItems;
            skillScroll.horizontal = false;
            skillScroll.vertical = true;
            skillScroll.inertia = false;
            skillScroll.movementType = ScrollRect.MovementType.Clamped;
            skillScroll.scrollSensitivity = 44f;
            var track = Panel("Shop Skill Scrollbar", listFrame.transform, new Vector2(374f, 0f), new Vector2(8f, 544f), Border);
            track.raycastTarget = true;
            var handle = Panel("Shop Skill Scroll Handle", track.transform, Vector2.zero, Vector2.zero, Accent);
            Stretch(handle.rectTransform);
            handle.raycastTarget = true;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            skillScroll.verticalScrollbar = scrollbar;
            skillScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            skillScroll.onValueChanged.AddListener(_ => SaveScrollPosition());

            var detailBorder = Panel("Shop Selected Detail", root, new Vector2(423f, -50f), new Vector2(384f, 644f), Border);
            var detail = Panel("Shop Detail Surface", detailBorder.transform, Vector2.zero, new Vector2(380f, 640f), DuelVisualTheme.Paper);
            detailFrame = detailBorder.rectTransform;
            detailSurface = detail.rectTransform;
            DuelVisualTheme.Frame(detail, true);
            detailStyle = new SkillLaneBadge(detail.transform, art.UIFont, "Shop Detail Heading", Vector2.zero,
                250f, 24f, 16);
            detailStyle.Clear("선택한 기술");
            detailIcon = Panel("Shop Detail Icon", detail.transform, new Vector2(-132f, 235f), Vector2.one * 74f, Color.white);
            detailIcon.preserveAspect = true;
            detailName = Label("Shop Detail Name", detail.transform, new Vector2(45f, 257f), new Vector2(246f, 36f), 24);
            Fit(detailName, 17, 24);
            purpose = Label("Shop Detail Purpose", detail.transform, new Vector2(45f, 223f), new Vector2(246f, 28f), 17, Accent);
            Fit(purpose, 14, 17);
            detailInfo = new SkillInfoView(detail.transform, art.UIFont, new Vector2(0f, 64f), 334f, "Shop");
            values = detailInfo.PowerText;
            effect = detailInfo.EffectText;
            damageHint = detailInfo.DamageText;
            detailRule = Panel("Shop Detail Rule", detail.transform, new Vector2(0f, -55f), new Vector2(334f, 2f), Border).rectTransform;
            preview = Label("Shop Upgrade Preview", detail.transform, new Vector2(0f, -91f), new Vector2(334f, 52f), 18);
            price = Label("Shop Price", detail.transform, new Vector2(0f, -139f), new Vector2(334f, 32f), 22, Gold);
            wallet = Label("Shop Wallet", detail.transform, new Vector2(0f, -174f), new Vector2(334f, 26f), 17, Muted);
            availability = Label("Shop Availability", detail.transform, new Vector2(0f, -218f), new Vector2(334f, 46f), 17, Muted);
            detailName.color = purpose.color = preview.color
                = price.color = wallet.color = availability.color = DuelVisualTheme.Ink;
            primaryAction = ActionButton("Shop Primary Action", detail.transform, "기술 획득", new Vector2(0f, -273f),
                new Vector2(334f, 50f), PerformTransaction, true);
            actionCaption = primaryAction.GetComponentInChildren<Text>();
            Refresh();
        }

        public void SelectCategory(ShopCategory category)
        {
            if (disposed || State.Category == category) return;
            SaveScrollPosition();
            RememberSelection();
            State.Category = category;
            State.SelectedSkillId = category == ShopCategory.Purchase ? State.PurchaseSkillId : State.UpgradeSkillId;
            Refresh();
        }

        public void SelectSkill(int skillId)
        {
            if (disposed || !ContainsCard(skillId)) return;
            State.SelectedSkillId = skillId;
            RememberSelection();
            Refresh();
        }

        public void Refresh()
        {
            if (disposed) return;
            int itemCount = State.Category == ShopCategory.Purchase ? CampaignSkillCatalog.AcquisitionSkills.Count : run.OwnedSkills.Count;
            if (builtCount != itemCount || builtCategory != State.Category) BuildCards();
            if (!ContainsCard(State.SelectedSkillId)) State.SelectedSkillId = cards.Count > 0 ? cards[0].SkillId : 0;
            RememberSelection();
            purchaseTab.GetComponent<Image>().color = State.Category == ShopCategory.Purchase ? Selected : CardColor;
            upgradeTab.GetComponent<Image>().color = State.Category == ShopCategory.Upgrade ? Selected : CardColor;
            listCount.text = (State.Category == ShopCategory.Purchase ? "판매 기술 " : "보유 기술 ") + cards.Count + "개";
            foreach (SkillCard card in cards)
            {
                LegacySkill skill = FindSkill(card.SkillId);
                CampaignOwnedSkill owned = FindOwned(card.SkillId);
                card.Icon.sprite = art.GetSkillIcon(skill.IconId);
                card.Name.text = skill.Name;
                card.Act.text = "ACT " + skill.Cost;
                card.Status.text = State.Category == ShopCategory.Purchase ? owned != null ? "획득 완료" : string.Empty
                    : "강화 " + owned.Level + "/3";
                card.Background.color = State.SelectedSkillId == card.SkillId ? Selected : CardColor;
            }
            RefreshDetail();
        }

        private void BuildCards()
        {
            foreach (SkillCard card in cards)
            {
                card.Button.onClick.RemoveAllListeners();
                card.Button.gameObject.SetActive(false);
                Destroy(card.Button.gameObject);
            }
            cards.Clear();
            builtCategory = State.Category;
            if (State.Category == ShopCategory.Purchase)
                foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) AddCard(skill.Id);
            else foreach (CampaignOwnedSkill owned in run.OwnedSkills) AddCard(owned.SkillId);
            builtCount = cards.Count;
            int rows = (cards.Count + 1) / 2;
            listItems.sizeDelta = new Vector2(0f, Mathf.Max(544f, rows * 124f + 6f));
            skillScroll.verticalNormalizedPosition = State.Category == ShopCategory.Purchase ? State.PurchaseScroll : State.UpgradeScroll;
        }

        private void AddCard(int skillId)
        {
            int index = cards.Count;
            var card = new SkillCard { SkillId = skillId };
            card.Button = ActionButton("Shop Skill " + skillId, listItems, null,
                new Vector2(index % 2 == 0 ? -183f : 183f, -6f - index / 2 * 124f),
                new Vector2(350f, 106f), () => SelectSkill(skillId));
            RectTransform rect = card.Button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            card.Background = card.Button.GetComponent<Image>();
            card.Icon = Panel("Shop Card Icon", card.Button.transform, new Vector2(-125f, 0f), Vector2.one * 58f, Color.white);
            card.Icon.preserveAspect = true;
            card.Name = Label("Shop Card Name", card.Button.transform, new Vector2(30f, 18f), new Vector2(238f, 32f), 20);
            Fit(card.Name, 15, 20);
            card.Act = Label("Shop Card ACT", card.Button.transform, new Vector2(-13f, -20f), new Vector2(154f, 24f), 16, Muted);
            card.Status = Label("Shop Card State", card.Button.transform, new Vector2(104f, -20f), new Vector2(104f, 24f), 14,
                Accent, TextAnchor.MiddleRight);
            cards.Add(card);
        }

        private void RefreshDetail()
        {
            LegacySkill skill = FindSkill(State.SelectedSkillId);
            if (skill == null)
            {
                detailStyle.Clear("선택한 기술");
                detailIcon.enabled = false;
                detailName.text = "기술을 선택하세요";
                detailInfo.Clear();
                purpose.text = preview.text = price.text = string.Empty;
                wallet.text = "보유 재화 " + run.Currency;
                availability.text = "선택할 기술이 없습니다.";
                primaryAction.interactable = false;
                actionCaption.color = Muted;
                LayoutDetail();
                return;
            }
            detailIcon.enabled = true;
            detailIcon.sprite = art.GetSkillIcon(skill.IconId);
            detailName.text = skill.Name;
            detailStyle.SetLane(skill.LaneIndex);
            purpose.text = CampaignSkillText.Purpose(skill);
            detailInfo.SetSkill(skill);
            wallet.text = "보유 재화 " + run.Currency;
            CampaignOwnedSkill owned = FindOwned(skill.Id);
            bool editing = run.Phase == CampaignPhase.Lobby || run.Phase == CampaignPhase.Maintenance;
            if (State.Category == ShopCategory.Purchase)
            {
                CampaignSkillOffer offer = FindOffer(skill.Id);
                preview.text = string.Empty;
                price.text = owned != null ? "보유 중인 기술" : offer != null ? "구매 비용 " + offer.Price : "판매하지 않는 기술";
                actionCaption.text = owned != null ? "획득 완료" : "기술 획득";
                availability.text = owned != null ? "편성 화면에서 장착할 수 있습니다."
                    : !editing ? "로비에서 이용할 수 있습니다."
                    : offer == null ? "구매할 수 없는 기술입니다."
                    : run.Currency < offer.Price ? "재화 " + (offer.Price - run.Currency) + " 부족"
                    : "구매하면 보유 기술에 추가됩니다.";
                primaryAction.interactable = editing && owned == null && offer != null && run.Currency >= offer.Price;
            }
            else
            {
                bool maximum = owned.Level >= CampaignOwnedSkill.MaximumLevel;
                preview.text = maximum ? "강화 3/3 · 최대 단계" : UpgradePreview(owned);
                price.text = maximum ? "추가 강화 없음" : "강화 비용 " + owned.UpgradeCost;
                actionCaption.text = maximum ? "강화 완료" : "강화하기";
                availability.text = maximum ? "최대 단계까지 강화했습니다."
                    : !editing ? "로비에서 이용할 수 있습니다."
                    : run.Currency < owned.UpgradeCost ? "재화 " + (owned.UpgradeCost - run.Currency) + " 부족"
                    : "강화하면 위력이 2 높아집니다.";
                primaryAction.interactable = editing && !maximum && run.Currency >= owned.UpgradeCost;
            }
            actionCaption.color = primaryAction.interactable ? DuelVisualTheme.Ink : Foreground;
            LayoutDetail();
        }

        private void LayoutDetail()
        {
            bool hasPreview = !string.IsNullOrEmpty(preview.text);
            preview.gameObject.SetActive(hasPreview);
            float previewContentHeight = hasPreview ? Mathf.Ceil(Mathf.Max(24f, preview.preferredHeight)) + 2f : 0f;
            float previewHeight = hasPreview ? previewContentHeight + 8f : 0f;
            float height = 104f + detailInfo.Height + 20f + previewHeight + 32f + 6f + 40f + 8f + 50f + 16f;
            detailFrame.sizeDelta = new Vector2(384f, height + 4f);
            detailFrame.anchoredPosition = new Vector2(423f, 272f - (height + 4f) / 2f);
            detailSurface.sizeDelta = new Vector2(380f, height);
            float top = height / 2f;
            detailStyle.Root.anchoredPosition = new Vector2(-18f, top - 18f);
            detailIcon.rectTransform.anchoredPosition = new Vector2(-105f, top - 62f);
            detailIcon.rectTransform.sizeDelta = Vector2.one * 64f;
            detailName.rectTransform.anchoredPosition = new Vector2(20f, top - 49f);
            detailName.rectTransform.sizeDelta = new Vector2(162f, 36f);
            purpose.rectTransform.anchoredPosition = new Vector2(20f, top - 78f);
            purpose.rectTransform.sizeDelta = new Vector2(162f, 26f);
            detailInfo.PlaceTop(top - 104f);
            float cursor = top - 104f - detailInfo.Height - 8f;
            detailRule.anchoredPosition = new Vector2(0f, cursor - 1f);
            cursor -= 12f;
            if (hasPreview)
            {
                preview.rectTransform.sizeDelta = new Vector2(334f, previewContentHeight);
                preview.rectTransform.anchoredPosition = new Vector2(0f, cursor - previewContentHeight / 2f);
                cursor -= previewHeight;
            }
            price.rectTransform.anchoredPosition = new Vector2(-78f, cursor - 16f);
            price.rectTransform.sizeDelta = new Vector2(178f, 32f);
            Fit(price, 16, 21);
            wallet.rectTransform.anchoredPosition = new Vector2(93f, cursor - 16f);
            wallet.rectTransform.sizeDelta = new Vector2(148f, 26f);
            wallet.alignment = TextAnchor.MiddleRight;
            Fit(wallet, 14, 17);
            cursor -= 38f;
            availability.rectTransform.anchoredPosition = new Vector2(0f, cursor - 20f);
            availability.rectTransform.sizeDelta = new Vector2(334f, 40f);
            cursor -= 48f;
            primaryAction.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, cursor - 25f);
        }

        private static string UpgradePreview(CampaignOwnedSkill owned)
        {
            int nextLevel = owned.Level + 1;
            int min = owned.BaseSkill.MinPower + nextLevel * 2;
            int max = owned.BaseSkill.MaxPower + nextLevel * 2;
            string nextPower = min == max ? min.ToString() : min + "–" + max;
            return "강화 " + owned.Level + "/3 → " + nextLevel + "/3\n"
                + (owned.Skill.Kind == LegacySkillKind.Defence ? "방어 " : "위력 ")
                + CampaignSkillText.Power(owned.Skill) + " → " + nextPower;
        }

        private void PerformTransaction()
        {
            if (disposed) return;
            bool editing = run.Phase == CampaignPhase.Lobby || run.Phase == CampaignPhase.Maintenance;
            if (!editing) return;
            int id = State.SelectedSkillId;
            if (State.Category == ShopCategory.Purchase)
            {
                CampaignSkillOffer offer = FindOffer(id);
                if (FindOwned(id) != null || offer == null || run.Currency < offer.Price) return;
                acquire?.Invoke(id);
            }
            else
            {
                CampaignOwnedSkill owned = FindOwned(id);
                if (owned == null || owned.Level >= CampaignOwnedSkill.MaximumLevel || run.Currency < owned.UpgradeCost) return;
                upgrade?.Invoke(id);
            }
            if (!disposed) Refresh();
        }

        private void RememberSelection()
        {
            if (State.Category == ShopCategory.Purchase) State.PurchaseSkillId = State.SelectedSkillId;
            else State.UpgradeSkillId = State.SelectedSkillId;
        }

        private void SaveScrollPosition()
        {
            if (disposed) return;
            if (State.Category == ShopCategory.Purchase) State.PurchaseScroll = skillScroll.verticalNormalizedPosition;
            else State.UpgradeScroll = skillScroll.verticalNormalizedPosition;
        }

        private bool ContainsCard(int id)
        { foreach (SkillCard card in cards) if (card.SkillId == id) return true; return false; }

        private CampaignOwnedSkill FindOwned(int id)
        { foreach (CampaignOwnedSkill owned in run.OwnedSkills) if (owned.SkillId == id) return owned; return null; }

        private CampaignSkillOffer FindOffer(int id)
        { foreach (CampaignSkillOffer offer in run.Offers) if (offer.SkillId == id) return offer; return null; }

        private LegacySkill FindSkill(int id)
        {
            CampaignOwnedSkill owned = FindOwned(id);
            if (owned != null) return owned.Skill;
            CampaignSkillOffer offer = FindOffer(id);
            if (offer != null) return offer.Skill;
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) if (skill.Id == id) return skill;
            return null;
        }

        public void Dispose()
        {
            if (disposed) return;
            SaveScrollPosition();
            RememberSelection();
            disposed = true;
            skillScroll.onValueChanged.RemoveAllListeners();
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position, Vector2 size, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size, primary ? Accent : CardColor);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            if (caption != null)
                Label("Button Label", image.transform, Vector2.zero, size - new Vector2(12f, 4f), 20,
                    primary ? DuelVisualTheme.Ink : Foreground, TextAnchor.MiddleCenter).text = caption;
            button.onClick.AddListener(() =>
            {
                if (disposed) return;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                action?.Invoke();
            });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize,
            Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = art.UIFont;
            label.fontSize = fontSize;
            label.color = color ?? Foreground;
            label.alignment = alignment;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static void Fit(Text label, int min, int max)
        { label.resizeTextForBestFit = true; label.resizeTextMinSize = min; label.resizeTextMaxSize = max; }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.anchoredPosition = rect.sizeDelta = Vector2.zero; }

        private static void Destroy(GameObject target)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(target); else UnityEngine.Object.DestroyImmediate(target); }
    }
}
