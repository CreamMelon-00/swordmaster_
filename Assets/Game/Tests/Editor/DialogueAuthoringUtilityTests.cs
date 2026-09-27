using System;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.Dialogue;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.EditorTools.Dialogue.Tests
{
    public sealed class DialogueAuthoringUtilityTests
    {
        [Test]
        public void Validate_TreatsEmptyDocumentAsSavableDraft()
        {
            DialogueDocumentValidation result = DialogueAuthoringUtility.Validate("draft", string.Empty);

            Assert.That(result.State, Is.EqualTo(DialogueDocumentState.Draft));
            Assert.That(result.CanPreview, Is.False);
            Assert.That(result.ErrorLine, Is.Zero);
        }

        [Test]
        public void Validate_UsesRuntimeParserAndCollectsUniqueSpeakers()
        {
            DialogueDocumentValidation result = DialogueAuthoringUtility.Validate("valid",
                "@left NAME | ROLE\nTEXT-A\n@right OTHER\nTEXT-B\n@left\nTEXT-C");

            Assert.That(result.State, Is.EqualTo(DialogueDocumentState.Valid));
            Assert.That(result.Script.Lines.Count, Is.EqualTo(3));
            Assert.That(result.Speakers.Count, Is.EqualTo(2));
            Assert.That(result.Speakers[0].Name, Is.EqualTo("NAME"));
            Assert.That(result.Speakers[0].Role, Is.EqualTo("ROLE"));
        }

        [Test]
        public void Validate_CollectsAStagedSpeakerBeforeTheirFirstDialogueLine()
        {
            DialogueDocumentValidation result = DialogueAuthoringUtility.Validate("staged-speaker",
                "@show left STAGED | STAGED ROLE\n@narrator\nTEXT");

            Assert.That(result.State, Is.EqualTo(DialogueDocumentState.Valid));
            Assert.That(result.Speakers.Count, Is.EqualTo(1));
            Assert.That(result.Speakers[0].Name, Is.EqualTo("STAGED"));
            Assert.That(result.Speakers[0].Role, Is.EqualTo("STAGED ROLE"));
        }

        [Test]
        public void Validate_ReportsRuntimeParserSourceLine()
        {
            DialogueDocumentValidation result = DialogueAuthoringUtility.Validate("invalid",
                "TEXT\n@unknown VALUE");

            Assert.That(result.State, Is.EqualTo(DialogueDocumentState.Error));
            Assert.That(result.ErrorLine, Is.EqualTo(2));
            Assert.That(result.Message, Does.Contain("@unknown"));
        }

        [Test]
        public void TryAppendBlock_WritesExplicitGrammarAndEscapesVisibleMarker()
        {
            Assert.That(DialogueAuthoringUtility.TryAppendBlock(string.Empty, DialogueSide.Narrator,
                string.Empty, string.Empty, "#TEXT", out string narrator, out string narratorError), Is.True,
                narratorError);
            Assert.That(DialogueAuthoringUtility.TryAppendBlock(narrator, DialogueSide.Right,
                "NAME", "ROLE", "@TEXT", out string source, out string speakerError), Is.True,
                speakerError);

            DialogueScript parsed = DialogueScriptParser.Parse("round-trip", source);
            Assert.That(parsed.Lines.Count, Is.EqualTo(2));
            Assert.That(parsed.Lines[0].Text, Is.EqualTo("#TEXT"));
            Assert.That(parsed.Lines[1].Side, Is.EqualTo(DialogueSide.Right));
            Assert.That(parsed.Lines[1].SpeakerName, Is.EqualTo("NAME"));
            Assert.That(parsed.Lines[1].SpeakerRole, Is.EqualTo("ROLE"));
            Assert.That(parsed.Lines[1].Text, Is.EqualTo("@TEXT"));
        }

        [Test]
        public void TryAppendBlock_RejectsInvalidQuickEntryWithoutChangingSource()
        {
            string original = "@narrator\nTEXT\n";
            bool success = DialogueAuthoringUtility.TryAppendBlock(original, DialogueSide.Left,
                string.Empty, string.Empty, new string('A', DialogueLine.MaxTextLength + 1),
                out string result, out string error);

            Assert.That(success, Is.False);
            Assert.That(result, Is.EqualTo(original));
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void TryAppendStageCommand_WritesGrammarThatRoundTripsThroughRuntimeParser()
        {
            string source = string.Empty;
            Assert.That(DialogueAuthoringUtility.TryAppendStageCommand(ref source,
                DialogueStageQuickCommand.Show, DialogueSide.Left, DialogueSide.Narrator,
                "LEFT", "LEFT ROLE", out string showLeftError), Is.True, showLeftError);
            Assert.That(DialogueAuthoringUtility.TryAppendStageCommand(ref source,
                DialogueStageQuickCommand.Show, DialogueSide.Right, DialogueSide.Narrator,
                "RIGHT", string.Empty, out string showRightError), Is.True, showRightError);
            Assert.That(DialogueAuthoringUtility.TryAppendStageCommand(ref source,
                DialogueStageQuickCommand.Hide, DialogueSide.Left, DialogueSide.Narrator,
                string.Empty, string.Empty, out string hideError), Is.True, hideError);
            Assert.That(DialogueAuthoringUtility.TryAppendStageCommand(ref source,
                DialogueStageQuickCommand.Move, DialogueSide.Right, DialogueSide.Left,
                string.Empty, string.Empty, out string moveError), Is.True, moveError);
            Assert.That(DialogueAuthoringUtility.TryAppendBlock(source, DialogueSide.Narrator,
                string.Empty, string.Empty, "TEXT", out string completed, out string blockError),
                Is.True, blockError);

            Assert.That(source, Is.EqualTo(
                "@show left LEFT | LEFT ROLE\n" +
                "@show right RIGHT\n" +
                "@hide left\n" +
                "@move right left\n"));
            DialogueScript parsed = DialogueScriptParser.Parse("stage-round-trip", completed);
            Assert.That(parsed.Lines.Count, Is.EqualTo(1));
            Assert.That(parsed.Lines[0].Stage.Left.SpeakerName, Is.EqualTo("RIGHT"));
            Assert.That(parsed.Lines[0].Stage.Right, Is.Null);
        }

        [TestCase(DialogueStageQuickCommand.Show, DialogueSide.Narrator, DialogueSide.Narrator, "", "")]
        [TestCase(DialogueStageQuickCommand.Show, DialogueSide.Left, DialogueSide.Narrator, "", "ROLE")]
        [TestCase(DialogueStageQuickCommand.Hide, DialogueSide.Narrator, DialogueSide.Narrator, "", "")]
        [TestCase(DialogueStageQuickCommand.Move, DialogueSide.Left, DialogueSide.Left, "", "")]
        public void TryAppendStageCommand_RejectsInvalidInputWithoutChangingSource(
            DialogueStageQuickCommand command, DialogueSide side, DialogueSide targetSide,
            string speakerName, string speakerRole)
        {
            const string original = "@narrator\nTEXT\n";
            string source = original;

            bool success = DialogueAuthoringUtility.TryAppendStageCommand(ref source,
                command, side, targetSide, speakerName, speakerRole, out string error);

            Assert.That(success, Is.False);
            Assert.That(source, Is.EqualTo(original));
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void TryAppendValidatedStageCommand_RejectsCommandsThatDoNotMatchTheCurrentStage()
        {
            const string original = "@narrator\nTEXT\n";

            bool success = DialogueAuthoringUtility.TryAppendValidatedStageCommand("stage-state", original,
                DialogueStageQuickCommand.Hide, DialogueSide.Left, DialogueSide.Right,
                string.Empty, string.Empty, out string result, out string error);

            Assert.That(success, Is.False);
            Assert.That(result, Is.EqualTo(original));
            Assert.That(error, Does.Contain("left"));
        }

        [Test]
        public void TryAppendValidatedStageCommand_AllowsADeclaredSpeakerInAnEmptyDraft()
        {
            bool success = DialogueAuthoringUtility.TryAppendValidatedStageCommand("stage-draft", string.Empty,
                DialogueStageQuickCommand.Show, DialogueSide.Left, DialogueSide.Right,
                "LEFT", "ROLE", out string result, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(result, Is.EqualTo("@show left LEFT | ROLE\n"));
        }

        [Test]
        public void DialoguePathValidation_RejectsOutsideAndTraversalPaths()
        {
            Assert.That(DialogueAuthoringUtility.IsDialogueAssetPath(
                "Assets/Game/Resources/Dialogue/document.txt"), Is.True);
            Assert.That(DialogueAuthoringUtility.IsDialogueAssetPath(
                "Assets/Game/Resources/Dialogue/../outside.txt"), Is.False);
            Assert.That(DialogueAuthoringUtility.IsDialogueAssetPath(
                "Assets/Game/Resources/Other/document.txt"), Is.False);
        }

        [Test]
        public void WriteAssetText_UsesUtf8WithoutBomAndNormalizesLineEndings()
        {
            string path = DialogueAuthoringUtility.DialogueAssetRoot +
                "/__dialogue-tool-test-" + Guid.NewGuid().ToString("N") + ".txt";
            try
            {
                DialogueAuthoringUtility.WriteAssetText(path, "OLD");
                DialogueAuthoringUtility.WriteAssetText(path, "TEXT-A\r\nTEXT-B\rTEXT-C");
                byte[] bytes = File.ReadAllBytes(DialogueAuthoringUtility.AssetToAbsolutePath(path));
                Assert.That(bytes.Length, Is.GreaterThan(0));
                Assert.That(bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf,
                    Is.False, "Authoring files should not acquire a UTF-8 BOM.");
                Assert.That(DialogueAuthoringUtility.ReadAssetText(path), Is.EqualTo("TEXT-A\nTEXT-B\nTEXT-C"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void WriteRecoveryCopy_IsSelfDescribingAndDiscoverableAfterRestart()
        {
            string recoveryPath = DialogueAuthoringUtility.WriteRecoveryCopy(
                "Assets/Game/Resources/Dialogue/document.txt", "TEXT-A\r\nTEXT-B", 42L);
            try
            {
                Assert.That(File.Exists(recoveryPath), Is.True);
                Assert.That(recoveryPath.Replace('\\', '/'), Does.Contain("/Library/TurnLimbo/DialogueRecovery/"));
                Assert.That(DialogueAuthoringUtility.TryReadRecoveryCopy(recoveryPath,
                    out DialogueRecoveryRecord record), Is.True);
                Assert.That(record.DocumentPath,
                    Is.EqualTo("Assets/Game/Resources/Dialogue/document.txt"));
                Assert.That(record.BaseWriteTicks, Is.EqualTo(42L));
                Assert.That(record.Source, Is.EqualTo("TEXT-A\nTEXT-B"));
                Assert.That(DialogueAuthoringUtility.FindLatestRecoveryCopy(), Is.EqualTo(recoveryPath));
            }
            finally
            {
                if (File.Exists(recoveryPath)) File.Delete(recoveryPath);
            }
        }

        [Test]
        public void PortraitCatalog_AssignsReplacesAndClearsExactSpeakerName()
        {
            var catalog = ScriptableObject.CreateInstance<DialoguePortraitCatalog>();
            var texture = new Texture2D(2, 2);
            Sprite first = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            Sprite second = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                catalog.SetPortraitForEditor("NAME", first);
                Assert.That(catalog.FindPortrait("NAME"), Is.SameAs(first));
                Assert.That(catalog.FindPortrait("name"), Is.Null);

                catalog.SetPortraitForEditor("NAME", second);
                Assert.That(catalog.FindPortrait("NAME"), Is.SameAs(second));
                Assert.That(catalog.Entries.Count, Is.EqualTo(1));

                catalog.SetPortraitForEditor("NAME", null);
                Assert.That(catalog.FindPortrait("NAME"), Is.Null);
                Assert.That(catalog.Entries, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
