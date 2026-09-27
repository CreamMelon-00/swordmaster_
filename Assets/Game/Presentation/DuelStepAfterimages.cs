using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>A fixed, prewarmed sprite pool driven by the arena's real-time clock.</summary>
    public sealed class DuelStepAfterimages : IDisposable
    {
        public const int Capacity = 12;
        public const float Lifetime = 0.22f;
        public const float InitialAlpha = 0.5f;
        private static readonly Color DodgeTint = new Color(0.62f, 0.80f, 0.91f, InitialAlpha);
        private static readonly Color PressureTint = new Color(0.93f, 0.68f, 0.37f, InitialAlpha);
        private readonly Transform root;
        private readonly View[] views = new View[Capacity];
        private int nextView;
        private bool disposed;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < views.Length; index++)
                    if (views[index].Active) count++;
                return count;
            }
        }

        public DuelStepAfterimages(Transform parent, Material sharedSpriteMaterial, int arenaLayer, bool layeredActor = false)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var rootObject = new GameObject("Duel Step Afterimages") { layer = arenaLayer };
            root = rootObject.transform;
            root.SetParent(parent, false);
            for (int index = 0; index < views.Length; index++)
            {
                var instance = new GameObject("Step Afterimage " + index) { layer = arenaLayer };
                instance.transform.SetParent(root, false);
                var renderer = instance.AddComponent<SpriteRenderer>();
                if (sharedSpriteMaterial != null) renderer.sharedMaterial = sharedSpriteMaterial;
                instance.SetActive(false);
                SpriteRenderer lowerRenderer = null;
                if (layeredActor)
                {
                    var lower = new GameObject("Lower Body") { layer = arenaLayer };
                    lower.transform.SetParent(instance.transform, false);
                    lowerRenderer = lower.AddComponent<SpriteRenderer>();
                    if (sharedSpriteMaterial != null) lowerRenderer.sharedMaterial = sharedSpriteMaterial;
                }
                views[index] = new View(instance, renderer, lowerRenderer);
            }
        }

        public void Emit(SpriteRenderer source, LegacyStepAction action, SpriteRenderer lowerSource = null)
        {
            if (disposed || source == null || source.sprite == null) return;
            View view = views[nextView];
            nextView = (nextView + 1) % views.Length;
            view.Transform.position = source.transform.position;
            view.Transform.rotation = source.transform.rotation;
            view.Transform.localScale = source.transform.localScale;
            view.Renderer.sprite = source.sprite;
            view.Renderer.flipX = source.flipX;
            view.Renderer.flipY = source.flipY;
            view.Renderer.sortingLayerID = source.sortingLayerID;
            view.Renderer.sortingOrder = source.sortingOrder - 1;
            view.Tint = action == LegacyStepAction.Pressure ? PressureTint : DodgeTint;
            view.Renderer.color = view.Tint;
            if (view.LowerRenderer != null)
            {
                view.LowerRenderer.enabled = lowerSource != null && lowerSource.enabled && lowerSource.sprite != null;
                if (view.LowerRenderer.enabled)
                {
                    view.LowerRenderer.sprite = lowerSource.sprite;
                    view.LowerRenderer.sharedMaterial = lowerSource.sharedMaterial;
                    view.LowerRenderer.flipX = lowerSource.flipX;
                    view.LowerRenderer.flipY = lowerSource.flipY;
                    view.LowerRenderer.sortingLayerID = lowerSource.sortingLayerID;
                    view.LowerRenderer.sortingOrder = lowerSource.sortingOrder - 1;
                    view.LowerRenderer.transform.localPosition = source.transform.InverseTransformPoint(lowerSource.transform.position);
                    view.LowerRenderer.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * lowerSource.transform.rotation;
                    view.LowerRenderer.transform.localScale = lowerSource.transform.localScale;
                    view.LowerRenderer.color = view.Tint;
                }
            }
            view.Age = 0f;
            view.Active = true;
            view.GameObject.SetActive(true);
        }

        public void Tick(float realDelta)
        {
            if (disposed) return;
            float delta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            for (int index = 0; index < views.Length; index++)
            {
                View view = views[index];
                if (!view.Active) continue;
                view.Age += delta;
                if (view.Age >= Lifetime)
                {
                    Deactivate(view);
                    continue;
                }
                Color color = view.Tint;
                color.a *= 1f - view.Age / Lifetime;
                view.Renderer.color = color;
                if (view.LowerRenderer != null) view.LowerRenderer.color = color;
            }
        }

        public void Reset()
        {
            for (int index = 0; index < views.Length; index++) Deactivate(views[index]);
            nextView = 0;
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
            if (root != null) Object.Destroy(root.gameObject);
        }

        private static void Deactivate(View view)
        {
            view.Active = false;
            view.Age = 0f;
            view.Renderer.sprite = null;
            if (view.LowerRenderer != null) view.LowerRenderer.sprite = null;
            view.GameObject.SetActive(false);
        }

        private sealed class View
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly SpriteRenderer Renderer;
            public readonly SpriteRenderer LowerRenderer;
            public float Age;
            public Color Tint;
            public bool Active;

            public View(GameObject gameObject, SpriteRenderer renderer, SpriteRenderer lowerRenderer)
            {
                GameObject = gameObject;
                Transform = gameObject.transform;
                Renderer = renderer;
                LowerRenderer = lowerRenderer;
            }
        }
    }
}
