using System;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacyQueueVisibilityTests
    {
        [Test]
        public void BothPerspectives_KeepTheirExactQueues_AndRevealOnlyTheOpponentsFirstTwo()
        {
            LocalVersusMatch match = NewMatch();
            QueueBoth(match);
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);

            Assert.That(host.PlayerQueue, Is.SameAs(match.Left.Queue));
            Assert.That(guest.PlayerQueue, Is.SameAs(match.Right.Queue));
            Assert.That(host.EnemyQueue, Is.SameAs(match.Right.Queue));
            Assert.That(guest.EnemyQueue, Is.SameAs(match.Left.Queue));
            Assert.That(host.PlayerQueue.Count, Is.EqualTo(4));
            Assert.That(guest.PlayerQueue.Count, Is.EqualTo(4));
            Assert.That(host.EnemyQueue[3].Id, Is.EqualTo(1003), "The simulation retains exact hidden data.");
            foreach (LocalVersusPerspective view in new[] { host, guest })
            {
                Assert.That(view.IsEnemySkillRevealed(0), Is.True);
                Assert.That(view.IsEnemySkillRevealed(1), Is.True);
                Assert.That(view.IsEnemySkillRevealed(2), Is.False);
                Assert.That(view.IsEnemySkillRevealed(3), Is.False);
                Assert.That(view.IsEnemySkillRevealed(-1), Is.False);
                Assert.That(view.IsEnemySkillRevealed(4), Is.False);
            }
        }

        [Test]
        public void LaterTechnique_RevealsAtSlotStart_StaysRevealed_AndResetsNextTurnOrRestart()
        {
            LocalVersusMatch match = NewMatch();
            QueueBoth(match);
            Commit(match);
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            match.ResolveNextSlot();
            match.ResolveNextSlot();
            Assert.That(host.IsEnemySkillRevealed(2), Is.False);
            Assert.That(guest.IsEnemySkillRevealed(2), Is.False);

            match.BeginNextSlot();
            Assert.That(match.CurrentSlot.HitsResolved, Is.Zero);
            Assert.That(host.IsEnemySkillRevealed(2), Is.True);
            Assert.That(guest.IsEnemySkillRevealed(2), Is.True);
            Assert.That(host.IsEnemySkillRevealed(3), Is.False);
            FinishCurrentSlot(match);
            Assert.That(host.IsEnemySkillRevealed(2), Is.True, "Completing a slot must not hide it again.");
            Assert.That(host.IsEnemySkillRevealed(3), Is.False);
            match.ResolveNextSlot();
            Assert.That(host.IsEnemySkillRevealed(3), Is.True);

            match.BeginNextTurn();
            QueueBoth(match);
            Assert.That(host.IsEnemySkillRevealed(1), Is.True);
            Assert.That(host.IsEnemySkillRevealed(2), Is.False);
            Assert.That(guest.IsEnemySkillRevealed(2), Is.False);
            Commit(match);
            match.ResolveNextSlot();
            match.ResolveNextSlot();
            match.BeginNextSlot();
            Assert.That(host.IsEnemySkillRevealed(2), Is.True);

            match.Reset();
            QueueBoth(match);
            Assert.That(host.IsEnemySkillRevealed(1), Is.True);
            Assert.That(host.IsEnemySkillRevealed(2), Is.False);
            Assert.That(guest.IsEnemySkillRevealed(2), Is.False);
        }

        [Test]
        public void BreakCanceledHiddenReservation_RemainsHiddenWhenItsEmptyExecutionSlotStarts()
        {
            LocalVersusMatch match = NewMatch(resistance: 1, power: 5);
            QueueBoth(match);
            Commit(match);
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            match.ResolveNextSlot();
            Assert.That(match.Left.BreakCanceledFromSlotIndex, Is.EqualTo(1));
            Assert.That(match.Right.BreakCanceledFromSlotIndex, Is.EqualTo(1));
            match.ResolveNextSlot();
            match.BeginNextSlot();
            Assert.That(match.CurrentSlot.LeftSkill, Is.Null);
            Assert.That(match.CurrentSlot.RightSkill, Is.Null);
            Assert.That(host.IsEnemySkillRevealed(2), Is.False);
            Assert.That(guest.IsEnemySkillRevealed(2), Is.False);
            FinishCurrentSlot(match);
            match.ResolveNextSlot();
            Assert.That(host.IsEnemySkillRevealed(2), Is.False);
            Assert.That(host.IsEnemySkillRevealed(3), Is.False);
            Assert.That(guest.IsEnemySkillRevealed(2), Is.False);
            Assert.That(host.IsEnemySkillRevealed(1), Is.True, "The public prefix stays known even if canceled.");
        }

        [Test]
        public void DomainForecast_DoesNotRevealLiveQueuesOrChangeTheirExecutionProgress()
        {
            LocalVersusMatch match = NewMatch();
            QueueBoth(match);
            var view = new LocalVersusPerspective(match, 0);
            var slots = new List<LegacySlotForecast>();
            view.ForecastTurn(slots);

            Assert.That(slots.Count, Is.EqualTo(4), "The domain forecast keeps its complete simulation.");
            Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(match.CurrentSlot, Is.Null);
            Assert.That(view.IsEnemySkillRevealed(2), Is.False);
            Assert.That(view.IsEnemySkillRevealed(3), Is.False);
            Assert.That(view.Player.Health, Is.EqualTo(1000));
            Assert.That(view.Enemy.Health, Is.EqualTo(1000));
        }

        [Test]
        public void Campaign_ContinuesToRevealTheWholeEnemyQueue()
        {
            var duel = new LegacyQueuedDuel(1000, 100, 1000, 100,
                new[] { Attack(1000, 0) }, new[] { Attack(1001, 0) }, new[] { 4 });
            Assert.That(duel.EnemyQueue.Count, Is.EqualTo(4));
            for (int index = 0; index < duel.EnemyQueue.Count; index++)
                Assert.That(duel.IsEnemySkillRevealed(index), Is.True);
            Assert.That(duel.IsEnemySkillRevealed(-1), Is.False);
            Assert.That(duel.IsEnemySkillRevealed(4), Is.False);
            Assert.That(LegacyQueueVisibility.CanForecastSlot(duel, 4), Is.True);
        }

        [Test]
        public void ForecastPolicy_AllowsOnlyTheKnownPrefix_WithoutReducingFullyKnownUnopposedForecasts()
        {
            LocalVersusMatch match = NewMatch();
            QueueBoth(match);
            var view = new LocalVersusPerspective(match, 0);
            Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 0), Is.True);
            Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 1), Is.True);
            Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 2), Is.False);
            Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 4), Is.False);
            Assert.That(LegacyQueueVisibility.CanForecastSlot(view, -1), Is.False);
            Assert.Throws<ArgumentNullException>(() => LegacyQueueVisibility.CanForecastSlot(null, 0));
            Commit(match);
            Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 0), Is.False);

            match.Reset();
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 1), Is.True);
            Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 4), Is.True,
                "An entirely known short opposing queue also permits later unopposed previews.");
        }

        [Test]
        public void ForecastCommitCycleFeed_HiddenTailCanChangePublicPower_ButNeverChangesForecastAvailability()
        {
            KeywordSheet.WithKeywords(() =>
            {
                var rawDamage = new List<int>();
                foreach (bool hiddenFeeder in new[] { false, true })
                {
                    LegacySkill cycle = LegacySkillDefinitions.Skill(KeywordSheet.CycleQ);
                    LegacySkill middle = Attack(1002, 1);
                    LegacySkill tail = hiddenFeeder ? LegacySkillDefinitions.Skill(KeywordSheet.Feeder) : Attack(1003, 2);
                    var match = new LocalVersusMatch(new[] { Attack(1000, 0) }, new[] { cycle, middle, tail },
                        1000, 100, 1000, 100, randomSeed: 17);
                    Assert.That(match.TryCommit(0), Is.True);
                    Assert.That(match.TryQueueLane(1, 0), Is.True);
                    Assert.That(match.TryQueueLane(1, 1), Is.True);
                    Assert.That(match.TryQueueLane(1, 2), Is.True);
                    var view = new LocalVersusPerspective(match, 0);
                    var slots = new List<LegacySlotForecast>();
                    view.ForecastTurn(slots);
                    rawDamage.Add(slots[0].PlayerHealthDamage);

                    Assert.That(slots.Count, Is.EqualTo(3));
                    Assert.That(view.IsEnemySkillRevealed(2), Is.False);
                    Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 0), Is.False);
                    Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 1), Is.False);
                    Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 2), Is.False);
                }
                Assert.That(rawDamage, Is.EqualTo(new[] { 5, 7 }),
                    "Commit applies a hidden feeder to an earlier public cycling technique in the raw simulation.");
            });
        }

        [Test]
        public void ForecastPolicy_KeepsTheSlotBeforeAPublicCycle_AndAllowsCycleWithNoHiddenTail()
        {
            KeywordSheet.WithKeywords(() =>
            {
                var match = new LocalVersusMatch(new[] { Attack(1000, 0) },
                    new[] { Attack(1001, 0), LegacySkillDefinitions.Skill(KeywordSheet.CycleW), Attack(1003, 2) },
                    1000, 100, 1000, 100, randomSeed: 17);
                Assert.That(match.TryCommit(0), Is.True);
                Assert.That(match.TryQueueLane(1, 0), Is.True);
                Assert.That(match.TryQueueLane(1, 1), Is.True);
                var view = new LocalVersusPerspective(match, 0);
                Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 1), Is.True);
                Assert.That(match.TryQueueLane(1, 2), Is.True);
                Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 0), Is.True);
                Assert.That(LegacyQueueVisibility.CanForecastSlot(view, 1), Is.False);
            });
        }

        private static LocalVersusMatch NewMatch(int resistance = 100, int power = 1)
            => new LocalVersusMatch(new[] { Attack(1000, 0, power), Attack(1001, 1, power) },
                new[] { Attack(1002, 0, power), Attack(1003, 1, power) },
                1000, resistance, 1000, resistance, randomSeed: 17);

        private static LegacySkill Attack(int id, int lane, int power = 1)
            => new LegacySkill(id, "Test", 0, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 1, lane, string.Empty);

        private static void QueueBoth(LocalVersusMatch match)
        {
            int first = match.CurrentPlanner;
            QueueFour(match, first);
            Assert.That(match.TryCommit(first), Is.True);
            QueueFour(match, 1 - first);
        }

        private static void QueueFour(LocalVersusMatch match, int player)
        {
            for (int count = 0; count < 3; count++) Assert.That(match.TryQueueLane(player, 0), Is.True);
            Assert.That(match.TryQueueLane(player, 1), Is.True);
        }

        private static void Commit(LocalVersusMatch match)
        {
            while (match.Phase == LegacyDuelPhase.Planning) Assert.That(match.TryCommit(match.CurrentPlanner), Is.True);
        }

        private static void FinishCurrentSlot(LocalVersusMatch match)
        {
            while (!match.CurrentSlot.IsResolved) match.ResolveNextHit();
            match.CompleteCurrentSlot();
        }
    }
}
