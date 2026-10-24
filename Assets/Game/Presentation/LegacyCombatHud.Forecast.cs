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
        public const string ForecastPlanningOnly = LegacySlotForecast.SummaryPrefix + "예약 중에만 표시";
        // A row over the hint (HUD units before the card's scale), as 맞물림's row.
        private const float ForecastNoteRow = 22f;
        // The queued card's mark on its left rim (the 순환 count rides the right).
        private static readonly Vector2 ForecastMarkCenter = new Vector2(-25f, 12f);
        private const float ForecastMarkSize = 18f;
        // On the paper cards the verdict reads in the keyword inks (ACT 회복's green, 고화력's red-brown).
        private static readonly Color FavourableInk = new Color32(36, 89, 75, 255), UnfavourableInk = new Color32(116, 61, 45, 255);
        private readonly List<LegacySlotForecast> turnForecast = new List<LegacySlotForecast>();
        // The rule forecast stays exact; only its safe, revealed prefix reaches the HUD or its public accessor.
        private readonly List<LegacySlotForecast> rawTurnForecast = new List<LegacySlotForecast>();
        private readonly List<LegacySlotForecast> tempoPlayerForecast = new List<LegacySlotForecast>(),
            tempoEnemyForecast = new List<LegacySlotForecast>();
        private int tempoForecastVersion = -1;
        private IReadOnlyList<LegacyTimedSkill> forecastEnemyTiming;
        private readonly List<int> forecastVerdicts = new List<int>();
        // The enemy's queued attacks the player's skill in their slot will dodge by itself (자동 회피), as forecast.
        private readonly List<int> forecastDodged = new List<int>();
        private readonly List<LegacySkill> forecastPlayerQueue = new List<LegacySkill>(), forecastEnemyQueue = new List<LegacySkill>();
        private readonly LegacySkill[] forecastFronts = new LegacySkill[3];
        private ILegacyDuelView forecastSession;
        private LegacyDuelPhase forecastPhase;
        private int forecastRound = -1, forecastAct = -1, forecastVersion, forecastCycles;
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

        /// <summary>The revealed, safe prefix of the shown turn's forecast (empty outside planning or while switched off).</summary>
        public IReadOnlyList<LegacySlotForecast> SlotForecast => turnForecast;

        /// <summary>Whether the enemy's queued card <paramref name="queueIndex"/> is struck through: the forecast has the player's
        /// skill in its slot dodge it by itself (사용 효과 자동 회피).</summary>
        public bool IsEnemyCardDodged(int queueIndex) => !disposed && enemyQueue.IsDodged(queueIndex);

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
            forecastDodged.Clear();
            if (planning && SlotForecastEnabled)
            {
                EnsureForecast(session);
                for (int slot = 0; slot < session.PlayerQueue.Count; slot++)
                {
                    LegacySlotForecast forecast = QueueForecast(true, slot);
                    forecastVerdicts.Add(forecast != null && forecast.Reached ? (int)Judge(forecast) : -1);
                    // 자동 회피: the enemy's attack in that slot is struck through on its card.
                    if (!UsesTempoQueue(session) && forecast != null && forecast.Reached && forecast.EnemySkill?.Kind == LegacySkillKind.Attack &&
                        slot < session.EnemyQueue.Count && session.EnemyQueue[slot]?.Id == forecast.EnemySkill.Id &&
                        LegacySkillDefinitions.Find(forecast.PlayerSkill)?.Effect.AutoDodge == true)
                        forecastDodged.Add(slot);
                }
                if (UsesTempoQueue(session))
                    foreach (LegacySlotForecast segment in turnForecast)
                        if (segment.Reached && segment.EnemyQueueIndex >= 0 &&
                            segment.EnemySkill?.Kind == LegacySkillKind.Attack &&
                            LegacySkillDefinitions.Find(segment.PlayerSkill)?.Effect.AutoDodge == true &&
                            !forecastDodged.Contains(segment.EnemyQueueIndex))
                            forecastDodged.Add(segment.EnemyQueueIndex);
            }
            else ResetForecast();
            playerQueue.SetForecastVerdicts(forecastVerdicts);
            enemyQueue.SetDodgedSlots(forecastDodged);
        }

        // Forecasts the turn again when anything it reads has changed: the queues, ACT, the lanes' fronts, the fighters, the
        // 순환 counts and once-per-turn 예약 효과 (CycleSignature), or the turn. A changed forecast also lets the held skill's be
        // asked again.
        private void EnsureForecast(ILegacyDuelView session)
        {
            int cycles = CycleSignature(session);
            bool changed = !ReferenceEquals(forecastSession, session) || forecastPhase != session.Phase ||
                forecastRound != session.RoundNumber || forecastAct != session.Act ||
                forecastPlayerHealth != session.Player.Health || forecastPlayerResistance != session.Player.Resistance ||
                forecastEnemyHealth != session.Enemy.Health || forecastEnemyResistance != session.Enemy.Resistance ||
                forecastCycles != cycles ||
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
            forecastCycles = cycles;
            Remember(forecastPlayerQueue, session.PlayerQueue);
            Remember(forecastEnemyQueue, session.EnemyQueue);
            forecastEnemyTiming = UsesTempoQueue(session) ? LegacySkillTiming.Build(session.EnemyQueue) : null;
            forecastVersion++;
            session.ForecastTurn(rawTurnForecast);
            turnForecast.Clear();
            int enemyIndex = 0;
            for (int index = 0; index < rawTurnForecast.Count; index++)
            {
                enemyIndex = Mathf.Max(enemyIndex, rawTurnForecast[index].EnemyQueueIndex);
                if (!LegacyQueueVisibility.CanForecastSlot(session, UsesTempoQueue(session) ? enemyIndex : index)) break;
                turnForecast.Add(rawTurnForecast[index]);
            }
        }

        /// <summary>The held explanation's 예상 line for <paramref name="skill"/>, the front of its lane: the slot it would take if
        /// queued now. Shown for as long as the duel forecasts; true when the row came or went, so the card is laid out again.</summary>
        private bool UpdatePlayerForecastNote(LegacySkill skill)
        {
            bool planning = displayedSession?.Phase == LegacyDuelPhase.Planning;
            LegacySlotForecast forecast = null;
            if (SlotForecastEnabled && planning)
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
            // A cancel/return skill can land before the current queue's end; judge the slot it will actually take.
            int slot = UsesTempoQueue(displayedSession) ? forecast?.EnemyQueueIndex ?? 0
                : forecast?.SlotIndex ?? displayedSession?.PlayerQueue.Count ?? 0;
            slot = Mathf.Max(0, slot);
            bool shown = SlotForecastEnabled && (!planning || LegacyQueueVisibility.CanForecastSlot(displayedSession, slot));
            if (shown && planning && UsesTempoQueue(displayedSession) && forecast != null && forecastEnemyTiming != null)
                for (int index = 0; index < forecastEnemyTiming.Count; index++)
                {
                    LegacyTimedSkill enemy = forecastEnemyTiming[index];
                    if (enemy.StartHalfTick < forecast.EndHalfTick && enemy.EndHalfTick > forecast.StartHalfTick &&
                        !LegacyQueueVisibility.CanForecastSlot(displayedSession, index))
                    { shown = false; break; }
                }
            bool toggled = playerForecastNote.gameObject.activeSelf != shown;
            if (toggled) playerForecastNote.gameObject.SetActive(shown);
            if (!shown)
            {
                playerForecastText.text = string.Empty;
                playerForecastGlyph.gameObject.SetActive(false);
                return toggled;
            }
            ShowForecastLine(playerForecastText, playerForecastGlyph, forecast, planning ? ForecastUnavailable : ForecastPlanningOnly);
            return toggled;
        }

        /// <summary>The Tab card's 예상 line for the inspected slot (<see cref="SetInspectedSlot"/>).</summary>
        private bool UpdateEnemyForecastNote()
        {
            bool planning = displayedSession?.Phase == LegacyDuelPhase.Planning;
            bool shown = SlotForecastEnabled && (!planning ||
                LegacyQueueVisibility.CanForecastSlot(displayedSession, inspectedSlot));
            bool toggled = enemyForecastNote.gameObject.activeSelf != shown;
            if (toggled) enemyForecastNote.gameObject.SetActive(shown);
            if (!shown)
            {
                enemyForecastText.text = string.Empty;
                enemyForecastGlyph.gameObject.SetActive(false);
                return toggled;
            }
            LegacySlotForecast forecast = null;
            if (planning)
            {
                EnsureForecast(displayedSession);
                forecast = QueueForecast(false, inspectedSlot);
            }
            ShowForecastLine(enemyForecastText, enemyForecastGlyph, forecast, ForecastPlanningOnly);
            return toggled;
        }

        // One ordinary/charged card can span several enemy completions. Its mark describes the whole occupied
        // interval, including attacks it receives while still winding up, instead of borrowing another card's segment.
        private LegacySlotForecast QueueForecast(bool player, int queueIndex)
        {
            if (queueIndex < 0) return null;
            if (!UsesTempoQueue(displayedSession)) return queueIndex < turnForecast.Count ? turnForecast[queueIndex] : null;
            if (tempoForecastVersion != forecastVersion)
            {
                CacheTempoForecast(tempoPlayerForecast, displayedSession.PlayerQueue.Count, true);
                CacheTempoForecast(tempoEnemyForecast, displayedSession.EnemyQueue.Count, false);
                tempoForecastVersion = forecastVersion;
            }
            var cache = player ? tempoPlayerForecast : tempoEnemyForecast;
            return queueIndex < cache.Count ? cache[queueIndex] : null;
        }

        private void CacheTempoForecast(List<LegacySlotForecast> cache, int count, bool player)
        {
            cache.Clear();
            for (int queueIndex = 0; queueIndex < count; queueIndex++)
            {
                bool hidden = false;
                // Do not show a partial verdict for an action crossing a hidden enemy tail.
                for (int i = turnForecast.Count; i < rawTurnForecast.Count; i++)
                    if ((player ? rawTurnForecast[i].PlayerQueueIndex : rawTurnForecast[i].EnemyQueueIndex) == queueIndex)
                    { hidden = true; break; }
                cache.Add(hidden ? null : LegacySlotForecast.AggregateForQueue(turnForecast, queueIndex, player));
            }
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
            rawTurnForecast.Clear();
            tempoPlayerForecast.Clear();
            tempoEnemyForecast.Clear();
            tempoForecastVersion = -1;
            forecastEnemyTiming = null;
            forecastVerdicts.Clear();
            forecastDodged.Clear();
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

        // The 순환 counts of every skill the forecast can meet (the lanes and both queues, each side its own) and the
        // once-per-turn 예약 효과 and each lane's spent reservations, folded into one number. A cancellation can leave the
        // same queue, front and ACT as before while its lane allowance has changed.
        private static int CycleSignature(ILegacyDuelView session)
        {
            unchecked
            {
                int signature = 17 + (int)session.ReserveEffectsUsedThisTurn;
                for (int lane = 0; lane < 3; lane++)
                {
                    signature = signature * 31 + session.LaneReservationsThisTurn(lane);
                    IReadOnlyList<LegacySkill> skills = session.GetLane(lane);
                    for (int index = 0; index < skills.Count; index++)
                        signature = signature * 31 + session.CycleUses(skills[index].Id);
                }
                IReadOnlyList<LegacySkill> queue = session.PlayerQueue;
                for (int index = 0; index < queue.Count; index++) signature = signature * 31 + session.CycleUses(queue[index].Id);
                queue = session.EnemyQueue;
                for (int index = 0; index < queue.Count; index++) signature = signature * 31 + session.EnemyCycleUses(queue[index].Id);
                return signature;
            }
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
