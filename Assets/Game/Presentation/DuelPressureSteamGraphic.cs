using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A small pressure leak and a short valve-release cloud, driven by the HUD's own clock.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelPressureSteamGraphic : MaskableGraphic
    {
        public const int MaximumPuffs = 12;
        private const int SoftPuffSegments = 12;
        public const int MaximumVertices = MaximumPuffs * (SoftPuffSegments * 2 + 1);
        private static readonly Color SteamInk = new Color(.94f, .92f, .84f, 1f);
        private static readonly Color ReleaseInk = new Color(.84f, .87f, .84f, 1f);
        private Sprite puffSprite;
        private Vector4 puffUv;
        private float charge, release;
        private bool releasing;

        public float Charge => charge;
        public float Release => release;
        public bool IsReleasing => releasing;
        public override Texture mainTexture => puffSprite != null ? puffSprite.texture : base.mainTexture;

        /// <param name="puff">The HUD's existing generated steam sprite, if available; otherwise a soft mesh is used.</param>
        public static DuelPressureSteamGraphic Create(Transform parent, string name, Vector2 size, Sprite puff = null)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.gameObject.layer = parent.gameObject.layer;
            node.SetParent(parent, false);
            node.anchorMin = node.anchorMax = node.pivot = Vector2.one * .5f;
            node.anchoredPosition = Vector2.zero;
            node.sizeDelta = size;
            var graphic = node.gameObject.AddComponent<DuelPressureSteamGraphic>();
            graphic.raycastTarget = false;
            graphic.color = Color.white;
            graphic.puffSprite = puff;
            graphic.puffUv = puff != null ? DataUtility.GetOuterUV(puff) : new Vector4(0f, 0f, 1f, 1f);
            node.gameObject.SetActive(false);
            return graphic;
        }

        /// <summary>Charge and release are normalized elapsed animation progress, not combat values or new resources.
        /// Configure(0, 0, false) hides and resets the effect. The graphic never advances time on its own.</summary>
        public void Configure(float charge01, float release01, bool isReleasing)
        {
            charge01 = SafeProgress(charge01);
            release01 = SafeProgress(release01);
            bool visible = isReleasing ? release01 < 1f : charge01 > 0f;
            bool changed = charge != charge01 || release != release01 || releasing != isReleasing;
            charge = charge01;
            release = release01;
            releasing = isReleasing;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (visible && changed) SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            charge = release = 0f;
            releasing = false;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width <= 2f || bounds.height <= 2f) return;
            if (releasing)
            {
                if (release >= 1f) return;
                DrawRelease(vertices, bounds);
            }
            else if (charge > 0f) DrawLeak(vertices, bounds);
        }

        private void DrawLeak(VertexHelper vertices, Rect bounds)
        {
            float radius = Mathf.Min(bounds.height * .1f, bounds.width * .045f);
            for (int index = 0; index < 6; index++)
            {
                float phase = Mathf.Repeat(charge * 1.7f + index * .371f, 1f);
                float side = index % 2 == 0 ? -1f : 1f;
                float drift = Mathf.Sin(index * 2.3f + phase * 1.8f);
                Vector2 point = bounds.center + new Vector2(side * bounds.width * .32f + drift * radius,
                    bounds.height * (-.22f + phase * .36f));
                Color ink = SteamInk;
                ink.a = (.09f + charge * .2f) * Mathf.Sin(phase * Mathf.PI);
                Puff(vertices, point, radius * (.35f + phase * .8f), index * 23f + phase * 12f, ink);
            }
        }

        private void DrawRelease(VertexHelper vertices, Rect bounds)
        {
            float radius = Mathf.Min(bounds.height * .19f, bounds.width * .085f);
            for (int index = 0; index < MaximumPuffs; index++)
            {
                float seed = Mathf.Repeat(index * .618034f, 1f);
                float delay = seed * .1f;
                float time = Mathf.Clamp01((release - delay) / (1f - delay));
                if (time <= 0f || time >= 1f) continue;
                float spread = 1f - (1f - time) * (1f - time);
                float side = index % 2 == 0 ? -1f : 1f;
                float size = radius * (.4f + spread * (.65f + seed * .35f));
                Vector2 point = bounds.center + new Vector2(side * bounds.width * (.2f + spread * (.14f + seed * .1f)),
                    bounds.height * (-.2f + spread * (.24f + seed * .25f)));
                // Keep the valve cloud inside its deliberately wider FX rect, away from unrelated HUD controls.
                point.x = Mathf.Clamp(point.x, bounds.xMin + size * 1.25f, bounds.xMax - size * 1.25f);
                point.y = Mathf.Clamp(point.y, bounds.yMin + size * 1.25f, bounds.yMax - size * 1.25f);
                Color ink = Color.Lerp(ReleaseInk, SteamInk, seed);
                ink.a = .62f * Mathf.Clamp01(time / .06f) * (1f - time) * (1f - time);
                Puff(vertices, point, size, side * (seed * 18f + spread * 20f), ink);
            }
        }

        private void Puff(VertexHelper vertices, Vector2 center, float radius, float angle, Color ink)
        {
            ink *= color;
            if (radius <= 0f || ink.a <= .001f) return;
            if (puffSprite != null)
            {
                float turn = angle * Mathf.Deg2Rad;
                Vector2 x = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn)) * radius;
                Vector2 y = new Vector2(-x.y, x.x);
                int start = vertices.currentVertCount;
                vertices.AddVert(center - x - y, ink, new Vector2(puffUv.x, puffUv.y));
                vertices.AddVert(center - x + y, ink, new Vector2(puffUv.x, puffUv.w));
                vertices.AddVert(center + x + y, ink, new Vector2(puffUv.z, puffUv.w));
                vertices.AddVert(center + x - y, ink, new Vector2(puffUv.z, puffUv.y));
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start, start + 2, start + 3);
                return;
            }
            // No texture allocation is needed for a standalone HUD or a missing optional steam sprite.
            int first = vertices.currentVertCount;
            vertices.AddVert(center, ink, Vector2.zero);
            Color middle = ink, edge = ink;
            middle.a *= .4f;
            edge.a = 0f;
            for (int n = 0; n < SoftPuffSegments; n++)
            {
                float turn = n * Mathf.PI * 2f / SoftPuffSegments;
                Vector2 direction = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn));
                vertices.AddVert(center + direction * radius * .52f, middle, Vector2.zero);
                vertices.AddVert(center + direction * radius, edge, Vector2.zero);
            }
            for (int n = 0; n < SoftPuffSegments; n++)
            {
                int a = first + 1 + n * 2, b = first + 1 + (n + 1) % SoftPuffSegments * 2;
                vertices.AddTriangle(first, a, b);
                vertices.AddTriangle(a, a + 1, b + 1);
                vertices.AddTriangle(a, b + 1, b);
            }
        }

        private static float SafeProgress(float value)
            => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
    }
}
