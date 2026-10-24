using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CameraFramingLiftPlayModeTests
    {
        [UnityTest]
        public IEnumerator OrdinaryPlanningAndCombat_ActuallyLiftTheCamera_WhileKeepingFeetAboveTheRealHud()
        {
            using (var previous = new Scope(0f))
            using (var raised = new Scope(.3f))
            foreach (ArenaBackdropKind backdrop in new[] { ArenaBackdropKind.Forest, ArenaBackdropKind.SchoolCorridor })
            foreach (float aspect in new[] { 16f / 9f, 4f / 3f })
            foreach (bool combat in new[] { false, true })
            {
                previous.Prepare(backdrop, aspect, combat);
                raised.Prepare(backdrop, aspect, combat);
                previous.Settle(); raised.Settle();
                Assert.That(raised.Y - previous.Y, Is.EqualTo(.3f).Within(.004f),
                    "The final safety fit must not silently undo the higher view. " + backdrop + " " + aspect + " " + combat);
                Assert.That(raised.Hud.QueuePanelFloor, Is.GreaterThan(.05f));
                AssertFeetClear(raised);
                Assert.That(raised.Arena.PlayerGroundPosition, Is.EqualTo(previous.Arena.PlayerGroundPosition));
                Assert.That(raised.Arena.EnemyGroundPosition, Is.EqualTo(previous.Arena.EnemyGroundPosition));
                Assert.That(raised.Duel.Player.Health, Is.EqualTo(1000));
                if (combat) Assert.That(raised.Duel.CurrentSlot.HitsResolved, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AWaitingEnemyRoster_StillAllowsTheHigherDefaultView_AndKeepsEveryFootClear()
        {
            using (var previous = new Scope(0f))
            using (var raised = new Scope(.3f))
            foreach (bool combat in new[] { false, true })
            {
                previous.Prepare(ArenaBackdropKind.SchoolCorridor, 4f / 3f, combat, group: true);
                raised.Prepare(ArenaBackdropKind.SchoolCorridor, 4f / 3f, combat, group: true);
                previous.Settle(); raised.Settle();
                Assert.That(raised.Y - previous.Y, Is.EqualTo(.3f).Within(.004f));
                AssertFeetClear(raised);
                Assert.That(raised.Arena.ActiveEnemyIndex, Is.Zero);
                for (int enemy = 0; enemy < raised.Arena.EnemyCount; enemy++)
                {
                    Vector3 feet = raised.Arena.ArenaCamera.WorldToViewportPoint(raised.Arena.GetEnemyGroundPosition(enemy));
                    Assert.That(feet.y, Is.GreaterThanOrEqualTo(raised.Hud.QueuePanelFloor - .002f));
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CutInAndFinishingFocus_KeepTheirOwnFraming_WithoutAddingTheDefaultLift()
        {
            using (var previous = new Scope(0f))
            using (var raised = new Scope(.3f))
            {
                previous.Prepare(ArenaBackdropKind.SchoolCorridor, 16f / 9f, true);
                raised.Prepare(ArenaBackdropKind.SchoolCorridor, 16f / 9f, true);
                previous.Arena.SetCutIn(1f); raised.Arena.SetCutIn(1f);
                previous.Settle(); raised.Settle();
                Assert.That(raised.Y, Is.EqualTo(previous.Y).Within(.002f));
                Assert.That(raised.Size, Is.EqualTo(previous.Size).Within(.002f));

                previous.Arena.SetCutIn(0f); raised.Arena.SetCutIn(0f);
                previous.Arena.ObserveCombatContact(); raised.Arena.ObserveCombatContact();
                UnityEngine.Random.State randomState = UnityEngine.Random.state;
                previous.Arena.HoldFinishingFocus(false, true, 10f);
                UnityEngine.Random.state = randomState;
                raised.Arena.HoldFinishingFocus(false, true, 10f);
                previous.Settle(); raised.Settle();
                Assert.That(raised.Arena.IsCameraCloseUp, Is.True);
                Assert.That(raised.Y, Is.EqualTo(previous.Y).Within(.002f));
                Assert.That(raised.Size, Is.EqualTo(previous.Size).Within(.002f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AResumedCutscene_RestoresTheRaisedShotExactly_AndDoesNotApplyTheLiftTwice()
        {
            using (var scope = new Scope(.3f))
            {
                scope.Prepare(ArenaBackdropKind.SchoolCorridor, 16f / 9f, true);
                scope.Settle();
                Camera camera = scope.Arena.ArenaCamera;
                Vector3 position = camera.transform.localPosition;
                float size = camera.orthographicSize;
                Invoke(scope.Arena, "SuspendForCutscene");
                camera.transform.localPosition = new Vector3(12f, 8f, -10f);
                camera.orthographicSize = 2f;
                Invoke(scope.Arena, "ResumeAfterCutscene");
                Assert.That(camera.transform.localPosition, Is.EqualTo(position));
                Assert.That(camera.orthographicSize, Is.EqualTo(size));
                scope.Settle();
                Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(.002f));
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.002f));
            }
            yield return null;
        }

        [Test]
        public void CameraLiftAuthoring_HasASmallDefault_AndSafeRuntimeLimits()
        {
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                Assert.That(settings.CameraFramingLift, Is.EqualTo(.3f));
                JsonUtility.FromJsonOverwrite("{\"cameraFramingLift\":-2}", settings);
                Assert.That(settings.CameraFramingLift, Is.Zero);
                JsonUtility.FromJsonOverwrite("{\"cameraFramingLift\":99}", settings);
                Assert.That(settings.CameraFramingLift, Is.EqualTo(.6f));
                typeof(DuelPresentationSettings).GetField("cameraFramingLift", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(settings, float.NaN);
                Assert.That(settings.CameraFramingLift, Is.EqualTo(.3f));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        private static void AssertFeetClear(Scope scope)
        {
            Camera camera = scope.Arena.ArenaCamera;
            foreach (Vector3 ground in new[] { scope.Arena.PlayerGroundPosition, scope.Arena.EnemyGroundPosition })
                Assert.That(camera.WorldToViewportPoint(ground).y,
                    Is.GreaterThanOrEqualTo(scope.Hud.QueuePanelFloor - .002f));
        }

        private static void Invoke(LegacyArenaView arena, string name)
            => typeof(LegacyArenaView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, null);

        private sealed class Scope : IDisposable
        {
            private readonly GameObject root = new GameObject("Camera Lift Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private bool combat;
            public LegacyArenaView Arena { get; }
            public LegacyCombatHud Hud { get; }
            public LegacyQueuedDuel Duel { get; }
            public float Y => Arena.ArenaCamera.transform.localPosition.y;
            public float Size => Arena.ArenaCamera.orthographicSize;

            public Scope(float lift)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                typeof(DuelPresentationSettings).GetField("cameraFramingLift", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(settings, lift);
                JsonUtility.FromJsonOverwrite("{\"battleDriftSpeed\":0,\"impactCameraShake\":0}", settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Arena.ArenaCamera.enabled = false;
                Hud = new LegacyCombatHud(root.transform, art, _ => { }, () => { }, () => { }, settings);
                LegacySkill skill = LegacySkillDefinitions.Skill(1);
                Duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { skill }, new[] { skill }, new[] { 1 });
            }

            public void Prepare(ArenaBackdropKind backdrop, float aspect, bool inCombat, bool group = false)
            {
                combat = inCombat;
                Duel.Reset();
                Arena.SetBackdrop(backdrop); Arena.Reset();
                Arena.ArenaCamera.aspect = aspect;
                Arena.ConfigureEnemyRoster(group
                    ? new[] { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student }
                    : new[] { EnemyAppearance.Student });
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, Arena.ActorGroundY, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(2f, Arena.ActorGroundY, 0f);
                if (group)
                {
                    Arena.GetEnemyRenderer(1).transform.localPosition = new Vector3(10f, Arena.ActorGroundY + .4f, 0f);
                    Arena.GetEnemyRenderer(2).transform.localPosition = new Vector3(14f, Arena.ActorGroundY - .4f, 0f);
                }
                Arena.SetDepthPresentationVisible(true);
                Hud.BeginTurn();
                if (combat)
                {
                    Assert.That(Duel.TryQueueLane(0), Is.True);
                    Duel.Commit(); Duel.BeginNextSlot();
                    Arena.BeginSlot(Duel.CurrentSlot.PlayerSkill, Duel.CurrentSlot.EnemySkill);
                    Hud.EndTurn(); Hud.BeginCombat();
                }
                else { Arena.BeginTurn(); Arena.SetPlanningState(10f, false); }
                RefreshHud();
            }

            public void Settle()
            {
                for (int frame = 0; frame < 120; frame++)
                {
                    Arena.SetHudFloor(Hud.QueuePanelFloor);
                    Arena.Tick(0f, .02f);
                    RefreshHud();
                }
            }

            private void RefreshHud()
            {
                Hud.Refresh(Duel, 10f, combat, combat ? 0 : -1, Arena.ArenaCamera,
                    Arena.PlayerRenderer.transform, Arena.EnemyRenderer.transform, .02f, .02f);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose(); Arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(root);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
