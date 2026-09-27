using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Animates only the incoming lobby page, leaving the shared navigation available.</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyScreenTransition : MonoBehaviour
    {
        private const float Duration = .22f;
        private const float SlideDistance = 32f;

        private RectTransform page;
        private CanvasGroup pageGroup;
        private Vector3 originalPosition;
        private float originalAlpha;
        private bool originalInteractable, originalBlocksRaycasts;
        private float elapsed, horizontalOffset;

        public bool IsTransitioning { get; private set; }
        public float Progress { get; private set; } = 1f;

        public void Play(RectTransform nextPage, int direction = 1)
        {
            // Restore the previous page before replacement or a rapid second navigation request.
            Finish();
            if (nextPage == null || !isActiveAndEnabled || !nextPage.gameObject.activeInHierarchy)
                return;

            page = nextPage;
            if (!page.TryGetComponent(out pageGroup))
                pageGroup = page.gameObject.AddComponent<CanvasGroup>();

            originalPosition = page.localPosition;
            originalAlpha = pageGroup.alpha;
            originalInteractable = pageGroup.interactable;
            originalBlocksRaycasts = pageGroup.blocksRaycasts;
            horizontalOffset = direction > 0 ? SlideDistance : direction < 0 ? -SlideDistance : 0f;
            elapsed = 0f;
            Progress = 0f;
            IsTransitioning = true;
            pageGroup.interactable = false;
            pageGroup.blocksRaycasts = false;
            Apply(0f);
        }

        public void Advance(float unscaledDelta)
        {
            if (!IsTransitioning) return;
            if (page == null || pageGroup == null || !page.gameObject.activeInHierarchy)
            {
                Finish();
                return;
            }

            if (float.IsNaN(unscaledDelta) || unscaledDelta <= 0f) return;
            elapsed = Mathf.Min(Duration, elapsed + unscaledDelta);
            Progress = elapsed / Duration;
            if (Progress >= 1f)
            {
                Finish();
                return;
            }

            float remaining = 1f - Progress;
            Apply(1f - remaining * remaining * remaining);
        }

        public void Finish()
        {
            if (IsTransitioning)
            {
                if (page != null) page.localPosition = originalPosition;
                if (pageGroup != null)
                {
                    pageGroup.alpha = originalAlpha;
                    pageGroup.interactable = originalInteractable;
                    pageGroup.blocksRaycasts = originalBlocksRaycasts;
                }
            }

            page = null;
            pageGroup = null;
            elapsed = 0f;
            Progress = 1f;
            IsTransitioning = false;
        }

        private void Apply(float easedProgress)
        {
            page.localPosition = originalPosition + new Vector3(horizontalOffset * (1f - easedProgress), 0f, 0f);
            pageGroup.alpha = originalAlpha * easedProgress;
        }

        private void Update() => Advance(Time.unscaledDeltaTime);
        private void OnDisable() => Finish();
        private void OnDestroy() => Finish();
    }
}
