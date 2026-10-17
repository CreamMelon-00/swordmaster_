using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Framing requests read the battle, but cannot become a second attack or move its reservation queue.</summary>
    public sealed class CombatCameraDramaPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator WindupToStrike_FollowsTheSwordAndPushesIn_WithVisibleNativeCameraAndLetterboxChanges()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.StartExchange();
                scope.AdvanceShot(.12f, 16);
                Snapshot windup = scope.Read("windup");
                Color32[] first = scope.Capture("01-windup");
                scope.AdvanceShot(.50f, 16);
                Snapshot strike = scope.Read("strike");
                Color32[] second = scope.Capture("02-strike");
                scope.AdvanceShot(.82f, 16);
                Snapshot follow = scope.Read("follow-through");
                scope.Capture("03-follow-through");

                Assert.That(strike.camera.x - windup.camera.x, Is.GreaterThan(.15f),
                    "The lens travels from the attacker's preparation toward the contact, even when both ground points are planted.");
                Assert.That(windup.size - strike.size, Is.GreaterThan(.1f), "The strike has a deliberate push-in, not only shake.");
                Assert.That(strike.shot, Is.GreaterThan(.2f));
                Assert.That(strike.letterboxInset, Is.GreaterThan(1f).And.LessThanOrEqualTo(36f));
                Assert.That(follow.camera.x, Is.GreaterThan(windup.camera.x + .15f), "Follow-through holds the directed motion before returning.");
                Assert.That(ChangedPixels(first, second), Is.GreaterThan(500), "Native rendered frames visibly change, including the bands.");
                Assert.That(scope.Duel.CurrentSlot.HitsResolved, Is.Zero, "Camera previews cannot execute the held exchange.");
                Assert.That(scope.Duel.Player.Health, Is.EqualTo(1000));
                Assert.That(scope.Duel.Enemy.Health, Is.EqualTo(1000));
                AssertPairOnScreen(scope.Arena);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrioExchange_FramesTheActivePairInsteadOfRemoteWaitingBodies_AtWideAndNarrowAspects()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.Arena.ConfigureEnemyRoster(new[] { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student });
                scope.Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, scope.Arena.ActorGroundY, 0f);
                scope.Arena.GetEnemyRenderer(0).transform.localPosition = new Vector3(2f, scope.Arena.ActorGroundY, 0f);
                scope.Arena.GetEnemyRenderer(1).transform.localPosition = new Vector3(13f, scope.Arena.ActorGroundY + .7f, 0f);
                scope.Arena.GetEnemyRenderer(2).transform.localPosition = new Vector3(18f, scope.Arena.ActorGroundY - .6f, 0f);
                scope.Arena.BeginSlot(Attack(), Attack());
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(960, 720) })
                {
                    scope.Resize(size);
                    scope.AdvanceWithoutRequest(70);
                    float wideSize = scope.Arena.ArenaCamera.orthographicSize;
                    scope.AdvanceShot(.50f, 40);
                    Assert.That(scope.Arena.ArenaCamera.orthographicSize, Is.LessThan(wideSize - .5f),
                        "The inactive roster no longer forces a distant wide shot during the current exchange. " + size);
                    Assert.That(scope.Arena.ActiveEnemyIndex, Is.Zero, "A camera focus cannot promote an incoming enemy.");
                    AssertPairOnScreen(scope.Arena);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedHits_StayBounded_ThenAnUnrequestedOrDisabledShotReturnsToTheNormalFraming()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.StartExchange();
                scope.AdvanceWithoutRequest(70);
                Snapshot baseline = scope.Read("baseline");
                for (int hit = 0; hit < 18; hit++)
                {
                    foreach (float progress in new[] { .12f, .50f, .82f })
                    {
                        scope.AdvanceShot(progress, 8);
                        Assert.That(scope.Arena.ArenaCamera.orthographicSize, Is.GreaterThan(2f).And.LessThan(6f));
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, scope.Arena.ArenaCamera.transform.localEulerAngles.z)), Is.LessThanOrEqualTo(1.3f),
                            "Repeated contacts cannot accumulate a roll or zoom.");
                        AssertPairOnScreen(scope.Arena);
                    }
                }
                scope.AdvanceWithoutRequest(70);
                AssertBaseline(scope.Read("released"), baseline);

                scope.AdvanceShot(.5f, 20);
                JsonUtility.FromJsonOverwrite("{\"combatCameraMotionEnabled\":false}", scope.Settings);
                scope.AdvanceShot(.5f, 70);
                AssertBaseline(scope.Read("disabled"), baseline);
                Assert.That(scope.Hud.CombatLetterboxInset, Is.LessThan(.1f), "Disabling the camera motion also releases its extra bands.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraUsesRealTimeThroughHitStop_ButZeroRealTimeAndTurnResetHoldOrClearTheShot()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.StartExchange();
                scope.AdvanceShot(.12f, 15);
                Snapshot held = scope.Read("held");
                for (int frame = 0; frame < 10; frame++) scope.Frame(.5f, true, .02f, 0f);
                Snapshot frozen = scope.Read("no real time");
                Assert.That(frozen.camera, Is.EqualTo(held.camera));
                Assert.That(frozen.rotation, Is.EqualTo(held.rotation));
                Assert.That(frozen.size, Is.EqualTo(held.size));
                Assert.That(frozen.shot, Is.EqualTo(held.shot));
                Assert.That(frozen.letterboxInset, Is.EqualTo(held.letterboxInset));
                scope.AdvanceShot(.5f, 15); // A stopped combat clock still receives real-time presentation ticks.
                Assert.That(Vector3.Distance(scope.Arena.ArenaCamera.transform.localPosition, held.camera), Is.GreaterThan(.1f));
                Assert.That(scope.Duel.CurrentSlot.HitsResolved, Is.Zero);
                scope.Arena.BeginTurn();
                Assert.That(scope.Arena.CombatShotAmount, Is.Zero);
                Assert.That(scope.Arena.CombatLetterboxAccent, Is.Zero);
                scope.Arena.BeginSlot(Attack(), Attack()); scope.AdvanceShot(.5f, 15);
                scope.Arena.Reset();
                Assert.That(scope.Arena.CombatShotAmount, Is.Zero);
                Assert.That(scope.Arena.CombatLetterboxAccent, Is.Zero);
                scope.Arena.SetPlanningState(10f, false, true); scope.Arena.Tick(0f, .02f);
                Assert.That(scope.Arena.CombatShotAmount, Is.Zero, "Planning cannot inherit the preceding exchange's directed framing.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LetterboxAddsOnlyBoundedInwardMotion_WithoutMovingTheSkillRows_AndPlanningClearsIt()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.StartExchange(); scope.AdvanceWithoutRequest(30);
                RectTransform mine = scope.Hud.GetQueuedSkillAnchor(true, 0), theirs = scope.Hud.GetQueuedSkillAnchor(false, 0);
                Assert.That(mine, Is.Not.Null); Assert.That(theirs, Is.Not.Null);
                Vector2 mineBefore = mine.anchoredPosition, theirsBefore = theirs.anchoredPosition;
                RectTransform top = FindRect(scope.Hud.Root.transform, "AttackView Up"), bottom = FindRect(scope.Hud.Root.transform, "AttackView Down");
                float topBefore = top.anchoredPosition.y, bottomBefore = bottom.anchoredPosition.y;
                for (int frame = 0; frame < 30; frame++)
                {
                    scope.Hud.SetCombatLetterboxAccent(1f);
                    scope.RefreshHud(0f, .02f);
                }
                Assert.That(scope.Hud.CombatLetterboxInset, Is.GreaterThan(30f).And.LessThanOrEqualTo(36f));
                Assert.That(top.anchoredPosition.y, Is.LessThan(topBefore - 30f));
                Assert.That(bottom.anchoredPosition.y, Is.GreaterThan(bottomBefore + 30f));
                Assert.That(mine.anchoredPosition, Is.EqualTo(mineBefore));
                Assert.That(theirs.anchoredPosition, Is.EqualTo(theirsBefore), "Camera bands must leave the player's reservation layout intact.");
                for (int frame = 0; frame < 50; frame++) scope.RefreshHud(0f, .02f);
                Assert.That(scope.Hud.CombatLetterboxInset, Is.LessThan(.1f), "The accent is a consumed frame request, not a latched mode.");
                scope.Hud.SetCombatLetterboxAccent(1f); scope.RefreshHud(0f, .2f);
                scope.Hud.BeginTurn();
                Assert.That(scope.Hud.CombatLetterboxAccent, Is.Zero);
                Assert.That(scope.Hud.CombatLetterboxInset, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualStageFive_DrivesTheShotAndBands_AndPauseFreezesBothAlongWithTheCurrentSkill()
        {
            yield return null;
            using (var scope = new CampaignScope())
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.IsGroupBattle, Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null && controller.ArenaView.CombatShotAmount > .15f &&
                    controller.Hud.CombatLetterboxInset > .5f, "The actual controller must feed both the arena and HUD during a stage-five exchange.");
                Camera camera = controller.ArenaView.ArenaCamera;
                Vector3 position = camera.transform.localPosition;
                Quaternion rotation = camera.transform.localRotation;
                float size = camera.orthographicSize, amount = controller.ArenaView.CombatShotAmount;
                float inset = controller.Hud.CombatLetterboxInset;
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                int hits = slot.HitsResolved, health = controller.Session.Player.Health;
                int enemyHealth = ((ILegacyEnemyRosterView)controller.Session).TotalEnemyHealth;
                Assert.That(controller.PauseBattle(), Is.True);
                scope.AdvanceFor(.6f);
                Assert.That(camera.transform.localPosition, Is.EqualTo(position));
                Assert.That(camera.transform.localRotation, Is.EqualTo(rotation));
                Assert.That(camera.orthographicSize, Is.EqualTo(size));
                Assert.That(controller.ArenaView.CombatShotAmount, Is.EqualTo(amount));
                Assert.That(controller.Hud.CombatLetterboxInset, Is.EqualTo(inset));
                Assert.That(controller.Session.CurrentSlot, Is.SameAs(slot));
                Assert.That(slot.HitsResolved, Is.EqualTo(hits));
                Assert.That(controller.Session.Player.Health, Is.EqualTo(health));
                Assert.That(((ILegacyEnemyRosterView)controller.Session).TotalEnemyHealth, Is.EqualTo(enemyHealth));
                Assert.That(controller.ResumeBattle(), Is.True);
                scope.AdvanceFor(.08f);
                Assert.That(Vector3.Distance(camera.transform.localPosition, position) + Mathf.Abs(camera.orthographicSize - size),
                    Is.GreaterThan(.001f), "Resuming continues the shot from its paused framing.");
                controller.RestartMatch();
                Assert.That(controller.ArenaView.CombatShotAmount, Is.Zero);
                Assert.That(controller.Hud.CombatLetterboxInset, Is.Zero);
            }
            yield return null;
        }

        private static void AssertBaseline(Snapshot actual, Snapshot baseline)
        {
            Assert.That(actual.shot, Is.LessThan(.002f));
            Assert.That(Vector3.Distance(actual.camera, baseline.camera), Is.LessThan(.02f));
            Assert.That(actual.size, Is.EqualTo(baseline.size).Within(.02f));
            Assert.That(Quaternion.Angle(actual.rotation, baseline.rotation), Is.LessThan(.05f));
        }

        private static void AssertPairOnScreen(LegacyArenaView arena)
        {
            foreach (Vector3 ground in new[] { arena.PlayerGroundPosition, arena.EnemyGroundPosition })
            {
                Vector3 feet = arena.ArenaCamera.WorldToViewportPoint(ground);
                Vector3 head = arena.ArenaCamera.WorldToViewportPoint(ground + new Vector3(0f, 1.5f, 0f));
                Assert.That(feet.x, Is.InRange(.025f, .975f));
                Assert.That(head.x, Is.InRange(.025f, .975f));
                Assert.That(feet.y, Is.InRange(.1f, .94f));
                Assert.That(head.y, Is.InRange(.1f, .96f), "The pair's faces and ground marks remain in the frame throughout the shot.");
            }
        }

        private static RectTransform FindRect(Transform root, string name)
        {
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            Assert.Fail("Missing native letterbox band " + name); return null;
        }
        private static int ChangedPixels(Color32[] first, Color32[] second)
        {
            int count = 0;
            for (int i = 0; i < first.Length; i++)
                if (Math.Abs(first[i].r - second[i].r) + Math.Abs(first[i].g - second[i].g) + Math.Abs(first[i].b - second[i].b) > 12) count++;
            return count;
        }
        private static LegacySkill Attack() => new LegacySkill(98781, "촬영 검증 베기", 0, 5, 5,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);

        [Serializable] private sealed class Snapshot
        {
            public string phase;
            public Vector3 camera;
            public Quaternion rotation;
            public float size, shot, letterboxAccent, letterboxInset;
            public Vector3 player, enemy;
        }

        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject root = new GameObject("Combat Camera Drama Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private RenderTexture target;
            public DuelPresentationSettings Settings { get; } = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            public LegacyArenaView Arena { get; }
            public LegacyCombatHud Hud { get; }
            public LegacyQueuedDuel Duel { get; }
            public ArenaScope()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                Arena = LegacyArenaView.Create(root.transform, art, Settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor); Arena.Reset(); Arena.SetOpeningPositions(-2f, 2f);
                Hud = new LegacyCombatHud(root.transform, art, _ => { }, () => { }, () => { }, Settings);
                Duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Attack() }, new[] { Attack() }, new[] { 1 }, meshPercent: 0);
                Assert.That(Duel.TryQueueLane(0), Is.True);
                Resize(new Vector2Int(1280, 720));
            }
            public void StartExchange()
            {
                Duel.Commit(); Duel.BeginNextSlot();
                Arena.BeginSlot(Duel.CurrentSlot.PlayerSkill, Duel.CurrentSlot.EnemySkill);
                // A real commit closes the input dock before the exchange. Advance that UI clock separately so
                // holding combat at zero does not leave a planning-sized dock forcing a protective wide shot.
                Hud.EndTurn();
                Hud.BeginCombat();
                for (int frame = 0; frame < 25; frame++) RefreshHud(.02f, .02f);
                AdvanceWithoutRequest(25);
            }
            public void AdvanceShot(float progress, int frames)
            {
                typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private).Invoke(Arena,
                    new object[] { LegacyArenaView.OriginalClipDuration * progress });
                for (int frame = 0; frame < frames; frame++) Frame(progress, true, 0f, .02f);
            }
            public void AdvanceWithoutRequest(int frames)
            {
                for (int frame = 0; frame < frames; frame++) Frame(0f, false, 0f, .02f);
            }
            public void Frame(float progress, bool request, float delta, float realDelta)
            {
                if (request) Arena.SetCombatShot(progress, true, true, false);
                Arena.SetHudFloor(Hud.QueuePanelFloor); Arena.Tick(delta, realDelta);
                Hud.SetCombatLetterboxAccent(Arena.CombatLetterboxAccent);
                RefreshHud(delta, realDelta);
            }
            public void RefreshHud(float delta, float realDelta)
            {
                Hud.Refresh(Duel, 10f, true, Duel.CurrentSlot?.SlotIndex ?? -1, Arena.ArenaCamera,
                    Arena.PlayerRenderer.transform, Arena.EnemyRenderer.transform, delta, realDelta);
                Canvas.ForceUpdateCanvases();
            }
            public Snapshot Read(string phase) => new Snapshot
            {
                phase = phase, camera = Arena.ArenaCamera.transform.localPosition,
                rotation = Arena.ArenaCamera.transform.localRotation, size = Arena.ArenaCamera.orthographicSize,
                shot = Arena.CombatShotAmount, letterboxAccent = Hud.CombatLetterboxAccent,
                letterboxInset = Hud.CombatLetterboxInset, player = Arena.PlayerGroundPosition, enemy = Arena.EnemyGroundPosition
            };
            public void Resize(Vector2Int size)
            {
                Arena.ArenaCamera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                Camera camera = Arena.ArenaCamera;
                camera.targetTexture = target; camera.aspect = size.x / (float)size.y;
                Canvas canvas = Hud.Root.GetComponent<Canvas>(); CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(1f, camera.nearClipPlane + .1f);
                float logX = Mathf.Log(size.x / scaler.referenceResolution.x, 2f), logY = Mathf.Log(size.y / scaler.referenceResolution.y, 2f);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logX, logY, scaler.matchWidthOrHeight));
            }
            public Color32[] Capture(string name)
            {
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Arena.ArenaCamera.cullingMask = 1 << 31;
                Arena.ArenaCamera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                Canvas.ForceUpdateCanvases(); Arena.ArenaCamera.Render();
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                RenderTexture old = RenderTexture.active;
                try
                {
                    RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    string output = Environment.GetEnvironmentVariable("SWORDMASTER_CAMERA_CAPTURE_DIR");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Directory.CreateDirectory(output);
                        File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
                        File.WriteAllText(Path.Combine(output, name + ".json"), JsonUtility.ToJson(Read(name), true));
                    }
                    return texture.GetPixels32();
                }
                finally { RenderTexture.active = old; Object.Destroy(texture); }
            }
            public void Dispose()
            {
                Arena.ArenaCamera.targetTexture = null; target.Release(); Object.Destroy(target);
                Hud.Dispose(); Arena.Dispose(); art.Dispose(); Object.Destroy(Settings); Object.Destroy(root);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }

        private sealed class CampaignScope : IDisposable
        {
            private readonly bool wasEnabled;
            private readonly Action<float, Keyboard> advance;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, originalHudSettings, settings;
            public DuelPrototypeController Controller { get; }
            public CampaignScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null); Assert.That(Controller.AutoSaveEnabled, Is.False);
                wasEnabled = Controller.enabled; Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalArenaSettings = (DuelPresentationSettings)typeof(LegacyArenaView).GetField("settings", Private).GetValue(Controller.ArenaView);
                originalHudSettings = (DuelPresentationSettings)typeof(LegacyCombatHud).GetField("presentationSettings", Private).GetValue(Controller.Hud);
                settings = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite("{\"combatCameraMotionEnabled\":true,\"combatCameraMotionStrength\":1,\"hitStopDuration\":0,\"bladeBindSeconds\":0}", settings);
                typeof(DuelPrototypeController).GetField("presentationSettings", Private).SetValue(Controller, settings);
                typeof(LegacyArenaView).GetField("settings", Private).SetValue(Controller.ArenaView, settings);
                typeof(LegacyCombatHud).GetField("presentationSettings", Private).SetValue(Controller.Hud, settings);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                Controller.RestartJourney();
                for (int stage = 1; stage <= 4; stage++)
                {
                    Assert.That(Controller.Campaign.TryStartStage(stage), Is.True);
                    Assert.That(Controller.Campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                    Assert.That(Controller.Campaign.ReturnToLobby(), Is.True);
                }
                Assert.That(Controller.StartCampaignStage(5), Is.True);
                Controller.SkipStartCard(); AdvanceFor(.7f);
            }
            public void AdvanceFor(float seconds)
            {
                int frames = Mathf.CeilToInt(seconds / .005f);
                for (int frame = 0; frame < frames; frame++) advance(seconds / frames, null);
            }
            public void Until(Func<bool> condition, string message)
            {
                int frame = 0;
                while (!condition() && frame++ < 4000) advance(.005f, null);
                Assert.That(condition(), Is.True, message);
            }
            public void Dispose()
            {
                if (Controller.IsPaused) Controller.ResumeBattle();
                typeof(DuelPrototypeController).GetField("presentationSettings", Private).SetValue(Controller, originalSettings);
                typeof(LegacyArenaView).GetField("settings", Private).SetValue(Controller.ArenaView, originalArenaSettings);
                typeof(LegacyCombatHud).GetField("presentationSettings", Private).SetValue(Controller.Hud, originalHudSettings);
                Controller.RestartMatch(); Controller.enabled = wasEnabled; Object.Destroy(settings);
            }
        }
    }
}
