using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        public const string QueueBackdropName = "Combat Queue Backdrop";
        // The current arena draws scenery at -5, and the player/three-enemy roster at 0..-3.
        public const int QueueBackdropSortingOrder = -4;
        public const float QueueBackdropAlpha = .38f, QueueBackdropScale = 1.35f;
        private const float QueueBackdropHeight = .53f;
        private RectTransform queueBackdrop, queueBackdropContent;
        private Canvas queueBackdropCanvas;
        private CanvasGroup queueBackdropGroup;
        private Camera queueBackdropAnchorCamera;
        private float queueBackdropWorldY;
        private RectTransform[] queueBackdropElements;
        private int[] queueBackdropSiblingIndices;
        private readonly List<Transform> queueBackdropLayerNodes = new List<Transform>();

        /// <summary>The original rows are on the arena canvas; hiding the HUD retains this placement until restored.</summary>
        public bool QueueIsBackdrop { get; private set; }

        private void PrepareQueueBackdrop(bool resolving)
        {
            bool show = resolving && worldCamera != null && root.gameObject.activeInHierarchy;
            if (show && queueBackdrop == null) BuildQueueBackdrop();
            if (QueueIsBackdrop != show)
            {
                if (show)
                {
                    for (int i = 0; i < queueBackdropElements.Length; i++)
                        queueBackdropSiblingIndices[i] = queueBackdropElements[i].GetSiblingIndex();
                    foreach (RectTransform element in queueBackdropElements)
                    {
                        element.SetParent(queueBackdropContent, false);
                        SetQueueVisualLayer(element, queueBackdrop.gameObject.layer);
                    }
                }
                else RestoreQueueBackdrop();
                QueueIsBackdrop = show;
            }
            if (queueBackdrop == null) return;
            queueBackdropCanvas.worldCamera = worldCamera;
            queueBackdropCanvas.planeDistance = worldCamera != null
                ? Mathf.Clamp(1f, worldCamera.nearClipPlane + .1f, worldCamera.farClipPlane - .1f) : 1f;
            queueBackdrop.gameObject.SetActive(show);
            queuePanels.gameObject.SetActive(!show);
            queueBackdropGroup.alpha = QueueBackdropAlpha * fade.alpha;
        }

        private void BuildQueueBackdrop()
        {
            queueBackdrop = Rect(QueueBackdropName, root.parent, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            // Use the arena's own culling layer without coupling the HUD to a particular layer number.
            int mask = worldCamera.cullingMask;
            int layer = root.gameObject.layer;
            for (int i = 0; i < 32; i++)
                if ((mask & (1 << i)) != 0) { layer = i; break; }
            queueBackdrop.gameObject.layer = layer;
            queueBackdropCanvas = queueBackdrop.gameObject.AddComponent<Canvas>();
            queueBackdropCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            queueBackdropCanvas.worldCamera = worldCamera;
            queueBackdropCanvas.sortingLayerID = 0;
            queueBackdropCanvas.sortingOrder = QueueBackdropSortingOrder;
            queueBackdropCanvas.pixelPerfect = canvas.pixelPerfect;
            var scaler = queueBackdrop.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            // CanvasScaler's first Update may be later than this refresh; use the live HUD scale immediately.
            queueBackdropCanvas.scaleFactor = canvas.scaleFactor;
            queueBackdropGroup = queueBackdrop.gameObject.AddComponent<CanvasGroup>();
            queueBackdropGroup.interactable = queueBackdropGroup.blocksRaycasts = false;
            // Logical positions remain the original HUD's, including the readout/camera clearance below.
            // Only this common parent scales/translates the actual queue visuals into the battle scene.
            queueBackdropContent = Rect("Queue Backdrop Content", queueBackdrop, Vector2.zero,
                root.rect.size, Vector2.one * .5f);
            queueBackdropElements = new[] { queueCues, playerQueue.Root, enemyQueue.Root, queueSparks,
                queueCancellationLayer, breakQueueLayer };
            queueBackdropSiblingIndices = new int[queueBackdropElements.Length];
        }

        private void LayoutQueueBackdrop()
        {
            if (!QueueIsBackdrop || queueBackdropContent == null) return;
            Vector2 size = root.rect.size;
            if (size.x < 1f || size.y < 1f) size = new Vector2(1920f, 1080f);
            // Fit using the full panel rather than the shrinking remaining queue: consumption never changes zoom.
            float extent = Mathf.Min(QueuePanelWidth + Mathf.Max(QueueSeamGap * .5f, queueSeamReserve),
                size.x * .5f - ScreenMargin) + QueueCardWidth * QueueActiveCardMaxScale;
            float scale = Mathf.Min(QueueBackdropScale, Mathf.Max(.1f, (size.x * .5f - ScreenMargin) / extent));
            queueBackdropContent.sizeDelta = size;
            queueBackdropCanvas.scaleFactor = canvas.scaleFactor;
            queueBackdropContent.localScale = Vector3.one * scale;
            // Keep the initial battle height in the arena. The screen-space canvas still preserves icon size,
            // but its content cancels vertical camera travel instead of riding every camera lift and dip.
            float distance = queueBackdropCanvas.planeDistance;
            if (queueBackdropAnchorCamera != worldCamera)
            {
                queueBackdropAnchorCamera = worldCamera;
                queueBackdropWorldY = worldCamera.ViewportToWorldPoint(
                    new Vector3(.5f, QueueBackdropHeight, distance)).y;
            }
            Vector3 anchor = worldCamera.ViewportToWorldPoint(new Vector3(.5f, .5f, distance));
            anchor.y = queueBackdropWorldY;
            Vector3 viewport = worldCamera.WorldToViewportPoint(anchor);
            queueBackdropContent.anchoredPosition = new Vector2(size.x * (viewport.x - .5f),
                size.y * (viewport.y - .5f) - playerQueue.Root.anchoredPosition.y * scale);
            queueBackdropGroup.alpha = QueueBackdropAlpha * fade.alpha;
        }

        private void SetQueueVisualLayer(RectTransform visual, int layer)
        {
            queueBackdropLayerNodes.Clear();
            visual.GetComponentsInChildren(true, queueBackdropLayerNodes);
            foreach (Transform node in queueBackdropLayerNodes) node.gameObject.layer = layer;
        }

        private void RestoreQueueBackdrop()
        {
            if (queueBackdropElements != null && QueueIsBackdrop)
            {
                foreach (RectTransform element in queueBackdropElements)
                {
                    if (element == null) continue;
                    element.SetParent(root, false);
                    SetQueueVisualLayer(element, root.gameObject.layer);
                }
                // The six elements were built in this order; restore their original places among the other HUD nodes.
                for (int i = 0; i < queueBackdropElements.Length; i++)
                    if (queueBackdropElements[i] != null)
                        queueBackdropElements[i].SetSiblingIndex(queueBackdropSiblingIndices[i]);
            }
            QueueIsBackdrop = false;
            queueBackdropAnchorCamera = null;
            if (queueBackdrop != null) queueBackdrop.gameObject.SetActive(false);
            if (queuePanels != null) queuePanels.gameObject.SetActive(true);
        }

        private void DisposeQueueBackdrop()
        {
            RestoreQueueBackdrop();
            if (queueBackdrop != null) Destroy(queueBackdrop.gameObject);
            queueBackdrop = queueBackdropContent = null;
            queueBackdropCanvas = null;
            queueBackdropGroup = null;
        }
    }
}
