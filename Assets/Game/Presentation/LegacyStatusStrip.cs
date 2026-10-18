using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A fighter's live statuses. Counts come only from combat snapshots; hovering never changes them.</summary>
    public sealed class LegacyStatusStrip : MonoBehaviour
    {
        public const float IconWidth = 40f, Height = 44f, Gap = 5f;
        private const float TooltipWidth = 336f, TooltipHeight = 174f;
        private readonly List<StatusIcon> icons = new List<StatusIcon>();
        private readonly List<LegacyStatusSnapshot> displayedStatuses = new List<LegacyStatusSnapshot>();
        private RectTransform root, canvasRect, tooltip;
        private Text tooltipTitle, tooltipDescription;
        private Font font;
        private int count, hovered = -1;

        public RectTransform Root => root;
        public int Count => count;
        public RectTransform TooltipRoot => tooltip;
        /// <summary>The displayed snapshot: statuses of the same kind with the same remaining count share one icon.</summary>
        public LegacyStatusSnapshot GetStatus(int index) => icons[index].Snapshot;
        public RectTransform GetIconRoot(int index) => index >= 0 && index < count ? icons[index].Root : null;

        public static LegacyStatusStrip Create(RectTransform parent, Font font, string name)
        {
            RectTransform rect = MakeRect(name, parent, Vector2.zero, new Vector2(IconWidth, Height));
            var result = rect.gameObject.AddComponent<LegacyStatusStrip>();
            result.root = rect;
            result.canvasRect = parent;
            result.font = font;
            result.BuildTooltip();
            rect.gameObject.SetActive(false);
            return result;
        }

        public void Refresh(IReadOnlyList<LegacyStatusSnapshot> statuses)
        {
            displayedStatuses.Clear();
            if (statuses != null)
                foreach (LegacyStatusSnapshot value in statuses)
                {
                    int matching = -1;
                    for (int i = 0; i < displayedStatuses.Count; i++)
                        if (displayedStatuses[i].Kind == value.Kind && displayedStatuses[i].RemainingCount == value.RemainingCount)
                        { matching = i; break; }
                    if (matching < 0) displayedStatuses.Add(value);
                    else
                    {
                        LegacyStatusSnapshot previous = displayedStatuses[matching];
                        displayedStatuses[matching] = new LegacyStatusSnapshot(value.Kind,
                            checked(previous.Percent + value.Percent), value.RemainingCount);
                    }
                }
            count = displayedStatuses.Count;
            while (icons.Count < count) icons.Add(BuildIcon(icons.Count));
            float width = count > 0 ? count * IconWidth + (count - 1) * Gap : 0f;
            root.sizeDelta = new Vector2(width, Height);
            for (int index = 0; index < icons.Count; index++)
            {
                StatusIcon icon = icons[index];
                icon.Root.gameObject.SetActive(index < count);
                if (index >= count) continue;
                LegacyStatusSnapshot value = displayedStatuses[index];
                if (index == hovered && icon.Snapshot.Kind != value.Kind) HideTooltip();
                icon.Snapshot = value;
                icon.Root.anchoredPosition = new Vector2(-width * .5f + IconWidth * .5f + index * (IconWidth + Gap), 0f);
                icon.Symbol.SetKind(value.Kind);
                Color ink = value.Kind == LegacyStatusKind.PowerUp ? new Color32(235, 210, 250, 255)
                    : value.Kind == LegacyStatusKind.Protection ? new Color32(143, 236, 226, 255)
                    : new Color32(255, 159, 135, 255);
                icon.Symbol.color = ink;
                icon.Magnitude.text = value.Percent + "%";
                icon.Remaining.text = value.RemainingCount.ToString();
            }
            if (hovered >= count) HideTooltip();
            else if (hovered >= 0) UpdateTooltip();
            root.gameObject.SetActive(count > 0);
        }

        public void SetVisible(bool shown)
        {
            root.gameObject.SetActive(shown && count > 0);
            if (!shown) HideTooltip();
        }

        public void ShowTooltip(int index)
        {
            if (index < 0 || index >= count || !root.gameObject.activeInHierarchy) return;
            hovered = index;
            UpdateTooltip();
            tooltip.SetAsLastSibling();
            tooltip.gameObject.SetActive(true);
        }

        public void HideTooltip()
        {
            hovered = -1;
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (hovered >= 0) PositionTooltip();
        }

        private void OnDisable() => HideTooltip();

        private void OnDestroy()
        {
            if (tooltip != null) Destroy(tooltip.gameObject);
        }

        private void UpdateTooltip()
        {
            LegacyStatusSnapshot value = icons[hovered].Snapshot;
            tooltipTitle.text = LegacyStatusNames.Name(value.Kind) + "  ·  " + value.RemainingCount + "회 남음";
            tooltipDescription.text = LegacyStatusNames.Description(value.Kind, value.Percent);
            PositionTooltip();
        }

        private void PositionTooltip()
        {
            if (tooltip == null || hovered < 0) return;
            Vector3 world = icons[hovered].Root.TransformPoint(new Vector3(0f, Height * .5f, 0f));
            Vector2 position = canvasRect.InverseTransformPoint(world);
            position.y += TooltipHeight * .5f + 10f;
            Vector2 half = canvasRect.rect.size * .5f;
            position.x = Mathf.Clamp(position.x, -half.x + TooltipWidth * .5f + 12f, half.x - TooltipWidth * .5f - 12f);
            position.y = Mathf.Clamp(position.y, -half.y + TooltipHeight * .5f + 12f, half.y - TooltipHeight * .5f - 12f);
            tooltip.anchoredPosition = position;
        }

        private StatusIcon BuildIcon(int index)
        {
            RectTransform rect = MakeRect("Status Icon " + index, root, Vector2.zero, new Vector2(IconWidth, Height));
            Image plate = rect.gameObject.AddComponent<Image>();
            plate.color = new Color(.055f, .065f, .08f, .96f);
            plate.raycastTarget = true;
            Outline frame = rect.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(.72f, .68f, .54f, .9f);
            frame.effectDistance = Vector2.one;
            RectTransform glyph = MakeRect("Status Symbol", rect, new Vector2(0f, 3f), Vector2.one * 26f);
            LegacyStatusIconGraphic symbol = glyph.gameObject.AddComponent<LegacyStatusIconGraphic>();
            symbol.raycastTarget = false;
            Text magnitude = MakeText("Status Magnitude", rect, new Vector2(0f, -14f), new Vector2(38f, 13f), 11);
            Text remaining = MakeText("Status Remaining", rect, new Vector2(14f, 15f), new Vector2(19f, 19f), 14);
            remaining.fontStyle = FontStyle.Bold;
            Outline countOutline = remaining.gameObject.AddComponent<Outline>();
            countOutline.effectColor = Color.black;
            countOutline.effectDistance = Vector2.one;
            rect.gameObject.AddComponent<LegacyStatusHoverTarget>().Configure(this, index);
            return new StatusIcon { Root = rect, Symbol = symbol, Magnitude = magnitude, Remaining = remaining };
        }

        private void BuildTooltip()
        {
            tooltip = MakeRect(name + " Tooltip", canvasRect, Vector2.zero, new Vector2(TooltipWidth, TooltipHeight));
            Image background = tooltip.gameObject.AddComponent<Image>();
            background.color = DuelVisualTheme.Surface;
            background.raycastTarget = false;
            Outline outline = tooltip.gameObject.AddComponent<Outline>();
            outline.effectColor = DuelVisualTheme.Accent;
            outline.effectDistance = Vector2.one;
            tooltipTitle = MakeText("Status Name", tooltip, new Vector2(0f, 58f), new Vector2(308f, 30f), 20);
            tooltipTitle.alignment = TextAnchor.MiddleLeft;
            tooltipTitle.color = DuelVisualTheme.Accent;
            tooltipDescription = MakeText("Status Definition", tooltip, new Vector2(0f, -20f), new Vector2(308f, 112f), 16);
            tooltipDescription.alignment = TextAnchor.UpperLeft;
            tooltipDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltipDescription.verticalOverflow = VerticalWrapMode.Truncate;
            tooltip.gameObject.SetActive(false);
        }

        private Text MakeText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            Text text = MakeRect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = DuelVisualTheme.Foreground;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform MakeRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var node = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private sealed class StatusIcon
        {
            public RectTransform Root;
            public LegacyStatusIconGraphic Symbol;
            public Text Magnitude, Remaining;
            public LegacyStatusSnapshot Snapshot;
        }
    }

    public sealed class LegacyStatusHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private LegacyStatusStrip owner;
        private int index;
        public void Configure(LegacyStatusStrip value, int slot) { owner = value; index = slot; }
        public void OnPointerEnter(PointerEventData eventData) => owner.ShowTooltip(index);
        public void OnPointerExit(PointerEventData eventData) => owner.HideTooltip();
    }
}
