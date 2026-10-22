using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    public sealed class ActRecoveryStealTests
    {
        [TearDown]
        public void RestoreSheet() => LegacySkillDefinitions.Install(null);

        [TestCase(0)]
        [TestCase(1)]
        public void AttackCondition_TransfersRecoveryAtUse_WithoutChangingCurrentAct(int owner)
        {
            var match = Match(owner == 0 ? new[] { Thief() } : new[] { Attack() },
                owner == 1 ? new[] { Thief() } : new[] { Attack() });
            Plan(match, (player) => Queue(match, player, player == owner ? 2 : 0));
            int leftAct = match.Left.Act, rightAct = match.Right.Act;
            var slot = match.BeginNextSlot();
            var feedback = owner == 0 ? slot.LeftFeedback : slot.RightFeedback;
            Assert.That(feedback.ActRecoveryStolen, Is.EqualTo(1));
            Assert.That(feedback.ActStolen, Is.Zero);
            Assert.That(feedback.ActReceived, Is.Zero);
            Assert.That(feedback.ConditionMet && feedback.EffectActivated, Is.True);
            Assert.That(match.GetFighter(owner).NextActGain, Is.EqualTo(4));
            Assert.That(match.GetFighter(1 - owner).NextActGain, Is.EqualTo(2));
            Assert.That((match.Left.Act, match.Right.Act), Is.EqualTo((leftAct, rightAct)));
            Assert.That(match.GetFighter(owner).HasUsedActRecoverySteal(62), Is.True);
        }

        [Test]
        public void RealThreeByThreeLoadouts_StealRecoveryFromAnOpponentWithZeroRemainingAct()
        {
            var left = new VersusLoadout();
            var right = new VersusLoadout();
            Assert.That(left.TryPlaceSkill(62, 2, 0), Is.True);
            Assert.That(right.TryPlaceSkill(4, 1, 0), Is.True);
            var match = new LocalVersusMatch(left.ToSkills(), right.ToSkills(), leftResistance: 100, rightResistance: 100);
            Plan(match, p => Queue(match, p, p == 0 ? 2 : 1));
            Assert.That((match.Left.Act, match.Right.Act), Is.EqualTo((1, 0)));
            var slot = match.BeginNextSlot();
            Assert.That((slot.LeftSkill.Id, slot.RightSkill.Id), Is.EqualTo((62, 4)));
            Assert.That(slot.LeftFeedback.ActRecoveryStolen, Is.EqualTo(1));
            Assert.That((match.Left.Act, match.Right.Act), Is.EqualTo((1, 0)));
            Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((4, 2)));
            FinishSlot(match);
            Assert.That(match.Left.State.IsDefeated || match.Right.State.IsDefeated, Is.False);
            match.BeginNextTurn();
            Assert.That((match.Left.Act, match.Right.Act), Is.EqualTo((5, 2)));
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.False);
        }

        [Test]
        public void CampaignHasNoOpponentRecoveryWallet_AndNeverTransfersRecovery()
        {
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { Thief() }, new[] { Attack() }, new[] { 1 });
            Assert.That(duel.TryQueueLane(2), Is.True);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerFeedback.ActRecoveryStolen, Is.Zero);
            Assert.That(duel.NextActGain, Is.EqualTo(3));
        }

        [Test]
        public void FeedbackCopiesKeepRecoveryTransferWhenConditionalDamageIsArmed()
        {
            var rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table)).Select(r => r.Fields.ToArray()).ToList();
            string[] row = rows.Single(r => r[0] == "62");
            row[Array.IndexOf(rows[0], LegacySkillSheet.Column.OpponentState)] = "붕괴";
            row[Array.IndexOf(rows[0], LegacySkillSheet.Column.ConditionalDamage)] = "200%";
            string sheet = CsvTable.Write(rows);
            LegacySkillDefinitions.Install(() => sheet);
            var match = Match(new[] { Thief() }, new[] { Attack() }, resistance: 0);
            Plan(match, p => Queue(match, p, p == 0 ? 2 : 0));
            var feedback = match.BeginNextSlot().LeftFeedback;
            Assert.That(feedback.ActRecoveryStolen, Is.EqualTo(1));
            Assert.That(feedback.ConditionalDamagePercent, Is.EqualTo(200));
            Assert.That(feedback.ActStolen, Is.Zero);
        }

        [TestCase(LegacySkillKind.Defence)]
        [TestCase(LegacySkillKind.Wait)]
        public void NonAttackDoesNotSpendTheSuccessfulUse(LegacySkillKind kind)
        {
            LegacySkill other = kind == LegacySkillKind.Wait ? LegacyCommonActions.Breathe : Guard();
            var match = Match(new[] { Thief() }, new[] { other });
            Plan(match, p => { if (p == 0) Queue(match, p, 2); else if (kind == LegacySkillKind.Wait)
                Assert.That(match.TryQueueBreath(p), Is.True); else Queue(match, p, 0); });
            Assert.That(match.BeginNextSlot().LeftFeedback.ActRecoveryStolen, Is.Zero);
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.False);
            Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((3, 3)));
        }

        [Test]
        public void EmptySlotDoesNotActivate()
        {
            var match = Match(new[] { Thief() }, new[] { Attack() });
            Plan(match, p => { if (p == 0) Queue(match, p, 2); });
            Assert.That(match.BeginNextSlot().LeftFeedback.ActRecoveryStolen, Is.Zero);
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.False);
        }

        [Test]
        public void LaterCopyMaySucceedAfterTheFirstFacedAGuard_ThenStopsForTheTurn()
        {
            var match = Match(new[] { Thief(hits: 4) }, new[] { Guard(), Attack(), Attack(902) });
            Plan(match, p => { for (int i = 0; i < 3; i++) Queue(match, p, p == 0 ? 2 : 0); });
            var first = match.BeginNextSlot();
            Assert.That(first.LeftFeedback.ActRecoveryStolen, Is.Zero);
            FinishSlot(match);
            var second = match.BeginNextSlot();
            Assert.That(second.LeftFeedback.ActRecoveryStolen, Is.EqualTo(1));
            for (int hit = 0; hit < 4; hit++)
            {
                match.ResolveNextHit();
                Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((4, 2)));
            }
            match.CompleteCurrentSlot();
            var third = match.BeginNextSlot();
            Assert.That(third.LeftFeedback.ActRecoveryStolen, Is.Zero);
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.True);
        }

        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        public void TargetKeepsAtLeastOne_OnlyActualLossIsGained(int targetRecovery, int stolen)
        {
            var match = Match(new[] { Thief() }, new[] { Attack() });
            SetRecovery(match.Right, targetRecovery);
            Plan(match, p => Queue(match, p, p == 0 ? 2 : 0));
            Assert.That(match.BeginNextSlot().LeftFeedback.ActRecoveryStolen, Is.EqualTo(stolen));
            Assert.That(match.Right.NextActGain, Is.EqualTo(targetRecovery - stolen));
            Assert.That(match.Left.NextActGain, Is.EqualTo(3 + stolen));
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.EqualTo(stolen > 0));
        }

        [Test]
        public void LargeAuthoredAmountClampsToTheTargetsActualAvailableRecovery()
        {
            InstallPhrase("상대 공격 시 다음 턴 ACT 회복 10 강탈");
            var match = Match(new[] { Thief() }, new[] { Attack() });
            Plan(match, p => Queue(match, p, p == 0 ? 2 : 0));
            Assert.That(match.BeginNextSlot().LeftFeedback.ActRecoveryStolen, Is.EqualTo(2));
            Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((5, 1)));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SimultaneousTransfersUseOneSnapshotAndConsumeBothSuccesses(int opening)
        {
            var match = Match(new[] { Thief() }, new[] { Thief() }, opening);
            Plan(match, p => Queue(match, p, 2));
            var slot = match.BeginNextSlot();
            Assert.That((slot.LeftFeedback.ActRecoveryStolen, slot.RightFeedback.ActRecoveryStolen), Is.EqualTo((1, 1)));
            Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((3, 3)));
            Assert.That(match.Left.HasUsedActRecoverySteal(62) && match.Right.HasUsedActRecoverySteal(62), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SimultaneousTransferCannotStealRecoveryTheOtherSideJustGained(int lowOwner)
        {
            var match = Match(new[] { Thief() }, new[] { Thief() });
            SetRecovery(match.GetFighter(lowOwner), 1);
            Plan(match, p => Queue(match, p, 2));
            var slot = match.BeginNextSlot();
            Assert.That((lowOwner == 0 ? slot.LeftFeedback : slot.RightFeedback).ActRecoveryStolen, Is.EqualTo(1));
            Assert.That((lowOwner == 0 ? slot.RightFeedback : slot.LeftFeedback).ActRecoveryStolen, Is.Zero);
            Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((2, 2)));
        }

        [Test]
        public void OrdinarySkillRecoveryAppliesBeforeTheSharedTransferSnapshot()
        {
            var match = Match(new[] { Thief() }, new[] { Copy(1, 0) });
            SetRecovery(match.Right, 1);
            Plan(match, p => Queue(match, p, p == 0 ? 2 : 0));
            var slot = match.BeginNextSlot();
            Assert.That(slot.RightFeedback.ActGainGranted, Is.EqualTo(1));
            Assert.That(slot.LeftFeedback.ActRecoveryStolen, Is.EqualTo(1));
            Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((4, 1)));
        }

        [Test]
        public void AbsorbedDamageDoesNotGateTheTransfer()
        {
            var match = Match(new[] { Thief() }, new[] { Attack() });
            ((LegacyStatusCollection)typeof(LocalVersusFighter).GetField("StatusEffects", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(match.Right)).Grant(LegacyStatusKind.Protection, 100, 1);
            Plan(match, p => Queue(match, p, p == 0 ? 2 : 0));
            var slot = match.BeginNextSlot();
            Assert.That(slot.LeftFeedback.ActRecoveryStolen, Is.EqualTo(1));
            Assert.That(match.ResolveNextHit().RightDisplayedDamage, Is.Zero);
        }

        [Test]
        public void CancelledReservationNeverTransfersRecovery()
        {
            var match = Match(new[] { Thief(), LegacySkillDefinitions.Skill(45) }, new[] { Attack() });
            Plan(match, p => { Queue(match, p, p == 0 ? 2 : 0); if (p == 0) Queue(match, p, 2); });
            Assert.That(match.Left.Queue.Single().Id, Is.EqualTo(45));
            Assert.That(match.BeginNextSlot().LeftFeedback.ActRecoveryStolen, Is.Zero);
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.False);
        }

        [Test]
        public void BreakCancelsFutureUsesWithoutSpendingTheirPerTurnSuccess()
        {
            var match = Match(new[] { Attack(), Thief() }, new[] { Attack(901, 100) }, resistance: 50);
            Plan(match, p => { Queue(match, p, 0); if (p == 0) Queue(match, p, 2); });
            match.ResolveNextSlot();
            Assert.That(match.Left.BreakCanceledFromSlotIndex, Is.EqualTo(1));
            Assert.That(match.BeginNextSlot().LeftSkill, Is.Null);
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.False);
            Assert.That(match.Right.NextActGain, Is.EqualTo(3));
        }

        [Test]
        public void TurnAdvanceAndResetClearUses_AndApplyTheTransferredRecoveryOnce()
        {
            var match = Match(new[] { Thief() }, new[] { Attack() });
            Plan(match, p => Queue(match, p, p == 0 ? 2 : 0));
            match.ResolveNextSlot();
            match.BeginNextTurn();
            Assert.That((match.Left.Act, match.Right.Act), Is.EqualTo((7, 5)));
            Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((3, 3)));
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.False);
            Plan(match, p => Queue(match, p, p == 0 ? 2 : 0));
            Assert.That(match.BeginNextSlot().LeftFeedback.ActRecoveryStolen, Is.EqualTo(1));
            match.Reset();
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.False);
            Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((3, 3)));
        }

        [Test]
        public void ForecastCopiesSuccessfulUseStateWithoutMutatingTheOriginal()
        {
            var match = Match(new[] { Thief() }, new[] { Attack() });
            var uses = (HashSet<int>)typeof(LocalVersusFighter).GetField("ActRecoveryStealUses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match.Left);
            uses.Add(62);
            var copy = (LocalVersusFighter)typeof(LocalVersusFighter).GetMethod("Copy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match.Left, null);
            Assert.That(copy.HasUsedActRecoverySteal(62), Is.True);
            uses.Clear();
            Queue(match, 0, 2);
            Assert.That(match.TryCommit(0), Is.True);
            Queue(match, 1, 0);
            var slots = new List<LegacySlotForecast>();
            for (int read = 0; read < 4; read++) match.ForecastTurn(read % 2, slots);
            Assert.That(match.Left.HasUsedActRecoverySteal(62), Is.False);
            Assert.That((match.Left.NextActGain, match.Right.NextActGain), Is.EqualTo((3, 3)));
            Assert.That(match.TryCommit(1), Is.True);
            Assert.That(match.BeginNextSlot().LeftFeedback.ActRecoveryStolen, Is.EqualTo(1));
        }

        [TestCase("상대 공격 시 다음 턴 ACT 회복 0 강탈")]
        [TestCase("상대 공격 시 다음 턴 ACT 회복 11 강탈")]
        [TestCase("상대 공격 시 다음 턴 ACT 회복 -1 강탈")]
        [TestCase("상대 공격 시 다음 턴 ACT 회복 1.5 강탈")]
        public void InvalidRecoveryStealGrammarIsRejected(string phrase)
            => Assert.Throws<SkillSheetException>(() => LegacySkillSheet.Parse("invalid", ChangedPhrase(phrase)));

        [Test]
        public void RecoveryStealGrammarRoundTripsWithOptionalSpacesAndKeepsImmediateGrammarAvailable()
        {
            var table = LegacySkillSheet.Parse("compact", ChangedPhrase("상대공격시다음턴ACT회복1강탈"));
            Assert.That(table.Find(62).Effect.ActRecoveryStealAmount, Is.EqualTo(1));
            var reread = LegacySkillSheet.Parse("written", LegacySkillSheet.Write(table));
            Assert.That(reread.Find(62).Effect.HasActRecoverySteal, Is.True);
            Assert.That(LegacySkillLabels.UseEffect(reread.Find(62).Effect), Is.EqualTo("상대 공격 시 다음 턴 ACT 회복 1 강탈"));
            Assert.That(LegacySkillSheet.Parse("instant", ChangedPhrase("상대 ACT 8 이상이면 ACT 3 강탈")).Find(62).Effect.HasActSteal, Is.True);
        }

        private static string ChangedPhrase(string phrase)
        {
            var rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table)).Select(r => r.Fields.ToArray()).ToList();
            rows.Single(r => r[0] == "62")[Array.IndexOf(rows[0], LegacySkillSheet.Column.UseEffect)] = phrase;
            return CsvTable.Write(rows);
        }
        private static void InstallPhrase(string phrase)
        { string text = ChangedPhrase(phrase); LegacySkillDefinitions.Install(() => text); }
        private static void SetRecovery(LocalVersusFighter fighter, int amount)
            => typeof(LocalVersusFighter).GetProperty("NextActGain").SetValue(fighter, amount);
        private static LegacySkill Thief(int hits = 1) => Copy(62, 2, hits);
        private static LegacySkill Copy(int id, int lane, int hits = 1)
        { var s = LegacySkillDefinitions.Skill(id); return new LegacySkill(id, s.Name, 0, s.MinPower, s.MaxPower, s.Kind, s.Property, hits, lane, s.Description); }
        private static LegacySkill Attack(int id = 900, int power = 1)
            => new LegacySkill(id, "검증 공격", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");
        private static LegacySkill Guard() => new LegacySkill(901, "검증 방어", 0, 1, 1, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");
        private static LocalVersusMatch Match(LegacySkill[] left, LegacySkill[] right, int opening = 0, int resistance = 100)
            => new LocalVersusMatch(left, right, 1000, resistance, 1000, resistance, randomSeed: 317, openingPlayer: opening);
        private static void Plan(LocalVersusMatch match, Action<int> actions)
        { while (match.Phase == LegacyDuelPhase.Planning) { int p = match.CurrentPlanner; actions(p); Assert.That(match.TryCommit(p), Is.True); } }
        private static void Queue(LocalVersusMatch match, int player, int lane) => Assert.That(match.TryQueueLane(player, lane), Is.True);
        private static void FinishSlot(LocalVersusMatch match)
        { while (!match.CurrentSlot.IsResolved) match.ResolveNextHit(); match.CompleteCurrentSlot(); }
    }

}
