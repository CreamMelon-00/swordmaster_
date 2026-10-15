using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class BladeBindHitEligibilityTests
    {
        [Test]
        public void CampaignPeek_LeavesStateAndFutureRandomRollsUntouched_AndMatchesTheActualHit()
        {
            LegacySkill player = Attack(-30201, 8, 13), enemy = Attack(-30202, 9, 15);
            LegacyQueuedDuel inspected = Campaign(player, enemy, 1000, 1000, 3), control = Campaign(player, enemy, 1000, 1000, 3);
            Assert.That(inspected.CanBindCurrentHit(), Is.False, "No slot has started.");
            inspected.Commit();
            control.Commit();
            for (int index = 0; index < 3; index++)
            {
                inspected.BeginNextSlot();
                control.BeginNextSlot();
                LegacySlotFacts facts = inspected.CurrentSlotFacts;
                var playerBefore = State(inspected.Player);
                var enemyBefore = State(inspected.Enemy);
                int act = inspected.Act, nextAct = inspected.NextActGain;
                for (int read = 0; read < 4; read++) Assert.That(inspected.CanBindCurrentHit(), Is.True);
                Assert.That(inspected.CurrentSlotFacts, Is.EqualTo(facts));
                Assert.That(State(inspected.Player), Is.EqualTo(playerBefore));
                Assert.That(State(inspected.Enemy), Is.EqualTo(enemyBefore));
                Assert.That((inspected.Act, inspected.NextActGain, inspected.CurrentSlot.HitsResolved), Is.EqualTo((act, nextAct, 0)));
                Assert.That(inspected.CurrentClash, Is.EqualTo(control.CurrentClash), "A peek must not advance later slot rolls.");
                LegacyHitResult hit = inspected.ResolveNextHit(), expected = control.ResolveNextHit();
                Assert.That((hit.PlayerHealthDamage, hit.EnemyHealthDamage), Is.EqualTo((0, 0)));
                Assert.That(hit.PlayerResistanceDamage, Is.GreaterThan(0));
                Assert.That(hit.EnemyResistanceDamage, Is.GreaterThan(0));
                Assert.That(inspected.Player.IsResistanceBroken || inspected.Enemy.IsResistanceBroken, Is.False);
                Assert.That((hit.PlayerDisplayedDamage, hit.EnemyDisplayedDamage, hit.Outcome),
                    Is.EqualTo((expected.PlayerDisplayedDamage, expected.EnemyDisplayedDamage, expected.Outcome)));
                Assert.That(inspected.CanBindCurrentHit(), Is.False, "A resolved first hit cannot be held again.");
                inspected.CompleteCurrentSlot();
                control.CompleteCurrentSlot();
            }
        }

        [TestCase(10, 100)]
        [TestCase(100, 10)]
        [TestCase(0, 100)]
        [TestCase(100, 0)]
        public void Campaign_RejectsTheHitThatWouldBreakOrReachHealth(int playerResistance, int enemyResistance)
        {
            LegacyQueuedDuel duel = Campaign(Attack(-30203, 10), Attack(-30204, 10), playerResistance, enemyResistance);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.CanBindCurrentHit(), Is.False);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerHealthDamage > 0 || hit.EnemyHealthDamage > 0 ||
                playerResistance > 0 && duel.Player.IsResistanceBroken || enemyResistance > 0 && duel.Enemy.IsResistanceBroken,
                Is.True, "The actual hit must confirm the excluded health loss or new break.");
        }

        [Test]
        public void Campaign_RejectsSuccessfulDodgeAndPressure_ButNotAnIneffectiveAttempt()
        {
            LegacyQueuedDuel dodged = Campaign(Attack(-30205, 10), Attack(-30206, 10));
            dodged.Commit();
            dodged.BeginNextSlot();
            Assert.That(dodged.TryStep(LegacyStepAction.Dodge, true, out bool dodge) && dodge, Is.True);
            Assert.That(dodged.CanBindCurrentHit(), Is.False);

            LegacyQueuedDuel pressured = Campaign(Attack(-30207, 10), Attack(-30208, 10));
            pressured.Commit();
            pressured.BeginNextSlot();
            Assert.That(pressured.TryStep(LegacyStepAction.Pressure, true, out bool pressure) && pressure, Is.True);
            Assert.That(pressured.CanBindCurrentHit(), Is.False);
            Assert.That(pressured.ResolveNextHit().EnemyHealthDamage, Is.GreaterThan(0), "Pressure reaches health beyond the blades.");

            LegacyQueuedDuel missed = Campaign(Attack(-30209, 10), Attack(-30210, 10));
            missed.Commit();
            missed.BeginNextSlot();
            Assert.That(missed.TryStep(LegacyStepAction.Dodge, false, out bool success), Is.True);
            Assert.That(success, Is.False);
            Assert.That(missed.CanBindCurrentHit(), Is.True, "A failed attempt does not change how the attacks land.");

            LegacyQueuedDuel emptyPressure = Campaign(Attack(-30220, 0), Attack(-30221, 0));
            emptyPressure.Commit();
            emptyPressure.BeginNextSlot();
            Assert.That(emptyPressure.TryStep(LegacyStepAction.Pressure, true, out bool noBonus) && noBonus, Is.True);
            Assert.That(emptyPressure.CanBindCurrentHit(), Is.True, "Zero total power grants no flat pressure damage.");
            LegacyHitResult harmless = emptyPressure.ResolveNextHit();
            Assert.That(harmless.EnemyHealthDamage, Is.Zero);
            Assert.That(harmless.EnemyDisplayedDamage, Is.EqualTo(harmless.EnemyResistanceDamage));
        }

        [Test]
        public void Campaign_ExcludesAnUnrolledPendingCounter_ButAcceptsAnAlreadyFilledEnemyCounter()
        {
            LegacySkill attack = Attack(-30211, 10);
            var pending = new LegacyQueuedDuel(100, 100, 100, 100, new[] { attack }, new[] { attack }, new[] { 1 },
                playerCounter: new LegacyCounter(attack), meshPercent: 0);
            pending.Commit();
            pending.BeginNextSlot();
            Assert.That(pending.CurrentSlot.PendingPlayerCounter, Is.SameAs(attack));
            Assert.That(pending.CanBindCurrentHit(), Is.False);
            Assert.That(pending.CanBindCurrentHit(), Is.False);
            Assert.That((pending.PlayerCountersRemaining, pending.CurrentSlot.HitsResolved), Is.EqualTo((1, 0)));
            Assert.That(pending.CurrentSlot.PlayerSkill, Is.Null);

            var filled = new LegacyQueuedDuel(100, 100, 100, 100, new[] { attack },
                System.Array.Empty<LegacySkill>(), new[] { 0 }, enemyCounter: new LegacyCounter(attack), meshPercent: 0);
            Assert.That(filled.TryQueueLane(0), Is.True);
            filled.Commit();
            filled.BeginNextSlot();
            Assert.That(filled.CurrentSlot.EnemyCountered, Is.True);
            Assert.That(filled.CanBindCurrentHit(), Is.True, "A filled attack counter is an ordinary attack-vs-attack clash.");
            Assert.That(filled.EnemyCountersRemaining, Is.Zero);
            LegacyHitResult hit = filled.ResolveNextHit();
            Assert.That((hit.PlayerHealthDamage, hit.EnemyHealthDamage), Is.EqualTo((0, 0)));
        }

        [Test]
        public void Campaign_OnlyTheFirstHitOfTwoAttacksCanBind_AndAGuardCannot()
        {
            LegacyQueuedDuel duel = Campaign(Attack(-30212, 20, hits: 2), Attack(-30213, 20, hits: 2));
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.CanBindCurrentHit(), Is.True);
            duel.ResolveNextHit();
            Assert.That(duel.IsCurrentSlotResolved, Is.False);
            Assert.That(duel.CanBindCurrentHit(), Is.False);

            var guard = new LegacySkill(-30214, "Guard", 0, 10, 10, LegacySkillKind.Defence, LegacySkillProperty.Defence,
                1, 0, string.Empty, iconId: 1);
            LegacyQueuedDuel guarded = Campaign(Attack(-30215, 10), guard);
            guarded.Commit();
            guarded.BeginNextSlot();
            Assert.That(guarded.CanBindCurrentHit(), Is.False);
        }

        [Test]
        public void VersusPeek_IsReadOnlyFromBothPerspectives_AndDoesNotConsumeLaterRolls()
        {
            LegacySkill left = Attack(-30216, 8, 13), right = Attack(-30217, 9, 15);
            LocalVersusMatch match = Versus(left, right, 1000, 1000, 3), control = Versus(left, right, 1000, 1000, 3);
            for (int index = 0; index < 3; index++)
            {
                match.BeginNextSlot();
                control.BeginNextSlot();
                LegacySlotFacts host = match.CurrentSlotFacts(0), guest = match.CurrentSlotFacts(1);
                var leftBefore = State(match.Left.State);
                var rightBefore = State(match.Right.State);
                for (int read = 0; read < 4; read++) Assert.That(match.CanBindCurrentHit(), Is.True);
                Assert.That(match.CurrentSlotFacts(0), Is.EqualTo(host));
                Assert.That(match.CurrentSlotFacts(1), Is.EqualTo(guest));
                Assert.That(host.Mirrored(), Is.EqualTo(guest));
                Assert.That(State(match.Left.State), Is.EqualTo(leftBefore));
                Assert.That(State(match.Right.State), Is.EqualTo(rightBefore));
                Assert.That(match.CurrentClash(0), Is.EqualTo(control.CurrentClash(0)));
                LocalVersusHit hit = match.ResolveNextHit(), expected = control.ResolveNextHit();
                Assert.That((hit.LeftHealthDamage, hit.RightHealthDamage), Is.EqualTo((0, 0)));
                Assert.That((hit.LeftResistanceDamage, hit.RightResistanceDamage, hit.Outcome),
                    Is.EqualTo((expected.LeftResistanceDamage, expected.RightResistanceDamage, expected.Outcome)));
                Assert.That(match.CanBindCurrentHit(), Is.False);
                match.CompleteCurrentSlot();
                control.CompleteCurrentSlot();
            }
            Assert.That(match.CanBindCurrentHit(), Is.False, "No slot is active between attacks.");
        }

        [TestCase(100, 100, true)]
        [TestCase(12, 100, false)]
        [TestCase(100, 10, false)]
        [TestCase(0, 100, false)]
        public void Versus_HealthAndBreakExclusionsAreSymmetric(int leftResistance, int rightResistance, bool eligible)
        {
            LegacySkill left = Attack(-30218, 10), right = Attack(-30219, 12);
            LocalVersusMatch match = Versus(left, right, leftResistance, rightResistance);
            LocalVersusMatch mirrored = Versus(right, left, rightResistance, leftResistance);
            match.BeginNextSlot();
            mirrored.BeginNextSlot();
            Assert.That(match.CanBindCurrentHit(), Is.EqualTo(eligible));
            Assert.That(mirrored.CanBindCurrentHit(), Is.EqualTo(eligible));
            Assert.That(match.CurrentSlot.HitsResolved, Is.Zero);
            Assert.That(mirrored.CurrentSlot.HitsResolved, Is.Zero);
        }

        private static (int health, int resistance, int floor) State(LegacyFighterState state)
            => (state.Health, state.Resistance, state.HealthFloor);

        private static LegacyQueuedDuel Campaign(LegacySkill player, LegacySkill enemy, int playerResistance = 100,
            int enemyResistance = 100, int count = 1)
        {
            var duel = new LegacyQueuedDuel(100, playerResistance, 100, enemyResistance, new[] { player }, new[] { enemy },
                new[] { count }, randomSeed: 17, meshPercent: 0);
            for (int index = 0; index < count; index++) Assert.That(duel.TryQueueLane(0), Is.True);
            return duel;
        }

        private static LocalVersusMatch Versus(LegacySkill left, LegacySkill right, int leftResistance, int rightResistance,
            int count = 1)
        {
            var match = new LocalVersusMatch(new[] { left }, new[] { right }, 100, leftResistance, 100, rightResistance,
                randomSeed: 17);
            while (match.Phase == LegacyDuelPhase.Planning)
            {
                int planner = match.CurrentPlanner;
                if (match.GetFighter(planner).Queue.Count < count) Assert.That(match.TryQueueLane(planner, 0), Is.True);
                else Assert.That(match.TryPass(planner), Is.True);
            }
            return match;
        }

        private static LegacySkill Attack(int id, int power, int? max = null, int hits = 1)
            => new LegacySkill(id, "Bind test", 0, power, max ?? power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty, iconId: 1);
    }
}
