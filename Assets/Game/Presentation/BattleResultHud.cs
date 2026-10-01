using System;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Displays a settled result snapshot; progression and rewards belong to CampaignRun.</summary>
    public sealed class BattleResultHud : IDisposable
    {
        private readonly LegacyDuelArt art;
        private readonly RectTransform root;
        private readonly Text heading, stage, rounds, playerHealth, enemyHealth, curriculum, curriculumDetail, notice;
        private readonly Button lobbyButton, retryButton, nextButton;
        private readonly Text retryCaption, lobbyCaption, nextCaption;
        private bool disposed;
        private static readonly Color Surface = DuelVisualTheme.Surface;
        private static readonly Color Raised = DuelVisualTheme.RaisedSurface;
        private static readonly Color Border = DuelVisualTheme.Border;
        private static readonly Color Accent = DuelVisualTheme.Accent;
        private static readonly Color Gold = DuelVisualTheme.Paper;
        private static readonly Color Foreground = DuelVisualTheme.Foreground;
        private static readonly Color Muted = DuelVisualTheme.Muted;

        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;

        public BattleResultHud(Transform parent, LegacyDuelArt art, Action returnToLobby, Action retry, Action nextStage)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            root = Rect("Battle Result HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();

            var veil = Panel("Result Backdrop", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .78f));
            Stretch(veil.rectTransform);
            veil.raycastTarget = true;
            var border = Panel("Result Card Border", root, Vector2.zero, new Vector2(844f, 654f), Border);
            var card = Panel("Result Card", border.transform, Vector2.zero, new Vector2(840f, 650f), Surface);
            DuelVisualTheme.DressPanel(card);
            heading = Label("Result Heading", card.transform, Vector2.up * 238f, new Vector2(760f, 76f), 58);
            stage = Label("Result Stage", card.transform, Vector2.up * 179f, new Vector2(760f, 42f), 23, Muted);
            Panel("Result Rule", card.transform, Vector2.up * 144f, new Vector2(736f, 2f), Border);
            rounds = Statistic(card.transform, "Result Rounds", "진행 턴", -248f);
            playerHealth = Statistic(card.transform, "Result Player HP", "내 남은 HP", 0f);
            enemyHealth = Statistic(card.transform, "Result Enemy HP", "상대 남은 HP", 248f);
            // Currency is hidden while it has no use; this panel reports the curriculum instead.
            var progress = Panel("Result Curriculum Panel", card.transform, new Vector2(0f, -61f),
                new Vector2(736f, 104f), Raised);
            DuelVisualTheme.Frame(progress);
            Label("Result Curriculum Heading", progress.transform, new Vector2(-210f, 27f), new Vector2(300f, 28f), 18, Muted).text = "커리큘럼";
            curriculum = Label("Result Curriculum", progress.transform, new Vector2(-210f, -14f), new Vector2(300f, 48f), 30, Gold);
            curriculum.resizeTextForBestFit = true;
            curriculum.resizeTextMinSize = 20;
            curriculum.resizeTextMaxSize = 30;
            curriculumDetail = Label("Result Curriculum Detail", progress.transform, new Vector2(170f, 0f), new Vector2(360f, 72f), 19);
            notice = Label("Result Notice", card.transform, new Vector2(0f, -161f), new Vector2(736f, 78f), 20, Muted);
            lobbyButton = ActionButton("Result Lobby", card.transform, "로비로", new Vector2(-248f, -255f), returnToLobby);
            retryButton = ActionButton("Result Retry", card.transform, "재도전", new Vector2(0f, -255f), retry);
            retryCaption = retryButton.GetComponentInChildren<Text>();
            lobbyCaption = lobbyButton.GetComponentInChildren<Text>();
            nextButton = ActionButton("Result Next Stage", card.transform, "다음 스테이지", new Vector2(248f, -255f), nextStage, true);
            nextCaption = nextButton.GetComponentInChildren<Text>();
            Hide();
        }

        /// <param name="missionExitsToLobby">Whether a mission's exits reach the lobby because no mission is playable
        /// now. Null guesses from the result (a victory from the last 서막 mission on reaches the lobby).</param>
        /// <param name="storyNotice">A mission's first win: what it opened (replaces the usual notice). A stage win:
        /// the story mission that has just arrived (replaces the second line).</param>
        public void Show(BattleResult result, bool? missionExitsToLobby = null, string storyNotice = null)
        {
            if (disposed) return;
            if (result == null) throw new ArgumentNullException(nameof(result));
            root.gameObject.SetActive(true);
            heading.text = result.IsMission
                ? result.Victory ? "임무 완료" : "임무 실패"
                : result.Outcome == DuelMatchOutcome.PlayerVictory ? "승리"
                : result.Outcome == DuelMatchOutcome.Draw ? "무승부" : "패배";
            heading.color = result.Victory ? Accent : result.Outcome == DuelMatchOutcome.Draw ? Foreground : DuelVisualTheme.Danger;
            stage.text = result.IsMission ? $"{ChapterOf(result.StageNumber)}  ·  임무 {result.StageNumber:00}  ·  {result.StageName}"
                : $"스테이지 {result.StageNumber:00}  ·  {result.StageName}";
            rounds.text = result.RoundNumber.ToString();
            playerHealth.text = Mathf.Max(0, result.PlayerHealth).ToString();
            enemyHealth.text = Mathf.Max(0, result.EnemyHealth).ToString();
            ShowCurriculum(result);
            // A mission's exits open the next briefing, or the lobby once the arc is over.
            bool toLobby = result.IsMission && (missionExitsToLobby ?? result.Victory && result.StageNumber >= PrologueMissions.Count);
            bool hasStory = result.Victory && !string.IsNullOrEmpty(storyNotice);
            notice.text = hasStory && result.IsMission ? storyNotice
                : hasStory ? FirstLine(BuildNotice(result, toLobby)) + "\n" + storyNotice
                : BuildNotice(result, toLobby);
            retryCaption.text = "재도전";
            lobbyCaption.text = result.IsMission && !toLobby ? "브리핑으로" : "로비로";
            nextCaption.text = !result.IsMission ? "다음 스테이지" : toLobby ? "여정 계속" : "다음 임무";
            retryButton.interactable = result.CanRetry;
            // After a mission victory the briefing exit would duplicate 다음 임무, so only retry and next remain.
            bool showLobby = !(result.IsMission && result.CanAdvance);
            lobbyButton.gameObject.SetActive(showLobby);
            nextButton.gameObject.SetActive(result.CanAdvance);
            // The visible actions stay evenly spaced.
            bool three = showLobby && result.CanAdvance;
            float left = three ? -248f : -132f;
            lobbyButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(left, -255f);
            retryButton.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(!showLobby ? left : three ? 0f : 132f, -255f);
            nextButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(three ? 248f : 132f, -255f);
            ClearSelection();
        }

        public void Hide()
        {
            if (disposed) return;
            root.gameObject.SetActive(false);
            ClearSelection();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lobbyButton.onClick.RemoveAllListeners();
            retryButton.onClick.RemoveAllListeners();
            nextButton.onClick.RemoveAllListeners();
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private static string FirstLine(string text)
        {
            int end = text.IndexOf('\n');
            return end < 0 ? text : text.Substring(0, end);
        }

        private static string ChapterOf(int missionNumber)
            => missionNumber >= 1 && missionNumber <= StoryMissions.Count ? StoryMissions.Get(missionNumber).Chapter
                : MissionBriefingHud.ChapterName;

        private static string BuildNotice(BattleResult result, bool toLobby)
        {
            if (result.IsMission)
                return !result.Victory
                    ? toLobby ? "임무에 실패했습니다.\n다시 도전하거나 로비로 돌아갈 수 있습니다."
                        : "임무에 실패했습니다.\n다시 도전하거나 브리핑으로 돌아갈 수 있습니다."
                    : result.StageNumber == PrologueMissions.Count ? "「깨어남」의 임무를 모두 마쳤습니다.\n이제 편성과 커리큘럼이 열립니다."
                    : result.StageNumber > PrologueMissions.Count ? toLobby ? "임무를 완료했습니다.\n여정을 계속하면 로비로 돌아갑니다."
                        : "임무를 완료했습니다.\n다음 임무로 넘어갈 수 있습니다."
                    : toLobby ? "「깨어남」은 이미 마쳤습니다.\n여정을 계속하면 로비로 돌아갑니다." : "다음 임무로 넘어갈 수 있습니다.";
            if (!result.Victory)
                return result.Outcome == DuelMatchOutcome.Draw ? "승부가 나지 않았습니다. 재도전하거나 기술 편성을 바꿔보세요."
                    : "로비에서 편성을 바꾸거나 다시 도전해보세요.";
            string clear = result.FirstClear ? "첫 클리어!" : "다시 클리어했습니다.";
            return result.UnlockedStageNumber > 0 ? clear + $"\n스테이지 {result.UnlockedStageNumber:00} 개방!"
                : clear + (result.CanAdvance ? "\n다음 스테이지에 도전할 수 있습니다." : "\n마지막 스테이지까지 마쳤습니다.");
        }

        /// <summary>Stage battles count toward the curriculum whatever the outcome; opening-arc missions do not.</summary>
        private void ShowCurriculum(BattleResult result)
        {
            CurriculumNode completed = result.CompletedCurriculumNode, active = result.ActiveCurriculumNode;
            if (result.IsMission)
            {
                bool prologue = result.StageNumber <= PrologueMissions.Count;
                curriculum.text = prologue ? "깨어남 이후" : "반영 안 됨";
                curriculum.color = Muted;
                curriculumDetail.text = prologue ? "「깨어남」의 임무는 커리큘럼에 반영되지 않습니다." : "임무는 커리큘럼에 반영되지 않습니다.";
                return;
            }
            if (completed != null)
            {
                curriculum.text = completed.Title + " 완료";
                curriculum.color = Gold;
                curriculumDetail.text = "새 기술  " + SkillNames(completed) + "\n편성에서 장착할 수 있습니다.";
            }
            else if (active != null)
            {
                curriculum.text = $"{active.Title}  {result.ActiveCurriculumBattles}/{active.Battles}";
                curriculum.color = Gold;
                curriculumDetail.text = "진행 중인 과정입니다.";
            }
            else if (result.CurriculumFinished)
            {
                curriculum.text = "모두 완료";
                curriculum.color = Muted;
                curriculumDetail.text = "더 고를 과정이 없습니다.\n초기화하면 다른 길을 고를 수 있습니다.";
            }
            else
            {
                curriculum.text = "진행 없음";
                curriculum.color = Muted;
                curriculumDetail.text = "로비의 커리큘럼에서 과정을 고르면\n전투마다 진행됩니다.";
            }
        }

        private static string SkillNames(CurriculumNode node)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (int id in node.SkillIds)
                foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills)
                    if (skill.Id == id) names.Add(skill.Name);
            return names.Count > 0 ? string.Join(", ", names) : "없음";
        }

        private Text Statistic(Transform parent, string name, string caption, float x)
        {
            var frame = Panel(name + " Panel", parent, new Vector2(x, 64f), new Vector2(232f, 110f), Raised);
            DuelVisualTheme.Frame(frame);
            Label(name + " Label", frame.transform, Vector2.up * 29f, new Vector2(216f, 30f), 18, Muted).text = caption;
            return Label(name, frame.transform, new Vector2(0f, -15f), new Vector2(216f, 52f), 36);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, new Vector2(224f, 58f), primary ? Accent : Raised);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label("Button Label", image.transform, Vector2.zero, new Vector2(208f, 48f), 22,
                primary ? DuelVisualTheme.Ink : Foreground).text = caption;
            button.onClick.AddListener(() => { ClearSelection(); action?.Invoke(); });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color? color = null)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = art.UIFont;
            text.fontSize = fontSize;
            text.color = color ?? Foreground;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = rect.anchoredPosition = Vector2.zero;
        }

        private static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
