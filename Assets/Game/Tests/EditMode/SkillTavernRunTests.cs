using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using TurnLimbo.Runtime.Tavern;

namespace TurnLimbo.Core.Tests
{
    public sealed class SkillTavernRunTests
    {
        [Test]
        public void NewRun_StartsWithEightBreathsThreeOffersAndFirstIncome()
        {
            var run = Run();
            Assert.That(run.RoundNumber, Is.EqualTo(1));
            Assert.That(run.RoundIncome, Is.EqualTo(3));
            Assert.That(run.Gold, Is.EqualTo(run.RoundIncome));
            Assert.That(run.Board.Count, Is.EqualTo(8));
            Assert.That(run.Board.All(card => card == null), Is.True);
            Assert.That(run.ShopOffers.Count, Is.EqualTo(3));
            Assert.That(run.ShopOffers.All(skill => skill != null), Is.True);
            Assert.That(run.Inventory, Is.Empty);
            Assert.That(run.BuildQueue().All(skill => ReferenceEquals(skill, LegacyCommonActions.Breathe)), Is.True);
        }

        [Test]
        public void Buy_UsesShopPriceAndStoresACopyWithoutDeploying()
        {
            var run = new SkillTavernRun(new[] { Skill(101, cost: 9) });
            LegacySkill offer = run.ShopOffers[1];
            Assert.That(run.CanBuy(1), Is.True);
            Assert.That(run.TryBuy(1), Is.True);
            Assert.That(run.Gold, Is.Zero, "The shop price is independent of a technique's combat ACT cost.");
            Assert.That(run.Inventory.Count, Is.EqualTo(1));
            Assert.That(run.Inventory[0].InstanceId, Is.GreaterThan(0));
            AssertSkillCopy(run.Inventory[0].Skill, offer);
            Assert.That(run.ShopOffers[1], Is.Null);
            Assert.That(run.Board.All(card => card == null), Is.True);
        }

        [Test]
        public void BuyAndDeploy_UsesTheSamePriceAndPlacesACopyDirectlyInTheFirstHole()
        {
            var run = Run();
            LegacySkill offer = run.ShopOffers[0];
            Assert.That(run.CanBuyAndDeploy(0), Is.True);
            Assert.That(run.TryBuyAndDeploy(0), Is.True);
            Assert.That(run.Gold, Is.Zero);
            Assert.That(run.Inventory, Is.Empty);
            Assert.That(run.Board[0].InstanceId, Is.EqualTo(1));
            AssertSkillCopy(run.Board[0].Skill, offer);
            Assert.That(run.ShopOffers[0], Is.Null);
            Assert.That(run.CanBuy(0), Is.False);
            Assert.That(run.CanBuyAndDeploy(0), Is.False);
        }

        [Test]
        public void DirectAndStoredPurchases_HaveEqualCostsAndDoNotChargeForLaterDeployment()
        {
            var direct = Run();
            var stored = Run();
            Assert.That(direct.TryBuyAndDeploy(0), Is.True);
            Assert.That(stored.TryBuy(0), Is.True);
            Assert.That(direct.Gold, Is.EqualTo(stored.Gold));
            Assert.That(stored.Board.All(card => card == null), Is.True);
            Assert.That(stored.TryDeploy(stored.Inventory[0].InstanceId), Is.True);
            Assert.That(Snapshot(stored), Is.EqualTo(Snapshot(direct)));
        }

        [Test]
        public void BuyAndDeploy_FillsASoldPositionWithoutMovingOtherCardsOrStoredCopies()
        {
            var run = Run();
            FillBoard(run, 3);
            SkillTavernCard left = run.Board[0], right = run.Board[2];
            Assert.That(run.TrySellBoard(1), Is.True);
            run.NextRound();
            SkillTavernCard stored = Buy(run, 1);
            run.NextRound();
            int beforeGold = run.Gold;
            Assert.That(run.TryBuyAndDeploy(0), Is.True);
            Assert.That(run.Gold, Is.EqualTo(beforeGold - SkillTavernRun.PurchasePrice));
            Assert.That(run.Board[0], Is.SameAs(left));
            Assert.That(run.Board[1], Is.Not.Null);
            Assert.That(run.Board[1].InstanceId, Is.Not.EqualTo(stored.InstanceId));
            Assert.That(run.Board[2], Is.SameAs(right));
            Assert.That(run.Board[3], Is.Null);
            Assert.That(run.Inventory.Single(), Is.SameAs(stored));
        }

