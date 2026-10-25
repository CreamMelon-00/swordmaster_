using System;
using System.Globalization;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Shared names for the sustained effects shown in skill details and fighter status icons.</summary>
    public static class LegacyStatusNames
    {
        public static string Name(LegacyStatusKind kind)
        {
            switch (kind)
            {
                case LegacyStatusKind.PowerUp: return "강화";
                case LegacyStatusKind.Protection: return "보호";
                case LegacyStatusKind.Vulnerability: return "취약";
                case LegacyStatusKind.Bleeding: return "출혈";
                case LegacyStatusKind.Jealousy: return "질투";
                case LegacyStatusKind.Fixed: return "고정";
                case LegacyStatusKind.Tension: return "긴장";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>The final number is the remaining uses. Standard 50% vulnerability and fixed-damage bleeding omit strength.</summary>
        public static string Label(LegacyStatusKind kind, int percent, int count)
        {
            string strength = kind == LegacyStatusKind.Bleeding || kind == LegacyStatusKind.Jealousy ||
                kind == LegacyStatusKind.Fixed || kind == LegacyStatusKind.Tension || kind == LegacyStatusKind.Vulnerability && percent == 50
                ? string.Empty : " " + percent.ToString(CultureInfo.InvariantCulture) + "%";
            return Name(kind) + strength + " " + count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Zero percent explains the notation generically; status icons pass their actual percentage.</summary>
        public static string Description(LegacyStatusKind kind, int percent)
        {
            string strength = percent > 0 ? percent.ToString(CultureInfo.InvariantCulture) + "%" : "표시된 비율만큼";
            switch (kind)
            {
                case LegacyStatusKind.PowerUp:
                    return "표시된 횟수의 기술을 사용할 때 공격·방어 위력이 " + strength +
                        " 증가합니다. 숨고르기·빈 칸은 세지 않으며 턴이 지나도 남습니다.";
                case LegacyStatusKind.Protection:
                    return "보호 비율: " + (percent > 0 ? strength : "표시된 비율") +
                        ". 표시된 횟수의 공격 기술에 적용합니다. 취약과 서로 상쇄되며, " +
                        "최종 피해 감소율은 최대 100%입니다. 연타는 1회로 세며, 회피하면 소모하지 않고 턴이 지나도 남습니다.";
                case LegacyStatusKind.Vulnerability:
                    return "표시된 횟수의 공격 기술을 받을 때 피해가 " + strength +
                        " 증가합니다. 보호가 있으면 두 비율이 서로 상쇄됩니다. " +
                        "연타는 1회로 세며, 회피하면 소모하지 않고 턴이 지나도 남습니다.";
                case LegacyStatusKind.Bleeding:
                    return "자신의 공격 기술에서 타격을 실행한 뒤마다 고정 체력 피해 " +
                        LegacyStatusCollection.BleedingDamagePerHit.ToString(CultureInfo.InvariantCulture) +
                        "를 받습니다. 명중하지 않아도 발동하며 방어·보호·취약·붕괴의 영향을 받지 않습니다. " +
                        "공격 기술 하나가 끝나면 남은 횟수가 1 줄어듭니다. 방어·숨고르기는 소모하지 않습니다. " +
                        "새로 부여된 출혈은 이번 양측 공격에 적용하지 않습니다. 중복 부여는 횟수만 더하며 턴이 지나도 남습니다.";
                case LegacyStatusKind.Jealousy:
                    return "이아가 공격·방어하지 않는 칸에서 피해를 받으면 타격마다 1씩 쌓입니다. 그런 피해가 없는 턴 뒤에는 사라집니다. " +
                        "5 이상이면 다음 준비 시작에 모두 소모하고 ‘나도 할 수 있어…’를 대기열 맨 뒤에 예약합니다. " +
                        "이 기술에 맞으면 고정 1로 다음 준비의 넘기기를 막습니다.";
                case LegacyStatusKind.Fixed:
                    return "남은 준비 단계 동안 넘기기를 사용할 수 없습니다. 기술 예약과 숨고르기는 사용할 수 있습니다. " +
                        "전투 중 부여받으면 다음 준비 단계부터 적용합니다. 같은 턴의 연속 적중은 기간을 더하지 않습니다.";
                case LegacyStatusKind.Tension:
                    return "다음 실제 공격 기술에서 원래 2타 이상인 공격의 실행 타수가 1 줄어듭니다. 타격당 위력은 유지합니다. " +
                        "공격 기술 하나가 끝나면 남은 횟수가 1 줄어들며, 단타와 반격도 1회로 셉니다. " +
                        "방어·숨고르기·빈 칸·취소된 기술은 소모하지 않습니다. " +
                        "새로 부여된 긴장은 이미 시작한 공격에 적용하지 않습니다. 중복 부여는 횟수만 더하며 턴이 지나도 남습니다.";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
