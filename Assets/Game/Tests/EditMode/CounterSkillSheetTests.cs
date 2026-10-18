using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class CounterSkillSheetTests
    {
        private const int CounterId = 600;
        private static int Index(string header) => LegacySkillSheet.Headers.ToList().IndexOf(header);
        private static List<string[]> Rows() => CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
            .Select(r => r.Fields.ToArray()).ToList();
        private static void Set(List<string[]> rows, string header, string value)
            => rows.Single(r => r[0] == CounterId.ToString())[Index(header)] = value;
        private static LegacySkillTable Parse(List<string[]> rows) => LegacySkillSheet.Parse("counter test", CsvTable.Write(rows));

        [Test]
        public void StartingCounter_IsAvailableWithoutTakingAnOrdinaryStartingSlot()
        {
            LegacySkillTable table = Parse(Rows());
            LegacySkillDefinition counter = table.Find(CounterId);
            Assert.That(counter.IsCounter, Is.True);
            Assert.That(counter.CounterUsesPerTurn, Is.EqualTo(1));
            Assert.That(counter.Skill.Cost, Is.Zero);
            Assert.That(table.InitialSkills.Count, Is.EqualTo(9));
            Assert.That(table.InitialSkills.Any(s => s.Id == CounterId), Is.False);
            Assert.That(table.InitialCounterSkills.Select(s => s.Id), Has.Member(CounterId));
            Assert.That(table.GroupOf(counter), Is.EqualTo(LegacySkillGroup.Starting));
        }

        [Test]
        public void CounterMetadata_RoundTripsAndIsNamedInSheetChanges()
        {
            LegacySkillTable before = Parse(Rows());
            List<string[]> rows = Rows();
            Set(rows, Column.CounterUsesPerTurn, "2");
            LegacySkillTable after = Parse(rows);
            LegacySkillTable roundTrip = LegacySkillSheet.Parse("again", LegacySkillSheet.Write(after));
            Assert.That(roundTrip.Find(CounterId).CounterUsesPerTurn, Is.EqualTo(2));
            Assert.That(LegacySkillSheet.Describe(before, after).Lines.Single(), Does.Contain("반격 횟수"));
        }

        [Test]
        public void OlderSheetWithoutCounterColumn_KeepsItsNineStartingSkills()
        {
            List<string[]> rows = Rows().Where(r => r[0] != CounterId.ToString()).ToList();
            int at = Index(Column.CounterUsesPerTurn);
            rows = rows.Select(r => r.Where((cell, index) => index != at).ToArray()).ToList();
            LegacySkillTable table = Parse(rows);
            Assert.That(table.InitialSkills.Count, Is.EqualTo(9));
            Assert.That(table.InitialCounterSkills, Is.Empty);
            Assert.That(table.All.All(d => !d.IsCounter && d.CounterUsesPerTurn == 0), Is.True);
        }

        [Test]
        public void AcquiredCounter_RemainsAnAcquisitionInsteadOfBeingGrantedAtStart()
        {
            List<string[]> rows = Rows();
            Set(rows, Column.Group, LegacySkillSheet.AcquisitionGroup);
            LegacySkillTable table = Parse(rows);
            Assert.That(table.InitialCounterSkills.Any(s => s.Id == CounterId), Is.False);
            Assert.That(table.AcquisitionSkills.Select(s => s.Id), Has.Member(CounterId));
            Assert.That(table.Find(CounterId).IsCounter, Is.True);
        }

        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("매번")]
        public void InvalidCounterCount_IsReportedAsASheetProblem(string count)
        {
            List<string[]> rows = Rows();
            Set(rows, Column.CounterUsesPerTurn, count);
            var error = Assert.Throws<SkillSheetException>(() => Parse(rows));
            Assert.That(error.Problems, Has.Some.Contains("반격 횟수"));
        }

        [TestCase(Column.Cost, "1")]
        [TestCase(Column.Group, "적")]
        [TestCase(Column.Group, "일회성")]
        [TestCase(Column.ReserveEffect, "숨고르기 +1")]
        [TestCase(Column.UseEffect, "자동 회피")]
        [TestCase(Column.QueueRemovalMode, "반환")]
        [TestCase(Column.CyclePowerPerUse, "1")]
        public void CounterRejectsCostsAndQueueEffectsItCannotUse(string column, string value)
        {
            List<string[]> rows = Rows();
            Set(rows, column, value);
            if (column == Column.CyclePowerPerUse) Set(rows, Column.CycleMaxCount, "2");
            Assert.Throws<SkillSheetException>(() => Parse(rows));
        }

        [Test]
        public void DefenceAndInitialEffects_AreSupportedByTheSameCounterMetadata()
        {
            List<string[]> rows = Rows();
            Set(rows, Column.Kind, "방어");
            Set(rows, Column.Property, "방어");
            Set(rows, Column.ActGain, "2");
            Set(rows, Column.PowerBuff, "20%");
            Set(rows, Column.BuffSlots, "1");
            LegacySkillDefinition counter = Parse(rows).Find(CounterId);
            Assert.That(counter.Skill.Kind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(counter.Effect.ActGain, Is.EqualTo(2));
            Assert.That(counter.Effect.BuffPowerPercent, Is.EqualTo(20));
        }

        [Test]
        public void CounterCannotEnterTheVersusRoster_RegardlessOfItsAcquisitionGroup()
        {
            var loadout = new VersusLoadout();
            int[] before = loadout.ExportIds();
            Assert.That(loadout.TryPlaceSkill(CounterId, 0, 0), Is.False);
            int[] invalid = loadout.ExportIds();
            invalid[0] = CounterId;
            Assert.That(loadout.TryImportIds(invalid), Is.False);
            CollectionAssert.AreEqual(before, loadout.ExportIds());
            string original = LegacySkillSheet.Write(LegacySkillDefinitions.Table);
            List<string[]> rows = Rows();
            Set(rows, Column.Group, LegacySkillSheet.AcquisitionGroup);
            try
            {
                string changed = CsvTable.Write(rows);
                LegacySkillDefinitions.Install(() => changed);
                Assert.That(loadout.TryPlaceSkill(CounterId, 0, 0), Is.False);
            }
            finally { LegacySkillDefinitions.Install(() => original); }
        }
    }
}
