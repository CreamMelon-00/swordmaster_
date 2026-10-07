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
                "@actor elisa at -4 right",
                "@actor elisa face left",
                "@actor elisa pose block",
                "@actor elisa move 1.5 1.2 &",
                "@actor elisa attack pierce",
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
                "@left 엘리사 | 깨어난 사람",
                "@actor elisa at -3",
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
            Assert.That(second.SpeakerName, Is.EqualTo("엘리사"));
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
            Assert.That(unknown.Message, Does.Contain("@zoom").And.Contain("@actor").And.Contain("@flashback").And.Contain("@aura")
                .And.Contain("@recall"));
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
        [TestCase("@actor elisa", 1)]
        [TestCase("@actor elisa hide", 1)]
        [TestCase("@actor elisa move 0 1", 1)]
        [TestCase("@actor elisa at 0 up", 1)]
        [TestCase("@actor elisa at 0 &", 1)]
        [TestCase("@actor elisa at 0\n@actor elisa dance", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa pose sit", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa attack kick", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa face left &", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa hide\n@actor elisa pose idle", 3)]
        [TestCase("@actor dummy at 3\n@actor dummy attack slash", 2)]
        [TestCase("@actor dummy at 3\n@actor dummy pose block", 2)]
        [TestCase("@actor dummy at 3\n@actor dummy move 0 1", 2)]
        [TestCase("@actor dummy at 3\n@actor knight at 4", 2)]
        [TestCase("@actor knight at 3\n@actor dummy at 4", 2)]
        [TestCase("@actor senior at 0\n@actor senior pose hurt", 2)]
        [TestCase("@actor senior at 0\n@actor senior pose block", 2)]
        [TestCase("@actor senior move 0 1", 1)]
        [TestCase("@flashback", 1)]
        [TestCase("@flashback dim", 1)]
        [TestCase("@flashback on 1 2", 1)]
        [TestCase("@flashback on -1", 1)]
        [TestCase("@shake", 1)]
        [TestCase("@shake big", 1)]
        [TestCase("@shake 0", 1)]
        [TestCase("@shake -0.2", 1)]
        [TestCase("@shake 1.6", 1)]
        [TestCase("@shake 0.3 0", 1)]
        [TestCase("@shake 0.3 31", 1)]
        [TestCase("@sound", 1)]
        [TestCase("@sound charge &", 1)]
        [TestCase("@sound charge 1.5", 1)]
        [TestCase("@sound charge loud", 1)]
        [TestCase("@sound charge 1 2", 1)]
        [TestCase("@ambience", 1)]
        [TestCase("@ambience off", 1)]
        [TestCase("@ambience aura-loop 1 2", 1)]
        [TestCase("@ambience aura-loop\n@ambience off 1\n@ambience off", 3)]
        [TestCase("@charge", 1)]
        [TestCase("@charge elisa 1", 1)]
        [TestCase("@actor elisa at 0\n@charge elisa", 2)]
        [TestCase("@actor elisa at 0\n@charge elisa 0", 2)]
        [TestCase("@actor elisa at 0\n@charge elisa soon", 2)]
        [TestCase("@actor elisa at 0\n@charge elisa stop &", 2)]
        [TestCase("@actor elisa at 0\n@charge elisa 1 2", 2)]
        [TestCase("@actor elisa at 0\n@charge elisa 1 hold 2", 2)]
        [TestCase("@actor elisa at 0\n@charge elisa stop hold", 2)]
        [TestCase("@actor elisa at 0\n@charge elisa hold", 2)]
        [TestCase("@actor elisa at 0\n@charge ghost 1", 2)]
        [TestCase("@aura knight on", 1)]
        [TestCase("@actor knight at 3\n@aura knight", 2)]
        [TestCase("@actor knight at 3\n@aura knight glow", 2)]
        [TestCase("@actor knight at 3\n@aura knight on 1 2", 2)]
        [TestCase("@actor knight at 3\n@actor knight hide\n@aura knight off", 3)]
        [TestCase("@actor elisa tremble 1", 1)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble 0", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble -1", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble 31", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble soon", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble 1 0", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble 1 -0.1", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble 1 0.6", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble 1 big", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa tremble 1 0.1 2", 2)]
        [TestCase("@actor elisa at 0\n@actor elisa hide\n@actor elisa tremble 1", 3)]
        [TestCase("@recall 0", 1)]
        [TestCase("@recall -1", 1)]
        [TestCase("@recall 31", 1)]
        [TestCase("@recall soon", 1)]
        [TestCase("@recall 1 2", 1)]
        [TestCase("@Recall", 1)]
        public void Parse_RejectsBadStagingAtItsLine(string source, int line)
        {
            Assert.That(Fails(source).LineNumber, Is.EqualTo(line));
        }

        [Test]
        public void Parse_ReadsTheEffectAndSoundCommands()
        {
            CutsceneScript script = Parse(string.Join("\n",
                "@flashback on 1.5",
                "@flashback off &",
                "@shake 0.4",
                "@shake 1.5 2 &",
                "@sound charge",
                "@sound charge-cut 0.6",
                "@ambience aura-loop 2 &",
                "@ambience off 1",
                "@actor knight at 4 left",
                "@charge knight 2 &",
                "@charge knight stop",
                "@charge knight 1.5",
                "@aura knight on 1",
                "@aura knight off 0.5 &",
                "@aura knight off",
                "@charge knight 2.5 hold &",
                "@charge knight 1 hold"));
            CutsceneStep[] steps = script.Steps.ToArray();
            Assert.That(steps.Length, Is.EqualTo(17));

            Assert.That(steps[0].Kind == CutsceneStepKind.Flashback && steps[0].FlashbackOn, Is.True);
            Assert.That(steps[0].HoldSeconds, Is.EqualTo(1.5f));
            Assert.That(steps[1].FlashbackOn || steps[1].Waits, Is.False);
            Assert.That(steps[1].Seconds, Is.Zero, "Without seconds the colour changes at once.");

            Assert.That(steps[2].Kind, Is.EqualTo(CutsceneStepKind.Shake));
            Assert.That(steps[2].Strength, Is.EqualTo(.4f));
            Assert.That(steps[2].HoldSeconds, Is.EqualTo(CutsceneStep.DefaultShakeSeconds), "A shake has a default length and holds for it.");
            Assert.That(steps[3].Strength == CutsceneStep.MaximumShake && steps[3].Seconds == 2f && !steps[3].Waits, Is.True);

            Assert.That(steps[4].Kind, Is.EqualTo(CutsceneStepKind.Sound));
            Assert.That(steps[4].Resource, Is.EqualTo("Sfx/charge"), "Sounds are named under Resources/Sfx.");
            Assert.That(steps[4].Volume, Is.EqualTo(1f));
            Assert.That(steps[4].HoldSeconds, Is.Zero, "A sound never holds the cutscene.");
            Assert.That(steps[5].Resource == "Sfx/charge-cut" && steps[5].Volume == .6f, Is.True);

            Assert.That(steps[6].Kind == CutsceneStepKind.Ambience && steps[6].Resource == "Sfx/aura-loop", Is.True);
            Assert.That(steps[6].Seconds == 2f && !steps[6].Waits, Is.True);
            Assert.That(steps[7].Resource, Is.Null, "off fades the loop out.");
            Assert.That(steps[7].HoldSeconds, Is.EqualTo(1f));

            Assert.That(steps[9].Kind == CutsceneStepKind.Charge && steps[9].Actor == CutsceneActor.Knight, Is.True);
            Assert.That(steps[9].ChargeStops || steps[9].Waits, Is.False);
            Assert.That(steps[9].Seconds, Is.EqualTo(2f));
            Assert.That(steps[10].ChargeStops, Is.True);
            Assert.That(steps[10].HoldSeconds, Is.Zero, "Cutting a charge off is instant.");
            Assert.That(steps[11].HoldSeconds, Is.EqualTo(1.5f), "A charge holds for its length.");
            Assert.That(steps[9].ChargeHolds || steps[11].ChargeHolds, Is.False, "A charge releases on its own unless held.");

            Assert.That(steps[12].Kind == CutsceneStepKind.Aura && steps[12].AuraOn && steps[12].HoldSeconds == 1f, Is.True);
            Assert.That(steps[13].AuraOn || steps[13].Waits, Is.False);
            Assert.That(steps[14].AuraOn, Is.False);
            Assert.That(steps[14].Seconds, Is.Zero);

            Assert.That(steps[15].Kind == CutsceneStepKind.Charge && steps[15].ChargeHolds && !steps[15].ChargeStops, Is.True);
            Assert.That(steps[15].Seconds == 2.5f && !steps[15].Waits, Is.True);
            Assert.That(steps[16].ChargeHolds, Is.True);
            Assert.That(steps[16].HoldSeconds, Is.EqualTo(1f), "A held charge holds the cutscene only until it is full.");
        }

        [Test]
        public void Parse_ReadsTremble_ForEveryFigure_WithASlightDefaultStrength()
        {
            CutsceneScript script = Parse(string.Join("\n",
                "@actor elisa at -5 right",
                "@actor elisa tremble 0.6",
                "@actor elisa tremble 1.2 0.15 &",
                "@actor dummy at 3",
                "@actor dummy tremble 0.4",
                "@actor dummy hide",
                "@actor knight at 4 left",
                "@actor knight tremble .5 .5",
                "@actor senior at -8",
                "@actor senior tremble 1 &"));
            CutsceneStep[] steps = script.Steps.ToArray();
            Assert.That(steps.Length, Is.EqualTo(10));

            CutsceneStep slight = steps[1];
            Assert.That(slight.Kind == CutsceneStepKind.Actor && slight.Action == CutsceneActorAction.Tremble, Is.True);
            Assert.That(slight.Actor, Is.EqualTo(CutsceneActor.Elisa));
            Assert.That(slight.Seconds, Is.EqualTo(.6f));
            Assert.That(slight.HoldSeconds, Is.EqualTo(.6f), "A tremble holds for its time…");
            Assert.That(slight.Strength, Is.EqualTo(CutsceneStep.DefaultTrembleStrength), "…and is slight unless told otherwise.");
            Assert.That(CutsceneStep.DefaultTrembleStrength, Is.LessThan(.1f));
            Assert.That(slight.X == 0f && slight.Facing == CutsceneFacing.Unchanged, Is.True, "It moves no place and no facing.");

            Assert.That(steps[2].Strength == .15f && steps[2].Seconds == 1.2f, Is.True);
            Assert.That(steps[2].Waits, Is.False, "& lets the scene go on while she shivers.");
            Assert.That(steps[2].HoldSeconds, Is.Zero);
            Assert.That(steps[4].Actor == CutsceneActor.Dummy && steps[4].Action == CutsceneActorAction.Tremble, Is.True,
                "The dummy, which cannot move, can still shake.");
            Assert.That(steps[7].Actor == CutsceneActor.Knight && steps[7].Strength == CutsceneStep.MaximumTremble, Is.True);
            Assert.That(steps[9].Actor == CutsceneActor.Senior && !steps[9].Waits, Is.True);

            Assert.That(Fails("@actor elisa at 0\n@actor elisa tremble 1 0.9").Message, Does.Contain("떨림 세기"));
            Assert.That(Fails("@actor elisa at 0\n@actor elisa tremble 0").Message, Does.Contain("떠는 시간"));
            Assert.That(Fails("@actor elisa at 0\n@actor elisa sway 1").Message, Does.Contain("tremble"));
        }

        [Test]
        public void Parse_ReadsRecall_WithItsDefaultTime()
        {
            CutsceneScript script = Parse("@recall\n@recall 2.5\n@recall 0.8 &\n하나");
            CutsceneStep[] steps = script.Steps.ToArray();
            Assert.That(steps.Length, Is.EqualTo(4));
            Assert.That(steps[0].Kind, Is.EqualTo(CutsceneStepKind.Recall));
            Assert.That(steps[0].Seconds, Is.EqualTo(CutsceneStep.DefaultRecallSeconds));
            Assert.That(steps[0].HoldSeconds, Is.EqualTo(1.5f), "A recall holds about a second and a half unless told otherwise.");
            Assert.That(steps[1].Kind == CutsceneStepKind.Recall && steps[1].HoldSeconds == 2.5f, Is.True);
            Assert.That(steps[2].Seconds == .8f && !steps[2].Waits && steps[2].HoldSeconds == 0f, Is.True);
            Assert.That(steps[0].Resource, Is.Null, "Which screen comes back is the game's choice, not the script's.");
            Assert.That(Fails("@recall 0").Message, Does.Contain("떠올리는 시간"));
        }

        [Test]
        public void Parse_TheSeniorKnightHasHisOwnPlace_AndOnlyHisArtsMoves()
        {
            CutsceneScript script = Parse(string.Join("\n",
                "@actor elisa at -5 right",
                "@actor knight at 5 left",
                "@actor senior at -14 right",
                "@actor senior move -8 1.5",
                "@actor senior face left",
                "@actor senior pose idle",
                "@actor senior attack slash &",
                "@charge senior 1",
                "@aura senior on",
                "@actor senior hide",
                "@actor knight hide"));
            Assert.That(script.Steps.Count, Is.EqualTo(11), "Elisa, the knight and the senior knight stand together.");
            Assert.That(script.Steps[2].Actor == CutsceneActor.Senior && script.Steps[2].Action == CutsceneActorAction.Place, Is.True);
            Assert.That(script.Steps[3].Seconds, Is.EqualTo(1.5f));
            Assert.That(script.Steps[6].Attack == CutsceneAttack.Slash && !script.Steps[6].Waits, Is.True);
            Assert.That(Fails("@actor senior at 0\n@actor senior pose hurt").Message, Does.Contain("상급기사"));
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
        public void Parse_AStagedScene_StartsWithItsCastStanding_AndMayRestageIt()
        {
            var cast = new[] { CutsceneActor.Elisa, CutsceneActor.Knight };
            CutsceneScript script = CutsceneScriptParser.Parse("Cutscene/test", string.Join("\n",
                "@actor knight move 3 1",
                "@charge knight 1 &",
                "@aura knight on",
                "@actor elisa face left",
                "@actor elisa pose hurt",
                "하나",
                "@actor knight hide",
                "@actor dummy at 4",
                "@actor elisa at -2 right"), cast);
            Assert.That(script.OnStage, Is.EqualTo(cast), "The scene says who already stands on stage.");
            Assert.That(script.StartsOnStage(CutsceneActor.Elisa) && script.StartsOnStage(CutsceneActor.Knight), Is.True);
            Assert.That(script.StartsOnStage(CutsceneActor.Dummy) || script.StartsOnStage(CutsceneActor.Senior), Is.False);
            Assert.That(script.Steps.Count, Is.EqualTo(9));
            Assert.That(script.Steps[0].Action, Is.EqualTo(CutsceneActorAction.Move), "No @actor … at is needed first.");

            Assert.That(Parse("하나").OnStage, Is.Empty, "A plain scene starts on a bare stage.");
            Assert.That(Fails("@actor knight move 3 1").Message, Does.Contain("무대에 없습니다"));
            CutsceneParseException shared = Assert.Throws<CutsceneParseException>(() => CutsceneScriptParser.Parse(
                "Cutscene/test", "@actor dummy at 3", new[] { CutsceneActor.Elisa, CutsceneActor.Knight }));
            Assert.That(shared.Message, Does.Contain("같은 자리"), "The standing knight holds the figure the dummy would use.");
            Assert.That(CutsceneScriptParser.Parse("Cutscene/test", "@actor dummy pose hurt",
                new[] { CutsceneActor.Elisa, CutsceneActor.Dummy }).Steps.Count, Is.EqualTo(1));
        }

        [Test]
        public void Parse_RefusesAnImpossibleStartingCast()
        {
            Assert.Throws<System.ArgumentException>(() => CutsceneScriptParser.Parse("Cutscene/test", "하나",
                new[] { CutsceneActor.Knight, CutsceneActor.Dummy }), "The knight and the dummy share one figure.");
            Assert.Throws<System.ArgumentException>(() => CutsceneScriptParser.Parse("Cutscene/test", "하나",
                new[] { CutsceneActor.Elisa, CutsceneActor.Elisa }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CutsceneScript("x",
                new[] { CutsceneStep.ForWait(1, 1f) }, new[] { (CutsceneActor)9 }));
            Assert.That(new CutsceneScript("x", new[] { CutsceneStep.ForWait(1, 1f) }, null).OnStage, Is.Empty);
        }

        [Test]
        public void Steps_ClampTheirOwnInvariants()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForWait(0, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForWait(1, -1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForCamera(1, 0f, 9f, 0f, true));
            Assert.Throws<System.ArgumentException>(() => CutsceneStep.ForImage(1, " ", 0f, true));
            Assert.Throws<System.ArgumentException>(() => new CutsceneScript("x", new CutsceneStep[0]));
            CutsceneStep pose = CutsceneStep.ForActor(1, CutsceneActor.Elisa, CutsceneActorAction.Pose, seconds: 5f);
            Assert.That(pose.Seconds, Is.Zero, "Instant actor steps never hold.");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForShake(1, 0f, 1f, true));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForShake(1, CutsceneStep.MaximumShake + .1f, 1f, true));
            Assert.Throws<System.ArgumentException>(() => CutsceneStep.ForSound(1, " "));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForSound(1, "charge", 1.5f));
            Assert.Throws<System.ArgumentException>(() => CutsceneStep.ForAmbience(1, " ", 0f, true));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForCharge(1, CutsceneActor.Knight, 0f, true));
            Assert.That(CutsceneStep.ForChargeStop(1, CutsceneActor.Knight).HoldSeconds, Is.Zero);
            Assert.That(CutsceneStep.ForAmbience(1, null, 2f, true).Resource, Is.Null);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForTremble(1, CutsceneActor.Elisa, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForTremble(1, CutsceneActor.Elisa, 1f, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                CutsceneStep.ForTremble(1, CutsceneActor.Elisa, 1f, CutsceneStep.MaximumTremble + .1f));
            Assert.Throws<System.ArgumentException>(() =>
                CutsceneStep.ForActor(1, CutsceneActor.Elisa, CutsceneActorAction.Tremble, seconds: 1f), "A tremble needs its strength.");
            Assert.That(CutsceneStep.ForTremble(1, CutsceneActor.Dummy, .5f).Strength, Is.EqualTo(CutsceneStep.DefaultTrembleStrength));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CutsceneStep.ForRecall(1, 0f));
            Assert.That(CutsceneStep.ForRecall(1).HoldSeconds, Is.EqualTo(CutsceneStep.DefaultRecallSeconds));
        }
    }
}
