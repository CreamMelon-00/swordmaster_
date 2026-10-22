using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TurnLimbo.Presentation
{
    /// <summary>Versus's battle: a <see cref="LocalVersusMatch"/> (one complete public queue per planner, a 20-second clock
    /// each, free 넘기기, both planners confirming resolves their queues; no steps, 맞물림 or 반격) played on the arena and shown on
    /// the campaign's duel HUD (<see cref="LegacyCombatHud"/>) from the viewer's side (<see cref="LocalVersusPerspective"/>):
    /// the viewer is always the HUD's player, on the arena's left, so the guest sees the match mirrored. It is the online
    /// friend duel's battle engine and has no other way in (one keyboard's two-player play is gone):
    /// <see cref="OnlineVersusController"/> starts it (<see cref="StartOnline"/>) and feeds it the host-approved commands.
    /// Over the duel HUD: the PvP overlay (<see cref="Hud"/>), the single-play start card ('나' and '상대', both 엘리사),
    /// the finishing blow (<see cref="DuelFinale"/>) and the single-play result card
    /// (<see cref="BattleResultHud.ShowVersus"/>). Campaign progress never enters this match.
    ///
    /// <para>The same match events play the same presentation on both peers, timed from the same
    /// <see cref="DuelPresentationSettings"/> (slot timing, hit stop, the decisive close-up's slow motion, the finishing
    /// blow), and nothing on one screen can stretch or skip it: holding a skill's explanation or Tab never slows time (the
    /// clocks are the host's), and the start card cannot be skipped. So no peer's planning clock starts later than the other's
    /// because of what its screen showed.</para></summary>
    public sealed partial class LocalVersusController : IDisposable
    {
        /// <summary>A turn's clock for each player: 20 seconds, plus what that player's skills earned last turn (제한시간), at
        /// most <see cref="LegacyPlanningClock.VersusCeilingSeconds"/>.</summary>
        public const float PlanningSeconds = 20f;
        /// <summary>After this many turns without a winner the match is a draw.</summary>
        public const int RoundLimit = 20;
        /// <summary>The start card's names: the viewer on the left, the opponent on the right.</summary>
        public const string LocalName = "나", OpponentName = "상대";
        /// <summary>The duel HUD's seal finalizes the viewer's complete queue.</summary>
        public const string CommitLabel = "예약 확정";
        public const string PassLabel = CommitLabel;
        /// <summary>The stage plate's and the result card's title.</summary>
        public const string MatchTitle = "온라인 대전";
        private enum ViewPhase { Planning, Approaching, Closing, PlayingSlot, BetweenSlots, AfterTurn, Returning, Finished }

        private readonly LegacyDuelArt art;
        private readonly LegacyArenaView arena;
        private readonly DuelPresentationSettings settings;
        private readonly DuelPresentationSettings ownedSettings;
        private readonly Action exitToTitle;
        private readonly LegacyCombatHud battleHud;
        private readonly LocalVersusHud hud;
        private readonly BattleResultHud resultHud;
        private readonly DuelStartCard startCard;
        private readonly DuelFinale finale;
        private readonly DuelSkillActivationCue activationCue;
        // The removal mode of the reservation whose cancelled cards tumble out (CancelledQueue), and the 순환 +1 cues read
        // before the action that may commit the turn (CaptureFeedCues).
        private LegacyQueueRemovalMode cancelledMode = LegacyQueueRemovalMode.Cancel;
        private string ownFeedCue, opponentFeedCue;
        private LegacySkill ownFeeder, opponentFeeder;
        private readonly DuelResistanceFeedback resistanceFeedback;
        private readonly DuelBreakImpactCue breakImpactCue;
        private readonly DuelLaneHolds holds = new DuelLaneHolds();
        private readonly Func<int, bool> queueOnRelease;
        private readonly AudioSource effects;
        private readonly AudioClip criticalSound;
        private readonly GameObject audioRoot;
        private readonly float[] clocks = new float[2];
        // Each clock's start this turn (the bar and the time pressure measure against it), and the one shown.
        private readonly float[] turnClocks = { PlanningSeconds, PlanningSeconds };
        private float shownTurnClock = PlanningSeconds;
        // What the last applied action cost its player's clock (a reservation's 제한시간), and whether 일회성 had been set
        // this turn before it, for the viewer's own presentation.
        private float lastActionSeconds;
        private bool temporaryUsedBefore;
        // The seconds the viewer's skills earned this turn's clock (제한시간), shown as '+N초' off the timer once the timer
        // shows the viewer's own clock (the opponent may hold the turn's first reservation).
        private float pendingTimeGained;
        private LocalVersusMatch match;
        private LocalVersusPerspective view;
        private LegacySkill[] leftSkills, rightSkills;
        private ViewPhase viewPhase;
        private float phaseTime;
        private float hitStopRemaining;
        // The clock the timer shows: the current planner's, kept through the resolution that follows.
        private float shownClock = PlanningSeconds;
        // Snapshotted when a slot begins, as in the campaign's battles.
        private float slotPlaybackSpeed = 1f, slotAttackInterval, slotImpactTime, slotCycleDuration, slotDuration;
        private float betweenSlotsDuration;
        private float turnCleanupDuration = DuelPrototypeController.TurnCleanupDelay;
        private bool finishingHitPlayback;
        private float finishingHitTime, finishingClipEnd;
        private int highlightedSlot = -1, nextSlotIndex;
        private int playerSlotDamage, enemySlotDamage;
        // The presentation director, as the campaign's (2026-10-15): rated symmetrically (the opponent's 간파 lifts a slot
        // as the viewer's does), so both screens give each slot the same weight and the same time.
        private readonly LegacyShowDirector showDirector = new LegacyShowDirector();
        private LocalVersusMatch directorMatch;
        private int directorRound = -1;
        // A blade bind holds the battle's clock for this many real seconds more, after the hit stop.
        private float bladeBindRemaining;
        private bool bladeBindHitPending, releasingBladeBindHit;
        private const float BladeBindCameraShare = .6f;
        private bool inspecting;
        private int inspectingEnemy;
        private bool wasExitConfirming;
        private bool active;
        private bool onlineHost;
        private bool pendingOnlineRequest;
        private bool disposed;
        private int onlineLocalPlayer = -1;
        private int onlineSeed;
        private int onlineOpeningPlayer;
        private Action<OnlineVersusAction, int> requestOnlineAction;
        private Action requestOnlineRematch;

        public event Action<int, OnlineVersusAction, int, int> OnlineActionApplied;
        public bool IsActive => active;
        /// <summary>This screen's side of the match (0 = Left, the host; 1 = Right, the guest), or -1 between matches.</summary>
        public int OnlineLocalPlayer => active ? onlineLocalPlayer : -1;
        public LocalVersusMatch Match => match;
        /// <summary>The match from this screen's side, as the duel HUD reads it (the host sees Left as its own, the guest
        /// Right); null between matches.</summary>
        public LocalVersusPerspective Perspective => view;
        /// <summary>The PvP overlay over the duel HUD: whose turn, the hint, the foot markers and the exit.</summary>
        public LocalVersusHud Hud => hud;
        /// <summary>The duel HUD, the campaign's own (<see cref="LegacyCombatHud"/>), showing the viewer as the player.</summary>
        public LegacyCombatHud BattleHud => battleHud;
        public BattleResultHud ResultHud => resultHud;
        public DuelStartCard StartCard => startCard;
        public DuelFinale Finale => finale;
        public DuelPresentationSettings PresentationSettings => settings;
        /// <summary>Whether a match opens with the start card. Like the campaign's (<see cref="DuelPrototypeController"/>),
        /// only the title's entry turns it on, so tests and direct API use start at the first planning turn.</summary>
        public bool StartCardsEnabled { get; set; }
        public bool IsShowingStartCard => startCard.IsShowing;
        public bool IsShowingResult => active && resultHud.IsVisible;
        /// <summary>Committed queues are playing out (from the approach to the return).</summary>
        public bool IsResolving => active && match != null && viewPhase != ViewPhase.Planning && viewPhase != ViewPhase.Finished;
        /// <summary>Tab holds the opponent's queue under inspection; time runs on as ever.</summary>
        public bool IsInspecting => inspecting;
        /// <summary>The skill whose explanation a held lane has open, or null.</summary>
        public LegacySkill ExplainedSkill => holds.ExplainedSkill;
        public float LeftClock => clocks[0];
        public float RightClock => clocks[1];
        /// <summary>What the duel HUD's timer shows: the current planner's clock (out of that clock's
        /// <see cref="ShownTurnClock"/>).</summary>
        public float ShownClock => shownClock;
        /// <summary>The shown clock's start this turn: <see cref="PlanningSeconds"/> plus what the player's skills earned
        /// last turn (사용 효과 앞 X마다 다음 턴 제한시간), at most <see cref="LegacyPlanningClock.VersusCeilingSeconds"/>.</summary>
        public float ShownTurnClock => shownTurnClock;
        /// <summary>A player's clock start this turn (0 = Left, 1 = Right).</summary>
        public float TurnClock(int player) => turnClocks[player];
        public float HitStopTimeRemaining => hitStopRemaining;
        /// <summary>The presentation director (<see cref="LegacyShowDirector"/>), rating every slot alike on both screens.</summary>
        public LegacyShowDirector ShowDirector => showDirector;
        /// <summary>Real seconds the current blade bind still holds the battle's clock (after the hit stop); 0 while none.</summary>
        public float BladeBindTimeRemaining => bladeBindRemaining;
        /// <summary>The legacy unopposed-streak multiplier, used only when progressive tempo is off. It speeds a run of
        /// unopposed slots (<see cref="LegacyShowDirector.StreakSpeed"/>, and the wait after such a slot). Versus has no step,
        /// 반격 or 맞물림, so a slot's kind is settled at its start and it plays faster from there; both screens count the
        /// same streak and rate the same tiers, so they play every slot for the same time.</summary>
        public float StreakPlaybackSpeed => viewPhase == ViewPhase.PlayingSlot ? showDirector.StreakSpeed
            : viewPhase == ViewPhase.BetweenSlots ? showDirector.GapSpeed : 1f;
        /// <summary>The turn's progressive playback speed, with an optional multiplier after either queue runs out. Versus has no steps or
        /// pending counters, so the whole slot can accelerate from its start, identically on both screens.</summary>
        public float ResolutionPlaybackSpeed => viewPhase == ViewPhase.PlayingSlot ? showDirector.PlaybackSpeed
            : viewPhase == ViewPhase.BetweenSlots ? showDirector.PlaybackGapSpeed
            : viewPhase == ViewPhase.Closing && showDirector.Tempo.Enabled ? showDirector.PlaybackGapSpeed : 1f;
        /// <summary>The stage plate's words: the match and its turn limit.</summary>
        public static string StageText => MatchTitle + "  ·  최대 " + RoundLimit + "턴";

        /// <param name="settings">The battles' presentation tuning (the campaign's asset); null loads it from Resources.</param>
        public LocalVersusController(Transform parent, LegacyDuelArt art, LegacyArenaView arena, Action exitToTitle,
            DuelPresentationSettings settings = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.exitToTitle = exitToTitle ?? throw new ArgumentNullException(nameof(exitToTitle));
            if (settings == null) settings = Resources.Load<DuelPresentationSettings>("DuelPresentationSettings");
            if (settings == null) settings = ownedSettings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            this.settings = settings;
            queueOnRelease = TryQueueLane;
            battleHud = new LegacyCombatHud(parent, art, lane => TryQueueLane(lane), () => TryCommit(), null,
                settings, () => TryBreath(), () => TryCycle());
            battleHud.SetStatusArena(arena);
            battleHud.SetCommitLabel(CommitLabel);
            battleHud.SetStageText(StageText);
            battleHud.Root.SetActive(false);
            hud = new LocalVersusHud(parent, art, Exit);
            hud.SetActorAnchors(arena.ArenaCamera, arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            hud.SetQueueAnchors(battleHud.PlayerQueuePanel, battleHud.EnemyQueuePanel);
            hud.Hide();
            resultHud = new BattleResultHud(parent, art, Exit, () => Rematch(), null);
            activationCue = new DuelSkillActivationCue(battleHud.Root.transform, art.UIFont);
            resistanceFeedback = new DuelResistanceFeedback(battleHud.Root.transform, art.UIFont);
            breakImpactCue = new DuelBreakImpactCue(battleHud.Root.transform);
            finale = new DuelFinale(parent, arena);
            // A click on the card does nothing: versus's card runs its full length on both peers.
            startCard = new DuelStartCard(parent, art, () => { });
            audioRoot = new GameObject("Local Versus Audio");
            audioRoot.transform.SetParent(parent, false);
            effects = audioRoot.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            effects.spatialBlend = 0f;
            criticalSound = Resources.Load<AudioClip>("LegacyDuel/Audio/Critical");
        }

        /// <summary>Starts a match on this screen. The host creates the seed and decides which side moves first, and applies
        /// every command itself (its own, and the guest's it accepts: <see cref="TryApplyOnlinePeerAction"/>); a guest only
        /// requests its own (<paramref name="requestAction"/>) and mirrors the accepted ones
        /// (<see cref="ApplyOnlineAcceptedAction"/>). With no network behind it, a host-only match runs whole on this screen
        /// (the tests drive it so).</summary>
        /// <param name="localPlayer">This screen's side: 0 (Left, the host) or 1 (Right, the guest).</param>
        /// <param name="requestRematch">The result card's 다시 겨루기: a vote the online room settles.</param>
        public void StartOnline(int seed, int localPlayer, bool isHost, int openingPlayer,
            IReadOnlyList<LegacySkill> leftSkills, IReadOnlyList<LegacySkill> rightSkills,
            Action<OnlineVersusAction, int> requestAction, Action requestRematch)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LocalVersusController));
            if (localPlayer < 0 || localPlayer > 1 || openingPlayer < 0 || openingPlayer > 1)
                throw new ArgumentOutOfRangeException(nameof(localPlayer));
            this.leftSkills = CopySkills(leftSkills);
            this.rightSkills = CopySkills(rightSkills);
            onlineHost = isHost;
            onlineLocalPlayer = localPlayer;
            onlineOpeningPlayer = openingPlayer;
            onlineSeed = seed;
            requestOnlineAction = requestAction;
            requestOnlineRematch = requestRematch;
            pendingOnlineRequest = false;
            BeginMatch();
        }

        public void Stop()
        {
            if (disposed || !active) return;
            arena.ClearLowHealthHeartbeat();
            arena.ClearSlowMotionAfterimages();
            active = false;
            effects.Stop();
            ClearCues();
            finale.Clear();
            startCard.Clear();
            holds.Clear(battleHud);
            battleHud.Reset();
            HideBattleScreens();
            leftSkills = rightSkills = null;
            match = null;
            view = null;
            inspecting = false;
            hitStopRemaining = 0f;
            ClearShowDirector();
            onlineHost = pendingOnlineRequest = false;
            onlineLocalPlayer = -1;
            requestOnlineAction = null;
            requestOnlineRematch = null;
        }

        // Tab holds every command but queue confirmation, as in the campaign's battles.
        public bool TryQueueLane(int lane) => !inspecting && SubmitAction(OnlineVersusAction.QueueLane, lane);
        public bool TryCycle() => !inspecting && SubmitAction(OnlineVersusAction.Cycle, -1);
        public bool TryBreath() => !inspecting && SubmitAction(OnlineVersusAction.Breath, -1);
        public bool TryCommit() => SubmitAction(OnlineVersusAction.Pass, -1);
        public bool TryPass() => TryCommit();

        private bool SubmitAction(OnlineVersusAction action, int lane)
        {
            if (!CanPlan) return false;
            if (!onlineHost)
            {
                // Ask the host only for what it would take. On the guest's own turn its copy of the match is the host's, so
                // a command the copy refuses (a lane ACT cannot pay, a fourth 숨고르기, 넘기기 with nothing to turn) does
                // nothing here, as on the host and in single play, rather than holding the dock for a round trip. Its clocks
                // are the host's too (synchronised), so a reservation its clock cannot pay for is not sent either.
                if (!CanApplyMatchAction(onlineLocalPlayer, action, lane) || !CanAffordAction(onlineLocalPlayer, action, lane))
                    return false;
                pendingOnlineRequest = true;
                requestOnlineAction?.Invoke(action, lane);
                RefreshHud(0f, 0f);
                return true;
            }
            int player = match.CurrentPlanner;
            int round = match.RoundNumber;
            if (!CanAffordAction(player, action, lane)) return false;
            LegacySkill[] cancelled = CancelledQueue(player, action, lane);
            CaptureFeedCues();
            float seconds = ReservationSeconds(player, action, lane);
            if (!ApplyMatchAction(player, action, lane)) return false;
            SpendClock(player, seconds);
            AfterAction(player, action, lane, cancelled);
            OnlineActionApplied?.Invoke(player, action, lane, round);
            return true;
        }

        /// <summary>Only the host accepts its connected opponent's requested command.</summary>
        public bool TryApplyOnlinePeerAction(OnlineVersusAction action, int lane, int round)
        {
            if (!active || !onlineHost || !IsPlanningShown ||
                match.Phase != LegacyDuelPhase.Planning || match.CurrentPlanner != 1 || match.RoundNumber != round)
                return false;
            // The host's clocks decide: a reservation the guest's clock cannot pay for is refused.
            if (!CanAffordAction(1, action, lane)) return false;
            CaptureFeedCues();
            float seconds = ReservationSeconds(1, action, lane);
            if (!ApplyMatchAction(1, action, lane)) return false;
            SpendClock(1, seconds);
            AfterAction(1, action, lane, null);
            OnlineActionApplied?.Invoke(1, action, lane, round);
            return true;
        }

        /// <summary>The guest applies only a host-approved command, in its original turn order.</summary>
        public bool ApplyOnlineAcceptedAction(int player, OnlineVersusAction action, int lane, int round)
        {
            if (!active || onlineHost || !IsPlanningShown || match.Phase != LegacyDuelPhase.Planning ||
                match.CurrentPlanner != player || match.RoundNumber != round)
                return false;
            LegacySkill[] cancelled = CancelledQueue(player, action, lane);
            CaptureFeedCues();
            // The host already took the time; its clocks come with this command (SyncOnlineClocks). Only shown here.
            lastActionSeconds = ReservationSeconds(player, action, lane);
            if (!ApplyMatchAction(player, action, lane)) return false;
            if (player == onlineLocalPlayer) pendingOnlineRequest = false;
            AfterAction(player, action, lane, cancelled);
            return true;
        }

        public void RejectOnlineRequest()
        {
            pendingOnlineRequest = false;
            if (active && match != null) RefreshHud(0f, 0f);
        }

        public void SyncOnlineClocks(float left, float right)
        {
            if (!active || onlineHost || match == null) return;
            // A turn's clock may start above 20 seconds (제한시간), never above the versus ceiling.
            clocks[0] = Mathf.Clamp(left, 0f, LegacyPlanningClock.VersusCeilingSeconds);
            clocks[1] = Mathf.Clamp(right, 0f, LegacyPlanningClock.VersusCeilingSeconds);
            RefreshHud(0f, 0f);
        }

        private bool ApplyMatchAction(int player, OnlineVersusAction action, int lane)
        {
            switch (action)
            {
                case OnlineVersusAction.QueueLane: return match.TryQueueLane(player, lane);
                case OnlineVersusAction.Cycle: return match.TryCycleLanes(player);
                case OnlineVersusAction.Breath: return match.TryQueueBreath(player);
                case OnlineVersusAction.Pass: return match.TryCommit(player);
                default: return false;
            }
        }

        // 예약 효과 제한시간 -n초: what reserving the lane's front skill costs its player's clock (0 for anything else).
        private float ReservationSeconds(int player, OnlineVersusAction action, int lane)
        {
            if (action != OnlineVersusAction.QueueLane || lane < 0 || lane > 2 || match == null) return 0f;
            IReadOnlyList<LegacySkill> skills = match.GetFighter(player).GetLane(lane);
            return skills.Count == 0 ? 0f : LegacyPlanningClock.ReservationSeconds(skills[0]);
        }

        // Whether the player's clock can pay for the command (versus clocks always run).
        private bool CanAffordAction(int player, OnlineVersusAction action, int lane)
            => LegacyPlanningClock.CanAfford(clocks[player], ReservationSeconds(player, action, lane), true);

        private void SpendClock(int player, float seconds)
        {
            lastActionSeconds = seconds;
            clocks[player] = LegacyPlanningClock.Spend(clocks[player], seconds, true);
        }

        // ApplyMatchAction's read-only checks, including whether this planner can finalize their queue.
        private bool CanApplyMatchAction(int player, OnlineVersusAction action, int lane)
        {
            switch (action)
            {
                case OnlineVersusAction.QueueLane: return match.CanQueueLane(player, lane);
                case OnlineVersusAction.Cycle: return match.CanCycleLanes(player);
                case OnlineVersusAction.Breath: return match.CanQueueBreath(player);
                case OnlineVersusAction.Pass: return match.CanCommit(player);
                default: return false;
            }
        }

        /// <summary>The result card's 다시 겨루기: sends this screen's vote and waits (상대 기다리는 중). The rematch itself
        /// comes as the host's next <see cref="StartOnline"/>, once both players have voted.</summary>
        public bool Rematch()
        {
            if (!active || match == null || !match.IsFinished || hud.IsExitConfirming) return false;
            resultHud.SetRematchPending(true);
            requestOnlineRematch?.Invoke();
            return true;
        }

        public void Tick(float realDelta, Keyboard keyboard)
        {
            if (!active || disposed || match == null) return;
            realDelta = float.IsNaN(realDelta) || float.IsInfinity(realDelta) ? 0f : Mathf.Max(0f, realDelta);
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) hud.ToggleExitConfirmation();
            // However the question opened or closed (Esc, 타이틀 [Esc], 계속하기), keys held across it never queue.
            if (hud.IsExitConfirming != wasExitConfirming)
            {
                wasExitConfirming = hud.IsExitConfirming;
                holds.Clear(battleHud);
                ConsumeHeldLaneKeys(keyboard);
            }
            // The question never pauses the match: the host's clocks keep running, and so does what is on screen.
            // Break impulses use real time even during the finishing blow's frozen frame.
            battleHud.AdvanceBreakQueueFragments(realDelta);
            breakImpactCue.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            // The result card stands over the battle's last picture: nothing moves under it.
            if (viewPhase == ViewPhase.Finished) return;
            if (startCard.IsShowing)
            {
                AdvanceStartCard(realDelta);
                ConsumeHeldLaneKeys(keyboard);
                return;
            }
            // The finishing blow's freeze frame: nothing moves until the result comes.
            if (finale.IsFrozen)
            {
                finale.Tick(realDelta);
                if (finale.IsComplete) ShowResult();
                return;
            }
            inspecting = CanInspect && keyboard != null && keyboard.tabKey.isPressed;
            bool timedOut = AdvanceClock(realDelta);
            if (viewPhase == ViewPhase.Planning && !timedOut && !hud.IsExitConfirming)
                ReadPlanningInput(keyboard, realDelta);

            // Planning never slows (not for Tab, not for an explanation): the clocks are real time on both peers. Slow
            // motion belongs to the decisive close-up (a break, the finishing blow, a heavy hit) and the finishing blow, both
            // set by the same hits on both screens.
            float speed = IsResolving && arena.IsFatalFocus ? LegacyArenaView.DecisiveSlowMotionScale : 1f;
            if (finale.IsRunning) speed = finale.CombatSpeed;
            // Later slots accelerate, with an optional extra speed once one queue runs out. Both screens use the same facts; cinematic
            // slow motion still owns its clock. The old streak is a tuning fallback only.
            else if (IsResolving && speed >= 1f) speed *= ResolutionPlaybackSpeed;
            float stoppedTime = Mathf.Min(realDelta, hitStopRemaining);
            hitStopRemaining = Mathf.Max(0f, hitStopRemaining - stoppedTime);
            // A blade bind holds the battle's clock after the hit stop, the same seconds on both screens.
            float exchangeTime = bladeExchange.Hold(realDelta - stoppedTime, arena);
            float liveTime = realDelta - stoppedTime - exchangeTime - AdvanceBladeBind(realDelta - stoppedTime - exchangeTime);
            // The finishing blow's slow motion runs on the time the hit stop left; its freeze begins once it is over.
            if (finale.IsRunning)
            {
                finale.Tick(liveTime);
                if (finale.IsComplete)
                {
                    ShowResult();
                    return;
                }
            }
            float delta = liveTime * speed;
            bool planning = IsPlanningShown;
            float clock = planning ? clocks[match.CurrentPlanner] : shownClock;
            arena.SetPlanningState(Mathf.Min(10f, clock), inspecting);
            // The running clock's last share pushes the camera in a touch, whoever's clock it is, against that clock's own
            // start this turn.
            arena.SetTimePressure(planning
                ? LegacyTimePressure.Target(clock, turnClocks[match.CurrentPlanner], settings.TimePressureShare) : 0f);
            // The fighters' feet stay above the duel HUD's queue panels (as last laid out).
            arena.SetHudFloor(battleHud.QueuePanelFloor);
            // A Major slot (and a blade bind) pushes the combat framing in a touch.
            arena.SetDirectorPush(DirectorCameraPush);
            // 긴장 조명 from this screen's side: either fighter's low health darkens both screens alike, and each screen
            // swells for its own player's turn of the flow. Lighting only: nothing here reaches the match or its timing.
            arena.SetTensionLighting(LegacyHealthReading.Of(view.Player, view.Enemy), settings.TensionLighting);
            if (!finale.IsRunning && !bladeExchange.IsHolding && viewPhase == ViewPhase.PlayingSlot && view.CurrentSlot != null)
                DuelPrototypeController.RequestCombatCameraShot(arena,
                    .2f + .8f * Mathf.Clamp01(phaseTime / Mathf.Max(.001f, slotDuration)),
                    view.CurrentSlot.PlayerSkill, view.CurrentSlot.EnemySkill);
            arena.Tick(delta, realDelta, deferSlowAfterimages: true);
            bladeExchange.Present(arena, art, effects);
            phaseTime += delta;
            switch (viewPhase)
            {
                case ViewPhase.Approaching:
                    if (arena.ApproachComplete)
                    {
                        battleHud.BeginCombat();
                        PrepareSlot();
                    }
                    break;
                case ViewPhase.Closing:
                    if (!arena.IsPursuing) arena.CloseDistance(delta);
                    if (arena.IsReadyForSlot) BeginSlot();
                    break;
                case ViewPhase.PlayingSlot:
                    PlaySlot(delta);
                    break;
                case ViewPhase.BetweenSlots:
                    if (phaseTime >= betweenSlotsDuration) PrepareSlot();
                    break;
                case ViewPhase.AfterTurn:
                    if (finishingHitPlayback)
                        arena.HoldSlotAtTime(Mathf.Min(finishingHitTime + phaseTime, finishingClipEnd));
                    // A finishing blow hands over from its freeze frame itself (ShowResult), without the return.
                    if (phaseTime >= turnCleanupDuration && !finale.IsRunning)
                    {
                        battleHud.BeginReturn();
                        arena.EndTurn();
                        SetViewPhase(ViewPhase.Returning);
                    }
                    break;
                case ViewPhase.Returning:
                    if (arena.ReturnComplete)
                    {
                        if (match.IsFinished) ShowResult();
                        else StartPlanning();
                    }
                    break;
            }
            // A signature move's strike trail.
            arena.TickSignatureTrails(realDelta, viewPhase == ViewPhase.PlayingSlot && !bladeExchange.IsHolding);
            if (viewPhase != ViewPhase.Finished) arena.SampleSlowAfterimages(realDelta);
            arena.TickAttackMotionTrail(realDelta, viewPhase == ViewPhase.PlayingSlot && !bladeExchange.IsHolding);
            if (viewPhase != ViewPhase.Finished) RefreshHud(delta, realDelta);
        }

        private void BeginMatch()
        {
            // Another match on the same screens uses the retry setting, with the same minimum coin reveal on both peers.
            bool rematch = match != null;
            match = new LocalVersusMatch(leftSkills, rightSkills,
                randomSeed: onlineSeed, roundLimit: RoundLimit, openingPlayer: onlineOpeningPlayer);
            view = new LocalVersusPerspective(match, onlineLocalPlayer);
            pendingOnlineRequest = false;
            active = true;
            ResetClocks();
            shownClock = shownTurnClock = PlanningSeconds;
            effects.Stop();
            ClearCues();
            finale.Clear();
            startCard.Clear();
            resultHud.Hide();
            arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
            arena.SetEnemyAppearance(EnemyAppearance.Elisa);
            arena.Reset();
            arena.SetDepthPerspective(onlineLocalPlayer);
            arena.BeginTurn();
            arena.SetResistanceBroken(false, false);
            ClearShowDirector();
            battleHud.Reset();
            battleHud.SetEnemyName(OpponentName);
            battleHud.Root.SetActive(true);
            hud.Show();
            holds.Clear(battleHud);
            wasExitConfirming = false;
            hitStopRemaining = 0f;
            highlightedSlot = -1;
            nextSlotIndex = 0;
            playerSlotDamage = enemySlotDamage = 0;
            inspecting = false;
            inspectingEnemy = 0;
            SetViewPhase(ViewPhase.Planning);
            BeginStartCard(rematch);
            RefreshHud(0f, 0f);
        }

        private static LegacySkill[] CopySkills(IReadOnlyList<LegacySkill> skills)
        {
            if (skills == null || skills.Count == 0) throw new ArgumentException("A versus loadout is required.", nameof(skills));
            var copy = new LegacySkill[skills.Count];
            for (int i = 0; i < skills.Count; i++)
                copy[i] = skills[i] ?? throw new ArgumentException("A versus loadout cannot contain empty skills.", nameof(skills));
            return copy;
        }

        // Each player's clock for the new turn: 20 seconds and what that player's skills earned last turn, up to 30.
        private void ResetClocks()
        {
            pendingTimeGained = 0f;
            for (int player = 0; player < clocks.Length; player++)
            {
                int bonus = match != null ? match.GetFighter(player).PlanningSecondsBonus : 0;
                clocks[player] = turnClocks[player] = LegacyPlanningClock.VersusTurnStart(PlanningSeconds, bonus);
            }
        }

        // A planning turn is on screen, someone holds it, and the start card is gone.
        private bool IsPlanningShown => viewPhase == ViewPhase.Planning && match.Phase == LegacyDuelPhase.Planning &&
            match.CurrentPlanner >= 0 && !startCard.IsShowing;

        private bool CanPlan => active && match != null && IsPlanningShown && !hud.IsExitConfirming &&
            match.CurrentPlanner == onlineLocalPlayer && !pendingOnlineRequest;

        // Tab reads the opponent's queue on either player's turn.
        private bool CanInspect => IsPlanningShown && !hud.IsExitConfirming;

        /// <summary>The start card ('나' on the left, '상대' on the right, both 엘리사) holds the match from its start to its first
        /// planning turn: no clock, no input. The central coin reveals the host's opening-player result. It cannot be
        /// skipped, and a rematch also holds long enough to show its new toss.</summary>
        private void BeginStartCard(bool rematch)
        {
            float seconds = !StartCardsEnabled ? 0f : rematch ? settings.StartCardRetrySeconds : settings.StartCardSeconds;
            if (seconds <= 0f)
            {
                startCard.Clear();
                return;
            }
            startCard.ShowVersusCoinToss(seconds, match.OpeningPlayer, view.LocalPlayer);
            battleHud.Root.SetActive(false);
            hud.SetBattleVisible(false);
        }

        // A frame of the start card: the fighters breathe behind it; the duel HUD comes back under it as it leaves.
        private void AdvanceStartCard(float realDelta)
        {
            arena.SetPlanningState(Mathf.Min(10f, shownClock), false);
            // Framed for the queue panels already, so the camera does not drift as the HUD comes back under the card.
            arena.SetHudFloor(battleHud.QueuePanelFloor);
            arena.Tick(realDelta, realDelta);
            if (startCard.Tick(realDelta))
            {
                ShowBattleScreens();
                holds.Clear(battleHud);
            }
            else if (startCard.IsLeaving && !battleHud.Root.activeSelf) ShowBattleScreens();
            RefreshHud(0f, realDelta);
        }

        private void ShowBattleScreens()
        {
            battleHud.Root.SetActive(true);
            hud.SetBattleVisible(true);
        }

        private void HideBattleScreens()
        {
            battleHud.Root.SetActive(false);
            resultHud.SetRematchPending(false);
            resultHud.Hide();
            hud.Hide();
        }

        /// <summary>The current planner's clock runs on real time while that player holds the reservation turn (never under
        /// the start card). Only the host finalizes an expired queue; the guest's clocks follow the host's.</summary>
        private bool AdvanceClock(float realDelta)
        {
            if (!IsPlanningShown || realDelta <= 0f) return false;
            int planner = match.CurrentPlanner;
            clocks[planner] = Mathf.Max(0f, clocks[planner] - realDelta);
            if (clocks[planner] > 0f || !onlineHost) return false;
            int round = match.RoundNumber;
            // The second confirmation starts combat: capture its 순환 +1 cues before applying the action.
            CaptureFeedCues();
            if (!match.TryCommit(planner)) return false;
            AfterAction(planner, OnlineVersusAction.Pass, -1, null);
            OnlineActionApplied?.Invoke(planner, OnlineVersusAction.Pass, -1, round);
            return true;
        }

        /// <summary>The campaign's planning keys (<see cref="DuelPrototypeController"/>): Q/W/E and the pointer on the gears'
        /// windows by <see cref="DuelLaneHolds"/> (a tap queues, a hold explains), 1/2/3 queue at once, Shift 넘기기, S
        /// 숨고르기, Space/Enter the seal (예약 확정), L the log, and Tab (held) reads the opponent's queue, ←/→ along it. Only the
        /// viewer's own turn acts (<see cref="CanPlan"/>); nothing here slows time.</summary>
        private void ReadPlanningInput(Keyboard keyboard, float realDelta)
        {
            if (inspecting)
            {
                holds.Clear(battleHud);
                if (keyboard == null) return;
                if (keyboard.leftArrowKey.wasPressedThisFrame) inspectingEnemy = Mathf.Max(0, inspectingEnemy - 1);
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                    inspectingEnemy = Mathf.Min(view.EnemyQueue.Count - 1, inspectingEnemy + 1);
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) TryCommit();
                return;
            }
            if (keyboard == null)
            {
                // Without a keyboard the lanes still answer the pointer held on their gears' windows.
                for (int lane = 0; lane < DuelLaneHolds.LaneCount; lane++) ReadLaneHold(lane, false, false, false, realDelta);
                return;
            }
            if (keyboard.lKey.wasPressedThisFrame) battleHud.ToggleLog();
            if (keyboard.sKey.wasPressedThisFrame) TryBreath();
            // Before the lane keys, so a lane key pressed in the same frame is for the skill 넘기기 brought forward.
            if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame) TryCycle();
            ReadLaneHold(0, keyboard.qKey.isPressed, keyboard.qKey.wasPressedThisFrame, keyboard.qKey.wasReleasedThisFrame, realDelta);
            ReadLaneHold(1, keyboard.wKey.isPressed, keyboard.wKey.wasPressedThisFrame, keyboard.wKey.wasReleasedThisFrame, realDelta);
            ReadLaneHold(2, keyboard.eKey.isPressed, keyboard.eKey.wasPressedThisFrame, keyboard.eKey.wasReleasedThisFrame, realDelta);
            if (keyboard.digit1Key.wasPressedThisFrame) TryQueueLane(0);
            if (keyboard.digit2Key.wasPressedThisFrame) TryQueueLane(1);
            if (keyboard.digit3Key.wasPressedThisFrame) TryQueueLane(2);
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) TryCommit();
        }

        private void ReadLaneHold(int lane, bool keyHeld, bool keyPressed, bool keyReleased, float realDelta)
            => holds.Read(lane, keyHeld, keyPressed, keyReleased, realDelta, battleHud, settings, view.GetLane(lane),
                queueOnRelease);

        // Keys held across the opening toss or exit question cannot queue a skill when released afterwards.
        private void ConsumeHeldLaneKeys(Keyboard keyboard)
        {
            if (keyboard == null) return;
            if (keyboard.qKey.isPressed) holds.Consume(0);
            if (keyboard.wKey.isPressed) holds.Consume(1);
            if (keyboard.eKey.isPressed) holds.Consume(2);
        }

        // The viewer's queue before a lane skill that cancels or returns it, so its cards can tumble out (the campaign's QueueLane).
        private LegacySkill[] CancelledQueue(int player, OnlineVersusAction action, int lane)
        {
            if (action != OnlineVersusAction.QueueLane || view == null || !view.IsPlayerSide(player) || lane < 0 || lane > 2)
                return null;
            LocalVersusFighter fighter = match.GetFighter(player);
            IReadOnlyList<LegacySkill> laneSkills = fighter.GetLane(lane);
            LegacyQueueRemovalMode removal = laneSkills.Count == 0 ? LegacyQueueRemovalMode.None
                : LegacySkillDefinitions.Find(laneSkills[0])?.Effect?.QueueRemovalMode ?? LegacyQueueRemovalMode.None;
            if (fighter.Queue.Count == 0 || removal == LegacyQueueRemovalMode.None)
                return null;
            cancelledMode = removal;
            var queued = new LegacySkill[fighter.Queue.Count];
            for (int index = 0; index < queued.Length; index++) queued[index] = fighter.Queue[index];
            return queued;
        }

        /// <summary>A reservation was applied to the match: the viewer's own is presented as in the campaign's battles (the
        /// gears turn, the selection sound). Each player confirms once after building their whole queue.</summary>
        private void AfterAction(int player, OnlineVersusAction action, int lane, LegacySkill[] cancelled)
        {
            if (view.IsPlayerSide(player)) PresentOwnAction(action, lane, cancelled);
            AfterReservation();
        }

        private void PresentOwnAction(OnlineVersusAction action, int lane, LegacySkill[] cancelled)
        {
            switch (action)
            {
                case OnlineVersusAction.QueueLane:
                    if (cancelled != null) battleHud.PlayQueueCancellation(cancelled, cancelledMode);
                    battleHud.ClearHoldCancel();
                    // The lane's gear turns a slot: the queued skill to the upper right, the next one up into the window.
                    battleHud.PlayLaneTurn(lane);
                    // 예약 효과: the time it cost, and the 일회성 copies it set. The planner's clock continues running;
                    // '-4초' rises over the reserved lane's window (its cost read '3 ACT · 4초').
                    if (lastActionSeconds > 0f) battleHud.ShowLaneTimeSpent(lane, lastActionSeconds);
                    LocalVersusFighter self = view.Me;
                    // When the opponent holds a card in the slot just reserved into, a spark bursts at both cards.
                    battleHud.PlayReservationSpark(self.LastQueuedSlotIndex);
                    if (!temporaryUsedBefore && (self.ReserveEffectsUsedThisTurn & LegacyReserveEffect.Temporary) != 0 &&
                        self.LastQueuedSlotIndex >= 0 && self.LastQueuedSlotIndex < self.Queue.Count)
                    {
                        LegacySkill reserved = self.Queue[self.LastQueuedSlotIndex];
                        LegacySkill spawned = LegacyKeywordLinks.TemporarySpawn(reserved, out int copies);
                        if (spawned != null) battleHud.ShowLanePop(spawned.LaneIndex, "+" + copies, true);
                        activationCue.ShowKeyword(true, reserved, LegacyKeywordPreview.TemporarySpawnCue(reserved));
                    }
                    PlaySelection();
                    break;
                case OnlineVersusAction.Cycle:
                    battleHud.ClearHoldCancel();
                    // Every lane's gear turns together, meshed, and ratchets. 넘기기 is free in versus.
                    LocalVersusFighter me = view.Me;
                    battleHud.PlayLaneTurn(new[] { me.GetLane(0).Count > 0, me.GetLane(1).Count > 0, me.GetLane(2).Count > 0 });
                    PlaySelection();
                    // A lane key already down was pressed for the skill that just left.
                    holds.ConsumeActive();
                    break;
                case OnlineVersusAction.Breath:
                    battleHud.ClearHoldCancel();
                    battleHud.PlayReservationSpark(view.PlayerQueue.Count - 1);
                    PlaySelection();
                    break;
            }
            holds.DropExplanation();
        }

        // 순환 +1 rises as the turn commits, inside its second confirmation: what it raises on
        // each side is read before every action and said once the resolution begins.
        private void CaptureFeedCues()
        {
            lastActionSeconds = 0f;
            if (view == null || match == null || match.Phase != LegacyDuelPhase.Planning) return;
            temporaryUsedBefore = (view.Me.ReserveEffectsUsedThisTurn & LegacyReserveEffect.Temporary) != 0;
            ownFeedCue = LegacyKeywordPreview.CycleFeedCue(view.PlayerQueue, view.CycleUses);
            ownFeeder = LegacyKeywordPreview.FirstCycleFeeder(view.PlayerQueue);
            opponentFeedCue = LegacyKeywordPreview.CycleFeedCue(view.EnemyQueue, view.EnemyCycleUses);
            opponentFeeder = LegacyKeywordPreview.FirstCycleFeeder(view.EnemyQueue);
        }

        private void PlaySelection()
        {
            effects.pitch = 1f;
            art.PlaySelection(effects, UnityEngine.Random.Range(0, 3));
        }

        private void AfterReservation()
        {
            if (match.Phase != LegacyDuelPhase.Planning)
            {
                // Both full queues are confirmed (or the last turn ended with nothing queued: a draw).
                holds.Clear(battleHud);
                inspecting = false;
                battleHud.HideExplanation();
                battleHud.EndTurn();
                highlightedSlot = -1;
                nextSlotIndex = 0;
                if (match.Phase == LegacyDuelPhase.Resolving && match.ResolutionSlotCount > 0)
                {
                    // The director's turn: its ledger starts afresh with the numbers as they are now.
                    directorMatch = null;
                    EnsureDirectorTurn();
                    arena.BeginApproach();
                    SetViewPhase(ViewPhase.Approaching);
                    activationCue.Reset();
                    activationCue.ShowKeyword(true, ownFeeder, ownFeedCue);
                    activationCue.ShowKeyword(false, opponentFeeder, opponentFeedCue);
                }
                else EndRound();
                ownFeedCue = opponentFeedCue = null;
                ownFeeder = opponentFeeder = null;
            }
            RefreshHud(0f, 0f);
        }

        private void SetViewPhase(ViewPhase next)
        {
            if (next == ViewPhase.AfterTurn) turnCleanupDuration = settings.TurnCleanupSeconds;
            if (next != ViewPhase.AfterTurn) finishingHitPlayback = false;
            viewPhase = next;
            phaseTime = 0f;
        }

        private void PrepareSlot(bool waitBetweenSlots = false)
        {
            if (DuelPrototypeController.HasOnlyBreakCanceledSlotsLeft(view, nextSlotIndex))
                while (!match.IsFinished && !match.IsTurnResolved)
                    nextSlotIndex = match.ResolveNextSlot().SlotIndex + 1;
            if (match.IsFinished || match.IsTurnResolved)
            {
                EndRound();
                return;
            }
            highlightedSlot = nextSlotIndex;
            if (waitBetweenSlots)
            {
                // After a Minor slot the wait is shorter too (the director saves the time).
                // The exhausted attack's empty gap is compressed without speeding its clip or funding extra emphasis.
                betweenSlotsDuration = showDirector.ShortenGap(Mathf.Max(0f, settings.SkillInterval))
                    * showDirector.PlaybackGapScale;
                if (betweenSlotsDuration > 0f)
                {
                    SetViewPhase(ViewPhase.BetweenSlots);
                    return;
                }
            }
            arena.PrepareSlotMovement(view, nextSlotIndex);
            SetViewPhase(ViewPhase.Closing);
            if (arena.IsReadyForSlot) BeginSlot();
        }

        // Uses the playback speed and attack gap snapshotted when the slot began.
        private float SlotDurationFor(int hits)
        {
            float clipDuration = LegacyArenaView.OriginalClipDuration / slotPlaybackSpeed;
            return clipDuration * hits + slotAttackInterval * (hits - 1) + .01f;
        }

        private void BeginSlot()
        {
            int playerResistanceBefore = view.Player.Resistance;
            int enemyResistanceBefore = view.Enemy.Resistance;
            bool playerWasBroken = view.Player.IsResistanceBroken;
            bool enemyWasBroken = view.Enemy.IsResistanceBroken;
            LocalVersusPerspectiveSlot slot = view.Slot(match.BeginNextSlot());
            highlightedSlot = slot.SlotIndex;
            // The director rates what the slot's start already tells (a Major slot pushes the camera in at once).
            EnsureDirectorTurn();
            LegacySlotRank startRank = showDirector.BeginSlot(view.CurrentSlotFacts);
            ShowSlotWeight(slot.SlotIndex, startRank);
            // Snapshotted together, so live tuning never moves an impact to another frame mid-skill.
            slotPlaybackSpeed = Mathf.Max(.01f, settings.AnimationPlaybackSpeed);
            slotAttackInterval = Mathf.Max(0f, settings.AttackInterval);
            float clipDuration = LegacyArenaView.OriginalClipDuration / slotPlaybackSpeed;
            slotImpactTime = LegacyArenaView.OriginalImpactTime / slotPlaybackSpeed;
            slotCycleDuration = clipDuration + slotAttackInterval;
            slotDuration = SlotDurationFor(slot.HitCount);
            playerSlotDamage = enemySlotDamage = 0;
            arena.ConfigureSlotTiming(slotPlaybackSpeed, slotAttackInterval);
            arena.BeginSlot(slot.PlayerSkill, slot.EnemySkill);
            // A signature move on either side (no windup in versus: its glow gathers to the first strike).
            if (settings.SignatureTreatmentEnabled)
            {
                if (startRank.PlayerSignature)
                    DuelPrototypeController.BeginSignature(arena, settings, true, slot.PlayerSkill, slotImpactTime);
                if (startRank.EnemySignature)
                    DuelPrototypeController.BeginSignature(arena, settings, false, slot.EnemySkill, slotImpactTime);
            }
            battleHud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, slotDuration);
            activationCue.Reset();
            battleHud.SetSkillFeedback(slot);
            // A count-ahead payoff's bonus (power, next turn's time) joins its slot's cue, on either side.
            activationCue.Show(true, slot.PlayerSkill, slot.PlayerFeedback, KeywordCue(view.PlayerQueue, slot.SlotIndex,
                view.ForPlayer(slot.Source.LeftPlanningSecondsGained, slot.Source.RightPlanningSecondsGained)));
            activationCue.Show(false, slot.EnemySkill, slot.EnemyFeedback, KeywordCue(view.EnemyQueue, slot.SlotIndex,
                view.ForEnemy(slot.Source.LeftPlanningSecondsGained, slot.Source.RightPlanningSecondsGained)));
            // The actual capped change of the slot's opening effects (recovery, direct loss), never a hit's.
            resistanceFeedback.Show(true, view.Player.Resistance - playerResistanceBefore);
            resistanceFeedback.Show(false, view.Enemy.Resistance - enemyResistanceBefore);
            AnnounceBreaks(playerWasBroken, enemyWasBroken);
            SetViewPhase(ViewPhase.PlayingSlot);
        }

        private static string KeywordCue(IReadOnlyList<LegacySkill> queue, int slotIndex, int secondsGained)
        {
            string power = LegacyKeywordPreview.AheadPowerCue(queue, slotIndex);
            string time = LegacyKeywordPreview.AheadTimeCue(secondsGained);
            return power == null ? time : time == null ? power : power + " · " + time;
        }

        private void PlaySlot(float delta)
        {
            if (bladeBindHitPending || bladeExchange.IsHolding) return;
            LocalVersusPerspectiveSlot slot = view.CurrentSlot;
            if (slot == null) return;
            float attackTime = slotImpactTime + slotCycleDuration * slot.HitsResolved;
            float animationClipEnd = slotCycleDuration * slot.HitsResolved +
                LegacyArenaView.OriginalClipDuration / slotPlaybackSpeed;
            if (!slot.IsResolved && phaseTime >= attackTime)
            {
                if (!releasingBladeBindHit && !arena.IsReadyForSlot)
                {
                    // Movement keeps running while this contact frame waits: no strike lands out of reach, and a long
                    // chase never fires several deferred hits together.
                    if (!arena.IsPursuing) arena.CloseDistance(delta);
                    if (!arena.IsReadyForSlot)
                    {
                        phaseTime = attackTime;
                        arena.HoldSlotAtTime(phaseTime);
                        return;
                    }
                }
                if (TryBeginBladeExchange(slot, attackTime)) return;
                if (!releasingBladeBindHit && TryBeginBladeBind(slot.SlotIndex, attackTime)) return;
                int playerResistanceBefore = view.Player.Resistance;
                int enemyResistanceBefore = view.Enemy.Resistance;
                int playerHealthBefore = view.Player.Health;
                int enemyHealthBefore = view.Enemy.Health;
                bool playerWasBroken = view.Player.IsResistanceBroken;
                bool enemyWasBroken = view.Enemy.IsResistanceBroken;
                LocalVersusPerspectiveHit hit = view.Hit(match.ResolveNextHit());
                arena.ObserveCombatContact();
                arena.PresentWeaponTrailContact(hit.HitIndex, hit.PlayerAttacked, hit.EnemyAttacked);
                // The queue panels' clash motion reads the hit now: a finishing blow completes its slot before the HUD's frame.
                battleHud.ObserveClash(view);
                // The director rates the slot again: its weight never drops within the slot.
                ShowSlotWeight(slot.SlotIndex, showDirector.Observe(view.CurrentSlotFacts));
                if (hit.PlayerAttacked) playerSlotDamage = hit.EnemyDisplayedDamage;
                if (hit.EnemyAttacked) enemySlotDamage = hit.PlayerDisplayedDamage;
                bool enemyBrokeNow = enemyResistanceBefore > 0 && view.Enemy.Resistance <= 0;
                bool playerBrokeNow = playerResistanceBefore > 0 && view.Player.Resistance <= 0;
                bool decisiveNow = false;
                if (hit.PlayerAttacked)
                    decisiveNow |= PresentHit(true, hit.EnemyHealthDamage, hit.EnemyResistanceDamage,
                        hit.EnemyDisplayedDamage, hit.EnemyPushPower,
                        hit.EnemySkill?.Kind == LegacySkillKind.Defence,
                        enemyBrokeNow,
                        enemyHealthBefore > 0 && view.Enemy.Health <= 0,
                        DuelPrototypeController.Exchange(hit.EnemySkill, hit.EnemyAttacked), hit.EnemyDamageReasons, hit.PlayerSkill);
                if (hit.EnemyAttacked)
                    decisiveNow |= PresentHit(false, hit.PlayerHealthDamage, hit.PlayerResistanceDamage,
                        hit.PlayerDisplayedDamage, hit.PlayerPushPower,
                        hit.PlayerSkill?.Kind == LegacySkillKind.Defence,
                        playerBrokeNow,
                        playerHealthBefore > 0 && view.Player.Health <= 0,
                        DuelPrototypeController.Exchange(hit.PlayerSkill, hit.PlayerAttacked), hit.PlayerDamageReasons, hit.EnemySkill);
                AnnounceBreaks(playerWasBroken, enemyWasBroken, hit.EnemyAttacked, hit.PlayerAttacked);
                // 간파: the viewer's own read paid off (each screen cues its own), once per slot.
                CueRead(slot.SlotIndex);
                if (hit.Outcome == DuelMatchOutcome.InProgress)
                {
                    // A Major slot's first presented hit: a longer stop and a second impact under it.
                    if (hit.PlayerAttacked || hit.EnemyAttacked) PresentMajorMoment(decisiveNow);
                    // A Minor slot (no attack) plays its rest after its last hit faster.
                    if (slot.IsResolved && showDirector.ShortensSlot)
                    {
                        slotDuration = attackTime + showDirector.ShortenSlotRest(Mathf.Max(0f, slotDuration - attackTime));
                        battleHud.SetCurrentSlotDuration(slotDuration);
                    }
                }
                if (hit.Outcome != DuelMatchOutcome.InProgress)
                {
                    finale.Begin(settings, hit.Outcome, false, hitStopRemaining);
                    // Finish this strike's pose without allowing another attack cycle.
                    finishingHitPlayback = true;
                    finishingHitTime = attackTime;
                    finishingClipEnd = Mathf.Max(attackTime, animationClipEnd - .0001f);
                    slotDuration = animationClipEnd;
                    battleHud.SetCurrentSlotDuration(slotDuration);
                    arena.HoldSlotAtTime(attackTime);
                    CompleteSlot(slot);
                    return;
                }
                // A long frame must not batch several impacts before any push has been observed, nor immediately finish
                // the final animation.
                if (phaseTime >= animationClipEnd)
                {
                    phaseTime = attackTime;
                    arena.HoldSlotAtTime(phaseTime);
                }
            }
            if (phaseTime >= slotDuration && slot.IsResolved) CompleteSlot(slot);
        }

        private void CompleteSlot(LocalVersusPerspectiveSlot slot)
        {
            battleHud.RecordResolvedSlot(slot.PlayerSkill, slot.EnemySkill, playerSlotDamage, enemySlotDamage);
            match.CompleteCurrentSlot();
            showDirector.EndSlot();
            nextSlotIndex = slot.SlotIndex + 1;
            PrepareSlot(true);
        }

        // The director takes this turn's numbers once per resolution (symmetric: both screens rate every slot alike).
        private void EnsureDirectorTurn()
        {
            if (match == null || ReferenceEquals(directorMatch, match) && directorRound == match.RoundNumber) return;
            directorMatch = match;
            directorRound = match.RoundNumber;
            bladeBindRemaining = 0f;
            bladeBindHitPending = false;
            showDirector.BeginTurn(settings.ShowPacing, settings.SlotImportanceTuning(true), settings.UnopposedStreak,
                settings.CombatTempo);
        }

        private void ClearShowDirector()
        {
            showDirector.Clear();
            directorMatch = null;
            directorRound = -1;
            bladeBindRemaining = 0f;
            bladeBindHitPending = releasingBladeBindHit = false;
            bladeExchange.Clear();
            arena.ClearBladeBind();
        }

        private void ShowSlotWeight(int slotIndex, LegacySlotRank rank)
            => battleHud.SetClashEmphasis(slotIndex, showDirector.Enabled && rank.Tier == LegacySlotTier.Major
                ? settings.MajorClashScale : 1f);

        private float DirectorCameraPush
        {
            get
            {
                if (!IsResolving) return 0f;
                float push = settings.MajorCameraPush;
                bool slotPlays = viewPhase == ViewPhase.PlayingSlot;
                float major = showDirector.Enabled && slotPlays && showDirector.IsMajor ? push : 0f;
                // A signature move (either side, so both screens alike) comes closer still.
                if (slotPlays && showDirector.IsSignature && settings.SignatureTreatmentEnabled)
                    major = Mathf.Max(major, settings.SignatureCameraPush);
                return bladeBindRemaining > 0f ? Mathf.Max(major, push * BladeBindCameraShare) : major;
            }
        }

        // 간파: the viewer's read paid off in this slot (the director keeps it to once per slot).
        private void CueRead(int slotIndex)
        {
            if (showDirector.TakeReadCue() == LegacyReadEvent.None || !settings.ReadCueEnabled) return;
            battleHud.ShowReadCue(slotIndex);
            if (settings.ReadFighterAccent) arena.AccentFighter(true, settings.ReadCueInk);
        }

        private void PresentMajorMoment(bool decisive)
        {
            bool before = showDirector.MajorMomentGiven;
            float stop = showDirector.HitStop(settings.HitStopDuration);
            if (before || !showDirector.MajorMomentGiven) return;
            hitStopRemaining = Mathf.Max(hitStopRemaining, stop);
            if (!decisive) art.PlayClash(effects, false, UnityEngine.Random.value < .5f, settings.MajorImpactVolume);
            // A signature move rings once more (either side's: both screens alike).
            if (showDirector.IsSignature && settings.SignatureTreatmentEnabled)
                art.PlayClash(effects, true, false, settings.SignatureImpactVolume);
        }

        private bool TryBeginBladeBind(int slotIndex, float attackTime)
        {
            if (!showDirector.Enabled || !showDirector.IsBladeBindCandidate || !match.CanBindCurrentHit()) return false;
            PresentMajorMoment(false);
            float seconds = showDirector.BladeBind();
            if (!(seconds > 0f)) return false;
            phaseTime = attackTime;
            arena.HoldSlotAtTime(attackTime);
            hitStopRemaining = Mathf.Max(hitStopRemaining, settings.HitStopDuration);
            bladeBindHitPending = true;
            bladeBindRemaining = seconds;
            arena.PresentBladeBindContact();
            arena.BeginBladeBind(seconds, settings.BladeBindSway, settings.BladeBindPress, settings.BladeBindSpring,
                settings.BladeBindSparks);
            battleHud.ObserveClash(view);
            battleHud.BindClash(slotIndex, seconds, settings.BladeBindSparks);
            art.PlayClash(effects, true, false, .4f);
            return true;
        }

        private float AdvanceBladeBind(float available)
        {
            if (!(bladeBindRemaining > 0f)) return 0f;
            float bound = Mathf.Clamp(available, 0f, bladeBindRemaining);
            bladeBindRemaining -= bound;
            bool released = bladeBindRemaining <= 1e-5f && bladeBindHitPending;
            if (released)
            {
                bladeBindHitPending = false;
                releasingBladeBindHit = true;
                // Both perspectives commit their hit at release, before the arena shares the queued pushes.
                try { PlaySlot(0f); }
                finally { releasingBladeBindHit = false; }
            }
            arena.AdvanceBladeBind(bound);
            battleHud.AdvanceClashBind(bound);
            if (bladeBindRemaining <= 1e-5f)
            {
                bladeBindRemaining = 0f;
                arena.FinishBladeBind();
                battleHud.FinishClashBind();
                art.PlayClash(effects, true, true, .35f);
            }
            // Keep one rendered release before another impact, even if this frame supplied excess time.
            return released ? Mathf.Max(bound, available) : bound;
        }

        // Returns whether the hit was decisive (a break, the finishing blow, a heavy hit).
        private bool PresentHit(bool playerAttacks, int healthDamage, int resistanceDamage,
            int displayedDamage, int pushPower, bool guarded, bool resistanceBroke, bool finishingBlow,
            LegacyArenaView.HitExchange exchange, LegacyDamageReason damageReasons, LegacySkill attackingSkill)
        {
            // Only decisive moments get the slow close-up, tilt and critical sound: a break, the finishing blow, or one hit
            // taking a set share of the target's maximum health. A merely big number is still gold and larger.
            bool decisive = LegacyDecisiveHit.IsDecisive(resistanceBroke, finishingBlow, healthDamage,
                (playerAttacks ? view.Enemy : view.Player).MaxHealth, settings.DecisiveHealthDamagePercent);
            bool emphasised = decisive || displayedDamage >= 12;
            // A resistance-only hit displays exactly its resistance loss; anything more reached the body.
            bool bodyHit = healthDamage > 0 || displayedDamage > resistanceDamage;
            if (bodyHit) exchange = LegacyArenaView.HitExchange.None;
            bool resistanceNumber = !bodyHit && resistanceDamage > 0 && displayedDamage == resistanceDamage;
            bool mixedResources = bodyHit && resistanceDamage > 0 && !resistanceBroke;
            // Both sides of a simultaneous clash share the same real-time stop.
            if (!releasingBladeBindHit)
                hitStopRemaining = Mathf.Max(hitStopRemaining, settings.HitStopDuration);
            arena.PresentHit(playerAttacks, healthDamage, resistanceDamage, guarded,
                emphasised && !releasingBladeBindHit, pushPower, exchange, decisive);
            if (decisive) battleHud.FatalAttack(playerAttacks);
            Transform target = playerAttacks ? arena.EnemyRenderer.transform : arena.PlayerRenderer.transform;
            battleHud.ShowHitDamage(!playerAttacks, displayedDamage, target.position, emphasised, resistanceNumber,
                damageReasons, resistanceBroke, attackingSkill, mixedResources);
            effects.pitch = resistanceBroke ? (playerAttacks ? .96f : .82f) : UnityEngine.Random.Range(.75f, 1.25f);
            art.PlayClash(effects, false, UnityEngine.Random.value >= .5f);
            if (decisive && criticalSound != null)
                effects.PlayOneShot(criticalSound, resistanceBroke ? .72f : .55f);
            return decisive;
        }

        /// <summary>Calls out every fighter whose resistance broke during the last rules step (a hit or a slot's opening
        /// effect) and updates the break aura.</summary>
        private void AnnounceBreaks(bool playerWasBroken, bool enemyWasBroken,
            bool playerHitPresented = false, bool enemyHitPresented = false)
        {
            bool playerBroke = !playerWasBroken && view.Player.IsResistanceBroken;
            bool enemyBroke = !enemyWasBroken && view.Enemy.IsResistanceBroken;
            // Cancellation survives a recovery by the already-started opposing skill.
            battleHud.PlayBreakQueueCancellation(true, view.PlayerBreakCanceledFromSlotIndex);
            battleHud.PlayBreakQueueCancellation(false, view.EnemyBreakCanceledFromSlotIndex);
            if (playerBroke)
            {
                battleHud.ShowBreakCallout(true, arena.PlayerRenderer.transform.position, arena.PlayerRenderer.transform);
                breakImpactCue.Show(true);
            }
            if (enemyBroke)
            {
                battleHud.ShowBreakCallout(false, arena.EnemyRenderer.transform.position, arena.EnemyRenderer.transform);
                breakImpactCue.Show(false);
            }
            // A direct effect has no impact animation to play the decisive sound.
            bool needsBreakSound = (playerBroke && !playerHitPresented) || (enemyBroke && !enemyHitPresented);
            if (needsBreakSound) arena.PresentDecisiveFocus(!playerBroke);
            if (needsBreakSound && criticalSound != null)
            {
                effects.pitch = playerBroke ? .82f : .96f;
                effects.PlayOneShot(criticalSound, .72f);
            }
            arena.SetResistanceBroken(view.Player.IsResistanceBroken, view.Enemy.IsResistanceBroken);
        }

        private void EndRound() => SetViewPhase(ViewPhase.AfterTurn);

        private void StartPlanning()
        {
            int playerResistanceBefore = view.Player.Resistance;
            int enemyResistanceBefore = view.Enemy.Resistance;
            int[] temporaryBefore = LegacyKeywordPreview.TemporaryCounts(view);
            match.BeginNextTurn();
            ResetClocks();
            holds.Clear(battleHud);
            highlightedSlot = -1;
            inspectingEnemy = 0;
            arena.BeginTurn();
            battleHud.BeginTurn();
            activationCue.Reset();
            // 제한시간: the viewer's own clock starts later by what its skills earned last turn. Its '+N초' waits until the
            // timer shows that clock (RefreshHud).
            pendingTimeGained = Mathf.Max(0f, turnClocks[onlineLocalPlayer] - PlanningSeconds);
            for (int lane = 0; lane < temporaryBefore.Length; lane++)
                if (temporaryBefore[lane] > 0) battleHud.ShowLanePop(lane, LegacyKeywordPreview.TemporaryExpired, false);
            // The automatic refill, a full turn after the one in which resistance broke.
            if (view.Player.Resistance > playerResistanceBefore)
                battleHud.ShowRecoveryCallout(true, arena.PlayerRenderer.transform.position, arena.PlayerRenderer.transform);
            if (view.Enemy.Resistance > enemyResistanceBefore)
                battleHud.ShowRecoveryCallout(false, arena.EnemyRenderer.transform.position, arena.EnemyRenderer.transform);
            arena.SetResistanceBroken(view.Player.IsResistanceBroken, view.Enemy.IsResistanceBroken);
            SetViewPhase(ViewPhase.Planning);
        }

        /// <summary>The match is over and shown: the single-play result card from the viewer's side over the battle's last
        /// picture (after the finishing blow's freeze frame, or the last turn's return).</summary>
        private void ShowResult()
        {
            if (!match.IsFinished) return;
            arena.ClearLowHealthHeartbeat();
            arena.ClearSlowMotionAfterimages();
            finale.Clear();
            effects.Stop();
            ClearCues();
            holds.Clear(battleHud);
            hitStopRemaining = 0f;
            ClearShowDirector();
            inspecting = false;
            battleHud.ClearSkillFeedback();
            battleHud.HideExplanation();
            battleHud.Root.SetActive(false);
            hud.SetBattleVisible(false);
            SetViewPhase(ViewPhase.Finished);
            resultHud.ShowVersus(view.Outcome, MatchTitle, match.RoundNumber, view.Player.Health, view.Enemy.Health);
        }

        private void RefreshHud(float delta, float realDelta)
        {
            if (match == null || view == null) return;
            bool planning = IsPlanningShown;
            if (planning)
            {
                shownClock = clocks[match.CurrentPlanner];
                shownTurnClock = turnClocks[match.CurrentPlanner];
                // The time the viewer's skills earned rises off the timer only over the viewer's own clock.
                if (pendingTimeGained > 0f && match.CurrentPlanner == onlineLocalPlayer)
                {
                    battleHud.ShowTimeGained(pendingTimeGained);
                    pendingTimeGained = 0f;
                }
            }
            // Only the viewer's own turn takes input: the opponent's turn, a request waiting for the host and the exit
            // question hold the dock.
            battleHud.SetInputsHeld(!view.IsMyTurn || pendingOnlineRequest || hud.IsExitConfirming);
            // 넘기기 is free in versus; a reservation's 제한시간 is paid from the viewer's own clock, which always runs.
            battleHud.SetLaneCycleCost(0f, true);
            battleHud.SetReservationClock(clocks[view.LocalPlayer], true);
            arena.SetResistanceBroken(view.Player.IsResistanceBroken, view.Enemy.IsResistanceBroken);
            battleHud.SetCombatLetterboxAccent(arena.CombatLetterboxAccent);
            battleHud.Refresh(view, shownClock, IsResolving, highlightedSlot, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform, delta, realDelta, shownTurnClock);
            resistanceFeedback.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            // Reproject after the arena's pose and camera update without advancing the cue a second time.
            breakImpactCue.Tick(0f, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            activationCue.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            if (inspecting && view.EnemyQueue.Count > 0)
            {
                int slot = Mathf.Clamp(inspectingEnemy, 0, view.EnemyQueue.Count - 1);
                battleHud.SetInspectedSlot(slot);
                battleHud.ShowQueueComparison(view.EnemyQueue[slot],
                    slot < view.PlayerQueue.Count ? view.PlayerQueue[slot] : null);
            }
            else if (planning && holds.ExplainedSkill != null) battleHud.ShowExplanation(holds.ExplainedSkill, false);
            else battleHud.HideExplanation();
            hud.Refresh(match, view.LocalPlayer, pendingOnlineRequest);
        }

        private void ClearCues()
        {
            activationCue.Reset();
            resistanceFeedback.Reset();
            breakImpactCue.Reset();
        }

        private void Exit()
        {
            if (active) exitToTitle();
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            activationCue.Dispose();
            resistanceFeedback.Dispose();
            breakImpactCue.Dispose();
            startCard.Dispose();
            finale.Dispose();
            resultHud.Dispose();
            hud.Dispose();
            battleHud.Dispose();
            if (audioRoot != null) UnityEngine.Object.Destroy(audioRoot);
            if (ownedSettings != null) UnityEngine.Object.Destroy(ownedSettings);
        }
    }
}
