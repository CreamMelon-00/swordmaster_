using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A reservation's occupied interval. Two half ticks are one ordinary technique.</summary>
    public sealed class LegacyTimedSkill
    {
        internal LegacyTimedSkill(int queueIndex, LegacySkill skill, int start, int end)
        { QueueIndex = queueIndex; Skill = skill; StartHalfTick = start; EndHalfTick = end; }
        public int QueueIndex { get; }
        public LegacySkill Skill { get; }
        public int StartHalfTick { get; }
        public int EndHalfTick { get; }
        public int DurationHalfTicks => EndHalfTick - StartHalfTick;
    }

    /// <summary>The common packing rule for the desk, forecast and actual battle. Only adjacent fast reservations
    /// share an ordinary cell; an unmatched fast reservation leaves the second half of its cell empty.</summary>
    public static class LegacySkillTiming
    {
        public static IReadOnlyList<LegacyTimedSkill> Build(IReadOnlyList<LegacySkill> queue)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            var result = new LegacyTimedSkill[queue.Count];
            int cursor = 0;
            for (int i = 0; i < queue.Count; i++)
            {
                LegacySkill skill = queue[i];
                int duration = skill?.DurationHalfUnits ?? 2;
                if (duration != 1 && (cursor & 1) != 0) cursor = checked(cursor + 1);
                int end = checked(cursor + duration);
                result[i] = new LegacyTimedSkill(i, skill, cursor, end);
                cursor = end;
            }
            return result;
        }

        public static int TotalHalfTicks(IReadOnlyList<LegacySkill> queue)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            int cursor = 0;
            for (int i = 0; i < queue.Count; i++)
            {
                int duration = queue[i]?.DurationHalfUnits ?? 2;
                if (duration != 1 && (cursor & 1) != 0) cursor = checked(cursor + 1);
                cursor = checked(cursor + duration);
            }
            return checked((cursor + 1) / 2 * 2);
        }

        internal static LegacyTimeSegment[] Segments(IReadOnlyList<LegacyTimedSkill> player,
            IReadOnlyList<LegacyTimedSkill> enemy)
        {
            var points = new SortedSet<int> { 0 };
            AddPoints(points, player); AddPoints(points, enemy);
            int final = Math.Max(End(player), End(enemy));
            if ((final & 1) != 0) points.Add(checked(final + 1));
            var result = new List<LegacyTimeSegment>();
            int previous = 0;
            foreach (int point in points)
            {
                if (point == 0) continue;
                result.Add(new LegacyTimeSegment(previous, point, Active(player, previous, point), Active(enemy, previous, point)));
                previous = point;
            }
            return result.ToArray();
        }

        private static int End(IReadOnlyList<LegacyTimedSkill> entries)
            => entries.Count == 0 ? 0 : entries[entries.Count - 1].EndHalfTick;
        private static void AddPoints(SortedSet<int> points, IReadOnlyList<LegacyTimedSkill> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            { points.Add(entries[i].StartHalfTick); points.Add(entries[i].EndHalfTick); }
        }
        private static LegacyTimedSkill Active(IReadOnlyList<LegacyTimedSkill> entries, int start, int end)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].StartHalfTick <= start && entries[i].EndHalfTick >= end) return entries[i];
            return null;
        }
    }

    internal sealed class LegacyTimeSegment
    {
        internal LegacyTimeSegment(int start, int end, LegacyTimedSkill player, LegacyTimedSkill enemy)
        { Start = start; End = end; Player = player; Enemy = enemy; }
        internal readonly int Start, End;
        internal readonly LegacyTimedSkill Player, Enemy;
    }
}
