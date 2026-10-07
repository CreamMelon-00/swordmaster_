using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A speech bubble's tail (<see cref="DuelBarks"/>), as code-drawn UI geometry: a brass-rimmed wedge from the
    /// bubble's lower edge down to its tip over the fighter's head, opening into the bubble so the rim does not cross it;
    /// or, for a thought, two beads trailing off instead. Its rect's lower <see cref="Height"/> is the tail; the rest
    /// reaches up over the bubble's own rim.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelBarkTail : MaskableGraphic
    {
        public const float Width = 22f, Height = 14f, Overlap = 4f;
        private const float Rim = 2f;
        private bool thought;
        private Color fill = DuelVisualTheme.Surface, rim = DuelVisualTheme.Border;

        public bool IsThought => thought;

        public void Configure(bool isThought, Color surface, Color edge)
        {
            if (thought == isThought && fill == surface && rim == edge) return;
            thought = isThought;
            fill = surface;
            rim = edge;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width < 4f || bounds.height < Overlap + 4f) return;
            float x = bounds.center.x, tip = bounds.yMin, edge = bounds.yMax - Overlap;
            if (thought)
            {
                // Two beads trailing down toward the head, the larger one just under the bubble.
                Bead(vertices, new Vector2(x - 1.5f, edge - 3.5f), 3f);
                Bead(vertices, new Vector2(x + 1.5f, tip + 2.6f), 1.2f);
                return;
            }
            float half = bounds.width * .5f;
            Triangle(vertices, new Vector2(x - half, edge), new Vector2(x + half, edge), new Vector2(x, tip), rim);
            Triangle(vertices, new Vector2(x - half + Rim * 1.6f, edge), new Vector2(x + half - Rim * 1.6f, edge),
                new Vector2(x, tip + Rim * 2.4f), fill);
            // Over the bubble's rim where the wedge joins it.
            Quad(vertices, x - half + Rim * 1.6f, edge - .5f, bounds.width - Rim * 3.2f, Overlap + .5f, fill);
        }

        private void Bead(VertexHelper vertices, Vector2 centre, float radius)
        {
            Diamond(vertices, centre, radius + Rim, rim);
            Diamond(vertices, centre, radius, fill);
        }

        private static void Diamond(VertexHelper vertices, Vector2 centre, float radius, Color color)
        {
            int index = vertices.currentVertCount;
            vertices.AddVert(new Vector3(centre.x, centre.y + radius), color, Vector2.zero);
            vertices.AddVert(new Vector3(centre.x + radius, centre.y), color, Vector2.zero);
            vertices.AddVert(new Vector3(centre.x, centre.y - radius), color, Vector2.zero);
            vertices.AddVert(new Vector3(centre.x - radius, centre.y), color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }

        private static void Triangle(VertexHelper vertices, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int index = vertices.currentVertCount;
            vertices.AddVert(a, color, Vector2.zero);
            vertices.AddVert(b, color, Vector2.zero);
            vertices.AddVert(c, color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
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
