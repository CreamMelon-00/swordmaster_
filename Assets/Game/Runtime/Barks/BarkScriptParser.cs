using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Barks
{
    public sealed class BarkParseException : FormatException
    {
        public BarkParseException(string scriptId, int lineNumber, string message)
            : base(lineNumber > 0
                ? $"전투 대사 '{scriptId}', {lineNumber}번째 줄: {message}"
                : $"전투 대사 '{scriptId}': {message}")
        {
            ScriptId = scriptId;
            LineNumber = lineNumber;
        }

        public string ScriptId { get; }
        public int LineNumber { get; }
    }

    /// <summary>Reads a battle's bark file (<c>Docs/Barks.md</c>):
    /// <code>
    /// # 메모
    /// @on &lt;상황&gt; [enemy|player]
    /// 한 줄 대사
    /// </code>
    /// Each <c>@on</c> names a trigger (<see cref="BarkScript.TriggerName"/>) and who speaks (the enemy unless
    /// <c>player</c>), and the lines under it, one or more, are what may be said; one is picked when it fires. <c>#</c> starts
    /// a comment line, and a memo after an <c>@on</c>'s words; a line that should start with <c>@</c> or <c>#</c> is written
    /// <c>\@</c> or <c>\#</c>. A file with only comments is valid and says nothing.</summary>
    public static class BarkScriptParser
    {
        public const string OnDirective = "@on";
        private const string Usage = "@on <상황> [enemy|player]";

        public static BarkScript Parse(string scriptId, string source)
        {
            if (string.IsNullOrWhiteSpace(scriptId)) throw new ArgumentException("A bark file id is required.", nameof(scriptId));
            if (source == null) throw new ArgumentNullException(nameof(source));
            string id = scriptId.Trim();
            string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length > 0 && lines[0].Length > 0 && lines[0][0] == (char)0xFEFF) lines[0] = lines[0].Substring(1);

            var entries = new List<BarkEntry>();
            // Where each trigger and speaker was declared, to point a repeated @on at the first.
            var declared = new Dictionary<int, int>();
            BarkTrigger trigger = BarkTrigger.Start;
            BarkSpeaker speaker = BarkSpeaker.Enemy;
            int declaredAt = 0;
            var said = new List<string>();
            for (int index = 0; index < lines.Length; index++)
            {
                int lineNumber = index + 1;
                string line = lines[index].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == '@')
                {
                    string command = FirstToken(line);
                    if (command != OnDirective)
                        throw Error(id, lineNumber, $"알 수 없는 지시어 '{command}'입니다. 전투 대사의 지시어는 {OnDirective} 하나입니다" +
                            "(소문자). 대사가 @로 시작한다면 \\@로 적으세요.");
                    Close(id, entries, trigger, speaker, declaredAt, said);
                    ParseOn(id, lineNumber, line.Substring(command.Length), out trigger, out speaker);
                    int key = BarkScript.Key(trigger, speaker);
                    if (declared.TryGetValue(key, out int first))
                        throw Error(id, lineNumber, $"{OnDirective} {BarkScript.TriggerName(trigger)} {SpeakerName(speaker)}은(는) 이미 " +
                            $"{first}번째 줄에 있습니다. 대사를 그 아래에 이어 적으세요.");
                    declared.Add(key, lineNumber);
                    declaredAt = lineNumber;
                    continue;
                }
                if (line.StartsWith("\\@", StringComparison.Ordinal) || line.StartsWith("\\#", StringComparison.Ordinal))
                    line = line.Substring(1);
                if (declaredAt == 0)
                    throw Error(id, lineNumber, $"대사보다 먼저 '{Usage}'로 언제, 누가 말할지 적으세요.");
                if (line.Length > BarkScript.MaxTextLength)
                    throw Error(id, lineNumber, $"말풍선 대사는 {BarkScript.MaxTextLength}자를 넘을 수 없습니다(지금 {line.Length}자). " +
                        "짧게 줄이세요.");
                said.Add(line);
            }
            Close(id, entries, trigger, speaker, declaredAt, said);
            return new BarkScript(id, entries);
        }

        // The @on block that just ended becomes an entry; one with no line is a mistake.
        private static void Close(string id, ICollection<BarkEntry> entries, BarkTrigger trigger, BarkSpeaker speaker,
            int declaredAt, List<string> said)
        {
            if (declaredAt == 0) return;
            if (said.Count == 0)
                throw Error(id, declaredAt, $"{OnDirective} {BarkScript.TriggerName(trigger)} 아래에 대사가 한 줄도 없습니다. " +
                    "대사를 적거나 이 줄 앞에 #을 붙여 막으세요.");
            entries.Add(new BarkEntry(trigger, speaker, said.ToArray(), declaredAt));
            said.Clear();
        }

        private static void ParseOn(string id, int lineNumber, string arguments, out BarkTrigger trigger, out BarkSpeaker speaker)
        {
            // A memo may follow the words: nothing in them is a #.
            int memo = arguments.IndexOf('#');
            if (memo >= 0) arguments = arguments.Substring(0, memo);
            string[] words = arguments.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 1 || words.Length > 2) throw Error(id, lineNumber, $"형식은 '{Usage}'입니다.");
            if (!BarkScript.TryParseTrigger(words[0], out trigger))
                throw Error(id, lineNumber, $"상황 '{words[0]}'을(를) 모릅니다. " +
                    $"{string.Join(", ", BarkScript.TriggerNames)} 중 하나를 소문자로 적으세요.");
            speaker = BarkSpeaker.Enemy;
            if (words.Length == 2)
                speaker = words[1] == "enemy" ? BarkSpeaker.Enemy : words[1] == "player" ? BarkSpeaker.Player
                    : throw Error(id, lineNumber, "말하는 쪽은 enemy(상대) 또는 player(엘리사)입니다. 안 쓰면 enemy입니다.");
        }

        private static string SpeakerName(BarkSpeaker speaker) => speaker == BarkSpeaker.Player ? "player" : "enemy";

        private static string FirstToken(string line)
        {
            for (int index = 0; index < line.Length; index++)
                if (char.IsWhiteSpace(line[index]) || line[index] == '#') return line.Substring(0, index);
            return line;
        }

        private static BarkParseException Error(string id, int lineNumber, string message)
            => new BarkParseException(id, lineNumber, message);
    }
}
