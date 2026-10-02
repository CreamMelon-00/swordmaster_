using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacySkillDefinitionTests
    {
        [Test]
        public void Table_SplitsIntoStartingSkillsAndCurriculumSkills()
        {
            IReadOnlyList<LegacySkillDefinition> all = LegacySkillDefinitions.All;
            Assert.That(all.Select(d => d.Skill).ToArray(),
                Is.EqualTo(LegacyInitialSkills.All.Concat(CampaignSkillCatalog.AcquisitionSkills).ToArray()));
            // The curriculum may grow; the starting set stays at nine.
            Assert.That(LegacyInitialSkills.All.Count, Is.EqualTo(9));
            Assert.That(CampaignSkillCatalog.AcquisitionSkills.Count, Is.GreaterThan(0));
            Assert.That(all.Select(d => d.Skill.Id).Distinct().Count(), Is.EqualTo(all.Count));
        }

        [Test]
        public void StartingSkills_FillEachLaneWithThree()
        {
            for (int lane = 0; lane < 3; lane++)
                Assert.That(LegacyInitialSkills.All.Count(s => s.LaneIndex == lane), Is.EqualTo(3), "lane " + lane);
        }

        [Test]
        public void Find_UsesTheIdForCopiesAndIgnoresUnknownSkills()
        {
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                LegacySkill s = definition.Skill;
                var copy = new LegacySkill(s.Id, s.Name + "+1", s.Cost, s.MinPower + 3, s.MaxPower + 3, s.Kind,
                    s.Property, s.AttackCount, s.LaneIndex, s.Description, s.AnimationName, s.IconId);
                Assert.That(LegacySkillDefinitions.Find(s), Is.SameAs(definition));
                Assert.That(LegacySkillDefinitions.Find(copy), Is.SameAs(definition));
            }
            Assert.That(LegacySkillDefinitions.Find(null), Is.Null);
            Assert.That(LegacySkillDefinitions.Find(LegacyCommonActions.Breathe), Is.Null);
            Assert.That(LegacySkillDefinitions.Find(new LegacySkill(950, "unknown", 1, 4, 6,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "")), Is.Null);
        }

        [Test]
        public void EveryTableSkill_HasItsOwnLabelAndDetail()
        {
            // A table id keeps its label and detail even if a copy's shape ever changes.
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                Assert.That(definition.Text.ShortLabel, Is.Not.Null.And.Not.Empty, "label of " + definition.Skill.Id);
                Assert.That(definition.Text.Detail, Is.Not.Null.And.Not.Empty, "detail of " + definition.Skill.Id);
            }
        }

        [Test]
        public void ShippedSheet_HoldsTheNineteenTechniquesInTableOrder()
        {
            string path = Path.Combine(Environment.CurrentDirectory, LegacySkillDefinitions.SheetAssetPath);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Take(3).ToArray(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 with a byte order mark, for Excel.");
            LegacySkillTable table = LegacySkillSheet.Parse("shipped", File.ReadAllText(path));
            Assert.That(table.All.Select(d => d.Skill.Id), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 14, 15, 16, 17, 21, 32, 10, 12, 19, 42 }));
            Assert.That(table.InitialSkills.Select(s => s.Id), Is.EqualTo(Enumerable.Range(1, 9)));
            Assert.That(table.AcquisitionSkills.Select(s => s.Id), Is.EqualTo(new[] { 14, 15, 16, 17, 21, 32, 10, 12, 19, 42 }));
            Assert.That(LegacySkillDefinitions.All.Select(d => d.Skill.Id), Is.EqualTo(table.All.Select(d => d.Skill.Id)),
                "The runtime reads the same file.");
            Assert.That(LegacySkillDefinitions.Table.SheetId, Is.EqualTo(LegacySkillDefinitions.SheetAssetPath));
            Assert.That(LegacySkillDefinitions.SheetAssetPath, Is.EqualTo("Assets/Game/Resources/" + LegacySkillDefinitions.SheetResourcePath + ".csv"));
        }

        [Test]
        public void ShippedSheet_UsesOnlyIconsTheAtlasHas()
        {
            // Presentation's LegacyDuelArt.SkillIconCount; the runtime does not know the atlas.
            const int skillIconCount = 15;
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
                Assert.That(definition.Skill.IconId, Is.InRange(1, skillIconCount), "icon of " + definition.Skill.Id);
        }

        [TestCase(16, "알티바호")]
        [TestCase(12, "플레슈")]
        [TestCase(19, "르프리즈")]
        [TestCase(42, "쿠페")]
        [TestCase(1, "베기")]
        [TestCase(17, "호흡")]
        public void ShippedSheet_NamesTheTechniques(int id, string name)
        {
            Assert.That(LegacySkillDefinitions.Skill(id).Name, Is.EqualTo(name));
        }

        [Test]
        public void FindAndSkill_LookUpById()
        {
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                Assert.That(LegacySkillDefinitions.Find(definition.Skill.Id), Is.SameAs(definition));
                Assert.That(LegacySkillDefinitions.Skill(definition.Skill.Id), Is.SameAs(definition.Skill));
            }
            Assert.That(LegacySkillDefinitions.Find(999), Is.Null);
            Assert.That(LegacySkillDefinitions.Find(LegacyCommonActions.Breathe.Id), Is.Null);
            var error = Assert.Throws<InvalidOperationException>(() => LegacySkillDefinitions.Skill(999));
            Assert.That(error.Message, Does.Contain("ID 999"));
            Assert.That(error.Message, Does.Contain(LegacySkillDefinitions.SheetAssetPath));
        }

        [Test]
        public void Install_AFailingSheetThrowsTheSameErrorUntilAGoodSheetReplacesIt()
        {
            string good = LegacySkillSheet.Write(LegacySkillDefinitions.Table);
            int reads = 0;
            try
            {
                LegacySkillDefinitions.Install(() =>
                {
                    reads++;
                    return "ID,이름\n1,베기\n";
                });
                var first = Assert.Throws<SkillSheetException>(() => _ = LegacySkillDefinitions.All);
                var second = Assert.Throws<SkillSheetException>(() => LegacySkillDefinitions.Find(1));
                Assert.That(second, Is.SameAs(first), "The failure is kept, not re-read.");
                Assert.That(reads, Is.EqualTo(1));
                Assert.That(first.Message, Does.StartWith("기술 시트 '" + LegacySkillDefinitions.SheetAssetPath + "'"));
                Assert.That(LegacySkillDefinitions.Find(null), Is.Null, "Null needs no table.");

                LegacySkillDefinitions.Reload();
                Assert.Throws<SkillSheetException>(() => _ = LegacySkillDefinitions.InitialSkills);
                Assert.That(reads, Is.EqualTo(2), "Reload reads the source again.");

                LegacySkillDefinitions.Install(() => throw new InvalidOperationException("no sheet"));
                Assert.That(Assert.Throws<InvalidOperationException>(() => _ = LegacySkillDefinitions.All).Message, Is.EqualTo("no sheet"));

                LegacySkillDefinitions.Install(() =>
                {
                    reads++;
                    return good.Replace(",베기,", ",새 베기,");
                });
                Assert.That(LegacySkillDefinitions.Skill(1).Name, Is.EqualTo("새 베기"));
                Assert.That(LegacySkillDefinitions.Skill(2).Name, Is.EqualTo("예리한 베기"));
                Assert.That(LegacySkillDefinitions.InitialSkills[0].Name, Is.EqualTo("새 베기"));
                Assert.That(reads, Is.EqualTo(3), "A good table is read once.");
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
            Assert.That(LegacySkillDefinitions.Skill(1).Name, Is.EqualTo("베기"), "Null goes back to the sheet file.");
        }

        [Test]
        public void Install_AFileThatCouldNotBeOpenedIsTriedAgainOnTheNextAccess()
        {
            // Excel holding the sheet: closing Excel does not change the file, so nothing would reload a kept failure.
            string good = LegacySkillSheet.Write(LegacySkillDefinitions.Table);
            int reads = 0;
            try
            {
                LegacySkillDefinitions.Install(() =>
                {
                    if (++reads == 1) throw new IOException("used by another process");
                    return good;
                });
                Assert.Throws<IOException>(() => _ = LegacySkillDefinitions.All);
                Assert.That(LegacySkillDefinitions.Skill(1).Name, Is.EqualTo("베기"));
                Assert.That(reads, Is.EqualTo(2));
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        [Test]
        public void ReorderedOrRenamedSheet_ReachesMissionsStagesAndEnemiesById()
        {
            List<string[]> rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            int name = LegacySkillSheet.Headers.ToList().IndexOf(LegacySkillSheet.Column.Name);
            // 막기 moves to the top, the acquisition rows reverse, and 베기 is renamed.
            string[] guard = rows.Single(row => row[0] == "7");
            rows.Remove(guard);
            rows.Insert(1, guard);
            rows.Reverse(10, rows.Count - 10);
            rows.Single(row => row[0] == "1")[name] = "새 베기";
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                Assert.That(LegacySkillDefinitions.All[0].Skill.Id, Is.EqualTo(7));
                Assert.That(LegacyInitialSkills.All.Select(s => s.Id), Is.EqualTo(new[] { 7, 1, 2, 3, 4, 5, 6, 8, 9 }));

                Assert.That(PrologueMissions.Get(1).PlayerSkills.Select(s => s.Name), Is.EqualTo(new[] { "새 베기", "예리한 베기" }));
                Assert.That(PrologueMissions.Get(4).EnemySkills.Select(s => s.Id), Is.EqualTo(new[] { 1, 5, 7, 2 }));
                Assert.That(StoryMissions.Get(8).PlayerSkills.Select(s => s.Id), Is.EqualTo(new[] { 1, 2, 7, 3, 4, 8, 5, 6, 9 }));
                Assert.That(new LegacyQueuedDuel().EnemyQueue.Select(s => s.Name), Is.EqualTo(new[] { "새 베기", "예리한 베기" }));
                Assert.That(CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Gatekeeper).Turn(1).Select(s => s.Id), Is.EqualTo(new[] { 1, 3 }));

                var run = new CampaignRun();
                Assert.That(run.GetStage(5).EnemyCounterBasis, Is.SameAs(LegacySkillDefinitions.Skill(7)));
                Assert.That(run.GetStage(7).EnemyCounterBasis, Is.SameAs(LegacySkillDefinitions.Skill(6)));
                Assert.That(run.TryStartStage(1), Is.True);
                Assert.That(run.CreateDuel().EnemyQueue.Select(s => s.Name), Is.EqualTo(new[] { "새 베기", "예리한 베기" }));
                Assert.That(run.GetEquippedLane(0).Select(s => s.SkillId), Is.EqualTo(new[] { 7, 1, 2 }),
                    "Only the default loadout follows the starting rows' order.");
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
            Assert.That(PrologueMissions.Get(1).PlayerSkills[0].Name, Is.EqualTo("베기"));
        }

        [Test]
        public void Effects_UseOnlyCombinationsTheInterpreterDefines()
        {
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                LegacySkillEffect effect = definition.Effect;
                string id = "skill " + definition.Skill.Id;
                Assert.That(effect.OpponentProperty.HasValue && effect.OpponentKind.HasValue, Is.False, id);
                Assert.That(effect.HasOpponentCondition && effect.ResistanceRecoveryPercent > 0, Is.False, id);
                if (effect.HasOpponentCondition)
                    Assert.That(effect.ActGain > 0 || effect.OpponentResistanceReduction > 0, Is.True, id);
                Assert.That(effect.HasBuff, Is.EqualTo(effect.BuffPowerPercent != 0 || effect.BuffProtectionPercent != 0), id);
                if (definition.HighPower || definition.VariablePower)
                    Assert.That(definition.Skill.Kind, Is.EqualTo(LegacySkillKind.Attack), id);
            }
        }
    }
}
