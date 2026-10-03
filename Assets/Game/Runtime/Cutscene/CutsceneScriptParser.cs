using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TurnLimbo.Runtime.Dialogue;

namespace TurnLimbo.Runtime.Cutscene
{
    public sealed class CutsceneParseException : FormatException
    {
        public CutsceneParseException(string scriptId, int lineNumber, string message, Exception inner = null)
            : base(lineNumber > 0
                ? $"컷신 '{scriptId}', {lineNumber}번째 줄: {message}"
                : $"컷신 '{scriptId}': {message}", inner)
        {
            ScriptId = scriptId;
            LineNumber = lineNumber;
        }

        public string ScriptId { get; }
        public int LineNumber { get; }
    }

    /// <summary>Reads a cutscene: the dialogue grammar (<see cref="DialogueScriptParser"/>, unchanged) plus staging
    /// directives. The staging lines are read here and hidden from the dialogue parser as comments, so dialogue lines
    /// keep their source line numbers and stage snapshots; both are then merged in source order.</summary>
    public static class CutsceneScriptParser
    {
        private static readonly string[] DialogueDirectives = { "@narrator", "@left", "@right", "@show", "@hide", "@move" };
        private static readonly string[] StagingDirectives = { "@wait", "@fade", "@bars", "@camera", "@image", "@actor" };
        // Lets the dialogue parser validate stage directives after the last line and files with no dialogue at all.
        private const string Sentinel = "\n@narrator\n__TURN_LIMBO_CUTSCENE_END__\n";

        private sealed class ActorState
        {
            public bool EliseVisible, KnightVisible, DummyVisible;

            public bool IsVisible(CutsceneActor actor)
                => actor == CutsceneActor.Elise ? EliseVisible : actor == CutsceneActor.Knight ? KnightVisible : DummyVisible;

            public void Set(CutsceneActor actor, bool visible)
            {
                if (actor == CutsceneActor.Elise) EliseVisible = visible;
                else if (actor == CutsceneActor.Knight) KnightVisible = visible;
                else DummyVisible = visible;
            }
        }

        public static CutsceneScript Parse(string scriptId, string source)
        {
            if (string.IsNullOrWhiteSpace(scriptId)) throw new ArgumentException("A cutscene id is required.", nameof(scriptId));
            if (source == null) throw new ArgumentNullException(nameof(source));
            string id = scriptId.Trim();
            string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length > 0 && lines[0].Length > 0 && lines[0][0] == '﻿') lines[0] = lines[0].Substring(1);

            var staging = new List<CutsceneStep>();
            var masked = new StringBuilder(source.Length + Sentinel.Length);
            var actors = new ActorState();
            bool imageShown = false;
            for (int index = 0; index < lines.Length; index++)
            {
                int lineNumber = index + 1;
                string line = lines[index].Trim();
                if (index > 0) masked.Append('\n');
                string command = line.StartsWith("@", StringComparison.Ordinal) ? FirstToken(line) : null;
                if (command != null && Array.IndexOf(StagingDirectives, command) >= 0)
                {
                    staging.Add(ParseStaging(id, lineNumber, command, Arguments(line, command), actors, ref imageShown));
                    masked.Append('#');
                    continue;
                }
                if (command != null && Array.IndexOf(DialogueDirectives, command) < 0)
                    throw new CutsceneParseException(id, lineNumber,
                        $"알 수 없는 지시어 '{command}'입니다. 대사 지시어(@narrator, @left, @right, @show, @hide, @move)나 " +
                        "연출 지시어(@wait, @fade, @bars, @camera, @image, @actor)를 소문자로 적으세요.");
                masked.Append(lines[index]);
            }
            masked.Append(Sentinel);

            IReadOnlyList<DialogueLine> dialogue;
            try
            {
                dialogue = DialogueScriptParser.Parse(id, masked.ToString()).Lines;
            }
            catch (DialogueParseException exception)
            {
                throw new CutsceneParseException(id, exception.LineNumber, Detail(exception.Message), exception);
            }

            var steps = new List<CutsceneStep>(staging.Count + dialogue.Count);
            int stagingIndex = 0;
            // The last dialogue line is the sentinel.
            for (int lineIndex = 0; lineIndex < dialogue.Count - 1; lineIndex++)
            {
                DialogueLine line = dialogue[lineIndex];
                while (stagingIndex < staging.Count && staging[stagingIndex].SourceLineNumber < line.SourceLineNumber)
                    steps.Add(staging[stagingIndex++]);
                steps.Add(CutsceneStep.ForLine(line));
            }
            while (stagingIndex < staging.Count) steps.Add(staging[stagingIndex++]);
            if (steps.Count == 0) throw new CutsceneParseException(id, 0, "연출 명령이나 대사가 하나도 없습니다.");
            return new CutsceneScript(id, steps);
        }

