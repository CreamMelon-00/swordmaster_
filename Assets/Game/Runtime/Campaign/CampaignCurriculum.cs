namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The prototype curriculum: the ten techniques that the shop used to sell, split into three schools.
    /// Every node takes one finished battle for now. Each node is titled with the current sheet name of the technique
    /// it grants (a null title), and descriptions name other techniques with {기술:ID} tokens, so a renamed row reaches
    /// the tree. Order and exclusive pairs are placeholders.</summary>
    public static class CampaignCurriculum
    {
        public static CurriculumTree Default { get; } = new CurriculumTree(new[]
        {
            // 참격: two cuts, then either the all-in single stroke or the guard-breaking 쿠페.
            new CurriculumNode("horizontal-cut", null, CurriculumBranch.Slash, .5f, 0, new[] { 14 },
                description: "넓게 휘두르는 한 번의 베기를 익힙니다."),
            new CurriculumNode("diagonal-cut", null, CurriculumBranch.Slash, .5f, 1, new[] { 15 },
                requiresAll: new[] { "horizontal-cut" }, description: "비스듬히 두 번 베어 내립니다."),
            new CurriculumNode("one-stroke", null, CurriculumBranch.Slash, 0f, 2, new[] { 16 },
                requiresAll: new[] { "diagonal-cut" }, exclusiveWith: new[] { "quick-draw" },
                description: "위력이 크게 흔들리는 단 한 번의 일격입니다. {기술:42:와} 함께 고를 수 없습니다."),
            new CurriculumNode("quick-draw", null, CurriculumBranch.Slash, 1f, 2, new[] { 42 },
                requiresAll: new[] { "diagonal-cut" }, exclusiveWith: new[] { "one-stroke" },
                description: "방어하는 상대의 저항을 곧바로 깎습니다. {기술:16:와} 함께 고를 수 없습니다."),
            // 관통: pressure and a heavy thrust; the follow-up buff opens from either attacking school.
            new CurriculumNode("advance", null, CurriculumBranch.Pierce, 2.5f, 0, new[] { 12 },
                description: "밀고 들어가 두 번 찌르고 다음 턴 ACT를 얻지만, 다음 칸에 틈이 생깁니다."),
            new CurriculumNode("vital-thrust", null, CurriculumBranch.Pierce, 2.5f, 1, new[] { 21 },
                requiresAll: new[] { "advance" }, description: "급소를 노리는 강한 한 번의 찌르기입니다."),
            new CurriculumNode("preparation", null, CurriculumBranch.Pierce, 2.5f, 2, new[] { 10 },
                requiresAny: new[] { "diagonal-cut", "vital-thrust" },
                description: "다음 칸의 공격과 방어를 강하게 합니다. {기술:15:나} {기술:21:를} 마치면 열립니다."),
            // 수비: a plain guard, then either the flexible guard or resistance recovery.
            new CurriculumNode("breathing", null, CurriculumBranch.Guard, 4.5f, 0, new[] { 17 },
                description: "숨을 고르며 받아 내는 방어입니다."),
            new CurriculumNode("suppleness", null, CurriculumBranch.Guard, 4f, 1, new[] { 32 },
                requiresAll: new[] { "breathing" }, exclusiveWith: new[] { "fighting-spirit" },
                description: "유연하게 흘려 받는 방어입니다. {기술:19:와} 함께 고를 수 없습니다."),
            new CurriculumNode("fighting-spirit", null, CurriculumBranch.Guard, 5f, 1, new[] { 19 },
                requiresAll: new[] { "breathing" }, exclusiveWith: new[] { "suppleness" },
                description: "막는 동안 저항을 회복합니다. {기술:32:와} 함께 고를 수 없습니다."),
        });
    }
}
