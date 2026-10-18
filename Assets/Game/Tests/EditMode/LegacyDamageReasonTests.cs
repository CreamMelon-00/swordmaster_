using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacyDamageReasonTests
    {
        [Test]
        public void BreakingHit_HasNoBrokenMultiplier_ButTheCurrentSkillsFollowupDoes()
        {
            LegacyQueuedDuel duel = Duel(Attack(900, 10, 2), Attack(901, 1), resistance: 5);
            Begin(duel);
            LegacyHitResult breaking = duel.ResolveNextHit();
            Assert.That(breaking.EnemyResistanceDamage, Is.EqualTo(5));
            Assert.That(breaking.EnemyDisplayedDamage, Is.Zero);
            Assert.That(breaking.EnemyDamageReasons, Is.EqualTo(LegacyDamageReason.None));
            Assert.That(duel.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(1));
            Assert.That(duel.CurrentSlot.IsResolved, Is.False, "A break only cancels later skills, not this skill's hits.");
            LegacyHitResult followup = duel.ResolveNextHit();
            Assert.That(followup.EnemyHealthDamage, Is.EqualTo(10));
            Assert.That(followup.EnemyDamageReasons, Is.EqualTo(LegacyDamageReason.BrokenDouble));
        }

        [Test]
        public void BrokenTargetBonus_IsRecordedOnlyAfterTheBreakingHit()
        {
            LegacyQueuedDuel duel = Duel(LegacySkillDefinitions.Skill(44), Attack(901, 1), resistance: 1);
            Begin(duel);
            LegacyHitResult breaking = duel.ResolveNextHit();
            Assert.That(breaking.EnemyDamageReasons, Is.EqualTo(LegacyDamageReason.None));
            LegacyHitResult followup = duel.ResolveNextHit();
            Assert.That(followup.EnemyDamageReasons,
                Is.EqualTo(LegacyDamageReason.BrokenDouble | LegacyDamageReason.BrokenBonus));
        }

        [TestCase(1, 2, 5, LegacyDamageReason.None)]
        [TestCase(4, 1, 6, LegacyDamageReason.GuardReduction)]
        [TestCase(20, 1, 0, LegacyDamageReason.GuardReduction)]
        public void GuardReason_RequiresAnActualReduction_AndIncludesAFullBlock(int guard, int hits, int expectedDamage,
            LegacyDamageReason expected)
        {
            // Every non-wait skill rolls at least 1. A 1 guard split over two attack hits gives zero guard per hit.
            LegacyQueuedDuel duel = Duel(Attack(900, 10, hits), Guard(901, guard));
            Begin(duel);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyHealthDamage, Is.EqualTo(expectedDamage));
            Assert.That(hit.EnemyDamageReasons, Is.EqualTo(expected));
            Assert.That(hit.PlayerDamageReasons, Is.EqualTo(LegacyDamageReason.None));
        }

        [Test]
        public void PressurePassingAFullGuard_OnABrokenTarget_DoesNotClaimBrokenDouble()
        {
            LegacyQueuedDuel duel = Duel(Attack(900, 12, 3), Guard(901, 30), resistance: 0);
            Begin(duel);
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out bool succeeded) && succeeded, Is.True);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyHealthDamage, Is.EqualTo(4));
            Assert.That(hit.EnemyDamageReasons,
                Is.EqualTo(LegacyDamageReason.GuardReduction | LegacyDamageReason.PressureBonus));
            Assert.That(LegacyDamageReasonText.Primary(hit.EnemyDamageReasons), Is.EqualTo("압박 추가 피해"));
        }

        [TestCase(4, 0, LegacyDamageReason.GuardReduction)]
        [TestCase(8, 2, LegacyDamageReason.GuardReduction | LegacyDamageReason.PressureGuard)]
        public void DefensivePressure_ReportsItsHalvingOnlyWhenItChangesDamage(int power, int expectedDamage,
            LegacyDamageReason expectedReasons)
        {
            LegacyQueuedDuel duel = Duel(Guard(900, 4), Attack(901, power));
            Begin(duel);
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out bool succeeded) && succeeded, Is.True);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerHealthDamage, Is.EqualTo(expectedDamage));
            Assert.That(hit.PlayerDamageReasons, Is.EqualTo(expectedReasons));
        }

        [TestCase(1, 0, LegacyDamageReason.ProtectionReduction)]
        [TestCase(20, 10, LegacyDamageReason.ProtectionReduction)]
        public void Protection_ReportsOnlyAChangeThatSurvivesRounding(int power, int expectedDamage,
            LegacyDamageReason expectedReasons)
        {
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50,
                new[] { Guard(8, 100) }, new[] { Attack(901, power) }, new[] { 2 }, meshPercent: 0);
            // A breath has no guard mitigation, so the following slot isolates protection and its rounding.
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueBreath(), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            duel.BeginNextSlot();
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerHealthDamage, Is.EqualTo(expectedDamage));
            Assert.That(hit.PlayerDamageReasons, Is.EqualTo(expectedReasons));
        }

        [TestCase(1000, false)]
        [TestCase(10, true)]
        public void ConditionalMultiplier_RequiresHealthDamage_AndDoesNotImplyBrokenDouble(int resistance, bool overflows)
        {
            var duel = new LegacyQueuedDuel(1000, resistance, 1000, 50, new[] { Attack(900, 1) },
                new[] { Attack(901, 700), LegacySkillDefinitions.Skill(502) }, new[] { 2 }, meshPercent: 0);
            Assert.That(duel.TryQueueBreath() && duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.Player.Health, Is.EqualTo(300));
            duel.BeginNextSlot();
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyAttackConditionMet, Is.True);
            Assert.That(hit.PlayerDamageReasons, Is.EqualTo(overflows
                ? LegacyDamageReason.ConditionalMultiplier : LegacyDamageReason.None));
            Assert.That(hit.PlayerHealthDamage > 0, Is.EqualTo(overflows));
        }

        [Test]
        public void Dodge_ReportsNoDamageReasonsForTheAvoidedAttack()
        {
            LegacyQueuedDuel duel = Duel(Guard(900, 4), Attack(901, 8), resistance: 0);
            Begin(duel);
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool succeeded) && succeeded, Is.True);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerDisplayedDamage, Is.Zero);
            Assert.That(hit.PlayerDamageReasons, Is.EqualTo(LegacyDamageReason.None));
        }

        [Test]
        public void VersusReasons_FollowTheRecipient_WhenViewedByHostAndGuest()
        {
            var match = new LocalVersusMatch(new[] { Attack(900, 10) }, new[] { Guard(901, 4) },
                1000, 0, 1000, 0, randomSeed: 17);
            Assert.That(match.TryQueueLane(0, 0) && match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0) && match.TryCommit(1), Is.True);
            match.BeginNextSlot();
            LocalVersusHit hit = match.ResolveNextHit();
            LegacyDamageReason expected = LegacyDamageReason.GuardReduction | LegacyDamageReason.BrokenDouble;
            Assert.That(hit.RightHealthDamage, Is.EqualTo(12));
            Assert.That(hit.RightDamageReasons, Is.EqualTo(expected));
            Assert.That(hit.LeftDamageReasons, Is.EqualTo(LegacyDamageReason.None));
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(host.Hit(hit).EnemyDamageReasons, Is.EqualTo(expected));
            Assert.That(guest.Hit(hit).PlayerDamageReasons, Is.EqualTo(expected));
            Assert.That(host.Hit(hit).PlayerDamageReasons, Is.EqualTo(LegacyDamageReason.None));
            Assert.That(guest.Hit(hit).EnemyDamageReasons, Is.EqualTo(LegacyDamageReason.None));
        }

        [Test]
        public void GroupBattle_ReasonsUseTheCurrentlyFacingEnemy_WithoutChangingTheOtherEnemies()
        {
            var script = new EnemyScript(new[] { new[] { Guard(910, 4) } });
            var duel = new LegacyQueuedDuel(1000, 50, new[] { Attack(900, 10) }, new[]
            {
                new LegacyEnemyDefinition("broken", 1000, 0, script),
                new LegacyEnemyDefinition("whole 1", 1000, 50, script),
                new LegacyEnemyDefinition("whole 2", 1000, 50, script)
            }, randomSeed: 17, meshPercent: 0);
            for (int slot = 0; slot < 3; slot++) Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            for (int slot = 0; slot < 3; slot++)
            {
                int owner = duel.EnemyOwnerAtSlot(slot);
                int[] before = { duel.Enemies[0].Health, duel.Enemies[1].Health, duel.Enemies[2].Health };
                duel.BeginNextSlot();
                Assert.That(duel.Enemy, Is.SameAs(duel.Enemies[owner]));
                bool broken = duel.Enemy.IsResistanceBroken;
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.EnemyHealthDamage, Is.EqualTo(broken ? 12 : 6));
                Assert.That(hit.EnemyDamageReasons, Is.EqualTo(LegacyDamageReason.GuardReduction |
                    (broken ? LegacyDamageReason.BrokenDouble : LegacyDamageReason.None)));
                for (int i = 0; i < 3; i++)
                    Assert.That(duel.Enemies[i].Health, Is.EqualTo(before[i] - (i == owner ? hit.EnemyHealthDamage : 0)));
                duel.CompleteCurrentSlot();
                Assert.That(duel.ActiveEnemyIndex, Is.EqualTo(owner), "The presented target stays until the next slot.");
            }
        }

        [TestCase(LegacyDamageReason.None, "")]
        [TestCase(LegacyDamageReason.BrokenDouble, "붕괴 ×2")]
        [TestCase(LegacyDamageReason.BrokenDouble | LegacyDamageReason.BrokenBonus, "붕괴 추가 피해")]
        [TestCase(LegacyDamageReason.BrokenDouble | LegacyDamageReason.ConditionalMultiplier, "조건 강화")]
        [TestCase(LegacyDamageReason.GuardReduction | LegacyDamageReason.PressureGuard, "방어 압박")]
        public void PrimaryReason_ShowsOneRelevantCause(LegacyDamageReason reasons, string expected)
            => Assert.That(LegacyDamageReasonText.Primary(reasons), Is.EqualTo(expected));

        [Test]
        public void PrimaryReason_DescribesTheAttackerDefinition_OnlyForARecordedCause()
        {
            LegacySkill finisher = LegacySkillDefinitions.Skill(50);
            Assert.That(LegacyDamageReasonText.Primary(LegacyDamageReason.ConditionalMultiplier, finisher),
                Is.EqualTo("체력 30% 이하 ×2"));
            Assert.That(LegacyDamageReasonText.Primary(LegacyDamageReason.BrokenBonus, LegacySkillDefinitions.Skill(44)),
                Is.EqualTo("붕괴 추가 +100%"));
            Assert.That(LegacyDamageReasonText.Primary(LegacyDamageReason.None, finisher), Is.Empty);
            Assert.That(LegacyDamageReasonText.Primary(LegacyDamageReason.BrokenDouble, finisher), Is.EqualTo("붕괴 ×2"));
        }

        private static LegacyQueuedDuel Duel(LegacySkill player, LegacySkill enemy, int resistance = 50)
            => new LegacyQueuedDuel(1000, resistance, 1000, resistance,
                new[] { player }, new[] { enemy }, new[] { 1 }, meshPercent: 0);

        private static void Begin(LegacyQueuedDuel duel)
        {
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
        }

        private static LegacySkill Attack(int id, int power, int hits = 1)
            => new LegacySkill(id, "attack", 1, power, power,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 1, power, power,
                LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");
    }
}