        [Test]
        public void FullFormation_RejectsDirectPurchaseWithoutChargeButStillAllowsStorage()
        {
            var run = Run();
            FillBoard(run, 8);
            run.NextRound();
            Assert.That(run.CanBuy(0), Is.True);
            Assert.That(run.CanBuyAndDeploy(0), Is.False);
            AssertUnchanged(run, () => run.TryBuyAndDeploy(0));
            int gold = run.Gold;
            Assert.That(run.TryBuy(0), Is.True);
            Assert.That(run.Gold, Is.EqualTo(gold - SkillTavernRun.PurchasePrice));
            Assert.That(run.Inventory.Count, Is.EqualTo(1));
            Assert.That(run.Board.All(card => card != null), Is.True);
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(int.MaxValue)]
        public void InvalidDirectPurchase_IsRejectedAtomicallyWithoutConsumingAnInstanceId(int index)
        {
            var run = Run();
            Assert.That(run.CanBuyAndDeploy(index), Is.False);
            AssertUnchanged(run, () => run.TryBuyAndDeploy(index));
            Assert.That(run.TryBuyAndDeploy(0), Is.True);
            Assert.That(run.Board[0].InstanceId, Is.EqualTo(1));
        }

        [Test]
        public void DirectPurchaseWithoutEnoughGold_LeavesTheOfferAndFormationUntouched()
        {
            var run = Run();
            Assert.That(run.TryBuyAndDeploy(0), Is.True);
            Assert.That(run.CanBuyAndDeploy(1), Is.False);
            AssertUnchanged(run, () => run.TryBuyAndDeploy(1));
        }

        [Test]
        public void PurchasedOffer_CannotBeBoughtAgainEvenAfterSelling()
        {
            var run = Run();
            Assert.That(run.TryBuy(0), Is.True);
            Assert.That(run.TrySellInventory(run.Inventory[0].InstanceId), Is.True);
            AssertUnchanged(run, () => run.TryBuy(0));
            Assert.That(run.CanBuy(0), Is.False);
        }

        [Test]
        public void InsufficientGold_LeavesOffersInventoryAndBoardUntouched()
        {
            var run = Run();
            Assert.That(run.TryBuy(0), Is.True);
            Assert.That(run.CanBuy(1), Is.False);
            AssertUnchanged(run, () => run.TryBuy(1));
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(int.MaxValue)]
        public void InvalidShopIndex_IsRejectedWithoutChanges(int index)
        {
            var run = Run();
            Assert.That(run.CanBuy(index), Is.False);
            AssertUnchanged(run, () => run.TryBuy(index));
        }

        [Test]
        public void Deploy_ConsumesInventoryInPurchaseIndependentOrder()
        {
            var run = Run();
            SkillTavernCard first = Buy(run, 0);
            run.NextRound();
            SkillTavernCard second = Buy(run, 1);
            Assert.That(run.CanDeploy(second.InstanceId), Is.True);
            Assert.That(run.TryDeploy(second.InstanceId), Is.True);
            Assert.That(run.Board[0], Is.SameAs(second));
            Assert.That(run.Inventory.Single(), Is.SameAs(first));
            Assert.That(run.TryDeploy(first.InstanceId), Is.True);
            Assert.That(run.Board[1], Is.SameAs(first));
            Assert.That(run.Inventory, Is.Empty);
            Assert.That(run.CanDeploy(first.InstanceId), Is.False);
            AssertUnchanged(run, () => run.TryDeploy(first.InstanceId));
        }

        [Test]
        public void SellBoard_RefundsOneAndLeavesTheExactPositionEmpty()
        {
            var run = Run();
            FillBoard(run, 3);
            SkillTavernCard left = run.Board[0];
            SkillTavernCard right = run.Board[2];
            int beforeGold = run.Gold;
            Assert.That(run.CanSellBoard(1), Is.True);
            Assert.That(run.TrySellBoard(1), Is.True);
            Assert.That(run.Gold, Is.EqualTo(beforeGold + 1));
            Assert.That(run.Board[0], Is.SameAs(left));
            Assert.That(run.Board[1], Is.Null);
            Assert.That(run.Board[2], Is.SameAs(right));
            Assert.That(run.Inventory, Is.Empty, "Selling does not return a card to storage.");
            Assert.That(run.BuildQueue()[1], Is.SameAs(LegacyCommonActions.Breathe));
            Assert.That(run.CanSellBoard(1), Is.False);
            AssertUnchanged(run, () => run.TrySellBoard(1));
        }