        private static CutsceneStep ParseStaging(string id, int line, string command, string[] args, ActorState actors,
            ref bool imageShown)
        {
            bool waits = true;
            if (args.Length > 0 && args[args.Length - 1] == "&")
            {
                waits = false;
                Array.Resize(ref args, args.Length - 1);
            }
            switch (command)
            {
                case "@wait":
                    if (!waits) throw Error(id, line, "@wait는 기다리는 명령이라 &를 붙이면 아무 일도 하지 않습니다. &를 지우세요.");
                    Expect(id, line, args, 1, 1, "@wait <초>");
                    return CutsceneStep.ForWait(line, Seconds(id, line, args[0]));
                case "@fade":
                {
                    Expect(id, line, args, 2, 2, "@fade out|in <초>");
                    bool toBlack = args[0] == "out" ? true : args[0] == "in" ? false
                        : throw Error(id, line, "@fade 다음에는 out(검게) 또는 in(밝게)을 적으세요.");
                    return CutsceneStep.ForFade(line, toBlack, Seconds(id, line, args[1]), waits);
                }
                case "@bars":
                {
                    Expect(id, line, args, 1, 2, "@bars on|off [초]");
                    bool on = args[0] == "on" ? true : args[0] == "off" ? false
                        : throw Error(id, line, "@bars 다음에는 on 또는 off를 적으세요.");
                    return CutsceneStep.ForBars(line, on, OptionalSeconds(id, line, args, 1), waits);
                }
                case "@camera":
                {
                    if (args.Length > 0 && args[0] == "reset")
                    {
                        Expect(id, line, args, 1, 2, "@camera reset [초]");
                        return CutsceneStep.ForCamera(line, 0f, CutsceneStep.DefaultCameraSize, OptionalSeconds(id, line, args, 1), waits);
                    }
                    Expect(id, line, args, 2, 3, "@camera <x> <크기> [초] 또는 @camera reset [초]");
                    float size = Number(id, line, args[1], "카메라 크기");
                    if (size < CutsceneStep.MinimumCameraSize || size > CutsceneStep.MaximumCameraSize)
                        throw Error(id, line, $"카메라 크기는 {CutsceneStep.MinimumCameraSize:0}~{CutsceneStep.MaximumCameraSize:0}입니다(기본 {CutsceneStep.DefaultCameraSize:0}, 작을수록 가깝다).");
                    return CutsceneStep.ForCamera(line, Position(id, line, args[0]), size, OptionalSeconds(id, line, args, 2), waits);
                }
                case "@image":
                {
                    Expect(id, line, args, 1, 2, "@image <Resources 경로> [초] 또는 @image off [초]");
                    if (args[0] == "off")
                    {
                        if (!imageShown) throw Error(id, line, "보이는 그림이 없어 @image off를 할 수 없습니다.");
                        imageShown = false;
                        return CutsceneStep.ForImage(line, null, OptionalSeconds(id, line, args, 1), waits);
                    }
                    imageShown = true;
                    return CutsceneStep.ForImage(line, args[0], OptionalSeconds(id, line, args, 1), waits);
                }
                default:
                    return ParseActor(id, line, args, waits, actors);
            }
        }

