using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>일회성 and 제한시간 on the duel HUD (Docs/SkillSheet.md, 예약 효과·사용 효과):
    /// <list type="bullet">
    /// <item>a 일회성 skill wears a small <c>1회</c> tag on its icon in the lane's window and at the gear's next socket; its
    /// card in the queue gets the same word as its keyword mark (<see cref="LegacyKeywordPreview.QueueMark"/>). Its copies come
    /// and go without the gear turning (the lane's order changes length, so the gear swaps at once), and the used socket keeps
    /// showing the 일회성 skill just reserved, since it never goes round to the back of its lane;</item>
    /// <item>over a lane's window a short pop: <c>+3</c> as copies arrive, <c>소멸</c> as a new turn takes the ones left, and
    /// in versus a reservation's <c>-4초</c> (its timer has already turned to the opponent's clock);</item>
    /// <item>a skill whose reservation costs planning time shows it beside its ACT (<c>3 ACT · 4초</c>) while the clock runs,
    /// and greys like a skill ACT cannot pay for when the clock cannot spare it (n + 0.5 seconds);</item>
    /// <item>a turn that starts with seconds the last turn earned shows <c>+N초</c> rising off the timer, in the place of
    /// 넘기기's <c>-1초</c>.</item>
    /// </list></summary>
    public sealed partial class LegacyCombatHud
    {
        private const float LanePopSeconds = .9f, LanePopRise = 18f;
        private static readonly Vector2 LanePopHome = new Vector2(0f, SkillWindowSize * .5f + 52f);
        private static readonly Color TemporaryTagPaper = SkillToneStyle.Paper(LegacySkillTone.MultiHit);
        private static readonly Color TemporaryTagInk = SkillToneStyle.Ink(LegacySkillTone.MultiHit);
        private readonly RectTransform[] windowTemporaryTags = new RectTransform[3], nextTemporaryTags = new RectTransform[3];
        private readonly Text[] lanePops = new Text[3];
        private readonly float[] lanePopRemaining = new float[3];
        private readonly Color[] lanePopInk = new Color[3];
        private float reservationClock;
        private bool reservationClockRuns;
        // The colour the rising time text wears now: spent time in the danger ink, gained time in health's green.
        private Color timeSpentInk = DuelVisualTheme.Danger;

        /// <summary>Whether a lane's window shows the <c>1회</c> tag (its front skill is 일회성).</summary>
        public bool IsLaneTemporaryTagShown(int lane)
            => !disposed && lane >= 0 && lane < windowTemporaryTags.Length && windowTemporaryTags[lane].gameObject.activeSelf;

        /// <summary>The pop over a lane's window (<c>+3</c>, <c>소멸</c>), or null while none shows.</summary>
        public string GetLanePop(int lane)
            => disposed || lane < 0 || lane >= lanePops.Length || lanePopRemaining[lane] <= 0f ? null : lanePops[lane].text;

        /// <summary>The ACT (and planning time) a lane's window shows for its front skill: <c>1 ACT</c>, <c>3 ACT · 4초</c>.</summary>
        public string GetLaneCostLabel(int lane) => disposed || lane < 0 || lane >= costs.Length ? null : costs[lane].text;

        /// <summary>The skill the used socket of a lane shows when it is not the lane's last (a 일회성 skill just reserved), or
        /// null.</summary>
        public LegacySkill GetUsedSocketOverride(int lane) => disposed || lane < 0 || lane >= gears.Length ? null : gears[lane].UsedSkill;

        /// <summary>The planning clock reservations pay from now (<see cref="LegacyPlanningClock"/>): its time left and whether it
        /// runs. A held clock (수련, an untimed mission, a coached beat) shows no time cost and refuses nothing. Versus passes the
        /// viewer's own clock, which always runs.</summary>
        public void SetReservationClock(float remaining, bool clockRuns)
        {
            if (disposed) return;
            reservationClock = remaining;
            reservationClockRuns = clockRuns;
        }

        /// <summary>A new turn starts with <paramref name="seconds"/> more on the clock: <c>+N초</c> rises off the timer.</summary>
        public void ShowTimeGained(float seconds)
        {
            if (disposed || !(seconds > 0f)) return;
            timeSpent.text = "+" + seconds.ToString("0.#") + "초";
            timeSpentInk = DuelVisualTheme.Health;
            timeSpentRemaining = TimeSpentDuration;
            AdvanceTimeSpent(0f);
        }

        /// <summary>A short word over <paramref name="lane"/>'s window: <c>+3</c> as 일회성 copies arrive
        /// (<paramref name="arriving"/>), <c>소멸</c> as the ones left go.</summary>
        public void ShowLanePop(int lane, string text, bool arriving)
        {
            if (disposed || lane < 0 || lane >= lanePops.Length || string.IsNullOrEmpty(text)) return;
            lanePops[lane].text = text;
            lanePopInk[lane] = arriving ? TemporaryTagPaper : MutedText;
            lanePopRemaining[lane] = LanePopSeconds;
            AdvanceLanePops(0f);
        }

        /// <summary>A reservation's 제한시간 cost where the timer cannot show it: <c>-4초</c> in the danger ink over the
        /// reserved lane's window. Versus uses this while the player continues building their queue.</summary>
        public void ShowLaneTimeSpent(int lane, float seconds)
        {
            if (disposed || !(seconds > 0f) || lane < 0 || lane >= lanePops.Length) return;
            lanePops[lane].text = "-" + seconds.ToString("0.#") + "초";
            lanePopInk[lane] = DuelVisualTheme.Danger;
            lanePopRemaining[lane] = LanePopSeconds;
            AdvanceLanePops(0f);
        }

        private void BuildTemporaryMarks()
        {
            for (int lane = 0; lane < gears.Length; lane++)
            {
                SkillGear gear = gears[lane];
                // On the icons themselves, so the tags ride the gear's turns.
                windowTemporaryTags[lane] = TemporaryTag(gear.Icons[2].rectTransform, 1f);
                nextTemporaryTags[lane] = TemporaryTag(gear.Icons[3].rectTransform, .8f);
                Text pop = Text("Lane Pop", gear.Window, LanePopHome, new Vector2(120f, 24f), 18, TextAnchor.MiddleCenter);
                pop.supportRichText = false;
                pop.raycastTarget = false;
                Outline outline = pop.gameObject.AddComponent<Outline>();
                outline.effectColor = DuelVisualTheme.Ink;
                outline.effectDistance = new Vector2(1f, -1f);
                pop.gameObject.SetActive(false);
                lanePops[lane] = pop;
            }
        }

        private RectTransform TemporaryTag(RectTransform icon, float scale)
        {
            var tag = Panel("Temporary Tag", icon, Vector2.zero, new Vector2(28f, 14f) * scale, TemporaryTagPaper, TemporaryTagInk);
            RectTransform rect = tag.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(-4f, 4f) * scale;
            Text word = Text("Temporary Tag Text", rect, Vector2.zero, new Vector2(28f, 14f) * scale, Mathf.RoundToInt(11f * scale),
                TextAnchor.MiddleCenter);
            word.text = LegacyKeywordPreview.TemporaryTag;
            word.color = TemporaryTagInk;
            word.supportRichText = false;
            word.raycastTarget = false;
            tag.raycastTarget = false;
            tag.gameObject.SetActive(false);
            return rect;
        }

        // The lane's tags, its cost line and whether the clock can spare its front skill's time cost (the lane's ACT check
        // already done by the caller).
        private bool UpdateLaneTemporaryAndTime(int lane, System.Collections.Generic.IReadOnlyList<LegacySkill> sequence,
            int actCost, bool actAffordable)
        {
            LegacySkill current = sequence[0];
            SetActive(windowTemporaryTags[lane], LegacySkillDefinitions.IsTemporary(current));
            SetActive(nextTemporaryTags[lane], sequence.Count > 1 && LegacySkillDefinitions.IsTemporary(sequence[1]));
            int seconds = reservationClockRuns ? LegacyPlanningClock.ReservationSeconds(current) : 0;
            bool timeShort = !LegacyPlanningClock.CanAfford(reservationClock, seconds, reservationClockRuns);
            string costText = seconds > 0 ? actCost + " ACT · " + seconds + "초" : actCost + " ACT";
            if (costs[lane].text != costText)
            {
                costs[lane].text = costText;
                // The plate widens for the time.
                gears[lane].CostPlate.sizeDelta = new Vector2(seconds > 0 ? 92f : 60f, 20f);
                costs[lane].rectTransform.sizeDelta = new Vector2(seconds > 0 ? 96f : 64f, 18f);
            }
            costs[lane].color = timeShort ? DuelVisualTheme.Danger : Foreground;
            return actAffordable && !timeShort;
        }

        private static void SetActive(Component component, bool active)
        {
            if (component.gameObject.activeSelf != active) component.gameObject.SetActive(active);
        }

        private void HideLaneTemporaryMarks(int lane)
        {
            SetActive(windowTemporaryTags[lane], false);
            SetActive(nextTemporaryTags[lane], false);
        }

        private void AdvanceLanePops(float realDelta)
        {
            for (int lane = 0; lane < lanePops.Length; lane++)
            {
                lanePopRemaining[lane] = Mathf.Max(0f, lanePopRemaining[lane] - Mathf.Max(0f, realDelta));
                bool visible = lanePopRemaining[lane] > 0f;
                if (lanePops[lane].gameObject.activeSelf != visible) lanePops[lane].gameObject.SetActive(visible);
                if (!visible) continue;
                float t = 1f - lanePopRemaining[lane] / LanePopSeconds;
                lanePops[lane].rectTransform.anchoredPosition = LanePopHome + new Vector2(0f, LanePopRise * t);
                Color ink = lanePopInk[lane];
                ink.a = 1f - t * t;
                lanePops[lane].color = ink;
            }
        }

        private void ResetTemporaryMarks()
        {
            for (int lane = 0; lane < lanePops.Length; lane++)
            {
                lanePopRemaining[lane] = 0f;
                HideLaneTemporaryMarks(lane);
                gears[lane].UsedSkill = null;
            }
            AdvanceLanePops(0f);
            reservationClock = 0f;
            reservationClockRuns = false;
        }

        // A new planning turn: the used sockets go back to showing each lane's last skill, and each gear reads its lane's
        // order afresh. The turn start's own change (the 일회성 copies left going) is then shown at once, never read as a
        // reservation: one copy left at a lane's front would otherwise look like a front that was reserved and left.
        private void ForgetUsedTemporarySkills()
        {
            foreach (SkillGear gear in gears)
            {
                gear.ShownOrder.Clear();
                if (gear.UsedSkill != null)
                {
                    gear.UsedSkill = null;
                    gear.ShownIcons[1] = -1;
                }
            }
        }
    }
}
