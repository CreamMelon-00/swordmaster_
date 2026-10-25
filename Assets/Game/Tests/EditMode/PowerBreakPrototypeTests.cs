using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class PowerBreakPrototypeTests
    {
        private static LegacySkill Attack(int id, int power, int duration = 2, int hits = 1)
            => new LegacySkill(id, "power break " + id, 0, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty, durationHalfUnits: duration);

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard " + id, 0, power, power, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 0, string.Empty);

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy,
            LegacyCounter playerCounter = null, LegacyCounter enemyCounter = null)
        {
            var duel = new LegacyQueuedDuel(10000, 50, 10000, 50, player, enemy, new[] { enemy.Length },
                playerCounter: playerCounter, enemyCounter: enemyCounter, meshPercent: 0, usePowerBreak: true);
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            return duel;
        }

        private static LegacyHitResult Finish(LegacyQueuedDuel duel)
        {
            LegacyHitResult first = null;
            while (!duel.CurrentSlot.IsResolved)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                if (first == null) first = hit;
            }
            duel.CompleteCurrentSlot();
            return first;
        }

        [TestCase(4, false, false)]
        [TestCase(5, true, false)]
        [TestCase(6, true, false)]
        [TestCase(4, false, true)]
        [TestCase(5, true, true)]
        [TestCase(6, true, true)]
        public void ThresholdComparesTheWholeTechniqueOnce_AndDestroysOnlyTheWeakerSidesNextReservation(
            int difference, bool breaks, bool playerWeaker)
        {
            var strong = new[] { Attack(99101, 10 + difference), Attack(99102, 6), Attack(99103, 7) };
            var weak = new[] { Attack(99104, 10), Attack(99105, 6), Attack(99106, 7) };
            LegacyQueuedDuel duel = Duel(playerWeaker ? weak : strong, playerWeaker ? strong : weak);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerTotalPower, Is.EqualTo(playerWeaker ? 10 : 10 + difference));
            Assert.That(slot.EnemyTotalPower, Is.EqualTo(playerWeaker ? 10 + difference : 10));
            LegacyHitResult hit = Finish(duel);
            Assert.That(playerWeaker ? hit.PlayerBroken : hit.EnemyBroken, Is.EqualTo(breaks));
            Assert.That(playerWeaker ? hit.EnemyBroken : hit.PlayerBroken, Is.False);
            Assert.That(playerWeaker ? duel.IsPlayerSkillDestroyed(1) : duel.IsEnemySkillDestroyed(1), Is.EqualTo(breaks));
            Assert.That(duel.IsPlayerSkillDestroyed(2), Is.False);
            Assert.That(duel.IsEnemySkillDestroyed(2), Is.False);
            Assert.That(hit.PlayerResistanceDamage + hit.EnemyResistanceDamage, Is.Zero);
            Assert.That(duel.Player.MaxResistance + duel.Enemy.MaxResistance, Is.Zero);
            Assert.That(duel.Player.IsResistanceBroken || duel.Enemy.IsResistanceBroken, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AGuardCannotBeBrokenByAnOverwhelmingAttack(bool playerGuard)
        {
            var guarded = new[] { Guard(99111, 1), Attack(99112, 7) };
            var attacked = new[] { Attack(99113, 50), Attack(99114, 7) };
            var duel = Duel(playerGuard ? guarded : attacked, playerGuard ? attacked : guarded);
            duel.Commit(); duel.BeginNextSlot();
            LegacyHitResult hit = Finish(duel);
            Assert.That(hit.PlayerBroken || hit.EnemyBroken, Is.False);
            Assert.That(duel.IsPlayerSkillDestroyed(1) || duel.IsEnemySkillDestroyed(1), Is.False);
        }

        [Test]
        public void OnlyTheDestroyedReservationIsExposed_TheCurrentAndFollowingSkillsKeepTheirOrdinaryDamage()
        {
            var duel = Duel(new[] { Attack(99121, 20), Attack(99122, 6), Attack(99123, 7) },
                new[] { Attack(99124, 5), Attack(99125, 8), Attack(99126, 7) });
            duel.Commit(); duel.BeginNextSlot();
            LegacyHitResult breaking = Finish(duel);
            Assert.That(breaking.EnemyHealthDamage, Is.EqualTo(20), "The current losing technique is not itself an exposed gap.");
            LegacyCurrentSlot gap = duel.BeginNextSlot();
            Assert.That(gap.EnemySkill, Is.Null);
            Assert.That(gap.EnemyBrokenGap, Is.True);
            Assert.That(gap.EnemyQueueIndex, Is.EqualTo(1));
            Assert.That(duel.EnemyQueue[1].Id, Is.EqualTo(99125), "The original reservation remains available to the shatter cue.");
            Assert.That(Finish(duel).EnemyHealthDamage, Is.EqualTo(12));
            LegacyCurrentSlot following = duel.BeginNextSlot();
            Assert.That(following.EnemySkill.Id, Is.EqualTo(99126));
            Assert.That(following.StartHalfTick, Is.EqualTo(4));
            Assert.That(following.EnemyBrokenGap, Is.False);
            Assert.That(Finish(duel).EnemyHealthDamage, Is.EqualTo(7));
        }

        [Test]
        public void BreakingAMultiHitAction_DoesNotStopItsRemainingHitsOrDestroyMoreThanOneFollowingSkill()
        {
            var duel = Duel(new[] { Attack(99131, 20), Attack(99132, 6), Attack(99133, 7) },
                new[] { Attack(99134, 6, hits: 3), Attack(99135, 8), Attack(99136, 7) });
            duel.Commit(); duel.BeginNextSlot();
            for (int i = 0; i < 3; i++)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.EnemyAttacked, Is.True);
                Assert.That(hit.EnemyBroken, Is.EqualTo(i == 0));
                Assert.That(hit.PlayerHealthDamage, Is.EqualTo(2));
                Assert.That(duel.IsEnemySkillDestroyed(1), Is.True);
                Assert.That(duel.IsEnemySkillDestroyed(2), Is.False);
            }
            duel.CompleteCurrentSlot();
        }

        [Test]
        public void ADestroyedFastSkill_ExposesItsHalfCellButNotItsOriginalPadding()
        {
            var duel = Duel(new[] { Attack(99141, 20), Attack(99142, 6, 1), Attack(99143, 6, 1), Attack(99144, 7) },
                new[] { Attack(99145, 5), Attack(99146, 8, 1), Attack(99147, 7) });
            duel.Commit(); duel.BeginNextSlot(); Finish(duel);
            LegacyCurrentSlot fastGap = duel.BeginNextSlot();
            Assert.That(fastGap.StartHalfTick, Is.EqualTo(2));
            Assert.That(fastGap.EndHalfTick, Is.EqualTo(3));
            Assert.That(fastGap.EnemyBrokenGap, Is.True);
            Assert.That(Finish(duel).EnemyHealthDamage, Is.EqualTo(12));
            LegacyCurrentSlot padding = duel.BeginNextSlot();
            Assert.That(padding.StartHalfTick, Is.EqualTo(3));
            Assert.That(padding.EndHalfTick, Is.EqualTo(4));
            Assert.That(padding.EnemySkill, Is.Null);
            Assert.That(padding.EnemyBrokenGap, Is.False);
            Assert.That(Finish(duel).EnemyHealthDamage, Is.EqualTo(6));
            LegacyCurrentSlot following = duel.BeginNextSlot();
            Assert.That(following.StartHalfTick, Is.EqualTo(4));
            Assert.That(following.EnemySkill.Id, Is.EqualTo(99147));
        }

        [Test]
        public void ADestroyedChargeSkill_ExposesItsWholeIntervalIncludingEarlierFastContacts()
        {
            var duel = Duel(new[] { Attack(99151, 20), Attack(99152, 6, 1), Attack(99153, 6, 1), Attack(99154, 6), Attack(99155, 7) },
                new[] { Attack(99156, 5), Attack(99157, 8, 4), Attack(99158, 7) });
            duel.Commit(); duel.BeginNextSlot(); Finish(duel);
            foreach (int end in new[] { 3, 4, 6 })
            {
                LegacyCurrentSlot gap = duel.BeginNextSlot();
                Assert.That(gap.EndHalfTick, Is.EqualTo(end));
                Assert.That(gap.EnemyBrokenGap, Is.True);
                Assert.That(gap.EnemyQueueIndex, Is.EqualTo(1));
                Assert.That(duel.QueueIndexAtSegment(false, gap.SlotIndex), Is.EqualTo(1));
                Assert.That(Finish(duel).EnemyHealthDamage, Is.EqualTo(12));
            }
            LegacyCurrentSlot following = duel.BeginNextSlot();
            Assert.That(following.StartHalfTick, Is.EqualTo(6));
            Assert.That(following.EnemyBrokenGap, Is.False);
            Assert.That(following.EnemySkill.Id, Is.EqualTo(99158));
        }

        [Test]
        public void DestroyedGapDoesNotConsumeACounter_ButALaterNaturalEmptySlotStillCan()
        {
            var counter = new LegacyCounter(Attack(99164, 4), 1);
            var duel = Duel(new[] { Attack(99161, 20), Attack(99162, 6), Attack(99163, 6) },
                new[] { Attack(99165, 5), Attack(99166, 7) }, enemyCounter: counter);
            duel.Commit(); duel.BeginNextSlot(); Finish(duel);
            LegacyCurrentSlot gap = duel.BeginNextSlot();
            Assert.That(gap.EnemyCountered, Is.False);
            Assert.That(duel.EnemyCountersRemaining, Is.EqualTo(1));
            Assert.That(Finish(duel).EnemyHealthDamage, Is.EqualTo(12));
            LegacyCurrentSlot empty = duel.BeginNextSlot();
            Assert.That(empty.EnemyBrokenGap, Is.False);
            Assert.That(empty.EnemyCountered, Is.True);
            Assert.That(duel.EnemyCountersRemaining, Is.Zero);
        }

        [Test]
        public void ForecastUsesTheSameDestructionAndDamageWithoutChangingTheLiveQueueOrFighters()
        {
            var duel = Duel(new[] { Attack(99171, 20), Attack(99172, 6), Attack(99173, 7) },
                new[] { Attack(99174, 5), Attack(99175, 8), Attack(99176, 7) });
            var forecast = new List<LegacySlotForecast>();
            duel.ForecastTurn(forecast);
            Assert.That(forecast.Count, Is.EqualTo(3));
            Assert.That(forecast[0].EnemyBroken, Is.True);
            Assert.That(forecast[0].Summary, Does.Contain("다음 기술 파괴"));
            Assert.That(forecast[1].EnemyBrokenGap, Is.True);
            Assert.That(forecast[1].EnemyHealthDamage, Is.EqualTo(12));
            Assert.That(forecast[2].EnemySkill.Id, Is.EqualTo(99176));
            Assert.That(duel.IsEnemySkillDestroyed(1), Is.False);
            Assert.That(duel.Enemy.Health, Is.EqualTo(10000));
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            duel.Commit();
            for (int i = 0; i < forecast.Count; i++)
            {
                duel.BeginNextSlot();
                LegacyHitResult hit = Finish(duel);
                Assert.That(hit.EnemyHealthDamage, Is.EqualTo(forecast[i].EnemyHealthDamage));
            }
            duel.BeginNextTurn();
            Assert.That(duel.IsEnemySkillDestroyed(1), Is.False);
            Assert.That(duel.Enemy.IsResistanceBroken, Is.False);
            duel.Reset();
            Assert.That(duel.Enemy.Health, Is.EqualTo(10000));
            Assert.That(duel.IsEnemySkillDestroyed(1), Is.False);
        }

        [Test]
        public void SharedEnemyQueue_DestroysOnlyThatOwnersNextSkill_AndTheGapStillTargetsItsOwner()
        {
            var weak = Attack(99177, 5);
            var script = new EnemyScript(new[] { new[] { weak, weak, weak } });
            var duel = new LegacyQueuedDuel(10000, 50, new[] { Attack(99178, 20) },
                new[] { new LegacyEnemyDefinition("first", 10000, 50, script),
                    new LegacyEnemyDefinition("second", 10000, 50, script) },
                meshPercent: 0, usePowerBreak: true);
            for (int i = 0; i < 3; i++) Assert.That(duel.TryQueueLane(0), Is.True);
            int firstOwner = duel.EnemyOwnerAtSlot(0);
            int nextOwnIndex = -1;
            for (int i = 1; i < duel.EnemyQueue.Count; i++)
                if (duel.EnemyOwnerAtSlot(i) == firstOwner) { nextOwnIndex = i; break; }
            Assert.That(nextOwnIndex, Is.EqualTo(2), "The shared queue alternates the two enemies.");
            duel.Commit(); duel.BeginNextSlot(); Finish(duel);
            Assert.That(duel.IsEnemySkillDestroyed(1), Is.False, "The intervening ally's skill is intact.");
            Assert.That(duel.IsEnemySkillDestroyed(nextOwnIndex), Is.True);
            duel.BeginNextSlot(); Finish(duel);
            int firstHealth = duel.Enemies[firstOwner].Health;
            int otherHealth = duel.Enemies[1 - firstOwner].Health;
            LegacyCurrentSlot gap = duel.BeginNextSlot();
            Assert.That(duel.ActiveEnemyIndex, Is.EqualTo(firstOwner));
            Assert.That(gap.EnemyBrokenGap, Is.True);
            Assert.That(Finish(duel).EnemyHealthDamage, Is.EqualTo(40));
            Assert.That(duel.Enemies[firstOwner].Health, Is.EqualTo(firstHealth - 40));
            Assert.That(duel.Enemies[1 - firstOwner].Health, Is.EqualTo(otherHealth));
            Assert.That(duel.IsEnemySkillDestroyed(4), Is.False);
            Assert.That(duel.IsEnemySkillDestroyed(5), Is.False);
        }

        [Test]
        public void ADestroyedReservationOfAnEnemyKilledByTheBreakingHit_DoesNotExposeItsLivingAlly()
        {
            var weak = Attack(99187, 5);
            var script = new EnemyScript(new[] { new[] { weak, weak, weak } });
            LegacyQueuedDuel duel = null;
            for (int seed = 1; seed <= 32; seed++)
            {
                var candidate = new LegacyQueuedDuel(10000, 50,
                    new[] { Attack(99188, 20), Attack(99189, 6), Attack(99190, 6) },
                    new[] { new LegacyEnemyDefinition("falls", 10, 50, script),
                        new LegacyEnemyDefinition("survives", 1000, 50, script) },
                    randomSeed: seed, meshPercent: 0, usePowerBreak: true);
                if (candidate.EnemyOwnerAtSlot(0) == 0) { duel = candidate; break; }
            }
            Assert.That(duel, Is.Not.Null);
            for (int i = 0; i < 3; i++) Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit(); duel.BeginNextSlot();
            Assert.That(Finish(duel).EnemyBroken, Is.True);
            Assert.That(duel.Enemies[0].IsDefeated, Is.True);
            Assert.That(duel.IsEnemySkillDestroyed(2), Is.True, "The destruction event can remain recorded after its owner falls.");
            duel.BeginNextSlot(); Finish(duel);
            int allyHealth = duel.Enemies[1].Health;
            LegacyCurrentSlot canceled = duel.BeginNextSlot();
            Assert.That(duel.ActiveEnemyIndex, Is.EqualTo(1));
            Assert.That(canceled.EnemyBrokenGap, Is.False, "A dead fighter's exposed interval cannot move to a living ally.");
            Assert.That(duel.Enemy.IsBrokenGap, Is.False);
            Assert.That(Finish(duel).EnemyHealthDamage, Is.EqualTo(6));
            Assert.That(duel.Enemies[1].Health, Is.EqualTo(allyHealth - 6));
        }

        [Test]
        public void LocalVersusAndMirroredPerspectiveUseTheSameSingleDestructionRule()
        {
            var match = new LocalVersusMatch(new[] { Attack(99181, 20), Attack(99182, 6), Attack(99183, 7) },
                new[] { Attack(99184, 5), Attack(99185, 8), Attack(99186, 7) },
                leftHealth: 10000, rightHealth: 10000, usePowerBreak: true);
            for (int i = 0; i < 3; i++) Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            for (int i = 0; i < 3; i++) Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(match.TryCommit(1), Is.True);
            match.BeginNextSlot();
            LocalVersusHit hit = match.ResolveNextHit();
            Assert.That(hit.RightBroken, Is.True);
            match.CompleteCurrentSlot();
            var left = new LocalVersusPerspective(match, 0);
            var right = new LocalVersusPerspective(match, 1);
            Assert.That(left.IsEnemySkillDestroyed(1), Is.True);
            Assert.That(right.IsPlayerSkillDestroyed(1), Is.True);
            Assert.That(left.IsEnemySkillDestroyed(2), Is.False);
            LocalVersusSlot gap = match.BeginNextSlot();
            Assert.That(gap.RightBrokenGap, Is.True);
            Assert.That(left.CurrentSlot.EnemyBrokenGap, Is.True);
            Assert.That(right.CurrentSlot.PlayerBrokenGap, Is.True);
            Assert.That(match.ResolveNextHit().RightHealthDamage, Is.EqualTo(12));
            match.CompleteCurrentSlot();
            Assert.That(match.BeginNextSlot().RightSkill.Id, Is.EqualTo(99186));
        }
    }
}
