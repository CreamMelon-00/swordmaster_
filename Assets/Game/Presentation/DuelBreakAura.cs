using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Stays on a fighter while its resistance is broken and incoming HP damage is doubled:
    /// a pulsing red pixel outline behind the body and a cracked ring on its ground shadow.
    /// It owns separate renderers, so the actor's colour, hit flash and sorting are untouched.
    /// The look is placeholder art drawn in code; replace it here.</summary>
    public sealed class DuelBreakAura : IDisposable
    {
        /// <summary>Two pixels of the 40 PPU fighter art.</summary>
        public const float OutlineThickness = 0.05f;
        public const float FadeDuration = 0.15f;
        public const float PulsePeriod = 0.9f;
        public const int RingSegments = 28;
        // Behind the lower body (-1) and upper body (0); tied with the ground shadow (-2)
        // but nearer the camera, so the outline and ring draw over the shadow.
        public const int SortingOrder = -2;
        public static readonly Color Tint = new Color(1f, 0.24f, 0.14f, 1f);
        private const float OutlineDepth = -0.01f;
        private const float RingDepth = -0.005f;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly Vector3[] OutlineOffsets =
        {
            new Vector3(OutlineThickness, 0f, OutlineDepth), new Vector3(-OutlineThickness, 0f, OutlineDepth),
            new Vector3(0f, OutlineThickness, OutlineDepth), new Vector3(0f, -OutlineThickness, OutlineDepth),
        };

        private readonly SpriteRenderer upperSource, lowerSource;
        private readonly SpriteRenderer[] upperOutline, lowerOutline;
        private readonly MeshRenderer ring;
        private readonly Mesh ringMesh;
        private readonly MaterialPropertyBlock ringProperties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock outlineProperties = new MaterialPropertyBlock();
        private float pulseTime;
        private bool disposed;

        /// <summary>Whether the fighter is broken; the visuals fade toward this state.</summary>
        public bool IsBroken { get; private set; }
        /// <summary>Current fade, from 0 (hidden) to 1 (fully shown).</summary>
        public float Intensity { get; private set; }
        public bool IsVisible => HasRequiredAssets && Intensity > 0f;
        public bool HasRequiredAssets { get; }
        public int OutlineRendererCount => (upperOutline?.Length ?? 0) + (lowerOutline?.Length ?? 0);

        /// <param name="lower">The separate lower-body renderer of a layered actor, or null.</param>
        /// <param name="ground">The ground shadow the cracked ring lies on.</param>
        /// <param name="material">Shared material using the TurnLimbo/Duel Break Silhouette shader.</param>
        public DuelBreakAura(SpriteRenderer upper, SpriteRenderer lower, SpriteRenderer ground, Material material, int layer)
        {
            upperSource = upper != null ? upper : throw new ArgumentNullException(nameof(upper));
            lowerSource = lower;
            HasRequiredAssets = material != null && material.shader != null && material.shader.isSupported;
            if (!HasRequiredAssets) return;
            upperOutline = CreateOutline(upper, material, layer);
            if (lower != null) lowerOutline = CreateOutline(lower, material, layer);
            if (ground != null)
            {
                var ringObject = new GameObject("Break Ring") { layer = layer };
                ringObject.transform.SetParent(ground.transform, false);
                ringObject.transform.localPosition = new Vector3(0f, 0f, RingDepth);
                float radius = ground.sprite != null ? Mathf.Max(ground.sprite.bounds.extents.x, ground.sprite.bounds.extents.y) : 0.5f;
                ringMesh = CreateCrackedRing(radius > 0f ? radius : 0.5f);
                ringObject.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                ring = ringObject.AddComponent<MeshRenderer>();
                ring.sharedMaterial = material;
                ring.sortingLayerID = ground.sortingLayerID;
                ring.sortingOrder = SortingOrder;
                ring.shadowCastingMode = ShadowCastingMode.Off;
                ring.receiveShadows = false;
                ring.lightProbeUsage = LightProbeUsage.Off;
                ring.reflectionProbeUsage = ReflectionProbeUsage.Off;
                ring.allowOcclusionWhenDynamic = false;
            }
            Apply();
        }

        public void SetBroken(bool broken)
        {
            IsBroken = broken;
            Apply();
        }

        /// <summary>Advances the fade and pulse on the combat clock: hit stop freezes it, slow motion slows it.</summary>
        public void Tick(float delta)
        {
            if (disposed) return;
            delta = delta > 0f && !float.IsInfinity(delta) ? delta : 0f;
            Intensity = Mathf.MoveTowards(Intensity, IsBroken ? 1f : 0f, delta / FadeDuration);
            pulseTime = Intensity > 0f ? (pulseTime + delta) % PulsePeriod : 0f;
            Apply();
        }

        public void Reset()
        {
            IsBroken = false;
            Intensity = pulseTime = 0f;
            Apply();
        }

        /// <summary>Copies the actor's current frames and applies the fade; call after the actors are sampled.</summary>
        public void Apply()
        {
            if (disposed || !HasRequiredAssets) return;
            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(pulseTime / PulsePeriod * 2f * Mathf.PI));
            Color color = Tint;
            color.a = Intensity * pulse;
            bool visible = Intensity > 0f;
            Follow(upperOutline, upperSource, visible, color, outlineProperties);
            Follow(lowerOutline, lowerSource, visible, color, outlineProperties);
            if (ring == null) return;
            ring.enabled = visible;
            if (!visible) return;
            ringProperties.SetColor(TintId, new Color(color.r, color.g, color.b, color.a * 0.9f));
            ring.SetPropertyBlock(ringProperties);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // The renderers are children of the actor and go with the arena; only the mesh is owned here.
            if (ringMesh != null) Object.Destroy(ringMesh);
        }

        private static void Follow(SpriteRenderer[] outline, SpriteRenderer source, bool visible, Color color,
            MaterialPropertyBlock properties)
        {
            if (outline == null) return;
            bool shown = visible && source != null && source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
            foreach (SpriteRenderer copy in outline)
            {
                copy.enabled = shown;
                if (!shown) continue;
                copy.sprite = source.sprite;
                copy.flipX = source.flipX;
                copy.flipY = source.flipY;
                // URP 17 hands SpriteRenderer.color to its own sprite shaders per draw, not as vertex
                // colour, so the tint travels in the renderer's property block (keeping its sprite texture).
                copy.GetPropertyBlock(properties);
                properties.SetColor(TintId, color);
                copy.SetPropertyBlock(properties);
            }
        }

        /// <summary>The colour an outline copy is drawn with.</summary>
        public static Color OutlineColor(SpriteRenderer copy)
        {
            var properties = new MaterialPropertyBlock();
            copy.GetPropertyBlock(properties);
            return properties.GetColor(TintId);
        }

        private static SpriteRenderer[] CreateOutline(SpriteRenderer source, Material material, int layer)
        {
            var copies = new SpriteRenderer[OutlineOffsets.Length];
            for (int i = 0; i < copies.Length; i++)
            {
                var copyObject = new GameObject("Break Outline " + i) { layer = layer };
                copyObject.transform.SetParent(source.transform, false);
                copyObject.transform.localPosition = OutlineOffsets[i];
                var copy = copyObject.AddComponent<SpriteRenderer>();
                copy.sharedMaterial = material;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = SortingOrder;
                copy.enabled = false;
                copies[i] = copy;
            }
            return copies;
        }

        /// <summary>A jagged ring with a few gaps, in the shadow's own (squashed) space.</summary>
        private static Mesh CreateCrackedRing(float radius)
        {
            var vertices = new System.Collections.Generic.List<Vector3>(RingSegments * 4);
            var triangles = new System.Collections.Generic.List<int>(RingSegments * 6);
            for (int i = 0; i < RingSegments; i++)
            {
                // Fixed gaps read as cracks; a stable jitter keeps the edge uneven.
                if (i % 7 == 3 || i % 9 == 6) continue;
                float start = (i + 0.08f) / RingSegments * 2f * Mathf.PI;
                float end = (i + 0.92f) / RingSegments * 2f * Mathf.PI;
                float outer = radius * (1.02f + 0.07f * Mathf.Sin(i * 2.7f));
                float inner = radius * (0.84f + 0.05f * Mathf.Sin(i * 1.9f + 1f));
                int first = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(start) * inner, Mathf.Sin(start) * inner, 0f));
                vertices.Add(new Vector3(Mathf.Cos(start) * outer, Mathf.Sin(start) * outer, 0f));
                vertices.Add(new Vector3(Mathf.Cos(end) * inner, Mathf.Sin(end) * inner, 0f));
                vertices.Add(new Vector3(Mathf.Cos(end) * outer, Mathf.Sin(end) * outer, 0f));
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first + 2); triangles.Add(first + 1); triangles.Add(first + 3);
            }
            var mesh = new Mesh { name = "Duel Break Ring" };
            mesh.SetVertices(vertices);
            var colors = new Color[vertices.Count];
            var uvs = new Vector2[vertices.Count];
            for (int i = 0; i < colors.Length; i++) { colors[i] = Color.white; uvs[i] = new Vector2(0.5f, 0.5f); }
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
