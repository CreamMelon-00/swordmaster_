using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class CampaignRunTests
    {
        [Test]
        public void FreshRun_PreservesInitialBattleAndNineSkillLanes()
        {
            var run = NewBattleRun();
            LegacyQueuedDuel duel = run.CreateDuel(11);

            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Battle));
            Assert.That(run.StageNumber, Is.EqualTo(1));
            Assert.That(run.StageCount, Is.EqualTo(8));
            Assert.That(run.Currency, Is.Zero);
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
            Assert.That(duel.Enemy.Health, Is.EqualTo(80));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(15));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, SkillIds(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, SkillIds(duel.GetLane(1)));
            CollectionAssert.AreEqual(new[] { 5, 6, 9 }, SkillIds(duel.GetLane(2)));
            CollectionAssert.AreEqual(new[] { 1, 2 }, SkillIds(duel.EnemyQueue));
        }

        [Test]
        public void Win_PaysOnceAndRequiresMaintenanceBeforeNextBattle()
        {
            var run = NewBattleRun();
            Assert.That(run.TryStartNextStage(), Is.False);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.InProgress), Is.False);
            Assert.That(run.TryCompleteBattle((DuelMatchOutcome)999), Is.False);
            Assert.That(run.Currency, Is.Zero);

            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Maintenance));
            Assert.That(run.Currency, Is.EqualTo(60));
            Assert.That(run.LastReward, Is.EqualTo(60));
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.False);
            Assert.That(run.Currency, Is.EqualTo(60));
            Assert.That(run.RetryCurrentStage(), Is.False);

            Assert.That(run.TryStartNextStage(), Is.True);
            Assert.That(run.StageNumber, Is.EqualTo(2));
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Battle));
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.TryStartNextStage(), Is.False);
        }

        [TestCase(DuelMatchOutcome.EnemyVictory)]
        [TestCase(DuelMatchOutcome.Draw)]
        public void LossOrDraw_DoesNotPayAndRetryKeepsStageAndPurchases(DuelMatchOutcome outcome)
        {
            var run = NewBattleRun();
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            run.TryUpgradeSkill(1);
            run.TryStartNextStage();

            Assert.That(run.TryCompleteBattle(outcome), Is.True);
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Failed));
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.Currency, Is.EqualTo(30));
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.TryStartNextStage(), Is.False);
            Assert.That(run.TryAcquireSkill(run.Offers[0].SkillId), Is.False);
            Assert.That(run.TryUpgradeSkill(1), Is.False);
            Assert.That(run.RetryCurrentStage(), Is.True);
            Assert.That(run.RetryCurrentStage(), Is.False);
            Assert.That(run.StageNumber, Is.EqualTo(2));
            Assert.That(run.Currency, Is.EqualTo(30));
            Assert.That(Owned(run, 1).Level, Is.EqualTo(1));

            LegacyQueuedDuel retry = run.CreateDuel();
            Assert.That(retry.Player.Health, Is.EqualTo(100));
            Assert.That(retry.Player.Resistance, Is.EqualTo(50));
            Assert.That(retry.Enemy.Health, Is.EqualTo(95));
            Assert.That(retry.GetLane(0)[0].MinPower, Is.EqualTo(6));
        }

        [Test]
        public void TransactionsDuringBattle_RejectWithoutChangingRun()
        {
            var run = NewBattleRun();
            int offerId = run.Offers[0].SkillId;
            Assert.That(run.TryAcquireSkill(offerId), Is.False);
            Assert.That(run.TryUpgradeSkill(1), Is.False);
            Assert.That(run.Currency, Is.Zero);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
            Assert.That(Owned(run, 1).Level, Is.Zero);

            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            run.TryStartNextStage();
            Assert.That(run.TryAcquireSkill(offerId), Is.False);
            Assert.That(run.TryUpgradeSkill(1), Is.False);
            Assert.That(run.Currency, Is.EqualTo(60));
        }

        [Test]
        public void Acquisition_PaysCatalogPriceOnceAndRequiresExplicitEquip()
        {
            var run = NewBattleRun();
            ReachMaintenance(run, 3);
            CampaignSkillOffer offer = run.Offers[0];
            int initialCurrency = run.Currency;
            int initialOfferCount = run.Offers.Count;
            Assert.That(offer.Price, Is.EqualTo(45 + 10 * (offer.Skill.Cost - 1)));
            Assert.That(run.TryAcquireSkill(offer.SkillId), Is.True);
            Assert.That(run.Currency, Is.EqualTo(initialCurrency - offer.Price));
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(10));
            Assert.That(run.Offers.Count, Is.EqualTo(initialOfferCount - 1));
            Assert.That(Owned(run, offer.SkillId).Level, Is.Zero);
            Assert.That(run.TryAcquireSkill(offer.SkillId), Is.False);
            Assert.That(run.Currency, Is.EqualTo(initialCurrency - offer.Price));
            Assert.That(run.IsSkillEquipped(offer.SkillId), Is.False);
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.TryEquipSkill(offer.SkillId), Is.False);
            Assert.That(run.TryUnequipSkill(run.GetEquippedLane(offer.Skill.LaneIndex)[0].SkillId), Is.True);
            Assert.That(run.TryEquipSkill(offer.SkillId), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);

            IReadOnlyList<LegacySkill> lane = run.CreateDuel().GetLane(offer.Skill.LaneIndex);
            Assert.That(lane[0].Id, Is.EqualTo(offer.SkillId));
            Assert.That(lane[0].AnimationName, Is.EqualTo(offer.Skill.AnimationName));
        }

        [Test]
        public void InvalidOrUnaffordablePurchase_HasNoMutation()
        {
            var run = NewBattleRun();
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            run.TryUpgradeSkill(1);
            int offers = run.Offers.Count;
            Assert.That(run.Currency, Is.EqualTo(30));
            Assert.That(run.TryAcquireSkill(run.Offers[0].SkillId), Is.False);
            Assert.That(run.TryAcquireSkill(1), Is.False);
            Assert.That(run.TryAcquireSkill(-1), Is.False);
            Assert.That(run.TryUpgradeSkill(-1), Is.False);
            Assert.That(run.TryUpgradeSkill(run.Offers[0].SkillId), Is.False);
            Assert.That(run.Currency, Is.EqualTo(30));
            Assert.That(run.Offers.Count, Is.EqualTo(offers));
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
        }

        [Test]
        public void Upgrade_PreservesCombatIdentityAndChangesRealDuelPowerWithoutMutatingBasis()
        {
            var run = NewBattleRun();
            LegacySkill basis = LegacyInitialSkills.All[0];
            LegacyQueuedDuel oldDuel = run.CreateDuel(99);
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(run.TryUpgradeSkill(1), Is.True);

            CampaignOwnedSkill owned = Owned(run, 1);
            Assert.That(owned.Level, Is.EqualTo(1));
            Assert.That(owned.UpgradeCost, Is.EqualTo(55));
            Assert.That(owned.BaseSkill, Is.SameAs(basis));
            Assert.That(owned.Skill, Is.Not.SameAs(basis));
            Assert.That(owned.Skill.Id, Is.EqualTo(basis.Id));
            Assert.That(owned.Skill.Name, Is.EqualTo("베기 +1"));
            Assert.That(owned.Skill.MinPower, Is.EqualTo(basis.MinPower + 2));
            Assert.That(owned.Skill.MaxPower, Is.EqualTo(basis.MaxPower + 2));
            Assert.That(owned.Skill.Cost, Is.EqualTo(basis.Cost));
            Assert.That(owned.Skill.Kind, Is.EqualTo(basis.Kind));
            Assert.That(owned.Skill.Property, Is.EqualTo(basis.Property));
            Assert.That(owned.Skill.AttackCount, Is.EqualTo(basis.AttackCount));
            Assert.That(owned.Skill.LaneIndex, Is.EqualTo(basis.LaneIndex));
            Assert.That(owned.Skill.AnimationName, Is.EqualTo(basis.AnimationName));
            Assert.That(owned.Skill.IconId, Is.EqualTo(basis.IconId));
            Assert.That(basis.MinPower, Is.EqualTo(4));
            Assert.That(basis.MaxPower, Is.EqualTo(5));
            Assert.That(oldDuel.GetLane(0)[0].MinPower, Is.EqualTo(4));

            run.TryStartNextStage();
            LegacyQueuedDuel enhancedDuel = run.CreateDuel(99);
            Assert.That(enhancedDuel.GetLane(0)[0], Is.SameAs(owned.Skill));
            Assert.That(enhancedDuel.TryQueueLane(0), Is.True);
            enhancedDuel.Commit();
            LegacySlotResult result = enhancedDuel.ResolveNextSlot();
            Assert.That(result.EnemyResistanceDamage, Is.InRange(6, 7));
        }

        [Test]
        public void Upgrade_UsesEscalatingCostsAndStopsAtLevelThree()
        {
            var run = NewBattleRun();
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(Owned(run, 1).UpgradeCost, Is.EqualTo(30));
            Assert.That(run.TryUpgradeSkill(1), Is.True);
            Assert.That(run.TryUpgradeSkill(1), Is.False);
            Assert.That(run.Currency, Is.EqualTo(30));
            run.TryStartNextStage();
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(Owned(run, 1).UpgradeCost, Is.EqualTo(55));
            Assert.That(run.TryUpgradeSkill(1), Is.True);
            Assert.That(run.Currency, Is.EqualTo(45));
            run.TryStartNextStage();
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(Owned(run, 1).UpgradeCost, Is.EqualTo(80));
            Assert.That(run.TryUpgradeSkill(1), Is.True);
            Assert.That(Owned(run, 1).Level, Is.EqualTo(3));
            Assert.That(Owned(run, 1).Skill.MinPower, Is.EqualTo(10));
            Assert.That(Owned(run, 1).Skill.MaxPower, Is.EqualTo(11));
            Assert.That(Owned(run, 1).Skill.Name, Is.EqualTo("베기 +3"));
            ReachMaintenance(run, 5);
            int currency = run.Currency;
            Assert.That(currency, Is.GreaterThan(Owned(run, 1).UpgradeCost));
            Assert.That(run.TryUpgradeSkill(1), Is.False);
            Assert.That(run.Currency, Is.EqualTo(currency));
        }

        [Test]
        public void AllStages_IncreaseStatsAndEnemyPowerWithoutChangingPatternOrPlayerRecovery()
        {
            var run = NewBattleRun();
            int rewardTotal = 0;
            var names = new HashSet<string>();
            for (int number = 1; number <= 8; number++)
            {
                CampaignStage stage = run.CurrentStage;
                Assert.That(stage.Number, Is.EqualTo(number));
                Assert.That(stage.Name, Is.Not.Empty);
                Assert.That(names.Add(stage.Name), Is.True);
                Assert.That(stage.EnemyHealth, Is.EqualTo(80 + 15 * (number - 1)));
                Assert.That(stage.EnemyResistance, Is.EqualTo(15 + 3 * (number - 1)));
                Assert.That(stage.EnemyPowerBonus, Is.EqualTo(number - 1));
                Assert.That(stage.Reward, Is.EqualTo(60 + 10 * (number - 1)));

                LegacyQueuedDuel duel = run.CreateDuel(number);
                Assert.That(duel.Player.Health, Is.EqualTo(100));
                Assert.That(duel.Player.Resistance, Is.EqualTo(50));
                Assert.That(duel.Enemy.Health, Is.EqualTo(stage.EnemyHealth));
                Assert.That(duel.Enemy.Resistance, Is.EqualTo(stage.EnemyResistance));
                Assert.That(duel.EnemyQueue.Count, Is.EqualTo(2));
                for (int index = 0; index < 2; index++)
                {
                    Assert.That(duel.EnemyQueue[index].MinPower,
                        Is.EqualTo(LegacyInitialSkills.All[index].MinPower + stage.EnemyPowerBonus));
                    Assert.That(duel.EnemyQueue[index].MaxPower,
                        Is.EqualTo(LegacyInitialSkills.All[index].MaxPower + stage.EnemyPowerBonus));
                }

                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                rewardTotal += stage.Reward;
                Assert.That(run.Currency, Is.EqualTo(rewardTotal));
                if (number < 8) Assert.That(run.TryStartNextStage(), Is.True);
            }
            Assert.That(rewardTotal, Is.EqualTo(760));
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Completed));
            Assert.That(run.LastReward, Is.EqualTo(130));
            Assert.That(run.TryStartNextStage(), Is.False);
            Assert.That(run.RetryCurrentStage(), Is.False);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.TryAcquireSkill(run.Offers[0].SkillId), Is.False);
            Assert.That(run.TryUpgradeSkill(1), Is.False);
        }

        [Test]
        public void Reset_RestoresInitialOwnershipOffersStatsAndCurrency()
        {
            var run = NewBattleRun();
            int offerCount = run.Offers.Count;
            ReachMaintenance(run, 3);
            int acquiredId = run.Offers[0].SkillId;
            run.TryAcquireSkill(acquiredId);
            run.TryUpgradeSkill(1);
            run.TryStartNextStage();
            run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory);
            run.Reset();

            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(run.StageNumber, Is.EqualTo(1));
            Assert.That(run.Currency, Is.Zero);
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
            Assert.That(run.Offers.Count, Is.EqualTo(offerCount));
            Assert.That(Owned(run, 1).Level, Is.Zero);
            Assert.That(Owned(run, 1).Skill.MinPower, Is.EqualTo(4));
            Assert.That(Owned(run, acquiredId), Is.Null);
            Assert.That(run.CreateDuel().Enemy.Health, Is.EqualTo(80));
        }

        [Test]
        public void ShopCatalog_HasUniqueUnownedIdsAndSupportedLegacyAnimationFamilies()
        {
            var run = NewBattleRun();
            var ids = new HashSet<int>();
            foreach (CampaignOwnedSkill owned in run.OwnedSkills) ids.Add(owned.SkillId);
            Assert.That(run.Offers.Count, Is.GreaterThanOrEqualTo(3));
            foreach (CampaignSkillOffer offer in run.Offers)
            {
                Assert.That(ids.Add(offer.SkillId), Is.True);
                Assert.That(offer.Skill.LaneIndex, Is.InRange(0, 2));
                Assert.That(offer.Price, Is.EqualTo(45 + 10 * (offer.Skill.Cost - 1)));
                CollectionAssert.Contains(new[] { "Slash", "Penetrate", "Hit", "Defense" }, offer.Skill.AnimationName);
            }
        }

        [Test]
        public void EnemyForecast_ReusesTwoThreeTwoOnePatternAcrossTurns()
        {
            LegacyQueuedDuel duel = new CampaignRun().CreateDuel(13);
            int[][] expectedIds = { new[] { 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 1 }, new[] { 2 } };
            for (int turn = 0; turn < expectedIds.Length; turn++)
            {
                CollectionAssert.AreEqual(expectedIds[turn], SkillIds(duel.EnemyQueue));
                duel.Commit();
                while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
                Assert.That(duel.IsFinished, Is.False);
                if (turn + 1 < expectedIds.Length) duel.BeginNextTurn();
            }
        }

        [Test]
        public void AcquiredUpgrade_PreservesSharedIconAndAnimationWhileJoiningNextStageSnapshot()
        {
            var run = NewBattleRun();
            ReachMaintenance(run, 3);
            Assert.That(run.TryAcquireSkill(14), Is.True);
            Assert.That(run.TryUpgradeSkill(14), Is.True);
            CampaignOwnedSkill owned = Owned(run, 14);
            Assert.That(owned.Skill.IconId, Is.EqualTo(10));
            Assert.That(owned.Skill.AnimationName, Is.EqualTo("Slash"));
            Assert.That(owned.Skill.MinPower, Is.EqualTo(8));
            Assert.That(owned.Skill.MaxPower, Is.EqualTo(14));
            Assert.That(owned.BaseSkill.MinPower, Is.EqualTo(6));
            Assert.That(owned.BaseSkill.MaxPower, Is.EqualTo(12));
            Assert.That(run.IsSkillEquipped(14), Is.False);
            Assert.That(run.TryUnequipSkill(7), Is.True);
            Assert.That(run.TryEquipSkill(14), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.TryStartNextStage(), Is.True);
            IReadOnlyList<LegacySkill> lane = run.CreateDuel(17).GetLane(0);
            Assert.That(lane[lane.Count - 1], Is.SameAs(owned.Skill));
        }

        private static CampaignRun NewBattleRun()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            return run;
        }

        private static void ReachMaintenance(CampaignRun run, int stageNumber)
        {
            while (run.StageNumber <= stageNumber)
            {
                if (run.Phase == CampaignPhase.Battle) run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
                if (run.StageNumber == stageNumber) return;
                Assert.That(run.TryStartNextStage(), Is.True);
            }
        }

        private static CampaignOwnedSkill Owned(CampaignRun run, int skillId)
        {
            foreach (CampaignOwnedSkill owned in run.OwnedSkills) if (owned.SkillId == skillId) return owned;
            return null;
        }

        private static int[] SkillIds(IReadOnlyList<LegacySkill> skills)
        {
            var ids = new int[skills.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = skills[i].Id;
            return ids;
        }
    }
}
