using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class GroundFootworkPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator HorizontalArrival_PreservesBothDepths_AcceleratesThenBrakes_AndHitStopHoldsItsVelocity()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                scope.Place(-6f, -.15f, 6f, .45f);
                float playerY = arena.PlayerGroundPosition.y, enemyY = arena.EnemyGroundPosition.y;
                arena.BeginApproach();
                var speeds = new List<float>();
                for (int frame = 0; frame < 1000 && !arena.ApproachComplete; frame++)
                {
                    Vector3 before = arena.PlayerGroundPosition;
                    arena.Tick(.01f, .01f);
                    speeds.Add(Vector3.Distance(arena.PlayerGroundPosition, before) / .01f);
                    Assert.That(arena.Separation, Is.GreaterThanOrEqualTo(LegacyArenaView.ContactDistance - .011f),
                        "Arrival cannot overshoot through the opponent's contact surface.");
                    Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(playerY).Within(.0001f));
                    Assert.That(arena.EnemyGroundPosition.y, Is.EqualTo(enemyY).Within(.0001f),
                        "Approaching attacks preserve the actual floor depth instead of converging on a shared row.");
                    if (frame == 4)
                    {
                        Vector3 held = arena.PlayerGroundPosition;
                        Vector2 velocity = Velocity(arena, true);
                        Assert.That(velocity.magnitude, Is.GreaterThan(.01f));
                        arena.Tick(0f, .2f);
                        Assert.That(arena.PlayerGroundPosition, Is.EqualTo(held));
                        Assert.That(Velocity(arena, true), Is.EqualTo(velocity), "A stopped combat clock neither moves nor brakes the retained velocity.");
                    }
                }
                Assert.That(arena.ApproachComplete, Is.True);
                Assert.That(arena.IsInRange, Is.True);
                Vector2 playerVelocity = Velocity(arena, true), enemyVelocity = Velocity(arena, false);
                SetVelocity(arena, true, new Vector2(playerVelocity.x, .2f));
                SetVelocity(arena, false, new Vector2(enemyVelocity.x, -.2f));
                Assert.That(arena.IsInRange, Is.True,
                    "Reaching horizontal contact permits the exchange even while floor-depth momentum is settling.");
                SetVelocity(arena, true, playerVelocity); SetVelocity(arena, false, enemyVelocity);
                Assert.That(speeds.Count, Is.GreaterThan(10));
                float peak = 0f;
                foreach (float speed in speeds) peak = Mathf.Max(peak, speed);
                Assert.That(speeds[0], Is.LessThan(peak * .7f), "The first step starts below full travel speed.");
                float last = 0f;
                for (int i = speeds.Count - 5; i < speeds.Count; i++) last += speeds[i];
                Assert.That(last / 5f, Is.LessThan(peak * .85f), "The approach brakes across several final steps rather than stopping a constant-speed slide abruptly.");
                Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(playerY).Within(.0001f));
                Assert.That(arena.EnemyGroundPosition.y, Is.EqualTo(enemyY).Within(.0001f));
                for (int frame = 0; frame < 15; frame++)
                {
                    arena.Tick(.01f, .01f);
                    Assert.That(arena.IsInRange, Is.True, "Finishing the approach must not coast back out of reach.");
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator HorizontalContact_WithExistingDepthMomentum_BrakesBrieflyWithoutWaitingOrReturningToARow()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                scope.Place(-2.01f, -.35f, 2.01f, .35f);
                arena.BeginApproach();
                // A preceding sideways action left the fighters moving away from one another in depth.
                // Their next approach needs only a small horizontal step and must preserve that physical momentum.
                SetVelocity(arena, true, new Vector2(0f, -8f));
                SetVelocity(arena, false, new Vector2(0f, 8f));
                Vector3 playerStart = arena.PlayerGroundPosition, enemyStart = arena.EnemyGroundPosition;
                arena.Tick(.01f, .01f);
                Assert.That(arena.PlayerGroundPosition.y, Is.LessThan(playerStart.y - .001f));
                Assert.That(arena.EnemyGroundPosition.y, Is.GreaterThan(enemyStart.y + .001f),
                    "Existing depth momentum continues briefly instead of snapping to rest or attracting the fighters together.");
                Assert.That(Velocity(arena, true).y, Is.LessThan(-.01f).And.GreaterThan(-8f));
                Assert.That(Velocity(arena, false).y, Is.GreaterThan(.01f).And.LessThan(8f));
                Assert.That(arena.IsInRange, Is.True,
                    "Horizontal contact admits the next exchange while the existing depth velocity is still braking.");

                Vector3 heldPlayer = arena.PlayerGroundPosition, heldEnemy = arena.EnemyGroundPosition;
                Vector2 heldPlayerVelocity = Velocity(arena, true), heldEnemyVelocity = Velocity(arena, false);
                arena.Tick(0f, .2f);
                Assert.That(arena.PlayerGroundPosition, Is.EqualTo(heldPlayer));
                Assert.That(arena.EnemyGroundPosition, Is.EqualTo(heldEnemy));
                Assert.That(Velocity(arena, true), Is.EqualTo(heldPlayerVelocity));
                Assert.That(Velocity(arena, false), Is.EqualTo(heldEnemyVelocity),
                    "A stopped combat clock freezes the retained depth momentum too.");

                for (int frame = 0; frame < 3; frame++) arena.Tick(.01f, .01f);
                Assert.That(arena.ApproachComplete, Is.True,
                    "Only the normal brief approach settle applies; depth convergence or a complete depth brake adds no wait.");
                Assert.That(Mathf.Abs(Velocity(arena, true).y), Is.GreaterThan(.01f));
                Assert.That(Mathf.Abs(Velocity(arena, false).y), Is.GreaterThan(.01f));
                arena.BeginSlot(Attack(), Attack());
                for (int frame = 0; frame < 30; frame++)
                {
                    arena.Tick(.01f, .01f);
                    Assert.That(arena.IsInRange, Is.True);
                    Assert.That(playerStart.y - arena.PlayerGroundPosition.y, Is.InRange(0f, .1801f));
                    Assert.That(arena.EnemyGroundPosition.y - enemyStart.y, Is.InRange(0f, .1801f),
                        "Residual depth momentum plants within a short stride even when the skill starts before rest.");
                }
                Assert.That(Mathf.Abs(Velocity(arena, true).y), Is.LessThan(.0001f));
                Assert.That(Mathf.Abs(Velocity(arena, false).y), Is.LessThan(.0001f));
                float playerReached = arena.PlayerGroundPosition.y, enemyReached = arena.EnemyGroundPosition.y;
                Assert.That(playerReached, Is.LessThan(playerStart.y - .01f));
                Assert.That(enemyReached, Is.GreaterThan(enemyStart.y + .01f));
                for (int frame = 0; frame < 30; frame++)
                {
                    arena.Tick(.01f, .01f);
                    Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(playerReached).Within(.0001f));
                    Assert.That(arena.EnemyGroundPosition.y, Is.EqualTo(enemyReached).Within(.0001f));
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AngledImpact_FollowsTheAttackLine_OnBothPerspectives_AndReturnKeepsTheReachedGround()
        {
            yield return null;
            using (var host = new ArenaScope())
            using (var guest = new ArenaScope())
            {
                host.Arena.SetDepthPerspective(0); guest.Arena.SetDepthPerspective(1);
                host.Place(-2f, -.04f, 2f, .04f);
                guest.Place(-2f, .04f, 2f, -.04f);
                host.Arena.BeginSlot(Attack(LegacySkillProperty.Hit), Attack(LegacySkillProperty.Hit));
                guest.Arena.BeginSlot(Attack(LegacySkillProperty.Hit), Attack(LegacySkillProperty.Hit));
                Vector3 hostStart = host.Arena.EnemyRenderer.transform.localPosition;
                Vector3 guestStart = guest.Arena.PlayerRenderer.transform.localPosition;
                host.Arena.PresentHit(true, 12, 0, false, false, 12);
                guest.Arena.PresentHit(false, 12, 0, false, false, 12);
                Vector3 hostPush = host.Arena.EnemyKnockbackTarget - hostStart;
                Vector3 guestPush = guest.Arena.PlayerKnockbackTarget - guestStart;
                Assert.That(hostPush.x, Is.GreaterThan(0f));
                Assert.That(hostPush.y, Is.GreaterThan(.001f), "A glancing attack retains its actual upward component.");
                Assert.That(hostPush.y / hostPush.x, Is.LessThanOrEqualTo(.35001f));
                Assert.That(guestPush.x, Is.EqualTo(-hostPush.x).Within(.001f));
                Assert.That(guestPush.y, Is.EqualTo(hostPush.y).Within(.001f));
                for (int frame = 0; frame < 40; frame++)
                {
                    host.Arena.Tick(.01f, .01f); guest.Arena.Tick(.01f, .01f);
                    Assert.That(host.Arena.EnemyGroundPosition.y, Is.EqualTo(guest.Arena.PlayerGroundPosition.y).Within(.001f));
                }
                float reachedY = host.Arena.EnemyGroundPosition.y;
                host.Arena.EndTurn();
                for (int frame = 0; frame < 100; frame++)
                {
                    host.Arena.SetPlanningState(10f, false);
                    host.Arena.Tick(.01f, .01f);
                    Assert.That(host.Arena.EnemyGroundPosition.y, Is.EqualTo(reachedY).Within(.005f),
                        "Turn return and idle planning do not pull the impact back to a prescribed row.");
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DodgeUsesTheFloorSideways_ThenKeepsThatGround_WhilePressureAddsNoVerticalGesture()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                var displacements = new List<float>();
                foreach (int perspective in new[] { 0, 1 })
                {
                    arena.Reset(); arena.SetDepthPerspective(perspective); scope.Place(-2f, 0f, 2f, 0f);
                    arena.BeginSlot(Attack(), Attack());
                    float start = arena.PlayerGroundPosition.y;
                    arena.PerformStep(LegacyStepAction.Dodge);
                    for (int frame = 0; frame < 15; frame++) arena.Tick(.01f, .01f);
                    float reachedY = arena.PlayerGroundPosition.y;
                    displacements.Add(reachedY - start);
                    Assert.That(Mathf.Abs(reachedY - start), Is.GreaterThan(.2f));
                    arena.EndTurn();
                    for (int frame = 0; frame < 60; frame++)
                    {
                        arena.SetPlanningState(10f, false);
                        arena.Tick(.01f, .01f);
                        Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(reachedY).Within(.005f));
                    }
                    arena.BeginSlot(Attack(), Attack());
                    for (int frame = 0; frame < 20; frame++) arena.Tick(.01f, .01f);
                    Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(reachedY).Within(.005f), "A later skill cannot reset a sidestep's ground depth.");
                    for (int frame = 0; frame < 120 && !arena.IsInRange; frame++)
                    {
                        arena.CloseDistance(.01f);
                        arena.Tick(.01f, .01f);
                    }
                    Assert.That(arena.IsInRange, Is.True, "Contact after a sidestep depends on horizontal range, not matching floor depth.");
                    Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(reachedY).Within(.005f),
                        "Closing for the next skill cannot undo the completed sidestep.");
                    arena.BeginSlot(Attack(), Attack());
                    for (int frame = 0; frame < 30; frame++)
                    {
                        arena.Tick(.01f, .01f);
                        Assert.That(arena.IsInRange, Is.True, "Starting the next skill cannot coast out of contact again.");
                    }
                }
                Assert.That(displacements[0], Is.EqualTo(-displacements[1]).Within(.001f));
                arena.Reset(); scope.Place(-2f, 0f, 2f, 0f); arena.BeginSlot(Attack(), Attack());
                float pressureStart = arena.PlayerGroundPosition.y;
                arena.PerformStep(LegacyStepAction.Pressure);
                for (int frame = 0; frame < 15; frame++) arena.Tick(.01f, .01f);
                Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(pressureStart).Within(.0001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedDodges_BalanceBothDirections_AcrossSlotsAndTurns_WithoutPressureConsumingASide()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                scope.Place(-2f, 0f, 2f, 0f);
                float start = arena.PlayerGroundPosition.y, first = 0f, previous = 0f;
                string output = Environment.GetEnvironmentVariable("SWORDMASTER_DODGE_BALANCE_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(output)) Directory.CreateDirectory(output);
                var trace = new DodgeBalanceTrace();
                for (int dodge = 0; dodge < 12; dodge++)
                {
                    float displacement = CompleteDodge(arena, success: dodge % 3 == 0, cinematic: dodge % 3 != 2);
                    trace.displacements.Add(displacement); trace.depths.Add(arena.PlayerGroundPosition.y);
                    if (displacement > 0f) trace.upward++; else trace.downward++;
                    if (dodge < 2 && !string.IsNullOrEmpty(output))
                    {
                        yield return null;
                        scope.Capture(Path.Combine(output, displacement > 0f ? "upward-dodge.png" : "downward-dodge.png"));
                    }
                    if (dodge == 0) first = displacement;
                    else Assert.That(Mathf.Sign(displacement), Is.EqualTo(-Mathf.Sign(previous)),
                        "Successive dodges alternate floor direction even across a new skill or turn.");
                    previous = displacement;
                    if (dodge % 2 == 0)
                    {
                        arena.BeginSlot(Attack(), Attack());
                        float beforePressure = arena.PlayerGroundPosition.y;
                        arena.PerformStep(LegacyStepAction.Pressure, cinematic: false);
                        arena.Tick(.15f, .15f);
                        Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(beforePressure).Within(.0001f));
                        arena.EndTurn(); arena.BeginTurn();
                    }
                }
                Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(start).Within(.0001f),
                    "An even number of unobstructed dodges cannot accumulate an upward or downward drift.");
                Assert.That(trace.upward, Is.EqualTo(6)); Assert.That(trace.downward, Is.EqualTo(6));
                if (!string.IsNullOrEmpty(output))
                    File.WriteAllText(Path.Combine(output, "dodge-balance-trace.json"), JsonUtility.ToJson(trace, true));
                Assert.That(CompleteDodge(arena, success: true, cinematic: true), Is.EqualTo(first).Within(.0001f));
                arena.Reset(); scope.Place(-2f, 0f, 2f, 0f);
                Assert.That(CompleteDodge(arena, success: true, cinematic: true), Is.EqualTo(first).Within(.0001f),
                    "A new battle resets the direction sequence after an odd number of dodges.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DodgeNearEitherDepthBoundary_TakesAFullInwardStep_ThenAlternatesFromTheActualSide()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                foreach (int perspective in new[] { 0, 1 })
                foreach (float depth in new[] { -1.2f, -1.15f, 1.15f, 1.2f })
                {
                    arena.Reset(); arena.SetDepthPerspective(perspective); scope.Place(-2f, depth, 2f, 0f);
                    float inward = CompleteDodge(arena);
                    Assert.That(Mathf.Sign(inward), Is.EqualTo(-Mathf.Sign(depth)),
                        "At either edge the dodge must take the available inward side, including mirrored battles.");
                    scope.Place(-2f, 0f, 2f, 0f);
                    Assert.That(CompleteDodge(arena), Is.EqualTo(-inward).Within(.0001f),
                        "The next direction alternates from the side actually travelled after an edge redirect.");
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualApproachAndRecoil_CanBeRenderedAsShortMotionSequences_WithGroundAndVelocityTraces()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                string output = Environment.GetEnvironmentVariable("SWORDMASTER_GROUND_FOOTWORK_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(output)) Directory.CreateDirectory(output);
                var trace = new MotionTrace();
                foreach (string sequence in new[] { "approach", "recoil" })
                {
                    scope.Arena.Reset();
                    if (sequence == "approach") { scope.Place(-5f, -.15f, 5f, .45f); scope.Arena.BeginApproach(); }
                    else
                    {
                        scope.Place(-2f, -.04f, 2f, .04f);
                        scope.Arena.BeginSlot(Attack(LegacySkillProperty.Hit), Attack());
                        scope.Arena.PresentHit(true, 12, 0, false, false, 12);
                    }
                    float time = 0f;
                    foreach (float sample in new[] { 0f, .05f, .10f, .20f, .35f, .55f, .90f })
                    {
                        while (time + .00001f < sample)
                        {
                            float delta = Mathf.Min(.01f, sample - time);
                            scope.Arena.Tick(delta, delta); time += delta;
                        }
                        trace.frames.Add(new MotionFrame
                        {
                            sequence = sequence, time = time,
                            player = scope.Arena.PlayerGroundPosition, enemy = scope.Arena.EnemyGroundPosition,
                            playerVelocity = Velocity(scope.Arena, true), enemyVelocity = Velocity(scope.Arena, false),
                            playerCircle = scope.Arena.PlayerGroundCircle.position, enemyCircle = scope.Arena.EnemyGroundCircle.position,
                            separation = scope.Arena.Separation, inRange = scope.Arena.IsInRange,
                            playerOrder = scope.Arena.PlayerDepthSortingOrder, enemyOrder = scope.Arena.EnemyDepthSortingOrder
                        });
                        yield return null;
                        if (!string.IsNullOrEmpty(output)) scope.Capture(Path.Combine(output, sequence + "-" + Mathf.RoundToInt(time * 1000f).ToString("000") + "ms.png"));
                    }
                    Assert.That(scope.Arena.IsInRange, Is.True, sequence + " must finish and remain in actual contact range.");
                    if (sequence == "approach") Assert.That(scope.Arena.ApproachComplete, Is.True);
                }
                Assert.That(trace.frames.Count, Is.EqualTo(14));
                if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "ground-motion-trace.json"), JsonUtility.ToJson(trace, true));
            }
            yield return null;
        }

        private static float CompleteDodge(LegacyArenaView arena, bool success = false, bool cinematic = false)
        {
            arena.BeginSlot(Attack(), Attack());
            float start = arena.PlayerGroundPosition.y, previous = start, sign = 0f;
            arena.PerformStep(LegacyStepAction.Dodge, success, cinematic: cinematic);
            float gestureDuration = arena.StepGestureDuration;
            foreach (float share in new[] { .13f, .07f, .31f, .49f })
            {
                float delta = gestureDuration * share;
                arena.Tick(delta, delta);
                float reached = arena.PlayerGroundPosition.y;
                if (sign == 0f) sign = Mathf.Sign(reached - start);
                Assert.That((reached - previous) * sign, Is.GreaterThanOrEqualTo(-.0001f),
                    "A dodge chooses its direction once; changing frame sizes cannot reverse it mid-step.");
                arena.Tick(0f, 0f);
                Assert.That(arena.PlayerGroundPosition.y, Is.EqualTo(reached).Within(.0001f),
                    "Paused presentation cannot move the fighter or consume another direction.");
                previous = reached;
            }
            float displacement = arena.PlayerGroundPosition.y - start;
            Assert.That(Mathf.Abs(displacement), Is.EqualTo(.28f * (success && cinematic ? 1.45f : 1f)).Within(.0001f),
                "Available floor space permits the complete sideways stride instead of a clipped edge gesture.");
            return displacement;
        }

        private static Vector2 Velocity(LegacyArenaView arena, bool player)
        {
            object actor = typeof(LegacyArenaView).GetField(player ? "player" : "enemy", Private).GetValue(arena);
            return (Vector2)actor.GetType().GetField("GroundVelocity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(actor);
        }

        private static void SetVelocity(LegacyArenaView arena, bool player, Vector2 velocity)
        {
            object actor = typeof(LegacyArenaView).GetField(player ? "player" : "enemy", Private).GetValue(arena);
            actor.GetType().GetField("GroundVelocity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(actor, velocity);
        }

        private static LegacySkill Attack(LegacySkillProperty property = LegacySkillProperty.Slash)
            => new LegacySkill(88311, "보법 검증", 0, 12, 12, LegacySkillKind.Attack,
                property, 3, 0, string.Empty, iconId: 1);
        [Serializable] private sealed class DodgeBalanceTrace
        {
            public List<float> displacements = new List<float>();
            public List<float> depths = new List<float>();
            public int upward, downward;
        }
        [Serializable] private sealed class MotionTrace { public List<MotionFrame> frames = new List<MotionFrame>(); }
        [Serializable] private sealed class MotionFrame
        {
            public string sequence;
            public float time, separation;
            public Vector3 player, enemy, playerCircle, enemyCircle;
            public Vector2 playerVelocity, enemyVelocity;
            public int playerOrder, enemyOrder;
            public bool inRange;
        }

        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject root = new GameObject("Natural Ground Footwork Test");
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly RenderTexture target;
            public LegacyArenaView Arena { get; }
            public ArenaScope()
            {
                art = new LegacyDuelArt(); settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor); Arena.SetEnemyAppearance(EnemyAppearance.Elisa); Arena.Reset();
                target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
                Arena.ArenaCamera.targetTexture = target; Arena.ArenaCamera.aspect = 960f / 540f;
            }
            public void Place(float playerX, float playerY, float enemyX, float enemyY)
            {
                Arena.PlayerRenderer.transform.localPosition = new Vector3(playerX, Arena.ActorGroundY + playerY, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(enemyX, Arena.ActorGroundY + enemyY, 0f);
                Arena.Tick(0f, 0f);
            }
            public void Capture(string path)
            {
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Arena.ArenaCamera.cullingMask = 1 << 31;
                Arena.ArenaCamera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                Arena.ArenaCamera.Render();
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                RenderTexture old = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { RenderTexture.active = old; Object.Destroy(texture); }
            }
            public void Dispose()
            {
                Arena.ArenaCamera.targetTexture = null; target.Release(); Object.Destroy(target);
                Arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(root);
            }
        }
    }
}
