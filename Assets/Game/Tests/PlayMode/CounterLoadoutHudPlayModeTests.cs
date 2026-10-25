using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The optional counter lives outside Q/W/E, uses the shared skill detail and draft save/cancel, and cannot
    /// enter a gear through a rack drag.</summary>
    public sealed class CounterLoadoutHudPlayModeTests
    {
        private const int PracticeCounter = 600;

        [Test]
        public void CounterAcquisitionCopy_NamesItsOwnSlotEvenIfItsAuthoredLaneIsClosed()
        {
            LegacySkill counter = LegacySkillDefinitions.Skill(PracticeCounter);
            Assert.That(CampaignCurriculumHud.SkillLabel(counter), Is.EqualTo("연습 반격(반격)"));
            var node = new CurriculumNode("counter-label", null, CurriculumBranch.Foundation, 0, 0, new[] { PracticeCounter });
            Assert.That(CampaignCurriculumHud.LearnedDetail(null, node, counter, null, false),
                Is.EqualTo("편성의 반격 슬롯에 장착할 수 있습니다."));
            Assert.That(AcademyFieldView.PickupNotice(counter, true, false),
                Does.Contain("(반격)").And.Contain("반격 슬롯").And.Not.Contain("해금"));
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject CanvasRoot;
            public readonly RectTransform Panel;
            public readonly CampaignRun Run = new CampaignRun();
            public readonly CampaignLoadoutHud.ViewState State = new CampaignLoadoutHud.ViewState();
            public CampaignLoadoutHud Hud;
            public int CounterCalls;
            private readonly bool useCallback;

            public Fixture(bool useCallback = true)
            {
                this.useCallback = useCallback;
                CanvasRoot = new GameObject("Counter Loadout Canvas", typeof(RectTransform), typeof(Canvas));
                CanvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Panel = new GameObject("Counter Loadout Panel", typeof(RectTransform)).GetComponent<RectTransform>();
                Panel.SetParent(CanvasRoot.transform, false);
                Panel.sizeDelta = new Vector2(1300f, 850f);
                Build();
            }

            public void Build()
            {
                Hud?.Dispose();
                Action<int> counter = useCallback ? (Action<int>)(id =>
                {
                    CounterCalls++;
                    Assert.That(Run.TryEquipCounter(id), Is.True);
                }) : null;
                Hud = new CampaignLoadoutHud(Panel, new LegacyDuelArt(), Run,
                    (id, lane, slot) => Assert.That(Run.TryPlaceLoadoutSkill(id, lane, slot), Is.True),
                    id => Assert.That(Run.TryUnequipSkill(id), Is.True),
                    () => Assert.That(Run.TrySaveLoadout(), Is.True),
                    () => Assert.That(Run.TryResetLoadout(), Is.True), State, counter);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose() { Hud?.Dispose(); Object.Destroy(CanvasRoot); }
        }

        [UnityTest]
        public IEnumerator CounterSlot_StartsEmpty_AndCounterDoesNotAppearInNormalRack()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Run.DraftCounterSkill, Is.Null);
                Assert.That(fixture.Run.EquippedCounterSkill, Is.Null);
                Assert.That(Text(fixture, "Loadout Counter Slot", true), Is.EqualTo("반격 · 미장착"));
                Assert.That(Find(fixture.Hud.Root, "Loadout Owned Skill " + PracticeCounter), Is.Null);
                Assert.That(fixture.State.CounterMode, Is.False);
                foreach (string key in new[] { "Q", "W", "E" })
                    Assert.That(Find(fixture.Hud.Root, "Loadout Gear " + key), Is.Not.Null);

                Click(fixture, "Loadout Counter Slot");
                Assert.That(fixture.State.CounterMode, Is.True);
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False, "Selecting the slot does not equip anything.");
                Assert.That(Text(fixture, "Loadout Owned Heading"), Is.EqualTo("보유 반격"));
                Transform card = Find(fixture.Hud.Root, "Loadout Owned Skill " + PracticeCounter);
                Assert.That(card, Is.Not.Null);
                Assert.That(card.GetComponent<CampaignLoadoutCardDrag>(), Is.Null, "Automatic counters cannot be dragged into Q/W/E.");
                Assert.That(Text(fixture, "Loadout Detail Name"), Is.EqualTo("미장착"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChoosingCounter_ShowsTriggerAndFreeAct_ThenExplicitActionSavesAndUnequips()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Click(fixture, "Loadout Counter Slot");
                Click(fixture, "Loadout Owned Skill " + PracticeCounter);
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
                Assert.That(Text(fixture, "Loadout Detail Name"), Is.EqualTo("연습 반격"));
                Assert.That(Text(fixture, "ACT Value"), Is.EqualTo("0"));
                Assert.That(Text(fixture, "Loadout Detail Effect"), Is.EqualTo("추가 효과 없음"));
                Assert.That(Text(fixture, "Loadout Counter Action", true), Is.EqualTo("반격 장착"));

                Click(fixture, "Loadout Counter Action");
                Assert.That(fixture.CounterCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.DraftCounterSkill?.Id, Is.EqualTo(PracticeCounter));
                Assert.That(fixture.Run.EquippedCounterSkill, Is.Null, "The slot participates in the existing draft.");
                Assert.That(Text(fixture, "Loadout Counter Slot", true), Is.EqualTo("반격 · 연습 반격 · 턴당 1회"));
                Click(fixture, "Loadout Save");
                Assert.That(fixture.Run.EquippedCounterSkill?.Id, Is.EqualTo(PracticeCounter));
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False);

                Click(fixture, "Loadout Counter Action");
                Assert.That(fixture.Run.DraftCounterSkill, Is.Null);
                Assert.That(fixture.Run.EquippedCounterSkill?.Id, Is.EqualTo(PracticeCounter));
                Click(fixture, "Loadout Save");
                Assert.That(fixture.Run.EquippedCounterSkill, Is.Null);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CancelRestoresCounterAndNormalSlots_WhileCounterSelectionSurvivesRebuild()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                int firstQ = fixture.Run.GetLoadoutSlot(0, 0).SkillId;
                int selectedQ = fixture.Run.GetLoadoutSlot(0, 1).SkillId;
                Click(fixture, "Loadout Slot Q 2");
                Click(fixture, "Loadout Counter Slot");
                Click(fixture, "Loadout Owned Skill " + PracticeCounter);
                Click(fixture, "Loadout Counter Action");
                Assert.That(fixture.Run.TryUnequipSkill(firstQ), Is.True);
                fixture.Hud.Refresh();
                fixture.Build();
                Assert.That(fixture.State.CounterMode, Is.True);
                Assert.That(fixture.State.SelectedCounterSkillId, Is.EqualTo(PracticeCounter));
                Assert.That(Text(fixture, "Loadout Detail Name"), Is.EqualTo("연습 반격"));
                Click(fixture, "Loadout Cancel");
                Assert.That(fixture.Run.DraftCounterSkill, Is.Null);
                Assert.That(fixture.Run.GetLoadoutSlot(0, 0)?.SkillId, Is.EqualTo(firstQ));
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
                Click(fixture, "Loadout Normal Skills");
                Assert.That(fixture.State.CounterMode, Is.False);
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(selectedQ));
                Assert.That(fixture.State.SelectedSlot, Is.EqualTo(1));
                Assert.That(Text(fixture, "Loadout Owned Heading"), Is.EqualTo("보유 기술"));
                Assert.That(Find(fixture.Hud.Root, "Loadout Owned Skill " + PracticeCounter), Is.Null);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CounterActionWithoutCallback_UsesCampaignDraft_AndGearClickReturnsToNormalSkills()
        {
            yield return null;
            using (var fixture = new Fixture(false))
            {
                Click(fixture, "Loadout Counter Slot");
                Click(fixture, "Loadout Owned Skill " + PracticeCounter);
                Click(fixture, "Loadout Counter Action");
                Assert.That(fixture.Run.DraftCounterSkill?.Id, Is.EqualTo(PracticeCounter));
                Click(fixture, "Loadout Gear W");
                Assert.That(fixture.State.CounterMode, Is.False);
                Assert.That(fixture.State.OwnedFilter, Is.EqualTo(1));
                Assert.That(fixture.Run.DraftCounterSkill?.Id, Is.EqualTo(PracticeCounter));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CounterSlot_FitsBetweenExistingGearsAndOwnedHeading_OutsideSharedDetail()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var slot = Find(fixture.Hud.Root, "Loadout Counter Slot").GetComponent<RectTransform>();
                var q = Find(fixture.Hud.Root, "Loadout Gear Q").GetComponent<RectTransform>();
                var detail = Find(fixture.Hud.Root, "Loadout Selected Detail").GetComponent<RectTransform>();
                var heading = Find(fixture.Hud.Root, "Loadout Owned Heading").GetComponent<RectTransform>();
                Assert.That(slot.anchoredPosition.y + slot.rect.height * .5f,
                    Is.LessThan(q.anchoredPosition.y - q.rect.height * .5f));
                Assert.That(slot.anchoredPosition.y - slot.rect.height * .5f,
                    Is.GreaterThan(heading.anchoredPosition.y + heading.rect.height * .5f));
                Assert.That(slot.anchoredPosition.x + slot.rect.width * .5f,
                    Is.LessThan(detail.anchoredPosition.x - detail.rect.width * .5f));
                PanelFits(slot, fixture.Panel);
            }
            yield return null;
        }

        private static void PanelFits(RectTransform child, RectTransform parent)
        {
            Assert.That(Mathf.Abs(child.anchoredPosition.x) + child.rect.width * .5f, Is.LessThan(parent.rect.width * .5f));
            Assert.That(Mathf.Abs(child.anchoredPosition.y) + child.rect.height * .5f, Is.LessThan(parent.rect.height * .5f));
        }

        private static Transform Find(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name && child.gameObject.activeInHierarchy) return child;
            return null;
        }

        private static void Click(Fixture fixture, string name)
        {
            Transform found = Find(fixture.Hud.Root, name);
            Assert.That(found, Is.Not.Null, name);
            var button = found.GetComponent<Button>();
            Assert.That(button.interactable, Is.True, name);
            button.onClick.Invoke();
        }

        private static string Text(Fixture fixture, string name, bool buttonLabel = false)
        {
            Transform found = Find(fixture.Hud.Root, name);
            Assert.That(found, Is.Not.Null, name);
            return buttonLabel ? found.GetComponentInChildren<Text>().text : found.GetComponent<Text>().text;
        }
    }
}
