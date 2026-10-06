using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The decisive close-up's rule (<see cref="LegacyDecisiveHit"/>): a break, a finishing blow, or one hit taking
    /// at least a quarter of the target's maximum health as health, judged on the hit results the controller presents.</summary>
    public sealed class LegacyDecisiveHitTests
    {
        [Test]
        public void AQuarterOfMaximumHealth_IsReachedExactly_WithoutRounding()
        {
            Assert.That(LegacyDecisiveHit.DefaultHealthDamagePercent, Is.EqualTo(25));
            Assert.That(LegacyDecisiveHit.IsHeavy(25, 100), Is.True, "Exactly a quarter is enough.");
            Assert.That(LegacyDecisiveHit.IsHeavy(24, 100), Is.False);
            Assert.That(LegacyDecisiveHit.IsHeavy(100, 100), Is.True);
            Assert.That(LegacyDecisiveHit.IsHeavy(20, 80), Is.True);
            Assert.That(LegacyDecisiveHit.IsHeavy(19, 80), Is.False);
            // A quarter of 90 is 22.5: 22 (24.4%) falls short, as it would not if it were rounded; 23 reaches it.
            Assert.That(LegacyDecisiveHit.IsHeavy(22, 90), Is.False);
            Assert.That(LegacyDecisiveHit.IsHeavy(23, 90), Is.True);
            Assert.That(LegacyDecisiveHit.IsHeavy(10, 41), Is.False, "10.25 is needed.");
            Assert.That(LegacyDecisiveHit.IsHeavy(11, 41), Is.True);
            Assert.That(LegacyDecisiveHit.IsHeavy(1, 3), Is.True, "Any health at all is a third of 3.");
            Assert.That(LegacyDecisiveHit.IsHeavy(int.MaxValue / 4 + 1, int.MaxValue), Is.True, "No overflow on large values.");
            Assert.That(LegacyDecisiveHit.IsHeavy(int.MaxValue / 4 - 1, int.MaxValue), Is.False);

            // Another share, as the presentation setting may set it.
            Assert.That(LegacyDecisiveHit.IsHeavy(10, 100, 10), Is.True);
            Assert.That(LegacyDecisiveHit.IsHeavy(9, 100, 10), Is.False);
            Assert.That(LegacyDecisiveHit.IsHeavy(99, 100, 100), Is.False, "At 100% only all of it counts.");
            Assert.That(LegacyDecisiveHit.IsHeavy(100, 100, 100), Is.True);
        }

        [Test]
        public void NoHealthDamage_NoMaximumOrASwitchedOffShare_IsNeverHeavy()
        {
            Assert.That(LegacyDecisiveHit.IsHeavy(0, 100), Is.False);
            Assert.That(LegacyDecisiveHit.IsHeavy(-30, 100), Is.False);
            Assert.That(LegacyDecisiveHit.IsHeavy(0, 0), Is.False, "Not even against nothing.");
            Assert.That(LegacyDecisiveHit.IsHeavy(25, 0), Is.False);
            Assert.That(LegacyDecisiveHit.IsHeavy(100, 100, 0), Is.False, "A share of 0 switches the trigger off.");
            Assert.That(LegacyDecisiveHit.IsHeavy(100, 100, -5), Is.False);
        }

        [Test]
        public void BreaksAndFinishingBlows_StayDecisive_AndAQuarterOfHealthJoinsThem()
        {
            Assert.That(LegacyDecisiveHit.IsDecisive(true, false, 0, 100), Is.True, "A break that overflows nothing.");
            Assert.That(LegacyDecisiveHit.IsDecisive(false, true, 1, 100), Is.True, "A finishing blow that takes the last point.");
            Assert.That(LegacyDecisiveHit.IsDecisive(false, false, 24, 100), Is.False);
            Assert.That(LegacyDecisiveHit.IsDecisive(false, false, 25, 100), Is.True);
            Assert.That(LegacyDecisiveHit.IsDecisive(false, false, 25, 100, 0), Is.False);
            Assert.That(LegacyDecisiveHit.IsDecisive(true, false, 0, 100, 0) && LegacyDecisiveHit.IsDecisive(false, true, 1, 100, 0),
                Is.True, "Switching the share off keeps breaks and finishing blows.");
        }

        [Test]
        public void OnlyHealthCounts_NotTheResistanceAHitTakes()
        {
            // 40 against an attacking enemy is a resistance exchange: 40% of her health in number, none of it health.
            var clash = Slotted(new LegacyQueuedDuel(100, 50, 100, 50, new[] { Attack(900, 40) },
                new[] { Attack(901, 1) }, new[] { 1 }));
            LegacyHitResult absorbed = clash.ResolveNextHit();
            Assert.That((absorbed.EnemyResistanceDamage, absorbed.EnemyHealthDamage), Is.EqualTo((40, 0)));
            Assert.That(clash.Enemy.Resistance, Is.EqualTo(10), "Nothing broke.");
            Assert.That(LegacyDecisiveHit.IsHeavy(absorbed.EnemyHealthDamage, clash.Enemy.MaxHealth), Is.False);
            Assert.That(LegacyDecisiveHit.IsDecisive(false, false, absorbed.EnemyHealthDamage, clash.Enemy.MaxHealth), Is.False);

            // Into a guard, what gets through is health: 26 against a 1-power guard is exactly a quarter.
            var guarded = Slotted(new LegacyQueuedDuel(100, 50, 100, 50, new[] { Attack(900, 26) },
                new[] { Guard(901, 1) }, new[] { 1 }));
            LegacyHitResult through = guarded.ResolveNextHit();
            Assert.That(through.EnemyHealthDamage, Is.EqualTo(25));
            Assert.That(LegacyDecisiveHit.IsHeavy(through.EnemyHealthDamage, guarded.Enemy.MaxHealth), Is.True);

            // A guard that holds everything leaves nothing to count.
            var held = Slotted(new LegacyQueuedDuel(100, 50, 100, 50, new[] { Attack(900, 30) },
                new[] { Guard(901, 50) }, new[] { 1 }));
            LegacyHitResult blocked = held.ResolveNextHit();
            Assert.That(blocked.EnemyHealthDamage, Is.Zero);
            Assert.That(LegacyDecisiveHit.IsHeavy(blocked.EnemyHealthDamage, held.Enemy.MaxHealth), Is.False);
        }

        [Test]
        public void AMultiHitSlot_IsJudgedHitByHit_TheBreakingHitByItsOverflow()
        {
            // Three hits of 20 into an empty slot: 60 in all, yet no single hit takes a quarter of 100.
            var spread = Slotted(new LegacyQueuedDuel(100, 50, 100, 50, new[] { Attack(900, 60, 3) },
                Array.Empty<LegacySkill>(), new[] { 0 }));
            for (int index = 0; index < 3; index++)
            {
                LegacyHitResult hit = spread.ResolveNextHit();
                Assert.That(hit.EnemyHealthDamage, Is.EqualTo(20), "hit " + index);
                Assert.That(LegacyDecisiveHit.IsHeavy(hit.EnemyHealthDamage, spread.Enemy.MaxHealth), Is.False, "hit " + index);
            }
            Assert.That(spread.Enemy.Health, Is.EqualTo(40));

            // Three hits of 30 against an attacking enemy: the whole slot is a resistance exchange.
            var clash = Slotted(new LegacyQueuedDuel(100, 50, 100, 50, new[] { Attack(900, 90, 3) },
                new[] { Attack(901, 1) }, new[] { 1 }));
            // The first only wears her resistance (50 to 20).
            LegacyHitResult worn = clash.ResolveNextHit();
            Assert.That((worn.EnemyResistanceDamage, worn.EnemyHealthDamage), Is.EqualTo((30, 0)));
            Assert.That(LegacyDecisiveHit.IsDecisive(false, false, worn.EnemyHealthDamage, clash.Enemy.MaxHealth), Is.False);
            // The second breaks it: decisive as a break, while its overflow of 10 alone would not be.
            LegacyHitResult breaking = clash.ResolveNextHit();
            Assert.That((breaking.EnemyResistanceDamage, breaking.EnemyHealthDamage), Is.EqualTo((20, 10)));
            Assert.That(clash.Enemy.IsResistanceBroken, Is.True);
            Assert.That(LegacyDecisiveHit.IsHeavy(breaking.EnemyHealthDamage, clash.Enemy.MaxHealth), Is.False);
            Assert.That(LegacyDecisiveHit.IsDecisive(true, false, breaking.EnemyHealthDamage, clash.Enemy.MaxHealth), Is.True);
            // The third lands on broken resistance: 30 doubled is 60 of her health, heavy on its own.
            LegacyHitResult followUp = clash.ResolveNextHit();
            Assert.That((followUp.EnemyResistanceDamage, followUp.EnemyHealthDamage), Is.EqualTo((0, 60)));
            Assert.That(LegacyDecisiveHit.IsDecisive(false, false, followUp.EnemyHealthDamage, clash.Enemy.MaxHealth), Is.True);
            Assert.That(clash.Enemy.Health, Is.EqualTo(30));

            // A breaking hit whose overflow alone is a quarter is heavy as well as a break.
            var overflow = Slotted(new LegacyQueuedDuel(100, 50, 100, 10, new[] { Attack(900, 36) },
                new[] { Attack(901, 1) }, new[] { 1 }));
            LegacyHitResult big = overflow.ResolveNextHit();
            Assert.That((big.EnemyResistanceDamage, big.EnemyHealthDamage), Is.EqualTo((10, 26)));
            Assert.That(LegacyDecisiveHit.IsHeavy(big.EnemyHealthDamage, overflow.Enemy.MaxHealth), Is.True);
        }

        [Test]
        public void EitherDirection_IsMeasuredAgainstTheTargetsOwnMaximum()
        {
            // The enemy's 49 into the player's empty slot is 24.5% of the player's 200, though it would be heavy on 100.
            var light = Slotted(new LegacyQueuedDuel(200, 50, 100, 50, new[] { Attack(902, 1) },
                new[] { Attack(900, 49) }, new[] { 1 }), false);
            LegacyHitResult glancing = light.ResolveNextHit();
            Assert.That(glancing.EnemyAttacked && !glancing.PlayerAttacked, Is.True);
            Assert.That(glancing.PlayerHealthDamage, Is.EqualTo(49));
            Assert.That(LegacyDecisiveHit.IsHeavy(glancing.PlayerHealthDamage, light.Player.MaxHealth), Is.False);
            Assert.That(LegacyDecisiveHit.IsHeavy(glancing.PlayerHealthDamage, light.Enemy.MaxHealth), Is.True);

            var heavy = Slotted(new LegacyQueuedDuel(200, 50, 100, 50, new[] { Attack(902, 1) },
                new[] { Attack(900, 50) }, new[] { 1 }), false);
            LegacyHitResult landed = heavy.ResolveNextHit();
            Assert.That(landed.PlayerHealthDamage, Is.EqualTo(50));
            Assert.That(LegacyDecisiveHit.IsHeavy(landed.PlayerHealthDamage, heavy.Player.MaxHealth), Is.True);
        }

        [Test]
        public void ThePressureBonusCounts_AndHealthAFloorKeepsDoesNot()
        {
            // 15 into an empty slot is not a quarter; pressure adds the rolled 15 to the same hit's health damage.
            var pressed = Slotted(new LegacyQueuedDuel(100, 50, 100, 50, new[] { Attack(900, 15) },
                Array.Empty<LegacySkill>(), new[] { 0 }));
            Assert.That(pressed.TryStep(LegacyStepAction.Pressure, true, out bool success) && success, Is.True);
            LegacyHitResult boosted = pressed.ResolveNextHit();
            Assert.That(boosted.EnemyHealthDamage, Is.EqualTo(30));
            Assert.That(LegacyDecisiveHit.IsHeavy(boosted.EnemyHealthDamage, pressed.Enemy.MaxHealth), Is.True);
            var plain = Slotted(new LegacyQueuedDuel(100, 50, 100, 50, new[] { Attack(900, 15) },
                Array.Empty<LegacySkill>(), new[] { 0 }));
            Assert.That(LegacyDecisiveHit.IsHeavy(plain.ResolveNextHit().EnemyHealthDamage, plain.Enemy.MaxHealth), Is.False);

            // 이아's floor in the 서막's last mission: 30 of her 40 is heavy, but from 10 health only 9 can go.
            var floored = Slotted(new LegacyQueuedDuel(100, 50, 40, 10, new[] { Attack(900, 30) },
                Array.Empty<LegacySkill>(), new[] { 0 }, enemyHealthFloor: 1));
            LegacyHitResult first = floored.ResolveNextHit();
            Assert.That(first.EnemyHealthDamage, Is.EqualTo(30));
            Assert.That(LegacyDecisiveHit.IsHeavy(first.EnemyHealthDamage, floored.Enemy.MaxHealth), Is.True);
            floored.CompleteCurrentSlot();
            floored.BeginNextTurn();
            Assert.That(floored.TryQueueLane(0), Is.True);
            floored.Commit();
            floored.BeginNextSlot();
            LegacyHitResult kept = floored.ResolveNextHit();
            Assert.That(floored.Enemy.Health, Is.EqualTo(1));
            Assert.That((kept.EnemyHealthDamage, kept.EnemyDisplayedDamage), Is.EqualTo((9, 30)), "The number shown stays the blow's.");
            Assert.That(LegacyDecisiveHit.IsHeavy(kept.EnemyHealthDamage, floored.Enemy.MaxHealth), Is.False,
                "9 of 40 is what she lost.");
        }

        /// <summary>Queues the player's first skill (or nothing), commits, and begins the first slot.</summary>
        private static LegacyQueuedDuel Slotted(LegacyQueuedDuel duel, bool playerQueues = true)
        {
            if (playerQueues) Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            return duel;
        }

        private static LegacySkill Attack(int id, int power, int hits = 1)
            => new LegacySkill(id, "attack", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");
    }
}
