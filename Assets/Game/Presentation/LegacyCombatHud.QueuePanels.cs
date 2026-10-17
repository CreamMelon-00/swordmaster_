using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The two facing queue panels over the command dock (2026-10-15, author's sketch; the queues rode the fighters'
    /// heads before). The left panel holds the player's queue (the viewer's, in versus: the perspective already puts the
    /// viewer on the left), the right one the opponent's, mirrored round the centre seam: slot 1 is the card nearest the seam
    /// on both sides, the player's row fills from its right end outward and the opponent's from its left end outward, so slot k
    /// stands at the same distance from the centre on both sides (<see cref="LegacyQueuePanelLayout"/>). A small brass seam mark
    /// between the two first slots marks where the pairs meet. The rows keep their names ("Player Requests", "Enemy Requests")
    /// and every card keeps its decorations, which are its children. While planning the panels sit just above the dock; as the
    /// dock folds away for a resolving turn they sink with it onto the lower letterbox band, and after each resolved slot both
    /// rows slide one step toward the seam on the battle's clock, so the pair being resolved is always the innermost. The
    /// slide is drawn only: it never holds the resolution. A card the controller keeps up over the dock's centre (the coach's)
    /// widens the seam so it covers no card (<see cref="SetQueueSeamReserve"/>), a held skill's explanation opens above the
    /// panels, and the arena keeps the fighters' feet over them (<see cref="QueuePanelFloor"/>,
    /// <see cref="LegacyArenaView.SetHudFloor"/>). <see cref="DuelPresentationSettings"/> holds the numbers. The pairing cues
    /// and the resolving pair's clash motion on these panels are <c>LegacyCombatHud.Clash.cs</c>'.</summary>
    public sealed partial class LegacyCombatHud
    {
        public const string QueuePanelsName = "Queue Panels", PlayerQueuePanelName = "Player Queue Panel",
            EnemyQueuePanelName = "Enemy Queue Panel", QueueSeamName = "Queue Seam";
        // How far the tabs over a card (취소/반환 and keyword marks) rise past its top edge, at the built card size: a held
        // explanation opens above them.
        private const float QueueMarkRise = 30f;
        private const float QueueRangeHeight = 18f;
        // From the theme itself: static fields of another part of this class may not be set yet when these are.
        private static readonly Color QueueTraySurface = new Color(DuelVisualTheme.Surface.r, DuelVisualTheme.Surface.g,
            DuelVisualTheme.Surface.b, .82f);
        private static readonly Color QueueTrayBorder = new Color(DuelVisualTheme.Border.r, DuelVisualTheme.Border.g,
            DuelVisualTheme.Border.b, .9f);
        private RectTransform queuePanels, playerQueuePanel, enemyQueuePanel, queueSeam;
        private Text playerQueueRange, enemyQueueRange;
        private float queueCardScale = 1f;
        private int queueWindowStart, queueWindowCapacity;
        // The slide toward the seam: where it began (slots out), its battle seconds so far, and the slots consumed it follows.
        private float queueSlideFrom, queueSlideElapsed;
        private int queueConsumed;
        private float queueSeamReserve;
        // As last laid out: the panels' top and the room their cards' tabs take over it (HUD units from the screen's bottom),
        // and the share of the screen's height the arena keeps the fighters' feet above.
        private float queuePanelTop, queueRowTop, queuePanelFloor;

        /// <summary>The left panel's tray (the player's queue, the viewer's in versus), under its cards.</summary>
        public RectTransform PlayerQueuePanel => disposed ? null : playerQueuePanel;

        /// <summary>The right panel's tray (the opponent's queue).</summary>
        public RectTransform EnemyQueuePanel => disposed ? null : enemyQueuePanel;

        /// <summary>The seam mark between the two first slots, at the centre.</summary>
        public RectTransform QueueSeam => disposed ? null : queueSeam;

        /// <summary>A queue card's side as drawn (HUD units, <see cref="DuelPresentationSettings.QueueCardSize"/>).</summary>
        public float QueueCardWidth => QueueCardSize * queueCardScale;

        /// <summary>The spacing both rows use now (the tuned one unless the longer row had to tighten).</summary>
        public float QueueSpacing => disposed ? 0f : playerQueue.CurrentPitch;

        /// <summary>The first absolute slot shown in both rows, and how many readable slots fit either panel.</summary>
        public int QueueWindowStart => disposed ? 0 : queueWindowStart;
        public int QueueWindowCapacity => disposed ? 0 : queueWindowCapacity;

        /// <summary>The slots both rows have still to slide toward the seam (0 at rest).</summary>
        public float QueueSlide => disposed ? 0f
            : LegacyQueuePanelLayout.Slide(queueSlideFrom, queueSlideElapsed, QueueSlideSeconds);

        /// <summary>The panels' top as last laid out, HUD units from the screen's bottom (their cards' tabs may rise past it).</summary>
        public float QueuePanelTop => disposed ? 0f : queuePanelTop;

        /// <summary>The top of the room the panels' cards take with the tabs over them (취소/반환, keyword marks), HUD units from
        /// the screen's bottom: a held skill's explanation opens <see cref="ExplanationGap"/> above it (or above the coach's card).</summary>
        public float QueueRowTop => disposed ? 0f : queueRowTop;

        /// <summary>The share of the screen's height, from its bottom, that the arena keeps the fighters' feet above: the
        /// panels' top and the tuned clearance (<see cref="LegacyArenaView.SetHudFloor"/>). 0 when the settings switch it off.</summary>
        public float QueuePanelFloor => disposed ? 0f : queuePanelFloor;

        /// <summary>Half the width of a card the controller keeps up over the dock's centre (the coach's), HUD units; 0 for
        /// none. The seam opens round it, so it covers no queue card; both panels keep their mirror. Asked for every frame.</summary>
        public void SetQueueSeamReserve(float halfWidth)
            => queueSeamReserve = halfWidth > 0f && !float.IsInfinity(halfWidth) ? halfWidth : 0f;

        private float QueuePanelWidth => presentationSettings != null ? presentationSettings.QueuePanelWidth
            : LegacyQueuePanelLayout.DefaultPanelWidth;
        private float QueueCardSetting => presentationSettings != null ? presentationSettings.QueueCardSize
            : LegacyQueuePanelLayout.DefaultCardSize;
        private float QueueCardSpacing => presentationSettings != null ? presentationSettings.QueueCardSpacing
            : LegacyQueuePanelLayout.DefaultCardSpacing;
        private float QueueSeamGap => presentationSettings != null ? presentationSettings.QueueSeamGap
            : LegacyQueuePanelLayout.DefaultSeamGap;
        private float QueueSlideSeconds => presentationSettings != null ? presentationSettings.QueueSlideSeconds
            : LegacyQueuePanelLayout.DefaultSlideSeconds;
        private float QueuePanelDockGap => presentationSettings != null ? presentationSettings.QueuePanelDockGap
            : LegacyQueuePanelLayout.DefaultDockGap;
        private float QueuePanelResolvingBottom => presentationSettings != null ? presentationSettings.QueuePanelResolvingBottom
            : LegacyQueuePanelLayout.DefaultResolvingBottom;
        private bool QueuePanelKeepsFightersClear => presentationSettings == null || presentationSettings.QueuePanelKeepsFightersClear;
        private float QueuePanelFighterClearance => presentationSettings != null ? presentationSettings.QueuePanelFighterClearance
            : LegacyQueuePanelLayout.DefaultFighterClearance;

        // The trays and the seam mark, under the card rows (built just before them).
        private void BuildQueuePanels(Sprite white)
        {
            queuePanels = Rect(QueuePanelsName, root, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            float height = QueueCardSize + LegacyQueuePanelLayout.Padding * 2f;
            playerQueuePanel = Panel(PlayerQueuePanelName, queuePanels, Vector2.zero,
                new Vector2(LegacyQueuePanelLayout.DefaultPanelWidth, height), QueueTraySurface, QueueTrayBorder).rectTransform;
            enemyQueuePanel = Panel(EnemyQueuePanelName, queuePanels, Vector2.zero,
                new Vector2(LegacyQueuePanelLayout.DefaultPanelWidth, height), QueueTraySurface, QueueTrayBorder).rectTransform;
            playerQueueRange = Text("Player Queue Range", playerQueuePanel, Vector2.zero,
                new Vector2(280f, QueueRangeHeight), 16, TextAnchor.MiddleCenter);
            enemyQueueRange = Text("Enemy Queue Range", enemyQueuePanel, Vector2.zero,
                new Vector2(280f, QueueRangeHeight), 16, TextAnchor.MiddleCenter);
            playerQueueRange.color = enemyQueueRange.color = DuelVisualTheme.Muted;
            queueSeam = Rect(QueueSeamName, queuePanels, Vector2.zero, new Vector2(12f, QueueCardSize), Vector2.one * .5f);
            Color line = DuelVisualTheme.Accent;
            line.a = .5f;
            Image("Seam Line", queueSeam, white, Vector2.zero, new Vector2(2f, QueueCardSize + 12f), line);
            Image("Seam Mark", queueSeam, white, Vector2.zero, Vector2.one * 9f, DuelVisualTheme.Accent).rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, 45f);
        }

        // The cards' scale at rest, from the tuned card size (decorations scale with their card).
        private void ApplyQueueCardSize()
        {
            // Use the full turn's queues so resolution never grows or spreads its remaining cards after a consumed slot.
            int count = displayedSession == null ? 0
                : Mathf.Max(displayedSession.PlayerQueue.Count, displayedSession.EnemyQueue.Count);
            float card = LegacyQueuePanelLayout.CardSize(QueueCardRoom(), count, QueueCardSetting, QueueCardSpacing);
            queueCardScale = card / QueueCardSize;
            playerQueue.CardScale = enemyQueue.CardScale = queueCardScale;
        }

        private float QueueCardRoom()
        {
            float width = root.rect.width > 1f ? root.rect.width : 1920f;
            float seam = Mathf.Max(QueueSeamGap * .5f, queueSeamReserve);
            float outer = Mathf.Min(seam + QueuePanelWidth, width * .5f - ScreenMargin);
            return Mathf.Max(1f, outer - seam - LegacyQueuePanelLayout.Padding * 2f);
        }

        /// <summary>Lays both panels and their rows out for this frame (after both rows took their cards): over the dock, or
        /// sinking with it as it folds away, the seam opened round the coach's card, one spacing for both rows, and the
        /// slide toward the seam begun when a slot was consumed and moved on by <paramref name="delta"/> (the battle's
        /// clock, so it follows bullet time and the hit stop, and stops with the battle).</summary>
        private void LayoutQueuePanels(bool resolving, int activeSlot, float delta)
        {
            Vector2 size = root.rect.size;
            if (size.x < 1f || size.y < 1f) size = new Vector2(1920f, 1080f);
            Vector2 half = size * .5f;
            float pad = LegacyQueuePanelLayout.Padding, card = QueueCardWidth;
            // The dock's own fold (Refresh's transition) carries the panels down to the letterbox band and back up.
            float fold = OutQuad(Mathf.Clamp01(transition));
            float trayBottom = Mathf.Lerp(DockBottomInset + DockHeight + QueuePanelDockGap, QueuePanelResolvingBottom, fold);
            float seam = Mathf.Max(QueueSeamGap * .5f, queueSeamReserve);
            float room = QueueCardRoom(), trayWidth = room + pad * 2f;
            // Card size and spacing stay fixed for this turn. A shared window then replaces the unreadable overlap of
            // very long queues; the same logical slot remains opposite itself on both sides.
            int count = Mathf.Max(playerQueue.QueueLength, enemyQueue.QueueLength);
            float spacing = LegacyQueuePanelLayout.ReadableSpacing(card, QueueCardSetting, QueueCardSpacing);
            queueWindowCapacity = LegacyQueuePanelLayout.Capacity(room, card, spacing);
            float viewportRoom = half.x - ScreenMargin - seam - pad;
            // Reserve the small range line for the whole turn if even the longest slide could need it. Changing the
            // visible outer count mid-slide must never move the active pair up and down.
            int smallestSlideCapacity = Mathf.Min(queueWindowCapacity, LegacyQueuePanelLayout.Capacity(
                viewportRoom - LegacyQueuePanelLayout.MaximumSlide * spacing, card, spacing));
            float rangeHeight = count > smallestSlideCapacity ? QueueRangeHeight : 0f;

            // A consumed slot hides its card at once (as before); the rest start where they stood and slide in.
            int consumed = resolving ? Mathf.Max(0, activeSlot) : 0;
            float seconds = QueueSlideSeconds;
            if (consumed != queueConsumed)
            {
                queueSlideFrom = LegacyQueuePanelLayout.SlideStart(
                    LegacyQueuePanelLayout.Slide(queueSlideFrom, queueSlideElapsed, seconds), queueConsumed, consumed);
                queueSlideElapsed = 0f;
                queueConsumed = consumed;
            }
            else if (queueSlideFrom > 0f)
            {
                queueSlideElapsed += Mathf.Max(0f, delta);
                if (queueSlideElapsed >= seconds) queueSlideFrom = queueSlideElapsed = 0f;
            }
            float slide = LegacyQueuePanelLayout.Slide(queueSlideFrom, queueSlideElapsed, seconds);
            // Coach cards can leave very narrow panels at a screen edge. Reveal an outer card only once its whole
            // sliding footprint fits the viewport; both rows lose/reveal that same slot, while the current pair stays.
            queueWindowCapacity = Mathf.Min(queueWindowCapacity,
                LegacyQueuePanelLayout.Capacity(viewportRoom - slide * spacing, card, spacing));
            int end = Mathf.Max(playerQueue.QueueEndSlot, enemyQueue.QueueEndSlot);
            int focus = resolving ? consumed : inspectedSlot >= 0 ? inspectedSlot : playerQueue.QueueLength;
            queueWindowStart = LegacyQueuePanelLayout.WindowStart(end, consumed, queueWindowCapacity, focus);
            float trayHeight = card + pad * 2f + rangeHeight;
            float cardLine = trayBottom + rangeHeight + pad + card * .5f;

            float inner = seam + pad + card * .5f, lineY = cardLine - half.y;
            playerQueue.Root.anchoredPosition = enemyQueue.Root.anchoredPosition = new Vector2(0f, lineY);
            // The resolving pair's clash motion (LegacyCombatHud.Clash.cs) poses its cards for the rows' layout.
            PoseQueueClash(inner, spacing, slide, lineY, consumed, delta);
            playerQueue.Layout(spacing, inner, slide, card, queueWindowStart, queueWindowCapacity);
            enemyQueue.Layout(spacing, inner, slide, card, queueWindowStart, queueWindowCapacity);
            float trayY = trayBottom + trayHeight * .5f - half.y;
            playerQueuePanel.anchoredPosition = new Vector2(-(seam + trayWidth * .5f), trayY);
            enemyQueuePanel.anchoredPosition = new Vector2(seam + trayWidth * .5f, trayY);
            playerQueuePanel.sizeDelta = enemyQueuePanel.sizeDelta = new Vector2(trayWidth, trayHeight);
            UpdateQueueRange(playerQueueRange, playerQueue.QueueEndSlot, consumed, rangeHeight, trayWidth, trayHeight);
            UpdateQueueRange(enemyQueueRange, enemyQueue.QueueEndSlot, consumed, rangeHeight, trayWidth, trayHeight);
            queueSeam.anchoredPosition = new Vector2(0f, lineY);
            queueSeam.localScale = Vector3.one * queueCardScale;
            queuePanelTop = trayBottom + trayHeight;
            queueRowTop = cardLine + card * .5f + QueueMarkRise * queueCardScale;
            queuePanelFloor = QueuePanelKeepsFightersClear
                ? Mathf.Clamp01((queuePanelTop + QueuePanelFighterClearance) / size.y) : 0f;
        }

        private void UpdateQueueRange(Text label, int end, int consumed, float height, float width, float trayHeight)
        {
            bool shown = height > 0f && end > consumed &&
                (queueWindowStart > consumed || end - queueWindowStart > queueWindowCapacity);
            label.gameObject.SetActive(shown);
            if (!shown) return;
            int first = Mathf.Max(consumed, queueWindowStart), last = Mathf.Min(end, queueWindowStart + queueWindowCapacity);
            string text = first >= end ? "이전 " + (end - consumed)
                : (first + 1) + "–" + last + " / " + end;
            if (label.text != text) label.text = text;
            label.rectTransform.anchoredPosition = new Vector2(0f, -trayHeight * .5f + height * .5f);
            label.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, width - 12f), height);
        }

        // Reset: no slide, no seam reserve (the controller asks again every frame).
        private void ResetQueuePanels()
        {
            queueSlideFrom = queueSlideElapsed = 0f;
            queueConsumed = 0;
            queueSeamReserve = 0f;
            queueWindowStart = queueWindowCapacity = 0;
            if (playerQueueRange != null) playerQueueRange.gameObject.SetActive(false);
            if (enemyQueueRange != null) enemyQueueRange.gameObject.SetActive(false);
        }
    }
}
