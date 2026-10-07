using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public enum DuelSkillBurstStyle { Shield, Strike, Recovery, Danger, LevelUp, Break }

    /// <summary>Short, hollow actor-side flash for skill feedback.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelSkillActivationBurst : MaskableGraphic
    {
        private static readonly float[] BreakArcStarts = { 12f, 104f, 194f, 283f };
        private static readonly float[] BreakArcEnds = { 73f, 164f, 254f, 344f };
        private float progress = 1f;
        private DuelSkillBurstStyle style;

        public float Progress => progress;
        public DuelSkillBurstStyle Style => style;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Configure(Color tint, DuelSkillBurstStyle nextStyle, float normalizedAge)
        {
            float next = float.IsNaN(normalizedAge) || float.IsInfinity(normalizedAge)
                ? 1f : Mathf.Clamp01(normalizedAge);
            if (color == tint && style == nextStyle && Mathf.Approximately(progress, next)) return;
            color = tint;
            style = nextStyle;
            progress = next;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (progress >= 1f) return;

            float ease = 1f - (1f - progress) * (1f - progress) * (1f - progress);
            float fade = (1f - progress) * (1f - progress);
            float radius = style == DuelSkillBurstStyle.Break ? 57f + 68f * ease : 30f + 55f * ease;
            Color halo = color;
            halo.a *= .32f * fade;
            Color stroke = color;
            stroke.a *= fade;
            Color glint = Color.Lerp(color, Color.white, .65f);
            glint.a = .95f * fade;

            if (style == DuelSkillBurstStyle.Break)
            {
                // Four separated arcs and flying splinters read as a resistance shell cracking, without filling
                // the actor's silhouette. The real-time pulse starts bright even while the combat clock is stopped.
                for (int index = 0; index < BreakArcStarts.Length; index++)
                {
                    AddArc(vertices, radius, 17f, BreakArcStarts[index], BreakArcEnds[index], 9, halo);
                    AddArc(vertices, radius, 3.8f, BreakArcStarts[index], BreakArcEnds[index], 9, stroke);
                }
                for (int index = 0; index < 8; index++)
                {
                    float angle = 21f + index * 45f;
                    AddRay(vertices, angle, radius + 9f, radius + 29f + (index % 2 == 0 ? 8f : 0f),
                        6f, .9f, index % 2 == 0 ? glint : stroke);
                }
            }
            else if (style == DuelSkillBurstStyle.LevelUp)
            {
                // A short expanding crown stays hollow: the actor remains visible through its centre.
                AddArc(vertices, radius + 5f, 12f, 0f, 360f, 36, halo);
                AddArc(vertices, radius + 5f, 3f, 0f, 360f, 36, stroke);
                AddArc(vertices, radius - 8f, 1.5f, 35f, 145f, 12, glint);
                AddArc(vertices, radius - 8f, 1.5f, 215f, 325f, 12, glint);
                for (int index = 0; index < 8; index++)
                {
                    float angle = 22.5f + index * 45f;
                    AddRay(vertices, angle, radius + 11f, radius + 30f, 4.5f, .8f, stroke);
                }
            }
            else if (style == DuelSkillBurstStyle.Danger)
            {
                // Two broken, pointed arcs pull the eye toward the enemy without covering the sprite.
                AddArc(vertices, radius, 12f, -77f, -18f, 9, halo);
                AddArc(vertices, radius, 12f, 18f, 77f, 9, halo);
                AddArc(vertices, radius, 3.6f, -77f, -18f, 9, stroke);
                AddArc(vertices, radius, 3.6f, 18f, 77f, 9, stroke);
                for (int index = -2; index <= 2; index++)
                {
                    float angle = index * 27f;
                    AddRay(vertices, angle, radius + 4f, radius + 22f + (index == 0 ? 11f : 0f),
                        7f, 1.6f, stroke);
                    AddRay(vertices, angle, radius + 11f, radius + 19f + (index == 0 ? 11f : 0f),
                        1.4f, .5f, glint);
                }
            }
            else if (style == DuelSkillBurstStyle.Shield)
            {
                // A broad outer shield arc leaves the fighter's body readable.
                AddArc(vertices, radius, 13f, 112f, 248f, 22, halo);
                AddArc(vertices, radius, 4.3f, 112f, 248f, 22, stroke);
                AddArc(vertices, radius - 9f, 2.4f, 130f, 230f, 16, glint);
                for (int index = -2; index <= 2; index++)
                    AddRay(vertices, 180f + index * 24f, radius + 5f, radius + 17f,
                        4.2f, 1.1f, glint);
            }
            else if (style == DuelSkillBurstStyle.Strike)
            {
                // Direct resistance loss reads as two quick diagonal cuts outside the attacker.
                float travel = 22f * ease;
                AddLine(vertices, new Vector2(-61f - travel, -37f),
                    new Vector2(-20f - travel, 43f), 10f, halo);
                AddLine(vertices, new Vector2(-61f - travel, -37f),
                    new Vector2(-20f - travel, 43f), 3.5f, stroke);
                AddLine(vertices, new Vector2(-34f - travel, -39f),
                    new Vector2(7f - travel, 41f), 8f, halo);
                AddLine(vertices, new Vector2(-34f - travel, -39f),
                    new Vector2(7f - travel, 41f), 2f, glint);
            }
            else
            {
                // A few rising diamonds signal real restoration without filling the sprite.
                for (int index = -1; index <= 1; index++)
                {
                    float x = -36f + index * 22f;
                    float y = -25f + 53f * ease + (index == 0 ? 8f : 0f);
                    float size = index == 0 ? 7f : 5f;
                    AddDiamond(vertices, new Vector2(x, y), size + 7f, halo);
                    AddDiamond(vertices, new Vector2(x, y), size, glint);
                    AddLine(vertices, new Vector2(x, y - 19f), new Vector2(x, y - 8f), 2f, stroke);
                }
            }
        }

        private static void AddLine(VertexHelper vertices, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 cross = new Vector2(-direction.y, direction.x) * (width * .5f);
            AddQuad(vertices, from - cross, from + cross, to + cross, to - cross, tint);
        }

        private static void AddDiamond(VertexHelper vertices, Vector2 center, float radius, Color tint)
        {
            AddQuad(vertices, center + Vector2.up * radius, center + Vector2.right * radius,
                center - Vector2.up * radius, center - Vector2.right * radius, tint);
        }

        private static void AddArc(VertexHelper vertices, float radius, float width,
            float firstAngle, float lastAngle, int segments, Color tint)
        {
            float inner = radius - width * .5f, outer = radius + width * .5f;
            for (int index = 0; index < segments; index++)
            {
                float a = Mathf.Lerp(firstAngle, lastAngle, (float)index / segments) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(firstAngle, lastAngle, (float)(index + 1) / segments) * Mathf.Deg2Rad;
                Vector2 from = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 to = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                AddQuad(vertices, from * inner, from * outer, to * outer, to * inner, tint);
            }
        }

        private static void AddRay(VertexHelper vertices, float angle, float inner, float outer,
            float baseWidth, float tipWidth, Color tint)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 cross = new Vector2(-direction.y, direction.x);
            AddQuad(vertices,
                direction * inner - cross * (baseWidth * .5f),
                direction * inner + cross * (baseWidth * .5f),
                direction * outer + cross * (tipWidth * .5f),
                direction * outer - cross * (tipWidth * .5f), tint);
        }

        private static void AddQuad(VertexHelper vertices, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(a, tint, Vector2.zero);
            vertices.AddVert(b, tint, Vector2.zero);
            vertices.AddVert(c, tint, Vector2.zero);
            vertices.AddVert(d, tint, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start + 2, start + 3, start);
        }
    }
}
