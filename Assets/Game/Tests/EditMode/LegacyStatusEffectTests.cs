using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacyStatusEffectTests
    {
        [Test]
        public void Vulnerability_IsGrantedAfterTheCurrentAttack_AndSurvivesTheTurnAndActRecovery()
        {
            var duel = Duel(new[] { Skill(12, 4) }, new[] { Attack(900, 8, hits: 2) });
            Queue(duel, 0);
            Assert.That(ResolveTurn(duel).PlayerResistanceDamage, Is.EqualTo(8));
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
            Assert.That(duel.NextActGain, Is.EqualTo(5));
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(8));
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
        }

        [Test]
        public void NonAttacksAndEmptyActions_DoNotSpendIncomingAttackCounts()
        {
            var duel = Duel(new[] { Skill(12, 4) }, new[] { Guard(900, 100) },
                new[] { Guard(901, 100) }, new[] { Guard(902, 100) }, new[] { Attack(903, 4) });
            Queue(duel, 0);
            ResolveTurn(duel);
            duel.BeginNextTurn();
            Assert.That(duel.TryQueueBreath(), Is.True);
            ResolveTurn(duel);
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
            duel.BeginNextTurn();
            ResolveTurn(duel);
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
            duel.BeginNextTurn();
            Assert.That(ResolveTurn(duel).PlayerHealthDamage, Is.EqualTo(6));
            Assert.That(duel.PlayerStatuses, Is.Empty, "An attack received in an empty own slot still counts.");
        }

        [Test]
        public void RepeatedVulnerability_AddsCounts_AndAMultihitAttackSpendsExactlyOne()
        {
            var duel = Duel(new[] { Skill(12, 4) }, new[] { Guard(900, 100), Guard(901, 100) },
                new[] { Attack(902, 12, hits: 3) });
            Queue(duel, 0);
            Queue(duel, 0);
            ResolveTurn(duel);
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 2);
            duel.BeginNextTurn();
            duel.Commit();
            duel.BeginNextSlot();
            for (int hit = 0; hit < 3; hit++)
            {
                Assert.That(duel.ResolveNextHit().PlayerHealthDamage, Is.EqualTo(6));
                AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
            }
            Assert.That(duel.CompleteCurrentSlot().PlayerHealthDamage, Is.EqualTo(18));
        }

        [Test]
        public void RegrantDuringAnAttack_SpendsOneOldCount_AndLeavesTheNewCount()
        {
            var duel = Duel(new[] { Skill(12, 4) }, new[] { Guard(900, 100) }, new[] { Attack(901, 8, hits: 2) });
            Queue(duel, 0);
            ResolveTurn(duel);
            duel.BeginNextTurn();
            Queue(duel, 0);
            Assert.That(ResolveTurn(duel).PlayerResistanceDamage, Is.EqualTo(12));
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
        }

        [Test]
        public void FullyGuardedAttack_StillSpendsOneVulnerabilityCount()
        {
            var duel = Duel(new[] { Skill(12, 4), Guard(100, 100, lane: 1) },
                new[] { Guard(900, 100) }, new[] { Attack(901, 8, hits: 2) });
            Queue(duel, 0);
            ResolveTurn(duel);
            duel.BeginNextTurn();
            Queue(duel, 1);
            LegacySlotResult result = ResolveTurn(duel);
            Assert.That(result.PlayerHealthDamage + result.PlayerResistanceDamage, Is.Zero);
            Assert.That(duel.PlayerStatuses, Is.Empty);
        }

        [Test]
        public void ManualAndAutomaticDodge_KeepVulnerabilityForTheNextReceivedAttack()
        {
            var duel = Duel(new[] { Skill(12, 4), Skill(54, 6, lane: 2) }, new[] { Guard(900, 100) },
                new[] { Attack(901, 4) }, new[] { Attack(902, 4) }, new[] { Attack(903, 4) });
            Queue(duel, 0);
            ResolveTurn(duel);
            duel.BeginNextTurn();
            Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool success) && success, Is.True);
            Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.Zero);
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
            duel.BeginNextTurn();
            Queue(duel, 2);
            Assert.That(ResolveTurn(duel).PlayerResistanceDamage, Is.Zero);
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
            duel.BeginNextTurn();
            Assert.That(ResolveTurn(duel).PlayerHealthDamage, Is.EqualTo(6));
            Assert.That(duel.PlayerStatuses, Is.Empty);
        }

        [Test]
        public void Power_SurvivesBreathsEmptySlotsAndTurnBoundaries_ThenBoostsAnActualGuard()
        {
            var duel = Duel(new[] { Skill(10, 4), Guard(100, 100, lane: 1) }, new[] { Guard(900, 100) },
                new[] { Guard(901, 100) }, new[] { Guard(902, 100) }, new[] { Attack(903, 151) });
            Queue(duel, 0);
            ResolveTurn(duel);
            duel.BeginNextTurn();
            Assert.That(duel.TryQueueBreath(), Is.True);
            ResolveTurn(duel);
            duel.BeginNextTurn();
            ResolveTurn(duel);
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.PowerUp, 50, 1);
            duel.BeginNextTurn();
            Queue(duel, 1);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerFeedback.PowerBuffPercent, Is.EqualTo(50));
            Assert.That(duel.PlayerStatuses, Is.Empty);
            Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.EqualTo(1), "The 100-point guard becomes 150 against 151 attack.");
        }

        [Test]
        public void ReapplyingPower_PreservesOldAdditiveStrengthsAndIndependentCounts()
        {
            var duel = Duel(new[] { Guard(9, 10), Attack(100, 20, lane: 1) }, new[] { Guard(900, 100), Guard(901, 100) },
                new[] { Attack(902, 1) });
            Queue(duel, 0);
            Queue(duel, 0);
            ResolveTurn(duel);
            Assert.That(duel.PlayerStatuses.Select(status => status.RemainingCount), Is.EqualTo(new[] { 1, 2 }));
            duel.BeginNextTurn();
            Queue(duel, 1);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerFeedback.PowerBuffPercent, Is.EqualTo(100));
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.PowerUp, 50, 1);
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(40));
        }

        [Test]
        public void Protection_SurvivesNonAttacks_AndReducesTheNextReceivedAttack()
        {
            var duel = Duel(new[] { Guard(8, 100) }, new[] { Guard(900, 100) },
                new[] { Guard(901, 100) }, new[] { Attack(902, 4) });
            Queue(duel, 0);
            ResolveTurn(duel);
            duel.BeginNextTurn();
            Assert.That(duel.TryQueueBreath(), Is.True);
            ResolveTurn(duel);
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Protection, 50, 1);
            duel.BeginNextTurn();
            Assert.That(ResolveTurn(duel).PlayerHealthDamage, Is.EqualTo(2));
            Assert.That(duel.PlayerStatuses, Is.Empty);
        }

        [Test]
        public void Forecast_CopiesPersistentStatuses_WithoutSpendingOrRegrantingLiveCounts()
        {
            var duel = Duel(new[] { Skill(12, 4) }, new[] { Guard(900, 100) }, new[] { Attack(901, 8, hits: 2) });
            Queue(duel, 0);
            ResolveTurn(duel);
            duel.BeginNextTurn();
            Queue(duel, 0);
            int resistance = duel.Player.Resistance;
            LegacySlotForecast first = duel.ForecastTurn()[0];
            LegacySlotForecast second = duel.ForecastTurn()[0];
            Assert.That(first.PlayerResistanceDamage, Is.EqualTo(12));
            Assert.That(second.PlayerResistanceDamage, Is.EqualTo(first.PlayerResistanceDamage));
            Assert.That(duel.Player.Resistance, Is.EqualTo(resistance));
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
            Assert.That(ResolveTurn(duel).PlayerResistanceDamage, Is.EqualTo(first.PlayerResistanceDamage));
            AssertStatus(duel.PlayerStatuses, LegacyStatusKind.Vulnerability, 50, 1);
        }

        [Test]
        public void EnemyRoster_OwnsAndCopiesEachEnemysStatusesIndependently_AndResetClearsThem()
        {
            var enemies = new[]
            {
                new LegacyEnemyDefinition("A", 1000, 1000, Script(new[] { Skill(12, 4) })),
                new LegacyEnemyDefinition("B", 1000, 1000, Script(new[] { Guard(900, 100) })),
            };
            var duel = new LegacyQueuedDuel(1000, 1000, new[] { Attack(100, 1) }, enemies, randomSeed: 1, meshPercent: 0);
            ResolveTurn(duel);
            AssertStatus(duel.GetEnemyStatuses(0), LegacyStatusKind.Vulnerability, 50, 1);
            Assert.That(duel.GetEnemyStatuses(1), Is.Empty);
            duel.BeginNextTurn();
            duel.ForecastTurn();
            AssertStatus(duel.GetEnemyStatuses(0), LegacyStatusKind.Vulnerability, 50, 1);
            Assert.That(duel.GetEnemyStatuses(1), Is.Empty);
            duel.Reset();
            Assert.That(duel.PlayerStatuses, Is.Empty);
            Assert.That(duel.GetEnemyStatuses(0), Is.Empty);
            Assert.That(duel.GetEnemyStatuses(1), Is.Empty);
        }

        [Test]
        public void Versus_MirrorsPersistentStatusesAndForecasts_AndConsumesOneCountPerAttack()
        {
            var skills = new[] { Skill(12, 4), Guard(100, 100, lane: 1), Attack(101, 8, lane: 2, hits: 2) };
            var match = new LocalVersusMatch(skills, skills, randomSeed: 1, leftHealth: 1000, leftResistance: 1000, rightHealth: 1000, rightResistance: 1000);
            QueueBoth(match, 0, 0);
            ResolveTurn(match);
            AssertStatus(match.Left.Statuses, LegacyStatusKind.Vulnerability, 50, 1);
            AssertStatus(match.Right.Statuses, LegacyStatusKind.Vulnerability, 50, 1);
            match.BeginNextTurn();
            QueueBoth(match, 0, 0);
            match.ForecastTurn(0, new List<LegacySlotForecast>());
            AssertStatus(match.Left.Statuses, LegacyStatusKind.Vulnerability, 50, 1);
            Assert.That(ResolveTurn(match).LeftResistanceDamage, Is.EqualTo(6));
            AssertStatus(match.Left.Statuses, LegacyStatusKind.Vulnerability, 50, 1);
            AssertStatus(match.Right.Statuses, LegacyStatusKind.Vulnerability, 50, 1);
            var view = new LocalVersusPerspective(match, 1);
            Assert.That(view.PlayerStatuses, Is.SameAs(match.Right.Statuses));
            Assert.That(view.EnemyStatuses, Is.SameAs(match.Left.Statuses));
            match.BeginNextTurn();
            QueueBoth(match, 1, 2);
            Assert.That(ResolveTurn(match).LeftHealthDamage, Is.Zero);
            Assert.That(match.Left.Statuses, Is.Empty);
            AssertStatus(match.Right.Statuses, LegacyStatusKind.Vulnerability, 50, 1);
            match.Reset();
            Assert.That(match.Left.Statuses, Is.Empty);
            Assert.That(match.Right.Statuses, Is.Empty);
        }

        [Test]
        public void Vulnerability_PreservesOtherSheetPercentages_AndCopiesDoNotShareMutation()
        {
            var statuses = new LegacyStatusCollection();
            statuses.Grant(LegacyStatusKind.Vulnerability, 25, 1);
            statuses.Grant(LegacyStatusKind.Vulnerability, 25, 2);
            statuses.Grant(LegacyStatusKind.Vulnerability, 50, 1);
            Assert.That(statuses.Statuses.Count, Is.EqualTo(2));
            Assert.That(statuses.Statuses[0].RemainingCount, Is.EqualTo(3));
            Assert.That(statuses.ReceivedMultiplier, Is.EqualTo(1.75));
            LegacyStatusCollection copy = statuses.Copy();
            copy.Grant(LegacyStatusKind.Vulnerability, 25, 1);
            copy.Clear();
            Assert.That(statuses.Statuses[0].RemainingCount, Is.EqualTo(3));
            Assert.That(statuses.ReceivedMultiplier, Is.EqualTo(1.75));
        }

        [Test]
        public void Reset_ClearsUnspentPlayerStatuses()
        {
            var duel = Duel(new[] { Skill(12, 4) }, new[] { Guard(900, 100) });
            Queue(duel, 0);
            ResolveTurn(duel);
            Assert.That(duel.PlayerStatuses, Is.Not.Empty);
            duel.Reset();
            Assert.That(duel.PlayerStatuses, Is.Empty);
        }

        private static LegacySkill Skill(int id, int power, int lane = 0)
        {
            LegacySkill basis = LegacySkillDefinitions.Skill(id);
            return new LegacySkill(id, basis.Name, 0, power, power, basis.Kind, basis.Property, basis.AttackCount, lane, "");
        }
        private static LegacySkill Attack(int id, int power, int lane = 0, int hits = 1)
            => new LegacySkill(id, "attack", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, lane, "");
        private static LegacySkill Guard(int id, int power, int lane = 0)
            => new LegacySkill(id, "guard", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, lane, "");
        private static EnemyScript Script(params LegacySkill[][] turns) => new EnemyScript(turns);
        private static LegacyQueuedDuel Duel(LegacySkill[] player, params LegacySkill[][] turns)
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, Script(turns), randomSeed: 1, meshPercent: 0);
        private static void Queue(LegacyQueuedDuel duel, int lane) => Assert.That(duel.TryQueueLane(lane), Is.True);
        private static LegacySlotResult ResolveTurn(LegacyQueuedDuel duel)
        {
            duel.Commit();
            LegacySlotResult result = null;
            while (!duel.IsTurnResolved && !duel.IsFinished) result = duel.ResolveNextSlot();
            return result;
        }
        private static void QueueBoth(LocalVersusMatch match, int leftLane, int rightLane)
        {
            int first = match.CurrentPlanner;
            Assert.That(match.TryQueueLane(first, first == 0 ? leftLane : rightLane), Is.True);
            Assert.That(match.TryQueueLane(1 - first, first == 0 ? rightLane : leftLane), Is.True);
        }
        private static LocalVersusSlotResult ResolveTurn(LocalVersusMatch match)
        {
            Assert.That(match.TryPass(match.CurrentPlanner), Is.True);
            Assert.That(match.TryPass(match.CurrentPlanner), Is.True);
            LocalVersusSlotResult result = null;
            while (!match.IsTurnResolved) result = match.ResolveNextSlot();
            return result;
        }
        private static void AssertStatus(IReadOnlyList<LegacyStatusSnapshot> statuses, LegacyStatusKind kind, int percent, int count)
        {
            Assert.That(statuses.Count, Is.EqualTo(1));
            Assert.That(statuses[0].Kind, Is.EqualTo(kind));
            Assert.That(statuses[0].Percent, Is.EqualTo(percent));
            Assert.That(statuses[0].RemainingCount, Is.EqualTo(count));
        }
    }
}
