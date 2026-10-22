using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Match-only preparation overlay. The caller owns the selected loadout and match start. As the campaign
    /// loadout (2026-10-16): one gear per lane with a socket per slot (<see cref="LoadoutGear"/>: icon and ACT only, the
    /// first at the top and the rest the way the battle's gears turn), the pool's unplaced skills in a two-row rack
    /// (<see cref="LoadoutRack"/>) under a lane filter (<see cref="LoadoutFilter"/>), one hint line, and the selected
    /// skill's detail.</summary>
    public sealed class VersusLoadoutHud : IDisposable
    {
        public const int SortingOrder = 280;
        /// <summary>The gears' centres: y, and x for Q then a lane's pitch (in the preparation paper's space).</summary>
        public const float GearCentreY = 92f, FirstLaneX = -580f, LanePitch = 266f;
        /// <summary>The pool rack's window width.</summary>
        public const float RackWidth = 1380f;

        private sealed class SkillCard
        {
            public int Id, Lane;
            public Button Button;
            public RectTransform Rect;
            public Image Image, Icon;
            public Text Name;
            public SkillRolePip Role;
            public VersusLoadoutCardDrag Drag;
        }

        // The pool heading row: the heading and the filter after it (no scroll hint: one hint line, as the campaign's).
        private const float RackHeadingY = -128f;

        private readonly LegacyDuelArt art;
        private readonly RectTransform root, availableContent;
        private readonly GameObject ownedEventSystem;
        private readonly Action confirmed, cancelled, changed;
        private readonly LoadoutGear[] gears = new LoadoutGear[VersusLoadout.LaneCount];
        private readonly VersusLoadoutCardDrag[,] slotDrags = new VersusLoadoutCardDrag[VersusLoadout.LaneCount, VersusLoadout.SlotsPerLane];
        private readonly List<SkillCard> availableCards = new List<SkillCard>();
        private readonly Text heading, confirmLabel, detailName;
        private readonly Image detailIcon;
        private readonly SkillLaneBadge detailStyle;
        private readonly SkillInfoView detailInfo;
        private readonly LoadoutFilter laneFilter;
        private readonly Button resetButton;
        private readonly VersusLoadout loadout;
        private Texture2D socketTexture;
        private Sprite socketSprite;
        private int selectedId;
        private int draggedSkillId, draggedLane = -1, dragPointerId;
        private RectTransform dragGhost;
        private int[] openingIds;
        private bool disposed;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        public Button ConfirmButton { get; }
        public Button CancelButton { get; }
        public VersusLoadout Loadout => loadout;
        public bool IsDragging => draggedSkillId != 0;
        /// <summary>Which lane's skills the pool rack shows (<see cref="LoadoutRack.AllLanes"/>: every lane's). Each
        /// <see cref="Show"/> starts on 전체; selecting a gear or its socket (a click, or dragging the socket's skill) turns it
        /// to that lane, and the filter row can set it too.</summary>
        public int Filter { get; private set; } = LoadoutRack.AllLanes;

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
            // The battle dock's socket picture, this view's own copy (released with it), as the campaign loadout's.
            socketTexture = LegacyCombatHud.SocketTexture();
            socketSprite = DuelGearShimmer.CreateSprite(socketTexture);

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
            // The one hint line, the campaign loadout's (no 클릭·드래그 line, no placement hint by the pool).
            Label("Versus Loadout Subheading", paper.transform, new Vector2(0f, 334f),
                new Vector2(1190f, 34f), 20, DuelVisualTheme.Muted, TextAnchor.MiddleCenter).text = CampaignLobbyHud.LoadoutHint;

            for (int lane = 0; lane < VersusLoadout.LaneCount; lane++)
            {
                int gearLane = lane;
                float x = FirstLaneX + lane * LanePitch;
                // The school's dyed badge with its key, as the campaign loadout's lane headings, over the lane's gear.
                new SkillLaneBadge(paper.transform, art.UIFont, "Versus Loadout Heading " + SkillLaneStyle.Key(lane),
                    new Vector2(x, 264f), 236f, 36f, 22).SetLane(lane);
                var gear = new LoadoutGear(paper.transform, art.UIFont, "Versus", lane, VersusLoadout.SlotsPerLane,
                    new Vector2(x, GearCentreY), socketSprite, slot => ClickSlot(gearLane, slot), () => ClickGear(gearLane));
                for (int slot = 0; slot < VersusLoadout.SlotsPerLane; slot++)
                {
                    GameObject socket = gear.SocketButton(slot).gameObject;
                    slotDrags[lane, slot] = socket.AddComponent<VersusLoadoutCardDrag>();
                    socket.AddComponent<VersusLoadoutSlotDrop>().Configure(this, lane, slot);
                }
                gears[lane] = gear;
            }

            var detailBorder = Panel("Versus Loadout Detail Border", paper.transform,
                new Vector2(414f, 101f), new Vector2(414f, 408f), DuelVisualTheme.Border);
            var detail = Panel("Versus Loadout Detail", detailBorder.transform, Vector2.zero,
                new Vector2(408f, 402f), DuelVisualTheme.Paper);
            DuelVisualTheme.Frame(detail, true);
            // The selected skill's school badge over its icon and its name level with the icon's centre, as the campaign
            // loadout's detail (no role line under the name since 2026-10-16).
            detailStyle = new SkillLaneBadge(detail.transform, art.UIFont, "Versus Detail Heading", new Vector2(-20f, 183f),
                250f, 24f, 16);
            detailIcon = Panel("Versus Detail Icon", detail.transform, new Vector2(-145f, 135f),
                new Vector2(66f, 66f), Color.white);
            detailIcon.preserveAspect = true;
            detailName = Label("Versus Detail Name", detail.transform, new Vector2(44f, 135f),
                new Vector2(248f, 32f), 25, DuelVisualTheme.Ink);
            Fit(detailName, 16, 25);
            detailInfo = new SkillInfoView(detail.transform, art.UIFont, new Vector2(0f, -51f),
                370f, "Versus Loadout");

            Label("Versus Available Heading", paper.transform, new Vector2(-587f, RackHeadingY),
                new Vector2(200f, 34f), 23, DuelVisualTheme.Foreground).text = "배치 가능한 기술";
            // The lane filter, 전체 · Q · W · E, after the heading (every lane is open in versus).
            laneFilter = new LoadoutFilter(paper.transform, art.UIFont, "Versus", new[] { 0, 1, 2 },
                new Vector2(-480f, RackHeadingY), SelectFilter);

            var collection = Rect("Versus Available Collection", paper.transform,
                new Vector2(0f, -222f), new Vector2(RackWidth, LoadoutRack.Height));
            var scroll = collection.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel("Versus Available Viewport", collection, Vector2.zero,
                new Vector2(RackWidth, LoadoutRack.Height), Color.clear);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            availableContent = Rect("Versus Available Content", viewport.transform,
                Vector2.zero, new Vector2(RackWidth, LoadoutRack.Height));
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

        public void Show(string heading = "대전 기술 편성", string confirmCaption = "편성 완료")
        {
            if (disposed) return;
            CancelDrag();
            openingIds = loadout.ExportIds();
            selectedId = 0;
            // A fresh preparation starts with nothing selected, so on every lane's skills.
            SetFilter(LoadoutRack.AllLanes);
            this.heading.text = heading ?? "대전 기술 편성";
            confirmLabel.text = confirmCaption ?? "편성 완료";
            root.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (disposed) return;
            CancelDrag();
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
            int laneToPlace = IsDragging ? draggedLane : -1;

            for (int lane = 0; lane < VersusLoadout.LaneCount; lane++)
            {
                LoadoutGear gear = gears[lane];
                int selectedSlot = -1;
                for (int slot = 0; slot < VersusLoadout.SlotsPerLane; slot++)
                {
                    LegacySkill skill = loadout.GetSkill(lane, slot);
                    slotDrags[lane, slot].Configure(this, skill.Id, lane);
                    // A socket shows only its skill's icon and ACT (every versus slot is filled).
                    gear.Show(slot, art.GetSkillIcon(skill.IconId), skill.Cost);
                    gear.SetTarget(slot, laneToPlace == lane && draggedSkillId != skill.Id);
                    if (selectedId == skill.Id) selectedSlot = slot;
                }
                gear.SetSelected(selectedSlot);
            }

            LayoutAvailableCards();
            laneFilter.SetChoice(Filter);
            detailIcon.enabled = selected != null && art.GetSkillIcon(selected.IconId) != null;
            detailIcon.sprite = selected == null ? null : art.GetSkillIcon(selected.IconId);
            detailName.text = selected?.Name ?? "기술을 선택하세요";
            // As the campaign loadout's detail: the school's badge only while a skill is selected (every versus slot is filled).
            detailStyle.Root.gameObject.SetActive(selected != null);
            if (selected != null) detailStyle.SetLane(selected.LaneIndex);
            if (selected == null) detailInfo.SetEmptyMessage("기술을 선택하면 효과를 볼 수 있습니다.");
            else detailInfo.SetSkill(selected);
        }

        // The pool's every card is built once; the rack only hides the placed ones and the other lanes' and lays the rest
        // out, so a filter change never destroys a card under the pointer.
        private void LayoutAvailableCards()
        {
            int shown = 0;
            foreach (SkillCard card in availableCards)
            {
                bool visible = !loadout.IsEquipped(card.Id) && (Filter == LoadoutRack.AllLanes || card.Lane == Filter);
                if (card.Button.gameObject.activeSelf != visible) card.Button.gameObject.SetActive(visible);
                if (!visible) continue;
                card.Rect.anchoredPosition = LoadoutRack.CardPosition(shown++);
                card.Image.color = selectedId == card.Id ? DuelVisualTheme.Selected : DuelVisualTheme.Card;
            }
            availableContent.sizeDelta = new Vector2(LoadoutRack.ContentWidth(shown, RackWidth), LoadoutRack.Height);
            float offset = Mathf.Clamp(-availableContent.anchoredPosition.x, 0f, availableContent.sizeDelta.x - RackWidth);
            availableContent.anchoredPosition = new Vector2(-offset, 0f);
        }

        private void BuildAvailableCards()
        {
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialSkills) AddAvailableCard(skill);
            foreach (LegacySkill skill in LegacySkillDefinitions.AcquisitionSkills)
                if (!VersusLoadout.IsExcluded(skill.Id) && LegacySkillDefinitions.Find(skill)?.IsCounter != true) AddAvailableCard(skill);
            foreach (LegacySkill skill in LegacySkillDefinitions.VersusSkills)
                if (!VersusLoadout.IsExcluded(skill.Id) && LegacySkillDefinitions.Find(skill)?.IsCounter != true) AddAvailableCard(skill);
        }

        // A compact card, as the campaign loadout's owned ones: the icon at the left with its core mark, the name, and under
        // it the cost and the lane.
        private void AddAvailableCard(LegacySkill skill)
        {
            int id = skill.Id;
            var card = new SkillCard { Id = id, Lane = skill.LaneIndex };
            card.Button = ActionButton("Versus Available Skill " + id, availableContent, null,
                Vector2.zero, new Vector2(LoadoutRack.CardWidth, LoadoutRack.CardHeight), () => Select(id));
            card.Rect = card.Button.GetComponent<RectTransform>();
            card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0f, 1f);
            card.Image = card.Button.GetComponent<Image>();
            card.Icon = Panel("Available Icon", card.Button.transform, new Vector2(-68f, 0f),
                new Vector2(44f, 44f), Color.white);
            card.Icon.sprite = art.GetSkillIcon(skill.IconId);
            card.Icon.enabled = card.Icon.sprite != null;
            card.Icon.preserveAspect = true;
            card.Role = new SkillRolePip(card.Button.transform, new Vector2(-50f, 17f));
            card.Role.SetSkill(skill);
            card.Name = Label("Available Name", card.Button.transform, new Vector2(24f, 11f),
                new Vector2(128f, 24f), 17, DuelVisualTheme.Foreground);
            card.Name.text = skill.Name;
            Fit(card.Name, 13, 17);
            Label("Available Cost", card.Button.transform, new Vector2(4f, -14f),
                new Vector2(88f, 20f), 14, DuelVisualTheme.Muted).text = "ACT " + skill.Cost;
            Label("Available Lane", card.Button.transform, new Vector2(68f, -14f),
                new Vector2(36f, 20f), 14, SkillLaneStyle.Edge(skill.LaneIndex),
                TextAnchor.MiddleRight).text = SkillLaneStyle.Key(skill.LaneIndex);
            card.Drag = card.Button.gameObject.AddComponent<VersusLoadoutCardDrag>();
            card.Drag.Configure(this, skill.Id, skill.LaneIndex);
            availableCards.Add(card);
        }

        // The rack follows the selection: a skill outside the lane shown brings its lane's skills up.
        private void Select(int skillId)
        {
            if (!IsVisible) return;
            selectedId = skillId;
            LegacySkill skill = FindAvailable(skillId);
            if (skill != null && Filter != LoadoutRack.AllLanes && Filter != skill.LaneIndex) SetFilter(skill.LaneIndex);
            Refresh();
        }

        // A socket selects its skill and shows that lane's skills.
        private void ClickSlot(int lane, int slot)
        {
            if (!IsVisible) return;
            selectedId = loadout.GetSkillId(lane, slot);
            SetFilter(lane);
            Refresh();
        }

        // The wheel off its sockets: that lane's skills, the selection as it was.
        private void ClickGear(int lane)
        {
            if (!IsVisible) return;
            SetFilter(lane);
            Refresh();
        }

        private void SelectFilter(int filter)
        {
            if (!IsVisible) return;
            SetFilter(filter);
            Refresh();
        }

        private void SetFilter(int filter)
        {
            if (Filter == filter) return;
            Filter = filter;
            // Another lane's skills start from the rack's left.
            availableContent.anchoredPosition = Vector2.zero;
        }

        internal bool BeginDrag(int skillId, int lane, PointerEventData pointer)
        {
            LegacySkill skill = FindAvailable(skillId);
            if (!IsVisible || !root.gameObject.activeInHierarchy || pointer == null ||
                pointer.button != PointerEventData.InputButton.Left || skill == null || skill.LaneIndex != lane)
                return false;
            CancelDrag();
            // A skill taken from a socket (always an equipped one; a rack card's never is) selects that socket, so the rack
            // follows to its lane as a click there would. A card taken from the rack leaves the filter: the rack never
            // shifts under it.
            if (loadout.IsEquipped(skillId)) SetFilter(lane);
            selectedId = draggedSkillId = skillId;
            draggedLane = lane;
            dragPointerId = pointer.pointerId;
            Refresh();
            dragGhost = Rect("Versus Loadout Drag Ghost", root, Vector2.zero, new Vector2(76f, 76f));
            var image = dragGhost.gameObject.AddComponent<Image>();
            image.sprite = art.GetSkillIcon(skill.IconId);
            image.preserveAspect = true;
            image.raycastTarget = false;
            var group = dragGhost.gameObject.AddComponent<CanvasGroup>();
            group.alpha = .88f;
            group.interactable = group.blocksRaycasts = false;
            UpdateDrag(pointer);
            return true;
        }

        internal void UpdateDrag(PointerEventData pointer)
        {
            if (disposed || dragGhost == null || pointer == null || pointer.pointerId != dragPointerId) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer.position,
                pointer.pressEventCamera, out Vector2 point)) dragGhost.anchoredPosition = point;
        }

        internal void Drop(int lane, int slot, PointerEventData pointer)
        {
            if (!IsVisible || !IsDragging || pointer == null || pointer.pointerDrag == null ||
                pointer.pointerId != dragPointerId || pointer.button != PointerEventData.InputButton.Left) return;
            var source = pointer.pointerDrag.GetComponent<VersusLoadoutCardDrag>();
            if (source == null || source.Owner != this) return;
            int id = draggedSkillId;
            bool compatible = lane == draggedLane && slot >= 0 && slot < VersusLoadout.SlotsPerLane;
            CancelDrag();
            if (!compatible) return;
            int[] before = loadout.ExportIds();
            if (!loadout.TryPlaceSkill(id, lane, slot)) return;
            selectedId = id;
            Refresh();
            if (!Equal(before, loadout.ExportIds())) changed?.Invoke();
        }

        internal void CancelDrag()
        {
            bool wasDragging = IsDragging;
            draggedSkillId = 0;
            draggedLane = -1;
            if (dragGhost != null)
            {
                dragGhost.gameObject.SetActive(false);
                Destroy(dragGhost.gameObject);
            }
            dragGhost = null;
            if (wasDragging && !disposed) Refresh();
        }

        private void ResetClicked()
        {
            if (!IsVisible) return;
            CancelDrag();
            int[] before = loadout.ExportIds();
            loadout.Reset();
            if (!Equal(before, loadout.ExportIds())) changed?.Invoke();
            selectedId = 0;
            Refresh();
        }

        private static LegacySkill FindAvailable(int id)
        {
            if (id == 0 || VersusLoadout.IsExcluded(id) || LegacySkillDefinitions.Find(id)?.IsCounter == true) return null;
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialSkills)
                if (skill.Id == id) return skill;
            foreach (LegacySkill skill in LegacySkillDefinitions.AcquisitionSkills)
                if (skill.Id == id) return skill;
            foreach (LegacySkill skill in LegacySkillDefinitions.VersusSkills)
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
            CancelDrag();
            root.gameObject.SetActive(false);
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                button.onClick.RemoveAllListeners();
            Destroy(root.gameObject);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
            DuelGearShimmer.Release(socketSprite);
            DuelGearShimmer.Release(socketTexture);
            socketSprite = null;
            socketTexture = null;
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

    /// <summary>Each card and each gear socket owns its pointer drag; drops are validated by its preparation view.</summary>
    public sealed class VersusLoadoutCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private int skillId, lane;
        private bool dragging;
        internal VersusLoadoutHud Owner { get; private set; }
        internal void Configure(VersusLoadoutHud owner, int id, int laneIndex)
        { Owner = owner; skillId = id; lane = laneIndex; }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            eventData.eligibleForClick = false;
            dragging = Owner != null && Owner.BeginDrag(skillId, lane, eventData);
        }
        public void OnDrag(PointerEventData eventData) { if (dragging) Owner?.UpdateDrag(eventData); }
        public void OnEndDrag(PointerEventData eventData) { if (dragging) Owner?.CancelDrag(); dragging = false; }
        private void OnDisable() { if (dragging) Owner?.CancelDrag(); dragging = false; }
    }

    /// <summary>A gear socket takes a dragged skill of its lane (<see cref="VersusLoadoutHud"/>).</summary>
    public sealed class VersusLoadoutSlotDrop : MonoBehaviour, IDropHandler
    {
        private VersusLoadoutHud owner;
        private int lane, slot;
        internal void Configure(VersusLoadoutHud view, int laneIndex, int slotIndex)
        { owner = view; lane = laneIndex; slot = slotIndex; }
        public void OnDrop(PointerEventData eventData) => owner?.Drop(lane, slot, eventData);
    }
}
