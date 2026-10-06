using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class CampaignSheetCheckTests
    {
        /// <summary>The shipped table as the writer lays it out, one string array per sheet row.</summary>
        private static List<string[]> Rows()
            => CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table)).Select(record => record.Fields.ToArray()).ToList();

        private static string[] RowOf(List<string[]> rows, int id) => rows.Single(row => row[0] == id.ToString());

        private static void Set(List<string[]> rows, int id, string header, string value)
            => RowOf(rows, id)[LegacySkillSheet.Headers.ToList().IndexOf(header)] = value;

        private static LegacySkillTable Parse(List<string[]> rows) => LegacySkillSheet.Parse("test", CsvTable.Write(rows));

        /// <summary>14 가로베기 made a starting skill and 막기 a 획득 one, so Q still has three starting skills.</summary>
        private static List<string[]> HorizontalCutStarting()
        {
            List<string[]> rows = Rows();
            Set(rows, 14, Column.Group, LegacySkillSheet.StartingGroup);
            Set(rows, 7, Column.Group, LegacySkillSheet.AcquisitionGroup);
            return rows;
        }

        [Test]
        public void ShippedSheet_FitsTheCode()
        {
            Assert.That(CampaignSheetCheck.Problems(LegacySkillDefinitions.Table), Is.Empty);
            Assert.That(CampaignSheetCheck.Warnings(LegacySkillDefinitions.Table), Is.Empty);
        }

        [Test]
        public void CurriculumSkillMadeStarting_IsAProblem_AndTheRowNoNodeGrantsAWarning()
        {
            LegacySkillTable table = Parse(HorizontalCutStarting());
            Assert.That(CampaignSheetCheck.Problems(table), Is.EqualTo(new[]
            {
                "ID 14 가로베기의 '구분'이(가) '시작'입니다. 커리큘럼 과정 'horizontal-cut'이(가) 주는 기술이라 '획득'이어야 합니다 " +
                "(시작 기술이면 그 과정을 마쳐도 새로 얻는 기술이 없습니다).",
            }));
            Assert.That(CampaignSheetCheck.Warnings(table), Is.EqualTo(new[]
            {
                "ID 7 막기: '구분'이(가) '획득'이지만 이 기술을 주는 커리큘럼 과정이 없어 게임에서 얻을 수 없습니다. " +
                "프로그래머가 CampaignCurriculum.cs에 과정을 넣어야 합니다.",
            }));
        }

        [Test]
        public void CurriculumSkillRenumberedOrDeleted_IsAProblem()
        {
            List<string[]> rows = Rows();
            Set(rows, 42, Column.Id, "45");
            LegacySkillTable renumbered = Parse(rows);
            Assert.That(CampaignSheetCheck.Problems(renumbered), Is.EqualTo(new[]
            {
                "ID 42 기술이 시트에 없습니다. 커리큘럼 과정 'quick-draw'이(가) 이 기술을 주므로 그 과정을 마치는 전투가 끝날 때 " +
                "게임이 멈춥니다. ID를 바꾸거나 행을 지우지 마세요.",
            }));
            Assert.That(CampaignSheetCheck.Warnings(renumbered).Single(), Does.StartWith("ID 45 쿠페: "));

            rows.Remove(RowOf(rows, 45));
            LegacySkillTable deleted = Parse(rows);
            Assert.That(CampaignSheetCheck.Problems(deleted).Single(), Does.StartWith("ID 42 기술이 시트에 없습니다."));
            Assert.That(CampaignSheetCheck.Warnings(deleted), Is.Empty);
        }

        [Test]
        public void EnemyRowGrantedByANodeOrAStage_IsAProblem_AndNeverAWarning()
        {
            List<string[]> rows = Rows();
            Set(rows, 14, Column.Group, LegacySkillSheet.EnemyGroup);
            Set(rows, 44, Column.Group, LegacySkillSheet.EnemyGroup);
            LegacySkillTable table = Parse(rows);
            Assert.That(CampaignSheetCheck.Problems(table), Is.EqualTo(new[]
            {
                "ID 14 가로베기의 '구분'이(가) '적'입니다. 커리큘럼 과정 'horizontal-cut'이(가) 주는 기술이라 '획득'이어야 합니다 " +
                "(적 기술은 플레이어가 얻거나 편성하지 않습니다).",
                "ID 44 몰아치기의 '구분'이(가) '적'입니다. 스테이지 2 첫 클리어 보상이라 '획득'이어야 합니다.",
            }));
            Assert.That(CampaignSheetCheck.Warnings(table), Is.Empty, "No node grants 500, and it needs none.");
        }

        [Test]
        public void MissingEnemySkillOfTheForcedLoss_IsAProblem()
        {
            List<string[]> rows = Rows();
            rows.Remove(RowOf(rows, 500));
            Assert.That(CampaignSheetCheck.Problems(Parse(rows)).Single(), Does.StartWith("ID 500 기술이 시트에 없습니다. 코드가 이 ID를 씁니다"));
        }

        [Test]
        public void CodeSkillRenumbered_IsAProblem()
        {
            List<string[]> rows = Rows();
            // Still a starting W skill, so the sheet's own rules pass.
            Set(rows, 3, Column.Id, "30");
            Assert.That(CampaignSheetCheck.Problems(Parse(rows)), Is.EqualTo(new[]
            {
                "ID 3 기술이 시트에 없습니다. 코드가 이 ID를 씁니다(스테이지 적의 행동·반격기, 서막·수련 임무와 그 안내 문구). " +
                "그 기술을 처음 쓰는 곳에서 게임이 멈추니 ID를 바꾸거나 행을 지우지 마세요.",
            }));
        }

        [Test]
        public void MissingStageReward_IsReportedAndStopsFirstClearBeforeChangingTheRun()
        {
            List<string[]> rows = Rows();
            rows.Remove(RowOf(rows, 43));
            LegacySkillTable table = Parse(rows);
            Assert.That(CampaignSheetCheck.Problems(table).Single(),
                Does.StartWith("ID 43 기술이 시트에 없습니다. 스테이지 1 첫 클리어 보상이므로"));
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                var run = new CampaignRun();
                Assert.That(run.TryStartStage(1), Is.True);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Assert.Throws<InvalidOperationException>(() => run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory));
                    Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Battle));
                    Assert.That(run.Currency, Is.Zero);
                    Assert.That(run.IsStageCleared(1), Is.False);
                    Assert.That(run.LastOutcome, Is.Null);
                }
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        [Test]
        public void Check_ReadsOnlyTheGivenTable()
        {
            LegacySkillTable table = Parse(Rows());
            try
            {
                LegacySkillDefinitions.Install(() => "ID\n1\n");
                Assert.That(CampaignSheetCheck.Problems(table), Is.Empty);
                Assert.That(CampaignSheetCheck.Warnings(table), Is.Empty);
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
            Assert.Throws<ArgumentNullException>(() => CampaignSheetCheck.Problems(null));
        }

        [Test]
        public void CodeSkillIds_CoverEveryTableIdTheCodeUses()
        {
            // Every name becomes [[id]], so formatted copy shows which ids it names.
            List<string[]> rows = Rows();
            foreach (string[] row in rows.Skip(1)) Set(rows, int.Parse(row[0]), Column.Name, "[[" + row[0] + "]]");
            string sheet = CsvTable.Write(rows);
            var used = new HashSet<int>();
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                var copy = new List<string>();
                foreach (PrologueMission mission in StoryMissions.All)
                {
                    copy.Add(mission.Title);
                    copy.AddRange(mission.Objectives);
                    copy.Add(mission.UnlockText);
                    foreach (MissionGuideBeat beat in mission.CreateGuide().Beats)
                        copy.AddRange(new[] { beat.Title, beat.Description, beat.InputHint });
                    foreach (LegacySkill skill in mission.PlayerSkills.Concat(mission.EnemySkills)) used.Add(skill.Id);
                    foreach (LegacySkill skill in mission.Empowerment?.EnemyScript.AllSkills ?? new LegacySkill[0]) used.Add(skill.Id);
                }
                foreach (string text in copy.Where(text => text != null))
                    foreach (Match match in Regex.Matches(text, @"\[\[(\d+)\]\]")) used.Add(int.Parse(match.Groups[1].Value));
                foreach (CampaignEnemyRhythm rhythm in Enum.GetValues(typeof(CampaignEnemyRhythm)))
                    foreach (LegacySkill skill in CampaignEnemyRhythms.Script(rhythm)?.AllSkills ?? new LegacySkill[0]) used.Add(skill.Id);
                var run = new CampaignRun();
                for (int stage = 1; stage <= run.StageCount; stage++)
                    if (run.GetStage(stage).EnemyCounterBasis != null) used.Add(run.GetStage(stage).EnemyCounterBasis.Id);
                foreach (LegacySkill skill in new LegacyQueuedDuel().EnemyQueue) used.Add(skill.Id);
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
            used.RemoveWhere(id => id < 0 || id >= LegacySkillSheet.ReservedIdStart);
            IEnumerable<int> granted = CampaignCurriculum.Default.Nodes.SelectMany(node => node.SkillIds);
            CollectionAssert.IsSubsetOf(used, CampaignSheetCheck.CodeSkillIds.Concat(granted).ToList());
            CollectionAssert.IsSubsetOf(CampaignSheetCheck.CodeSkillIds, used.ToList(), "Every listed id is used somewhere.");
        }

        [Test]
        public void NodeGrantingAStartingSkill_StillCompletes_AndItsSaveRestores()
        {
            string sheet = CsvTable.Write(HorizontalCutStarting());
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                var run = new CampaignRun();
                Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
                Assert.That(run.TryStartStage(1), Is.True);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(run.LastCompletedCurriculumNode.Id, Is.EqualTo("horizontal-cut"));
                Assert.That(run.OwnedSkills.Count(owned => owned.SkillId == 14), Is.EqualTo(1), "Already owned, so nothing new.");

                CampaignSave save = run.CaptureSave();
                var restored = new CampaignRun();
                Assert.That(restored.TryRestore(save, out string error), Is.True, error);
                Assert.That(restored.Curriculum.Completed, Is.EqualTo(new[] { "horizontal-cut" }));
                Assert.That(restored.OwnedSkills.Select(owned => owned.SkillId), Is.EquivalentTo(run.OwnedSkills.Select(owned => owned.SkillId)));
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        [Test]
        public void NodeWhoseSkillIsMissing_StopsTheBattleEndBeforeAnythingChanges()
        {
            List<string[]> rows = Rows();
            rows.Remove(RowOf(rows, 42));
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                var run = new CampaignRun();
                Assert.That(run.TrySelectCurriculumNode("horizontal-cut") && run.TryStartStage(1), Is.True);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(run.TrySelectCurriculumNode("diagonal-cut") && run.TryStartNextStage(), Is.True);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(run.TrySelectCurriculumNode("quick-draw") && run.TryStartNextStage(), Is.True);
                int currency = run.Currency;

                // The controller calls again every frame while it waits, so the second call must fail the same way.
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var error = Assert.Throws<InvalidOperationException>(() => run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory));
                    Assert.That(error.Message, Does.Contain("ID 42 기술이 없습니다"));
                    Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Battle));
                    Assert.That(run.LastOutcome, Is.Null);
                    Assert.That(run.Curriculum.Completed, Is.EqualTo(new[] { "horizontal-cut", "diagonal-cut" }));
                    Assert.That(run.Curriculum.Active.Id, Is.EqualTo("quick-draw"));
                    Assert.That(run.Curriculum.ActiveBattles, Is.Zero);
                    Assert.That(run.Currency, Is.EqualTo(currency));
                    Assert.That(run.IsStageCleared(3), Is.False);
                }
                Assert.That(run.TryAbandonBattle(), Is.True, "The run itself is still whole.");
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }
    }
}