        private static CutsceneStep ParseActor(string id, int line, string[] args, bool waits, ActorState actors)
        {
            if (args.Length < 2) throw Error(id, line, "@actor <elise|knight|dummy> <at|hide|move|face|pose|attack> ... 형식으로 적으세요.");
            CutsceneActor actor = args[0] == "elise" ? CutsceneActor.Elise : args[0] == "knight" ? CutsceneActor.Knight
                : args[0] == "dummy" ? CutsceneActor.Dummy
                : throw Error(id, line, $"인물 '{args[0]}'을(를) 모릅니다. elise, knight, dummy 중 하나를 적으세요.");
            string action = args[1];
            if (action != "at" && !actors.IsVisible(actor))
                throw Error(id, line, $"{args[0]}이(가) 무대에 없습니다. 먼저 @actor {args[0]} at <x>로 세우세요.");
            switch (action)
            {
                case "at":
                {
                    RejectNoWait(id, line, waits, "@actor at");
                    Expect(id, line, args, 3, 4, "@actor <인물> at <x> [left|right]");
                    CutsceneActor partner = actor == CutsceneActor.Knight ? CutsceneActor.Dummy
                        : actor == CutsceneActor.Dummy ? CutsceneActor.Knight : CutsceneActor.Elise;
                    if (partner != actor && actors.IsVisible(partner))
                        throw Error(id, line, "기사와 허수아비는 같은 자리를 씁니다. 먼저 다른 쪽을 @actor ... hide로 내리세요.");
                    CutsceneFacing facing = args.Length == 4 ? Facing(id, line, args[3]) : CutsceneFacing.Unchanged;
                    actors.Set(actor, true);
                    return CutsceneStep.ForActor(line, actor, CutsceneActorAction.Place, Position(id, line, args[2]), facing);
                }
                case "hide":
                    RejectNoWait(id, line, waits, "@actor hide");
                    Expect(id, line, args, 2, 2, "@actor <인물> hide");
                    actors.Set(actor, false);
                    return CutsceneStep.ForActor(line, actor, CutsceneActorAction.Hide);
                case "move":
                    Expect(id, line, args, 4, 4, "@actor <인물> move <x> <초>");
                    if (actor == CutsceneActor.Dummy) throw Error(id, line, "허수아비는 움직이지 않습니다.");
                    return CutsceneStep.ForActor(line, actor, CutsceneActorAction.Move, Position(id, line, args[2]),
                        seconds: Seconds(id, line, args[3]), waits: waits);
                case "face":
                    RejectNoWait(id, line, waits, "@actor face");
                    Expect(id, line, args, 3, 3, "@actor <인물> face left|right");
                    return CutsceneStep.ForActor(line, actor, CutsceneActorAction.Face, facing: Facing(id, line, args[2]));
                case "pose":
                {
                    RejectNoWait(id, line, waits, "@actor pose");
                    Expect(id, line, args, 3, 3, "@actor <인물> pose idle|hurt|block");
                    CutscenePose pose = args[2] == "idle" ? CutscenePose.Idle : args[2] == "hurt" ? CutscenePose.Hurt
                        : args[2] == "block" ? CutscenePose.Block
                        : throw Error(id, line, "자세는 idle, hurt, block 중 하나입니다.");
                    if (actor == CutsceneActor.Dummy && pose == CutscenePose.Block)
                        throw Error(id, line, "허수아비에는 막는 자세가 없습니다(idle, hurt).");
                    return CutsceneStep.ForActor(line, actor, CutsceneActorAction.Pose, pose: pose);
                }
                case "attack":
                {
                    Expect(id, line, args, 3, 3, "@actor <인물> attack slash|pierce|blunt");
                    if (actor == CutsceneActor.Dummy) throw Error(id, line, "허수아비는 공격하지 않습니다.");
                    CutsceneAttack attack = args[2] == "slash" ? CutsceneAttack.Slash : args[2] == "pierce" ? CutsceneAttack.Pierce
                        : args[2] == "blunt" ? CutsceneAttack.Blunt
                        : throw Error(id, line, "공격은 slash(베기), pierce(찌르기), blunt(내려치기) 중 하나입니다.");
                    return CutsceneStep.ForActor(line, actor, CutsceneActorAction.Attack, attack: attack, waits: waits);
                }
                default:
                    throw Error(id, line, $"'{action}'은(는) 인물 명령이 아닙니다. at, hide, move, face, pose, attack 중 하나를 적으세요.");
            }
        }

        private static void RejectNoWait(string id, int line, bool waits, string command)
        {
            if (!waits) throw Error(id, line, $"{command}는 바로 끝나므로 &를 붙이지 않습니다.");
        }

        private static void Expect(string id, int line, string[] args, int minimum, int maximum, string usage)
        {
            if (args.Length < minimum || args.Length > maximum) throw Error(id, line, $"형식은 '{usage}'입니다.");
        }

        private static CutsceneFacing Facing(string id, int line, string value)
            => value == "left" ? CutsceneFacing.Left : value == "right" ? CutsceneFacing.Right
                : throw Error(id, line, "방향은 left 또는 right입니다.");

        private static float OptionalSeconds(string id, int line, string[] args, int index)
            => args.Length > index ? Seconds(id, line, args[index]) : 0f;

        private static float Seconds(string id, int line, string value)
        {
            float seconds = Number(id, line, value, "시간(초)");
            if (seconds < 0f || seconds > CutsceneStep.MaximumSeconds)
                throw Error(id, line, $"시간은 0~{CutsceneStep.MaximumSeconds:0}초입니다.");
            return seconds;
        }

        private static float Position(string id, int line, string value)
        {
            float x = Number(id, line, value, "위치");
            if (Math.Abs(x) > CutsceneStep.MaximumX)
                throw Error(id, line, $"위치는 -{CutsceneStep.MaximumX:0}~{CutsceneStep.MaximumX:0}입니다(전투 시작 위치는 엘리사 -5, 상대 5).");
            return x;
        }

        private static float Number(string id, int line, string value, string what)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ||
                float.IsNaN(number) || float.IsInfinity(number))
                throw Error(id, line, $"{what} '{value}'을(를) 숫자로 읽을 수 없습니다.");
            return number;
        }

        private static string FirstToken(string line)
        {
            for (int index = 0; index < line.Length; index++)
                if (char.IsWhiteSpace(line[index])) return line.Substring(0, index);
            return line;
        }

        private static string[] Arguments(string line, string command)
            => line.Substring(command.Length).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        // The dialogue parser's message already names the script and line; keep only what went wrong.
        private static string Detail(string message)
        {
            int cut = message.IndexOf(": ", StringComparison.Ordinal);
            return cut >= 0 ? message.Substring(cut + 2) : message;
        }

        private static CutsceneParseException Error(string id, int line, string message)
            => new CutsceneParseException(id, line, message);
    }
}
