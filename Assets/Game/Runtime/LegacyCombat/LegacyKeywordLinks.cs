using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The 예약 효과 that work once per turn per kind, as bits of a fighter's <c>ReserveEffectsUsedThisTurn</c>
    /// (<see cref="LegacyQueuedDuel.ReserveEffectsUsedThisTurn"/>, <see cref="LocalVersusFighter.ReserveEffectsUsedThisTurn"/>).
    /// A new planning turn clears every bit.</summary>
    [Flags]
    public enum LegacyReserveEffect
    {
        None = 0,
        /// <summary>숨고르기 +1 has added its breath this turn (<see cref="LegacySkillEffect.ReserveBreath"/>).</summary>
        Breath = 1 << 0,
        /// <summary>일회성 &lt;ID&gt; ×n has set its copies this turn (<see cref="LegacySkillEffect.ReserveTemporarySkillId"/>).</summary>
        Temporary = 1 << 1,
    }

    /// <summary>The rules every effect that reads the queue shares (Docs/SkillSheet.md, 예약 효과·사용 효과), in one place for
    /// both resolvers (<see cref="LegacyQueuedDuel"/>, <see cref="LocalVersusMatch"/>) and the screens' previews:
    /// <list type="number">
    /// <item>앞 (ahead) means reserved before this slot in this turn's queue.</item>
    /// <item>Skills count by kind (their id: two copies of one skill count once); 숨고르기 counts by entry.</item>
    /// <item>Gains work once per turn: a count-ahead payoff (power or next turn's time) and the 순환 feeder act only for the
    /// first copy of their skill in the committed queue, so a second copy gains nothing. (Reserve-time gains, the automatic
    /// breath and the 일회성 copies, use a bit of <see cref="LegacyReserveEffect"/> instead; a time cost is paid every
    /// time.)</item>
    /// </list>
    /// Everything here reads the queue it is given and the skill sheet; nothing keeps state.</summary>
    public static class LegacyKeywordLinks
    {
        /// <summary>How many of <paramref name="target"/> stand in <paramref name="queue"/> before <paramref name="slotIndex"/>
        /// (which may be the queue's length, for a skill about to be appended): breaths by entry, skills by kind.</summary>
        public static int CountAhead(IReadOnlyList<LegacySkill> queue, int slotIndex, LegacyAheadTarget target)
        {
            if (queue == null || target == LegacyAheadTarget.None) return 0;
            int end = Math.Min(Math.Max(0, slotIndex), queue.Count);
            int count = 0;
            for (int index = 0; index < end; index++)
            {
                LegacySkill skill = queue[index];
                if (skill == null) continue;
                if (target == LegacyAheadTarget.Breath)
                {
                    if (skill.IsWait) count++;
                    continue;
                }
                if (skill.IsWait || !IsFirstOfKind(queue, index, skill)) continue;
                LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
                if (effect == null) continue;
                if (target == LegacyAheadTarget.CycleSkill ? effect.HasCycle : effect.OpponentResistanceReduction > 0) count++;
            }
            return count;
        }

        /// <summary>Whether no copy of the skill at <paramref name="slotIndex"/> stands before it in the queue.</summary>
        public static bool IsFirstOfKind(IReadOnlyList<LegacySkill> queue, int slotIndex)
            => queue != null && slotIndex >= 0 && slotIndex < queue.Count && IsFirstOfKind(queue, slotIndex, queue[slotIndex]);

        /// <summary>Whether no copy of <paramref name="skill"/> (the same id) stands before <paramref name="slotIndex"/>, which may
        /// be the queue's length for a skill about to be appended. Breaths are never a kind of their own.</summary>
        public static bool IsFirstOfKind(IReadOnlyList<LegacySkill> queue, int slotIndex, LegacySkill skill)
        {
            if (skill == null || skill.IsWait) return false;
            if (queue == null) return true;
            int end = Math.Min(Math.Max(0, slotIndex), queue.Count);
            for (int index = 0; index < end; index++)
                if (queue[index] != null && !queue[index].IsWait && queue[index].Id == skill.Id) return false;
            return true;
        }

        /// <summary>What <paramref name="skill"/>'s 사용 효과 <c>앞 &lt;대상&gt;마다 위력</c> counts at <paramref name="slotIndex"/>,
        /// capped at its maximum: 0 without that effect, and 0 for a copy that is not the queue's first (one per turn).</summary>
        public static int AheadPowerCount(IReadOnlyList<LegacySkill> queue, int slotIndex, LegacySkill skill)
        {
            LegacySkillEffect effect = AheadPowerEffect(skill);
            if (effect == null || !IsFirstOfKind(queue, slotIndex, skill)) return 0;
            return Math.Min(CountAhead(queue, slotIndex, effect.AheadPowerTarget), effect.AheadPowerMax);
        }

        /// <summary>The base power <paramref name="skill"/> gains at <paramref name="slotIndex"/> of <paramref name="queue"/> from
        /// its 사용 효과 <c>앞 &lt;대상&gt;마다 위력 +n (최대 m)</c>: n per counted entry, at most m of them; 0 for a copy that is
        /// not the queue's first. Both resolvers add it to the roll range beside the 순환 bonus, before buffs, 맞물림 and
        /// multipliers. With <paramref name="slotIndex"/> at the queue's length it previews a skill about to be reserved.</summary>
        public static int AheadPowerBonus(IReadOnlyList<LegacySkill> queue, int slotIndex, LegacySkill skill)
        {
            LegacySkillEffect effect = AheadPowerEffect(skill);
            return effect == null ? 0 : checked(AheadPowerCount(queue, slotIndex, skill) * effect.AheadPowerPerUnit);
        }

        /// <summary>The 순환 count the cycling skill with <paramref name="skillId"/> gains when <paramref name="queue"/> is
        /// committed: one per kind of 순환 +1 feeder whose first copy stands after that skill's first copy (before its cap,
        /// which <see cref="ApplyCycleFeed"/> applies). 0 when the skill is not queued or does not cycle.</summary>
        public static int CycleFeedReceived(IReadOnlyList<LegacySkill> queue, int skillId)
        {
            if (queue == null) return 0;
            int first = -1;
            for (int index = 0; index < queue.Count; index++)
                if (queue[index] != null && !queue[index].IsWait && queue[index].Id == skillId)
                {
                    first = index;
                    break;
                }
            if (first < 0 || LegacySkillDefinitions.Find(queue[first])?.Effect.HasCycle != true) return 0;
            int feed = 0;
            for (int index = first + 1; index < queue.Count; index++)
            {
                LegacySkill skill = queue[index];
                if (skill == null || skill.IsWait || !IsFirstOfKind(queue, index, skill)) continue;
                if (LegacySkillDefinitions.Find(skill)?.Effect.ReserveCycleFeed == true) feed++;
            }
            return feed;
        }

        /// <summary>순환 +1 as a turn is committed: the first copy of each feeder in <paramref name="queue"/> raises by one the
        /// count in <paramref name="uses"/> of every kind of cycling skill ahead of it, each up to its cap. Run once on the
        /// committed queue, so a source or target already taken off the queue gives or gets nothing and nothing is undone
        /// later. Returns how many skills' counts rose; <paramref name="raised"/>, when given, gets their ids in queue order.</summary>
        public static int ApplyCycleFeed(IReadOnlyList<LegacySkill> queue, IDictionary<int, int> uses, List<int> raised = null)
        {
            if (uses == null) throw new ArgumentNullException(nameof(uses));
            if (queue == null) return 0;
            int count = 0;
            for (int index = 0; index < queue.Count; index++)
            {
                LegacySkill skill = queue[index];
                if (skill == null || skill.IsWait || !IsFirstOfKind(queue, index, skill)) continue;
                LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
                if (effect == null || !effect.HasCycle) continue;
                int feed = CycleFeedReceived(queue, skill.Id);
                if (feed == 0) continue;
                int before = uses.TryGetValue(skill.Id, out int found) ? found : 0;
                int after = Math.Min(effect.CycleMaxCount, checked(before + feed));
                if (after <= before) continue;
                uses[skill.Id] = after;
                count++;
                raised?.Add(skill.Id);
            }
            return count;
        }

        /// <summary>The 순환 count the skill at <paramref name="slotIndex"/> reads when its slot starts, for a planning queue:
        /// <paramref name="usesNow"/> (its count before the turn), the commit's 순환 +1 (<see cref="CycleFeedReceived"/>), and
        /// one more per earlier copy of it in the queue, all within its cap. 0 for a skill that does not cycle.</summary>
        public static int PlannedCycleUses(IReadOnlyList<LegacySkill> queue, int slotIndex, int usesNow)
        {
            if (queue == null || slotIndex < 0 || slotIndex >= queue.Count) return 0;
            LegacySkill skill = queue[slotIndex];
            if (skill == null || skill.IsWait) return 0;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            if (effect == null || !effect.HasCycle) return 0;
            int earlier = 0;
            for (int index = 0; index < slotIndex; index++)
                if (queue[index] != null && !queue[index].IsWait && queue[index].Id == skill.Id) earlier++;
            long planned = (long)usesNow + CycleFeedReceived(queue, skill.Id) + earlier;
            return (int)Math.Min(effect.CycleMaxCount, Math.Max(0, planned));
        }

        /// <summary>The same for a committed queue that is resolving, whose 순환 +1 is already in <paramref name="usesNow"/>: a
        /// slot from <paramref name="firstUncastSlot"/> on reads today's count plus one per earlier copy not yet cast, within
        /// the cap. A slot already cast reads today's count. 0 for a skill that does not cycle.</summary>
        public static int UpcomingCycleUses(IReadOnlyList<LegacySkill> queue, int slotIndex, int firstUncastSlot, int usesNow)
        {
            if (queue == null || slotIndex < 0 || slotIndex >= queue.Count) return 0;
            LegacySkill skill = queue[slotIndex];
            if (skill == null || skill.IsWait) return 0;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            if (effect == null || !effect.HasCycle) return 0;
            int earlier = 0;
            for (int index = Math.Max(0, firstUncastSlot); index < slotIndex; index++)
                if (queue[index] != null && !queue[index].IsWait && queue[index].Id == skill.Id) earlier++;
            return (int)Math.Min(effect.CycleMaxCount, Math.Max(0, (long)usesNow + earlier));
        }

        /// <summary>The next turn's planning seconds <paramref name="skill"/>'s 사용 효과 <c>앞 &lt;대상&gt;마다 다음 턴 제한시간 +n초
        /// (최대 m)</c> earns at <paramref name="slotIndex"/>: n per counted entry, at most m of them; 0 without that effect or
        /// for a copy that is not the queue's first. Both resolvers add it to the fighter's next turn as the slot starts, within
        /// <see cref="LegacyPlanningClock.MaximumBonusSeconds"/> for the turn.</summary>
        public static int AheadTimeBonus(IReadOnlyList<LegacySkill> queue, int slotIndex, LegacySkill skill)
        {
            LegacySkillEffect effect = AheadTimeEffect(skill);
            return effect == null ? 0 : checked(AheadTimeCount(queue, slotIndex, skill) * effect.AheadTimePerUnit);
        }

        /// <summary>What <paramref name="skill"/>'s 사용 효과 <c>앞 &lt;대상&gt;마다 다음 턴 제한시간</c> counts at
        /// <paramref name="slotIndex"/>, capped at its maximum; 0 without that effect or for a second copy.</summary>
        public static int AheadTimeCount(IReadOnlyList<LegacySkill> queue, int slotIndex, LegacySkill skill)
        {
            LegacySkillEffect effect = AheadTimeEffect(skill);
            if (effect == null || !IsFirstOfKind(queue, slotIndex, skill)) return 0;
            return Math.Min(CountAhead(queue, slotIndex, effect.AheadTimeTarget), effect.AheadTimeMax);
        }

        /// <summary>The 일회성 row <paramref name="skill"/>'s 예약 효과 <c>일회성 &lt;ID&gt; ×n</c> sets (the sheet's level-zero
        /// skill), or null without that effect or when the sheet lacks the row.</summary>
        public static LegacySkill TemporarySpawn(LegacySkill skill, out int count)
        {
            count = 0;
            LegacySkillEffect effect = skill == null || skill.IsWait ? null : LegacySkillDefinitions.Find(skill)?.Effect;
            if (effect == null || !effect.HasTemporarySpawn) return null;
            LegacySkill spawned = LegacySkillDefinitions.Find(effect.ReserveTemporarySkillId)?.Skill;
            if (spawned != null) count = effect.ReserveTemporaryCount;
            return spawned;
        }

        /// <summary>예약 효과 일회성 as a skill is reserved, for both resolvers: unless <paramref name="used"/> already has the
        /// <see cref="LegacyReserveEffect.Temporary"/> bit, n copies of the 일회성 row go to the front of that row's lane
        /// (<paramref name="lanes"/>, by lane index) when <paramref name="laneOpen"/> says the duel has it, and the bit is set.
        /// Returns the lane they went to, or -1 when nothing was set.</summary>
        internal static int SpawnTemporary(LegacySkill reserved, List<LegacySkill>[] lanes, Func<int, bool> laneOpen,
            ref LegacyReserveEffect used)
        {
            if ((used & LegacyReserveEffect.Temporary) != 0) return -1;
            LegacySkill spawned = TemporarySpawn(reserved, out int count);
            if (spawned == null || count <= 0) return -1;
            int lane = spawned.LaneIndex;
            if (lane < 0 || lane >= lanes.Length || !laneOpen(lane)) return -1;
            for (int copy = 0; copy < count; copy++) lanes[lane].Insert(0, spawned);
            used |= LegacyReserveEffect.Temporary;
            return lane;
        }

        /// <summary>A new planning turn: every 일회성 skill still in a lane goes. Returns how many went.</summary>
        internal static int ClearTemporary(List<LegacySkill>[] lanes)
        {
            int removed = 0;
            foreach (List<LegacySkill> lane in lanes) removed += lane.RemoveAll(LegacySkillDefinitions.IsTemporary);
            return removed;
        }

        private static LegacySkillEffect AheadTimeEffect(LegacySkill skill)
        {
            if (skill == null || skill.IsWait) return null;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            return effect != null && effect.HasAheadTime ? effect : null;
        }

        private static LegacySkillEffect AheadPowerEffect(LegacySkill skill)
        {
            if (skill == null || skill.IsWait) return null;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            return effect != null && effect.HasAheadPower ? effect : null;
        }
    }
}
