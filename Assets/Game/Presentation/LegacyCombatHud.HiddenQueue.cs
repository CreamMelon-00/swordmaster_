using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        private static string HiddenQueuePropertyText(LegacySkill skill)
            => skill == null ? "미공개" : skill.IsWait ? "대기"
                : skill.Kind == LegacySkillKind.Defence ? "수비" : LegacySkillLabels.Property(skill.Property);

        // The same property papers as SkillInfoView's type ribbon. School, role and skill art never tint a hidden card.
        private static Color HiddenQueuePropertyInk(LegacySkill skill)
        {
            if (skill == null || skill.IsWait) return DuelVisualTheme.Muted;
            switch (skill.Property)
            {
                case LegacySkillProperty.Slash: return new Color32(220, 195, 173, 255);
                case LegacySkillProperty.Penetrate: return new Color32(198, 211, 217, 255);
                case LegacySkillProperty.Hit: return new Color32(223, 206, 163, 255);
                default: return new Color32(202, 208, 174, 255);
            }
        }

        private static Text BuildHiddenQueueProperty(RectTransform parent, Font font)
        {
            var label = Rect("Hidden Property", parent, Vector2.zero, new Vector2(58f, 28f), Vector2.one * .5f)
                .gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 18;
            label.gameObject.SetActive(false);
            return label;
        }

        private bool EnemyCardIsHidden(int slot)
            => displayedSession != null && !displayedSession.IsEnemySkillRevealed(slot);

        private LegacySkill EnemySkillForMask(int slot)
            => displayedSession != null && slot >= 0 && slot < displayedSession.EnemyQueue.Count
                ? displayedSession.EnemyQueue[slot] : null;

        private sealed partial class QueueView
        {
            private readonly List<Text> hiddenProperties = new List<Text>();
            private ILegacyDuelView visibilitySession;

            private void AddHiddenProperty(RectTransform item)
            {
                if (!player) hiddenProperties.Add(BuildHiddenQueueProperty(item, font));
            }

            private bool IdentityVisible(int slot)
                => !IsDestroyedGap(slot) && (player || visibilitySession == null || visibilitySession.IsEnemySkillRevealed(slot));

            private bool IsDestroyedGap(int slot)
            {
                if (visibilitySession?.UsePowerBreak != true) return false;
                if (player) return !visibilitySession.Player.IsDefeated && visibilitySession.IsPlayerSkillDestroyed(slot);
                if (!visibilitySession.IsEnemySkillDestroyed(slot)) return false;
                // Destruction remains in the turn's history after death. Its damage window belongs only to its
                // original living owner, never to the ally who later takes over that owner's empty time.
                if (visibilitySession is ILegacyEnemyRosterView roster && roster.Enemies.Count > 1)
                {
                    int owner = roster.EnemyOwnerAtSlot(slot);
                    return owner >= 0 && owner < roster.Enemies.Count && !roster.Enemies[owner].IsDefeated;
                }
                return !visibilitySession.Enemy.IsDefeated;
            }

            private void HideDestroyedCardMarks()
            {
                if (visibilitySession?.UsePowerBreak != true) return;
                for (int slot = 0; slot < icons.Count; slot++)
                {
                    if (!IsDestroyedGap(slot)) continue;
                    counterMarks[slot].SetActive(false);
                    cycleMarks[slot].gameObject.SetActive(false);
                    keywordMarks[slot].gameObject.SetActive(false);
                    feedback[slot].Clear();
                    if (slot < meshMarks.Count) meshMarks[slot].gameObject.SetActive(false);
                    if (slot < forecastMarks.Count) forecastMarks[slot].gameObject.SetActive(false);
                    if (slot < queuePowerSourceMarks.Count) queuePowerSourceMarks[slot].gameObject.SetActive(false);
                    if (slot < queuePowerTargetMarks.Count) queuePowerTargetMarks[slot].gameObject.SetActive(false);
                    if (slot < dodgeStrikes.Count) dodgeStrikes[slot].SetActive(false);
                }
            }

            private bool RefreshCardIdentity(int index, LegacySkill skill, Func<int, Sprite> iconFor)
            {
                bool revealed = IdentityVisible(index);
                // Do not retain a hidden skill's picture behind a tint: its silhouette would still identify it.
                icons[index].sprite = revealed ? iconFor(skill.IconId) : null;
                icons[index].enabled = revealed;
                frames[index].color = !revealed ? HiddenQueuePropertyInk(skill)
                    : schoolsNamed && !skill.IsWait ? SkillLaneStyle.Edge(skill.LaneIndex) : Border;
                if (!player)
                {
                    Text property = hiddenProperties[index];
                    if (property.gameObject.activeSelf == revealed) property.gameObject.SetActive(!revealed);
                    if (!revealed)
                    {
                        string text = HiddenQueuePropertyText(skill);
                        if (property.text != text) property.text = text;
                        property.color = HiddenQueuePropertyInk(skill);
                    }
                }
                return revealed;
            }
        }
    }
}
