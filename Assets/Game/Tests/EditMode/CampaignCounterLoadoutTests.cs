using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    public sealed class CampaignCounterLoadoutTests
    {
        private const int PracticeCounter = 600;

        [Test]
        public void FreshRun_OwnsThePracticeCounterWithoutChangingItsNineLaneSkills()
        {
            var run = new CampaignRun();
            Assert.That(run.GetOwnedCounterSkills().Select(skill => skill.Id), Does.Contain(PracticeCounter));
            Assert.That(run.GetOwnedCounterSkills().Single(skill => skill.Id == PracticeCounter),
                Is.SameAs(run.GetOwnedSkill(PracticeCounter).Skill));
            Assert.That(run.EquippedCounterSkill, Is.Null);
            Assert.That(run.DraftCounterSkill, Is.Null);
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.CreateDuel().PlayerCounter, Is.Null);
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.CaptureSave().CounterSkillId, Is.Zero);
        }

        [Test]
        public void DraftCounter_UsesTheExistingSaveAndResetFlow()
        {
            var run = new CampaignRun();
            Assert.That(run.TryEquipCounter(PracticeCounter), Is.True);
            Assert.That(run.DraftCounterSkill.Id, Is.EqualTo(PracticeCounter));
            Assert.That(run.EquippedCounterSkill, Is.Null);
            Assert.That(run.CaptureSave().CounterSkillId, Is.Zero, "Draft edits are not persistent.");
            Assert.That(run.CreateDuel().PlayerCounter, Is.Null, "A snapshot sees the saved loadout.");
            Assert.That(run.HasLoadoutChanges, Is.True);
            Assert.That(run.CanStartStage(1), Is.False);
            Assert.That(run.TryResetLoadout(), Is.True);
            Assert.That(run.DraftCounterSkill, Is.Null);
            Assert.That(run.HasLoadoutChanges, Is.False);

            Assert.That(run.TryEquipCounter(PracticeCounter), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.EquippedCounterSkill, Is.SameAs(run.DraftCounterSkill));
            Assert.That(run.CaptureSave().CounterSkillId, Is.EqualTo(PracticeCounter));
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.TryEquipCounter(0), Is.True);
            Assert.That(run.DraftCounterSkill, Is.Null);
            Assert.That(run.TryResetLoadout(), Is.True);
            Assert.That(run.DraftCounterSkill.Id, Is.EqualTo(PracticeCounter));
            Assert.That(run.TryEquipCounter(0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.EquippedCounterSkill, Is.Null);
        }

        [Test]
        public void FailedLaneSave_DoesNotCommitTheCounterDraft()
        {
            var run = new CampaignRun();
            Assert.That(run.TryEquipCounter(PracticeCounter), Is.True);
            Assert.That(run.TryUnequipSkill(1), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.False);
            Assert.That(run.EquippedCounterSkill, Is.Null);
            Assert.That(run.CaptureSave().CounterSkillId, Is.Zero);
            Assert.That(run.GetEquippedLane(0)[0].SkillId, Is.EqualTo(1));
            Assert.That(run.TryEquipSkill(1), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.EquippedCounterSkill.Id, Is.EqualTo(PracticeCounter));
        }

        [Test]
        public void CounterAndOrdinarySlots_RejectEachOthersSkills()
        {
            var run = new CampaignRun();
            Assert.That(run.IsCounterSkill(PracticeCounter), Is.True);
            Assert.That(run.IsCounterSkill(1), Is.False);
            Assert.That(run.IsCounterSkill(999), Is.False);
            Assert.That(run.TryEquipCounter(1), Is.False);
            Assert.That(run.TryEquipCounter(999), Is.False);
            Assert.That(run.TryEquipCounter(-1), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(PracticeCounter, 0, 0), Is.False);
            Assert.That(run.TryUnequipSkill(1), Is.True);
            Assert.That(run.TryEquipSkill(PracticeCounter), Is.False);
            Assert.That(run.TryEquipCounter(PracticeCounter), Is.True);
            Assert.That(run.GetLoadoutSlot(0, 0), Is.Null);
        }

        [Test]
        public void BattleAndJourneyReset_KeepTheCounterSlotRules()
        {
            var run = EquippedRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryEquipCounter(0), Is.False);
            Assert.That(run.TryEquipCounter(PracticeCounter), Is.False);
            Assert.That(run.TrySaveLoadout(), Is.False);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(run.EquippedCounterSkill.Id, Is.EqualTo(PracticeCounter), "A starting counter survives a curriculum reset.");
            run.Reset();
            Assert.That(run.EquippedCounterSkill, Is.Null);
            Assert.That(run.DraftCounterSkill, Is.Null);
            Assert.That(run.GetOwnedSkill(PracticeCounter), Is.Not.Null);
            Assert.That(run.HasLoadoutChanges, Is.False);
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(8)]
        public void StageDuel_UsesTheOwnedCounterForEveryEnemyConstructionPath(int stage)
        {
            var run = EquippedRun();
            for (int number = 1; number < stage; number++) Win(run, number);
            Assert.That(run.TryStartStage(stage), Is.True);
            AssertCounter(run, run.CreateDuel());
        }

        [Test]
        public void TrainingDuel_ReceivesTheCounterWithoutPuttingItInALane()
        {
            var run = EquippedRun();
            Win(run, 1);
            Win(run, 2);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TryStartTraining(), Is.True);
            LegacyQueuedDuel duel = run.CreateDuel();
            AssertCounter(run, duel);
            Assert.That(duel.EnemyQueue, Is.Empty);
        }

        [Test]
        public void EquippedCounter_AnswersAnEmptySlotWithoutActOrLaneRotationAndRefillsNextTurn()
        {
            CampaignRun run = EquippedRun();
            LegacyQueuedDuel duel = run.CreateDuel();
            int firstAttack = duel.EnemyQueue.ToList().FindIndex(skill => skill.Kind == LegacySkillKind.Attack);
            Assert.That(firstAttack, Is.GreaterThanOrEqualTo(0));
            int act = duel.Act;
            int[][] lanes = Enumerable.Range(0, 3).Select(lane => duel.GetLane(lane).Select(skill => skill.Id).ToArray()).ToArray();
            Assert.That(duel.ForecastPlayerCounterSlots(), Does.Contain(firstAttack));
            duel.Commit();
            for (int slot = 0; slot < firstAttack; slot++) duel.ResolveNextSlot();
            LegacyCurrentSlot current = duel.BeginNextSlot();
            Assert.That(current.PendingPlayerCounter, Is.SameAs(run.EquippedCounterSkill));
            Assert.That(duel.PlayerCountersRemaining, Is.EqualTo(1));
            duel.ResolveNextHit();
            Assert.That(current.PlayerCountered, Is.True);
            Assert.That(current.PlayerSkill, Is.SameAs(run.EquippedCounterSkill));
            Assert.That(duel.PlayerCountersRemaining, Is.Zero);
            duel.ResolveNextSlot();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
            Assert.That(duel.Act, Is.EqualTo(act));
            for (int lane = 0; lane < 3; lane++)
                Assert.That(duel.GetLane(lane).Select(skill => skill.Id), Is.EqualTo(lanes[lane]));
            Assert.That(duel.IsFinished, Is.False);
            duel.BeginNextTurn();
            Assert.That(duel.PlayerCountersRemaining, Is.EqualTo(1));
        }

        [Test]
        public void SaveRoundTrip_RestoresTheCounterAndItsUpgradedOwnedInstance()
        {
            var run = EquippedRun();
            LegacySkill counter = run.EquippedCounterSkill;
            int before = counter.MinPower;
            for (int i = 0; i < 10; i++)
                Assert.That(run.TryGainClashExperience(counter, LegacySkillDefinitions.Skill(1)), Is.True);
            Assert.That(counter.MinPower, Is.EqualTo(before + 2));
            Assert.That(LegacySkillDefinitions.Skill(PracticeCounter).MinPower, Is.EqualTo(before));
            string text = GameSaveCodec.Serialize(new GameSave(0, run.CaptureSave()));
            Assert.That(text, Does.Contain("\ncounter-skill 600\n"));
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            var restored = new CampaignRun();
            Assert.That(restored.TryRestore(parsed.Campaign, out error), Is.True, error);
            Assert.That(restored.EquippedCounterSkill.Id, Is.EqualTo(PracticeCounter));
            Assert.That(restored.EquippedCounterSkill.MinPower, Is.EqualTo(before + 2));
            Assert.That(restored.GetOwnedSkill(PracticeCounter).Experience, Is.EqualTo(10));
            Assert.That(restored.HasLoadoutChanges, Is.False);
            AssertCounter(restored, restored.CreateDuel());
            Assert.That(GameSaveCodec.Serialize(new GameSave(0, restored.CaptureSave())), Is.EqualTo(text));
        }

        [Test]
        public void OlderSaveWithoutACounterLine_AddsOwnershipAndKeepsTheSlotEmpty()
        {
            const string olderSave = "turn-limbo-save 2\nprologue 4\ncurrency 0\ncleared\n" +
                "curriculum-done\nlane 0 1 2 7\nlane 1 3 4 8\nlane 2 5 6 9\n";
            Assert.That(GameSaveCodec.TryParse(olderSave, out GameSave parsed, out string error), Is.True, error);
            Assert.That(parsed.Campaign.CounterSkillId, Is.Zero);
            var restored = new CampaignRun();
            Assert.That(parsed.TryApply(new PrologueRun(), restored, out error), Is.True, error);
            Assert.That(restored.GetOwnedSkill(PracticeCounter), Is.Not.Null);
            Assert.That(restored.EquippedCounterSkill, Is.Null);
            Assert.That(restored.DraftCounterSkill, Is.Null);
            Assert.That(restored.CreateDuel().PlayerCounter, Is.Null);
            Assert.That(GameSaveCodec.Serialize(new GameSave(4, restored.CaptureSave())), Does.Not.Contain("counter-skill"));
        }

        [TestCase("counter-skill 600\ncounter-skill 600\n")]
        [TestCase("counter-skill 0\ncounter-skill 600\n")]
        [TestCase("counter-skill -1\n")]
        [TestCase("counter-skill text\n")]
        [TestCase("counter-skill\n")]
        [TestCase("counter-skill 600 1\n")]
        [TestCase("counter-skill 2147483648\n")]
        public void CounterSaveLine_RejectsMalformedOrRepeatedValues(string extra)
        {
            string valid = GameSaveCodec.Serialize(new GameSave(0, new CampaignRun().CaptureSave()));
            Assert.That(GameSaveCodec.TryParse(valid + extra, out GameSave parsed, out string error), Is.False);
            Assert.That(parsed, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase(1)]
        [TestCase(-1)]
        [TestCase(999)]
        public void Restore_RejectsAnInvalidCounterWithoutMutatingTheRun(int counterId)
        {
            var target = EquippedRun();
            LegacySkill before = target.EquippedCounterSkill;
            CampaignSave basis = new CampaignRun().CaptureSave();
            CampaignSave invalid = CopySave(basis, counterId);
            Assert.That(target.TryRestore(invalid, out string error), Is.False);
            Assert.That(error, Does.Contain("반격"));
            Assert.That(target.EquippedCounterSkill, Is.SameAs(before));
            Assert.That(target.HasLoadoutChanges, Is.False);
        }

        [Test]
        public void Restore_RejectsACounterStoredInAnOrdinaryLane()
        {
            CampaignSave basis = new CampaignRun().CaptureSave();
            int[][] lanes = basis.Loadout.Select(lane => lane.ToArray()).ToArray();
            lanes[0][0] = PracticeCounter;
            var invalid = new CampaignSave(0, basis.ClearedStages, basis.CurriculumCompleted, 0, lanes);
            Assert.That(new CampaignRun().TryRestore(invalid, out string error), Is.False);
            Assert.That(error, Does.Contain("반격 칸"));
        }

        [Test]
        public void CurriculumTreeMigration_PreservesAStartingCounterAndItsExperience()
        {
            var run = EquippedRun();
            Assert.That(run.TryGainClashExperience(run.EquippedCounterSkill, LegacySkillDefinitions.Skill(1)), Is.True);
            CampaignSave before = run.CaptureSave();
            var oldTree = new CampaignSave(before.Currency, before.ClearedStages, before.CurriculumCompleted,
                before.CurriculumPoints, before.Loadout, before.TrainingVictoryCount, before.SkillExperience,
                before.FieldPickups, CampaignCurriculum.TreeVersion + 1, PracticeCounter);
            var target = new CampaignRun();
            Assert.That(target.TryRestore(oldTree, out string error), Is.True, error);
            Assert.That(target.EquippedCounterSkill.Id, Is.EqualTo(PracticeCounter));
            Assert.That(target.GetOwnedSkill(PracticeCounter).Experience, Is.EqualTo(1));
        }

        [Test]
        public void AcquiredCounter_CannotBeEquippedUntilOwned_AndLeavesWithItsOnlyGrant()
        {
            List<string[]> rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            rows.Single(row => row[0] == PracticeCounter.ToString())[Array.IndexOf(rows[0], LegacySkillSheet.Column.Group)]
                = LegacySkillSheet.AcquisitionGroup;
            string sheet = CsvTable.Write(rows);
            var tree = new CurriculumTree(new[]
            {
                new CurriculumNode("counter-training", null, CurriculumBranch.Foundation, 0, 0, new[] { PracticeCounter }),
            });
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                var run = new CampaignRun(tree);
                Assert.That(run.GetOwnedCounterSkills(), Is.Empty);
                Assert.That(run.TryEquipCounter(PracticeCounter), Is.False);
                Win(run, 1);
                Assert.That(run.TryLearnCurriculumNode("counter-training"), Is.True);
                Assert.That(run.TryEquipCounter(PracticeCounter), Is.True);
                Assert.That(run.TrySaveLoadout(), Is.True);
                CampaignSave acquired = run.CaptureSave();
                var restored = new CampaignRun(tree);
                Assert.That(restored.TryRestore(acquired, out string error), Is.True, error);
                Assert.That(restored.EquippedCounterSkill.Id, Is.EqualTo(PracticeCounter));

                Assert.That(run.TryResetCurriculum(), Is.True);
                Assert.That(run.GetOwnedSkill(PracticeCounter), Is.Null);
                Assert.That(run.EquippedCounterSkill, Is.Null);
                Assert.That(run.DraftCounterSkill, Is.Null);
                Assert.That(run.CaptureSave().CounterSkillId, Is.Zero);
                Assert.That(run.HasLoadoutChanges, Is.False);
                Assert.That(run.EquippedSkillCount, Is.EqualTo(9));

                var oldTree = new CampaignSave(acquired.Currency, acquired.ClearedStages, acquired.CurriculumCompleted,
                    acquired.CurriculumPoints, acquired.Loadout, acquired.TrainingVictoryCount, acquired.SkillExperience,
                    acquired.FieldPickups, CampaignCurriculum.TreeVersion + 1, PracticeCounter);
                Assert.That(restored.TryRestore(oldTree, out error), Is.True, error);
                Assert.That(restored.GetOwnedCounterSkills(), Is.Empty);
                Assert.That(restored.EquippedCounterSkill, Is.Null);
                Assert.That(restored.DraftCounterSkill, Is.Null);
                Assert.That(restored.RestoreDroppedCurriculum, Is.True);
                Assert.That(restored.HasLoadoutChanges, Is.False);
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        private static CampaignRun EquippedRun()
        {
            var run = new CampaignRun();
            Assert.That(run.TryEquipCounter(PracticeCounter), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            return run;
        }

        private static void Win(CampaignRun run, int stage)
        {
            Assert.That(run.TryStartStage(stage), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
        }

        private static void AssertCounter(CampaignRun run, LegacyQueuedDuel duel)
        {
            Assert.That(duel.PlayerCounter.Skill, Is.SameAs(run.GetOwnedSkill(PracticeCounter).Skill));
            Assert.That(duel.PlayerCounter.UsesPerTurn,
                Is.EqualTo(LegacySkillDefinitions.Find(PracticeCounter).CounterUsesPerTurn));
            Assert.That(duel.PlayerCountersRemaining, Is.EqualTo(duel.PlayerCounter.UsesPerTurn));
            for (int lane = 0; lane < 3; lane++)
                Assert.That(duel.GetLane(lane).Any(skill => skill.Id == PracticeCounter), Is.False);
        }

        private static CampaignSave CopySave(CampaignSave source, int counterId)
            => new CampaignSave(source.Currency, source.ClearedStages, source.CurriculumCompleted,
                source.CurriculumPoints, source.Loadout, source.TrainingVictoryCount, source.SkillExperience,
                source.FieldPickups, source.CurriculumTreeVersion, counterId);
    }
}
