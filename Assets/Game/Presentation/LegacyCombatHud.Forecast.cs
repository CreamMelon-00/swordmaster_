using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>편성 예상 on the duel HUD (<see cref="LegacySlotForecast"/> has the rule, Docs/SlotForecast.md). While planning,
    /// each of the player's queued cards carries a small mark on its left rim: how its slot is expected to go against the
    /// enemy's skill in the same slot — 유리 (a green ▲), 불리 (a red ▼) or 비슷 (a grey =) — and the held skill's explanation
    /// and the enemy's Tab card get one line over their hint, <c>예상: 상대 저항 -12 · 내 저항 -6</c>: for the held skill, the
    /// slot it would take if queued now; under Tab, the inspected slot. The duel forecasts on a copy of itself with average
    /// power rolls and no steps, so reading it never changes the fight; the HUD asks again only when the planning state it
    /// shows changes. <see cref="DuelPresentationSettings"/> switches it (and versus's) off and weighs the verdict.</summary>
    public sealed partial class LegacyCombatHud
    {
        /// <summary>The held explanation's line when its skill cannot be queued now (ACT short).</summary>
        public const string ForecastUnavailable = LegacySlotForecast.SummaryPrefix + "지금은 예약할 수 없음";
        /// <summary>The Tab card's line outside planning, when nothing is forecast.</summary>
        public const string ForecastPlanningOnly = LegacySlotForecast.SummaryPrefix + "편성 중에만";
        // A row over the hint (HUD units before the card's scale), as 맞물림's row.
        private const float ForecastNoteRow = 22f;
        // The queued card's mark on its left rim (the 순환 count rides the right).
        private static readonly Vector2 ForecastMarkCenter = new Vector2(-25f, 12f);
        private const float ForecastMarkSize = 18f;
        // On the paper cards the verdict reads in the keyword inks (ACT 회복's green, 고화력's red-brown).
        private static readonly Color FavourableInk = new Color32(36, 89, 75, 255), UnfavourableInk = new Color32(116, 61, 45, 255);
        private readonly List<LegacySlotForecast> turnForecast = new List<LegacySlotForecast>();
        private readonly List<int> forecastVerdicts = new List<int>();
        private readonly List<LegacySkill> forecastPlayerQueue = new List<LegacySkill>(), forecastEnemyQueue = new List<LegacySkill>();
        private readonly LegacySkill[] forecastFronts = new LegacySkill[3];
        private ILegacyDuelView forecastSession;
        private LegacyDuelPhase forecastPhase;
        private int forecastRound = -1, forecastAct = -1, forecastVersion;
        private int forecastPlayerHealth, forecastPlayerResistance, forecastEnemyHealth, forecastEnemyResistance;
        private LegacySkill heldForecastSkill;
        private int heldForecastVersion = -1;
        private LegacySlotForecast heldForecast;
        private RectTransform playerForecastNote, enemyForecastNote;
        private Text playerForecastText, enemyForecastText;
        private SlotForecastGlyph playerForecastGlyph, enemyForecastGlyph;

        /// <summary>Whether the duel shown now forecasts its slots: on unless the settings switch it off (or off in versus).</summary>
        public bool SlotForecastEnabled => !disposed && displayedSession != null && (presentationSettings == null ||
            presentationSettings.SlotForecastEnabled &&
            (!(displayedSession is LocalVersusPerspective) || presentationSettings.SlotForecastInVersus));

        /// <summary>The turn as last forecast for the shown duel (empty outside planning or while switched off).</summary>
        public IReadOnlyList<LegacySlotForecast> SlotForecast => turnForecast;

        /// <summary>The verdict the player's queued card <paramref name="queueIndex"/> shows, or null while it shows none.</summary>
        public LegacyForecastVerdict? GetForecastVerdict(int queueIndex) => disposed ? null : playerQueue.GetForecastVerdict(queueIndex);

        /// <summary>The mark on the player's queued card <paramref name="queueIndex"/>, or null while it shows none.</summary>
        public RectTransform GetForecastMark(int queueIndex) => disposed ? null : playerQueue.GetForecastMark(queueIndex);

        /// <summary>The held explanation's 예상 line, or null while it is not shown.</summary>
        public string ForecastNote => disposed || !playerForecastNote.gameObject.activeSelf ? null : playerForecastText.text;

        /// <summary>The Tab card's 예상 line, or null while it is not shown.</summary>
        public string EnemyForecastNote => disposed || !enemyForecastNote.gameObject.activeSelf ? null : enemyForecastText.text;

        /// <summary>A slot's verdict with the tuned weights.</summary>
        public LegacyForecastVerdict Judge(LegacySlotForecast forecast) => presentationSettings == null ? forecast.Judge()
            : forecast.Judge(presentationSettings.SlotForecastResistanceWeight, presentationSettings.SlotForecastBreakBonus,
                presentationSettings.SlotForecastMargin);

        // The rows over both explanations' hints: a verdict glyph and the line, hidden until a duel that forecasts shows them.
        private void BuildForecastNotes()
        {
            playerForecastNote = BuildForecastNote(playerExplanation, PlayerExplanationScale, ExplanationType(14), ExplanationType(11),
                out playerForecastText, out playerForecastGlyph);
            enemyForecastNote = BuildForecastNote(enemyExplanation, 1f, 14, 11, out enemyForecastText, out enemyForecastGlyph);
        }

        private RectTransform BuildForecastNote(RectTransform panel, float scale, int fontSize, int minimumSize, out Text text,
            out SlotForecastGlyph glyph)
        {
            RectTransform row = Rect("Forecast Note", panel, Vector2.zero, new Vector2(416f, ForecastNoteRow) * scale, Vector2.one * .5f);
            RectTransform mark = Rect("Forecast Note Glyph", row, new Vector2(-199f, 0f) * scale, Vector2.one * 14f * scale, Vector2.one * .5f);
            mark.gameObject.AddComponent<CanvasRenderer>();
            glyph = mark.gameObject.AddComponent<SlotForecastGlyph>();
            glyph.raycastTarget = false;
            text = Text("Forecast Note Text", row, new Vector2(12f, 0f) * scale, new Vector2(392f, ForecastNoteRow) * scale,
                fontSize, TextAnchor.MiddleLeft);
            text.color = DuelVisualTheme.Ink;
            text.supportRichText = false;
            FitExplanationLabel(text, minimumSize);
            row.gameObject.SetActive(false);
            return row;
        }

        /// <summary>Brings the turn's forecast and the player's card marks up to date (every frame, before the queue rows).</summary>
        private void UpdateSlotForecast(ILegacyDuelView session, bool planning)
        {
            forecastVerdicts.Clear();
            if (planning && SlotForecastEnabled)
            {
                EnsureForecast(session);
                for (int slot = 0; slot < session.PlayerQueue.Count; slot++)
                {
                    LegacySlotForecast forecast = slot < turnForecast.Count ? turnForecast[slot] : null;
                    forecastVerdicts.Add(forecast != null && forecast.Reached ? (int)Judge(forecast) : -1);
                }
            }
            playerQueue.SetForecastVerdicts(forecastVerdicts);
        }

        // Forecasts the turn again when anything it reads has changed: the queues, ACT, the lanes' fronts, the fighters or
        // the turn. A changed forecast also lets the held skill's be asked again.
        private void EnsureForecast(ILegacyDuelView session)
        {
            bool changed = !ReferenceEquals(forecastSession, session) || forecastPhase != session.Phase ||
                forecastRound != session.RoundNumber || forecastAct != session.Act ||
                forecastPlayerHealth != session.Player.Health || forecastPlayerResistance != session.Player.Resistance ||
                forecastEnemyHealth != session.Enemy.Health || forecastEnemyResistance != session.Enemy.Resistance ||
                !SameSkills(forecastPlayerQueue, session.PlayerQueue) || !SameSkills(forecastEnemyQueue, session.EnemyQueue);
            for (int lane = 0; lane < forecastFronts.Length; lane++)
            {
                IReadOnlyList<LegacySkill> order = session.GetLane(lane);
                LegacySkill front = order.Count > 0 ? order[0] : null;
                if (!ReferenceEquals(forecastFronts[lane], front)) changed = true;
                forecastFronts[lane] = front;
            }
            if (!changed) return;
            forecastSession = session;
            forecastPhase = session.Phase;
            forecastRound = session.RoundNumber;
            forecastAct = session.Act;
            forecastPlayerHealth = session.Player.Health;
            forecastPlayerResistance = session.Player.Resistance;
            forecastEnemyHealth = session.Enemy.Health;
            forecastEnemyResistance = session.Enemy.Resistance;
            Remember(forecastPlayerQueue, session.PlayerQueue);
            Remember(forecastEnemyQueue, session.EnemyQueue);
            forecastVersion++;
            session.ForecastTurn(turnForecast);
        }

        /// <summary>The held explanation's 예상 line for <paramref name="skill"/>, the front of its lane: the slot it would take if
        /// queued now. Shown for as long as the duel forecasts; true when the row came or went, so the card is laid out again.</summary>
        private bool UpdatePlayerForecastNote(LegacySkill skill)
        {
            bool shown = SlotForecastEnabled;
            bool toggled = playerForecastNote.gameObject.activeSelf != shown;
            if (toggled) playerForecastNote.gameObject.SetActive(shown);
            if (!shown) return toggled;
            LegacySlotForecast forecast = null;
            bool planning = displayedSession.Phase == LegacyDuelPhase.Planning;
            if (planning)
            {
                EnsureForecast(displayedSession);
                if (!ReferenceEquals(heldForecastSkill, skill) || heldForecastVersion != forecastVersion)
                {
                    heldForecastSkill = skill;
                    heldForecastVersion = forecastVersion;
                    IReadOnlyList<LegacySkill> lane = displayedSession.GetLane(Mathf.Clamp(skill.LaneIndex, 0, 2));
                    heldForecast = lane.Count > 0 && ReferenceEquals(lane[0], skill)
                        ? displayedSession.ForecastIfQueued(skill.LaneIndex) : null;
                }
                forecast = heldForecast;
            }
            ShowForecastLine(playerForecastText, playerForecastGlyph, forecast, planning ? ForecastUnavailable : ForecastPlanningOnly);
            return toggled;
        }

        /// <summary>The Tab card's 예상 line for the inspected slot (<see cref="SetInspectedSlot"/>).</summary>
        private bool UpdateEnemyForecastNote()
        {
            bool shown = SlotForecastEnabled;
            bool toggled = enemyForecastNote.gameObject.activeSelf != shown;
            if (toggled) enemyForecastNote.gameObject.SetActive(shown);
            if (!shown) return toggled;
            LegacySlotForecast forecast = null;
            if (displayedSession.Phase == LegacyDuelPhase.Planning)
            {
                EnsureForecast(displayedSession);
                if (inspectedSlot >= 0 && inspectedSlot < turnForecast.Count) forecast = turnForecast[inspectedSlot];
            }
            ShowForecastLine(enemyForecastText, enemyForecastGlyph, forecast, ForecastPlanningOnly);
            return toggled;
        }

        private void ShowForecastLine(Text text, SlotForecastGlyph glyph, LegacySlotForecast forecast, string fallback)
        {
            string line = forecast?.Summary ?? fallback;
            bool judged = forecast != null && forecast.Reached;
            LegacyForecastVerdict verdict = judged ? Judge(forecast) : LegacyForecastVerdict.Even;
            if (text.text != line) text.text = line;
            text.color = !judged ? DuelVisualTheme.Ink : verdict == LegacyForecastVerdict.Favourable ? FavourableInk
                : verdict == LegacyForecastVerdict.Unfavourable ? UnfavourableInk : DuelVisualTheme.Ink;
            text.fontStyle = judged && verdict != LegacyForecastVerdict.Even ? FontStyle.Bold : FontStyle.Normal;
            glyph.gameObject.SetActive(judged);
            glyph.SetVerdict(verdict);
            glyph.color = text.color;
        }

        private void ResetForecast()
        {
            turnForecast.Clear();
            forecastVerdicts.Clear();
            forecastPlayerQueue.Clear();
            forecastEnemyQueue.Clear();
            for (int lane = 0; lane < forecastFronts.Length; lane++) forecastFronts[lane] = null;
            forecastSession = null;
            forecastRound = forecastAct = -1;
            heldForecastSkill = null;
            heldForecast = null;
            heldForecastVersion = -1;
            playerForecastNote.gameObject.SetActive(false);
            enemyForecastNote.gameObject.SetActive(false);
        }

        private static bool SameSkills(List<LegacySkill> remembered, IReadOnlyList<LegacySkill> current)
        {
            if (remembered.Count != current.Count) return false;
            for (int index = 0; index < current.Count; index++)
                if (!ReferenceEquals(remembered[index], current[index])) return false;
            return true;
        }

        private static void Remember(List<LegacySkill> remembered, IReadOnlyList<LegacySkill> current)
        {
            remembered.Clear();
            for (int index = 0; index < current.Count; index++) remembered.Add(current[index]);
        }

        /// <summary>The queued card's mark: its fill on the dark HUD (유리 green, 불리 red, 비슷 grey); its glyph is ink.</summary>
        public static Color ForecastFill(LegacyForecastVerdict verdict)
            => verdict == LegacyForecastVerdict.Favourable ? DuelVisualTheme.Health
                : verdict == LegacyForecastVerdict.Unfavourable ? DuelVisualTheme.Danger : DuelVisualTheme.Muted;
    }

    /// <summary>편성 예상's verdict as a small engraved sign: ▲ for 유리, ▼ for 불리, = for 비슷. A non-interactive mesh in
    /// the graphic's colour, so it reads without the font or colour alone.</summary>
    public sealed class SlotForecastGlyph : MaskableGraphic
    {
        public LegacyForecastVerdict Verdict { get; private set; }

        public void SetVerdict(LegacyForecastVerdict value)
        {
            if (Verdict == value) return;
            Verdict = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float size = Mathf.Min(r.width, r.height);
            Vector2 c = r.center;
            switch (Verdict)
            {
                case LegacyForecastVerdict.Favourable:
                    Triangle(vh, c + new Vector2(-.42f, -.32f) * size, c + new Vector2(0f, .36f) * size, c + new Vector2(.42f, -.32f) * size);
                    break;
                case LegacyForecastVerdict.Unfavourable:
                    Triangle(vh, c + new Vector2(-.42f, .32f) * size, c + new Vector2(.42f, .32f) * size, c + new Vector2(0f, -.36f) * size);
                    break;
                default:
                    Bar(vh, c + new Vector2(0f, .16f) * size, size * .8f, size * .16f);
                    Bar(vh, c + new Vector2(0f, -.16f) * size, size * .8f, size * .16f);
                    break;
            }
        }

        private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 d)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
        }

        private void Bar(VertexHelper vh, Vector2 center, float width, float height)
        {
            int index = vh.currentVertCount;
            vh.AddVert(center + new Vector2(-width, -height) * .5f, color, Vector2.zero);
            vh.AddVert(center + new Vector2(-width, height) * .5f, color, Vector2.zero);
            vh.AddVert(center + new Vector2(width, height) * .5f, color, Vector2.zero);
            vh.AddVert(center + new Vector2(width, -height) * .5f, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
