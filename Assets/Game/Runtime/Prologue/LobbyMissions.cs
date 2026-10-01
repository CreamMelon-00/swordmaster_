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
        public const string Chapter = "수련";
        private const string SchoolGate = "LegacyDuel/Background/pa_background_-_school_in_game";
        private const string Forest = "ForestArena/forest-far";
        private const string Continue = "계속 버튼 · Enter";
        private const string CommitKeys = "Space / Enter / 확정 버튼";
        private const string Watch = "전투를 지켜보세요";

        private static readonly IReadOnlyList<LegacySkill> Starting = LegacyInitialSkills.All;
        // Starting skills by lane: Q 베기·예리한 베기·막기, W 찌르기·정교한 찌르기·흘리기, E 부수기·강력한 부수기·쳐내기.
        private static readonly LegacySkill[] LaneQ = { Starting[0], Starting[1], Starting[6] };
        private static readonly LegacySkill[] LaneW = { Starting[2], Starting[3], Starting[7] };
        private static readonly LegacySkill[] LaneE = { Starting[4], Starting[5], Starting[8] };
        private static readonly LegacySkill PracticeSlash = PrologueMissions.PracticeSlash;
        private static readonly LegacySkill PracticeDownwardSlash = PrologueMissions.PracticeDownwardSlash;
        private static readonly LegacySkill PracticeGuard = PrologueMissions.PracticeGuard;

        private const CombatFeature AfterLaneE = CombatFeature.LaneQ | CombatFeature.LaneE;
        private const CombatFeature AfterBreath = AfterLaneE | CombatFeature.Breath;
        private const CombatFeature AfterDodge = AfterBreath | CombatFeature.Dodge;
        private const CombatFeature AfterLaneW = AfterDodge | CombatFeature.LaneW;

        private static readonly PrologueMission[] missions =
        {
            // 5. E열 (기교): conditional skills that shake the opponent or help the player. Opens after stage 1.
            new PrologueMission(5, "기교 검술", SchoolGate,
                new[] { "E열 기교 검술을 예약해 상대를 흔든다", "선배를 쓰러뜨린다", "완료하면 E열이 열린다" },
                new[] { new MissionEnemy("선배", PrologueMissions.EnemySilhouette) },
                50, 15, new[] { PracticeSlash, PracticeGuard, PracticeDownwardSlash }, new[] { 3, 2 },
                Concat(LaneQ, LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "기교 검술",
                        "E열이 열립니다. 기교 검술은 조건이 맞을 때 상대를 흔들거나 나에게 이로운 효과를 주는 기술입니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "E로 예약하세요",
                        "E를 짧게 누르거나 카드를 클릭해 부수기를 예약하세요.", "E 짧게 누르기 / 카드 클릭", lane: 2),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "Q와 E 두 열을 오가며 순서를 짭니다. 확정하세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "기교를 지켜보세요",
                        "기술마다 효과가 붙는 조건이 다릅니다. Q나 E를 길게 누르면 설명을 볼 수 있습니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "두 열로 승리하세요",
                        "Q와 E를 섞어 선배를 쓰러뜨리세요.", "Q/E 예약 · Space 확정 · Tab 상대 확인 · Escape 임무 포기"),
                },
                AfterLaneE, CombatFeature.LaneE, 1, Chapter, "E열 기교 검술이 열렸습니다. 스테이지에서도 E열을 씁니다."),
            // 6. 숨고르기: skip a slot without ACT so a skill lands where its condition is met. Opens after stage 2.
            new PrologueMission(6, "숨 고르기", Forest,
                new[] { "숨고르기로 상대의 방어를 흘려보낸다", "선배를 쓰러뜨린다", "완료하면 숨고르기가 열린다" },
                new[] { new MissionEnemy("선배", PrologueMissions.EnemySilhouette) },
                55, 16, new[] { PracticeGuard, PracticeSlash, PracticeDownwardSlash }, new[] { 2, 3 },
                Concat(LaneQ, LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "숨 고르기",
                        "상대가 첫 순번에 막기를 예약했습니다. 막는 칸에 공격하면 위력이 깎입니다. 숨고르기는 ACT 없이 한 칸을 비워 둡니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Breathe, "S로 숨을 고르세요",
                        "S를 누르거나 숨고르기 버튼으로 첫 칸을 비우세요. 턴마다 세 번까지 쓸 수 있습니다.", "S / 숨고르기 버튼"),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "다음 칸에 공격하세요",
                        "숨을 고른 뒤 E로 부수기를 두 번째 칸에 넣으세요. 조건이 맞는 칸에 기술을 옮기는 방법입니다.",
                        "E 짧게 누르기 / 카드 클릭", lane: 2),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "숨을 고른 칸에 상대가 공격하면 그 공격은 막지 못하고 받습니다. 확정하세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "칸을 옮겼습니다",
                        "비운 칸 다음에 들어간 기술이 상대의 방어를 피해 들어갑니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "칸을 골라 싸우세요",
                        "숨고르기로 기술이 들어갈 칸을 맞추며 선배를 쓰러뜨리세요.",
                        "Q/E 예약 · S 숨고르기 · Space 확정 · Escape 임무 포기"),
                },
                AfterBreath, CombatFeature.Breath, 2, Chapter, "숨고르기(S)가 열렸습니다."),
            // 7. 회피 (A): even during resolution a timely input matters. Opens after stage 3.
            new PrologueMission(7, "회피", Forest,
                new[] { "A로 상대의 공격을 피한다", "선배를 쓰러뜨린다", "완료하면 회피가 열린다" },
                new[] { new MissionEnemy("선배", PrologueMissions.EnemySilhouette) },
                55, 16, new[] { PracticeDownwardSlash, PracticeSlash, PracticeDownwardSlash }, new[] { 3, 2 },
                Concat(LaneQ, LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "전투 중에도 움직입니다",
                        "상대 공격이 들어오기 직전, 내 몸의 흰 원이 줄어듭니다. 원이 가장 작을 때 A를 누르면 피합니다. 빗나가면 다음 턴 ACT 회복이 줄어듭니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "빈손으로 확정하세요",
                        "이번 턴은 예약 없이 확정해 상대 공격만 받아 보세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.Dodge, "A로 피하세요",
                        "흰 원이 줄어들 때 A를 누르세요.", "A 회피"),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "피하며 싸우세요",
                        "예약한 기술로 공격하고, 막을 수 없는 공격은 A로 피하세요.",
                        "Q/E 예약 · S 숨고르기 · Space 확정 · A 회피 · Escape 임무 포기"),
                },
                AfterDodge, CombatFeature.Dodge, 3, Chapter, "회피(A)가 열렸습니다."),
            // 8. W열 (강공): costly, powerful skills. Opens after stage 4.
            new PrologueMission(8, "강공 검술", SchoolGate,
                new[] { "W열 강공 검술로 큰 피해를 준다", "선배를 쓰러뜨린다", "완료하면 W열이 열린다" },
                new[] { new MissionEnemy("선배", PrologueMissions.EnemySilhouette) },
                70, 18, new[] { PracticeSlash, PracticeDownwardSlash, PracticeGuard, PracticeSlash }, new[] { 3, 3, 2 },
                Concat(Concat(LaneQ, LaneW), LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "강공 검술",
                        "W열이 열립니다. 강공 검술은 ACT를 많이 쓰는 대신 강한 기술입니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "W로 예약하세요",
                        "W를 짧게 누르거나 카드를 클릭해 찌르기를 예약하세요.", "W 짧게 누르기 / 카드 클릭", lane: 1),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "이제 Q/W/E 세 열을 모두 씁니다. 확정하세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.WatchTurn, "강공을 지켜보세요",
                        "강한 기술일수록 ACT를 아껴 두었다가 넣을 자리를 고르는 것이 중요합니다.", Watch),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "세 열로 승리하세요",
                        "세 열을 모두 써서 선배를 쓰러뜨리세요.",
                        "Q/W/E 예약 · S 숨고르기 · Space 확정 · A 회피 · Escape 임무 포기"),
                },
                AfterLaneW, CombatFeature.LaneW, 4, Chapter, "W열 강공 검술이 열렸습니다."),
            // 9. 압박 (D): press a strike at the chosen moment for extra damage. Opens after stage 5.
            new PrologueMission(9, "압박", Forest,
                new[] { "D로 내 공격을 밀어붙여 큰 피해를 넣는다", "선배를 쓰러뜨린다", "완료하면 압박이 열린다" },
                new[] { new MissionEnemy("선배", PrologueMissions.EnemySilhouette) },
                80, 18, new[] { PracticeGuard, PracticeSlash, PracticeDownwardSlash, PracticeGuard }, new[] { 3, 3, 2 },
                Concat(Concat(LaneQ, LaneW), LaneE), true, new[]
                {
                    new MissionGuideBeat(MissionGuideStepKind.Info, "원하는 때에 몰아칩니다",
                        "내 공격이 들어가기 직전 흰 원이 줄어들 때 D를 누르면 압박합니다. 성공하면 기술 위력만큼 추가 피해를 줍니다. 빗나가면 다음 턴 ACT 회복이 줄어듭니다.", Continue),
                    new MissionGuideBeat(MissionGuideStepKind.Queue, "W로 강한 기술을 예약하세요",
                        "압박은 강한 기술에 걸수록 효과가 큽니다. W를 누르세요.", "W 짧게 누르기 / 카드 클릭", lane: 1),
                    new MissionGuideBeat(MissionGuideStepKind.Commit, "확정하세요",
                        "확정하고 내 공격이 들어갈 때를 기다리세요.", CommitKeys),
                    new MissionGuideBeat(MissionGuideStepKind.Pressure, "D로 압박하세요",
                        "흰 원이 줄어들 때 D를 누르세요.", "D 압박"),
                    new MissionGuideBeat(MissionGuideStepKind.Free, "배운 것을 모두 쓰세요",
                        "예약·숨고르기·회피·압박을 모두 써서 선배를 쓰러뜨리세요.",
                        "Q/W/E 예약 · S 숨고르기 · Space 확정 · A 회피 · D 압박 · Escape 임무 포기"),
                },
                CombatFeature.All, CombatFeature.Pressure, 5, Chapter, "압박(D)이 열렸습니다. 이제 모든 기본 기능을 씁니다."),
        };

        public static IReadOnlyList<PrologueMission> All => missions;
        public static int Count => missions.Length;

        private static LegacySkill[] Concat(LegacySkill[] a, LegacySkill[] b)
        {
            var result = new LegacySkill[a.Length + b.Length];
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
