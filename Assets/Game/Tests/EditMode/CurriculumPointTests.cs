using System;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>수련 점수: what earns them (stage battles, story missions' first wins), what they buy (curriculum nodes at
    /// their cost), the screens' affordability helpers and the "newly affordable" signal (Docs/Curriculum.md).</summary>
    public sealed class CurriculumPointTests
    {
        [Test]
        public void Rules_AreOneForABattleOneForAWinTwoForAFirstClearAndOneForAMission()
        {
            Assert.That((CurriculumPointRules.Battle, CurriculumPointRules.Victory, CurriculumPointRules.FirstClear,
                CurriculumPointRules.StoryMission), Is.EqualTo((1, 1, 2, 1)));
            Assert.That(CurriculumPointRules.FirstClearedStage, Is.EqualTo(4));
            Assert.That(CurriculumPointRules.ForStageBattle(true, true), Is.EqualTo(new CurriculumPointGain(1, 1, 2)));
            Assert.That(CurriculumPointRules.ForStageBattle(true, false), Is.EqualTo(new CurriculumPointGain(1, 1)));
            Assert.That(CurriculumPointRules.ForStageBattle(false, true), Is.EqualTo(new CurriculumPointGain(1)),
                "A loss is never a first clear.");
            Assert.That(CurriculumPointRules.ForStoryMission(), Is.EqualTo(new CurriculumPointGain(mission: 1)));
            Assert.That(new CurriculumPointGain(1, 1, 2, 1).Total, Is.EqualTo(5));
            Assert.That(default(CurriculumPointGain).IsEmpty, Is.True);
            Assert.That(new CurriculumPointGain(mission: 1).IsEmpty, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CurriculumPointGain(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CurriculumPointGain(victory: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CurriculumPointGain(firstClear: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CurriculumPointGain(mission: -1));
        }

        [TestCase(DuelMatchOutcome.PlayerVictory, false, 1, 1, 2)]
        [TestCase(DuelMatchOutcome.PlayerVictory, true, 1, 1, 0)]
        [TestCase(DuelMatchOutcome.EnemyVictory, false, 1, 0, 0)]
        [TestCase(DuelMatchOutcome.EnemyVictory, true, 1, 0, 0)]
        [TestCase(DuelMatchOutcome.Draw, false, 1, 0, 0)]
        public void StageBattle_EarnsForFinishingWinningAndTheFirstClear(DuelMatchOutcome outcome, bool clearedBefore,
            int battle, int victory, int firstClear)
        {
            var run = new CampaignRun();
            if (clearedBefore) Win(run, 1);
            int before = run.CurriculumPoints;
            Assert.That(run.GetStageVictoryPoints(1), Is.EqualTo(clearedBefore ? 2 : 4), "What the stage preview shows.");
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.LastCurriculumPointGain.IsEmpty, Is.True, "A new battle has earned nothing yet.");
            Assert.That(run.TryCompleteBattle(outcome), Is.True);
            Assert.That(run.LastCurriculumPointGain, Is.EqualTo(new CurriculumPointGain(battle, victory, firstClear)));
            Assert.That(run.CurriculumPoints, Is.EqualTo(before + battle + victory + firstClear));
            Assert.That(run.Curriculum.CompletedCount, Is.Zero, "Points wait to be spent.");
        }

        [Test]
        public void ResultScreenRetry_EarnsAgain_AndAWinOnRetryIsTheFirstClear()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
            Assert.That(run.RetryCurrentStage(), Is.True);
            Assert.That(run.LastCurriculumPointGain.IsEmpty, Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.Draw), Is.True);
            Assert.That(run.CurriculumPoints, Is.EqualTo(2));
            Assert.That(run.RetryCurrentStage(), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.LastCurriculumPointGain.FirstClear, Is.EqualTo(2));
            Assert.That(run.CurriculumPoints, Is.EqualTo(6));
        }

        [Test]
        public void AbandonedOrUnfinishedBattle_AndTrainingFight_EarnNothing()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.InProgress), Is.False);
            Assert.That(run.TryCompleteBattle((DuelMatchOutcome)999), Is.False);
            Assert.That(run.TryAbandonBattle(), Is.True);
            Assert.That(run.CurriculumPoints, Is.Zero);
            Assert.That(run.LastCurriculumPointGain.IsEmpty, Is.True);

            Win(run, 1);
            Win(run, 2);
            Assert.That(run.ReturnToLobby(), Is.True);
            int points = run.CurriculumPoints;
            Assert.That(run.TryStartTraining(), Is.True);
            Assert.That(run.LastCurriculumPointGain.IsEmpty, Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.CurriculumPoints, Is.EqualTo(points), "Training (허수아비) earns nothing.");
            Assert.That(run.LastCurriculumPointGain.IsEmpty, Is.True);
            Assert.That(run.CurriculumNewlyAffordable, Is.False);
        }

        [Test]
        public void StoryMissionPoint_IsAddedInAnyPhase_OpenCurriculumOrNot()
        {
            var run = new CampaignRun();
            run.SetProgression(CombatFeature.LaneQ | CombatFeature.Cycle, 0);
            Assert.That(run.IsCurriculumOpen, Is.False);
            Assert.That(run.AwardStoryMissionPoints(), Is.EqualTo(new CurriculumPointGain(mission: 1)));
            Assert.That(run.CurriculumPoints, Is.EqualTo(1), "The bank grows before the curriculum opens.");
            Assert.That(run.LastCurriculumPointGain.Mission, Is.EqualTo(1));
            Assert.That(run.CurriculumNewlyAffordable, Is.False, "Nothing is said while it is closed.");

            run.ClearProgression();
            Assert.That(run.TryStartStage(1), Is.True);
            run.AwardStoryMissionPoints();
            Assert.That(run.CurriculumPoints, Is.EqualTo(2), "The run's own phase does not matter.");
            Assert.That(run.TryAbandonBattle(), Is.True);
        }

        [Test]
        public void NewlyAffordable_IsSetWhenTheBalanceFirstCoversAnAvailableNode()
        {
            var run = new CampaignRun();
            Lose(run);
            Assert.That(run.CurriculumNewlyAffordable, Is.True, "0 → 1 covers 가로베기 (1).");
            Lose(run);
            Assert.That(run.CurriculumNewlyAffordable, Is.False, "1 → 2: nothing new is covered.");
            Assert.That(run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.CurriculumNewlyAffordable, Is.False, "Learning clears nothing but earns nothing either.");
            foreach (string id in new[] { "preparation", "advance", "quick-draw" })
            {
                if (!run.IsCurriculumNodeAffordable(id)) Lose(run);
                Assert.That(run.TryLearnCurriculumNode(id), Is.True, id);
            }
            Assert.That(run.CurriculumPoints, Is.Zero);
            Lose(run);
            Assert.That(run.CurriculumNewlyAffordable, Is.True, "0 → 1 covers 사선베기 and 유연함.");
            Assert.That(run.TryLearnCurriculumNode("diagonal-cut"), Is.True);
            Assert.That(run.TryLearnCurriculumNode("suppleness"), Is.False);
            Lose(run);
            Assert.That(run.CurriculumNewlyAffordable, Is.True, "Spent down to 0, the next 0 → 1 covers 유연함 again.");
            Lose(run);
            Assert.That(run.CurriculumNewlyAffordable, Is.True, "1 → 2 covers the twists (2).");
            Lose(run);
            Assert.That(run.CurriculumNewlyAffordable, Is.False, "2 → 3: no available node costs 3 yet.");
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.CurriculumNewlyAffordable, Is.False, "The next battle starts afresh.");
            Assert.That(run.TryAbandonBattle(), Is.True);

            var closed = new CampaignRun();
            closed.SetProgression(CombatFeature.All & ~CombatFeature.LaneW, int.MaxValue);
            Lose(closed);
            Assert.That(closed.CurriculumNewlyAffordable, Is.False, "A closed curriculum says nothing.");
        }

        [Test]
        public void AffordabilityHelpers_CountWhatTheBalanceCovers_AndHowFarTheCheapestIs()
        {
            var run = new CampaignRun();
            Assert.That(run.AffordableCurriculumNodeCount, Is.Zero);
            Assert.That(run.CurriculumPointsToNextNode, Is.EqualTo(1), "다음 과정까지 1점.");
            foreach (string id in new[] { "horizontal-cut", "preparation", "advance" })
            {
                Lose(run);
                Assert.That(run.AffordableCurriculumNodeCount, Is.EqualTo(1));
                Assert.That(run.CurriculumPointsToNextNode, Is.Zero);
                Assert.That(run.TryLearnCurriculumNode(id), Is.True);
            }
            Assert.That(run.CurriculumPointsToNextNode, Is.EqualTo(1));
            Lose(run);
            Assert.That(run.AffordableCurriculumNodeCount, Is.EqualTo(3), "The three introductions, one point each.");
            Assert.That(run.Curriculum.AffordableCount(0), Is.Zero);
            Assert.That(run.Curriculum.PointsToNext(0), Is.EqualTo(1));
            Assert.That(run.TryLearnCurriculumNode("quick-draw"), Is.True);
            Lose(run);
            Assert.That(run.TryLearnCurriculumNode("diagonal-cut"), Is.True);
            Lose(run);
            Assert.That(run.TryLearnCurriculumNode("suppleness"), Is.True);
            Assert.That(run.CurriculumPoints, Is.Zero);
            Assert.That(run.CurriculumPointsToNextNode, Is.EqualTo(2), "Every twist costs 2.");
            Lose(run);
            Assert.That(run.CurriculumPointsToNextNode, Is.EqualTo(1));
            Assert.That(run.AffordableCurriculumNodeCount, Is.Zero);
            Assert.That(run.Curriculum.AffordableCount(2), Is.EqualTo(8), "The six twists and two new bridges, one at a time.");
            Assert.That(run.Curriculum.SpentPoints, Is.EqualTo(6));
            Assert.That(run.CurriculumRefund, Is.EqualTo(6));
            Assert.That(run.Curriculum.BecameAffordable(1, 2), Is.True);
            Assert.That(run.Curriculum.BecameAffordable(2, 2), Is.False);
            Assert.That(run.Curriculum.BecameAffordable(2, 9), Is.False, "No available node costs more than 2 yet.");
        }

        [Test]
        public void StageVictoryPoints_ShowWhatAWinWouldEarnNow()
        {
            var run = new CampaignRun();
            Assert.That(run.GetStageVictoryPoints(1), Is.EqualTo(4));
            Assert.That(run.GetStageVictoryPoints(8), Is.EqualTo(4), "Locked or not, an uncleared stage pays its first clear.");
            Assert.That(run.GetStageVictoryPoints(0), Is.Zero);
            Assert.That(run.GetStageVictoryPoints(9), Is.Zero);
            Win(run, 1);
            Assert.That(run.GetStageVictoryPoints(1), Is.EqualTo(2));
        }

        [Test]
        public void JourneyReset_StartsTheBalanceOver()
        {
            var run = new CampaignRun();
            Win(run, 1);
            Assert.That(run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.CurriculumPoints, Is.EqualTo(3));
            run.AwardStoryMissionPoints();
            run.Reset();
            Assert.That(run.CurriculumPoints, Is.Zero);
            Assert.That(run.CurriculumRefund, Is.Zero);
            Assert.That(run.LastCurriculumPointGain.IsEmpty, Is.True);
            Assert.That(run.CurriculumNewlyAffordable, Is.False);
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.CaptureSave().CurriculumPoints, Is.Zero);
        }

        [Test]
        public void JourneyReset_StartsWithWhatTheKeptStoryPaid_AsAnOlderSaveOfTheSameStateDoes()
        {
            // The story outlives the journey and a won mission is never won again, so a new journey starts with the
            // missions' points: each mission still pays once in every journey.
            var run = new CampaignRun();
            for (int mission = 0; mission < 12; mission++) run.AwardStoryMissionPoints();
            Win(run, 1);
            Assert.That(run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            run.Reset(12);
            Assert.That(run.CurriculumPoints, Is.EqualTo(12 * CurriculumPointRules.StoryMission));
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.CurriculumRefund, Is.Zero);
            Assert.That(run.LastCurriculumPointGain.IsEmpty, Is.True);
            Assert.That(run.CurriculumNewlyAffordable, Is.False);
            Win(run, 1);
            Assert.That(run.CurriculumPoints, Is.EqualTo(16));

            // A save written before 수련 점수 with the same state (12 missions won, stage 1 cleared) restores with the
            // same balance, so either path to it ends alike.
            CampaignSave live = run.CaptureSave();
            var older = new CampaignSave(live.Currency, live.ClearedStages, live.CurriculumCompleted, null, live.Loadout,
                live.TrainingVictoryCount, live.SkillExperience, live.FieldPickups);
            Assert.That(new CampaignRun().CurriculumPointsIn(older, 12), Is.EqualTo(run.CurriculumPoints));
            var restored = new CampaignRun();
            Assert.That(restored.TryRestore(older, 12, out string error), Is.True, error);
            Assert.That(restored.CurriculumPoints, Is.EqualTo(16));

            run.Reset(-3);
            Assert.That(run.CurriculumPoints, Is.Zero, "A negative count credits nothing.");
            run.Reset(0);
            Assert.That(run.CurriculumPoints, Is.Zero);
        }

        [Test]
        public void Balance_SaturatesInsteadOfOverflowing()
        {
            var run = new CampaignRun();
            CampaignSave basis = run.CaptureSave();
            var rich = new CampaignSave(0, new int[0], new[] { "horizontal-cut" }, int.MaxValue - 1, basis.Loadout);
            Assert.That(run.TryRestore(rich, out string error), Is.True, error);
            Win(run, 1);
            Assert.That(run.CurriculumPoints, Is.EqualTo(int.MaxValue));
            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(run.CurriculumPoints, Is.EqualTo(int.MaxValue), "The refund saturates too.");
            Assert.That(run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.CurriculumPoints, Is.EqualTo(int.MaxValue - 1));
        }

        private static void Win(CampaignRun run, int stage)
        {
            Assert.That(run.TryStartStage(stage), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
        }

        // One point: a lost battle of stage one. Ends in the lobby.
        private static void Lose(CampaignRun run)
        {
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
        }
    }
}
