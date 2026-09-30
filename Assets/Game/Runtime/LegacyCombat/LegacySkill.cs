using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public enum LegacySkillKind { Attack, Defence, Wait }
    public enum LegacySkillProperty { Slash, Hit, Penetrate, Defence, None }
    public enum LegacyDuelPhase { Planning, Resolving, Finished }

    public sealed class LegacySkill
    {
        public LegacySkill(int id, string name, int cost, int minPower, int maxPower,
            LegacySkillKind kind, LegacySkillProperty property, int attackCount,
            int laneIndex, string description, string animationName = null, int iconId = 0)
        {
            if (cost < 0 || minPower < 0 || maxPower < minPower || attackCount < 1)
                throw new ArgumentOutOfRangeException(nameof(cost), "Skill costs and power must be valid and hit count must be positive.");
            if (laneIndex < 0 || laneIndex > 2) throw new ArgumentOutOfRangeException(nameof(laneIndex));
            Id = id;
            IconId = iconId > 0 ? iconId : id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Cost = cost;
            MinPower = minPower;
            MaxPower = maxPower;
            Kind = kind;
            Property = property;
            AttackCount = attackCount;
            LaneIndex = laneIndex;
            Description = description ?? string.Empty;
            AnimationName = animationName ?? (property == LegacySkillProperty.Defence ? "Defense" : property.ToString());
        }

        public int Id { get; }
        public int IconId { get; }
        public string Name { get; }
        public int Cost { get; }
        public int MinPower { get; }
        public int MaxPower { get; }
        public LegacySkillKind Kind { get; }
        public bool IsWait => Kind == LegacySkillKind.Wait;
        public LegacySkillProperty Property { get; }
        public int AttackCount { get; }
        public int LaneIndex { get; }
        public string Description { get; }
        public string AnimationName { get; }
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
        // Level zero rows 1-9 from the original Assets/csv/스킬 수치.csv, defined in LegacySkillDefinitions.
        public static IReadOnlyList<LegacySkill> All => LegacySkillDefinitions.InitialSkills;
    }
}
