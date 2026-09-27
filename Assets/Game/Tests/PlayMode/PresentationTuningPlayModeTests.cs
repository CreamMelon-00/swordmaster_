using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class PresentationTuningPlayModeTests
    {
        [UnityTest]
        public IEnumerator BackdropWithoutSettings_RetainsItsOriginalDefaults()
        {
            yield return null;
            GameObject fixture = CreateFixture(out Camera camera);
            try
            {
                // The original three-argument constructor remains usable on its own.
                var forest = new ForestParallaxBackdrop(fixture.transform, null, 30);
                forest.Reset(camera);
                Assert.That(forest.HasRequiredAssets, Is.True);
                AssertGeometry(forest, new[] { 14.4f, 14.4f, 12.6f, 12.6f }, new[] { -.5f, -.5f, -1.2f, -.75f });
            }
            finally { Object.Destroy(fixture); }
        }

        [UnityTest]
        public IEnumerator LiveLayerSettings_ApplyAtDeltaZero_WithoutReplacingPooledRenderers()
        {
            yield return null;
            GameObject fixture = CreateFixture(out Camera camera);
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                var forest = new ForestParallaxBackdrop(fixture.transform, null, 30, settings);
                forest.Reset(camera);
                Assert.That(forest.HasRequiredAssets, Is.True);
                var originalTiles = CaptureTiles(forest);
                JsonUtility.FromJsonOverwrite("{\"farMistHeight\":12,\"farMistY\":-1,\"farHeight\":10,\"farY\":2,\"midHeight\":6,\"midY\":-3,\"nearHeight\":5,\"nearY\":-0.75}", settings);
                forest.Tick(camera, false, 0f);
                AssertGeometry(forest, new[] { 12f, 10f, 6f, 5f }, new[] { -1f, 2f, -3f, -.75f });
                AssertSamePool(forest, originalTiles);
                forest.Tick(camera, false, 0f);
                AssertSamePool(forest, originalTiles);
                AssertGeometry(forest, new[] { 12f, 10f, 6f, 5f }, new[] { -1f, 2f, -3f, -.75f });
            }
            finally
            {
                Object.Destroy(fixture);
                Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator ChangedTileWidths_CoverNegativeTravel_AndReuseTheExpandedPool()
        {
            yield return null;
            GameObject fixture = CreateFixture(out Camera camera);
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                camera.orthographicSize = 6f;
                camera.aspect = 32f / 9f;
                var forest = new ForestParallaxBackdrop(fixture.transform, null, 30, settings);
                forest.Reset(camera);
                Assert.That(forest.HasRequiredAssets, Is.True);
                int originalFarCount = forest.Layers[0].Tiles.Count;
                JsonUtility.FromJsonOverwrite("{\"farMistHeight\":5,\"farHeight\":5,\"midHeight\":5,\"nearHeight\":5}", settings);
                forest.Tick(camera, false, 0f);
                Assert.That(forest.Layers[0].Tiles.Count, Is.GreaterThan(originalFarCount),
                    "Smaller tiles should expand the pool only when coverage requires more renderers.");
                AssertGeometry(forest, new[] { 5f, 5f, 5f, 5f }, new[] { -.5f, -.5f, -1.2f, -.75f });
                var expandedTiles = CaptureTiles(forest);
                // Tuning is respected as authored; the backdrop does not inflate
                // small layer heights to hide vertical gaps outside the tuned view.
                JsonUtility.FromJsonOverwrite("{\"farMistHeight\":100,\"farHeight\":100,\"midHeight\":100,\"nearHeight\":100}", settings);
                forest.Tick(camera, false, 0f);
                AssertSamePool(forest, expandedTiles);
                AssertGeometry(forest, new[] { 100f, 100f, 100f, 100f }, new[] { -.5f, -.5f, -1.2f, -.75f });
                JsonUtility.FromJsonOverwrite("{\"farMistHeight\":5,\"farHeight\":5,\"midHeight\":5,\"nearHeight\":5}", settings);
                foreach (float x in new[] { -75.5f, -420.25f, 600.1f, 0f })
                {
                    camera.transform.localPosition = new Vector3(x, -1.5f, -10f);
                    forest.Tick(camera, false, 0f);
                    AssertSamePool(forest, expandedTiles);
                    AssertGeometry(forest, new[] { 5f, 5f, 5f, 5f }, new[] { -.5f, -.5f, -1.2f, -.75f });
                    AssertHorizontalCoverage(forest, camera);
                }
            }
            finally
            {
                Object.Destroy(fixture);
                Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator ResourcesSettings_RoundTripThroughAnEditableClone_WithoutChangingTheAsset()
        {
            yield return null;
            var source = Resources.Load<DuelPresentationSettings>("DuelPresentationSettings");
            Assert.That(source, Is.Not.Null, "The controller's authoring asset must be available through Resources.");
            string originalJson = JsonUtility.ToJson(source);
            var edited = Object.Instantiate(source);
            var roundTrip = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            GameObject fixture = CreateFixture(out Camera camera);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"farMistHeight\":15,\"farMistY\":-1,\"farHeight\":18,\"farY\":1,\"midHeight\":10,\"midY\":-2,\"nearHeight\":8,\"nearY\":-4}", edited);
                string editedJson = JsonUtility.ToJson(edited);
                JsonUtility.FromJsonOverwrite(editedJson, roundTrip);
                Assert.That(JsonUtility.ToJson(roundTrip), Is.EqualTo(editedJson));
                var forest = new ForestParallaxBackdrop(fixture.transform, null, 30, roundTrip);
                forest.Reset(camera);
                Assert.That(forest.HasRequiredAssets, Is.True);
                AssertGeometry(forest, new[] { 15f, 18f, 10f, 8f }, new[] { -1f, 1f, -2f, -4f });
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(originalJson),
                    "PlayMode tuning fixtures must edit temporary clones, never the persistent Inspector asset.");
            }
            finally
            {
                Object.Destroy(fixture);
                Object.Destroy(edited);
                Object.Destroy(roundTrip);
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(originalJson));
            }
        }

        [UnityTest]
        public IEnumerator AddedMistSettings_PreserveExistingFarTreeTuning_AndClampIndependently()
        {
            yield return null;
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            GameObject fixture = CreateFixture(out Camera camera);
            try
            {
                // Previously authored settings contain no mist fields. Their far
                // values still size the trees, not the newly added opaque layer.
                JsonUtility.FromJsonOverwrite("{\"farHeight\":9,\"farY\":2,\"midHeight\":11,\"midY\":-2,\"nearHeight\":7,\"nearY\":-3,\"animationPlaybackSpeed\":0.8,\"attackInterval\":0.21,\"bloomIntensity\":0.4}", settings);
                Assert.That(settings.FarMistHeight, Is.EqualTo(14.4f));
                Assert.That(settings.FarMistY, Is.EqualTo(-.5f));
                var forest = new ForestParallaxBackdrop(fixture.transform, null, 30, settings);
                forest.Reset(camera);
                Assert.That(forest.HasRequiredAssets, Is.True);
                AssertGeometry(forest, new[] { 14.4f, 9f, 11f, 7f }, new[] { -.5f, 2f, -2f, -3f });
                var originalTiles = CaptureTiles(forest);

                JsonUtility.FromJsonOverwrite("{\"farMistHeight\":13,\"farMistY\":3}", settings);
                forest.Tick(camera, false, 0f);
                AssertGeometry(forest, new[] { 13f, 9f, 11f, 7f }, new[] { 3f, 2f, -2f, -3f });
                AssertSamePool(forest, originalTiles);
                Assert.That(settings.AnimationPlaybackSpeed, Is.EqualTo(.8f));
                Assert.That(settings.AttackInterval, Is.EqualTo(.21f));
                Assert.That(settings.BloomIntensity, Is.EqualTo(.4f));

                JsonUtility.FromJsonOverwrite("{\"farMistHeight\":0,\"farMistY\":100}", settings);
                Assert.That(settings.FarMistHeight, Is.EqualTo(1f));
                Assert.That(settings.FarMistY, Is.EqualTo(50f));
                Assert.That(settings.FarHeight, Is.EqualTo(9f));
                Assert.That(settings.FarY, Is.EqualTo(2f));
            }
            finally
            {
                Object.Destroy(fixture);
                Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator StepFeedbackSettings_DefaultAndClamp_WithoutChangingExistingTempo()
        {
            yield return null;
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                Assert.That(settings.StepDodgeDistance, Is.EqualTo(1.4f));
                Assert.That(settings.StepPressureDistance, Is.EqualTo(1.1f));
                Assert.That(settings.StepSlowMotionDuration, Is.EqualTo(.28f));
                Assert.That(settings.StepSlowMotionScale, Is.EqualTo(.25f));
                Assert.That(settings.StepFocusDuration, Is.EqualTo(.36f));
                Assert.That(settings.StepCameraZoom, Is.EqualTo(.5f));
                Assert.That(settings.StepBackdropDarkening, Is.EqualTo(.55f));
                JsonUtility.FromJsonOverwrite("{\"stepDodgeDistance\":-1,\"stepPressureDistance\":999," +
                    "\"stepSlowMotionDuration\":-1,\"stepSlowMotionScale\":0,\"stepFocusDuration\":999," +
                    "\"stepCameraZoom\":999,\"stepBackdropDarkening\":999,\"animationPlaybackSpeed\":0.8}", settings);
                Assert.That(settings.StepDodgeDistance, Is.Zero);
                Assert.That(settings.StepPressureDistance, Is.EqualTo(3f));
                Assert.That(settings.StepSlowMotionDuration, Is.Zero);
                Assert.That(settings.StepSlowMotionScale, Is.EqualTo(.05f));
                Assert.That(settings.StepFocusDuration, Is.EqualTo(2f));
                Assert.That(settings.StepCameraZoom, Is.EqualTo(1.5f));
                Assert.That(settings.StepBackdropDarkening, Is.EqualTo(.85f));
                Assert.That(settings.AnimationPlaybackSpeed, Is.EqualTo(.8f));
                Assert.That(settings.StepTimingWindow, Is.EqualTo(.1f));
            }
            finally { Object.Destroy(settings); }
        }

        private static GameObject CreateFixture(out Camera camera)
        {
            var fixture = new GameObject("Presentation Tuning Fixture");
            var cameraObject = new GameObject("Tuning Camera");
            cameraObject.transform.SetParent(fixture.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.aspect = 16f / 9f;
            camera.transform.localPosition = new Vector3(0f, -1.5f, -10f);
            return fixture;
        }

        private static List<SpriteRenderer[]> CaptureTiles(ForestParallaxBackdrop forest)
        {
            var pools = new List<SpriteRenderer[]>();
            foreach (ForestParallaxBackdrop.Layer layer in forest.Layers)
            {
                var tiles = new SpriteRenderer[layer.Tiles.Count];
                for (int index = 0; index < tiles.Length; index++) tiles[index] = layer.Tiles[index];
                pools.Add(tiles);
            }
            return pools;
        }

        private static void AssertSamePool(ForestParallaxBackdrop forest, List<SpriteRenderer[]> pools)
        {
            for (int layer = 0; layer < forest.Layers.Count; layer++)
            {
                Assert.That(forest.Layers[layer].Tiles.Count, Is.EqualTo(pools[layer].Length));
                for (int tile = 0; tile < pools[layer].Length; tile++)
                    Assert.That(forest.Layers[layer].Tiles[tile], Is.SameAs(pools[layer][tile]));
            }
        }

        private static void AssertGeometry(ForestParallaxBackdrop forest, float[] heights, float[] centers)
        {
            float[] factors = { .10f, .24f, 1f, 1.35f };
            int[] orders = { -7, -6, -5, 1 };
            string[] sources = { "forest-far-mist", "forest-far-trees", "forest-belt-mid", "forest-near" };
            Assert.That(forest.Layers.Count, Is.EqualTo(4));
            for (int index = 0; index < forest.Layers.Count; index++)
            {
                ForestParallaxBackdrop.Layer layer = forest.Layers[index];
                Assert.That(layer.Sprite, Is.SameAs(Resources.Load<Sprite>("ForestArena/" + sources[index])));
                Assert.That(layer.TileWidth, Is.EqualTo(heights[index] * 3f).Within(.01f));
                Assert.That(layer.ParallaxFactor, Is.EqualTo(factors[index]));
                foreach (SpriteRenderer tile in layer.Tiles)
                {
                    float scale = heights[index] / layer.Sprite.bounds.size.y;
                    Assert.That(Vector3.Distance(tile.transform.localScale, Vector3.one * scale), Is.LessThan(.001f),
                        "Resizing must also update inactive renderers before their next use.");
                    Assert.That(tile.sprite, Is.SameAs(layer.Sprite));
                    Assert.That(tile.sortingOrder, Is.EqualTo(orders[index]));
                    if (!tile.gameObject.activeSelf) continue;
                    Assert.That(tile.bounds.size.y, Is.EqualTo(heights[index]).Within(.01f));
                    Assert.That(tile.transform.localPosition.y, Is.EqualTo(centers[index]).Within(.001f));
                }
            }
        }

        private static void AssertHorizontalCoverage(ForestParallaxBackdrop forest, Camera camera)
        {
            foreach (ForestParallaxBackdrop.Layer layer in forest.Layers)
            {
                var active = new List<SpriteRenderer>();
                foreach (SpriteRenderer tile in layer.Tiles)
                    if (tile.gameObject.activeSelf) active.Add(tile);
                Assert.That(active.Count, Is.GreaterThanOrEqualTo(3));
                active.Sort((left, right) => left.bounds.min.x.CompareTo(right.bounds.min.x));
                float depth = Vector3.Dot(active[0].bounds.center - camera.transform.position, camera.transform.forward);
                float leftEdge = camera.ViewportToWorldPoint(new Vector3(0f, .5f, depth)).x;
                float rightEdge = camera.ViewportToWorldPoint(new Vector3(1f, .5f, depth)).x;
                Assert.That(active[0].bounds.min.x, Is.LessThanOrEqualTo(leftEdge + .01f));
                Assert.That(active[active.Count - 1].bounds.max.x, Is.GreaterThanOrEqualTo(rightEdge - .01f));
                for (int index = 1; index < active.Count; index++)
                {
                    Assert.That(active[index].bounds.min.x - active[index - 1].bounds.max.x, Is.LessThanOrEqualTo(.01f));
                    Assert.That(active[index].flipX, Is.Not.EqualTo(active[index - 1].flipX),
                        "Absolute negative tile indices must keep alternating edge mirrors after a width change.");
                }
            }
        }
    }
}
