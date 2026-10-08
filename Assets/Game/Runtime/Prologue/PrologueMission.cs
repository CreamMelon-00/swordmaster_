using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>How the arena draws a mission's enemy. Presentation maps each value to an art set.</summary>
    public enum EnemyAppearance
    {
        /// <summary>Iia, the student swordswoman in the prologue missions and cutscenes.</summary>
        Student,
        /// <summary>A straw training dummy on a fixed stand: idle and hurt only, and it never moves.</summary>
        TrainingDummy,
        /// <summary>The black-haired school cadet used in odd-numbered campaign stages.</summary>
        CadetA,
        /// <summary>The trouser-uniform school cadet used in even-numbered campaign stages.</summary>
        CadetB,
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

    /// <summary>A story event in the middle of a mission's battle, such as 이아's 수훈 in the 서막's last mission. The first
    /// hit that brings the enemy to <see cref="ThresholdPercent"/> of its health or below pauses the battle for
    /// <see cref="Scene"/>; the enemy then follows <see cref="EnemyScript"/> from the next planning turn (the turn in
    /// progress resumes as it was). A skipped scene still empowers the enemy, and a retry starts without it.</summary>
    public sealed class MissionEmpowerment
    {
        private readonly MissionSkill[][] loop;

        internal MissionEmpowerment(int thresholdPercent, string scene, IReadOnlyList<IReadOnlyList<MissionSkill>> loop,
            bool keepsAura, bool forcedLoss)
        {
            if (thresholdPercent <= 0 || thresholdPercent >= 100) throw new ArgumentOutOfRangeException(nameof(thresholdPercent));
            if (string.IsNullOrWhiteSpace(scene)) throw new ArgumentException("An empowerment needs a scene.", nameof(scene));
            if (loop == null || loop.Count == 0) throw new ArgumentException("An empowerment needs the enemy's turns.", nameof(loop));
            this.loop = new MissionSkill[loop.Count][];
            for (int turn = 0; turn < loop.Count; turn++)
            {
                if (loop[turn] == null || loop[turn].Count == 0)
                    throw new ArgumentException("Every enemy turn needs at least one action.", nameof(loop));
                this.loop[turn] = new MissionSkill[loop[turn].Count];
                for (int action = 0; action < loop[turn].Count; action++) this.loop[turn][action] = loop[turn][action];
            }
            ThresholdPercent = thresholdPercent;
            Scene = scene;
            KeepsAura = keepsAura;
            ForcedLoss = forcedLoss;
        }

        /// <summary>The enemy's health, in percent of its maximum, at or below which the event fires.</summary>
        public int ThresholdPercent { get; }
        /// <summary>A Resources path to the cutscene the battle pauses for.</summary>
        public string Scene { get; }
        /// <summary>The empowered enemy's turns, looping from its first; read from the skill sheet now.</summary>
        public EnemyScript EnemyScript
        {
            get
            {
                var turns = new IReadOnlyList<LegacySkill>[loop.Length];
                for (int turn = 0; turn < loop.Length; turn++)
                    turns[turn] = Array.ConvertAll(loop[turn], skill => skill.Resolve());
                return new EnemyScript(turns);
            }
        }
        /// <summary>The aura the scene lights on the enemy stays on until the battle ends.</summary>
        public bool KeepsAura { get; }
        /// <summary>From the event on the battle is a forced loss: the player's defeat completes the mission (with its
        /// outro instead of a result screen). The enemy cannot fall (<see cref="PrologueMission.EnemyHealthFloor"/>).</summary>
        public bool ForcedLoss { get; }

        /// <summary>Whether <paramref name="skill"/> is one of the empowered enemy's own techniques (the 서막's 수훈 skills:
        /// 라우다레, 베네디체레, 프레디카레): an enemy-only sheet row (구분 <c>적</c>) that her empowered turns use. Presentation
        /// opens each with a cut-in. Matched by id against the sheet as it is now, like <see cref="EnemyScript"/>.</summary>
        public bool IsSignatureSkill(LegacySkill skill)
        {
            if (skill == null) return false;
            bool enemyOnly = false;
            foreach (LegacySkill row in LegacySkillDefinitions.EnemySkills)
                if (row.Id == skill.Id)
                {
                    enemyOnly = true;
                    break;
                }
            if (!enemyOnly) return false;
            foreach (MissionSkill[] turn in loop)
                foreach (MissionSkill action in turn)
                    if (action.Resolve().Id == skill.Id) return true;
            return false;
        }

        /// <summary>Whether <paramref name="skill"/> is a signature attack of several hits (라우다레): presentation shakes the
        /// camera a little on each of its hits.</summary>
        public bool IsSignatureBarrage(LegacySkill skill)
            => skill != null && skill.Kind == LegacySkillKind.Attack && skill.AttackCount > 1 && IsSignatureSkill(skill);
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
            EncounterKind encounter = EncounterKind.Battle, string battleEnemyName = null, int enemyHealthFloor = 0,
            MissionEmpowerment empowerment = null)
        {
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A mission needs a title.", nameof(title));
            if (enemyHealth <= 0) throw new ArgumentOutOfRangeException(nameof(enemyHealth));
            // A zero-resistance enemy would stay broken (x2) for the whole fight.
            if (enemyResistance <= 0) throw new ArgumentOutOfRangeException(nameof(enemyResistance));
            if (enemyHealthFloor < 0 || enemyHealthFloor >= enemyHealth) throw new ArgumentOutOfRangeException(nameof(enemyHealthFloor));
            // Otherwise the player could still win the battle a forced loss promises to lose.
            if (empowerment != null && empowerment.ForcedLoss && enemyHealthFloor == 0)
                throw new ArgumentException("A forced loss needs an enemy that cannot fall.", nameof(enemyHealthFloor));
            if (battleEnemyName != null && battleEnemyName.Trim().Length == 0)
                throw new ArgumentException("A battle name cannot be blank.", nameof(battleEnemyName));
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
            BattleEnemyName = battleEnemyName ?? this.enemies[0].Name;
            EnemyHealthFloor = enemyHealthFloor;
            Empowerment = empowerment;
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
        /// <summary>The enemy's name in battle (status panel and any HUD label). The briefing keeps <see cref="Enemies"/>:
        /// in the 서막's last mission it says 떠돌이 기사, while the battle says 이아, who has named herself by then.</summary>
        public string BattleEnemyName { get; }
        public int EnemyHealth { get; }
        public int EnemyResistance { get; }
        /// <summary>Damage never takes the enemy's health below this for the whole battle; 0 lets it fall.</summary>
        public int EnemyHealthFloor { get; }
        /// <summary>The mid-battle story event, or null.</summary>
        public MissionEmpowerment Empowerment { get; }
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
        /// <summary>The fallback for <see cref="MissionEmpowerment.Scene"/> when that cutscene does not exist.</summary>
        public string EventDialogue => $"Dialogue/mission-{Number:00}-event";
        /// <summary>The battlefield scenes that, where they exist, replace <see cref="IntroDialogue"/> and <see cref="OutroDialogue"/>.</summary>
        public string IntroCutscene => $"Cutscene/mission-{Number:00}-intro";
        public string OutroCutscene => $"Cutscene/mission-{Number:00}-outro";
        /// <summary>Who already stands on stage when one of this mission's cutscenes starts (parse them with
        /// <see cref="CutsceneScriptParser.Parse(string, string, IReadOnlyList{CutsceneActor})"/>): Elisa and the mission's
        /// enemy, the dummy for the straw target and the knight otherwise. The intro and outro find them at their battle
        /// starting places; the event finds them where the battle paused.</summary>
        public IReadOnlyList<CutsceneActor> SceneCast => EnemyAppearance == EnemyAppearance.TrainingDummy ? DummyCast : KnightCast;
        private static readonly CutsceneActor[] DummyCast = { CutsceneActor.Elisa, CutsceneActor.Dummy };
        private static readonly CutsceneActor[] KnightCast = { CutsceneActor.Elisa, CutsceneActor.Knight };

        /// <summary>The duel for one attempt, with the enemy's health floor and the empowerment's threshold.
        /// A player's owned technique can replace its sheet definition so story retries use earned levels too.
        /// <paramref name="meshPercent"/> is 맞물림's power percent per chained skill (<see cref="LegacyQueuedDuel.MeshPercent"/>);
        /// a mission with one lane never meshes, so 맞물림 starts with mission 5's second lane.</summary>
        public LegacyQueuedDuel CreateDuel(int seed = 1, Func<LegacySkill, LegacySkill> playerSkillResolver = null,
            int meshPercent = LegacyMeshing.DefaultPercent)
        {
            IReadOnlyList<LegacySkill> playerSkills = PlayerSkills;
            if (playerSkillResolver != null)
            {
                var resolved = new LegacySkill[playerSkills.Count];
                for (int index = 0; index < resolved.Length; index++)
                    resolved[index] = playerSkillResolver(playerSkills[index]) ?? playerSkills[index];
                playerSkills = resolved;
            }
            return new LegacyQueuedDuel(PlayerHealth, PlayerResistance, EnemyHealth, EnemyResistance,
                playerSkills, EnemySkills, enemyActionCounts, seed, features: Features,
                enemyHealthFloor: EnemyHealthFloor, enemyHealthThresholdPercent: Empowerment?.ThresholdPercent ?? 0,
                meshPercent: meshPercent);
        }

        /// <summary>Whether a finished battle completes the mission: a victory always does. After a forced-loss
        /// empowerment has been applied (<paramref name="empowered"/>), so does the player's defeat; a defeat before it
        /// is an ordinary failure.</summary>
        public bool Completes(DuelMatchOutcome outcome, bool empowered)
            => outcome == DuelMatchOutcome.PlayerVictory ||
               outcome == DuelMatchOutcome.EnemyVictory && empowered && Empowerment != null && Empowerment.ForcedLoss;

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
