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
        }
        private readonly RectTransform root;
        private readonly LegacyDuelArt art;
        private readonly DuelPresentationSettings settings;
        private readonly Cue dodge, pressure;
        private readonly Text recovery;
        private readonly Canvas canvas;
        private Camera actorCamera;
        private Transform actor;
        private LegacyCurrentSlot shownSlot;
        private bool disposed;
        private const float FeedbackDuration = .35f;
        public const float ActorTargetWorldRadius = 1.5f;
        // The inherited sword sprite pivot includes empty space beside the body.
        // Use a fixed actor-local torso point so animation frame bounds cannot move the cue.
        public static readonly Vector3 ActorBodyLocalOffset = new Vector3(-.882f, -.35f, 0f);
        public GameObject Root => root.gameObject;
        public bool IsVisible => !disposed && root.gameObject.activeInHierarchy;
        public Transform Actor => actor;
        public DuelStepRing DodgeRing => dodge.Ring;
        public DuelStepRing PressureRing => pressure.Ring;

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
            dodge = BuildCue("Dodge Cue", "A  회피", true);
            pressure = BuildCue("Pressure Cue", "D  압박", false);
            // A small cost reminder, not another timing panel or gauge.
            recovery = Label("ACT Recovery Notice", root, new Vector2(0f, 30f), new Vector2(650f, 22f));
            recovery.rectTransform.anchorMin = recovery.rectTransform.anchorMax = new Vector2(.5f, 0f);
            Reset();
        }

        public void BindActor(Camera camera, Transform player)
        {
            actorCamera = camera;
            actor = player;
        }

        public void Refresh(bool visible, LegacyCurrentSlot slot, float progress, bool timingWindow,
            float windowFraction, bool usedStep)
        {
            if (disposed) return;
            root.gameObject.SetActive(visible);
            if (!visible)
            {
                HideCue(dodge);
                HideCue(pressure);
                return;
            }
            if (!ReferenceEquals(shownSlot, slot))
            {
                dodge.FeedbackTime = pressure.FeedbackTime = 0f;
                shownSlot = slot;
            }
            bool pending = slot != null && slot.HitsResolved == 0;
            bool anchored = TryProjectActor(out Vector2 center, out float targetRadius);
            UpdateCue(dodge, pending && slot.EnemySkill?.Kind == LegacySkillKind.Attack,
                slot?.DodgeSucceeded == true, progress, timingWindow, windowFraction, anchored, center, targetRadius);
            // A pending counter can already be backed by pressure before it strikes.
            LegacySkill own = slot?.PlayerSkill ?? slot?.PendingPlayerCounter;
            UpdateCue(pressure, pending && own != null && !own.IsWait,
                slot?.PressureSucceeded == true, progress, timingWindow, windowFraction, anchored, center, targetRadius);
            recovery.text = usedStep ? "스텝 사용 · 다음 턴 ACT 자연 회복 없음" : "A 회피 · D 압박  /  Shift 느리게 보기";
            recovery.color = usedStep ? DuelVisualTheme.Danger : DuelVisualTheme.Foreground;
        }

        /// <summary>Withdraws a cue's result, e.g. pressure cancelled together with the counter it backed.</summary>
        public void ClearFeedback(LegacyStepAction action)
        {
            if (disposed) return;
            Cue cue = action == LegacyStepAction.Dodge ? dodge : pressure;
            cue.FeedbackTime = 0f;
            cue.FeedbackSuccess = false;
        }

        public void ShowFeedback(LegacyStepAction action, bool success)
        {
            if (disposed) return;
            Cue cue = action == LegacyStepAction.Dodge ? dodge : pressure;
            cue.FeedbackTime = FeedbackDuration;
            cue.FeedbackSuccess = success;
        }

        public void Tick(float realDelta)
        {
            if (disposed) return;
            realDelta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            dodge.FeedbackTime = Mathf.Max(0f, dodge.FeedbackTime - realDelta);
            pressure.FeedbackTime = Mathf.Max(0f, pressure.FeedbackTime - realDelta);
        }

        public void Reset()
        {
            dodge.FeedbackTime = pressure.FeedbackTime = 0f;
            actor = null;
            actorCamera = null;
            shownSlot = null;
            HideCue(dodge);
            HideCue(pressure);
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
            cue.Status.text = feedback ? cue.FeedbackSuccess ? "성공" : "빗나감" : string.Empty;
            cue.Status.color = feedback && !cue.FeedbackSuccess ? DuelVisualTheme.Danger : Color.white;
        }

        private static void HideCue(Cue cue)
        {
            cue.FeedbackTime = 0f;
            cue.FeedbackSuccess = false;
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
