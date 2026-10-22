using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The Korean words for skill enums, shared by the skill sheet and the screens. The sheet also reads each
    /// enum's English name (any case) so a designer may type either.</summary>
    public static class LegacySkillLabels
    {
        /// <summary>예약 효과 phrases (<see cref="LegacySkillEffect.ReserveCycleFeed"/>, <see cref="LegacySkillEffect.ReserveBreath"/>).</summary>
        public const string CycleFeedPhrase = "순환 +1", AutoBreathPhrase = "숨고르기 +1";
        /// <summary>사용 효과 phrase for <see cref="LegacySkillEffect.AutoDodge"/>.</summary>
        public const string AutoDodgePhrase = "자동 회피";
        /// <summary>ACT 강탈 tests the opponent's remaining ACT when used and transfers current ACT, not next-turn recovery.</summary>
        public const string ActStealKeyword = "ACT 강탈";
        public const string ActRecoveryStealKeyword = "ACT 회복 강탈";
        public const int MinimumActStealValue = 1, MaximumActStealValue = 10;
        /// <summary>Bounds for 보호 소모 위력 +N and 앞 공격 총타수 M 이상 위력 +N.</summary>
        public const int MinimumUsePowerBonus = 1, MaximumUsePowerBonus = 50;
        public const int MinimumAttackHitsRequired = 1, MaximumAttackHitsRequired = 20;
        public const int MinimumBleedingCount = 1, MaximumBleedingCount = 20;
        private static readonly Regex BleedingGrantPattern = new Regex(
            @"^(자신에게|상대에게)출혈([0-9]{1,3})부여$", RegexOptions.CultureInvariant);
        private static readonly Regex BleedingReplacementPattern = new Regex(
            @"^상대가출혈상태면출혈([0-9]{1,3})로대체$", RegexOptions.CultureInvariant);
        private static readonly Regex BleedingReductionPattern = new Regex(
            @"^자신의출혈([0-9]{1,3})감소$", RegexOptions.CultureInvariant);
        private static readonly Regex BleedingPowerPattern = new Regex(
            @"^(상대|자기)출혈([0-9]{1,3})이상기본위력\+([0-9]{1,3})$", RegexOptions.CultureInvariant);
        /// <summary>The range of n in 사용 효과 <c>앞 &lt;대상&gt;마다 위력 +n (최대 m)</c>.</summary>
        public const int MinimumAheadPowerPerUnit = 1, MaximumAheadPowerPerUnit = 10;
        /// <summary>The range of m in the same phrase.</summary>
        public const int MinimumAheadCount = 1, MaximumAheadCount = 4;
        /// <summary>예약 효과 <c>일회성 &lt;ID&gt; ×n</c>'s first word (<see cref="LegacySkillEffect.ReserveTemporarySkillId"/>) and the
        /// range of n.</summary>
        public const string TemporaryPhrase = "일회성";
        public const int MinimumTemporaryCount = 1, MaximumTemporaryCount = 3;
        /// <summary>예약 효과 <c>제한시간 -n초</c>'s first word (<see cref="LegacySkillEffect.ReservePlanningSecondsCost"/>) and the
        /// range of n.</summary>
        public const string PlanningTimePhrase = "제한시간";
        public const int MinimumReserveSecondsCost = 1, MaximumReserveSecondsCost = 5;
        /// <summary>The ranges of n and m in 사용 효과 <c>앞 &lt;대상&gt;마다 다음 턴 제한시간 +n초 (최대 m)</c>.</summary>
        public const int MinimumAheadSeconds = 1, MaximumAheadSeconds = 3;
        public const int MinimumAheadTimeCount = 1, MaximumAheadTimeCount = 3;
        private const string BrokenState = "붕괴", HealthState = "체력", AtMost = "이하";
        // 앞 <대상>마다 위력 +n (최대 m), spaces optional between the parts (inside the target's name too); ASCII digits only.
        private static readonly Regex AheadPowerPattern = new Regex(
            @"^앞\s*(숨고르기|순환\s*기술|저항\s*감소\s*기술)\s*마다\s*위력\s*\+\s*([0-9]{1,3})\s*\(\s*최대\s*([0-9]{1,3})\s*\)$",
            RegexOptions.CultureInvariant);
        private static readonly Regex ProtectionSpendPattern = new Regex(
            @"^보호\s*소모\s*위력\s*\+\s*([0-9]{1,3})$", RegexOptions.CultureInvariant);
        private static readonly Regex AheadAttackHitsPattern = new Regex(
            @"^앞\s*공격\s*총\s*타수\s*([0-9]{1,3})\s*이상\s*위력\s*\+\s*([0-9]{1,3})$", RegexOptions.CultureInvariant);
        private static readonly Regex ActStealPattern = new Regex(
            @"^상대\s*ACT\s*([0-9]{1,3})\s*이상이면\s*ACT\s*([0-9]{1,3})\s*강탈$", RegexOptions.CultureInvariant);
        private static readonly Regex ActRecoveryStealPattern = new Regex(
            @"^상대\s*공격\s*시\s*다음\s*턴\s*ACT\s*회복\s*([0-9]{1,3})\s*강탈$", RegexOptions.CultureInvariant);
        // 앞 <대상>마다 다음 턴 제한시간 +n초 (최대 m), spaces optional between the parts.
        private static readonly Regex AheadTimePattern = new Regex(
            @"^앞\s*(숨고르기|순환\s*기술|저항\s*감소\s*기술)\s*마다\s*다음\s*턴\s*제한\s*시간\s*\+\s*([0-9]{1,3})\s*초\s*\(\s*최대\s*([0-9]{1,3})\s*\)$",
            RegexOptions.CultureInvariant);
        // 일회성 <ID> ×n: × (U+00D7), or x / X / * as typed on a keyboard.
        private static readonly Regex TemporaryPattern = new Regex(
            @"^일회성\s*([0-9]{1,4})\s*[×xX*]\s*([0-9]{1,3})$", RegexOptions.CultureInvariant);
        // 제한시간 -n초 (a cost) and 제한시간 +n초 (a gain at reservation, which the sheet refuses with its own message).
        private static readonly Regex ReserveTimePattern = new Regex(
            @"^제한\s*시간\s*([-+−])\s*([0-9]{1,3})\s*초$", RegexOptions.CultureInvariant);
        private static readonly LegacyAheadTarget[] aheadTargets =
            { LegacyAheadTarget.Breath, LegacyAheadTarget.CycleSkill, LegacyAheadTarget.ResistanceReduction };
        // 체력 N% 이하, spaces optional between the parts; ASCII digits only (a leading zero still reads).
        private static readonly Regex HealthPattern = new Regex(@"^체력\s*([0-9]{1,3})\s*%\s*이하$", RegexOptions.CultureInvariant);
        private static readonly LegacySkillProperty[] properties =
            { LegacySkillProperty.Slash, LegacySkillProperty.Hit, LegacySkillProperty.Penetrate, LegacySkillProperty.Defence, LegacySkillProperty.None };
        private static readonly LegacySkillKind[] kinds = { LegacySkillKind.Attack, LegacySkillKind.Defence, LegacySkillKind.Wait };
        private static readonly LegacySkillSymbol[] symbols =
        {
            LegacySkillSymbol.Act, LegacySkillSymbol.Sword, LegacySkillSymbol.Guard, LegacySkillSymbol.Hits,
            LegacySkillSymbol.Recovery, LegacySkillSymbol.Followup, LegacySkillSymbol.Reduction, LegacySkillSymbol.Variance,
            LegacySkillSymbol.Cycle, LegacySkillSymbol.OneShot, LegacySkillSymbol.Time,
        };
        private static readonly LegacySkillTone[] tones =
        {
            LegacySkillTone.Neutral, LegacySkillTone.Recovery, LegacySkillTone.Followup, LegacySkillTone.Reduction,
            LegacySkillTone.HighPower, LegacySkillTone.MultiHit, LegacySkillTone.Variance, LegacySkillTone.Defence,
            LegacySkillTone.Cycle,
        };

        public static string Property(LegacySkillProperty property)
        {
            switch (property)
            {
                case LegacySkillProperty.Slash: return "참격";
                case LegacySkillProperty.Hit: return "타격";
                case LegacySkillProperty.Penetrate: return "관통";
                case LegacySkillProperty.Defence: return "방어";
                default: return "없음";
            }
        }

        public static string Kind(LegacySkillKind kind)
        {
            switch (kind)
            {
                case LegacySkillKind.Attack: return "공격";
                case LegacySkillKind.Defence: return "방어";
                default: return "대기";
            }
        }

        public static string Symbol(LegacySkillSymbol symbol)
        {
            switch (symbol)
            {
                case LegacySkillSymbol.Act: return "ACT";
                case LegacySkillSymbol.Sword: return "검";
                case LegacySkillSymbol.Guard: return "방어";
                case LegacySkillSymbol.Hits: return "타수";
                case LegacySkillSymbol.Recovery: return "회복";
                case LegacySkillSymbol.Followup: return "후속";
                case LegacySkillSymbol.Reduction: return "감소";
                case LegacySkillSymbol.Cycle: return "순환";
                case LegacySkillSymbol.OneShot: return "일회";
                case LegacySkillSymbol.Time: return "시간";
                default: return "편차";
            }
        }

        public static string Tone(LegacySkillTone tone)
        {
            switch (tone)
            {
                case LegacySkillTone.Neutral: return "기본";
                case LegacySkillTone.Recovery: return "회복";
                case LegacySkillTone.Followup: return "후속";
                case LegacySkillTone.Reduction: return "감소";
                case LegacySkillTone.HighPower: return "고화력";
                case LegacySkillTone.MultiHit: return "연타";
                case LegacySkillTone.Variance: return "편차";
                case LegacySkillTone.Cycle: return "순환";
                default: return "방어";
            }
        }

        /// <summary>A 상대 상태 조건 as the sheet writes it: <c>붕괴</c>, <c>체력 30% 이하</c>, or empty for none.</summary>
        public static string OpponentState(LegacyOpponentState state, int healthPercent)
        {
            switch (state)
            {
                case LegacyOpponentState.Broken: return BrokenState;
                case LegacyOpponentState.HealthAtMost:
                    return HealthState + " " + healthPercent.ToString(CultureInfo.InvariantCulture) + "% " + AtMost;
                default: return string.Empty;
            }
        }

        /// <summary>Reads <c>붕괴</c> or <c>체력 N% 이하</c> with N from 1 to 99; spaces between the parts are optional
        /// (<c>체력30%이하</c>, <c>체력 30 % 이하</c>). Blank and anything else read false.</summary>
        public static bool TryParseOpponentState(string text, out LegacyOpponentState state, out int healthPercent)
        {
            state = LegacyOpponentState.None;
            healthPercent = 0;
            string trimmed = text?.Trim() ?? string.Empty;
            if (trimmed == BrokenState)
            {
                state = LegacyOpponentState.Broken;
                return true;
            }
            Match match = HealthPattern.Match(trimmed);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int percent) ||
                percent < 1 || percent > 99)
                return false;
            state = LegacyOpponentState.HealthAtMost;
            healthPercent = percent;
            return true;
        }

        /// <summary>What a 사용 효과 counts, as its phrase names it: 숨고르기, 순환 기술, 저항 감소 기술; empty for none.</summary>
        public static string AheadTarget(LegacyAheadTarget target)
        {
            switch (target)
            {
                case LegacyAheadTarget.Breath: return "숨고르기";
                case LegacyAheadTarget.CycleSkill: return "순환 기술";
                case LegacyAheadTarget.ResistanceReduction: return "저항 감소 기술";
                default: return string.Empty;
            }
        }

        /// <summary>A row's 예약 효과 as the sheet writes it (<see cref="CycleFeedPhrase"/>, <see cref="AutoBreathPhrase"/>,
        /// <c>일회성 59 ×3</c>, <c>제한시간 -4초</c>), or empty for none.</summary>
        public static string ReserveEffect(LegacySkillEffect effect)
        {
            if (effect == null) return string.Empty;
            if (effect.ReserveCycleFeed) return CycleFeedPhrase;
            if (effect.ReserveBreath) return AutoBreathPhrase;
            if (effect.HasTemporarySpawn)
                return TemporaryPhrase + " " + effect.ReserveTemporarySkillId.ToString(CultureInfo.InvariantCulture) + " ×" +
                    effect.ReserveTemporaryCount.ToString(CultureInfo.InvariantCulture);
            if (effect.ReservePlanningSecondsCost > 0)
                return PlanningTimePhrase + " -" + effect.ReservePlanningSecondsCost.ToString(CultureInfo.InvariantCulture) + "초";
            return string.Empty;
        }

        /// <summary>A row's 사용 효과 as the sheet writes it: <c>앞 숨고르기마다 위력 +4 (최대 4)</c>,
        /// <c>앞 순환 기술마다 다음 턴 제한시간 +2초 (최대 3)</c>, <see cref="AutoDodgePhrase"/>,
        /// <c>상대 ACT 8 이상이면 ACT 3 강탈</c>, or empty for none. Multiple effects are separated by semicolons.</summary>
        public static string UseEffect(LegacySkillEffect effect)
        {
            if (effect == null) return string.Empty;
            var phrases = new List<string>();
            if (effect.HasAheadPower)
                phrases.Add("앞 " + AheadTarget(effect.AheadPowerTarget) + "마다 위력 +" +
                    effect.AheadPowerPerUnit.ToString(CultureInfo.InvariantCulture) + " (최대 " +
                    effect.AheadPowerMax.ToString(CultureInfo.InvariantCulture) + ")");
            if (effect.HasAheadTime)
                phrases.Add("앞 " + AheadTarget(effect.AheadTimeTarget) + "마다 다음 턴 " + PlanningTimePhrase + " +" +
                    effect.AheadTimePerUnit.ToString(CultureInfo.InvariantCulture) + "초 (최대 " +
                    effect.AheadTimeMax.ToString(CultureInfo.InvariantCulture) + ")");
            if (effect.AutoDodge) phrases.Add(AutoDodgePhrase);
            if (effect.HasProtectionSpend)
                phrases.Add("보호 소모 위력 +" + effect.ConsumeProtectionPowerBonus.ToString(CultureInfo.InvariantCulture));
            if (effect.HasAheadHitPower)
                phrases.Add("앞 공격 총타수 " + effect.AheadAttackHitsRequired.ToString(CultureInfo.InvariantCulture) +
                    " 이상 위력 +" + effect.AheadAttackHitsPowerBonus.ToString(CultureInfo.InvariantCulture));
            if (effect.HasActSteal)
                phrases.Add("상대 ACT " + effect.ActStealThreshold.ToString(CultureInfo.InvariantCulture) +
                    " 이상이면 ACT " + effect.ActStealAmount.ToString(CultureInfo.InvariantCulture) + " 강탈");
            if (effect.HasActRecoverySteal)
                phrases.Add("상대 공격 시 다음 턴 ACT 회복 " + effect.ActRecoveryStealAmount.ToString(CultureInfo.InvariantCulture) + " 강탈");
            if (effect.SelfBleedingCount > 0)
                phrases.Add("자신에게 출혈 " + effect.SelfBleedingCount.ToString(CultureInfo.InvariantCulture) + " 부여");
            if (effect.OpponentBleedingCount > 0)
                phrases.Add("상대에게 출혈 " + effect.OpponentBleedingCount.ToString(CultureInfo.InvariantCulture) + " 부여");
            if (effect.OpponentBleedingReplacementCount > 0)
                phrases.Add("상대가 출혈 상태면 출혈 " + effect.OpponentBleedingReplacementCount.ToString(CultureInfo.InvariantCulture) + "로 대체");
            if (effect.HasBleedingReduction)
                phrases.Add("자신의 출혈 " + effect.BleedingReduction.ToString(CultureInfo.InvariantCulture) + " 감소");
            if (effect.OpponentBleedingRequired > 0)
                phrases.Add("상대 출혈 " + effect.OpponentBleedingRequired.ToString(CultureInfo.InvariantCulture) +
                    " 이상 기본 위력 +" + effect.OpponentBleedingPowerBonus.ToString(CultureInfo.InvariantCulture));
            if (effect.SelfBleedingRequired > 0)
                phrases.Add("자기 출혈 " + effect.SelfBleedingRequired.ToString(CultureInfo.InvariantCulture) +
                    " 이상 기본 위력 +" + effect.SelfBleedingPowerBonus.ToString(CultureInfo.InvariantCulture));
            return string.Join("; ", phrases);
        }

        /// <summary>Reads one 예약 효과 phrase into <paramref name="effect"/>: <see cref="CycleFeedPhrase"/>,
        /// <see cref="AutoBreathPhrase"/>, <c>일회성 &lt;ID&gt; ×n</c> (n 1-3; x or * for ×) or <c>제한시간 -n초</c> (n 1-5); spaces
        /// are optional (<c>순환+1</c>). Whether the id names a 일회성 row is the sheet's check once every row is read. False, with
        /// nothing set, for blank or anything else.</summary>
        internal static bool TryReadReserveEffect(string text, LegacySkillEffect effect)
        {
            string trimmed = text?.Trim() ?? string.Empty;
            string compact = Compact(trimmed);
            if (compact == Compact(CycleFeedPhrase))
            {
                effect.ReserveCycleFeed = true;
                return true;
            }
            if (compact == Compact(AutoBreathPhrase))
            {
                effect.ReserveBreath = true;
                return true;
            }
            Match temporary = TemporaryPattern.Match(trimmed);
            if (temporary.Success)
            {
                if (!int.TryParse(temporary.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
                    !int.TryParse(temporary.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
                    id < 1 || count < MinimumTemporaryCount || count > MaximumTemporaryCount)
                    return false;
                effect.ReserveTemporarySkillId = id;
                effect.ReserveTemporaryCount = count;
                return true;
            }
            Match time = ReserveTimePattern.Match(trimmed);
            if (!time.Success || time.Groups[1].Value == "+" ||
                !int.TryParse(time.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) ||
                seconds < MinimumReserveSecondsCost || seconds > MaximumReserveSecondsCost)
                return false;
            effect.ReservePlanningSecondsCost = seconds;
            return true;
        }

        /// <summary>Whether <paramref name="text"/> reads as <c>제한시간 +n초</c>: time gained as a skill is reserved, which no row
        /// may write (a remover's 반환 and 넘기기 would repeat it).</summary>
        internal static bool IsReserveTimeGain(string text)
        {
            Match time = ReserveTimePattern.Match(text?.Trim() ?? string.Empty);
            return time.Success && time.Groups[1].Value == "+";
        }

        /// <summary>Reads semicolon-separated 사용 효과 phrases into <paramref name="effect"/>: <c>앞 &lt;대상&gt;마다 위력 +n (최대 m)</c> (n 1-10,
        /// m 1-4), <c>앞 &lt;대상&gt;마다 다음 턴 제한시간 +n초 (최대 m)</c> (n 1-3, m 1-3), <see cref="AutoDodgePhrase"/>,
        /// or <c>상대 ACT n 이상이면 ACT m 강탈</c> (n and m 1-10); spaces
        /// between the parts are optional. Rejects duplicate categories atomically; false, with nothing set, for blank or invalid phrases.</summary>
        internal static bool TryReadUseEffect(string text, LegacySkillEffect effect)
        {
            var parsed = new LegacySkillEffect();
            foreach (string phrase in (text ?? string.Empty).Split(';'))
                if (!TryReadSingleUseEffect(phrase, parsed)) return false;
            effect.AheadPowerTarget = parsed.AheadPowerTarget;
            effect.AheadPowerPerUnit = parsed.AheadPowerPerUnit;
            effect.AheadPowerMax = parsed.AheadPowerMax;
            effect.AheadTimeTarget = parsed.AheadTimeTarget;
            effect.AheadTimePerUnit = parsed.AheadTimePerUnit;
            effect.AheadTimeMax = parsed.AheadTimeMax;
            effect.AutoDodge = parsed.AutoDodge;
            effect.ConsumeProtectionPowerBonus = parsed.ConsumeProtectionPowerBonus;
            effect.AheadAttackHitsRequired = parsed.AheadAttackHitsRequired;
            effect.AheadAttackHitsPowerBonus = parsed.AheadAttackHitsPowerBonus;
            effect.ActStealThreshold = parsed.ActStealThreshold;
            effect.ActStealAmount = parsed.ActStealAmount;
            effect.ActRecoveryStealAmount = parsed.ActRecoveryStealAmount;
            effect.SelfBleedingCount = parsed.SelfBleedingCount;
            effect.OpponentBleedingCount = parsed.OpponentBleedingCount;
            effect.OpponentBleedingReplacementCount = parsed.OpponentBleedingReplacementCount;
            effect.BleedingReduction = parsed.BleedingReduction;
            effect.OpponentBleedingRequired = parsed.OpponentBleedingRequired;
            effect.OpponentBleedingPowerBonus = parsed.OpponentBleedingPowerBonus;
            effect.SelfBleedingRequired = parsed.SelfBleedingRequired;
            effect.SelfBleedingPowerBonus = parsed.SelfBleedingPowerBonus;
            return true;
        }

        private static bool TryReadSingleUseEffect(string text, LegacySkillEffect effect)
        {
            string trimmed = text?.Trim() ?? string.Empty;
            if (TryReadBleedingEffect(trimmed, effect)) return true;
            if (Compact(trimmed) == Compact(AutoDodgePhrase))
            {
                if (effect.AutoDodge) return false;
                effect.AutoDodge = true;
                return true;
            }
            Match recoverySteal = ActRecoveryStealPattern.Match(trimmed);
            if (recoverySteal.Success)
            {
                int amount = int.Parse(recoverySteal.Groups[1].Value, CultureInfo.InvariantCulture);
                if (effect.HasActRecoverySteal || amount < MinimumActStealValue || amount > MaximumActStealValue) return false;
                effect.ActRecoveryStealAmount = amount;
                return true;
            }
            Match actSteal = ActStealPattern.Match(trimmed);
            if (actSteal.Success)
            {
                int threshold = int.Parse(actSteal.Groups[1].Value, CultureInfo.InvariantCulture);
                int amount = int.Parse(actSteal.Groups[2].Value, CultureInfo.InvariantCulture);
                if (effect.HasActSteal || threshold < MinimumActStealValue || threshold > MaximumActStealValue ||
                    amount < MinimumActStealValue || amount > MaximumActStealValue) return false;
                effect.ActStealThreshold = threshold;
                effect.ActStealAmount = amount;
                return true;
            }
            Match protectionSpend = ProtectionSpendPattern.Match(trimmed);
            if (protectionSpend.Success)
            {
                int bonus = int.Parse(protectionSpend.Groups[1].Value, CultureInfo.InvariantCulture);
                if (effect.HasProtectionSpend || bonus < MinimumUsePowerBonus || bonus > MaximumUsePowerBonus) return false;
                effect.ConsumeProtectionPowerBonus = bonus;
                return true;
            }
            Match attackHits = AheadAttackHitsPattern.Match(trimmed);
            if (attackHits.Success)
            {
                int hits = int.Parse(attackHits.Groups[1].Value, CultureInfo.InvariantCulture);
                int bonus = int.Parse(attackHits.Groups[2].Value, CultureInfo.InvariantCulture);
                if (effect.HasAheadHitPower || hits < MinimumAttackHitsRequired || hits > MaximumAttackHitsRequired ||
                    bonus < MinimumUsePowerBonus || bonus > MaximumUsePowerBonus) return false;
                effect.AheadAttackHitsRequired = hits;
                effect.AheadAttackHitsPowerBonus = bonus;
                return true;
            }
            if (TryReadAhead(AheadPowerPattern, trimmed, MinimumAheadPowerPerUnit, MaximumAheadPowerPerUnit,
                MinimumAheadCount, MaximumAheadCount, out LegacyAheadTarget target, out int perUnit, out int max))
            {
                if (effect.HasAheadPower) return false;
                effect.AheadPowerTarget = target;
                effect.AheadPowerPerUnit = perUnit;
                effect.AheadPowerMax = max;
                return true;
            }
            if (TryReadAhead(AheadTimePattern, trimmed, MinimumAheadSeconds, MaximumAheadSeconds,
                MinimumAheadTimeCount, MaximumAheadTimeCount, out target, out perUnit, out max))
            {
                if (effect.HasAheadTime) return false;
                effect.AheadTimeTarget = target;
                effect.AheadTimePerUnit = perUnit;
                effect.AheadTimeMax = max;
                return true;
            }
            return false;
        }

        private static bool TryReadBleedingEffect(string text, LegacySkillEffect effect)
        {
            string compact = Compact(text);
            Match grant = BleedingGrantPattern.Match(compact);
            if (grant.Success)
            {
                int count = int.Parse(grant.Groups[2].Value, CultureInfo.InvariantCulture);
                if (!ValidBleedingCount(count)) return false;
                if (grant.Groups[1].Value == "자신에게")
                {
                    if (effect.SelfBleedingCount > 0) return false;
                    effect.SelfBleedingCount = count;
                }
                else
                {
                    if (effect.OpponentBleedingCount > 0) return false;
                    effect.OpponentBleedingCount = count;
                }
                return true;
            }
            Match replacement = BleedingReplacementPattern.Match(compact);
            if (replacement.Success)
            {
                int count = int.Parse(replacement.Groups[1].Value, CultureInfo.InvariantCulture);
                if (effect.OpponentBleedingReplacementCount > 0 || !ValidBleedingCount(count)) return false;
                effect.OpponentBleedingReplacementCount = count;
                return true;
            }
            Match reduction = BleedingReductionPattern.Match(compact);
            if (reduction.Success)
            {
                int count = int.Parse(reduction.Groups[1].Value, CultureInfo.InvariantCulture);
                if (effect.HasBleedingReduction || !ValidBleedingCount(count)) return false;
                effect.BleedingReduction = count;
                return true;
            }
            Match power = BleedingPowerPattern.Match(compact);
            if (!power.Success) return false;
            int required = int.Parse(power.Groups[2].Value, CultureInfo.InvariantCulture);
            int bonus = int.Parse(power.Groups[3].Value, CultureInfo.InvariantCulture);
            if (!ValidBleedingCount(required) || bonus < MinimumUsePowerBonus || bonus > MaximumUsePowerBonus) return false;
            if (power.Groups[1].Value == "상대")
            {
                if (effect.OpponentBleedingRequired > 0) return false;
                effect.OpponentBleedingRequired = required;
                effect.OpponentBleedingPowerBonus = bonus;
            }
            else
            {
                if (effect.SelfBleedingRequired > 0) return false;
                effect.SelfBleedingRequired = required;
                effect.SelfBleedingPowerBonus = bonus;
            }
            return true;
        }

        private static bool ValidBleedingCount(int count) => count >= MinimumBleedingCount && count <= MaximumBleedingCount;

        private static bool TryReadAhead(Regex pattern, string text, int minimumPerUnit, int maximumPerUnit, int minimumCount,
            int maximumCount, out LegacyAheadTarget target, out int perUnit, out int max)
        {
            target = LegacyAheadTarget.None;
            perUnit = max = 0;
            Match match = pattern.Match(text);
            if (!match.Success ||
                !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out perUnit) ||
                !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out max) ||
                perUnit < minimumPerUnit || perUnit > maximumPerUnit || max < minimumCount || max > maximumCount)
                return false;
            string name = Compact(match.Groups[1].Value);
            foreach (LegacyAheadTarget candidate in aheadTargets)
                if (Compact(AheadTarget(candidate)) == name)
                {
                    target = candidate;
                    return true;
                }
            return false;
        }

        private static string Compact(string text) => Regex.Replace(text ?? string.Empty, @"\s+", string.Empty);

        /// <summary>Reads a label or an English enum name. <see cref="LegacySkillProperty.None"/> reads too; the sheet
        /// rejects it for techniques.</summary>
        public static bool TryParseProperty(string text, out LegacySkillProperty property)
            => TryParse(text, properties, Property, out property);

        /// <summary>Reads a label or an English enum name, <see cref="LegacySkillKind.Wait"/> included.</summary>
        public static bool TryParseKind(string text, out LegacySkillKind kind) => TryParse(text, kinds, Kind, out kind);

        public static bool TryParseSymbol(string text, out LegacySkillSymbol symbol) => TryParse(text, symbols, Symbol, out symbol);

        public static bool TryParseTone(string text, out LegacySkillTone tone) => TryParse(text, tones, Tone, out tone);

        // Labels compare exactly and English names ignore case. Enum.TryParse would also take numbers, which a sheet
        // cell must not mean.
        private static bool TryParse<T>(string text, T[] values, Func<T, string> label, out T value) where T : struct
        {
            string trimmed = text?.Trim() ?? string.Empty;
            foreach (T candidate in values)
                if (trimmed == label(candidate) || string.Equals(trimmed, candidate.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    value = candidate;
                    return true;
                }
            value = default;
            return false;
        }
    }
}
