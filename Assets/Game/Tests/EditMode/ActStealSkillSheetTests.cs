using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class ActStealSkillSheetTests
    {
        private const string Phrase = "상대 ACT 8 이상이면 ACT 3 강탈";

        [TestCase("상대 ACT 8 이상이면 ACT 3 강탈", 8, 3)]
        [TestCase(" 상대ACT8이상이면ACT3강탈 ", 8, 3)]
        [TestCase("상대 ACT 1 이상이면 ACT 1 강탈", 1, 1)]
        [TestCase("상대 ACT 10 이상이면 ACT 10 강탈", 10, 10)]
        [TestCase("상대 ACT 01 이상이면 ACT 010 강탈", 1, 10)]
        public void UsePhrase_ReadsBothBounds_AndRoundTripsAsOneCanonicalUseEffect(string phrase, int threshold, int amount)
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.UseEffect, phrase);
            LegacySkillTable table = Parse(rows);
            LegacySkillEffect effect = table.Find(3).Effect;
            Assert.That((effect.ActStealThreshold, effect.ActStealAmount), Is.EqualTo((threshold, amount)));
            Assert.That(effect.HasActSteal && effect.HasUseEffect, Is.True);
            Assert.That(effect.HasReserveEffect || effect.HasBuff || effect.HasCycle || effect.HasOpponentCondition, Is.False);
            Assert.That(effect.ActGain, Is.Zero, "Stolen ACT belongs to the current pool, not next-turn recovery.");
            Assert.That(effect.Roles & LegacySkillRole.ActRecovery, Is.EqualTo(LegacySkillRole.None));
            string canonical = "상대 ACT " + threshold + " 이상이면 ACT " + amount + " 강탈";
            Assert.That(LegacySkillLabels.UseEffect(effect), Is.EqualTo(canonical));

            string written = LegacySkillSheet.Write(table);
            LegacySkillTable reread = LegacySkillSheet.Parse("ACT 강탈 round trip", written);
            Assert.That(LegacySkillSheet.Write(reread), Is.EqualTo(written));
            Assert.That(LegacySkillSheetTests.Dump(reread.Find(3)), Is.EqualTo(LegacySkillSheetTests.Dump(table.Find(3))));
        }

        [TestCase("상대 ACT 0 이상이면 ACT 3 강탈")]
        [TestCase("상대 ACT 11 이상이면 ACT 3 강탈")]
        [TestCase("상대 ACT 8 이상이면 ACT 0 강탈")]
        [TestCase("상대 ACT 8 이상이면 ACT 11 강탈")]
        [TestCase("상대 ACT -8 이상이면 ACT 3 강탈")]
        [TestCase("상대 ACT 8 이상이면 ACT -3 강탈")]
        [TestCase("상대 ACT 8.5 이상이면 ACT 3 강탈")]
        [TestCase("상대 ACT ８ 이상이면 ACT ３ 강탈")]
        [TestCase("상대 ACT 8 초과이면 ACT 3 강탈")]
        [TestCase("상대 ACT 8 이상 ACT 3 강탈")]
        [TestCase("상대 ACT 8 이상이면 ACT 3 강탈, 자동 회피")]
        [TestCase("ACT 강탈 3")]
        public void InvalidUsePhrase_RejectsTheRow_WithTheSupportedGrammarAndBounds(string phrase)
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.UseEffect, phrase);
            SkillSheetException error = Assert.Throws<SkillSheetException>(() => Parse(rows));
            Assert.That(error.Problems, Has.Count.EqualTo(1));
            Assert.That(error.Problems[0], Does.Contain("'사용 효과'"));
            Assert.That(error.Problems[0], Does.Contain("상대 ACT N 이상이면 ACT M 강탈(N과 M은 1~10의 정수)"));
        }

        [Test]
        public void BlankUseEffect_LeavesOldRowsUnchanged_AndChangesAreReportedInTheExistingColumn()
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.UseEffect, "");
            LegacySkillTable before = Parse(rows);
            LegacySkillEffect untouched = before.Find(3).Effect;
            Assert.That((untouched.ActStealThreshold, untouched.ActStealAmount, untouched.HasActSteal), Is.EqualTo((0, 0, false)));
            Assert.That(untouched.HasUseEffect, Is.False);
            Set(rows, 3, Column.UseEffect, Phrase);
            LegacySkillTable after = Parse(rows);
            LegacySkillSheetChanges changes = LegacySkillSheet.Describe(before, after);
            Assert.That((changes.Added, changes.Removed, changes.Changed), Is.EqualTo((0, 0, 1)));
            Assert.That(changes.Lines.Single(), Does.EndWith("사용 효과"));
            Assert.That(LegacySkillSheet.Headers, Has.No.Member("ACT 강탈"), "The existing use-effect column carries the keyword.");
        }

        [TestCase(500)]
        [TestCase(59)]
        public void EnemyAndTemporaryRows_KeepTheirExistingUseEffectRestrictions(int id)
        {
            List<string[]> rows = Rows();
            Set(rows, id, Column.UseEffect, Phrase);
            SkillSheetException error = Assert.Throws<SkillSheetException>(() => Parse(rows));
            Assert.That(error.Problems.First(), Does.Contain("'사용 효과'을(를) 쓸 수 없습니다"));
        }

        [Test]
        public void CounterAndReservationCells_RejectStealInsteadOfChangingItsActivationTiming()
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.ReserveEffect, Phrase);
            Assert.That(Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems.Single(), Does.Contain("'예약 효과'"));

            rows = Rows();
            rows.Add(KeywordSheet.Row(919, "E", "공격", 0, 4,
                (Column.CounterUsesPerTurn, "1"), (Column.UseEffect, Phrase)));
            Assert.That(Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems.Single(), Does.Contain("반격 기술에는 예약 효과, 사용 효과"));
        }

        [Test]
        public void PreviewKeepsTheConditionExplicit_UntilTheSkillIsUsedAgainstAnActOpponent()
        {
            const int skillId = 918;
            KeywordSheet.With(() =>
            {
                LegacyQueuedDuel duel = KeywordLinkTests.Duel(skillId);
                LegacySkill skill = LegacySkillDefinitions.Skill(skillId);
                Assert.That(LegacyKeywordPreview.LaneLine(duel, skill), Is.EqualTo("상대 ACT 8+ · 강탈 3"));
                Assert.That(LegacyKeywordPreview.Note(duel, skill),
                    Is.EqualTo("ACT 강탈 · 사용 시 상대 ACT가 8 이상이면 ACT 3 강탈"));
                KeywordLinkTests.Queue(duel, 2);
                LegacyKeywordMark planning = LegacyKeywordPreview.QueueMark(duel, true, 0, true);
                Assert.That((planning.Text, planning.Kind, planning.Spent), Is.EqualTo(("강탈 3?", LegacyKeywordMarkKind.ActSteal, false)));
                duel.Commit();
                duel.BeginNextSlot();
                LegacyKeywordMark used = LegacyKeywordPreview.QueueMark(duel, true, 0, false);
                Assert.That((used.Text, used.Kind, used.Spent), Is.EqualTo(("강탈 0", LegacyKeywordMarkKind.ActSteal, true)),
                    "Campaign enemies have no ACT to steal; neither the preview nor the mark promises a gain.");
            }, KeywordSheet.Row(skillId, "E", "공격", 2, 4, (Column.UseEffect, Phrase)));
        }

        private static List<string[]> Rows() => CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
            .Select(record => record.Fields.ToArray()).ToList();

        private static void Set(List<string[]> rows, int id, string column, string value)
            => rows.Single(row => row[0] == id.ToString())[LegacySkillSheet.Headers.ToList().IndexOf(column)] = value;

        private static LegacySkillTable Parse(List<string[]> rows) => LegacySkillSheet.Parse("ACT 강탈 test", CsvTable.Write(rows));
    }
}
