using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TurnLimbo.Runtime.Campaign;

namespace TurnLimbo.Runtime.Save
{
    /// <summary>A small line-based text format for <see cref="GameSave"/>, readable in any editor:
    /// <code>
    /// turn-limbo-save 1
    /// prologue 4
    /// currency 120
    /// cleared 1 2
    /// skill 1 0
    /// lane 0 1 2 7
    /// </code>
    /// <c>skill</c> (id, level) repeats per owned skill in order; <c>lane</c> (index, three ids) appears once per lane.
    /// Parsing is strict: unknown keys, missing or repeated keys and other versions are rejected. Game rules are
    /// checked later by <see cref="GameSave.TryApply"/>.</summary>
    public static class GameSaveCodec
    {
        public const string Header = "turn-limbo-save";
        private const int LaneCount = 3;

        public static string Serialize(GameSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            CampaignSave campaign = save.Campaign;
            var text = new StringBuilder();
            text.Append(Header).Append(' ').Append(GameSave.CurrentVersion).Append('\n');
            text.Append("prologue ").Append(save.PrologueCleared).Append('\n');
            text.Append("currency ").Append(campaign.Currency).Append('\n');
            text.Append("cleared");
            foreach (int number in campaign.ClearedStages) text.Append(' ').Append(number);
            text.Append('\n');
            foreach (SavedSkill skill in campaign.OwnedSkills)
                text.Append("skill ").Append(skill.Id).Append(' ').Append(skill.Level).Append('\n');
            for (int lane = 0; lane < campaign.Loadout.Count; lane++)
            {
                text.Append("lane ").Append(lane);
                foreach (int id in campaign.Loadout[lane]) text.Append(' ').Append(id);
                text.Append('\n');
            }
            return text.ToString();
        }

        public static bool TryParse(string source, out GameSave save, out string error)
        {
            save = null;
            if (source == null)
            {
                error = "저장 파일이 비어 있습니다.";
                return false;
            }
            string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length > 0 && lines[0].Length > 0 && lines[0][0] == '﻿') lines[0] = lines[0].Substring(1);

            int? prologue = null, currency = null;
            List<int> cleared = null;
            var skills = new List<SavedSkill>();
            var lanes = new List<int>[LaneCount];
            bool headerRead = false;
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length == 0) continue;
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                int lineNumber = index + 1;
                if (!headerRead)
                {
                    if (parts[0] != Header || parts.Length != 2 || !TryInt(parts[1], out int version))
                        return Fail(out error, lineNumber, "Turn Limbo 저장 파일이 아닙니다.");
                    if (version != GameSave.CurrentVersion)
                        return Fail(out error, lineNumber, $"지원하지 않는 저장 버전 {version}입니다.");
                    headerRead = true;
                    continue;
                }
                if (!TryInts(parts, 1, out int[] values)) return Fail(out error, lineNumber, "숫자가 아닌 값이 있습니다.");
                switch (parts[0])
                {
                    case "prologue":
                        if (prologue.HasValue) return Fail(out error, lineNumber, "prologue가 두 번 있습니다.");
                        if (values.Length != 1) return Fail(out error, lineNumber, "prologue에는 값이 하나만 필요합니다.");
                        prologue = values[0];
                        break;
                    case "currency":
                        if (currency.HasValue) return Fail(out error, lineNumber, "currency가 두 번 있습니다.");
                        if (values.Length != 1) return Fail(out error, lineNumber, "currency에는 값이 하나만 필요합니다.");
                        currency = values[0];
                        break;
                    case "cleared":
                        if (cleared != null) return Fail(out error, lineNumber, "cleared가 두 번 있습니다.");
                        cleared = new List<int>(values);
                        break;
                    case "skill":
                        if (values.Length != 2) return Fail(out error, lineNumber, "skill에는 기술 번호와 강화 단계가 필요합니다.");
                        skills.Add(new SavedSkill(values[0], values[1]));
                        break;
                    case "lane":
                        if (values.Length < 1 || values[0] < 0 || values[0] >= LaneCount)
                            return Fail(out error, lineNumber, "lane에는 0~2의 열 번호가 필요합니다.");
                        if (lanes[values[0]] != null) return Fail(out error, lineNumber, $"lane {values[0]}이(가) 두 번 있습니다.");
                        lanes[values[0]] = new List<int>(values).GetRange(1, values.Length - 1);
                        break;
                    default:
                        return Fail(out error, lineNumber, $"알 수 없는 항목 '{parts[0]}'입니다.");
                }
            }
            if (!headerRead) return Fail(out error, 0, "저장 파일이 비어 있습니다.");
            if (!prologue.HasValue) return Fail(out error, 0, "prologue 항목이 없습니다.");
            if (!currency.HasValue) return Fail(out error, 0, "currency 항목이 없습니다.");
            if (cleared == null) return Fail(out error, 0, "cleared 항목이 없습니다.");
            for (int lane = 0; lane < LaneCount; lane++)
                if (lanes[lane] == null) return Fail(out error, 0, $"lane {lane} 항목이 없습니다.");

            save = new GameSave(prologue.Value, new CampaignSave(currency.Value, cleared, skills, lanes));
            error = null;
            return true;
        }

        private static bool TryInts(string[] parts, int start, out int[] values)
        {
            values = new int[parts.Length - start];
            for (int index = start; index < parts.Length; index++)
                if (!TryInt(parts[index], out values[index - start])) return false;
            return true;
        }

        private static bool TryInt(string text, out int value)
            => int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        private static bool Fail(out string error, int lineNumber, string message)
        {
            error = lineNumber > 0 ? $"저장 파일 {lineNumber}번째 줄: {message}" : "저장 파일: " + message;
            return false;
        }
    }
}
