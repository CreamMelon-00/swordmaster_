using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Short real-time HDR flashes layered over the inherited impact particles.</summary>
    public sealed class DuelImpactGlow : IDisposable
    {
        private const int MaximumViews = 16;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly Color WarmImpact = new Color(1f, 0.28f, 0.06f, 1f);
        private static readonly Color GuardImpact = new Color(0.05f, 1f, 0.9f, 1f);
        private static readonly Color FatalImpact = new Color(1f, 0.82f, 0.3f, 1f);

        private readonly Transform root;
        private readonly DuelPresentationSettings settings;
        private readonly Shader shader;
        private readonly Mesh quad;
        private readonly Material material;
        private readonly int arenaLayer;
        private readonly List<View> views = new List<View>(MaximumViews);
        private uint emissionSequence;
        private bool disposed;

        public int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var view in views)
                    if (view.Active) count++;
                return count;
            }
        }

        public bool HasRequiredAssets => !disposed && shader != null && shader.isSupported &&
            quad != null && material != null;

        /// <summary>Keeps every glow off screen without ending it, while a cutscene plays in the middle of a battle.</summary>
        public bool Hidden
        {
            get => !disposed && !root.gameObject.activeSelf;
            set { if (!disposed) root.gameObject.SetActive(!value); }
        }

        public DuelImpactGlow(Transform parent, int arenaLayer, DuelPresentationSettings settings)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            this.arenaLayer = arenaLayer;

            var rootObject = new GameObject("Duel Impact Glow") { layer = arenaLayer };
            root = rootObject.transform;
            root.SetParent(parent, false);

            shader = Resources.Load<Shader>("DuelVFX/ImpactGlow");
            quad = CreateQuad();
            if (shader != null)
                material = new Material(shader) { name = "Duel Impact Glow (Runtime)" };
        }

        public void Emit(Vector3 worldPosition, bool guarded, bool fatal)
        {
            if (disposed) return;
            if (!CanRender())
            {
                Reset();
                return;
            }
            if (!HasRequiredAssets) return;

            View view = AcquireView();
            view.Age = 0f;
            view.Tint = fatal ? FatalImpact : guarded ? GuardImpact : WarmImpact;
            view.Transform.position = worldPosition;
            view.Transform.rotation = Quaternion.Euler(0f, 0f, (emissionSequence++ % 8u) * 22.5f);
            view.GameObject.SetActive(true);
            view.Active = true;
            ApplyLiveSettings(view, 0f);
        }

        /// <summary>Advances on real time so slow motion and hit stop cannot leave the screen covered.</summary>
        public void Tick(float realDelta)
        {
            if (disposed) return;
            if (!CanRender())
            {
                Reset();
                return;
            }

            float delta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            float duration = settings.GlowDuration;
            foreach (var view in views)
            {
                if (!view.Active) continue;
                view.Age += delta;
                if (view.Age >= duration)
                {
                    Deactivate(view);
                    continue;
                }
                ApplyLiveSettings(view, view.Age / duration);
            }
        }

        public void Reset()
        {
            foreach (var view in views) Deactivate(view);
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
            if (root != null) Object.Destroy(root.gameObject);
            if (material != null) Object.Destroy(material);
            if (quad != null) Object.Destroy(quad);
        }

        private bool CanRender() => settings.GlowEnabled && settings.GlowIntensity > 0f;

        private View AcquireView()
        {
            foreach (var view in views)
                if (!view.Active) return view;

            if (views.Count < MaximumViews)
            {
                var created = CreateView(views.Count);
                views.Add(created);
                return created;
            }

            // A seventeenth simultaneous hit replaces the oldest flash without growing the pool.
            View oldest = views[0];
            for (int index = 1; index < views.Count; index++)
                if (views[index].Age > oldest.Age) oldest = views[index];
            return oldest;
        }

        private View CreateView(int index)
        {
            var instance = new GameObject("Impact Glow " + index) { layer = arenaLayer };
            instance.transform.SetParent(root, false);
            var filter = instance.AddComponent<MeshFilter>();
            filter.sharedMesh = quad;
            var renderer = instance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 20;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            instance.SetActive(false);
            return new View(instance, renderer);
        }

        private void ApplyLiveSettings(View view, float progress)
        {
            float diameter = settings.GlowRadius * 2f;
            view.Transform.localScale = new Vector3(diameter, diameter, 1f);
            view.Properties.SetColor(TintId, view.Tint);
            view.Properties.SetFloat(IntensityId, settings.GlowIntensity);
            view.Properties.SetFloat(ProgressId, Mathf.Clamp01(progress));
            view.Renderer.SetPropertyBlock(view.Properties);
        }

        private static void Deactivate(View view)
        {
            if (!view.Active) return;
            view.Active = false;
            view.Age = 0f;
            view.GameObject.SetActive(false);
        }

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Duel Impact Glow Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.01f));
            mesh.UploadMeshData(true);
            return mesh;
        }

        private sealed class View
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly MeshRenderer Renderer;
            public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
            public Color Tint;
            public float Age;
            public bool Active;

            public View(GameObject gameObject, MeshRenderer renderer)
            {
                GameObject = gameObject;
                Transform = gameObject.transform;
                Renderer = renderer;
            }
        }
    }
}
