using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class MultiHitEmphasisTests
    {
        [Test]
        public void ShippedMultiHitAttacks_LeaveTheirOwnLastStrikeAtFullWeight()
        {
            int techniques = 0;
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                LegacySkill skill = definition.Skill;
                if (skill.Kind != LegacySkillKind.Attack || skill.AttackCount <= 1) continue;
                techniques++;
                for (int hit = 0; hit < skill.AttackCount - 1; hit++)
                    Assert.That(LegacyMultiHitEmphasis.Strength(skill, hit, false),
                        Is.EqualTo(LegacyMultiHitEmphasis.DefaultMiddleStrength), skill.Name);
                Assert.That(LegacyMultiHitEmphasis.Strength(skill, skill.AttackCount - 1, false),
                    Is.EqualTo(1f), skill.Name);
            }
            Assert.That(techniques, Is.GreaterThan(0), "The actual sheet must exercise multi-hit techniques.");
        }

        [Test]
        public void UnequalHitCounts_EmphasiseEachFightersOwnFinish()
        {
            LegacySkill shortAttack = Attack(2);
            LegacySkill longAttack = Attack(4);
            Assert.That(LegacyMultiHitEmphasis.Strength(shortAttack, 0, false), Is.LessThan(1f));
            Assert.That(LegacyMultiHitEmphasis.Strength(longAttack, 0, false), Is.LessThan(1f));
            Assert.That(LegacyMultiHitEmphasis.Strength(shortAttack, 1, false), Is.EqualTo(1f));
            Assert.That(LegacyMultiHitEmphasis.Strength(longAttack, 1, false), Is.LessThan(1f),
                "The other fighter finishing must not promote this technique's middle contact.");
            Assert.That(LegacyMultiHitEmphasis.Strength(longAttack, 3, false), Is.EqualTo(1f));
        }

        [Test]
        public void BreakHeavyOrFinishingStrike_KeepsFullWeightBeforeTheAuthoredFinish()
        {
            LegacySkill attack = Attack(4);
            for (int hit = 0; hit < attack.AttackCount; hit++)
                Assert.That(LegacyMultiHitEmphasis.Strength(attack, hit, true, middleStrength: 0f),
                    Is.EqualTo(1f), "A decisive contact already has a dedicated presentation.");
        }

        [Test]
        public void SingleHitsGuardsWaitsAndUnattributedDamage_KeepExistingPresentation()
        {
            foreach (LegacySkill skill in new[] { null, Attack(1), LegacyCommonActions.Breathe,
                new LegacySkill(990002, "수비 검증", 0, 1, 1, LegacySkillKind.Defence,
                    LegacySkillProperty.Defence, 3, 0, string.Empty) })
                Assert.That(LegacyMultiHitEmphasis.Strength(skill, 0, false, middleStrength: 0f), Is.EqualTo(1f));
        }

        [Test]
        public void NewTechniquesAndAllAttackProperties_NeedNoPerTechniqueRegistration()
        {
            foreach (LegacySkillProperty property in new[] {
                LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
            {
                var attack = new LegacySkill(-990001, "공통 연타 검증", 0, 10, 10,
                    LegacySkillKind.Attack, property, 2, 0, string.Empty);
                Assert.That(LegacyMultiHitEmphasis.Strength(attack, 0, false), Is.LessThan(1f));
                Assert.That(LegacyMultiHitEmphasis.Strength(attack, 1, false), Is.EqualTo(1f));
            }
        }

        [Test]
        public void DisabledAndInvalidContacts_DoNotReduceUnrelatedFeedback()
        {
            LegacySkill attack = Attack(3);
            Assert.That(LegacyMultiHitEmphasis.Strength(attack, 0, false, enabled: false), Is.EqualTo(1f));
            foreach (int hit in new[] { int.MinValue, -1, 3, int.MaxValue })
                Assert.That(LegacyMultiHitEmphasis.Strength(attack, hit, false), Is.EqualTo(1f));
        }

        [TestCase(float.NaN, LegacyMultiHitEmphasis.DefaultMiddleStrength)]
        [TestCase(float.PositiveInfinity, LegacyMultiHitEmphasis.DefaultMiddleStrength)]
        [TestCase(float.NegativeInfinity, LegacyMultiHitEmphasis.DefaultMiddleStrength)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(.8f, .8f)]
        [TestCase(2f, 1f)]
        public void MiddleStrength_RemainsFiniteAndNeverExceedsExistingWeight(float configured, float expected)
            => Assert.That(LegacyMultiHitEmphasis.Strength(Attack(3), 0, false, middleStrength: configured),
                Is.EqualTo(expected));

        private static LegacySkill Attack(int count) => new LegacySkill(990001, "연타 검증", 0, 10, 10,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, count, 0, string.Empty);
    }
}
