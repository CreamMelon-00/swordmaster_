using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Rules shared by combat resolution and prospective opponent highlighting.</summary>
    public static class LegacySkillConditions
    {
        public static bool HasOpponentCondition(LegacySkill skill)
            => skill != null && !skill.IsWait && (skill.Id == 7 || skill.Id == 42);

        public static bool MatchesOpponent(LegacySkill skill, LegacySkill opposingSkill)
        {
            if (!HasOpponentCondition(skill) || opposingSkill == null) return false;
            return skill.Id == 7 ? opposingSkill.Property == LegacySkillProperty.Hit
                : opposingSkill.Kind == LegacySkillKind.Defence;
        }

        public static bool HasSelfCondition(LegacySkill skill)
            => skill != null && !skill.IsWait && skill.Id == 19;

        public static bool MatchesSelfCondition(LegacySkill skill, LegacyFighterState fighter)
            => HasSelfCondition(skill) && fighter != null && fighter.Resistance < fighter.MaxResistance
                && ResistanceRecoveryAmount(fighter) > 0;

        internal static int ResistanceRecoveryAmount(LegacyFighterState fighter)
            => (int)Math.Round(fighter.MaxResistance * .1d, MidpointRounding.ToEven);
    }

    /// <summary>Immutable feedback for the actual effect and buff snapshot of an initialized combat slot.</summary>
    public sealed class LegacySkillFeedback
    {
        internal static LegacySkillFeedback None { get; } = new LegacySkillFeedback(null, false, false, 0, 0);

        internal LegacySkillFeedback(LegacySkill skill, bool conditionMet, bool effectActivated,
            int powerBuffPercent, int protectionBuffPercent, int grantedPowerBuffPercent = 0,
            int grantedProtectionBuffPercent = 0, int grantedBuffSlots = 0)
        {
            ConditionMet = conditionMet;
            EffectActivated = effectActivated;
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

        /// <summary>The conditional rule matched; actual state changes are reported separately.</summary>
        public bool ConditionMet { get; }
        public bool EffectActivated { get; }
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
    }
}
