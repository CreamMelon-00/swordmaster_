namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The curriculum: a three-node trunk (가로베기 → 준비 → 플레슈) that opens three keyword schools, 붕괴 | 순환 |
    /// 판짜기, each an introduction, two twists and a capstone that needs either twist, and two bridge nodes on the capstone
    /// row (바트망 between 붕괴 and 순환, 숫돌 between 순환 and 판짜기), each needing one inner twist from both neighbours. A school's later nodes count or raise the keyword its
    /// introduction carries (Docs/Curriculum.md). Learning a node costs 수련 점수 (<see cref="CurriculumNode.Cost"/>, written on
    /// each node so the author tunes them one by one): the trunk and the introductions 1, the twists 2, the capstones and the
    /// bridges 3, 33 for all 17. None excludes another, so every node can be completed. Each node is titled with the current
    /// sheet name of the technique it grants (a null title), and descriptions name other techniques with {기술:ID} tokens, so
    /// a renamed row reaches the tree. 16 알티바호 is stage 7's first-clear reward instead
    /// (<see cref="CampaignStage.FirstClearSkillId"/>).</summary>
    public static class CampaignCurriculum
    {
        /// <summary>Written into every save (<c>curriculum-tree N</c>). A save that records another version, or none (an
        /// older one, read as 1), keeps everything but its curriculum progress (<see cref="CampaignSave.ForCurriculumTree"/>).
        /// Raise it whenever the default tree changes in a way an older save could break: a node is deleted, a node's
        /// prerequisites narrow, or the technique a node grants changes. Adding nodes, widening a requirement or changing a
        /// cost keeps it (a save holds the completed ids and the unspent balance, and either stays valid).</summary>
        public const int TreeVersion = 2;

        /// <summary>The 붕괴 and 순환 capstones' bonus.</summary>
        private static readonly CurriculumStatReward HealthCapstone = new CurriculumStatReward(health: 5);
        /// <summary>The 판짜기 capstone's bonus ("판을 먼저 읽어 상대가 늦게 다가온다").</summary>
        private static readonly CurriculumStatReward TimeCapstone = new CurriculumStatReward(planningSeconds: 2);

        public static CurriculumTree Default { get; } = new CurriculumTree(new[]
        {
            // 기초: the trunk. Each step reaches further: this slot, the next slot, the next turn.
            new CurriculumNode("horizontal-cut", null, CurriculumBranch.Foundation, 2.5f, 0, new[] { 14 },
                cost: 1, description: "한 칸에 한 번, 군더더기 없이 벱니다. 모든 과정의 시작입니다."),
            new CurriculumNode("preparation", null, CurriculumBranch.Foundation, 2.5f, 1, new[] { 10 },
                requiresAll: new[] { "horizontal-cut" },
                cost: 1, description: "처음으로 기술이 자기 칸 밖, 다음 칸에 닿습니다."),
            new CurriculumNode("advance", null, CurriculumBranch.Foundation, 2.5f, 2, new[] { 12 },
                requiresAll: new[] { "preparation" },
                cost: 1, description: "다음 턴을 당겨 쓰고 틈을 내줍니다. 마치면 세 학과가 열립니다."),

            // The three introductions: each carries its school's keyword.
            new CurriculumNode("quick-draw", null, CurriculumBranch.Collapse, .5f, 3, new[] { 42 },
                requiresAll: new[] { "advance" },
                cost: 1, description: "붕괴 입문: 방어하는 상대의 저항을 깎습니다."),
            new CurriculumNode("diagonal-cut", null, CurriculumBranch.Circulation, 2.5f, 3, new[] { 15 },
                requiresAll: new[] { "advance" },
                cost: 1, description: "순환 입문: 같은 전투에서 쓸수록 손에 익습니다."),
            new CurriculumNode("suppleness", null, CurriculumBranch.Retraction, 4.5f, 3, new[] { 32 },
                requiresAll: new[] { "advance" },
                cost: 1, description: "판짜기 입문: 짜 둔 수를 반환하고 다시 짭니다."),

            // The twists, two per school.
            new CurriculumNode("fighting-spirit", null, CurriculumBranch.Collapse, 0f, 4, new[] { 19 },
                requiresAll: new[] { "quick-draw" },
                cost: 2, description: "상대가 무너진 틈에 내 자세를 되찾습니다."),
            new CurriculumNode("esquive", null, CurriculumBranch.Collapse, 1f, 4, new[] { 54 },
                requiresAll: new[] { "quick-draw" },
                cost: 2, description: "피하면서 치면 상대만 저항을 잃습니다."),
            new CurriculumNode("vital-thrust", null, CurriculumBranch.Circulation, 2f, 4, new[] { 21 },
                requiresAll: new[] { "diagonal-cut" },
                cost: 2, description: "찌를수록 급소에 가까워집니다."),
            new CurriculumNode("quick-steps", null, CurriculumBranch.Circulation, 3f, 4, new[] { 57 },
                requiresAll: new[] { "diagonal-cut" },
                cost: 2, description: "익은 동작이 이어지면 발이 빨라져 상대가 늦게 닿습니다."),
            new CurriculumNode("breathing", null, CurriculumBranch.Retraction, 4f, 4, new[] { 17 },
                requiresAll: new[] { "suppleness" },
                cost: 2, description: "막고 나서 숨을 고릅니다."),
            new CurriculumNode("contretemps", null, CurriculumBranch.Retraction, 5f, 4, new[] { 55 },
                requiresAll: new[] { "suppleness" },
                cost: 2, description: "고른 숨만큼, 한 박자 늦게 더 세게 칩니다."),

            // The capstone row: three capstones (either twist opens one) and the bridges between 붕괴 and 순환 and between
            // 순환 and 판짜기.
            new CurriculumNode("estocada", null, CurriculumBranch.Collapse, .5f, 5, new[] { 51 },
                requiresAny: new[] { "fighting-spirit", "esquive" }, statReward: HealthCapstone,
                cost: 3, description: "붕괴의 정수: 깎아 둔 만큼 깊이 꿰뚫습니다."),
            new CurriculumNode("battement", null, CurriculumBranch.Bridge, 1.5f, 5, new[] { 52 },
                requiresAll: new[] { "esquive", "vital-thrust" },
                cost: 3, description: "붕괴와 순환의 다리: 상대 칼을 두드릴수록 손에 익습니다."),
            new CurriculumNode("cadence", null, CurriculumBranch.Circulation, 2.5f, 5, new[] { 53 },
                requiresAny: new[] { "vital-thrust", "quick-steps" }, statReward: HealthCapstone,
                cost: 3, description: "순환의 정수: 박자를 맞추면 앞의 동작이 한 번 더 손에 익습니다."),
            new CurriculumNode("whetstone", null, CurriculumBranch.Bridge, 3.5f, 5, new[] { 58 },
                requiresAll: new[] { "quick-steps", "breathing" },
                cost: 3, description: "순환과 판짜기의 다리: 정공 열에 일회성 갈아내기를 세웁니다."),
            new CurriculumNode("doublet", null, CurriculumBranch.Retraction, 4.5f, 5, new[] { 56 },
                requiresAny: new[] { "breathing", "contretemps" }, statReward: TimeCapstone,
                cost: 3, description: "판짜기의 정수: 보여 준 수를 거두고 진짜 한 수를 두 배로. 그 사이 상대가 다가옵니다."),
        });
    }
}
