using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>편성 예상 (<see cref="LegacySlotForecast"/>, Docs/SlotForecast.md): a planning turn forecast by the duel's own rules on a
    /// copy of it, average power rolls and no steps. It must match the same queues resolved with every roll fixed at its
    /// average (copies of the sheet's skills whose range is that one number, so the rules and effects are the real ones),
    /// and it must never change the live duel.</summary>
    public sealed class SlotForecastTests
    {
        [Test]
        public void AveragePower_IsTheRangesMiddle_AHalfRoundedUp()
        {
            Assert.That(LegacySlotForecast.AveragePower(3, 4), Is.EqualTo(4));
            Assert.That(LegacySlotForecast.AveragePower(15, 18), Is.EqualTo(17));
            Assert.That(LegacySlotForecast.AveragePower(4, 6), Is.EqualTo(5));
            Assert.That(LegacySlotForecast.AveragePower(5, 5), Is.EqualTo(5));
            Assert.That(LegacySlotForecast.AveragePower(18, 42), Is.EqualTo(30));
            Assert.Throws<ArgumentOutOfRangeException>(() => LegacySlotForecast.AveragePower(5, 4));
        }

        [Test]
        public void Forecast_MatchesTheSameQueuesResolvedWithAverageRolls_WithBuffsMeshingAndTheEnemysCounter()
        {
            // Q W E · 숨고르기 · Q E against 깊은 찌르기, 쳐내기 (a guard that powers the enemy's next two slots), 베기, and a
            // counter (연속 베기) for the first one-sided attack: a chain of three meshes, the counter answers slot 4.
            LegacySkill[] enemyTurn = { Skill(3), Skill(9), Skill(1) };
            Func<bool, LegacyQueuedDuel> make = fixedRolls => new LegacyQueuedDuel(100, 50, 100, 40,
                Pick(fixedRolls, LegacyInitialSkills.All), new EnemyScript(new[] { Pick(fixedRolls, enemyTurn) }), 5,
                enemyCounter: new LegacyCounter(Pick(fixedRolls, Skill(2))), playerActGainBonus: 4);
            LegacyQueuedDuel duel = make(false), twin = make(true);
            foreach (LegacyQueuedDuel side in new[] { duel, twin }) Queue(side, 0, 1, 2, -1, 0, 2);

            IReadOnlyList<LegacySlotForecast> forecast = duel.ForecastTurn();
            Assert.That(forecast.Count, Is.EqualTo(6));
            Assert.That(duel.PlayerMesh(1).IsMeshed, Is.True, "The forecast covers a meshed chain…");
            Assert.That(forecast[4].EnemySkill.Id, Is.EqualTo(2), "…and the enemy's counter filling its empty slot.");
            Assert.That(forecast[5].EnemySkill, Is.Null, "One use: the next one-sided attack meets nothing.");
            AssertMatchesResolution(forecast, twin);
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
        }

        [Test]
        public void Forecast_MatchesAverageRolls_WithThePlayersCounterAndABreak()
        {
            // The enemy's queue is longer: its third attack meets the player's counter (no dodge in a forecast). 깊은 찌르기
            // breaks the enemy's low resistance in slot 1, so the next slot lands on a broken target.
            LegacySkill[] enemyTurn = { Skill(1), Skill(1), Skill(3), Skill(2) };
            Func<bool, LegacyQueuedDuel> make = fixedRolls => new LegacyQueuedDuel(100, 50, 100, 12,
                Pick(fixedRolls, LegacyInitialSkills.All), new EnemyScript(new[] { Pick(fixedRolls, enemyTurn) }), 9,
                playerCounter: new LegacyCounter(Pick(fixedRolls, Skill(2))), playerActGainBonus: 2);
            LegacyQueuedDuel duel = make(false), twin = make(true);
            foreach (LegacyQueuedDuel side in new[] { duel, twin }) Queue(side, 0, 1);

            IReadOnlyList<LegacySlotForecast> forecast = duel.ForecastTurn();
            Assert.That(forecast.Count, Is.EqualTo(4));
            Assert.That(forecast[1].EnemyBroken, Is.True, "깊은 찌르기 breaks the enemy's 12 resistance.");
            Assert.That(forecast[1].Summary, Does.Contain("상대 저항 -").And.Contain("(붕괴)"));
            Assert.That(forecast[2].PlayerSkill.Id, Is.EqualTo(2), "The player's counter answers the one-sided attack.");
            Assert.That(forecast[3].PlayerSkill, Is.Null, "Its one use is spent.");
            AssertMatchesResolution(forecast, twin);
        }

        [Test]
        public void Forecast_NeverChangesTheLiveDuel_NorItsRandomRolls()
        {
            Func<LegacyQueuedDuel> make = () => new LegacyQueuedDuel(100, 50, 80, 20, LegacyInitialSkills.All,
                new EnemyScript(new[] { new[] { Skill(3), Skill(7) }, new[] { Skill(1), Skill(5), Skill(2) } }), 77,
                enemyCounter: new LegacyCounter(Skill(1)), playerActGainBonus: 3);
            LegacyQueuedDuel duel = make(), untouched = make();
            for (int turn = 0; turn < 3 && !duel.IsFinished; turn++)
            {
                foreach (LegacyQueuedDuel side in new[] { duel, untouched }) Queue(side, 1, 0, 2);
                string before = Snapshot(duel);
                Assert.That(Snapshot(untouched), Is.EqualTo(before));
                for (int look = 0; look < 3; look++)
                {
                    duel.ForecastTurn();
                    for (int lane = 0; lane < 3; lane++) duel.ForecastIfQueued(lane);
                }
                Assert.That(Snapshot(duel), Is.EqualTo(before), "Forecasting changes nothing the duel shows.");

                duel.Commit();
                untouched.Commit();
                while (!duel.IsTurnResolved && !duel.IsFinished)
                {
                    LegacySlotResult forecastSide = duel.ResolveNextSlot(), plain = untouched.ResolveNextSlot();
                    Assert.That(Describe(forecastSide), Is.EqualTo(Describe(plain)),
                        "The live duel rolls exactly what it would have without the forecast.");
                }
                Assert.That(duel.IsFinished, Is.EqualTo(untouched.IsFinished));
                if (duel.IsFinished) break;
                duel.BeginNextTurn();
                untouched.BeginNextTurn();
            }
        }

        [Test]
        public void ForecastIfQueued_IsTheSlotTheLaneWouldTake_AndNullWhenTheLaneCannotQueue()
        {
            Func<LegacyQueuedDuel> make = () => new LegacyQueuedDuel(100, 50, 100, 30, LegacyInitialSkills.All,
                new EnemyScript(new[] { new[] { Skill(3), Skill(5), Skill(7) } }), 3, playerActGainBonus: 1);
            LegacyQueuedDuel duel = make();
            Assert.That(duel.TryQueueLane(0), Is.True);
            for (int lane = 0; lane < 3; lane++)
            {
                LegacyQueuedDuel queued = make();
                Assert.That(queued.TryQueueLane(0) && queued.TryQueueLane(lane), Is.True);
                LegacySlotForecast expected = queued.ForecastTurn()[1], actual = duel.ForecastIfQueued(lane);
                Assert.That(actual, Is.Not.Null, "Lane " + lane);
                Assert.That(Describe(actual), Is.EqualTo(Describe(expected)), "Lane " + lane);
                Assert.That(actual.PlayerSkill, Is.SameAs(duel.GetLane(lane)[0]));
            }
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "Asking never queues.");
            Assert.That(duel.Act, Is.EqualTo(3));

            Assert.That(duel.TryQueueLane(1), Is.True, "깊은 찌르기 spends 2 ACT…");
            Assert.That(duel.ForecastIfQueued(1), Is.Null, "…so 정교한 찌르기 (3 ACT) cannot follow it.");
            var closed = new LegacyQueuedDuel(100, 50, 100, 30, LegacyInitialSkills.All,
                new EnemyScript(new[] { new[] { Skill(1) } }), 3, features: CombatFeature.LaneQ);
            Assert.That(closed.ForecastIfQueued(2), Is.Null, "A closed lane queues nothing.");
            closed.Commit();
            Assert.That(closed.ForecastTurn(), Is.Empty, "Only planning is forecast.");
            Assert.That(closed.ForecastIfQueued(0), Is.Null);
        }

        [Test]
        public void SlotsAfterTheFinishingBlow_AreUnreached()
        {
            // An enemy on 10 health with no resistance left to spend falls to the first 깊은 찌르기; the rest never resolves.
            var duel = new LegacyQueuedDuel(100, 50, 10, 0, LegacyInitialSkills.All,
                new EnemyScript(new[] { new[] { Skill(7), Skill(1), Skill(1) } }), 1, playerActGainBonus: 3);
            Queue(duel, 1, 0, 0);
            IReadOnlyList<LegacySlotForecast> forecast = duel.ForecastTurn();
            Assert.That(forecast.Count, Is.EqualTo(3));
            Assert.That(forecast[0].Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(forecast[0].Judge(), Is.EqualTo(LegacyForecastVerdict.Favourable));
            Assert.That(forecast[0].Summary, Does.StartWith(LegacySlotForecast.SummaryPrefix + "상대 체력 -").And.EndWith(" · 승리"));
            foreach (LegacySlotForecast later in forecast.Skip(1))
            {
                Assert.That(later.Reached, Is.False);
                Assert.That(later.Summary, Is.EqualTo(LegacySlotForecast.UnreachedSummary));
                Assert.That(later.Judge(), Is.EqualTo(LegacyForecastVerdict.Even));
                Assert.That(later.PlayerSkill, Is.SameAs(duel.PlayerQueue[later.SlotIndex]));
            }
        }

        [Test]
        public void Verdict_WeighsHealthResistanceAndBreaks_AndTheSummaryReadsFromThePlayersSide()
        {
            var clash = Slot(playerResistance: 6, enemyResistance: 12);
            Assert.That(clash.Summary, Is.EqualTo("예상: 상대 저항 -12 · 내 저항 -6"));
            Assert.That(clash.Score(), Is.EqualTo(300), "Resistance at half weight: (12 - 6) × 50.");
            Assert.That(clash.Judge(), Is.EqualTo(LegacyForecastVerdict.Favourable));
            Assert.That(clash.Judge(margin: 4), Is.EqualTo(LegacyForecastVerdict.Even));
            Assert.That(clash.Judge(resistanceWeightPercent: 100, margin: 4), Is.EqualTo(LegacyForecastVerdict.Favourable));

            var hit = Slot(enemyHealth: 9);
            Assert.That(hit.Summary, Is.EqualTo("예상: 상대 체력 -9"));
            var taken = Slot(playerHealth: 9, playerResistance: 0);
            Assert.That(taken.Summary, Is.EqualTo("예상: 내 체력 -9"));
            Assert.That(taken.Judge(), Is.EqualTo(LegacyForecastVerdict.Unfavourable));
            Assert.That(Slot().Summary, Is.EqualTo("예상: 변화 없음"));
            Assert.That(Slot().Judge(), Is.EqualTo(LegacyForecastVerdict.Even));
            Assert.That(Slot(enemyResistance: -5).Summary, Is.EqualTo("예상: 상대 저항 +5"), "Restoration reads as a gain.");

            var broken = Slot(enemyHealth: 2, enemyResistance: 4, enemyBroken: true, playerResistance: 4);
            Assert.That(broken.Summary, Is.EqualTo("예상: 상대 체력 -2 · 상대 저항 -4(붕괴) · 내 저항 -4"));
            Assert.That(broken.Score(), Is.EqualTo(200 + 100 * LegacySlotForecast.DefaultBreakBonus));
            Assert.That(broken.Judge(breakBonus: 0), Is.EqualTo(LegacyForecastVerdict.Even), "Without the break's worth, 2 health is close.");
            Assert.That(broken.Judge(), Is.EqualTo(LegacyForecastVerdict.Favourable));

            Assert.That(Slot(outcome: DuelMatchOutcome.EnemyVictory, playerHealth: 3).Summary, Is.EqualTo("예상: 내 체력 -3 · 패배"));
            Assert.That(Slot(outcome: DuelMatchOutcome.EnemyVictory, playerHealth: 3).Judge(margin: 30),
                Is.EqualTo(LegacyForecastVerdict.Unfavourable), "Losing the fight is always 불리.");
            Assert.That(LegacySlotForecast.VerdictLabel(LegacyForecastVerdict.Favourable), Is.EqualTo("유리"));
            Assert.That(LegacySlotForecast.VerdictLabel(LegacyForecastVerdict.Unfavourable), Is.EqualTo("불리"));
            Assert.That(LegacySlotForecast.VerdictLabel(LegacyForecastVerdict.Even), Is.EqualTo("비슷"));
        }

        [Test]
        public void VersusForecast_MatchesAverageRolls_FromEitherSide_AndLeavesTheMatchAlone()
        {
            Func<bool, int, LocalVersusMatch> make = (fixedRolls, seed) => new LocalVersusMatch(
                Pick(fixedRolls, LegacyInitialSkills.All), Pick(fixedRolls, LegacyInitialSkills.All), 100, 30, 100, 30, seed);
            LocalVersusMatch match = make(false, 21), untouched = make(false, 21);
            // Left: Q W E, Right: W 숨고르기 E E; the right side queues one more.
            foreach (LocalVersusMatch side in new[] { match, untouched }) PlayVersus(side);
            string before = Snapshot(match);
            for (int player = 0; player < 2; player++)
            {
                var forecast = new List<LegacySlotForecast>();
                match.ForecastTurn(player, forecast);
                var view = new LocalVersusPerspective(match, player);
                var viewed = new List<LegacySlotForecast>();
                view.ForecastTurn(viewed);
                Assert.That(viewed.Select(Describe), Is.EqualTo(forecast.Select(Describe)), "The perspective forwards its side.");
                Assert.That(forecast.Count, Is.EqualTo(4));
                LocalVersusMatch twin = make(true, 99);
                PlayVersus(twin);
                PassTwice(twin);
                LocalVersusFighter self = twin.GetFighter(player), other = twin.GetFighter(1 - player);
                for (int slot = 0; slot < forecast.Count; slot++)
                {
                    bool selfWhole = !self.State.IsResistanceBroken, otherWhole = !other.State.IsResistanceBroken;
                    LocalVersusSlotResult result = twin.ResolveNextSlot();
                    LegacySlotForecast seen = forecast[slot];
                    bool left = player == 0;
                    Assert.That(seen.PlayerHealthDamage, Is.EqualTo(left ? result.LeftHealthDamage : result.RightHealthDamage), "slot " + slot);
                    Assert.That(seen.EnemyHealthDamage, Is.EqualTo(left ? result.RightHealthDamage : result.LeftHealthDamage), "slot " + slot);
                    Assert.That(seen.PlayerResistanceDamage, Is.EqualTo(left ? result.LeftResistanceDamage : result.RightResistanceDamage));
                    Assert.That(seen.EnemyResistanceDamage, Is.EqualTo(left ? result.RightResistanceDamage : result.LeftResistanceDamage));
                    Assert.That(seen.PlayerBroken, Is.EqualTo(selfWhole && self.State.IsResistanceBroken));
                    Assert.That(seen.EnemyBroken, Is.EqualTo(otherWhole && other.State.IsResistanceBroken));
                    Assert.That(seen.PlayerSkill?.Id, Is.EqualTo((left ? result.LeftSkill : result.RightSkill)?.Id));
                    Assert.That(seen.EnemySkill?.Id, Is.EqualTo((left ? result.RightSkill : result.LeftSkill)?.Id));
                    if (twin.IsFinished) break;
                }
            }
            Assert.That(Snapshot(match), Is.EqualTo(before), "Forecasting from either side changes nothing.");

            PassTwice(match);
            PassTwice(untouched);
            while (!match.IsTurnResolved && !match.IsFinished)
                Assert.That(Describe(match.ResolveNextSlot()), Is.EqualTo(Describe(untouched.ResolveNextSlot())),
                    "The match rolls what it would have rolled without the forecast.");
        }

        [Test]
        public void VersusForecastIfQueued_IsTheSlotAfterThisSidesLast_WhoeverHoldsTheTurn()
        {
            var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All, randomSeed: 4);
            Assert.That(match.TryQueueLane(0, 2), Is.True, "Left: 기세 꺾기.");
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(guest.IsOpponentsTurn, Is.False);
            LegacySlotForecast thrust = guest.ForecastIfQueued(1);
            Assert.That(thrust, Is.Not.Null);
            Assert.That(thrust.SlotIndex, Is.Zero, "The guest's queue is empty: its next skill takes slot 1.");
            Assert.That(thrust.PlayerSkill.Id, Is.EqualTo(3));
            Assert.That(thrust.EnemySkill.Id, Is.EqualTo(5));
            Assert.That(match.Right.Queue, Is.Empty, "Asking never queues.");

            Assert.That(match.TryQueueLane(1, 1), Is.True);
            var queued = new List<LegacySlotForecast>();
            guest.ForecastTurn(queued);
            Assert.That(Describe(queued[0]), Is.EqualTo(Describe(thrust)));
            var host = new LocalVersusPerspective(match, 0);
            Assert.That(host.IsMyTurn, Is.True);
            Assert.That(guest.ForecastIfQueued(1), Is.Null, "The guest's 1 ACT left cannot pay for 정교한 찌르기.");
            LegacySlotForecast cut = guest.ForecastIfQueued(0);
            Assert.That(cut, Is.Not.Null, "Off its turn too: 베기 fits the guest's 1 ACT.");
            Assert.That(cut.SlotIndex, Is.EqualTo(1));
            Assert.That(cut.EnemySkill, Is.Null, "The host has nothing in slot 2 yet.");
            Assert.That(cut.EnemyHealthDamage, Is.GreaterThan(0), "A one-sided attack reaches health.");
            Assert.That(host.ForecastIfQueued(0).SlotIndex, Is.EqualTo(1));
        }

        private static void AssertMatchesResolution(IReadOnlyList<LegacySlotForecast> forecast, LegacyQueuedDuel twin)
        {
            twin.Commit();
            for (int slot = 0; slot < forecast.Count; slot++)
            {
                LegacySlotForecast seen = forecast[slot];
                if (twin.IsFinished)
                {
                    Assert.That(seen.Reached, Is.False, "slot " + slot);
                    continue;
                }
                bool playerWhole = !twin.Player.IsResistanceBroken, enemyWhole = !twin.Enemy.IsResistanceBroken;
                LegacySlotResult result = twin.ResolveNextSlot();
                Assert.That(seen.Reached, Is.True, "slot " + slot);
                Assert.That(seen.SlotIndex, Is.EqualTo(result.SlotIndex));
                Assert.That(seen.PlayerSkill?.Id, Is.EqualTo(result.PlayerSkill?.Id), "slot " + slot);
                Assert.That(seen.EnemySkill?.Id, Is.EqualTo(result.EnemySkill?.Id), "slot " + slot);
                Assert.That(seen.PlayerHealthDamage, Is.EqualTo(result.PlayerHealthDamage), "slot " + slot);
                Assert.That(seen.EnemyHealthDamage, Is.EqualTo(result.EnemyHealthDamage), "slot " + slot);
                Assert.That(seen.PlayerResistanceDamage, Is.EqualTo(result.PlayerResistanceDamage), "slot " + slot);
                Assert.That(seen.EnemyResistanceDamage, Is.EqualTo(result.EnemyResistanceDamage), "slot " + slot);
                Assert.That(seen.PlayerBroken, Is.EqualTo(playerWhole && twin.Player.IsResistanceBroken), "slot " + slot);
                Assert.That(seen.EnemyBroken, Is.EqualTo(enemyWhole && twin.Enemy.IsResistanceBroken), "slot " + slot);
                Assert.That(seen.Outcome, Is.EqualTo(result.Outcome), "slot " + slot);
            }
        }

        // Queues lanes in order; -1 queues 숨고르기.
        private static void Queue(LegacyQueuedDuel duel, params int[] lanes)
        {
            foreach (int lane in lanes)
                Assert.That(lane < 0 ? duel.TryQueueBreath() : duel.TryQueueLane(lane), Is.True, "lane " + lane);
        }

        private static void PlayVersus(LocalVersusMatch match)
        {
            // Left (0) opens: 베기, 기세 꺾기, 연속 베기; Right: 깊은 찌르기, 숨고르기, 기세 꺾기, 숨고르기 after Left passes once.
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 1), Is.True);
            Assert.That(match.TryQueueLane(0, 2), Is.True);
            Assert.That(match.TryQueueBreath(1), Is.True);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 2), Is.True);
            Assert.That(match.TryQueueLane(0, 1), Is.False, "Left has spent its 3 ACT.");
            Assert.That(match.TryPass(0), Is.True);
            Assert.That(match.TryQueueBreath(1), Is.True);
            Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
        }

        private static void PassTwice(LocalVersusMatch match)
        {
            while (match.Phase == LegacyDuelPhase.Planning) Assert.That(match.TryPass(match.CurrentPlanner), Is.True);
        }

        private static LegacySlotForecast Slot(int playerHealth = 0, int enemyHealth = 0, int playerResistance = 0,
            int enemyResistance = 0, bool playerBroken = false, bool enemyBroken = false,
            DuelMatchOutcome outcome = DuelMatchOutcome.InProgress)
            => new LegacySlotForecast(0, null, null, playerHealth, enemyHealth, playerResistance, enemyResistance,
                playerBroken, enemyBroken, outcome);

        private static LegacySkill Skill(int id) => LegacySkillDefinitions.Skill(id);

        // The sheet's skill, or a copy whose power range is its average: the same id (so the same effects), cost, hits and lane.
        private static LegacySkill[] Pick(bool fixedRolls, IEnumerable<LegacySkill> skills)
            => skills.Select(skill => Pick(fixedRolls, skill)).ToArray();

        private static LegacySkill Pick(bool fixedRolls, LegacySkill skill)
        {
            if (!fixedRolls || skill.IsWait) return skill;
            int power = LegacySlotForecast.AveragePower(skill.MinPower, skill.MaxPower);
            return new LegacySkill(skill.Id, skill.Name, skill.Cost, power, power, skill.Kind, skill.Property,
                skill.AttackCount, skill.LaneIndex, skill.Description, skill.AnimationName, skill.IconId);
        }

        private static string Describe(LegacySlotForecast slot)
            => $"{slot.SlotIndex}:{slot.PlayerSkill?.Id}/{slot.EnemySkill?.Id} hp {slot.PlayerHealthDamage}/{slot.EnemyHealthDamage} " +
                $"res {slot.PlayerResistanceDamage}/{slot.EnemyResistanceDamage} break {slot.PlayerBroken}/{slot.EnemyBroken} " +
                $"{slot.Outcome} {slot.Reached}";

        private static string Describe(LegacySlotResult slot)
            => $"{slot.SlotIndex}:{slot.PlayerSkill?.Id}/{slot.EnemySkill?.Id} hp {slot.PlayerHealthDamage}/{slot.EnemyHealthDamage} " +
                $"res {slot.PlayerResistanceDamage}/{slot.EnemyResistanceDamage} {slot.Outcome}";

        private static string Describe(LocalVersusSlotResult slot)
            => $"{slot.SlotIndex}:{slot.LeftSkill?.Id}/{slot.RightSkill?.Id} hp {slot.LeftHealthDamage}/{slot.RightHealthDamage} " +
                $"res {slot.LeftResistanceDamage}/{slot.RightResistanceDamage} {slot.Outcome}";

        private static string Snapshot(LegacyQueuedDuel duel)
            => $"{duel.Phase} r{duel.RoundNumber} act {duel.Act}+{duel.NextActGain} q[{Ids(duel.PlayerQueue)}] e[{Ids(duel.EnemyQueue)}] " +
                $"lanes[{Ids(duel.GetLane(0))}|{Ids(duel.GetLane(1))}|{Ids(duel.GetLane(2))}] " +
                $"p {duel.Player.Health}/{duel.Player.Resistance} e {duel.Enemy.Health}/{duel.Enemy.Resistance} " +
                $"counters {duel.PlayerCountersRemaining}/{duel.EnemyCountersRemaining} breaths {duel.BreathsRemainingThisTurn} " +
                $"cycle {duel.CycleUses(46)} threshold {duel.EnemyHealthThresholdReached} {duel.Outcome}";

        private static string Snapshot(LocalVersusMatch match)
            => $"{match.Phase} r{match.RoundNumber} planner {match.CurrentPlanner} passes {match.ConsecutivePasses} " +
                Fighter(match.Left) + " " + Fighter(match.Right) + " " + match.Outcome;

        private static string Fighter(LocalVersusFighter fighter)
            => $"act {fighter.Act}+{fighter.NextActGain} q[{Ids(fighter.Queue)}] " +
                $"lanes[{Ids(fighter.GetLane(0))}|{Ids(fighter.GetLane(1))}|{Ids(fighter.GetLane(2))}] " +
                $"{fighter.State.Health}/{fighter.State.Resistance} breaths {fighter.BreathsRemainingThisTurn}";

        private static string Ids(IReadOnlyList<LegacySkill> skills) => string.Join(",", skills.Select(skill => skill.Id));
    }
}
