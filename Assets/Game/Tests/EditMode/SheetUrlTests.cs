using System;
using NUnit.Framework;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    public sealed class SheetUrlTests
    {
        private const string Id = "1AbC-d_E2fGh";
        private const string Export = "https://docs.google.com/spreadsheets/d/" + Id + "/export?format=csv&gid=";

        private static string Csv(string input)
        {
            Assert.That(SheetUrl.TryGetCsvUrl(input, out string url, out string error), Is.True, error);
            Assert.That(error, Is.Null);
            return url;
        }

        private static string Error(string input)
        {
            Assert.That(SheetUrl.TryGetCsvUrl(input, out string url, out string error), Is.False, url);
            Assert.That(url, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            return error;
        }

        [TestCase("https://docs.google.com/spreadsheets/d/" + Id + "/edit#gid=123", "123")]
        [TestCase("https://docs.google.com/spreadsheets/d/" + Id + "/edit?gid=7#gid=7", "7")]
        [TestCase("https://docs.google.com/spreadsheets/d/" + Id + "/edit?usp=sharing&gid=42", "42")]
        [TestCase("https://docs.google.com/spreadsheets/d/" + Id + "/edit?usp=sharing", "0")]
        [TestCase("https://docs.google.com/spreadsheets/d/" + Id + "/edit", "0")]
        [TestCase("https://docs.google.com/spreadsheets/d/" + Id, "0")]
        [TestCase("  docs.google.com/spreadsheets/d/" + Id + "/edit#gid=5  ", "5")]
        [TestCase("https://docs.google.com/spreadsheets/u/1/d/" + Id + "/edit#gid=8", "8")]
        public void EditorAddress_BecomesTheTabsCsvExport(string input, string gid)
        {
            Assert.That(Csv(input), Is.EqualTo(Export + gid));
        }

        [TestCase("https://docs.google.com/spreadsheets/d/e/2PACX-1vQ/pubhtml", "https://docs.google.com/spreadsheets/d/e/2PACX-1vQ/pub?output=csv")]
        [TestCase("https://docs.google.com/spreadsheets/d/e/2PACX-1vQ/pub?output=csv", "https://docs.google.com/spreadsheets/d/e/2PACX-1vQ/pub?output=csv")]
        [TestCase("https://docs.google.com/spreadsheets/d/e/2PACX-1vQ/pub?gid=5&single=true&output=html", "https://docs.google.com/spreadsheets/d/e/2PACX-1vQ/pub?gid=5&single=true&output=csv")]
        [TestCase("https://docs.google.com/spreadsheets/d/e/2PACX-1vQ/pubhtml?gid=9&single=true", "https://docs.google.com/spreadsheets/d/e/2PACX-1vQ/pub?gid=9&single=true&output=csv")]
        public void PublishedAddress_KeepsItsTabAndAsksForCsv(string input, string expected)
        {
            Assert.That(Csv(input), Is.EqualTo(expected));
        }

        [Test]
        public void CsvExportAddress_IsReturnedUnchanged()
        {
            const string url = "https://docs.google.com/spreadsheets/d/" + Id + "/export?format=csv&gid=31";
            Assert.That(Csv(url), Is.EqualTo(url));
            Assert.That(Error("https://docs.google.com/spreadsheets/d/" + Id + "/export?format=xlsx"), Does.Contain("format=csv"));
        }

        [Test]
        public void CsvExportAddress_PastedWithoutAScheme_GetsHttps()
        {
            string url = Csv("  docs.google.com/spreadsheets/d/" + Id + "/export?format=csv&gid=3 ");
            Assert.That(url, Is.EqualTo(Export + "3"));
            Assert.That(Uri.TryCreate(url, UriKind.Absolute, out Uri _), Is.True, "HttpClient needs an absolute address.");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("시트 주소")]
        [TestCase("https://example.com/spreadsheets/d/" + Id + "/edit")]
        [TestCase("https://docs.google.com.example.com/spreadsheets/d/" + Id + "/edit")]
        [TestCase("https://docs.google.com/document/d/" + Id + "/edit")]
        [TestCase("https://docs.google.com/spreadsheets/d/" + Id + "/copy")]
        [TestCase("https://docs.google.com/spreadsheets/d/e/2PACX-1vQ/edit")]
        [TestCase("https://docs.google.com/spreadsheets/d/" + Id + "/edit#gid=abc")]
        [TestCase("ftp://docs.google.com/spreadsheets/d/" + Id + "/edit")]
        public void AnythingElse_FailsWithAKoreanMessage(string input)
        {
            string error = Error(input);
            Assert.That(error, Does.Match("[가-힣]"));
        }
    }
}
