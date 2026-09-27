using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Dialogue;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Tutorial;
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
        private TutorialCoachHud tutorialHud;
        private BattleResult battleResult;
        private TutorialProgress tutorial;
        private float tutorialInspectionRemaining;
        private int dialogueOpenedFrame = -1;
        private AudioSource effectsSource;
        private AudioClip criticalSound;
        private ViewPhase viewPhase;
        private float phaseTime;
        private float slotDuration;
        private float slotPlaybackSpeed = 1f;
        private float slotAttackInterval;
        private float slotImpactTime;
        private float slotCycleDuration;
        private float slotAnticipationDuration;
        private float slotStepWindow;
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
        public TutorialCoachHud TutorialHud => tutorialHud;
        public DialogueHud DialogueHud => dialogueHud;
        public DialogueLine CurrentDialogueLine => dialogueSession?.Current;
        public BattleResult Result => battleResult;
        public TutorialProgress Tutorial => tutorial;
        public bool IsTutorial => tutorial != null;
        public bool IsShowingResult => viewPhase == ViewPhase.Outcome && battleResult != null;
        public bool IsShowingDialogue => dialogueSession != null && dialogueHud != null && dialogueHud.IsVisible;
        public bool IsInLobby => !IsShowingDialogue && viewPhase == ViewPhase.Outcome && !IsShowingResult &&
            !IsTutorial && campaign.Phase == CampaignPhase.Lobby;
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
        public bool CanStep => !IsTutorial && session != null && session.Phase == LegacyDuelPhase.Resolving &&
            (viewPhase == ViewPhase.ClosingDistance || viewPhase == ViewPhase.SkillWindup ||
             viewPhase == ViewPhase.PlayingSlot || viewPhase == ViewPhase.BetweenSlots);
        private float TimeUntilFirstImpact => viewPhase == ViewPhase.SkillWindup
            ? slotAnticipationDuration - phaseTime + slotImpactTime : slotImpactTime - phaseTime;
        public bool IsStepTimingWindow => CanStep && session.CurrentSlot != null &&
            session.CurrentSlot.HitsResolved == 0 &&
            (viewPhase == ViewPhase.SkillWindup || viewPhase == ViewPhase.PlayingSlot) &&
            TimeUntilFirstImpact >= 0f && TimeUntilFirstImpact <= slotStepWindow &&
            (arena.IsInRange || session.CurrentSlot.DodgeSucceeded || session.CurrentSlot.PressureSucceeded);
        public float StepCueProgress => session?.CurrentSlot == null ? 0f : session.CurrentSlot.HitsResolved > 0 ? 1f :
            Mathf.Clamp01(1f - TimeUntilFirstImpact / Mathf.Max(.001f, slotAnticipationDuration + slotImpactTime));
        public float StepWindowFraction => slotStepWindow / Mathf.Max(.001f, slotAnticipationDuration + slotImpactTime);
        public bool HasRequiredArt => art != null && art.HasRequiredAssets &&
            arena != null && arena.HasRequiredAssets && hud != null && hud.HasRequiredAssets &&
            lobbyHud != null && lobbyHud.HasRequiredAssets;
        private bool IsInspecting => CanChoose && (tutorialInspectionRemaining > 0f ||
            Keyboard.current != null && Keyboard.current.tabKey.isPressed);

        private void Awake()
        {
            campaign = new CampaignRun();
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
                presentationSettings, () => QueueBreath());
            stepHud = new DuelStepHud(hud.Root.transform, art, presentationSettings);
            stepAudio = new DuelStepAudio(transform);
            resistanceFeedback = new DuelResistanceFeedback(hud.Root.transform, art.UIFont);
            dialogueHud = new DialogueHud(transform, art, () => ContinueDialogue(), () => CloseDialogue());
            lobbyHud = new CampaignLobbyHud(transform, art,
                id => AcquireSkill(id), id => UpgradeSkill(id), id => EquipSkill(id),
                id => UnequipSkill(id), (id, direction) => MoveEquippedSkill(id, direction),
                stage => StartCampaignStage(stage), RestartJourney, () => StartTutorial(),
                (id, lane, slot) => PlaceLoadoutSkill(id, lane, slot),
                () => SaveLoadout(), () => ResetLoadout());
            resultHud = new BattleResultHud(transform, art, () => DismissBattleResult(),
                () => RetryBattleResult(), () => AdvanceFromBattleResult());
            tutorialHud = new TutorialCoachHud(transform, art, () => AdvanceTutorial(),
                ReturnToLobby, () => InspectTutorialEnemy());
            RestartJourney();
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
                    if (keyboard.escapeKey.wasPressedThisFrame) CloseDialogue();
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
            tutorialInspectionRemaining = Mathf.Max(0f, tutorialInspectionRemaining - realDelta);
            if (IsTutorial && tutorial.CanAdvance && keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                AdvanceTutorial();
                return;
            }
            // Judge the cue the player saw, before this frame advances or resolves its impact.
            if (CanStep && keyboard != null)
            {
                if (keyboard.aKey.wasPressedThisFrame) TryStep(LegacyStepAction.Dodge, out _);
                if (keyboard.dKey.wasPressedThisFrame) TryStep(LegacyStepAction.Pressure, out _);
            }
            float speed = IsInspecting ? 0.2f :
                IsResolving && keyboard != null && keyboard.leftShiftKey.isPressed ? 0.4f :
                IsResolving && arena.IsFatalFocus ? 0.15f : 1f;
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
                    if (CanChoose && !IsTutorial)
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

        private void BeginSlotAnimation()
        {
            int playerResistanceBefore = session.Player.Resistance;
            int enemyResistanceBefore = session.Enemy.Resistance;
            LegacyCurrentSlot slot = session.BeginNextSlot();
            highlightedSlot = slot.SlotIndex;
            slotPlaybackSpeed = Mathf.Max(0.01f, presentationSettings.AnimationPlaybackSpeed);
            slotAttackInterval = Mathf.Max(0f, presentationSettings.AttackInterval);
            float clipDuration = OriginalClipDuration / slotPlaybackSpeed;
            slotImpactTime = OriginalAttackEventTime / slotPlaybackSpeed;
            slotCycleDuration = clipDuration + slotAttackInterval;
            slotAnticipationDuration = IsTutorial ? 0f : presentationSettings.StepAnticipationDuration;
            slotStepWindow = presentationSettings.StepTimingWindow;
            slotDuration = clipDuration * slot.HitCount + slotAttackInterval * (slot.HitCount - 1) + 0.01f;
            playerSlotDamage = enemySlotDamage = 0;
            arena.ConfigureSlotTiming(slotPlaybackSpeed, slotAttackInterval);
            arena.BeginSlot(slot.PlayerSkill, slot.EnemySkill);
            hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, slotDuration + slotAnticipationDuration);
            hud.SetSkillFeedback(slot);
            // Report the actual capped change without treating recovery or
            // direct resistance loss as an animation hit or knockback.
            resistanceFeedback.Show(true, session.Player.Resistance - playerResistanceBefore);
            resistanceFeedback.Show(false, session.Enemy.Resistance - enemyResistanceBefore);
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
                LegacyHitResult hit = session.ResolveNextHit();
                if (hit.PlayerAttacked) playerSlotDamage = hit.EnemyDisplayedDamage;
                if (hit.EnemyAttacked) enemySlotDamage = hit.PlayerDisplayedDamage;
                if (hit.PlayerAttacked)
                    PresentHit(true, hit.EnemyHealthDamage, hit.EnemyResistanceDamage,
                        hit.EnemyDisplayedDamage, hit.EnemyPushPower,
                        hit.EnemySkill?.Kind == LegacySkillKind.Defence,
                        enemyResistanceBefore > 0 && session.Enemy.Resistance <= 0);
                if (hit.EnemyAttacked && !hit.PlayerDodged)
                    PresentHit(false, hit.PlayerHealthDamage, hit.PlayerResistanceDamage,
                        hit.PlayerDisplayedDamage, hit.PlayerPushPower,
                        hit.PlayerSkill?.Kind == LegacySkillKind.Defence,
                        playerResistanceBefore > 0 && session.Player.Resistance <= 0);
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

        private void PresentHit(bool playerAttacks, int healthDamage, int resistanceDamage,
            int displayedDamage, int pushPower, bool guarded, bool resistanceBroke)
        {
            bool fatal = resistanceBroke || displayedDamage >= 12;
            // Both sides of a simultaneous clash share the same real-time stop.
            hitStopRemaining = Mathf.Max(hitStopRemaining, presentationSettings.HitStopDuration);
            arena.PresentHit(playerAttacks, healthDamage, resistanceDamage, guarded, fatal, pushPower);
            if (fatal) hud.FatalAttack(playerAttacks);
            Transform target = playerAttacks ? arena.EnemyRenderer.transform : arena.PlayerRenderer.transform;
            hud.ShowHitDamage(!playerAttacks, displayedDamage, target.position, fatal);
            effectsSource.pitch = Random.Range(0.75f, 1.25f);
            art.PlayClash(effectsSource, false, Random.value >= 0.5f);
            if (fatal && criticalSound != null)
                effectsSource.PlayOneShot(criticalSound, 0.55f);
        }

        private void StartPlanning()
        {
            session.BeginNextTurn();
            ClearHeldKeys();
            planningTime = PlanningDuration;
            highlightedSlot = -1;
            inspectingEnemy = 0;
            explainedSkill = null;
            arena.BeginTurn();
            hud.BeginTurn();
            SetViewPhase(ViewPhase.Planning);
            tutorial?.NotifyTurnBegan(session.RoundNumber);
            RefreshTutorial();
        }

        public bool TryStep(LegacyStepAction action, out bool success)
        {
            success = false;
            if (!CanStep || !session.TryStep(action, IsStepTimingWindow, out success)) return false;
            arena.PerformStep(action, success);
            stepHud.ShowFeedback(action, success);
            stepAudio.Play(action, success, presentationSettings.StepSoundVolume);
            RefreshHud(0f);
            return true;
        }

        private void SetViewPhase(ViewPhase next)
        {
            viewPhase = next;
            phaseTime = 0f;
        }

        private void ReadPlanningInput(Keyboard keyboard)
        {
            if (IsTutorial && tutorial.Step == TutorialStep.InspectEnemy && keyboard.tabKey.isPressed)
                InspectTutorialEnemy();
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
                if (holdTimes[lane] >= 1f)
                {
                    explainedSkill = session.GetLane(lane)[0];
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
                IsTutorial && !tutorial.AllowsQueue(lane) || !session.TryQueueLane(lane)) return false;
            tutorial?.NotifyQueued(lane);
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, Random.Range(0, 3));
            explainedSkill = null;
            RefreshHud(0f);
            RefreshTutorial();
            return true;
        }

        public bool QueueBreath()
        {
            if (!CanChoose || IsInspecting || IsTutorial || !session.TryQueueBreath()) return false;
            effectsSource.pitch = 1f;
            art.PlaySelection(effectsSource, Random.Range(0, 3));
            explainedSkill = null;
            RefreshHud(0f);
            return true;
        }

        public void CommitTurn()
        {
            if (!CanChoose || IsTutorial && !tutorial.AllowsCommit) return;
            ClearHeldKeys();
            explainedSkill = null;
            tutorialInspectionRemaining = 0f;
            hud.HideExplanation();
            session.Commit();
            hud.EndTurn();
            arena.BeginApproach();
            highlightedSlot = -1;
            SetViewPhase(ViewPhase.Approaching);
            tutorial?.NotifyCommitted();
            RefreshHud(0f);
            RefreshTutorial();
        }

        public void RestartMatch()
        {
            CloseDialogue();
            tutorial = null;
            campaign.Reset();
            campaign.TryStartStage(1);
            StartStageBattle();
        }

        public void RestartJourney()
        {
            CloseDialogue();
            tutorial = null;
            campaign.Reset();
            session = campaign.CreateDuel(System.Environment.TickCount);
            ResetBattlePresentation();
            lobbyHud.ResetView();
            ShowLobby();
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
            dialogueSession = new DialogueSession(script);
            activeDialoguePortraitCatalog = portraitCatalog;
            dialogueOpenedFrame = Time.frameCount;
            ShowCurrentDialogueLine();
            return true;
        }

        public bool ContinueDialogue()
        {
            if (!IsShowingDialogue) return false;
            if (!dialogueSession.MoveNext())
            {
                CloseDialogue();
                return false;
            }
            ShowCurrentDialogueLine();
            return true;
        }

        public bool CloseDialogue()
        {
            bool wasOpen = IsShowingDialogue;
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

        public bool AcquireSkill(int skillId)
        {
            if (!IsInLobby || !campaign.TryAcquireSkill(skillId)) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        public bool UpgradeSkill(int skillId)
        {
            if (!IsInLobby || !campaign.TryUpgradeSkill(skillId)) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        public void ContinueCampaign()
        {
            if (IsShowingResult) DismissBattleResult();
            else if (IsInLobby) StartCampaignStage(lobbyHud.SelectedStageNumber);
        }

        public bool StartTutorial()
        {
            if (!IsInLobby) return false;
            tutorial = new TutorialProgress();
            session = TutorialStage.CreateDuel();
            ResetBattlePresentation();
            return true;
        }

        public bool AdvanceTutorial()
        {
            if (!IsTutorial || IsShowingResult || !CanChoose || !tutorial.TryAdvance()) return false;
            ClearHeldKeys();
            RefreshTutorial();
            return true;
        }

        public bool InspectTutorialEnemy()
        {
            if (!IsTutorial || !CanChoose || tutorial.Step != TutorialStep.InspectEnemy) return false;
            tutorial.NotifyInspected();
            inspectingEnemy = 0;
            tutorialInspectionRemaining = 2f;
            ClearHeldKeys();
            RefreshHud(0f);
            RefreshTutorial();
            return true;
        }

        public bool DismissBattleResult()
        {
            if (!IsShowingResult) return false;
            ShowLobby();
            return true;
        }

        public bool RetryBattleResult()
        {
            if (!IsShowingResult) return false;
            bool practice = battleResult.IsTutorial;
            int number = battleResult.StageNumber;
            ShowLobby();
            return practice ? StartTutorial() : StartCampaignStage(number);
        }

        public bool AdvanceFromBattleResult()
        {
            if (!IsShowingResult || !battleResult.CanAdvance) return false;
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
            return true;
        }

        public bool ResetLoadout()
        {
            if (!IsInLobby || !campaign.TryResetLoadout()) return false;
            lobbyHud.Show(campaign);
            return true;
        }

        public void ReturnToLobby()
        {
            if (!IsTutorial && campaign.Phase == CampaignPhase.Battle) campaign.TryAbandonBattle();
            ShowLobby();
        }

        private void FinishStage()
        {
            if (IsShowingResult || !session.IsFinished) return;
            bool firstClear = !IsTutorial && !campaign.IsStageCleared(campaign.StageNumber);
            int unlockedBefore = campaign.HighestUnlockedStage;
            if (IsTutorial) tutorial.Finish();
            else if (!campaign.TryCompleteBattle(session.Outcome)) return;
            bool victory = session.Outcome == DuelMatchOutcome.PlayerVictory;
            battleResult = new BattleResult(session.Outcome, IsTutorial,
                IsTutorial ? 0 : campaign.StageNumber, IsTutorial ? TutorialStage.Name : campaign.CurrentStage.Name,
                IsTutorial ? 0 : campaign.LastReward, campaign.Currency, session.RoundNumber,
                session.Player.Health, session.Enemy.Health, firstClear && victory,
                !IsTutorial && campaign.HighestUnlockedStage > unlockedBefore ? campaign.HighestUnlockedStage : 0,
                !IsTutorial && victory && campaign.StageNumber < campaign.StageCount &&
                campaign.StageNumber + 1 <= campaign.HighestUnlockedStage);
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
            tutorialHud.Hide();
            resultHud.Show(battleResult);
        }

        private void ShowLobby()
        {
            CloseDialogue();
            tutorial = null;
            battleResult = null;
            tutorialInspectionRemaining = 0f;
            resultHud.Hide();
            tutorialHud.Hide();
            hud.SetTutorialMode(false);
            hud.SetTutorialInput(null);
            hud.SetTutorialFocus(-1, false, false, false);
            campaign.ReturnToLobby();
            effectsSource.Stop();
            stepAudio.Stop();
            stepHud.Reset();
            resistanceFeedback.Reset();
            hud.ClearSkillFeedback();
            arena.Reset();
            hitStopRemaining = 0f;
            SetViewPhase(ViewPhase.Outcome);
            planningTime = 0f;
            ClearHeldKeys();
            explainedSkill = null;
            hud.Root.SetActive(false);
            lobbyHud.Show(campaign);
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
            tutorialInspectionRemaining = 0f;
            resultHud.Hide();
            tutorialHud.Hide();
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
            arena.Reset();
            hud.Reset();
            hud.SetTutorialMode(IsTutorial);
            hud.SetTutorialInput(tutorial);
            hud.SetStage(IsTutorial ? 0 : campaign.StageNumber, campaign.StageCount,
                IsTutorial ? TutorialStage.Name : campaign.CurrentStage.Name);
            SetViewPhase(ViewPhase.Planning);
            ClearHeldKeys();
            RefreshHud(0f);
            RefreshTutorial();
        }

        private void RefreshTutorial()
        {
            if (!IsTutorial || IsShowingResult)
            {
                tutorialHud.Hide();
                hud.SetTutorialInput(null);
                hud.SetTutorialFocus(-1, false, false, false);
                return;
            }
            tutorialHud.Show(tutorial);
            hud.SetTutorialInput(tutorial);
            hud.SetTutorialFocus(tutorial.ExpectedLane, tutorial.Step == TutorialStep.CommitQueue,
                tutorial.Step == TutorialStep.InspectEnemy, tutorial.Step == TutorialStep.TurnRecovery);
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
            hud.Refresh(session, planningTime, IsResolving, highlightedSlot, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform, delta, realDelta);
            stepHud.BindActor(arena.ArenaCamera, arena.PlayerRenderer.transform);
            stepHud.Refresh(CanStep, session.CurrentSlot, StepCueProgress, IsStepTimingWindow,
                StepWindowFraction, session.UsedStepThisTurn);
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
            tutorialHud?.Dispose();
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
