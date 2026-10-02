using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacyStepCombatTests
    {
        [TestCase(0)]
        [TestCase(50)]
        public void Dodge_IgnoresEveryEnemyHitWithoutCancellingEitherSkill(int resistance)
        {
            var duel = Duel(new[] { Attack(100, 9, 3) }, new[] { Attack(101, 12, 3) }, resistance: resistance);
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Dodge, true, true);

            for (int i = 0; i < 3; i++)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.EnemyAttacked, Is.True);
                Assert.That(hit.PlayerAttacked, Is.True);
                Assert.That(hit.PlayerDodged, Is.True);
                Assert.That(hit.PlayerHealthDamage, Is.Zero);
                Assert.That(hit.PlayerResistanceDamage, Is.Zero);
                Assert.That(hit.PlayerDisplayedDamage, Is.Zero);
                Assert.That(hit.PlayerPushPower, Is.Zero);
            }
            LegacySlotResult result = duel.CompleteCurrentSlot();
            Assert.That(duel.Player.Health, Is.EqualTo(1000));
            Assert.That(duel.Player.Resistance, Is.EqualTo(resistance));
            Assert.That(result.EnemyHealthDamage + result.EnemyResistanceDamage, Is.GreaterThan(0));
        }

        [Test]
        public void Dodge_AlsoProtectsAnEmptyPlayerSlot()
        {
            var duel = Duel(new[] { Guard(100, 1) }, new[] { Attack(101, 12, 3) });
            duel.Commit();
            duel.BeginNextSlot();
            AssertStep(duel, LegacyStepAction.Pressure, true, false);
            AssertStep(duel, LegacyStepAction.Dodge, true, true);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.PlayerSkill, Is.Null);
            Assert.That(result.PlayerHealthDamage, Is.Zero);
            Assert.That(result.PlayerResistanceDamage, Is.Zero);
        }

        [Test]
        public void Pressure_AddsTheTotalSkillPowerOnceWithRemainderAcrossMultipleHits()
        {
            var duel = Duel(new[] { Attack(100, 11, 3) }, new[] { Attack(101, 1) });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            int[] bonus = { 4, 4, 3 };
            for (int i = 0; i < 3; i++)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.PlayerPressured, Is.True);
                Assert.That(hit.EnemyHealthDamage, Is.EqualTo(bonus[i]));
                Assert.That(hit.EnemyResistanceDamage, Is.EqualTo(3));
                Assert.That(hit.EnemyDisplayedDamage, Is.EqualTo(3 + bonus[i]));
                Assert.That(hit.EnemyPushPower, Is.EqualTo(3 + bonus[i]));
            }
            LegacySlotResult result = duel.CompleteCurrentSlot();
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(11));
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo(9));
        }

        [Test]
        public void Pressure_FlatBonusIsNotDoubledByBrokenResistance()
        {
            var duel = Duel(new[] { Attack(100, 11, 3) }, new[] { Attack(101, 1) }, resistance: 0);
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(18 + 11));
            Assert.That(result.EnemyResistanceDamage, Is.Zero);
        }

        [Test]
        public void Pressure_AlsoAddsWholePowerWhenTheOpposingSlotIsEmpty()
        {
            var duel = Duel(new[] { Attack(100, 11, 3) }, new[] { Guard(101, 1) });
            duel.TryQueueLane(0);
            duel.TryQueueLane(0);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemySkill, Is.Null);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(9 + 11));
            Assert.That(result.EnemyResistanceDamage, Is.Zero);
        }

        [TestCase(50, 18)]
        [TestCase(0, 24)]
        public void Pressure_AgainstEnemyGuardStillAddsWholeUnblockedSkillPower(int resistance, int damage)
        {
            var duel = Duel(new[] { Attack(100, 12, 3) }, new[] { Guard(101, 6) }, resistance: resistance);
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(damage));
            Assert.That(result.EnemyResistanceDamage, Is.Zero);
        }

        [Test]
        public void Pressure_FlatBonusPassesAnEnemyGuardThatFullyBlocksTheNormalAttack()
        {
            var duel = Duel(new[] { Attack(100, 12, 3) }, new[] { Guard(101, 30) });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(12));
            Assert.That(result.EnemyResistanceDamage, Is.Zero);
        }

        [Test]
        public void Pressure_WithOwnGuardHalvesPostGuardDamageRatherThanDoublingGuardPower()
        {
            var duel = Duel(new[] { Guard(100, 3) }, new[] { Attack(101, 8, 2) });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacyHitResult first = duel.ResolveNextHit();
            LegacyHitResult second = duel.ResolveNextHit();
            Assert.That(first.PlayerHealthDamage, Is.EqualTo(2));
            Assert.That(second.PlayerHealthDamage, Is.EqualTo(2));
            Assert.That(first.PlayerPressured, Is.True);
            LegacySlotResult result = duel.CompleteCurrentSlot();
            Assert.That(result.PlayerHealthDamage, Is.EqualTo(4));
            Assert.That(result.EnemyHealthDamage, Is.Zero);
            Assert.That(result.EnemyResistanceDamage, Is.Zero);
        }

        [TestCase(4, 50, 0)]
        [TestCase(6, 50, 2)]
        [TestCase(6, 0, 3)]
        public void DefensivePressure_HalvesFinalDamageWithTiesToEvenRounding(int enemyPower, int resistance, int expected)
        {
            var duel = Duel(new[] { Guard(100, 3) }, new[] { Attack(101, enemyPower) }, resistance: resistance);
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerHealthDamage, Is.EqualTo(expected));
            Assert.That(hit.PlayerDisplayedDamage, Is.EqualTo(expected));
            Assert.That(hit.PlayerResistanceDamage, Is.Zero);
            Assert.That(hit.EnemyHealthDamage, Is.Zero);
        }

        [Test]
        public void DefensivePressure_HalvesRawDamageBeforeClampingToRemainingHealth()
        {
            var duel = new LegacyQueuedDuel(3, 50, 100, 50,
                new[] { Guard(100, 2) }, new[] { Attack(101, 20) }, new[] { 1 });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerHealthDamage, Is.EqualTo(3));
            Assert.That(hit.PlayerDisplayedDamage, Is.EqualTo(9));
            Assert.That(duel.Player.Health, Is.Zero);
            Assert.That(duel.CompleteCurrentSlot().Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
        }

        [Test]
        public void GuardAgainstGuard_AcceptsPressureWithoutCreatingACounterAttack()
        {
            var duel = Duel(new[] { Guard(100, 3) }, new[] { Guard(101, 10) });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Dodge, true, false);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerAttacked, Is.False);
            Assert.That(hit.EnemyAttacked, Is.False);
            Assert.That(hit.PlayerPressured, Is.True);
            Assert.That(hit.PlayerHealthDamage + hit.EnemyHealthDamage, Is.Zero);
        }

        [Test]
        public void RepeatedPressure_DoesNotMultiplyItsBonusAndBothStepKindsCanSucceed()
        {
            var duel = Duel(new[] { Attack(100, 11, 3) }, new[] { Attack(101, 12, 3) });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            for (int i = 0; i < 8; i++) AssertStep(duel, LegacyStepAction.Pressure, true, false);
            AssertStep(duel, LegacyStepAction.Dodge, true, true);
            AssertStep(duel, LegacyStepAction.Dodge, true, false);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(11));
            Assert.That(result.PlayerHealthDamage + result.PlayerResistanceDamage, Is.Zero);
            Assert.That(duel.Act, Is.EqualTo(2));
        }

        [Test]
        public void Steps_OnlySucceedBeforeTheFirstHitAndMistimedAttemptsStillCount()
        {
            var duel = Duel(new[] { Attack(100, 9, 3) }, new[] { Attack(101, 12, 3) });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Dodge, false, false);
            Assert.That(duel.UsedStepThisTurn, Is.True);
            duel.ResolveNextHit();
            AssertStep(duel, LegacyStepAction.Dodge, true, false);
            AssertStep(duel, LegacyStepAction.Pressure, true, false);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.PlayerResistanceDamage, Is.EqualTo(12));
            Assert.That(result.EnemyHealthDamage, Is.Zero);
        }

        [Test]
        public void AttemptsInSkillGaps_HaveNoLimitAreCountedAndMissOnlyTheFollowingNaturalRecovery()
        {
            var duel = Duel(new[] { Attack(100, 1) }, new[] { Guard(101, 1) });
            duel.Commit();
            for (int i = 0; i < 20; i++) AssertStep(duel, LegacyStepAction.Dodge, false, false);
            Assert.That(duel.CurrentSlot, Is.Null);
            Assert.That(duel.Act, Is.EqualTo(3));
            Assert.That(duel.StepAttemptsThisTurn, Is.EqualTo(20));
            duel.ResolveNextSlot();
            AssertStep(duel, LegacyStepAction.Pressure, true, false);
            Assert.That(duel.StepAttemptsThisTurn, Is.EqualTo(21));
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(3), "Gap inputs are misses and cost the next natural recovery.");
            Assert.That(duel.UsedStepThisTurn, Is.False);
            Assert.That(duel.StepAttemptsThisTurn, Is.Zero);
            Assert.That(duel.StepMissedThisTurn, Is.False);
            ResolveTurn(duel);
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(6));
        }

        [TestCase(1, 1)]
        [TestCase(7, 2)]
        public void MissedStep_KeepsResidualActAndSkillGrantedRecovery(int skillId, int bonus)
        {
            LegacySkill skill = skillId == 1 ? Attack(1, 1) : Guard(7, 10);
            LegacySkill enemy = new LegacySkill(101, "hit", 1, 1, 1,
                LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 0, "");
            var duel = Duel(new[] { skill }, new[] { enemy });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, false, false);
            duel.ResolveNextSlot();
            Assert.That(duel.NextActGain, Is.EqualTo(3 + bonus));
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(2 + bonus));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            Assert.That(duel.UsedStepThisTurn, Is.False);
        }

        [Test]
        public void NewSlotAndTurn_ClearSuccessFlagsWhileTheTurnKeepsCountingAttempts()
        {
            var duel = Duel(new[] { Attack(100, 1) }, new[] { Attack(101, 1) }, count: 2);
            duel.TryQueueLane(0);
            duel.TryQueueLane(0);
            duel.Commit();
            duel.BeginNextSlot();
            AssertStep(duel, LegacyStepAction.Dodge, true, true);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            duel.ResolveNextSlot();
            LegacyCurrentSlot next = duel.BeginNextSlot();
            Assert.That(next.DodgeSucceeded, Is.False);
            Assert.That(next.PressureSucceeded, Is.False);
            Assert.That(duel.UsedStepThisTurn, Is.True);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerDodged, Is.False);
            Assert.That(hit.PlayerPressured, Is.False);
            duel.CompleteCurrentSlot();
            Assert.That(duel.StepAttemptsThisTurn, Is.EqualTo(2));
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(4));
            Assert.That(duel.UsedStepThisTurn, Is.False);
        }

        [Test]
        public void PlanningFinishedAndInvalidActions_AreRejectedAndResetRestoresTheOpening()
        {
            var duel = new LegacyQueuedDuel(20, 50, 1, 0,
                new[] { Attack(100, 10) }, new[] { Guard(101, 1) }, new[] { 1 });
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool success), Is.False);
            Assert.That(success, Is.False);
            Assert.That(duel.UsedStepThisTurn, Is.False);
            QueueAndBegin(duel);
            Assert.That(duel.TryStep((LegacyStepAction)999, true, out success), Is.False);
            Assert.That(duel.UsedStepThisTurn, Is.False);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            duel.ResolveNextSlot();
            Assert.That(duel.IsFinished, Is.True);
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out success), Is.False);
            Assert.That(success, Is.False);
            duel.Reset();
            Assert.That(duel.Act, Is.EqualTo(3));
            Assert.That(duel.UsedStepThisTurn, Is.False);
            Assert.That(duel.CurrentSlot, Is.Null);
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
        }

        [Test]
        public void Pressure_UsesBuffedWholePowerAndIgnoresEnemyReceivedDamageReduction()
        {
            LegacySkill opening = new LegacySkill(3, "support", 0, 5, 5,
                LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 3, 0, "");
            LegacySkill followup = new LegacySkill(100, "followup", 0, 20, 20,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 3, 0, "");
            var duel = Duel(new[] { opening, followup }, new[] { Guard(8, 0), Attack(101, 1) }, count: 2);
            duel.TryQueueLane(0);
            duel.TryQueueLane(0);
            duel.Commit();
            duel.ResolveNextSlot();
            duel.BeginNextSlot();
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(22));
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo(15));
        }

        [Test]
        public void Pressure_OverkillClampsHealthButRetainsRawDamageAndCompletesTheSlot()
        {
            var duel = new LegacyQueuedDuel(100, 50, 3, 0,
                new[] { Attack(100, 4) }, new[] { Guard(101, 1) }, new[] { 1 });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyHealthDamage, Is.EqualTo(3));
            Assert.That(hit.EnemyDisplayedDamage, Is.EqualTo(10));
            Assert.That(hit.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(duel.IsFinished, Is.False);
            LegacySlotResult complete = duel.CompleteCurrentSlot();
            Assert.That(complete.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(duel.Enemy.Health, Is.Zero);
            Assert.That(duel.IsFinished, Is.True);
        }

        [Test]
        public void ZeroPowerPressure_DoesNotAddTheLegacyMinimumHitPowerAsABonus()
        {
            var duel = Duel(new[] { Attack(100, 0, 3) }, new[] { Attack(101, 1) });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.Zero);
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo(3));
        }

        [Test]
        public void MistimedInputs_DoNotChangeSeededNormalDamageOrSkillEffects()
        {
            LegacySkill[] skills = { LegacySkillDefinitions.Skill(1), LegacySkillDefinitions.Skill(2) };
            var baseline = Duel(skills, new[] { LegacySkillDefinitions.Skill(5) }, count: 2, resistance: 0);
            var mistimed = Duel(skills, new[] { LegacySkillDefinitions.Skill(5) }, count: 2, resistance: 0);
            foreach (LegacyQueuedDuel duel in new[] { baseline, mistimed })
            {
                duel.TryQueueLane(0);
                duel.TryQueueLane(0);
                duel.Commit();
            }
            for (int i = 0; i < 2; i++)
            {
                baseline.BeginNextSlot();
                mistimed.BeginNextSlot();
                AssertStep(mistimed, LegacyStepAction.Pressure, false, false);
                while (!baseline.IsCurrentSlotResolved)
                {
                    LegacyHitResult expected = baseline.ResolveNextHit();
                    LegacyHitResult actual = mistimed.ResolveNextHit();
                    Assert.That(actual.PlayerHealthDamage, Is.EqualTo(expected.PlayerHealthDamage));
                    Assert.That(actual.EnemyHealthDamage, Is.EqualTo(expected.EnemyHealthDamage));
                    Assert.That(actual.PlayerDisplayedDamage, Is.EqualTo(expected.PlayerDisplayedDamage));
                    Assert.That(actual.EnemyDisplayedDamage, Is.EqualTo(expected.EnemyDisplayedDamage));
                    Assert.That(actual.PlayerPushPower, Is.EqualTo(expected.PlayerPushPower));
                    Assert.That(actual.EnemyPushPower, Is.EqualTo(expected.EnemyPushPower));
                }
                baseline.CompleteCurrentSlot();
                mistimed.CompleteCurrentSlot();
            }
            Assert.That(mistimed.Player.Health, Is.EqualTo(baseline.Player.Health));
            Assert.That(mistimed.Enemy.Health, Is.EqualTo(baseline.Enemy.Health));
            Assert.That(mistimed.NextActGain, Is.EqualTo(baseline.NextActGain));
        }

        [TestCase(0, .1f)]
        [TestCase(1, .065f)]
        [TestCase(2, .04225f)]
        [TestCase(3, .04f)]
        [TestCase(20, .04f)]
        public void StepWindow_NarrowsPerAttemptDownToAHumanFloor(int attempts, float expected)
        {
            Assert.That(LegacyStepTiming.Window(.1f, attempts, .65f, .04f), Is.EqualTo(expected).Within(.00001f));
        }

        [Test]
        public void StepWindow_NeverWidensAndIgnoresInvalidTuning()
        {
            Assert.That(LegacyStepTiming.Window(.03f, 5, .65f, .04f), Is.EqualTo(.03f), "The floor never widens a smaller base.");
            Assert.That(LegacyStepTiming.Window(.1f, 1, 0f, .04f), Is.EqualTo(.1f * LegacyStepTiming.DefaultDecay).Within(.00001f));
            Assert.That(LegacyStepTiming.Window(.1f, 1, 1.5f, .04f), Is.EqualTo(.1f * LegacyStepTiming.DefaultDecay).Within(.00001f));
            Assert.That(LegacyStepTiming.Window(0f, 0), Is.Zero);
            Assert.That(LegacyStepTiming.Window(.1f, 4, 1f, .04f), Is.EqualTo(.1f), "A decay of 1 keeps the window.");
        }

        [Test]
        public void Reset_AfterAMissRestoresTheOpeningAct()
        {
            var duel = Duel(new[] { Attack(100, 1) }, new[] { Attack(101, 1) });
            QueueAndBegin(duel);
            AssertStep(duel, LegacyStepAction.Dodge, false, false);
            Assert.That(duel.StepMissedThisTurn, Is.True);
            duel.Reset();
            Assert.That(duel.Act, Is.EqualTo(3));
            Assert.That(duel.StepMissedThisTurn, Is.False);
            Assert.That(duel.StepAttemptsThisTurn, Is.Zero);
        }

        [Test]
        public void OnlyAMissCostsTheNaturalRecovery_SuccessesAreFree()
        {
            var clean = Duel(new[] { Attack(100, 1) }, new[] { Attack(101, 1) });
            QueueAndBegin(clean);
            AssertStep(clean, LegacyStepAction.Dodge, true, true);
            AssertStep(clean, LegacyStepAction.Pressure, true, true);
            Assert.That(clean.StepMissedThisTurn, Is.False);
            clean.ResolveNextSlot();
            clean.BeginNextTurn();
            Assert.That(clean.Act, Is.EqualTo(2 + 3), "Clean steps keep the natural recovery.");

            var missed = Duel(new[] { Attack(100, 1) }, new[] { Attack(101, 1) });
            QueueAndBegin(missed);
            AssertStep(missed, LegacyStepAction.Dodge, true, true);
            AssertStep(missed, LegacyStepAction.Dodge, true, false);
            Assert.That(missed.StepMissedThisTurn, Is.True, "Repeating a won dodge is a miss.");
            missed.ResolveNextSlot();
            missed.BeginNextTurn();
            Assert.That(missed.Act, Is.EqualTo(2));
            Assert.That(missed.StepMissedThisTurn, Is.False);
        }

        [Test]
        public void SuccessStreak_CountsConsecutiveSuccessesAndAnyMissOrNewTurnRestartsIt()
        {
            var duel = Duel(new[] { Attack(100, 1) }, new[] { Attack(101, 1) }, count: 3);
            for (int i = 0; i < 3; i++) Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            AssertStep(duel, LegacyStepAction.Dodge, true, true);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            Assert.That(duel.StepSuccessStreak, Is.EqualTo(2));
            AssertStep(duel, LegacyStepAction.Dodge, true, false);
            Assert.That(duel.StepSuccessStreak, Is.Zero, "Repeating an effect already won counts as a miss.");
            duel.ResolveNextSlot();
            duel.BeginNextSlot();
            AssertStep(duel, LegacyStepAction.Dodge, true, true);
            Assert.That(duel.StepSuccessStreak, Is.EqualTo(1));
            Assert.That(duel.StepAttemptsThisTurn, Is.EqualTo(4));
            duel.ResolveNextSlot();
            duel.BeginNextSlot();
            AssertStep(duel, LegacyStepAction.Dodge, false, false);
            Assert.That(duel.StepSuccessStreak, Is.Zero);
            AssertStep(duel, LegacyStepAction.Pressure, true, true);
            Assert.That(duel.StepSuccessStreak, Is.EqualTo(1));
            duel.ResolveNextSlot();
            duel.BeginNextTurn();
            Assert.That(duel.StepSuccessStreak, Is.Zero);
            Assert.That(duel.StepAttemptsThisTurn, Is.Zero);
            duel.Reset();
            Assert.That(duel.StepSuccessStreak, Is.Zero);
        }

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy,
            int count = 1, int resistance = 50)
            => new LegacyQueuedDuel(1000, resistance, 1000, resistance, player, enemy, new[] { count }, 42);

        private static LegacySkill Attack(int id, int power, int hits = 1)
            => new LegacySkill(id, "attack", 1, power, power,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 1, power, power,
                LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");

        private static void QueueAndBegin(LegacyQueuedDuel duel)
        {
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
        }

        private static void AssertStep(LegacyQueuedDuel duel, LegacyStepAction action, bool timing, bool expectedSuccess)
        {
            Assert.That(duel.TryStep(action, timing, out bool success), Is.True);
            Assert.That(success, Is.EqualTo(expectedSuccess));
        }

        private static void ResolveTurn(LegacyQueuedDuel duel)
        {
            duel.Commit();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
        }
    }
}
