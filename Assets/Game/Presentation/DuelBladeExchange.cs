using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>A short visual exchange shared by solo and versus playback. It never advances a duel or consumes its
    /// random stream. Contact feedback is emitted after the arena's real-time tick so a long frame cannot age it away.</summary>
    internal sealed class DuelBladeExchange
    {
        private readonly LegacyBladeExchange sequence = new LegacyBladeExchange();
        private object previousSlot;
        private bool heldThisFrame, contactPending;
        private float impactTime, strength, volume;

        public bool IsHolding => sequence.IsPlaying || heldThisFrame;
        public int ContactCount => sequence.ContactCount;
        public int ContactsPresented => sequence.ContactsPresented;
        public float Duration => sequence.TotalDuration;

        public bool TryBegin(object slot, int turn, int index, LegacySkill player, LegacySkill enemy,
            DuelPresentationSettings settings, float contactTime)
        {
            if (slot == null || ReferenceEquals(previousSlot, slot) || !settings.BladeExchangeEnabled ||
                !LegacyBladeExchange.CanExchange(player, enemy)) return false;
            previousSlot = slot;
            sequence.Begin(turn, index, player.Id, enemy.Id, settings.BladeExchangeMinSeconds, settings.BladeExchangeMaxSeconds);
            impactTime = contactTime;
            strength = settings.BladeExchangeStrength;
            volume = settings.BladeExchangeVolume;
            contactPending = false;
            return true;
        }

        /// <summary>Consumes the entire frame while exchanging, including the release frame. This prevents an actual
        /// hit, or several hits after a stall, from sharing a frame with the last harmless contact.</summary>
        public float Hold(float available, LegacyArenaView arena)
        {
            bool wasHeld = heldThisFrame;
            heldThisFrame = sequence.IsPlaying;
            contactPending = false;
            if (!heldThisFrame)
            {
                if (wasHeld) arena.HoldSlotAtTime(impactTime);
                return 0f;
            }
            contactPending = sequence.Advance(available);
            return available;
        }

        public void Present(LegacyArenaView arena, LegacyDuelArt art, AudioSource source)
        {
            if (!IsHolding) return;
            arena.HoldSlotAtTime(impactTime * sequence.PoseShare);
            if (!contactPending) return;
            contactPending = false;
            arena.PresentBladeExchangeContact(strength);
            source.pitch = sequence.ContactsPresented % 2 == 0 ? 1.12f : 1.06f;
            art.PlayClash(source, true, false, volume);
        }

        public void Clear()
        {
            sequence.Clear();
            previousSlot = null;
            heldThisFrame = contactPending = false;
        }
    }
}
