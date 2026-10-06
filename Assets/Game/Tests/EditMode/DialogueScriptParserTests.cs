using NUnit.Framework;
using TurnLimbo.Runtime.Dialogue;

namespace TurnLimbo.Core.Tests
{
    public sealed class DialogueScriptParserTests
    {
        [Test]
        public void Parse_DefaultsToNarratorAndPreservesKoreanPunctuation()
        {
            DialogueScript script = DialogueScriptParser.Parse("opening", "안녕, \"검\" & 방패…");

            Assert.That(script.Id, Is.EqualTo("opening"));
            Assert.That(script.Lines.Count, Is.EqualTo(1));
            Assert.That(script.Lines[0].Side, Is.EqualTo(DialogueSide.Narrator));
            Assert.That(script.Lines[0].SpeakerName, Is.Empty);
            Assert.That(script.Lines[0].Text, Is.EqualTo("안녕, \"검\" & 방패…"));
            Assert.That(script.Lines[0].SourceLineNumber, Is.EqualTo(1));
            Assert.That(script.Lines[0].Stage.Left, Is.Null);
            Assert.That(script.Lines[0].Stage.Right, Is.Null);
            Assert.That(DialogueStageSnapshot.Empty.Left, Is.Null);
            Assert.That(DialogueStageSnapshot.Empty.Right, Is.Null);
        }

        [Test]
        public void Parse_RemembersLeftAndRightSpeakersIndependently()
        {
            DialogueScript script = DialogueScriptParser.Parse("sides",
                "@left 왼쪽 이름 | 역할 A\n왼쪽 첫 대사\n@right 오른쪽 이름 | 역할 B\n오른쪽 대사\n@left\n왼쪽 재등장\n@narrator\n설명\n@right\n오른쪽 재등장");

            Assert.That(script.Lines.Count, Is.EqualTo(5));
            Assert.That(script.Lines[0].Side, Is.EqualTo(DialogueSide.Left));
            Assert.That(script.Lines[0].SpeakerName, Is.EqualTo("왼쪽 이름"));
            Assert.That(script.Lines[0].SpeakerRole, Is.EqualTo("역할 A"));
            Assert.That(script.Lines[0].Stage.Left.SpeakerName, Is.EqualTo("왼쪽 이름"));
            Assert.That(script.Lines[0].Stage.Left.SpeakerRole, Is.EqualTo("역할 A"));
            Assert.That(script.Lines[0].Stage.Right, Is.Null);
            Assert.That(script.Lines[1].Side, Is.EqualTo(DialogueSide.Right));
            Assert.That(script.Lines[1].Stage.Left.SpeakerName, Is.EqualTo("왼쪽 이름"));
            Assert.That(script.Lines[1].Stage.Right.SpeakerName, Is.EqualTo("오른쪽 이름"));
            Assert.That(script.Lines[1].Stage.Right.SpeakerRole, Is.EqualTo("역할 B"));
            Assert.That(script.Lines[2].SpeakerName, Is.EqualTo("왼쪽 이름"));
            Assert.That(script.Lines[3].Side, Is.EqualTo(DialogueSide.Narrator));
            Assert.That(script.Lines[3].Stage.Left.SpeakerName, Is.EqualTo("왼쪽 이름"));
            Assert.That(script.Lines[3].Stage.Right.SpeakerName, Is.EqualTo("오른쪽 이름"));
            Assert.That(script.Lines[4].SpeakerName, Is.EqualTo("오른쪽 이름"));
        }

        [Test]
        public void Parse_EachPhysicalTextLineBecomesOneAdvanceStep()
        {
            DialogueScript script = DialogueScriptParser.Parse("steps", "@left 화자\n첫 줄\n둘째 줄\n셋째 줄");

            Assert.That(script.Lines.Count, Is.EqualTo(3));
            Assert.That(script.Lines[0].Text, Is.EqualTo("첫 줄"));
            Assert.That(script.Lines[1].Text, Is.EqualTo("둘째 줄"));
            Assert.That(script.Lines[2].Text, Is.EqualTo("셋째 줄"));
        }

        [Test]
        public void Parse_IgnoresCommentsAndBlankLinesAndUnescapesLiteralMarkers()
        {
            DialogueScript script = DialogueScriptParser.Parse("comments",
                "# 작성 메모\n\n\\@표시할 계정\n  \\#표시할 해시  \n# 끝 메모");

            Assert.That(script.Lines.Count, Is.EqualTo(2));
            Assert.That(script.Lines[0].Text, Is.EqualTo("@표시할 계정"));
            Assert.That(script.Lines[1].Text, Is.EqualTo("#표시할 해시"));
        }

