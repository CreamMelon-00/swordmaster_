using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A brass coin with a readable edge while it flips. Geometry stays in the existing uGUI canvas, without
    /// textures, a second camera, physics or any authority over the opening-player result.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelTossCoin : MaskableGraphic
    {
        private const int Segments = 48;
        private float angle, pulse;
        private bool revealed, localFace;
        private static readonly Color Edge = new Color(.13f, .09f, .045f, 1f);
        private static readonly Color Brass = new Color(.76f, .55f, .27f, 1f);
        private static readonly Color Light = new Color(.94f, .79f, .48f, 1f);
        private static readonly Color Reverse = new Color(.73f, .53f, .39f, 1f);

        public void SetPose(float angle, bool revealed, bool localFace, float pulse)
        {
            if (Mathf.Approximately(this.angle, angle) && this.revealed == revealed && this.localFace == localFace &&
                Mathf.Approximately(this.pulse, pulse)) return;
            this.angle = angle;
            this.revealed = revealed;
            this.localFace = localFace;
            this.pulse = pulse;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            float radius = Mathf.Min(bounds.width, bounds.height) * .43f;
            if (radius < 2f) return;
            Vector2 centre = bounds.center;
            float faceWidth = Mathf.Max(.025f, Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad)));
            Vector2 scale = new Vector2(faceWidth, 1f);
            // The extrusion remains visible even with the face completely edge-on.
            Disc(vertices, centre + Vector2.right * 6f, radius + 2f, scale, Edge);
            Disc(vertices, centre, radius, scale, Brass);
            Disc(vertices, centre, radius - 4f, scale, Light);
            Disc(vertices, centre, radius - 9f, scale, Edge);
            Color face = revealed && !localFace ? Reverse : DuelVisualTheme.Paper;
            Disc(vertices, centre, radius - 12f, scale, face);
            Ring(vertices, centre, radius - 19f, 1.5f, scale, Brass);
            for (int index = 0; index < 12; index++)
            {
                float a = index * Mathf.PI / 6f;
                Vector2 at = centre + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius - 6f), scale);
                Diamond(vertices, at, new Vector2(2.5f * faceWidth, 2.5f), Edge);
            }
            // The anonymous seal is replaced by the viewer-relative text only after landing.
            if (!revealed)
            {
                Diamond(vertices, centre, new Vector2(25f * faceWidth, 42f), Brass);
                Diamond(vertices, centre, new Vector2(13f * faceWidth, 28f), face);
            }
            else
            {
                Diamond(vertices, centre + Vector2.up * 53f, new Vector2(6f, 8f), Brass);
                Diamond(vertices, centre - Vector2.up * 53f, new Vector2(6f, 8f), Brass);
                if (pulse > 0f)
                    for (int index = 0; index < 8; index++)
                    {
                        float a = index * Mathf.PI / 4f;
                        Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Color flash = Light;
                        flash.a = pulse;
                        Diamond(vertices, centre + radial * (radius + 16f + (1f - pulse) * 13f),
                            new Vector2(2f + pulse * 2f, 4f + pulse * 4f), flash);
                    }
            }
        }

        private void Disc(VertexHelper vertices, Vector2 centre, float radius, Vector2 scale, Color tint)
        {
            int first = vertices.currentVertCount;
            vertices.AddVert(centre, tint * color, Vector2.zero);
            for (int index = 0; index < Segments; index++)
            {
                float a = index * Mathf.PI * 2f / Segments;
                vertices.AddVert(centre + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, scale),
                    tint * color, Vector2.zero);
            }
            for (int index = 0; index < Segments; index++)
                vertices.AddTriangle(first, first + 1 + index, first + 1 + (index + 1) % Segments);
        }

        private void Ring(VertexHelper vertices, Vector2 centre, float radius, float thickness, Vector2 scale, Color tint)
        {
            int first = vertices.currentVertCount;
            for (int index = 0; index < Segments; index++)
            {
                float a = index * Mathf.PI * 2f / Segments;
                Vector2 at = Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), scale);
                vertices.AddVert(centre + at * radius, tint * color, Vector2.zero);
                vertices.AddVert(centre + at * (radius - thickness), tint * color, Vector2.zero);
            }
            for (int index = 0; index < Segments; index++)
            {
                int current = first + index * 2, next = first + ((index + 1) % Segments) * 2;
                vertices.AddTriangle(current, next, next + 1);
                vertices.AddTriangle(current, next + 1, current + 1);
            }
        }

        private void Diamond(VertexHelper vertices, Vector2 centre, Vector2 radius, Color tint)
        {
            int first = vertices.currentVertCount;
            vertices.AddVert(centre + Vector2.up * radius.y, tint * color, Vector2.zero);
            vertices.AddVert(centre + Vector2.right * radius.x, tint * color, Vector2.zero);
            vertices.AddVert(centre - Vector2.up * radius.y, tint * color, Vector2.zero);
            vertices.AddVert(centre - Vector2.right * radius.x, tint * color, Vector2.zero);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first, first + 2, first + 3);
        }
    }
}
