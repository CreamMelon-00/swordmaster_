using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        private const float StatusFootGap = 18f, StatusStripSeparation = 8f;
        private LegacyArenaView statusArena;
        private LegacyStatusStrip playerStatuses;
        private readonly List<LegacyStatusStrip> enemyStatuses = new List<LegacyStatusStrip>();
        private readonly List<Rect> statusStripBounds = new List<Rect>();
        private readonly Vector3[] statusPanelCorners = new Vector3[4];
        private readonly List<LegacyStatusSnapshot> bossPlayerStatuses = new List<LegacyStatusSnapshot>();
        private readonly List<LegacyStatusSnapshot> bossEnemyStatuses = new List<LegacyStatusSnapshot>();

        /// <summary>Reuses the arena's physical ground anchors, including each waiting enemy in a multi-actor encounter.</summary>
        public void SetStatusArena(LegacyArenaView arena) => statusArena = arena;

        public LegacyStatusStrip GetStatusStrip(bool playerSide, int enemyIndex = 0)
            => disposed ? null : playerSide ? playerStatuses
                : enemyIndex >= 0 && enemyIndex < enemyStatuses.Count ? enemyStatuses[enemyIndex] : null;

        private void RefreshFighterStatuses(ILegacyDuelView session, Transform playerActor, Transform enemyActor, Camera camera)
        {
            if (playerStatuses == null) playerStatuses = LegacyStatusStrip.Create(root, art.UIFont, "Player Status Icons");
            var roster = session as ILegacyEnemyRosterView;
            int enemies = roster != null ? roster.Enemies.Count : 1;
            while (enemyStatuses.Count < enemies)
                enemyStatuses.Add(LegacyStatusStrip.Create(root, art.UIFont, "Enemy Status Icons " + enemyStatuses.Count));
            var boss = session as LegacyQueuedDuel;
            bool iia = boss != null && boss.IsIiaBossBattle;
            playerStatuses.Refresh(iia ? BossStatuses(session.PlayerStatuses, bossPlayerStatuses,
                LegacyStatusKind.Fixed, boss.PlayerFixedTurnsRemaining) : session.PlayerStatuses);
            bool any = playerStatuses.Count > 0 && !session.Player.IsDefeated;
            for (int index = 0; index < enemyStatuses.Count; index++)
            {
                LegacyStatusStrip strip = enemyStatuses[index];
                if (index >= enemies)
                {
                    strip.SetVisible(false);
                    continue;
                }
                IReadOnlyList<LegacyStatusSnapshot> statuses = roster != null ? roster.GetEnemyStatuses(index) : session.EnemyStatuses;
                strip.Refresh(iia && index == 0 ? BossStatuses(statuses, bossEnemyStatuses,
                    LegacyStatusKind.Jealousy, boss.IiaJealousy) : statuses);
                bool alive = roster != null ? !roster.Enemies[index].IsDefeated : !session.Enemy.IsDefeated;
                strip.SetVisible(alive);
                any |= alive && strip.Count > 0;
            }
            // No status, no additional camera reservation: normal framing remains exactly as before.
            if (any && statusArena != null && QueuePanelKeepsFightersClear)
            {
                float canvasHeight = root.rect.height > 1f ? root.rect.height : 1080f;
                float top = Mathf.Max(queueRowTop, CombatReadoutTop);
                queuePanelFloor = Mathf.Max(queuePanelFloor,
                    Mathf.Clamp01((top + StatusFootGap + LegacyStatusStrip.Height + 12f) / canvasHeight));
            }
            statusStripBounds.Clear();
            AddStatusPanelBlocker(playerQueuePanel, QueueMarkRise * queueCardScale);
            AddStatusPanelBlocker(enemyQueuePanel, QueueMarkRise * queueCardScale);
            AddStatusPanelBlocker(combatReadout, 0f);
            PlaceStatusStrip(playerStatuses, session.Player.IsDefeated, camera,
                statusArena != null ? statusArena.PlayerGroundPosition : GroundAnchor(playerActor));
            for (int index = 0; index < enemies; index++)
            {
                bool defeated = roster != null ? roster.Enemies[index].IsDefeated : session.Enemy.IsDefeated;
                Transform actor = enemyActors != null && index < enemyActors.Count ? enemyActors[index] : enemyActor;
                Vector3 feet = statusArena != null
                    ? roster != null && statusArena.EnemyCount > index ? statusArena.GetEnemyGroundPosition(index) : statusArena.EnemyGroundPosition
                    : GroundAnchor(actor);
                PlaceStatusStrip(enemyStatuses[index], defeated, camera, feet);
            }
        }

        private static IReadOnlyList<LegacyStatusSnapshot> BossStatuses(IReadOnlyList<LegacyStatusSnapshot> source,
            List<LegacyStatusSnapshot> destination, LegacyStatusKind kind, int count)
        {
            destination.Clear();
            if (source != null) destination.AddRange(source);
            if (count > 0) destination.Add(new LegacyStatusSnapshot(kind, 0, count));
            return destination;
        }

        private static Vector3 GroundAnchor(Transform actor)
            => actor != null ? actor.TransformPoint(new Vector3(0f, MobStudentAnimationSet.GroundOffset, 0f)) : Vector3.zero;

        private void PlaceStatusStrip(LegacyStatusStrip strip, bool defeated, Camera camera, Vector3 feet)
        {
            if (strip.Count == 0 || defeated || camera == null)
            {
                strip.SetVisible(false);
                return;
            }
            Vector3 screen = camera.WorldToScreenPoint(feet);
            Camera hudCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, hudCamera, out Vector2 anchor))
            {
                strip.SetVisible(false);
                return;
            }
            Vector2 size = strip.Root.sizeDelta;
            Vector2 half = root.rect.size * .5f;
            if (half.x < 1f || half.y < 1f) half = new Vector2(960f, 540f);
            Vector2 position = anchor + Vector2.down * (StatusFootGap + size.y * .5f);
            position.x = Mathf.Clamp(position.x, -half.x + size.x * .5f + 12f, half.x - size.x * .5f - 12f);
            float floor = -half.y + 12f + size.y * .5f;
            position.y = Mathf.Max(position.y, floor);
            // Check all earlier strips together: moving away from one fighter must not cover an earlier neighbour.
            Vector2 desired = position;
            float best = float.PositiveInfinity;
            for (int option = 0; option < 1 + statusStripBounds.Count * 2; option++)
            {
                float x = option == 0 ? desired.x : option % 2 == 1
                    ? statusStripBounds[(option - 1) / 2].xMin - StatusStripSeparation - size.x * .5f
                    : statusStripBounds[(option - 1) / 2].xMax + StatusStripSeparation + size.x * .5f;
                if (x - size.x * .5f < -half.x + 12f || x + size.x * .5f > half.x - 12f) continue;
                Vector2 candidatePosition = new Vector2(x, desired.y);
                Rect candidate = new Rect(candidatePosition - size * .5f, size);
                bool clear = true;
                foreach (Rect previous in statusStripBounds)
                    if (Expanded(previous, StatusStripSeparation).Overlaps(candidate)) { clear = false; break; }
                float distance = (candidatePosition - desired).sqrMagnitude;
                if (!clear || distance >= best) continue;
                best = distance;
                position = candidatePosition;
            }
            if (float.IsPositiveInfinity(best))
            {
                // A very narrow viewport may have no side aisle. Clear the dock for this frame; the requested
                // camera floor provides room under the actual feet on the next arena update.
                float uiTop = Mathf.Max(queueRowTop, CombatReadoutTop) - half.y;
                position.y = Mathf.Max(position.y, uiTop + 8f + size.y * .5f);
            }
            strip.Root.anchoredPosition = position;
            strip.SetVisible(true);
            statusStripBounds.Add(new Rect(position - size * .5f, size));
        }

        private void AddStatusPanelBlocker(RectTransform panel, float rise)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            panel.GetWorldCorners(statusPanelCorners);
            Vector2 minimum = root.InverseTransformPoint(statusPanelCorners[0]);
            Vector2 maximum = root.InverseTransformPoint(statusPanelCorners[2]);
            statusStripBounds.Add(UnityEngine.Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y + rise));
        }

        private void ClearFighterStatuses()
        {
            if (playerStatuses != null) playerStatuses.Refresh(null);
            foreach (LegacyStatusStrip strip in enemyStatuses) strip.Refresh(null);
            statusStripBounds.Clear();
        }
    }
}
