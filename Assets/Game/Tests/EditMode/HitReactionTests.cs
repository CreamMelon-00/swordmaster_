using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class HitReactionTests
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void NoIncomingDamage_NeverFlinches(bool targetAttacked, bool targetBroken)
        {
            Assert.That(LegacyHitReaction.ShouldHurt(targetAttacked, targetBroken, 0, 0, 0, 0), Is.False);
            Assert.That(LegacyHitReaction.ShouldHurt(targetAttacked, targetBroken, 0, 0, 10, 20), Is.False);
        }

        [TestCase(9, false)]
        [TestCase(10, true)]
        [TestCase(11, true)]
        public void ClashingAttacks_FlinchWhenDamageDealtIsAtMostHalfDamageTaken(int taken, bool hurt)
        {
            Assert.That(LegacyHitReaction.ShouldHurt(true, false, taken, 0, 5, 0), Is.EqualTo(hurt));
        }

        [TestCase(10, 0, 5, 0, true)]
        [TestCase(0, 10, 0, 5, true)]
        [TestCase(3, 7, 2, 3, true)]
        [TestCase(3, 6, 2, 3, false)]
        [TestCase(2, 8, 6, 0, false)]
        [TestCase(2, 8, 0, 6, false)]
        public void Comparison_CountsBothHealthAndResistance(int healthTaken, int resistanceTaken,
            int healthDealt, int resistanceDealt, bool hurt)
        {
            Assert.That(LegacyHitReaction.ShouldHurt(true, false, healthTaken, resistanceTaken,
                healthDealt, resistanceDealt), Is.EqualTo(hurt));
        }

        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(3, 7)]
        public void UnopposedHit_FlinchesForAnyIncomingDamage(int healthTaken, int resistanceTaken)
        {
            Assert.That(LegacyHitReaction.ShouldHurt(false, false, healthTaken, resistanceTaken, 0, 0), Is.True);
        }

        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(3, 7)]
        public void BrokenTarget_FlinchesOnDamageEvenWhenItsOwnAttackIsStronger(int healthTaken,
            int resistanceTaken)
        {
            Assert.That(LegacyHitReaction.ShouldHurt(true, true, healthTaken, resistanceTaken, 100, 100), Is.True);
        }

        [Test]
        public void SameExchange_OnlyTheOverwhelmedSideFlinches()
        {
            bool weaker = LegacyHitReaction.ShouldHurt(true, false, 4, 6, 2, 3);
            bool stronger = LegacyHitReaction.ShouldHurt(true, false, 2, 3, 4, 6);
            Assert.That(weaker, Is.True);
            Assert.That(stronger, Is.False);
        }

        [TestCase(5, 5)]
        [TestCase(5, 6)]
        [TestCase(6, 5)]
        public void SimilarAttacks_DoNotProduceMutualFlinches(int leftDamage, int rightDamage)
        {
            Assert.That(LegacyHitReaction.ShouldHurt(true, false, rightDamage, 0, leftDamage, 0), Is.False);
            Assert.That(LegacyHitReaction.ShouldHurt(true, false, leftDamage, 0, rightDamage, 0), Is.False);
        }

        [Test]
        public void MultiHitExchange_UsesEachHitsDamageIndependently()
        {
            Assert.That(LegacyHitReaction.ShouldHurt(true, false, 10, 0, 5, 0), Is.True);
            Assert.That(LegacyHitReaction.ShouldHurt(true, false, 2, 0, 5, 0), Is.False);
            Assert.That(LegacyHitReaction.ShouldHurt(true, false, 2, 0, 0, 0), Is.True);
        }

        [TestCase(int.MaxValue, int.MaxValue, int.MaxValue, 0, true)]
        [TestCase(int.MaxValue, int.MaxValue - 1, int.MaxValue, 0, false)]
        [TestCase(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, false)]
        [TestCase(int.MaxValue, 0, int.MaxValue, int.MaxValue, false)]
        public void LargeDamage_DoesNotOverflow(int healthTaken, int resistanceTaken, int healthDealt,
            int resistanceDealt, bool hurt)
        {
            Assert.That(LegacyHitReaction.ShouldHurt(true, false, healthTaken, resistanceTaken,
                healthDealt, resistanceDealt), Is.EqualTo(hurt));
        }

        [TestCase(-100, 10, 0, 5, true)]
        [TestCase(10, -100, 5, 0, true)]
        [TestCase(10, 0, -100, 5, true)]
        [TestCase(10, 0, 5, -100, true)]
        [TestCase(-1, -1, 0, 0, false)]
        [TestCase(0, 0, -1, -1, false)]
        public void NegativeComponents_AreIgnoredIndividually(int healthTaken, int resistanceTaken,
            int healthDealt, int resistanceDealt, bool hurt)
        {
            Assert.That(LegacyHitReaction.ShouldHurt(true, false, healthTaken, resistanceTaken,
                healthDealt, resistanceDealt), Is.EqualTo(hurt));
        }
    }
}
