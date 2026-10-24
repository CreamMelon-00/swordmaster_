using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class BleedingStatusAndSheetTests
    {
        [Test]
        public void Bleeding_UsesFixedDamageAndOneCombinedDuration_WithoutChangingCombatMultipliers()
        {
            var statuses = new LegacyStatusCollection();
            statuses.Grant(LegacyStatusKind.Bleeding, 50, 3);
            statuses.GrantBleeding(2);
            Assert.That(statuses.BleedingCount, Is.EqualTo(5));
            Assert.That(statuses.Statuses.Count, Is.EqualTo(1));
            LegacyStatusSnapshot bleeding = statuses.Statuses.Single();
            Assert.That(bleeding.Kind, Is.EqualTo(LegacyStatusKind.Bleeding));
            Assert.That(bleeding.Percent, Is.EqualTo(2), "The snapshot strength is fixed damage, not a percentage.");
            Assert.That(bleeding.RemainingCount, Is.EqualTo(5));
            Assert.That(statuses.PowerPercent, Is.Zero);
            Assert.That(statuses.ProtectionPercent, Is.Zero);
            Assert.That(statuses.ReceivedMultiplier, Is.EqualTo(1d));
            Assert.That(statuses.HasProtection, Is.False);
        }

        [Test]
        public void Bleeding_IsNotConsumedByPowerOrIncomingAttackCounters()
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantBleeding(3);
            statuses.Grant(LegacyStatusKind.PowerUp, 50, 2);
            statuses.Grant(LegacyStatusKind.Protection, 50, 1);
            statuses.Grant(LegacyStatusKind.Vulnerability, 50, 1);
            Invoke(statuses, "ConsumeTechnique");
            Invoke(statuses, "ConsumeIncomingAttack", Boundary(statuses));
            Assert.That(Statuses(statuses), Is.EqualTo("Bleeding:2:3|PowerUp:50:1"));
            statuses.ConsumeBleedingAttack(Boundary(statuses));
            Assert.That(Statuses(statuses), Is.EqualTo("Bleeding:2:2|PowerUp:50:1"));
        }

        [Test]
        public void NewBleeding_WaitsForTheNextAttack_ThenConsumesOneWholeTechnique()
        {
            var statuses = new LegacyStatusCollection();
            int oldBoundary = Boundary(statuses);
            statuses.GrantBleeding(3);
            Assert.That(statuses.BleedingDamageThrough(oldBoundary), Is.Zero);
            statuses.ConsumeBleedingAttack(oldBoundary);
            Assert.That(statuses.BleedingCount, Is.EqualTo(3));
            int nextBoundary = Boundary(statuses);
            for (int hit = 0; hit < 5; hit++)
                Assert.That(statuses.BleedingDamageThrough(nextBoundary), Is.EqualTo(2), "Each hit sees the same attack duration.");
            statuses.ConsumeBleedingAttack(nextBoundary);
            Assert.That(statuses.BleedingCount, Is.EqualTo(2), "Five hits spend one attack technique, not five counts.");
        }

        [Test]
        public void RegrantingOldBleeding_AddsCounts_AndOnlySpendsTheCurrentOldCount()
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantBleeding(1);
            int boundary = Boundary(statuses);
            statuses.GrantBleeding(2);
            Assert.That(statuses.BleedingDamageThrough(boundary), Is.EqualTo(2));
            statuses.ConsumeBleedingAttack(boundary);
            Assert.That(statuses.BleedingCount, Is.EqualTo(2), "Both newly granted attack counts survive.");
            Assert.That(statuses.Statuses.Count, Is.EqualTo(1));
        }

        [Test]
        public void StanchingAllOldBleeding_SuppressesItWithoutActivatingANewGrantInThisAttack()
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantBleeding(2);
            statuses.Grant(LegacyStatusKind.Vulnerability, 50, 3);
            int boundary = Boundary(statuses);
            Assert.That(statuses.ReduceBleeding(3), Is.EqualTo(2));
            Assert.That(statuses.BleedingDamageThrough(boundary), Is.Zero);
            statuses.GrantBleeding(4);
            Assert.That(statuses.BleedingDamageThrough(boundary), Is.Zero);
            statuses.ConsumeBleedingAttack(boundary);
            Assert.That(Statuses(statuses), Is.EqualTo("Vulnerability:50:3|Bleeding:2:4"));
            Assert.That(statuses.BleedingDamageThrough(Boundary(statuses)), Is.EqualTo(2));
        }

        [Test]
        public void PartialStanch_LeavesOldBleedingActive_AndNeverConsumesOtherStatusKinds()
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantBleeding(4);
            statuses.Grant(LegacyStatusKind.PowerUp, 50, 2);
            statuses.Grant(LegacyStatusKind.Protection, 50, 2);
            statuses.Grant(LegacyStatusKind.Vulnerability, 50, 2);
            int boundary = Boundary(statuses);
            Assert.That(statuses.ReduceBleeding(2), Is.EqualTo(2));
            Assert.That(statuses.BleedingDamageThrough(boundary), Is.EqualTo(2));
            statuses.ConsumeBleedingAttack(boundary);
            Assert.That(Statuses(statuses), Is.EqualTo("Bleeding:2:1|PowerUp:50:2|Protection:50:2|Vulnerability:50:2"));
            Assert.That(statuses.ConsumeProtection(), Is.True);
            Assert.That(Statuses(statuses), Is.EqualTo("Bleeding:2:1|PowerUp:50:2|Vulnerability:50:2"));
        }

        [Test]
        public void BleedingCopies_AreIndependent_AndClearRemovesTheirDuration()
        {
            var original = new LegacyStatusCollection();
            original.GrantBleeding(3);
            LegacyStatusCollection copy = original.Copy();
            Assert.That(copy.BleedingDamageThrough(Boundary(original)), Is.EqualTo(2));
            copy.ReduceBleeding(1);
            copy.ConsumeBleedingAttack(Boundary(copy));
            Assert.That(copy.BleedingCount, Is.EqualTo(1));
            Assert.That(original.BleedingCount, Is.EqualTo(3));
            copy.Clear();
            Assert.That(copy.Statuses, Is.Empty);
            Assert.That(copy.BleedingCount, Is.Zero);
            Assert.That(copy.BleedingDamageThrough(int.MaxValue), Is.Zero);
            Assert.That(original.BleedingCount, Is.EqualTo(3));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveBleedingChanges_DoNotMutateStatuses(int count)
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantBleeding(3);
            statuses.GrantBleeding(count);
            Assert.That(statuses.ReduceBleeding(count), Is.Zero);
            Assert.That(statuses.BleedingCount, Is.EqualTo(3));
        }

        [Test]
        public void BleedingText_ExplainsFixedDamageAttackDurationAndDelayedGrants()
        {
            Assert.That(LegacyStatusNames.Name(LegacyStatusKind.Bleeding), Is.EqualTo("출혈"));
            Assert.That(LegacyStatusNames.Label(LegacyStatusKind.Bleeding, 2, 4), Is.EqualTo("출혈 4"));
            string text = LegacyStatusNames.Description(LegacyStatusKind.Bleeding, 2);
            Assert.That(text, Does.Contain("고정 체력 피해 2"));
            Assert.That(text, Does.Contain("명중하지 않아도"));
            Assert.That(text, Does.Contain("방어·보호·취약·붕괴의 영향을 받지 않습니다"));
            Assert.That(text, Does.Contain("공격 기술 하나가 끝나면"));
            Assert.That(text, Does.Contain("방어·숨고르기는 소모하지 않습니다"));
            Assert.That(text, Does.Contain("이번 양측 공격에 적용하지 않습니다"));
            Assert.That(text, Does.Contain("횟수만 더하며 턴이 지나도 남습니다"));
            Assert.That(text, Does.Not.Contain("%"));
        }

        [Test]
        public void CompoundBleedingEffects_RoundTripEveryField_AlongsideExistingColumnEffects()
        {
            const string phrase = "자기 출혈 3 이상 기본 위력 +12; 상대 출혈 3 이상 기본 위력 +16; " +
                "자신의 출혈 2 감소; 상대가 출혈 상태면 출혈 4로 대체; 상대에게 출혈 1 부여; 자신에게 출혈 2 부여";
            List<string[]> rows = Rows();
            Set(rows, 3, Column.ActGain, "1");
            Set(rows, 3, Column.UseEffect, phrase);
            LegacySkillTable parsed = Parse(rows);
            LegacySkillDefinition definition = parsed.Find(3);
            LegacySkillEffect effect = definition.Effect;
            Assert.That(effect.SelfBleedingCount, Is.EqualTo(2));
            Assert.That(effect.OpponentBleedingCount, Is.EqualTo(1));
            Assert.That(effect.OpponentBleedingReplacementCount, Is.EqualTo(4));
            Assert.That(effect.BleedingReduction, Is.EqualTo(2));
            Assert.That(effect.OpponentBleedingRequired, Is.EqualTo(3));
            Assert.That(effect.OpponentBleedingPowerBonus, Is.EqualTo(16));
            Assert.That(effect.SelfBleedingRequired, Is.EqualTo(3));
            Assert.That(effect.SelfBleedingPowerBonus, Is.EqualTo(12));
            Assert.That(effect.ActGain, Is.EqualTo(1), "Parsing the compound cell preserves effects from other columns.");
            Assert.That(effect.HasUseEffect && effect.HasBleedingEffect, Is.True);
            Assert.That(effect.Roles, Is.EqualTo(LegacySkillRole.ActRecovery | LegacySkillRole.Bleeding |
                LegacySkillRole.BleedingReduction | LegacySkillRole.BleedingPower));
            string canonical = LegacySkillLabels.UseEffect(effect);
            Assert.That(canonical, Is.EqualTo("자신에게 출혈 2 부여; 상대에게 출혈 1 부여; " +
                "상대가 출혈 상태면 출혈 4로 대체; 자신의 출혈 2 감소; " +
                "상대 출혈 3 이상 기본 위력 +16; 자기 출혈 3 이상 기본 위력 +12"));
            LegacySkillTable reread = LegacySkillSheet.Parse("bleeding round trip", LegacySkillSheet.Write(parsed));
            Assert.That(LegacySkillSheetTests.Dump(reread.Find(3)), Is.EqualTo(LegacySkillSheetTests.Dump(definition)));
            Assert.That(LegacySkillSheet.Headers.Count, Is.EqualTo(48), "Compound use effects stay in their phrase column; the optional duration column serves prototype skills.");
        }

        [TestCase(1, 1)]
        [TestCase(20, 50)]
        public void BleedingPhrases_AcceptCountAndPowerBounds_WithOptionalSpaces(int count, int bonus)
        {
            LegacySkillEffect effect = Effect($"자신에게출혈{count}부여;상대에게출혈{count}부여;" +
                $"상대가출혈상태면출혈{count}로대체;자신의출혈{count}감소;" +
                $"상대출혈{count}이상기본위력+{bonus};자기출혈{count}이상기본위력+{bonus}");
            Assert.That(effect.SelfBleedingCount, Is.EqualTo(count));
            Assert.That(effect.OpponentBleedingCount, Is.EqualTo(count));
            Assert.That(effect.OpponentBleedingReplacementCount, Is.EqualTo(count));
            Assert.That(effect.BleedingReduction, Is.EqualTo(count));
            Assert.That(effect.BleedingPowerBonus(count, count), Is.EqualTo(bonus * 2));
        }

        [Test]
        public void ExistingUseEffects_CanCoexistWithBleeding_AndRetainTheirCanonicalText()
        {
            LegacySkillEffect effect = Effect("상대에게 출혈 1 부여; 자동 회피; 보호 소모 위력 +10; " +
                "앞 공격 총타수 5 이상 위력 +12; 상대 공격 시 다음 턴 ACT 회복 2 강탈; " +
                "앞 숨고르기마다 위력 +4 (최대 4); 앞 순환 기술마다 다음 턴 제한시간 +2초 (최대 3); " +
                "상대 ACT 8 이상이면 ACT 3 강탈");
            Assert.That(effect.AutoDodge && effect.HasProtectionSpend && effect.HasAheadHitPower &&
                effect.HasActRecoverySteal && effect.HasAheadPower && effect.HasAheadTime && effect.HasActSteal, Is.True);
            Assert.That(effect.OpponentBleedingCount, Is.EqualTo(1));
            string canonical = LegacySkillLabels.UseEffect(effect);
            Assert.That(LegacySkillLabels.UseEffect(Effect(canonical)), Is.EqualTo(canonical));
        }

        [TestCase("자신에게 출혈 0 부여")]
        [TestCase("자신에게 출혈 21 부여")]
        [TestCase("상대에게 출혈 -1 부여")]
        [TestCase("상대에게 출혈 1.5 부여")]
        [TestCase("상대에게 출혈 １ 부여")]
        [TestCase("상대에게 출혈 1 부여; 상대가 출혈 상태면 출혈 0로 대체")]
        [TestCase("상대에게 출혈 1 부여; 상대가 출혈 상태면 출혈 21로 대체")]
        [TestCase("상대에게 출혈 1 부여; 상대 2타 이상 공격이면 출혈 4로 대체")]
        [TestCase("자신의 출혈 0 감소")]
        [TestCase("자신의 출혈 21 감소")]
        [TestCase("상대 출혈 0 이상 기본 위력 +16")]
        [TestCase("상대 출혈 21 이상 기본 위력 +16")]
        [TestCase("자기 출혈 3 이상 기본 위력 +0")]
        [TestCase("자기 출혈 3 이상 기본 위력 +51")]
        [TestCase("자기 출혈 3 초과 기본 위력 +12")]
        [TestCase("자신에게 출혈 2 부여;")]
        [TestCase(";자신에게 출혈 2 부여")]
        [TestCase("자신에게 출혈 2 부여;;상대에게 출혈 1 부여")]
        [TestCase("자신에게 출혈 2 부여, 상대에게 출혈 1 부여")]
        [TestCase("자신에게 출혈 2 부여; 자신에게 출혈 3 부여")]
        [TestCase("상대에게 출혈 1 부여; 상대에게 출혈 2 부여")]
        [TestCase("상대에게 출혈 1 부여; 상대가 출혈 상태면 출혈 4로 대체; 상대가 출혈 상태면 출혈 5로 대체")]
        [TestCase("자신의 출혈 2 감소; 자신의 출혈 1 감소")]
        [TestCase("상대 출혈 3 이상 기본 위력 +16; 상대 출혈 4 이상 기본 위력 +12")]
        [TestCase("자기 출혈 3 이상 기본 위력 +12; 자기 출혈 4 이상 기본 위력 +16")]
        [TestCase("자동 회피; 자동 회피; 상대에게 출혈 1 부여")]
        public void BleedingPhrases_RejectInvalidBoundsSyntaxAndDuplicateCategories(string phrase)
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.UseEffect, phrase);
            IReadOnlyList<string> problems = Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems;
            Assert.That(problems.Any(problem => problem.Contains("'사용 효과':") && problem.Contains("쓸 수 없습니다")), Is.True,
                string.Join("\n", problems));
        }

        [Test]
        public void BleedingReplacement_RequiresTheOrdinaryGrant_AndReplacesInsteadOfAdding()
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.UseEffect, "상대가 출혈 상태면 출혈 4로 대체");
            IReadOnlyList<string> problems = Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems;
            Assert.That(problems.Single(), Does.Contain("'상대에게 출혈 N 부여'도 지정해야"));
            LegacySkillEffect effect = Effect("상대에게 출혈 1 부여; 상대가 출혈 상태면 출혈 4로 대체");
            Assert.That(effect.BleedingToInflict(0), Is.EqualTo(1));
            Assert.That(effect.BleedingToInflict(1), Is.EqualTo(4));
            Assert.That(effect.BleedingToInflict(2), Is.EqualTo(4));
            Assert.That(effect.BleedingToInflict(20), Is.EqualTo(4));
            Assert.That(effect.BleedingToInflict(int.MaxValue), Is.EqualTo(4));
            Assert.That(Effect("상대에게 출혈 1 부여").BleedingToInflict(3), Is.EqualTo(1),
                "Existing bleeding alone cannot introduce a replacement absent from the skill.");
        }

        [TestCase(2, 2, 0)]
        [TestCase(3, 2, 12)]
        [TestCase(2, 3, 16)]
        [TestCase(3, 3, 28)]
        [TestCase(4, 5, 28)]
        public void BleedingPower_UsesInclusiveIndependentCountThresholds(int self, int opponent, int expected)
        {
            LegacySkillEffect effect = Effect("상대 출혈 3 이상 기본 위력 +16; 자기 출혈 3 이상 기본 위력 +12");
            Assert.That(effect.BleedingPowerBonus(self, opponent), Is.EqualTo(expected));
        }

        [Test]
        public void Guard_MayStanchButCannotUseBleedingPowerBonuses()
        {
            List<string[]> rows = Rows();
            Set(rows, 7, Column.UseEffect, "자신의 출혈 2 감소");
            LegacySkillEffect effect = Parse(rows).Find(7).Effect;
            Assert.That(effect.BleedingReduction, Is.EqualTo(2));
            Assert.That(effect.HasBleedingReduction, Is.True);
            Assert.That(effect.HasBleedingGrant || effect.HasBleedingPower, Is.False);
            Set(rows, 7, Column.UseEffect, "자기 출혈 3 이상 기본 위력 +12");
            IReadOnlyList<string> problems = Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems;
            Assert.That(problems.Any(problem => problem.Contains("공격 기술에만")), Is.True);
        }

        [TestCase(500, "적")]
        [TestCase(59, "일회성")]
        [TestCase(600, "반격 기술에는")]
        public void Bleeding_DoesNotBypassExistingGroupRestrictions(int id, string restriction)
        {
            List<string[]> rows = Rows();
            Set(rows, id, Column.UseEffect, "상대에게 출혈 1 부여");
            IReadOnlyList<string> problems = Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems;
            Assert.That(problems.Any(problem => problem.Contains(restriction) && problem.Contains("사용 효과")), Is.True,
                string.Join("\n", problems));
        }

        private static int Boundary(LegacyStatusCollection statuses)
            => (int)typeof(LegacyStatusCollection).GetProperty("GrantSerial", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(statuses);
        private static void Invoke(LegacyStatusCollection statuses, string method, params object[] arguments)
            => typeof(LegacyStatusCollection).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(statuses, arguments);
        private static string Statuses(LegacyStatusCollection statuses)
            => string.Join("|", statuses.Statuses.Select(status => $"{status.Kind}:{status.Percent}:{status.RemainingCount}"));
        private static List<string[]> Rows()
            => CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table)).Select(record => record.Fields.ToArray()).ToList();
        private static void Set(List<string[]> rows, int id, string column, string value)
            => rows.Single(row => row[0] == id.ToString())[LegacySkillSheet.Headers.ToList().IndexOf(column)] = value;
        private static LegacySkillTable Parse(IEnumerable<string[]> rows)
            => LegacySkillSheet.Parse("bleeding tests", CsvTable.Write(rows));
        private static LegacySkillEffect Effect(string phrase)
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.UseEffect, phrase);
            return Parse(rows).Find(3).Effect;
        }
    }
}
