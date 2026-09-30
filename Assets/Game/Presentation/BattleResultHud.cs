using System;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
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
        private readonly Text heading, stage, rounds, playerHealth, enemyHealth, reward, currency, notice;
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
            var rewards = Panel("Result Reward Panel", card.transform, new Vector2(0f, -61f),
                new Vector2(736f, 104f), Raised);
            DuelVisualTheme.Frame(rewards);
            Label("Result Reward Heading", rewards.transform, new Vector2(-210f, 27f), new Vector2(300f, 28f), 18, Muted).text = "획득 재화";
            reward = Label("Result Reward", rewards.transform, new Vector2(-210f, -14f), new Vector2(300f, 48f), 36, Gold);
            currency = Label("Result Currency", rewards.transform, new Vector2(170f, 0f), new Vector2(320f, 64f), 22);
            notice = Label("Result Notice", card.transform, new Vector2(0f, -161f), new Vector2(736f, 78f), 20, Muted);
            lobbyButton = ActionButton("Result Lobby", card.transform, "로비로", new Vector2(-248f, -255f), returnToLobby);
            retryButton = ActionButton("Result Retry", card.transform, "재도전", new Vector2(0f, -255f), retry);
            retryCaption = retryButton.GetComponentInChildren<Text>();
            lobbyCaption = lobbyButton.GetComponentInChildren<Text>();
            nextButton = ActionButton("Result Next Stage", card.transform, "다음 스테이지", new Vector2(248f, -255f), nextStage, true);
            nextCaption = nextButton.GetComponentInChildren<Text>();
            Hide();
        }

        /// <param name="arcComplete">Whether the opening arc is already over, so a mission's exits reach the lobby.</param>
        public void Show(BattleResult result, bool arcComplete = false)
        {
            if (disposed) return;
            if (result == null) throw new ArgumentNullException(nameof(result));
            root.gameObject.SetActive(true);
            heading.text = result.IsMission
                ? result.Victory ? "임무 완료" : "임무 실패"
                : result.Outcome == DuelMatchOutcome.PlayerVictory ? "승리"
                : result.Outcome == DuelMatchOutcome.Draw ? "무승부" : "패배";
            heading.color = result.Victory ? Accent : result.Outcome == DuelMatchOutcome.Draw ? Foreground : DuelVisualTheme.Danger;
            stage.text = result.IsMission ? $"{MissionBriefingHud.ChapterName}  ·  임무 {result.StageNumber:00}  ·  {result.StageName}"
                : $"스테이지 {result.StageNumber:00}  ·  {result.StageName}";
            rounds.text = result.RoundNumber.ToString();
            playerHealth.text = Mathf.Max(0, result.PlayerHealth).ToString();
            enemyHealth.text = Mathf.Max(0, result.EnemyHealth).ToString();
            reward.text = "+ " + Mathf.Max(0, result.Reward);
            currency.text = "보유 재화\n" + Mathf.Max(0, result.Currency);
            // A mission's exits open the next briefing, or the lobby once the arc is over.
            bool toLobby = result.IsMission && (arcComplete || result.Victory && result.StageNumber >= PrologueMissions.Count);
            notice.text = BuildNotice(result, toLobby);
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

        private static string BuildNotice(BattleResult result, bool toLobby)
        {
            if (result.IsMission)
                return !result.Victory
                    ? toLobby ? "임무에 실패했습니다.\n다시 도전하거나 로비로 돌아갈 수 있습니다."
                        : "임무에 실패했습니다.\n다시 도전하거나 브리핑으로 돌아갈 수 있습니다."
                    : result.StageNumber >= PrologueMissions.Count ? "서막의 임무를 모두 마쳤습니다.\n이제 편성과 상점이 열립니다."
                    : toLobby ? "서막은 이미 마쳤습니다.\n여정을 계속하면 로비로 돌아갑니다." : "다음 임무로 넘어갈 수 있습니다.";
            if (!result.Victory)
                return result.Outcome == DuelMatchOutcome.Draw ? "승부가 나지 않았습니다. 재도전하거나 기술 편성을 바꿔보세요."
                    : "이번 전투의 보상은 없습니다.\n로비에서 편성을 바꾸거나 다시 도전해보세요.";
            string clear = result.FirstClear ? "첫 클리어 보상 획득" : "재클리어 보상 획득 · 기본 보상의 절반";
            return result.UnlockedStageNumber > 0 ? clear + $"\n스테이지 {result.UnlockedStageNumber:00} 개방!"
                : clear + (result.CanAdvance ? "\n다음 스테이지에 도전할 수 있습니다." : "\n보상은 보유 재화에 반영되었습니다.");
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
