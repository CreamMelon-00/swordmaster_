using System;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Core.Tests
{
    public sealed class BattleResultTests
    {
        [Test]
        public void FirstVictory_KeepsRewardAndNewUnlockInImmutableSnapshot()
        {
            var result = new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구",
                60, 105, 4, 87, 0, true, 2, true);
            Assert.That(result.Victory, Is.True);
            Assert.That(result.IsTutorial, Is.False);
            Assert.That(result.StageNumber, Is.EqualTo(1));
            Assert.That(result.StageName, Is.EqualTo("숲길 입구"));
            Assert.That(result.Reward, Is.EqualTo(60));
            Assert.That(result.Currency, Is.EqualTo(105));
            Assert.That(result.RoundNumber, Is.EqualTo(4));
            Assert.That(result.PlayerHealth, Is.EqualTo(87));
            Assert.That(result.EnemyHealth, Is.Zero);
            Assert.That(result.FirstClear, Is.True);
            Assert.That(result.UnlockedStageNumber, Is.EqualTo(2));
            Assert.That(result.CanAdvance, Is.True);
            Assert.That(result.CanRetry, Is.True);
        }

        [Test]
        public void ReplayVictory_CanAdvanceWithoutClaimingAnotherUnlock()
        {
            var result = new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구",
                30, 90, 3, 78, 0, false, 0, true);
            Assert.That(result.Reward, Is.EqualTo(30));
            Assert.That(result.FirstClear, Is.False);
            Assert.That(result.UnlockedStageNumber, Is.Zero);
            Assert.That(result.CanAdvance, Is.True);
        }

        [TestCase(DuelMatchOutcome.EnemyVictory)]
        [TestCase(DuelMatchOutcome.Draw)]
        public void NonVictory_CanRetryButCannotClaimRewardsOrAdvance(DuelMatchOutcome outcome)
        {
            var result = new BattleResult(outcome, false, 2, "이끼 낀 오솔길",
                0, 45, 5, 0, 12, false, 0, false);
            Assert.That(result.Victory, Is.False);
            Assert.That(result.CanRetry, Is.True);
            Assert.That(result.Reward, Is.Zero);
            Assert.That(result.CanAdvance, Is.False);
        }

        [Test]
        public void TutorialVictory_CanUseStageZeroWithoutChangingJourney()
        {
            var result = new BattleResult(DuelMatchOutcome.PlayerVictory, true, 0, "연습 숲길",
                0, 75, 3, 100, 0, false, 0, false);
            Assert.That(result.Victory, Is.True);
            Assert.That(result.IsTutorial, Is.True);
            Assert.That(result.Reward, Is.Zero);
            Assert.That(result.Currency, Is.EqualTo(75));
            Assert.That(result.FirstClear, Is.False);
            Assert.That(result.UnlockedStageNumber, Is.Zero);
            Assert.That(result.CanAdvance, Is.False);
            Assert.That(result.CanRetry, Is.True);
        }

        [TestCase(DuelMatchOutcome.InProgress)]
        [TestCase((DuelMatchOutcome)99)]
        public void UnfinishedOrUnknownOutcome_CannotBecomeResult(DuelMatchOutcome outcome)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(outcome: outcome));
        }

        [TestCase(DuelMatchOutcome.EnemyVictory)]
        [TestCase(DuelMatchOutcome.Draw)]
        public void NonVictory_RejectsRewardFirstClearUnlockAndAdvance(DuelMatchOutcome outcome)
        {
            Assert.Throws<ArgumentException>(() => Create(outcome: outcome, reward: 1));
            Assert.Throws<ArgumentException>(() => Create(outcome: outcome, firstClear: true));
            Assert.Throws<ArgumentException>(() => Create(outcome: outcome, unlockedStageNumber: 2));
            Assert.Throws<ArgumentException>(() => Create(outcome: outcome, canAdvance: true));
        }

        [Test]
        public void Tutorial_RejectsCampaignRewardsAndProgress()
        {
            Assert.Throws<ArgumentException>(() => Create(isTutorial: true, reward: 1));
            Assert.Throws<ArgumentException>(() => Create(isTutorial: true, firstClear: true));
            Assert.Throws<ArgumentException>(() => Create(isTutorial: true, unlockedStageNumber: 2));
            Assert.Throws<ArgumentException>(() => Create(isTutorial: true, canAdvance: true));
        }

        [Test]
        public void NewUnlock_RequiresFirstClearAndHigherStage()
        {
            Assert.Throws<ArgumentException>(() => Create(unlockedStageNumber: 2));
            Assert.Throws<ArgumentException>(() => Create(firstClear: true, unlockedStageNumber: 1));
        }

        [Test]
        public void InvalidBoundaries_AreRejectedAtConstruction()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(stageNumber: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(isTutorial: true, stageNumber: -1));
            Assert.Throws<ArgumentException>(() => Create(stageName: null));
            Assert.Throws<ArgumentException>(() => Create(stageName: " "));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(reward: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(currency: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(roundNumber: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(playerHealth: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(enemyHealth: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(unlockedStageNumber: -1));
        }

        private static BattleResult Create(DuelMatchOutcome outcome = DuelMatchOutcome.PlayerVictory,
            bool isTutorial = false, int stageNumber = 1, string stageName = "숲길 입구",
            int reward = 0, int currency = 0, int roundNumber = 1, int playerHealth = 100, int enemyHealth = 0,
            bool firstClear = false, int unlockedStageNumber = 0, bool canAdvance = false)
            => new BattleResult(outcome, isTutorial, stageNumber, stageName, reward, currency, roundNumber,
                playerHealth, enemyHealth, firstClear, unlockedStageNumber, canAdvance);
    }
}
