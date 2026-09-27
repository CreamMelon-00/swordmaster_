using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The duel club's restrained wood, brass and paper vocabulary. No gameplay state.</summary>
    public static class DuelVisualTheme
    {
        public static readonly Color Surface = Hex(0x29231c);
        public static readonly Color RaisedSurface = Hex(0x382d23);
        public static readonly Color Card = Hex(0x30291f);
        public static readonly Color Selected = Hex(0x51412b);
        public static readonly Color Border = Hex(0x957449);
        public static readonly Color Accent = Hex(0xc8a365);
        public static readonly Color Foreground = Hex(0xe9debf);
        public static readonly Color Muted = Hex(0xb7aa8e);
        public static readonly Color Ink = Hex(0x332c23);
        public static readonly Color Paper = Hex(0xd8c8a4);
        public static readonly Color Danger = Hex(0xc58069);
        public static readonly Color Track = Hex(0x1e211f);
        public static readonly Color Health = Hex(0x789878);
        public static readonly Color Steel = Hex(0x91aaa7);

        // Decorations are code-native UI geometry, not generated paintings over interactive controls.
        public static void Frame(Image image, bool ornate = false)
        {
            if (image == null) return;
            Transform existing = image.transform.Find("Brass Trim");
            DuelPanelTrim trim;
            if (existing != null) trim = existing.GetComponent<DuelPanelTrim>();
            else
            {
                var decoration = new GameObject("Brass Trim", typeof(RectTransform), typeof(CanvasRenderer));
                decoration.layer = image.gameObject.layer;
                var rect = (RectTransform)decoration.transform;
                rect.SetParent(image.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = rect.anchoredPosition = Vector2.zero;
                trim = decoration.AddComponent<DuelPanelTrim>();
                // Existing labels/icons stay above the decorative material.
                rect.SetAsFirstSibling();
            }
            if (trim == null) return;
            trim.raycastTarget = false;
            trim.Configure(ornate);
        }

        public static void DressPanel(Image image) => Frame(image, true);

        public static void StyleButton(Button button, bool primary = false)
        {
            if (button == null) return;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.10f, 1.06f, .95f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.80f, .75f, .65f, 1f);
            colors.disabledColor = new Color(.63f, .60f, .55f, 1f);
            colors.fadeDuration = .10f;
            button.colors = colors;
            Frame(button.targetGraphic as Image, primary);
        }

        private static Color Hex(uint rgb) => new Color(((rgb >> 16) & 255) / 255f,
            ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }

    /// <summary>One non-interactive, batched trim mesh. No textures, Update or per-frame allocations.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelPanelTrim : MaskableGraphic
    {
        private bool ornate;

        public void Configure(bool value)
        {
            ornate = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width < 12f || bounds.height < 12f) return;
            Color brass = DuelVisualTheme.Border;
            Color shade = new Color(.08f, .065f, .045f, .8f);
            float inset = ornate ? 6f : 2f;
            Outline(vertices, bounds, 0f, 2f, shade);
            Outline(vertices, bounds, inset, 1f, brass);
            // Short highlight on the upper edge reads as a worn metal rim, not a neon outline.
            Quad(vertices, bounds.xMin + inset + 8f, bounds.yMax - inset - 2f,
                Mathf.Max(0f, bounds.width - inset * 2f - 16f), 1f,
                new Color(DuelVisualTheme.Accent.r, DuelVisualTheme.Accent.g, DuelVisualTheme.Accent.b, .38f));
            if (!ornate) return;
            Color pin = DuelVisualTheme.Accent;
            foreach (float sideX in new[] { bounds.xMin + 9f, bounds.xMax - 9f })
                foreach (float sideY in new[] { bounds.yMin + 9f, bounds.yMax - 9f })
                {
                    Quad(vertices, sideX - 2f, sideY - 2f, 4f, 4f, shade);
                    Quad(vertices, sideX - 1f, sideY, 2f, 1f, pin);
                    Quad(vertices, sideX, sideY - 1f, 1f, 2f, pin);
                }
            // Very quiet horizontal grain only at the blank panel edges; never under body text.
            Color grain = new Color(brass.r, brass.g, brass.b, .13f);
            for (int line = 0; line < 3; line++)
                Quad(vertices, bounds.xMin + 18f + line * 6f, bounds.yMin + 15f + line * 3f,
                    Mathf.Min(54f, bounds.width * .16f), 1f, grain);
        }

        private static void Outline(VertexHelper vertices, Rect bounds, float inset, float width, Color color)
        {
            float x = bounds.xMin + inset, y = bounds.yMin + inset;
            float w = bounds.width - inset * 2f, h = bounds.height - inset * 2f;
            Quad(vertices, x, y, w, width, color);
            Quad(vertices, x, y + h - width, w, width, color);
            Quad(vertices, x, y + width, width, h - width * 2f, color);
            Quad(vertices, x + w - width, y + width, width, h - width * 2f, color);
        }

        private static void Quad(VertexHelper vertices, float x, float y, float width, float height, Color color)
        {
            if (width <= 0f || height <= 0f) return;
            int index = vertices.currentVertCount;
            vertices.AddVert(new Vector3(x, y), color, Vector2.zero);
            vertices.AddVert(new Vector3(x, y + height), color, Vector2.zero);
            vertices.AddVert(new Vector3(x + width, y + height), color, Vector2.zero);
            vertices.AddVert(new Vector3(x + width, y), color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }
    }
}
