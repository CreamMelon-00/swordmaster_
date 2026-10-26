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
        public float PlayerPressureTargetCharge => disposed ? 0f : playerPowerPressure.TargetCharge;
        public float PlayerPressureChargeSeconds => disposed ? 0f : playerPowerPressure.ChargeDuration;
        public float PlayerPressureHoldSeconds => disposed ? 0f : playerPowerPressure.HoldDuration;

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

        /// <summary>Every real player skill gets pressure once its immutable roll exists, including defence, breathing
        /// and an accepted counter. Incomplete long actions and empty intervals do not invent a power value.</summary>
        public bool BeginPlayerPowerPressure(ILegacySlotView slot)
        {
            if (disposed || displayedSession?.UsePowerBreak != true || !DuelPowerPressure.ShouldHold(slot)) return false;
            int action = slot is LegacyCurrentSlot current ? current.PlayerQueueIndex : slot.SlotIndex;
            // Counter/empty-side presentation beats must not collide with a later real reservation's index.
            if (action < 0 || slot.PlayerSkill == null) action = -1 - slot.SlotIndex;
            if (ReferenceEquals(lastPressureSession, displayedSession) && lastPressureRound == displayedSession.RoundNumber &&
                lastPressureQueueIndex == action) return false;
            lastPressureSession = displayedSession;
            lastPressureRound = displayedSession.RoundNumber;
            lastPressureQueueIndex = action;
            if (!ReferenceEquals(powerClashSlot, slot)) SetPowerNumbers(slot);
            playerPressureSlot = slot;
            float sharedHold = slot is LocalVersusPerspectiveSlot
                ? Mathf.Max(DuelPowerPressure.HoldFor(slot.PlayerTotalPower, slot.PlayerMaximumPower),
                    DuelPowerPressure.HoldFor(slot.EnemyTotalPower, slot.EnemyMaximumPower)) : 0f;
            playerPowerPressure.Begin(slot.PlayerTotalPower, slot.PlayerMaximumPower, sharedHold);
            shownPressureValue = -1;
            ApplyPlayerPowerPressure();
            return true;
        }

        private bool HasPlayerPowerPressureFor(ILegacySlotView slot)
            => ReferenceEquals(playerPressureSlot, slot) && playerPowerPressure.HasValue;

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
            bool emitting = visible && playerPressureSlot.PlayerSkill != null && playerPowerPressure.IsVisible;
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
