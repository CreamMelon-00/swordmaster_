using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using UnityEditor;
using UnityEngine;

namespace TurnLimbo.EditorTools
{
    /// <summary>Downloads the skill sheet from Google Sheets as CSV, checks it with the game's own parser, shows what would
    /// change and only then overwrites <see cref="LegacySkillDefinitions.SheetAssetPath"/>. It also checks and opens the
    /// local file. The designer guide is Docs/SkillSheet.md.</summary>
    public sealed class SkillSheetWindow : EditorWindow
    {
        private const string WindowTitle = "기술 시트";
        private const int TimeoutSeconds = 20;
        // The confirmation dialog lists this many change lines; the window lists them all.
        private const int DialogLineLimit = 12;
        private const char ByteOrderMark = (char)0xFEFF;
        private const string SharingHelp = "구글 시트의 공유 → 일반 액세스를 '링크가 있는 모든 사용자'(뷰어)로 바꾸거나, " +
            "파일 → 공유 → 웹에 게시로 게시한 뒤 다시 받아오세요.";
        internal const string FileInUseHelp = "Excel 등에서 파일을 열어 두었다면 닫고 다시 시도하세요.";

        [SerializeField] private MessageType resultType = MessageType.None;
        [SerializeField] private string resultMessage = string.Empty;
        [SerializeField] private List<string> resultLines = new List<string>();
        [SerializeField] private Vector2 resultScroll;

        private Task<Response> download;
        private CancellationTokenSource cancellation;
        private double downloadStarted;
        private bool cancelRequested;

        private sealed class Response
        {
            public int Status;
            public string Reason;
            public bool Success;
            public byte[] Body;
        }

        [MenuItem("Turn Limbo/기술 시트", false, 40)]
        public static void OpenWindow()
        {
            var window = GetWindow<SkillSheetWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(480f, 360f);
            window.Show();
        }

