using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class PowerPressureRangeTests
    {
        [TestCase(1, 10, 1, 1, 10)]
        [TestCase(3, 20, 3, 3, 18)]
        [TestCase(5, 5, 1, 5, 5)]
        [TestCase(0, 0, 1, 1, 1)]
        public void ActualRolledTotalsStayInsideTheirSnapshotBounds_IncludingHitRounding(
            int minimum, int maximum, int hits, int expectedMinimum, int expectedMaximum)
        {
            var seen = new HashSet<int>();
            for (int seed = 1; seed <= 128; seed++)
            {
                LegacySkill attack = Skill(99881, minimum, maximum, hits);
                var duel = Ready(new[] { attack }, Array.Empty<LegacySkill>(), seed);
                duel.Commit();
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower),
                    Is.EqualTo((expectedMinimum, expectedMaximum)));
                Assert.That(slot.PlayerTotalPower, Is.InRange(expectedMinimum, expectedMaximum));
                seen.Add(slot.PlayerTotalPower);
            }
            Assert.That(seen, Does.Contain(expectedMinimum));
            Assert.That(seen, Does.Contain(expectedMaximum));
        }

        [Test]
        public void BuffAndMeshingBoundsSurviveBuffConsumption_AndLaterStatusChanges()
        {
            var duel = Ready(new[] { Skill(99882, 3, 11, 3), Skill(99883, 1, 1, lane: 1) },
                Array.Empty<LegacySkill>(), mesh: 20);
            LegacyStatusCollection statuses = PlayerStatuses(duel);
            statuses.Grant(LegacyStatusKind.PowerUp, 50, 1);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerMesh.BonusPercent, Is.EqualTo(40));
            Assert.That(slot.PlayerFeedback.PowerBuffPercent, Is.EqualTo(50));
            Assert.That(statuses.PowerPercent, Is.Zero, "The cast already consumed its original buff.");
            Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower), Is.EqualTo((3, 18)),
                "The original 3..11 roll used 190%, then divided by three hits before rounding.");
            statuses.Grant(LegacyStatusKind.PowerUp, 200, 1);
            Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower), Is.EqualTo((3, 18)));
        }

        [Test]
        public void TensionUsesTheActualReducedHitCount_InsteadOfTheAuthoredCount()
        {
            var duel = Ready(new[] { Skill(99884, 3, 11, 3) }, Array.Empty<LegacySkill>());
            PlayerStatuses(duel).GrantTension(1);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerAttackCount, Is.EqualTo(2));
            Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower), Is.EqualTo((2, 6)));
            Assert.That(slot.PlayerTotalPower, Is.InRange(2, 6));
        }

        [Test]
        public void DefenceKeepsItsBuffedBounds_WithoutMultiplyingByZeroAttackHits()
        {
            var guard = new LegacySkill(99895, "수비 범위", 0, 4, 9, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 0, string.Empty);
            var duel = Ready(new[] { guard }, Array.Empty<LegacySkill>());
            PlayerStatuses(duel).Grant(LegacyStatusKind.PowerUp, 50, 1);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerAttackCount, Is.Zero);
            Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower), Is.EqualTo((6, 13)));
            Assert.That(slot.PlayerTotalPower, Is.InRange(6, 13));
        }

        [Test]
        public void SnapshotCreationAndRepeatedReadsConsumeNoAdditionalRandomRolls()
        {
            LegacySkill attack = Skill(99896, 1, 10);
            var duel = Ready(new[] { attack, attack, attack }, Array.Empty<LegacySkill>(), seed: 17);
            var expected = new Random(17);
            duel.Commit();
            for (int cast = 0; cast < 3; cast++)
            {
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                for (int read = 0; read < 20; read++)
                    Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower), Is.EqualTo((1, 10)));
                Assert.That(slot.PlayerTotalPower, Is.EqualTo(expected.Next(1, 11)));
                Finish(duel);
            }
        }

        [Test]
        public void ProtectionSpendIsInTheBounds_BeforeThePercentageMultiplierAndHitDivision()
        {
            var duel = Ready(new[] { Skill(60, 2, 10, 2) }, Array.Empty<LegacySkill>());
            PlayerStatuses(duel).Grant(LegacyStatusKind.Protection, 50, 2);
            PlayerStatuses(duel).Grant(LegacyStatusKind.PowerUp, 50, 1);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.ProtectionSpendPowerBonus, Is.EqualTo(16));
            Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower), Is.EqualTo((26, 38)),
                "(2..10 +16) *150%, divided by two and floored, then two actual hits.");
        }

        [Test]
        public void CyclingSnapshotsTheCurrentUse_NotTheNextUsesAlreadyIncrementedCount()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacySkill cycle = KeywordSheet.Skill(KeywordSheet.CycleQ);
                var duel = Ready(new[] { cycle, cycle }, Array.Empty<LegacySkill>());
                duel.Commit();
                LegacyCurrentSlot first = duel.BeginNextSlot();
                Assert.That((first.PlayerMinimumPower, first.PlayerMaximumPower), Is.EqualTo((5, 5)));
                Assert.That(duel.CycleUses(cycle.Id), Is.EqualTo(1));
                Finish(duel);
                LegacyCurrentSlot second = duel.BeginNextSlot();
                Assert.That((second.PlayerMinimumPower, second.PlayerMaximumPower), Is.EqualTo((7, 7)));
                Assert.That((first.PlayerMinimumPower, first.PlayerMaximumPower), Is.EqualTo((5, 5)));
            });
        }

        [Test]
        public void CountAheadFlatPowerIsInTheSnapshot_EvenWhenFeedbackHasNoHitCountBonus()
        {
            KeywordSheet.WithKeywords(() =>
            {
                var duel = Ready(new[] { LegacyCommonActions.Breathe, KeywordSheet.Skill(KeywordSheet.BreathPayoff) },
                    Array.Empty<LegacySkill>());
                duel.Commit(); duel.BeginNextSlot(); Finish(duel);
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                Assert.That(slot.PlayerFeedback.AheadHitPowerBonus, Is.Zero);
                Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower), Is.EqualTo((10, 10)));
                Assert.That(slot.PlayerTotalPower, Is.EqualTo(10));
            });
        }

        [Test]
        public void ChargeHasNoInventedRangeWhilePreparing_AndHasItsActualBoundsOnCompletion()
        {
            LegacySkill charge = Skill(99885, 3, 13, 2, duration: 4);
            var duel = Ready(new[] { charge }, new[] { Skill(99886, 1, 1), Skill(99886, 1, 1) });
            duel.Commit();
            LegacyCurrentSlot first = duel.BeginNextSlot();
            Assert.That(first.PlayerSkill, Is.Null);
            Assert.That((first.PlayerMinimumPower, first.PlayerMaximumPower), Is.EqualTo((0, 0)));
            Finish(duel);
            LegacyCurrentSlot complete = duel.BeginNextSlot();
            Assert.That(complete.PlayerActionCompletes, Is.True);
            Assert.That((complete.PlayerMinimumPower, complete.PlayerMaximumPower), Is.EqualTo((2, 12)));
        }

        [Test]
        public void CounterGetsItsBoundsOnlyAfterTheExistingDeferredChoiceRollsIt()
        {
            LegacySkill counter = Skill(99887, 1, 10, 2);
            var duel = Ready(Array.Empty<LegacySkill>(), new[] { Skill(99888, 1, 1) }, counter: counter);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PendingPlayerCounter, Is.SameAs(counter));
            Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower), Is.EqualTo((0, 0)));
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.True);
            Assert.That(slot.PlayerCountered, Is.True);
            Assert.That((slot.PlayerMinimumPower, slot.PlayerMaximumPower), Is.EqualTo((2, 10)));
            Assert.That(slot.PlayerTotalPower, Is.InRange(2, 10));
        }

        [Test]
        public void BreathingAndTrulyEmptyIntervalsHaveZeroBounds()
        {
            var duel = Ready(new[] { LegacyCommonActions.Breathe },
                new[] { Skill(99889, 1, 1), Skill(99889, 1, 1) });
            duel.Commit();
            LegacyCurrentSlot first = duel.BeginNextSlot();
            Assert.That((first.PlayerMinimumPower, first.PlayerMaximumPower), Is.EqualTo((0, 0)));
            Finish(duel);
            LegacyCurrentSlot second = duel.BeginNextSlot();
            Assert.That(second.PlayerSkill, Is.Null);
            Assert.That((second.PlayerMinimumPower, second.PlayerMaximumPower), Is.EqualTo((0, 0)));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void VersusPerspectivesReadTheirOwnAndTheOtherSnapshots_WithBuffsAndReducedHits(int viewer)
        {
            var match = new LocalVersusMatch(new[] { Skill(99891, 1, 10) }, new[] { Skill(99892, 3, 20, 3) },
                randomSeed: 7, usePowerBreak: true);
            FighterStatuses(match.Left).Grant(LegacyStatusKind.PowerUp, 50, 1);
            FighterStatuses(match.Right).GrantTension(1);
            foreach (int owner in new[] { 0, 1 })
            {
                Assert.That(match.TryQueueLane(owner, 0), Is.True);
                Assert.That(match.TryCommit(owner), Is.True);
            }
            LocalVersusSlot source = match.BeginNextSlot();
            Assert.That((source.LeftMinimumPower, source.LeftMaximumPower), Is.EqualTo((1, 15)));
            Assert.That((source.RightMinimumPower, source.RightMaximumPower), Is.EqualTo((2, 12)));
            ILegacySlotView view = new LocalVersusPerspective(match, viewer).CurrentSlot;
            Assert.That((view.PlayerMinimumPower, view.PlayerMaximumPower), Is.EqualTo(viewer == 0 ? (1, 15) : (2, 12)));
            Assert.That((view.EnemyMinimumPower, view.EnemyMaximumPower), Is.EqualTo(viewer == 0 ? (2, 12) : (1, 15)));
            Assert.That(view.PlayerTotalPower, Is.InRange(view.PlayerMinimumPower, view.PlayerMaximumPower));
            Assert.That(view.EnemyTotalPower, Is.InRange(view.EnemyMinimumPower, view.EnemyMaximumPower));
        }

        [Test]
        public void IiaMirrorCanceledCounterClearsTheOriginalMirroredBoundsAsWellAsItsPower()
        {
            var duel = Ready(Array.Empty<LegacySkill>(), new[] { LegacySkillDefinitions.Skill(504) },
                counter: Skill(99893, 1, 10)).EnableIiaBossPatterns();
            // Enabling boss patterns resets its preparation; install the narrow scenario afterward.
            Arrange(duel, Array.Empty<LegacySkill>(), new[] { LegacySkillDefinitions.Skill(504) });
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That((slot.EnemyMinimumPower, slot.EnemyMaximumPower), Is.EqualTo((10, 10)));
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, false, out _), Is.True);
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.True);
            Assert.That(slot.PlayerSkill, Is.Null);
            Assert.That((slot.EnemyMinimumPower, slot.EnemyMaximumPower, slot.EnemyTotalPower), Is.EqualTo((0, 0, 0)));
        }

        private static LegacySkill Skill(int id, int min, int max, int hits = 1, int lane = 0, int duration = 2)
            => new LegacySkill(id, "압력 범위", 0, min, max, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, lane, string.Empty, durationHalfUnits: duration);

        private static LegacyQueuedDuel Ready(LegacySkill[] player, LegacySkill[] enemy, int seed = 7,
            int mesh = 0, LegacySkill counter = null)
        {
            var duel = new LegacyQueuedDuel(10000, 0, 10000, 0,
                player.Length > 0 ? player : new[] { Skill(99894, 1, 1) }, enemy, new[] { enemy.Length },
                randomSeed: seed, playerCounter: counter == null ? null : new LegacyCounter(counter),
                meshPercent: mesh, usePowerBreak: true);
            Arrange(duel, player, enemy);
            return duel;
        }

        private static void Arrange(LegacyQueuedDuel duel, LegacySkill[] player, LegacySkill[] enemy)
        {
            // The existing presentation-sample API deliberately permits a truly empty side, unlike fixed tavern queues.
            var prepare = typeof(LegacyQueuedDuel).GetMethod("TrySetPreparedQueues", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(prepare, Is.Not.Null);
            Assert.That((bool)prepare.Invoke(duel, new object[] { player, enemy }), Is.True);
        }

        private static LegacyStatusCollection PlayerStatuses(LegacyQueuedDuel duel)
            => (LegacyStatusCollection)typeof(LegacyQueuedDuel).GetField("playerStatuses", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(duel);

        private static LegacyStatusCollection FighterStatuses(LocalVersusFighter fighter)
            => (LegacyStatusCollection)typeof(LocalVersusFighter).GetField("StatusEffects", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(fighter);

        private static void Finish(LegacyQueuedDuel duel)
        {
            while (!duel.CurrentSlot.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
        }
    }
}
