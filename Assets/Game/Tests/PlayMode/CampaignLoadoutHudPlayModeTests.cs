using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>The campaign loadout (<see cref="CampaignLoadoutHud"/>): one gear per open lane with a socket per slot
    /// (<see cref="LoadoutGear"/>, icon and ACT only, the first at the top and the rest the way the battle's gears turn),
    /// drag to place, swap and replace, click to select, the owned rack's lane filter that follows the selection, its two
    /// rows, the detail, and save, cancel and the status line.</summary>
    public sealed class CampaignLoadoutHudPlayModeTests
    {
        private static readonly string[] Keys = { "Q", "W", "E" };

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
        public IEnumerator LoadoutView_HasOneGearPerLaneWithThreeSockets_AndOneSelectedExplanation()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                int socketCount = 0;
                for (int lane = 0; lane < 3; lane++)
                {
                    Transform gear = Named(fixture.Hud.Root, "Loadout Gear " + Keys[lane]);
                    Assert.That(gear.gameObject.activeSelf, Is.True);
                    Assert.That(gear.GetComponent<Image>().sprite, Is.SameAs(AcademyUiSkin.GearWheel),
                        Keys[lane] + " wears the battle dock's wheel.");
                    Assert.That(gear.GetComponent<Button>(), Is.Not.Null, "The wheel itself is clickable.");
                    for (int slot = 1; slot <= 3; slot++)
                    {
                        var socket = Button(fixture.Hud.Root, "Loadout Slot " + Keys[lane] + " " + slot);
                        Assert.That(socket.gameObject.activeSelf, Is.True);
                        Assert.That(socket.transform.parent, Is.SameAs(gear), "Each socket sits on its lane's gear.");
                        AssertSocketShows(socket, fixture.Run.GetLoadoutSlot(lane, slot - 1));
                        socketCount++;
                    }
                }
                Assert.That(socketCount, Is.EqualTo(9));
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Counts"), Is.Null,
                    "The nine visible sockets already show the loadout's capacity.");
                foreach (string lane in Keys)
                    Assert.That(TryNamed(fixture.Hud.Root, "Loadout Lane " + lane), Is.Null,
                        "All open lanes' gears are visible together, never behind tabs.");
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Owned Hint"), Is.Null,
                    "One hint line only: the campaign page's subtitle, no second 클릭·드래그 line inside this view.");
                Assert.That(Button(fixture.Hud.Root, "Loadout Save").interactable, Is.False);
                Assert.That(Button(fixture.Hud.Root, "Loadout Cancel").interactable, Is.False);
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero,
                    "Every starting skill is already in a socket.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Owned Empty").text, Is.EqualTo("지금 추가할 수 있는 기술이 없습니다"));
                Assert.That(Named(fixture.Hud.Root, "Loadout Owned Empty").gameObject.activeSelf, Is.True);
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("베기"));
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Detail Role"), Is.Null,
                    "No role line (로비 목적) under the name since 2026-10-16.");
                Assert.That(Position(fixture.Hud.Root, "Loadout Detail Name").y,
                    Is.EqualTo(Position(fixture.Hud.Root, "Loadout Detail Icon").y).Within(.01f), "The name sits level with the icon.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Effect").text,
                    Does.Contain("다음 턴").And.Contain("ACT 회복 +1"));
                Assert.That(Label(fixture.Hud.Root, "ACT Value").text, Is.EqualTo("1"));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Values").text, Is.EqualTo("3–4"));
                Assert.That(Named(fixture.Hud.Root, "Skill Experience").gameObject.activeInHierarchy, Is.True);
                Assert.That(Label(fixture.Hud.Root, "Skill Experience Level").text, Is.EqualTo("숙련 0/3"));
                Assert.That(Label(fixture.Hud.Root, "Skill Experience Value").text, Is.EqualTo("0/10"));
                Assert.That(fixture.PlaceCalls, Is.Zero, "The first socket click only selects its details.");
                foreach (Graphic graphic in fixture.Hud.Root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.GetComponent<Button>() == null && graphic.name != "Loadout Collection Viewport")
                        Assert.That(graphic.raycastTarget, Is.False, "Decorations must not intercept card drags: " + graphic.name);
                foreach (Button button in fixture.Hud.Root.GetComponentsInChildren<Button>(true))
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            }
        }

        [UnityTest]
        public IEnumerator SelectedSkill_ShowsReadableExperienceAndKeepsRemovalOutsideTheDetailCard()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                Canvas.ForceUpdateCanvases();
                GameObject root = fixture.Hud.Root;
                RectTransform detail = Named(root, "Loadout Selected Detail").GetComponent<RectTransform>();
                RectTransform surface = Named(root, "Detail Surface").GetComponent<RectTransform>();
                RectTransform summary = Named(root, "Skill Summary").GetComponent<RectTransform>();
                RectTransform remove = Button(root, "Loadout Remove Selected").GetComponent<RectTransform>();
                RectTransform ownedHeading = Named(root, "Loadout Owned Heading").GetComponent<RectTransform>();
                RectTransform filter = Named(root, "Loadout Filter").GetComponent<RectTransform>();
                RectTransform collection = Named(root, "Loadout Collection").GetComponent<RectTransform>();
                Transform space = root.transform;
                Assert.That(remove.IsChildOf(detail.transform), Is.False, "The action must be separate from the skill description.");
                Assert.That(remove.gameObject.activeInHierarchy, Is.True);
                Assert.That(WorldBounds(remove, space).yMax,
                    Is.LessThanOrEqualTo(WorldBounds(detail, space).yMin + 1f), "The action sits below the detail card.");
                Assert.That(WorldBounds(remove, space).yMin,
                    Is.GreaterThanOrEqualTo(WorldBounds(collection, space).yMax - 1f), "The action stays above the owned list.");
                Assert.That(WorldBounds(remove, space).Overlaps(WorldBounds(ownedHeading, space)), Is.False);
                Assert.That(WorldBounds(remove, space).Overlaps(WorldBounds(filter, space)), Is.False);
                Assert.That(WorldBounds(filter, space).Overlaps(WorldBounds(ownedHeading, space)), Is.False,
                    "The filter follows the owned heading on its row.");
                Assert.That(WorldBounds(filter, space).yMin,
                    Is.GreaterThanOrEqualTo(WorldBounds(collection, space).yMax - 1f), "The filter sits above the rack.");
                Assert.That(WorldBounds(summary, surface).yMin,
                    Is.GreaterThanOrEqualTo(surface.rect.yMin - 1f), "The enlarged footer stays on the card.");
                for (int lane = 0; lane < 3; lane++)
                {
                    RectTransform gear = Named(root, "Loadout Gear " + Keys[lane]).GetComponent<RectTransform>();
                    RectTransform heading = Named(root, "Loadout Heading " + Keys[lane] + " Style Badge").GetComponent<RectTransform>();
                    Assert.That(WorldBounds(gear, space).Overlaps(WorldBounds(detail, space)), Is.False, Keys[lane] + " gear clear of the detail.");
                    Assert.That(WorldBounds(gear, space).yMin, Is.GreaterThan(WorldBounds(ownedHeading, space).yMax),
                        Keys[lane] + " gear above the owned heading row.");
                    Assert.That(WorldBounds(gear, space).yMax, Is.LessThan(WorldBounds(heading, space).yMin),
                        Keys[lane] + " gear under its lane's heading.");
                    if (lane > 0)
                        Assert.That(WorldBounds(gear, space).Overlaps(WorldBounds(
                            Named(root, "Loadout Gear " + Keys[lane - 1]).GetComponent<RectTransform>(), space)), Is.False,
                            "Neighbouring wheels do not overlap.");
                }

                RectTransform progress = Named(root, "Skill Experience").GetComponent<RectTransform>();
                Text level = Label(root, "Skill Experience Level");
                Text value = Label(root, "Skill Experience Value");
                RectTransform track = Named(root, "Skill Experience Track").GetComponent<RectTransform>();
                Assert.That(progress.gameObject.activeInHierarchy, Is.True);
                Assert.That(level.fontSize, Is.GreaterThanOrEqualTo(17));
                Assert.That(value.fontSize, Is.GreaterThanOrEqualTo(17));
                Assert.That(level.rectTransform.rect.height, Is.GreaterThanOrEqualTo(22f));
                Assert.That(value.rectTransform.rect.height, Is.GreaterThanOrEqualTo(22f));
                Assert.That(track.rect.height, Is.GreaterThanOrEqualTo(8f));
                Assert.That(level.preferredHeight, Is.LessThanOrEqualTo(level.rectTransform.rect.height + 1f));
                Assert.That(value.preferredHeight, Is.LessThanOrEqualTo(value.rectTransform.rect.height + 1f));
            }
        }

        [UnityTest]
        public IEnumerator EarnedLevel_UpdatesSelectedDetailAndSocketWithoutChangingLoadout()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                CampaignOwnedSkill owned = fixture.Run.OwnedSkills[0];
                for (int use = 0; use < 10; use++) owned.GainClashExperience();
                fixture.Hud.Refresh();
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("베기+"));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Values").text, Is.EqualTo("4–5"));
                Assert.That(Label(fixture.Hud.Root, "Skill Experience Level").text, Is.EqualTo("숙련 1/3"));
                Assert.That(Label(fixture.Hud.Root, "Skill Experience Value").text, Is.EqualTo("0/10"));
                // The socket reads the levelled skill: still its icon and its ACT, no name.
                AssertSocketShows(Button(fixture.Hud.Root, "Loadout Slot Q 1"), fixture.Run.GetLoadoutSlot(0, 0));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0).Skill.Name, Is.EqualTo("베기+"));
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ClosedLanes_AreNotDrawn_AndOpenLanesGearsShareOneRow()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                AssertColumns(fixture, new[] { "Q", "W", "E" }, new[] { -472f, -210f, 52f });
                AssertFilters(fixture, "Q", "W", "E");
                ClickSelectionOnly(fixture, "Loadout Slot W 1");
                Assert.That(fixture.State.ActiveLane, Is.EqualTo(1));
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(1));

                // Q and E, as after mission 5: W leaves no gap, and selection moves to the first open lane.
                fixture.Run.SetProgression(CombatFeature.LaneQ | CombatFeature.LaneE | CombatFeature.Cycle, int.MaxValue);
                fixture.Build();
                Assert.That(fixture.State.ActiveLane, Is.Zero);
                Assert.That(fixture.State.SelectedSkillId, Is.Zero, "A closed lane's skill is never explained.");
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(CampaignLoadoutHud.AllLanes), "A closed lane's filter falls back to 전체.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("기술을 선택하세요"));
                AssertColumns(fixture, new[] { "Q", "E" }, new[] { -341f, -79f });
                AssertFilters(fixture, "Q", "E");
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Heading W"), Is.Null);
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Gear W"), Is.Null);
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
                AssertFilters(fixture, "W", "E");

                // One lane: its gear takes the block's centre, and there is nothing to filter.
                fixture.Run.SetProgression(CombatFeature.LaneQ | CombatFeature.Cycle, int.MaxValue);
                fixture.Build();
                Assert.That(fixture.State.ActiveLane, Is.Zero);
                AssertColumns(fixture, new[] { "Q" }, new[] { -210f });
                Assert.That(Named(fixture.Hud.Root, "Loadout Filter").gameObject.activeSelf, Is.False, "One open lane: no filter row.");
                foreach (string lane in Keys)
                    Assert.That(TryNamed(fixture.Hud.Root, "Loadout Lane " + lane), Is.Null, lane + " tab");
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero);
                DragCard(fixture, "Loadout Slot Q 1", "Loadout Slot Q 3");
                Assert.That(fixture.Run.HasLoadoutChanges, Is.True, "The open lane is still edited by dragging.");
                Button(fixture.Hud.Root, "Loadout Cancel").onClick.Invoke();

                fixture.Run.ClearProgression();
                fixture.Build();
                AssertColumns(fixture, new[] { "Q", "W", "E" }, new[] { -472f, -210f, 52f });
                AssertFilters(fixture, "Q", "W", "E");
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Counts"), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator SocketClick_OnlyChangesSelection_AndRingsThatSocket()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    for (int slot = 1; slot <= 3; slot++)
                    {
                        ClickSelectionOnly(fixture, "Loadout Slot " + Keys[lane] + " " + slot);
                        Assert.That(fixture.State.SelectedSlot, Is.EqualTo(slot - 1));
                        Assert.That(fixture.State.ActiveLane, Is.EqualTo(lane));
                        Assert.That(fixture.State.OwnedFilter, Is.EqualTo(lane), "The owned rack follows the selected gear.");
                        Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
                        AssertSelectionRing(fixture, Keys[lane], slot);
                    }
                }
                Assert.That(fixture.PlaceCalls, Is.Zero);
                Assert.That(Button(fixture.Hud.Root, "Loadout Save").interactable, Is.False);
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator OwnedSelection_OnlyShowsDetails_AndDragHighlightsMatchingSocketsThenCompletesDraft()
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
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(CampaignLoadoutHud.AllLanes), "Picking from the rack keeps the rack still.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("베기"));
                AssertSelectionRing(fixture, null, 0);
                AssertTargets(fixture, null);
                foreach (string lane in Keys)
                    for (int slot = 1; slot <= 3; slot++)
                    {
                        ClickSelectionOnly(fixture, "Loadout Owned Skill 1");
                        ClickSelectionOnly(fixture, "Loadout Slot " + lane + " " + slot);
                        AssertTargets(fixture, null);
                    }

                Assert.That(fixture.PlaceCalls, Is.Zero, "Clicking a socket after an available skill never places that skill.");
                // The last socket clicked was E's, so the rack shows E's skills: Q's filter brings 베기 back.
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 1"), Is.Null);
                ClickSelectionOnly(fixture, "Loadout Filter Q");
                GameObject source = ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 1").gameObject;
                PointerEventData pointer = DragPointer(source);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.True);
                AssertTargets(fixture, "Q");
                Assert.That(fixture.State.OwnedFilter, Is.Zero, "A drag leaves the filter as it was…");
                Assert.That(source.activeInHierarchy, Is.True, "…and the card being dragged where it was.");
                ExecuteEvents.Execute(Button(fixture.Hud.Root, "Loadout Slot Q 2").gameObject, pointer, ExecuteEvents.dropHandler);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.False);
                AssertTargets(fixture, null);
                Assert.That(fixture.PlaceCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 1).SkillId, Is.EqualTo(1));
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0), Is.Null);
                AssertSocketShows(Button(fixture.Hud.Root, "Loadout Slot Q 2"), fixture.Run.GetLoadoutSlot(0, 1));
                AssertSelectionRing(fixture, "Q", 2);
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 1"), Is.Null,
                    "The newly placed skill leaves the owned list immediately.");
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 2"), Is.Not.Null,
                    "The displaced skill becomes available again.");
                Assert.That(fixture.Run.GetEquippedLane(0)[1].SkillId, Is.EqualTo(2),
                    "The committed combat order changes only after save.");

                DragCard(fixture, "Loadout Owned Skill 2", "Loadout Slot Q 1");
                Assert.That(fixture.PlaceCalls, Is.EqualTo(2));
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero);
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text, Is.EqualTo("저장하지 않은 변경 사항"));
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
                AssertSocketShows(Button(fixture.Hud.Root, "Loadout Slot Q 1"), fixture.Run.GetLoadoutSlot(0, 0));
                AssertSocketShows(Button(fixture.Hud.Root, "Loadout Slot Q 2"), fixture.Run.GetLoadoutSlot(0, 1));
                Assert.That(fixture.Run.GetEquippedLane(0)[0].SkillId, Is.EqualTo(1), "Combat order stays committed until save.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text, Is.EqualTo("저장하지 않은 변경 사항"));
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
        public IEnumerator LoadoutRemove_LeavesABareSocketAndAvailableSkill_ThenDragRestores()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                Button(fixture.Hud.Root, "Loadout Remove Selected").onClick.Invoke();
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0), Is.Null);
                var slot = Button(fixture.Hud.Root, "Loadout Slot Q 1");
                Assert.That(slot.gameObject.activeSelf, Is.True);
                AssertSocketShows(slot, null);
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Counts"), Is.Null);
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text,
                    Is.EqualTo("빈칸을 모두 채워야 저장할 수 있습니다"));
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.EqualTo(1), "Q's rack shows the removed skill.");
                Assert.That(Button(fixture.Hud.Root, "Loadout Save").interactable, Is.False);
                Assert.That(Button(fixture.Hud.Root, "Loadout Cancel").interactable, Is.True);
                Assert.That(fixture.Run.GetEquippedLane(0).Count, Is.EqualTo(3));
                var blankPointer = DragPointer(slot.gameObject);
                ExecuteEvents.Execute(slot.gameObject, blankPointer, ExecuteEvents.beginDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.False);
                Assert.That(FindActiveGhost(fixture.CanvasRoot), Is.Null, "A bare socket cannot become a drag source.");
                ExecuteEvents.Execute(slot.gameObject, blankPointer, ExecuteEvents.endDragHandler);
                ClickSelectionOnly(fixture, "Loadout Owned Skill 1");
                ClickSelectionOnly(fixture, "Loadout Slot Q 1");
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0), Is.Null, "Clicking a bare socket cannot restore an available skill.");
                // The empty slot's detail: its name and its lane's school badge where a skill's would be (2026-10-16).
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("빈 슬롯"));
                Assert.That(Named(fixture.Hud.Root, "Detail Heading Style Badge").gameObject.activeSelf, Is.True);
                Assert.That(Label(fixture.Hud.Root, "Detail Heading").text, Is.EqualTo(SkillLaneStyle.FullName(0)));
                Assert.That(Label(Named(fixture.Hud.Root, "Detail Heading Style Badge").gameObject, "Style Key").text, Is.EqualTo("Q"));
                Assert.That(Named(fixture.Hud.Root, "Loadout Detail Icon").GetComponent<Image>().enabled, Is.False);
                AssertSelectionRing(fixture, "Q", 1);
                AssertTargets(fixture, null);
                DragCard(fixture, "Loadout Owned Skill 1", "Loadout Slot Q 1");
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0).SkillId, Is.EqualTo(1));
                AssertSocketShows(slot, fixture.Run.GetLoadoutSlot(0, 0));
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
        public IEnumerator OwnedSelection_PersistsAcrossRebuild_AndSocketsSelectAnyOpenLane()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Run.TryUnequipSkill(3), Is.True);
                fixture.Build();
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.EqualTo(1));
                ClickSelectionOnly(fixture, "Loadout Owned Skill 3");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Effect").text, Is.EqualTo("추가 효과 없음"));
                Assert.That(Label(fixture.Hud.Root, "Hits Value").text, Is.EqualTo("1회"));
                Assert.That(Label(fixture.Hud.Root, "ACT Value").text, Is.EqualTo("2"));
                fixture.Build();
                Assert.That(fixture.State.ActiveLane, Is.EqualTo(1));
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(3));
                Assert.That(fixture.State.SelectedSlot, Is.EqualTo(-1));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("깊은 찌르기"));
                Assert.That(Label(fixture.Hud.Root, "Detail Heading").text, Is.EqualTo(SkillLaneStyle.FullName(1)));
                ClickSelectionOnly(fixture, "Loadout Slot Q 3");
                Assert.That(fixture.PlaceCalls, Is.Zero, "Cross-lane click selects the other lane, never places the selected skill there.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Effect").text, Does.Contain("상대가 타격").And.Contain("ACT 회복 +2"));
                Assert.That(fixture.State.ActiveLane, Is.EqualTo(0));
                // Q's gear shows Q's owned skills: none left, so W's 깊은 찌르기 waits behind the filter.
                Assert.That(fixture.State.OwnedFilter, Is.Zero);
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero);
                Assert.That(Named(fixture.Hud.Root, "Loadout Owned Empty").gameObject.activeSelf, Is.True);
                Assert.That(Label(fixture.Hud.Root, "Loadout Owned Empty").text, Is.EqualTo("이 열에 추가할 수 있는 기술이 없습니다"));
                fixture.Build();
                Assert.That(fixture.State.OwnedFilter, Is.Zero, "The filter survives a rebuild, as the selection does.");
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.Zero);
                ClickSelectionOnly(fixture, "Loadout Filter All");
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 3"), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator NewlyEarnedSkill_SelectsForDetailsAndReplacesByDrag_ThenLeavesOwnedList()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Run.TryStartStage(1), Is.True);
                Assert.That(fixture.Run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(fixture.Run.ReturnToLobby(), Is.True);
                Assert.That(fixture.Run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
                Assert.That(NormalOwnedCount(fixture.Run), Is.EqualTo(11),
                    "The first stage grants 탐색 and the learned node grants 가로베기.");
                fixture.Build();
                ClickSelectionOnly(fixture, "Loadout Owned Skill 14");
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(14));
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("가로베기"));
                Assert.That(fixture.PlaceCalls, Is.Zero);
                AssertTargets(fixture, null);
                ClickSelectionOnly(fixture, "Loadout Slot Q 2");
                Assert.That(fixture.Run.GetLoadoutSlot(0, 1).SkillId, Is.EqualTo(2));
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 14"), Is.Not.Null, "Q's gear shows Q's owned skills.");
                DragCard(fixture, "Loadout Owned Skill 14", "Loadout Slot Q 2");
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
        public IEnumerator LoadoutDrag_DroppedOutsideSocketsLeavesDraftAndClearsTargets()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Run.TryUnequipSkill(1), Is.True);
                fixture.Hud.Refresh();
                int[] draftBefore = SlotIds(fixture.Run, true);
                GameObject source = Button(fixture.Hud.Root, "Loadout Owned Skill 1").gameObject;
                PointerEventData pointer = DragPointer(source);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.True);
                AssertTargets(fixture, "Q");
                pointer.position = new Vector2(-100f, -100f);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.dragHandler);
                // Off the sockets onto the wheel: the wheel takes clicks, never drops.
                ExecuteEvents.Execute(Button(fixture.Hud.Root, "Loadout Gear Q").gameObject, pointer, ExecuteEvents.dropHandler);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.False);
                Assert.That(fixture.PlaceCalls, Is.Zero);
                CollectionAssert.AreEqual(draftBefore, SlotIds(fixture.Run, true));
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill 1"), Is.Not.Null);
                Assert.That(FindActiveGhost(fixture.CanvasRoot), Is.Null);
                AssertTargets(fixture, null);
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(CampaignLoadoutHud.AllLanes), "A drag leaves the filter as it was.");
                ClickSelectionOnly(fixture, "Loadout Slot Q 2");
            }
        }

        [UnityTest]
        public IEnumerator LoadoutDrag_SameLaneSwapsCrossLaneCancelsAndGhostNeverBlocksInput()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(CampaignLoadoutHud.AllLanes));
                var source = Button(fixture.Hud.Root, "Loadout Slot Q 1").gameObject;
                var pointer = DragPointer(source);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.True);
                Assert.That(fixture.State.OwnedFilter, Is.Zero, "A skill taken from a socket turns the rack to its lane, as a click there would.");
                var ghost = FindActiveGhost(fixture.CanvasRoot);
                Assert.That(ghost, Is.Not.Null);
                Assert.That(ghost.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(ghost.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(pointer.eligibleForClick, Is.False);
                AssertTargets(fixture, "Q", "Q 1");
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

        [UnityTest]
        public IEnumerator OwnedFilter_FollowsTheSelectedGear_CanBeClicked_AndNeverChangesTheDraft()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(CampaignLoadoutHud.AllLanes), "The rack starts on 전체.");
                // Reordering a lane, one socket dragged onto another, selects the socket and turns the rack to that lane, as a
                // click there would (no rack card is under the pointer).
                int w1 = fixture.Run.GetLoadoutSlot(1, 0).SkillId, w3 = fixture.Run.GetLoadoutSlot(1, 2).SkillId;
                GameObject socket = Button(fixture.Hud.Root, "Loadout Slot W 1").gameObject;
                PointerEventData pointer = DragPointer(socket);
                ExecuteEvents.Execute(socket, pointer, ExecuteEvents.beginDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.True);
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(1), "Taking W 1's skill turns the rack to W at once.");
                ExecuteEvents.Execute(Button(fixture.Hud.Root, "Loadout Slot W 3").gameObject, pointer, ExecuteEvents.dropHandler);
                ExecuteEvents.Execute(socket, pointer, ExecuteEvents.endDragHandler);
                Assert.That(fixture.Run.GetLoadoutSlot(1, 2).SkillId, Is.EqualTo(w1));
                Assert.That(fixture.Run.GetLoadoutSlot(1, 0).SkillId, Is.EqualTo(w3));
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(1));
                AssertSelectionRing(fixture, "W", 3);
                ClickSelectionOnly(fixture, "Loadout Filter All");

                for (int lane = 0; lane < 3; lane++)
                    for (int slot = 0; slot < 3; slot++)
                        Assert.That(fixture.Run.TryUnequipSkill(fixture.Run.GetLoadoutSlot(lane, slot).SkillId), Is.True);
                fixture.Hud.Refresh();
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(CampaignLoadoutHud.AllLanes));
                Assert.That(Label(Named(fixture.Hud.Root, "Loadout Filter All").gameObject, "Button Label").text, Is.EqualTo("전체"));
                for (int lane = 0; lane < 3; lane++)
                    Assert.That(Label(Named(fixture.Hud.Root, "Loadout Filter " + Keys[lane]).gameObject, "Button Label").text, Is.EqualTo(Keys[lane]));
                AssertFilterShows(fixture, CampaignLoadoutHud.AllLanes, 9);

                // A socket's lane: the selection follows, the rack shows that lane only.
                ClickSelectionOnly(fixture, "Loadout Slot W 2");
                AssertFilterShows(fixture, 1, 3);
                Assert.That(fixture.State.SelectedSlot, Is.EqualTo(1));
                // The wheel off its sockets: that lane's rack, the selection unchanged.
                ClickSelectionOnly(fixture, "Loadout Gear E");
                AssertFilterShows(fixture, 2, 3);
                Assert.That(fixture.State.ActiveLane, Is.EqualTo(1));
                Assert.That(fixture.State.SelectedSlot, Is.EqualTo(1), "The wheel only turns the rack to its lane.");
                // The filter row itself, both ways.
                ClickSelectionOnly(fixture, "Loadout Filter All");
                AssertFilterShows(fixture, CampaignLoadoutHud.AllLanes, 9);
                ClickSelectionOnly(fixture, "Loadout Filter Q");
                AssertFilterShows(fixture, 0, 3);
                Assert.That(fixture.State.SelectedSlot, Is.EqualTo(1), "A filter click leaves the selection.");
                // An owned skill picked from outside the shown lane (by code) brings its lane up; one inside keeps it.
                int wSkill = OwnedOfLane(fixture.Run, 1);
                Button(fixture.Hud.Root, "Loadout Owned Skill " + wSkill).onClick.Invoke();
                AssertFilterShows(fixture, 1, 3);
                ClickSelectionOnly(fixture, "Loadout Filter All");
                ClickSelectionOnly(fixture, "Loadout Owned Skill " + OwnedOfLane(fixture.Run, 2));
                AssertFilterShows(fixture, CampaignLoadoutHud.AllLanes, 9);
                // It survives a rebuild; nothing filtered away is lost.
                ClickSelectionOnly(fixture, "Loadout Filter E");
                fixture.Build();
                AssertFilterShows(fixture, 2, 3);
                Assert.That(fixture.Run.HasLoadoutChanges, Is.True);
                Assert.That(fixture.PlaceCalls, Is.EqualTo(1), "Only the W reorder placed anything.");
            }
        }

        [UnityTest]
        public IEnumerator OwnedRack_FillsTwoRowsColumnByColumn_AndScrollsSideways()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                for (int lane = 0; lane < 3; lane++)
                    for (int slot = 0; slot < 3; slot++)
                        Assert.That(fixture.Run.TryUnequipSkill(fixture.Run.GetLoadoutSlot(lane, slot).SkillId), Is.True);
                fixture.Hud.Refresh();
                Canvas.ForceUpdateCanvases();
                var cards = new List<RectTransform>();
                foreach (CampaignOwnedSkill owned in fixture.Run.OwnedSkills)
                    if (!fixture.Run.IsCounterSkill(owned.SkillId))
                        cards.Add(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill " + owned.SkillId).GetComponent<RectTransform>());
                Assert.That(cards.Count, Is.EqualTo(9));
                for (int index = 0; index < cards.Count; index++)
                    Assert.That(cards[index].anchoredPosition, Is.EqualTo(CampaignLoadoutHud.OwnedCardPosition(index)), "Card " + index);
                Assert.That(cards[0].anchoredPosition.x, Is.EqualTo(cards[1].anchoredPosition.x), "A column holds two cards…");
                Assert.That(cards[0].anchoredPosition.y, Is.GreaterThan(cards[1].anchoredPosition.y), "…the first on the top row…");
                Assert.That(cards[2].anchoredPosition.y, Is.EqualTo(cards[0].anchoredPosition.y), "…and the next column starts on top again.");
                Assert.That(cards[2].anchoredPosition.x, Is.GreaterThan(cards[0].anchoredPosition.x));
                var rows = new HashSet<float>();
                foreach (RectTransform card in cards) rows.Add(card.anchoredPosition.y);
                Assert.That(rows.Count, Is.EqualTo(CampaignLoadoutHud.OwnedRows), "Two rows.");
                RectTransform viewport = Named(fixture.Hud.Root, "Loadout Collection Viewport").GetComponent<RectTransform>();
                Transform space = fixture.Hud.Root.transform;
                Rect window = WorldBounds(viewport, space);
                foreach (RectTransform card in cards)
                {
                    Rect bounds = WorldBounds(card, space);
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(window.yMax + .5f), card.name + " inside the rack");
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(window.yMin - .5f), card.name + " inside the rack");
                }
                for (int index = 2; index < cards.Count; index++)
                    Assert.That(WorldBounds(cards[index], space).Overlaps(WorldBounds(cards[index - 2], space)), Is.False);
                Assert.That(WorldBounds(cards[0], space).Overlaps(WorldBounds(cards[1], space)), Is.False, "The rows do not touch.");

                // Each card keeps its icon with its core mark, its name, ACT and lane, and a click only inspects it.
                Transform first = cards[0];
                CampaignOwnedSkill firstSkill = fixture.Run.OwnedSkills[0];
                Assert.That(Label(first.gameObject, "Owned Name").text, Is.EqualTo(firstSkill.Skill.Name));
                Assert.That(Label(first.gameObject, "Owned ACT").text, Is.EqualTo("ACT " + firstSkill.Skill.Cost));
                Assert.That(Label(first.gameObject, "Owned Lane").text, Is.EqualTo(Keys[firstSkill.Skill.LaneIndex]));
                Assert.That(first.Find("Role Pip"), Is.Not.Null);
                AssertIcon(first.Find("Owned Icon").GetComponent<Image>(), firstSkill.Skill);

                var scroll = Named(fixture.Hud.Root, "Loadout Collection").GetComponent<ScrollRect>();
                Assert.That(scroll.horizontal, Is.True, "Overflowing skills scroll sideways…");
                Assert.That(scroll.vertical, Is.False, "…never up and down.");
                RectTransform content = Named(fixture.Hud.Root, "Loadout Owned Items").GetComponent<RectTransform>();
                Assert.That(content.sizeDelta.x, Is.EqualTo(Mathf.Max(viewport.rect.width, 5 * CampaignLoadoutHud.OwnedColumnPitch)));
                Assert.That(content.sizeDelta.y, Is.EqualTo(CampaignLoadoutHud.OwnedRows * CampaignLoadoutHud.OwnedRowPitch));

                // More than the window holds (the field's four pickups join the nine): scrolled along, a click and a drag
                // leave the rack where it is, so nothing shifts under the pointer.
                foreach (CampaignFieldPickup pickup in CampaignFieldPickups.All)
                    Assert.That(fixture.Run.TryCollectFieldPickup(pickup.Id), Is.True, pickup.Id);
                fixture.Hud.Refresh();
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.EqualTo(13));
                Assert.That(content.sizeDelta.x, Is.EqualTo(7 * CampaignLoadoutHud.OwnedColumnPitch));
                Assert.That(content.sizeDelta.x, Is.GreaterThan(viewport.rect.width), "Seven columns overflow the window.");
                content.anchoredPosition = new Vector2(-150f, 0f);
                int inView = fixture.Run.OwnedSkills[2].SkillId;
                ClickSelectionOnly(fixture, "Loadout Owned Skill " + inView);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-150f), "A click only inspects: the rack stays.");
                GameObject source = ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill " + inView).gameObject;
                PointerEventData pointer = DragPointer(source);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.True);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-150f), "A drag leaves the rack under the pointer as it was…");
                Assert.That(source.activeInHierarchy, Is.True);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
                Assert.That(fixture.Hud.IsDragging, Is.False);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-150f), "…and so does letting go.");
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(CampaignLoadoutHud.AllLanes));

                // A lane's skills fit the window, so its filter shows them from the left end, closed up.
                ClickSelectionOnly(fixture, "Loadout Filter W");
                Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.EqualTo(4), "W's three and the field's 꿰뚫기.");
                Assert.That(content.sizeDelta.x, Is.EqualTo(viewport.rect.width), "W's skills fit the window.");
                Assert.That(content.anchoredPosition.x, Is.EqualTo(0f));
                RectTransform shownFirst = ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill " + OwnedOfLane(fixture.Run, 1)).GetComponent<RectTransform>();
                Assert.That(shownFirst.anchoredPosition, Is.EqualTo(CampaignLoadoutHud.OwnedCardPosition(0)), "The lane's skills close up from the start.");
            }
        }

        [UnityTest]
        public IEnumerator GearOrder_TopIsFirst_AndTheRestRunTheWayTheBattleGearsTurn()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Canvas.ForceUpdateCanvases();
                for (int lane = 0; lane < 3; lane++)
                {
                    string key = Keys[lane];
                    RectTransform gear = Named(fixture.Hud.Root, "Loadout Gear " + key).GetComponent<RectTransform>();
                    var at = new Vector2[3];
                    for (int slot = 0; slot < 3; slot++)
                    {
                        RectTransform socket = Named(fixture.Hud.Root, "Loadout Slot " + key + " " + (slot + 1)).GetComponent<RectTransform>();
                        at[slot] = gear.InverseTransformPoint(socket.position);
                        float angle = Mathf.Atan2(at[slot].y, at[slot].x) * Mathf.Rad2Deg;
                        Assert.That(LegacySkillGear.Wrap(angle), Is.EqualTo(LegacySkillGear.Wrap(LegacySkillGear.LoadoutSocketAngle(slot, 3))).Within(.5f),
                            key + " socket " + (slot + 1) + " sits where the lane's order puts it.");
                        Assert.That(at[slot].magnitude, Is.EqualTo(LoadoutGear.SocketRadius).Within(.5f), "On the rim.");
                    }
                    Assert.That(at[0].x, Is.EqualTo(0f).Within(.5f), key + ": the first skill at the top, where the battle shows the current one…");
                    Assert.That(at[0].y, Is.GreaterThan(Mathf.Max(at[1].y, at[2].y)));
                    Assert.That(at[1].x, Is.LessThan(0f), key + ": …the second on the left, where the battle's next skill comes up from…");
                    Assert.That(at[2].x, Is.GreaterThan(0f), key + ": …and the third on the right.");
                    Assert.That(Named(fixture.Hud.Root, "Loadout Slot " + key + " 1").GetComponent<RectTransform>().rect.width,
                        Is.GreaterThan(Named(fixture.Hud.Root, "Loadout Slot " + key + " 2").GetComponent<RectTransform>().rect.width),
                        "The first socket is the largest, as the battle's current skill.");
                    LoadoutGearArc arrow = gear.Find("Turn Arrow").GetComponent<LoadoutGearArc>();
                    Assert.That(arrow, Is.Not.Null, key + " shows which way it turns.");
                    Assert.That(arrow.Head, Is.GreaterThan(0f), "An arrow, with its head…");
                    Assert.That(arrow.Direction, Is.EqualTo(LegacySkillGear.TurnDirection), "…turning the battle gears' way: clockwise.");
                    Assert.That(arrow.StartAngle, Is.LessThan(LegacySkillGear.TopAngle).And.GreaterThan(0f), "It starts beside the top socket…");
                    Assert.That(arrow.Tip.x, Is.GreaterThan(0f), "…and points where the top skill goes once used.");
                    Assert.That(arrow.raycastTarget, Is.False);
                    Assert.That(arrow.Radius, Is.GreaterThan(LoadoutGear.SocketRadius + LoadoutGear.FirstSocketSize * .5f), "Round the outside.");
                }

                // The battle: the duel takes each lane in the sockets' order, and its gear turns the arrow's way.
                RectTransform qGear = Named(fixture.Hud.Root, "Loadout Gear Q").GetComponent<RectTransform>();
                float secondSide = qGear.InverseTransformPoint(Named(fixture.Hud.Root, "Loadout Slot Q 2").position).x;
                Assert.That(fixture.Run.TryStartStage(1), Is.True);
                LegacyQueuedDuel duel = fixture.Run.CreateDuel();
                for (int lane = 0; lane < 3; lane++)
                    for (int slot = 0; slot < 3; slot++)
                        Assert.That(duel.GetLane(lane)[slot].Id, Is.EqualTo(fixture.Run.GetLoadoutSlot(lane, slot).SkillId),
                            Keys[lane] + " socket " + (slot + 1) + " is the battle lane's skill " + (slot + 1) + ".");
                var parent = new GameObject("Loadout Gear Battle Check");
                var battle = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                try
                {
                    battle.Refresh(duel, 10f, false, -1, null, null, null, 0f, 0f);
                    float before = battle.LaneGearRotation(0);
                    int second = duel.GetLane(0)[1].Id;
                    Assert.That(duel.TryQueueLane(0), Is.True);
                    battle.PlayLaneTurn(0);
                    battle.Refresh(duel, 10f, false, -1, null, null, null, 0f, 0f);
                    Canvas.ForceUpdateCanvases();
                    var current = (RectTransform)battle.Root.transform.Find("Input/Keys/Current Q/Skill Image");
                    Assert.That(Mathf.Sign(current.anchoredPosition.x), Is.EqualTo(Mathf.Sign(secondSide)),
                        "In battle the second skill comes up from the side its socket sits on here.");
                    battle.Refresh(duel, 10f, false, -1, null, null, null, 0f, battle.LaneTurnSeconds);
                    float turned = LegacySkillGear.Wrap(battle.LaneGearRotation(0) - before + 180f) - 180f;
                    Assert.That(Mathf.Sign(turned), Is.EqualTo((float)LegacySkillGear.TurnDirection), "The battle gear turns the arrow's way…");
                    Assert.That(Mathf.Abs(turned), Is.EqualTo(LegacySkillGear.SlotDegrees).Within(.01f), "…a slot a skill…");
                    Assert.That(duel.GetLane(0)[0].Id, Is.EqualTo(second), "…bringing the second socket's skill to the top.");
                }
                finally { battle.Dispose(); Object.Destroy(parent); }
            }
        }

        [UnityTest]
        public IEnumerator Wheel_TakesThePointerOnItsDrawnDiscAndSocketsOnly_AndNeverLightsUnderASocket()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Canvas.ForceUpdateCanvases();
                for (int lane = 0; lane < 3; lane++)
                {
                    string key = Keys[lane];
                    RectTransform gear = Named(fixture.Hud.Root, "Loadout Gear " + key).GetComponent<RectTransform>();
                    Button wheel = gear.GetComponent<Button>();
                    Image picture = gear.GetComponent<Image>();
                    Button top = Button(fixture.Hud.Root, "Loadout Slot " + key + " 1");
                    Assert.That(wheel.transition, Is.EqualTo(Selectable.Transition.None),
                        key + ": the sockets are the wheel's children, so a tint of its own would light the whole wheel under a socket.");
                    for (int slot = 1; slot <= 3; slot++)
                        Assert.That(Button(fixture.Hud.Root, "Loadout Slot " + key + " " + slot).transition,
                            Is.EqualTo(Selectable.Transition.ColorTint), "Each socket keeps its own tint.");
                    Assert.That(gear.GetComponent<LoadoutGearHitArea>().Radius, Is.EqualTo(LoadoutGear.HitRadius));
                    Assert.That(LoadoutGear.HitRadius, Is.LessThan(LoadoutGear.WheelSize * .5f));

                    // The drawn wheel takes the pointer up to its tooth tips; the square picture's empty corners take nothing.
                    Assert.That(picture.Raycast(ScreenPoint(gear, Vector2.zero), null), Is.True, key + ": the wheel's centre.");
                    Assert.That(picture.Raycast(ScreenPoint(gear, new Vector2(LoadoutGear.HitRadius - 2f, 0f)), null), Is.True,
                        key + ": just inside the tips.");
                    Assert.That(picture.Raycast(ScreenPoint(gear, new Vector2(LoadoutGear.HitRadius + 2f, 0f)), null), Is.False,
                        key + ": just past the tips.");
                    float corner = LoadoutGear.WheelSize * .5f - 4f;
                    foreach (Vector2 at in new[] { new Vector2(corner, corner), new Vector2(-corner, corner),
                        new Vector2(corner, -corner), new Vector2(-corner, -corner) })
                    {
                        Assert.That(RectTransformUtility.RectangleContainsScreenPoint(gear, ScreenPoint(gear, at), null), Is.True);
                        Assert.That(picture.Raycast(ScreenPoint(gear, at), null), Is.False,
                            key + ": the picture's empty corner " + at + " clicks nothing (it would turn the rack).");
                    }

                    // The filter is the sockets' too (Unity asks every ancestor's): the top socket keeps its whole rect, also
                    // where it reaches past the wheel's tips.
                    Vector2 topEdge = LoadoutGear.SocketOffset(0, 3) + new Vector2(0f, LoadoutGear.FirstSocketSize * .5f - 2f);
                    Assert.That(topEdge.magnitude, Is.GreaterThan(LoadoutGear.HitRadius), "This point lies past the tips.");
                    Assert.That(RectTransformUtility.RectangleContainsScreenPoint(top.GetComponent<RectTransform>(), ScreenPoint(gear, topEdge), null), Is.True);
                    Assert.That(top.GetComponent<Image>().Raycast(ScreenPoint(gear, topEdge), null), Is.True,
                        key + ": the top socket's rim takes the pointer.");
                    for (int slot = 0; slot < 3; slot++)
                    {
                        Button socket = Button(fixture.Hud.Root, "Loadout Slot " + key + " " + (slot + 1));
                        Assert.That(socket.GetComponent<Image>().Raycast(ScreenPoint(gear, LoadoutGear.SocketOffset(slot, 3)), null), Is.True,
                            key + " socket " + (slot + 1) + "'s centre.");
                    }
                }
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

        /// <summary>The screen point of <paramref name="local"/> in <paramref name="rect"/>'s space (an overlay canvas).</summary>
        private static Vector2 ScreenPoint(RectTransform rect, Vector2 local)
            => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local));

        private static void DragCard(Fixture fixture, string sourceName, string targetName)
        {
            GameObject source = Button(fixture.Hud.Root, sourceName).gameObject;
            Assert.That(source.activeInHierarchy, Is.True, sourceName + " must be on screen to drag.");
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

        private static int OwnedOfLane(CampaignRun run, int lane)
        {
            foreach (CampaignOwnedSkill owned in run.OwnedSkills)
                if (!run.IsCounterSkill(owned.SkillId) && owned.Skill.LaneIndex == lane && !run.IsSkillInLoadout(owned.SkillId)) return owned.SkillId;
            Assert.Fail("No unplaced skill in lane " + lane);
            return 0;
        }

        private static int NormalOwnedCount(CampaignRun run)
        {
            int count = 0;
            foreach (CampaignOwnedSkill owned in run.OwnedSkills) if (!run.IsCounterSkill(owned.SkillId)) count++;
            return count;
        }

        /// <summary>The rack shows <paramref name="filter"/>'s unplaced skills only (<paramref name="count"/> of them), and
        /// the filter row marks that choice.</summary>
        private static void AssertFilterShows(Fixture fixture, int filter, int count)
        {
            Assert.That(fixture.State.OwnedFilter, Is.EqualTo(filter));
            Assert.That(ActiveOwnedCount(fixture.Hud.Root), Is.EqualTo(count), "Shown under filter " + filter);
            foreach (CampaignOwnedSkill owned in fixture.Run.OwnedSkills)
            {
                if (fixture.Run.IsCounterSkill(owned.SkillId) || fixture.Run.IsSkillInLoadout(owned.SkillId)) continue;
                bool expected = filter == CampaignLoadoutHud.AllLanes || owned.Skill.LaneIndex == filter;
                Assert.That(ActiveNamed(fixture.Hud.Root, "Loadout Owned Skill " + owned.SkillId) != null, Is.EqualTo(expected),
                    owned.Skill.Name + " under filter " + filter);
            }
            Assert.That(Named(fixture.Hud.Root, "Loadout Filter All").GetComponent<Image>().color,
                Is.EqualTo(filter == CampaignLoadoutHud.AllLanes ? DuelVisualTheme.Accent : DuelVisualTheme.Card));
            for (int lane = 0; lane < 3; lane++)
            {
                Transform button = TryNamed(fixture.Hud.Root, "Loadout Filter " + Keys[lane]);
                if (button != null)
                    Assert.That(button.GetComponent<Image>().color, Is.EqualTo(filter == lane ? DuelVisualTheme.Accent : DuelVisualTheme.Card),
                        Keys[lane] + " filter marks the choice");
            }
            for (int lane = 0; lane < 3; lane++)
                Assert.That(Named(fixture.Hud.Root, "Loadout Gear " + Keys[lane]).gameObject.activeInHierarchy, Is.True,
                    "The filter narrows the rack only: every gear stays.");
        }

        /// <summary>The filter row offers 전체 and exactly these open lanes, in order, after the owned heading.</summary>
        private static void AssertFilters(Fixture fixture, params string[] lanes)
        {
            Assert.That(Named(fixture.Hud.Root, "Loadout Filter").gameObject.activeSelf, Is.True);
            float previous = Position(fixture.Hud.Root, "Loadout Filter All").x;
            foreach (string lane in Keys)
                Assert.That(TryNamed(fixture.Hud.Root, "Loadout Filter " + lane) != null, Is.EqualTo(Array.IndexOf(lanes, lane) >= 0),
                    lane + " filter only for an open lane");
            foreach (string lane in lanes)
            {
                float x = Position(fixture.Hud.Root, "Loadout Filter " + lane).x;
                Assert.That(x, Is.GreaterThan(previous), "전체 · Q · W · E in order");
                previous = x;
            }
        }

        // A socket shows only its skill's icon and ACT cost (an empty one, the bare ring): its one text is the cost; no
        // name, order or core mark.
        private static void AssertSocketShows(Button socket, CampaignOwnedSkill owned)
        {
            Text[] texts = socket.GetComponentsInChildren<Text>(true);
            Assert.That(texts.Length, Is.EqualTo(1), socket.name + ": its ACT is its only text.");
            Assert.That(texts[0].name, Is.EqualTo("Slot ACT"));
            foreach (string gone in new[] { "Slot Name", "Slot Order", "Role Pip" })
                Assert.That(socket.transform.Find(gone), Is.Null, socket.name + " shows no " + gone);
            var ring = (Image)socket.targetGraphic;
            Assert.That(ring.sprite, Is.Not.Null, socket.name + " wears the battle's socket.");
            Assert.That(ring.sprite.texture.name, Is.EqualTo("Skill Gear Socket"));
            Image icon = socket.transform.Find("Slot Icon").GetComponent<Image>();
            Transform plate = socket.transform.Find("Slot ACT Plate");
            if (owned == null)
            {
                Assert.That(icon.enabled, Is.False, socket.name + ": an empty socket is the bare ring…");
                Assert.That(plate.gameObject.activeSelf, Is.False, "…with no cost.");
                Assert.That(texts[0].text, Is.Empty);
                return;
            }
            Assert.That(icon.enabled, Is.True, socket.name + " shows its icon.");
            AssertIcon(icon, owned.Skill);
            Assert.That(plate.gameObject.activeSelf, Is.True);
            Assert.That(texts[0].text, Is.EqualTo("ACT " + owned.Skill.Cost));
        }

        private static void AssertIcon(Image image, LegacySkill skill)
        {
            Sprite source = new LegacyDuelArt().GetSkillIcon(skill.IconId);
            Assert.That(image.sprite, Is.Not.Null, image.name);
            Assert.That(image.sprite.texture, Is.EqualTo(source.texture), image.name);
            Assert.That(image.sprite.name, Is.EqualTo(source.name), image.name + " shows " + skill.Name);
        }

        /// <summary>Only the given socket wears the selection ring (<paramref name="lane"/> null: none).</summary>
        private static void AssertSelectionRing(Fixture fixture, string lane, int slot)
        {
            foreach (string key in Keys)
                for (int index = 1; index <= 3; index++)
                    Assert.That(Named(Named(fixture.Hud.Root, "Loadout Slot " + key + " " + index).gameObject, "Slot Selection").gameObject.activeSelf,
                        Is.EqualTo(key == lane && index == slot), key + " socket " + index + " ring");
        }

        /// <summary>The sockets glowing as places for a dragged skill: every socket of <paramref name="eligibleLane"/> but
        /// <paramref name="except"/> (the socket it came from, "Q 1"), and no other.</summary>
        private static void AssertTargets(Fixture fixture, string eligibleLane, string except = null)
        {
            foreach (string lane in Keys)
                for (int slot = 1; slot <= 3; slot++)
                    Assert.That(Named(fixture.Hud.Root, "Loadout Slot Target " + lane + " " + slot).gameObject.activeSelf,
                        Is.EqualTo(lane == eligibleLane && lane + " " + slot != except), lane + " slot " + slot);
        }

        private static Rect WorldBounds(RectTransform rect, Transform relativeTo)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = relativeTo.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static Transform Named(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true)) if (candidate.name == name) return candidate;
            Assert.Fail("Missing loadout node: " + name);
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

        /// <summary>Exactly these lanes are drawn: each heading over its gear at the given x, the gear's three sockets round
        /// it in the lane's order (<see cref="LoadoutGear.SocketOffset"/>).</summary>
        private static void AssertColumns(Fixture fixture, string[] lanes, float[] xs)
        {
            int headings = 0, gears = 0;
            foreach (Transform candidate in fixture.Hud.Root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name.StartsWith("Loadout Heading ", StringComparison.Ordinal) && candidate.GetComponent<Text>() != null) headings++;
                if (candidate.name.StartsWith("Loadout Gear ", StringComparison.Ordinal)) gears++;
            }
            Assert.That(headings, Is.EqualTo(lanes.Length), "One heading per open lane.");
            Assert.That(gears, Is.EqualTo(lanes.Length), "One gear per open lane.");
            for (int index = 0; index < lanes.Length; index++)
            {
                Assert.That(Position(fixture.Hud.Root, "Loadout Heading " + lanes[index] + " Style Badge"),
                    Is.EqualTo(new Vector2(xs[index], 252f)), lanes[index] + " heading");
                Assert.That(Position(fixture.Hud.Root, "Loadout Gear " + lanes[index]),
                    Is.EqualTo(new Vector2(xs[index], CampaignLoadoutHud.GearCentreY)), lanes[index] + " gear");
                for (int slot = 1; slot <= 3; slot++)
                    Assert.That(Position(fixture.Hud.Root, "Loadout Slot " + lanes[index] + " " + slot),
                        Is.EqualTo(LoadoutGear.SocketOffset(slot - 1, 3)), lanes[index] + " socket " + slot);
            }
        }
    }
}
