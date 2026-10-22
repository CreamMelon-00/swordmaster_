using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    public sealed partial class LocalVersusController
    {
        private readonly DuelBladeExchange bladeExchange = new DuelBladeExchange();
        public bool IsPlayingBladeExchange => bladeExchange.IsHolding;
        public int BladeExchangeContactCount => bladeExchange.ContactCount;
        public int BladeExchangeContactsPresented => bladeExchange.ContactsPresented;
        public float BladeExchangeDuration => bladeExchange.Duration;

        private bool TryBeginBladeExchange(LocalVersusPerspectiveSlot slot, float attackTime)
        {
            if (slot.HitsResolved != 0 || view.Player.IsResistanceBroken || view.Enemy.IsResistanceBroken ||
                releasingBladeBindHit) return false;
            if (!bladeExchange.TryBegin(slot.Source, match.RoundNumber, slot.SlotIndex, slot.PlayerSkill, slot.EnemySkill,
                settings, attackTime)) return false;
            phaseTime = attackTime;
            bladeExchange.Present(arena, art, effects);
            return true;
        }
    }
}
