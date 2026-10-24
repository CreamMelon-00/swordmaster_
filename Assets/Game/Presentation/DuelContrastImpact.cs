using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>A brief ink backdrop and flat silhouettes, independent of the fighters' materials and combat clock.</summary>
    public sealed class DuelContrastImpact : IDisposable
    {
        public const float Duration = .07f;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int AttackOffsetId = Shader.PropertyToID("_AttackOffset");
        private static readonly Color Ink = new Color(.012f, .018f, .028f, 1f);
        private static readonly Color Paper = new Color(.94f, .92f, .85f, 1f);
        private readonly Transform root;
        private readonly Sprite white;
        private readonly Material material;
        private readonly SortingGroup sortingGroup;
        private readonly SpriteRenderer backdrop;
        private readonly SpriteRenderer[] silhouettes;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private float remaining;
        private int startedFrame;
        private bool disposed;

        public bool HasRequiredAssets => !disposed && material != null && material.shader != null && material.shader.isSupported;
        public bool Active => !disposed && remaining > 0f;

        public DuelContrastImpact(Transform parent, Material silhouetteMaterial, int layer)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            material = silhouetteMaterial;
            root = new GameObject("Duel Contrast Impact") { layer = layer }.transform;
            root.SetParent(parent, false);
            sortingGroup = root.gameObject.AddComponent<SortingGroup>();
            sortingGroup.sortingOrder = 32000;
            white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
            white.name = "Contrast Impact White (Runtime)";
            backdrop = Create("Impact Backdrop", layer, 0);
            backdrop.sprite = white;
            SetTint(backdrop, Ink);
            silhouettes = new[]
            {
                Create("Player Silhouette", layer, 2), Create("Player Lower Silhouette", layer, 1),
                Create("Enemy Silhouette", layer, 4), Create("Enemy Lower Silhouette", layer, 3),
            };
            foreach (var copy in silhouettes) SetTint(copy, Paper);
            root.gameObject.SetActive(false);
        }

        public bool Show()
        {
            if (!HasRequiredAssets) return false;
            remaining = Duration;
            startedFrame = Time.frameCount;
            root.gameObject.SetActive(true);
            return true;
        }

        /// <summary>The triggering frame is always drawn, including when a long rules tick produced the hit.</summary>
        public void Advance(float realDelta)
        {
            if (!Active || Time.frameCount == startedFrame || !(realDelta > 0f) || float.IsInfinity(realDelta)) return;
            remaining = Mathf.Max(0f, remaining - realDelta);
            if (remaining <= 0f) Clear();
        }

        public void Refresh(Camera camera, SpriteRenderer player, SpriteRenderer playerLower,
            SpriteRenderer enemy, SpriteRenderer enemyLower, bool playerInFront)
        {
            if (!Active || camera == null) return;
            // The group renders above every arena figure. Within it, retain the fighters' current depth order.
            sortingGroup.sortingLayerID = player.sortingLayerID;
            backdrop.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 2.5f,
                camera.transform.rotation);
            SetWorldScale(backdrop.transform, new Vector3(camera.orthographicSize * camera.aspect * 2.1f,
                camera.orthographicSize * 2.1f, 1f));
            Copy(silhouettes[0], player, camera, playerInFront ? 4 : 2);
            Copy(silhouettes[1], playerLower, camera, playerInFront ? 3 : 1);
            Copy(silhouettes[2], enemy, camera, playerInFront ? 2 : 4);
            Copy(silhouettes[3], enemyLower, camera, playerInFront ? 1 : 3);
        }

        public void Clear()
        {
            if (disposed) return;
            remaining = 0f;
            root.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear();
            disposed = true;
            Object.Destroy(root.gameObject);
            Object.Destroy(white);
        }

        private SpriteRenderer Create(string name, int layer, int order)
        {
            var item = new GameObject(name) { layer = layer };
            item.transform.SetParent(root, false);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Copy(SpriteRenderer copy, SpriteRenderer source, Camera camera, int order)
        {
            bool visible = source != null && source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
            copy.enabled = visible;
            if (!visible) return;
            source.GetPropertyBlock(properties);
            Vector4 offset = properties.GetVector(AttackOffsetId);
            Vector3 position = source.transform.TransformPoint(new Vector3(offset.x, offset.y, offset.z));
            // Pull only along the camera's depth axis; the screen position and size remain those of the live pose.
            position += camera.transform.forward * (2f - Vector3.Dot(position - camera.transform.position, camera.transform.forward));
            copy.transform.SetPositionAndRotation(position, source.transform.rotation);
            // BreakSilhouette also draws ordinary meshes and does not read URP's sprite flip properties.
            // Bake only the sprite's geometry flip into this copy; AttackSprite adds its lunge after the flip.
            Vector3 scale = source.transform.lossyScale;
            scale.x *= source.flipX ? -1f : 1f;
            scale.y *= source.flipY ? -1f : 1f;
            SetWorldScale(copy.transform, scale);
            copy.sprite = source.sprite;
            copy.flipX = copy.flipY = false;
            copy.sortingOrder = order;
        }

        private void SetTint(SpriteRenderer renderer, Color color)
        {
            properties.Clear();
            properties.SetColor(TintId, color);
            renderer.SetPropertyBlock(properties);
        }

        private static void SetWorldScale(Transform target, Vector3 scale)
        {
            Vector3 parentScale = target.parent.lossyScale;
            target.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
        }
    }
}
