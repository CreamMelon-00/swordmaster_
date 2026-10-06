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
        // The curriculum row is its panel and the gap under it. While the curriculum is closed the panel goes and the
        // card closes up around it, half from above and half from below.
        private const float CurriculumRowHeight = 114f, NoticeY = -161f, ActionY = -255f;
        private readonly RectTransform cardBorder, cardSurface, curriculumPanel;
        private readonly RectTransform[] upperRows;
        private readonly float[] upperRowY;
        private float actionY = ActionY;
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
            canvas.pixelPerfect = true;
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
            heading = Label("Result Heading", card.transform, Vector2.up * 238f, new Vector2(760f, 88f), 70);
            stage = Label("Result Stage", card.transform, Vector2.up * 179f, new Vector2(760f, 42f), 23, Muted);
            var rule = Panel("Result Rule", card.transform, Vector2.up * 144f, new Vector2(736f, 2f), Border);
            rounds = Statistic(card.transform, "Result Rounds", "진행 턴", -248f);
            playerHealth = Statistic(card.transform, "Result Player HP", "내 남은 HP", 0f);
            enemyHealth = Statistic(card.transform, "Result Enemy HP", "상대 남은 HP", 248f);
            // Treat the result numbers as one score line. Separate boxes gave every number the same dashboard weight.
            var leftDivider = Panel("Result Stat Divider Left", card.transform, new Vector2(-124f, 64f),
                new Vector2(2f, 68f), Border);
            var rightDivider = Panel("Result Stat Divider Right", card.transform, new Vector2(124f, 64f),
                new Vector2(2f, 68f), Border);
            cardBorder = border.rectTransform;
            cardSurface = card.rectTransform;
            upperRows = new[]
            {
                heading.rectTransform, stage.rectTransform, rule.rectTransform, (RectTransform)rounds.transform.parent,
                (RectTransform)playerHealth.transform.parent, (RectTransform)enemyHealth.transform.parent,
                leftDivider.rectTransform, rightDivider.rectTransform,
            };
            upperRowY = Array.ConvertAll(upperRows, row => row.anchoredPosition.y);
            // Currency is hidden while it has no use; this panel reports the curriculum instead.
            var progress = Panel("Result Curriculum Panel", card.transform, new Vector2(0f, -61f),
                new Vector2(736f, 104f), Raised);
            curriculumPanel = progress.rectTransform;
            Panel("Result Curriculum Accent", progress.transform, new Vector2(-366f, 0f),
                new Vector2(4f, 104f), Accent);
            Label("Result Curriculum Heading", progress.transform, new Vector2(-210f, 27f), new Vector2(300f, 28f), 18, Muted).text = "커리큘럼";
            curriculum = Label("Result Curriculum", progress.transform, new Vector2(-210f, -14f), new Vector2(300f, 48f), 30, Gold);
            curriculum.resizeTextForBestFit = true;
            curriculum.resizeTextMinSize = 20;
            curriculum.resizeTextMaxSize = 30;
            curriculumDetail = Label("Result Curriculum Detail", progress.transform, new Vector2(170f, 0f), new Vector2(360f, 72f), 19);
            curriculumDetail.resizeTextForBestFit = true;
            curriculumDetail.resizeTextMinSize = 14;
            curriculumDetail.resizeTextMaxSize = 19;
            notice = Label("Result Notice", card.transform, new Vector2(0f, NoticeY), new Vector2(736f, 78f), 20, Muted);
            lobbyButton = ActionButton("Result Lobby", card.transform, "로비로", new Vector2(-262f, ActionY), returnToLobby);
            retryButton = ActionButton("Result Retry", card.transform, "재도전", new Vector2(-43f, ActionY), retry);
            retryCaption = retryButton.GetComponentInChildren<Text>();
            lobbyCaption = lobbyButton.GetComponentInChildren<Text>();
            nextButton = ActionButton("Result Next Stage", card.transform, "다음 스테이지", new Vector2(231f, ActionY), nextStage, true);
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
            LayoutCurriculumRow(result.CurriculumOpen);
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
            // A single forward action leads the eye. On defeat, retry takes that place.
            SetActionProminence(nextButton, result.CanAdvance);
            SetActionProminence(retryButton, !result.CanAdvance && result.CanRetry);
            lobbyButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-262f, actionY);
            retryButton.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(!showLobby ? -165f : result.CanAdvance ? -43f : 231f, actionY);
            nextButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(231f, actionY);
            ClearSelection();
        }

        /// <summary>The dummy session reports damage and the next target without suggesting campaign rewards.</summary>
        public void ShowTraining(BattleResult result, int targetHealth, int nextTargetHealth)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!result.IsTraining) throw new ArgumentException("A training result is required.", nameof(result));
            if (targetHealth < 1) throw new ArgumentOutOfRangeException(nameof(targetHealth));
            if (nextTargetHealth < 1) throw new ArgumentOutOfRangeException(nameof(nextTargetHealth));
            Show(result);
            heading.text = result.Victory ? "수련 성공" : "수련 종료";
            heading.color = result.Victory ? Accent : Foreground;
            stage.text = "허수아비 수련";
            rounds.text = Mathf.Min(result.RoundNumber, 5) + " / 5";
            enemyHealth.text = Mathf.Max(0, result.EnemyHealth) + " / " + targetHealth;
            enemyHealth.resizeTextForBestFit = true;
            enemyHealth.resizeTextMinSize = 18;
            enemyHealth.resizeTextMaxSize = 40;
            int damage = Mathf.Max(0, targetHealth - result.EnemyHealth);
            notice.text = result.Victory
                ? $"가한 피해 {damage}\n다음 허수아비 체력 {nextTargetHealth}"
                : $"5턴 동안 가한 피해 {damage}";
            retryCaption.text = result.Victory ? "다음 수련" : "재도전";
            lobbyCaption.text = "로비로";
            lobbyButton.gameObject.SetActive(true);
            retryButton.interactable = true;
            nextButton.gameObject.SetActive(false);
            SetActionProminence(lobbyButton, false);
            SetActionProminence(retryButton, true);
            lobbyButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-153f, actionY);
            retryButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(139f, actionY);
        }

        /// <summary>Shows the curriculum row, or closes the card up without it while the curriculum is closed.</summary>
        private void LayoutCurriculumRow(bool shown)
        {
            float closeUp = shown ? 0f : CurriculumRowHeight * .5f;
            curriculumPanel.gameObject.SetActive(shown);
            cardBorder.sizeDelta = new Vector2(844f, 654f - 2f * closeUp);
            cardSurface.sizeDelta = new Vector2(840f, 650f - 2f * closeUp);
            for (int row = 0; row < upperRows.Length; row++)
                upperRows[row].anchoredPosition = new Vector2(upperRows[row].anchoredPosition.x, upperRowY[row] - closeUp);
            notice.rectTransform.anchoredPosition = new Vector2(0f, NoticeY + closeUp);
            actionY = ActionY + closeUp;
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

        /// <summary>The 서막 is over and what it opens, in two lines: a win of its last mission says it here, and since
        /// that mission ends in a forced loss with no result, the lobby it leads to says it the first time
        /// (<see cref="CampaignLobbyHud.SetArrivalNotice"/>).</summary>
        public static string PrologueCompleteNotice(bool curriculumOpen) => curriculumOpen
            ? "「깨어남」의 임무를 모두 마쳤습니다.\n이제 편성과 커리큘럼이 열립니다."
            : "「깨어남」의 임무를 모두 마쳤습니다.\n이제 편성과 스테이지가 열립니다.";

        private static string BuildNotice(BattleResult result, bool toLobby)
        {
            if (result.IsMission)
                return !result.Victory
                    ? toLobby ? "임무에 실패했습니다.\n다시 도전하거나 로비로 돌아갈 수 있습니다."
                        : "임무에 실패했습니다.\n다시 도전하거나 브리핑으로 돌아갈 수 있습니다."
                    : result.StageNumber == PrologueMissions.Count ? PrologueCompleteNotice(result.CurriculumOpen)
                    : result.StageNumber > PrologueMissions.Count ? toLobby ? "임무를 완료했습니다.\n여정을 계속하면 로비로 돌아갑니다."
                        : "임무를 완료했습니다.\n다음 임무로 넘어갈 수 있습니다."
                    : toLobby ? "「깨어남」은 이미 마쳤습니다.\n여정을 계속하면 로비로 돌아갑니다." : "다음 임무로 넘어갈 수 있습니다.";
            if (!result.Victory)
                return result.Outcome == DuelMatchOutcome.Draw ? "승부가 나지 않았습니다. 재도전하거나 기술 편성을 바꿔보세요."
                    : "로비에서 편성을 바꾸거나 다시 도전해보세요.";
            string clear = result.FirstClear
                ? string.IsNullOrEmpty(result.FirstClearSkillName) ? "첫 클리어!"
                    : $"첫 클리어! 새 기술 '{result.FirstClearSkillName}' 획득"
                : "다시 클리어했습니다.";
            return result.UnlockedStageNumber > 0 ? clear + $"\n스테이지 {result.UnlockedStageNumber:00} 개방!"
                : clear + (result.CanAdvance ? "\n다음 스테이지에 도전할 수 있습니다." : "\n마지막 스테이지까지 마쳤습니다.");
        }

        /// <summary>Stage battles count toward the curriculum whatever the outcome; opening-arc missions do not.</summary>
        private void ShowCurriculum(BattleResult result)
        {
            CurriculumNode completed = result.CompletedCurriculumNode, active = result.ActiveCurriculumNode;
            if (!result.CurriculumOpen)
            {
                // The row is hidden (LayoutCurriculumRow); nothing in it is left to name the curriculum either.
                curriculum.text = curriculumDetail.text = string.Empty;
                return;
            }
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
                string stats = StatRewardText(completed.StatReward);
                curriculumDetail.text = completed.SkillIds.Count == 0
                    ? "능력치 증가\n" + stats
                    : completed.StatReward.IsEmpty ? "새 기술  " + SkillNames(completed) + "\n편성에서 장착할 수 있습니다."
                        : "새 기술  " + SkillNames(completed) + "\n" + stats;
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
            {
                LegacySkill skill = LegacySkillDefinitions.Find(id)?.Skill;
                if (skill != null) names.Add(skill.Name);
            }
            return names.Count > 0 ? string.Join(", ", names) : "없음";
        }

        private static string StatRewardText(CurriculumStatReward reward)
        {
            var parts = new System.Collections.Generic.List<string>(5);
            if (reward.Health > 0) parts.Add("최대 체력 +" + reward.Health);
            if (reward.Resistance > 0) parts.Add("최대 저항 +" + reward.Resistance);
            if (reward.ActGain > 0) parts.Add("매 턴 ACT 회복 +" + reward.ActGain);
            if (reward.ActCapacity > 0) parts.Add("ACT 상한 +" + reward.ActCapacity);
            if (reward.PlanningSeconds > 0) parts.Add("선택 시간 +" + reward.PlanningSeconds + "초");
            return string.Join(" · ", parts);
        }

        private Text Statistic(Transform parent, string name, string caption, float x)
        {
            var frame = Panel(name + " Panel", parent, new Vector2(x, 64f), new Vector2(232f, 110f), Color.clear);
            Label(name + " Label", frame.transform, Vector2.up * 29f, new Vector2(216f, 30f), 18, Muted).text = caption;
            return Label(name, frame.transform, new Vector2(0f, -15f), new Vector2(216f, 52f), 40);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, primary ? new Vector2(272f, 66f) : new Vector2(194f, 50f),
                primary ? Accent : Raised);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label("Button Label", image.transform, Vector2.zero, new Vector2(180f, 46f), 22,
                primary ? DuelVisualTheme.Ink : Foreground).text = caption;
            button.onClick.AddListener(() => { ClearSelection(); action?.Invoke(); });
            return button;
        }

        private static void SetActionProminence(Button button, bool primary)
        {
            button.GetComponent<RectTransform>().sizeDelta = primary ? new Vector2(272f, 66f) : new Vector2(194f, 50f);
            button.GetComponent<Image>().color = primary ? Accent : Raised;
            button.GetComponentInChildren<Text>(true).color = primary ? DuelVisualTheme.Ink : Foreground;
            DuelVisualTheme.Frame(button.targetGraphic as Image, primary);
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
