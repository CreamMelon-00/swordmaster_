using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public enum ExplorationFieldMap { Corridor, Forest }

    /// <summary>
    /// Finite exploration maps. Their doors and ground keep authored coordinates; only distant scenery
    /// follows the camera. The owning field destroys this backdrop with its parent.
    /// </summary>
    public sealed class ExplorationFieldBackdrop
    {
        public const float WorldUnitsPerPixel = 12.4f / 724f;
        private const float AuthoredWidth = 4096f;
        private const float AuthoredHeight = 724f;
        private const float AuthoredCenterY = -1.5f;
        private const float CorridorCenterY = AuthoredCenterY + (160f - 32f) * WorldUnitsPerPixel * .5f;

        private readonly Transform root;
        private readonly MapLayers corridor;
        private readonly MapLayers forest;
        private MapLayers current;

        public ExplorationFieldMap CurrentMap { get; private set; }
        public IReadOnlyList<SpriteRenderer> Layers => current.Renderers;
        public bool IsVisible => root.gameObject.activeSelf;
        public bool HasRequiredAssets
        {
            get
            {
                foreach (SpriteRenderer renderer in current.Renderers)
                    if (renderer.sprite == null) return false;
                return true;
            }
        }

        /// <summary>Authored image bounds in field-local world coordinates, excluding export padding.</summary>
        public Rect MapBounds => new Rect(-AuthoredWidth * WorldUnitsPerPixel * .5f,
            AuthoredCenterY - AuthoredHeight * WorldUnitsPerPixel * .5f,
            AuthoredWidth * WorldUnitsPerPixel, AuthoredHeight * WorldUnitsPerPixel);

        public ExplorationFieldBackdrop(Transform parent, Material sharedMaterial, int fieldLayer)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var backdrop = new GameObject("Exploration Field Backdrop") { layer = fieldLayer };
            root = backdrop.transform;
            root.SetParent(parent, false);

            corridor = CreateMap("Corridor", root, fieldLayer, sharedMaterial, CorridorCenterY,
                new LayerSpec("Far Courtyard", "ExplorationMaps/Corridor/corridor-far", .12f, -90),
                new LayerSpec("Window Trees", "ExplorationMaps/Corridor/corridor-near", .35f, -80),
                new LayerSpec("Interior and Exit", "ExplorationMaps/Corridor/corridor-interior", 1f, -70));
            forest = CreateMap("Forest", root, fieldLayer, sharedMaterial, AuthoredCenterY,
                new LayerSpec("Far Mist", "ExplorationMaps/Forest/forest-far-mist", .10f, -90),
                new LayerSpec("Far Trees", "ExplorationMaps/Forest/forest-far-trees", .24f, -80),
                new LayerSpec("Path and School Entrance", "ExplorationMaps/Forest/forest-belt-mid", 1f, -70),
                // Foreground bushes can cover feet, but their ground alignment stays fixed at the map edges.
                new LayerSpec("Foreground Bushes", "ExplorationMaps/Forest/forest-near", 1f, 100));
            SetMap(ExplorationFieldMap.Corridor);
        }

        /// <summary>Convert a point from the unpadded authored image, whose origin is its upper left.</summary>
        public static Vector2 AuthoredToWorld(Vector2 pixel) => new Vector2(
            (pixel.x - AuthoredWidth * .5f) * WorldUnitsPerPixel,
            AuthoredCenterY + (AuthoredHeight * .5f - pixel.y) * WorldUnitsPerPixel);

        public void SetMap(ExplorationFieldMap map)
        {
            switch (map)
            {
                case ExplorationFieldMap.Corridor: current = corridor; break;
                case ExplorationFieldMap.Forest: current = forest; break;
                default: throw new ArgumentOutOfRangeException(nameof(map));
            }
            CurrentMap = map;
            corridor.Root.gameObject.SetActive(map == ExplorationFieldMap.Corridor);
            forest.Root.gameObject.SetActive(map == ExplorationFieldMap.Forest);
            foreach (SpriteRenderer renderer in current.Renderers)
                renderer.transform.localPosition = new Vector3(0f, current.CenterY, 1f);
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        /// <summary>Keep the viewport inside the finite image instead of repeating an entrance or end wall.</summary>
        public float ClampCameraX(Camera camera, float x)
        {
            Rect bounds = MapBounds;
            if (camera == null) return Mathf.Clamp(x, bounds.xMin, bounds.xMax);
            float angle = camera.transform.eulerAngles.z * Mathf.Deg2Rad;
            float halfWidth = camera.orthographicSize *
                (camera.aspect * Mathf.Abs(Mathf.Cos(angle)) + Mathf.Abs(Mathf.Sin(angle)));
            halfWidth /= Mathf.Max(.0001f, Mathf.Abs(root.lossyScale.x));
            if (halfWidth * 2f >= bounds.width) return bounds.center.x;
            return Mathf.Clamp(x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
        }

        public void Tick(Camera camera)
        {
            if (camera == null) return;
            float cameraX = root.InverseTransformPoint(camera.transform.position).x;
            for (int index = 0; index < current.Renderers.Length; index++)
            {
                SpriteRenderer renderer = current.Renderers[index];
                renderer.transform.localPosition = new Vector3(
                    cameraX * (1f - current.ParallaxFactors[index]), current.CenterY, 1f);
            }
        }

        private static MapLayers CreateMap(string name, Transform parent, int fieldLayer, Material material,
            float centerY, params LayerSpec[] specs)
        {
            var map = new GameObject(name + " Exploration Map") { layer = fieldLayer };
            map.transform.SetParent(parent, false);
            var renderers = new SpriteRenderer[specs.Length];
            var factors = new float[specs.Length];
            for (int index = 0; index < specs.Length; index++)
            {
                LayerSpec spec = specs[index];
                var child = new GameObject(spec.Name) { layer = fieldLayer };
                child.transform.SetParent(map.transform, false);
                child.transform.localPosition = new Vector3(0f, centerY, 1f);
                SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = Resources.Load<Sprite>(spec.Resource);
                renderer.sortingOrder = spec.SortingOrder;
                if (material != null) renderer.sharedMaterial = material;
                // Full Rect export registers every sparse layer to the same canvas. Matching pixel size,
                // rather than sprite bounds, also keeps corridor padding from shrinking the original scene.
                if (renderer.sprite != null)
                    child.transform.localScale = Vector3.one * renderer.sprite.pixelsPerUnit * WorldUnitsPerPixel;
                renderers[index] = renderer;
                factors[index] = spec.ParallaxFactor;
            }
            return new MapLayers(map.transform, renderers, factors, centerY);
        }

        private sealed class MapLayers
        {
            public readonly Transform Root;
            public readonly SpriteRenderer[] Renderers;
            public readonly float[] ParallaxFactors;
            public readonly float CenterY;

            public MapLayers(Transform root, SpriteRenderer[] renderers, float[] factors, float centerY)
            {
                Root = root;
                Renderers = renderers;
                ParallaxFactors = factors;
                CenterY = centerY;
            }
        }

        private readonly struct LayerSpec
        {
            public readonly string Name;
            public readonly string Resource;
            public readonly float ParallaxFactor;
            public readonly int SortingOrder;

            public LayerSpec(string name, string resource, float factor, int order)
            {
                Name = name;
                Resource = resource;
                ParallaxFactor = factor;
                SortingOrder = order;
            }
        }
    }
}
