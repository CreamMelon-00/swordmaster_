using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>A brief player-side announcement that outlives the duel HUD's final-frame handoff.</summary>
    public sealed class DuelSkillLevelUpCue : IDisposable
    {
        private const float Duration = 2.8f;
        private const float FadeDuration = .28f;
        private const float BurstDuration = .75f;
        private static readonly Color Gold = new Color32(248, 207, 121, 255);
        private readonly RectTransform root, panel;
        private readonly CanvasGroup group;
        private readonly Image glow;
        private readonly Text title, detail;
        private readonly DuelSkillActivationBurst burst;
        private float elapsed;
        private bool disposed;

        public bool IsVisible => !disposed && panel.gameObject.activeSelf;

        /// <param name="parent">An always-active battle owner, outside the duel HUD that hides for results.</param>
        public DuelSkillLevelUpCue(Transform parent, Font font)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (font == null) throw new ArgumentNullException(nameof(font));

            root = new GameObject("Skill Level Up Cue", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = root.anchoredPosition = Vector2.zero;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 450;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            panel = new GameObject("Skill Level Up Announcement", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
            panel.SetParent(root, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(24f, -225f);
            panel.sizeDelta = new Vector2(312f, 68f);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(.11f, .09f, .06f, .92f);
            background.raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, .65f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
            group = panel.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            glow = Image("Level Up Flash", panel, Color.clear);
            Stretch(glow.rectTransform, -5f);
            Image stripe = Image("Level Up Stripe", panel, Gold);
            stripe.rectTransform.anchorMin = stripe.rectTransform.anchorMax = new Vector2(0f, .5f);
            stripe.rectTransform.pivot = new Vector2(0f, .5f);
            stripe.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            stripe.rectTransform.sizeDelta = new Vector2(5f, 60f);
            title = Label("Skill Level Up Title", panel, font, new Vector2(0f, 13f),
                new Vector2(288f, 26f), 20, Gold);
            detail = Label("Skill Level Up Detail", panel, font, new Vector2(0f, -15f),
                new Vector2(288f, 24f), 16, DuelVisualTheme.Foreground);

            var burstRect = new GameObject("Skill Level Up Burst", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(DuelSkillActivationBurst)).GetComponent<RectTransform>();
            burstRect.SetParent(root, false);
            burstRect.anchorMin = burstRect.anchorMax = Vector2.one * .5f;
            burstRect.sizeDelta = Vector2.one * 260f;
            burst = burstRect.GetComponent<DuelSkillActivationBurst>();
            burst.raycastTarget = false;
            // The ring can brush the badge at its left edge; draw it behind the text.
            burstRect.SetAsFirstSibling();
            Reset();
        }

        public void Show(LegacySkill skill, int level)
        {
            if (disposed || skill == null || level < 1) return;
            title.text = skill.Name + " 레벨 업!";
            detail.text = "Lv. " + Mathf.Clamp(level, 1, 3) + "/3 · 기본 위력 +2";
            elapsed = 0f;
            panel.anchoredPosition = new Vector2(24f, -237f);
            panel.localScale = Vector3.one * 1.08f;
            group.alpha = 1f;
            glow.color = new Color(Gold.r, Gold.g, Gold.b, .3f);
            panel.gameObject.SetActive(true);
            burst.Configure(Gold, DuelSkillBurstStyle.LevelUp, 0f);
            burst.gameObject.SetActive(false);
        }

        public void Tick(float realDelta, Camera arenaCamera, Transform playerActor)
        {
            if (!IsVisible || realDelta <= 0f || float.IsNaN(realDelta) || float.IsInfinity(realDelta)) return;
            elapsed += realDelta;
            if (elapsed >= Duration)
            {
                Reset();
                return;
            }
            float settle = Mathf.Clamp01(elapsed / .18f);
            panel.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, settle);
            panel.anchoredPosition = new Vector2(24f, Mathf.Lerp(-237f, -225f, settle));
            group.alpha = Mathf.Clamp01((Duration - elapsed) / FadeDuration);
            glow.color = new Color(Gold.r, Gold.g, Gold.b, .3f * Mathf.Clamp01(1f - elapsed / .38f));
            if (elapsed < BurstDuration)
            {
                if (!PositionBurst(arenaCamera, playerActor)) PositionFallbackBurst();
                burst.Configure(Gold, DuelSkillBurstStyle.LevelUp, elapsed / BurstDuration);
                burst.gameObject.SetActive(true);
            }
            else burst.gameObject.SetActive(false);
        }

        public void Reset()
        {
            if (disposed) return;
            elapsed = 0f;
            title.text = detail.text = string.Empty;
            glow.color = Color.clear;
            panel.gameObject.SetActive(false);
            burst.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
            Object.Destroy(root.gameObject);
        }

        private bool PositionBurst(Camera arenaCamera, Transform actor)
        {
            if (arenaCamera == null || actor == null || !actor.gameObject.activeInHierarchy ||
                root.rect.width <= 1f || root.rect.height <= 1f) return false;
            Vector3 body = actor.TransformPoint(DuelStepHud.ActorBodyLocalOffset);
            Vector3 screen = arenaCamera.WorldToScreenPoint(body);
            if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width ||
                screen.y < 0f || screen.y > Screen.height) return false;
            Vector3 edgeScreen = arenaCamera.WorldToScreenPoint(body +
                arenaCamera.transform.right * DuelStepHud.ActorTargetWorldRadius);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 center) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, edgeScreen, null, out Vector2 edge))
                return false;
            float offset = Mathf.Clamp(Vector2.Distance(center, edge) * 1.15f, 110f, 170f);
            float bodyX = center.x;
            center.x = Mathf.Clamp(center.x - offset, root.rect.xMin + 130f, root.rect.xMax - 130f);
            // Near the screen edge the ring moves beside the fixed badge instead.
            if (center.x > bodyX - 60f) return false;
            center.y = Mathf.Clamp(center.y, root.rect.yMin + 130f, root.rect.yMax - 130f);
            burst.rectTransform.anchoredPosition = center;
            return true;
        }

        private void PositionFallbackBurst()
        {
            Rect canvasRect = root.rect;
            if (canvasRect.width <= 1f || canvasRect.height <= 1f)
            {
                // The first update can precede the canvas layout. Use its reference-resolution position.
                burst.rectTransform.anchoredPosition = new Vector2(-549f, 281f);
                return;
            }
            float x = canvasRect.xMin + panel.anchoredPosition.x + panel.rect.width + 75f;
            float y = canvasRect.yMax + panel.anchoredPosition.y - panel.rect.height * .5f;
            float insetX = Mathf.Min(130f, canvasRect.width * .5f);
            float insetY = Mathf.Min(130f, canvasRect.height * .5f);
            burst.rectTransform.anchoredPosition = new Vector2(
                Mathf.Clamp(x, canvasRect.xMin + insetX, canvasRect.xMax - insetX),
                Mathf.Clamp(y, canvasRect.yMin + insetY, canvasRect.yMax - insetY));
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Image image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string name, Transform parent, Font font, Vector2 position,
            Vector2 size, int fontSize, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Text)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text label = rect.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = fontSize - 4;
            label.resizeTextMaxSize = fontSize;
            label.color = color;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
