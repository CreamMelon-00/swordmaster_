namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The curriculum: a three-node trunk (가로베기 → 준비 → 플레슈) that opens three keyword schools, 붕괴 | 순환 |
    /// 판짜기, each an introduction, two twists and a capstone that needs either twist, and two bridge nodes on the capstone
    /// row (바트망 between 붕괴 and 순환, 숫돌 between 순환 and 판짜기), each needing one inner twist from both neighbours. A school's later nodes count or raise the keyword its
    /// introduction carries (Docs/Curriculum.md). Learning a node costs 수련 점수 (<see cref="CurriculumNode.Cost"/>, written on
    /// each node so the author tunes them one by one): the trunk and the introductions 1, the twists 2, the capstones and the
    /// bridges 3, with two additional two-point bridge choices on the seventh row, a six-node bleeding school,
    /// and seven tension nodes: 63 points for all 32. None excludes another,
    /// so every node can be completed. Each node is titled with the current
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
                cost: 1, description: "한 칸에 한 번 공격하는 기본 기술입니다."),
            new CurriculumNode("preparation", null, CurriculumBranch.Foundation, 2.5f, 1, new[] { 10 },
                requiresAll: new[] { "horizontal-cut" },
                cost: 1, description: "다음 칸에 사용할 기술의 위력을 높입니다."),
            new CurriculumNode("advance", null, CurriculumBranch.Foundation, 2.5f, 2, new[] { 12 },
                requiresAll: new[] { "preparation" },
                cost: 1, description: "다음 턴 ACT 회복량이 늘지만 취약 상태가 됩니다. 마치면 세 학과가 열립니다."),

            // The three introductions: each carries its school's keyword.
            new CurriculumNode("quick-draw", null, CurriculumBranch.Collapse, .5f, 3, new[] { 42 },
                requiresAll: new[] { "advance" },
                cost: 1, description: "붕괴 입문: 방어하는 상대의 저항을 깎습니다."),
            new CurriculumNode("diagonal-cut", null, CurriculumBranch.Circulation, 2.5f, 3, new[] { 15 },
                requiresAll: new[] { "advance" },
                cost: 1, description: "순환 입문: 같은 전투에서 사용할수록 위력이 오릅니다."),
            new CurriculumNode("suppleness", null, CurriculumBranch.Retraction, 4.5f, 3, new[] { 32 },
                requiresAll: new[] { "advance" },
                cost: 1, description: "판짜기 입문: 앞에 예약한 기술을 모두 반환합니다."),

            // The twists, two per school.
            new CurriculumNode("fighting-spirit", null, CurriculumBranch.Collapse, 0f, 4, new[] { 19 },
                requiresAll: new[] { "quick-draw" },
                cost: 2, description: "상대가 붕괴 상태이면 내 저항을 회복합니다."),
            new CurriculumNode("esquive", null, CurriculumBranch.Collapse, 1f, 4, new[] { 54 },
                requiresAll: new[] { "quick-draw" },
                cost: 2, description: "피하면서 치면 상대만 저항을 잃습니다."),
            new CurriculumNode("vital-thrust", null, CurriculumBranch.Circulation, 2f, 4, new[] { 21 },
                requiresAll: new[] { "diagonal-cut" },
                cost: 2, description: "순환이 쌓일수록 찌르기의 위력이 오릅니다."),
            new CurriculumNode("quick-steps", null, CurriculumBranch.Circulation, 3f, 4, new[] { 57 },
                requiresAll: new[] { "diagonal-cut" },
                cost: 2, description: "앞에 예약한 순환 기술의 종류가 많을수록 다음 턴 제한시간이 늘어납니다."),
            new CurriculumNode("breathing", null, CurriculumBranch.Retraction, 4f, 4, new[] { 17 },
                requiresAll: new[] { "suppleness" },
                cost: 2, description: "막고 나서 숨을 고릅니다."),
            new CurriculumNode("contretemps", null, CurriculumBranch.Retraction, 5f, 4, new[] { 55 },
                requiresAll: new[] { "suppleness" },
                cost: 2, description: "앞에 예약한 숨고르기가 많을수록 위력이 오릅니다."),

            // The capstone row: three capstones (either twist opens one) and the bridges between 붕괴 and 순환 and between
            // 순환 and 판짜기.
            new CurriculumNode("estocada", null, CurriculumBranch.Collapse, .5f, 5, new[] { 51 },
                requiresAny: new[] { "fighting-spirit", "esquive" }, statReward: HealthCapstone,
                cost: 3, description: "붕괴의 정수: 앞에 예약한 저항 감소 기술의 종류가 많을수록 위력이 오릅니다."),
            new CurriculumNode("battement", null, CurriculumBranch.Bridge, 1.5f, 5, new[] { 52 },
                requiresAll: new[] { "esquive", "vital-thrust" },
                cost: 3, description: "붕괴와 순환의 다리: 상대가 공격하면 저항을 낮추며, 순환이 쌓일수록 위력이 오릅니다."),
            new CurriculumNode("cadence", null, CurriculumBranch.Circulation, 2.5f, 5, new[] { 53 },
                requiresAny: new[] { "vital-thrust", "quick-steps" }, statReward: HealthCapstone,
                cost: 3, description: "순환의 정수: 앞에 예약한 순환 기술의 순환 횟수를 늘립니다."),
            new CurriculumNode("whetstone", null, CurriculumBranch.Bridge, 3.5f, 5, new[] { 58 },
                requiresAll: new[] { "quick-steps", "breathing" },
                cost: 3, description: "순환과 판짜기의 다리: 정공 열 맨 앞에 일회성 갈아내기를 추가합니다."),
            new CurriculumNode("doublet", null, CurriculumBranch.Retraction, 4.5f, 5, new[] { 56 },
                requiresAny: new[] { "breathing", "contretemps" }, statReward: TimeCapstone,
                cost: 3, description: "판짜기의 정수: 예약 시 제한시간을 쓰고 앞 기술을 모두 반환합니다. 2개 이상 반환하면 다음 기술 하나의 위력이 2배가 됩니다."),
            // Optional bridges use the existing pool, so either neighbouring route can unlock them.
            new CurriculumNode("pursuit", null, CurriculumBranch.Bridge, 1.5f, 6, new[] { 61 },
                requiresAny: new[] { "quick-draw", "diagonal-cut" },
                cost: 2, description: "앞에 예약한 서로 다른 공격 기술의 총타수가 5 이상이면 기본 위력이 오릅니다."),
            new CurriculumNode("breakthrough", null, CurriculumBranch.Bridge, 3.5f, 6, new[] { 60 },
                requiresAny: new[] { "diagonal-cut", "breathing" },
                cost: 2, description: "자신에게 남은 보호를 모두 소모해 이번 공격의 기본 위력을 높입니다."),

            // Bleeding has two routes: carry your own wounds for tempo, or make the opponent's attacks costly.
            // New nodes only extend the tree; old completed ids and unspent points remain valid at version 2.
            new CurriculumNode("blood-rush", null, CurriculumBranch.Bleeding, 1.5f, 7, new[] { 63 },
                requiresAny: new[] { "pursuit", "breakthrough" },
                cost: 1, description: "강하게 공격하고 다음 턴 ACT를 1 더 회복하지만 자신에게 출혈 2를 부여합니다."),
            new CurriculumNode("wound-cut", null, CurriculumBranch.Bleeding, 4.5f, 7, new[] { 64 },
                requiresAny: new[] { "pursuit", "breakthrough" },
                cost: 1, description: "낮은 위력으로 상대에게 출혈 1을 부여합니다."),
            new CurriculumNode("staunch", null, CurriculumBranch.Bleeding, 1.5f, 8, new[] { 66 },
                requiresAll: new[] { "blood-rush" },
                cost: 2, description: "방어하며 자신의 출혈을 2 줄입니다. 출혈을 남겨 보상을 받을지 정리할지 고릅니다."),
            new CurriculumNode("deep-wound", null, CurriculumBranch.Bleeding, 4.5f, 8, new[] { 65 },
                requiresAll: new[] { "wound-cut" },
                cost: 2, description: "상대에게 출혈 1을 부여하고, 사용 시 상대가 출혈 상태면 대신 출혈 4를 부여합니다."),
            new CurriculumNode("blood-fury", null, CurriculumBranch.Bleeding, 1.5f, 9, new[] { 68 },
                requiresAll: new[] { "staunch" },
                cost: 3, description: "사용 시 자신의 출혈이 3 이상이면 기본 위력이 12 오릅니다."),
            new CurriculumNode("wound-rend", null, CurriculumBranch.Bleeding, 4.5f, 9, new[] { 67 },
                requiresAll: new[] { "deep-wound" },
                cost: 3, description: "사용 시 상대의 출혈이 3 이상이면 기본 위력이 16 오릅니다."),

            // An advanced choice after either bleeding route; original completed ids remain valid at version 2.
            new CurriculumNode("intimidation", null, CurriculumBranch.Tension, 1.5f, 10, new[] { 69 },
                requiresAny: new[] { "blood-fury", "wound-rend" },
                cost: 1, description: "상대에게 긴장 3을 부여해 다음 연타의 타수를 줄입니다."),
            new CurriculumNode("overextended-flurry", null, CurriculumBranch.Tension, 4.5f, 10, new[] { 71 },
                requiresAny: new[] { "blood-fury", "wound-rend" },
                cost: 1, description: "강한 3타 공격 뒤 자신에게 긴장 3을 부여합니다."),
            new CurriculumNode("checking-guard", null, CurriculumBranch.Tension, 1.5f, 11, new[] { 70 },
                requiresAll: new[] { "intimidation" },
                cost: 2, description: "방어하며 상대에게 긴장 2를 부여합니다."),
            new CurriculumNode("composure", null, CurriculumBranch.Tension, 4.5f, 11, new[] { 72 },
                requiresAll: new[] { "overextended-flurry" },
                cost: 2, description: "방어하며 자신의 긴장을 2 줄여 다음 연타를 준비합니다."),
            new CurriculumNode("mental-focus", null, CurriculumBranch.Tension, 3f, 11, new[] { 75 },
                requiresAny: new[] { "intimidation", "overextended-flurry" },
                cost: 2, description: "방어하며 다음 턴 ACT 회복을 3 늘리고 자신에게 긴장 4를 부여합니다."),
            new CurriculumNode("exploit-tension", null, CurriculumBranch.Tension, 1.5f, 12, new[] { 73 },
                requiresAll: new[] { "checking-guard" },
                cost: 3, description: "사용 시 상대에게 긴장이 있으면 기본 위력이 10 오릅니다."),
            new CurriculumNode("steady-strike", null, CurriculumBranch.Tension, 4.5f, 12, new[] { 74 },
                requiresAny: new[] { "composure", "mental-focus" },
                cost: 3, description: "사용 시 자신에게 긴장이 있으면 단타의 기본 위력이 8 오릅니다."),
        });
    }
}
