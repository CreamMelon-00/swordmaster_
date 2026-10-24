using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class HiddenQueueHudPlayModeTests
    {
        private static readonly int[] Reservations = { 0, 1, 0, 1, 2, 0, 1, 2, 2 };
        private static readonly FieldInfo FragmentSprite = typeof(DuelQueueShatterGraphic)
            .GetField("sprite", BindingFlags.NonPublic | BindingFlags.Instance);

        [UnityTest]
        public IEnumerator OpponentCards_KeepOnlyProperty_AndTheSameHudRestoresOwnAndSinglePlayerArt()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LocalVersusMatch match = NewMatch();
                QueueBoth(match, commitLast: false);
                var host = new LocalVersusPerspective(match, 0);
                var guest = new LocalVersusPerspective(match, 1);
                fixture.Refresh(host);
                AssertOwnArt(fixture, host);
                AssertOpponentMask(fixture, host);
                Assert.That(Property(fixture.Card(false, 2)).text, Is.EqualTo("참격"));
                Assert.That(Property(fixture.Card(false, 3)).text, Is.EqualTo("수비"));
                Assert.That(Property(fixture.Card(false, 4)).text, Is.EqualTo("타격"));
                Assert.That(Property(fixture.Card(false, 5)).text, Is.EqualTo("관통"));
                Assert.That(fixture.Card(false, 2).GetComponent<Image>().color,
                    Is.EqualTo(fixture.Card(false, 8).GetComponent<Image>().color),
                    "Different lane dyes and pictures must collapse to the same property look.");

                int nodes = fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length;
                for (int frame = 0; frame < 3; frame++) fixture.Refresh(host);
                Assert.That(fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes),
                    "A refresh reuses the hidden labels instead of adding decorations each frame.");

                fixture.Refresh(guest);
                AssertOwnArt(fixture, guest);
                AssertOpponentMask(fixture, guest);

                var single = new LegacyQueuedDuel(10000, 10000, 10000, 10000, Skills(9300), Skills(9400), new[] { 9 }, 1);
                foreach (int lane in Reservations) Assert.That(single.TryQueueLane(lane), Is.True);
                fixture.Refresh(single);
                AssertOwnArt(fixture, single);
                for (int slot = 0; slot < single.EnemyQueue.Count; slot++)
                    AssertRevealed(fixture, false, slot, single.EnemyQueue[slot]);
            }
        }

        [UnityTest]
        public IEnumerator StartingASlot_RevealsItsPictureBeforeTheFirstHit_AndANewTurnHidesTheTailAgain()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LocalVersusMatch match = NewMatch();
                QueueBoth(match, commitLast: true);
                var view = new LocalVersusPerspective(match, 0);
                fixture.Refresh(view);
                AssertHidden(fixture, 2);
                match.ResolveNextSlot();
                match.ResolveNextSlot();
                LocalVersusSlot third = match.BeginNextSlot();
                Assert.That(third.SlotIndex, Is.EqualTo(2));
                Assert.That(third.HitsResolved, Is.Zero);
                fixture.Refresh(view);
                AssertRevealed(fixture, false, 2, view.EnemyQueue[2]);
                AssertHidden(fixture, 3);

                while (!match.IsTurnResolved) match.ResolveNextSlot();
                match.BeginNextTurn();
                QueueBoth(match, commitLast: false);
                fixture.Refresh(view);
                AssertOpponentMask(fixture, view);

                match.Reset();
                QueueBoth(match, commitLast: false);
                fixture.Refresh(view);
                AssertOpponentMask(fixture, view);
            }
        }

        [UnityTest]
        public IEnumerator AHiddenCyclingTechnique_HasNoBadge_AndItsStartedSlotRestoresTheRealCount()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacySkill[] plain = Skills(9500);
                var skills = new[] { plain[0], LegacySkillDefinitions.Skill(46), plain[2] };
                var match = new LocalVersusMatch(skills, skills, 10000, 10000, 10000, 10000, randomSeed: 3);
                for (int player = 0; player < 2; player++)
                {
                    foreach (int lane in new[] { 0, 1, 2, 0, 1, 2 })
                        Assert.That(match.TryQueueLane(player, lane), Is.True);
                    if (player == 0) Assert.That(match.TryCommit(player), Is.True);
                }
                var view = new LocalVersusPerspective(match, 0);
                fixture.Refresh(view);
                Assert.That(fixture.Card(true, 1).Find("Cycle Mark").gameObject.activeSelf, Is.True);
                Assert.That(fixture.Card(false, 1).Find("Cycle Mark").gameObject.activeSelf, Is.False,
                    "A known cycling card's planning count could expose a hidden later feeder; no fabricated 0/cap fallback.");
                AssertHidden(fixture, 4);
                Assert.That(match.TryCommit(1), Is.True);
                for (int slot = 0; slot < 4; slot++) match.ResolveNextSlot();
                match.BeginNextSlot();
                fixture.Refresh(view);
                AssertRevealed(fixture, false, 4, view.EnemyQueue[4]);
                Assert.That(fixture.Card(false, 4).Find("Cycle Mark").gameObject.activeSelf, Is.True,
                    "Once this slot starts its actual cycle count is public.");
                AssertHidden(fixture, 5);
            }
        }

        [UnityTest]
        public IEnumerator AHiddenWaitHasNoUniquePicture_AndBrokenOpponentCardsCannotCopyTheirIconsIntoFragments()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LocalVersusMatch match = NewMatch();
                int first = match.CurrentPlanner;
                QueueFighter(match, first);
                Assert.That(match.TryCommit(first), Is.True);
                int second = match.CurrentPlanner;
                QueueFighter(match, second);
                Assert.That(match.TryQueueBreath(second), Is.True);
                var view = new LocalVersusPerspective(match, first);
                fixture.Refresh(view);
                int waitSlot = view.EnemyQueue.Count - 1;
                AssertHidden(fixture, waitSlot);
                Assert.That(Property(fixture.Card(false, waitSlot)).text, Is.EqualTo("대기"));

                fixture.Hud.PlayBreakQueueCancellation(false, 2);
                Assert.That(fixture.Hud.ActiveBreakFragmentCardCount, Is.GreaterThan(0));
                Transform layer = fixture.Hud.Root.transform.Find("Queue Break Fragments");
                foreach (DuelQueueShatterGraphic fragment in layer.GetComponentsInChildren<DuelQueueShatterGraphic>(true))
                {
                    if (fragment.name != "Broken Icon" || !fragment.gameObject.activeInHierarchy) continue;
                    Assert.That(FragmentSprite.GetValue(fragment), Is.Null,
                        "A hidden icon must be absent from the fracture mesh, not merely drawn dark.");
                    Assert.That(fragment.raycastTarget, Is.False);
                }
                fixture.Hud.PlayBreakQueueCancellation(true, 2);
                bool ownPicturePreserved = false;
                foreach (DuelQueueShatterGraphic fragment in layer.GetComponentsInChildren<DuelQueueShatterGraphic>(true))
                    if (fragment.name == "Broken Icon" && fragment.gameObject.activeInHierarchy && FragmentSprite.GetValue(fragment) != null)
                        ownPicturePreserved = true;
                Assert.That(ownPicturePreserved, Is.True, "The player's canceled cards still fracture with their real art.");
            }
        }

        private static void AssertOwnArt(HudFixture fixture, ILegacyDuelView view)
        {
            for (int slot = 0; slot < view.PlayerQueue.Count; slot++)
                AssertRevealed(fixture, true, slot, view.PlayerQueue[slot]);
        }

        private static void AssertOpponentMask(HudFixture fixture, ILegacyDuelView view)
        {
            for (int slot = 0; slot < view.EnemyQueue.Count; slot++)
                if (slot < LegacyQueueVisibility.InitiallyRevealedSlots)
                    AssertRevealed(fixture, false, slot, view.EnemyQueue[slot]);
                else AssertHidden(fixture, slot);
        }

        private static void AssertRevealed(HudFixture fixture, bool player, int slot, LegacySkill skill)
        {
            Transform card = fixture.Card(player, slot);
            Image icon = card.Find("Icon").GetComponent<Image>();
            Assert.That(icon.enabled, Is.True);
            Assert.That(icon.sprite, Is.Not.Null, "The fixture uses actual imported card pictures.");
            Sprite source = fixture.Art.GetSkillIcon(skill.IconId);
            Assert.That(source, Is.Not.Null);
            Assert.That(icon.sprite.texture, Is.SameAs(source.texture));
            Assert.That(icon.sprite.name, Is.EqualTo(source.name), "HUD crops may create a new Sprite from the same picture.");
            if (!player) Assert.That(Property(card).gameObject.activeSelf, Is.False);
        }

        private static void AssertHidden(HudFixture fixture, int slot)
        {
            Transform card = fixture.Card(false, slot);
            Image icon = card.Find("Icon").GetComponent<Image>();
            Assert.That(icon.enabled, Is.False);
            Assert.That(icon.sprite, Is.Null, "No skill silhouette or texture remains available behind the mask.");
            Text property = Property(card);
            Assert.That(property.gameObject.activeSelf, Is.True);
            Assert.That(property.raycastTarget, Is.False);
            foreach (string marker in new[] { "Cycle Mark", "Queue Keyword Mark", "Counter Mark", "Dodged Strike", "Skill Condition Feedback" })
                Assert.That(card.Find(marker).gameObject.activeSelf, Is.False, marker + " must not identify a hidden technique.");
        }

        private static Text Property(Transform card) => card.Find("Hidden Property").GetComponent<Text>();

        private static LocalVersusMatch NewMatch()
            => new LocalVersusMatch(Skills(9100), Skills(9200, reverseIcons: true),
                10000, 10000, 10000, 10000, randomSeed: 3);

        private static void QueueBoth(LocalVersusMatch match, bool commitLast)
        {
            int first = match.CurrentPlanner;
            QueueFighter(match, first);
            Assert.That(match.TryCommit(first), Is.True);
            int second = match.CurrentPlanner;
            QueueFighter(match, second);
            if (commitLast) Assert.That(match.TryCommit(second), Is.True);
        }

        private static void QueueFighter(LocalVersusMatch match, int player)
        {
            foreach (int lane in Reservations) Assert.That(match.TryQueueLane(player, lane), Is.True);
        }

        private static LegacySkill[] Skills(int firstId, bool reverseIcons = false)
        {
            var properties = new[] { LegacySkillProperty.Slash, LegacySkillProperty.Slash, LegacySkillProperty.Hit,
                LegacySkillProperty.Slash, LegacySkillProperty.Defence, LegacySkillProperty.Penetrate,
                LegacySkillProperty.Penetrate, LegacySkillProperty.Hit, LegacySkillProperty.Slash };
            var skills = new LegacySkill[properties.Length];
            for (int index = 0; index < skills.Length; index++)
                skills[index] = new LegacySkill(firstId + index, "비공개 기술 " + index, 0, 5, 5,
                    properties[index] == LegacySkillProperty.Defence ? LegacySkillKind.Defence : LegacySkillKind.Attack,
                    properties[index], 1, index % 3, "검증", iconId: reverseIcons ? 9 - index : index + 1);
            return skills;
        }

        private sealed class HudFixture : IDisposable
        {
            private readonly GameObject parent = new GameObject("Hidden Queue HUD Test");
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            public LegacyDuelArt Art { get; } = new LegacyDuelArt();
            public LegacyCombatHud Hud { get; }

            public HudFixture()
            {
                Hud = new LegacyCombatHud(parent.transform, Art, _ => { }, () => { }, () => { }, settings, () => { }, () => { });
            }

            public Transform Card(bool player, int slot)
                => Hud.Root.transform.Find(player ? "Player Requests" : "Enemy Requests").GetChild(slot);

            public void Refresh(ILegacyDuelView view)
            {
                Hud.Refresh(view, 10f, view.Phase == LegacyDuelPhase.Resolving, view.CurrentSlot?.SlotIndex ?? -1,
                    null, null, null, 0f, 0f);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Art.Dispose();
                Object.Destroy(parent);
                Object.Destroy(settings);
            }
        }
    }
}
