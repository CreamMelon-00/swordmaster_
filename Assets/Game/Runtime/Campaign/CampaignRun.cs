using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    public enum CampaignPhase { Battle, Maintenance, Failed, Completed, Lobby }

    /// <summary>One eight-stage run with its curriculum and its 수련 점수 balance. Combat rules remain in LegacyQueuedDuel.
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
            new CampaignStage(GreenRibbonBossBattle.StageNumber, GreenRibbonBossBattle.StageName),
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
        private CampaignOwnedSkill equippedCounter, draftCounter;
        // Ids of the field pickups taken, in the order they were taken.
        private readonly List<string> collectedFieldPickups = new List<string>();
        private bool trainingBattle;

        public CampaignRun() : this(CampaignCurriculum.Default) { }

        /// <summary>Uses a supplied curriculum tree for authored variants and rule tests.</summary>
        public CampaignRun(CurriculumTree curriculumTree)
        {
            curriculum = new CurriculumProgress(curriculumTree);
            OwnedSkills = ownedSkills.AsReadOnly();
            CollectedFieldPickups = collectedFieldPickups.AsReadOnly();
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
        /// <summary>Starting skills first, then stage, curriculum and field-pickup skills in the order they were granted.</summary>
        public IReadOnlyList<CampaignOwnedSkill> OwnedSkills { get; }
        public CampaignOwnedSkill GetOwnedSkill(int skillId) => FindOwnedSkill(skillId);
        /// <summary>The optional counter saved with the loadout, separate from all three lanes.</summary>
        public LegacySkill EquippedCounterSkill => equippedCounter?.Skill;
        public LegacySkill DraftCounterSkill => draftCounter?.Skill;
        public bool IsCounterSkill(int skillId) => IsCounterDefinition(skillId);

        public IReadOnlyList<LegacySkill> GetOwnedCounterSkills()
        {
            var counters = new List<LegacySkill>();
            foreach (CampaignOwnedSkill owned in ownedSkills)
                if (IsCounterSkill(owned.SkillId)) counters.Add(owned.Skill);
            return counters.AsReadOnly();
        }

        /// <summary>Edits the optional counter slot. Zero clears it; saving commits the draft with the lane edits.</summary>
        public bool TryEquipCounter(int skillId)
        {
            if (!CanEditLoadout) return false;
            if (skillId == 0)
            {
                draftCounter = null;
                return true;
            }
            CampaignOwnedSkill owned = FindOwnedSkill(skillId);
            if (owned == null || !IsCounterSkill(skillId)) return false;
            draftCounter = owned;
            return true;
        }
        /// <summary>Ids of the field pickups taken (<see cref="CampaignFieldPickups"/>), in the order they were taken.
        /// The field hides these.</summary>
        public IReadOnlyList<string> CollectedFieldPickups { get; }

        public bool IsFieldPickupCollected(string pickupId) => pickupId != null && collectedFieldPickups.Contains(pickupId);

        /// <summary>Whether the pickup can be taken now: a known pickup not taken yet, in the lobby or between battles.
        /// Lanes and the curriculum do not matter; a technique of a closed lane is owned and waits for its lane.</summary>
        public bool CanCollectFieldPickup(string pickupId)
            => CanEditLoadout && CampaignFieldPickups.Find(pickupId) != null && !IsFieldPickupCollected(pickupId);

        /// <summary>Takes a technique lying in the field. It is owned from now on, like a curriculum grant: never
        /// equipped, kept by a curriculum reset, and saved through the pickup's id. Refused (false, nothing changes) for an
        /// unknown pickup, one already taken, or during a battle. A pickup whose technique is already owned is still taken
        /// but grants nothing new (<paramref name="granted"/> is null); the run never owns a technique twice. A technique
        /// the sheet lacks throws the sheet's Korean error before anything changes.</summary>
        /// <param name="granted">The newly owned technique, or null.</param>
        public bool TryCollectFieldPickup(string pickupId, out CampaignOwnedSkill granted)
        {
            granted = null;
            if (!CanCollectFieldPickup(pickupId)) return false;
            CampaignFieldPickup pickup = CampaignFieldPickups.Find(pickupId);
            _ = RequireCampaignSkill(pickup.SkillId);
            bool alreadyOwned = FindOwnedSkill(pickup.SkillId) != null;
            collectedFieldPickups.Add(pickup.Id);
            GrantSkill(pickup.SkillId);
            if (!alreadyOwned) granted = FindOwnedSkill(pickup.SkillId);
            return true;
        }

        public bool TryCollectFieldPickup(string pickupId) => TryCollectFieldPickup(pickupId, out _);

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
        /// <summary>The unspent 수련 점수 (never negative). Stage battles and story missions add to it
        /// (<see cref="CurriculumPointRules"/>), also while the curriculum is closed (the bank); learning a node spends it
        /// and a curriculum reset gives the spent points back. A new journey starts with what the story missions it keeps
        /// already paid (<see cref="Reset(int)"/>), zero in a new game.</summary>
        public int CurriculumPoints { get; private set; }
        /// <summary>What the last finished stage battle earned (empty after a training fight and once the next battle
        /// begins), or what the last <see cref="AwardStoryMissionPoints"/> earned.</summary>
        public CurriculumPointGain LastCurriculumPointGain { get; private set; }
        /// <summary>Whether <see cref="LastCurriculumPointGain"/> made the balance cover an available node it did not
        /// cover before (<see cref="CurriculumProgress.BecameAffordable"/>). Always false while the curriculum is closed.</summary>
        public bool CurriculumNewlyAffordable { get; private set; }
        /// <summary>Whether the node is available and the balance covers its cost (the curriculum and the phase aside).</summary>
        public bool IsCurriculumNodeAffordable(string nodeId) => curriculum.IsAffordable(nodeId, CurriculumPoints);
        /// <summary>How many available nodes the balance covers (익힐 수 있는 과정 k개).</summary>
        public int AffordableCurriculumNodeCount => curriculum.AffordableCount(CurriculumPoints);
        /// <summary>With none affordable, the points still missing for the cheapest available node (다음 과정까지 m점);
        /// zero otherwise.</summary>
        public int CurriculumPointsToNextNode => curriculum.PointsToNext(CurriculumPoints);
        /// <summary>What a curriculum reset gives back now: the costs of the completed nodes.</summary>
        public int CurriculumRefund => curriculum.SpentPoints;
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
        /// last one, with mission 8; outside the story everything is open). While it is closed, learning and resetting
        /// are refused; 수련 점수 still accrue (the bank) and saved progress is kept as it is for the day it opens.</summary>
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
                if (!ReferenceEquals(draftCounter, equippedCounter)) return true;
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

        /// <summary>The 수련 점수 a win of the stage would earn now: battle and victory, and the first clear while it is not
        /// cleared (승리 시 수련 점수 +2, 첫 클리어 +4). Zero for an unknown stage.</summary>
        public int GetStageVictoryPoints(int stageNumber)
            => IsValidStage(stageNumber) ? CurriculumPointRules.ForStageBattle(true, !IsStageCleared(stageNumber)).Total : 0;

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
            ClearLastCurriculumPointGain();
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
            if (owned == null || IsCounterSkill(skillId) || owned.Skill.LaneIndex != lane) return false;
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
            equippedCounter = draftCounter;
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
            if (owned == null || IsCounterSkill(skillId) || !IsLaneOpen(owned.Skill.LaneIndex)) return false;
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

        /// <summary>Settles the battle in progress. A stage battle earns 수련 점수 whatever the outcome
        /// (<see cref="CurriculumPointRules.ForStageBattle"/>), open curriculum or not; a training fight earns none.</summary>
        public bool TryCompleteBattle(DuelMatchOutcome outcome)
        {
            if (Phase != CampaignPhase.Battle) return false;
            if (outcome != DuelMatchOutcome.PlayerVictory && outcome != DuelMatchOutcome.EnemyVictory
                && outcome != DuelMatchOutcome.Draw) return false;
            if (trainingBattle)
            {
                LastOutcome = outcome;
                LastReward = 0;
                ClearLastCurriculumPointGain();
                if (outcome == DuelMatchOutcome.PlayerVictory && TrainingVictoryCount < int.MaxValue)
                    TrainingVictoryCount++;
                Phase = CampaignPhase.Maintenance;
                return true;
            }
            bool victory = outcome == DuelMatchOutcome.PlayerVictory;
            bool firstClear = victory && !clearedStages[StageNumber - 1];
            // The stage's first-clear technique must be in the sheet. It is looked up before anything changes, so a sheet
            // without it stops here with the sheet's own error and a retry cannot count the battle twice.
            int stageSkillRewardId = firstClear ? CurrentStage.FirstClearSkillId : 0;
            if (stageSkillRewardId != 0) _ = RequireCampaignSkill(stageSkillRewardId);

            LastOutcome = outcome;
            EarnCurriculumPoints(CurriculumPointRules.ForStageBattle(victory, firstClear));
            if (!victory)
            {
                LastReward = 0;
                Phase = CampaignPhase.Failed;
                return true;
            }

            LastReward = GetStageReward(StageNumber);
            Currency += LastReward;
            if (firstClear)
            {
                clearedStages[StageNumber - 1] = true;
                ClearedStageCount++;
                HighestUnlockedStage = UnlockedGroupEnd();
                if (stageSkillRewardId != 0) GrantSkill(stageSkillRewardId);
            }
            Phase = StageNumber == StageCount ? CampaignPhase.Completed : CampaignPhase.Maintenance;
            return true;
        }

        /// <summary>Pays a story mission's first completion (서막 and 수련 missions, once each; the story itself, not this
        /// run, knows which completion is the first: <c>PrologueRun.TryComplete</c> returning true). Works in any phase and
        /// whether or not the curriculum is open. Returns what it earned, which <see cref="LastCurriculumPointGain"/> and
        /// <see cref="CurriculumNewlyAffordable"/> then describe.</summary>
        public CurriculumPointGain AwardStoryMissionPoints()
        {
            CurriculumPointGain gain = CurriculumPointRules.ForStoryMission();
            EarnCurriculumPoints(gain);
            return gain;
        }

        private void EarnCurriculumPoints(CurriculumPointGain gain)
        {
            int before = CurriculumPoints;
            CurriculumPoints = AddPoints(before, gain.Total);
            LastCurriculumPointGain = gain;
            CurriculumNewlyAffordable = IsCurriculumOpen && curriculum.BecameAffordable(before, CurriculumPoints);
        }

        private void ClearLastCurriculumPointGain()
        {
            LastCurriculumPointGain = default;
            CurriculumNewlyAffordable = false;
        }

        // The balance saturates instead of overflowing.
        private static int AddPoints(int balance, int points)
            => points > 0 && balance > int.MaxValue - points ? int.MaxValue : balance + points;

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

        /// <summary>Whether the node can be learned now: the curriculum is open, the run is in the lobby or between battles
        /// (the result screen is the controller's to block), the node is available (prerequisites met, neither completed nor
        /// closed) and the balance covers its cost.</summary>
        public bool CanLearnCurriculumNode(string nodeId)
            => IsCurriculumOpen && CanEditLoadout && curriculum.IsAffordable(nodeId, CurriculumPoints);

        /// <summary>Spends the node's cost and completes it at once: its techniques are owned (not equipped) and its stat
        /// bonuses apply, as every completion has. Refused (false, nothing changes) unless
        /// <see cref="CanLearnCurriculumNode"/>. A technique the sheet lacks throws the sheet's Korean error before anything
        /// changes. <see cref="CurriculumProgress.LastCompleted"/> and <see cref="CurriculumProgress.OpenedBy"/> then name
        /// the node and what it opened.</summary>
        public bool TryLearnCurriculumNode(string nodeId)
        {
            if (!CanLearnCurriculumNode(nodeId)) return false;
            CurriculumNode node = curriculum.Tree.Find(nodeId);
            foreach (int skillId in node.SkillIds) _ = RequireCampaignSkill(skillId);
            CurriculumPoints -= node.Cost;
            curriculum.Complete(node.Id);
            GrantSkills(node);
            return true;
        }

        /// <summary>Clears the whole curriculum (lobby or maintenance, curriculum open, something completed): completed
        /// nodes and every skill they granted, and gives back the points they cost (<see cref="CurriculumRefund"/>). Stage
        /// first-clear skills and field-pickup skills remain owned. Saved lanes that lose a skill are refilled with that
        /// lane's starting skills.</summary>
        public bool TryResetCurriculum()
        {
            if (!IsCurriculumOpen || !CanEditLoadout || curriculum.CompletedCount == 0) return false;
            CurriculumPoints = AddPoints(CurriculumPoints, curriculum.SpentPoints);
            curriculum.Reset();
            ownedSkills.RemoveAll(owned => !IsAlwaysOwnedSkill(owned.SkillId) && !IsUnlockedStageSkill(owned.SkillId) &&
                !IsCollectedFieldSkill(owned.SkillId));
            RefillLanesWithStartingSkills();
            ResetLoadoutDraft();
            return true;
        }

        /// <param name="meshPercent">맞물림's power percent per chained skill for this duel
        /// (<see cref="LegacyQueuedDuel.MeshPercent"/>); 0 switches it off.</param>
        public LegacyQueuedDuel CreateDuel(int randomSeed = 1, int meshPercent = LegacyMeshing.DefaultPercent)
        {
            CurriculumStatReward stats = CurriculumStats;
            LegacyCounter playerCounter = equippedCounter == null ? null : new LegacyCounter(equippedCounter.Skill,
                LegacySkillDefinitions.Find(equippedCounter.SkillId).CounterUsesPerTurn);
            var playerSkills = new LegacySkill[EquippedSkillCount];
            int index = 0;
            foreach (List<CampaignOwnedSkill> lane in equippedLanes)
                foreach (CampaignOwnedSkill owned in lane) playerSkills[index++] = owned.Skill;

            if (trainingBattle)
                return new LegacyQueuedDuel(checked(100 + stats.Health), checked(50 + stats.Resistance),
                    TrainingDummyHealth, 0, playerSkills, Array.Empty<LegacySkill>(), new[] { 0 }, randomSeed,
                    playerCounter: playerCounter, features: Features, playerActGainBonus: stats.ActGain, playerActCapacityBonus: stats.ActCapacity,
                    roundLimit: TrainingRoundLimit, meshPercent: meshPercent);

            if (CurrentStage.Number == 5)
                return new LegacyQueuedDuel(checked(100 + stats.Health), checked(50 + stats.Resistance),
                    playerSkills, StageFiveEnemies(), randomSeed, playerCounter: playerCounter, features: Features,
                    playerActGainBonus: stats.ActGain, playerActCapacityBonus: stats.ActCapacity, meshPercent: meshPercent);

            LegacyCounter enemyCounter = CurrentStage.EnemyCounterBasis == null ? null
                : new LegacyCounter(WithStagePower(CurrentStage.EnemyCounterBasis), CurrentStage.EnemyCountersPerTurn);

            // Closed lanes keep their skills in the loadout; the duel leaves them out.
            EnemyScript script = CampaignEnemyRhythms.Script(CurrentStage.EnemyRhythm);
            if (CurrentStage.IsBossEncounter)
                return new LegacyQueuedDuel(checked(GreenRibbonBossBattle.PlayerBaseHealth + stats.Health),
                    checked(50 + stats.Resistance), CurrentStage.EnemyHealth, CurrentStage.EnemyResistance,
                    playerSkills, script, randomSeed, playerCounter: playerCounter, enemyCounter: enemyCounter, features: Features,
                    playerActGainBonus: stats.ActGain, playerActCapacityBonus: stats.ActCapacity,
                    roundLimit: GreenRibbonBossBattle.RoundLimit, meshPercent: meshPercent).EnableIiaBossPatterns();
            if (script != null)
                return new LegacyQueuedDuel(checked(100 + stats.Health), checked(50 + stats.Resistance),
                    CurrentStage.EnemyHealth, CurrentStage.EnemyResistance, playerSkills,
                    script.Select(WithStagePower), randomSeed, playerCounter: playerCounter, enemyCounter: enemyCounter, features: Features,
                    playerActGainBonus: stats.ActGain, playerActCapacityBonus: stats.ActCapacity, meshPercent: meshPercent);
            // The basic rhythm: the six basic skills in order, 2→3→2→1 actions a turn.
            var enemySkills = new LegacySkill[BasicRhythmSkillIds.Length];
            for (int i = 0; i < enemySkills.Length; i++) enemySkills[i] = WithStagePower(LegacySkillDefinitions.Skill(BasicRhythmSkillIds[i]));
            return new LegacyQueuedDuel(checked(100 + stats.Health), checked(50 + stats.Resistance),
                CurrentStage.EnemyHealth, CurrentStage.EnemyResistance, playerSkills, enemySkills,
                new[] { 2, 3, 2, 1 }, randomSeed, playerCounter: playerCounter, enemyCounter: enemyCounter, features: Features,
                playerActGainBonus: stats.ActGain, playerActCapacityBonus: stats.ActCapacity, meshPercent: meshPercent);
        }

        private LegacySkill WithStagePower(LegacySkill basis)
        {
            int bonus = CurrentStage.EnemyPowerBonus;
            return new LegacySkill(basis.Id, basis.Name, basis.Cost,
                basis.MinPower + bonus, basis.MaxPower + bonus, basis.Kind, basis.Property,
                basis.AttackCount, basis.LaneIndex, basis.Description, basis.AnimationName, basis.IconId, basis.DurationHalfUnits);
        }

        private static IReadOnlyList<LegacyEnemyDefinition> StageFiveEnemies()
        {
            LegacySkill slash = TrioSkill(1), quick = TrioSkill(2), thrust = TrioSkill(3),
                disrupt = TrioSkill(5), guard = TrioSkill(7);
            return new[]
            {
                new LegacyEnemyDefinition("수련생 1", 50, 10, new EnemyScript(new IReadOnlyList<LegacySkill>[]
                    { new[] { slash, thrust }, new[] { slash }, new[] { thrust, slash } })),
                new LegacyEnemyDefinition("수련생 2", 50, 10, new EnemyScript(new IReadOnlyList<LegacySkill>[]
                    { new[] { quick, guard }, new[] { thrust }, new[] { slash, quick } })),
                new LegacyEnemyDefinition("수련생 3", 50, 10, new EnemyScript(new IReadOnlyList<LegacySkill>[]
                    { new[] { slash, disrupt }, new[] { guard }, new[] { thrust, slash } })),
            };
        }

        private static LegacySkill TrioSkill(int id)
        {
            LegacySkill basis = LegacySkillDefinitions.Skill(id);
            return new LegacySkill(basis.Id, basis.Name, basis.Cost,
                Math.Max(1, basis.MinPower * 70 / 100), Math.Max(1, basis.MaxPower * 70 / 100),
                basis.Kind, basis.Property, basis.AttackCount, basis.LaneIndex, basis.Description, basis.AnimationName, basis.IconId, basis.DurationHalfUnits);
        }

        /// <summary>A new journey with no story mission won (a new game, tests and direct API use): balance 0.</summary>
        public void Reset() => Reset(0);

        /// <summary>A new journey: everything back to the start. The story is not part of the journey and stays won, so
        /// the new journey starts with what its story missions already paid,
        /// <see cref="CurriculumPointRules.StoryMission"/> (1) each; a mission is never won twice, so this is the only way
        /// it counts once in every journey. That is the same credit a save written before 수련 점수 gets for the same
        /// state (<see cref="CurriculumPointsIn"/>).</summary>
        /// <param name="storyMissionsWon">The story missions won so far (<c>PrologueRun.ClearedCount</c>).</param>
        public void Reset(int storyMissionsWon)
        {
            ownedSkills.Clear();
            foreach (List<CampaignOwnedSkill> lane in equippedLanes) lane.Clear();
            foreach (LegacySkill skill in LegacyInitialSkills.All)
            {
                var owned = new CampaignOwnedSkill(skill);
                ownedSkills.Add(owned);
                equippedLanes[skill.LaneIndex].Add(owned);
            }
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialCounterSkills)
                ownedSkills.Add(new CampaignOwnedSkill(skill));
            foreach (LegacySkill skill in LegacySkillDefinitions.PrototypeSkills)
                ownedSkills.Add(new CampaignOwnedSkill(skill));
            equippedCounter = null;
            ResetLoadoutDraft();
            curriculum.Reset();
            collectedFieldPickups.Clear();
            CurriculumPoints = Saturate(StoryMissionCredit(storyMissionsWon));
            ClearLastCurriculumPointGain();
            LastOutcome = null;
            RestoreDroppedCurriculum = false;
            StageNumber = 1;
            Currency = LastReward = 0;
            Array.Clear(clearedStages, 0, clearedStages.Length);
            HighestUnlockedStage = 2;
            ClearedStageCount = 0;
            TrainingVictoryCount = 0;
            trainingBattle = false;
            Phase = CampaignPhase.Lobby;
        }

        /// <summary>The persistent state. Owned skills follow from the curriculum, stage clears and field pickups taken, so
        /// only their progress is kept. Unsaved loadout edits and the battle in progress are not included.</summary>
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
            return new CampaignSave(Currency, cleared, curriculum.Completed, CurriculumPoints, loadout, TrainingVictoryCount,
                skillExperience, collectedFieldPickups, counterSkillId: equippedCounter?.SkillId ?? 0);
        }

        /// <summary>Whether the last successful <see cref="TryRestore"/> dropped curriculum progress that was made with
        /// another version of the default tree (<see cref="CampaignSave.ForCurriculumTree"/>), so the player should be told
        /// once. False after a restore that kept or had no progress, and after <see cref="Reset"/>.</summary>
        public bool RestoreDroppedCurriculum { get; private set; }

        /// <summary>Replaces this run with a saved state, back in the lobby with the saved loadout as the draft.
        /// A save made with another curriculum tree version is first normalized (<see cref="NormalizeSave"/>): its
        /// curriculum is emptied, its lanes lose the techniques only the curriculum granted and its experience records lose
        /// theirs. The whole save is then checked; when it breaks a rule nothing changes and <paramref name="error"/> says
        /// why. Curriculum progress is restored whether or not the curriculum is open (an older save may hold some before
        /// mission 8); a closed curriculum keeps it untouched until it opens. The balance is the save's, or worked out for a
        /// save without one (<see cref="CurriculumPointsIn"/>; this overload counts no story mission).</summary>
        public bool TryRestore(CampaignSave save, out string error) => TryRestore(save, 0, out error);

        /// <param name="storyMissionsWon">The story missions the same save records as won (<c>GameSave.PrologueCleared</c>),
        /// credited to a save written before 수련 점수 existed (<see cref="CurriculumPointsIn"/>).</param>
        public bool TryRestore(CampaignSave save, int storyMissionsWon, out string error)
        {
            CampaignSave normalized = NormalizeSave(save, out bool droppedCurriculum);
            error = ValidateSave(normalized);
            if (error != null) return false;
            RestoreDroppedCurriculum = droppedCurriculum;

            CurriculumPoints = RestoredCurriculumPoints(save, normalized, droppedCurriculum, storyMissionsWon);
            curriculum.Restore(normalized.CurriculumCompleted);
            ClearLastCurriculumPointGain();
            LastOutcome = null;
            save = normalized;
            ownedSkills.Clear();
            foreach (LegacySkill skill in LegacyInitialSkills.All) ownedSkills.Add(new CampaignOwnedSkill(skill));
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialCounterSkills) ownedSkills.Add(new CampaignOwnedSkill(skill));
            foreach (LegacySkill skill in LegacySkillDefinitions.PrototypeSkills) ownedSkills.Add(new CampaignOwnedSkill(skill));
            foreach (string id in save.CurriculumCompleted) GrantSkills(curriculum.Tree.Find(id));
            foreach (int number in save.ClearedStages)
                if (stages[number - 1].FirstClearSkillId != 0) GrantSkill(stages[number - 1].FirstClearSkillId);
            collectedFieldPickups.Clear();
            foreach (string id in save.FieldPickups)
            {
                collectedFieldPickups.Add(id);
                GrantSkill(CampaignFieldPickups.Find(id).SkillId);
            }
            foreach (KeyValuePair<int, int> entry in save.SkillExperience)
                FindOwnedSkill(entry.Key).RestoreExperience(entry.Value);
            for (int lane = 0; lane < equippedLanes.Length; lane++)
            {
                equippedLanes[lane].Clear();
                foreach (int id in save.Loadout[lane]) equippedLanes[lane].Add(FindOwnedSkill(id));
            }
            equippedCounter = save.CounterSkillId == 0 ? null : FindOwnedSkill(save.CounterSkillId);
            ResetLoadoutDraft();
            Array.Clear(clearedStages, 0, clearedStages.Length);
            foreach (int number in save.ClearedStages)
                clearedStages[number - 1] = true;
            HighestUnlockedStage = UnlockedGroupEnd();
            ClearedStageCount = save.ClearedStages.Count;
            TrainingVictoryCount = save.TrainingVictoryCount;
            trainingBattle = false;
            Currency = save.Currency;
            StageNumber = 1;
            LastReward = 0;
            Phase = CampaignPhase.Lobby;
            return true;
        }

        /// <summary>The 수련 점수 balance the save restores with, without changing anything (the title's 이어하기 summary reads
        /// it). A save with a <c>curriculum-points</c> line keeps its balance. One written before points existed is credited
        /// retroactively: <see cref="CurriculumPointRules.FirstClearedStage"/> (4) per cleared stage plus
        /// <see cref="CurriculumPointRules.StoryMission"/> (1) per story mission won, less the costs of the completed nodes
        /// it keeps (after the tree-version step), never below zero. When the tree version emptied the curriculum, the
        /// balance is the larger of the saved one and that credit with no node kept, so the spent points come back.</summary>
        /// <param name="storyMissionsWon">The story missions the same save records as won (<c>GameSave.PrologueCleared</c>).</param>
        public int CurriculumPointsIn(CampaignSave save, int storyMissionsWon)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            CampaignSave normalized = NormalizeSave(save, out bool dropped);
            return RestoredCurriculumPoints(save, normalized, dropped, storyMissionsWon);
        }

        private int RestoredCurriculumPoints(CampaignSave save, CampaignSave normalized, bool dropped, int storyMissionsWon)
        {
            long credit = (long)CurriculumPointRules.FirstClearedStage * normalized.ClearedStages.Count +
                StoryMissionCredit(storyMissionsWon);
            foreach (string id in normalized.CurriculumCompleted) credit -= curriculum.Tree.Find(id)?.Cost ?? 0;
            int retroactive = Saturate(credit);
            if (!save.CurriculumPoints.HasValue) return retroactive;
            return dropped ? Math.Max(save.CurriculumPoints.Value, retroactive) : save.CurriculumPoints.Value;
        }

        // What the won story missions pay: the journey reset's start and part of the retroactive credit.
        private static long StoryMissionCredit(int storyMissionsWon)
            => (long)CurriculumPointRules.StoryMission * Math.Max(0, storyMissionsWon);

        private static int Saturate(long points) => points <= 0 ? 0 : points >= int.MaxValue ? int.MaxValue : (int)points;

        /// <summary>A save of the current curriculum tree version as it is. Any other version gets its curriculum
        /// emptied (<see cref="CampaignSave.ForCurriculumTree"/>), which can leave techniques it granted in the lanes and in
        /// the experience records: a lane drops each technique the save no longer owns and takes the lane's starting
        /// techniques in sheet order, as many as it dropped, and an experience record of a technique no longer owned goes.
        /// What is owned is the starting techniques, the cleared stages' first-clear rewards and the field pickups taken,
        /// so a save that had cleared stage 7 keeps 16 알티바호. Everything else is left for the strict check that follows,
        /// so real damage still shows.</summary>
        private static CampaignSave NormalizeSave(CampaignSave save, out bool droppedCurriculum)
        {
            droppedCurriculum = false;
            if (save == null) return null;
            save = NormalizeOnslaughtExperience(save);
            save = NormalizePostureExperience(save);
            save = NormalizeFormerStageFourReward(save);
            if (save.CurriculumTreeVersion == CampaignCurriculum.TreeVersion) return save;
            CampaignSave reset = save.ForCurriculumTree(CampaignCurriculum.TreeVersion, out droppedCurriculum);
            var owned = new HashSet<int>();
            foreach (LegacySkill skill in LegacyInitialSkills.All) owned.Add(skill.Id);
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialCounterSkills) owned.Add(skill.Id);
            foreach (LegacySkill skill in LegacySkillDefinitions.PrototypeSkills) owned.Add(skill.Id);
            foreach (int number in reset.ClearedStages)
                if (number >= 1 && number <= stages.Length && stages[number - 1].FirstClearSkillId != 0)
                    owned.Add(stages[number - 1].FirstClearSkillId);
            foreach (string id in reset.FieldPickups)
            {
                CampaignFieldPickup pickup = CampaignFieldPickups.Find(id);
                if (pickup != null) owned.Add(pickup.SkillId);
            }
            var lanes = new List<List<int>>();
            for (int lane = 0; lane < reset.Loadout.Count; lane++)
            {
                var kept = new List<int>();
                foreach (int id in reset.Loadout[lane])
                    if (owned.Contains(id)) kept.Add(id);
                int dropped = reset.Loadout[lane].Count - kept.Count;
                foreach (LegacySkill skill in LegacyInitialSkills.All)
                {
                    if (dropped == 0) break;
                    if (skill.LaneIndex != lane || kept.Contains(skill.Id)) continue;
                    kept.Add(skill.Id);
                    dropped--;
                }
                lanes.Add(kept);
            }
            var experience = new List<KeyValuePair<int, int>>();
            foreach (KeyValuePair<int, int> entry in reset.SkillExperience)
                if (owned.Contains(entry.Key)) experience.Add(entry);
            int counterSkillId = reset.CounterSkillId;
            if (IsCounterDefinition(counterSkillId) && !owned.Contains(counterSkillId)) counterSkillId = 0;
            return new CampaignSave(reset.Currency, reset.ClearedStages, reset.CurriculumCompleted, reset.CurriculumPoints,
                lanes, reset.TrainingVictoryCount, experience, reset.FieldPickups, reset.CurriculumTreeVersion, counterSkillId,
                reset.OnslaughtExperienceVersion);
        }

        // Stage 4 used to grant 62 기세 훔치기. It is now multiplayer-only: remove only that formerly valid reward,
        // keeping the saved order and every other kind of progress. Malformed copies/experience stay for validation.
        private static CampaignSave NormalizeFormerStageFourReward(CampaignSave save)
        {
            const int retiredRewardId = 62;
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(retiredRewardId);
            bool clearedStageFour = false;
            foreach (int number in save.ClearedStages) if (number == 4) clearedStageFour = true;
            if (!LegacySkillDefinitions.Table.IsVersus(definition) || !clearedStageFour ||
                save.Loadout.Count != 3 || save.CounterSkillId == retiredRewardId) return save;

            int rewardSlot = -1;
            for (int lane = 0; lane < save.Loadout.Count; lane++)
            {
                if (save.Loadout[lane].Count != 3) return save;
                for (int slot = 0; slot < save.Loadout[lane].Count; slot++)
                    if (save.Loadout[lane][slot] == retiredRewardId)
                    {
                        if (lane != 2 || rewardSlot >= 0) return save;
                        rewardSlot = slot;
                    }
            }
            bool hasExperience = false;
            var experience = new List<KeyValuePair<int, int>>();
            foreach (KeyValuePair<int, int> entry in save.SkillExperience)
            {
                if (entry.Key == retiredRewardId)
                {
                    // Its old ACT 2 price allowed at most 5 clashes per level, three levels.
                    if (hasExperience || entry.Value < 0 || entry.Value > 5 * CampaignOwnedSkill.MaxLevel) return save;
                    hasExperience = true;
                }
                else experience.Add(entry);
            }
            if (rewardSlot < 0 && !hasExperience) return save;

            var lanes = new List<List<int>>();
            foreach (IReadOnlyList<int> lane in save.Loadout) lanes.Add(new List<int>(lane));
            if (rewardSlot >= 0)
            {
                LegacySkill replacement = null;
                foreach (LegacySkill skill in LegacyInitialSkills.All)
                    if (skill.LaneIndex == 2 && !lanes[2].Contains(skill.Id)) { replacement = skill; break; }
                if (replacement == null) return save;
                lanes[2][rewardSlot] = replacement.Id;
            }
            return new CampaignSave(save.Currency, save.ClearedStages, save.CurriculumCompleted, save.CurriculumPoints,
                lanes, save.TrainingVictoryCount, experience, save.FieldPickups, save.CurriculumTreeVersion, save.CounterSkillId,
                save.OnslaughtExperienceVersion);
        }

        // 44 몰아치기 changed from ACT 3 (three clashes per level) to ACT 2 (five). Unlike a cap decrease,
        // this needs an explicit once-only marker or an old level-three save would become level one on every load.
        private static CampaignSave NormalizeOnslaughtExperience(CampaignSave save)
        {
            if (save.OnslaughtExperienceVersion != 0) return save;
            const int previousRequired = 3;
            const int previousMaximum = previousRequired * CampaignOwnedSkill.MaxLevel;
            var experience = new List<KeyValuePair<int, int>>();
            foreach (KeyValuePair<int, int> entry in save.SkillExperience)
            {
                if (entry.Key != 44) { experience.Add(entry); continue; }
                // Keep corrupt historical values for strict validation, including values now inside the larger cap.
                if (entry.Value < 0 || entry.Value > previousMaximum) return save;
                LegacySkill adjusted = LegacySkillDefinitions.Find(44)?.Skill;
                if (adjusted == null) return save;
                int required = CampaignOwnedSkill.ExperienceRequiredForCost(adjusted.Cost);
                experience.Add(new KeyValuePair<int, int>(44, entry.Value * required / previousRequired));
            }
            return new CampaignSave(save.Currency, save.ClearedStages, save.CurriculumCompleted, save.CurriculumPoints,
                save.Loadout, save.TrainingVictoryCount, experience, save.FieldPickups, save.CurriculumTreeVersion,
                save.CounterSkillId, CampaignSave.CurrentOnslaughtExperienceVersion);
        }

        // 자세 잡기 and 쳐내기 used to cost 1 ACT, so legitimate older saves can hold up to 30 clashes. Their new
        // costs lower that cap: keep capped skills usable without accepting negative or impossible old experience.
        private static CampaignSave NormalizePostureExperience(CampaignSave save)
        {
            const int previousMaximum = 10 * CampaignOwnedSkill.MaxLevel;
            var experience = new List<KeyValuePair<int, int>>();
            bool changed = false;
            foreach (KeyValuePair<int, int> entry in save.SkillExperience)
            {
                LegacySkill adjusted = entry.Key == 45 || entry.Key == 9 ? LegacySkillDefinitions.Find(entry.Key)?.Skill : null;
                int maximum = adjusted == null ? previousMaximum
                    : CampaignOwnedSkill.ExperienceRequiredForCost(adjusted.Cost) * CampaignOwnedSkill.MaxLevel;
                if (entry.Value > maximum && entry.Value <= previousMaximum)
                {
                    experience.Add(new KeyValuePair<int, int>(entry.Key, maximum));
                    changed = true;
                }
                else experience.Add(entry);
            }
            return changed ? new CampaignSave(save.Currency, save.ClearedStages, save.CurriculumCompleted,
                save.CurriculumPoints, save.Loadout, save.TrainingVictoryCount, experience, save.FieldPickups,
                save.CurriculumTreeVersion, save.CounterSkillId, save.OnslaughtExperienceVersion) : save;
        }

        private string ValidateSave(CampaignSave save)
        {
            if (save == null) return "저장 데이터가 없습니다.";
            if (save.OnslaughtExperienceVersion < 0 || save.OnslaughtExperienceVersion > CampaignSave.CurrentOnslaughtExperienceVersion)
                return $"지원하지 않는 몰아치기 경험치 버전 {save.OnslaughtExperienceVersion}입니다.";
            if (save.Currency < 0) return $"재화가 음수입니다({save.Currency}).";
            if (save.CurriculumPoints < 0) return $"수련 점수가 음수입니다({save.CurriculumPoints}).";
            if (save.TrainingVictoryCount < 0) return $"수련 승리 횟수가 음수입니다({save.TrainingVictoryCount}).";

            var cleared = new HashSet<int>();
            foreach (int number in save.ClearedStages)
            {
                if (!IsValidStage(number)) return $"없는 스테이지 {number}을(를) 클리어했다고 되어 있습니다.";
                if (!cleared.Add(number)) return $"스테이지 {number}의 클리어가 중복되었습니다.";
            }
            if (save.TrainingVictoryCount > 0 && (!cleared.Contains(1) || !cleared.Contains(2)))
                return "스테이지 3 해금 전 수련 기록이 있습니다.";

            string curriculumError = curriculum.Validate(save.CurriculumCompleted);
            if (curriculumError != null) return curriculumError;
            // Owned skills follow completed nodes, first-clear stage rewards and field pickups taken; the loadout and the
            // experience records may only use those.
            var owned = new Dictionary<int, LegacySkill>();
            foreach (LegacySkill skill in LegacyInitialSkills.All) owned[skill.Id] = skill;
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialCounterSkills) owned[skill.Id] = skill;
            foreach (LegacySkill skill in LegacySkillDefinitions.PrototypeSkills) owned[skill.Id] = skill;
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
            var collected = new HashSet<string>();
            foreach (string id in save.FieldPickups)
            {
                CampaignFieldPickup pickup = CampaignFieldPickups.Find(id);
                if (pickup == null) return $"없는 필드 습득물 '{id}'을(를) 주웠다고 되어 있습니다.";
                if (!collected.Add(id)) return $"필드 습득물 '{id}'이(가) 중복되었습니다.";
                LegacySkill skill = FindSheetSkill(pickup.SkillId);
                if (skill == null) return $"필드 습득물 '{id}'이(가) 주는 기술 {pickup.SkillId}이(가) 없습니다.";
                owned[pickup.SkillId] = skill;
            }

            var experienced = new HashSet<int>();
            foreach (KeyValuePair<int, int> entry in save.SkillExperience)
            {
                if (!owned.TryGetValue(entry.Key, out LegacySkill skill))
                    return $"경험치를 기록한 기술 {entry.Key}을(를) 보유하지 않습니다.";
                if (!experienced.Add(entry.Key)) return $"기술 {entry.Key}의 경험치가 중복되었습니다.";
                int maximum = CampaignOwnedSkill.ExperienceRequiredForCost(skill.Cost) * CampaignOwnedSkill.MaxLevel;
                if (entry.Key == 44 && save.OnslaughtExperienceVersion == 0)
                    maximum = 3 * CampaignOwnedSkill.MaxLevel;
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
                    if (IsCounterSkill(id)) return $"반격 기술 {id}은(는) 반격 칸에 편성해야 합니다.";
                    if (owned[id].LaneIndex != lane) return $"기술 {id}은(는) {lane + 1}번째 열에 편성할 수 없습니다.";
                    if (!equipped.Add(id)) return $"기술 {id}이(가) 중복으로 편성되어 있습니다.";
                }
            }
            if (save.CounterSkillId != 0 && (!owned.ContainsKey(save.CounterSkillId) || !IsCounterSkill(save.CounterSkillId)))
                return $"보유하지 않은 반격 기술 {save.CounterSkillId}이(가) 편성되어 있습니다.";
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
                ownedSkills.Add(new CampaignOwnedSkill(RequireCampaignSkill(skillId)));
        }

        private static LegacySkill RequireCampaignSkill(int skillId)
        {
            LegacySkill skill = LegacySkillDefinitions.Skill(skillId);
            if (LegacySkillDefinitions.Table.IsVersus(LegacySkillDefinitions.Find(skillId)))
                throw new InvalidOperationException($"대전 전용 기술 {skillId} {skill.Name}은(는) 캠페인에서 얻을 수 없습니다.");
            return skill;
        }

        // Any sheet row, not only 획득 ones: a node whose technique became a starting one then grants nothing new
        // instead of making its saves unreadable (CampaignSheetCheck reports that sheet).
        private static LegacySkill FindSheetSkill(int skillId)
        {
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(skillId);
            return LegacySkillDefinitions.Table.IsVersus(definition) ? null : definition?.Skill;
        }

        private static bool IsStartingSkill(int skillId)
        {
            foreach (LegacySkill skill in LegacyInitialSkills.All)
                if (skill.Id == skillId) return true;
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialCounterSkills)
                if (skill.Id == skillId) return true;
            return false;
        }

        private static bool IsAlwaysOwnedSkill(int skillId)
        {
            if (IsStartingSkill(skillId)) return true;
            foreach (LegacySkill skill in LegacySkillDefinitions.PrototypeSkills)
                if (skill.Id == skillId) return true;
            return false;
        }

        private static bool IsCounterDefinition(int skillId)
            => LegacySkillDefinitions.Find(skillId)?.CounterUsesPerTurn > 0;

        private bool IsUnlockedStageSkill(int skillId)
        {
            for (int i = 0; i < clearedStages.Length; i++)
                if (clearedStages[i] && stages[i].FirstClearSkillId == skillId) return true;
            return false;
        }

        private bool IsCollectedFieldSkill(int skillId)
        {
            foreach (string id in collectedFieldPickups)
                if (CampaignFieldPickups.Find(id)?.SkillId == skillId) return true;
            return false;
        }

        /// <summary>Drops saved-lane skills that are no longer owned and fills the gaps with the lane's starting
        /// skills in their original order, so every lane keeps exactly three.</summary>
        private void RefillLanesWithStartingSkills()
        {
            if (equippedCounter != null && !ownedSkills.Contains(equippedCounter)) equippedCounter = null;
            for (int lane = 0; lane < equippedLanes.Length; lane++)
            {
                List<CampaignOwnedSkill> skills = equippedLanes[lane];
                skills.RemoveAll(owned => !ownedSkills.Contains(owned));
                foreach (CampaignOwnedSkill owned in ownedSkills)
                {
                    if (skills.Count >= loadoutSlots[lane].Length) break;
                    if (!IsCounterSkill(owned.SkillId) && owned.Skill.LaneIndex == lane && IsStartingSkill(owned.SkillId) &&
                        !skills.Contains(owned)) skills.Add(owned);
                }
            }
        }

        private bool CanEditLoadout => Phase == CampaignPhase.Lobby || Phase == CampaignPhase.Maintenance;
        private bool IsValidStage(int number) => number >= 1 && number <= StageCount;

        // Fights open as 1-2, 3-5, then 6-8. Story missions apply the separate StageLimit gate.
        private int UnlockedGroupEnd()
        {
            for (int number = 1; number <= 2; number++)
                if (!clearedStages[number - 1]) return 2;
            for (int number = 3; number <= 5; number++)
                if (!clearedStages[number - 1]) return 5;
            return StageCount;
        }

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
            draftCounter = equippedCounter;
            for (int lane = 0; lane < loadoutSlots.Length; lane++)
                for (int slot = 0; slot < loadoutSlots[lane].Length; slot++)
                    loadoutSlots[lane][slot] = slot < equippedLanes[lane].Count ? equippedLanes[lane][slot] : null;
        }

        private bool IsValidLoadout()
        {
            if (draftCounter != null && (!IsCounterSkill(draftCounter.SkillId) ||
                !ReferenceEquals(FindOwnedSkill(draftCounter.SkillId), draftCounter))) return false;
            for (int lane = 0; lane < loadoutSlots.Length; lane++)
                for (int slot = 0; slot < loadoutSlots[lane].Length; slot++)
                {
                    CampaignOwnedSkill owned = loadoutSlots[lane][slot];
                    if (owned == null || IsCounterSkill(owned.SkillId) || owned.Skill.LaneIndex != lane ||
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
            ClearLastCurriculumPointGain();
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
            EnemyHealth = IsBossEncounter ? GreenRibbonBossBattle.EnemyHealth : number == 5 ? 50 : 80 + 15 * (number - 1);
            EnemyResistance = IsBossEncounter ? GreenRibbonBossBattle.EnemyResistance : number == 5 ? 10 : 15 + 3 * (number - 1);
            EnemyPowerBonus = IsBossEncounter || number == 5 ? 0 : number - 1;
            Reward = 60 + 10 * (number - 1);
            FirstClearSkillId = number == 1 ? 43 : number == 2 ? 44 : number == 3 ? 45 : number == 5 ? 46 : number == 7 ? 16 : 0;
            // Placeholder counters until enemy archetypes exist: a guard (막기, 7) that taxes a
            // one-sided attack from stage five, then a strong thrust (정교한 찌르기, 4) from stage seven.
            enemyCounterSkillId = IsBossEncounter ? GreenRibbonBossBattle.EnemyCounterSkillId : number >= 7 ? 4 : number == 6 ? 7 : 0;
            EnemyCountersPerTurn = enemyCounterSkillId == 0 ? 0 : 1;
        }

        public int Number { get; }
        public bool IsBossEncounter => Number == GreenRibbonBossBattle.StageNumber;
        public string Name { get; }
        public int EnemyHealth { get; }
        public int EnemyResistance { get; }
        public int EnemyPowerBonus { get; }
        public int EnemyCount => Number == 5 ? 3 : 1;
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
                skill.AnimationName, skill.IconId, skill.DurationHalfUnits);
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
