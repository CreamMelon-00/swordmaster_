using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using TurnLimbo.Runtime.Tavern;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class SkillTavernBattleTests
    {
        [Test]
        public void Battle_CapturesEightPositionsAndLeavesCommitToThePresenter()
        {
            SkillTavernRun run = Run(Attack(-9100));
            BuyAndDeploy(run);
            run.NextRound();
            BuyAndDeploy(run);
            Assert.That(run.TrySellBoard(0), Is.True);
            string before = RunSnapshot(run);
            var battle = new SkillTavernBattle(run, seed: 5);
            Assert.That(battle.Duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(battle.Duel.PlayerQueue, Is.Empty);
            Assert.That(battle.PlayerQueue.Count, Is.EqualTo(8));
            Assert.That(battle.EnemyQueue.Count, Is.EqualTo(8));
            Assert.That(battle.PlayerQueue.Select(skill => skill.Id), Is.EqualTo(run.BuildQueue().Select(skill => skill.Id)));
            Assert.That(battle.PlayerQueue[0].IsWait, Is.True);
            Assert.That(battle.PlayerQueue[1].IsWait, Is.False);
            Assert.That(battle.Duel.Features.AllowsAnyStep(), Is.False);
            Assert.That(battle.Duel.MeshPercent, Is.Zero);
            Assert.That(battle.Duel.RoundLimit, Is.EqualTo(24));
            Assert.That(battle.Duel.Player.Health, Is.EqualTo(500));
            Assert.That(battle.Duel.Player.MaxHealth, Is.EqualTo(500));
            Assert.That(battle.Duel.Enemy.Health, Is.EqualTo(500));
            Assert.That(battle.Duel.Enemy.MaxHealth, Is.EqualTo(500));
            Assert.That(battle.Duel.Enemy.Resistance, Is.EqualTo(30));
            Assert.That(battle.EnemyName, Is.Not.Empty);
            Assert.That(RunSnapshot(run), Is.EqualTo(before));
        }

        [Test]
        public void FixedQueues_KeepOrderChargeNoActAndAssignTheSingleEnemy()
        {
            LegacyQueuedDuel duel = Duel();
            LegacySkill[] player = { Attack(-9100, cost: 99), LegacyCommonActions.Breathe, Attack(-9101) };
            LegacySkill[] enemy = { LegacyCommonActions.Breathe, Attack(-9200), Attack(-9201) };
            int act = duel.Act;
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
            Assert.That(duel.Act, Is.EqualTo(act));
            Assert.That(duel.PlayerQueue.Select(skill => skill.Id), Is.EqualTo(player.Select(skill => skill.Id)));
            Assert.That(duel.EnemyQueue.Select(skill => skill.Id), Is.EqualTo(enemy.Select(skill => skill.Id)));
            Assert.That(Enumerable.Range(0, 3).Select(duel.EnemyOwnerAtSlot), Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(duel.EnemyOwnerAtSlot(3), Is.EqualTo(-1));
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerSkill.Id, Is.EqualTo(-9100));
        }

        [TestCase(true, 0)]
        [TestCase(false, 0)]
        [TestCase(true, 1)]
        [TestCase(false, 1)]
        [TestCase(true, 2)]
        [TestCase(false, 2)]
        public void InvalidFixedQueue_IsRejectedAtomically(bool playerSide, int invalidKind)
        {
            LegacyQueuedDuel duel = Duel();
            Assert.That(duel.TryQueueLane(0), Is.True);
            LegacySkill[] valid = { Attack(-9101), LegacyCommonActions.Breathe };
            LegacySkill[] invalid = invalidKind == 0 ? null : invalidKind == 1 ? new LegacySkill[0]
                : new[] { Attack(-9102), null };
            string before = DuelSnapshot(duel);
            Assert.That(duel.TrySetFixedQueues(playerSide ? invalid : valid, playerSide ? valid : invalid), Is.False);
            Assert.That(DuelSnapshot(duel), Is.EqualTo(before));
        }

        [Test]
        public void FixedQueues_CannotReplaceAResolvingOrFinishedTurn()
        {
            LegacyQueuedDuel duel = Duel(roundLimit: 1);
            LegacySkill[] breaths = Breaths();
            Assert.That(duel.TrySetFixedQueues(breaths, breaths), Is.True);
            duel.Commit();
            string before = DuelSnapshot(duel);
            Assert.That(duel.TrySetFixedQueues(new[] { Attack(-9101) }, breaths), Is.False);
            Assert.That(DuelSnapshot(duel), Is.EqualTo(before));
            ResolveTurn(duel);
            Assert.That(duel.Phase, Is.EqualTo(LegacyDuelPhase.Finished));
            before = DuelSnapshot(duel);
            Assert.That(duel.TrySetFixedQueues(breaths, breaths), Is.False);
            Assert.That(DuelSnapshot(duel), Is.EqualTo(before));
        }

        [Test]
        public void FixedQueues_RejectMultipleEnemyOwnersWithoutChangingTheirQueue()
        {
            var script = new EnemyScript(new[] { new[] { Attack(-9200) } });
            var duel = new LegacyQueuedDuel(60, 30, new[] { Attack(-9100) }, new[]
            {
                new LegacyEnemyDefinition("첫 상대", 60, 30, script),
                new LegacyEnemyDefinition("둘째 상대", 60, 30, script),
            });
            string before = DuelSnapshot(duel);
            Assert.That(duel.TrySetFixedQueues(Breaths(), Breaths()), Is.False);
            Assert.That(DuelSnapshot(duel), Is.EqualTo(before));
        }

        [Test]
        public void FixedQueueImport_CopiesBothCollectionsAndMutableOwnedTechniques()
        {
            var campaign = new CampaignRun();
            CampaignOwnedSkill owned = campaign.GetOwnedSkill(1);
            int min = owned.Skill.MinPower;
            LegacySkill[] player = { owned.Skill };
            LegacySkill[] enemy = { Attack(-9200) };
            LegacyQueuedDuel duel = Duel();
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            player[0] = LegacyCommonActions.Breathe;
            enemy[0] = LegacyCommonActions.Breathe;
            for (int index = 0; index < owned.ExperienceRequired; index++) owned.GainClashExperience();
            Assert.That(duel.PlayerQueue[0].Id, Is.EqualTo(1));
            Assert.That(duel.PlayerQueue[0].MinPower, Is.EqualTo(min));
            Assert.That(duel.EnemyQueue[0].Id, Is.EqualTo(-9200));
            Assert.That(duel.PlayerQueue[0], Is.Not.SameAs(owned.Skill));
        }

        [Test]
        public void BattleSnapshots_AreReadOnlyAndSurviveLaterShopChanges()
        {
            SkillTavernRun run = Run(Attack(-9100));
            BuyAndDeploy(run);
            var battle = new SkillTavernBattle(run);
            Assert.That(battle.PlayerQueue[0], Is.Not.SameAs(run.Board[0].Skill));
            Assert.Throws<NotSupportedException>(() => ((IList<LegacySkill>)battle.PlayerQueue)[0] = LegacyCommonActions.Breathe);
            Assert.Throws<NotSupportedException>(() => ((IList<LegacySkill>)battle.EnemyQueue)[0] = LegacyCommonActions.Breathe);
            Assert.That(run.TrySellBoard(0), Is.True);
            run.NextRound();
            Assert.That(battle.PlayerQueue[0].Id, Is.EqualTo(-9100));
            Assert.That(battle.EnemyQueue.Count(skill => !skill.IsWait), Is.EqualTo(1));
        }

        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(9, 5)]
        [TestCase(15, 8)]
        [TestCase(int.MaxValue, 8)]
        public void EnemyFormation_GrowsEveryTwoShopRoundsAndKeepsEightSlots(int round, int count)
        {
            LegacySkill[] enemy = SkillTavernBattle.CreateEnemyQueue(round);
            Assert.That(enemy.Length, Is.EqualTo(8));
            Assert.That(enemy.Take(count).All(skill => !skill.IsWait), Is.True);
            Assert.That(enemy.Skip(count).All(skill => skill.IsWait), Is.True);
            Assert.That(enemy.Where(skill => !skill.IsWait).Select(skill => skill.Id).Distinct().Count(), Is.EqualTo(count));
            foreach (LegacySkill skill in enemy.Where(skill => !skill.IsWait))
            {
                Assert.That(skill.Id, Is.LessThanOrEqualTo(-8000));
                Assert.That(LegacySkillDefinitions.Find(skill), Is.Null);
                Assert.That(skill.MinPower, Is.InRange(4, 7));
                Assert.That(skill.MaxPower, Is.InRange(6, 9));
            }
        }

        [Test]
        public void EnemyFormation_CoversTheThreeAttackPropertiesAndDefence()
        {
            Assert.That(SkillTavernBattle.CreateEnemyQueue(15).Take(4).Select(skill => skill.Property),
                Is.EqualTo(new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate,
                    LegacySkillProperty.Hit, LegacySkillProperty.Defence }));
            Assert.That(SkillTavernBattle.CreateEnemyQueue(15)[3].Kind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.Throws<ArgumentOutOfRangeException>(() => SkillTavernBattle.CreateEnemyQueue(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SkillTavernBattle.CreateEnemyQueue(-1));
            Assert.Throws<ArgumentNullException>(() => new SkillTavernBattle(null));
        }

        [Test]
        public void EightBreathsOnBothSides_ResolveExactlyTwentyFourTurnsThenDraw()
        {
            LegacyQueuedDuel duel = Duel(roundLimit: SkillTavernBattle.MaximumTurns);
            int slots = 0;
            while (duel.Phase != LegacyDuelPhase.Finished)
            {
                Assert.That(duel.TrySetFixedQueues(Breaths(), Breaths()), Is.True);
                duel.Commit();
                while (duel.Phase == LegacyDuelPhase.Resolving && !duel.IsTurnResolved)
                {
                    Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.Zero);
                    slots++;
                }
                if (duel.Phase != LegacyDuelPhase.Finished) duel.BeginNextTurn();
            }
            Assert.That(slots, Is.EqualTo(8 * SkillTavernBattle.MaximumTurns));
            Assert.That(duel.RoundNumber, Is.EqualTo(24));
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.Draw));
        }

        [Test]
        public void Battle_RepeatsItsFixedFormationUntilTheTurnLimit()
        {
            var guard = new LegacySkill(-9100, "튼튼한 막기", 99, 100, 100, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 0, string.Empty);
            SkillTavernRun run = Run(guard);
            BuyAndDeploy(run);
            var battle = new SkillTavernBattle(run, seed: 2);
            Assert.That(battle.TryBeginNextTurn(), Is.False);
            Begin(battle);
            int turns = 0;
            do
            {
                Assert.That(battle.Duel.PlayerQueue.Select(skill => skill.Id), Is.EqualTo(battle.PlayerQueue.Select(skill => skill.Id)));
                Assert.That(battle.Duel.EnemyQueue.Select(skill => skill.Id), Is.EqualTo(battle.EnemyQueue.Select(skill => skill.Id)));
                ResolveTurn(battle.Duel);
                turns++;
            } while (battle.TryBeginNextTurn());
            Assert.That(turns, Is.EqualTo(24));
            Assert.That(battle.Duel.Outcome, Is.EqualTo(DuelMatchOutcome.Draw));
            Assert.That(battle.TryBeginNextTurn(), Is.False);
        }

        [Test]
        public void QueueChangingReservationEffects_DoNotInsertOrRemoveFormationPositions()
        {
            WithSheet(() =>
            {
                LegacyQueuedDuel duel = Duel();
                LegacySkill[] player = Breaths();
                player[0] = LegacySkillDefinitions.Skill(961);
                player[1] = LegacySkillDefinitions.Skill(962);
                player[2] = LegacySkillDefinitions.Skill(963);
                Assert.That(duel.TrySetFixedQueues(player, Breaths()), Is.True);
                Assert.That(duel.PlayerQueue.Select(skill => skill.Id), Is.EqualTo(player.Select(skill => skill.Id)));
                Assert.That(duel.AutoBreathSlotIndex, Is.EqualTo(-1));
                Assert.That(duel.ReserveEffectsUsedThisTurn, Is.EqualTo(LegacyReserveEffect.None));
                Assert.That(duel.GetLane(0).Count, Is.EqualTo(1), "Temporary spawns never reach the normal lane.");
                duel.Commit();
                ResolveTurn(duel);
                Assert.That(duel.LastResolvedSlot, Is.EqualTo(7));
            },
                SheetRow(961, "공격", (Column.ReserveEffect, "숨고르기 +1")),
                SheetRow(962, "방어", (Column.QueueRemovalMode, "반환")),
                SheetRow(963, "공격", (Column.ReserveEffect, "일회성 964 ×1")),
                SheetRow(964, "공격", (Column.Group, LegacySkillSheet.TemporaryGroup)));
        }

        [Test]
        public void FixedImport_ClearsAPreviousReservationRemovalMultiplier()
        {
            WithSheet(() =>
            {
                var duel = new LegacyQueuedDuel(1000, 100, 1000, 100,
                    new[] { LegacySkillDefinitions.Skill(961), LegacySkillDefinitions.Skill(962), Attack(-9100, 5) },
                    new[] { LegacyCommonActions.Breathe }, new[] { 1 }, features: CombatFeature.LaneQ, meshPercent: 0);
                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(duel.TrySetFixedQueues(new[] { Attack(-9101, 5), Attack(-9102, 5) }, Breaths()), Is.True);
                duel.Commit();
                duel.ResolveNextSlot();
                Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(5));
            }, SheetRow(961, "공격"),
                SheetRow(962, "방어", (Column.QueueRemovalMode, "취소"), (Column.QueuePowerMultiplierCap, "3")));
        }

        [Test]
        public void InitialBuffAndCountAheadUseEffects_StillUseTheExistingSlotRules()
        {
            WithSheet(() =>
            {
                LegacyQueuedDuel duel = Duel();
                LegacySkill[] player = { LegacySkillDefinitions.Skill(961), LegacyCommonActions.Breathe,
                    LegacySkillDefinitions.Skill(962) };
                Assert.That(duel.TrySetFixedQueues(player, Breaths()), Is.True);
                duel.Commit();
                duel.ResolveNextSlot();
                duel.ResolveNextSlot();
                Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(13),
                    "(base 5 + one earlier breath ×4) ×150%, rounded down.");
            }, SheetRow(961, "방어", (Column.PowerBuff, "50"), (Column.BuffSlots, "1")),
                SheetRow(962, "공격", (Column.UseEffect, "앞 숨고르기마다 위력 +4 (최대 4)")));
        }

        [Test]
        public void CycleFeedAndUseGrowth_RemainActiveWithoutReservationInsertion()
        {
            WithSheet(() =>
            {
                LegacyQueuedDuel duel = Duel();
                LegacySkill[] player = { LegacySkillDefinitions.Skill(961), LegacySkillDefinitions.Skill(962) };
                Assert.That(duel.TrySetFixedQueues(player, Breaths()), Is.True);
                duel.Commit();
                Assert.That(duel.CycleUses(961), Is.EqualTo(1));
                Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(7));
                Assert.That(duel.CycleUses(961), Is.EqualTo(2));
            }, SheetRow(961, "공격", (Column.CyclePowerPerUse, "2"), (Column.CycleMaxCount, "2")),
                SheetRow(962, "공격", (Column.ReserveEffect, "순환 +1")));
        }

        [Test]
        public void BreakCancellation_OnlyChangesTheExecutionCopyAndNextTurnRestoresFormation()
        {
            LegacyQueuedDuel duel = Duel();
            LegacySkill[] player = { Attack(-9100, 35), Attack(-9101, 1) };
            LegacySkill[] enemy = { Attack(-9200, 1), Attack(-9201, 1) };
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(1));
            LegacyCurrentSlot next = duel.BeginNextSlot();
            Assert.That(next.EnemySkill, Is.Null);
            Assert.That(enemy[1].Id, Is.EqualTo(-9201));
            ResolveCurrentSlot(duel);
            Assert.That(duel.IsTurnResolved, Is.True);
            duel.BeginNextTurn();
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            duel.Commit();
            Assert.That(duel.EnemyQueue[1].Id, Is.EqualTo(-9201));
        }

        [TestCase(DuelMatchOutcome.PlayerVictory)]
        [TestCase(DuelMatchOutcome.EnemyVictory)]
        [TestCase(DuelMatchOutcome.Draw)]
        public void Result_IsRecordedOnceAndPreservesTheShopFormationAndCurrency(DuelMatchOutcome outcome)
        {
            SkillTavernRun run = Run(Attack(-9100));
            BuyAndDeploy(run);
            string before = RunSnapshot(run);
            var battle = new SkillTavernBattle(run);
            Assert.That(battle.TryRecordResult(), Is.False);
            FinishWithOutcome(battle.Duel, outcome);
            Assert.That(battle.TryRecordResult(), Is.True);
            Assert.That(run.BattlesPlayed, Is.EqualTo(1));
            Assert.That(run.Wins, Is.EqualTo(outcome == DuelMatchOutcome.PlayerVictory ? 1 : 0));
            Assert.That(run.Losses, Is.EqualTo(outcome == DuelMatchOutcome.EnemyVictory ? 1 : 0));
            Assert.That(run.Draws, Is.EqualTo(outcome == DuelMatchOutcome.Draw ? 1 : 0));
            Assert.That(run.LastOutcome, Is.EqualTo(outcome));
            Assert.That(RunSnapshot(run), Is.EqualTo(before));
            Assert.That(battle.TryRecordResult(), Is.False);
            Assert.That(run.BattlesPlayed, Is.EqualTo(1));
        }

        [Test]
        public void PurchaseFormationBattleResultAndNextShop_CanRepeatInOneRun()
        {
            SkillTavernRun run = Run(Attack(-9100, 200));
            Assert.That(run.LastOutcome, Is.Null);
            for (int round = 1; round <= 4; round++)
            {
                Assert.That(run.RoundNumber, Is.EqualTo(round));
                BuyAndDeploy(run);
                string before = RunSnapshot(run);
                var battle = new SkillTavernBattle(run, seed: round);
                Begin(battle);
                do { ResolveTurn(battle.Duel); } while (battle.TryBeginNextTurn());
                Assert.That(battle.Duel.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
                Assert.That(battle.TryRecordResult(), Is.True);
                Assert.That(run.Wins, Is.EqualTo(round));
                Assert.That(RunSnapshot(run), Is.EqualTo(before));
                run.NextRound();
                Assert.That(run.Gold, Is.EqualTo(run.RoundIncome));
                Assert.That(run.ShopOffers.All(skill => skill != null), Is.True);
                Assert.That(run.Board.Count(card => card != null), Is.EqualTo(round));
            }
        }

        [Test]
        public void SameSeedAndFormation_ProduceTheSameBattleResult()
        {
            SkillTavernRun run = Run(Attack(-9100, 5));
            BuyAndDeploy(run);
            var first = new SkillTavernBattle(run, seed: 46);
            var second = new SkillTavernBattle(run, seed: 46);
            Begin(first);
            Begin(second);
            do { ResolveTurn(first.Duel); } while (first.TryBeginNextTurn());
            do { ResolveTurn(second.Duel); } while (second.TryBeginNextTurn());
            Assert.That(new[] { first.Duel.Player.Health, first.Duel.Player.Resistance, first.Duel.Enemy.Health,
                first.Duel.Enemy.Resistance, first.Duel.RoundNumber, (int)first.Duel.Outcome }, Is.EqualTo(new[]
                { second.Duel.Player.Health, second.Duel.Player.Resistance, second.Duel.Enemy.Health,
                    second.Duel.Enemy.Resistance, second.Duel.RoundNumber, (int)second.Duel.Outcome }));
        }

        private static SkillTavernRun Run(LegacySkill skill) => new SkillTavernRun(new[] { skill }, seed: 2);
        private static LegacySkill Attack(int id, int power = 5, int cost = 1)
            => new LegacySkill(id, "시험 베기", cost, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);
        private static LegacySkill[] Breaths() => Enumerable.Repeat(LegacyCommonActions.Breathe, 8).ToArray();
        private static LegacyQueuedDuel Duel(int roundLimit = SkillTavernBattle.MaximumTurns)
            => new LegacyQueuedDuel(1000, 30, 1000, 30, new[] { Attack(-9100) },
                new[] { Attack(-9200) }, new[] { 1 }, features: CombatFeature.LaneQ, roundLimit: roundLimit, meshPercent: 0);
        private static void BuyAndDeploy(SkillTavernRun run)
        {
            Assert.That(run.TryBuy(0), Is.True);
            Assert.That(run.TryDeploy(run.Inventory[run.Inventory.Count - 1].InstanceId), Is.True);
        }
        private static void Begin(SkillTavernBattle battle)
        {
            battle.Duel.Reset();
            Assert.That(battle.Duel.TrySetFixedQueues(battle.PlayerQueue, battle.EnemyQueue), Is.True);
            battle.Duel.Commit();
        }
        private static void ResolveCurrentSlot(LegacyQueuedDuel duel)
        {
            while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
        }
        private static void ResolveTurn(LegacyQueuedDuel duel)
        {
            while (duel.Phase == LegacyDuelPhase.Resolving && !duel.IsTurnResolved) duel.ResolveNextSlot();
        }
        private static void FinishWithOutcome(LegacyQueuedDuel duel, DuelMatchOutcome outcome)
        {
            while (duel.Phase != LegacyDuelPhase.Finished)
            {
                LegacySkill[] player = Breaths(), enemy = Breaths();
                if (outcome == DuelMatchOutcome.PlayerVictory) player[0] = Attack(-9100, 5000);
                if (outcome == DuelMatchOutcome.EnemyVictory) enemy[0] = Attack(-9200, 5000);
                Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
                duel.Commit();
                ResolveTurn(duel);
                if (duel.Phase != LegacyDuelPhase.Finished) duel.BeginNextTurn();
            }
            Assert.That(duel.Outcome, Is.EqualTo(outcome));
        }
        private static string RunSnapshot(SkillTavernRun run)
            => run.RoundNumber + ":" + run.Gold + ":" + string.Join(",", run.ShopOffers.Select(skill => skill?.Id ?? 0))
                + ":" + string.Join(",", run.Board.Select(card => card?.InstanceId ?? 0))
                + ":" + string.Join(",", run.Inventory.Select(card => card.InstanceId));
        private static string DuelSnapshot(LegacyQueuedDuel duel)
            => duel.Phase + ":" + duel.Act + ":" + string.Join(",", duel.PlayerQueue.Select(skill => skill.Id))
                + ":" + string.Join(",", duel.EnemyQueue.Select(skill => skill.Id))
                + ":" + string.Join(",", Enumerable.Range(0, duel.EnemyQueue.Count).Select(duel.EnemyOwnerAtSlot))
                + ":" + duel.AutoBreathSlotIndex + ":" + duel.LastQueuedSlotIndex + ":" + duel.ReserveEffectsUsedThisTurn;

        private static string[] SheetRow(int id, string kind, params (string column, string value)[] cells)
        {
            string[] row = Enumerable.Repeat(string.Empty, LegacySkillSheet.Headers.Count).ToArray();
            void Set(string column, string value) => row[LegacySkillSheet.Headers.ToList().IndexOf(column)] = value;
            Set(Column.Id, id.ToString());
            Set(Column.Name, "시험 기술 " + id);
            Set(Column.Group, LegacySkillSheet.AcquisitionGroup);
            Set(Column.Lane, "Q");
            Set(Column.Kind, kind);
            Set(Column.Property, kind == "방어" ? "방어" : "참격");
            Set(Column.Cost, "1");
            Set(Column.MinPower, "5");
            Set(Column.MaxPower, "5");
            Set(Column.Hits, "1");
            Set(Column.ShortLabel, "시험");
            Set(Column.Detail, "시험");
            foreach (var cell in cells) Set(cell.column, cell.value);
            return row;
        }
        private static void WithSheet(Action body, params string[][] additions)
        {
            List<string[]> rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            rows.AddRange(additions);
            string csv = CsvTable.Write(rows);
            try { LegacySkillDefinitions.Install(() => csv); body(); }
            finally { LegacySkillDefinitions.Install(null); }
        }
    }
}