        [Test]
        public void Parse_AcceptsBomAndAllCommonLineEndingsWithOriginalLineNumbers()
        {
            DialogueScript script = DialogueScriptParser.Parse("line-endings",
                "\ufeff# 메모\r\n@narrator\r첫 줄\n\n둘째 줄");

            Assert.That(script.Lines.Count, Is.EqualTo(2));
            Assert.That(script.Lines[0].SourceLineNumber, Is.EqualTo(3));
            Assert.That(script.Lines[1].SourceLineNumber, Is.EqualTo(5));
        }

        [Test]
        public void Parse_RedefiningSpeakerDoesNotChangeEarlierLines()
        {
            DialogueScript script = DialogueScriptParser.Parse("redefine",
                "@left 이전 이름 | 이전 역할\n이전 대사\n@left 새 이름 | 새 역할\n새 대사");

            Assert.That(script.Lines[0].SpeakerName, Is.EqualTo("이전 이름"));
            Assert.That(script.Lines[0].SpeakerRole, Is.EqualTo("이전 역할"));
            Assert.That(script.Lines[1].SpeakerName, Is.EqualTo("새 이름"));
            Assert.That(script.Lines[1].SpeakerRole, Is.EqualTo("새 역할"));
            Assert.That(script.Lines[0].Stage.Left.SpeakerName, Is.EqualTo("이전 이름"),
                "Later declarations must not mutate an earlier stage snapshot.");
            Assert.That(script.Lines[1].Stage.Left.SpeakerName, Is.EqualTo("새 이름"));
        }

        [Test]
        public void Parse_StageCommandsPersistWithoutAddingAdvanceSteps()
        {
            DialogueScript script = DialogueScriptParser.Parse("stage-commands",
                "@show left 왼쪽 이름 | 왼쪽 역할\n" +
                "@show right 오른쪽 이름 | 오른쪽 역할\n" +
                "@narrator\n둘 다 등장\n" +
                "@hide left\n왼쪽 퇴장\n" +
                "@move right left\n오른쪽에서 왼쪽으로 이동");

            Assert.That(script.Lines.Count, Is.EqualTo(3),
                "Stage directives must not become player advance steps.");
            Assert.That(script.Lines[0].Stage.Left.SpeakerName, Is.EqualTo("왼쪽 이름"));
            Assert.That(script.Lines[0].Stage.Left.SpeakerRole, Is.EqualTo("왼쪽 역할"));
            Assert.That(script.Lines[0].Stage.Right.SpeakerName, Is.EqualTo("오른쪽 이름"));
            Assert.That(script.Lines[0].Stage.Right.SpeakerRole, Is.EqualTo("오른쪽 역할"));

            Assert.That(script.Lines[1].Stage.Left, Is.Null);
            Assert.That(script.Lines[1].Stage.Right.SpeakerName, Is.EqualTo("오른쪽 이름"));

            Assert.That(script.Lines[2].Stage.Left.SpeakerName, Is.EqualTo("오른쪽 이름"));
            Assert.That(script.Lines[2].Stage.Left.SpeakerRole, Is.EqualTo("오른쪽 역할"));
            Assert.That(script.Lines[2].Stage.Right, Is.Null);
            Assert.That(script.Lines[0].Stage.Left.SpeakerName, Is.EqualTo("왼쪽 이름"),
                "Hide and move commands must not mutate an earlier stage snapshot.");
        }

        [Test]
        public void Parse_LegacySpeakerDirectivesAllowTheSameNameInBothSlots()
        {
            DialogueScript script = DialogueScriptParser.Parse("same-name",
                "@right 같은 이름\n오른쪽 대사\n@left 같은 이름\n왼쪽 대사");

            Assert.That(script.Lines.Count, Is.EqualTo(2));
            Assert.That(script.Lines[1].Stage.Left.SpeakerName, Is.EqualTo("같은 이름"));
            Assert.That(script.Lines[1].Stage.Right.SpeakerName, Is.EqualTo("같은 이름"));
        }

        [TestCase("@show center 이름\n대사", 1)]
        [TestCase("@show left\n대사", 1)]
        [TestCase("@hide narrator\n대사", 1)]
        [TestCase("@move left left\n대사", 1)]
        [TestCase("@move left right\n대사", 1)]
        [TestCase("@show left 왼쪽\n@show right 오른쪽\n@move left right\n대사", 3)]
        public void Parse_RejectsInvalidStageCommandsAtTheirSourceLine(string source, int expectedLine)
        {
            DialogueParseException error = Assert.Throws<DialogueParseException>(() =>
                DialogueScriptParser.Parse("broken-stage", source));

            Assert.That(error.LineNumber, Is.EqualTo(expectedLine));
        }

