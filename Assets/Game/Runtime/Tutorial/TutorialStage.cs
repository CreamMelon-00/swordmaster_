using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Tutorial
{
    public static class TutorialStage
    {
        public const string Name = "연습 숲길 · 첫 결투";

        public static LegacyQueuedDuel CreateDuel(int seed = 1)
        {
            // A short Q lane makes the guided Q -> W -> Q sequence cost exactly three ACT.
            // These are the original immutable skills, not altered tutorial combat rules.
            var playerSkills = new[]
            {
                LegacyInitialSkills.All[0], LegacyInitialSkills.All[6],
                LegacyInitialSkills.All[2], LegacyInitialSkills.All[7],
                LegacyInitialSkills.All[4], LegacyInitialSkills.All[5], LegacyInitialSkills.All[8],
            };
            var slash = new LegacySkill(1001, "연습 베기", 1, 1, 2,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0,
                "훈련용 검으로 가볍게 베어냅니다.", iconId: 10);
            var downwardSlash = new LegacySkill(1002, "연습 내려치기", 1, 1, 2,
                LegacySkillKind.Attack, LegacySkillProperty.Hit, 2, 2,
                "훈련용 검으로 가볍게 내려칩니다.", iconId: 5);
            // The first three slots put a Hit property attack opposite the guided guard.
            // Further turns use one weak attack so experimenting cannot rapidly end the lesson.
            return new LegacyQueuedDuel(100, 50, 32, 12, playerSkills,
                new[] { slash, slash, downwardSlash }, new[] { 3, 1, 1, 1 }, seed);
        }
    }
}
