using System;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public sealed class LegacyFighterState
    {
        internal LegacyFighterState(int maxHealth, int maxResistance, int healthFloor = 0)
        {
            if (maxHealth <= 0 || maxResistance < 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (healthFloor < 0 || healthFloor >= maxHealth) throw new ArgumentOutOfRangeException(nameof(healthFloor));
            MaxHealth = Health = maxHealth;
            MaxResistance = Resistance = maxResistance;
            HealthFloor = healthFloor;
        }

        // A forecast's copy (LegacySlotForecast): the same numbers, the pending recovery included, on a state of its own.
        private LegacyFighterState(LegacyFighterState source)
        {
            MaxHealth = source.MaxHealth;
            Health = source.Health;
            HealthFloor = source.HealthFloor;
            MaxResistance = source.MaxResistance;
            Resistance = source.Resistance;
            recoveryPending = source.recoveryPending;
        }

        internal LegacyFighterState Copy() => new LegacyFighterState(this);

        public int Health { get; private set; }
        public int MaxHealth { get; }
        /// <summary>Damage never takes health below this; above zero, the fighter cannot be defeated.</summary>
        public int HealthFloor { get; }
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

        /// <param name="conditionalDamagePercent">The attacker's 조건 피해 배율 when its 상대 상태 조건 held against this
        /// fighter before the hit, otherwise zero. It multiplies the health damage last, a breaking hit's overflow included.</param>
        internal LegacyHitImpact ReceiveHit(int power, bool resistanceHit, double receivedMultiplier,
            bool halveDamage = false, int brokenTargetDamagePercent = 0, int conditionalDamagePercent = 0)
        {
            int scaled = Round(power * receivedMultiplier);
            if (resistanceHit)
            {
                LegacyDamageReason reasons = receivedMultiplier < 1d && scaled < power
                    ? LegacyDamageReason.ProtectionReduction : LegacyDamageReason.None;
                if (Resistance <= scaled)
                {
                    // The overflow already includes the received-damage modifier. Apply only the remaining health
                    // rules here, before setting resistance to zero, so this breaking hit is not doubled unless the
                    // resistance was already broken. The incoming modifier and its rounding must not run twice.
                    LegacyHitImpact impact = ReceiveHealthDamage(scaled - Resistance, 1d,
                        halveDamage, brokenTargetDamagePercent, conditionalDamagePercent);
                    Resistance = halveDamage ? Resistance - Round(Resistance / 2d) : 0;
                    if (halveDamage && Resistance > 0) reasons |= LegacyDamageReason.PressureGuard;
                    return impact.WithReasons(reasons);
                }
                if (halveDamage)
                {
                    int before = scaled;
                    scaled = Round(scaled / 2d);
                    if (scaled < before) reasons |= LegacyDamageReason.PressureGuard;
                }
                Resistance -= scaled;
                return new LegacyHitImpact(scaled, scaled, reasons);
            }
            return ReceiveHealthDamage(power, receivedMultiplier, halveDamage, brokenTargetDamagePercent, conditionalDamagePercent);
        }

        private LegacyHitImpact ReceiveHealthDamage(int power, double receivedMultiplier,
            bool halveDamage = false, int brokenTargetDamagePercent = 0, int conditionalDamagePercent = 0)
        {
            int pushPower = Round(power * receivedMultiplier);
            int damage = pushPower;
            LegacyDamageReason reasons = receivedMultiplier < 1d && damage < power
                ? LegacyDamageReason.ProtectionReduction : LegacyDamageReason.None;
            if (Resistance <= 0)
            {
                damage *= 2;
                if (damage > pushPower) reasons |= LegacyDamageReason.BrokenDouble;
            }
            if (halveDamage)
            {
                // Apply defensive pressure after normal guard/buff/broken rules,
                // before HP clamping. Keep the combat's existing ties-to-even rounding.
                int before = damage;
                pushPower = Round(pushPower / 2d);
                damage = Round(damage / 2d);
                if (damage < before) reasons |= LegacyDamageReason.PressureGuard;
            }
            if (brokenTargetDamagePercent > 0)
            {
                int before = damage;
                damage = Round(damage * (1d + brokenTargetDamagePercent / 100d));
                if (damage != before) reasons |= LegacyDamageReason.BrokenBonus;
            }
            // 조건 피해 배율 last, on whatever the rules above left; the knockback (push power) keeps the plain value.
            if (conditionalDamagePercent > 0)
            {
                int before = damage;
                damage = Round(damage * (conditionalDamagePercent / 100d));
                if (damage != before) reasons |= LegacyDamageReason.ConditionalMultiplier;
            }
            Health = Math.Max(HealthFloor, Health - damage);
            return new LegacyHitImpact(pushPower, damage, reasons);
        }

        internal LegacyHitImpact ReceiveFlatHealthDamage(int power)
        {
            int damage = Math.Max(0, power);
            Health = Math.Max(HealthFloor, Health - damage);
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

        /// <summary>상대 붕괴: all resistance goes at once, leaving the same broken state a breaking hit leaves (zero
        /// resistance; BeginTurn keeps it one more turn, then restores it). Returns the resistance taken.</summary>
        internal int BreakResistance() => ReduceResistance(Resistance);

        private static int Round(double value) => Math.Max(0, (int)Math.Round(value, MidpointRounding.ToEven));
    }

    internal readonly struct LegacyHitImpact
    {
        public LegacyHitImpact(int pushPower, int displayedDamage, LegacyDamageReason damageReasons = LegacyDamageReason.None)
        {
            PushPower = pushPower;
            DisplayedDamage = displayedDamage;
            DamageReasons = damageReasons;
        }
        public int PushPower { get; }
        public int DisplayedDamage { get; }
        public LegacyDamageReason DamageReasons { get; }
        public LegacyHitImpact WithReasons(LegacyDamageReason reasons)
            => new LegacyHitImpact(PushPower, DisplayedDamage, DamageReasons | reasons);
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

    public sealed class LegacyCurrentSlot : ILegacySlotView
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
        /// <summary>Null for an empty slot, including while <see cref="PendingPlayerCounter"/> awaits the first hit.</summary>
        public LegacySkill PlayerSkill { get; internal set; }
        public LegacySkill EnemySkill { get; }
        /// <summary>Counts the remaining slot length, including a pending counter; shrinks after
        /// a cancelled counter or a decisive hit.</summary>
        public int HitCount { get; internal set; }
        public int HitsResolved { get; internal set; }
        public bool IsResolved => HitsResolved >= HitCount;
        public bool DodgeSucceeded { get; internal set; }
        public bool PressureSucceeded { get; internal set; }
        /// <summary>Any dodge input during this slot before its first hit, successful or not, or the player's skill
        /// dodging by itself (<see cref="AutoDodge"/>).</summary>
        public bool DodgeAttempted { get; internal set; }
        /// <summary>The player's skill in this slot dodges by itself (사용 효과 자동 회피): an opposing attack, a counter
        /// included, is dodged from the start (<see cref="DodgeSucceeded"/>), and the dodge key is ignored for the slot
        /// (pressure still works). It is not a step: no attempt is counted and the next turn's ACT is untouched.</summary>
        public bool AutoDodge { get; internal set; }
        /// <summary>The player's counter answering this one-sided attack. It becomes <see cref="PlayerSkill"/>
        /// at the first hit unless a dodge is attempted first.</summary>
        public LegacySkill PendingPlayerCounter { get; internal set; }
        public bool PlayerCountered { get; internal set; }
        /// <summary>The enemy's counter fills its empty slot; <see cref="EnemySkill"/> is that counter.</summary>
        public bool EnemyCountered { get; internal set; }
        /// <summary>맞물림 for the player's queued skill in this slot: when meshed, its power carries the chain's bonus
        /// (<see cref="LegacyMeshSlot.BonusPercent"/>, beside <see cref="LegacySkillFeedback.PowerBuffPercent"/>) and the
        /// slot takes no steps. A counter in an empty slot never meshes.</summary>
        public LegacyMeshSlot PlayerMesh { get; internal set; }
        /// <summary>The 순환 count the player's queued skill read as this slot started (its power bonus); 0 when it does
        /// not cycle (<see cref="LegacyQueuedDuel.CycleUsesAtSlot"/>).</summary>
        public int PlayerCycleUses { get; internal set; }
        /// <summary>The next turn's planning seconds the player's queued skill added as this slot started (사용 효과 앞 X마다 다음
        /// 턴 제한시간), after the turn's cap; 0 for none (<see cref="LegacyQueuedDuel.NextPlanningSeconds"/>).</summary>
        public int PlanningSecondsGained { get; internal set; }
        public LegacySkillFeedback PlayerFeedback { get; internal set; } = LegacySkillFeedback.None;
        public LegacySkillFeedback EnemyFeedback { get; internal set; } = LegacySkillFeedback.None;
        /// <summary>The most health the player has lost to a single hit of this slot so far (presentation only:
        /// <see cref="LegacySlotFacts.PlayerLargestHitTaken"/>).</summary>
        public int PlayerLargestHitTaken { get; internal set; }
        public int EnemyLargestHitTaken { get; internal set; }
        /// <summary>The player's skill's 상대 상태 조건 has held against the enemy where the rules checked it this slot: the
        /// recovery's gate, the slot's start (after both sides' initial effects) for a damage multiplier, or one of its hits
        /// (presentation only: <see cref="LegacySlotFacts.PlayerStateConditionHeld"/>).</summary>
        public bool PlayerStateConditionHeld { get; internal set; }
        public bool EnemyStateConditionHeld { get; internal set; }
        internal int PlayerPower { get; set; }
        internal int EnemyPower { get; }
        internal int PlayerTotalPower { get; set; }
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
            DuelMatchOutcome outcome, bool playerDodged = false, bool playerPressured = false,
            bool enemyReachedHealthThreshold = false, bool playerAttackConditionMet = false,
            bool enemyAttackConditionMet = false)
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
            PlayerDamageReasons = playerImpact.DamageReasons;
            EnemyDamageReasons = enemyImpact.DamageReasons;
            IsFinalHit = slot.IsResolved;
            Outcome = outcome;
            EnemyReachedHealthThreshold = enemyReachedHealthThreshold;
            PlayerAttackConditionMet = playerAttackConditionMet;
            EnemyAttackConditionMet = enemyAttackConditionMet;
            PlayerMesh = slot.PlayerMesh;
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
        /// <summary>Modifiers on the hit received by the player, including a guard that prevented all damage.</summary>
        public LegacyDamageReason PlayerDamageReasons { get; }
        public LegacyDamageReason EnemyDamageReasons { get; }
        public bool IsFinalHit { get; }
        public DuelMatchOutcome Outcome { get; }
        /// <summary>This is the hit that first brought the enemy to the duel's health threshold or below
        /// (<see cref="LegacyQueuedDuel.EnemyHealthThresholdPercent"/>) while the duel goes on: the moment a story
        /// event may interrupt. At most once per attempt.</summary>
        public bool EnemyReachedHealthThreshold { get; }
        /// <summary>The player's attack at this hit met its technique's 상대 상태 조건 against the enemy as it landed, so
        /// its 조건 피해 배율 multiplied whatever health damage it dealt (none for a hit the resistance absorbed).</summary>
        public bool PlayerAttackConditionMet { get; }
        /// <summary>The enemy's attack at this hit met its technique's 상대 상태 조건 against the player as it landed
        /// (never for a dodged hit), so its 조건 피해 배율 multiplied whatever health damage it dealt.</summary>
        public bool EnemyAttackConditionMet { get; }
        /// <summary>맞물림 for the slot this hit belongs to (<see cref="LegacyCurrentSlot.PlayerMesh"/>).</summary>
        public LegacyMeshSlot PlayerMesh { get; }
    }
}
