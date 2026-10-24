using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        private const string HiddenEnemyInformation = "기술 사용 시 공개됩니다.";
        private bool enemyInformationMasked;

        private bool IsEnemyInformationHidden(LegacySkill skill)
        {
            if (displayedSession == null || skill == null) return false;
            if (inspectedSlot >= 0 && inspectedSlot < displayedSession.EnemyQueue.Count)
                return !displayedSession.IsEnemySkillRevealed(inspectedSlot);
            var current = displayedSession.CurrentSlot;
            if (current != null && ReferenceEquals(current.EnemySkill, skill))
                return !displayedSession.IsEnemySkillRevealed(UsesTempoQueue(displayedSession) && current is LegacyCurrentSlot timed
                    ? timed.EnemyQueueIndex : current.SlotIndex);
            // Without an inspected index, a repeated object could refer to an unrevealed reservation.
            for (int index = 0; index < displayedSession.EnemyQueue.Count; index++)
                if (ReferenceEquals(displayedSession.EnemyQueue[index], skill) && !displayedSession.IsEnemySkillRevealed(index))
                    return true;
            return false;
        }

        private bool MaskEnemyInformation(LegacySkill skill)
        {
            if (!IsEnemyInformationHidden(skill)) return false;
            enemyInformationMasked = true;
            enemyName.text = HiddenQueuePropertyText(skill);
            enemyStyle.Clear("미공개 기술");
            enemyExplanationIcon.sprite = null;
            enemyExplanationIcon.enabled = false;
            enemyInfo.SetEmptyMessage(HiddenEnemyInformation);
            enemyForecastNote.gameObject.SetActive(false);
            return true;
        }

        private void RestoreEnemyInformation(LegacySkill skill)
        {
            if (!enemyInformationMasked) return;
            enemyInformationMasked = false;
            enemyName.text = skill.Name;
            if (skill.IsWait) enemyStyle.Clear("공용 행동");
            else if (displayedSession == null || displayedSession.Features.LaneCount() == 3)
                enemyStyle.SetLane(skill.LaneIndex, false);
            else enemyStyle.Clear(EnemySkillCaption);
            enemyExplanationIcon.sprite = skill.IsWait ? null : iconFor(skill.IconId);
            enemyExplanationIcon.enabled = enemyExplanationIcon.sprite != null;
        }

        private bool HasHiddenEnemyInformation()
        {
            if (displayedSession == null) return false;
            for (int index = 0; index < displayedSession.EnemyQueue.Count; index++)
                if (!displayedSession.IsEnemySkillRevealed(index)) return true;
            return false;
        }
    }
}
