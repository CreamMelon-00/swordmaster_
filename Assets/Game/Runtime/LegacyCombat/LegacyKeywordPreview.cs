using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>What a queued card's keyword mark stands for: the screens colour it by this.</summary>
    public enum LegacyKeywordMarkKind
    {
        None,
        /// <summary>The automatic 숨고르기 a 숨고르기 +1 skill reserved (<c>자동</c>).</summary>
        AutoBreath,
        /// <summary>A 순환 +1 feeder (<c>순환 +1 ×k</c>).</summary>
        CycleFeed,
        /// <summary>A count-ahead payoff, 앞 &lt;대상&gt;마다 위력 (<c>숨 +12</c>).</summary>
        AheadPower,
        /// <summary>자동 회피 (<c>회피</c>).</summary>
        AutoDodge,
        /// <summary>A 일회성 skill, used once and gone (<c>1회</c>).</summary>
        Temporary,
        /// <summary>A count-ahead payoff in next turn's planning time, 앞 &lt;대상&gt;마다 다음 턴 제한시간 (<c>다음 턴 +4초</c>).</summary>
        AheadTime,
    }

    /// <summary>One queued card's keyword mark: its words, what it stands for and whether it gains nothing (a second copy,
    /// nothing counted yet), which the screens draw muted.</summary>
    public readonly struct LegacyKeywordMark
    {
        public LegacyKeywordMark(string text, LegacyKeywordMarkKind kind, bool spent)
        {
            Text = text ?? string.Empty;
            Kind = kind;
            Spent = spent;
        }

        public string Text { get; }
        public LegacyKeywordMarkKind Kind { get; }
        public bool Spent { get; }
        public bool IsEmpty => Kind == LegacyKeywordMarkKind.None;
        public static LegacyKeywordMark None => default;
    }

    /// <summary>The words the duel screens use for the effects that read this turn's queue (예약 효과, 사용 효과;
    /// <see cref="LegacyKeywordLinks"/> has the rules): the lane window's "if reserved now" line, the held explanation's row,
    /// the queued cards' marks and 순환 counts, and the activation cues. Everything is read from an
    /// <see cref="ILegacyDuelView"/> (the campaign's duel or a versus match from one side), so the campaign and versus say the
    /// same, and nothing here changes the duel. Each reads the numbers the rules will use: a skill about to be reserved
    /// always goes to the queue's end, so counting the queue as it is now is exact.</summary>
    public static class LegacyKeywordPreview
    {
        /// <summary>A once-per-turn gain this skill's kind already took this turn: its next copy gains nothing. The badge of
        /// such a skill says <c>턴당 1회</c>, so the HUD uses the same words (the held explanation's row puts its effect first,
        /// <c>순환 +1 · 턴당 1회 · 사용함</c>).</summary>
        public const string UsedThisTurn = "턴당 1회 · 사용함";
        /// <summary>The automatic 숨고르기's card tag.</summary>
        public const string AutoBreathTag = "자동";
        /// <summary>자동 회피's card tag and its cue.</summary>
        public const string AutoDodgeTag = "회피", AutoDodgeCue = "자동 회피";
        /// <summary>A cycling card that 순환 +1 would raise but which is already at its cap.</summary>
        public const string CycleCapped = "최대";
        /// <summary>A 일회성 skill's tag on its gear, lane window and queued card.</summary>
        public const string TemporaryTag = "1회";
        /// <summary>The lane pop as the 일회성 copies left in a lane go at the next turn.</summary>
        public const string TemporaryExpired = "소멸";

        /// <summary>A count-ahead target's short name on the lane window and cards: 숨, 순환, 감소.</summary>
        public static string Unit(LegacyAheadTarget target)
        {
            switch (target)
            {
                case LegacyAheadTarget.Breath: return "숨";
                case LegacyAheadTarget.CycleSkill: return "순환";
                case LegacyAheadTarget.ResistanceReduction: return "감소";
                default: return string.Empty;
            }
        }

        /// <summary>The lane window's line under the front skill of a lane: what reserving <paramref name="skill"/> now would
        /// do. <c>숨 3/4 · +12</c>, <c>감소 2/3 · +10</c> (count-ahead power), <c>앞 순환 2</c> (순환 +1: the kinds ahead it would
        /// still raise), <c>자동 숨 1/1</c> (숨고르기 +1, while 숨고르기 is open), <c>갈아내기 ×3</c> (일회성, while its lane is
        /// open), <c>다음 턴 +4초</c> (count-ahead time), or <see cref="UsedThisTurn"/> when its gain was taken this turn. Null for a
        /// skill without such an effect.</summary>
        public static string LaneLine(ILegacyDuelView view, LegacySkill skill)
        {
            LegacySkillEffect effect = EffectOf(skill);
            if (view == null || effect == null) return null;
            IReadOnlyList<LegacySkill> queue = view.PlayerQueue;
            int slot = queue.Count;
            var parts = new List<string>(2);
            if (effect.ReserveCycleFeed)
                parts.Add(!LegacyKeywordLinks.IsFirstOfKind(queue, slot, skill) ? UsedThisTurn
                    : "앞 순환 " + RaisedAhead(queue, slot, view.CycleUses).Count);
            if (effect.ReserveBreath && view.Features.Has(CombatFeature.Breath))
                parts.Add(AutoBreathUsed(view) ? UsedThisTurn
                    : "자동 숨 " + LegacyQueuedDuel.MaximumAutoBreathsPerTurn + "/" + LegacyQueuedDuel.MaximumAutoBreathsPerTurn);
            if (effect.HasAheadPower)
                parts.Add(!LegacyKeywordLinks.IsFirstOfKind(queue, slot, skill) ? UsedThisTurn
                    : Unit(effect.AheadPowerTarget) + " " + LegacyKeywordLinks.AheadPowerCount(queue, slot, skill) + "/" +
                      effect.AheadPowerMax + " · +" + LegacyKeywordLinks.AheadPowerBonus(queue, slot, skill));
            LegacySkill spawned = LegacyKeywordLinks.TemporarySpawn(skill, out int copies);
            if (spawned != null && view.Features.HasLane(spawned.LaneIndex))
                parts.Add(TemporaryUsed(view) ? UsedThisTurn : TemporarySpawnText(spawned, copies));
            if (effect.HasAheadTime)
                parts.Add(!LegacyKeywordLinks.IsFirstOfKind(queue, slot, skill) ? UsedThisTurn
                    : NextTurnTime(LegacyKeywordLinks.AheadTimeBonus(queue, slot, skill)));
            return parts.Count == 0 ? null : string.Join(" · ", Distinct(parts));
        }

        /// <summary>The held explanation's row for the player's <paramref name="skill"/> (the front of its lane), in full
        /// words: what reserving it now would earn, why it would earn nothing, or for 자동 회피 what the slot it would take
        /// holds. Null for a skill without such an effect.</summary>
        public static string Note(ILegacyDuelView view, LegacySkill skill)
        {
            LegacySkillEffect effect = EffectOf(skill);
            if (view == null || effect == null) return null;
            IReadOnlyList<LegacySkill> queue = view.PlayerQueue;
            int slot = queue.Count;
            bool first = LegacyKeywordLinks.IsFirstOfKind(queue, slot, skill);
            var parts = new List<string>(2);
            if (effect.ReserveCycleFeed)
            {
                List<LegacySkill> raised = RaisedAhead(queue, slot, view.CycleUses);
                parts.Add(!first ? Used("순환 +1")
                    : raised.Count == 0 ? "순환 +1 · 앞에 올릴 순환 기술 없음"
                    : "순환 +1 · 지금 예약하면 " + Names(raised) + " 순환 +1");
            }
            if (effect.ReserveBreath && view.Features.Has(CombatFeature.Breath))
                parts.Add(AutoBreathUsed(view) ? Used("숨고르기 +1")
                    : "숨고르기 +1 · 지금 예약하면 바로 뒤에 숨고르기 (자동 1/1)");
            if (effect.HasAheadPower)
            {
                string target = LegacySkillLabels.AheadTarget(effect.AheadPowerTarget);
                parts.Add(!first ? Used(target + "마다 위력")
                    : target + " " + LegacyKeywordLinks.AheadPowerCount(queue, slot, skill) + "/" + effect.AheadPowerMax +
                      " · 지금 예약하면 위력 +" + LegacyKeywordLinks.AheadPowerBonus(queue, slot, skill));
            }
            LegacySkill spawned = LegacyKeywordLinks.TemporarySpawn(skill, out int copies);
            if (spawned != null && view.Features.HasLane(spawned.LaneIndex))
                parts.Add(TemporaryUsed(view) ? Used("일회성")
                    : "일회성 · 지금 예약하면 " + "QWE"[spawned.LaneIndex] + " 열 맨 앞에 " + spawned.Name + " " + copies + "개");
            if (effect.HasAheadTime)
            {
                string target = LegacySkillLabels.AheadTarget(effect.AheadTimeTarget);
                parts.Add(!first ? Used(target + "마다 제한시간")
                    : target + " " + LegacyKeywordLinks.AheadTimeCount(queue, slot, skill) + "/" + effect.AheadTimeMax +
                      " · 지금 예약하면 " + NextTurnTime(LegacyKeywordLinks.AheadTimeBonus(queue, slot, skill)));
            }
            if (effect.AutoDodge)
            {
                // The enemy skill already in the slot this one would take (none yet: the slot is still open).
                LegacySkill facing = slot < view.EnemyQueue.Count ? view.EnemyQueue[slot] : null;
                parts.Add(facing == null ? "자동 회피 · 이 칸 상대 공격을 모두 피함"
                    : facing.Kind == LegacySkillKind.Attack ? "자동 회피 · 지금 예약하면 " + KoreanParticle.Attach(facing.Name, "을") + " 피함"
                    : "자동 회피 · 이 칸 상대는 공격하지 않음");
            }
            return parts.Count == 0 ? null : string.Join("  ", parts);
        }

        /// <summary>The keyword mark of the card in <paramref name="slotIndex"/> of the player's row (<paramref name="playerRow"/>)
        /// or the opponent's: <see cref="AutoBreathTag"/> on this turn's automatic 숨고르기, <c>숨 +12</c> on a count-ahead
        /// payoff (its second copy <c>+0</c>), <c>순환 +1 ×k</c> on a 순환 +1 feeder while planning (k: the kinds ahead it
        /// raises), <see cref="AutoDodgeTag"/> on 자동 회피, <c>다음 턴 +4초</c> on a count-ahead time payoff (its second copy
        /// <c>+0초</c>) and <see cref="TemporaryTag"/> on a 일회성 skill. <see cref="LegacyKeywordMark.None"/> for any other card.</summary>
        public static LegacyKeywordMark QueueMark(ILegacyDuelView view, bool playerRow, int slotIndex, bool planning)
        {
            if (view == null) return LegacyKeywordMark.None;
            IReadOnlyList<LegacySkill> queue = playerRow ? view.PlayerQueue : view.EnemyQueue;
            if (slotIndex < 0 || slotIndex >= queue.Count || queue[slotIndex] == null) return LegacyKeywordMark.None;
            LegacySkill skill = queue[slotIndex];
            if (skill.IsWait)
                return slotIndex == (playerRow ? view.AutoBreathSlotIndex : view.EnemyAutoBreathSlotIndex)
                    ? new LegacyKeywordMark(AutoBreathTag, LegacyKeywordMarkKind.AutoBreath, false) : LegacyKeywordMark.None;
            LegacySkillEffect effect = EffectOf(skill);
            if (effect == null) return LegacyKeywordMark.None;
            bool first = LegacyKeywordLinks.IsFirstOfKind(queue, slotIndex);
            if (effect.HasAheadPower)
            {
                int bonus = LegacyKeywordLinks.AheadPowerBonus(queue, slotIndex, skill);
                return new LegacyKeywordMark(first ? Unit(effect.AheadPowerTarget) + " +" + bonus : "+0",
                    LegacyKeywordMarkKind.AheadPower, bonus == 0);
            }
            if (effect.ReserveCycleFeed && planning)
            {
                int raised = first ? RaisedAhead(queue, slotIndex, CycleUsesOf(view, playerRow)).Count : 0;
                return new LegacyKeywordMark(raised > 0 ? "순환 +1 ×" + raised : "순환 +0", LegacyKeywordMarkKind.CycleFeed,
                    raised == 0);
            }
            if (effect.AutoDodge) return new LegacyKeywordMark(AutoDodgeTag, LegacyKeywordMarkKind.AutoDodge, false);
            if (effect.HasAheadTime)
            {
                int seconds = LegacyKeywordLinks.AheadTimeBonus(queue, slotIndex, skill);
                return new LegacyKeywordMark(first ? NextTurnTime(seconds) : "+0초", LegacyKeywordMarkKind.AheadTime, seconds == 0);
            }
            if (LegacySkillDefinitions.IsTemporary(skill))
                return new LegacyKeywordMark(TemporaryTag, LegacyKeywordMarkKind.Temporary, false);
            return LegacyKeywordMark.None;
        }

        /// <summary>A 일회성 spawner's words: <c>갈아내기 ×3</c> (the lane line, the reservation's cue).</summary>
        public static string TemporarySpawnText(LegacySkill spawned, int copies)
            => spawned == null ? null : spawned.Name + " \u00D7" + copies;

        /// <summary>The cue as <paramref name="reserved"/> sets its 일회성 copies: <c>갈아내기 ×3</c>; null for a skill without the
        /// effect.</summary>
        public static string TemporarySpawnCue(LegacySkill reserved)
        {
            LegacySkill spawned = LegacyKeywordLinks.TemporarySpawn(reserved, out int copies);
            return spawned == null ? null : TemporarySpawnText(spawned, copies);
        }

        /// <summary>Next turn's planning seconds as the lane line, the card and the cue say them: <c>다음 턴 +4초</c>.</summary>
        public static string NextTurnTime(int seconds) => "다음 턴 +" + seconds + "초";

        /// <summary>The count-ahead time payoff's cue as its slot starts (the seconds the slot actually added, within the
        /// turn's cap): <c>다음 턴 +4초</c>; null for none.</summary>
        public static string AheadTimeCue(int gainedSeconds) => gainedSeconds > 0 ? NextTurnTime(gainedSeconds) : null;

        /// <summary>How many 일회성 skills each lane holds, for the screens to see what a new turn took away.</summary>
        public static int[] TemporaryCounts(ILegacyDuelView view)
        {
            var counts = new int[3];
            if (view == null) return counts;
            for (int lane = 0; lane < counts.Length; lane++)
                foreach (LegacySkill skill in view.GetLane(lane))
                    if (LegacySkillDefinitions.IsTemporary(skill)) counts[lane]++;
            return counts;
        }

        /// <summary>The 순환 count on the card in <paramref name="slotIndex"/>: <c>u/cap</c> as its slot will read it, and while
        /// planning, when a 순환 +1 behind it raises it, <c>u→u+1/cap</c> (<paramref name="raised"/>) or, already at its cap,
        /// <see cref="CycleCapped"/> (<paramref name="capped"/>). Null for a card that does not cycle.</summary>
        public static string CycleCount(ILegacyDuelView view, bool playerRow, int slotIndex, bool planning,
            out bool raised, out bool capped)
        {
            raised = capped = false;
            if (view == null) return null;
            IReadOnlyList<LegacySkill> queue = playerRow ? view.PlayerQueue : view.EnemyQueue;
            if (slotIndex < 0 || slotIndex >= queue.Count) return null;
            LegacySkill skill = queue[slotIndex];
            LegacySkillEffect effect = EffectOf(skill);
            if (effect == null || !effect.HasCycle) return null;
            int reads = playerRow ? view.CycleUsesAtSlot(slotIndex) : view.EnemyCycleUsesAtSlot(slotIndex);
            string cap = "/" + effect.CycleMaxCount;
            if (!planning || LegacyKeywordLinks.CycleFeedReceived(queue, skill.Id) == 0) return reads + cap;
            // Without the feed the slot would read today's count plus one per earlier copy, within the cap.
            int earlier = 0;
            for (int index = 0; index < slotIndex; index++)
                if (queue[index] != null && !queue[index].IsWait && queue[index].Id == skill.Id) earlier++;
            int before = (int)Math.Min(effect.CycleMaxCount, (long)CycleUsesOf(view, playerRow)(skill.Id) + earlier);
            if (reads > before)
            {
                raised = true;
                return before + "→" + reads + cap;
            }
            capped = true;
            return CycleCapped;
        }

        /// <summary>The 순환 +1 cue as a turn is committed: the names of the cycling skills in <paramref name="queue"/> whose
        /// counts rise, <c>순환 +1 · 사선베기·급소 찌르기</c>; null when none would (<see cref="LegacyKeywordLinks.ApplyCycleFeed"/> on
        /// a copy of the counts, so nothing changes).</summary>
        public static string CycleFeedCue(IReadOnlyList<LegacySkill> queue, Func<int, int> cycleUses)
        {
            if (queue == null || cycleUses == null) return null;
            var uses = new Dictionary<int, int>();
            foreach (LegacySkill skill in queue)
                if (EffectOf(skill)?.HasCycle == true && !uses.ContainsKey(skill.Id)) uses[skill.Id] = cycleUses(skill.Id);
            var raised = new List<int>();
            if (LegacyKeywordLinks.ApplyCycleFeed(queue, uses, raised) == 0) return null;
            var names = new List<string>(raised.Count);
            foreach (int id in raised) names.Add(NameIn(queue, id));
            return "순환 +1 · " + string.Join("·", names);
        }

        /// <summary>The first 순환 +1 feeder in <paramref name="queue"/>, for the cue's title; null for none.</summary>
        public static LegacySkill FirstCycleFeeder(IReadOnlyList<LegacySkill> queue)
        {
            if (queue == null) return null;
            foreach (LegacySkill skill in queue)
                if (EffectOf(skill)?.ReserveCycleFeed == true) return skill;
            return null;
        }

        /// <summary>A count-ahead payoff's cue as its slot starts: <c>숨고르기 3 · 위력 +12</c>, <c>저항 감소 2종 · 위력 +10</c>,
        /// <c>순환 2종 · 위력 +6</c>. Null when the skill in that slot gains nothing (none counted, or a second copy).</summary>
        public static string AheadPowerCue(IReadOnlyList<LegacySkill> queue, int slotIndex)
        {
            if (queue == null || slotIndex < 0 || slotIndex >= queue.Count) return null;
            LegacySkill skill = queue[slotIndex];
            LegacySkillEffect effect = EffectOf(skill);
            if (effect == null || !effect.HasAheadPower) return null;
            int bonus = LegacyKeywordLinks.AheadPowerBonus(queue, slotIndex, skill);
            if (bonus <= 0) return null;
            int count = LegacyKeywordLinks.AheadPowerCount(queue, slotIndex, skill);
            return LegacySkillLabels.AheadTarget(effect.AheadPowerTarget).Replace(" 기술", string.Empty) + " " + count +
                (effect.AheadPowerTarget == LegacyAheadTarget.Breath ? string.Empty : "종") + " · 위력 +" + bonus;
        }

        // The cycling skills (one per kind) before slotIndex that a 순환 +1 would still raise: below their cap with the
        // feed the queue already gives them.
        private static List<LegacySkill> RaisedAhead(IReadOnlyList<LegacySkill> queue, int slotIndex, Func<int, int> cycleUses)
        {
            var raised = new List<LegacySkill>();
            int end = Math.Min(Math.Max(0, slotIndex), queue.Count);
            for (int index = 0; index < end; index++)
            {
                LegacySkill skill = queue[index];
                LegacySkillEffect effect = EffectOf(skill);
                if (effect == null || !effect.HasCycle || !LegacyKeywordLinks.IsFirstOfKind(queue, index, skill)) continue;
                // The other feeders behind it give it their +1 anyway; only what is left under its cap is this one's to give.
                int fed = 0;
                for (int after = index + 1; after < queue.Count; after++)
                    if (after != slotIndex && EffectOf(queue[after])?.ReserveCycleFeed == true &&
                        LegacyKeywordLinks.IsFirstOfKind(queue, after)) fed++;
                if ((long)cycleUses(skill.Id) + fed < effect.CycleMaxCount) raised.Add(skill);
            }
            return raised;
        }

        // The held explanation's row for a once-per-turn gain already taken: its effect, then the lane line's words.
        private static string Used(string effect) => effect + " · " + UsedThisTurn;

        private static bool AutoBreathUsed(ILegacyDuelView view)
            => (view.ReserveEffectsUsedThisTurn & LegacyReserveEffect.Breath) != 0;

        private static bool TemporaryUsed(ILegacyDuelView view)
            => (view.ReserveEffectsUsedThisTurn & LegacyReserveEffect.Temporary) != 0;

        private static Func<int, int> CycleUsesOf(ILegacyDuelView view, bool playerRow)
            => playerRow ? (Func<int, int>)view.CycleUses : view.EnemyCycleUses;

        private static LegacySkillEffect EffectOf(LegacySkill skill)
            => skill == null || skill.IsWait ? null : LegacySkillDefinitions.Find(skill)?.Effect;

        private static string Names(List<LegacySkill> skills)
        {
            var names = new List<string>(skills.Count);
            foreach (LegacySkill skill in skills) names.Add(skill.Name);
            return string.Join("·", names);
        }

        private static string NameIn(IReadOnlyList<LegacySkill> queue, int id)
        {
            foreach (LegacySkill skill in queue)
                if (skill != null && skill.Id == id) return skill.Name;
            return LegacySkillDefinitions.Find(id)?.Skill.Name ?? id.ToString();
        }

        private static IEnumerable<string> Distinct(List<string> parts)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string part in parts)
                if (seen.Add(part)) yield return part;
        }
    }
}
