using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>제한시간: the planning clock's rules that skills reach (Docs/SkillSheet.md, 예약 효과·사용 효과). 제한시간 is how long
    /// the opponent takes to reach the player, so "faster me, slower opponent" adds time and "slower me, faster opponent"
    /// takes it; there is no speed number. A cost is paid as a skill is reserved, every time, and never refunded
    /// (<c>제한시간 -n초</c>, <see cref="LegacySkillEffect.ReservePlanningSecondsCost"/>); a gain comes when a skill is used, for
    /// the next turn (<c>앞 X마다 다음 턴 제한시간 +n초</c>, the resolvers' <c>NextPlanningSeconds</c>). Each side only ever moves
    /// its own clock, and a battle whose clock is held (수련, an untimed mission, a coached beat) neither pays nor gains.
    /// The clock itself is the screens' (the campaign's and versus's controllers); these are the numbers they share.</summary>
    public static class LegacyPlanningClock
    {
        /// <summary>A reservation or 넘기기 that would leave less than this on a running clock is refused.</summary>
        public const float MinimumTimeLeft = .5f;
        /// <summary>The most planning seconds one turn's skills may add to the next turn.</summary>
        public const int MaximumBonusSeconds = 10;
        /// <summary>Versus: a turn's clock never starts above this (its base is 20 seconds).</summary>
        public const float VersusCeilingSeconds = 30f;

        /// <summary>The planning seconds reserving <paramref name="skill"/> costs (its 예약 효과 <c>제한시간 -n초</c>); 0 for a skill
        /// without one, breathing and unknown skills.</summary>
        public static int ReservationSeconds(LegacySkill skill)
        {
            if (skill == null || skill.IsWait) return 0;
            return LegacySkillDefinitions.Find(skill)?.Effect.ReservePlanningSecondsCost ?? 0;
        }

        /// <summary>Whether a cost of <paramref name="seconds"/> can be paid with <paramref name="remaining"/> on the clock: always
        /// while the clock is held (<paramref name="clockRuns"/> false: nothing is paid) or for no cost, otherwise only when at
        /// least <see cref="MinimumTimeLeft"/> would be left (n + 0.5 seconds).</summary>
        public static bool CanAfford(float remaining, float seconds, bool clockRuns)
            => !clockRuns || !(seconds > 0f) || remaining - seconds >= MinimumTimeLeft;

        /// <summary>The clock after paying <paramref name="seconds"/>: unchanged while it is held, never below zero.</summary>
        public static float Spend(float remaining, float seconds, bool clockRuns)
            => clockRuns && seconds > 0f ? Math.Max(0f, remaining - seconds) : remaining;

        /// <summary>How long a turn's planning clock starts at: <paramref name="baseSeconds"/> plus the
        /// <paramref name="bonusSeconds"/> the last turn's skills earned (0 to <see cref="MaximumBonusSeconds"/>), never above
        /// <paramref name="ceilingSeconds"/> (nor below the base).</summary>
        public static float TurnStart(float baseSeconds, int bonusSeconds, float ceilingSeconds)
        {
            int bonus = Math.Max(0, Math.Min(MaximumBonusSeconds, bonusSeconds));
            return Math.Max(baseSeconds, Math.Min(ceilingSeconds, baseSeconds + bonus));
        }

        /// <summary>A campaign turn's start: its base (10 seconds and the curriculum's 선택 시간) plus the bonus, at most base + 10.</summary>
        public static float CampaignTurnStart(float baseSeconds, int bonusSeconds)
            => TurnStart(baseSeconds, bonusSeconds, baseSeconds + MaximumBonusSeconds);

        /// <summary>A versus turn's start for one player: the base plus that player's bonus, at most
        /// <see cref="VersusCeilingSeconds"/>.</summary>
        public static float VersusTurnStart(float baseSeconds, int bonusSeconds)
            => TurnStart(baseSeconds, bonusSeconds, VersusCeilingSeconds);

        /// <summary>Adds <paramref name="gained"/> to a turn's running <paramref name="nextSeconds"/> within
        /// <see cref="MaximumBonusSeconds"/>, and returns what was actually added.</summary>
        internal static int AddBonus(ref int nextSeconds, int gained)
        {
            if (gained <= 0) return 0;
            int before = nextSeconds;
            nextSeconds = Math.Min(MaximumBonusSeconds, checked(before + gained));
            return Math.Max(0, nextSeconds - before);
        }
    }
}
