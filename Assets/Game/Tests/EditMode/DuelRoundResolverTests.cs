using System;
using NUnit.Framework;
using TurnLimbo.Core.Combat;

namespace TurnLimbo.Core.Tests
{
    public sealed class DuelRoundResolverTests
    {
        private readonly DuelRoundResolver resolver = new DuelRoundResolver();

        [Test]
        public void AttackAgainstWait_DamagesOpponent()
        {
            RoundResolution result = resolver.Resolve(
                HealthyDuelist(),
                CombatMove.Attack("slash", DamageType.Slash, 4),
                HealthyDuelist(),
                CombatMove.Wait());

            Assert.That(result.LeftAfter.Health, Is.EqualTo(10));
            Assert.That(result.RightAfter.Health, Is.EqualTo(6));
            Assert.That(result.DamageTakenByRight, Is.EqualTo(4));
        }

        [Test]
        public void Guard_ReducesIncomingDamageAndDoesNotDealDamage()
        {
            RoundResolution result = resolver.Resolve(
                HealthyDuelist(),
                CombatMove.Attack("thrust", DamageType.Thrust, 7),
                HealthyDuelist(),
                CombatMove.Guard("parry", 5));

            Assert.That(result.LeftAfter.Health, Is.EqualTo(10));
            Assert.That(result.RightAfter.Health, Is.EqualTo(8));
            Assert.That(result.DamageTakenByRight, Is.EqualTo(2));
        }

        [Test]
        public void SimultaneousAttacks_AreResolvedFromStartingState()
        {
            RoundResolution result = resolver.Resolve(
                HealthyDuelist(),
                CombatMove.Attack("left-slash", DamageType.Slash, 3),
                HealthyDuelist(),
                CombatMove.Attack("right-strike", DamageType.Strike, 6));

            Assert.That(result.LeftAfter.Health, Is.EqualTo(4));
            Assert.That(result.RightAfter.Health, Is.EqualTo(7));
        }

        [Test]
        public void SimultaneousLethalAttacks_CanEndInDraw()
        {
            RoundResolution result = resolver.Resolve(
                new DuelistState(10, 3),
                CombatMove.Attack("left-slash", DamageType.Slash, 4),
                new DuelistState(10, 4),
                CombatMove.Attack("right-thrust", DamageType.Thrust, 3));

            Assert.That(result.IsFinished, Is.True);
            Assert.That(result.IsDraw, Is.True);
        }

        [Test]
        public void Resolve_DoesNotMutateStartingState()
        {
            DuelistState left = HealthyDuelist();
            DuelistState right = HealthyDuelist();

            resolver.Resolve(
                left,
                CombatMove.Attack("slash", DamageType.Slash, 4),
                right,
                CombatMove.Wait());

            Assert.That(left.Health, Is.EqualTo(10));
            Assert.That(right.Health, Is.EqualTo(10));
        }

        [Test]
        public void InvalidMoveData_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => CombatMove.Attack("", DamageType.Slash, 1));
            Assert.Throws<ArgumentException>(() => CombatMove.Attack("invalid", DamageType.None, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CombatMove.Guard("invalid", 0));
        }

        private static DuelistState HealthyDuelist()
        {
            return new DuelistState(10, 10);
        }
    }
}
