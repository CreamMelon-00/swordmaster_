using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Semantic pictogram for a skill keyword; presentation draws it. <see cref="OneShot"/> (일회, a dashed card) is
    /// 일회성's and <see cref="Time"/> (시간, a clock) 제한시간's.</summary>
    public enum LegacySkillSymbol { Act, Sword, Guard, Hits, Recovery, Followup, Reduction, Variance, Cycle, OneShot, Time }

    /// <summary>Semantic tone for a skill keyword; presentation colours it.</summary>
    public enum LegacySkillTone { Neutral, Recovery, Followup, Reduction, HighPower, MultiHit, Variance, Defence, Cycle }

    /// <summary>A sheet row's 상대 상태 조건: the state the opposing fighter must be in for the row's resistance recovery
    /// and conditional damage multiplier (<see cref="LegacySkillEffect.OpponentState"/>).</summary>
    public enum LegacyOpponentState
    {
        /// <summary>No state condition.</summary>
        None,
        /// <summary>붕괴: the opponent's resistance is broken (zero).</summary>
        Broken,
        /// <summary>체력 N% 이하: the opponent's health is at most <see cref="LegacySkillEffect.OpponentHealthPercent"/>
        /// percent of its maximum.</summary>
        HealthAtMost,
    }

    /// <summary>How a queue skill removes earlier reservations. Cancel keeps their ACT spent; Return refunds it.</summary>
    public enum LegacyQueueRemovalMode { None, Cancel, Return }

    /// <summary>What a 사용 효과 <c>앞 &lt;대상&gt;마다 …</c> counts among the entries reserved ahead of its skill in this turn's
    /// queue (<see cref="LegacyKeywordLinks.CountAhead"/>). Targets are roles, never skill ids, so a new skill with the role
    /// counts without touching the sheet.</summary>
    public enum LegacyAheadTarget
    {
        None,
        /// <summary>숨고르기: every breath ahead, by hand or automatic, counted by entry.</summary>
        Breath,
        /// <summary>순환 기술: skills with 순환 (<see cref="LegacySkillRole.Cycle"/>), counted by kind (two copies count once).</summary>
        CycleSkill,
        /// <summary>저항 감소 기술: skills with 저항 감소 (<see cref="LegacySkillRole.DirectResistanceDamage"/>), counted by kind,
        /// whether or not their opponent condition will hold.</summary>
        ResistanceReduction,
    }

    /// <summary>A skill's effects. Initial effects apply once when its slot starts, in this order:
    /// resistance recovery (if the opponent state condition holds), then (if the opponent condition holds) direct
    /// resistance loss, the opponent's break and ACT gain, then the buff. Broken-target damage and the conditional
    /// damage multiplier apply separately on each hit. The sheet rejects combinations the rules cannot honour;
    /// see <see cref="LegacySkillSheet"/>.</summary>
    public sealed class LegacySkillEffect
    {
        public static LegacySkillEffect None { get; } = new LegacySkillEffect();

        /// <summary>Extra ACT the player recovers next turn. Enemies never recover ACT.</summary>
        public int ActGain { get; internal set; }
        /// <summary>Power bonus granted to this fighter's following slots this turn.</summary>
        public int BuffPowerPercent { get; internal set; }
        /// <summary>Received-damage reduction for the following slots; negative values make them vulnerable.</summary>
        public int BuffProtectionPercent { get; internal set; }
        /// <summary>Following slots the buff lasts, counted per slot including empty ones. Zero grants no buff.</summary>
        public int BuffSlots { get; internal set; }
        /// <summary>Percent of maximum resistance restored, rounded half to even. A self condition.</summary>
        public int ResistanceRecoveryPercent { get; internal set; }
        /// <summary>Resistance removed directly from the opponent; any excess never becomes health damage.</summary>
        public int OpponentResistanceReduction { get; internal set; }
        /// <summary>Extra health damage on each hit when the target's resistance was already broken before that hit.</summary>
        public int BrokenTargetDamagePercent { get; internal set; }
        /// <summary>The opponent's resistance drops to zero, so it is broken exactly as if a hit had broken it: it takes
        /// double health damage and recovers at the usual turn. Every hit of the slot already meets a broken target.</summary>
        public bool BreaksOpponent { get; internal set; }
        /// <summary>When set, ACT gain, direct resistance loss and the break need the same slot's opponent to have this property.</summary>
        public LegacySkillProperty? OpponentProperty { get; internal set; }
        /// <summary>When set, ACT gain, direct resistance loss and the break need the same slot's opponent to be this kind.</summary>
        public LegacySkillKind? OpponentKind { get; internal set; }
        /// <summary>상대 상태 조건: when set, the resistance recovery happens only if the opposing fighter is in this state
        /// as the recovery applies (at the slot's start), and <see cref="ConditionalDamagePercent"/> applies to a hit only
        /// if the target is in this state as that hit lands. It never gates the opponent-condition effects or the buff.</summary>
        public LegacyOpponentState OpponentState { get; internal set; }
        /// <summary>For <see cref="LegacyOpponentState.HealthAtMost"/>, the percent of maximum health (1-99) the opponent's
        /// health must be at or below; zero otherwise.</summary>
        public int OpponentHealthPercent { get; internal set; }
        /// <summary>조건 피해 배율: a percent over 100 that multiplies each hit's health damage while
        /// <see cref="OpponentState"/> holds against the target at that hit, after the broken target's doubling, the
        /// defensive pressure's halving and <see cref="BrokenTargetDamagePercent"/>. Zero for none; attacks only.</summary>
        public int ConditionalDamagePercent { get; internal set; }
        /// <summary>Immediately removes all earlier queued skills when equipped. Cancel keeps their ACT spent;
        /// Return refunds their ACT. This is separate from any following-skill power effect.</summary>
        public LegacyQueueRemovalMode QueueRemovalMode { get; internal set; }
        /// <summary>When positive, the next used skill's power is multiplied by the number of removed skills,
        /// capped at this value. Zero means queue removal grants no power multiplier.</summary>
        public int QueuePowerMultiplierCap { get; internal set; }
        /// <summary>Base power added per previous use of this skill in the current battle.</summary>
        public int CyclePowerPerUse { get; internal set; }
        /// <summary>ACT cost added per previous use of this skill in the current battle.</summary>
        public int CycleCostPerUse { get; internal set; }
        /// <summary>Maximum previous uses counted for this skill; zero means it has no Cycle effect.</summary>
        public int CycleMaxCount { get; internal set; }
        public bool HasCycle => CycleMaxCount > 0;
        /// <summary>예약 효과 <c>순환 +1</c>: when the turn is committed, the first copy of this skill in the committed queue
        /// raises by one the 순환 count of every kind of cycling skill reserved ahead of it, each up to its cap
        /// (<see cref="LegacyKeywordLinks.ApplyCycleFeed"/>). A source or target taken off the queue before the commit gives
        /// or gets nothing.</summary>
        public bool ReserveCycleFeed { get; internal set; }
        /// <summary>예약 효과 <c>숨고르기 +1</c>: reserving this skill also reserves one 숨고르기 right after it, without using
        /// a hand breath; once per turn (<see cref="LegacyReserveEffect.Breath"/>). The breath stays when the skill is later
        /// cancelled or returned, as every breath does.</summary>
        public bool ReserveBreath { get; internal set; }
        /// <summary>사용 효과 <c>앞 &lt;대상&gt;마다 위력 +n (최대 m)</c>: what is counted ahead of this skill in the committed
        /// queue; <see cref="LegacyAheadTarget.None"/> for no such effect.</summary>
        public LegacyAheadTarget AheadPowerTarget { get; internal set; }
        /// <summary>Base power added per counted entry (n, 1-10). Added with the 순환 bonus, before buffs, 맞물림 and
        /// multipliers; only the first copy of this skill in the queue gains it.</summary>
        public int AheadPowerPerUnit { get; internal set; }
        /// <summary>The most entries counted (m, 1-4).</summary>
        public int AheadPowerMax { get; internal set; }
        /// <summary>사용 효과 <c>자동 회피</c>: in this skill's slot every hit of the opponent's attack (a counter filling the
        /// slot included) is dodged. Not a step: it costs no ACT, never narrows a step window, and the slot ignores the dodge
        /// key. Attacks only.</summary>
        public bool AutoDodge { get; internal set; }
        /// <summary>사용 효과 보호 소모 위력 +n: removes every Protection status before either fighter's slot
        /// multipliers are captured. Adds n to this attack's base power only if protection was actually removed;
        /// PowerUp and Vulnerability remain. Reservation alone never spends a status.</summary>
        public int ConsumeProtectionPowerBonus { get; internal set; }
        public bool HasProtectionSpend => ConsumeProtectionPowerBonus > 0;
        /// <summary>사용 효과 앞 공격 총타수 N 이상 위력 +M: counts each distinct attack id ahead once, using
        /// its AttackCount. Waits, defences and this skill's own id do not count. Only the first copy gains the bonus.</summary>
        public int AheadAttackHitsRequired { get; internal set; }
        public int AheadAttackHitsPowerBonus { get; internal set; }
        public bool HasAheadHitPower => AheadAttackHitsRequired > 0;
        /// <summary>예약 효과 <c>일회성 &lt;ID&gt; ×n</c>: the 일회성 row whose copies reserving this skill sets at the front of that
        /// row's lane, once per turn (<see cref="LegacyReserveEffect.Temporary"/>); 0 for none. The copies stay when this skill
        /// is later cancelled or returned, and every one left goes as the next planning turn begins.</summary>
        public int ReserveTemporarySkillId { get; internal set; }
        /// <summary>How many copies (n, 1-3).</summary>
        public int ReserveTemporaryCount { get; internal set; }
        /// <summary>예약 효과 <c>제한시간 -n초</c>: the planning seconds reserving this skill spends while the clock runs, every
        /// time (a cost, never refunded); 0 for none. The rules leave the clock to the screens
        /// (<see cref="LegacyPlanningClock"/>).</summary>
        public int ReservePlanningSecondsCost { get; internal set; }
        /// <summary>사용 효과 <c>앞 &lt;대상&gt;마다 다음 턴 제한시간 +n초 (최대 m)</c>: what is counted ahead of this skill in the
        /// committed queue; <see cref="LegacyAheadTarget.None"/> for no such effect. Only the queue's first copy gains.</summary>
        public LegacyAheadTarget AheadTimeTarget { get; internal set; }
        /// <summary>Next turn's planning seconds per counted entry (n, 1-3).</summary>
        public int AheadTimePerUnit { get; internal set; }
        /// <summary>The most entries counted (m, 1-3).</summary>
        public int AheadTimeMax { get; internal set; }
        public bool HasAheadPower => AheadPowerTarget != LegacyAheadTarget.None;
        public bool HasAheadTime => AheadTimeTarget != LegacyAheadTarget.None;
        public bool HasTemporarySpawn => ReserveTemporarySkillId > 0;
        /// <summary>Whether the row writes its 예약 효과 cell.</summary>
        public bool HasReserveEffect => ReserveCycleFeed || ReserveBreath || HasTemporarySpawn || ReservePlanningSecondsCost > 0;
        /// <summary>A 예약 효과 that adds something as the skill is reserved (순환 +1, 숨고르기 +1, 일회성), as opposed to the
        /// time cost; a remover may not carry one, since taking its own work back and reserving again would repeat it.</summary>
        public bool HasReserveGain => ReserveCycleFeed || ReserveBreath || HasTemporarySpawn;
        /// <summary>Whether the row writes its 사용 효과 cell.</summary>
        public bool HasUseEffect => HasAheadPower || HasAheadTime || AutoDodge || HasProtectionSpend || HasAheadHitPower;

        public bool HasOpponentCondition => OpponentProperty.HasValue || OpponentKind.HasValue;
        public bool HasOpponentStateCondition => OpponentState != LegacyOpponentState.None;
        public bool HasBuff => BuffSlots > 0;

        /// <summary>Whether <paramref name="opponent"/> is in <see cref="OpponentState"/> now; false without a state
        /// condition or an opponent. Health compares exactly: at most N percent means health × 100 ≤ N × maximum.</summary>
        public bool OpponentStateHolds(LegacyFighterState opponent)
        {
            if (opponent == null) return false;
            switch (OpponentState)
            {
                case LegacyOpponentState.Broken: return opponent.IsResistanceBroken;
                case LegacyOpponentState.HealthAtMost:
                    return (long)opponent.Health * 100 <= (long)OpponentHealthPercent * opponent.MaxHealth;
                default: return false;
            }
        }

        /// <summary>The roles this effect implies; the skill's kind, hits and design tags add the rest.</summary>
        public LegacySkillRole Roles
        {
            get
            {
                LegacySkillRole roles = LegacySkillRole.None;
                if (ActGain > 0) roles |= LegacySkillRole.ActRecovery;
                if (HasBuff && BuffPowerPercent > 0) roles |= LegacySkillRole.FollowupPower;
                if (HasBuff && BuffProtectionPercent > 0) roles |= LegacySkillRole.DamageReduction;
                if (HasBuff && BuffProtectionPercent < 0) roles |= LegacySkillRole.Vulnerability;
                if (ResistanceRecoveryPercent > 0) roles |= LegacySkillRole.ResistanceRecovery;
                if (OpponentResistanceReduction > 0) roles |= LegacySkillRole.DirectResistanceDamage;
                if (BrokenTargetDamagePercent > 0) roles |= LegacySkillRole.BrokenTargetDamage;
                if (BreaksOpponent) roles |= LegacySkillRole.OpponentBreak;
                if (ConditionalDamagePercent > 0) roles |= LegacySkillRole.ConditionalDamage;
                if (QueueRemovalMode == LegacyQueueRemovalMode.Cancel) roles |= LegacySkillRole.QueueCancel;
                if (QueueRemovalMode == LegacyQueueRemovalMode.Return) roles |= LegacySkillRole.QueueReturn;
                if (HasCycle) roles |= LegacySkillRole.Cycle;
                if (ReserveCycleFeed) roles |= LegacySkillRole.CycleFeed;
                if (ReserveBreath) roles |= LegacySkillRole.AutoBreath;
                if (HasAheadPower) roles |= LegacySkillRole.AheadPower;
                if (AutoDodge) roles |= LegacySkillRole.AutoDodge;
                if (HasTemporarySpawn) roles |= LegacySkillRole.TemporarySpawn;
                if (ReservePlanningSecondsCost > 0) roles |= LegacySkillRole.PlanningTimeCost;
                if (HasAheadTime) roles |= LegacySkillRole.NextTurnTime;
                if (HasProtectionSpend) roles |= LegacySkillRole.ProtectionSpend;
                if (HasAheadHitPower) roles |= LegacySkillRole.AheadHitPower;
                return roles;
            }
        }
    }

    /// <summary>Two keyword badges and an effect summary for a skill's detail view.</summary>
    public sealed class LegacySkillInfo
    {
        public string Main { get; internal set; }
        /// <summary>Replaces <see cref="Main"/> when describing an enemy's skill; null keeps it.</summary>
        public string EnemyMain { get; internal set; }
        public LegacySkillSymbol MainSymbol { get; internal set; }
        public LegacySkillTone MainTone { get; internal set; }
        public string Secondary { get; internal set; } = string.Empty;
        /// <summary>Replaces <see cref="Secondary"/> when describing an enemy's skill; null keeps it.</summary>
        public string EnemySecondary { get; internal set; }
        public LegacySkillSymbol SecondarySymbol { get; internal set; } = LegacySkillSymbol.Hits;
        public LegacySkillTone SecondaryTone { get; internal set; }
        public string Description { get; internal set; }
    }

    /// <summary>Player-facing copy. Every table row writes its own ShortLabel and Detail, so they never follow a copy's shape.
    /// A null Effect uses the generic text for the skill's kind and hits; a null Info uses generic badges. A set Info is
    /// shown as written.</summary>
    public sealed class LegacySkillText
    {
        public string ShortLabel { get; internal set; }
        public string Detail { get; internal set; }
        /// <summary>Lobby effect sentence.</summary>
        public string Effect { get; internal set; }
        public LegacySkillInfo Info { get; internal set; }
    }

    /// <summary>Everything that makes a technique: its numbers, its initial effect, its design tags and its copy.</summary>
    public sealed class LegacySkillDefinition
    {
        internal LegacySkillDefinition(LegacySkill skill, LegacySkillEffect effect = null, LegacySkillText text = null,
            bool highPower = false, bool variablePower = false, int counterUsesPerTurn = 0)
        {
            if (counterUsesPerTurn < 0) throw new ArgumentOutOfRangeException(nameof(counterUsesPerTurn));
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            Effect = effect ?? LegacySkillEffect.None;
            Text = text ?? new LegacySkillText();
            HighPower = highPower;
            VariablePower = variablePower;
            CounterUsesPerTurn = counterUsesPerTurn;
        }

        /// <summary>The level-zero skill. Stage and upgrade copies keep its id, which selects this definition.</summary>
        public LegacySkill Skill { get; }
        public LegacySkillEffect Effect { get; }
        public LegacySkillText Text { get; }
        /// <summary>Design tag for a strong attack; it does not change combat. Attacks only.</summary>
        public bool HighPower { get; }
        /// <summary>Design tag for an attack with a wide power range; it does not change combat. Attacks only.</summary>
        public bool VariablePower { get; }
        /// <summary>Positive for a player technique equipped in the separate counter slot; zero for an ordinary skill.
        /// Counter techniques cost no ACT and answer at most this many empty-slot attacks each turn.</summary>
        public int CounterUsesPerTurn { get; }
        public bool IsCounter => CounterUsesPerTurn > 0;
    }

    /// <summary>The single table of implemented techniques, read from the skill sheet
    /// (<see cref="SheetAssetPath"/>, see <see cref="LegacySkillSheet"/>). Adding a technique means adding one row there;
    /// combat effects, conditions, roles and every skill text read it by id.</summary>
    public static class LegacySkillDefinitions
    {
        /// <summary>The sheet as a Resources path; Presentation installs a source that loads it.</summary>
        public const string SheetResourcePath = "Skills/skills";
        /// <summary>The sheet's file, relative to the project root.</summary>
        public const string SheetAssetPath = "Assets/Game/Resources/Skills/skills.csv";

        private static readonly object gate = new object();
        private static Func<string> source;
        private static LegacySkillTable table;
        // A failed load is kept and thrown again on every access until Install or Reload, so the type itself never breaks.
        // An unreadable file is not kept (see Table).
        private static ExceptionDispatchInfo failure;
        private static int version;

        /// <summary>Every technique in sheet row order.</summary>
        public static IReadOnlyList<LegacySkillDefinition> All => Table.All;
        /// <summary>The nine starting techniques (시작 rows), three per lane. Their sheet order within a lane is the default
        /// loadout order; everything else names a technique by id.</summary>
        public static IReadOnlyList<LegacySkill> InitialSkills => Table.InitialSkills;
        /// <summary>Starting counter techniques, owned separately from the default Q/W/E loadout.</summary>
        public static IReadOnlyList<LegacySkill> InitialCounterSkills => Table.InitialCounterSkills;
        /// <summary>The techniques beyond the starting nine (획득 rows); stage first clears, the curriculum and the academy
        /// field's pickups grant them.</summary>
        public static IReadOnlyList<LegacySkill> AcquisitionSkills => Table.AcquisitionSkills;
        /// <summary>The enemy-only techniques (적 rows), such as 이아's 라우다레 in the 서막's last mission. The player never
        /// owns, equips or learns them; enemies name them by id.</summary>
        public static IReadOnlyList<LegacySkill> EnemySkills => Table.EnemySkills;
        /// <summary>The 일회성 rows, such as 갈아내기: a skill's 예약 효과 sets their copies at the front of a lane for one turn.
        /// The player never owns, equips or learns them, and they never reach the versus pool or the curriculum.</summary>
        public static IReadOnlyList<LegacySkill> TemporarySkills => Table.TemporarySkills;

        /// <summary>Whether <paramref name="skill"/> is a 일회성 row's (by id): used once and gone, never sent to the back of
        /// its lane. False for breathing and unknown skills.</summary>
        public static bool IsTemporary(LegacySkill skill)
            => skill != null && !skill.IsWait && Table.IsTemporary(Table.Find(skill.Id));

        /// <summary>The loaded table. The first access reads the sheet; a sheet with problems throws its
        /// <see cref="SkillSheetException"/> on every access until <see cref="Install"/> or <see cref="Reload"/>.
        /// A file that could not be opened (<see cref="IOException"/>) is tried again on the next access.</summary>
        public static LegacySkillTable Table
        {
            get
            {
                lock (gate)
                {
                    if (table != null) return table;
                    failure?.Throw();
                    try
                    {
                        table = LegacySkillSheet.Parse(SheetAssetPath, (source ?? ReadSheetFile)());
                        return table;
                    }
                    // A file another program holds is retried on the next access instead; nothing re-imports the sheet
                    // when that program merely closes it.
                    catch (Exception exception) when (!(exception is IOException || exception is UnauthorizedAccessException))
                    {
                        failure = ExceptionDispatchInfo.Capture(exception);
                        throw;
                    }
                }
            }
        }

        /// <summary>The definition selected by a skill's id, or null for breathing and unknown skills.</summary>
        public static LegacySkillDefinition Find(LegacySkill skill) =>
            skill == null || skill.IsWait ? null : Find(skill.Id);

        /// <summary>The definition with this id, or null when the sheet has none.</summary>
        public static LegacySkillDefinition Find(int id) => Table.Find(id);

        /// <summary>The level-zero technique with this id; throws when the sheet has no such row.</summary>
        public static LegacySkill Skill(int id)
            => Find(id)?.Skill ?? throw new InvalidOperationException(
                $"기술 시트 '{SheetAssetPath}'에 ID {id} 기술이 없습니다. 코드가 이 ID를 쓰니 ID를 바꾸거나 행을 지우지 마세요.");

        /// <summary>Changes on every <see cref="Install"/> and <see cref="Reload"/>, so copy formatted from the table
        /// (<see cref="SkillNameText"/>) knows when to format again.</summary>
        internal static int Version => version;

        /// <summary>Sets where the sheet text comes from and forgets the loaded table. Null goes back to reading
        /// <see cref="SheetAssetPath"/> under the working directory (the project root in the Unity editor and in tests).</summary>
        public static void Install(Func<string> sheetSource)
        {
            lock (gate)
            {
                source = sheetSource;
                table = null;
                failure = null;
                version++;
            }
        }

        /// <summary>Forgets the loaded table (or its failure); the next access reads the sheet again.</summary>
        public static void Reload()
        {
            lock (gate)
            {
                table = null;
                failure = null;
                version++;
            }
        }

        private static string ReadSheetFile()
        {
            string path = Path.Combine(Environment.CurrentDirectory, SheetAssetPath);
            if (!File.Exists(path)) throw new InvalidOperationException("기술 시트 파일이 없습니다: " + path);
            // Shared reading, so a sheet left open in Excel still loads.
            return SheetFile.ReadAllText(path);
        }
    }
}
