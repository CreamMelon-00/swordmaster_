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
        // Level zero rows 1-9 from the original Assets/csv/스킬 수치.csv.
        private static readonly IReadOnlyList<LegacySkill> skills = Array.AsReadOnly(new[]
        {
            new LegacySkill(1, "베기", 1, 4, 5, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "다음 턴 ACT 회복량 +1"),
            new LegacySkill(2, "예리한 베기", 2, 11, 15, LegacySkillKind.Attack, LegacySkillProperty.Slash, 2, 0, "칼을 휘둘러 상대의 약점을 베어냅니다."),
            new LegacySkill(3, "찌르기", 1, 5, 5, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 3, 1, "다음 기술 3개의 피해량 +10%"),
            new LegacySkill(4, "정교한 찌르기", 2, 11, 15, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 1, 1, "칼로 상대의 약한 부위를 꿰뚫습니다."),
            new LegacySkill(5, "부수기", 1, 6, 9, LegacySkillKind.Attack, LegacySkillProperty.Hit, 2, 2, "상대를 내리쳐 자세를 흐트러뜨립니다."),
            new LegacySkill(6, "강력한 부수기", 3, 16, 20, LegacySkillKind.Attack, LegacySkillProperty.Hit, 3, 2, "상대를 내려쳐 큰 피해를 줍니다."),
            new LegacySkill(7, "막기", 1, 5, 8, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "상대 타격 기술에 대응하면 다음 턴 ACT 회복량 +2"),
            new LegacySkill(8, "흘리기", 1, 7, 11, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 1, "이 페이즈의 이후 기술에서 받는 피해량 -30%"),
            new LegacySkill(9, "쳐내기", 1, 4, 6, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 2, "이 페이즈의 이후 기술 위력 +3%"),
        });

        public static IReadOnlyList<LegacySkill> All => skills;
    }
}
