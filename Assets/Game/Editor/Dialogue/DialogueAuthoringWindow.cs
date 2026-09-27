using System;
using System.IO;
using System.Text;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.Dialogue;
using UnityEditor;
using UnityEngine;

namespace TurnLimbo.EditorTools.Dialogue
{
    public sealed class DialogueAuthoringWindow : EditorWindow
    {
        private const string PortraitCatalogAssetPath = "Assets/Game/Resources/DialoguePortraitCatalog.asset";
        private const string RecoveryFileKey = "TurnLimbo.DialogueEditor.RecoveryFile";
        private const string RecoveryDocumentKey = "TurnLimbo.DialogueEditor.RecoveryDocument";
        private const string RecoveryTicksKey = "TurnLimbo.DialogueEditor.RecoveryTicks";
        private const double AutoSaveDelay = 0.8d;
        private static readonly string[] SideLabels = { "내레이션", "왼쪽 화자", "오른쪽 화자" };
        private static readonly string[] StageSideLabels = { "왼쪽", "오른쪽" };
        private static readonly string[] StageCommandLabels = { "등장", "퇴장", "이동" };

        [SerializeField] private string documentPath = string.Empty;
        [SerializeField] private string source = string.Empty;
        [SerializeField] private bool dirty;
        [SerializeField] private bool autoSave = true;
        [SerializeField] private bool externalChangeDetected;
        [SerializeField] private string saveError = string.Empty;
        [SerializeField] private long lastKnownWriteTicks;
        [SerializeField] private DialogueSide quickSide = DialogueSide.Narrator;
        [SerializeField] private string quickSpeakerName = string.Empty;
        [SerializeField] private string quickSpeakerRole = string.Empty;
        [SerializeField] private string quickText = string.Empty;
        [SerializeField] private Sprite quickPortrait;
        [SerializeField] private DialogueStageQuickCommand quickStageCommand;
        [SerializeField] private DialogueSide quickStageSide = DialogueSide.Left;
        [SerializeField] private DialogueSide quickStageTargetSide = DialogueSide.Right;
        [SerializeField] private string quickStageSpeakerName = string.Empty;
        [SerializeField] private string quickStageSpeakerRole = string.Empty;
        [SerializeField] private Sprite quickStagePortrait;
        [SerializeField] private int previewIndex;
        [SerializeField] private Vector2 sourceScroll;
        [SerializeField] private Vector2 sidebarScroll;

        private DialogueDocumentValidation validation;
        private DialoguePortraitCatalog portraitCatalog;
        private GUIStyle sourceStyle;
        private GUIStyle lineNumberStyle;
        private double lastEditTime;
        private double nextExternalCheck;
        private bool saving;
        private string recoveryFilePath = string.Empty;
        private string recoveryDocumentPath = string.Empty;
        private long recoveryBaseWriteTicks;
        private bool recoveryLoaded;

        [MenuItem("Turn Limbo/다이얼로그 편집기", false, 20)]
        public static void OpenWindow()
        {
            var window = GetWindow<DialogueAuthoringWindow>();
            window.titleContent = new GUIContent("다이얼로그 편집기");
            window.minSize = new Vector2(940f, 620f);
            window.Show();

            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(window.documentPath) &&
                DialogueAuthoringUtility.IsDialogueAssetPath(selectedPath))
                window.OpenDocument(selectedPath);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("다이얼로그 편집기");
            minSize = new Vector2(940f, 620f);
            portraitCatalog = AssetDatabase.LoadAssetAtPath<DialoguePortraitCatalog>(PortraitCatalogAssetPath);
            LoadRecoveryState();
            Undo.undoRedoPerformed += HandleUndoRedo;
            EditorApplication.update += EditorUpdate;
            if (!string.IsNullOrEmpty(documentPath) && lastKnownWriteTicks == 0L &&
                DialogueAuthoringUtility.IsDialogueAssetPath(documentPath) &&
                File.Exists(DialogueAuthoringUtility.AssetToAbsolutePath(documentPath)))
                ReloadDocument(false);
            else Revalidate();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            EditorApplication.update -= EditorUpdate;
            if (dirty)
            {
                bool saved = !externalChangeDetected &&
                    SaveDocument(true, !EditorApplication.isCompiling && !EditorApplication.isUpdating);
                if (!saved) PreserveRecoveryCopy();
            }
        }

