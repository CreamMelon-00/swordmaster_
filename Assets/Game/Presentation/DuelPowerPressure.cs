using System;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation
{
    /// <summary>Presentation-only pressure: climb to an already rolled power, release, then retain the final value.</summary>
    public sealed class DuelPowerPressure
    {
        // These are the full-scale times. A weaker roll opens the valve earlier on the same pressure curve.
        public const float ChargeSeconds = .30f, FastHalfSeconds = .06f, LockSeconds = .06f, SteamSeconds = .32f;
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
        private double elapsed, targetCharge, chargeDuration, holdDuration;
        private const double FastSlope = FastHalfSeconds * 2d;
        private const double SlowCurve = (ChargeSeconds - FastSlope) * 4d;
        public int Target { get; private set; }
        public int MaximumPower { get; private set; }
        public bool HasValue { get; private set; }
        public float TargetCharge => HasValue ? (float)targetCharge : 0f;
        public float ChargeDuration => HasValue ? (float)chargeDuration : 0f;
        public float HoldDuration => HasValue ? (float)holdDuration : 0f;
        private bool Charged => elapsed + ClockEpsilon >= chargeDuration;
        public bool IsHolding => HasValue && elapsed + ClockEpsilon < holdDuration;
        public bool IsReleasing => HasValue && Charged && elapsed + ClockEpsilon < chargeDuration + SteamSeconds;
        public bool IsVisible => HasValue && elapsed + ClockEpsilon < chargeDuration + SteamSeconds;
        public float Charge => !HasValue ? 0f : Charged ? (float)targetCharge
            : (float)Math.Min(targetCharge, ChargeAtTime(elapsed));
        public float Release => !HasValue || !Charged ? 0f
            : (float)Math.Max(0d, Math.Min(1d, (elapsed - chargeDuration) / SteamSeconds));
        public float Stamp => !HasValue || !Charged ? 0f
            : (float)Math.Min(1d, Math.Exp(-(elapsed - chargeDuration) * 14d));
        public int Value
        {
            get
            {
                if (!HasValue || Target == 0) return 0;
                if (Charged) return Target;
                // The last point belongs to the valve release, including a one-power attack.
                return (int)Math.Min(Target - 1d, Math.Floor(MaximumPower * ChargeAtTime(elapsed)));
            }
        }

        public static float DurationFor(int finalPower, int maximumPower)
            => (float)TimeAtCharge(TargetFraction(finalPower, maximumPower));

        public static float HoldFor(int finalPower, int maximumPower)
            => DurationFor(finalPower, maximumPower) + LockSeconds;

        public void Begin(int finalPower) => Begin(finalPower, finalPower);

        public void Begin(int finalPower, int maximumPower, float minimumHoldSeconds = 0f)
        {
            Target = Math.Max(0, finalPower);
            MaximumPower = Math.Max(Target, Math.Max(0, maximumPower));
            targetCharge = TargetFraction(Target, MaximumPower);
            chargeDuration = TimeAtCharge(targetCharge);
            // Two versus perspectives may show different rolls, but must release their shared contact together.
            double sharedHold = float.IsNaN(minimumHoldSeconds) || float.IsInfinity(minimumHoldSeconds) ? 0d
                : Math.Max(0d, Math.Min(HoldSeconds, minimumHoldSeconds));
            holdDuration = Math.Max(chargeDuration + LockSeconds, sharedHold);
            elapsed = 0d;
            HasValue = true;
        }

        private static double TargetFraction(int finalPower, int maximumPower)
        {
            int value = Math.Max(0, finalPower), maximum = Math.Max(value, maximumPower);
            return maximum > 0 ? (double)value / maximum : 0d;
        }

        private static double TimeAtCharge(double charge)
        {
            double late = Math.Max(0d, charge - .5d);
            return FastSlope * charge + SlowCurve * late * late;
        }

        private static double ChargeAtTime(double seconds)
        {
            if (seconds <= FastHalfSeconds) return Math.Max(0d, seconds / FastSlope);
            double late = seconds - FastHalfSeconds;
            // Stable quadratic inverse: the first half is fast, then each further point takes longer.
            return Math.Min(1d, .5d + 2d * late / (FastSlope + Math.Sqrt(FastSlope * FastSlope + 4d * SlowCurve * late)));
        }

        public void Advance(float realDelta)
        {
            if (!HasValue || !(realDelta > 0f) || float.IsInfinity(realDelta)) return;
            elapsed = Math.Min(ChargeSeconds + SteamSeconds + 1d, elapsed + realDelta);
        }

        public void Clear()
        {
            elapsed = 0d;
            targetCharge = chargeDuration = holdDuration = 0d;
            Target = MaximumPower = 0;
            HasValue = false;
        }
    }
}
