using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Dialogue;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The shipped cutscene files (Resources/Cutscene), read from disk and parsed as the game parses them: a
    /// mission's scenes with that mission's fighters on stage, and the opening on a bare stage. The
    /// scenes are the author's manuscript (Docs/StoryManuscript.md); these tests hold them to its nameplates,
    /// branches and staging, not to their wording.</summary>
    public sealed class PrologueSceneFileTests
    {
        private const string ResourcesFolder = "Assets/Game/Resources/";
        private const string CutsceneFolder = ResourcesFolder + "Cutscene";
        // The manuscript's nameplates (이름 | 칭호). Ia gives a name in mission 4's intro; earlier labels remain ??? | 떠돌이 기사.
        private static readonly string[] Nameplates =
        {
            "엘리사 | ???", "??? | ???", "??? | 선배?", "??? | 떠돌이 기사",
            "이아 | 떠돌이 기사", "이아 | 도미니코 기사단"
        };
        private static readonly string[] IaRoles = { "떠돌이 기사", "도미니코 기사단" };
        // ??? | ??? is the voice and the shout off the right edge, except the senior knight, who enters from the left.
        private const string SeniorKnightScene = "mission-04-outro";

        private static string Project(string relative) => Path.Combine(Environment.CurrentDirectory, relative);

        private static string[] Files()
            => Directory.GetFiles(Project(CutsceneFolder), "*.txt").OrderBy(path => path, StringComparer.Ordinal).ToArray();

        private static string Name(string path) => Path.GetFileNameWithoutExtension(path);

        /// <summary>A file as the controller reads it: <c>Cutscene/&lt;name&gt;</c>, with the mission's cast for mission-NN-….</summary>
        private static CutsceneScript Read(string path)
        {
            Match mission = Regex.Match(Name(path), @"^mission-(\d\d)-");
            IReadOnlyList<CutsceneActor> cast = mission.Success ? StoryMissions.Get(int.Parse(mission.Groups[1].Value)).SceneCast : null;
            return CutsceneScriptParser.Parse("Cutscene/" + Name(path), File.ReadAllText(path), cast);
        }

        private static IEnumerable<DialogueLine> Lines(CutsceneScript script)
            => script.Steps.Where(step => step.Kind == CutsceneStepKind.Line).Select(step => step.Line);

        [Test]
        public void EveryCutsceneFile_ParsesWithTheCastItPlaysWith()
        {
            string[] files = Files();
            Assert.That(files.Select(Name), Is.SupersetOf(new[]
            {
                "opening", "mission-01-intro", "mission-01-outro", "mission-02-intro", "mission-02-outro",
                "mission-03-intro", "mission-03-outro", "mission-04-intro", "mission-04-event", "mission-04-outro",
            }));
            foreach (string path in files)
            {
                CutsceneScript script = null;
                Assert.DoesNotThrow(() => script = Read(path), Name(path));
                Assert.That(script.LineCount, Is.GreaterThan(0), Name(path));
            }
        }

        [Test]
        public void PrologueScenes_HaveOneSourceEach_TheirCutscene()
        {
            foreach (PrologueMission mission in PrologueMissions.All)
            {
                Assert.That(File.Exists(Project(ResourcesFolder + mission.IntroCutscene + ".txt")), Is.True, mission.IntroCutscene);
                Assert.That(File.Exists(Project(ResourcesFolder + mission.OutroCutscene + ".txt")), Is.True, mission.OutroCutscene);
                foreach (string dialogue in new[] { mission.IntroDialogue, mission.OutroDialogue, mission.EventDialogue })
                    Assert.That(File.Exists(Project(ResourcesFolder + dialogue + ".txt")), Is.False, dialogue + " would be a second source.");
            }
            Assert.That(File.Exists(Project(ResourcesFolder + PrologueMissions.Get(4).Empowerment.Scene + ".txt")), Is.True);
        }

        [Test]
        public void Speakers_AreTheManuscriptsNameplates_OnTheSideWhereTheyStand()
        {
            foreach (string path in Files())
            {
                string scene = Name(path);
                bool knowsIa = scene.StartsWith("mission-04-", StringComparison.Ordinal);
                foreach (DialogueLine line in Lines(Read(path)))
                {
                    string where = $"{scene}:{line.SourceLineNumber}";
                    if (line.Side == DialogueSide.Narrator)
                    {
                        Assert.That(line.SpeakerName, Is.Empty, where);
                        continue;
                    }
                    Assert.That(Nameplates, Does.Contain(line.SpeakerName + " | " + line.SpeakerRole), where);
                    if (line.SpeakerName == "엘리사")
                        Assert.That(line.Side, Is.EqualTo(DialogueSide.Left), where);
                    else if (IaRoles.Contains(line.SpeakerRole))
                    {
                        Assert.That(line.Side, Is.EqualTo(DialogueSide.Right), where);
                        Assert.That(line.SpeakerName, Is.EqualTo(knowsIa ? "이아" : "???"), where + ": named once she gives her name.");
                    }
                    else if (line.SpeakerRole == "선배?" || scene == SeniorKnightScene)
                        Assert.That(line.Side, Is.EqualTo(DialogueSide.Left), where);
                    else
                        Assert.That(line.Side, Is.EqualTo(DialogueSide.Right), where);
                }
            }
        }

        [Test]
        public void Scenes_PlayOnlySoundsThatExist()
        {
            foreach (string path in Files())
            {
                foreach (CutsceneStep step in Read(path).Steps.Where(step =>
                    (step.Kind == CutsceneStepKind.Sound || step.Kind == CutsceneStepKind.Ambience) && step.Resource != null))
                {
                    string folder = Project(ResourcesFolder + Path.GetDirectoryName(step.Resource));
                    string name = Path.GetFileName(step.Resource);
                    string[] clips = Directory.GetFiles(folder, name + ".*")
                        .Where(file => !file.EndsWith(".meta", StringComparison.Ordinal)).ToArray();
                    Assert.That(clips.Length, Is.EqualTo(1), $"{Name(path)}:{step.SourceLineNumber} Resources/{step.Resource}");
                }
            }
        }

        [Test]
        public void MissionFourScenes_LightTheAuraForTheRestOfTheBattle_AndPutItOutAfterTheLoss()
        {
            PrologueMission missionFour = PrologueMissions.Get(4);
            CutsceneStep[] eventAura = AuraSteps(missionFour.Empowerment.Scene);
            Assert.That(eventAura.Select(step => step.AuraOn), Is.EqualTo(new[] { true }), "수훈 lights it and the battle keeps it.");
            Assert.That(AuraSteps(missionFour.OutroCutscene).Select(step => step.AuraOn).Last(), Is.False,
                "The outro starts with the aura on and ends it.");
        }

        [Test]
        public void RevisedStaging_TheTremble_AndBothSwordSwings_ArePlayed_WithoutMissionThreeRecall()
        {
            // [엘리사가 살짝 떨리는 연출] closes the opening, after 엘리사's last line.
            List<CutsceneStep> opening = Read(Project(CutsceneFolder + "/opening.txt")).Steps.ToList();
            int tremble = opening.FindLastIndex(step => IsActor(step, CutsceneActor.Elisa, CutsceneActorAction.Tremble));
            Assert.That(tremble, Is.GreaterThan(opening.FindLastIndex(step => step.Kind == CutsceneStepKind.Line)), "opening");

            // {엘리사 칼 휘두르는 모션}: a slash the knight blocks, before his '우왓?!' (the scene's last line).
            List<CutsceneStep> missionTwo = Read(Project(CutsceneFolder + "/mission-02-intro.txt")).Steps.ToList();
            int lastLine = missionTwo.FindLastIndex(step => step.Kind == CutsceneStepKind.Line);
            int slash = missionTwo.FindIndex(step => IsActor(step, CutsceneActor.Elisa, CutsceneActorAction.Attack) &&
                step.Attack == CutsceneAttack.Slash);
            int block = missionTwo.FindIndex(step => IsActor(step, CutsceneActor.Knight, CutsceneActorAction.Pose) &&
                step.Pose == CutscenePose.Block);
            Assert.That(slash, Is.GreaterThan(missionTwo.FindLastIndex(lastLine - 1, step => step.Kind == CutsceneStepKind.Line)));
            Assert.That(block, Is.GreaterThan(slash).And.LessThan(lastLine), "The knight blocks the stroke as he cries out.");
            CutsceneStep[] afterCry = missionTwo.Skip(lastLine + 1).ToArray();
            Assert.That(afterCry.Any(step => IsActor(step, CutsceneActor.Elisa, CutsceneActorAction.Move)), Is.False,
                "The manuscript does not send Elisa back to her starting mark after the clash.");
            float remainingStroke = CutsceneStep.AttackSeconds - CutsceneStep.AttackImpactSeconds;
            float settledTime = afterCry.Where(step => step.Kind == CutsceneStepKind.Wait).Sum(step => step.Seconds);
            Assert.That(settledTime + 0.0001f, Is.GreaterThanOrEqualTo(remainingStroke),
                "Even an immediate advance lets the blade finish before the handoff.");
            Assert.That(afterCry.Last().Kind, Is.EqualTo(CutsceneStepKind.Wait),
                "The battle start card takes over the close-range clash without a black flash.");

            // The revised mission 3 begins with the knight; Elisa no longer remembers the tutorial here.
            List<CutsceneStep> missionThree = Read(Project(CutsceneFolder + "/mission-03-intro.txt")).Steps.ToList();
            Assert.That(missionThree.Any(step => step.Kind == CutsceneStepKind.Recall), Is.False);
            Assert.That(missionThree.Where(step => step.Kind == CutsceneStepKind.Line)
                .All(step => step.Line.SpeakerRole == "떠돌이 기사"), Is.True);

            // Elisa strikes while Ia is still asking for her name, before his last cry and the battle.
            List<CutsceneStep> missionFour = Read(Project(CutsceneFolder + "/mission-04-intro.txt")).Steps.ToList();
            int cry = missionFour.FindLastIndex(step => step.Kind == CutsceneStepKind.Line);
            int question = missionFour.FindLastIndex(cry - 1, step => step.Kind == CutsceneStepKind.Line);
            int secondSlash = missionFour.FindIndex(step => IsActor(step, CutsceneActor.Elisa, CutsceneActorAction.Attack) &&
                step.Attack == CutsceneAttack.Slash);
            Assert.That(secondSlash, Is.GreaterThan(question).And.LessThan(cry));
        }

        [Test]
        public void InterruptedOpening_OffersBothChoices_AndOnlyFullListeningShowsVoiceFlashbacks()
        {
            CutsceneScript script = Read(Project(CutsceneFolder + "/opening.txt"));
            CutsceneStep[] choices = script.Steps.Where(step => step.Kind == CutsceneStepKind.Choice).ToArray();
            Assert.That(choices.Length, Is.EqualTo(1), "The opening offers one interruption choice.");
            Assert.That(choices[0].ChoiceA, Is.EqualTo("날 보내줘"));
            Assert.That(choices[0].ChoiceB, Is.EqualTo("…"));
            Assert.That(script.Steps.Any(step => step.Kind == CutsceneStepKind.Mark && step.Label == "core"),
                Is.True, "The essentials choice needs a return point at the short order.");
            AssertVoiceFlashbackIsFullOnly("opening");
            AssertVoiceFlashbackIsFullOnly("mission-02-intro");
        }

        [Test]
        public void EitherInterruptionChoice_SuppressesLaterVoiceMemories()
        {
            PlaybackRecord send = PlayScene("opening", false, 0);
            PlaybackRecord essentials = PlayScene("opening", false, 1);
            PlaybackRecord missionTwo = PlayScene("mission-02-intro", false);
            PlaybackRecord fullOpening = PlayScene("opening", true);

            foreach (PlaybackRecord skipped in new[] { send, essentials, missionTwo })
                Assert.That(skipped.Steps.Any(step => step.Kind == CutsceneStepKind.Flashback), Is.False,
                    "A and B must both leave the voice's later recall unplayed.");
            Assert.That(missionTwo.Lines.Any(line => line.SpeakerName == "???" && line.SpeakerRole == "???"),
                Is.False, "Neither skipped route may remember voice lines in mission 2.");
            Assert.That(send.Lines.Any(line => line.Text == "모두 죽여버리면 돼."), Is.False,
                "A goes straight to the forest.");
            Assert.That(essentials.Lines.Any(line => line.Text == "모두 죽여버리면 돼."), Is.True,
                "B hears the essential order before the forest.");
            Assert.That(fullOpening.Steps.Count(step => step.Kind == CutsceneStepKind.Flashback), Is.EqualTo(2),
                "Listening through still shows the opening memory.");
        }

        private static void AssertVoiceFlashbackIsFullOnly(string scene)
        {
            int fullDepth = 0;
            int flashbackSteps = 0;
            bool inFlashback = false;
            foreach (CutsceneStep step in Read(Project(CutsceneFolder + "/" + scene + ".txt")).Steps)
            {
                if (step.Kind == CutsceneStepKind.IfFull) { fullDepth++; continue; }
                if (step.Kind == CutsceneStepKind.EndIf)
                {
                    fullDepth--;
                    Assert.That(fullDepth, Is.GreaterThanOrEqualTo(0), scene + ": unmatched @endif.");
                    continue;
                }
                bool voiceSound = step.Kind == CutsceneStepKind.Sound && step.Resource == "Sfx/flashback";
                if (step.Kind == CutsceneStepKind.Flashback)
                {
                    flashbackSteps++;
                    if (step.FlashbackOn) inFlashback = true;
                }
                if (inFlashback || voiceSound || step.Kind == CutsceneStepKind.Flashback)
                    Assert.That(fullDepth, Is.GreaterThan(0),
                        scene + ":" + step.SourceLineNumber + ": A and B must skip the entire voice memory.");
                if (step.Kind == CutsceneStepKind.Flashback && !step.FlashbackOn) inFlashback = false;
            }
            Assert.That(fullDepth, Is.Zero, scene + ": conditional blocks must be closed.");
            Assert.That(inFlashback, Is.False, scene + ": the flashback must end.");
            Assert.That(flashbackSteps, Is.EqualTo(2), scene + ": the full-listening memory remains.");
        }

        private sealed class PlaybackRecord : ICutsceneStage
        {
            public readonly List<DialogueLine> Lines = new List<DialogueLine>();
            public readonly List<CutsceneStep> Steps = new List<CutsceneStep>();

            public void Run(CutsceneStep step) => Steps.Add(step);
            public void ShowLine(DialogueLine line, int lineIndex, int lineCount) => Lines.Add(line);
            public void HideLine() { }
        }

        private static PlaybackRecord PlayScene(string scene, bool full, int? interruptionChoice = null)
        {
            var record = new PlaybackRecord();
            var playback = new CutscenePlayback(Read(Project(CutsceneFolder + "/" + scene + ".txt")), record)
            {
                PlayFullVoiceMemories = full
            };
            playback.Start();
            if (interruptionChoice.HasValue)
                Assert.That(playback.JumpTo("skip-interrupt"), Is.True, scene + ": interruption target missing.");
            for (int turn = 0; turn < 500 && !playback.IsComplete; turn++)
            {
                if (playback.CurrentChoice != null)
                {
                    Assert.That(interruptionChoice.HasValue, Is.True, scene + ": unexpected choice.");
                    Assert.That(playback.Choose(interruptionChoice.Value), Is.True);
                }
                else if (playback.CurrentLine != null)
                    Assert.That(playback.Advance(), Is.True);
                else if (playback.HoldRemaining > 0f)
                    playback.Tick(30f);
                else
                    Assert.Fail(scene + ": playback did not advance.");
            }
            Assert.That(playback.IsComplete, Is.True, scene + ": playback did not finish.");
            return record;
        }

        private static bool IsActor(CutsceneStep step, CutsceneActor actor, CutsceneActorAction action)
            => step.Kind == CutsceneStepKind.Actor && step.Actor == actor && step.Action == action;

        private static CutsceneStep[] AuraSteps(string resourcePath)
            => Read(Project(ResourcesFolder + resourcePath + ".txt")).Steps
                .Where(step => step.Kind == CutsceneStepKind.Aura && step.Actor == CutsceneActor.Knight).ToArray();
    }
}
