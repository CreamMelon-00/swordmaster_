using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class KoreanParticleTests
    {
        // After a vowel; either form of the pair may be asked for.
        [TestCase("베기", "을", "베기를")]
        [TestCase("베기", "를", "베기를")]
        [TestCase("알티바호", "이", "알티바호가")]
        [TestCase("르프리즈", "은", "르프리즈는")]
        [TestCase("쿠페", "과", "쿠페와")]
        [TestCase("막기", "으로", "막기로")]
        [TestCase("플레슈", "이나", "플레슈나")]
        [TestCase("베기", "이랑", "베기랑")]
        // After a final consonant.
        [TestCase("호흡", "를", "호흡을")]
        [TestCase("유연함", "가", "유연함이")]
        [TestCase("호흡", "는", "호흡은")]
        [TestCase("유연함", "와", "유연함과")]
        [TestCase("호흡", "로", "호흡으로")]
        [TestCase("호흡", "나", "호흡이나")]
        [TestCase("호흡", "랑", "호흡이랑")]
        public void Hangul_PicksTheFormByTheLastSyllable(string word, string particle, string expected)
        {
            Assert.That(KoreanParticle.Attach(word, particle), Is.EqualTo(expected));
        }

        // ㄹ counts as a consonant everywhere except before 으로/로, which takes 로.
        [TestCase("칼", "으로", "칼로")]
        [TestCase("칼", "로", "칼로")]
        [TestCase("칼", "을", "칼을")]
        [TestCase("칼", "가", "칼이")]
        [TestCase("칼", "와", "칼과")]
        [TestCase("칼", "나", "칼이나")]
        public void RieulFinal_TakesRoButStaysAConsonant(string word, string particle, string expected)
        {
            Assert.That(KoreanParticle.Attach(word, particle), Is.EqualTo(expected));
        }

        // The last digit is read 영 일 이 삼 사 오 육 칠 팔 구.
        [TestCase("0", "를", "0을")]
        [TestCase("1", "를", "1을")]
        [TestCase("2", "을", "2를")]
        [TestCase("3", "가", "3이")]
        [TestCase("4", "은", "4는")]
        [TestCase("5", "과", "5와")]
        [TestCase("6", "로", "6으로")]
        [TestCase("7", "으로", "7로")]
        [TestCase("8", "으로", "8로")]
        [TestCase("9", "을", "9를")]
        [TestCase("10", "로", "10으로")]
        [TestCase("ACT 2", "이", "ACT 2가")]
        public void Digits_FollowTheirKoreanReading(string word, string particle, string expected)
        {
            Assert.That(KoreanParticle.Attach(word, particle), Is.EqualTo(expected));
        }

        [TestCase("ACT", "을", "ACT을(를)")]
        [TestCase("ACT", "이", "ACT이(가)")]
        [TestCase("ACT", "는", "ACT은(는)")]
        [TestCase("ACT", "와", "ACT과(와)")]
        [TestCase("ACT", "로", "ACT(으)로")]
        [TestCase("ACT", "나", "ACT(이)나")]
        [TestCase("ACT", "랑", "ACT(이)랑")]
        [TestCase("'베기'", "을", "'베기'을(를)")]
        [TestCase("베기 ", "을", "베기 을(를)")]
        [TestCase("ㄱ", "을", "ㄱ을(를)")]
        [TestCase("", "과", "과(와)")]
        [TestCase(null, "은", "은(는)")]
        public void AnythingElse_GetsTheCombinedForm(string word, string particle, string expected)
        {
            Assert.That(KoreanParticle.Attach(word, particle), Is.EqualTo(expected));
        }

        [Test]
        public void OnlyTheSevenPairsAreKnown()
        {
            foreach (string particle in new[] { "을", "를", "이", "가", "은", "는", "과", "와", "으로", "로", "이나", "나", "이랑", "랑" })
                Assert.That(KoreanParticle.IsKnown(particle), Is.True, particle);
            foreach (string particle in new[] { "의", "도", "에", "을(를)", "", null })
            {
                Assert.That(KoreanParticle.IsKnown(particle), Is.False, particle ?? "null");
                Assert.Throws<ArgumentException>(() => KoreanParticle.Attach("베기", particle), particle ?? "null");
            }
        }
    }
}
