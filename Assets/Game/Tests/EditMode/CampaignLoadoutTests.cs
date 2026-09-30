using System;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class CampaignLoadoutTests
    {
        [Test]
        public void FreshDraft_HasThreeFixedSlotsPerLaneAndCanSaveWithoutChanges()
        {
            var run = new CampaignRun();
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.CanSaveLoadout, Is.True);
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.That(run.GetLoadoutCount(lane), Is.EqualTo(3));
                for (int slot = 0; slot < 3; slot++)
                    Assert.That(run.GetLoadoutSlot(lane, slot), Is.SameAs(run.GetEquippedLane(lane)[slot]));
            }
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.CanStartStage(1), Is.True);
            Assert.That(run.HasLoadoutChanges, Is.False);
        }

        [Test]
        public void EmptyDraft_PreservesCommittedLoadoutAndCannotSaveOrStartBattle()
        {
            var run = new CampaignRun();
            for (int id = 1; id <= 9; id++) Assert.That(run.TryUnequipSkill(id), Is.True);
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.That(run.GetLoadoutCount(lane), Is.Zero);
                Assert.That(run.GetEquippedLane(lane).Count, Is.EqualTo(3));
                for (int slot = 0; slot < 3; slot++) Assert.That(run.GetLoadoutSlot(lane, slot), Is.Null);
            }
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.HasLoadoutChanges, Is.True);
            Assert.That(run.CanSaveLoadout, Is.False);
            Assert.That(run.TrySaveLoadout(), Is.False);
            Assert.That(run.CanStartStage(1), Is.False);
            Assert.That(run.TryStartStage(1), Is.False);
            Assert.That(run.CreateDuel().GetLane(0).Count, Is.EqualTo(3));
        }

        [Test]
        public void PlacingExistingSkill_SwapsWithOccupiedOrEmptySlotWithoutDuplicates()
        {
            var run = new CampaignRun();
            Assert.That(run.TryPlaceLoadoutSkill(1, 0, 2), Is.True);
            CollectionAssert.AreEqual(new[] { 7, 2, 1 }, DraftIds(run, 0));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, SavedIds(run, 0));
            Assert.That(run.TryUnequipSkill(2), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(7, 0, 1), Is.True);
            CollectionAssert.AreEqual(new[] { 0, 7, 1 }, DraftIds(run, 0));
            Assert.That(run.GetLoadoutCount(0), Is.EqualTo(2));
            Assert.That(run.IsSkillInLoadout(2), Is.False);
            Assert.That(run.IsSkillEquipped(2), Is.True);
            Assert.That(run.TryEquipSkill(2), Is.True);
            CollectionAssert.AreEqual(new[] { 2, 7, 1 }, DraftIds(run, 0));
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 2, 7, 1 }, SavedIds(run, 0));
            var ids = new HashSet<int>();
            for (int lane = 0; lane < 3; lane++)
                for (int slot = 0; slot < 3; slot++) Assert.That(ids.Add(run.GetLoadoutSlot(lane, slot).SkillId), Is.True);
        }

        [Test]
        public void PlacingUnassignedOwnedSkill_ReplacesTargetWithoutChangingOtherSlots()
        {
            CampaignRun run = CurriculumRun();
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.IsSkillInLoadout(14), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(14, 0, 1), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 14, 7 }, DraftIds(run, 0));
            Assert.That(run.IsSkillInLoadout(2), Is.False);
            Assert.That(run.IsSkillEquipped(2), Is.True);
            Assert.That(run.IsSkillEquipped(14), Is.False);
            Assert.That(run.GetLoadoutCount(0), Is.EqualTo(3));
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.IsSkillEquipped(14), Is.True);
            Assert.That(run.IsSkillEquipped(2), Is.False);
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, SavedIds(run, 1));
        }

        [Test]
        public void FailedSave_IsAtomicAcrossAllLanesAndLeavesExistingViewsUntouched()
        {
            var run = new CampaignRun();
            IReadOnlyList<CampaignOwnedSkill> observed = run.GetEquippedLane(0);
            CampaignOwnedSkill originalFirst = observed[0];
            run.TryPlaceLoadoutSkill(1, 0, 2);
            run.TryUnequipSkill(3);
            Assert.That(run.TrySaveLoadout(), Is.False);
            Assert.That(observed[0], Is.SameAs(originalFirst));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, SavedIds(run, 0));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, SavedIds(run, 1));
            CollectionAssert.AreEqual(new[] { 5, 6, 9 }, SavedIds(run, 2));
            Assert.That(run.HasLoadoutChanges, Is.True);
            Assert.That(run.TryEquipSkill(3), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(observed, Is.SameAs(run.GetEquippedLane(0)));
            Assert.That(observed[0].SkillId, Is.EqualTo(7));
        }

        [Test]
        public void DirtyButCompleteDraft_BlocksStageEntryUntilSavedOrReset()
        {
            var run = new CampaignRun();
            Assert.That(run.TryPlaceLoadoutSkill(7, 0, 0), Is.True);
            Assert.That(run.CanSaveLoadout, Is.True);
            Assert.That(run.HasLoadoutChanges, Is.True);
            Assert.That(run.CanStartStage(1), Is.False);
            Assert.That(run.TryStartStage(1), Is.False);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.CanStartStage(1), Is.True);
            run.TryPlaceLoadoutSkill(1, 0, 0);
            Assert.That(run.TryResetLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 7, 2, 1 }, DraftIds(run, 0));
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.TryStartStage(1), Is.True);
        }

        [Test]
        public void ResetDraft_RestoresSavedPositionsEvenAfterAllSlotsAreRemoved()
        {
            CampaignRun run = CurriculumRun();
            run.TryPlaceLoadoutSkill(14, 0, 2);
            run.TrySaveLoadout();
            for (int id = 1; id <= 9; id++) run.TryUnequipSkill(id);
            run.TryUnequipSkill(14);
            Assert.That(run.TryResetLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 2, 14 }, DraftIds(run, 0));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, DraftIds(run, 1));
            CollectionAssert.AreEqual(new[] { 5, 6, 9 }, DraftIds(run, 2));
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.CanSaveLoadout, Is.True);
        }

        [Test]
        public void AdjacentMove_SwapsWithEmptyFixedSlotInsteadOfCompressingLane()
        {
            var run = new CampaignRun();
            Assert.That(run.TryUnequipSkill(2), Is.True);
            Assert.That(run.TryMoveEquippedSkill(1, 1), Is.True);
            CollectionAssert.AreEqual(new[] { 0, 1, 7 }, DraftIds(run, 0));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, SavedIds(run, 0));
            Assert.That(run.TryEquipSkill(2), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 2, 1, 7 }, SavedIds(run, 0));
        }

        [Test]
        public void BattleSnapshots_UseOnlySavedOrderAndExistingSnapshotsRemainIndependent()
        {
            var run = new CampaignRun();
            LegacyQueuedDuel oldSnapshot = run.CreateDuel();
            run.TryPlaceLoadoutSkill(7, 0, 0);
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, DuelIds(run.CreateDuel().GetLane(0)));
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 7, 2, 1 }, DuelIds(run.CreateDuel().GetLane(0)));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, DuelIds(oldSnapshot.GetLane(0)));
            Assert.That(run.TryStartStage(1), Is.True);
            LegacyQueuedDuel battle = run.CreateDuel();
            Assert.That(battle.TryQueueLane(0), Is.True);
            Assert.That(battle.PlayerQueue[0].Id, Is.EqualTo(7));
            CollectionAssert.AreEqual(new[] { 7, 2, 1 }, SavedIds(run, 0));
        }

        [Test]
        public void SavedViews_CannotBypassOwnershipOrCountInvariants()
        {
            var run = new CampaignRun();
            var lane = (IList<CampaignOwnedSkill>)run.GetEquippedLane(0);
            Assert.Throws<NotSupportedException>(() => lane.Clear());
            Assert.Throws<NotSupportedException>(() => lane.RemoveAt(0));
            Assert.Throws<NotSupportedException>(() => lane[0] = null);
            Assert.That(run.GetLoadoutCount(0), Is.EqualTo(3));
            Assert.That(run.HasLoadoutChanges, Is.False);
        }

        [Test]
        public void InvalidIdsIndicesAndLaneAssignments_LeaveDraftAndCommittedUnchanged()
        {
            var run = new CampaignRun();
            Assert.That(run.TryPlaceLoadoutSkill(14, 0, 0), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(-1, 0, 0), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(1, -1, 0), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(1, 3, 0), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(1, 0, -1), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(1, 0, 3), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(1, 1, 0), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(3, 0, 0), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(1, 0, 0), Is.True);
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetLoadoutSlot(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetLoadoutSlot(3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetLoadoutSlot(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetLoadoutSlot(0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetLoadoutCount(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetLoadoutCount(3));
        }

        [TestCase(CampaignPhase.Battle)]
        [TestCase(CampaignPhase.Failed)]
        [TestCase(CampaignPhase.Completed)]
        public void NonEditablePhases_BlockEveryDraftMutationSaveAndReset(CampaignPhase phase)
        {
            CampaignRun run = RunInPhase(phase);
            Assert.That(run.CanSaveLoadout, Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(1, 0, 2), Is.False);
            Assert.That(run.TryUnequipSkill(1), Is.False);
            Assert.That(run.TryEquipSkill(1), Is.False);
            Assert.That(run.TryMoveEquippedSkill(1, 1), Is.False);
            Assert.That(run.TrySaveLoadout(), Is.False);
            Assert.That(run.TryResetLoadout(), Is.False);
            Assert.That(run.HasLoadoutChanges, Is.False);
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, DraftIds(run, 0));
        }

        [Test]
        public void Maintenance_RequiresSavingCompleteDraftBeforeNextStage()
        {
            var run = new CampaignRun();
            run.TryStartStage(1);
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Maintenance));
            Assert.That(run.TryPlaceLoadoutSkill(7, 0, 0), Is.True);
            Assert.That(run.TryStartNextStage(), Is.False);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.TryStartNextStage(), Is.True);
            Assert.That(run.StageNumber, Is.EqualTo(2));
        }

        [Test]
        public void CurriculumReset_DiscardsTheDraftAndRebasesItOnTheRefilledSavedLanes()
        {
            CampaignRun run = CurriculumRun();
            LegacyQueuedDuel oldSnapshot = run.CreateDuel();
            Assert.That(run.TryPlaceLoadoutSkill(14, 0, 0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 14, 2, 7 }, SavedIds(run, 0));
            run.TryUnequipSkill(2);
            run.TryUnequipSkill(3);
            Assert.That(run.CanSaveLoadout, Is.False);
            Assert.That(run.TryResetCurriculum(), Is.True);
            CollectionAssert.AreEqual(new[] { 2, 7, 1 }, SavedIds(run, 0), "The starting skill fills the freed slot.");
            CollectionAssert.AreEqual(new[] { 2, 7, 1 }, DraftIds(run, 0));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, DraftIds(run, 1), "Unsaved edits are discarded.");
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.CanSaveLoadout, Is.True);
            Assert.That(run.CanStartStage(1), Is.True);
            Assert.That(run.GetLoadoutSlot(0, 0), Is.SameAs(run.GetEquippedLane(0)[0]));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, DuelIds(oldSnapshot.GetLane(0)));
            CollectionAssert.AreEqual(new[] { 2, 7, 1 }, DuelIds(run.CreateDuel().GetLane(0)));
        }

        [Test]
        public void JourneyReset_RebasesDraftOwnershipOrderAndDirtyState()
        {
            CampaignRun run = CurriculumRun();
            run.TryPlaceLoadoutSkill(14, 0, 0);
            run.TryUnequipSkill(4);
            run.Reset();
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.CanSaveLoadout, Is.True);
            Assert.That(run.IsSkillInLoadout(14), Is.False);
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.Currency, Is.Zero);
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, DraftIds(run, 0));
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, DraftIds(run, 1));
            Assert.That(run.GetLoadoutSlot(0, 0), Is.SameAs(run.GetEquippedLane(0)[0]));
        }

        /// <summary>A lobby run that owns skill 14 from the curriculum without having equipped it.</summary>
        private static CampaignRun CurriculumRun()
        {
            var run = new CampaignRun();
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            return run;
        }

        private static CampaignRun RunInPhase(CampaignPhase phase)
        {
            var run = new CampaignRun();
            if (phase == CampaignPhase.Completed)
            {
                for (int stage = 1; stage <= run.StageCount; stage++)
                {
                    Assert.That(run.TryStartStage(stage), Is.True);
                    Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                }
            }
            else
            {
                run.TryStartStage(1);
                if (phase == CampaignPhase.Failed) run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory);
            }
            Assert.That(run.Phase, Is.EqualTo(phase));
            return run;
        }

        private static int[] DraftIds(CampaignRun run, int lane)
        {
            var ids = new int[3];
            for (int slot = 0; slot < ids.Length; slot++) ids[slot] = run.GetLoadoutSlot(lane, slot)?.SkillId ?? 0;
            return ids;
        }

        private static int[] SavedIds(CampaignRun run, int lane)
        {
            IReadOnlyList<CampaignOwnedSkill> skills = run.GetEquippedLane(lane);
            var ids = new int[skills.Count];
            for (int slot = 0; slot < ids.Length; slot++) ids[slot] = skills[slot].SkillId;
            return ids;
        }

        private static int[] DuelIds(IReadOnlyList<LegacySkill> skills)
        {
            var ids = new int[skills.Count];
            for (int slot = 0; slot < ids.Length; slot++) ids[slot] = skills[slot].Id;
            return ids;
        }
    }
}
