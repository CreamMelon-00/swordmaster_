using System;
using System.Globalization;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Actual modifiers on the damage a fighter received. Presentation only: no combat or network state.</summary>
    [Flags]
    public enum LegacyDamageReason
    {
        None = 0,
        BrokenDouble = 1 << 0,
        ConditionalMultiplier = 1 << 1,
        BrokenBonus = 1 << 2,
        GuardReduction = 1 << 3,
        ProtectionReduction = 1 << 4,
        PressureBonus = 1 << 5,
        PressureGuard = 1 << 6
    }

    public static class LegacyDamageReasonText
    {
        /// <summary>One cause beside the number. Definitions describe a recorded cause; they never infer activation.</summary>
        public static string Primary(LegacyDamageReason reasons, LegacySkill attacker = null)
        {
            if ((reasons & LegacyDamageReason.PressureBonus) != 0) return "압박 추가 피해";
            if ((reasons & LegacyDamageReason.ConditionalMultiplier) != 0)
            {
                LegacySkillEffect effect = LegacySkillDefinitions.Find(attacker)?.Effect;
                if (effect != null && effect.ConditionalDamagePercent > 0)
                {
                    string multiplier = (effect.ConditionalDamagePercent / 100d).ToString("0.##", CultureInfo.InvariantCulture);
                    if (effect.OpponentState == LegacyOpponentState.Broken) return "붕괴 조건 ×" + multiplier;
                    if (effect.OpponentState == LegacyOpponentState.HealthAtMost)
                        return "체력 " + effect.OpponentHealthPercent + "% 이하 ×" + multiplier;
                    return "조건 피해 ×" + multiplier;
                }
                return "조건 강화";
            }
            if ((reasons & LegacyDamageReason.BrokenBonus) != 0)
            {
                int bonus = LegacySkillDefinitions.Find(attacker)?.Effect.BrokenTargetDamagePercent ?? 0;
                return bonus > 0 ? "붕괴 추가 +" + bonus + "%" : "붕괴 추가 피해";
            }
            if ((reasons & LegacyDamageReason.BrokenDouble) != 0) return "붕괴 ×2";
            if ((reasons & LegacyDamageReason.PressureGuard) != 0) return "방어 압박";
            if ((reasons & LegacyDamageReason.GuardReduction) != 0) return "방어 감소";
            if ((reasons & LegacyDamageReason.ProtectionReduction) != 0) return "보호 감소";
            return string.Empty;
        }
    }
}
