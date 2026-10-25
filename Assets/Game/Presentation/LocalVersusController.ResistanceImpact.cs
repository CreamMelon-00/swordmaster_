using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    public sealed partial class LocalVersusController
    {
        private float resistanceImpactResumeTime;
        private int resistanceImpactPlayerBefore, resistanceImpactEnemyBefore;
        private bool resistanceImpactPlayerWasBroken, resistanceImpactEnemyWasBroken;

        private bool TryBeginResistanceImpact(LocalVersusPerspectiveSlot slot, int playerBefore, int enemyBefore,
            bool playerWasBroken, bool enemyWasBroken)
        {
            if (!arena.BeginResistanceImpact(slot.PlayerFeedback?.OpponentResistanceReduced > 0,
                slot.EnemyFeedback?.OpponentResistanceReduced > 0)) return false;
            resistanceImpactResumeTime = phaseTime;
            resistanceImpactPlayerBefore = playerBefore;
            resistanceImpactEnemyBefore = enemyBefore;
            resistanceImpactPlayerWasBroken = playerWasBroken;
            resistanceImpactEnemyWasBroken = enemyWasBroken;
            SetViewPhase(ViewPhase.ResistanceImpact);
            return true;
        }

        private void PresentSlotStartFeedback(LocalVersusPerspectiveSlot slot, int playerBefore, int enemyBefore,
            bool playerWasBroken, bool enemyWasBroken)
        {
            battleHud.SetSkillFeedback(slot);
            activationCue.Show(true, slot.PlayerSkill, slot.PlayerFeedback, KeywordCue(view.PlayerQueue, slot.SlotIndex,
                view.ForPlayer(slot.Source.LeftPlanningSecondsGained, slot.Source.RightPlanningSecondsGained), view.UsePowerBreak));
            activationCue.Show(false, slot.EnemySkill, slot.EnemyFeedback, KeywordCue(view.EnemyQueue, slot.SlotIndex,
                view.ForEnemy(slot.Source.LeftPlanningSecondsGained, slot.Source.RightPlanningSecondsGained), view.UsePowerBreak));
            resistanceFeedback.Show(true, view.Player.Resistance - playerBefore);
            resistanceFeedback.Show(false, view.Enemy.Resistance - enemyBefore);
            AnnounceBreaks(playerWasBroken, enemyWasBroken);
        }

        private void AdvanceResistanceImpactPresentation(float realDelta)
        {
            arena.SetHudFloor(battleHud.QueuePanelFloor);
            arena.Tick(0f, realDelta, deferSlowAfterimages: true);
            if (arena.AdvanceResistanceImpact(realDelta))
            {
                effects.pitch = .8f;
                art.PlayClash(effects, false, false, .45f);
                PresentSlotStartFeedback(view.CurrentSlot, resistanceImpactPlayerBefore, resistanceImpactEnemyBefore,
                    resistanceImpactPlayerWasBroken, resistanceImpactEnemyWasBroken);
            }
            if (!arena.IsResistanceImpactPlaying)
            {
                SetViewPhase(ViewPhase.PlayingSlot);
                phaseTime = resistanceImpactResumeTime;
            }
            RefreshHud(0f, realDelta);
        }
    }
}
