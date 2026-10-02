using System;
using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BattleResultMissionFlowPlayModeTests : InputTestFixture
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
                Assert.That(controller.SelectCurriculumNode("horizontal-cut"), Is.True);
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
                Assert.That(controller.Result.CompletedCurriculumNode.Id, Is.EqualTo("horizontal-cut"));
                Assert.That(controller.Result.ActiveCurriculumNode, Is.Null);
                Assert.That(Label(controller.ResultHud.Root, "Result Curriculum").text, Is.EqualTo("가로베기 완료"));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(60), "Reward is paid before the result is shown.");
                Assert.That(controller.Campaign.ClearedStageCount, Is.EqualTo(1));
                Assert.That(controller.Campaign.Curriculum.IsCompleted("horizontal-cut"), Is.True,
                    "The curriculum counts the battle before the result is shown.");
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(10));

                Assert.That(controller.QueueLane(0), Is.False);
                Assert.That(controller.StartCampaignStage(2), Is.False);
                Assert.That(controller.SelectCurriculumNode("diagonal-cut"), Is.False, "The result blocks the curriculum.");
                Assert.That(controller.StartMission(), Is.False);
                scope.Advance(5f);
                Assert.That(controller.Campaign.Currency, Is.EqualTo(60), "Result frames cannot pay a second reward.");
                Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.EqualTo(1));
                Assert.That(controller.Campaign.Curriculum.Active, Is.Null);
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(10), "…nor grant a skill twice.");

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
                Assert.That(controller.Result.CompletedCurriculumNode, Is.Null);
                Assert.That(Label(controller.ResultHud.Root, "Result Curriculum").text, Is.EqualTo("진행 없음"),
                    "Without a chosen node the battle reports no curriculum progress.");
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
        public IEnumerator NewGame_BriefsFirstMission_ThenIntroGuidedQOnlyDuelOutroAndResult()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.StartNewGame();
                string campaignBefore = CampaignFingerprint(controller.Campaign);
                Assert.That(controller.IsInBriefing, Is.True, "A new game opens on the first mission briefing.");
                Assert.That(controller.IsInLobby, Is.False);
                Assert.That(controller.IsMission, Is.False);
                Assert.That(controller.BriefingHud.IsVisible, Is.True);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(1));
                Assert.That(controller.LobbyHud.IsVisible, Is.False);
                Assert.That(controller.Hud.Root.activeSelf, Is.False);
                Assert.That(controller.Prologue.ClearedCount, Is.Zero);
                Assert.That(Label(controller.BriefingHud.Root, "Briefing Title").text, Is.EqualTo("처음 쥔 검"));
                Assert.That(Named(controller.BriefingHud.Root, "Mission Start").GetComponent<Button>(), Is.Not.Null);
                Assert.That(controller.BriefingHud.BackButton.gameObject.activeSelf, Is.False,
                    "A 서막 briefing has no way back to the lobby.");
                Assert.That(controller.LeaveBriefing(), Is.False);
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.StartCampaignStage(1), Is.False, "Stages stay closed until the arc is over.");
                Assert.That(controller.SelectCurriculumNode("horizontal-cut"), Is.False,
                    "The curriculum stays closed until the arc is over.");
                Assert.That(controller.CanChoose, Is.False);

                Assert.That(controller.StartMission(), Is.True);
                Assert.That(controller.IsShowingDialogue, Is.True, "Each mission opens with its intro dialogue.");
                Assert.That(controller.CurrentDialogueLine.Text, Is.EqualTo("테스트"));
                Assert.That(controller.IsMission, Is.False, "The duel waits for the intro.");
                Assert.That(controller.StartMission(), Is.False);
                Assert.That(controller.ContinueDialogue(), Is.False);
                Assert.That(controller.IsShowingDialogue, Is.False);
                Assert.That(controller.IsMission, Is.True);
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(1));
                Assert.That(controller.BriefingHud.IsVisible, Is.False);
                Assert.That(controller.CoachHud.IsVisible, Is.True, "The coach carries the old tutorial's guidance.");
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby), "Missions never start a campaign stage.");
                Assert.That(controller.Session.Enemy.MaxHealth, Is.EqualTo(24));
                Assert.That(controller.ArenaView.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy),
                    "The first mission strikes the straw dummy.");
                Assert.That(controller.Session.GetLane(1), Is.Empty);
                Assert.That(controller.Session.GetLane(2), Is.Empty);
                Assert.That(Named(controller.Hud.Root, "Current Q").gameObject.activeSelf, Is.True);
                Assert.That(Named(controller.Hud.Root, "Current W").gameObject.activeSelf, Is.False, "Only the Q lane is open.");
                Assert.That(Named(controller.Hud.Root, "Current E").gameObject.activeSelf, Is.False);
                Assert.That(Label(controller.Hud.Root, "Stage Label").text, Is.EqualTo("임무 01 / 04  ·  처음 쥔 검"));

                scope.Advance(100f);
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(10f), "The opening missions have no planning timer.");
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.False, "The opening beat cannot commit.");
                Assert.That(controller.QueueLane(0), Is.False, "The opening beat is read first.");
                Assert.That(controller.QueueBreath(), Is.False, "Breathing stays locked in the arc.");
                Assert.That(controller.CycleLanes(), Is.False, "넘기기 opens with this mission's win.");
                Assert.That(controller.AdvanceGuide(), Is.True);
                Assert.That(controller.Guide.ExpectedLane, Is.Zero);
                Assert.That(controller.QueueLane(1), Is.False);
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.Session.PlayerQueue[0].Id, Is.EqualTo(LegacySkillDefinitions.Skill(1).Id));
                Assert.That(controller.Session.PlayerQueue[1].Id, Is.EqualTo(LegacySkillDefinitions.Skill(2).Id));
                Assert.That(controller.Guide.Kind, Is.EqualTo(MissionGuideStepKind.Commit));
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.True);

                for (int turn = 0; turn < 4; turn++)
                {
                    scope.AdvanceUntilSettled();
                    if (controller.IsShowingDialogue || controller.IsShowingResult) break;
                    Assert.That(controller.Guide.IsFree, Is.True, "The first turn hands the dummy over to free play.");
                    bool queued = false;
                    while (controller.QueueLane(0)) queued = true;
                    Assert.That(queued, Is.True);
                    controller.CommitTurn();
                }
                Assert.That(controller.IsShowingDialogue, Is.True, "A mission victory plays its outro before the result.");
                Assert.That(controller.CurrentDialogueLine.Text, Is.EqualTo("테스트"));
                Assert.That(controller.IsShowingResult, Is.False);
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(1));
                Assert.That(controller.Session.Player.Health, Is.EqualTo(100), "The dummy never hits back.");
                Assert.That(controller.ContinueDialogue(), Is.False);
                Assert.That(controller.IsShowingResult, Is.True);
                Assert.That(controller.Result.IsMission, Is.True);
                Assert.That(controller.Result.Victory, Is.True);
                Assert.That(controller.Result.StageNumber, Is.EqualTo(1));
                Assert.That(controller.Result.Reward, Is.Zero);
                Assert.That(controller.Result.CanAdvance, Is.True);
                Assert.That(controller.Result.CompletedCurriculumNode, Is.Null);
                Assert.That(Label(controller.ResultHud.Root, "Result Notice").text,
                    Is.EqualTo("넘기기(Shift)가 열렸습니다.\n다음 임무가 열렸습니다."), "The first win announces 넘기기.");
                Assert.That(Label(controller.ResultHud.Root, "Result Curriculum").text, Is.EqualTo("깨어남 이후"),
                    "Missions never count toward the curriculum.");
                Assert.That(CampaignFingerprint(controller.Campaign), Is.EqualTo(campaignBefore));

                Assert.That(controller.RetryBattleResult(), Is.True);
                Assert.That(controller.IsShowingDialogue, Is.False, "A retry skips the intro.");
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(1));
                Assert.That(controller.Guide.StepIndex, Is.Zero);
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(1));

                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.escapeKey);
                yield return null;
                yield return null;
                Assert.That(controller.IsInBriefing, Is.True, "Escape leaves a mission for the briefing, not the lobby.");
                Assert.That(controller.IsMission, Is.False);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(2));
                Assert.That(controller.CoachHud.IsVisible, Is.False);
                Assert.That(controller.Hud.Root.activeSelf, Is.False);
                Release(keyboard.escapeKey);
                yield return null;
                Press(keyboard.enterKey);
                yield return null;
                Assert.That(controller.IsShowingDialogue, Is.True, "Enter on the briefing starts the mission intro.");
                Release(keyboard.enterKey);
                yield return null;
                Press(keyboard.escapeKey);
                yield return null;
                Assert.That(controller.IsShowingDialogue, Is.False);
                Assert.That(controller.IsMission, Is.True, "Skipping the intro still starts the mission.");
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(2));
                Assert.That(controller.Session.Enemy.MaxHealth, Is.EqualTo(36));
                Assert.That(controller.ArenaView.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student),
                    "Later missions fight the student again.");
                Release(keyboard.escapeKey);
                yield return null;
                Assert.That(controller.IsMission, Is.True, "The skip key must not also abandon the mission.");
                controller.enabled = false;

                scope.SetGuide(null);
                scope.LoseToResult();
                Assert.That(controller.Result.IsMission, Is.True);
                Assert.That(controller.Result.Victory, Is.False);
                Assert.That(controller.Result.CanAdvance, Is.False);
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(1));
                Assert.That(controller.DismissBattleResult(), Is.True);
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(2));
                Assert.That(CampaignFingerprint(controller.Campaign), Is.EqualTo(campaignBefore));
            }
        }

        [UnityTest]
        public IEnumerator LastMission_RunsItsTimerAfterTheOpeningBeat_AndItsVictoryOpensTheLobby()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.StartNewGame();
                Assert.That(controller.StartMission(), Is.True);
                Assert.That(controller.CloseDialogue(), Is.True);
                Assert.That(controller.IsMission, Is.False, "Closing the dialogue for cleanup never starts the duel.");
                Assert.That(controller.IsInBriefing, Is.True);

                for (int number = 1; number < PrologueMissions.Count; number++)
                    Assert.That(controller.Prologue.TryComplete(number, DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(controller.StartMission(), Is.True);
                Assert.That(controller.ContinueDialogue(), Is.False);
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(PrologueMissions.Count));
                Assert.That(controller.Session.GetLane(0).Count, Is.EqualTo(3));
                scope.Advance(1f);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(10f), "A coached beat holds the clock.");
                Assert.That(controller.AdvanceGuide(), Is.True);
                Assert.That(controller.Guide.IsFree, Is.True);
                scope.Advance(1f);
                Assert.That(controller.TurnTimeRemaining, Is.LessThan(10f), "The last mission runs the planning timer.");

                scope.WinToSettled();
                Assert.That(controller.IsShowingDialogue, Is.True);
                Assert.That(controller.ContinueDialogue(), Is.False);
                Assert.That(controller.IsShowingResult, Is.True);
                Assert.That(controller.Prologue.IsArcComplete, Is.True, "The 서막 is over; the lobby missions wait for their stages.");
                Assert.That(controller.Result.CanAdvance, Is.True);
                Assert.That(Label(controller.ResultHud.Root, "Result Notice").text, Does.Contain("커리큘럼"),
                    "The last mission announces the loadout and curriculum.");
                Assert.That(FindButton(controller.ResultHud.Root, "Result Next Stage").GetComponentInChildren<Text>().text,
                    Is.EqualTo("여정 계속"));
                Assert.That(FindButton(controller.ResultHud.Root, "Result Lobby").gameObject.activeSelf, Is.False);

                Assert.That(controller.AdvanceFromBattleResult(), Is.True);
                Assert.That(controller.IsInLobby, Is.True, "The lobby opens once the arc is over.");
                Assert.That(controller.IsInBriefing, Is.False);
                Assert.That(controller.IsMission, Is.False);
                Assert.That(controller.LobbyHud.IsVisible, Is.True);
                Assert.That(controller.BriefingHud.IsVisible, Is.False);
                Assert.That(controller.StartMission(), Is.False);
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
                Assert.That(controller.IsNextMissionAvailable, Is.False, "The first lobby mission waits for stage 1.");
                Assert.That(controller.LobbyHud.NextMission.Number, Is.EqualTo(PrologueMissions.Count + 1));
                Assert.That(controller.OpenNextMission(), Is.False);
                Assert.That(controller.IsInLobby, Is.True);
            }
        }

        private static Transform Named(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate;
            Assert.Fail("Missing UI node: " + name);
            return null;
        }

        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();

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
                value.Append(";o").Append(owned.SkillId);
            foreach (string node in run.Curriculum.Completed)
                value.Append(";c").Append(node);
            value.Append(";a").Append(run.Curriculum.Active?.Id ?? "-").Append(':').Append(run.Curriculum.ActiveBattles);
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
                WinQueued();
                AdvanceUntilResult();
            }

            /// <summary>Wins at once; a mission then stops on its outro dialogue instead of the result.</summary>
            public void WinToSettled()
            {
                WinQueued();
                AdvanceUntilSettled();
                Assert.That(Controller.IsShowingDialogue || Controller.IsShowingResult, Is.True);
            }

            private void WinQueued()
            {
                var zero = new LegacySkill(100, "Test", 1, 0, 0, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, "", iconId: 1);
                InstallDuel(new LegacyQueuedDuel(100, 50, 1, 0,
                    LegacyInitialSkills.All, new[] { zero }, new[] { 1 }, 3));
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
            }

            /// <summary>Drops the coach so a fixture duel can commit freely.</summary>
            public void SetGuide(MissionGuide guide)
                => typeof(DuelPrototypeController).GetField("guide", PrivateInstance).SetValue(Controller, guide);

            public void LoseToResult()
            {
                InstallDuel(new LegacyQueuedDuel(1, 0, 80, 15,
                    LegacyInitialSkills.All, new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 4));
                Controller.CommitTurn();
                AdvanceUntilResult();
            }

            /// <summary>Advances until planning, a result, or a dialogue that holds the flow.</summary>
            public void AdvanceUntilSettled()
            {
                int frames = 0;
                while (!Controller.IsShowingResult && !Controller.IsShowingDialogue && !Controller.CanChoose &&
                    frames++ < 2000) Advance(.025f);
                Assert.That(Controller.IsShowingResult || Controller.IsShowingDialogue || Controller.CanChoose, Is.True,
                    "The actual presentation must settle into planning, a dialogue or a result.");
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
