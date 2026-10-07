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
    /// mission's scenes with that mission's fighters on stage, anything else (the opening) on a bare stage. The 서막's
    /// scenes are the author's manuscript (Docs/PrologueManuscript.md); these tests hold them to the format, to the
    /// manuscript's nameplates and to the staging it asks for (the 수훈 aura; the 2nd edition's tremble, sword swing and
    /// tutorial recall), not to their wording.</summary>
    public sealed class PrologueSceneFileTests
    {
        private const string ResourcesFolder = "Assets/Game/Resources/";
        private const string CutsceneFolder = ResourcesFolder + "Cutscene";
        // The manuscript's nameplates (이름 | 칭호). 이아 gives her name in mission 4's intro; before it she is ??? | 떠돌이 기사.
        private static readonly string[] Nameplates = { "엘리사 | ???", "??? | ???", "??? | 떠돌이 기사", "이아 | 떠돌이 기사", "이아 | 도미니코 기사단" };
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
                    else
                        Assert.That(line.Side, Is.EqualTo(scene == SeniorKnightScene ? DialogueSide.Left : DialogueSide.Right), where);
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
        public void SecondEditionStaging_TheTremble_TheSwordSwing_AndTheTutorialRecall_ArePlayed()
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
            Assert.That(afterCry.Where(step => step.Kind == CutsceneStepKind.Wait).Sum(step => step.Seconds),
                Is.GreaterThanOrEqualTo(CutsceneStep.AttackSeconds - CutsceneStep.AttackImpactSeconds),
                "Even an immediate advance lets the blade finish before the handoff.");
            Assert.That(afterCry.Last().Kind, Is.EqualTo(CutsceneStepKind.Wait),
                "The battle start card takes over the close-range clash without a black flash.");

            // {튜토리얼 화면 연상}: one recall, after 엘리사's '(머릿속에서 들리는 이 소리는…)' and before the knight speaks.
            List<CutsceneStep> missionThree = Read(Project(CutsceneFolder + "/mission-03-intro.txt")).Steps.ToList();
            Assert.That(missionThree.Count(step => step.Kind == CutsceneStepKind.Recall), Is.EqualTo(1));
            int recall = missionThree.FindIndex(step => step.Kind == CutsceneStepKind.Recall);
            int elisa = missionThree.FindIndex(step => step.Kind == CutsceneStepKind.Line && step.Line.SpeakerName == "엘리사");
            int knight = missionThree.FindIndex(step => step.Kind == CutsceneStepKind.Line && step.Line.SpeakerRole == "떠돌이 기사");
            Assert.That(elisa, Is.GreaterThanOrEqualTo(0));
            Assert.That(recall, Is.GreaterThan(elisa).And.LessThan(knight), "The voice in her head is the coach she heard.");
        }

        private static bool IsActor(CutsceneStep step, CutsceneActor actor, CutsceneActorAction action)
            => step.Kind == CutsceneStepKind.Actor && step.Actor == actor && step.Action == action;

        private static CutsceneStep[] AuraSteps(string resourcePath)
            => Read(Project(ResourcesFolder + resourcePath + ".txt")).Steps
                .Where(step => step.Kind == CutsceneStepKind.Aura && step.Actor == CutsceneActor.Knight).ToArray();
    }
}
