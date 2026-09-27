using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TurnLimbo.Runtime.Dialogue;
using UnityEditor;
using UnityEngine;

namespace TurnLimbo.EditorTools.Dialogue
{
    public enum DialogueDocumentState
    {
        Draft,
        Valid,
        Error,
    }

    public enum DialogueStageQuickCommand
    {
        Show,
        Hide,
        Move,
    }

    public sealed class DialogueSpeakerSummary
    {
        public DialogueSpeakerSummary(string name, string role)
        {
            Name = name;
            Role = role;
        }

        public string Name { get; }
        public string Role { get; }
    }

    public sealed class DialogueDocumentValidation
    {
        public DialogueDocumentValidation(DialogueDocumentState state, DialogueScript script,
            int errorLine, string message, IReadOnlyList<DialogueSpeakerSummary> speakers)
        {
            State = state;
            Script = script;
            ErrorLine = errorLine;
            Message = message ?? string.Empty;
            Speakers = speakers ?? Array.Empty<DialogueSpeakerSummary>();
        }

        public DialogueDocumentState State { get; }
        public DialogueScript Script { get; }
        public int ErrorLine { get; }
        public string Message { get; }
        public IReadOnlyList<DialogueSpeakerSummary> Speakers { get; }
        public bool CanPreview => State == DialogueDocumentState.Valid && Script != null;
    }

    [Serializable]
    public sealed class DialogueRecoveryRecord
    {
        public string DocumentPath = string.Empty;
        public long BaseWriteTicks;
        public string Source = string.Empty;
        public string CreatedUtc = string.Empty;
    }

    public static class DialogueAuthoringUtility
    {
        public const string DialogueAssetRoot = "Assets/Game/Resources/Dialogue";

        public static DialogueDocumentValidation Validate(string documentId, string source)
        {
            source = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
                return new DialogueDocumentValidation(DialogueDocumentState.Draft, null, 0,
                    "아직 표시할 문장이 없는 빈 초안입니다.", Array.Empty<DialogueSpeakerSummary>());

            try
            {
                DialogueScript script = DialogueScriptParser.Parse(
                    string.IsNullOrWhiteSpace(documentId) ? "untitled" : documentId.Trim(), source);
                return new DialogueDocumentValidation(DialogueDocumentState.Valid, script, 0,
                    "규칙 검사를 통과했습니다.", CollectSpeakers(script));
            }
            catch (DialogueParseException exception)
            {
                return new DialogueDocumentValidation(DialogueDocumentState.Error, null,
                    exception.LineNumber, exception.Message, Array.Empty<DialogueSpeakerSummary>());
            }
        }

        public static bool TryAppendBlock(string source, DialogueSide side, string speakerName,
            string speakerRole, string text, out string result, out string error)
        {
            source = NormalizeLineEndings(source ?? string.Empty);
            speakerName = (speakerName ?? string.Empty).Trim();
            speakerRole = (speakerRole ?? string.Empty).Trim();
            text = (text ?? string.Empty).Trim();
            result = source;

            if (side != DialogueSide.Narrator && side != DialogueSide.Left && side != DialogueSide.Right)
            {
                error = "표시 위치를 선택해 주세요.";
                return false;
            }
            if (text.Length == 0)
            {
                error = "대사를 입력해 주세요.";
                return false;
            }
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
            {
                error = "빠른 입력에서는 한 번에 표시할 문장 한 줄만 추가할 수 있습니다.";
                return false;
            }
            if (text.Length > DialogueLine.MaxTextLength)
            {
                error = $"한 줄은 {DialogueLine.MaxTextLength}자를 넘을 수 없습니다.";
                return false;
            }
            if (side != DialogueSide.Narrator)
            {
                if (speakerName.Length == 0)
                {
                    error = "화자 이름을 입력해 주세요.";
                    return false;
                }
                if (ContainsDeclarationSeparator(speakerName) || ContainsLineBreak(speakerName))
                {
                    error = "화자 이름에는 | 또는 줄바꿈을 사용할 수 없습니다.";
                    return false;
                }
                if (ContainsDeclarationSeparator(speakerRole) || ContainsLineBreak(speakerRole))
                {
                    error = "역할·호칭에는 | 또는 줄바꿈을 사용할 수 없습니다.";
                    return false;
                }
            }

            string directive;
            if (side == DialogueSide.Narrator) directive = "@narrator";
            else
            {
                directive = side == DialogueSide.Left ? "@left " : "@right ";
                directive += speakerName;
                if (speakerRole.Length > 0) directive += " | " + speakerRole;
            }

            string escapedText = text[0] == '@' || text[0] == '#' ? "\\" + text : text;
            var builder = new StringBuilder(source.TrimEnd('\n'));
            if (builder.Length > 0) builder.Append("\n\n");
            builder.Append(directive).Append('\n').Append(escapedText).Append('\n');
            result = builder.ToString();
            error = string.Empty;
            return true;
        }

