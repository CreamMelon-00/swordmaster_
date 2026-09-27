using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Tutorial;

namespace TurnLimbo.Core.Tests
{
    public sealed class TutorialProgressTests
    {
        [Test]
        public void Welcome_RequiresStartingBeforeAnyCombatInput()
        {
            var progress = new TutorialProgress();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Welcome));
            Assert.That(progress.StepNumber, Is.EqualTo(1));
            Assert.That(progress.StepCount, Is.EqualTo(8));
            Assert.That(progress.CanAdvance, Is.True);
            Assert.That(progress.AllowsCommit, Is.False);
            Assert.That(progress.ExpectedLane, Is.EqualTo(-1));
            for (int lane = 0; lane < 3; lane++) Assert.That(progress.AllowsQueue(lane), Is.False);
            progress.NotifyQueued(0);
            progress.NotifyInspected();
            progress.NotifyCommitted();
            progress.NotifyTurnBegan(2);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Welcome));
            Assert.That(progress.TryAdvance(), Is.True);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.QueueAttack));
        }

        [Test]
        public void GuidedQueue_OnlyAcceptsQThenWThenQ()
        {
            var progress = new TutorialProgress();
            progress.TryAdvance();
            Assert.That(progress.ExpectedLane, Is.Zero);
            Assert.That(progress.AllowsQueue(0), Is.True);
            Assert.That(progress.AllowsQueue(1), Is.False);
            progress.NotifyQueued(1);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.QueueAttack));
            Assert.That(progress.TryAdvance(), Is.False);
            progress.NotifyQueued(0);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.QueueFollowup));
            Assert.That(progress.ExpectedLane, Is.EqualTo(1));
            progress.NotifyQueued(2);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.QueueFollowup));
            progress.NotifyQueued(1);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.QueueGuard));
            Assert.That(progress.ExpectedLane, Is.Zero);
            progress.NotifyQueued(0);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.InspectEnemy));
            Assert.That(progress.ExpectedLane, Is.EqualTo(-1));
            Assert.That(progress.AllowsCommit, Is.False);
        }

        [Test]
        public void InspectAndCommit_AreRequiredBeforeWatchingClash()
        {
            TutorialProgress progress = QueuedProgress();
            progress.NotifyCommitted();
            progress.NotifyTurnBegan(2);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.InspectEnemy));
            progress.NotifyInspected();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.CommitQueue));
            Assert.That(progress.AllowsCommit, Is.True);
            Assert.That(progress.AllowsQueue(0), Is.False);
            progress.NotifyCommitted();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.WatchClash));
            Assert.That(progress.AllowsCommit, Is.False);
            progress.NotifyTurnBegan(1);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.WatchClash));
            progress.NotifyTurnBegan(2);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.TurnRecovery));
            Assert.That(progress.CanAdvance, Is.True);
            Assert.That(progress.StepNumber, Is.EqualTo(8));
        }

        [Test]
        public void FreeBattle_EnablesAllLanesAndDoesNotRepeatGuidance()
        {
            TutorialProgress progress = FreeProgress();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.FreeBattle));
            Assert.That(progress.CanAdvance, Is.False);
            Assert.That(progress.AllowsCommit, Is.True);
            Assert.That(progress.StepNumber, Is.EqualTo(progress.StepCount));
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.That(progress.AllowsQueue(lane), Is.True);
                progress.NotifyQueued(lane);
            }
            progress.NotifyInspected();
            progress.NotifyCommitted();
            progress.NotifyTurnBegan(3);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.FreeBattle));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void InvalidLane_IsNeverAccepted(int lane)
        {
            var progress = new TutorialProgress();
            progress.TryAdvance();
            Assert.That(progress.AllowsQueue(lane), Is.False);
            progress.NotifyQueued(lane);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.QueueAttack));
            Assert.That(FreeProgress().AllowsQueue(lane), Is.False);
        }

        [Test]
        public void Finish_BlocksAllFurtherGuidedInputAndIsIdempotent()
        {
            TutorialProgress progress = FreeProgress();
            progress.Finish();
            progress.Finish();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Complete));
            Assert.That(progress.StepNumber, Is.EqualTo(progress.StepCount));
            Assert.That(progress.AllowsCommit, Is.False);
            Assert.That(progress.CanAdvance, Is.False);
            Assert.That(progress.TryAdvance(), Is.False);
            for (int lane = 0; lane < 3; lane++) Assert.That(progress.AllowsQueue(lane), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(7)]
        [TestCase(83)]
        public void Stage_FirstGuidedTurnHasGuardInThirdSlotAndRecoversExactlySixAct(int seed)
        {
            LegacyQueuedDuel duel = TutorialStage.CreateDuel(seed);
            Assert.That(duel.Act, Is.EqualTo(3));
            Assert.That(duel.GetLane(0)[0].Id, Is.EqualTo(1));
            Assert.That(duel.GetLane(0)[1].Id, Is.EqualTo(7));
            Assert.That(duel.GetLane(1)[0].Id, Is.EqualTo(3));
            Assert.That(duel.EnemyQueue.Count, Is.EqualTo(3));
            Assert.That(duel.EnemyQueue[2].Property, Is.EqualTo(LegacySkillProperty.Hit));
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.Act, Is.Zero);
            Assert.That(duel.PlayerQueue[2].Id, Is.EqualTo(7));
            ResolveTurn(duel);
            Assert.That(duel.IsFinished, Is.False);
            Assert.That(duel.Player.Health, Is.EqualTo(100));
            Assert.That(duel.Enemy.Health, Is.EqualTo(32));
            Assert.That(duel.Enemy.Resistance, Is.InRange(4, 5));
            Assert.That(duel.NextActGain, Is.EqualTo(6));
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(6));
            Assert.That(duel.RoundNumber, Is.EqualTo(2));
            Assert.That(duel.EnemyQueue.Count, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(7)]
        [TestCase(83)]
        public void Stage_OrdinaryAffordableQueueActionsCanWinAfterGuidance(int seed)
        {
            LegacyQueuedDuel duel = TutorialStage.CreateDuel(seed);
            duel.TryQueueLane(0);
            duel.TryQueueLane(1);
            duel.TryQueueLane(0);
            ResolveTurn(duel);
            for (int turn = 0; turn < 8 && !duel.IsFinished; turn++)
            {
                duel.BeginNextTurn();
                while (duel.TryQueueLane(0)) { }
                ResolveTurn(duel);
            }
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(duel.Player.Health, Is.GreaterThan(0));
        }

        [Test]
        public void Stage_IsIndependentOfCampaignLoadoutProgressAndCurrency()
        {
            var campaign = new CampaignRun();
            campaign.TryUnequipSkill(2);
            LegacyQueuedDuel duel = TutorialStage.CreateDuel();
            Assert.That(duel.GetLane(0).Count, Is.EqualTo(2));
            Assert.That(campaign.IsSkillEquipped(2), Is.True);
            Assert.That(campaign.IsSkillInLoadout(2), Is.False);
            Assert.That(campaign.HasLoadoutChanges, Is.True);
            duel.TryQueueLane(0);
            duel.TryQueueLane(1);
            duel.TryQueueLane(0);
            ResolveTurn(duel);
            Assert.That(campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(campaign.Currency, Is.Zero);
            Assert.That(campaign.ClearedStageCount, Is.Zero);
            Assert.That(campaign.HighestUnlockedStage, Is.EqualTo(1));
        }

        private static TutorialProgress QueuedProgress()
        {
            var progress = new TutorialProgress();
            progress.TryAdvance();
            progress.NotifyQueued(0);
            progress.NotifyQueued(1);
            progress.NotifyQueued(0);
            return progress;
        }

        private static TutorialProgress FreeProgress()
        {
            TutorialProgress progress = QueuedProgress();
            progress.NotifyInspected();
            progress.NotifyCommitted();
            progress.NotifyTurnBegan(2);
            progress.TryAdvance();
            return progress;
        }

        private static void ResolveTurn(LegacyQueuedDuel duel)
        {
            duel.Commit();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
        }
    }
}
