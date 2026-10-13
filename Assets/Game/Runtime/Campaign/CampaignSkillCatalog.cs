using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    public static class CampaignSkillCatalog
    {
        // The skill sheet's 획득 rows (selected level-zero legacy rows), read live through LegacySkillDefinitions.
        // Stage first clears, CampaignCurriculum and CampaignFieldPickups grant the acquisition rows.
        public static IReadOnlyList<LegacySkill> AcquisitionSkills => LegacySkillDefinitions.AcquisitionSkills;
    }
}
