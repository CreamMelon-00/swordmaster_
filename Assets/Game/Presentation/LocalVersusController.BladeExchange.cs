using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    public sealed partial class LocalVersusController
    {
        private readonly DuelBladeExchange bladeExchange = new DuelBladeExchange();
        public bool IsPlayingBladeExchange => bladeExchange.IsPlaying;
        public int BladeExchangeContactCount => bladeExchange.ContactCount;
        public int BladeExchangeContactsPresented => bladeExchange.ContactsPresented;
        public float BladeExchangeDuration => bladeExchange.Duration;

        private bool AllowsBladeExchange(LocalVersusPerspectiveSlot slot)
        {
            return slot != null && slot.HitsResolved == 0 && !view.Player.IsResistanceBroken && !view.Enemy.IsResistanceBroken &&
                !releasingBladeBindHit;
        }

        private void PrepareBladeExchange(LocalVersusPerspectiveSlot slot)
        {
            bladeExchange.Clear();
            if (AllowsBladeExchange(slot))
                bladeExchange.TryBegin(slot.Source, match.RoundNumber, slot.SlotIndex, slot.PlayerSkill, slot.EnemySkill,
                    settings, slotImpactTime);
        }

        private void PresentBladeExchange(float slotTime)
        {
            if (view.CurrentSlot?.HitsResolved > 0) return;
            if (!AllowsBladeExchange(view.CurrentSlot)) { bladeExchange.Clear(); return; }
            bladeExchange.Present(slotTime, arena, art, effects);
        }
    }
}
