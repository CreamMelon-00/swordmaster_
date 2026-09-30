using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class CampaignRunTests
    {
        [Test]
        public void FreshRun_PreservesInitialBattleAndNineSkillLanes()
        {
            var run = NewBattleRun();
            LegacyQueuedDuel duel = run.CreateDuel(11);

            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Battle));
            Assert.That(run.StageNumber, Is.EqualTo(1));
            Assert.That(run.StageCount, Is.EqualTo(8));
            Assert.That(run.Currency, Is.Zero);
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
            Assert.That(duel.Enemy.Health, Is.EqualTo(80));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(15));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, SkillIds(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, SkillIds(duel.GetLane(1)));
            CollectionAssert.AreEqual(new[] { 5, 6, 9 }, SkillIds(duel.GetLane(2)));
            CollectionAssert.AreEqual(new[] { 1, 2 }, SkillIds(duel.EnemyQueue));
        }

        [Test]
        public void Win_PaysOnceAndRequiresMaintenanceBeforeNextBattle()
        {
            var run = NewBattleRun();
            Assert.That(run.TryStartNextStage(), Is.False);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.InProgress), Is.False);
            Assert.That(run.TryCompleteBattle((DuelMatchOutcome)999), Is.False);
            Assert.That(run.Currency, Is.Zero);

            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Maintenance));
            Assert.That(run.Currency, Is.EqualTo(60));
            Assert.That(run.LastReward, Is.EqualTo(60));
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.False);
            Assert.That(run.Currency, Is.EqualTo(60));
            Assert.That(run.RetryCurrentStage(), Is.False);

            Assert.That(run.TryStartNextStage(), Is.True);
            Assert.That(run.StageNumber, Is.EqualTo(2));
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Battle));
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.TryStartNextStage(), Is.False);
        }

        [TestCase(DuelMatchOutcome.EnemyVictory)]
        [TestCase(DuelMatchOutcome.Draw)]
        public void LossOrDraw_DoesNotPayAndRetryKeepsStageAndCurriculum(DuelMatchOutcome outcome)
        {
            var run = NewBattleRun();
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            run.TryStartNextStage();

            Assert.That(run.TryCompleteBattle(outcome), Is.True);
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Failed));
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.Currency, Is.EqualTo(60));
            Assert.That(run.Curriculum.IsCompleted("horizontal-cut"), Is.True, "A lost battle still counts.");
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.TryStartNextStage(), Is.False);
            Assert.That(run.TrySelectCurriculumNode("diagonal-cut"), Is.False);
            Assert.That(run.TryResetCurriculum(), Is.False);
            Assert.That(run.RetryCurrentStage(), Is.True);
            Assert.That(run.RetryCurrentStage(), Is.False);
            Assert.That(run.StageNumber, Is.EqualTo(2));
            Assert.That(run.Currency, Is.EqualTo(60));
            Assert.That(Owned(run, 14), Is.Not.Null);
            Assert.That(run.Curriculum.CompletedCount, Is.EqualTo(1));

            LegacyQueuedDuel retry = run.CreateDuel();
            Assert.That(retry.Player.Health, Is.EqualTo(100));
            Assert.That(retry.Player.Resistance, Is.EqualTo(50));
            Assert.That(retry.Enemy.Health, Is.EqualTo(95));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, SkillIds(retry.GetLane(0)), "A granted skill waits to be equipped.");
        }

        [Test]
        public void GrantedSkill_RequiresExplicitEquipAndJoinsTheNextStageWithItsIconAndAnimation()
        {
            var run = new CampaignRun();
            LegacySkill granted = CampaignSkillCatalog.AcquisitionSkills[0];
            Assert.That(granted.Id, Is.EqualTo(14));
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Maintenance));
            Assert.That(run.LastCompletedCurriculumNode.Id, Is.EqualTo("horizontal-cut"));
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(10));
            CampaignOwnedSkill owned = Owned(run, granted.Id);
            Assert.That(owned.Skill, Is.SameAs(granted));
            Assert.That(owned.Skill.IconId, Is.EqualTo(10));
            Assert.That(owned.Skill.AnimationName, Is.EqualTo("Slash"));
            Assert.That(owned.Skill.MinPower, Is.EqualTo(6));
            Assert.That(owned.Skill.MaxPower, Is.EqualTo(12));
            Assert.That(run.IsSkillEquipped(granted.Id), Is.False);
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.TryEquipSkill(granted.Id), Is.False);
            Assert.That(run.TryUnequipSkill(run.GetEquippedLane(granted.LaneIndex)[0].SkillId), Is.True);
            Assert.That(run.TryEquipSkill(granted.Id), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.TryStartNextStage(), Is.True);

            IReadOnlyList<LegacySkill> lane = run.CreateDuel(17).GetLane(granted.LaneIndex);
            Assert.That(lane[0], Is.SameAs(owned.Skill));
            Assert.That(lane[0].AnimationName, Is.EqualTo(granted.AnimationName));
        }

        [Test]
        public void LaterStages_GiveTheEnemyAPlaceholderCounterWithTheStagePowerBonus()
        {
            var run = NewBattleRun();
            for (int number = 1; number <= 8; number++)
            {
                CampaignStage stage = run.CurrentStage;
                LegacyQueuedDuel duel = run.CreateDuel(number);
                Assert.That(duel.PlayerCounter, Is.Null, "The player earns counters later.");
                int expectedId = number >= 7 ? 6 : number >= 5 ? 7 : 0;
                if (expectedId == 0)
                {
                    Assert.That(stage.EnemyCounterBasis, Is.Null);
                    Assert.That(duel.EnemyCounter, Is.Null);
                }
                else
                {
                    LegacySkill basis = LegacyInitialSkills.All[expectedId == 6 ? 5 : 6];
                    Assert.That(stage.EnemyCounterBasis, Is.SameAs(basis));
                    Assert.That(duel.EnemyCounter.UsesPerTurn, Is.EqualTo(1));
                    Assert.That(duel.EnemyCounter.Skill.Id, Is.EqualTo(expectedId));
                    Assert.That(duel.EnemyCounter.Skill.MinPower, Is.EqualTo(basis.MinPower + stage.EnemyPowerBonus));
                    Assert.That(duel.EnemyCounter.Skill.MaxPower, Is.EqualTo(basis.MaxPower + stage.EnemyPowerBonus));
                    Assert.That(duel.EnemyCountersRemaining, Is.EqualTo(1));
                }
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                if (number < 8) Assert.That(run.TryStartNextStage(), Is.True);
            }
        }

        [Test]
        public void AllStages_IncreaseStatsAndEnemyPowerWithoutChangingPatternOrPlayerRecovery()
        {
            var run = NewBattleRun();
            int rewardTotal = 0;
            var names = new HashSet<string>();
            for (int number = 1; number <= 8; number++)
            {
                CampaignStage stage = run.CurrentStage;
                Assert.That(stage.Number, Is.EqualTo(number));
                Assert.That(stage.Name, Is.Not.Empty);
                Assert.That(names.Add(stage.Name), Is.True);
                Assert.That(stage.EnemyHealth, Is.EqualTo(80 + 15 * (number - 1)));
                Assert.That(stage.EnemyResistance, Is.EqualTo(15 + 3 * (number - 1)));
                Assert.That(stage.EnemyPowerBonus, Is.EqualTo(number - 1));
                Assert.That(stage.Reward, Is.EqualTo(60 + 10 * (number - 1)));

                LegacyQueuedDuel duel = run.CreateDuel(number);
                Assert.That(duel.Player.Health, Is.EqualTo(100));
                Assert.That(duel.Player.Resistance, Is.EqualTo(50));
                Assert.That(duel.Enemy.Health, Is.EqualTo(stage.EnemyHealth));
                Assert.That(duel.Enemy.Resistance, Is.EqualTo(stage.EnemyResistance));
                Assert.That(duel.EnemyQueue.Count, Is.EqualTo(2));
                for (int index = 0; index < 2; index++)
                {
                    Assert.That(duel.EnemyQueue[index].MinPower,
                        Is.EqualTo(LegacyInitialSkills.All[index].MinPower + stage.EnemyPowerBonus));
                    Assert.That(duel.EnemyQueue[index].MaxPower,
                        Is.EqualTo(LegacyInitialSkills.All[index].MaxPower + stage.EnemyPowerBonus));
                }

                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                rewardTotal += stage.Reward;
                Assert.That(run.Currency, Is.EqualTo(rewardTotal));
                if (number < 8) Assert.That(run.TryStartNextStage(), Is.True);
            }
            Assert.That(rewardTotal, Is.EqualTo(760));
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Completed));
            Assert.That(run.LastReward, Is.EqualTo(130));
            Assert.That(run.TryStartNextStage(), Is.False);
            Assert.That(run.RetryCurrentStage(), Is.False);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.False);
        }

        [Test]
        public void Reset_RestoresInitialOwnershipCurriculumStatsAndCurrency()
        {
            var run = NewBattleRun();
            ReachMaintenance(run, 3);
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            run.TryStartNextStage();
            run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory);
            Assert.That(Owned(run, 14), Is.Not.Null);
            run.ReturnToLobby();
            Assert.That(run.TrySelectCurriculumNode("diagonal-cut"), Is.True);
            run.Reset();

            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(run.StageNumber, Is.EqualTo(1));
            Assert.That(run.Currency, Is.Zero);
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
            Assert.That(Owned(run, 14), Is.Null);
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.Curriculum.Active, Is.Null);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null);
            Assert.That(run.Curriculum.GetState("diagonal-cut"), Is.EqualTo(CurriculumNodeState.Locked));
            Assert.That(run.CreateDuel().Enemy.Health, Is.EqualTo(80));
        }

        [Test]
        public void AcquisitionCatalog_HasUniqueUnownedIdsAndSupportedLegacyAnimationFamilies()
        {
            var run = new CampaignRun();
            var ids = new HashSet<int>();
            foreach (CampaignOwnedSkill owned in run.OwnedSkills) ids.Add(owned.SkillId);
            Assert.That(CampaignSkillCatalog.AcquisitionSkills.Count, Is.GreaterThanOrEqualTo(3));
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills)
            {
                Assert.That(ids.Add(skill.Id), Is.True);
                Assert.That(skill.LaneIndex, Is.InRange(0, 2));
                CollectionAssert.Contains(new[] { "Slash", "Penetrate", "Hit", "Defense" }, skill.AnimationName);
            }
        }

        [Test]
        public void EnemyForecast_ReusesTwoThreeTwoOnePatternAcrossTurns()
        {
            LegacyQueuedDuel duel = new CampaignRun().CreateDuel(13);
            int[][] expectedIds = { new[] { 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 1 }, new[] { 2 } };
            for (int turn = 0; turn < expectedIds.Length; turn++)
            {
                CollectionAssert.AreEqual(expectedIds[turn], SkillIds(duel.EnemyQueue));
                duel.Commit();
                while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
                Assert.That(duel.IsFinished, Is.False);
                if (turn + 1 < expectedIds.Length) duel.BeginNextTurn();
            }
        }

        private static CampaignRun NewBattleRun()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            return run;
        }

        private static void ReachMaintenance(CampaignRun run, int stageNumber)
        {
            while (run.StageNumber <= stageNumber)
            {
                if (run.Phase == CampaignPhase.Battle) run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
                if (run.StageNumber == stageNumber) return;
                Assert.That(run.TryStartNextStage(), Is.True);
            }
        }

        private static CampaignOwnedSkill Owned(CampaignRun run, int skillId)
        {
            foreach (CampaignOwnedSkill owned in run.OwnedSkills) if (owned.SkillId == skillId) return owned;
            return null;
        }

        private static int[] SkillIds(IReadOnlyList<LegacySkill> skills)
        {
            var ids = new int[skills.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = skills[i].Id;
            return ids;
        }
    }
}
