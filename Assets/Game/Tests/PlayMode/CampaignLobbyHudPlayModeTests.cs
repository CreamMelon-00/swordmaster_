using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CampaignLobbyHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator Home_UsesCompleteRoomBackgroundAndOverlayContract()
        {
            yield return null;
            var parent = new GameObject("Lobby Home Test");
            using (var hud = CreateHud(parent))
            {
                hud.ShowTab(LobbyTab.Home);
                hud.Show(new CampaignRun());

                Assert.That(hud.IsVisible, Is.True);
                Assert.That(hud.HasRequiredAssets, Is.True);
                Assert.That(hud.CurrentTab, Is.EqualTo(LobbyTab.Home));
                Assert.That(hud.SelectedStageNumber, Is.EqualTo(1));
                Assert.That(hud.Root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(200));
                Assert.That(hud.Root.GetComponent<CanvasScaler>().referenceResolution,
                    Is.EqualTo(new Vector2(1920f, 1080f)));
                Image room = FindImage(hud, "Bedroom Background");
                Assert.That(room.sprite, Is.Not.Null);
                Assert.That(room.sprite.name, Is.EqualTo("room"));
                Assert.That(room.GetComponent<AspectRatioFitter>().aspectMode,
                    Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
                Assert.That(FindText(hud, "Campaign Progress").text, Does.Contain("0/8"));
                Assert.That(FindText(hud, "Wallet").text, Does.Contain("0"));
                Assert.That(FindButton(hud, "Home Open Stages"), Is.Not.Null);
                Assert.That(FindRect(hud, "Home Sidebar").rect.width, Is.LessThan(400f));
            }
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Stages_CardSelectsOnlyAndExplicitChallengeUsesSelection()
        {
            yield return null;
            var parent = new GameObject("Lobby Stages Test");
            var run = RewardedLobby();
            int requestedStage = -1;
            using (var hud = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null,
                null, null, number => requestedStage = number, null))
            {
                hud.Show(run);
                hud.ShowTab(LobbyTab.Stages);
                Assert.That(FindButton(hud, "Stage Card 3").interactable, Is.False);
                Assert.That(hud.SelectStage(0), Is.False);
                Assert.That(hud.SelectStage(3), Is.False);

                FindButton(hud, "Stage Card 2").onClick.Invoke();
                Assert.That(hud.SelectedStageNumber, Is.EqualTo(2));
                Assert.That(requestedStage, Is.EqualTo(-1), "Selecting a card must not start combat.");
                Assert.That(FindText(hud, "Selected Stage Stats").text,
                    Does.Contain("적 체력  95").And.Contain("저항  18").And.Contain("위력  +1"));

                FindButton(hud, "Start Selected Stage").onClick.Invoke();
                Assert.That(requestedStage, Is.EqualTo(2));
            }
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Loadout_DraftPreservesCommittedOrderUntilValidNineSlotSave()
        {
            yield return null;
            var parent = new GameObject("Lobby Loadout Test");
            var run = new CampaignRun();
            using (var hud = CreateHud(parent))
            {
                hud.Show(run);
                hud.ShowTab(LobbyTab.Loadout);
                Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
                Assert.That(run.GetLoadoutCount(0), Is.EqualTo(3));
                Assert.That(run.GetLoadoutSlot(0, 0).SkillId, Is.EqualTo(1));

                Assert.That(run.TryUnequipSkill(1), Is.True);
                Assert.That(run.GetLoadoutSlot(0, 0), Is.Null);
                Assert.That(run.GetLoadoutCount(0), Is.EqualTo(2));
                Assert.That(run.HasLoadoutChanges, Is.True);
                Assert.That(run.CanSaveLoadout, Is.False);
                Assert.That(run.TrySaveLoadout(), Is.False);
                Assert.That(run.TryStartStage(1), Is.False, "A dirty draft cannot start combat.");
                CollectionAssert.AreEqual(new[] { 1, 2, 7 }, SkillIds(run.GetEquippedLane(0)),
                    "Draft edits must not mutate the committed battle lane.");

                Assert.That(run.TryMoveEquippedSkill(2, 1), Is.True);
                Assert.That(run.GetLoadoutSlot(0, 1).SkillId, Is.EqualTo(7));
                Assert.That(run.GetLoadoutSlot(0, 2).SkillId, Is.EqualTo(2));
                Assert.That(run.TryPlaceLoadoutSkill(1, 0, 0), Is.True);
                Assert.That(run.CanSaveLoadout, Is.True);
                Assert.That(run.TrySaveLoadout(), Is.True);
                Assert.That(run.HasLoadoutChanges, Is.False);
                CollectionAssert.AreEqual(new[] { 1, 7, 2 }, SkillIds(run.GetEquippedLane(0)));

                Assert.That(run.TryUnequipSkill(1), Is.True);
                Assert.That(run.TryUnequipSkill(7), Is.True);
                Assert.That(run.TryUnequipSkill(2), Is.True);
                Assert.That(run.GetLoadoutCount(0), Is.Zero);
                Assert.That(run.CanSaveLoadout, Is.False, "Every one of the nine fixed slots is required.");
                Assert.That(run.TrySaveLoadout(), Is.False);
                CollectionAssert.AreEqual(new[] { 1, 7, 2 }, SkillIds(run.GetEquippedLane(0)));
                Assert.That(run.TryResetLoadout(), Is.True);
                Assert.That(run.GetLoadoutCount(0), Is.EqualTo(3));
                Assert.That(run.HasLoadoutChanges, Is.False);
                hud.Show(run);
                Assert.That(hud.CurrentTab, Is.EqualTo(LobbyTab.Loadout));
            }
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Shop_TransactionPreservesScrollAndPurchaseDoesNotAutoEquip()
        {
            yield return null;
            var parent = new GameObject("Lobby Shop Test");
            var run = RewardedLobby();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.Currency, Is.EqualTo(90));
            CampaignLobbyHud hud = null;
            hud = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(),
                id => { Assert.That(run.TryAcquireSkill(id), Is.True); hud.Show(run); },
                id => { Assert.That(run.TryUpgradeSkill(id), Is.True); hud.Show(run); },
                null, null, null, null, null);
            using (hud)
            {
                hud.Show(run);
                hud.ShowTab(LobbyTab.Shop);
                FindButton(hud, "Shop Skill 14").onClick.Invoke();
                Assert.That(run.Currency, Is.EqualTo(90), "Selecting a shop card must not transact.");
                Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(FindButton(hud, "Shop Primary Action").interactable, Is.True);
                foreach (Button button in hud.Root.GetComponentsInChildren<Button>())
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                foreach (Scrollbar scrollbar in hud.Root.GetComponentsInChildren<Scrollbar>())
                    Assert.That(scrollbar.navigation.mode, Is.EqualTo(Navigation.Mode.None));

                FindButton(hud, "Shop Primary Action").onClick.Invoke();
                Assert.That(run.IsSkillEquipped(14), Is.False);
                Assert.That(run.IsSkillInLoadout(14), Is.False);
                Assert.That(run.Currency, Is.EqualTo(45));
                Assert.That(FindText(hud, "Shop Detail Name").text, Is.EqualTo("가로베기"),
                    "The purchased catalog card must stay selected after the lobby rebuild.");

                FindButton(hud, "Shop Category Upgrade").onClick.Invoke();
                ScrollRect owned = FindScroll(hud, "Shop Skill List");
                owned.verticalNormalizedPosition = .42f;
                FindButton(hud, "Shop Skill 1").onClick.Invoke();
                Assert.That(run.OwnedSkills[0].Level, Is.Zero, "Selecting an owned skill must not upgrade it.");
                Assert.That(FindScroll(hud, "Shop Skill List").verticalNormalizedPosition,
                    Is.EqualTo(.42f).Within(.001f), "Selection must preserve the category scroll position.");
                FindButton(hud, "Shop Primary Action").onClick.Invoke();
                Assert.That(run.OwnedSkills[0].Level, Is.EqualTo(1));
                Assert.That(run.Currency, Is.EqualTo(15));
                Assert.That(FindScroll(hud, "Shop Skill List").verticalNormalizedPosition,
                    Is.EqualTo(.42f).Within(.001f));

                hud.ShowTab(LobbyTab.Loadout);
                Assert.That(run.GetLoadoutCount(0), Is.EqualTo(3),
                    "Buying a skill must not replace any draft slot automatically.");
            }
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ButtonsAndShowHide_ClearEventSelectionBeforeCallbacks()
        {
            yield return null;
            var eventObject = new GameObject("Lobby Event System", typeof(EventSystem));
            var parent = new GameObject("Lobby Selection Test");
            var run = new CampaignRun();
            bool selectionWasClear = false;
            using (var hud = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null,
                null, null, _ => selectionWasClear = EventSystem.current.currentSelectedGameObject == null, null))
            {
                hud.Show(run);
                hud.ShowTab(LobbyTab.Stages);
                Button start = FindButton(hud, "Start Selected Stage");
                EventSystem.current.SetSelectedGameObject(start.gameObject);
                start.onClick.Invoke();
                Assert.That(selectionWasClear, Is.True);

                Button card = FindButton(hud, "Stage Card 1");
                EventSystem.current.SetSelectedGameObject(card.gameObject);
                hud.Show(run);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                EventSystem.current.SetSelectedGameObject(FindButton(hud, "Start Selected Stage").gameObject);
                hud.Hide();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
            }
            Object.Destroy(parent);
            Object.Destroy(eventObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattleReturn_ShowsRewardOrNoAwardBannerAndDisposeReleasesRoot()
        {
            yield return null;
            var parent = new GameObject("Lobby Outcome Test");
            var run = new CampaignRun();
            CampaignLobbyHud hud = null;
            hud = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null,
                stage =>
                {
                    Assert.That(run.TryStartStage(stage), Is.True);
                    hud.Hide();
                }, null);
            GameObject root;
            using (hud)
            {
                hud.ShowTab(LobbyTab.Stages);
                hud.Show(run);
                FindButton(hud, "Start Selected Stage").onClick.Invoke();
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
                Assert.That(run.ReturnToLobby(), Is.True);
                hud.Show(run);
                Assert.That(FindText(hud, "Reward Banner").text, Does.Contain("보상 없음"));
                root = hud.Root;
            }
            yield return null;
            Assert.That(root == null, Is.True);
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResetView_DuringBattleClearsPendingOutcomeAndReturnsHomeForNewJourney()
        {
            yield return null;
            var parent = new GameObject("Lobby Reset View Test");
            var run = new CampaignRun();
            CampaignLobbyHud hud = null;
            hud = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null,
                stage =>
                {
                    Assert.That(run.TryStartStage(stage), Is.True);
                    hud.Hide();
                }, null);
            using (hud)
            {
                hud.Show(run);
                hud.ShowTab(LobbyTab.Stages);
                FindButton(hud, "Start Selected Stage").onClick.Invoke();
                Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Battle));
                Assert.That(hud.IsVisible, Is.False);

                hud.ResetView();
                run.Reset();
                hud.Show(run);
                Assert.That(hud.CurrentTab, Is.EqualTo(LobbyTab.Home));
                Assert.That(hud.SelectedStageNumber, Is.EqualTo(1));
                Assert.That(TryFindText(hud, "Reward Banner"), Is.Null);
            }
            Object.Destroy(parent);
            yield return null;
        }

        private static CampaignLobbyHud CreateHud(GameObject parent)
            => new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);

        private static CampaignRun RewardedLobby()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            return run;
        }

        private static int[] SkillIds(System.Collections.Generic.IReadOnlyList<CampaignOwnedSkill> skills)
        {
            var ids = new int[skills.Count];
            for (int index = 0; index < ids.Length; index++) ids[index] = skills[index].SkillId;
            return ids;
        }

        private static Button FindButton(CampaignLobbyHud hud, string name)
        {
            Button result = TryFindButton(hud, name);
            Assert.That(result, Is.Not.Null, "Missing visible button: " + name);
            return result;
        }

        private static Button TryFindButton(CampaignLobbyHud hud, string name)
        {
            foreach (Button button in hud.Root.GetComponentsInChildren<Button>())
                if (button.name == name) return button;
            return null;
        }

        private static Text FindText(CampaignLobbyHud hud, string name)
        {
            Text result = TryFindText(hud, name);
            if (result != null) return result;
            Assert.Fail("Missing visible label: " + name);
            return null;
        }

        private static Text TryFindText(CampaignLobbyHud hud, string name)
        {
            foreach (Text text in hud.Root.GetComponentsInChildren<Text>())
                if (text.name == name) return text;
            return null;
        }

        private static Image FindImage(CampaignLobbyHud hud, string name)
        {
            foreach (Image image in hud.Root.GetComponentsInChildren<Image>())
                if (image.name == name) return image;
            Assert.Fail("Missing visible image: " + name);
            return null;
        }

        private static RectTransform FindRect(CampaignLobbyHud hud, string name)
        {
            foreach (RectTransform rect in hud.Root.GetComponentsInChildren<RectTransform>())
                if (rect.name == name) return rect;
            Assert.Fail("Missing visible rect: " + name);
            return null;
        }

        private static ScrollRect FindScroll(CampaignLobbyHud hud, string name)
        {
            foreach (ScrollRect scroll in hud.Root.GetComponentsInChildren<ScrollRect>())
                if (scroll.name == name) return scroll;
            Assert.Fail("Missing visible scroll: " + name);
            return null;
        }
    }
}
