using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        private const float ComparisonCardWidth = 460f;
        private const float ComparisonTopInset = 96f;
        private const float ComparisonCenterGap = 96f;
        private RectTransform comparisonPlayerExplanation, comparisonForecastSpace;
        private SkillInfoView comparisonPlayerInfo;
        private SkillLaneBadge comparisonPlayerStyle;
        private Image comparisonPlayerIcon;
        private Text comparisonPlayerName, comparisonPlayerHint, comparisonEnemyCaption, comparisonPlayerCaption;
        private bool queueComparisonVisible;

        /// <summary>The enemy's existing explanation card; in queue comparison it stands at the right edge.</summary>
        public RectTransform ExplanationRoot => enemyExplanation;
        /// <summary>The reserved player's standard-size card at the left; inactive for an empty matching slot.</summary>
        public RectTransform ComparisonPlayerRoot => comparisonPlayerExplanation;
        public bool IsQueueComparisonVisible => !disposed && queueComparisonVisible && enemyExplanation.gameObject.activeSelf;

        private void BuildQueueComparison()
        {
            comparisonEnemyCaption = ComparisonCaption(enemyExplanation, "상대 기술");
            comparisonEnemyCaption.gameObject.SetActive(false);
            var paper = Panel("Queue Player Skill Explain", root, Vector2.zero,
                new Vector2(ComparisonCardWidth, 332f), DuelVisualTheme.Paper);
            Dress(paper);
            comparisonPlayerExplanation = paper.rectTransform;
            comparisonPlayerCaption = ComparisonCaption(comparisonPlayerExplanation, "대응 기술");
            comparisonPlayerIcon = Image("Explanation Skill Icon", comparisonPlayerExplanation, null,
                Vector2.zero, Vector2.one * 64f);
            comparisonPlayerIcon.preserveAspect = true;
            comparisonPlayerName = Text("Name", comparisonPlayerExplanation, Vector2.zero,
                new Vector2(244f, 32f), 26, TextAnchor.MiddleLeft);
            comparisonPlayerName.color = DuelVisualTheme.Ink;
            FitExplanationLabel(comparisonPlayerName, 16);
            comparisonPlayerStyle = new SkillLaneBadge(comparisonPlayerExplanation, art.UIFont, "Power Cost Property", Vector2.zero);
            comparisonPlayerInfo = new SkillInfoView(comparisonPlayerExplanation, art.UIFont,
                Vector2.zero, 416f, "Comparison Player");
            comparisonPlayerInfo.EffectText.name = "Effect";
            comparisonPlayerHint = Text("Explanation Hint", comparisonPlayerExplanation, Vector2.zero,
                new Vector2(416f, 20f), 14, TextAnchor.MiddleRight);
            comparisonPlayerHint.color = DuelVisualTheme.Ink;
            FitExplanationLabel(comparisonPlayerHint, 11);
            // The indexed forecast is already on the enemy card. Reserve the same row on the player card so both align,
            // without repeating it or presenting a forecast for a new reservation as though it described this slot.
            comparisonForecastSpace = Rect("Comparison Forecast Space", comparisonPlayerExplanation, Vector2.zero,
                new Vector2(416f, ForecastNoteRow), Vector2.one * .5f);
            comparisonForecastSpace.gameObject.SetActive(false);
            comparisonPlayerExplanation.gameObject.SetActive(false);
        }

        private Text ComparisonCaption(RectTransform parent, string caption)
        {
            Text label = Text("Comparison Caption", parent, Vector2.zero, new Vector2(416f, 16f), 12, TextAnchor.MiddleRight);
            label.text = caption;
            label.color = DuelVisualTheme.Ink;
            return label;
        }

        /// <summary>Compares the inspected enemy slot with the skill already reserved in that same slot. Unlike a held
        /// lane's explanation, the player card never previews a new reservation, its mesh or its next-slot conditions.</summary>
        public void ShowQueueComparison(LegacySkill enemy, LegacySkill player, CampaignOwnedSkill owned = null)
        {
            if (disposed) return;
            if (enemy == null) { HideExplanation(); return; }
            queueComparisonVisible = true;
            playerExplanation.gameObject.SetActive(false);
            ClearConditionPreview();
            playerEffectFeedback.Clear();
            enemyEffectFeedback.Clear();

            explainedEnemy = enemy;
            enemyName.text = enemy.Name;
            bool named = displayedSession == null || displayedSession.Features.LaneCount() == 3;
            if (enemy.IsWait) enemyStyle.Clear("공용 행동");
            else if (named) enemyStyle.SetLane(enemy.LaneIndex, false);
            else enemyStyle.Clear(EnemySkillCaption);
            enemyExplanationIcon.sprite = enemy.IsWait ? null : iconFor(enemy.IconId);
            enemyExplanationIcon.enabled = enemyExplanationIcon.sprite != null;
            if (enemy.IsWait) enemyInfo.SetEmptyMessage(enemy.Description);
            else SetEnemyInfoSkill(enemy);
            UpdateEnemyForecastNote();
            enemyExplanationHint.text = ComparisonSlotHint;
            enemyExplanation.gameObject.SetActive(true);
            comparisonEnemyCaption.gameObject.SetActive(true);
            LayoutExplanation(enemyExplanation, enemyExplanationIcon, enemyName, enemyStyle, enemyInfo, enemyExplanationHint,
                1f, forecast: enemyForecastNote);

            bool hasPlayer = player != null;
            comparisonPlayerExplanation.gameObject.SetActive(hasPlayer);
            if (hasPlayer)
            {
                comparisonPlayerName.text = player.Name;
                if (player.IsWait) comparisonPlayerStyle.Clear("공용 행동");
                else comparisonPlayerStyle.SetLane(player.LaneIndex);
                comparisonPlayerIcon.sprite = player.IsWait ? null : iconFor(player.IconId);
                comparisonPlayerIcon.enabled = comparisonPlayerIcon.sprite != null;
                if (player.IsWait) comparisonPlayerInfo.SetEmptyMessage(player.Description);
                else SetPlayerInfoSkill(comparisonPlayerInfo, player, ReferenceEquals(owned?.Skill, player) ? owned : null);
                comparisonPlayerHint.text = ComparisonSlotHint;
                comparisonForecastSpace.gameObject.SetActive(enemyForecastNote.gameObject.activeSelf);
                LayoutExplanation(comparisonPlayerExplanation, comparisonPlayerIcon, comparisonPlayerName,
                    comparisonPlayerStyle, comparisonPlayerInfo, comparisonPlayerHint, 1f, forecast: comparisonForecastSpace);
            }
            else
            {
                comparisonPlayerName.text = string.Empty;
                comparisonPlayerInfo.Clear();
            }
            PositionQueueComparison(hasPlayer);
        }

        private string ComparisonSlotHint => inspectedSlot >= 0
            ? (inspectedSlot + 1) + "번째 칸 · Tab을 놓으면 닫기" : "Tab을 놓으면 닫기";

        private void PositionQueueComparison(bool hasPlayer)
        {
            Vector2 screen = root.rect.size;
            float height = enemyExplanation.sizeDelta.y;
            float floor = Mathf.Max(Mathf.Max(DockTop, queueRowTop),
                ScreenPosition(new Vector3(0f, explanationFloor, 0f)).y) + ExplanationGap;
            float availableHeight = Mathf.Max(1f, screen.y - ComparisonTopInset - floor);
            float availableWidth = Mathf.Max(1f, screen.x - 2f * ScreenMargin - ComparisonCenterGap);
            float scale = Mathf.Min(1f, availableHeight / height,
                availableWidth / (ComparisonCardWidth * (hasPlayer ? 2f : 1f)));
            Vector2 size = enemyExplanation.sizeDelta * scale;
            // Read both cards around the screen's middle, above the queue or coach when space is tight.
            float y = Mathf.Clamp(screen.y * .5f, floor + size.y / 2f,
                screen.y - ComparisonTopInset - size.y / 2f);
            enemyExplanation.localScale = Vector3.one * scale;
            enemyExplanation.anchoredPosition = ClampCenter(new Vector2(screen.x - ScreenMargin - size.x / 2f, y), size) - screen / 2f;
            PlaceComparisonCaption(comparisonEnemyCaption, height);
            if (!hasPlayer) return;
            comparisonPlayerExplanation.localScale = Vector3.one * scale;
            comparisonPlayerExplanation.anchoredPosition = ClampCenter(new Vector2(ScreenMargin + size.x / 2f, y), size) - screen / 2f;
            PlaceComparisonCaption(comparisonPlayerCaption, comparisonPlayerExplanation.sizeDelta.y);
        }

        private static void PlaceComparisonCaption(Text caption, float height)
            => caption.rectTransform.anchoredPosition = new Vector2(0f, height / 2f - 11f);

        private void HideQueueComparison()
        {
            if (!queueComparisonVisible) return;
            queueComparisonVisible = false;
            comparisonPlayerExplanation.gameObject.SetActive(false);
            comparisonEnemyCaption.gameObject.SetActive(false);
            enemyExplanation.localScale = Vector3.one;
            enemyExplanationIcon.enabled = true;
            bool named = displayedSession == null || displayedSession.Features.LaneCount() == 3;
            enemyExplanationHint.text = named ? EnemyHint : NeutralEnemyHint;
        }
    }
}
