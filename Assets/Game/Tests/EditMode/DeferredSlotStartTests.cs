using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class DeferredSlotStartTests
    {
        [Test]
        public void Preparation_SettlesTheCounterAndOpeningEffectsOnlyOnce_WithoutAHit()
        {
            LegacySkill counter = LegacySkillDefinitions.Skill(1);
            var duel = Duel(counter, Attack(990, 3, 11), 1);
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.False, "Planning has no pending impact.");
            duel.Commit();
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.False, "A slot must have begun.");
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            int hitCount = slot.HitCount;
            int act = duel.Act;
            Assert.That(slot.PendingPlayerCounter, Is.SameAs(counter));
            Assert.That(slot.PlayerSkill, Is.Null);
            Assert.That(duel.PlayerCountersRemaining, Is.EqualTo(1));
            Assert.That(duel.NextActGain, Is.EqualTo(3), "Beginning a slot does not apply the deferred recovery.");

            Assert.That(duel.PrepareCurrentSlotImpact(), Is.True);
            Assert.That(slot.PendingPlayerCounter, Is.Null);
            Assert.That(slot.PlayerSkill, Is.SameAs(counter));
            Assert.That(slot.PlayerCountered, Is.True);
            Assert.That(duel.PlayerCountersRemaining, Is.Zero);
            Assert.That(duel.NextActGain, Is.EqualTo(4));
            Assert.That(slot.PlayerFeedback.ActGainGranted, Is.EqualTo(1));
            Assert.That(slot.HitsResolved, Is.Zero);
            Assert.That(slot.HitCount, Is.EqualTo(hitCount));
            Assert.That(duel.Player.Health, Is.EqualTo(1000));
            Assert.That(duel.Enemy.Health, Is.EqualTo(1000));
            Assert.That(duel.Player.Resistance, Is.EqualTo(3000));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(3000));
            Assert.That(duel.Act, Is.EqualTo(act));
            Assert.That(duel.CycleUses(counter.Id), Is.Zero);
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));

            LegacySkillFeedback feedback = slot.PlayerFeedback;
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.False);
            Assert.That(slot.PlayerFeedback, Is.SameAs(feedback));
            Assert.That(duel.NextActGain, Is.EqualTo(4));
            duel.ResolveNextHit();
            Assert.That(duel.NextActGain, Is.EqualTo(4), "The subsequent hit does not reapply opening effects.");
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.False, "A resolved slot has no preparation left.");
            duel.CompleteCurrentSlot();
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.False);
        }

        [Test]
        public void DeferredBreak_CanBePresentedBeforeTheAttackDealsHealthDamage()
        {
            var duel = Duel(LegacySkillDefinitions.Skill(600), LegacySkillDefinitions.Skill(500), 1);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(duel.Player.IsResistanceBroken, Is.False, "The enemy's effects still wait for the counter decision.");
            Assert.That(slot.EnemyFeedback.OpponentResistanceReduced, Is.Zero);
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.True);
            Assert.That(duel.Player.IsResistanceBroken, Is.True);
            Assert.That(slot.EnemyFeedback.OpponentResistanceReduced, Is.EqualTo(3000));
            Assert.That(duel.Player.Health, Is.EqualTo(1000));
            Assert.That(duel.Enemy.Health, Is.EqualTo(1000));
            Assert.That(slot.HitsResolved, Is.Zero);
            Assert.That(slot.HitCount, Is.EqualTo(5));
            Assert.That(duel.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(1));
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.False);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerHealthDamage, Is.GreaterThan(0), "The actual attack is a separate later operation.");
            Assert.That(slot.HitsResolved, Is.EqualTo(1));
        }

        [TestCase(1, false)]
        [TestCase(42, false)]
        [TestCase(2026, false)]
        [TestCase(1, true)]
        [TestCase(42, true)]
        [TestCase(2026, true)]
        public void StagedAndImmediateResolution_HaveIdenticalHitsStateAndLaterRandomRolls(int seed, bool breaks)
        {
            LegacySkill counter = Attack(991, 2, 13, 2);
            LegacySkill enemy = breaks ? LegacySkillDefinitions.Skill(500) : Attack(990, 3, 17, 3);
            var staged = Duel(counter, enemy, 3, seed, 3);
            var immediate = Duel(counter, enemy, 3, seed, 3);
            ResolvePair(staged, immediate);
            staged.BeginNextTurn();
            immediate.BeginNextTurn();
            ResolvePair(staged, immediate);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DodgeWithdrawal_BeforePreparationKeepsTheCounterAndMatchesImmediateResolution(bool succeeds)
        {
            LegacySkill counter = Attack(991, 2, 13, 4);
            LegacySkill enemy = Attack(990, 3, 17, 2);
            var staged = Duel(counter, enemy, 2, 42);
            var immediate = Duel(counter, enemy, 2, 42);
            staged.Commit();
            immediate.Commit();
            LegacyCurrentSlot stagedSlot = staged.BeginNextSlot();
            immediate.BeginNextSlot();
            Assert.That(stagedSlot.HitCount, Is.EqualTo(4));
            Assert.That(staged.TryStep(LegacyStepAction.Dodge, succeeds, out bool stagedSuccess), Is.True);
            Assert.That(immediate.TryStep(LegacyStepAction.Dodge, succeeds, out bool immediateSuccess), Is.True);
            Assert.That(stagedSuccess, Is.EqualTo(immediateSuccess));
            Assert.That(stagedSlot.PendingPlayerCounter, Is.Null);
            Assert.That(stagedSlot.HitCount, Is.EqualTo(2));
            Assert.That(staged.PrepareCurrentSlotImpact(), Is.True, "The enemy's deferred effects still settle after withdrawing the counter.");
            Assert.That(staged.PlayerCountersRemaining, Is.EqualTo(1));
            Assert.That(stagedSlot.PlayerCountered, Is.False);
            ResolveCurrentPair(staged, immediate);
            Assert.That(staged.PlayerCountersRemaining, Is.EqualTo(1));
            Assert.That(staged.BeginNextSlot().PendingPlayerCounter, Is.SameAs(counter));
            immediate.BeginNextSlot();
            Assert.That(staged.PrepareCurrentSlotImpact(), Is.True);
            ResolveCurrentPair(staged, immediate);
            AssertState(staged, immediate);
        }

        [Test]
        public void OrdinarySlot_DoesNotReapplyItsOpeningEffects()
        {
            LegacySkill slash = LegacySkillDefinitions.Skill(1);
            var duel = new LegacyQueuedDuel(1000, 3000, 1000, 3000, new[] { slash },
                new[] { Attack(990, 3, 11) }, new[] { 1 }, 42);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            LegacySkillFeedback feedback = slot.PlayerFeedback;
            Assert.That(duel.NextActGain, Is.EqualTo(4));
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.False);
            Assert.That(slot.PlayerFeedback, Is.SameAs(feedback));
            Assert.That(duel.NextActGain, Is.EqualTo(4));
            Assert.That(slot.HitsResolved, Is.Zero);
        }

        private static void ResolvePair(LegacyQueuedDuel staged, LegacyQueuedDuel immediate)
        {
            staged.Commit();
            immediate.Commit();
            while (!staged.IsTurnResolved)
            {
                staged.BeginNextSlot();
                immediate.BeginNextSlot();
                staged.PrepareCurrentSlotImpact();
                Assert.That(staged.PrepareCurrentSlotImpact(), Is.False);
                ResolveCurrentPair(staged, immediate);
            }
            AssertState(staged, immediate);
        }

        private static void ResolveCurrentPair(LegacyQueuedDuel staged, LegacyQueuedDuel immediate)
        {
            while (!staged.CurrentSlot.IsResolved)
            {
                LegacyHitResult actual = staged.ResolveNextHit();
                LegacyHitResult expected = immediate.ResolveNextHit();
                Assert.That(new[] { actual.HitIndex, actual.PlayerHealthDamage, actual.EnemyHealthDamage,
                    actual.PlayerResistanceDamage, actual.EnemyResistanceDamage, actual.PlayerPushPower,
                    actual.EnemyPushPower, actual.PlayerDisplayedDamage, actual.EnemyDisplayedDamage,
                    actual.PlayerBleedingHealthDamage, actual.EnemyBleedingHealthDamage },
                    Is.EqualTo(new[] { expected.HitIndex, expected.PlayerHealthDamage, expected.EnemyHealthDamage,
                    expected.PlayerResistanceDamage, expected.EnemyResistanceDamage, expected.PlayerPushPower,
                    expected.EnemyPushPower, expected.PlayerDisplayedDamage, expected.EnemyDisplayedDamage,
                    expected.PlayerBleedingHealthDamage, expected.EnemyBleedingHealthDamage }));
                Assert.That(new[] { actual.PlayerAttacked, actual.EnemyAttacked, actual.PlayerDodged,
                    actual.PlayerPressured, actual.IsFinalHit }, Is.EqualTo(new[] { expected.PlayerAttacked,
                    expected.EnemyAttacked, expected.PlayerDodged, expected.PlayerPressured, expected.IsFinalHit }));
                Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
                AssertState(staged, immediate);
            }
            Assert.That(immediate.CurrentSlot.IsResolved, Is.True);
            staged.CompleteCurrentSlot();
            immediate.CompleteCurrentSlot();
            AssertState(staged, immediate);
        }

        private static void AssertState(LegacyQueuedDuel actual, LegacyQueuedDuel expected)
        {
            Assert.That(new[] { actual.Player.Health, actual.Enemy.Health, actual.Player.Resistance,
                actual.Enemy.Resistance, actual.PlayerCountersRemaining, actual.EnemyCountersRemaining,
                actual.Act, actual.NextActGain, actual.RoundNumber, actual.PlayerBreakCanceledFromSlotIndex,
                actual.EnemyBreakCanceledFromSlotIndex }, Is.EqualTo(new[] { expected.Player.Health,
                expected.Enemy.Health, expected.Player.Resistance, expected.Enemy.Resistance,
                expected.PlayerCountersRemaining, expected.EnemyCountersRemaining, expected.Act,
                expected.NextActGain, expected.RoundNumber, expected.PlayerBreakCanceledFromSlotIndex,
                expected.EnemyBreakCanceledFromSlotIndex }));
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
            Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
        }

        private static LegacyQueuedDuel Duel(LegacySkill counter, LegacySkill enemy, int count, int seed = 42,
            int counterUses = 1)
            => new LegacyQueuedDuel(1000, 3000, 1000, 3000, new[] { Attack(992, 2, 7) },
                new[] { enemy }, new[] { count }, seed, playerCounter: new LegacyCounter(counter, counterUses));

        private static LegacySkill Attack(int id, int min, int max, int hits = 1)
            => new LegacySkill(id, "Deferred impact test", 1, min, max, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty);
    }
}
