using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>A fixed, prewarmed pool of afterimages: copies of a figure's current frame left behind, fading out. The
    /// steps' dodge/pressure ghosts use it on the arena's real-time clock; the 수훈 afterimages
    /// (<see cref="DuelAuraAfterimages"/>) use a pool of their own, tinted, on the aura's clock.
    /// Whoever ticks a pool chooses its clock. When every ghost is in use, the oldest is reused.</summary>
    public sealed class DuelStepAfterimages : IDisposable
    {
        public const int Capacity = 12;
        public const float Lifetime = 0.22f;
        public const float InitialAlpha = 0.5f;
        private static readonly Color DodgeTint = new Color(0.62f, 0.80f, 0.91f, InitialAlpha);
        private static readonly Color PressureTint = new Color(0.93f, 0.68f, 0.37f, InitialAlpha);
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int AttackOffsetId = Shader.PropertyToID("_AttackOffset");
        private readonly Transform root;
        private readonly View[] views;
        // A material with its own tint (the break outline's silhouette) takes the ghost's colour per renderer, in a
        // property block: URP 17 hands SpriteRenderer.color to its own sprite shaders per draw, not as vertex colour, so
        // that shader never sees it. Those ghosts keep a white sprite colour, so the tint is never applied twice.
        private readonly bool tintsByProperty;
        private readonly MaterialPropertyBlock tintProperties;
        private readonly MaterialPropertyBlock sourceProperties = new MaterialPropertyBlock();
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

        /// <summary>How many ghosts the pool holds (all made up front).</summary>
        public int PoolSize => views.Length;
        /// <summary>The pool's object; every ghost lives under it.</summary>
        public Transform Root => root;
        /// <summary>World order behind every whole-figure group in this arena (-2 for a duel, -4 for four fighters).</summary>
        public int GroupedSortingCeiling { get; set; } = -2;

        /// <summary>Keeps every afterimage off screen without ending it, while a cutscene plays in the middle of a battle.</summary>
        public bool Hidden
        {
            get => !disposed && !root.gameObject.activeSelf;
            set { if (!disposed) root.gameObject.SetActive(!value); }
        }

        public DuelStepAfterimages(Transform parent, Material sharedSpriteMaterial, int arenaLayer, bool layeredActor = false)
            : this(parent, sharedSpriteMaterial, arenaLayer, layeredActor, Capacity, "Step")
        {
        }

        /// <param name="sharedSpriteMaterial">What the ghosts draw with: a sprite material tints a copy of the frame (its
        /// sprite colour), a flat silhouette material (the break outline's) paints its shape in the tint (its <c>_Tint</c>,
        /// set per renderer; see <see cref="DrawnColor"/>).</param>
        /// <param name="capacity">How many ghosts can show at once (at least one).</param>
        /// <param name="label">Names the pool ("Duel {label} Afterimages") and its ghosts ("{label} Afterimage N").</param>
        public DuelStepAfterimages(Transform parent, Material sharedSpriteMaterial, int arenaLayer, bool layeredActor,
            int capacity, string label)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            views = new View[Math.Max(1, capacity)];
            tintsByProperty = sharedSpriteMaterial != null && sharedSpriteMaterial.HasProperty(TintId);
            if (tintsByProperty) tintProperties = new MaterialPropertyBlock();
            label = string.IsNullOrEmpty(label) ? "Step" : label;
            var rootObject = new GameObject("Duel " + label + " Afterimages") { layer = arenaLayer };
            root = rootObject.transform;
            root.SetParent(parent, false);
            for (int index = 0; index < views.Length; index++)
            {
                var instance = new GameObject(label + " Afterimage " + index) { layer = arenaLayer };
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

        /// <summary>A step's ghost: just behind the figure (one sorting order lower than each of its parts).</summary>
        public void Emit(SpriteRenderer source, LegacyStepAction action, SpriteRenderer lowerSource = null)
        {
            if (source == null) return;
            Emit(source, action == LegacyStepAction.Pressure ? PressureTint : DodgeTint, Lifetime,
                source.sortingOrder - 1, lowerSource);
        }

        /// <summary>Leaves a ghost of <paramref name="source"/>'s current frame where it stands, fading from
        /// <paramref name="tint"/>'s opacity to nothing over <paramref name="lifetime"/> seconds of the pool's clock. Nothing
        /// for a source without a sprite or a lifetime of 0 or less.</summary>
        /// <param name="sortingOrder">The ghost's order in the source's sorting layer (a step's: one below the source); a
        /// lower body's ghost keeps its distance from it.</param>
        public void Emit(SpriteRenderer source, Color tint, float lifetime, int sortingOrder,
            SpriteRenderer lowerSource = null)
        {
            if (disposed || source == null || source.sprite == null || !(lifetime > 0f) || float.IsInfinity(lifetime)) return;
            View view = views[nextView];
            nextView = (nextView + 1) % views.Length;
            // Bake the rendered lunge into the snapshot's position. The ghost uses its own material and property block,
            // so it cannot inherit a stale offset or follow the source when that source recovers.
            Vector3 attackOffset = ReadAttackOffset(source);
            view.Transform.position = source.transform.position + source.transform.TransformVector(attackOffset);
            view.Transform.rotation = source.transform.rotation;
            view.Transform.localScale = source.transform.localScale;
            view.Lifetime = lifetime;
            view.Source = source;
            view.Renderer.sprite = source.sprite;
            view.Renderer.flipX = source.flipX;
            view.Renderer.flipY = source.flipY;
            view.Renderer.sortingLayerID = source.sortingLayerID;
            // Complete fighters now sort as groups at 0/-1. A world-space ghost lives outside those groups, so keep it
            // behind both figures rather than tying the rear group's order; aura ghosts requested at -3 stay there.
            int drawnOrder = source.TryGetComponent(out SortingGroup sourceGroup) && sourceGroup.enabled
                ? Mathf.Min(sortingOrder, GroupedSortingCeiling) : sortingOrder;
            view.Renderer.sortingOrder = drawnOrder;
            view.Tint = tint;
            Paint(view.Renderer, view.Tint);
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
                    view.LowerRenderer.sortingOrder = drawnOrder + (lowerSource.sortingOrder - source.sortingOrder);
                    Vector3 lowerPosition = lowerSource.transform.position +
                        lowerSource.transform.TransformVector(ReadAttackOffset(lowerSource));
                    view.LowerRenderer.transform.localPosition = source.transform.InverseTransformPoint(lowerPosition) - attackOffset;
                    view.LowerRenderer.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * lowerSource.transform.rotation;
                    view.LowerRenderer.transform.localScale = lowerSource.transform.localScale;
                    view.LowerRenderer.color = view.Tint;
                }
            }
            view.Age = 0f;
            view.Active = true;
            view.GameObject.SetActive(true);
        }

        /// <param name="realDelta">Seconds on the pool's clock (real time for the steps).</param>
        public void Tick(float realDelta)
        {
            if (disposed) return;
            float delta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            for (int index = 0; index < views.Length; index++)
            {
                View view = views[index];
                if (!view.Active) continue;
                view.Age += delta;
                if (view.Age >= view.Lifetime)
                {
                    Deactivate(view);
                    continue;
                }
                float progress = view.Age / view.Lifetime;
                Color color = view.Tint;
                color.a *= 1f - progress;
                Paint(view.Renderer, color);
                if (view.LowerRenderer != null) view.LowerRenderer.color = color;
            }
        }

        public void Reset()
        {
            for (int index = 0; index < views.Length; index++) Deactivate(views[index]);
            nextView = 0;
        }

        /// <summary>Removes one figure's copies without erasing another figure's trail in the same pool.</summary>
        public void ClearSource(SpriteRenderer source)
        {
            if (disposed || source == null) return;
            for (int index = 0; index < views.Length; index++)
                if (views[index].Source == source) Deactivate(views[index]);
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
            if (root != null) Object.Destroy(root.gameObject);
        }

        /// <summary>The colour <paramref name="ghost"/> is drawn with: the tint in its property block when its material
        /// has one (a silhouette's <c>_Tint</c>; its sprite colour stays white), else its sprite colour.</summary>
        public static Color DrawnColor(SpriteRenderer ghost)
        {
            if (ghost == null) return Color.clear;
            Material material = ghost.sharedMaterial;
            if (material == null || !material.HasProperty(TintId)) return ghost.color;
            var properties = new MaterialPropertyBlock();
            ghost.GetPropertyBlock(properties);
            return properties.GetColor(TintId);
        }

        // The colour goes where the material reads it: a silhouette's tint (one reused block, read back first so it keeps
        // the renderer's own sprite texture), or the sprite colour. Never both, or the opacity would count twice.
        private void Paint(SpriteRenderer renderer, Color color)
        {
            if (!tintsByProperty)
            {
                renderer.color = color;
                return;
            }
            renderer.GetPropertyBlock(tintProperties);
            tintProperties.SetColor(TintId, color);
            renderer.SetPropertyBlock(tintProperties);
        }

        private Vector3 ReadAttackOffset(SpriteRenderer source)
        {
            source.GetPropertyBlock(sourceProperties);
            Vector4 offset = sourceProperties.GetVector(AttackOffsetId);
            var position = new Vector3(offset.x, offset.y, offset.z);
            return IsFinite(position) ? position : Vector3.zero;
        }

        private static bool IsFinite(Vector3 value)
            => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
               !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

        private static void Deactivate(View view)
        {
            view.Active = false;
            view.Source = null;
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
            public float Lifetime = DuelStepAfterimages.Lifetime;
            public Color Tint;
            public bool Active;
            public SpriteRenderer Source;

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
