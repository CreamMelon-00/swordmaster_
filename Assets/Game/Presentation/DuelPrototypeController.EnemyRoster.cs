using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPrototypeController
    {
        private static readonly EnemyAppearance[] StageFiveAppearances =
            { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student };
        private readonly int[] enemyResistanceBeforeTurn = new int[3];

        public bool IsGroupBattle => session is ILegacyEnemyRosterView roster && roster.Enemies.Count > 1;
        private int RemainingEnemyHealth => session is ILegacyEnemyRosterView roster
            ? roster.TotalEnemyHealth : session.Enemy.Health;

        private void ConfigureBattleEnemies(EnemyAppearance singleAppearance)
        {
            arena.ConfigureEnemyRoster(IsGroupBattle ? StageFiveAppearances : new[] { singleAppearance });
            hud.SetEnemyActors(arena.EnemyActorTransforms);
            if (IsGroupBattle) SelectArenaEnemyForSlot(0);
            RefreshEnemyRosterState();
        }

        // The rules switch their target when BeginNextSlot applies its effects. The arena must approach that same
        // opponent first, without advancing any rules or moving a fighter by changing the active pointer.
        private void SelectArenaEnemyForSlot(int slotIndex)
        {
            if (!IsGroupBattle) return;
            var roster = (ILegacyEnemyRosterView)session;
            int target = roster.EnemyIndexForSlot(EnemyQueueIndexForExecutionSlot(slotIndex));
            if (target < 0 || target >= roster.Enemies.Count || target == arena.ActiveEnemyIndex) return;
            arena.SelectActiveEnemy(target);
            resistanceFeedback.Reset();
            breakImpactCue.Reset();
            skillActivationCue.Reset();
        }

        private LegacyFighterState EnemyFacingNextSlot()
        {
            if (!IsGroupBattle) return session.Enemy;
            var roster = (ILegacyEnemyRosterView)session;
            return roster.Enemies[roster.EnemyIndexForSlot(EnemyQueueIndexForExecutionSlot(session.LastResolvedSlot + 1))];
        }

        private int EnemyQueueIndexForExecutionSlot(int slotIndex)
            => session.HasTempoSkills ? session.QueueIndexAtSegment(false, slotIndex) : slotIndex;

        // Look ahead only after the last actual impact. Readying another body is presentation, not BeginNextSlot:
        // the current skill keeps its target, recovery, damage and original shared queue indices.
        private void RefreshUpcomingEnemy()
        {
            int upcoming = -1;
            LegacySkill skill = null;
            if (IsGroupBattle && !session.IsFinished && !session.Player.IsDefeated &&
                !missionEventPending && !finale.IsRunning &&
                (viewPhase == ViewPhase.BetweenSlots ||
                 viewPhase == ViewPhase.PlayingSlot && session.CurrentSlot != null && session.CurrentSlot.IsResolved))
            {
                var roster = (ILegacyEnemyRosterView)session;
                int next = session.CurrentSlot != null ? session.CurrentSlot.SlotIndex + 1 : session.LastResolvedSlot + 1;
                next = EnemyQueueIndexForExecutionSlot(next);
                if (next < 0) { arena.SetUpcomingEnemy(-1, null); return; }
                for (int slot = next; slot < session.EnemyQueue.Count; slot++)
                {
                    LegacySkill candidate = session.EnemyQueue[slot];
                    int owner = roster.EnemyOwnerAtSlot(slot);
                    bool brokenGap = session.UsePowerBreak && session.IsEnemySkillDestroyed(slot);
                    if (candidate == null || owner < 0 || owner >= roster.Enemies.Count ||
                        roster.IsEnemySlotCanceled(slot) && !brokenGap || roster.Enemies[owner].IsDefeated) continue;
                    if (owner != arena.ActiveEnemyIndex)
                    {
                        upcoming = owner;
                        // The owner still occupies a destroyed reservation's time, without preparing its lost skill.
                        skill = brokenGap ? null : candidate;
                    }
                    // A consecutive action by the same owner keeps that owner on the line; do not stage a later one.
                    break;
                }
            }
            arena.SetUpcomingEnemy(upcoming, skill);
        }

        private void RefreshEnemyRosterState()
        {
            if (!IsGroupBattle)
            {
                arena.SetResistanceBroken(session.Player.IsResistanceBroken, session.Enemy.IsResistanceBroken);
                return;
            }
            var roster = (ILegacyEnemyRosterView)session;
            for (int i = 0; i < roster.Enemies.Count; i++)
            {
                arena.SetEnemyResistanceBroken(i, roster.Enemies[i].IsResistanceBroken);
                arena.SetEnemyDefeated(i, roster.Enemies[i].IsDefeated);
            }
            arena.SetResistanceBroken(session.Player.IsResistanceBroken,
                !roster.Enemies[arena.ActiveEnemyIndex].IsDefeated && roster.Enemies[arena.ActiveEnemyIndex].IsResistanceBroken);
        }

        private LegacyHealthReading BattleHealthReading()
        {
            if (!IsGroupBattle) return LegacyHealthReading.Of(session.Player, session.Enemy);
            var roster = (ILegacyEnemyRosterView)session;
            bool broken = false;
            foreach (LegacyFighterState fighter in roster.Enemies)
                if (!fighter.IsDefeated && fighter.IsResistanceBroken) broken = true;
            return new LegacyHealthReading(session.Player.Health, session.Player.MaxHealth,
                roster.TotalEnemyHealth, roster.TotalEnemyMaxHealth, broken);
        }

        private void CaptureEnemyTurnResistance()
        {
            if (!IsGroupBattle) return;
            var roster = (ILegacyEnemyRosterView)session;
            for (int i = 0; i < roster.Enemies.Count; i++) enemyResistanceBeforeTurn[i] = roster.Enemies[i].Resistance;
        }

        private void AnnounceEnemyTurnRecovery(int singleEnemyBefore)
        {
            if (!IsGroupBattle)
            {
                if (session.Enemy.Resistance > singleEnemyBefore)
                    hud.ShowRecoveryCallout(false, arena.EnemyRenderer.transform.position, arena.EnemyRenderer.transform);
                return;
            }
            var roster = (ILegacyEnemyRosterView)session;
            for (int i = 0; i < roster.Enemies.Count; i++)
                if (!roster.Enemies[i].IsDefeated && roster.Enemies[i].Resistance > enemyResistanceBeforeTurn[i])
                {
                    var actor = arena.GetEnemyRenderer(i).transform;
                    hud.ShowRecoveryCallout(false, actor.position, actor);
                }
        }
    }
}
