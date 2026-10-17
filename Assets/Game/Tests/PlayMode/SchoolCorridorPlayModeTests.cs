using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
        public IEnumerator WholeDepthBand_StaysOnTheFloorAndAboveTheHud_AtWideAndNarrowViewports()
        {
            yield return null;
            using (var scope = new FloorBandScope())
            {
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(960, 720) })
                {
                    scope.Resize(size);
                    scope.ResetPlanning();
                    yield return null;
                    scope.Settle(false);
                    scope.Capture(size.x + "x" + size.y + "-planning");
                    AssertFloorBand(scope);
                    scope.BeginCombat();
                    scope.Settle(true);
                    scope.Capture(size.x + "x" + size.y + "-combat");
                    AssertFloorBand(scope);
                }
            }
            yield return null;
        }

        private static void AssertFloorBand(FloorBandScope scope)
        {
            LegacyArenaView arena = scope.Arena;
            Camera camera = arena.ArenaCamera;
            SpriteRenderer tile = arena.CorridorBackdrop.Layers[2].Tiles[0];
            float floorSeamY = tile.bounds.max.y - tile.bounds.size.y * (592f / 916f);
            var actors = new[] { arena.PlayerRenderer, arena.GetEnemyRenderer(0), arena.GetEnemyRenderer(1), arena.GetEnemyRenderer(2) };
            Assert.That(actors[1].transform.localPosition.y, Is.EqualTo(arena.ActorGroundY + 1.2f).Within(.001f));
            Assert.That(actors[3].transform.localPosition.y, Is.EqualTo(arena.ActorGroundY - 1.2f).Within(.001f));
            for (int index = 0; index < actors.Length; index++)
            {
                SpriteRenderer actor = actors[index];
                Vector3 feet = actor.transform.TransformPoint(0f, MobStudentAnimationSet.GroundOffset, 0f);
                Assert.That(feet.y, Is.LessThan(floorSeamY - .60f),
                    "Even the upper travel limit must place the actual feet visibly below the wall-floor seam. " + index);
                Transform circle = index == 0 ? arena.PlayerGroundCircle : arena.GetEnemyGroundCircle(index - 1);
                Assert.That(Vector3.Distance(circle.position, feet), Is.LessThan(.001f));
                Assert.That(circle.position.y, Is.InRange(tile.bounds.min.y, floorSeamY - .60f));
                SpriteRenderer shadow = actor.transform.Find("Original Ground Shadow").GetComponent<SpriteRenderer>();
                Vector3 footOnScreen = camera.WorldToViewportPoint(feet);
                Vector3 shadowBottom = camera.WorldToViewportPoint(new Vector3(shadow.bounds.center.x, shadow.bounds.min.y, 0f));
                if (!scope.IsCombat)
                    Assert.That(footOnScreen.x, Is.InRange(.015f, .985f), "The wide planning shot keeps every member in view.");
                Assert.That(shadowBottom.y, Is.GreaterThan(scope.Hud.QueuePanelFloor + .005f),
                    "The lowest edge of the movable floor remains visible above the skill dock. " + index);
            }
            // Only the opaque interior needs to cover the floor; the outdoor layers remain behind its windows.
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (SpriteRenderer part in arena.CorridorBackdrop.Layers[2].Tiles)
                if (part.gameObject.activeInHierarchy) { bottom = Mathf.Min(bottom, part.bounds.min.y); top = Mathf.Max(top, part.bounds.max.y); }
            foreach (SpriteRenderer part in arena.CorridorBackdrop.FloorExtensions)
                if (part.gameObject.activeInHierarchy) { bottom = Mathf.Min(bottom, part.bounds.min.y); top = Mathf.Max(top, part.bounds.max.y); }
            foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
            {
                float y = camera.ViewportToWorldPoint(new Vector3(corner.x, corner.y, 10f)).y;
                Assert.That(y, Is.InRange(bottom - .01f, top + .01f),
                    "The lowered camera must still have painted corridor coverage at every viewport edge.");
            }
        }

        private sealed class FloorBandScope : IDisposable
        {
            private readonly GameObject root = new GameObject("Corridor Floor Band Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private RenderTexture target;
            public LegacyArenaView Arena { get; }
            public LegacyCombatHud Hud { get; }
            private LegacyQueuedDuel duel;
            public bool IsCombat { get; private set; }
            private static LegacySkill Attack() => new LegacySkill(98611, "바닥 검증", 0, 5, 5,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);
            public FloorBandScope()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                JsonUtility.FromJsonOverwrite("{\"battleDriftSpeed\":0}", settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                Arena.Reset();
                Arena.ConfigureEnemyRoster(new[] { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student });
                Hud = new LegacyCombatHud(root.transform, art, _ => { }, () => { }, () => { }, settings);
                Hud.SetEnemyActors(Arena.EnemyActorTransforms);
            }
            public void Resize(Vector2Int size)
            {
                Camera camera = Arena.ArenaCamera;
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target; camera.aspect = size.x / (float)size.y;
                Canvas canvas = Hud.Root.GetComponent<Canvas>(); CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(1f, camera.nearClipPlane + .1f);
                float logX = Mathf.Log(size.x / scaler.referenceResolution.x, 2f), logY = Mathf.Log(size.y / scaler.referenceResolution.y, 2f);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logX, logY, scaler.matchWidthOrHeight));
            }
            public void ResetPlanning()
            {
                IsCombat = false;
                Arena.Reset();
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-3f, Arena.ActorGroundY - 1.2f, 0f);
                Arena.GetEnemyRenderer(0).transform.localPosition = new Vector3(2.5f, Arena.ActorGroundY + 1.2f, 0f);
                Arena.GetEnemyRenderer(1).transform.localPosition = new Vector3(4.5f, Arena.ActorGroundY, 0f);
                Arena.GetEnemyRenderer(2).transform.localPosition = new Vector3(6.5f, Arena.ActorGroundY - 1.2f, 0f);
                var script = new EnemyScript(new[] { new[] { Attack() } });
                duel = new LegacyQueuedDuel(1000, 1000, new[] { Attack() }, new[]
                {
                    new LegacyEnemyDefinition("견습생", 1000, 1000, script),
                    new LegacyEnemyDefinition("검술생", 1000, 1000, script),
                    new LegacyEnemyDefinition("수련생", 1000, 1000, script)
                }, randomSeed: 7, meshPercent: 0);
                Assert.That(duel.TryQueueLane(0), Is.True);
                Hud.BeginTurn();
            }
            public void BeginCombat()
            {
                duel.Commit(); duel.BeginNextSlot();
                IsCombat = true;
                Arena.SelectActiveEnemy(duel.ActiveEnemyIndex);
                Arena.BeginSlot(duel.CurrentSlot.PlayerSkill, duel.CurrentSlot.EnemySkill);
                Hud.EndTurn(); Hud.BeginCombat();
            }
            public void Settle(bool combat)
            {
                for (int frame = 0; frame < 90; frame++)
                {
                    if (combat) Arena.SetCombatShot(.5f, true, true, false);
                    else Arena.SetPlanningState(10f, false, true);
                    Arena.SetHudFloor(Hud.QueuePanelFloor);
                    Arena.Tick(0f, .02f);
                    Hud.SetCombatLetterboxAccent(Arena.CombatLetterboxAccent);
                    Hud.Refresh(duel, 10f, combat, combat ? 0 : -1, Arena.ArenaCamera,
                        Arena.PlayerRenderer.transform, Arena.EnemyRenderer.transform, .02f, .02f);
                    Canvas.ForceUpdateCanvases();
                }
            }
            public void Capture(string name)
            {
                string output = Environment.GetEnvironmentVariable("SWORDMASTER_FLOOR_BAND_CAPTURE_DIR");
                if (string.IsNullOrEmpty(output)) return;
                Directory.CreateDirectory(output);
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Camera camera = Arena.ArenaCamera; camera.cullingMask = 1 << 31;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                Canvas.ForceUpdateCanvases(); camera.Render();
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; Object.Destroy(texture); }
            }
            public void Dispose()
            {
                Arena.ArenaCamera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                Hud.Dispose(); Arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(root);
                if (scene != null) scene.enabled = sceneEnabled;
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
                Assert.That(arena.ActorGroundY, Is.EqualTo(-2.30f).Within(.001f));

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
                            // The cels keep a transparent margin (22 px) under the feet, so the sprite's bounds sit below
                            // them; the feet are at the authored ground point, GroundOffset under the actor, as its shadow is.
                            float feetY = actor.transform.TransformPoint(0f, MobStudentAnimationSet.GroundOffset, 0f).y;
                            Assert.That(actor.bounds.min.y, Is.LessThan(feetY));
                            Assert.That(actor.bounds.max.y, Is.GreaterThan(feetY));
                            Assert.That(feetY, Is.InRange(floorSeamY - 2.10f, floorSeamY - 1.60f),
                                "The actor's feet should stand visibly inside the marble floor.");
                            Transform shadow = actor.transform.Find("Original Ground Shadow");
                            Assert.That(shadow, Is.Not.Null);
                            Assert.That(shadow.position.y, Is.LessThan(floorSeamY - 1.60f),
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
                arena.SetPlanningState(10f, false);
                arena.Tick(0f, 2f);
                Vector3 normalPlanningPosition = camera.transform.localPosition;
                float normalPlanningSize = camera.orthographicSize;
                arena.SetPlanningState(10f, true);
                arena.Tick(0f, 2f);
                Assert.That(Vector3.Distance(camera.transform.localPosition, normalPlanningPosition), Is.LessThan(.001f),
                    "Queue inspection preserves the corridor's normal planning framing at the same clock and floor.");
                Assert.That(camera.orthographicSize, Is.EqualTo(normalPlanningSize).Within(.001f));
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
