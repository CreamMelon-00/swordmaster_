using System;
using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BattleResultTutorialFlowPlayModeTests : InputTestFixture
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTest]
        public IEnumerator CampaignVictory_ShowsPaidResult_BlocksActions_AndAdvancesOnlyToUnlockedStage()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.Result, Is.Null);
                Assert.That(controller.ResultHud.IsVisible, Is.False);
                scope.StartSelectedStage();
                scope.WinToResult();

                Assert.That(controller.IsShowingResult, Is.True);
                Assert.That(controller.ResultHud.IsVisible, Is.True);
                Assert.That(controller.LobbyHud.IsVisible, Is.False);
                Assert.That(controller.IsInLobby, Is.False);
                Assert.That(controller.Result, Is.Not.Null);
                Assert.That(controller.Result.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
                Assert.That(controller.Result.StageNumber, Is.EqualTo(1));
                Assert.That(controller.Result.Reward, Is.EqualTo(60));
                Assert.That(controller.Result.Currency, Is.EqualTo(60));
                Assert.That(controller.Result.FirstClear, Is.True);
                Assert.That(controller.Result.UnlockedStageNumber, Is.EqualTo(2));
                Assert.That(controller.Result.CanAdvance, Is.True);
                Assert.That(controller.Campaign.Currency, Is.EqualTo(60), "Reward is paid before the result is shown.");
                Assert.That(controller.Campaign.ClearedStageCount, Is.EqualTo(1));

                Assert.That(controller.QueueLane(0), Is.False);
                Assert.That(controller.StartCampaignStage(2), Is.False);
                Assert.That(controller.AcquireSkill(14), Is.False);
                Assert.That(controller.StartTutorial(), Is.False);
                scope.Advance(5f);
                Assert.That(controller.Campaign.Currency, Is.EqualTo(60), "Result frames cannot pay a second reward.");

                Assert.That(controller.AdvanceFromBattleResult(), Is.True);
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(2));
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Battle));
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.IsShowingResult, Is.False);
                Assert.That(controller.Result, Is.Null);
                Assert.That(controller.ResultHud.IsVisible, Is.False);
                Assert.That(controller.AdvanceFromBattleResult(), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator DefeatResult_CannotAdvance_RetriesCurrentStage_AndHeldEnterOnlyDismissesOnce()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartSelectedStage();
                scope.LoseToResult();

                Assert.That(controller.Result.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
                Assert.That(controller.Result.Reward, Is.Zero);
                Assert.That(controller.Result.CanAdvance, Is.False);
                Assert.That(controller.Campaign.Currency, Is.Zero);
                Assert.That(controller.AdvanceFromBattleResult(), Is.False);
                Assert.That(controller.RetryBattleResult(), Is.True);
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(1));
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Battle));
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.IsShowingResult, Is.False);

                scope.LoseToResult();
                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.enterKey);
                yield return null;
                yield return null;
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.IsShowingResult, Is.False);
                Assert.That(controller.ResultHud.IsVisible, Is.False);
                Assert.That(controller.DismissBattleResult(), Is.False, "A dismissed result cannot be consumed twice.");

                yield return null;
                Assert.That(controller.IsInLobby, Is.True,
                    "Held Enter from the result must not start the selected stage on the next frame.");
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
                Release(keyboard.enterKey);
                yield return null;
                controller.enabled = false;
            }
        }

        [UnityTest]
        public IEnumerator Tutorial_GuidesRealTurn_PreservesCampaign_RetriesPractice_AndEscapeReturnsWithoutReward()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                DuelPrototypeController controller = scope.Controller;
                string campaignBefore = CampaignFingerprint(controller.Campaign);
                Assert.That(controller.Tutorial, Is.Null);
                Assert.That(controller.StartTutorial(), Is.True);
                Assert.That(controller.StartTutorial(), Is.False);
                Assert.That(controller.IsTutorial, Is.True);
                Assert.That(controller.IsInLobby, Is.False);
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
                Assert.That(controller.TutorialHud.IsVisible, Is.True);
                Assert.That(controller.Session.Enemy.MaxHealth, Is.EqualTo(32));
                Assert.That(controller.Session.Enemy.MaxResistance, Is.EqualTo(12));
                Assert.That(controller.StartCampaignStage(1), Is.False);
                Assert.That(controller.AcquireSkill(14), Is.False);
                Assert.That(controller.UnequipSkill(1), Is.False);

                scope.Advance(100f);
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(10f), "Tutorial planning has no time limit.");
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.False, "Welcome cannot commit a queue.");

                Assert.That(controller.AdvanceTutorial(), Is.True);
                Assert.That(controller.Tutorial.Step, Is.EqualTo(TutorialStep.QueueAttack));
                Assert.That(controller.AdvanceTutorial(), Is.False);
                Assert.That(controller.QueueLane(1), Is.False);
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.Tutorial.Step, Is.EqualTo(TutorialStep.QueueFollowup));
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.False);

                Assert.That(controller.QueueLane(2), Is.False);
                Assert.That(controller.QueueLane(1), Is.True);
                Assert.That(controller.Tutorial.Step, Is.EqualTo(TutorialStep.QueueGuard));
                Assert.That(controller.QueueLane(1), Is.False);
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.Tutorial.Step, Is.EqualTo(TutorialStep.InspectEnemy));
                Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(3));
                Assert.That(controller.Session.PlayerQueue[0].Id, Is.EqualTo(1));
                Assert.That(controller.Session.PlayerQueue[1].Id, Is.EqualTo(3));
                Assert.That(controller.Session.PlayerQueue[2].Id, Is.EqualTo(7));
                Assert.That(controller.Session.Act, Is.Zero);
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.False, "Inspecting the enemy is required before commit.");

                Button inspect = FindButton(controller.TutorialHud.Root, "Tutorial Inspect Enemy");
                Assert.That(inspect.gameObject.activeInHierarchy, Is.True);
                inspect.onClick.Invoke();
                Assert.That(controller.Tutorial.Step, Is.EqualTo(TutorialStep.CommitQueue));
                Assert.That(controller.Hud.Root.transform.Find("Enemy Skill Explain").gameObject.activeSelf, Is.True);
                Assert.That(controller.InspectTutorialEnemy(), Is.False);

                controller.CommitTurn();
                Assert.That(controller.Tutorial.Step, Is.EqualTo(TutorialStep.WatchClash));
                Assert.That(controller.IsResolving, Is.True);
                scope.AdvanceUntilPlanningOrResult();
                Assert.That(controller.IsShowingResult, Is.False);
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
                Assert.That(controller.Session.Act, Is.EqualTo(6));
                Assert.That(controller.Tutorial.Step, Is.EqualTo(TutorialStep.TurnRecovery));
                Assert.That(controller.AdvanceTutorial(), Is.True);
                Assert.That(controller.Tutorial.Step, Is.EqualTo(TutorialStep.FreeBattle));

                Assert.That(controller.QueueLane(2), Is.True);
                Assert.That(controller.Session.PlayerQueue[0].Id, Is.EqualTo(5));
                Assert.That(controller.QueueLane(2), Is.True);
                Assert.That(controller.Session.PlayerQueue[1].Id, Is.EqualTo(6));
                Assert.That(controller.Session.PlayerQueue[1].Cost, Is.EqualTo(3));
                Assert.That(controller.Session.Act, Is.EqualTo(2));
                controller.CommitTurn();
                for (int turn = 0; turn < 3 && !controller.IsShowingResult; turn++)
                {
                    scope.AdvanceUntilPlanningOrResult();
                    if (controller.IsShowingResult) break;
                    bool queued = false;
                    while (controller.QueueLane(2)) queued = true;
                    while (controller.QueueLane(1)) queued = true;
                    while (controller.QueueLane(0)) queued = true;
                    Assert.That(queued, Is.True, "Free battle must retain at least one affordable action.");
                    controller.CommitTurn();
                }
                if (!controller.IsShowingResult) scope.AdvanceUntilPlanningOrResult();
                Assert.That(controller.IsShowingResult, Is.True, "The bounded tutorial fixture should reach a real result.");
                Assert.That(controller.Result.IsTutorial, Is.True);
                Assert.That(controller.Result.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
                Assert.That(controller.Result.Reward, Is.Zero);
                Assert.That(controller.Result.Currency, Is.Zero);
                Assert.That(controller.Result.CanAdvance, Is.False);
                Assert.That(CampaignFingerprint(controller.Campaign), Is.EqualTo(campaignBefore));

                Assert.That(controller.RetryBattleResult(), Is.True);
                Assert.That(controller.IsTutorial, Is.True);
                Assert.That(controller.Tutorial.Step, Is.EqualTo(TutorialStep.Welcome));
                Assert.That(controller.Session.Enemy.MaxHealth, Is.EqualTo(32));
                Assert.That(controller.Session.Enemy.MaxResistance, Is.EqualTo(12));
                Assert.That(CampaignFingerprint(controller.Campaign), Is.EqualTo(campaignBefore));

                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.escapeKey);
                yield return null;
                yield return null;
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.IsTutorial, Is.False);
                Assert.That(controller.IsShowingResult, Is.False);
                Assert.That(controller.Campaign.LastReward, Is.Zero);
                Assert.That(CampaignFingerprint(controller.Campaign), Is.EqualTo(campaignBefore));
                Release(keyboard.escapeKey);
                yield return null;
                controller.enabled = false;

                Assert.That(controller.StartTutorial(), Is.True);
                Assert.That(controller.AdvanceTutorial(), Is.True);
                controller.RestartJourney();
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.IsTutorial, Is.False);
                Assert.That(controller.Tutorial, Is.Null);
                Assert.That(controller.Result, Is.Null);
                Assert.That(controller.TutorialHud.IsVisible, Is.False);
                Assert.That(controller.ResultHud.IsVisible, Is.False);
            }
        }

        private static Button FindButton(GameObject root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            Assert.Fail("Missing button " + name + ".");
            return null;
        }

        private static string CampaignFingerprint(CampaignRun run)
        {
            var value = new StringBuilder();
            value.Append(run.Currency).Append('|').Append(run.ClearedStageCount).Append('|')
                .Append(run.HighestUnlockedStage).Append('|').Append(run.StageNumber).Append('|').Append(run.Phase);
            foreach (CampaignOwnedSkill owned in run.OwnedSkills)
                value.Append(";o").Append(owned.SkillId).Append(':').Append(owned.Level);
            for (int lane = 0; lane < 3; lane++)
                foreach (CampaignOwnedSkill owned in run.GetEquippedLane(lane))
                    value.Append(";e").Append(lane).Append(':').Append(owned.SkillId);
            return value.ToString();
        }

        private sealed class FlowScope : IDisposable
        {
            private readonly bool originalEnabled;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public FlowScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                Controller.RestartJourney();
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            public void StartSelectedStage()
            {
                Controller.LobbyHud.ShowTab(LobbyTab.Stages);
                Assert.That(Controller.LobbyHud.SelectStage(1), Is.True);
                Assert.That(Controller.StartCampaignStage(1), Is.True);
            }

            public void WinToResult()
            {
                var zero = new LegacySkill(100, "Test", 1, 0, 0, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, "", iconId: 1);
                InstallDuel(new LegacyQueuedDuel(100, 50, 1, 0,
                    LegacyInitialSkills.All, new[] { zero }, new[] { 1 }, 3));
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                AdvanceUntilResult();
            }

            public void LoseToResult()
            {
                InstallDuel(new LegacyQueuedDuel(1, 0, 80, 15,
                    LegacyInitialSkills.All, new[] { LegacyInitialSkills.All[0] }, new[] { 1 }, 4));
                Controller.CommitTurn();
                AdvanceUntilResult();
            }

            public void AdvanceUntilPlanningOrResult()
            {
                int frames = 0;
                while (!Controller.IsShowingResult && !Controller.CanChoose && frames++ < 2000) Advance(.025f);
                Assert.That(Controller.IsShowingResult || Controller.CanChoose, Is.True,
                    "The actual presentation must settle into planning or a result.");
            }

            private void AdvanceUntilResult()
            {
                int frames = 0;
                while (!Controller.IsShowingResult && frames++ < 2000) Advance(.025f);
                Assert.That(Controller.IsShowingResult, Is.True, "The actual presentation must reach its result HUD.");
                Assert.That(Controller.ResultHud.IsVisible, Is.True);
            }

            private void InstallDuel(LegacyQueuedDuel duel)
            {
                typeof(DuelPrototypeController).GetField("session", PrivateInstance).SetValue(Controller, duel);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
            }

            public void Dispose()
            {
                Controller.RestartJourney();
                Controller.enabled = originalEnabled;
            }
        }
    }
}
