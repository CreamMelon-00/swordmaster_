using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>One decorative contact inside the first strike's existing preparation. The combat clock keeps running;
    /// a missed visual window is skipped, and no gameplay hit or random roll belongs to this presenter.</summary>
    internal sealed class DuelBladeExchange
    {
        private readonly LegacyBladeExchange sequence = new LegacyBladeExchange();
        private bool armed;
        private float impactTime, startTime, duration, sampledTime, strength, volume;

        public bool IsPlaying { get; private set; }
        public int ContactCount => sequence.ContactCount;
        public int ContactsPresented => sequence.ContactsPresented;
        public float Duration => duration;

        public bool TryBegin(object slot, int turn, int index, LegacySkill player, LegacySkill enemy,
            DuelPresentationSettings settings, float contactTime)
        {
            Clear();
            if (slot == null || !settings.BladeExchangeEnabled || !(contactTime > 0f) || float.IsInfinity(contactTime) ||
                !LegacyBladeExchange.CanExchange(player, enemy)) return false;
            sequence.Begin(turn, index, player.Id, enemy.Id, settings.BladeExchangeMinSeconds, settings.BladeExchangeMaxSeconds);
            impactTime = contactTime;
            duration = Mathf.Min(sequence.TotalDuration, contactTime * .85f);
            startTime = contactTime - duration;
            strength = settings.BladeExchangeStrength;
            volume = settings.BladeExchangeVolume;
            armed = true;
            return true;
        }

        /// <summary>Called after the arena tick with the slot's current combat time. Temporary pose sampling preserves
        /// the arena's animation clock, so the real strike continues from the contact without a reset or extra wait.</summary>
        public void Present(float slotTime, LegacyArenaView arena, LegacyDuelArt art, AudioSource source)
        {
            IsPlaying = false;
            if (!armed) return;
            if (slotTime >= impactTime)
            {
                // Do not rewind a late frame or insert a cosmetic contact alongside its actual damage.
                armed = false;
                return;
            }
            if (slotTime < startTime || !arena.IsReadyForSlot) return;
            IsPlaying = true;
            float targetTime = Mathf.Clamp01((slotTime - startTime) / duration) * sequence.TotalDuration;
            bool contact = sequence.Advance(Mathf.Max(0f, targetTime - sampledTime));
            sampledTime = Mathf.Max(sampledTime, targetTime);
            arena.PresentBladeExchangePose(Mathf.Lerp(startTime, impactTime, sequence.PoseShare), sequence.RecoilShare);
            if (!contact) return;
            arena.PresentBladeExchangeContact(strength);
            source.pitch = 1.06f;
            art.PlayClash(source, true, false, volume);
        }

        public void Clear()
        {
            sequence.Clear();
            armed = IsPlaying = false;
            impactTime = startTime = duration = sampledTime = 0f;
        }
    }
}
