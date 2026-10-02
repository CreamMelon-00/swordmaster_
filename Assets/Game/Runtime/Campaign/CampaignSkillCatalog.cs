using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    public static class CampaignSkillCatalog
    {
        // The skill sheet's 획득 rows (selected level-zero legacy rows), read live through LegacySkillDefinitions.
        // CampaignCurriculum grants each of them from one node.
        public static IReadOnlyList<LegacySkill> AcquisitionSkills => LegacySkillDefinitions.AcquisitionSkills;
    }
}
