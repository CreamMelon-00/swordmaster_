using TurnLimbo.Runtime.Barks;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Dialogue;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPrototypeController : MonoBehaviour
    {
        private const float PlanningDuration = 10f;
        private float CurrentPlanningDuration => planningDuration;
        /// <summary>넘기기 spends this much planning time while the planning timer runs (it is free while the clock is
        /// held: untimed missions and coached beats).</summary>
        public const float LaneCycleTimeCost = 1f;
        /// <summary>A paid 넘기기 (or a reservation's 제한시간 cost) must leave at least this much planning time, so it never
        /// runs the clock out (and auto-commits the turn) before the player can use the skill it brought forward.</summary>
        public const float LaneCycleMinimumTimeLeft = LegacyPlanningClock.MinimumTimeLeft;
        private const float OriginalClipDuration = 1f / 6f;
        private const float OriginalAttackEventTime = 1f / 12f;
        /// <summary>Legacy fallback; active turns snapshot DuelPresentationSettings.TurnCleanupSeconds.</summary>
        public const float TurnCleanupDelay = 0.12f;
        [SerializeField] private DuelPresentationSettings presentationSettings;
        private enum ViewPhase { Planning, Approaching, ClosingDistance, SkillWindup, PlayingSlot, BetweenSlots, AfterTurn, Settling, Outcome }
        // Each lane's hold (its key, or the pointer on its gear's window) and the explanation a hold opened (LegacyLaneHold);
        // versus reads its lanes the same way.
        private readonly DuelLaneHolds laneHolds = new DuelLaneHolds();
        private System.Func<int, bool> queueLaneOnRelease;
        private LegacyQueuedDuel session;
        private LegacyDuelArt art;
        private LegacyArenaView arena;
        private LegacyCombatHud hud;
        private DuelStepHud stepHud;
        private DuelStepAudio stepAudio;
        private DuelResistanceFeedback resistanceFeedback;
        private DuelBreakImpactCue breakImpactCue;
        private DuelSkillActivationCue skillActivationCue;
        private DuelSkillLevelUpCue skillLevelUpCue;
        private DialogueHud dialogueHud;
        private DialogueSession dialogueSession;
        private DialoguePortraitCatalog defaultDialoguePortraitCatalog;
        private DialoguePortraitCatalog activeDialoguePortraitCatalog;
        private CampaignRun campaign;
        private CampaignLobbyHud lobbyHud;
        private AcademyFieldView academyField;
        private BattleResultHud resultHud;
        private DuelPauseHud pauseHud;
        private MissionCoachHud coachHud;
        private MissionBriefingHud briefingHud;
        private TitleHud titleHud;
        private LocalVersusController localVersus;
        private OnlineVersusController onlineVersus;
        private DuelLoadingHud loadingHud;
        private bool showingTitle;
        private GameSaveStore saveStore;
        private BattleResult battleResult;
        private PrologueRun prologue;
        private string pendingAcademyNotice;
        /// <summary>The field's arrival notice after 이어하기 emptied a curriculum made with an older tree
        /// (<see cref="CampaignRun.RestoreDroppedCurriculum"/>).</summary>
        internal const string CurriculumResetNotice =
            "커리큘럼이 새로 짜여 처음부터 익힙니다.\n모은 수련 점수와 이야기·스테이지 기록, 주운 기술은 그대로입니다.";
        // The opening-arc mission being fought, its coach, and whether its briefing is on screen.
        private PrologueMission mission;
        private MissionGuide guide;
        private bool showingBriefing;
        private float guideInspectionRemaining;
        // Runs after the player finishes or skips the open dialogue; cleanup closes never run it.
        private System.Action dialogueContinuation;
        private int dialogueOpenedFrame = -1;
        // The cutscene on stage, what runs once the player finishes or skips it, and the frame it opened on.
        private CutsceneHud cutsceneHud;
        private CutsceneDirector cutscene;
        private System.Action cutsceneContinuation;
        private bool lastCutsceneCompletedNaturally;
        private int cutsceneOpenedFrame = -1;
        private OpeningVoiceChoice openingChoice = OpeningVoiceChoice.Full;
        private bool openingCompleted = true;
        private bool openingBranchEnabled;
        private bool openingInterruptTriggered;
        private int openingClickCount;
        private int openingClickFrame = -1;
        private int openingInterruptFrame = -1;
        private bool openingSuppressNextPointerAdvance;
        private bool pointerPressedDuringReveal;
        private bool openingReplayUsesSavedRoute;
        // The first missions' coached screens, captured for the 서막's tutorial recall (@recall); kept for the session.
        private TutorialRecallAlbum recallAlbum;
        // A mission's battlefield scene leaves its last picture when it ends, for the result to show over (whatever
        // follows restages the arena). Other scenes hand the arena back at its duel framing.
        private bool cutsceneKeepsStage;
        // A mission's mid-battle event (the 서막's 수훈): the hit that reached its threshold waits for its hit stop, then
        // the battle pauses for the scene. Once the empowerment is applied, the rest of the attempt is empowered.
        private bool missionEventPending;
        private bool missionEmpowered;
        private bool skillExperienceDirty;
        // A battle paused for a mission event scene or dialogue; the triggering hit ends that turn.
        private bool battlePausedForEvent;
        // The finishing blow's slow motion, bars and freeze frame (every battle), and the 수훈 phase's cut-ins, grade, hum
        // and shakes (the 서막's last mission after its event).
        private DuelFinale finale;
        private DuelEmpowermentCues empowermentCues;
        // The battle's speech bubbles (Resources/Barks): told the battle's moments, they never pause it.
        private DuelBarks barks;
        // 엘리사's eye: the gear shimmer over the enemy's queue that each planning turn reveals, waiting until that turn is
        // on screen (after the start card).
        private DuelGearShimmer gearShimmer;
        private bool enemyQueueRevealPending;
        // 맞물림's cues: the gears biting in the player's queue row as a chain forms, and their flash at a meshed slot's
        // first hit.
        private DuelMeshCues meshCues;
        // The battle's start card holds it from the attempt's start to its first planning turn. A retry from the result
        // gets the short one; keys of the frame that put it up belong to the screen before.
        private DuelStartCard startCard;
        private bool startCardRetry;
        private int startCardOpenedFrame = -1;
        private const string TrainingOpponentName = "허수아비";
        // The forest's ambience bed under battles and forest scenes.
        private DuelForestAmbience forestAmbience;
        // Reads a mission cutscene's text by Resources path, or null when there is none (tests supply their own).
        private System.Func<string, string> missionSceneText = ReadTextResource;
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
        private bool finishingHitPlayback;
        private float finishingHitTime;
        private float finishingClipEnd;
        private float slotAnticipationDuration;
        private float slotStepWindow;
        // Snapshotted with the base window at slot start, so live tuning never changes a skill mid-cue.
        private float slotStepWindowDecay = LegacyStepTiming.DefaultDecay;
        private float slotStepMinimumWindow = LegacyStepTiming.DefaultMinimumWindow;
        private float betweenSlotsDuration;
        private float turnCleanupDuration = TurnCleanupDelay;
        private float hitStopRemaining;
        private float planningDuration = PlanningDuration;
        // This turn's own start time: the duration plus what the last turn's skills earned (제한시간), within +10 s. The bar
        // and the time pressure measure against it.
        private float turnPlanningDuration = PlanningDuration;
        // Whether the clock ran on the turn last committed: only then do the seconds its skills earn start the next turn
        // later (a held clock neither pays nor gains, even when the next turn's clock runs).
        private bool committedTurnClockRan;
        private float planningTime;
        private int highlightedSlot;
        private int inspectingEnemy;
        private int playerSlotDamage, enemySlotDamage;
        private DuelPresentationSettings ownedPresentationSettings;
        // The presentation director (2026-10-15): each resolving slot's weight and the pacing it buys (a Minor slot's rest
        // and the wait after it shorter, a Major slot's camera push and longer first hit stop, an even 합's blade bind),
        // within the turn's cap. It never changes the time before a slot's first hit (the step windows).
        private readonly LegacyShowDirector showDirector = new LegacyShowDirector();
        // The turn the director took its numbers for (a slot begun without CommitTurn takes them then).
        private LegacyQueuedDuel directorSession;
        private int directorRound = -1;
        // A blade bind holds the battle's clock for this many real seconds more, after the hit stop.
        private float bladeBindRemaining;
        private bool bladeBindHitPending, releasingBladeBindHit;
        // While a blade bind holds, the camera comes in by this share of a Major slot's push.
        private const float BladeBindCameraShare = .6f;

        public LegacyQueuedDuel Session => session;
        public CampaignRun Campaign => campaign;
        public CampaignLobbyHud LobbyHud => lobbyHud;
        public AcademyFieldView AcademyField => academyField;
        public BattleResultHud ResultHud => resultHud;
        public DuelPauseHud PauseHud => pauseHud;
        public bool IsPaused => pauseHud != null && pauseHud.IsVisible;
        public MissionCoachHud CoachHud => coachHud;
        public MissionBriefingHud BriefingHud => briefingHud;
        public TitleHud TitleHud => titleHud;
        public LocalVersusController LocalVersus => localVersus;
        public OnlineVersusController OnlineVersus => onlineVersus;
        public bool IsInOnlineVersus => onlineVersus != null && onlineVersus.IsActive;
        public DuelLoadingHud LoadingHud => loadingHud;
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
        /// <summary>The awakening opening, played after the title's 새 게임 and before the first briefing.</summary>
        public const string OpeningCutscene = "Cutscene/opening";
        public OpeningVoiceChoice OpeningChoice => openingChoice;
        public bool OpeningCompleted => openingCompleted;
        public bool IsInTitle => showingTitle && !IsShowingDialogue && !IsPlayingCutscene && titleHud != null && titleHud.IsVisible;
        public bool IsPlayingCutscene => cutscene != null;
        public CutsceneDirector Cutscene => cutscene;
        /// <summary>A story scene holds the screen: a cutscene, or a dialogue (missions without a cutscene file).</summary>
        public bool IsPlayingScene => IsPlayingCutscene || IsShowingDialogue;
        /// <summary>Whether the battle on screen is paused for a mission's mid-battle event scene (the 서막's 수훈).</summary>
        public bool IsBattlePausedForEvent => battlePausedForEvent;
        /// <summary>Whether this mission attempt's empowerment has been applied (the 서막's last mission after 이아's 수훈):
        /// the enemy follows its new script, and a forced-loss defeat now completes the mission.</summary>
        public bool IsMissionEmpowered => missionEmpowered;
        /// <summary>The finishing blow's slow motion, letterbox bars and freeze frame (and the 서막's final fall to grey).</summary>
        public DuelFinale Finale => finale;
        /// <summary>The 수훈 phase's presentation: cut-ins, the warm grade and vignette, the aura's hum, 라우다레's shakes.</summary>
        public DuelEmpowermentCues EmpowermentCues => empowermentCues;
        /// <summary>The battle's barks: one-line speech bubbles over the fighters' heads, from the battle's bark file.</summary>
        public DuelBarks Barks => barks;
        /// <summary>엘리사's eye: a brass gear and a glint over the enemy's queue as each planning turn reveals it.</summary>
        public DuelGearShimmer GearShimmer => gearShimmer;
        /// <summary>맞물림's cues: gears and sparks on the seam as queueing makes or lengthens a chain, and their flash at a
        /// meshed slot's first hit, where the step rings would have been.</summary>
        public DuelMeshCues MeshCues => meshCues;
        /// <summary>맞물림's percent every duel this controller makes is given
        /// (<see cref="DuelPresentationSettings.MeshPercent"/>; 0 switches it off).</summary>
        public int MeshPercent => presentationSettings != null ? presentationSettings.MeshPercent : LegacyMeshing.DefaultPercent;
        /// <summary>The start card that opens a battle: the two fighters' silhouettes and names, crossed swords between them.</summary>
        public DuelStartCard StartCard => startCard;
        /// <summary>Whether the battle's start card is up: it holds the battle until it has played or is skipped.</summary>
        public bool IsShowingStartCard => startCard.IsShowing;
        /// <summary>Whether a battle opens with its start card. Like auto-save, only the title's 이어하기/새 게임 turn it on,
        /// so tests and direct API use start a battle at its first planning turn as before (a test of the card turns it on).</summary>
        public bool StartCardsEnabled { get; set; }
        /// <summary>The forest's ambience bed under battles and the scenes on the forest arena.</summary>
        public DuelForestAmbience ForestAmbience => forestAmbience;
        public CutsceneHud CutsceneHud => cutsceneHud;
        /// <summary>What a cutscene's <c>@recall</c> brings back: the screens captured while the coached beats of the
        /// 서막's first missions were up this session (cleared at the title and on a new game).</summary>
        public TutorialRecallAlbum RecallAlbum => recallAlbum;
        public DialogueHud DialogueHud => dialogueHud;
        public DialogueLine CurrentDialogueLine => dialogueSession?.Current;
        public BattleResult Result => battleResult;
        public PrologueRun Prologue => prologue;
        public PrologueMission ActiveMission => mission;
        public MissionGuide Guide => guide;
        public bool IsMission => mission != null;
        public bool IsTrainingBattle => campaign != null && campaign.IsTrainingBattle;
        /// <summary>결투 or 전투 for the fight on screen (internal; changes presentation only). Missions say which;
        /// stages are 전투.</summary>
        public EncounterKind Encounter => IsInSkillTavern || IsTrainingBattle ? EncounterKind.Battle
            : IsMission ? mission.Encounter : campaign.CurrentStage.Encounter;
        public bool IsShowingResult => viewPhase == ViewPhase.Outcome && battleResult != null;
        public bool IsShowingDialogue => dialogueSession != null && dialogueHud != null && dialogueHud.IsVisible;
        public bool IsInBriefing => showingBriefing && !showingTitle && !IsShowingDialogue && !IsPlayingCutscene &&
            viewPhase == ViewPhase.Outcome && !IsShowingResult;
        public bool IsInLobby => !IsInSkillTavern && !IsInOnlineVersus && !IsShowingDialogue && !IsPlayingCutscene && viewPhase == ViewPhase.Outcome && !IsShowingResult &&
            !IsMission && !showingBriefing && !showingTitle && campaign.Phase == CampaignPhase.Lobby;
        public bool IsInAcademyField => IsInLobby && academyField != null && academyField.IsVisible;
        public DuelPresentationSettings PresentationSettings => presentationSettings;
        public LegacyArenaView ArenaView => arena;
        public LegacyCombatHud Hud => hud;
        public DuelStepHud StepHud => stepHud;
        public bool IsResolving => viewPhase != ViewPhase.Planning && viewPhase != ViewPhase.Outcome;
        public bool CanChoose => !IsInSkillTavern && !IsPaused && viewPhase == ViewPhase.Planning && session.Phase == LegacyDuelPhase.Planning;
        public int PlayerHealth => session.Player.Health;
        public int EnemyHealth => RemainingEnemyHealth;
        public DuelMatchOutcome Outcome => session.Outcome;
        public float TurnTimeRemaining => planningTime;
        /// <summary>The planning clock this turn started at: the stage's duration plus the seconds the last turn's skills earned
        /// (사용 효과 앞 X마다 다음 턴 제한시간), at most <see cref="LegacyPlanningClock.MaximumBonusSeconds"/> more.</summary>
        public float TurnPlanningDuration => turnPlanningDuration;
        public bool IsBetweenSlots => viewPhase == ViewPhase.BetweenSlots;
        public float HitStopTimeRemaining => hitStopRemaining;
        /// <summary>The presentation director (<see cref="LegacyShowDirector"/>): the resolving slot's weight and this turn's
        /// ledger of added and saved time.</summary>
        public LegacyShowDirector ShowDirector => showDirector;
        /// <summary>Real seconds the current blade bind still holds the battle's clock (after the hit stop); 0 while none.</summary>
        public float BladeBindTimeRemaining => bladeBindRemaining;
        /// <summary>The legacy unopposed-streak multiplier, used only when progressive tempo is off. It speeds a run of
        /// unopposed slots (<see cref="LegacyShowDirector.StreakSpeed"/>; the wait after such a slot,
        /// <see cref="LegacyShowDirector.GapSpeed"/>). Before a slot's first hit only where no step can be taken and no 반격
        /// waits to fill the slot, so a step window keeps its real time; 1 anywhere else. Another slow motion (a step's, the
        /// decisive close-up's, the finale's) takes over from it.</summary>
        public float StreakPlaybackSpeed
        {
            get
            {
                if (viewPhase == ViewPhase.BetweenSlots) return showDirector.GapSpeed;
                if (viewPhase != ViewPhase.PlayingSlot) return 1f;
                LegacyCurrentSlot slot = session?.CurrentSlot;
                if (slot == null) return 1f;
                if (slot.HitsResolved == 0 && !StreakFromSlotStart(slot)) return 1f;
                return showDirector.StreakSpeed;
            }
        }
        /// <summary>The turn's progressive playback speed, with an optional multiplier after either queue runs out. The first impact keeps
        /// its real-time step window whenever a step or pending counter can still change the slot.</summary>
        public float ResolutionPlaybackSpeed
        {
            get
            {
                if (viewPhase == ViewPhase.BetweenSlots) return showDirector.PlaybackGapSpeed;
                if (viewPhase == ViewPhase.ClosingDistance)
                    return showDirector.Tempo.Enabled ? showDirector.PlaybackGapSpeed : 1f;
                if (viewPhase != ViewPhase.PlayingSlot) return 1f;
                LegacyCurrentSlot slot = session?.CurrentSlot;
                if (slot == null || slot.HitsResolved == 0 && !StreakFromSlotStart(slot)) return 1f;
                return showDirector.PlaybackSpeed;
            }
        }
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
        // A cut-in holds the battle: no step is judged against its held cue.
        public bool CanStep => session != null && !IsInSkillTavern && !IsPaused && !battlePausedForEvent && !empowermentCues.IsCuttingIn &&
            !bladeBindHitPending && !bladeExchange.IsHolding &&
            StepFeatures.AllowsAnyStep() &&
            session.Phase == LegacyDuelPhase.Resolving &&
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
        private bool IsInspecting => CanChoose && !startCard.IsShowing && (guideInspectionRemaining > 0f ||
            Keyboard.current != null && Keyboard.current.tabKey.isPressed);
        /// <summary>Whether the player is reading a held skill's explanation this planning frame (Tab's inspection takes
        /// over from it). The planning clock runs at <see cref="DuelPresentationSettings.ExplanationTimeScale"/> meanwhile.</summary>
        public bool IsReadingExplanation => CanChoose && !IsInspecting && laneHolds.ExplainedSkill != null &&
            laneHolds.ExplainedSkill.Kind != LegacySkillKind.Wait;
        /// <summary>The planning clock's pace now (<see cref="LegacyLaneHold.PlanningTimeScale"/>): Tab's 0.2, a held
        /// explanation's, or 1. 전투's bullet-time drift runs on the same clock.</summary>
        public float PlanningTimeScale => LegacyLaneHold.PlanningTimeScale(CanChoose, IsInspecting, IsReadingExplanation,
            presentationSettings.ExplanationTimeScale);
        // A coached beat never runs out of time; a mission may also have no timer at all.
        private bool PlanningTimerRuns => !IsTrainingBattle &&
            (!IsMission || mission.PlanningTimer && (guide == null || guide.IsFree));
        /// <summary>What 넘기기 costs right now: the planning time it spends, or 0 while the clock is held.</summary>
        public float LaneCycleCost => PlanningTimerRuns ? LaneCycleTimeCost : 0f;
        /// <summary>Whether enough planning time is left to pay for 넘기기.</summary>
        public bool CanAffordLaneCycle => LegacyPlanningClock.CanAfford(planningTime, LaneCycleTimeCost, PlanningTimerRuns);
        /// <summary>The planning time reserving the front skill of <paramref name="lane"/> costs right now (its 예약 효과
        /// 제한시간 -n초), or 0 while the clock is held or for a skill without one.</summary>
        public float ReservationTimeCost(int lane)
        {
            if (!PlanningTimerRuns || session == null || lane < 0 || lane > 2) return 0f;
            System.Collections.Generic.IReadOnlyList<LegacySkill> skills = session.GetLane(lane);
            return skills.Count == 0 ? 0f : LegacyPlanningClock.ReservationSeconds(skills[0]);
        }

        private void Awake()
        {
            campaign = new CampaignRun();
            prologue = new PrologueRun();
            queueLaneOnRelease = QueueLane;
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
            hud.SetStatusArena(arena);
            stepHud = new DuelStepHud(hud.Root.transform, art, presentationSettings);
            stepAudio = new DuelStepAudio(transform);
            resistanceFeedback = new DuelResistanceFeedback(hud.Root.transform, art.UIFont);
            breakImpactCue = new DuelBreakImpactCue(hud.Root.transform);
            skillActivationCue = new DuelSkillActivationCue(hud.Root.transform, art.UIFont);
            skillLevelUpCue = new DuelSkillLevelUpCue(transform, art.UIFont);
            // A cutscene's lines use the same dialogue box: its buttons advance or skip the cutscene instead.
            dialogueHud = new DialogueHud(transform, art,
                () => { if (IsPlayingCutscene) AdvanceCutsceneFromPointer(); else ContinueDialogue(); },
                () => { if (IsPlayingCutscene) RequestCutsceneSkip(true); else FinishDialogue(); });
            cutsceneHud = new CutsceneHud(transform, art);
            finale = new DuelFinale(transform, arena, amount => FadeDuelOverlays(amount));
            empowermentCues = new DuelEmpowermentCues(transform, arena, () => presentationSettings);
            gearShimmer = new DuelGearShimmer(hud, () => presentationSettings);
            meshCues = new DuelMeshCues(hud, () => presentationSettings);
            barks = new DuelBarks(hud, art.UIFont, () => presentationSettings);
            startCard = new DuelStartCard(transform, art, () => SkipStartCard());
            forestAmbience = new DuelForestAmbience(transform, () => presentationSettings);
            Sprite lobbyRoomSprite = LobbyRoomBackdrop.PickRandom();
            lobbyHud = new CampaignLobbyHud(transform, art,
                id => LearnCurriculumNode(id), () => ResetCurriculum(), id => EquipSkill(id),
                id => UnequipSkill(id), (id, direction) => MoveEquippedSkill(id, direction),
                stage => StartCampaignStage(stage), RestartJourney,
                (id, lane, slot) => PlaceLoadoutSkill(id, lane, slot),
                () => SaveLoadout(), () => ResetLoadout(), () => OpenNextMission(), lobbyRoomSprite,
                () => StartTraining(), equipCounter: id => EquipCounter(id));
            academyField = new AcademyFieldView(transform, art, arena,
                stage => StartCampaignStage(stage), () => OpenNextMission(),
                () => StartTraining(), () => OpenFieldMenu(), id => CollectFieldPickup(id));
            resultHud = new BattleResultHud(transform, art, () => DismissBattleResult(),
                () => RetryBattleResult(), () => AdvanceFromBattleResult());
            pauseHud = new DuelPauseHud(transform, art, () => ResumeBattle(),
                () => RetryPausedBattle(), () => AbandonPausedBattle());
            coachHud = new MissionCoachHud(transform, art, () => AdvanceGuide(),
                () => AbandonBattle(), () => InspectGuideEnemy());
            recallAlbum = new TutorialRecallAlbum(this);
            briefingHud = new MissionBriefingHud(transform, art, () => StartMission(), () => LeaveBriefing());
            titleHud = new TitleHud(transform, art, () => ContinueGame(), () => NewGameFromTitle(), lobbyRoomSprite,
                () => StartOnlineVersus(), () => StartSkillTavern());
            InitializeSkillTavern();
            // The online friend duel's battle plays on the same arena with its own duel HUD, timed by the same presentation
            // settings. Its 타이틀로 goes back to the title, which also closes the online room (ShowTitle stops both).
            localVersus = new LocalVersusController(transform, art, arena, () => ShowTitle(), presentationSettings);
            onlineVersus = new OnlineVersusController(transform, art, localVersus, () => ShowTitle());
            loadingHud = DuelLoadingHud.Create(transform, art.UIFont);
            if (!combatDebugInstance) saveStore = new GameSaveStore(GameSaveStore.DefaultPath);
            session = campaign.CreateDuel(System.Environment.TickCount, MeshPercent);
            ResetBattlePresentation();
            lobbyHud.ResetView();
            if (combatDebugInstance) BeginCombatDebugLoop();
            else ShowTitle();
        }

        private void Update()
        {
            AdvancePresentation(Time.unscaledDeltaTime, Keyboard.current);
        }

        private void AdvancePresentation(float realDelta, Keyboard keyboard)
        {
            realDelta = Mathf.Max(0f, realDelta);
            if (AdvanceCombatDebug(realDelta, keyboard)) return;
            if (AdvanceSkillTavern(keyboard)) return;
            // The online room ticks its battle itself (the versus duel only runs under it).
            if (onlineVersus != null && onlineVersus.IsActive)
            {
                onlineVersus.Tick(realDelta, keyboard);
                return;
            }
            if (IsPaused)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ResumeBattle();
                return;
            }
            // Break impulses use real time even during the finale's frozen frame and the result hand-off.
            hud.AdvanceBreakQueueFragments(realDelta);
            breakImpactCue.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            // Level-up is its own overlay, so it can finish over a result even after the duel HUD closes.
            skillLevelUpCue.Tick(realDelta, arena.ArenaCamera, arena.PlayerRenderer.transform);
            // A step judged on the last skill still finishes its real-time result during the turn hand-off.
            stepHud.Tick(realDelta);
            // The 수훈 hum fades out on whatever screen follows the battle.
            empowermentCues.TickAudio(realDelta);
            // The forest's bed follows forest scenes only; indoor fights use their own quiet room tone.
            forestAmbience.Tick(realDelta, ForestIsTheScene, ForestVisibility, StoryLoopLevel);
            if (IsPlayingCutscene)
            {
                if (Time.frameCount > cutsceneOpenedFrame && Mouse.current != null &&
                    Mouse.current.leftButton.wasPressedThisFrame && !dialogueHud.IsChoosing)
                {
                    // Remember the state at press time: the text may finish naturally before Button.onClick runs.
                    pointerPressedDuringReveal = dialogueHud.IsRevealing;
                    RegisterOpeningClick();
                }
                // Keys of the frame that opened it (e.g. the title's Enter) belong to the screen before.
                if (keyboard != null && Time.frameCount > cutsceneOpenedFrame)
                {
                    if (dialogueHud.IsChoosing)
                    {
                        if (keyboard.upArrowKey.wasPressedThisFrame) dialogueHud.MoveChoiceSelection(-1);
                        else if (keyboard.downArrowKey.wasPressedThisFrame) dialogueHud.MoveChoiceSelection(1);
                        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                            dialogueHud.ConfirmChoiceSelection();
                        return;
                    }
                    if (keyboard.escapeKey.wasPressedThisFrame)
                    {
                        RequestCutsceneSkip();
                        return;
                    }
                    if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                    {
                        if (dialogueHud.IsRevealing) dialogueHud.CompleteReveal();
                        else AdvanceCutscene();
                    }
                }
                if (IsPlayingCutscene)
                {
                    cutscene.Tick(realDelta);
                    ChooseSavedOpeningReplayRoute();
                    dialogueHud.Tick(realDelta);
                    if (openingBranchEnabled && !openingInterruptTriggered)
                    {
                        bool canInterrupt = IsOpeningSkipWindow;
                        cutsceneHud.SetSkipHint(canInterrupt ? "Esc  선택으로" : "Esc  건너뛰기");
                        dialogueHud.SetCinematicHint(canInterrupt
                            ? "클릭 / Enter / Space로 글 완성·다음  ·  Esc로 선택" : null);
                    }
                    if (cutscene.IsComplete) FinishCutscene();
                }
                // Never let the key that advances or ends the cutscene reach the next screen in the same frame.
                return;
            }
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
            if (IsInAcademyField)
            {
                if (lobbyHud.IsVisible)
                {
                    if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseFieldMenu();
                }
                else academyField.Tick(realDelta, keyboard, Mouse.current);
                return;
            }
            if (IsInLobby)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) lobbyHud.ShowTab(LobbyTab.Home);
                else if (keyboard != null && keyboard.enterKey.wasPressedThisFrame && lobbyHud.CurrentTab == LobbyTab.Stages)
                    StartCampaignStage(lobbyHud.SelectedStageNumber);
                return;
            }
            if (startCard.IsShowing)
            {
                AdvanceStartCard(realDelta, keyboard);
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                // A decided battle keeps its established skip-to-result behavior.
                if (session.IsFinished || finale.IsRunning) AbandonBattle();
                else PauseBattle();
                return;
            }
            // The finishing blow's freeze frame (and the 서막's final fall into black and white after it): nothing moves, not
            // the fighters, the camera or the HUD's numbers, until it hands over to the result or the outro.
            if (finale.IsFrozen)
            {
                finale.Tick(realDelta);
                if (finale.IsComplete) CompleteFinale();
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
            // Planning slows while Tab inspects the enemy's queue or the player reads a held skill's explanation, the clock and
            // 전투's bullet-time drift alike. Otherwise slow motion belongs to decisive moments only (a break, a finishing
            // blow or a heavy hit), and step successes.
            float speed = CanChoose ? PlanningTimeScale
                : IsResolving && arena.IsFatalFocus ? LegacyArenaView.DecisiveSlowMotionScale : 1f;
            // Keep local cinematic slow motion on the established combat clock;
            // the step gesture, camera and feedback lifetime still use real time.
            if (IsResolving) speed = Mathf.Min(speed, arena.StepPresentationSpeed);
            // The finishing blow's longer slow motion takes over from the decisive close-up's.
            if (finale.IsRunning) speed = finale.CombatSpeed;
            // Each later slot plays faster, with an optional multiplier once one queue is exhausted. Step windows keep their
            // real time, and a cinematic slow motion still owns its clock. The old streak is a tuning fallback only.
            else if (IsResolving && speed >= 1f) speed *= ResolutionPlaybackSpeed;
            float stoppedTime = Mathf.Min(realDelta, hitStopRemaining);
            hitStopRemaining = Mathf.Max(0f, hitStopRemaining - stoppedTime);
            // The hit that brought the enemy to the event's threshold has landed and its hit stop has played out.
            // Pause here for the scene; remaining hits and slots are discarded when it ends.
            if (missionEventPending && hitStopRemaining <= 0f)
            {
                OpenMissionEvent();
                return;
            }
            // A blade bind holds the battle's clock after the hit stop: the two fighters struggle on its real time.
            float exchangeTime = bladeExchange.Hold(realDelta - stoppedTime, arena);
            float boundTime = AdvanceBladeBind(realDelta - stoppedTime - exchangeTime);
            // A 수훈 technique's cut-in holds the battle after the hit stop (its camera ease and flare run meanwhile).
            float liveTime = realDelta - stoppedTime - exchangeTime - boundTime;
            liveTime -= empowermentCues.HoldBattle(liveTime);
            empowermentCues.Tick(realDelta);
            // The finishing blow's slow motion runs on the time the hit stop left; its freeze begins once it is over.
            if (finale.IsRunning)
            {
                finale.Tick(liveTime);
                if (finale.IsComplete)
                {
                    CompleteFinale();
                    return;
                }
            }
            float delta = liveTime * speed;
            // 전투 planning is bullet time in the arena only; the planning clock above is unchanged.
            arena.SetPlanningState(planningTime, IsInspecting, CanChoose && Encounter == EncounterKind.Battle);
            // The running clock's last share pushes the camera in a touch (not untimed, not while a coached beat holds it).
            arena.SetTimePressure(CanChoose && PlanningTimerRuns
                ? LegacyTimePressure.Target(planningTime, turnPlanningDuration, presentationSettings.TimePressureShare) : 0f);
            // The fighters' feet stay above the duel HUD's queue panels (as last laid out).
            arena.SetHudFloor(hud.QueuePanelFloor);
            // A Major slot (and a blade bind) pushes the combat framing in a touch.
            arena.SetDirectorPush(DirectorCameraPush);
            // 긴장 조명: both fighters' health as the rules have it now lights the arena (not in training, where nothing
            // is at stake; the dummy never falls).
            if (!IsTrainingBattle)
                arena.SetTensionLighting(BattleHealthReading(), presentationSettings.TensionLighting);
            RefreshUpcomingEnemy();
            if (!finale.IsRunning && !bladeExchange.IsHolding && session.CurrentSlot != null &&
                (viewPhase == ViewPhase.SkillWindup || viewPhase == ViewPhase.PlayingSlot))
            {
                float progress = viewPhase == ViewPhase.SkillWindup
                    ? .2f * Mathf.Clamp01(phaseTime / Mathf.Max(.001f, slotAnticipationDuration))
                    : .2f + .8f * Mathf.Clamp01(phaseTime / Mathf.Max(.001f, slotDuration));
                RequestCombatCameraShot(arena, progress, session.CurrentSlot.PlayerSkill ?? session.CurrentSlot.PendingPlayerCounter,
                    session.CurrentSlot.EnemySkill);
            }
            arena.Tick(delta, realDelta, deferSlowAfterimages: true);
            bladeExchange.Present(arena, art, effectsSource);
            phaseTime += delta;
            switch (viewPhase)
            {
                case ViewPhase.Planning:
                    if (CanChoose)
                    {
                        // The first planning turn on screen opens the battle's barks (start); later turns say nothing new.
                        barks.BattleStarted();
                        // 엘리사's eye: the enemy's queue for this turn has just come up.
                        if (enemyQueueRevealPending)
                        {
                            enemyQueueRevealPending = false;
                            gearShimmer.Reveal();
                        }
                    }
                    ReadPlanningInput(keyboard, realDelta);
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
                    if (arena.IsReadyForSlot) BeginSlotAnimation();
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
                    if (finishingHitPlayback)
                        arena.HoldSlotAtTime(Mathf.Min(finishingHitTime + phaseTime, finishingClipEnd));
                    // A finishing blow hands over from its freeze frame itself (CompleteFinale), without the return.
                    if (phaseTime >= turnCleanupDuration && !finale.IsRunning)
                    {
                        hud.BeginReturn();
                        arena.EndTurn();
                        SetViewPhase(ViewPhase.Settling);
                    }
                    break;
                case ViewPhase.Settling:
                    if (arena.ReturnComplete)
                    {
                        if (combatDebugInstance) BeginCombatDebugLoop();
                        else if (session.IsFinished)
                        {
                            FinishStage();
                        }
                        else StartPlanning();
                    }
                    break;
                case ViewPhase.Outcome:
                    break;
            }
            arena.TickPressureAttackTrail(realDelta, viewPhase == ViewPhase.PlayingSlot && !bladeExchange.IsHolding);
            arena.TickAttackMotionTrail(realDelta, viewPhase == ViewPhase.PlayingSlot && !bladeExchange.IsHolding);
            // A signature move's strike trail, sampled like the pressure trail after this frame's holds.
            arena.TickSignatureTrails(realDelta, viewPhase == ViewPhase.PlayingSlot && !bladeExchange.IsHolding);
            if (viewPhase != ViewPhase.Outcome) arena.SampleSlowAfterimages(realDelta);
            if (viewPhase != ViewPhase.Outcome)
            {
                RefreshHudClock(delta, realDelta);
            }
        }

        /// <summary>A frame of the battle's start card: it holds the battle (no planning clock, no input, no bullet time)
        /// while the fighters breathe behind it. Enter, Space or Escape skips it, as a click on it does; the key that skips
        /// it never reaches the battle, and keys of the frame that put it up belong to the screen before. The duel HUD comes
        /// back under it as it leaves; the coach once it has gone.</summary>
        private void AdvanceStartCard(float realDelta, Keyboard keyboard)
        {
            if (keyboard != null && Time.frameCount > startCardOpenedFrame && (keyboard.enterKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                SkipStartCard();
                return;
            }
            arena.SetPlanningState(planningTime, false);
            // Framed for the queue panels already, so the camera does not drift as the HUD comes back under the card.
            arena.SetHudFloor(hud.QueuePanelFloor);
            arena.Tick(realDelta, realDelta);
            if (startCard.Tick(realDelta)) EndStartCard();
            // The duel HUD comes back under the leaving card (its veil still takes the clicks).
            else if (startCard.IsLeaving && !hud.Root.activeSelf) hud.Root.SetActive(true);
            RefreshHudClock(0f, realDelta);
        }

        private void PrepareNextSlot(bool waitBetweenSlots = false)
        {
            // Keep original slot indices for forecasts, but a wholly canceled tail has no action to animate.
            if (HasOnlyBreakCanceledSlotsLeft(session, session.LastResolvedSlot + 1))
                while (!session.IsFinished && !session.IsTurnResolved) session.ResolveNextSlot();
            if (session.IsFinished || session.IsTurnResolved)
            {
                SetViewPhase(ViewPhase.AfterTurn);
                return;
            }
            highlightedSlot = session.LastResolvedSlot + 1;
            if (waitBetweenSlots)
            {
                // After a Minor slot the wait is shorter too (the director saves the time).
                // The exhausted attack's empty gap is compressed without speeding its clip or funding extra emphasis.
                betweenSlotsDuration = showDirector.ShortenGap(Mathf.Max(0f, presentationSettings.SkillInterval))
                    * showDirector.PlaybackGapScale;
                if (IsGroupBattle && ((ILegacyEnemyRosterView)session).EnemyIndexForSlot(highlightedSlot) != arena.ActiveEnemyIndex)
                    betweenSlotsDuration /= presentationSettings.EnemyHandoffSpeedMultiplier;
                if (betweenSlotsDuration > 0f)
                {
                    SetViewPhase(ViewPhase.BetweenSlots);
                    return;
                }
            }
            SelectArenaEnemyForSlot(highlightedSlot);
            arena.PrepareSlotMovement(session, highlightedSlot);
            SetViewPhase(ViewPhase.ClosingDistance);
            if (arena.IsReadyForSlot) BeginSlotAnimation();
        }

        internal static bool HasOnlyBreakCanceledSlotsLeft(ILegacyDuelView duel, int nextSlot)
        {
            if (duel is ILegacyEnemyRosterView roster && roster.Enemies.Count > 1)
            {
                if (duel.Phase != LegacyDuelPhase.Resolving || duel.CurrentSlot != null) return false;
                int count = Mathf.Max(duel.PlayerQueue.Count, duel.EnemyQueue.Count);
                for (int slot = nextSlot; slot < count; slot++)
                {
                    bool playerActs = slot < duel.PlayerQueue.Count && duel.PlayerQueue[slot] != null &&
                        (duel.PlayerBreakCanceledFromSlotIndex < 0 || slot < duel.PlayerBreakCanceledFromSlotIndex);
                    bool enemyActs = slot < duel.EnemyQueue.Count && duel.EnemyQueue[slot] != null && !roster.IsEnemySlotCanceled(slot);
                    if (playerActs || enemyActs) return false;
                }
                return true;
            }
            if (duel.Phase != LegacyDuelPhase.Resolving || duel.CurrentSlot != null ||
                (duel.PlayerBreakCanceledFromSlotIndex < 0 && duel.EnemyBreakCanceledFromSlotIndex < 0)) return false;
            bool playerEmpty = nextSlot >= duel.PlayerQueue.Count ||
                (duel.PlayerBreakCanceledFromSlotIndex >= 0 && nextSlot >= duel.PlayerBreakCanceledFromSlotIndex);
            bool enemyEmpty = nextSlot >= duel.EnemyQueue.Count ||
                (duel.EnemyBreakCanceledFromSlotIndex >= 0 && nextSlot >= duel.EnemyBreakCanceledFromSlotIndex);
            return playerEmpty && enemyEmpty;
        }

        // Uses the playback speed and attack gap snapshotted when the slot began.
        private float SlotDurationFor(int hits)
        {
            float clipDuration = OriginalClipDuration / slotPlaybackSpeed;
            return clipDuration * hits + slotAttackInterval * (hits - 1) + 0.01f;
        }

        private void BeginSlotAnimation()
        {
            LegacyFighterState facing = EnemyFacingNextSlot();
            int playerResistanceBefore = session.Player.Resistance;
            int enemyResistanceBefore = facing.Resistance;
            bool playerWasBroken = session.Player.IsResistanceBroken;
            bool enemyWasBroken = facing.IsResistanceBroken;
            LegacyCurrentSlot slot = session.BeginNextSlot();
            highlightedSlot = slot.SlotIndex;
            // The director rates what the slot's start already tells (a Major slot pushes the camera in from its windup).
            EnsureDirectorTurn();
            LegacySlotRank startRank = showDirector.BeginSlot(session.CurrentSlotFacts);
            ShowSlotWeight(slot.SlotIndex, startRank);
            slotPlaybackSpeed = Mathf.Max(0.01f, presentationSettings.AnimationPlaybackSpeed);
            slotAttackInterval = Mathf.Max(0f, presentationSettings.AttackInterval);
            float clipDuration = OriginalClipDuration / slotPlaybackSpeed;
            slotImpactTime = OriginalAttackEventTime / slotPlaybackSpeed;
            slotCycleDuration = clipDuration + slotAttackInterval;
            // The windup is the steps' warning; a meshed slot (맞물림) takes no steps, so it strikes without one.
            slotAnticipationDuration = StepFeatures.AllowsAnyStep() && !slot.PlayerMesh.IsMeshed
                ? presentationSettings.StepAnticipationDuration : 0f;
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
            // A signature move's windup glow gathers to its first strike (the steps' windup is untouched).
            BeginSignatures(startRank, playerAction, slot.EnemySkill, slotAnticipationDuration + slotImpactTime);
            // 자동 회피: the skill steps back from the attack by itself as the slot starts, with the dodge's sound but none of a
            // timed step's slow motion; A is not taken in this slot (D still is).
            if (slot.AutoDodge && slot.DodgeSucceeded)
            {
                arena.PerformStep(LegacyStepAction.Dodge, true, session.StepSuccessStreak, cinematic: false);
                stepAudio.Play(LegacyStepAction.Dodge, true, presentationSettings.StepSoundVolume);
            }
            // One of 이아's 수훈 techniques opens with its cut-in; the slot's clock waits for it (HoldBattle).
            if (missionEmpowered) empowermentCues.TryBeginCutIn(mission.Empowerment, slot.EnemySkill);
            hud.SetCurrentSkills(playerAction, slot.EnemySkill, slotDuration + slotAnticipationDuration);
            skillActivationCue.Reset();
            hud.SetSkillFeedback(slot);
            if (!slotStartDeferred) ShowSkillActivationCues(slot);
            if (slot.EnemyCountered) hud.ShowCounterCallout(false, arena.EnemyRenderer.transform.position);
            // Report the actual capped change without treating recovery or
            // direct resistance loss as an animation hit or knockback.
            resistanceFeedback.Show(true, session.Player.Resistance - playerResistanceBefore);
            resistanceFeedback.Show(false, session.Enemy.Resistance - enemyResistanceBefore);
            AnnounceBreaks(playerWasBroken, enemyWasBroken);
            // A slot's opening effects can break resistance (라우다레's 상대 붕괴), which the barks hear too.
            barks.Hit(Blow(session.Enemy, enemyWasBroken, 0), Blow(session.Player, playerWasBroken, 0));
            SetViewPhase(slotAnticipationDuration > 0f ? ViewPhase.SkillWindup : ViewPhase.PlayingSlot);
        }

        private void ShowSkillActivationCues(LegacyCurrentSlot slot)
        {
            skillActivationCue.Show(true, slot.PlayerSkill, slot.PlayerFeedback, KeywordCue(slot));
            skillActivationCue.Show(false, slot.EnemySkill, slot.EnemyFeedback);
        }

        // The player's slot's keyword effects as it starts: a count-ahead payoff's bonus, next turn's time (only when the
        // clock ran on the turn being resolved: a held clock gains nothing), an automatic dodge.
        private string KeywordCue(LegacyCurrentSlot slot)
        {
            var words = new System.Collections.Generic.List<string>(3);
            string ahead = LegacyKeywordPreview.AheadPowerCue(session.PlayerQueue, slot.SlotIndex);
            if (ahead != null) words.Add(ahead);
            string time = committedTurnClockRan ? LegacyKeywordPreview.AheadTimeCue(slot.PlanningSecondsGained) : null;
            if (time != null) words.Add(time);
            if (slot.AutoDodge && slot.DodgeSucceeded) words.Add(LegacyKeywordPreview.AutoDodgeCue);
            return words.Count == 0 ? null : string.Join(" · ", words);
        }

        private void UpdateSlot(float delta)
        {
            if (bladeBindHitPending || bladeExchange.IsHolding) return;
            LegacyCurrentSlot slot = session.CurrentSlot;
            float attackTime = slotImpactTime + slotCycleDuration * slot.HitsResolved;
            float animationClipEnd = slotCycleDuration * slot.HitsResolved + OriginalClipDuration / slotPlaybackSpeed;
            if (!slot.IsResolved && phaseTime >= attackTime)
            {
                bool firstStepImpact = slot.HitsResolved == 0 && (slot.DodgeSucceeded || slot.PressureSucceeded);
                if (!releasingBladeBindHit && !arena.IsReadyForSlot && !firstStepImpact)
                {
                    // Movement keeps running while this contact frame waits.
                    // Pausing the slot clock prevents off-screen air strikes or
                    // several deferred hits firing together after a long chase.
                    if (!arena.IsPursuing) arena.CloseDistance(delta);
                    if (!arena.IsReadyForSlot)
                    {
                        phaseTime = attackTime;
                        arena.HoldSlotAtTime(phaseTime);
                        return;
                    }
                }
                if (TryBeginBladeExchange(slot, attackTime)) return;
                // The blades touch now, but this first hit lands only when their struggle releases.
                if (!releasingBladeBindHit && TryBeginBladeBind(slot.SlotIndex, attackTime)) return;
                int playerResistanceBefore = session.Player.Resistance;
                int enemyResistanceBefore = session.Enemy.Resistance;
                int playerHealthBefore = session.Player.Health;
                int enemyHealthBefore = session.Enemy.Health;
                bool playerWasBroken = session.Player.IsResistanceBroken;
                bool enemyWasBroken = session.Enemy.IsResistanceBroken;
                LegacyHitResult hit = session.ResolveNextHit();
                arena.ObserveCombatContact();
                arena.PresentWeaponTrailContact(hit.HitIndex, hit.PlayerAttacked, hit.EnemyAttacked);
                // The queue panels' clash motion reads the hit now: a finishing blow completes its slot before the HUD's frame.
                hud.ObserveClash(session);
                // The director rates the slot again (a break, a heavy hit, a fall): its weight never drops within the slot.
                ShowSlotWeight(slot.SlotIndex, showDirector.Observe(session.CurrentSlotFacts));
                // 맞물림: the meshed slot's gears flash at the player as its first hit lands, where the rings would have been.
                if (hit.HitIndex == 0) meshCues.ShowSlotFlash(hit.PlayerMesh);
                if (hit.HitIndex == 0 && slotStartDeferred)
                {
                    // The slot's initial effects waited for the counter decision; report them now,
                    // excluding this hit's own resistance loss, which the hit presents itself.
                    slotStartDeferred = false;
                    hud.SetSkillFeedback(slot);
                    ShowSkillActivationCues(slot);
                    resistanceFeedback.Show(true, session.Player.Resistance - playerResistanceBefore + hit.PlayerResistanceDamage);
                    resistanceFeedback.Show(false, session.Enemy.Resistance - enemyResistanceBefore + hit.EnemyResistanceDamage);
                    if (slot.PlayerCountered) hud.ShowCounterCallout(true, arena.PlayerRenderer.transform.position);
                }
                if (hit.PlayerAttacked) playerSlotDamage = hit.EnemyDisplayedDamage;
                if (hit.EnemyAttacked) enemySlotDamage = hit.PlayerDisplayedDamage;
                bool enemyBrokeNow = enemyResistanceBefore > 0 && session.Enemy.Resistance <= 0;
                bool playerBrokeNow = playerResistanceBefore > 0 && session.Player.Resistance <= 0;
                bool decisiveNow = false;
                if (hit.PlayerAttacked)
                    decisiveNow |= PresentHit(true, hit.EnemyHealthDamage - hit.EnemyBleedingHealthDamage, hit.EnemyResistanceDamage,
                        hit.EnemyDisplayedDamage, hit.EnemyPushPower,
                        hit.EnemySkill?.Kind == LegacySkillKind.Defence,
                        enemyBrokeNow,
                        enemyHealthBefore > 0 && enemyHealthBefore - (hit.EnemyHealthDamage - hit.EnemyBleedingHealthDamage) <= 0,
                        Exchange(hit.EnemySkill, hit.EnemyAttacked), hit.EnemyDamageReasons, hit.PlayerSkill);
                if (hit.EnemyAttacked && !hit.PlayerDodged)
                {
                    decisiveNow |= PresentHit(false, hit.PlayerHealthDamage - hit.PlayerBleedingHealthDamage, hit.PlayerResistanceDamage,
                        hit.PlayerDisplayedDamage, hit.PlayerPushPower,
                        hit.PlayerSkill?.Kind == LegacySkillKind.Defence,
                        playerBrokeNow,
                        playerHealthBefore > 0 && playerHealthBefore - (hit.PlayerHealthDamage - hit.PlayerBleedingHealthDamage) <= 0,
                        Exchange(hit.PlayerSkill, hit.PlayerAttacked), hit.PlayerDamageReasons, hit.EnemySkill);
                    if (missionEmpowered) empowermentCues.NotifyEnemyHit(mission.Empowerment, hit.EnemySkill);
                }
                hud.ShowBleedingDamage(true, hit.PlayerBleedingHealthDamage, arena.PlayerRenderer.transform.position);
                hud.ShowBleedingDamage(false, hit.EnemyBleedingHealthDamage, arena.EnemyRenderer.transform.position);
                // A Major slot's first presented hit: a longer stop and a second impact under it (never the finishing
                // blow's, which keeps its finale).
                if (hit.Outcome == DuelMatchOutcome.InProgress && (hit.PlayerAttacked || hit.EnemyAttacked && !hit.PlayerDodged))
                    PresentMajorMoment(decisiveNow);
                // Includes a deferred counter slot's start effects, which run inside this hit.
                AnnounceBreaks(playerWasBroken, enemyWasBroken,
                    hit.EnemyAttacked && !hit.PlayerDodged, hit.PlayerAttacked);
                // The first hit that brings the enemy to a mission event's threshold (never one that ends the duel).
                if (hit.EnemyReachedHealthThreshold && mission?.Empowerment != null && !missionEmpowered)
                    missionEventPending = true;
                // 간파: the player's read paid off; cued once per slot at the hit that shows it (not under an event's scene).
                if (!missionEventPending) CueRead(slot.SlotIndex);
                // The barks hear a hit the battle goes on after. The finishing blow is the finale's, and the event's
                // scene answers the hit that opens it.
                if (hit.Outcome == DuelMatchOutcome.InProgress && !missionEventPending)
                    barks.Hit(Blow(session.Enemy, enemyWasBroken, hit.EnemyHealthDamage - hit.EnemyBleedingHealthDamage),
                        Blow(session.Player, playerWasBroken, hit.PlayerHealthDamage - hit.PlayerBleedingHealthDamage));
                // A Minor slot (no attack) plays its rest after its last hit faster, as the director saves time.
                if (hit.Outcome == DuelMatchOutcome.InProgress && slot.IsResolved && showDirector.ShortensSlot)
                {
                    slotDuration = attackTime + showDirector.ShortenSlotRest(Mathf.Max(0f, slotDuration - attackTime));
                    hud.SetCurrentSlotDuration(slotDuration + slotAnticipationDuration);
                }
                if (hit.Outcome != DuelMatchOutcome.InProgress)
                {
                    BeginFinale(hit.Outcome);
                    // Finish this strike's pose without allowing another attack cycle.
                    finishingHitPlayback = true;
                    finishingHitTime = attackTime;
                    finishingClipEnd = Mathf.Max(attackTime, animationClipEnd - .0001f);
                    slotDuration = animationClipEnd;
                    hud.SetCurrentSlotDuration(slotDuration + slotAnticipationDuration);
                    arena.HoldSlotAtTime(attackTime);
                    CompleteResolvedSkillSlot(slot);
                    return;
                }
                // One member falling ends this skill's remaining cycles, while the other members keep the battle going.
                if (IsGroupBattle && session.Enemy.IsDefeated && slot.IsResolved)
                {
                    slotDuration = animationClipEnd;
                    hud.SetCurrentSlotDuration(slotDuration + slotAnticipationDuration);
                }
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
                CompleteResolvedSkillSlot(slot);
            }
        }

        private void CompleteResolvedSkillSlot(LegacyCurrentSlot slot)
        {
            RecordResolvedSkillSlot(slot);
            session.CompleteCurrentSlot();
            showDirector.EndSlot();
            PrepareNextSlot(true);
        }

        // The director takes this turn's numbers once per turn: at the commit, or at a slot begun without one (tests).
        private void EnsureDirectorTurn()
        {
            if (ReferenceEquals(directorSession, session) && directorRound == session.RoundNumber) return;
            directorSession = session;
            directorRound = session.RoundNumber;
            bladeBindRemaining = 0f;
            bladeBindHitPending = false;
            showDirector.BeginTurn(presentationSettings.ShowPacing, presentationSettings.SlotImportanceTuning(),
                presentationSettings.UnopposedStreak, presentationSettings.CombatTempo);
        }

        // Forgets the director's turn and any bind (a reset, a new battle, the lobby, the result).
        private void ClearShowDirector()
        {
            showDirector.Clear();
            directorSession = null;
            directorRound = -1;
            bladeBindRemaining = 0f;
            bladeBindHitPending = releasingBladeBindHit = false;
            bladeExchange.Clear();
            arena.ClearBladeBind();
        }

        // Playback may speed a slot up from its start only when nothing before its first hit can change it: no step can be
        // taken there (the steps are closed, or the slot is meshed) and no 반격 waits to fill it. Otherwise it waits for the
        // first hit, so the windup and the step window keep their real time.
        private bool StreakFromSlotStart(LegacyCurrentSlot slot)
            => slot.PendingPlayerCounter == null && (!StepFeatures.AllowsAnyStep() || slot.PlayerMesh.IsMeshed);

        // A slot's weight on the queue panels' seam: a Major pair goes farther.
        private void ShowSlotWeight(int slotIndex, LegacySlotRank rank)
            => hud.SetClashEmphasis(slotIndex, showDirector.Enabled && rank.Tier == LegacySlotTier.Major
                ? presentationSettings.MajorClashScale : 1f);

        // Presentation intent is read from the current exchange only. It never advances a skill or the battle clock.
        internal static void RequestCombatCameraShot(LegacyArenaView target, float progress,
            LegacySkill playerSkill, LegacySkill enemySkill)
        {
            bool playerAttacks = playerSkill?.Kind == LegacySkillKind.Attack;
            bool enemyAttacks = enemySkill?.Kind == LegacySkillKind.Attack;
            bool unopposed = playerAttacks && (enemySkill == null || enemySkill.IsWait) ||
                enemyAttacks && (playerSkill == null || playerSkill.IsWait);
            target.SetCombatShot(progress, playerAttacks, enemyAttacks, unopposed);
        }

        // The camera push this frame: a Major slot's from its start to its end, part of it through a blade bind.
        private float DirectorCameraPush
        {
            get
            {
                if (!IsResolving) return 0f;
                float push = presentationSettings.MajorCameraPush;
                bool slotPlays = viewPhase == ViewPhase.SkillWindup || viewPhase == ViewPhase.PlayingSlot;
                float major = showDirector.Enabled && slotPlays && showDirector.IsMajor ? push : 0f;
                // A signature move comes closer still, from its windup (its treatment's own push).
                if (slotPlays && showDirector.IsSignature && presentationSettings.SignatureTreatmentEnabled)
                    major = Mathf.Max(major, presentationSettings.SignatureCameraPush);
                return bladeBindRemaining > 0f ? Mathf.Max(major, push * BladeBindCameraShare) : major;
            }
        }

        // A signature move on either side (2026-10-15): its windup glow in its school's dye and its strike trail.
        private void BeginSignatures(LegacySlotRank start, LegacySkill playerSkill, LegacySkill enemySkill, float windup)
        {
            if (!presentationSettings.SignatureTreatmentEnabled) return;
            if (start.PlayerSignature) BeginSignature(arena, presentationSettings, true, playerSkill, windup);
            if (start.EnemySignature) BeginSignature(arena, presentationSettings, false, enemySkill, windup);
        }

        // One side's signature treatment on the arena (versus uses it too).
        internal static void BeginSignature(LegacyArenaView arena, DuelPresentationSettings settings, bool playerSide,
            LegacySkill skill, float windup)
            => arena.BeginSignature(playerSide, SignatureInk(skill), windup, settings.SignatureWindupGlow,
                settings.SignatureAfterimageSeconds, settings.SignatureAfterimageAlpha);

        /// <summary>A signature move's ink: its school's dye (<see cref="SkillLaneStyle.Edge"/>: 정공 gold, 강공 teal, 기교
        /// terracotta), the HUD's brass for a skill outside the three.</summary>
        internal static Color SignatureInk(LegacySkill skill)
            => skill != null && skill.LaneIndex >= 0 && skill.LaneIndex <= 2 ? SkillLaneStyle.Edge(skill.LaneIndex)
                : DuelVisualTheme.Accent;

        // 간파 (2026-10-15): the player's read paid off in this slot. Once per slot (the director keeps count): the gold
        // burst, ring and stamp on the player's seam card, and a brief light on the player's fighter. It takes no time.
        private void CueRead(int slotIndex)
        {
            if (showDirector.TakeReadCue() == LegacyReadEvent.None || !presentationSettings.ReadCueEnabled) return;
            hud.ShowReadCue(slotIndex);
            if (presentationSettings.ReadFighterAccent) arena.AccentFighter(true, presentationSettings.ReadCueInk);
        }

        // A Major slot's first presented hit (the director's Major moment): its stop lengthens and a second impact sounds
        // under it, unless the decisive close-up's critical sound already does.
        private void PresentMajorMoment(bool decisive)
        {
            bool before = showDirector.MajorMomentGiven;
            float stop = showDirector.HitStop(presentationSettings.HitStopDuration);
            if (before || !showDirector.MajorMomentGiven) return;
            hitStopRemaining = Mathf.Max(hitStopRemaining, stop);
            if (!decisive) art.PlayClash(effectsSource, false, Random.value < .5f, presentationSettings.MajorImpactVolume);
            // A signature move rings once more: the blade's steel over the impact.
            if (showDirector.IsSignature && presentationSettings.SignatureTreatmentEnabled)
                art.PlayClash(effectsSource, true, false, presentationSettings.SignatureImpactVolume);
        }

        // The director's blade bind for this slot, as long as the turn can afford: the arena's fighters and the seam's cards
        // hold at contact on the same real seconds. The rules and damage presentation wait for the blades to spring apart.
        private bool TryBeginBladeBind(int slotIndex, float attackTime)
        {
            if (!showDirector.Enabled || !showDirector.IsBladeBindCandidate || missionEventPending ||
                !session.CanBindCurrentHit()) return false;
            // Allocate the contact's emphasis before its bind, as for an ordinary hit. A refused bind still gets the
            // normal impact below; releasing an accepted bind must not add another stop to the turn's budget.
            PresentMajorMoment(false);
            float seconds = showDirector.BladeBind();
            if (!(seconds > 0f)) return false;
            phaseTime = attackTime;
            arena.HoldSlotAtTime(attackTime);
            hitStopRemaining = Mathf.Max(hitStopRemaining, presentationSettings.HitStopDuration);
            bladeBindHitPending = true;
            bladeBindRemaining = seconds;
            arena.PresentBladeBindContact();
            arena.BeginBladeBind(seconds, presentationSettings.BladeBindSway, presentationSettings.BladeBindPress,
                presentationSettings.BladeBindSpring, presentationSettings.BladeBindSparks);
            hud.ObserveClash(session);
            hud.BindClash(slotIndex, seconds, presentationSettings.BladeBindSparks);
            art.PlayClash(effectsSource, true, false, .4f);
            return true;
        }

        // Plays up to <paramref name="available"/> real seconds of the blade bind and returns what it took (0 while none).
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
                // Resolve exactly once, while the arena can still share the hit's pushes in FinishBladeBind.
                try { UpdateSlot(0f); }
                finally { releasingBladeBindHit = false; }
            }
            arena.AdvanceBladeBind(bound);
            hud.AdvanceClashBind(bound);
            if (bladeBindRemaining <= 1e-5f)
            {
                bladeBindRemaining = 0f;
                arena.FinishBladeBind();
                hud.FinishClashBind();
                // Both spring apart alike: the blades let go of each other.
                art.PlayClash(effectsSource, true, true, .35f);
            }
            // Show the released hit for a frame even after a long stall; leftover time must not batch the next hit.
            return released ? Mathf.Max(bound, available) : bound;
        }

        private void RecordResolvedSkillSlot(LegacyCurrentSlot slot)
        {
            hud.RecordResolvedSlot(slot.PlayerSkill, slot.EnemySkill, playerSlotDamage, enemySlotDamage);
            if (combatDebugInstance || IsInSkillTavern) return;
            CampaignOwnedSkill owned = slot.PlayerSkill == null ? null : campaign.GetOwnedSkill(slot.PlayerSkill.Id);
            int previousLevel = owned?.Level ?? 0;
            if (campaign.TryGainClashExperience(slot.PlayerSkill, slot.EnemySkill))
            {
                skillExperienceDirty = true;
                if (owned.Level > previousLevel) skillLevelUpCue.Show(owned.Skill, owned.Level);
            }
        }

        /// <summary>Calls out every fighter whose resistance broke during the last rules step, whether a
        /// hit or a skill effect (such as 쿠페's direct reduction) broke it, and updates the break aura.</summary>
        private void AnnounceBreaks(bool playerWasBroken, bool enemyWasBroken,
            bool playerHitPresented = false, bool enemyHitPresented = false)
        {
            bool playerBroke = !playerWasBroken && session.Player.IsResistanceBroken;
            bool enemyBroke = !enemyWasBroken && session.Enemy.IsResistanceBroken;
            // A current skill can restore resistance after an opening effect broke it. Cancellation still happened.
            hud.PlayBreakQueueCancellation(true, session.PlayerBreakCanceledFromSlotIndex);
            hud.PlayBreakQueueCancellation(false, session.EnemyBreakCanceledFromSlotIndex);
            if (IsGroupBattle) hud.PlayEnemyQueueCancellation();
            if (playerBroke)
            {
                hud.ShowBreakCallout(true, arena.PlayerRenderer.transform.position, arena.PlayerRenderer.transform);
                breakImpactCue.Show(true);
            }
            if (enemyBroke)
            {
                hud.ShowBreakCallout(false, arena.EnemyRenderer.transform.position, arena.EnemyRenderer.transform);
                breakImpactCue.Show(false);
            }
            // A direct effect has no impact animation to play the decisive sound. A deferred effect can also
            // break someone whose hit was dodged, so use the presented hit for each target instead of the slot phase.
            bool needsBreakSound = (playerBroke && !playerHitPresented) || (enemyBroke && !enemyHitPresented);
            if (needsBreakSound) arena.PresentDecisiveFocus(!playerBroke);
            if (needsBreakSound && criticalSound != null)
            {
                effectsSource.pitch = playerBroke ? .82f : .96f;
                effectsSource.PlayOneShot(criticalSound, .72f);
            }
            RefreshEnemyRosterState();
        }

        /// <summary>What a fighter took in a moment, for the barks: whether its resistance broke just now, and the health it
        /// lost to the hit.</summary>
        private static BarkBlow Blow(LegacyFighterState fighter, bool wasBroken, int healthDamage)
            => new BarkBlow(!wasBroken && fighter.IsResistanceBroken, healthDamage, fighter.Health, fighter.MaxHealth);

        // The rules resolve an attack against the target's attack skill as a resistance
        // exchange for the whole slot, even after that skill's own hits have ended.
        internal static LegacyArenaView.HitExchange Exchange(LegacySkill targetSkill, bool targetStrikes) =>
            targetSkill?.Kind != LegacySkillKind.Attack ? LegacyArenaView.HitExchange.None
                : targetStrikes ? LegacyArenaView.HitExchange.MutualClash : LegacyArenaView.HitExchange.BladeBlock;

        // Returns whether the hit was decisive (a break, the finishing blow, a heavy hit).
        private bool PresentHit(bool playerAttacks, int healthDamage, int resistanceDamage,
            int displayedDamage, int pushPower, bool guarded, bool resistanceBroke, bool finishingBlow,
            LegacyArenaView.HitExchange exchange, LegacyDamageReason damageReasons, LegacySkill attackingSkill)
        {
            // Only decisive moments get the slow close-up, tilt and critical sound: a break, the finishing blow, or one hit
            // taking a set share of the target's maximum health (LegacyDecisiveHit). A merely big number is still gold and larger.
            bool decisive = LegacyDecisiveHit.IsDecisive(resistanceBroke, finishingBlow, healthDamage,
                (playerAttacks ? session.Enemy : session.Player).MaxHealth, presentationSettings.DecisiveHealthDamagePercent);
            bool emphasised = decisive || displayedDamage >= 12;
            // A resistance-only hit displays exactly its resistance loss. Anything more reached
            // the body, even when the health change was clamped at zero.
            bool bodyHit = healthDamage > 0 || displayedDamage > resistanceDamage;
            if (bodyHit) exchange = LegacyArenaView.HitExchange.None;
            // Steel only when the number is the resistance lost. A break with no HP overflow
            // is announced by its state callout instead of a misleading zero.
            bool resistanceNumber = !bodyHit && resistanceDamage > 0 && displayedDamage == resistanceDamage;
            bool mixedResources = bodyHit && resistanceDamage > 0 && !resistanceBroke;
            // Both sides of a simultaneous clash share the same real-time stop.
            if (!releasingBladeBindHit)
                hitStopRemaining = Mathf.Max(hitStopRemaining, presentationSettings.HitStopDuration);
            // A locked pair releases in its strike pose, including a large resistance-only number.
            arena.PresentHit(playerAttacks, healthDamage, resistanceDamage, guarded,
                emphasised && !releasingBladeBindHit, pushPower, exchange, decisive);
            if (decisive) hud.FatalAttack(playerAttacks);
            Transform target = playerAttacks ? arena.EnemyRenderer.transform : arena.PlayerRenderer.transform;
            hud.ShowHitDamage(!playerAttacks, displayedDamage, target.position, emphasised, resistanceNumber,
                damageReasons, resistanceBroke, attackingSkill, mixedResources);
            effectsSource.pitch = resistanceBroke ? (playerAttacks ? .96f : .82f) : Random.Range(0.75f, 1.25f);
            art.PlayClash(effectsSource, false, Random.value >= 0.5f);
            if (decisive && criticalSound != null)
                effectsSource.PlayOneShot(criticalSound, resistanceBroke ? .72f : .55f);
            return decisive;
        }

        private void StartPlanning()
        {
            if (IsInSkillTavernBattle) { FinishSkillTavernTurn(); return; }
            int playerResistanceBefore = session.Player.Resistance;
            int enemyResistanceBefore = session.Enemy.Resistance;
            int[] temporaryBefore = LegacyKeywordPreview.TemporaryCounts(session);
            CaptureEnemyTurnResistance();
            session.BeginNextTurn();
            ClearHeldKeys();
            // 제한시간: the last turn's skills may have earned this turn more time, but only if that turn's clock ran (a
            // coached or untimed turn earns nothing, even for a timed turn after it). A held clock now shows no gain.
            turnPlanningDuration = LegacyPlanningClock.CampaignTurnStart(CurrentPlanningDuration,
                committedTurnClockRan ? session.PlanningSecondsBonus : 0);
            planningTime = turnPlanningDuration;
            highlightedSlot = -1;
            inspectingEnemy = 0;
            laneHolds.DropExplanation();
            SelectArenaEnemyForSlot(0);
            arena.BeginTurn();
            hud.BeginTurn();
            skillActivationCue.Reset();
            float gained = turnPlanningDuration - CurrentPlanningDuration;
            if (PlanningTimerRuns && gained > 0f) hud.ShowTimeGained(gained);
            // The 일회성 copies left in a lane went with the turn.
            for (int lane = 0; lane < temporaryBefore.Length; lane++)
                if (temporaryBefore[lane] > 0) hud.ShowLanePop(lane, LegacyKeywordPreview.TemporaryExpired, false);
            // The automatic refill, a full turn after the one in which resistance broke.
            if (session.Player.Resistance > playerResistanceBefore)
                hud.ShowRecoveryCallout(true, arena.PlayerRenderer.transform.position, arena.PlayerRenderer.transform);
            AnnounceEnemyTurnRecovery(enemyResistanceBefore);
            RefreshEnemyRosterState();
            SetViewPhase(ViewPhase.Planning);
            // The enemy's new queue is up: 엘리사's eye plays over it on the turn's first frame.
            enemyQueueRevealPending = true;
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
            int previousStepStreak = session?.StepSuccessStreak ?? 0;
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
            stepHud.ShowFeedback(action, success, session.StepSuccessStreak, previousStepStreak);
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
            if (next == ViewPhase.AfterTurn) turnCleanupDuration = presentationSettings.TurnCleanupSeconds;
            if (next != ViewPhase.AfterTurn) finishingHitPlayback = false;
            viewPhase = next;
            phaseTime = 0f;
        }

        private void ReadPlanningInput(Keyboard keyboard, float realDelta)
        {
            if (keyboard != null && guide != null && guide.AllowsInspect && keyboard.tabKey.isPressed)
                InspectGuideEnemy();
            if (IsInspecting)
            {
                ClearHeldKeys();
                if (keyboard == null) return;
                if (keyboard.leftArrowKey.wasPressedThisFrame) inspectingEnemy = Mathf.Max(0, inspectingEnemy - 1);
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                    inspectingEnemy = Mathf.Min(session.EnemyQueue.Count - 1, inspectingEnemy + 1);
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) CommitTurn();
                return;
            }
            if (keyboard == null)
            {
                // Without a keyboard the lanes still answer the pointer held on their gears' windows.
                for (int lane = 0; lane < DuelLaneHolds.LaneCount; lane++) ReadLaneHold(lane, false, false, false, realDelta);
                return;
            }
            if (keyboard.lKey.wasPressedThisFrame) hud.ToggleLog();
            if (keyboard.sKey.wasPressedThisFrame) QueueBreath();
            // Before the lane keys, so a lane key pressed in the same frame is for the skill 넘기기 brought forward.
            if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame) CycleLanes();
            ReadLaneHold(0, keyboard.qKey.isPressed, keyboard.qKey.wasPressedThisFrame, keyboard.qKey.wasReleasedThisFrame, realDelta);
            ReadLaneHold(1, keyboard.wKey.isPressed, keyboard.wKey.wasPressedThisFrame, keyboard.wKey.wasReleasedThisFrame, realDelta);
            ReadLaneHold(2, keyboard.eKey.isPressed, keyboard.eKey.wasPressedThisFrame, keyboard.eKey.wasReleasedThisFrame, realDelta);
            if (keyboard.digit1Key.wasPressedThisFrame && IsLaneOpen(0)) QueueLane(0);
            if (keyboard.digit2Key.wasPressedThisFrame && IsLaneOpen(1)) QueueLane(1);
            if (keyboard.digit3Key.wasPressedThisFrame && IsLaneOpen(2)) QueueLane(2);
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) CommitTurn();
        }

        /// <summary>Whether this duel has the lane at all. A closed lane's keys do nothing: no hold bar, no
        /// explanation, no queue, and they do not close an explanation another lane holds open.</summary>
        private bool IsLaneOpen(int lane) => session != null && session.Features.HasLane(lane);

        /// <summary>A lane's hold this frame, from its key and from the pointer on its gear's window (<see
        /// cref="LegacyLaneHold"/>, real time): a tap queues on release; releasing while the bar fills safely cancels with
        /// a brief cue; held to <see cref="DuelPresentationSettings.ExplanationHoldSeconds"/> it opens the front skill's
        /// explanation, and that release only closes it. A press on another lane closes an open explanation.</summary>
        private void ReadLaneHold(int lane, bool keyHeld, bool keyPressed, bool keyReleased, float realDelta)
        {
            if (!IsLaneOpen(lane)) return;
            laneHolds.Read(lane, keyHeld, keyPressed, keyReleased, realDelta, hud, presentationSettings, session.GetLane(lane),
                queueLaneOnRelease);
        }

        public bool QueueLane(int lane)
        {
            if (!CanChoose || IsInspecting || lane < 0 || lane > 2 ||
                guide != null && !guide.AllowsQueue(lane)) return false;
            // The duel removes cancelled or returned reservations immediately. Keep their old order just long enough for the
            // HUD to let the cards tumble out before its normal queue refresh reuses those card objects.
            LegacySkill[] cancelledQueue = null;
            var laneSkills = session.GetLane(lane);
            LegacyQueueRemovalMode removal = laneSkills.Count > 0
                ? LegacySkillDefinitions.Find(laneSkills[0])?.Effect?.QueueRemovalMode ?? LegacyQueueRemovalMode.None
                : LegacyQueueRemovalMode.None;
            if (removal != LegacyQueueRemovalMode.None && session.PlayerQueue.Count > 0)
            {
                cancelledQueue = new LegacySkill[session.PlayerQueue.Count];
                for (int index = 0; index < cancelledQueue.Length; index++) cancelledQueue[index] = session.PlayerQueue[index];
            }
            // 예약 효과 제한시간 -n초: paid as the skill is reserved, refused when the running clock cannot spare n + 0.5 s.
            float timeCost = ReservationTimeCost(lane);
            if (!LegacyPlanningClock.CanAfford(planningTime, timeCost, PlanningTimerRuns)) return false;
            LegacySkill reserved = laneSkills.Count > 0 ? laneSkills[0] : null;
            bool temporaryBefore = (session.ReserveEffectsUsedThisTurn & LegacyReserveEffect.Temporary) != 0;
            if (!session.TryQueueLane(lane)) return false;
            if (timeCost > 0f)
            {
                planningTime = LegacyPlanningClock.Spend(planningTime, timeCost, PlanningTimerRuns);
                hud.ShowTimeSpent(timeCost);
                // 전투: bullet time lets go for a moment, as for 넘기기.
                arena.BreakBulletTime();
            }
            // 예약 효과 일회성: its copies came to the front of their lane.
            if (!temporaryBefore && (session.ReserveEffectsUsedThisTurn & LegacyReserveEffect.Temporary) != 0)
            {
                LegacySkill spawned = LegacyKeywordLinks.TemporarySpawn(reserved, out int copies);
                if (spawned != null) hud.ShowLanePop(spawned.LaneIndex, "+" + copies, true);
                skillActivationCue.ShowKeyword(true, reserved, LegacyKeywordPreview.TemporarySpawnCue(reserved));
            }
            if (cancelledQueue != null) hud.PlayQueueCancellation(cancelledQueue, removal);
            hud.ClearHoldCancel();
            guide?.NotifyQueued(lane);
            // The lane's gear turns a slot: the queued skill to the upper right, the next one up into the window.
            hud.PlayLaneTurn(lane);
            // 맞물림: when it made or lengthened a chain, gears bite on the seam in the queue row, with sparks and a sound.
            meshCues.Queued(session);
            // When the enemy holds a card in the slot just reserved into, a spark bursts at both cards (the forecast's look).
            hud.PlayReservationSpark(session.LastQueuedSlotIndex);
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, Random.Range(0, 3));
            laneHolds.DropExplanation();
            RefreshHud(0f);
            RefreshGuide();
            return true;
        }

        /// <summary>넘기기 (Shift): every open lane sends its front skill to the back unused. It costs planning time
        /// (<see cref="LaneCycleTimeCost"/>) while the clock runs, not ACT; all lanes turn together, so lining skills
        /// up across lanes means choosing which ones to use first.</summary>
        public bool CycleLanes()
        {
            if (!CanChoose || IsInspecting || guide != null && !guide.AllowsCycle || !CanAffordLaneCycle ||
                !session.TryCycleLanes()) return false;
            hud.ClearHoldCancel();
            float cost = LaneCycleCost;
            if (cost > 0f)
            {
                planningTime = Mathf.Max(0f, planningTime - cost);
                hud.ShowTimeSpent(cost);
                // 전투: bullet time lets go for a moment, as if that second had just passed.
                arena.BreakBulletTime();
            }
            // Every open lane's gear turns together, meshed (a one-skill lane's skill comes round again), and ratchets.
            hud.PlayLaneTurn(new[] { session.GetLane(0).Count > 0, session.GetLane(1).Count > 0, session.GetLane(2).Count > 0 });
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, Random.Range(0, 3));
            // A lane key already down was pressed for the skill that just left, so its release must not queue the
            // one that came forward. A held explanation picks up the new front on the next frame.
            laneHolds.ConsumeActive();
            laneHolds.DropExplanation();
            guide?.NotifyCycled();
            RefreshHud(0f);
            RefreshGuide();
            return true;
        }

        public bool QueueBreath()
        {
            // The duel refuses a closed 숨고르기; a coached mission also waits for its lesson beat.
            if (!CanChoose || IsInspecting || guide != null && !guide.AllowsBreath || !session.TryQueueBreath()) return false;
            hud.ClearHoldCancel();
            // A 숨고르기 takes a slot too: it meets whatever the enemy holds there.
            hud.PlayReservationSpark(session.PlayerQueue.Count - 1);
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, Random.Range(0, 3));
            laneHolds.DropExplanation();
            guide?.NotifyBreathed();
            RefreshHud(0f);
            RefreshGuide();
            return true;
        }

        public void CommitTurn()
        {
            if (!CanChoose || guide != null && !guide.AllowsCommit) return;
            // A turn committed under the start card (direct API use; the player's input waits for it) begins the battle.
            SkipStartCard();
            // The director's turn: its ledger starts afresh with the numbers as they are now.
            directorSession = null;
            ClearHeldKeys();
            laneHolds.DropExplanation();
            guideInspectionRemaining = 0f;
            hud.HideExplanation();
            // Before the guide moves on: the seconds this turn's skills earn count only if this turn's clock ran.
            committedTurnClockRan = PlanningTimerRuns;
            // 순환 +1 rises as the turn is committed: read what it raises first, and say so.
            string feed = LegacyKeywordPreview.CycleFeedCue(session.PlayerQueue, session.CycleUses);
            LegacySkill feeder = LegacyKeywordPreview.FirstCycleFeeder(session.PlayerQueue);
            session.Commit();
            SelectArenaEnemyForSlot(0);
            EnsureDirectorTurn();
            skillActivationCue.Reset();
            if (feed != null) skillActivationCue.ShowKeyword(true, feeder, feed);
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
            if (combatDebugInstance) { BeginCombatDebugLoop(); return; }
            CloseDialogue();
            ClearMissionState();
            campaign.Reset();
            SyncStoryProgression();
            campaign.TryStartStage(1);
            StartStageBattle();
        }

        /// <summary>Restarts the post-arc journey (the lobby's 여정 초기화): a fresh campaign, back in the lobby.
        /// The opening arc's progress is kept, and so are the 수련 점수 its won missions paid (one each, as a save written
        /// before points existed is credited). Like the story's unlocks (<see cref="SyncStoryProgression"/>) this follows
        /// the story only while it is in play; direct use without it (tests, API) starts the journey at zero.</summary>
        public void RestartJourney()
        {
            academyField?.ResetExploration();
            CloseDialogue();
            ClearMissionState();
            campaign.Reset(StoryProgressionEnabled ? prologue.ClearedCount : 0);
            SyncStoryProgression();
            session = campaign.CreateDuel(System.Environment.TickCount, MeshPercent);
            ResetBattlePresentation();
            lobbyHud.ResetView();
            ShowLobby();
            AutoSave();
        }

        /// <summary>A new game: a fresh campaign and the opening arc from its first mission briefing.</summary>
        public void StartNewGame() => StartNewGame(false);

        private void StartNewGame(bool playOpening)
        {
            academyField?.ResetExploration();
            pendingAcademyNotice = null;
            CloseDialogue();
            ClearMissionState();
            campaign.Reset();
            prologue.Reset();
            openingChoice = OpeningVoiceChoice.Full;
            openingCompleted = !playOpening;
            dialogueHud.MaskElisaName = false;
            openingBranchEnabled = openingInterruptTriggered = false;
            openingClickCount = 0;
            openingSuppressNextPointerAdvance = false;
            pointerPressedDuringReveal = false;
            SyncStoryProgression();
            session = campaign.CreateDuel(System.Environment.TickCount, MeshPercent);
            ResetBattlePresentation();
            // A new story remembers only the lessons it teaches again.
            recallAlbum.Clear();
            lobbyHud.ResetView();
            ShowBriefing();
            AutoSave();
        }

        /// <summary>The title's 온라인 친구 대전, versus's only entry: opens a two-player invite-code lobby without changing
        /// campaign progress. Its battles run on <see cref="LocalVersus"/>.</summary>
        public bool StartOnlineVersus()
        {
            if (!IsInTitle || onlineVersus == null) return false;
            titleHud.Hide();
            showingTitle = false;
            lobbyHud.Hide();
            hud.Root.SetActive(false);
            // A match the title opens starts with its start card, on both peers alike.
            localVersus.StartCardsEnabled = true;
            onlineVersus.Start();
            return true;
        }

        /// <summary>The title screen: 이어하기 when a valid save exists, 새 게임, and 온라인 친구 대전 (which never touches the
        /// save). Auto-saving stays off until 이어하기 or 새 게임 is chosen.</summary>
        public void ShowTitle()
        {
            academyField?.ResetExploration();
            pendingAcademyNotice = null;
            onlineVersus?.Stop();
            localVersus?.Stop();
            ClearBattleScreens();
            recallAlbum.Clear();
            lobbyHud.Hide();
            AutoSaveEnabled = false;
            StoryProgressionEnabled = false;
            StartCardsEnabled = false;
            if (localVersus != null) localVersus.StartCardsEnabled = false;
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

        /// <summary>Loads the save and resumes an unfinished opening, the next mission briefing, or the lobby.</summary>
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
            StartCardsEnabled = true;
            openingChoice = save.OpeningChoice;
            openingCompleted = save.OpeningCompleted;
            if (campaign.RestoreDroppedCurriculum)
            {
                // The save's curriculum belonged to an older tree. Saving now records the current tree version, so the
                // field's arrival notice says it once.
                AutoSave();
                pendingAcademyNotice = CurriculumResetNotice;
            }
            dialogueHud.MaskElisaName = openingChoice == OpeningVoiceChoice.Leave;
            SyncStoryProgression();
            session = campaign.CreateDuel(System.Environment.TickCount, MeshPercent);
            ResetBattlePresentation();
            lobbyHud.ResetView();
            if (!openingCompleted)
            {
                openingBranchEnabled = openingChoice == OpeningVoiceChoice.Full;
                openingClickCount = 0;
                if (PlayCutscene(OpeningCutscene, CompleteOpeningAndShowBriefing))
                {
                    openingReplayUsesSavedRoute = openingChoice != OpeningVoiceChoice.Full;
                    if (openingBranchEnabled) ShowOpeningInterruptionHint();
                }
                else CompleteOpeningAndShowBriefing();
            }
            else if (prologue.IsArcComplete) ShowLobby();
            else ShowBriefing();
            return true;
        }

        /// <summary>The title's 새 게임: starts over and replaces any save with the fresh state, then plays the
        /// awakening opening before the first briefing. 이어하기 resumes it if the opening was left unfinished.</summary>
        public bool NewGameFromTitle()
        {
            if (!IsInTitle) return false;
            AutoSaveEnabled = true;
            StoryProgressionEnabled = true;
            StartCardsEnabled = true;
            StartNewGame(true);
            openingBranchEnabled = true;
            openingClickCount = 0;
            if (PlayCutscene(OpeningCutscene, CompleteOpeningAndShowBriefing)) ShowOpeningInterruptionHint();
            else CompleteOpeningAndShowBriefing();
            return true;
        }

        private void ShowOpeningInterruptionHint()
        {
            if (!IsPlayingCutscene) return;
            cutsceneHud.SetSkipHint("Esc  선택으로");
            dialogueHud.SetCinematicHint("클릭 / Enter / Space로 글 완성·다음  ·  Esc로 선택");
        }

        private void CompleteOpeningAndShowBriefing()
        {
            openingCompleted = true;
            AutoSave();
            ShowBriefing();
        }

        /// <summary>Plays a cutscene from the lobby and comes back to it. Later story scenes default to the school;
        /// callers can explicitly place an outdoor scene in the forest.</summary>
        public bool StartCutscene(CutsceneScript script) => StartCutscene(script, ArenaBackdropKind.SchoolCorridor);

        public bool StartCutscene(CutsceneScript script, ArenaBackdropKind backdrop)
        {
            if (script == null) throw new System.ArgumentNullException(nameof(script));
            if (!IsInLobby) return false;
            OpenCutscene(script, ShowLobby, bareBackdrop: backdrop);
            return true;
        }

        /// <summary>Replays an already completed opening-arc scene from the lobby without advancing the story.</summary>
        public bool ReplayStoryCutscene(string resourcePath, int missionNumber = 0)
        {
            if (!IsInLobby) return false;

            CutsceneScript script;
            PrologueMission shown = null;
            if (missionNumber == 0)
            {
                if (resourcePath != OpeningCutscene) return false;
                TextAsset source = Resources.Load<TextAsset>(resourcePath);
                if (source == null) return false;
                try
                {
                    script = CutsceneScriptParser.Parse(resourcePath, source.text);
                }
                catch (CutsceneParseException exception)
                {
                    Debug.LogWarning(exception.Message);
                    return false;
                }
            }
            else
            {
                if (missionNumber < 1 || missionNumber > PrologueMissions.Count || !prologue.IsCleared(missionNumber))
                    return false;
                shown = PrologueMissions.Get(missionNumber);
                if (resourcePath != shown.IntroCutscene && resourcePath != shown.OutroCutscene &&
                    resourcePath != shown.Empowerment?.Scene) return false;
                script = LoadMissionCutscene(shown, resourcePath);
                if (script == null) return false;
            }

            lobbyHud.ShowTab(LobbyTab.Home);
            bool enemyAura = shown?.Empowerment?.KeepsAura == true && resourcePath == shown.OutroCutscene;
            OpenCutscene(script, ShowLobby, shown, enemyAura,
                bareBackdrop: resourcePath == OpeningCutscene ? ArenaBackdropKind.Forest : ArenaBackdropKind.SchoolCorridor);
            openingReplayUsesSavedRoute = resourcePath == OpeningCutscene && openingChoice != OpeningVoiceChoice.Full;
            return true;
        }

        /// <summary>Plays a cutscene from Resources, then runs <paramref name="continuation"/> once the player reaches
        /// its end or skips it. Returns false (and runs nothing) when the file is missing or invalid.</summary>
        private bool PlayCutscene(string resourcePath, System.Action continuation,
            ArenaBackdropKind backdrop = ArenaBackdropKind.Forest)
        {
            TextAsset source = string.IsNullOrWhiteSpace(resourcePath) ? null : Resources.Load<TextAsset>(resourcePath);
            if (source == null)
            {
                Debug.LogWarning($"Cutscene was not found at Resources/{resourcePath}.txt; continuing without it.");
                return false;
            }
            try
            {
                OpenCutscene(CutsceneScriptParser.Parse(resourcePath, source.text), continuation,
                    bareBackdrop: backdrop);
                return true;
            }
            catch (CutsceneParseException exception)
            {
                Debug.LogWarning(exception.Message);
                return false;
            }
        }

        /// <param name="battlefield">A mission whose scene this is: its fighters stand at their battle starting places
        /// first (<see cref="StageBattlefield"/>), and its last picture stays when it ends (a victory's result shows over
        /// the outro's end, not over a reset arena).</param>
        /// <param name="fromGrey">Real seconds over which the scene regains its colour from black and white (the 서막's final
        /// fall); 0 opens in colour.</param>
        /// <param name="bareBackdrop">Scenery for an unstaged scene, such as the opening or a lobby story scene.</param>
        private void OpenCutscene(CutsceneScript script, System.Action continuation, PrologueMission battlefield = null,
            bool enemyAura = false, float fromGrey = 0f, ArenaBackdropKind bareBackdrop = ArenaBackdropKind.SchoolCorridor)
        {
            // From a bare arena: no title, briefing, lobby, result, coach, dialogue or duel HUD.
            ClearBattleScreens();
            lobbyHud.Hide();
            if (battlefield != null) StageBattlefield(battlefield, enemyAura);
            else
            {
                arena.SetBackdrop(bareBackdrop);
                arena.Reset();
            }
            BeginCutscene(script, continuation, false, battlefield != null, fromGrey);
        }

        private void BeginCutscene(CutsceneScript script, System.Action continuation, bool resumesBattle, bool keepsStage = false,
            float fromGrey = 0f)
        {
            openingReplayUsesSavedRoute = false;
            cutsceneContinuation = continuation;
            cutsceneKeepsStage = keepsStage;
            cutsceneOpenedFrame = Time.frameCount;
            cutscene = new CutsceneDirector(script, arena, cutsceneHud, dialogueHud, defaultDialoguePortraitCatalog, resumesBattle,
                recallAlbum) { OpensFromGreySeconds = fromGrey };
            cutscene.Playback.PlayFullVoiceMemories = openingChoice == OpeningVoiceChoice.Full;
            cutscene.ChoiceSelected += OnCutsceneChoiceSelected;
            cutscene.Start();
            if (cutscene.IsComplete) FinishCutscene();
        }

        /// <summary>A mission scene's starting picture: the mission's fighters as its battle starts them (Elisa at -5 facing
        /// right, the dummy or the knight at 5 facing left), the camera at the duel framing, no fade. With
        /// <paramref name="enemyAura"/> the enemy still wears the 수훈 aura the battle ended with.</summary>
        private void StageBattlefield(PrologueMission shown, bool enemyAura)
        {
            arena.SetBackdrop(shown.Number <= PrologueMissions.Count
                ? ArenaBackdropKind.Forest : ArenaBackdropKind.SchoolCorridor);
            arena.SetEnemyAppearance(shown.EnemyAppearance);
            arena.Reset();
            if (enemyAura) arena.EnemyPowerAura.SetAura(true);
        }

        /// <summary>Plays one of a mission's scenes: its battlefield cutscene (<paramref name="cutscenePath"/>, parsed with
        /// the mission's fighters on stage) when that file exists, otherwise its dialogue (<paramref name="dialoguePath"/>),
        /// then runs <paramref name="continuation"/> once the player reaches its end or skips it. Returns false (and runs
        /// nothing) when neither plays.</summary>
        /// <param name="fromGrey">The cutscene opens black and white and regains its colour over these seconds.</param>
        private bool PlayMissionScene(PrologueMission shown, string cutscenePath, string dialoguePath,
            System.Action continuation, bool enemyAura = false, float fromGrey = 0f)
        {
            CutsceneScript script = LoadMissionCutscene(shown, cutscenePath);
            if (script == null) return PlayMissionDialogue(dialoguePath, continuation);
            OpenCutscene(script, continuation, shown, enemyAura, fromGrey);
            return true;
        }

        /// <summary>A mission's cutscene, or null when the file does not exist (the dialogue then plays) or cannot be read
        /// (a warning).</summary>
        private CutsceneScript LoadMissionCutscene(PrologueMission shown, string resourcePath)
        {
            string source = string.IsNullOrWhiteSpace(resourcePath) ? null : missionSceneText(resourcePath);
            if (source == null) return null;
            try
            {
                return CutsceneScriptParser.Parse(resourcePath, source, shown.SceneCast);
            }
            catch (CutsceneParseException exception)
            {
                Debug.LogWarning(exception.Message);
                return null;
            }
        }

        private static string ReadTextResource(string resourcePath)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            return asset != null ? asset.text : null;
        }

        /// <summary>A mission's mid-battle event (the 서막's 수훈), on the frame the battle paused on: the duel HUD, coach
        /// and step cues leave, and the scene plays on the arena as it stands (or its dialogue over it). Its end, or
        /// Escape, applies the empowerment and opens a new planning turn. Without either file the
        /// empowerment applies at once.</summary>
        private void OpenMissionEvent()
        {
            missionEventPending = false;
            PrologueMission shown = mission;
            if (shown?.Empowerment == null || missionEmpowered) return;
            battlePausedForEvent = true;
            empowermentCues.SetPaused(true);
            // The scene answers the moment: no bubble waits over it.
            barks.Hide();
            hud.Root.SetActive(false);
            coachHud.Hide();
            stepAudio.Stop();
            ClearHeldKeys();
            CutsceneScript script = LoadMissionCutscene(shown, shown.Empowerment.Scene);
            if (script != null) BeginCutscene(script, ResumeMissionBattle, true);
            else if (!PlayMissionDialogue(shown.EventDialogue, ResumeMissionBattle)) ResumeMissionBattle();
        }

        /// <summary>The event scene is over (or skipped): the hit that triggered it remains, while the rest of that
        /// committed turn is cancelled. Begin a fresh planning turn against the empowered enemy.</summary>
        private void ResumeMissionBattle()
        {
            battlePausedForEvent = false;
            empowermentCues.SetPaused(false);
            LegacyCurrentSlot interruptedSlot = session.CurrentSlot;
            if (session.InterruptResolvingTurn() != null) RecordResolvedSkillSlot(interruptedSlot);
            ClearShowDirector();
            // The event ends at the duel's staging places. Clear the previous strike's push, pose and camera
            // before exposing the battle again; its health, resistance, ACT and skill progression live in session.
            arena.Reset();
            stepHud.Reset();
            resistanceFeedback.Reset();
            breakImpactCue.Reset();
            skillActivationCue.Reset();
            ApplyEmpowerment();
            hud.Root.SetActive(true);
            ClearHeldKeys();
            StartPlanning();
            barks.Empowered();
            RefreshHud(0f);
        }

        /// <summary>The new planning turn follows the empowered enemy script. The 수훈 aura stays on the enemy until
        /// the battle ends (lit here if the scene did not light it), and the
        /// 수훈 phase's presentation begins (<see cref="DuelEmpowermentCues"/>).</summary>
        private void ApplyEmpowerment()
        {
            MissionEmpowerment empowerment = mission?.Empowerment;
            if (empowerment == null || missionEmpowered) return;
            missionEmpowered = true;
            session.ReplaceEnemyScript(empowerment.EnemyScript);
            if (empowerment.KeepsAura && !arena.EnemyPowerAura.IsAuraOn) arena.EnemyPowerAura.SetAura(true, EmpowermentAuraFade);
            empowermentCues.Begin();
        }

        /// <summary>How long the 수훈 aura takes to fade in when the scene did not light it (skipped early, or no scene).</summary>
        private const float EmpowermentAuraFade = .4f;

        private void AdvanceCutsceneFromPointer()
        {
            if (openingSuppressNextPointerAdvance)
            {
                openingSuppressNextPointerAdvance = false;
                pointerPressedDuringReveal = false;
                return;
            }
            if (dialogueHud.IsChoosing) return;
            bool revealOnly = pointerPressedDuringReveal || dialogueHud.IsRevealing;
            pointerPressedDuringReveal = false;
            if (revealOnly)
            {
                dialogueHud.CompleteReveal();
                return;
            }
            AdvanceCutscene();
        }

        private bool IsOpeningSkipWindow
        {
            get
            {
                if (!openingBranchEnabled || openingInterruptTriggered || cutscene == null ||
                    cutscene.Playback.Script.Id != OpeningCutscene) return false;
                return cutscene.Playback.Script.TryFindMark("skip-end", out int end) &&
                    cutscene.Playback.StepsRun <= end;
            }
        }

        private void RegisterOpeningClick()
        {
            // A previous press may have triggered the interruption while no dialogue button was present.
            if (openingSuppressNextPointerAdvance && Time.frameCount > openingInterruptFrame)
                openingSuppressNextPointerAdvance = false;
            if (!IsOpeningSkipWindow || openingClickFrame == Time.frameCount) return;
            openingClickFrame = Time.frameCount;
            if (++openingClickCount >= 10 && TriggerOpeningInterruption())
                openingSuppressNextPointerAdvance = true;
        }

        private bool TriggerOpeningInterruption()
        {
            if (!IsOpeningSkipWindow) return false;
            openingInterruptTriggered = true;
            openingInterruptFrame = Time.frameCount;
            cutscene.Playback.PlayFullVoiceMemories = false;
            if (cutscene.JumpTo("skip-interrupt"))
            {
                cutsceneHud.SetSkipHint("선택지까지 진행");
                dialogueHud.SetCinematicHint("클릭 / Enter / Space로 글 완성·다음");
                return true;
            }
            openingInterruptTriggered = false;
            return false;
        }

        private void RequestCutsceneSkip(bool fromPointer = false)
        {
            if (cutscene == null) return;
            if (fromPointer && openingSuppressNextPointerAdvance)
            {
                openingSuppressNextPointerAdvance = false;
                return;
            }
            if (TriggerOpeningInterruption()) return;
            if ((openingInterruptTriggered && openingBranchEnabled) || dialogueHud.IsChoosing) return;
            SkipCutscene();
        }

        private void OnCutsceneChoiceSelected(int index)
        {
            if (!openingInterruptTriggered || cutscene == null ||
                cutscene.Playback.Script.Id != OpeningCutscene) return;
            openingChoice = index == 0 ? OpeningVoiceChoice.Leave : OpeningVoiceChoice.Essential;
            dialogueHud.MaskElisaName = openingChoice == OpeningVoiceChoice.Leave;
            dialogueHud.SetCinematicHint(null);
            openingBranchEnabled = openingInterruptTriggered = false;
            AutoSave();
        }

        private void ChooseSavedOpeningReplayRoute()
        {
            if (!openingReplayUsesSavedRoute || cutscene?.Playback.CurrentChoice == null) return;
            cutscene.Choose(openingChoice == OpeningVoiceChoice.Leave ? 0 : 1);
        }

        /// <summary>Moves past the cutscene's waiting line. Returns false while a timed step plays.</summary>
        public bool AdvanceCutscene()
        {
            if (!IsPlayingCutscene) return false;
            bool atSavedInterruption = openingReplayUsesSavedRoute &&
                cutscene.Playback.Script.TryFindMark("skip-end", out int end) &&
                cutscene.Playback.CurrentLine != null && cutscene.Playback.StepsRun == end;
            if (atSavedInterruption)
            {
                if (!cutscene.JumpTo("skip-interrupt")) return false;
            }
            else if (!cutscene.Advance()) return false;
            ChooseSavedOpeningReplayRoute();
            if (cutscene.IsComplete) FinishCutscene();
            return true;
        }

        /// <summary>Ends the cutscene now and continues whatever it was leading to (Escape).</summary>
        public bool SkipCutscene()
        {
            if (!IsPlayingCutscene) return false;
            FinishCutscene();
            return true;
        }

        private void FinishCutscene()
        {
            System.Action next = cutsceneContinuation;
            lastCutsceneCompletedNaturally = cutscene.IsComplete;
            // A scene in the middle of a battle hands the arena back before the interrupted turn is closed. A mission's
            // battlefield scene keeps its last picture: its result shows over it, and the battle, briefing or lobby that
            // follows restages the arena anyway.
            EndCutscene(!cutscene.ResumesBattle && !cutsceneKeepsStage);
            next?.Invoke();
        }

        /// <summary>Ends any cutscene without continuing its flow (cleanup, restarts and tests), and gives the arena
        /// back at its duel framing.</summary>
        public bool StopCutscene()
        {
            if (cutscene == null) return false;
            EndCutscene(true);
            return true;
        }

        private void EndCutscene(bool resetArena)
        {
            CutsceneDirector stopping = cutscene;
            cutscene = null;
            cutsceneContinuation = null;
            cutsceneKeepsStage = false;
            cutsceneOpenedFrame = -1;
            openingBranchEnabled = openingInterruptTriggered = false;
            openingSuppressNextPointerAdvance = false;
            pointerPressedDuringReveal = false;
            openingReplayUsesSavedRoute = false;
            stopping.ChoiceSelected -= OnCutsceneChoiceSelected;
            stopping.Dispose();
            if (!resetArena) return;
            arena.SetEnemyAppearance(EnemyAppearance.Student);
            arena.Reset();
        }

        /// <summary>Escape on a story scene: skips the cutscene, or the rest of the dialogue, and continues whatever it
        /// was leading to. Returns false when no scene is showing.</summary>
        public bool SkipScene()
        {
            if (SkipCutscene()) return true;
            if (!IsShowingDialogue) return false;
            FinishDialogue();
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
            // 이어하기 follows the story, so the curriculum counts only once the saved missions have opened it.
            var story = new PrologueRun();
            story.TryRestore(save.PrologueCleared);
            // Read through the tree version as loading will, so an older tree's progress shows as the 0 it becomes, and
            // the balance as loading works it out (a save from before 수련 점수 is credited retroactively).
            int completed = save.Campaign.ForCurriculumTree(CampaignCurriculum.TreeVersion, out _).CurriculumCompleted.Count;
            string curriculum = CampaignRun.OpensCurriculum(story.UnlockedFeatures)
                ? $"커리큘럼 {completed} / {campaign.Curriculum.Tree.Nodes.Count}  ·  " +
                  $"수련 점수 {campaign.CurriculumPointsIn(save.Campaign, save.PrologueCleared)}  ·  "
                : string.Empty;
            return $"도미니코 학원  ·  전투 클리어 {save.Campaign.ClearedStages.Count} / {campaign.StageCount}  ·  " +
                curriculum + $"임무 완료 {save.PrologueCleared} / {StoryMissions.Count}";
        }

        /// <summary>Writes the persistent progress after a settled change. Never during a battle's own state.</summary>
        private void AutoSave()
        {
            if (combatDebugInstance || !AutoSaveEnabled || saveStore == null) return;
            if (!saveStore.TrySave(GameSave.Capture(prologue, campaign, openingChoice, openingCompleted), out string error))
                Debug.LogWarning(error);
        }

        /// <summary>Starts the briefed mission: its intro scene first (the battlefield cutscene, or the dialogue where there
        /// is none), then the duel. Skipping the intro starts it too.</summary>
        public bool StartMission()
        {
            if (!IsInBriefing || prologue.CurrentMission == null) return false;
            PrologueMission briefed = prologue.CurrentMission;
            int number = briefed.Number;
            lastCutsceneCompletedNaturally = false;
            if (!PlayMissionScene(briefed, briefed.IntroCutscene, briefed.IntroDialogue,
                    () => BeginMissionBattle(number, number == 2 && lastCutsceneCompletedNaturally)))
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
            // A cutscene's line uses the same box; only the cutscene takes it away.
            if (!IsPlayingCutscene) dialogueHud?.Hide();
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

        /// <summary>Spends 수련 점수 on a curriculum node and completes it at once (lobby only, once the curriculum is open,
        /// the node available and affordable: <see cref="CampaignRun.TryLearnCurriculumNode"/>), then autosaves. No
        /// confirmation step.</summary>
        public bool LearnCurriculumNode(string nodeId)
        {
            if (!IsInLobby || !campaign.TryLearnCurriculumNode(nodeId)) return false;
            // The learning moment: the curriculum page pulses the card and names what it gave, with the technique's lane
            // sound (the battle's queue cue, as a field pickup's). A node granting no technique sounds Q's.
            lobbyHud.PresentCurriculumLearned(nodeId);
            lobbyHud.Show(campaign);
            CurriculumNode node = campaign.Curriculum.Tree.Find(nodeId);
            int grantedId = node != null && node.SkillIds.Count > 0 ? node.SkillIds[0] : 0;
            LegacySkill granted = grantedId == 0 ? null : LegacySkillDefinitions.Find(grantedId)?.Skill;
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, granted?.LaneIndex ?? 0);
            AutoSave();
            return true;
        }

        /// <summary>Clears the curriculum and the skills it granted and gives back the points it cost (lobby only, once
        /// the curriculum is open).</summary>
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
            else if (IsInLobby && !IsInAcademyField) StartCampaignStage(lobbyHud.SelectedStageNumber);
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
            if (battleResult.IsMission && battleResult.StageNumber <= PrologueMissions.Count) ShowBriefing();
            else ShowLobby();
            return true;
        }

        public bool RetryBattleResult()
        {
            if (!IsShowingResult) return false;
            // The same battle again: it opens with the short start card (the new attempt takes the flag).
            startCardRetry = true;
            bool retried = RestartFromResult();
            startCardRetry = false;
            return retried;
        }

        private bool RestartFromResult()
        {
            if (battleResult.IsTraining) return RetryThroughLobby(StartTraining);
            int number = battleResult.StageNumber;
            // A mission retry skips its intro, as a restarted StarCraft mission does.
            if (battleResult.IsMission)
            {
                BeginMissionBattle(number);
                return true;
            }
            return RetryThroughLobby(() => StartCampaignStage(number));
        }

        // A retry only passes through the lobby (field) to start the next battle in the same frame, so the field could
        // not show the pending arrival notice. It is held back and kept for the next real arrival.
        private bool RetryThroughLobby(System.Func<bool> start)
        {
            string notice = pendingAcademyNotice;
            pendingAcademyNotice = null;
            ShowLobby();
            bool started = start();
            pendingAcademyNotice = notice;
            return started;
        }

        public bool AdvanceFromBattleResult()
        {
            if (!IsShowingResult || !battleResult.CanAdvance) return false;
            // The opening arc still advances from briefing to briefing. Later missions and freely chosen
            // encounters return to the academy, where the player picks what to do next.
            if (battleResult.IsMission && battleResult.StageNumber < PrologueMissions.Count)
            {
                ShowBriefing();
                return true;
            }
            ShowLobby();
            return true;
        }

        public bool StartCampaignStage(int stageNumber)
        {
            if (!IsInLobby || !campaign.TryStartStage(stageNumber)) return false;
            StartStageBattle();
            return true;
        }

        public bool StartTraining()
        {
            if (!IsInLobby || !campaign.TryStartTraining()) return false;
            session = campaign.CreateDuel(System.Environment.TickCount, MeshPercent);
            ResetBattlePresentation();
            loadingHud.Play("훈련장을 준비하는 중");
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

        public bool EquipCounter(int skillId)
        {
            if (!IsInLobby || !campaign.TryEquipCounter(skillId)) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        public bool ResetLoadout()
        {
            if (!IsInLobby || !campaign.TryResetLoadout()) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        /// <summary>Leaves the battle: a mission goes back to its briefing, a stage to the lobby. The pause menu's 포기 and
        /// coach's 임무 포기 come through <see cref="AbandonBattle"/>, which never leaves a decided battle.</summary>
        public void ReturnToLobby()
        {
            if (IsInSkillTavern) { ReturnToSkillTavernFormation(); return; }
            if (IsMission)
            {
                ShowBriefing();
                SaveAbandonedSkillExperience();
                loadingHud.Play("임무 기록으로 돌아가는 중");
                return;
            }
            if (campaign.Phase == CampaignPhase.Battle) campaign.TryAbandonBattle();
            ShowLobby();
            SaveAbandonedSkillExperience();
            loadingHud.Play("기사학교로 돌아가는 중");
        }

        private void SaveAbandonedSkillExperience()
        {
            if (!skillExperienceDirty) return;
            AutoSave();
            skillExperienceDirty = false;
        }

        /// <summary>Holds the current undecided battle, including its real-time presentation and planning clock.</summary>
        public bool PauseBattle()
        {
            if (IsInSkillTavern || IsPaused || session == null || session.IsFinished || viewPhase == ViewPhase.Outcome ||
                IsPlayingScene || IsShowingResult || startCard.IsShowing || finale.IsRunning) return false;
            ClearHeldKeys();
            ConsumeHeldLaneKeys();
            pauseHud.Show();
            return true;
        }

        public bool ResumeBattle()
        {
            if (!IsPaused) return false;
            pauseHud.Hide();
            ClearHeldKeys();
            ConsumeHeldLaneKeys();
            return true;
        }

        // Keys pressed before or during the menu cannot queue a skill when released after continuing.
        private void ConsumeHeldLaneKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.qKey.isPressed) laneHolds.Consume(0);
            if (keyboard.wKey.isPressed) laneHolds.Consume(1);
            if (keyboard.eKey.isPressed) laneHolds.Consume(2);
        }

        /// <summary>Starts the same mission, stage or dummy training again without resetting campaign progression.</summary>
        public bool RetryPausedBattle()
        {
            if (!IsPaused) return false;
            int missionNumber = mission?.Number ?? 0;
            int stageNumber = campaign.StageNumber;
            bool training = IsTrainingBattle;
            pauseHud.Hide();
            startCardRetry = true;
            try
            {
                ReturnToLobby();
                if (missionNumber > 0)
                {
                    BeginMissionBattle(missionNumber);
                    return true;
                }
                return training ? StartTraining() : StartCampaignStage(stageNumber);
            }
            finally { startCardRetry = false; }
        }

        public bool AbandonPausedBattle()
        {
            if (!IsPaused) return false;
            pauseHud.Hide();
            AbandonBattle();
            return true;
        }

        /// <summary>Pause menu or coach's 임무 포기: a decided battle is never abandoned. During its finishing
        /// blow the result or the outro comes at once (<see cref="SkipFinale"/>); with every finishing-blow length at 0, a
        /// battle already over whose last turn is still ending finishes now. Otherwise the battle is left
        /// (<see cref="ReturnToLobby"/>).</summary>
        private void AbandonBattle()
        {
            if (SkipFinale()) return;
            if (session.IsFinished && viewPhase != ViewPhase.Outcome && !IsShowingResult)
            {
                FinishStage();
                // Should the battle have refused to end, it is left as before.
                if (viewPhase == ViewPhase.Outcome) return;
            }
            ReturnToLobby();
        }

        /// <summary>The hit that ended the duel has been presented: the finishing blow's slow motion, bars and freeze frame
        /// begin (with every length switched off, the turn ends as it always did). The 서막's forced loss falls into black
        /// and white after the freeze.</summary>
        private void BeginFinale(DuelMatchOutcome outcome)
        {
            bool fall = outcome == DuelMatchOutcome.EnemyVictory && IsMission && mission.Completes(outcome, missionEmpowered);
            finale.Begin(presentationSettings, outcome, fall, hitStopRemaining);
            // The finishing blow has the moment to itself.
            barks.Hide();
        }

        /// <summary>Escape or the coach's 임무 포기 during the finishing blow (<see cref="AbandonBattle"/>): what it leads to
        /// (the result, or the outro) comes at once, so a decided battle is never abandoned. Returns false when no
        /// finishing blow is playing.</summary>
        public bool SkipFinale()
        {
            if (!finale.IsRunning) return false;
            CompleteFinale();
            return true;
        }

        /// <summary>The finishing blow has played out: the result or the outro comes now, over its still last picture. The
        /// turn's usual return to idle is skipped, so nothing moves between the freeze frame and what follows.</summary>
        private void CompleteFinale()
        {
            FinishStage();
            // Should the battle have refused to end, its usual turn end takes over.
            if (finale.IsComplete) finale.Clear();
        }

        /// <summary>The 서막's final fall drains the arena's colour, which the duel HUD, an overlay, never gets: the HUD fades
        /// out with it, and the coach leaves as it begins (the hand-over would hide it a moment later anyway). At 1 (the
        /// finale cleared) the HUD is back as drawn; the coach comes back with its next beat.</summary>
        private void FadeDuelOverlays(float amount)
        {
            hud.SetFade(amount);
            if (amount < 1f) coachHud.Hide();
        }

        private void FinishStage()
        {
            if (combatDebugInstance) { BeginCombatDebugLoop(); return; }
            if (IsShowingResult || !session.IsFinished) return;
            if (IsInSkillTavern) { FinishSkillTavernBattle(); return; }
            if (IsTrainingBattle)
            {
                FinishTraining();
                return;
            }
            if (IsMission)
            {
                FinishMission();
                return;
            }
            bool firstClear = !campaign.IsStageCleared(campaign.StageNumber);
            int unlockedBefore = campaign.HighestUnlockedStage;
            if (!campaign.TryCompleteBattle(session.Outcome)) return;
            AutoSave();
            skillExperienceDirty = false;
            bool victory = session.Outcome == DuelMatchOutcome.PlayerVictory;
            // 수련 점수 accrue while the curriculum is closed too (the bank), but the result says nothing about them then.
            bool curriculumOpen = campaign.IsCurriculumOpen;
            var result = new BattleResult(session.Outcome, false,
                campaign.StageNumber, campaign.CurrentStage.Name,
                campaign.LastReward, campaign.Currency, session.RoundNumber,
                session.Player.Health, RemainingEnemyHealth, firstClear && victory,
                campaign.HighestUnlockedStage > unlockedBefore &&
                campaign.StageLimit >= campaign.HighestUnlockedStage ? campaign.HighestUnlockedStage : 0,
                victory,
                curriculumOpen ? campaign.LastCurriculumPointGain : default,
                curriculumOpen ? campaign.CurriculumPoints : 0,
                curriculumOpen ? campaign.AffordableCurriculumNodeCount : 0,
                curriculumOpen ? campaign.CurriculumPointsToNextNode : 0,
                curriculumOpen && campaign.Curriculum.IsFinished, curriculumOpen,
                firstClear && victory && campaign.CurrentStage.FirstClearSkillId != 0
                    ? LegacySkillDefinitions.Skill(campaign.CurrentStage.FirstClearSkillId).Name : null,
                curriculumNewlyAffordable: campaign.CurriculumNewlyAffordable);
            // Points that newly cover a node: the field says so as the player arrives (the result's row says it too).
            if (result.CurriculumNewlyAffordable) pendingAcademyNotice = AcademyFieldView.CurriculumAffordableNotice;
            EndDuelPresentation();
            // A newly available lesson is now collected at the academy notice board.
            PrologueMission arrived = victory && prologue.IsArcComplete && IsNextMissionAvailable &&
                campaign.StageNumber == prologue.CurrentMission.RequiredClearedStage ? prologue.CurrentMission : null;
            battleResult = result;
            resultHud.Show(result, null, arrived != null ? $"새 임무 '{arrived.Title}' 도착 · 학원 게시판에서 확인하세요." : null);
        }

        private void FinishTraining()
        {
            int targetHealth = campaign.TrainingDummyHealth;
            if (!campaign.TryCompleteBattle(session.Outcome)) return;
            if (session.Outcome == DuelMatchOutcome.PlayerVictory || skillExperienceDirty) AutoSave();
            skillExperienceDirty = false;
            var result = new BattleResult(session.Outcome, false, campaign.StageNumber, "허수아비 수련", 0, campaign.Currency,
                Mathf.Max(1, session.RoundNumber), session.Player.Health, session.Enemy.Health,
                false, 0, false, curriculumOpen: false, isTraining: true);
            EndDuelPresentation();
            battleResult = result;
            resultHud.ShowTraining(result, targetHealth, campaign.TrainingDummyHealth);
        }

        /// <summary>A finished mission: record progress, play the outro on a victory, then show the result. A forced loss
        /// (the player's defeat after the 서막's 수훈) completes the mission like a win but shows no result: its outro
        /// plays, then the story goes on as the old win's 여정 계속 did (to the lobby after the 서막).</summary>
        private void FinishMission()
        {
            bool victory = session.Outcome == DuelMatchOutcome.PlayerVictory;
            bool forcedLoss = !victory && mission.Completes(session.Outcome, missionEmpowered);
            // The final fall hands over in black and white; the outro then regains its colour.
            float fromGrey = forcedLoss && finale.EndsInGrey ? presentationSettings.FinalFallColourReturnSeconds : 0f;
            guide?.Finish();
            int stageLimitBefore = campaign.StageLimit;
            bool curriculumWasOpen = campaign.IsCurriculumOpen;
            bool firstWin = prologue.TryComplete(mission.Number, session.Outcome, missionEmpowered);
            CurriculumPointGain missionPoints = default;
            if (firstWin)
            {
                SyncStoryProgression();
                // Each story mission's first completion pays its 수련 점수, open curriculum or not (the bank).
                missionPoints = campaign.AwardStoryMissionPoints();
            }
            if (firstWin || skillExperienceDirty) AutoSave();
            skillExperienceDirty = false;
            if (forcedLoss)
            {
                PrologueMission lost = mission;
                // The outro starts with the 수훈 aura still on the enemy; its @aura … off ends it.
                bool aura = missionEmpowered && lost.Empowerment.KeepsAura;
                // No result says that the 서막 is over and what it opened, as the old win's did: the lobby the story
                // reaches next says it, the first time only (replays and 이어하기 do not).
                if (firstWin && lost.Number == PrologueMissions.Count)
                    pendingAcademyNotice = BattleResultHud.PrologueCompleteNotice(campaign.IsCurriculumOpen);
                EndDuelPresentation();
                if (!PlayMissionScene(lost, lost.OutroCutscene, lost.OutroDialogue, ShowBriefing, aura, fromGrey))
                    ShowBriefing();
                return;
            }
            // After the story is synced, so mission 8's first win already reports the curriculum it opened and the points
            // banked until then.
            bool curriculumOpen = campaign.IsCurriculumOpen;
            var result = new BattleResult(session.Outcome, true, mission.Number, mission.Title, 0, campaign.Currency,
                session.RoundNumber, session.Player.Health, session.Enemy.Health, false, 0, victory,
                curriculumOpen ? missionPoints : default,
                curriculumOpen ? campaign.CurriculumPoints : 0,
                curriculumOpen ? campaign.AffordableCurriculumNodeCount : 0,
                curriculumOpen ? campaign.CurriculumPointsToNextNode : 0,
                curriculumOpen && campaign.Curriculum.IsFinished, curriculumOpen,
                curriculumNewlyAffordable: firstWin && campaign.CurriculumNewlyAffordable,
                curriculumJustOpened: firstWin && !curriculumWasOpen && curriculumOpen);
            // The field says it as the player arrives: the curriculum just opened with the points banked until now, or the
            // mission's point newly covers a node.
            if (result.CurriculumJustOpened)
                pendingAcademyNotice = AcademyFieldView.CurriculumOpenedNotice(result.CurriculumBalance);
            else if (result.CurriculumNewlyAffordable) pendingAcademyNotice = AcademyFieldView.CurriculumAffordableNotice;
            // A first win that opened something (넘기기 for mission 1, a feature for each lobby mission) announces it,
            // then where the story goes next. Inside the opening arc the result leads straight to the next briefing
            // (AdvanceFromBattleResult), so only a mission after it points at the academy's board.
            string unlockNotice = firstWin && !string.IsNullOrEmpty(mission.UnlockText)
                ? mission.UnlockText + (prologue.IsComplete ? string.Empty
                    : IsNextMissionAvailable ? "\n다음 임무가 열렸습니다." +
                        (mission.Number < PrologueMissions.Count ? string.Empty : " 학원 게시판에서 확인하세요.")
                    : "\n학원에서 필요한 전투를 마치면 다음 임무가 열립니다.")
                : null;
            if (firstWin && campaign.StageLimit > stageLimitBefore &&
                campaign.HighestUnlockedStage > stageLimitBefore && mission.Number > PrologueMissions.Count)
            {
                int firstStage = stageLimitBefore + 1;
                int lastStage = System.Math.Min(campaign.HighestUnlockedStage, campaign.StageLimit);
                string groupNotice = $"학원에 전투 {firstStage:00}~{lastStage:00}의 상대가 나타났습니다.";
                unlockNotice = string.IsNullOrEmpty(mission.UnlockText)
                    ? groupNotice : mission.UnlockText + "\n" + groupNotice;
            }
            EndDuelPresentation();
            if (victory && PlayMissionScene(mission, mission.OutroCutscene, mission.OutroDialogue,
                () => ShowResult(result, unlockNotice)))
            {
                return;
            }
            ShowResult(result, unlockNotice);
        }

        private void EndDuelPresentation()
        {
            arena.ClearLowHealthHeartbeat();
            arena.ClearSlowMotionAfterimages();
            pauseHud.Hide();
            ClearMissionEvent();
            ClearBattleCinematics();
            effectsSource.Stop();
            hitStopRemaining = 0f;
            ClearShowDirector();
            stepAudio.Stop();
            skillActivationCue.Reset();
            planningTime = 0f;
            ClearHeldKeys();
            laneHolds.DropExplanation();
            SetViewPhase(ViewPhase.Outcome);
            hud.ClearSkillFeedback();
            hud.Root.SetActive(false);
            lobbyHud.Hide();
            coachHud.Hide();
        }

        private void ShowResult(BattleResult result, string unlockNotice = null)
        {
            battleResult = result;
            // Post-prologue missions return to the field even when another mission is ready.
            resultHud.Show(result, result.IsMission && (prologue.IsArcComplete || !IsNextMissionAvailable), unlockNotice);
        }

        private void BeginMissionBattle(int number, bool carryIntroPositions = false)
        {
            // Mission 2 ends with Elisa and the knight at sword contact. Keep that spacing as the duel opens instead of
            // visibly sending Elisa back to her staging mark. A skipped or absent intro starts at the usual marks.
            float playerOpeningX = arena.PlayerRenderer.transform.localPosition.x;
            float enemyOpeningX = arena.EnemyRenderer.transform.localPosition.x;
            carryIntroPositions = carryIntroPositions && enemyOpeningX - playerOpeningX >= LegacyArenaView.ContactDistance;
            PrologueMission next = prologue.Missions[number - 1];
            CloseDialogue();
            mission = next;
            guide = next.CreateGuide();
            session = next.CreateDuel(System.Environment.TickCount,
                skill => campaign.GetOwnedSkill(skill.Id)?.Skill, MeshPercent);
            ResetBattlePresentation(carryIntroPositions ? playerOpeningX : (float?)null,
                carryIntroPositions ? enemyOpeningX : (float?)null);
            loadingHud.Play("임무 전장을 준비하는 중");
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
            ClearMissionEvent();
            missionEmpowered = false;
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
            PrologueMission next = prologue.IsArcComplete ? prologue.CurrentMission : null;
            lobbyHud.SetNextMission(next, next != null && IsNextMissionAvailable);
            if (prologue.IsArcComplete)
            {
                lobbyHud.SetFieldMenuMode(true);
                lobbyHud.Hide();
                string storyStatus = next == null ? "스토리 게시판 · 현재 임무 완료"
                    : $"스토리 게시판 · 전투 {next.RequiredClearedStage:00} 후 새 임무";
                academyField.Show(campaign, next != null && IsNextMissionAvailable,
                    campaign.IsTrainingUnlocked, storyStatus, pendingAcademyNotice);
                pendingAcademyNotice = null;
            }
            else
            {
                lobbyHud.SetFieldMenuMode(false);
                lobbyHud.Show(campaign);
            }
        }

        public bool OpenFieldMenu()
        {
            if (!IsInAcademyField || academyField.IsTransitioning || lobbyHud.IsVisible) return false;
            academyField.PauseMovement();
            lobbyHud.ShowFieldMenu(campaign);
            return true;
        }

        public bool CloseFieldMenu()
        {
            if (!IsInAcademyField || !lobbyHud.IsVisible) return false;
            lobbyHud.Hide();
            academyField.Resume();
            return true;
        }

        /// <summary>TEMPORARY field pickup test (Docs/FieldPickups.md): takes a technique lying in the academy field, the
        /// field's Space/click on a pickup. The run owns it from now on (never equipped); it is saved at once, sounds the
        /// battle's skill-queue cue, and the field's notice names it, with its lane only while that lane is open
        /// (<see cref="AcademyFieldView.PickupNotice"/>). Only while the field takes input, and once per pickup.</summary>
        public bool CollectFieldPickup(string pickupId)
        {
            if (!IsInAcademyField || lobbyHud.IsVisible || academyField.IsTransitioning) return false;
            if (!campaign.TryCollectFieldPickup(pickupId, out CampaignOwnedSkill granted)) return false;
            AutoSave();
            LegacySkill skill = granted != null ? granted.Skill
                : LegacySkillDefinitions.Find(CampaignFieldPickups.Find(pickupId).SkillId)?.Skill;
            if (skill == null) return true;
            // The battle's queue sound (add_skill_1~3), picked by the technique's lane instead of at random.
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, skill.LaneIndex);
            academyField.SetArrivalNotice(AcademyFieldView.PickupNotice(skill, granted != null,
                campaign.IsLaneOpen(skill.LaneIndex)));
            return true;
        }

        /// <summary>Leaves every duel, result, coach and dialogue screen, ready for the lobby or a briefing.</summary>
        private void ClearBattleScreens()
        {
            StopSkillTavernMode();
            academyField?.Hide();
            pauseHud.Hide();
            StopCutscene();
            showingTitle = false;
            titleHud?.Hide();
            CloseDialogue();
            ClearMissionState();
            ClearBattleCinematics();
            battleResult = null;
            resultHud.Hide();
            coachHud.Hide();
            hud.SetMissionMode(false);
            hud.SetTrainingMode(false);
            hud.SetGuide(null);
            hud.SetGuideFocus(-1, false, false, false);
            campaign.ReturnToLobby();
            effectsSource.Stop();
            stepAudio.Stop();
            stepHud.Reset();
            resistanceFeedback.Reset();
            breakImpactCue.Reset();
            skillActivationCue.Reset();
            skillLevelUpCue.Reset();
            hud.ClearSkillFeedback();
            arena.SetEnemyAppearance(EnemyAppearance.Student);
            arena.Reset();
            arena.ConfigureEnemyRoster(new[] { EnemyAppearance.Student });
            hud.SetEnemyActors(arena.EnemyActorTransforms);
            hitStopRemaining = 0f;
            ClearShowDirector();
            SetViewPhase(ViewPhase.Outcome);
            planningTime = 0f;
            ClearHeldKeys();
            laneHolds.DropExplanation();
            hud.Root.SetActive(false);
        }

        private void StartStageBattle()
        {
            session = campaign.CreateDuel(System.Environment.TickCount, MeshPercent);
            ResetBattlePresentation();
            loadingHud.Play("전투를 준비하는 중");
        }

        private void ResetBattlePresentation(float? playerOpeningX = null, float? enemyOpeningX = null)
        {
            academyField?.Hide();
            pauseHud.Hide();
            skillExperienceDirty = false;
            skillLevelUpCue.Reset();
            StopCutscene();
            CloseDialogue();
            // A new attempt starts unempowered; the arena reset below takes any 수훈 aura away.
            ClearMissionEvent();
            missionEmpowered = false;
            ClearBattleCinematics();
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
            // Tavern resumes the same duel after each shop; only its arena and HUD are reset here.
            if (!IsInSkillTavernBattle) session.Reset();
            stepHud.Reset();
            resistanceFeedback.Reset();
            breakImpactCue.Reset();
            skillActivationCue.Reset();
            hud.ClearSkillFeedback();
            lobbyHud.Hide();
            hud.Root.SetActive(true);
            hitStopRemaining = betweenSlotsDuration = slotDuration = slotAttackInterval = slotImpactTime = slotCycleDuration = 0f;
            slotAnticipationDuration = slotStepWindow = 0f;
            ClearShowDirector();
            slotPlaybackSpeed = 1f;
            planningDuration = PlanningDuration + (IsMission ? 0 : campaign.CurriculumStats.PlanningSeconds);
            committedTurnClockRan = false;
            turnPlanningDuration = LegacyPlanningClock.CampaignTurnStart(CurrentPlanningDuration, session.PlanningSecondsBonus);
            planningTime = turnPlanningDuration;
            highlightedSlot = -1;
            inspectingEnemy = 0;
            laneHolds.DropExplanation();
            arena.SetEnemyAppearance(IsTrainingBattle ? EnemyAppearance.TrainingDummy
                : IsMission ? mission.EnemyAppearance : CampaignEnemyVariant.ForStage(campaign.StageNumber));
            arena.SetBackdrop(IsMission && mission.Number <= PrologueMissions.Count
                ? ArenaBackdropKind.Forest : ArenaBackdropKind.SchoolCorridor);
            arena.Reset();
            ConfigureBattleEnemies(IsTrainingBattle ? EnemyAppearance.TrainingDummy
                : IsMission ? mission.EnemyAppearance : CampaignEnemyVariant.ForStage(campaign.StageNumber));
            if (playerOpeningX.HasValue && enemyOpeningX.HasValue)
                arena.SetOpeningPositions(playerOpeningX.Value, enemyOpeningX.Value);
            hud.Reset();
            hud.SetMissionMode(IsMission, IsMission && mission.PlanningTimer, IsMission && mission.BreathEnabled);
            hud.SetTrainingMode(IsTrainingBattle);
            hud.SetGuide(guide);
            if (IsMission) hud.SetStage(mission.Number, MissionCountFor(mission), mission.Title);
            else if (IsTrainingBattle) hud.SetStage(1, 1, TrainingOpponentName);
            else hud.SetStage(campaign.StageNumber, campaign.StageCount, campaign.CurrentStage.Name);
            // A mission names its enemy as the battle knows it (이아 in the 서막's last mission, unlike its briefing).
            hud.SetEnemyName(IsMission ? mission.BattleEnemyName : IsGroupBattle ? "훈련생 3인조" : null);
            // Each attempt hears its barks afresh: a retry may say its start line again.
            barks.Begin(BarkResource);
            SetViewPhase(ViewPhase.Planning);
            // The first planning turn reveals the enemy's queue too, once it is on screen.
            enemyQueueRevealPending = true;
            BeginStartCard();
            ClearHeldKeys();
            RefreshHud(0f);
            RefreshGuide();
        }

        /// <summary>The attempt opens with its start card when this session shows them and its length is not 0 (a retry
        /// from the result gets the short one): the duel HUD waits under it until it leaves, the coach until it has gone.
        /// Only a battle that starts (a mission, a stage, training) has one, not the duel the lobby keeps behind its
        /// screens.</summary>
        private void BeginStartCard()
        {
            bool starts = IsMission || campaign.Phase == CampaignPhase.Battle;
            float seconds = !StartCardsEnabled || !starts ? 0f
                : startCardRetry ? presentationSettings.StartCardRetrySeconds : presentationSettings.StartCardSeconds;
            startCardRetry = false;
            if (seconds <= 0f)
            {
                startCard.Clear();
                return;
            }
            startCard.Show(seconds, StartCardOpponent, StartCardOpponentSilhouette,
                openingChoice == OpeningVoiceChoice.Leave ? "???" : DuelStartCard.PlayerName);
            startCardOpenedFrame = Time.frameCount;
            hud.Root.SetActive(false);
        }

        /// <summary>Skips the battle's start card (a click on it, Enter, Space or Escape): the first planning turn begins at
        /// once. Returns false when no card is up.</summary>
        public bool SkipStartCard()
        {
            if (!startCard.IsShowing) return false;
            startCard.Clear();
            EndStartCard();
            return true;
        }

        /// <summary>The start card has gone: the duel HUD is back (if its exit had not brought it yet), the coach comes up
        /// and the first planning turn begins.</summary>
        private void EndStartCard()
        {
            if (!hud.Root.activeSelf) hud.Root.SetActive(true);
            ClearHeldKeys();
            RefreshGuide();
        }

        /// <summary>The name the start card gives the opponent: a mission's battle name (이아 in the 서막's last mission) and
        /// the dummy in training. A stage's enemy has no name yet, so its card shows the silhouette alone: a place name is
        /// not a fighter's (and stage 8's has 결투 in it). Once stage enemies are named, their name goes here.</summary>
        private string StartCardOpponent => IsTrainingBattle ? TrainingOpponentName : IsMission ? mission.BattleEnemyName
            : IsGroupBattle ? "훈련생 3인조" : string.Empty;

        /// <summary>The opponent's silhouette on the start card: a mission's briefing silhouette, else the dummy's or the
        /// knight's, as the arena draws the enemy.</summary>
        private string StartCardOpponentSilhouette => IsMission ? mission.Enemies[0].SilhouetteResource
            : IsTrainingBattle ? PrologueMissions.DummySilhouette : CampaignEnemyVariant.SilhouetteResource(campaign.StageNumber);

        /// <summary>Whether a visible battle, result, cutscene or battlefield dialogue takes place in the forest.</summary>
        private bool ForestIsTheScene => arena.BackdropKind == ArenaBackdropKind.Forest &&
            (IsPlayingCutscene || viewPhase != ViewPhase.Outcome || IsShowingResult || IsShowingDialogue && IsMission);

        /// <summary>How much of the picture shows: a cutscene's black fade hides the forest, and its sound with it.</summary>
        private float ForestVisibility => IsPlayingCutscene ? 1f - cutsceneHud.FadeAmount : 1f;

        /// <summary>How loud the story's own loop is, 0 to 1 (a cutscene's @ambience, the 수훈 hum): the forest gives way to it.</summary>
        private float StoryLoopLevel
        {
            get
            {
                float scene = IsPlayingCutscene ? cutscene.Audio.AmbienceVolume : 0f;
                float humVolume = presentationSettings.EmpowermentAuraLoopVolume;
                float hum = humVolume > 0f ? empowermentCues.LoopVolume / humVolume : 0f;
                return Mathf.Clamp01(Mathf.Max(scene, hum));
            }
        }

        /// <summary>Drops a pending or paused mission event (the battle is left, restarted or over). Whether the attempt was
        /// empowered stays until the mission is left or the next attempt starts, so the mission's end can still tell.</summary>
        private void ClearMissionEvent()
        {
            missionEventPending = false;
            battlePausedForEvent = false;
        }

        /// <summary>Ends the start card, the finishing blow, the 수훈 phase's presentation, the barks and the gear shimmer at
        /// once (the battle is over, restarted or left): no card, letterbox, grey, cut-in, warm grade, speech bubble or gear
        /// is left behind, and the hum fades out on its own.</summary>
        private void ClearBattleCinematics()
        {
            startCard.Clear();
            finale.Clear();
            empowermentCues.Clear();
            barks.End();
            gearShimmer.Clear();
            meshCues.Clear();
            enemyQueueRevealPending = false;
        }

        /// <summary>This battle's bark file: <c>Barks/mission-NN</c> for a story mission, <c>Barks/stage-NN</c> for a stage, none
        /// in training.</summary>
        private string BarkResource => combatDebugInstance || IsInSkillTavern || IsTrainingBattle ? null
            : IsMission ? BarkScript.MissionResource(mission.Number) : BarkScript.StageResource(campaign.StageNumber);

        private void RefreshGuide()
        {
            // The coach waits for the start card to go.
            if (guide == null || IsShowingResult || viewPhase == ViewPhase.Outcome || startCard.IsShowing)
            {
                coachHud.Hide();
                hud.SetGuide(null);
                hud.SetGuideFocus(-1, false, false, false);
                return;
            }
            coachHud.Show(guide, MissionCoachCard.MissionLabel(mission.Number));
            hud.SetGuide(guide);
            hud.SetGuideFocus(guide.ExpectedLane, guide.FocusesCommit, guide.FocusesEnemyQueue, guide.FocusesAct);
            RememberCoachBeat();
        }

        /// <summary>The first missions' coached beats are the voice 엘리사 later hears in her head (mission 3's
        /// <c>@recall</c>): each beat's screen is captured once a session, after its card has settled, if that beat is
        /// still the one up then (the album checks); a beat whose capture failed is not tried again. A fighter's speech
        /// bubble is no part of the voice: while one is up (a <c>start</c> line on the first beat), the capture waits
        /// for it to go.</summary>
        private void RememberCoachBeat()
        {
            if (!TutorialRecall.Remembers(mission) || !recallAlbum.CapturesScreens) return;
            string key = TutorialRecall.Key(mission.Number, guide.StepNumber);
            if (recallAlbum.HasTried(key) || recallAlbum.PendingKey == key) return;
            MissionGuide shown = guide;
            int step = shown.StepIndex;
            recallAlbum.RequestCapture(key, () => guide == shown && shown.StepIndex == step && !shown.IsComplete &&
                coachHud.IsVisible && !IsPlayingScene && !IsShowingResult && !battlePausedForEvent && !finale.IsRunning &&
                !startCard.IsShowing && !IsPaused,
                () => !barks.IsShowing(BarkSpeaker.Player) && !barks.IsShowing(BarkSpeaker.Enemy));
        }

        // A key (or the pointer on a window) still down was pressed before: under Tab, for the explanation the guide's Enter
        // closed, or for a turn now committed. Its hold is over, so letting go of it never queues (DuelLaneHolds.Clear).
        private void ClearHeldKeys() => laneHolds.Clear(hud);

        private void RefreshHud(float delta) => RefreshHudClock(delta, delta);

        /// <summary>The room the queue panels' seam keeps round the coach's card while it is up (expanded) over the dock's
        /// centre: half its width and a small margin, HUD units (both HUDs scale from 1920x1080 alike); 0 otherwise. The
        /// folded free-play card sits at the screen's lower left, beside the dock.</summary>
        private float CoachSeamReserve => coachHud != null && coachHud.TopEdge > 0f
            ? MissionCoachCard.FrameSize.x * .5f + CoachSeamMargin : 0f;

        private const float CoachSeamMargin = 8f;

        private void RefreshHudClock(float delta, float realDelta)
        {
            // Every frame, after this frame's poses: the outline follows the actor's current sprite.
            RefreshEnemyRosterState();
            hud.SetLaneCycleCost(LaneCycleCost, CanAffordLaneCycle);
            hud.SetReservationClock(planningTime, PlanningTimerRuns);
            // The coach's card, while it is up over the dock's centre, opens the queue panels' seam round it.
            hud.SetQueueSeamReserve(CoachSeamReserve);
            hud.SetCombatLetterboxAccent(arena.CombatLetterboxAccent);
            hud.Refresh(session, planningTime, IsResolving, highlightedSlot, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform, delta, realDelta, turnPlanningDuration);
            // On real time, over the head HUDs as just laid out.
            barks.Tick(realDelta);
            gearShimmer.Tick(realDelta);
            meshCues.SyncLinks(session);
            meshCues.Tick(realDelta, arena.ArenaCamera, arena.PlayerRenderer.transform);
            stepHud.BindActor(arena.ArenaCamera, arena.PlayerRenderer.transform);
            stepHud.Refresh(CanStep && !combatDebugInstance, session.CurrentSlot, StepCueProgress, IsStepTimingWindow,
                StepWindowFraction, session.UsedStepThisTurn, session.StepAttemptsThisTurn, session.StepMissedThisTurn,
                StepFeatures, session.StepSuccessStreak, session.IsCurrentSlotMeshed);
            resistanceFeedback.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            // Reproject after the arena's pose and camera update without advancing the cue a second time.
            breakImpactCue.Tick(0f, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            skillActivationCue.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            // A held skill's explanation opens above the coach's card while it is up over the dock, never under it.
            hud.SetExplanationFloor(coachHud != null && coachHud.IsVisible ? coachHud.TopEdge : 0f);
            if (IsInspecting && session.EnemyQueue.Count > 0)
            {
                int slot = Mathf.Clamp(inspectingEnemy, 0, session.EnemyQueue.Count - 1);
                hud.SetInspectedSlot(slot);
                LegacySkill reserved = slot < session.PlayerQueue.Count ? session.PlayerQueue[slot] : null;
                CampaignOwnedSkill owned = reserved != null ? campaign.GetOwnedSkill(reserved.Id) : null;
                hud.ShowQueueComparison(session.EnemyQueue[slot], reserved,
                    ReferenceEquals(owned?.Skill, reserved) ? owned : null);
            }
            else if (CanChoose && laneHolds.ExplainedSkill != null)
            {
                LegacySkill explained = laneHolds.ExplainedSkill;
                CampaignOwnedSkill owned = campaign.GetOwnedSkill(explained.Id);
                hud.ShowExplanation(explained, false, ReferenceEquals(owned?.Skill, explained) ? owned : null);
            }
            else hud.HideExplanation();
        }

        private void OnDisable()
        {
            if (!combatDebugInstance) SetCombatDebugMode(false);
            pauseHud?.Hide();
            stepAudio?.Stop();
            breakImpactCue?.Reset();
            skillActivationCue?.Reset();
            skillLevelUpCue?.Reset();
            forestAmbience?.Stop();
        }

        private void OnDestroy()
        {
            DisposeCombatDebug();
            // First, while the arena and the screens it touches still exist: it releases the senior knight's runtime art
            // and its sounds, and gives a suspended battle's arena back. Its continuation never runs.
            cutscene?.Dispose();
            cutscene = null;
            cutsceneContinuation = null;
            recallAlbum?.Dispose();
            dialogueHud?.Dispose();
            cutsceneHud?.Dispose();
            pauseHud?.Dispose();
            resultHud?.Dispose();
            coachHud?.Dispose();
            briefingHud?.Dispose();
            onlineVersus?.Dispose();
            localVersus?.Dispose();
            skillTavernHud?.Dispose();
            skillTavernBattleHud?.Dispose();
            titleHud?.Dispose();
            loadingHud?.Dispose();
            lobbyHud?.Dispose();
            academyField?.Dispose();
            stepHud?.Dispose();
            stepAudio?.Dispose();
            resistanceFeedback?.Dispose();
            breakImpactCue?.Dispose();
            skillActivationCue?.Dispose();
            skillLevelUpCue?.Dispose();
            startCard?.Dispose();
            finale?.Dispose();
            empowermentCues?.Dispose();
            barks?.Dispose();
            gearShimmer?.Dispose();
            meshCues?.Dispose();
            forestAmbience?.Dispose();
            hud?.Dispose();
            arena?.Dispose();
            art?.Dispose();
            if (ownedPresentationSettings != null) Object.Destroy(ownedPresentationSettings);
        }
    }
}
