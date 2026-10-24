using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SlowMotionMovementPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Speed = .6f;

        [UnityTest]
        public IEnumerator ManualSuccess_TakesALongerStride_WhileMissAutomaticAndRepeatedStepsKeepTheirDistance()
        {
            yield return null;
            using (var scope = new MotionScope())
            foreach (LegacyStepAction action in new[] { LegacyStepAction.Dodge, LegacyStepAction.Pressure })
            {
                scope.Begin(separation: 8f);
                Vector3 ordinary = scope.Step(action, false);
                float ordinaryDuration = scope.Arena.StepGestureDuration;
                scope.Begin(separation: 8f);
                Vector3 successful = scope.Step(action, true);
                Assert.That(successful.x, Is.EqualTo(ordinary.x * 1.45f).Within(.0001f));
                Assert.That(scope.Arena.StepGestureDuration, Is.EqualTo(ordinaryDuration * 1.8f).Within(.0001f),
                    "The longer existing success window is retained while the body travels farther.");
                Assert.That(successful.y, Is.EqualTo(ordinary.y * 1.45f).Within(.0001f));

                Vector3 repeated = scope.Step(action, true);
                Assert.That(repeated.x, Is.EqualTo(ordinary.x).Within(.0001f),
                    "Repeated input cannot stack the cinematic stride in one skill.");
                Assert.That(Mathf.Abs(repeated.y), Is.EqualTo(Mathf.Abs(ordinary.y)).Within(.0001f));
                scope.Begin(separation: 8f);
                Vector3 automatic = scope.Step(action, true, cinematic: false);
                Assert.That(automatic, Is.EqualTo(ordinary), "Automatic steps retain their authored travel.");
                Assert.That(scope.Arena.StepGestureDuration, Is.EqualTo(ordinaryDuration).Within(.0001f));

                if (action != LegacyStepAction.Pressure) continue;
                scope.Begin(separation: LegacyArenaView.MinimumStepSeparation + .05f);
                Vector3 clamped = scope.Step(action, true);
                Assert.That(clamped.x, Is.EqualTo(.05f).Within(.0001f));
                Assert.That(clamped.y, Is.Zero, "Pressure's larger stride must not invent sideways travel.");
                Assert.That(scope.Arena.Separation, Is.EqualTo(LegacyArenaView.MinimumStepSeparation).Within(.0001f));
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulDodge_NearEitherBoundaryChoosesRoomForTheWholeLargerStride()
        {
            yield return null;
            using (var scope = new MotionScope())
            foreach (int perspective in new[] { 0, 1 })
            foreach (float depth in new[] { -1.2f, -.9f, .9f, 1.2f })
            {
                scope.Begin(playerDepth: depth);
                scope.Arena.SetDepthPerspective(perspective);
                Vector3 movement = scope.Step(LegacyStepAction.Dodge, true);
                Assert.That(Mathf.Sign(movement.y), Is.EqualTo(-Mathf.Sign(depth)));
                Assert.That(Mathf.Abs(movement.y), Is.EqualTo(.406f).Within(.0001f),
                    "The side selection must reserve the complete cinematic stride, not the smaller ordinary stride.");
                Assert.That(scope.Arena.PlayerRenderer.transform.localPosition.y - scope.Arena.ActorGroundY,
                    Is.InRange(-1.2f, 1.2f));
            }
        }

        [UnityTest]
        public IEnumerator DecisiveSlowMotion_AmplifiesOnlyThePlayersDrawnLunge_WithoutMovingFeetOrCombatAnchors()
        {
            yield return null;
            using (var scope = new MotionScope())
            foreach (bool enemyReceivedHit in new[] { true, false })
            {
                scope.Begin(); scope.Hold(.46f);
                Vector3 ordinary = scope.Arena.PlayerAttackVisualOffset;
                Vector3 enemyOffset = scope.Arena.EnemyAttackVisualOffset;
                Vector3 playerAnchor = scope.Arena.PlayerRenderer.transform.position;
                Vector3 enemyAnchor = scope.Arena.EnemyRenderer.transform.position;
                Vector3 playerFeet = scope.Arena.PlayerGroundPosition;
                Vector3 enemyFeet = scope.Arena.EnemyGroundPosition;
                bool inRange = scope.Arena.IsInRange;
                Assert.That(ordinary.x, Is.GreaterThan(.1f), "The real attack shader displacement is active.");
                scope.Capture(enemyReceivedHit ? "attack-normal" : "received-hit-normal");
                scope.Arena.PresentDecisiveFocus(enemyReceivedHit);
                scope.Arena.Tick(0f, .12f);
                scope.Hold(.46f);
                Assert.That(scope.Arena.PlayerAttackVisualOffset.x, Is.GreaterThan(ordinary.x * 1.5f));
                Assert.That(scope.Arena.PlayerAttackVisualOffset.x, Is.LessThanOrEqualTo(ordinary.x * 1.7001f));
                Assert.That(scope.Arena.EnemyAttackVisualOffset, Is.EqualTo(enemyOffset));
                Assert.That(scope.Arena.PlayerRenderer.transform.position, Is.EqualTo(playerAnchor));
                Assert.That(scope.Arena.EnemyRenderer.transform.position, Is.EqualTo(enemyAnchor));
                Assert.That(scope.Arena.PlayerGroundPosition, Is.EqualTo(playerFeet));
                Assert.That(scope.Arena.EnemyGroundPosition, Is.EqualTo(enemyFeet));
                Assert.That(scope.Arena.IsInRange, Is.EqualTo(inRange));
                scope.Capture(enemyReceivedHit ? "attack-slow" : "received-hit-slow");
                Vector3 held = scope.Arena.PlayerAttackVisualOffset;
                scope.Hold(.46f); scope.Hold(.46f);
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(held),
                    "Controller contact holds resample the pose without advancing or compounding its movement boost.");
            }
        }

        [UnityTest]
        public IEnumerator SlowMovement_FreezesWithPresentationTime_AndHidesThenRestoresAcrossCutscenes()
        {
            yield return null;
            using (var scope = new MotionScope())
            {
                scope.Begin(); scope.Hold(.4f);
                scope.Arena.PresentDecisiveFocus(true); scope.Arena.Tick(0f, .12f); scope.Hold(.4f);
                float amount = scope.Arena.SlowMotionMovementAmount;
                Vector3 drawnOffset = scope.Arena.PlayerAttackVisualOffset;
                Assert.That(amount, Is.GreaterThan(.8f));
                for (int frame = 0; frame < 12; frame++) { scope.Arena.Tick(0f, 0f); scope.Hold(.4f); }
                Assert.That(scope.Arena.SlowMotionMovementAmount, Is.EqualTo(amount));
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(drawnOffset));
                Invoke(scope.Arena, "SuspendForCutscene");
                Assert.That(scope.Arena.SlowMotionMovementAmount, Is.Zero);
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero));
                scope.Arena.Tick(0f, .1f);
                Assert.That(scope.Arena.SlowMotionMovementAmount, Is.Zero, "Scenes cannot borrow combat movement.");
                Invoke(scope.Arena, "ResumeAfterCutscene");
                Assert.That(scope.Arena.SlowMotionMovementAmount, Is.EqualTo(amount),
                    "The suspended easing state is restored with the exact battle pose.");
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(drawnOffset));
                scope.Hold(.4f);
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(drawnOffset));
            }
        }

        [UnityTest]
        public IEnumerator SlowMovement_ClearsAtPhaseBoundaries_AndDoesNotAddMovementToPlanningMissesOrIdlePoses()
        {
            yield return null;
            using (var scope = new MotionScope())
            {
                foreach (string boundary in new[] { "turn", "end", "reset" })
                {
                    scope.Begin(); scope.Arena.PresentDecisiveFocus(true); scope.Arena.Tick(0f, .12f);
                    Assert.That(scope.Arena.SlowMotionMovementAmount, Is.GreaterThan(.8f));
                    if (boundary == "turn") scope.Arena.BeginTurn();
                    else if (boundary == "end") scope.Arena.EndTurn();
                    else scope.Arena.Reset();
                    Assert.That(scope.Arena.SlowMotionMovementAmount, Is.Zero, boundary);
                    Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero), boundary);
                }
                scope.Arena.SetPlanningState(5f, false, true); scope.Arena.Tick(.01f, .12f);
                Assert.That(scope.Arena.SlowMotionMovementAmount, Is.Zero, "Planning bullet time is not an action reward.");
                scope.Begin(); scope.Step(LegacyStepAction.Dodge, false);
                Assert.That(scope.Arena.SlowMotionMovementAmount, Is.Zero);
                scope.Begin(); scope.Arena.BeginSlot(null, null); scope.Arena.PresentDecisiveFocus(true);
                Vector3 idle = scope.Arena.PlayerRenderer.transform.position;
                scope.Arena.Tick(0f, .12f);
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero));
                Assert.That(scope.Arena.PlayerRenderer.transform.position, Is.EqualTo(idle),
                    "The dramatic lunge follows an attack pose; slow motion alone cannot move an idle fighter.");
            }
        }

        private static void Invoke(LegacyArenaView arena, string method, params object[] arguments) =>
            typeof(LegacyArenaView).GetMethod(method, Private).Invoke(arena, arguments);

        private sealed class MotionScope : IDisposable
        {
            private readonly GameObject root = new GameObject("Slow Motion Movement Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private RenderTexture target;
            public readonly LegacyArenaView Arena;

            public MotionScope()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                JsonUtility.FromJsonOverwrite("{\"battleDriftSpeed\":0,\"impactCameraShake\":0," +
                    "\"attackRhythmEnabled\":true,\"attackRhythmStrength\":1,\"attackLungeDistance\":0.6}", settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                Arena.SetEnemyAppearance(EnemyAppearance.Elisa);
            }

            public void Begin(float separation = 4f, float playerDepth = 0f)
            {
                Arena.Reset();
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-separation * .5f, Arena.ActorGroundY + playerDepth, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(separation * .5f, Arena.ActorGroundY, 0f);
                var attack = new LegacySkill(98794, "슬로모션 동선 검증", 0, 5, 5,
                    LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty);
                Arena.SetAttackRhythmOverride(true);
                Invoke(Arena, "ConfigureSlotTiming", Speed, .1f);
                Arena.BeginSlot(attack, attack);
            }

            public Vector3 Step(LegacyStepAction action, bool success, bool cinematic = true)
            {
                Vector3 start = Arena.PlayerRenderer.transform.position;
                Arena.PerformStep(action, success, cinematic: cinematic);
                Arena.Tick(0f, Arena.StepGestureDuration);
                return Arena.PlayerRenderer.transform.position - start;
            }

            public void Hold(float phase) => Invoke(Arena, "HoldSlotAtTime", LegacyArenaView.OriginalClipDuration / Speed * phase);

            public void Capture(string name)
            {
                string output = Environment.GetEnvironmentVariable("SWORDMASTER_SLOW_MOVE_CAPTURE_DIR");
                if (string.IsNullOrEmpty(output)) return;
                Directory.CreateDirectory(output);
                if (target == null)
                {
                    target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                    Arena.ArenaCamera.targetTexture = target; Arena.ArenaCamera.aspect = 1280f / 720f;
                }
                Volume volume = root.GetComponentInChildren<Volume>(true);
                bool enabled = volume != null && volume.enabled;
                if (volume != null) volume.enabled = false;
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Camera camera = Arena.ArenaCamera; camera.cullingMask = 1 << 31;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                if (volume != null) volume.enabled = enabled;
                camera.Render();
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
                Arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(root);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
