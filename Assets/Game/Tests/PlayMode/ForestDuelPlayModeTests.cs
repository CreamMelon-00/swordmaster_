using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ForestDuelPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator Knockback_FollowsActualDamage_AndGuardReducesDisplacement()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                LegacyArenaView arena = controller.ArenaView;
                float light = MeasureEnemyPush(arena, 2, 0, false, 100);
                float heavy = MeasureEnemyPush(arena, 12, 0, false, 100);
                float resistance = MeasureEnemyPush(arena, 0, 12, false, 100);
                float guarded = MeasureEnemyPush(arena, 12, 0, true, 100);
                float blocked = MeasureEnemyPush(arena, 0, 0, true, 12);
                float miss = MeasureEnemyPush(arena, 0, 0, false, 0);
                Assert.That(light, Is.GreaterThan(0f));
                Assert.That(heavy, Is.GreaterThan(light), "Equal raw attack power must not hide the actual damage difference.");
                Assert.That(resistance, Is.EqualTo(heavy).Within(.01f), "Resistance damage also contributes to impact strength.");
                Assert.That(guarded, Is.GreaterThan(0f).And.LessThan(heavy));
                Assert.That(blocked, Is.GreaterThan(0f).And.LessThan(guarded), "A fully blocked strike is a small recoil, not a full launch.");
                Assert.That(miss, Is.EqualTo(0f).Within(.01f));
            }
            finally
            {
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        [UnityTest]
        public IEnumerator RepeatedHits_AccumulateUnfinishedPush_AndFightersNeverCross()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                LegacyArenaView arena = controller.ArenaView;
                arena.Reset();
                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                float start = arena.EnemyRenderer.transform.localPosition.x;
                arena.PresentHit(true, 8, 0, false, false, 8);
                float firstTarget = arena.EnemyKnockbackTarget.x;
                arena.Tick(LegacyArenaView.PushDuration * .25f, LegacyArenaView.PushDuration * .25f);
                Assert.That(arena.HasPendingPush, Is.True);
                arena.PresentHit(true, 8, 0, false, false, 8);
                Assert.That(arena.EnemyKnockbackTarget.x - firstTarget,
                    Is.EqualTo(firstTarget - start).Within(.01f), "The second strike must add to the previous endpoint, not overwrite its remaining movement.");
                arena.Tick(LegacyArenaView.PushDuration + .01f, LegacyArenaView.PushDuration + .01f);
                Assert.That(arena.EnemyRenderer.transform.localPosition.x - start, Is.GreaterThan(firstTarget - start));
                arena.PresentHit(false, 8, 0, false, false, 8);
                for (int index = 0; index < 60; index++)
                {
                    arena.Tick(.01f, .01f);
                    Assert.That(arena.Separation, Is.GreaterThanOrEqualTo(LegacyArenaView.ContactDistance - .01f),
                        "Simultaneous knockback and pursuit must keep the player on the left and the enemy on the right.");
                }
                Assert.That(arena.HasPendingPush, Is.False);
                arena.Reset();
                arena.CloseDistance(20f);
                Assert.That(arena.Separation, Is.EqualTo(LegacyArenaView.ContactDistance).Within(.01f),
                    "Even a large catch-up delta must stop at contact instead of collapsing both actors into one position.");
            }
            finally
            {
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        [UnityTest]
        public IEnumerator NextTurn_RetainsDisplacedPositions_AndRestartAloneRestoresSpawns()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                LegacyArenaView arena = controller.ArenaView;
                arena.Reset();
                arena.BeginSlot(LegacyInitialSkills.All[0], null);
                arena.PresentHit(true, 12, 0, false, false, 12);
                arena.Tick(LegacyArenaView.PushDuration + .01f, LegacyArenaView.PushDuration + .01f);
                Transform player = arena.PlayerRenderer.transform;
                Transform enemy = arena.EnemyRenderer.transform;
                // Translate an already displaced duel far from its spawns without changing combat data.
                player.localPosition += Vector3.right * 40f;
                enemy.localPosition += Vector3.right * 40f;
                Vector3 playerPosition = player.localPosition;
                Vector3 enemyPosition = enemy.localPosition;
                arena.EndTurn();
                arena.Tick(LegacyArenaView.ReturnDuration + .01f, LegacyArenaView.ReturnDuration + .01f);
                Assert.That(arena.ReturnComplete, Is.True);
                Assert.That(player.localPosition, Is.EqualTo(playerPosition));
                Assert.That(enemy.localPosition, Is.EqualTo(enemyPosition));
                arena.BeginTurn();
                arena.Tick(.1f, .1f);
                Assert.That(player.localPosition, Is.EqualTo(playerPosition), "Planning must not reset the ongoing battlefield.");
                Assert.That(enemy.localPosition, Is.EqualTo(enemyPosition));
                arena.BeginApproach();
                arena.Tick(.01f, .01f);
                Assert.That(arena.ApproachComplete, Is.False);
                float approachSpeed = LegacyArenaView.ApproachSpeed * controller.PresentationSettings.MovementSpeedMultiplier;
                Assert.That(Vector3.Distance(player.localPosition, playerPosition), Is.LessThanOrEqualTo(approachSpeed * .01f + .001f));
                Assert.That(Vector3.Distance(enemy.localPosition, enemyPosition), Is.LessThanOrEqualTo(approachSpeed * .01f + .001f));
                arena.Tick(1f, 1f);
                Assert.That(arena.ApproachComplete, Is.True);
                Assert.That(arena.Separation, Is.EqualTo(LegacyArenaView.ContactDistance).Within(.01f));
                controller.RestartMatch();
                Assert.That(player.localPosition, Is.EqualTo(new Vector3(-5f, -.5f, 0f)));
                Assert.That(enemy.localPosition, Is.EqualTo(new Vector3(5f, -.5f, 0f)));
                Assert.That(arena.HasPendingPush, Is.False);
                Assert.That(arena.IsPursuing, Is.False);
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(6f));
            }
            finally
            {
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        [UnityTest]
        public IEnumerator Camera_FollowsDistantDuelCenters_DuringPlanningAndCombat()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                LegacyArenaView arena = controller.ArenaView;
                arena.Reset();
                arena.PlayerRenderer.transform.localPosition = new Vector3(47f, -.5f, 0f);
                arena.EnemyRenderer.transform.localPosition = new Vector3(53f, -.5f, 0f);
                arena.BeginTurn();
                arena.SetPlanningState(10f, false);
                SettleCamera(arena);
                Assert.That(arena.ArenaCamera.transform.localPosition.x, Is.EqualTo(arena.DuelCenter.x).Within(.01f));
                Assert.That(arena.ArenaCamera.transform.localPosition.y, Is.EqualTo(arena.DuelCenter.y - 1f).Within(.01f));
                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                SettleCamera(arena);
                Assert.That(arena.ArenaCamera.transform.localPosition.x, Is.EqualTo(arena.DuelCenter.x).Within(.01f),
                    "Combat must not add the last planning pivot to the world midpoint a second time.");
                arena.PlayerRenderer.transform.localPosition = new Vector3(-57f, -.5f, 0f);
                arena.EnemyRenderer.transform.localPosition = new Vector3(-51f, -.5f, 0f);
                arena.BeginTurn();
                SettleCamera(arena);
                Assert.That(arena.ArenaCamera.transform.localPosition.x, Is.EqualTo(arena.DuelCenter.x).Within(.01f));
            }
            finally
            {
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        [UnityTest]
        public IEnumerator Forest_LoadsFourDistinctSprites_AndEachLayerActuallyParallaxes()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                controller.RestartMatch();
                LegacyArenaView arena = controller.ArenaView;
                ForestParallaxBackdrop forest = arena.ForestBackdrop;
                Assert.That(forest, Is.Not.Null);
                Assert.That(forest.HasRequiredAssets, Is.True);
                Assert.That(forest.Layers.Count, Is.EqualTo(4));
                string[] names = { "forest-far-mist", "forest-far-trees", "forest-belt-mid", "forest-near" };
                float[] factors = { .10f, .24f, 1f, 1.35f };
                var sprites = new HashSet<Sprite>();
                var textures = new HashSet<Texture>();
                var positions = new float[forest.Layers.Count];
                Camera camera = arena.ArenaCamera;
                camera.transform.localPosition = new Vector3(0f, -1.5f, -10f);
                forest.Reset(camera);
                for (int index = 0; index < forest.Layers.Count; index++)
                {
                    ForestParallaxBackdrop.Layer layer = forest.Layers[index];
                    Sprite source = Resources.Load<Sprite>("ForestArena/" + names[index]);
                    Assert.That(source, Is.Not.Null);
                    Assert.That(layer.Sprite, Is.SameAs(source));
                    Assert.That(sprites.Add(source), Is.True);
                    Assert.That(textures.Add(source.texture), Is.True);
                    Assert.That(layer.ParallaxFactor, Is.EqualTo(factors[index]).Within(.001f));
                    Assert.That(layer.TileWidth, Is.GreaterThan(1f));
                    Assert.That(layer.Tiles.Count, Is.GreaterThanOrEqualTo(3));
                    foreach (SpriteRenderer tile in layer.Tiles) Assert.That(tile.sprite, Is.SameAs(source));
                    positions[index] = layer.Tiles[0].transform.localPosition.x;
                }
                camera.transform.localPosition += Vector3.right;
                forest.Tick(camera, false, 0f);
                for (int index = 0; index < forest.Layers.Count; index++)
                    Assert.That(forest.Layers[index].Tiles[0].transform.localPosition.x - positions[index],
                        Is.EqualTo(1f - factors[index]).Within(.01f), "Moving the camera must move each actual sprite layer at its own factor.");
            }
            finally
            {
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        [UnityTest]
        public IEnumerator Forest_HorizontalLoopCoversWideProjectedRangeAndLongTravel()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            Camera camera = controller.ArenaView.ArenaCamera;
            float cameraAspect = camera.aspect;
            try
            {
                controller.RestartMatch();
                ForestParallaxBackdrop forest = controller.ArenaView.ForestBackdrop;
                Assert.That(forest, Is.Not.Null);
                Assert.That(forest.HasRequiredAssets, Is.True);
                camera.aspect = 32f / 9f;
                // Size 20 stresses the horizontal tile pool only. This does not
                // claim vertical coverage or visual support for every camera angle.
                // The runtime uses planning size <= 6 with no rotation and combat
                // size 3.5. The separate runtime coverage test exercises 17 degrees.
                // The opaque far layer is 14.4 units high; smaller ground/fern
                // layers need not cover the sky.
                camera.orthographicSize = 20f;
                camera.transform.localRotation = Quaternion.Euler(0f, 0f, 17f);
                float maximumWidth = 0f;
                foreach (ForestParallaxBackdrop.Layer layer in forest.Layers) maximumWidth = Mathf.Max(maximumWidth, layer.TileWidth);
                forest.Tick(camera, false, .1f);
                var poolCounts = new int[forest.Layers.Count];
                var pooledTiles = new HashSet<SpriteRenderer>();
                for (int index = 0; index < forest.Layers.Count; index++)
                {
                    poolCounts[index] = forest.Layers[index].Tiles.Count;
                    foreach (SpriteRenderer tile in forest.Layers[index].Tiles) pooledTiles.Add(tile);
                }
                float[] travel = { 0f, maximumWidth * 5.25f, -maximumWidth * 6.5f, maximumWidth * 20f, 0f };
                foreach (float x in travel)
                {
                    camera.transform.localPosition = new Vector3(x, -1.5f, -10f);
                    forest.Tick(camera, false, .1f);
                    AssertForestCoverage(forest, camera);
                    for (int index = 0; index < forest.Layers.Count; index++)
                    {
                        Assert.That(forest.Layers[index].Tiles.Count, Is.EqualTo(poolCounts[index]),
                            "Traversal at the same viewport size must recycle, not allocate, tiles.");
                        foreach (SpriteRenderer tile in forest.Layers[index].Tiles) Assert.That(pooledTiles.Contains(tile), Is.True);
                    }
                }
                camera.orthographicSize = 6f;
                forest.Tick(camera, false, .1f);
                AssertForestCoverage(forest, camera);
                for (int index = 0; index < forest.Layers.Count; index++)
                    Assert.That(forest.Layers[index].Tiles.Count, Is.EqualTo(poolCounts[index]), "A smaller viewport must retain its reusable pool.");
            }
            finally
            {
                camera.aspect = cameraAspect;
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        [UnityTest]
        public IEnumerator BeltForest_ReducesLayerGeometry_AndKeepsActorFeetInsideTheGroundBand()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                controller.RestartMatch();
                LegacyArenaView arena = controller.ArenaView;
                ForestParallaxBackdrop forest = arena.ForestBackdrop;
                Assert.That(forest.HasRequiredAssets, Is.True);
                float[] heights = { 14.4f, 14.4f, 12.6f, 12.6f };
                float[] centers = { -.5f, -.5f, -1.2f, -.75f };
                int[] orders = { -7, -6, -5, 1 };
                for (int index = 0; index < forest.Layers.Count; index++)
                {
                    ForestParallaxBackdrop.Layer layer = forest.Layers[index];
                    Assert.That(layer.Sprite.rect.width / layer.Sprite.rect.height, Is.EqualTo(3f).Within(.001f));
                    Assert.That(layer.TileWidth, Is.EqualTo(heights[index] * 3f).Within(.01f));
                    foreach (SpriteRenderer tile in layer.Tiles)
                    {
                        if (!tile.gameObject.activeSelf) continue;
                        Assert.That(tile.bounds.size.y, Is.EqualTo(heights[index]).Within(.01f),
                            "Background detail should be smaller without scaling the inherited actors.");
                        Assert.That(tile.bounds.size.x, Is.EqualTo(layer.TileWidth).Within(.01f));
                        Assert.That(tile.transform.localPosition.y, Is.EqualTo(centers[index]).Within(.001f));
                        Assert.That(tile.sortingOrder, Is.EqualTo(orders[index]));
                    }
                }
                Bounds ground = FirstActiveTile(forest.Layers[2]).bounds;
                // The new belt floor begins around 47% down from the image and
                // extends to its bottom. Geometry verifies the agreed band, not
                // bitmap opacity or the artistic readability of its perspective.
                float groundBack = ground.max.y - ground.size.y * .47f;
                float groundFront = ground.min.y;
                foreach (SpriteRenderer actor in new[] { arena.PlayerRenderer, arena.EnemyRenderer })
                {
                    Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(actor.sprite.pixelsPerUnit, Is.EqualTo(18f));
                    Assert.That(actor.bounds.min.y, Is.InRange(groundFront, groundBack),
                        "Both idle actors' feet must sit inside the broad belt floor, not on a distant narrow ledge.");
                }
            }
            finally
            {
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        [UnityTest]
        public IEnumerator BeltForest_RuntimeCameraRangesKeepHorizontalCoverageAndOpaqueFarHeight()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            Camera camera = controller.ArenaView.ArenaCamera;
            float originalAspect = camera.aspect;
            try
            {
                controller.RestartMatch();
                ForestParallaxBackdrop forest = controller.ArenaView.ForestBackdrop;
                Assert.That(forest.HasRequiredAssets, Is.True);
                float[] aspects = { 16f / 9f, 32f / 9f };
                // Orthographic size, local camera Y, Z rotation: actual planning/combat views.
                Vector3[] views = { new Vector3(6f, -1.5f, 0f), new Vector3(3.5f, -.5f, 17f) };
                float travelWidth = forest.Layers[0].TileWidth;
                float[] positions = { 0f, travelWidth * 5.25f, -travelWidth * 6.5f };
                foreach (float aspect in aspects)
                    foreach (Vector3 view in views)
                        foreach (float x in positions)
                        {
                            camera.aspect = aspect;
                            camera.orthographicSize = view.x;
                            camera.transform.localPosition = new Vector3(x, view.y, -10f);
                            camera.transform.localRotation = Quaternion.Euler(0f, 0f, view.z);
                            forest.Tick(camera, false, 0f);
                            AssertForestCoverage(forest, camera);
                            SpriteRenderer far = FirstActiveTile(forest.Layers[0]);
                            Assert.That(far.transform.localPosition.y, Is.EqualTo(-.5f).Within(.001f),
                                "The distant forest has a fixed world height, not a camera-Y-following backdrop.");
                            float depth = Vector3.Dot(far.bounds.center - camera.transform.position, camera.transform.forward);
                            foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                            {
                                Vector3 worldCorner = camera.ViewportToWorldPoint(new Vector3(corner.x, corner.y, depth));
                                Assert.That(worldCorner.y, Is.InRange(far.bounds.min.y - .01f, far.bounds.max.y + .01f),
                                    "The opaque far layer must cover the real camera's top and bottom, including tilted corners.");
                            }
                        }
            }
            finally
            {
                camera.aspect = originalAspect;
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        [UnityTest]
        public IEnumerator DamageNumbers_ReprojectTheirWorldImpactPoint_WithoutAdvancingAtDeltaZero()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                controller.RestartMatch();
                LegacyArenaView arena = controller.ArenaView;
                Camera camera = arena.ArenaCamera;
                Transform root = controller.Hud.Root.transform;
                Canvas canvas = root.GetComponent<Canvas>();
                RefreshHud(controller, 0f);
                Vector3 impactPosition = new Vector3(2f, .4f, 0f);
                controller.Hud.ShowHitDamage(false, 7, impactPosition);
                Text damageText = null;
                foreach (Transform child in root)
                {
                    if (child.name != "Damage" || !child.gameObject.activeSelf) continue;
                    Assert.That(damageText, Is.Null, "Only the newly spawned number should be active after a reset.");
                    damageText = child.GetComponent<Text>();
                }
                Assert.That(damageText, Is.Not.Null);
                RefreshHud(controller, .2f);
                Vector2 firstPosition = damageText.rectTransform.anchoredPosition;
                Vector3 firstScreen = camera.WorldToScreenPoint(impactPosition);
                Vector2 animationOffset = firstPosition -
                    new Vector2(firstScreen.x, firstScreen.y) / Mathf.Max(.001f, canvas.scaleFactor);
                Vector3 size = damageText.transform.localScale;
                Color color = damageText.color;
                camera.transform.localPosition += new Vector3(3f, 1f, 0f);
                camera.orthographicSize = 4f;
                camera.transform.localRotation = Quaternion.Euler(0f, 0f, 17f);
                RefreshHud(controller, 0f);
                Vector3 screenPosition = camera.WorldToScreenPoint(impactPosition);
                Vector2 expectedPosition = new Vector2(screenPosition.x, screenPosition.y) /
                    Mathf.Max(.001f, canvas.scaleFactor) + animationOffset;
                Assert.That(damageText.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(damageText.rectTransform.anchorMax, Is.EqualTo(Vector2.zero));
                Assert.That(Vector2.Distance(damageText.rectTransform.anchoredPosition, expectedPosition), Is.LessThan(.01f),
                    "At zero delta, an impact number must reproject its world point while preserving its current rise/stagger offset.");
                Assert.That(Vector2.Distance(damageText.rectTransform.anchoredPosition, firstPosition), Is.GreaterThan(1f));
                Assert.That(damageText.text, Is.EqualTo("7"));
                Assert.That(damageText.transform.localScale, Is.EqualTo(size));
                Assert.That(damageText.color, Is.EqualTo(color));
                // An ordinary number lasts 1.05 real seconds. Its .85 remaining seconds
                // must survive the zero-delta reproject, checked through expiration.
                RefreshHud(controller, .849f);
                Assert.That(damageText.gameObject.activeSelf, Is.True);
                RefreshHud(controller, .002f);
                Assert.That(damageText.gameObject.activeSelf, Is.False);
            }
            finally
            {
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        private static DuelPrototypeController FindPrototype()
        {
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            return controller;
        }

        private static float MeasureEnemyPush(LegacyArenaView arena, int hpDamage, int resistanceDamage, bool guarded, int rawPower)
        {
            arena.Reset();
            arena.BeginSlot(LegacyInitialSkills.All[0], null);
            float start = arena.EnemyRenderer.transform.localPosition.x;
            arena.PresentHit(true, hpDamage, resistanceDamage, guarded, false, rawPower);
            arena.Tick(LegacyArenaView.PushDuration + .01f, LegacyArenaView.PushDuration + .01f);
            return arena.EnemyRenderer.transform.localPosition.x - start;
        }

        private static void SettleCamera(LegacyArenaView arena)
        {
            // Camera smoothing is on real time; zero scaled time leaves actor positions untouched.
            arena.Tick(0f, 1f);
            arena.Tick(0f, 1f);
        }

        private static void RefreshHud(DuelPrototypeController controller, float delta)
        {
            LegacyArenaView arena = controller.ArenaView;
            controller.Hud.Refresh(controller.Session, controller.TurnTimeRemaining, false, -1, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform, delta, delta);
            Canvas.ForceUpdateCanvases();
        }

        private static SpriteRenderer FirstActiveTile(ForestParallaxBackdrop.Layer layer)
        {
            foreach (SpriteRenderer tile in layer.Tiles)
                if (tile.gameObject.activeSelf) return tile;
            Assert.Fail("A loaded forest layer must contain an active tile.");
            return null;
        }

        private static void AssertForestCoverage(ForestParallaxBackdrop forest, Camera camera)
        {
            float angle = camera.transform.eulerAngles.z * Mathf.Deg2Rad;
            float halfWidth = camera.orthographicSize *
                (camera.aspect * Mathf.Abs(Mathf.Cos(angle)) + Mathf.Abs(Mathf.Sin(angle)));
            foreach (ForestParallaxBackdrop.Layer layer in forest.Layers)
            {
                var active = new List<SpriteRenderer>();
                foreach (SpriteRenderer tile in layer.Tiles)
                    if (tile.gameObject.activeSelf) active.Add(tile);
                Assert.That(active.Count, Is.GreaterThanOrEqualTo(3));
                active.Sort((left, right) => left.bounds.min.x.CompareTo(right.bounds.min.x));
                Assert.That(active[0].bounds.min.x, Is.LessThanOrEqualTo(camera.transform.position.x - halfWidth + .01f));
                Assert.That(active[active.Count - 1].bounds.max.x, Is.GreaterThanOrEqualTo(camera.transform.position.x + halfWidth - .01f));
                for (int index = 1; index < active.Count; index++)
                    Assert.That(active[index].bounds.min.x - active[index - 1].bounds.max.x, Is.LessThanOrEqualTo(.01f),
                        "Adjacent repeated sprites must cover the viewport without horizontal gaps.");
            }
        }
    }
}
