using System;
using System.Globalization;
using System.Text;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Puts the skill sheet's names into code text, so a technique's name lives only in the sheet.
    /// <c>{기술:12}</c> becomes technique 12's name and <c>{기술:12:을}</c> adds the particle form that fits it (either form
    /// of a <see cref="KoreanParticle"/> pair). Particles that never change, such as 의 or 도, go after the token.
    /// An id the sheet lacks stays visible as <c>{기술:12?}</c> (an unknown particle as <c>{기술:12:x?}</c>), so a
    /// missing name shows up on screen and in tests instead of disappearing.</summary>
    public static class LegacySkillNames
    {
        private const string Open = "{기술:";

        /// <summary>The token for a technique's plain name.</summary>
        public static string Token(int id) => Open + id.ToString(CultureInfo.InvariantCulture) + "}";

        public static bool HasTokens(string text) => text != null && text.IndexOf(Open, StringComparison.Ordinal) >= 0;

        /// <summary>The template with every token replaced from the current sheet. Text without tokens comes back as is
        /// and does not read the sheet. A malformed token (no digits, no closing brace) is left untouched.</summary>
        public static string Format(string template)
        {
            if (!HasTokens(template)) return template;
            var text = new StringBuilder(template.Length + 16);
            int index = 0;
            while (index < template.Length)
            {
                int start = template.IndexOf(Open, index, StringComparison.Ordinal);
                if (start < 0) break;
                text.Append(template, index, start - index);
                int idStart = start + Open.Length;
                int idEnd = idStart;
                while (idEnd < template.Length && template[idEnd] >= '0' && template[idEnd] <= '9') idEnd++;
                string particle = null;
                int close = -1;
                if (idEnd < template.Length && template[idEnd] == '}') close = idEnd;
                else if (idEnd < template.Length && template[idEnd] == ':')
                {
                    close = template.IndexOf('}', idEnd + 1);
                    if (close >= 0) particle = template.Substring(idEnd + 1, close - idEnd - 1);
                }
                if (idEnd == idStart || close < 0 || particle != null && (particle.Length == 0 || particle.IndexOf('{') >= 0))
                {
                    text.Append(Open);
                    index = idStart;
                    continue;
                }
                text.Append(Replacement(template.Substring(idStart, idEnd - idStart), particle));
                index = close + 1;
            }
            if (index < template.Length) text.Append(template, index, template.Length - index);
            return text.ToString();
        }

        private static string Replacement(string id, string particle)
        {
            LegacySkillDefinition definition =
                int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number > 0
                    ? LegacySkillDefinitions.Find(number) : null;
            if (definition == null) return Open + id + "?}";
            if (particle == null) return definition.Skill.Name;
            return KoreanParticle.IsKnown(particle)
                ? KoreanParticle.Attach(definition.Skill.Name, particle)
                : Open + id + ":" + particle + "?}";
        }
    }

    /// <summary>Copy kept by data built once (missions, the curriculum) that may hold <see cref="LegacySkillNames"/> tokens.
    /// It is formatted when read and again only after the sheet is installed or reloaded, so a screen that reads it every
    /// frame keeps getting the same string.</summary>
    internal sealed class SkillNameText
    {
        private readonly string template;
        private readonly bool hasTokens;
        private string formatted;
        private int formattedVersion;

        internal SkillNameText(string template)
        {
            this.template = template ?? string.Empty;
            hasTokens = LegacySkillNames.HasTokens(this.template);
        }

        public string Value
        {
            get
            {
                if (!hasTokens) return template;
                int version = LegacySkillDefinitions.Version;
                if (formatted == null || formattedVersion != version)
                {
                    formatted = LegacySkillNames.Format(template);
                    formattedVersion = version;
                }
                return formatted;
            }
        }
    }
}
