using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Dialogue
{
    public sealed class DialogueParseException : FormatException
    {
        public DialogueParseException(string scriptId, int lineNumber, string message)
            : base(lineNumber > 0
                ? $"다이얼로그 '{scriptId}', {lineNumber}번째 줄: {message}"
                : $"다이얼로그 '{scriptId}': {message}")
        {
            ScriptId = scriptId;
            LineNumber = lineNumber;
        }

        public string ScriptId { get; }
        public int LineNumber { get; }
    }

    public static class DialogueScriptParser
    {
        private sealed class SpeakerDefinition
        {
            public SpeakerDefinition(string name, string role)
            {
                Name = name;
                Role = role;
            }

            public string Name { get; }
            public string Role { get; }
        }

        public static DialogueScript Parse(string scriptId, string source)
        {
            if (string.IsNullOrWhiteSpace(scriptId)) throw new ArgumentException("A dialogue id is required.", nameof(scriptId));
            if (source == null) throw new ArgumentNullException(nameof(source));
            scriptId = scriptId.Trim();

            string normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] physicalLines = normalized.Split('\n');
            if (physicalLines.Length > 0 && physicalLines[0].Length > 0 && physicalLines[0][0] == '\ufeff')
                physicalLines[0] = physicalLines[0].Substring(1);

            var parsed = new List<DialogueLine>();
            DialogueSide activeSide = DialogueSide.Narrator;
            SpeakerDefinition left = null, right = null;
            bool leftVisible = false, rightVisible = false;

            for (int index = 0; index < physicalLines.Length; index++)
            {
                int sourceLineNumber = index + 1;
                string line = physicalLines[index].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                if (line.StartsWith("\\@", StringComparison.Ordinal) || line.StartsWith("\\#", StringComparison.Ordinal))
                {
                    AddLine(parsed, activeSide, left, right, leftVisible, rightVisible,
                        sourceLineNumber, line.Substring(1), scriptId);
                    continue;
                }

                if (line[0] != '@')
                {
                    AddLine(parsed, activeSide, left, right, leftVisible, rightVisible,
                        sourceLineNumber, line, scriptId);
                    continue;
                }

                int separator = FirstWhitespace(line);
                string command = separator < 0 ? line : line.Substring(0, separator);
                string declaration = separator < 0 ? string.Empty : line.Substring(separator).Trim();
                switch (command)
                {
                    case "@narrator":
                        if (declaration.Length > 0)
                            throw Error(scriptId, sourceLineNumber, "@narrator 뒤에는 화자 이름이나 역할을 적을 수 없습니다.");
                        activeSide = DialogueSide.Narrator;
                        break;
                    case "@left":
                        if (declaration.Length > 0)
                            left = ParseSpeaker(scriptId, sourceLineNumber, declaration, "@left");
                        else if (left == null)
                            throw Error(scriptId, sourceLineNumber, "먼저 이름을 적은 @left 화자를 선언해야 합니다.");
                        leftVisible = true;
                        activeSide = DialogueSide.Left;
                        break;
                    case "@right":
                        if (declaration.Length > 0)
                            right = ParseSpeaker(scriptId, sourceLineNumber, declaration, "@right");
                        else if (right == null)
                            throw Error(scriptId, sourceLineNumber, "먼저 이름을 적은 @right 화자를 선언해야 합니다.");
                        rightVisible = true;
                        activeSide = DialogueSide.Right;
                        break;
                    case "@show":
                    {
                        ParseSideAndRemainder(scriptId, sourceLineNumber, declaration, "@show",
                            out DialogueSide side, out string speakerDeclaration);
                        if (speakerDeclaration.Length > 0)
                        {
                            SpeakerDefinition speaker = ParseSpeaker(scriptId, sourceLineNumber,
                                speakerDeclaration, "@show");
                            SetSpeaker(side, speaker, ref left, ref right);
                        }
                        else if (GetSpeaker(side, left, right) == null)
                        {
                            throw Error(scriptId, sourceLineNumber,
                                $"{SideName(side)} 위치에는 다시 표시할 화자가 없습니다. 화자 이름을 함께 적으세요.");
                        }

                        SetVisible(side, true, ref leftVisible, ref rightVisible);
                        break;
                    }
                    case "@hide":
                    {
                        DialogueSide side = ParseSingleSide(scriptId, sourceLineNumber, declaration, "@hide");
                        if (!IsVisible(side, leftVisible, rightVisible))
                            throw Error(scriptId, sourceLineNumber,
                                $"{SideName(side)} 위치에는 퇴장시킬 화자가 없습니다.");
                        SetVisible(side, false, ref leftVisible, ref rightVisible);
                        break;
                    }
                    case "@move":
                    {
                        ParseMove(scriptId, sourceLineNumber, declaration,
                            out DialogueSide sourceSide, out DialogueSide destinationSide);
                        if (!IsVisible(sourceSide, leftVisible, rightVisible))
                            throw Error(scriptId, sourceLineNumber,
                                $"{SideName(sourceSide)} 위치에는 이동시킬 화자가 없습니다.");
                        if (IsVisible(destinationSide, leftVisible, rightVisible))
                            throw Error(scriptId, sourceLineNumber,
                                $"{SideName(destinationSide)} 위치가 이미 다른 화자로 채워져 있습니다.");

                        SpeakerDefinition moving = GetSpeaker(sourceSide, left, right);
                        SetSpeaker(destinationSide, moving, ref left, ref right);
                        SetVisible(sourceSide, false, ref leftVisible, ref rightVisible);
                        SetVisible(destinationSide, true, ref leftVisible, ref rightVisible);
                        if (activeSide == sourceSide) activeSide = destinationSide;
                        break;
                    }
                    default:
                        throw Error(scriptId, sourceLineNumber,
                            $"알 수 없는 지시어 '{command}'입니다. 표시할 문장이 @로 시작한다면 \\@로 적으세요.");
                }
            }

            if (parsed.Count == 0) throw Error(scriptId, 0, "파일에 실제로 표시할 문장이 없습니다.");
            return new DialogueScript(scriptId, parsed);
        }

        private static void AddLine(ICollection<DialogueLine> destination, DialogueSide side,
            SpeakerDefinition left, SpeakerDefinition right, bool leftVisible, bool rightVisible,
            int sourceLineNumber, string text, string scriptId)
        {
            if (text.Length > DialogueLine.MaxTextLength)
                throw Error(scriptId, sourceLineNumber,
                    $"한 번에 표시할 문장은 {DialogueLine.MaxTextLength}자를 넘을 수 없습니다. 다음 표시 줄로 나누세요.");
            SpeakerDefinition speaker = side == DialogueSide.Left ? left : side == DialogueSide.Right ? right : null;
            if (side != DialogueSide.Narrator && speaker == null)
                throw Error(scriptId, sourceLineNumber, $"{side} 위치에 선언된 화자가 없습니다.");
            if (side == DialogueSide.Left && !leftVisible || side == DialogueSide.Right && !rightVisible)
                throw Error(scriptId, sourceLineNumber,
                    "현재 화자가 무대에서 숨겨져 있습니다. @left, @right 또는 @narrator로 말할 대상을 다시 지정하세요.");
            var stage = new DialogueStageSnapshot(
                leftVisible ? ToStageSlot(left) : null,
                rightVisible ? ToStageSlot(right) : null);
            destination.Add(new DialogueLine(sourceLineNumber, side,
                speaker?.Name ?? string.Empty, speaker?.Role ?? string.Empty, text, stage));
        }

        private static DialogueStageSlot ToStageSlot(SpeakerDefinition speaker)
            => speaker == null ? null : new DialogueStageSlot(speaker.Name, speaker.Role);

        private static SpeakerDefinition ParseSpeaker(string scriptId, int lineNumber, string declaration,
            string directive)
        {
            int divider = declaration.IndexOf('|');
            if (divider >= 0 && declaration.IndexOf('|', divider + 1) >= 0)
                throw Error(scriptId, lineNumber, "화자 선언에는 | 구분자를 한 번만 사용할 수 있습니다.");

            string name = (divider < 0 ? declaration : declaration.Substring(0, divider)).Trim();
            string role = divider < 0 ? string.Empty : declaration.Substring(divider + 1).Trim();
            if (name.Length == 0) throw Error(scriptId, lineNumber, $"{directive} 뒤에 화자 이름이 필요합니다.");
            if (divider >= 0 && role.Length == 0) throw Error(scriptId, lineNumber, "|를 지우거나 뒤에 역할·호칭을 적으세요.");
            return new SpeakerDefinition(name, role);
        }

        private static void ParseSideAndRemainder(string scriptId, int lineNumber, string declaration,
            string directive, out DialogueSide side, out string remainder)
        {
            int separator = FirstWhitespace(declaration);
            string sideToken = separator < 0 ? declaration : declaration.Substring(0, separator);
            remainder = separator < 0 ? string.Empty : declaration.Substring(separator).Trim();
            side = ParseSide(scriptId, lineNumber, sideToken, directive);
        }

        private static DialogueSide ParseSingleSide(string scriptId, int lineNumber, string declaration,
            string directive)
        {
            ParseSideAndRemainder(scriptId, lineNumber, declaration, directive,
                out DialogueSide side, out string remainder);
            if (remainder.Length > 0)
                throw Error(scriptId, lineNumber, $"{directive}에는 left 또는 right 위치 하나만 적으세요.");
            return side;
        }

        private static void ParseMove(string scriptId, int lineNumber, string declaration,
            out DialogueSide source, out DialogueSide destination)
        {
            string[] tokens = declaration.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 2)
                throw Error(scriptId, lineNumber, "@move에는 출발 위치와 도착 위치를 차례로 적으세요.");
            source = ParseSide(scriptId, lineNumber, tokens[0], "@move");
            destination = ParseSide(scriptId, lineNumber, tokens[1], "@move");
            if (source == destination)
                throw Error(scriptId, lineNumber, "@move의 출발 위치와 도착 위치는 달라야 합니다.");
        }

        private static DialogueSide ParseSide(string scriptId, int lineNumber, string token, string directive)
        {
            switch (token)
            {
                case "left":
                    return DialogueSide.Left;
                case "right":
                    return DialogueSide.Right;
                default:
                    throw Error(scriptId, lineNumber,
                        $"{directive} 위치는 left 또는 right로 적어야 합니다.");
            }
        }

        private static SpeakerDefinition GetSpeaker(DialogueSide side, SpeakerDefinition left,
            SpeakerDefinition right) => side == DialogueSide.Left ? left : right;

        private static void SetSpeaker(DialogueSide side, SpeakerDefinition speaker,
            ref SpeakerDefinition left, ref SpeakerDefinition right)
        {
            if (side == DialogueSide.Left) left = speaker;
            else right = speaker;
        }

        private static bool IsVisible(DialogueSide side, bool leftVisible, bool rightVisible)
            => side == DialogueSide.Left ? leftVisible : rightVisible;

        private static void SetVisible(DialogueSide side, bool visible,
            ref bool leftVisible, ref bool rightVisible)
        {
            if (side == DialogueSide.Left) leftVisible = visible;
            else rightVisible = visible;
        }

        private static string SideName(DialogueSide side) => side == DialogueSide.Left ? "left" : "right";

        private static int FirstWhitespace(string value)
        {
            for (int index = 0; index < value.Length; index++)
                if (char.IsWhiteSpace(value[index])) return index;
            return -1;
        }

        private static DialogueParseException Error(string id, int lineNumber, string message)
            => new DialogueParseException(id, lineNumber, message);
    }
}
