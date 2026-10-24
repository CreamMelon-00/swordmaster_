using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        public const float BreakQueueFragmentSeconds = .78f;
        private const int MaximumBreakFragmentCards = 32;
        private RectTransform breakQueueLayer;
        private readonly List<BreakFragmentCard> breakFragmentCards = new List<BreakFragmentCard>();
        private int playerShatteredFrom = int.MaxValue, enemyShatteredFrom = int.MaxValue;
        private readonly HashSet<int> enemyRosterShatteredSlots = new HashSet<int>();
        public int BreakFragmentEmissionCount { get; private set; }
        public int ActiveBreakFragmentCardCount
        {
            get
            {
                int count = 0;
                if (!disposed)
                    foreach (BreakFragmentCard card in breakFragmentCards)
                        if (card.Root.gameObject.activeSelf) count++;
                return count;
            }
        }

        private void BuildBreakQueueFragments()
        {
            breakQueueLayer = Rect("Queue Break Fragments", root, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            breakQueueLayer.anchorMin = Vector2.zero;
            breakQueueLayer.anchorMax = Vector2.one;
        }

        /// <summary>The broken side keeps its current skill. Only later reservations fracture, from the last drawn
        /// cards before the rows refresh. The rules own cancellation; these pooled fragments never change the queue.</summary>
        public void PlayBreakQueueCancellation(bool playerSide, int firstCancelledSlot)
        {
            if (disposed || displayedSession == null || firstCancelledSlot < 0) return;
            int previous = playerSide ? playerShatteredFrom : enemyShatteredFrom;
            if (firstCancelledSlot >= previous) return;
            if (playerSide) playerShatteredFrom = firstCancelledSlot;
            else enemyShatteredFrom = firstCancelledSlot;
            QueueView row = playerSide ? playerQueue : enemyQueue;
            var queue = playerSide ? displayedSession.PlayerQueue : displayedSession.EnemyQueue;
            for (int slot = firstCancelledSlot; slot < queue.Count && slot < previous; slot++)
            {
                if (queue[slot] != null) EmitBreakFragment(row, slot, playerSide);
            }
        }

        /// <summary>Only the individual cancelled enemy slots fracture. Mixed owners keep their original shared queue
        /// positions; a subsequent break or defeat can cancel another owner's slots without replaying any earlier one.</summary>
        public void PlayEnemyQueueCancellation()
        {
            if (disposed || !(displayedSession is ILegacyEnemyRosterView roster) || roster.Enemies.Count <= 1) return;
            for (int slot = 0; slot < displayedSession.EnemyQueue.Count; slot++)
                if (roster.IsEnemySlotCanceled(slot) && enemyRosterShatteredSlots.Add(slot))
                    EmitBreakFragment(enemyQueue, slot, false);
        }

        private void EmitBreakFragment(QueueView row, int slot, bool playerSide)
        {
            if (row.GetAnchor(slot) == null ||
                !row.TryGetCardLook(slot, out RectTransform source, out Color ring, out Sprite icon, out _)) return;
            // A cancellation can arrive before the next row refresh. Recheck visibility instead of copying old revealed art.
            if (!playerSide && EnemyCardIsHidden(slot))
            {
                icon = null;
                ring = HiddenQueuePropertyInk(EnemySkillForMask(slot));
            }
            BreakFragmentCard card = AcquireBreakFragmentCard();
            if (card == null) return;
            card.Root.anchoredPosition = breakQueueLayer.InverseTransformPoint(source.TransformPoint(source.rect.center));
            card.Root.localScale = new Vector3(source.lossyScale.x / breakQueueLayer.lossyScale.x,
                source.lossyScale.y / breakQueueLayer.lossyScale.y, 1f);
            card.Root.rotation = source.rotation;
            card.Elapsed = 0f;
            card.Outward = playerSide ? -1f : 1f;
            card.Seed = slot;
            card.Ring = ring;
            card.IconSprite = icon;
            card.Root.gameObject.SetActive(true);
            card.Shell.Configure(null, ring, false, 0f, card.Outward, slot);
            card.Icon.Configure(icon, Color.white, true, 0f, card.Outward, slot);
            BreakFragmentEmissionCount++;
        }

        /// <summary>Real time also carries fragments through hit stop and the finale. The owner calls this after its
        /// pause gate, separately from Refresh, so the battle clock cannot freeze a just-broken card.</summary>
        public void AdvanceBreakQueueFragments(float realDelta)
        {
            if (disposed) return;
            float delta = float.IsNaN(realDelta) || float.IsInfinity(realDelta) ? 0f : Mathf.Max(0f, realDelta);
            foreach (BreakFragmentCard card in breakFragmentCards)
            {
                if (!card.Root.gameObject.activeSelf) continue;
                card.Elapsed = Mathf.Min(BreakQueueFragmentSeconds, card.Elapsed + delta);
                if (card.Elapsed >= BreakQueueFragmentSeconds)
                {
                    card.Root.gameObject.SetActive(false);
                    continue;
                }
                float progress = card.Elapsed / BreakQueueFragmentSeconds;
                card.Shell.Configure(null, card.Ring, false, progress, card.Outward, card.Seed);
                card.Icon.Configure(card.IconSprite, Color.white, true, progress, card.Outward, card.Seed);
            }
        }

        private BreakFragmentCard AcquireBreakFragmentCard()
        {
            foreach (BreakFragmentCard card in breakFragmentCards)
                if (!card.Root.gameObject.activeSelf) return card;
            if (breakFragmentCards.Count >= MaximumBreakFragmentCards) return null;
            var node = Rect("Broken Queued Skill", breakQueueLayer, Vector2.zero,
                Vector2.one * QueueCardSize, Vector2.one * .5f);
            var shell = Rect("Broken Shell", node, Vector2.zero, Vector2.one * QueueCardSize, Vector2.one * .5f)
                .gameObject.AddComponent<DuelQueueShatterGraphic>();
            var icon = Rect("Broken Icon", node, Vector2.zero, Vector2.one * QueueIconSize, Vector2.one * .5f)
                .gameObject.AddComponent<DuelQueueShatterGraphic>();
            var created = new BreakFragmentCard(node, shell, icon);
            breakFragmentCards.Add(created);
            return created;
        }

        private void ClearBreakQueueFragments()
        {
            foreach (BreakFragmentCard card in breakFragmentCards) card.Root.gameObject.SetActive(false);
            playerShatteredFrom = enemyShatteredFrom = int.MaxValue;
            enemyRosterShatteredSlots.Clear();
            BreakFragmentEmissionCount = 0;
        }

        private sealed class BreakFragmentCard
        {
            public readonly RectTransform Root;
            public readonly DuelQueueShatterGraphic Shell, Icon;
            public float Elapsed, Outward;
            public int Seed;
            public Color Ring;
            public Sprite IconSprite;
            public BreakFragmentCard(RectTransform root, DuelQueueShatterGraphic shell, DuelQueueShatterGraphic icon)
            { Root = root; Shell = shell; Icon = icon; }
        }
    }
}
