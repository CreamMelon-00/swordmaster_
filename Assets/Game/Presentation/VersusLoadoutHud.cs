using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Match-only preparation overlay. The caller owns the selected loadout and match start.</summary>
    public sealed class VersusLoadoutHud : IDisposable
    {
        public const int SortingOrder = 280;

        private sealed class SkillCard
        {
            public int Id;
            public Button Button;
            public Image Image, Icon;
            public Text Name;
        }

        private readonly LegacyDuelArt art;
        private readonly RectTransform root, availableContent;
        private readonly GameObject ownedEventSystem;
        private readonly Action confirmed, cancelled, changed;
        private readonly Button[,] slotButtons = new Button[VersusLoadout.LaneCount, VersusLoadout.SlotsPerLane];
        private readonly Image[,] slotIcons = new Image[VersusLoadout.LaneCount, VersusLoadout.SlotsPerLane];
        private readonly Text[,] slotNames = new Text[VersusLoadout.LaneCount, VersusLoadout.SlotsPerLane];
        private readonly Image[,] slotTargets = new Image[VersusLoadout.LaneCount, VersusLoadout.SlotsPerLane];
        private readonly List<SkillCard> availableCards = new List<SkillCard>();
        private readonly Text heading, confirmLabel, hint, detailName, detailKind;
        private readonly Image detailIcon;
        private readonly SkillInfoView detailInfo;
        private readonly Button resetButton;
        private VersusLoadout loadout;
        private int selectedId;
        private int[] openingIds;
        private bool disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public Button ConfirmButton { get; }
        public Button CancelButton { get; }
        public VersusLoadout Loadout => loadout;

        public VersusLoadoutHud(Transform parent, LegacyDuelArt art, VersusLoadout loadout,
            Action confirm = null, Action cancel = null, Action changed = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            confirmed = confirm;
            cancelled = cancel;
            this.changed = changed;

            root = Rect("Versus Loadout HUD", parent, Vector2.zero, Vector2.zero);
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
                ownedEventSystem = new GameObject("Versus Loadout EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(parent, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var backdrop = Panel("Versus Loadout Backdrop", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .9f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            var border = Panel("Versus Loadout Border", root, Vector2.zero,
                new Vector2(1496f, 940f), DuelVisualTheme.Border);
            var paper = Panel("Versus Loadout Surface", border.transform, Vector2.zero,
                new Vector2(1488f, 932f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(paper);

            heading = Label("Versus Loadout Heading", paper.transform, new Vector2(0f, 386f),
                new Vector2(1220f, 56f), 43, DuelVisualTheme.Foreground, TextAnchor.MiddleCenter);
            Label("Versus Loadout Subheading", paper.transform, new Vector2(0f, 334f),
                new Vector2(1190f, 34f), 20, DuelVisualTheme.Muted, TextAnchor.MiddleCenter).text =
                "Q · W · E 각 3칸의 기술을 선택하세요. 고른 기술을 같은 열의 칸에 배치할 수 있습니다.";

            const float firstX = -572f, pitch = 258f;
            string[] lanes = { "Q", "W", "E" };
            for (int lane = 0; lane < VersusLoadout.LaneCount; lane++)
            {
                int selectedLane = lane;
                float x = firstX + lane * pitch;
                var laneHeader = Panel("Versus Loadout Lane " + lanes[lane], paper.transform,
                    new Vector2(x, 264f), new Vector2(236f, 36f), DuelVisualTheme.RaisedSurface);
                DuelVisualTheme.Frame(laneHeader);
                Label("Lane Letter", laneHeader.transform, Vector2.zero, new Vector2(210f, 32f),
                    23, DuelVisualTheme.Accent, TextAnchor.MiddleCenter).text = lanes[lane];
                for (int slot = 0; slot < VersusLoadout.SlotsPerLane; slot++)
                {
                    int selectedSlot = slot;
                    float y = 195f - slot * 93f;
                    var target = Panel("Versus Slot Target " + lanes[lane] + " " + (slot + 1),
                        paper.transform, new Vector2(x, y), new Vector2(246f, 88f), DuelVisualTheme.Accent);
                    target.gameObject.SetActive(false);
                    slotTargets[lane, slot] = target;
                    var button = ActionButton("Versus Slot " + lanes[lane] + " " + (slot + 1),
                        paper.transform, null, new Vector2(x, y), new Vector2(238f, 80f),
                        () => ClickSlot(selectedLane, selectedSlot));
                    slotButtons[lane, slot] = button;
                    Label("Slot Order", button.transform, new Vector2(-105f, 26f), new Vector2(24f, 18f),
                        15, DuelVisualTheme.Muted, TextAnchor.MiddleCenter).text = (slot + 1).ToString();
                    var icon = Panel("Slot Icon", button.transform, new Vector2(-72f, -4f),
                        new Vector2(51f, 51f), Color.white);
                    icon.preserveAspect = true;
                    slotIcons[lane, slot] = icon;
                    var name = Label("Slot Name", button.transform, new Vector2(37f, 0f),
                        new Vector2(145f, 44f), 20, DuelVisualTheme.Foreground);
                    Fit(name, 14, 20);
                    slotNames[lane, slot] = name;
                }
            }

            var detailBorder = Panel("Versus Loadout Detail Border", paper.transform,
                new Vector2(414f, 101f), new Vector2(414f, 408f), DuelVisualTheme.Border);
            var detail = Panel("Versus Loadout Detail", detailBorder.transform, Vector2.zero,
                new Vector2(408f, 402f), DuelVisualTheme.Paper);
            DuelVisualTheme.Frame(detail, true);
            detailIcon = Panel("Versus Detail Icon", detail.transform, new Vector2(-145f, 143f),
                new Vector2(66f, 66f), Color.white);
            detailIcon.preserveAspect = true;
            detailName = Label("Versus Detail Name", detail.transform, new Vector2(44f, 159f),
                new Vector2(248f, 32f), 25, DuelVisualTheme.Ink);
            Fit(detailName, 16, 25);
            detailKind = Label("Versus Detail Kind", detail.transform, new Vector2(44f, 125f),
                new Vector2(248f, 25f), 17, DuelVisualTheme.Ink);
            detailInfo = new SkillInfoView(detail.transform, art.UIFont, new Vector2(0f, -51f),
                370f, "Versus Loadout");

            Label("Versus Available Heading", paper.transform, new Vector2(-527f, -128f),
                new Vector2(320f, 34f), 23, DuelVisualTheme.Foreground).text = "배치 가능한 기술";
            hint = Label("Versus Loadout Hint", paper.transform, new Vector2(63f, -128f),
                new Vector2(780f, 30f), 17, DuelVisualTheme.Accent, TextAnchor.MiddleRight);
            Label("Versus Available Scroll Hint", paper.transform, new Vector2(576f, -128f),
                new Vector2(220f, 30f), 15, DuelVisualTheme.Muted, TextAnchor.MiddleRight).text =
                "휠로 더 보기  →";

            var collection = Rect("Versus Available Collection", paper.transform,
                new Vector2(0f, -222f), new Vector2(1380f, 138f));
            var scroll = collection.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel("Versus Available Viewport", collection, Vector2.zero,
                new Vector2(1380f, 138f), Color.clear);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            availableContent = Rect("Versus Available Content", viewport.transform,
                Vector2.zero, new Vector2(1380f, 138f));
            availableContent.anchorMin = availableContent.anchorMax = new Vector2(0f, 1f);
            availableContent.pivot = new Vector2(0f, 1f);
            scroll.viewport = viewport.rectTransform;
            scroll.content = availableContent;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.inertia = false;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            BuildAvailableCards();
            Panel("Versus Loadout Footer Rule", paper.transform, new Vector2(0f, -338f),
                new Vector2(1370f, 2f), DuelVisualTheme.Border);
            resetButton = ActionButton("Versus Loadout Reset", paper.transform, "기본 편성",
                new Vector2(-552f, -395f), new Vector2(242f, 58f), ResetClicked);
            CancelButton = ActionButton("Versus Loadout Cancel", paper.transform, "뒤로",
                new Vector2(344f, -395f), new Vector2(190f, 58f), Cancel);
            ConfirmButton = ActionButton("Versus Loadout Confirm", paper.transform, null,
                new Vector2(574f, -395f), new Vector2(244f, 58f), Confirm, true);
            confirmLabel = Label("Button Label", ConfirmButton.transform, Vector2.zero,
                new Vector2(230f, 52f), 20, DuelVisualTheme.Ink, TextAnchor.MiddleCenter);
            Hide();
        }

        public void SetLoadout(VersusLoadout value)
        {
            if (disposed) return;
            loadout = value ?? throw new ArgumentNullException(nameof(value));
            selectedId = 0;
            openingIds = null;
            Refresh();
        }

        public void Show(string heading = "대전 기술 편성", string confirmCaption = "편성 완료")
        {
            if (disposed) return;
            openingIds = loadout.ExportIds();
            selectedId = 0;
            this.heading.text = heading ?? "대전 기술 편성";
            confirmLabel.text = confirmCaption ?? "편성 완료";
            root.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (disposed) return;
            root.gameObject.SetActive(false);
            ClearSelection();
        }

        public void Confirm()
        {
            if (!IsVisible) return;
            openingIds = null;
            Hide();
            confirmed?.Invoke();
        }

        public void Cancel()
        {
            if (!IsVisible) return;
            if (openingIds != null && !Equal(openingIds, loadout.ExportIds()))
            {
                loadout.TryImportIds(openingIds);
                changed?.Invoke();
            }
            openingIds = null;
            Hide();
            cancelled?.Invoke();
        }

        public void Refresh()
        {
            if (disposed) return;
            LegacySkill selected = FindAvailable(selectedId);
            if (selected == null) selectedId = 0;
            selected = FindAvailable(selectedId);
            int laneToPlace = selected?.LaneIndex ?? -1;

            for (int lane = 0; lane < VersusLoadout.LaneCount; lane++)
                for (int slot = 0; slot < VersusLoadout.SlotsPerLane; slot++)
                {
                    LegacySkill skill = loadout.GetSkill(lane, slot);
                    slotIcons[lane, slot].sprite = art.GetSkillIcon(skill.IconId);
                    slotIcons[lane, slot].enabled = slotIcons[lane, slot].sprite != null;
                    slotNames[lane, slot].text = skill.Name;
                    slotButtons[lane, slot].GetComponent<Image>().color =
                        selectedId == skill.Id ? DuelVisualTheme.Selected : DuelVisualTheme.Card;
                    slotTargets[lane, slot].gameObject.SetActive(laneToPlace == lane &&
                        selectedId != skill.Id);
                }

            int available = 0;
            foreach (SkillCard card in availableCards)
            {
                bool equipped = loadout.IsEquipped(card.Id);
                card.Button.gameObject.SetActive(!equipped);
                if (equipped) continue;
                card.Button.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(available * 204f + 100f, -69f);
                card.Image.color = selectedId == card.Id ? DuelVisualTheme.Selected : DuelVisualTheme.Card;
                available++;
            }
            availableContent.sizeDelta = new Vector2(Mathf.Max(1380f, available * 204f), 138f);
            hint.text = selected == null ? "기술을 선택한 다음 같은 열의 칸을 누르세요."
                : (new[] { "Q", "W", "E" })[selected.LaneIndex] + " 열의 칸을 눌러 배치 · 같은 열 안에서는 순서를 바꿀 수 있습니다.";
            detailIcon.enabled = selected != null && art.GetSkillIcon(selected.IconId) != null;
            detailIcon.sprite = selected == null ? null : art.GetSkillIcon(selected.IconId);
            detailName.text = selected?.Name ?? "기술을 선택하세요";
            detailKind.text = selected == null ? string.Empty
                : (new[] { "Q", "W", "E" })[selected.LaneIndex] + "  ·  " +
                    (selected.Kind == LegacySkillKind.Defence ? "방어" : "공격");
            if (selected == null) detailInfo.SetEmptyMessage("보유 기술이나 편성된 기술을 선택하면 효과를 볼 수 있습니다.");
            else detailInfo.SetSkill(selected);
        }

        private void BuildAvailableCards()
        {
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialSkills) AddAvailableCard(skill);
            foreach (LegacySkill skill in LegacySkillDefinitions.AcquisitionSkills) AddAvailableCard(skill);
        }

        private void AddAvailableCard(LegacySkill skill)
        {
            int id = skill.Id;
            var card = new SkillCard { Id = id };
            card.Button = ActionButton("Versus Available Skill " + id, availableContent, null,
                Vector2.zero, new Vector2(194f, 116f), () => Select(id));
            var cardRect = card.Button.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0f, 1f);
            card.Image = card.Button.GetComponent<Image>();
            card.Icon = Panel("Available Icon", card.Button.transform, new Vector2(0f, 22f),
                new Vector2(52f, 52f), Color.white);
            card.Icon.sprite = art.GetSkillIcon(skill.IconId);
            card.Icon.enabled = card.Icon.sprite != null;
            card.Icon.preserveAspect = true;
            card.Name = Label("Available Name", card.Button.transform, new Vector2(0f, -22f),
                new Vector2(178f, 25f), 17, DuelVisualTheme.Foreground, TextAnchor.MiddleCenter);
            card.Name.text = skill.Name;
            Fit(card.Name, 12, 17);
            Label("Available Cost", card.Button.transform, new Vector2(-43f, -46f),
                new Vector2(88f, 20f), 14, DuelVisualTheme.Muted).text = "ACT " + skill.Cost;
            Label("Available Lane", card.Button.transform, new Vector2(53f, -46f),
                new Vector2(64f, 20f), 14, DuelVisualTheme.Accent,
                TextAnchor.MiddleRight).text = (new[] { "Q", "W", "E" })[skill.LaneIndex];
            availableCards.Add(card);
        }

        private void Select(int skillId)
        {
            if (!IsVisible) return;
            selectedId = skillId;
            Refresh();
        }

        private void ClickSlot(int lane, int slot)
        {
            if (!IsVisible) return;
            LegacySkill selected = FindAvailable(selectedId);
            if (selected == null || selected.LaneIndex != lane)
            {
                selectedId = loadout.GetSkillId(lane, slot);
                Refresh();
                return;
            }
            int before = loadout.GetSkillId(lane, slot);
            if (loadout.TryPlaceSkill(selectedId, lane, slot) && before != selectedId)
            {
                changed?.Invoke();
                Refresh();
            }
            else if (before == selectedId) Refresh();
        }

        private void ResetClicked()
        {
            if (!IsVisible) return;
            int[] before = loadout.ExportIds();
            loadout.Reset();
            if (!Equal(before, loadout.ExportIds())) changed?.Invoke();
            selectedId = 0;
            Refresh();
        }

        private static LegacySkill FindAvailable(int id)
        {
            if (id == 0) return null;
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialSkills)
                if (skill.Id == id) return skill;
            foreach (LegacySkill skill in LegacySkillDefinitions.AcquisitionSkills)
                if (skill.Id == id) return skill;
            return null;
        }

        private static bool Equal(int[] left, int[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                button.onClick.RemoveAllListeners();
            Destroy(root.gameObject);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position,
            Vector2 size, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size,
                primary ? DuelVisualTheme.Accent : DuelVisualTheme.Card);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            if (caption != null)
                Label("Button Label", button.transform, Vector2.zero, size - new Vector2(12f, 4f),
                    19, primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground,
                    TextAnchor.MiddleCenter).text = caption;
            button.onClick.AddListener(() =>
            {
                if (disposed) return;
                ClearSelection();
                action?.Invoke();
            });
            return button;
        }

        private static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size,
            int sizeInPoints, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = art.UIFont;
            label.fontSize = sizeInPoints;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static void Fit(Text label, int minimum, int maximum)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = minimum;
            label.resizeTextMaxSize = maximum;
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
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = rect.sizeDelta = Vector2.zero;
        }

        private static void Destroy(GameObject target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
