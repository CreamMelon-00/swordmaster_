using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Rules shared by combat resolution and prospective opponent highlighting.</summary>
    public static class LegacySkillConditions
    {
        public static bool HasOpponentCondition(LegacySkill skill)
            => LegacySkillDefinitions.Find(skill)?.Effect.HasOpponentCondition == true;

        public static bool MatchesOpponent(LegacySkill skill, LegacySkill opposingSkill)
        {
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            if (effect == null || !effect.HasOpponentCondition || opposingSkill == null) return false;
            return effect.OpponentProperty.HasValue ? opposingSkill.Property == effect.OpponentProperty.Value
                : opposingSkill.Kind == effect.OpponentKind.Value;
        }

        /// <summary>Whether the skill has a 상대 상태 조건 (붕괴, 체력 N% 이하) for its recovery or damage multiplier.</summary>
        public static bool HasOpponentStateCondition(LegacySkill skill)
            => LegacySkillDefinitions.Find(skill)?.Effect.HasOpponentStateCondition == true;

        /// <summary>Whether the skill's 상대 상태 조건 holds against <paramref name="opponent"/> as it stands and something
        /// it gates would act for <paramref name="self"/>: the 조건 피해 배율, or a 저항 회복 with room to restore (as
        /// <see cref="MatchesSelfCondition"/>). It reads the condition only: a slot starting in this state reports
        /// <see cref="LegacySkillFeedback.ConditionMet"/> when the recovery restores something or the multiplier is armed
        /// (its hits reach health, <see cref="LegacySkillFeedback.ConditionalDamagePercent"/>); the multiplier is checked
        /// again on every hit.</summary>
        public static bool MatchesOpponentState(LegacySkill skill, LegacyFighterState self, LegacyFighterState opponent)
        {
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            if (effect == null || !effect.HasOpponentStateCondition || !effect.OpponentStateHolds(opponent)) return false;
            return (effect.ConditionalDamagePercent > 0 && skill.Kind == LegacySkillKind.Attack) ||
                MatchesSelfCondition(skill, self);
        }

        /// <summary>Whether the skill's 저항 회복 waits on a 상대 상태 조건 that holds against <paramref name="opponent"/> as it
        /// stands: the gate the rules read first of the skill's initial effects, whatever the recovery then restores (none at
        /// full resistance). Presentation reads it for 간파 (<see cref="LegacySlotFacts.PlayerStateConditionHeld"/>).</summary>
        internal static bool RecoveryStateHolds(LegacySkill skill, LegacyFighterState opponent)
        {
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            return effect != null && effect.ResistanceRecoveryPercent > 0 && effect.HasOpponentStateCondition &&
                effect.OpponentStateHolds(opponent);
        }

        /// <summary>Whether the attack's 조건 피해 배율 has its 상대 상태 조건 holding against <paramref name="opponent"/> as it
        /// stands, whether or not a hit will reach health (presentation's 간파, as above).</summary>
        internal static bool DamageStateHolds(LegacySkill skill, LegacyFighterState opponent)
        {
            if (skill == null || skill.Kind != LegacySkillKind.Attack) return false;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            return effect != null && effect.ConditionalDamagePercent > 0 && effect.OpponentStateHolds(opponent);
        }

        public static bool HasSelfCondition(LegacySkill skill)
            => LegacySkillDefinitions.Find(skill)?.Effect.ResistanceRecoveryPercent > 0;

        public static bool MatchesSelfCondition(LegacySkill skill, LegacyFighterState fighter)
            => HasSelfCondition(skill) && fighter != null && fighter.Resistance < fighter.MaxResistance
                && ResistanceRecoveryAmount(fighter, LegacySkillDefinitions.Find(skill).Effect.ResistanceRecoveryPercent) > 0;

        internal static int ResistanceRecoveryAmount(LegacyFighterState fighter, int percent)
            => (int)Math.Round(fighter.MaxResistance * (percent / 100d), MidpointRounding.ToEven);
    }

    /// <summary>Immutable feedback for the actual effect and buff snapshot of an initialized combat slot.</summary>
    public sealed class LegacySkillFeedback
    {
        internal static LegacySkillFeedback None { get; } = new LegacySkillFeedback(null, false, false, 0, 0);

        internal LegacySkillFeedback(LegacySkill skill, bool conditionMet, bool effectActivated,
            int powerBuffPercent, int protectionBuffPercent, int grantedPowerBuffPercent = 0,
            int grantedProtectionBuffPercent = 0, int grantedBuffSlots = 0,
            int actGainGranted = 0, int resistanceRestored = 0, int opponentResistanceReduced = 0,
            bool opponentBroken = false, int actStolen = 0, int actReceived = 0)
        {
            ConditionMet = conditionMet;
            EffectActivated = effectActivated;
            ActGainGranted = actGainGranted;
            ActStolen = actStolen;
            ActReceived = actReceived;
            ResistanceRestored = resistanceRestored;
            OpponentResistanceReduced = opponentResistanceReduced;
            OpponentBroken = opponentBroken;
            PowerBuffPercent = skill != null && !skill.IsWait ? powerBuffPercent : 0;
            ProtectionBuffPercent = skill != null ? protectionBuffPercent : 0;
            bool visibleTechnique = skill != null && !skill.IsWait && skill.Property != LegacySkillProperty.None;
            HasPowerBuff = visibleTechnique && PowerBuffPercent > 0;
            HasBeneficialBuff = visibleTechnique && (PowerBuffPercent > 0 || ProtectionBuffPercent > 0);
            GrantedPowerBuffPercent = visibleTechnique ? grantedPowerBuffPercent : 0;
            GrantedProtectionBuffPercent = visibleTechnique ? grantedProtectionBuffPercent : 0;
            GrantedBuffSlots = visibleTechnique ? grantedBuffSlots : 0;
            HasGrantedBeneficialBuff = visibleTechnique && grantedBuffSlots > 0 &&
                (GrantedPowerBuffPercent > 0 || GrantedProtectionBuffPercent > 0);
        }

        private LegacySkillFeedback(LegacySkillFeedback source, int conditionalDamagePercent)
        {
            ConditionMet = source.ConditionMet || conditionalDamagePercent > 0;
            EffectActivated = source.EffectActivated || conditionalDamagePercent > 0;
            ActGainGranted = source.ActGainGranted;
            ActStolen = source.ActStolen;
            ActReceived = source.ActReceived;
            ActRecoveryStolen = source.ActRecoveryStolen;
            ResistanceRestored = source.ResistanceRestored;
            OpponentResistanceReduced = source.OpponentResistanceReduced;
            OpponentBroken = source.OpponentBroken;
            ConditionalDamagePercent = conditionalDamagePercent;
            PowerBuffPercent = source.PowerBuffPercent;
            ProtectionBuffPercent = source.ProtectionBuffPercent;
            HasPowerBuff = source.HasPowerBuff;
            HasBeneficialBuff = source.HasBeneficialBuff;
            GrantedPowerBuffPercent = source.GrantedPowerBuffPercent;
            GrantedProtectionBuffPercent = source.GrantedProtectionBuffPercent;
            GrantedBuffSlots = source.GrantedBuffSlots;
            HasGrantedBeneficialBuff = source.HasGrantedBeneficialBuff;
            ProtectionSpendPowerBonus = source.ProtectionSpendPowerBonus;
            AheadHitPowerBonus = source.AheadHitPowerBonus;
            AheadAttackHits = source.AheadAttackHits;
        }

        private LegacySkillFeedback(LegacySkillFeedback source, int protectionSpendPowerBonus,
            int aheadHitPowerBonus, int aheadAttackHits) : this(source, source.ConditionalDamagePercent)
        {
            ProtectionSpendPowerBonus = Math.Max(0, protectionSpendPowerBonus);
            AheadHitPowerBonus = Math.Max(0, aheadHitPowerBonus);
            AheadAttackHits = Math.Max(0, aheadAttackHits);
            bool activated = ProtectionSpendPowerBonus > 0 || AheadHitPowerBonus > 0;
            ConditionMet = source.ConditionMet || activated;
            EffectActivated = source.EffectActivated || activated;
        }

        /// <summary>The actual base-power bonuses of this cast. They do not grant a percentage buff or future uses.</summary>
        internal LegacySkillFeedback WithUsePowerBonuses(int protectionSpendPowerBonus, int aheadHitPowerBonus, int aheadAttackHits)
            => new LegacySkillFeedback(this, protectionSpendPowerBonus, aheadHitPowerBonus, aheadAttackHits);

        private LegacySkillFeedback(LegacySkillFeedback source, int actRecoveryStolen, bool recoverySteal)
            : this(source, source.ConditionalDamagePercent)
        {
            ActRecoveryStolen = Math.Max(0, actRecoveryStolen);
            ConditionMet = source.ConditionMet || ActRecoveryStolen > 0;
            EffectActivated = source.EffectActivated || ActRecoveryStolen > 0;
        }

        internal LegacySkillFeedback WithActRecoverySteal(int amount)
            => amount > 0 ? new LegacySkillFeedback(this, amount, true) : this;

        /// <summary>This snapshot with the 조건 피해 배율 the slot's hits carry from its start; zero keeps it as it is.</summary>
        internal LegacySkillFeedback WithConditionalDamage(int percent)
            => percent > 0 ? new LegacySkillFeedback(this, percent) : this;

        /// <summary>The conditional rule matched; actual state changes are reported separately.</summary>
        public bool ConditionMet { get; }
        public bool EffectActivated { get; }
        /// <summary>ACT added to the player's next-turn recovery, not to their current ACT.</summary>
        public int ActGainGranted { get; }
        /// <summary>Current, unspent ACT removed from the opponent by this cast; recovery for the next turn is unchanged.</summary>
        public int ActStolen { get; }
        /// <summary>The stolen ACT actually received after this fighter's maximum, including room opened by a simultaneous steal.</summary>
        public int ActReceived { get; }
        /// <summary>Actual next-turn recovery removed from the opponent and gained by this fighter. Current ACT is unchanged.</summary>
        public int ActRecoveryStolen { get; }
        /// <summary>Actual resistance restored at slot start, after the maximum cap.</summary>
        public int ResistanceRestored { get; }
        /// <summary>Actual resistance removed directly from the opposing fighter, after the zero cap; a break counts
        /// what it took.</summary>
        public int OpponentResistanceReduced { get; }
        /// <summary>This slot's start broke the opposing fighter (상대 붕괴). False when it was already broken.</summary>
        public bool OpponentBroken { get; }
        /// <summary>The 조건 피해 배율 (a percent over 100) the slot's hits carry because the technique's 상대 상태 조건
        /// already held against the opponent once both sides' initial effects were in, and the hits will reach health:
        /// the opponent is broken or not attacking, and a guard does not absorb the hit. It then counts as the matched
        /// condition and an activated effect. Zero otherwise (a clash against whole resistance, an absorbing guard), though
        /// a hit of the slot may still multiply health damage, such as a breaking hit's overflow or a later hit that
        /// meets the condition (<see cref="LegacyHitResult.PlayerAttackConditionMet"/>). The enemy's armed strike may still
        /// be dodged afterwards.</summary>
        public int ConditionalDamagePercent { get; }
        /// <summary>Additive power bonus used to roll this slot, before its buff uses are consumed.</summary>
        public int PowerBuffPercent { get; }
        /// <summary>Net received-damage reduction used by this slot, from zero to one hundred percent.</summary>
        public int ProtectionBuffPercent { get; }
        public bool HasPowerBuff { get; }
        public bool HasBeneficialBuff { get; }
        /// <summary>Power bonus newly granted by this slot's initial effect, for subsequent slots.</summary>
        public int GrantedPowerBuffPercent { get; }
        /// <summary>Protection newly granted for subsequent slots; negative values represent vulnerability.</summary>
        public int GrantedProtectionBuffPercent { get; }
        /// <summary>Initial use count of the newly granted buff; this immutable snapshot never ticks down.</summary>
        public int GrantedBuffSlots { get; }
        public bool HasGrantedBeneficialBuff { get; }
        /// <summary>Flat base power gained by actually consuming protection, zero when none was present.</summary>
        public int ProtectionSpendPowerBonus { get; }
        /// <summary>Flat base power gained by this turn's first qualifying hit-count payoff.</summary>
        public int AheadHitPowerBonus { get; }
        /// <summary>The distinct preceding attack techniques' total hits when this skill started, including an unmet threshold.</summary>
        public int AheadAttackHits { get; }
    }
}
