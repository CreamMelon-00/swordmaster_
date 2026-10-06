using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The Korean words for skill enums, shared by the skill sheet and the screens. The sheet also reads each
    /// enum's English name (any case) so a designer may type either.</summary>
    public static class LegacySkillLabels
    {
        private const string BrokenState = "붕괴", HealthState = "체력", AtMost = "이하";
        // 체력 N% 이하, spaces optional between the parts; ASCII digits only (a leading zero still reads).
        private static readonly Regex HealthPattern = new Regex(@"^체력\s*([0-9]{1,3})\s*%\s*이하$", RegexOptions.CultureInvariant);
        private static readonly LegacySkillProperty[] properties =
            { LegacySkillProperty.Slash, LegacySkillProperty.Hit, LegacySkillProperty.Penetrate, LegacySkillProperty.Defence, LegacySkillProperty.None };
        private static readonly LegacySkillKind[] kinds = { LegacySkillKind.Attack, LegacySkillKind.Defence, LegacySkillKind.Wait };
        private static readonly LegacySkillSymbol[] symbols =
        {
            LegacySkillSymbol.Act, LegacySkillSymbol.Sword, LegacySkillSymbol.Guard, LegacySkillSymbol.Hits,
            LegacySkillSymbol.Recovery, LegacySkillSymbol.Followup, LegacySkillSymbol.Reduction, LegacySkillSymbol.Variance,
        };
        private static readonly LegacySkillTone[] tones =
        {
            LegacySkillTone.Neutral, LegacySkillTone.Recovery, LegacySkillTone.Followup, LegacySkillTone.Reduction,
            LegacySkillTone.HighPower, LegacySkillTone.MultiHit, LegacySkillTone.Variance, LegacySkillTone.Defence,
        };

        public static string Property(LegacySkillProperty property)
        {
            switch (property)
            {
                case LegacySkillProperty.Slash: return "참격";
                case LegacySkillProperty.Hit: return "타격";
                case LegacySkillProperty.Penetrate: return "관통";
                case LegacySkillProperty.Defence: return "방어";
                default: return "없음";
            }
        }

        public static string Kind(LegacySkillKind kind)
        {
            switch (kind)
            {
                case LegacySkillKind.Attack: return "공격";
                case LegacySkillKind.Defence: return "방어";
                default: return "대기";
            }
        }

        public static string Symbol(LegacySkillSymbol symbol)
        {
            switch (symbol)
            {
                case LegacySkillSymbol.Act: return "ACT";
                case LegacySkillSymbol.Sword: return "검";
                case LegacySkillSymbol.Guard: return "방어";
                case LegacySkillSymbol.Hits: return "타수";
                case LegacySkillSymbol.Recovery: return "회복";
                case LegacySkillSymbol.Followup: return "후속";
                case LegacySkillSymbol.Reduction: return "감소";
                default: return "편차";
            }
        }

        public static string Tone(LegacySkillTone tone)
        {
            switch (tone)
            {
                case LegacySkillTone.Neutral: return "기본";
                case LegacySkillTone.Recovery: return "회복";
                case LegacySkillTone.Followup: return "후속";
                case LegacySkillTone.Reduction: return "감소";
                case LegacySkillTone.HighPower: return "고화력";
                case LegacySkillTone.MultiHit: return "연타";
                case LegacySkillTone.Variance: return "편차";
                default: return "방어";
            }
        }

        /// <summary>A 상대 상태 조건 as the sheet writes it: <c>붕괴</c>, <c>체력 30% 이하</c>, or empty for none.</summary>
        public static string OpponentState(LegacyOpponentState state, int healthPercent)
        {
            switch (state)
            {
                case LegacyOpponentState.Broken: return BrokenState;
                case LegacyOpponentState.HealthAtMost:
                    return HealthState + " " + healthPercent.ToString(CultureInfo.InvariantCulture) + "% " + AtMost;
                default: return string.Empty;
            }
        }

        /// <summary>Reads <c>붕괴</c> or <c>체력 N% 이하</c> with N from 1 to 99; spaces between the parts are optional
        /// (<c>체력30%이하</c>, <c>체력 30 % 이하</c>). Blank and anything else read false.</summary>
        public static bool TryParseOpponentState(string text, out LegacyOpponentState state, out int healthPercent)
        {
            state = LegacyOpponentState.None;
            healthPercent = 0;
            string trimmed = text?.Trim() ?? string.Empty;
            if (trimmed == BrokenState)
            {
                state = LegacyOpponentState.Broken;
                return true;
            }
            Match match = HealthPattern.Match(trimmed);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int percent) ||
                percent < 1 || percent > 99)
                return false;
            state = LegacyOpponentState.HealthAtMost;
            healthPercent = percent;
            return true;
        }

        /// <summary>Reads a label or an English enum name. <see cref="LegacySkillProperty.None"/> reads too; the sheet
        /// rejects it for techniques.</summary>
        public static bool TryParseProperty(string text, out LegacySkillProperty property)
            => TryParse(text, properties, Property, out property);

        /// <summary>Reads a label or an English enum name, <see cref="LegacySkillKind.Wait"/> included.</summary>
        public static bool TryParseKind(string text, out LegacySkillKind kind) => TryParse(text, kinds, Kind, out kind);

        public static bool TryParseSymbol(string text, out LegacySkillSymbol symbol) => TryParse(text, symbols, Symbol, out symbol);

        public static bool TryParseTone(string text, out LegacySkillTone tone) => TryParse(text, tones, Tone, out tone);

        // Labels compare exactly and English names ignore case. Enum.TryParse would also take numbers, which a sheet
        // cell must not mean.
        private static bool TryParse<T>(string text, T[] values, Func<T, string> label, out T value) where T : struct
        {
            string trimmed = text?.Trim() ?? string.Empty;
            foreach (T candidate in values)
                if (trimmed == label(candidate) || string.Equals(trimmed, candidate.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    value = candidate;
                    return true;
                }
            value = default;
            return false;
        }
    }
}
