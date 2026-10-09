using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LocalVersusMatchTests
    {
        [Test]
        public void Planning_AlternatesOnReservations_AndTwoPassesCommit()
        {
            var match = NewMatch(new[] { Attack(100, 1, 5) }, new[] { Attack(200, 1, 5) });
            Assert.That(match.CurrentPlanner, Is.Zero);
            Assert.That(match.TryQueueLane(1, 0), Is.False);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.CurrentPlanner, Is.EqualTo(1));
            Assert.That(match.TryQueueBreath(1), Is.True);
            Assert.That(match.CurrentPlanner, Is.Zero);
            Assert.That(match.TryPass(0), Is.True);
            Assert.That(match.ConsecutivePasses, Is.EqualTo(1));
            Assert.That(match.LastPassPlayer, Is.Zero);
            Assert.That(match.TryPass(1), Is.True);
            Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Resolving));
            Assert.That(match.CurrentPlanner, Is.EqualTo(-1));
            Assert.That(match.ResolutionSlotCount, Is.EqualTo(1));
            Assert.That(match.Left.Queue[0].Id, Is.EqualTo(100));
            Assert.That(match.Right.Queue[0].IsWait, Is.True);
        }

        [Test]
        public void FreeCycle_RetainsPlanner_AndActionAfterPassClearsPassStreak()
        {
            var match = NewMatch(new[] { Attack(100, 1, 4), Attack(101, 1, 7) },
                new[] { Attack(200, 1, 4) });
            Assert.That(match.TryCycleLanes(0), Is.True);
            Assert.That(match.CurrentPlanner, Is.Zero);
            Assert.That(match.Left.GetLane(0)[0].Id, Is.EqualTo(101));
            Assert.That(match.TryPass(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(match.ConsecutivePasses, Is.Zero);
            Assert.That(match.TryPass(0), Is.True);
            Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(match.CurrentPlanner, Is.EqualTo(1));
        }

        [Test]
        public void BothSidesSpendActIndependently_AndNaturalActReturnsOnNextTurn()
        {
            var match = NewMatch(new[] { Attack(100, 3, 1) }, new[] { Attack(200, 1, 1) });
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.Left.Act, Is.Zero);
            Assert.That(match.Right.Act, Is.EqualTo(3));
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(match.Right.Act, Is.EqualTo(2));
            Commit(match);
            ResolveTurn(match);
            match.BeginNextTurn();
            Assert.That(match.Left.Act, Is.EqualTo(3));
            Assert.That(match.Right.Act, Is.EqualTo(5));
            Assert.That(match.OpeningPlayer, Is.EqualTo(1));
            Assert.That(match.CurrentPlanner, Is.EqualTo(1));
        }

        [Test]
        public void CancelRemovesEarlierSkillsWithoutRefundingAct_AndKeepsBreath()
        {
            var left = new[] { Attack(100, 1, 5), LegacySkillDefinitions.Skill(45) };
            var match = NewMatch(left, new[] { Attack(200, 1, 1) });
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryPass(1), Is.True);
            Assert.That(match.TryQueueBreath(0), Is.True);
            Assert.That(match.TryPass(1), Is.True);
            Assert.That(match.TryQueueLane(0, 2), Is.True);
            Assert.That(match.Left.Act, Is.EqualTo(1));
            Assert.That(match.Left.Queue.Count, Is.EqualTo(2));
            Assert.That(match.Left.Queue[0].IsWait, Is.True);
            Assert.That(match.Left.Queue[1].Id, Is.EqualTo(45));
        }

        [Test]
        public void CyclingSkill_CountsActualUsesOnly_AndBothReservationsKeepOriginalCost()
        {
            LegacySkill cycle = LegacySkillDefinitions.Skill(46);
            var match = NewMatch(new[] { cycle }, new[] { Attack(200, 1, 1) });
            Assert.That(match.TryQueueLane(0, 1), Is.True);
            Assert.That(match.Left.EffectiveCost(cycle), Is.EqualTo(1));
            Assert.That(match.TryPass(1), Is.True);
            Assert.That(match.TryQueueLane(0, 1), Is.True);
            Assert.That(match.Left.Act, Is.EqualTo(1));
            Assert.That(match.Left.CycleUses(46), Is.Zero);
            Commit(match);
            ResolveTurn(match);
            Assert.That(match.Left.CycleUses(46), Is.EqualTo(2));
            match.Left.EffectivePowerRange(cycle, out int min, out int max);
            Assert.That(min, Is.EqualTo(15));
            Assert.That(max, Is.EqualTo(20));
            match.BeginNextTurn();
            Assert.That(match.Left.EffectiveCost(cycle), Is.EqualTo(3));
        }

        [Test]
        public void SimultaneousLethalHits_AreDraw_AndLaterHitsStop()
        {
            var match = NewMatch(new[] { Attack(100, 1, 30, hits: 3) },
                new[] { Attack(200, 1, 30, hits: 3) }, health: 5, resistance: 0);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Commit(match);
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(slot.HitCount, Is.EqualTo(3));
            LocalVersusHit hit = match.ResolveNextHit();
            Assert.That(hit.LeftAttacked && hit.RightAttacked, Is.True);
            Assert.That(hit.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
            Assert.That(hit.IsFinalHit, Is.True);
            Assert.That(slot.HitCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => match.ResolveNextHit());
            Assert.That(match.CompleteCurrentSlot().Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
            Assert.That(match.IsFinished, Is.True);
        }

        [Test]
        public void OneSidedLethalFirstHit_StopsMultiHitWithoutAttackingCorpse()
        {
            var match = NewMatch(new[] { Attack(100, 1, 30, hits: 3) },
                new[] { Attack(200, 1, 1) }, health: 5, resistance: 0);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryQueueBreath(1), Is.True);
            Commit(match);
            match.BeginNextSlot();
            LocalVersusHit hit = match.ResolveNextHit();
            Assert.That(hit.Outcome, Is.EqualTo(LocalVersusOutcome.LeftVictory));
            Assert.That(hit.IsFinalHit, Is.True);
            Assert.That(match.CompleteCurrentSlot().Outcome, Is.EqualTo(LocalVersusOutcome.LeftVictory));
        }

        [Test]
        public void CancelBoost_MultipliesOnlyTheNextQueuedTechnique()
        {
            var match = NewMatch(new[] { Attack(100, 0, 6), LegacySkillDefinitions.Skill(45) },
                new[] { Attack(200, 1, 1) });
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryPass(1), Is.True);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryPass(1), Is.True);
            Assert.That(match.TryQueueLane(0, 2), Is.True);
            Assert.That(match.TryPass(1), Is.True);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.Left.Queue.Count, Is.EqualTo(2));
            Commit(match);
            Assert.That(match.ResolveNextSlot().RightHealthDamage, Is.Zero);
            Assert.That(match.ResolveNextSlot().RightHealthDamage, Is.EqualTo(12));
        }

        [Test]
        public void ConditionalActGain_WorksForBothPlayers()
        {
            LegacySkill skill = LegacySkillDefinitions.Skill(1);
            var match = NewMatch(new[] { skill }, new[] { skill });
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Commit(match);
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(match.Left.NextActGain, Is.EqualTo(4));
            Assert.That(match.Right.NextActGain, Is.EqualTo(4));
            Assert.That(slot.LeftFeedback.ActGainGranted, Is.EqualTo(1));
            Assert.That(slot.RightFeedback.ActGainGranted, Is.EqualTo(1));
        }

        [Test]
        public void GuardAgainstHit_GrantsConditionalActToEitherSide()
        {
            LegacySkill guard = LegacySkillDefinitions.Skill(7);
            LegacySkill hit = LegacySkillDefinitions.Skill(5);
            var leftGuards = NewMatch(new[] { guard }, new[] { hit });
            Assert.That(leftGuards.TryQueueLane(0, 0), Is.True);
            Assert.That(leftGuards.TryQueueLane(1, 2), Is.True);
            Commit(leftGuards);
            leftGuards.BeginNextSlot();
            Assert.That(leftGuards.Left.NextActGain, Is.EqualTo(5));
            Assert.That(leftGuards.Right.NextActGain, Is.EqualTo(3));

            var rightGuards = NewMatch(new[] { hit }, new[] { guard });
            Assert.That(rightGuards.TryQueueLane(0, 2), Is.True);
            Assert.That(rightGuards.TryQueueLane(1, 0), Is.True);
            Commit(rightGuards);
            rightGuards.BeginNextSlot();
            Assert.That(rightGuards.Left.NextActGain, Is.EqualTo(3));
            Assert.That(rightGuards.Right.NextActGain, Is.EqualTo(5));
        }

        [Test]
        public void EmptyRoundAtLimit_IsDraw_AndResetRestoresOpeningPlayer()
        {
            var match = new LocalVersusMatch(new[] { Attack(100, 1, 1) },
                new[] { Attack(200, 1, 1) }, roundLimit: 1, openingPlayer: 1);
            Assert.That(match.CurrentPlanner, Is.EqualTo(1));
            Assert.That(match.TryPass(1), Is.True);
            Assert.That(match.TryPass(0), Is.True);
            Assert.That(match.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
            Assert.That(match.IsFinished, Is.True);
            match.Reset();
            Assert.That(match.CurrentPlanner, Is.EqualTo(1));
            Assert.That(match.Left.Act, Is.EqualTo(3));
            Assert.That(match.Right.Act, Is.EqualTo(3));
        }

        private static LocalVersusMatch NewMatch(LegacySkill[] left, LegacySkill[] right,
            int health = 100, int resistance = 50)
            => new LocalVersusMatch(left, right, health, resistance, health, resistance, randomSeed: 17);

        private static LegacySkill Attack(int id, int cost, int power, int hits = 1)
            => new LegacySkill(id, "Test", cost, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty);

        private static void Commit(LocalVersusMatch match)
        {
            int first = match.CurrentPlanner;
            Assert.That(match.TryPass(first), Is.True);
            Assert.That(match.TryPass(1 - first), Is.True);
        }

        private static void ResolveTurn(LocalVersusMatch match)
        {
            while (!match.IsTurnResolved && !match.IsFinished) match.ResolveNextSlot();
        }
    }
}





