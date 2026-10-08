using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>The story missions played from the lobby after the 서막. Each waits for one cleared stage, teaches one
    /// combat feature and opens it for every later stage (order chosen by the user: E열 → 숨고르기 → 회피 → W열 → 압박).
    /// The next stage stays closed until the mission is won. Stats, enemies and copy are placeholders.</summary>
    public static class LobbyMissions
    {
        public const string Chapter = "가르침";
        private const string Corridor = "SchoolCorridor/corridor-composite";
        private const string Continue = "계속 버튼 · Enter";
        private const string CommitKeys = "Space / Enter / 확정 버튼";
        private const string Watch = "전투를 지켜보세요";

        // Starting skills by lane, by sheet id: Q 베기·연속 베기·막기, W 깊은 찌르기·정교한 찌르기·흘리기,
        // E 기세 꺾기·허점 노리기·쳐내기.
        private static readonly MissionSkill[] LaneQ = { MissionSkill.Table(1), MissionSkill.Table(2), MissionSkill.Table(7) };
        private static readonly MissionSkill[] LaneW = { MissionSkill.Table(3), MissionSkill.Table(4), MissionSkill.Table(8) };
        private static readonly MissionSkill[] LaneE = { MissionSkill.Table(5), MissionSkill.Table(6), MissionSkill.Table(9) };
        private static readonly LegacySkill PracticeSlash = PrologueMissions.PracticeSlash;
        private static readonly LegacySkill PracticeDownwardSlash = PrologueMissions.PracticeDownwardSlash;
        private static readonly LegacySkill PracticeGuard = PrologueMissions.PracticeGuard;

        // 넘기기 opened with the first 서막 mission.
        private const CombatFeature AfterLaneE = CombatFeature.LaneQ | CombatFeature.Cycle | CombatFeature.LaneE;
        private const CombatFeature AfterBreath = AfterLaneE | CombatFeature.Breath;
        private const CombatFeature AfterDodge = AfterBreath | CombatFeature.Dodge;
        private const CombatFeature AfterLaneW = AfterDodge | CombatFeature.LaneW;

        // Copy names sheet techniques with tokens and the enemy's practice skills in words, as in PrologueMissions.
        private static readonly PrologueMission[] missions =
        {
            // 5. E열 (기교): matching the opponent's action can directly reduce resistance. Opens after stage 1. With a second
            // school, 맞물림 (LegacyMeshing) starts here; the free beat mentions it.
            new PrologueMission(5, "기교 검술", Corridor,
                new[] { "상대 공격에 {기술:5:를} 맞춰 저항을 낮춘다", "떠돌이 기사를 쓰러뜨린다", "완료하면 E열이 열린다" },
                new[] { new MissionEnemy("떠돌이 기사", PrologueMissions.EnemySilhouette) },
                50, 15, new MissionSkill[] { PracticeSlash, PracticeGuard, PracticeDownwardSlash }, new[] { 3, 2 },
                Concat(LaneQ, LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "기교 검술",
                        "E열이 열립니다. 기교 검술은 같은 칸의 상대 행동을 읽고 맞추면 추가 효과를 얻습니다. {기술:5:는} 상대 공격에 맞으면 저항을 직접 낮춥니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "E로 예약하세요",
                        "상대 첫 칸은 공격입니다. E를 짧게 누르거나 카드를 클릭해 {기술:5:를} 같은 칸에 예약하세요.", "E 짧게 누르기 / 카드 클릭", lane: 2),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "Q와 E 두 열을 오가며 순서를 짭니다. 확정하세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "기교를 지켜보세요",
                        "상대 공격에 {기술:5:를} 맞추면 저항이 직접 5 줄어듭니다. 기술마다 조건이 다르니 Q나 E를 길게 눌러 설명을 확인하세요.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "두 열로 승리하세요",
                        "이제 넘기기는 열린 열을 모두 함께 한 칸씩 돌립니다. Q와 E를 섞어 떠돌이 기사를 쓰러뜨리세요. 두 열을 번갈아 넣으면 톱니가 맞물려 위력이 오릅니다.",
                        "Q/E 예약 · Shift 넘기기 · Space 확정 · Tab 적 확인\nQ/E 길게 눌러 기술 설명 · Esc 일시정지"),
                },
                AfterLaneE, CombatFeature.LaneE, 1, Chapter, "E열 기교 검술이 열렸습니다. 스테이지에서도 E열을 씁니다."),
            // 6. 숨고르기: skip a slot without ACT so a skill lands where its condition is met. Opens after stage 2.
            new PrologueMission(6, "숨 고르기", Corridor,
                new[] { "숨을 골라 상대의 방어를 흘려보낸다", "떠돌이 기사를 쓰러뜨린다", "완료하면 숨고르기가 열린다" },
                new[] { new MissionEnemy("떠돌이 기사", PrologueMissions.EnemySilhouette) },
                55, 16, new MissionSkill[] { PracticeGuard, PracticeSlash, PracticeDownwardSlash }, new[] { 2, 3 },
                Concat(LaneQ, LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "숨 고르기",
                        "상대가 첫 순번에 막기를 예약했습니다. 막는 칸에 공격하면 위력이 깎입니다. 숨고르기는 ACT 없이 한 칸을 비워 둡니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Breathe, "S로 숨을 고르세요",
                        "S를 누르거나 숨고르기 버튼으로 첫 칸을 비우세요. 턴마다 세 번까지 쓸 수 있습니다.", "S / 숨고르기 버튼"),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "다음 칸에 공격하세요",
                        "첫 칸은 상대 방어, 둘째 칸은 공격입니다. 숨을 고른 뒤 E로 {기술:5:를} 둘째 칸에 넣어 저항 감소 조건을 맞추세요.",
                        "E 짧게 누르기 / 카드 클릭", lane: 2),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "숨을 고른 칸에 상대가 공격하면 그 공격은 막지 못하고 받습니다. 확정하세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "칸을 옮겼습니다",
                        "첫 칸의 방어를 넘기고 둘째 칸 공격에 {기술:5:를} 맞춰 저항을 직접 낮췄습니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "칸을 골라 싸우세요",
                        "숨고르기로 기술이 들어갈 칸을 맞추며 떠돌이 기사를 쓰러뜨리세요.",
                        "Q/E 예약 · Shift 넘기기 · S 숨고르기 · Space 확정\nTab 적 확인 · Q/E 길게 설명 · Esc 일시정지"),
                },
                AfterBreath, CombatFeature.Breath, 2, Chapter, "숨고르기(S)가 열렸습니다."),
            // 7. 회피 (A): even during resolution a timely input matters. Opens after stage 3.
            new PrologueMission(7, "피하는 법", Corridor,
                new[] { "상대의 공격을 피한다", "떠돌이 기사를 쓰러뜨린다", "완료하면 회피가 열린다" },
                new[] { new MissionEnemy("떠돌이 기사", PrologueMissions.EnemySilhouette) },
                55, 16, new MissionSkill[] { PracticeDownwardSlash, PracticeSlash, PracticeDownwardSlash }, new[] { 3, 2 },
                Concat(LaneQ, LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "전투 중에도 움직입니다",
                        "상대 공격이 들어오기 직전, 내 몸의 흰 원이 줄어듭니다. 원이 가장 작을 때 A를 누르면 피합니다. 스텝은 성공해도 빗나가도 다음 턴 ACT 자연 회복이 1로 제한됩니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "빈손으로 확정하세요",
                        "이번 턴은 예약 없이 확정해 상대 공격만 받아 보세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.Dodge, "A로 피하세요",
                        "흰 원이 줄어들 때 A를 누르세요.", "A 회피"),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "피하며 싸우세요",
                        "예약한 기술로 공격하고, 막을 수 없는 공격은 A로 피하세요.",
                        "Q/E 예약 · Shift 넘기기 · S 숨고르기 · Space 확정\nTab 적 확인 · A 회피 · Q/E 길게 설명 · Esc 일시정지"),
                },
                AfterDodge, CombatFeature.Dodge, 3, Chapter, "회피(A)가 열렸습니다."),
            // 8. W열 (강공): costly, powerful skills. Opens after stage 4. With W every lane is open, so its win also opens
            // the curriculum (CampaignRun.IsCurriculumOpen).
            new PrologueMission(8, "강공 검술", Corridor,
                new[] { "강공 검술로 큰 피해를 준다", "떠돌이 기사를 쓰러뜨린다", "완료하면 W열과 커리큘럼이 열린다" },
                new[] { new MissionEnemy("떠돌이 기사", PrologueMissions.EnemySilhouette) },
                70, 18, new MissionSkill[] { PracticeSlash, PracticeDownwardSlash, PracticeGuard, PracticeSlash }, new[] { 3, 3, 2 },
                Concat(Concat(LaneQ, LaneW), LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "강공 검술",
                        "W열이 열립니다. 강공 검술은 ACT를 모아 한 칸에 큰 위력을 싣습니다. 첫 기술은 ACT 2를 쓰는 단타입니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "W로 예약하세요",
                        "W를 짧게 누르거나 카드를 클릭해 {기술:3:를} 예약하세요.", "W 짧게 누르기 / 카드 클릭", lane: 1),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "이제 Q/W/E 세 열을 모두 씁니다. 확정하세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "강공을 지켜보세요",
                        "강공은 한 칸의 위력이 큽니다. ACT를 모으고 상대 큐에서 넣을 자리를 고르세요.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "세 열로 승리하세요",
                        "세 열을 모두 써서 떠돌이 기사를 쓰러뜨리세요.",
                        "Q/W/E 예약 · Shift 넘기기 · S 숨고르기 · Space 확정\nTab 적 확인 · A 회피 · Q/W/E 길게 설명 · Esc 일시정지"),
                },
                AfterLaneW, CombatFeature.LaneW, 4, Chapter, "W열 강공 검술이 열렸습니다. 커리큘럼도 열렸습니다."),
            // 9. 압박 (D): press a strike at the chosen moment for extra damage. Opens after stage 5.
            new PrologueMission(9, "몰아붙이기", Corridor,
                new[] { "내 공격을 밀어붙여 큰 피해를 넣는다", "떠돌이 기사를 쓰러뜨린다", "완료하면 압박이 열린다" },
                new[] { new MissionEnemy("떠돌이 기사", PrologueMissions.EnemySilhouette) },
                80, 18, new MissionSkill[] { PracticeGuard, PracticeSlash, PracticeDownwardSlash, PracticeGuard }, new[] { 3, 3, 2 },
                Concat(Concat(LaneQ, LaneW), LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "원하는 때에 몰아칩니다",
                        "내 공격이 들어가기 직전 흰 원이 줄어들 때 D를 누르면 압박합니다. 성공하면 기술 위력만큼 추가 피해를 줍니다. 스텝은 성공해도 빗나가도 다음 턴 ACT 자연 회복이 1로 제한됩니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "W로 강한 기술을 예약하세요",
                        "압박은 강한 기술에 걸수록 효과가 큽니다. W를 누르세요.", "W 짧게 누르기 / 카드 클릭", lane: 1),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "확정하고 내 공격이 들어갈 때를 기다리세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.Pressure, "D로 압박하세요",
                        "흰 원이 줄어들 때 D를 누르세요.", "D 압박"),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "배운 것을 모두 쓰세요",
                        "예약·숨고르기·회피·압박을 모두 써서 떠돌이 기사를 쓰러뜨리세요.",
                        "Q/W/E 예약 · Shift 넘기기 · S 숨고르기 · Space 확정\nTab 적 확인 · A 회피 · D 압박 · Q/W/E 길게 설명 · Esc 일시정지"),
                },
                CombatFeature.All, CombatFeature.Pressure, 5, Chapter, "압박(D)이 열렸습니다. 이제 모든 기본 기능을 씁니다."),
        };

        public static IReadOnlyList<PrologueMission> All => missions;
        public static int Count => missions.Length;

        private static MissionSkill[] Concat(MissionSkill[] a, MissionSkill[] b)
        {
            var result = new MissionSkill[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }
    }

    /// <summary>The whole linear story chain: the 서막 (<see cref="PrologueMissions"/>) followed by the lobby missions.
    /// Mission numbers are positions in this chain.</summary>
    public static class StoryMissions
    {
        private static readonly PrologueMission[] missions = Build();

        public static IReadOnlyList<PrologueMission> All => missions;
        public static int Count => missions.Length;

        public static PrologueMission Get(int number)
        {
            if (number < 1 || number > missions.Length) throw new ArgumentOutOfRangeException(nameof(number));
            return missions[number - 1];
        }

        private static PrologueMission[] Build()
        {
            var all = new List<PrologueMission>(PrologueMissions.All);
            all.AddRange(LobbyMissions.All);
            for (int index = 0; index < all.Count; index++)
                if (all[index].Number != index + 1) throw new InvalidOperationException("Story missions are numbered in order.");
            return all.ToArray();
        }
    }
}
