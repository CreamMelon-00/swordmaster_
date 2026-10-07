using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Actor-attached step arcs. Input and combat timing remain authoritative elsewhere.</summary>
    public sealed class DuelStepHud : IDisposable
    {
        private sealed class Cue
        {
            public RectTransform Root;
            public DuelStepRing Ring;
            public Text Key, Status, WindowMarker;
            public bool Left;
            public float FeedbackTime;
            public bool FeedbackSuccess;
            public int FeedbackStreak;
        }
        private readonly RectTransform root;
        private readonly LegacyDuelArt art;
        private readonly DuelPresentationSettings settings;
        private readonly Cue dodge, pressure;
        private readonly Text recovery;
        private readonly DuelStepResultBurst resultBurst;
        private readonly DuelStepFailureEdge failureEdge;
        private readonly RectTransform comboRoot;
        private readonly Text comboTitle, comboCount;
        private readonly Canvas canvas;
        private Camera actorCamera;
        private Transform actor;
        private LegacyCurrentSlot shownSlot;
        private int shownAttempts = -1;
        private bool shownMissed;
        private CombatFeature shownFeatures = CombatFeature.All;
        private int successStreak;
        private int brokenStreak;
        private int resultStreak;
        private bool timingVisible;
        private bool lastStepLeft;
        private bool resultSuccess;
        private float resultTime;
        private float failureEdgeTime;
        private float comboPopTime;
        private float comboFailureTime;
        private bool disposed;
        private const float FeedbackDuration = .35f;
        private const float ResultDuration = .55f;
        private const float FailureDuration = .72f;
        private const float FailureEdgeDuration = .5f;
        private const float ComboPopDuration = .42f;
        public const float ActorTargetWorldRadius = 1.5f;
        // The inherited sword sprite pivot includes empty space beside the body.
        // Use a fixed actor-local torso point so animation frame bounds cannot move the cue.
        public static readonly Vector3 ActorBodyLocalOffset = new Vector3(-.882f, -.35f, 0f);
        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && timingVisible && root.gameObject.activeInHierarchy;
        public Transform Actor => actor;
        public DuelStepRing DodgeRing => dodge.Ring;
        public DuelStepRing PressureRing => pressure.Ring;
        public int SuccessStreak => successStreak;

        public DuelStepHud(Transform parent, LegacyDuelArt art, DuelPresentationSettings settings = null)
        {
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.settings = settings;
            root = Rect("Step Timing", parent, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            canvas = root.GetComponentInParent<Canvas>();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            var edgeRect = Rect("Step Failure Edge", root, Vector2.zero, Vector2.zero);
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = Vector2.one;
            failureEdge = edgeRect.gameObject.AddComponent<DuelStepFailureEdge>();
            failureEdge.raycastTarget = false;
            resultBurst = Rect("Step Result Burst", root, Vector2.zero, Vector2.one * 280f)
                .gameObject.AddComponent<DuelStepResultBurst>();
            resultBurst.raycastTarget = false;
            dodge = BuildCue("Dodge Cue", "A  회피", true);
            pressure = BuildCue("Pressure Cue", "D  압박", false);
            comboRoot = Rect("Step Combo", root, Vector2.zero, new Vector2(132f, 62f));
            comboTitle = Label("Step Combo Title", comboRoot, new Vector2(0f, 17f), new Vector2(132f, 20f));
            comboTitle.fontSize = 16;
            comboCount = Label("Step Combo Count", comboRoot, new Vector2(0f, -9f), new Vector2(132f, 38f));
            comboCount.fontSize = 27;
            // A small key and window reminder, not another timing panel or gauge.
            recovery = Label("ACT Recovery Notice", root, new Vector2(0f, 30f), new Vector2(650f, 22f));
            recovery.rectTransform.anchorMin = recovery.rectTransform.anchorMax = new Vector2(.5f, 0f);
            Reset();
        }

        public void BindActor(Camera camera, Transform player)
        {
            actorCamera = camera;
            actor = player;
        }

        public const string KeyHint = "A 회피 · D 압박";

        /// <summary>The key hint for the steps a duel has opened (missions open 회피 before 압박).</summary>
        public static string KeyHintFor(CombatFeature features)
            => features.AllowsStep(LegacyStepAction.Dodge) && features.AllowsStep(LegacyStepAction.Pressure) ? KeyHint
                : features.AllowsStep(LegacyStepAction.Dodge) ? "A 회피"
                : features.AllowsStep(LegacyStepAction.Pressure) ? "D 압박" : string.Empty;

        /// <param name="attemptsThisTurn">Steps attempted this turn; each one narrowed the success window.</param>
        /// <param name="missedThisTurn">A step missed this turn, so next turn's natural ACT recovery is lost.</param>
        /// <param name="features">The duel's open steps: a closed step shows no cue and no key hint.</param>
        /// <param name="streakThisTurn">The duel's current consecutive successes, when available.</param>
        public void Refresh(bool visible, LegacyCurrentSlot slot, float progress, bool timingWindow,
            float windowFraction, bool usedStep, int attemptsThisTurn = 0, bool missedThisTurn = false,
            CombatFeature features = CombatFeature.All, int streakThisTurn = -1)
        {
            if (disposed) return;
            timingVisible = visible;
            SyncRootVisibility();
            if (!visible)
            {
                HideCue(dodge);
                HideCue(pressure);
                recovery.gameObject.SetActive(false);
                UpdateResult(TryProjectActor(out Vector2 hiddenCenter, out float hiddenRadius),
                    hiddenCenter, hiddenRadius);
                return;
            }
            recovery.gameObject.SetActive(true);
            if (streakThisTurn >= 0 && successStreak != streakThisTurn)
            {
                successStreak = streakThisTurn;
                if (successStreak == 0 && comboFailureTime <= 0f) comboPopTime = 0f;
                if (successStreak > 0) SetSuccessComboText();
            }
            if (!ReferenceEquals(shownSlot, slot))
            {
                dodge.FeedbackTime = pressure.FeedbackTime = 0f;
                shownSlot = slot;
            }
            bool pending = slot != null && slot.HitsResolved == 0;
            bool anchored = TryProjectActor(out Vector2 center, out float targetRadius);
            UpdateCue(dodge, pending && features.AllowsStep(LegacyStepAction.Dodge) && slot.EnemySkill?.Kind == LegacySkillKind.Attack,
                slot?.DodgeSucceeded == true, progress, timingWindow, windowFraction, anchored, center, targetRadius);
            // A pending counter can already be backed by pressure before it strikes.
            LegacySkill own = slot?.PlayerSkill ?? slot?.PendingPlayerCounter;
            UpdateCue(pressure, pending && features.AllowsStep(LegacyStepAction.Pressure) && own != null && !own.IsWait,
                slot?.PressureSucceeded == true, progress, timingWindow, windowFraction, anchored, center, targetRadius);
            UpdateResult(anchored, center, targetRadius);
            int attempts = Math.Max(attemptsThisTurn, usedStep ? 1 : 0);
            if (attempts != shownAttempts || missedThisTurn != shownMissed || features != shownFeatures)
            {
                shownAttempts = attempts;
                shownMissed = missedThisTurn;
                shownFeatures = features;
                string hint = KeyHintFor(features);
                recovery.text = attempts == 0 ? hint : missedThisTurn
                    ? hint + "  ·  이번 턴 " + attempts + "회 · 빗나감: 다음 턴 ACT 자연 회복 없음"
                    : hint + "  ·  이번 턴 " + attempts + "회 · 성공 구간이 좁아졌습니다";
                recovery.color = missedThisTurn ? DuelVisualTheme.Danger : DuelVisualTheme.Foreground;
            }
        }

        /// <summary>Withdraws a cue's result, e.g. pressure cancelled together with the counter it backed.</summary>
        public void ClearFeedback(LegacyStepAction action)
        {
            if (disposed) return;
            Cue cue = action == LegacyStepAction.Dodge ? dodge : pressure;
            cue.FeedbackTime = 0f;
            cue.FeedbackSuccess = false;
            cue.FeedbackStreak = 0;
        }

        /// <param name="successStreak">Consecutive successes this turn; from two on the cue reads "연속 N".</param>
        /// <param name="previousStreak">The streak before this judgment, used to show a broken combo on failure.</param>
        public void ShowFeedback(LegacyStepAction action, bool success, int successStreak = 1, int previousStreak = 0)
        {
            if (disposed) return;
            Cue cue = action == LegacyStepAction.Dodge ? dodge : pressure;
            cue.FeedbackTime = FeedbackDuration;
            cue.FeedbackSuccess = success;
            cue.FeedbackStreak = success ? Math.Max(1, successStreak) : 0;
            lastStepLeft = action == LegacyStepAction.Dodge;
            resultSuccess = success;
            resultStreak = success ? Math.Max(1, successStreak) : 0;
            resultTime = success ? ResultDuration : FailureDuration;
            if (success)
            {
                this.successStreak = Math.Max(1, successStreak);
                brokenStreak = 0;
                comboPopTime = ComboPopDuration;
                comboFailureTime = 0f;
                failureEdgeTime = 0f;
                SetSuccessComboText();
            }
            else
            {
                brokenStreak = Math.Max(previousStreak, this.successStreak);
                this.successStreak = 0;
                comboPopTime = 0f;
                comboFailureTime = FailureDuration;
                failureEdgeTime = FailureEdgeDuration;
                comboTitle.text = brokenStreak > 0 ? "연속 " + brokenStreak + " 끊김" : "판정 실패";
                comboCount.text = "빗나감";
            }
            resultBurst.Configure(success, resultStreak, 0f);
            resultBurst.gameObject.SetActive(true);
            failureEdge.SetProgress(success ? 1f : 0f);
            failureEdge.gameObject.SetActive(!success);
            SyncRootVisibility();
        }

        public void Tick(float realDelta)
        {
            if (disposed) return;
            realDelta = realDelta > 0f && !float.IsNaN(realDelta) && !float.IsInfinity(realDelta) ? realDelta : 0f;
            dodge.FeedbackTime = Mathf.Max(0f, dodge.FeedbackTime - realDelta);
            pressure.FeedbackTime = Mathf.Max(0f, pressure.FeedbackTime - realDelta);
            resultTime = Mathf.Max(0f, resultTime - realDelta);
            failureEdgeTime = Mathf.Max(0f, failureEdgeTime - realDelta);
            comboPopTime = Mathf.Max(0f, comboPopTime - realDelta);
            comboFailureTime = Mathf.Max(0f, comboFailureTime - realDelta);
            SyncRootVisibility();
            if (root.gameObject.activeSelf)
                UpdateResult(TryProjectActor(out Vector2 center, out float radius), center, radius);
        }

        public void Reset()
        {
            dodge.FeedbackTime = pressure.FeedbackTime = 0f;
            actor = null;
            actorCamera = null;
            shownSlot = null;
            shownAttempts = -1;
            shownMissed = false;
            shownFeatures = CombatFeature.All;
            timingVisible = false;
            HideCue(dodge);
            HideCue(pressure);
            ClearResult();
            root.gameObject.SetActive(false);
        }

        private bool TryProjectActor(out Vector2 center, out float radius)
        {
            center = Vector2.zero;
            radius = 0f;
            if (actor == null || actorCamera == null || !actor.gameObject.activeInHierarchy) return false;
            Vector3 bodyCenter = actor.TransformPoint(ActorBodyLocalOffset);
            Vector3 screen = actorCamera.WorldToScreenPoint(bodyCenter);
            if (screen.z <= 0f) return false;
            Vector3 screenEdge = actorCamera.WorldToScreenPoint(bodyCenter +
                actorCamera.transform.right * ActorTargetWorldRadius);
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out center) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenEdge, uiCamera, out Vector2 edge)) return false;
            radius = Vector2.Distance(center, edge);
            return radius > .01f && !float.IsNaN(radius) && !float.IsInfinity(radius);
        }

        private void SetSuccessComboText()
        {
            comboTitle.text = "연속 성공";
            comboCount.text = "×" + successStreak;
        }

        private void UpdateResult(bool anchored, Vector2 center, float targetRadius)
        {
            bool showBurst = anchored && resultTime > 0f;
            resultBurst.gameObject.SetActive(showBurst);
            if (showBurst)
            {
                resultBurst.rectTransform.anchoredPosition = center;
                // Follow camera zoom, but keep the hollow shock ring clear of the surrounding HUD.
                float actorScale = Mathf.Clamp(targetRadius / DuelStepRing.ExpectedRadius, .8f, 1.35f);
                int tier = Mathf.Clamp(resultStreak, 1, 5);
                float streakScale = resultSuccess ? 1f + .035f * (tier - 1) : 1.05f;
                float scale = actorScale * streakScale;
                resultBurst.rectTransform.localScale = new Vector3(scale, scale, 1f);
                resultBurst.Configure(resultSuccess, resultStreak,
                    1f - resultTime / (resultSuccess ? ResultDuration : FailureDuration));
            }
            bool showEdge = failureEdgeTime > 0f;
            failureEdge.gameObject.SetActive(showEdge);
            if (showEdge) failureEdge.SetProgress(1f - failureEdgeTime / FailureEdgeDuration);

            bool showCombo = anchored && (successStreak > 0 && (timingVisible || resultTime > 0f) ||
                comboFailureTime > 0f);
            comboRoot.gameObject.SetActive(showCombo);
            if (!showCombo) return;
            float side = lastStepLeft ? -1f : 1f;
            float x = center.x + side * (targetRadius + 145f);
            float y = center.y + 75f;
            Rect bounds = root.rect;
            comboRoot.anchoredPosition = new Vector2(
                Mathf.Clamp(x, bounds.xMin + 70f, bounds.xMax - 70f),
                Mathf.Clamp(y, bounds.yMin + 42f, bounds.yMax - 42f));
            bool failure = comboFailureTime > 0f;
            if (failure)
            {
                float age = 1f - comboFailureTime / FailureDuration;
                comboTitle.color = new Color(1f, .58f, .48f, Mathf.Clamp01(1f - age * .8f));
                comboCount.color = new Color(1f, .23f, .17f, Mathf.Clamp01(1f - age * .65f));
                comboCount.fontSize = 24;
                comboRoot.localScale = Vector3.one * (1.13f - .13f * Mathf.Clamp01(age * 4f));
            }
            else
            {
                int tier = Mathf.Clamp(successStreak, 1, 5);
                Color tone = Color.Lerp(new Color(.85f, .96f, 1f), new Color(1f, .72f, .36f),
                    (tier - 1f) / 4f);
                comboTitle.color = new Color(tone.r, tone.g, tone.b, .9f);
                comboCount.color = tone;
                comboCount.fontSize = 26 + tier * 2;
                float pop = comboPopTime / ComboPopDuration;
                float scale = 1f + .035f * (tier - 1) + .18f * pop * pop;
                comboRoot.localScale = Vector3.one * scale;
            }
        }

        private void ClearResult()
        {
            successStreak = brokenStreak = resultStreak = 0;
            resultTime = failureEdgeTime = comboPopTime = comboFailureTime = 0f;
            resultBurst.Configure(false, 0, 1f);
            failureEdge.SetProgress(1f);
            resultBurst.gameObject.SetActive(false);
            failureEdge.gameObject.SetActive(false);
            comboRoot.gameObject.SetActive(false);
            comboRoot.localScale = Vector3.one;
            comboTitle.text = comboCount.text = string.Empty;
        }

        private void SyncRootVisibility()
        {
            root.gameObject.SetActive(timingVisible || resultTime > 0f || failureEdgeTime > 0f ||
                comboFailureTime > 0f);
        }

        private void UpdateCue(Cue cue, bool pending, bool succeeded, float progress, bool timingWindow,
            float windowFraction, bool anchored, Vector2 center, float targetRadius)
        {
            bool feedback = cue.FeedbackTime > 0f;
            bool show = anchored && (pending || feedback);
            cue.Root.gameObject.SetActive(show);
            if (!show)
            {
                HideCue(cue);
                return;
            }
            // Stable actor pivot, never frame-dependent sprite bounds or queue-card pulse.
            // Only the arc scales with projected world radius; labels keep their UI size.
            cue.Root.anchoredPosition = center;
            cue.Root.localRotation = Quaternion.identity;
            float scale = targetRadius / DuelStepRing.ExpectedRadius;
            cue.Ring.rectTransform.localScale = new Vector3(scale, scale, 1f);
            float side = cue.Left ? -1f : 1f;
            float labelX = side * (scale * (DuelStepRing.ExpectedRadius + DuelStepRing.Travel) + 55f);
            cue.Key.rectTransform.anchoredPosition = new Vector2(labelX, 0f);
            cue.WindowMarker.rectTransform.anchoredPosition = new Vector2(labelX, 26f);
            cue.Status.rectTransform.anchoredPosition = new Vector2(labelX, -26f);
            bool pulse = feedback && cue.FeedbackSuccess;
            cue.Ring.gameObject.SetActive(pending || pulse);
            float pulseProgress = pulse ? 1f - cue.FeedbackTime / FeedbackDuration : 0f;
            cue.Ring.Configure(pulse || succeeded ? 1f : progress, pending && timingWindow, pulse,
                cue.Left, windowFraction, scale, settings != null ? settings.StepRingGlowIntensity : 1.6f,
                pulseProgress);
            cue.Ring.color = new Color(1f, 1f, 1f, !pending && pulse ? cue.FeedbackTime / FeedbackDuration : 1f);
            cue.WindowMarker.gameObject.SetActive(pending && cue.Ring.HasSuccessBand);
            cue.WindowMarker.color = timingWindow ? Color.white : new Color(1f, 1f, 1f, .65f);
            cue.Status.text = !feedback ? string.Empty : !cue.FeedbackSuccess ? "빗나감"
                : cue.FeedbackStreak >= 2 ? "연속 " + cue.FeedbackStreak : "성공";
            cue.Status.color = feedback && !cue.FeedbackSuccess ? DuelVisualTheme.Danger : Color.white;
        }

        private static void HideCue(Cue cue)
        {
            cue.FeedbackTime = 0f;
            cue.FeedbackSuccess = false;
            cue.FeedbackStreak = 0;
            cue.Root.gameObject.SetActive(false);
            cue.Ring.gameObject.SetActive(false);
            cue.Ring.Configure(0f, false, false, cue.Left, 0f, 1f);
            cue.WindowMarker.gameObject.SetActive(false);
            cue.Status.text = string.Empty;
        }

        private Cue BuildCue(string name, string key, bool left)
        {
            var parent = Rect(name, root, Vector2.zero, Vector2.one * 200f);
            var ringObject = Rect("Timing Ring", parent, Vector2.zero, Vector2.one * 200f);
            var ring = ringObject.gameObject.AddComponent<DuelStepRing>();
            ring.raycastTarget = false;
            var keyLabel = Label("Step Key", parent, Vector2.zero, new Vector2(100f, 22f));
            keyLabel.text = key;
            var marker = Label("Success Window Marker", parent, Vector2.zero, new Vector2(100f, 22f));
            marker.text = "성공 구간";
            var status = Label("Timing Status", parent, Vector2.zero, new Vector2(110f, 22f));
            return new Cue { Root = parent, Ring = ring, Key = keyLabel, Status = status, WindowMarker = marker, Left = left };
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = art.UIFont;
            label.fontSize = 16;
            label.color = Color.white;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .9f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            root.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
    }
}
