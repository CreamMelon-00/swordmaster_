using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BattlePausePlayModeTests : InputTestFixture
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator Escape_PausesThePlanningClock_AndTheNextEscapeContinues()
        {
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using (var scope = new PauseScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartStage();
                float remaining = controller.TurnTimeRemaining;

                Press(keyboard.escapeKey);
                yield return null;
                scope.Advance(.02f, keyboard);
                Assert.That(controller.IsPaused, Is.True);
                Assert.That(controller.PauseHud.IsVisible, Is.True);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(remaining));
                AssertMenu(controller.PauseHud);

                Release(keyboard.escapeKey);
                yield return null;
                scope.Advance(3f, keyboard);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(remaining),
                    "A long real-time frame must not run the planning deadline under the menu.");
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(1));

                Press(keyboard.escapeKey);
                yield return null;
                scope.Advance(.02f, keyboard);
                Assert.That(controller.IsPaused, Is.False);
                Assert.That(controller.PauseHud.IsVisible, Is.False);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(remaining),
                    "The key used to close the menu must not also spend a battle frame.");
                Release(keyboard.escapeKey);
                yield return null;
                scope.Advance(.1f);
                Assert.That(controller.TurnTimeRemaining, Is.LessThan(remaining));
            }
        }

        [UnityTest]
        public IEnumerator AKeyPressedUnderTheMenu_DoesNotQueueAfterContinuing()
        {
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using (var scope = new PauseScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartStage();
                Assert.That(controller.PauseBattle(), Is.True);

                Press(keyboard.qKey);
                yield return null;
                scope.Advance(1f, keyboard);
                MenuButton(controller.PauseHud, "Pause Resume").onClick.Invoke();
                Assert.That(controller.Session.PlayerQueue, Is.Empty);

                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.Session.PlayerQueue, Is.Empty,
                    "Releasing a key first held under the pause menu must not choose a skill.");

                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(1),
                    "The next fresh tap should still choose the lane normally.");
            }
        }

        [UnityTest]
        public IEnumerator PausingDuringResolution_HoldsTheFightAtItsCurrentFrame()
        {
            yield return null;
            using (var scope = new PauseScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartStage();
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Advance(.02f);
                Assert.That(controller.IsResolving, Is.True);

                LegacyQueuedDuel duel = controller.Session;
                int round = duel.RoundNumber;
                int playerHealth = controller.PlayerHealth;
                int enemyHealth = controller.EnemyHealth;
                float phaseTime = scope.PhaseTime;
                float hitStop = controller.HitStopTimeRemaining;
                Vector3 playerPosition = controller.ArenaView.PlayerRenderer.transform.localPosition;
                Vector3 enemyPosition = controller.ArenaView.EnemyRenderer.transform.localPosition;
                var slot = duel.CurrentSlot;
                int hits = slot?.HitsResolved ?? 0;

                Assert.That(controller.PauseBattle(), Is.True);
                scope.Advance(5f);
                Assert.That(controller.Session, Is.SameAs(duel));
                Assert.That(duel.RoundNumber, Is.EqualTo(round));
                Assert.That(controller.PlayerHealth, Is.EqualTo(playerHealth));
                Assert.That(controller.EnemyHealth, Is.EqualTo(enemyHealth));
                Assert.That(scope.PhaseTime, Is.EqualTo(phaseTime));
                Assert.That(controller.HitStopTimeRemaining, Is.EqualTo(hitStop));
                Assert.That(controller.ArenaView.PlayerRenderer.transform.localPosition, Is.EqualTo(playerPosition));
                Assert.That(controller.ArenaView.EnemyRenderer.transform.localPosition, Is.EqualTo(enemyPosition));
                Assert.That(duel.CurrentSlot, Is.SameAs(slot));
                Assert.That(duel.CurrentSlot?.HitsResolved ?? 0, Is.EqualTo(hits));
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(controller.ResumeBattle(), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator Buttons_ContinueRetryAndAbandon_KeepTheCurrentStageAndItsProgress()
        {
            yield return null;
            using (var scope = new PauseScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartStage();
                Assert.That(controller.QueueLane(0), Is.True);
                LegacyQueuedDuel firstAttempt = controller.Session;
                int stage = controller.Campaign.StageNumber;

                Assert.That(controller.PauseBattle(), Is.True);
                MenuButton(controller.PauseHud, "Pause Resume").onClick.Invoke();
                Assert.That(controller.IsPaused, Is.False);
                Assert.That(controller.Session, Is.SameAs(firstAttempt));
                Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(1));

                Assert.That(controller.PauseBattle(), Is.True);
                MenuButton(controller.PauseHud, "Pause Retry").onClick.Invoke();
                Assert.That(controller.IsPaused, Is.False);
                Assert.That(controller.PauseHud.IsVisible, Is.False);
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Battle));
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(stage));
                Assert.That(controller.Session, Is.Not.SameAs(firstAttempt));
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(1));
                Assert.That(controller.Session.PlayerQueue.Count, Is.Zero);
                Assert.That(controller.CanChoose, Is.True);

                Assert.That(controller.PauseBattle(), Is.True);
                MenuButton(controller.PauseHud, "Pause Abandon").onClick.Invoke();
                Assert.That(controller.IsPaused, Is.False);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.Campaign.ClearedStageCount, Is.Zero);
                Assert.That(controller.PauseHud.IsVisible, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator RetryingAMission_ReopensThatMissionWithoutReturningToTheBriefing()
        {
            yield return null;
            using (var scope = new PauseScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.StartNewGame();
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.StartMission(), Is.True);
                Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.IsMission, Is.True);
                int mission = controller.ActiveMission.Number;
                LegacyQueuedDuel firstAttempt = controller.Session;

                Assert.That(controller.PauseBattle(), Is.True);
                Assert.That(controller.RetryPausedBattle(), Is.True);
                Assert.That(controller.IsPaused, Is.False);
                Assert.That(controller.IsMission, Is.True);
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(mission));
                Assert.That(controller.Session, Is.Not.SameAs(firstAttempt));
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(1));
                Assert.That(controller.Guide.StepIndex, Is.Zero);
                Assert.That(controller.IsInBriefing, Is.False);

                Assert.That(controller.PauseBattle(), Is.True);
                Assert.That(controller.AbandonPausedBattle(), Is.True);
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.IsPaused, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator RetryingTraining_RestartsTheSameDummyWithoutAdvancingItsHealth()
        {
            yield return null;
            using (var scope = new PauseScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartStage();
                Assert.That(controller.Campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                controller.ReturnToLobby();
                Assert.That(controller.StartCampaignStage(2), Is.True);
                Assert.That(controller.Campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                controller.ReturnToLobby();
                Assert.That(controller.Campaign.IsTrainingUnlocked, Is.True);
                Assert.That(controller.StartTraining(), Is.True);

                LegacyQueuedDuel firstAttempt = controller.Session;
                int dummyHealth = controller.Campaign.TrainingDummyHealth;
                Assert.That(controller.IsTrainingBattle, Is.True);
                Assert.That(controller.EnemyHealth, Is.EqualTo(dummyHealth));
                Assert.That(controller.PauseBattle(), Is.True);
                Assert.That(controller.RetryPausedBattle(), Is.True);

                Assert.That(controller.IsPaused, Is.False);
                Assert.That(controller.IsTrainingBattle, Is.True);
                Assert.That(controller.Session, Is.Not.SameAs(firstAttempt));
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(1));
                Assert.That(controller.Session.RoundLimit, Is.EqualTo(5));
                Assert.That(controller.EnemyHealth, Is.EqualTo(dummyHealth));
                Assert.That(controller.Campaign.TrainingVictoryCount, Is.Zero);
                Assert.That(controller.Campaign.TrainingDummyHealth, Is.EqualTo(dummyHealth));
            }
        }

        private static void AssertMenu(DuelPauseHud pauseHud)
        {
            GameObject root = pauseHud.Root;
            Assert.That(root.activeInHierarchy, Is.True);
            Image dim = root.transform.Find("Pause Backdrop")?.GetComponent<Image>();
            Assert.That(dim, Is.Not.Null);
            Assert.That(dim.color.a, Is.GreaterThan(.5f));
            Assert.That(dim.color.grayscale, Is.LessThan(.4f));
            Assert.That(MenuButton(pauseHud, "Pause Resume").GetComponentInChildren<Text>().text,
                Is.EqualTo("계속하기"));
            Assert.That(MenuButton(pauseHud, "Pause Retry").GetComponentInChildren<Text>().text,
                Is.EqualTo("재도전"));
            Assert.That(MenuButton(pauseHud, "Pause Abandon").GetComponentInChildren<Text>().text,
                Is.EqualTo("포기"));
            Assert.That(root.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(3));
        }

        private static Button MenuButton(DuelPauseHud pauseHud, string name)
        {
            foreach (Button button in pauseHud.Root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            Assert.Fail("Pause button missing: " + name);
            return null;
        }

        private sealed class PauseScope : IDisposable
        {
            private readonly bool originalEnabled;
            private readonly bool originalStartCards;
            private readonly bool originalStoryProgression;
            private readonly bool originalAutoSave;
            private readonly PropertyInfo storyProgression;
            private readonly PropertyInfo autoSave;
            private readonly Action<float, Keyboard> advance;

            public DuelPrototypeController Controller { get; }
            public float PhaseTime => (float)typeof(DuelPrototypeController)
                .GetField("phaseTime", PrivateInstance).GetValue(Controller);

            public PauseScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                originalStartCards = Controller.StartCardsEnabled;
                storyProgression = typeof(DuelPrototypeController).GetProperty("StoryProgressionEnabled",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                autoSave = typeof(DuelPrototypeController).GetProperty("AutoSaveEnabled",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(storyProgression, Is.Not.Null);
                Assert.That(autoSave, Is.Not.Null);
                originalStoryProgression = Controller.StoryProgressionEnabled;
                originalAutoSave = Controller.AutoSaveEnabled;
                Controller.enabled = false;
                Controller.StartCardsEnabled = false;
                storyProgression.SetValue(Controller, false);
                autoSave.SetValue(Controller, false);
                MethodInfo method = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
                Assert.That(method, Is.Not.Null);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    method);
                Controller.RestartJourney();
            }

            public void StartStage()
            {
                if (!Controller.IsInLobby) Controller.ReturnToLobby();
                Assert.That(Controller.StartCampaignStage(1), Is.True);
                Assert.That(Controller.CanChoose, Is.True);
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            public void Dispose()
            {
                if (Controller.IsPaused) Controller.ResumeBattle();
                Controller.RestartJourney();
                Controller.StartCardsEnabled = originalStartCards;
                storyProgression.SetValue(Controller, originalStoryProgression);
                typeof(DuelPrototypeController).GetMethod("SyncStoryProgression", PrivateInstance)
                    .Invoke(Controller, null);
                autoSave.SetValue(Controller, originalAutoSave);
                Controller.enabled = originalEnabled;
            }
        }
    }
}
