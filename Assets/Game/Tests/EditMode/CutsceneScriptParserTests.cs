using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Dialogue;

namespace TurnLimbo.Core.Tests
{
    public sealed class CutsceneScriptParserTests
    {
        private static CutsceneScript Parse(string source) => CutsceneScriptParser.Parse("Cutscene/test", source);

        private static CutsceneParseException Fails(string source)
            => Assert.Throws<CutsceneParseException>(() => Parse(source));

        [Test]
        public void Parse_ReadsEveryStagingCommand()
        {
            CutsceneScript script = Parse(string.Join("\n",
                "@fade out 0",
                "@bars on 0.5 &",
                "@camera -4 3.5 2",
                "@camera reset",
                "@image ForestArena/forest-far-mist 1",
                "@image off",
                "@actor elise at -4 right",
                "@actor elise face left",
                "@actor elise pose block",
                "@actor elise move 1.5 1.2 &",
                "@actor elise attack pierce",
                "@actor dummy at 3",
                "@actor dummy pose hurt",
                "@actor dummy hide",
                "@actor knight at 4 left",
                "@actor knight attack blunt &",
                "@wait 0.42"));
            CutsceneStep[] steps = script.Steps.ToArray();
            Assert.That(steps.Length, Is.EqualTo(17));
            Assert.That(script.LineCount, Is.Zero, "A cutscene may be staging only.");
            Assert.That(steps.Select(step => step.SourceLineNumber), Is.EqualTo(Enumerable.Range(1, 17)));

            Assert.That(steps[0].Kind, Is.EqualTo(CutsceneStepKind.Fade));
            Assert.That(steps[0].ToBlack && steps[0].Seconds == 0f && steps[0].Waits, Is.True);
            Assert.That(steps[1].Kind == CutsceneStepKind.Bars && steps[1].BarsOn, Is.True);
            Assert.That(steps[1].Waits, Is.False, "A trailing & does not hold the cutscene.");
            Assert.That(steps[1].HoldSeconds, Is.Zero);
            Assert.That(steps[2].X, Is.EqualTo(-4f));
            Assert.That(steps[2].CameraSize, Is.EqualTo(3.5f));
            Assert.That(steps[2].HoldSeconds, Is.EqualTo(2f));
            Assert.That(steps[3].X == 0f && steps[3].CameraSize == CutsceneStep.DefaultCameraSize, Is.True, "reset is the duel framing.");
            Assert.That(steps[4].Resource, Is.EqualTo("ForestArena/forest-far-mist"));
            Assert.That(steps[5].Kind == CutsceneStepKind.Image && steps[5].Resource == null, Is.True);

            Assert.That(steps[6].Action, Is.EqualTo(CutsceneActorAction.Place));
            Assert.That(steps[6].Facing, Is.EqualTo(CutsceneFacing.Right));
            Assert.That(steps[7].Facing, Is.EqualTo(CutsceneFacing.Left));
            Assert.That(steps[8].Pose, Is.EqualTo(CutscenePose.Block));
            Assert.That(steps[9].Action == CutsceneActorAction.Move && steps[9].X == 1.5f && steps[9].Seconds == 1.2f, Is.True);
            Assert.That(steps[9].Waits, Is.False);
            Assert.That(steps[10].Attack, Is.EqualTo(CutsceneAttack.Pierce));
            Assert.That(steps[10].HoldSeconds, Is.EqualTo(CutsceneStep.AttackSeconds), "A stroke holds for its length.");
            Assert.That(steps[11].Actor == CutsceneActor.Dummy && steps[11].Facing == CutsceneFacing.Unchanged, Is.True);
            Assert.That(steps[13].Action, Is.EqualTo(CutsceneActorAction.Hide));
            Assert.That(steps[14].Actor, Is.EqualTo(CutsceneActor.Knight));
            Assert.That(steps[15].Attack == CutsceneAttack.Blunt && !steps[15].Waits, Is.True);
            Assert.That(steps[16].Kind == CutsceneStepKind.Wait && steps[16].HoldSeconds == .42f, Is.True);
        }

        [Test]
        public void Parse_KeepsDialogueGrammar_AndMergesLinesWithStagingInSourceOrder()
        {
            CutsceneScript script = Parse(string.Join("\n",
                "﻿# 메모",
                "@fade out 0",
                "첫 줄",
                "@left 엘리제 | 깨어난 사람",
                "@actor elise at -3",
                "둘째 줄",
                "@show right 떠돌이 기사",
                "@wait 1",
                "\\@셋째 줄",
                "@hide right"));
            CutsceneStep[] steps = script.Steps.ToArray();
            Assert.That(steps.Select(step => step.Kind), Is.EqualTo(new[]
            {
                CutsceneStepKind.Fade, CutsceneStepKind.Line, CutsceneStepKind.Actor, CutsceneStepKind.Line,
                CutsceneStepKind.Wait, CutsceneStepKind.Line,
            }));
            Assert.That(script.LineCount, Is.EqualTo(3));
            DialogueLine first = steps[1].Line, second = steps[3].Line, third = steps[5].Line;
            Assert.That(first.Side, Is.EqualTo(DialogueSide.Narrator), "Lines before a speaker are narration.");
            Assert.That(first.SourceLineNumber, Is.EqualTo(3));
            Assert.That(second.SpeakerName, Is.EqualTo("엘리제"));
            Assert.That(second.SpeakerRole, Is.EqualTo("깨어난 사람"));
            Assert.That(third.Text, Is.EqualTo("@셋째 줄"), "Escapes still work.");
            Assert.That(third.Stage.Right.SpeakerName, Is.EqualTo("떠돌이 기사"), "Stage directives still carry over.");
            Assert.That(third.SourceLineNumber, Is.EqualTo(9));
        }

