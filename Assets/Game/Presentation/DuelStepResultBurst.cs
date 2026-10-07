using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A hollow, actor-centred impulse for a judged dodge or pressure step.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelStepResultBurst : MaskableGraphic
    {
        private const int RingSegments = 48;
        private static readonly Color CoolSuccess = new Color(.77f, .94f, 1f, 1f);
        private static readonly Color WarmSuccess = new Color(1f, .70f, .31f, 1f);
        private static readonly Color FailureRed = new Color(1f, .17f, .13f, 1f);
        private static readonly Color FailureDark = new Color(.57f, .035f, .045f, 1f);

        private bool success;
        private int streakTier;
        private float progress = 1f;

        public bool Success => success;
        public int StreakTier => streakTier;
        public float Progress => progress;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <param name="normalizedAge">Zero at judgment, one when the impulse is finished.</param>
        public void Configure(bool success, int streak, float normalizedAge)
        {
            int nextTier = success ? Mathf.Clamp(streak, 1, 5) : 0;
            float nextProgress = float.IsNaN(normalizedAge) || float.IsInfinity(normalizedAge)
                ? 1f : Mathf.Clamp01(normalizedAge);
            if (this.success == success && streakTier == nextTier && Mathf.Approximately(progress, nextProgress))
                return;
            this.success = success;
            streakTier = nextTier;
            progress = nextProgress;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (progress >= 1f) return;
            float ease = 1f - (1f - progress) * (1f - progress);
            float fade = (1f - progress) * (1f - progress) * color.a;
            if (success) DrawSuccess(vertices, ease, fade);
            else DrawFailure(vertices, ease, fade);
        }

        private void DrawSuccess(VertexHelper vertices, float ease, float fade)
        {
            int tier = Mathf.Max(1, streakTier);
            Color ink = Color.Lerp(CoolSuccess, WarmSuccess, (tier - 1f) / 4f);
            float radius = 48f + 32f * ease;
            float strength = .68f + tier * .105f;
            AddRing(vertices, radius, 9f + tier * .8f, WithAlpha(ink, .075f * strength * fade));
            AddRing(vertices, radius, 1.6f + tier * .16f, WithAlpha(ink, .72f * strength * fade));
            if (tier >= 3)
            {
                float echo = Mathf.Clamp01((progress - .08f) / .92f);
                AddRing(vertices, 48f + 25f * echo, 1.1f,
                    WithAlpha(ink, (.12f + tier * .035f) * fade));
            }

            int rayCount = 4 + tier * 3;
            float rayLength = (5f + tier * 2.2f) * (1f - .25f * progress);
            for (int index = 0; index < rayCount; index++)
            {
                // Every other ray is shorter, keeping a small burst readable without a solid disc.
                float angle = (index + .25f) * Mathf.PI * 2f / rayCount;
                float length = index % 2 == 0 ? rayLength : rayLength * .62f;
                AddRay(vertices, angle, radius + 4f, radius + 4f + length,
                    1.1f + tier * .13f, WithAlpha(ink, (.27f + tier * .065f) * fade));
            }
        }

        private void DrawFailure(VertexHelper vertices, float ease, float fade)
        {
            float radius = 50f + 27f * ease;
            Color red = WithAlpha(FailureRed, .87f * fade);
            Color shadow = WithAlpha(FailureDark, .16f * fade);
            // Missing sections and uneven shard lengths read as a broken judgment ring.
            for (int index = 0; index < 18; index++)
            {
                if (index % 5 == 3) continue;
                float start = (index + .12f) * Mathf.PI * 2f / 18f;
                float end = (index + (index % 3 == 0 ? .77f : .9f)) * Mathf.PI * 2f / 18f;
                AddArc(vertices, radius, 3.2f, start, end, shadow);
                AddArc(vertices, radius, 1.8f, start, end, red);
            }
            for (int index = 0; index < 9; index++)
            {
                float angle = (index + .38f) * Mathf.PI * 2f / 9f;
                float inner = radius + 4f;
                float outer = Mathf.Min(98f, inner + (index % 3 == 0 ? 17f : 10f) * (1f - .25f * progress));
                AddShard(vertices, angle, inner, outer, index % 2 == 0 ? 3f : 2f,
                    WithAlpha(FailureRed, (index % 3 == 0 ? .56f : .38f) * fade));
            }
        }

        private static void AddRing(VertexHelper vertices, float radius, float width, Color tint)
        {
            for (int index = 0; index < RingSegments; index++)
            {
                float start = index * Mathf.PI * 2f / RingSegments;
                float end = (index + 1) * Mathf.PI * 2f / RingSegments;
                AddArc(vertices, radius, width, start, end, tint);
            }
        }

        private static void AddArc(VertexHelper vertices, float radius, float width, float start, float end, Color tint)
        {
            float inner = radius - width * .5f, outer = radius + width * .5f;
            Vector2 a = Direction(start), b = Direction(end);
            int first = vertices.currentVertCount;
            vertices.AddVert(a * inner, tint, Vector2.zero);
            vertices.AddVert(a * outer, tint, Vector2.zero);
            vertices.AddVert(b * outer, tint, Vector2.zero);
            vertices.AddVert(b * inner, tint, Vector2.zero);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first + 2, first + 3, first);
        }

        private static void AddRay(VertexHelper vertices, float angle, float inner, float outer,
            float halfWidth, Color tint)
        {
            Vector2 direction = Direction(angle);
            Vector2 side = new Vector2(-direction.y, direction.x) * halfWidth;
            int first = vertices.currentVertCount;
            vertices.AddVert(direction * inner - side, tint, Vector2.zero);
            vertices.AddVert(direction * inner + side, tint, Vector2.zero);
            vertices.AddVert(direction * outer + side, tint, Vector2.zero);
            vertices.AddVert(direction * outer - side, tint, Vector2.zero);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first + 2, first + 3, first);
        }

        private static void AddShard(VertexHelper vertices, float angle, float inner, float outer,
            float halfWidth, Color tint)
        {
            Vector2 direction = Direction(angle);
            Vector2 side = new Vector2(-direction.y, direction.x) * halfWidth;
            int first = vertices.currentVertCount;
            vertices.AddVert(direction * inner - side, tint, Vector2.zero);
            vertices.AddVert(direction * inner + side, tint, Vector2.zero);
            vertices.AddVert(direction * outer, WithAlpha(tint, tint.a * .3f), Vector2.zero);
            vertices.AddTriangle(first, first + 1, first + 2);
        }

        private static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        private static Color WithAlpha(Color source, float alpha)
        {
            source.a = Mathf.Clamp01(alpha);
            return source;
        }
    }

    /// <summary>A short danger tint confined to the screen edges after a failed step.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelStepFailureEdge : MaskableGraphic
    {
        private float progress = 1f;

        public float Progress => progress;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <param name="normalizedAge">Zero at failure, one when the flash has faded.</param>
        public void SetProgress(float normalizedAge)
        {
            float next = float.IsNaN(normalizedAge) || float.IsInfinity(normalizedAge)
                ? 1f : Mathf.Clamp01(normalizedAge);
            if (Mathf.Approximately(progress, next)) return;
            progress = next;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (progress >= 1f) return;
            Rect bounds = rectTransform.rect;
            float width = Mathf.Min(168f, bounds.width * .12f);
            if (width <= 0f || bounds.height <= 0f) return;
            float fade = (1f - progress) * (1f - progress) * color.a;
            AddSide(vertices, bounds.xMin, bounds.xMin + width, bounds, .48f * fade);
            AddSide(vertices, bounds.xMax, bounds.xMax - width, bounds, .4f * fade);
        }

        private static void AddSide(VertexHelper vertices, float outside, float inside, Rect bounds, float alpha)
        {
            Color edge = new Color(1f, .07f, .055f, alpha);
            Color transparent = new Color(.62f, .025f, .035f, 0f);
            int first = vertices.currentVertCount;
            vertices.AddVert(new Vector2(outside, bounds.yMin), edge, Vector2.zero);
            vertices.AddVert(new Vector2(outside, bounds.yMax), edge, Vector2.up);
            vertices.AddVert(new Vector2(inside, bounds.yMax), transparent, Vector2.one);
            vertices.AddVert(new Vector2(inside, bounds.yMin), transparent, Vector2.right);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first + 2, first + 3, first);
        }
    }
}
