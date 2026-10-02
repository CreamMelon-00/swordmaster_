using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>How the arena draws a mission's enemy. Presentation maps each value to an art set.</summary>
    public enum EnemyAppearance
    {
        /// <summary>The student swordsman used by stages and most missions.</summary>
        Student,
        /// <summary>A straw training dummy on a fixed stand: idle and hurt only, and it never moves.</summary>
        TrainingDummy,
    }

    /// <summary>An enemy shown on a mission briefing.</summary>
    public sealed class MissionEnemy
    {
        public MissionEnemy(string name, string silhouetteResource, EnemyAppearance appearance = EnemyAppearance.Student)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An enemy needs a name.", nameof(name));
            Name = name;
            SilhouetteResource = silhouetteResource ?? string.Empty;
            Appearance = appearance;
        }

        public string Name { get; }
        /// <summary>A Resources path to a sprite drawn as the enemy's silhouette.</summary>
        public string SilhouetteResource { get; }
        public EnemyAppearance Appearance { get; }
    }

    /// <summary>A skill a mission uses: a sheet technique named by id and looked up when it is read, so an edited
    /// sheet reaches every mission, or a fixed skill off the sheet (practice skills, 숨고르기).</summary>
    internal readonly struct MissionSkill
    {
        private readonly int tableId;
        private readonly LegacySkill fixedSkill;

        private MissionSkill(int tableId, LegacySkill fixedSkill)
        {
            this.tableId = tableId;
            this.fixedSkill = fixedSkill;
        }

        public static MissionSkill Table(int id) => new MissionSkill(id, null);

        public static implicit operator MissionSkill(LegacySkill skill)
            => new MissionSkill(0, skill ?? throw new ArgumentNullException(nameof(skill)));

        public LegacySkill Resolve() => fixedSkill ?? LegacySkillDefinitions.Skill(tableId);
    }

    /// <summary>One story mission: its briefing, its duel, its coached lessons and what winning it opens.
    /// The 서막 missions come first and open the lobby; later missions wait for a cleared stage and each open
    /// one combat feature (<see cref="Unlocks"/>).</summary>
    public sealed class PrologueMission
    {
        public const int PlayerHealth = 100;
        public const int PlayerResistance = 50;
        private readonly MissionSkill[] playerSkills, enemySkills;
        private readonly int[] enemyActionCounts;
        // Copy may name sheet techniques with LegacySkillNames tokens; it is formatted when read.
        private readonly SkillNameText title, unlockText;
        private readonly SkillNameText[] objectives;
        private readonly MissionEnemy[] enemies;
        private readonly MissionGuideBeat[] guide;

        internal PrologueMission(int number, string title, string backgroundResource,
            IReadOnlyList<string> objectives, IReadOnlyList<MissionEnemy> enemies,
            int enemyHealth, int enemyResistance, IReadOnlyList<MissionSkill> enemySkills,
            IReadOnlyList<int> enemyActionCounts, IReadOnlyList<MissionSkill> playerSkills,
            bool planningTimer, IReadOnlyList<MissionGuideBeat> guide,
            CombatFeature features = CombatFeature.LaneQ, CombatFeature unlocks = CombatFeature.None,
            int requiredClearedStage = 0, string chapter = DefaultChapter, string unlockText = null,
            EncounterKind encounter = EncounterKind.Battle)
        {
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A mission needs a title.", nameof(title));
            if (enemyHealth <= 0) throw new ArgumentOutOfRangeException(nameof(enemyHealth));
            // A zero-resistance enemy would stay broken (x2) for the whole fight.
            if (enemyResistance <= 0) throw new ArgumentOutOfRangeException(nameof(enemyResistance));
            Number = number;
            this.title = new SkillNameText(title);
            BackgroundResource = backgroundResource ?? string.Empty;
            this.objectives = Array.ConvertAll(Copy(objectives, nameof(objectives)), objective => new SkillNameText(objective));
            this.enemies = Copy(enemies, nameof(enemies));
            EnemyHealth = enemyHealth;
            EnemyResistance = enemyResistance;
            this.enemySkills = Copy(enemySkills, nameof(enemySkills));
            this.enemyActionCounts = Copy(enemyActionCounts, nameof(enemyActionCounts));
            this.playerSkills = Copy(playerSkills, nameof(playerSkills));
            if (!features.HasLane(0) && !features.HasLane(1) && !features.HasLane(2))
                throw new ArgumentException("A mission needs at least one open lane.", nameof(features));
            // The player skills' lanes come from the sheet, so they are checked when the skills are read.
            if (requiredClearedStage < 0) throw new ArgumentOutOfRangeException(nameof(requiredClearedStage));
            if (string.IsNullOrWhiteSpace(chapter)) throw new ArgumentException("A mission needs a chapter.", nameof(chapter));
            PlanningTimer = planningTimer;
            this.guide = guide == null || guide.Count == 0 ? null : Copy(guide, nameof(guide));
            Features = features;
            Unlocks = unlocks;
            RequiredClearedStage = requiredClearedStage;
            Chapter = chapter;
            this.unlockText = new SkillNameText(unlockText);
            Encounter = encounter;
        }

        public const string DefaultChapter = "깨어남";

        /// <summary>결투 or 전투: how the fight is presented (never shown, no rule effect). The 서막 is 결투.</summary>
        public EncounterKind Encounter { get; }

        public int Number { get; }
        public string Title => title.Value;
        /// <summary>A Resources path to the briefing background sprite.</summary>
        public string BackgroundResource { get; }
        public IReadOnlyList<string> Objectives => Array.ConvertAll(objectives, objective => objective.Value);
        public IReadOnlyList<MissionEnemy> Enemies => enemies;
        public int EnemyHealth { get; }
        public int EnemyResistance { get; }
        /// <summary>The enemy's skill cycle, read from the skill sheet now.</summary>
        public IReadOnlyList<LegacySkill> EnemySkills => Resolve(enemySkills);
        public IReadOnlyList<int> EnemyActionCounts => enemyActionCounts;
        /// <summary>The player's skills, read from the skill sheet now. Throws when the sheet moved one of them to a lane
        /// this mission keeps closed.</summary>
        public IReadOnlyList<LegacySkill> PlayerSkills
        {
            get
            {
                LegacySkill[] skills = Resolve(playerSkills);
                foreach (LegacySkill skill in skills)
                    if (!Features.HasLane(skill.LaneIndex))
                        throw new InvalidOperationException(
                            $"임무 {Number}의 기술 {skill.Id} {KoreanParticle.Attach(skill.Name, "이")} 이 임무에서 닫힌 열에 있습니다. 기술 시트의 '열'을 확인하세요.");
                return skills;
            }
        }
        /// <summary>Whether the 10-second planning timer runs once the coach leaves the player free.</summary>
        public bool PlanningTimer { get; }
        /// <summary>What this mission's duel allows; the 서막 has only the Q lane, plus 넘기기 from mission 2.</summary>
        public CombatFeature Features { get; }
        /// <summary>What the first win of this mission opens for every later stage and mission.</summary>
        public CombatFeature Unlocks { get; }
        /// <summary>The stage that must be cleared before this mission can be played; 0 for the 서막.</summary>
        public int RequiredClearedStage { get; }
        /// <summary>The briefing's chapter label, e.g. 서막.</summary>
        public string Chapter { get; }
        /// <summary>One line announcing what winning opens, or empty.</summary>
        public string UnlockText => unlockText.Value;
        /// <summary>How many of the Q/W/E lanes the player has in this mission.</summary>
        public int LaneCount => Features.LaneCount();
        /// <summary>Whether A or D steps are open (<see cref="Features"/> tells which).</summary>
        public bool StepsEnabled => Features.AllowsAnyStep();
        public bool BreathEnabled => Features.Has(CombatFeature.Breath);
        public EnemyAppearance EnemyAppearance => enemies[0].Appearance;
        public string IntroDialogue => $"Dialogue/mission-{Number:00}-intro";
        public string OutroDialogue => $"Dialogue/mission-{Number:00}-outro";

        public LegacyQueuedDuel CreateDuel(int seed = 1)
            => new LegacyQueuedDuel(PlayerHealth, PlayerResistance, EnemyHealth, EnemyResistance,
                PlayerSkills, EnemySkills, enemyActionCounts, seed, features: Features);

        /// <summary>A fresh coach for one attempt, or null when the mission has no coached beats.</summary>
        public MissionGuide CreateGuide() => guide == null ? null : new MissionGuide(guide);

        private static LegacySkill[] Resolve(MissionSkill[] skills)
        {
            var resolved = new LegacySkill[skills.Length];
            for (int i = 0; i < resolved.Length; i++) resolved[i] = skills[i].Resolve();
            return resolved;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source, string name)
        {
            if (source == null || source.Count == 0) throw new ArgumentException("A mission needs " + name + ".", name);
            var copy = new T[source.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                T item = source[i];
                if (item == null) throw new ArgumentException(name + " cannot contain null.", name);
                copy[i] = item;
            }
            return copy;
        }
    }
}
