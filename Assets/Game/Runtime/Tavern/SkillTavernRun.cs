using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Tavern
{
    /// <summary>A purchased copy; two copies of one technique have different instance ids.</summary>
    public sealed class SkillTavernCard
    {
        internal SkillTavernCard(int instanceId, LegacySkill skill)
        {
            InstanceId = instanceId;
            Skill = skill;
        }

        public int InstanceId { get; }
        public LegacySkill Skill { get; }
    }

    /// <summary>The local shop and fixed formation experiment. It does not modify campaign progress.</summary>
    public sealed class SkillTavernRun
    {
        public const int SlotCount = 8;
        public const int ShopSize = 3;
        public const int PurchasePrice = 3;
        public const int RefreshPrice = 1;
        public const int SellPrice = 1;
        public const int MaximumIncome = 10;

        private readonly LegacySkill[] catalog;
        private readonly LegacySkill[] shop = new LegacySkill[ShopSize];
        private readonly SkillTavernCard[] board = new SkillTavernCard[SlotCount];
        private readonly List<SkillTavernCard> inventory = new List<SkillTavernCard>();
        private readonly Random random;
        private int nextInstanceId = 1;

        public SkillTavernRun(int seed = 0) : this(DefaultCatalog(), seed) { }

        public SkillTavernRun(IEnumerable<LegacySkill> catalog, int seed = 0)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var copies = new List<LegacySkill>();
            var ids = new HashSet<int>();
            foreach (LegacySkill skill in catalog)
            {
                if (skill == null || skill.IsWait)
                    throw new ArgumentException("The shop needs non-wait techniques.", nameof(catalog));
                if (ids.Add(skill.Id)) copies.Add(Copy(skill));
            }
            if (copies.Count == 0) throw new ArgumentException("The shop needs at least one technique.", nameof(catalog));
            this.catalog = copies.ToArray();
            random = new Random(seed);
            ShopOffers = Array.AsReadOnly(shop);
            Board = Array.AsReadOnly(board);
            Inventory = inventory.AsReadOnly();
            NextRound();
        }

        public int RoundNumber { get; private set; }
        public int Gold { get; private set; }
        public int RoundIncome => (int)Math.Min(MaximumIncome, (long)RoundNumber + 2);
        public IReadOnlyList<LegacySkill> ShopOffers { get; }
        public IReadOnlyList<SkillTavernCard> Board { get; }
        public IReadOnlyList<SkillTavernCard> Inventory { get; }
        public int BattlesPlayed => Wins + Losses + Draws;
        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public int Draws { get; private set; }
        public DuelMatchOutcome? LastOutcome { get; private set; }

        internal void RecordBattleResult(DuelMatchOutcome outcome)
        {
            switch (outcome)
            {
                case DuelMatchOutcome.PlayerVictory: Wins++; break;
                case DuelMatchOutcome.EnemyVictory: Losses++; break;
                case DuelMatchOutcome.Draw: Draws++; break;
                default: throw new ArgumentOutOfRangeException(nameof(outcome));
            }
            LastOutcome = outcome;
        }

        public bool CanBuy(int shopIndex)
            => shopIndex >= 0 && shopIndex < ShopSize && shop[shopIndex] != null && Gold >= PurchasePrice;

        public bool CanBuyAndDeploy(int shopIndex)
            => CanBuy(shopIndex) && Array.IndexOf(board, null) >= 0;

        public bool CanRefreshShop => Gold >= RefreshPrice;

        public bool CanDeploy(int instanceId)
            => InventoryIndex(instanceId) >= 0 && Array.IndexOf(board, null) >= 0;

        public bool CanSellBoard(int slotIndex)
            => slotIndex >= 0 && slotIndex < SlotCount && board[slotIndex] != null;

        public bool CanSellInventory(int instanceId) => InventoryIndex(instanceId) >= 0;

        public bool TryBuy(int shopIndex)
        {
            if (!CanBuy(shopIndex)) return false;
            var card = new SkillTavernCard(nextInstanceId, Copy(shop[shopIndex]));
            inventory.Add(card);
            nextInstanceId++;
            shop[shopIndex] = null;
            Gold -= PurchasePrice;
            return true;
        }

        public bool TryBuyAndDeploy(int shopIndex)
        {
            if (!CanBuyAndDeploy(shopIndex)) return false;
            int slot = Array.IndexOf(board, null);
            var card = new SkillTavernCard(nextInstanceId, Copy(shop[shopIndex]));
            board[slot] = card;
            nextInstanceId++;
            shop[shopIndex] = null;
            Gold -= PurchasePrice;
            return true;
        }

        public bool TryRefreshShop()
        {
            if (!CanRefreshShop) return false;
            Gold -= RefreshPrice;
            RefillShop();
            return true;
        }

        public bool TryDeploy(int instanceId)
        {
            if (!CanDeploy(instanceId)) return false;
            int index = InventoryIndex(instanceId);
            int slot = Array.IndexOf(board, null);
            board[slot] = inventory[index];
            inventory.RemoveAt(index);
            return true;
        }

        public bool TrySellBoard(int slotIndex)
        {
            if (!CanSellBoard(slotIndex)) return false;
            board[slotIndex] = null;
            Gold += SellPrice;
            return true;
        }

        public bool TrySellInventory(int instanceId)
        {
            if (!CanSellInventory(instanceId)) return false;
            int index = InventoryIndex(instanceId);
            inventory.RemoveAt(index);
            Gold += SellPrice;
            return true;
        }

        public void NextRound()
        {
            RoundNumber++;
            Gold = RoundIncome;
            RefillShop();
        }

        private void RefillShop()
        {
            // Shuffle candidate indices rather than the catalog, so the catalog itself stays unchanged.
            var candidates = new int[catalog.Length];
            for (int index = 0; index < candidates.Length; index++) candidates[index] = index;
            for (int index = 0; index < Math.Min(ShopSize, candidates.Length); index++)
            {
                int selected = random.Next(index, candidates.Length);
                int previous = candidates[index];
                candidates[index] = candidates[selected];
                candidates[selected] = previous;
            }
            for (int index = 0; index < ShopSize; index++)
                shop[index] = Copy(catalog[candidates[index % candidates.Length]]);
        }

        /// <summary>Empty positions remain breathing actions; selling never shifts later techniques.</summary>
        public LegacySkill[] BuildQueue()
        {
            var queue = new LegacySkill[SlotCount];
            for (int index = 0; index < SlotCount; index++)
                queue[index] = board[index] == null ? LegacyCommonActions.Breathe : Copy(board[index].Skill);
            return queue;
        }

        private int InventoryIndex(int instanceId)
        {
            for (int index = 0; index < inventory.Count; index++)
                if (inventory[index].InstanceId == instanceId) return index;
            return -1;
        }

        private static IEnumerable<LegacySkill> DefaultCatalog()
        {
            LegacySkillTable table = LegacySkillDefinitions.Table;
            foreach (LegacySkillDefinition definition in table.All)
            {
                LegacySkillGroup group = table.GroupOf(definition);
                if ((group == LegacySkillGroup.Starting || group == LegacySkillGroup.Acquisition) &&
                    !definition.IsCounter && !definition.Skill.IsWait)
                    yield return definition.Skill;
            }
        }

        private static LegacySkill Copy(LegacySkill skill)
            => new LegacySkill(skill.Id, skill.Name, skill.Cost, skill.MinPower, skill.MaxPower,
                skill.Kind, skill.Property, skill.AttackCount, skill.LaneIndex, skill.Description,
                skill.AnimationName, skill.IconId, skill.DurationHalfUnits);
    }
}
