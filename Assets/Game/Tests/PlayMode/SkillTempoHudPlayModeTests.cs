using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillTempoHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator TimelineWidths_AlignFastPairWithNormal_AndChargeWithTwoNormalCards()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(new[] { Skill(901, 1), Skill(902, 1), Skill(903, 2), Skill(904, 4) },
                    new[] { Skill(911, 2), Skill(912, 2), Skill(913, 2), Skill(914, 2) });
                scope.Refresh(duel);
                var fast = scope.Card(true, 0);
                var second = scope.Card(true, 1);
                var normal = scope.Card(true, 2);
                var charge = scope.Card(true, 3);
                Assert.That(fast.rect.width, Is.EqualTo(normal.rect.width * .5f).Within(.001f));
                Assert.That(charge.rect.width, Is.EqualTo(normal.rect.width * 2f).Within(.001f));
                Assert.That(fast.rect.height, Is.EqualTo(normal.rect.height));
                Assert.That(charge.rect.height, Is.EqualTo(normal.rect.height));
                Assert.That(Outer(second), Is.EqualTo(-Outer(scope.Card(false, 0))).Within(.01f),
                    "The second fast completion meets the opposing normal completion.");
                Assert.That(Outer(charge), Is.EqualTo(-Outer(scope.Card(false, 3))).Within(.01f));
                Assert.That(duel.Player.Health, Is.EqualTo(1000), "Layout never resolves the battle.");
            }
        }

        [UnityTest]
        public IEnumerator FastThenNormal_ShowsHalfACardOfEmptyTime_AndPooledCardsRegainSquareShape()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(new[] { Skill(901, 1), Skill(902, 2) }, new[] { Skill(911, 2), Skill(912, 2) });
                scope.Refresh(duel);
                var fast = scope.Card(true, 0);
                var normal = scope.Card(true, 1);
                float scale = normal.localScale.x;
                float gap = Mathf.Abs(normal.anchoredPosition.x - fast.anchoredPosition.x) -
                    (normal.rect.width + fast.rect.width) * scale * .5f;
                Assert.That(gap, Is.EqualTo(normal.rect.width * scale * .5f).Within(.01f));
                var ordinary = Duel(new[] { Skill(903, 2), Skill(904, 2) }, new[] { Skill(913, 2) });
                scope.Refresh(ordinary);
                Assert.That(scope.Card(true, 0).rect.width, Is.EqualTo(scope.Card(true, 0).rect.height));
                Assert.That(scope.Card(true, 1).rect.width, Is.EqualTo(scope.Card(true, 1).rect.height));
            }
        }

        [UnityTest]
        public IEnumerator Resolving_NormalCardStaysVisibleAcrossBothFastActions_ThenDisappears()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(new[] { Skill(901, 1), Skill(902, 1), Skill(903, 2) },
                    new[] { Skill(911, 2), Skill(912, 2) });
                scope.Refresh(duel);
                duel.Commit();
                var first = duel.BeginNextSlot();
                scope.Refresh(duel, true, first.SlotIndex);
                Assert.That(scope.Card(false, 0), Is.Not.Null);
                while (!first.IsResolved) duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
                scope.Refresh(duel, true, first.SlotIndex + 1);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null,
                    "Consumed cards stay hidden while the next action is approaching and CurrentSlot is null.");
                Assert.That(scope.Card(false, 0), Is.Not.Null);
                var second = duel.BeginNextSlot();
                scope.Refresh(duel, true, second.SlotIndex);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);
                Assert.That(scope.Card(true, 1), Is.Not.Null);
                Assert.That(scope.Card(false, 0), Is.Not.Null, "The same normal action still occupies the timeline.");
                while (!second.IsResolved) duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
                var third = duel.BeginNextSlot();
                scope.Refresh(duel, true, third.SlotIndex);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(false, 0), Is.Null);
                Assert.That(scope.Card(false, 1), Is.Not.Null);
            }
        }

        private static float Outer(RectTransform card)
            => card.anchoredPosition.x + Mathf.Sign(card.anchoredPosition.x) * card.rect.width * card.localScale.x * .5f;

        private static LegacySkill Skill(int id, int duration)
            => new LegacySkill(id, "시험", 0, 1, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                1, 0, "", durationHalfUnits: duration);

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy)
        {
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { enemy.Length }, meshPercent: 0);
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            return duel;
        }

        private sealed class Scope : IDisposable
        {
            private readonly GameObject parent = new GameObject("Skill Tempo HUD Test");
            public LegacyCombatHud Hud { get; }
            public Scope()
            {
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                Canvas.ForceUpdateCanvases();
            }
            public void Refresh(LegacyQueuedDuel duel, bool resolving = false, int slot = 0)
            {
                Hud.Refresh(duel, 10f, resolving, slot, null, null, null, 0f, 0f);
                Canvas.ForceUpdateCanvases();
            }
            public RectTransform Card(bool player, int index)
            {
                var card = Hud.GetQueuedSkillAnchor(player, index);
                Assert.That(card, Is.Not.Null);
                return card;
            }
            public void Dispose() { Hud.Dispose(); Object.Destroy(parent); }
        }
    }
}
