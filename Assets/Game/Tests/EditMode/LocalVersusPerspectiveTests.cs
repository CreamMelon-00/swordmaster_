using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LocalVersusPerspectiveTests
    {
        [Test]
        public void Host_SeesLeftAsPlayer_AndGuest_SeesTheMatchMirrored()
        {
            LegacySkill cycle = LegacySkillDefinitions.Skill(46);
            var match = NewMatch(new[] { Attack(100, 1, 5), cycle }, new[] { Attack(200, 2, 5) });
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(host.IsMyTurn, Is.True);
            Assert.That(guest.IsOpponentsTurn, Is.True);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(host.IsMyTurn, Is.True, "The opening player keeps the whole planning phase.");
            Assert.That(guest.IsMyTurn, Is.False);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueBreath(1), Is.True);

            Assert.That(host.Player, Is.SameAs(match.Left.State));
            Assert.That(host.Enemy, Is.SameAs(match.Right.State));
            Assert.That(guest.Player, Is.SameAs(match.Right.State));
            Assert.That(guest.Enemy, Is.SameAs(match.Left.State));
            Assert.That(host.PlayerQueue, Is.SameAs(match.Left.Queue));
            Assert.That(host.EnemyQueue, Is.SameAs(match.Right.Queue));
            Assert.That(guest.PlayerQueue[0].IsWait, Is.True);
            Assert.That(guest.EnemyQueue[0].Id, Is.EqualTo(100), "The opponent's reservations are public and live.");
            Assert.That(host.Act, Is.EqualTo(2));
            Assert.That(guest.Act, Is.EqualTo(3));
            Assert.That(host.PlayerMaximumAct, Is.EqualTo(match.Left.MaximumAct));
            Assert.That(host.BreathsRemainingThisTurn, Is.EqualTo(3));
            Assert.That(guest.BreathsRemainingThisTurn, Is.EqualTo(2));
            Assert.That(host.GetLane(1)[0].Id, Is.EqualTo(46));
            Assert.That(guest.GetLane(0)[0].Id, Is.EqualTo(200));
            Assert.That(guest.GetLane(1), Is.Empty);
            Assert.That(host.EffectiveCost(cycle), Is.EqualTo(match.Left.EffectiveCost(cycle)));
            Assert.That(guest.EffectiveCost(match.Right.GetLane(0)[0]), Is.EqualTo(2));
            Assert.That(host.CycleUses(46), Is.Zero);
            host.EffectivePowerRange(cycle, out int min, out int max);
            match.Left.EffectivePowerRange(cycle, out int leftMin, out int leftMax);
            Assert.That(min, Is.EqualTo(leftMin));
            Assert.That(max, Is.EqualTo(leftMax));
            Assert.That(host.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(guest.RoundNumber, Is.EqualTo(1));
            Assert.That(host.IsMyTurn, Is.False);
            Assert.That(guest.IsMyTurn, Is.True);
            Assert.That(host.EnemyQueue[0].IsWait, Is.True);
            Assert.That(match.CommittedPlannerCount, Is.EqualTo(1));
        }

        [Test]
        public void SidesAndPairs_MapToEachViewersOwnSide()
        {
            var match = NewMatch(new[] { Attack(100, 1, 5) }, new[] { Attack(200, 1, 5) });
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(host.IsPlayerSide(0), Is.True);
            Assert.That(host.IsPlayerSide(1), Is.False);
            Assert.That(guest.IsPlayerSide(1), Is.True);
            Assert.That(guest.IsPlayerSide(0), Is.False);
            Assert.That(host.PlayerIndex(true), Is.Zero);
            Assert.That(guest.PlayerIndex(true), Is.EqualTo(1));
            Assert.That(guest.PlayerIndex(false), Is.Zero);
            Assert.That(host.Me, Is.SameAs(match.Left));
            Assert.That(guest.Me, Is.SameAs(match.Right));
            Assert.That(guest.Opponent, Is.SameAs(match.Left));
            Assert.That(host.ForPlayer(12.5f, 4f), Is.EqualTo(12.5f));
            Assert.That(guest.ForPlayer(12.5f, 4f), Is.EqualTo(4f));
            Assert.That(guest.ForEnemy("left", "right"), Is.EqualTo("left"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => host.IsPlayerSide(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new LocalVersusPerspective(match, 2));
        }

        [Test]
        public void CurrentSlot_KeepsOneViewPerSlot_WithEachViewersSkillsAndFeedback()
        {
            LegacySkill guard = LegacySkillDefinitions.Skill(7);
            LegacySkill hit = LegacySkillDefinitions.Skill(5);
            var match = NewMatch(new[] { guard }, new[] { hit, Attack(201, 1, 3) });
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 2), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Commit(match);
            Assert.That(host.CurrentSlot, Is.Null);
            Assert.That(((ILegacyDuelView)guest).CurrentSlot, Is.Null);

            LocalVersusSlot slot = match.BeginNextSlot();
            LocalVersusPerspectiveSlot hostSlot = host.CurrentSlot;
            LocalVersusPerspectiveSlot guestSlot = guest.CurrentSlot;
            Assert.That(host.CurrentSlot, Is.SameAs(hostSlot), "The HUD keeps a slot's feedback while the same view comes back.");
            Assert.That(((ILegacyDuelView)host).CurrentSlot, Is.SameAs(hostSlot));
            Assert.That(host.Slot(slot), Is.SameAs(hostSlot));
            Assert.That(hostSlot.Source, Is.SameAs(slot));
            Assert.That(hostSlot.SlotIndex, Is.Zero);
            Assert.That(hostSlot.PlayerSkill, Is.SameAs(guard));
            Assert.That(hostSlot.EnemySkill, Is.SameAs(hit));
            Assert.That(guestSlot.PlayerSkill, Is.SameAs(hit));
            Assert.That(guestSlot.EnemySkill, Is.SameAs(guard));
            Assert.That(hostSlot.PlayerFeedback, Is.SameAs(slot.LeftFeedback));
            Assert.That(hostSlot.EnemyFeedback, Is.SameAs(slot.RightFeedback));
            Assert.That(guestSlot.PlayerFeedback, Is.SameAs(slot.RightFeedback));
            Assert.That(guestSlot.EnemyFeedback, Is.SameAs(slot.LeftFeedback));
            // The guard met its hit: its ACT bonus belongs to the host's player and to the guest's enemy.
            Assert.That(hostSlot.PlayerFeedback.ActGainGranted, Is.EqualTo(2));
            Assert.That(guestSlot.EnemyFeedback.ActGainGranted, Is.EqualTo(2));
            Assert.That(guestSlot.PlayerFeedback.ActGainGranted, Is.Zero);

            while (!slot.IsResolved) match.ResolveNextHit();
            Assert.That(guestSlot.HitsResolved, Is.EqualTo(slot.HitCount));
            Assert.That(guestSlot.IsResolved, Is.True);
            match.CompleteCurrentSlot();
            Assert.That(host.CurrentSlot, Is.Null);
            match.BeginNextSlot();
            Assert.That(host.CurrentSlot, Is.Not.SameAs(hostSlot));
            Assert.That(host.CurrentSlot.SlotIndex, Is.EqualTo(1));
            Assert.That(host.CurrentSlot.PlayerSkill, Is.Null, "The host reserved one skill; its second slot is empty.");
            Assert.That(guest.CurrentSlot.PlayerSkill.Id, Is.EqualTo(201));
        }

        [Test]
        public void Hits_KeepSinglePlayerDamageNumbersAndPush_AndMapToEachViewer()
        {
            var match = NewMatch(new[] { Attack(100, 1, 30) }, new[] { Attack(200, 1, 10) },
                health: 5, resistance: 0);
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Commit(match);
            match.BeginNextSlot();
            LocalVersusHit hit = match.ResolveNextHit();
            // Broken resistance doubles the number a single-player hit shows; health only falls to zero.
            Assert.That(hit.RightHealthDamage, Is.EqualTo(5));
            Assert.That(hit.RightDisplayedDamage, Is.EqualTo(60));
            Assert.That(hit.RightPushPower, Is.EqualTo(30));
            Assert.That(hit.LeftDisplayedDamage, Is.EqualTo(20));
            Assert.That(hit.LeftPushPower, Is.EqualTo(10));

            LocalVersusPerspectiveHit hostHit = host.Hit(hit);
            LocalVersusPerspectiveHit guestHit = guest.Hit(hit);
            Assert.That(hostHit.Source, Is.SameAs(hit));
            Assert.That(hostHit.PlayerAttacked && hostHit.EnemyAttacked, Is.True);
            Assert.That(hostHit.EnemyHealthDamage, Is.EqualTo(5));
            Assert.That(hostHit.EnemyDisplayedDamage, Is.EqualTo(60));
            Assert.That(hostHit.EnemyPushPower, Is.EqualTo(30));
            Assert.That(hostHit.PlayerDisplayedDamage, Is.EqualTo(20));
            Assert.That(hostHit.PlayerHealthDamage, Is.EqualTo(5));
            Assert.That(guestHit.PlayerDisplayedDamage, Is.EqualTo(60));
            Assert.That(guestHit.PlayerPushPower, Is.EqualTo(30));
            Assert.That(guestHit.EnemyDisplayedDamage, Is.EqualTo(20));
            Assert.That(guestHit.PlayerSkill.Id, Is.EqualTo(200));
            Assert.That(guestHit.EnemySkill.Id, Is.EqualTo(100));
            Assert.That(hostHit.Outcome, Is.EqualTo(DuelMatchOutcome.Draw));
            Assert.That(guestHit.IsFinalHit, Is.True);
            Assert.That(host.Hit(null), Is.Null);
        }

        [Test]
        public void ResistanceHeldHit_ShowsTheResistanceTaken_AndAnUnansweredSideShowsNothing()
        {
            var match = NewMatch(new[] { Attack(100, 1, 10) }, new[] { Attack(200, 1, 10) });
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueBreath(1), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Commit(match);

            // Slot 1: the host's attack meets the guest's breath and reaches health.
            match.BeginNextSlot();
            LocalVersusPerspectiveHit first = guest.Hit(match.ResolveNextHit());
            Assert.That(first.EnemyAttacked, Is.True);
            Assert.That(first.PlayerAttacked, Is.False);
            Assert.That(first.PlayerHealthDamage, Is.EqualTo(10));
            Assert.That(first.PlayerDisplayedDamage, Is.EqualTo(10));
            Assert.That(first.EnemyDisplayedDamage, Is.Zero);
            Assert.That(first.EnemyPushPower, Is.Zero);
            match.CompleteCurrentSlot();

            // Slot 2: the guest's attack alone, against no skill: health again.
            match.BeginNextSlot();
            LocalVersusPerspectiveHit second = guest.Hit(match.ResolveNextHit());
            Assert.That(second.PlayerAttacked, Is.True);
            Assert.That(second.EnemyDisplayedDamage, Is.EqualTo(10));
            match.CompleteCurrentSlot();

            // A clash against whole resistance shows the resistance it took.
            match.BeginNextTurn();
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(match.TryCommit(1), Is.True);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Commit(match);
            match.BeginNextSlot();
            LocalVersusPerspectiveHit clash = guest.Hit(match.ResolveNextHit());
            Assert.That(clash.PlayerResistanceDamage, Is.EqualTo(10));
            Assert.That(clash.PlayerHealthDamage, Is.Zero);
            Assert.That(clash.PlayerDisplayedDamage, Is.EqualTo(10));
            Assert.That(clash.EnemyDisplayedDamage, Is.EqualTo(clash.EnemyResistanceDamage));
        }

        [Test]
        public void Outcome_IsTheViewersWinOrLoss()
        {
            var match = NewMatch(new[] { Attack(100, 1, 30) }, new[] { Attack(200, 1, 1) },
                health: 5, resistance: 0);
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(host.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueBreath(1), Is.True);
            Commit(match);
            match.ResolveNextSlot();
            Assert.That(match.Outcome, Is.EqualTo(LocalVersusOutcome.LeftVictory));
            Assert.That(host.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(guest.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(guest.ToDuelOutcome(LocalVersusOutcome.RightVictory), Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(host.ToDuelOutcome(LocalVersusOutcome.RightVictory), Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(guest.ToDuelOutcome(LocalVersusOutcome.Draw), Is.EqualTo(DuelMatchOutcome.Draw));
        }

        [Test]
        public void CountersAndMeshReadEmpty_AndEveryFeatureIsOpen()
        {
            var match = NewMatch(new[] { Attack(100, 1, 5) }, new[] { Attack(200, 1, 5) });
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(guest.Features, Is.EqualTo(CombatFeature.All));
            Assert.That(guest.PlayerCounter, Is.Null);
            Assert.That(guest.EnemyCounter, Is.Null);
            Assert.That(guest.PlayerCountersRemaining, Is.Zero);
            Assert.That(guest.EnemyCountersRemaining, Is.Zero);
            var slots = new List<int> { 0, 1 };
            guest.ForecastEnemyCounterSlots(slots);
            Assert.That(slots, Is.Empty);
            slots.Add(2);
            guest.ForecastPlayerCounterSlots(slots);
            Assert.That(slots, Is.Empty);
            Assert.That(guest.EnemyCounterFacing(match.Right.GetLane(0)[0]), Is.Null);
            Assert.That(guest.MeshPercent, Is.Zero);
            Assert.That(guest.PlayerMesh(0).IsMeshed, Is.False);
            Assert.That(guest.PlayerMesh(0).SlotIndex, Is.Zero);
            LegacyMeshSlot ifQueued = guest.PlayerMeshIfQueued(0);
            Assert.That(ifQueued.IsMeshed, Is.False);
            Assert.That(ifQueued.SlotIndex, Is.EqualTo(1), "The next slot of the viewer's own queue.");
        }

        [Test]
        public void CancelBoost_IsReadForTheViewersQueue_AsSinglePlayReadsIt()
        {
            LegacySkill attack = Attack(100, 0, 6);
            LegacySkill cancel = LegacySkillDefinitions.Skill(45);
            var match = NewMatch(new[] { Attack(200, 1, 1) }, new[] { attack, cancel });
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 2), Is.True);
            Assert.That(guest.QueuePowerSourceSlotIndex, Is.Zero);
            Assert.That(guest.QueuePowerRemovedSkillCount, Is.EqualTo(2));
            Assert.That(guest.QueuePowerTargetSlotIndex, Is.EqualTo(-1), "No technique follows the cancel yet.");
            Assert.That(match.TryQueueBreath(1), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);

            // The same reservations in a single-player duel read the same boost.
            var single = new LegacyQueuedDuel(100, 50, 100, 50, new[] { attack, cancel },
                new[] { Attack(200, 1, 1) }, new[] { 1 });
            Assert.That(single.TryQueueLane(0), Is.True);
            Assert.That(single.TryQueueLane(0), Is.True);
            Assert.That(single.TryQueueLane(2), Is.True);
            Assert.That(single.TryQueueBreath(), Is.True);
            Assert.That(single.TryQueueLane(0), Is.True);
            ILegacyDuelView expected = single;
            Assert.That(guest.QueuePowerSourceSlotIndex, Is.EqualTo(expected.QueuePowerSourceSlotIndex));
            Assert.That(guest.QueuePowerRemovedSkillCount, Is.EqualTo(expected.QueuePowerRemovedSkillCount));
            Assert.That(guest.QueuePowerTargetSlotIndex, Is.EqualTo(expected.QueuePowerTargetSlotIndex));
            Assert.That(guest.QueuePowerTargetSlotIndex, Is.EqualTo(2), "The breath is skipped.");
            for (int slot = -1; slot < 4; slot++)
                Assert.That(guest.QueuePowerMultiplierForSlot(slot), Is.EqualTo(expected.QueuePowerMultiplierForSlot(slot)),
                    "slot " + slot);
            Assert.That(guest.QueuePowerMultiplierForSlot(2), Is.EqualTo(2));
            Assert.That(guest.QueuePowerSourceSlotIndex, Is.EqualTo(match.Right.QueuePowerSourceSlotIndex));

            Assert.That(host.QueuePowerSourceSlotIndex, Is.EqualTo(-1));
            Assert.That(host.QueuePowerRemovedSkillCount, Is.Zero);
            Assert.That(host.QueuePowerTargetSlotIndex, Is.EqualTo(-1));
            Assert.That(host.QueuePowerMultiplierForSlot(2), Is.EqualTo(1));
        }

        [Test]
        public void OpponentsCyclingSkill_ReadsTheOpponentsUses_AndTheCampaignEnemyReadsTheSheet()
        {
            LegacySkill cycle = LegacySkillDefinitions.Skill(46);
            var match = NewMatch(new[] { Attack(100, 1, 5) }, new[] { cycle });
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.TryPass(0), Is.True);
            Assert.That(match.TryQueueLane(1, cycle.LaneIndex), Is.True);
            Commit(match);
            while (!match.IsTurnResolved) match.ResolveNextSlot();
            match.BeginNextTurn();
            Assert.That(match.Right.CycleUses(46), Is.EqualTo(1));

            // The host's opponent is Right: its card shows Right's uses, cost and power, as Right's own desk does.
            Assert.That(host.EnemyCycleUses(46), Is.EqualTo(1));
            Assert.That(host.EnemyEffectiveCost(cycle), Is.EqualTo(match.Right.EffectiveCost(cycle)));
            Assert.That(host.EnemyEffectiveCost(cycle), Is.EqualTo(cycle.Cost + 1));
            host.EnemyEffectivePowerRange(cycle, out int min, out int max);
            match.Right.EffectivePowerRange(cycle, out int rightMin, out int rightMax);
            Assert.That(min, Is.EqualTo(rightMin));
            Assert.That(max, Is.EqualTo(rightMax));
            Assert.That(min, Is.GreaterThan(cycle.MinPower));
            Assert.That(host.CycleUses(46), Is.Zero, "The host's own count is Left's.");
            Assert.That(guest.CycleUses(46), Is.EqualTo(1));
            Assert.That(guest.EnemyCycleUses(46), Is.Zero, "The guest's opponent is Left.");
            Assert.That(guest.EnemyEffectiveCost(cycle), Is.EqualTo(cycle.Cost));

            // The campaign's scripted enemy never cycles: whatever the player has used, its side reads the sheet.
            var single = new LegacyQueuedDuel(100, 50, 100, 50, new[] { cycle }, new[] { Attack(200, 1, 1) }, new[] { 1 });
            Assert.That(single.TryQueueLane(cycle.LaneIndex), Is.True);
            single.Commit();
            while (!single.IsTurnResolved) single.ResolveNextSlot();
            single.BeginNextTurn();
            Assert.That(single.CycleUses(46), Is.EqualTo(1));
            ILegacyDuelView view = single;
            Assert.That(view.EnemyCycleUses(46), Is.Zero);
            Assert.That(view.EnemyEffectiveCost(cycle), Is.EqualTo(cycle.Cost));
            view.EnemyEffectivePowerRange(cycle, out min, out max);
            Assert.That(min, Is.EqualTo(cycle.MinPower));
            Assert.That(max, Is.EqualTo(cycle.MaxPower));
        }

        [Test]
        public void SinglePlayerDuel_ReadsTheSameThroughTheView()
        {
            var duel = new LegacyQueuedDuel(100, 50, 80, 15, new[] { Attack(100, 1, 5) },
                new[] { Attack(200, 1, 5) }, new[] { 1 });
            ILegacyDuelView view = duel;
            Assert.That(view.CurrentSlot, Is.Null);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(view.Act, Is.EqualTo(duel.Act));
            Assert.That(view.PlayerQueue, Is.SameAs(duel.PlayerQueue));
            Assert.That(view.Features, Is.EqualTo(duel.Features));
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(view.CurrentSlot, Is.SameAs(slot));
            Assert.That(view.CurrentSlot.PlayerFeedback, Is.SameAs(slot.PlayerFeedback));
            Assert.That(view.CurrentSlot.EnemySkill, Is.SameAs(slot.EnemySkill));
        }

        private static LocalVersusMatch NewMatch(LegacySkill[] left, LegacySkill[] right,
            int health = 100, int resistance = 50)
            => new LocalVersusMatch(left, right, health, resistance, health, resistance, randomSeed: 17);

        private static LegacySkill Attack(int id, int cost, int power, int hits = 1)
            => new LegacySkill(id, "Test", cost, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty);

        private static void Commit(LocalVersusMatch match)
        {
            while (match.Phase == LegacyDuelPhase.Planning)
                Assert.That(match.TryCommit(match.CurrentPlanner), Is.True);
        }
    }
}
