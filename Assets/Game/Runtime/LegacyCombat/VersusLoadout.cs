using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A match-only draft: Q, W and E each have three ordered player techniques.</summary>
    public sealed class VersusLoadout
    {
        public const int LaneCount = 3;
        public const int SlotsPerLane = 3;
        public const int SkillCount = LaneCount * SlotsPerLane;

        private readonly int[] ids = new int[SkillCount];

        public VersusLoadout() { Reset(); }

        public LegacySkill GetSkill(int lane, int slot)
        {
            CheckSlot(lane, slot);
            return LegacySkillDefinitions.Skill(ids[Index(lane, slot)]);
        }

        public int GetSkillId(int lane, int slot)
        {
            CheckSlot(lane, slot);
            return ids[Index(lane, slot)];
        }

        public bool IsEquipped(int skillId)
        {
            foreach (int id in ids) if (id == skillId) return true;
            return false;
        }

        public bool TryPlaceSkill(int skillId, int lane, int slot)
        {
            if (!ValidSlot(lane, slot) || !TryFindAvailable(skillId, out LegacySkill skill) ||
                skill.LaneIndex != lane) return false;
            int target = Index(lane, slot);
            int source = Array.IndexOf(ids, skillId);
            if (source == target) return true;
            if (source >= 0) ids[source] = ids[target];
            ids[target] = skillId;
            return true;
        }

        public void Reset()
        {
            int[] next = new int[SkillCount];
            var counts = new int[LaneCount];
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialSkills)
            {
                if (skill == null || skill.LaneIndex < 0 || skill.LaneIndex >= LaneCount ||
                    counts[skill.LaneIndex] >= SlotsPerLane)
                    throw new InvalidOperationException("The starting versus loadout has invalid lane counts.");
                next[Index(skill.LaneIndex, counts[skill.LaneIndex]++)] = skill.Id;
            }
            for (int lane = 0; lane < LaneCount; lane++)
                if (counts[lane] != SlotsPerLane)
                    throw new InvalidOperationException("The starting versus loadout needs three skills per lane.");
            Array.Copy(next, ids, SkillCount);
        }

        /// <summary>Independent Q1–Q3, W1–W3, E1–E3 snapshot for lobby messages.</summary>
        public int[] ExportIds() => (int[])ids.Clone();

        /// <summary>Rejects a malformed or enemy-only roster without changing the previous one.</summary>
        public bool TryImportIds(IReadOnlyList<int> incoming)
        {
            if (incoming == null || incoming.Count != SkillCount) return false;
            var next = new int[SkillCount];
            var seen = new HashSet<int>();
            for (int index = 0; index < SkillCount; index++)
            {
                int id = incoming[index];
                if (!TryFindAvailable(id, out LegacySkill skill) ||
                    skill.LaneIndex != index / SlotsPerLane || !seen.Add(id)) return false;
                next[index] = id;
            }
            Array.Copy(next, ids, SkillCount);
            return true;
        }

        public LegacySkill[] ToSkills()
        {
            var skills = new LegacySkill[SkillCount];
            for (int index = 0; index < SkillCount; index++)
                skills[index] = LegacySkillDefinitions.Skill(ids[index]);
            return skills;
        }

        private static bool TryFindAvailable(int id, out LegacySkill skill)
        {
            foreach (LegacySkill candidate in LegacySkillDefinitions.InitialSkills)
                if (candidate.Id == id) { skill = candidate; return true; }
            foreach (LegacySkill candidate in LegacySkillDefinitions.AcquisitionSkills)
                if (candidate.Id == id) { skill = candidate; return true; }
            skill = null;
            return false;
        }

        private static void CheckSlot(int lane, int slot)
        {
            if (!ValidSlot(lane, slot)) throw new ArgumentOutOfRangeException(nameof(lane));
        }

        private static bool ValidSlot(int lane, int slot)
            => lane >= 0 && lane < LaneCount && slot >= 0 && slot < SlotsPerLane;

        private static int Index(int lane, int slot) => lane * SlotsPerLane + slot;
    }
}
