using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    public enum CampaignPhase { Battle, Maintenance, Failed, Completed, Lobby }

    /// <summary>One eight-stage run with its curriculum. Combat rules remain in LegacyQueuedDuel.
    /// Currency is still earned and saved but has no use at the moment (the shop was replaced by the curriculum).</summary>
    public sealed class CampaignRun
    {
        public const int TrainingBaseHealth = 50;
        public const int TrainingRoundLimit = 5;
        // The first six starting attacks, in sheet ID order; the sheet supplies their current names and effects.
        private static readonly int[] BasicRhythmSkillIds = { 1, 2, 3, 4, 5, 6 };
        private static readonly CampaignStage[] stages =
        {
            new CampaignStage(1, "복도 입구"),
            new CampaignStage(2, "첫 번째 창가"),
            new CampaignStage(3, "햇살 드는 복도"),
            new CampaignStage(4, "석조 기둥 사이"),
            new CampaignStage(5, "가스등 아래"),
            new CampaignStage(6, "긴 창가"),
            new CampaignStage(7, "복도 안쪽"),
            new CampaignStage(8, "복도 끝 결투"),
        };
        internal static IReadOnlyList<CampaignStage> StageDefinitions => stages;

        private readonly List<CampaignOwnedSkill> ownedSkills = new List<CampaignOwnedSkill>();
        private readonly CurriculumProgress curriculum;
        private readonly bool[] clearedStages = new bool[stages.Length];
        private readonly List<CampaignOwnedSkill>[] equippedLanes =
        {
            new List<CampaignOwnedSkill>(), new List<CampaignOwnedSkill>(), new List<CampaignOwnedSkill>(),
        };
        private readonly IReadOnlyList<CampaignOwnedSkill>[] equippedLaneViews;
        private readonly CampaignOwnedSkill[][] loadoutSlots =
        {
            new CampaignOwnedSkill[3], new CampaignOwnedSkill[3], new CampaignOwnedSkill[3],
        };
        private bool trainingBattle;

        public CampaignRun() : this(CampaignCurriculum.Default) { }

        /// <summary>Uses a supplied curriculum tree for authored variants and rule tests.</summary>
        public CampaignRun(CurriculumTree curriculumTree)
        {
            curriculum = new CurriculumProgress(curriculumTree);
            OwnedSkills = ownedSkills.AsReadOnly();
            equippedLaneViews = new IReadOnlyList<CampaignOwnedSkill>[equippedLanes.Length];
            for (int i = 0; i < equippedLanes.Length; i++) equippedLaneViews[i] = equippedLanes[i].AsReadOnly();
            Reset();
        }

        public CampaignPhase Phase { get; private set; }
        public int StageNumber { get; private set; }
        public int StageCount => stages.Length;
        public int Currency { get; private set; }
        public int LastReward { get; private set; }
        public int HighestUnlockedStage { get; private set; }
        public int ClearedStageCount { get; private set; }
        /// <summary>Successful dummy fights. Only this count, not an in-progress fight, is saved.</summary>
        public int TrainingVictoryCount { get; private set; }
        /// <summary>The target's health for the next training fight, capped at the combat engine's int limit.</summary>
        public int TrainingDummyHealth
        {
            get
            {
                int health = TrainingBaseHealth;
                for (int i = 0; i < TrainingVictoryCount; i++)
                {
                    if (health > int.MaxValue / 2) return int.MaxValue;
                    health *= 2;
                }
                return health;
            }
        }
        public bool IsTrainingBattle => trainingBattle;
        /// <summary>Training opens when stage 3 can be entered, including the story's stage gate.</summary>
        public bool IsTrainingUnlocked => HighestUnlockedStage >= 3 && StageLimit >= 3;
        public bool CanStartTraining => CanEditLoadout && CanEnterStage(3);
        public int EquippedSkillCount => equippedLanes[0].Count + equippedLanes[1].Count + equippedLanes[2].Count;
        public CampaignStage CurrentStage => stages[StageNumber - 1];
        /// <summary>Starting skills first, then stage and curriculum skills in the order they were granted.</summary>
        public IReadOnlyList<CampaignOwnedSkill> OwnedSkills { get; }
        public CampaignOwnedSkill GetOwnedSkill(int skillId) => FindOwnedSkill(skillId);

        /// <summary>A real slot clash trains its equipped player skill once, regardless of the number of hits.
        /// Matching the owned instance also excludes sheet and enemy skills that happen to have the same ID.</summary>
        public bool TryGainClashExperience(LegacySkill playerSkill, LegacySkill enemySkill)
        {
            if (playerSkill == null || enemySkill == null || playerSkill.IsWait || enemySkill.IsWait) return false;
            CampaignOwnedSkill owned = FindOwnedSkill(playerSkill.Id);
            return owned != null && ReferenceEquals(owned.Skill, playerSkill) && owned.GainClashExperience();
        }
        public CurriculumProgress Curriculum => curriculum;
        /// <summary>Permanent combat bonuses from completed curriculum nodes. Derived from completion order so
        /// restoring an older save and resetting the curriculum cannot leave a separate bonus value behind.</summary>
        public CurriculumStatReward CurriculumStats
        {
            get
            {
                var total = default(CurriculumStatReward);
                foreach (string id in curriculum.Completed)
                    total += curriculum.Tree.Find(id).StatReward;
                return total;
            }
        }
        /// <summary>The node the last finished battle completed, or null.</summary>
        public CurriculumNode LastCompletedCurriculumNode { get; private set; }
        /// <summary>How the last stage battle ended, or null when none has finished since it began.</summary>
        public DuelMatchOutcome? LastOutcome { get; private set; }
        /// <summary>The combat features stage battles allow. Story missions open them; the default (everything) is
        /// what stages had before missions unlocked features, and what tests and direct API use still get.</summary>
        public CombatFeature Features { get; private set; } = CombatFeature.All;
        /// <summary>The highest stage the story allows (the next one waits for its mission). Unlimited by default.</summary>
        public int StageLimit { get; private set; } = int.MaxValue;

        /// <summary>Applies what the story has opened. Not part of <see cref="Reset"/>: a new journey keeps the story.</summary>
        public void SetProgression(CombatFeature features, int stageLimit)
        {
            if (!features.HasLane(0) && !features.HasLane(1) && !features.HasLane(2))
                throw new ArgumentException("At least one lane must be open.", nameof(features));
            if (stageLimit < 0) throw new ArgumentOutOfRangeException(nameof(stageLimit));
            Features = features;
            StageLimit = stageLimit;
        }

        /// <summary>Back to everything open and no stage limit.</summary>
        public void ClearProgression() => SetProgression(CombatFeature.All, int.MaxValue);

        /// <summary>Whether the lane's skills go to battle. Closed lanes keep their saved skills for later.</summary>
        public bool IsLaneOpen(int laneIndex) => Features.HasLane(laneIndex);

        /// <summary>Whether the curriculum exists for the player: only once every lane is open (the story opens W, the
        /// last one, with mission 8; outside the story everything is open). While it is closed, selecting and resetting
        /// are refused and battles count toward nothing; saved progress is kept as it is for the day it opens.</summary>
        public bool IsCurriculumOpen => OpensCurriculum(Features);

        /// <summary>Whether these features open the curriculum: all three lanes.</summary>
        public static bool OpensCurriculum(CombatFeature features) => features.LaneCount() == 3;

        /// <summary>An unlocked stage that still waits for a story mission.</summary>
        public bool IsStageWaitingForMission(int stageNumber)
            => IsValidStage(stageNumber) && stageNumber <= HighestUnlockedStage && stageNumber > StageLimit;
        public bool HasLoadoutChanges
        {
            get
            {
                for (int lane = 0; lane < loadoutSlots.Length; lane++)
                    for (int slot = 0; slot < loadoutSlots[lane].Length; slot++)
                        if (slot >= equippedLanes[lane].Count ||
                            !ReferenceEquals(loadoutSlots[lane][slot], equippedLanes[lane][slot])) return true;
                return false;
            }
        }
        public bool CanSaveLoadout => CanEditLoadout && IsValidLoadout();

        public CampaignStage GetStage(int stageNumber)
        {
            if (!IsValidStage(stageNumber)) throw new ArgumentOutOfRangeException(nameof(stageNumber));
            return stages[stageNumber - 1];
        }

        public bool IsStageCleared(int stageNumber)
            => IsValidStage(stageNumber) && clearedStages[stageNumber - 1];

        public int GetStageReward(int stageNumber)
            => IsValidStage(stageNumber) ? stages[stageNumber - 1].Reward / (IsStageCleared(stageNumber) ? 2 : 1) : 0;

        public bool CanStartStage(int stageNumber)
            => CanEditLoadout && CanEnterStage(stageNumber);

        public bool TryStartStage(int stageNumber)
        {
            if (!CanStartStage(stageNumber)) return false;
            BeginStage(stageNumber);
            return true;
        }

        public bool TryStartTraining()
        {
            if (!CanStartTraining) return false;
            StageNumber = 3;
            trainingBattle = true;
            LastReward = 0;
            LastCompletedCurriculumNode = null;
            LastOutcome = null;
            Phase = CampaignPhase.Battle;
            return true;
        }

        public bool ReturnToLobby()
        {
            if (Phase == CampaignPhase.Battle) return false;
            Phase = CampaignPhase.Lobby;
            trainingBattle = false;
            return true;
        }

        public bool TryAbandonBattle()
        {
            if (Phase != CampaignPhase.Battle) return false;
            LastReward = 0;
            Phase = CampaignPhase.Lobby;
            trainingBattle = false;
            return true;
        }

        public IReadOnlyList<CampaignOwnedSkill> GetEquippedLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= equippedLanes.Length) throw new ArgumentOutOfRangeException(nameof(laneIndex));
            return equippedLaneViews[laneIndex];
        }

        public bool IsSkillEquipped(int skillId)
        {
            foreach (List<CampaignOwnedSkill> lane in equippedLanes)
                foreach (CampaignOwnedSkill owned in lane)
                    if (owned.SkillId == skillId) return true;
            return false;
        }

        public CampaignOwnedSkill GetLoadoutSlot(int lane, int slot)
        {
            if (lane < 0 || lane >= loadoutSlots.Length) throw new ArgumentOutOfRangeException(nameof(lane));
            if (slot < 0 || slot >= loadoutSlots[lane].Length) throw new ArgumentOutOfRangeException(nameof(slot));
            return loadoutSlots[lane][slot];
        }

        public int GetLoadoutCount(int lane)
        {
            if (lane < 0 || lane >= loadoutSlots.Length) throw new ArgumentOutOfRangeException(nameof(lane));
            int count = 0;
            foreach (CampaignOwnedSkill owned in loadoutSlots[lane]) if (owned != null) count++;
            return count;
        }

        public bool IsSkillInLoadout(int skillId)
        {
            foreach (CampaignOwnedSkill[] lane in loadoutSlots)
                foreach (CampaignOwnedSkill owned in lane)
                    if (owned != null && owned.SkillId == skillId) return true;
            return false;
        }

        public bool TryPlaceLoadoutSkill(int skillId, int lane, int slot)
        {
            if (!CanEditLoadout || lane < 0 || lane >= loadoutSlots.Length ||
                slot < 0 || slot >= loadoutSlots[lane].Length || !IsLaneOpen(lane)) return false;
            CampaignOwnedSkill owned = FindOwnedSkill(skillId);
            if (owned == null || owned.Skill.LaneIndex != lane) return false;
            CampaignOwnedSkill[] slots = loadoutSlots[lane];
            for (int source = 0; source < slots.Length; source++)
                if (ReferenceEquals(slots[source], owned))
                {
                    CampaignOwnedSkill displaced = slots[slot];
                    slots[slot] = owned;
                    slots[source] = displaced;
                    return true;
                }
            slots[slot] = owned;
            return true;
        }

        public bool TrySaveLoadout()
        {
            if (!CanSaveLoadout) return false;
            // Validate every slot before changing any committed lane. Existing views
            // keep observing the same lists, but only see complete saved loadouts.
            for (int lane = 0; lane < equippedLanes.Length; lane++)
            {
                equippedLanes[lane].Clear();
                equippedLanes[lane].AddRange(loadoutSlots[lane]);
            }
            return true;
        }

        public bool TryResetLoadout()
        {
            if (!CanEditLoadout) return false;
            ResetLoadoutDraft();
            return true;
        }

        public bool TryEquipSkill(int skillId)
        {
            if (!CanEditLoadout || IsSkillInLoadout(skillId)) return false;
            CampaignOwnedSkill owned = FindOwnedSkill(skillId);
            if (owned == null || !IsLaneOpen(owned.Skill.LaneIndex)) return false;
            CampaignOwnedSkill[] lane = loadoutSlots[owned.Skill.LaneIndex];
            for (int slot = 0; slot < lane.Length; slot++)
                if (lane[slot] == null)
                {
                    lane[slot] = owned;
                    return true;
                }
            return false;
        }

        public bool TryUnequipSkill(int skillId)
        {
            if (!CanEditLoadout) return false;
            for (int laneIndex = 0; laneIndex < loadoutSlots.Length; laneIndex++)
            {
                CampaignOwnedSkill[] lane = loadoutSlots[laneIndex];
                for (int i = 0; i < lane.Length; i++)
                    if (lane[i] != null && lane[i].SkillId == skillId)
                    {
                        if (!IsLaneOpen(laneIndex)) return false;
                        lane[i] = null;
                        return true;
                    }
            }
            return false;
        }

        public bool TryMoveEquippedSkill(int skillId, int direction)
        {
            if (!CanEditLoadout || (direction != -1 && direction != 1)) return false;
            for (int laneIndex = 0; laneIndex < loadoutSlots.Length; laneIndex++)
            {
                CampaignOwnedSkill[] lane = loadoutSlots[laneIndex];
                for (int i = 0; i < lane.Length; i++)
                    if (lane[i] != null && lane[i].SkillId == skillId)
                    {
                        if (!IsLaneOpen(laneIndex)) return false;
                        int target = i + direction;
                        if (target < 0 || target >= lane.Length) return false;
                        CampaignOwnedSkill other = lane[target];
                        lane[target] = lane[i];
                        lane[i] = other;
                        return true;
                    }
            }
            return false;
        }

        public bool TryCompleteBattle(DuelMatchOutcome outcome)
        {
            if (Phase != CampaignPhase.Battle) return false;
            if (outcome != DuelMatchOutcome.PlayerVictory && outcome != DuelMatchOutcome.EnemyVictory
                && outcome != DuelMatchOutcome.Draw) return false;
            if (trainingBattle)
            {
                LastOutcome = outcome;
                LastReward = 0;
                LastCompletedCurriculumNode = null;
                if (outcome == DuelMatchOutcome.PlayerVictory && TrainingVictoryCount < int.MaxValue)
                    TrainingVictoryCount++;
                Phase = CampaignPhase.Maintenance;
                return true;
            }
            // The node this battle completes must find its techniques in the sheet. They are looked up before anything
            // changes, so a sheet without one stops here with the sheet's own error and a retry cannot count the battle twice.
            CurriculumNode completing = IsCurriculumOpen ? curriculum.CompletesNext : null;
            if (completing != null)
                foreach (int skillId in completing.SkillIds) _ = LegacySkillDefinitions.Skill(skillId);
            int stageSkillRewardId = outcome == DuelMatchOutcome.PlayerVictory && !clearedStages[StageNumber - 1]
                ? CurrentStage.FirstClearSkillId : 0;
            if (stageSkillRewardId != 0) _ = LegacySkillDefinitions.Skill(stageSkillRewardId);

            LastOutcome = outcome;
            // Every finished battle counts toward the node in progress, as days pass in a national focus. A closed
            // curriculum counts nothing, even a node an older save left in progress.
            LastCompletedCurriculumNode = IsCurriculumOpen ? curriculum.RecordBattle() : null;
            if (LastCompletedCurriculumNode != null) GrantSkills(LastCompletedCurriculumNode);
            if (outcome != DuelMatchOutcome.PlayerVictory)
            {
                LastReward = 0;
                Phase = CampaignPhase.Failed;
                return true;
            }

            LastReward = GetStageReward(StageNumber);
            Currency += LastReward;
            if (!clearedStages[StageNumber - 1])
            {
                clearedStages[StageNumber - 1] = true;
                ClearedStageCount++;
                HighestUnlockedStage = Math.Max(HighestUnlockedStage, Math.Min(StageNumber + 1, StageCount));
                if (stageSkillRewardId != 0) GrantSkill(stageSkillRewardId);
            }
            Phase = StageNumber == StageCount ? CampaignPhase.Completed : CampaignPhase.Maintenance;
            return true;
        }

        public bool TryStartNextStage()
        {
            if (trainingBattle || Phase != CampaignPhase.Maintenance || StageNumber >= StageCount) return false;
            return TryStartStage(StageNumber + 1);
        }

        public bool RetryCurrentStage()
        {
            if (Phase != CampaignPhase.Failed || !CanEnterStage(StageNumber)) return false;
            BeginStage(StageNumber);
            return true;
        }

        /// <summary>Makes the node the one in progress (lobby or maintenance, curriculum open). The choice can still
        /// change until a battle counts.</summary>
        public bool TrySelectCurriculumNode(string nodeId) => IsCurriculumOpen && CanEditLoadout && curriculum.TrySelect(nodeId);

        /// <summary>Clears the whole curriculum (lobby or maintenance, curriculum open): completed nodes, the node in
        /// progress and every skill they granted. Stage first-clear skills remain owned. Saved lanes that lose a skill
        /// are refilled with that lane's starting skills.</summary>
        public bool TryResetCurriculum()
        {
            if (!IsCurriculumOpen || !CanEditLoadout || curriculum.CompletedCount == 0 && curriculum.Active == null) return false;
            curriculum.Reset();
            LastCompletedCurriculumNode = null;
            ownedSkills.RemoveAll(owned => !IsStartingSkill(owned.SkillId) && !IsUnlockedStageSkill(owned.SkillId));
            RefillLanesWithStartingSkills();
            ResetLoadoutDraft();
            return true;
        }

        /// <param name="meshPercent">맞물림's power percent per chained skill for this duel
        /// (<see cref="LegacyQueuedDuel.MeshPercent"/>); 0 switches it off.</param>
        public LegacyQueuedDuel CreateDuel(int randomSeed = 1, int meshPercent = LegacyMeshing.DefaultPercent)
        {
            CurriculumStatReward stats = CurriculumStats;
            var playerSkills = new LegacySkill[EquippedSkillCount];
            int index = 0;
            foreach (List<CampaignOwnedSkill> lane in equippedLanes)
                foreach (CampaignOwnedSkill owned in lane) playerSkills[index++] = owned.Skill;

            if (trainingBattle)
                return new LegacyQueuedDuel(checked(100 + stats.Health), checked(50 + stats.Resistance),
                    TrainingDummyHealth, 0, playerSkills, Array.Empty<LegacySkill>(), new[] { 0 }, randomSeed,
                    features: Features, playerActGainBonus: stats.ActGain, playerActCapacityBonus: stats.ActCapacity,
                    roundLimit: TrainingRoundLimit, meshPercent: meshPercent);

            LegacyCounter enemyCounter = CurrentStage.EnemyCounterBasis == null ? null
                : new LegacyCounter(WithStagePower(CurrentStage.EnemyCounterBasis), CurrentStage.EnemyCountersPerTurn);

            // Closed lanes keep their skills in the loadout; the duel leaves them out.
            EnemyScript script = CampaignEnemyRhythms.Script(CurrentStage.EnemyRhythm);
            if (script != null)
                return new LegacyQueuedDuel(checked(100 + stats.Health), checked(50 + stats.Resistance),
                    CurrentStage.EnemyHealth, CurrentStage.EnemyResistance, playerSkills,
                    script.Select(WithStagePower), randomSeed, enemyCounter: enemyCounter, features: Features,
                    playerActGainBonus: stats.ActGain, playerActCapacityBonus: stats.ActCapacity, meshPercent: meshPercent);
            // The basic rhythm: the six basic skills in order, 2→3→2→1 actions a turn.
            var enemySkills = new LegacySkill[BasicRhythmSkillIds.Length];
            for (int i = 0; i < enemySkills.Length; i++) enemySkills[i] = WithStagePower(LegacySkillDefinitions.Skill(BasicRhythmSkillIds[i]));
            return new LegacyQueuedDuel(checked(100 + stats.Health), checked(50 + stats.Resistance),
                CurrentStage.EnemyHealth, CurrentStage.EnemyResistance, playerSkills, enemySkills,
                new[] { 2, 3, 2, 1 }, randomSeed, enemyCounter: enemyCounter, features: Features,
                playerActGainBonus: stats.ActGain, playerActCapacityBonus: stats.ActCapacity, meshPercent: meshPercent);
        }

        private LegacySkill WithStagePower(LegacySkill basis)
        {
            int bonus = CurrentStage.EnemyPowerBonus;
            return new LegacySkill(basis.Id, basis.Name, basis.Cost,
                basis.MinPower + bonus, basis.MaxPower + bonus, basis.Kind, basis.Property,
                basis.AttackCount, basis.LaneIndex, basis.Description, basis.AnimationName, basis.IconId);
        }

        public void Reset()
        {
            ownedSkills.Clear();
            foreach (List<CampaignOwnedSkill> lane in equippedLanes) lane.Clear();
            foreach (LegacySkill skill in LegacyInitialSkills.All)
            {
                var owned = new CampaignOwnedSkill(skill);
                ownedSkills.Add(owned);
                equippedLanes[skill.LaneIndex].Add(owned);
            }
            ResetLoadoutDraft();
            curriculum.Reset();
            LastCompletedCurriculumNode = null;
            LastOutcome = null;
            StageNumber = 1;
            Currency = LastReward = 0;
            Array.Clear(clearedStages, 0, clearedStages.Length);
            HighestUnlockedStage = 1;
            ClearedStageCount = 0;
            TrainingVictoryCount = 0;
            trainingBattle = false;
            Phase = CampaignPhase.Lobby;
        }

        /// <summary>The persistent state. Owned skills follow from the curriculum and stage clears, so only their progress is kept.
        /// Unsaved loadout edits and the battle in progress are not included.</summary>
        public CampaignSave CaptureSave()
        {
            var cleared = new List<int>();
            for (int index = 0; index < clearedStages.Length; index++)
                if (clearedStages[index]) cleared.Add(index + 1);
            var loadout = new List<int>[equippedLanes.Length];
            for (int lane = 0; lane < equippedLanes.Length; lane++)
            {
                loadout[lane] = new List<int>();
                foreach (CampaignOwnedSkill skill in equippedLanes[lane]) loadout[lane].Add(skill.SkillId);
            }
            var skillExperience = new List<KeyValuePair<int, int>>();
            foreach (CampaignOwnedSkill owned in ownedSkills)
                if (owned.Experience > 0)
                    skillExperience.Add(new KeyValuePair<int, int>(owned.SkillId, owned.Experience));
            skillExperience.Sort((left, right) => left.Key.CompareTo(right.Key));
            return new CampaignSave(Currency, cleared, curriculum.Completed, curriculum.Active?.Id, curriculum.ActiveBattles,
                loadout, TrainingVictoryCount, skillExperience);
        }

        /// <summary>Replaces this run with a saved state, back in the lobby with the saved loadout as the draft.
        /// The whole save is checked first; when it breaks a rule nothing changes and <paramref name="error"/> says why.
        /// Curriculum progress is restored whether or not the curriculum is open (an older save may hold some before
        /// mission 8); a closed curriculum keeps it untouched until it opens.</summary>
        public bool TryRestore(CampaignSave save, out string error)
        {
            error = ValidateSave(save);
            if (error != null) return false;

            curriculum.Restore(save.CurriculumCompleted, save.CurriculumActive, save.CurriculumBattles);
            LastCompletedCurriculumNode = null;
            LastOutcome = null;
            ownedSkills.Clear();
            foreach (LegacySkill skill in LegacyInitialSkills.All) ownedSkills.Add(new CampaignOwnedSkill(skill));
            foreach (string id in save.CurriculumCompleted) GrantSkills(curriculum.Tree.Find(id));
            foreach (int number in save.ClearedStages)
                if (stages[number - 1].FirstClearSkillId != 0) GrantSkill(stages[number - 1].FirstClearSkillId);
            foreach (KeyValuePair<int, int> entry in save.SkillExperience)
                FindOwnedSkill(entry.Key).RestoreExperience(entry.Value);
            for (int lane = 0; lane < equippedLanes.Length; lane++)
            {
                equippedLanes[lane].Clear();
                foreach (int id in save.Loadout[lane]) equippedLanes[lane].Add(FindOwnedSkill(id));
            }
            ResetLoadoutDraft();
            Array.Clear(clearedStages, 0, clearedStages.Length);
            HighestUnlockedStage = 1;
            foreach (int number in save.ClearedStages)
            {
                clearedStages[number - 1] = true;
                // A first clear opens the next stage, exactly as TryCompleteBattle does.
                HighestUnlockedStage = Math.Max(HighestUnlockedStage, Math.Min(number + 1, StageCount));
            }
            ClearedStageCount = save.ClearedStages.Count;
            TrainingVictoryCount = save.TrainingVictoryCount;
            trainingBattle = false;
            Currency = save.Currency;
            StageNumber = 1;
            LastReward = 0;
            Phase = CampaignPhase.Lobby;
            return true;
        }

        private string ValidateSave(CampaignSave save)
        {
            if (save == null) return "저장 데이터가 없습니다.";
            if (save.Currency < 0) return $"재화가 음수입니다({save.Currency}).";
            if (save.TrainingVictoryCount < 0) return $"수련 승리 횟수가 음수입니다({save.TrainingVictoryCount}).";

            var cleared = new HashSet<int>();
            foreach (int number in save.ClearedStages)
            {
                if (!IsValidStage(number)) return $"없는 스테이지 {number}을(를) 클리어했다고 되어 있습니다.";
                if (!cleared.Add(number)) return $"스테이지 {number}의 클리어가 중복되었습니다.";
            }
            if (save.TrainingVictoryCount > 0 && (!cleared.Contains(1) || !cleared.Contains(2)))
                return "스테이지 3 해금 전 수련 기록이 있습니다.";

            string curriculumError = curriculum.Validate(save.CurriculumCompleted, save.CurriculumActive, save.CurriculumBattles);
            if (curriculumError != null) return curriculumError;
            // Owned skills follow completed nodes and first-clear stage rewards; the loadout may only use those.
            var owned = new Dictionary<int, LegacySkill>();
            foreach (LegacySkill skill in LegacyInitialSkills.All) owned[skill.Id] = skill;
            foreach (string id in save.CurriculumCompleted)
                foreach (int skillId in curriculum.Tree.Find(id).SkillIds)
                {
                    LegacySkill skill = FindSheetSkill(skillId);
                    if (skill == null) return $"커리큘럼 '{id}'이(가) 알 수 없는 기술 {skillId}을(를) 줍니다.";
                    owned[skillId] = skill;
                }
            foreach (int number in save.ClearedStages)
            {
                int skillId = stages[number - 1].FirstClearSkillId;
                if (skillId == 0) continue;
                LegacySkill skill = FindSheetSkill(skillId);
                if (skill == null) return $"스테이지 {number} 첫 클리어 보상 기술 {skillId}이(가) 없습니다.";
                owned[skillId] = skill;
            }

            var experienced = new HashSet<int>();
            foreach (KeyValuePair<int, int> entry in save.SkillExperience)
            {
                if (!owned.TryGetValue(entry.Key, out LegacySkill skill))
                    return $"경험치를 기록한 기술 {entry.Key}을(를) 보유하지 않습니다.";
                if (!experienced.Add(entry.Key)) return $"기술 {entry.Key}의 경험치가 중복되었습니다.";
                int maximum = CampaignOwnedSkill.ExperienceRequiredForCost(skill.Cost) * CampaignOwnedSkill.MaxLevel;
                if (entry.Value < 0 || entry.Value > maximum)
                    return $"기술 {entry.Key}의 경험치 {entry.Value}은(는) 0~{maximum} 범위 밖입니다.";
            }

            if (save.Loadout.Count != equippedLanes.Length) return $"편성 열이 {save.Loadout.Count}개입니다.";
            var equipped = new HashSet<int>();
            for (int lane = 0; lane < save.Loadout.Count; lane++)
            {
                IReadOnlyList<int> ids = save.Loadout[lane];
                if (ids.Count != loadoutSlots[lane].Length) return $"{lane + 1}번째 편성 열의 기술이 {ids.Count}개입니다.";
                foreach (int id in ids)
                {
                    if (!owned.ContainsKey(id)) return $"보유하지 않은 기술 {id}이(가) 편성되어 있습니다.";
                    if (owned[id].LaneIndex != lane) return $"기술 {id}은(는) {lane + 1}번째 열에 편성할 수 없습니다.";
                    if (!equipped.Add(id)) return $"기술 {id}이(가) 중복으로 편성되어 있습니다.";
                }
            }
            return null;
        }

        // A technique the sheet lacks throws the sheet's Korean error (LegacySkillDefinitions.Skill).
        private void GrantSkills(CurriculumNode node)
        {
            foreach (int skillId in node.SkillIds)
                GrantSkill(skillId);
        }

        private void GrantSkill(int skillId)
        {
            if (FindOwnedSkill(skillId) == null)
                ownedSkills.Add(new CampaignOwnedSkill(LegacySkillDefinitions.Skill(skillId)));
        }

        // Any sheet row, not only 획득 ones: a node whose technique became a starting one then grants nothing new
        // instead of making its saves unreadable (CampaignSheetCheck reports that sheet).
        private static LegacySkill FindSheetSkill(int skillId) => LegacySkillDefinitions.Find(skillId)?.Skill;

        private static bool IsStartingSkill(int skillId)
        {
            foreach (LegacySkill skill in LegacyInitialSkills.All)
                if (skill.Id == skillId) return true;
            return false;
        }

        private bool IsUnlockedStageSkill(int skillId)
        {
            for (int i = 0; i < clearedStages.Length; i++)
                if (clearedStages[i] && stages[i].FirstClearSkillId == skillId) return true;
            return false;
        }

        /// <summary>Drops saved-lane skills that are no longer owned and fills the gaps with the lane's starting
        /// skills in their original order, so every lane keeps exactly three.</summary>
        private void RefillLanesWithStartingSkills()
        {
            for (int lane = 0; lane < equippedLanes.Length; lane++)
            {
                List<CampaignOwnedSkill> skills = equippedLanes[lane];
                skills.RemoveAll(owned => !ownedSkills.Contains(owned));
                foreach (CampaignOwnedSkill owned in ownedSkills)
                {
                    if (skills.Count >= loadoutSlots[lane].Length) break;
                    if (owned.Skill.LaneIndex == lane && IsStartingSkill(owned.SkillId) && !skills.Contains(owned)) skills.Add(owned);
                }
            }
        }

        private bool CanEditLoadout => Phase == CampaignPhase.Lobby || Phase == CampaignPhase.Maintenance;
        private bool IsValidStage(int number) => number >= 1 && number <= StageCount;

        private bool CanEnterStage(int number)
        {
            if (!IsValidStage(number) || number > HighestUnlockedStage || number > StageLimit || HasLoadoutChanges) return false;
            // Ownership, uniqueness and lane assignment are maintained by the only
            // mutation APIs; read-only views cannot bypass these invariants.
            foreach (List<CampaignOwnedSkill> lane in equippedLanes)
                if (lane.Count != 3) return false;
            return true;
        }

        private void ResetLoadoutDraft()
        {
            for (int lane = 0; lane < loadoutSlots.Length; lane++)
                for (int slot = 0; slot < loadoutSlots[lane].Length; slot++)
                    loadoutSlots[lane][slot] = slot < equippedLanes[lane].Count ? equippedLanes[lane][slot] : null;
        }

        private bool IsValidLoadout()
        {
            for (int lane = 0; lane < loadoutSlots.Length; lane++)
                for (int slot = 0; slot < loadoutSlots[lane].Length; slot++)
                {
                    CampaignOwnedSkill owned = loadoutSlots[lane][slot];
                    if (owned == null || owned.Skill.LaneIndex != lane ||
                        !ReferenceEquals(FindOwnedSkill(owned.SkillId), owned)) return false;
                    for (int previousLane = 0; previousLane <= lane; previousLane++)
                    {
                        int precedingSlots = previousLane == lane ? slot : loadoutSlots[previousLane].Length;
                        for (int previousSlot = 0; previousSlot < precedingSlots; previousSlot++)
                            if (loadoutSlots[previousLane][previousSlot].SkillId == owned.SkillId) return false;
                    }
                }
            return true;
        }

        private void BeginStage(int number)
        {
            StageNumber = number;
            trainingBattle = false;
            LastReward = 0;
            LastCompletedCurriculumNode = null;
            LastOutcome = null;
            Phase = CampaignPhase.Battle;
        }

        private CampaignOwnedSkill FindOwnedSkill(int skillId)
        {
            foreach (CampaignOwnedSkill owned in ownedSkills) if (owned.SkillId == skillId) return owned;
            return null;
        }
    }

    public sealed class CampaignStage
    {
        // The counter's technique id, or 0 for none; looked up on use so a sheet edit reaches it.
        private readonly int enemyCounterSkillId;

        internal CampaignStage(int number, string name)
        {
            Number = number;
            Name = name;
            EnemyHealth = 80 + 15 * (number - 1);
            EnemyResistance = 15 + 3 * (number - 1);
            EnemyPowerBonus = number - 1;
            Reward = 60 + 10 * (number - 1);
            FirstClearSkillId = number == 1 ? 43 : number == 2 ? 44 : 0;
            // Placeholder counters until enemy archetypes exist: a guard (막기, 7) that taxes a
            // one-sided attack from stage five, then a strong thrust (정교한 찌르기, 4) from stage seven.
            enemyCounterSkillId = number >= 7 ? 4 : number >= 5 ? 7 : 0;
            EnemyCountersPerTurn = enemyCounterSkillId == 0 ? 0 : 1;
        }

        public int Number { get; }
        public string Name { get; }
        public int EnemyHealth { get; }
        public int EnemyResistance { get; }
        public int EnemyPowerBonus { get; }
        /// <summary>The enemy counter before this stage's power bonus, or null when it has none.</summary>
        public LegacySkill EnemyCounterBasis => enemyCounterSkillId == 0 ? null : LegacySkillDefinitions.Skill(enemyCounterSkillId);
        public int EnemyCountersPerTurn { get; }
        public int Reward { get; }
        /// <summary>The technique granted once for this stage's first victory, or zero if there is none.</summary>
        public int FirstClearSkillId { get; }
        /// <summary>결투 or 전투: how the fight is presented (never shown, no rule effect). Every stage is 전투 for now.</summary>
        public EncounterKind Encounter => EncounterKind.Battle;
        /// <summary>How this stage's enemy spends its turns (internal; never named on screen).</summary>
        public CampaignEnemyRhythm EnemyRhythm => CampaignEnemyRhythms.ForStage(Number);
    }

    /// <summary>A player-owned skill and its cumulative clashes. This instance is separate from the immutable sheet
    /// definition so upgrades during combat never strengthen enemies that use the same skill ID.</summary>
    public sealed class CampaignOwnedSkill
    {
        public const int MaxLevel = 3;

        internal CampaignOwnedSkill(LegacySkill skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            Skill = new LegacySkill(skill.Id, skill.Name, skill.Cost, skill.MinPower, skill.MaxPower,
                skill.Kind, skill.Property, skill.AttackCount, skill.LaneIndex, skill.Description,
                skill.AnimationName, skill.IconId);
        }

        public LegacySkill Skill { get; }
        public int SkillId => Skill.Id;
        public int Experience { get; private set; }
        public int Level => Math.Min(MaxLevel, Experience / ExperienceRequired);
        public int ExperienceRequired => ExperienceRequiredForCost(Skill.Cost);
        public int ExperienceThisLevel => IsMaxLevel ? ExperienceRequired : Experience % ExperienceRequired;
        public bool IsMaxLevel => Level >= MaxLevel;

        // Temporary progression pace by ACT cost.
        internal static int ExperienceRequiredForCost(int cost)
            => cost <= 1 ? 10 : cost == 2 ? 5 : cost == 3 ? 3 : cost == 4 ? 2 : 1;

        public bool GainClashExperience()
        {
            if (IsMaxLevel) return false;
            int before = Level;
            Experience++;
            if (Level != before) Skill.SetUpgradeLevel(Level);
            return true;
        }

        internal void RestoreExperience(int experience)
        {
            Experience = experience;
            Skill.SetUpgradeLevel(Level);
        }
    }
}
