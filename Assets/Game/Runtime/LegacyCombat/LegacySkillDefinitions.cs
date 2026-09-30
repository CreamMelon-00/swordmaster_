using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Semantic pictogram for a skill keyword; presentation draws it.</summary>
    public enum LegacySkillSymbol { Act, Sword, Guard, Hits, Recovery, Followup, Reduction, Variance }

    /// <summary>Semantic tone for a skill keyword; presentation colours it.</summary>
    public enum LegacySkillTone { Neutral, Recovery, Followup, Reduction, HighPower, MultiHit, Variance, Defence }

    /// <summary>A skill's initial effect, applied once when its slot starts, in this order:
    /// resistance recovery, then (if the opponent condition holds) direct resistance loss and ACT gain, then the buff.
    /// The table rejects combinations this order cannot honour; see <see cref="LegacySkillDefinitions"/>.</summary>
    public sealed class LegacySkillEffect
    {
        public static LegacySkillEffect None { get; } = new LegacySkillEffect();

        /// <summary>Extra ACT the player recovers next turn. Enemies never recover ACT.</summary>
        public int ActGain { get; internal set; }
        /// <summary>Power bonus granted to this fighter's following slots this turn.</summary>
        public int BuffPowerPercent { get; internal set; }
        /// <summary>Received-damage reduction for the following slots; negative values make them vulnerable.</summary>
        public int BuffProtectionPercent { get; internal set; }
        /// <summary>Following slots the buff lasts, counted per slot including empty ones. Zero grants no buff.</summary>
        public int BuffSlots { get; internal set; }
        /// <summary>Percent of maximum resistance restored, rounded half to even. A self condition.</summary>
        public int ResistanceRecoveryPercent { get; internal set; }
        /// <summary>Resistance removed directly from the opponent; any excess never becomes health damage.</summary>
        public int OpponentResistanceReduction { get; internal set; }
        /// <summary>When set, ACT gain and direct resistance loss need the same slot's opponent to have this property.</summary>
        public LegacySkillProperty? OpponentProperty { get; internal set; }
        /// <summary>When set, ACT gain and direct resistance loss need the same slot's opponent to be this kind.</summary>
        public LegacySkillKind? OpponentKind { get; internal set; }

        public bool HasOpponentCondition => OpponentProperty.HasValue || OpponentKind.HasValue;
        public bool HasBuff => BuffSlots > 0;

        /// <summary>The roles this effect implies; the skill's kind, hits and design tags add the rest.</summary>
        public LegacySkillRole Roles
        {
            get
            {
                LegacySkillRole roles = LegacySkillRole.None;
                if (ActGain > 0) roles |= LegacySkillRole.ActRecovery;
                if (HasBuff && BuffPowerPercent > 0) roles |= LegacySkillRole.FollowupPower;
                if (HasBuff && BuffProtectionPercent > 0) roles |= LegacySkillRole.DamageReduction;
                if (HasBuff && BuffProtectionPercent < 0) roles |= LegacySkillRole.Vulnerability;
                if (ResistanceRecoveryPercent > 0) roles |= LegacySkillRole.ResistanceRecovery;
                if (OpponentResistanceReduction > 0) roles |= LegacySkillRole.DirectResistanceDamage;
                return roles;
            }
        }
    }

    /// <summary>Two keyword badges and an effect summary for a skill's detail view.</summary>
    public sealed class LegacySkillInfo
    {
        public string Main { get; internal set; }
        /// <summary>Replaces <see cref="Main"/> when describing an enemy's skill; null keeps it.</summary>
        public string EnemyMain { get; internal set; }
        public LegacySkillSymbol MainSymbol { get; internal set; }
        public LegacySkillTone MainTone { get; internal set; }
        public string Secondary { get; internal set; } = string.Empty;
        /// <summary>Replaces <see cref="Secondary"/> when describing an enemy's skill; null keeps it.</summary>
        public string EnemySecondary { get; internal set; }
        public LegacySkillSymbol SecondarySymbol { get; internal set; } = LegacySkillSymbol.Hits;
        public LegacySkillTone SecondaryTone { get; internal set; }
        public string Description { get; internal set; }
    }

    /// <summary>Player-facing copy. Every table row writes its own ShortLabel and Detail, so they never follow a copy's shape.
    /// A null Effect uses the generic text for the skill's kind and hits; a null Purpose uses the short label;
    /// a null Info uses generic badges. A set Info is shown as written.</summary>
    public sealed class LegacySkillText
    {
        public string ShortLabel { get; internal set; }
        public string Detail { get; internal set; }
        /// <summary>Lobby purpose line; falls back to <see cref="ShortLabel"/>.</summary>
        public string Purpose { get; internal set; }
        /// <summary>Lobby effect sentence.</summary>
        public string Effect { get; internal set; }
        public LegacySkillInfo Info { get; internal set; }
    }

    /// <summary>Everything that makes a technique: its numbers, its initial effect, its design tags and its copy.</summary>
    public sealed class LegacySkillDefinition
    {
        internal LegacySkillDefinition(LegacySkill skill, LegacySkillEffect effect = null, LegacySkillText text = null,
            bool highPower = false, bool variablePower = false)
        {
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            Effect = effect ?? LegacySkillEffect.None;
            Text = text ?? new LegacySkillText();
            HighPower = highPower;
            VariablePower = variablePower;
        }

        /// <summary>The level-zero skill. Stage and upgrade copies keep its id, which selects this definition.</summary>
        public LegacySkill Skill { get; }
        public LegacySkillEffect Effect { get; }
        public LegacySkillText Text { get; }
        /// <summary>Design tag for a strong attack; it does not change combat. Attacks only.</summary>
        public bool HighPower { get; }
        /// <summary>Design tag for an attack with a wide power range; it does not change combat. Attacks only.</summary>
        public bool VariablePower { get; }
    }

    /// <summary>The single table of implemented techniques. Adding a technique means adding one entry here;
    /// combat effects, conditions, roles and every skill text read it by id.</summary>
    public static class LegacySkillDefinitions
    {
        private const string AttackDamageDetail = LegacySkillRoles.AttackDamageDetail;
        private const string SplitInTwo = "총 위력을 두 번에 나누어 공격합니다. 위력이 2배가 되는 것은 아닙니다.";
        private const string PlainGuardDetail = "부가 효과 없이 방어 수치로 상대 공격을 줄입니다.";
        private const string PlayerAct = "플레이어 ACT";

        // Level zero rows 1-9 from the original Assets/csv/스킬 수치.csv, then the ten acquisition skills.
        private static readonly LegacySkillDefinition[] definitions =
        {
            new LegacySkillDefinition(
                new LegacySkill(1, "베기", 1, 4, 5, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "다음 턴 ACT 회복량 +1"),
                new LegacySkillEffect { ActGain = 1 },
                new LegacySkillText
                {
                    ShortLabel = "ACT 회복",
                    Detail = "다음 턴 ACT 회복량 +1.\n" + AttackDamageDetail,
                    Purpose = "다음 턴 ACT 회복",
                    Effect = "사용하면 다음 턴 ACT를 1 더 회복합니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "ACT +1", EnemyMain = PlayerAct, MainSymbol = LegacySkillSymbol.Recovery, MainTone = LegacySkillTone.Recovery,
                        Secondary = "다음 턴", SecondarySymbol = LegacySkillSymbol.Act, SecondaryTone = LegacySkillTone.Recovery,
                        Description = "다음 턴 ACT 회복 +1",
                    },
                }),
            new LegacySkillDefinition(
                new LegacySkill(2, "예리한 베기", 2, 11, 15, LegacySkillKind.Attack, LegacySkillProperty.Slash, 2, 0, "칼을 휘둘러 상대의 약점을 베어냅니다."),
                text: new LegacySkillText
                {
                    ShortLabel = "고화력·연타",
                    Detail = "총 위력을 2회로 나누는 강한 공격.\n" + AttackDamageDetail,
                    Effect = SplitInTwo,
                },
                highPower: true),
            new LegacySkillDefinition(
                new LegacySkill(3, "찌르기", 1, 5, 5, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 3, 1, "다음 기술 3개의 피해량 +10%"),
                new LegacySkillEffect { BuffPowerPercent = 10, BuffSlots = 3 },
                new LegacySkillText
                {
                    ShortLabel = "후속 위력",
                    Detail = "이번 턴의 이후 3슬롯의 공격·방어 위력 +10%.\n" + AttackDamageDetail,
                    Purpose = "뒤에 놓인 행동 강화",
                    Effect = "이 기술 뒤의 행동 3개는 공격·방어 위력이 10% 높아집니다. 이번 턴만 적용됩니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "후속 +10%", MainSymbol = LegacySkillSymbol.Followup, MainTone = LegacySkillTone.Followup,
                        Secondary = "뒤 3칸", SecondarySymbol = LegacySkillSymbol.Hits, SecondaryTone = LegacySkillTone.Followup,
                        Description = "이번 턴 · 뒤 3칸 위력 +10%\n공격·방어 모두 적용 · 위력 분할\n소수점 버림 · 한 타 최소 1",
                    },
                }),
            new LegacySkillDefinition(
                new LegacySkill(4, "정교한 찌르기", 2, 11, 15, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 1, 1, "칼로 상대의 약한 부위를 꿰뚫습니다."),
                text: new LegacySkillText
                {
                    ShortLabel = "고화력",
                    Detail = "한 번에 위력을 싣는 단타 공격.\n" + AttackDamageDetail,
                },
                highPower: true),
            new LegacySkillDefinition(
                new LegacySkill(5, "부수기", 1, 6, 9, LegacySkillKind.Attack, LegacySkillProperty.Hit, 2, 2, "상대를 내리쳐 자세를 흐트러뜨립니다."),
                text: new LegacySkillText
                {
                    ShortLabel = "연타",
                    Detail = "총 위력을 2회로 나누어 공격.\n" + AttackDamageDetail,
                    Effect = SplitInTwo,
                }),
            new LegacySkillDefinition(
                new LegacySkill(6, "강력한 부수기", 3, 16, 20, LegacySkillKind.Attack, LegacySkillProperty.Hit, 3, 2, "상대를 내려쳐 큰 피해를 줍니다."),
                text: new LegacySkillText
                {
                    ShortLabel = "고화력·연타",
                    Detail = "총 위력을 3회로 나누는 강한 공격.\n" + AttackDamageDetail,
                    Effect = "총 위력을 세 번에 나누어 공격합니다. 위력이 3배가 되는 것은 아닙니다.",
                },
                highPower: true),
            new LegacySkillDefinition(
                new LegacySkill(7, "막기", 1, 5, 8, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "상대 타격 기술에 대응하면 다음 턴 ACT 회복량 +2"),
                new LegacySkillEffect { ActGain = 2, OpponentProperty = LegacySkillProperty.Hit },
                new LegacySkillText
                {
                    ShortLabel = "조건부 ACT",
                    Detail = "방어. 상대가 타격 기술이면 다음 턴 ACT 회복량 +2.",
                    Purpose = "방어 + 조건부 ACT 회복",
                    Effect = "같은 순서의 상대 공격이 타격 속성이면, 다음 턴 ACT를 2 더 회복합니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "조건부 ACT +2", EnemyMain = PlayerAct, MainSymbol = LegacySkillSymbol.Recovery, MainTone = LegacySkillTone.Recovery,
                        Secondary = "타격 대응", SecondarySymbol = LegacySkillSymbol.Guard, SecondaryTone = LegacySkillTone.Defence,
                        Description = "같은 칸 상대가 타격일 때\n다음 턴 ACT 회복 +2",
                    },
                }),
            new LegacySkillDefinition(
                new LegacySkill(8, "흘리기", 1, 7, 11, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 1, "이 페이즈의 이후 기술에서 받는 피해량 -30%"),
                new LegacySkillEffect { BuffProtectionPercent = 30, BuffSlots = 10 },
                new LegacySkillText
                {
                    ShortLabel = "피해 감소",
                    Detail = "방어. 이번 턴의 이후 슬롯에서 받는 피해 -30% (최대 10슬롯).",
                    Purpose = "방어 + 이후 피해 감소",
                    Effect = "이 기술 뒤에서 받는 피해가 30% 줄어듭니다. 이번 턴의 이후 행동 최대 10개에 적용됩니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "피해 -30%", MainSymbol = LegacySkillSymbol.Guard, MainTone = LegacySkillTone.Reduction,
                        Secondary = "후속 보호", SecondarySymbol = LegacySkillSymbol.Reduction, SecondaryTone = LegacySkillTone.Reduction,
                        Description = "이번 턴 · 뒤 최대 10칸\n받는 피해 30% 감소",
                    },
                }),
            new LegacySkillDefinition(
                new LegacySkill(9, "쳐내기", 1, 4, 6, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 2, "이 페이즈의 이후 기술 위력 +3%"),
                // The source CSV says "next turn", but Skill_Blocking adds
                // to Cur, and Unit.TurnInit discards it at the turn boundary.
                new LegacySkillEffect { BuffPowerPercent = 3, BuffSlots = 10 },
                new LegacySkillText
                {
                    ShortLabel = "후속 위력",
                    Detail = "방어. 이번 턴의 이후 슬롯 공격·방어 위력 +3% (최대 10슬롯).",
                    Purpose = "뒤에 놓인 행동 강화",
                    Effect = "이 기술 뒤 행동의 공격·방어 위력이 3% 높아집니다. 이번 턴의 이후 행동 최대 10개에 적용됩니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "후속 +3%", MainSymbol = LegacySkillSymbol.Followup, MainTone = LegacySkillTone.Followup,
                        Secondary = "위력 지원", SecondarySymbol = LegacySkillSymbol.Sword, SecondaryTone = LegacySkillTone.Followup,
                        Description = "이번 턴 · 뒤 최대 10칸 위력 +3%\n공격·방어 모두 적용",
                    },
                }),

            // Selected level-zero legacy rows. The first six retain their numeric-only
            // behavior; imported utility skills below have explicit runtime effects.
            new LegacySkillDefinition(
                new LegacySkill(14, "가로베기", 1, 6, 12, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                    1, 0, "공격력 6~12 / 1회", iconId: 10),
                text: new LegacySkillText { ShortLabel = "단타", Detail = "부가 효과 없는 단타 공격.\n" + AttackDamageDetail }),
            new LegacySkillDefinition(
                new LegacySkill(15, "사선베기", 1, 7, 10, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                    2, 0, "공격력 7~10 / 2회", iconId: 11),
                text: new LegacySkillText { ShortLabel = "연타", Detail = "부가 효과 없는 2연타 공격.\n" + AttackDamageDetail, Effect = SplitInTwo }),
            new LegacySkillDefinition(
                new LegacySkill(16, "일도양단", 5, 3, 20, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                    1, 1, "공격력 3~20 / 1회", iconId: 12),
                text: new LegacySkillText
                {
                    ShortLabel = "변동 한방",
                    Detail = "위력 편차가 큰 단타 공격. 높은 위력이 보장되지는 않습니다.\n" + AttackDamageDetail,
                    Effect = "한 번 공격하며 위력 차이가 큽니다. 높은 위력이 항상 나오지는 않습니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "위력 편차", MainSymbol = LegacySkillSymbol.Variance, MainTone = LegacySkillTone.Variance,
                        Secondary = "단타", SecondarySymbol = LegacySkillSymbol.Sword,
                        Description = "위력 편차가 큰 1회 공격\n최대 위력이 보장되지는 않습니다.",
                    },
                },
                variablePower: true),
            new LegacySkillDefinition(
                new LegacySkill(17, "호흡", 2, 9, 12, LegacySkillKind.Defence, LegacySkillProperty.Defence,
                    1, 0, "방어력 9~12", iconId: 13),
                text: new LegacySkillText { ShortLabel = "방어", Detail = PlainGuardDetail }),
            new LegacySkillDefinition(
                new LegacySkill(21, "급소 찌르기", 3, 12, 17, LegacySkillKind.Attack, LegacySkillProperty.Penetrate,
                    1, 1, "공격력 12~17 / 1회", iconId: 14),
                text: new LegacySkillText { ShortLabel = "고화력", Detail = "한 번에 위력을 싣는 강한 찌르기.\n" + AttackDamageDetail },
                highPower: true),
            new LegacySkillDefinition(
                new LegacySkill(32, "유연함", 2, 8, 12, LegacySkillKind.Defence, LegacySkillProperty.Defence,
                    1, 1, "방어력 8~12", iconId: 15),
                text: new LegacySkillText { ShortLabel = "방어", Detail = PlainGuardDetail }),
            new LegacySkillDefinition(
                new LegacySkill(10, "준비", 1, 2, 3, LegacySkillKind.Attack, LegacySkillProperty.Hit,
                    1, 0, "이번 턴의 다음 행동 공격·방어 위력 +30%", iconId: 3),
                // Ready's Setting runs after the source's power snapshot,
                // so its one-use bonus strengthens the following slot.
                new LegacySkillEffect { BuffPowerPercent = 30, BuffSlots = 1 },
                new LegacySkillText
                {
                    ShortLabel = "다음 칸 강화",
                    Detail = "이번 턴의 바로 다음 1슬롯 공격·방어 위력 +30%.\n" + AttackDamageDetail,
                    Purpose = "바로 다음 행동 강화",
                    Effect = "이번 턴의 바로 다음 칸에서 공격·방어 위력이 30% 높아집니다. 턴을 넘겨 유지되지 않습니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "후속 +30%", MainSymbol = LegacySkillSymbol.Followup, MainTone = LegacySkillTone.Followup,
                        Secondary = "다음 1칸", SecondarySymbol = LegacySkillSymbol.Hits, SecondaryTone = LegacySkillTone.Followup,
                        Description = "이번 턴 · 바로 다음 1칸\n공격·방어 위력 +30%",
                    },
                }),
            new LegacySkillDefinition(
                new LegacySkill(12, "전진", 1, 4, 8, LegacySkillKind.Attack, LegacySkillProperty.Penetrate,
                    2, 2, "다음 턴 ACT 회복 +3 / 이번 턴의 다음 칸 받는 피해 배율 +50%", iconId: 1),
                // Forward uses Cur/count=1 in the source despite its CSV
                // saying next turn. Vulnerability expires at this turn's end.
                new LegacySkillEffect { ActGain = 3, BuffProtectionPercent = -50, BuffSlots = 1 },
                new LegacySkillText
                {
                    ShortLabel = "ACT 회복·취약",
                    Detail = "사용하면 다음 턴 ACT 회복량 +3 (플레이어 전용). 이번 턴의 바로 다음 1슬롯에서 받는 피해 배율 +50%. 총 위력을 2회로 나누어 공격.\n" + AttackDamageDetail,
                    Purpose = "ACT 회복 + 다음 칸 취약",
                    Effect = "사용하면 다음 턴 ACT를 3 더 회복합니다. 이번 턴의 바로 다음 칸에서 받는 피해 배율이 50% 늘어납니다. 총 위력을 두 번에 나누어 공격합니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "ACT +3", EnemyMain = PlayerAct, MainSymbol = LegacySkillSymbol.Recovery, MainTone = LegacySkillTone.Recovery,
                        Secondary = "배율 +50%", SecondarySymbol = LegacySkillSymbol.Sword, SecondaryTone = LegacySkillTone.HighPower,
                        Description = "다음 턴 ACT 회복 +3\n이번 턴 · 다음 1칸 받는 피해 배율 +50%\n위력 2회 분할 · 버림 · 한 타 최소 1",
                    },
                }),
            new LegacySkillDefinition(
                new LegacySkill(19, "투지", 2, 7, 11, LegacySkillKind.Defence, LegacySkillProperty.Defence,
                    1, 2, "시작 시 최대 저항력의 10% 회복 / 최대 저항력까지", iconId: 13),
                new LegacySkillEffect { ResistanceRecoveryPercent = 10 },
                new LegacySkillText
                {
                    ShortLabel = "저항 회복",
                    Detail = "방어. 기술 시작 시 최대 저항의 10%를 반올림해 회복합니다 (최대치까지).",
                    Purpose = "방어 + 저항 회복",
                    Effect = "기술 시작 시 최대 저항의 10%를 반올림해 회복합니다. 최대치를 넘지 않으며, 방어 수치로 같은 칸의 상대 공격을 줄입니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "저항 회복", MainSymbol = LegacySkillSymbol.Recovery, MainTone = LegacySkillTone.Recovery,
                        Secondary = "최대치의 10%", SecondarySymbol = LegacySkillSymbol.Guard, SecondaryTone = LegacySkillTone.Defence,
                        Description = "기술 시작 시 최대 저항의 10% 회복\n반올림 · 최대치 제한\n같은 칸 상대 공격을 방어",
                    },
                }),
            new LegacySkillDefinition(
                new LegacySkill(42, "발검", 1, 4, 8, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                    1, 0, "상대 방어 시 저항력 20 직접 감소 / 다음 턴 ACT 회복 +3", iconId: 10),
                // The player skill's actual code grants three, not the CSV's one.
                // Never use the enemy-only instant break rule.
                new LegacySkillEffect { OpponentKind = LegacySkillKind.Defence, OpponentResistanceReduction = 20, ActGain = 3 },
                new LegacySkillText
                {
                    ShortLabel = "방어 대응·저항 감소",
                    Detail = "기술 시작 시 같은 슬롯 상대가 방어이면 저항을 직접 20 감소시키고 다음 턴 ACT 회복량 +3 (ACT는 플레이어 전용). 초과 저항 감소는 체력 피해로 이어지지 않습니다.\n" + AttackDamageDetail,
                    Purpose = "방어 대응 + 저항 감소·ACT 회복",
                    Effect = "기술 시작 시 같은 칸의 상대가 방어이면 저항을 직접 20 줄이고, 다음 턴 ACT를 3 더 회복합니다. 초과 감소는 체력 피해로 이어지지 않습니다.",
                    Info = new LegacySkillInfo
                    {
                        Main = "저항 -20", MainSymbol = LegacySkillSymbol.Reduction, MainTone = LegacySkillTone.Defence,
                        Secondary = "ACT +3", EnemySecondary = PlayerAct, SecondarySymbol = LegacySkillSymbol.Recovery, SecondaryTone = LegacySkillTone.Recovery,
                        Description = "기술 시작 시 같은 칸 상대가 방어이면\n저항 직접 -20 · 다음 턴 ACT +3\n초과 저항 감소는 체력 피해 없음",
                    },
                }),
        };

        private const int InitialCount = 9;
        private static readonly Dictionary<int, LegacySkillDefinition> byId = Index();
        private static readonly IReadOnlyList<LegacySkill> initial = Skills(0, InitialCount);
        private static readonly IReadOnlyList<LegacySkill> acquisition = Skills(InitialCount, definitions.Length - InitialCount);
        private static readonly IReadOnlyList<LegacySkillDefinition> all = Array.AsReadOnly(definitions);

        public static IReadOnlyList<LegacySkillDefinition> All => all;
        /// <summary>The nine starting techniques, three per lane: attacks then guards, each in Q/W/E order.
        /// Positions matter: the enemy pattern, tutorial and stage counters index this list.</summary>
        public static IReadOnlyList<LegacySkill> InitialSkills => initial;
        /// <summary>The techniques the campaign shop offers, in shelf order.</summary>
        public static IReadOnlyList<LegacySkill> AcquisitionSkills => acquisition;

        /// <summary>The definition selected by a skill's id, or null for breathing and unknown skills.</summary>
        public static LegacySkillDefinition Find(LegacySkill skill) =>
            skill == null || skill.IsWait ? null : byId.TryGetValue(skill.Id, out var definition) ? definition : null;

        private static Dictionary<int, LegacySkillDefinition> Index()
        {
            var index = new Dictionary<int, LegacySkillDefinition>(definitions.Length);
            var initialPerLane = new int[3];
            for (int i = 0; i < definitions.Length; i++)
            {
                LegacySkillDefinition definition = definitions[i];
                if (index.ContainsKey(definition.Skill.Id))
                    throw new InvalidOperationException("Duplicate skill definition id " + definition.Skill.Id);
                Validate(definition);
                if (i < InitialCount && definition.Skill.LaneIndex >= 0 && definition.Skill.LaneIndex < initialPerLane.Length)
                    initialPerLane[definition.Skill.LaneIndex]++;
                index.Add(definition.Skill.Id, definition);
            }
            // The loadout saves exactly three skills per lane, and the starting set must already be one.
            foreach (int count in initialPerLane)
                if (count != 3) throw new InvalidOperationException("The starting skills must be three per lane");
            return index;
        }

        /// <summary>Rejects rows the effect interpreter or the skill texts would silently mishandle.</summary>
        private static void Validate(LegacySkillDefinition definition)
        {
            LegacySkill skill = definition.Skill;
            LegacySkillEffect effect = definition.Effect;
            string problem = null;
            if (skill.IsWait) problem = "a wait action is never looked up";
            else if (effect.ActGain < 0 || effect.BuffSlots < 0 || effect.ResistanceRecoveryPercent < 0 ||
                     effect.OpponentResistanceReduction < 0)
                problem = "effect amounts cannot be negative";
            else if (effect.HasBuff != (effect.BuffPowerPercent != 0 || effect.BuffProtectionPercent != 0))
                problem = "a buff needs both slots and a power or protection change";
            else if (effect.OpponentProperty.HasValue && effect.OpponentKind.HasValue)
                problem = "an opponent condition checks a property or a kind, not both";
            else if (effect.HasOpponentCondition && effect.ResistanceRecoveryPercent > 0)
                problem = "resistance recovery is its own condition and cannot share an opponent condition";
            else if (effect.HasOpponentCondition && effect.ActGain == 0 && effect.OpponentResistanceReduction == 0)
                problem = "an opponent condition gates only ACT gain and direct resistance loss";
            else if ((definition.HighPower || definition.VariablePower) && skill.Kind != LegacySkillKind.Attack)
                problem = "power tags apply to attacks only";
            else if (definition.Text.Info != null && (definition.Text.Info.Main == null || definition.Text.Info.Description == null))
                problem = "a detail badge set needs its main keyword and description";
            else if (string.IsNullOrEmpty(definition.Text.ShortLabel) || string.IsNullOrEmpty(definition.Text.Detail))
                problem = "a table skill needs its own short label and detail";
            if (problem != null)
                throw new InvalidOperationException("Skill definition " + skill.Id + ": " + problem);
        }

        private static IReadOnlyList<LegacySkill> Skills(int start, int count)
        {
            var skills = new LegacySkill[count];
            for (int i = 0; i < count; i++) skills[i] = definitions[start + i].Skill;
            return Array.AsReadOnly(skills);
        }
    }
}
