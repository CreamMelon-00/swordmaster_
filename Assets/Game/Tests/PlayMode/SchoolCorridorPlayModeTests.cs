using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SchoolCorridorPlayModeTests
    {
        [UnityTest]
        public IEnumerator PaddedCorridor_CoversDefaultAndWideTiltedCameraAfterLongTravel()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            Camera camera = controller.ArenaView.ArenaCamera;
            float originalAspect = camera.aspect;
            try
            {
                controller.RestartMatch();
                SchoolCorridorBackdrop corridor = controller.ArenaView.CorridorBackdrop;
                Assert.That(corridor.HasRequiredAssets, Is.True);
                float travel = corridor.Layers[0].TileWidth;
                float[] aspects = { 16f / 9f, 32f / 9f };
                Vector3[] views = { new Vector3(6f, -1.5f, 0f), new Vector3(3.5f, -.5f, 17f) };
                float[] positions = { 0f, travel * 5.25f, -travel * 6.5f };
                foreach (float aspect in aspects)
                    foreach (Vector3 view in views)
                        foreach (float x in positions)
                        {
                            camera.aspect = aspect;
                            camera.orthographicSize = view.x;
                            camera.transform.localPosition = new Vector3(x, view.y, -10f);
                            camera.transform.localRotation = Quaternion.Euler(0f, 0f, view.z);
                            corridor.Tick(camera, false, 0f);
                            foreach (SchoolCorridorBackdrop.Layer layer in corridor.Layers)
                            {
                                float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                                foreach (SpriteRenderer tile in layer.Tiles)
                                {
                                    if (!tile.gameObject.activeSelf) continue;
                                    Bounds bounds = tile.bounds;
                                    minX = Mathf.Min(minX, bounds.min.x);
                                    maxX = Mathf.Max(maxX, bounds.max.x);
                                    minY = Mathf.Min(minY, bounds.min.y);
                                    maxY = Mathf.Max(maxY, bounds.max.y);
                                }
                                foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                                {
                                    Vector3 world = camera.ViewportToWorldPoint(new Vector3(corner.x, corner.y, 10f));
                                    Assert.That(world.x, Is.InRange(minX - .01f, maxX + .01f), layer.Sprite.name + " horizontal coverage");
                                    Assert.That(world.y, Is.InRange(minY - .01f, maxY + .01f), layer.Sprite.name + " vertical coverage");
                                }
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
        public IEnumerator LaterBattlesUseLayeredCorridor_WhileTheOpeningMissionKeepsTheForest()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                controller.RestartMatch();
                LegacyArenaView arena = controller.ArenaView;
                Assert.That(arena.BackdropKind, Is.EqualTo(ArenaBackdropKind.SchoolCorridor));
                Assert.That(arena.ForestBackdrop.IsVisible, Is.False);
                Assert.That(arena.CorridorBackdrop.IsVisible, Is.True);
                Assert.That(arena.CorridorBackdrop.HasRequiredAssets, Is.True);
                Assert.That(arena.CorridorBackdrop.Layers.Count, Is.EqualTo(3));
                Assert.That(arena.ActorGroundY, Is.EqualTo(-1.45f).Within(.001f));

                string[] names = { "corridor-far", "corridor-near", "corridor-interior" };
                float[] factors = { .12f, .35f, 1f };
                float[] initialX = new float[names.Length];
                float expectedWorldHeight = 916f * 12.4f / 724f;
                float expectedCenterY = -1.5f + (160f - 32f) * 12.4f / 724f * .5f;
                Camera camera = arena.ArenaCamera;
                for (int index = 0; index < names.Length; index++)
                {
                    SchoolCorridorBackdrop.Layer layer = arena.CorridorBackdrop.Layers[index];
                    Sprite source = Resources.Load<Sprite>("SchoolCorridor/" + names[index]);
                    Assert.That(layer.Sprite, Is.SameAs(source));
                    Assert.That(source, Is.Not.Null);
                    Assert.That(source.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(source.rect.size, Is.EqualTo(new Vector2(2172f, 916f)));
                    Assert.That(layer.ParallaxFactor, Is.EqualTo(factors[index]).Within(.001f));
                    Assert.That(layer.Tiles.Count, Is.GreaterThanOrEqualTo(3));
                    SpriteRenderer tile = layer.Tiles[0];
                    Assert.That(tile.transform.localScale.y * source.rect.height / source.pixelsPerUnit,
                        Is.EqualTo(expectedWorldHeight).Within(.01f));
                    Assert.That(tile.transform.localPosition.y, Is.EqualTo(expectedCenterY).Within(.001f));
                    Assert.That(layer.TileWidth, Is.EqualTo(37.2f).Within(.01f),
                        "Vertical padding must not change the horizontal artwork scale.");
                    if (index == 2)
                    {
                        Assert.That(tile.bounds.min.y, Is.LessThanOrEqualTo(camera.transform.position.y - camera.orthographicSize),
                            "The interior floor covers the bottom of the default camera.");
                        Assert.That(tile.bounds.max.y, Is.GreaterThanOrEqualTo(camera.transform.position.y + camera.orthographicSize),
                            "The window arches cover the top of the default camera.");
                        float floorSeamY = tile.bounds.max.y - expectedWorldHeight * (592f / 916f);
                        foreach (SpriteRenderer actor in new[] { arena.PlayerRenderer, arena.EnemyRenderer })
                        {
                            Assert.That(actor.bounds.min.y, Is.InRange(floorSeamY - 1.25f, floorSeamY - .75f),
                                "The actor's feet should stand visibly inside the marble floor.");
                            Transform shadow = actor.transform.Find("Original Ground Shadow");
                            Assert.That(shadow, Is.Not.Null);
                            Assert.That(shadow.position.y, Is.LessThan(floorSeamY - .75f),
                                "The ground shadow should move with the actor instead of staying at the wall.");
                        }
                    }
                    initialX[index] = tile.transform.localPosition.x;
                }
                arena.Tick(0f, 1f);
                Assert.That(camera.transform.localPosition.y, Is.EqualTo(-1.5f).Within(.01f),
                    "Moving only the actors forward must keep the corridor framing unchanged.");
                camera.transform.localPosition += Vector3.right;
                arena.TickBackdrop(camera, false, 0f);
                for (int index = 0; index < names.Length; index++)
                    Assert.That(arena.CorridorBackdrop.Layers[index].Tiles[0].transform.localPosition.x - initialX[index],
                        Is.EqualTo(1f - factors[index]).Within(.01f));
                arena.SetPlanningState(10f, true);
                arena.Tick(0f, 1f);
                Assert.That(camera.transform.localPosition.y,
                    Is.EqualTo(arena.EnemyRenderer.transform.localPosition.y + .5f).Within(.01f),
                    "An inspected fighter stays fully framed when the camera zooms in.");
                arena.BeginApproach();
                arena.Tick(0f, 1f);
                foreach (SpriteRenderer actor in new[] { arena.PlayerRenderer, arena.EnemyRenderer })
                {
                    SpriteRenderer shadow = actor.transform.Find("Original Ground Shadow").GetComponent<SpriteRenderer>();
                    Assert.That(shadow.bounds.min.y,
                        Is.GreaterThan(camera.transform.localPosition.y - camera.orthographicSize + .05f),
                        "The closer combat camera must not crop the moved ground shadows.");
                }

                controller.ShowTitle();
                controller.StartNewGame();
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(1));
                Assert.That(controller.StartMission(), Is.True);
                Assert.That(arena.BackdropKind, Is.EqualTo(ArenaBackdropKind.Forest));
                Assert.That(arena.ForestBackdrop.IsVisible, Is.True);
                Assert.That(arena.CorridorBackdrop.IsVisible, Is.False);
                Assert.That(arena.ActorGroundY, Is.EqualTo(-.5f).Within(.001f));
                Assert.That(arena.PlayerRenderer.transform.localPosition.y, Is.EqualTo(-.5f).Within(.001f));
                Assert.That(arena.EnemyRenderer.transform.localPosition.y, Is.EqualTo(-.5f).Within(.001f));
                Assert.That(Resources.Load<Sprite>(LobbyMissions.All[0].BackgroundResource), Is.Not.Null,
                    "The first post-prologue briefing uses the opaque corridor composite.");
            }
            finally
            {
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }
    }
}
