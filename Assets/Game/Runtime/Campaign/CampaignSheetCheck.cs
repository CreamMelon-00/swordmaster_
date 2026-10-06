using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>Checks a skill sheet against what the game code expects of it, beyond the sheet's own rules
    /// (<see cref="LegacySkillSheet.Parse"/>): the techniques stages and the curriculum grant and the ids the code names. Such a
    /// sheet still loads, but a missing technique stops the game where it is first needed and a node granting a
    /// starting technique grants nothing, so the editor window (which then refuses a download), the import check and
    /// the Play-start check report these next to the sheet's own problems. Only the given table is read, never the
    /// loaded one.</summary>
    public static class CampaignSheetCheck
    {
        /// <summary>The technique ids code names besides the curriculum's: the stage enemies' basic cycle (1-6) and
        /// rhythms (1-9), the enemy counters (4, 7), the 서막 missions (1, 2, 5, 7, and 500 라우다레 in 이아's 수훈 script),
        /// the 수련 missions' lanes (1-9), the default duel (1-6) and the {기술:ID} tokens in mission copy (1, 2, 3, 5, 7).
        /// Ids from <see cref="LegacySkillSheet.ReservedIdStart"/> up are the practice skills', which the sheet already refuses.</summary>
        public static IReadOnlyList<int> CodeSkillIds { get; } = Array.AsReadOnly(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 500 });

        /// <summary>What the game cannot run with, one Korean line each; empty when the sheet fits the code.
        /// <list type="bullet">
        /// <item>A technique a <see cref="CampaignCurriculum.Default"/> node grants is missing (completing the node would
        /// stop the battle's end), is a 시작 row (the node would grant nothing; the curriculum gives 획득 rows) or is an
        /// 적 row (the player would own an enemy-only technique).</item>
        /// <item>A first-clear stage reward is missing or is a 시작 or 적 row.</item>
        /// <item>An id of <see cref="CodeSkillIds"/> is missing.</item>
        /// </list></summary>
        public static IReadOnlyList<string> Problems(LegacySkillTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var problems = new List<string>();
            var reported = new HashSet<int>();
            foreach (CurriculumNode node in CampaignCurriculum.Default.Nodes)
                foreach (int id in node.SkillIds)
                {
                    LegacySkillDefinition definition = table.Find(id);
                    if (definition == null)
                        problems.Add($"ID {id} 기술이 시트에 없습니다. 커리큘럼 과정 '{node.Id}'이(가) 이 기술을 주므로 " +
                            "그 과정을 마치는 전투가 끝날 때 게임이 멈춥니다. ID를 바꾸거나 행을 지우지 마세요.");
                    else if (table.IsStarting(definition))
                        problems.Add($"ID {id} {definition.Skill.Name}의 '{Column.Group}'이(가) '{LegacySkillSheet.StartingGroup}'입니다. " +
                            $"커리큘럼 과정 '{node.Id}'이(가) 주는 기술이라 '{LegacySkillSheet.AcquisitionGroup}'이어야 합니다 " +
                            "(시작 기술이면 그 과정을 마쳐도 새로 얻는 기술이 없습니다).");
                    else if (table.IsEnemy(definition))
                        problems.Add($"ID {id} {definition.Skill.Name}의 '{Column.Group}'이(가) '{LegacySkillSheet.EnemyGroup}'입니다. " +
                            $"커리큘럼 과정 '{node.Id}'이(가) 주는 기술이라 '{LegacySkillSheet.AcquisitionGroup}'이어야 합니다 " +
                            "(적 기술은 플레이어가 얻거나 편성하지 않습니다).");
                    reported.Add(id);
                }
            foreach (CampaignStage stage in CampaignRun.StageDefinitions)
            {
                int id = stage.FirstClearSkillId;
                if (id == 0) continue;
                LegacySkillDefinition definition = table.Find(id);
                if (definition == null)
                    problems.Add($"ID {id} 기술이 시트에 없습니다. 스테이지 {stage.Number} 첫 클리어 보상이므로 이 기술을 지급할 때 게임이 멈춥니다. ID를 바꾸거나 행을 지우지 마세요.");
                else if (table.IsStarting(definition))
                    problems.Add($"ID {id} {definition.Skill.Name}의 '{Column.Group}'이(가) '{LegacySkillSheet.StartingGroup}'입니다. " +
                        $"스테이지 {stage.Number} 첫 클리어 보상이라 '{LegacySkillSheet.AcquisitionGroup}'이어야 합니다.");
                else if (table.IsEnemy(definition))
                    problems.Add($"ID {id} {definition.Skill.Name}의 '{Column.Group}'이(가) '{LegacySkillSheet.EnemyGroup}'입니다. " +
                        $"스테이지 {stage.Number} 첫 클리어 보상이라 '{LegacySkillSheet.AcquisitionGroup}'이어야 합니다.");
                reported.Add(id);
            }
            foreach (int id in CodeSkillIds)
                if (!reported.Contains(id) && table.Find(id) == null)
                    problems.Add($"ID {id} 기술이 시트에 없습니다. 코드가 이 ID를 씁니다(스테이지 적의 행동·반격기, 서막·수련 임무와 " +
                        "그 안내 문구). 그 기술을 처음 쓰는 곳에서 게임이 멈추니 ID를 바꾸거나 행을 지우지 마세요.");
            return problems.AsReadOnly();
        }

        /// <summary>What still runs but is probably unintended, one Korean line each: a 획득 row no stage or curriculum node grants,
        /// which the game never hands out. A programmer adds the node after the designer adds the row, so this never blocks.</summary>
        public static IReadOnlyList<string> Warnings(LegacySkillTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var warnings = new List<string>();
            foreach (LegacySkill skill in table.AcquisitionSkills)
                if (CampaignCurriculum.Default.FindGranting(skill.Id) == null &&
                    !IsStageReward(skill.Id))
                    warnings.Add($"ID {skill.Id} {skill.Name}: '{Column.Group}'이(가) '{LegacySkillSheet.AcquisitionGroup}'이지만 " +
                        "이 기술을 주는 커리큘럼 과정이 없어 게임에서 얻을 수 없습니다. 프로그래머가 CampaignCurriculum.cs에 과정을 넣어야 합니다.");
            return warnings.AsReadOnly();
        }

        private static bool IsStageReward(int skillId)
        {
            foreach (CampaignStage stage in CampaignRun.StageDefinitions)
                if (stage.FirstClearSkillId == skillId) return true;
            return false;
        }

        /// <summary><see cref="Problems"/> as one Console message, headed like a <see cref="SkillSheetException"/>.</summary>
        public static string ProblemMessage(string sheetId, IReadOnlyList<string> problems)
            => $"기술 시트 '{sheetId}': 게임 코드와 맞지 않는 곳 {problems.Count}개\n" + string.Join("\n", problems);

        /// <summary><see cref="Warnings"/> as one Console message.</summary>
        public static string WarningMessage(string sheetId, IReadOnlyList<string> warnings)
            => $"기술 시트 '{sheetId}': 경고 {warnings.Count}개\n" + string.Join("\n", warnings);
    }
}
