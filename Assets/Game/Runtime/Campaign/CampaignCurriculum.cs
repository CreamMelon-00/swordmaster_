namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The prototype curriculum: the ten techniques that the shop used to sell, split into three schools.
    /// Every node takes one finished battle for now. Titles, order and exclusive pairs are placeholders.</summary>
    public static class CampaignCurriculum
    {
        public static CurriculumTree Default { get; } = new CurriculumTree(new[]
        {
            // 참격: two cuts, then either the all-in single stroke or the guard-breaking draw.
            new CurriculumNode("horizontal-cut", "가로베기", CurriculumBranch.Slash, .5f, 0, new[] { 14 },
                description: "넓게 휘두르는 한 번의 베기를 익힙니다."),
            new CurriculumNode("diagonal-cut", "사선베기", CurriculumBranch.Slash, .5f, 1, new[] { 15 },
                requiresAll: new[] { "horizontal-cut" }, description: "비스듬히 두 번 베어 내립니다."),
            new CurriculumNode("one-stroke", "일도양단", CurriculumBranch.Slash, 0f, 2, new[] { 16 },
                requiresAll: new[] { "diagonal-cut" }, exclusiveWith: new[] { "quick-draw" },
                description: "위력이 크게 흔들리는 단 한 번의 일격입니다. 발검과 함께 고를 수 없습니다."),
            new CurriculumNode("quick-draw", "발검", CurriculumBranch.Slash, 1f, 2, new[] { 42 },
                requiresAll: new[] { "diagonal-cut" }, exclusiveWith: new[] { "one-stroke" },
                description: "방어하는 상대의 저항을 곧바로 깎습니다. 일도양단과 함께 고를 수 없습니다."),
            // 관통: pressure and a heavy thrust; the follow-up buff opens from either attacking school.
            new CurriculumNode("advance", "전진", CurriculumBranch.Pierce, 2.5f, 0, new[] { 12 },
                description: "밀고 들어가 두 번 찌르고 다음 턴 ACT를 얻지만, 다음 칸에 틈이 생깁니다."),
            new CurriculumNode("vital-thrust", "급소 찌르기", CurriculumBranch.Pierce, 2.5f, 1, new[] { 21 },
                requiresAll: new[] { "advance" }, description: "급소를 노리는 강한 한 번의 찌르기입니다."),
            new CurriculumNode("preparation", "준비", CurriculumBranch.Pierce, 2.5f, 2, new[] { 10 },
                requiresAny: new[] { "diagonal-cut", "vital-thrust" },
                description: "다음 칸의 공격과 방어를 강하게 합니다. 사선베기나 급소 찌르기를 마치면 열립니다."),
            // 수비: a plain guard, then either the flexible guard or resistance recovery.
            new CurriculumNode("breathing", "호흡", CurriculumBranch.Guard, 4.5f, 0, new[] { 17 },
                description: "숨을 고르며 받아 내는 방어입니다."),
            new CurriculumNode("suppleness", "유연함", CurriculumBranch.Guard, 4f, 1, new[] { 32 },
                requiresAll: new[] { "breathing" }, exclusiveWith: new[] { "fighting-spirit" },
                description: "유연하게 흘려 받는 방어입니다. 투지와 함께 고를 수 없습니다."),
            new CurriculumNode("fighting-spirit", "투지", CurriculumBranch.Guard, 5f, 1, new[] { 19 },
                requiresAll: new[] { "breathing" }, exclusiveWith: new[] { "suppleness" },
                description: "막는 동안 저항을 회복합니다. 유연함과 함께 고를 수 없습니다."),
        });
    }
}
