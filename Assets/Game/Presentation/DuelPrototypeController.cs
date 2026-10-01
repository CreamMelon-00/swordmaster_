using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Dialogue;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TurnLimbo.Presentation
{
    public sealed class DuelPrototypeController : MonoBehaviour
    {
        private const float PlanningDuration = 10f;
        private const float OriginalClipDuration = 1f / 6f;
        private const float OriginalAttackEventTime = 1f / 12f;
        public const float TurnCleanupDelay = 0.12f;
        [SerializeField] private DuelPresentationSettings presentationSettings;
        private enum ViewPhase { Planning, Approaching, ClosingDistance, SkillWindup, PlayingSlot, BetweenSlots, AfterTurn, Settling, Outcome }
        private readonly float[] holdTimes = new float[3];
        private readonly bool[] holdConsumed = new bool[3];
        private LegacyQueuedDuel session;
        private LegacyDuelArt art;
        private LegacyArenaView arena;
        private LegacyCombatHud hud;
        private DuelStepHud stepHud;
        private DuelStepAudio stepAudio;
        private DuelResistanceFeedback resistanceFeedback;
        private DialogueHud dialogueHud;
        private DialogueSession dialogueSession;
        private DialoguePortraitCatalog defaultDialoguePortraitCatalog;
        private DialoguePortraitCatalog activeDialoguePortraitCatalog;
        private CampaignRun campaign;
        private CampaignLobbyHud lobbyHud;
        private BattleResultHud resultHud;
        private MissionCoachHud coachHud;
        private MissionBriefingHud briefingHud;
        private TitleHud titleHud;
        private bool showingTitle;
        private GameSaveStore saveStore;
        private BattleResult battleResult;
        private PrologueRun prologue;
        // The opening-arc mission being fought, its coach, and whether its briefing is on screen.
        private PrologueMission mission;
        private MissionGuide guide;
        private bool showingBriefing;
        private float guideInspectionRemaining;
        // Runs after the player finishes or skips the open dialogue; cleanup closes never run it.
        private System.Action dialogueContinuation;
        private int dialogueOpenedFrame = -1;
        private AudioSource effectsSource;
        private AudioClip criticalSound;
        private ViewPhase viewPhase;
        private float phaseTime;
        private float slotDuration;
        // A pending player counter defers the slot's initial effects to its first hit.
        private bool slotStartDeferred;
        private float slotPlaybackSpeed = 1f;
        private float slotAttackInterval;
        private float slotImpactTime;
        private float slotCycleDuration;
        private float slotAnticipationDuration;
        private float slotStepWindow;
        // Snapshotted with the base window at slot start, so live tuning never changes a skill mid-cue.
        private float slotStepWindowDecay = LegacyStepTiming.DefaultDecay;
        private float slotStepMinimumWindow = LegacyStepTiming.DefaultMinimumWindow;
        private float betweenSlotsDuration;
        private float hitStopRemaining;
        private float planningTime;
        private int highlightedSlot;
        private int inspectingEnemy;
        private int playerSlotDamage, enemySlotDamage;
        private LegacySkill explainedSkill;
        private int explainedLane = -1;
        private DuelPresentationSettings ownedPresentationSettings;

        public LegacyQueuedDuel Session => session;
        public CampaignRun Campaign => campaign;
        public CampaignLobbyHud LobbyHud => lobbyHud;
        public BattleResultHud ResultHud => resultHud;
        public MissionCoachHud CoachHud => coachHud;
        public MissionBriefingHud BriefingHud => briefingHud;
        public TitleHud TitleHud => titleHud;
        /// <summary>The auto-save file. Tests may point it elsewhere before using the title.</summary>
        public GameSaveStore SaveStore
        {
            get => saveStore;
            set => saveStore = value ?? throw new System.ArgumentNullException(nameof(value));
        }
        /// <summary>Whether progress is written to <see cref="SaveStore"/>. Only the title's 이어하기/새 게임 turn it on,
        /// so tests and direct API use never touch the player's save.</summary>
        public bool AutoSaveEnabled { get; private set; }
        /// <summary>Whether stage battles follow the story's unlocks (lanes, 숨고르기, steps) and stage cap. Like
        /// auto-save, only the title's 이어하기/새 게임 turn it on, so tests and direct API use keep every feature.</summary>
        public bool StoryProgressionEnabled { get; private set; }
        /// <summary>Whether the next story mission can be played now (its stage, if any, is cleared).</summary>
        public bool IsNextMissionAvailable => prologue.CanPlayCurrent(campaign.IsStageCleared);
        public bool IsInTitle => showingTitle && !IsShowingDialogue && titleHud != null && titleHud.IsVisible;
        public DialogueHud DialogueHud => dialogueHud;
        public DialogueLine CurrentDialogueLine => dialogueSession?.Current;
        public BattleResult Result => battleResult;
        public PrologueRun Prologue => prologue;
        public PrologueMission ActiveMission => mission;
        public MissionGuide Guide => guide;
        public bool IsMission => mission != null;
        public bool IsShowingResult => viewPhase == ViewPhase.Outcome && battleResult != null;
        public bool IsShowingDialogue => dialogueSession != null && dialogueHud != null && dialogueHud.IsVisible;
        public bool IsInBriefing => showingBriefing && !showingTitle && !IsShowingDialogue && viewPhase == ViewPhase.Outcome &&
            !IsShowingResult;
        public bool IsInLobby => !IsShowingDialogue && viewPhase == ViewPhase.Outcome && !IsShowingResult &&
            !IsMission && !showingBriefing && !showingTitle && campaign.Phase == CampaignPhase.Lobby;
        public DuelPresentationSettings PresentationSettings => presentationSettings;
        public LegacyArenaView ArenaView => arena;
        public LegacyCombatHud Hud => hud;
        public DuelStepHud StepHud => stepHud;
        public bool IsResolving => viewPhase != ViewPhase.Planning && viewPhase != ViewPhase.Outcome;
        public bool CanChoose => viewPhase == ViewPhase.Planning && session.Phase == LegacyDuelPhase.Planning;
        public int PlayerHealth => session.Player.Health;
        public int EnemyHealth => session.Enemy.Health;
        public DuelMatchOutcome Outcome => session.Outcome;
        public float TurnTimeRemaining => planningTime;
        public bool IsBetweenSlots => viewPhase == ViewPhase.BetweenSlots;
        public float HitStopTimeRemaining => hitStopRemaining;
        public float ActiveSlotPlaybackSpeed => slotPlaybackSpeed;
        public float ActiveSlotAttackInterval => slotAttackInterval;
        public float ActiveSlotDuration => slotDuration;
        public float ActiveSlotElapsedTime => viewPhase == ViewPhase.PlayingSlot ? phaseTime : 0f;
        public bool IsSkillWindup => viewPhase == ViewPhase.SkillWindup;
        /// <summary>The steps the player may take now: what the duel allows, minus what a coached beat holds back.</summary>
        public CombatFeature StepFeatures
        {
            get
            {
                if (session == null) return CombatFeature.None;
                CombatFeature features = session.Features;
                if (guide != null && !guide.AllowsStep(LegacyStepAction.Dodge)) features &= ~CombatFeature.Dodge;
                if (guide != null && !guide.AllowsStep(LegacyStepAction.Pressure)) features &= ~CombatFeature.Pressure;
                return features;
            }
        }
        public bool CanStep => session != null && StepFeatures.AllowsAnyStep() && session.Phase == LegacyDuelPhase.Resolving &&
            (viewPhase == ViewPhase.ClosingDistance || viewPhase == ViewPhase.SkillWindup ||
             viewPhase == ViewPhase.PlayingSlot || viewPhase == ViewPhase.BetweenSlots);
        private float TimeUntilFirstImpact => viewPhase == ViewPhase.SkillWindup
            ? slotAnticipationDuration - phaseTime + slotImpactTime : slotImpactTime - phaseTime;
        public bool IsStepTimingWindow => CanStep && session.CurrentSlot != null &&
            session.CurrentSlot.HitsResolved == 0 &&
            (viewPhase == ViewPhase.SkillWindup || viewPhase == ViewPhase.PlayingSlot) &&
            TimeUntilFirstImpact >= 0f && TimeUntilFirstImpact <= CurrentStepWindow &&
            (arena.IsInRange || session.CurrentSlot.DodgeSucceeded || session.CurrentSlot.PressureSucceeded);
        public float StepCueProgress => session?.CurrentSlot == null ? 0f : session.CurrentSlot.HitsResolved > 0 ? 1f :
            Mathf.Clamp01(1f - TimeUntilFirstImpact / Mathf.Max(.001f, slotAnticipationDuration + slotImpactTime));
        public float StepWindowFraction => CurrentStepWindow / Mathf.Max(.001f, slotAnticipationDuration + slotImpactTime);
        /// <summary>The success window for the next attempt: it narrows with each attempt this turn.</summary>
        public float CurrentStepWindow => LegacyStepTiming.Window(slotStepWindow, session?.StepAttemptsThisTurn ?? 0,
            slotStepWindowDecay, slotStepMinimumWindow);
        public bool HasRequiredArt => art != null && art.HasRequiredAssets &&
            arena != null && arena.HasRequiredAssets && hud != null && hud.HasRequiredAssets &&
            lobbyHud != null && lobbyHud.HasRequiredAssets;
        private bool IsInspecting => CanChoose && (guideInspectionRemaining > 0f ||
            Keyboard.current != null && Keyboard.current.tabKey.isPressed);
        // A coached beat never runs out of time; a mission may also have no timer at all.
        private bool PlanningTimerRuns => !IsMission || mission.PlanningTimer && (guide == null || guide.IsFree);

        private void Awake()
        {
            campaign = new CampaignRun();
            prologue = new PrologueRun();
            art = new LegacyDuelArt();
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.spatialBlend = 0f;
            if (Object.FindAnyObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            criticalSound = Resources.Load<AudioClip>("LegacyDuel/Audio/Critical");
            defaultDialoguePortraitCatalog = Resources.Load<DialoguePortraitCatalog>(DialoguePortraitCatalog.ResourcePath);
            if (presentationSettings == null)
                presentationSettings = Resources.Load<DuelPresentationSettings>("DuelPresentationSettings");
            if (presentationSettings == null)
            {
                ownedPresentationSettings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                presentationSettings = ownedPresentationSettings;
            }
            arena = LegacyArenaView.Create(transform, art, presentationSettings);
            hud = new LegacyCombatHud(transform, art, lane => QueueLane(lane), CommitTurn, RestartMatch,
                presentationSettings, () => QueueBreath(), () => CycleLanes());
            stepHud = new DuelStepHud(hud.Root.transform, art, presentationSettings);
            stepAudio = new DuelStepAudio(transform);
            resistanceFeedback = new DuelResistanceFeedback(hud.Root.transform, art.UIFont);
            dialogueHud = new DialogueHud(transform, art, () => ContinueDialogue(), () => FinishDialogue());
            lobbyHud = new CampaignLobbyHud(transform, art,
                id => SelectCurriculumNode(id), () => ResetCurriculum(), id => EquipSkill(id),
                id => UnequipSkill(id), (id, direction) => MoveEquippedSkill(id, direction),
                stage => StartCampaignStage(stage), RestartJourney,
                (id, lane, slot) => PlaceLoadoutSkill(id, lane, slot),
                () => SaveLoadout(), () => ResetLoadout(), () => OpenNextMission());
            resultHud = new BattleResultHud(transform, art, () => DismissBattleResult(),
                () => RetryBattleResult(), () => AdvanceFromBattleResult());
            coachHud = new MissionCoachHud(transform, art, () => AdvanceGuide(),
                ReturnToLobby, () => InspectGuideEnemy());
            briefingHud = new MissionBriefingHud(transform, art, () => StartMission(), () => LeaveBriefing());
            titleHud = new TitleHud(transform, art, () => ContinueGame(), () => NewGameFromTitle());
            saveStore = new GameSaveStore(GameSaveStore.DefaultPath);
            session = campaign.CreateDuel(System.Environment.TickCount);
            ResetBattlePresentation();
            lobbyHud.ResetView();
            ShowTitle();
        }

        private void Update()
        {
            AdvancePresentation(Time.unscaledDeltaTime, Keyboard.current);
        }

        private void AdvancePresentation(float realDelta, Keyboard keyboard)
        {
            realDelta = Mathf.Max(0f, realDelta);
            if (IsShowingDialogue)
            {
                if (Time.frameCount > dialogueOpenedFrame && keyboard != null)
                {
                    // Escape skips the rest of the scene; the mission flow still continues.
                    if (keyboard.escapeKey.wasPressedThisFrame) FinishDialogue();
                    else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                        ContinueDialogue();
                }
                // Never let the key that advances or closes dialogue reach the lobby or combat in the same frame.
                return;
            }
            if (IsShowingResult)
            {
                if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                    DismissBattleResult();
                return;
            }
            if (IsInTitle)
            {
                // Enter continues when a save exists and otherwise starts a new game; the confirmation needs a click.
                if (keyboard != null)
                {
                    if (titleHud.IsConfirming)
                    {
                        if (keyboard.escapeKey.wasPressedThisFrame) titleHud.CancelConfirm();
                    }
                    else if (keyboard.enterKey.wasPressedThisFrame)
                    {
                        if (titleHud.CanContinue) ContinueGame();
                        else titleHud.PressNewGame();
                    }
                }
                return;
            }
            if (IsInBriefing)
            {
                if (keyboard != null && keyboard.enterKey.wasPressedThisFrame) StartMission();
                else if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) LeaveBriefing();
                return;
            }
            if (IsInLobby)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) lobbyHud.ShowTab(LobbyTab.Home);
                else if (keyboard != null && keyboard.enterKey.wasPressedThisFrame && lobbyHud.CurrentTab == LobbyTab.Stages)
                    StartCampaignStage(lobbyHud.SelectedStageNumber);
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ReturnToLobby();
                return;
            }
            guideInspectionRemaining = Mathf.Max(0f, guideInspectionRemaining - realDelta);
            if (guide != null && guide.CanAdvance && keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                AdvanceGuide();
                return;
            }
            // Judge the cue the player saw, before this frame advances or resolves its impact.
            if (CanStep && keyboard != null)
            {
                // Keys pressed together share the window shown on this frame; each still
                // counts as an attempt and narrows the window for later frames.
                bool timing = IsStepTimingWindow;
                if (keyboard.aKey.wasPressedThisFrame) TryStep(LegacyStepAction.Dodge, timing, out _);
                if (keyboard.dKey.wasPressedThisFrame) TryStep(LegacyStepAction.Pressure, timing, out _);
            }
            // Slow motion belongs to decisive moments only: a break or a finishing blow, and step successes.
            float speed = IsInspecting ? 0.2f : IsResolving && arena.IsFatalFocus ? LegacyArenaView.DecisiveSlowMotionScale : 1f;
            // Keep local cinematic slow motion on the established combat clock;
            // the step gesture, camera and feedback lifetime still use real time.
            if (IsResolving) speed = Mathf.Min(speed, arena.StepPresentationSpeed);
            float stoppedTime = Mathf.Min(realDelta, hitStopRemaining);
            hitStopRemaining = Mathf.Max(0f, hitStopRemaining - stoppedTime);
            float delta = (realDelta - stoppedTime) * speed;
            arena.SetPlanningState(planningTime, IsInspecting);
            arena.Tick(delta, realDelta);
            phaseTime += delta;
            switch (viewPhase)
            {
                case ViewPhase.Planning:
                    if (keyboard != null) ReadPlanningInput(keyboard);
                    if (CanChoose && PlanningTimerRuns)
                    {
                        planningTime = Mathf.Max(0f, planningTime - delta);
                        if (planningTime <= 0f) CommitTurn();
                    }
                    break;
                case ViewPhase.Approaching:
                    if (arena.ApproachComplete)
                    {
                        hud.BeginCombat();
                        PrepareNextSlot();
                    }
                    break;
                case ViewPhase.ClosingDistance:
                    if (!arena.IsPursuing) arena.CloseDistance(delta);
                    if (arena.IsInRange) BeginSlotAnimation();
                    break;
                case ViewPhase.SkillWindup:
                    arena.HoldSlotAtTime(0f);
                    if (phaseTime >= slotAnticipationDuration) SetViewPhase(ViewPhase.PlayingSlot);
                    break;
                case ViewPhase.PlayingSlot:
                    UpdateSlot(delta);
                    break;
                case ViewPhase.BetweenSlots:
                    if (phaseTime >= betweenSlotsDuration) PrepareNextSlot();
                    break;
                case ViewPhase.AfterTurn:
                    if (phaseTime >= TurnCleanupDelay)
                    {
                        hud.BeginReturn();
                        arena.EndTurn();
                        SetViewPhase(ViewPhase.Settling);
                    }
                    break;
                case ViewPhase.Settling:
                    if (arena.ReturnComplete)
                    {
                        if (session.IsFinished)
                        {
                            FinishStage();
                        }
                        else StartPlanning();
                    }
                    break;
                case ViewPhase.Outcome:
                    break;
            }
            arena.TickPressureAttackTrail(realDelta, viewPhase == ViewPhase.PlayingSlot);
            if (viewPhase != ViewPhase.Outcome)
            {
                stepHud.Tick(realDelta);
                RefreshHudClock(delta, realDelta);
            }
        }

        private void PrepareNextSlot(bool waitBetweenSlots = false)
        {
            if (session.IsFinished || session.IsTurnResolved)
            {
                SetViewPhase(ViewPhase.AfterTurn);
                return;
            }
            highlightedSlot = session.LastResolvedSlot + 1;
            if (waitBetweenSlots)
            {
                betweenSlotsDuration = Mathf.Max(0f, presentationSettings.SkillInterval);
                if (betweenSlotsDuration > 0f)
                {
                    SetViewPhase(ViewPhase.BetweenSlots);
                    return;
                }
            }
            SetViewPhase(ViewPhase.ClosingDistance);
            if (arena.IsInRange) BeginSlotAnimation();
        }

        // Uses the playback speed and attack gap snapshotted when the slot began.
        private float SlotDurationFor(int hits)
        {
            float clipDuration = OriginalClipDuration / slotPlaybackSpeed;
            return clipDuration * hits + slotAttackInterval * (hits - 1) + 0.01f;
        }

        private void BeginSlotAnimation()
        {
            int playerResistanceBefore = session.Player.Resistance;
            int enemyResistanceBefore = session.Enemy.Resistance;
            bool playerWasBroken = session.Player.IsResistanceBroken;
            bool enemyWasBroken = session.Enemy.IsResistanceBroken;
            LegacyCurrentSlot slot = session.BeginNextSlot();
            highlightedSlot = slot.SlotIndex;
            slotPlaybackSpeed = Mathf.Max(0.01f, presentationSettings.AnimationPlaybackSpeed);
            slotAttackInterval = Mathf.Max(0f, presentationSettings.AttackInterval);
            float clipDuration = OriginalClipDuration / slotPlaybackSpeed;
            slotImpactTime = OriginalAttackEventTime / slotPlaybackSpeed;
            slotCycleDuration = clipDuration + slotAttackInterval;
            slotAnticipationDuration = StepFeatures.AllowsAnyStep() ? presentationSettings.StepAnticipationDuration : 0f;
            slotStepWindow = presentationSettings.StepTimingWindow;
            slotStepWindowDecay = presentationSettings.StepWindowDecay;
            slotStepMinimumWindow = presentationSettings.StepMinimumWindow;
            slotDuration = SlotDurationFor(slot.HitCount);
            playerSlotDamage = enemySlotDamage = 0;
            arena.ConfigureSlotTiming(slotPlaybackSpeed, slotAttackInterval);
            // A pending player counter plays from the windup; a dodge attempt withdraws it.
            LegacySkill playerAction = slot.PlayerSkill ?? slot.PendingPlayerCounter;
            slotStartDeferred = slot.PendingPlayerCounter != null;
            arena.BeginSlot(playerAction, slot.EnemySkill);
            hud.SetCurrentSkills(playerAction, slot.EnemySkill, slotDuration + slotAnticipationDuration);
            hud.SetSkillFeedback(slot);
            if (slot.EnemyCountered) hud.ShowCounterCallout(false, arena.EnemyRenderer.transform.position);
            // Report the actual capped change without treating recovery or
            // direct resistance loss as an animation hit or knockback.
            resistanceFeedback.Show(true, session.Player.Resistance - playerResistanceBefore);
            resistanceFeedback.Show(false, session.Enemy.Resistance - enemyResistanceBefore);
            AnnounceBreaks(playerWasBroken, enemyWasBroken);
            SetViewPhase(slotAnticipationDuration > 0f ? ViewPhase.SkillWindup : ViewPhase.PlayingSlot);
        }

        private void UpdateSlot(float delta)
        {
            LegacyCurrentSlot slot = session.CurrentSlot;
            float attackTime = slotImpactTime + slotCycleDuration * slot.HitsResolved;
            float animationClipEnd = slotCycleDuration * slot.HitsResolved + OriginalClipDuration / slotPlaybackSpeed;
            if (!slot.IsResolved && phaseTime >= attackTime)
            {
                bool firstStepImpact = slot.HitsResolved == 0 && (slot.DodgeSucceeded || slot.PressureSucceeded);
                if (!arena.IsInRange && !firstStepImpact)
                {
                    // Movement keeps running while this contact frame waits.
                    // Pausing the slot clock prevents off-screen air strikes or
                    // several deferred hits firing together after a long chase.
                    if (!arena.IsPursuing) arena.CloseDistance(delta);
                    if (!arena.IsInRange)
                    {
                        phaseTime = attackTime;
                        arena.HoldSlotAtTime(phaseTime);
                        return;
                    }
                }
                int playerResistanceBefore = session.Player.Resistance;
                int enemyResistanceBefore = session.Enemy.Resistance;
                int playerHealthBefore = session.Player.Health;
                int enemyHealthBefore = session.Enemy.Health;
                bool playerWasBroken = session.Player.IsResistanceBroken;
                bool enemyWasBroken = session.Enemy.IsResistanceBroken;
                LegacyHitResult hit = session.ResolveNextHit();
                if (hit.HitIndex == 0 && slotStartDeferred)
                {
                    // The slot's initial effects waited for the counter decision; report them now,
                    // excluding this hit's own resistance loss, which the hit presents itself.
                    slotStartDeferred = false;
                    hud.SetSkillFeedback(slot);
                    resistanceFeedback.Show(true, session.Player.Resistance - playerResistanceBefore + hit.PlayerResistanceDamage);
                    resistanceFeedback.Show(false, session.Enemy.Resistance - enemyResistanceBefore + hit.EnemyResistanceDamage);
                    if (slot.PlayerCountered) hud.ShowCounterCallout(true, arena.PlayerRenderer.transform.position);
                }
                if (hit.PlayerAttacked) playerSlotDamage = hit.EnemyDisplayedDamage;
                if (hit.EnemyAttacked) enemySlotDamage = hit.PlayerDisplayedDamage;
                if (hit.PlayerAttacked)
                    PresentHit(true, hit.EnemyHealthDamage, hit.EnemyResistanceDamage,
                        hit.EnemyDisplayedDamage, hit.EnemyPushPower,
                        hit.EnemySkill?.Kind == LegacySkillKind.Defence,
                        enemyResistanceBefore > 0 && session.Enemy.Resistance <= 0,
                        enemyHealthBefore > 0 && session.Enemy.Health <= 0,
                        Exchange(hit.EnemySkill, hit.EnemyAttacked));
                if (hit.EnemyAttacked && !hit.PlayerDodged)
                    PresentHit(false, hit.PlayerHealthDamage, hit.PlayerResistanceDamage,
                        hit.PlayerDisplayedDamage, hit.PlayerPushPower,
                        hit.PlayerSkill?.Kind == LegacySkillKind.Defence,
                        playerResistanceBefore > 0 && session.Player.Resistance <= 0,
                        playerHealthBefore > 0 && session.Player.Health <= 0,
                        Exchange(hit.PlayerSkill, hit.PlayerAttacked));
                // Includes a deferred counter slot's start effects, which run inside this hit.
                AnnounceBreaks(playerWasBroken, enemyWasBroken);
                // A long frame must not batch several impacts before any push
                // has been observed, nor immediately finish the final animation.
                if (phaseTime >= animationClipEnd)
                {
                    phaseTime = attackTime;
                    arena.HoldSlotAtTime(phaseTime);
                }
            }
            if (phaseTime >= slotDuration && slot.IsResolved)
            {
                hud.RecordResolvedSlot(slot.PlayerSkill, slot.EnemySkill, playerSlotDamage, enemySlotDamage);
                session.CompleteCurrentSlot();
                PrepareNextSlot(true);
            }
        }

        /// <summary>Calls out every fighter whose resistance broke during the last rules step, whether a
        /// hit or a skill effect (such as 발검's direct reduction) broke it, and updates the break aura.</summary>
        private void AnnounceBreaks(bool playerWasBroken, bool enemyWasBroken)
        {
            if (!playerWasBroken && session.Player.IsResistanceBroken)
                hud.ShowBreakCallout(true, arena.PlayerRenderer.transform.position, arena.PlayerRenderer.transform);
            if (!enemyWasBroken && session.Enemy.IsResistanceBroken)
                hud.ShowBreakCallout(false, arena.EnemyRenderer.transform.position, arena.EnemyRenderer.transform);
            arena.SetResistanceBroken(session.Player.IsResistanceBroken, session.Enemy.IsResistanceBroken);
        }

        // The rules resolve an attack against the target's attack skill as a resistance
        // exchange for the whole slot, even after that skill's own hits have ended.
        private static LegacyArenaView.HitExchange Exchange(LegacySkill targetSkill, bool targetStrikes) =>
            targetSkill?.Kind != LegacySkillKind.Attack ? LegacyArenaView.HitExchange.None
                : targetStrikes ? LegacyArenaView.HitExchange.MutualClash : LegacyArenaView.HitExchange.BladeBlock;

        private void PresentHit(bool playerAttacks, int healthDamage, int resistanceDamage,
            int displayedDamage, int pushPower, bool guarded, bool resistanceBroke, bool finishingBlow,
            LegacyArenaView.HitExchange exchange)
        {
            // Only decisive moments (a break or the finishing blow) get the slow close-up, tilt and
            // critical sound; a merely big number is still gold and larger.
            bool decisive = resistanceBroke || finishingBlow;
            bool emphasised = decisive || displayedDamage >= 12;
            // A resistance-only hit displays exactly its resistance loss. Anything more reached
            // the body, even when the health change was clamped at zero.
            bool bodyHit = healthDamage > 0 || displayedDamage > resistanceDamage;
            if (bodyHit) exchange = LegacyArenaView.HitExchange.None;
            // Steel only when the number is the resistance lost. A break that overflows nothing
            // shows its 0 HP as before; the break callout tells the rest.
            bool resistanceNumber = !bodyHit && resistanceDamage > 0 && displayedDamage == resistanceDamage;
            // Both sides of a simultaneous clash share the same real-time stop.
            hitStopRemaining = Mathf.Max(hitStopRemaining, presentationSettings.HitStopDuration);
            arena.PresentHit(playerAttacks, healthDamage, resistanceDamage, guarded, emphasised, pushPower, exchange, decisive);
            if (decisive) hud.FatalAttack(playerAttacks);
            Transform target = playerAttacks ? arena.EnemyRenderer.transform : arena.PlayerRenderer.transform;
            hud.ShowHitDamage(!playerAttacks, displayedDamage, target.position, emphasised, resistanceNumber);
            effectsSource.pitch = Random.Range(0.75f, 1.25f);
            art.PlayClash(effectsSource, false, Random.value >= 0.5f);
            if (decisive && criticalSound != null)
                effectsSource.PlayOneShot(criticalSound, 0.55f);
        }

        private void StartPlanning()
        {
            int playerResistanceBefore = session.Player.Resistance;
            int enemyResistanceBefore = session.Enemy.Resistance;
            session.BeginNextTurn();
            ClearHeldKeys();
            planningTime = PlanningDuration;
            highlightedSlot = -1;
            inspectingEnemy = 0;
            explainedSkill = null;
            arena.BeginTurn();
            hud.BeginTurn();
            // The automatic refill, a full turn after the one in which resistance broke.
            if (session.Player.Resistance > playerResistanceBefore)
                hud.ShowRecoveryCallout(true, arena.PlayerRenderer.transform.position, arena.PlayerRenderer.transform);
            if (session.Enemy.Resistance > enemyResistanceBefore)
                hud.ShowRecoveryCallout(false, arena.EnemyRenderer.transform.position, arena.EnemyRenderer.transform);
            arena.SetResistanceBroken(session.Player.IsResistanceBroken, session.Enemy.IsResistanceBroken);
            SetViewPhase(ViewPhase.Planning);
            guide?.NotifyTurnBegan(session.RoundNumber);
            RefreshGuide();
        }

        public bool TryStep(LegacyStepAction action, out bool success) =>
            TryStep(action, IsStepTimingWindow, out success);

        private bool TryStep(LegacyStepAction action, bool timingWindow, out bool success)
        {
            success = false;
            LegacyCurrentSlot slot = session?.CurrentSlot;
            bool counterPending = slot?.PendingPlayerCounter != null;
            bool pressureBacked = counterPending && slot.PressureSucceeded;
            if (!CanStep || guide != null && !guide.AllowsStep(action) || !session.TryStep(action, timingWindow, out success)) return false;
            if (counterPending && slot.PendingPlayerCounter == null && slot.PlayerSkill == null)
            {
                // Evading forgoes the counter: its strikes no longer lengthen the slot,
                // and pressure that backed it is withdrawn with it.
                slotDuration = SlotDurationFor(slot.HitCount);
                arena.SetPlayerSlotSkill(null);
                hud.SetCurrentSlotDuration(slotDuration + slotAnticipationDuration);
                if (pressureBacked) stepHud.ClearFeedback(LegacyStepAction.Pressure);
            }
            arena.PerformStep(action, success, session.StepSuccessStreak);
            stepHud.ShowFeedback(action, success, session.StepSuccessStreak);
            stepAudio.Play(action, success, presentationSettings.StepSoundVolume);
            RefreshHud(0f);
            if (guide != null)
            {
                guide.NotifyStepped(action);
                RefreshGuide();
            }
            return true;
        }

        private void SetViewPhase(ViewPhase next)
        {
            viewPhase = next;
            phaseTime = 0f;
        }

        private void ReadPlanningInput(Keyboard keyboard)
        {
            if (guide != null && guide.AllowsInspect && keyboard.tabKey.isPressed)
                InspectGuideEnemy();
            if (IsInspecting)
            {
                ClearHeldKeys();
                if (keyboard.leftArrowKey.wasPressedThisFrame) inspectingEnemy = Mathf.Max(0, inspectingEnemy - 1);
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                    inspectingEnemy = Mathf.Min(session.EnemyQueue.Count - 1, inspectingEnemy + 1);
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) CommitTurn();
                return;
            }
            if (keyboard.lKey.wasPressedThisFrame) hud.ToggleLog();
            if (keyboard.sKey.wasPressedThisFrame) QueueBreath();
            // Before the lane keys, so a lane key pressed in the same frame is for the skill 넘기기 brought forward.
            if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame) CycleLanes();
            ReadLaneKey(0, keyboard.qKey.isPressed, keyboard.qKey.wasPressedThisFrame, keyboard.qKey.wasReleasedThisFrame);
            ReadLaneKey(1, keyboard.wKey.isPressed, keyboard.wKey.wasPressedThisFrame, keyboard.wKey.wasReleasedThisFrame);
            ReadLaneKey(2, keyboard.eKey.isPressed, keyboard.eKey.wasPressedThisFrame, keyboard.eKey.wasReleasedThisFrame);
            if (keyboard.digit1Key.wasPressedThisFrame) QueueLane(0);
            if (keyboard.digit2Key.wasPressedThisFrame) QueueLane(1);
            if (keyboard.digit3Key.wasPressedThisFrame) QueueLane(2);
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) CommitTurn();
        }

        private void ReadLaneKey(int lane, bool held, bool pressed, bool released)
        {
            if (pressed && explainedSkill != null)
            {
                explainedSkill = null;
                explainedLane = -1;
            }
            if (held)
            {
                holdTimes[lane] += Time.unscaledDeltaTime;
                hud.SetHoldProgress(lane, Mathf.Clamp01((holdTimes[lane] - 0.3f) / 0.5f));
                // A lane the duel does not have (e.g. W/E in the Q-only missions) has nothing to explain.
                var laneSkills = session.GetLane(lane);
                if (holdTimes[lane] >= 1f && laneSkills.Count > 0)
                {
                    explainedSkill = laneSkills[0];
                    explainedLane = lane;
                    holdConsumed[lane] = true;
                }
            }
            if (released)
            {
                if (!holdConsumed[lane] && holdTimes[lane] <= 0.3f) QueueLane(lane);
                if (explainedLane == lane)
                {
                    explainedSkill = null;
                    explainedLane = -1;
                }
                holdTimes[lane] = 0f;
                holdConsumed[lane] = false;
                hud.SetHoldProgress(lane, 0f);
            }
        }

        public bool QueueLane(int lane)
        {
            if (!CanChoose || IsInspecting || lane < 0 || lane > 2 ||
                guide != null && !guide.AllowsQueue(lane) || !session.TryQueueLane(lane)) return false;
            guide?.NotifyQueued(lane);
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, Random.Range(0, 3));
            explainedSkill = null;
            RefreshHud(0f);
            RefreshGuide();
            return true;
        }

        /// <summary>넘기기 (Shift): every open lane sends its front skill to the back unused. Free, planning only;
        /// all lanes turn together, so lining skills up across lanes means choosing which ones to use first.</summary>
        public bool CycleLanes()
        {
            if (!CanChoose || IsInspecting || guide != null && !guide.AllowsCycle || !session.TryCycleLanes()) return false;
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, Random.Range(0, 3));
            // A lane key already down was pressed for the skill that just left, so its release must not queue the
            // one that came forward. A held explanation picks up the new front on the next frame.
            for (int lane = 0; lane < holdTimes.Length; lane++)
                if (holdTimes[lane] > 0f) holdConsumed[lane] = true;
            explainedSkill = null;
            guide?.NotifyCycled();
            RefreshHud(0f);
            RefreshGuide();
            return true;
        }

        public bool QueueBreath()
        {
            // The duel refuses a closed 숨고르기; a coached mission also waits for its lesson beat.
            if (!CanChoose || IsInspecting || guide != null && !guide.AllowsBreath || !session.TryQueueBreath()) return false;
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, Random.Range(0, 3));
            explainedSkill = null;
            guide?.NotifyBreathed();
            RefreshHud(0f);
            RefreshGuide();
            return true;
        }

        public void CommitTurn()
        {
            if (!CanChoose || guide != null && !guide.AllowsCommit) return;
            ClearHeldKeys();
            explainedSkill = null;
            guideInspectionRemaining = 0f;
            hud.HideExplanation();
            session.Commit();
            hud.EndTurn();
            arena.BeginApproach();
            highlightedSlot = -1;
            SetViewPhase(ViewPhase.Approaching);
            guide?.NotifyCommitted();
            RefreshHud(0f);
            RefreshGuide();
        }

        public void RestartMatch()
        {
            CloseDialogue();
            ClearMissionState();
            campaign.Reset();
            SyncStoryProgression();
            campaign.TryStartStage(1);
            StartStageBattle();
        }

        /// <summary>Restarts the post-arc journey (the lobby's 여정 초기화): a fresh campaign, back in the lobby.
        /// The opening arc's progress is kept.</summary>
        public void RestartJourney()
        {
            CloseDialogue();
            ClearMissionState();
            campaign.Reset();
            SyncStoryProgression();
            session = campaign.CreateDuel(System.Environment.TickCount);
            ResetBattlePresentation();
            lobbyHud.ResetView();
            ShowLobby();
            AutoSave();
        }

        /// <summary>A new game: a fresh campaign and the opening arc from its first mission briefing.</summary>
        public void StartNewGame()
        {
            CloseDialogue();
            ClearMissionState();
            campaign.Reset();
            prologue.Reset();
            SyncStoryProgression();
            session = campaign.CreateDuel(System.Environment.TickCount);
            ResetBattlePresentation();
            lobbyHud.ResetView();
            ShowBriefing();
            AutoSave();
        }

        /// <summary>The title screen: 이어하기 when a valid save exists, and 새 게임. Auto-saving stays off until one is chosen.</summary>
        public void ShowTitle()
        {
            ClearBattleScreens();
            lobbyHud.Hide();
            AutoSaveEnabled = false;
            StoryProgressionEnabled = false;
            SyncStoryProgression();
            showingTitle = true;
            string summary = null, notice = null;
            if (saveStore.Exists)
            {
                if (TryReadSave(out GameSave save, out string error)) summary = DescribeSave(save);
                else
                {
                    Debug.LogWarning(error);
                    notice = "저장 파일을 읽을 수 없어 이어할 수 없습니다. 새 게임을 시작하면 덮어씁니다.";
                }
            }
            titleHud.Show(summary, notice, saveStore.Exists);
        }

        /// <summary>Loads the save and resumes at the next mission's briefing, or the lobby once the arc is over.</summary>
        public bool ContinueGame()
        {
            if (!IsInTitle) return false;
            // The file may have changed since the title read it (e.g. the editor's 세이브 삭제 during Play).
            if (!TryReadSave(out GameSave save, out string error) || !save.TryApply(prologue, campaign, out error))
            {
                Debug.LogWarning(error);
                ShowTitle();
                return false;
            }
            AutoSaveEnabled = true;
            StoryProgressionEnabled = true;
            SyncStoryProgression();
            session = campaign.CreateDuel(System.Environment.TickCount);
            ResetBattlePresentation();
            lobbyHud.ResetView();
            ShowBriefing();
            return true;
        }

        /// <summary>The title's 새 게임: starts over and replaces any save with the fresh state.</summary>
        public bool NewGameFromTitle()
        {
            if (!IsInTitle) return false;
            AutoSaveEnabled = true;
            StoryProgressionEnabled = true;
            StartNewGame();
            return true;
        }

        /// <summary>Hands the story's unlocks and stage cap to the campaign, or opens everything outside a title session.</summary>
        private void SyncStoryProgression()
        {
            if (StoryProgressionEnabled) campaign.SetProgression(prologue.UnlockedFeatures, prologue.StageLimit);
            else campaign.ClearProgression();
        }

        /// <summary>The lobby's 임무 entry: opens the next story mission's briefing once its stage is cleared.</summary>
        public bool OpenNextMission()
        {
            if (!IsInLobby || !prologue.IsArcComplete || !IsNextMissionAvailable) return false;
            ShowBriefing();
            return true;
        }

        /// <summary>Escape or the briefing's back button: a lobby mission's briefing returns to the lobby.
        /// The 서막 briefings have nowhere else to go.</summary>
        public bool LeaveBriefing()
        {
            if (!IsInBriefing || !prologue.IsArcComplete) return false;
            ShowLobby();
            return true;
        }

        /// <summary>The count shown next to a mission number: the 서막 counts its own missions, later chapters the chain.</summary>
        private int MissionCountFor(PrologueMission shown)
            => shown.RequiredClearedStage == 0 ? prologue.ArcMissionCount : prologue.MissionCount;

        /// <summary>Reads the save and checks it against the game's rules without touching the live runs.</summary>
        private bool TryReadSave(out GameSave save, out string error)
            => saveStore.TryLoad(out save, out error) && save.Validate(out error);

        private string DescribeSave(GameSave save)
        {
            if (save.PrologueCleared < PrologueMissions.Count)
            {
                PrologueMission next = PrologueMissions.Get(save.PrologueCleared + 1);
                return $"{MissionBriefingHud.ChapterName}  ·  임무 {next.Number} / {PrologueMissions.Count}  ·  {next.Title}";
            }
            return $"로비  ·  스테이지 클리어 {save.Campaign.ClearedStages.Count} / {campaign.StageCount}  ·  " +
                $"커리큘럼 {save.Campaign.CurriculumCompleted.Count} / {campaign.Curriculum.Tree.Nodes.Count}  ·  " +
                $"임무 완료 {save.PrologueCleared} / {StoryMissions.Count}";
        }

        /// <summary>Writes the persistent progress after a settled change. Never during a battle's own state.</summary>
        private void AutoSave()
        {
            if (!AutoSaveEnabled || saveStore == null) return;
            if (!saveStore.TrySave(GameSave.Capture(prologue, campaign), out string error)) Debug.LogWarning(error);
        }

        /// <summary>Starts the briefed mission: its intro dialogue first, then the duel. Skipping the intro starts it too.</summary>
        public bool StartMission()
        {
            if (!IsInBriefing || prologue.CurrentMission == null) return false;
            int number = prologue.CurrentMission.Number;
            if (!PlayMissionDialogue(prologue.CurrentMission.IntroDialogue, () => BeginMissionBattle(number)))
                BeginMissionBattle(number);
            return true;
        }

        public bool StartDialogue(string resourcePath)
        {
            if (!IsInLobby) return false;
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                Debug.LogError("Dialogue resource path is required.");
                return false;
            }

            resourcePath = resourcePath.Trim();

            TextAsset source = Resources.Load<TextAsset>(resourcePath);
            if (source == null)
            {
                Debug.LogError($"Dialogue TextAsset was not found at Resources/{resourcePath}.txt");
                return false;
            }

            try
            {
                return StartDialogue(DialogueScriptParser.Parse(resourcePath, source.text),
                    defaultDialoguePortraitCatalog);
            }
            catch (DialogueParseException exception)
            {
                Debug.LogError(exception.Message);
                return false;
            }
        }

        public bool StartDialogue(DialogueScript script)
            => StartDialogue(script, defaultDialoguePortraitCatalog);

        public bool StartDialogue(DialogueScript script, DialoguePortraitCatalog portraitCatalog)
        {
            if (script == null) throw new System.ArgumentNullException(nameof(script));
            if (!IsInLobby) return false;
            OpenDialogue(script, portraitCatalog, null);
            return true;
        }

        private void OpenDialogue(DialogueScript script, DialoguePortraitCatalog portraitCatalog, System.Action continuation)
        {
            dialogueContinuation = continuation;
            dialogueSession = new DialogueSession(script);
            activeDialoguePortraitCatalog = portraitCatalog;
            dialogueOpenedFrame = Time.frameCount;
            ShowCurrentDialogueLine();
        }

        /// <summary>Plays a mission dialogue from Resources, then runs <paramref name="continuation"/> once the player
        /// reaches its end or skips it. Returns false (and runs nothing) when the file is missing or invalid.</summary>
        private bool PlayMissionDialogue(string resourcePath, System.Action continuation)
        {
            TextAsset source = string.IsNullOrWhiteSpace(resourcePath) ? null : Resources.Load<TextAsset>(resourcePath);
            if (source == null)
            {
                Debug.LogWarning($"Mission dialogue was not found at Resources/{resourcePath}.txt; continuing without it.");
                return false;
            }
            try
            {
                OpenDialogue(DialogueScriptParser.Parse(resourcePath, source.text), defaultDialoguePortraitCatalog, continuation);
                return true;
            }
            catch (DialogueParseException exception)
            {
                Debug.LogWarning(exception.Message);
                return false;
            }
        }

        public bool ContinueDialogue()
        {
            if (!IsShowingDialogue) return false;
            if (!dialogueSession.MoveNext())
            {
                FinishDialogue();
                return false;
            }
            ShowCurrentDialogueLine();
            return true;
        }

        /// <summary>The player finished or skipped the dialogue: close it and continue whatever it was guarding.</summary>
        private void FinishDialogue()
        {
            System.Action next = dialogueContinuation;
            CloseDialogue();
            next?.Invoke();
        }

        /// <summary>Closes any dialogue without continuing its flow (cleanup, restarts and tests).</summary>
        public bool CloseDialogue()
        {
            bool wasOpen = IsShowingDialogue;
            dialogueContinuation = null;
            dialogueSession = null;
            activeDialoguePortraitCatalog = null;
            dialogueOpenedFrame = -1;
            dialogueHud?.Hide();
            return wasOpen;
        }

        private void ShowCurrentDialogueLine()
        {
            DialogueLine line = dialogueSession.Current;
            Sprite leftPortrait = activeDialoguePortraitCatalog?.FindPortrait(line.Stage.Left?.SpeakerName);
            Sprite rightPortrait = activeDialoguePortraitCatalog?.FindPortrait(line.Stage.Right?.SpeakerName);
            dialogueHud.Show(line, dialogueSession.CurrentIndex, dialogueSession.Count,
                leftPortrait, rightPortrait);
        }

        /// <summary>Makes a curriculum node the one in progress (lobby only).</summary>
        public bool SelectCurriculumNode(string nodeId)
        {
            if (!IsInLobby || !campaign.TrySelectCurriculumNode(nodeId)) return false;
            lobbyHud.Show(campaign);
            AutoSave();
            return true;
        }

        /// <summary>Clears the curriculum and the skills it granted (lobby only).</summary>
        public bool ResetCurriculum()
        {
            if (!IsInLobby || !campaign.TryResetCurriculum()) return false;
            lobbyHud.Show(campaign);
            AutoSave();
            return true;
        }

        public void ContinueCampaign()
        {
            if (IsShowingResult) DismissBattleResult();
            else if (IsInLobby) StartCampaignStage(lobbyHud.SelectedStageNumber);
        }

        public bool AdvanceGuide()
        {
            if (guide == null || IsShowingResult || !CanChoose || !guide.TryAdvance()) return false;
            ClearHeldKeys();
            RefreshGuide();
            return true;
        }

        public bool InspectGuideEnemy()
        {
            if (guide == null || !CanChoose || !guide.AllowsInspect) return false;
            guide.NotifyInspected();
            inspectingEnemy = 0;
            guideInspectionRemaining = 2f;
            ClearHeldKeys();
            RefreshHud(0f);
            RefreshGuide();
            return true;
        }

        public bool DismissBattleResult()
        {
            if (!IsShowingResult) return false;
            if (battleResult.IsMission) ShowBriefing();
            else ShowLobby();
            return true;
        }

        public bool RetryBattleResult()
        {
            if (!IsShowingResult) return false;
            int number = battleResult.StageNumber;
            // A mission retry skips its intro, as a restarted StarCraft mission does.
            if (battleResult.IsMission)
            {
                BeginMissionBattle(number);
                return true;
            }
            ShowLobby();
            return StartCampaignStage(number);
        }

        public bool AdvanceFromBattleResult()
        {
            if (!IsShowingResult || !battleResult.CanAdvance) return false;
            // The next mission starts from its briefing; after the last one the lobby opens.
            if (battleResult.IsMission)
            {
                ShowBriefing();
                return true;
            }
            int number = battleResult.StageNumber + 1;
            ShowLobby();
            return StartCampaignStage(number);
        }

        public bool StartCampaignStage(int stageNumber)
        {
            if (!IsInLobby || !campaign.TryStartStage(stageNumber)) return false;
            StartStageBattle();
            return true;
        }

        public bool EquipSkill(int skillId)
        {
            if (!IsInLobby || !campaign.TryEquipSkill(skillId)) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        public bool UnequipSkill(int skillId)
        {
            if (!IsInLobby || !campaign.TryUnequipSkill(skillId)) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        public bool MoveEquippedSkill(int skillId, int direction)
        {
            if (!IsInLobby || !campaign.TryMoveEquippedSkill(skillId, direction)) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        public bool PlaceLoadoutSkill(int skillId, int laneIndex, int slotIndex)
        {
            if (!IsInLobby || !campaign.TryPlaceLoadoutSkill(skillId, laneIndex, slotIndex)) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        public bool SaveLoadout()
        {
            if (!IsInLobby || !campaign.TrySaveLoadout()) return false;
            lobbyHud.Show(campaign);
            AutoSave();
            return true;
        }

        public bool ResetLoadout()
        {
            if (!IsInLobby || !campaign.TryResetLoadout()) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        /// <summary>Escape or the coach's abandon: a mission goes back to its briefing, a stage to the lobby.</summary>
        public void ReturnToLobby()
        {
            if (IsMission)
            {
                ShowBriefing();
                return;
            }
            if (campaign.Phase == CampaignPhase.Battle) campaign.TryAbandonBattle();
            ShowLobby();
        }

        private void FinishStage()
        {
            if (IsShowingResult || !session.IsFinished) return;
            if (IsMission)
            {
                FinishMission();
                return;
            }
            bool firstClear = !campaign.IsStageCleared(campaign.StageNumber);
            int unlockedBefore = campaign.HighestUnlockedStage;
            if (!campaign.TryCompleteBattle(session.Outcome)) return;
            AutoSave();
            bool victory = session.Outcome == DuelMatchOutcome.PlayerVictory;
            var result = new BattleResult(session.Outcome, false,
                campaign.StageNumber, campaign.CurrentStage.Name,
                campaign.LastReward, campaign.Currency, session.RoundNumber,
                session.Player.Health, session.Enemy.Health, firstClear && victory,
                campaign.HighestUnlockedStage > unlockedBefore ? campaign.HighestUnlockedStage : 0,
                victory && campaign.StageNumber < campaign.StageCount &&
                campaign.StageNumber + 1 <= campaign.HighestUnlockedStage && campaign.StageNumber + 1 <= campaign.StageLimit,
                campaign.LastCompletedCurriculumNode, campaign.Curriculum.Active, campaign.Curriculum.ActiveBattles,
                campaign.Curriculum.IsFinished);
            EndDuelPresentation();
            // Clearing the stage a lobby mission waited for brings that mission; the next stage waits for it.
            PrologueMission arrived = victory && prologue.IsArcComplete && IsNextMissionAvailable &&
                campaign.StageNumber == prologue.CurrentMission.RequiredClearedStage ? prologue.CurrentMission : null;
            battleResult = result;
            resultHud.Show(result, null, arrived != null ? $"새 임무 '{arrived.Title}' 도착 · 로비에서 브리핑을 여세요." : null);
        }

        /// <summary>A finished mission: record progress, play the outro on a victory, then show the result.</summary>
        private void FinishMission()
        {
            bool victory = session.Outcome == DuelMatchOutcome.PlayerVictory;
            guide?.Finish();
            bool firstWin = prologue.TryComplete(mission.Number, session.Outcome);
            if (firstWin)
            {
                SyncStoryProgression();
                AutoSave();
            }
            var result = new BattleResult(session.Outcome, true, mission.Number, mission.Title, 0, campaign.Currency,
                session.RoundNumber, session.Player.Health, session.Enemy.Health, false, 0, victory);
            // A first win that opened something (넘기기 for mission 1, a feature for each lobby mission) announces it,
            // then where the story goes next.
            string unlockNotice = firstWin && !string.IsNullOrEmpty(mission.UnlockText)
                ? mission.UnlockText + (prologue.IsComplete ? string.Empty
                    : IsNextMissionAvailable ? "\n다음 임무가 열렸습니다." : "\n다음 스테이지를 깨면 다음 임무가 열립니다.")
                : null;
            EndDuelPresentation();
            if (victory && PlayMissionDialogue(mission.OutroDialogue, () => ShowResult(result, unlockNotice))) return;
            ShowResult(result, unlockNotice);
        }

        private void EndDuelPresentation()
        {
            effectsSource.Stop();
            hitStopRemaining = 0f;
            stepAudio.Stop();
            planningTime = 0f;
            ClearHeldKeys();
            explainedSkill = null;
            SetViewPhase(ViewPhase.Outcome);
            hud.ClearSkillFeedback();
            hud.Root.SetActive(false);
            lobbyHud.Hide();
            coachHud.Hide();
        }

        private void ShowResult(BattleResult result, string unlockNotice = null)
        {
            battleResult = result;
            // A mission's exits reach the lobby when no mission is playable right now (the next waits for a stage).
            resultHud.Show(result, result.IsMission && !IsNextMissionAvailable, unlockNotice);
        }

        private void BeginMissionBattle(int number)
        {
            PrologueMission next = prologue.Missions[number - 1];
            CloseDialogue();
            mission = next;
            guide = next.CreateGuide();
            session = next.CreateDuel(System.Environment.TickCount);
            ResetBattlePresentation();
        }

        /// <summary>The next mission's briefing; the lobby instead when no mission is playable right now.</summary>
        private void ShowBriefing()
        {
            if (!IsNextMissionAvailable)
            {
                ShowLobby();
                return;
            }
            ClearBattleScreens();
            showingBriefing = true;
            lobbyHud.Hide();
            PrologueMission next = prologue.CurrentMission;
            briefingHud.Show(next, MissionCountFor(next), prologue.IsArcComplete);
        }

        private void ClearMissionState()
        {
            mission = null;
            guide = null;
            showingBriefing = false;
            guideInspectionRemaining = 0f;
            briefingHud?.Hide();
        }

        private void ShowLobby()
        {
            ClearBattleScreens();
            SyncStoryProgression();
            // Lobby missions appear on the lobby once the 서막 is over and their stage is cleared.
            PrologueMission next = prologue.IsArcComplete ? prologue.CurrentMission : null;
            lobbyHud.SetNextMission(next, next != null && IsNextMissionAvailable);
            lobbyHud.Show(campaign);
        }

        /// <summary>Leaves every duel, result, coach and dialogue screen, ready for the lobby or a briefing.</summary>
        private void ClearBattleScreens()
        {
            showingTitle = false;
            titleHud?.Hide();
            CloseDialogue();
            ClearMissionState();
            battleResult = null;
            resultHud.Hide();
            coachHud.Hide();
            hud.SetMissionMode(false);
            hud.SetGuide(null);
            hud.SetGuideFocus(-1, false, false, false);
            campaign.ReturnToLobby();
            effectsSource.Stop();
            stepAudio.Stop();
            stepHud.Reset();
            resistanceFeedback.Reset();
            hud.ClearSkillFeedback();
            arena.SetEnemyAppearance(EnemyAppearance.Student);
            arena.Reset();
            hitStopRemaining = 0f;
            SetViewPhase(ViewPhase.Outcome);
            planningTime = 0f;
            ClearHeldKeys();
            explainedSkill = null;
            hud.Root.SetActive(false);
        }

        private void StartStageBattle()
        {
            session = campaign.CreateDuel(System.Environment.TickCount);
            ResetBattlePresentation();
        }

        private void ResetBattlePresentation()
        {
            CloseDialogue();
            battleResult = null;
            guideInspectionRemaining = 0f;
            showingBriefing = false;
            briefingHud.Hide();
            showingTitle = false;
            titleHud?.Hide();
            resultHud.Hide();
            coachHud.Hide();
            effectsSource.Stop();
            effectsSource.pitch = 1f;
            stepAudio.Stop();
            session.Reset();
            stepHud.Reset();
            resistanceFeedback.Reset();
            hud.ClearSkillFeedback();
            lobbyHud.Hide();
            hud.Root.SetActive(true);
            hitStopRemaining = betweenSlotsDuration = slotDuration = slotAttackInterval = slotImpactTime = slotCycleDuration = 0f;
            slotAnticipationDuration = slotStepWindow = 0f;
            slotPlaybackSpeed = 1f;
            planningTime = PlanningDuration;
            highlightedSlot = -1;
            inspectingEnemy = 0;
            explainedSkill = null;
            arena.SetEnemyAppearance(IsMission ? mission.EnemyAppearance : EnemyAppearance.Student);
            arena.Reset();
            hud.Reset();
            hud.SetMissionMode(IsMission, IsMission && mission.PlanningTimer, IsMission && mission.BreathEnabled);
            hud.SetGuide(guide);
            if (IsMission) hud.SetStage(mission.Number, MissionCountFor(mission), mission.Title);
            else hud.SetStage(campaign.StageNumber, campaign.StageCount, campaign.CurrentStage.Name);
            SetViewPhase(ViewPhase.Planning);
            ClearHeldKeys();
            RefreshHud(0f);
            RefreshGuide();
        }

        private void RefreshGuide()
        {
            if (guide == null || IsShowingResult || viewPhase == ViewPhase.Outcome)
            {
                coachHud.Hide();
                hud.SetGuide(null);
                hud.SetGuideFocus(-1, false, false, false);
                return;
            }
            coachHud.Show(guide, $"임무 {mission.Number:00}");
            hud.SetGuide(guide);
            hud.SetGuideFocus(guide.ExpectedLane, guide.FocusesCommit, guide.FocusesEnemyQueue, guide.FocusesAct);
        }

        private void ClearHeldKeys()
        {
            explainedSkill = null;
            explainedLane = -1;
            for (int i = 0; i < holdTimes.Length; i++)
            {
                holdTimes[i] = 0f;
                holdConsumed[i] = false;
                hud?.SetHoldProgress(i, 0f);
            }
        }

        private void RefreshHud(float delta) => RefreshHudClock(delta, delta);

        private void RefreshHudClock(float delta, float realDelta)
        {
            // Every frame, after this frame's poses: the outline follows the actor's current sprite.
            arena.SetResistanceBroken(session.Player.IsResistanceBroken, session.Enemy.IsResistanceBroken);
            hud.Refresh(session, planningTime, IsResolving, highlightedSlot, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform, delta, realDelta);
            stepHud.BindActor(arena.ArenaCamera, arena.PlayerRenderer.transform);
            stepHud.Refresh(CanStep, session.CurrentSlot, StepCueProgress, IsStepTimingWindow,
                StepWindowFraction, session.UsedStepThisTurn, session.StepAttemptsThisTurn, session.StepMissedThisTurn,
                StepFeatures);
            resistanceFeedback.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            if (IsInspecting && session.EnemyQueue.Count > 0)
            {
                hud.SetInspectedSlot(Mathf.Clamp(inspectingEnemy, 0, session.EnemyQueue.Count - 1));
                hud.ShowExplanation(session.EnemyQueue[Mathf.Clamp(inspectingEnemy, 0, session.EnemyQueue.Count - 1)], true);
            }
            else if (CanChoose && explainedSkill != null) hud.ShowExplanation(explainedSkill, false);
            else hud.HideExplanation();
        }

        private void OnDisable() => stepAudio?.Stop();

        private void OnDestroy()
        {
            dialogueHud?.Dispose();
            resultHud?.Dispose();
            coachHud?.Dispose();
            briefingHud?.Dispose();
            titleHud?.Dispose();
            lobbyHud?.Dispose();
            stepHud?.Dispose();
            stepAudio?.Dispose();
            resistanceFeedback?.Dispose();
            hud?.Dispose();
            arena?.Dispose();
            art?.Dispose();
            if (ownedPresentationSettings != null) Object.Destroy(ownedPresentationSettings);
        }
    }
}
