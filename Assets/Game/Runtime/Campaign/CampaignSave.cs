using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The persistent part of a <see cref="CampaignRun"/>: currency, cleared stages, curriculum progress,
    /// training wins, skill experience, the field pickups taken and the saved loadout. Owned skills follow from the
    /// completed curriculum nodes, the cleared stages and the pickups taken. Battle state, the unsaved loadout
    /// draft and lobby selections are not kept. The data is not validated here; <see cref="CampaignRun.TryRestore"/>
    /// checks it against the game's rules. Curriculum progress belongs to one version of the default tree
    /// (<see cref="CurriculumTreeVersion"/>); <see cref="ForCurriculumTree"/> drops it for another.</summary>
    public sealed class CampaignSave
    {
        public CampaignSave(int currency, IEnumerable<int> clearedStages, IEnumerable<string> curriculumCompleted,
            string curriculumActive, int curriculumBattles, IEnumerable<IEnumerable<int>> loadout,
            int trainingVictoryCount = 0, IEnumerable<KeyValuePair<int, int>> skillExperience = null,
            IEnumerable<string> fieldPickups = null, int curriculumTreeVersion = CampaignCurriculum.TreeVersion)
        {
            if (clearedStages == null) throw new ArgumentNullException(nameof(clearedStages));
            if (curriculumCompleted == null) throw new ArgumentNullException(nameof(curriculumCompleted));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            Currency = currency;
            TrainingVictoryCount = trainingVictoryCount;
            ClearedStages = new List<int>(clearedStages).AsReadOnly();
            CurriculumCompleted = new List<string>(curriculumCompleted).AsReadOnly();
            CurriculumActive = curriculumActive;
            CurriculumBattles = curriculumBattles;
            SkillExperience = new List<KeyValuePair<int, int>>(skillExperience ??
                Array.Empty<KeyValuePair<int, int>>()).AsReadOnly();
            FieldPickups = new List<string>(fieldPickups ?? Array.Empty<string>()).AsReadOnly();
            CurriculumTreeVersion = curriculumTreeVersion;
            var lanes = new List<IReadOnlyList<int>>();
            foreach (IEnumerable<int> lane in loadout)
                lanes.Add(new List<int>(lane ?? throw new ArgumentException("A loadout lane is missing.", nameof(loadout))).AsReadOnly());
            Loadout = lanes.AsReadOnly();
        }

        public int Currency { get; }
        /// <summary>Dummy fights won; omitted in older version 2 saves and then treated as zero.</summary>
        public int TrainingVictoryCount { get; }
        /// <summary>Numbers of the cleared stages.</summary>
        public IReadOnlyList<int> ClearedStages { get; }
        /// <summary>Completed curriculum node ids in completion order.</summary>
        public IReadOnlyList<string> CurriculumCompleted { get; }
        /// <summary>The node in progress, or null.</summary>
        public string CurriculumActive { get; }
        /// <summary>Finished battles counted toward <see cref="CurriculumActive"/>.</summary>
        public int CurriculumBattles { get; }
        /// <summary>Nonzero cumulative clashes per owned skill ID. An absent entry has zero experience.</summary>
        public IReadOnlyList<KeyValuePair<int, int>> SkillExperience { get; }
        /// <summary>Ids of the field pickups taken (<see cref="CampaignFieldPickups"/>), in the order they were taken;
        /// omitted in older version 2 saves and then empty.</summary>
        public IReadOnlyList<string> FieldPickups { get; }
        /// <summary>The saved Q/W/E lanes as skill ids.</summary>
        public IReadOnlyList<IReadOnlyList<int>> Loadout { get; }
        /// <summary>The version of the default curriculum tree the curriculum progress was made with
        /// (<see cref="CampaignCurriculum.TreeVersion"/>). A save file without the line reads 1, the tree before the
        /// version existed; a save made in code defaults to the current version.</summary>
        public int CurriculumTreeVersion { get; }

        /// <summary>This save as the given tree version reads it. The same version returns this save itself. Another version
        /// returns a copy stamped with <paramref name="treeVersion"/> whose curriculum is empty: no completed node, none in
        /// progress, no counted battle. Everything else (story, stages, pickups, experience, the loadout) is copied as it
        /// is; <see cref="CampaignRun.TryRestore"/> then drops what only the curriculum granted.</summary>
        /// <param name="droppedProgress">True only when the copy lost a completed node or a node in progress, so the
        /// player has something to be told about.</param>
        public CampaignSave ForCurriculumTree(int treeVersion, out bool droppedProgress)
        {
            if (treeVersion == CurriculumTreeVersion)
            {
                droppedProgress = false;
                return this;
            }
            droppedProgress = CurriculumCompleted.Count > 0 || CurriculumActive != null;
            return new CampaignSave(Currency, ClearedStages, Array.Empty<string>(), null, 0, Loadout, TrainingVictoryCount,
                SkillExperience, FieldPickups, treeVersion);
        }
    }
}
