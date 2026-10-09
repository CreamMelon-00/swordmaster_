using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CampaignFlowPlayModeTests : InputTestFixture
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTest]
        public IEnumerator JourneyStartsInBedroom_LobbyPausesCombat_StageSelectionRequired()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                controller.RestartJourney();
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.LobbyHud.CurrentTab, Is.EqualTo(LobbyTab.Home));
                Assert.That(controller.LobbyHud.IsVisible, Is.True);
                Assert.That(controller.HasRequiredArt, Is.True);
                Assert.That(controller.Hud.Root.activeSelf, Is.False);
                Assert.That(controller.StartCampaignStage(2), Is.False);
                Assert.That(controller.QueueLane(0), Is.False);
                Assert.That(controller.SelectCurriculumNode("horizontal-cut"), Is.True);
                Assert.That(controller.LobbyHud.CurrentTab, Is.EqualTo(LobbyTab.Home));
                scope.Advance(100f);
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(1));
                Assert.That(controller.Session.PlayerQueue.Count, Is.Zero);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.enterKey);
                yield return null;
                yield return null;
                Assert.That(controller.IsInLobby, Is.True, "Enter on Home must not accidentally start a fight.");
                Release(keyboard.enterKey);
                yield return null;
                controller.LobbyHud.ShowTab(LobbyTab.Stages);
                Assert.That(controller.LobbyHud.SelectStage(1), Is.True);
                Press(keyboard.enterKey);
                yield return null;
                yield return null;
                Assert.That(controller.CanChoose, Is.True);
                Release(keyboard.enterKey);
                yield return null;
                Press(keyboard.escapeKey);
                yield return null;
                yield return null;
                Assert.That(controller.IsPaused, Is.True);
                Assert.That(controller.IsInLobby, Is.False);
                Assert.That(controller.AbandonPausedBattle(), Is.True);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.Campaign.Currency, Is.Zero);
                Assert.That(controller.Campaign.ClearedStageCount, Is.Zero);
                Assert.That(controller.Campaign.Curriculum.Active.Id, Is.EqualTo("horizontal-cut"));
                Assert.That(controller.Campaign.Curriculum.ActiveBattles, Is.Zero,
                    "Abandoning a battle never counts toward the curriculum.");
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(9));
                Release(keyboard.escapeKey);
            }
        }

        [UnityTest]
        public IEnumerator EnterAfterCurriculumAction_ContinuesOnce_WithoutSelectingAgain()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                scope.WinStage();
                controller.LobbyHud.ShowTab(LobbyTab.Curriculum);
                FindActive<Button>(controller.LobbyHud.Root, "Curriculum Node advance").onClick.Invoke();
                Assert.That(controller.Campaign.Curriculum.Active, Is.Null, "A node click only selects it for viewing.");
                Assert.That(FindActive<Text>(controller.LobbyHud.Root, "Curriculum Detail Name").text, Is.EqualTo("플레슈"));

                Button primary = FindActive<Button>(controller.LobbyHud.Root, "Curriculum Primary Action");
                Assert.That(primary.interactable, Is.True);
                EventSystem.current.SetSelectedGameObject(primary.gameObject);
                primary.onClick.Invoke();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                Assert.That(controller.Campaign.Curriculum.Active.Id, Is.EqualTo("advance"));
                Assert.That(controller.LobbyHud.CurrentTab, Is.EqualTo(LobbyTab.Curriculum));
                Assert.That(FindActive<Button>(controller.LobbyHud.Root, "Curriculum Primary Action").interactable, Is.False,
                    "The node in progress cannot be chosen again.");
                controller.LobbyHud.ShowTab(LobbyTab.Stages);
                controller.LobbyHud.SelectStage(2);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.enterKey);
                yield return null;
                yield return null;
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(2));
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Battle));
                Assert.That(controller.ArenaView.EnemyAppearance, Is.EqualTo(EnemyAppearance.CadetB),
                    "The second stage fields the cadet shown in its lobby preview.");
                Assert.That(controller.ArenaView.EnemyRenderer.sprite.name, Does.StartWith("cadet-b-"),
                    "The entrance may still be showing Cadet B walking when this frame is checked.");
                Assert.That(controller.Campaign.Curriculum.Active.Id, Is.EqualTo("advance"));
                Assert.That(controller.Campaign.Curriculum.ActiveBattles, Is.Zero);
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(10));
                Release(keyboard.enterKey);
                yield return null;
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(2));
                Assert.That(controller.CanChoose, Is.True);
                controller.enabled = false;
                scope.WinStage();
                Assert.That(controller.Campaign.Curriculum.IsCompleted("advance"), Is.True);
                Assert.That(controller.Campaign.Curriculum.Active, Is.Null);
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(12), "The finished battle grants the skill once.");
            }
        }

        [UnityTest]
        public IEnumerator ActualVictory_OpensMaintenance_CurriculumSkillJoinsNextStage()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                Assert.That(controller.LobbyHud.IsVisible, Is.False);
                Assert.That(controller.SelectCurriculumNode("horizontal-cut"), Is.False, "The curriculum is chosen in the lobby.");
                controller.ReturnToLobby();
                Assert.That(controller.SelectCurriculumNode("horizontal-cut"), Is.True);
                Assert.That(controller.StartCampaignStage(1), Is.True);
                scope.WinStage();
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(60));
                Assert.That(controller.Campaign.Curriculum.IsCompleted("horizontal-cut"), Is.True);
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(11));
                Assert.That(controller.LobbyHud.IsVisible, Is.True);
                Assert.That(controller.Hud.Root.activeSelf, Is.False);
                Assert.That(controller.CanChoose, Is.False);
                Assert.That(controller.QueueLane(0), Is.False);
                controller.CommitTurn();
                scope.Advance(2f);
                Assert.That(controller.Campaign.Currency, Is.EqualTo(60), "Outcome frames cannot pay again.");
                Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.EqualTo(1), "…nor count the battle again.");
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(11));
                Assert.That(controller.SelectCurriculumNode("horizontal-cut"), Is.False, "A completed node cannot run again.");
                Assert.That(controller.Campaign.IsSkillEquipped(14), Is.False,
                    "A completed node grants its skill without equipping it.");
                Assert.That(controller.Campaign.IsSkillInLoadout(14), Is.False);
                Assert.That(controller.EquipSkill(14), Is.False, "The default Q lane is full.");
                Assert.That(controller.UnequipSkill(7), Is.True);
                Assert.That(controller.Campaign.GetEquippedLane(0)[2].SkillId, Is.EqualTo(7),
                    "Draft edits must not change the committed battle lane.");
                Assert.That(controller.EquipSkill(14), Is.True);
                Assert.That(controller.Campaign.GetLoadoutSlot(0, 2).SkillId, Is.EqualTo(14));
                Assert.That(controller.Campaign.HasLoadoutChanges, Is.True);
                Assert.That(controller.StartCampaignStage(2), Is.False,
                    "Combat entry is blocked until the draft is saved.");
                Assert.That(controller.SaveLoadout(), Is.True);
                Assert.That(controller.Campaign.HasLoadoutChanges, Is.False);
                Assert.That(controller.StartCampaignStage(2), Is.True);
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(2));
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Battle));
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.LobbyHud.IsVisible, Is.False);
                Assert.That(controller.Hud.Root.activeSelf, Is.True);
                Assert.That(controller.PlayerHealth, Is.EqualTo(100));
                Assert.That(controller.Session.Player.Resistance, Is.EqualTo(50));
                Assert.That(controller.EnemyHealth, Is.EqualTo(95));
                Assert.That(controller.Session.GetLane(0)[2].Id, Is.EqualTo(14));
                Assert.That(controller.Session.GetLane(0)[2].IconId, Is.EqualTo(10));
                controller.StartCampaignStage(2);
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(2));
                Assert.That(controller.Hud.Root.transform.Find("Stage/Stage Label").GetComponent<Text>().text,
                    Does.Contain("02 / 08"));
            }
        }

        [UnityTest]
        public IEnumerator Training_UsesDummyWithoutPlanningClock_ThenRetriesAtDoubleHealth()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                scope.WinStage();
                Assert.That(controller.StartCampaignStage(2), Is.True);
                scope.WinStage();
                int currency = controller.Campaign.Currency;
                int cleared = controller.Campaign.ClearedStageCount;
                int owned = controller.Campaign.OwnedSkills.Count;
                int curriculum = controller.Campaign.Curriculum.CompletedCount;

                Assert.That(controller.Campaign.IsTrainingUnlocked, Is.True);
                Assert.That(controller.StartTraining(), Is.True);
                Assert.That(controller.IsTrainingBattle, Is.True);
                Assert.That(controller.ArenaView.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy));
                Assert.That(controller.EnemyHealth, Is.EqualTo(50));
                Assert.That(controller.Session.RoundLimit, Is.EqualTo(5));
                Assert.That(controller.Guide, Is.Null);
                Assert.That(controller.CoachHud.IsVisible, Is.False);
                float planningTime = controller.TurnTimeRemaining;
                scope.Advance(25f);
                Assert.That(controller.CanChoose, Is.True, "Training has no real-time planning deadline.");
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(planningTime));
                Assert.That(controller.LaneCycleCost, Is.Zero);
                Assert.That(FindActive<Text>(controller.Hud.Root, "Untimed Hint").text,
                    Is.EqualTo("수련 · 시간 제한 없음"));

                scope.WinToResult();
                Assert.That(controller.Result.IsTraining, Is.True);
                Assert.That(FindActive<Text>(controller.ResultHud.Root, "Result Heading").text, Is.EqualTo("수련 성공"));
                Assert.That(FindActive<Text>(controller.ResultHud.Root, "Result Notice").text,
                    Does.Contain("가한 피해 50").And.Contain("다음 허수아비 체력 100"));
                Assert.That(controller.Campaign.TrainingDummyHealth, Is.EqualTo(100));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(currency));
                Assert.That(controller.Campaign.ClearedStageCount, Is.EqualTo(cleared));
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(owned));
                Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.EqualTo(curriculum));

                Assert.That(controller.RetryBattleResult(), Is.True);
                Assert.That(controller.IsTrainingBattle, Is.True);
                Assert.That(controller.EnemyHealth, Is.EqualTo(100));
                Assert.That(controller.Session.RoundLimit, Is.EqualTo(5));
                controller.ReturnToLobby();
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.Campaign.TrainingVictoryCount, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator Defeat_CompletesTheActiveNode_ResetCurriculumRemovesItsSkillAndRefillsTheLane()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                controller.ReturnToLobby();
                Assert.That(controller.SelectCurriculumNode("horizontal-cut"), Is.True);
                Assert.That(controller.StartCampaignStage(1), Is.True);
                scope.LoseToResult();
                Assert.That(controller.Result.Victory, Is.False);
                Assert.That(controller.Result.CompletedCurriculumNode.Id, Is.EqualTo("horizontal-cut"),
                    "A defeat still counts as a finished battle.");
                Assert.That(controller.Result.ActiveCurriculumNode, Is.Null);
                Assert.That(FindActive<Text>(controller.ResultHud.Root, "Result Curriculum").text, Is.EqualTo("가로베기 완료"));
                Assert.That(FindActive<Text>(controller.ResultHud.Root, "Result Curriculum Detail").text, Does.Contain("가로베기"));
                Assert.That(controller.Campaign.ClearedStageCount, Is.Zero);
                Assert.That(controller.Campaign.LastReward, Is.Zero);
                Assert.That(controller.Campaign.Curriculum.IsCompleted("horizontal-cut"), Is.True);
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(10));
                Assert.That(controller.Campaign.IsSkillInLoadout(14), Is.False);
                Assert.That(controller.ResetCurriculum(), Is.False, "The result blocks lobby actions.");
                Assert.That(controller.DismissBattleResult(), Is.True);
                Assert.That(controller.LobbyHud.IsVisible, Is.True);

                Assert.That(controller.UnequipSkill(7), Is.True);
                Assert.That(controller.EquipSkill(14), Is.True);
                Assert.That(controller.SaveLoadout(), Is.True);
                Assert.That(controller.Campaign.GetEquippedLane(0)[2].SkillId, Is.EqualTo(14));

                controller.LobbyHud.ShowTab(LobbyTab.Curriculum);
                Button reset = FindActive<Button>(controller.LobbyHud.Root, "Curriculum Reset");
                Assert.That(reset.interactable, Is.True);
                reset.onClick.Invoke();
                Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.EqualTo(1), "The first click only arms the reset.");
                Assert.That(reset.GetComponentInChildren<Text>().text, Is.EqualTo("한 번 더 누르면 초기화"));
                reset.onClick.Invoke();
                Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.Zero);
                Assert.That(controller.Campaign.Curriculum.Active, Is.Null);
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(controller.Campaign.IsSkillEquipped(14), Is.False);
                Assert.That(controller.Campaign.GetEquippedLane(0)[2].SkillId, Is.EqualTo(7),
                    "The lane that lost the skill is refilled with its starting skill.");
                Assert.That(controller.Campaign.GetLoadoutSlot(0, 2).SkillId, Is.EqualTo(7));
                Assert.That(controller.Campaign.HasLoadoutChanges, Is.False);
                Assert.That(controller.ResetCurriculum(), Is.False, "Nothing is left to reset.");

                Assert.That(controller.StartCampaignStage(1), Is.True);
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(1));
                Assert.That(controller.PlayerHealth, Is.EqualTo(100));
                Assert.That(controller.EnemyHealth, Is.EqualTo(80));
                Assert.That(controller.Session.GetLane(0)[2].Id, Is.EqualTo(7));
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(10f));
                Assert.That(controller.Hud.LogCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator FinalClear_StopsAtEight_NewJourneyRestoresInitialState()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                for (int stage = 1; stage <= 8; stage++)
                {
                    scope.WinStage();
                    Assert.That(controller.Campaign.StageNumber, Is.EqualTo(stage));
                    if (stage == 1) Assert.That(controller.SelectCurriculumNode("breathing"), Is.True);
                    if (stage < 8) Assert.That(controller.StartCampaignStage(stage + 1), Is.True);
                }
                Assert.That(controller.Campaign.ClearedStageCount, Is.EqualTo(8));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(760));
                Assert.That(controller.Campaign.Curriculum.IsCompleted("breathing"), Is.True);
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(14));
                Assert.That(controller.LobbyHud.IsVisible, Is.True);
                Assert.That(controller.Campaign.HighestUnlockedStage, Is.EqualTo(8));
                controller.RestartJourney();
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(1));
                Assert.That(controller.Campaign.Currency, Is.Zero);
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.Zero);
                Assert.That(controller.Campaign.Curriculum.Active, Is.Null);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.CanChoose, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator AcquiredSkill_UsesDistinctIcon_InCurrentNextQueueAndLog()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                var skill = LegacySkillDefinitions.Skill(14);
                scope.InstallDuel(new LegacyQueuedDuel(100, 50, 80, 15,
                    new[] { skill, LegacySkillDefinitions.Skill(1) }, new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 2));
                var root = controller.Hud.Root.transform;
                var current = root.Find("Input/Keys/Current Q/Skill Image").GetComponent<Image>();
                var next = root.Find("Input/Keys/Next Q/Next Skill Image").GetComponent<Image>();
                var art = new LegacyDuelArt();
                Assert.That(current.sprite, Is.Not.Null);
                Assert.That(next.sprite, Is.Not.Null);
                Assert.That(current.sprite, Is.SameAs(art.GetSkillIcon(10)));
                Assert.That(next.sprite, Is.SameAs(art.GetSkillIcon(1)));
                Assert.That(next.sprite, Is.Not.SameAs(current.sprite),
                    "Acquired skill 14 and initial skill 1 must keep distinct role glyphs.");
                Sprite acquiredIcon = current.sprite;
                Assert.That(controller.QueueLane(0), Is.True);
                var queued = root.Find("Player Requests/Queued Skill/Icon").GetComponent<Image>();
                Assert.That(queued.sprite, Is.SameAs(acquiredIcon));
                Assert.That(current.sprite, Is.SameAs(art.GetSkillIcon(1)), "Reservation rotates the lane's current card.");
                controller.Hud.RecordResolvedSlot(skill, null, 6, 0);
                var logIcon = root.Find("Log View/Log Scroll View/Viewport/Content/Player/Log Player Skill/Icon").GetComponent<Image>();
                Assert.That(logIcon.sprite, Is.SameAs(acquiredIcon));
            }
        }

        /// <summary>The active node with this name; rebuilt lobby pages leave their old nodes inactive until destroyed.</summary>
        private static T FindActive<T>(GameObject root, string name) where T : Component
        {
            foreach (T candidate in root.GetComponentsInChildren<T>())
                if (candidate.name == name) return candidate;
            Assert.Fail("Missing active UI node: " + name);
            return null;
        }

        private sealed class FlowScope : IDisposable
        {
            private readonly bool originalEnabled;
            private readonly Action<float, UnityEngine.InputSystem.Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public FlowScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                Controller.RestartMatch();
                advance = (Action<float, UnityEngine.InputSystem.Keyboard>)Delegate.CreateDelegate(
                    typeof(Action<float, UnityEngine.InputSystem.Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
            }

            public void Advance(float delta) => advance(delta, null);

            public void InstallDuel(LegacyQueuedDuel duel)
            {
                typeof(DuelPrototypeController).GetField("session", PrivateInstance).SetValue(Controller, duel);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, new object[] { null, null });
            }

            public void WinStage()
            {
                WinToResult();
                Assert.That(Controller.DismissBattleResult(), Is.True);
                Assert.That(Controller.LobbyHud.IsVisible, Is.True);
            }

            public void WinToResult()
            {
                // A tiny enemy fixture exercises real commit/hit/settling/result flow,
                // without turning the check into a long balance/autoplay benchmark.
                var zero = new LegacySkill(100, "Test", 1, 0, 0, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, "", iconId: 1);
                InstallDuel(new LegacyQueuedDuel(100, 50, 1, 0,
                    LegacyInitialSkills.All, new[] { zero }, new[] { 1 }, 3));
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                UntilResult();
            }

            /// <summary>Loses at once and stops on the result, so a test can read it before dismissing.</summary>
            public void LoseToResult()
            {
                InstallDuel(new LegacyQueuedDuel(1, 0, 80, 15,
                    LegacyInitialSkills.All, new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 4));
                Controller.CommitTurn();
                UntilResult();
            }

            private void UntilResult()
            {
                int frames = 0;
                while (Controller.IsResolving && frames++ < 2000) Advance(.025f);
                Assert.That(Controller.IsResolving, Is.False, "The actual presentation must reach a stable outcome.");
                Assert.That(Controller.IsShowingResult, Is.True);
                Assert.That(Controller.ResultHud.IsVisible, Is.True);
            }

            public void Dispose()
            {
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
            }
        }
    }
}
