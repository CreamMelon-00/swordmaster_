using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Tavern;
using UnityEngine.InputSystem;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPrototypeController
    {
        private SkillTavernHud skillTavernHud;
        private SkillTavernBattleHud skillTavernBattleHud;
        private SkillTavernRun skillTavernRun;
        private SkillTavernBattle skillTavernBattle;
        private LegacyQueuedDuel skillTavernCampaignSession;
        private bool skillTavernActive, skillTavernResult;

        public SkillTavernHud SkillTavernHud => skillTavernHud;
        public SkillTavernRun SkillTavern => skillTavernRun;
        public SkillTavernBattle SkillTavernBattle => skillTavernBattle;
        public SkillTavernBattleHud SkillTavernBattleHud => skillTavernBattleHud;
        public bool IsInSkillTavern => skillTavernActive;
        public bool IsInSkillTavernBattle => skillTavernActive && skillTavernBattle != null && !skillTavernResult;
        public bool IsShowingSkillTavernResult => skillTavernActive && skillTavernResult;

        private void InitializeSkillTavern()
        {
            skillTavernHud = new SkillTavernHud(transform, art, ShowTitle,
                () => RestartSkillTavern(), (action, argument) => TrySkillTavernAction(action, argument));
            skillTavernBattleHud = new SkillTavernBattleHud(transform, art, () => ReturnToSkillTavernFormation(),
                () => TrySkillTavernAction(SkillTavernAction.NextRound), ShowTitle);
        }

        /// <summary>A separate, session-only economy experiment. The campaign save is never its state owner.</summary>
        public bool StartSkillTavern()
        {
            if (!IsInTitle || titleHud.IsConfirming || skillTavernHud == null) return false;
            if (skillTavernRun == null) skillTavernRun = new SkillTavernRun(System.Environment.TickCount);
            titleHud.Hide();
            showingTitle = false;
            lobbyHud.Hide();
            academyField.Hide();
            hud.Root.SetActive(false);
            skillTavernActive = true;
            if (skillTavernResult && skillTavernBattle != null)
                skillTavernBattleHud.ShowResult(skillTavernRun, skillTavernBattle);
            else skillTavernHud.Show(skillTavernRun);
            return true;
        }

        public bool RestartSkillTavern()
        {
            if (!IsInSkillTavern || !skillTavernHud.IsVisible) return false;
            skillTavernRun = new SkillTavernRun(System.Environment.TickCount);
            skillTavernHud.Show(skillTavernRun);
            return true;
        }

        /// <summary>The local authority for UI requests. A future room can forward these requests to its host without
        /// moving purchase, placement or sale rules into the view. These actions are not a network protocol.</summary>
        public bool TrySkillTavernAction(SkillTavernAction action, int argument = 0)
        {
            if (!IsInSkillTavern || skillTavernRun == null) return false;
            if (action == SkillTavernAction.NextRound)
            {
                if (argument != 0 || !IsShowingSkillTavernResult || skillTavernBattle == null ||
                    !skillTavernBattle.Duel.IsFinished) return false;
                skillTavernRun.NextRound();
                ShowSkillTavernFormation();
                return true;
            }
            // Only the shop owns economy requests. A playing or settled fight cannot edit its snapshot.
            if (!skillTavernHud.IsVisible || skillTavernBattle != null) return false;
            bool accepted;
            switch (action)
            {
                case SkillTavernAction.Buy: accepted = skillTavernRun.TryBuy(argument); break;
                case SkillTavernAction.Deploy: accepted = skillTavernRun.TryDeploy(argument); break;
                case SkillTavernAction.SellBoard: accepted = skillTavernRun.TrySellBoard(argument); break;
                case SkillTavernAction.SellInventory: accepted = skillTavernRun.TrySellInventory(argument); break;
                case SkillTavernAction.StartBattle:
                    if (argument != 0) return false;
                    return StartSkillTavernBattle();
                default: return false;
            }
            skillTavernHud.Show(skillTavernRun);
            return accepted;
        }

        private bool StartSkillTavernBattle()
        {
            skillTavernCampaignSession = session;
            skillTavernBattle = new SkillTavernBattle(skillTavernRun, System.Environment.TickCount);
            skillTavernResult = false;
            skillTavernHud.Hide();
            session = skillTavernBattle.Duel;
            ResetBattlePresentation();
            arena.SetEnemyAppearance(EnemyAppearance.CadetB);
            ConfigureBattleEnemies(EnemyAppearance.CadetB);
            hud.SetStageText("선술집 · " + skillTavernRun.RoundNumber + "라운드");
            hud.SetEnemyName(skillTavernBattle.EnemyName);
            hud.SetInputsHeld(true);
            hud.SetTurnHeaderVisible(false);
            if (!session.TrySetFixedQueues(skillTavernBattle.PlayerQueue, skillTavernBattle.EnemyQueue))
            {
                ShowSkillTavernFormation();
                return false;
            }
            session.Commit();
            BeginSkillTavernPlayback();
            skillTavernBattleHud.ShowBattle(skillTavernRun, skillTavernBattle);
            return true;
        }

        private void BeginNextSkillTavernTurn()
        {
            int playerResistanceBefore = session.Player.Resistance;
            int enemyResistanceBefore = session.Enemy.Resistance;
            if (!skillTavernBattle.TryBeginNextTurn()) return;
            arena.BeginTurn();
            hud.BeginTurn();
            if (session.Player.Resistance > playerResistanceBefore)
                hud.ShowRecoveryCallout(true, arena.PlayerRenderer.transform.position, arena.PlayerRenderer.transform);
            AnnounceEnemyTurnRecovery(enemyResistanceBefore);
            RefreshEnemyRosterState();
            BeginSkillTavernPlayback();
        }

        // Shares the ordinary slot, hit, camera and finish presentation, without opening manual reservation inputs.
        private void BeginSkillTavernPlayback()
        {
            directorSession = null;
            ClearHeldKeys();
            laneHolds.DropExplanation();
            guideInspectionRemaining = planningTime = 0f;
            committedTurnClockRan = false;
            hud.HideExplanation();
            SelectArenaEnemyForSlot(0);
            EnsureDirectorTurn();
            skillActivationCue.Reset();
            hud.EndTurn();
            arena.BeginApproach();
            highlightedSlot = -1;
            enemyQueueRevealPending = false;
            SetViewPhase(ViewPhase.Approaching);
            RefreshHud(0f);
        }

        private void FinishSkillTavernBattle()
        {
            if (!IsInSkillTavernBattle || !skillTavernBattle.Duel.IsFinished) return;
            skillTavernBattle.TryRecordResult();
            skillTavernResult = true;
            EndDuelPresentation();
            skillTavernBattleHud.ShowResult(skillTavernRun, skillTavernBattle);
        }

        public bool ReturnToSkillTavernFormation()
        {
            if (!IsInSkillTavernBattle) return false;
            // A decided fight still pays its result; Esc during the finishing shot skips only its presentation.
            if (skillTavernBattle.Duel.IsFinished) FinishSkillTavernBattle();
            else ShowSkillTavernFormation();
            return true;
        }

        private void ShowSkillTavernFormation()
        {
            if (skillTavernCampaignSession != null) EndDuelPresentation();
            RestoreSkillTavernSession();
            skillTavernBattleHud.Hide();
            skillTavernBattle = null;
            skillTavernResult = false;
            skillTavernHud.Show(skillTavernRun);
        }

        private void RestoreSkillTavernSession()
        {
            if (skillTavernCampaignSession == null) return;
            session = skillTavernCampaignSession;
            skillTavernCampaignSession = null;
            hud.SetStageText(null);
            hud.SetTurnHeaderVisible(true);
            hud.SetInputsHeld(false);
        }

        private void StopSkillTavernMode()
        {
            if (IsInSkillTavernBattle)
            {
                if (skillTavernBattle.Duel.IsFinished)
                {
                    skillTavernBattle.TryRecordResult();
                    skillTavernResult = true;
                }
                else skillTavernBattle = null;
            }
            RestoreSkillTavernSession();
            skillTavernActive = false;
            skillTavernHud?.Hide();
            skillTavernBattleHud?.Hide();
        }

        private bool AdvanceSkillTavern(Keyboard keyboard)
        {
            if (!IsInSkillTavern) return false;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (IsInSkillTavernBattle) ReturnToSkillTavernFormation();
                else ShowTitle();
                return true;
            }
            return !IsInSkillTavernBattle;
        }
    }
}
