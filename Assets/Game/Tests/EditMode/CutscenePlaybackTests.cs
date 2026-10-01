using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Dialogue;

namespace TurnLimbo.Core.Tests
{
    public sealed class CutscenePlaybackTests
    {
        private sealed class RecordingStage : ICutsceneStage
        {
            public readonly List<string> Log = new List<string>();

            public void Run(CutsceneStep step) => Log.Add("run " + step.SourceLineNumber);

            public void ShowLine(DialogueLine line, int lineIndex, int lineCount)
                => Log.Add($"show {line.Text} {lineIndex + 1}/{lineCount}");

            public void HideLine() => Log.Add("hide");
        }

        private static CutscenePlayback Play(string source, out RecordingStage stage)
        {
            stage = new RecordingStage();
            var playback = new CutscenePlayback(CutsceneScriptParser.Parse("Cutscene/test", source), stage);
            playback.Start();
            return playback;
        }

        [Test]
        public void InstantStepsRunTogether_TimedStepsHold_AndTheEndCompletes()
        {
            CutscenePlayback playback = Play("@fade out 0\n@actor elise at 0\n@fade in 2\n@bars on 1 &\n@wait 0.5", out var stage);
            Assert.That(stage.Log, Is.EqualTo(new[] { "run 1", "run 2", "run 3" }), "The first timed step holds.");
            Assert.That(playback.HoldRemaining, Is.EqualTo(2f));
            playback.Tick(1.5f);
            Assert.That(stage.Log.Count, Is.EqualTo(3));
            Assert.That(playback.HoldRemaining, Is.EqualTo(.5f).Within(1e-5f));
            playback.Tick(.6f);
            Assert.That(stage.Log, Is.EqualTo(new[] { "run 1", "run 2", "run 3", "run 4", "run 5" }),
                "A step written with & starts the next one at once.");
            Assert.That(playback.HoldRemaining, Is.EqualTo(.5f), "Leftover time is not carried into the next hold.");
            Assert.That(playback.IsComplete, Is.False);
            playback.Tick(.5f);
            Assert.That(playback.IsComplete, Is.True);
            playback.Tick(1f);
            Assert.That(stage.Log.Count, Is.EqualTo(5));
        }

        [Test]
        public void LinesWaitForThePlayer_StayUpBetweenLines_AndHideBeforeTheSceneMovesOn()
        {
            CutscenePlayback playback = Play("첫째\n@actor elise at 0\n둘째\n@camera 2 4 1 &\n셋째\n@wait 1\n@actor elise hide", out var stage);
            Assert.That(stage.Log, Is.EqualTo(new[] { "show 첫째 1/3" }));
            Assert.That(playback.CurrentLine.Text, Is.EqualTo("첫째"));
            playback.Tick(10f);
            Assert.That(stage.Log.Count, Is.EqualTo(1), "Time alone never moves past a line.");
            Assert.That(playback.Advance(), Is.True);
            Assert.That(stage.Log, Is.EqualTo(new[] { "show 첫째 1/3", "run 2", "show 둘째 2/3" }),
                "An instant step between lines keeps the box up.");
            Assert.That(playback.Advance(), Is.True);
            Assert.That(stage.Log[3], Is.EqualTo("run 4"));
            Assert.That(stage.Log[4], Is.EqualTo("show 셋째 3/3"), "A step that does not hold keeps the box up too.");
            Assert.That(playback.Advance(), Is.True);
            Assert.That(stage.Log[5], Is.EqualTo("hide"), "The box goes away before a hold.");
            Assert.That(stage.Log[6], Is.EqualTo("run 6"));
            Assert.That(playback.CurrentLine, Is.Null);
            Assert.That(playback.Advance(), Is.False, "Nothing to advance during a hold.");
            playback.Tick(1f);
            Assert.That(stage.Log[7], Is.EqualTo("run 7"));
            Assert.That(playback.IsComplete, Is.True);
        }

        [Test]
        public void ALastLineIsHiddenWhenTheCutsceneEnds_AndStartRunsOnce()
        {
            CutscenePlayback playback = Play("@wait 1\n마지막", out var stage);
            playback.Start();
            Assert.That(stage.Log, Is.EqualTo(new[] { "run 1" }));
            playback.Tick(1f);
            Assert.That(playback.Advance(), Is.True);
            Assert.That(stage.Log, Is.EqualTo(new[] { "run 1", "show 마지막 1/1", "hide" }));
            Assert.That(playback.IsComplete, Is.True);
            Assert.That(playback.StepsRun, Is.EqualTo(2));
        }

        [Test]
        public void AnInstantOnlyCutscene_CompletesOnStart()
        {
            CutscenePlayback playback = Play("@actor elise at 0\n@fade out 0", out var stage);
            Assert.That(playback.IsComplete, Is.True);
            Assert.That(stage.Log, Is.EqualTo(new[] { "run 1", "run 2" }));
        }
    }
}
