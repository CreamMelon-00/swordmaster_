using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The 간파 cue on the queue panels (2026-10-15, the read rewards): when the player's read pays off in a slot
    /// (<see cref="TurnLimbo.Runtime.LegacyCombat.LegacyShowDirector.TakeReadCue"/>: a 상대 조건 met, a break, a 맞물림 chain),
    /// the player's card at the seam bursts in gold (<see cref="ReadSparkName"/>), a gold ring spreads off it
    /// (<see cref="ReadRingName"/>), and a small stamp <see cref="ReadStampText"/> pops over it (<see cref="ReadStampName"/>,
    /// switchable). The fighter's light accent is the arena's (<see cref="LegacyArenaView.AccentFighter"/>). It plays on real
    /// time (so it shows through a long hit stop), follows the card while the card is on its row, never takes input and
    /// never holds anything; the next turn (and <see cref="Reset"/>) takes it away. It says nothing the skill activation
    /// cue says (no skill name, no effect): only that the read worked.</summary>
    public sealed partial class LegacyCombatHud
    {
        public const string ReadCueName = "Read Cue", ReadSparkName = "Read Spark", ReadRingName = "Read Ring",
            ReadStampName = "Read Stamp", ReadStampText = "간파";
        // The ring spreads this much past the card and fades over this many real seconds; the burst lasts at most this long.
        private const float ReadRingGrowth = .35f, ReadRingSeconds = .45f, ReadSparkMaximumSeconds = .5f;
        // The stamp pops in over this long from this scale, fades over its last ReadStampFade, and rises this far (HUD units).
        private const float ReadStampPop = .12f, ReadStampPopScale = 1.45f, ReadStampFade = .3f, ReadStampRise = 10f;
        private const int ReadStampFontSize = 24;
        private RectTransform readCue, readRing, readStamp;
        private CanvasGroup readRingGroup, readStampGroup;
        private Text readStampLabel;
        private Outline readStampOutline;
        private readonly System.Collections.Generic.List<Image> readRingEdges = new System.Collections.Generic.List<Image>(4);
        private SparkBurst readSpark;
        private int readCueSlot = -1;
        private float readCueElapsed, readCueSeconds;
        private bool readStampShown;
        private Vector2 readCueAt;

        /// <summary>Whether the 간파 cue is playing (its burst, ring or stamp still on screen).</summary>
        public bool IsReadCuePlaying => !disposed && readCueSlot >= 0;
        /// <summary>The slot whose card the cue plays on (-1 while none).</summary>
        public int ReadCueSlot => disposed ? -1 : readCueSlot;
        /// <summary>The '간파' stamp (active while it shows).</summary>
        public RectTransform ReadStamp => disposed ? null : readStamp;
        /// <summary>Whether the '간파' stamp is showing now.</summary>
        public bool IsReadStampShown => !disposed && readCueSlot >= 0 && readStamp != null && readStamp.gameObject.activeSelf &&
            readCue.gameObject.activeSelf;
        /// <summary>The 간파 burst while it plays, else null.</summary>
        public DuelMeshSparks ReadSpark => disposed || readSpark == null || !readSpark.IsPlaying ? null : readSpark.Sparks;

        private bool ReadCueEnabled => presentationSettings == null || presentationSettings.ReadCueEnabled;
        private bool ReadStampEnabled => presentationSettings == null || presentationSettings.ReadStampEnabled;
        private float ReadCueSettingSeconds => presentationSettings != null ? presentationSettings.ReadCueSeconds : .9f;
        private Color ReadCueInk => presentationSettings != null ? presentationSettings.ReadCueInk
            : DuelPresentationSettings.DefaultReadCueInk;

        /// <summary>The player's read paid off in slot <paramref name="slot"/>: the cue plays on that slot's card on the
        /// player's row (where the card last stood once the row hides it). Nothing while switched off
        /// (<see cref="DuelPresentationSettings.ReadCueEnabled"/>) or for a slot below 0. A new cue replaces one playing.</summary>
        public void ShowReadCue(int slot)
        {
            if (disposed || slot < 0 || readCue == null || !ReadCueEnabled) return;
            readCueSlot = slot;
            readCueElapsed = 0f;
            readCueSeconds = ReadCueSettingSeconds;
            readStampShown = ReadStampEnabled;
            RectTransform card = playerQueue.GetAnchor(slot);
            readCueAt = card != null ? card.anchoredPosition : new Vector2(-(QueueSeamGap * .5f + QueueCardWidth), 0f);
            Color ink = ReadCueInk;
            int count = presentationSettings != null ? presentationSettings.ReadSparkCount : 12;
            float size = (presentationSettings != null ? presentationSettings.ReadSparkSize : 40f) * queueCardScale;
            Color head = Color.Lerp(ink, Color.white, .5f), tail = ink;
            head.a = 1f;
            tail.a = 0f;
            BeginSpark(readSpark, readCueAt, Mathf.Min(ReadSparkMaximumSeconds, readCueSeconds * .55f), size, count, head, tail,
                1f);
            readSpark.Slot = slot;
            readSpark.PlayerSide = true;
            foreach (Image edge in readRingEdges) edge.color = new Color(ink.r, ink.g, ink.b, 1f);
            readStampLabel.color = new Color(ink.r, ink.g, ink.b, 1f);
            readCue.gameObject.SetActive(true);
            readRing.gameObject.SetActive(true);
            readStamp.gameObject.SetActive(readStampShown);
            readCue.SetAsLastSibling();
            LayoutReadCue();
        }

        // Over the card rows and their sparks (built after them): the burst, the ring and the stamp.
        private void BuildReadCue()
        {
            readSpark = CreateSpark(ReadSparkName);
            readCue = Rect(ReadCueName, queueSparks, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            readRing = Rect(ReadRingName, readCue, Vector2.zero, Vector2.one * QueueCardSize, Vector2.one * .5f);
            Edges(readRing, 3f, DuelPresentationSettings.DefaultReadCueInk, readRingEdges);
            readRingGroup = Quiet(readRing);
            readStamp = Rect(ReadStampName, readCue, Vector2.zero, new Vector2(96f, 36f), Vector2.one * .5f);
            readStampGroup = Quiet(readStamp);
            readStampLabel = Text("Read Stamp Label", readStamp, Vector2.zero, new Vector2(96f, 36f), ReadStampFontSize,
                TextAnchor.MiddleCenter);
            readStampLabel.text = ReadStampText;
            readStampLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            readStampLabel.supportRichText = false;
            readStampOutline = readStampLabel.gameObject.AddComponent<Outline>();
            readStampOutline.effectColor = new Color(DuelVisualTheme.Ink.r, DuelVisualTheme.Ink.g, DuelVisualTheme.Ink.b, .9f);
            readStampOutline.effectDistance = new Vector2(2f, -2f);
            readCue.gameObject.SetActive(false);
        }

        // The cue's frame on real time, after the rows were laid out (so it follows the card where it now stands).
        private void UpdateReadCue(float realDelta)
        {
            if (readCueSlot < 0 || readCue == null) return;
            readCueElapsed += realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            RectTransform card = playerQueue.GetAnchor(readCueSlot);
            if (card != null) readCueAt = card.anchoredPosition;
            if (readSpark.IsPlaying) readSpark.Node.anchoredPosition = readCueAt;
            AdvanceSpark(readSpark, realDelta);
            if (readCueElapsed >= Mathf.Max(readCueSeconds, ReadRingSeconds) && !readSpark.IsPlaying)
            {
                ResetReadCue();
                return;
            }
            LayoutReadCue();
        }

        private void LayoutReadCue()
        {
            float card = QueueCardWidth;
            float ring = Mathf.Clamp01(readCueElapsed / ReadRingSeconds);
            readRing.anchoredPosition = readCueAt;
            readRing.sizeDelta = Vector2.one * (card + 6f * queueCardScale);
            readRing.localScale = Vector3.one * (1f + ReadRingGrowth * OutQuad(ring));
            readRingGroup.alpha = 1f - ring;
            if (ring >= 1f && readRing.gameObject.activeSelf) readRing.gameObject.SetActive(false);
            if (!readStampShown) return;
            float seconds = Mathf.Max(.01f, readCueSeconds), elapsed = Mathf.Min(readCueElapsed, seconds);
            float pop = Mathf.Clamp01(elapsed / ReadStampPop);
            float fade = Mathf.Clamp01((seconds - elapsed) / ReadStampFade);
            readStamp.anchoredPosition = readCueAt + new Vector2(0f,
                card * .5f + (ReadStampFontSize * .5f + 8f) * queueCardScale + ReadStampRise * (elapsed / seconds));
            readStamp.localScale = Vector3.one * (Mathf.Lerp(ReadStampPopScale, 1f, OutQuad(pop)) * Mathf.Max(.75f, queueCardScale));
            readStampGroup.alpha = Mathf.Min(Mathf.Clamp01(elapsed / (ReadStampPop * .6f)), fade);
            if (readCueElapsed >= seconds && readStamp.gameObject.activeSelf) readStamp.gameObject.SetActive(false);
        }

        // Nothing of the cue is left (the next turn, a reset, its end).
        private void ResetReadCue()
        {
            readCueSlot = -1;
            readCueElapsed = 0f;
            readStampShown = false;
            if (readSpark != null) StopSpark(readSpark);
            if (readStamp != null) readStamp.gameObject.SetActive(false);
            if (readCue != null) readCue.gameObject.SetActive(false);
        }
    }
}
