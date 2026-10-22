using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPrototypeController
    {
        private ViewPhase resistanceImpactResumePhase;
        private float resistanceImpactResumeTime;
        private int resistanceImpactPlayerBefore, resistanceImpactEnemyBefore;
        private bool resistanceImpactPlayerWasBroken, resistanceImpactEnemyWasBroken, resistanceImpactLocksStep;

        private bool TryBeginResistanceImpact(LegacyCurrentSlot slot, int playerBefore, int enemyBefore,
            bool playerWasBroken, bool enemyWasBroken)
        {
            if (!arena.BeginResistanceImpact(slot.PlayerFeedback?.OpponentResistanceReduced > 0,
                slot.EnemyFeedback?.OpponentResistanceReduced > 0)) return false;
            resistanceImpactResumePhase = viewPhase;
            resistanceImpactResumeTime = phaseTime;
            resistanceImpactPlayerBefore = playerBefore;
            resistanceImpactEnemyBefore = enemyBefore;
            resistanceImpactPlayerWasBroken = playerWasBroken;
            resistanceImpactEnemyWasBroken = enemyWasBroken;
            stepHud.Refresh(false, slot, 0f, false, 0f, session.UsedStepThisTurn);
            SetViewPhase(ViewPhase.ResistanceImpact);
            return true;
        }

        private void PresentSlotStartFeedback(LegacyCurrentSlot slot, int playerBefore, int enemyBefore,
            bool playerWasBroken, bool enemyWasBroken)
        {
            hud.SetSkillFeedback(slot);
            if (!slotStartDeferred) ShowSkillActivationCues(slot);
            if (slot.EnemyCountered) hud.ShowCounterCallout(false, arena.EnemyRenderer.transform.position);
            if (slot.PlayerCountered) hud.ShowCounterCallout(true, arena.PlayerRenderer.transform.position);
            resistanceFeedback.Show(true, session.Player.Resistance - playerBefore);
            resistanceFeedback.Show(false, session.Enemy.Resistance - enemyBefore);
            AnnounceBreaks(playerWasBroken, enemyWasBroken);
            barks.Hit(Blow(session.Enemy, enemyWasBroken, 0), Blow(session.Player, playerWasBroken, 0));
        }

        // A counter's initial effects wait for the final step decision. Commit only those effects here; the real hit
        // still resolves once, after any body check, with the same power rolls and damage rules.
        private bool TryPrepareDeferredResistanceImpact(LegacyCurrentSlot slot)
        {
            if (!slotStartDeferred) return false;
            int playerBefore = session.Player.Resistance, enemyBefore = session.Enemy.Resistance;
            bool playerWasBroken = session.Player.IsResistanceBroken, enemyWasBroken = session.Enemy.IsResistanceBroken;
            if (!session.PrepareCurrentSlotImpact()) return false;
            slotStartDeferred = false;
            if (slot.PlayerFeedback?.OpponentResistanceReduced > 0 || slot.EnemyFeedback?.OpponentResistanceReduced > 0)
            {
                phaseTime = slotImpactTime;
                arena.HoldSlotAtTime(phaseTime);
                if (TryBeginResistanceImpact(slot, playerBefore, enemyBefore, playerWasBroken, enemyWasBroken))
                {
                    resistanceImpactLocksStep = true;
                    bladeExchange.Clear();
                    return true;
                }
            }
            PresentSlotStartFeedback(slot, playerBefore, enemyBefore, playerWasBroken, enemyWasBroken);
            return false;
        }

        private void AdvanceResistanceImpactPresentation(float checkDelta, float realDelta)
        {
            arena.SetHudFloor(hud.QueuePanelFloor);
            arena.Tick(0f, realDelta, deferSlowAfterimages: true);
            if (arena.AdvanceResistanceImpact(checkDelta))
            {
                effectsSource.pitch = .8f;
                art.PlayClash(effectsSource, false, false, .45f);
                PresentSlotStartFeedback(session.CurrentSlot, resistanceImpactPlayerBefore, resistanceImpactEnemyBefore,
                    resistanceImpactPlayerWasBroken, resistanceImpactEnemyWasBroken);
            }
            if (!arena.IsResistanceImpactPlaying)
            {
                SetViewPhase(resistanceImpactResumePhase);
                phaseTime = resistanceImpactResumeTime;
            }
            // This frame belongs to the check even when it crosses its end: never batch a sword hit into its contact.
            RefreshHudClock(0f, realDelta);
        }
    }
}