        [Test]
        public void Parse_ReportsDialogueErrorsAsCutsceneErrorsAtTheirLine()
        {
            CutsceneParseException hidden = Fails("@fade out 0\n@hide left\n");
            Assert.That(hidden.LineNumber, Is.EqualTo(2), "A trailing stage directive is still checked.");
            Assert.That(hidden.Message, Does.StartWith("컷신 'Cutscene/test', 2번째 줄: "));
            Assert.That(hidden.InnerException, Is.InstanceOf<DialogueParseException>());
            Assert.That(Fails("@wait 1\n" + new string('가', DialogueLine.MaxTextLength + 1)).LineNumber, Is.EqualTo(2));
            CutsceneParseException unknown = Fails("@wait 1\n@zoom 3\n");
            Assert.That(unknown.LineNumber, Is.EqualTo(2));
            Assert.That(unknown.Message, Does.Contain("@zoom").And.Contain("@actor"));
            Assert.That(Fails("@Fade out 1").LineNumber, Is.EqualTo(1), "Directives are lower case.");
            CutsceneParseException empty = Fails("# 메모만\n\n");
            Assert.That(empty.LineNumber, Is.Zero);
        }

        [TestCase("@wait", 1)]
        [TestCase("@wait 1 &", 1)]
        [TestCase("@wait -1", 1)]
        [TestCase("@wait 31", 1)]
        [TestCase("@wait 1,5", 1)]
        [TestCase("@fade dark 1", 1)]
        [TestCase("@fade out", 1)]
        [TestCase("@bars up", 1)]
        [TestCase("@camera 0 1.5", 1)]
        [TestCase("@camera 0 7", 1)]
        [TestCase("@camera 40 4", 1)]
        [TestCase("@camera reset 1 2", 1)]
        [TestCase("@image off", 1)]
        [TestCase("@image", 1)]
        [TestCase("@actor ghost at 0", 1)]
        [TestCase("@actor elise", 1)]
        [TestCase("@actor elise hide", 1)]
        [TestCase("@actor elise move 0 1", 1)]
        [TestCase("@actor elise at 0 up", 1)]
        [TestCase("@actor elise at 0 &", 1)]
        [TestCase("@actor elise at 0\n@actor elise dance", 2)]
        [TestCase("@actor elise at 0\n@actor elise pose sit", 2)]
        [TestCase("@actor elise at 0\n@actor elise attack kick", 2)]
        [TestCase("@actor elise at 0\n@actor elise face left &", 2)]
        [TestCase("@actor elise at 0\n@actor elise hide\n@actor elise pose idle", 3)]
        [TestCase("@actor dummy at 3\n@actor dummy attack slash", 2)]
        [TestCase("@actor dummy at 3\n@actor dummy pose block", 2)]
        [TestCase("@actor dummy at 3\n@actor dummy move 0 1", 2)]
        [TestCase("@actor dummy at 3\n@actor knight at 4", 2)]
        [TestCase("@actor knight at 3\n@actor dummy at 4", 2)]
        public void Parse_RejectsBadStagingAtItsLine(string source, int line)
        {
            Assert.That(Fails(source).LineNumber, Is.EqualTo(line));
        }

        [Test]
        public void Parse_AllowsTheKnightAndDummyToTakeTurns_AndRepositioningAVisibleFigure()
        {
            CutsceneScript script = Parse(string.Join("\n",
                "@actor dummy at 3", "@actor dummy hide", "@actor knight at 4", "@actor knight at 2 right",
                "@image LobbyRoom/room", "@image ForestArena/forest-far", "@image off 1"));
            Assert.That(script.Steps.Count, Is.EqualTo(7));
        }

        [Test]
        public void Steps_ClampTheirOwnInvariants()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForWait(0, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForWait(1, -1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForCamera(1, 0f, 9f, 0f, true));
            Assert.Throws<System.ArgumentException>(() => CutsceneStep.ForImage(1, " ", 0f, true));
            Assert.Throws<System.ArgumentException>(() => new CutsceneScript("x", new CutsceneStep[0]));
            CutsceneStep pose = CutsceneStep.ForActor(1, CutsceneActor.Elise, CutsceneActorAction.Pose, seconds: 5f);
            Assert.That(pose.Seconds, Is.Zero, "Instant actor steps never hold.");
        }
    }
}
