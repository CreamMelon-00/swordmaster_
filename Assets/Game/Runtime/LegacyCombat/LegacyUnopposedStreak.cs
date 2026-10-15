using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>What a resolving slot does to a turn's unopposed streak (<see cref="LegacyUnopposedStreak.StepOf"/>).</summary>
    public enum LegacyStreakStep
    {
        /// <summary>Neither side attacks (guard against guard, a guard or a breath against nothing, a breath against a
        /// breath, nothing at all): the streak stays as it is, neither longer nor broken.</summary>
        Keeps,
        /// <summary>Exactly one side attacks and the other has nothing there (an empty slot or a breath), whichever side
        /// attacks: the streak grows by one.</summary>
        Extends,
        /// <summary>Both sides act against an attack (a 합, a guard against an attack, a dodge, a 반격 filling the slot): the
        /// streak starts over.</summary>
        Resets
    }

    /// <summary>The unopposed streak's numbers (<see cref="LegacyUnopposedStreak"/>). Presentation only: the rules never read
    /// them. The presentation settings hold them; <see cref="Default"/> is the code's defaults and <see cref="Off"/> changes
    /// nothing.</summary>
    public readonly struct LegacyStreakTuning
    {
        public const float DefaultStepPerSlot = .15f, DefaultMaximumSpeed = 1.6f;
        public const int DefaultStartLength = 2;
        /// <summary>The ranges the numbers are kept in: each slot of a streak adds at most this much speed, a streak plays at
        /// most this many times faster, and it starts speeding up by this long a streak at the latest.</summary>
        public const float MaximumStepPerSlot = .5f, MaximumSpeedLimit = 3f;
        public const int MaximumStartLength = 8;

        /// <param name="enabled">Whether a streak speeds anything up at all.</param>
        /// <param name="stepPerSlot">How much faster each slot of the streak plays than the one before, from
        /// <paramref name="startLength"/> on (0 to 0.5; 0.15: 1.15×, 1.3×, …).</param>
        /// <param name="maximumSpeed">The fastest a streak plays (1 to 3; 1 never faster).</param>
        /// <param name="startLength">The streak's length at which it first plays faster (1 to 8; 2: the second unopposed slot
        /// in a row).</param>
        public LegacyStreakTuning(bool enabled, float stepPerSlot, float maximumSpeed, int startLength)
        {
            Enabled = enabled;
            StepPerSlot = Clamp(stepPerSlot, 0f, MaximumStepPerSlot, 0f);
            MaximumSpeed = Clamp(maximumSpeed, 1f, MaximumSpeedLimit, 1f);
            StartLength = startLength < 1 ? 1 : startLength > MaximumStartLength ? MaximumStartLength : startLength;
        }

        /// <summary>The defaults: from the second unopposed slot in a row each plays 0.15 times faster than the one before
        /// (1.15×, 1.3×, 1.45×), up to 1.6×.</summary>
        public static LegacyStreakTuning Default
            => new LegacyStreakTuning(true, DefaultStepPerSlot, DefaultMaximumSpeed, DefaultStartLength);

        /// <summary>Switched off: every slot keeps its speed.</summary>
        public static LegacyStreakTuning Off => new LegacyStreakTuning(false, 0f, 1f, DefaultStartLength);

        public bool Enabled { get; }
        public float StepPerSlot { get; }
        public float MaximumSpeed { get; }
        public int StartLength { get; }
        /// <summary>Whether any streak can play faster: switched on, a step above 0 and a cap above 1.</summary>
        public bool Accelerates => Enabled && StepPerSlot > 0f && MaximumSpeed > 1f;

        private static float Clamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) ? fallback : value < min ? min : value > max ? max : value;
    }

    /// <summary>일방 공격 가속 (presentation only; the rules never read it): when one side keeps attacking into nothing, the
    /// run plays faster and faster, on top of the presentation director's pacing (<see cref="LegacyShowDirector"/>). Pure: the
    /// same slots always count and speed the same, and both sides count alike, so a versus guest's mirrored view agrees.
    /// <list type="bullet">
    /// <item>The streak counts the slots of a turn in a row where exactly one side attacks and the other has nothing there
    /// (<see cref="LegacyClashKind.Unopposed"/>: an empty slot or a breath), whichever side attacks.</item>
    /// <item>A slot where neither side attacks (<see cref="LegacyClashKind.Settle"/>, <see cref="LegacyClashKind.None"/>)
    /// neither adds to nor breaks it; any slot where both sides act against an attack (a 합, a guard, a dodge, a 반격 filling
    /// the slot) starts it over. A turn starts at 0.</item>
    /// <item>An unopposed slot plays <see cref="Speed"/> times faster: 1 below
    /// <see cref="LegacyStreakTuning.StartLength"/>, then <see cref="LegacyStreakTuning.StepPerSlot"/> more per slot up to
    /// <see cref="LegacyStreakTuning.MaximumSpeed"/>.</item>
    /// <item>A Major or Climax slot (a break, a heavy hit, a big unopposed attack, a 간파, a signature move, the finishing
    /// blow) keeps its emphasis at its own speed, but the streak counts on through it.</item>
    /// </list>
    /// When a slot may play faster (never before its first hit while a step could still be taken) is the screen's to keep.</summary>
    public static class LegacyUnopposedStreak
    {
        /// <summary>What a slot of <paramref name="kind"/> does to the streak.</summary>
        public static LegacyStreakStep StepOf(LegacyClashKind kind)
        {
            switch (kind)
            {
                case LegacyClashKind.Unopposed: return LegacyStreakStep.Extends;
                case LegacyClashKind.Clash:
                case LegacyClashKind.GuardPierced:
                case LegacyClashKind.GuardHeld:
                case LegacyClashKind.Dodged: return LegacyStreakStep.Resets;
                default: return LegacyStreakStep.Keeps;
            }
        }

        /// <summary>The streak after a slot of <paramref name="kind"/>, given the one before it (below 0 reads as 0).</summary>
        public static int Next(int streak, LegacyClashKind kind)
        {
            streak = Math.Max(0, streak);
            switch (StepOf(kind))
            {
                case LegacyStreakStep.Extends: return streak == int.MaxValue ? streak : streak + 1;
                case LegacyStreakStep.Resets: return 0;
                default: return streak;
            }
        }

        /// <summary>The streak after a turn's slots of <paramref name="kinds"/>, in order, from 0.</summary>
        public static int Count(IEnumerable<LegacyClashKind> kinds)
        {
            if (kinds == null) throw new ArgumentNullException(nameof(kinds));
            int streak = 0;
            foreach (LegacyClashKind kind in kinds) streak = Next(streak, kind);
            return streak;
        }

        /// <summary>How many times faster a streak of <paramref name="streak"/> slots plays: 1 below
        /// <see cref="LegacyStreakTuning.StartLength"/> (or switched off), then 1 + <see cref="LegacyStreakTuning.StepPerSlot"/>
        /// per slot from it on, at most <see cref="LegacyStreakTuning.MaximumSpeed"/>.</summary>
        public static float Speed(int streak, LegacyStreakTuning tuning)
        {
            if (!tuning.Accelerates || streak < tuning.StartLength) return 1f;
            double speed = 1d + tuning.StepPerSlot * ((double)streak - tuning.StartLength + 1d);
            return (float)Math.Min(tuning.MaximumSpeed, speed);
        }

        /// <summary>Whether a slot of <paramref name="kind"/> rated <paramref name="tier"/> may play faster: unopposed, and
        /// below Major (a Major or Climax slot keeps its emphasis).</summary>
        public static bool Accelerates(LegacyClashKind kind, LegacySlotTier tier)
            => kind == LegacyClashKind.Unopposed && tier < LegacySlotTier.Major;

        /// <summary>The playback speed of a slot of <paramref name="kind"/> rated <paramref name="tier"/>, the streak counted
        /// with it (<paramref name="streak"/>): <see cref="Speed"/> when it <see cref="Accelerates"/>, otherwise 1.</summary>
        public static float SlotSpeed(int streak, LegacyClashKind kind, LegacySlotTier tier, LegacyStreakTuning tuning)
            => Accelerates(kind, tier) ? Speed(streak, tuning) : 1f;

        /// <summary>Every slot's playback speed over one turn, its slots rated in order (each rank's
        /// <see cref="LegacySlotRank.Kind"/> and <see cref="LegacySlotRank.Tier"/>), the streak starting at 0.</summary>
        public static float[] Speeds(IReadOnlyList<LegacySlotRank> slots, LegacyStreakTuning tuning)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            var speeds = new float[slots.Count];
            int streak = 0;
            for (int index = 0; index < speeds.Length; index++)
            {
                streak = Next(streak, slots[index].Kind);
                speeds[index] = SlotSpeed(streak, slots[index].Kind, slots[index].Tier, tuning);
            }
            return speeds;
        }
    }
}
