using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CampaignLoadoutHudPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject CanvasRoot;
            public readonly RectTransform Panel;
            public readonly CampaignRun Run = new CampaignRun();
            public readonly CampaignLoadoutHud.ViewState State = new CampaignLoadoutHud.ViewState();
            public CampaignLoadoutHud Hud;
            public int PlaceCalls;

            public Fixture()
            {
                CanvasRoot = new GameObject("Loadout View Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasScaler));
                var canvas = CanvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 600;
                var scaler = CanvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = .5f;
                Panel = new GameObject("Loadout Test Panel", typeof(RectTransform)).GetComponent<RectTransform>();
                Panel.SetParent(CanvasRoot.transform, false);
                Panel.anchorMin = Panel.anchorMax = Panel.pivot = Vector2.one * .5f;
                Panel.sizeDelta = new Vector2(1300f, 850f);
                Build();
                Canvas.ForceUpdateCanvases();
            }

            public void Build()
            {
                Hud?.Dispose();
                Hud = new CampaignLoadoutHud(Panel, new LegacyDuelArt(), Run,
                    (id, lane, slot) => { PlaceCalls++; Assert.That(Run.TryPlaceLoadoutSkill(id, lane, slot), Is.True); },
                    id => Assert.That(Run.TryUnequipSkill(id), Is.True),
                    () => Assert.That(Run.TrySaveLoadout(), Is.True),
                    () => Assert.That(Run.TryResetLoadout(), Is.True), State);
            }

            public void Dispose() { Hud?.Dispose(); Object.Destroy(CanvasRoot); }
        }

        [UnityTest]
        public IEnumerator LoadoutView_HasNineFixedCompactSlotsAndOneSelectedExplanation()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                int slotCount = 0;
                foreach (string lane in new[] { "Q", "W", "E" })
                    for (int slot = 1; slot <= 3; slot++)
                    {
                        var card = Button(fixture.Hud.Root, "Loadout Slot " + lane + " " + slot);
                        Assert.That(card.gameObject.activeSelf, Is.True);
                        Assert.That(card.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(3),
                            "Compact slots contain only order, name and ACT labels.");
                        Assert.That(TextUnder(card.transform, "Slot Order").text, Is.EqualTo(slot.ToString()));
                        Assert.That(TextUnder(card.transform, "Slot ACT").text, Does.StartWith("ACT "));
                        slotCount++;
                    }
                Assert.That(slotCount, Is.EqualTo(9));
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Counts"), Is.Null,
                    "The nine visible slots already show the loadout's capacity.");
                foreach (string lane in new[] { "Q", "W", "E" })
                    Assert.That(TryNamed(fixture.Hud.Root, "Loadout Lane " + lane), Is.Null,
                        "All open lanes are visible together without a filter.");
                Assert.That(Button(fixture.Hud.Root, "Loadout Save").interactable, Is.False);
                Assert.That(Button(fixture.Hud.Root, "Loadout Cancel").interactable, Is.False);
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero,
                    "Every starting skill is already in a slot.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Owned Empty").text, Is.EqualTo("남은 기술 없음"));
                Assert.That(Named(fixture.Hud.Root, "Loadout Owned Empty").gameObject.activeSelf, Is.True);
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("베기"));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Role").text, Does.Contain("ACT 회복"));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Effect").text,
                    Does.Contain("다음 턴").And.Contain("ACT 회복 +1"));
                Assert.That(Label(fixture.Hud.Root, "ACT Value").text, Is.EqualTo("1"));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Values").text, Is.EqualTo("4–5"));
                Assert.That(Named(fixture.Hud.Root, "Skill Experience").gameObject.activeInHierarchy, Is.True);
                Assert.That(Label(fixture.Hud.Root, "Skill Experience Level").text, Is.EqualTo("숙련 0/3"));
                Assert.That(Label(fixture.Hud.Root, "Skill Experience Value").text, Is.EqualTo("0/10"));
                Assert.That(fixture.PlaceCalls, Is.Zero, "The first slot click only selects its details.");
                foreach (Graphic graphic in fixture.Hud.Root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.GetComponent<Button>() == null && graphic.name != "Loadout Collection Viewport")
                        Assert.That(graphic.raycastTarget, Is.False, "Decorations must not intercept card drags.");
                foreach (Button button in fixture.Hud.Root.GetComponentsInChildren<Button>(true))
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            }
        }

        [UnityTest]
        public IEnumerator EarnedLevel_UpdatesSelectedDetailAndSlotWithoutChangingLoadout()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                CampaignOwnedSkill owned = fixture.Run.OwnedSkills[0];
                for (int use = 0; use < 10; use++) owned.GainClashExperience();
                fixture.Hud.Refresh();
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("베기+"));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Values").text, Is.EqualTo("6–7"));
                Assert.That(Label(fixture.Hud.Root, "Skill Experience Level").text, Is.EqualTo("숙련 1/3"));
                Assert.That(Label(fixture.Hud.Root, "Skill Experience Value").text, Is.EqualTo("0/10"));
                Assert.That(TextUnder(Button(fixture.Hud.Root, "Loadout Slot Q 1").transform, "Slot Name").text,
                    Is.EqualTo("베기+"));
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ClosedLanes_AreNotDrawn_AndOpenLanesShareOneGridWithoutFilters()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                AssertColumns(fixture, new[] { "Q", "W", "E" }, new[] { -472f, -210f, 52f });
                ClickSelectionOnly(fixture, "Loadout Slot W 1");
                Assert.That(fixture.State.ActiveLane, Is.EqualTo(1));

                // Q and E, as after mission 5: W leaves no gap, and selection moves to the first open lane.
                fixture.Run.SetProgression(CombatFeature.LaneQ | CombatFeature.LaneE | CombatFeature.Cycle, int.MaxValue);
                fixture.Build();
                Assert.That(fixture.State.ActiveLane, Is.Zero);
                Assert.That(fixture.State.SelectedSkillId, Is.Zero, "A closed lane's skill is never explained.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("기술을 선택하세요"));
                AssertColumns(fixture, new[] { "Q", "E" }, new[] { -341f, -79f });
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Heading W"), Is.Null);
                for (int slot = 1; slot <= 3; slot++) Assert.That(TryNamed(fixture.Hud.Root, "Loadout Slot W " + slot), Is.Null);
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Lane W"), Is.Null);
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Lane Q"), Is.Null);
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Lane E"), Is.Null);
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Counts"), Is.Null);
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero);
                ClickSelectionOnly(fixture, "Loadout Slot E 1");
                Assert.That(fixture.State.ActiveLane, Is.EqualTo(2));
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");

                // The fallback is the first open lane, not always Q.
                fixture.Run.SetProgression(CombatFeature.LaneW | CombatFeature.LaneE, int.MaxValue);
                fixture.Build();
                Assert.That(fixture.State.ActiveLane, Is.EqualTo(1));
                AssertColumns(fixture, new[] { "W", "E" }, new[] { -341f, -79f });

                // One lane: its column takes the block's centre.
                fixture.Run.SetProgression(CombatFeature.LaneQ | CombatFeature.Cycle, int.MaxValue);
                fixture.Build();
                Assert.That(fixture.State.ActiveLane, Is.Zero);
                AssertColumns(fixture, new[] { "Q" }, new[] { -210f });
                foreach (string lane in new[] { "Q", "W", "E" })
                    Assert.That(TryNamed(fixture.Hud.Root, "Loadout Lane " + lane), Is.Null, lane + " filter");
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero);
                DragCard(fixture, "Loadout Slot Q 1", "Loadout Slot Q 3");
                Assert.That(fixture.Run.HasLoadoutChanges, Is.True, "The open lane is still edited by dragging.");
                Button(fixture.Hud.Root, "Loadout Cancel").onClick.Invoke();

                fixture.Run.ClearProgression();
                fixture.Build();
                AssertColumns(fixture, new[] { "Q", "W", "E" }, new[] { -472f, -210f, 52f });
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Counts"), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator SlotClick_OnlyChangesSelectionWhenNoOwnedSkillIsSelected()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                foreach (string lane in new[] { "Q", "W", "E" })
                {
                    for (int slot = 1; slot <= 3; slot++)
                    {
                        ClickSelectionOnly(fixture, "Loadout Slot " + lane + " " + slot);
                        Assert.That(fixture.State.SelectedSlot, Is.EqualTo(slot - 1));
                        Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
                    }
                }
                Assert.That(fixture.PlaceCalls, Is.Zero);
                Assert.That(Button(fixture.Hud.Root, "Loadout Save").interactable, Is.False);
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator OwnedSelection_HighlightsOnlyMatchingSlots_AndClickReplacesThenCompletesDraft()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Run.TryUnequipSkill(1), Is.True);
                fixture.Hud.Refresh();
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.EqualTo(1));
                Assert.That(Named(fixture.Hud.Root, "Loadout Owned Empty").gameObject.activeSelf, Is.False);
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 1"), Is.Not.Null);
                ClickSelectionOnly(fixture, "Loadout Owned Skill 1");
                Assert.That(fixture.State.SelectedSlot, Is.EqualTo(-1));
                Assert.That(Label(fixture.Hud.Root, "Loadout Owned Hint").text, Is.EqualTo("Q 칸을 눌러 배치"));
                foreach (string lane in new[] { "Q", "W", "E" })
                    for (int slot = 1; slot <= 3; slot++)
                        Assert.That(Named(fixture.Hud.Root, "Loadout Slot Target " + lane + " " + slot).gameObject.activeSelf,
                            Is.EqualTo(lane == "Q"), lane + " slot " + slot);

                ClickSelectionOnly(fixture, "Loadout Slot W 1");
                Assert.That(fixture.PlaceCalls, Is.Zero, "A different lane cannot accept the selected Q skill.");
                ClickSelectionOnly(fixture, "Loadout Owned Skill 1");
                Button(fixture.Hud.Root, "Loadout Slot Q 2").onClick.Invoke();
                Assert.That(fixture.PlaceCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 1).SkillId, Is.EqualTo(1));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0), Is.Null);
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 1"), Is.Null,
                    "The newly placed skill leaves the owned list immediately.");
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 2"), Is.Not.Null,
                    "The displaced skill becomes available again.");
                Assert.That(fixture.Run.GetEquippedLane(0)[1].SkillId, Is.EqualTo(2),
                    "The committed combat order changes only after save.");

                ClickSelectionOnly(fixture, "Loadout Owned Skill 2");
                Button(fixture.Hud.Root, "Loadout Slot Q 1").onClick.Invoke();
                Assert.That(fixture.PlaceCalls, Is.EqualTo(2));
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero);
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text, Is.EqualTo("저장 전 변경"));
                Assert.That(Button(fixture.Hud.Root, "Loadout Save").interactable, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator LoadoutDrag_SwapAndSaveUsesDraftBeforeCommittedOrder()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                ClickSelectionOnly(fixture, "Loadout Slot Q 2");
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(2));
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
                DragCard(fixture, "Loadout Slot Q 1", "Loadout Slot Q 2");
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0).SkillId, Is.EqualTo(2));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 1).SkillId, Is.EqualTo(1));
                Assert.That(fixture.Run.GetEquippedLane(0)[0].SkillId, Is.EqualTo(1), "Combat order stays committed until save.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text, Is.EqualTo("저장 전 변경"));
                Assert.That(Button(fixture.Hud.Root, "Loadout Save").interactable, Is.True);
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(1));
                Assert.That(fixture.State.SelectedSlot, Is.EqualTo(1));
                Button(fixture.Hud.Root, "Loadout Save").onClick.Invoke();
                Assert.That(fixture.Run.GetEquippedLane(0)[0].SkillId, Is.EqualTo(2));
                Assert.That(fixture.Run.GetEquippedLane(0)[1].SkillId, Is.EqualTo(1));
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text, Is.Empty);
                Assert.That(Button(fixture.Hud.Root, "Loadout Save").interactable, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator LoadoutRemove_ShowsBlankSlotAndAvailableSkill_ThenClickRestores()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                Button(fixture.Hud.Root, "Loadout Remove Selected").onClick.Invoke();
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0), Is.Null);
                var slot = Button(fixture.Hud.Root, "Loadout Slot Q 1");
                Assert.That(slot.gameObject.activeSelf, Is.True);
                Assert.That(TextUnder(slot.transform, "Slot Name").text, Is.EqualTo("빈 슬롯"));
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Counts"), Is.Null);
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text,
                    Is.EqualTo("빈 칸을 채워야 저장할 수 있습니다"));
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.EqualTo(1));
                Assert.That(Button(fixture.Hud.Root, "Loadout Save").interactable, Is.False);
                Assert.That(Button(fixture.Hud.Root, "Loadout Cancel").interactable, Is.True);
                Assert.That(fixture.Run.GetEquippedLane(0).Count, Is.EqualTo(3));
                var blankPointer = DragPointer(slot.gameObject);
                ExecuteEvents.Execute(slot.gameObject, blankPointer, ExecuteEvents.beginDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.False);
                Assert.That(FindActiveGhost(fixture.CanvasRoot), Is.Null, "A blank slot cannot become a drag source.");
                ExecuteEvents.Execute(slot.gameObject, blankPointer, ExecuteEvents.endDragHandler);
                ClickSelectionOnly(fixture, "Loadout Owned Skill 1");
                Button(fixture.Hud.Root, "Loadout Slot Q 1").onClick.Invoke();
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0).SkillId, Is.EqualTo(1));
                Assert.That(fixture.PlaceCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero);
                ClickSelectionOnly(fixture, "Loadout Slot Q 3");
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
                DragCard(fixture, "Loadout Slot Q 1", "Loadout Slot Q 3");
                Assert.That(fixture.Run.HasLoadoutChanges, Is.True);
                Button(fixture.Hud.Root, "Loadout Cancel").onClick.Invoke();
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0).SkillId, Is.EqualTo(1));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 2).SkillId, Is.EqualTo(7));
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator OwnedSelection_PersistsAcrossRebuild_AndSlotsSelectAnyOpenLane()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Run.TryUnequipSkill(3), Is.True);
                fixture.Build();
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.EqualTo(1));
                ClickSelectionOnly(fixture, "Loadout Owned Skill 3");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Effect").text, Is.EqualTo("표시 위력으로 1회 공격"));
                Assert.That(Label(fixture.Hud.Root, "Hits Value").text, Is.EqualTo("1회"));
                Assert.That(Label(fixture.Hud.Root, "ACT Value").text, Is.EqualTo("2"));
                fixture.Build();
                Assert.That(fixture.State.ActiveLane, Is.EqualTo(1));
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(3));
                Assert.That(fixture.State.SelectedSlot, Is.EqualTo(-1));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("깊은 찌르기"));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Role").text, Is.EqualTo("한 칸에 위력 집중"));
                ClickSelectionOnly(fixture, "Loadout Slot Q 3");
                Assert.That(fixture.PlaceCalls, Is.Zero, "Cross-lane click selects the other lane, never places the selected skill there.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Effect").text, Does.Contain("상대가 타격").And.Contain("ACT 회복 +2"));
                Assert.That(fixture.State.ActiveLane, Is.EqualTo(0));
            }
        }

        [UnityTest]
        public IEnumerator NewlyEarnedSkill_SelectsAndReplacesByClick_ThenLeavesOwnedList()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
                Assert.That(fixture.Run.TryStartStage(1), Is.True);
                Assert.That(fixture.Run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(fixture.Run.ReturnToLobby(), Is.True);
                Assert.That(fixture.Run.OwnedSkills.Count, Is.EqualTo(11),
                    "The first stage grants 탐색 and the completed node grants 가로베기.");
                fixture.Build();
                ClickSelectionOnly(fixture, "Loadout Owned Skill 14");
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(14));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("가로베기"));
                Assert.That(fixture.PlaceCalls, Is.Zero);
                Assert.That(Named(fixture.Hud.Root, "Loadout Slot Target Q 2").gameObject.activeSelf, Is.True);
                Button(fixture.Hud.Root, "Loadout Slot Q 2").onClick.Invoke();
                Assert.That(fixture.PlaceCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 1).SkillId, Is.EqualTo(14));
                Assert.That(fixture.Run.GetEquippedLane(0)[1].SkillId, Is.EqualTo(2));
                Assert.That(fixture.Run.HasLoadoutChanges, Is.True);
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 14"), Is.Null);
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 2"), Is.Not.Null);
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                ClickSelectionOnly(fixture, "Loadout Owned Skill 2");
                Assert.That(fixture.Run.HasLoadoutChanges, Is.True, "Selection also leaves an existing draft unchanged.");
            }
        }

        [UnityTest]
        public IEnumerator LoadoutDrag_SameLaneSwapsCrossLaneCancelsAndGhostNeverBlocksInput()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var source = Button(fixture.Hud.Root, "Loadout Slot Q 1").gameObject;
                var pointer = DragPointer(source);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.True);
                var ghost = FindActiveGhost(fixture.CanvasRoot);
                Assert.That(ghost, Is.Not.Null);
                Assert.That(ghost.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(ghost.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(pointer.eligibleForClick, Is.False);
                ExecuteEvents.Execute(Button(fixture.Hud.Root, "Loadout Slot Q 2").gameObject, pointer, ExecuteEvents.dropHandler);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
                Assert.That(fixture.PlaceCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 1).SkillId, Is.EqualTo(1));
                Assert.That(fixture.Hud.IsDragging, Is.False);
                Assert.That(FindActiveGhost(fixture.CanvasRoot), Is.Null);

                Assert.That(fixture.Run.TryUnequipSkill(1), Is.True);
                fixture.Hud.Refresh();
                source = Button(fixture.Hud.Root, "Loadout Owned Skill 1").gameObject;
                pointer = DragPointer(source);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(Button(fixture.Hud.Root, "Loadout Slot W 1").gameObject, pointer, ExecuteEvents.dropHandler);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
                Assert.That(fixture.PlaceCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.GetLoadoutSlot(1, 0).SkillId, Is.EqualTo(3));
                Assert.That(fixture.Hud.IsDragging, Is.False);
                Assert.That(FindActiveGhost(fixture.CanvasRoot), Is.Null);

                DragCard(fixture, "Loadout Owned Skill 1", "Loadout Slot Q 3");
                Assert.That(fixture.PlaceCalls, Is.EqualTo(2));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 2).SkillId, Is.EqualTo(1));
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 1"), Is.Null);

                source = Button(fixture.Hud.Root, "Loadout Slot Q 3").gameObject;
                pointer = DragPointer(source);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.True);
                fixture.Hud.Dispose();
                Assert.That(fixture.Hud.IsDragging, Is.False);
                Assert.That(FindActiveGhost(fixture.CanvasRoot), Is.Null, "Disposing or rebuilding the view must not leave a ghost on the top canvas.");
            }
        }

        private static PointerEventData DragPointer(GameObject source)
        {
            return new PointerEventData(EventSystem.current)
            {
                pointerDrag = source,
                position = RectTransformUtility.WorldToScreenPoint(null, source.transform.position),
                eligibleForClick = true,
                button = PointerEventData.InputButton.Left,
            };
        }

        private static void DragCard(Fixture fixture, string sourceName, string targetName)
        {
            GameObject source = Button(fixture.Hud.Root, sourceName).gameObject;
            PointerEventData pointer = DragPointer(source);
            ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
            Assert.That(fixture.Hud.IsDragging, Is.True);
            ExecuteEvents.Execute(Button(fixture.Hud.Root, targetName).gameObject, pointer, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
            Assert.That(fixture.Hud.IsDragging, Is.False);
        }

        private static void ClickSelectionOnly(Fixture fixture, string name)
        {
            int callsBefore = fixture.PlaceCalls;
            bool dirtyBefore = fixture.Run.HasLoadoutChanges;
            int[] draftBefore = SlotIds(fixture.Run, true), savedBefore = SlotIds(fixture.Run, false);
            Button(fixture.Hud.Root, name).onClick.Invoke();
            Assert.That(fixture.PlaceCalls, Is.EqualTo(callsBefore), "A selection click must never request placement: " + name);
            Assert.That(fixture.Run.HasLoadoutChanges, Is.EqualTo(dirtyBefore), "A selection click must not change draft dirtiness: " + name);
            CollectionAssert.AreEqual(draftBefore, SlotIds(fixture.Run, true), "A selection click must not alter draft slots: " + name);
            CollectionAssert.AreEqual(savedBefore, SlotIds(fixture.Run, false), "A selection click must not alter saved combat order: " + name);
        }

        private static int[] SlotIds(CampaignRun run, bool draft)
        {
            var ids = new int[9];
            for (int lane = 0; lane < 3; lane++)
                for (int slot = 0; slot < 3; slot++)
                    ids[lane * 3 + slot] = draft ? run.GetLoadoutSlot(lane, slot)?.SkillId ?? 0
                        : run.GetEquippedLane(lane)[slot].SkillId;
            return ids;
        }

        private static Transform FindActiveGhost(GameObject canvas)
        {
            foreach (Transform candidate in canvas.GetComponentsInChildren<Transform>(true))
                if (candidate.name == "Loadout Drag Ghost" && candidate.gameObject.activeInHierarchy) return candidate;
            return null;
        }

        private static int ActiveOwnedCount(GameObject root)
        {
            int count = 0;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name.StartsWith("Loadout Owned Skill ", StringComparison.Ordinal) && button.gameObject.activeInHierarchy) count++;
            return count;
        }

        private static Transform Named(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true)) if (candidate.name == name) return candidate;
            Assert.Fail("Missing loadout node: " + name);
            return null;
        }

        private static Text TextUnder(Transform parent, string name)
        {
            foreach (Text candidate in parent.GetComponentsInChildren<Text>(true)) if (candidate.name == name) return candidate;
            Assert.Fail("Missing label: " + name);
            return null;
        }

        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();
        private static Button Button(GameObject root, string name) => Named(root, name).GetComponent<Button>();
        private static Vector2 Position(GameObject root, string name) => Named(root, name).GetComponent<RectTransform>().anchoredPosition;

        private static Transform TryNamed(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true)) if (candidate.name == name) return candidate;
            return null;
        }

        private static Transform ActiveNamed(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name && candidate.gameObject.activeInHierarchy) return candidate;
            return null;
        }

        /// <summary>Exactly these lanes are drawn, each heading and its three slots in one column at the given x.</summary>
        private static void AssertColumns(Fixture fixture, string[] lanes, float[] xs)
        {
            int headings = 0;
            foreach (Transform candidate in fixture.Hud.Root.GetComponentsInChildren<Transform>(true))
                if (candidate.name.StartsWith("Loadout Heading ", StringComparison.Ordinal) && candidate.GetComponent<Text>() != null) headings++;
            Assert.That(headings, Is.EqualTo(lanes.Length), "One heading per open lane.");
            for (int index = 0; index < lanes.Length; index++)
            {
                Assert.That(Position(fixture.Hud.Root, "Loadout Heading " + lanes[index] + " Style Badge"),
                    Is.EqualTo(new Vector2(xs[index], 252f)), lanes[index] + " heading");
                for (int slot = 1; slot <= 3; slot++)
                    Assert.That(Position(fixture.Hud.Root, "Loadout Slot " + lanes[index] + " " + slot),
                        Is.EqualTo(new Vector2(xs[index], 189f - (slot - 1) * 94f)), lanes[index] + " slot " + slot);
            }
        }
    }
}
