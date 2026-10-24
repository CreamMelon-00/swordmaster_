using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Six faint background streaks in one reusable mesh. Does not alter actors, cameras or shared materials.</summary>
    public sealed class DuelReengageSpeedLines : IDisposable
    {
        public const int StreakCount = 6;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private readonly Transform root;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private readonly Vector3[] vertices = new Vector3[StreakCount * 4];
        private readonly Color32[] colors = new Color32[StreakCount * 4];
        private float clock;
        private bool disposed;

        public bool Visible => !disposed && renderer.enabled;
        public bool HasRequiredAssets => !disposed && material != null && material.shader != null && material.shader.isSupported;

        public DuelReengageSpeedLines(Transform parent, Material sharedMaterial, int layer)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            material = sharedMaterial;
            root = new GameObject("Duel Reengage Speed Lines") { layer = layer }.transform;
            root.SetParent(parent, false);
            mesh = new Mesh { name = "Reengage Streaks (Runtime)" };
            mesh.MarkDynamic();
            var uv = new Vector2[vertices.Length];
            var triangles = new int[StreakCount * 6];
            for (int i = 0; i < StreakCount; i++)
            {
                int v = i * 4, t = i * 6;
                uv[v] = new Vector2(0f, 0f); uv[v + 1] = new Vector2(0f, 1f);
                uv[v + 2] = new Vector2(1f, 1f); uv[v + 3] = new Vector2(1f, 0f);
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = root.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = -4;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor(TintId, Color.white);
            properties.SetTexture(MainTexId, Texture2D.whiteTexture);
            renderer.SetPropertyBlock(properties);
            Clear();
        }

        public void Tick(Camera camera, Vector3 groundCenter, int direction, float strength, float realDelta, int sortingLayerId)
        {
            if (!HasRequiredAssets || camera == null || !camera.orthographic || direction == 0 ||
                !(strength > 0f) || !Finite(strength) || !(realDelta > 0f) || !Finite(realDelta) ||
                !(camera.orthographicSize > 0f) || !Finite(camera.orthographicSize) ||
                !(camera.aspect > 0f) || !Finite(camera.aspect))
            {
                Clear();
                return;
            }
            strength = Mathf.Clamp01(strength);
            clock = (clock + realDelta * 9f) % 1f;
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            // Keep the lines in the upper battle background, above the lower queue panels.
            Vector3 viewport = camera.WorldToViewportPoint(groundCenter + Vector3.up * 1.5f);
            viewport.x = .5f;
            viewport.y = Mathf.Clamp(viewport.y, .52f, .68f);
            viewport.z = Mathf.Max(camera.nearClipPlane + .1f, viewport.z);
            root.SetPositionAndRotation(camera.ViewportToWorldPoint(viewport), camera.transform.rotation);
            Vector3 parentScale = root.parent.lossyScale;
            root.localScale = new Vector3(1f / SafeScale(parentScale.x), 1f / SafeScale(parentScale.y), 1f / SafeScale(parentScale.z));
            renderer.sortingLayerID = sortingLayerId;
            float length = Mathf.Clamp(halfWidth * .18f, .55f, 2.2f);
            float thickness = Mathf.Clamp(halfHeight * .0024f, .012f, .026f);
            for (int i = 0; i < StreakCount; i++)
            {
                float phase = (clock + i * .173f) % 1f;
                float x = (1f - phase * 2f) * halfWidth * direction;
                float y = (i - 2.5f) * halfHeight * .105f;
                float halfThickness = thickness * (.65f + i % 3 * .18f);
                int v = i * 4;
                vertices[v] = new Vector3(x, y - halfThickness, 0f);
                vertices[v + 1] = new Vector3(x, y + halfThickness, 0f);
                vertices[v + 2] = new Vector3(x + length * direction, y + halfThickness, 0f);
                vertices[v + 3] = new Vector3(x + length * direction, y - halfThickness, 0f);
                float fade = Mathf.Sin(phase * Mathf.PI);
                byte alpha = (byte)(255f * .23f * strength * fade);
                colors[v] = colors[v + 1] = new Color32(220, 231, 217, 0);
                colors[v + 2] = colors[v + 3] = new Color32(220, 231, 217, alpha);
            }
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.RecalculateBounds();
            renderer.enabled = true;
        }

        public void Clear()
        {
            if (disposed) return;
            renderer.enabled = false;
            clock = 0f;
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear();
            disposed = true;
            Object.Destroy(root.gameObject);
            Object.Destroy(mesh);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float SafeScale(float value) => Mathf.Abs(value) > .0001f ? value : 1f;
    }
}
