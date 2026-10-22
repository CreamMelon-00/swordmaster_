using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A short visual exchange before a real hit. Its local hash never consumes combat randomness.
    /// The sword approaches once, deflects, then joins the actual attack without rewinding to its ready pose.
    /// The caller fits this timeline into the existing windup and alone presents sound and the real hit.</summary>
    public sealed class LegacyBladeExchange
    {
        public const float DefaultMinSeconds = .08f;
        public const float DefaultMaxSeconds = .14f;
        public const float ContactShare = .4f;
        private const float RecoilPeakShare = .68f;
        private double elapsed, totalDuration;

        public bool IsPlaying { get; private set; }
        public int ContactCount { get; private set; }
        public int ContactsPresented { get; private set; }
        public float TotalDuration => (float)totalDuration;
        public float Progress => totalDuration > 0d ? (float)(elapsed / totalDuration) : 0f;

        /// <summary>The contact approach never runs backward, including after completion.</summary>
        public float PoseShare => Smooth(Progress / ContactShare);

        /// <summary>A separate, brief deflection after contact; it returns to zero at the real attack's handoff.</summary>
        public float RecoilShare
        {
            get
            {
                float progress = Progress;
                if (progress <= ContactShare || progress >= 1f) return 0f;
                return progress <= RecoilPeakShare
                    ? Smooth((progress - ContactShare) / (RecoilPeakShare - ContactShare))
                    : Smooth((1f - progress) / (1f - RecoilPeakShare));
            }
        }

        public void Begin(int turn, int slot, int leftSkillId, int rightSkillId,
            float min = DefaultMinSeconds, float max = DefaultMaxSeconds)
        {
            if (!Finite(min) || min <= 0f) throw new ArgumentOutOfRangeException(nameof(min));
            if (!Finite(max) || max < min || max > float.MaxValue / 2f)
                throw new ArgumentOutOfRangeException(nameof(max));
            int first = Math.Min(leftSkillId, rightSkillId), second = Math.Max(leftSkillId, rightSkillId);
            uint key = Mix(0x6D2B79F5u, unchecked((uint)turn));
            key = Mix(key, unchecked((uint)slot));
            key = Mix(key, unchecked((uint)first));
            key = Mix(key, unchecked((uint)second));
            ContactCount = 1;
            elapsed = 0d;
            ContactsPresented = 0;
            uint sample = Mix(key, 0x94D049BBu);
            double fraction = (sample & 0x00FFFFFFu) / 16777215d;
            totalDuration = min + ((double)max - min) * fraction;
            IsPlaying = true;
        }

        /// <summary>Reports the one contact as its time passes without discarding frame overshoot or extending the
        /// windup. A caller may omit a cosmetic contact if the whole timeline passed between rendered frames.</summary>
        public bool Advance(float realSeconds)
        {
            if (!Finite(realSeconds) || realSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if (!IsPlaying || realSeconds == 0f) return false;
            elapsed = Math.Min(totalDuration, elapsed + realSeconds);
            bool contact = ContactsPresented == 0 && Progress >= ContactShare;
            if (contact) ContactsPresented = 1;
            if (elapsed >= totalDuration) IsPlaying = false;
            return contact;
        }

        public void Clear()
        {
            IsPlaying = false;
            ContactCount = ContactsPresented = 0;
            elapsed = totalDuration = 0d;
        }

        public static bool CanExchange(LegacySkill a, LegacySkill b)
        {
            if (a == null || b == null || a.IsWait || b.IsWait) return false;
            return a.Kind == LegacySkillKind.Attack &&
                (b.Kind == LegacySkillKind.Attack || b.Kind == LegacySkillKind.Defence) ||
                b.Kind == LegacySkillKind.Attack && a.Kind == LegacySkillKind.Defence;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Smooth(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            return value * value * (3f - 2f * value);
        }

        private static uint Mix(uint state, uint value)
        {
            unchecked
            {
                uint mixed = state ^ (value + 0x9E3779B9u);
                mixed = (mixed ^ (mixed >> 16)) * 0x85EBCA6Bu;
                mixed = (mixed ^ (mixed >> 13)) * 0xC2B2AE35u;
                return mixed ^ (mixed >> 16);
            }
        }
    }
}
