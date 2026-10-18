using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    public sealed class ActStealTests
    {
        [TestCase(7, false)]
        [TestCase(8, true)]
        [TestCase(10, true)]
        public void Threshold_UsesCurrentUnspentAct_WhenTheSkillStarts(int targetAct, bool activates)
        {
            var match = Match(new[] { Steal() }, new[] { Attack(900, 10 - targetAct) });
            StockBoth(match);
            Plan(match, player => Queue(match, player, player == 0 ? 2 : 0));
            Assert.That(match.Left.Act, Is.EqualTo(8));
            Assert.That(match.Right.Act, Is.EqualTo(targetAct));

            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(match.Right.Act, Is.EqualTo(targetAct - (activates ? 3 : 0)));
            Assert.That(match.Left.Act, Is.EqualTo(activates ? 10 : 8));
            Assert.That(slot.LeftFeedback.ActStolen, Is.EqualTo(activates ? 3 : 0));
            Assert.That(slot.LeftFeedback.ActReceived, Is.EqualTo(activates ? 2 : 0));
            Assert.That(slot.LeftFeedback.ConditionMet, Is.EqualTo(activates));
            Assert.That(slot.LeftFeedback.EffectActivated, Is.EqualTo(activates));
            Assert.That(slot.LeftFeedback.ActGainGranted, Is.Zero);
            Assert.That(match.Left.NextActGain, Is.EqualTo(3));
            Assert.That(match.Right.NextActGain, Is.EqualTo(3));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Reservation_DoesNotSteal_AndOpponentCanSpendBelowThresholdBeforeExecution(int owner)
        {
            var match = Match(owner == 0 ? new[] { Steal() } : new[] { Attack(900, 3) },
                owner == 1 ? new[] { Steal() } : new[] { Attack(900, 3) });
            StockBoth(match);
            Plan(match, player =>
            {
                if (player == owner)
                {
                    Queue(match, player, 2);
                    Assert.That(match.GetFighter(1 - owner).Act, Is.EqualTo(player == match.OpeningPlayer ? 10 : 7));
                }
                else Queue(match, player, 0);
            });
            LocalVersusSlot slot = match.BeginNextSlot();
            LegacySkillFeedback feedback = owner == 0 ? slot.LeftFeedback : slot.RightFeedback;
            Assert.That(feedback.ActStolen, Is.Zero);
            Assert.That(match.GetFighter(owner).Act, Is.EqualTo(8));
            Assert.That(match.GetFighter(1 - owner).Act, Is.EqualTo(7));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Steal_IsSymmetric_TransfersCurrentAct_AndCarriesIntoNextTurn(int owner)
        {
            LegacySkill[] thief = { Steal(), Attack(900, 3) };
            LegacySkill[] other = { Attack(901, 1) };
            var match = Match(owner == 0 ? thief : other, owner == 1 ? thief : other);
            for (int turn = 0; turn < 3; turn++)
            {
                Plan(match, player => { if (player == owner) Queue(match, player, 0); });
                ResolveTurn(match);
                match.BeginNextTurn();
            }
            Assert.That(match.GetFighter(owner).Act, Is.EqualTo(3));
            Assert.That(match.GetFighter(1 - owner).Act, Is.EqualTo(10));
            Plan(match, player => { if (player == owner) Queue(match, player, 2); });
            LocalVersusSlot slot = match.BeginNextSlot();
            LegacySkillFeedback feedback = owner == 0 ? slot.LeftFeedback : slot.RightFeedback;
            Assert.That(feedback.ActStolen, Is.EqualTo(3));
            Assert.That(feedback.ActReceived, Is.EqualTo(3));
            Assert.That(match.GetFighter(owner).Act, Is.EqualTo(4));
            Assert.That(match.GetFighter(1 - owner).Act, Is.EqualTo(7));
            Assert.That(match.GetFighter(owner).NextActGain, Is.EqualTo(3));
            ResolveTurn(match);
            match.BeginNextTurn();
            Assert.That(match.GetFighter(owner).Act, Is.EqualTo(7));
            Assert.That(match.GetFighter(1 - owner).Act, Is.EqualTo(10));
            Assert.That(feedback.ActStolen, Is.EqualTo(3), "The feedback remains the immutable use snapshot.");
        }

        [TestCase(2, 2)]
        [TestCase(1, 1)]
        [TestCase(0, 0)]
        public void OwnActCap_DiscardsExcessGain_WithoutReducingTargetLoss(int cost, int received)
        {
            var match = Match(new[] { Steal(cost: cost) }, new[] { Attack(900, 1) });
            StockBoth(match);
            Plan(match, player => { if (player == 0) Queue(match, player, 2); });
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(slot.LeftFeedback.ActStolen, Is.EqualTo(3));
            Assert.That(slot.LeftFeedback.ActReceived, Is.EqualTo(received));
            Assert.That(match.Left.Act, Is.EqualTo(10));
            Assert.That(match.Right.Act, Is.EqualTo(7));
            Assert.That(slot.LeftFeedback.EffectActivated && slot.LeftFeedback.ConditionMet, Is.True);
        }

        [TestCase(0, 8, 8)]
        [TestCase(1, 8, 8)]
        public void SimultaneousSteals_ReadBothPreSlotValues_AndOpenRoomBeforeApplyingCaps(int opening, int leftAct, int rightAct)
        {
            var match = Match(new[] { Steal() }, new[] { Steal() }, opening: opening);
            StockBoth(match);
            Plan(match, player => Queue(match, player, 2));
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(match.Left.Act, Is.EqualTo(leftAct));
            Assert.That(match.Right.Act, Is.EqualTo(rightAct));
            Assert.That(slot.LeftFeedback.ActStolen, Is.EqualTo(3));
            Assert.That(slot.RightFeedback.ActStolen, Is.EqualTo(3));
            Assert.That(slot.LeftFeedback.ActReceived, Is.EqualTo(3));
            Assert.That(slot.RightFeedback.ActReceived, Is.EqualTo(3));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SimultaneousSteals_CannotUseNewlyReceivedActToReachTheThreshold(int lowOwner)
        {
            var match = Match(new[] { Steal(cost: lowOwner == 0 ? 3 : 0) },
                new[] { Steal(cost: lowOwner == 1 ? 3 : 0) });
            StockBoth(match);
            Plan(match, player => Queue(match, player, 2));
            LocalVersusSlot slot = match.BeginNextSlot();
            LegacySkillFeedback low = lowOwner == 0 ? slot.LeftFeedback : slot.RightFeedback;
            LegacySkillFeedback high = lowOwner == 0 ? slot.RightFeedback : slot.LeftFeedback;
            Assert.That(low.ActStolen, Is.EqualTo(3), "The low-ACT fighter targets the opponent's original 10 ACT.");
            Assert.That(high.ActStolen, Is.Zero, "The high-ACT fighter targets the opponent's original 7 ACT, not its new 10.");
            Assert.That(match.GetFighter(lowOwner).Act, Is.EqualTo(10));
            Assert.That(match.GetFighter(1 - lowOwner).Act, Is.EqualTo(7));
        }

        [Test]
        public void RepeatedSkills_ReevaluateTheThreshold_AndMultiHitDoesNotRepeatTheTransfer()
        {
            var match = Match(new[] { Steal(hits: 4) }, new[] { Attack(900, 1) });
            StockBoth(match);
            Plan(match, player =>
            {
                if (player != 0) return;
                Queue(match, player, 2);
                Queue(match, player, 2);
            });
            LocalVersusSlot first = match.BeginNextSlot();
            Assert.That(first.HitCount, Is.EqualTo(4));
            Assert.That(first.LeftFeedback.ActStolen, Is.EqualTo(3));
            for (int hit = 0; hit < 4; hit++)
            {
                match.ResolveNextHit();
                Assert.That(match.Left.Act, Is.EqualTo(9));
                Assert.That(match.Right.Act, Is.EqualTo(7));
            }
            match.CompleteCurrentSlot();
            LocalVersusSlot second = match.BeginNextSlot();
            Assert.That(second.LeftFeedback.ActStolen, Is.Zero, "The later cast sees the target's new 7 ACT.");
            Assert.That(second.LeftFeedback.EffectActivated, Is.False);
            Assert.That(first.LeftFeedback.ActStolen, Is.EqualTo(3));
        }

        [Test]
        public void QueueCancel_RemovesTheSkillWithoutApplyingItsUseEffect()
        {
            var match = Match(new[] { Steal(), LegacySkillDefinitions.Skill(45) }, new[] { Attack(900, 1) });
            StockBoth(match);
            Plan(match, player =>
            {
                if (player != 0) return;
                Queue(match, player, 2);
                Queue(match, player, 2);
            });
            Assert.That(match.Left.Queue.Select(skill => skill.Id), Is.EqualTo(new[] { 45 }));
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That(slot.LeftFeedback.ActStolen, Is.Zero);
            Assert.That(match.Right.Act, Is.EqualTo(10));
        }

        [Test]
        public void ResistanceBreak_CancelsLaterSteal_ButLeavesCurrentCastIntact()
        {
            var match = Match(new[] { Attack(900, 0), Steal() }, new[] { Attack(901, 0, power: 100) }, resistance: 50);
            StockBoth(match);
            Plan(match, player =>
            {
                Queue(match, player, 0);
                if (player == 0) Queue(match, player, 2);
            });
            match.ResolveNextSlot();
            Assert.That(match.Left.BreakCanceledFromSlotIndex, Is.EqualTo(1));
            LocalVersusSlot canceled = match.BeginNextSlot();
            Assert.That(canceled.LeftSkill, Is.Null);
            Assert.That(canceled.LeftFeedback.ActStolen, Is.Zero);
            Assert.That(match.Right.Act, Is.EqualTo(10));
        }

        [Test]
        public void Defeat_BeforeTheReservedSteal_PreventsItsUseEffect()
        {
            var match = Match(new[] { Attack(900, 0), Steal() }, new[] { Attack(901, 0, power: 5000) });
            StockBoth(match);
            Plan(match, player =>
            {
                Queue(match, player, 0);
                if (player == 0) Queue(match, player, 2);
            });
            match.ResolveNextSlot();
            Assert.That(match.IsFinished, Is.True);
            Assert.That(match.Outcome, Is.EqualTo(LocalVersusOutcome.RightVictory));
            Assert.That(match.Right.Act, Is.EqualTo(10));
            Assert.Throws<InvalidOperationException>(() => match.BeginNextSlot());
        }

        [Test]
        public void FeedbackCopies_PreserveActualActTransfer_WhenConditionalDamageIsArmed()
        {
            WithStealDamageCondition(() =>
            {
                var match = Match(new[] { Steal() }, new[] { Attack(900, 1) }, resistance: 0);
                StockBoth(match);
                Plan(match, player => { if (player == 0) Queue(match, player, 2); });
                LegacySkillFeedback feedback = match.BeginNextSlot().LeftFeedback;
                Assert.That(feedback.ActStolen, Is.EqualTo(3));
                Assert.That(feedback.ActReceived, Is.EqualTo(2));
                Assert.That(feedback.ConditionalDamagePercent, Is.EqualTo(200));
                Assert.That(feedback.EffectActivated && feedback.ConditionMet, Is.True);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CampaignEnemies_HaveNoActWallet_AndNeverParticipateInActSteal(bool playerUsesSteal)
        {
            var duel = new LegacyQueuedDuel(1000, 100, 1000, 100,
                new[] { playerUsesSteal ? Steal() : Attack(900, 0) },
                new[] { playerUsesSteal ? Attack(900, 0) : Steal() }, new[] { 1 }, playerActGainBonus: 7);
            Assert.That(duel.Act, Is.EqualTo(10));
            Assert.That(duel.TryQueueLane(playerUsesSteal ? 2 : 0), Is.True);
            int currentAct = duel.Act, nextAct = duel.NextActGain;
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            LegacySkillFeedback feedback = playerUsesSteal ? slot.PlayerFeedback : slot.EnemyFeedback;
            Assert.That(feedback.ActStolen, Is.Zero);
            Assert.That(feedback.ActReceived, Is.Zero);
            Assert.That(feedback.EffectActivated, Is.False);
            Assert.That(duel.Act, Is.EqualTo(currentAct));
            Assert.That(duel.NextActGain, Is.EqualTo(nextAct));
        }

        [Test]
        public void ForecastsAndPerspectiveReads_DoNotTransferLiveAct_OrConsumeRandomRolls()
        {
            LocalVersusMatch match = ForecastMatch(), twin = ForecastMatch();
            var slots = new List<LegacySlotForecast>();
            for (int read = 0; read < 12; read++)
            {
                match.ForecastTurn(read % 2, slots);
                Assert.That(slots.Count, Is.EqualTo(1));
                match.CurrentClash(read % 2);
                match.CurrentSlotFacts(read % 2);
                Assert.That(match.Left.Act, Is.EqualTo(8));
                Assert.That(match.Right.Act, Is.EqualTo(10));
                Assert.That(match.Left.NextActGain, Is.EqualTo(3));
                Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
                Assert.That(match.CommittedPlannerCount, Is.EqualTo(twin.CommittedPlannerCount));
                Assert.That(match.CurrentSlot, Is.Null);
            }
            Commit(match);
            Commit(twin);
            LocalVersusSlot actual = match.BeginNextSlot(), expected = twin.BeginNextSlot();
            Assert.That(match.Left.Act, Is.EqualTo(twin.Left.Act));
            Assert.That(match.Right.Act, Is.EqualTo(twin.Right.Act));
            Assert.That(actual.LeftFeedback.ActStolen, Is.EqualTo(3));
            LocalVersusHit actualHit = match.ResolveNextHit(), expectedHit = twin.ResolveNextHit();
            Assert.That(actualHit.LeftDisplayedDamage, Is.EqualTo(expectedHit.LeftDisplayedDamage));
            Assert.That(actualHit.RightDisplayedDamage, Is.EqualTo(expectedHit.RightDisplayedDamage));
        }

        private static LocalVersusMatch ForecastMatch()
        {
            var match = Match(new[] { Steal() }, new[] { Attack(900, 1) });
            StockBoth(match);
            if (match.CurrentPlanner == 1) Assert.That(match.TryCommit(1), Is.True);
            Queue(match, 0, 2);
            return match;
        }

        private static LegacySkill Steal(int cost = 2, int hits = 1)
            => new LegacySkill(62, "기세 훔치기", cost, 3, 5, LegacySkillKind.Attack,
                LegacySkillProperty.Penetrate, hits, 2, string.Empty);

        private static LegacySkill Attack(int id, int cost, int power = 1)
            => new LegacySkill(id, "검증 공격", cost, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 1, 0, string.Empty);

        private static LocalVersusMatch Match(LegacySkill[] left, LegacySkill[] right, int opening = 0, int resistance = 100)
            => new LocalVersusMatch(left, right, 1000, resistance, 1000, resistance, randomSeed: 317, openingPlayer: opening);

        private static void StockBoth(LocalVersusMatch match)
        {
            for (int turn = 0; turn < 3; turn++)
            {
                Commit(match);
                ResolveTurn(match);
                match.BeginNextTurn();
            }
            Assert.That(match.Left.Act, Is.EqualTo(10));
            Assert.That(match.Right.Act, Is.EqualTo(10));
        }

        private static void Queue(LocalVersusMatch match, int player, int lane)
            => Assert.That(match.TryQueueLane(player, lane), Is.True);

        private static void Plan(LocalVersusMatch match, Action<int> actions)
        {
            while (match.Phase == LegacyDuelPhase.Planning)
            {
                int player = match.CurrentPlanner;
                actions(player);
                Assert.That(match.TryCommit(player), Is.True);
            }
        }

        private static void Commit(LocalVersusMatch match)
            => Plan(match, player => { });

        private static void ResolveTurn(LocalVersusMatch match)
        {
            while (!match.IsTurnResolved && !match.IsFinished) match.ResolveNextSlot();
        }

        private static void WithStealDamageCondition(Action body)
        {
            List<string[]> rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            List<string> headers = LegacySkillSheet.Headers.ToList();
            string[] steal = rows.Single(row => row[0] == "62");
            steal[headers.IndexOf(LegacySkillSheet.Column.OpponentState)] = "붕괴";
            steal[headers.IndexOf(LegacySkillSheet.Column.ConditionalDamage)] = "200%";
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                body();
            }
            finally { LegacySkillDefinitions.Install(null); }
        }
    }
}
