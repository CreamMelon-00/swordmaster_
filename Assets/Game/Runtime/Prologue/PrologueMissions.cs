using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>The opening arc before the lobby opens (like StarCraft II's Mar Sara missions): one short,
    /// linear mission per basic rule, ending in a forced loss to 이아 (mission 4). Titles, stats and coach copy are
    /// placeholders; the scenes come from the author's manuscript (Docs/PrologueManuscript.md).
    /// The missions played from the lobby afterwards are <see cref="LobbyMissions"/>; <see cref="StoryMissions"/>
    /// is the whole chain.</summary>
    public static class PrologueMissions
    {
        public const string EnemySilhouette = "EnemyStudent/Animations/idle/frame-01";
        public const string DummySilhouette = "TrainingDummy/Animations/idle/frame-01";
        // Briefings show where the scene happens: Elisa wakes in the misty forest and meets the knight in it.
        private const string MistForest = "ForestArena/forest-far-mist";
        private const string Forest = "ForestArena/forest-far";
        private const string Continue = "계속 버튼 · Enter";
        private const string QueueOnce = "Q 짧게 누르기 / 카드 클릭";
        private const string CommitKeys = "Space / Enter / 확정 버튼";
        private const string Watch = "전투를 지켜보세요";
        private const string FreeKeys = "Q 예약 · Shift 넘기기 · Space 확정 · Tab 상대 확인 · Escape 임무 포기";
        /// <summary>The 서막's lane plus 넘기기, which mission 1's win opens.</summary>
        private const CombatFeature QWithCycle = CombatFeature.LaneQ | CombatFeature.Cycle;

        // Sheet techniques by id: 베기, 연속 베기, 기세 꺾기, 막기.
        private static readonly MissionSkill Slash = MissionSkill.Table(1);
        private static readonly MissionSkill DoubleSlash = MissionSkill.Table(2);
        private static readonly MissionSkill BreakMomentum = MissionSkill.Table(5);
        private static readonly MissionSkill Guard = MissionSkill.Table(7);
        // 라우다레: 이아's 수훈 technique, an enemy-only (적) sheet row. It breaks the player as it starts and strikes five times.
        private static readonly MissionSkill Laudare = MissionSkill.Table(500);
        // Where the battle of mission 4 pauses for 이아's 수훈.
        private const string EmpowermentScene = "Cutscene/mission-04-event";

        // The practice skills are not sheet rows. Their ids (1001-1003) sit in the range the sheet may not use, since
        // definitions, roles and texts are found by id.
        internal static readonly LegacySkill PracticeSlash = new LegacySkill(LegacySkillSheet.ReservedIdStart + 1, "연습 베기", 1, 1, 2,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "훈련용 검으로 가볍게 베어냅니다.", iconId: 10);
        internal static readonly LegacySkill PracticeDownwardSlash = new LegacySkill(LegacySkillSheet.ReservedIdStart + 2, "연습 내려치기", 1, 1, 2,
            LegacySkillKind.Attack, LegacySkillProperty.Hit, 2, 2, "훈련용 검으로 가볍게 내려칩니다.", iconId: 5);
        internal static readonly LegacySkill PracticeGuard = new LegacySkill(LegacySkillSheet.ReservedIdStart + 3, "연습 막기", 1, 2, 3,
            LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 1, "훈련용 검으로 공격을 받아냅니다.", iconId: 7);

        // Every 서막 mission is a 결투 (the original turn presentation); later missions and stages are 전투.
        // Copy names the player's sheet techniques with {기술:ID:particle} tokens, so a renamed row reaches the coach.
        // The enemy's practice skills (상대의 베기, 내려치기, 막기) and 숨고르기 are not sheet rows and stay written out.
        private static readonly PrologueMission[] missions =
        {
            // 1. The dummy never attacks: mashing Q wins in two turns and the hits land on the body.
            new PrologueMission(1, "처음 쥔 검", MistForest,
                new[] { "허수아비를 쓰러뜨린다" },
                new[] { new MissionEnemy("허수아비", DummySilhouette, EnemyAppearance.TrainingDummy) },
                24, 10, new MissionSkill[] { LegacyCommonActions.Breathe }, new[] { 1 },
                new[] { Slash, DoubleSlash }, false, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "처음 쥔 검",
                        "기술은 바로 쓰지 않고 먼저 순서대로 예약합니다. 이번 상대는 반격하지 않으니 마음껏 베어 보세요.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "Q로 공격을 예약하세요",
                        "Q를 짧게 누르거나 카드를 클릭하면 {기술:1:가} ACT 1을 쓰고 첫 순서에 들어갑니다.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "한 번 더 예약하세요",
                        "예약한 기술은 열의 맨 뒤로 돌아가고 다음 기술이 올라옵니다. Q로 {기술:2:를} 이어서 예약하세요.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "예약한 순서를 확정하면 전투가 시작됩니다. 확정한 뒤에는 바꿀 수 없습니다.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "지켜보세요",
                        "예약한 순서대로 공격합니다. 상대가 막지 않은 공격은 모두 체력 피해가 됩니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "끝까지 베어 내세요",
                        "예약하고 확정하기를 반복해 허수아비를 쓰러뜨리세요.", "Q 예약 · Space 확정 · Escape 임무 포기"),
                }, unlocks: CombatFeature.Cycle, unlockText: "넘기기(Shift)가 열렸습니다.", encounter: EncounterKind.Duel),
            // 2. The enemy attacks: clashes trade resistance, the break doubles HP damage, ACT recovers each turn.
            new PrologueMission(2, "인사는 칼로", Forest,
                new[] { "맞부딪쳐 상대의 저항을 무너뜨린다", "떠돌이 기사를 쓰러뜨린다" },
                new[] { new MissionEnemy("떠돌이 기사", EnemySilhouette) },
                36, 12, new MissionSkill[] { PracticeSlash, PracticeSlash, PracticeDownwardSlash }, new[] { 2, 1 },
                new[] { Slash, DoubleSlash }, false, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "상대도 공격합니다",
                        "이번 상대는 같은 순번에 공격을 예약합니다. 공격끼리 부딪치면 체력 대신 저항이 먼저 깎입니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Inspect, "상대의 기술을 살펴보세요",
                        "상대 머리 위에도 이번 턴의 기술 큐가 보입니다. Tab을 누르고 있으면 상세 설명이 열립니다.",
                        "Tab 누르고 있기 · 좌우 방향키로 순번 확인"),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "공격을 예약하세요",
                        "Q로 {기술:1:를} 예약해 상대의 첫 공격과 같은 순번에 맞세우세요.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "두 번째 순번도 맞세우세요",
                        "Q를 한 번 더 눌러 {기술:2:를} 상대의 두 번째 공격에 맞세우세요.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "두 공격 모두 상대와 맞붙습니다. 확정하세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "공격끼리 맞붙습니다",
                        "공격끼리 대결하면 저항이 줄어듭니다. 저항이 무너지면 받는 체력 피해가 2배가 되고, 캐릭터에 붉은 윤곽이 생깁니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Info, "ACT가 회복됐습니다",
                        "턴이 바뀌면 ACT가 3 회복되고, {기술:1:를} 쓰면 1 더 회복합니다. 남은 ACT는 다음 턴으로 이어지며 최대 10까지 모입니다.",
                        Continue, focusAct: true),
                    new MissionGuideBeat(MissionGuideStepKind.Cycle, "Shift로 넘기세요",
                        "Shift를 누르면 열의 맨 앞 기술을 쓰지 않고 뒤로 보냅니다. ACT는 들지 않지만, 제한 시간이 흐를 때는 한 번에 1초를 씁니다. {기술:2:를} 앞으로 가져오세요.",
                        "Shift / 넘기기 버튼"),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "무너진 틈을 노리세요",
                        "필요한 기술을 넘겨 가며 저항이 무너진 상대에게 공격을 몰아 넣으세요.", FreeKeys),
                }, features: QWithCycle, encounter: EncounterKind.Duel),
            // 3. The enemy also guards: defence reduces the same slot's damage; 막기 against a Hit-property attack refunds ACT.
            new PrologueMission(3, "받아내는 법", Forest,
                new[] { "상대의 내려치기를 {기술:7:로} 받아낸다", "떠돌이 기사를 쓰러뜨린다" },
                new[] { new MissionEnemy("떠돌이 기사", EnemySilhouette) },
                40, 15, new MissionSkill[] { PracticeSlash, PracticeDownwardSlash, PracticeGuard }, new[] { 3, 2 },
                new[] { Slash, Guard, DoubleSlash }, false, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "막고, 되갚으세요",
                        "이번 상대는 공격하고, 내려치고, 막기도 합니다. 방어는 같은 순번의 공격 피해를 방어 수치만큼 줄입니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "{기술:1:를} 예약하세요",
                        "첫 순번은 상대의 베기와 맞붙습니다.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "돌아온 열에서 {기술:7:를} 고르세요",
                        "{기술:1:가} 열의 뒤로 돌아가고 {기술:7:가} 올라왔습니다. 두 번째 순번의 내려치기를 {기술:7:로} 받아내세요.", QueueOnce, lane: 0),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "세 번째 순번은 비워 둡니다. 확정해 맞붙으세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "방어는 피해를 줄입니다",
                        "{기술:7:가} 내려치기의 피해를 줄였습니다. 상대가 방어하는 순번에 공격하면 내 피해도 그만큼 줄어듭니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Info, "{기술:7}의 보상",
                        "타격 속성 공격을 {기술:7:로} 받아내 다음 턴 ACT를 2 더 얻었습니다. 상대의 큐를 보고 막을 자리를 고르세요.",
                        Continue, focusAct: true),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "승리하세요",
                        "공격과 방어를 섞어 상대를 쓰러뜨리세요.", FreeKeys),
                }, features: QWithCycle, encounter: EncounterKind.Duel),
            // 4. No new rule, but the planning timer starts: the arc's first real duel, which the player cannot win. The
            // briefing still says 떠돌이 기사; she names herself 이아 in the intro, so the battle says 이아. She cannot
            // fall (health floor 1). The hit that brings her to half health pauses the battle for her 수훈; from the
            // next turn she uses 라우다레 every turn (at least 50 health damage to the broken player, so two turns from
            // full health), and the player's defeat then completes the 서막 with the outro instead of a result screen.
            // So the briefing and the coach ask the player to fight her to the end, never to win (or that it is lost).
            new PrologueMission(4, "떠돌이 기사", Forest,
                new[] { "제한 시간 안에 기술을 예약한다", "떠돌이 기사와 끝까지 겨룬다" },
                new[] { new MissionEnemy("떠돌이 기사", EnemySilhouette) },
                55, 15, new[] { Slash, BreakMomentum, Guard, DoubleSlash }, new[] { 2, 2, 3 },
                new[] { Slash, Guard, DoubleSlash }, true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "이제 제한 시간이 흐릅니다",
                        "지금부터는 턴마다 10초 안에 예약을 마쳐야 합니다. 시간이 다 되면 예약한 만큼 자동으로 확정됩니다. 넘기기도 한 번에 1초를 씁니다.", Continue),
                    // The coach speaks during the battle, so it already says 이아.
                    new MissionGuideBeat(MissionGuideStepKind.Free, "이아",
                        "배운 것을 모두 써서 이아와 겨루세요.", FreeKeys),
                }, features: QWithCycle, encounter: EncounterKind.Duel, battleEnemyName: "이아", enemyHealthFloor: 1,
                empowerment: new MissionEmpowerment(50, EmpowermentScene, new[] { new[] { Laudare } },
                    keepsAura: true, forcedLoss: true)),
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
