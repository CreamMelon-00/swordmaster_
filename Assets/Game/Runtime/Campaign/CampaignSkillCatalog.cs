using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    public static class CampaignSkillCatalog
    {
        // Selected level-zero legacy rows, defined with their effects and copy in LegacySkillDefinitions.
        public static IReadOnlyList<LegacySkill> AcquisitionSkills => LegacySkillDefinitions.AcquisitionSkills;
    }
}
