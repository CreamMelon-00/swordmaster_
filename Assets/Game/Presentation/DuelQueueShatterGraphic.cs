using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Eight textured pieces of the actual reserved icon and its frame. Fixed geometry and a reusable mesh
    /// keep a long canceled queue from spawning particles, materials or per-piece GameObjects.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelQueueShatterGraphic : MaskableGraphic
    {
        private static readonly Vector2[] Rim = {
            new Vector2(-.5f, -.5f), new Vector2(0f, -.5f), new Vector2(.5f, -.5f), new Vector2(.5f, 0f),
            new Vector2(.5f, .5f), new Vector2(0f, .5f), new Vector2(-.5f, .5f), new Vector2(-.5f, 0f)
        };
        private Sprite sprite;
        private bool icon;
        private float outward;
        private int seed;
        public float Progress { get; private set; }
        public override Texture mainTexture => icon && sprite != null ? sprite.texture : s_WhiteTexture;

        public void Configure(Sprite picture, Color tint, bool drawIcon, float progress, float direction, int slot)
        {
            bool textureChanged = sprite != picture || icon != drawIcon;
            sprite = picture;
            color = tint;
            icon = drawIcon;
            Progress = Mathf.Clamp01(progress);
            outward = direction;
            seed = slot;
            raycastTarget = false;
            if (textureChanged) SetMaterialDirty();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (icon && sprite == null) return;
            Vector2 extent = rectTransform.rect.size;
            if (icon)
            {
                Vector2 spriteSize = sprite.rect.size;
                float fit = Mathf.Min(extent.x / spriteSize.x, extent.y / spriteSize.y);
                extent = spriteSize * fit;
            }
            Vector4 uv = icon ? DataUtility.GetOuterUV(sprite) : new Vector4(0f, 0f, 1f, 1f);
            float t = Progress * LegacyCombatHud.BreakQueueFragmentSeconds;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.38f, 1f, Progress));
            // A slightly off-centre fracture avoids a perfectly radial flower shape.
            Vector2 fracture = new Vector2((seed % 3 - 1) * .075f, .035f);
            for (int piece = 0; piece < Rim.Length; piece++)
            {
                Vector2 a = fracture, b = Rim[piece], c = Rim[(piece + 1) % Rim.Length];
                Vector2 centre = (a + b + c) / 3f;
                Vector2 direction = centre.normalized;
                float variation = ((piece * 7 + seed * 3) % 11) / 10f;
                Vector2 offset = new Vector2((direction.x * (105f + variation * 60f) + outward * 55f) * t,
                    (115f + direction.y * 90f + variation * 65f) * t - 380f * t * t);
                float spin = (piece % 2 == 0 ? 1f : -1f) * (125f + variation * 190f) * t * Mathf.Deg2Rad;
                float cos = Mathf.Cos(spin), sin = Mathf.Sin(spin);
                Color tint = color;
                if (!icon) tint = Color.Lerp(tint, DuelVisualTheme.Accent, Mathf.Max(0f, 1f - Progress / .14f));
                tint.a *= fade;
                if (icon) AddTriangle(mesh, a, b, c, centre, extent, uv, offset, cos, sin, tint);
                else
                {
                    Color fill = DuelVisualTheme.Card; fill.a *= fade;
                    AddTriangle(mesh, a, b, c, centre, extent, uv, offset, cos, sin, fill);
                    // The outside rim and two fine fracture edges travel with the same piece as the icon.
                    AddEdge(mesh, b, c, centre, extent, uv, offset, cos, sin, tint, 3f);
                    Color crack = tint; crack.a *= .55f;
                    AddEdge(mesh, a, b, centre, extent, uv, offset, cos, sin, crack, 1.1f);
                }
            }
        }

        private static void AddTriangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 centre,
            Vector2 extent, Vector4 uv, Vector2 offset, float cos, float sin, Color tint)
        {
            int start = mesh.currentVertCount;
            AddVertex(mesh, a, centre, extent, uv, offset, cos, sin, tint);
            AddVertex(mesh, b, centre, extent, uv, offset, cos, sin, tint);
            AddVertex(mesh, c, centre, extent, uv, offset, cos, sin, tint);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private static void AddEdge(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 centre, Vector2 extent,
            Vector4 uv, Vector2 offset, float cos, float sin, Color tint, float width)
        {
            Vector2 edge = Vector2.Scale(b - a, extent).normalized;
            Vector2 normal = new Vector2(-edge.y / extent.x, edge.x / extent.y) * width;
            AddTriangle(mesh, a, b, b + normal, centre, extent, uv, offset, cos, sin, tint);
            AddTriangle(mesh, a, b + normal, a + normal, centre, extent, uv, offset, cos, sin, tint);
        }

        private static void AddVertex(VertexHelper mesh, Vector2 point, Vector2 centre, Vector2 extent,
            Vector4 uv, Vector2 offset, float cos, float sin, Color tint)
        {
            Vector2 relative = Vector2.Scale(point - centre, extent);
            Vector2 position = Vector2.Scale(centre, extent) + offset +
                new Vector2(relative.x * cos - relative.y * sin, relative.x * sin + relative.y * cos);
            mesh.AddVert(position, tint, new Vector2(Mathf.Lerp(uv.x, uv.z, point.x + .5f),
                Mathf.Lerp(uv.y, uv.w, point.y + .5f)));
        }
    }
}
