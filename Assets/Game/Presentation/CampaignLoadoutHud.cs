using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Three draft slots for each open lane, unplaced owned skills, and one selected skill explanation.
    /// CampaignRun owns every loadout mutation. A lane the story has not opened is not drawn at all.</summary>
    public sealed class CampaignLoadoutHud : IDisposable
    {
        public sealed class ViewState
        {
            public int ActiveLane { get; internal set; }
            public int SelectedSkillId { get; internal set; }
            public int SelectedSlot { get; internal set; } = -1;
            public float OwnedScrollOffset { get; internal set; }
            public void Reset() { ActiveLane = SelectedSkillId = 0; SelectedSlot = -1; OwnedScrollOffset = 0f; }
        }

        private sealed class Card
        {
            public Button Button;
            public Image Background, Icon, TargetBorder;
            public Text Name, Cost, Marker;
            public CampaignLoadoutCardDrag Drag;
            public int SkillId;
        }

        private readonly RectTransform root, ownedItems;
        private readonly RectTransform detailFrame, detailSurface;
        private readonly LegacyDuelArt art;
        private readonly CampaignRun run;
        private readonly Action<int, int, int> placeSkill;
        private readonly Action<int> removeSkill;
        private readonly Action save, reset;
        // Closed lanes have no heading or slots: their entries stay null.
        private readonly Card[,] slots = new Card[3, 3];
        private readonly SkillLaneBadge[] laneStyles = new SkillLaneBadge[3];
        /// <summary>The open lanes in Q, W, E order.</summary>
        private readonly int[] openLanes;
        private readonly List<Card> ownedCards = new List<Card>();
        private readonly Text detailName, detailRole, status, placementHint, ownedEmpty;
        private readonly SkillLaneBadge detailStyle;
        private readonly SkillInfoView detailInfo;
        private readonly Image detailIcon;
        private readonly Button removeButton, saveButton, cancelButton;
        private RectTransform dragGhost, dragLayer;
        private int draggedSkillId, draggedLane = -1;
        private bool disposed;
        private static Color CardColor => DuelVisualTheme.Card;
        private static Color Selected => DuelVisualTheme.Selected;
        private static Color Border => DuelVisualTheme.Border;
        private static Color Accent => DuelVisualTheme.Accent;
        private static Color Foreground => DuelVisualTheme.Foreground;
        private static Color Muted => DuelVisualTheme.Muted;
        private static readonly string[] LaneNames = { "Q", "W", "E" };
        // The lane block's centre and column pitch: open lanes are packed around the centre, so three lanes sit at
        // -472 / -210 / 52, two at -341 / -79 and one at -210.
        private const float LaneBlockCentre = -210f, LanePitch = 262f;

        public GameObject Root => root.gameObject;
        public ViewState State { get; }
        public bool IsDragging => draggedSkillId != 0;

        public CampaignLoadoutHud(RectTransform parent, LegacyDuelArt art, CampaignRun run,
            Action<int, int, int> placeSkill, Action<int> removeSkill, Action save, Action reset, ViewState state = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.placeSkill = placeSkill;
            this.removeSkill = removeSkill;
            this.save = save;
            this.reset = reset;
            State = state ?? new ViewState();
            var open = new List<int>();
            for (int lane = 0; lane < 3; lane++) if (run.IsLaneOpen(lane)) open.Add(lane);
            openLanes = open.ToArray();
            // The remembered selection may belong to a lane this run has closed.
            State.ActiveLane = Mathf.Clamp(State.ActiveLane, 0, 2);
            if (!run.IsLaneOpen(State.ActiveLane)) State.ActiveLane = openLanes[0];
            root = Rect("Loadout Content", parent, Vector2.zero, Vector2.zero);
            Stretch(root);

            for (int index = 0; index < openLanes.Length; index++)
            {
                int lane = openLanes[index];
                float x = LaneBlockCentre + (index - (openLanes.Length - 1) * .5f) * LanePitch;
                laneStyles[lane] = new SkillLaneBadge(root, art.UIFont, "Loadout Heading " + LaneNames[lane],
                    new Vector2(x, 252f), 242f, 32f, 22);
                laneStyles[lane].SetLane(lane);
                for (int slot = 0; slot < 3; slot++)
                {
                    int selectedLane = lane, selectedSlot = slot;
                    var card = new Card();
                    card.TargetBorder = Panel("Loadout Slot Target " + LaneNames[lane] + " " + (slot + 1), root,
                        new Vector2(x, 189f - slot * 94f), new Vector2(250f, 92f),
                        Color.Lerp(SkillLaneStyle.Paper(lane), Accent, .45f));
                    card.TargetBorder.gameObject.SetActive(false);
                    card.Button = ActionButton("Loadout Slot " + LaneNames[lane] + " " + (slot + 1), root, null,
                        new Vector2(x, 189f - slot * 94f), new Vector2(242f, 84f),
                        () => ClickSlot(selectedLane, selectedSlot));
                    card.Background = card.Button.GetComponent<Image>();
                    Label("Slot Order", card.Button.transform, new Vector2(-105f, 27f), new Vector2(24f, 22f),
                        16, Muted, TextAnchor.MiddleCenter).text = (slot + 1).ToString();
                    card.Icon = SkillIcon("Slot Icon", card.Button.transform, new Vector2(-72f, -4f), 54f);
                    card.Name = Label("Slot Name", card.Button.transform, new Vector2(38f, 15f), new Vector2(144f, 30f), 20);
                    Fit(card.Name, 14, 20);
                    card.Cost = Label("Slot ACT", card.Button.transform, new Vector2(38f, -18f), new Vector2(144f, 24f), 16, Muted);
                    card.Drag = card.Button.gameObject.AddComponent<CampaignLoadoutCardDrag>();
                    card.Drag.Configure(this, 0, lane, slot);
                    card.Button.gameObject.AddComponent<CampaignLoadoutSlotDrop>().Configure(this, lane, slot);
                    slots[lane, slot] = card;
                }
            }
            var detailBorder = Panel("Loadout Selected Detail", root, new Vector2(423f, 86f), new Vector2(384f, 396f), Border);
            var detail = Panel("Detail Surface", detailBorder.transform, Vector2.zero, new Vector2(380f, 392f), DuelVisualTheme.Paper);
            detailFrame = detailBorder.rectTransform;
            detailSurface = detail.rectTransform;
            DuelVisualTheme.Frame(detail, true);
            detailStyle = new SkillLaneBadge(detail.transform, art.UIFont, "Detail Heading", Vector2.zero,
                250f, 24f, 16);
            detailStyle.Clear("선택한 기술");
            detailIcon = SkillIcon("Loadout Detail Icon", detail.transform, new Vector2(-132f, 127f), 66f);
            detailName = Label("Loadout Detail Name", detail.transform, new Vector2(45f, 146f), new Vector2(246f, 32f), 23);
            Fit(detailName, 16, 23);
            detailRole = Label("Loadout Detail Role", detail.transform, new Vector2(45f, 116f), new Vector2(246f, 26f), 16, Accent);
            Fit(detailRole, 14, 16);
            detailInfo = new SkillInfoView(detail.transform, art.UIFont, new Vector2(0f, -17f), 334f,
                "Loadout", expandedExperience: true);
            detailName.color = detailRole.color = DuelVisualTheme.Ink;
            // Keep the removal action outside the paper detail, on the collection heading row.
            removeButton = ActionButton("Loadout Remove Selected", root, "편성에서 빼기",
                new Vector2(423f, -124f), new Vector2(310f, 36f), RemoveSelected);

            Label("Loadout Owned Heading", root, new Vector2(-479f, -124f), new Vector2(220f, 30f), 21).text = "보유 기술";
            placementHint = Label("Loadout Owned Hint", root, new Vector2(-140f, -124f),
                new Vector2(400f, 28f), 16, Accent, TextAnchor.MiddleRight);
            var collection = Rect("Loadout Collection", root, new Vector2(0f, -222f), new Vector2(1200f, 126f));
            var scroll = collection.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel("Loadout Collection Viewport", collection, Vector2.zero, new Vector2(1200f, 126f), Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.raycastTarget = true;
            ownedItems = Rect("Loadout Owned Items", viewport.transform, Vector2.zero, new Vector2(1200f, 126f));
            ownedItems.anchorMin = ownedItems.anchorMax = new Vector2(0f, 1f);
            ownedItems.pivot = new Vector2(0f, 1f);
            ownedEmpty = Label("Loadout Owned Empty", viewport.transform, Vector2.zero,
                new Vector2(1160f, 92f), 18, Muted, TextAnchor.MiddleCenter);
            ownedEmpty.text = "남은 기술 없음";
            scroll.viewport = viewport.rectTransform;
            scroll.content = ownedItems;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.inertia = false;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            Panel("Loadout Footer Rule", root, new Vector2(0f, -296f), new Vector2(1190f, 2f), Border);
            status = Label("Loadout Status", root, new Vector2(-250f, -349f), new Vector2(680f, 30f), 16, Muted);
            cancelButton = ActionButton("Loadout Cancel", root, "변경 취소", new Vector2(218f, -349f), new Vector2(196f, 48f),
                ResetClicked);
            saveButton = ActionButton("Loadout Save", root, "편성 저장", new Vector2(459f, -349f), new Vector2(238f, 48f),
                SaveClicked, true);
            Refresh();
        }

        public void Refresh()
        {
            if (disposed) return;
            ReconcileSelection();
            int placementLane = PlacementLane();
            foreach (int lane in openLanes)
            {
                laneStyles[lane].SetSelected(State.SelectedSkillId != 0 && State.ActiveLane == lane);
                for (int slot = 0; slot < 3; slot++)
                {
                    CampaignOwnedSkill owned = run.GetLoadoutSlot(lane, slot);
                    Card card = slots[lane, slot];
                    bool target = lane == placementLane;
                    card.SkillId = owned?.SkillId ?? 0;
                    card.Drag.Configure(this, card.SkillId, lane, slot);
                    card.Icon.enabled = owned != null;
                    card.Icon.sprite = owned == null ? null : art.GetSkillIcon(owned.Skill.IconId);
                    card.Name.text = owned?.Skill.Name ?? "빈 슬롯";
                    card.Name.color = owned == null ? Muted : Foreground;
                    card.Cost.text = owned == null ? string.Empty : "ACT " + owned.Skill.Cost;
                    card.TargetBorder.gameObject.SetActive(target);
                    card.Background.color = State.ActiveLane == lane && State.SelectedSlot == slot ? Selected
                        : target ? Color.Lerp(CardColor, SkillLaneStyle.Paper(lane), .14f) : CardColor;
                }
            }
            if (!OwnedCardsMatchDraft()) BuildOwnedCards();
            foreach (Card card in ownedCards)
            {
                CampaignOwnedSkill owned = FindOwned(card.SkillId);
                card.Name.text = owned.Skill.Name;
                card.Background.color = State.SelectedSkillId == card.SkillId
                    ? Color.Lerp(Selected, Accent, .18f) : CardColor;
            }
            ownedEmpty.gameObject.SetActive(ownedCards.Count == 0);
            placementHint.text = placementLane >= 0 && draggedSkillId == 0
                ? LaneNames[placementLane] + " 칸을 눌러 배치" : string.Empty;
            RefreshDetail();
            bool hasEmptySlot = false;
            foreach (int lane in openLanes)
                if (run.GetLoadoutCount(lane) < 3) hasEmptySlot = true;
            status.text = hasEmptySlot ? "빈 칸을 채워야 저장할 수 있습니다"
                : run.HasLoadoutChanges ? "저장 전 변경" : string.Empty;
            saveButton.interactable = run.CanSaveLoadout && run.HasLoadoutChanges;
            saveButton.GetComponentInChildren<Text>().color = saveButton.interactable ? DuelVisualTheme.Ink : Foreground;
            cancelButton.interactable = run.HasLoadoutChanges;
        }

        private int PlacementLane()
        {
            if (draggedSkillId != 0) return draggedLane;
            if (State.SelectedSlot >= 0 || run.IsSkillInLoadout(State.SelectedSkillId)) return -1;
            CampaignOwnedSkill selected = FindOwned(State.SelectedSkillId);
            return selected != null && run.IsLaneOpen(selected.Skill.LaneIndex) ? selected.Skill.LaneIndex : -1;
        }

        private bool OwnedCardsMatchDraft()
        {
            int index = 0;
            foreach (CampaignOwnedSkill owned in run.OwnedSkills)
            {
                if (!run.IsLaneOpen(owned.Skill.LaneIndex) || run.IsSkillInLoadout(owned.SkillId)) continue;
                if (index >= ownedCards.Count || ownedCards[index].SkillId != owned.SkillId) return false;
                index++;
            }
            return index == ownedCards.Count;
        }

        private void BuildOwnedCards()
        {
            float scrollOffset = ownedCards.Count == 0 ? State.OwnedScrollOffset
                : Mathf.Max(0f, -ownedItems.anchoredPosition.x);
            foreach (Card card in ownedCards) { card.Button.gameObject.SetActive(false); Destroy(card.Button.gameObject); }
            ownedCards.Clear();
            foreach (CampaignOwnedSkill owned in run.OwnedSkills)
            {
                if (!run.IsLaneOpen(owned.Skill.LaneIndex) || run.IsSkillInLoadout(owned.SkillId)) continue;
                int id = owned.SkillId;
                var card = new Card { SkillId = id };
                card.Button = ActionButton("Loadout Owned Skill " + id, ownedItems, null,
                    new Vector2(100f + ownedCards.Count * 200f, -60f), new Vector2(190f, 108f), () => SelectOwned(id));
                var rect = card.Button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                card.Background = card.Button.GetComponent<Image>();
                card.Icon = SkillIcon("Owned Icon", card.Button.transform, new Vector2(0f, 20f), 48f);
                card.Icon.sprite = art.GetSkillIcon(owned.Skill.IconId);
                card.Name = Label("Owned Name", card.Button.transform, new Vector2(0f, -18f), new Vector2(174f, 24f), 17,
                    null, TextAnchor.MiddleCenter);
                Fit(card.Name, 13, 17);
                card.Cost = Label("Owned ACT", card.Button.transform, new Vector2(-42f, -40f), new Vector2(90f, 20f), 15, Muted);
                card.Cost.text = "ACT " + owned.Skill.Cost;
                card.Marker = Label("Owned Lane", card.Button.transform, new Vector2(54f, -40f),
                    new Vector2(60f, 20f), 15, SkillLaneStyle.Paper(owned.Skill.LaneIndex), TextAnchor.MiddleRight);
                card.Marker.text = LaneNames[owned.Skill.LaneIndex];
                card.Drag = card.Button.gameObject.AddComponent<CampaignLoadoutCardDrag>();
                card.Drag.Configure(this, id, owned.Skill.LaneIndex, -1);
                ownedCards.Add(card);
            }
            ownedItems.sizeDelta = new Vector2(Mathf.Max(1200f, ownedCards.Count * 200f), 126f);
            State.OwnedScrollOffset = Mathf.Min(scrollOffset, ownedItems.sizeDelta.x - 1200f);
            ownedItems.anchoredPosition = new Vector2(-State.OwnedScrollOffset, 0f);
        }

        private void ReconcileSelection()
        {
            CampaignOwnedSkill selected = FindOwned(State.SelectedSkillId);
            if (selected == null)
            {
                State.SelectedSlot = Mathf.Clamp(State.SelectedSlot, -1, 2);
                State.SelectedSkillId = State.SelectedSlot >= 0 && run.IsLaneOpen(State.ActiveLane)
                    ? run.GetLoadoutSlot(State.ActiveLane, State.SelectedSlot)?.SkillId ?? 0 : 0;
                return;
            }
            if (!run.IsLaneOpen(selected.Skill.LaneIndex))
            {
                State.ActiveLane = openLanes[0];
                State.SelectedSkillId = 0;
                State.SelectedSlot = -1;
                return;
            }
            State.ActiveLane = selected.Skill.LaneIndex;
            if (State.SelectedSlot < 0) return;
            if (run.GetLoadoutSlot(State.ActiveLane, State.SelectedSlot)?.SkillId == State.SelectedSkillId) return;
            State.SelectedSlot = -1;
            for (int slot = 0; slot < 3; slot++)
                if (run.GetLoadoutSlot(State.ActiveLane, slot)?.SkillId == State.SelectedSkillId) State.SelectedSlot = slot;
        }

        private void RefreshDetail()
        {
            CampaignOwnedSkill owned = FindOwned(State.SelectedSkillId);
            detailIcon.enabled = owned != null;
            detailIcon.sprite = owned == null ? null : art.GetSkillIcon(owned.Skill.IconId);
            detailStyle.Root.gameObject.SetActive(owned != null);
            removeButton.gameObject.SetActive(owned != null && run.IsSkillInLoadout(owned.SkillId) && run.IsLaneOpen(owned.Skill.LaneIndex));
            if (owned == null)
            {
                detailName.text = State.SelectedSlot >= 0 ? "빈 슬롯" : "기술을 선택하세요";
                detailRole.text = State.SelectedSlot >= 0 ? LaneNames[State.ActiveLane] : string.Empty;
                detailInfo.Clear();
                LayoutDetail();
                return;
            }
            LegacySkill skill = owned.Skill;
            detailStyle.SetLane(skill.LaneIndex, true);
            detailName.text = skill.Name;
            detailRole.text = CampaignSkillText.Purpose(skill);
            detailInfo.SetSkill(skill, owned: owned);
            LayoutDetail();
        }

        private void LayoutDetail()
        {
            // The loadout card uses the former action row for its larger experience footer.
            float height = 104f + detailInfo.Height;
            detailFrame.sizeDelta = new Vector2(384f, height + 4f);
            detailFrame.anchoredPosition = new Vector2(423f, 284f - (height + 4f) / 2f);
            detailSurface.sizeDelta = new Vector2(380f, height);
            float top = height / 2f;
            detailStyle.Root.anchoredPosition = new Vector2(-18f, top - 18f);
            detailIcon.rectTransform.anchoredPosition = new Vector2(-105f, top - 62f);
            detailIcon.rectTransform.sizeDelta = Vector2.one * 64f;
            detailName.rectTransform.anchoredPosition = new Vector2(20f, top - 49f);
            detailName.rectTransform.sizeDelta = new Vector2(162f, 32f);
            detailRole.rectTransform.anchoredPosition = new Vector2(20f, top - 78f);
            detailRole.rectTransform.sizeDelta = new Vector2(162f, 26f);
            detailInfo.PlaceTop(top - 104f);
        }

        private void SelectOwned(int skillId)
        {
            CampaignOwnedSkill owned = FindOwned(skillId);
            if (disposed || owned == null || !run.IsLaneOpen(owned.Skill.LaneIndex)) return;
            State.ActiveLane = owned.Skill.LaneIndex;
            State.SelectedSkillId = skillId;
            State.SelectedSlot = -1;
            Refresh();
        }

        private void ClickSlot(int lane, int slot)
        {
            if (disposed) return;
            CampaignOwnedSkill selected = FindOwned(State.SelectedSkillId);
            if (selected != null && State.SelectedSlot < 0 && !run.IsSkillInLoadout(selected.SkillId) &&
                selected.Skill.LaneIndex == lane)
            {
                PlaceSelected(selected.SkillId, lane, slot);
                return;
            }
            State.ActiveLane = lane;
            State.SelectedSlot = slot;
            State.SelectedSkillId = run.GetLoadoutSlot(lane, slot)?.SkillId ?? 0;
            Refresh();
        }

        private void PlaceSelected(int skillId, int lane, int slot)
        {
            State.ActiveLane = lane;
            State.SelectedSkillId = skillId;
            State.SelectedSlot = slot;
            placeSkill?.Invoke(skillId, lane, slot);
            // A controller callback may dispose this view while rebuilding the lobby.
            if (!disposed) Refresh();
        }

        private void RemoveSelected()
        {
            int id = State.SelectedSkillId;
            CampaignOwnedSkill selected = FindOwned(id);
            if (disposed || id == 0 || !run.IsSkillInLoadout(id) || selected == null || !run.IsLaneOpen(selected.Skill.LaneIndex)) return;
            State.SelectedSlot = -1;
            removeSkill?.Invoke(id);
            if (!disposed) Refresh();
        }

        private void SaveClicked()
        {
            save?.Invoke();
            if (!disposed) Refresh();
        }

        private void ResetClicked()
        {
            reset?.Invoke();
            if (!disposed) Refresh();
        }

        internal bool BeginDrag(int skillId, int lane, int slot, PointerEventData pointer)
        {
            if (disposed || !root.gameObject.activeInHierarchy || FindOwned(skillId) == null || lane < 0 || lane > 2 ||
                !run.IsLaneOpen(lane)) return false;
            CancelDrag();
            State.ActiveLane = lane;
            State.SelectedSkillId = skillId;
            State.SelectedSlot = slot;
            draggedSkillId = skillId;
            draggedLane = lane;
            Refresh();
            Canvas canvas = root.GetComponentInParent<Canvas>();
            dragLayer = canvas == null ? root : canvas.rootCanvas.transform as RectTransform;
            dragGhost = Rect("Loadout Drag Ghost", dragLayer, Vector2.zero, new Vector2(76f, 76f));
            var ghostImage = dragGhost.gameObject.AddComponent<Image>();
            ghostImage.sprite = art.GetSkillIcon(FindOwned(skillId).Skill.IconId);
            ghostImage.preserveAspect = true;
            ghostImage.raycastTarget = false;
            var group = dragGhost.gameObject.AddComponent<CanvasGroup>();
            group.alpha = .88f;
            group.interactable = group.blocksRaycasts = false;
            UpdateDrag(pointer);
            return true;
        }

        internal void UpdateDrag(PointerEventData pointer)
        {
            if (disposed || dragGhost == null || dragLayer == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, pointer.position, pointer.pressEventCamera, out Vector2 point))
                dragGhost.anchoredPosition = point;
        }

        internal void Drop(int lane, int slot, PointerEventData pointer)
        {
            if (disposed || !IsDragging || pointer.pointerDrag == null) return;
            var source = pointer.pointerDrag.GetComponent<CampaignLoadoutCardDrag>();
            if (source == null || source.Owner != this || lane != draggedLane) return;
            int id = draggedSkillId;
            CancelDrag();
            PlaceSelected(id, lane, slot);
        }

        internal void CancelDrag()
        {
            bool wasDragging = draggedSkillId != 0;
            draggedSkillId = 0;
            draggedLane = -1;
            if (dragGhost != null) { dragGhost.gameObject.SetActive(false); Destroy(dragGhost.gameObject); }
            dragGhost = dragLayer = null;
            if (wasDragging && !disposed) Refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            State.OwnedScrollOffset = Mathf.Max(0f, -ownedItems.anchoredPosition.x);
            disposed = true;
            CancelDrag();
            root.gameObject.SetActive(false);
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            Destroy(root.gameObject);
        }

        private CampaignOwnedSkill FindOwned(int id)
        {
            if (id == 0) return null;
            foreach (CampaignOwnedSkill owned in run.OwnedSkills) if (owned.SkillId == id) return owned;
            return null;
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
                Label("Button Label", image.transform, Vector2.zero, size - new Vector2(12f, 4f), 18,
                    primary ? DuelVisualTheme.Ink : Foreground, TextAnchor.MiddleCenter).text = caption;
            button.onClick.AddListener(() =>
            {
                if (disposed) return;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                action?.Invoke();
            });
            return button;
        }

        private Image SkillIcon(string name, Transform parent, Vector2 position, float size)
        {
            var image = Panel(name, parent, position, Vector2.one * size, Color.white);
            image.preserveAspect = true;
            return image;
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

    /// <summary>Pointer-local bridge; no shared drag manager or per-frame polling.</summary>
    public sealed class CampaignLoadoutCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private int skillId, lane, slot;
        private bool dragging;
        internal CampaignLoadoutHud Owner { get; private set; }
        internal void Configure(CampaignLoadoutHud owner, int id, int laneIndex, int slotIndex)
        { Owner = owner; skillId = id; lane = laneIndex; slot = slotIndex; }
        public void OnBeginDrag(PointerEventData eventData)
        {
            eventData.eligibleForClick = false;
            dragging = Owner != null && skillId != 0 && Owner.BeginDrag(skillId, lane, slot, eventData);
        }
        public void OnDrag(PointerEventData eventData) { if (dragging) Owner?.UpdateDrag(eventData); }
        public void OnEndDrag(PointerEventData eventData) { if (dragging) Owner?.CancelDrag(); dragging = false; }
        private void OnDisable() { if (dragging) Owner?.CancelDrag(); dragging = false; }
    }

    public sealed class CampaignLoadoutSlotDrop : MonoBehaviour, IDropHandler
    {
        private CampaignLoadoutHud owner;
        private int lane, slot;
        internal void Configure(CampaignLoadoutHud view, int laneIndex, int slotIndex)
        { owner = view; lane = laneIndex; slot = slotIndex; }
        public void OnDrop(PointerEventData eventData) => owner?.Drop(lane, slot, eventData);
    }
}
