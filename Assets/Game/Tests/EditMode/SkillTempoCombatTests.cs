using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class SkillTempoCombatTests
    {
        private static LegacySkill Attack(int id, int power = 5, int duration = 2, int hits = 1)
            => new LegacySkill(id, "tempo " + id, 1, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, "", durationHalfUnits: duration);

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy,
            LegacyCounter playerCounter = null, LegacyCounter enemyCounter = null, int health = 100, int resistance = 50)
        {
            var duel = new LegacyQueuedDuel(health, resistance, health, resistance,
                player, enemy, new[] { enemy.Length }, playerCounter: playerCounter, enemyCounter: enemyCounter, meshPercent: 0);
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            return duel;
        }

        [TestCase(new[] { 1, 1, 2 }, new[] { 0, 1, 2 }, new[] { 1, 2, 4 }, 4)]
        [TestCase(new[] { 1, 2 }, new[] { 0, 2 }, new[] { 1, 4 }, 4)]
        [TestCase(new[] { 1, 1, 1 }, new[] { 0, 1, 2 }, new[] { 1, 2, 3 }, 4)]
        [TestCase(new[] { 1, 4, 1 }, new[] { 0, 2, 6 }, new[] { 1, 6, 7 }, 8)]
        [TestCase(new[] { 4, 1, 1, 2 }, new[] { 0, 4, 5, 6 }, new[] { 4, 5, 6, 8 }, 8)]
        [TestCase(new[] { 2, 2, 2 }, new[] { 0, 2, 4 }, new[] { 2, 4, 6 }, 6)]
        public void Packing_OnlyConsecutiveFastTechniquesShareAnOrdinaryCell(int[] durations, int[] starts, int[] ends, int total)
        {
            var queue = durations.Select((duration, i) => Attack(9000 + i, duration: duration)).ToArray();
            var entries = LegacySkillTiming.Build(queue);
            CollectionAssert.AreEqual(starts, entries.Select(entry => entry.StartHalfTick));
            CollectionAssert.AreEqual(ends, entries.Select(entry => entry.EndHalfTick));
            CollectionAssert.AreEqual(Enumerable.Range(0, queue.Length), entries.Select(entry => entry.QueueIndex));
            Assert.That(LegacySkillTiming.TotalHalfTicks(queue), Is.EqualTo(total));
        }

        [Test]
        public void TwoFastAgainstNormal_EarlierHitIsUnopposed_FinalHitClashes_NormalActsExactlyOnce()
        {
            var duel = Duel(new[] { Attack(9001, 5, 1), Attack(9002, 7, 1) }, new[] { Attack(9003, 20) });
            duel.Commit();
            Assert.That(duel.ResolutionSlotCount, Is.EqualTo(2));
            LegacyCurrentSlot first = duel.BeginNextSlot();
            Assert.That(first.PlayerSkill.Id, Is.EqualTo(9001));
            Assert.That(first.EnemySkill, Is.Null);
            Assert.That(first.EnemyActionSkill.Id, Is.EqualTo(9003));
            Assert.That(first.EnemyActionStarts, Is.True);
            Assert.That(first.EnemyActionCompletes, Is.False);
            Assert.That(first.EnemyActionDurationHalfTicks, Is.EqualTo(2));
            var hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyHealthDamage, Is.EqualTo(5));
            Assert.That(hit.EnemyAttacked, Is.False);
            duel.CompleteCurrentSlot();
            LegacyCurrentSlot second = duel.BeginNextSlot();
            Assert.That(second.EnemyActionStarts, Is.False);
            Assert.That(second.EnemyActionCompletes, Is.True);
            Assert.That(second.EnemyQueueIndex, Is.Zero);
            Assert.That(second.PlayerQueueIndex, Is.EqualTo(1));
            duel.ResolveNextHit(); duel.CompleteCurrentSlot();
            Assert.That(duel.Enemy.Health, Is.EqualTo(95));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(43));
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Player.Resistance, Is.EqualTo(30));
            Assert.That(duel.IsTurnResolved, Is.True);
        }

        [Test]
        public void ChargeAgainstTwoNormalTechniques_TakesAnEarlyHit_ThenCompletesOneAttack()
        {
            var duel = Duel(new[] { Attack(9001, 18, 4) }, new[] { Attack(9002, 5), Attack(9003, 7) });
            duel.Commit();
            var early = duel.ResolveNextSlot();
            Assert.That(early.PlayerSkill, Is.Null);
            Assert.That(early.PlayerHealthDamage, Is.EqualTo(5));
            var final = duel.ResolveNextSlot();
            Assert.That(final.PlayerSkill.Id, Is.EqualTo(9001));
            Assert.That(final.PlayerQueueIndex, Is.Zero);
            Assert.That(final.EnemyQueueIndex, Is.EqualTo(1));
            Assert.That(duel.Player.Health, Is.EqualTo(95));
            Assert.That(duel.Player.Resistance, Is.EqualTo(43));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(32));
            Assert.That(duel.IsTurnResolved, Is.True);
        }

        [Test]
        public void FastThenNormal_LeavesPadding_AndNeverPullsTheNormalIntoTheFastCell()
        {
            var duel = Duel(new[] { Attack(9001, 5, 1), Attack(9002, 8) }, new[] { Attack(9003, 10, 4) });
            duel.Commit();
            Assert.That(duel.ResolutionSlotCount, Is.EqualTo(3));
            duel.ResolveNextSlot();
            var padding = duel.BeginNextSlot();
            Assert.That(padding.StartHalfTick, Is.EqualTo(1));
            Assert.That(padding.EndHalfTick, Is.EqualTo(2));
            Assert.That(padding.PlayerActionSkill, Is.Null);
            Assert.That(padding.PlayerQueueIndex, Is.EqualTo(-1));
            Assert.That(padding.EnemyActionSkill, Is.Not.Null);
            Assert.That(padding.PlayerSkill, Is.Null);
            Assert.That(padding.EnemySkill, Is.Null);
            duel.ResolveNextHit(); duel.CompleteCurrentSlot();
            var final = duel.BeginNextSlot();
            Assert.That(final.PlayerActionStarts, Is.True);
            Assert.That(final.PlayerActionStartHalfTick, Is.EqualTo(2));
            Assert.That(final.PlayerSkill.Id, Is.EqualTo(9002));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void MixedQueues_ExecuteEveryTechniqueAndItsExistingHitsOnce(int swap)
        {
            var a = new[] { Attack(9000, 3, 1, 2), Attack(9001, 3, 1), Attack(9002, 3, 4, 3), Attack(9003, 3) };
            var b = new[] { Attack(9010, 3), Attack(9011, 3, 1, 2), Attack(9012, 3, 4), Attack(9013, 3, 1) };
            var player = swap == 0 ? a : b; var enemy = swap == 0 ? b : a;
            var duel = Duel(player, enemy, health: 10000, resistance: 10000);
            duel.Commit();
            int playerHits = 0, enemyHits = 0;
            var playerActions = new List<int>(); var enemyActions = new List<int>();
            while (!duel.IsTurnResolved)
            {
                var slot = duel.BeginNextSlot();
                if (slot.PlayerSkill != null) playerActions.Add(slot.PlayerSkill.Id);
                if (slot.EnemySkill != null) enemyActions.Add(slot.EnemySkill.Id);
                while (!slot.IsResolved)
                {
                    var hit = duel.ResolveNextHit();
                    if (hit.PlayerAttacked) playerHits++;
                    if (hit.EnemyAttacked) enemyHits++;
                }
                duel.CompleteCurrentSlot();
            }
            CollectionAssert.AreEqual(player.Select(skill => skill.Id), playerActions);
            CollectionAssert.AreEqual(enemy.Select(skill => skill.Id), enemyActions);
            Assert.That(playerHits, Is.EqualTo(player.Sum(skill => skill.AttackCount)));
            Assert.That(enemyHits, Is.EqualTo(enemy.Sum(skill => skill.AttackCount)));
        }

        [Test]
        public void OngoingNormalTechnique_DoesNotTriggerACounterForEitherEarlyFastHit()
        {
            var counter = new LegacyCounter(Attack(9090, 20), 3);
            var duel = Duel(new[] { Attack(9000, 5, 1), Attack(9001, 5, 1) }, new[] { Attack(9002, 10) }, enemyCounter: counter);
            var forecast = new List<int>(); duel.ForecastEnemyCounterSlots(forecast);
            Assert.That(forecast, Is.Empty);
            duel.Commit(); duel.ResolveNextSlot(); duel.ResolveNextSlot();
            Assert.That(duel.EnemyCountersRemaining, Is.EqualTo(3));
        }

        [Test]
        public void AnEmptyOpponentAfterItsFastPair_CanCounterTheLaterCharge()
        {
            var counter = new LegacyCounter(Attack(9090, 20), 1);
            var duel = Duel(new[] { Attack(9000, 15, 4) }, new[] { Attack(9001, 5, 1), Attack(9002, 5, 1) }, enemyCounter: counter);
            var slots = new List<int>(); duel.ForecastEnemyCounterSlots(slots);
            CollectionAssert.AreEqual(new[] { 2 }, slots);
            duel.Commit(); duel.ResolveNextSlot(); duel.ResolveNextSlot();
            var final = duel.BeginNextSlot();
            Assert.That(final.EnemyCountered, Is.True);
            Assert.That(final.EnemyActionSkill.Id, Is.EqualTo(9090));
        }

        [Test]
        public void ExistingUseEffectOfAChargedSkill_RunsOnceAtItsCompletionSegment()
        {
            var source = LegacySkillDefinitions.Skill(1);
            var charged = new LegacySkill(source.Id, source.Name, source.Cost, source.MinPower, source.MaxPower,
                source.Kind, source.Property, source.AttackCount, source.LaneIndex, source.Description, durationHalfUnits: 4);
            var duel = Duel(new[] { charged }, new[] { Attack(9001, 2), Attack(9002, 2) });
            duel.Commit();
            int before = duel.NextActGain;
            duel.ResolveNextSlot();
            Assert.That(duel.NextActGain, Is.EqualTo(before), "The charge has not used its effect while preparing.");
            duel.ResolveNextSlot();
            Assert.That(duel.NextActGain, Is.EqualTo(before + 1));
        }

        [Test]
        public void Forecast_UsesTheSameIntervalsAndLogicalIndices_WithoutTouchingTheLiveBattle()
        {
            var duel = Duel(new[] { Attack(9001, 5, 1), Attack(9002, 7, 1) }, new[] { Attack(9003, 20) });
            var forecast = duel.ForecastTurn();
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(duel.Enemy.Health, Is.EqualTo(100));
            Assert.That(forecast.Count, Is.EqualTo(2));
            Assert.That(forecast[0].EnemySkill, Is.Null);
            Assert.That(forecast[0].EnemyActionSkill.Id, Is.EqualTo(9003));
            Assert.That(forecast[1].PlayerQueueIndex, Is.EqualTo(1));
            Assert.That(forecast[1].EnemyQueueIndex, Is.Zero);
            duel.Commit();
            for (int i = 0; i < forecast.Count; i++)
            {
                var actual = duel.ResolveNextSlot();
                Assert.That(actual.StartHalfTick, Is.EqualTo(forecast[i].StartHalfTick));
                Assert.That(actual.EndHalfTick, Is.EqualTo(forecast[i].EndHalfTick));
                Assert.That(actual.PlayerHealthDamage, Is.EqualTo(forecast[i].PlayerHealthDamage));
                Assert.That(actual.EnemyHealthDamage, Is.EqualTo(forecast[i].EnemyHealthDamage));
                Assert.That(actual.PlayerResistanceDamage, Is.EqualTo(forecast[i].PlayerResistanceDamage));
                Assert.That(actual.EnemyResistanceDamage, Is.EqualTo(forecast[i].EnemyResistanceDamage));
            }
        }

        [Test]
        public void DefeatDuringPreparation_StopsTheLaterChargedAttack()
        {
            var duel = Duel(new[] { Attack(9001, 99, 4) }, new[] { Attack(9002, 30), Attack(9003, 2) }, health: 20);
            duel.Commit();
            var result = duel.ResolveNextSlot();
            Assert.That(result.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(result.PlayerSkill, Is.Null);
            Assert.That(duel.Enemy.Health, Is.EqualTo(20));
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Finished));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AResistanceBreak_DoesNotCancelTheActionAlreadyInMotion_ButCancelsItsFollowingReservation(bool swap)
        {
            LegacySkill source = LegacySkillDefinitions.Skill(500);
            var breaker = new LegacySkill(source.Id, source.Name, source.Cost, source.MinPower, source.MaxPower,
                source.Kind, source.Property, source.AttackCount, source.LaneIndex, source.Description, durationHalfUnits: 1);
            var ordinary = new[] { Attack(9000, 5), Attack(9001, 5) };
            var fast = new[] { breaker, Attack(9010, 5, 1) };
            var duel = Duel(swap ? fast : ordinary, swap ? ordinary : fast, health: 1000);
            duel.Commit();
            var first = duel.ResolveNextSlot();
            Assert.That(swap ? duel.Enemy.IsResistanceBroken : duel.Player.IsResistanceBroken, Is.True);
            Assert.That(swap ? first.EnemySkill : first.PlayerSkill, Is.Null);
            Assert.That(swap ? duel.EnemyBreakCanceledFromSlotIndex : duel.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(1));
            var second = duel.ResolveNextSlot();
            Assert.That((swap ? second.EnemySkill : second.PlayerSkill)?.Id, Is.EqualTo(9000),
                "The technique already moving survives the break.");
            var canceled = duel.ResolveNextSlot();
            Assert.That(swap ? canceled.EnemySkill : canceled.PlayerSkill, Is.Null,
                "Its later reservation is canceled and never executes.");
        }

        [Test]
        public void AChargedCyclingTechnique_IncrementsItsCycleOnlyOnce()
        {
            var source = LegacySkillDefinitions.Skill(15);
            var charged = new LegacySkill(source.Id, source.Name, source.Cost, source.MinPower, source.MaxPower,
                source.Kind, source.Property, source.AttackCount, source.LaneIndex, source.Description, durationHalfUnits: 4);
            var duel = Duel(new[] { charged }, new[] { Attack(9001, 2, 1), Attack(9002, 2, 1), Attack(9003, 2) });
            duel.Commit();
            duel.ResolveNextSlot(); duel.ResolveNextSlot();
            Assert.That(duel.CycleUses(15), Is.Zero);
            var current = duel.BeginNextSlot();
            Assert.That(current.PlayerCycleUses, Is.Zero);
            Assert.That(duel.CycleUses(15), Is.EqualTo(1));
            while (!current.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
            Assert.That(duel.CycleUses(15), Is.EqualTo(1));
        }

        [TestCase(2, 5, 7, 20)]
        [TestCase(4, 12, 0, 0)]
        public void CandidateForecast_AggregatesDamageTakenThroughoutItsWholePreparation(int duration,
            int expectedHealthDamage, int expectedResistanceDamage, int expectedEnemyResistanceDamage)
        {
            var candidate = Attack(9000, 20, duration);
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { candidate },
                new[] { Attack(9010, 5, 1), Attack(9011, 7, 1) }, new[] { 2 }, meshPercent: 0);
            var forecast = duel.ForecastIfQueued(0);
            Assert.That(forecast.PlayerQueueIndex, Is.Zero);
            Assert.That(forecast.StartHalfTick, Is.Zero);
            Assert.That(forecast.EndHalfTick, Is.EqualTo(duration));
            Assert.That(forecast.PlayerHealthDamage, Is.EqualTo(expectedHealthDamage));
            Assert.That(forecast.PlayerResistanceDamage, Is.EqualTo(expectedResistanceDamage));
            Assert.That(forecast.EnemyResistanceDamage, Is.EqualTo(expectedEnemyResistanceDamage));
            Assert.That(duel.PlayerQueue, Is.Empty);
            Assert.That(duel.Player.Health, Is.EqualTo(100));
        }

        [TestCase(1, 2)]
        [TestCase(602, 4)]
        public void OwnedTechnique_GrantsClashExperienceOnceAndPaysActOnce_AcrossAllItsSegments(int skillId, int enemyFastCount)
        {
            var run = new CampaignRun();
            var owned = run.GetOwnedSkill(skillId);
            var enemy = Enumerable.Range(0, enemyFastCount).Select(i => Attack(9000 + i, 1, 1)).ToArray();
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { owned.Skill }, enemy, new[] { enemy.Length }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(owned.Skill.LaneIndex), Is.True);
            int paid = duel.Act;
            int experience = owned.Experience;
            duel.Commit();
            int grants = 0;
            while (!duel.IsTurnResolved)
            {
                var result = duel.ResolveNextSlot();
                if (run.TryGainClashExperience(result.PlayerSkill, result.EnemySkill)) grants++;
            }
            Assert.That(grants, Is.EqualTo(1));
            Assert.That(owned.Experience, Is.EqualTo(experience + 1));
            Assert.That(duel.Act, Is.EqualTo(paid));
            Assert.That(paid, Is.EqualTo(3 - owned.Skill.Cost));
        }

        [Test]
        public void PreparingACharge_DoesNotConsumeItsPowerBuffUntilTheOneTechniqueCompletes()
        {
            var source = LegacySkillDefinitions.Skill(10);
            var grant = new LegacySkill(source.Id, source.Name, source.Cost, 4, 4, source.Kind, source.Property,
                source.AttackCount, source.LaneIndex, source.Description);
            var guard = new LegacySkill(9099, "guard", 0, 100, 100, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 0, "");
            var duel = Duel(new[] { grant, Attack(9000, 20, 4) },
                new[] { guard, Attack(9010, 1, 1), Attack(9011, 1, 1), Attack(9012, 1) });
            duel.Commit(); duel.ResolveNextSlot();
            Assert.That(duel.PlayerStatuses.Single(status => status.Kind == LegacyStatusKind.PowerUp).RemainingCount, Is.EqualTo(1));
            duel.ResolveNextSlot(); duel.ResolveNextSlot();
            Assert.That(duel.PlayerStatuses.Single(status => status.Kind == LegacyStatusKind.PowerUp).RemainingCount, Is.EqualTo(1));
            var final = duel.BeginNextSlot();
            Assert.That(final.PlayerFeedback.PowerBuffPercent, Is.EqualTo(50));
            Assert.That(duel.PlayerStatuses.Any(status => status.Kind == LegacyStatusKind.PowerUp), Is.False);
            var result = duel.ResolveNextSlot();
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo(30));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(8)]
        public void MixedTempoAgainstAnEnemyGroup_KeepsEveryIntervalOnItsActualReservationOwner(int seed)
        {
            var player = new[] { Attack(9000, 5, 1), Attack(9001, 5, 1), Attack(9002, 20, 4) };
            var enemies = new[]
            {
                new LegacyEnemyDefinition("A", 1000, 1000, new EnemyScript(new[] { new[] { Attack(9100, 3) } })),
                new LegacyEnemyDefinition("B", 1000, 1000, new EnemyScript(new[] { new[] { Attack(9101, 7) } }))
            };
            var duel = new LegacyQueuedDuel(1000, 1000, player, enemies, seed, meshPercent: 0);
            int firstOwner = duel.EnemyOwnerAtSlot(0), secondOwner = duel.EnemyOwnerAtSlot(1);
            int firstPower = duel.EnemyQueue[0].MaxPower, secondPower = duel.EnemyQueue[1].MaxPower;
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
            duel.Commit();
            while (!duel.IsTurnResolved)
            {
                var slot = duel.BeginNextSlot();
                if (slot.EnemyQueueIndex >= 0)
                    Assert.That(duel.ActiveEnemyIndex, Is.EqualTo(duel.EnemyOwnerAtSlot(slot.EnemyQueueIndex)));
                int owner = duel.ActiveEnemyIndex;
                int[] health = duel.Enemies.Select(enemy => enemy.Health).ToArray();
                int[] resistance = duel.Enemies.Select(enemy => enemy.Resistance).ToArray();
                duel.ResolveNextSlot();
                for (int i = 0; i < duel.Enemies.Count; i++)
                    if (i != owner)
                    {
                        Assert.That(duel.Enemies[i].Health, Is.EqualTo(health[i]));
                        Assert.That(duel.Enemies[i].Resistance, Is.EqualTo(resistance[i]));
                    }
            }
            Assert.That(duel.Enemies[firstOwner].Health, Is.EqualTo(995));
            Assert.That(duel.Enemies[firstOwner].Resistance, Is.EqualTo(995));
            Assert.That(duel.Enemies[secondOwner].Health, Is.EqualTo(980));
            Assert.That(duel.Enemies[secondOwner].Resistance, Is.EqualTo(1000));
            Assert.That(duel.Player.Health, Is.EqualTo(1000 - secondPower));
            Assert.That(duel.Player.Resistance, Is.EqualTo(1000 - firstPower));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(8)]
        public void AnEnemyKilledWhilePreparing_CannotGiveItsUnfinishedTechniqueToTheNextLivingEnemy(int seed)
        {
            var player = new[] { Attack(9000, 5, 1), Attack(9001, 1, 1), Attack(9002, 20, 4) };
            var enemies = new[]
            {
                new LegacyEnemyDefinition("A", 5, 0, new EnemyScript(new[] { new[] { Attack(9100, 3) } })),
                new LegacyEnemyDefinition("B", 5, 0, new EnemyScript(new[] { new[] { Attack(9101, 7) } }))
            };
            var duel = new LegacyQueuedDuel(1000, 1000, player, enemies, seed, meshPercent: 0);
            int firstOwner = duel.EnemyOwnerAtSlot(0), secondOwner = duel.EnemyOwnerAtSlot(1);
            int secondPower = duel.EnemyQueue[1].MaxPower;
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
            duel.Commit(); duel.ResolveNextSlot();
            Assert.That(duel.Enemies[firstOwner].IsDefeated, Is.True);
            Assert.That(duel.IsEnemySlotCanceled(0), Is.True);
            var second = duel.BeginNextSlot();
            Assert.That(second.EnemySkill, Is.Null);
            Assert.That(second.EnemyActionSkill, Is.Null);
            Assert.That(duel.ActiveEnemyIndex, Is.EqualTo(secondOwner));
            duel.ResolveNextSlot();
            Assert.That(duel.Enemies[secondOwner].Health, Is.EqualTo(3), "The surviving enemy started with broken resistance, so the one-power hit deals two.");
            duel.ResolveNextSlot(); duel.ResolveNextSlot();
            Assert.That(duel.Player.Health, Is.EqualTo(1000 - secondPower));
            Assert.That(duel.Player.Resistance, Is.EqualTo(1000));
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
        }

        [Test]
        public void AGroupCounterHint_AfterAChargeUsesTimeRatherThanTheNumberOfQueuedCards()
        {
            var charge = Attack(9000, 5, 4); var fast = Attack(9001, 5, 1);
            var counters = new[] { new LegacyCounter(Attack(9200, 5)), new LegacyCounter(Attack(9201, 5)) };
            var script = new EnemyScript(new[] { new[] { Attack(9100, 3) } });
            var enemies = new[]
            {
                new LegacyEnemyDefinition("A", 1000, 1000, script, counters[0]),
                new LegacyEnemyDefinition("B", 1000, 1000, script, counters[1])
            };
            var duel = new LegacyQueuedDuel(1000, 1000, new[] { charge, fast }, enemies, meshPercent: 0);
            int owner = duel.EnemyOwnerAtSlot(1);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.EnemyCounterFacing(fast), Is.SameAs(counters[owner].Skill));
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1));
            Assert.That(duel.Act, Is.EqualTo(2));
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit(); duel.ResolveNextSlot(); duel.ResolveNextSlot();
            var final = duel.BeginNextSlot();
            Assert.That(final.EnemyCountered, Is.True);
            Assert.That(final.EnemySkill, Is.SameAs(counters[owner].Skill));
        }

        [Test]
        public void ExecutionProgress_RemainsAvailableBetweenSlots_AndClampsAtTheEnd()
        {
            var duel = Duel(new[] { Attack(9000, 2, 1), Attack(9001, 2, 1) }, new[] { Attack(9002, 2) });
            duel.Commit(); duel.ResolveNextSlot();
            Assert.That(duel.CurrentSlot, Is.Null);
            Assert.That(duel.StartHalfTickAtSegment(0), Is.Zero);
            Assert.That(duel.StartHalfTickAtSegment(1), Is.EqualTo(1));
            Assert.That(duel.StartHalfTickAtSegment(2), Is.EqualTo(2));
            Assert.That(duel.StartHalfTickAtSegment(99), Is.EqualTo(2));
            Assert.That(duel.StartHalfTickAtSegment(-1), Is.Zero);
            duel.ResolveNextSlot();
            Assert.That(duel.StartHalfTickAtSegment(1), Is.EqualTo(1), "The final displayed segment remains addressable.");
            duel.BeginNextTurn();
            Assert.That(duel.StartHalfTickAtSegment(1), Is.Zero);
        }

        [Test]
        public void AChargeInAMeshedChain_RemainsMeshedWhilePreparing_AndDoesNotAllowEarlySteps()
        {
            var charge = Attack(9000, 20, 4);
            var next = new LegacySkill(9001, "next school", 1, 5, 5, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 1, 1, "");
            var enemy = new[] { Attack(9100, 1, 1), Attack(9101, 1, 1), Attack(9102, 1) };
            var duel = new LegacyQueuedDuel(1000, 100, 1000, 100, new[] { charge, next }, enemy,
                new[] { enemy.Length }, meshPercent: 10);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(1), Is.True);
            duel.Commit();
            var preparing = duel.BeginNextSlot();
            Assert.That(preparing.PlayerSkill, Is.Null);
            Assert.That(preparing.PlayerActionSkill, Is.SameAs(charge));
            Assert.That(duel.IsCurrentSlotMeshed, Is.True);
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out _), Is.False);
            Assert.That(duel.StepAttemptsThisTurn, Is.Zero);
            duel.ResolveNextSlot(); duel.ResolveNextSlot();
            var completion = duel.BeginNextSlot();
            Assert.That(completion.PlayerMesh.BonusPercent, Is.EqualTo(20));
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(24));
        }
    }
}
