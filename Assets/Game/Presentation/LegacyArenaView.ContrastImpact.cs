using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private DuelContrastImpact contrastImpact;
        private bool battleEndImpactShown;

        public bool ContrastImpactActive => contrastImpact != null && contrastImpact.Active;

        /// <summary>One contrast frame when a battle is decided, for either winner or a draw. No skill or pose setting selects it.</summary>
        public bool PresentBattleEndImpact(DuelMatchOutcome outcome)
        {
            if (disposed || suspension != null || battleEndImpactShown ||
                (outcome != DuelMatchOutcome.PlayerVictory && outcome != DuelMatchOutcome.EnemyVictory &&
                 outcome != DuelMatchOutcome.Draw) || !contrastImpact.Show()) return false;
            battleEndImpactShown = true;
            RefreshContrastImpact();
            RefreshContrastGrade();
            return true;
        }

        /// <summary>Advance before the controller's finale/result early returns, independently of the pose clock.</summary>
        public void AdvanceContrastImpact(float realDelta)
        {
            if (disposed || contrastImpact == null) return;
            bool wasActive = ContrastImpactActive;
            contrastImpact.Advance(realDelta);
            RefreshContrastImpact();
            if (wasActive != ContrastImpactActive) RefreshContrastGrade();
        }

        public void ClearContrastImpact()
        {
            if (disposed || contrastImpact == null || !contrastImpact.Active) return;
            contrastImpact.Clear();
            RefreshContrastGrade();
        }

        private void RefreshContrastImpact()
        {
            if (!ContrastImpactActive) return;
            contrastImpact.Refresh(ArenaCamera, player.Renderer, player.LowerRenderer,
                enemy.Renderer, enemy.LowerRenderer, PlayerDepthSortingOrder > EnemyDepthSortingOrder);
        }

        private void RefreshContrastGrade()
        {
            // Reapply the owned volume immediately: a result/frozen frame may never call Arena.Tick again.
            RefreshExposure();
            RefreshBloom();
            ApplyGrade();
        }
    }
}