        private static string SheetFilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            LegacySkillDefinitions.SheetAssetPath));

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            EditorApplication.update += PollDownload;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollDownload;
            if (download == null) return;
            cancellation.Cancel();
            ForgetDownload();
            EditorUtility.ClearProgressBar();
        }

        private void OnGUI()
        {
            bool busy = download != null;
            SkillSheetSettings settings = SkillSheetSettings.instance;

            EditorGUILayout.LabelField("구글 시트", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(busy))
            {
                string url = EditorGUILayout.TextField("주소", settings.Url);
                // Stored as typed; the address is trimmed where it is used.
                if (url != settings.Url) settings.Url = url;
            }
            EditorGUILayout.HelpBox(
                "받을 탭을 연 상태에서 브라우저 주소창의 주소를 붙여 넣으세요. 시트를 공유 → 일반 액세스 → '링크가 있는 모든 사용자'(뷰어)로 " +
                "두거나 파일 → 공유 → 웹에 게시로 게시해야 받을 수 있습니다. 주소는 ProjectSettings/TurnLimboSkillSheet.asset에 저장되어 " +
                "git으로 함께 씁니다.", MessageType.None);
            using (new EditorGUI.DisabledScope(busy))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("구글 시트에서 받아오기", GUILayout.Height(28f))) StartDownload(settings.Url);
                if (GUILayout.Button("시트 열기", GUILayout.Width(96f), GUILayout.Height(28f))) OpenSheet(settings.Url);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("프로젝트의 CSV", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(LegacySkillDefinitions.SheetAssetPath, EditorStyles.miniLabel,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            using (new EditorGUI.DisabledScope(busy))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("CSV 검사")) CheckLocalFile();
                if (GUILayout.Button("CSV 파일 열기")) OpenLocalFile();
                if (GUILayout.Button("폴더에서 보기")) RevealLocalFile();
            }

            DrawResult();
        }

        private void DrawResult()
        {
            if (string.IsNullOrEmpty(resultMessage)) return;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(resultMessage, resultType);
            if (resultLines.Count == 0) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{resultLines.Count}줄", EditorStyles.miniLabel);
                if (GUILayout.Button("결과 복사", EditorStyles.miniButton, GUILayout.Width(72f)))
                    EditorGUIUtility.systemCopyBuffer = resultMessage + "\n" + string.Join("\n", resultLines);
            }
            resultScroll = EditorGUILayout.BeginScrollView(resultScroll);
            foreach (string line in resultLines) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void ShowResult(MessageType type, string message, IEnumerable<string> lines = null)
        {
            resultType = type;
            resultMessage = message;
            resultLines = lines?.ToList() ?? new List<string>();
            resultScroll = Vector2.zero;
            Repaint();
        }

        private void StartDownload(string url)
        {
            if (!SheetUrl.TryGetCsvUrl(url, out string csvUrl, out string error))
            {
                ShowResult(MessageType.Error, error);
                return;
            }
            cancellation = new CancellationTokenSource();
            cancelRequested = false;
            downloadStarted = EditorApplication.timeSinceStartup;
            CancellationToken token = cancellation.Token;
            download = Task.Run(() => FetchAsync(csvUrl, token));
        }

        private static async Task<Response> FetchAsync(string url, CancellationToken token)
        {
            using (var handler = new HttpClientHandler { AllowAutoRedirect = true })
            using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) })
            using (HttpResponseMessage response = await client.GetAsync(url, token).ConfigureAwait(false))
            {
                byte[] body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return new Response
                {
                    Status = (int)response.StatusCode,
                    Reason = response.ReasonPhrase,
                    Success = response.IsSuccessStatusCode,
                    Body = body,
                };
            }
        }

        // Runs every editor frame; the download itself runs on a worker thread.
        private void PollDownload()
        {
            if (download == null) return;
            if (!download.IsCompleted)
            {
                float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - downloadStarted) / TimeoutSeconds));
                if (EditorUtility.DisplayCancelableProgressBar(WindowTitle, "구글 시트에서 받는 중…", progress) && !cancelRequested)
                {
                    cancelRequested = true;
                    cancellation.Cancel();
                }
                return;
            }

            Task<Response> finished = download;
            bool cancelled = cancelRequested;
            ForgetDownload();
            EditorUtility.ClearProgressBar();
            try
            {
                Receive(finished, cancelled);
            }
            catch (Exception exception)
            {
                ShowResult(MessageType.Error, "받은 시트를 처리하지 못했습니다: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ForgetDownload()
        {
            download = null;
            cancellation?.Dispose();
            cancellation = null;
        }

        private void Receive(Task<Response> task, bool cancelled)
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Exception error = task.Exception?.GetBaseException();
                if (cancelled) ShowResult(MessageType.Info, "받아오기를 취소했습니다. CSV는 그대로입니다.");
                else if (task.IsCanceled || error is OperationCanceledException)
                    ShowResult(MessageType.Error, $"{TimeoutSeconds}초 안에 시트를 받지 못했습니다. 인터넷 연결을 확인하고 다시 받아오세요.");
                else ShowResult(MessageType.Error, $"시트를 받지 못했습니다: {error?.Message}\n인터넷 연결과 주소를 확인하세요.");
                return;
            }

            Response response = task.Result;
            if (!response.Success)
            {
                // 404 reads 사백사 and takes 로; 403 (사백삼) and 500 (오백) take 으로.
                string status = response.Status.ToString(CultureInfo.InvariantCulture);
                string ro = KoreanParticle.Attach(status, "로").Substring(status.Length);
                ShowResult(MessageType.Error, $"구글 시트가 오류 {status}({response.Reason}){ro} 답했습니다. " +
                    "주소와 탭이 맞는지 확인하세요. " + SharingHelp);
                return;
            }
            string text = Normalize(new UTF8Encoding(false).GetString(response.Body ?? new byte[0]));
            if (LooksLikeHtml(text))
            {
                ShowResult(MessageType.Error, "구글 시트가 CSV 대신 웹 페이지(로그인 화면 등)를 보냈습니다. " +
                    "시트가 공유되지 않은 것 같습니다. " + SharingHelp);
                return;
            }

            LegacySkillTable downloaded;
            try
            {
                downloaded = LegacySkillSheet.Parse("구글 시트", text);
            }
            catch (SkillSheetException exception)
            {
                ShowResult(MessageType.Error, $"받은 시트에 문제가 {exception.Problems.Count}개 있어 CSV를 바꾸지 않았습니다. " +
                    "구글 시트에서 고친 뒤 다시 받아오세요.", exception.Problems);
                return;
            }
            // A sheet that reads but lacks what the code needs would stop the game later, so it is refused too.
            IReadOnlyList<string> codeProblems = CampaignSheetCheck.Problems(downloaded);
            if (codeProblems.Count > 0)
            {
                ShowResult(MessageType.Error, $"받은 시트가 게임 코드와 맞지 않는 곳이 {codeProblems.Count}개 있어 CSV를 바꾸지 않았습니다. " +
                    "구글 시트에서 고치거나, 의도한 변경이면 프로그래머가 코드를 먼저 고친 뒤 다시 받아오세요.", codeProblems);
                return;
            }
            Offer(downloaded, text);
        }

        /// <summary>Shows what the downloaded sheet changes and, once confirmed, writes it over the project's sheet.</summary>
        private void Offer(LegacySkillTable downloaded, string text)
        {
            var lines = new List<string>();
            LegacySkillTable current = ReadCurrent(out string currentText, out string currentProblem);
            if (currentProblem != null) lines.Add(currentProblem);
            LegacySkillSheetChanges changes = LegacySkillSheet.Describe(current, downloaded);
            lines.AddRange(changes.Lines);
            List<string> warnings = Warnings(downloaded);
            lines.AddRange(warnings);
            MessageType settled = warnings.Count > 0 ? MessageType.Warning : MessageType.Info;
            string warningNote = warnings.Count > 0 ? $" 경고 {warnings.Count}개가 있습니다." : string.Empty;

            if (!changes.HasChanges && text == currentText)
            {
                ShowResult(settled, changes.Summary + " CSV를 그대로 둡니다." + warningNote, lines);
                return;
            }
            // Memo columns, comment rows and spellings such as 10 / 10% do not change the table but do change the file.
            string summary = changes.HasChanges ? changes.Summary
                : $"기술 내용은 그대로이고 메모 열, 주석 행이나 칸 표기만 다릅니다 (기술 {downloaded.All.Count}개).";
            bool playing = EditorApplication.isPlaying;

            var dialog = new StringBuilder(summary);
            if (lines.Count > 0) dialog.Append('\n');
            foreach (string line in lines.Take(DialogLineLimit)) dialog.Append('\n').Append(line);
            if (lines.Count > DialogLineLimit)
                dialog.Append($"\n… 외 {lines.Count - DialogLineLimit}줄은 창의 목록에서 볼 수 있습니다.");
            dialog.Append($"\n\n{LegacySkillDefinitions.SheetAssetPath} 파일을 이 내용으로 덮어씁니다.");
            if (playing) dialog.Append("\n지금은 플레이 중이라 다음 플레이부터 반영됩니다.");
            ShowResult(MessageType.Info, summary, lines);
            if (!EditorUtility.DisplayDialog(WindowTitle, dialog.ToString(), "덮어쓰기", "취소"))
            {
                ShowResult(MessageType.Info, "덮어쓰지 않았습니다. CSV는 그대로입니다. " + summary, lines);
                return;
            }

            try
            {
                WriteSheet(text);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                ShowResult(MessageType.Error, $"CSV를 쓰지 못했습니다: {exception.Message}\n" +
                    "Excel 등에서 파일을 열어 두었다면 닫고 다시 받아오세요.", lines);
                return;
            }
            AssetDatabase.ImportAsset(LegacySkillDefinitions.SheetAssetPath, ImportAssetOptions.ForceUpdate);
            SkillSheetImportWatcher.ReloadTable();
            ShowResult(settled, "CSV를 덮어썼습니다. " + summary + (playing ? " 다음 플레이부터 반영됩니다." : string.Empty) +
                warningNote, lines);
        }

        /// <summary>The project's sheet as it is now, or null (with the reason) when it is missing or has problems, in
        /// which case every downloaded row counts as added.</summary>
        private static LegacySkillTable ReadCurrent(out string text, out string problem)
        {
            text = null;
            problem = null;
            try
            {
                if (!File.Exists(SheetFilePath))
                {
                    problem = "지금 CSV 파일이 없어 모든 기술을 새로 추가하는 것으로 셉니다.";
                    return null;
                }
                text = Normalize(SheetFile.ReadAllText(SheetFilePath));
                return LegacySkillSheet.Parse(LegacySkillDefinitions.SheetAssetPath, text);
            }
            catch (SkillSheetException exception)
            {
                problem = $"지금 CSV에 문제가 {exception.Problems.Count}개 있어 비교할 수 없습니다. 모든 기술을 새로 추가하는 것으로 셉니다." +
                    (exception.Problems.Count > 0 ? " 첫 문제: " + exception.Problems[0] : string.Empty);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                problem = $"지금 CSV를 읽지 못해 모든 기술을 새로 추가하는 것으로 셉니다: {exception.Message} {FileInUseHelp}";
            }
            return null;
        }

        private void CheckLocalFile()
        {
            if (!File.Exists(SheetFilePath))
            {
                ShowResult(MessageType.Error, "CSV 파일이 없습니다: " + SheetFilePath);
                return;
            }
            try
            {
                // Shared reading, so the check works while Excel still has the file open.
                LegacySkillTable table = LegacySkillSheet.Parse(LegacySkillDefinitions.SheetAssetPath,
                    SheetFile.ReadAllText(SheetFilePath));
                IReadOnlyList<string> codeProblems = CampaignSheetCheck.Problems(table);
                List<string> warnings = Warnings(table);
                string counts = $"기술 {table.All.Count}개 (시작 {table.InitialSkills.Count}개, 획득 {table.AcquisitionSkills.Count}개)";
                if (codeProblems.Count > 0)
                    ShowResult(MessageType.Error, $"시트 규칙 문제는 없지만 게임 코드와 맞지 않는 곳이 {codeProblems.Count}개 있습니다. " +
                        $"줄마다 적힌 대로 고치세요. {counts}.", codeProblems.Concat(warnings));
                else if (warnings.Count == 0) ShowResult(MessageType.Info, $"문제가 없습니다. {counts}.");
                else ShowResult(MessageType.Warning, $"규칙 문제는 없지만 경고가 {warnings.Count}개 있습니다. {counts}.", warnings);
            }
            catch (SkillSheetException exception)
            {
                ShowResult(MessageType.Error, $"CSV에 문제가 {exception.Problems.Count}개 있습니다. " +
                    "고칠 때까지 게임이 기술 표를 읽지 못합니다.", exception.Problems);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                ShowResult(MessageType.Error, $"CSV를 읽지 못했습니다: {exception.Message}\n{FileInUseHelp}");
            }
        }

        private void OpenSheet(string url)
        {
            if (!SheetUrl.TryGetCsvUrl(url, out _, out string error))
            {
                ShowResult(MessageType.Error, error);
                return;
            }
            string address = url.Trim();
            Application.OpenURL(address.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? address : "https://" + address);
        }

        private void OpenLocalFile()
        {
            if (File.Exists(SheetFilePath)) EditorUtility.OpenWithDefaultApp(SheetFilePath);
            else ShowResult(MessageType.Error, "CSV 파일이 없습니다: " + SheetFilePath);
        }

        private void RevealLocalFile()
        {
            string path = SheetFilePath;
            EditorUtility.RevealInFinder(File.Exists(path) ? path : Path.GetDirectoryName(path));
        }

        /// <summary>Everything worth knowing that does not block: icon numbers the atlas lacks, and 획득 rows no curriculum
        /// node grants (<see cref="CampaignSheetCheck.Warnings"/>).</summary>
        private static List<string> Warnings(LegacySkillTable table)
        {
            List<string> warnings = IconWarnings(table);
            warnings.AddRange(CampaignSheetCheck.Warnings(table));
            return warnings;
        }

        /// <summary>The atlas has icons 1..<see cref="LegacyDuelArt.SkillIconCount"/> only; any other number draws no icon.
        /// The runtime does not know the atlas, so this is a warning here rather than a sheet problem.</summary>
        private static List<string> IconWarnings(LegacySkillTable table)
        {
            int count = LegacyDuelArt.SkillIconCount;
            var warnings = new List<string>();
            foreach (LegacySkillDefinition definition in table.All)
            {
                LegacySkill skill = definition.Skill;
                if (skill.IconId >= 1 && skill.IconId <= count) continue;
                warnings.Add($"그림: {skill.Id} {skill.Name}의 그림 {skill.IconId}번이 없어 기술 아이콘이 비어 보입니다. " +
                    $"'그림' 칸에 1~{count} 중 하나를 적으세요 (비우면 ID와 같은 번호).");
            }
            return warnings;
        }

        /// <summary>LF line ends, no byte order mark and a final line break, so equal sheets compare equal however they
        /// were saved; <see cref="WriteSheet"/> adds the byte order mark back.</summary>
        private static string Normalize(string text)
        {
            text = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            if (text.Length > 0 && text[0] == ByteOrderMark) text = text.Substring(1);
            return text.Length == 0 || text.EndsWith("\n", StringComparison.Ordinal) ? text : text + "\n";
        }

        private static bool LooksLikeHtml(string text)
        {
            string start = text.TrimStart();
            if (start.StartsWith("<", StringComparison.Ordinal)) return true;
            string head = start.Length > 1024 ? start.Substring(0, 1024) : start;
            return head.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                head.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // UTF-8 with a byte order mark so Excel reads Korean; a temp file then a replace, so a failed write leaves the
        // old sheet whole. The .tmp name keeps Unity from importing the temp file.
        private static void WriteSheet(string text)
        {
            string path = SheetFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, text, new UTF8Encoding(true));
                if (!File.Exists(path)) File.Move(temp, path);
                else
                {
                    try
                    {
                        File.Replace(temp, path, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(temp, path, true);
                    }
                }
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }

    /// <summary>Makes edit mode forget the loaded skill table whenever Unity imports, deletes or moves the sheet, so a change
    /// saved from Excel shows up without a script reload, and logs the sheet's problems at once. A running game keeps the
    /// table it started with: the next Play reads the sheet again anyway (SkillSheetLoader), and edit mode reloads when
    /// Play ends.</summary>
    public sealed class SkillSheetImportWatcher : AssetPostprocessor
    {
        private static bool reloadAfterPlay;

        [InitializeOnLoadMethod]
        private static void WatchPlayMode() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool imported = IsSheet(importedAssets) || IsSheet(movedAssets);
            if (!imported && !IsSheet(deletedAssets) && !IsSheet(movedFromAssetPaths)) return;
            ReloadTable();
            if (imported) LogProblems();
            else Debug.LogError($"기술 시트가 {LegacySkillDefinitions.SheetAssetPath}에서 사라졌습니다. 이 경로에 있어야 게임이 기술을 읽습니다.");
        }

        /// <summary>Forgets the loaded table now, or once Play ends when a game is running.</summary>
        internal static void ReloadTable()
        {
            if (EditorApplication.isPlaying) reloadAfterPlay = true;
            else LegacySkillDefinitions.Reload();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !reloadAfterPlay) return;
            reloadAfterPlay = false;
            LegacySkillDefinitions.Reload();
        }

        // Reads the file itself rather than the table, which may load through Resources, unsafe during an import. The
        // reading is shared: Excel keeps the file open after saving it, which is when this runs.
        private static void LogProblems()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", LegacySkillDefinitions.SheetAssetPath));
            LegacySkillTable table;
            try
            {
                table = LegacySkillSheet.Parse(LegacySkillDefinitions.SheetAssetPath, SheetFile.ReadAllText(path));
            }
            catch (SkillSheetException exception)
            {
                Debug.LogError(exception.Message);
                return;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"기술 시트를 읽지 못했습니다: {path}\n{exception.Message}\n{SkillSheetWindow.FileInUseHelp}");
                return;
            }
            IReadOnlyList<string> problems = CampaignSheetCheck.Problems(table);
            if (problems.Count > 0) Debug.LogError(CampaignSheetCheck.ProblemMessage(table.SheetId, problems));
            IReadOnlyList<string> warnings = CampaignSheetCheck.Warnings(table);
            if (warnings.Count > 0) Debug.LogWarning(CampaignSheetCheck.WarningMessage(table.SheetId, warnings));
        }

        private static bool IsSheet(string[] paths) => paths != null && paths.Any(path =>
            string.Equals(path, LegacySkillDefinitions.SheetAssetPath, StringComparison.OrdinalIgnoreCase));
    }
}
