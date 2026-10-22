using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacyBleedingTests
    {
        [Test]
        public void SelfGrant_StartsWithTheNextAttack_AndThreeHitsSpendOneTechnique()
        {
            var duel = Duel(new[] { Skill(63, 18), Attack(100, 9, 1, 3) }, Guard(900), Guard(901));
            Queue(duel, 0, 1);
            duel.Commit();
            LegacyCurrentSlot first = duel.BeginNextSlot();
            Assert.That(first.PlayerFeedback.SelfBleedingGranted, Is.EqualTo(2));
            Assert.That(duel.ResolveNextSlot().PlayerBleedingHealthDamage, Is.Zero);
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(2));
            duel.BeginNextSlot();
            for (int i = 0; i < 3; i++)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.PlayerBleedingHealthDamage, Is.EqualTo(2));
                Assert.That(hit.PlayerHealthDamage, Is.EqualTo(2));
                Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(i == 2 ? 1 : 2));
            }
            Assert.That(duel.CompleteCurrentSlot().PlayerBleedingHealthDamage, Is.EqualTo(6));
        }

        [Test]
        public void OpponentGrant_DoesNotHurtAnyHitOfEitherCurrentAttack()
        {
            var duel = Duel(new[] { Skill(64, 6), Guard(100, 1) }, Attack(900, 9, hits: 3), Attack(901, 9, hits: 3));
            Queue(duel, 0, 1);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerFeedback.OpponentBleedingGranted, Is.EqualTo(1));
            Assert.That(duel.ResolveNextSlot().EnemyBleedingHealthDamage, Is.Zero);
            Assert.That(Bleeding(duel.EnemyStatuses), Is.EqualTo(1));
            Assert.That(duel.ResolveNextSlot().EnemyBleedingHealthDamage, Is.EqualTo(6));
            Assert.That(Bleeding(duel.EnemyStatuses), Is.Zero);
        }

        [Test]
        public void ExistingAndNewSelfBleeding_AddCounts_ButOnlyTheOldAttackIsSpent()
        {
            var duel = Duel(new[] { Skill(63, 18) }, Guard(900));
            Statuses(duel).GrantBleeding(1);
            Queue(duel, 0);
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().PlayerBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(1000)]
        public void Bleeding_IgnoresBrokenStateAndProtectionVulnerability_AndDoesNotSpendIncomingStatuses(int resistance)
        {
            var duel = new LegacyQueuedDuel(1000, resistance, 1000, 1000, new[] { Attack(100, 9, hits: 3) },
                new[] { Guard(900) }, new[] { 1 }, meshPercent: 0);
            LegacyStatusCollection statuses = Statuses(duel);
            statuses.GrantBleeding(1);
            statuses.Grant(LegacyStatusKind.Protection, 100, 1);
            statuses.Grant(LegacyStatusKind.Vulnerability, 50, 1);
            Queue(duel, 0);
            duel.Commit();
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.PlayerHealthDamage, Is.EqualTo(6));
            Assert.That(result.PlayerResistanceDamage, Is.Zero);
            Assert.That(duel.PlayerStatuses.Select(s => s.Kind), Is.EquivalentTo(new[] { LegacyStatusKind.Protection, LegacyStatusKind.Vulnerability }));
        }

        [Test]
        public void GuardBreathingAndEmptySlots_PreserveBleedingAcrossTurns_ResetClearsIt()
        {
            var duel = Duel(new[] { Guard(100) }, Guard(900), Guard(901), Guard(902));
            Statuses(duel).GrantBleeding(3);
            Queue(duel, 0);
            Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            while (!duel.IsTurnResolved) Assert.That(duel.ResolveNextSlot().PlayerBleedingHealthDamage, Is.Zero);
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(3));
            duel.BeginNextTurn();
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(3));
            duel.Reset();
            Assert.That(duel.PlayerStatuses, Is.Empty);
        }

        [Test]
        public void Hemostasis_PrecedesBothNewGrants_AndCannotActivateFreshBleedingInTheSameAttack()
        {
            // Use the actual hemostasis effect on an attack to cover its generic resolver timing, not only the guard row.
            var duel = Duel(new[] { Attack(66, 9, hits: 3), Attack(100, 9, 1, 3) }, Skill(64, 6), Guard(900));
            Statuses(duel).GrantBleeding(1);
            Queue(duel, 0, 1);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerFeedback.BleedingRemoved, Is.EqualTo(1));
            Assert.That(duel.ResolveNextSlot().PlayerBleedingHealthDamage, Is.Zero);
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(1), "Only the newly inflicted count survives.");
            Assert.That(duel.ResolveNextSlot().PlayerBleedingHealthDamage, Is.EqualTo(6));
        }

        [TestCase(0, 1, 0, 1)]
        [TestCase(0, 3, 0, 1)]
        [TestCase(0, 1, 1, 4)]
        [TestCase(0, 3, 2, 4)]
        [TestCase(1, 1, 0, 1)]
        [TestCase(1, 1, 1, 4)]
        [TestCase(2, 1, 1, 4)]
        [TestCase(3, 1, 1, 4)]
        public void ExistingOpponentBleeding_ReplacesTheBaseCount_RegardlessOfHitsOrAction(int action, int hits, int oldCount, int granted)
        {
            LegacySkill opponent = action == 0 ? Attack(900, 9, hits: hits)
                : action == 1 ? Guard(900) : action == 2 ? LegacyCommonActions.Breathe : null;
            var duel = Duel(new[] { Skill(65, 8) }, opponent == null ? new LegacySkill[0] : new[] { opponent });
            Statuses(duel, false).GrantBleeding(oldCount);
            Queue(duel, 0);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerFeedback.OpponentBleedingGranted, Is.EqualTo(granted));
            bool oldAttack = oldCount > 0 && action == 0;
            Assert.That(duel.ResolveNextSlot().EnemyBleedingHealthDamage, Is.EqualTo(oldAttack ? 2 * hits : 0));
            Assert.That(Bleeding(duel.EnemyStatuses), Is.EqualTo(oldCount + granted - (oldAttack ? 1 : 0)));
        }

        [Test]
        public void ReplacementGrant_ReadsExistingOpponentBleedingBeforeHemostasis()
        {
            var duel = Duel(new[] { Skill(65, 8) }, Skill(66, 5));
            Statuses(duel, false).GrantBleeding(1);
            Queue(duel, 0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemyFeedback.BleedingRemoved, Is.EqualTo(1));
            Assert.That(slot.PlayerFeedback.OpponentBleedingGranted, Is.EqualTo(4));
            Assert.That(duel.ResolveNextSlot().EnemyBleedingHealthDamage, Is.Zero);
            Assert.That(Bleeding(duel.EnemyStatuses), Is.EqualTo(4));
        }

        [Test]
        public void SameSlotNewSelfGrant_CannotEnableTheOpponentsReplacementGrant()
        {
            var duel = Duel(new[] { Skill(65, 8) }, Skill(63, 18));
            Queue(duel, 0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemyFeedback.SelfBleedingGranted, Is.EqualTo(2));
            Assert.That(slot.PlayerFeedback.OpponentBleedingGranted, Is.EqualTo(1));
            Assert.That(duel.ResolveNextSlot().EnemyBleedingHealthDamage, Is.Zero);
            Assert.That(Bleeding(duel.EnemyStatuses), Is.EqualTo(3));
        }

        [Test]
        public void ConditionalBasePower_IsAddedBeforePercentageAndPerHitFloor()
        {
            var duel = Duel(new[] { Attack(68, 9, hits: 3) }, Attack(900, 1));
            Statuses(duel).GrantBleeding(3);
            Statuses(duel).Grant(LegacyStatusKind.PowerUp, 50, 1);
            Queue(duel, 0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.SelfBleedingPowerBonus, Is.EqualTo(12));
            Assert.That(slot.PlayerFeedback.PowerBuffPercent, Is.EqualTo(50));
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo(30), "floor((9 + 12) * 1.5 / 3) * 3");
            Assert.That(result.PlayerBleedingHealthDamage, Is.EqualTo(6));
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(2));
        }

        [Test]
        public void OpponentBleedingCondition_ReadsBeforeHemostasis_NotThePostEffectCount()
        {
            var duel = Duel(new[] { Skill(67, 16) }, Skill(66, 5));
            Statuses(duel, false).GrantBleeding(3);
            Queue(duel, 0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.OpponentBleedingPowerBonus, Is.EqualTo(16));
            Assert.That(slot.EnemyFeedback.BleedingRemoved, Is.EqualTo(2));
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(27), "16 + 16 base power minus 5 guard");
            Assert.That(Bleeding(duel.EnemyStatuses), Is.EqualTo(1));
        }

        [Test]
        public void NewOpponentGrant_CannotSatisfySameSlotBleedingPowerCondition()
        {
            var duel = Duel(new[] { Skill(68, 9) }, Skill(65, 7));
            Statuses(duel).GrantBleeding(2);
            Queue(duel, 0);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerFeedback.SelfBleedingPowerBonus, Is.Zero);
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(9));
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(5));
        }

        [Test]
        public void Counter_UsesExistingBleedingForEveryHit_AndPreservesTheOpponentsNewGrant()
        {
            var counter = new LegacyCounter(Attack(100, 9, hits: 3));
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Guard(101) },
                new[] { Skill(64, 6) }, new[] { 1 }, playerCounter: counter, meshPercent: 0);
            Statuses(duel).GrantBleeding(1);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PendingPlayerCounter, Is.Not.Null);
            Assert.That(duel.ResolveNextSlot().PlayerBleedingHealthDamage, Is.EqualTo(6));
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(1));
        }

        [Test]
        public void DodgeCanceledCounter_DoesNotSpendBleeding_ButTheDodgedAttackStillBleeds()
        {
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Guard(101) },
                new[] { Skill(64, 6) }, new[] { 1 }, playerCounter: new LegacyCounter(Attack(100, 9)), meshPercent: 0);
            Statuses(duel).GrantBleeding(1);
            Statuses(duel, false).GrantBleeding(1);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool success) && success, Is.True);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerAttacked, Is.False);
            Assert.That(hit.PlayerBleedingHealthDamage, Is.Zero);
            Assert.That(hit.EnemyBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(2));
            Assert.That(Bleeding(duel.EnemyStatuses), Is.Zero);
        }

        [Test]
        public void EnemyCounter_AlsoBleeds_AndFreshOpponentGrantWaitsUntilItsNextAttack()
        {
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Skill(64, 6) },
                new LegacySkill[0], new[] { 0 }, enemyCounter: new LegacyCounter(Attack(100, 9, hits: 3)), meshPercent: 0);
            Statuses(duel, false).GrantBleeding(1);
            Queue(duel, 0);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().EnemyCountered, Is.True);
            Assert.That(duel.ResolveNextSlot().EnemyBleedingHealthDamage, Is.EqualTo(6));
            Assert.That(Bleeding(duel.EnemyStatuses), Is.EqualTo(1));
        }

        [Test]
        public void SimultaneousBleedingKO_StopsLaterHits_AfterBothFirstAttacks_AndKeepsCampaignOutcomePolicy()
        {
            var duel = new LegacyQueuedDuel(2, 1000, 2, 1000, new[] { Attack(100, 9, hits: 3) },
                new[] { Attack(101, 9, hits: 3) }, new[] { 1 }, meshPercent: 0);
            Statuses(duel).GrantBleeding(2);
            Statuses(duel, false).GrantBleeding(2);
            Queue(duel, 0);
            duel.Commit();
            duel.BeginNextSlot();
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerResistanceDamage, Is.EqualTo(3));
            Assert.That(hit.EnemyResistanceDamage, Is.EqualTo(3));
            Assert.That(hit.PlayerBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(hit.EnemyBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(hit.IsFinalHit, Is.True);
            Assert.That(hit.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(1));
            Assert.That(Bleeding(duel.EnemyStatuses), Is.EqualTo(1));
        }

        [Test]
        public void HealthFloor_ClampsBleedingActualDamage_WithoutSkippingCountConsumption()
        {
            var duel = new LegacyQueuedDuel(2, 1000, 2, 1000, new[] { Attack(100, 9, hits: 3) },
                new[] { Attack(101, 9, hits: 3) }, new[] { 1 }, playerHealthFloor: 1, enemyHealthFloor: 1, meshPercent: 0);
            Statuses(duel).GrantBleeding(1);
            Statuses(duel, false).GrantBleeding(1);
            Queue(duel, 0);
            duel.Commit();
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.PlayerBleedingHealthDamage, Is.EqualTo(1));
            Assert.That(result.EnemyBleedingHealthDamage, Is.EqualTo(1));
            Assert.That(duel.Player.Health, Is.EqualTo(1));
            Assert.That(duel.Enemy.Health, Is.EqualTo(1));
            Assert.That(Bleeding(duel.PlayerStatuses), Is.Zero);
        }

        [Test]
        public void ForecastAndBladeBind_CopyBleedingWithoutMutatingOrIgnoringSelfDamage()
        {
            var duel = Duel(new[] { Attack(100, 9, hits: 3) }, Attack(101, 9, hits: 3));
            Statuses(duel).GrantBleeding(2);
            Queue(duel, 0);
            LegacySlotForecast forecast = duel.ForecastTurn()[0];
            Assert.That(forecast.PlayerHealthDamage, Is.EqualTo(6));
            Assert.That(duel.Player.Health, Is.EqualTo(1000));
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(2));
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.CanBindCurrentHit(), Is.False);
            Assert.That(duel.Player.Health, Is.EqualTo(1000));
            Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.EqualTo(forecast.PlayerHealthDamage));
        }

        [Test]
        public void BreakCanceledFutureAttack_DoesNotSpendAnotherBleedingCount()
        {
            var duel = new LegacyQueuedDuel(1000, 1, 1000, 1000, new[] { Attack(100, 3) },
                new[] { Attack(101, 3), Guard(102) }, new[] { 2 }, meshPercent: 0);
            Statuses(duel).GrantBleeding(3);
            Queue(duel, 0, 0);
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().PlayerBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(duel.ResolveNextSlot().PlayerBleedingHealthDamage, Is.Zero);
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(2));
        }

        [Test]
        public void AutomaticDodge_DoesNotPreventTheOpponentsOwnBleedingOrSpendIncomingVulnerability()
        {
            var duel = Duel(new[] { Skill(54, 6) }, Attack(100, 9, hits: 3));
            Statuses(duel).Grant(LegacyStatusKind.Vulnerability, 50, 1);
            Statuses(duel, false).GrantBleeding(1);
            Queue(duel, 0);
            duel.Commit();
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.PlayerHealthDamage + result.PlayerResistanceDamage, Is.Zero);
            Assert.That(result.EnemyBleedingHealthDamage, Is.EqualTo(6));
            Assert.That(Bleeding(duel.EnemyStatuses), Is.Zero);
            Assert.That(duel.PlayerStatuses.Single().Kind, Is.EqualTo(LegacyStatusKind.Vulnerability));
        }

        [Test]
        public void InterruptedMultiHit_SpendsOneCountForTheExecutedAttack_AndNeverItsUnexecutedHits()
        {
            var duel = Duel(new[] { Attack(100, 9, hits: 3) }, Guard(900));
            Statuses(duel).GrantBleeding(2);
            Queue(duel, 0);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.ResolveNextHit().PlayerBleedingHealthDamage, Is.EqualTo(2));
            LegacySlotResult settled = duel.InterruptResolvingTurn();
            Assert.That(settled.PlayerBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(Bleeding(duel.PlayerStatuses), Is.EqualTo(1));
            Assert.That(duel.Player.Health, Is.EqualTo(998));
        }

        [Test]
        public void EnemyRoster_BleedingAndForecastCopiesBelongToEachIndividualOwner()
        {
            var enemies = new[]
            {
                new LegacyEnemyDefinition("A", 1000, 1000, new EnemyScript(new[] { new[] { Attack(100, 9, hits: 3) } })),
                new LegacyEnemyDefinition("B", 1000, 1000, new EnemyScript(new[] { new[] { Attack(101, 9, hits: 3) } })),
            };
            var duel = new LegacyQueuedDuel(1000, 1000, new[] { Guard(102) }, enemies, meshPercent: 0);
            var statuses = (LegacyStatusCollection[])typeof(LegacyQueuedDuel)
                .GetField("enemyStatusesByOwner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duel);
            statuses[0].GrantBleeding(2);
            LegacySlotForecast[] forecast = duel.ForecastTurn().ToArray();
            Assert.That(forecast.Sum(slot => slot.EnemyHealthDamage), Is.EqualTo(6));
            Assert.That(Bleeding(duel.GetEnemyStatuses(0)), Is.EqualTo(2));
            Assert.That(Bleeding(duel.GetEnemyStatuses(1)), Is.Zero);
            duel.Commit();
            int index = 0;
            while (!duel.IsTurnResolved)
            {
                int owner = duel.EnemyIndexForSlot(index++);
                Assert.That(duel.ResolveNextSlot().EnemyBleedingHealthDamage, Is.EqualTo(owner == 0 ? 6 : 0));
            }
            Assert.That(duel.Enemies[0].Health, Is.EqualTo(994));
            Assert.That(duel.Enemies[1].Health, Is.EqualTo(1000));
            Assert.That(Bleeding(duel.GetEnemyStatuses(0)), Is.EqualTo(1));
            duel.Reset();
            Assert.That(duel.GetEnemyStatuses(0), Is.Empty);
            Assert.That(duel.GetEnemyStatuses(1), Is.Empty);
        }

        [Test]
        public void Versus_BothSidesGrantAndBleedIdentically_AndPerspectiveReportsTheOwnersDamage()
        {
            var skills = new[] { Skill(63, 18), Attack(100, 9, 1, 3) };
            var match = Match(skills, skills);
            QueueBoth(match, new[] { 0, 1 }, new[] { 0, 1 });
            Assert.That(match.ResolveNextSlot().LeftBleedingHealthDamage, Is.Zero);
            Assert.That(Bleeding(match.Left.Statuses), Is.EqualTo(2));
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(match.CanBindCurrentHit(), Is.False);
            Assert.That(slot.LeftBleedingDamagePerHit, Is.EqualTo(2));
            LocalVersusHit hit = match.ResolveNextHit();
            Assert.That(hit.LeftBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(hit.RightBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(new LocalVersusPerspective(match, 0).Hit(hit).PlayerBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(new LocalVersusPerspective(match, 1).Hit(hit).EnemyBleedingHealthDamage, Is.EqualTo(2));
            while (!slot.IsResolved) match.ResolveNextHit();
            LocalVersusSlotResult result = match.CompleteCurrentSlot();
            Assert.That(result.LeftBleedingHealthDamage, Is.EqualTo(6));
            Assert.That(result.RightBleedingHealthDamage, Is.EqualTo(6));
            Assert.That(Bleeding(match.Left.Statuses), Is.EqualTo(1));
            Assert.That(Bleeding(match.Right.Statuses), Is.EqualTo(1));
        }

        [Test]
        public void Versus_ForecastCopiesBleedingAndConditionalBasePower_ThenResetClearsBothOwners()
        {
            var match = Match(new[] { Skill(68, 9) }, new[] { Attack(100, 3) });
            Statuses(match.Left).GrantBleeding(3);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            var forecast = new List<LegacySlotForecast>();
            match.ForecastTurn(0, forecast);
            Assert.That(forecast[0].PlayerHealthDamage, Is.EqualTo(2));
            Assert.That(forecast[0].EnemyResistanceDamage, Is.EqualTo(21));
            Assert.That(Bleeding(match.Left.Statuses), Is.EqualTo(3));
            Assert.That(match.TryCommit(1), Is.True);
            Assert.That(match.BeginNextSlot().LeftFeedback.SelfBleedingPowerBonus, Is.EqualTo(12));
            Assert.That(match.ResolveNextSlot().RightResistanceDamage, Is.EqualTo(21));
            match.Reset();
            Assert.That(match.Left.Statuses, Is.Empty);
            Assert.That(match.Right.Statuses, Is.Empty);
        }

        [Test]
        public void Versus_HemostasisRemovesOldBleedingBeforeTheOpponentsFreshGrant()
        {
            var match = Match(new[] { Attack(66, 9, hits: 3) }, new[] { Skill(64, 6) });
            Statuses(match.Left).GrantBleeding(1);
            Statuses(match.Right).GrantBleeding(1);
            QueueBoth(match, new[] { 0 }, new[] { 0 });
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(slot.LeftFeedback.BleedingRemoved, Is.EqualTo(1));
            Assert.That(slot.RightFeedback.OpponentBleedingGranted, Is.EqualTo(1));
            LocalVersusSlotResult result = match.ResolveNextSlot();
            Assert.That(result.LeftBleedingHealthDamage, Is.Zero);
            Assert.That(result.RightBleedingHealthDamage, Is.EqualTo(2));
            Assert.That(Bleeding(match.Left.Statuses), Is.EqualTo(1));
            Assert.That(Bleeding(match.Right.Statuses), Is.Zero);
        }

        [TestCase(0, 0, false)]
        [TestCase(0, 1, false)]
        [TestCase(1, 0, false)]
        [TestCase(1, 1, false)]
        [TestCase(0, 0, true)]
        [TestCase(0, 1, true)]
        [TestCase(1, 0, true)]
        [TestCase(1, 1, true)]
        public void Versus_ReplacementGrantUsesStartingBleeding_FromEitherPerspectiveAndInForecast(
            int deepPlayer, int oldCount, bool hemostasis)
        {
            LegacySkill deep = Skill(65, 8);
            LegacySkill other = Skill(hemostasis ? 66 : 63, hemostasis ? 5 : 18);
            var match = Match(new[] { deepPlayer == 0 ? deep : other }, new[] { deepPlayer == 1 ? deep : other });
            LocalVersusFighter recipient = match.GetFighter(1 - deepPlayer);
            Statuses(recipient).GrantBleeding(oldCount);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            var forecast = new List<LegacySlotForecast>();
            match.ForecastTurn(deepPlayer, forecast);
            Assert.That(Bleeding(recipient.Statuses), Is.EqualTo(oldCount));
            Assert.That(match.TryCommit(1), Is.True);
            LocalVersusSlot slot = match.BeginNextSlot();
            int granted = oldCount > 0 ? 4 : 1;
            Assert.That((deepPlayer == 0 ? slot.LeftFeedback : slot.RightFeedback).OpponentBleedingGranted, Is.EqualTo(granted));
            Assert.That(new LocalVersusPerspective(match, deepPlayer).CurrentSlot.PlayerFeedback.OpponentBleedingGranted, Is.EqualTo(granted));
            LocalVersusSlotResult result = match.ResolveNextSlot();
            Assert.That(deepPlayer == 0 ? result.LeftHealthDamage : result.RightHealthDamage, Is.EqualTo(forecast[0].PlayerHealthDamage));
            Assert.That(deepPlayer == 0 ? result.RightHealthDamage : result.LeftHealthDamage, Is.EqualTo(forecast[0].EnemyHealthDamage));
            int remaining = (hemostasis ? 0 : oldCount) + granted + (hemostasis ? 0 : 2) - (!hemostasis && oldCount > 0 ? 1 : 0);
            Assert.That(Bleeding(recipient.Statuses), Is.EqualTo(remaining));
        }

        [Test]
        public void Versus_SimultaneousBleedingKO_IsADraw_AndBothAttacksHappenFirst()
        {
            var match = new LocalVersusMatch(new[] { Attack(100, 9, hits: 3) }, new[] { Attack(101, 9, hits: 3) },
                leftHealth: 2, rightHealth: 2, leftResistance: 1000, rightResistance: 1000);
            Statuses(match.Left).GrantBleeding(1);
            Statuses(match.Right).GrantBleeding(1);
            QueueBoth(match, new[] { 0 }, new[] { 0 });
            match.BeginNextSlot();
            LocalVersusHit hit = match.ResolveNextHit();
            Assert.That(hit.LeftResistanceDamage, Is.EqualTo(3));
            Assert.That(hit.RightResistanceDamage, Is.EqualTo(3));
            Assert.That(hit.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
            Assert.That(hit.IsFinalHit, Is.True);
        }

        private static LegacySkill Skill(int id, int power, int lane = 0)
        {
            LegacySkill basis = LegacySkillDefinitions.Skill(id);
            return new LegacySkill(id, basis.Name, 0, power, power, basis.Kind, basis.Property, basis.AttackCount, lane, "");
        }
        private static LegacySkill Attack(int id, int power, int lane = 0, int hits = 1)
            => new LegacySkill(id, "attack", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, lane, "");
        private static LegacySkill Guard(int id, int lane = 0)
            => new LegacySkill(id, "guard", 0, 1000, 1000, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, lane, "");
        private static LegacyQueuedDuel Duel(LegacySkill[] player, params LegacySkill[] enemy)
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { enemy.Length }, meshPercent: 0);
        private static LocalVersusMatch Match(LegacySkill[] left, LegacySkill[] right)
            => new LocalVersusMatch(left, right, leftHealth: 1000, rightHealth: 1000, leftResistance: 1000, rightResistance: 1000);
        private static void Queue(LegacyQueuedDuel duel, params int[] lanes)
        {
            foreach (int lane in lanes) Assert.That(duel.TryQueueLane(lane), Is.True);
        }
        private static void QueueBoth(LocalVersusMatch match, int[] left, int[] right)
        {
            foreach (int lane in left) Assert.That(match.TryQueueLane(0, lane), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            foreach (int lane in right) Assert.That(match.TryQueueLane(1, lane), Is.True);
            Assert.That(match.TryCommit(1), Is.True);
        }
        private static int Bleeding(IReadOnlyList<LegacyStatusSnapshot> statuses)
            => statuses.Where(s => s.Kind == LegacyStatusKind.Bleeding).Sum(s => s.RemainingCount);
        private static LegacyStatusCollection Statuses(LegacyQueuedDuel duel, bool player = true)
            => player ? (LegacyStatusCollection)typeof(LegacyQueuedDuel).GetField("playerStatuses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duel)
                : (LegacyStatusCollection)typeof(LegacyQueuedDuel).GetProperty("enemyStatuses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duel);
        private static LegacyStatusCollection Statuses(LocalVersusFighter fighter)
            => (LegacyStatusCollection)typeof(LocalVersusFighter).GetField("StatusEffects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fighter);
    }
}
