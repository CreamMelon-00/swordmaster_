using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>One owned skill in a save: its base skill id and upgrade level.</summary>
    public readonly struct SavedSkill
    {
        public SavedSkill(int id, int level)
        {
            Id = id;
            Level = level;
        }

        public int Id { get; }
        public int Level { get; }
    }

    /// <summary>The persistent part of a <see cref="CampaignRun"/>: currency, cleared stages, owned skills and the
    /// saved loadout. Battle state, the unsaved loadout draft and lobby selections are not kept. The data is not
    /// validated here; <see cref="CampaignRun.TryRestore"/> checks it against the game's rules.</summary>
    public sealed class CampaignSave
    {
        public CampaignSave(int currency, IEnumerable<int> clearedStages, IEnumerable<SavedSkill> ownedSkills,
            IEnumerable<IEnumerable<int>> loadout)
        {
            if (clearedStages == null) throw new ArgumentNullException(nameof(clearedStages));
            if (ownedSkills == null) throw new ArgumentNullException(nameof(ownedSkills));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            Currency = currency;
            ClearedStages = new List<int>(clearedStages).AsReadOnly();
            OwnedSkills = new List<SavedSkill>(ownedSkills).AsReadOnly();
            var lanes = new List<IReadOnlyList<int>>();
            foreach (IEnumerable<int> lane in loadout)
                lanes.Add(new List<int>(lane ?? throw new ArgumentException("A loadout lane is missing.", nameof(loadout))).AsReadOnly());
            Loadout = lanes.AsReadOnly();
        }

        public int Currency { get; }
        /// <summary>Numbers of the cleared stages.</summary>
        public IReadOnlyList<int> ClearedStages { get; }
        /// <summary>Owned skills in acquisition order.</summary>
        public IReadOnlyList<SavedSkill> OwnedSkills { get; }
        /// <summary>The saved Q/W/E lanes as skill ids.</summary>
        public IReadOnlyList<IReadOnlyList<int>> Loadout { get; }
    }
}
