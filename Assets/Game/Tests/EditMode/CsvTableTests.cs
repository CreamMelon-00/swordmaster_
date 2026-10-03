using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    public sealed class CsvTableTests
    {
        private static string[][] Fields(string text) => CsvTable.Read(text).Select(record => record.Fields.ToArray()).ToArray();

        [Test]
        public void Read_SplitsRecordsAndFields_NumberingRowsFromOne()
        {
            IReadOnlyList<CsvRecord> records = CsvTable.Read("ID,이름,ACT\n1,베기,1\n2,연속 베기,1\n");
            Assert.That(records.Count, Is.EqualTo(3), "The last line break does not open a row.");
            Assert.That(records.Select(record => record.RowNumber), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(records[0].Fields, Is.EqualTo(new[] { "ID", "이름", "ACT" }));
            Assert.That(records[2].Fields, Is.EqualTo(new[] { "2", "연속 베기", "1" }));
            Assert.That(records[1][7], Is.Empty, "A short row reads empty past its end.");
            Assert.That(CsvTable.Read(string.Empty), Is.Empty);
            Assert.That(Fields("a,"), Is.EqualTo(new[] { new[] { "a", "" } }), "A trailing comma is one more empty field.");
        }

        [Test]
        public void Read_QuotedFieldsKeepCommasQuotesAndLineBreaks_AndStillCountAsOneRow()
        {
            IReadOnlyList<CsvRecord> records = CsvTable.Read("\"a,b\",\"say \"\"hi\"\"\",\"line 1\nline 2\",\"\"\nnext,row\n");
            Assert.That(records.Count, Is.EqualTo(2));
            Assert.That(records[0].Fields, Is.EqualTo(new[] { "a,b", "say \"hi\"", "line 1\nline 2", "" }));
            Assert.That(records[1].RowNumber, Is.EqualTo(2), "A cell with a line break is still one spreadsheet row.");
            Assert.That(records[1].Fields, Is.EqualTo(new[] { "next", "row" }));
            Assert.That(Fields("ab\"c,d"), Is.EqualTo(new[] { new[] { "ab\"c", "d" } }), "A quote inside a plain field is a character.");
        }

        [Test]
        public void Read_AcceptsCrLfLfAndCr_AndTurnsBreaksInsideCellsIntoLf()
        {
            Assert.That(Fields("a\r\nb\nc\rd"), Is.EqualTo(new[] { new[] { "a" }, new[] { "b" }, new[] { "c" }, new[] { "d" } }));
            Assert.That(Fields("\"x\r\ny\rz\"\r\n"), Is.EqualTo(new[] { new[] { "x\ny\nz" } }));
            IReadOnlyList<CsvRecord> records = CsvTable.Read("a\r\n\r\nb\r\n");
            Assert.That(records.Count, Is.EqualTo(3), "An empty line is an empty row, so later rows keep their numbers.");
            Assert.That(records[1].Fields, Is.EqualTo(new[] { "" }));
            Assert.That(records[2].RowNumber, Is.EqualTo(3));
        }

        [Test]
        public void Read_DropsALeadingByteOrderMark()
        {
            Assert.That(Fields("\ufeffID,이름\n"), Is.EqualTo(new[] { new[] { "ID", "이름" } }));
            Assert.That(Fields("\ufeff\"ID\"\n"), Is.EqualTo(new[] { new[] { "ID" } }), "A quoted first cell still opens its quote.");
        }

        [Test]
        public void Read_UnterminatedQuote_NamesTheRowWhereTheCellStarted()
        {
            var error = Assert.Throws<CsvFormatException>(() => CsvTable.Read("a\nb\nc,\"open\nmore\nlines"));
            Assert.That(error.RowNumber, Is.EqualTo(3));
            Assert.That(error.Message, Does.StartWith("3행: "));
            Assert.That(error.Message, Does.Contain("닫히지 않았습니다"));
        }

        [Test]
        public void Read_TextAfterAClosingQuote_IsAnError()
        {
            var error = Assert.Throws<CsvFormatException>(() => CsvTable.Read("ok\n\"done\"x,y"));
            Assert.That(error.RowNumber, Is.EqualTo(2));
        }

        [Test]
        public void Write_QuotesOnlyWhatNeedsIt_EndsEveryRowWithLf_AndReadsBack()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "plain", "a,b", "say \"hi\"", "two\nlines", "", null, " spaced " },
                new[] { "다음" },
            };
            string text = CsvTable.Write(rows);
            Assert.That(text, Is.EqualTo("plain,\"a,b\",\"say \"\"hi\"\"\",\"two\nlines\",,, spaced \n다음\n"));
            Assert.That(Fields(text), Is.EqualTo(new[]
            {
                new[] { "plain", "a,b", "say \"hi\"", "two\nlines", "", "", " spaced " },
                new[] { "다음" },
            }));
            Assert.That(CsvTable.Write(new[] { new[] { "carriage\rreturn" } }), Is.EqualTo("\"carriage\rreturn\"\n"));
        }

        [Test]
        public void SheetFile_ReadsAFileAnotherProgramHoldsOpenForWriting_WithoutTheByteOrderMark()
        {
            string path = Path.Combine(Path.GetTempPath(), "sheet-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                File.WriteAllText(path, "ID,이름\n1,베기\n", new UTF8Encoding(true));
                // How Excel keeps an open workbook: writing, and sharing it for reading only.
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                    Assert.That(SheetFile.ReadAllText(path), Is.EqualTo("ID,이름\n1,베기\n"));
                File.WriteAllText(path, "ID\n", new UTF8Encoding(false));
                Assert.That(SheetFile.ReadAllText(path), Is.EqualTo("ID\n"));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
