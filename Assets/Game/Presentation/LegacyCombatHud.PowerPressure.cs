using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        private readonly DuelPowerPressure playerPowerPressure = new DuelPowerPressure();
        private Image playerPressureTrack, playerPressureFill;
        private DuelPressureSteamGraphic playerPressureSteam;
        private ILegacySlotView playerPressureSlot;
        private ILegacyDuelView lastPressureSession;
        private int lastPressureRound = -1, lastPressureQueueIndex = -1;
        private int shownPressureValue = -1;

        public bool IsPlayerPowerPressurizing => !disposed && playerPowerPressure.IsHolding;
        public float PlayerPressureCharge => disposed ? 0f : playerPowerPressure.Charge;
        public float PlayerPressureRelease => disposed ? 0f : playerPowerPressure.Release;

        private void BuildPowerPressure()
        {
            playerPressureTrack = Image("Player Pressure Gauge", powerClashNumbers, null,
                new Vector2(-80f, -25f), new Vector2(104f, 5f), new Color(Border.r, Border.g, Border.b, .55f));
            // Filled Image geometry requires a sprite; a null sprite draws the full quad regardless of fillAmount.
            playerPressureFill = Image("Pressure Fill", playerPressureTrack.transform, Load("white"), Vector2.zero,
                new Vector2(104f, 5f), Accent);
            Filled(playerPressureFill, UnityEngine.UI.Image.FillMethod.Horizontal, 0);
            playerPressureSteam = DuelPressureSteamGraphic.Create(powerClashNumbers, "Player Pressure Steam",
                new Vector2(170f, 40f), steamSprite);
            // Keep the cloud in the number row, above the existing skill-name line below the gauge.
            playerPressureSteam.rectTransform.anchoredPosition = new Vector2(-80f, -6f);
            playerPressureSteam.transform.SetAsFirstSibling();
            HidePlayerPowerPressure();
        }

        /// <summary>Pressure only accompanies a real attack clash whose immutable roll already exists. No forecasts,
        /// rerolls, counters or incomplete charge/normal actions are used to invent a number before the resolver has one.</summary>
        public bool BeginPlayerPowerPressure(ILegacySlotView slot)
        {
            if (disposed || displayedSession?.UsePowerBreak != true || slot == null || !IsPowerClash(slot) ||
                IsPressureCounter(slot)) return false;
            int action = slot is LegacyCurrentSlot current ? current.PlayerQueueIndex : slot.SlotIndex;
            if (action < 0) return false;
            if (ReferenceEquals(lastPressureSession, displayedSession) && lastPressureRound == displayedSession.RoundNumber &&
                lastPressureQueueIndex == action) return false;
            lastPressureSession = displayedSession;
            lastPressureRound = displayedSession.RoundNumber;
            lastPressureQueueIndex = action;
            if (!ReferenceEquals(powerClashSlot, slot)) SetPowerClash(slot);
            playerPressureSlot = slot;
            playerPowerPressure.Begin(slot.PlayerTotalPower);
            shownPressureValue = -1;
            ApplyPlayerPowerPressure();
            return true;
        }

        private static bool IsPressureCounter(ILegacySlotView slot)
            => slot is LegacyCurrentSlot current && (current.PlayerCountered || current.EnemyCountered ||
                    current.PendingPlayerCounter != null); // Versus does not support counters.

        private void AdvancePlayerPowerPressure(float realDelta)
        {
            if (playerPressureSlot == null) return;
            playerPowerPressure.Advance(realDelta);
            ApplyPlayerPowerPressure();
        }

        private void ApplyPlayerPowerPressure()
        {
            bool visible = PowerClashVisible && ReferenceEquals(playerPressureSlot, powerClashSlot) &&
                playerPowerPressure.HasValue;
            bool emitting = visible && playerPowerPressure.IsVisible;
            playerPressureTrack.gameObject.SetActive(emitting && !(powerNumberShattered && powerPlayerBroken));
            playerPressureSteam.gameObject.SetActive(emitting);
            if (!visible) return;
            if (!powerClashImpacted)
            {
                int value = playerPowerPressure.Value;
                if (shownPressureValue != value)
                {
                    shownPressureValue = value;
                    playerClashPower.text = value.ToString();
                }
                // Only the player's value changes. The enemy keeps its actual rolled value and its existing position.
                playerClashPower.rectTransform.localScale = Vector3.one * (1f + .2f * playerPowerPressure.Stamp);
            }
            playerPressureFill.fillAmount = playerPowerPressure.Charge;
            if (emitting)
                playerPressureSteam.Configure(playerPowerPressure.Charge, playerPowerPressure.Release,
                    playerPowerPressure.IsReleasing);
        }

        private void HidePlayerPowerPressure()
        {
            playerPowerPressure.Clear();
            playerPressureSlot = null;
            shownPressureValue = -1;
            if (playerPressureTrack != null) playerPressureTrack.gameObject.SetActive(false);
            if (playerPressureSteam != null)
            {
                playerPressureSteam.Configure(0f, 0f, false);
                playerPressureSteam.gameObject.SetActive(false);
            }
        }

        private void ClearPlayerPowerPressure(bool forgetAction)
        {
            HidePlayerPowerPressure();
            if (!forgetAction) return;
            lastPressureSession = null;
            lastPressureRound = lastPressureQueueIndex = -1;
        }
    }
}
