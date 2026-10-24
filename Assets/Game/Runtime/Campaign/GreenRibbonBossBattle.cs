using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The forest duel's pressure and opening groups, revealed as player reservations arrive.</summary>
    public static class GreenRibbonBossBattle
    {
        public const int StageNumber = 8;
        public const string EnemyName = "이아";
        public const string StageName = "숲속의 결투";
        public const int PlayerBaseHealth = 300;
        public const int EnemyHealth = 400;
        public const int EnemyResistance = 50;
        public const int RoundLimit = 30;
        public const int EnemyCounterSkillId = 0;
        public const int MiddleSlashSkillId = 503;
        public const int MirrorSkillId = 504;
        public const int FollowSkillId = 505;
        public const int JealousySkillId = 506;

        public static int PhaseAtHealth(int health, int maximumHealth)
        {
            if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            return (long)health * 100 <= (long)maximumHealth * 30 ? 3
                : (long)health * 100 <= (long)maximumHealth * 75 ? 2 : 1;
        }

        public static int PatternCount(int phase)
        {
            if (phase < 1 || phase > 3) throw new ArgumentOutOfRangeException(nameof(phase));
            return phase == 1 ? 3 : 4;
        }

        /// <summary>Steps are one-based. The explicit breath belongs to the preceding 호흡 reservation.</summary>
        public static IReadOnlyList<LegacySkill> CreatePattern(int phase, int step, bool alternative = false)
        {
            if (step < 1 || step > PatternCount(phase)) throw new ArgumentOutOfRangeException(nameof(step));
            if (phase == 1)
            {
                switch (step)
                {
                    case 1: return T(43, 503);
                    case 2: return alternative ? T(15, 10) : T(505, 10);
                    default: return T(5, 47, 503, 1);
                }
            }
            if (phase == 2)
            {
                switch (step)
                {
                    case 1: return T(-1, 8, 43);
                    case 2: return T(505, 47, 504, 47);
                    case 3: return alternative ? T(17, -1) : T(503);
                    default: return T(-1, 47, 5, 503);
                }
            }
            switch (step)
            {
                case 1: return T(-1, 47, 504, 47);
                case 2: return alternative ? T(17, -1) : T(505, 504);
                case 3: return alternative ? T(48, 47, 6) : T(5, 47, 47);
                default: return T(-1, 47, 503);
            }
        }

        /// <summary>Bootstrap script; EnableIiaBossPatterns owns the live, health-driven queue.</summary>
        public static EnemyScript CreateScript() => new EnemyScript(new[]
        {
            CreatePattern(1, 1), CreatePattern(1, 2), CreatePattern(1, 3),
        });

        private static IReadOnlyList<LegacySkill> T(params int[] ids)
        {
            var result = new LegacySkill[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                LegacySkill source = ids[i] < 0 ? LegacyCommonActions.Breathe : LegacySkillDefinitions.Skill(ids[i]);
                result[i] = new LegacySkill(source.Id, source.Name, source.Cost, source.MinPower, source.MaxPower,
                    source.Kind, source.Property, source.AttackCount, source.LaneIndex, source.Description, source.AnimationName, source.IconId, source.DurationHalfUnits);
            }
            return Array.AsReadOnly(result);
        }
    }
}
