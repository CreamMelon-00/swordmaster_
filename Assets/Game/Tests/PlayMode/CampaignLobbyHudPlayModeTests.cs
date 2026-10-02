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
                Assert.That(FindText(hud, "Header Curriculum").text, Is.EqualTo("커리큘럼  선택 안 함"));
                Assert.That(TryFindText(hud, "Wallet"), Is.Null, "The lobby no longer shows currency.");
                Assert.That(FindButton(hud, "Home Open Stages"), Is.Not.Null);
                Assert.That(FindButton(hud, "Home Open Curriculum"), Is.Not.Null);
                Assert.That(FindButton(hud, "Tab Curriculum").GetComponentInChildren<Text>().text, Is.EqualTo("커리큘럼"));
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
            var run = FirstStageClearedLobby();
            int requestedStage = -1;
            using (var hud = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null,
                null, null, number => requestedStage = number, null))
            {
                hud.Show(run);
                hud.ShowTab(LobbyTab.Stages);
                Assert.That(FindText(hud, "Selected Stage State").text, Is.EqualTo("클리어 완료"));
                Assert.That(FindText(hud, "Selected Stage Curriculum").text, Is.EqualTo("커리큘럼  진행 중인 과정 없음"));
                Assert.That(TryFindText(hud, "Selected Stage Reward"), Is.Null, "The stage preview no longer shows a reward.");
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
        public IEnumerator Curriculum_NodeClickOnlySelectsChoiceSurvivesRebuildAndCompletionDoesNotAutoEquip()
        {
            yield return null;
            var parent = new GameObject("Lobby Curriculum Test");
            var run = new CampaignRun();
            int selectCalls = 0;
            CampaignLobbyHud hud = null;
            hud = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(),
                id => { selectCalls++; Assert.That(run.TrySelectCurriculumNode(id), Is.True); hud.Show(run); },
                null, null, null, null,
                stage =>
                {
                    Assert.That(run.TryStartStage(stage), Is.True);
                    hud.Hide();
                }, null);
            using (hud)
            {
                hud.Show(run);
                hud.ShowTab(LobbyTab.Curriculum);
                Assert.That(FindRect(hud, "Curriculum Panel"), Is.Not.Null);
                FindButton(hud, "Curriculum Node advance").onClick.Invoke();
                Assert.That(selectCalls, Is.Zero, "Selecting a node card must not start it.");
                Assert.That(run.Curriculum.Active, Is.Null);
                Assert.That(FindText(hud, "Curriculum Detail Name").text, Is.EqualTo("플레슈"));
                Assert.That(FindButton(hud, "Curriculum Primary Action").interactable, Is.True);
                foreach (Button button in hud.Root.GetComponentsInChildren<Button>())
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));

                FindButton(hud, "Curriculum Primary Action").onClick.Invoke();
                Assert.That(selectCalls, Is.EqualTo(1));
                Assert.That(run.Curriculum.Active.Id, Is.EqualTo("advance"));
                Assert.That(hud.CurrentTab, Is.EqualTo(LobbyTab.Curriculum));
                Assert.That(FindText(hud, "Curriculum Detail Name").text, Is.EqualTo("플레슈"),
                    "The chosen node must stay selected after the lobby rebuild.");
                Assert.That(FindButton(hud, "Curriculum Primary Action").interactable, Is.False);
                Assert.That(FindText(hud, "Header Curriculum").text, Is.EqualTo("커리큘럼  플레슈 0/1"));

                hud.ShowTab(LobbyTab.Stages);
                Assert.That(FindText(hud, "Selected Stage Curriculum").text, Is.EqualTo("커리큘럼  플레슈 0/1"));
                FindButton(hud, "Start Selected Stage").onClick.Invoke();
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
                Assert.That(run.ReturnToLobby(), Is.True);
                hud.Show(run);
                Assert.That(FindText(hud, "Outcome Banner").text, Is.EqualTo("전투 종료  ·  커리큘럼 완료: 플레슈"),
                    "A lost battle still counts toward the node in progress.");
                Assert.That(run.OwnedSkills.Count, Is.EqualTo(10));
                Assert.That(run.IsSkillEquipped(12), Is.False);
                Assert.That(run.IsSkillInLoadout(12), Is.False);

                hud.ShowTab(LobbyTab.Curriculum);
                Assert.That(TryFindText(hud, "Outcome Banner"), Is.Null, "The curriculum page keeps the outcome note out of its header.");
                Assert.That(FindText(hud, "Curriculum Detail Name").text, Is.EqualTo("플레슈"));
                Assert.That(FindButton(hud, "Curriculum Primary Action").GetComponentInChildren<Text>().text,
                    Is.EqualTo("완료한 과정"));
                Assert.That(FindText(hud, "Header Curriculum").text, Is.EqualTo("커리큘럼  선택 안 함"));

                hud.ShowTab(LobbyTab.Loadout);
                Assert.That(run.GetLoadoutCount(2), Is.EqualTo(3),
                    "A granted skill must not replace any draft slot automatically.");
                Assert.That(run.HasLoadoutChanges, Is.False);
            }
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Curriculum_WithoutCallbacksUsesTheRunAndResetNeedsTwoClicks()
        {
            yield return null;
            var parent = new GameObject("Lobby Curriculum Fallback Test");
            var run = new CampaignRun();
            using (var hud = CreateHud(parent))
            {
                hud.Show(run);
                hud.ShowTab(LobbyTab.Curriculum);
                Assert.That(FindButton(hud, "Curriculum Reset").interactable, Is.False, "A fresh curriculum has nothing to reset.");
                FindButton(hud, "Curriculum Node horizontal-cut").onClick.Invoke();
                FindButton(hud, "Curriculum Primary Action").onClick.Invoke();
                Assert.That(run.Curriculum.Active.Id, Is.EqualTo("horizontal-cut"));
                Assert.That(FindText(hud, "Header Curriculum").text, Is.EqualTo("커리큘럼  가로베기 0/1"),
                    "The run fallback refreshes the lobby.");

                Assert.That(run.TryStartStage(1), Is.True);
                hud.Show(run);
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(run.ReturnToLobby(), Is.True);
                hud.Show(run);
                Assert.That(hud.CurrentTab, Is.EqualTo(LobbyTab.Curriculum));
                Assert.That(run.Curriculum.IsCompleted("horizontal-cut"), Is.True);
                Assert.That(run.OwnedSkills.Count, Is.EqualTo(10));
                Assert.That(FindText(hud, "Curriculum Completed Count").text, Is.EqualTo("완료 1 / 10"));

                Button reset = FindButton(hud, "Curriculum Reset");
                Assert.That(reset.interactable, Is.True);
                reset.onClick.Invoke();
                Assert.That(run.Curriculum.CompletedCount, Is.EqualTo(1), "The first click only arms the reset.");
                Assert.That(FindButton(hud, "Curriculum Reset").GetComponentInChildren<Text>().text,
                    Is.EqualTo("한 번 더 누르면 초기화"));
                FindButton(hud, "Curriculum Reset").onClick.Invoke();
                Assert.That(run.Curriculum.CompletedCount, Is.Zero);
                Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(FindText(hud, "Curriculum Completed Count").text, Is.EqualTo("완료 0 / 10"));
                Assert.That(FindButton(hud, "Curriculum Reset").GetComponentInChildren<Text>().text,
                    Is.EqualTo("커리큘럼 초기화"));
                Assert.That(FindButton(hud, "Curriculum Reset").interactable, Is.False);
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
        public IEnumerator BattleReturn_ShowsOutcomeAndCompletedCurriculumBannerAndDisposeReleasesRoot()
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
                Assert.That(FindText(hud, "Outcome Banner").text, Is.EqualTo("전투 종료"));
                Assert.That(TryFindText(hud, "Reward Banner"), Is.Null);

                Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
                hud.Show(run);
                Assert.That(FindText(hud, "Outcome Banner").text, Is.EqualTo("전투 종료"),
                    "A lobby refresh keeps the last battle's outcome.");
                Assert.That(FindText(hud, "Header Curriculum").text, Is.EqualTo("커리큘럼  가로베기 0/1"));
                FindButton(hud, "Start Selected Stage").onClick.Invoke();
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(run.ReturnToLobby(), Is.True);
                hud.Show(run);
                Assert.That(FindText(hud, "Outcome Banner").text, Is.EqualTo("승리  ·  커리큘럼 완료: 가로베기"));
                Assert.That(FindText(hud, "Header Curriculum").text, Is.EqualTo("커리큘럼  선택 안 함"));
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
                Assert.That(TryFindText(hud, "Outcome Banner"), Is.Null);
            }
            Object.Destroy(parent);
            yield return null;
        }

        private static CampaignLobbyHud CreateHud(GameObject parent)
            => new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);

        private static CampaignRun FirstStageClearedLobby()
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
    }
}
