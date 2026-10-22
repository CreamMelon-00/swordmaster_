using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The versus loadout (<see cref="VersusLoadoutHud"/>), as the campaign loadout since 2026-10-16: one gear per
    /// lane with a socket per slot (<see cref="LoadoutGear"/>, icon and ACT only, the first at the top and the rest the way
    /// the battle's gears turn), drag to place, swap and replace, click to select, the pool rack's lane filter that follows
    /// the selection, its two rows, the pool exclusion, and confirm, back and reset.</summary>
    public sealed class VersusLoadoutHudPlayModeTests
    {
        private static readonly string[] Keys = { "Q", "W", "E" };

        [UnityTest]
        public IEnumerator MultiplayerOnlyTechnique_CanBeInspectedAndEquipped()
        {
            yield return null;
            var host = new GameObject("Versus Exclusive Technique Test Host");
            var loadout = new VersusLoadout();
            int original = loadout.GetSkillId(2, 0);
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout))
            {
                hud.Show();
                GameObject source = Named(hud.Root, "Versus Available Skill 62");
                source.GetComponent<Button>().onClick.Invoke();
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(LegacySkillDefinitions.Skill(62).Name));
                Assert.That(loadout.GetSkillId(2, 0), Is.EqualTo(original), "Inspecting a technique does not equip it.");
                DragAndDrop(source, Named(hud.Root, "Versus Slot E 1"));
                Assert.That(loadout.GetSkillId(2, 0), Is.EqualTo(62));
                var match = new LocalVersusMatch(loadout.ToSkills(), new VersusLoadout().ToSkills());
                Assert.That(match.Left.GetLane(2)[0].Id, Is.EqualTo(62));
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator ClickingAvailableAndEquippedSkillsOnlyShowsTheirInformation()
        {
            yield return null;
            var host = new GameObject("Versus Click Information Test Host");
            var loadout = new VersusLoadout();
            int[] before = loadout.ExportIds();
            int changed = 0;
            LegacySkill qSkill = AvailableQSkill();
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout, changed: () => changed++))
            {
                hud.Show();
                AssertSelectionRing(hud.Root, null, 0);
                Button(hud.Root, "Versus Available Skill " + qSkill.Id).onClick.Invoke();
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(qSkill.Name));
                AssertSelectionRing(hud.Root, null, 0);
                AssertNoTargets(hud.Root);
                Assert.That(hud.IsDragging, Is.False);
                Button(hud.Root, "Versus Slot Q 1").onClick.Invoke();
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(loadout.GetSkill(0, 0).Name));
                AssertSelectionRing(hud.Root, "Q", 1);
                Button(hud.Root, "Versus Slot E 3").onClick.Invoke();
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(loadout.GetSkill(2, 2).Name));
                AssertSelectionRing(hud.Root, "E", 3);
                Assert.That(loadout.ExportIds(), Is.EqualTo(before));
                Assert.That(changed, Is.Zero);
                AssertNoTargets(hud.Root);
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator Preparation_DragCanReplaceSwapConfirmAndCancelToRestore()
        {
            yield return null;
            var host = new GameObject("Versus Loadout Test Host");
            int confirmed = 0, cancelled = 0, changed = 0;
            var loadout = new VersusLoadout();
            LegacySkill qSkill = AvailableQSkill();
            int originalQ = loadout.GetSkillId(0, 0);
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout,
                () => confirmed++, () => cancelled++, () => changed++))
            {
                // As the online room opens it (기술 편성).
                hud.Show();
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(Label(hud.Root, "Versus Loadout Heading").text, Is.EqualTo("대전 기술 편성"));
                Assert.That(hud.ConfirmButton.GetComponentInChildren<Text>().text, Is.EqualTo("편성 완료"));
                GameObject source = Named(hud.Root, "Versus Available Skill " + qSkill.Id);
                PointerEventData pointer = BeginDrag(source);
                Assert.That(hud.IsDragging, Is.True);
                AssertTargets(hud.Root, "Q");
                Assert.That(hud.Filter, Is.EqualTo(LoadoutRack.AllLanes), "A drag leaves the filter as it was…");
                Assert.That(source.activeInHierarchy, Is.True, "…and the card being dragged where it was.");
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(qSkill.Name));
                GameObject ghost = Named(hud.Root, "Versus Loadout Drag Ghost");
                Assert.That(ghost.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(ghost.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Vector2 beforePosition = ghost.GetComponent<RectTransform>().anchoredPosition;
                pointer.position += new Vector2(50f, 30f);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.dragHandler);
                Assert.That(ghost.GetComponent<RectTransform>().anchoredPosition, Is.Not.EqualTo(beforePosition));
                Drop(source, Named(hud.Root, "Versus Slot Q 1"), pointer);
                Assert.That(loadout.GetSkillId(0, 0), Is.EqualTo(qSkill.Id));
                Assert.That(changed, Is.EqualTo(1));
                Assert.That(hud.IsDragging, Is.False);
                AssertNoTargets(hud.Root);
                AssertSocketShows(Named(hud.Root, "Versus Slot Q 1"), qSkill);
                AssertSelectionRing(hud.Root, "Q", 1);
                Assert.That(ActiveNamed(hud.Root, "Versus Available Skill " + qSkill.Id), Is.Null,
                    "The placed skill leaves the rack immediately…");
                Assert.That(ActiveNamed(hud.Root, "Versus Available Skill " + originalQ), Is.Not.Null,
                    "…and the one it replaced comes back.");
                int qSecond = loadout.GetSkillId(0, 1);
                GameObject first = Named(hud.Root, "Versus Slot Q 1");
                pointer = BeginDrag(first);
                AssertTargets(hud.Root, "Q", "Q 1");
                Drop(first, Named(hud.Root, "Versus Slot Q 2"), pointer);
                Assert.That(loadout.GetSkillId(0, 0), Is.EqualTo(qSecond));
                Assert.That(loadout.GetSkillId(0, 1), Is.EqualTo(qSkill.Id));
                AssertSocketShows(Named(hud.Root, "Versus Slot Q 1"), LegacySkillDefinitions.Skill(qSecond));
                AssertSocketShows(Named(hud.Root, "Versus Slot Q 2"), qSkill);
                AssertSelectionRing(hud.Root, "Q", 2);
                Assert.That(changed, Is.EqualTo(2));
                DragAndDrop(Named(hud.Root, "Versus Slot Q 2"), Named(hud.Root, "Versus Slot Q 2"));
                Assert.That(changed, Is.EqualTo(2), "Dropping on the current slot does not publish a loadout change.");
                hud.ConfirmButton.onClick.Invoke();
                Assert.That(confirmed, Is.EqualTo(1));
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(loadout.ToSkills()[1].Id, Is.EqualTo(qSkill.Id));

                hud.Show();
                DragAndDrop(Named(hud.Root, "Versus Available Skill " + originalQ), Named(hud.Root, "Versus Slot Q 2"));
                Assert.That(loadout.GetSkillId(0, 1), Is.EqualTo(originalQ));
                Assert.That(changed, Is.EqualTo(3));
                hud.CancelButton.onClick.Invoke();
                Assert.That(cancelled, Is.EqualTo(1));
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(loadout.GetSkillId(0, 1), Is.EqualTo(qSkill.Id),
                    "Going back should restore the loadout shown when preparation opened.");
                Assert.That(changed, Is.EqualTo(4));
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator InvalidAndOutsideDropsLeaveTheDraftUnchangedAndHideClearsDrag()
        {
            yield return null;
            var host = new GameObject("Versus Cancel Drag Test Host");
            var loadout = new VersusLoadout();
            int[] before = loadout.ExportIds();
            int changed = 0;
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout, changed: () => changed++))
            {
                hud.Show();
                GameObject source = Named(hud.Root, "Versus Available Skill " + AvailableQSkill().Id);
                DragAndDrop(source, Named(hud.Root, "Versus Slot W 1"));
                Assert.That(hud.IsDragging, Is.False);
                Assert.That(loadout.ExportIds(), Is.EqualTo(before));
                AssertNoTargets(hud.Root);

                // Off the sockets onto the wheel: the wheel takes clicks, never drops.
                DragAndDrop(source, Named(hud.Root, "Versus Gear Q"));
                Assert.That(hud.IsDragging, Is.False);
                Assert.That(loadout.ExportIds(), Is.EqualTo(before));
                AssertNoTargets(hud.Root);

                PointerEventData pointer = BeginDrag(source);
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
                Assert.That(hud.IsDragging, Is.False);
                Assert.That(loadout.ExportIds(), Is.EqualTo(before));
                AssertNoTargets(hud.Root);

                pointer = new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Right, pointerDrag = source };
                ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
                Assert.That(hud.IsDragging, Is.False);

                pointer = BeginDrag(source);
                GameObject ghost = Named(hud.Root, "Versus Loadout Drag Ghost", onlyActive: true);
                hud.Hide();
                Assert.That(hud.IsDragging, Is.False);
                Assert.That(ghost.activeSelf, Is.False);
                AssertNoTargets(hud.Root);
                hud.Show();
                ExecuteEvents.Execute(Named(hud.Root, "Versus Slot Q 1"), pointer, ExecuteEvents.dropHandler);
                Assert.That(loadout.ExportIds(), Is.EqualTo(before), "A stale pointer cannot commit a hidden or cancelled drag.");
                Assert.That(changed, Is.Zero);
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator ResetConfirmAndCancelClearAnyUncommittedDrag()
        {
            yield return null;
            var host = new GameObject("Versus Footer Drag Cleanup Test Host");
            var loadout = new VersusLoadout();
            int[] before = loadout.ExportIds();
            int changed = 0;
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout, changed: () => changed++))
            {
                foreach (string action in new[] { "Versus Loadout Reset", "Versus Loadout Confirm", "Versus Loadout Cancel" })
                {
                    hud.Show();
                    BeginDrag(Named(hud.Root, "Versus Available Skill " + AvailableQSkill().Id));
                    GameObject ghost = Named(hud.Root, "Versus Loadout Drag Ghost", onlyActive: true);
                    Button(hud.Root, action).onClick.Invoke();
                    Assert.That(hud.IsDragging, Is.False);
                    Assert.That(ghost.activeSelf, Is.False);
                    AssertNoTargets(hud.Root);
                    Assert.That(loadout.ExportIds(), Is.EqualTo(before));
                }
                Assert.That(changed, Is.Zero);
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator Preparation_HasOneGearPerLane_SocketsShowIconAndActOnly_AndOneHintLine()
        {
            yield return null;
            var host = new GameObject("Versus Gear Layout Test Host");
            var loadout = new VersusLoadout();
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout))
            {
                hud.Show();
                Canvas.ForceUpdateCanvases();
                int gears = 0, sockets = 0;
                foreach (Transform node in hud.Root.GetComponentsInChildren<Transform>(true))
                {
                    if (node.name.StartsWith("Versus Gear ", StringComparison.Ordinal)) gears++;
                    if (node.name.StartsWith("Versus Slot ", StringComparison.Ordinal) && node.GetComponent<Button>() != null) sockets++;
                }
                Assert.That(gears, Is.EqualTo(3), "One gear per lane.");
                Assert.That(sockets, Is.EqualTo(VersusLoadout.SkillCount), "A socket per slot, nothing else named as one.");
                for (int lane = 0; lane < 3; lane++)
                {
                    GameObject gear = Named(hud.Root, "Versus Gear " + Keys[lane]);
                    Assert.That(gear.GetComponent<Image>().sprite, Is.SameAs(AcademyUiSkin.GearWheel), Keys[lane] + " wears the battle dock's wheel.");
                    Assert.That(gear.GetComponent<Button>(), Is.Not.Null, "The wheel itself is clickable…");
                    Assert.That(gear.GetComponent<Button>().transition, Is.EqualTo(Selectable.Transition.None),
                        "…but never lights up: a socket's hover would light the whole wheel.");
                    Assert.That(gear.GetComponent<LoadoutGearHitArea>().Radius, Is.EqualTo(LoadoutGear.HitRadius),
                        "Only the drawn wheel and the sockets take the pointer, not the picture's corners.");
                    Assert.That(gear.GetComponent<RectTransform>().anchoredPosition,
                        Is.EqualTo(new Vector2(VersusLoadoutHud.FirstLaneX + lane * VersusLoadoutHud.LanePitch, VersusLoadoutHud.GearCentreY)));
                    RectTransform badge = Named(hud.Root, "Versus Loadout Heading " + Keys[lane] + " Style Badge").GetComponent<RectTransform>();
                    Assert.That(badge.anchoredPosition.x, Is.EqualTo(gear.GetComponent<RectTransform>().anchoredPosition.x), "The lane's heading over its gear.");
                    Assert.That(WorldBounds(gear.GetComponent<RectTransform>(), hud.Root.transform).yMax,
                        Is.LessThan(WorldBounds(badge, hud.Root.transform).yMin), Keys[lane] + " gear under its heading.");
                    if (lane > 0)
                        Assert.That(WorldBounds(gear.GetComponent<RectTransform>(), hud.Root.transform).Overlaps(WorldBounds(
                            Named(hud.Root, "Versus Gear " + Keys[lane - 1]).GetComponent<RectTransform>(), hud.Root.transform)), Is.False,
                            "Neighbouring wheels do not overlap.");
                    Assert.That(WorldBounds(gear.GetComponent<RectTransform>(), hud.Root.transform).Overlaps(WorldBounds(
                        Named(hud.Root, "Versus Loadout Detail Border").GetComponent<RectTransform>(), hud.Root.transform)), Is.False,
                        Keys[lane] + " gear clear of the detail.");
                    Assert.That(WorldBounds(gear.GetComponent<RectTransform>(), hud.Root.transform).yMin,
                        Is.GreaterThan(WorldBounds(Named(hud.Root, "Versus Available Heading").GetComponent<RectTransform>(), hud.Root.transform).yMax),
                        Keys[lane] + " gear above the rack's heading row.");
                    for (int slot = 0; slot < 3; slot++)
                    {
                        GameObject socket = Named(hud.Root, "Versus Slot " + Keys[lane] + " " + (slot + 1));
                        Assert.That(socket.transform.parent, Is.SameAs(gear.transform), "Each socket sits on its lane's gear.");
                        Assert.That(socket.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(LoadoutGear.SocketOffset(slot, 3)));
                        AssertSocketShows(socket, loadout.GetSkill(lane, slot));
                        Assert.That(socket.GetComponentInChildren<DuelPanelTrim>(true), Is.Null,
                            socket.name + " is a picture with its own round silhouette, no rectangular trim.");
                    }
                }
                Assert.That(Named(hud.Root, "Versus Filter All").GetComponentInChildren<DuelPanelTrim>(true), Is.Not.Null,
                    "The filter's buttons are the theme's plain buttons.");

                // One hint line, the campaign loadout's; no 클릭·드래그 line, no placement hint and no scroll hint by the rack.
                Assert.That(Label(hud.Root, "Versus Loadout Subheading").text, Is.EqualTo(CampaignLobbyHud.LoadoutHint));
                Assert.That(TryNamed(hud.Root, "Versus Loadout Hint"), Is.Null);
                Assert.That(TryNamed(hud.Root, "Versus Available Scroll Hint"), Is.Null, "The rack reads as the campaign's: no 휠로 더 보기.");
                foreach (Text text in hud.Root.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(text.text, Does.Not.Contain("클릭:").And.Not.Contain("드래그:"), text.name);
                    Assert.That(text.text, Does.Not.Contain("놓아 배치").And.Not.Contain("더 보기"), text.name);
                }
                foreach (Graphic graphic in hud.Root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.GetComponent<Button>() == null && graphic.name != "Versus Available Viewport" &&
                        graphic.name != "Versus Loadout Backdrop")
                        Assert.That(graphic.raycastTarget, Is.False, "Decorations must not intercept card drags: " + graphic.name);
                foreach (Button button in hud.Root.GetComponentsInChildren<Button>(true))
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator PoolFilter_FollowsTheSelectedGear_CanBeClicked_AndNeverChangesTheLoadout()
        {
            yield return null;
            var host = new GameObject("Versus Filter Test Host");
            var loadout = new VersusLoadout();
            int[] before = loadout.ExportIds();
            int changed = 0;
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout, changed: () => changed++))
            {
                hud.Show();
                Assert.That(Label(Named(hud.Root, "Versus Filter All"), "Button Label").text, Is.EqualTo("전체"));
                for (int lane = 0; lane < 3; lane++)
                    Assert.That(Label(Named(hud.Root, "Versus Filter " + Keys[lane]), "Button Label").text, Is.EqualTo(Keys[lane]));
                float previous = Named(hud.Root, "Versus Filter All").GetComponent<RectTransform>().anchoredPosition.x;
                foreach (string key in Keys)
                {
                    float x = Named(hud.Root, "Versus Filter " + key).GetComponent<RectTransform>().anchoredPosition.x;
                    Assert.That(x, Is.GreaterThan(previous), "전체 · Q · W · E in order");
                    previous = x;
                }
                Rect filterBounds = WorldBounds(Named(hud.Root, "Versus Filter").GetComponent<RectTransform>(), hud.Root.transform);
                Assert.That(filterBounds.Overlaps(WorldBounds(Named(hud.Root, "Versus Available Heading").GetComponent<RectTransform>(),
                    hud.Root.transform)), Is.False, "The filter follows the rack's heading on its row.");
                Assert.That(filterBounds.yMin, Is.GreaterThanOrEqualTo(WorldBounds(Named(hud.Root, "Versus Available Collection")
                    .GetComponent<RectTransform>(), hud.Root.transform).yMax - 1f), "The filter sits above the rack.");
                AssertFilterShows(hud, loadout, LoadoutRack.AllLanes);

                // A socket's lane: the selection follows, the rack shows that lane only.
                Button(hud.Root, "Versus Slot W 2").onClick.Invoke();
                AssertFilterShows(hud, loadout, 1);
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(loadout.GetSkill(1, 1).Name));
                // The wheel off its sockets: that lane's rack, the selection unchanged.
                Button(hud.Root, "Versus Gear E").onClick.Invoke();
                AssertFilterShows(hud, loadout, 2);
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(loadout.GetSkill(1, 1).Name),
                    "The wheel only turns the rack to its lane.");
                AssertSelectionRing(hud.Root, "W", 2);
                // The filter row itself, both ways; a filter click leaves the selection.
                Button(hud.Root, "Versus Filter All").onClick.Invoke();
                AssertFilterShows(hud, loadout, LoadoutRack.AllLanes);
                Button(hud.Root, "Versus Filter Q").onClick.Invoke();
                AssertFilterShows(hud, loadout, 0);
                AssertSelectionRing(hud.Root, "W", 2);
                // A skill picked from outside the shown lane (by code) brings its lane up; one inside keeps it.
                LegacySkill wSkill = Pool().First(skill => skill.LaneIndex == 1 && !loadout.IsEquipped(skill.Id));
                Button(hud.Root, "Versus Available Skill " + wSkill.Id).onClick.Invoke();
                AssertFilterShows(hud, loadout, 1);
                Button(hud.Root, "Versus Filter All").onClick.Invoke();
                LegacySkill eSkill = Pool().First(skill => skill.LaneIndex == 2 && !loadout.IsEquipped(skill.Id));
                Button(hud.Root, "Versus Available Skill " + eSkill.Id).onClick.Invoke();
                AssertFilterShows(hud, loadout, LoadoutRack.AllLanes);
                // Reordering a lane, one socket dragged onto another, selects the socket and turns the rack to that lane, as a
                // click there would (no rack card is under the pointer).
                int e1 = loadout.GetSkillId(2, 0), e3 = loadout.GetSkillId(2, 2);
                GameObject socket = Named(hud.Root, "Versus Slot E 1");
                PointerEventData pointer = BeginDrag(socket);
                Assert.That(hud.IsDragging, Is.True);
                Assert.That(hud.Filter, Is.EqualTo(2), "Taking E 1's skill turns the rack to E at once.");
                Drop(socket, Named(hud.Root, "Versus Slot E 3"), pointer);
                Assert.That(loadout.GetSkillId(2, 2), Is.EqualTo(e1));
                Assert.That(loadout.GetSkillId(2, 0), Is.EqualTo(e3));
                Assert.That(changed, Is.EqualTo(1));
                AssertFilterShows(hud, loadout, 2);
                AssertSelectionRing(hud.Root, "E", 3);
                // Under a lane's filter a drag still places, and the filter stays.
                Button(hud.Root, "Versus Filter Q").onClick.Invoke();
                LegacySkill qSkill = AvailableQSkill();
                DragAndDrop(Named(hud.Root, "Versus Available Skill " + qSkill.Id), Named(hud.Root, "Versus Slot Q 3"));
                Assert.That(loadout.GetSkillId(0, 2), Is.EqualTo(qSkill.Id));
                Assert.That(changed, Is.EqualTo(2));
                AssertFilterShows(hud, loadout, 0);
                // Each preparation starts on 전체.
                hud.CancelButton.onClick.Invoke();
                Assert.That(loadout.ExportIds(), Is.EqualTo(before));
                hud.Show();
                AssertFilterShows(hud, loadout, LoadoutRack.AllLanes);
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo("기술을 선택하세요"));
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator PoolRack_FillsTwoRowsColumnByColumn_ScrollsSideways_AndKeepsTheExclusion()
        {
            yield return null;
            var host = new GameObject("Versus Rack Test Host");
            var loadout = new VersusLoadout();
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout))
            {
                hud.Show();
                Canvas.ForceUpdateCanvases();
                List<LegacySkill> unplaced = Pool().Where(skill => !loadout.IsEquipped(skill.Id)).ToList();
                Assert.That(unplaced.Count, Is.GreaterThan(4), "Enough skills to fill more than one column.");
                var cards = new List<RectTransform>();
                foreach (LegacySkill skill in unplaced)
                    cards.Add(ActiveNamed(hud.Root, "Versus Available Skill " + skill.Id).GetComponent<RectTransform>());
                Assert.That(ActiveAvailableCount(hud.Root), Is.EqualTo(unplaced.Count), "Placed skills stay out of the rack.");
                for (int index = 0; index < cards.Count; index++)
                    Assert.That(cards[index].anchoredPosition, Is.EqualTo(LoadoutRack.CardPosition(index)), "Card " + index);
                Assert.That(cards[0].anchoredPosition.x, Is.EqualTo(cards[1].anchoredPosition.x), "A column holds two cards…");
                Assert.That(cards[0].anchoredPosition.y, Is.GreaterThan(cards[1].anchoredPosition.y), "…the first on the top row…");
                Assert.That(cards[2].anchoredPosition.y, Is.EqualTo(cards[0].anchoredPosition.y), "…and the next column starts on top again.");
                Assert.That(cards[2].anchoredPosition.x, Is.GreaterThan(cards[0].anchoredPosition.x));
                var rows = new HashSet<float>();
                foreach (RectTransform card in cards) rows.Add(card.anchoredPosition.y);
                Assert.That(rows.Count, Is.EqualTo(LoadoutRack.Rows), "Two rows.");
                RectTransform viewport = Named(hud.Root, "Versus Available Viewport").GetComponent<RectTransform>();
                Rect window = WorldBounds(viewport, hud.Root.transform);
                foreach (RectTransform card in cards)
                {
                    Rect bounds = WorldBounds(card, hud.Root.transform);
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(window.yMax + .5f), card.name + " inside the rack");
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(window.yMin - .5f), card.name + " inside the rack");
                }
                Assert.That(WorldBounds(cards[0], hud.Root.transform).Overlaps(WorldBounds(cards[1], hud.Root.transform)), Is.False,
                    "The rows do not touch.");
                for (int index = 2; index < cards.Count; index++)
                    Assert.That(WorldBounds(cards[index], hud.Root.transform).Overlaps(WorldBounds(cards[index - 2], hud.Root.transform)), Is.False);

                // Each card keeps its icon with its core mark, its name, ACT and lane, and a click only inspects it.
                GameObject first = cards[0].gameObject;
                LegacySkill firstSkill = unplaced[0];
                Assert.That(Label(first, "Available Name").text, Is.EqualTo(firstSkill.Name));
                Assert.That(Label(first, "Available Cost").text, Is.EqualTo("ACT " + firstSkill.Cost));
                Assert.That(Label(first, "Available Lane").text, Is.EqualTo(Keys[firstSkill.LaneIndex]));
                Assert.That(first.transform.Find("Role Pip"), Is.Not.Null);
                AssertIcon(first.transform.Find("Available Icon").GetComponent<Image>(), firstSkill);
                Assert.That(cards[0].sizeDelta, Is.EqualTo(new Vector2(LoadoutRack.CardWidth, LoadoutRack.CardHeight)));

                // The pool exclusion: 에스키브 (54) has no card at all.
                foreach (int excluded in VersusLoadout.ExcludedSkillIds)
                    Assert.That(TryNamed(hud.Root, "Versus Available Skill " + excluded), Is.Null, "Excluded skill " + excluded);

                var scroll = Named(hud.Root, "Versus Available Collection").GetComponent<ScrollRect>();
                Assert.That(scroll.horizontal, Is.True, "Overflowing skills scroll sideways…");
                Assert.That(scroll.vertical, Is.False, "…never up and down.");
                RectTransform content = Named(hud.Root, "Versus Available Content").GetComponent<RectTransform>();
                Assert.That(content.sizeDelta.x, Is.EqualTo(LoadoutRack.ContentWidth(unplaced.Count, VersusLoadoutHud.RackWidth)));
                Assert.That(content.sizeDelta.y, Is.EqualTo(LoadoutRack.Height));
                Assert.That(viewport.rect.width, Is.EqualTo(VersusLoadoutHud.RackWidth));

                // The whole pool is wider than the window: scrolled along, a click, a drag and a placement leave the rack
                // where it is, so nothing shifts under the pointer (a placement swaps one card for another).
                Assert.That(content.sizeDelta.x, Is.GreaterThan(VersusLoadoutHud.RackWidth), "The whole pool overflows the window.");
                content.anchoredPosition = new Vector2(-150f, 0f);
                LegacySkill inView = unplaced[2];
                Button(hud.Root, "Versus Available Skill " + inView.Id).onClick.Invoke();
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(inView.Name));
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-150f), "A click only inspects: the rack stays.");
                GameObject source = Named(hud.Root, "Versus Available Skill " + inView.Id);
                PointerEventData pointer = BeginDrag(source);
                Assert.That(hud.IsDragging, Is.True);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-150f), "A drag leaves the rack under the pointer as it was…");
                int replaced = loadout.GetSkillId(inView.LaneIndex, 0);
                Drop(source, Named(hud.Root, "Versus Slot " + Keys[inView.LaneIndex] + " 1"), pointer);
                Assert.That(loadout.GetSkillId(inView.LaneIndex, 0), Is.EqualTo(inView.Id));
                Assert.That(ActiveNamed(hud.Root, "Versus Available Skill " + replaced), Is.Not.Null, "The replaced skill is back in the rack…");
                Assert.That(ActiveAvailableCount(hud.Root), Is.EqualTo(unplaced.Count), "…as many cards as before…");
                Assert.That(content.anchoredPosition.x, Is.EqualTo(-150f), "…and the rack still where it was.");
                Assert.That(hud.Filter, Is.EqualTo(LoadoutRack.AllLanes));

                // A lane's skills fit the window, so its filter shows them from the left end, closed up.
                Button(hud.Root, "Versus Filter W").onClick.Invoke();
                List<LegacySkill> wUnplaced = Pool().Where(skill => skill.LaneIndex == 1 && !loadout.IsEquipped(skill.Id)).ToList();
                Assert.That(ActiveAvailableCount(hud.Root), Is.EqualTo(wUnplaced.Count));
                Assert.That(content.sizeDelta.x, Is.EqualTo(VersusLoadoutHud.RackWidth), "W's skills fit the window.");
                Assert.That(content.anchoredPosition.x, Is.EqualTo(0f));
                for (int index = 0; index < wUnplaced.Count; index++)
                    Assert.That(ActiveNamed(hud.Root, "Versus Available Skill " + wUnplaced[index].Id).GetComponent<RectTransform>().anchoredPosition,
                        Is.EqualTo(LoadoutRack.CardPosition(index)), "The lane's skills close up from the start.");
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator GearOrder_TopIsFirst_AndTheMatchTakesEachLaneInSocketOrder()
        {
            yield return null;
            var host = new GameObject("Versus Gear Order Test Host");
            var loadout = new VersusLoadout();
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout))
            {
                hud.Show();
                Canvas.ForceUpdateCanvases();
                for (int lane = 0; lane < 3; lane++)
                {
                    string key = Keys[lane];
                    RectTransform gear = Named(hud.Root, "Versus Gear " + key).GetComponent<RectTransform>();
                    var at = new Vector2[3];
                    for (int slot = 0; slot < 3; slot++)
                    {
                        RectTransform socket = Named(hud.Root, "Versus Slot " + key + " " + (slot + 1)).GetComponent<RectTransform>();
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
                    LoadoutGearArc arrow = gear.Find("Turn Arrow").GetComponent<LoadoutGearArc>();
                    Assert.That(arrow, Is.Not.Null, key + " shows which way it turns.");
                    Assert.That(arrow.Head, Is.GreaterThan(0f));
                    Assert.That(arrow.Direction, Is.EqualTo(LegacySkillGear.TurnDirection), "…turning the battle gears' way: clockwise.");
                    Assert.That(arrow.Tip.x, Is.GreaterThan(0f), "It points where the top skill goes once used.");
                    Assert.That(arrow.raycastTarget, Is.False);
                }

                // The match takes each lane in the sockets' order (VersusLoadout.ToSkills), also after a reorder here, and
                // reserving the reordered lane brings its second socket's skill to the front, as the battle's gear turns it
                // to the top.
                int w1 = loadout.GetSkillId(1, 0), w2 = loadout.GetSkillId(1, 1), w3 = loadout.GetSkillId(1, 2);
                DragAndDrop(Named(hud.Root, "Versus Slot W 1"), Named(hud.Root, "Versus Slot W 3"));
                Assert.That(loadout.GetSkillId(1, 0), Is.EqualTo(w3), "The reorder took: W 1 now holds W 3's skill…");
                Assert.That(loadout.GetSkillId(1, 1), Is.EqualTo(w2));
                Assert.That(loadout.GetSkillId(1, 2), Is.EqualTo(w1), "…and W 3 holds W 1's.");
                AssertSocketShows(Named(hud.Root, "Versus Slot W 1"), LegacySkillDefinitions.Skill(w3));
                var match = new LocalVersusMatch(loadout.ToSkills(), new VersusLoadout().ToSkills());
                for (int lane = 0; lane < 3; lane++)
                    for (int slot = 0; slot < 3; slot++)
                        Assert.That(match.Left.GetLane(lane)[slot].Id, Is.EqualTo(loadout.GetSkillId(lane, slot)),
                            Keys[lane] + " socket " + (slot + 1) + " is the battle lane's skill " + (slot + 1) + ".");
                Assert.That(match.Left.GetLane(1)[0].Id, Is.EqualTo(w3), "The battle's W starts on the skill now at the top.");
                Assert.That(match.TryQueueLane(0, 1), Is.True);
                Assert.That(match.Left.GetLane(1)[0].Id, Is.EqualTo(w2), "Reserving W brings socket 2's skill to the front.");
            }
            Object.Destroy(host);
        }

        private static LegacySkill AvailableQSkill() => LegacySkillDefinitions.AcquisitionSkills
            .First(skill => skill.LaneIndex == 0 && !VersusLoadout.IsExcluded(skill.Id));

        /// <summary>The versus pool in rack order, including multiplayer-only techniques.</summary>
        private static IEnumerable<LegacySkill> Pool()
            => LegacySkillDefinitions.InitialSkills.Concat(
                LegacySkillDefinitions.AcquisitionSkills).Concat(LegacySkillDefinitions.VersusSkills)
                .Where(skill => !VersusLoadout.IsExcluded(skill.Id) && LegacySkillDefinitions.Find(skill)?.IsCounter != true);

        private static PointerEventData BeginDrag(GameObject source)
        {
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -1,
                pointerDrag = source,
                position = RectTransformUtility.WorldToScreenPoint(null, source.transform.position)
            };
            ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
            return pointer;
        }

        private static void DragAndDrop(GameObject source, GameObject target) => Drop(source, target, BeginDrag(source));

        private static void Drop(GameObject source, GameObject target, PointerEventData pointer)
        {
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
        }

        private static void AssertNoTargets(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Versus Slot Target ", StringComparison.Ordinal))
                    Assert.That(child.gameObject.activeSelf, Is.False, child.name);
        }

        /// <summary>The sockets glowing as places for a dragged skill: every socket of <paramref name="eligibleLane"/> but
        /// <paramref name="except"/> (the socket it came from, "Q 1"), and no other.</summary>
        private static void AssertTargets(GameObject root, string eligibleLane, string except = null)
        {
            foreach (string lane in Keys)
                for (int slot = 1; slot <= 3; slot++)
                    Assert.That(Named(root, "Versus Slot Target " + lane + " " + slot).activeSelf,
                        Is.EqualTo(lane == eligibleLane && lane + " " + slot != except), lane + " slot " + slot);
        }

        /// <summary>Only the given socket wears the selection ring (<paramref name="lane"/> null: none).</summary>
        private static void AssertSelectionRing(GameObject root, string lane, int slot)
        {
            foreach (string key in Keys)
                for (int index = 1; index <= 3; index++)
                    Assert.That(Named(Named(root, "Versus Slot " + key + " " + index), "Slot Selection").activeSelf,
                        Is.EqualTo(key == lane && index == slot), key + " socket " + index + " ring");
        }

        // A socket shows only its skill's icon and ACT cost: its one text is the cost; no name, order or core mark.
        private static void AssertSocketShows(GameObject socket, LegacySkill skill)
        {
            Text[] texts = socket.GetComponentsInChildren<Text>(true);
            Assert.That(texts.Length, Is.EqualTo(1), socket.name + ": its ACT is its only text.");
            Assert.That(texts[0].name, Is.EqualTo("Slot ACT"));
            Assert.That(texts[0].text, Is.EqualTo("ACT " + skill.Cost));
            foreach (string gone in new[] { "Slot Name", "Slot Order", "Role Pip" })
                Assert.That(socket.transform.Find(gone), Is.Null, socket.name + " shows no " + gone);
            var ring = (Image)socket.GetComponent<Button>().targetGraphic;
            Assert.That(ring.sprite, Is.Not.Null, socket.name + " wears the battle's socket.");
            Assert.That(ring.sprite.texture.name, Is.EqualTo("Skill Gear Socket"));
            Assert.That(socket.transform.Find("Slot ACT Plate").gameObject.activeSelf, Is.True);
            Image icon = socket.transform.Find("Slot Icon").GetComponent<Image>();
            Assert.That(icon.enabled, Is.True, socket.name + " shows its icon.");
            AssertIcon(icon, skill);
        }

        private static void AssertIcon(Image image, LegacySkill skill)
        {
            Sprite source = new LegacyDuelArt().GetSkillIcon(skill.IconId);
            Assert.That(image.sprite, Is.Not.Null, image.name);
            Assert.That(image.sprite.texture, Is.EqualTo(source.texture), image.name);
            Assert.That(image.sprite.name, Is.EqualTo(source.name), image.name + " shows " + skill.Name);
        }

        /// <summary>The rack shows <paramref name="filter"/>'s unplaced pool skills only, the filter row marks that choice,
        /// and every gear stays.</summary>
        private static void AssertFilterShows(VersusLoadoutHud hud, VersusLoadout loadout, int filter)
        {
            Assert.That(hud.Filter, Is.EqualTo(filter));
            int expectedCount = 0;
            foreach (LegacySkill skill in Pool())
            {
                bool expected = !loadout.IsEquipped(skill.Id) && (filter == LoadoutRack.AllLanes || skill.LaneIndex == filter);
                if (expected) expectedCount++;
                Assert.That(ActiveNamed(hud.Root, "Versus Available Skill " + skill.Id) != null, Is.EqualTo(expected),
                    skill.Name + " under filter " + filter);
            }
            Assert.That(ActiveAvailableCount(hud.Root), Is.EqualTo(expectedCount), "Shown under filter " + filter);
            Assert.That(Named(hud.Root, "Versus Filter All").GetComponent<Image>().color,
                Is.EqualTo(filter == LoadoutRack.AllLanes ? DuelVisualTheme.Accent : DuelVisualTheme.Card));
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.That(Named(hud.Root, "Versus Filter " + Keys[lane]).GetComponent<Image>().color,
                    Is.EqualTo(filter == lane ? DuelVisualTheme.Accent : DuelVisualTheme.Card), Keys[lane] + " filter marks the choice");
                Assert.That(Named(hud.Root, "Versus Gear " + Keys[lane]).activeInHierarchy, Is.True,
                    "The filter narrows the rack only: every gear stays.");
            }
        }

        private static int ActiveAvailableCount(GameObject root)
        {
            int count = 0;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name.StartsWith("Versus Available Skill ", StringComparison.Ordinal) && button.gameObject.activeInHierarchy) count++;
            return count;
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

        private static GameObject Named(GameObject root, string name, bool onlyActive = false)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name && (!onlyActive || child.gameObject.activeSelf)) return child.gameObject;
            Assert.Fail("Missing UI object: " + name);
            return null;
        }

        private static GameObject TryNamed(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }

        private static GameObject ActiveNamed(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name && child.gameObject.activeInHierarchy) return child.gameObject;
            return null;
        }

        private static Text Label(GameObject root, string name)
        {
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
                if (label.name == name) return label;
            Assert.Fail("Missing UI label: " + name);
            return null;
        }

        private static Button Button(GameObject root, string name)
            => Named(root, name).GetComponent<Button>();
    }
}
