using System;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public sealed class LegacyFighterState
    {
        internal LegacyFighterState(int maxHealth, int maxResistance)
        {
            if (maxHealth <= 0 || maxResistance < 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            MaxHealth = Health = maxHealth;
            MaxResistance = Resistance = maxResistance;
        }

        public int Health { get; private set; }
        public int MaxHealth { get; }
        public int Resistance { get; private set; }
        public int MaxResistance { get; }
        public bool IsResistanceBroken => Resistance <= 0;
        public bool IsDefeated => Health <= 0;
        private bool recoveryPending;

        internal void BeginTurn()
        {
            // Original Unit.TurnInit leaves one complete planning/resolution turn
            // with broken resistance, then restores it at the following turn.
            if (Resistance <= 0 && !recoveryPending) recoveryPending = true;
            else if (recoveryPending)
            {
                Resistance = MaxResistance;
                recoveryPending = false;
            }
        }

        internal LegacyHitImpact ReceiveHit(int power, bool resistanceHit, double receivedMultiplier, bool halveDamage = false)
        {
            int scaled = Round(power * receivedMultiplier);
            if (resistanceHit)
            {
                if (Resistance <= scaled)
                {
                    // Original ShieldDamage calls Damage before setting shield to
                    // zero. The breaking hit's overflow is not doubled unless the
                    // resistance was already broken; its multiplier applies twice.
                    LegacyHitImpact impact = ReceiveHealthDamage(scaled - Resistance, receivedMultiplier, halveDamage);
                    Resistance = halveDamage ? Resistance - Round(Resistance / 2d) : 0;
                    return impact;
                }
                if (halveDamage) scaled = Round(scaled / 2d);
                Resistance -= scaled;
                return new LegacyHitImpact(scaled, scaled);
            }
            return ReceiveHealthDamage(power, receivedMultiplier, halveDamage);
        }

        private LegacyHitImpact ReceiveHealthDamage(int power, double receivedMultiplier, bool halveDamage = false)
        {
            int pushPower = Round(power * receivedMultiplier);
            int damage = pushPower;
            if (Resistance <= 0) damage *= 2;
            if (halveDamage)
            {
                // Apply defensive pressure after normal guard/buff/broken rules,
                // before HP clamping. Keep the combat's existing ties-to-even rounding.
                pushPower = Round(pushPower / 2d);
                damage = Round(damage / 2d);
            }
            Health = Math.Max(0, Health - damage);
            return new LegacyHitImpact(pushPower, damage);
        }

        internal LegacyHitImpact ReceiveFlatHealthDamage(int power)
        {
            int damage = Math.Max(0, power);
            Health = Math.Max(0, Health - damage);
            return new LegacyHitImpact(damage, damage);
        }

        internal int RestoreResistance(int amount)
        {
            int previous = Resistance;
            Resistance = Math.Min(MaxResistance, Resistance + Math.Max(0, amount));
            // Keep any already scheduled automatic recovery at the turn boundary.
            return Resistance - previous;
        }

        internal int ReduceResistance(int amount)
        {
            // Skill effects alter resistance directly: no HP overflow, damage
            // multipliers, or guard mitigation apply to this state change.
            int previous = Resistance;
            Resistance = Math.Max(0, Resistance - Math.Max(0, amount));
            return previous - Resistance;
        }

        private static int Round(double value) => Math.Max(0, (int)Math.Round(value, MidpointRounding.ToEven));
    }

    internal readonly struct LegacyHitImpact
    {
        public LegacyHitImpact(int pushPower, int displayedDamage)
        {
            PushPower = pushPower;
            DisplayedDamage = displayedDamage;
        }
        public int PushPower { get; }
        public int DisplayedDamage { get; }
    }

    public sealed class LegacySlotResult
    {
        internal LegacySlotResult(int slotIndex, LegacySkill playerSkill, LegacySkill enemySkill,
            int playerHealthDamage, int enemyHealthDamage, int playerResistanceDamage,
            int enemyResistanceDamage, DuelMatchOutcome outcome)
        {
            SlotIndex = slotIndex;
            PlayerSkill = playerSkill;
            EnemySkill = enemySkill;
            PlayerHealthDamage = playerHealthDamage;
            EnemyHealthDamage = enemyHealthDamage;
            PlayerResistanceDamage = playerResistanceDamage;
            EnemyResistanceDamage = enemyResistanceDamage;
            Outcome = outcome;
        }

        public int SlotIndex { get; }
        public LegacySkill PlayerSkill { get; }
        public LegacySkill EnemySkill { get; }
        public int PlayerHealthDamage { get; }
        public int EnemyHealthDamage { get; }
        /// <summary>Net loss over the slot, including initial effects; restoration is negative.</summary>
        public int PlayerResistanceDamage { get; }
        /// <summary>Net loss over the slot, including initial effects; restoration is negative.</summary>
        public int EnemyResistanceDamage { get; }
        public DuelMatchOutcome Outcome { get; }
    }

    public sealed class LegacyCurrentSlot
    {
        internal LegacyCurrentSlot(int index, LegacySkill playerSkill, LegacySkill enemySkill,
            int playerPower, int enemyPower, int playerTotalPower,
            double playerReceivedMultiplier, double enemyReceivedMultiplier,
            LegacyFighterState player, LegacyFighterState enemy)
        {
            SlotIndex = index;
            PlayerSkill = playerSkill;
            EnemySkill = enemySkill;
            HitCount = Math.Max(1, Math.Max(playerSkill?.AttackCount ?? 0, enemySkill?.AttackCount ?? 0));
            PlayerPower = playerPower;
            EnemyPower = enemyPower;
            PlayerTotalPower = playerTotalPower;
            PlayerReceivedMultiplier = playerReceivedMultiplier;
            EnemyReceivedMultiplier = enemyReceivedMultiplier;
            PlayerStartingHealth = player.Health;
            EnemyStartingHealth = enemy.Health;
            PlayerStartingResistance = player.Resistance;
            EnemyStartingResistance = enemy.Resistance;
        }

        public int SlotIndex { get; }
        public LegacySkill PlayerSkill { get; }
        public LegacySkill EnemySkill { get; }
        public int HitCount { get; }
        public int HitsResolved { get; internal set; }
        public bool IsResolved => HitsResolved >= HitCount;
        public bool DodgeSucceeded { get; internal set; }
        public bool PressureSucceeded { get; internal set; }
        public LegacySkillFeedback PlayerFeedback { get; internal set; } = LegacySkillFeedback.None;
        public LegacySkillFeedback EnemyFeedback { get; internal set; } = LegacySkillFeedback.None;
        internal int PlayerPower { get; }
        internal int EnemyPower { get; }
        internal int PlayerTotalPower { get; }
        internal double PlayerReceivedMultiplier { get; }
        internal double EnemyReceivedMultiplier { get; }
        internal int PlayerStartingHealth { get; }
        internal int EnemyStartingHealth { get; }
        internal int PlayerStartingResistance { get; }
        internal int EnemyStartingResistance { get; }
    }

    public sealed class LegacyHitResult
    {
        internal LegacyHitResult(LegacyCurrentSlot slot, int hitIndex, bool playerAttacked, bool enemyAttacked,
            int playerHealthDamage, int enemyHealthDamage, int playerResistanceDamage,
            int enemyResistanceDamage, LegacyHitImpact playerImpact, LegacyHitImpact enemyImpact,
            DuelMatchOutcome outcome, bool playerDodged = false, bool playerPressured = false)
        {
            SlotIndex = slot.SlotIndex;
            PlayerSkill = slot.PlayerSkill;
            EnemySkill = slot.EnemySkill;
            HitIndex = hitIndex;
            PlayerAttacked = playerAttacked;
            EnemyAttacked = enemyAttacked;
            PlayerDodged = playerDodged;
            PlayerPressured = playerPressured;
            PlayerHealthDamage = playerHealthDamage;
            EnemyHealthDamage = enemyHealthDamage;
            PlayerResistanceDamage = playerResistanceDamage;
            EnemyResistanceDamage = enemyResistanceDamage;
            PlayerPushPower = playerImpact.PushPower;
            EnemyPushPower = enemyImpact.PushPower;
            PlayerDisplayedDamage = playerImpact.DisplayedDamage;
            EnemyDisplayedDamage = enemyImpact.DisplayedDamage;
            IsFinalHit = slot.IsResolved;
            Outcome = outcome;
        }

        public int SlotIndex { get; }
        public LegacySkill PlayerSkill { get; }
        public LegacySkill EnemySkill { get; }
        public int HitIndex { get; }
        public bool PlayerAttacked { get; }
        public bool EnemyAttacked { get; }
        public bool PlayerDodged { get; }
        public bool PlayerPressured { get; }
        public int PlayerHealthDamage { get; }
        public int EnemyHealthDamage { get; }
        public int PlayerResistanceDamage { get; }
        public int EnemyResistanceDamage { get; }
        public int PlayerPushPower { get; }
        public int EnemyPushPower { get; }
        public int PlayerDisplayedDamage { get; }
        public int EnemyDisplayedDamage { get; }
        public bool IsFinalHit { get; }
        public DuelMatchOutcome Outcome { get; }
    }
}
