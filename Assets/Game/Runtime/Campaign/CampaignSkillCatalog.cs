using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    public static class CampaignSkillCatalog
    {
        // Selected level-zero legacy rows. The first six retain their numeric-only
        // behavior; imported utility skills below have explicit runtime effects.
        private static readonly IReadOnlyList<LegacySkill> acquisitionSkills = Array.AsReadOnly(new[]
        {
            new LegacySkill(14, "가로베기", 1, 6, 12, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                1, 0, "공격력 6~12 / 1회", iconId: 10),
            new LegacySkill(15, "사선베기", 1, 7, 10, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                2, 0, "공격력 7~10 / 2회", iconId: 11),
            new LegacySkill(16, "일도양단", 5, 3, 20, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                1, 1, "공격력 3~20 / 1회", iconId: 12),
            new LegacySkill(17, "호흡", 2, 9, 12, LegacySkillKind.Defence, LegacySkillProperty.Defence,
                1, 0, "방어력 9~12", iconId: 13),
            new LegacySkill(21, "급소 찌르기", 3, 12, 17, LegacySkillKind.Attack, LegacySkillProperty.Penetrate,
                1, 1, "공격력 12~17 / 1회", iconId: 14),
            new LegacySkill(32, "유연함", 2, 8, 12, LegacySkillKind.Defence, LegacySkillProperty.Defence,
                1, 1, "방어력 8~12", iconId: 15),
            new LegacySkill(10, "준비", 1, 2, 3, LegacySkillKind.Attack, LegacySkillProperty.Hit,
                1, 0, "이번 턴의 다음 행동 공격·방어 위력 +30%", iconId: 3),
            new LegacySkill(12, "전진", 1, 4, 8, LegacySkillKind.Attack, LegacySkillProperty.Penetrate,
                2, 2, "다음 턴 ACT 회복 +3 / 이번 턴의 다음 칸 받는 피해 배율 +50%", iconId: 1),
            new LegacySkill(19, "투지", 2, 7, 11, LegacySkillKind.Defence, LegacySkillProperty.Defence,
                1, 2, "시작 시 최대 저항력의 10% 회복 / 최대 저항력까지", iconId: 13),
            new LegacySkill(42, "발검", 1, 4, 8, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                1, 0, "상대 방어 시 저항력 20 직접 감소 / 다음 턴 ACT 회복 +3", iconId: 10),
        });

        public static IReadOnlyList<LegacySkill> AcquisitionSkills => acquisitionSkills;
    }
}
