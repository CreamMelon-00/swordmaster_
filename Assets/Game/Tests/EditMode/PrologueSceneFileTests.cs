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
    /// scenes are the author's manuscript (Docs/PrologueManuscript.md); these tests hold them to the format and to the
    /// manuscript's nameplates, not to their wording.</summary>
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

        private static CutsceneStep[] AuraSteps(string resourcePath)
            => Read(Project(ResourcesFolder + resourcePath + ".txt")).Steps
                .Where(step => step.Kind == CutsceneStepKind.Aura && step.Actor == CutsceneActor.Knight).ToArray();
    }
}
