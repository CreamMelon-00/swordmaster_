using System.Collections.Generic;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>The school corridor's outdoor view moves behind the fixed indoor wall and floor.</summary>
    public sealed class SchoolCorridorBackdrop
    {
        // The 724px authored scene still occupies 12.4 world units. A 160px ceiling and 32px floor
        // extension cover camera tilt without shrinking the windows or moving the marble floor seam.
        private const float WorldUnitsPerPixel = 12.4f / 724f;
        private const float PaddedWorldHeight = 916f * WorldUnitsPerPixel;
        private const float PaddedCenterY = -1.5f + (160f - 32f) * WorldUnitsPerPixel * .5f;
        private readonly Transform root;
        private readonly Material material;
        private readonly int arenaLayer;
        private readonly Layer[] layers;
        private Color tint = Color.white;

        public IReadOnlyList<Layer> Layers => layers;
        public bool IsVisible => root.gameObject.activeSelf;
        public bool HasRequiredAssets => layers[0].Sprite != null && layers[1].Sprite != null && layers[2].Sprite != null;

        public SchoolCorridorBackdrop(Transform parent, Material sharedMaterial, int arenaLayer)
        {
            material = sharedMaterial;
            this.arenaLayer = arenaLayer;
            var corridor = new GameObject("School Corridor Parallax") { layer = arenaLayer };
            root = corridor.transform;
            root.SetParent(parent, false);
            layers = new[]
            {
                // All three authored images share one vertical framing so the courtyard meets the window openings.
                new Layer("Far Courtyard", "SchoolCorridor/corridor-far", .12f, PaddedWorldHeight, PaddedCenterY, -7),
                new Layer("Near Trees", "SchoolCorridor/corridor-near", .35f, PaddedWorldHeight, PaddedCenterY, -6),
                // The reframed floor starts near y=-2.7, just above the actors' feet.
                new Layer("Interior Windows", "SchoolCorridor/corridor-interior", 1f, PaddedWorldHeight, PaddedCenterY, -5)
            };
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Reset(Camera camera)
        {
            tint = Color.white;
            Tick(camera, false, 0f);
        }

        public void Tick(Camera camera, bool inspecting, float scaledDelta, float focusDarkening = 0f)
        {
            if (camera == null) return;
            tint = Color.Lerp(tint, inspecting ? new Color(.6f, .6f, .6f, 1f) : Color.white,
                Mathf.Clamp01(scaledDelta * 5f));
            float darkness = float.IsNaN(focusDarkening) || float.IsInfinity(focusDarkening)
                ? 0f : Mathf.Clamp01(focusDarkening);
            Color focusedTint = new Color(tint.r * (1f - darkness), tint.g * (1f - darkness),
                tint.b * (1f - darkness), tint.a);
            float cameraX = root.InverseTransformPoint(camera.transform.position).x;
            float angle = camera.transform.eulerAngles.z * Mathf.Deg2Rad;
            float visibleWidth = 2f * camera.orthographicSize *
                (camera.aspect * Mathf.Abs(Mathf.Cos(angle)) + Mathf.Abs(Mathf.Sin(angle)));
            foreach (Layer layer in layers)
            {
                if (layer.Sprite == null) continue;
                int count = Mathf.Max(3, Mathf.CeilToInt(visibleWidth / layer.TileWidth) + 2);
                if (count % 2 == 0) count++;
                EnsureTiles(layer, count);
                float offset = cameraX * (1f - layer.ParallaxFactor);
                int centerIndex = Mathf.FloorToInt(cameraX * layer.ParallaxFactor / layer.TileWidth);
                for (int index = 0; index < layer.tiles.Count; index++)
                {
                    SpriteRenderer tile = layer.tiles[index];
                    bool active = index < count;
                    if (tile.gameObject.activeSelf != active) tile.gameObject.SetActive(active);
                    if (!active) continue;
                    int tileIndex = centerIndex + index - count / 2;
                    tile.transform.localPosition = new Vector3(offset + tileIndex * layer.TileWidth, layer.Y, 1f);
                    // Mirrored joins put the same source edge on both sides of each seam.
                    tile.flipX = (tileIndex & 1) != 0;
                    tile.color = focusedTint;
                }
            }
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
            internal readonly float Scale;
            internal readonly int SortingOrder;
            public readonly float Y;
            public Sprite Sprite { get; }
            public float ParallaxFactor { get; }
            public float TileWidth { get; }
            public IReadOnlyList<SpriteRenderer> Tiles => tiles;

            internal Layer(string name, string resource, float factor, float worldHeight, float y, int order)
            {
                Name = name;
                Sprite = Resources.Load<Sprite>(resource);
                ParallaxFactor = factor;
                SortingOrder = order;
                Y = y;
                // Tight-mesh sprites can have smaller bounds than their authored canvas (the tree layer is sparse).
                // Register all layers by the full source rect so their pixels meet the transparent window openings.
                Scale = Sprite != null ? worldHeight / (Sprite.rect.height / Sprite.pixelsPerUnit) : 1f;
                TileWidth = Sprite != null ? Sprite.rect.width / Sprite.pixelsPerUnit * Scale : worldHeight * 3f;
            }
        }
    }
}
