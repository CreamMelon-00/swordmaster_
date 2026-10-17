using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class BreakQueueCancellationTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void Duel_BreakFinishesCurrentMultihit_CancelsOnlyBrokenSidesLaterSkills_WithoutRefund(bool playerBroken)
        {
            var player = Attack(900, 1, 12, 3);
            var enemy = Attack(901, 1, 12, 3);
            var duel = new LegacyQueuedDuel(1000, playerBroken ? 2 : 1000, 1000, playerBroken ? 1000 : 2,
                new[] { player }, new[] { enemy }, new[] { 3 }, meshPercent: 0,
                playerCounter: new LegacyCounter(player, 4), enemyCounter: new LegacyCounter(enemy, 4));
            QueueThree(duel);
            duel.Commit();
            LegacyCurrentSlot current = duel.BeginNextSlot();
            LegacyHitResult first = duel.ResolveNextHit();

            Assert.That(first.PlayerAttacked && first.EnemyAttacked, Is.True);
            Assert.That(duel.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(playerBroken ? 1 : -1));
            Assert.That(duel.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(playerBroken ? -1 : 1));
            Assert.That(current.IsResolved, Is.False, "A break does not stop the active skill's other hits.");
            while (!current.IsResolved)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.PlayerAttacked && hit.EnemyAttacked, Is.True);
            }
            Assert.That(current.HitsResolved, Is.EqualTo(3));
            duel.CompleteCurrentSlot();
            for (int i = 1; i < 3; i++)
            {
                LegacySlotResult result = duel.ResolveNextSlot();
                Assert.That(result.SlotIndex, Is.EqualTo(i), "Canceled reservations never shift the opponent's indexes.");
                Assert.That(playerBroken ? result.PlayerSkill : result.EnemySkill, Is.Null);
                Assert.That(playerBroken ? result.EnemySkill : result.PlayerSkill, Is.Not.Null);
            }
            Assert.That(duel.Act, Is.Zero, "Cancellation never refunds the ACT paid for all three reservations.");
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(3), "Original icons remain available for shatter snapshots.");
            Assert.That(duel.EnemyQueue.Count, Is.EqualTo(3));
            Assert.That(playerBroken ? duel.PlayerCountersRemaining : duel.EnemyCountersRemaining, Is.EqualTo(4),
                "A canceled side cannot refill its emptied slots with automatic counters.");

            duel.BeginNextTurn();
            Assert.That(playerBroken ? duel.Player.IsResistanceBroken : duel.Enemy.IsResistanceBroken, Is.True);
            Assert.That(duel.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(-1));
            Assert.That(duel.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(-1));
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().PlayerSkill, Is.SameAs(player));
            LegacySlotResult second = duel.ResolveNextSlot();
            Assert.That(second.PlayerSkill, Is.SameAs(player), "Ongoing broken status does not cancel a new turn's queue.");
            Assert.That(second.EnemySkill, Is.SameAs(enemy));
            Assert.That(duel.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(-1));
            Assert.That(duel.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Duel_SimultaneousDirectBreaksKeepBothCurrentSkills_AndCancelBothLaterQueues()
        {
            LegacySkill laudare = LegacySkillDefinitions.Skill(500);
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { laudare }, new[] { laudare }, new[] { 3 }, meshPercent: 0);
            QueueThree(duel);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(duel.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(1));
            Assert.That(duel.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(1));
            Assert.That(slot.PlayerFeedback.OpponentBroken && slot.EnemyFeedback.OpponentBroken, Is.True);
            while (!slot.IsResolved)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.PlayerAttacked && hit.EnemyAttacked, Is.True);
            }
            Assert.That(slot.HitsResolved, Is.EqualTo(5));
            duel.CompleteCurrentSlot();
            while (!duel.IsTurnResolved)
            {
                LegacySlotResult result = duel.ResolveNextSlot();
                Assert.That(result.PlayerSkill, Is.Null);
                Assert.That(result.EnemySkill, Is.Null);
                Assert.That(result.PlayerHealthDamage + result.EnemyHealthDamage, Is.Zero);
            }
        }

        [Test]
        public void Duel_CanceledSkillsDoNotGainCycleUses_OrGrantTheirActEffects_AndBreathsAreCanceledToo()
        {
            LegacySkill cycle = LegacySkillDefinitions.Skill(46), actGain = LegacySkillDefinitions.Skill(43);
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 1000,
                new[] { Attack(900, 1, 1), cycle, actGain }, new[] { LegacySkillDefinitions.Skill(500) }, new[] { 4 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(1) && duel.TryQueueLane(0) && duel.TryQueueBreath(), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            for (int i = 1; i < 4; i++) Assert.That(duel.ResolveNextSlot().PlayerSkill, Is.Null);
            Assert.That(duel.CycleUses(cycle.Id), Is.Zero);
            Assert.That(duel.CycleUsesAtSlot(1), Is.Zero);
            Assert.That(duel.NextActGain, Is.EqualTo(duel.PlayerBaseActGain));
            Assert.That(duel.Act, Is.Zero);
        }

        [Test]
        public void Duel_ForecastUsesTheSameBreakCancellation_WithoutChangingLiveQueuesOrFighters()
        {
            var duel = new LegacyQueuedDuel(1000, 2, 1000, 1000, new[] { Attack(900, 1, 12, 3) },
                new[] { Attack(901, 1, 12, 3) }, new[] { 3 }, meshPercent: 0);
            QueueThree(duel);
            IReadOnlyList<LegacySlotForecast> forecast = duel.ForecastTurn();
            Assert.That(forecast.Count, Is.EqualTo(3));
            Assert.That(forecast[0].PlayerBroken, Is.True);
            Assert.That(forecast[1].PlayerSkill, Is.Null);
            Assert.That(forecast[2].EnemySkill, Is.Not.Null);
            Assert.That(duel.Player.Resistance, Is.EqualTo(2));
            Assert.That(duel.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(-1));
            Assert.That(duel.PlayerQueue[1], Is.Not.Null);
            duel.Commit();
            for (int i = 0; i < forecast.Count; i++)
            {
                LegacySlotResult actual = duel.ResolveNextSlot();
                Assert.That(actual.PlayerSkill, Is.SameAs(forecast[i].PlayerSkill));
                Assert.That(actual.EnemySkill, Is.SameAs(forecast[i].EnemySkill));
                Assert.That(actual.PlayerHealthDamage, Is.EqualTo(forecast[i].PlayerHealthDamage));
                Assert.That(actual.EnemyHealthDamage, Is.EqualTo(forecast[i].EnemyHealthDamage));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Versus_BreakKeepsCurrentMultihit_OnlyCancelsTheBrokenSide_AndMirrorsToEachPlayer(bool leftBroken)
        {
            LegacySkill left = Attack(900, 1, 12, 3), right = Attack(901, 1, 12, 3);
            var match = new LocalVersusMatch(new[] { left }, new[] { right },
                1000, leftBroken ? 2 : 1000, 1000, leftBroken ? 1000 : 2);
            QueueThree(match);
            Commit(match);
            LocalVersusSlot slot = match.BeginNextSlot();
            match.ResolveNextHit();
            Assert.That(match.Left.BreakCanceledFromSlotIndex, Is.EqualTo(leftBroken ? 1 : -1));
            Assert.That(match.Right.BreakCanceledFromSlotIndex, Is.EqualTo(leftBroken ? -1 : 1));
            var brokenView = new LocalVersusPerspective(match, leftBroken ? 0 : 1);
            var wholeView = new LocalVersusPerspective(match, leftBroken ? 1 : 0);
            Assert.That(brokenView.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(1));
            Assert.That(wholeView.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(1));
            while (!slot.IsResolved)
            {
                LocalVersusHit hit = match.ResolveNextHit();
                Assert.That(hit.LeftAttacked && hit.RightAttacked, Is.True);
            }
            Assert.That(slot.HitsResolved, Is.EqualTo(3));
            match.CompleteCurrentSlot();
            for (int i = 1; i < 3; i++)
            {
                LocalVersusSlotResult result = match.ResolveNextSlot();
                Assert.That(result.SlotIndex, Is.EqualTo(i));
                Assert.That(leftBroken ? result.LeftSkill : result.RightSkill, Is.Null);
                Assert.That(leftBroken ? result.RightSkill : result.LeftSkill, Is.Not.Null);
            }
            Assert.That(match.Left.Act + match.Right.Act, Is.Zero);
            Assert.That(brokenView.PlayerQueue.Count, Is.EqualTo(3));
            Assert.That(brokenView.CycleUsesAtSlot(1), Is.Zero);

            match.BeginNextTurn();
            Assert.That(brokenView.Player.IsResistanceBroken, Is.True);
            Assert.That(brokenView.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(-1));
            for (int i = 0; i < 4; i++) Assert.That(match.TryQueueLane(match.CurrentPlanner, 0), Is.True);
            Commit(match);
            match.ResolveNextSlot();
            LocalVersusSlotResult next = match.ResolveNextSlot();
            Assert.That(next.LeftSkill, Is.SameAs(left));
            Assert.That(next.RightSkill, Is.SameAs(right));
            Assert.That(match.Left.BreakCanceledFromSlotIndex, Is.EqualTo(-1));
            Assert.That(match.Right.BreakCanceledFromSlotIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Versus_SimultaneousDirectBreaksCancelBothTails_ButBothFiveHitSkillsFinish()
        {
            LegacySkill laudare = LegacySkillDefinitions.Skill(500);
            var match = new LocalVersusMatch(new[] { laudare }, new[] { laudare }, 1000, 50, 1000, 50);
            QueueThree(match);
            Commit(match);
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(match.Left.BreakCanceledFromSlotIndex, Is.EqualTo(1));
            Assert.That(match.Right.BreakCanceledFromSlotIndex, Is.EqualTo(1));
            while (!slot.IsResolved)
            {
                LocalVersusHit hit = match.ResolveNextHit();
                Assert.That(hit.LeftAttacked && hit.RightAttacked, Is.True);
            }
            Assert.That(slot.HitsResolved, Is.EqualTo(5));
            match.CompleteCurrentSlot();
            while (!match.IsTurnResolved)
            {
                LocalVersusSlotResult result = match.ResolveNextSlot();
                Assert.That(result.LeftSkill, Is.Null);
                Assert.That(result.RightSkill, Is.Null);
                Assert.That(result.LeftHealthDamage + result.RightHealthDamage, Is.Zero);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Versus_ForecastMirrorsTheSameCanceledIndexes_AndDoesNotChangeTheMatch(int perspective)
        {
            var match = new LocalVersusMatch(new[] { Attack(900, 1, 12, 3) }, new[] { Attack(901, 1, 12, 3) },
                1000, 2, 1000, 1000);
            QueueThree(match);
            var forecast = new List<LegacySlotForecast>();
            match.ForecastTurn(perspective, forecast);
            Assert.That(forecast.Count, Is.EqualTo(3));
            Assert.That(perspective == 0 ? forecast[1].PlayerSkill : forecast[1].EnemySkill, Is.Null);
            Assert.That(match.Left.State.Resistance, Is.EqualTo(2));
            Assert.That(match.Left.BreakCanceledFromSlotIndex, Is.EqualTo(-1));
            Assert.That(match.Left.Queue[1], Is.Not.Null);
            Commit(match);
            for (int i = 0; i < forecast.Count; i++)
            {
                LocalVersusSlotResult actual = match.ResolveNextSlot();
                Assert.That(perspective == 0 ? actual.LeftSkill : actual.RightSkill, Is.SameAs(forecast[i].PlayerSkill));
                Assert.That(perspective == 0 ? actual.RightSkill : actual.LeftSkill, Is.SameAs(forecast[i].EnemySkill));
                Assert.That(perspective == 0 ? actual.LeftHealthDamage : actual.RightHealthDamage, Is.EqualTo(forecast[i].PlayerHealthDamage));
            }
        }

        private static LegacySkill Attack(int id, int cost, int power, int hits = 1)
            => new LegacySkill(id, "Break probe", cost, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "");

        private static void QueueThree(LegacyQueuedDuel duel)
        {
            for (int i = 0; i < 3; i++) Assert.That(duel.TryQueueLane(0), Is.True);
        }

        private static void QueueThree(LocalVersusMatch match)
        {
            for (int i = 0; i < 6; i++) Assert.That(match.TryQueueLane(match.CurrentPlanner, 0), Is.True);
        }

        private static void Commit(LocalVersusMatch match)
        {
            Assert.That(match.TryPass(match.CurrentPlanner), Is.True);
            Assert.That(match.TryPass(match.CurrentPlanner), Is.True);
        }
    }
}
