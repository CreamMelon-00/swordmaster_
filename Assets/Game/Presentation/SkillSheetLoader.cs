using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Points the skill table at the sheet in Resources (<see cref="LegacySkillDefinitions.SheetResourcePath"/>)
    /// before anything reads it. Installing also forgets the table read in an earlier Play session, which survives while
    /// domain reload is off, so every Play reads the sheet as it is now.</summary>
    public static class SkillSheetLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install() => LegacySkillDefinitions.Install(ReadSheet);

        // Reads the table once before the first scene so sheet problems head the Console instead of surfacing
        // wherever a skill is first needed. The game still throws the same error there. A sheet that reads but does not
        // fit the code (CampaignSheetCheck) is reported here too.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Check()
        {
            LegacySkillTable table;
            try
            {
                table = LegacySkillDefinitions.Table;
            }
            catch (Exception exception)
            {
                Debug.LogError(exception.Message);
                return;
            }
            IReadOnlyList<string> problems = CampaignSheetCheck.Problems(table);
            if (problems.Count > 0) Debug.LogError(CampaignSheetCheck.ProblemMessage(table.SheetId, problems));
            IReadOnlyList<string> warnings = CampaignSheetCheck.Warnings(table);
            if (warnings.Count > 0) Debug.LogWarning(CampaignSheetCheck.WarningMessage(table.SheetId, warnings));
        }

        private static string ReadSheet()
        {
            var sheet = Resources.Load<TextAsset>(LegacySkillDefinitions.SheetResourcePath);
            if (sheet == null)
                throw new InvalidOperationException(
                    $"기술 시트를 찾지 못했습니다: Resources/{LegacySkillDefinitions.SheetResourcePath} ({LegacySkillDefinitions.SheetAssetPath}).");
            return sheet.text;
        }
    }
}
