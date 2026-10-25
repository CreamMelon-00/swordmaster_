using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacyTensionEngineTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(24)]
        public void Tension_RemovesOneExecutedHitWithoutRedistributingPower_AndSpendsOneTechnique(int originalHits)
        {
            LegacySkill attack = Attack(100, 24, originalHits);
            var duel = Duel(new[] { attack }, Attack(900, 1));
            Statuses(duel).GrantTension(2);
            Queue(duel, 0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            int actual = System.Math.Max(1, originalHits - 1);
            Assert.That(slot.PlayerAttackCount, Is.EqualTo(actual));
            Assert.That(slot.PlayerActionAttackCount, Is.EqualTo(actual));
            Assert.That(duel.CurrentClash.PlayerHits, Is.EqualTo(actual));
            Assert.That(duel.CurrentClash.PlayerPower, Is.EqualTo(24 / originalHits));
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo((24 / originalHits) * actual));
            Assert.That(result.PlayerAttackCount, Is.EqualTo(actual));
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(1));
            Assert.That(slot.PlayerSkill, Is.SameAs(attack));
            Assert.That(attack.AttackCount, Is.EqualTo(originalHits));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(24)]
        public void Bleeding_OnlyTriggersForTheRemainingExecutedHits(int hits)
        {
            var duel = Duel(new[] { Attack(100, 24, hits) }, Guard(900));
            Statuses(duel).GrantTension(1);
            Statuses(duel).GrantBleeding(2);
            Queue(duel, 0);
            duel.Commit();
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.PlayerBleedingHealthDamage, Is.EqualTo(2 * (hits - 1)));
            Assert.That(Tension(duel.PlayerStatuses), Is.Zero);
            Assert.That(Count(duel.PlayerStatuses, LegacyStatusKind.Bleeding), Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SelfGrant_ExcludesThisAttack_AndAddsCountsWithoutSpendingNewUses(int oldCount)
        {
            var duel = Duel(new[] { Skill(71, 36), Attack(100, 24, 3, lane: 1) }, Guard(900), Guard(901));
            Statuses(duel).GrantTension(oldCount);
            Queue(duel, 0, 1);
            duel.Commit();
            LegacyCurrentSlot first = duel.BeginNextSlot();
            Assert.That(first.PlayerFeedback.SelfTensionGranted, Is.EqualTo(3));
            Assert.That(first.PlayerAttackCount, Is.EqualTo(oldCount == 0 ? 3 : 2));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(3));
            Assert.That(duel.BeginNextSlot().PlayerAttackCount, Is.EqualTo(2));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(2));
        }

        [Test]
        public void SimultaneousOpponentGrants_DoNotShortenEitherCurrentAttack()
        {
            var duel = Duel(new[] { Attack(69, 24, 3) }, Attack(69, 24, 3));
            Queue(duel, 0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerAttackCount, Is.EqualTo(3));
            Assert.That(slot.EnemyAttackCount, Is.EqualTo(3));
            Assert.That(slot.PlayerFeedback.OpponentTensionGranted, Is.EqualTo(3));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(3));
            Assert.That(Tension(duel.EnemyStatuses), Is.EqualTo(3));
        }

        [Test]
        public void GuardBreathEmptyAndTurnBoundary_PreserveCounts_ResetClearsThem()
        {
            var duel = Duel(new[] { Guard(100) }, Guard(900), Guard(901), Guard(902));
            Statuses(duel).GrantTension(3);
            Queue(duel, 0);
            Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(3));
            duel.BeginNextTurn();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(3));
            duel.Reset();
            Assert.That(duel.PlayerStatuses, Is.Empty);
        }

        [Test]
        public void CleansingBothSides_PrecedesNewGrants_AndFullyCleansedAttackKeepsAllHits()
        {
            var duel = Duel(new[] { Attack(72, 24, 3) }, Skill(69, 5));
            Statuses(duel).GrantTension(1);
            Queue(duel, 0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.TensionRemoved, Is.EqualTo(1));
            Assert.That(slot.PlayerAttackCount, Is.EqualTo(3));
            Assert.That(slot.EnemyFeedback.OpponentTensionGranted, Is.EqualTo(3));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(3));
        }

        [Test]
        public void FlatTensionPowerBonus_PrecedesStrengthAndOriginalHitDivision()
        {
            var duel = Duel(new[] { Attack(74, 24, 3) }, Attack(900, 1));
            Statuses(duel).GrantTension(1);
            Statuses(duel).Grant(LegacyStatusKind.PowerUp, 50, 1);
            Queue(duel, 0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.SelfTensionPowerBonus, Is.EqualTo(8));
            Assert.That(slot.PlayerFeedback.PowerBuffPercent, Is.EqualTo(50));
            Assert.That(slot.PlayerAttackCount, Is.EqualTo(2));
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(32), "(24+8)*1.5/3 =16 per hit, two executed hits.");
        }

        [Test]
        public void PreparingCleanseAttack_KeepsTheSamePredictedHitCountUntilItsEffectsComplete()
        {
            var duel = Duel(new[] { Attack(72, 24, 3, duration: 4) }, Guard(900, duration: 1), Guard(901, duration: 1));
            Statuses(duel).GrantTension(1);
            Queue(duel, 0);
            duel.Commit();
            while (!duel.IsTurnResolved)
            {
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                Assert.That(slot.PlayerActionAttackCount, Is.EqualTo(3));
                if (slot.PlayerSkill != null) Assert.That(slot.PlayerAttackCount, Is.EqualTo(3));
                duel.ResolveNextSlot();
            }
            Assert.That(Tension(duel.PlayerStatuses), Is.Zero);
        }

        [Test]
        public void OpponentCondition_ReadsBeforeCleansing_AndNewGrantsCannotSatisfyIt()
        {
            var duel = Duel(new[] { Skill(73, 8) }, Skill(72, 6));
            Statuses(duel, false).GrantTension(1);
            Queue(duel, 0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.OpponentTensionPowerBonus, Is.EqualTo(10));
            Assert.That(slot.EnemyFeedback.TensionRemoved, Is.EqualTo(1));
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(12));
            var fresh = Duel(new[] { Skill(74, 16) }, Skill(69, 5));
            Queue(fresh, 0);
            fresh.Commit();
            Assert.That(fresh.BeginNextSlot().PlayerFeedback.SelfTensionPowerBonus, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void FastOpponentGrant_CannotShortenAnAlreadyStartedLongAction(int oldCount)
        {
            var duel = Duel(new[] { Attack(100, 24, 3), Attack(101, 24, 3, lane: 1, duration: 1) },
                Skill(69, 5, duration: 1), Guard(900, duration: 1));
            Statuses(duel).GrantTension(oldCount);
            Queue(duel, 0, 1);
            duel.Commit();
            LegacyCurrentSlot preparing = duel.BeginNextSlot();
            Assert.That(preparing.PlayerSkill, Is.Null);
            Assert.That(preparing.PlayerActionAttackCount, Is.EqualTo(oldCount == 0 ? 3 : 2));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(oldCount + 3));
            LegacyCurrentSlot completing = duel.BeginNextSlot();
            Assert.That(completing.PlayerAttackCount, Is.EqualTo(oldCount == 0 ? 3 : 2));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(3));
            Assert.That(duel.BeginNextSlot().PlayerAttackCount, Is.EqualTo(2));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(2));
        }

        [Test]
        public void FastSelfGrant_OnlyShortensTheNextFastAction()
        {
            var duel = Duel(new[] { Skill(71, 36, duration: 1), Attack(100, 24, 3, lane: 1, duration: 1) }, Guard(900));
            Queue(duel, 0, 1);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerAttackCount, Is.EqualTo(3));
            duel.ResolveNextSlot();
            Assert.That(duel.BeginNextSlot().PlayerAttackCount, Is.EqualTo(2));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(2));
        }

        [Test]
        public void ChargeAction_SharesOneStartingSnapshotAcrossAllPreparationSegments()
        {
            var duel = Duel(new[] { Attack(100, 24, 3, duration: 4) }, Skill(69, 5, duration: 1), Skill(69, 5, duration: 1));
            Queue(duel, 0);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerActionAttackCount, Is.EqualTo(3));
            duel.ResolveNextSlot();
            duel.ResolveNextSlot();
            LegacyCurrentSlot complete = duel.BeginNextSlot();
            Assert.That(complete.PlayerAttackCount, Is.EqualTo(3));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(6));
        }

        [Test]
        public void PowerConditionUsesTheActualUseTime_ButHitReductionKeepsTheActionStart()
        {
            var duel = Duel(new[] { Attack(74, 24, 3) }, Skill(69, 5, duration: 1), Guard(900, duration: 1));
            Queue(duel, 0);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacyCurrentSlot complete = duel.BeginNextSlot();
            Assert.That(complete.PlayerAttackCount, Is.EqualTo(3));
            Assert.That(complete.PlayerFeedback.SelfTensionPowerBonus, Is.EqualTo(8));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(3));
        }

        [Test]
        public void CounterUsesReducedHits_AndKeepsTheSameSlotsNewGrant()
        {
            var duel = Duel(new[] { Guard(100) }, new[] { Skill(69, 5) }, new LegacyCounter(Attack(101, 24, 3)));
            Statuses(duel).GrantTension(1);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.True);
            Assert.That(duel.CurrentSlot.PlayerAttackCount, Is.EqualTo(2));
            Assert.That(duel.CurrentSlot.PlayerCountered, Is.True);
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(3));
        }

        [Test]
        public void DodgeCanceledCounter_DoesNotSpendTension()
        {
            var duel = Duel(new[] { Guard(100) }, new[] { Skill(69, 5) }, new LegacyCounter(Attack(101, 24, 3)));
            Statuses(duel).GrantTension(1);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool success) && success, Is.True);
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(4));
        }

        [Test]
        public void EnemyCounterAlsoUsesReducedHits()
        {
            var duel = new LegacyQueuedDuel(10000, 10000, 10000, 10000, new[] { Skill(69, 5) }, new LegacySkill[0], new[] { 0 },
                enemyCounter: new LegacyCounter(Attack(100, 24, 3)), meshPercent: 0);
            Statuses(duel, false).GrantTension(1);
            Queue(duel, 0);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().EnemyAttackCount, Is.EqualTo(2));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.EnemyStatuses), Is.EqualTo(3));
        }

        [Test]
        public void InterruptedAttackSpendsOneCount_AndCanceledFutureAttackSpendsNone()
        {
            var duel = Duel(new[] { Attack(100, 24, 3) }, Guard(900));
            Statuses(duel).GrantTension(2);
            Queue(duel, 0);
            duel.Commit();
            duel.BeginNextSlot();
            duel.ResolveNextHit();
            duel.InterruptResolvingTurn();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(1));
            var broken = new LegacyQueuedDuel(10000, 1, 10000, 10000, new[] { Attack(100, 24, 3) },
                new[] { Attack(900, 3), Guard(901) }, new[] { 2 }, meshPercent: 0);
            Statuses(broken).GrantTension(3);
            Queue(broken, 0, 0);
            broken.Commit();
            broken.ResolveNextSlot();
            Assert.That(broken.ResolveNextSlot().PlayerSkill, Is.Null);
            Assert.That(Tension(broken.PlayerStatuses), Is.EqualTo(2));
        }

        [Test]
        public void BleedingKOEndsMultiHitEarly_ButSpendsTensionOnlyOnce()
        {
            var duel = new LegacyQueuedDuel(2, 10000, 10000, 10000, new[] { Attack(100, 24, 3) },
                new[] { Guard(900) }, new[] { 1 }, meshPercent: 0);
            Statuses(duel).GrantTension(2);
            Statuses(duel).GrantBleeding(1);
            Queue(duel, 0);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.ResolveNextHit().IsFinalHit, Is.True);
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(1));
        }

        [Test]
        public void AheadAttackHitRequirement_ContinuesCountingOriginalReservationHits()
        {
            var duel = Duel(new[] { Attack(100, 24, 3), Attack(101, 24, 2, lane: 1), Skill(61, 6, lane: 2) },
                Attack(900, 1), Attack(901, 1), Attack(902, 1));
            Statuses(duel).GrantTension(2);
            Queue(duel, 0, 1, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            duel.ResolveNextSlot();
            LegacyCurrentSlot payoff = duel.BeginNextSlot();
            Assert.That(payoff.PlayerFeedback.AheadAttackHits, Is.EqualTo(5));
            Assert.That(payoff.PlayerFeedback.AheadHitPowerBonus, Is.EqualTo(14));
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(20));
        }

        [Test]
        public void FixedQueueCopiesKeepSourceIdentityAndEffects_AndForecastKeepsActualActionCounts()
        {
            LegacySkill source = Skill(71, 36, duration: 4);
            var duel = Duel(new[] { source }, Guard(900, duration: 1), Guard(901, duration: 1));
            Assert.That(duel.TrySetFixedQueues(new[] { source }, new[] { Guard(900, duration: 1), Guard(901, duration: 1) }), Is.True);
            Statuses(duel).GrantTension(1);
            var forecast = duel.ForecastTurn();
            LegacySlotForecast aggregate = LegacySlotForecast.AggregateForQueue(forecast, 0, true);
            Assert.That(aggregate.PlayerAttackCount, Is.EqualTo(2));
            Assert.That(aggregate.PlayerActionAttackCount, Is.EqualTo(2));
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(1));
            Assert.That(source.AttackCount, Is.EqualTo(3));
            duel.Commit();
            while (!duel.IsTurnResolved)
            {
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                if (slot.PlayerSkill != null)
                {
                    Assert.That(slot.PlayerSkill.Id, Is.EqualTo(71));
                    Assert.That(slot.PlayerFeedback.SelfTensionGranted, Is.EqualTo(3));
                    Assert.That(slot.PlayerAttackCount, Is.EqualTo(2));
                }
                duel.ResolveNextSlot();
            }
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(3));
        }

        [TestCase(0, 2)]
        [TestCase(0, 3)]
        [TestCase(1, 2)]
        [TestCase(1, 3)]
        [TestCase(1, 24)]
        public void Versus_ActualCountsAndForecastAreMirroredWithoutMutatingLiveState(int player, int hits)
        {
            LegacySkill attack = Attack(100, 24, hits);
            LegacySkill other = Attack(900, 1);
            var match = Match(new[] { player == 0 ? attack : other }, new[] { player == 1 ? attack : other });
            LocalVersusFighter fighter = match.GetFighter(player);
            Statuses(fighter).GrantTension(2);
            Statuses(fighter).GrantBleeding(2);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            var forecast = new List<LegacySlotForecast>();
            match.ForecastTurn(player, forecast);
            Assert.That(forecast[0].PlayerAttackCount, Is.EqualTo(hits - 1));
            Assert.That(forecast[0].PlayerHealthDamage, Is.EqualTo(2 * (hits - 1)));
            Assert.That(Tension(fighter.Statuses), Is.EqualTo(2));
            Assert.That(match.TryCommit(1), Is.True);
            match.BeginNextSlot();
            var view = new LocalVersusPerspective(match, player);
            Assert.That(view.CurrentSlot.PlayerAttackCount, Is.EqualTo(hits - 1));
            Assert.That(view.CurrentClash.PlayerHits, Is.EqualTo(hits - 1));
            LocalVersusPerspectiveHit first = view.Hit(match.ResolveNextHit());
            Assert.That(first.PlayerAttackCount, Is.EqualTo(hits - 1));
            LocalVersusSlotResult result = match.ResolveNextSlot();
            Assert.That(player == 0 ? result.LeftHealthDamage : result.RightHealthDamage, Is.EqualTo(forecast[0].PlayerHealthDamage));
            Assert.That(Tension(fighter.Statuses), Is.EqualTo(1));
        }

        [Test]
        public void Versus_SimultaneousSelfGrantsAndCleansingUseTheSameBoundaryRules()
        {
            var match = Match(new[] { Attack(72, 24, 3) }, new[] { Skill(69, 5) });
            Statuses(match.Left).GrantTension(1);
            QueueBoth(match);
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(slot.LeftAttackCount, Is.EqualTo(3));
            Assert.That(slot.LeftFeedback.TensionRemoved, Is.EqualTo(1));
            Assert.That(slot.RightFeedback.OpponentTensionGranted, Is.EqualTo(3));
            match.ResolveNextSlot();
            Assert.That(Tension(match.Left.Statuses), Is.EqualTo(3));
        }

        [Test]
        public void ConcentrationGuard_PreservesOldTension_AndRecoversActOnlyOnTheNextTurn()
        {
            LegacySkill concentration = LegacySkillDefinitions.Skill(75);
            var duel = Duel(new[] { concentration }, Guard(900));
            Statuses(duel).GrantTension(1);
            Queue(duel, concentration.LaneIndex);
            Assert.That(duel.Act, Is.EqualTo(1), "Opening ACT 3 pays the actual ACT 2 cost.");
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerSkill.Kind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(slot.PlayerAttackCount, Is.Zero);
            Assert.That(slot.PlayerFeedback.SelfTensionGranted, Is.EqualTo(4));
            Assert.That(slot.PlayerFeedback.ActGainGranted, Is.EqualTo(3));
            Assert.That(duel.Act, Is.EqualTo(1), "This grants recovery, not current ACT.");
            Assert.That(duel.NextActGain, Is.EqualTo(6));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(5), "The guard keeps its old count and adds four.");
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(7), "Residual 1 plus natural 3 and the granted 3.");
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(5));
        }

        [Test]
        public void CancelingConcentrationReservation_GrantsNeitherTensionNorNextTurnRecovery()
        {
            LegacySkill concentration = LegacySkillDefinitions.Skill(75);
            LegacySkill posture = LegacySkillDefinitions.Skill(45);
            var duel = Duel(new[] { concentration, posture }, Guard(900));
            Statuses(duel).GrantTension(1);
            duel.Commit();
            duel.ResolveNextSlot();
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(6));
            Queue(duel, concentration.LaneIndex, posture.LaneIndex);
            Assert.That(duel.PlayerQueue.Any(skill => skill.Id == 75), Is.False);
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(1));
            duel.Commit();
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(1));
        }

        [Test]
        public void ConcentrationAndOpposingHarassment_AddBothGrantsWithoutSpendingEitherGuard()
        {
            LegacySkill concentration = LegacySkillDefinitions.Skill(75);
            var duel = Duel(new[] { concentration }, Skill(70, 5));
            Statuses(duel).GrantTension(1);
            Statuses(duel, false).GrantTension(2);
            Queue(duel, concentration.LaneIndex);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerAttackCount, Is.Zero);
            Assert.That(slot.EnemyAttackCount, Is.Zero);
            Assert.That(slot.PlayerFeedback.SelfTensionGranted, Is.EqualTo(4));
            Assert.That(slot.EnemyFeedback.OpponentTensionGranted, Is.EqualTo(2));
            duel.ResolveNextSlot();
            Assert.That(Tension(duel.PlayerStatuses), Is.EqualTo(7), "Existing 1 + self 4 + opponent 2, with no attacking technique.");
            Assert.That(Tension(duel.EnemyStatuses), Is.EqualTo(2));
            Assert.That(duel.NextActGain, Is.EqualTo(6));
        }

        [Test]
        public void Versus_ConcentrationGrantsRecoveryAndKeepsGuardTensionForBothPlayers()
        {
            LegacySkill concentration = LegacySkillDefinitions.Skill(75);
            var match = Match(new[] { concentration }, new[] { concentration });
            Statuses(match.Left).GrantTension(1);
            Statuses(match.Right).GrantTension(2);
            Assert.That(match.TryQueueLane(0, concentration.LaneIndex), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, concentration.LaneIndex), Is.True);
            Assert.That(match.TryCommit(1), Is.True);
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(slot.LeftAttackCount + slot.RightAttackCount, Is.Zero);
            Assert.That(slot.LeftFeedback.ActGainGranted, Is.EqualTo(3));
            Assert.That(slot.RightFeedback.ActGainGranted, Is.EqualTo(3));
            Assert.That(match.Left.Act, Is.EqualTo(1));
            Assert.That(match.Right.Act, Is.EqualTo(1));
            match.ResolveNextSlot();
            Assert.That(Tension(match.Left.Statuses), Is.EqualTo(5));
            Assert.That(Tension(match.Right.Statuses), Is.EqualTo(6));
            Assert.That(match.Left.NextActGain, Is.EqualTo(6));
            Assert.That(match.Right.NextActGain, Is.EqualTo(6));
            match.BeginNextTurn();
            Assert.That(match.Left.Act, Is.EqualTo(7));
            Assert.That(match.Right.Act, Is.EqualTo(7));
        }

        private static LegacySkill Skill(int id, int power, int lane = 0, int duration = 2)
        {
            LegacySkill source = LegacySkillDefinitions.Skill(id);
            return new LegacySkill(id, source.Name, 0, power, power, source.Kind, source.Property, source.AttackCount, lane, "", durationHalfUnits: duration);
        }
        private static LegacySkill Attack(int id, int power, int hits = 1, int lane = 0, int duration = 2)
            => new LegacySkill(id, "attack", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, lane, "", durationHalfUnits: duration);
        private static LegacySkill Guard(int id, int lane = 0, int duration = 2)
            => new LegacySkill(id, "guard", 0, 1000, 1000, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, lane, "", durationHalfUnits: duration);
        private static LegacyQueuedDuel Duel(LegacySkill[] player, params LegacySkill[] enemy)
            => Duel(player, enemy, null);
        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy, LegacyCounter playerCounter)
            => new LegacyQueuedDuel(10000, 10000, 10000, 10000, player, enemy, new[] { enemy.Length }, playerCounter: playerCounter, meshPercent: 0);
        private static LocalVersusMatch Match(LegacySkill[] left, LegacySkill[] right)
            => new LocalVersusMatch(left, right, leftHealth: 10000, rightHealth: 10000, leftResistance: 10000, rightResistance: 10000);
        private static void Queue(LegacyQueuedDuel duel, params int[] lanes)
        {
            foreach (int lane in lanes) Assert.That(duel.TryQueueLane(lane), Is.True);
        }
        private static void QueueBoth(LocalVersusMatch match)
        {
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(match.TryCommit(1), Is.True);
        }
        private static int Tension(IReadOnlyList<LegacyStatusSnapshot> statuses) => Count(statuses, LegacyStatusKind.Tension);
        private static int Count(IReadOnlyList<LegacyStatusSnapshot> statuses, LegacyStatusKind kind)
            => statuses.Where(s => s.Kind == kind).Sum(s => s.RemainingCount);
        private static LegacyStatusCollection Statuses(LegacyQueuedDuel duel, bool player = true)
            => player ? (LegacyStatusCollection)typeof(LegacyQueuedDuel).GetField("playerStatuses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duel)
                : (LegacyStatusCollection)typeof(LegacyQueuedDuel).GetProperty("enemyStatuses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duel);
        private static LegacyStatusCollection Statuses(LocalVersusFighter fighter)
            => (LegacyStatusCollection)typeof(LocalVersusFighter).GetField("StatusEffects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fighter);
    }
}
