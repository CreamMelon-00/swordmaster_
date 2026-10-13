using System;
using System.Collections;
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
    public sealed class VersusLoadoutHudPlayModeTests
    {
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
                Button(hud.Root, "Versus Available Skill " + qSkill.Id).onClick.Invoke();
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(qSkill.Name));
                AssertNoTargets(hud.Root);
                Assert.That(hud.IsDragging, Is.False);
                Button(hud.Root, "Versus Slot Q 1").onClick.Invoke();
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(loadout.GetSkill(0, 0).Name));
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
                Assert.That(Named(hud.Root, "Versus Slot Target Q 1").activeSelf, Is.True);
                Assert.That(Named(hud.Root, "Versus Slot Target W 1").activeSelf, Is.False);
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
                int qSecond = loadout.GetSkillId(0, 1);
                DragAndDrop(Named(hud.Root, "Versus Slot Q 1"), Named(hud.Root, "Versus Slot Q 2"));
                Assert.That(loadout.GetSkillId(0, 0), Is.EqualTo(qSecond));
                Assert.That(loadout.GetSkillId(0, 1), Is.EqualTo(qSkill.Id));
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

        private static LegacySkill AvailableQSkill() => LegacySkillDefinitions.AcquisitionSkills
            .First(skill => skill.LaneIndex == 0 && !VersusLoadout.IsExcluded(skill.Id));

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

        private static GameObject Named(GameObject root, string name, bool onlyActive = false)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name && (!onlyActive || child.gameObject.activeSelf)) return child.gameObject;
            Assert.Fail("Missing UI object: " + name);
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