        public static bool TryAppendStageCommand(ref string source, DialogueStageQuickCommand command,
            DialogueSide side, DialogueSide targetSide, string speakerName, string speakerRole,
            out string error)
        {
            string normalizedSource = NormalizeLineEndings(source ?? string.Empty);
            speakerName = (speakerName ?? string.Empty).Trim();
            speakerRole = (speakerRole ?? string.Empty).Trim();

            if (!IsStageSide(side))
            {
                error = "등장·퇴장할 위치를 왼쪽 또는 오른쪽으로 선택해 주세요.";
                return false;
            }

            string directive;
            switch (command)
            {
                case DialogueStageQuickCommand.Show:
                    if (speakerName.Length == 0 && speakerRole.Length > 0)
                    {
                        error = "역할·호칭을 적으려면 화자 이름도 함께 입력해 주세요.";
                        return false;
                    }
                    if (ContainsDeclarationSeparator(speakerName) || ContainsLineBreak(speakerName))
                    {
                        error = "화자 이름에는 | 또는 줄바꿈을 사용할 수 없습니다.";
                        return false;
                    }
                    if (ContainsDeclarationSeparator(speakerRole) || ContainsLineBreak(speakerRole))
                    {
                        error = "역할·호칭에는 | 또는 줄바꿈을 사용할 수 없습니다.";
                        return false;
                    }
                    directive = "@show " + StageSideToken(side);
                    if (speakerName.Length > 0)
                    {
                        directive += " " + speakerName;
                        if (speakerRole.Length > 0) directive += " | " + speakerRole;
                    }
                    break;
                case DialogueStageQuickCommand.Hide:
                    directive = "@hide " + StageSideToken(side);
                    break;
                case DialogueStageQuickCommand.Move:
                    if (!IsStageSide(targetSide))
                    {
                        error = "이동할 위치를 왼쪽 또는 오른쪽으로 선택해 주세요.";
                        return false;
                    }
                    if (side == targetSide)
                    {
                        error = "출발 위치와 도착 위치는 달라야 합니다.";
                        return false;
                    }
                    directive = "@move " + StageSideToken(side) + " " + StageSideToken(targetSide);
                    break;
                default:
                    error = "무대 명령을 선택해 주세요.";
                    return false;
            }

            var builder = new StringBuilder(normalizedSource.TrimEnd('\n'));
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(directive).Append('\n');
            source = builder.ToString();
            error = string.Empty;
            return true;
        }

