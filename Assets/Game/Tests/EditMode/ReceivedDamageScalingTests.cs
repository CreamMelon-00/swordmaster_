using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>Incoming protection/vulnerability applies once before resistance is subtracted. The health rules then
    /// receive an already scaled overflow; they still own broken damage, pressure and the attacker's extra multipliers.</summary>
    public sealed class ReceivedDamageScalingTests
    {
        private static readonly ConstructorInfo FighterConstructor = typeof(LegacyFighterState).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(int), typeof(int) }, null);
        private static readonly MethodInfo ReceiveHit = typeof(LegacyFighterState).GetMethod("ReceiveHit",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [TestCase(20, 10, 1.5, 10, 20, LegacyDamageReason.None)]
        [TestCase(20, 5, .5, 5, 5, LegacyDamageReason.ProtectionReduction)]
        [TestCase(20, 10, 1, 10, 10, LegacyDamageReason.None)]
        [TestCase(20, 50, 1.5, 30, 0, LegacyDamageReason.None)]
        [TestCase(20, 50, .5, 10, 0, LegacyDamageReason.ProtectionReduction)]
        [TestCase(20, 10, 0, 0, 0, LegacyDamageReason.ProtectionReduction)]
        public void AttackClash_ScalesOnceBeforeResistance_AndDoesNotDoubleTheBreakingHit(int power, int resistance,
            double multiplier, int expectedResistanceDamage, int expectedHealthDamage, LegacyDamageReason reasons)
        {
            LegacyFighterState fighter = Fighter(resistance);
            Impact hit = Hit(fighter, power, true, multiplier);
            Assert.That(resistance - fighter.Resistance, Is.EqualTo(expectedResistanceDamage));
            Assert.That(1000 - fighter.Health, Is.EqualTo(expectedHealthDamage));
            Assert.That(hit.Displayed, Is.EqualTo(expectedHealthDamage > 0 ? expectedHealthDamage : expectedResistanceDamage));
            Assert.That(hit.Push, Is.EqualTo(hit.Displayed));
            Assert.That(hit.Reasons, Is.EqualTo(reasons));
        }

        [TestCase(1, 1, 1.5, 1)]
        [TestCase(5, 3, 1.5, 5)]
        [TestCase(5, 1, .5, 1)]
        [TestCase(3, 2, .5, 0)]
        public void BreakingOverflow_DoesNotRoundTheIncomingModifierAgain(int power, int resistance,
            double multiplier, int expectedHealthDamage)
        {
            LegacyFighterState fighter = Fighter(resistance);
            Impact hit = Hit(fighter, power, true, multiplier);
            Assert.That(fighter.Resistance, Is.Zero);
            Assert.That(1000 - fighter.Health, Is.EqualTo(expectedHealthDamage));
            Assert.That(hit.Displayed, Is.EqualTo(expectedHealthDamage));
            Assert.That(hit.Push, Is.EqualTo(expectedHealthDamage));
            Assert.That(hit.Reasons, Is.EqualTo(multiplier < 1 ? LegacyDamageReason.ProtectionReduction : LegacyDamageReason.None));
        }

        [TestCase(1.5, 60, 30, LegacyDamageReason.BrokenDouble)]
        [TestCase(.5, 20, 10, LegacyDamageReason.ProtectionReduction | LegacyDamageReason.BrokenDouble)]
        [TestCase(1, 40, 20, LegacyDamageReason.BrokenDouble)]
        [TestCase(0, 0, 0, LegacyDamageReason.ProtectionReduction)]
        public void AlreadyBroken_ReceivesTheSameScaledDamageThroughAttackClashAndDirectHealthRoutes(double multiplier,
            int expectedDamage, int expectedPush, LegacyDamageReason reasons)
        {
            foreach (bool resistanceHit in new[] { true, false })
            {
                LegacyFighterState fighter = Fighter(0);
                Impact hit = Hit(fighter, 20, resistanceHit, multiplier);
                Assert.That(1000 - fighter.Health, Is.EqualTo(expectedDamage), "resistance hit: " + resistanceHit);
                Assert.That(hit.Displayed, Is.EqualTo(expectedDamage));
                Assert.That(hit.Push, Is.EqualTo(expectedPush));
                Assert.That(hit.Reasons, Is.EqualTo(reasons));
            }
        }

        [TestCase(1.5, 5, 10, 30, LegacyDamageReason.None)]
        [TestCase(.5, 3, 2, 10, LegacyDamageReason.ProtectionReduction)]
        public void Multihit_BreakingHitAndFollowupEachApplyTheIncomingModifierOnce(double multiplier, int resistance,
            int breakingDamage, int followupDamage, LegacyDamageReason firstReasons)
        {
            LegacyFighterState fighter = Fighter(resistance);
            Impact first = Hit(fighter, 10, true, multiplier);
            Impact followup = Hit(fighter, 10, true, multiplier);
            Assert.That(first.Displayed, Is.EqualTo(breakingDamage));
            Assert.That(first.Reasons, Is.EqualTo(firstReasons));
            Assert.That(followup.Displayed, Is.EqualTo(followupDamage));
            Assert.That(followup.Reasons, Is.EqualTo(firstReasons | LegacyDamageReason.BrokenDouble));
            Assert.That(1000 - fighter.Health, Is.EqualTo(breakingDamage + followupDamage));
        }

        [TestCase(1.5, 120, 15, LegacyDamageReason.BrokenDouble | LegacyDamageReason.PressureGuard |
            LegacyDamageReason.BrokenBonus | LegacyDamageReason.ConditionalMultiplier)]
        [TestCase(.5, 40, 5, LegacyDamageReason.ProtectionReduction | LegacyDamageReason.BrokenDouble |
            LegacyDamageReason.PressureGuard | LegacyDamageReason.BrokenBonus | LegacyDamageReason.ConditionalMultiplier)]
        public void AlreadyBroken_PreservesPressureThenBrokenBonusThenConditionalDamage(double multiplier, int damage,
            int push, LegacyDamageReason reasons)
        {
            LegacyFighterState fighter = Fighter(0);
            Impact hit = Hit(fighter, 20, true, multiplier, halve: true, brokenBonus: 100, conditional: 200);
            Assert.That(hit.Displayed, Is.EqualTo(damage));
            Assert.That(hit.Push, Is.EqualTo(push), "Knockback excludes broken and attacker damage multipliers.");
            Assert.That(hit.Reasons, Is.EqualTo(reasons));
            Assert.That(1000 - fighter.Health, Is.EqualTo(damage));
        }

        [Test]
        public void BreakingOverflow_PreservesConditionalDamageWithoutInventingBrokenDouble()
        {
            LegacyFighterState fighter = Fighter(10);
            Impact hit = Hit(fighter, 20, true, 1.5, conditional: 200);
            Assert.That(fighter.Resistance, Is.Zero);
            Assert.That(hit.Push, Is.EqualTo(20));
            Assert.That(hit.Displayed, Is.EqualTo(40));
            Assert.That(hit.Reasons, Is.EqualTo(LegacyDamageReason.ConditionalMultiplier));
        }

        [Test]
        public void PressureAtTheBreakBoundary_PreservesResistanceAndTiesToEvenRounding()
        {
            LegacyFighterState fighter = Fighter(5);
            Impact hit = Hit(fighter, 20, true, 1.5, halve: true, conditional: 200);
            Assert.That(fighter.Resistance, Is.EqualTo(3), "The existing defensive pressure keeps the rounded half resistance.");
            Assert.That(hit.Push, Is.EqualTo(12), "Already scaled overflow25 is halved once, with ties-to-even rounding.");
            Assert.That(hit.Displayed, Is.EqualTo(24));
            Assert.That(hit.Reasons, Is.EqualTo(LegacyDamageReason.PressureGuard | LegacyDamageReason.ConditionalMultiplier));
        }

        [Test]
        public void HealthFloor_StillClampsActualLossWithoutClampingTheDisplayedDamage()
        {
            LegacyFighterState fighter = Fighter(0, health: 40, floor: 5);
            Impact hit = Hit(fighter, 20, true, 1.5);
            Assert.That(fighter.Health, Is.EqualTo(5));
            Assert.That(hit.Displayed, Is.EqualTo(60));
            Assert.That(hit.Push, Is.EqualTo(30));
            Assert.That(hit.Reasons, Is.EqualTo(LegacyDamageReason.BrokenDouble));
        }

        [TestCase(LegacyStatusKind.Vulnerability, 10, 20)]
        [TestCase(LegacyStatusKind.Protection, 5, 5)]
        [TestCase(LegacyStatusKind.Vulnerability, 0, 60)]
        [TestCase(LegacyStatusKind.Protection, 0, 20)]
        public void CampaignForecastAndActualClash_AgreeWithoutSpendingLiveStatuses(LegacyStatusKind kind,
            int resistance, int damage)
        {
            var duel = new LegacyQueuedDuel(1000, resistance, 1000, 1000,
                new[] { Attack(897, 1) }, new[] { Attack(898, 20) }, new[] { 1 }, meshPercent: 0);
            var statuses = (LegacyStatusCollection)typeof(LegacyQueuedDuel).GetField("playerStatuses",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duel);
            statuses.Grant(kind, 50, 1);
            Assert.That(duel.ForecastIfQueued(0).PlayerHealthDamage, Is.EqualTo(damage));
            Assert.That(duel.Player.Health, Is.EqualTo(1000));
            Assert.That(duel.Player.Resistance, Is.EqualTo(resistance));
            Assert.That(statuses.Statuses.Count, Is.EqualTo(1));
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.ForecastTurn()[0].PlayerHealthDamage, Is.EqualTo(damage));
            Assert.That(statuses.Statuses.Count, Is.EqualTo(1));
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.EqualTo(damage));
            Assert.That(statuses.Statuses, Is.Empty);
        }

        [TestCase(0, LegacyStatusKind.Vulnerability, 10, 20)]
        [TestCase(1, LegacyStatusKind.Vulnerability, 10, 20)]
        [TestCase(0, LegacyStatusKind.Protection, 5, 5)]
        [TestCase(1, LegacyStatusKind.Protection, 5, 5)]
        [TestCase(0, LegacyStatusKind.Vulnerability, 0, 60)]
        [TestCase(1, LegacyStatusKind.Vulnerability, 0, 60)]
        [TestCase(0, LegacyStatusKind.Protection, 0, 20)]
        [TestCase(1, LegacyStatusKind.Protection, 0, 20)]
        public void VersusForecastAndActualClash_UseTheSameSingleMultiplierForEitherOwner(int owner, LegacyStatusKind kind,
            int resistance, int damage)
        {
            var match = new LocalVersusMatch(new[] { Attack(897, owner == 0 ? 1 : 20) },
                new[] { Attack(898, owner == 1 ? 1 : 20) }, leftHealth: 1000,
                leftResistance: owner == 0 ? resistance : 1000, rightHealth: 1000,
                rightResistance: owner == 1 ? resistance : 1000);
            LocalVersusFighter target = match.GetFighter(owner);
            var statuses = (LegacyStatusCollection)typeof(LocalVersusFighter).GetField("StatusEffects",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
            statuses.Grant(kind, 50, 1);
            Assert.That(match.TryQueueLane(0, 0) && match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            var forecast = new List<LegacySlotForecast>();
            match.ForecastTurn(owner, forecast);
            Assert.That(forecast[0].PlayerHealthDamage, Is.EqualTo(damage));
            Assert.That(target.State.Health, Is.EqualTo(1000));
            Assert.That(target.State.Resistance, Is.EqualTo(resistance));
            Assert.That(statuses.Statuses.Count, Is.EqualTo(1));
            Assert.That(match.TryCommit(1), Is.True);
            LocalVersusSlotResult actual = match.ResolveNextSlot();
            Assert.That(owner == 0 ? actual.LeftHealthDamage : actual.RightHealthDamage, Is.EqualTo(damage));
            Assert.That(statuses.Statuses, Is.Empty);
        }

        private static LegacyFighterState Fighter(int resistance, int health = 1000, int floor = 0)
            => (LegacyFighterState)FighterConstructor.Invoke(new object[] { health, resistance, floor });
        private static Impact Hit(LegacyFighterState fighter, int power, bool resistanceHit, double multiplier,
            bool halve = false, int brokenBonus = 0, int conditional = 0)
        {
            object impact = ReceiveHit.Invoke(fighter,
                new object[] { power, resistanceHit, multiplier, halve, brokenBonus, conditional });
            Type type = impact.GetType();
            return new Impact((int)type.GetProperty("PushPower").GetValue(impact),
                (int)type.GetProperty("DisplayedDamage").GetValue(impact),
                (LegacyDamageReason)type.GetProperty("DamageReasons").GetValue(impact));
        }
        private static LegacySkill Attack(int id, int power)
            => new LegacySkill(id, "scaling test", 1, power, power,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");
        private readonly struct Impact
        {
            public Impact(int push, int displayed, LegacyDamageReason reasons)
            { Push = push; Displayed = displayed; Reasons = reasons; }
            public int Push { get; }
            public int Displayed { get; }
            public LegacyDamageReason Reasons { get; }
        }
    }
}
