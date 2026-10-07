using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>
    /// Small mirrored pennants for the turn badge. The staffs cross below the number,
    /// leaving the middle of the badge clear for a separate text graphic.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelCrossedFlags : MaskableGraphic
    {
        public const float PreferredWidth = 208f;
        public const float PreferredHeight = 70f;
        public const float NumberClearWidth = 60f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width < 24f || bounds.height < 12f) return;

            float scale = Mathf.Min(bounds.width / PreferredWidth, bounds.height / PreferredHeight);
            Vector2 origin = bounds.center;

            // The centre of each pole sits behind the number. Only its crossed foot
            // is visible below the numeral; the upper segment emerges at either side.
            Staff(vertices, origin, scale, -1f);
            Staff(vertices, origin, scale, 1f);
            Pennant(vertices, origin, scale, -1f);
            Pennant(vertices, origin, scale, 1f);
        }

        private void Staff(VertexHelper vertices, Vector2 origin, float scale, float side)
        {
            Vector2 foot = Position(origin, scale, side, -12f, -31f);
            Vector2 innerEnd = Position(origin, scale, side, 3f, -19f);
            Vector2 outerStart = Position(origin, scale, side, 38f, 9f);
            Vector2 top = Position(origin, scale, side, 59f, 29f);

            Segment(vertices, foot, innerEnd, 5f * scale, DuelVisualTheme.Ink);
            Segment(vertices, outerStart, top, 5f * scale, DuelVisualTheme.Ink);
            Segment(vertices, foot, innerEnd, 2.6f * scale, DuelVisualTheme.Border);
            Segment(vertices, outerStart, top, 2.6f * scale, DuelVisualTheme.Border);
            Diamond(vertices, top, 2.5f * scale, DuelVisualTheme.Accent);
        }

        private void Pennant(VertexHelper vertices, Vector2 origin, float scale, float side)
        {
            // Swallowtail silhouettes face away from the turn number.
            Shape(vertices, origin, scale, side, 57f, 94f, 84f, 7f, 25f, DuelVisualTheme.Ink);
            Shape(vertices, origin, scale, side, 60f, 90f, 80f, 10f, 22f, DuelVisualTheme.Accent);
            Segment(vertices, Position(origin, scale, side, 63f, 19f),
                Position(origin, scale, side, 85f, 19f), 1.7f * scale, DuelVisualTheme.Paper);
        }

        private static Vector2 Position(Vector2 origin, float scale, float side, float x, float y)
            => origin + new Vector2(side * x * scale, y * scale);

        private void Shape(VertexHelper vertices, Vector2 origin, float scale, float side,
            float near, float far, float notch, float bottom, float top, Color tint)
        {
            Vector2 a = Position(origin, scale, side, near, top);
            Vector2 b = Position(origin, scale, side, far, top);
            Vector2 c = Position(origin, scale, side, notch, (bottom + top) * .5f);
            Vector2 d = Position(origin, scale, side, far, bottom);
            Vector2 e = Position(origin, scale, side, near, bottom);
            Triangle(vertices, a, b, c, tint);
            Triangle(vertices, a, c, e, tint);
            Triangle(vertices, c, d, e, tint);
        }

        private void Segment(VertexHelper vertices, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 side = new Vector2(from.y - to.y, to.x - from.x).normalized * (width * .5f);
            Quad(vertices, from - side, from + side, to + side, to - side, tint);
        }

        private void Diamond(VertexHelper vertices, Vector2 centre, float radius, Color tint)
            => Quad(vertices, centre + Vector2.up * radius, centre + Vector2.right * radius,
                centre + Vector2.down * radius, centre + Vector2.left * radius, tint);

        private void Quad(VertexHelper vertices, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            Color vertexColor = tint * color;
            int index = vertices.currentVertCount;
            vertices.AddVert(a, vertexColor, Vector2.zero);
            vertices.AddVert(b, vertexColor, Vector2.zero);
            vertices.AddVert(c, vertexColor, Vector2.zero);
            vertices.AddVert(d, vertexColor, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }

        private void Triangle(VertexHelper vertices, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            Color vertexColor = tint * color;
            int index = vertices.currentVertCount;
            vertices.AddVert(a, vertexColor, Vector2.zero);
            vertices.AddVert(b, vertexColor, Vector2.zero);
            vertices.AddVert(c, vertexColor, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
        }
    }
}
