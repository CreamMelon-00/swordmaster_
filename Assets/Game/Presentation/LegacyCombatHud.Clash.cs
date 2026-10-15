using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The queue panels' pairing cues and clash motion (2026-10-15, the author's "one skill is one Limbus coin": the
    /// pairs must read without a bracket board or new numbers). While planning, the opponent card the player's next
    /// reservation would meet (<see cref="LegacyClashMotion.NextOpponentSlot(ILegacyDuelView)"/>) wears a pulsing brass ring
    /// and a faint socket marks its mirror on the player's panel, where that reservation will land; both hide while the player
    /// cannot reserve (inputs held: a versus opponent's turn; Tab; nothing left to reserve) or the opponent has no card there.
    /// Reserving into a slot the opponent also fills (<see cref="PlayReservationSpark"/>) bursts a short spark at both cards at
    /// once, in the slot forecast's look: bright, warm and larger for 유리, dim, cold and smaller for 불리. While a slot
    /// resolves, its two cards (the innermost pair, at the centre seam) move by the slot's classification
    /// (<see cref="LegacyClashMotion.Classify"/>, read from <see cref="ILegacyDuelView.CurrentClash"/>, so a versus guest's
    /// view is mirrored): attacking cards lean in as the slot starts, and from its first hit the two meet with one spark at
    /// the contact point, the stronger skill drives on and the weaker is knocked back by the power gap, an attack thrusts
    /// into a guard that braces (knocked back by the pierce, or the attack rebounds), a lone attack lunges, a dodger steps up
    /// as the attack whiffs past, a side that breaks shakes (<see cref="LegacyClashPose"/>). All of it is drawn on the
    /// battle's clock and never holds anything: a slot completed mid-meeting hides its cards at once as before once the next
    /// slot is under way, and a copy of each (<see cref="ClashCardName"/>, under the rows) plays the rest of the meeting and
    /// fades, while the turn's last slot (a finishing blow too), whose cards stay shown until the next turn, plays it out on
    /// its own cards, never on a copy as well; the row lays every card out
    /// afresh each frame, so whatever stops the motion (the turn's end, <see cref="Reset"/>, a rebuilt HUD) leaves the cards
    /// at rest. <see cref="DuelPresentationSettings"/> holds the numbers.</summary>
    public sealed partial class LegacyCombatHud
    {
        public const string QueueCuesName = "Queue Cues", QueueSparksName = "Queue Sparks",
            NextOpponentRingName = "Next Opponent Ring", NextReservationSocketName = "Next Reservation Socket",
            ClashCardName = "Clash Card", ClashSparkName = "Clash Spark", ReservationSparkName = "Reservation Spark";
        /// <summary>The tunables' defaults (<see cref="DuelPresentationSettings"/>): the ring's strength, pulse (real seconds),
        /// pulse depth and margin round the card (HUD units at the built card size), the socket's strength, the reservation
        /// spark's real seconds, size (HUD units), sparks per card and the 유리/불리 scales, and the contact spark's battle
        /// seconds, size and sparks.</summary>
        public const float DefaultNextOpponentRingAlpha = .8f, DefaultNextOpponentRingPulseSeconds = 1.1f,
            DefaultNextOpponentRingPulseDepth = .4f, DefaultNextOpponentRingMargin = 5f,
            DefaultNextReservationSocketAlpha = .35f, DefaultReservationSparkSeconds = .32f, DefaultReservationSparkSize = 24f,
            DefaultReservationSparkFavourableScale = 1.3f, DefaultReservationSparkUnfavourableScale = .7f,
            DefaultClashSparkSeconds = .34f, DefaultClashSparkSize = 30f;
        public const int DefaultReservationSparkCount = 8, DefaultClashSparkCount = 10;
        // The ring's lines and the socket's are this thick (HUD units at the built card size).
        private const float RingLine = 2f, SocketLine = 2f;
        private const float SparkWidthShare = .07f, MinimumSparkWidth = 1.5f;
        // Contact sparks and reservation-spark pairs that can play at once, and slots whose cards may still be leaving.
        private const int ClashSparkCapacity = 2, ReservationSparkPairs = 2, LeavingClashCapacity = 3;
        private RectTransform queueCues, queueSparks, nextOpponentRing, nextReservationSocket;
        private CanvasGroup nextOpponentRingGroup, nextReservationSocketGroup;
        // The ring's four edges and the socket's fill, inked from the settings while they show.
        private readonly List<Image> nextOpponentRingEdges = new List<Image>(4);
        private Image nextReservationSocketFill;
        private float ringPhase;
        private int ringSlot = -1, pendingReservationSpark = -1, reservationSparkSlot = -1;
        private LegacyForecastVerdict? reservationSparkVerdict;
        private ClashPlay liveClash;
        private readonly List<ClashPlay> leavingClashes = new List<ClashPlay>();
        private readonly SparkBurst[] clashSparks = new SparkBurst[ClashSparkCapacity];
        private readonly SparkBurst[] reservationSparks = new SparkBurst[ReservationSparkPairs * 2];
        private int nextClashSpark, nextReservationPair;
        private SparkBurst lastClashSpark;
        // The director's emphasis for a slot whose pair is not on the seam yet (SetClashEmphasis): applied when it comes.
        private int pendingEmphasisSlot = -1;
        private float pendingEmphasis = 1f;
        /// <summary>How far the two cards of a blade bind sway together along the row as they hold at the seam (HUD units,
        /// at the built card size; <see cref="LegacyBladeBind.Sway"/>).</summary>
        public const float ClashBindSway = 6f;

        /// <summary>The layer under the queue cards (the ring, the socket and the cards leaving the seam), and the one over them
        /// (the sparks); both stand where the rows do, at the centre seam on the cards' line.</summary>
        public RectTransform QueueCues => disposed ? null : queueCues;
        public RectTransform QueueSparks => disposed ? null : queueSparks;

        /// <summary>The ring round the opponent card the next reservation would meet, and the socket at its mirror on the
        /// player's panel (inactive while they are hidden).</summary>
        public RectTransform NextOpponentRing => disposed ? null : nextOpponentRing;
        public RectTransform NextReservationSocket => disposed ? null : nextReservationSocket;

        /// <summary>The opponent slot the ring marks now, or -1 while it is hidden.</summary>
        public int NextOpponentRingSlot => disposed ? -1 : ringSlot;

        /// <summary>Whether a reservation spark is bursting now.</summary>
        public bool IsReservationSparkPlaying
        {
            get
            {
                if (disposed) return false;
                foreach (SparkBurst burst in reservationSparks)
                    if (burst != null && burst.IsPlaying) return true;
                return false;
            }
        }

        /// <summary>The slot of the last reservation spark that played this turn (-1 for none), and the forecast verdict it
        /// showed (null: the slot had none, and it played the 비슷 look).</summary>
        public int ReservationSparkSlot => disposed ? -1 : reservationSparkSlot;
        public LegacyForecastVerdict? ReservationSparkVerdict => disposed ? null : reservationSparkVerdict;

        /// <summary>The latest reservation spark's burst at one side's card while it plays, else null.</summary>
        public DuelMeshSparks GetReservationSpark(bool playerSide)
        {
            if (disposed) return null;
            int pair = (nextReservationPair + ReservationSparkPairs - 1) % ReservationSparkPairs;
            SparkBurst burst = reservationSparks[pair * 2 + (playerSide ? 0 : 1)];
            return burst != null && burst.IsPlaying ? burst.Sparks : null;
        }

        /// <summary>The resolving slot's classification as the motion plays it (null while no slot resolves), and its slot.</summary>
        public LegacyClash? ShownClash => disposed || liveClash == null ? (LegacyClash?)null : liveClash.Clash;
        public int ClashSlot => disposed || liveClash == null ? -1 : liveClash.Slot;

        /// <summary>Whether the resolving pair has met (its first hit resolved, or a blade bind holds it before resolution).</summary>
        public bool IsClashMeeting => !disposed && liveClash != null && liveClash.Impact;

        /// <summary>The director's emphasis on the resolving pair (<see cref="SetClashEmphasis"/>; 1 while none).</summary>
        public float ClashEmphasis => disposed || liveClash == null ? 1f : liveClash.Emphasis;

        /// <summary>Whether the resolving pair is held at the seam in a blade bind now (<see cref="BindClash"/>).</summary>
        public bool IsClashBinding => !disposed && liveClash != null && liveClash.Binding;

        /// <summary>Whether the resolving pair plays its meeting as a blade bind: held at the seam, then both bounce back
        /// alike (no push and recoil: both dealt damage).</summary>
        public bool IsClashBound => !disposed && liveClash != null && liveClash.Bound;

        /// <summary>The presentation director's weight on slot <paramref name="slot"/>'s pair (a Major slot's
        /// <see cref="DuelPresentationSettings.MajorClashScale"/>; 1 for none): the lean, the drive, the knock-back, the
        /// side-step and the tilt go that much farther, and the contact spark is that much larger. The meeting's time is
        /// unchanged. A pair not on the seam yet takes it when it comes. Once the pair's meeting has begun to play (any time
        /// after the frame of its first hit) its weight stays as it was: a weight a later hit brings (a break on the second
        /// hit) would otherwise snap the cards mid push, so it changes nothing for this pair.</summary>
        public void SetClashEmphasis(int slot, float scale)
        {
            if (disposed || slot < 0) return;
            scale = float.IsNaN(scale) || float.IsInfinity(scale) ? 1f : Mathf.Clamp(scale, 1f, 2f);
            if (liveClash != null && liveClash.Slot == slot)
            {
                // The first hit's own weight still lands: the meeting starts from where the cards stand (its clock at 0).
                if (!liveClash.Impact || liveClash.ImpactElapsed <= 0f) liveClash.Emphasis = scale;
                pendingEmphasisSlot = -1;
                return;
            }
            pendingEmphasisSlot = slot;
            pendingEmphasis = scale;
        }

        /// <summary>A blade bind on slot <paramref name="slot"/>'s pair, which has just met (before or after its first hit): for
        /// <paramref name="seconds"/> of the real time the controller hands over (<see cref="AdvanceClashBind"/>, while the
        /// battle's clock stands still) both cards go to the seam and hold there, swaying together one way and then the other
        /// (<see cref="ClashBindSway"/>), sparking <paramref name="sparks"/> more times; then the meeting goes on as an even
        /// 합 (both bounce back alike). Nothing while the motion is off or the pair is another slot's.</summary>
        public void BindClash(int slot, float seconds, int sparks)
        {
            if (disposed || !ClashMotionEnabled || liveClash == null || liveClash.Slot != slot ||
                !(seconds > 0f) || float.IsInfinity(seconds)) return;
            // Contact is a presentation event: the real hit may wait until the blades release.
            liveClash.MeetBeforeHit(ClashTuning);
            liveClash.Bound = true;
            liveClash.BindSeconds = seconds;
            liveClash.BindElapsed = 0f;
            liveClash.BindSparks = Mathf.Max(0, sparks);
        }

        /// <summary>Plays <paramref name="realSeconds"/> more of the pair's blade bind (the same real seconds the arena's bind
        /// gets). Nothing while none holds.</summary>
        public void AdvanceClashBind(float realSeconds)
        {
            if (disposed || liveClash == null || !liveClash.Binding) return;
            liveClash.AdvanceBind(realSeconds, ClashTuning);
            // The contact clock is held, so its sparks must use the bind's real seconds to leave the seam and fade.
            // Otherwise each new burst stays at phase zero (invisible) until the delayed hit releases the battle clock.
            foreach (SparkBurst burst in clashSparks) AdvanceSpark(burst, realSeconds);
        }

        /// <summary>The pair's blade bind is over (the controller's bind time has run): the meeting goes on from the contact
        /// on the battle's clock. Nothing while none holds.</summary>
        public void FinishClashBind()
        {
            if (disposed || liveClash == null || !liveClash.Binding) return;
            liveClash.AdvanceBind(liveClash.BindSeconds, ClashTuning);
        }

        /// <summary>The pose one side's resolving card was last drawn with (rest while none is).</summary>
        public LegacyCardPose GetClashPose(bool playerSide)
            => disposed || liveClash == null ? LegacyCardPose.Rest : (playerSide ? liveClash.Player : liveClash.Enemy).LastPose;

        /// <summary>The copies of completed slots' cards still playing out their meeting (<see cref="ClashCardName"/>).</summary>
        public int LeavingClashCardCount
        {
            get
            {
                if (disposed) return 0;
                int count = 0;
                foreach (ClashPlay play in leavingClashes)
                    count += (play.Player.Ghost != null ? 1 : 0) + (play.Enemy.Ghost != null ? 1 : 0);
                return count;
            }
        }

        /// <summary>Whether a copy of slot <paramref name="slot"/>'s card (either side's) is playing out its meeting under the
        /// rows: only once the rows have hidden that slot's cards.</summary>
        public bool HasLeavingClashCopy(int slot)
        {
            if (disposed) return false;
            foreach (ClashPlay play in leavingClashes)
                if (play.Slot == slot && (play.Player.Ghost != null || play.Enemy.Ghost != null)) return true;
            return false;
        }

        /// <summary>Whether one side's card is shaking from a break this slot (resolving or leaving).</summary>
        public bool IsClashShaking(bool playerSide)
        {
            if (disposed) return false;
            if (liveClash != null && (playerSide ? liveClash.Player : liveClash.Enemy).Shaking) return true;
            foreach (ClashPlay play in leavingClashes)
                if ((playerSide ? play.Player : play.Enemy).Shaking) return true;
            return false;
        }

        /// <summary>The latest contact spark while it plays, else null.</summary>
        public DuelMeshSparks ClashSpark => disposed || lastClashSpark == null || !lastClashSpark.IsPlaying ? null : lastClashSpark.Sparks;

        /// <summary>Whether anything of the clash motion is still on screen: a resolving pair, cards leaving, a spark.</summary>
        public bool IsClashPlaying
        {
            get
            {
                if (disposed) return false;
                if (liveClash != null || leavingClashes.Count > 0) return true;
                foreach (SparkBurst burst in clashSparks)
                    if (burst != null && burst.IsPlaying) return true;
                return false;
            }
        }

        /// <summary>The player has just reserved into <paramref name="slot"/> (a lane's skill: its
        /// <see cref="LegacyQueuedDuel.LastQueuedSlotIndex"/>, before an automatic 숨고르기 it brought; a 숨고르기: the queue's
        /// last slot). If the opponent holds a card there, a short spark bursts at both cards on the next
        /// <see cref="Refresh"/> (after the forecast has read the new queue), in that slot's forecast look. Versus calls it
        /// for the viewer's own reservations only.</summary>
        public void PlayReservationSpark(int slot)
        {
            if (disposed) return;
            pendingReservationSpark = slot >= 0 ? slot : -1;
        }

        /// <summary>Reads the resolving slot (<see cref="ILegacyDuelView.CurrentClash"/>) into the clash motion now, without
        /// moving its clock. <see cref="Refresh"/> reads it every frame; a controller also calls it right after each hit, so
        /// a hit that completes its slot in the same frame (a finishing blow) still shows its meeting.</summary>
        public void ObserveClash(ILegacyDuelView session)
        {
            if (disposed || session == null || !ClashMotionEnabled) return;
            LegacyClashInput input = session.CurrentClash;
            if (liveClash != null && liveClash.Slot != input.SlotIndex)
            {
                LeaveClash(liveClash);
                liveClash = null;
            }
            if (!input.HasSlot) return;
            if (liveClash == null)
            {
                liveClash = new ClashPlay(input.SlotIndex);
                if (pendingEmphasisSlot == input.SlotIndex) liveClash.Emphasis = pendingEmphasis;
                pendingEmphasisSlot = -1;
            }
            liveClash.Observe(input);
        }

        private bool QueuePairCuesEnabled => presentationSettings == null || presentationSettings.QueuePairCuesEnabled;
        private float NextOpponentRingAlpha => presentationSettings != null ? presentationSettings.NextOpponentRingAlpha
            : DefaultNextOpponentRingAlpha;
        private float NextOpponentRingPulseSeconds => presentationSettings != null ? presentationSettings.NextOpponentRingPulseSeconds
            : DefaultNextOpponentRingPulseSeconds;
        private float NextReservationSocketAlpha => presentationSettings != null ? presentationSettings.NextReservationSocketAlpha
            : DefaultNextReservationSocketAlpha;
        private Color NextOpponentRingInk => presentationSettings != null ? presentationSettings.NextOpponentRingInk
            : DuelPresentationSettings.DefaultNextOpponentRingInk;
        private float NextOpponentRingMargin => presentationSettings != null ? presentationSettings.NextOpponentRingMargin
            : DefaultNextOpponentRingMargin;
        private float NextOpponentRingPulseDepth => presentationSettings != null ? presentationSettings.NextOpponentRingPulseDepth
            : DefaultNextOpponentRingPulseDepth;
        private Color NextReservationSocketInk => presentationSettings != null ? presentationSettings.NextReservationSocketInk
            : DuelPresentationSettings.DefaultNextReservationSocketInk;
        private bool ClashMotionEnabled => presentationSettings == null || presentationSettings.ClashMotionEnabled;
        private LegacyClashTuning ClashTuning => presentationSettings != null ? presentationSettings.ClashTuning
            : LegacyClashTuning.Default;

        // Under the card rows (built just before them): the ring, the socket and the leaving cards.
        private void BuildQueueCues()
        {
            queueCues = Rect(QueueCuesName, root, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            nextReservationSocket = Rect(NextReservationSocketName, queueCues, Vector2.zero, Vector2.one * QueueCardSize,
                Vector2.one * .5f);
            nextReservationSocketFill = Image("Socket Fill", nextReservationSocket, null, Vector2.zero, Vector2.zero,
                NextReservationSocketInk);
            RectTransform fill = nextReservationSocketFill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            Edges(nextReservationSocket, SocketLine, DuelVisualTheme.Border);
            nextReservationSocketGroup = Quiet(nextReservationSocket);
            nextReservationSocket.gameObject.SetActive(false);
            nextOpponentRing = Rect(NextOpponentRingName, queueCues, Vector2.zero,
                Vector2.one * (QueueCardSize + NextOpponentRingMargin * 2f), Vector2.one * .5f);
            Edges(nextOpponentRing, RingLine, NextOpponentRingInk, nextOpponentRingEdges);
            nextOpponentRingGroup = Quiet(nextOpponentRing);
            nextOpponentRing.gameObject.SetActive(false);
        }

        // Over the card rows (built just after them): the contact and reservation sparks.
        private void BuildQueueSparks()
        {
            queueSparks = Rect(QueueSparksName, root, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            for (int index = 0; index < clashSparks.Length; index++) clashSparks[index] = CreateSpark(ClashSparkName);
            for (int index = 0; index < reservationSparks.Length; index++) reservationSparks[index] = CreateSpark(ReservationSparkName);
        }

        // A frame's four thin edges, following its size (kept in <paramref name="into"/> to be inked again).
        private static void Edges(RectTransform frame, float line, Color ink, List<Image> into = null)
        {
            Image[] edges =
            {
                Edge("Top", frame, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, line), ink),
                Edge("Bottom", frame, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, line), ink),
                Edge("Left", frame, Vector2.zero, new Vector2(0f, 1f), new Vector2(line, 0f), ink),
                Edge("Right", frame, new Vector2(1f, 0f), Vector2.one, new Vector2(line, 0f), ink),
            };
            into?.AddRange(edges);
        }

        private static Image Edge(string name, RectTransform frame, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color ink)
        {
            Image edge = Image(name, frame, null, Vector2.zero, size, ink);
            edge.rectTransform.anchorMin = anchorMin;
            edge.rectTransform.anchorMax = anchorMax;
            return edge;
        }

        // A group that fades its node and never takes input.
        private static CanvasGroup Quiet(RectTransform node)
        {
            var group = node.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            return group;
        }

        private SparkBurst CreateSpark(string name)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(DuelMeshSparks))
                .GetComponent<RectTransform>();
            node.gameObject.layer = queueSparks.gameObject.layer;
            node.SetParent(queueSparks, false);
            node.anchorMin = node.anchorMax = node.pivot = Vector2.one * .5f;
            node.sizeDelta = Vector2.one * 200f;
            var burst = new SparkBurst { Node = node, Sparks = node.GetComponent<DuelMeshSparks>() };
            burst.Sparks.raycastTarget = false;
            StopSpark(burst);
            return burst;
        }

        /// <summary>The clash motion's frame, before the rows take this frame's cards (a slot just completed still has its card
        /// to hand to a leaving copy): reads the resolving slot and moves the meetings on by <paramref name="delta"/> (the
        /// battle's clock). A slot that has left the duel hands its cards to copies only once its rows hide them, which they
        /// do on this frame's refresh when <paramref name="activeSlot"/> has moved past it; the turn's last slot (and a
        /// finishing blow) keeps its cards shown until the next turn, so it plays out on them. Outside a resolving turn, or
        /// switched off, nothing of it is left.</summary>
        private void AdvanceQueueClash(ILegacyDuelView session, bool resolving, int activeSlot, float delta)
        {
            if (!resolving || !ClashMotionEnabled)
            {
                ClearQueueClash();
                return;
            }
            ObserveClash(session);
            // The rows show the slots from the active one on (QueueView.Refresh): a left slot below it goes this frame.
            int consumed = Mathf.Max(0, activeSlot);
            foreach (ClashPlay play in leavingClashes)
                if (!play.TakenOut && play.Slot < consumed) TakeCardsOut(play);
            LegacyClashTuning tuning = ClashTuning;
            liveClash?.Advance(delta, tuning);
            for (int index = leavingClashes.Count - 1; index >= 0; index--)
            {
                ClashPlay play = leavingClashes[index];
                play.Advance(delta, tuning);
                if (!play.Finished(tuning)) continue;
                ReleaseClash(play);
                leavingClashes.RemoveAt(index);
            }
            foreach (SparkBurst burst in clashSparks) AdvanceSpark(burst, delta);
        }

        /// <summary>Poses the resolving pair for this frame's layout (<see cref="LayoutQueuePanels"/>, before the rows lay their
        /// cards out), a left slot whose rows still show its cards (on those cards) and the leaving copies, and fires a contact
        /// spark that is due. <paramref name="consumed"/> is the slots already gone from the rows; the resolving card stands
        /// that many slots in.</summary>
        private void PoseQueueClash(float inner, float spacing, float slide, float lineY, int consumed, float delta)
        {
            queueCues.anchoredPosition = queueSparks.anchoredPosition = new Vector2(0f, lineY);
            playerQueue.ClearPose();
            enemyQueue.ClearPose();
            if (liveClash == null && leavingClashes.Count == 0) return;
            LegacyClashTuning tuning = ClashTuning;
            foreach (ClashPlay play in leavingClashes)
            {
                if (play.TakenOut)
                {
                    PoseLeavingCard(play, play.Player, tuning);
                    PoseLeavingCard(play, play.Enemy, tuning);
                }
                else
                {
                    // Its rows still show it (the turn's last slot): the meeting plays on its own cards, never on a copy too.
                    // A resolving pair, posed after, takes a row's one pose (never both at once with the controllers).
                    PlaceClash(play, inner, spacing, slide, consumed);
                    PoseCard(play, play.Player, playerQueue, tuning);
                    PoseCard(play, play.Enemy, enemyQueue, tuning);
                }
                if (play.SparkDue) FireClashSpark(play, tuning);
            }
            if (liveClash != null)
            {
                ClashPlay play = liveClash;
                PlaceClash(play, inner, spacing, slide, consumed);
                if (!play.Impact) play.Lean(delta, tuning);
                PoseCard(play, play.Player, playerQueue, tuning);
                PoseCard(play, play.Enemy, enemyQueue, tuning);
                if (play.SparkDue) FireClashSpark(play, tuning);
            }
        }

        // Where the slot's cards stand this frame and their scale at rest as the rows drew them (the active card's
        // enlargement in), so the front edges the meeting reaches are the drawn ones. Both rows share the place and scale.
        private void PlaceClash(ClashPlay play, float inner, float spacing, float slide, int consumed)
        {
            float distance = LegacyQueuePanelLayout.CardDistance(Mathf.Max(0, play.Slot - consumed), inner, spacing, slide);
            QueueView row = playerQueue.IsShown(play.Slot) ? playerQueue : enemyQueue.IsShown(play.Slot) ? enemyQueue : null;
            if (row == null || !row.TryGetCardLook(play.Slot, out _, out _, out _, out float rest)) rest = queueCardScale;
            play.Place(distance, Mathf.Max(0f, distance - QueueCardSize * rest * .5f), rest);
        }

        /// <summary>The card the clash motion draws off its place in one side's row this frame (the resolving pair's, or the
        /// turn's last slot still meeting after it completed): its slot and the offset added to its place in the row. False
        /// while every card of that row stands where the row puts it.</summary>
        public bool TryGetPosedQueueCard(bool playerSide, out int slot, out Vector2 offset)
        {
            slot = -1;
            offset = Vector2.zero;
            return !disposed && (playerSide ? playerQueue : enemyQueue).TryGetPose(out slot, out offset);
        }

        private static void PoseCard(ClashPlay play, ClashCard side, QueueView row, LegacyClashTuning tuning)
        {
            side.Posed = row.IsShown(play.Slot);
            if (!side.Posed)
            {
                side.LastPose = LegacyCardPose.Rest;
                return;
            }
            LegacyCardPose pose = play.PoseOf(side, tuning);
            side.LastPose = pose;
            row.SetPose(play.Slot, Offset(pose, side.IsPlayer), Tilt(pose, side.IsPlayer), 1f + pose.Squash);
        }

        private static void PoseLeavingCard(ClashPlay play, ClashCard side, LegacyClashTuning tuning)
        {
            if (side.Ghost == null) return;
            LegacyCardPose pose = play.PoseOf(side, tuning);
            side.LastPose = pose;
            side.Ghost.anchoredPosition = new Vector2(side.BaseX, 0f) + Offset(pose, side.IsPlayer);
            side.Ghost.localRotation = Quaternion.Euler(0f, 0f, Tilt(pose, side.IsPlayer));
            side.Ghost.localScale = Vector3.one * (side.RestScale * (1f + pose.Squash));
            side.GhostGroup.alpha = LegacyClashPose.GhostAlpha(play.Progress(tuning));
        }

        // Forward is toward the seam: right for the player's card, left for the opponent's.
        private static Vector2 Offset(LegacyCardPose pose, bool playerSide) => new Vector2((playerSide ? 1f : -1f) * pose.Forward, pose.Up);

        // A positive tilt leans the card back, away from the seam: anticlockwise on the left, clockwise on the right.
        private static float Tilt(LegacyCardPose pose, bool playerSide) => (playerSide ? 1f : -1f) * pose.Tilt;

        // One spark where the two cards' fronts stand as they meet: the seam itself when both reach it, a guard's face when only
        // the attack travels, halfway across a gap too wide to close (the coach's seam).
        private void FireClashSpark(ClashPlay play, LegacyClashTuning tuning)
        {
            play.SparkDue = false;
            float seconds = presentationSettings != null ? presentationSettings.ClashSparkSeconds : DefaultClashSparkSeconds;
            int count = presentationSettings != null ? presentationSettings.ClashSparkCount : DefaultClashSparkCount;
            float size = (presentationSettings != null ? presentationSettings.ClashSparkSize : DefaultClashSparkSize) *
                LegacyClashPose.SparkScale(play.Clash) * queueCardScale * play.Emphasis;
            LegacyClashKind kind = play.Clash.Kind;
            float playerFront = play.Player.BaseX + QueueCardSize * play.Player.RestScale * .5f +
                LegacyClashPose.Meet(kind, play.Clash.PlayerMove, play.ContactOf(play.Player, tuning), tuning).Forward;
            float enemyFront = play.Enemy.BaseX - QueueCardSize * play.Enemy.RestScale * .5f -
                LegacyClashPose.Meet(kind, play.Clash.EnemyMove, play.ContactOf(play.Enemy, tuning), tuning).Forward;
            SparkBurst burst = clashSparks[nextClashSpark];
            nextClashSpark = (nextClashSpark + 1) % clashSparks.Length;
            Color head = presentationSettings != null ? presentationSettings.ClashSparkHeadInk : DuelPresentationSettings.DefaultClashSparkHeadInk;
            Color tail = presentationSettings != null ? presentationSettings.ClashSparkTailInk : DuelPresentationSettings.DefaultClashSparkTailInk;
            BeginSpark(burst, new Vector2((playerFront + enemyFront) * .5f, 0f), seconds, size, count, head, tail, 1f);
            burst.Slot = play.Slot;
            lastClashSpark = burst;
        }

        // The slot left the duel (completed, or the turn settled): what is left of its meeting goes on leaving. Its cards
        // play it while their rows still show them (the turn's last slot: the controllers keep it active until the next
        // turn); once the rows hide them, copies take over (AdvanceQueueClash). A meeting not begun, or over, leaves nothing:
        // the cards just go, or rest.
        private void LeaveClash(ClashPlay play)
        {
            LegacyClashTuning tuning = ClashTuning;
            if (!play.Impact || play.Finished(tuning)) return;
            leavingClashes.Add(play);
            while (leavingClashes.Count > LeavingClashCapacity)
            {
                ReleaseClash(leavingClashes[0]);
                leavingClashes.RemoveAt(0);
            }
        }

        // The rows hide the slot's cards on this frame's refresh: copies of them, as last drawn, play out the meeting.
        private void TakeCardsOut(ClashPlay play)
        {
            play.TakenOut = true;
            TakeCardOut(play, play.Player, playerQueue);
            TakeCardOut(play, play.Enemy, enemyQueue);
        }

        private void TakeCardOut(ClashPlay play, ClashCard side, QueueView row)
        {
            if (!side.Posed || !row.TryGetCardLook(play.Slot, out RectTransform card, out Color ring, out Sprite icon, out float rest))
                return;
            Image copy = Panel(ClashCardName, queueCues, card.anchoredPosition, Vector2.one * QueueCardSize, Card, ring);
            // The card's ring in its school's dye and its icon, as the row builds them.
            PanelSurface(copy).rectTransform.sizeDelta = -Vector2.one * (QueueCardRing * 2f);
            Image picture = Image("Icon", copy.transform, icon, Vector2.zero, Vector2.one * QueueIconSize);
            picture.preserveAspect = true;
            side.Ghost = copy.rectTransform;
            side.Ghost.localRotation = card.localRotation;
            side.Ghost.localScale = card.localScale;
            side.GhostGroup = Quiet(side.Ghost);
            side.RestScale = rest;
        }

        private static void ReleaseClash(ClashPlay play)
        {
            ReleaseCard(play.Player);
            ReleaseCard(play.Enemy);
        }

        private static void ReleaseCard(ClashCard side)
        {
            if (side.Ghost == null) return;
            side.Ghost.gameObject.SetActive(false);
            Destroy(side.Ghost.gameObject);
            side.Ghost = null;
            side.GhostGroup = null;
        }

        // Nothing of the motion is left: no pair, no leaving card, no spark (planning, the turn's start, a reset).
        private void ClearQueueClash()
        {
            liveClash = null;
            pendingEmphasisSlot = -1;
            if (leavingClashes.Count > 0)
            {
                foreach (ClashPlay play in leavingClashes) ReleaseClash(play);
                leavingClashes.Clear();
            }
            foreach (SparkBurst burst in clashSparks)
                if (burst != null) StopSpark(burst);
            lastClashSpark = null;
            playerQueue?.ClearPose();
            enemyQueue?.ClearPose();
        }

        /// <summary>The planning cues for this frame, after the rows are laid out: the ring and the socket where the next
        /// reservation meets the opponent (only while the player can reserve now and the opponent has a card there), then the
        /// reservation sparks (moving on on real time with their cards, and a pending one begun).</summary>
        private void UpdateQueueCues(ILegacyDuelView session, bool planning, float realDelta)
        {
            int slot = planning && QueuePairCuesEnabled && !inputsHeld && inspectedSlot < 0 && CanReserveNow()
                ? LegacyClashMotion.NextOpponentSlot(session) : -1;
            RectTransform target = slot >= 0 ? enemyQueue.GetAnchor(slot) : null;
            ringSlot = target != null ? slot : -1;
            float ringAlpha = NextOpponentRingAlpha, socketAlpha = NextReservationSocketAlpha;
            bool ringShown = target != null && ringAlpha > 0f, socketShown = target != null && socketAlpha > 0f;
            if (nextOpponentRing.gameObject.activeSelf != ringShown) nextOpponentRing.gameObject.SetActive(ringShown);
            if (nextReservationSocket.gameObject.activeSelf != socketShown) nextReservationSocket.gameObject.SetActive(socketShown);
            if (target == null) ringPhase = 0f;
            else
            {
                // Planning draws every card at its tuned size, so the ring and the socket fit any of them.
                float size = QueueCardWidth;
                Vector2 place = target.anchoredPosition;
                nextOpponentRing.anchoredPosition = place;
                nextOpponentRing.sizeDelta = Vector2.one * (size + NextOpponentRingMargin * 2f * queueCardScale);
                Color ringInk = NextOpponentRingInk;
                foreach (Image edge in nextOpponentRingEdges)
                    if (edge.color != ringInk) edge.color = ringInk;
                float period = NextOpponentRingPulseSeconds, depth = NextOpponentRingPulseDepth;
                ringPhase = period > 0f ? Mathf.Repeat(ringPhase + Mathf.Max(0f, realDelta) / period, 1f) : 0f;
                // Breathing between (1 − depth) of its strength and all of it (60% to 100% by default).
                nextOpponentRingGroup.alpha = ringAlpha *
                    (period > 0f ? 1f - depth * .5f * (1f - Mathf.Cos(ringPhase * Mathf.PI * 2f)) : 1f);
                // The same distance from the seam on the player's side: slot k faces slot k.
                nextReservationSocket.anchoredPosition = new Vector2(-place.x, place.y);
                nextReservationSocket.sizeDelta = Vector2.one * size;
                nextReservationSocketGroup.alpha = socketAlpha;
                Color socketInk = NextReservationSocketInk;
                if (nextReservationSocketFill.color != socketInk) nextReservationSocketFill.color = socketInk;
            }

            foreach (SparkBurst burst in reservationSparks) FollowReservationSpark(burst, realDelta);
            if (pendingReservationSpark < 0) return;
            int reserved = pendingReservationSpark;
            pendingReservationSpark = -1;
            if (planning) StartReservationSpark(reserved);
        }

        // Whether the player may reserve anything now: a lane takes a click, or 숨고르기 does.
        private bool CanReserveNow()
        {
            foreach (Button lane in laneButtons)
                if (lane != null && lane.interactable) return true;
            return breathButton.gameObject.activeSelf && breathButton.interactable;
        }

        // A spark at both cards of the slot just reserved into, if the opponent holds one there, in the forecast's look.
        private void StartReservationSpark(int slot)
        {
            RectTransform mine = playerQueue.GetAnchor(slot), theirs = enemyQueue.GetAnchor(slot);
            float seconds = presentationSettings != null ? presentationSettings.ReservationSparkSeconds : DefaultReservationSparkSeconds;
            int baseCount = presentationSettings != null ? presentationSettings.ReservationSparkCount : DefaultReservationSparkCount;
            if (mine == null || theirs == null || !(seconds > 0f) || baseCount <= 0) return;
            LegacyForecastVerdict? verdict = slot < forecastVerdicts.Count && forecastVerdicts[slot] >= 0
                ? (LegacyForecastVerdict)forecastVerdicts[slot] : (LegacyForecastVerdict?)null;
            ReservationLook(verdict, out Color ink, out float scale);
            float size = (presentationSettings != null ? presentationSettings.ReservationSparkSize : DefaultReservationSparkSize) *
                scale * queueCardScale;
            int count = Mathf.Max(1, Mathf.RoundToInt(baseCount * scale));
            // A hot head drawn toward white, cooling to the ink.
            Color head = Color.Lerp(ink, Color.white, .4f), tail = ink;
            head.a = 1f;
            tail.a = 0f;
            int pair = nextReservationPair;
            nextReservationPair = (pair + 1) % ReservationSparkPairs;
            for (int side = 0; side < 2; side++)
            {
                bool playerSide = side == 0;
                SparkBurst burst = reservationSparks[pair * 2 + side];
                BeginSpark(burst, InnerEdge(playerSide ? mine : theirs, playerSide), seconds, size, count, head, tail, ink.a);
                burst.Slot = slot;
                burst.PlayerSide = playerSide;
            }
            reservationSparkSlot = slot;
            reservationSparkVerdict = verdict;
        }

        private void ReservationLook(LegacyForecastVerdict? verdict, out Color ink, out float scale)
        {
            DuelPresentationSettings tuning = presentationSettings;
            switch (verdict)
            {
                case LegacyForecastVerdict.Favourable:
                    ink = tuning != null ? tuning.ReservationSparkFavourableInk : DuelPresentationSettings.DefaultReservationSparkFavourableInk;
                    scale = tuning != null ? tuning.ReservationSparkFavourableScale : DefaultReservationSparkFavourableScale;
                    break;
                case LegacyForecastVerdict.Unfavourable:
                    ink = tuning != null ? tuning.ReservationSparkUnfavourableInk : DuelPresentationSettings.DefaultReservationSparkUnfavourableInk;
                    scale = tuning != null ? tuning.ReservationSparkUnfavourableScale : DefaultReservationSparkUnfavourableScale;
                    break;
                default:
                    ink = tuning != null ? tuning.ReservationSparkEvenInk : DuelPresentationSettings.DefaultReservationSparkEvenInk;
                    scale = 1f;
                    break;
            }
        }

        // The card's edge facing the seam, where a reservation spark bursts.
        private Vector2 InnerEdge(RectTransform card, bool playerSide)
            => card.anchoredPosition + new Vector2((playerSide ? 1f : -1f) * QueueCardWidth * .5f, 0f);

        private void FollowReservationSpark(SparkBurst burst, float realDelta)
        {
            if (burst == null || !burst.IsPlaying) return;
            RectTransform card = (burst.PlayerSide ? playerQueue : enemyQueue).GetAnchor(burst.Slot);
            if (card == null)
            {
                StopSpark(burst);
                return;
            }
            burst.Node.anchoredPosition = InnerEdge(card, burst.PlayerSide);
            AdvanceSpark(burst, realDelta);
        }

        private static void BeginSpark(SparkBurst burst, Vector2 position, float seconds, float size, int count, Color head,
            Color tail, float alpha)
        {
            if (!(seconds > 0f) || count <= 0 || !(size > 0f))
            {
                StopSpark(burst);
                return;
            }
            burst.Elapsed = 0f;
            burst.Seconds = seconds;
            burst.Count = count;
            burst.Reach = size * LegacyMeshCue.SparkReach;
            burst.Width = Mathf.Max(MinimumSparkWidth, size * SparkWidthShare);
            burst.Node.anchoredPosition = position;
            burst.Sparks.SetInks(head, tail);
            burst.Sparks.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (!burst.Node.gameObject.activeSelf) burst.Node.gameObject.SetActive(true);
            burst.Sparks.Configure(count, 0f, burst.Reach, burst.Width);
        }

        private static void AdvanceSpark(SparkBurst burst, float delta)
        {
            if (burst == null || !burst.IsPlaying) return;
            burst.Elapsed += delta > 0f && !float.IsInfinity(delta) ? delta : 0f;
            if (!burst.IsPlaying)
            {
                StopSpark(burst);
                return;
            }
            burst.Sparks.Configure(burst.Count, burst.Elapsed / burst.Seconds, burst.Reach, burst.Width);
        }

        private static void StopSpark(SparkBurst burst)
        {
            burst.Elapsed = burst.Seconds = 0f;
            burst.Slot = -1;
            burst.Sparks.Configure(0, 1f, 0f, 0f);
            if (burst.Node.gameObject.activeSelf) burst.Node.gameObject.SetActive(false);
        }

        // A new turn (and Reset): no ring, no spark, no motion.
        private void ResetQueueCues()
        {
            ClearQueueClash();
            ResetReadCue();
            pendingReservationSpark = -1;
            reservationSparkSlot = -1;
            reservationSparkVerdict = null;
            foreach (SparkBurst burst in reservationSparks)
                if (burst != null) StopSpark(burst);
            ringSlot = -1;
            ringPhase = 0f;
            if (nextOpponentRing != null) nextOpponentRing.gameObject.SetActive(false);
            if (nextReservationSocket != null) nextReservationSocket.gameObject.SetActive(false);
        }

        /// <summary>One resolving slot's meeting: its classification (live until the first hit, then kept), its clock from the
        /// first hit, and its two cards.</summary>
        private sealed class ClashPlay
        {
            public readonly int Slot;
            public readonly ClashCard Player = new ClashCard(true), Enemy = new ClashCard(false);
            public LegacyClash Clash;
            // TakenOut: its slot left the rows and copies of its cards play the rest of the meeting.
            public bool Impact, SparkDue, TakenOut;
            public float ImpactElapsed;
            // The director's weight (a Major slot's scale, 1 for none) and a blade bind: Bound plays the meeting as an even
            // 합; while BindSeconds is left the pair holds at the seam on the real time the controller hands over.
            public float Emphasis = 1f;
            public bool Bound;
            public float BindSeconds, BindElapsed;
            public int BindSparks;
            private bool sparkFired;
            private bool awaitingFirstHit;

            public bool Binding => BindSeconds > 0f && BindElapsed < BindSeconds;

            public ClashPlay(int slot) { Slot = slot; }

            public void Observe(LegacyClashInput input)
            {
                if (!Impact || awaitingFirstHit && input.Decided)
                {
                    // A dodge, or a 반격 settling at the first hit, may still change it until then.
                    Clash = LegacyClashMotion.Classify(input);
                    if (input.Decided)
                    {
                        awaitingFirstHit = false;
                        if (!Impact)
                        {
                            Impact = true;
                            ImpactElapsed = 0f;
                            Player.ImpactStart = Player.Approach;
                            Enemy.ImpactStart = Enemy.Approach;
                        }
                    }
                }
                // A later hit of a multi-hit skill can still break someone.
                Player.NoteBreak(input.PlayerBroken);
                Enemy.NoteBreak(input.EnemyBroken);
            }

            // The controller reached the contact frame without resolving damage yet. Start directly at the seam and
            // let the eventual first hit settle the classification, retaining this already-shown contact and its spark.
            public void MeetBeforeHit(LegacyClashTuning tuning)
            {
                if (Impact) return;
                Impact = true;
                awaitingFirstHit = true;
                ImpactElapsed = LegacyClashPose.StrikeShare * tuning.ImpactSeconds;
                Player.ImpactStart = Player.Approach;
                Enemy.ImpactStart = Enemy.Approach;
                if (Clash.Contact) sparkFired = SparkDue = true;
            }

            public void Advance(float delta, LegacyClashTuning tuning)
            {
                delta = delta > 0f && !float.IsInfinity(delta) ? delta : 0f;
                // A bind holds the meeting at the seam: only its own real time moves it (AdvanceBind).
                if (Impact && !Binding)
                {
                    float before = ImpactElapsed;
                    ImpactElapsed += delta;
                    if (!sparkFired && Clash.Contact && LegacyClashPose.ReachesContact(before, ImpactElapsed, tuning.ImpactSeconds))
                        sparkFired = SparkDue = true;
                }
                Player.AdvanceShake(delta, tuning.BreakShakeSeconds);
                Enemy.AdvanceShake(delta, tuning.BreakShakeSeconds);
            }

            public float Progress(LegacyClashTuning tuning) => LegacyClashPose.Progress(ImpactElapsed, tuning.ImpactSeconds);

            public bool Finished(LegacyClashTuning tuning)
                => Impact && !Binding && ImpactElapsed >= tuning.ImpactSeconds && !Player.Shaking && !Enemy.Shaking;

            // The bind's real time: the cards travel on to the seam (the contact spark as they meet) and hold there, a spark
            // at each of its moments; then the meeting goes on from the contact on the battle's clock.
            public void AdvanceBind(float realSeconds, LegacyClashTuning tuning)
            {
                if (!Binding) return;
                realSeconds = realSeconds > 0f && !float.IsInfinity(realSeconds) ? realSeconds : 0f;
                float moment = LegacyClashPose.StrikeShare * tuning.ImpactSeconds;
                float before = ImpactElapsed;
                ImpactElapsed = Mathf.Max(ImpactElapsed, Mathf.Min(moment, ImpactElapsed + realSeconds));
                if (!sparkFired && Clash.Contact && LegacyClashPose.ReachesContact(before, ImpactElapsed, tuning.ImpactSeconds))
                    sparkFired = SparkDue = true;
                float share = BindElapsed / BindSeconds;
                BindElapsed = Mathf.Min(BindSeconds, BindElapsed + realSeconds);
                if (LegacyBladeBind.SparksBetween(share, BindElapsed / BindSeconds, BindSparks) > 0) SparkDue = true;
                if (BindElapsed >= BindSeconds)
                {
                    // Held to the end: the meeting goes on from the contact.
                    ImpactElapsed = Mathf.Max(ImpactElapsed, moment);
                    BindSeconds = BindElapsed = 0f;
                }
            }

            // Where the resolving cards stand this frame (both rows share the spacing, so the pair mirrors).
            public void Place(float distance, float gap, float restScale)
            {
                Player.BaseX = -distance;
                Enemy.BaseX = distance;
                Player.Gap = Enemy.Gap = gap;
                Player.RestScale = Enemy.RestScale = restScale;
            }

            // Before the first hit: lean toward the approach the classification asks for now.
            public void Lean(float delta, LegacyClashTuning tuning)
            {
                tuning = Emphasized(tuning);
                Player.Approach = LegacyClashPose.Follow(Player.Approach,
                    LegacyClashPose.ApproachTarget(Clash.Kind, Clash.PlayerMove, ContactOf(Player, tuning), tuning), delta,
                    tuning.ApproachSeconds);
                Enemy.Approach = LegacyClashPose.Follow(Enemy.Approach,
                    LegacyClashPose.ApproachTarget(Clash.Kind, Clash.EnemyMove, ContactOf(Enemy, tuning), tuning), delta,
                    tuning.ApproachSeconds);
            }

            public float ContactOf(ClashCard side, LegacyClashTuning tuning)
                => LegacyClashPose.ContactTravel(Clash.Kind, side.IsPlayer ? Clash.PlayerMove : Clash.EnemyMove, side.Gap,
                    (side.IsPlayer ? Enemy : Player).Gap, tuning.Reach);

            // The director's weight on the distances (never the times): a Major slot's pair goes farther.
            private LegacyClashTuning Emphasized(LegacyClashTuning t)
                => Emphasis == 1f ? t : new LegacyClashTuning(t.ApproachSeconds, t.ApproachDistance * Emphasis,
                    t.ImpactSeconds, t.Reach, t.PushDistance * Emphasis, t.KnockbackDistance * Emphasis,
                    t.SideStepDistance * Emphasis, t.TiltDegrees * Emphasis, t.BreakShake, t.BreakShakeSeconds, t.SettleDip,
                    t.BraceSquash);

            public LegacyCardPose PoseOf(ClashCard side, LegacyClashTuning tuning)
            {
                float contact = ContactOf(side, tuning);
                tuning = Emphasized(tuning);
                // A bound meeting is an even 합 whatever the small gap: both bounce back alike.
                LegacyClashMove move = Bound ? LegacyClashMove.Bounce : side.IsPlayer ? Clash.PlayerMove : Clash.EnemyMove;
                float strength = Bound ? 0f : side.IsPlayer ? Clash.PlayerStrength : Clash.EnemyStrength;
                LegacyCardPose pose = Impact
                    ? LegacyClashPose.Impact(Clash.Kind, move, strength, side.ImpactStart, contact, Progress(tuning), tuning)
                    : side.Approach;
                if (Binding)
                {
                    // The locked pair sways together along the row (forward is toward the seam: the opponent's is mirrored).
                    float sway = LegacyBladeBind.Sway(BindElapsed / BindSeconds, ClashBindSway);
                    pose = new LegacyCardPose(pose.Forward + (side.IsPlayer ? sway : -sway), pose.Up, pose.Tilt, pose.Squash);
                }
                return side.Shaking
                    ? pose.Shaken(LegacyClashPose.Shake(side.BreakElapsed, tuning.BreakShakeSeconds, tuning.BreakShake))
                    : pose;
            }
        }

        /// <summary>One side's card in a meeting: its approach, where its meeting began, a break's shake, where it stands, and
        /// its copy once its slot has left the row.</summary>
        private sealed class ClashCard
        {
            public readonly bool IsPlayer;
            public LegacyCardPose Approach, ImpactStart, LastPose;
            public float BaseX, Gap, RestScale = 1f, BreakElapsed = -1f;
            public bool Posed;
            public RectTransform Ghost;
            public CanvasGroup GhostGroup;
            private bool brokenSeen;

            public ClashCard(bool isPlayer) { IsPlayer = isPlayer; }

            public bool Shaking => BreakElapsed >= 0f;

            public void NoteBreak(bool broken)
            {
                if (!broken || brokenSeen) return;
                brokenSeen = true;
                BreakElapsed = 0f;
            }

            public void AdvanceShake(float delta, float seconds)
            {
                if (BreakElapsed < 0f) return;
                BreakElapsed += delta;
                if (!(BreakElapsed < seconds)) BreakElapsed = -1f;
            }
        }

        /// <summary>A short spark burst on the queue panels (a contact, or one card of a reservation pair).</summary>
        private sealed class SparkBurst
        {
            public RectTransform Node;
            public DuelMeshSparks Sparks;
            public float Elapsed, Seconds, Reach, Width;
            public int Count, Slot = -1;
            public bool PlayerSide;

            public bool IsPlaying => Seconds > 0f && Elapsed < Seconds;
        }
    }
}
