using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The persistent part of a <see cref="CampaignRun"/>: currency, cleared stages, curriculum progress and
    /// the saved loadout. Owned skills follow from the completed curriculum nodes. Battle state, the unsaved loadout
    /// draft and lobby selections are not kept. The data is not validated here; <see cref="CampaignRun.TryRestore"/>
    /// checks it against the game's rules.</summary>
    public sealed class CampaignSave
    {
        public CampaignSave(int currency, IEnumerable<int> clearedStages, IEnumerable<string> curriculumCompleted,
            string curriculumActive, int curriculumBattles, IEnumerable<IEnumerable<int>> loadout)
        {
            if (clearedStages == null) throw new ArgumentNullException(nameof(clearedStages));
            if (curriculumCompleted == null) throw new ArgumentNullException(nameof(curriculumCompleted));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            Currency = currency;
            ClearedStages = new List<int>(clearedStages).AsReadOnly();
            CurriculumCompleted = new List<string>(curriculumCompleted).AsReadOnly();
            CurriculumActive = curriculumActive;
            CurriculumBattles = curriculumBattles;
            var lanes = new List<IReadOnlyList<int>>();
            foreach (IEnumerable<int> lane in loadout)
                lanes.Add(new List<int>(lane ?? throw new ArgumentException("A loadout lane is missing.", nameof(loadout))).AsReadOnly());
            Loadout = lanes.AsReadOnly();
        }

        public int Currency { get; }
        /// <summary>Numbers of the cleared stages.</summary>
        public IReadOnlyList<int> ClearedStages { get; }
        /// <summary>Completed curriculum node ids in completion order.</summary>
        public IReadOnlyList<string> CurriculumCompleted { get; }
        /// <summary>The node in progress, or null.</summary>
        public string CurriculumActive { get; }
        /// <summary>Finished battles counted toward <see cref="CurriculumActive"/>.</summary>
        public int CurriculumBattles { get; }
        /// <summary>The saved Q/W/E lanes as skill ids.</summary>
        public IReadOnlyList<IReadOnlyList<int>> Loadout { get; }
    }
}
