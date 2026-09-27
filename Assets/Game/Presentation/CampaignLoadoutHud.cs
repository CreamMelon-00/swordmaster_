using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Nine draft slots and one selected skill explanation. CampaignRun owns every loadout mutation.</summary>
    public sealed class CampaignLoadoutHud : IDisposable
    {
        public sealed class ViewState
        {
            public int ActiveLane { get; internal set; }
            public int SelectedSkillId { get; internal set; }
            public int SelectedSlot { get; internal set; } = -1;
            public void Reset() { ActiveLane = SelectedSkillId = 0; SelectedSlot = -1; }
        }

        private sealed class Card
        {
            public Button Button;
            public Image Background, Icon;
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
        private readonly Card[,] slots = new Card[3, 3];
        private readonly SkillLaneBadge[] laneStyles = new SkillLaneBadge[3];
        private readonly Button[] laneFilters = new Button[3];
        private readonly List<Card> ownedCards = new List<Card>();
        private readonly Text detailName, detailRole, detailValues, detailEffect, damageHint, status, counts;
        private readonly SkillLaneBadge detailStyle;
        private readonly SkillInfoView detailInfo;
        private readonly Image detailIcon;
        private readonly Button removeButton, saveButton, cancelButton;
        private RectTransform dragGhost, dragLayer;
        private int draggedSkillId, draggedLane = -1, builtLane = -1, builtOwnedCount = -1;
        private bool disposed;
        private static Color CardColor => DuelVisualTheme.Card;
        private static Color Selected => DuelVisualTheme.Selected;
        private static Color Border => DuelVisualTheme.Border;
        private static Color Accent => DuelVisualTheme.Accent;
        private static Color Foreground => DuelVisualTheme.Foreground;
        private static Color Muted => DuelVisualTheme.Muted;
        private static readonly string[] LaneNames = { "Q", "W", "E" };

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
            State.ActiveLane = Mathf.Clamp(State.ActiveLane, 0, 2);
            root = Rect("Loadout Content", parent, Vector2.zero, Vector2.zero);
            Stretch(root);

            for (int lane = 0; lane < 3; lane++)
            {
                float x = -472f + lane * 262f;
                laneStyles[lane] = new SkillLaneBadge(root, art.UIFont, "Loadout Heading " + LaneNames[lane],
                    new Vector2(x, 252f), 242f, 32f, 22);
                laneStyles[lane].SetLane(lane);
                for (int slot = 0; slot < 3; slot++)
                {
                    int selectedLane = lane, selectedSlot = slot;
                    var card = new Card();
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
            detailInfo = new SkillInfoView(detail.transform, art.UIFont, new Vector2(0f, -17f), 334f, "Loadout");
            detailValues = detailInfo.PowerText;
            detailEffect = detailInfo.EffectText;
            damageHint = detailInfo.DamageText;
            detailName.color = detailRole.color = DuelVisualTheme.Ink;
            removeButton = ActionButton("Loadout Remove Selected", detail.transform, "편성에서 빼기",
                new Vector2(0f, -172f), new Vector2(190f, 34f), RemoveSelected);

            Label("Loadout Owned Heading", root, new Vector2(-479f, -124f), new Vector2(220f, 30f), 21).text = "보유 기술";
            for (int lane = 0; lane < 3; lane++)
            {
                int selectedLane = lane;
                laneFilters[lane] = ActionButton("Loadout Lane " + LaneNames[lane], root,
                    LaneNames[lane] + " " + SkillLaneStyle.Name(lane),
                    new Vector2(-185f + lane * 104f, -124f), new Vector2(90f, 32f), () => SelectLane(selectedLane));
            }
            Label("Loadout Owned Hint", root, new Vector2(394f, -134f), new Vector2(396f, 24f), 15, Muted,
                TextAnchor.MiddleRight).text = "선택한 검술의 기술만 표시합니다";
            var collection = Rect("Loadout Collection", root, new Vector2(0f, -222f), new Vector2(1200f, 126f));
            var scroll = collection.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel("Loadout Collection Viewport", collection, Vector2.zero, new Vector2(1200f, 126f), Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.raycastTarget = true;
            ownedItems = Rect("Loadout Owned Items", viewport.transform, Vector2.zero, new Vector2(1200f, 126f));
            ownedItems.anchorMin = ownedItems.anchorMax = new Vector2(0f, 1f);
            ownedItems.pivot = new Vector2(0f, 1f);
            scroll.viewport = viewport.rectTransform;
            scroll.content = ownedItems;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.inertia = false;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            Panel("Loadout Footer Rule", root, new Vector2(0f, -296f), new Vector2(1190f, 2f), Border);
            counts = Label("Loadout Counts", root, new Vector2(-250f, -319f), new Vector2(680f, 28f), 19);
            status = Label("Loadout Status", root, new Vector2(-250f, -350f), new Vector2(680f, 28f), 16, Muted);
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
            for (int lane = 0; lane < 3; lane++)
            {
                laneStyles[lane].SetSelected(State.ActiveLane == lane);
                laneFilters[lane].GetComponent<Image>().color = State.ActiveLane == lane ? Selected : CardColor;
                for (int slot = 0; slot < 3; slot++)
                {
                    CampaignOwnedSkill owned = run.GetLoadoutSlot(lane, slot);
                    Card card = slots[lane, slot];
                    card.SkillId = owned?.SkillId ?? 0;
                    card.Drag.Configure(this, card.SkillId, lane, slot);
                    card.Icon.enabled = owned != null;
                    card.Icon.sprite = owned == null ? null : art.GetSkillIcon(owned.Skill.IconId);
                    card.Name.text = owned?.Skill.Name ?? "빈 슬롯";
                    card.Name.color = owned == null ? Muted : Foreground;
                    card.Cost.text = owned == null ? string.Empty : "ACT " + owned.Skill.Cost;
                    card.Background.color = State.ActiveLane == lane && State.SelectedSlot == slot ? Selected : CardColor;
                }
            }
            if (builtLane != State.ActiveLane || builtOwnedCount != run.OwnedSkills.Count) BuildOwnedCards();
            foreach (Card card in ownedCards)
            {
                CampaignOwnedSkill owned = FindOwned(card.SkillId);
                card.Name.text = owned.Skill.Name;
                card.Marker.text = run.IsSkillInLoadout(card.SkillId) ? "편성" : string.Empty;
                card.Background.color = State.SelectedSkillId == card.SkillId ? Selected : CardColor;
            }
            RefreshDetail();
            counts.text = $"Q {run.GetLoadoutCount(0)}/3  ·  W {run.GetLoadoutCount(1)}/3  ·  E {run.GetLoadoutCount(2)}/3";
            string missing = string.Empty;
            for (int lane = 0; lane < 3; lane++)
                if (run.GetLoadoutCount(lane) < 3)
                    missing += (missing.Length > 0 ? " · " : string.Empty) + LaneNames[lane] + " " + (3 - run.GetLoadoutCount(lane)) + "개 부족";
            status.text = !run.HasLoadoutChanges ? "저장된 편성"
                : missing.Length > 0 ? "저장 전 변경 · " + missing : "저장 전 변경 · 저장하면 전투에 반영됩니다";
            saveButton.interactable = run.CanSaveLoadout && run.HasLoadoutChanges;
            saveButton.GetComponentInChildren<Text>().color = saveButton.interactable ? DuelVisualTheme.Ink : Foreground;
            cancelButton.interactable = run.HasLoadoutChanges;
        }

        private void BuildOwnedCards()
        {
            foreach (Card card in ownedCards) { card.Button.gameObject.SetActive(false); Destroy(card.Button.gameObject); }
            ownedCards.Clear();
            builtLane = State.ActiveLane;
            builtOwnedCount = run.OwnedSkills.Count;
            foreach (CampaignOwnedSkill owned in run.OwnedSkills)
            {
                if (owned.Skill.LaneIndex != State.ActiveLane) continue;
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
                card.Marker = Label("Owned Equipped", card.Button.transform, new Vector2(54f, -40f), new Vector2(60f, 20f), 13,
                    Accent, TextAnchor.MiddleRight);
                card.Drag = card.Button.gameObject.AddComponent<CampaignLoadoutCardDrag>();
                card.Drag.Configure(this, id, State.ActiveLane, -1);
                ownedCards.Add(card);
            }
            ownedItems.sizeDelta = new Vector2(Mathf.Max(1200f, ownedCards.Count * 200f), 126f);
            ownedItems.anchoredPosition = Vector2.zero;
        }

        private void ReconcileSelection()
        {
            CampaignOwnedSkill selected = FindOwned(State.SelectedSkillId);
            if (selected == null || selected.Skill.LaneIndex != State.ActiveLane)
            {
                State.SelectedSkillId = 0;
                State.SelectedSlot = Mathf.Clamp(State.SelectedSlot, -1, 2);
                return;
            }
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
            removeButton.gameObject.SetActive(owned != null && run.IsSkillInLoadout(owned.SkillId));
            if (owned == null)
            {
                detailStyle.Clear("선택한 기술");
                detailName.text = State.SelectedSlot >= 0 ? "빈 슬롯" : "기술을 선택하세요";
                detailRole.text = State.SelectedSlot >= 0
                    ? LaneNames[State.ActiveLane] + " " + SkillLaneStyle.Name(State.ActiveLane) + "에 배치" : string.Empty;
                detailInfo.SetEmptyMessage("기술을 클릭하면 설명을 봅니다.\n순서 변경·교체는 슬롯으로 끌어 놓으세요.");
                LayoutDetail();
                return;
            }
            LegacySkill skill = owned.Skill;
            detailStyle.SetLane(skill.LaneIndex, true, " · 강화 " + owned.Level + "/3");
            detailName.text = skill.Name;
            detailRole.text = CampaignSkillText.Purpose(skill);
            detailInfo.SetSkill(skill);
            LayoutDetail();
        }

        private void LayoutDetail()
        {
            float footer = removeButton.gameObject.activeSelf ? 54f : 16f;
            float height = 104f + detailInfo.Height + footer;
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
            removeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(12f, -top + 28f);
            removeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(310f, 34f);
        }

        private void SelectLane(int lane)
        {
            CancelDrag();
            if (State.ActiveLane != lane) { State.ActiveLane = lane; State.SelectedSkillId = 0; State.SelectedSlot = -1; }
            Refresh();
        }

        private void SelectOwned(int skillId)
        {
            CampaignOwnedSkill owned = FindOwned(skillId);
            if (disposed || owned == null) return;
            State.ActiveLane = owned.Skill.LaneIndex;
            State.SelectedSkillId = skillId;
            State.SelectedSlot = -1;
            Refresh();
        }

        private void ClickSlot(int lane, int slot)
        {
            if (disposed) return;
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
            if (disposed || id == 0 || !run.IsSkillInLoadout(id)) return;
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
            if (disposed || !root.gameObject.activeInHierarchy || FindOwned(skillId) == null || lane < 0 || lane > 2) return false;
            CancelDrag();
            State.ActiveLane = lane;
            State.SelectedSkillId = skillId;
            State.SelectedSlot = slot;
            Refresh();
            draggedSkillId = skillId;
            draggedLane = lane;
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
            draggedSkillId = 0;
            draggedLane = -1;
            if (dragGhost != null) { dragGhost.gameObject.SetActive(false); Destroy(dragGhost.gameObject); }
            dragGhost = dragLayer = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            CancelDrag();
            disposed = true;
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
