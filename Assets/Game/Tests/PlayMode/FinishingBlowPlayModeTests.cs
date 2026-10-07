using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The finishing blow in a stage battle (a 전투, with its bullet time) through the real controller: a longer
    /// slow motion than the decisive close-up's, letterbox bars sliding in, a freeze frame of its length, then the
    /// result with the bars and the freeze gone; and nothing left behind when the battle is left in the middle of it.</summary>
    public sealed class FinishingBlowPlayModeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private const float Frame = .02f;

        [UnityTest]
        public IEnumerator TheFinishingBlow_SlowsTheBattle_BringsTheBarsIn_Freezes_ThenTheResultComesClear()
        {
            yield return null;
            using (var scope = new BattleScope())
            {
                DuelPrototypeController controller = scope.Controller;
                DuelPresentationSettings settings = controller.PresentationSettings;
                Assume.That(settings.FinishingSlowMotionSeconds, Is.GreaterThan(.5f));
                Assume.That(settings.FinishingFreezeSeconds, Is.GreaterThan(.1f));
                Assume.That(settings.FinishingSlowMotionScale, Is.LessThan(LegacyArenaView.DecisiveSlowMotionScale));
                LegacyArenaView arena = controller.ArenaView;
                DuelFinale finale = controller.Finale;
                scope.StartOneBlowStage();
                Assert.That(controller.Encounter, Is.EqualTo(EncounterKind.Battle), "A stage is a 전투, bullet time and all.");
                scope.AdvanceUntil(() => finale.IsRunning, "The strike ends the duel.");
                Assert.That(controller.Session.IsFinished && controller.Outcome == DuelMatchOutcome.PlayerVictory, Is.True);
                Assert.That(finale.Timeline.Current, Is.EqualTo(LegacyFinishingBlow.Stage.SlowMotion));
                Assert.That(arena.IsFinishingFocus, Is.True, "The close-up holds on the one who fell…");
                Assert.That(controller.IsShowingResult, Is.False, "…and the result waits.");
                Assert.That(finale.Letterbox.IsVisible, Is.False, "The bars start out of sight.");

                // The hit's own stop comes first; the slow motion counts from its end.
                float hitStop = controller.HitStopTimeRemaining;
                if (hitStop > 0f)
                {
                    scope.Advance(hitStop * .5f);
                    Assert.That(finale.Timeline.StageElapsed, Is.Zero, "The hit stop is not part of the slow motion.");
                    scope.Advance(hitStop * .5f);
                }
                FieldInfo phaseTime = typeof(DuelPrototypeController).GetField("phaseTime", PrivateInstance);
                Assert.That(phaseTime, Is.Not.Null);
                float clockBefore = (float)phaseTime.GetValue(controller);
                for (int frame = 0; frame < 10; frame++) scope.Advance(Frame);
                float clockAfter = (float)phaseTime.GetValue(controller);
                Assert.That(clockAfter - clockBefore, Is.EqualTo(10 * Frame * settings.FinishingSlowMotionScale).Within(1e-4f),
                    "The battle clock crawls at the finishing blow's scale, slower than an ordinary decisive close-up.");
                Assert.That(finale.Letterbox.Amount, Is.GreaterThan(0f), "The bars slide in.");
                Assert.That(arena.BulletTimeAmount, Is.Zero, "No bullet time while it plays.");
                Assert.That(controller.IsResolving, Is.True);

                scope.AdvanceUntil(() => finale.IsFrozen, "The slow motion runs its length.");
                Assert.That(arena.IsFinishingFocus, Is.True, "The close-up is still on when the picture stops.");
                Assert.That(finale.Letterbox.Amount, Is.EqualTo(1f).Within(1e-3f));
                Transform player = arena.PlayerRenderer.transform, enemy = arena.EnemyRenderer.transform;
                Transform camera = arena.ArenaCamera.transform;
                Vector3 playerAt = player.localPosition, enemyAt = enemy.localPosition, cameraAt = camera.localPosition;
                Quaternion cameraRoll = camera.localRotation;
                float cameraSize = arena.ArenaCamera.orthographicSize;
                Sprite playerSprite = arena.PlayerRenderer.sprite, enemySprite = arena.EnemyRenderer.sprite;
                float frozenClock = (float)phaseTime.GetValue(controller);
                int frozenFrames = 0;
                while (!controller.IsShowingResult && frozenFrames < 1000)
                {
                    Assert.That(player.localPosition, Is.EqualTo(playerAt), "Nothing moves in the freeze frame…");
                    Assert.That(enemy.localPosition, Is.EqualTo(enemyAt));
                    Assert.That(camera.localPosition, Is.EqualTo(cameraAt), "…not even the camera…");
                    Assert.That(Quaternion.Angle(camera.localRotation, cameraRoll), Is.Zero);
                    Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(cameraSize));
                    Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerSprite));
                    Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemySprite));
                    Assert.That((float)phaseTime.GetValue(controller), Is.EqualTo(frozenClock), "…or the battle's clock.");
                    scope.Advance(Frame);
                    frozenFrames++;
                }
                Assert.That(controller.IsShowingResult, Is.True, "Then the result.");
                Assert.That(frozenFrames * Frame, Is.EqualTo(settings.FinishingFreezeSeconds).Within(Frame + 1e-4f),
                    "The freeze lasts its set time.");
                Assert.That(controller.Result.Victory, Is.True);
                Assert.That(finale.Letterbox.IsVisible, Is.False, "The bars are gone before the result shows…");
                Assert.That(finale.IsRunning || finale.IsComplete, Is.False, "…and so is the freeze.");
                Assert.That(arena.FlashbackAmount, Is.Zero, "A stage's finishing blow never turns grey.");

                Assert.That(controller.RetryBattleResult(), Is.True);
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(arena.IsFinishingFocus || arena.IsFatalFocus, Is.False);
                Assert.That(finale.Letterbox.IsVisible, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator LeavingInTheMiddleOfIt_LeavesNoBarsNoFreezeAndNoSlowMotion()
        {
            yield return null;
            using (var scope = new BattleScope())
            {
                DuelPrototypeController controller = scope.Controller;
                DuelFinale finale = controller.Finale;
                Assume.That(controller.PresentationSettings.FinishingFreezeSeconds, Is.GreaterThan(0f));

                // Escape in the slow motion skips to the result: a decided battle is never abandoned.
                scope.StartOneBlowStage();
                Assert.That(controller.SkipFinale(), Is.False, "Nothing to skip before the duel is decided.");
                scope.AdvanceUntil(() => finale.IsRunning, "The strike ends the duel.");
                Assert.That(controller.SkipFinale(), Is.True);
                Assert.That(controller.IsShowingResult && controller.Result.Victory, Is.True);
                AssertNothingLeft(controller, false);
                Assert.That(controller.SkipFinale(), Is.False);

                // A restart in the slow motion.
                scope.StartOneBlowStage();
                scope.AdvanceUntil(() => finale.IsRunning, "The strike ends the duel.");
                for (int frame = 0; frame < 10; frame++) scope.Advance(Frame);
                Assert.That(finale.Letterbox.IsVisible, Is.True);
                controller.RestartMatch();
                AssertNothingLeft(controller);
                Assert.That(controller.CanChoose, Is.True, "The new duel plans at once…");
                float planning = controller.TurnTimeRemaining;
                scope.Advance(.1f);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(planning - .1f).Within(1e-4f), "…on a clock at its own pace.");

                // The lobby (Escape) in the freeze frame.
                scope.StartOneBlowStage();
                scope.AdvanceUntil(() => finale.IsFrozen, "The slow motion runs its length.");
                controller.ReturnToLobby();
                Assert.That(controller.IsInLobby, Is.True);
                AssertNothingLeft(controller);
                Assert.That(controller.Result, Is.Null, "No result comes later from the abandoned battle.");
                scope.Advance(Frame);
                Assert.That(controller.IsShowingResult, Is.False);

                // The title in the freeze frame.
                scope.StartOneBlowStage();
                scope.AdvanceUntil(() => finale.IsFrozen, "The slow motion runs its length.");
                controller.ShowTitle();
                Assert.That(controller.IsInTitle, Is.True);
                AssertNothingLeft(controller);
            }
        }

        /// <param name="arenaReset">The battle was left (the arena restaged), not handed over to its result, which shows
        /// over the arena as the blow left it.</param>
        private static void AssertNothingLeft(DuelPrototypeController controller, bool arenaReset = true)
        {
            DuelFinale finale = controller.Finale;
            Assert.That(finale.Letterbox.IsVisible || finale.Letterbox.Root.activeSelf, Is.False, "No bars stay up…");
            Assert.That(finale.IsRunning || finale.IsFrozen || finale.IsComplete, Is.False, "…no freeze…");
            Assert.That(finale.CombatSpeed, Is.EqualTo(1f), "…no slow motion…");
            if (arenaReset)
                Assert.That(controller.ArenaView.IsFinishingFocus || controller.ArenaView.IsFatalFocus, Is.False, "…no close-up…");
            Assert.That(controller.ArenaView.FlashbackAmount, Is.Zero, "…and no grey.");
            Assert.That(controller.Hud.Fade, Is.EqualTo(1f), "The duel HUD is never left faded.");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Global time is never touched.");
        }

        /// <summary>The controller driven frame by frame on a stage battle whose single strike ends it.</summary>
        private sealed class BattleScope : IDisposable
        {
            private readonly bool originalEnabled;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public BattleScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                Controller.RestartJourney();
            }

            public void Advance(float delta) => advance(delta, null);

            public void AdvanceUntil(Func<bool> condition, string message)
            {
                int frames = 0;
                while (!condition() && frames++ < 2000) Advance(Frame);
                Assert.That(condition(), Is.True, message);
            }

            /// <summary>Stage 1 with a fixture duel: a 30-power strike against a 10-health enemy without resistance, who
            /// only breathes; queued and committed.</summary>
            public void StartOneBlowStage()
            {
                Controller.RestartMatch();
                var strike = new LegacySkill(210, "Test Finisher", 1, 30, 30, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0,
                    "", iconId: 1);
                typeof(DuelPrototypeController).GetField("session", PrivateInstance).SetValue(Controller,
                    new LegacyQueuedDuel(100, 50, 10, 0, new[] { strike }, new[] { LegacyCommonActions.Breathe }, new[] { 1 }, 3));
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                Assert.That(Controller.IsResolving, Is.True);
            }

            public void Dispose()
            {
                Controller.RestartJourney();
                Controller.enabled = originalEnabled;
            }
        }
    }
}
