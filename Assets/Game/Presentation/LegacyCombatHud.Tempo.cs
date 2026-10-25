using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        private int tempoDisplayStartHalfTick;
        private static bool UsesTempoQueue(ILegacyDuelView session)
            => session is LegacyQueuedDuel duel && duel.HasTempoSkills;

        private readonly List<int> tempoPlayerCounters = new List<int>(), tempoEnemyCounters = new List<int>();
        private readonly List<LegacySkill> tempoCounterPlayerQueue = new List<LegacySkill>(),
            tempoCounterEnemyQueue = new List<LegacySkill>();
        private ILegacyDuelView tempoCounterSession;
        private ILegacySlotView tempoCounterSlot;
        private int tempoCounterSignature;

        private void RefreshCounterForecasts(ILegacyDuelView session)
        {
            bool tempo = UsesTempoQueue(session);
            int signature = 0;
            if (tempo)
            {
                unchecked
                {
                    signature = (((int)session.Phase * 31 + session.RoundNumber) * 31 + session.Act) * 31 + CycleSignature(session);
                    signature = signature * 31 + session.Player.Health;
                    signature = signature * 31 + session.Player.Resistance;
                    signature = signature * 31 + session.Enemy.Health;
                    signature = signature * 31 + session.Enemy.Resistance;
                    signature = signature * 31 + session.PlayerCountersRemaining;
                    signature = signature * 31 + session.EnemyCountersRemaining;
                    signature = signature * 31 + ((session as LegacyQueuedDuel)?.CurrentSlot?.HitsResolved ?? -1);
                    if (session is ILegacyEnemyRosterView roster)
                        foreach (LegacyFighterState enemy in roster.Enemies)
                        {
                            signature = signature * 31 + enemy.Health;
                            signature = signature * 31 + enemy.Resistance;
                        }
                }
                if (ReferenceEquals(tempoCounterSession, session) && ReferenceEquals(tempoCounterSlot, session.CurrentSlot) &&
                    signature == tempoCounterSignature && SameSkills(tempoCounterPlayerQueue, session.PlayerQueue) &&
                    SameSkills(tempoCounterEnemyQueue, session.EnemyQueue))
                {
                    playerQueue.SetCounterMarks(tempoPlayerCounters);
                    enemyQueue.SetCounterMarks(tempoEnemyCounters);
                    return;
                }
            }
            session.ForecastEnemyCounterSlots(counterForecast);
            MapTempoCounterMarks(session, true);
            playerQueue.SetCounterMarks(counterForecast);
            RememberCounters(tempoPlayerCounters, counterForecast);
            session.ForecastPlayerCounterSlots(counterForecast);
            MapTempoCounterMarks(session, false);
            enemyQueue.SetCounterMarks(counterForecast);
            RememberCounters(tempoEnemyCounters, counterForecast);
            tempoCounterSession = tempo ? session : null;
            tempoCounterSlot = session.CurrentSlot;
            tempoCounterSignature = signature;
            Remember(tempoCounterPlayerQueue, session.PlayerQueue);
            Remember(tempoCounterEnemyQueue, session.EnemyQueue);
        }

        private static void RememberCounters(List<int> target, List<int> source)
        {
            target.Clear();
            target.AddRange(source);
        }

        private void MapTempoCounterMarks(ILegacyDuelView session, bool attackerIsPlayer)
        {
            if (!UsesTempoQueue(session)) return;
            var duel = (LegacyQueuedDuel)session;
            for (int i = counterForecast.Count - 1; i >= 0; i--)
            {
                int queueIndex = duel.QueueIndexAtSegment(attackerIsPlayer, counterForecast[i]);
                if (queueIndex < 0) counterForecast.RemoveAt(i);
                else counterForecast[i] = queueIndex;
            }
        }

        // Both rows use the same half-tick axis. The visible widths are exactly .5 / 1 / 2 ordinary cards;
        // padding after an unmatched fast technique stays empty instead of moving the next technique forward.
        private void LayoutTempoQueuePanels(bool resolving)
        {
            Vector2 size = root.rect.size;
            if (size.x < 1f || size.y < 1f) size = new Vector2(1920f, 1080f);
            float card = QueueCardWidth, pad = LegacyQueuePanelLayout.Padding;
            float room = QueueCardRoom(), trayWidth = room + pad * 2f;
            float seam = Mathf.Max(QueueSeamGap * .5f, queueSeamReserve);
            int end = Mathf.Max(playerQueue.TempoEnd, enemyQueue.TempoEnd);
            int capacity = Mathf.Max(2, Mathf.FloorToInt(room / card) * 2);
            int consumed = resolving ? tempoDisplayStartHalfTick : 0;
            // Keep the whole action in view while it winds up across several segments.
            int earliest = consumed;
            if (resolving)
            {
                earliest = Mathf.Min(earliest, playerQueue.TempoActionStart(consumed));
                earliest = Mathf.Min(earliest, enemyQueue.TempoActionStart(consumed));
            }
            int focus = inspectedSlot >= 0 ? enemyQueue.TempoFocusEnd(inspectedSlot) : playerQueue.TempoEnd;
            int start = resolving ? Mathf.Max(0, earliest / 2 * 2)
                : Mathf.Max(0, (focus - capacity + 1) / 2 * 2);
            start = Mathf.Min(start, Mathf.Max(0, (end - capacity + 1) / 2 * 2));
            queueWindowStart = start / 2;
            queueWindowCapacity = capacity / 2;
            queueSlideFrom = queueSlideElapsed = 0f;
            queueConsumed = consumed / 2;
            float range = end > capacity ? QueueRangeHeight : 0f;
            float trayBottom = Mathf.Lerp(DockBottomInset + DockHeight + QueuePanelDockGap,
                QueuePanelResolvingBottom, OutQuad(Mathf.Clamp01(transition)));
            float trayHeight = card + pad * 2f + range;
            float cardLine = trayBottom + range + pad + card * .5f;
            float lineY = cardLine - size.y * .5f;
            playerQueue.Root.anchoredPosition = enemyQueue.Root.anchoredPosition = new Vector2(0f, lineY);
            playerQueue.ClearPose();
            enemyQueue.ClearPose();
            playerQueue.LayoutTempo(seam + pad, card, start, capacity);
            enemyQueue.LayoutTempo(seam + pad, card, start, capacity);
            queueCues.anchoredPosition = queueSparks.anchoredPosition = new Vector2(0f, lineY);
            float trayY = trayBottom + trayHeight * .5f - size.y * .5f;
            playerQueuePanel.anchoredPosition = new Vector2(-(seam + trayWidth * .5f), trayY);
            enemyQueuePanel.anchoredPosition = new Vector2(seam + trayWidth * .5f, trayY);
            playerQueuePanel.sizeDelta = enemyQueuePanel.sizeDelta = new Vector2(trayWidth, trayHeight);
            UpdateQueueRange(playerQueueRange, (playerQueue.TempoEnd + 1) / 2, consumed / 2, range, trayWidth, trayHeight);
            UpdateQueueRange(enemyQueueRange, (enemyQueue.TempoEnd + 1) / 2, consumed / 2, range, trayWidth, trayHeight);
            queueSeam.anchoredPosition = new Vector2(0f, lineY);
            queueSeam.localScale = Vector3.one * queueCardScale;
            queuePanelTop = trayBottom + trayHeight;
            queueRowTop = cardLine + card * .5f + QueueMarkRise * queueCardScale;
            queuePanelFloor = QueuePanelKeepsFightersClear
                ? Mathf.Clamp01((queuePanelTop + QueuePanelFighterClearance) / size.y) : 0f;
        }

        private sealed partial class QueueView
        {
            private bool tempoLayout, tempoResolving;
            private int tempoConsumed;
            private readonly List<LegacySkill> tempoRemembered = new List<LegacySkill>();
            private IReadOnlyList<LegacyTimedSkill> tempoEntries = Array.Empty<LegacyTimedSkill>();
            public int TempoEnd => tempoEntries.Count == 0 ? 0 : (tempoEntries[tempoEntries.Count - 1].EndHalfTick + 1) / 2 * 2;
            public int TempoFocusEnd(int slot) => slot >= 0 && slot < tempoEntries.Count ? tempoEntries[slot].EndHalfTick : TempoEnd;
            public int TempoCompletionIndex(int halfTick)
            {
                for (int i = 0; i < tempoEntries.Count; i++)
                    if (tempoEntries[i].EndHalfTick == halfTick) return i;
                return -1;
            }
            public int TempoActionStart(int halfTick)
            {
                for (int i = 0; i < tempoEntries.Count; i++)
                    if (tempoEntries[i].StartHalfTick <= halfTick && tempoEntries[i].EndHalfTick > halfTick)
                        return tempoEntries[i].StartHalfTick;
                return halfTick;
            }

            public void PrepareTempo(IReadOnlyList<LegacySkill> queue, bool enabled, bool resolving, int consumed)
            {
                if (tempoLayout && !enabled) ResetTempo();
                tempoLayout = enabled;
                tempoResolving = resolving;
                tempoConsumed = consumed;
                if (!enabled) return;
                bool changed = tempoRemembered.Count != queue.Count;
                for (int i = 0; !changed && i < queue.Count; i++) changed = !ReferenceEquals(tempoRemembered[i], queue[i]);
                if (!changed) return;
                tempoRemembered.Clear();
                for (int i = 0; i < queue.Count; i++) tempoRemembered.Add(queue[i]);
                tempoEntries = LegacySkillTiming.Build(tempoRemembered);
            }

            private bool TempoUnconsumed(int index) => !tempoResolving ||
                index < tempoEntries.Count && tempoEntries[index].EndHalfTick > tempoConsumed;

            public void LayoutTempo(float innerEdge, float cardSize, int start, int capacity)
            {
                CurrentPitch = cardSize;
                DisplaySize = HorizontalSpan = Vector2.zero;
                float minimum = float.MaxValue, maximum = 0f;
                for (int n = visibleItems.Count - 1; n >= 0; n--)
                {
                    int index = visibleSlots[n];
                    LegacyTimedSkill entry = tempoEntries[index];
                    if (entry.StartHalfTick >= start && entry.EndHalfTick <= start + capacity) continue;
                    visibleItems[n].gameObject.SetActive(false);
                    visibleItems.RemoveAt(n);
                    visibleSlots.RemoveAt(n);
                }
                for (int n = 0; n < visibleItems.Count; n++)
                {
                    int index = visibleSlots[n];
                    LegacyTimedSkill timed = tempoEntries[index];
                    RectTransform item = visibleItems[n];
                    float ratio = timed.DurationHalfTicks * .5f;
                    item.sizeDelta = new Vector2(QueueCardSize * ratio, QueueCardSize);
                    brokenGapLabels[index].rectTransform.sizeDelta = new Vector2(Mathf.Max(22f, item.sizeDelta.x - 6f), 46f);
                    highlights[index].rectTransform.sizeDelta = item.sizeDelta;
                    icons[index].rectTransform.sizeDelta = new Vector2(QueueIconSize * ratio, QueueIconSize);
                    icons[index].preserveAspect = false;
                    if (player && index < forecastMarks.Count)
                        forecastMarks[index].anchoredPosition = new Vector2(-QueueCardSize * ratio * .5f + 7f, ForecastMarkCenter.y);
                    float near = innerEdge + (timed.StartHalfTick - start) * cardSize * .5f;
                    float width = ratio * cardSize;
                    LegacyQueueInsertionPose entry = insertion.Pose(index);
                    item.anchoredPosition = new Vector2((player ? -1f : 1f) * (near + width * .5f), entry.Height * cardSize);
                    item.localScale = new Vector3(restScales[index] * entry.ScaleX, restScales[index] * entry.ScaleY, restScales[index]);
                    item.localRotation = Quaternion.identity;
                    minimum = Mathf.Min(minimum, near);
                    maximum = Mathf.Max(maximum, near + width);
                }
                if (visibleItems.Count == 0) return;
                DisplaySize = new Vector2(maximum - minimum, cardSize);
                HorizontalSpan = player ? new Vector2(-maximum, -minimum) : new Vector2(minimum, maximum);
            }

            private void ResetTempo()
            {
                tempoLayout = tempoResolving = false;
                tempoConsumed = 0;
                tempoRemembered.Clear();
                tempoEntries = Array.Empty<LegacyTimedSkill>();
                // Pooled views must regain their square geometry when the next turn uses ordinary skills only.
                for (int i = 0; i < icons.Count; i++)
                {
                    ((RectTransform)icons[i].transform.parent).sizeDelta = Vector2.one * QueueCardSize;
                    highlights[i].rectTransform.sizeDelta = Vector2.one * QueueCardSize;
                    icons[i].rectTransform.sizeDelta = Vector2.one * QueueIconSize;
                    brokenGapLabels[i].rectTransform.sizeDelta = new Vector2(58f, 46f);
                    icons[i].preserveAspect = true;
                    if (player && i < forecastMarks.Count) forecastMarks[i].anchoredPosition = ForecastMarkCenter;
                }
            }
        }
    }
}
