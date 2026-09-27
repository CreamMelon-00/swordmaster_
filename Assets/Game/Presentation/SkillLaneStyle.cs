using System;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Names for authored skill groups, not damage properties or additional combat effects.</summary>
    public static class SkillLaneStyle
    {
        private static readonly string[] Names = { "정공", "강공", "기교" };
        private static readonly string[] FullNames = { "정공 검술", "강공 검술", "기교 검술" };
        private static readonly string[] Keys = { "Q", "W", "E" };
        private static readonly Color32[] Papers =
        {
            new Color32(218, 204, 172, 255), new Color32(189, 207, 201, 255), new Color32(217, 188, 174, 255),
        };

        public static string Name(int lane) => Names[Checked(lane)];
        public static string FullName(int lane) => FullNames[Checked(lane)];
        public static string Key(int lane) => Keys[Checked(lane)];
        public static Color Paper(int lane) => Papers[Checked(lane)];

        private static int Checked(int lane)
        {
            if (lane < 0 || lane > 2) throw new ArgumentOutOfRangeException(nameof(lane));
            return lane;
        }
    }

    /// <summary>A reusable, non-interactive brass label. Enemy skills omit the player's input key.</summary>
    public sealed class SkillLaneBadge
    {
        private readonly Image background, keySurface;
        private readonly Text key;
        private int shownLane = -1;
        private bool shownKey, selected;
        private string shownSuffix;

        public RectTransform Root { get; }
        public Text Label { get; }

        public SkillLaneBadge(Transform parent, Font font, string labelName, Vector2 center,
            float width = 144f, float height = 24f, int fontSize = 15)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            Root = Rect(labelName + " Style Badge", parent, center, new Vector2(width, height));
            background = Root.gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            DuelVisualTheme.Frame(background);
            var keyRect = Rect("Style Key Surface", Root, Vector2.zero, new Vector2(height + 2f, height - 4f));
            keySurface = keyRect.gameObject.AddComponent<Image>();
            keySurface.raycastTarget = false;
            keySurface.color = DuelVisualTheme.Surface;
            key = Text("Style Key", keyRect, font, keyRect.sizeDelta, fontSize);
            key.alignment = TextAnchor.MiddleCenter;
            key.color = DuelVisualTheme.Foreground;
            Label = Text(labelName, Root, font, new Vector2(width - 42f, height - 2f), fontSize);
            Clear();
        }

        public void SetLane(int lane, bool showKey = true, string suffix = null)
        {
            string name = SkillLaneStyle.FullName(lane);
            if (shownLane == lane && shownKey == showKey && shownSuffix == suffix) return;
            shownLane = lane; shownKey = showKey; shownSuffix = suffix;
            Label.text = name + (suffix ?? string.Empty);
            key.text = SkillLaneStyle.Key(lane);
            Arrange(showKey);
            StyleSelection();
        }

        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            StyleSelection();
        }

        public void Clear(string caption = null)
        {
            shownLane = -1; shownSuffix = null;
            Label.text = caption ?? string.Empty;
            key.text = string.Empty;
            background.color = DuelVisualTheme.Paper;
            Arrange(false);
        }

        private void Arrange(bool showKey)
        {
            keySurface.gameObject.SetActive(showKey);
            keySurface.rectTransform.anchoredPosition = new Vector2(-Root.sizeDelta.x / 2f + keySurface.rectTransform.sizeDelta.x / 2f + 2f, 0f);
            Label.rectTransform.anchoredPosition = new Vector2(showKey ? 17f : 0f, 0f);
            Label.rectTransform.sizeDelta = new Vector2(Root.sizeDelta.x - (showKey ? 42f : 12f), Root.sizeDelta.y - 2f);
            Label.alignment = showKey ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
        }

        private void StyleSelection()
        {
            if (shownLane < 0) return;
            background.color = selected ? Color.Lerp(SkillLaneStyle.Paper(shownLane), DuelVisualTheme.Accent, .22f) : SkillLaneStyle.Paper(shownLane);
            keySurface.color = selected ? DuelVisualTheme.Accent : DuelVisualTheme.Surface;
            key.color = selected ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 center, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = center; rect.sizeDelta = size;
            return rect;
        }

        private static Text Text(string name, Transform parent, Font font, Vector2 size, int fontSize)
        {
            var label = Rect(name, parent, Vector2.zero, size).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = fontSize; label.color = DuelVisualTheme.Ink;
            label.supportRichText = false; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = fontSize - 2; label.resizeTextMaxSize = fontSize;
            return label;
        }
    }
}
