using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class TensionStatusAndSheetTests
    {
        [Test]
        public void Tension_AppendsItsEnumValue_AndKeepsFixedReductionWithCombinedAttackCounts()
        {
            Assert.That((int)LegacyStatusKind.PowerUp, Is.EqualTo(0));
            Assert.That((int)LegacyStatusKind.Protection, Is.EqualTo(1));
            Assert.That((int)LegacyStatusKind.Vulnerability, Is.EqualTo(2));
            Assert.That((int)LegacyStatusKind.Bleeding, Is.EqualTo(3));
            Assert.That((int)LegacyStatusKind.Jealousy, Is.EqualTo(4));
            Assert.That((int)LegacyStatusKind.Fixed, Is.EqualTo(5));
            Assert.That((int)LegacyStatusKind.Tension, Is.EqualTo(6));
            var statuses = new LegacyStatusCollection();
            statuses.Grant(LegacyStatusKind.Tension, 50, 2);
            statuses.GrantTension(3);
            Assert.That(statuses.TensionCount, Is.EqualTo(5));
            LegacyStatusSnapshot tension = statuses.Statuses.Single();
            Assert.That(tension.Kind, Is.EqualTo(LegacyStatusKind.Tension));
            Assert.That(tension.Percent, Is.EqualTo(1), "Fixed execution-hit reduction, never an intensity percentage.");
            Assert.That(tension.RemainingCount, Is.EqualTo(5));
            Assert.That(statuses.PowerPercent, Is.Zero);
            Assert.That(statuses.ProtectionPercent, Is.Zero);
            Assert.That(statuses.ReceivedMultiplier, Is.EqualTo(1d));
        }

        [Test]
        public void Tension_IsExcludedFromOrdinaryPowerAndIncomingAttackConsumption()
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantTension(3);
            statuses.GrantBleeding(2);
            statuses.Grant(LegacyStatusKind.PowerUp, 50, 2);
            statuses.Grant(LegacyStatusKind.Protection, 50, 1);
            statuses.Grant(LegacyStatusKind.Vulnerability, 50, 1);
            Invoke(statuses, "ConsumeTechnique");
            Invoke(statuses, "ConsumeIncomingAttack", Boundary(statuses));
            Assert.That(Statuses(statuses), Is.EqualTo("Tension:1:3|Bleeding:2:2|PowerUp:50:1"));
            statuses.ConsumeTensionAttack(Boundary(statuses));
            Assert.That(Statuses(statuses), Is.EqualTo("Tension:1:2|Bleeding:2:2|PowerUp:50:1"));
            statuses.ConsumeBleedingAttack(Boundary(statuses));
            Assert.That(Statuses(statuses), Is.EqualTo("Tension:1:2|Bleeding:2:1|PowerUp:50:1"));
        }

        [Test]
        public void FreshTension_CannotActivateOrBeConsumedByAnAlreadyStartedAttack()
        {
            var statuses = new LegacyStatusCollection();
            int boundary = Boundary(statuses);
            statuses.GrantTension(2);
            Assert.That(statuses.TensionActiveThrough(boundary), Is.False);
            statuses.ConsumeTensionAttack(boundary);
            Assert.That(statuses.TensionCount, Is.EqualTo(2));
            int nextBoundary = Boundary(statuses);
            Assert.That(statuses.TensionActiveThrough(nextBoundary), Is.True);
            statuses.ConsumeTensionAttack(nextBoundary);
            Assert.That(statuses.TensionCount, Is.EqualTo(1));
            statuses.ConsumeTensionAttack(nextBoundary);
            Assert.That(statuses.TensionCount, Is.Zero);
            Assert.That(statuses.TensionActiveThrough(nextBoundary), Is.False);
        }

        [Test]
        public void TensionRegrant_AddsOnlyDuration_AndKeepsFreshCountsAfterAnOldAttack()
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantTension(1);
            int boundary = Boundary(statuses);
            statuses.GrantTension(2);
            Assert.That(statuses.TensionActiveThrough(boundary), Is.True);
            Assert.That(statuses.TensionCount, Is.EqualTo(3));
            statuses.ConsumeTensionAttack(boundary);
            Assert.That(statuses.TensionCount, Is.EqualTo(2));
            Assert.That(statuses.Statuses.Count, Is.EqualTo(1));
        }

        [Test]
        public void FullTensionCleanse_SuppressesTheOldEffect_AndANewGrantStillWaits()
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantTension(2);
            statuses.GrantBleeding(3);
            int boundary = Boundary(statuses);
            Assert.That(statuses.ReduceTension(4), Is.EqualTo(2));
            Assert.That(statuses.TensionActiveThrough(boundary), Is.False);
            statuses.GrantTension(3);
            Assert.That(statuses.TensionActiveThrough(boundary), Is.False);
            statuses.ConsumeTensionAttack(boundary);
            Assert.That(Statuses(statuses), Is.EqualTo("Bleeding:2:3|Tension:1:3"));
            Assert.That(statuses.TensionActiveThrough(Boundary(statuses)), Is.True);
        }

        [Test]
        public void PartialTensionCleanse_PreservesOldActivation_AndOtherStatusKinds()
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantTension(4);
            statuses.GrantBleeding(3);
            statuses.Grant(LegacyStatusKind.PowerUp, 50, 2);
            statuses.Grant(LegacyStatusKind.Protection, 50, 2);
            statuses.Grant(LegacyStatusKind.Vulnerability, 50, 2);
            int boundary = Boundary(statuses);
            Assert.That(statuses.ReduceTension(2), Is.EqualTo(2));
            Assert.That(statuses.TensionActiveThrough(boundary), Is.True);
            statuses.ConsumeTensionAttack(boundary);
            Assert.That(Statuses(statuses), Is.EqualTo("Tension:1:1|Bleeding:2:3|PowerUp:50:2|Protection:50:2|Vulnerability:50:2"));
            statuses.ConsumeProtection();
            Assert.That(statuses.TensionCount, Is.EqualTo(1));
            Assert.That(statuses.BleedingCount, Is.EqualTo(3));
        }

        [Test]
        public void TensionCopy_IsIndependentAndPreservesTheActivationBoundary_AndClearRemovesIt()
        {
            var original = new LegacyStatusCollection();
            original.GrantTension(3);
            int boundary = Boundary(original);
            LegacyStatusCollection copy = original.Copy();
            Assert.That(copy.TensionActiveThrough(boundary), Is.True);
            copy.ReduceTension(1);
            copy.ConsumeTensionAttack(boundary);
            Assert.That(copy.TensionCount, Is.EqualTo(1));
            Assert.That(original.TensionCount, Is.EqualTo(3));
            copy.Clear();
            Assert.That(copy.TensionCount, Is.Zero);
            Assert.That(copy.Statuses, Is.Empty);
            Assert.That(copy.TensionActiveThrough(int.MaxValue), Is.False);
            Assert.That(original.TensionCount, Is.EqualTo(3));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveTensionChanges_DoNotGrantOrReduceAnything(int count)
        {
            var statuses = new LegacyStatusCollection();
            statuses.GrantTension(count);
            Assert.That(statuses.Statuses, Is.Empty);
            statuses.GrantTension(3);
            statuses.GrantTension(count);
            Assert.That(statuses.ReduceTension(count), Is.Zero);
            Assert.That(statuses.TensionCount, Is.EqualTo(3));
        }

        [Test]
        public void TensionText_ExplainsHitReductionWithoutRedistributingPower_AndAttackBasedDuration()
        {
            Assert.That(LegacyStatusNames.Name(LegacyStatusKind.Tension), Is.EqualTo("긴장"));
            Assert.That(LegacyStatusNames.Label(LegacyStatusKind.Tension, 1, 3), Is.EqualTo("긴장 3"));
            string text = LegacyStatusNames.Description(LegacyStatusKind.Tension, 1);
            Assert.That(text, Does.Contain("원래 2타 이상"));
            Assert.That(text, Does.Contain("실행 타수가 1 줄어"));
            Assert.That(text, Does.Contain("타격당 위력은 유지"));
            Assert.That(text, Does.Contain("단타와 반격도 1회"));
            Assert.That(text, Does.Contain("방어·숨고르기·빈 칸·취소된 기술은 소모하지 않습니다"));
            Assert.That(text, Does.Contain("이미 시작한 공격에 적용하지 않습니다"));
            Assert.That(text, Does.Contain("횟수만 더하며 턴이 지나도 남습니다"));
            Assert.That(text, Does.Not.Contain("%"));
            Assert.That(LegacyStatusNames.Name(LegacyStatusKind.Jealousy), Is.EqualTo("질투"));
            Assert.That(LegacyStatusNames.Name(LegacyStatusKind.Fixed), Is.EqualTo("고정"));
        }

        [Test]
        public void TensionCompoundPhrases_RoundTripEveryField_AndKeepBleedingAndOtherColumnEffects()
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.ActGain, "1");
            Set(rows, 3, Column.UseEffect, "상대 긴장 1 이상 기본 위력 +10; 자기 긴장 1 이상 기본 위력 +8; " +
                "자신의 긴장 2 감소; 상대에게 긴장 1 부여; 자신에게 긴장 2 부여; 자신에게 출혈 2 부여");
            LegacySkillTable parsed = Parse(rows);
            LegacySkillDefinition definition = parsed.Find(3);
            LegacySkillEffect effect = definition.Effect;
            Assert.That(effect.SelfTensionCount, Is.EqualTo(2));
            Assert.That(effect.OpponentTensionCount, Is.EqualTo(1));
            Assert.That(effect.TensionReduction, Is.EqualTo(2));
            Assert.That(effect.SelfTensionRequired, Is.EqualTo(1));
            Assert.That(effect.SelfTensionPowerBonus, Is.EqualTo(8));
            Assert.That(effect.OpponentTensionRequired, Is.EqualTo(1));
            Assert.That(effect.OpponentTensionPowerBonus, Is.EqualTo(10));
            Assert.That(effect.ActGain, Is.EqualTo(1));
            Assert.That(effect.SelfBleedingCount, Is.EqualTo(2));
            Assert.That(effect.HasUseEffect && effect.HasTensionEffect, Is.True);
            Assert.That(effect.Roles, Is.EqualTo(LegacySkillRole.ActRecovery | LegacySkillRole.Bleeding |
                LegacySkillRole.Tension | LegacySkillRole.TensionReduction | LegacySkillRole.TensionPower));
            Assert.That(LegacySkillLabels.UseEffect(effect), Is.EqualTo("자신에게 출혈 2 부여; 자신에게 긴장 2 부여; " +
                "상대에게 긴장 1 부여; 자신의 긴장 2 감소; 자기 긴장 1 이상 기본 위력 +8; 상대 긴장 1 이상 기본 위력 +10"));
            LegacySkillTable reread = LegacySkillSheet.Parse("tension round trip", LegacySkillSheet.Write(parsed));
            Assert.That(LegacySkillSheetTests.Dump(reread.Find(3)), Is.EqualTo(LegacySkillSheetTests.Dump(definition)));
            Assert.That(LegacySkillSheet.Headers.Count, Is.EqualTo(48), "The existing tempo column and all sheet columns remain.");
        }

        [TestCase(1, 1)]
        [TestCase(20, 50)]
        public void TensionPhrases_AcceptCountAndBonusBoundsAndOptionalSpacing(int count, int bonus)
        {
            LegacySkillEffect effect = Effect($"자신에게긴장{count}부여;상대에게긴장{count}부여;자신의긴장{count}감소;" +
                $"자기긴장{count}이상기본위력+{bonus};상대긴장{count}이상기본위력+{bonus}");
            Assert.That(effect.SelfTensionCount, Is.EqualTo(count));
            Assert.That(effect.OpponentTensionCount, Is.EqualTo(count));
            Assert.That(effect.TensionReduction, Is.EqualTo(count));
            Assert.That(effect.TensionPowerBonus(count, count), Is.EqualTo(bonus * 2));
            Assert.That(LegacySkillLabels.UseEffect(Effect(LegacySkillLabels.UseEffect(effect))), Is.EqualTo(LegacySkillLabels.UseEffect(effect)));
        }

        [TestCase("자신에게 긴장 0 부여")]
        [TestCase("자신에게 긴장 21 부여")]
        [TestCase("상대에게 긴장 0 부여")]
        [TestCase("상대에게 긴장 21 부여")]
        [TestCase("상대에게 긴장 -1 부여")]
        [TestCase("상대에게 긴장 1.5 부여")]
        [TestCase("상대에게 긴장 １ 부여")]
        [TestCase("자신의 긴장 0 감소")]
        [TestCase("자신의 긴장 21 감소")]
        [TestCase("자기 긴장 0 이상 기본 위력 +8")]
        [TestCase("자기 긴장 21 이상 기본 위력 +8")]
        [TestCase("상대 긴장 0 이상 기본 위력 +10")]
        [TestCase("상대 긴장 21 이상 기본 위력 +10")]
        [TestCase("자기 긴장 1 이상 기본 위력 +0")]
        [TestCase("상대 긴장 1 이상 기본 위력 +51")]
        [TestCase("자기 긴장 1 초과 기본 위력 +8")]
        [TestCase("자신에게 긴장 2 부여;")]
        [TestCase(";상대에게 긴장 1 부여")]
        [TestCase("자신에게 긴장 2 부여;;상대에게 긴장 1 부여")]
        [TestCase("자신에게 긴장 2 부여, 상대에게 긴장 1 부여")]
        [TestCase("자신에게 긴장 2 부여; 자신에게 긴장 3 부여")]
        [TestCase("상대에게 긴장 1 부여; 상대에게 긴장 2 부여")]
        [TestCase("자신의 긴장 2 감소; 자신의 긴장 1 감소")]
        [TestCase("자기 긴장 1 이상 기본 위력 +8; 자기 긴장 2 이상 기본 위력 +10")]
        [TestCase("상대 긴장 1 이상 기본 위력 +10; 상대 긴장 2 이상 기본 위력 +8")]
        public void TensionPhrases_RejectInvalidBoundsGrammarAndDuplicateCategories(string phrase)
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.UseEffect, phrase);
            IReadOnlyList<string> problems = Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems;
            Assert.That(problems.Any(problem => problem.Contains("'사용 효과':") && problem.Contains("쓸 수 없습니다")), Is.True,
                string.Join("\n", problems));
        }

        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 8)]
        [TestCase(0, 1, 10)]
        [TestCase(1, 1, 18)]
        [TestCase(20, 20, 18)]
        public void TensionPower_UsesInclusiveIndependentThresholds(int self, int opponent, int expected)
        {
            LegacySkillEffect effect = Effect("자기 긴장 1 이상 기본 위력 +8; 상대 긴장 1 이상 기본 위력 +10");
            Assert.That(effect.TensionPowerBonus(self, opponent), Is.EqualTo(expected));
        }

        [Test]
        public void Guard_MayGrantAndCleanseTension()
        {
            List<string[]> rows = Rows();
            Set(rows, 7, Column.UseEffect, "자신에게 긴장 2 부여; 상대에게 긴장 1 부여; 자신의 긴장 2 감소");
            LegacySkillEffect effect = Parse(rows).Find(7).Effect;
            Assert.That(effect.SelfTensionCount, Is.EqualTo(2));
            Assert.That(effect.OpponentTensionCount, Is.EqualTo(1));
            Assert.That(effect.TensionReduction, Is.EqualTo(2));
            Assert.That(effect.HasTensionGrant && effect.HasTensionReduction, Is.True);
            Assert.That(effect.HasTensionPower, Is.False);
        }

        [TestCase("자기 긴장 1 이상 기본 위력 +8")]
        [TestCase("상대 긴장 1 이상 기본 위력 +10")]
        public void Guard_CannotUseAnAttackBasePowerBonus(string phrase)
        {
            List<string[]> rows = Rows();
            Set(rows, 7, Column.UseEffect, phrase);
            IReadOnlyList<string> problems = Assert.Throws<SkillSheetException>(() => Parse(rows)).Problems;
            Assert.That(problems.Any(problem => problem.Contains("공격 기술에만")), Is.True);
        }

        [TestCase(500, "적")]
        [TestCase(59, "일회성")]
        [TestCase(600, "반격 기술에는")]
        public void Tension_DoesNotBypassExistingEffectGroupRestrictions(int id, string restriction)
        {
            List<string[]> rows = Rows();
            Set(rows, id, Column.UseEffect, "상대에게 긴장 1 부여");
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
            => LegacySkillSheet.Parse("tension tests", CsvTable.Write(rows));
        private static LegacySkillEffect Effect(string phrase)
        {
            List<string[]> rows = Rows();
            Set(rows, 3, Column.UseEffect, phrase);
            return Parse(rows).Find(3).Effect;
        }
    }
}
