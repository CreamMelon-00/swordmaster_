using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The start card's mark between the two fighters (<see cref="DuelStartCard"/>): two swords crossed point up,
    /// drawn in code as UI geometry (steel blades, brass guards and pommels, dark grips, each edged in ink so it reads on
    /// any backdrop). <see cref="Spread"/> is each blade's lean from upright; the card closes them from wide to their rest
    /// as it comes in. No word: the card never names the kind of fight.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelCrossedSwords : MaskableGraphic
    {
        /// <summary>Each blade's lean from upright at rest, degrees.</summary>
        public const float RestSpread = 38f;
        private static readonly Color Blade = new Color(.86f, .89f, .9f, 1f);
        private static readonly Color BladeShade = new Color(.62f, .68f, .7f, 1f);
        private static readonly Color Edge = new Color(.08f, .07f, .06f, .9f);
        private static readonly Color Grip = DuelVisualTheme.RaisedSurface;
        private const float Rim = .012f;
        private float spread = RestSpread;

        public float Spread
        {
            get => spread;
            set
            {
                float next = float.IsNaN(value) ? RestSpread : Mathf.Clamp(value, 0f, 89f);
                if (Mathf.Approximately(next, spread)) return;
                spread = next;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            float length = Mathf.Min(bounds.width, bounds.height);
            if (length < 8f) return;
            // The swords cross a little above their middles, on the blades: the hilts low, the points high.
            Vector2 cross = bounds.center + Vector2.up * length * .06f;
            Sword(vertices, cross, -spread, length);
            Sword(vertices, cross, spread, length);
        }

        // One sword along its axis (pommel to point) leaning `lean` degrees from upright, positions as shares of its length
        // from the crossing: the guard, the grip below it, the pommel at the end, the blade up to its point.
        private void Sword(VertexHelper vertices, Vector2 cross, float lean, float length)
        {
            float radians = lean * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            var side = new Vector2(along.y, -along.x);
            const float guardAt = -.2f, gripEnd = -.4f, pommelAt = -.44f, tipStart = .4f, tip = .5f;
            const float bladeHalf = .034f, guardHalf = .12f, guardThick = .022f, gripHalf = .024f, pommel = .04f;
            // Ink first, a rim larger, so every part reads on the arena and on the card's veil.
            Strip(vertices, cross, along, side, length, guardAt, tipStart, bladeHalf + Rim, Edge);
            Point(vertices, cross, along, side, length, tipStart, tip + Rim * 1.6f, bladeHalf + Rim, Edge);
            Strip(vertices, cross, along, side, length, gripEnd - Rim, guardAt, gripHalf + Rim, Edge);
            Strip(vertices, cross, along, side, length, guardAt - guardThick - Rim, guardAt + guardThick + Rim, guardHalf + Rim, Edge);
            Diamond(vertices, cross + along * (pommelAt * length), pommel * length + Rim * length, Edge);
            // The blade, lit on one side; its point.
            Strip(vertices, cross, along, side, length, guardAt, tipStart, bladeHalf, Blade);
            Strip(vertices, cross, along, side, length, guardAt, tipStart, bladeHalf * .35f, BladeShade, bladeHalf * .5f);
            Point(vertices, cross, along, side, length, tipStart, tip, bladeHalf, Blade);
            Strip(vertices, cross, along, side, length, gripEnd, guardAt, gripHalf, Grip);
            Strip(vertices, cross, along, side, length, guardAt - guardThick, guardAt + guardThick, guardHalf, DuelVisualTheme.Accent);
            Diamond(vertices, cross + along * (pommelAt * length), pommel * length, DuelVisualTheme.Accent);
        }

        // A band along the axis from `from` to `to` (shares of the length), `half` wide each side, shifted sideways by `offset`.
        private void Strip(VertexHelper vertices, Vector2 cross, Vector2 along, Vector2 side, float length, float from, float to,
            float half, Color color, float offset = 0f)
        {
            Vector2 shift = side * (offset * length);
            Vector2 start = cross + along * (from * length) + shift, end = cross + along * (to * length) + shift;
            Vector2 width = side * (half * length);
            Quad(vertices, start - width, start + width, end + width, end - width, color);
        }

        private void Point(VertexHelper vertices, Vector2 cross, Vector2 along, Vector2 side, float length, float from, float to,
            float half, Color color)
        {
            Vector2 start = cross + along * (from * length);
            Vector2 width = side * (half * length);
            Triangle(vertices, start - width, start + width, cross + along * (to * length), color);
        }

        private void Diamond(VertexHelper vertices, Vector2 centre, float radius, Color color)
            => Quad(vertices, centre + Vector2.up * radius, centre + Vector2.right * radius, centre + Vector2.down * radius,
                centre + Vector2.left * radius, color);

        private void Quad(VertexHelper vertices, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            color *= this.color;
            int index = vertices.currentVertCount;
            vertices.AddVert(a, color, Vector2.zero);
            vertices.AddVert(b, color, Vector2.zero);
            vertices.AddVert(c, color, Vector2.zero);
            vertices.AddVert(d, color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }

        private void Triangle(VertexHelper vertices, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            color *= this.color;
            int index = vertices.currentVertCount;
            vertices.AddVert(a, color, Vector2.zero);
            vertices.AddVert(b, color, Vector2.zero);
            vertices.AddVert(c, color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
        }
    }
}