        [Test]
        public void Deploy_FillsTheLeftmostHoleBeforeLaterEmptyPositions()
        {
            var run = Run();
            FillBoard(run, 4);
            SkillTavernCard last = run.Board[3];
            Assert.That(run.TrySellBoard(1), Is.True);
            Assert.That(run.TrySellBoard(2), Is.True);
            run.NextRound();
            SkillTavernCard replacement = Buy(run);
            Assert.That(run.TryDeploy(replacement.InstanceId), Is.True);
            Assert.That(run.Board[1], Is.SameAs(replacement));
            Assert.That(run.Board[2], Is.Null);
            Assert.That(run.Board[3], Is.SameAs(last));
            Assert.That(run.Board[4], Is.Null);
        }

        [Test]
        public void FullBoard_RejectsDeploymentWithoutLosingTheStoredCard()
        {
            var run = Run();
            FillBoard(run, SkillTavernRun.SlotCount);
            run.NextRound();
            SkillTavernCard waiting = Buy(run);
            Assert.That(run.CanDeploy(waiting.InstanceId), Is.False);
            AssertUnchanged(run, () => run.TryDeploy(waiting.InstanceId));
            Assert.That(run.Inventory.Single(), Is.SameAs(waiting));
        }

        [Test]
        public void InventorySale_RefundsOneWithoutChangingOtherCopies()
        {
            var run = new SkillTavernRun(new[] { Skill(101) });
            SkillTavernCard first = Buy(run);
            run.NextRound();
            SkillTavernCard second = Buy(run);
            int beforeGold = run.Gold;
            Assert.That(run.CanSellInventory(first.InstanceId), Is.True);
            Assert.That(run.TrySellInventory(first.InstanceId), Is.True);
            Assert.That(run.Gold, Is.EqualTo(beforeGold + SkillTavernRun.SellPrice));
            Assert.That(run.Inventory.Single(), Is.SameAs(second));
            Assert.That(run.CanSellInventory(first.InstanceId), Is.False);
            AssertUnchanged(run, () => run.TrySellInventory(first.InstanceId));
            AssertUnchanged(run, () => run.TryDeploy(first.InstanceId));
        }

        [TestCase(-1)]
        [TestCase(8)]
        [TestCase(int.MaxValue)]
        public void InvalidBoardIndex_IsRejectedWithoutChanges(int index)
        {
            var run = Run();
            FillBoard(run, 1);
            Assert.That(run.CanSellBoard(index), Is.False);
            AssertUnchanged(run, () => run.TrySellBoard(index));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(int.MaxValue)]
        public void UnknownInstance_CannotBeDeployedOrSold(int instanceId)
        {
            var run = Run();
            Buy(run);
            Assert.That(run.CanDeploy(instanceId), Is.False);
            Assert.That(run.CanSellInventory(instanceId), Is.False);
            AssertUnchanged(run, () => run.TryDeploy(instanceId));
            AssertUnchanged(run, () => run.TrySellInventory(instanceId));
        }

        [Test]
        public void RefreshShop_SpendsOneWithoutChangingRoundOrOwnedCards()
        {
            var run = new SkillTavernRun(Enumerable.Range(101, 12).Select(id => Skill(id)), seed: 27);
            Assert.That(run.TryBuyAndDeploy(0), Is.True);
            run.NextRound();
            SkillTavernCard stored = Buy(run, 1);
            run.NextRound();
            LegacySkill[] oldOffers = run.ShopOffers.ToArray();
            SkillTavernCard deployed = run.Board[0];
            int gold = run.Gold, round = run.RoundNumber, income = run.RoundIncome;

            Assert.That(run.CanRefreshShop, Is.True);
            Assert.That(run.TryRefreshShop(), Is.True);

            Assert.That(run.Gold, Is.EqualTo(gold - 1));
            Assert.That(run.RoundNumber, Is.EqualTo(round));
            Assert.That(run.RoundIncome, Is.EqualTo(income));
            Assert.That(run.Board[0], Is.SameAs(deployed));
            Assert.That(run.Board.Skip(1), Is.All.Null);
            Assert.That(run.Inventory.Single(), Is.SameAs(stored));
            Assert.That(run.BattlesPlayed, Is.Zero);
            Assert.That(run.ShopOffers.Select(skill => skill.Id).Distinct().Count(), Is.EqualTo(3));
            for (int index = 0; index < SkillTavernRun.ShopSize; index++)
                Assert.That(run.ShopOffers[index], Is.Not.SameAs(oldOffers[index]));
            Assert.That(run.TryBuy(0), Is.True);
            Assert.That(run.Inventory[1].InstanceId, Is.EqualTo(3), "Refreshing creates no purchased card instances.");
        }

