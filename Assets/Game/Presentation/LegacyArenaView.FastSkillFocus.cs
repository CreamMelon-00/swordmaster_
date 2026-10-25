using System;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private readonly DuelFastSkillFocusEnvelope fastSkillFocus = new DuelFastSkillFocusEnvelope();

        /// <summary>Request the existing backdrop and combat-band focus for this rendered frame only.
        /// This cue never changes the action clock, step result, attack motion or camera subject.</summary>
        public void SetFastSkillFocus(bool active)
        {
            if (!disposed && suspension == null) fastSkillFocus.Request(active);
        }

        public float FastSkillFocusAmount => disposed || suspension != null ? 0f : fastSkillFocus.Amount;

        /// <summary>Visual focus composes by strength; a successful step and a fast action do not double the darkness.</summary>
        public float SlowMotionBackdropFocus => DuelFastSkillFocusEnvelope.Combine(StepFocusAmount, FastSkillFocusAmount);

        private void TickFastSkillFocus(float realDelta)
            => fastSkillFocus.Tick(realDelta, !disposed && suspension == null && resolving &&
                !approaching && !returning && cutInAmount <= .001f);

        private void ClearFastSkillFocus() => fastSkillFocus.Clear();
    }

    // Pure visual-envelope state so both its frame request and real-time lifetime can be checked without Unity.
    internal sealed class DuelFastSkillFocusEnvelope
    {
        public const float EnterSeconds = .07f, ExitSeconds = .12f;
        private bool requested;
        public float Amount { get; private set; }

        public void Request(bool active) => requested = active;

        public void Tick(float realDelta, bool allowed)
        {
            bool active = requested && allowed;
            requested = false;
            if (!allowed)
            {
                Clear();
                return;
            }
            if (!(realDelta > 0f) || float.IsInfinity(realDelta)) return;
            float target = active ? 1f : 0f;
            float seconds = active ? EnterSeconds : ExitSeconds;
            float blend = 1f - (float)Math.Exp(-3f * realDelta / seconds);
            Amount += (target - Amount) * blend;
            if (Math.Abs(Amount - target) < .001f) Amount = target;
        }

        public void Clear()
        {
            requested = false;
            Amount = 0f;
        }

        public static float Combine(float step, float fast) => Math.Max(Clamp01(step), Clamp01(fast));

        private static float Clamp01(float value)
            => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
    }
}
