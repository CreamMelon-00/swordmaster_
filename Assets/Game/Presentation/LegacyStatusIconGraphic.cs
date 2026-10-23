using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Shape-coded status symbols: an upward sword, shields, and a blood drop.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LegacyStatusIconGraphic : MaskableGraphic
    {
        private LegacyStatusKind kind;

        public void SetKind(LegacyStatusKind value)
        {
            if (kind == value) return;
            kind = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            Vector2 centre = bounds.center;
            float scale = Mathf.Min(bounds.width, bounds.height) * .5f;
            if (scale <= 0f) return;
            if (kind == LegacyStatusKind.Jealousy)
            {
                Vector2[] eye = { new Vector2(-.92f, 0f), new Vector2(-.45f, .52f), new Vector2(.45f, .52f),
                    new Vector2(.92f, 0f), new Vector2(.45f, -.52f), new Vector2(-.45f, -.52f) };
                for (int i = 0; i < eye.Length; i++)
                    Segment(mesh, centre, scale, eye[i], eye[(i + 1) % eye.Length], .14f);
                Segment(mesh, centre, scale, new Vector2(0f, -.3f), new Vector2(0f, .3f), .24f);
                return;
            }
            if (kind == LegacyStatusKind.Fixed)
            {
                Polygon(mesh, centre, scale, new[] { new Vector2(-.65f, .12f), new Vector2(.65f, .12f),
                    new Vector2(.65f, -.84f), new Vector2(-.65f, -.84f) });
                Segment(mesh, centre, scale, new Vector2(-.4f, .12f), new Vector2(-.4f, .68f), .15f);
                Segment(mesh, centre, scale, new Vector2(-.4f, .68f), new Vector2(.4f, .68f), .15f);
                Segment(mesh, centre, scale, new Vector2(.4f, .68f), new Vector2(.4f, .12f), .15f);
                return;
            }
            if (kind == LegacyStatusKind.PowerUp)
            {
                Polygon(mesh, centre, scale, new[] { new Vector2(0f, .92f), new Vector2(-.48f, .35f),
                    new Vector2(-.19f, .35f), new Vector2(-.19f, -.42f), new Vector2(.19f, -.42f),
                    new Vector2(.19f, .35f), new Vector2(.48f, .35f) });
                Segment(mesh, centre, scale, new Vector2(-.46f, -.42f), new Vector2(.46f, -.42f), .12f);
                Segment(mesh, centre, scale, new Vector2(0f, -.42f), new Vector2(0f, -.88f), .16f);
                return;
            }
            if (kind == LegacyStatusKind.Bleeding)
            {
                Polygon(mesh, centre, scale, new[] { new Vector2(0f, .95f), new Vector2(-.28f, .48f),
                    new Vector2(-.58f, .04f), new Vector2(-.56f, -.45f), new Vector2(-.32f, -.78f),
                    new Vector2(0f, -.9f), new Vector2(.32f, -.78f), new Vector2(.56f, -.45f),
                    new Vector2(.58f, .04f), new Vector2(.28f, .48f) });
                return;
            }
            // Outline instead of a filled shield leaves the crack legible at the small HUD size.
            Vector2[] shield = { new Vector2(0f, .84f), new Vector2(.67f, .61f), new Vector2(.58f, -.18f),
                new Vector2(0f, -.86f), new Vector2(-.58f, -.18f), new Vector2(-.67f, .61f) };
            for (int i = 0; i < shield.Length; i++)
                Segment(mesh, centre, scale, shield[i], shield[(i + 1) % shield.Length], .14f);
            if (kind == LegacyStatusKind.Vulnerability)
            {
                Segment(mesh, centre, scale, new Vector2(.12f, .68f), new Vector2(-.16f, .19f), .15f);
                Segment(mesh, centre, scale, new Vector2(-.16f, .19f), new Vector2(.18f, -.07f), .15f);
                Segment(mesh, centre, scale, new Vector2(.18f, -.07f), new Vector2(-.15f, -.65f), .15f);
            }
            else
            {
                Segment(mesh, centre, scale, new Vector2(-.3f, .02f), new Vector2(-.06f, -.23f), .15f);
                Segment(mesh, centre, scale, new Vector2(-.06f, -.23f), new Vector2(.34f, .31f), .15f);
            }
        }

        private void Segment(VertexHelper mesh, Vector2 centre, float scale, Vector2 a, Vector2 b, float width)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(centre + (a - normal) * scale, color, Vector2.zero);
            mesh.AddVert(centre + (a + normal) * scale, color, Vector2.zero);
            mesh.AddVert(centre + (b + normal) * scale, color, Vector2.zero);
            mesh.AddVert(centre + (b - normal) * scale, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private void Polygon(VertexHelper mesh, Vector2 centre, float scale, Vector2[] points)
        {
            // These silhouettes are star-shaped about their centre.
            int start = mesh.currentVertCount;
            mesh.AddVert(centre, color, Vector2.zero);
            foreach (Vector2 point in points) mesh.AddVert(centre + point * scale, color, Vector2.zero);
            for (int i = 0; i < points.Length; i++)
                mesh.AddTriangle(start, start + i + 1, start + (i + 1) % points.Length + 1);
        }
    }
}
