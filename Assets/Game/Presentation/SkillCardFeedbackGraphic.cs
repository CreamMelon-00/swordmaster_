using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>One reusable card mesh for condition borders and six quiet buff sparks.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillCardFeedbackGraphic : MaskableGraphic
    {
        public const int MaximumSparks = 6;
        public const int MaximumVertices = 100;
        private static readonly Color ConditionInk = new Color32(116, 230, 219, 255);
        private static readonly Color PowerInk = new Color32(215, 197, 214, 255);
        private float phase;

        public bool ConditionTarget { get; private set; }
        public bool ConditionReady { get; private set; }
        public bool EffectActivated { get; private set; }
        public int PowerBuffPercent { get; private set; }
        public int ProtectionBuffPercent { get; private set; }
        public bool HasBuffParticles => PowerBuffPercent > 0 || ProtectionBuffPercent > 0;
        public int ActiveSparkCount => HasBuffParticles ? MaximumSparks : 0;

        public static SkillCardFeedbackGraphic Create(Transform parent, string name, float padding = 0f)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.one * padding * 2f;
            rect.anchoredPosition = Vector2.zero;
            var graphic = rect.gameObject.AddComponent<SkillCardFeedbackGraphic>();
            graphic.raycastTarget = false;
            graphic.color = Color.white;
            rect.gameObject.SetActive(false);
            return graphic;
        }

        public void SetState(bool target, bool ready, bool activated, int powerPercent = 0, int protectionPercent = 0)
        {
            powerPercent = Mathf.Max(0, powerPercent);
            protectionPercent = Mathf.Clamp(protectionPercent, 0, 100);
            bool changed = ConditionTarget != target || ConditionReady != ready || EffectActivated != activated ||
                PowerBuffPercent != powerPercent || ProtectionBuffPercent != protectionPercent;
            ConditionTarget = target;
            ConditionReady = ready;
            EffectActivated = activated;
            PowerBuffPercent = powerPercent;
            ProtectionBuffPercent = protectionPercent;
            bool visible = target || ready || activated || HasBuffParticles;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!changed) return;
            phase = 0f;
            SetVerticesDirty();
        }

        public void Tick(float realDelta)
        {
            if (!gameObject.activeSelf || realDelta <= 0f || float.IsNaN(realDelta) || float.IsInfinity(realDelta)) return;
            phase = (phase + realDelta) % 12f;
            SetVerticesDirty();
        }

        public void Clear() => SetState(false, false, false);

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width <= 4f || bounds.height <= 4f) return;
            float pulse = .5f + .5f * Mathf.Sin(phase * 2.2f);
            bool strong = ConditionReady || EffectActivated;
            Color ink = EffectActivated ? DuelVisualTheme.Accent
                : ConditionTarget || ConditionReady ? ConditionInk
                : PowerBuffPercent > 0 ? PowerInk : DuelVisualTheme.Steel;
            if (strong)
            {
                Color tint = ink;
                tint.a = .06f;
                Quad(vertices, bounds.xMin, bounds.yMin, bounds.width, bounds.height, tint);
            }
            ink.a = strong ? .92f + .06f * pulse : ConditionTarget ? .83f + .04f * pulse : .55f;
            float width = strong ? 3.5f : 2f;
            Border(vertices, bounds, width, ink);
            if (strong)
            {
                Color inner = ConditionReady ? ConditionInk : ink;
                inner.a = .8f;
                Rect inset = new Rect(bounds.xMin + 5f, bounds.yMin + 5f,
                    bounds.width - 10f, bounds.height - 10f);
                if (inset.width > 2f && inset.height > 2f) Border(vertices, inset, 1f, inner);
            }
            if (ConditionTarget || strong)
            {
                Color corner = ink;
                corner.a = .98f;
                Corners(vertices, bounds, strong ? 12f : 8f, width, corner);
            }
            if (strong)
            {
                Color marker = ConditionReady ? ConditionInk : DuelVisualTheme.Accent;
                marker.a = 1f;
                // Center on the outside corner so the fixed marker avoids the icon and text.
                Diamond(vertices, new Vector2(bounds.xMax - 1f, bounds.yMax - 1f), 9f, marker);
            }
            if (!HasBuffParticles) return;
            for (int index = 0; index < MaximumSparks; index++)
            {
                float lifetime = Mathf.Repeat(phase * .65f + index / (float)MaximumSparks, 1f);
                float angle = index * Mathf.PI * 2f / MaximumSparks + phase * .15f;
                Vector2 half = bounds.size * .5f - Vector2.one * 5f;
                Vector2 point = bounds.center + new Vector2(Mathf.Cos(angle) * half.x, Mathf.Sin(angle) * half.y);
                point.y += (lifetime - .5f) * 6f;
                Color spark = PowerBuffPercent > 0 && (ProtectionBuffPercent == 0 || index % 2 == 0)
                    ? PowerInk : DuelVisualTheme.Steel;
                spark.a = Mathf.Sin(lifetime * Mathf.PI) * .55f;
                Diamond(vertices, point, 1.4f + .7f * spark.a, spark);
            }
        }

        private static void Border(VertexHelper vertices, Rect bounds, float width, Color ink)
        {
            Quad(vertices, bounds.xMin, bounds.yMin, bounds.width, width, ink);
            Quad(vertices, bounds.xMin, bounds.yMax - width, bounds.width, width, ink);
            Quad(vertices, bounds.xMin, bounds.yMin + width, width, bounds.height - width * 2f, ink);
            Quad(vertices, bounds.xMax - width, bounds.yMin + width, width, bounds.height - width * 2f, ink);
        }

        private static void Corners(VertexHelper vertices, Rect bounds, float length, float width, Color ink)
        {
            Quad(vertices, bounds.xMin, bounds.yMin, length, width, ink);
            Quad(vertices, bounds.xMax - length, bounds.yMin, length, width, ink);
            Quad(vertices, bounds.xMin, bounds.yMax - width, length, width, ink);
            Quad(vertices, bounds.xMax - length, bounds.yMax - width, length, width, ink);
            Quad(vertices, bounds.xMin, bounds.yMin, width, length, ink);
            Quad(vertices, bounds.xMax - width, bounds.yMin, width, length, ink);
            Quad(vertices, bounds.xMin, bounds.yMax - length, width, length, ink);
            Quad(vertices, bounds.xMax - width, bounds.yMax - length, width, length, ink);
        }

        private static void Quad(VertexHelper vertices, float x, float y, float width, float height, Color ink)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(new Vector3(x, y), ink, Vector2.zero);
            vertices.AddVert(new Vector3(x + width, y), ink, Vector2.zero);
            vertices.AddVert(new Vector3(x + width, y + height), ink, Vector2.zero);
            vertices.AddVert(new Vector3(x, y + height), ink, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }

        private static void Diamond(VertexHelper vertices, Vector2 center, float radius, Color ink)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(center + Vector2.left * radius, ink, Vector2.zero);
            vertices.AddVert(center + Vector2.up * radius, ink, Vector2.zero);
            vertices.AddVert(center + Vector2.right * radius, ink, Vector2.zero);
            vertices.AddVert(center + Vector2.down * radius, ink, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }
    }
}
