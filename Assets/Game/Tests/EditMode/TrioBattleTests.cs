using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class TrioBattleTests
    {
        [Test]
        public void SharedQueue_InterleavesOwnersReproducibly_AndKeepsEachScriptOrder()
        {
            LegacyQueuedDuel first = Trio(Attack(900, 1), Guard(910), actions: 2);
            LegacyQueuedDuel second = Trio(Attack(900, 1), Guard(910), actions: 2);
            int[] owners = Owners(first);
            Assert.That(owners, Is.EqualTo(Owners(second)));
            Assert.That(owners.Take(3).Distinct().Count(), Is.EqualTo(3));
            for (int i = 1; i < owners.Length; i++) Assert.That(owners[i], Is.Not.EqualTo(owners[i - 1]));
            foreach (int owner in Enumerable.Range(0, 3))
                Assert.That(owners.Count(value => value == owner), Is.EqualTo(2));

            var variants = new HashSet<string>();
            for (int seed = 1; seed <= 8; seed++)
                variants.Add(string.Join(",", Owners(Trio(Attack(900, 1), Guard(910), seed: seed))));
            Assert.That(variants.Count, Is.GreaterThan(1));
            first.Reset();
            Assert.That(Owners(first), Is.EqualTo(owners));
        }

        [Test]
        public void EachSlot_DamagesItsOwner_AndCompletionKeepsThePresentedOpponent()
        {
            LegacyQueuedDuel duel = Trio(Attack(900, 11), Guard(910), actions: 2);
            Queue(duel, 6);
            duel.Commit();
            for (int slot = 0; slot < 6; slot++)
            {
                int owner = duel.EnemyOwnerAtSlot(slot);
                int[] before = duel.Enemies.Select(enemy => enemy.Health).ToArray();
                duel.BeginNextSlot();
                Assert.That(duel.ActiveEnemyIndex, Is.EqualTo(owner));
                Assert.That(duel.Enemy, Is.SameAs(duel.Enemies[owner]));
                duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
                Assert.That(duel.ActiveEnemyIndex, Is.EqualTo(owner));
                for (int i = 0; i < 3; i++) Assert.That(duel.Enemies[i].Health, Is.EqualTo(before[i] - (i == owner ? 10 : 0)));
            }
            Assert.That(duel.TotalEnemyHealth, Is.EqualTo(240));
            Assert.That(duel.TotalEnemyMaxHealth, Is.EqualTo(300));
        }

        [Test]
        public void Break_OnlyCancelsThatOwnersLaterReservations_CurrentMultihitFinishes()
        {
            LegacyQueuedDuel duel = Trio(Attack(900, 12, 3), Attack(910, 3, 3), resistance: 1, actions: 2);
            Queue(duel, 1);
            int owner = duel.EnemyOwnerAtSlot(0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            LegacyHitResult firstHit = duel.ResolveNextHit();
            Assert.That(duel.Enemies[owner].IsResistanceBroken, Is.True);
            Assert.That(slot.IsResolved, Is.False, "Breaking is not the end of a multihit skill.");
            while (!slot.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
            Assert.That(slot.HitsResolved, Is.EqualTo(3));
            Assert.That(duel.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(-1), "There is no global enemy cutoff.");
            Assert.That(duel.Act, Is.EqualTo(3), "Cancellation never changes already-paid ACT.");
            for (int i = 1; i < duel.EnemyQueue.Count; i++)
            {
                bool canceled = duel.EnemyOwnerAtSlot(i) == owner;
                Assert.That(duel.IsEnemySlotCanceled(i), Is.EqualTo(canceled));
                Assert.That(duel.EnemyQueue[i], Is.Not.Null, "Public cards remain available for their fragments.");
                LegacySlotResult result = duel.ResolveNextSlot();
                Assert.That(result.EnemySkill == null, Is.EqualTo(canceled));
            }
            duel.BeginNextTurn();
            Assert.That(Enumerable.Range(0, duel.EnemyQueue.Count).Any(duel.IsEnemySlotCanceled), Is.False);
            Assert.That(duel.Enemies[owner].IsResistanceBroken, Is.True, "Broken enemies may reserve again next turn.");
        }

        [Test]
        public void Defeat_StopsTheCurrentMultihit_CancelsOnlyItsOwner_AndNeedsAllThreeForVictory()
        {
            LegacyQueuedDuel duel = Trio(Attack(900, 40, 4), Guard(910), health: 1, actions: 2);
            Queue(duel, 6);
            int firstOwner = duel.EnemyOwnerAtSlot(0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            LegacyHitResult first = duel.ResolveNextHit();
            Assert.That(first.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            Assert.That(slot.IsResolved, Is.True);
            Assert.That(slot.HitsResolved, Is.EqualTo(1));
            Assert.That(duel.IsEnemySlotCanceled(3), Is.True);
            Assert.That(duel.EnemyOwnerAtSlot(3), Is.EqualTo(firstOwner));
            duel.CompleteCurrentSlot();
            Assert.That(duel.IsFinished, Is.False);
            duel.ResolveNextSlot();
            Assert.That(duel.IsFinished, Is.False);
            duel.ResolveNextSlot();
            Assert.That(duel.IsFinished, Is.True);
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(duel.TotalEnemyHealth, Is.Zero);
        }

        [Test]
        public void DefeatedOwner_DoesNotReturnNextTurn_AndEmptyEnemySlotsKeepLastLivingTarget()
        {
            LegacyQueuedDuel duel = Trio(Attack(900, 11), Guard(910), health: 10, actions: 2);
            Queue(duel, 1);
            int fallen = duel.EnemyOwnerAtSlot(0);
            ResolveTurn(duel);
            Assert.That(duel.Enemies[fallen].IsDefeated, Is.True);
            duel.BeginNextTurn();
            Assert.That(Owners(duel), Has.None.EqualTo(fallen));
            Assert.That(duel.EnemyQueue.Count, Is.EqualTo(4));

            LegacyQueuedDuel tail = Trio(Attack(900, 2), Guard(910), actions: 1);
            Queue(tail, 5);
            int last = tail.EnemyOwnerAtSlot(2);
            tail.Commit();
            for (int i = 0; i < 5; i++)
            {
                tail.BeginNextSlot();
                Assert.That(tail.ActiveEnemyIndex, Is.EqualTo(i < 3 ? tail.EnemyOwnerAtSlot(i) : last));
                tail.ResolveNextHit();
                tail.CompleteCurrentSlot();
            }
            Assert.That(tail.Enemies[last].Health, Is.EqualTo(95), "The two unopposed casts meet no guard.");
        }

        [Test]
        public void Forecast_DeepCopiesEveryEnemy_AndMatchesLiveResolutionWithoutMutatingThePlan()
        {
            LegacyQueuedDuel duel = Trio(Attack(900, 12, 3), Attack(910, 3), resistance: 1, actions: 2);
            Queue(duel, 6);
            int[] health = duel.Enemies.Select(enemy => enemy.Health).ToArray();
            int[] resistance = duel.Enemies.Select(enemy => enemy.Resistance).ToArray();
            int[] owners = Owners(duel);
            IReadOnlyList<LegacySlotForecast> forecasts = duel.ForecastTurn();
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(duel.Enemies.Select(enemy => enemy.Health), Is.EqualTo(health));
            Assert.That(duel.Enemies.Select(enemy => enemy.Resistance), Is.EqualTo(resistance));
            Assert.That(Owners(duel), Is.EqualTo(owners));
            Assert.That(Enumerable.Range(0, duel.EnemyQueue.Count).Any(duel.IsEnemySlotCanceled), Is.False);
            duel.Commit();
            foreach (LegacySlotForecast forecast in forecasts)
            {
                LegacySlotResult result = duel.ResolveNextSlot();
                Assert.That(result.PlayerHealthDamage, Is.EqualTo(forecast.PlayerHealthDamage));
                Assert.That(result.EnemyHealthDamage, Is.EqualTo(forecast.EnemyHealthDamage));
                Assert.That(result.EnemyResistanceDamage, Is.EqualTo(forecast.EnemyResistanceDamage));
                Assert.That(result.EnemySkill?.Id, Is.EqualTo(forecast.EnemySkill?.Id));
                Assert.That(result.Outcome, Is.EqualTo(forecast.Outcome));
            }
        }

        [Test]
        public void OneOnOne_ExposesOneEnemyWithoutChangingItsQueueAndOutcome()
        {
            var duel = new LegacyQueuedDuel(100, 50, 10, 10, new[] { Attack(900, 11) },
                new[] { Guard(910) }, new[] { 1 }, meshPercent: 0);
            Assert.That(duel.Enemies.Count, Is.EqualTo(1));
            Assert.That(duel.Enemies[0], Is.SameAs(duel.Enemy));
            Assert.That(duel.EnemyOwnerAtSlot(0), Is.Zero);
            Assert.That(duel.EnemyOwnerAtSlot(1), Is.EqualTo(-1));
            Assert.That(duel.EnemyIndexForSlot(1), Is.Zero);
            Queue(duel, 1);
            ResolveTurn(duel);
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
        }

        [Test]
        public void StageFive_HasThreeWeakerEnemies_AndSixThreeSixActions_WithTheSameReward()
        {
            var campaign = new CampaignRun();
            Assert.That(campaign.TryStartStage(1), Is.True);
            for (int stage = 1; stage < 5; stage++)
            {
                campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
                Assert.That(campaign.TryStartNextStage(), Is.True);
            }
            LegacyQueuedDuel duel = campaign.CreateDuel(37);
            Assert.That(duel.Enemies.Count, Is.EqualTo(3));
            Assert.That(duel.Enemies.All(enemy => enemy.MaxHealth == 50 && enemy.MaxResistance == 10), Is.True);
            Assert.That(duel.TotalEnemyMaxHealth, Is.EqualTo(150));
            Assert.That(duel.EnemyCounter, Is.Null);
            foreach (int count in new[] { 6, 3, 6 })
            {
                Assert.That(duel.EnemyQueue.Count, Is.EqualTo(count));
                foreach (LegacySkill skill in duel.EnemyQueue)
                {
                    LegacySkill basis = LegacySkillDefinitions.Skill(skill.Id);
                    Assert.That(skill.MinPower, Is.EqualTo(System.Math.Max(1, basis.MinPower * 70 / 100)));
                    Assert.That(skill.MaxPower, Is.EqualTo(System.Math.Max(1, basis.MaxPower * 70 / 100)));
                }
                ResolveTurn(duel);
                if (!duel.IsFinished) duel.BeginNextTurn();
            }
            Assert.That(campaign.CurrentStage.FirstClearSkillId, Is.EqualTo(46));
            Assert.That(campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(campaign.OwnedSkills.Any(owned => owned.SkillId == 46), Is.True);
        }

        private static LegacyQueuedDuel Trio(LegacySkill player, LegacySkill enemy, int health = 100,
            int resistance = 10, int actions = 2, int seed = 17)
        {
            var turn = Enumerable.Repeat(enemy, actions).ToArray();
            var script = new EnemyScript(new IReadOnlyList<LegacySkill>[] { turn });
            // Identical attacks across two lanes keep long owner-resolution sequences legal at three reservations per lane.
            var secondLane = new LegacySkill(player.Id, player.Name, player.Cost, player.MinPower, player.MaxPower,
                player.Kind, player.Property, player.AttackCount, 1, player.Description, player.AnimationName, player.IconId);
            return new LegacyQueuedDuel(10000, 10000, new[] { player, secondLane }, new[]
            {
                new LegacyEnemyDefinition("수련생 1", health, resistance, script),
                new LegacyEnemyDefinition("수련생 2", health, resistance, script),
                new LegacyEnemyDefinition("수련생 3", health, resistance, script),
            }, seed, meshPercent: 0);
        }

        private static LegacySkill Attack(int id, int power, int hits = 1)
            => new LegacySkill(id, "공격", 0, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, "");
        private static LegacySkill Guard(int id)
            => new LegacySkill(id, "방어", 0, 0, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");
        private static int[] Owners(LegacyQueuedDuel duel)
            => Enumerable.Range(0, duel.EnemyQueue.Count).Select(duel.EnemyOwnerAtSlot).ToArray();
        private static void Queue(LegacyQueuedDuel duel, int count)
        {
            for (int i = 0; i < count; i++) Assert.That(duel.TryQueueLane(i / LegacyLaneLimit.MaxReservations), Is.True);
        }
        private static void ResolveTurn(LegacyQueuedDuel duel)
        {
            duel.Commit();
            while (!duel.IsFinished && !duel.IsTurnResolved) duel.ResolveNextSlot();
        }
    }
}
