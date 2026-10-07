using System.Collections.Generic;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Camera-relative, endlessly recycled forest tiles. The arena owns their lifetime.</summary>
    public sealed class ForestParallaxBackdrop
    {
        private readonly Transform root;
        private readonly Material material;
        private readonly int arenaLayer;
        private readonly DuelPresentationSettings settings;
        private readonly Layer[] layers;
        private Color tint = Color.white;

        public IReadOnlyList<Layer> Layers => layers;
        public bool IsVisible => root.gameObject.activeSelf;
        public bool HasRequiredAssets => layers[0].Sprite != null && layers[1].Sprite != null &&
            layers[2].Sprite != null && layers[3].Sprite != null;

        public ForestParallaxBackdrop(Transform parent, Material sharedMaterial, int arenaLayer,
            DuelPresentationSettings settings = null)
        {
            material = sharedMaterial;
            this.arenaLayer = arenaLayer;
            this.settings = settings;
            var forest = new GameObject("Forest Parallax") { layer = arenaLayer };
            root = forest.transform;
            root.SetParent(parent, false);
            layers = new[]
            {
                // Reduce scenery scale without shrinking actors or zooming out the duel camera.
                // Opaque sky/mist covers the view behind a separately moving transparent tree silhouette layer.
                new Layer("Far Mist", "ForestArena/forest-far-mist", 0.10f, 14.4f, -0.5f, -7),
                new Layer("Far Trees", "ForestArena/forest-far-trees", 0.24f, 14.4f, -0.5f, -6),
                // The belt's rear edge is ~47% down the image (y=-0.82); feet at y=-2.73 stand inside it.
                new Layer("Forest Path", "ForestArena/forest-belt-mid", 1f, 12.6f, -1.2f, -5),
                new Layer("Near Ferns", "ForestArena/forest-near", 1.35f, 12.6f, -0.75f, 1)
            };
            ApplySettings();
        }

        public void Reset(Camera camera)
        {
            tint = Color.white;
            Tick(camera, false, 0f);
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Tick(Camera camera, bool inspecting, float scaledDelta, float focusDarkening = 0f)
        {
            if (camera == null) return;
            ApplySettings();
            tint = Color.Lerp(tint, inspecting ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white,
                Mathf.Clamp01(scaledDelta * 5f));
            // Focus is transient and separate from inspection tint. Applying it
            // from the base colour each frame prevents repeated steps from
            // permanently darkening pooled tiles, and never touches the actors.
            float darkness = float.IsNaN(focusDarkening) || float.IsInfinity(focusDarkening)
                ? 0f : Mathf.Clamp01(focusDarkening);
            Color focusedTint = new Color(tint.r * (1f - darkness), tint.g * (1f - darkness),
                tint.b * (1f - darkness), tint.a);
            var cameraX = root.InverseTransformPoint(camera.transform.position).x;
            var angle = camera.transform.eulerAngles.z * Mathf.Deg2Rad;
            // Include the tilted camera's corners, rather than just its unrotated width.
            var visibleWidth = 2f * camera.orthographicSize *
                (camera.aspect * Mathf.Abs(Mathf.Cos(angle)) + Mathf.Abs(Mathf.Sin(angle)));
            foreach (var layer in layers)
            {
                if (layer.Sprite == null) continue;
                var count = Mathf.Max(3, Mathf.CeilToInt(visibleWidth / layer.TileWidth) + 2);
                if (count % 2 == 0) count++;
                EnsureTiles(layer, count);
                var offset = cameraX * (1f - layer.ParallaxFactor);
                var centerIndex = Mathf.FloorToInt(cameraX * layer.ParallaxFactor / layer.TileWidth);
                for (var index = 0; index < layer.tiles.Count; index++)
                {
                    var tile = layer.tiles[index];
                    var active = index < count;
                    if (tile.gameObject.activeSelf != active) tile.gameObject.SetActive(active);
                    if (!active) continue;
                    var tileIndex = centerIndex + index - count / 2;
                    tile.transform.localPosition = new Vector3(offset + tileIndex * layer.TileWidth, layer.Y, 1f);
                    // Alternating reflection makes both joins share precisely the same source edge.
                    // Absolute indices keep the pattern stable when pooled tiles are recycled, including negative X.
                    tile.flipX = (tileIndex & 1) != 0;
                    tile.color = focusedTint;
                }
            }
        }

        private void ApplySettings()
        {
            if (settings == null) return;
            layers[0].ApplyLayerSize(settings.FarMistHeight, settings.FarMistY);
            // Keep the established farHeight/farY fields attached to the distant trees.
            layers[1].ApplyLayerSize(settings.FarHeight, settings.FarY);
            layers[2].ApplyLayerSize(settings.MidHeight, settings.MidY);
            layers[3].ApplyLayerSize(settings.NearHeight, settings.NearY);
        }

        private void EnsureTiles(Layer layer, int count)
        {
            while (layer.tiles.Count < count)
            {
                var child = new GameObject(layer.Name + " Tile " + layer.tiles.Count) { layer = arenaLayer };
                child.transform.SetParent(root, false);
                child.transform.localScale = Vector3.one * layer.Scale;
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = layer.Sprite;
                renderer.sortingOrder = layer.SortingOrder;
                if (material != null) renderer.sharedMaterial = material;
                layer.tiles.Add(renderer);
            }
        }

        public sealed class Layer
        {
            internal readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>(5);
            internal readonly string Name;
            internal float Y { get; private set; }
            internal float Scale { get; private set; }
            internal readonly int SortingOrder;
            public Sprite Sprite { get; }
            public float ParallaxFactor { get; }
            public float TileWidth { get; private set; }
            public IReadOnlyList<SpriteRenderer> Tiles => tiles;

            internal Layer(string name, string resource, float factor, float worldHeight, float y, int order)
            {
                Name = name;
                Sprite = Resources.Load<Sprite>(resource);
                ParallaxFactor = factor;
                SortingOrder = order;
                ApplyLayerSize(worldHeight, y);
            }

            internal void ApplyLayerSize(float worldHeight, float y)
            {
                Y = y;
                float nextScale = Sprite != null ? worldHeight / Sprite.bounds.size.y : 1f;
                float nextWidth = Sprite != null ? Sprite.bounds.size.x * nextScale : worldHeight * 3f;
                if (Scale == nextScale && TileWidth == nextWidth) return;
                Scale = nextScale;
                TileWidth = nextWidth;
                // Inspector changes resize existing pooled renderers, including
                // inactive tiles. Unchanged values do not rewrite their scale.
                foreach (var tile in tiles) tile.transform.localScale = Vector3.one * Scale;
            }
        }
    }
}
