using System;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Runtime.Barks;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The bark file format (Docs/Barks.md) and the template files shipped for the 서막's missions.</summary>
    public sealed class BarkScriptParserTests
    {
        private static BarkScript Parse(string source) => BarkScriptParser.Parse("Barks/test", source);

        private static BarkParseException Fails(string source) => Assert.Throws<BarkParseException>(() => Parse(source));

        [Test]
        public void Parse_ReadsEachOnBlock_WithItsSpeakerAndLines()
        {
            BarkScript script = Parse(string.Join("\n",
                "# 메모",
                "@on start",
                "덤벼라.",
                "",
                "@on enemy-hurt player   # 엘리사가 말한다",
                "(통했다…!)",
                "좋아!",
                "@on enemy-hurt",
                "크윽…",
                "@on sutun enemy",
                "\\@이것이 수훈이다",
                "\\# 표시되는 샵"));
            Assert.That(script.Id, Is.EqualTo("Barks/test"));
            Assert.That(script.Entries.Count, Is.EqualTo(4));
            Assert.That(script.IsEmpty, Is.False);

            BarkEntry start = script.Find(BarkTrigger.Start, BarkSpeaker.Enemy);
            Assert.That(start.Lines, Is.EqualTo(new[] { "덤벼라." }));
            Assert.That(start.SourceLineNumber, Is.EqualTo(2));
            Assert.That(script.Find(BarkTrigger.Start, BarkSpeaker.Player), Is.Null, "No speaker given is the enemy.");

            BarkEntry thought = script.Find(BarkTrigger.EnemyHurt, BarkSpeaker.Player);
            Assert.That(thought.Lines, Is.EqualTo(new[] { "(통했다…!)", "좋아!" }), "A memo may follow an @on's words.");
            Assert.That(thought.SourceLineNumber, Is.EqualTo(5));
            Assert.That(script.Find(BarkTrigger.EnemyHurt, BarkSpeaker.Enemy).Lines, Is.EqualTo(new[] { "크윽…" }),
                "One trigger may have a line for each side.");
            Assert.That(script.Find(BarkTrigger.Sutun, BarkSpeaker.Enemy).Lines, Is.EqualTo(new[] { "@이것이 수훈이다", "# 표시되는 샵" }),
                "\\@ and \\# start a line with @ or #.");
            Assert.That(script.Find(BarkTrigger.EnemyLow, BarkSpeaker.Enemy), Is.Null);
        }

        [Test]
        public void Parse_AcceptsWindowsLineEndings_AByteOrderMark_AndSurroundingSpaces()
        {
            BarkScript script = Parse((char)0xFEFF + "# 메모\r\n  @on player-low   player  \r\n   (아직이야…)   \r\n");
            BarkEntry entry = script.Find(BarkTrigger.PlayerLow, BarkSpeaker.Player);
            Assert.That(entry.Lines, Is.EqualTo(new[] { "(아직이야…)" }));
            Assert.That(entry.SourceLineNumber, Is.EqualTo(2));
        }

        [Test]
        public void AFileOfCommentsOnly_IsValid_AndSaysNothing()
        {
            Assert.That(Parse("# 아직 대사가 없다\n#\n# @on start\n\n").IsEmpty, Is.True);
            Assert.That(Parse(string.Empty).IsEmpty, Is.True);
        }

        [Test]
        public void EveryTrigger_HasItsWord()
        {
            Assert.That(BarkScript.TriggerNames, Is.EqualTo(new[]
            {
                "start", "enemy-broken", "player-broken", "enemy-hurt", "player-hurt", "enemy-low", "player-low", "sutun",
            }));
            foreach (BarkTrigger trigger in Enum.GetValues(typeof(BarkTrigger)))
            {
                Assert.That(BarkScript.TryParseTrigger(BarkScript.TriggerName(trigger), out BarkTrigger read), Is.True);
                Assert.That(read, Is.EqualTo(trigger));
                BarkScript script = Parse("@on " + BarkScript.TriggerName(trigger) + "\n한 줄");
                Assert.That(script.Find(trigger, BarkSpeaker.Enemy), Is.Not.Null);
            }
            Assert.That(BarkScript.TryParseTrigger("Start", out _), Is.False, "Words are lowercase.");
        }

        [Test]
        public void Errors_NameTheFileAndLine_InKorean()
        {
            BarkParseException error = Fails("# 메모\n그 정도인가.");
            Assert.That(error.LineNumber, Is.EqualTo(2));
            Assert.That(error.ScriptId, Is.EqualTo("Barks/test"));
            Assert.That(error.Message, Does.StartWith("전투 대사 'Barks/test', 2번째 줄: "));
            Assert.That(error.Message, Does.Contain("@on <상황> [enemy|player]"), "A line needs an @on above it.");

            error = Fails("@on start\n덤벼라.\n@wait 1");
            Assert.That(error.LineNumber, Is.EqualTo(3));
            Assert.That(error.Message, Does.Contain("알 수 없는 지시어 '@wait'"));
            Assert.That(error.Message, Does.Contain("\\@"), "It says how to start a line with @.");
            Assert.That(Fails("@On start\n덤벼라.").Message, Does.Contain("'@On'"), "Directives are lowercase.");
        }

        [Test]
        public void Errors_ForTheOnLine()
        {
            BarkParseException error = Fails("@on hurt\n크윽");
            Assert.That(error.LineNumber, Is.EqualTo(1));
            Assert.That(error.Message, Does.Contain("상황 'hurt'"));
            Assert.That(error.Message, Does.Contain("start, enemy-broken, player-broken, enemy-hurt, player-hurt, enemy-low, player-low, sutun"));

            Assert.That(Fails("@on start elisa\n덤벼라.").Message, Does.Contain("enemy(상대) 또는 player(엘리사)"));
            Assert.That(Fails("@on\n덤벼라.").Message, Does.Contain("형식은 '@on <상황> [enemy|player]'"));
            Assert.That(Fails("@on start enemy now\n덤벼라.").Message, Does.Contain("형식은"));
            Assert.That(Fails("@on # 메모만\n덤벼라.").Message, Does.Contain("형식은"));
        }

        [Test]
        public void AnOnWithoutLines_IsAnError_AtItsLine()
        {
            BarkParseException error = Fails("@on start\n# 아직\n@on enemy-low\n…");
            Assert.That(error.LineNumber, Is.EqualTo(1));
            Assert.That(error.Message, Does.Contain("@on start 아래에 대사가 한 줄도 없습니다"));
            Assert.That(Fails("@on start\n덤벼라.\n@on sutun\n").LineNumber, Is.EqualTo(3), "The last block too.");
        }

        [Test]
        public void ARepeatedOn_PointsAtTheFirst()
        {
            BarkParseException error = Fails("@on start\n하나\n@on enemy-low\n둘\n@on start enemy\n셋");
            Assert.That(error.LineNumber, Is.EqualTo(5));
            Assert.That(error.Message, Does.Contain("@on start enemy은(는) 이미 1번째 줄에 있습니다"));
            Assert.That(() => Parse("@on start\n하나\n@on start player\n둘"), Throws.Nothing, "Each side may have its own block.");
        }

        [Test]
        public void ALineLongerThanABubbleHolds_IsAnError()
        {
            string longest = new string('가', BarkScript.MaxTextLength);
            Assert.That(Parse("@on start\n" + longest).Find(BarkTrigger.Start, BarkSpeaker.Enemy).Lines[0], Is.EqualTo(longest));
            BarkParseException error = Fails("@on start\n" + longest + "가");
            Assert.That(error.LineNumber, Is.EqualTo(2));
            Assert.That(error.Message, Does.Contain($"{BarkScript.MaxTextLength}자를 넘을 수 없습니다(지금 {BarkScript.MaxTextLength + 1}자)"));
        }

        [Test]
        public void ResourcePaths_FollowTheMissionOrStageNumber()
        {
            Assert.That(BarkScript.MissionResource(4), Is.EqualTo("Barks/mission-04"));
            Assert.That(BarkScript.StageResource(12), Is.EqualTo("Barks/stage-12"));
        }

        [Test]
        public void TheShippedTemplates_ForTheFirstFourMissions_ParseAndSayNothingYet()
        {
            for (int mission = 1; mission <= 4; mission++)
            {
                string resource = BarkScript.MissionResource(mission);
                string path = Path.Combine(Environment.CurrentDirectory, "Assets/Game/Resources", resource + ".txt");
                Assert.That(File.Exists(path), Is.True, resource);
                string source = File.ReadAllText(path);
                BarkScript script = null;
                Assert.DoesNotThrow(() => script = BarkScriptParser.Parse(resource, source), resource);
                Assert.That(script.IsEmpty, Is.True, "Nothing shows until the author writes lines: " + resource);
                // Each template lists the triggers its battle can fire, ready to uncomment (sutun only where 수훈 happens).
                foreach (BarkTrigger trigger in Enum.GetValues(typeof(BarkTrigger)))
                {
                    bool listed = source.Contains("# @on " + BarkScript.TriggerName(trigger) + " ");
                    Assert.That(listed, Is.EqualTo(trigger != BarkTrigger.Sutun || mission == 4),
                        resource + " lists " + BarkScript.TriggerName(trigger));
                }
            }
        }
    }
}
