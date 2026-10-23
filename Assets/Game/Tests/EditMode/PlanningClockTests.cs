using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    /// <summary>제한시간 (<see cref="LegacyPlanningClock"/>): a reservation's cost needs n + 0.5 seconds on a running clock and a held
    /// clock neither pays nor refuses; 57 잔걸음's 사용 효과 adds next turn's seconds per kind of 순환 skill ahead (first copy only,
    /// +10 a turn at most), which both resolvers carry into the next turn and the forecast names.</summary>
    public sealed class PlanningClockTests
    {
        private const int Q = 0, W = 1, E = 2;
        private const int DiagonalCut = 15, VitalThrust = 21, QuickSteps = 57;

        [TestCase(10f, 4f, true, true)]
        [TestCase(4.5f, 4f, true, true)]
        [TestCase(4.49f, 4f, true, false)]
        [TestCase(1f, 4f, false, true)]
        [TestCase(0f, 0f, true, true)]
        [TestCase(1.5f, 1f, true, true)]
        [TestCase(1.4f, 1f, true, false)]
        public void CanAfford_NeedsTheCostAndHalfASecondOnARunningClock(float remaining, float cost, bool runs, bool expected)
            => Assert.That(LegacyPlanningClock.CanAfford(remaining, cost, runs), Is.EqualTo(expected));

        [Test]
        public void Spend_TakesTheCostFromARunningClockOnly_NeverBelowZero()
        {
            Assert.That(LegacyPlanningClock.Spend(10f, 4f, true), Is.EqualTo(6f));
            Assert.That(LegacyPlanningClock.Spend(3f, 4f, true), Is.Zero);
            Assert.That(LegacyPlanningClock.Spend(10f, 4f, false), Is.EqualTo(10f), "A held clock pays nothing.");
            Assert.That(LegacyPlanningClock.Spend(10f, 0f, true), Is.EqualTo(10f));
        }

        [Test]
        public void TurnStart_AddsTheBonus_UpToTenSecondsAndTheCeiling()
        {
            Assert.That(LegacyPlanningClock.CampaignTurnStart(10f, 0), Is.EqualTo(10f));
            Assert.That(LegacyPlanningClock.CampaignTurnStart(12f, 6), Is.EqualTo(18f));
            Assert.That(LegacyPlanningClock.CampaignTurnStart(10f, 14), Is.EqualTo(20f), "Base + 10 at most.");
            Assert.That(LegacyPlanningClock.CampaignTurnStart(10f, -3), Is.EqualTo(10f));
            Assert.That(LegacyPlanningClock.VersusTurnStart(20f, 6), Is.EqualTo(26f));
            Assert.That(LegacyPlanningClock.VersusTurnStart(20f, 10), Is.EqualTo(30f));
            Assert.That(LegacyPlanningClock.VersusTurnStart(25f, 10), Is.EqualTo(30f), "Versus never starts above 30 seconds.");
            Assert.That(LegacyPlanningClock.MaximumBonusSeconds, Is.EqualTo(10));
            Assert.That(LegacyPlanningClock.VersusCeilingSeconds, Is.EqualTo(30f));
        }

        [Test]
        public void ReservationSeconds_IsTheReserveEffectsCost()
        {
            Assert.That(LegacyPlanningClock.ReservationSeconds(LegacySkillDefinitions.Skill(56)), Is.EqualTo(4), "두블레.");
            Assert.That(LegacyPlanningClock.ReservationSeconds(LegacySkillDefinitions.Skill(1)), Is.Zero);
            Assert.That(LegacyPlanningClock.ReservationSeconds(LegacyCommonActions.Breathe), Is.Zero);
            Assert.That(LegacyPlanningClock.ReservationSeconds(null), Is.Zero);
        }

        [Test]
        public void QuickSteps_AddsTwoSecondsPerKindOfCycleSkillAhead_FirstCopyOnly_ForTheNextTurn()
        {
            LegacySkill quickSteps = LegacySkillDefinitions.Skill(QuickSteps);
            var secondLaneCopy = new LegacySkill(quickSteps.Id, quickSteps.Name, quickSteps.Cost,
                quickSteps.MinPower, quickSteps.MaxPower, quickSteps.Kind, quickSteps.Property, quickSteps.AttackCount,
                E, quickSteps.Description, quickSteps.AnimationName, quickSteps.IconId);
            // The duplicate remains the same technique for its once-per-turn effect, reserved from another lane.
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 100,
                new[] { LegacySkillDefinitions.Skill(DiagonalCut), quickSteps, LegacySkillDefinitions.Skill(VitalThrust), secondLaneCopy },
                Array.Empty<LegacySkill>(), new[] { 0 }, randomSeed: 1, playerActGainBonus: 7, meshPercent: 0);
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { DiagonalCut, QuickSteps }));
            Queue(duel, Q);
            Assert.That(duel.GetLane(Q)[0].Id, Is.EqualTo(QuickSteps));
            Assert.That(LegacyKeywordPreview.LaneLine(duel, duel.GetLane(Q)[0]), Is.EqualTo("다음 턴 +2초"), "One 순환 kind ahead.");
            Queue(duel, Q, W);
            Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { DiagonalCut, QuickSteps, VitalThrust }));
            // Now reserve a second 잔걸음 after both 순환 kinds.
            Queue(duel, Q);
            Assert.That(duel.GetLane(E)[0].Id, Is.EqualTo(QuickSteps));
            Assert.That(LegacyKeywordPreview.LaneLine(duel, duel.GetLane(E)[0]), Is.EqualTo(LegacyKeywordPreview.UsedThisTurn),
                "A second copy gains nothing.");
            Queue(duel, E);
            Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { DiagonalCut, QuickSteps, VitalThrust, DiagonalCut, QuickSteps }));
            Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 1, true).Text, Is.EqualTo("다음 턴 +2초"));
            LegacyKeywordMark second = LegacyKeywordPreview.QueueMark(duel, true, 4, true);
            Assert.That((second.Text, second.Kind, second.Spent), Is.EqualTo(("+0초", LegacyKeywordMarkKind.AheadTime, true)));

            // The forecast names the time on 잔걸음's slot; the live duel gains nothing from it.
            IReadOnlyList<LegacySlotForecast> forecast = duel.ForecastTurn();
            Assert.That(forecast.Select(slot => slot.NextTurnSeconds), Is.EqualTo(new[] { 0, 2, 0, 0, 0 }));
            Assert.That(forecast[1].Summary, Does.EndWith("다음 턴 제한시간 +2초"));
            Assert.That(duel.NextPlanningSeconds, Is.Zero);

            duel.Commit();
            duel.ResolveNextSlot();
            duel.BeginNextSlot();
            Assert.That(duel.CurrentSlot.PlanningSecondsGained, Is.EqualTo(2), "One 순환 kind (사선베기) ahead of the first copy.");
            Assert.That(duel.NextPlanningSeconds, Is.EqualTo(2));
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            Assert.That(duel.NextPlanningSeconds, Is.EqualTo(2), "The second copy, after two kinds, adds nothing.");
            Assert.That(duel.PlanningSecondsBonus, Is.Zero, "This turn's clock was already set.");
            duel.BeginNextTurn();
            Assert.That((duel.NextPlanningSeconds, duel.PlanningSecondsBonus), Is.EqualTo((0, 2)));
            duel.Commit();
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            duel.BeginNextTurn();
            Assert.That(duel.PlanningSecondsBonus, Is.Zero, "A turn that earns nothing gives the next nothing.");
        }

        [Test]
        public void QuickSteps_BehindTwoCycleKinds_GainsFour_AndATurnAddsTenAtMost()
        {
            LegacyQueuedDuel duel = Duel(DiagonalCut, QuickSteps, VitalThrust);
            Queue(duel, Q, W);
            Assert.That(duel.GetLane(Q)[0].Id, Is.EqualTo(QuickSteps));
            Assert.That(LegacyKeywordPreview.LaneLine(duel, duel.GetLane(Q)[0]), Is.EqualTo("다음 턴 +4초"));
            Assert.That(LegacyKeywordPreview.Note(duel, duel.GetLane(Q)[0]), Is.EqualTo("순환 기술 2/3 · 지금 예약하면 다음 턴 +4초"));
            Queue(duel, Q);
            duel.Commit();
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            Assert.That(duel.NextPlanningSeconds, Is.EqualTo(4));

            // Two kinds of time payoff, 9 seconds each: the turn stops at +10.
            KeywordSheet.With(() =>
            {
                LegacyQueuedDuel capped = Duel(916, 917);
                Assert.That(capped.TryQueueBreath() && capped.TryQueueBreath() && capped.TryQueueBreath(), Is.True);
                Queue(capped, Q, E);
                capped.Commit();
                var gained = new List<int>();
                while (!capped.IsTurnResolved)
                {
                    capped.BeginNextSlot();
                    gained.Add(capped.CurrentSlot.PlanningSecondsGained);
                    while (!capped.IsCurrentSlotResolved) capped.ResolveNextHit();
                    capped.CompleteCurrentSlot();
                }
                Assert.That(gained, Is.EqualTo(new[] { 0, 0, 0, 9, 1 }));
                Assert.That(capped.NextPlanningSeconds, Is.EqualTo(LegacyPlanningClock.MaximumBonusSeconds));
                Assert.That(LegacyKeywordPreview.AheadTimeCue(1), Is.EqualTo("다음 턴 +1초"));
                Assert.That(LegacyKeywordPreview.AheadTimeCue(0), Is.Null);
            },
                KeywordSheet.Row(916, "Q", "공격", 1, 4, (Column.UseEffect, "앞 숨고르기마다 다음 턴 제한시간 +3초 (최대 3)")),
                KeywordSheet.Row(917, "E", "공격", 1, 4, (Column.UseEffect, "앞 숨고르기마다 다음 턴 제한시간 +3초 (최대 3)")));
        }

        [Test]
        public void Versus_CarriesEachPlayersOwnSeconds_AndForecastsThem()
        {
            LegacySkill[] skills = { LegacySkillDefinitions.Skill(DiagonalCut), LegacySkillDefinitions.Skill(QuickSteps),
                LegacySkillDefinitions.Skill(VitalThrust) };
            var match = new LocalVersusMatch(skills, skills, randomSeed: 3, leftHealth: 500, rightHealth: 500);
            Assert.That(match.TryQueueLane(0, Q), Is.True);
            Assert.That(match.TryQueueLane(0, Q), Is.True);
            Assert.That(Ids(match.Left.Queue), Is.EqualTo(new[] { DiagonalCut, QuickSteps }));
            var forecast = new List<LegacySlotForecast>();
            match.ForecastTurn(0, forecast);
            Assert.That(forecast.Select(slot => slot.NextTurnSeconds), Is.EqualTo(new[] { 0, 2 }));
            match.ForecastTurn(1, forecast);
            Assert.That(forecast.Select(slot => slot.NextTurnSeconds), Is.EqualTo(new[] { 0, 0 }), "Only the user's own clock.");
            Assert.That(match.Left.NextPlanningSeconds, Is.Zero, "Forecasting changes nothing.");
            Assert.That(match.TryCommit(0) && match.TryCommit(1), Is.True);
            match.ResolveNextSlot();
            LocalVersusSlot slot = match.BeginNextSlot();
            Assert.That((slot.LeftPlanningSecondsGained, slot.RightPlanningSecondsGained), Is.EqualTo((2, 0)));
            while (!match.IsTurnResolved) match.ResolveNextSlot();
            match.BeginNextTurn();
            Assert.That((match.Left.PlanningSecondsBonus, match.Right.PlanningSecondsBonus), Is.EqualTo((2, 0)));
            Assert.That(match.Left.NextPlanningSeconds, Is.Zero);
        }

        private static LegacyQueuedDuel Duel(params int[] ids)
            => new LegacyQueuedDuel(1000, 50, 1000, 100, ids.Select(LegacySkillDefinitions.Skill).ToArray(),
                Array.Empty<LegacySkill>(), new[] { 0 }, randomSeed: 1, playerActGainBonus: 7, meshPercent: 0);

        private static void Queue(LegacyQueuedDuel duel, params int[] lanes)
        {
            foreach (int lane in lanes) Assert.That(duel.TryQueueLane(lane), Is.True, "lane " + lane);
        }

        private static int[] Ids(IReadOnlyList<LegacySkill> skills) => skills.Select(skill => skill.Id).ToArray();
    }
}
