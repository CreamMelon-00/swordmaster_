using System;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class CampaignLobbyTests
    {
        [Test]
        public void NewJourney_StartsInLobbyWithOnlyFirstStageUnlockedAndOriginalEquippedOrder()
        {
            var run = new CampaignRun();
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(1));
            Assert.That(run.ClearedStageCount, Is.Zero);
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.CanStartStage(1), Is.True);
            Assert.That(run.CanStartStage(2), Is.False);
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, EquippedIds(run, 0));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, EquippedIds(run, 1));
            CollectionAssert.AreEqual(new[] { 5, 6, 9 }, EquippedIds(run, 2));
            for (int id = 1; id <= 9; id++) Assert.That(run.IsSkillEquipped(id), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.Currency, Is.Zero);
            Assert.That(run.ReturnToLobby(), Is.True);
        }

        [Test]
        public void InvalidOrLockedStagesAndInvalidLanes_AreRejectedWithoutMutation()
        {
            var run = new CampaignRun();
            foreach (int number in new[] { -1, 0, 2, 8, 9, int.MaxValue })
            {
                Assert.That(run.CanStartStage(number), Is.False);
                Assert.That(run.TryStartStage(number), Is.False);
                Assert.That(run.IsStageCleared(number), Is.False);
            }
            Assert.That(run.GetStageReward(0), Is.Zero);
            Assert.That(run.GetStageReward(9), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetStage(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetStage(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetEquippedLane(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetEquippedLane(3));
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(run.StageNumber, Is.EqualTo(1));
            Assert.That(run.GetStage(2).EnemyHealth, Is.EqualTo(95));
            Assert.That(run.GetStage(2).Number, Is.EqualTo(2));
        }

        [Test]
        public void FirstClear_UnlocksNextAndLobbyKeepsRewardOwnershipAndStageSelection()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.IsStageCleared(1), Is.True);
            Assert.That(run.IsStageCleared(2), Is.False);
            Assert.That(run.ClearedStageCount, Is.EqualTo(1));
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(2));
            Assert.That(run.GetStageReward(1), Is.EqualTo(30));
            Assert.That(run.GetStageReward(2), Is.EqualTo(70));
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.LastReward, Is.EqualTo(60));
            Assert.That(run.Currency, Is.EqualTo(60));
            Assert.That(run.StageNumber, Is.EqualTo(1));
            Assert.That(run.CanStartStage(2), Is.True);
            Assert.That(run.CanStartStage(3), Is.False);
            Assert.That(run.TryStartStage(2), Is.True);
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.StageNumber, Is.EqualTo(2));
            Assert.That(run.CurrentStage.EnemyHealth, Is.EqualTo(95));
            Assert.That(run.TryStartStage(1), Is.False);
            Assert.That(run.CanStartStage(1), Is.False);
            Assert.That(run.ReturnToLobby(), Is.False);
        }

        [Test]
        public void ClearedStageReplay_PaysHalfOnceWithoutDoubleClearingOrUnlocking()
        {
            var run = new CampaignRun();
            WinStage(run, 1);
            run.ReturnToLobby();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.LastReward, Is.EqualTo(30));
            Assert.That(run.Currency, Is.EqualTo(90));
            Assert.That(run.ClearedStageCount, Is.EqualTo(1));
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(2));
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.Currency, Is.EqualTo(90));
            WinStage(run, 2);
            Assert.That(run.LastReward, Is.EqualTo(70));
            Assert.That(run.Currency, Is.EqualTo(160));
            Assert.That(run.ClearedStageCount, Is.EqualTo(2));
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(3));
        }

        [Test]
        public void FailedBattle_ReturnsToLobbyWithoutPayingOrUnlockingAndCanSelectEarlierStage()
        {
            var run = new CampaignRun();
            WinStage(run, 1);
            run.ReturnToLobby();
            run.TryStartStage(2);
            run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory);
            Assert.That(run.Currency, Is.EqualTo(60));
            Assert.That(run.ClearedStageCount, Is.EqualTo(1));
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(2));
            Assert.That(run.TryStartStage(2), Is.False);
            Assert.That(run.CanStartStage(2), Is.False);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.CreateDuel().Enemy.Health, Is.EqualTo(80));
            Assert.That(run.StageNumber, Is.EqualTo(1));
        }

        [Test]
        public void AbandonBattle_ReturnsToLobbyWithoutAwardOrProgressLoss()
        {
            var run = new CampaignRun();
            Assert.That(run.TryAbandonBattle(), Is.False);
            WinStage(run, 1);
            Assert.That(run.TryStartStage(2), Is.True);
            Assert.That(run.TryAbandonBattle(), Is.True);
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(run.Currency, Is.EqualTo(60));
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.StageNumber, Is.EqualTo(2));
            Assert.That(run.IsStageCleared(2), Is.False);
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(2));
            Assert.That(run.TryAbandonBattle(), Is.False);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.TryStartStage(2), Is.True);
        }

        [Test]
        public void LobbyCurriculum_GrantsOwnedSkillButRequiresRoomAndExplicitEquip()
        {
            var run = new CampaignRun();
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            WinStage(run, 1);
            run.ReturnToLobby();
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(11));
            Assert.That(run.IsSkillEquipped(14), Is.False);
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.TryEquipSkill(14), Is.False);
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.False, "A completed node is not taken again.");
            Assert.That(run.TryUnequipSkill(7), Is.True);
            Assert.That(run.TryEquipSkill(14), Is.True);
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, EquippedIds(run, 0));
            Assert.That(run.HasLoadoutChanges, Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 2, 14 }, EquippedIds(run, 0));
            CollectionAssert.AreEqual(new[] { 1, 2, 14 }, DuelIds(run.CreateDuel().GetLane(0)));
            Assert.That(run.TryStartStage(2), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 2, 14 }, DuelIds(run.CreateDuel().GetLane(0)));
        }

        [Test]
        public void EquippedLanes_KeepSavedThreeWhileDraftCanBeEmptyAndExposeLiveReadOnlyViews()
        {
            var run = new CampaignRun();
            IReadOnlyList<CampaignOwnedSkill> observedLane = run.GetEquippedLane(0);
            Assert.That(run.TryEquipSkill(1), Is.False);
            Assert.That(run.TryEquipSkill(14), Is.False);
            Assert.That(run.TryEquipSkill(-1), Is.False);
            Assert.That(run.TryUnequipSkill(-1), Is.False);
            Assert.That(run.TryUnequipSkill(1), Is.True);
            Assert.That(observedLane.Count, Is.EqualTo(3));
            Assert.That(run.TryUnequipSkill(2), Is.True);
            Assert.That(run.TryUnequipSkill(7), Is.True);
            Assert.That(run.GetLoadoutCount(0), Is.Zero);
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.CanStartStage(1), Is.False);
            Assert.That(run.TrySaveLoadout(), Is.False);
            Assert.That(run.TryEquipSkill(1), Is.True);
            Assert.That(run.TryEquipSkill(2), Is.True);
            Assert.That(run.TryEquipSkill(7), Is.True);
            Assert.That(observedLane.Count, Is.EqualTo(3));
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, EquippedIds(run, 0));
            Assert.Throws<NotSupportedException>(() => ((IList<CampaignOwnedSkill>)observedLane).Clear());
        }

        [Test]
        public void EquippedReorder_ChangesOnlyItsLaneAndTheActualBattleQueueOrder()
        {
            var run = new CampaignRun();
            Assert.That(run.TryMoveEquippedSkill(1, -1), Is.False);
            Assert.That(run.TryMoveEquippedSkill(7, 1), Is.False);
            Assert.That(run.TryMoveEquippedSkill(1, 0), Is.False);
            Assert.That(run.TryMoveEquippedSkill(1, 2), Is.False);
            Assert.That(run.TryMoveEquippedSkill(14, 1), Is.False);
            Assert.That(run.TryMoveEquippedSkill(2, 1), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, EquippedIds(run, 0));
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 7, 2 }, EquippedIds(run, 0));
            Assert.That(run.TryMoveEquippedSkill(7, -1), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 7, 1, 2 }, EquippedIds(run, 0));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, EquippedIds(run, 1));
            Assert.That(run.TryStartStage(1), Is.True);
            LegacyQueuedDuel duel = run.CreateDuel();
            CollectionAssert.AreEqual(new[] { 7, 1, 2 }, DuelIds(duel.GetLane(0)));
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.PlayerQueue[0].Id, Is.EqualTo(7));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, DuelIds(duel.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 7, 1, 2 }, EquippedIds(run, 0));
        }

        [Test]
        public void Battle_BlocksCurriculumEquipmentOrderAndLobbyReturnUntilAbandonedOrFinished()
        {
            var run = new CampaignRun();
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            WinStage(run, 1);
            run.ReturnToLobby();
            Assert.That(run.TryUnequipSkill(7), Is.True);
            Assert.That(run.TryEquipSkill(14), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.TrySelectCurriculumNode("diagonal-cut"), Is.True);
            Assert.That(run.TryStartStage(2), Is.True);
            Assert.That(run.TryEquipSkill(14), Is.False);
            Assert.That(run.TryUnequipSkill(1), Is.False);
            Assert.That(run.TryMoveEquippedSkill(1, 1), Is.False);
            Assert.That(run.TrySelectCurriculumNode("advance"), Is.False);
            Assert.That(run.TryResetCurriculum(), Is.False);
            Assert.That(run.ReturnToLobby(), Is.False);
            Assert.That(run.Curriculum.Active.Id, Is.EqualTo("diagonal-cut"));
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(11));
            CollectionAssert.AreEqual(new[] { 1, 2, 14 }, EquippedIds(run, 0));
            Assert.That(run.TryAbandonBattle(), Is.True);
            Assert.That(run.Curriculum.Active.Id, Is.EqualTo("diagonal-cut"), "An abandoned battle does not count.");
            Assert.That(run.TryUnequipSkill(14), Is.True);
            Assert.That(run.TryEquipSkill(7), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
        }

        [Test]
        public void AllClearedJourney_ReturnsToLobbyAndFinalStageCanBeReplayedForHalfReward()
        {
            var run = new CampaignRun();
            for (int number = 1; number <= run.StageCount; number++) WinStage(run, number);
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Completed));
            Assert.That(run.ClearedStageCount, Is.EqualTo(8));
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(8));
            Assert.That(run.Currency, Is.EqualTo(760));
            Assert.That(run.TryStartStage(8), Is.False);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.LastReward, Is.EqualTo(130));
            Assert.That(run.GetStageReward(8), Is.EqualTo(65));
            WinStage(run, 8);
            Assert.That(run.LastReward, Is.EqualTo(65));
            Assert.That(run.Currency, Is.EqualTo(825));
            Assert.That(run.ClearedStageCount, Is.EqualTo(8));
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(8));
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True, "The curriculum stays open after the last stage.");
        }

        [Test]
        public void Reset_ReturnsToFreshLobbyAndDiscardsClearHistoryEquipmentChangesAndCurriculum()
        {
            var run = new CampaignRun();
            run.TrySelectCurriculumNode("horizontal-cut");
            WinStage(run, 1);
            WinStage(run, 2);
            run.ReturnToLobby();
            run.TrySelectCurriculumNode("diagonal-cut");
            run.TryUnequipSkill(7);
            run.TryEquipSkill(14);
            run.TryMoveEquippedSkill(14, -1);
            run.Reset();
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(run.StageNumber, Is.EqualTo(1));
            Assert.That(run.Currency, Is.Zero);
            Assert.That(run.LastReward, Is.Zero);
            Assert.That(run.ClearedStageCount, Is.Zero);
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(1));
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.Curriculum.Active, Is.Null);
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.CanSaveLoadout, Is.True);
            Assert.That(run.IsSkillEquipped(14), Is.False);
            Assert.That(run.IsSkillInLoadout(14), Is.False);
            Assert.That(run.GetStageReward(1), Is.EqualTo(60));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, EquippedIds(run, 0));
            for (int number = 1; number <= 8; number++) Assert.That(run.IsStageCleared(number), Is.False);
        }

        private static void WinStage(CampaignRun run, int number)
        {
            Assert.That(run.TryStartStage(number), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
        }

        private static int[] EquippedIds(CampaignRun run, int laneIndex)
        {
            IReadOnlyList<CampaignOwnedSkill> lane = run.GetEquippedLane(laneIndex);
            var ids = new int[lane.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = lane[i].SkillId;
            return ids;
        }

        private static int[] DuelIds(IReadOnlyList<LegacySkill> lane)
        {
            var ids = new int[lane.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = lane[i].Id;
            return ids;
        }
    }
}
