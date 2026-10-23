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
        private bool skillTavernActive, skillTavernResult, skillTavernPlaying;
        private bool skillTavernCameraHidden, skillTavernPreviousCameraEnabled;

        public SkillTavernHud SkillTavernHud => skillTavernHud;
        public SkillTavernRun SkillTavern => skillTavernRun;
        public SkillTavernBattle SkillTavernBattle => skillTavernBattle;
        public SkillTavernBattleHud SkillTavernBattleHud => skillTavernBattleHud;
        public bool IsInSkillTavern => skillTavernActive;
        public bool IsInSkillTavernBattle => skillTavernActive && skillTavernPlaying;
        public bool IsShowingSkillTavernResult => skillTavernActive && skillTavernResult;

        private void InitializeSkillTavern()
        {
            skillTavernHud = new SkillTavernHud(transform, art, ShowTitle,
                () => RestartSkillTavern(), (action, argument) => TrySkillTavernAction(action, argument));
            skillTavernBattleHud = new SkillTavernBattleHud(transform, art, () => ReturnToSkillTavernFormation(),
                () => RestartSkillTavern(), ShowTitle);
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
            else ShowSkillTavernFormation();
            return true;
        }

        public bool RestartSkillTavern()
        {
            if (!IsInSkillTavern || IsInSkillTavernBattle ||
                !skillTavernHud.IsVisible && !IsShowingSkillTavernResult) return false;
            skillTavernRun = new SkillTavernRun(System.Environment.TickCount);
            skillTavernBattle = null;
            ShowSkillTavernFormation();
            return true;
        }

        /// <summary>The local authority for UI requests. A future room can forward these requests to its host without
        /// moving purchase, placement or sale rules into the view. These actions are not a network protocol.</summary>
        public bool TrySkillTavernAction(SkillTavernAction action, int argument = 0)
        {
            if (!IsInSkillTavern || skillTavernRun == null) return false;
            // Only the shop owns economy requests. A playing or settled fight cannot edit its snapshot.
            if (!skillTavernHud.IsVisible || skillTavernPlaying || skillTavernResult) return false;
            bool accepted;
            switch (action)
            {
                case SkillTavernAction.Buy: accepted = skillTavernRun.TryBuy(argument); break;
                case SkillTavernAction.BuyAndDeploy: accepted = skillTavernRun.TryBuyAndDeploy(argument); break;
                case SkillTavernAction.RefreshShop:
                    if (argument != 0) return false;
                    accepted = skillTavernRun.TryRefreshShop();
                    break;
                case SkillTavernAction.Deploy: accepted = skillTavernRun.TryDeploy(argument); break;
                case SkillTavernAction.SellBoard: accepted = skillTavernRun.TrySellBoard(argument); break;
                case SkillTavernAction.SellInventory: accepted = skillTavernRun.TrySellInventory(argument); break;
                case SkillTavernAction.StartBattle:
                    if (argument != 0) return false;
                    return StartSkillTavernBattle();
                default: return false;
            }
            skillTavernHud.Show(skillTavernRun, skillTavernBattle);
            return accepted;
        }

        private bool StartSkillTavernBattle()
        {
            if (skillTavernBattle == null)
                skillTavernBattle = new SkillTavernBattle(skillTavernRun, System.Environment.TickCount);
            if (!skillTavernBattle.TryStartTurn()) return false;
            skillTavernCampaignSession = session;
            skillTavernResult = false;
            skillTavernPlaying = true;
            skillTavernHud.Hide();
            RestoreSkillTavernCamera();
            session = skillTavernBattle.Duel;
            ResetBattlePresentation();
            arena.SetEnemyAppearance(EnemyAppearance.CadetB);
            ConfigureBattleEnemies(EnemyAppearance.CadetB);
            hud.SetStageText("선술집 · " + skillTavernRun.RoundNumber + "라운드");
            hud.SetEnemyName(skillTavernBattle.EnemyName);
            hud.SetInputsHeld(true);
            hud.SetTurnHeaderVisible(false);
            BeginSkillTavernPlayback();
            skillTavernBattleHud.ShowBattle(skillTavernRun, skillTavernBattle);
            return true;
        }

        private void FinishSkillTavernTurn()
        {
            if (!IsInSkillTavernBattle || !skillTavernBattle.TryCompleteTurn()) return;
            if (!skillTavernBattle.Duel.IsFinished)
            {
                ShowSkillTavernFormation();
                return;
            }
            skillTavernPlaying = false;
            skillTavernResult = true;
            EndDuelPresentation();
            skillTavernBattleHud.ShowResult(skillTavernRun, skillTavernBattle);
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
            FinishSkillTavernTurn();
        }

        public bool ReturnToSkillTavernFormation()
        {
            if (!IsInSkillTavernBattle) return false;
            // A settled turn cannot be undone during its return animation. Earlier exits restore its shop checkpoint.
            if (skillTavernBattle.Duel.IsFinished || skillTavernBattle.Duel.IsTurnResolved) FinishSkillTavernTurn();
            else
            {
                if (!skillTavernBattle.TryCancelTurn()) return false;
                ShowSkillTavernFormation();
            }
            return true;
        }

        private void ShowSkillTavernFormation()
        {
            if (skillTavernCampaignSession != null) EndDuelPresentation();
            RestoreSkillTavernSession();
            skillTavernBattleHud.Hide();
            skillTavernPlaying = false;
            skillTavernResult = false;
            HideSkillTavernCamera();
            skillTavernHud.Show(skillTavernRun, skillTavernBattle);
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
                if (skillTavernBattle.Duel.IsFinished || skillTavernBattle.Duel.IsTurnResolved)
                {
                    skillTavernBattle.TryCompleteTurn();
                    skillTavernResult = skillTavernBattle.Duel.IsFinished;
                }
                else skillTavernBattle.TryCancelTurn();
            }
            RestoreSkillTavernSession();
            RestoreSkillTavernCamera();
            skillTavernPlaying = false;
            skillTavernActive = false;
            skillTavernHud?.Hide();
            skillTavernBattleHud?.Hide();
        }

        private void HideSkillTavernCamera()
        {
            if (!skillTavernCameraHidden)
                skillTavernPreviousCameraEnabled = arena.ArenaCamera.enabled;
            skillTavernCameraHidden = true;
            arena.ArenaCamera.enabled = false;
        }

        private void RestoreSkillTavernCamera()
        {
            if (!skillTavernCameraHidden) return;
            arena.ArenaCamera.enabled = skillTavernPreviousCameraEnabled;
            skillTavernCameraHidden = false;
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
