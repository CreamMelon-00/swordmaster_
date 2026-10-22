using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPrototypeController
    {
        private readonly DuelBladeExchange bladeExchange = new DuelBladeExchange();
        public bool IsPlayingBladeExchange => bladeExchange.IsHolding;
        public int BladeExchangeContactCount => bladeExchange.ContactCount;
        public int BladeExchangeContactsPresented => bladeExchange.ContactsPresented;
        public float BladeExchangeDuration => bladeExchange.Duration;

        private bool TryBeginBladeExchange(LegacyCurrentSlot slot, float attackTime)
        {
            if (slot.HitsResolved != 0 || slot.PendingPlayerCounter != null || slot.PlayerCountered || slot.EnemyCountered ||
                slot.DodgeSucceeded || slot.PressureSucceeded || arena.IsStepping ||
                session.Player.IsResistanceBroken || session.Enemy.IsResistanceBroken || IsTrainingBattle ||
                missionEventPending || releasingBladeBindHit) return false;
            if (!bladeExchange.TryBegin(slot, session.RoundNumber, slot.SlotIndex, slot.PlayerSkill, slot.EnemySkill,
                presentationSettings, attackTime)) return false;
            phaseTime = attackTime;
            bladeExchange.Present(arena, art, effectsSource);
            return true;
        }
    }
}
