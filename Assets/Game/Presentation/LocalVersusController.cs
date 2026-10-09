using System;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Owns a self-contained, public-information local duel. Campaign progress never enters this match.</summary>
    public sealed class LocalVersusController : IDisposable
    {
        public const float PlanningSeconds = 20f;
        private const float ClipSeconds = 1f / 6f;
        private const float ImpactSeconds = 1f / 12f;
        private const float HitInterval = .19f;
        private const float SlotPause = .23f;
        private const float HitStop = .075f;
        private enum ViewPhase { Planning, Approaching, Closing, PlayingSlot, BetweenSlots, Returning, Finished }

        private readonly LegacyDuelArt art;
        private readonly LegacyArenaView arena;
        private readonly Action exitToTitle;
        private readonly LocalVersusHud hud;
        private readonly DuelSkillActivationCue activationCue;
        private readonly DuelResistanceFeedback resistanceFeedback;
        private readonly DuelBreakImpactCue breakImpactCue;
        private readonly DamageCue damageCue;
        private readonly AudioSource effects;
        private readonly GameObject audioRoot;
        private readonly float[] clocks = new float[2];
        private LocalVersusMatch match;
        private ViewPhase viewPhase;
        private float phaseTime;
        private float hitStopRemaining;
        private float finalHitTime = -1f;
        private int rematchCount;
        private bool active;
        private bool online;
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
        public bool IsOnline => active && online;
        public int OnlineLocalPlayer => online ? onlineLocalPlayer : -1;
        public LocalVersusMatch Match => match;
        public LocalVersusHud Hud => hud;
        public float LeftClock => clocks[0];
        public float RightClock => clocks[1];

        public LocalVersusController(Transform parent, LegacyDuelArt art, LegacyArenaView arena, Action exitToTitle)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.exitToTitle = exitToTitle ?? throw new ArgumentNullException(nameof(exitToTitle));
            hud = new LocalVersusHud(parent, art, lane => TryQueueLane(lane),
                () => TryCycle(), () => TryBreath(), () => TryPass(), () => Rematch(), Exit);
            hud.Hide();
            activationCue = new DuelSkillActivationCue(hud.Root.transform, art.UIFont, localVersus: true);
            resistanceFeedback = new DuelResistanceFeedback(hud.Root.transform, art.UIFont);
            breakImpactCue = new DuelBreakImpactCue(hud.Root.transform);
            damageCue = new DamageCue(hud.Root.transform, art.UIFont);
            audioRoot = new GameObject("Local Versus Audio");
            audioRoot.transform.SetParent(parent, false);
            effects = audioRoot.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            effects.volume = .7f;
        }

        public void Start()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LocalVersusController));
            online = false;
            onlineHost = pendingOnlineRequest = false;
            onlineLocalPlayer = -1;
            requestOnlineAction = null;
            requestOnlineRematch = null;
            rematchCount = 0;
            BeginMatch();
        }

        /// <summary>The host creates the seed and decides which side moves first; a guest only mirrors accepted actions.</summary>
        public void StartOnline(int seed, int localPlayer, bool isHost, int openingPlayer,
            Action<OnlineVersusAction, int> requestAction, Action requestRematch)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LocalVersusController));
            if (localPlayer < 0 || localPlayer > 1 || openingPlayer < 0 || openingPlayer > 1)
                throw new ArgumentOutOfRangeException(nameof(localPlayer));
            online = true;
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
            active = false;
            effects.Stop();
            activationCue.Reset();
            resistanceFeedback.Reset();
            breakImpactCue.Reset();
            damageCue.Reset();
            hud.SetRematchPending(false);
            hud.Hide();
            match = null;
            online = false;
            onlineHost = pendingOnlineRequest = false;
            onlineLocalPlayer = -1;
            requestOnlineAction = null;
            requestOnlineRematch = null;
        }

        public bool TryQueueLane(int lane) => SubmitAction(OnlineVersusAction.QueueLane, lane);
        public bool TryCycle() => SubmitAction(OnlineVersusAction.Cycle, -1);
        public bool TryBreath() => SubmitAction(OnlineVersusAction.Breath, -1);
        public bool TryPass() => SubmitAction(OnlineVersusAction.Pass, -1);

        private bool SubmitAction(OnlineVersusAction action, int lane)
        {
            if (!CanPlan) return false;
            if (online && !onlineHost)
            {
                pendingOnlineRequest = true;
                requestOnlineAction?.Invoke(action, lane);
                RefreshHud();
                return true;
            }
            int player = match.CurrentPlanner;
            int round = match.RoundNumber;
            if (!ApplyMatchAction(player, action, lane)) return false;
            AfterReservation();
            if (online) OnlineActionApplied?.Invoke(player, action, lane, round);
            return true;
        }

        /// <summary>Only the host accepts its connected opponent's requested command.</summary>
        public bool TryApplyOnlinePeerAction(OnlineVersusAction action, int lane, int round)
        {
            if (!active || !online || !onlineHost ||
                match.Phase != LegacyDuelPhase.Planning || match.CurrentPlanner != 1 || match.RoundNumber != round)
                return false;
            if (!ApplyMatchAction(1, action, lane)) return false;
            AfterReservation();
            OnlineActionApplied?.Invoke(1, action, lane, round);
            return true;
        }

        /// <summary>The guest applies only a host-approved command, in its original turn order.</summary>
        public bool ApplyOnlineAcceptedAction(int player, OnlineVersusAction action, int lane, int round)
        {
            if (!active || !online || onlineHost || match.Phase != LegacyDuelPhase.Planning ||
                match.CurrentPlanner != player || match.RoundNumber != round)
                return false;
            if (!ApplyMatchAction(player, action, lane)) return false;
            if (player == onlineLocalPlayer) pendingOnlineRequest = false;
            AfterReservation();
            return true;
        }

        public void RejectOnlineRequest()
        {
            pendingOnlineRequest = false;
            if (active && match != null) RefreshHud();
        }

        public void SyncOnlineClocks(float left, float right)
        {
            if (!active || !online || onlineHost || match == null) return;
            clocks[0] = Mathf.Clamp(left, 0f, PlanningSeconds);
            clocks[1] = Mathf.Clamp(right, 0f, PlanningSeconds);
            RefreshHud();
        }

        private bool ApplyMatchAction(int player, OnlineVersusAction action, int lane)
        {
            switch (action)
            {
                case OnlineVersusAction.QueueLane: return match.TryQueueLane(player, lane);
                case OnlineVersusAction.Cycle: return match.TryCycleLanes(player);
                case OnlineVersusAction.Breath: return match.TryQueueBreath(player);
                case OnlineVersusAction.Pass: return match.TryPass(player);
                default: return false;
            }
        }

        public bool Rematch()
        {
            if (!active || match == null || !match.IsFinished || hud.IsExitConfirming) return false;
            if (online)
            {
                hud.SetRematchPending(true);
                requestOnlineRematch?.Invoke();
                return true;
            }
            rematchCount++;
            BeginMatch();
            return true;
        }

        public void Tick(float realDelta, Keyboard keyboard)
        {
            if (!active || disposed || match == null) return;
            realDelta = float.IsNaN(realDelta) || float.IsInfinity(realDelta) ? 0f : Mathf.Max(0f, realDelta);
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                hud.ToggleExitConfirmation();
                return;
            }
            if (hud.IsExitConfirming && !online) return;
            activationCue.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            resistanceFeedback.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            breakImpactCue.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            damageCue.Tick(realDelta, arena.ArenaCamera,
                arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
            bool timedOut = false;
            if (viewPhase == ViewPhase.Planning && match.CurrentPlanner >= 0 && realDelta > 0f)
            {
                int planner = match.CurrentPlanner;
                clocks[planner] = Mathf.Max(0f, clocks[planner] - realDelta);
                if (clocks[planner] <= 0f && (!online || onlineHost))
                {
                    timedOut = true;
                    int round = match.RoundNumber;
                    match.TryPass(planner);
                    AfterReservation();
                    if (online) OnlineActionApplied?.Invoke(planner, OnlineVersusAction.Pass, -1, round);
                }
            }
            if (viewPhase == ViewPhase.Planning && !timedOut && keyboard != null)
            {
                // Charge elapsed time to the player who held the turn at frame start.
                if (keyboard.qKey.wasPressedThisFrame) TryQueueLane(0);
                else if (keyboard.wKey.wasPressedThisFrame) TryQueueLane(1);
                else if (keyboard.eKey.wasPressedThisFrame) TryQueueLane(2);
                else if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
                    TryCycle();
                else if (keyboard.sKey.wasPressedThisFrame) TryBreath();
                else if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) TryPass();
            }
            if (viewPhase == ViewPhase.Planning)
            {
                arena.SetPlanningState(Mathf.Min(10f, clocks[Mathf.Max(0, match.CurrentPlanner)]), false);
                arena.Tick(realDelta, realDelta);
                RefreshHud();
                return;
            }

            float liveTime = realDelta - Mathf.Min(realDelta, hitStopRemaining);
            hitStopRemaining = Mathf.Max(0f, hitStopRemaining - realDelta);
            arena.Tick(liveTime, realDelta);
            phaseTime += liveTime;
            switch (viewPhase)
            {
                case ViewPhase.Approaching:
                    if (arena.ApproachComplete) PrepareSlot();
                    break;
                case ViewPhase.Closing:
                    if (!arena.IsPursuing) arena.CloseDistance(liveTime);
                    if (arena.IsInRange) BeginSlot();
                    break;
                case ViewPhase.PlayingSlot:
                    PlaySlot(liveTime);
                    break;
                case ViewPhase.BetweenSlots:
                    if (phaseTime >= SlotPause) PrepareSlot();
                    break;
                case ViewPhase.Returning:
                    if (arena.ReturnComplete)
                    {
                        if (match.IsFinished) viewPhase = ViewPhase.Finished;
                        else
                        {
                            match.BeginNextTurn();
                            ResetClocks();
                            arena.BeginTurn();
                            viewPhase = ViewPhase.Planning;
                        }
                        phaseTime = 0f;
                    }
                    break;
                case ViewPhase.Finished:
                    break;
            }
            RefreshHud();
        }

        private void BeginMatch()
        {
            match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All,
                randomSeed: online ? onlineSeed : Environment.TickCount, roundLimit: 20,
                openingPlayer: online ? onlineOpeningPlayer : rematchCount % 2);
            pendingOnlineRequest = false;
            hud.SetRematchPending(false);
            ResetClocks();
            activationCue.Reset();
            resistanceFeedback.Reset();
            breakImpactCue.Reset();
            damageCue.Reset();
            arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
            arena.SetEnemyAppearance(EnemyAppearance.CadetA);
            arena.Reset();
            arena.BeginTurn();
            arena.SetResistanceBroken(false, false);
            viewPhase = ViewPhase.Planning;
            phaseTime = hitStopRemaining = 0f;
            finalHitTime = -1f;
            active = true;
            hud.Show();
            RefreshHud();
        }

        private void ResetClocks()
        {
            clocks[0] = PlanningSeconds;
            clocks[1] = PlanningSeconds;
        }

        private bool CanPlan => active && match != null && match.Phase == LegacyDuelPhase.Planning &&
            viewPhase == ViewPhase.Planning && !hud.IsExitConfirming &&
            (!online || match.CurrentPlanner == onlineLocalPlayer && !pendingOnlineRequest);

        private void RefreshHud()
        {
            if (match != null) hud.Refresh(match, clocks[0], clocks[1], online ? onlineLocalPlayer : -1);
        }

        private void AfterReservation()
        {
            if (match.Phase == LegacyDuelPhase.Resolving)
            {
                if (match.ResolutionSlotCount > 0)
                {
                    arena.BeginApproach();
                    viewPhase = ViewPhase.Approaching;
                }
                else EndRound();
                phaseTime = 0f;
            }
            else if (match.IsFinished)
                viewPhase = ViewPhase.Finished;
            RefreshHud();
        }

        private void PrepareSlot()
        {
            if (match.IsFinished || match.IsTurnResolved)
            {
                EndRound();
                return;
            }
            if (!arena.IsInRange)
            {
                viewPhase = ViewPhase.Closing;
                phaseTime = 0f;
                return;
            }
            BeginSlot();
        }

        private void BeginSlot()
        {
            int leftResistance = match.Left.State.Resistance;
            int rightResistance = match.Right.State.Resistance;
            bool leftBroken = match.Left.State.IsResistanceBroken;
            bool rightBroken = match.Right.State.IsResistanceBroken;
            LocalVersusSlot slot = match.BeginNextSlot();
            arena.ConfigureSlotTiming(1f, HitInterval);
            arena.BeginSlot(slot.LeftSkill, slot.RightSkill);
            resistanceFeedback.Show(true, match.Left.State.Resistance - leftResistance);
            resistanceFeedback.Show(false, match.Right.State.Resistance - rightResistance);
            if (!leftBroken && match.Left.State.IsResistanceBroken) breakImpactCue.Show(true);
            if (!rightBroken && match.Right.State.IsResistanceBroken) breakImpactCue.Show(false);
            activationCue.Show(true, slot.LeftSkill, slot.LeftFeedback);
            activationCue.Show(false, slot.RightSkill, slot.RightFeedback);
            arena.SetResistanceBroken(match.Left.State.IsResistanceBroken, match.Right.State.IsResistanceBroken);
            viewPhase = ViewPhase.PlayingSlot;
            phaseTime = 0f;
            finalHitTime = -1f;
        }

        private void PlaySlot(float delta)
        {
            LocalVersusSlot slot = match.CurrentSlot;
            if (slot == null) return;
            float cycle = ClipSeconds + HitInterval;
            float impactAt = ImpactSeconds + cycle * slot.HitsResolved;
            if (!slot.IsResolved && phaseTime >= impactAt)
            {
                if (!arena.IsInRange)
                {
                    if (!arena.IsPursuing) arena.CloseDistance(delta);
                    phaseTime = impactAt;
                    arena.HoldSlotAtTime(phaseTime);
                    return;
                }
                bool leftBroken = match.Left.State.IsResistanceBroken;
                bool rightBroken = match.Right.State.IsResistanceBroken;
                LocalVersusHit hit = match.ResolveNextHit();
                arena.HoldSlotAtTime(impactAt);
                PresentHit(true, hit.LeftAttacked, hit.RightHealthDamage, hit.RightResistanceDamage,
                    slot.RightSkill, hit.RightAttacked, !rightBroken && match.Right.State.IsResistanceBroken,
                    hit.Outcome != LocalVersusOutcome.InProgress);
                PresentHit(false, hit.RightAttacked, hit.LeftHealthDamage, hit.LeftResistanceDamage,
                    slot.LeftSkill, hit.LeftAttacked, !leftBroken && match.Left.State.IsResistanceBroken,
                    hit.Outcome != LocalVersusOutcome.InProgress);
                resistanceFeedback.Show(true, -hit.LeftResistanceDamage);
                resistanceFeedback.Show(false, -hit.RightResistanceDamage);
                if (!leftBroken && match.Left.State.IsResistanceBroken) breakImpactCue.Show(true);
                if (!rightBroken && match.Right.State.IsResistanceBroken) breakImpactCue.Show(false);
                arena.SetResistanceBroken(match.Left.State.IsResistanceBroken, match.Right.State.IsResistanceBroken);
                // One visible contact per frame. A push must be observed before the next contact is judged.
                phaseTime = impactAt;
                if (hit.Outcome != LocalVersusOutcome.InProgress)
                    finalHitTime = impactAt;
            }
            float heldTime = finalHitTime >= 0f
                ? Mathf.Min(phaseTime, finalHitTime + ClipSeconds - ImpactSeconds - .001f) : phaseTime;
            arena.HoldSlotAtTime(heldTime);
            float endAt = finalHitTime >= 0f
                ? finalHitTime + .35f : ClipSeconds + cycle * (slot.HitCount - 1);
            if (slot.IsResolved && phaseTime >= endAt) FinishSlot();
        }

        private void PresentHit(bool leftAttacks, bool attacked, int healthDamage, int resistanceDamage,
            LegacySkill defendingSkill, bool simultaneous, bool brokeResistance, bool ended)
        {
            if (!attacked) return;
            bool guarded = defendingSkill?.Kind == LegacySkillKind.Defence;
            bool decisive = brokeResistance || ended || healthDamage >= 20;
            var exchange = defendingSkill?.Kind == LegacySkillKind.Attack && healthDamage <= 0
                ? simultaneous ? LegacyArenaView.HitExchange.MutualClash : LegacyArenaView.HitExchange.BladeBlock
                : LegacyArenaView.HitExchange.None;
            arena.PresentHit(leftAttacks, healthDamage, resistanceDamage, guarded, decisive,
                -1, exchange, decisive);
            damageCue.Show(!leftAttacks, healthDamage, decisive);
            hitStopRemaining = Mathf.Max(hitStopRemaining, HitStop);
            effects.pitch = brokeResistance ? .9f : UnityEngine.Random.Range(.85f, 1.15f);
            art.PlayClash(effects, false, UnityEngine.Random.value >= .5f);
        }

        private void FinishSlot()
        {
            match.CompleteCurrentSlot();
            if (match.IsFinished)
            {
                activationCue.Reset();
                resistanceFeedback.Reset();
                breakImpactCue.Reset();
                damageCue.Reset();
            }
            if (match.IsFinished || match.IsTurnResolved)
            {
                EndRound();
                return;
            }
            viewPhase = ViewPhase.BetweenSlots;
            phaseTime = 0f;
        }

        private void EndRound()
        {
            arena.EndTurn();
            viewPhase = ViewPhase.Returning;
            phaseTime = 0f;
        }

        private void Exit()
        {
            if (active) exitToTitle();
        }

        /// <summary>Large HP numbers follow the struck actor; resistance changes have their own smaller cue.</summary>
        private sealed class DamageCue : IDisposable
        {
            private const float Lifetime = .78f;
            private readonly RectTransform root;
            private readonly Canvas canvas;
            private readonly Text[] labels = new Text[2];
            private readonly float[] remaining = new float[2];
            private bool disposed;

            public DamageCue(Transform parent, Font font)
            {
                root = new GameObject("Versus Damage Numbers", typeof(RectTransform)).GetComponent<RectTransform>();
                root.SetParent(parent, false);
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.sizeDelta = root.anchoredPosition = Vector2.zero;
                canvas = root.GetComponentInParent<Canvas>();
                for (int i = 0; i < 2; i++)
                {
                    var rect = new GameObject("Versus " + (i + 1) + "P Damage", typeof(RectTransform))
                        .GetComponent<RectTransform>();
                    rect.SetParent(root, false);
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                    rect.sizeDelta = new Vector2(220f, 72f);
                    Text label = rect.gameObject.AddComponent<Text>();
                    label.font = font;
                    label.fontSize = 47;
                    label.fontStyle = FontStyle.Bold;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.raycastTarget = false;
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                    var shadow = rect.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = DuelVisualTheme.Surface;
                    shadow.effectDistance = new Vector2(2f, -2f);
                    labels[i] = label;
                    label.gameObject.SetActive(false);
                }
            }

            public void Show(bool leftTarget, int healthDamage, bool decisive)
            {
                if (disposed || healthDamage <= 0) return;
                int index = leftTarget ? 0 : 1;
                Text label = labels[index];
                label.text = "-" + healthDamage;
                label.fontSize = decisive ? 57 : 47;
                label.color = decisive ? DuelVisualTheme.Accent : new Color32(255, 113, 101, 255);
                remaining[index] = Lifetime;
                label.gameObject.SetActive(true);
            }

            public void Tick(float delta, Camera camera, Transform left, Transform right)
            {
                if (disposed) return;
                for (int i = 0; i < 2; i++)
                {
                    if (remaining[i] <= 0f) continue;
                    remaining[i] = Mathf.Max(0f, remaining[i] - Mathf.Max(0f, delta));
                    Text label = labels[i];
                    Transform actor = i == 0 ? left : right;
                    if (remaining[i] <= 0f || camera == null || actor == null)
                    {
                        label.gameObject.SetActive(false);
                        continue;
                    }
                    Vector3 screen = camera.WorldToScreenPoint(actor.TransformPoint(
                        new Vector3(0f, 1.65f + (Lifetime - remaining[i]) * .65f, 0f)));
                    Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        ? canvas.worldCamera : null;
                    if (screen.z <= 0f ||
                        !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera,
                            out Vector2 point))
                    {
                        label.gameObject.SetActive(false);
                        continue;
                    }
                    label.rectTransform.anchoredPosition = point;
                    Color color = label.color;
                    color.a = Mathf.Clamp01(remaining[i] / .25f);
                    label.color = color;
                    label.gameObject.SetActive(true);
                }
            }

            public void Reset()
            {
                for (int i = 0; i < 2; i++)
                {
                    remaining[i] = 0f;
                    labels[i].gameObject.SetActive(false);
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                Reset();
                disposed = true;
                UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            activationCue.Dispose();
            resistanceFeedback.Dispose();
            breakImpactCue.Dispose();
            damageCue.Dispose();
            hud.Dispose();
            if (audioRoot != null) UnityEngine.Object.Destroy(audioRoot);
        }
    }
}
