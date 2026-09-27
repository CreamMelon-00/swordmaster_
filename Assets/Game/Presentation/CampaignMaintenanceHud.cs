using System;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Event-refreshed maintenance view. The campaign owns all purchases and progression.</summary>
    public sealed class CampaignMaintenanceHud : IDisposable
    {
        private readonly LegacyDuelArt art;
        private readonly Action<int> acquire, upgrade;
        private readonly Action continueRun, restartRun;
        private readonly RectTransform root, content;
        private ScrollRect ownedScroll, offerScroll;
        private CampaignPhase shownPhase;
        private bool disposed;
        private readonly bool assetsAvailable;

        private static readonly Color Surface = DuelVisualTheme.Surface;
        private static readonly Color Card = DuelVisualTheme.Card;
        private static readonly Color Border = DuelVisualTheme.Border;
        private static readonly Color Accent = DuelVisualTheme.Accent;
        private static readonly Color Foreground = DuelVisualTheme.Foreground;
        private static readonly Color Muted = DuelVisualTheme.Muted;
        private static readonly Color Gold = DuelVisualTheme.Paper;
        private const float WindowWidth = 1440, WindowHeight = 900;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public bool HasRequiredAssets => assetsAvailable && art.UIFont != null;

        public CampaignMaintenanceHud(Transform parent, LegacyDuelArt art, Action<int> acquire,
            Action<int> upgrade, Action continueRun, Action restartRun)
        {
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.acquire = acquire;
            this.upgrade = upgrade;
            this.continueRun = continueRun;
            this.restartRun = restartRun;
            assetsAvailable = true;
            for (int i = 1; i <= LegacyDuelArt.SkillIconCount; i++)
                if (art.GetSkillIcon(i) == null) assetsAvailable = false;

            root = Rect("Campaign Maintenance HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var dim = Panel("Maintenance Dim", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .82f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var window = Panel("Maintenance Window", root, Vector2.zero,
                new Vector2(WindowWidth + 4, WindowHeight + 4), Border);
            var inner = Panel("Maintenance Surface", window.transform, Vector2.zero,
                new Vector2(WindowWidth, WindowHeight), Surface);
            DuelVisualTheme.DressPanel(inner);
            inner.raycastTarget = true;
            content = Rect("Maintenance Content", inner.transform, Vector2.zero,
                new Vector2(WindowWidth, WindowHeight));
            Hide();
        }

        public void Show(CampaignRun run)
        {
            if (disposed) throw new ObjectDisposedException(nameof(CampaignMaintenanceHud));
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.Phase == CampaignPhase.Battle) { Hide(); return; }

            bool preserveScroll = IsVisible && shownPhase == CampaignPhase.Maintenance
                && run.Phase == CampaignPhase.Maintenance;
            float ownedPosition = preserveScroll && ownedScroll != null ? ownedScroll.verticalNormalizedPosition : 1;
            float offerPosition = preserveScroll && offerScroll != null ? offerScroll.verticalNormalizedPosition : 1;
            ClearSelection();
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            ownedScroll = offerScroll = null;
            shownPhase = run.Phase;
            root.gameObject.SetActive(true);
            if (run.Phase == CampaignPhase.Maintenance)
                BuildMaintenance(run, ownedPosition, offerPosition);
            else
                BuildResult(run);
            Canvas.ForceUpdateCanvases();
        }

        public void Hide()
        {
            if (disposed) return;
            ClearSelection();
            root.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            Hide();
            disposed = true;
            Destroy(root.gameObject);
        }

        private void BuildMaintenance(CampaignRun run, float ownedPosition, float offerPosition)
        {
            Label("Title", content, "정비", new Vector2(-660, 392), new Vector2(500, 50), 36);
            Label("Stage Summary", content, $"{run.StageNumber} / {run.StageCount}  ·  {run.CurrentStage.Name} 클리어",
                new Vector2(-660, 347), new Vector2(870, 28), 20, Muted);
            Label("Wallet", content, $"재화  {run.Currency}", new Vector2(520, 392), new Vector2(300, 48),
                30, Gold, TextAnchor.MiddleRight);
            Label("Clear Reward", content, $"클리어 보상  +{run.LastReward}", new Vector2(520, 347),
                new Vector2(300, 28), 19, Muted, TextAnchor.MiddleRight);
            Rule("Header Rule", 314);
            Label("Acquisition Heading", content, "새 기술 획득", new Vector2(-660, 283), new Vector2(600, 32), 24);
            Label("Owned Heading", content, $"보유 기술 강화  ·  {run.OwnedSkills.Count}개  ·  스크롤", new Vector2(16, 283),
                new Vector2(640, 32), 24);

            offerScroll = Scroll("Acquisition", new Vector2(-358, -12), new Vector2(624, 524), out var offers);
            const float offerWidth = 304, offerHeight = 164, gap = 12;
            offers.sizeDelta = new Vector2(0, Mathf.Max(524, ((run.Offers.Count + 1) / 2) * (offerHeight + gap) - gap));
            for (int i = 0; i < run.Offers.Count; i++)
            {
                var offer = run.Offers[i];
                var card = GridCard("Offer " + offer.SkillId, offers, i, offerWidth, offerHeight, gap);
                Icon(card, offer.Skill, new Vector2(-112, 46), 56);
                Label("Skill Name", card, offer.Skill.Name, new Vector2(-72, 49), new Vector2(204, 40), 20);
                Label("Skill Values", card, Values(offer.Skill), new Vector2(-72, 15), new Vector2(204, 36), 15, Accent);
                Label("Skill Description", card, offer.Skill.Description, new Vector2(-137, -21), new Vector2(274, 34), 14, Muted);
                int id = offer.SkillId;
                Button("Acquire Skill " + id, card, $"획득  ·  {offer.Price}", new Vector2(0, -61),
                    new Vector2(274, 36), run.Currency >= offer.Price, () => acquire?.Invoke(id));
            }
            if (run.Offers.Count == 0)
                Label("All Skills Acquired", offers, "모든 기술을 획득했습니다.", new Vector2(0, -100),
                    new Vector2(600, 60), 20, Muted, TextAnchor.MiddleCenter);

            ownedScroll = Scroll("Owned Skills", new Vector2(348, -12), new Vector2(684, 524), out var owned);
            const float ownedWidth = 334, ownedHeight = 146;
            owned.sizeDelta = new Vector2(0, Mathf.Max(524, ((run.OwnedSkills.Count + 1) / 2) * (ownedHeight + gap) - gap));
            for (int i = 0; i < run.OwnedSkills.Count; i++)
            {
                var item = run.OwnedSkills[i];
                var card = GridCard("Owned Skill " + item.SkillId, owned, i, ownedWidth, ownedHeight, gap);
                Icon(card, item.Skill, new Vector2(-123, 35), 48);
                Label("Skill Name", card, item.Skill.Name, new Vector2(-91, 44), new Vector2(242, 38), 20);
                string power = item.Level < CampaignOwnedSkill.MaximumLevel
                    ? $"위력 {Power(item.Skill)} → {item.Skill.MinPower + 2}–{item.Skill.MaxPower + 2}"
                    : $"위력 {Power(item.Skill)}  ·  최대 강화";
                Label("Upgrade Power", card, power, new Vector2(-91, 13), new Vector2(242, 22), 16, Accent);
                Label("Skill Values", card, $"ACT {item.Skill.Cost}  ·  {Lane(item.Skill)}열  ·  강화 {item.Level}/3",
                    new Vector2(-153, -14), new Vector2(306, 22), 16, Muted);
                int id = item.SkillId;
                bool maximum = item.Level >= CampaignOwnedSkill.MaximumLevel;
                Button("Upgrade Skill " + id, card, maximum ? "강화 완료" : $"강화  ·  {item.UpgradeCost}",
                    new Vector2(0, -51), new Vector2(306, 34), !maximum && run.Currency >= item.UpgradeCost,
                    () => upgrade?.Invoke(id));
            }
            ownedScroll.verticalNormalizedPosition = ownedPosition;
            offerScroll.verticalNormalizedPosition = offerPosition;
            Rule("Footer Rule", -302);
            int nextNumber = run.StageNumber + 1;
            Label("Next Stage", content,
                $"다음  {nextNumber} / {run.StageCount}  ·  적 체력 {80 + 15 * run.StageNumber}  ·  저항 {15 + 3 * run.StageNumber}  ·  위력 +{run.StageNumber}",
                new Vector2(-660, -337), new Vector2(1300, 32), 20, Foreground);
            Label("Maintenance Hint", content, "획득한 기술은 해당 기술열에 추가됩니다.\n강화는 ACT를 유지한 채 위력을 높입니다. 다음 전투에서 체력·저항력이 회복됩니다.",
                new Vector2(-660, -395), new Vector2(1010, 54), 17, Muted);
            Button("Continue Run", content, "다음 전투  [Enter]", new Vector2(548, -394),
                new Vector2(260, 60), true, () => continueRun?.Invoke(), true);
        }

        private void BuildResult(CampaignRun run)
        {
            bool complete = run.Phase == CampaignPhase.Completed;
            Label("Title", content, complete ? "숲의 끝까지" : "다시 준비할 시간", new Vector2(0, 225),
                new Vector2(1260, 90), 48, complete ? Accent : Foreground, TextAnchor.MiddleCenter);
            Label("Result Summary", content, complete ? $"{run.StageCount}개 스테이지 클리어" : $"{run.StageNumber} / {run.StageCount}  ·  {run.CurrentStage.Name}",
                new Vector2(0, 145), new Vector2(1260, 48), 28, Muted, TextAnchor.MiddleCenter);
            Label("Wallet", content, complete ? $"보유 재화  {run.Currency}  ·  마지막 보상 +{run.LastReward}" : $"보유 재화  {run.Currency}",
                new Vector2(0, 42), new Vector2(1260, 48), 30, Gold, TextAnchor.MiddleCenter);
            Label("Result Hint", content, complete ? "새 여정을 시작하면 획득한 기술과 강화, 재화가 초기화됩니다."
                : "획득한 기술과 강화, 재화는 유지됩니다.\n체력·저항력을 회복하고 같은 스테이지에 다시 도전합니다.",
                new Vector2(0, -40), new Vector2(1260, 76), 21, Muted, TextAnchor.MiddleCenter);
            Button("Continue Run", content, complete ? "새 여정  [Enter]" : "다시 도전  [Enter]", new Vector2(0, -158),
                new Vector2(430, 70), true, () => continueRun?.Invoke(), true);
            if (!complete)
                Button("Restart Run", content, "새 여정  ·  초기화", new Vector2(0, -257), new Vector2(320, 48),
                    true, () => restartRun?.Invoke());
        }

        private ScrollRect Scroll(string name, Vector2 position, Vector2 size, out RectTransform items)
        {
            var frame = Rect(name + " Scroll", content, position, size);
            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel(name + " Viewport", frame, Vector2.zero, size, Color.clear).rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.GetComponent<Image>().raycastTarget = true;
            items = Rect(name + " Items", viewport, Vector2.zero, Vector2.zero);
            items.anchorMin = new Vector2(0, 1);
            items.anchorMax = Vector2.one;
            items.pivot = new Vector2(0, 1);
            scroll.viewport = viewport;
            scroll.content = items;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 44;
            scroll.inertia = false;
            var track = Panel(name + " Scrollbar", frame, new Vector2(size.x * .5f + 12, 0),
                new Vector2(6, size.y), DuelVisualTheme.Track);
            track.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            var handle = Panel("Scroll Handle", track.transform, Vector2.zero, new Vector2(6, size.y), Accent);
            Stretch(handle.rectTransform);
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        private RectTransform GridCard(string name, RectTransform parent, int index, float width, float height, float gap)
        {
            var cardImage = Panel(name, parent, Vector2.zero, new Vector2(width, height), Card);
            DuelVisualTheme.Frame(cardImage);
            var card = cardImage.rectTransform;
            card.anchorMin = card.anchorMax = new Vector2(0, 1);
            card.pivot = new Vector2(0, 1);
            card.anchoredPosition = new Vector2((index % 2) * (width + gap), -(index / 2) * (height + gap));
            // Children use the card centre while the card itself is laid out from the viewport's top left.
            var inner = Rect("Card Content", card, new Vector2(width * .5f, -height * .5f), new Vector2(width, height));
            inner.anchorMin = inner.anchorMax = new Vector2(0, 1);
            return inner;
        }

        private void Icon(Transform parent, LegacySkill skill, Vector2 position, float size)
        {
            var image = Panel("Skill Icon", parent, position, Vector2.one * size, Color.white);
            image.sprite = art.GetSkillIcon(skill.IconId);
            image.preserveAspect = true;
        }

        private Button Button(string name, Transform parent, string caption, Vector2 position, Vector2 size,
            bool enabled, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size, primary ? Accent : DuelVisualTheme.RaisedSurface);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            button.interactable = enabled;
            Label("Button Label", image.transform, caption, Vector2.zero, size - new Vector2(12, 4), primary ? 23 : 18,
                primary ? DuelVisualTheme.Ink : Foreground, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => { ClearSelection(); action?.Invoke(); });
            return button;
        }

        private Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize,
            Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = Rect(name, parent, position, size);
            if (alignment == TextAnchor.MiddleLeft || alignment == TextAnchor.UpperLeft)
                rect.pivot = new Vector2(0, .5f);
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

        private void Rule(string name, float y)
            => Panel(name, content, new Vector2(0, y), new Vector2(WindowWidth - 80, 2), Border);

        private static string Power(LegacySkill skill)
            => skill.MinPower == skill.MaxPower ? skill.MinPower.ToString() : $"{skill.MinPower}–{skill.MaxPower}";
        private static string Lane(LegacySkill skill) => skill.LaneIndex == 0 ? "Q" : skill.LaneIndex == 1 ? "W" : "E";
        private static string Values(LegacySkill skill)
            => $"ACT {skill.Cost}  ·  {Lane(skill)}열\n위력 {Power(skill)}  ·  {skill.AttackCount}연타";

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void ClearSelection()
        {
            if (EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(root)) EventSystem.current.SetSelectedGameObject(null);
        }

        private static void Destroy(GameObject value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
