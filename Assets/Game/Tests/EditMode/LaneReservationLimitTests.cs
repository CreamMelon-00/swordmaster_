using System;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class LaneReservationLimitTests
    {
        [Test]
        public void EachLaneAcceptsThreeReservations_AndTheFourthChangesNothing()
        {
            LegacyQueuedDuel duel = PassiveDuel(Skill(920, 0), Skill(921, 1), Skill(922, 2));
            ILegacyDuelView view = duel;
            for (int lane = 0; lane < 3; lane++)
            {
                for (int count = 0; count < LegacyLaneLimit.MaxReservations; count++)
                {
                    Assert.That(view.LaneReservationsThisTurn(lane), Is.EqualTo(count));
                    Assert.That(view.LaneReservationsRemaining(lane), Is.EqualTo(3 - count));
                    Assert.That(duel.CanQueueLane(lane), Is.True);
                    Assert.That(duel.TryQueueLane(lane), Is.True);
                }
                int act = duel.Act;
                int[] queued = duel.PlayerQueue.Select(skill => skill.Id).ToArray();
                int[] order = duel.GetLane(lane).Select(skill => skill.Id).ToArray();
                int lastSlot = duel.LastQueuedSlotIndex;
                Assert.That(duel.CanQueueLane(lane), Is.False);
                Assert.That(duel.TryQueueLane(lane), Is.False);
                Assert.That(view.LaneReservationsThisTurn(lane), Is.EqualTo(3));
                Assert.That(view.LaneReservationsRemaining(lane), Is.Zero);
                Assert.That(duel.Act, Is.EqualTo(act));
                Assert.That(duel.PlayerQueue.Select(skill => skill.Id), Is.EqualTo(queued));
                Assert.That(duel.GetLane(lane).Select(skill => skill.Id), Is.EqualTo(order));
                Assert.That(duel.LastQueuedSlotIndex, Is.EqualTo(lastSlot));
            }
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(9), "Each lane has its own allowance.");
        }

        [Test]
        public void RejectedReservationsDoNotSpendTheAllowance_AndClosedOrEmptyLanesHaveNone()
        {
            var duel = new LegacyQueuedDuel(1000, 100, 1000, 100,
                new[] { Skill(920, 0, cost: 4), Skill(921, 1) }, Array.Empty<LegacySkill>(), new[] { 0 },
                features: CombatFeature.LaneQ);
            Assert.That(duel.TryQueueLane(-1) || duel.TryQueueLane(3), Is.False);
            Assert.That(duel.TryQueueLane(0), Is.False, "Opening ACT cannot pay four.");
            Assert.That(duel.TryQueueLane(1) || duel.TryQueueLane(2), Is.False);
            Assert.That(duel.LaneReservationsThisTurn(0), Is.Zero);
            Assert.That(duel.LaneReservationsRemaining(0), Is.EqualTo(3), "ACT and allowance are independent.");
            Assert.That(duel.LaneReservationsRemaining(1), Is.Zero);
            Assert.That(duel.LaneReservationsRemaining(2), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => duel.LaneReservationsThisTurn(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => duel.LaneReservationsRemaining(3));
            ResolveTurn(duel);
            duel.BeginNextTurn();
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.LaneReservationsThisTurn(0), Is.EqualTo(1));
        }

        [TestCase("취소")]
        [TestCase("반환")]
        public void RemovingReservationsDoesNotRestoreTheAllowance_AndACappedRemoverCannotChangeTheQueue(string mode)
        {
            const int removerId = 918;
            KeywordSheet.With(() =>
            {
                var duel = PassiveDuel(Skill(920, 0), Skill(921, 0), LegacySkillDefinitions.Skill(removerId), Skill(922, 1));
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "The remover accepted as reservation three removes the first two.");
                Assert.That(duel.LaneReservationsThisTurn(0), Is.EqualTo(3));
                Assert.That(duel.LaneReservationsRemaining(0), Is.Zero);
                Assert.That(duel.TryQueueLane(1), Is.True, "Another lane can still reserve.");
                Assert.That(duel.TryCycleLanes() && duel.TryCycleLanes(), Is.True);
                Assert.That(duel.GetLane(0)[0].Id, Is.EqualTo(removerId));
                int[] before = duel.PlayerQueue.Select(skill => skill.Id).ToArray();
                int act = duel.Act;
                Assert.That(duel.TryQueueLane(0), Is.False, "The capped remover must not erase the other lane's skill.");
                Assert.That(duel.PlayerQueue.Select(skill => skill.Id), Is.EqualTo(before));
                Assert.That(duel.Act, Is.EqualTo(act));
                Assert.That(duel.LaneReservationsThisTurn(0), Is.EqualTo(3));
                Assert.That(duel.LaneReservationsThisTurn(1), Is.EqualTo(1));
            }, KeywordSheet.Row(removerId, "Q", "공격", 0, 1, (Column.QueueRemovalMode, mode)));
        }

        [Test]
        public void CyclingAndBothKindsOfBreathingDoNotUseLaneReservations()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = PassiveDuel(LegacySkillDefinitions.Skill(KeywordSheet.AutoBreathGuard), Skill(920, 0));
                Assert.That(duel.TryCycleLanes(), Is.True);
                Assert.That(duel.LaneReservationsThisTurn(0), Is.Zero);
                Assert.That(duel.TryCycleLanes(), Is.True);
                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(duel.AutoBreathSlotIndex, Is.EqualTo(1));
                Assert.That(duel.LaneReservationsThisTurn(0), Is.EqualTo(1));
                for (int count = 0; count < LegacyQueuedDuel.MaximumBreathsPerTurn; count++)
                    Assert.That(duel.TryQueueBreath(), Is.True);
                Assert.That(duel.LaneReservationsThisTurn(0), Is.EqualTo(1));
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                Assert.That(duel.LaneReservationsRemaining(0), Is.Zero);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(7), "Three manual skills, one automatic breath and three manual breaths.");
            });
        }

        [Test]
        public void ForecastCopiesTheAllowance_WithoutSpendingTheLiveDuelAllowance()
        {
            LegacyQueuedDuel duel = PassiveDuel(Skill(920, 0), Skill(921, 1));
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
            Assert.That(duel.ForecastIfQueued(0), Is.Not.Null);
            duel.ForecastTurn();
            Assert.That(duel.LaneReservationsThisTurn(0), Is.EqualTo(2));
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(2));
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.ForecastIfQueued(0), Is.Null, "The copy inherits an exhausted lane.");
            Assert.That(duel.ForecastIfQueued(1), Is.Not.Null);
            Assert.That(duel.LaneReservationsThisTurn(1), Is.Zero);
            Assert.That(duel.TryQueueLane(0), Is.False);
        }

        [Test]
        public void NextPlanningTurnAndRestartResetEveryLaneAllowance()
        {
            LegacyQueuedDuel duel = PassiveDuel(Skill(920, 0), Skill(921, 1), Skill(922, 2));
            for (int lane = 0; lane < 3; lane++)
                for (int count = 0; count < 3; count++) Assert.That(duel.TryQueueLane(lane), Is.True);
            ResolveTurn(duel);
            Assert.That(duel.LaneReservationsThisTurn(0), Is.EqualTo(3), "Resolution does not reset an allowance.");
            duel.BeginNextTurn();
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.That(duel.LaneReservationsThisTurn(lane), Is.Zero);
                Assert.That(duel.LaneReservationsRemaining(lane), Is.EqualTo(3));
                Assert.That(duel.TryQueueLane(lane), Is.True);
            }
            duel.Reset();
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.That(duel.LaneReservationsThisTurn(lane), Is.Zero);
                Assert.That(duel.LaneReservationsRemaining(lane), Is.EqualTo(3));
            }
        }

        [Test]
        public void AutomaticCountersDoNotCountAsManualLaneReservations()
        {
            var duel = new LegacyQueuedDuel(1000, 100, 1000, 100,
                new[] { Skill(920, 0) }, new[] { Skill(921, 1) }, new[] { 4 },
                playerCounter: new LegacyCounter(Skill(922, 0)));
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().PlayerSkill.Id, Is.EqualTo(922), "The automatic counter fills the unreserved slot.");
            Assert.That(duel.LaneReservationsThisTurn(0), Is.Zero);
            Assert.That(duel.LaneReservationsRemaining(0), Is.EqualTo(3));
            Assert.That(duel.EnemyQueue.Count, Is.EqualTo(4), "Scripted enemy queues keep their own action count.");
        }

        [Test]
        public void FixedTavernQueuesMayContainEightSkillsFromOneLaneWithoutManualReservations()
        {
            LegacySkill skill = Skill(920, 0);
            LegacyQueuedDuel duel = PassiveDuel(skill);
            Assert.That(duel.TrySetFixedQueues(Enumerable.Repeat(skill, 8).ToArray(), new[] { Skill(921, 0) }), Is.True);
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(8));
            Assert.That(duel.LaneReservationsThisTurn(0), Is.Zero);
            Assert.That(duel.ForecastTurn().Count, Is.EqualTo(8));
            Assert.That(duel.LaneReservationsThisTurn(0), Is.Zero);
        }

        private static LegacyQueuedDuel PassiveDuel(params LegacySkill[] skills)
            => new LegacyQueuedDuel(1000, 100, 1000, 100, skills, Array.Empty<LegacySkill>(), new[] { 0 }, meshPercent: 0);

        private static LegacySkill Skill(int id, int lane, int cost = 0)
            => new LegacySkill(id, "lane test", cost, 1, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane, string.Empty);

        private static void ResolveTurn(LegacyQueuedDuel duel)
        {
            duel.Commit();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
        }
    }
}
