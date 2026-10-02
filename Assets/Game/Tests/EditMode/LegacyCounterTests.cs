using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacyCounterTests
    {
        [Test]
        public void NoCountersByDefault_NothingIsForecast()
        {
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 1) });
            QueueLane(duel, 0, 3);
            Assert.That(duel.PlayerCounter, Is.Null);
            Assert.That(duel.EnemyCounter, Is.Null);
            Assert.That(duel.EnemyCountersRemaining, Is.Zero);
            Assert.That(duel.ForecastEnemyCounterSlots(), Is.Empty);
            Assert.That(duel.ForecastPlayerCounterSlots(), Is.Empty);
        }

        [Test]
        public void EnemyGuardCounter_FillsItsEmptySlotAndReducesTheOneSidedAttack()
        {
            LegacySkill counter = Guard(102, 6);
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 1) }, enemyCounter: new LegacyCounter(counter));
            QueueLane(duel, 0, 2);
            CollectionAssert.AreEqual(new[] { 1 }, duel.ForecastEnemyCounterSlots());
            int act = duel.Act;
            duel.Commit();
            duel.ResolveNextSlot();

            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemyCountered, Is.True);
            Assert.That(slot.EnemySkill, Is.SameAs(counter));
            Assert.That(duel.EnemyCountersRemaining, Is.Zero);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemySkill, Is.SameAs(counter));
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(10 - 6), "The counter guards the formerly free hit.");
            Assert.That(result.EnemyResistanceDamage, Is.Zero);
            Assert.That(duel.Act, Is.EqualTo(act), "A counter is never queued and costs nothing.");
        }

        [Test]
        public void EnemyAttackCounter_TurnsTheFreeHitIntoAResistanceClash()
        {
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 1) },
                enemyCounter: new LegacyCounter(Attack(103, 7)));
            QueueLane(duel, 0, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.Zero);
            Assert.That(result.PlayerHealthDamage, Is.Zero);
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo(10));
            Assert.That(result.PlayerResistanceDamage, Is.EqualTo(7));
        }

        [Test]
        public void CounterUsesAnswerTheFirstOneSidedAttacksAndResetEachTurn_WithoutAdvancingThePattern()
        {
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 1), Attack(104, 2) },
                enemyCounter: new LegacyCounter(Guard(102, 6)));
            QueueLane(duel, 0, 3);
            CollectionAssert.AreEqual(new[] { 1 }, duel.ForecastEnemyCounterSlots());
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(4));
            CollectionAssert.IsEmpty(duel.ForecastEnemyCounterSlots(), "The single use is spent.");
            LegacySlotResult uncountered = duel.ResolveNextSlot();
            Assert.That(uncountered.EnemySkill, Is.Null);
            Assert.That(uncountered.EnemyHealthDamage, Is.EqualTo(10));

            duel.BeginNextTurn();
            Assert.That(duel.EnemyCountersRemaining, Is.EqualTo(1));
            Assert.That(duel.EnemyQueue[0].Id, Is.EqualTo(104), "Counters do not consume the enemy's pattern.");
        }

        [Test]
        public void DefenseAndBreathingDoNotDrawACounter()
        {
            var duel = Duel(new[] { Attack(100, 10), Guard(105, 4, 1) }, new[] { Attack(101, 1) },
                enemyCounter: new LegacyCounter(Guard(102, 6)));
            QueueLane(duel, 0, 1);
            QueueLane(duel, 1, 1);
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.ForecastEnemyCounterSlots(), Is.Empty);
            duel.Commit();
            for (int i = 0; i < 3; i++)
            {
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                Assert.That(slot.EnemyCountered, Is.False);
                duel.ResolveNextSlot();
            }
            Assert.That(duel.EnemyCountersRemaining, Is.EqualTo(1));
        }

        [Test]
        public void PlayerCounter_SettlesAtTheFirstHitAndGuardsTheEnemysExtraAttack()
        {
            LegacySkill counter = Guard(110, 6);
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 9) }, count: 2,
                playerCounter: new LegacyCounter(counter));
            QueueLane(duel, 0, 1);
            CollectionAssert.AreEqual(new[] { 1 }, duel.ForecastPlayerCounterSlots());
            duel.Commit();
            duel.ResolveNextSlot();

            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerSkill, Is.Null);
            Assert.That(slot.PendingPlayerCounter, Is.SameAs(counter));
            Assert.That(duel.PlayerCountersRemaining, Is.EqualTo(1), "Nothing is spent before the counter strikes.");
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerSkill, Is.SameAs(counter));
            Assert.That(slot.PlayerCountered, Is.True);
            Assert.That(slot.PendingPlayerCounter, Is.Null);
            Assert.That(duel.PlayerCountersRemaining, Is.Zero);
            Assert.That(hit.PlayerHealthDamage, Is.EqualTo(9 - 6));
            Assert.That(duel.CompleteCurrentSlot().PlayerSkill, Is.SameAs(counter));
        }

        [Test]
        public void DodgeAttempt_ForgoesTheCounterShrinksTheSlotAndKeepsTheUseForTheNextOneSidedAttack()
        {
            LegacySkill counter = Attack(111, 6, 3);
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 9) }, count: 3,
                playerCounter: new LegacyCounter(counter));
            QueueLane(duel, 0, 1);
            CollectionAssert.AreEqual(new[] { 1 }, duel.ForecastPlayerCounterSlots());
            duel.Commit();
            duel.ResolveNextSlot();

            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.HitCount, Is.EqualTo(3), "The pending counter's hits are expected to play.");
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, false, out bool success), Is.True);
            Assert.That(success, Is.False, "Even a mistimed evasion is a choice to evade.");
            Assert.That(slot.DodgeAttempted, Is.True);
            Assert.That(slot.PendingPlayerCounter, Is.Null);
            Assert.That(slot.HitCount, Is.EqualTo(1));
            LegacySlotResult dodged = duel.ResolveNextSlot();
            Assert.That(dodged.PlayerSkill, Is.Null);
            Assert.That(dodged.PlayerHealthDamage, Is.EqualTo(9));
            Assert.That(duel.PlayerCountersRemaining, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 2 }, duel.ForecastPlayerCounterSlots());

            LegacySlotResult countered = duel.ResolveNextSlot();
            Assert.That(countered.PlayerSkill, Is.SameAs(counter));
            Assert.That(countered.PlayerHealthDamage, Is.Zero, "The attack counter clashes on resistance.");
            Assert.That(countered.PlayerResistanceDamage, Is.EqualTo(9));
            Assert.That(countered.EnemyResistanceDamage, Is.EqualTo(6));
        }

        [Test]
        public void Pressure_BacksAPendingCounterUnlessADodgeAttemptFollows()
        {
            var backed = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 9) }, count: 2,
                playerCounter: new LegacyCounter(Attack(112, 8)));
            QueueLane(backed, 0, 1);
            backed.Commit();
            backed.ResolveNextSlot();
            backed.BeginNextSlot();
            Assert.That(backed.TryStep(LegacyStepAction.Pressure, true, out bool pressed), Is.True);
            Assert.That(pressed, Is.True);
            LegacySlotResult result = backed.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(8), "The counter's whole power as flat pressure damage.");
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo(8));

            var cancelled = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 9) }, count: 2,
                playerCounter: new LegacyCounter(Attack(112, 8)));
            QueueLane(cancelled, 0, 1);
            cancelled.Commit();
            cancelled.ResolveNextSlot();
            LegacyCurrentSlot slot = cancelled.BeginNextSlot();
            cancelled.TryStep(LegacyStepAction.Pressure, true, out _);
            Assert.That(cancelled.TryStep(LegacyStepAction.Dodge, true, out bool dodged), Is.True);
            Assert.That(dodged, Is.True);
            Assert.That(slot.PressureSucceeded, Is.False);
            LegacySlotResult evaded = cancelled.ResolveNextSlot();
            Assert.That(evaded.PlayerSkill, Is.Null);
            Assert.That(evaded.EnemyHealthDamage + evaded.EnemyResistanceDamage, Is.Zero);
            Assert.That(evaded.PlayerHealthDamage + evaded.PlayerResistanceDamage, Is.Zero);
        }

        [Test]
        public void ACounterNeverAnswersACounter()
        {
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 1) },
                playerCounter: new LegacyCounter(Guard(110, 6)), enemyCounter: new LegacyCounter(Attack(103, 7)));
            QueueLane(duel, 0, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemyCountered, Is.True);
            Assert.That(slot.PendingPlayerCounter, Is.Null);
            duel.ResolveNextSlot();
            Assert.That(duel.PlayerCountersRemaining, Is.EqualTo(1));
        }

        [Test]
        public void CounterSkillEffectsApplyWhenItStrikes()
        {
            // Slash (id 1) grants the player +1 ACT next turn; as a counter it still does.
            LegacySkill slash = LegacySkillDefinitions.Skill(1);
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 1) }, count: 2,
                playerCounter: new LegacyCounter(slash));
            QueueLane(duel, 0, 1);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(duel.NextActGain, Is.EqualTo(3), "Its effects wait for the counter to strike.");
            duel.ResolveNextHit();
            Assert.That(duel.NextActGain, Is.EqualTo(4));
            Assert.That(slot.PlayerFeedback.EffectActivated, Is.True);
        }

        [Test]
        public void PlayerForecast_KeepsThePendingSlotHoldsItsUseAndMovesOnAfterADodge()
        {
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 9) }, count: 3,
                playerCounter: new LegacyCounter(Guard(110, 6)));
            QueueLane(duel, 0, 1);
            duel.Commit();
            duel.ResolveNextSlot();

            duel.BeginNextSlot();
            CollectionAssert.AreEqual(new[] { 1 }, duel.ForecastPlayerCounterSlots(),
                "The pending counter keeps its card and does not also claim the next attack.");
            duel.TryStep(LegacyStepAction.Dodge, false, out _);
            CollectionAssert.AreEqual(new[] { 2 }, duel.ForecastPlayerCounterSlots(), "The released use moves on.");
            duel.ResolveNextSlot();

            duel.BeginNextSlot();
            CollectionAssert.AreEqual(new[] { 2 }, duel.ForecastPlayerCounterSlots());
            duel.ResolveNextHit();
            CollectionAssert.AreEqual(new[] { 2 }, duel.ForecastPlayerCounterSlots(), "It still plays in this slot.");
            duel.CompleteCurrentSlot();
            CollectionAssert.IsEmpty(duel.ForecastPlayerCounterSlots());
        }

        [Test]
        public void EnemyForecast_KeepsTheCurrentCounterSlotUntilItCompletes()
        {
            var duel = Duel(new[] { Attack(100, 10) }, new[] { Attack(101, 1) },
                enemyCounter: new LegacyCounter(Guard(102, 6)));
            QueueLane(duel, 0, 3);
            duel.Commit();
            duel.ResolveNextSlot();
            CollectionAssert.AreEqual(new[] { 1 }, duel.ForecastEnemyCounterSlots());
            duel.BeginNextSlot();
            CollectionAssert.AreEqual(new[] { 1 }, duel.ForecastEnemyCounterSlots());
            duel.ResolveNextSlot();
            CollectionAssert.IsEmpty(duel.ForecastEnemyCounterSlots());
        }

        [Test]
        public void Forecasts_AreEmptyOnceTheDuelEnds()
        {
            // With no resistance left, the first clash reaches the enemy's small health.
            var duel = new LegacyQueuedDuel(1000, 50, 5, 0, new[] { Attack(100, 10) }, new[] { Attack(101, 1) },
                new[] { 1 }, 42, enemyCounter: new LegacyCounter(Guard(102, 6)));
            QueueLane(duel, 0, 3);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.IsFinished, Is.True);
            CollectionAssert.IsEmpty(duel.ForecastEnemyCounterSlots(), "Slots that will never play are not forecast.");
        }

        [Test]
        public void EnemyCounterFacing_NamesTheCounterAPlannedAttackWouldMeet()
        {
            LegacySkill counter = Guard(102, 6);
            LegacySkill attack = Attack(100, 10);
            var duel = Duel(new[] { attack, Guard(105, 4, 1) }, new[] { Attack(101, 1) },
                enemyCounter: new LegacyCounter(counter));
            Assert.That(duel.EnemyCounterFacing(attack), Is.Null, "The first slot meets the enemy's queued action.");
            QueueLane(duel, 0, 1);
            Assert.That(duel.EnemyCounterFacing(attack), Is.SameAs(counter));
            Assert.That(duel.EnemyCounterFacing(duel.GetLane(1)[0]), Is.Null, "A defence does not draw the counter.");
            QueueLane(duel, 0, 1);
            Assert.That(duel.EnemyCounterFacing(attack), Is.Null, "The single use is already forecast.");
            duel.Commit();
            Assert.That(duel.EnemyCounterFacing(attack), Is.Null, "Only planning previews a next queue slot.");
        }

        [Test]
        public void CounterRejectsBreathingAndNonPositiveUses()
        {
            Assert.Throws<System.ArgumentException>(() => new LegacyCounter(LegacyCommonActions.Breathe));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new LegacyCounter(Guard(102, 6), 0));
            Assert.Throws<System.ArgumentNullException>(() => new LegacyCounter(null));
        }

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy, int count = 1,
            LegacyCounter playerCounter = null, LegacyCounter enemyCounter = null)
            => new LegacyQueuedDuel(1000, 50, 1000, 50, player, enemy, new[] { count }, 42, playerCounter, enemyCounter);

        private static LegacySkill Attack(int id, int power, int hits = 1)
            => new LegacySkill(id, "attack", 1, power, power,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "");

        private static LegacySkill Guard(int id, int power, int lane = 0)
            => new LegacySkill(id, "guard", 1, power, power,
                LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, lane, "");

        private static void QueueLane(LegacyQueuedDuel duel, int lane, int times)
        {
            for (int i = 0; i < times; i++) Assert.That(duel.TryQueueLane(lane), Is.True);
        }
    }
}
