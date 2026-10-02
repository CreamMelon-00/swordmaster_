using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacySkillNamesTests
    {
        [Test]
        public void Format_ReplacesTokensWithSheetNamesAndFittingParticles()
        {
            Assert.That(LegacySkillNames.Token(12), Is.EqualTo("{기술:12}"));
            Assert.That(LegacySkillNames.Format("{기술:1}"), Is.EqualTo(LegacySkillDefinitions.Skill(1).Name));
            Assert.That(LegacySkillNames.Format("{기술:16} / {기술:12} / {기술:19} / {기술:42}"),
                Is.EqualTo("알티바호 / 플레슈 / 르프리즈 / 쿠페"), "The four renamed techniques.");
            Assert.That(LegacySkillNames.Format("Q로 {기술:1:을} 예약하세요."), Is.EqualTo("Q로 베기를 예약하세요."));
            Assert.That(LegacySkillNames.Format("{기술:1:를}"), Is.EqualTo("베기를"), "Either form of the pair is accepted.");
            Assert.That(LegacySkillNames.Format("{기술:42:과} 함께"), Is.EqualTo("쿠페와 함께"));
            Assert.That(LegacySkillNames.Format("{기술:32:와} 함께"), Is.EqualTo("유연함과 함께"));
            Assert.That(LegacySkillNames.Format("{기술:7:로} 받아낸다"), Is.EqualTo("막기로 받아낸다"));
            Assert.That(LegacySkillNames.Format("{기술:7}의 보상"), Is.EqualTo("막기의 보상"), "Fixed particles follow the token.");
            Assert.That(LegacySkillNames.Format("{기술:15:나} {기술:21:를} 마치면"), Is.EqualTo("사선베기나 급소 찌르기를 마치면"));
        }

        [Test]
        public void Format_LeavesPlainTextAndMalformedTokensAlone()
        {
            string plain = "베기를 예약하세요";
            Assert.That(LegacySkillNames.Format(plain), Is.SameAs(plain));
            Assert.That(LegacySkillNames.Format(null), Is.Null);
            Assert.That(LegacySkillNames.Format(string.Empty), Is.Empty);
            Assert.That(LegacySkillNames.HasTokens(plain), Is.False);
            Assert.That(LegacySkillNames.HasTokens("{기술:1}"), Is.True);
            foreach (string malformed in new[] { "{기술:}", "{기술:x}", "{기술:1", "{기술:1:}", "{기술:1:을", "{기술: 1}", "{1}" })
                Assert.That(LegacySkillNames.Format(malformed), Is.EqualTo(malformed), malformed);
            Assert.That(LegacySkillNames.Format("{기술:} {기술:1}"), Is.EqualTo("{기술:} 베기"), "A bad token does not hide the next one.");
        }

        [Test]
        public void Format_MarksUnknownIdsAndParticlesVisibly()
        {
            Assert.That(LegacySkillNames.Format("{기술:999}"), Is.EqualTo("{기술:999?}"));
            Assert.That(LegacySkillNames.Format("{기술:999:을} 예약"), Is.EqualTo("{기술:999?} 예약"));
            Assert.That(LegacySkillNames.Format("{기술:0}"), Is.EqualTo("{기술:0?}"));
            Assert.That(LegacySkillNames.Format("{기술:99999999999}"), Is.EqualTo("{기술:99999999999?}"));
            Assert.That(LegacySkillNames.Format("{기술:1:의}"), Is.EqualTo("{기술:1:의?}"), "의 never changes, so it goes after the token.");
            Assert.That(LegacySkillNames.Format("{기술:999?}"), Is.EqualTo("{기술:999?}"), "A marker stays as it is.");
        }

        [Test]
        public void CurriculumAndMissionCopy_HasNoTokenLeftAfterFormatting()
        {
            foreach (CurriculumNode node in CampaignCurriculum.Default.Nodes)
            {
                AssertFormatted(node.Title, node.Id);
                AssertFormatted(node.Description, node.Id);
                Assert.That(node.SkillIds.Count, Is.EqualTo(1), node.Id);
                Assert.That(node.Title, Is.EqualTo(LegacySkillDefinitions.Skill(node.SkillIds[0]).Name),
                    node.Id + " is titled with the technique it grants.");
            }
            var missions = PrologueMissions.All.Concat(LobbyMissions.All).ToList();
            Assert.That(missions.Count, Is.EqualTo(StoryMissions.Count));
            foreach (PrologueMission mission in missions)
            {
                string label = "mission " + mission.Number;
                AssertFormatted(mission.Title, label);
                AssertFormatted(mission.Chapter, label);
                AssertFormatted(mission.UnlockText, label);
                foreach (string objective in mission.Objectives) AssertFormatted(objective, label);
                foreach (MissionEnemy enemy in mission.Enemies) AssertFormatted(enemy.Name, label);
                MissionGuide guide = mission.CreateGuide();
                Assert.That(guide, Is.Not.Null, label);
                foreach (MissionGuideBeat beat in guide.Beats)
                {
                    AssertFormatted(beat.Title, label);
                    AssertFormatted(beat.Description, label);
                    AssertFormatted(beat.InputHint, label);
                }
            }
        }

        [Test]
        public void RenamedSheetRows_ReachTheCoachTheBriefingAndTheCurriculum()
        {
            CurriculumNode oneStroke = CampaignCurriculum.Default.Find("one-stroke");
            CurriculumNode quickDraw = CampaignCurriculum.Default.Find("quick-draw");
            PrologueMission mission = StoryMissions.Get(3);
            IReadOnlyList<MissionGuideBeat> beats = mission.CreateGuide().Beats;
            Assert.That(beats[1].Title, Is.EqualTo("베기를 예약하세요"));
            Assert.That(beats[2].Description, Is.EqualTo("베기가 열의 뒤로 돌아가고 막기가 올라왔습니다. 두 번째 순번의 내려치기를 막기로 받아내세요."));
            Assert.That(beats[2].Description, Is.SameAs(beats[2].Description), "The coach reads the same string every frame.");
            Assert.That(beats[5].Title, Is.EqualTo("막기의 보상"));
            Assert.That(mission.Objectives[0], Is.EqualTo("상대의 내려치기를 막기로 받아낸다"));
            Assert.That(quickDraw.Title, Is.EqualTo("쿠페"));
            Assert.That(oneStroke.Description, Is.EqualTo("위력이 크게 흔들리는 단 한 번의 일격입니다. 쿠페와 함께 고를 수 없습니다."));

            List<string[]> rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            int name = LegacySkillSheet.Headers.ToList().IndexOf(LegacySkillSheet.Column.Name);
            // A ㄹ-final name, a consonant-final name and an old name, so the particles must change too.
            rows.Single(row => row[0] == "1")[name] = "새 칼";
            rows.Single(row => row[0] == "7")[name] = "튕겨냄";
            rows.Single(row => row[0] == "42")[name] = "발검";
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                Assert.That(beats[1].Title, Is.EqualTo("새 칼을 예약하세요"));
                Assert.That(beats[2].Description,
                    Is.EqualTo("새 칼이 열의 뒤로 돌아가고 튕겨냄이 올라왔습니다. 두 번째 순번의 내려치기를 튕겨냄으로 받아내세요."));
                Assert.That(beats[5].Title, Is.EqualTo("튕겨냄의 보상"));
                Assert.That(mission.Objectives[0], Is.EqualTo("상대의 내려치기를 튕겨냄으로 받아낸다"));
                Assert.That(StoryMissions.Get(1).CreateGuide().Beats[1].Description,
                    Is.EqualTo("Q를 짧게 누르거나 카드를 클릭하면 새 칼이 ACT 1을 쓰고 첫 순서에 들어갑니다."));
                Assert.That(quickDraw.Title, Is.EqualTo("발검"));
                Assert.That(oneStroke.Description, Is.EqualTo("위력이 크게 흔들리는 단 한 번의 일격입니다. 발검과 함께 고를 수 없습니다."));
                Assert.That(beats[3].Title, Is.EqualTo("확정하세요"), "Copy without tokens is untouched.");
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
            Assert.That(beats[1].Title, Is.EqualTo("베기를 예약하세요"));
            Assert.That(quickDraw.Title, Is.EqualTo("쿠페"));
        }

        [Test]
        public void CurriculumNode_WithoutATitleUsesItsOnlySkillsName()
        {
            var node = new CurriculumNode("a", null, CurriculumBranch.Slash, 0f, 0, new[] { 12 },
                description: "{기술:19:와} 함께 고를 수 없습니다.");
            Assert.That(node.Title, Is.EqualTo("플레슈"));
            Assert.That(node.Description, Is.EqualTo("르프리즈와 함께 고를 수 없습니다."));
            Assert.That(new CurriculumNode("b", "{기술:7} 수련", CurriculumBranch.Guard, 0f, 0, new[] { 7, 8 }).Title,
                Is.EqualTo("막기 수련"), "A written title may use tokens too.");
            Assert.That(new CurriculumNode("c", null, CurriculumBranch.Guard, 0f, 0, new[] { 999 }).Title, Is.EqualTo("{기술:999?}"));
        }

        private static void AssertFormatted(string text, string label)
        {
            Assert.That(text, Is.Not.Null, label);
            Assert.That(text, Does.Not.Contain("{기술:"), label + ": " + text);
        }
    }
}
