using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ContactCameraReadabilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator PendingContact_KeepsTheActiveEnvelopeAboveTheRealHud_AfterRollAndShake()
        {
            yield return null;
            foreach (float aspect in new[] { 16f / 9f, 4f / 3f })
            using (var scope = new CameraScope(aspect))
            {
                // The envelope is arena-local. Translating the whole arena must not change its viewport guarantee.
                scope.Root.transform.position = new Vector3(31f, -17f, 0f);
                scope.Arena.ArenaCamera.transform.localRotation = Quaternion.Euler(0f, 0f, 11f);
                scope.Arena.AddCameraShake(.3f, .2f);
                scope.Tick(.02f);
                Assert.That(scope.Arena.IsReadingContact, Is.True, "A windup keeps the pair until the outcome is known.");
                Assert.That(scope.Arena.HudFloor, Is.GreaterThan(0f));
                AssertEnvelope(scope);
                scope.Arena.ObserveCombatContact();
                scope.Tick(.03f);
                AssertEnvelope(scope);
                Assert.That(scope.Duel.CurrentSlot.HitsResolved, Is.Zero, "Camera requests do not execute combat rules.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DecisiveSlowClockStartsOnImpact_ThenTheReadPairGivesWayToTheProtagonistCloseUp()
        {
            yield return null;
            using (var scope = new CameraScope())
            {
                float globalTime = Time.timeScale;
                scope.Arena.PresentHit(true, 1, 0, false, true, 0);
                Assert.That(scope.Arena.IsFatalFocus, Is.True, "Controllers must see the decisive slow-clock flag immediately.");
                Assert.That(scope.Arena.IsCameraCloseUp, Is.False);
                scope.Tick(LegacyArenaView.ContactReadSeconds - .02f);
                Assert.That(scope.Arena.IsReadingContact, Is.True);
                AssertEnvelope(scope);
                float pairSize = scope.Arena.ArenaCamera.orthographicSize;

                scope.Tick(.03f);
                Assert.That(scope.Arena.IsReadingContact, Is.False);
                Assert.That(scope.Arena.IsCameraCloseUp && scope.Arena.IsFatalFocus, Is.True);
                scope.Tick(.2f);
                Assert.That(scope.Arena.ArenaCamera.orthographicSize, Is.LessThan(pairSize));
                AssertPlayerOwnsFocus(scope);
                Assert.That(Time.timeScale, Is.EqualTo(globalTime));
                Assert.That(scope.Duel.CurrentSlot.HitsResolved, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ALongFrameAndCompletedSlot_StillShowOneReadablePairBeforeTheExistingCloseUp()
        {
            yield return null;
            using (var scope = new CameraScope())
            {
                scope.Arena.PresentHit(false, 1, 0, false, true, 0);
                scope.Arena.EndTurn();
                scope.Tick(.3f);
                AssertEnvelope(scope);
                float firstFrameSize = scope.Arena.ArenaCamera.orthographicSize;
                Assert.That(scope.Arena.IsFatalFocus, Is.True, "Reading a frame cannot extend or cancel the slow clock.");
                scope.Tick(.1f);
                Assert.That(scope.Arena.IsCameraCloseUp, Is.True);
                Assert.That(scope.Arena.ArenaCamera.orthographicSize, Is.LessThan(firstFrameSize));
                AssertPlayerOwnsFocus(scope);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ANewContact_ReframesAnOldCloseUpImmediately_WithoutRenewingItsSlowClock()
        {
            yield return null;
            using (var scope = new CameraScope())
            {
                scope.Arena.PresentHit(true, 1, 0, false, true, 0);
                scope.Tick(LegacyArenaView.ContactReadSeconds + .01f);
                scope.Tick(.2f);
                Assert.That(scope.Arena.IsCameraCloseUp, Is.True);
                float closeSize = scope.Arena.ArenaCamera.orthographicSize;
                scope.Arena.PlayerRenderer.transform.localPosition += Vector3.left * 3f;
                scope.Arena.EnemyRenderer.transform.localPosition += Vector3.right * 3f;

                // A dodged hit can call this without PresentHit; it still needs an immediately readable pair.
                scope.Arena.ObserveCombatContact();
                Assert.That(scope.Arena.IsReadingContact, Is.True);
                Assert.That(scope.Arena.IsCameraCloseUp, Is.False);
                Assert.That(scope.Arena.ArenaCamera.orthographicSize, Is.GreaterThan(closeSize));
                AssertEnvelope(scope);
                scope.Tick(.6f);
                Assert.That(scope.Arena.IsFatalFocus, Is.False, "A later contact never renews the original decisive timer.");
                AssertEnvelope(scope);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DistantWaitingEnemies_CannotWidenTheActiveContact_OrChangeItsTarget()
        {
            yield return null;
            using (var scope = new CameraScope(4f / 3f, group: true))
            {
                scope.Tick(.02f);
                Rect envelope = scope.Arena.ContactReadBounds;
                Camera camera = scope.Arena.ArenaCamera;
                Vector3 position = camera.transform.localPosition;
                float size = camera.orthographicSize;
                scope.Arena.GetEnemyRenderer(1).transform.localPosition = new Vector3(200f, -100f, 0f);
                scope.Arena.GetEnemyRenderer(2).transform.localPosition = new Vector3(-200f, 100f, 0f);
                scope.Tick(0f);
                Assert.That(scope.Arena.ContactReadBounds, Is.EqualTo(envelope));
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.001f));
                Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(.001f));
                Assert.That(scope.Arena.ActiveEnemyIndex, Is.Zero);
                AssertEnvelope(scope);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ZeroDeltaAndSuspension_PreserveTheReadWindow_WhilePhaseChangesClearPendingContacts()
        {
            yield return null;
            using (var scope = new CameraScope())
            {
                scope.Arena.ObserveCombatContact();
                scope.Tick(.03f);
                for (int i = 0; i < 5; i++) scope.Tick(0f);
                Assert.That(scope.Arena.IsReadingContact, Is.True);
                Vector3 position = scope.Arena.ArenaCamera.transform.localPosition;
                Quaternion rotation = scope.Arena.ArenaCamera.transform.localRotation;
                float size = scope.Arena.ArenaCamera.orthographicSize;

                Invoke(scope.Arena, "SuspendForCutscene");
                Assert.That(scope.Arena.IsReadingContact, Is.False, "A cutscene owns its authored framing.");
                scope.Arena.ObserveCombatContact();
                scope.Arena.Tick(0f, 1f);
                Invoke(scope.Arena, "ResumeAfterCutscene");
                Assert.That(scope.Arena.IsReadingContact, Is.True);
                Assert.That(scope.Arena.ArenaCamera.transform.localPosition, Is.EqualTo(position));
                Assert.That(Quaternion.Angle(scope.Arena.ArenaCamera.transform.localRotation, rotation), Is.LessThan(.001f));
                Assert.That(scope.Arena.ArenaCamera.orthographicSize, Is.EqualTo(size));
                scope.Tick(.10f);
                Assert.That(scope.Arena.IsReadingContact, Is.True, "The scene neither aged nor renewed the remaining read time.");
                scope.Tick(.02f);
                Assert.That(scope.Arena.IsReadingContact, Is.False);

                scope.Arena.BeginSlot(Attack(), Attack());
                Assert.That(scope.Arena.IsReadingContact, Is.True);
                scope.Arena.EndTurn();
                Assert.That(scope.Arena.IsReadingContact, Is.False, "A slot with no impact leaves no pending contact on return.");
                scope.Arena.BeginSlot(Attack(), Attack());
                scope.Arena.BeginTurn();
                Assert.That(scope.Arena.IsReadingContact, Is.False);
                scope.Arena.BeginSlot(Attack(), Attack());
                scope.Arena.Reset();
                Assert.That(scope.Arena.IsReadingContact || scope.Arena.IsCameraCloseUp || scope.Arena.IsFatalFocus, Is.False);
            }
            yield return null;
        }

        private static void AssertEnvelope(CameraScope scope)
        {
            Rect bounds = scope.Arena.ContactReadBounds;
            Transform arena = scope.Arena.PlayerRenderer.transform.parent;
            Camera camera = scope.Arena.ArenaCamera;
            foreach (Vector2 corner in new[] { bounds.min, new Vector2(bounds.xMin, bounds.yMax),
                new Vector2(bounds.xMax, bounds.yMin), bounds.max })
            {
                Vector3 point = camera.WorldToViewportPoint(arena.TransformPoint(new Vector3(corner.x, corner.y, 0f)));
                Assert.That(point.z, Is.GreaterThan(0f));
                Assert.That(point.x, Is.InRange((float)LegacyContactCamera.SideMargin - .002f,
                    1f - (float)LegacyContactCamera.SideMargin + .002f));
                Assert.That(point.y, Is.InRange(scope.Arena.HudFloor + (float)LegacyContactCamera.BottomMargin - .002f,
                    1f - (float)LegacyContactCamera.TopMargin + .002f));
            }
        }

        private static void AssertPlayerOwnsFocus(CameraScope scope)
        {
            Camera camera = scope.Arena.ArenaCamera;
            float player = camera.WorldToViewportPoint(scope.Arena.PlayerRenderer.transform.position).x;
            float enemy = camera.WorldToViewportPoint(scope.Arena.EnemyRenderer.transform.position).x;
            Assert.That(Mathf.Abs(player - .5f), Is.LessThan(Mathf.Abs(enemy - .5f)));
        }

        private static void Invoke(LegacyArenaView arena, string name)
        {
            MethodInfo method = typeof(LegacyArenaView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(arena, null);
        }

        private static LegacySkill Attack() => new LegacySkill(98790, "접촉 판독", 0, 5, 5,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);

        private sealed class CameraScope : IDisposable
        {
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            public readonly GameObject Root = new GameObject("Contact Camera Readability Test");
            public readonly LegacyArenaView Arena;
            public readonly LegacyCombatHud Hud;
            public readonly LegacyQueuedDuel Duel;

            public CameraScope(float aspect = 16f / 9f, bool group = false)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                JsonUtility.FromJsonOverwrite("{\"battleDriftSpeed\":0,\"impactCameraShake\":0}", settings);
                Arena = LegacyArenaView.Create(Root.transform, art, settings);
                Arena.ArenaCamera.enabled = false;
                Arena.ArenaCamera.aspect = aspect;
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                if (group) Arena.ConfigureEnemyRoster(new[] { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student });
                Arena.SetDepthPresentationVisible(true);
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-3f, Arena.ActorGroundY - .4f, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(3f, Arena.ActorGroundY + .4f, 0f);
                if (group)
                {
                    Arena.GetEnemyRenderer(1).transform.localPosition = new Vector3(15f, Arena.ActorGroundY + 1f, 0f);
                    Arena.GetEnemyRenderer(2).transform.localPosition = new Vector3(20f, Arena.ActorGroundY - 1f, 0f);
                }
                Arena.BeginSlot(Attack(), Attack());
                Duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Attack() }, new[] { Attack() },
                    new[] { 1 }, meshPercent: 0);
                Assert.That(Duel.TryQueueLane(0), Is.True);
                Duel.Commit(); Duel.BeginNextSlot();
                Hud = new LegacyCombatHud(Root.transform, art, _ => { }, () => { }, () => { }, settings);
                Hud.EndTurn(); Hud.BeginCombat();
                RefreshHud(0f);
            }

            public void Tick(float realDelta)
            {
                Arena.SetCombatShot(.5f, true, true, false);
                Arena.SetHudFloor(Hud.QueuePanelFloor);
                Arena.Tick(0f, realDelta);
                RefreshHud(realDelta);
            }

            private void RefreshHud(float realDelta)
            {
                Hud.SetCombatLetterboxAccent(Arena.CombatLetterboxAccent);
                Hud.Refresh(Duel, 10f, true, 0, Arena.ArenaCamera,
                    Arena.PlayerRenderer.transform, Arena.EnemyRenderer.transform, 0f, realDelta);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose(); Arena.Dispose(); art.Dispose();
                Object.Destroy(settings); Object.Destroy(Root);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
