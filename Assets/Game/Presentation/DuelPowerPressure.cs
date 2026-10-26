using System;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    /// <summary>Presentation-only pressure: climb to an already rolled power, release, then retain the final value.</summary>
    public sealed class DuelPowerPressure
    {
        public const float ChargeSeconds = .45f, LockSeconds = .12f, SteamSeconds = .32f;
        public const float HoldSeconds = ChargeSeconds + LockSeconds;

        /// <summary>Only a real skill completing now has its immutable power roll. A pending long action or an empty
        /// interval has no value to animate; every completed skill kind, including zero-power breathing, does.</summary>
        public static bool CanPresent(ILegacySlotView slot, bool player = true)
            => slot != null && (player ? slot.PlayerSkill : slot.EnemySkill) != null &&
                (slot is LegacyCurrentSlot current ? current.HitsResolved == 0
                    : slot is LocalVersusPerspectiveSlot versus && versus.HitsResolved == 0);

        /// <summary>Versus peers share the same beat even when only the other side has a skill. That viewer keeps the
        /// opponent's value fixed and shows no player gauge; a one-sided completion cannot make its clock run ahead.</summary>
        public static bool ShouldHold(ILegacySlotView slot)
            => CanPresent(slot) || slot is LocalVersusPerspectiveSlot && CanPresent(slot, false);
        private const double ClockEpsilon = .000001d;
        private double elapsed;
        public int Target { get; private set; }
        public bool HasValue { get; private set; }
        private bool Charged => elapsed + ClockEpsilon >= ChargeSeconds;
        public bool IsHolding => HasValue && elapsed + ClockEpsilon < HoldSeconds;
        public bool IsReleasing => HasValue && Charged && elapsed + ClockEpsilon < ChargeSeconds + SteamSeconds;
        public bool IsVisible => HasValue && elapsed + ClockEpsilon < ChargeSeconds + SteamSeconds;
        public float Charge => HasValue ? Charged ? 1f : (float)Math.Min(1d, elapsed / ChargeSeconds) : 0f;
        public float Release => !HasValue || !Charged ? 0f
            : (float)Math.Max(0d, Math.Min(1d, (elapsed - ChargeSeconds) / SteamSeconds));
        public float Stamp => !HasValue || !Charged ? 0f
            : (float)Math.Min(1d, Math.Exp(-(elapsed - ChargeSeconds) * 14d));
        public int Value
        {
            get
            {
                if (!HasValue || Target == 0) return 0;
                if (Charged) return Target;
                double t = Math.Min(1d, elapsed / ChargeSeconds);
                // The last point belongs to the valve release, including a one-power attack.
                return Math.Min(Target - 1, (int)Math.Floor(Target * (1d - (1d - t) * (1d - t))));
            }
        }

        public void Begin(int finalPower)
        {
            Target = Math.Max(0, finalPower);
            elapsed = 0d;
            HasValue = true;
        }

        public void Advance(float realDelta)
        {
            if (!HasValue || !(realDelta > 0f) || float.IsInfinity(realDelta)) return;
            elapsed = Math.Min(ChargeSeconds + SteamSeconds + 1d, elapsed + realDelta);
        }

        public void Clear()
        {
            elapsed = 0d;
            Target = 0;
            HasValue = false;
        }
    }
}
