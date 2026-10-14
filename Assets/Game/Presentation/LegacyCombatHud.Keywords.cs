using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The effects that read this turn's queue (예약 효과, 사용 효과: 순환 +1, 숨고르기 +1, 앞 X마다 위력, 자동 회피) on
    /// the duel HUD, all read from the duel (<see cref="LegacyKeywordPreview"/> has the words), so they are shown before a
    /// skill is reserved and follow the queue as it changes:
    /// <list type="bullet">
    /// <item>under a lane's window, in the 순환 count's place, one line saying what reserving its front skill now would do
    /// (<c>숨 3/4 · +12</c>, <c>감소 2/3 · +10</c>, <c>앞 순환 2</c>, <c>자동 숨 1/1</c>, or <c>턴당 1회 · 사용함</c>);</item>
    /// <item>under 숨고르기's count, <c>자동 1/1</c> while a lane holds a skill that adds one;</item>
    /// <item>on the held explanation, a row over the 맞물림 row with the same in full words;</item>
    /// <item>on queued cards (the queue rows), a keyword mark over the card where a 취소/반환 mark is not (<c>자동</c>,
    /// <c>숨 +12</c>, <c>순환 +1 ×2</c>, <c>회피</c>), and a 순환 count raised by 순환 +1 written <c>1→2/2</c>, or <c>최대</c>
    /// at its cap.</item>
    /// </list>
    /// The cues as the effects work are <see cref="DuelSkillActivationCue"/>'s.</summary>
    public sealed partial class LegacyCombatHud
    {
        // The row's height on the held explanation (HUD units before its scale), like the 맞물림 row's.
        private const float KeywordNoteRow = 22f;
        private static readonly Color KeywordPreviewRim = new Color32(81, 153, 143, 255);
        private readonly RectTransform[] lanePreviewMarks = new RectTransform[3];
        private readonly Text[] lanePreviews = new Text[3];
        private Text autoBreathCount;
        private RectTransform playerKeywordNote;
        private Text playerKeywordNoteText;
        private string shownKeywordNote;

        /// <summary>The line under <paramref name="lane"/>'s window (what reserving its front skill now would do), or null while
        /// it shows none.</summary>
        public string GetLanePreview(int lane)
            => disposed || lane < 0 || lane >= lanePreviewMarks.Length || !lanePreviewMarks[lane].gameObject.activeInHierarchy
                ? null : lanePreviews[lane].text;

        /// <summary>숨고르기's <c>자동 n/1</c>, or null while no lane holds a skill that adds one.</summary>
        public string AutoBreathCount => disposed || !autoBreathCount.gameObject.activeSelf ? null : autoBreathCount.text;

        /// <summary>The held explanation's keyword row (null while it is not shown).</summary>
        public string KeywordNote => disposed || !playerKeywordNote.gameObject.activeSelf ? null : playerKeywordNoteText.text;

        /// <summary>The keyword mark on a queued card (<c>자동</c>, <c>숨 +12</c>, …), or null while it shows none.</summary>
        public string GetKeywordMarkLabel(bool playerSide, int queueIndex)
            => disposed ? null : (playerSide ? playerQueue : enemyQueue).GetKeywordMarkLabel(queueIndex);

        /// <summary>A queued card's 순환 count (<c>1/2</c>, <c>1→2/2</c>, <c>최대</c>), or null while it shows none.</summary>
        public string GetCycleCountLabel(bool playerSide, int queueIndex)
            => disposed ? null : (playerSide ? playerQueue : enemyQueue).GetCycleCountLabel(queueIndex);

        // Each lane window's line sits where its 순환 count does (a cycling skill keeps the count), a little wider.
        private void BuildLanePreviews()
        {
            for (int lane = 0; lane < lanePreviewMarks.Length; lane++)
            {
                var plate = Panel("Keyword Preview", gears[lane].Window, new Vector2(0f, -34f), new Vector2(108f, 21f),
                    DuelVisualTheme.Ink, KeywordPreviewRim);
                lanePreviewMarks[lane] = plate.rectTransform;
                Text line = Text("Keyword Preview Text", plate.transform, Vector2.zero, new Vector2(104f, 18f), 13, TextAnchor.MiddleCenter);
                line.color = Foreground;
                line.supportRichText = false;
                line.verticalOverflow = VerticalWrapMode.Truncate;
                line.resizeTextForBestFit = true;
                line.resizeTextMinSize = 10;
                line.resizeTextMaxSize = 13;
                lanePreviews[lane] = line;
                plate.gameObject.SetActive(false);
            }
        }

        // Under 숨고르기's key hint, at its right: the automatic breath a skill in the deck adds this turn.
        private void BuildAutoBreathCount(Transform breathButton)
        {
            autoBreathCount = Text("Auto Breath", breathButton, new Vector2(38f, -42f), new Vector2(58f, 18f), 12, TextAnchor.MiddleCenter);
            autoBreathCount.color = KeywordPreviewRim;
            autoBreathCount.supportRichText = false;
            autoBreathCount.gameObject.SetActive(false);
        }

        // A lane's line for its front skill: shown for a skill whose effect reads the queue (a cycling one keeps its count).
        private void UpdateLanePreview(int lane, ILegacyDuelView session, LegacySkill current, bool hasCycle)
        {
            string line = hasCycle ? null : LegacyKeywordPreview.LaneLine(session, current);
            bool shown = !string.IsNullOrEmpty(line);
            if (lanePreviewMarks[lane].gameObject.activeSelf != shown) lanePreviewMarks[lane].gameObject.SetActive(shown);
            if (shown && lanePreviews[lane].text != line) lanePreviews[lane].text = line;
            if (shown) lanePreviews[lane].color = line == LegacyKeywordPreview.UsedThisTurn ? MutedText : Foreground;
        }

        private void HideLanePreview(int lane) => lanePreviewMarks[lane].gameObject.SetActive(false);

        private void UpdateAutoBreathCount()
        {
            ILegacyDuelView session = displayedSession;
            bool shown = session != null && BreathShown && HasAutoBreathSkill(session);
            if (autoBreathCount.gameObject.activeSelf != shown) autoBreathCount.gameObject.SetActive(shown);
            if (!shown) return;
            int left = (session.ReserveEffectsUsedThisTurn & LegacyReserveEffect.Breath) != 0 ? 0
                : LegacyQueuedDuel.MaximumAutoBreathsPerTurn;
            string text = "자동 " + left + "/" + LegacyQueuedDuel.MaximumAutoBreathsPerTurn;
            if (autoBreathCount.text != text) autoBreathCount.text = text;
            autoBreathCount.color = left > 0 ? KeywordPreviewRim : MutedText;
        }

        // Whether any lane holds a skill that adds 숨고르기 when reserved (예약 효과 숨고르기 +1).
        private static bool HasAutoBreathSkill(ILegacyDuelView session)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                var skills = session.GetLane(lane);
                for (int index = 0; index < skills.Count; index++)
                    if (LegacySkillDefinitions.Find(skills[index])?.Effect.ReserveBreath == true) return true;
            }
            return false;
        }

        // The row over the held explanation's 맞물림 row: a small mark and a line.
        private void BuildKeywordNote(float scale)
        {
            playerKeywordNote = Rect("Keyword Note", playerExplanation, Vector2.zero, new Vector2(416f, KeywordNoteRow) * scale,
                Vector2.one * .5f);
            Image("Keyword Note Mark", playerKeywordNote, null, new Vector2(-199f, 0f) * scale, Vector2.one * 10f * scale,
                SkillToneStyle.Ink(LegacySkillTone.Cycle));
            playerKeywordNoteText = Text("Keyword Note Text", playerKeywordNote, new Vector2(12f, 0f) * scale,
                new Vector2(392f, KeywordNoteRow) * scale, ExplanationType(14), TextAnchor.MiddleLeft);
            playerKeywordNoteText.color = DuelVisualTheme.Ink;
            playerKeywordNoteText.supportRichText = false;
            FitExplanationLabel(playerKeywordNoteText, ExplanationType(10));
            playerKeywordNote.gameObject.SetActive(false);
        }

        /// <summary>Brings the held explanation's keyword row up to date for <paramref name="skill"/>, the front of its lane
        /// (it follows the queue while the skill is held). True when the row came or went, so the card is laid out again.</summary>
        private bool UpdateKeywordNote(LegacySkill skill)
        {
            string note = displayedSession == null ? null : LegacyKeywordPreview.Note(displayedSession, skill);
            bool shown = !string.IsNullOrEmpty(note);
            bool toggled = playerKeywordNote.gameObject.activeSelf != shown;
            if (toggled) playerKeywordNote.gameObject.SetActive(shown);
            if (shown && note != shownKeywordNote)
            {
                shownKeywordNote = note;
                playerKeywordNoteText.text = note;
            }
            return toggled;
        }

        private void ResetKeywordNote()
        {
            shownKeywordNote = null;
            playerKeywordNote.gameObject.SetActive(false);
            for (int lane = 0; lane < lanePreviewMarks.Length; lane++) HideLanePreview(lane);
            autoBreathCount.gameObject.SetActive(false);
        }
    }
}
