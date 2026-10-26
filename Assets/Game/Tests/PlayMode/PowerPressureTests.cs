using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class PowerPressureTests
    {
        [TestCase(LegacySkillKind.Attack)]
        [TestCase(LegacySkillKind.Defence)]
        [TestCase(LegacySkillKind.Wait)]
        public void EveryCompletedPlayerSkillGetsPressure_RegardlessOfTheOpponentKind(LegacySkillKind kind)
        {
            var duel = PolicyDuel(new[] { PolicySkill(99861, kind) }, new[] { PolicySkill(99862, LegacySkillKind.Defence) });
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(DuelPowerPressure.CanPresent(slot), Is.True);
            Assert.That(DuelPowerPressure.ShouldHold(slot), Is.True);
            duel.ResolveNextHit();
            Assert.That(DuelPowerPressure.CanPresent(slot), Is.False, "A hit already under way cannot start a new preparation.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FastUnilateralCompletionGetsPressure_ChargeWaitsForItsOwnRolledCompletion(bool charge)
        {
            LegacySkill first = PolicySkill(99863, LegacySkillKind.Attack, charge ? 4 : 1);
            var duel = PolicyDuel(charge ? new[] { first } : new[] { first, first },
                new[] { PolicySkill(99864, LegacySkillKind.Attack), PolicySkill(99864, LegacySkillKind.Attack) });
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(DuelPowerPressure.CanPresent(slot), Is.EqualTo(!charge));
            FinishPolicySlot(duel);
            slot = duel.BeginNextSlot();
            Assert.That(DuelPowerPressure.CanPresent(slot), Is.True);
            Assert.That(slot.PlayerSkill, Is.Not.Null);
        }

        [Test]
        public void PendingCounterWaitsForItsActualRoll_ThenReceivesPressureBeforeTheHit()
        {
            LegacySkill attack = PolicySkill(99865, LegacySkillKind.Attack);
            var duel = PolicyDuel(new[] { attack }, new[] { attack, attack }, new LegacyCounter(attack));
            duel.BeginNextSlot(); FinishPolicySlot(duel);
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PendingPlayerCounter, Is.Not.Null);
            Assert.That(DuelPowerPressure.CanPresent(slot), Is.False);
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.True);
            Assert.That(slot.PlayerCountered, Is.True);
            Assert.That(slot.HitsResolved, Is.Zero);
            Assert.That(DuelPowerPressure.CanPresent(slot), Is.True);
        }

        [Test]
        public void AutomaticEmptyIntervalsDoNotPretendToBePlayerSkills()
        {
            LegacySkill attack = PolicySkill(99866, LegacySkillKind.Attack);
            var duel = PolicyDuel(new[] { attack }, new[] { attack, attack });
            duel.BeginNextSlot(); FinishPolicySlot(duel);
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerSkill, Is.Null);
            Assert.That(DuelPowerPressure.CanPresent(slot), Is.False);
            Assert.That(DuelPowerPressure.ShouldHold(slot), Is.False);
            Assert.That(DuelPowerPressure.CanPresent(null), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void VersusViewersShareTheHold_WhenOnlyOnePlayerHasARealSkill(int viewer)
        {
            LegacySkill attack = PolicySkill(99867, LegacySkillKind.Attack);
            var match = new LocalVersusMatch(new[] { attack }, new[] { attack }, randomSeed: 7, usePowerBreak: true);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryCommit(1), Is.True);
            match.BeginNextSlot();
            var view = new LocalVersusPerspective(match, viewer);
            Assert.That(DuelPowerPressure.CanPresent(view.CurrentSlot), Is.EqualTo(viewer == 0));
            Assert.That(DuelPowerPressure.ShouldHold(view.CurrentSlot), Is.True,
                "The viewer with an empty queue must not advance the shared match sooner.");
        }

        private static LegacySkill PolicySkill(int id, LegacySkillKind kind, int duration = 2)
            => new LegacySkill(id, "압력 범위 검사", 0, kind == LegacySkillKind.Wait ? 0 : 5,
                kind == LegacySkillKind.Wait ? 0 : 5, kind, kind == LegacySkillKind.Defence
                    ? LegacySkillProperty.Defence : LegacySkillProperty.Slash, 1, 0, string.Empty, durationHalfUnits: duration);

        private static LegacyQueuedDuel PolicyDuel(LegacySkill[] player, LegacySkill[] enemy, LegacyCounter counter = null)
        {
            var duel = new LegacyQueuedDuel(1000, 0, 1000, 0, player, enemy, new[] { enemy.Length },
                randomSeed: 7, playerCounter: counter, usePowerBreak: true);
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            duel.Commit();
            return duel;
        }

        private static void FinishPolicySlot(LegacyQueuedDuel duel)
        {
            while (!duel.CurrentSlot.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
        }

        [Test]
        public void BeginningUsesZero_WhileRetainingTheAlreadyRolledTarget()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(28);
            Assert.That(pressure.Target, Is.EqualTo(28));
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.Charge, Is.Zero);
            Assert.That(pressure.IsHolding, Is.True);
            Assert.That(pressure.IsVisible, Is.True);
            Assert.That(pressure.IsReleasing, Is.False);
            Assert.That(pressure.Release, Is.Zero);
            Assert.That(pressure.Stamp, Is.Zero);
        }

        [TestCase(1)]
        [TestCase(28)]
        [TestCase(int.MaxValue)]
        public void CompressionNeverDecreasesOrRevealsTheFinalValueEarly(int target)
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(target);
            int previous = 0;
            float previousCharge = 0f;
            for (int i = 0; i < 40; i++)
            {
                pressure.Advance(.01f);
                Assert.That(pressure.Value, Is.InRange(previous, target - 1));
                Assert.That(pressure.Charge, Is.InRange(previousCharge, 1f));
                Assert.That(pressure.IsReleasing, Is.False);
                previous = pressure.Value;
                previousCharge = pressure.Charge;
            }
            pressure.Advance(DuelPowerPressure.ChargeSeconds - .4f + .00001f);
            Assert.That(pressure.Value, Is.EqualTo(target));
            Assert.That(pressure.Charge, Is.EqualTo(1f));
        }

        [Test]
        public void ReleaseLocksTheFinalNumber_BeforeAttackHoldEnds()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(17);
            pressure.Advance(DuelPowerPressure.ChargeSeconds);
            Assert.That(pressure.Value, Is.EqualTo(17));
            Assert.That(pressure.IsHolding, Is.True, "The final number has a short readable lock before the attack.");
            Assert.That(pressure.IsReleasing, Is.True);
            Assert.That(pressure.Stamp, Is.EqualTo(1f).Within(.0001f));
            pressure.Advance(DuelPowerPressure.LockSeconds);
            Assert.That(pressure.IsHolding, Is.False, "Two exact phase-sized ticks must not add a frame at the boundary.");
            Assert.That(pressure.Value, Is.EqualTo(17));
            Assert.That(pressure.IsReleasing, Is.True, "The steam tail can overlap the attacking animation.");
        }

        [Test]
        public void WholeAndSplitRealTimeBudgetsReleaseAtTheSameBoundary()
        {
            var whole = new DuelPowerPressure();
            var split = new DuelPowerPressure();
            whole.Begin(24); split.Begin(24);
            whole.Advance(DuelPowerPressure.HoldSeconds);
            for (int i = 0; i < 57; i++) split.Advance(.01f);
            Assert.That(whole.IsHolding, Is.False);
            Assert.That(split.IsHolding, Is.False);
            Assert.That(split.Value, Is.EqualTo(whole.Value));
            Assert.That(split.Release, Is.EqualTo(whole.Release).Within(.00001f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void PressureUsesElapsedRealTimeRatherThanFrameCount(int fps)
        {
            var whole = new DuelPowerPressure();
            var split = new DuelPowerPressure();
            whole.Begin(28); split.Begin(28);
            whole.Advance(.3f);
            for (int i = 0; i < fps * 3 / 10; i++) split.Advance(1f / fps);
            Assert.That(split.Value, Is.EqualTo(whole.Value));
            Assert.That(split.Charge, Is.EqualTo(whole.Charge).Within(.00001f));
            Assert.That(split.IsHolding, Is.EqualTo(whole.IsHolding));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void PauseAndInvalidDeltaNeverAdvanceOrCorruptPressure(float delta)
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(20);
            pressure.Advance(.1f);
            int value = pressure.Value;
            float charge = pressure.Charge;
            pressure.Advance(delta);
            Assert.That(pressure.Value, Is.EqualTo(value));
            Assert.That(pressure.Charge, Is.EqualTo(charge));
            Assert.That(pressure.IsHolding, Is.True);
        }

        [TestCase(0)]
        [TestCase(-17)]
        public void NonPositiveTargetsRemainZeroWithoutInvalidGaugeValues(int target)
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(target);
            pressure.Advance(.2f);
            Assert.That(pressure.Target, Is.Zero);
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.Charge, Is.InRange(0f, 1f));
            pressure.Advance(1f);
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.IsHolding, Is.False);
        }

        [Test]
        public void SteamFinishesWithoutDroppingTheFinalNumber()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(17);
            pressure.Advance(DuelPowerPressure.ChargeSeconds + DuelPowerPressure.SteamSeconds);
            Assert.That(pressure.IsVisible, Is.False);
            Assert.That(pressure.IsReleasing, Is.False);
            Assert.That(pressure.IsHolding, Is.False);
            Assert.That(pressure.HasValue, Is.True);
            Assert.That(pressure.Value, Is.EqualTo(17));
            Assert.That(pressure.Charge, Is.EqualTo(1f));
            Assert.That(pressure.Release, Is.EqualTo(1f).Within(.00001f));
        }

        [Test]
        public void ClearDropsEveryTransientState_AndBeginCanStartANewNumber()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(28);
            pressure.Advance(.5f);
            pressure.Clear();
            Assert.That(pressure.HasValue, Is.False);
            Assert.That(pressure.IsVisible || pressure.IsHolding || pressure.IsReleasing, Is.False);
            Assert.That(pressure.Target, Is.Zero);
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.Charge + pressure.Release + pressure.Stamp, Is.Zero);
            pressure.Advance(.1f);
            Assert.That(pressure.HasValue, Is.False);
            pressure.Begin(5);
            Assert.That(pressure.Target, Is.EqualTo(5));
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.IsHolding, Is.True);
        }

        [Test]
        public void BeginningANewValueDiscardsThePreviousSteamTail()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(28);
            pressure.Advance(.6f);
            pressure.Begin(5);
            Assert.That(pressure.IsReleasing, Is.False);
            Assert.That(pressure.Release, Is.Zero);
            Assert.That(pressure.Stamp, Is.Zero);
            Assert.That(pressure.Value, Is.Zero);
            Assert.That(pressure.Target, Is.EqualTo(5));
        }

        [Test]
        public void AnOversizedFiniteFrameCompletesSafelyAndStaysCompleted()
        {
            var pressure = new DuelPowerPressure();
            pressure.Begin(int.MaxValue);
            pressure.Advance(float.MaxValue);
            pressure.Advance(float.MaxValue);
            Assert.That(pressure.Value, Is.EqualTo(int.MaxValue));
            Assert.That(pressure.IsHolding || pressure.IsVisible || pressure.IsReleasing, Is.False);
            Assert.That(pressure.Charge, Is.EqualTo(1f));
            Assert.That(pressure.Release, Is.EqualTo(1f));
        }
    }
}
