using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacyBreathingActionTests
    {
        [Test]
        public void CommonAction_IsAnIdleZeroCostZeroPowerSlotWithoutChangingExistingEnumValues()
        {
            LegacySkill breath = LegacyCommonActions.Breathe;
            Assert.That(breath.Id, Is.EqualTo(-1));
            Assert.That(breath.IconId, Is.EqualTo(-1));
            Assert.That(breath.Name, Is.EqualTo("숨고르기"));
            Assert.That(breath.Cost, Is.Zero);
            Assert.That(breath.MinPower, Is.Zero);
            Assert.That(breath.MaxPower, Is.Zero);
            Assert.That(breath.AttackCount, Is.EqualTo(1));
            Assert.That(breath.IsWait, Is.True);
            Assert.That(breath.Kind, Is.EqualTo(LegacySkillKind.Wait));
            Assert.That(breath.Property, Is.EqualTo(LegacySkillProperty.None));
            Assert.That(breath.AnimationName, Is.EqualTo("Idle"));
            Assert.That((int)LegacySkillKind.Attack, Is.Zero);
            Assert.That((int)LegacySkillKind.Defence, Is.EqualTo(1));
            Assert.That((int)LegacySkillProperty.Slash, Is.Zero);
            Assert.That((int)LegacySkillProperty.Defence, Is.EqualTo(3));
        }

        [Test]
        public void AtZeroAct_ThreeBreathsAppendWithoutCostLaneRotationOrStateChange()
        {
            var duel = new LegacyQueuedDuel();
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(duel.TryQueueLane(2), Is.True);
            Assert.That(duel.Act, Is.Zero);
            int[][] lanes = { Ids(duel.GetLane(0)), Ids(duel.GetLane(1)), Ids(duel.GetLane(2)) };
            int[] forecast = Ids(duel.EnemyQueue);

            for (int count = 1; count <= LegacyQueuedDuel.MaximumBreathsPerTurn; count++)
            {
                Assert.That(duel.TryQueueBreath(), Is.True);
                Assert.That(duel.BreathsQueuedThisTurn, Is.EqualTo(count));
                Assert.That(duel.BreathsRemainingThisTurn, Is.EqualTo(3 - count));
                Assert.That(duel.PlayerQueue[count + 2], Is.SameAs(LegacyCommonActions.Breathe));
            }
            for (int attempt = 0; attempt < 4; attempt++) Assert.That(duel.TryQueueBreath(), Is.False);
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(6));
            Assert.That(duel.Act, Is.Zero);
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
            Assert.That(duel.Enemy.Health, Is.EqualTo(80));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(15));
            Assert.That(duel.UsedStepThisTurn, Is.False);
            for (int lane = 0; lane < 3; lane++) CollectionAssert.AreEqual(lanes[lane], Ids(duel.GetLane(lane)));
            CollectionAssert.AreEqual(forecast, Ids(duel.EnemyQueue));
        }

        [Test]
        public void InterleavingBreaths_PreservesChosenSkillOrderAndOnlyRotatesChosenLanes()
        {
            var duel = new LegacyQueuedDuel();
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(duel.TryQueueBreath(), Is.True);
            CollectionAssert.AreEqual(new[] { -1, 1, -1, 3, -1 }, Ids(duel.PlayerQueue));
            CollectionAssert.AreEqual(new[] { 2, 7, 1 }, Ids(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 4, 8, 3 }, Ids(duel.GetLane(1)));
            CollectionAssert.AreEqual(new[] { 5, 6, 9 }, Ids(duel.GetLane(2)));
            Assert.That(duel.Act, Is.EqualTo(1));
            duel.Commit();
            Assert.That(duel.ResolutionSlotCount, Is.EqualTo(5));
            Assert.That(duel.TryQueueBreath(), Is.False);
            Assert.That(duel.TryQueueLane(2), Is.False);
        }

        [Test]
        public void BreathShiftsAnAttackPastEnemyGuardIntoTheFollowingAttackClash()
        {
            LegacySkill attack = Attack(100, 8, cost: 1);
            LegacySkill[] enemy = { Guard(900, 100), Attack(901, 4) };
            var direct = Duel(new[] { attack }, enemy, 2);
            Assert.That(direct.TryQueueLane(0), Is.True);
            direct.Commit();
            Assert.That(direct.ResolveNextSlot().EnemyHealthDamage, Is.Zero);
            Assert.That(direct.Enemy.Resistance, Is.EqualTo(50));

            var delayed = Duel(new[] { attack }, enemy, 2);
            Assert.That(delayed.TryQueueBreath(), Is.True);
            Assert.That(delayed.TryQueueLane(0), Is.True);
            delayed.Commit();
            LegacySlotResult pause = delayed.ResolveNextSlot();
            Assert.That(pause.PlayerSkill, Is.SameAs(LegacyCommonActions.Breathe));
            Assert.That(pause.PlayerHealthDamage + pause.EnemyHealthDamage, Is.Zero);
            LegacySlotResult clash = delayed.ResolveNextSlot();
            Assert.That(clash.PlayerSkill, Is.SameAs(attack));
            Assert.That(clash.EnemyResistanceDamage, Is.EqualTo(8));
            Assert.That(clash.PlayerResistanceDamage, Is.EqualTo(4));
            Assert.That(delayed.Act, Is.EqualTo(direct.Act));
        }

        [TestCase(50, 12)]
        [TestCase(0, 24)]
        public void EnemyMultiHitAgainstBreath_HitsUnguardedHealthAndNeverClashesResistance(int resistance, int damage)
        {
            var duel = Duel(new[] { Attack(100, 1) }, new[] { Attack(900, 12, hits: 3) }, resistance: resistance);
            Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.HitCount, Is.EqualTo(3));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            for (int hitIndex = 0; hitIndex < 3; hitIndex++)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.PlayerAttacked, Is.False);
                Assert.That(hit.EnemyAttacked, Is.True);
                Assert.That(hit.PlayerResistanceDamage, Is.Zero);
                Assert.That(hit.EnemyHealthDamage + hit.EnemyResistanceDamage, Is.Zero);
                Assert.That(hit.PlayerHealthDamage, Is.EqualTo(damage / 3));
                Assert.That(hit.PlayerPressured, Is.False);
            }
            LegacySlotResult result = duel.CompleteCurrentSlot();
            Assert.That(result.PlayerHealthDamage, Is.EqualTo(damage));
            Assert.That(duel.Player.Resistance, Is.EqualTo(resistance));
            Assert.That(duel.BreathsQueuedThisTurn, Is.EqualTo(1));
        }

        [Test]
        public void BreathsAgainstEmptySlots_OccupyAFullSlotWithoutInventingActionsOrRecovery()
        {
            var duel = Duel(new[] { Attack(100, 1) }, new[] { Guard(900, 1) });
            for (int count = 0; count < 3; count++) Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            Assert.That(duel.ResolutionSlotCount, Is.EqualTo(3));
            for (int index = 0; index < 3; index++)
            {
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                Assert.That(slot.HitCount, Is.EqualTo(1));
                Assert.That(slot.PlayerSkill.IsWait, Is.True);
                if (index > 0) Assert.That(slot.EnemySkill, Is.Null);
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.PlayerAttacked, Is.False);
                Assert.That(hit.EnemyAttacked, Is.False);
                Assert.That(hit.PlayerHealthDamage + hit.EnemyHealthDamage, Is.Zero);
                Assert.That(hit.PlayerResistanceDamage + hit.EnemyResistanceDamage, Is.Zero);
                Assert.That(hit.PlayerDisplayedDamage + hit.EnemyDisplayedDamage, Is.Zero);
                Assert.That(hit.PlayerPushPower + hit.EnemyPushPower, Is.Zero);
                duel.CompleteCurrentSlot();
                Assert.That(duel.IsTurnResolved, Is.EqualTo(index == 2));
            }
            Assert.That(duel.Act, Is.EqualTo(3));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
        }

        [Test]
        public void Breathing_DoesNotConsumePowerRandomnessComparedWithAnEmptyPlayerSlot()
        {
            LegacySkill randomEnemy = new LegacySkill(900, "random", 0, 1, 20, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 1, 0, "");
            var waiting = Duel(new[] { Attack(100, 1) }, new[] { randomEnemy }, 3);
            var empty = Duel(new[] { Attack(100, 1) }, new[] { randomEnemy }, 3);
            for (int count = 0; count < 3; count++) Assert.That(waiting.TryQueueBreath(), Is.True);
            waiting.Commit();
            empty.Commit();
            for (int index = 0; index < 3; index++)
            {
                LegacySlotResult waitResult = waiting.ResolveNextSlot();
                LegacySlotResult emptyResult = empty.ResolveNextSlot();
                Assert.That(waitResult.PlayerHealthDamage, Is.EqualTo(emptyResult.PlayerHealthDamage));
                Assert.That(waitResult.PlayerResistanceDamage, Is.EqualTo(emptyResult.PlayerResistanceDamage));
            }
            Assert.That(waiting.Player.Health, Is.EqualTo(empty.Player.Health));
            Assert.That(waiting.Enemy.Health, Is.EqualTo(empty.Enemy.Health));
            Assert.That(waiting.Act, Is.EqualTo(empty.Act));
            Assert.That(waiting.NextActGain, Is.EqualTo(empty.NextActGain));
        }

        [Test]
        public void NextPlanningTurn_RestoresThreeUsesAndOnlyOrdinaryNaturalActRecovery()
        {
            var duel = Duel(new[] { Attack(100, 1) }, new[] { Guard(900, 1) });
            for (int count = 0; count < 3; count++) Assert.That(duel.TryQueueBreath(), Is.True);
            ResolveTurn(duel);
            Assert.That(duel.BreathsRemainingThisTurn, Is.Zero);
            Assert.That(duel.Act, Is.EqualTo(3));
            duel.BeginNextTurn();
            Assert.That(duel.RoundNumber, Is.EqualTo(2));
            Assert.That(duel.BreathsQueuedThisTurn, Is.Zero);
            Assert.That(duel.BreathsRemainingThisTurn, Is.EqualTo(3));
            Assert.That(duel.PlayerQueue, Is.Empty);
            Assert.That(duel.Act, Is.EqualTo(6));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.Act, Is.EqualTo(6));
        }

        [Test]
        public void Reset_RestoresBreathsStateQueueOpeningActAndForecast()
        {
            var duel = new LegacyQueuedDuel();
            Assert.That(duel.TryQueueLane(0), Is.True);
            for (int count = 0; count < 3; count++) Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            Assert.That(duel.TryQueueBreath(), Is.False);
            duel.Reset();
            Assert.That(duel.BreathsQueuedThisTurn, Is.Zero);
            Assert.That(duel.BreathsRemainingThisTurn, Is.EqualTo(3));
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(duel.PlayerQueue, Is.Empty);
            Assert.That(duel.CurrentSlot, Is.Null);
            Assert.That(duel.Act, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, Ids(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 1, 2 }, Ids(duel.EnemyQueue));
            Assert.That(duel.TryQueueBreath(), Is.True);
        }

        [Test]
        public void PressureOnBreath_NeverSucceedsOrGuardsAndItsMissCostsTheNaturalRecovery()
        {
            var duel = Duel(new[] { Attack(100, 1) }, new[] { Attack(900, 8) });
            Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out bool success), Is.True);
            Assert.That(success, Is.False);
            Assert.That(slot.PressureSucceeded, Is.False);
            Assert.That(duel.UsedStepThisTurn, Is.True);
            Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.EqualTo(8));
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(3));
            Assert.That(duel.BreathsRemainingThisTurn, Is.EqualTo(3));
            Assert.That(duel.UsedStepThisTurn, Is.False);
        }

        [Test]
        public void DodgeDuringBreath_StillAvoidsEveryEnemyHitAndASuccessCostsNoAct()
        {
            var duel = Duel(new[] { Attack(100, 1) }, new[] { Attack(900, 12, hits: 3) });
            Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool success), Is.True);
            Assert.That(success, Is.True);
            for (int hitIndex = 0; hitIndex < 3; hitIndex++)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.PlayerDodged, Is.True);
                Assert.That(hit.PlayerAttacked, Is.False);
                Assert.That(hit.PlayerHealthDamage + hit.PlayerResistanceDamage, Is.Zero);
                Assert.That(hit.PlayerDisplayedDamage + hit.PlayerPushPower, Is.Zero);
            }
            duel.CompleteCurrentSlot();
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(6));
            Assert.That(duel.BreathsRemainingThisTurn, Is.EqualTo(3));
        }

        [Test]
        public void BreathConsumesAOneSlotBuffWithoutApplyingItToTheFollowingAttack()
        {
            LegacySkill ready = Attack(10, 4);
            var duel = Duel(new[] { ready, Attack(100, 20) },
                new[] { Guard(900, 100), Guard(901, 1), Attack(902, 1) }, 3);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacySlotResult waiting = duel.ResolveNextSlot();
            Assert.That(waiting.EnemyHealthDamage + waiting.EnemyResistanceDamage, Is.Zero);
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(20));
        }

        [Test]
        public void ExistingReceivedDamageBuffsAdvanceOverBreathingLikeAnyOtherSlot()
        {
            var duel = Duel(new[] { Attack(12, 1), Attack(100, 1) },
                new[] { Attack(900, 4) }, 3);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            Assert.That(duel.ResolveNextSlot().PlayerResistanceDamage, Is.EqualTo(4));
            LegacySlotResult waiting = duel.ResolveNextSlot();
            Assert.That(waiting.PlayerHealthDamage, Is.EqualTo(6));
            Assert.That(waiting.PlayerResistanceDamage, Is.Zero);
            Assert.That(duel.ResolveNextSlot().PlayerResistanceDamage, Is.EqualTo(4));
            Assert.That(duel.NextActGain, Is.EqualTo(6), "Only the existing Forward action grants its bonus.");
        }

        [Test]
        public void AWaitInALane_CannotBypassTheCommonActionLimitOrRotateTheLane()
        {
            var duel = Duel(new[] { LegacyCommonActions.Breathe }, new[] { Guard(900, 1) });
            Assert.That(duel.TryQueueLane(0), Is.False);
            Assert.That(duel.PlayerQueue, Is.Empty);
            Assert.That(duel.GetLane(0)[0], Is.SameAs(LegacyCommonActions.Breathe));
            for (int count = 0; count < 3; count++) Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.TryQueueLane(0), Is.False);
            Assert.That(duel.TryQueueBreath(), Is.False);
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(3));
            Assert.That(duel.Act, Is.EqualTo(3));
        }

        [Test]
        public void LethalMultiHitAgainstBreath_CompletesTheCurrentSlotThenStopsFurtherBreaths()
        {
            var duel = new LegacyQueuedDuel(3, 50, 100, 50, new[] { Attack(100, 1) },
                new[] { Attack(900, 12, hits: 3) }, new[] { 1 });
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            LegacyHitResult first = duel.ResolveNextHit();
            Assert.That(duel.Player.Health, Is.Zero);
            Assert.That(first.IsFinalHit, Is.False);
            Assert.That(duel.IsFinished, Is.False);
            duel.ResolveNextHit();
            Assert.That(duel.ResolveNextHit().IsFinalHit, Is.True);
            Assert.That(duel.CompleteCurrentSlot().Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(duel.IsFinished, Is.True);
            Assert.That(duel.LastResolvedSlot, Is.Zero);
            Assert.That(duel.TryQueueBreath(), Is.False);
            Assert.That(duel.BreathsQueuedThisTurn, Is.EqualTo(2));
        }

        private static LegacySkill Attack(int id, int power, int hits = 1, int cost = 0)
            => new LegacySkill(id, "attack", cost, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 0, power, power, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 0, "");

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy, int count = 1, int resistance = 50)
            => new LegacyQueuedDuel(1000, resistance, 1000, resistance, player, enemy, new[] { count }, randomSeed: 1);

        private static int[] Ids(IReadOnlyList<LegacySkill> skills)
        {
            var ids = new int[skills.Count];
            for (int index = 0; index < ids.Length; index++) ids[index] = skills[index].Id;
            return ids;
        }

        private static void ResolveTurn(LegacyQueuedDuel duel)
        {
            duel.Commit();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
        }
    }
}
