using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class BreakthroughPursuitTests
    {
        [Test]
        public void ProtectionSpend_RemovesEveryProtectionGrant_AndPreservesOtherStatuses()
        {
            var statuses = new LegacyStatusCollection();
            statuses.Grant(LegacyStatusKind.Protection, 25, 2);
            statuses.Grant(LegacyStatusKind.Vulnerability, 50, 3);
            statuses.Grant(LegacyStatusKind.Protection, 50, 1);
            statuses.Grant(LegacyStatusKind.PowerUp, 50, 2);
            Assert.That(statuses.HasProtection, Is.True);
            Assert.That(statuses.ConsumeProtection(), Is.True);
            Assert.That(statuses.HasProtection, Is.False);
            Assert.That(Statuses(statuses.Statuses), Is.EqualTo("Vulnerability:50:3|PowerUp:50:2"));
            Assert.That(statuses.ConsumeProtection(), Is.False, "No resource means no second activation.");
            Assert.That(Statuses(statuses.Statuses), Is.EqualTo("Vulnerability:50:3|PowerUp:50:2"));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Breakthrough_SpendsProtectionBeforeTheSameSlotsMultihitDamage_WithoutRemovingVulnerability(int grants)
        {
            LegacySkill[] preparation = Enumerable.Repeat(Guard(88000, 100), grants + 1).ToArray();
            var duel = Duel(new[] { Skill(8, 1), Skill(60, 8, 1), Skill(12, 4, 2) },
                preparation, new[] { Attack(88001, 12, hits: 3) }, new[] { LegacyCommonActions.Breathe });
            for (int count = 0; count < grants; count++) Queue(duel, 0);
            Queue(duel, 2);
            Resolve(duel);
            Assert.That(duel.PlayerStatuses.Count(status => status.Kind == LegacyStatusKind.Protection), Is.EqualTo(grants));
            Assert.That(duel.PlayerStatuses.Any(status => status.Kind == LegacyStatusKind.Vulnerability), Is.True);
            duel.BeginNextTurn();

            string before = Snapshot(duel);
            LegacySlotForecast candidate = duel.ForecastIfQueued(1);
            Assert.That(candidate.PlayerResistanceDamage, Is.EqualTo(18));
            Assert.That(candidate.EnemyResistanceDamage, Is.EqualTo(18));
            Assert.That(duel.ForecastIfQueued(1).PlayerResistanceDamage, Is.EqualTo(18));
            Assert.That(Snapshot(duel), Is.EqualTo(before), "Candidate forecasts must keep both statuses, queues and ACT intact.");

            Queue(duel, 1);
            before = Snapshot(duel);
            Assert.That(duel.ForecastTurn()[0].PlayerResistanceDamage, Is.EqualTo(18));
            Assert.That(Snapshot(duel), Is.EqualTo(before));
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.ProtectionSpendPowerBonus, Is.EqualTo(10));
            Assert.That(duel.PlayerStatuses.Any(status => status.Kind == LegacyStatusKind.Protection), Is.False);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.PlayerResistanceDamage, Is.EqualTo(candidate.PlayerResistanceDamage));
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo(candidate.EnemyResistanceDamage));

            duel.BeginNextTurn();
            Queue(duel, 1);
            duel.Commit();
            slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.ProtectionSpendPowerBonus, Is.Zero);
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(8), "No protection leaves the unboosted base attack.");
        }

        [Test]
        public void Breakthrough_CanActivateAgainInTheSameTurn_OnlyAfterProtectionIsRegranted()
        {
            var duel = Duel(new[] { Skill(8, 1), Skill(60, 8, 1) }, Array.Empty<LegacySkill>());
            Queue(duel, 0, 1, 1, 0, 1);
            duel.Commit();
            var bonuses = new List<int>();
            while (!duel.IsTurnResolved)
            {
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                if (slot.PlayerSkill.Id == 60) bonuses.Add(slot.PlayerFeedback.ProtectionSpendPowerBonus);
                duel.ResolveNextSlot();
            }
            Assert.That(bonuses, Is.EqualTo(new[] { 10, 0, 10 }));
        }

        [Test]
        public void Breakthrough_AddsBasePowerBeforeTheExistingPowerMultiplier()
        {
            var duel = Duel(new[] { Skill(8, 1), Skill(10, 1), Skill(60, 8, 1) }, Array.Empty<LegacySkill>());
            Queue(duel, 0, 0, 1);
            LegacySlotResult[] result = Resolve(duel);
            Assert.That(result.Last().EnemyHealthDamage, Is.EqualTo(27), "(8 + 10) × 강화 150%, not 8 × 150% + 10.");
        }

        [Test]
        public void EnemyBreakthrough_SpendsOnlyItsOwnersProtection_AndForecastKeepsTheRosterIntact()
        {
            var enemies = new[]
            {
                new LegacyEnemyDefinition("A", 1000, 1000,
                    new EnemyScript(new[] { new[] { Skill(8, 1) }, new[] { Skill(60, 8, 1) } })),
                new LegacyEnemyDefinition("B", 1000, 1000,
                    new EnemyScript(new[] { new[] { Skill(8, 1) }, new[] { Guard(88000, 100) } })),
            };
            var duel = new LegacyQueuedDuel(1000, 1000, new[] { Attack(88001, 1) }, enemies, randomSeed: 1, meshPercent: 0);
            Resolve(duel);
            duel.BeginNextTurn();
            string a = Statuses(duel.GetEnemyStatuses(0)), b = Statuses(duel.GetEnemyStatuses(1));
            Assert.That(a, Is.EqualTo("Protection:50:1"));
            Assert.That(b, Is.EqualTo(a));
            Assert.That(duel.ForecastTurn().Single(slot => slot.EnemySkill?.Id == 60).PlayerHealthDamage, Is.EqualTo(18));
            Assert.That(Statuses(duel.GetEnemyStatuses(0)), Is.EqualTo(a));
            Assert.That(Statuses(duel.GetEnemyStatuses(1)), Is.EqualTo(b));
            Assert.That(Resolve(duel).Single(slot => slot.EnemySkill?.Id == 60).PlayerHealthDamage, Is.EqualTo(18));
            Assert.That(duel.GetEnemyStatuses(0), Is.Empty);
            Assert.That(Statuses(duel.GetEnemyStatuses(1)), Is.EqualTo(b));
        }

        [Test]
        public void Versus_BothBreakthroughsSpendBeforeEitherSidesReceivedDamageSnapshot()
        {
            LegacySkill[] skills = { Skill(8, 1), Skill(60, 8, 1) };
            var match = Match(skills);
            QueueBoth(match, 0);
            Resolve(match);
            match.BeginNextTurn();
            string before = MatchSnapshot(match);
            for (int player = 0; player < 2; player++)
            {
                LegacySlotForecast candidate = match.ForecastIfQueued(player, 1);
                Assert.That(candidate.EnemyHealthDamage, Is.EqualTo(9), "The unqueued opponent still retains its protection.");
            }
            Assert.That(MatchSnapshot(match), Is.EqualTo(before));
            QueueBoth(match, 1);
            before = MatchSnapshot(match);
            for (int player = 0; player < 2; player++)
            {
                var forecast = new List<LegacySlotForecast>();
                match.ForecastTurn(player, forecast);
                Assert.That(forecast[0].PlayerResistanceDamage, Is.EqualTo(18));
                Assert.That(forecast[0].EnemyResistanceDamage, Is.EqualTo(18));
            }
            Assert.That(MatchSnapshot(match), Is.EqualTo(before));
            LocalVersusSlotResult result = Resolve(match).Single();
            Assert.That(result.LeftResistanceDamage, Is.EqualTo(18));
            Assert.That(result.RightResistanceDamage, Is.EqualTo(18));
            Assert.That(match.Left.Statuses, Is.Empty);
            Assert.That(match.Right.Statuses, Is.Empty);
        }

        [TestCase(4, 0)]
        [TestCase(5, 12)]
        [TestCase(6, 12)]
        public void Pursuit_HasAnInclusiveFiveHitThreshold(int hits, int bonus)
        {
            LegacySkill chase = Skill(61, 6, 2);
            LegacySkill[] queue = { Attack(88000, 1, hits: 2), Attack(88001, 1, hits: hits - 2), chase };
            Assert.That(LegacyKeywordLinks.CountAheadAttackHits(queue, 2, chase), Is.EqualTo(hits));
            Assert.That(LegacyKeywordLinks.AheadAttackHitsPowerBonus(queue, 2, chase), Is.EqualTo(bonus));
            Assert.That(LegacyKeywordLinks.AheadPowerBonus(queue, 2, chase), Is.EqualTo(bonus));
        }

        [Test]
        public void Pursuit_CountsDistinctAttackIdsOnly_ExcludingBreathsGuardsCurrentAndLaterSlots()
        {
            LegacySkill chase = Skill(61, 6, 2);
            LegacySkill two = Attack(88000, 4, hits: 2), three = Attack(88001, 6, hits: 3);
            LegacySkill guard = new LegacySkill(88002, "guard", 0, 1, 1,
                LegacySkillKind.Defence, LegacySkillProperty.Defence, 9, 1, "");
            LegacySkill[] queue = { two, two, LegacyCommonActions.Breathe, guard, three, chase, Attack(88003, 1, hits: 9) };
            Assert.That(LegacyKeywordLinks.CountAheadAttackHits(queue, 5), Is.EqualTo(5));
            Assert.That(LegacyKeywordLinks.AheadAttackHitsPowerBonus(queue, 5, chase), Is.EqualTo(12));
            LegacySkill[] repeated = { two, two, two, chase };
            Assert.That(LegacyKeywordLinks.CountAheadAttackHits(repeated, 3, chase), Is.EqualTo(2));
            Assert.That(LegacyKeywordLinks.AheadAttackHitsPowerBonus(repeated, 3, chase), Is.Zero);
            LegacySkill[] laterCopy = { chase, two, three, chase };
            Assert.That(LegacyKeywordLinks.CountAheadAttackHits(laterCopy, 3), Is.EqualTo(5));
            Assert.That(LegacyKeywordLinks.AheadAttackHitsPowerBonus(laterCopy, 3, chase), Is.Zero,
                "The first copy spends its opportunity even if it failed the threshold.");
            Assert.That(LegacyKeywordLinks.CountAheadAttackHits(null, 0, chase), Is.Zero);
            Assert.That(LegacyKeywordLinks.CountAheadAttackHits(queue, -1, chase), Is.Zero);
        }

        [Test]
        public void Pursuit_UsesTheFirstCommittedCopy_ResetsNextTurn_AndForecastsWithoutMutation()
        {
            var duel = Duel(PursuitSkills(), Array.Empty<LegacySkill>());
            Queue(duel, 0, 0);
            string before = Snapshot(duel);
            Assert.That(duel.ForecastIfQueued(2).EnemyHealthDamage, Is.EqualTo(18));
            Assert.That(duel.ForecastIfQueued(2).EnemyHealthDamage, Is.EqualTo(18));
            Assert.That(Snapshot(duel), Is.EqualTo(before));
            Queue(duel, 2, 2);
            before = Snapshot(duel);
            Assert.That(duel.ForecastTurn().Select(slot => slot.EnemyHealthDamage), Is.EqualTo(new[] { 4, 6, 18, 6 }));
            Assert.That(Snapshot(duel), Is.EqualTo(before));
            Assert.That(Resolve(duel).Select(slot => slot.EnemyHealthDamage), Is.EqualTo(new[] { 4, 6, 18, 6 }));
            duel.BeginNextTurn();
            Queue(duel, 2);
            Assert.That(Resolve(duel).Single().EnemyHealthDamage, Is.EqualTo(6), "Last turn's hits do not carry over.");
            duel.BeginNextTurn();
            Queue(duel, 0, 0, 2);
            Assert.That(Resolve(duel).Last().EnemyHealthDamage, Is.EqualTo(18), "A new turn gets a new first-copy opportunity.");
        }

        [Test]
        public void Pursuit_AddsItsBonusBeforeTheExistingPowerMultiplier()
        {
            var duel = Duel(PursuitSkills().Concat(new[] { Skill(10, 1, 1) }).ToArray(), Array.Empty<LegacySkill>());
            Queue(duel, 0, 0, 1, 2);
            Assert.That(duel.ForecastTurn().Last().EnemyHealthDamage, Is.EqualTo(27));
            Assert.That(Resolve(duel).Last().EnemyHealthDamage, Is.EqualTo(27), "(6 + 12) × 강화 150%.");
        }

        [TestCase("반환")]
        [TestCase("취소")]
        public void Pursuit_RecountsTheSurvivingQueueAfterEarlierAttacksAreRemoved(string mode)
        {
            KeywordSheet.With(() =>
            {
                var duel = Duel(PursuitSkills().Concat(new[] { KeywordSheet.Skill(897) }).ToArray(), Array.Empty<LegacySkill>());
                Queue(duel, 0, 0);
                Assert.That(duel.ForecastIfQueued(2).EnemyHealthDamage, Is.EqualTo(18));
                Queue(duel, 1);
                Assert.That(duel.PlayerQueue.Select(skill => skill.Id), Is.EqualTo(new[] { 897 }));
                Assert.That(duel.ForecastIfQueued(2).EnemyHealthDamage, Is.EqualTo(6));
                Queue(duel, 2);
                Assert.That(Resolve(duel).Last().EnemyHealthDamage, Is.EqualTo(6));
            }, KeywordSheet.Row(897, "W", "방어", 0, 0, (Column.QueueRemovalMode, mode)));
        }

        [Test]
        public void Versus_PursuitReadsEachOwnersQueue_AndForecastsMatchBothSidesWithoutMutation()
        {
            var match = Match(PursuitSkills());
            Assert.That(match.TryQueueLane(0, 0) && match.TryQueueLane(0, 0), Is.True);
            string before = MatchSnapshot(match);
            Assert.That(match.ForecastIfQueued(0, 2).EnemyHealthDamage, Is.EqualTo(18));
            Assert.That(MatchSnapshot(match), Is.EqualTo(before));
            Assert.That(match.TryQueueLane(0, 2) && match.TryQueueLane(0, 2), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0) && match.TryQueueLane(1, 0), Is.True);
            before = MatchSnapshot(match);
            Assert.That(match.ForecastIfQueued(1, 2).EnemyResistanceDamage, Is.EqualTo(18),
                "The responder's same bonus now clashes with the confirmed leading queue.");
            Assert.That(MatchSnapshot(match), Is.EqualTo(before));
            Assert.That(match.TryQueueLane(1, 2) && match.TryQueueLane(1, 2), Is.True);
            before = MatchSnapshot(match);
            for (int player = 0; player < 2; player++)
            {
                var forecast = new List<LegacySlotForecast>();
                match.ForecastTurn(player, forecast);
                Assert.That(forecast.Select(slot => slot.PlayerResistanceDamage), Is.EqualTo(new[] { 4, 6, 18, 6 }));
                Assert.That(forecast.Select(slot => slot.EnemyResistanceDamage), Is.EqualTo(new[] { 4, 6, 18, 6 }));
            }
            Assert.That(MatchSnapshot(match), Is.EqualTo(before));
            LocalVersusSlotResult[] results = Resolve(match);
            Assert.That(results.Select(slot => slot.LeftResistanceDamage), Is.EqualTo(new[] { 4, 6, 18, 6 }));
            Assert.That(results.Select(slot => slot.RightResistanceDamage), Is.EqualTo(new[] { 4, 6, 18, 6 }));
        }

        private static LegacySkill[] PursuitSkills()
            => new[] { Attack(88000, 4, hits: 2), Attack(88001, 6, hits: 3), Skill(61, 6, 2) };
        private static LegacySkill Skill(int id, int power, int lane = 0)
        {
            LegacySkill basis = LegacySkillDefinitions.Skill(id);
            return new LegacySkill(id, basis.Name, 0, power, power, basis.Kind, basis.Property, basis.AttackCount, lane, "");
        }
        private static LegacySkill Attack(int id, int power, int lane = 0, int hits = 1)
            => new LegacySkill(id, "attack", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, lane, "");
        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");
        private static LegacyQueuedDuel Duel(LegacySkill[] player, params LegacySkill[][] turns)
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000, player,
                new EnemyScript(turns.Select(turn => turn.Length == 0 ? new[] { LegacyCommonActions.Breathe } : turn).ToArray()),
                randomSeed: 1, meshPercent: 0);
        private static LocalVersusMatch Match(LegacySkill[] skills)
            => new LocalVersusMatch(skills, skills, randomSeed: 1,
                leftHealth: 1000, leftResistance: 1000, rightHealth: 1000, rightResistance: 1000);
        private static void Queue(LegacyQueuedDuel duel, params int[] lanes)
        {
            foreach (int lane in lanes) Assert.That(duel.TryQueueLane(lane), Is.True, "lane " + lane);
        }
        private static LegacySlotResult[] Resolve(LegacyQueuedDuel duel)
        {
            duel.Commit();
            var results = new List<LegacySlotResult>();
            while (!duel.IsTurnResolved && !duel.IsFinished) results.Add(duel.ResolveNextSlot());
            return results.ToArray();
        }
        private static void QueueBoth(LocalVersusMatch match, int lane)
        {
            int first = match.CurrentPlanner;
            Assert.That(match.TryQueueLane(first, lane), Is.True);
            Assert.That(match.TryCommit(first), Is.True);
            Assert.That(match.TryQueueLane(1 - first, lane), Is.True);
        }
        private static LocalVersusSlotResult[] Resolve(LocalVersusMatch match)
        {
            Assert.That(match.TryCommit(match.CurrentPlanner), Is.True);
            var results = new List<LocalVersusSlotResult>();
            while (!match.IsTurnResolved) results.Add(match.ResolveNextSlot());
            return results.ToArray();
        }
        private static string Statuses(IReadOnlyList<LegacyStatusSnapshot> statuses)
            => string.Join("|", statuses.Select(status => $"{status.Kind}:{status.Percent}:{status.RemainingCount}"));
        private static string Snapshot(LegacyQueuedDuel duel)
            => SlotForecastTests.Snapshot(duel) + " statuses " + Statuses(duel.PlayerStatuses) + "/" + Statuses(duel.EnemyStatuses);
        private static string MatchSnapshot(LocalVersusMatch match)
            => $"{match.Phase}/{match.RoundNumber}/{match.CurrentPlanner}/{match.ConsecutivePasses} " + Fighter(match.Left) + "/" + Fighter(match.Right);
        private static string Fighter(LocalVersusFighter fighter)
            => $"{fighter.Act}/{fighter.State.Health}/{fighter.State.Resistance} " + string.Join(",", fighter.Queue.Select(skill => skill.Id)) +
                " lanes " + string.Join("|", Enumerable.Range(0, 3).Select(lane => string.Join(",", fighter.GetLane(lane).Select(skill => skill.Id)))) +
                " statuses " + Statuses(fighter.Statuses);
    }
}
