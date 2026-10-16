using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Short weapon-light snapshots, independent of body motion and the battle clock. Geometry is authored in
    /// the fighter art's local units, with forward along +x; the caller supplies the body's visible world origin.</summary>
    public sealed class DuelWeaponTrail : IDisposable
    {
        public const int Capacity = 8;
        public const float DefaultLifetime = .12f;
        private const int Segments = 12;
        private const int PointCount = Segments + 1;
        private const int LayerCount = 2;
        private const float OuterWidth = .24f;
        private const float CoreWidth = .065f;
        private const int SortingOrder = 8;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly Color OuterInk = new Color(1.25f, .74f, .28f, .55f);
        private static readonly Color CoreInk = new Color(1.75f, 1.60f, 1.32f, .92f);

        // Contact-frame hand and blade tip, normalised to face right. The full-body sprites have no weapon bone.
        private static readonly BladeAnchor[] ElisaSlash =
        {
            new BladeAnchor(.95f, -.85f, 2.68f, -1.85f),
            new BladeAnchor(1.10f, .60f, 2.40f, 2.10f),
            new BladeAnchor(.45f, -.28f, 2.30f, -.28f),
        };
        private static readonly BladeAnchor[] ElisaPierce =
        {
            new BladeAnchor(1.20f, -.35f, 3f, -.30f),
            new BladeAnchor(1.10f, -.10f, 3.10f, -.08f),
            new BladeAnchor(1.45f, -.60f, 3.35f, -.60f),
        };
        private static readonly BladeAnchor[] ElisaHit =
        {
            new BladeAnchor(1.78f, -.05f, 2f, -.10f),
            new BladeAnchor(1.03f, .30f, 1.23f, .27f),
            new BladeAnchor(.88f, -.80f, 1.08f, -.98f),
        };
        private static readonly BladeAnchor[] StudentSlash =
        {
            new BladeAnchor(.73f, -.58f, 2.80f, -1.38f),
            new BladeAnchor(1.03f, .07f, 2.68f, 1.70f),
            new BladeAnchor(.53f, -.48f, 2.78f, -.48f),
        };
        private static readonly BladeAnchor[] StudentPierce =
        {
            new BladeAnchor(.80f, -.55f, 2.95f, -.55f),
            new BladeAnchor(.65f, -.10f, 2.80f, .22f),
            new BladeAnchor(.45f, -.75f, 2.68f, -.78f),
        };
        private static readonly BladeAnchor[] StudentHit =
        {
            new BladeAnchor(1.10f, -.48f, 1.33f, -.55f),
            new BladeAnchor(1.05f, .45f, 1.20f, .70f),
            new BladeAnchor(.65f, -.82f, .93f, -1.13f),
        };

        private readonly Material material;
        private readonly View[] views = new View[Capacity];
        private int nextView;
        private bool disposed;

        public Transform Root { get; }
        public int PoolSize => views.Length;
        public int TotalEmissionCount { get; private set; }
        public bool HasRequiredAssets => !disposed && material != null && material.shader != null && material.shader.isSupported;
        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (View view in views)
                    if (view.Active) count++;
                return count;
            }
        }
        public bool Hidden
        {
            get => !disposed && Root != null && !Root.gameObject.activeSelf;
            set { if (!disposed && Root != null) Root.gameObject.SetActive(!value); }
        }

        public DuelWeaponTrail(Transform parent, int layer)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var rootObject = new GameObject("Duel Weapon Trails") { layer = layer };
            Root = rootObject.transform;
            Root.SetParent(parent, false);
            Shader shader = Resources.Load<Shader>("DuelVFX/BreakSilhouette");
            if (shader != null && shader.isSupported)
            {
                material = new Material(shader) { name = "Duel Weapon Trail (Runtime)" };
                material.SetColor(TintId, Color.white);
                material.mainTexture = Texture2D.whiteTexture;
            }
            for (int index = 0; index < views.Length; index++) views[index] = CreateView(index, layer);
        }

        /// <summary>Leaves one fixed stroke of light. Left-facing fighters mirror the canonical art coordinates.
        /// Defence/None, invalid positions, non-positive strength or lifetime leave nothing behind.</summary>
        public void Emit(Vector3 worldVisualOrigin, bool facesRight, LegacySkillProperty property, int variant,
            bool studentStyle, float strength, float lifetime)
        {
            if (!HasRequiredAssets || !IsFinite(worldVisualOrigin) || !IsFinite(strength) || !(strength > 0f) ||
                !IsFinite(lifetime) || !(lifetime > 0f) ||
                property != LegacySkillProperty.Slash && property != LegacySkillProperty.Penetrate &&
                property != LegacySkillProperty.Hit) return;
            View view = views[nextView];
            nextView = (nextView + 1) % views.Length;
            int variation = ((variant % 3) + 3) % 3;
            BuildStroke(view, property, variation, studentStyle);
            view.Transform.position = worldVisualOrigin;
            view.Transform.localRotation = Quaternion.identity;
            view.Transform.localScale = new Vector3(facesRight ? 1f : -1f, 1f, 1f);
            view.Age = 0f;
            view.Lifetime = lifetime;
            view.Strength = Mathf.Clamp01(strength);
            view.Active = true;
            Apply(view, 1f);
            view.GameObject.SetActive(true);
            if (TotalEmissionCount < int.MaxValue) TotalEmissionCount++;
        }

        /// <summary>Fades on the supplied real seconds, including hit stop and a held blade bind.</summary>
        public void Tick(float realDelta)
        {
            if (disposed) return;
            float delta = IsFinite(realDelta) && realDelta > 0f ? realDelta : 0f;
            foreach (View view in views)
            {
                if (!view.Active) continue;
                view.Age += delta;
                if (view.Age >= view.Lifetime)
                {
                    Deactivate(view);
                    continue;
                }
                float remaining = 1f - view.Age / view.Lifetime;
                Apply(view, Mathf.SmoothStep(0f, 1f, remaining));
            }
        }

        public void Reset()
        {
            foreach (View view in views) Deactivate(view);
            nextView = 0;
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
            foreach (View view in views)
                if (view.Mesh != null) Object.Destroy(view.Mesh);
            if (material != null) Object.Destroy(material);
            if (Root != null) Object.Destroy(Root.gameObject);
        }

        private View CreateView(int index, int layer)
        {
            var instance = new GameObject("Weapon Trail " + index) { layer = layer };
            instance.transform.SetParent(Root, false);
            var mesh = new Mesh { name = "Duel Weapon Trail " + index };
            mesh.MarkDynamic();
            var view = new View(instance, mesh);
            for (int coat = 0; coat < LayerCount; coat++)
            {
                for (int point = 0; point < PointCount; point++)
                {
                    int vertex = coat * PointCount * 2 + point * 2;
                    view.Colors[vertex] = view.Colors[vertex + 1] = coat == 0 ? OuterInk : CoreInk;
                    view.Uvs[vertex] = new Vector2(point / (float)Segments, 0f);
                    view.Uvs[vertex + 1] = new Vector2(point / (float)Segments, 1f);
                    if (point == Segments) continue;
                    int triangle = (coat * Segments + point) * 6;
                    view.Triangles[triangle] = vertex;
                    view.Triangles[triangle + 1] = vertex + 1;
                    view.Triangles[triangle + 2] = vertex + 2;
                    view.Triangles[triangle + 3] = vertex + 2;
                    view.Triangles[triangle + 4] = vertex + 1;
                    view.Triangles[triangle + 5] = vertex + 3;
                }
            }
            mesh.vertices = view.Vertices;
            mesh.colors = view.Colors;
            mesh.uv = view.Uvs;
            mesh.triangles = view.Triangles;
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = instance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = SortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            view.Renderer = renderer;
            instance.SetActive(false);
            return view;
        }

        private static void BuildStroke(View view, LegacySkillProperty property, int variant, bool studentStyle)
        {
            BladeAnchor anchor = Anchors(property, studentStyle)[variant];
            Vector2 blade = anchor.Tip - anchor.Hand;
            Vector2 direction = blade.normalized;
            float endAngle = Mathf.Atan2(blade.y, blade.x);
            for (int point = 0; point < PointCount; point++)
            {
                float progress = point / (float)Segments;
                Vector2 position;
                if (property == LegacySkillProperty.Slash && variant != 2)
                {
                    float sweep = (variant == 0 ? 58f : -58f) * Mathf.Deg2Rad;
                    float angle = endAngle + sweep * (1f - progress);
                    position = anchor.Hand + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * blade.magnitude;
                }
                else if (property == LegacySkillProperty.Slash)
                {
                    position = Vector2.Lerp(anchor.Hand + direction * .20f, anchor.Tip, progress);
                    position.y += Mathf.Sin(progress * Mathf.PI) * .10f;
                }
                else if (property == LegacySkillProperty.Penetrate)
                    position = Vector2.Lerp(anchor.Hand + direction * .15f, anchor.Tip + direction * .40f, progress);
                else
                {
                    // Blunt cels carry the sword behind the body: trace only the forward knuckle/handle contact.
                    float length = .25f + variant * .10f;
                    position = anchor.Tip + direction * ((progress - .55f) * length);
                }
                view.Points[point] = position;
            }
            float widthScale = property == LegacySkillProperty.Slash ? 1f : .70f;
            for (int point = 0; point < PointCount; point++)
            {
                Vector2 tangent = view.Points[Mathf.Min(point + 1, Segments)] - view.Points[Mathf.Max(0, point - 1)];
                Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized;
                float taper = Mathf.Sin(point / (float)Segments * Mathf.PI);
                if (point == 0 || point == Segments) taper = 0f;
                for (int coat = 0; coat < LayerCount; coat++)
                {
                    float width = (coat == 0 ? OuterWidth : CoreWidth) * widthScale * taper * .5f;
                    int vertex = coat * PointCount * 2 + point * 2;
                    Vector2 side = normal * width;
                    view.Vertices[vertex] = view.Points[point] - side;
                    view.Vertices[vertex + 1] = view.Points[point] + side;
                }
            }
            view.Mesh.vertices = view.Vertices;
            view.Mesh.RecalculateBounds();
        }

        private static BladeAnchor[] Anchors(LegacySkillProperty property, bool studentStyle)
            => property == LegacySkillProperty.Slash ? (studentStyle ? StudentSlash : ElisaSlash)
                : property == LegacySkillProperty.Penetrate ? (studentStyle ? StudentPierce : ElisaPierce)
                : studentStyle ? StudentHit : ElisaHit;

        private static void Apply(View view, float fade)
        {
            // The cue starts just before contact; its first real-time hundredth is a softer glint rather than a full sweep.
            float entry = Mathf.Lerp(.55f, 1f, Mathf.Clamp01(view.Age / .01f));
            view.Properties.SetColor(TintId, new Color(1f, 1f, 1f, view.Strength * fade * entry));
            view.Renderer.SetPropertyBlock(view.Properties);
        }

        private static void Deactivate(View view)
        {
            view.Active = false;
            view.Age = 0f;
            view.GameObject.SetActive(false);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private readonly struct BladeAnchor
        {
            public readonly Vector2 Hand, Tip;
            public BladeAnchor(float handX, float handY, float tipX, float tipY)
            {
                Hand = new Vector2(handX, handY);
                Tip = new Vector2(tipX, tipY);
            }
        }

        private sealed class View
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly Mesh Mesh;
            public readonly Vector2[] Points = new Vector2[PointCount];
            public readonly Vector3[] Vertices = new Vector3[PointCount * 2 * LayerCount];
            public readonly Color[] Colors = new Color[PointCount * 2 * LayerCount];
            public readonly Vector2[] Uvs = new Vector2[PointCount * 2 * LayerCount];
            public readonly int[] Triangles = new int[Segments * 6 * LayerCount];
            public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
            public MeshRenderer Renderer;
            public float Age, Lifetime, Strength;
            public bool Active;

            public View(GameObject gameObject, Mesh mesh)
            {
                GameObject = gameObject;
                Transform = gameObject.transform;
                Mesh = mesh;
            }
        }
    }
}
