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
        public void Table_SplitsIntoStartingCurriculumAndEnemySkills()
        {
            IReadOnlyList<LegacySkillDefinition> all = LegacySkillDefinitions.All;
            Assert.That(all.Select(d => d.Skill).ToArray(), Is.EqualTo(LegacyInitialSkills.All
                .Concat(CampaignSkillCatalog.AcquisitionSkills).Concat(LegacySkillDefinitions.EnemySkills).ToArray()));
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
        public void StartingSkills_TeachBasicQConditionalEAndConcentratedW()
        {
            Assert.That(LegacyInitialSkills.All.Where(s => s.LaneIndex == 0).Select(s => s.Id),
                Is.EqualTo(new[] { 1, 2, 7 }));
            Assert.That(LegacyInitialSkills.All.Where(s => s.LaneIndex == 1).Select(s => s.Id),
                Is.EqualTo(new[] { 3, 4, 8 }));
            Assert.That(LegacyInitialSkills.All.Where(s => s.LaneIndex == 2).Select(s => s.Id),
                Is.EqualTo(new[] { 5, 6, 9 }));

            LegacySkill slash = LegacySkillDefinitions.Skill(1);
            LegacySkill combo = LegacySkillDefinitions.Skill(2);
            LegacySkill guard = LegacySkillDefinitions.Skill(7);
            Assert.That(new[] { slash.Cost, combo.Cost, guard.Cost }, Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(combo.Name, Is.EqualTo("연속 베기"));
            Assert.That(combo.AttackCount, Is.EqualTo(2));
            Assert.That(combo.MaxPower, Is.EqualTo(9));
            Assert.That(LegacySkillDefinitions.Find(combo).HighPower, Is.False,
                "The basic Q combo must leave the concentrated burst role to W.");
            Assert.That(guard.Kind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(LegacySkillDefinitions.Find(slash).Effect.ActGain, Is.EqualTo(1));
            Assert.That(LegacySkillDefinitions.Find(guard).Effect.ActGain, Is.EqualTo(2));
            Assert.That(LegacySkillDefinitions.Find(guard).Effect.OpponentProperty, Is.EqualTo(LegacySkillProperty.Hit));

            LegacySkill deep = LegacySkillDefinitions.Skill(3);
            LegacySkill precise = LegacySkillDefinitions.Skill(4);
            LegacySkill parry = LegacySkillDefinitions.Skill(8);
            Assert.That(new[] { deep.Cost, precise.Cost, parry.Cost }, Is.EqualTo(new[] { 2, 3, 3 }));
            Assert.That(deep.MinPower, Is.GreaterThan(combo.MaxPower));
            Assert.That(precise.MinPower, Is.GreaterThan(deep.MinPower));
            Assert.That(parry.MinPower, Is.GreaterThan(guard.MaxPower));
            Assert.That(deep.AttackCount, Is.EqualTo(1));
            Assert.That(precise.AttackCount, Is.EqualTo(1));
            Assert.That(parry.Kind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(LegacySkillDefinitions.Find(parry).Effect.BuffProtectionPercent, Is.EqualTo(25));
            Assert.That(LegacySkillDefinitions.Find(parry).Effect.BuffSlots, Is.EqualTo(1));

            LegacySkill pressure = LegacySkillDefinitions.Skill(5);
            LegacySkill opening = LegacySkillDefinitions.Skill(6);
            LegacySkill deflect = LegacySkillDefinitions.Skill(9);
            Assert.That(new[] { pressure.Cost, opening.Cost, deflect.Cost }, Is.EqualTo(new[] { 1, 2, 1 }));
            Assert.That(LegacySkillDefinitions.Find(pressure).Effect.OpponentKind, Is.EqualTo(LegacySkillKind.Attack));
            Assert.That(LegacySkillDefinitions.Find(pressure).Effect.OpponentResistanceReduction, Is.EqualTo(5));
            Assert.That(LegacySkillDefinitions.Find(opening).Effect.OpponentKind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(LegacySkillDefinitions.Find(opening).Effect.OpponentResistanceReduction, Is.EqualTo(8));
            Assert.That(LegacySkillDefinitions.Find(deflect).Effect.BuffPowerPercent, Is.EqualTo(20));
            Assert.That(LegacySkillDefinitions.Find(deflect).Effect.BuffSlots, Is.EqualTo(2));
        }

        [Test]
        public void FirstTwoStageQRewards_UseDedicatedArtworkAndTheirOwnRules()
        {
            LegacySkill scout = LegacySkillDefinitions.Skill(43);
            Assert.That((scout.Name, scout.Cost, scout.MinPower, scout.MaxPower, scout.LaneIndex),
                Is.EqualTo(("탐색", 1, 2, 3, 0)));
            Assert.That(scout.Kind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(scout.IconId, Is.EqualTo(16));
            Assert.That(LegacySkillDefinitions.Find(scout).Effect.ActGain, Is.EqualTo(1));

            LegacySkill barrage = LegacySkillDefinitions.Skill(44);
            Assert.That((barrage.Name, barrage.Cost, barrage.MinPower, barrage.MaxPower, barrage.AttackCount),
                Is.EqualTo(("몰아치기", 3, 10, 15, 4)));
            Assert.That(barrage.Kind, Is.EqualTo(LegacySkillKind.Attack));
            Assert.That(barrage.Property, Is.EqualTo(LegacySkillProperty.Slash));
            Assert.That(barrage.LaneIndex, Is.Zero);
            Assert.That(barrage.IconId, Is.EqualTo(17));
            Assert.That(LegacySkillDefinitions.Find(barrage).Effect.BrokenTargetDamagePercent, Is.EqualTo(25));
            Assert.That((LegacySkillRoles.Get(barrage) & LegacySkillRole.BrokenTargetDamage) != 0, Is.True);
        }

        [Test]
        public void Laudare_IsIasEnemyOnlyFiveHitSlashThatBreaksItsTarget()
        {
            LegacySkill laudare = LegacySkillDefinitions.Skill(500);
            Assert.That((laudare.Name, laudare.Cost, laudare.MinPower, laudare.MaxPower, laudare.AttackCount, laudare.LaneIndex),
                Is.EqualTo(("라우다레", 0, 30, 35, 5, 0)));
            Assert.That(laudare.Kind, Is.EqualTo(LegacySkillKind.Attack));
            Assert.That(laudare.Property, Is.EqualTo(LegacySkillProperty.Slash));
            Assert.That(laudare.IconId, Is.EqualTo(18), "Its own empowered slash icon.");
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(laudare);
            Assert.That(definition.Effect.BreaksOpponent, Is.True);
            Assert.That(definition.Effect.HasOpponentCondition, Is.False, "It breaks whatever it meets.");
            Assert.That(definition.HighPower, Is.True);
            Assert.That(LegacySkillDefinitions.Table.IsEnemy(definition), Is.True);
            Assert.That(LegacySkillDefinitions.EnemySkills.Select(s => s.Id), Is.EqualTo(new[] { 500, 501, 502 }));
            Assert.That(LegacyInitialSkills.All.Concat(CampaignSkillCatalog.AcquisitionSkills), Has.No.Member(laudare));
            Assert.That(definition.Text.Info.EnemyMain, Is.EqualTo("내 저항 붕괴"), "The player reads it as the enemy's skill.");
            Assert.That(CampaignCurriculum.Default.FindGranting(500), Is.Null);

            // The player can neither equip it nor load a save that does.
            var run = new CampaignRun();
            Assert.That(run.TryEquipSkill(500), Is.False);
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 500), Is.False);
            var save = new CampaignSave(0, new int[0], new string[0], null, 0,
                new[] { new[] { 500, 2, 7 }, new[] { 3, 4, 8 }, new[] { 5, 6, 9 } });
            Assert.That(run.TryRestore(save, out string error), Is.False);
            Assert.That(error, Does.Contain("500"));
        }

        [Test]
        public void BenedicereAndPraedicare_FollowLaudareInIasMottoTurn()
        {
            LegacySkill benedicere = LegacySkillDefinitions.Skill(501);
            Assert.That((benedicere.Name, benedicere.Cost, benedicere.MinPower, benedicere.MaxPower, benedicere.AttackCount, benedicere.LaneIndex),
                Is.EqualTo(("베네디체레", 0, 5, 10, 1, 0)));
            Assert.That((benedicere.Kind, benedicere.Property), Is.EqualTo((LegacySkillKind.Defence, LegacySkillProperty.Defence)));
            Assert.That(benedicere.IconId, Is.EqualTo(19), "Its own empowered helmet icon.");
            LegacySkillDefinition guard = LegacySkillDefinitions.Find(benedicere);
            Assert.That((guard.Effect.ResistanceRecoveryPercent, guard.Effect.OpponentState), Is.EqualTo((25, LegacyOpponentState.Broken)));
            Assert.That(guard.Effect.HasOpponentCondition || guard.HighPower, Is.False);
            Assert.That(guard.Text.Info.Secondary, Is.EqualTo("상대 붕괴 시"));
            Assert.That(guard.Text.Info.EnemySecondary, Is.EqualTo("내 저항 붕괴 시"), "The player reads it as the enemy's skill.");

            LegacySkill praedicare = LegacySkillDefinitions.Skill(502);
            Assert.That((praedicare.Name, praedicare.Cost, praedicare.MinPower, praedicare.MaxPower, praedicare.AttackCount, praedicare.LaneIndex),
                Is.EqualTo(("프레디카레", 0, 24, 32, 1, 0)));
            Assert.That((praedicare.Kind, praedicare.Property), Is.EqualTo((LegacySkillKind.Attack, LegacySkillProperty.Slash)));
            Assert.That(praedicare.IconId, Is.EqualTo(20), "Its own empowered finisher icon.");
            LegacySkillDefinition finisher = LegacySkillDefinitions.Find(praedicare);
            Assert.That((finisher.Effect.OpponentState, finisher.Effect.OpponentHealthPercent, finisher.Effect.ConditionalDamagePercent),
                Is.EqualTo((LegacyOpponentState.HealthAtMost, 30, 200)));
            Assert.That(finisher.HighPower, Is.True);
            Assert.That(finisher.Text.Info.Secondary, Is.EqualTo("상대 체력 30% 이하"));
            Assert.That(finisher.Text.Info.EnemySecondary, Is.EqualTo("내 체력 30% 이하"));

            var run = new CampaignRun();
            foreach (LegacySkillDefinition definition in new[] { guard, finisher })
            {
                Assert.That(LegacySkillDefinitions.Table.IsEnemy(definition), Is.True);
                Assert.That(definition.Skill.Description, Is.EqualTo("서막 4 임무 이아의 수훈 기술."));
                Assert.That(definition.Text.Purpose ?? definition.Text.Effect, Is.Null, "Never in the lobby.");
                Assert.That(CampaignCurriculum.Default.FindGranting(definition.Skill.Id), Is.Null);
                Assert.That(run.TryEquipSkill(definition.Skill.Id), Is.False);
            }
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
        public void ShippedSheet_HoldsTheTwentyFourTechniquesInTableOrder()
        {
            string path = Path.Combine(Environment.CurrentDirectory, LegacySkillDefinitions.SheetAssetPath);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Take(3).ToArray(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 with a byte order mark, for Excel.");
            LegacySkillTable table = LegacySkillSheet.Parse("shipped", File.ReadAllText(path));
            Assert.That(table.All.Select(d => d.Skill.Id),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 14, 15, 16, 17, 21, 32, 10, 12, 19, 42, 43, 44, 500, 501, 502 }));
            Assert.That(table.InitialSkills.Select(s => s.Id), Is.EqualTo(Enumerable.Range(1, 9)));
            Assert.That(table.AcquisitionSkills.Select(s => s.Id), Is.EqualTo(new[] { 14, 15, 16, 17, 21, 32, 10, 12, 19, 42, 43, 44 }));
            Assert.That(table.EnemySkills.Select(s => s.Id), Is.EqualTo(new[] { 500, 501, 502 }));
            Assert.That(LegacySkillDefinitions.All.Select(d => d.Skill.Id), Is.EqualTo(table.All.Select(d => d.Skill.Id)),
                "The runtime reads the same file.");
            Assert.That(LegacySkillDefinitions.Table.SheetId, Is.EqualTo(LegacySkillDefinitions.SheetAssetPath));
            Assert.That(LegacySkillDefinitions.SheetAssetPath, Is.EqualTo("Assets/Game/Resources/" + LegacySkillDefinitions.SheetResourcePath + ".csv"));
        }

        [Test]
        public void ShippedSheet_UsesOnlyImportedIcons()
        {
            // Presentation's LegacyDuelArt.SkillIconCount; the runtime does not know the art source.
            const int skillIconCount = 20;
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
                Assert.That(definition.Skill.IconId, Is.InRange(1, skillIconCount), "icon of " + definition.Skill.Id);
        }

        [TestCase(16, "알티바호")]
        [TestCase(12, "플레슈")]
        [TestCase(19, "르프리즈")]
        [TestCase(42, "쿠페")]
        [TestCase(1, "베기")]
        [TestCase(17, "호흡")]
        [TestCase(43, "탐색")]
        [TestCase(44, "몰아치기")]
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
                Assert.That(LegacySkillDefinitions.Skill(2).Name, Is.EqualTo("연속 베기"));
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

                Assert.That(PrologueMissions.Get(1).PlayerSkills.Select(s => s.Name), Is.EqualTo(new[] { "새 베기", "연속 베기" }));
                Assert.That(PrologueMissions.Get(4).EnemySkills.Select(s => s.Id), Is.EqualTo(new[] { 1, 5, 7, 2 }));
                Assert.That(StoryMissions.Get(8).PlayerSkills.Select(s => s.Id), Is.EqualTo(new[] { 1, 2, 7, 3, 4, 8, 5, 6, 9 }));
                Assert.That(new LegacyQueuedDuel().EnemyQueue.Select(s => s.Name), Is.EqualTo(new[] { "새 베기", "연속 베기" }));
                Assert.That(CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Gatekeeper).Turn(1).Select(s => s.Id), Is.EqualTo(new[] { 1, 3 }));

                var run = new CampaignRun();
                Assert.That(run.GetStage(5).EnemyCounterBasis, Is.SameAs(LegacySkillDefinitions.Skill(7)));
                Assert.That(run.GetStage(7).EnemyCounterBasis, Is.SameAs(LegacySkillDefinitions.Skill(4)));
                Assert.That(run.TryStartStage(1), Is.True);
                Assert.That(run.CreateDuel().EnemyQueue.Select(s => s.Name), Is.EqualTo(new[] { "새 베기", "연속 베기" }));
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
                // 상대 상태 조건 stands alone and gates a recovery or a multiplier; the multiplier needs it and an attack.
                if (effect.HasOpponentStateCondition)
                {
                    Assert.That(effect.HasOpponentCondition, Is.False, id);
                    Assert.That(effect.ResistanceRecoveryPercent > 0 || effect.ConditionalDamagePercent > 0, Is.True, id);
                }
                if (effect.OpponentState == LegacyOpponentState.HealthAtMost)
                    Assert.That(effect.OpponentHealthPercent, Is.InRange(1, 99), id);
                else Assert.That(effect.OpponentHealthPercent, Is.Zero, id);
                if (effect.ConditionalDamagePercent != 0)
                {
                    Assert.That(effect.ConditionalDamagePercent, Is.GreaterThan(100), id);
                    Assert.That(effect.HasOpponentStateCondition, Is.True, id);
                    Assert.That(definition.Skill.Kind, Is.EqualTo(LegacySkillKind.Attack), id);
                }
                if (definition.HighPower || definition.VariablePower)
                    Assert.That(definition.Skill.Kind, Is.EqualTo(LegacySkillKind.Attack), id);
            }
        }
    }
}
