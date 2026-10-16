using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The loadout screens' rack of the skills not in a gear (<see cref="CampaignLoadoutHud"/>'s owned skills,
    /// <see cref="VersusLoadoutHud"/>'s pool): compact cards in <see cref="Rows"/> rows, each column filled top to bottom,
    /// the rack scrolling sideways. Both screens lay their racks out from here, and filter them with
    /// <see cref="LoadoutFilter"/>, so they read alike.</summary>
    public static class LoadoutRack
    {
        /// <summary>The filter that shows every lane's skills.</summary>
        public const int AllLanes = -1;
        public const int Rows = 2;
        public const float ColumnPitch = 200f, RowPitch = 66f;
        /// <summary>A card: its icon at the left with its core mark, its name, and under it its ACT and its lane.</summary>
        public const float CardWidth = 192f, CardHeight = 60f;
        public const float Height = Rows * RowPitch;

        /// <summary>Where the <paramref name="shown"/>th visible card sits in the rack (from its top left).</summary>
        public static Vector2 CardPosition(int shown)
            => new Vector2((shown / Rows) * ColumnPitch + ColumnPitch * .5f,
                -((shown % Rows) * RowPitch + RowPitch * .5f));

        /// <summary>The rack's content width for <paramref name="shown"/> visible cards: never narrower than its window.</summary>
        public static float ContentWidth(int shown, float windowWidth)
            => Mathf.Max(windowWidth, (Mathf.Max(0, shown) + Rows - 1) / Rows * ColumnPitch);
    }

    /// <summary>A loadout rack's lane filter, a small row <c>전체 · Q · W · E</c> (the given lanes only, a dot between
    /// neighbours) whose left end sits at the given point. It keeps no state: its owner says which filter is chosen
    /// (<see cref="SetChoice"/>, <see cref="LoadoutRack.AllLanes"/> for 전체) and hears its clicks. Nodes are named from
    /// <c>prefix</c>: the row <c>"&lt;prefix&gt; Filter"</c> (its rect wraps the buttons), the buttons
    /// <c>"&lt;prefix&gt; Filter All"</c>, <c>"&lt;prefix&gt; Filter Q"</c>.., each with a <c>Button Label</c>, and a
    /// <c>Filter Dot</c> between neighbours.</summary>
    public sealed class LoadoutFilter
    {
        public const float AllWidth = 52f, LaneWidth = 34f, DotWidth = 14f, RowHeight = 28f;
        private const int FontSize = 16;

        private readonly Button all;
        private readonly Button[] lanes = new Button[3];

        /// <param name="left">The row's left end (x) and its centre line (y) in <paramref name="parent"/>.</param>
        /// <param name="choose">A filter was clicked (<see cref="LoadoutRack.AllLanes"/> or a lane).</param>
        public LoadoutFilter(Transform parent, Font font, string prefix, IReadOnlyList<int> openLanes, Vector2 left,
            Action<int> choose)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (openLanes == null) throw new ArgumentNullException(nameof(openLanes));
            Root = Rect(prefix + " Filter", parent, Vector2.zero, Vector2.zero);
            var buttons = new List<Button>();
            all = FilterButton(prefix + " Filter All", "전체", AllWidth, LoadoutRack.AllLanes, font, choose);
            buttons.Add(all);
            foreach (int lane in openLanes)
            {
                lanes[lane] = FilterButton(prefix + " Filter " + SkillLaneStyle.Key(lane), SkillLaneStyle.Key(lane), LaneWidth,
                    lane, font, choose);
                buttons.Add(lanes[lane]);
            }

            float width = DotWidth * (buttons.Count - 1);
            foreach (Button button in buttons) width += button.GetComponent<RectTransform>().sizeDelta.x;
            Root.anchoredPosition = new Vector2(left.x + width * .5f, left.y);
            Root.sizeDelta = new Vector2(width, RowHeight);
            float x = -width * .5f;
            for (int index = 0; index < buttons.Count; index++)
            {
                if (index > 0)
                {
                    Label("Filter Dot", Root, new Vector2(x + DotWidth * .5f, 0f), new Vector2(DotWidth, RowHeight), font,
                        DuelVisualTheme.Muted).text = "·";
                    x += DotWidth;
                }
                RectTransform rect = buttons[index].GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(x + rect.sizeDelta.x * .5f, 0f);
                x += rect.sizeDelta.x;
            }
        }

        public RectTransform Root { get; }

        /// <summary>The button for <paramref name="filter"/> (null for a lane not offered).</summary>
        public Button ButtonFor(int filter) => filter == LoadoutRack.AllLanes ? all : filter >= 0 && filter < 3 ? lanes[filter] : null;

        /// <summary>Marks the chosen filter: brass with dark ink; the others plain, 전체 in the foreground ink and each lane
        /// in its own colour.</summary>
        public void SetChoice(int filter)
        {
            Style(all, filter == LoadoutRack.AllLanes, DuelVisualTheme.Foreground);
            for (int lane = 0; lane < 3; lane++)
                if (lanes[lane] != null) Style(lanes[lane], filter == lane, SkillLaneStyle.Edge(lane));
        }

        private static void Style(Button button, bool chosen, Color ink)
        {
            button.GetComponent<Image>().color = chosen ? DuelVisualTheme.Accent : DuelVisualTheme.Card;
            button.GetComponentInChildren<Text>().color = chosen ? DuelVisualTheme.Ink : ink;
        }

        // A plain themed button (DuelVisualTheme.StyleButton, so with its trim) and its caption, as the screens' own.
        private Button FilterButton(string name, string caption, float width, int filter, Font font, Action<int> choose)
        {
            var size = new Vector2(width, RowHeight);
            var image = Rect(name, Root, Vector2.zero, size).gameObject.AddComponent<Image>();
            image.color = DuelVisualTheme.Card;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button);
            Label("Button Label", button.transform, Vector2.zero, size - new Vector2(12f, 4f), font,
                DuelVisualTheme.Foreground).text = caption;
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                choose?.Invoke(filter);
            });
            return button;
        }

        private static Text Label(string name, Transform parent, Vector2 position, Vector2 size, Font font, Color color)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = FontSize;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
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
    }
}
