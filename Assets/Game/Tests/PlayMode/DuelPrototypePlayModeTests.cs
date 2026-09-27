using System.Collections;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class DuelPrototypePlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator KeyboardTap_QueuesOnRelease_AndSpaceCommitsTheWholeTurn()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            // The official fixture isolates OS input and synchronizes events with the PlayerLoop.
            Press(keyboard.qKey);
            yield return null;
            yield return null;
            Assert.That(keyboard.qKey.isPressed, Is.True);
            Assert.That(controller.Session.PlayerQueue.Count, Is.Zero, "The original short tap queues on release, not press.");
            Release(keyboard.qKey);
            yield return null;
            yield return null;
            Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(1));
            Assert.That(controller.Session.PlayerQueue[0].Id, Is.EqualTo(1));
            Assert.That(controller.Session.Act, Is.EqualTo(2));
            Assert.That(controller.PlayerHealth, Is.EqualTo(100));
            Assert.That(controller.EnemyHealth, Is.EqualTo(80));

            Press(keyboard.spaceKey);
            yield return null;
            yield return null;
            Assert.That(controller.IsResolving, Is.True);
            Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(1));
            Release(keyboard.spaceKey);
            yield return WaitUntilPlanningOrFinished(controller);
            Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator QueuePlanning_DoesNotResolveUntilCommit_ThenContinuesToNextTurn()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Assert.That(controller.HasRequiredArt, Is.True, "Original actor sprites, background and font, plus the new skill buttons, must import.");
            Assert.That(controller.PlayerHealth, Is.EqualTo(100));
            Assert.That(controller.EnemyHealth, Is.EqualTo(80));
            Assert.That(controller.Session.Act, Is.EqualTo(3));
            Assert.That(controller.Session.EnemyQueue.Count, Is.EqualTo(2));
            Assert.That(controller.QueueLane(0), Is.True);
            Assert.That(controller.QueueLane(1), Is.True);
            Assert.That(controller.QueueLane(2), Is.True);
            Assert.That(controller.PlayerHealth, Is.EqualTo(100));
            Assert.That(controller.EnemyHealth, Is.EqualTo(80));
            Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(3));
            Assert.That(controller.Session.PlayerQueue[0].Id, Is.EqualTo(1));
            Assert.That(controller.Session.PlayerQueue[1].Id, Is.EqualTo(3));
            Assert.That(controller.Session.PlayerQueue[2].Id, Is.EqualTo(5));
            Assert.That(controller.Session.GetLane(0)[0].Id, Is.EqualTo(2));
            Assert.That(controller.Session.Act, Is.Zero);
            Assert.That(controller.CanChoose, Is.True, "Running out of ACT must not auto-commit.");
            yield return CaptureFrame("LegacyDuel-Planning.png");
            controller.CommitTurn();
            Assert.That(controller.IsResolving, Is.True);
            Assert.That(controller.QueueLane(0), Is.False);
            yield return new WaitForSecondsRealtime(0.62f);
            yield return CaptureFrame("LegacyDuel-Clash.png");
            yield return WaitUntilPlanningOrFinished(controller);
            Assert.That(controller.CanChoose, Is.True);
            Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
            Assert.That(controller.Session.PlayerQueue.Count, Is.Zero);
            Assert.That(controller.Session.GetLane(0)[0].Id, Is.EqualTo(2));
            Assert.That(controller.Session.EnemyQueue.Count, Is.EqualTo(3));
            Assert.That(controller.Session.Act, Is.EqualTo(4), "Base ACT and original Cut reward must arrive next turn.");
            Assert.That(controller.TurnTimeRemaining, Is.GreaterThan(9f));
        }

        [UnityTest]
        public IEnumerator CompleteDuel_ProducesOutcome_AndRematchRestoresDeckAndResources()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            for (int turn = 0; turn < 60 && controller.Outcome == DuelMatchOutcome.InProgress; turn++)
            {
                // Select the three original lane heads as funds allow.
                bool queued;
                do
                {
                    queued = false;
                    for (int lane = 0; lane < 3; lane++)
                        queued |= controller.QueueLane(lane);
                } while (queued);
                controller.CommitTurn();
                yield return WaitUntilPlanningOrFinished(controller);
            }
            Assert.That(controller.Outcome, Is.Not.EqualTo(DuelMatchOutcome.InProgress));
            Assert.That(controller.CanChoose, Is.False);
            Assert.That(controller.Session.Player.IsDefeated || controller.Session.Enemy.IsDefeated, Is.True);
            yield return CaptureFrame("LegacyDuel-Outcome.png");
            controller.RestartMatch();
            Assert.That(controller.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            Assert.That(controller.PlayerHealth, Is.EqualTo(100));
            Assert.That(controller.EnemyHealth, Is.EqualTo(80));
            Assert.That(controller.Session.Act, Is.EqualTo(3));
            Assert.That(controller.Session.GetLane(0)[0].Id, Is.EqualTo(1));
            Assert.That(controller.Session.EnemyQueue[0].Id, Is.EqualTo(1));
            Assert.That(controller.Session.PlayerQueue.Count, Is.Zero);
            Assert.That(controller.TurnTimeRemaining, Is.EqualTo(10f));
        }

        [UnityTest]
        public IEnumerator PlanningDeadline_CommitsEmptyQueueAndResolvesEnemy()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            yield return new WaitForSecondsRealtime(10.1f);
            Assert.That(controller.IsResolving, Is.True);
            Assert.That(controller.Session.PlayerQueue.Count, Is.Zero);
            yield return WaitUntilPlanningOrFinished(controller);
            Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
            Assert.That(controller.PlayerHealth, Is.LessThan(100));
            Assert.That(controller.Session.Act, Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator OriginalWorldScale_ApproachesBeforeFirstImpact_AndContinuesAtTheDuelPosition()
        {
            yield return null;
            var controller = FindPrototype();
            controller.RestartMatch();
            var arena = controller.ArenaView;
            Assert.That(arena.HasMobStudentAnimations, Is.True);
            Assert.That(arena.PlayerRenderer.sprite.pixelsPerUnit, Is.EqualTo(MobStudentAnimationSet.PixelsPerUnit));
            Assert.That(arena.EnemyRenderer.sprite.pixelsPerUnit, Is.EqualTo(18f));
            Assert.That(arena.PlayerRenderer.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(arena.PlayerRenderer.transform.localPosition, Is.EqualTo(new Vector3(-5, -.5f, 0)));
            Assert.That(arena.EnemyRenderer.transform.localPosition, Is.EqualTo(new Vector3(5, -.5f, 0)));
            foreach (string actor in new[] { "Player", "Enemy0" })
                foreach (string clipName in new[] { "Idle", "Slash", "Penetrate", "Hit", "Defense" })
                {
                    var clip = Resources.Load<AnimationClip>("LegacyArena/Animation/" + actor + "/" + clipName);
                    Assert.That(clip, Is.Not.Null);
                    Assert.That(clip.events, Is.Empty, "The copied clips must not call the legacy combat a second time.");
                }
            controller.QueueLane(0);
            controller.CommitTurn();
            yield return new WaitForSecondsRealtime(LegacyArenaView.ApproachDuration * .5f);
            Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.GreaterThan(-5));
            Assert.That(arena.EnemyRenderer.transform.localPosition.x, Is.LessThan(5));
            Assert.That(controller.Session.CurrentSlot, Is.Null, "No damage should happen during the opening approach.");
            Assert.That(controller.PlayerHealth, Is.EqualTo(100));
            Assert.That(controller.EnemyHealth, Is.EqualTo(80));
            yield return WaitUntilPlanningOrFinished(controller);
            Assert.That(controller.CanChoose, Is.True);
            Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
            Assert.That(arena.Separation, Is.GreaterThanOrEqualTo(LegacyArenaView.ContactDistance - .01f),
                "The next turn must preserve the fighters' order instead of returning to fixed spawn points.");
        }

        [UnityTest]
        public IEnumerator OriginalImpact_HasParticlesKnockbackAndFocus_AndResetClearsThem()
        {
            yield return null;
            var controller = FindPrototype();
            controller.RestartMatch();
            controller.enabled = false;
            try
            {
                var arena = controller.ArenaView;
                arena.BeginApproach();
                arena.Tick(.5f, .5f);
                float enemyStart = arena.EnemyRenderer.transform.position.x;
                arena.BeginSlot(controller.Session.GetLane(0)[0], controller.Session.EnemyQueue[0]);
                arena.PresentHit(true, 8, 0, false, true, 4);
                arena.Tick(.1f, .1f);
                Assert.That(arena.EnemyRenderer.transform.position.x, Is.GreaterThan(enemyStart));
                Assert.That(arena.ActiveParticleCount, Is.GreaterThan(0), "The original impact prefab must emit particles.");
                Assert.That(arena.IsFatalFocus, Is.True);
                Assert.That(arena.ArenaCamera.orthographicSize, Is.LessThan(3.5f));
                foreach (var renderer in controller.GetComponentsInChildren<ParticleSystemRenderer>())
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null);
                        Assert.That(material.shader.isSupported, Is.True, material.shader.name);
                    }
                arena.Tick(0, .7f);
                Assert.That(arena.IsFatalFocus, Is.False, "Focus expires on real time, independently of slow motion.");
                controller.RestartMatch();
                Assert.That(arena.ActiveParticleCount, Is.Zero);
                Assert.That(arena.EnemyRenderer.color, Is.EqualTo(Color.white));
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(6));
            }
            finally { controller.enabled = true; }
        }

        [UnityTest]
        public IEnumerator OriginalHud_DetailsAreHiddenUntilInspect_AndControlsHideDuringCombat()
        {
            yield return null;
            var controller = FindPrototype();
            controller.RestartMatch();
            var hud = controller.Hud.Root.transform;
            Assert.That(hud.Find("Skill Explain").gameObject.activeSelf, Is.False);
            Assert.That(hud.Find("Enemy Skill Explain").gameObject.activeSelf, Is.False);
            var inputs = hud.Find("Input/Keys").GetComponent<CanvasGroup>();
            Assert.That(inputs.alpha, Is.EqualTo(1));
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.tabKey);
            yield return null;
            yield return null;
            Assert.That(hud.Find("Enemy Skill Explain").gameObject.activeSelf, Is.True);
            // Original A commit remains available even while examining the enemy queue.
            Press(keyboard.spaceKey);
            yield return null;
            yield return null;
            Assert.That(controller.IsResolving, Is.True);
            Assert.That(inputs.alpha, Is.Zero);
            Assert.That(inputs.blocksRaycasts, Is.False);
            Assert.That(hud.Find("Enemy Skill Explain").gameObject.activeSelf, Is.False);
            Release(keyboard.tabKey);
            Release(keyboard.spaceKey);
            yield return WaitUntilPlanningOrFinished(controller);
            Assert.That(inputs.alpha, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CompactStatusBars_AndOptionalCombatLog_RestoreAndReset()
        {
            yield return null;
            var controller = FindPrototype();
            controller.RestartMatch();
            var root = controller.Hud.Root.transform;
            var retryLabel = root.Find("FadePanel/Retry/Retry Label").GetComponent<Text>();
            Assert.That(retryLabel.color.grayscale, Is.GreaterThan(.7f), "The compact retry label must remain readable over the dark button.");
            Assert.That(root.Find("FadePanel/Retry/Surface").GetComponent<Image>().color.grayscale, Is.LessThan(.35f),
                "Contrast is measured against the inner button surface, not its bright accent border.");
            foreach (string statusName in new[] { "PlayerStatus", "EnemyStatus" })
            {
                Transform status = root.Find(statusName);
                Assert.That(status.localScale, Is.EqualTo(Vector3.one));
                foreach (string barName in new[] { "HP", "HP delayed", "Resistance", "Resistance delayed" })
                {
                    Image bar = status.Find(barName).GetComponent<Image>();
                    Assert.That(bar.type, Is.EqualTo(Image.Type.Filled));
                    Assert.That(bar.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
                    Assert.That(bar.GetComponent<Mask>(), Is.Null, "Compact bars must not retain the original fan masks.");
                    Assert.That(Mathf.DeltaAngle(bar.transform.localEulerAngles.z, 0), Is.EqualTo(0).Within(.1f));
                }
            }
            var open = root.Find("Input/Keys/LogButton").GetComponent<Button>();
            var close = root.Find("Log View/Close Log").GetComponent<Button>();
            Assert.That(controller.Hud.LogOpen, Is.False, "Logs must not increase the default screen's information load.");
            open.onClick.Invoke();
            Assert.That(controller.Hud.LogOpen, Is.True);
            close.onClick.Invoke();
            Assert.That(controller.Hud.LogOpen, Is.False);
            controller.QueueLane(0);
            controller.CommitTurn();
            yield return WaitUntilPlanningOrFinished(controller);
            Assert.That(controller.Hud.LogCount, Is.EqualTo(2), "The log records one paired row per resolved slot.");
            open.onClick.Invoke();
            Assert.That(controller.Hud.LogOpen, Is.True);
            controller.RestartMatch();
            Assert.That(controller.Hud.LogOpen, Is.False);
            Assert.That(controller.Hud.LogCount, Is.Zero);
        }

        private static DuelPrototypeController FindPrototype()
        {
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            return controller;
        }

        private static IEnumerator WaitUntilPlanningOrFinished(DuelPrototypeController controller)
        {
            float deadline = Time.realtimeSinceStartup + 25f;
            while (controller.IsResolving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsResolving, Is.False, "The full queue must finish rather than stall.");
        }

        private static IEnumerator CaptureFrame(string fileName)
        {
            // Graphics-enabled Editor runs provide visual evidence. Headless test runs skip capture.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            string path = Path.Combine(Application.dataPath, "../Logs", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
        }
    }
}