        [Test]
        public void Parse_ReportsUnknownDirectiveAndSourceLine()
        {
            DialogueParseException error = Assert.Throws<DialogueParseException>(() =>
                DialogueScriptParser.Parse("broken", "설명\n@unknown 값"));

            Assert.That(error.ScriptId, Is.EqualTo("broken"));
            Assert.That(error.LineNumber, Is.EqualTo(2));
            Assert.That(error.Message, Does.Contain("2번째 줄").And.Contain("@unknown"));
        }

        [TestCase("@left")]
        [TestCase("@right")]
        [TestCase("@left | 역할")]
        [TestCase("@right 이름 |")]
        [TestCase("@left 이름 | 역할 | 추가")]
        public void Parse_RejectsInvalidSpeakerDirectives(string source)
        {
            Assert.Throws<DialogueParseException>(() => DialogueScriptParser.Parse("broken-speaker", source));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("# 주석만 있음")]
        [TestCase("@narrator")]
        public void Parse_RejectsFilesWithoutDialogueText(string source)
        {
            DialogueParseException error = Assert.Throws<DialogueParseException>(() =>
                DialogueScriptParser.Parse("empty", source));
            Assert.That(error.LineNumber, Is.Zero);
        }

        [Test]
        public void Parse_RejectsOverlongDisplayStepWithItsSourceLine()
        {
            string source = "# 메모\n" + new string('가', DialogueLine.MaxTextLength + 1);

            DialogueParseException error = Assert.Throws<DialogueParseException>(() =>
                DialogueScriptParser.Parse("too-long", source));

            Assert.That(error.LineNumber, Is.EqualTo(2));
            Assert.That(error.Message, Does.Contain(DialogueLine.MaxTextLength.ToString()));
        }

        [Test]
        public void Session_AdvancesCompletesRestartsAndSkipsIdempotently()
        {
            DialogueScript script = DialogueScriptParser.Parse("session", "첫 줄\n둘째 줄\n셋째 줄");
            var session = new DialogueSession(script);

            Assert.That(session.Current.Text, Is.EqualTo("첫 줄"));
            Assert.That(session.CurrentIndex, Is.Zero);
            Assert.That(session.MoveNext(), Is.True);
            Assert.That(session.Current.Text, Is.EqualTo("둘째 줄"));
            Assert.That(session.MoveNext(), Is.True);
            Assert.That(session.MoveNext(), Is.False);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.Current, Is.Null);
            Assert.That(session.MoveNext(), Is.False);

            session.Restart();
            Assert.That(session.Current.Text, Is.EqualTo("첫 줄"));
            session.SkipToEnd();
            session.SkipToEnd();
            Assert.That(session.IsComplete, Is.True);
        }

        [Test]
        public void Sessions_SharingOneScriptKeepIndependentProgress()
        {
            DialogueScript script = DialogueScriptParser.Parse("shared", "첫 줄\n둘째 줄");
            var first = new DialogueSession(script);
            var second = new DialogueSession(script);

            first.MoveNext();
            Assert.That(first.Current.Text, Is.EqualTo("둘째 줄"));
            Assert.That(second.Current.Text, Is.EqualTo("첫 줄"));
        }

        [Test]
        public void LinesWrappedInParentheses_AreMonologue_WithOrWithoutASpeaker_AndKeepThem()
        {
            DialogueScript script = DialogueScriptParser.Parse("monologue", string.Join("\n",
                "(나를 향하던 목소리가 끊기자, 어두운 방에 불이 켜지듯 눈앞이 밝아졌다.)",
                "@left 엘리사 | ???",
                "(이 검은…)",
                "윽…",
                "(여긴 어디지…) 하고 중얼거렸다.",
                "  (일단 움직여볼까…)  "));
            var monologue = new bool[script.Lines.Count];
            for (int index = 0; index < monologue.Length; index++) monologue[index] = script.Lines[index].IsMonologue;
            Assert.That(monologue, Is.EqualTo(new[] { true, true, false, false, true }));
            Assert.That(script.Lines[0].Side, Is.EqualTo(DialogueSide.Narrator), "Narration can be a thought too.");
            Assert.That(script.Lines[1].SpeakerName, Is.EqualTo("엘리사"));
            Assert.That(script.Lines[1].Text, Is.EqualTo("(이 검은…)"), "The parentheses stay on screen.");
        }

        [TestCase("(…)", true)]
        [TestCase(" (공백) ", true)]
        [TestCase("()", true)]
        [TestCase("(", false)]
        [TestCase(")", false)]
        [TestCase("(앞) 뒤", false)]
        [TestCase("앞 (뒤)", false)]
        [TestCase("（전각）", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsMonologueText_NeedsTheWholeTrimmedLineInParentheses(string text, bool expected)
        {
            Assert.That(DialogueLine.IsMonologueText(text), Is.EqualTo(expected));
        }
    }
}
