using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A fixed two-line explanation above the queue seam on the overlay canvas. The last observed result and
    /// opponent identity survive slot completion; neither depends on card motion or on the arena camera.</summary>
    public sealed partial class LegacyCombatHud
    {
        public const string CombatReadoutName = "Combat Readout";
        private const float ReadoutWidth = 600f, ReadoutHeight = 60f, ReadoutGap = 8f;
        private float ReadoutShownHeight => displayedSession?.UsePowerBreak == true &&
            (PowerClashVisible || ActivePowerNumberFragmentCount > 0) ? 134f : ReadoutHeight;
        private RectTransform combatReadout;
        private Text combatReadoutPair, combatReadoutResult;
        private LegacySlotFacts readoutFacts = LegacySlotFacts.None;
        private LegacySkill readoutPlayerSkill, readoutEnemySkill;
        private string readoutOpponent = "상대";
        private int readoutRound = -1;

        public RectTransform CombatReadout => disposed ? null : combatReadout;
        public string CombatReadoutText => disposed || combatReadoutResult == null ? string.Empty : combatReadoutResult.text;
        public string CombatReadoutPairText => disposed || combatReadoutPair == null ? string.Empty : combatReadoutPair.text;
        public int CombatReadoutSlot => disposed ? -1 : readoutFacts.SlotIndex;
        /// <summary>The readout's top in HUD units from the screen's bottom; zero while hidden.</summary>
        public float CombatReadoutTop => disposed || combatReadout == null || !combatReadout.gameObject.activeSelf ? 0f
            : combatReadout.anchoredPosition.y + ReadoutCanvasHeight * .5f + ReadoutShownHeight * .5f;
        private float ReadoutCanvasHeight => root.rect.height > 1f ? root.rect.height : 1080f;

        private void BuildCombatReadout()
        {
            if (combatReadout != null) return;
            Image panel = Panel(CombatReadoutName, root, Vector2.zero, new Vector2(ReadoutWidth, ReadoutHeight),
                new Color(Surface.r, Surface.g, Surface.b, .93f), Border);
            combatReadout = panel.rectTransform;
            panel.raycastTarget = false;
            Quiet(combatReadout);
            combatReadoutPair = Text("Current Skills", combatReadout, new Vector2(0f, 15f),
                new Vector2(ReadoutWidth - 24f, 26f), 20, TextAnchor.MiddleCenter);
            combatReadoutResult = Text("Actual Result", combatReadout, new Vector2(0f, -13f),
                new Vector2(ReadoutWidth - 24f, 28f), 22, TextAnchor.MiddleCenter);
            combatReadoutPair.color = MutedText;
            combatReadoutResult.color = Foreground;
            ConfigureReadoutText(combatReadoutPair, 15, 20);
            ConfigureReadoutText(combatReadoutResult, 18, 22);
            combatReadout.gameObject.SetActive(false);
        }

        private static void ConfigureReadoutText(Text text, int minimum, int maximum)
        {
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimum;
            text.resizeTextMaxSize = maximum;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>Capture immediately after a hit, before the resolver clears the slot. Current-slot identity is captured
        /// once: a kill can already change EnemyIndexForSlot's fallback while the hit still belongs to the fallen enemy.</summary>
        public void ObserveCombatReadout(ILegacyDuelView session)
        {
            if (disposed || combatReadout == null) return;
            if (session == null || session.Phase == LegacyDuelPhase.Planning)
            {
                ResetCombatReadout();
                return;
            }
            if (readoutRound != session.RoundNumber)
            {
                ResetCombatReadout();
                readoutRound = session.RoundNumber;
            }
            LegacySlotFacts facts = session.CurrentSlotFacts;
            ILegacySlotView slot = session.CurrentSlot;
            if (!facts.HasSlot || slot == null) return;
            bool newSlot = !readoutFacts.HasSlot || facts.SlotIndex != readoutFacts.SlotIndex;
            if (newSlot) readoutOpponent = LegacyClashReadout.OpponentLabel(session);
            var timing = UsesTempoQueue(session) ? slot as LegacyCurrentSlot : null;
            LegacySkill playerAction = timing != null ? timing.PlayerActionSkill : slot.PlayerSkill;
            LegacySkill enemyAction = timing != null ? timing.EnemyActionSkill : slot.EnemySkill;
            if (readoutFacts != facts || !ReferenceEquals(readoutPlayerSkill, playerAction) ||
                !ReferenceEquals(readoutEnemySkill, enemyAction))
            {
                readoutFacts = facts;
                readoutPlayerSkill = playerAction;
                readoutEnemySkill = enemyAction;
                string pair = timing == null ? LegacyClashReadout.Pair(playerAction, enemyAction, readoutOpponent)
                    : "내 " + (playerAction?.Name ?? "빈자리") +
                        (playerAction != null && !timing.PlayerActionCompletes ? " (준비 중)" : string.Empty) +
                        "  ↔  " + readoutOpponent + " " + (enemyAction?.Name ?? "빈자리") +
                        (enemyAction != null && !timing.EnemyActionCompletes ? " (준비 중)" : string.Empty);
                if (session.UsePowerBreak)
                    pair = "내 " + (slot.PlayerBrokenGap ? "파괴된 빈틈 ×2" : playerAction?.Name ?? "빈자리") +
                        (timing != null && playerAction != null && !timing.PlayerActionCompletes ? " (준비 중)" : string.Empty) +
                        "  ↔  " + readoutOpponent + " " + (slot.EnemyBrokenGap ? "파괴된 빈틈 ×2" : enemyAction?.Name ?? "빈자리") +
                        (timing != null && enemyAction != null && !timing.EnemyActionCompletes ? " (준비 중)" : string.Empty);
                string result = session.UsePowerBreak ? LegacyClashReadout.PowerBreakResult(facts) : LegacyClashReadout.Result(facts);
                if (combatReadoutPair.text != pair) combatReadoutPair.text = pair;
                if (combatReadoutResult.text != result) combatReadoutResult.text = result;
            }
            if (!combatReadout.gameObject.activeSelf) combatReadout.gameObject.SetActive(true);
        }

        private void RefreshCombatReadout(ILegacyDuelView session)
        {
            ObserveCombatReadout(session);
            if (combatReadout == null || !combatReadout.gameObject.activeSelf) return;
            combatReadout.sizeDelta = new Vector2(ReadoutWidth, ReadoutShownHeight);
            bool numbers = ReadoutShownHeight > ReadoutHeight;
            combatReadoutPair.rectTransform.anchoredPosition = new Vector2(0f, numbers ? -17f : 15f);
            combatReadoutResult.rectTransform.anchoredPosition = new Vector2(0f, numbers ? -47f : -13f);
            float coachTop = explanationFloor > 0f ? ScreenPosition(new Vector3(0f, explanationFloor, 0f)).y : 0f;
            float bottom = Mathf.Max(queueRowTop, coachTop) + ReadoutGap;
            combatReadout.anchoredPosition = new Vector2(0f, bottom + ReadoutShownHeight * .5f - ReadoutCanvasHeight * .5f);
        }

        private void ResetCombatReadout()
        {
            readoutFacts = LegacySlotFacts.None;
            readoutPlayerSkill = readoutEnemySkill = null;
            readoutOpponent = "상대";
            readoutRound = -1;
            if (combatReadout != null && combatReadout.gameObject.activeSelf) combatReadout.gameObject.SetActive(false);
            if (combatReadoutPair != null && combatReadoutPair.text.Length > 0) combatReadoutPair.text = string.Empty;
            if (combatReadoutResult != null && combatReadoutResult.text.Length > 0) combatReadoutResult.text = string.Empty;
        }
    }
}
