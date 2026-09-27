using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
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
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.Campaign.Currency, Is.Zero);
                Assert.That(controller.Campaign.ClearedStageCount, Is.Zero);
                Release(keyboard.escapeKey);
            }
        }

        [UnityTest]
        public IEnumerator EnterAfterPurchase_ContinuesOnce_WithoutBuyingAgain()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                scope.WinStage();
                controller.LobbyHud.ShowTab(LobbyTab.Shop);
                Button purchaseCategory = null;
                foreach (var button in controller.LobbyHud.Root.GetComponentsInChildren<Button>())
                    if (button.name == "Shop Category Purchase") purchaseCategory = button;
                Assert.That(purchaseCategory, Is.Not.Null);
                purchaseCategory.onClick.Invoke();

                Button skill = null;
                foreach (var button in controller.LobbyHud.Root.GetComponentsInChildren<Button>())
                    if (button.name == "Shop Skill 14") skill = button;
                Assert.That(skill, Is.Not.Null);
                skill.onClick.Invoke();
                Assert.That(controller.Campaign.Currency, Is.EqualTo(60), "Card click only selects the offer.");

                Button purchase = null;
                foreach (var button in controller.LobbyHud.Root.GetComponentsInChildren<Button>())
                    if (button.name == "Shop Primary Action") purchase = button;
                Assert.That(purchase, Is.Not.Null);
                EventSystem.current.SetSelectedGameObject(purchase.gameObject);
                purchase.onClick.Invoke();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                Assert.That(controller.Campaign.Currency, Is.EqualTo(15));
                controller.LobbyHud.ShowTab(LobbyTab.Stages);
                controller.LobbyHud.SelectStage(2);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.enterKey);
                yield return null;
                yield return null;
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(2));
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(10));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(15));
                Release(keyboard.enterKey);
                yield return null;
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(2));
                Assert.That(controller.CanChoose, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ActualVictory_OpensMaintenance_PurchaseJoinsNextStage()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                Assert.That(controller.LobbyHud.IsVisible, Is.False);
                Assert.That(controller.AcquireSkill(14), Is.False);
                scope.WinStage();
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(60));
                Assert.That(controller.LobbyHud.IsVisible, Is.True);
                Assert.That(controller.Hud.Root.activeSelf, Is.False);
                Assert.That(controller.CanChoose, Is.False);
                Assert.That(controller.QueueLane(0), Is.False);
                controller.CommitTurn();
                scope.Advance(2f);
                Assert.That(controller.Campaign.Currency, Is.EqualTo(60), "Outcome frames cannot pay again.");
                Assert.That(controller.AcquireSkill(14), Is.True);
                Assert.That(controller.AcquireSkill(14), Is.False);
                Assert.That(controller.Campaign.Currency, Is.EqualTo(15));
                Assert.That(controller.UpgradeSkill(1), Is.False);
                Assert.That(controller.Campaign.IsSkillEquipped(14), Is.False);
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
        public IEnumerator Upgrades_AffectNextBattle_FailureRetryRetainsThem()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                scope.WinStage();
                Assert.That(controller.UpgradeSkill(1), Is.True);
                Assert.That(controller.UpgradeSkill(3), Is.True);
                Assert.That(controller.Campaign.Currency, Is.Zero);
                controller.StartCampaignStage(2);
                Assert.That(controller.Session.GetLane(0)[0].Name, Is.EqualTo("베기 +1"));
                Assert.That(controller.Session.GetLane(0)[0].MinPower, Is.EqualTo(6));
                Assert.That(controller.Session.GetLane(1)[0].MinPower, Is.EqualTo(7));
                scope.LoseStage();
                Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
                Assert.That(controller.Campaign.LastReward, Is.Zero);
                Assert.That(controller.Campaign.Currency, Is.Zero);
                Assert.That(controller.UpgradeSkill(1), Is.False, "No funds remain.");
                Assert.That(controller.LobbyHud.IsVisible, Is.True);
                controller.StartCampaignStage(2);
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(2));
                Assert.That(controller.PlayerHealth, Is.EqualTo(100));
                Assert.That(controller.EnemyHealth, Is.EqualTo(95));
                Assert.That(controller.Session.GetLane(0)[0].MinPower, Is.EqualTo(6));
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
                    if (stage < 8) Assert.That(controller.StartCampaignStage(stage + 1), Is.True);
                }
                Assert.That(controller.Campaign.ClearedStageCount, Is.EqualTo(8));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(760));
                Assert.That(controller.LobbyHud.IsVisible, Is.True);
                Assert.That(controller.Campaign.HighestUnlockedStage, Is.EqualTo(8));
                controller.RestartJourney();
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(1));
                Assert.That(controller.Campaign.Currency, Is.Zero);
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(controller.Campaign.Offers.Count, Is.EqualTo(CampaignSkillCatalog.AcquisitionSkills.Count));
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
                var skill = CampaignSkillCatalog.AcquisitionSkills[0];
                scope.InstallDuel(new LegacyQueuedDuel(100, 50, 80, 15,
                    new[] { skill, LegacyInitialSkills.All[0] }, new[] { LegacyInitialSkills.All[0] }, new[] { 1 }, 2));
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
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
            }

            public void WinStage()
            {
                // A tiny enemy fixture exercises real commit/hit/settling/result flow,
                // without turning the check into a long balance/autoplay benchmark.
                var zero = new LegacySkill(100, "Test", 1, 0, 0, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, "", iconId: 1);
                InstallDuel(new LegacyQueuedDuel(100, 50, 1, 0,
                    LegacyInitialSkills.All, new[] { zero }, new[] { 1 }, 3));
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                UntilOutcome();
            }

            public void LoseStage()
            {
                InstallDuel(new LegacyQueuedDuel(1, 0, 80, 15,
                    LegacyInitialSkills.All, new[] { LegacyInitialSkills.All[0] }, new[] { 1 }, 4));
                Controller.CommitTurn();
                UntilOutcome();
            }

            private void UntilOutcome()
            {
                int frames = 0;
                while (Controller.IsResolving && frames++ < 2000) Advance(.025f);
                Assert.That(Controller.IsResolving, Is.False, "The actual presentation must reach a stable outcome.");
                Assert.That(Controller.IsShowingResult, Is.True);
                Assert.That(Controller.ResultHud.IsVisible, Is.True);
                Assert.That(Controller.DismissBattleResult(), Is.True);
                Assert.That(Controller.LobbyHud.IsVisible, Is.True);
            }

            public void Dispose()
            {
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
            }
        }
    }
}
