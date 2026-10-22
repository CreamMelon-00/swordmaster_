using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A short visual exchange before a real hit. Its local hash never consumes combat randomness.
    /// Each beat approaches contact then releases the pose; the caller alone presents sound and the real hit.</summary>
    public sealed class LegacyBladeExchange
    {
        public const float DefaultMinSeconds = .08f;
        public const float DefaultMaxSeconds = .14f;
        private const float RestPoseShare = .1f;
        private readonly double[] beatDurations = new double[2];
        private double elapsed, totalDuration;

        public bool IsPlaying { get; private set; }
        public int ContactCount { get; private set; }
        public int ContactsPresented { get; private set; }
        public float TotalDuration => (float)totalDuration;

        public float PoseShare
        {
            get
            {
                if (!IsPlaying) return RestPoseShare;
                double local = elapsed;
                for (int index = 0; index < ContactCount; index++)
                {
                    double duration = beatDurations[index];
                    if (local <= duration)
                    {
                        double phase = local / duration;
                        double triangle = phase <= .5d ? phase * 2d : (1d - phase) * 2d;
                        return (float)(RestPoseShare + (1d - RestPoseShare) * Math.Max(0d, triangle));
                    }
                    local -= duration;
                }
                return RestPoseShare;
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
            ContactCount = 1 + (int)(Mix(key, 0xD1B54A35u) & 1u);
            elapsed = totalDuration = 0d;
            ContactsPresented = 0;
            for (int index = 0; index < ContactCount; index++)
            {
                uint sample = Mix(key, index == 0 ? 0x94D049BBu : 0x369DEA0Fu);
                double fraction = (sample & 0x00FFFFFFu) / 16777215d;
                beatDurations[index] = min + ((double)max - min) * fraction;
                totalDuration += beatDurations[index];
            }
            IsPlaying = true;
        }

        /// <summary>Presents at most one contact. A long frame stops at that contact and discards overshoot,
        /// ensuring the contact pose is visible before any later beat or the actual hit can play.</summary>
        public bool Advance(float realSeconds)
        {
            if (!Finite(realSeconds) || realSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if (!IsPlaying || realSeconds == 0f) return false;
            double target = Math.Min(totalDuration, elapsed + realSeconds);
            if (ContactsPresented < ContactCount)
            {
                double contactTime = beatDurations[ContactsPresented] / 2d;
                for (int index = 0; index < ContactsPresented; index++) contactTime += beatDurations[index];
                if (target >= contactTime)
                {
                    elapsed = contactTime;
                    ContactsPresented++;
                    return true;
                }
            }
            elapsed = target;
            if (elapsed >= totalDuration) IsPlaying = false;
            return false;
        }

        public void Clear()
        {
            IsPlaying = false;
            ContactCount = ContactsPresented = 0;
            elapsed = totalDuration = 0d;
            beatDurations[0] = beatDurations[1] = 0d;
        }

        public static bool CanExchange(LegacySkill a, LegacySkill b)
        {
            if (a == null || b == null || a.IsWait || b.IsWait) return false;
            return a.Kind == LegacySkillKind.Attack &&
                (b.Kind == LegacySkillKind.Attack || b.Kind == LegacySkillKind.Defence) ||
                b.Kind == LegacySkillKind.Attack && a.Kind == LegacySkillKind.Defence;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
