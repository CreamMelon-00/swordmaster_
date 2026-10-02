using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The lobby missions after the 서막 open one combat feature each and hold back the stages after theirs.
    /// Only a title session (이어하기/새 게임) follows the story; direct API use keeps every feature and stage open.</summary>
    public sealed class MissionUnlockFlowPlayModeTests : InputTestFixture
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        /// <summary>The Q lane and 넘기기, which the 서막 opens.</summary>
        private const CombatFeature QAndCycle = CombatFeature.LaneQ | CombatFeature.Cycle;
        private const CombatFeature QAndE = QAndCycle | CombatFeature.LaneE;

        [UnityTest]
        public IEnumerator AfterTheArc_StagesFightWithOnlyQ_AndTheNextMissionWaitsForStageOne()
        {
            yield return null;
            using (var scope = new UnlockScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.ContinueFrom(PrologueMissions.Count, 0);
                Assert.That(controller.IsInLobby, Is.True, "Mission 5 waits for stage 1, so 이어하기 resumes in the lobby.");
                Assert.That(controller.IsInBriefing, Is.False);
                Assert.That(controller.Prologue.IsArcComplete, Is.True);
                Assert.That(controller.Campaign.Features, Is.EqualTo(QAndCycle), "Only the Q lane and 넘기기 are open after the 서막.");
                Assert.That(controller.Campaign.StageLimit, Is.EqualTo(1));
                Assert.That(controller.IsNextMissionAvailable, Is.False);
                Assert.That(controller.OpenNextMission(), Is.False);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.LobbyHud.NextMission.Number, Is.EqualTo(5));
                Assert.That(controller.LobbyHud.IsNextMissionAvailable, Is.False);
                Assert.That(controller.LobbyHud.CurrentTab, Is.EqualTo(LobbyTab.Home));
                GameObject lobby = controller.LobbyHud.Root;
                Assert.That(ActiveLabel(lobby, "Home Mission Title").text, Is.EqualTo("가르침  ·  임무 5  ·  기교 검술"));
                Assert.That(ActiveLabel(lobby, "Home Mission State").text, Does.Contain("스테이지 01을 클리어하면 열립니다"));
                Assert.That(ActiveButton(lobby, "Home Mission Open").interactable, Is.False, "The banner stays locked.");

                // Stage 1 fights with the Q lane only: the saved W/E skills, 숨고르기 and the steps stay out.
                Assert.That(controller.StartCampaignStage(1), Is.True);
                LegacyQueuedDuel duel = controller.Session;
                Assert.That(duel.Features, Is.EqualTo(QAndCycle));
                Assert.That(duel.GetLane(0).Count, Is.EqualTo(3));
                Assert.That(Named(controller.Hud.Root, "CycleButton").gameObject.activeSelf, Is.True, "넘기기 came with the 서막.");
                Assert.That(duel.GetLane(1), Is.Empty, "The W lane's equipped skills never enter the duel.");
                Assert.That(duel.GetLane(2), Is.Empty);
                Assert.That(Named(controller.Hud.Root, "Current Q").gameObject.activeSelf, Is.True);
                Assert.That(Named(controller.Hud.Root, "Current W").gameObject.activeSelf, Is.False);
                Assert.That(Named(controller.Hud.Root, "Current E").gameObject.activeSelf, Is.False);
                Assert.That(Named(controller.Hud.Root, "BreathButton").gameObject.activeSelf, Is.False,
                    "A closed 숨고르기 is not drawn in stages either.");
                Assert.That(controller.QueueBreath(), Is.False);
                Assert.That(controller.QueueLane(1), Is.False);
                Assert.That(controller.QueueLane(2), Is.False);
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1));
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.True);
                scope.AdvanceUntil(() => duel.CurrentSlot != null, "The committed turn must reach its first slot.");
                Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Resolving));
                Assert.That(controller.CanStep, Is.False, "No step is open yet.");
                Assert.That(controller.IsSkillWindup, Is.False, "Without an open step a slot has no step windup.");
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out _), Is.False);
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out _), Is.False);
                Assert.That(duel.StepAttemptsThisTurn, Is.Zero);
                controller.ReturnToLobby();
                Assert.That(controller.IsInLobby, Is.True);

                // Clearing stage 1 brings the mission; stage 2 waits for it.
                Assert.That(controller.StartCampaignStage(1), Is.True);
                scope.WinToSettled();
                Assert.That(controller.IsShowingResult, Is.True);
                Assert.That(controller.Result.IsMission, Is.False);
                Assert.That(controller.Result.Victory, Is.True);
                Assert.That(controller.Result.CanAdvance, Is.False, "Stage 2 waits for the mission, so no next-stage button.");
                Assert.That(FindButton(controller.ResultHud.Root, "Result Next Stage").gameObject.activeSelf, Is.False);
                Assert.That(Label(controller.ResultHud.Root, "Result Notice").text, Does.Contain("새 임무 '기교 검술' 도착"),
                    "The stage result announces the mission that has just arrived.");
                Assert.That(controller.DismissBattleResult(), Is.True);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.Campaign.IsStageCleared(1), Is.True);
                Assert.That(controller.Campaign.HighestUnlockedStage, Is.EqualTo(2));
                Assert.That(controller.IsNextMissionAvailable, Is.True);
                Assert.That(controller.LobbyHud.IsNextMissionAvailable, Is.True);
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                Assert.That(ActiveLabel(lobby, "Home Mission State").text, Is.EqualTo("새 임무가 도착했습니다."));
                Assert.That(ActiveButton(lobby, "Home Mission Open").interactable, Is.True);
                Assert.That(controller.Campaign.IsStageWaitingForMission(2), Is.True);
                Assert.That(controller.Campaign.CanStartStage(2), Is.False, "Stage 2 waits for the mission.");
                Assert.That(controller.StartCampaignStage(2), Is.False);
                Assert.That(controller.IsInLobby, Is.True);

                controller.LobbyHud.ShowTab(LobbyTab.Stages);
                Assert.That(ActiveLabel(lobby, "Stage State 1").text, Is.EqualTo("클리어"));
                Assert.That(ActiveLabel(lobby, "Stage State 2").text, Is.EqualTo("임무 필요"));
                Assert.That(ActiveLabel(lobby, "Stage State 3").text, Is.EqualTo("잠김"));
                Assert.That(controller.LobbyHud.SelectStage(2), Is.True);
                Assert.That(ActiveLabel(lobby, "Selected Stage State").text, Is.EqualTo("먼저 임무 '기교 검술'을(를) 완료하세요"));
                Assert.That(ActiveButton(lobby, "Start Selected Stage").interactable, Is.False);
                Button openMission = ActiveButton(lobby, "Stages Open Mission");
                Assert.That(openMission.interactable, Is.True);
                openMission.onClick.Invoke();
                Assert.That(controller.IsInBriefing, Is.True, "The Stages tab opens the waiting mission's briefing.");
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(5));
                Assert.That(Label(controller.BriefingHud.Root, "Briefing Chapter").text, Is.EqualTo("가르침  ·  임무 5 / 9"));
                Assert.That(controller.BriefingHud.BackButton.gameObject.activeSelf, Is.True);
                Assert.That(controller.LeaveBriefing(), Is.True);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.LeaveBriefing(), Is.False, "Only a briefing can be left.");
            }
        }

        [UnityTest]
        public IEnumerator LobbyMission_BriefsFromTheLobby_EscapeLeaves_AndItsWinOpensTheELaneForStages()
        {
            yield return null;
            using (var scope = new UnlockScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.ContinueFrom(PrologueMissions.Count, 1);
                Assert.That(controller.IsInBriefing, Is.True, "With stage 1 cleared, 이어하기 resumes at mission 5's briefing.");
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(5));
                Assert.That(Label(controller.BriefingHud.Root, "Briefing Chapter").text, Is.EqualTo("가르침  ·  임무 5 / 9"));
                Assert.That(Label(controller.BriefingHud.Root, "Briefing Title").text, Is.EqualTo("기교 검술"));
                Button back = controller.BriefingHud.BackButton;
                Assert.That(back, Is.Not.Null);
                Assert.That(back.name, Is.EqualTo("Mission Back"));
                Assert.That(back.gameObject.activeSelf, Is.True, "A lobby mission's briefing offers the way back.");
                Assert.That(back.GetComponentInChildren<Text>(true).text, Is.EqualTo("로비로  [Esc]"));
                Assert.That(controller.OpenNextMission(), Is.False, "Only the lobby opens a briefing.");

                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.escapeKey);
                yield return null;
                Release(keyboard.escapeKey);
                yield return null;
                controller.enabled = false;
                Assert.That(controller.IsInLobby, Is.True, "Escape leaves a lobby mission's briefing for the lobby.");
                Assert.That(controller.IsInBriefing, Is.False);
                Assert.That(controller.BriefingHud.IsVisible, Is.False);
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(PrologueMissions.Count));

                GameObject lobby = controller.LobbyHud.Root;
                ActiveButton(lobby, "Home Mission Open").onClick.Invoke();
                Assert.That(controller.IsInBriefing, Is.True, "The Home banner opens the briefing.");
                back.onClick.Invoke();
                Assert.That(controller.IsInLobby, Is.True, "…and the back button returns to the lobby.");
                Assert.That(controller.OpenNextMission(), Is.True);
                Assert.That(controller.IsInBriefing, Is.True);
                scope.StartBriefedMission();
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(5));
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby), "Missions never start a stage.");
                LegacyQueuedDuel duel = controller.Session;
                Assert.That(duel.Features, Is.EqualTo(QAndE));
                Assert.That(duel.GetLane(0).Count, Is.EqualTo(3));
                Assert.That(duel.GetLane(1), Is.Empty);
                Assert.That(duel.GetLane(2).Count, Is.EqualTo(3), "Mission 5 teaches the E lane.");
                Assert.That(Named(controller.Hud.Root, "Current W").gameObject.activeSelf, Is.False);
                Assert.That(Named(controller.Hud.Root, "Current E").gameObject.activeSelf, Is.True);
                Assert.That(Label(controller.Hud.Root, "Stage Label").text, Is.EqualTo("임무 05 / 09  ·  기교 검술"));
                Assert.That(Named(controller.Hud.Root, "BreathButton").gameObject.activeSelf, Is.False);
                Assert.That(controller.QueueBreath(), Is.False, "숨고르기 is still closed.");

                scope.SetGuide(null);
                scope.WinToSettled();
                Assert.That(controller.IsShowingDialogue, Is.True, "The outro plays before the result.");
                Assert.That(scope.Load().PrologueCleared, Is.EqualTo(5), "The first win is saved.");
                Assert.That(controller.ContinueDialogue(), Is.False);
                Assert.That(controller.IsShowingResult, Is.True);
                Assert.That(controller.Result.IsMission, Is.True);
                Assert.That(controller.Result.Victory, Is.True);
                Assert.That(controller.Result.StageNumber, Is.EqualTo(5));
                Assert.That(controller.Campaign.Features, Is.EqualTo(QAndE), "The first win opens the E lane for stages.");
                Assert.That(controller.Campaign.StageLimit, Is.EqualTo(2), "…and stage 2, where mission 6 waits.");
                Assert.That(controller.IsNextMissionAvailable, Is.False);
                Assert.That(Label(controller.ResultHud.Root, "Result Stage").text, Is.EqualTo("가르침  ·  임무 05  ·  기교 검술"));
                Assert.That(Label(controller.ResultHud.Root, "Result Notice").text,
                    Does.Contain(StoryMissions.Get(5).UnlockText).And.Contain("다음 스테이지를 깨면 다음 임무가 열립니다"));
                Button next = FindButton(controller.ResultHud.Root, "Result Next Stage");
                Assert.That(next.gameObject.activeSelf, Is.True);
                Assert.That(next.GetComponentInChildren<Text>().text, Is.EqualTo("여정 계속"));
                Assert.That(FindButton(controller.ResultHud.Root, "Result Lobby").gameObject.activeSelf, Is.False);
                next.onClick.Invoke();
                Assert.That(controller.IsInLobby, Is.True, "Mission 6 waits for stage 2, so 여정 계속 opens the lobby.");
                Assert.That(controller.IsInBriefing, Is.False);
                Assert.That(controller.LobbyHud.NextMission.Number, Is.EqualTo(6));
                Assert.That(controller.LobbyHud.IsNextMissionAvailable, Is.False);
                Assert.That(ActiveLabel(lobby, "Home Mission State").text, Does.Contain("스테이지 02을 클리어하면 열립니다"));

                controller.LobbyHud.ShowTab(LobbyTab.Loadout);
                Assert.That(ActiveLabel(lobby, "Loadout Counts").text, Is.EqualTo("Q 3/3  ·  W 잠김  ·  E 3/3"));
                for (int slot = 1; slot <= 3; slot++)
                {
                    Assert.That(SlotCost(lobby, "W", slot), Is.EqualTo("임무로 열림"), "W slot " + slot);
                    Assert.That(SlotCost(lobby, "Q", slot), Does.StartWith("ACT "), "Q slot " + slot);
                    Assert.That(SlotCost(lobby, "E", slot), Does.StartWith("ACT "), "E slot " + slot);
                }
                Assert.That(controller.UnequipSkill(LegacySkillDefinitions.Skill(3).Id), Is.False, "A closed lane's skills stay put.");
                Assert.That(controller.Campaign.HasLoadoutChanges, Is.False);

                controller.LobbyHud.ShowTab(LobbyTab.Stages);
                Assert.That(ActiveLabel(lobby, "Stage State 2").text, Is.EqualTo("도전 가능"));
                Assert.That(controller.Campaign.CanStartStage(2), Is.True);
                Assert.That(controller.StartCampaignStage(2), Is.True);
                duel = controller.Session;
                Assert.That(duel.Features, Is.EqualTo(QAndE));
                Assert.That(duel.GetLane(2).Count, Is.EqualTo(3), "Stages now fight with the E lane.");
                Assert.That(duel.GetLane(1), Is.Empty);
                Assert.That(Named(controller.Hud.Root, "Current E").gameObject.activeSelf, Is.True);
                Assert.That(controller.QueueLane(2), Is.True);
                Assert.That(controller.QueueBreath(), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator BreathMission_CoachHoldsQueueingAndCommitUntilSIsUsed()
        {
            yield return null;
            using (var scope = new UnlockScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.ContinueFrom(PrologueMissions.Count + 1, 2);
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(6));
                Assert.That(Label(controller.BriefingHud.Root, "Briefing Chapter").text, Is.EqualTo("가르침  ·  임무 6 / 9"));
                Assert.That(controller.Campaign.Features, Is.EqualTo(QAndE));
                scope.StartBriefedMission();
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(6));
                LegacyQueuedDuel duel = controller.Session;
                Assert.That(duel.Features, Is.EqualTo(QAndE | CombatFeature.Breath));
                Button breath = Named(controller.Hud.Root, "BreathButton").GetComponent<Button>();
                Assert.That(controller.Guide.Kind, Is.EqualTo(MissionGuideStepKind.Info));
                Assert.That(breath.gameObject.activeSelf, Is.False, "The button waits for its lesson.");
                Assert.That(controller.QueueBreath(), Is.False, "The opening beat is read first.");

                Assert.That(controller.AdvanceGuide(), Is.True);
                Assert.That(controller.Guide.Kind, Is.EqualTo(MissionGuideStepKind.Breathe));
                Assert.That(breath.gameObject.activeSelf, Is.True);
                Assert.That(breath.interactable, Is.True);
                Assert.That(controller.QueueLane(0), Is.False, "The coach waits for 숨고르기.");
                Assert.That(controller.QueueLane(2), Is.False);
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.False, "…and the beat cannot be skipped by committing.");
                Assert.That(duel.PlayerQueue, Is.Empty);

                Assert.That(controller.QueueBreath(), Is.True);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1));
                Assert.That(duel.PlayerQueue[0].IsWait, Is.True);
                Assert.That(controller.Guide.Kind, Is.EqualTo(MissionGuideStepKind.Queue), "The breath moves the coach on.");
                Assert.That(controller.Guide.ExpectedLane, Is.EqualTo(2));
                Assert.That(controller.QueueBreath(), Is.False, "The next beat asks for the E lane.");
                Assert.That(controller.QueueLane(0), Is.False);
                Assert.That(controller.QueueLane(2), Is.True);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(2));
                Assert.That(controller.Guide.Kind, Is.EqualTo(MissionGuideStepKind.Commit));
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.True);
                scope.AdvanceUntil(() => duel.CurrentSlot != null, "The committed turn must reach its first slot.");
                Assert.That(controller.CanStep, Is.False, "Mission 6 has not opened any step.");
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out _), Is.False);
                Assert.That(duel.StepAttemptsThisTurn, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator DodgeMission_DodgeBeatTakesOnlyTheDodge_AndPressureStaysClosed()
        {
            yield return null;
            using (var scope = new UnlockScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.ContinueFrom(PrologueMissions.Count + 2, 3);
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(7));
                Assert.That(controller.Campaign.Features, Is.EqualTo(QAndE | CombatFeature.Breath),
                    "Stages have what missions 5 and 6 opened.");
                Assert.That(controller.Campaign.StageLimit, Is.EqualTo(3));
                scope.StartBriefedMission();
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(7));
                LegacyQueuedDuel duel = controller.Session;
                Assert.That(duel.Features, Is.EqualTo(QAndE | CombatFeature.Breath | CombatFeature.Dodge));
                Assert.That(DuelStepHud.KeyHintFor(duel.Features), Is.EqualTo("A 회피"));

                Assert.That(controller.AdvanceGuide(), Is.True);
                Assert.That(controller.Guide.Kind, Is.EqualTo(MissionGuideStepKind.Commit));
                Assert.That(duel.PlayerQueue, Is.Empty);
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.True, "This beat commits an empty queue.");
                Assert.That(controller.Guide.Kind, Is.EqualTo(MissionGuideStepKind.Dodge));
                scope.AdvanceUntil(() => controller.CanStep && duel.CurrentSlot != null,
                    "The enemy's first strike must open the step window.");
                Assert.That(duel.CurrentSlot.EnemySkill.Kind, Is.EqualTo(LegacySkillKind.Attack));

                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out _), Is.False, "The dodge lesson takes only A.");
                Assert.That(duel.StepAttemptsThisTurn, Is.Zero, "A refused step is not an attempt.");
                Assert.That(controller.Guide.Kind, Is.EqualTo(MissionGuideStepKind.Dodge));
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out _), Is.True);
                Assert.That(duel.StepAttemptsThisTurn, Is.EqualTo(1));
                Assert.That(controller.Guide.IsFree, Is.True, "The attempt finishes the lesson.");
                Assert.That(controller.CanStep, Is.True);
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out _), Is.False,
                    "In free play the duel itself still refuses the closed 압박.");
                Assert.That(duel.StepAttemptsThisTurn, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator DirectApiUse_KeepsEveryFeature_AndNoStageWaitsForAMission()
        {
            yield return null;
            using (var scope = new UnlockScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.NewGameFromTitle(), Is.True);
                Assert.That(controller.SkipCutscene(), Is.True, "새 게임 opens with the awakening cutscene.");
                Assert.That(controller.StoryProgressionEnabled, Is.True);
                Assert.That(controller.Campaign.Features, Is.EqualTo(CombatFeature.LaneQ), "A new story starts with the Q lane.");
                Assert.That(controller.Campaign.StageLimit, Is.Zero, "No stage is open during the 서막.");
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(Label(controller.BriefingHud.Root, "Briefing Chapter").text, Is.EqualTo("깨어남  ·  임무 1 / 4"));
                Assert.That(controller.BriefingHud.BackButton.gameObject.activeSelf, Is.False, "The 서막 has no way back.");
                Assert.That(controller.LeaveBriefing(), Is.False);
                Assert.That(controller.IsInBriefing, Is.True);

                controller.ShowTitle();
                Assert.That(controller.StoryProgressionEnabled, Is.False, "The title ends the story session.");
                Assert.That(controller.Campaign.Features, Is.EqualTo(CombatFeature.All));
                Assert.That(controller.Campaign.StageLimit, Is.EqualTo(int.MaxValue));
                controller.StartNewGame();
                controller.RestartJourney();
                Assert.That(controller.StoryProgressionEnabled, Is.False);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.Campaign.Features, Is.EqualTo(CombatFeature.All));
                Assert.That(controller.Campaign.StageLimit, Is.EqualTo(int.MaxValue));
                Assert.That(controller.LobbyHud.NextMission, Is.Null, "No lobby mission is listed before the 서막 is over.");
                Assert.That(TryFindActive(controller.LobbyHud.Root, "Home Mission Border"), Is.Null);
                Assert.That(controller.StartCampaignStage(1), Is.True);
                LegacyQueuedDuel duel = controller.Session;
                Assert.That(duel.Features, Is.EqualTo(CombatFeature.All));
                for (int lane = 0; lane < 3; lane++) Assert.That(duel.GetLane(lane).Count, Is.EqualTo(3), "Lane " + lane);
                Assert.That(Named(controller.Hud.Root, "BreathButton").gameObject.activeSelf, Is.True);
                Assert.That(controller.QueueBreath(), Is.True);
                controller.ReturnToLobby();

                Assert.That(controller.Prologue.TryRestore(PrologueMissions.Count), Is.True);
                controller.RestartJourney();
                Assert.That(controller.Campaign.Features, Is.EqualTo(CombatFeature.All), "Direct use never applies the story's locks.");
                Assert.That(controller.Campaign.StageLimit, Is.EqualTo(int.MaxValue));
                Assert.That(controller.LobbyHud.NextMission.Number, Is.EqualTo(5), "The lobby still lists the next mission.");
                Assert.That(controller.LobbyHud.IsNextMissionAvailable, Is.False);
                Assert.That(controller.StartCampaignStage(1), Is.True);
                scope.WinToSettled();
                Assert.That(controller.IsShowingResult, Is.True);
                Assert.That(controller.DismissBattleResult(), Is.True);
                Assert.That(controller.IsNextMissionAvailable, Is.True);
                Assert.That(controller.Campaign.IsStageWaitingForMission(2), Is.False);
                Assert.That(controller.Campaign.CanStartStage(2), Is.True, "Outside a title session no stage waits for a mission.");
                controller.LobbyHud.ShowTab(LobbyTab.Stages);
                Assert.That(ActiveLabel(controller.LobbyHud.Root, "Stage State 2").text, Is.EqualTo("도전 가능"));
                Assert.That(controller.StartCampaignStage(2), Is.True);
                Assert.That(controller.Session.Features, Is.EqualTo(CombatFeature.All));
                Assert.That(controller.AutoSaveEnabled, Is.False);
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

        /// <summary>The lobby rebuilds its page and destroys the old one at the end of the frame (deactivated at once),
        /// so its nodes are looked up among active objects only.</summary>
        private static Transform TryFindActive(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>())
                if (candidate.name == name) return candidate;
            return null;
        }

        private static Transform ActiveNamed(GameObject root, string name)
        {
            Transform found = TryFindActive(root, name);
            Assert.That(found, Is.Not.Null, "Missing active UI node: " + name);
            return found;
        }

        private static Text ActiveLabel(GameObject root, string name) => ActiveNamed(root, name).GetComponent<Text>();

        private static Button ActiveButton(GameObject root, string name) => ActiveNamed(root, name).GetComponent<Button>();

        private static string SlotCost(GameObject lobby, string lane, int slot)
            => Label(ActiveNamed(lobby, "Loadout Slot " + lane + " " + slot).gameObject, "Slot ACT").text;

        private static Button FindButton(GameObject root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            Assert.Fail("Missing button " + name + ".");
            return null;
        }

        /// <summary>A temporary save store and a title session (story progression on), restoring a fresh lobby afterwards.</summary>
        private sealed class UnlockScope : IDisposable
        {
            private readonly GameSaveStore originalStore;
            private readonly bool originalEnabled;
            private readonly string directory;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public GameSaveStore Store { get; }

            public UnlockScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalStore = Controller.SaveStore;
                directory = Path.Combine(Application.temporaryCachePath, "MissionUnlockTests-" + Guid.NewGuid().ToString("N"));
                Store = new GameSaveStore(Path.Combine(directory, GameSaveStore.FileName));
                Controller.SaveStore = Store;
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
            }

            /// <summary>Writes a save with the first <paramref name="missionsWon"/> story missions won and stages
            /// 1..<paramref name="stagesCleared"/> cleared, then continues it from the title.</summary>
            public void ContinueFrom(int missionsWon, int stagesCleared)
            {
                var campaign = new CampaignRun();
                for (int stage = 1; stage <= stagesCleared; stage++)
                {
                    Assert.That(campaign.TryStartStage(stage), Is.True);
                    Assert.That(campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                }
                campaign.ReturnToLobby();
                var prologue = new PrologueRun();
                Assert.That(prologue.TryRestore(missionsWon), Is.True);
                Assert.That(Store.TrySave(GameSave.Capture(prologue, campaign), out string error), Is.True, error);
                Controller.ShowTitle();
                Assert.That(Controller.TitleHud.CanContinue, Is.True);
                Assert.That(Controller.ContinueGame(), Is.True);
                Assert.That(Controller.StoryProgressionEnabled, Is.True, "이어하기 follows the story's unlocks.");
                Assert.That(Controller.Prologue.ClearedCount, Is.EqualTo(missionsWon));
                Assert.That(Controller.Campaign.ClearedStageCount, Is.EqualTo(stagesCleared));
            }

            public GameSave Load()
            {
                Assert.That(Store.TryLoad(out GameSave save, out string error), Is.True, error);
                Assert.That(save.Validate(out error), Is.True, error);
                return save;
            }

            /// <summary>Starts the briefed mission and reads through its one-line intro.</summary>
            public void StartBriefedMission()
            {
                Assert.That(Controller.StartMission(), Is.True);
                Assert.That(Controller.IsShowingDialogue, Is.True, "Each mission opens with its intro.");
                Assert.That(Controller.ContinueDialogue(), Is.False);
                Assert.That(Controller.IsShowingDialogue, Is.False);
                Assert.That(Controller.IsMission, Is.True);
                Assert.That(Controller.CanChoose, Is.True);
            }

            public void Advance(float delta) => advance(delta, null);

            public void AdvanceUntil(Func<bool> condition, string message)
            {
                int frames = 0;
                while (!condition() && frames++ < 4000) Advance(.01f);
                Assert.That(condition(), Is.True, message);
            }

            /// <summary>Drops the coach so a fixture duel can commit freely.</summary>
            public void SetGuide(MissionGuide guide)
                => typeof(DuelPrototypeController).GetField("guide", PrivateInstance).SetValue(Controller, guide);

            /// <summary>Wins at once and advances until the result or a mission's outro.</summary>
            public void WinToSettled()
            {
                var zero = new LegacySkill(100, "Test", 1, 0, 0, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, "", iconId: 1);
                typeof(DuelPrototypeController).GetField("session", PrivateInstance).SetValue(Controller,
                    new LegacyQueuedDuel(100, 50, 1, 0, LegacyInitialSkills.All, new[] { zero }, new[] { 1 }, 3));
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                int frames = 0;
                while (!Controller.IsShowingResult && !Controller.IsShowingDialogue && frames++ < 2000) advance(.025f, null);
                Assert.That(Controller.IsShowingResult || Controller.IsShowingDialogue, Is.True);
            }

            public void Dispose()
            {
                // Turn saving and the story off while the temporary store is still in place, then leave a fresh lobby.
                Controller.ShowTitle();
                Controller.SaveStore = originalStore;
                Controller.StartNewGame();
                Controller.RestartJourney();
                Controller.enabled = originalEnabled;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
