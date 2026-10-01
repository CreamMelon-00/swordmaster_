using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>A brief, side-specific announcement for a condition that actually matched during a slot.</summary>
    public sealed class DuelSkillActivationCue : IDisposable
    {
        private const float Duration = 1.05f;
        private const float FadeDuration = .25f;
        private const float BurstDuration = .65f;
        private static readonly Color ActInk = new Color32(132, 239, 215, 255);
        private static readonly Color RecoveryInk = new Color32(168, 224, 159, 255);
        private static readonly Color PowerInk = new Color32(231, 207, 246, 255);
        private static readonly Color DangerFlash = new Color32(255, 96, 82, 255);
        private readonly RectTransform root;
        private readonly Canvas canvas;
        private readonly CueView player, enemy;
        private bool disposed;

        public DuelSkillActivationCue(Transform parent, Font font)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (font == null) throw new ArgumentNullException(nameof(font));
            root = new GameObject("Skill Activation Cues", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = root.anchoredPosition = Vector2.zero;
            canvas = root.GetComponentInParent<Canvas>();
            player = CreateView("Player Skill Activation Cue", true, font);
            enemy = CreateView("Enemy Skill Activation Cue", false, font);
        }

        /// <summary>Uses the immutable applied-effect snapshot; enemy guards can warn of a matched response without claiming ACT.</summary>
        public bool Show(bool playerSide, LegacySkill skill, LegacySkillFeedback feedback)
        {
            if (disposed || skill == null || feedback == null || !feedback.ConditionMet)
                return false;

            // The matching guard is still a readable threat when the enemy has no ACT pool.
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            bool enemyGuardResponse = !playerSide && !feedback.EffectActivated &&
                skill.Kind == LegacySkillKind.Defence && effect != null &&
                effect.HasOpponentCondition && effect.ActGain > 0;
            if (!feedback.EffectActivated && !enemyGuardResponse) return false;

            string detail = enemyGuardResponse ? "내 공격을 읽음" : string.Empty;
            Color tint = playerSide ? ActInk : DuelVisualTheme.Danger;
            if (feedback.OpponentResistanceReduced > 0)
            {
                detail = (playerSide ? "상대 저항 -" : "내 저항 -") + feedback.OpponentResistanceReduced;
                if (playerSide) tint = DuelVisualTheme.Accent;
            }
            if (feedback.ResistanceRestored > 0)
            {
                detail = Join(detail, "저항 회복 +" + feedback.ResistanceRestored);
                if (playerSide) tint = RecoveryInk;
            }
            if (feedback.ActGainGranted > 0)
                detail = Join(detail, "다음 턴 ACT 회복 +" + feedback.ActGainGranted);
            if (feedback.GrantedPowerBuffPercent > 0)
            {
                detail = Join(detail, "뒤 " + feedback.GrantedBuffSlots + "칸 위력 +" + feedback.GrantedPowerBuffPercent + "%");
                if (playerSide) tint = PowerInk;
            }
            if (feedback.GrantedProtectionBuffPercent > 0)
                detail = Join(detail, "뒤 " + feedback.GrantedBuffSlots + "칸 피해 -" + feedback.GrantedProtectionBuffPercent + "%");
            else if (feedback.GrantedProtectionBuffPercent < 0)
                detail = Join(detail, "다음 칸 받는 피해 +" + -feedback.GrantedProtectionBuffPercent + "%");
            if (string.IsNullOrEmpty(detail)) return false;

            CueView view = playerSide ? player : enemy;
            view.Title.text = (playerSide ? string.Empty : "상대 ") + skill.Name +
                (enemyGuardResponse ? " 대응!" : " 성공!");
            view.Detail.text = detail;
            view.Title.color = tint;
            view.Stripe.color = tint;
            view.Outline.effectColor = tint;
            view.Tint = tint;
            view.BurstStyle = !playerSide ? DuelSkillBurstStyle.Danger
                : feedback.OpponentResistanceReduced > 0 ? DuelSkillBurstStyle.Strike
                : feedback.ResistanceRestored > 0 ? DuelSkillBurstStyle.Recovery
                : DuelSkillBurstStyle.Shield;
            view.Elapsed = 0f;
            view.Root.anchoredPosition = new Vector2(playerSide ? 24f : -24f, -156f);
            view.Root.localScale = Vector3.one * 1.04f;
            view.Group.alpha = 1f;
            view.Root.gameObject.SetActive(true);
            view.Root.SetAsLastSibling();
            view.Burst.Configure(playerSide ? tint : DangerFlash, view.BurstStyle, 0f);
            view.Burst.gameObject.SetActive(false);
            return true;
        }

        public void Tick(float realDelta, Camera arenaCamera = null, Transform playerActor = null,
            Transform enemyActor = null)
        {
            if (disposed || realDelta <= 0f || float.IsNaN(realDelta) || float.IsInfinity(realDelta)) return;
            UpdateView(player, realDelta, arenaCamera, playerActor);
            UpdateView(enemy, realDelta, arenaCamera, enemyActor);
        }

        public void Reset()
        {
            if (disposed) return;
            Hide(player);
            Hide(enemy);
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
            Object.Destroy(root.gameObject);
        }

        private static string Join(string left, string right) => string.IsNullOrEmpty(left) ? right : left + " · " + right;

        private CueView CreateView(string name, bool playerSide, Font font)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(CanvasGroup)).GetComponent<RectTransform>();
            panel.SetParent(root, false);
            panel.anchorMin = panel.anchorMax = new Vector2(playerSide ? 0f : 1f, 1f);
            panel.pivot = new Vector2(playerSide ? 0f : 1f, 1f);
            panel.anchoredPosition = new Vector2(playerSide ? 24f : -24f, -148f);
            panel.sizeDelta = new Vector2(312f, 68f);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(.11f, .095f, .08f, .86f);
            background.raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var glow = Image("Flash", panel, Vector2.zero, new Vector2(-6f, -6f), Color.clear);
            glow.rectTransform.anchorMin = Vector2.zero;
            glow.rectTransform.anchorMax = Vector2.one;
            var stripe = Image("Effect Stripe", panel,
                new Vector2(playerSide ? -5f : 5f, 0f), new Vector2(5f, -8f), Color.white);
            stripe.rectTransform.anchorMin = new Vector2(playerSide ? 1f : 0f, 0f);
            stripe.rectTransform.anchorMax = new Vector2(playerSide ? 1f : 0f, 1f);
            stripe.rectTransform.pivot = new Vector2(playerSide ? 1f : 0f, .5f);
            var title = Text("Skill", panel, font, new Vector2(0f, 14f), new Vector2(286f, 24f), 20);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 17;
            title.resizeTextMaxSize = 20;
            var detail = Text("Applied Effect", panel, font, new Vector2(0f, -12f), new Vector2(286f, 36f), 16);
            detail.color = DuelVisualTheme.Foreground;
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            detail.resizeTextForBestFit = true;
            detail.resizeTextMinSize = 14;
            detail.resizeTextMaxSize = 16;
            DuelSkillActivationBurst burst = CreateBurst(playerSide);
            panel.gameObject.SetActive(false);
            return new CueView(panel, group, stripe, glow, outline, title, detail, burst, playerSide);
        }

        private DuelSkillActivationBurst CreateBurst(bool playerSide)
        {
            string name = playerSide ? "Player Skill Activation Burst" : "Enemy Skill Activation Burst";
            var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(DuelSkillActivationBurst)).GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.sizeDelta = Vector2.one * 220f;
            DuelSkillActivationBurst burst = rect.GetComponent<DuelSkillActivationBurst>();
            burst.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return burst;
        }

        private static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Text(string name, Transform parent, Font font, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text label = rect.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }

        private void UpdateView(CueView view, float delta, Camera arenaCamera, Transform actor)
        {
            if (!view.Root.gameObject.activeSelf) return;
            view.Elapsed += delta;
            if (view.Elapsed >= Duration)
            {
                Hide(view);
                return;
            }
            float settle = Mathf.Clamp01(view.Elapsed / .17f);
            view.Root.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, 1f - (1f - settle) * (1f - settle));
            view.Root.anchoredPosition = new Vector2(view.PlayerSide ? 24f : -24f,
                Mathf.Lerp(-156f, -148f, settle));
            view.Group.alpha = Mathf.Clamp01((Duration - view.Elapsed) / FadeDuration);
            Color glow = view.Tint;
            glow.a = .2f * Mathf.Clamp01(1f - view.Elapsed / .35f);
            view.Glow.color = glow;
            if (view.Elapsed < BurstDuration && PositionBurst(view, arenaCamera, actor))
            {
                view.Burst.Configure(view.PlayerSide ? view.Tint : DangerFlash,
                    view.BurstStyle, view.Elapsed / BurstDuration);
                view.Burst.gameObject.SetActive(true);
            }
            else view.Burst.gameObject.SetActive(false);
        }

        private bool PositionBurst(CueView view, Camera arenaCamera, Transform actor)
        {
            if (arenaCamera == null || actor == null || !actor.gameObject.activeInHierarchy ||
                root.rect.width <= 1f || root.rect.height <= 1f) return false;
            Vector3 body = actor.TransformPoint(view.PlayerSide
                ? DuelStepHud.ActorBodyLocalOffset : new Vector3(-.4f, -.35f, 0f));
            Vector3 screen = arenaCamera.WorldToScreenPoint(body);
            if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width ||
                screen.y < 0f || screen.y > Screen.height) return false;
            Vector3 edgeScreen = arenaCamera.WorldToScreenPoint(body +
                arenaCamera.transform.right * DuelStepHud.ActorTargetWorldRadius);
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 center) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, edgeScreen, uiCamera, out Vector2 edge))
                return false;
            float offset = Mathf.Clamp(Vector2.Distance(center, edge) * .95f, 82f, 165f);
            center.x += view.PlayerSide ? -offset : offset;
            center.x = Mathf.Clamp(center.x, root.rect.xMin + 110f, root.rect.xMax - 110f);
            center.y = Mathf.Clamp(center.y, root.rect.yMin + 110f, root.rect.yMax - 110f);
            view.Burst.rectTransform.anchoredPosition = center;
            return true;
        }

        private static void Hide(CueView view)
        {
            view.Elapsed = 0f;
            view.Title.text = view.Detail.text = string.Empty;
            view.Glow.color = Color.clear;
            view.Root.gameObject.SetActive(false);
            view.Burst.gameObject.SetActive(false);
        }

        private sealed class CueView
        {
            public readonly RectTransform Root;
            public readonly CanvasGroup Group;
            public readonly Image Stripe, Glow;
            public readonly Outline Outline;
            public readonly Text Title, Detail;
            public readonly DuelSkillActivationBurst Burst;
            public readonly bool PlayerSide;
            public Color Tint;
            public DuelSkillBurstStyle BurstStyle;
            public float Elapsed;

            public CueView(RectTransform root, CanvasGroup group, Image stripe, Image glow, Outline outline,
                Text title, Text detail, DuelSkillActivationBurst burst, bool playerSide)
            {
                Root = root;
                Group = group;
                Stripe = stripe;
                Glow = glow;
                Outline = outline;
                Title = title;
                Detail = detail;
                Burst = burst;
                PlayerSide = playerSide;
            }
        }
    }
}