        private void OnLostFocus()
        {
            if (autoSave && dirty && !externalChangeDetected) SaveDocument(true);
        }

        private void OnGUI()
        {
            if (validation == null) Revalidate();
            if (sourceStyle == null)
            {
                sourceStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = false,
                    font = EditorStyles.textArea.font,
                };
                lineNumberStyle = new GUIStyle(sourceStyle)
                {
                    alignment = TextAnchor.UpperRight,
                    wordWrap = false,
                };
            }

            DrawToolbar();
            DrawDocumentStatus();
            if (string.IsNullOrEmpty(documentPath))
            {
                EditorGUILayout.HelpBox(
                    "새 문서를 만들거나 Dialogue 폴더의 텍스트 파일을 여세요. 새 문서는 빈 파일로 생성되며 예시 스토리를 넣지 않습니다.",
                    MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSourceEditor();
                DrawSidebar();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("새 문서", EditorStyles.toolbarButton, GUILayout.Width(68f))) CreateNewDocument();
                if (GUILayout.Button("열기", EditorStyles.toolbarButton, GUILayout.Width(48f))) ChooseDocument();

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(documentPath)))
                {
                    if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(48f))) SaveDocument(false);
                    if (GUILayout.Button("다른 이름", EditorStyles.toolbarButton, GUILayout.Width(70f))) SaveDocumentAs();
                    if (GUILayout.Button("다시 읽기", EditorStyles.toolbarButton, GUILayout.Width(70f))) ReloadDocument(true);
                }

                GUILayout.FlexibleSpace();
                bool updatedAutoSave = GUILayout.Toggle(autoSave, "자동 저장", EditorStyles.toolbarButton,
                    GUILayout.Width(72f));
                if (updatedAutoSave != autoSave)
                {
                    Undo.RecordObject(this, "자동 저장 설정 변경");
                    autoSave = updatedAutoSave;
                    if (autoSave && dirty) lastEditTime = EditorApplication.timeSinceStartup;
                }
                string saveState = !string.IsNullOrEmpty(saveError) ? "! 저장 실패" :
                    dirty ? "● 저장 필요" : "✓ 저장됨";
                GUILayout.Label(saveState, EditorStyles.miniLabel,
                    GUILayout.Width(82f));
            }
        }

        private void DrawDocumentStatus()
        {
            if (HasRecoveryCopy() && !recoveryLoaded)
            {
                EditorGUILayout.HelpBox(
                    $"저장하지 못한 편집 내용의 복구본이 있습니다.\n{recoveryDocumentPath}",
                    MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("복구본 열기")) RestoreRecoveryCopy();
                    if (GUILayout.Button("복구본 버리기")) ClearRecoveryState(true);
                }
            }
            else if (HasRecoveryCopy())
            {
                EditorGUILayout.HelpBox(
                    "복구본을 편집 중입니다. 원본 파일 저장이 성공하면 복구본이 자동으로 삭제됩니다.",
                    MessageType.Info);
            }

            if (!string.IsNullOrEmpty(documentPath))
                EditorGUILayout.LabelField(documentPath, EditorStyles.miniLabel);

            if (externalChangeDetected)
            {
                bool documentExists = !string.IsNullOrEmpty(documentPath) &&
                    File.Exists(DialogueAuthoringUtility.AssetToAbsolutePath(documentPath));
                EditorGUILayout.HelpBox(
                    documentExists
                        ? "파일이 Unity 밖에서 변경되었습니다. 자동 저장을 멈췄습니다. 외부 변경을 다시 읽거나 현재 편집 내용을 덮어쓸지 선택하세요."
                        : "열어 둔 파일이 삭제되었습니다. 자동 저장을 멈췄습니다. 현재 편집 내용으로 파일을 복구할 수 있습니다.",
                    MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (documentExists && GUILayout.Button("외부 변경 다시 읽기")) ReloadDocument(false);
                    string writeLabel = documentExists ? "현재 내용으로 덮어쓰기" : "현재 내용으로 파일 복구";
                    if (GUILayout.Button(writeLabel))
                    {
                        SaveDocument(false, true, true);
                    }
                }
            }

            if (!string.IsNullOrEmpty(saveError))
            {
                EditorGUILayout.HelpBox("마지막 저장에 실패했습니다. 자동 저장을 멈췄습니다.\n" + saveError,
                    MessageType.Error);
                if (GUILayout.Button("저장 다시 시도")) SaveDocument(false);
            }

            switch (validation.State)
            {
                case DialogueDocumentState.Draft:
                    EditorGUILayout.HelpBox(validation.Message, MessageType.Info);
                    break;
                case DialogueDocumentState.Valid:
                    EditorGUILayout.HelpBox(
                        $"규칙 검사 통과 · {validation.Script.Lines.Count}개 표시 줄 · {validation.Speakers.Count}명 화자",
                        MessageType.Info);
                    break;
                case DialogueDocumentState.Error:
                    EditorGUILayout.HelpBox(validation.Message, MessageType.Error);
                    break;
            }
        }

        private void DrawSourceEditor()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(480f, position.width * .59f))))
            {
                EditorGUILayout.LabelField("스토리 원문", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("한 줄이 한 번의 진행 단위입니다. 주석과 수동 서식은 그대로 보존됩니다.",
                    EditorStyles.miniLabel);
                sourceScroll = EditorGUILayout.BeginScrollView(sourceScroll, GUILayout.ExpandHeight(true));
                float contentHeight = Mathf.Max(430f,
                    (CountPhysicalLines(source) + 1) * Mathf.Max(16f, sourceStyle.lineHeight) + 12f);
                string updated;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.TextArea(BuildLineNumbers(source), lineNumberStyle,
                        GUILayout.Width(46f), GUILayout.Height(contentHeight));
                    GUI.SetNextControlName("Dialogue Story Source");
                    updated = EditorGUILayout.TextArea(source ?? string.Empty, sourceStyle,
                        GUILayout.ExpandWidth(true), GUILayout.Height(contentHeight));
                }
                EditorGUILayout.EndScrollView();
                if (!string.Equals(updated, source, StringComparison.Ordinal))
                {
                    Undo.RecordObject(this, "다이얼로그 텍스트 편집");
                    source = updated;
                    MarkEdited();
                }
            }
        }

        private void DrawSidebar()
        {
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll, GUILayout.ExpandHeight(true));
            DrawQuickEntry();
            EditorGUILayout.Space(10f);
            DrawStageQuickEntry();
            EditorGUILayout.Space(10f);
            DrawPortraitAssignments();
            EditorGUILayout.Space(10f);
            DrawPreview();
            EditorGUILayout.EndScrollView();
        }

        private void DrawQuickEntry()
        {
            EditorGUILayout.LabelField("빠른 입력", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("입력한 블록을 원문 끝에 문법에 맞춰 추가합니다.", MessageType.None);
            int side = EditorGUILayout.Popup("표시 위치", (int)quickSide, SideLabels);
            quickSide = (DialogueSide)side;
            if (quickSide != DialogueSide.Narrator)
            {
                quickSpeakerName = EditorGUILayout.TextField("화자 이름", quickSpeakerName);
                quickSpeakerRole = EditorGUILayout.TextField("역할·호칭", quickSpeakerRole);
                quickPortrait = (Sprite)EditorGUILayout.ObjectField("화자 이미지", quickPortrait,
                    typeof(Sprite), false);
            }
            quickText = EditorGUILayout.TextField("표시할 문장", quickText);
            EditorGUILayout.LabelField($"{(quickText ?? string.Empty).Length} / {DialogueLine.MaxTextLength}자",
                EditorStyles.miniLabel);

            bool canAppend = DialogueAuthoringUtility.TryAppendBlock(source, quickSide,
                quickSpeakerName, quickSpeakerRole, quickText, out string appended, out string error);
            using (new EditorGUI.DisabledScope(!canAppend))
            {
                if (GUILayout.Button("원문 끝에 추가", GUILayout.Height(28f)))
                {
                    Undo.RecordObject(this, "다이얼로그 표시 줄 추가");
                    source = appended;
                    quickText = string.Empty;
                    previewIndex = int.MaxValue;
                    MarkEdited();
                    if (quickSide != DialogueSide.Narrator && quickPortrait != null)
                    {
                        try { AssignPortrait(quickSpeakerName.Trim(), quickPortrait); }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                            EditorUtility.DisplayDialog("화자 이미지 저장 실패",
                                "텍스트는 저장 대기 상태로 유지했습니다.\n" + exception.Message, "확인");
                        }
                    }
                    GUI.FocusControl(null);
                }
            }
            if (!canAppend && !string.IsNullOrWhiteSpace(quickText))
                EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        private void DrawStageQuickEntry()
        {
            EditorGUILayout.LabelField("무대 연출", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "초상화는 퇴장·이동·교체할 때까지 유지되며, 말하지 않는 화자는 자동으로 어두워집니다.",
                MessageType.None);
            quickStageCommand = (DialogueStageQuickCommand)EditorGUILayout.Popup("명령",
                (int)quickStageCommand, StageCommandLabels);
            quickStageSide = StageSidePopup("위치", quickStageSide);

            if (quickStageCommand == DialogueStageQuickCommand.Move)
                quickStageTargetSide = StageSidePopup("이동할 위치", quickStageTargetSide);
            else if (quickStageCommand == DialogueStageQuickCommand.Show)
            {
                quickStageSpeakerName = EditorGUILayout.TextField("화자 이름", quickStageSpeakerName);
                bool declaresSpeaker = !string.IsNullOrWhiteSpace(quickStageSpeakerName);
                using (new EditorGUI.DisabledScope(!declaresSpeaker))
                {
                    quickStageSpeakerRole = EditorGUILayout.TextField("역할·호칭", quickStageSpeakerRole);
                    quickStagePortrait = (Sprite)EditorGUILayout.ObjectField("화자 이미지", quickStagePortrait,
                        typeof(Sprite), false);
                }
                if (!declaresSpeaker)
                {
                    quickStageSpeakerRole = string.Empty;
                    quickStagePortrait = null;
                    EditorGUILayout.LabelField(
                        "이름을 비우면 이 위치의 마지막 화자와 기존 이미지를 그대로 다시 사용합니다.",
                        EditorStyles.wordWrappedMiniLabel);
                }
            }

            string documentId = string.IsNullOrEmpty(documentPath) ? "untitled" :
                Path.GetFileNameWithoutExtension(documentPath);
            string appended;
            bool canAppend;
            string error;
            if (validation.State == DialogueDocumentState.Error)
            {
                appended = source;
                canAppend = DialogueAuthoringUtility.TryAppendStageCommand(ref appended,
                    quickStageCommand, quickStageSide, quickStageTargetSide,
                    quickStageSpeakerName, quickStageSpeakerRole, out error);
            }
            else
            {
                canAppend = DialogueAuthoringUtility.TryAppendValidatedStageCommand(documentId, source,
                    quickStageCommand, quickStageSide, quickStageTargetSide,
                    quickStageSpeakerName, quickStageSpeakerRole, out appended, out error);
            }
            using (new EditorGUI.DisabledScope(!canAppend))
            {
                if (GUILayout.Button("원문 끝에 연출 추가", GUILayout.Height(26f)))
                {
                    Undo.RecordObject(this, "다이얼로그 무대 연출 추가");
                    source = appended;
                    previewIndex = int.MaxValue;
                    MarkEdited();
                    if (quickStageCommand == DialogueStageQuickCommand.Show &&
                        !string.IsNullOrWhiteSpace(quickStageSpeakerName) && quickStagePortrait != null)
                    {
                        try { AssignPortrait(quickStageSpeakerName.Trim(), quickStagePortrait); }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                            EditorUtility.DisplayDialog("화자 이미지 저장 실패",
                                "텍스트는 저장 대기 상태로 유지했습니다.\n" + exception.Message, "확인");
                        }
                    }
                    GUI.FocusControl(null);
                }
            }
            if (!canAppend) EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        private void DrawPortraitAssignments()
        {
            EditorGUILayout.LabelField("화자 이미지", EditorStyles.boldLabel);
            if (!validation.CanPreview)
            {
                EditorGUILayout.HelpBox("규칙 검사를 통과하면 문서에 등장하는 화자별 이미지를 지정할 수 있습니다.",
                    MessageType.None);
                return;
            }
            if (validation.Speakers.Count == 0)
            {
                EditorGUILayout.LabelField("이미지를 지정할 화자가 없습니다.", EditorStyles.miniLabel);
                return;
            }

            for (int index = 0; index < validation.Speakers.Count; index++)
            {
                DialogueSpeakerSummary speaker = validation.Speakers[index];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(speaker.Name, EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(speaker.Role))
                        EditorGUILayout.LabelField(speaker.Role, EditorStyles.miniLabel);
                    Sprite current = portraitCatalog == null ? null : portraitCatalog.FindPortrait(speaker.Name);
                    Sprite updated = (Sprite)EditorGUILayout.ObjectField(current, typeof(Sprite), false,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (updated != current) AssignPortrait(speaker.Name, updated);
                }
            }
            EditorGUILayout.LabelField("같은 이름의 화자는 모든 문서에서 같은 기본 이미지를 사용합니다.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("현재 줄 미리보기", EditorStyles.boldLabel);
            if (!validation.CanPreview)
            {
                EditorGUILayout.HelpBox("빈 초안 또는 오류가 있는 문서는 미리보기를 표시하지 않습니다.",
                    MessageType.None);
                return;
            }

            int count = validation.Script.Lines.Count;
            previewIndex = Mathf.Clamp(previewIndex, 0, count - 1);
            previewIndex = EditorGUILayout.IntSlider("표시 줄", previewIndex + 1, 1, count) - 1;
            DialogueLine line = validation.Script.Lines[previewIndex];
            string location = line.Side == DialogueSide.Narrator ? "내레이션" :
                line.Side == DialogueSide.Left ? "왼쪽" : "오른쪽";
            EditorGUILayout.LabelField(location, EditorStyles.miniBoldLabel);
            if (line.ShowsNameplate)
            {
                EditorGUILayout.LabelField(line.SpeakerName, EditorStyles.boldLabel);
                if (line.SpeakerRole.Length > 0) EditorGUILayout.LabelField(line.SpeakerRole, EditorStyles.miniLabel);
            }
            EditorGUILayout.HelpBox(line.Text, MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStageSlotPreview("왼쪽", line.Stage.Left, line.Side == DialogueSide.Left);
                DrawStageSlotPreview("오른쪽", line.Stage.Right, line.Side == DialogueSide.Right);
            }
        }

        private void DrawStageSlotPreview(string label, DialogueStageSlot slot, bool active)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(130f)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                if (slot == null)
                {
                    EditorGUILayout.LabelField("비어 있음", EditorStyles.miniLabel);
                    GUILayoutUtility.GetRect(100f, 120f, GUILayout.ExpandWidth(true));
                    return;
                }

                EditorGUILayout.LabelField(active ? "발화 중" : "대기 (어둡게)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(slot.SpeakerName, EditorStyles.boldLabel);
                if (slot.SpeakerRole.Length > 0)
                    EditorGUILayout.LabelField(slot.SpeakerRole, EditorStyles.miniLabel);
                Sprite sprite = portraitCatalog == null ? null : portraitCatalog.FindPortrait(slot.SpeakerName);
                Texture preview = sprite == null ? null :
                    AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
                Rect previewRect = GUILayoutUtility.GetRect(100f, 140f, GUILayout.ExpandWidth(true));
                if (preview == null)
                {
                    EditorGUI.LabelField(previewRect, "이미지 미지정", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                Color previousColor = GUI.color;
                GUI.color = active ? Color.white : new Color(.42f, .42f, .42f, 1f);
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);
                GUI.color = previousColor;
            }
        }

        private static DialogueSide StageSidePopup(string label, DialogueSide value)
        {
            int selected = value == DialogueSide.Right ? 1 : 0;
            return EditorGUILayout.Popup(label, selected, StageSideLabels) == 0
                ? DialogueSide.Left
                : DialogueSide.Right;
        }

        private void CreateNewDocument()
        {
            if (!ConfirmLeaveCurrentDocument()) return;
            string path = EditorUtility.SaveFilePanelInProject("새 다이얼로그 문서", "dialogue", "txt",
                "Dialogue 폴더 안에 저장할 빈 문서 이름을 정하세요.", DialogueAuthoringUtility.DialogueAssetRoot);
            if (string.IsNullOrEmpty(path)) return;
            if (!DialogueAuthoringUtility.IsDialogueAssetPath(path))
            {
                EditorUtility.DisplayDialog("잘못된 위치",
                    "다이얼로그 문서는 Assets/Game/Resources/Dialogue 폴더 안에 만들어야 합니다.", "확인");
                return;
            }
            DialogueAuthoringUtility.WriteAssetText(path, string.Empty);
            OpenDocument(path);
        }

        private void ChooseDocument()
        {
            if (!ConfirmLeaveCurrentDocument()) return;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "Game/Resources/Dialogue"));
            string absolutePath = EditorUtility.OpenFilePanel("다이얼로그 문서 열기", root, "txt");
            if (string.IsNullOrEmpty(absolutePath)) return;
            string path = DialogueAuthoringUtility.AbsoluteToAssetPath(absolutePath);
            if (!DialogueAuthoringUtility.IsDialogueAssetPath(path))
            {
                EditorUtility.DisplayDialog("잘못된 위치",
                    "Dialogue 폴더 안의 .txt 파일만 열 수 있습니다.", "확인");
                return;
            }
            OpenDocument(path);
        }

        private void SaveDocumentAs()
        {
            string defaultName = string.IsNullOrEmpty(documentPath) ? "dialogue" :
                Path.GetFileNameWithoutExtension(documentPath);
            string path = EditorUtility.SaveFilePanelInProject("다이얼로그 다른 이름으로 저장", defaultName,
                "txt", "Dialogue 폴더 안에 저장하세요.", DialogueAuthoringUtility.DialogueAssetRoot);
            if (string.IsNullOrEmpty(path)) return;
            if (!DialogueAuthoringUtility.IsDialogueAssetPath(path))
            {
                EditorUtility.DisplayDialog("잘못된 위치",
                    "다이얼로그 문서는 Assets/Game/Resources/Dialogue 폴더 안에 저장해야 합니다.", "확인");
                return;
            }
            documentPath = path.Replace('\\', '/');
            lastKnownWriteTicks = File.Exists(DialogueAuthoringUtility.AssetToAbsolutePath(documentPath))
                ? DialogueAuthoringUtility.GetLastWriteTicks(documentPath) : 0L;
            dirty = true;
            externalChangeDetected = false;
            if (SaveDocument(false, true, true))
            {
                if (recoveryLoaded) ClearRecoveryState(true);
                Undo.ClearUndo(this);
            }
            SelectCurrentAsset();
        }

        private void OpenDocument(string path)
        {
            documentPath = path.Replace('\\', '/');
            source = DialogueAuthoringUtility.ReadAssetText(documentPath);
            dirty = false;
            externalChangeDetected = false;
            saveError = string.Empty;
            lastKnownWriteTicks = DialogueAuthoringUtility.GetLastWriteTicks(documentPath);
            previewIndex = 0;
            Undo.ClearUndo(this);
            Revalidate();
            SelectCurrentAsset();
            Repaint();
        }

        private bool SaveDocument(bool silent, bool importAsset = true, bool forceOverwrite = false)
        {
            if (string.IsNullOrEmpty(documentPath)) return false;
            long diskWriteTicks = DialogueAuthoringUtility.GetLastWriteTicks(documentPath);
            bool diskChanged = lastKnownWriteTicks != 0L && diskWriteTicks != lastKnownWriteTicks;
            if ((externalChangeDetected || diskChanged) && !forceOverwrite)
            {
                externalChangeDetected = true;
                if (silent) return false;
                bool overwrite = EditorUtility.DisplayDialog("외부 변경 덮어쓰기",
                    "Unity 밖에서 바뀐 파일을 현재 내용으로 덮어쓸까요?", "덮어쓰기", "취소");
                if (!overwrite) return false;
                forceOverwrite = true;
            }
            if (externalChangeDetected && !forceOverwrite) return false;

            try
            {
                saving = true;
                DialogueAuthoringUtility.WriteAssetText(documentPath, source, importAsset);
                lastKnownWriteTicks = DialogueAuthoringUtility.GetLastWriteTicks(documentPath);
                dirty = false;
                externalChangeDetected = false;
                saveError = string.Empty;
                if (recoveryLoaded ||
                    string.Equals(recoveryDocumentPath, documentPath, StringComparison.OrdinalIgnoreCase))
                    ClearRecoveryState(true);
                return true;
            }
            catch (Exception exception)
            {
                saveError = exception.Message;
                if (!silent) EditorUtility.DisplayDialog("저장 실패", exception.Message, "확인");
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                saving = false;
                Repaint();
            }
        }

        private void ReloadDocument(bool confirmDirty)
        {
            if (string.IsNullOrEmpty(documentPath)) return;
            if (!File.Exists(DialogueAuthoringUtility.AssetToAbsolutePath(documentPath)))
            {
                externalChangeDetected = true;
                EditorUtility.DisplayDialog("파일을 찾을 수 없음",
                    "열어 둔 다이얼로그 파일이 삭제되었습니다. 현재 내용으로 파일을 복구하거나 다른 문서를 여세요.", "확인");
                Repaint();
                return;
            }
            if (confirmDirty && dirty && !EditorUtility.DisplayDialog("변경 내용 버리기",
                    "저장하지 않은 현재 편집 내용을 버리고 파일을 다시 읽을까요?", "다시 읽기", "취소"))
                return;
            bool discardLoadedRecovery = recoveryLoaded;
            OpenDocument(documentPath);
            if (discardLoadedRecovery) ClearRecoveryState(true);
        }

        private bool ConfirmLeaveCurrentDocument()
        {
            if (!dirty) return true;
            int choice = EditorUtility.DisplayDialogComplex("저장하지 않은 변경",
                "현재 다이얼로그의 변경 내용을 어떻게 할까요?", "저장", "취소", "버리기");
            if (choice == 1) return false;
            if (choice == 2)
            {
                if (recoveryLoaded) ClearRecoveryState(true);
                return true;
            }
            return SaveDocument(false);
        }

        private void AssignPortrait(string speakerName, Sprite sprite)
        {
            if (portraitCatalog == null && sprite == null) return;
            if (portraitCatalog == null)
            {
                portraitCatalog = CreateInstance<DialoguePortraitCatalog>();
                AssetDatabase.CreateAsset(portraitCatalog, PortraitCatalogAssetPath);
            }
            Undo.RecordObject(portraitCatalog, "다이얼로그 화자 이미지 변경");
            portraitCatalog.SetPortraitForEditor(speakerName, sprite);
            EditorUtility.SetDirty(portraitCatalog);
            AssetDatabase.SaveAssetIfDirty(portraitCatalog);
            Repaint();
        }

        private void MarkEdited()
        {
            dirty = true;
            lastEditTime = EditorApplication.timeSinceStartup;
            Revalidate();
            Repaint();
        }

        private void Revalidate()
        {
            string id = string.IsNullOrEmpty(documentPath) ? "untitled" :
                Path.GetFileNameWithoutExtension(documentPath);
            validation = DialogueAuthoringUtility.Validate(id, source);
            if (validation.CanPreview)
                previewIndex = Mathf.Clamp(previewIndex, 0, validation.Script.Lines.Count - 1);
        }

        private void HandleUndoRedo()
        {
            dirty = !string.IsNullOrEmpty(documentPath);
            lastEditTime = EditorApplication.timeSinceStartup;
            Revalidate();
            Repaint();
        }

        private void EditorUpdate()
        {
            if (string.IsNullOrEmpty(documentPath) || saving) return;
            double now = EditorApplication.timeSinceStartup;
            if (now >= nextExternalCheck)
            {
                nextExternalCheck = now + 1d;
                long writeTicks = DialogueAuthoringUtility.GetLastWriteTicks(documentPath);
                if (lastKnownWriteTicks != 0L && writeTicks != lastKnownWriteTicks)
                {
                    if (dirty || writeTicks == 0L)
                    {
                        externalChangeDetected = true;
                        Repaint();
                    }
                    else ReloadDocument(false);
                }
            }
            if (autoSave && dirty && !externalChangeDetected && string.IsNullOrEmpty(saveError) &&
                now - lastEditTime >= AutoSaveDelay)
                SaveDocument(true);
        }

        private void SelectCurrentAsset()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(documentPath);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void LoadRecoveryState()
        {
            recoveryLoaded = false;
            recoveryFilePath = SessionState.GetString(RecoveryFileKey, string.Empty);
            if (!DialogueAuthoringUtility.TryReadRecoveryCopy(recoveryFilePath,
                    out DialogueRecoveryRecord record))
            {
                recoveryFilePath = DialogueAuthoringUtility.FindLatestRecoveryCopy();
                if (!DialogueAuthoringUtility.TryReadRecoveryCopy(recoveryFilePath, out record))
                {
                    ClearRecoveryState(false);
                    return;
                }
            }

            recoveryDocumentPath = record.DocumentPath;
            recoveryBaseWriteTicks = record.BaseWriteTicks;
            SessionState.SetString(RecoveryFileKey, recoveryFilePath);
            SessionState.EraseString(RecoveryDocumentKey);
            SessionState.EraseString(RecoveryTicksKey);
        }

        private bool HasRecoveryCopy()
            => !string.IsNullOrEmpty(recoveryFilePath) && File.Exists(recoveryFilePath) &&
               DialogueAuthoringUtility.IsDialogueAssetPath(recoveryDocumentPath);

        private void PreserveRecoveryCopy()
        {
            try
            {
                string previousRecovery = recoveryFilePath;
                string previousRecoveryDocument = recoveryDocumentPath;
                string newRecovery = DialogueAuthoringUtility.WriteRecoveryCopy(
                    documentPath, source, lastKnownWriteTicks);
                recoveryFilePath = newRecovery;
                recoveryDocumentPath = documentPath;
                recoveryBaseWriteTicks = lastKnownWriteTicks;
                recoveryLoaded = false;
                SessionState.SetString(RecoveryFileKey, recoveryFilePath);
                SessionState.EraseString(RecoveryDocumentKey);
                SessionState.EraseString(RecoveryTicksKey);
                if (!string.IsNullOrEmpty(previousRecovery) &&
                    string.Equals(previousRecoveryDocument, documentPath,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(previousRecovery, newRecovery, StringComparison.OrdinalIgnoreCase) &&
                    DialogueAuthoringUtility.IsRecoveryCopyPath(previousRecovery) &&
                    File.Exists(previousRecovery))
                {
                    try { File.Delete(previousRecovery); }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("이전 다이얼로그 복구본을 정리하지 못했습니다: " +
                            exception.Message);
                    }
                }
                Debug.LogWarning("다이얼로그 편집 내용을 원본에 저장하지 못해 복구본을 남겼습니다: " +
                    recoveryFilePath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void RestoreRecoveryCopy()
        {
            if (!DialogueAuthoringUtility.TryReadRecoveryCopy(recoveryFilePath,
                    out DialogueRecoveryRecord record))
            {
                ClearRecoveryState(false);
                return;
            }
            if (dirty && !EditorUtility.DisplayDialog("현재 편집 내용 교체",
                    "현재 창의 편집 내용을 복구본으로 교체할까요?", "복구", "취소"))
                return;

            documentPath = record.DocumentPath;
            source = record.Source;
            recoveryDocumentPath = record.DocumentPath;
            recoveryBaseWriteTicks = record.BaseWriteTicks;
            lastKnownWriteTicks = record.BaseWriteTicks;
            long diskTicks = DialogueAuthoringUtility.GetLastWriteTicks(documentPath);
            externalChangeDetected = diskTicks != lastKnownWriteTicks;
            dirty = true;
            saveError = string.Empty;
            previewIndex = 0;
            Undo.ClearUndo(this);
            recoveryLoaded = true;
            Revalidate();
            Repaint();
        }

        private void ClearRecoveryState(bool deleteFile)
        {
            if (deleteFile && DialogueAuthoringUtility.IsRecoveryCopyPath(recoveryFilePath) &&
                File.Exists(recoveryFilePath))
            {
                try { File.Delete(recoveryFilePath); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            recoveryFilePath = string.Empty;
            recoveryDocumentPath = string.Empty;
            recoveryBaseWriteTicks = 0L;
            recoveryLoaded = false;
            SessionState.EraseString(RecoveryFileKey);
            SessionState.EraseString(RecoveryDocumentKey);
            SessionState.EraseString(RecoveryTicksKey);
        }

        private static int CountPhysicalLines(string value)
        {
            if (string.IsNullOrEmpty(value)) return 1;
            int count = 1;
            for (int index = 0; index < value.Length; index++)
                if (value[index] == '\n') count++;
            return count;
        }

        private static string BuildLineNumbers(string value)
        {
            int count = CountPhysicalLines(value);
            var builder = new StringBuilder(count * 3);
            for (int line = 1; line <= count; line++)
            {
                if (line > 1) builder.Append('\n');
                builder.Append(line);
            }
            return builder.ToString();
        }
    }
}