        public static bool TryAppendValidatedStageCommand(string documentId, string source,
            DialogueStageQuickCommand command, DialogueSide side, DialogueSide targetSide,
            string speakerName, string speakerRole, out string result, out string error)
        {
            string original = source ?? string.Empty;
            string candidate = original;
            result = original;
            if (!TryAppendStageCommand(ref candidate, command, side, targetSide,
                speakerName, speakerRole, out error))
                return false;

            string validationSource = candidate.TrimEnd('\n') +
                "\n@narrator\n__TURN_LIMBO_STAGE_VALIDATION__\n";
            try
            {
                DialogueScriptParser.Parse(
                    string.IsNullOrWhiteSpace(documentId) ? "untitled" : documentId.Trim(),
                    validationSource);
                result = candidate;
                error = string.Empty;
                return true;
            }
            catch (DialogueParseException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool IsDialogueAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            string normalized = NormalizeAssetPath(assetPath);
            return normalized.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                normalized.StartsWith(DialogueAssetRoot + "/", StringComparison.OrdinalIgnoreCase) &&
                !normalized.Contains("/../", StringComparison.Ordinal) &&
                !normalized.EndsWith("/..", StringComparison.Ordinal);
        }

        public static string AbsoluteToAssetPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)) return string.Empty;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(absolutePath);
            string prefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return string.Empty;
            return NormalizeAssetPath(fullPath.Substring(prefix.Length));
        }

        public static string AssetToAbsolutePath(string assetPath)
        {
            if (!IsDialogueAssetPath(assetPath))
                throw new ArgumentException("다이얼로그 텍스트는 Dialogue 폴더 안의 .txt 파일이어야 합니다.", nameof(assetPath));
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot,
                NormalizeAssetPath(assetPath).Replace('/', Path.DirectorySeparatorChar)));
            string dialogueRoot = Path.GetFullPath(Path.Combine(projectRoot,
                DialogueAssetRoot.Replace('/', Path.DirectorySeparatorChar))) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(dialogueRoot, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Dialogue 폴더 밖의 경로는 사용할 수 없습니다.", nameof(assetPath));
            return fullPath;
        }

        public static string ReadAssetText(string assetPath)
            => NormalizeLineEndings(File.ReadAllText(AssetToAbsolutePath(assetPath), Encoding.UTF8));

        public static void WriteAssetText(string assetPath, string source, bool importAsset = true)
        {
            string absolutePath = AssetToAbsolutePath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            WriteTextAtomically(absolutePath, NormalizeLineEndings(source ?? string.Empty));
            if (importAsset) AssetDatabase.ImportAsset(NormalizeAssetPath(assetPath),
                ImportAssetOptions.ForceUpdate);
        }

        public static string WriteRecoveryCopy(string documentPath, string source, long baseWriteTicks)
        {
            if (!IsDialogueAssetPath(documentPath))
                throw new ArgumentException("복구할 문서는 Dialogue 폴더 안의 .txt 파일이어야 합니다.",
                    nameof(documentPath));

            string recoveryRoot = GetRecoveryRoot();
            Directory.CreateDirectory(recoveryRoot);
            string documentName = Path.GetFileNameWithoutExtension(documentPath);
            string fileName = documentName + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") +
                "-" + Guid.NewGuid().ToString("N") + ".json";
            string recoveryPath = Path.Combine(recoveryRoot, fileName);
            var record = new DialogueRecoveryRecord
            {
                DocumentPath = NormalizeAssetPath(documentPath),
                BaseWriteTicks = baseWriteTicks,
                Source = NormalizeLineEndings(source ?? string.Empty),
                CreatedUtc = DateTime.UtcNow.ToString("O"),
            };
            WriteTextAtomically(recoveryPath, JsonUtility.ToJson(record, true));
            return recoveryPath;
        }

        public static bool TryReadRecoveryCopy(string recoveryPath, out DialogueRecoveryRecord record)
        {
            record = null;
            if (!IsRecoveryCopyPath(recoveryPath) || !File.Exists(recoveryPath)) return false;
            try
            {
                DialogueRecoveryRecord parsed = JsonUtility.FromJson<DialogueRecoveryRecord>(
                    File.ReadAllText(recoveryPath, Encoding.UTF8));
                if (parsed == null || !IsDialogueAssetPath(parsed.DocumentPath) || parsed.Source == null)
                    return false;
                parsed.DocumentPath = NormalizeAssetPath(parsed.DocumentPath);
                parsed.Source = NormalizeLineEndings(parsed.Source);
                record = parsed;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string FindLatestRecoveryCopy()
        {
            string recoveryRoot = GetRecoveryRoot();
            if (!Directory.Exists(recoveryRoot)) return string.Empty;

            string latestPath = string.Empty;
            DateTime latestWriteTime = DateTime.MinValue;
            foreach (string candidate in Directory.GetFiles(recoveryRoot, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (!TryReadRecoveryCopy(candidate, out _)) continue;
                try
                {
                    DateTime writeTime = File.GetLastWriteTimeUtc(candidate);
                    if (writeTime <= latestWriteTime) continue;
                    latestWriteTime = writeTime;
                    latestPath = candidate;
                }
                catch (IOException)
                {
                    // The file may have been removed by another editor instance while scanning.
                }
            }
            return latestPath;
        }

        public static bool IsRecoveryCopyPath(string recoveryPath)
        {
            if (string.IsNullOrWhiteSpace(recoveryPath)) return false;
            try
            {
                string root = Path.GetFullPath(GetRecoveryRoot()).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(recoveryPath);
                return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static long GetLastWriteTicks(string assetPath)
        {
            string path = AssetToAbsolutePath(assetPath);
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
        }

        public static string NormalizeLineEndings(string value)
            => (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

        private static IReadOnlyList<DialogueSpeakerSummary> CollectSpeakers(DialogueScript script)
        {
            var result = new List<DialogueSpeakerSummary>();
            var indexByName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < script.Lines.Count; index++)
            {
                DialogueLine line = script.Lines[index];
                if (line.ShowsNameplate)
                    CollectSpeaker(line.SpeakerName, line.SpeakerRole, result, indexByName);
                CollectSpeaker(line.Stage.Left?.SpeakerName, line.Stage.Left?.SpeakerRole, result, indexByName);
                CollectSpeaker(line.Stage.Right?.SpeakerName, line.Stage.Right?.SpeakerRole, result, indexByName);
            }
            return result.AsReadOnly();
        }

        private static void CollectSpeaker(string name, string role, IList<DialogueSpeakerSummary> result,
            IDictionary<string, int> indexByName)
        {
            if (string.IsNullOrEmpty(name)) return;
            role = role ?? string.Empty;
            if (!indexByName.TryGetValue(name, out int existing))
            {
                indexByName.Add(name, result.Count);
                result.Add(new DialogueSpeakerSummary(name, role));
            }
            else if (result[existing].Role.Length == 0 && role.Length > 0)
            {
                result[existing] = new DialogueSpeakerSummary(name, role);
            }
        }

        private static bool IsStageSide(DialogueSide side)
            => side == DialogueSide.Left || side == DialogueSide.Right;

        private static string StageSideToken(DialogueSide side)
            => side == DialogueSide.Left ? "left" : "right";

        private static bool ContainsDeclarationSeparator(string value)
            => value.IndexOf('|') >= 0;

        private static bool ContainsLineBreak(string value)
            => value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;

        private static string NormalizeAssetPath(string value) => value.Replace('\\', '/').Trim();

        private static string GetRecoveryRoot()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Library", "TurnLimbo", "DialogueRecovery");
        }

        private static void WriteTextAtomically(string destinationPath, string value)
        {
            string tempPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tempPath, value, new UTF8Encoding(false));
                if (!File.Exists(destinationPath))
                {
                    File.Move(tempPath, destinationPath);
                    return;
                }

                try
                {
                    File.Replace(tempPath, destinationPath, null);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithMoves(tempPath, destinationPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static void ReplaceWithMoves(string tempPath, string destinationPath)
        {
            string backupPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".backup";
            File.Move(destinationPath, backupPath);
            try
            {
                File.Move(tempPath, destinationPath);
                File.Delete(backupPath);
            }
            catch
            {
                if (!File.Exists(destinationPath) && File.Exists(backupPath))
                    File.Move(backupPath, destinationPath);
                throw;
            }
            finally
            {
                if (File.Exists(backupPath) && File.Exists(destinationPath)) File.Delete(backupPath);
            }
        }
    }
}
