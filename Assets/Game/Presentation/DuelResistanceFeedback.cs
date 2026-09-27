using System;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Signed resistance changes on a separate real-time clock, without hit feedback.</summary>
    public sealed class DuelResistanceFeedback : IDisposable
    {
        private const float Duration = 1f;
        private const float FadeDuration = .4f;
        private readonly RectTransform root;
        private readonly Canvas canvas;
        private readonly Text playerLabel, enemyLabel;
        private float playerRemaining, enemyRemaining;
        private bool disposed;

        public DuelResistanceFeedback(Transform parent, Font font)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (font == null) throw new ArgumentNullException(nameof(font));
            root = new GameObject("Duel Resistance Feedback", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = root.anchoredPosition = Vector2.zero;
            canvas = root.GetComponentInParent<Canvas>();
            playerLabel = CreateLabel("Player Resistance Effect", font);
            enemyLabel = CreateLabel("Enemy Resistance Effect", font);
        }

        public void Show(bool targetPlayer, int delta)
        {
            if (disposed || delta == 0) return;
            Text label = targetPlayer ? playerLabel : enemyLabel;
            label.text = "저항 " + (delta > 0 ? "+" : string.Empty) + delta;
            label.color = delta > 0 ? DuelVisualTheme.Health : DuelVisualTheme.Danger;
            if (targetPlayer) playerRemaining = Duration;
            else enemyRemaining = Duration;
            label.gameObject.SetActive(true);
        }

        public void Tick(float realDelta, Camera camera, Transform player, Transform enemy)
        {
            if (disposed) return;
            UpdateLabel(playerLabel, ref playerRemaining, realDelta, camera, player);
            UpdateLabel(enemyLabel, ref enemyRemaining, realDelta, camera, enemy);
        }

        public void Reset()
        {
            if (disposed) return;
            playerRemaining = enemyRemaining = 0f;
            playerLabel.text = enemyLabel.text = string.Empty;
            playerLabel.gameObject.SetActive(false);
            enemyLabel.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
            UnityEngine.Object.Destroy(root.gameObject);
        }

        private Text CreateLabel(string name, Font font)
        {
            var labelRoot = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            labelRoot.SetParent(root, false);
            labelRoot.anchorMin = labelRoot.anchorMax = labelRoot.pivot = Vector2.one * .5f;
            labelRoot.sizeDelta = new Vector2(180f, 38f);
            var label = labelRoot.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 26;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
            label.raycastTarget = false;
            var shadow = labelRoot.gameObject.AddComponent<Shadow>();
            shadow.effectColor = DuelVisualTheme.Surface;
            shadow.effectDistance = new Vector2(1f, -1f);
            labelRoot.gameObject.SetActive(false);
            return label;
        }

        private void UpdateLabel(Text label, ref float remaining, float realDelta, Camera camera, Transform actor)
        {
            remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, realDelta));
            if (remaining <= 0f || camera == null || actor == null || !actor.gameObject.activeInHierarchy ||
                root.rect.width <= 1f || root.rect.height <= 1f)
            {
                label.gameObject.SetActive(false);
                return;
            }
            // The fixed actor-local point avoids animation-dependent sprite bounds.
            // Keep the text below the existing head-attached status/queue stack.
            Vector3 screen = camera.WorldToScreenPoint(actor.TransformPoint(new Vector3(-.6f, .7f, 0f)));
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 position))
            {
                label.gameObject.SetActive(false);
                return;
            }
            Vector2 half = label.rectTransform.sizeDelta * .5f;
            position.x = Mathf.Clamp(position.x, root.rect.xMin + half.x, root.rect.xMax - half.x);
            position.y = Mathf.Clamp(position.y, root.rect.yMin + half.y, root.rect.yMax - half.y);
            label.rectTransform.anchoredPosition = position;
            Color ink = label.color;
            ink.a = Mathf.Clamp01(remaining / FadeDuration);
            label.color = ink;
            label.gameObject.SetActive(true);
        }
    }
}
