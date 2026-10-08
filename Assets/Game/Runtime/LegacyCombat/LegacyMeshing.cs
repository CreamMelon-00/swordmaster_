using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>맞물림 (meshing): queued player skills of different schools mesh like gears. A skill's school is its lane
    /// (정공 Q, 강공 W, 기교 E). A chain is a maximal run of neighbouring queued skills in which every neighbouring pair
    /// differs in school; anything that is not a skill (숨고르기, an empty slot) breaks it, and a chain needs two skills.
    /// Every skill of an N-skill chain gains N × the duel's percent of power (<see cref="LegacyQueuedDuel.MeshPercent"/>),
    /// added into the same percentage as power buffs, and its slot takes no steps (<see cref="LegacyQueuedDuel.TryStep"/>).
    /// Only the player's queue meshes. Engine-free and deterministic: a queue alone decides its chains.</summary>
    public static class LegacyMeshing
    {
        /// <summary>The per-skill percent a duel uses unless it is given another (tests, the dump, a new duel).</summary>
        public const int DefaultPercent = 10;
        /// <summary>The school of whatever is not a skill: it never meshes and breaks a chain.</summary>
        public const int NoSchool = -1;

        /// <summary>The school of a queued action: its lane (0 정공 Q, 1 강공 W, 2 기교 E), or <see cref="NoSchool"/> for
        /// 숨고르기 and an empty slot.</summary>
        public static int School(LegacySkill skill) => skill == null || skill.IsWait ? NoSchool : skill.LaneIndex;

        /// <summary>Whether two neighbouring queued actions mesh: both are skills, of different schools.</summary>
        public static bool Meshes(LegacySkill left, LegacySkill right)
        {
            int leftSchool = School(left), rightSchool = School(right);
            return leftSchool != NoSchool && rightSchool != NoSchool && leftSchool != rightSchool;
        }

        /// <summary>The power bonus every skill of a chain of <paramref name="chainLength"/> skills gains: N × percent,
        /// zero for a skill that is not in a chain or while 맞물림 is off (percent 0).</summary>
        public static int BonusPercent(int chainLength, int percent)
            => chainLength < 2 || percent <= 0 ? 0 : checked(chainLength * percent);

        /// <summary>The chain <paramref name="slotIndex"/> belongs to in <paramref name="queue"/> (slot order), with the
        /// bonus at <paramref name="percent"/> per skill. A slot outside the queue is an empty slot; percent 0 meshes
        /// nothing.</summary>
        public static LegacyMeshSlot Find(IReadOnlyList<LegacySkill> queue, int slotIndex, int percent)
        {
            if (queue == null || percent <= 0 || slotIndex < 0 || slotIndex >= queue.Count ||
                School(queue[slotIndex]) == NoSchool) return LegacyMeshSlot.Unmeshed(slotIndex);
            int start = slotIndex, end = slotIndex;
            while (start > 0 && Meshes(queue[start - 1], queue[start])) start--;
            while (end + 1 < queue.Count && Meshes(queue[end], queue[end + 1])) end++;
            int length = end - start + 1;
            return length < 2 ? LegacyMeshSlot.Unmeshed(slotIndex)
                : new LegacyMeshSlot(slotIndex, start, length, BonusPercent(length, percent));
        }

        /// <summary>The chain a skill of <paramref name="school"/> would join if it were queued after
        /// <paramref name="queue"/>, as the slot <c>queue.Count</c>: the chain ending at the last queued skill grows by
        /// one when the schools differ, otherwise the new skill would stand alone.</summary>
        public static LegacyMeshSlot FindIfAppended(IReadOnlyList<LegacySkill> queue, int school, int percent)
        {
            int slotIndex = queue?.Count ?? 0;
            if (queue == null || percent <= 0 || school < 0 || school > 2 || slotIndex == 0) return LegacyMeshSlot.Unmeshed(slotIndex);
            int last = School(queue[slotIndex - 1]);
            if (last == NoSchool || last == school) return LegacyMeshSlot.Unmeshed(slotIndex);
            LegacyMeshSlot previous = Find(queue, slotIndex - 1, percent);
            int start = previous.IsMeshed ? previous.ChainStart : slotIndex - 1;
            int length = slotIndex - start + 1;
            return new LegacyMeshSlot(slotIndex, start, length, BonusPercent(length, percent));
        }
    }

    /// <summary>One slot of the player's queue under 맞물림: whether it is meshed, its chain and the chain's bonus.</summary>
    public readonly struct LegacyMeshSlot : IEquatable<LegacyMeshSlot>
    {
        internal LegacyMeshSlot(int slotIndex, int chainStart, int chainLength, int bonusPercent)
        {
            SlotIndex = slotIndex;
            ChainStart = chainStart;
            ChainLength = chainLength;
            BonusPercent = bonusPercent;
        }

        internal static LegacyMeshSlot Unmeshed(int slotIndex) => new LegacyMeshSlot(slotIndex, slotIndex, 0, 0);

        public int SlotIndex { get; }
        /// <summary>The chain's first slot; the slot itself when it is not meshed.</summary>
        public int ChainStart { get; }
        /// <summary>The number of skills in the chain (two or more), or zero when the slot is not meshed.</summary>
        public int ChainLength { get; }
        /// <summary>The power percent this slot's skill gains: <see cref="ChainLength"/> × the duel's percent, or zero.</summary>
        public int BonusPercent { get; }
        public bool IsMeshed => ChainLength >= 2;
        /// <summary>The chain's last slot; the slot itself when it is not meshed.</summary>
        public int ChainEnd => IsMeshed ? ChainStart + ChainLength - 1 : SlotIndex;
        /// <summary>This slot meshes with the one before it (the gears turn between the two icons).</summary>
        public bool MeshesWithPrevious => IsMeshed && SlotIndex > ChainStart;
        /// <summary>This slot meshes with the one after it.</summary>
        public bool MeshesWithNext => IsMeshed && SlotIndex < ChainEnd;

        public bool Equals(LegacyMeshSlot other) => SlotIndex == other.SlotIndex && ChainStart == other.ChainStart &&
            ChainLength == other.ChainLength && BonusPercent == other.BonusPercent;
        public override bool Equals(object obj) => obj is LegacyMeshSlot other && Equals(other);
        public override int GetHashCode() => ((SlotIndex * 31 + ChainStart) * 31 + ChainLength) * 31 + BonusPercent;
        public override string ToString() => IsMeshed
            ? $"slot {SlotIndex} meshed {ChainStart}..{ChainEnd} (+{BonusPercent}%)" : $"slot {SlotIndex} unmeshed";
    }
}
