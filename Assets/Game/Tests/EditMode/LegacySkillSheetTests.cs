using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacySkillSheetTests
    {
        // Sheet rows of the shipped table as written: the header is row 1, then ids in table order.
        private const int Row1 = 2, Row2 = 3, Row7 = 8, Row9 = 10, Row16 = 13, Row17 = 14, Row42 = 20, Row43 = 21;

        /// <summary>The shipped table as the writer lays it out, one string array per sheet row.</summary>
        private static List<string[]> Rows()
            => CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table)).Select(record => record.Fields.ToArray()).ToList();

        private static int Index(string header) => LegacySkillSheet.Headers.ToList().IndexOf(header);

        private static string[] RowOf(List<string[]> rows, int id) => rows.Single(row => row[0] == id.ToString());

        private static void Set(List<string[]> rows, int id, string header, string value) => RowOf(rows, id)[Index(header)] = value;

        private static string Csv(IEnumerable<string[]> rows) => CsvTable.Write(rows);

        private static LegacySkillTable Parse(IEnumerable<string[]> rows) => LegacySkillSheet.Parse("test", Csv(rows));

        private static IReadOnlyList<string> Problems(IEnumerable<string[]> rows)
            => Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems;

        private static string Problem(List<string[]> rows)
        {
            IReadOnlyList<string> problems = Problems(rows);
            Assert.That(problems.Count, Is.EqualTo(1), string.Join("\n", problems));
            return problems[0];
        }

        /// <summary>Every public value of a definition, nested objects included, so a new field joins the comparison.</summary>
        internal static string Dump(LegacySkillDefinition definition)
        {
            var parts = new List<string>();
            void Add(string label, object value)
            {
                if (value == null)
                {
                    parts.Add(label + "=null");
                    return;
                }
                foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
                    if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                        Add(label + "." + property.Name, property.GetValue(value));
                    else parts.Add(label + "." + property.Name + "=" + (property.GetValue(value) ?? "null"));
            }
            Add("definition", definition);
            return string.Join("\n", parts);
        }

        [Test]
        public void WriteThenParse_GivesBackTheShippedTable()
        {
            LegacySkillTable shipped = LegacySkillDefinitions.Table;
            string csv = LegacySkillSheet.Write(shipped);
            LegacySkillTable parsed = LegacySkillSheet.Parse("round trip", csv);
            Assert.That(parsed.All.Count, Is.EqualTo(shipped.All.Count));
            for (int index = 0; index < shipped.All.Count; index++)
            {
                Assert.That(Dump(parsed.All[index]), Is.EqualTo(Dump(shipped.All[index])), "row of " + shipped.All[index].Skill.Id);
                Assert.That(parsed.IsStarting(parsed.All[index]), Is.EqualTo(shipped.IsStarting(shipped.All[index])));
            }
            Assert.That(parsed.InitialSkills.Select(s => s.Id), Is.EqualTo(shipped.InitialSkills.Select(s => s.Id)));
            Assert.That(parsed.AcquisitionSkills.Select(s => s.Id), Is.EqualTo(shipped.AcquisitionSkills.Select(s => s.Id)));
            Assert.That(LegacySkillSheet.Write(parsed), Is.EqualTo(csv), "Writing is stable.");
            Assert.That(parsed.SheetId, Is.EqualTo("round trip"));
        }

        [Test]
        public void Parse_ReadsTheSameSheetWithWindowsLineEnds()
        {
            // git checks text out with CRLF on Windows, inside multi-line cells too.
            LegacySkillTable shipped = LegacySkillDefinitions.Table;
            LegacySkillTable crlf = LegacySkillSheet.Parse("crlf", LegacySkillSheet.Write(shipped).Replace("\n", "\r\n"));
            for (int index = 0; index < shipped.All.Count; index++)
                Assert.That(Dump(crlf.All[index]), Is.EqualTo(Dump(shipped.All[index])), "row of " + shipped.All[index].Skill.Id);
        }

        [Test]
        public void Write_HasAByteOrderMark_LfLines_AndTheHeadersInOrder()
        {
            string csv = LegacySkillSheet.Write(LegacySkillDefinitions.Table);
            Assert.That(csv[0], Is.EqualTo('\ufeff'), "Excel needs the mark to read Korean.");
            Assert.That(csv, Does.Not.Contain("\r"));
            Assert.That(csv.Substring(1, csv.IndexOf('\n') - 1), Is.EqualTo(string.Join(",", LegacySkillSheet.Headers)));
            Assert.That(LegacySkillSheet.Headers.Count, Is.EqualTo(37));
            Assert.That(LegacySkillSheet.Headers.Distinct().Count(), Is.EqualTo(37));
        }

        [Test]
        public void Write_SpellsOutBadgeSymbolsAndTones_AndLeavesDefaultsBlank()
        {
            List<string[]> rows = Rows();
            string[] slash = RowOf(rows, 1), sharp = RowOf(rows, 2), altibajo = RowOf(rows, 16);
            Assert.That(slash[Index(Column.Icon)], Is.Empty, "Icon = id stays blank.");
            Assert.That(slash[Index(Column.Animation)], Is.Empty, "The property's animation stays blank.");
            Assert.That(altibajo[Index(Column.Icon)], Is.EqualTo("12"));
            Assert.That(sharp[Index(Column.InfoMainSymbol)], Is.Empty, "No badges, no badge cells.");
            // 16's badges keep the default secondary tone, still written out.
            Assert.That(altibajo[Index(Column.InfoSecondaryTone)], Is.EqualTo("기본"));
            Assert.That(altibajo[Index(Column.InfoMainSymbol)], Is.EqualTo("편차"));
            Assert.That(altibajo[Index(Column.VariablePower)], Is.EqualTo("O"));
            Assert.That(RowOf(rows, 12)[Index(Column.ProtectionBuff)], Is.EqualTo("-50%"));
            Assert.That(RowOf(rows, 7)[Index(Column.OpponentProperty)], Is.EqualTo("타격"));
            Assert.That(RowOf(rows, 42)[Index(Column.OpponentKind)], Is.EqualTo("방어"));
        }

        [Test]
        public void Columns_AreFoundByHeader_InAnyOrder_AndOtherColumnsAreMemos()
        {
            List<string[]> rows = Rows();
            // Reverse the columns, then add a memo column and a blank-headed column in the middle.
            List<string[]> shuffled = rows.Select((row, index) =>
            {
                var cells = row.Reverse().ToList();
                cells.Insert(5, index == 0 ? "메모" : "아무 글");
                cells.Insert(9, index == 0 ? "" : "x");
                return cells.ToArray();
            }).ToList();
            LegacySkillTable table = Parse(shuffled);
            LegacySkillTable shipped = LegacySkillDefinitions.Table;
            for (int index = 0; index < shipped.All.Count; index++)
                Assert.That(Dump(table.All[index]), Is.EqualTo(Dump(shipped.All[index])));
        }

        [Test]
        public void Header_ListsEveryMissingColumnByItsExactName()
        {
            List<string[]> rows = Rows();
            rows[0][Index(Column.Name)] = " 이름 ";
            rows[0][Index(Column.Hits)] = "타 수";
            rows = rows.Select(row => row.Where((cell, index) =>
                index != Index(Column.Cost) && index != Index(Column.InfoDescription)).ToArray()).ToList();
            string problem = Problem(rows);
            Assert.That(problem, Does.StartWith("1행: "));
            Assert.That(problem, Does.Contain("'ACT', '타수', '배지 요약'"), "Trimmed headers count; '타 수' does not.");
            Assert.That(problem, Does.Contain("'" + string.Join("', '", LegacySkillSheet.Headers) + "'"), "The full list helps fix it.");
        }

        [Test]
        public void Header_RejectsADuplicateColumn()
        {
            List<string[]> rows = Rows().Select(row => row.Concat(new[] { row[Index(Column.Name)] }).ToArray()).ToList();
            Assert.That(Problem(rows), Is.EqualTo("1행: '이름' 열이 2번 있습니다. 같은 이름의 열은 하나만 둘 수 있습니다."));
            Assert.That(Problems(new List<string[]>()).Single(), Does.StartWith("1행: "), "An empty sheet has no header.");
        }

        [Test]
        public void EmptyAndCommentRows_AreSkipped_ButStillCountForRowNumbers()
        {
            List<string[]> rows = Rows();
            int width = rows[0].Length;
            rows.Insert(1, new string[width]);
            rows.Insert(2, new[] { "# 시작 기술" });
            rows.Insert(3, new[] { "  " });
            rows.Insert(4, Enumerable.Repeat(" ", width).ToArray());
            LegacySkillTable table = Parse(rows);
            Assert.That(table.All.Count, Is.EqualTo(21));
            Assert.That(table.All[0].Skill.Id, Is.EqualTo(1));

            Set(rows, 2, Column.Cost, "1.5");
            Assert.That(Problem(rows), Is.EqualTo($"{Row2 + 4}행 'ACT': '1.5'은(는) 0 이상의 정수여야 합니다."));
        }

        [Test]
        public void RowNumbers_CountAMultiLineCellAsOneRow()
        {
            List<string[]> rows = Rows();
            Assert.That(RowOf(rows, 1)[Index(Column.Detail)], Does.Contain("\n"), "베기's detail spans two lines.");
            Set(rows, 2, Column.Hits, "0");
            Assert.That(Problem(rows), Is.EqualTo($"{Row2}행 '타수': '0'은(는) 1 이상의 정수여야 합니다."));
        }

        [TestCase(Column.Id, "abc", "1 이상의 정수")]
        [TestCase(Column.Id, "0", "1 이상의 정수")]
        [TestCase(Column.Id, "", "비어 있습니다")]
        [TestCase(Column.Name, " ", "비어 있습니다")]
        [TestCase(Column.Group, "기본", "시작, 획득 중 하나")]
        [TestCase(Column.Lane, "R", "Q, W, E 중 하나")]
        [TestCase(Column.Kind, "베기", "공격, 방어 중 하나")]
        [TestCase(Column.Kind, "대기", "공용 행동")]
        [TestCase(Column.Kind, "Wait", "공용 행동")]
        [TestCase(Column.Property, "없음", "참격, 타격, 관통, 방어 중 하나")]
        [TestCase(Column.Property, "불", "참격, 타격, 관통, 방어 중 하나")]
        [TestCase(Column.Cost, "-1", "0 이상의 정수")]
        [TestCase(Column.MinPower, "", "비어 있습니다")]
        [TestCase(Column.MaxPower, "열", "0 이상의 정수")]
        [TestCase(Column.Icon, "0", "1 이상의 정수")]
        [TestCase(Column.ActGain, "-2", "0 이상의 정수")]
        [TestCase(Column.BrokenTargetDamage, "-1%", "0 이상의 정수")]
        [TestCase(Column.PowerBuff, "10.5%", "정수여야 합니다 (끝에 %를 붙여도 됩니다)")]
        [TestCase(Column.BuffSlots, "-1", "0 이상의 정수")]
        [TestCase(Column.ResistanceRecovery, "-10%", "0 이상의 정수")]
        [TestCase(Column.ResistanceReduction, "많이", "0 이상의 정수")]
        [TestCase(Column.OpponentProperty, "아무거나", "비워 두거나 참격, 타격, 관통, 방어 중 하나")]
        [TestCase(Column.OpponentKind, "대기", "공용 행동")]
        [TestCase(Column.HighPower, "maybe", "O, ○, TRUE, 1, 예, Y")]
        [TestCase(Column.ShortLabel, "", "비어 있습니다")]
        [TestCase(Column.Detail, "", "비어 있습니다")]
        [TestCase(Column.InfoMainSymbol, "별", "비워 두거나 ACT, 검, 방어, 타수, 회복, 후속, 감소, 편차 중 하나")]
        [TestCase(Column.InfoSecondaryTone, "빨강", "비워 두거나 기본, 회복, 후속, 감소, 고화력, 연타, 편차, 방어 중 하나")]
        public void BadCell_NamesItsRowColumnAndValue(string header, string value, string expected)
        {
            List<string[]> rows = Rows();
            // 막기 (row 8) is a guard with badges and an opponent condition.
            Set(rows, 7, header, value);
            IReadOnlyList<string> problems = Problems(rows);
            string problem = problems.First();
            string prefix = $"{Row7}행 '{header}': ";
            Assert.That(problem, Does.StartWith(prefix), string.Join("\n", problems));
            if (value.Trim().Length > 0) Assert.That(problem, Does.Contain("'" + value + "'"));
            Assert.That(problem, Does.Contain(expected));
        }

        [Test]
        public void Cells_AcceptEnglishNames_Percent_BooleanWords_AndTrimSpaces()
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.Kind, "attack");
            Set(rows, 3, Column.Property, "  PENETRATE ");
            Set(rows, 3, Column.Lane, "w");
            Set(rows, 9, Column.PowerBuff, "20");
            Set(rows, 9, Column.InfoMainSymbol, "followup");
            Set(rows, 9, Column.InfoMainTone, "Followup");
            Set(rows, 8, Column.ProtectionBuff, " 25 % ");
            Set(rows, 2, Column.HighPower, "false");
            Set(rows, 4, Column.HighPower, "○");
            Set(rows, 6, Column.HighPower, "아니오");
            Set(rows, 16, Column.VariablePower, "y");
            Set(rows, 14, Column.HighPower, "X");
            Set(rows, 15, Column.HighPower, "아니오");
            Set(rows, 17, Column.HighPower, "0");
            Set(rows, 10, Column.HighPower, "false");
            LegacySkillTable table = Parse(rows);
            LegacySkillTable shipped = LegacySkillDefinitions.Table;
            for (int index = 0; index < shipped.All.Count; index++)
                Assert.That(Dump(table.All[index]), Is.EqualTo(Dump(shipped.All[index])), "row of " + shipped.All[index].Skill.Id);
        }

        [Test]
        public void BlankOptionalCells_ReadTheirDefaults()
        {
            List<string[]> rows = Rows();
            Set(rows, 5, Column.InfoMainSymbol, "");
            Set(rows, 5, Column.InfoMainTone, "");
            Set(rows, 5, Column.InfoSecondarySymbol, "");
            Set(rows, 5, Column.InfoSecondaryTone, "");
            Set(rows, 5, Column.InfoSecondary, "");
            Set(rows, 5, Column.Purpose, "");
            Set(rows, 5, Column.Description, "");
            LegacySkillDefinition trick = Parse(rows).Find(5);
            Assert.That(trick.Text.Info.MainSymbol, Is.EqualTo(LegacySkillSymbol.Act));
            Assert.That(trick.Text.Info.MainTone, Is.EqualTo(LegacySkillTone.Neutral));
            Assert.That(trick.Text.Info.SecondarySymbol, Is.EqualTo(LegacySkillSymbol.Hits));
            Assert.That(trick.Text.Info.SecondaryTone, Is.EqualTo(LegacySkillTone.Neutral));
            Assert.That(trick.Text.Info.Secondary, Is.Empty);
            Assert.That(trick.Text.Info.EnemyMain, Is.Null);
            Assert.That(trick.Text.Purpose, Is.Null);
            Assert.That(trick.Skill.Description, Is.Empty);
            Assert.That(trick.Skill.IconId, Is.EqualTo(5));
            Assert.That(trick.Skill.AnimationName, Is.EqualTo("Hit"));
            Assert.That(Parse(rows).Find(7).Skill.AnimationName, Is.EqualTo("Defense"));
            Assert.That(Parse(rows).Find(2).Text.Info, Is.Null, "A row with no badge cell has no badges.");
            Assert.That(Parse(rows).Find(2).Effect, Is.SameAs(LegacySkillEffect.None));
        }

        [Test]
        public void SkillRules_PowerRangeAndBuffs()
        {
            List<string[]> rows = Rows();
            Set(rows, 1, Column.MinPower, "9");
            Assert.That(Problem(rows), Is.EqualTo($"{Row1}행 (ID 1) '최대 위력'(5)이(가) '최소 위력'(9)보다 작습니다."));

            rows = Rows();
            Set(rows, 9, Column.BuffSlots, "");
            Assert.That(Problem(rows), Is.EqualTo($"{Row9}행 (ID 9) '위력 버프'나 '보호 버프'를 쓰려면 '버프 칸'이(가) 1 이상이어야 합니다."));

            rows = Rows();
            Set(rows, 9, Column.PowerBuff, "0%");
            Assert.That(Problem(rows), Is.EqualTo($"{Row9}행 (ID 9) '버프 칸'이(가) 있으면 '위력 버프'나 '보호 버프'도 있어야 합니다."));
        }

        [Test]
        public void SkillRules_OpponentConditions()
        {
            List<string[]> rows = Rows();
            Set(rows, 7, Column.OpponentKind, "공격");
            Assert.That(Problem(rows), Is.EqualTo($"{Row7}행 (ID 7) 상대 조건은 '상대 속성 조건'과(와) '상대 종류 조건' 중 하나만 쓸 수 있습니다."));

            rows = Rows();
            Set(rows, 7, Column.ResistanceRecovery, "10%");
            Assert.That(Problem(rows), Is.EqualTo($"{Row7}행 (ID 7) '저항 회복'은(는) 그 자체가 조건이라 상대 조건과 함께 쓸 수 없습니다."));

            rows = Rows();
            Set(rows, 16, Column.OpponentProperty, "참격");
            Assert.That(Problem(rows), Is.EqualTo($"{Row16}행 (ID 16) 상대 조건은 ACT 회복이나 저항 감소가 있어야 합니다."));

            rows = Rows();
            Set(rows, 42, Column.ActGain, "");
            Parse(rows);
            Set(rows, 42, Column.ResistanceReduction, "");
            Assert.That(Problem(rows), Is.EqualTo($"{Row42}행 (ID 42) 상대 조건은 ACT 회복이나 저항 감소가 있어야 합니다."),
                "Either amount is enough; neither is not.");
        }

        [Test]
        public void SkillRules_PowerTagsBadgesAndDuplicateIds()
        {
            List<string[]> rows = Rows();
            Set(rows, 17, Column.HighPower, "O");
            Assert.That(Problem(rows), Is.EqualTo($"{Row17}행 (ID 17) '고화력'과(와) '위력 편차'은(는) 공격에만 붙일 수 있습니다."));

            rows = Rows();
            Set(rows, 17, Column.InfoSecondaryTone, "방어");
            Assert.That(Problem(rows), Is.EqualTo($"{Row17}행 (ID 17) 배지 칸을 하나라도 쓰면 '배지 주 문구'과(와) '배지 요약'을(를) 모두 적어야 합니다."));

            rows = Rows();
            Set(rows, 7, Column.InfoDescription, "");
            Assert.That(Problem(rows), Does.StartWith($"{Row7}행 (ID 7) 배지 칸을"));

            rows = Rows();
            Set(rows, 42, Column.Id, "16");
            Assert.That(Problem(rows), Is.EqualTo($"{Row42}행 (ID 16) ID가 {Row16}행과 겹칩니다. ID는 기술마다 달라야 합니다."));
        }

        [Test]
        public void BrokenTargetBonus_RequiresAnAttackAndRoundTripsThroughTheSheet()
        {
            List<string[]> rows = Rows();
            Assert.That(RowOf(rows, 44)[Index(Column.BrokenTargetDamage)], Is.EqualTo("25%"));
            Assert.That(Parse(rows).Find(44).Effect.BrokenTargetDamagePercent, Is.EqualTo(25));

            Set(rows, 43, Column.BrokenTargetDamage, "25%");
            Assert.That(Problem(rows), Is.EqualTo($"{Row43}행 (ID 43) '붕괴 대상 추가 피해'은(는) 공격 기술에만 쓸 수 있습니다."));
        }

        [Test]
        public void Ids_FromTheReservedRangeAreRefused_SoPracticeSkillsKeepTheirOwnDefinitions()
        {
            List<string[]> rows = Rows();
            Set(rows, 42, Column.Id, "1001");
            Assert.That(Problem(rows), Is.EqualTo($"{Row42}행 'ID': '1001'은(는) 쓸 수 없습니다. 1000 이상은 코드에 있는 서막 연습 기술" +
                "(연습 베기 등)이 쓰는 번호입니다. 1~999 중 쓰지 않는 번호를 적으세요."));
            Set(rows, 1001, Column.Id, "999");
            Assert.That(Parse(rows).Find(999).Skill.Name, Is.EqualTo("쿠페"), "Every lower id is free.");

            var practice = PrologueMissions.All.SelectMany(mission => mission.EnemySkills)
                .Where(skill => LegacySkillDefinitions.Find(skill) == null && !skill.IsWait).ToList();
            Assert.That(practice.Select(skill => skill.Name).Distinct(), Is.EquivalentTo(new[] { "연습 베기", "연습 내려치기", "연습 막기" }));
            foreach (LegacySkill skill in practice)
                Assert.That(skill.Id, Is.GreaterThanOrEqualTo(LegacySkillSheet.ReservedIdStart), skill.Name);
        }

        [Test]
        public void Header_SavedInTheKoreanCodePage_SaysTheFileIsNotUtf8()
        {
            // '이름' as Excel's plain 'CSV(쉼표로 분리)' saves it on a Korean PC (CP949), read as UTF-8.
            string garbled = Encoding.UTF8.GetString(new byte[] { 0xC0, 0xCC, 0xB8, 0xA7 });
            Assert.That(garbled.IndexOf((char)0xFFFD), Is.GreaterThanOrEqualTo(0));
            List<string[]> rows = Rows();
            rows[0] = rows[0].Select(header => header.Any(c => c >= '가' && c <= '힣') ? garbled : header).ToArray();
            Assert.That(Problem(rows), Is.EqualTo("1행: 파일이 UTF-8이 아닙니다(한글이 깨져 읽힙니다). " +
                "Excel에서 파일 → 다른 이름으로 저장 → CSV UTF-8(쉼표로 분리)로 다시 저장하세요."),
                "Not the missing-column list: 'ID' and 'ACT' still read, the Korean names cannot.");
        }

        [Test]
        public void BooleanCell_ListsBothSpellingsWithAParticleThatFitsAnyList()
        {
            List<string[]> rows = Rows();
            Set(rows, 7, Column.HighPower, "maybe");
            Assert.That(Problems(rows).First(), Is.EqualTo($"{Row7}행 '고화력': 'maybe'은(는) 예/아니오로 읽을 수 없습니다. " +
                "예는 O, ○, TRUE, 1, 예, Y 중 하나, 아니오는 빈칸이나 X, FALSE, 0, 아니오, N 중 하나로 적으세요."));
        }

        [Test]
        public void TableRules_ThreeStartingSkillsPerLane_AndSomeToAcquire()
        {
            List<string[]> rows = Rows();
            Set(rows, 7, Column.Lane, "W");
            Assert.That(Problem(rows), Is.EqualTo("'구분'이(가) '시작'인 기술은 열마다 3개여야 합니다 (지금 Q 2개, W 4개, E 3개)."));

            rows = Rows();
            Set(rows, 14, Column.Group, "시작");
            Assert.That(Problem(rows), Does.Contain("지금 Q 4개, W 3개, E 3개"));

            rows = Rows();
            foreach (string[] row in rows.Skip(1)) if (row[Index(Column.Group)] == "획득") row[Index(Column.Group)] = "";
            Assert.That(Problems(rows).Count(problem => problem.Contains("'구분'")), Is.EqualTo(12),
                "Blank groups are cell problems; the table rules wait until every row's group reads.");

            rows = Rows().Take(10).ToList();
            Assert.That(Problem(rows), Does.Contain("'획득'인 기술이 하나도 없습니다"));
        }

        [Test]
        public void Problems_AreAllCollected_AndTheMessageNamesTheSheet()
        {
            List<string[]> rows = Rows();
            Set(rows, 1, Column.Cost, "x");
            Set(rows, 1, Column.Property, "y");
            Set(rows, 2, Column.MinPower, "99");
            Set(rows, 16, Column.Lane, "");
            var error = Assert.Throws<SkillSheetException>(() => LegacySkillSheet.Parse("Skills/test.csv", Csv(rows)));
            Assert.That(error.SheetId, Is.EqualTo("Skills/test.csv"));
            Assert.That(error.Problems, Is.EqualTo(new[]
            {
                $"{Row1}행 '속성': 'y'은(는) 쓸 수 없습니다. 참격, 타격, 관통, 방어 중 하나를 적으세요.",
                $"{Row1}행 'ACT': 'x'은(는) 0 이상의 정수여야 합니다.",
                $"{Row2}행 (ID 2) '최대 위력'(9)이(가) '최소 위력'(99)보다 작습니다.",
                $"{Row16}행 '열': 칸이 비어 있습니다. Q, W, E 중 하나를 적으세요.",
            }));
            Assert.That(error.Message, Does.StartWith("기술 시트 'Skills/test.csv'"));
            foreach (string problem in error.Problems) Assert.That(error.Message, Does.Contain(problem));
            Assert.That(error, Is.InstanceOf<FormatException>());
        }

        [Test]
        public void CsvSyntaxErrors_BecomeSheetProblemsWithTheRow()
        {
            string csv = LegacySkillSheet.Write(LegacySkillDefinitions.Table) + "99,\"열린 따옴표\n";
            var error = Assert.Throws<SkillSheetException>(() => LegacySkillSheet.Parse("test", csv));
            Assert.That(error.Problems.Single(), Does.StartWith("23행: "));
        }

        [Test]
        public void Describe_NamesAddedRemovedAndChangedSkillsByColumn()
        {
            LegacySkillTable current = LegacySkillDefinitions.Table;
            List<string[]> oldRows = Rows();
            Set(oldRows, 16, Column.Name, "일도양단");
            Set(oldRows, 16, Column.Cost, "4");
            oldRows.Remove(RowOf(oldRows, 12));
            string[] retired = RowOf(oldRows, 14).ToArray();
            retired[Index(Column.Id)] = "99";
            retired[Index(Column.Name)] = "옛 기술";
            oldRows.Add(retired);
            LegacySkillTable before = Parse(oldRows);

            LegacySkillSheetChanges changes = LegacySkillSheet.Describe(before, current);
            Assert.That(changes.Lines, Is.EqualTo(new[] { "변경: 16 알티바호 — 이름, ACT", "추가: 12 플레슈", "삭제: 99 옛 기술" }));
            Assert.That(changes.Added, Is.EqualTo(1));
            Assert.That(changes.Removed, Is.EqualTo(1));
            Assert.That(changes.Changed, Is.EqualTo(1));
            Assert.That(changes.Reordered, Is.False, "12 is new, so the others keep their order.");
            Assert.That(changes.HasChanges, Is.True);
            Assert.That(changes.Summary, Is.EqualTo("추가 1 · 삭제 1 · 변경 1 (기술 21개)"));
        }

        [Test]
        public void Describe_ReportsNoChange_ARowOrderChange_AndEverythingNewWithoutACurrentSheet()
        {
            LegacySkillTable current = LegacySkillDefinitions.Table;
            LegacySkillSheetChanges same = LegacySkillSheet.Describe(current, Parse(Rows()));
            Assert.That(same.HasChanges, Is.False);
            Assert.That(same.Lines, Is.Empty);
            Assert.That(same.Summary, Is.EqualTo("바뀐 내용이 없습니다 (기술 21개)."));

            List<string[]> rows = Rows();
            string[] guard = RowOf(rows, 7);
            rows.Remove(guard);
            rows.Insert(1, guard);
            LegacySkillSheetChanges moved = LegacySkillSheet.Describe(current, Parse(rows));
            Assert.That(moved.Reordered && moved.HasChanges, Is.True);
            Assert.That(moved.Lines.Single(), Does.StartWith("순서: "));
            Assert.That(moved.Summary, Does.Contain("순서 변경"));

            LegacySkillSheetChanges fresh = LegacySkillSheet.Describe(null, current);
            Assert.That(fresh.Added, Is.EqualTo(21));
            Assert.That(fresh.Lines[0], Is.EqualTo("추가: 1 베기"));
            Assert.That(fresh.Reordered, Is.False);
        }
    }
}
