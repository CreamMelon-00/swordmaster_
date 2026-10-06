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
            bool opponentBroken = false)
        {
            ConditionMet = conditionMet;
            EffectActivated = effectActivated;
            ActGainGranted = actGainGranted;
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

        /// <summary>The conditional rule matched; actual state changes are reported separately.</summary>
        public bool ConditionMet { get; }
        public bool EffectActivated { get; }
        /// <summary>ACT added to the player's next-turn recovery, not to their current ACT.</summary>
        public int ActGainGranted { get; }
        /// <summary>Actual resistance restored at slot start, after the maximum cap.</summary>
        public int ResistanceRestored { get; }
        /// <summary>Actual resistance removed directly from the opposing fighter, after the zero cap; a break counts
        /// what it took.</summary>
        public int OpponentResistanceReduced { get; }
        /// <summary>This slot's start broke the opposing fighter (상대 붕괴). False when it was already broken.</summary>
        public bool OpponentBroken { get; }
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
