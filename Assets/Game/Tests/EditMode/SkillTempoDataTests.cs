using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using TurnLimbo.Runtime.Tavern;

namespace TurnLimbo.Core.Tests
{
    public sealed class SkillTempoDataTests
    {
        private static List<string[]> Rows() => CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
            .Select(record => record.Fields.ToArray()).ToList();
        private static int Column(string header) => LegacySkillSheet.Headers.ToList().IndexOf(header);
        private static LegacySkillTable Parse(IEnumerable<string[]> rows) => LegacySkillSheet.Parse("tempo data test", CsvTable.Write(rows));

        [Test]
        public void ExistingTechniquesAndCommonBreathing_KeepTheirNormalDuration()
        {
            Assert.That(LegacySkillDefinitions.All.Where(definition => definition.Skill.Id != 601 && definition.Skill.Id != 602)
                .All(definition => definition.Skill.DurationHalfUnits == 2), Is.True);
            Assert.That(LegacyCommonActions.Breathe.DurationHalfUnits, Is.EqualTo(2));
            var ordinary = new LegacySkill(901, "ordinary", 1, 1, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");
            Assert.That(ordinary.DurationHalfUnits, Is.EqualTo(2));
            Assert.That(ordinary.IsFast || ordinary.IsCharge, Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(6)]
        [TestCase(8)]
        public void DurationConstructor_AcceptsHalfNormalOrWholeSlotCharges(int duration)
        {
            var skill = new LegacySkill(901, "sample", 1, 1, 1, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 1, 0, "", durationHalfUnits: duration);
            Assert.That(skill.DurationHalfUnits, Is.EqualTo(duration));
            Assert.That(skill.IsFast, Is.EqualTo(duration == 1));
            Assert.That(skill.IsCharge, Is.EqualTo(duration >= 4));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(int.MaxValue)]
        public void DurationConstructor_RejectsUnsupportedOrUnboundedLengths(int duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacySkill(901, "sample", 1, 1, 1,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "", durationHalfUnits: duration));
        }

        [Test]
        public void OlderSheetWithoutDurationColumn_ReadsEveryTechniqueAsNormal()
        {
            int at = Column(LegacySkillSheet.Column.DurationSlots);
            LegacySkillTable old = Parse(Rows().Select(row => row.Where((cell, index) => index != at).ToArray()));
            Assert.That(old.All.All(definition => definition.Skill.DurationHalfUnits == 2), Is.True);
            Assert.That(old.InitialSkills.Count, Is.EqualTo(9));
        }

        [TestCase("0")]
        [TestCase("-0.5")]
        [TestCase("1.5")]
        [TestCase("2.5")]
        [TestCase("5")]
        [TestCase("고속")]
        [TestCase("99999999999999999999999999999999")]
        public void InvalidSheetDuration_IsReportedAtTheDurationColumn(string value)
        {
            List<string[]> rows = Rows();
            rows.Single(row => row[0] == "601")[Column(LegacySkillSheet.Column.DurationSlots)] = value;
            SkillSheetException error = Assert.Throws<SkillSheetException>(() => Parse(rows));
            Assert.That(error.Problems, Has.Some.Contains("소요 칸"));
        }

        [Test]
        public void Duration_RoundTripsAndIsNamedInTheSheetChangeSummary()
        {
            LegacySkillTable before = Parse(Rows());
            List<string[]> rows = Rows();
            rows.Single(row => row[0] == "601")[Column(LegacySkillSheet.Column.DurationSlots)] = "3";
            LegacySkillTable after = Parse(rows);
            LegacySkillTable reread = LegacySkillSheet.Parse("reread", LegacySkillSheet.Write(after));
            Assert.That(reread.Find(601).Skill.DurationHalfUnits, Is.EqualTo(6));
            Assert.That(reread.Find(602).Skill.DurationHalfUnits, Is.EqualTo(4));
            Assert.That(LegacySkillSheet.Describe(before, after).Lines.Single(), Does.Contain("소요 칸"));
        }

        [Test]
        public void CounterTechnique_CannotClaimAFastOrChargeDuration()
        {
            List<string[]> rows = Rows();
            rows.Single(row => row[0] == "600")[Column(LegacySkillSheet.Column.DurationSlots)] = "0.5";
            Assert.That(Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems, Has.Some.Contains("반격 기술"));
        }

        [Test]
        public void PrototypeRows_AreOwnedWithoutChangingTheInitialOrOtherModePools()
        {
            LegacySkillTable table = LegacySkillDefinitions.Table;
            Assert.That(table.PrototypeSkills.Select(skill => skill.Id), Is.EqualTo(new[] { 601, 602 }));
            Assert.That(table.InitialSkills.Count, Is.EqualTo(9));
            Assert.That(table.InitialSkills.Concat(table.AcquisitionSkills).Concat(table.VersusSkills)
                .Any(skill => skill.Id == 601 || skill.Id == 602), Is.False);
            Assert.That(table.GroupOf(table.Find(601)), Is.EqualTo(LegacySkillGroup.Prototype));
            Assert.That(table.Find(601).Skill.IsFast, Is.True);
            Assert.That(table.Find(602).Skill.IsCharge, Is.True);
            var versus = new VersusLoadout();
            Assert.That(versus.TryPlaceSkill(601, 0, 0), Is.False);
            Assert.That(versus.TryPlaceSkill(602, 1, 0), Is.False);
            for (int seed = 1; seed <= 32; seed++)
                Assert.That(new SkillTavernRun(seed).ShopOffers.Any(skill => skill.Id == 601 || skill.Id == 602), Is.False);
        }

        [Test]
        public void FreshAndOlderCampaignSaves_OfferBothPrototypesWithoutChangingTheirSavedLoadout()
        {
            var run = new CampaignRun();
            CampaignSave old = run.CaptureSave();
            var restored = new CampaignRun();
            Assert.That(restored.TryRestore(old, out string error), Is.True, error);
            foreach (CampaignRun candidate in new[] { run, restored })
            {
                Assert.That(candidate.GetOwnedSkill(601).Skill.IsFast, Is.True);
                Assert.That(candidate.GetOwnedSkill(602).Skill.DurationHalfUnits, Is.EqualTo(4));
                Assert.That(candidate.EquippedSkillCount, Is.EqualTo(9));
                Assert.That(candidate.GetEquippedLane(0).Select(owned => owned.SkillId), Is.EqualTo(new[] { 1, 2, 7 }));
                Assert.That(candidate.GetEquippedLane(1).Select(owned => owned.SkillId), Is.EqualTo(new[] { 3, 4, 8 }));
                Assert.That(candidate.HasLoadoutChanges, Is.False);
            }
        }

        [Test]
        public void CampaignOwnedCopies_KeepDurationThroughUpgradeSaveAndBattleCreation()
        {
            var run = new CampaignRun();
            Assert.That(run.TryPlaceLoadoutSkill(601, 0, 0), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(602, 1, 0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            foreach (int id in new[] { 601, 602 })
            {
                CampaignOwnedSkill owned = run.GetOwnedSkill(id);
                Assert.That(owned.Skill, Is.Not.SameAs(LegacySkillDefinitions.Skill(id)));
                for (int clash = 0; clash < owned.ExperienceRequired; clash++) owned.GainClashExperience();
                Assert.That(owned.Level, Is.EqualTo(1));
                Assert.That(owned.Skill.DurationHalfUnits, Is.EqualTo(id == 601 ? 1 : 4));
            }
            var restored = new CampaignRun();
            Assert.That(restored.TryRestore(run.CaptureSave(), out string error), Is.True, error);
            LegacyQueuedDuel duel = restored.CreateDuel();
            Assert.That(duel.GetLane(0)[0], Is.SameAs(restored.GetOwnedSkill(601).Skill));
            Assert.That(duel.GetLane(0)[0].DurationHalfUnits, Is.EqualTo(1));
            Assert.That(duel.GetLane(1)[0].DurationHalfUnits, Is.EqualTo(4));
            Assert.That(restored.GetOwnedSkill(601).Level, Is.EqualTo(1));
            Assert.That(restored.GetOwnedSkill(602).Level, Is.EqualTo(1));
            Assert.That(LegacySkillDefinitions.Skill(601).MinPower, Is.EqualTo(3), "Owned upgrades never alter the sheet.");
        }

        [Test]
        public void OlderCurriculumMigration_KeepsPrototypeLoadoutAndExperience()
        {
            var run = new CampaignRun();
            Assert.That(run.TryPlaceLoadoutSkill(601, 0, 0), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(602, 1, 0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            run.GetOwnedSkill(601).GainClashExperience();
            CampaignSave saved = run.CaptureSave();
            var old = new CampaignSave(saved.Currency, saved.ClearedStages, saved.CurriculumCompleted, saved.CurriculumPoints,
                saved.Loadout, saved.TrainingVictoryCount, saved.SkillExperience, saved.FieldPickups,
                curriculumTreeVersion: 0, counterSkillId: saved.CounterSkillId);
            var restored = new CampaignRun();
            Assert.That(restored.TryRestore(old, out string error), Is.True, error);
            Assert.That(restored.GetEquippedLane(0)[0].SkillId, Is.EqualTo(601));
            Assert.That(restored.GetEquippedLane(1)[0].SkillId, Is.EqualTo(602));
            Assert.That(restored.GetOwnedSkill(601).Experience, Is.EqualTo(1));
        }

        [Test]
        public void TavernExplicitCopies_PreserveMetadataWithoutAddingPrototypesToItsDefaultShop()
        {
            foreach (int id in new[] { 601, 602 })
            {
                LegacySkill source = LegacySkillDefinitions.Skill(id);
                var shop = new SkillTavernRun(new[] { source });
                Assert.That(shop.TryBuy(0), Is.True);
                Assert.That(shop.Inventory[0].Skill.DurationHalfUnits, Is.EqualTo(source.DurationHalfUnits));
                Assert.That(shop.Inventory[0].Skill, Is.Not.SameAs(source));
            }
        }

        [Test]
        public void CurriculumReset_KeepsPrototypeOwnershipAndEquippedCopies()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(601, 0, 0), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(602, 1, 0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            LegacySkill fast = run.GetOwnedSkill(601).Skill;
            LegacySkill charge = run.GetOwnedSkill(602).Skill;
            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(run.GetOwnedSkill(601).Skill, Is.SameAs(fast));
            Assert.That(run.GetOwnedSkill(602).Skill, Is.SameAs(charge));
            Assert.That(run.GetEquippedLane(0)[0].Skill, Is.SameAs(fast));
            Assert.That(run.GetEquippedLane(1)[0].Skill, Is.SameAs(charge));
            Assert.That(run.GetOwnedSkill(14), Is.Null, "Curriculum techniques still follow the existing reset rule.");
        }
    }
}
