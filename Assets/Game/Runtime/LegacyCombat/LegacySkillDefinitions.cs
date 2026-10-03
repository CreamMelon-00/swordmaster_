using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Semantic pictogram for a skill keyword; presentation draws it.</summary>
    public enum LegacySkillSymbol { Act, Sword, Guard, Hits, Recovery, Followup, Reduction, Variance }

    /// <summary>Semantic tone for a skill keyword; presentation colours it.</summary>
    public enum LegacySkillTone { Neutral, Recovery, Followup, Reduction, HighPower, MultiHit, Variance, Defence }

    /// <summary>A skill's effects. Initial effects apply once when its slot starts, in this order:
    /// resistance recovery, then (if the opponent condition holds) direct resistance loss and ACT gain, then the buff.
    /// Broken-target damage applies separately on each hit. The sheet rejects combinations the rules cannot honour;
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
        /// <summary>When set, ACT gain and direct resistance loss need the same slot's opponent to have this property.</summary>
        public LegacySkillProperty? OpponentProperty { get; internal set; }
        /// <summary>When set, ACT gain and direct resistance loss need the same slot's opponent to be this kind.</summary>
        public LegacySkillKind? OpponentKind { get; internal set; }

        public bool HasOpponentCondition => OpponentProperty.HasValue || OpponentKind.HasValue;
        public bool HasBuff => BuffSlots > 0;

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
    /// A null Effect uses the generic text for the skill's kind and hits; a null Purpose uses the short label;
    /// a null Info uses generic badges. A set Info is shown as written.</summary>
    public sealed class LegacySkillText
    {
        public string ShortLabel { get; internal set; }
        public string Detail { get; internal set; }
        /// <summary>Lobby purpose line; falls back to <see cref="ShortLabel"/>.</summary>
        public string Purpose { get; internal set; }
        /// <summary>Lobby effect sentence.</summary>
        public string Effect { get; internal set; }
        public LegacySkillInfo Info { get; internal set; }
    }

    /// <summary>Everything that makes a technique: its numbers, its initial effect, its design tags and its copy.</summary>
    public sealed class LegacySkillDefinition
    {
        internal LegacySkillDefinition(LegacySkill skill, LegacySkillEffect effect = null, LegacySkillText text = null,
            bool highPower = false, bool variablePower = false)
        {
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            Effect = effect ?? LegacySkillEffect.None;
            Text = text ?? new LegacySkillText();
            HighPower = highPower;
            VariablePower = variablePower;
        }

        /// <summary>The level-zero skill. Stage and upgrade copies keep its id, which selects this definition.</summary>
        public LegacySkill Skill { get; }
        public LegacySkillEffect Effect { get; }
        public LegacySkillText Text { get; }
        /// <summary>Design tag for a strong attack; it does not change combat. Attacks only.</summary>
        public bool HighPower { get; }
        /// <summary>Design tag for an attack with a wide power range; it does not change combat. Attacks only.</summary>
        public bool VariablePower { get; }
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
        /// <summary>The techniques beyond the starting nine (획득 rows); stage first clears and the curriculum grant them.</summary>
        public static IReadOnlyList<LegacySkill> AcquisitionSkills => Table.AcquisitionSkills;

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
