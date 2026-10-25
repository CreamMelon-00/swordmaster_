using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Pure view-model and keyword checks; no scene or Unity object is created.</summary>
    public sealed class SkillTempoInfoTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void FastSingleHit_HasADurationBadgeWithoutBeingDescribedAsMultihit(bool enemy)
        {
            SkillInfoContent content = SkillInfoContent.For(LegacySkillDefinitions.Skill(601), enemy);
            Assert.That(content.Main, Is.EqualTo("고속"));
            Assert.That(content.Secondary, Is.EqualTo("단타"));
            Assert.That(content.Description, Is.Empty);
            Assert.That(LegacySkillDefinitions.Skill(601).AttackCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TwoSlotCharge_NamesItsDurationAndExposedPreparation(bool enemy)
        {
            SkillInfoContent content = SkillInfoContent.For(LegacySkillDefinitions.Skill(602), enemy);
            Assert.That(content.Main, Is.EqualTo("차지 2칸"));
            Assert.That(content.Description, Is.Empty);
        }

        [Test]
        public void NormalSingleAndMultihitSkills_KeepTheirExistingBadges()
        {
            Assert.That(SkillInfoContent.For(LegacySkillDefinitions.Skill(1), false).Main, Is.EqualTo("ACT +1"));
            Assert.That(SkillInfoContent.For(LegacySkillDefinitions.Skill(2), false).Main, Is.EqualTo("분할 연타"));
        }

        [Test]
        public void TimingBadge_PreservesTheExistingEffectKeywordAndDescription()
        {
            LegacySkill original = LegacySkillDefinitions.Skill(42);
            SkillInfoContent originalContent = SkillInfoContent.For(original, false);
            var fast = new LegacySkill(original.Id, original.Name, original.Cost, original.MinPower, original.MaxPower,
                original.Kind, original.Property, original.AttackCount, original.LaneIndex, original.Description,
                original.AnimationName, original.IconId, durationHalfUnits: 1);
            SkillInfoContent timed = SkillInfoContent.For(fast, false);
            Assert.That(timed.Main, Is.EqualTo("고속"));
            Assert.That(timed.Secondary, Is.EqualTo(originalContent.Main));
            Assert.That(timed.Description, Does.Contain(originalContent.Description));
        }

        [TestCase("고속", "고속")]
        [TestCase("차지 2칸", "차지")]
        public void DurationBadges_ResolveToRegisteredHoverTerms(string badge, string expected)
        {
            MethodInfo lookup = typeof(SkillKeywordHover).GetMethod("BadgeTerm", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(lookup, Is.Not.Null);
            Assert.That(lookup.Invoke(null, new object[] { badge }), Is.EqualTo(expected));
            Assert.That(SkillKeywordHover.TryExplain(expected, out string description), Is.True);
            Assert.That(description, Is.Not.Empty);
        }

        [Test]
        public void FastExplanation_DistinguishesTwoTechniquesFromMultihitAndKeepsTheOrdinaryActionRunning()
        {
            Assert.That(SkillKeywordHover.TryExplain("고속", out string description), Is.True);
            Assert.That(description, Does.Contain("연타가 아닌 별개의 기술").And.Contain("끝까지 진행"));
        }

        [Test]
        public void ChargeExplanation_StatesDamageExposureAndDeathCancellation()
        {
            Assert.That(SkillKeywordHover.TryExplain("차지", out string description), Is.True);
            Assert.That(description, Does.Contain("마지막").And.Contain("상대의 공격을 받습니다").And.Contain("사망"));
        }
    }
}
