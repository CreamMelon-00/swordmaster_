using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Pure information contracts for the prototype; no Unity objects or native resources are created.</summary>
    public sealed class PowerBreakSkillInfoTests
    {
        private static readonly HashSet<int> RetiredIds = new HashSet<int> { 5, 6, 19, 42, 48, 51, 52, 500, 501 };

        [TestCase(5, false)]
        [TestCase(6, false)]
        [TestCase(19, false)]
        [TestCase(42, false)]
        [TestCase(48, false)]
        [TestCase(51, false)]
        [TestCase(52, false)]
        [TestCase(500, false)]
        [TestCase(501, false)]
        [TestCase(5, true)]
        [TestCase(6, true)]
        [TestCase(19, true)]
        [TestCase(42, true)]
        [TestCase(48, true)]
        [TestCase(51, true)]
        [TestCase(52, true)]
        [TestCase(500, true)]
        [TestCase(501, true)]
        public void PrototypeRetiresTheNineOldEffects_WhileRawCopyRemainsAvailable(int id, bool enemy)
        {
            LegacySkill skill = LegacySkillDefinitions.Skill(id);
            SkillInfoContent raw = SkillInfoContent.For(skill, enemy);
            SkillInfoContent prototype = SkillInfoContent.ForPrototype(skill, enemy);
            string oldCopy = Copy(raw), newCopy = Copy(prototype);
            Assert.That(oldCopy.Contains("저항") || oldCopy.Contains("붕괴"), Is.True, "The legacy explanation must remain authored.");
            Assert.That(newCopy, Does.Not.Contain("저항"));
            Assert.That(newCopy, Does.Not.Contain("붕괴"), "An old direct-break effect cannot promise a free prototype break.");
            Assert.That(CampaignSkillText.Effect(skill), Does.Not.Contain("저항").And.Not.Contain("붕괴"));
            Assert.That(Copy(SkillInfoContent.For(skill, enemy)), Is.EqualTo(oldCopy), "Reading the prototype cannot rewrite the legacy view model.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RetiringBattemensResistanceEffect_KeepsItsRealCyclePowerAndCap(bool enemy)
        {
            LegacySkill skill = LegacySkillDefinitions.Skill(52);
            SkillInfoContent content = SkillInfoContent.ForPrototype(skill, enemy);
            Assert.That(content.Main, Is.EqualTo("순환"));
            Assert.That(content.Description, Does.Contain("기본 위력 +2").And.Contain("최대 2회"));
            Assert.That(content.Description, Does.Not.Contain("저항"));
            Assert.That((skill.Cost, skill.MinPower, skill.MaxPower, skill.AttackCount), Is.EqualTo((1, 2, 4, 1)));
            Assert.That(CampaignSkillText.Power(skill), Is.EqualTo("2–4"));
        }

        [Test]
        public void EveryUnaffectedSkill_KeepsItsExistingKeywordsSymbolsTonesAndDescription()
        {
            int inspected = 0;
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                if (RetiredIds.Contains(definition.Skill.Id)) continue;
                foreach (bool enemy in new[] { false, true })
                {
                    SkillInfoContent before = SkillInfoContent.For(definition.Skill, enemy);
                    SkillInfoContent after = SkillInfoContent.ForPrototype(definition.Skill, enemy);
                    Assert.That((after.Main, after.Secondary, after.Description, after.MainSymbol, after.SecondarySymbol,
                            after.MainTone, after.SecondaryTone),
                        Is.EqualTo((before.Main, before.Secondary, before.Description, before.MainSymbol, before.SecondarySymbol,
                            before.MainTone, before.SecondaryTone)), definition.Skill.Id + " enemy=" + enemy);
                    inspected++;
                }
            }
            Assert.That(inspected, Is.GreaterThan(80));
        }

        [Test]
        public void InspectingEveryPrototypeCard_DoesNotRewriteTheAuthoredSheetOrAnySkillNumbers()
        {
            string before = LegacySkillSheet.Write(LegacySkillDefinitions.Table);
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                SkillInfoContent.ForPrototype(definition.Skill, false);
                SkillInfoContent.ForPrototype(definition.Skill, true);
                CampaignSkillText.Effect(definition.Skill);
                CampaignSkillText.Power(definition.Skill);
                CampaignSkillText.DamageHint(definition.Skill);
            }
            Assert.That(LegacySkillSheet.Write(LegacySkillDefinitions.Table), Is.EqualTo(before));
            Assert.That(LegacySkillDefinitions.Find(5).Effect.OpponentResistanceReduction, Is.EqualTo(10));
            Assert.That(LegacySkillDefinitions.Find(500).Effect.BreaksOpponent, Is.True,
                "Reverting the experiment must not require recovering overwritten sheet data.");
        }

        [Test]
        public void BreakTooltip_ExplainsTheActualThresholdOneDestroyedSkillGapAndDefenceImmunity()
        {
            Assert.That(SkillKeywordHover.TryExplain("붕괴", out string explanation), Is.True);
            Assert.That(explanation, Does.Contain("공격끼리").And.Contain("최종 위력 차이").And.Contain("5 이상"));
            Assert.That(explanation, Does.Contain("다음 기술 하나").And.Contain("파괴된 자리만").And.Contain("2배"));
            Assert.That(explanation, Does.Contain("방어 기술은 붕괴당하지 않습니다"));
            Assert.That(explanation, Does.Not.Contain("저항").And.Not.Contain("회복되기 전까지"));
        }

        [Test]
        public void LobbyAttackHint_NamesPowerBreakAndTheDestroyedGap_InsteadOfResistanceRouting()
        {
            string attack = CampaignSkillText.DamageHint(LegacySkillDefinitions.Skill(1));
            Assert.That(attack, Does.Contain("위력 차이 5").And.Contain("다음 기술").And.Contain("빈칸").And.Contain("2배"));
            Assert.That(attack, Does.Not.Contain("저항"));
            Assert.That(CampaignSkillText.DamageHint(LegacySkillDefinitions.Skill(8)), Is.Empty);
        }

        [TestCase("고속", "별개의 기술")]
        [TestCase("차지", "피해를 받아도 동작을 이어가")]
        [TestCase("반격", "내 칸이 비었을 때")]
        [TestCase("출혈", "공격")]
        [TestCase("질투", "타격")]
        [TestCase("긴장", "실행 타수가 1 줄어듭니다")]
        public void OtherSharedKeywords_KeepTheirEstablishedMeaning(string term, string meaning)
        {
            Assert.That(SkillKeywordHover.TryExplain(term, out string explanation), Is.True);
            Assert.That(explanation, Does.Contain(meaning));
        }

        private static string Copy(SkillInfoContent content)
            => content.Main + "\n" + content.Secondary + "\n" + content.Description;
    }
}
