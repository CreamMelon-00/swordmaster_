using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Korean particles whose form follows the sound before them (을/를, 이/가, ...). Copy that inserts a name
    /// asks here for the fitting form instead of printing both, as in 을(를).</summary>
    public static class KoreanParticle
    {
        // Each pair: the form after a final consonant, the form after a vowel, and the combined form shown when the
        // word's last sound is unknown. 으로/로 also takes 로 after ㄹ.
        private static readonly string[][] pairs =
        {
            new[] { "을", "를", "을(를)" },
            new[] { "이", "가", "이(가)" },
            new[] { "은", "는", "은(는)" },
            new[] { "과", "와", "과(와)" },
            new[] { "으로", "로", "(으)로" },
            new[] { "이나", "나", "(이)나" },
            new[] { "이랑", "랑", "(이)랑" },
        };
        private const int RoPair = 4;
        // How a final digit is read: 영 일 이 삼 사 오 육 칠 팔 구.
        private const string DigitReadings = "영일이삼사오육칠팔구";
        private const char FirstSyllable = '가', LastSyllable = '힣';
        private const int FinalCount = 28, FinalRieul = 8;

        /// <summary>Whether the particle is either form of a pair this class knows.</summary>
        public static bool IsKnown(string particle) => FindPair(particle) >= 0;

        /// <summary>The word followed by the form of the particle that fits its last syllable or digit; either form of a
        /// pair may be given. A word ending in anything else (Latin letters, punctuation, nothing) gets the combined form.</summary>
        public static string Attach(string word, string particle)
        {
            int pair = FindPair(particle);
            if (pair < 0) throw new ArgumentException($"'{particle}' is not a particle this class can choose a form for.", nameof(particle));
            word = word ?? string.Empty;
            int final = FinalOf(word);
            string[] forms = pairs[pair];
            if (final < 0) return word + forms[2];
            bool consonant = final != 0 && !(pair == RoPair && final == FinalRieul);
            return word + (consonant ? forms[0] : forms[1]);
        }

        private static int FindPair(string particle)
        {
            for (int i = 0; i < pairs.Length; i++)
                if (particle == pairs[i][0] || particle == pairs[i][1]) return i;
            return -1;
        }

        // The last sound's final consonant index (0 = none, 8 = ㄹ), or -1 when the word does not end in Hangul or a digit.
        private static int FinalOf(string word)
        {
            if (word.Length == 0) return -1;
            char last = word[word.Length - 1];
            if (last >= '0' && last <= '9') last = DigitReadings[last - '0'];
            if (last < FirstSyllable || last > LastSyllable) return -1;
            return (last - FirstSyllable) % FinalCount;
        }
    }
}
