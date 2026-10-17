using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        private static readonly Vector2 EnemyRosterPanelSize = new Vector2(176f, 66f);
        private const float EnemyRosterPanelGap = 8f;
        private readonly List<EnemyRosterStatus> enemyRosterStatuses = new List<EnemyRosterStatus>();
        private readonly List<Rect> enemyRosterBlockers = new List<Rect>(8);
        private IReadOnlyList<Transform> enemyActors;
        private int highlightedEnemy = -1;

        /// <summary>Stable actor roots in the same order as the duel's enemy roster. This never owns their state.</summary>
        public void SetEnemyActors(IReadOnlyList<Transform> actors) => enemyActors = actors;

        public RectTransform GetEnemyStatusRoot(int index)
            => !disposed && index >= 0 && index < enemyRosterStatuses.Count &&
                enemyRosterStatuses[index].Root.gameObject.activeInHierarchy ? enemyRosterStatuses[index].Root : null;

        public RectTransform GetEnemyQueueOwnerMark(int slotIndex) => disposed ? null : enemyQueue.GetOwnerMark(slotIndex);
        public int HighlightedEnemyIndex => disposed ? -1 : highlightedEnemy;

        // Plain numerals remain legible in the game's bitmap font. The colour supplements, rather than replaces, identity.
        private static string EnemyIdentityNumber(int index)
            => index == 0 ? "1" : index == 1 ? "2" : index == 2 ? "3" : (index + 1).ToString();

        private static Color EnemyIdentityColor(int index)
        {
            switch (index % 3)
            {
                case 0: return new Color32(222, 169, 104, 255);
                case 1: return new Color32(136, 198, 197, 255);
                default: return new Color32(199, 171, 216, 255);
            }
        }

        private bool RefreshEnemyRoster(ILegacyDuelView session, Transform playerActor, Transform fallback, Camera camera, float delta)
        {
            var roster = session as ILegacyEnemyRosterView;
            bool multiple = roster != null && roster.Enemies.Count > 1;
            if (!multiple)
            {
                highlightedEnemy = -1;
                foreach (EnemyRosterStatus status in enemyRosterStatuses) status.Root.gameObject.SetActive(false);
                return false;
            }
            highlightedEnemy = EnemyRosterHighlight(session, roster);
            while (enemyRosterStatuses.Count < roster.Enemies.Count)
                enemyRosterStatuses.Add(BuildEnemyRosterStatus(enemyRosterStatuses.Count));
            for (int index = 0; index < enemyRosterStatuses.Count; index++)
            {
                EnemyRosterStatus status = enemyRosterStatuses[index];
                bool shown = index < roster.Enemies.Count && roster.Enemies[index].Health > 0;
                status.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                LegacyFighterState state = roster.Enemies[index];
                status.Refresh(state, delta);
                bool active = index == highlightedEnemy;
                Color identity = EnemyIdentityColor(index);
                status.Frame.color = active ? Accent : new Color(identity.r, identity.g, identity.b, .6f);
                status.ActiveLine.gameObject.SetActive(active);
                status.State.text = state.IsResistanceBroken ? "붕괴" : active ? "교전" : string.Empty;
                status.State.color = state.IsResistanceBroken ? DuelVisualTheme.Danger : Accent;
                string name = roster.EnemyName(index);
                if (status.ShownName != name)
                {
                    status.ShownName = name;
                    status.Name.text = name;
                }
                Transform actor = enemyActors != null && index < enemyActors.Count ? enemyActors[index] : fallback;
                if (actor == null || camera == null) continue;
                Vector3 screen = camera.WorldToScreenPoint(actor.TransformPoint(new Vector3(-.4f, 1.45f, 0f)));
                Camera hudCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, hudCamera, out Vector2 position)) continue;
                Vector2 half = root.rect.size * .5f;
                position = ClampCenter(position + half + Vector2.up * (EnemyRosterPanelSize.y * .5f + 12f),
                    EnemyRosterPanelSize) - half;
                status.Root.anchoredPosition = position;
                status.Stack = RosterRect(position);
            }
            // A later panel must clear every earlier one at once. Sequentially pushing it away from individual panels
            // could move it back into a panel already visited, especially when the viewport clamps one side.
            enemyRosterBlockers.Clear();
            if (playerHeadStack.width > 0f) enemyRosterBlockers.Add(playerHeadStack);
            AddRosterHeadBlocker(playerActor, camera, -.6f);
            for (int index = 0; index < roster.Enemies.Count; index++)
            {
                if (roster.Enemies[index].Health <= 0) continue;
                Transform actor = enemyActors != null && index < enemyActors.Count ? enemyActors[index] : fallback;
                AddRosterHeadBlocker(actor, camera, -.4f);
            }
            for (int index = 0; index < enemyRosterStatuses.Count; index++)
            {
                EnemyRosterStatus status = enemyRosterStatuses[index];
                if (!status.Root.gameObject.activeSelf) continue;
                PlaceRosterPanel(status);
                enemyRosterBlockers.Add(status.Stack);
                if (index == highlightedEnemy)
                {
                    enemyHeadStack = status.Stack;
                    enemyHeadX = status.Root.anchoredPosition.x;
                }
            }
            return true;
        }

        private void AddRosterHeadBlocker(Transform actor, Camera camera, float headX)
        {
            if (actor == null || camera == null) return;
            Vector3 screen = camera.WorldToScreenPoint(actor.TransformPoint(new Vector3(headX, 1.45f, 0f)));
            Camera hudCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, hudCamera, out Vector2 top))
                enemyRosterBlockers.Add(new Rect(top + new Vector2(-58f, -110f), new Vector2(116f, 110f)));
        }

        private int EnemyRosterHighlight(ILegacyDuelView session, ILegacyEnemyRosterView roster)
        {
            if (session.Phase == LegacyDuelPhase.Planning)
            {
                if (inspectedSlot >= 0) return roster.EnemyIndexForSlot(inspectedSlot);
                // The first forthcoming attacker is the initial target; Tab instead points at the inspected pairing.
                for (int slot = 0; slot < session.EnemyQueue.Count; slot++)
                    if (!roster.IsEnemySlotCanceled(slot)) return roster.EnemyIndexForSlot(slot);
            }
            return roster.ActiveEnemyIndex;
        }

        private void PlaceRosterPanel(EnemyRosterStatus status)
        {
            Vector2 desired = status.Root.anchoredPosition;
            if (RosterPositionClear(desired)) return;
            Vector2 half = root.rect.size * .5f;
            float halfWidth = EnemyRosterPanelSize.x * .5f, halfHeight = EnemyRosterPanelSize.y * .5f;
            float placementGap = EnemyRosterPanelGap + .5f;
            float bestScore = float.PositiveInfinity;
            Vector2 best = desired;
            // The nearest free rectangle starts at its desired coordinate, a blocker edge, or a viewport edge. With
            // three enemies and their protected heads this bounded search allocates nothing during Refresh.
            for (int xIndex = 0; xIndex < 3 + enemyRosterBlockers.Count * 2; xIndex++)
            {
                float x = xIndex == 0 ? desired.x : xIndex == 1 ? -half.x + ScreenMargin + halfWidth
                    : xIndex == 2 ? half.x - ScreenMargin - halfWidth
                    : (xIndex - 3) % 2 == 0
                        ? enemyRosterBlockers[(xIndex - 3) / 2].xMin - halfWidth - placementGap
                        : enemyRosterBlockers[(xIndex - 3) / 2].xMax + halfWidth + placementGap;
                for (int yIndex = 0; yIndex < 2 + enemyRosterBlockers.Count; yIndex++)
                {
                    float y = yIndex == 0 ? desired.y : yIndex == 1 ? half.y - ScreenMargin - halfHeight
                        : enemyRosterBlockers[yIndex - 2].yMax + halfHeight + placementGap;
                    // Head panels may move sideways or above their own head, never down across the fighter's body.
                    if (y < desired.y || x - halfWidth < -half.x + ScreenMargin ||
                        x + halfWidth > half.x - ScreenMargin || y + halfHeight > half.y - ScreenMargin) continue;
                    var candidate = new Vector2(x, y);
                    if (!RosterPositionClear(candidate)) continue;
                    float score = (candidate - desired).sqrMagnitude;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = candidate;
                }
            }
            status.Root.anchoredPosition = best;
            status.Stack = RosterRect(best);
        }

        private bool RosterPositionClear(Vector2 position)
        {
            Rect candidate = RosterRect(position);
            foreach (Rect blocker in enemyRosterBlockers)
                if (Expanded(blocker, EnemyRosterPanelGap).Overlaps(candidate)) return false;
            return true;
        }

        private static Rect RosterRect(Vector2 centre) => new Rect(centre - EnemyRosterPanelSize * .5f, EnemyRosterPanelSize);
        private static Rect Expanded(Rect value, float gap)
            => UnityEngine.Rect.MinMaxRect(value.xMin - gap, value.yMin - gap, value.xMax + gap, value.yMax + gap);

        private EnemyRosterStatus BuildEnemyRosterStatus(int index)
        {
            Image frame = Panel("Enemy Roster Status " + index, root, Vector2.zero, EnemyRosterPanelSize);
            frame.transform.SetSiblingIndex(enemyStatus.Root.GetSiblingIndex() + index + 1);
            var status = new EnemyRosterStatus(frame);
            Image number = Image("Enemy Number Plate", frame.transform, null, new Vector2(-72f, 18f),
                new Vector2(22f, 22f), EnemyIdentityColor(index));
            Text label = Text("Enemy Number", number.transform, Vector2.zero, Vector2.one * 22f, 16, TextAnchor.MiddleCenter);
            label.text = EnemyIdentityNumber(index);
            label.color = DuelVisualTheme.Ink;
            label.supportRichText = false;
            status.Name = Text("Battle Name", frame.transform, new Vector2(-3f, 18f),
                new Vector2(104f, 20f), 14, TextAnchor.MiddleLeft);
            status.Name.supportRichText = false;
            FitExplanationLabel(status.Name, 12);
            status.State = Text("Battle State", frame.transform, new Vector2(61f, 18f),
                new Vector2(36f, 20f), 12, TextAnchor.MiddleRight);
            Image("HP Track", frame.transform, null, new Vector2(0f, -3f), new Vector2(152f, 10f), Track);
            status.HealthLag = Image("HP delayed", frame.transform, Load("white"), new Vector2(0f, -3f),
                new Vector2(152f, 10f), DuelVisualTheme.Danger);
            status.Health = Image("HP", frame.transform, Load("white"), new Vector2(0f, -3f),
                new Vector2(152f, 10f), DuelVisualTheme.Health);
            Image("Resistance Track", frame.transform, null, new Vector2(0f, -20f), new Vector2(152f, 6f), Track);
            status.ResistanceLag = Image("Resistance delayed", frame.transform, Load("white"), new Vector2(0f, -20f),
                new Vector2(152f, 6f), DuelVisualTheme.Danger);
            status.Resistance = Image("Resistance", frame.transform, Load("white"), new Vector2(0f, -20f),
                new Vector2(152f, 6f), DuelVisualTheme.Steel);
            Filled(status.Health, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            Filled(status.HealthLag, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            Filled(status.Resistance, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            Filled(status.ResistanceLag, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            status.ActiveLine = Image("Active Enemy", frame.transform, null, new Vector2(0f, -31f),
                new Vector2(168f, 3f), Accent).rectTransform;
            return status;
        }

        private void ResetEnemyRoster()
        {
            highlightedEnemy = -1;
            enemyStatus.Root.gameObject.SetActive(true);
            foreach (EnemyRosterStatus status in enemyRosterStatuses)
            {
                status.Reset();
                status.Root.gameObject.SetActive(false);
            }
        }

        private sealed class EnemyRosterStatus
        {
            public readonly Image Frame;
            public readonly RectTransform Root;
            public Image Health, HealthLag, Resistance, ResistanceLag;
            public Text Name, State;
            public RectTransform ActiveLine;
            public Rect Stack;
            public string ShownName;
            private float delay;
            public EnemyRosterStatus(Image frame) { Frame = frame; Root = frame.rectTransform; }
            public void Refresh(LegacyFighterState state, float delta)
            {
                float health = state.Health / (float)state.MaxHealth;
                float resistance = state.MaxResistance > 0 ? state.Resistance / (float)state.MaxResistance : 0f;
                if (health < Health.fillAmount || resistance < Resistance.fillAmount) delay = .5f;
                Health.fillAmount = health;
                Resistance.fillAmount = resistance;
                if (delay <= 0f)
                {
                    HealthLag.fillAmount = Mathf.MoveTowards(HealthLag.fillAmount, health, delta);
                    ResistanceLag.fillAmount = Mathf.MoveTowards(ResistanceLag.fillAmount, resistance, delta);
                }
                else delay -= delta;
            }
            public void Reset()
            {
                delay = 0f;
                Health.fillAmount = HealthLag.fillAmount = Resistance.fillAmount = ResistanceLag.fillAmount = 1f;
            }
        }
    }
}
