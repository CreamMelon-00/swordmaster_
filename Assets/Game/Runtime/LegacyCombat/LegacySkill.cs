using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public enum LegacySkillKind { Attack, Defence, Wait }
    public enum LegacySkillProperty { Slash, Hit, Penetrate, Defence, None }
    public enum LegacyDuelPhase { Planning, Resolving, Finished }

    public sealed class LegacySkill
    {
        private readonly string baseName;
        private readonly int baseMinPower, baseMaxPower;

        public LegacySkill(int id, string name, int cost, int minPower, int maxPower,
            LegacySkillKind kind, LegacySkillProperty property, int attackCount,
            int laneIndex, string description, string animationName = null, int iconId = 0)
        {
            if (cost < 0 || minPower < 0 || maxPower < minPower || attackCount < 1)
                throw new ArgumentOutOfRangeException(nameof(cost), "Skill costs and power must be valid and hit count must be positive.");
            if (laneIndex < 0 || laneIndex > 2) throw new ArgumentOutOfRangeException(nameof(laneIndex));
            Id = id;
            IconId = iconId > 0 ? iconId : id;
            baseName = Name = name ?? throw new ArgumentNullException(nameof(name));
            Cost = cost;
            baseMinPower = MinPower = minPower;
            baseMaxPower = MaxPower = maxPower;
            Kind = kind;
            Property = property;
            AttackCount = attackCount;
            LaneIndex = laneIndex;
            Description = description ?? string.Empty;
            AnimationName = animationName ?? DefaultAnimationName(property);
        }

        /// <summary>The animation a skill plays when it names none; the skill sheet leaves this one blank.</summary>
        internal static string DefaultAnimationName(LegacySkillProperty property)
            => property == LegacySkillProperty.Defence ? "Defense" : property.ToString();

        public int Id { get; }
        public int IconId { get; }
        public string Name { get; private set; }
        public int Cost { get; }
        public int MinPower { get; private set; }
        public int MaxPower { get; private set; }
        public LegacySkillKind Kind { get; }
        public bool IsWait => Kind == LegacySkillKind.Wait;
        public LegacySkillProperty Property { get; }
        public int AttackCount { get; }
        public int LaneIndex { get; }
        public string Description { get; }
        public string AnimationName { get; }

        /// <summary>Only a player's owned copy is upgraded. Keeping that copy stable lets a duel already holding it
        /// use its new base power from the next slot, without changing the shared sheet definition.</summary>
        internal void SetUpgradeLevel(int level)
        {
            if (level < 0 || level > 3) throw new ArgumentOutOfRangeException(nameof(level));
            Name = baseName + new string('+', level);
            MinPower = checked(baseMinPower + 2 * level);
            MaxPower = checked(baseMaxPower + 2 * level);
        }
    }

    public static class LegacyCommonActions
    {
        // A common queue action, not an acquired/equipped technique or a lane.
        public static LegacySkill Breathe { get; } = new LegacySkill(-1, "숨고르기", 0, 0, 0,
            LegacySkillKind.Wait, LegacySkillProperty.None, 1, 0,
            "ACT를 쓰지 않고 한 박자 쉬어 기술 순서를 조절합니다.", "Idle");
    }

    public static class LegacyInitialSkills
    {
        // The skill sheet's 시작 rows (originally level zero rows 1-9 of Assets/csv/스킬 수치.csv), read live.
        public static IReadOnlyList<LegacySkill> All => LegacySkillDefinitions.InitialSkills;
    }
}
