using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>The opening arc before the lobby opens (like StarCraft II's Mar Sara missions): one short,
    /// linear mission per basic rule. Titles, stats and copy are placeholders; the dialogues are stubs.
    /// The missions played from the lobby afterwards are <see cref="LobbyMissions"/>; <see cref="StoryMissions"/>
    /// is the whole chain.</summary>
    public static class PrologueMissions
    {
        public const string EnemySilhouette = "EnemyStudent/Animations/idle/frame-01";
        public const string DummySilhouette = "TrainingDummy/Animations/idle/frame-01";
        private const string ClubRoom = "LobbyRoom/room";
        private const string SchoolGate = "LegacyDuel/Background/pa_background_-_school_in_game";
        private const string Forest = "ForestArena/forest-far";
        private const string Continue = "계속 버튼 · Enter";
        private const string QueueOnce = "Q 짧게 누르기 / 카드 클릭";
        private const string CommitKeys = "Space / Enter / 확정 버튼";
        private const string Watch = "전투를 지켜보세요";
        private const string FreeKeys = "Q 예약 · Shift 넘기기 · Space 확정 · Tab 상대 확인 · Escape 임무 포기";
        /// <summary>The 서막's lane plus 넘기기, which mission 1's win opens.</summary>
        private const CombatFeature QWithCycle = CombatFeature.LaneQ | CombatFeature.Cycle;

        private static readonly LegacySkill Slash = LegacyInitialSkills.All[0];
        private static readonly LegacySkill SharpSlash = LegacyInitialSkills.All[1];
        private static readonly LegacySkill Guard = LegacyInitialSkills.All[6];

        internal static readonly LegacySkill PracticeSlash = new LegacySkill(1001, "연습 베기", 1, 1, 2,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "훈련용 검으로 가볍게 베어냅니다.", iconId: 10);
        internal static readonly LegacySkill PracticeDownwardSlash = new LegacySkill(1002, "연습 내려치기", 1, 1, 2,
            LegacySkillKind.Attack, LegacySkillProperty.Hit, 2, 2, "훈련용 검으로 가볍게 내려칩니다.", iconId: 5);
        internal static readonly LegacySkill PracticeGuard = new LegacySkill(1003, "연습 막기", 1, 2, 3,
            LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 1, "훈련용 검으로 공격을 받아냅니다.", iconId: 7);

        private static readonly PrologueMission[] missions =
        {
            // 1. The dummy never attacks: mashing Q wins in two turns and the hits land on the body.
            new PrologueMission(1, "첫 타격", ClubRoom,
                new[] { "허수아비를 쓰러뜨린다" },
                new[] { new MissionEnemy("허수아비", DummySilhouette, EnemyAppearance.TrainingDummy) },
                24, 10, new[] { LegacyCommonActions.Breathe }, new[] { 1 },
                new[] { Slash, SharpSlash }, false, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "첫 타격",
                        "기술은 바로 쓰지 않고 먼저 순서대로 예약합니다. 이번 상대는 반격하지 않으니 마음껏 베어 보세요.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "Q로 공격을 예약하세요",
                        "Q를 짧게 누르거나 카드를 클릭하면 베기가 ACT 1을 쓰고 첫 순서에 들어갑니다.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "한 번 더 예약하세요",
                        "예약한 기술은 열의 맨 뒤로 돌아가고 다음 기술이 올라옵니다. Q로 예리한 베기를 이어서 예약하세요.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "예약한 순서를 확정하면 전투가 시작됩니다. 확정한 뒤에는 바꿀 수 없습니다.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "지켜보세요",
                        "예약한 순서대로 공격합니다. 상대가 막지 않은 공격은 모두 체력 피해가 됩니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "끝까지 베어 내세요",
                        "예약하고 확정하기를 반복해 허수아비를 쓰러뜨리세요.", "Q 예약 · Space 확정 · Escape 임무 포기"),
                }, unlocks: CombatFeature.Cycle, unlockText: "넘기기(Shift)가 열렸습니다."),
            // 2. The enemy attacks: clashes trade resistance, the break doubles HP damage, ACT recovers each turn.
            new PrologueMission(2, "맞서는 검", SchoolGate,
                new[] { "공격끼리 맞붙어 상대의 저항을 무너뜨린다", "신입생을 쓰러뜨린다" },
                new[] { new MissionEnemy("신입생", EnemySilhouette) },
                36, 12, new[] { PracticeSlash, PracticeSlash, PracticeDownwardSlash }, new[] { 2, 1 },
                new[] { Slash, SharpSlash }, false, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "상대도 공격합니다",
                        "이번 상대는 같은 순번에 공격을 예약합니다. 공격끼리 부딪치면 체력 대신 저항이 먼저 깎입니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Inspect, "상대의 기술을 살펴보세요",
                        "상대 머리 위에도 이번 턴의 기술 큐가 보입니다. Tab을 누르고 있으면 상세 설명이 열립니다.",
                        "Tab 누르고 있기 · ← / → 순번 확인"),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "공격을 예약하세요",
                        "Q로 베기를 예약해 상대의 첫 공격과 같은 순번에 맞세우세요.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "두 번째 순번도 맞세우세요",
                        "Q를 한 번 더 눌러 예리한 베기를 상대의 두 번째 공격에 맞세우세요.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "두 공격 모두 상대와 맞붙습니다. 확정하세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "공격끼리 맞붙습니다",
                        "공격끼리 대결하면 저항이 줄어듭니다. 저항이 무너지면 받는 체력 피해가 2배가 되고, 캐릭터에 붉은 윤곽이 생깁니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Info, "ACT가 회복됐습니다",
                        "턴이 바뀌면 ACT가 3 회복되고, 베기를 쓰면 1 더 회복합니다. 남은 ACT는 다음 턴으로 이어지며 최대 10까지 모입니다.",
                        Continue, focusAct: true),
                    new MissionGuideBeat(MissionGuideStepKind.Cycle, "Shift로 넘기세요",
                        "Shift를 누르면 열의 맨 앞 기술을 쓰지 않고 뒤로 보냅니다. 비용은 없지만, 열이 여럿이면 모든 열이 함께 돌아갑니다. 예리한 베기를 앞으로 가져오세요.",
                        "Shift / 넘기기 버튼"),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "무너진 틈을 노리세요",
                        "필요한 기술을 넘겨 가며 저항이 무너진 상대에게 공격을 몰아 넣으세요.", FreeKeys),
                }, features: QWithCycle),
            // 3. The enemy also guards: defence reduces the same slot's damage; 막기 against a Hit-property attack refunds ACT.
            new PrologueMission(3, "막아내기", Forest,
                new[] { "상대의 내려치기를 막기로 받아낸다", "신입생을 쓰러뜨린다" },
                new[] { new MissionEnemy("신입생", EnemySilhouette) },
                40, 15, new[] { PracticeSlash, PracticeDownwardSlash, PracticeGuard }, new[] { 3, 2 },
                new[] { Slash, Guard, SharpSlash }, false, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "막고, 되갚으세요",
                        "이번 상대는 공격하고, 내려치고, 막기도 합니다. 방어는 같은 순번의 공격 피해를 방어 수치만큼 줄입니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "베기를 예약하세요",
                        "첫 순번은 상대의 베기와 맞붙습니다.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "돌아온 열에서 막기를 고르세요",
                        "베기가 열의 뒤로 돌아가고 막기가 올라왔습니다. 두 번째 순번의 내려치기를 막기로 받아내세요.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "세 번째 순번은 비워 둡니다. 확정해 맞붙으세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "방어는 피해를 줄입니다",
                        "막기가 내려치기의 피해를 줄였습니다. 상대가 방어하는 순번에 공격하면 내 피해도 그만큼 줄어듭니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Info, "막기의 보상",
                        "타격 속성 공격을 막기로 받아내 다음 턴 ACT를 2 더 얻었습니다. 상대의 큐를 보고 막을 자리를 고르세요.",
                        Continue, focusAct: true),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "승리하세요",
                        "공격과 방어를 섞어 상대를 쓰러뜨리세요.", FreeKeys),
                }, features: QWithCycle),
            // 4. No new rule, but the planning timer starts: the arc's first real duel.
            new PrologueMission(4, "마지막 결투", Forest,
                new[] { "제한 시간 안에 기술을 예약한다", "선배를 쓰러뜨린다" },
                new[] { new MissionEnemy("선배", EnemySilhouette) },
                55, 15, new[] { Slash, LegacyInitialSkills.All[4], Guard, SharpSlash }, new[] { 2, 2, 3 },
                new[] { Slash, Guard, SharpSlash }, true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "이제 제한 시간이 흐릅니다",
                        "지금부터는 턴마다 10초 안에 예약을 마쳐야 합니다. 시간이 다 되면 예약한 만큼 자동으로 확정됩니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "마지막 결투",
                        "배운 것을 모두 써서 선배를 꺾으세요.", FreeKeys),
                }, features: QWithCycle),
        };

        public static IReadOnlyList<PrologueMission> All => missions;
        public static int Count => missions.Length;

        public static PrologueMission Get(int number)
        {
            if (number < 1 || number > missions.Length) throw new ArgumentOutOfRangeException(nameof(number));
            return missions[number - 1];
        }
    }
}
