using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A thin, glowing half-circle converging onto the actor's impact target.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelStepRing : MaskableGraphic
    {
        public const float ExpectedRadius = 40f;
        public const float Travel = 20f;
        private const int Segments = 36;
        private static readonly Vector2[] LeftArc = CreateArc(true), RightArc = CreateArc(false);
        private float pixelScale = 1f;
        public float Progress { get; private set; }
        public float Radius => ExpectedRadius + Travel * (1f - Progress);
        public float WindowFraction { get; private set; }
        public float WindowStartRadius => ExpectedRadius + Travel * WindowFraction;
        public float CoreLineWidth => 1.05f;
        public bool IsLeftArc { get; private set; }
        public bool HasSuccessBand => WindowFraction > 0f;
        public bool IsTimingWindow { get; private set; }
        public bool SuccessPulse { get; private set; }
        public float GlowStrength { get; private set; } = 1.6f;
        public float PulseProgress { get; private set; }
        public float PulseRadius => ExpectedRadius + 18f / pixelScale * (1f - Mathf.Pow(1f - PulseProgress, 2f));

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Configure(float progress, bool timingWindow, bool successPulse, bool leftArc,
            float windowFraction, float scale, float glowStrength = 1.6f, float pulseProgress = 0f)
        {
            progress = float.IsNaN(progress) || float.IsInfinity(progress) ? 0f : Mathf.Clamp01(progress);
            windowFraction = float.IsNaN(windowFraction) || float.IsInfinity(windowFraction)
                ? 0f : Mathf.Clamp01(windowFraction);
            scale = float.IsNaN(scale) || float.IsInfinity(scale) ? 1f : Mathf.Max(.01f, scale);
            glowStrength = float.IsNaN(glowStrength) || float.IsInfinity(glowStrength)
                ? 1.6f : Mathf.Clamp(glowStrength, 0f, 3f);
            pulseProgress = !successPulse || float.IsNaN(pulseProgress) || float.IsInfinity(pulseProgress)
                ? 0f : Mathf.Clamp01(pulseProgress);
            if (Mathf.Approximately(Progress, progress) && IsTimingWindow == timingWindow &&
                SuccessPulse == successPulse && IsLeftArc == leftArc &&
                Mathf.Approximately(WindowFraction, windowFraction) && Mathf.Approximately(pixelScale, scale) &&
                Mathf.Approximately(GlowStrength, glowStrength) && Mathf.Approximately(PulseProgress, pulseProgress)) return;
            Progress = progress;
            IsTimingWindow = timingWindow;
            SuccessPulse = successPulse;
            IsLeftArc = leftArc;
            WindowFraction = windowFraction;
            pixelScale = scale;
            GlowStrength = glowStrength;
            PulseProgress = pulseProgress;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            float unit = 1f / pixelScale;
            bool bright = IsTimingWindow || SuccessPulse;
            // The success band is visible before the moving arc arrives, not just after a successful input.
            if (HasSuccessBand)
            {
                AddArc(vertices, (ExpectedRadius + WindowStartRadius) * .5f,
                    WindowStartRadius - ExpectedRadius, new Color(1f, 1f, 1f, bright ? .10f : .045f));
                AddArc(vertices, WindowStartRadius, .75f * unit, new Color(1f, 1f, 1f, .55f), true);
                AddTick(vertices, WindowStartRadius, unit, 7f * unit, new Color(1f, 1f, 1f, .8f));
            }
            AddArc(vertices, ExpectedRadius, 3f * unit, new Color(0f, 0f, 0f, .45f));
            AddArc(vertices, ExpectedRadius, .85f * unit, new Color(1f, 1f, 1f, .65f));
            AddTick(vertices, ExpectedRadius, unit, 7f * unit, Color.white);
            // Feathered white halos give bloom-like glow without a material per cue.
            // Stroke width stays constant when the camera zooms or the success state changes.
            AddArc(vertices, Radius, 3.4f * unit, new Color(0f, 0f, 0f, .65f));
            float pulseFade = SuccessPulse ? (1f - PulseProgress) * (1f - PulseProgress) : 1f;
            float glow = GlowStrength * pulseFade;
            AddArc(vertices, Radius, (SuccessPulse ? 30f : 14f) * unit,
                new Color(1f, 1f, 1f, (SuccessPulse ? .045f : bright ? .025f : .012f) * glow));
            AddArc(vertices, Radius, (SuccessPulse ? 20f : 9f) * unit,
                new Color(1f, 1f, 1f, (SuccessPulse ? .11f : bright ? .055f : .025f) * glow));
            AddArc(vertices, Radius, (SuccessPulse ? 8f : 5f) * unit,
                new Color(1f, 1f, 1f, (SuccessPulse ? .28f : bright ? .16f : .07f) * glow));
            AddArc(vertices, Radius, CoreLineWidth * unit, new Color(1f, 1f, 1f, bright ? 1f : .74f));
            if (SuccessPulse && GlowStrength > 0f)
            {
                // Only a hollow arc expands: the body and the rest of the screen
                // stay unobscured. Cached arc points serve every feather layer.
                AddArc(vertices, PulseRadius, 14f * unit, new Color(1f, 1f, 1f, .04f * glow));
                AddArc(vertices, PulseRadius, 6f * unit, new Color(1f, 1f, 1f, .12f * glow));
                AddArc(vertices, PulseRadius, CoreLineWidth * unit, new Color(1f, 1f, 1f, .7f * glow));
            }
        }

        private void AddArc(VertexHelper vertices, float radius, float width, Color tint, bool dashed = false)
        {
            tint *= color;
            Vector2[] arc = IsLeftArc ? LeftArc : RightArc;
            float inner = Mathf.Max(0f, radius - width / 2f), outer = radius + width / 2f;
            for (int index = 0; index < Segments; index++)
            {
                if (dashed && index % 3 == 2) continue;
                int start = vertices.currentVertCount;
                vertices.AddVert(arc[index] * inner, tint, Vector2.zero);
                vertices.AddVert(arc[index] * outer, tint, Vector2.zero);
                vertices.AddVert(arc[index + 1] * outer, tint, Vector2.zero);
                vertices.AddVert(arc[index + 1] * inner, tint, Vector2.zero);
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start + 2, start + 3, start);
            }
        }

        private void AddTick(VertexHelper vertices, float radius, float width, float height, Color tint)
        {
            float x = IsLeftArc ? -radius : radius;
            tint *= color;
            int start = vertices.currentVertCount;
            vertices.AddVert(new Vector2(x - width * .5f, -height * .5f), tint, Vector2.zero);
            vertices.AddVert(new Vector2(x - width * .5f, height * .5f), tint, Vector2.zero);
            vertices.AddVert(new Vector2(x + width * .5f, height * .5f), tint, Vector2.zero);
            vertices.AddVert(new Vector2(x + width * .5f, -height * .5f), tint, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start + 2, start + 3, start);
        }

        private static Vector2[] CreateArc(bool left)
        {
            var points = new Vector2[Segments + 1];
            for (int index = 0; index <= Segments; index++)
            {
                float angle = ((left ? 102f : -78f) + index * 156f / Segments) * Mathf.Deg2Rad;
                points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return points;
        }
    }
}
