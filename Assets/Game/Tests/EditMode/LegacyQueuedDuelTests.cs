using System;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacyQueuedDuelTests
    {
        [Test]
        public void InitialTurn_UsesOriginalStatsThreeLanesAndEnemyForecast()
        {
            var duel = new LegacyQueuedDuel();

            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
            Assert.That(duel.Enemy.Health, Is.EqualTo(80));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(15));
            Assert.That(duel.Act, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, Ids(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, Ids(duel.GetLane(1)));
            CollectionAssert.AreEqual(new[] { 5, 6, 9 }, Ids(duel.GetLane(2)));
            CollectionAssert.AreEqual(new[] { 1, 2 }, Ids(duel.EnemyQueue));
        }

        [Test]
        public void Queueing_OnlySpendsActAndRotatesSelectedLane()
        {
            var duel = new LegacyQueuedDuel();

            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(duel.TryQueueLane(2), Is.True);

            Assert.That(duel.Act, Is.Zero);
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Enemy.Health, Is.EqualTo(80));
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(15));
            Assert.That(duel.RoundNumber, Is.EqualTo(1));
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            CollectionAssert.AreEqual(new[] { 1, 3, 5 }, Ids(duel.PlayerQueue));
            CollectionAssert.AreEqual(new[] { 2, 7, 1 }, Ids(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 4, 8, 3 }, Ids(duel.GetLane(1)));
            CollectionAssert.AreEqual(new[] { 6, 9, 5 }, Ids(duel.GetLane(2)));
        }

        [Test]
        public void UnaffordableSkill_DoesNotRotateOrEnterQueue()
        {
            var duel = new LegacyQueuedDuel();
            duel.TryQueueLane(2);

            Assert.That(duel.TryQueueLane(2), Is.False);
            Assert.That(duel.Act, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 5 }, Ids(duel.PlayerQueue));
            CollectionAssert.AreEqual(new[] { 6, 9, 5 }, Ids(duel.GetLane(2)));
        }

        [Test]
        public void Commit_FreezesQueueAndResolvesOnePairPerCall()
        {
            var duel = new LegacyQueuedDuel();
            duel.TryQueueLane(0);
            duel.TryQueueLane(1);
            duel.Commit();

            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Enemy.Health, Is.EqualTo(80));
            Assert.That(duel.TryQueueLane(2), Is.False);
            Assert.Throws<InvalidOperationException>(() => duel.BeginNextTurn());

            LegacySlotResult first = duel.ResolveNextSlot();
            Assert.That(first.SlotIndex, Is.Zero);
            Assert.That(first.PlayerSkill.Id, Is.EqualTo(1));
            Assert.That(first.EnemySkill.Id, Is.EqualTo(1));
            Assert.That(first.PlayerHealthDamage, Is.Zero);
            Assert.That(first.EnemyHealthDamage, Is.Zero);
            Assert.That(first.PlayerResistanceDamage, Is.InRange(4, 5));
            Assert.That(first.EnemyResistanceDamage, Is.InRange(4, 5));
            Assert.That(duel.IsTurnResolved, Is.False);

            LegacySlotResult second = duel.ResolveNextSlot();
            Assert.That(second.PlayerSkill.Id, Is.EqualTo(3));
            Assert.That(second.EnemySkill.Id, Is.EqualTo(2));
            Assert.That(duel.LastResolvedSlot, Is.EqualTo(1));
            Assert.That(duel.IsTurnResolved, Is.True);
            Assert.Throws<InvalidOperationException>(() => duel.ResolveNextSlot());
        }

        [Test]
        public void UnspentActCarriesAndCutAddsToFollowingTurnGain()
        {
            var duel = new LegacyQueuedDuel();
            duel.TryQueueLane(0);
            ResolveTurn(duel);

            Assert.That(duel.Act, Is.EqualTo(2));
            Assert.That(duel.NextActGain, Is.EqualTo(4));
            duel.BeginNextTurn();

            Assert.That(duel.Act, Is.EqualTo(6));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            Assert.That(duel.PlayerQueue, Is.Empty);
            CollectionAssert.AreEqual(new[] { 2, 7, 1 }, Ids(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, Ids(duel.EnemyQueue));
        }

        [Test]
        public void EmptyCommit_RunsFullEnemyQueueAndResourceIsCappedAtTen()
        {
            var duel = TestDuel(new[] { Attack(100, 1) }, new[] { Guard(101, 1) }, new[] { 2, 3, 2, 1 });
            for (int round = 0; round < 3; round++)
            {
                duel.Commit();
                int slots = 0;
                while (!duel.IsTurnResolved)
                {
                    LegacySlotResult result = duel.ResolveNextSlot();
                    Assert.That(result.PlayerSkill, Is.Null);
                    slots++;
                }
                Assert.That(slots, Is.EqualTo(new[] { 2, 3, 2 }[round]));
                duel.BeginNextTurn();
            }
            Assert.That(duel.Act, Is.EqualTo(10));
            Assert.That(duel.EnemyQueue.Count, Is.EqualTo(1));
        }

        [Test]
        public void EnemyPattern_PersistsAcrossTurnBoundariesAndWraps()
        {
            var duel = new LegacyQueuedDuel();
            ResolveTurn(duel);
            duel.BeginNextTurn();
            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, Ids(duel.EnemyQueue));
            ResolveTurn(duel);
            duel.BeginNextTurn();
            CollectionAssert.AreEqual(new[] { 6, 1 }, Ids(duel.EnemyQueue));
            ResolveTurn(duel);
            duel.BeginNextTurn();
            CollectionAssert.AreEqual(new[] { 2 }, Ids(duel.EnemyQueue));
        }

        [Test]
        public void MissingOpposingSlot_HitsHealthWithoutReducingResistance()
        {
            var duel = TestDuel(new[] { Attack(100, 4) }, new[] { Guard(101, 4) }, new[] { 1 });
            duel.TryQueueLane(0);
            duel.TryQueueLane(0);
            duel.Commit();
            LegacySlotResult guarded = duel.ResolveNextSlot();
            LegacySlotResult exposed = duel.ResolveNextSlot();

            Assert.That(guarded.EnemyHealthDamage, Is.Zero);
            Assert.That(exposed.EnemySkill, Is.Null);
            Assert.That(exposed.EnemyHealthDamage, Is.EqualTo(4));
            Assert.That(exposed.EnemyResistanceDamage, Is.Zero);
        }

        [Test]
        public void MultiHitAttack_DividesGuardPowerAcrossHits()
        {
            var duel = TestDuel(new[] { Attack(100, 12, 3) }, new[] { Guard(101, 6) }, new[] { 1 });
            duel.TryQueueLane(0);
            duel.Commit();

            LegacySlotResult result = duel.ResolveNextSlot();

            Assert.That(result.EnemyHealthDamage, Is.EqualTo(6));
            Assert.That(result.EnemyResistanceDamage, Is.Zero);
        }

        [Test]
        public void BrokenResistance_DoublesLaterHitsAndRecoversAfterOneFullTurn()
        {
            var duel = TestDuel(new[] { Attack(100, 4) }, new[] { Attack(101, 4) }, new[] { 2, 1, 1 }, 50, 5);
            duel.TryQueueLane(0);
            duel.TryQueueLane(0);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacySlotResult breaking = duel.ResolveNextSlot();

            Assert.That(breaking.EnemyHealthDamage, Is.EqualTo(3));
            Assert.That(duel.Enemy.Resistance, Is.Zero);
            duel.BeginNextTurn();
            Assert.That(duel.Enemy.Resistance, Is.Zero);
            duel.TryQueueLane(0);
            duel.Commit();
            LegacySlotResult broken = duel.ResolveNextSlot();
            Assert.That(broken.EnemyHealthDamage, Is.EqualTo(8));
            duel.BeginNextTurn();
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(5));
            Assert.That(duel.Player.Resistance, Is.EqualTo(5));
        }

        [Test]
        public void Stab_BoostsFollowingThreeSkillsWithinSamePhase()
        {
            LegacySkill stab = LegacyInitialSkills.All[2];
            var strike = new LegacySkill(100, "test", 0, 20, 20, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 1, "");
            var duel = TestDuel(new[] { stab, strike, strike, strike, strike }, new[] { Attack(101, 1) }, new[] { 5 }, 100, 1000);
            for (int i = 0; i < 5; i++) Assert.That(duel.TryQueueLane(1), Is.True);
            duel.Commit();

            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(3));
            for (int i = 0; i < 3; i++) Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(22));
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(20));
        }

        [Test]
        public void GuardAgainstHit_AddsTwoNextTurnAct()
        {
            var duel = TestDuel(new[] { LegacyInitialSkills.All[6] },
                new[] { new LegacySkill(100, "hit", 1, 6, 6, LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 0, "") }, new[] { 1 });
            duel.TryQueueLane(0);
            ResolveTurn(duel);

            Assert.That(duel.NextActGain, Is.EqualTo(5));
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(7));
        }

        [Test]
        public void SimultaneousLethalSlot_PrioritizesPlayerDefeatAndResetRestoresOpening()
        {
            var duel = TestDuel(new[] { Attack(100, 2) }, new[] { Attack(101, 2) }, new[] { 1 }, 3, 0);
            duel.TryQueueLane(0);
            duel.Commit();
            LegacySlotResult result = duel.ResolveNextSlot();

            Assert.That(result.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Finished));
            Assert.That(duel.Player.Health, Is.Zero);
            Assert.That(duel.Enemy.Health, Is.Zero);
            Assert.That(duel.TryQueueLane(0), Is.False);
            Assert.Throws<InvalidOperationException>(() => duel.BeginNextTurn());
            duel.Reset();
            Assert.That(duel.RoundNumber, Is.EqualTo(1));
            Assert.That(duel.Player.Health, Is.EqualTo(3));
            Assert.That(duel.Enemy.Health, Is.EqualTo(3));
            Assert.That(duel.Act, Is.EqualTo(3));
            Assert.That(duel.PlayerQueue, Is.Empty);
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
        }

        [Test]
        public void LethalAttack_EndsWithPlayerVictoryAndStopsRemainingSlots()
        {
            var duel = new LegacyQueuedDuel(100, 50, 3, 0,
                new[] { Attack(100, 4) }, new[] { Guard(101, 1) }, new[] { 2 });
            duel.TryQueueLane(0);
            duel.TryQueueLane(0);
            duel.Commit();

            LegacySlotResult result = duel.ResolveNextSlot();

            Assert.That(result.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(duel.Enemy.Health, Is.Zero);
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.IsFinished, Is.True);
            Assert.Throws<InvalidOperationException>(() => duel.ResolveNextSlot());
        }

        [Test]
        public void EmptyPlayerQueue_CanEndInEnemyVictory()
        {
            var duel = new LegacyQueuedDuel(3, 0, 100, 50,
                new[] { Guard(100, 4) }, new[] { Attack(101, 4) }, new[] { 1 });
            duel.Commit();

            LegacySlotResult result = duel.ResolveNextSlot();

            Assert.That(result.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(result.PlayerSkill, Is.Null);
            Assert.That(duel.Player.Health, Is.Zero);
            Assert.That(duel.Enemy.Health, Is.EqualTo(100));
        }

        [Test]
        public void Reset_RestoresRotatedLanesActAndOpeningForecast()
        {
            var duel = new LegacyQueuedDuel();
            duel.TryQueueLane(0);
            duel.TryQueueLane(1);
            ResolveTurn(duel);
            duel.BeginNextTurn();

            duel.Reset();

            Assert.That(duel.Act, Is.EqualTo(3));
            Assert.That(duel.LastResolvedSlot, Is.EqualTo(-1));
            Assert.That(duel.PlayerQueue, Is.Empty);
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Enemy.Health, Is.EqualTo(80));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, Ids(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, Ids(duel.GetLane(1)));
            CollectionAssert.AreEqual(new[] { 1, 2 }, Ids(duel.EnemyQueue));
        }

        [Test]
        public void BeginSlot_InitializesEffectsWithoutDealingAnyHit()
        {
            var duel = new LegacyQueuedDuel();
            duel.TryQueueLane(0);
            duel.Commit();

            LegacyCurrentSlot slot = duel.BeginNextSlot();

            Assert.That(slot.PlayerSkill.Id, Is.EqualTo(1));
            Assert.That(slot.EnemySkill.Id, Is.EqualTo(1));
            Assert.That(slot.HitsResolved, Is.Zero);
            Assert.That(slot.HitCount, Is.EqualTo(1));
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
            Assert.That(duel.Enemy.Health, Is.EqualTo(80));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(15));
            Assert.That(duel.NextActGain, Is.EqualTo(4));
            Assert.That(duel.LastResolvedSlot, Is.EqualTo(-1));
            Assert.Throws<InvalidOperationException>(() => duel.BeginNextSlot());
            Assert.Throws<InvalidOperationException>(() => duel.CompleteCurrentSlot());
        }

        [Test]
        public void UnequalHitCounts_ResolveOnIndividualFramesAndAggregateAtCompletion()
        {
            var duel = TestDuel(new[] { Attack(100, 12, 3) }, new[] { Attack(101, 4) }, new[] { 1 });
            duel.TryQueueLane(0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();

            LegacyHitResult first = duel.ResolveNextHit();
            Assert.That(first.HitIndex, Is.Zero);
            Assert.That(first.PlayerAttacked, Is.True);
            Assert.That(first.EnemyAttacked, Is.True);
            Assert.That(first.PlayerResistanceDamage, Is.EqualTo(4));
            Assert.That(first.EnemyResistanceDamage, Is.EqualTo(4));
            Assert.That(first.IsFinalHit, Is.False);
            Assert.That(duel.Player.Resistance, Is.EqualTo(46));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(46));

            LegacyHitResult second = duel.ResolveNextHit();
            Assert.That(second.HitIndex, Is.EqualTo(1));
            Assert.That(second.PlayerAttacked, Is.True);
            Assert.That(second.EnemyAttacked, Is.False);
            Assert.That(second.PlayerResistanceDamage, Is.Zero);
            Assert.That(second.EnemyResistanceDamage, Is.EqualTo(4));

            LegacyHitResult third = duel.ResolveNextHit();
            Assert.That(third.IsFinalHit, Is.True);
            Assert.That(duel.IsCurrentSlotResolved, Is.True);
            Assert.That(slot.HitsResolved, Is.EqualTo(3));
            Assert.That(duel.IsTurnResolved, Is.False);
            Assert.Throws<InvalidOperationException>(() => duel.ResolveNextHit());

            LegacySlotResult complete = duel.CompleteCurrentSlot();
            Assert.That(complete.PlayerResistanceDamage, Is.EqualTo(4));
            Assert.That(complete.EnemyResistanceDamage, Is.EqualTo(12));
            Assert.That(duel.CurrentSlot, Is.Null);
            Assert.That(duel.IsTurnResolved, Is.True);
            Assert.That(duel.LastResolvedSlot, Is.Zero);
        }

        [Test]
        public void LethalEarlyHit_DoesNotStopLaterHitsOrDeclareVictoryBeforeSlotCompletes()
        {
            var duel = TestDuel(new[] { Attack(100, 4, 2) }, new[] { Attack(101, 3, 3) }, new[] { 1 }, 3, 0);
            duel.TryQueueLane(0);
            duel.Commit();
            duel.BeginNextSlot();

            LegacyHitResult first = duel.ResolveNextHit();
            Assert.That(duel.Enemy.Health, Is.Zero);
            Assert.That(duel.Player.Health, Is.EqualTo(1));
            Assert.That(first.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            Assert.That(duel.IsFinished, Is.False);

            LegacyHitResult second = duel.ResolveNextHit();
            Assert.That(second.EnemyAttacked, Is.True);
            Assert.That(duel.Player.Health, Is.Zero);
            Assert.That(second.IsFinalHit, Is.False);
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));

            LegacyHitResult third = duel.ResolveNextHit();
            Assert.That(third.PlayerAttacked, Is.False);
            Assert.That(third.EnemyAttacked, Is.True);
            Assert.That(third.IsFinalHit, Is.True);
            Assert.That(third.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(duel.IsFinished, Is.False);
            LegacySlotResult complete = duel.CompleteCurrentSlot();
            Assert.That(complete.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(duel.IsFinished, Is.True);
        }

        [Test]
        public void GuardAgainstGuard_ResolvesOneIdleFrameAndKeepsSkillEffect()
        {
            var followup = new LegacySkill(100, "followup", 1, 100, 100,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 2, "");
            var duel = TestDuel(new[] { LegacyInitialSkills.All[8], followup }, new[] { Guard(101, 1) }, new[] { 2 }, 1000);
            duel.TryQueueLane(2);
            duel.TryQueueLane(2);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();

            LegacyHitResult idle = duel.ResolveNextHit();

            Assert.That(slot.HitCount, Is.EqualTo(1));
            Assert.That(idle.PlayerAttacked, Is.False);
            Assert.That(idle.EnemyAttacked, Is.False);
            Assert.That(idle.PlayerHealthDamage, Is.Zero);
            Assert.That(idle.EnemyHealthDamage, Is.Zero);
            Assert.That(idle.IsFinalHit, Is.True);
            duel.CompleteCurrentSlot();
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(102));
        }

        [Test]
        public void HitByHitAndConvenienceResolution_ProduceSameSeededDamageAndState()
        {
            var paired = TestDuel(new[] { LegacyInitialSkills.All[0], LegacyInitialSkills.All[1] },
                new[] { LegacyInitialSkills.All[4] }, new[] { 2 });
            var immediate = TestDuel(new[] { LegacyInitialSkills.All[0], LegacyInitialSkills.All[1] },
                new[] { LegacyInitialSkills.All[4] }, new[] { 2 });
            paired.TryQueueLane(0);
            paired.TryQueueLane(0);
            immediate.TryQueueLane(0);
            immediate.TryQueueLane(0);
            paired.Commit();
            immediate.Commit();

            for (int index = 0; index < 2; index++)
            {
                paired.BeginNextSlot();
                while (!paired.IsCurrentSlotResolved) paired.ResolveNextHit();
                LegacySlotResult hitByHit = paired.CompleteCurrentSlot();
                LegacySlotResult oneCall = immediate.ResolveNextSlot();
                Assert.That(hitByHit.PlayerHealthDamage, Is.EqualTo(oneCall.PlayerHealthDamage));
                Assert.That(hitByHit.EnemyHealthDamage, Is.EqualTo(oneCall.EnemyHealthDamage));
                Assert.That(hitByHit.PlayerResistanceDamage, Is.EqualTo(oneCall.PlayerResistanceDamage));
                Assert.That(hitByHit.EnemyResistanceDamage, Is.EqualTo(oneCall.EnemyResistanceDamage));
            }
            Assert.That(paired.Player.Health, Is.EqualTo(immediate.Player.Health));
            Assert.That(paired.Enemy.Health, Is.EqualTo(immediate.Enemy.Health));
            Assert.That(paired.NextActGain, Is.EqualTo(immediate.NextActGain));
        }

        [Test]
        public void OverkillOnBrokenResistance_PreservesRawPopupDamageAndPreDoublePush()
        {
            var duel = TestDuel(new[] { Attack(100, 4) }, new[] { Attack(101, 4) }, new[] { 1 }, 3, 0);
            duel.TryQueueLane(0);
            duel.Commit();
            duel.BeginNextSlot();

            LegacyHitResult hit = duel.ResolveNextHit();

            Assert.That(hit.EnemyHealthDamage, Is.EqualTo(3));
            Assert.That(hit.EnemyDisplayedDamage, Is.EqualTo(8));
            Assert.That(hit.EnemyPushPower, Is.EqualTo(4));
            Assert.That(hit.PlayerHealthDamage, Is.EqualTo(3));
            Assert.That(hit.PlayerDisplayedDamage, Is.EqualTo(8));
            Assert.That(hit.PlayerPushPower, Is.EqualTo(4));
        }

        [Test]
        public void BreakingHit_PushesOnlyOverflowAndFollowingHitUsesBrokenDamage()
        {
            var duel = TestDuel(new[] { Attack(100, 8, 2) }, new[] { Attack(101, 1) }, new[] { 1 }, 100, 2);
            duel.TryQueueLane(0);
            duel.Commit();
            duel.BeginNextSlot();

            LegacyHitResult breaking = duel.ResolveNextHit();
            Assert.That(breaking.EnemyResistanceDamage, Is.EqualTo(2));
            Assert.That(breaking.EnemyHealthDamage, Is.EqualTo(2));
            Assert.That(breaking.EnemyDisplayedDamage, Is.EqualTo(2));
            Assert.That(breaking.EnemyPushPower, Is.EqualTo(2));

            LegacyHitResult broken = duel.ResolveNextHit();
            Assert.That(broken.EnemyResistanceDamage, Is.Zero);
            Assert.That(broken.EnemyHealthDamage, Is.EqualTo(8));
            Assert.That(broken.EnemyDisplayedDamage, Is.EqualTo(8));
            Assert.That(broken.EnemyPushPower, Is.EqualTo(4));
        }

        [Test]
        public void OrdinaryResistanceHit_DisplaysAndPushesResistanceDamage()
        {
            var duel = TestDuel(new[] { Attack(100, 4) }, new[] { Attack(101, 1) }, new[] { 1 });
            duel.TryQueueLane(0);
            duel.Commit();
            duel.BeginNextSlot();

            LegacyHitResult hit = duel.ResolveNextHit();

            Assert.That(hit.EnemyHealthDamage, Is.Zero);
            Assert.That(hit.EnemyResistanceDamage, Is.EqualTo(4));
            Assert.That(hit.EnemyDisplayedDamage, Is.EqualTo(4));
            Assert.That(hit.EnemyPushPower, Is.EqualTo(4));
        }

        private static LegacyQueuedDuel TestDuel(LegacySkill[] playerSkills, LegacySkill[] enemySkills,
            int[] counts, int health = 100, int resistance = 50)
            => new LegacyQueuedDuel(health, resistance, health, resistance, playerSkills, enemySkills, counts);

        private static LegacySkill Attack(int id, int power, int hits = 1)
            => new LegacySkill(id, "test attack", 1, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "test guard", 1, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");

        private static int[] Ids(System.Collections.Generic.IReadOnlyList<LegacySkill> skills)
        {
            var ids = new int[skills.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = skills[i].Id;
            return ids;
        }

        private static void ResolveTurn(LegacyQueuedDuel duel)
        {
            duel.Commit();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
        }
    }
}
