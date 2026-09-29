using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    public enum CampaignPhase { Battle, Maintenance, Failed, Completed, Lobby }

    /// <summary>One in-memory eight-stage run. Combat rules remain in LegacyQueuedDuel.</summary>
    public sealed class CampaignRun
    {
        private static readonly CampaignStage[] stages =
        {
            new CampaignStage(1, "숲길 입구"),
            new CampaignStage(2, "이끼 낀 오솔길"),
            new CampaignStage(3, "안개 숲"),
            new CampaignStage(4, "고목의 갈림길"),
            new CampaignStage(5, "깊은 녹음"),
            new CampaignStage(6, "숲의 경계"),
            new CampaignStage(7, "마지막 고갯길"),
            new CampaignStage(8, "숲의 끝 결투"),
        };

        private readonly List<CampaignOwnedSkill> ownedSkills = new List<CampaignOwnedSkill>();
        private readonly List<CampaignSkillOffer> offers = new List<CampaignSkillOffer>();
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

        public CampaignRun()
        {
            OwnedSkills = ownedSkills.AsReadOnly();
            Offers = offers.AsReadOnly();
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
        public int EquippedSkillCount => equippedLanes[0].Count + equippedLanes[1].Count + equippedLanes[2].Count;
        public CampaignStage CurrentStage => stages[StageNumber - 1];
        public IReadOnlyList<CampaignOwnedSkill> OwnedSkills { get; }
        public IReadOnlyList<CampaignSkillOffer> Offers { get; }
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

        public bool ReturnToLobby()
        {
            if (Phase == CampaignPhase.Battle) return false;
            Phase = CampaignPhase.Lobby;
            return true;
        }

        public bool TryAbandonBattle()
        {
            if (Phase != CampaignPhase.Battle) return false;
            LastReward = 0;
            Phase = CampaignPhase.Lobby;
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
                slot < 0 || slot >= loadoutSlots[lane].Length) return false;
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
            if (owned == null) return false;
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
            foreach (CampaignOwnedSkill[] lane in loadoutSlots)
                for (int i = 0; i < lane.Length; i++)
                    if (lane[i] != null && lane[i].SkillId == skillId)
                    {
                        lane[i] = null;
                        return true;
                    }
            return false;
        }

        public bool TryMoveEquippedSkill(int skillId, int direction)
        {
            if (!CanEditLoadout || (direction != -1 && direction != 1)) return false;
            foreach (CampaignOwnedSkill[] lane in loadoutSlots)
                for (int i = 0; i < lane.Length; i++)
                    if (lane[i] != null && lane[i].SkillId == skillId)
                    {
                        int target = i + direction;
                        if (target < 0 || target >= lane.Length) return false;
                        CampaignOwnedSkill other = lane[target];
                        lane[target] = lane[i];
                        lane[i] = other;
                        return true;
                    }
            return false;
        }

        public bool TryCompleteBattle(DuelMatchOutcome outcome)
        {
            if (Phase != CampaignPhase.Battle) return false;
            if (outcome != DuelMatchOutcome.PlayerVictory && outcome != DuelMatchOutcome.EnemyVictory
                && outcome != DuelMatchOutcome.Draw) return false;

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
            }
            Phase = StageNumber == StageCount ? CampaignPhase.Completed : CampaignPhase.Maintenance;
            return true;
        }

        public bool TryStartNextStage()
        {
            if (Phase != CampaignPhase.Maintenance || StageNumber >= StageCount) return false;
            return TryStartStage(StageNumber + 1);
        }

        public bool RetryCurrentStage()
        {
            if (Phase != CampaignPhase.Failed || !CanEnterStage(StageNumber)) return false;
            BeginStage(StageNumber);
            return true;
        }

        public bool TryAcquireSkill(int skillId)
        {
            if (!CanEditLoadout) return false;
            for (int i = 0; i < offers.Count; i++)
            {
                CampaignSkillOffer offer = offers[i];
                if (offer.SkillId != skillId) continue;
                if (Currency < offer.Price) return false;
                Currency -= offer.Price;
                ownedSkills.Add(new CampaignOwnedSkill(offer.Skill));
                offers.RemoveAt(i);
                return true;
            }
            return false;
        }

        public bool TryUpgradeSkill(int skillId)
        {
            if (!CanEditLoadout) return false;
            foreach (CampaignOwnedSkill owned in ownedSkills)
            {
                if (owned.SkillId != skillId) continue;
                if (owned.Level >= CampaignOwnedSkill.MaximumLevel || Currency < owned.UpgradeCost) return false;
                Currency -= owned.UpgradeCost;
                owned.Upgrade();
                return true;
            }
            return false;
        }

        public LegacyQueuedDuel CreateDuel(int randomSeed = 1)
        {
            var playerSkills = new LegacySkill[EquippedSkillCount];
            int index = 0;
            foreach (List<CampaignOwnedSkill> lane in equippedLanes)
                foreach (CampaignOwnedSkill owned in lane) playerSkills[index++] = owned.Skill;

            var enemySkills = new LegacySkill[6];
            for (int i = 0; i < enemySkills.Length; i++)
            {
                // From stage three, one forecasted guard in the six-action
                // cycle gives the imported guard counter an actual opponent.
                enemySkills[i] = WithStagePower(LegacyInitialSkills.All[CurrentStage.Number >= 3 && i == 4 ? 6 : i]);
            }
            LegacyCounter enemyCounter = CurrentStage.EnemyCounterBasis == null ? null
                : new LegacyCounter(WithStagePower(CurrentStage.EnemyCounterBasis), CurrentStage.EnemyCountersPerTurn);

            return new LegacyQueuedDuel(100, 50, CurrentStage.EnemyHealth, CurrentStage.EnemyResistance,
                playerSkills, enemySkills, new[] { 2, 3, 2, 1 }, randomSeed, enemyCounter: enemyCounter);
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
            offers.Clear();
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) offers.Add(new CampaignSkillOffer(skill));
            StageNumber = 1;
            Currency = LastReward = 0;
            Array.Clear(clearedStages, 0, clearedStages.Length);
            HighestUnlockedStage = 1;
            ClearedStageCount = 0;
            Phase = CampaignPhase.Lobby;
        }

        private bool CanEditLoadout => Phase == CampaignPhase.Lobby || Phase == CampaignPhase.Maintenance;
        private bool IsValidStage(int number) => number >= 1 && number <= StageCount;

        private bool CanEnterStage(int number)
        {
            if (!IsValidStage(number) || number > HighestUnlockedStage || HasLoadoutChanges) return false;
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
            LastReward = 0;
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
        internal CampaignStage(int number, string name)
        {
            Number = number;
            Name = name;
            EnemyHealth = 80 + 15 * (number - 1);
            EnemyResistance = 15 + 3 * (number - 1);
            EnemyPowerBonus = number - 1;
            Reward = 60 + 10 * (number - 1);
            // Placeholder counters until enemy archetypes exist: a guard that taxes a
            // one-sided attack from stage five, a heavy smash that clashes with it from seven.
            EnemyCounterBasis = number >= 7 ? LegacyInitialSkills.All[5] : number >= 5 ? LegacyInitialSkills.All[6] : null;
            EnemyCountersPerTurn = EnemyCounterBasis == null ? 0 : 1;
        }

        public int Number { get; }
        public string Name { get; }
        public int EnemyHealth { get; }
        public int EnemyResistance { get; }
        public int EnemyPowerBonus { get; }
        /// <summary>The enemy counter before this stage's power bonus, or null when it has none.</summary>
        public LegacySkill EnemyCounterBasis { get; }
        public int EnemyCountersPerTurn { get; }
        public int Reward { get; }
    }

    public sealed class CampaignOwnedSkill
    {
        public const int MaximumLevel = 3;

        internal CampaignOwnedSkill(LegacySkill baseSkill)
        {
            BaseSkill = baseSkill ?? throw new ArgumentNullException(nameof(baseSkill));
            Skill = BaseSkill;
        }

        public LegacySkill BaseSkill { get; }
        public LegacySkill Skill { get; private set; }
        public int SkillId => BaseSkill.Id;
        public int Level { get; private set; }
        public int UpgradeCost => 30 + 25 * Level;

        internal void Upgrade()
        {
            if (Level >= MaximumLevel) throw new InvalidOperationException("The skill is already fully upgraded.");
            Level++;
            int bonus = 2 * Level;
            Skill = new LegacySkill(BaseSkill.Id, BaseSkill.Name + " +" + Level, BaseSkill.Cost,
                BaseSkill.MinPower + bonus, BaseSkill.MaxPower + bonus, BaseSkill.Kind, BaseSkill.Property,
                BaseSkill.AttackCount, BaseSkill.LaneIndex, BaseSkill.Description, BaseSkill.AnimationName, BaseSkill.IconId);
        }
    }

    public sealed class CampaignSkillOffer
    {
        internal CampaignSkillOffer(LegacySkill skill)
        {
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            Price = 45 + 10 * (skill.Cost - 1);
        }

        public LegacySkill Skill { get; }
        public int SkillId => Skill.Id;
        public int Price { get; }
    }
}
