using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The arena's ground plane, separate from animated artwork: depth orders the whole fighter and the hollow
    /// circle follows its actual feet, while shader lunges, cutscene borrowing and a training dummy keep their contracts.</summary>
    public sealed class CombatDepthPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator Approach_PreservesDistinctGroundDepths_ThenIdleSlotsAndReturnKeepThem()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                arena.PlayerRenderer.transform.localPosition = new Vector3(-5f, arena.ActorGroundY - .1f, 0f);
                arena.EnemyRenderer.transform.localPosition = new Vector3(5f, arena.ActorGroundY + .6f, 0f);
                Vector3 initialPlayer = arena.PlayerGroundPosition, initialEnemy = arena.EnemyGroundPosition;
                for (int frame = 0; frame < 20; frame++)
                {
                    arena.SetPlanningState(10f, false);
                    arena.Tick(0f, .05f);
                }
                Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(initialPlayer.y).Within(.0001f), "Planning invents no vertical staging lane.");
                Assert.That(arena.EnemyGroundPosition.y, Is.EqualTo(initialEnemy.y).Within(.0001f));
                AssertGroundPresentation(arena);
                arena.BeginApproach();
                for (int frame = 0; frame < 120 && !arena.ApproachComplete; frame++) arena.Tick(.02f, .02f);
                Assert.That(arena.ApproachComplete, Is.True);
                Assert.That(arena.IsInRange, Is.True);
                Assert.That(arena.PlayerGroundPosition.x, Is.GreaterThan(initialPlayer.x + 1f));
                Assert.That(arena.EnemyGroundPosition.x, Is.LessThan(initialEnemy.x - 1f));
                Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(initialPlayer.y).Within(.0001f));
                Assert.That(arena.EnemyGroundPosition.y, Is.EqualTo(initialEnemy.y).Within(.0001f),
                    "The attack begins at horizontal contact without pulling either fighter onto the other's floor depth.");
                arena.BeginSlot(Attack(), Attack());
                Vector3 playerHeld = arena.PlayerGroundPosition, enemyHeld = arena.EnemyGroundPosition;
                arena.Tick(0f, .2f);
                Assert.That(arena.PlayerGroundPosition, Is.EqualTo(playerHeld), "Real time alone cannot move an active exchange during hit stop.");
                Assert.That(arena.EnemyGroundPosition, Is.EqualTo(enemyHeld));
                // Let arrival momentum plant the feet before isolating whether slot parity invents a new destination.
                int stillFrames = 0;
                for (int frame = 0; frame < 120 && stillFrames < 5; frame++)
                {
                    Vector3 playerBefore = arena.PlayerGroundPosition, enemyBefore = arena.EnemyGroundPosition;
                    arena.Tick(.01f, .01f);
                    bool still = Vector3.Distance(playerBefore, arena.PlayerGroundPosition) < .00001f &&
                        Vector3.Distance(enemyBefore, arena.EnemyGroundPosition) < .00001f;
                    stillFrames = still ? stillFrames + 1 : 0;
                }
                Assert.That(stillFrames, Is.EqualTo(5), "Arrival momentum must settle rather than drift indefinitely.");
                playerHeld = arena.PlayerGroundPosition; enemyHeld = arena.EnemyGroundPosition;
                for (int slot = 0; slot < 4; slot++)
                {
                    arena.BeginSlot(Attack(), Attack());
                    for (int frame = 0; frame < 15; frame++) arena.Tick(.02f, .02f);
                    Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(playerHeld.y).Within(.005f), "Slot parity alone cannot create a new vertical destination.");
                    Assert.That(arena.EnemyGroundPosition.y, Is.EqualTo(enemyHeld.y).Within(.005f));
                    AssertGroundPresentation(arena);
                }
                Vector3 beforeReturn = arena.PlayerGroundPosition;
                arena.EndTurn();
                for (int frame = 0; frame < 45; frame++) arena.Tick(.02f, .02f);
                Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(beforeReturn.y).Within(.005f), "The turn's end keeps the ground reached in combat.");
                AssertGroundPresentation(arena);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MirroredPerspectives_PreserveEachPersonsCanonicalGroundYAndForegroundOrder()
        {
            yield return null;
            using (var host = new ArenaScope())
            using (var guest = new ArenaScope())
            {
                host.Arena.SetDepthPerspective(0); guest.Arena.SetDepthPerspective(1);
                host.Arena.PlayerRenderer.transform.localPosition = new Vector3(-5f, host.Arena.ActorGroundY - .25f, 0f);
                host.Arena.EnemyRenderer.transform.localPosition = new Vector3(5f, host.Arena.ActorGroundY + .4f, 0f);
                guest.Arena.PlayerRenderer.transform.localPosition = new Vector3(-5f, guest.Arena.ActorGroundY + .4f, 0f);
                guest.Arena.EnemyRenderer.transform.localPosition = new Vector3(5f, guest.Arena.ActorGroundY - .25f, 0f);
                for (int frame = 0; frame < 20; frame++)
                {
                    host.Arena.SetPlanningState(10f, false); guest.Arena.SetPlanningState(10f, false);
                    host.Arena.Tick(0f, .05f); guest.Arena.Tick(0f, .05f);
                }
                AssertMirroredGround(host.Arena, guest.Arena);
                host.Arena.BeginApproach(); guest.Arena.BeginApproach();
                for (int frame = 0; frame < 60; frame++)
                {
                    host.Arena.Tick(.02f, .02f); guest.Arena.Tick(.02f, .02f);
                    AssertMirroredGround(host.Arena, guest.Arena);
                }
                host.Arena.BeginSlot(Attack(), Attack()); guest.Arena.BeginSlot(Attack(), Attack());
                for (int frame = 0; frame < 30; frame++)
                {
                    host.Arena.Tick(.02f, .02f); guest.Arena.Tick(.02f, .02f);
                    AssertMirroredGround(host.Arena, guest.Arena);
                }
                host.Arena.EndTurn(); guest.Arena.EndTurn();
                for (int frame = 0; frame < 30; frame++)
                {
                    host.Arena.Tick(.02f, .02f); guest.Arena.Tick(.02f, .02f);
                    AssertMirroredGround(host.Arena, guest.Arena);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualCampaignPause_HoldsGroundTravelAndItsCircles_UntilPlayResumes()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.AutoSaveEnabled, Is.False);
            bool wasEnabled = controller.enabled;
            LegacyQueuedDuel original = controller.Session;
            controller.enabled = false;
            FieldInfo session = typeof(DuelPrototypeController).GetField("session", Private);
            var advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), controller,
                typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
            try
            {
                typeof(DuelPrototypeController).GetField("mission", Private).SetValue(controller, null);
                typeof(DuelPrototypeController).GetField("guide", Private).SetValue(controller, null);
                session.SetValue(controller, new LegacyQueuedDuel(10000, 10000, 10000, 10000,
                    new[] { Attack() }, new[] { Attack() }, new[] { 3 }, meshPercent: 0));
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(controller, new object[] { null, null });
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                advance(.01f, null);
                Vector3 player = controller.ArenaView.PlayerGroundPosition, enemy = controller.ArenaView.EnemyGroundPosition;
                Assert.That(controller.PauseBattle(), Is.True);
                advance(.5f, null);
                Assert.That(controller.ArenaView.PlayerGroundPosition, Is.EqualTo(player));
                Assert.That(controller.ArenaView.EnemyGroundPosition, Is.EqualTo(enemy));
                AssertGroundPresentation(controller.ArenaView);
                Assert.That(controller.ResumeBattle(), Is.True);
                advance(.05f, null);
                Assert.That(Vector3.Distance(controller.ArenaView.PlayerGroundPosition, player), Is.GreaterThan(.001f));
            }
            finally
            {
                if (controller.IsPaused) controller.ResumeBattle();
                session.SetValue(controller, original); controller.RestartMatch(); controller.enabled = wasEnabled;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GroundCrossing_ReordersWholeFighters_AndTheCircleIgnoresAnAttackDrawOffset()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                // The real controller snapshots timing and the shader lunge before starting each skill.
                typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(arena, new object[] { 1f, 0f });
                arena.BeginSlot(Attack(), Attack());
                Place(arena, -.4f, .4f);
                AssertGroundPresentation(arena);
                Assert.That(arena.PlayerDepthSortingOrder, Is.GreaterThan(arena.EnemyDepthSortingOrder));
                Assert.That(arena.PlayerRenderer.GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(arena.PlayerDepthSortingOrder));
                Assert.That(arena.EnemyRenderer.GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(arena.EnemyDepthSortingOrder));
                Vector3 playerFloor = arena.PlayerGroundPosition, enemyFloor = arena.EnemyGroundPosition;
                typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private).Invoke(arena,
                    new object[] { LegacyArenaView.OriginalClipDuration * .5f });
                Assert.That(arena.PlayerAttackVisualOffset.magnitude, Is.GreaterThan(.01f));
                Assert.That(Vector3.Distance(arena.PlayerGroundPosition, playerFloor), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(arena.EnemyGroundPosition, enemyFloor), Is.LessThan(.0001f));
                AssertGroundPresentation(arena);
                Place(arena, .4f, -.4f);
                Assert.That(arena.EnemyDepthSortingOrder, Is.GreaterThan(arena.PlayerDepthSortingOrder),
                    "Crossing ground Y swaps the front fighter, including its lower body, shadow and aura children.");
                AssertGroundPresentation(arena);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CutsceneBorrowing_RestoresGroundPositionsAndOrdering_AndResetReusesTheTwoCircles()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                Place(arena, -.35f, .25f);
                Vector3 player = arena.PlayerRenderer.transform.localPosition, enemy = arena.EnemyRenderer.transform.localPosition;
                Transform playerCircle = arena.PlayerGroundCircle, enemyCircle = arena.EnemyGroundCircle;
                int playerOrder = arena.PlayerDepthSortingOrder, enemyOrder = arena.EnemyDepthSortingOrder;
                typeof(LegacyArenaView).GetMethod("SuspendForCutscene", Private).Invoke(arena, null);
                Assert.That(CircleVisible(playerCircle) || CircleVisible(enemyCircle), Is.False,
                    "Combat ground guides do not appear over staged cutscene placement.");
                arena.PlayerRenderer.transform.localPosition = new Vector3(-5f, arena.ActorGroundY, 0f);
                arena.EnemyRenderer.transform.localPosition = new Vector3(5f, arena.ActorGroundY, 0f);
                typeof(LegacyArenaView).GetMethod("ResumeAfterCutscene", Private).Invoke(arena, null);
                Assert.That(arena.PlayerRenderer.transform.localPosition, Is.EqualTo(player));
                Assert.That(arena.EnemyRenderer.transform.localPosition, Is.EqualTo(enemy));
                Assert.That(arena.PlayerDepthSortingOrder, Is.EqualTo(playerOrder));
                Assert.That(arena.EnemyDepthSortingOrder, Is.EqualTo(enemyOrder));
                AssertGroundPresentation(arena);
                arena.Reset();
                Assert.That(arena.PlayerRenderer.transform.localPosition.y, Is.EqualTo(arena.ActorGroundY).Within(.0001f));
                Assert.That(arena.EnemyRenderer.transform.localPosition.y, Is.EqualTo(arena.ActorGroundY).Within(.0001f));
                Assert.That(arena.PlayerGroundCircle, Is.SameAs(playerCircle));
                Assert.That(arena.EnemyGroundCircle, Is.SameAs(enemyCircle));
                Assert.That(CircleVisible(playerCircle) || CircleVisible(enemyCircle), Is.False,
                    "A reset returns to unstaged placement until the next battle presentation starts.");
                arena.SetDepthPresentationVisible(true);
                AssertGroundPresentation(arena);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrainingDummy_KeepsItsGroundPositionThroughoutApproachAttackAndReturn()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                arena.SetEnemyAppearance(EnemyAppearance.TrainingDummy);
                arena.Reset();
                Vector3 ground = arena.EnemyGroundPosition;
                arena.BeginApproach();
                for (int frame = 0; frame < 120 && !arena.ApproachComplete; frame++)
                {
                    arena.Tick(.02f, .02f);
                    Assert.That(Vector3.Distance(arena.EnemyGroundPosition, ground), Is.LessThan(.0001f));
                }
                Assert.That(arena.ApproachComplete, Is.True);
                Assert.That(arena.IsInRange, Is.True);
                arena.BeginSlot(Attack(), null);
                for (int frame = 0; frame < 45; frame++)
                {
                    arena.Tick(.02f, .02f);
                    Assert.That(Vector3.Distance(arena.EnemyGroundPosition, ground), Is.LessThan(.0001f));
                }
                arena.EndTurn();
                for (int frame = 0; frame < 45; frame++)
                {
                    arena.Tick(.02f, .02f);
                    Assert.That(Vector3.Distance(arena.EnemyGroundPosition, ground), Is.LessThan(.0001f));
                }
                AssertGroundPresentation(arena);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControlledDepth_IsActuallyRenderedAtThreeAspectRatios_WithGroundMarkersVisible()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                string output = Environment.GetEnvironmentVariable("SWORDMASTER_COMBAT_DEPTH_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(output)) Directory.CreateDirectory(output);
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 960), new Vector2Int(1920, 810) })
                {
                    scope.Resize(size);
                    scope.Arena.Reset();
                    Place(scope.Arena, -.4f, .4f);
                    AssertGroundPresentation(scope.Arena);
                    yield return null;
                    Color32[] visible = scope.Capture(string.IsNullOrEmpty(output) ? null : Path.Combine(output, size.x + "x" + size.y + "-depth-front-player.png"));
                    Renderer playerCircle = scope.Arena.PlayerGroundCircle.GetComponent<Renderer>();
                    Renderer enemyCircle = scope.Arena.EnemyGroundCircle.GetComponent<Renderer>();
                    playerCircle.enabled = enemyCircle.enabled = false;
                    Color32[] hidden;
                    try { hidden = scope.Capture(); }
                    finally { playerCircle.enabled = enemyCircle.enabled = true; }
                    int changed = 0;
                    for (int pixel = 0; pixel < visible.Length; pixel++)
                        if (Math.Abs(visible[pixel].r - hidden[pixel].r) + Math.Abs(visible[pixel].g - hidden[pixel].g) +
                            Math.Abs(visible[pixel].b - hidden[pixel].b) > 12) changed++;
                    Assert.That(changed, Is.GreaterThan(30), "The ground circles must actually render rather than only exist as transforms.");
                    Place(scope.Arena, .4f, -.4f);
                    yield return null;
                    if (!string.IsNullOrEmpty(output)) scope.Capture(Path.Combine(output, size.x + "x" + size.y + "-depth-front-enemy.png"));
                }
            }
            yield return null;
        }

        private static void Place(LegacyArenaView arena, float playerY, float enemyY)
        {
            arena.PlayerRenderer.transform.localPosition = new Vector3(-.65f, arena.ActorGroundY + playerY, 0f);
            arena.EnemyRenderer.transform.localPosition = new Vector3(.65f, arena.ActorGroundY + enemyY, 0f);
            arena.Tick(0f, 0f);
            arena.SetDepthPresentationVisible(true);
        }

        private static void AssertMirroredGround(LegacyArenaView host, LegacyArenaView guest)
        {
            Assert.That(host.PlayerGroundPosition.y, Is.EqualTo(guest.EnemyGroundPosition.y).Within(.0001f));
            Assert.That(host.EnemyGroundPosition.y, Is.EqualTo(guest.PlayerGroundPosition.y).Within(.0001f));
            Assert.That(host.PlayerDepthSortingOrder, Is.EqualTo(guest.EnemyDepthSortingOrder),
                "Equal ground depth also keeps the same canonical fighter in front on both screens.");
            Assert.That(host.EnemyDepthSortingOrder, Is.EqualTo(guest.PlayerDepthSortingOrder));
        }

        private static bool CircleVisible(Transform circle)
            => circle != null && circle.gameObject.activeInHierarchy && circle.GetComponent<Renderer>().enabled;

        private static void AssertGroundPresentation(LegacyArenaView arena)
        {
            Assert.That(CircleVisible(arena.PlayerGroundCircle), Is.True);
            Assert.That(CircleVisible(arena.EnemyGroundCircle), Is.True);
            Assert.That(Vector3.Distance(arena.PlayerGroundCircle.position, arena.PlayerGroundPosition), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(arena.EnemyGroundCircle.position, arena.EnemyGroundPosition), Is.LessThan(.001f));
            float difference = arena.PlayerGroundPosition.y - arena.EnemyGroundPosition.y;
            if (Mathf.Abs(difference) > .01f)
                Assert.That((arena.PlayerDepthSortingOrder - arena.EnemyDepthSortingOrder) * difference, Is.LessThan(0f));
        }

        private static LegacySkill Attack()
            => new LegacySkill(88301, "깊이 검증 연격", 0, 12, 12, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 3, 0, string.Empty, iconId: 1);

        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject root = new GameObject("Combat Depth Test");
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private RenderTexture target;
            public LegacyArenaView Arena { get; }
            public ArenaScope()
            {
                art = new LegacyDuelArt(); settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{\"attackRhythmEnabled\":true,\"attackRhythmStrength\":1,\"attackLungeDistance\":0.6}", settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                Arena.SetEnemyAppearance(EnemyAppearance.Elisa);
                Arena.Reset();
            }
            public void Resize(Vector2Int size)
            {
                // Render only this owned arena; the scene's original controller and other test arenas share layer 30.
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Arena.ArenaCamera.cullingMask = 1 << 31;
                Arena.ArenaCamera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                Arena.ArenaCamera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                Arena.ArenaCamera.targetTexture = target;
                Arena.ArenaCamera.aspect = size.x / (float)size.y;
            }
            public Color32[] Capture(string path = null)
            {
                Arena.ArenaCamera.Render();
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                RenderTexture old = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    if (!string.IsNullOrEmpty(path)) File.WriteAllBytes(path, texture.EncodeToPNG());
                    return texture.GetPixels32();
                }
                finally { RenderTexture.active = old; Object.Destroy(texture); }
            }
            public void Dispose()
            {
                Arena.ArenaCamera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                Arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(root);
            }
        }
    }
}
