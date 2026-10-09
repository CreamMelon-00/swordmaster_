using System;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;

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
            Assert.That(duel.TryQueueLane(2), Is.True);
            Assert.That(duel.TryQueueLane(1), Is.False, "The W opener costs 2 ACT, more than the remaining 1.");

            Assert.That(duel.Act, Is.EqualTo(1));
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Enemy.Health, Is.EqualTo(80));
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(15));
            Assert.That(duel.RoundNumber, Is.EqualTo(1));
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            CollectionAssert.AreEqual(new[] { 1, 5 }, Ids(duel.PlayerQueue));
            CollectionAssert.AreEqual(new[] { 2, 7, 1 }, Ids(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, Ids(duel.GetLane(1)));
            CollectionAssert.AreEqual(new[] { 6, 9, 5 }, Ids(duel.GetLane(2)));
        }

        [Test]
        public void QueueCancel_RemovesEarlierTechniquesImmediately_WithoutRefundingActOrBreaths()
        {
            LegacySkill costly = Attack(900, 10);
            LegacySkill posture = LegacySkillDefinitions.Skill(45);
            var duel = new LegacyQueuedDuel(100, 50, 100, 50,
                new[] { costly, posture }, Array.Empty<LegacySkill>(), new[] { 0 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.TryQueueLane(2), Is.True);

            CollectionAssert.AreEqual(new[] { -1, 45 }, Ids(duel.PlayerQueue));
            Assert.That(duel.Act, Is.Zero, "The two removed ACT-1 skills are not refunded.");
            Assert.That(duel.BreathsQueuedThisTurn, Is.EqualTo(1), "Breathing is not a technique and stays queued.");
            Assert.That(duel.QueuePowerSourceSlotIndex, Is.EqualTo(1));
            Assert.That(duel.QueuePowerRemovedSkillCount, Is.EqualTo(2));
            Assert.That(duel.QueuePowerTargetSlotIndex, Is.EqualTo(-1), "No following technique is queued yet.");
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().PlayerSkill.IsWait, Is.True);
            Assert.That(duel.ResolveNextSlot().PlayerSkill.Id, Is.EqualTo(45));
            duel.BeginNextTurn();
            Assert.That(duel.QueuePowerSourceSlotIndex, Is.EqualTo(-1), "The boost expires with its turn.");
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(5, 5)]
        [TestCase(6, 5)]
        public void QueueCancel_BoostsOnlyTheNextTechnique_ByRemovedCountUpToFive(int removedCount, int multiplier)
        {
            LegacySkill disposable = new LegacySkill(900, "disposable", 0, 1, 1,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");
            LegacySkill target = new LegacySkill(901, "target", 0, 10, 10,
                LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 1, "");
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50,
                new[] { disposable, target, LegacySkillDefinitions.Skill(45) },
                Array.Empty<LegacySkill>(), new[] { 0 }, meshPercent: 0);
            for (int i = 0; i < removedCount; i++)
                Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueLane(2), Is.True);
            Assert.That(duel.TryQueueLane(1) && duel.TryQueueLane(1), Is.True);

            CollectionAssert.AreEqual(new[] { 45, 901, 901 }, Ids(duel.PlayerQueue));
            Assert.That(duel.QueuePowerRemovedSkillCount, Is.EqualTo(removedCount));
            Assert.That(duel.QueuePowerTargetSlotIndex, Is.EqualTo(1));
            Assert.That(duel.QueuePowerMultiplierForSlot(1), Is.EqualTo(multiplier));
            Assert.That(duel.QueuePowerMultiplierForSlot(2), Is.EqualTo(1));
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(10 * multiplier));
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(10),
                "The multiplier is consumed by the first following technique.");
        }

        [Test]
        public void QueueCancel_BoostsDefencePowerAndSkipsBreathingToFindTheNextTechnique()
        {
            LegacySkill disposable = new LegacySkill(900, "disposable", 0, 1, 1,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");
            LegacySkill guard = new LegacySkill(901, "guard", 0, 10, 10,
                LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 1, "");
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50,
                new[] { disposable, guard, LegacySkillDefinitions.Skill(45) },
                new[] { LegacyCommonActions.Breathe, LegacyCommonActions.Breathe, Attack(902, 30) },
                new[] { 3 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueLane(2), Is.True);
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(duel.QueuePowerTargetSlotIndex, Is.EqualTo(2));
            Assert.That(duel.QueuePowerMultiplierForSlot(2), Is.EqualTo(2));

            duel.Commit();
            duel.ResolveNextSlot();
            duel.ResolveNextSlot();
            duel.BeginNextSlot();
            Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.EqualTo(10));
        }

        [Test]
        public void QueueReturn_RefundsTheRemovedTechniquesButNotTheNewSkill()
        {
            var rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            int modeColumn = Array.IndexOf(rows[0], "앞 기술 처리");
            int capColumn = Array.IndexOf(rows[0], "후속 위력 배율 상한");
            Assert.That(modeColumn, Is.GreaterThan(0));
            Assert.That(capColumn, Is.GreaterThan(0));
            string[] postureRow = rows.Single(row => row[0] == "45");
            postureRow[modeColumn] = "반환";
            postureRow[capColumn] = "";
            string modifiedSheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => modifiedSheet);
                LegacySkill replacement = LegacySkillDefinitions.Skill(45);
                var duel = new LegacyQueuedDuel(100, 50, 100, 50,
                    new[] { Attack(900, 10), replacement }, Array.Empty<LegacySkill>(), new[] { 0 });
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                Assert.That(duel.Act, Is.EqualTo(1));
                Assert.That(duel.TryQueueLane(2), Is.True);
                CollectionAssert.AreEqual(new[] { 45 }, Ids(duel.PlayerQueue));
                Assert.That(duel.Act, Is.EqualTo(2), "Two prior ACT are returned; the new skill still costs one.");
                Assert.That(duel.QueuePowerSourceSlotIndex, Is.EqualTo(-1),
                    "The return keyword alone does not grant a power multiplier.");
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        [Test]
        public void Cycle_FirstUseKeepsBaseStats_ThenRaisesTheNextTurnsCostAndPower()
        {
            LegacySkill sharpening = LegacySkillDefinitions.Skill(46);
            var duel = new LegacyQueuedDuel(100, 50, 10000, 50,
                new[] { sharpening }, Array.Empty<LegacySkill>(), new[] { 0 }, meshPercent: 0);

            Assert.That(duel.CycleUses(46), Is.Zero);
            Assert.That(duel.EffectiveCost(sharpening), Is.EqualTo(1));
            duel.EffectivePowerRange(sharpening, out int min, out int max);
            Assert.That((min, max), Is.EqualTo((5, 10)));
            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(duel.Act, Is.EqualTo(2));
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.InRange(5, 10));
            Assert.That(duel.CycleUses(46), Is.EqualTo(1));

            duel.BeginNextTurn();
            Assert.That(duel.EffectiveCost(sharpening), Is.EqualTo(2));
            duel.EffectivePowerRange(sharpening, out min, out max);
            Assert.That((min, max), Is.EqualTo((10, 15)));
            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(duel.Act, Is.EqualTo(3));
        }

        [Test]
        public void Cycle_ReservationsInOneTurnKeepTheirPrice_ButEachActualUseGainsPower()
        {
            // One stable instance is deliberately reserved twice. Its sheet ID supplies the cycle effect;
            // fixed base power makes the per-use roll exact without touching the owned skill's identity.
            var sharpening = new LegacySkill(46, "연마", 1, 5, 5,
                LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 1, "");
            var duel = new LegacyQueuedDuel(100, 50, 10000, 50,
                new[] { sharpening }, Array.Empty<LegacySkill>(), new[] { 0 }, meshPercent: 0);

            Assert.That(duel.TryQueueLane(1) && duel.TryQueueLane(1), Is.True);
            Assert.That(duel.Act, Is.EqualTo(1), "Both reservations cost one ACT before either skill is used.");
            CollectionAssert.AreEqual(new[] { 46, 46 }, Ids(duel.PlayerQueue));
            Assert.That(duel.CycleUses(46), Is.Zero, "Reservation alone is not a use.");
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(5));
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(10));
            Assert.That(duel.CycleUses(46), Is.EqualTo(2));
        }

        [Test]
        public void Cycle_CancelledReservationNeverCountsAsAUse()
        {
            LegacySkill sharpening = LegacySkillDefinitions.Skill(46);
            var duel = new LegacyQueuedDuel(100, 50, 10000, 0,
                new[] { sharpening, LegacySkillDefinitions.Skill(45) },
                Array.Empty<LegacySkill>(), new[] { 0 }, meshPercent: 0);

            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(duel.TryQueueLane(2), Is.True);
            CollectionAssert.AreEqual(new[] { 45 }, Ids(duel.PlayerQueue));
            Assert.That(duel.CycleUses(46), Is.Zero);
            duel.Commit();
            duel.ResolveNextSlot();
            duel.BeginNextTurn();
            Assert.That(duel.CycleUses(46), Is.Zero);
            Assert.That(duel.EffectiveCost(sharpening), Is.EqualTo(1));
            duel.EffectivePowerRange(sharpening, out int min, out int max);
            Assert.That((min, max), Is.EqualTo((5, 10)));
        }

        [Test]
        public void Cycle_ResetStartsANewBattleAtZeroUses()
        {
            LegacySkill sharpening = LegacySkillDefinitions.Skill(46);
            var duel = new LegacyQueuedDuel(100, 50, 10000, 50,
                new[] { sharpening }, Array.Empty<LegacySkill>(), new[] { 0 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(1), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.CycleUses(46), Is.EqualTo(1));

            duel.Reset();
            Assert.That(duel.RoundNumber, Is.EqualTo(1));
            Assert.That(duel.CycleUses(46), Is.Zero);
            Assert.That(duel.EffectiveCost(sharpening), Is.EqualTo(1));
            duel.EffectivePowerRange(sharpening, out int min, out int max);
            Assert.That((min, max), Is.EqualTo((5, 10)));
        }

        [Test]
        public void Cycle_NineUsesCapBothCostAndPowerAcrossFurtherUses()
        {
            LegacySkill sharpening = LegacySkillDefinitions.Skill(46);
            var duel = new LegacyQueuedDuel(100, 50, 100000, 0,
                new[] { sharpening }, Array.Empty<LegacySkill>(), new[] { 0 },
                playerActGainBonus: 7, meshPercent: 0);

            for (int use = 0; use < 11; use++)
            {
                int cycle = Math.Min(use, 9);
                Assert.That(duel.CycleUses(46), Is.EqualTo(cycle));
                Assert.That(duel.EffectiveCost(sharpening), Is.EqualTo(1 + cycle));
                duel.EffectivePowerRange(sharpening, out int min, out int max);
                Assert.That((min, max), Is.EqualTo((5 + 5 * cycle, 10 + 5 * cycle)));
                Assert.That(duel.TryQueueLane(1), Is.True);
                duel.Commit();
                duel.ResolveNextSlot();
                Assert.That(duel.CycleUses(46), Is.EqualTo(Math.Min(use + 1, 9)));
                if (use < 10) duel.BeginNextTurn();
            }
        }

        [Test]
        public void QueueReturn_RefundsTheActualCycleReservationCost()
        {
            var rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            int modeColumn = Array.IndexOf(rows[0], "앞 기술 처리");
            int capColumn = Array.IndexOf(rows[0], "후속 위력 배율 상한");
            string[] postureRow = rows.Single(row => row[0] == "45");
            postureRow[modeColumn] = "반환";
            postureRow[capColumn] = "";
            string modifiedSheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => modifiedSheet);
                LegacySkill sharpening = LegacySkillDefinitions.Skill(46);
                var duel = new LegacyQueuedDuel(100, 50, 10000, 0,
                    new[] { sharpening, LegacySkillDefinitions.Skill(45) },
                    Array.Empty<LegacySkill>(), new[] { 0 }, meshPercent: 0);
                Assert.That(duel.TryQueueLane(1), Is.True);
                duel.Commit();
                duel.ResolveNextSlot();
                duel.BeginNextTurn();
                Assert.That(duel.CycleUses(46), Is.EqualTo(1));
                Assert.That(duel.Act, Is.EqualTo(5));

                Assert.That(duel.TryQueueLane(1), Is.True);
                Assert.That(duel.Act, Is.EqualTo(3), "The second reservation costs two ACT.");
                Assert.That(duel.TryQueueLane(2), Is.True);
                CollectionAssert.AreEqual(new[] { 45 }, Ids(duel.PlayerQueue));
                Assert.That(duel.Act, Is.EqualTo(4), "Return refunds the spent two ACT, then charges one ACT.");
                Assert.That(duel.CycleUses(46), Is.EqualTo(1), "The removed reservation was not used.");
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        [Test]
        public void UnaffordableSkill_DoesNotRotateOrEnterQueue()
        {
            var duel = new LegacyQueuedDuel();
            duel.TryQueueLane(0);
            duel.TryQueueLane(2);

            Assert.That(duel.TryQueueLane(2), Is.False);
            Assert.That(duel.Act, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 1, 5 }, Ids(duel.PlayerQueue));
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
            // 베기 (Q) meshes with the W skill after it (맞물림, +20%): floor(4 × 1.2) = 4 or floor(5 × 1.2) = 6.
            Assert.That(first.EnemyResistanceDamage, Is.EqualTo(4).Or.EqualTo(6));
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
        public void DeepThrust_SpendsTwoActForOneStrongHitWithoutBuffingLaterSkills()
        {
            LegacySkill stab = LegacySkillDefinitions.Skill(3);
            var strike = new LegacySkill(100, "test", 0, 20, 20, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 1, "");
            var duel = TestDuel(new[] { stab, strike, strike, strike, strike }, new[] { Attack(101, 1) }, new[] { 5 }, 100, 1000);
            for (int i = 0; i < 5; i++) Assert.That(duel.TryQueueLane(1), Is.True);
            duel.Commit();

            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.InRange(11, 14));
            for (int i = 0; i < 4; i++) Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(20));
        }

        [Test]
        public void GuardAgainstHit_AddsTwoNextTurnAct()
        {
            var duel = TestDuel(new[] { LegacySkillDefinitions.Skill(7) },
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
        public void LethalFirstHit_StopsBothSkillsBeforeTheirRemainingHits()
        {
            var duel = TestDuel(new[] { Attack(100, 4, 2) }, new[] { Attack(101, 3, 3) }, new[] { 1 }, 3, 0);
            duel.TryQueueLane(0);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();

            LegacyHitResult first = duel.ResolveNextHit();
            Assert.That(first.PlayerAttacked, Is.True);
            Assert.That(first.EnemyAttacked, Is.True, "Both attacks in the lethal frame still resolve.");
            Assert.That(duel.Enemy.Health, Is.Zero);
            Assert.That(duel.Player.Health, Is.EqualTo(1));
            Assert.That(slot.HitsResolved, Is.EqualTo(1));
            Assert.That(first.IsFinalHit, Is.True);
            Assert.That(first.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(duel.IsCurrentSlotResolved, Is.True);
            Assert.Throws<InvalidOperationException>(() => duel.ResolveNextHit());

            LegacySlotResult complete = duel.CompleteCurrentSlot();
            Assert.That(complete.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(complete.PlayerHealthDamage, Is.EqualTo(2));
            Assert.That(complete.EnemyHealthDamage, Is.EqualTo(3));
            Assert.That(duel.IsFinished, Is.True);
            Assert.That(duel.Player.Health, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => duel.ResolveNextSlot());
        }

        [Test]
        public void SimultaneousLethalFirstHit_DealsBothImpactsBeforeStoppingMultiHitSkills()
        {
            var duel = TestDuel(new[] { Attack(100, 6, 3) }, new[] { Attack(101, 6, 3) },
                new[] { 1 }, 3, 0);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();

            LegacyHitResult first = duel.ResolveNextHit();

            Assert.That(first.PlayerAttacked, Is.True);
            Assert.That(first.EnemyAttacked, Is.True);
            Assert.That(first.PlayerHealthDamage, Is.EqualTo(3));
            Assert.That(first.EnemyHealthDamage, Is.EqualTo(3));
            Assert.That(duel.Player.Health, Is.Zero);
            Assert.That(duel.Enemy.Health, Is.Zero);
            Assert.That(slot.HitsResolved, Is.EqualTo(1));
            Assert.That(first.IsFinalHit, Is.True);
            Assert.That(first.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory),
                "The existing simultaneous defeat priority applies within the lethal hit.");
            Assert.Throws<InvalidOperationException>(() => duel.ResolveNextHit());
            Assert.That(duel.CompleteCurrentSlot().Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
        }

        [Test]
        public void Scout_GrantsOneExtraActOnTheNextTurn()
        {
            var duel = new LegacyQueuedDuel(100, 50, 100, 50,
                new[] { LegacySkillDefinitions.Skill(43) }, new[] { Guard(901, 0) }, new[] { 1 }, 7);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.Act, Is.EqualTo(2), "The skill still spends one ACT during planning.");
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.ActGainGranted, Is.EqualTo(1));
            Assert.That(duel.NextActGain, Is.EqualTo(4));
            duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(6));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
        }

        [Test]
        public void Barrage_AddsDamageOnlyToHitsAfterTheTargetHasBroken()
        {
            LegacySkill barrage = LegacySkillDefinitions.Skill(44);
            var ordinary = new LegacySkill(944, "ordinary four hits", barrage.Cost,
                barrage.MinPower, barrage.MaxPower, barrage.Kind, barrage.Property,
                barrage.AttackCount, barrage.LaneIndex, "");
            LegacyQueuedDuel enhanced = BarrageDuel(barrage);
            LegacyQueuedDuel baseline = BarrageDuel(ordinary);
            Assert.That(enhanced.TryQueueLane(0), Is.True);
            Assert.That(baseline.TryQueueLane(0), Is.True);
            enhanced.Commit();
            baseline.Commit();
            enhanced.BeginNextSlot();
            baseline.BeginNextSlot();

            LegacyHitResult breaking = enhanced.ResolveNextHit();
            LegacyHitResult plainBreaking = baseline.ResolveNextHit();
            Assert.That(enhanced.Enemy.IsResistanceBroken, Is.True);
            Assert.That(breaking.EnemyHealthDamage, Is.EqualTo(plainBreaking.EnemyHealthDamage),
                "The resistance-breaking hit does not count as hitting an already broken target.");

            LegacyHitResult followup = enhanced.ResolveNextHit();
            LegacyHitResult plainFollowup = baseline.ResolveNextHit();
            int expected = (int)Math.Round(plainFollowup.EnemyHealthDamage * 1.25,
                MidpointRounding.ToEven);
            Assert.That(followup.EnemyHealthDamage, Is.EqualTo(expected));
            Assert.That(followup.EnemyHealthDamage, Is.GreaterThan(plainFollowup.EnemyHealthDamage));
            Assert.That(followup.EnemyPushPower, Is.EqualTo(plainFollowup.EnemyPushPower),
                "The bonus changes health damage, not knockback power.");

            for (int hit = 2; hit < 4; hit++)
            {
                enhanced.ResolveNextHit();
                baseline.ResolveNextHit();
            }
            Assert.That(enhanced.CurrentSlot.HitsResolved, Is.EqualTo(4));
            Assert.That(enhanced.CurrentSlot.IsResolved, Is.True);
        }

        [Test]
        public void GuardAgainstGuard_ResolvesOneIdleFrameAndKeepsSkillEffect()
        {
            var followup = new LegacySkill(100, "followup", 1, 100, 100,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 2, "");
            var duel = TestDuel(new[] { LegacySkillDefinitions.Skill(9), followup }, new[] { Guard(101, 1) }, new[] { 2 }, 1000);
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
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(119),
                "The following 100-power attack gains 20%, then the enemy's 1-point guard is subtracted.");
        }

        [Test]
        public void HitByHitAndConvenienceResolution_ProduceSameSeededDamageAndState()
        {
            var paired = TestDuel(new[] { LegacySkillDefinitions.Skill(1), LegacySkillDefinitions.Skill(2) },
                new[] { LegacySkillDefinitions.Skill(5) }, new[] { 2 });
            var immediate = TestDuel(new[] { LegacySkillDefinitions.Skill(1), LegacySkillDefinitions.Skill(2) },
                new[] { LegacySkillDefinitions.Skill(5) }, new[] { 2 });
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

        [Test]
        public void PassiveEnemy_QueuesNothingAndEndsAfterFiveTurnsWithoutAHit()
        {
            var duel = new LegacyQueuedDuel(100, 50, 50, 0, new[] { Attack(100, 1) },
                System.Array.Empty<LegacySkill>(), new[] { 0 }, roundLimit: 5);

            for (int round = 1; round <= 5; round++)
            {
                Assert.That(duel.RoundNumber, Is.EqualTo(round));
                Assert.That(duel.EnemyQueue, Is.Empty);
                duel.Commit();
                if (round < 5)
                {
                    Assert.That(duel.IsTurnResolved, Is.True);
                    Assert.That(duel.IsFinished, Is.False);
                    duel.BeginNextTurn();
                }
            }

            Assert.That(duel.IsFinished, Is.True);
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.Draw));
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Enemy.Health, Is.EqualTo(50));
        }

        [Test]
        public void PassiveEnemy_KillOnTheLastTurnWinsBeforeTheTurnLimit()
        {
            var duel = new LegacyQueuedDuel(100, 50, 1, 0, new[] { Attack(100, 1) },
                System.Array.Empty<LegacySkill>(), new[] { 0 }, roundLimit: 5);
            for (int round = 1; round < 5; round++)
            {
                duel.Commit();
                duel.BeginNextTurn();
            }

            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(duel.IsFinished, Is.True);
            Assert.That(duel.RoundNumber, Is.EqualTo(5));
        }

        [Test]
        public void EnemyHealthFloor_KeepsTheEnemyStanding_WhateverHitsIt()
        {
            var duel = new LegacyQueuedDuel(100, 50, 50, 10, new[] { Attack(900, 999) },
                System.Array.Empty<LegacySkill>(), new[] { 0 }, enemyHealthFloor: 1);
            Assert.That(duel.EnemyHealthFloor, Is.EqualTo(1));
            Assert.That(duel.Enemy.HealthFloor, Is.EqualTo(1));
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(duel.Enemy.Health, Is.EqualTo(1));
            Assert.That(hit.EnemyHealthDamage, Is.EqualTo(49), "The health actually lost.");
            Assert.That(hit.EnemyDisplayedDamage, Is.EqualTo(999), "The number shown stays the blow's, as at zero.");
            Assert.That(hit.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            Assert.That(duel.CompleteCurrentSlot().Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            Assert.That(duel.Enemy.IsDefeated || duel.IsFinished, Is.False);

            // Pressure's flat bonus respects the floor too.
            duel.BeginNextTurn();
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out bool pressed) && pressed, Is.True);
            Assert.That(duel.ResolveNextSlot().Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            Assert.That(duel.Enemy.Health, Is.EqualTo(1));

            Assert.That(new LegacyQueuedDuel().EnemyHealthFloor, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyQueuedDuel(100, 50, 50, 10, new[] { Attack(900, 1) },
                System.Array.Empty<LegacySkill>(), new[] { 0 }, enemyHealthFloor: 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyQueuedDuel(100, 50, 50, 10, new[] { Attack(900, 1) },
                System.Array.Empty<LegacySkill>(), new[] { 0 }, enemyHealthFloor: -1));
        }

        [Test]
        public void EnemyHealthThreshold_ReportsTheExactHitThatFirstReachesIt_OncePerAttempt()
        {
            // 40 health, threshold 50%: 20 or less. Three 10-power hits leave 30, 20, 10.
            var duel = new LegacyQueuedDuel(100, 50, 40, 10, new[] { Attack(900, 10) },
                System.Array.Empty<LegacySkill>(), new[] { 0 }, enemyHealthThresholdPercent: 50);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                for (int i = 0; i < 3; i++) Assert.That(duel.TryQueueLane(0), Is.True);
                duel.Commit();
                var reached = new System.Collections.Generic.List<bool>();
                while (!duel.IsTurnResolved)
                {
                    duel.BeginNextSlot();
                    while (!duel.IsCurrentSlotResolved) reached.Add(duel.ResolveNextHit().EnemyReachedHealthThreshold);
                    duel.CompleteCurrentSlot();
                }
                Assert.That(reached, Is.EqualTo(new[] { false, true, false }), "Exactly half counts, and only the first time.");
                Assert.That(duel.EnemyHealthThresholdReached, Is.True);
                duel.Reset();
                Assert.That(duel.EnemyHealthThresholdReached, Is.False, "A retry starts fresh.");
            }
            Assert.That(new LegacyQueuedDuel().EnemyHealthThresholdPercent, Is.Zero);
        }

        [Test]
        public void EnemyHealthThreshold_IsNotReportedOnAHitThatEndsTheDuel()
        {
            // Both fighters are unguarded (no resistance): the clash takes the enemy from 40 to 10 and the player from 20 to 0.
            var duel = new LegacyQueuedDuel(20, 0, 40, 0, new[] { Attack(900, 15) },
                new[] { Attack(901, 10) }, new[] { 1 }, enemyHealthThresholdPercent: 50);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(duel.Enemy.Health, Is.EqualTo(10));
            Assert.That(hit.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(hit.EnemyReachedHealthThreshold || duel.EnemyHealthThresholdReached, Is.False, "Nothing is left to interrupt.");

            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyQueuedDuel(100, 50, 50, 10, new[] { Attack(900, 1) },
                System.Array.Empty<LegacySkill>(), new[] { 0 }, enemyHealthThresholdPercent: 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyQueuedDuel(100, 50, 50, 10, new[] { Attack(900, 1) },
                System.Array.Empty<LegacySkill>(), new[] { 0 }, enemyHealthThresholdPercent: -1));
        }

        [Test]
        public void InterruptResolvingTurn_SettlesOnlyLandedHits_AndStartsTheReplacementScriptNextTurn()
        {
            LegacySkill flurry = Attack(900, 30, 3);
            LegacySkill empowered = Attack(901, 2);
            var duel = new LegacyQueuedDuel(100, 0, 40, 1, new[] { flurry },
                new[] { LegacyCommonActions.Breathe }, new[] { 1 }, enemyHealthFloor: 1,
                enemyHealthThresholdPercent: 50);
            for (int i = 0; i < 3; i++) Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(duel.ResolveNextHit().EnemyReachedHealthThreshold, Is.False);
            Assert.That(duel.ResolveNextHit().EnemyReachedHealthThreshold, Is.True);
            Assert.That(slot.HitsResolved, Is.EqualTo(2));
            Assert.That(slot.HitCount, Is.EqualTo(3));
            duel.ReplaceEnemyScript(new EnemyScript(new[] { new[] { empowered } }));

            LegacySlotResult settled = duel.InterruptResolvingTurn();

            Assert.That(settled.PlayerSkill, Is.SameAs(flurry));
            Assert.That(settled.EnemyHealthDamage, Is.EqualTo(20));
            Assert.That(slot.HitCount, Is.EqualTo(2), "The third strike never occurs.");
            Assert.That(duel.Enemy.Health, Is.EqualTo(20));
            Assert.That(duel.CurrentSlot, Is.Null);
            Assert.That(duel.IsTurnResolved, Is.True, "Remaining committed slots are discarded.");
            Assert.That(duel.IsFinished, Is.False);
            Assert.Throws<InvalidOperationException>(() => duel.ResolveNextSlot());

            duel.BeginNextTurn();
            Assert.That(duel.RoundNumber, Is.EqualTo(2));
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(duel.Enemy.Health, Is.EqualTo(20));
            Assert.That(duel.PlayerQueue, Is.Empty);
            CollectionAssert.AreEqual(new[] { empowered.Id }, Ids(duel.EnemyQueue));
        }

        [Test]
        public void InterruptResolvingTurn_AfterSettledSlot_DoesNotSettleItTwice()
        {
            var duel = new LegacyQueuedDuel(100, 0, 40, 1, new[] { Attack(900, 30) },
                new[] { LegacyCommonActions.Breathe }, new[] { 1 }, enemyHealthFloor: 1,
                enemyHealthThresholdPercent: 50);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            LegacySlotResult first = duel.ResolveNextSlot();
            Assert.That(first.EnemyHealthDamage, Is.EqualTo(30));
            Assert.That(duel.LastResolvedSlot, Is.Zero);

            Assert.That(duel.InterruptResolvingTurn(), Is.Null);
            Assert.That(duel.Enemy.Health, Is.EqualTo(10));
            Assert.That(duel.IsTurnResolved, Is.True);
            duel.BeginNextTurn();
            Assert.That(duel.Enemy.Health, Is.EqualTo(10));
        }

        [Test]
        public void InterruptResolvingTurn_RefusesUnresolvedOrDecisiveHits()
        {
            var duel = new LegacyQueuedDuel(100, 0, 20, 0, new[] { Attack(900, 30) },
                new[] { LegacyCommonActions.Breathe }, new[] { 1 });
            Assert.Throws<InvalidOperationException>(() => duel.InterruptResolvingTurn());
            duel.TryQueueLane(0);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.Throws<InvalidOperationException>(() => duel.InterruptResolvingTurn());
            Assert.That(duel.ResolveNextHit().Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.Throws<InvalidOperationException>(() => duel.InterruptResolvingTurn());
            Assert.That(duel.Enemy.Health, Is.Zero);
            Assert.That(duel.CurrentSlot.HitsResolved, Is.EqualTo(1));
        }

        [Test]
        public void ReplaceEnemyScript_StartsAtTheNextPlanningTurn_AndResetBringsBackTheOriginalEnemy()
        {
            LegacySkill a = Attack(900, 1), b = Attack(901, 1), c = Attack(902, 1);
            var script = new EnemyScript(new[] { new[] { b }, new[] { c } });
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Guard(903, 1) }, new[] { a, a }, new[] { 2 });

            // During planning: the queue already shown stays for this turn.
            duel.ReplaceEnemyScript(script);
            CollectionAssert.AreEqual(new[] { 900, 900 }, Ids(duel.EnemyQueue));
            ResolveTurn(duel);
            duel.BeginNextTurn();
            CollectionAssert.AreEqual(new[] { 901 }, Ids(duel.EnemyQueue), "The script's own first turn, whatever the round.");
            ResolveTurn(duel);
            duel.BeginNextTurn();
            CollectionAssert.AreEqual(new[] { 902 }, Ids(duel.EnemyQueue));
            ResolveTurn(duel);
            duel.BeginNextTurn();
            CollectionAssert.AreEqual(new[] { 901 }, Ids(duel.EnemyQueue), "It loops.");

            // Mid-turn: the committed queue plays out, then the new enemy queues.
            duel.Reset();
            CollectionAssert.AreEqual(new[] { 900, 900 }, Ids(duel.EnemyQueue), "Reset brings back the original enemy.");
            duel.Commit();
            duel.ResolveNextSlot();
            duel.ReplaceEnemyScript(script);
            Assert.That(duel.ResolveNextSlot().EnemySkill.Id, Is.EqualTo(900));
            duel.BeginNextTurn();
            CollectionAssert.AreEqual(new[] { 901 }, Ids(duel.EnemyQueue));

            // A replacement still waiting when the attempt restarts is dropped.
            duel.ReplaceEnemyScript(new EnemyScript(new[] { new[] { c } }));
            duel.Reset();
            ResolveTurn(duel);
            duel.BeginNextTurn();
            CollectionAssert.AreEqual(new[] { 900, 900 }, Ids(duel.EnemyQueue));
            Assert.Throws<ArgumentNullException>(() => duel.ReplaceEnemyScript(null));
        }

        private static LegacyQueuedDuel TestDuel(LegacySkill[] playerSkills, LegacySkill[] enemySkills,
            int[] counts, int health = 100, int resistance = 50)
            => new LegacyQueuedDuel(health, resistance, health, resistance, playerSkills, enemySkills, counts);

        private static LegacyQueuedDuel BarrageDuel(LegacySkill skill)
            => new LegacyQueuedDuel(100, 50, 100, 1,
                new[] { skill }, new[] { Attack(902, 1) }, new[] { 1 }, 17);

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
