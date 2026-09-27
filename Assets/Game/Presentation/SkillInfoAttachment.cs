using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public enum SkillInfoAttachmentShape { Seal, PowerPlate, TypeRibbon }

    /// <summary>Reusable paper/brass fittings on a skill card's edge; no textures or input.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillInfoAttachment : MaskableGraphic
    {
        private static readonly Vector2[] SealPoints = CreateSealPoints();
        private static readonly Vector2[] PowerPoints =
        {
            new Vector2(-.36f, .5f), new Vector2(.36f, .5f), new Vector2(.5f, .32f),
            new Vector2(.5f, -.32f), new Vector2(.36f, -.5f), new Vector2(-.36f, -.5f),
            new Vector2(-.5f, -.32f), new Vector2(-.5f, .32f)
        };
        private static readonly Vector2[] RibbonPoints =
        {
            new Vector2(-.5f, .34f), new Vector2(-.34f, .5f), new Vector2(.34f, .5f),
            new Vector2(.5f, .34f), new Vector2(.5f, -.5f), new Vector2(0f, -.40f),
            new Vector2(-.5f, -.5f)
        };
        private SkillInfoAttachmentShape shape;

        public void Configure(SkillInfoAttachmentShape value)
        {
            if (shape == value) return;
            shape = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width < 12f || bounds.height < 12f) return;
            Vector2[] points = shape == SkillInfoAttachmentShape.Seal ? SealPoints
                : shape == SkillInfoAttachmentShape.PowerPlate ? PowerPoints : RibbonPoints;
            Fill(vertices, bounds, points, 0f, new Vector2(2f, -2f), new Color(.10f, .08f, .06f, .9f));
            Fill(vertices, bounds, points, 0f, Vector2.zero, DuelVisualTheme.Border);
            Fill(vertices, bounds, points, 2f, Vector2.zero, DuelVisualTheme.Surface);
            Fill(vertices, bounds, points, 4f, Vector2.zero, color);
            Rim(vertices, bounds, points, 6f, .75f,
                new Color(DuelVisualTheme.Accent.r, DuelVisualTheme.Accent.g, DuelVisualTheme.Accent.b, .55f));
        }

        private static Vector2[] CreateSealPoints()
        {
            var points = new Vector2[32];
            for (int index = 0; index < points.Length; index++)
            {
                float angle = (90f - index * 360f / points.Length) * Mathf.Deg2Rad;
                points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f;
            }
            return points;
        }

        private static Vector2 Position(Rect bounds, Vector2 point, float inset, Vector2 offset) =>
            bounds.center + Vector2.Scale(point, bounds.size - Vector2.one * (inset * 2f)) + offset;

        private static void Fill(VertexHelper vertices, Rect bounds, Vector2[] points,
            float inset, Vector2 offset, Color tint)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(bounds.center + offset, tint, Vector2.zero);
            for (int index = 0; index < points.Length; index++)
                vertices.AddVert(Position(bounds, points[index], inset, offset), tint, Vector2.zero);
            for (int index = 0; index < points.Length; index++)
                vertices.AddTriangle(start, start + 1 + index, start + 1 + (index + 1) % points.Length);
        }

        private static void Rim(VertexHelper vertices, Rect bounds, Vector2[] points,
            float inset, float thickness, Color tint)
        {
            for (int index = 0; index < points.Length; index++)
            {
                int next = (index + 1) % points.Length, start = vertices.currentVertCount;
                vertices.AddVert(Position(bounds, points[index], inset, Vector2.zero), tint, Vector2.zero);
                vertices.AddVert(Position(bounds, points[next], inset, Vector2.zero), tint, Vector2.zero);
                vertices.AddVert(Position(bounds, points[next], inset + thickness, Vector2.zero), tint, Vector2.zero);
                vertices.AddVert(Position(bounds, points[index], inset + thickness, Vector2.zero), tint, Vector2.zero);
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}

