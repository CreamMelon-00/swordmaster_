using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPrototypeController
    {
        private readonly DuelBladeExchange bladeExchange = new DuelBladeExchange();
        public bool IsPlayingBladeExchange => bladeExchange.IsPlaying;
        public int BladeExchangeContactCount => bladeExchange.ContactCount;
        public int BladeExchangeContactsPresented => bladeExchange.ContactsPresented;
        public float BladeExchangeDuration => bladeExchange.Duration;

        private bool AllowsBladeExchange(LegacyCurrentSlot slot)
        {
            return slot != null && !LegacyArenaView.UsesTempoPresentation(slot) && !(slot.HitsResolved != 0 || slot.PendingPlayerCounter != null || slot.PlayerCountered || slot.EnemyCountered ||
                slot.DodgeSucceeded || slot.PressureSucceeded || arena.IsStepping ||
                session.Player.IsResistanceBroken || session.Enemy.IsResistanceBroken || IsTrainingBattle ||
                missionEventPending || releasingBladeBindHit);
        }

        private void PrepareBladeExchange(LegacyCurrentSlot slot)
        {
            bladeExchange.Clear();
            if (AllowsBladeExchange(slot))
                bladeExchange.TryBegin(slot, session.RoundNumber, slot.SlotIndex, slot.PlayerSkill, slot.EnemySkill,
                    presentationSettings, slotImpactTime);
        }

        private void PresentBladeExchange(float slotTime)
        {
            if (session.CurrentSlot?.HitsResolved > 0) return;
            if (!AllowsBladeExchange(session.CurrentSlot)) { bladeExchange.Clear(); return; }
            bladeExchange.Present(slotTime, arena, art, effectsSource);
        }
    }
}