        [Test]
        public void RefreshShop_CanRepeatUntilZero_AndRejectedRequestsDoNotConsumeRandomRolls()
        {
            LegacySkill[] catalog = Enumerable.Range(101, 12).Select(id => Skill(id)).ToArray();
            var run = new SkillTavernRun(catalog, seed: 593);
            var control = new SkillTavernRun(catalog, seed: 593);
            for (int remaining = 2; remaining >= 0; remaining--)
            {
                Assert.That(run.TryRefreshShop(), Is.True);
                Assert.That(control.TryRefreshShop(), Is.True);
                Assert.That(run.Gold, Is.EqualTo(remaining));
                Assert.That(Snapshot(run), Is.EqualTo(Snapshot(control)));
            }
            LegacySkill[] exhaustedOffers = run.ShopOffers.ToArray();
            Assert.That(run.CanRefreshShop, Is.False);
            AssertUnchanged(run, () => run.TryRefreshShop());
            AssertUnchanged(run, () => run.TryRefreshShop());
            for (int index = 0; index < SkillTavernRun.ShopSize; index++)
                Assert.That(run.ShopOffers[index], Is.SameAs(exhaustedOffers[index]));
            run.NextRound();
            control.NextRound();
            Assert.That(Snapshot(run), Is.EqualTo(Snapshot(control)), "A failed refresh must not change later offers.");
            Assert.That(run.Gold, Is.EqualTo(4), "Refreshing never advances income or carries the old balance.");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void RefreshShop_RefillsPurchasedSlotsEvenWithASmallCatalog(int catalogSize)
        {
            var run = new SkillTavernRun(Enumerable.Range(101, catalogSize).Select(id => Skill(id)), seed: 51);
            Assert.That(run.TryBuyAndDeploy(0), Is.True);
            Assert.That(run.ShopOffers[0], Is.Null);
            Assert.That(run.TrySellBoard(0), Is.True);
            Assert.That(run.Gold, Is.EqualTo(1));
            Assert.That(run.TryRefreshShop(), Is.True);
            Assert.That(run.Gold, Is.Zero);
            Assert.That(run.RoundNumber, Is.EqualTo(1));
            Assert.That(run.Board, Is.All.Null);
            Assert.That(run.Inventory, Is.Empty);
            Assert.That(run.ShopOffers.Count, Is.EqualTo(3));
            Assert.That(run.ShopOffers, Is.All.Not.Null);
            Assert.That(run.ShopOffers.Select(skill => skill.Id).Distinct().Count(), Is.EqualTo(catalogSize));
            Assert.That(run.ShopOffers[0], Is.Not.SameAs(run.ShopOffers[2]));
        }

        [Test]
        public void NextRound_ReplacesGoldAndOffersButPreservesFormationAndInventory()
        {
            var run = Run();
            SkillTavernCard deployed = Buy(run);
            Assert.That(run.TryDeploy(deployed.InstanceId), Is.True);
            run.NextRound();
            SkillTavernCard stored = Buy(run);
            LegacySkill[] oldOffers = run.ShopOffers.ToArray();
            Assert.That(run.Gold, Is.EqualTo(1));
            run.NextRound();
            Assert.That(run.RoundNumber, Is.EqualTo(3));
            Assert.That(run.RoundIncome, Is.EqualTo(5));
            Assert.That(run.Gold, Is.EqualTo(5), "Unspent currency does not carry over.");
            Assert.That(run.Board[0], Is.SameAs(deployed));
            Assert.That(run.Inventory.Single(), Is.SameAs(stored));
            for (int index = 0; index < SkillTavernRun.ShopSize; index++)
            {
                Assert.That(run.ShopOffers[index], Is.Not.Null);
                Assert.That(run.ShopOffers[index], Is.Not.SameAs(oldOffers[index]));
            }
        }

        [Test]
        public void RoundIncome_GrowsToTenAndNeverExceedsIt()
        {
            var run = Run();
            for (int round = 1; round <= 25; round++)
            {
                Assert.That(run.RoundNumber, Is.EqualTo(round));
                Assert.That(run.RoundIncome, Is.EqualTo(Math.Min(10, round + 2)));
                Assert.That(run.Gold, Is.EqualTo(run.RoundIncome));
                run.NextRound();
            }
        }

        [Test]
        public void SaleGold_CanExceedIncomeCapAndResetsNextRound()
        {
            var run = Run();
            FillBoard(run, 1);
            while (run.RoundNumber < 8) run.NextRound();
            Assert.That(run.TrySellBoard(0), Is.True);
            Assert.That(run.Gold, Is.EqualTo(11));
            run.NextRound();
            Assert.That(run.Gold, Is.EqualTo(10));
        }

        [Test]
        public void Inventory_CanHoldMoreThanEightCards()
        {
            var run = Run();
            for (int index = 0; index < 12; index++)
            {
                Buy(run);
                run.NextRound();
            }
            Assert.That(run.Inventory.Count, Is.EqualTo(12));
            Assert.That(run.Board.All(card => card == null), Is.True);
        }

        [Test]
        public void CopiesOfOneTechnique_HaveUniqueIdsAndSeparateSkills()
        {
            LegacySkill basis = Skill(101);
            var run = new SkillTavernRun(new[] { basis });
            SkillTavernCard first = Buy(run);
            run.NextRound();
            SkillTavernCard second = Buy(run);
            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
            Assert.That(first.Skill.Id, Is.EqualTo(second.Skill.Id));
            AssertSkillCopy(first.Skill, basis);
            AssertSkillCopy(second.Skill, first.Skill);
            Assert.That(run.TrySellInventory(first.InstanceId), Is.True);
            run.NextRound();
            SkillTavernCard third = Buy(run);
            Assert.That(third.InstanceId, Is.Not.EqualTo(first.InstanceId));
            Assert.That(third.InstanceId, Is.Not.EqualTo(second.InstanceId));
        }

        [Test]
        public void Queue_PreservesEightPositionsAndCanBeEditedWithoutChangingFormation()
        {
            var run = Run();
            FillBoard(run, 3);
            Assert.That(run.TrySellBoard(1), Is.True);
            LegacySkill[] queue = run.BuildQueue();
            Assert.That(queue.Length, Is.EqualTo(8));
            AssertSkillCopy(queue[0], run.Board[0].Skill);
            Assert.That(queue[1], Is.SameAs(LegacyCommonActions.Breathe));
            AssertSkillCopy(queue[2], run.Board[2].Skill);
            Assert.That(queue.Skip(3).All(skill => skill.IsWait), Is.True);
            queue[0] = LegacyCommonActions.Breathe;
            Assert.That(run.BuildQueue()[0].IsWait, Is.False);
            Assert.That(run.Board[0], Is.Not.Null);
        }

        [Test]
        public void Collections_RejectReplacementAndInsertion()
        {
            var run = Run();
            Assert.Throws<NotSupportedException>(() => ((IList<LegacySkill>)run.ShopOffers)[0] = Skill(999));
            Assert.Throws<NotSupportedException>(() => ((IList<SkillTavernCard>)run.Board)[0] = null);
            Assert.Throws<NotSupportedException>(() => ((IList<SkillTavernCard>)run.Inventory).Add(null));
            Buy(run);
            Assert.Throws<NotSupportedException>(() => ((IList<SkillTavernCard>)run.Inventory).Clear());
            Assert.That(run.Inventory.Count, Is.EqualTo(1));
        }

        [Test]
        public void Catalog_IsCopiedBeforeCallerChangesItsCollection()
        {
            LegacySkill basis = Skill(101);
            var catalog = new List<LegacySkill> { basis };
            var run = new SkillTavernRun(catalog);
            catalog.Clear();
            catalog.Add(Skill(999));
            for (int round = 0; round < 5; round++)
            {
                foreach (LegacySkill offer in run.ShopOffers) AssertSkillCopy(offer, basis);
                run.NextRound();
            }
        }

        [Test]
        public void CatalogSkills_AreCopiedBeforeAnotherModeUpgradesTheSource()
        {
            var campaign = new CampaignRun();
            CampaignOwnedSkill owned = campaign.GetOwnedSkill(1);
            int originalMin = owned.Skill.MinPower;
            var run = new SkillTavernRun(new[] { owned.Skill });
            for (int count = 0; count < owned.ExperienceRequired; count++) owned.GainClashExperience();
            Assert.That(owned.Skill.MinPower, Is.GreaterThan(originalMin));
            run.NextRound();
            Assert.That(run.ShopOffers.All(skill => skill.MinPower == originalMin), Is.True);
            Assert.That(run.ShopOffers.All(skill => !skill.Name.EndsWith("+", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void Seed_ReproducesOffersAndInstanceIdsAcrossTheSameActions()
        {
            LegacySkill[] pool = Enumerable.Range(101, 12).Select(id => Skill(id)).ToArray();
            var first = new SkillTavernRun(pool, seed: 943);
            var second = new SkillTavernRun(pool, seed: 943);
            for (int round = 0; round < 30; round++)
            {
                Assert.That(Snapshot(first), Is.EqualTo(Snapshot(second)));
                Assert.That(first.TryBuy(round % 3), Is.True);
                Assert.That(second.TryBuy(round % 3), Is.True);
                first.NextRound();
                second.NextRound();
            }
            Assert.That(Snapshot(first), Is.EqualTo(Snapshot(second)));
        }

        [Test]
        public void AdequateCatalog_NeverRepeatsATechniqueWithinOneShop()
        {
            var run = new SkillTavernRun(Enumerable.Range(101, 12).Select(id => Skill(id)), seed: 2);
            for (int round = 0; round < 100; round++)
            {
                Assert.That(run.ShopOffers.Select(skill => skill.Id).Distinct().Count(), Is.EqualTo(3));
                run.NextRound();
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void SmallCatalog_StillFillsAllThreeOffersWithDistinctCopies(int size)
        {
            var run = new SkillTavernRun(Enumerable.Range(101, size).Select(id => Skill(id)));
            Assert.That(run.ShopOffers.Count, Is.EqualTo(3));
            Assert.That(run.ShopOffers.All(skill => skill != null), Is.True);
            Assert.That(run.ShopOffers.Select(skill => skill.Id).Distinct().Count(), Is.EqualTo(size));
            Assert.That(run.ShopOffers[0], Is.Not.SameAs(run.ShopOffers[2]));
        }

        [Test]
        public void DuplicateCatalogEntries_DoNotCreateDuplicateOffersWhenThreeKindsExist()
        {
            var run = new SkillTavernRun(new[] { Skill(101), Skill(101), Skill(102), Skill(103) });
            for (int round = 0; round < 20; round++)
            {
                Assert.That(run.ShopOffers.Select(skill => skill.Id), Is.EquivalentTo(new[] { 101, 102, 103 }));
                run.NextRound();
            }
        }

        [Test]
        public void InvalidCatalogs_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new SkillTavernRun((IEnumerable<LegacySkill>)null));
            Assert.Throws<ArgumentException>(() => new SkillTavernRun(new LegacySkill[0]));
            Assert.Throws<ArgumentException>(() => new SkillTavernRun(new LegacySkill[] { null }));
            Assert.Throws<ArgumentException>(() => new SkillTavernRun(new[] { LegacyCommonActions.Breathe }));
        }

        [Test]
        public void DefaultCatalog_UsesSheetGroupsRatherThanFixedTechniqueIds()
        {
            List<string[]> rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            int groupColumn = Array.IndexOf(rows[0], LegacySkillSheet.Column.Group);
            int[] freeIds = Enumerable.Range(1, 999)
                .Except(rows.Skip(1).Select(row => int.Parse(row[0]))).Reverse().Take(2).ToArray();
            int addedId = freeIds[0], acquiredCounterId = freeIds[1];
            int originalCounterId = LegacySkillDefinitions.All.First(definition => definition.IsCounter).Skill.Id;
            string[] added = (string[])rows.Single(row => row[0] == "1").Clone();
            added[0] = addedId.ToString();
            added[groupColumn] = LegacySkillSheet.AcquisitionGroup;
            rows.Add(added);
            string[] acquiredCounter = (string[])rows.Single(row => row[0] == originalCounterId.ToString()).Clone();
            acquiredCounter[0] = acquiredCounterId.ToString();
            acquiredCounter[groupColumn] = LegacySkillSheet.AcquisitionGroup;
            rows.Add(acquiredCounter);
            string customSheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => customSheet);
                LegacySkillTable table = LegacySkillDefinitions.Table;
                int[] expected = table.All.Where(definition => !definition.IsCounter && !definition.Skill.IsWait &&
                    (table.GroupOf(definition) == LegacySkillGroup.Starting ||
                     table.GroupOf(definition) == LegacySkillGroup.Acquisition))
                    .Select(definition => definition.Skill.Id).ToArray();
                var observed = new HashSet<int>();
                var run = new SkillTavernRun(seed: 867);
                for (int round = 0; round < 500; round++)
                {
                    foreach (LegacySkill offer in run.ShopOffers)
                    {
                        Assert.That(expected, Does.Contain(offer.Id));
                        Assert.That(offer, Is.Not.SameAs(table.Find(offer.Id).Skill));
                        observed.Add(offer.Id);
                    }
                    run.NextRound();
                }
                Assert.That(observed, Is.EquivalentTo(expected));
                Assert.That(observed, Does.Contain(addedId));
                Assert.That(observed.Contains(acquiredCounterId), Is.False);
                Assert.That(observed.Contains(originalCounterId), Is.False);
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        [Test]
        public void ShopAndFormationActions_DoNotModifyTheSharedSkillTable()
        {
            string before = LegacySkillSheet.Write(LegacySkillDefinitions.Table);
            var run = new SkillTavernRun(seed: 20);
            for (int round = 0; round < 20; round++)
            {
                SkillTavernCard card = Buy(run);
                if (round < SkillTavernRun.SlotCount) Assert.That(run.TryDeploy(card.InstanceId), Is.True);
                else Assert.That(run.TrySellInventory(card.InstanceId), Is.True);
                run.BuildQueue();
                run.NextRound();
            }
            Assert.That(LegacySkillSheet.Write(LegacySkillDefinitions.Table), Is.EqualTo(before));
        }

        private static SkillTavernRun Run() => new SkillTavernRun(new[] { Skill(101), Skill(102), Skill(103) }, seed: 10);

        private static LegacySkill Skill(int id, int cost = 1)
            => new LegacySkill(id, "기술 " + id, cost, 4, 7, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 2, 1, "테스트 설명", "Slash", 3);

        private static SkillTavernCard Buy(SkillTavernRun run, int shopIndex = 0)
        {
            Assert.That(run.TryBuy(shopIndex), Is.True);
            return run.Inventory[run.Inventory.Count - 1];
        }

        private static void FillBoard(SkillTavernRun run, int count)
        {
            for (int index = 0; index < count; index++)
            {
                if (index > 0) run.NextRound();
                SkillTavernCard card = Buy(run);
                Assert.That(run.TryDeploy(card.InstanceId), Is.True);
            }
        }

        private static void AssertSkillCopy(LegacySkill actual, LegacySkill source)
        {
            Assert.That(actual, Is.Not.SameAs(source));
            Assert.That(new object[] { actual.Id, actual.IconId, actual.Name, actual.Cost, actual.MinPower,
                actual.MaxPower, actual.Kind, actual.Property, actual.AttackCount, actual.LaneIndex,
                actual.Description, actual.AnimationName }, Is.EqualTo(new object[] { source.Id, source.IconId,
                source.Name, source.Cost, source.MinPower, source.MaxPower, source.Kind, source.Property,
                source.AttackCount, source.LaneIndex, source.Description, source.AnimationName }));
        }

        private static void AssertUnchanged(SkillTavernRun run, Func<bool> action)
        {
            string before = Snapshot(run);
            Assert.That(action(), Is.False);
            Assert.That(Snapshot(run), Is.EqualTo(before));
        }

        private static string Snapshot(SkillTavernRun run)
            => run.RoundNumber + ":" + run.Gold + ":" +
               string.Join(",", run.ShopOffers.Select(skill => skill == null ? "-" : skill.Id.ToString())) + ":" +
               string.Join(",", run.Board.Select(card => card == null ? "-" : card.InstanceId + "/" + card.Skill.Id)) + ":" +
               string.Join(",", run.Inventory.Select(card => card.InstanceId + "/" + card.Skill.Id));
    }
}
