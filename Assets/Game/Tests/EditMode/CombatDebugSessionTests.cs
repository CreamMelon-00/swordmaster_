using System;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class CombatDebugSessionTests
    {
        [TestCase(0, 5, 0, 5)]
        [TestCase(1, 1, 1, 1)]
        [TestCase(10, 10, 10, 10)]
        [TestCase(-4, 20, 0, 10)]
        [TestCase(20, -4, 10, 0)]
        [TestCase(0, 0, 0, 0)]
        public void Factory_PreparesTheClampedQueues_ThatCommitAndResolveInFull(int enemyCount, int playerCount,
            int expectedEnemy, int expectedPlayer)
        {
            LegacyQueuedDuel duel = LegacyCombatDebugSession.Create(enemyCount, playerCount);
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That((duel.EnemyQueue.Count, duel.PlayerQueue.Count), Is.EqualTo((expectedEnemy, expectedPlayer)));
            Assert.That(duel.LaneReservationsThisTurn(0), Is.Zero, "Prepared samples do not spend manual reservations.");
            duel.Commit();
            int resolved = 0;
            while (!duel.IsTurnResolved && !duel.IsFinished)
            {
                duel.ResolveNextSlot();
                resolved++;
            }
            Assert.That(resolved, Is.EqualTo(Math.Max(expectedEnemy, expectedPlayer)));
            Assert.That(duel.IsTurnResolved, Is.True);
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            Assert.That(duel.Player.IsDefeated || duel.Enemy.IsDefeated, Is.False);
        }

        [Test]
        public void DefaultSample_QueuesFiveFixedAttacks_WithNormalStepFeaturesButNoMeshingOrCounters()
        {
            LegacyQueuedDuel duel = LegacyCombatDebugSession.Create(1);
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(5));
            LegacySkill skill = duel.PlayerQueue[0];
            Assert.That((skill.Cost, skill.MinPower, skill.MaxPower, skill.AttackCount, skill.IconId), Is.EqualTo((0, 10, 10, 1, 1)));
            Assert.That((skill.Kind, skill.Property), Is.EqualTo((LegacySkillKind.Attack, LegacySkillProperty.Slash)));
            Assert.That(skill.Id, Is.LessThan(-1), "A separate debug skill, never a player's owned technique or the common breath.");
            Assert.That(duel.Features, Is.EqualTo(CombatFeature.All));
            Assert.That(duel.MeshPercent, Is.Zero);
            Assert.That(duel.PlayerCounter, Is.Null);
            Assert.That(duel.EnemyCounter, Is.Null);
            Assert.That((duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance),
                Is.EqualTo((100, 30, 100, 30)));
            Assert.That((duel.Player.HealthFloor, duel.Enemy.HealthFloor), Is.EqualTo((1, 1)));
        }

        [Test]
        public void BothFloors_PreserveDamageNumbersAndBreaks_WithoutDefeat_AndSurviveReset()
        {
            LegacyQueuedDuel duel = HeavyDuel(1, 1);
            duel.Commit();
            duel.BeginNextSlot();
            LegacyHitResult first = duel.ResolveNextHit();
            Assert.That((duel.Player.Health, duel.Enemy.Health), Is.EqualTo((1, 1)));
            Assert.That(duel.Player.IsResistanceBroken && duel.Enemy.IsResistanceBroken, Is.True);
            Assert.That((first.PlayerHealthDamage, first.EnemyHealthDamage), Is.EqualTo((99, 99)));
            Assert.That(first.PlayerDisplayedDamage, Is.GreaterThan(first.PlayerHealthDamage));
            Assert.That(first.EnemyDisplayedDamage, Is.GreaterThan(first.EnemyHealthDamage));
            duel.CompleteCurrentSlot();
            // Both sides broke, so the next reservation is canceled. Exercise the floor's displayed damage with a fresh
            // skill next turn instead: the fighters are still alive at one HP and the break cancellation has expired.
            LegacySlotResult canceled = duel.ResolveNextSlot();
            Assert.That(canceled.PlayerSkill, Is.Null);
            Assert.That(canceled.EnemySkill, Is.Null);
            Assert.That(duel.IsTurnResolved && !duel.IsFinished, Is.True);
            duel.BeginNextTurn();
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            LegacyHitResult second = duel.ResolveNextHit();
            Assert.That((second.PlayerHealthDamage, second.EnemyHealthDamage), Is.EqualTo((0, 0)));
            Assert.That(second.PlayerDisplayedDamage, Is.GreaterThan(0));
            Assert.That(second.EnemyDisplayedDamage, Is.GreaterThan(0));
            duel.CompleteCurrentSlot();
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            Assert.That(duel.IsTurnResolved && !duel.IsFinished, Is.True);

            duel.Reset();
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That((duel.PlayerHealthFloor, duel.EnemyHealthFloor, duel.Player.HealthFloor, duel.Enemy.HealthFloor),
                Is.EqualTo((1, 1, 1, 1)));
            Assert.That((duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance),
                Is.EqualTo((100, 30, 100, 30)));
        }

        [Test]
        public void ForecastCopy_KeepsBothFloors_AndLeavesTheOriginalPlanningDuelUntouched()
        {
            LegacyQueuedDuel duel = HeavyDuel(1, 1);
            IReadOnlyList<LegacySlotForecast> forecast = duel.ForecastTurn();
            Assert.That(forecast.Count, Is.EqualTo(2));
            foreach (LegacySlotForecast slot in forecast)
            {
                Assert.That(slot.Reached, Is.True);
                Assert.That(slot.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            }
            Assert.That((forecast[0].PlayerHealthDamage, forecast[0].EnemyHealthDamage), Is.EqualTo((99, 99)));
            Assert.That((forecast[1].PlayerHealthDamage, forecast[1].EnemyHealthDamage), Is.EqualTo((0, 0)));
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That((duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance),
                Is.EqualTo((100, 30, 100, 30)));
            Assert.That((duel.PlayerQueue.Count, duel.EnemyQueue.Count), Is.EqualTo((2, 2)));
        }

        [Test]
        public void NormalDuel_DefaultFloorsStillAllowDefeat()
        {
            LegacySkill attack = HeavyAttack();
            var duel = new LegacyQueuedDuel(100, 30, 100, 30, new[] { attack }, new[] { attack }, new[] { 1 });
            Assert.That((duel.PlayerHealthFloor, duel.EnemyHealthFloor), Is.EqualTo((0, 0)));
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.Player.IsDefeated && duel.Enemy.IsDefeated, Is.True);
            Assert.That(duel.IsFinished, Is.True);
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory), "Normal simultaneous defeat keeps its existing priority.");
        }

        [Test]
        public void PlayerFloor_UsesTheSameBounds_ForPatternAndScriptConstructors()
        {
            LegacySkill attack = HeavyAttack();
            var script = new EnemyScript(new[] { new[] { attack } });
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyQueuedDuel(100, 30, 100, 30,
                new[] { attack }, new[] { attack }, new[] { 1 }, playerHealthFloor: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyQueuedDuel(100, 30, 100, 30,
                new[] { attack }, script, playerHealthFloor: 100));
            var duel = new LegacyQueuedDuel(100, 30, 100, 30, new[] { attack }, script, playerHealthFloor: 1,
                enemyHealthFloor: 1);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That((duel.Player.Health, duel.Enemy.Health), Is.EqualTo((1, 1)));
        }

        private static LegacyQueuedDuel HeavyDuel(int playerFloor, int enemyFloor)
        {
            LegacySkill attack = HeavyAttack();
            var duel = new LegacyQueuedDuel(100, 30, 100, 30, new[] { attack }, new[] { attack }, new[] { 2 },
                playerHealthFloor: playerFloor, enemyHealthFloor: enemyFloor);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
            return duel;
        }

        private static LegacySkill HeavyAttack() => new LegacySkill(-10002, "Floor test", 0, 1000, 1000,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);
    }
}
