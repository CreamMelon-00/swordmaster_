using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class QueueInsertionPlayModeTests
    {
        [UnityTest]
        public IEnumerator RepeatedReservationsOfTheSameSkill_InsertSeparatelyAndSettleOnRealTimeWithoutChangingCombat()
        {
            yield return null;
            using (var scope = new HudScope())
            {
                LegacyQueuedDuel duel = PassiveDuel();
                scope.Refresh(duel);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);

                Assert.That(duel.TryQueueLane(0), Is.True);
                int[] reserved = State(duel);
                scope.Refresh(duel);
                RectTransform first = Card(scope.Hud, true, 0);
                Assert.That(first.anchoredPosition.y, Is.GreaterThan(0f), "A new reservation begins above its socket.");
                CollectionAssert.AreEqual(reserved, State(duel));

                scope.Refresh(duel, realDelta: .13f);
                Assert.That(first.anchoredPosition.y, Is.LessThan(0f), "The short landing passes slightly below the socket.");
                Assert.That(first.localScale.y, Is.LessThan(first.localScale.x), "Landing briefly compresses the icon.");
                scope.Refresh(duel, realDelta: .09f);
                AssertSettled(first);

                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(duel.PlayerQueue[1], Is.SameAs(duel.PlayerQueue[0]), "The lane deliberately reuses one skill object.");
                reserved = State(duel);
                scope.Refresh(duel);
                AssertSettled(Card(scope.Hud, true, 0));
                Assert.That(Card(scope.Hud, true, 1).anchoredPosition.y, Is.GreaterThan(0f),
                    "The second occurrence inserts even though its skill reference is unchanged.");
                scope.Refresh(duel, realDelta: .22f);
                AssertSettled(Card(scope.Hud, true, 0));
                AssertSettled(Card(scope.Hud, true, 1));
                CollectionAssert.AreEqual(reserved, State(duel), "HUD time never spends ACT, resolves damage, or changes queues.");
            }
        }

        [UnityTest]
        public IEnumerator IiaRevealsInsertAtReservationTime_WhileExistingCardsAndTheLastJealousyCardStaySeated()
        {
            yield return null;
            using (var scope = new HudScope())
            {
                var duel = new LegacyQueuedDuel(100000, 1000, 400, 50, new[] { Attack(1, 1) },
                    GreenRibbonBossBattle.CreateScript(), 1).EnableIiaBossPatterns();
                Resolve(duel); duel.BeginNextTurn();
                Assert.That(duel.TrySetFixedQueues(new[] { Attack(1, 6) }, new[] { LegacyCommonActions.Breathe }), Is.True);
                Resolve(duel); duel.BeginNextTurn();
                Assert.That(duel.EnemyQueue.Select(skill => skill.Id), Is.EqualTo(new[] { 5, 47, 506 }));
                scope.Refresh(duel);
                for (int slot = 0; slot < 3; slot++) AssertSettled(Card(scope.Hud, false, slot));

                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(duel.EnemyQueue.Select(skill => skill.Id), Is.EqualTo(new[] { 5, 47, 503, 506 }));
                int[] reserved = State(duel);
                scope.Refresh(duel);
                Assert.That(Card(scope.Hud, true, 0).anchoredPosition.y, Is.GreaterThan(0f));
                Assert.That(Card(scope.Hud, false, 2).anchoredPosition.y, Is.GreaterThan(0f),
                    "The newly disclosed enemy technique inserts on the same reservation frame.");
                foreach (int slot in new[] { 0, 1, 3 }) AssertSettled(Card(scope.Hud, false, slot));
                scope.Refresh(duel, realDelta: .22f);
                CollectionAssert.AreEqual(reserved, State(duel));

                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(duel.EnemyQueue.Select(skill => skill.Id), Is.EqualTo(new[] { 5, 47, 503, 1, 506 }));
                reserved = State(duel);
                scope.Refresh(duel);
                Assert.That(Card(scope.Hud, false, 3).anchoredPosition.y, Is.GreaterThan(0f));
                foreach (int slot in new[] { 0, 1, 2, 4 }) AssertSettled(Card(scope.Hud, false, slot));
                scope.Refresh(duel, realDelta: .22f);
                for (int slot = 0; slot < 5; slot++) AssertSettled(Card(scope.Hud, false, slot));
                CollectionAssert.AreEqual(reserved, State(duel));
            }
        }

        [UnityTest]
        public IEnumerator CommitTurnBoundariesResetAndHiddenHud_CancelInsertionWithoutReplayingExistingCards()
        {
            yield return null;
            using (var scope = new HudScope())
            {
                LegacyQueuedDuel duel = PassiveDuel();
                scope.Refresh(duel);
                Assert.That(duel.TryQueueLane(0), Is.True);
                scope.Refresh(duel);
                Assert.That(Card(scope.Hud, true, 0).anchoredPosition.y, Is.GreaterThan(0f));
                duel.Commit();
                scope.Refresh(duel, resolving: true);
                AssertSettled(Card(scope.Hud, true, 0));

                while (!duel.IsTurnResolved) duel.ResolveNextSlot();
                duel.BeginNextTurn();
                scope.Refresh(duel);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);
                Assert.That(duel.TryQueueLane(0), Is.True);
                scope.Refresh(duel);
                Assert.That(Card(scope.Hud, true, 0).anchoredPosition.y, Is.GreaterThan(0f));
                scope.Hud.BeginTurn();
                scope.Refresh(duel);
                AssertSettled(Card(scope.Hud, true, 0));

                duel.Reset();
                scope.Hud.Reset();
                scope.Refresh(duel);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);
                Assert.That(duel.TryQueueLane(0), Is.True);
                scope.Refresh(duel);
                Assert.That(Card(scope.Hud, true, 0).anchoredPosition.y, Is.GreaterThan(0f));
                scope.Hud.Root.gameObject.SetActive(false);
                // No hidden frame is supplied: OnDisable must discard the unfinished entrance itself.
                scope.Hud.Root.gameObject.SetActive(true);
                scope.Refresh(duel);
                AssertSettled(Card(scope.Hud, true, 0), "Returning to the HUD never replays a stale reservation.");
            }
        }

        private static LegacyQueuedDuel PassiveDuel()
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Attack(1, 1) },
                Array.Empty<LegacySkill>(), new[] { 0 }, meshPercent: 0);

        private static LegacySkill Attack(int power, int hits)
            => new LegacySkill(901, "검사 공격", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "");

        private static int[] State(LegacyQueuedDuel duel)
            => new[] { duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance,
                duel.Act, duel.RoundNumber, duel.PlayerQueue.Count, duel.EnemyQueue.Count, duel.IiaJealousy };

        private static RectTransform Card(LegacyCombatHud hud, bool player, int slot)
        {
            RectTransform card = hud.GetQueuedSkillAnchor(player, slot);
            Assert.That(card, Is.Not.Null, (player ? "Player" : "Enemy") + " card " + slot);
            return card;
        }

        private static void AssertSettled(RectTransform card, string message = null)
        {
            Assert.That(card.anchoredPosition.y, Is.EqualTo(0f).Within(.001f), message);
            Assert.That(card.localScale.y, Is.EqualTo(card.localScale.x).Within(.001f), message);
        }

        private static void Resolve(LegacyQueuedDuel duel)
        {
            duel.Commit();
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
        }

        private sealed class HudScope : IDisposable
        {
            private readonly GameObject parent = new GameObject("Queue Insertion HUD Test");
            public LegacyCombatHud Hud { get; }

            public HudScope()
            {
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                Canvas.ForceUpdateCanvases();
            }

            public void Refresh(LegacyQueuedDuel duel, float realDelta = 0f, bool resolving = false)
            {
                Hud.Refresh(duel, 10f, resolving, -1, null, null, null, 0f, realDelta);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(parent);
            }
        }
    }
}
