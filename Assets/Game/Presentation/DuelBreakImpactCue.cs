using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>A real-time impulse at the instant resistance breaks, separate from the lasting broken aura.</summary>
    public sealed class DuelBreakImpactCue : IDisposable
    {
        private const float BurstDuration = .65f;
        private const float EdgeDuration = .38f;
        private static readonly Color PlayerInk = new Color32(255, 81, 68, 255);
        private static readonly Color EnemyInk = new Color32(255, 208, 119, 255);
        private static readonly int AttackOffsetId = Shader.PropertyToID("_AttackOffset");
        private readonly MaterialPropertyBlock actorProperties = new MaterialPropertyBlock();

        private readonly RectTransform root;
        private readonly Canvas canvas;
        private readonly View player, enemy;
        private readonly DuelBreakShieldClip shieldClip;
        private Camera lastCamera;
        private Transform lastPlayer, lastEnemy;
        private bool disposed;

        public DuelBreakImpactCue(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            root = new GameObject("Break Impact Cues", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = root.anchoredPosition = Vector2.zero;
            // The impact sits behind state callouts, damage and the skill dock. Its centre stays transparent.
            root.SetAsFirstSibling();
            canvas = root.GetComponentInParent<Canvas>();
            shieldClip = new DuelBreakShieldClip();
            player = CreateView(true);
            enemy = CreateView(false);
            Reset();
        }

        public void Show(bool targetPlayer)
        {
            if (disposed) return;
            View view = targetPlayer ? player : enemy;
            view.Elapsed = 0f;
            view.Burst.Configure(view.Tint, DuelSkillBurstStyle.Break, 0f);
            view.Burst.gameObject.SetActive(true);
            view.Edge.SetProgress(0f);
            view.Edge.gameObject.SetActive(true);
            view.Shield.sprite = shieldClip.Sample(0f);
            view.Shield.enabled = shieldClip.IsReady;
            view.Shield.gameObject.SetActive(shieldClip.IsReady);
            Position(view, lastCamera, targetPlayer ? lastPlayer : lastEnemy);
        }

        public void Tick(float realDelta, Camera arenaCamera, Transform playerActor, Transform enemyActor)
        {
            if (disposed) return;
            lastCamera = arenaCamera;
            lastPlayer = playerActor;
            lastEnemy = enemyActor;
            float delta = realDelta > 0f && !float.IsNaN(realDelta) && !float.IsInfinity(realDelta)
                ? realDelta : 0f;
            TickView(player, delta, arenaCamera, playerActor);
            TickView(enemy, delta, arenaCamera, enemyActor);
        }

        public void Reset()
        {
            if (disposed) return;
            ResetView(player);
            ResetView(enemy);
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
            shieldClip.Dispose();
            Object.Destroy(root.gameObject);
        }

        private View CreateView(bool targetPlayer)
        {
            string side = targetPlayer ? "Player" : "Enemy";
            Color tint = targetPlayer ? PlayerInk : EnemyInk;
            var edgeRoot = new GameObject(side + " Break Edge", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(DuelBreakEdgeFlash)).GetComponent<RectTransform>();
            edgeRoot.SetParent(root, false);
            edgeRoot.anchorMin = Vector2.zero;
            edgeRoot.anchorMax = Vector2.one;
            edgeRoot.sizeDelta = edgeRoot.anchoredPosition = Vector2.zero;
            var edge = edgeRoot.GetComponent<DuelBreakEdgeFlash>();
            edge.Configure(targetPlayer, tint);

            var burstRoot = new GameObject(side + " Break Impact", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(DuelSkillActivationBurst)).GetComponent<RectTransform>();
            burstRoot.SetParent(root, false);
            burstRoot.anchorMin = burstRoot.anchorMax = Vector2.one * .5f;
            burstRoot.sizeDelta = Vector2.one * 340f;
            var burst = burstRoot.GetComponent<DuelSkillActivationBurst>();
            burst.raycastTarget = false;
            var shieldRoot = new GameObject(side + " Break Shield", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            shieldRoot.SetParent(root, false);
            shieldRoot.anchorMin = shieldRoot.anchorMax = Vector2.one * .5f;
            shieldRoot.sizeDelta = Vector2.one * 256f;
            var shield = shieldRoot.GetComponent<Image>();
            shield.raycastTarget = false;
            shield.color = Color.white;
            return new View(targetPlayer, tint, burst, edge, shield);
        }

        private void TickView(View view, float delta, Camera arenaCamera, Transform actor)
        {
            if (!view.Burst.gameObject.activeSelf) return;
            view.Elapsed += delta;
            if (view.Elapsed >= BurstDuration)
            {
                ResetView(view);
                return;
            }
            view.Burst.Configure(view.Tint, DuelSkillBurstStyle.Break, view.Elapsed / BurstDuration);
            view.Shield.sprite = shieldClip.Sample(view.Elapsed);
            view.Shield.gameObject.SetActive(shieldClip.IsReady && view.Elapsed < shieldClip.Duration);
            Position(view, arenaCamera, actor);
            if (view.Elapsed >= EdgeDuration) view.Edge.gameObject.SetActive(false);
            else view.Edge.SetProgress(view.Elapsed / EdgeDuration);
        }

        private void Position(View view, Camera arenaCamera, Transform actor)
        {
            Rect bounds = root.rect;
            Vector2 position = new Vector2(bounds.xMin + bounds.width * (view.TargetPlayer ? .3f : .7f),
                bounds.yMin + bounds.height * .48f);
            view.Shield.enabled = shieldClip.IsReady;
            view.Shield.rectTransform.sizeDelta = Vector2.one * 256f;
            if (arenaCamera != null && actor != null && actor.gameObject.activeInHierarchy)
            {
                var renderer = actor.GetComponent<SpriteRenderer>();
                string spriteName = renderer != null && renderer.sprite != null ? renderer.sprite.name : "";
                bool centredBody = spriteName.StartsWith("final-", StringComparison.Ordinal) ||
                    spriteName.StartsWith("enemy-", StringComparison.Ordinal) ||
                    spriteName.StartsWith("cadet-", StringComparison.Ordinal);
                // The shield and its surrounding wave share one torso anchor, including during camera focus.
                Vector3 localBody = centredBody ? new Vector3(0f, -.15f, 0f) : DuelStepHud.ActorBodyLocalOffset;
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(actorProperties);
                    Vector4 offset = actorProperties.GetVector(AttackOffsetId);
                    if (Finite(offset.x) && Finite(offset.y) && Finite(offset.z))
                        localBody += new Vector3(offset.x, offset.y, offset.z);
                }
                // AttackSprite applies this displacement after sprite flipping without moving the Transform.
                Vector3 body = actor.TransformPoint(localBody);
                Vector3 screen = arenaCamera.WorldToScreenPoint(body);
                Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera : null;
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        root, screen, uiCamera, out Vector2 projected))
                {
                    position = projected;
                    Vector3 right = arenaCamera.WorldToScreenPoint(body + arenaCamera.transform.right * DuelBreakShieldClip.WorldWidth);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, right, uiCamera, out Vector2 edge))
                        view.Shield.rectTransform.sizeDelta = Vector2.one * Vector2.Distance(projected, edge);
                }
                else view.Shield.enabled = false;
            }
            // Do not clamp only the wave inward at a screen edge: that separates its centre from the shield.
            view.Shield.rectTransform.anchoredPosition = position;
            view.Burst.rectTransform.anchoredPosition = position;
            // The procedural arcs use local UI pixels. Match them to the projected shield instead of a
            // fixed HUD size, otherwise the 1920px combat canvas and camera close-ups hide them inside the body.
            view.Burst.rectTransform.localScale = Vector3.one * (view.Shield.rectTransform.sizeDelta.x / 200f);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ResetView(View view)
        {
            view.Elapsed = 0f;
            view.Burst.gameObject.SetActive(false);
            view.Edge.gameObject.SetActive(false);
            view.Shield.gameObject.SetActive(false);
        }

        private sealed class View
        {
            public readonly bool TargetPlayer;
            public readonly Color Tint;
            public readonly DuelSkillActivationBurst Burst;
            public readonly DuelBreakEdgeFlash Edge;
            public readonly Image Shield;
            public float Elapsed;

            public View(bool targetPlayer, Color tint, DuelSkillActivationBurst burst, DuelBreakEdgeFlash edge, Image shield)
            {
                TargetPlayer = targetPlayer;
                Tint = tint;
                Burst = burst;
                Edge = edge;
                Shield = shield;
            }
        }
    }

    /// <summary>A brief gradient on the struck side of the screen. It never fills or blocks the play area.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelBreakEdgeFlash : MaskableGraphic
    {
        private bool playerSide;
        private float progress = 1f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Configure(bool targetPlayer, Color tint)
        {
            playerSide = targetPlayer;
            color = tint;
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void SetProgress(float normalizedAge)
        {
            float next = Mathf.Clamp01(normalizedAge);
            if (Mathf.Approximately(progress, next)) return;
            progress = next;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (progress >= 1f) return;
            Rect rect = rectTransform.rect;
            float width = Mathf.Min(136f, rect.width * .2f);
            if (width <= 0f || rect.height <= 0f) return;
            float fade = (1f - progress) * (1f - progress);
            Color outer = color;
            outer.a = (playerSide ? .38f : .24f) * fade;
            Color inner = outer;
            inner.a = 0f;
            float outside = playerSide ? rect.xMin : rect.xMax;
            float inside = outside + (playerSide ? width : -width);
            int first = vertices.currentVertCount;
            vertices.AddVert(new Vector2(outside, rect.yMin), outer, Vector2.zero);
            vertices.AddVert(new Vector2(outside, rect.yMax), outer, Vector2.up);
            vertices.AddVert(new Vector2(inside, rect.yMax), inner, Vector2.one);
            vertices.AddVert(new Vector2(inside, rect.yMin), inner, Vector2.right);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first + 2, first + 3, first);
        }
    }
}
