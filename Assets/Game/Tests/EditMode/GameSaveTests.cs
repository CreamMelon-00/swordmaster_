using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;

namespace TurnLimbo.Core.Tests
{
    public sealed class GameSaveTests
    {
        [Test]
        public void RoundTrip_RestoresArcStagesCurriculumAndTheSavedLoadoutOnly()
        {
            CampaignRun source = PlayedCampaign();
            var prologue = new PrologueRun();
            Assert.That(prologue.TryComplete(1, DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(prologue.TryComplete(2, DuelMatchOutcome.PlayerVictory), Is.True);
            // An unsaved draft edit is not progress and must not reach the file.
            Assert.That(source.TryUnequipSkill(source.GetEquippedLane(1)[0].SkillId), Is.True);
            Assert.That(source.HasLoadoutChanges, Is.True);

            string text = GameSaveCodec.Serialize(GameSave.Capture(prologue, source));
            StringAssert.Contains($"\ncurriculum-tree {CampaignCurriculum.TreeVersion}\n", text, "The tree version is always written.");
            StringAssert.Contains("\ncurriculum-done horizontal-cut preparation advance\ncurriculum-points 6\n", text,
                "Completion order is kept, then the unspent 수련 점수.");
            StringAssert.DoesNotContain("curriculum-active", text, "No node is in progress any more.");
            StringAssert.DoesNotContain("\nskill ", text, "Owned skills follow from the completed nodes.");
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            var targetPrologue = new PrologueRun();
            var target = new CampaignRun();
            Assert.That(parsed.TryApply(targetPrologue, target, out error), Is.True, error);

            Assert.That(targetPrologue.ClearedCount, Is.EqualTo(2));
            Assert.That(Fingerprint(target, persistentOnly: true), Is.EqualTo(Fingerprint(source, persistentOnly: true)));
            Assert.That(target.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(target.HasLoadoutChanges, Is.False, "The saved loadout becomes the draft.");
            Assert.That(target.HighestUnlockedStage, Is.EqualTo(5));
            CollectionAssert.AreEqual(new[] { "horizontal-cut", "preparation", "advance" }, target.Curriculum.Completed);
            Assert.That(target.CurriculumPoints, Is.EqualTo(6));
            Assert.That(target.LastCurriculumPointGain.IsEmpty, Is.True, "A loaded game has no last battle.");
            Assert.That(target.RestoreDroppedCurriculum, Is.False, "A save of the current tree keeps its curriculum.");
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 14, 10, 12, 43, 44 },
                target.OwnedSkills.Where(owned => !target.IsCounterSkill(owned.SkillId) && !LegacySkillDefinitions.IsPrototype(owned.Skill)).Select(owned => owned.SkillId),
                "Owned skills follow completed nodes and first-clear stage rewards, regardless of grant order.");
            Assert.That(target.CanStartStage(3), Is.True);
            Assert.That(target.CreateDuel(1).GetLane(0)[0].Id, Is.EqualTo(14), "The saved loadout fights.");
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(targetPrologue, target)), Is.EqualTo(text),
                "Loading and saving again changes nothing.");

            // The restored balance keeps earning and buys the next node.
            Assert.That(target.TryStartStage(1), Is.True);
            Assert.That(target.TryCompleteBattle(DuelMatchOutcome.Draw), Is.True);
            Assert.That(target.CurriculumPoints, Is.EqualTo(7));
            Assert.That(target.ReturnToLobby(), Is.True);
            Assert.That(target.TryLearnCurriculumNode("quick-draw"), Is.True);
            Assert.That(target.CurriculumPoints, Is.EqualTo(6));
            Assert.That(target.OwnedSkills.Any(owned => owned.SkillId == 42), Is.True);
        }

        [TestCase(OpeningVoiceChoice.Leave)]
        [TestCase(OpeningVoiceChoice.Essential)]
        public void OpeningChoice_PersistsWithoutChangingOlderVersionTwoSaves(OpeningVoiceChoice choice)
        {
            var prologue = new PrologueRun();
            var campaign = new CampaignRun();
            string text = GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign, choice));
            StringAssert.Contains($"\nopening-choice {(int)choice}\n", text);
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            Assert.That(parsed.OpeningChoice, Is.EqualTo(choice));
            Assert.That(GameSaveCodec.Serialize(parsed), Is.EqualTo(text));

            string original = GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign));
            StringAssert.DoesNotContain("opening-choice", original);
            Assert.That(GameSaveCodec.TryParse(original, out GameSave oldSave, out error), Is.True, error);
            Assert.That(oldSave.OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Full));
        }

        [TestCase(OpeningVoiceChoice.Full)]
        [TestCase(OpeningVoiceChoice.Leave)]
        [TestCase(OpeningVoiceChoice.Essential)]
        public void UnfinishedOpening_RoundTrips_WhileOlderSavesRemainComplete(OpeningVoiceChoice choice)
        {
            var prologue = new PrologueRun();
            var campaign = new CampaignRun();
            string pending = GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign, choice, false));
            StringAssert.Contains("\nopening-pending 1\n", pending);
            Assert.That(GameSaveCodec.TryParse(pending, out GameSave parsed, out string error), Is.True, error);
            Assert.That(parsed.OpeningChoice, Is.EqualTo(choice));
            Assert.That(parsed.OpeningCompleted, Is.False);
            Assert.That(GameSaveCodec.Serialize(parsed), Is.EqualTo(pending));

            string older = GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign, choice));
            StringAssert.DoesNotContain("opening-pending", older);
            Assert.That(GameSaveCodec.TryParse(older, out GameSave completed, out error), Is.True, error);
            Assert.That(completed.OpeningCompleted, Is.True);
            Assert.That(GameSaveCodec.TryParse(pending.Replace("opening-pending 1", "opening-pending 0"),
                out _, out _), Is.False);
            Assert.That(GameSaveCodec.TryParse(pending.Replace("opening-pending 1", "opening-pending 1\nopening-pending 1"),
                out _, out _), Is.False);
        }

        [Test]
        public void FreshGame_SavesAndLoadsAsAFreshGame()
        {
            string text = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), new CampaignRun()));
            StringAssert.Contains("\ncurriculum-done\ncurriculum-points 0\n", text, "Empty curriculum lines are still written.");
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            var prologue = new PrologueRun();
            prologue.CompleteAll();
            CampaignRun campaign = PlayedCampaign();
            Assert.That(parsed.TryApply(prologue, campaign, out error), Is.True, error);
            Assert.That(prologue.ClearedCount, Is.Zero);
            Assert.That(Fingerprint(campaign), Is.EqualTo(Fingerprint(new CampaignRun())));
        }

        [Test]
        public void ExistingVersionTwoSave_RestoresTheSameSkillIdsAndLanesAfterStarterRedesign()
        {
            const string olderSave = "turn-limbo-save 2\nprologue 4\ncurrency 0\ncleared\n" +
                "curriculum-done\ncurriculum-active\n" +
                "lane 0 1 2 7\nlane 1 3 4 8\nlane 2 5 6 9\n";
            Assert.That(GameSaveCodec.TryParse(olderSave, out GameSave parsed, out string error), Is.True, error);
            var prologue = new PrologueRun();
            var campaign = new CampaignRun();
            Assert.That(parsed.TryApply(prologue, campaign, out error), Is.True, error);
            Assert.That(prologue.ClearedCount, Is.EqualTo(4));
            Assert.That(campaign.GetEquippedLane(0).Select(entry => entry.SkillId), Is.EqualTo(new[] { 1, 2, 7 }));
            Assert.That(campaign.GetEquippedLane(1).Select(entry => entry.SkillId), Is.EqualTo(new[] { 3, 4, 8 }));
            Assert.That(campaign.GetEquippedLane(2).Select(entry => entry.SkillId), Is.EqualTo(new[] { 5, 6, 9 }));
            Assert.That(campaign.OwnedSkills.Where(owned => !campaign.IsCounterSkill(owned.SkillId) && !LegacySkillDefinitions.IsPrototype(owned.Skill)).Select(entry => entry.SkillId),
                Is.EqualTo(Enumerable.Range(1, 9)));
            Assert.That(campaign.GetEquippedLane(0)[1].Skill.Name, Is.EqualTo("연속 베기"),
                "A pre-redesign ID now reads its current definition without changing the saved lane.");
            Assert.That(campaign.RestoreDroppedCurriculum, Is.False, "It had no curriculum progress to drop, so no notice.");
            Assert.That(campaign.CurriculumPoints, Is.EqualTo(4), "Credited retroactively: the four 서막 missions it won.");
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign)),
                Is.EqualTo(olderSave.Replace("cleared\n", $"cleared\ncurriculum-tree {CampaignCurriculum.TreeVersion}\n")
                    .Replace("curriculum-active\n", "curriculum-points 4\n")),
                "Saving again adds the tree version and the balance, and drops the old node-in-progress line.");
            Assert.That(campaign.TrainingVictoryCount, Is.Zero, "The optional field is absent in older saves.");
            Assert.That(campaign.OwnedSkills.All(entry => entry.Experience == 0), Is.True,
                "The optional experience records are absent in older saves.");
        }

        [Test]
        public void SkillExperience_RoundTripsWithoutChangingOlderVersionTwoSaves()
        {
            var source = new CampaignRun();
            CampaignOwnedSkill one = source.GetOwnedSkill(1);
            CampaignOwnedSkill two = source.GetOwnedSkill(3);
            LegacySkill enemy = LegacySkillDefinitions.Skill(5);
            for (int i = 0; i < 12; i++) Assert.That(source.TryGainClashExperience(one.Skill, enemy), Is.True);
            for (int i = 0; i < 5; i++) Assert.That(source.TryGainClashExperience(two.Skill, enemy), Is.True);

            string text = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), source));
            StringAssert.Contains("\nskill-xp 1 12\n", text);
            StringAssert.Contains("\nskill-xp 3 5\n", text);
            StringAssert.DoesNotContain("\nskill-xp 2 ", text);
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            var restored = new CampaignRun();
            Assert.That(restored.TryRestore(parsed.Campaign, out error), Is.True, error);
            Assert.That(restored.GetOwnedSkill(1).Experience, Is.EqualTo(12));
            Assert.That(restored.GetOwnedSkill(1).Level, Is.EqualTo(1));
            Assert.That(restored.GetOwnedSkill(1).Skill.MinPower,
                Is.EqualTo(LegacySkillDefinitions.Skill(1).MinPower + 2));
            Assert.That(restored.GetOwnedSkill(3).Experience, Is.EqualTo(5));
            Assert.That(restored.GetOwnedSkill(3).Level, Is.EqualTo(1));
            Assert.That(restored.GetOwnedSkill(2).Experience, Is.Zero);
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), restored)), Is.EqualTo(text));
        }

        [TestCase(0, 0)]
        [TestCase(3, 3)]
        [TestCase(9, 9)]
        [TestCase(10, 9)]
        [TestCase(30, 9)]
        public void PostureCostRebalance_LoadsHistoricExperience_AndSavesTheCurrentCap(int previousExperience,
            int expectedExperience)
        {
            CampaignSave basis = PlayedCampaign().CaptureSave();
            int counter = LegacySkillDefinitions.InitialCounterSkills[0].Id;
            var historic = new CampaignSave(basis.Currency, new[] { 1, 2, 3 }, basis.CurriculumCompleted,
                basis.CurriculumPoints, basis.Loadout, 2, new[]
                {
                    new KeyValuePair<int, int>(1, 4), new KeyValuePair<int, int>(45, previousExperience)
                }, new[] { "forest-pierce" }, CampaignCurriculum.TreeVersion, counter);
            string text = GameSaveCodec.Serialize(new GameSave(2, historic));
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            Assert.That(parsed.Validate(out error), Is.True, error);
            var restored = new CampaignRun();
            var prologue = new PrologueRun();
            Assert.That(parsed.TryApply(prologue, restored, out error), Is.True, error);
            CampaignOwnedSkill posture = restored.GetOwnedSkill(45);
            Assert.That(posture.Skill.Cost, Is.EqualTo(3));
            Assert.That(posture.Experience, Is.EqualTo(expectedExperience));
            Assert.That(posture.Level, Is.EqualTo(expectedExperience / 3));
            Assert.That(posture.Skill.MinPower, Is.EqualTo(LegacySkillDefinitions.Skill(45).MinPower + posture.Level * 2));
            Assert.That(restored.GetOwnedSkill(1).Experience, Is.EqualTo(4), "Other skill experience is unchanged.");
            CampaignSave normalized = restored.CaptureSave();
            Assert.That(normalized.Currency, Is.EqualTo(historic.Currency));
            Assert.That(normalized.CurriculumPoints, Is.EqualTo(historic.CurriculumPoints));
            Assert.That(normalized.TrainingVictoryCount, Is.EqualTo(historic.TrainingVictoryCount));
            Assert.That(normalized.CounterSkillId, Is.EqualTo(counter));
            CollectionAssert.AreEqual(historic.ClearedStages, normalized.ClearedStages);
            CollectionAssert.AreEqual(historic.CurriculumCompleted, normalized.CurriculumCompleted);
            CollectionAssert.AreEqual(historic.FieldPickups, normalized.FieldPickups);
            for (int lane = 0; lane < 3; lane++)
                CollectionAssert.AreEqual(historic.Loadout[lane], normalized.Loadout[lane]);

            string current = GameSaveCodec.Serialize(GameSave.Capture(prologue, restored));
            if (expectedExperience == 0) StringAssert.DoesNotContain("skill-xp 45 ", current);
            else StringAssert.Contains("\nskill-xp 45 " + expectedExperience + "\n", current);
            Assert.That(GameSaveCodec.TryParse(current, out GameSave again, out error), Is.True, error);
            var reloaded = new CampaignRun();
            Assert.That(again.TryApply(new PrologueRun(), reloaded, out error), Is.True, error);
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(prologue, reloaded)), Is.EqualTo(current),
                "Loading a migrated save again must not change its experience a second time.");
        }

        [TestCase(-1)]
        [TestCase(31)]
        public void PostureCostRebalance_StillRejectsExperienceOutsideItsHistoricBounds(int experience)
        {
            CampaignSave basis = PlayedCampaign().CaptureSave();
            var corrupt = new GameSave(2, With(basis, cleared: new[] { 1, 2, 3 },
                skillXp: new[] { new KeyValuePair<int, int>(45, experience) }));
            CampaignRun target = PlayedCampaign();
            string before = Fingerprint(target);
            var prologue = new PrologueRun();
            Assert.That(corrupt.TryApply(prologue, target, out string error), Is.False);
            StringAssert.Contains("기술 45의 경험치", error);
            Assert.That(Fingerprint(target), Is.EqualTo(before));
            Assert.That(prologue.ClearedCount, Is.Zero);
        }

        [Test]
        public void ParryCostRebalance_LoadsEveryHistoricExperienceValue_AndResavesIdempotently([Range(0, 30)] int previousExperience)
        {
            CampaignSave basis = PlayedCampaign().CaptureSave();
            int counter = LegacySkillDefinitions.InitialCounterSkills[0].Id;
            var historic = new CampaignSave(basis.Currency, new[] { 1, 2, 3 }, basis.CurriculumCompleted,
                basis.CurriculumPoints, basis.Loadout, 2, new[]
                {
                    new KeyValuePair<int, int>(1, 4), new KeyValuePair<int, int>(9, previousExperience),
                    new KeyValuePair<int, int>(45, 6)
                }, new[] { "forest-pierce" }, CampaignCurriculum.TreeVersion, counter);
            string text = GameSaveCodec.Serialize(new GameSave(2, historic));
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            Assert.That(parsed.Validate(out error), Is.True, error);
            var restored = new CampaignRun();
            var prologue = new PrologueRun();
            Assert.That(parsed.TryApply(prologue, restored, out error), Is.True, error);
            int expectedExperience = Math.Min(previousExperience, 15);
            CampaignOwnedSkill parry = restored.GetOwnedSkill(9);
            Assert.That(parry.Skill.Cost, Is.EqualTo(2));
            Assert.That(parry.Experience, Is.EqualTo(expectedExperience));
            Assert.That(parry.Level, Is.EqualTo(expectedExperience / 5));
            Assert.That(parry.Skill.MinPower, Is.EqualTo(LegacySkillDefinitions.Skill(9).MinPower + parry.Level * 2));
            Assert.That(restored.GetOwnedSkill(1).Experience, Is.EqualTo(4));
            Assert.That(restored.GetOwnedSkill(45).Experience, Is.EqualTo(6), "Other in-range experience stays intact.");
            Assert.That(prologue.ClearedCount, Is.EqualTo(2));
            CampaignSave normalized = restored.CaptureSave();
            Assert.That(normalized.Currency, Is.EqualTo(historic.Currency));
            Assert.That(normalized.CurriculumPoints, Is.EqualTo(historic.CurriculumPoints));
            Assert.That(normalized.TrainingVictoryCount, Is.EqualTo(historic.TrainingVictoryCount));
            Assert.That(normalized.CounterSkillId, Is.EqualTo(counter));
            CollectionAssert.AreEqual(historic.ClearedStages, normalized.ClearedStages);
            CollectionAssert.AreEqual(historic.CurriculumCompleted, normalized.CurriculumCompleted);
            CollectionAssert.AreEqual(historic.FieldPickups, normalized.FieldPickups);
            for (int lane = 0; lane < 3; lane++)
                CollectionAssert.AreEqual(historic.Loadout[lane], normalized.Loadout[lane]);

            string current = GameSaveCodec.Serialize(GameSave.Capture(prologue, restored));
            StringAssert.StartsWith("turn-limbo-save 2\n", current);
            if (expectedExperience == 0) StringAssert.DoesNotContain("skill-xp 9 ", current);
            else StringAssert.Contains("\nskill-xp 9 " + expectedExperience + "\n", current);
            Assert.That(GameSaveCodec.TryParse(current, out GameSave again, out error), Is.True, error);
            var reloaded = new CampaignRun();
            Assert.That(again.TryApply(new PrologueRun(), reloaded, out error), Is.True, error);
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(prologue, reloaded)), Is.EqualTo(current));
        }

        [TestCase(-1)]
        [TestCase(31)]
        [TestCase(int.MaxValue)]
        public void ParryCostRebalance_RejectsExperienceOutsideItsHistoricBounds_WithoutChangingProgress(int experience)
        {
            CampaignSave basis = PlayedCampaign().CaptureSave();
            var corrupt = new GameSave(2, With(basis,
                skillXp: new[] { new KeyValuePair<int, int>(9, experience) }));
            CampaignRun target = PlayedCampaign();
            string before = Fingerprint(target);
            var prologue = new PrologueRun();
            Assert.That(corrupt.TryApply(prologue, target, out string error), Is.False);
            StringAssert.Contains("기술 9의 경험치", error);
            Assert.That(Fingerprint(target), Is.EqualTo(before));
            Assert.That(prologue.ClearedCount, Is.Zero);
        }

        [Test]
        public void FieldPickups_RoundTripInVersionTwo_AndOlderSavesHaveNone()
        {
            var source = new CampaignRun();
            Assert.That(source.TryCollectFieldPickup("forest-finisher") && source.TryCollectFieldPickup("forest-pierce"), Is.True);
            Assert.That(source.TryPlaceLoadoutSkill(50, 2, 0) && source.TrySaveLoadout(), Is.True);
            Assert.That(source.TryGainClashExperience(source.GetOwnedSkill(50).Skill, LegacySkillDefinitions.Skill(1)), Is.True);

            string text = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), source));
            StringAssert.Contains("\ncurriculum-points 0\nfield-pickups forest-finisher forest-pierce\nlane 0 ", text,
                "The pickups taken, in order, after the curriculum lines.");
            StringAssert.Contains("\nlane 2 50 6 9\n", text);
            StringAssert.Contains("\nskill-xp 50 1\n", text);
            StringAssert.StartsWith("turn-limbo-save 2\n", text, "An optional line keeps version 2.");
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            CollectionAssert.AreEqual(new[] { "forest-finisher", "forest-pierce" }, parsed.Campaign.FieldPickups);
            var restored = new CampaignRun();
            Assert.That(parsed.TryApply(new PrologueRun(), restored, out error), Is.True, error);
            Assert.That(Fingerprint(restored, persistentOnly: true), Is.EqualTo(Fingerprint(source, persistentOnly: true)));
            CollectionAssert.AreEqual(new[] { "forest-finisher", "forest-pierce" }, restored.CollectedFieldPickups);
            Assert.That(restored.GetOwnedSkill(47), Is.Not.Null);
            Assert.That(restored.GetOwnedSkill(50).Experience, Is.EqualTo(1));
            Assert.That(restored.GetEquippedLane(2)[0].SkillId, Is.EqualTo(50));
            Assert.That(restored.CanCollectFieldPickup("forest-finisher"), Is.False, "A loaded pickup stays taken.");
            Assert.That(restored.CanCollectFieldPickup("forest-sidestep"), Is.True);
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), restored)), Is.EqualTo(text));

            // A save from before the field has no line: nothing taken, and it saves back unchanged.
            string older = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), PlayedCampaign()));
            StringAssert.DoesNotContain("field-pickups", older);
            Assert.That(GameSaveCodec.TryParse(older, out parsed, out error), Is.True, error);
            Assert.That(parsed.Campaign.FieldPickups, Is.Empty);
            var olderRun = new CampaignRun();
            Assert.That(parsed.TryApply(new PrologueRun(), olderRun, out error), Is.True, error);
            Assert.That(olderRun.CollectedFieldPickups, Is.Empty);
            Assert.That(olderRun.OwnedSkills.Any(owned => CampaignFieldPickups.FindGranting(owned.SkillId) != null), Is.False);
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), olderRun)), Is.EqualTo(older));

            // The format only reads ids; whether they name a pickup is a rule checked on apply. A bare key is none.
            Assert.That(GameSaveCodec.TryParse(older.Replace("curriculum-points", "field-pickups\ncurriculum-points"),
                out parsed, out error), Is.True, error);
            Assert.That(parsed.Campaign.FieldPickups, Is.Empty);
            Assert.That(GameSaveCodec.TryParse(older + "field-pickups no-such-pickup\n", out parsed, out error), Is.True, error);
            Assert.That(parsed.Validate(out error), Is.False);
            StringAssert.Contains("no-such-pickup", error);
        }

        [Test]
        public void TrainingVictories_RoundTripInVersionTwoWithoutChangingStageOrCurriculum()
        {
            CampaignRun source = PlayedCampaign();
            Assert.That(source.TryStartTraining(), Is.True);
            Assert.That(source.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            string text = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), source));
            StringAssert.Contains("\ntraining-wins 1\n", text);

            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            var restored = new CampaignRun();
            Assert.That(restored.TryRestore(parsed.Campaign, out error), Is.True, error);
            Assert.That(restored.TrainingVictoryCount, Is.EqualTo(1));
            Assert.That(restored.TrainingDummyHealth, Is.EqualTo(100));
            Assert.That(restored.HighestUnlockedStage, Is.EqualTo(5));
            Assert.That(restored.CurriculumPoints, Is.EqualTo(6), "A training fight earns no 수련 점수.");
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), restored)), Is.EqualTo(text));
        }

        [TestCase(new int[0], 2)]
        [TestCase(new[] { 1 }, 2)]
        [TestCase(new[] { 2 }, 2)]
        [TestCase(new[] { 1, 2 }, 5)]
        [TestCase(new[] { 1, 2, 5 }, 5)]
        [TestCase(new[] { 1, 2, 3, 4, 5 }, 8)]
        [TestCase(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 8)]
        public void Restore_DerivesUnlockedStagesFromClearsLikeFirstClearsDo(int[] cleared, int highest)
        {
            CampaignSave save = With(new CampaignRun().CaptureSave(), cleared: cleared);
            var run = new CampaignRun();
            Assert.That(run.TryRestore(save, out string error), Is.True, error);
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(highest));
            Assert.That(run.ClearedStageCount, Is.EqualTo(cleared.Length));
            foreach (int number in cleared) Assert.That(run.IsStageCleared(number), Is.True);
        }

        [Test]
        public void Apply_RejectsSavesThatBreakTheRules_AndChangesNeitherRun()
        {
            CampaignSave valid = PlayedCampaign().CaptureSave();
            List<string> done = valid.CurriculumCompleted.ToList();
            var lanes = valid.Loadout.Select(lane => lane.ToList()).ToList();
            var broken = new Dictionary<string, GameSave>
            {
                ["negative currency"] = Save(2, With(valid, currency: -1)),
                ["negative 수련 점수"] = Save(2, With(valid, points: -1)),
                ["negative training victories"] = Save(2, With(valid, trainingWins: -1)),
                ["training before stage three"] = Save(2, With(valid, cleared: new[] { 1 }, trainingWins: 1)),
                ["stage 0"] = Save(2, With(valid, cleared: new[] { 0 })),
                ["stage 9"] = Save(2, With(valid, cleared: new[] { 9 })),
                ["duplicate clear"] = Save(2, With(valid, cleared: new[] { 1, 1 })),
                ["unknown node"] = Save(2, With(valid, done: done.Append("no-such-node"))),
                ["node done twice"] = Save(2, With(valid, done: done.Append("horizontal-cut"))),
                ["node before its prerequisite"] = Save(2, With(valid, done: new[] { "horizontal-cut", "advance", "preparation" })),
                ["capstone before either twist"] = Save(2, With(valid, done: done.Concat(new[] { "quick-draw", "estocada" }))),
                ["bridge after one of its two twists"] = Save(2, With(valid,
                    done: done.Concat(new[] { "quick-draw", "esquive", "battement" }))),
                ["node of the removed one-stroke"] = Save(2, With(valid, done: done.Append("one-stroke"))),
                ["very negative 수련 점수"] = Save(2, new CampaignSave(valid.Currency, valid.ClearedStages,
                    valid.CurriculumCompleted, int.MinValue, valid.Loadout)),
                ["skill of an uncompleted node"] = Save(2, With(valid, loadout: SwapFirst(lanes, 0, 15))),
                ["skill of an affordable node not learned"] = Save(2, With(valid, loadout: SwapFirst(lanes, 0, 42))),
                ["stage 7 reward before stage 7"] = Save(2, With(valid, loadout: SwapFirst(lanes, 1, 16))),
                ["unknown skill"] = Save(2, With(valid, loadout: SwapFirst(lanes, 0, 9999))),
                ["two lanes"] = Save(2, With(valid, loadout: lanes.Take(2))),
                ["short lane"] = Save(2, With(valid, loadout: new[] { lanes[0].Take(2).ToList(), lanes[1], lanes[2] })),
                ["wrong lane"] = Save(2, With(valid, loadout: new[] { lanes[1], lanes[0], lanes[2] })),
                ["duplicate in loadout"] = Save(2, With(valid, loadout: new[]
                    { new List<int> { lanes[0][0], lanes[0][0], lanes[0][2] }, lanes[1], lanes[2] })),
                ["experience for unowned skill"] = Save(2, With(valid,
                    skillXp: new[] { new KeyValuePair<int, int>(16, 1) })),
                ["duplicate experience"] = Save(2, With(valid,
                    skillXp: new[] { new KeyValuePair<int, int>(1, 1), new KeyValuePair<int, int>(1, 2) })),
                ["negative experience"] = Save(2, With(valid,
                    skillXp: new[] { new KeyValuePair<int, int>(1, -1) })),
                ["experience past level three"] = Save(2, With(valid,
                    skillXp: new[] { new KeyValuePair<int, int>(1, 31) })),
                ["unknown field pickup"] = Save(2, With(valid, pickups: new[] { "no-such-pickup" })),
                ["field pickup taken twice"] = Save(2, With(valid, pickups: new[] { "forest-pierce", "forest-pierce" })),
                ["field skill not picked up"] = Save(2, With(valid, loadout: SwapFirst(lanes, 1, 47))),
                ["experience for a field skill not picked up"] = Save(2, With(valid,
                    skillXp: new[] { new KeyValuePair<int, int>(47, 1) })),
                ["arc below zero"] = Save(-1, valid),
                ["story past the end"] = Save(StoryMissions.Count + 1, valid),
                ["mission completed before the opening"] = new GameSave(1, valid, OpeningVoiceChoice.Leave, false),
            };
            foreach (KeyValuePair<string, GameSave> entry in broken)
            {
                var prologue = new PrologueRun();
                Assert.That(prologue.TryComplete(1, DuelMatchOutcome.PlayerVictory), Is.True);
                CampaignRun campaign = PlayedCampaign();
                Assert.That(campaign.TryUnequipSkill(campaign.GetEquippedLane(2)[0].SkillId), Is.True);
                string before = Fingerprint(campaign);
                Assert.That(entry.Value.TryApply(prologue, campaign, out string error), Is.False, entry.Key);
                Assert.That(error, Is.Not.Empty, entry.Key);
                Assert.That(Fingerprint(campaign), Is.EqualTo(before), entry.Key + " must not change the campaign.");
                Assert.That(campaign.HasLoadoutChanges, Is.True, entry.Key + " must keep the unsaved draft.");
                Assert.That(prologue.ClearedCount, Is.EqualTo(1), entry.Key + " must not change the arc.");
                Assert.That(entry.Value.Validate(out _), Is.False, entry.Key);
            }
            Assert.That(Save(2, valid).Validate(out string validError), Is.True, validError);
        }

        [Test]
        public void Codec_RejectsMalformedFiles()
        {
            string valid = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), new CampaignRun()));
            var malformed = new Dictionary<string, string>
            {
                ["null"] = null,
                ["empty"] = "",
                ["blank"] = "\n\n  \n",
                ["other file"] = "hello world\n",
                ["future version"] = valid.Replace("turn-limbo-save 2", "turn-limbo-save 3"),
                ["no version"] = valid.Replace("turn-limbo-save 2", "turn-limbo-save"),
                ["unknown key"] = valid + "gold 5\n",
                ["old skill key"] = valid + "skill 1 0\n",
                ["missing prologue"] = RemoveLine(valid, "prologue"),
                ["missing currency"] = RemoveLine(valid, "currency"),
                ["missing cleared"] = RemoveLine(valid, "cleared"),
                ["missing curriculum-done"] = RemoveLine(valid, "curriculum-done"),
                ["missing lane"] = RemoveLine(valid, "lane 2"),
                ["repeated currency"] = valid + "currency 5\n",
                ["repeated training wins"] = valid + "training-wins 1\ntraining-wins 2\n",
                ["training wins not a number"] = valid + "training-wins many\n",
                ["training wins pair"] = valid + "training-wins 1 2\n",
                ["repeated curriculum-done"] = valid + "curriculum-done horizontal-cut\n",
                ["repeated curriculum-active"] = valid + "curriculum-active\ncurriculum-active advance 0\n",
                ["repeated curriculum-points"] = valid + "curriculum-points 3\n",
                ["curriculum-points not a number"] = valid.Replace("curriculum-points 0", "curriculum-points many"),
                ["curriculum-points pair"] = valid.Replace("curriculum-points 0", "curriculum-points 1 2"),
                ["bare curriculum-points"] = valid.Replace("curriculum-points 0", "curriculum-points"),
                ["repeated lane"] = valid + "lane 0 1 2 3\n",
                ["lane out of range"] = valid + "lane 3 1 2 3\n",
                ["skill experience missing count"] = valid + "skill-xp 1\n",
                ["skill experience extra count"] = valid + "skill-xp 1 2 3\n",
                ["skill experience not a number"] = valid + "skill-xp 1 many\n",
                ["repeated field-pickups"] = valid + "field-pickups forest-pierce\nfield-pickups forest-finisher\n",
                ["repeated curriculum-tree"] = valid + "curriculum-tree 2\n",
                ["curriculum-tree zero"] = valid.Replace("curriculum-tree 2", "curriculum-tree 0"),
                ["curriculum-tree not a number"] = valid.Replace("curriculum-tree 2", "curriculum-tree two"),
                ["curriculum-tree pair"] = valid.Replace("curriculum-tree 2", "curriculum-tree 2 3"),
                ["bare curriculum-tree"] = valid.Replace("curriculum-tree 2", "curriculum-tree"),
                ["not a number"] = valid.Replace("currency 0", "currency many"),
                ["prologue pair"] = valid.Replace("prologue 0", "prologue 0 1"),
            };
            foreach (KeyValuePair<string, string> entry in malformed)
            {
                Assert.That(entry.Value, Is.Not.EqualTo(valid), entry.Key + " must change the file.");
                Assert.That(GameSaveCodec.TryParse(entry.Value, out GameSave save, out string error), Is.False, entry.Key);
                Assert.That(save, Is.Null, entry.Key);
                Assert.That(error, Is.Not.Empty, entry.Key);
            }
        }

        [Test]
        public void Codec_RejectsVersionOneSavesAndAsksForANewGame()
        {
            const string versionOne = "turn-limbo-save 1\nprologue 4\ncurrency 120\ncleared 1 2\nskill 1 0\n" +
                "lane 0 1 2 7\nlane 1 3 4 8\nlane 2 5 6 9\n";
            Assert.That(GameSave.CurrentVersion, Is.EqualTo(2));
            Assert.That(GameSaveCodec.TryParse(versionOne, out GameSave save, out string error), Is.False);
            Assert.That(save, Is.Null);
            StringAssert.Contains("이전 버전(1)", error);
            Assert.That(GameSaveCodec.TryParse(versionOne.Replace("save 1", "save 3"), out save, out error), Is.False);
            StringAssert.DoesNotContain("이전 버전", error, "A newer file is not called an old one.");
        }

        [Test]
        public void Codec_ReadsCurriculumLinesAndLeavesTheirRulesToApply()
        {
            string valid = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), new CampaignRun()));
            Assert.That(GameSaveCodec.TryParse(valid, out GameSave parsed, out string error), Is.True, error);
            Assert.That(parsed.Campaign.CurriculumCompleted, Is.Empty);
            Assert.That(parsed.Campaign.CurriculumPoints, Is.EqualTo(0), "A fresh game writes its balance too.");

            string progressed = valid.Replace("curriculum-done\n", "curriculum-done  horizontal-cut   preparation \n")
                .Replace("curriculum-points 0\n", "curriculum-points 3\n");
            Assert.That(GameSaveCodec.TryParse(progressed, out parsed, out error), Is.True, error);
            CollectionAssert.AreEqual(new[] { "horizontal-cut", "preparation" }, parsed.Campaign.CurriculumCompleted);
            Assert.That(parsed.Campaign.CurriculumPoints, Is.EqualTo(3));
            Assert.That(parsed.Validate(out error), Is.True, error);

            // An older file's node in progress is read once and ignored, whatever it names; without a points line the
            // balance is left for the restore to work out.
            foreach (string active in new[] { "curriculum-active", "curriculum-active advance 0", "curriculum-active no-such-node 7",
                         "curriculum-active advance many" })
            {
                string older = progressed.Replace("curriculum-points 3\n", active + "\n");
                Assert.That(GameSaveCodec.TryParse(older, out parsed, out error), Is.True, active + ": " + error);
                Assert.That(parsed.Campaign.CurriculumPoints, Is.Null, active);
                Assert.That(parsed.Validate(out error), Is.True, active + ": " + error);
            }

            // The format only reads ids and numbers; whether they fit the tree and the rules is checked on apply.
            foreach (string readable in new[]
            {
                valid.Replace("curriculum-points 0\n", "curriculum-points -1\n"),
                valid.Replace("curriculum-done\n", "curriculum-done no-such-node\n"),
                valid.Replace("curriculum-done\n", "curriculum-done diagonal-cut\n"),
            })
            {
                Assert.That(GameSaveCodec.TryParse(readable, out parsed, out error), Is.True, readable + error);
                Assert.That(parsed.Validate(out error), Is.False, readable);
                Assert.That(error, Is.Not.Empty, readable);
            }
        }

        /// <summary>A save from the 2026-10-13 build (the current tree, before 수련 점수): six story missions won, stages 1
        /// and 2 cleared, the trunk's three nodes completed, quick-draw in progress, no points line.</summary>
        private const string BeforePointsSave = "turn-limbo-save 2\nprologue 6\ncurrency 130\ncleared 1 2\ncurriculum-tree 2\n" +
            "curriculum-done horizontal-cut preparation advance\ncurriculum-active quick-draw 0\n" +
            "lane 0 14 2 7\nlane 1 3 4 8\nlane 2 5 6 9\n";

        [Test]
        public void SaveFromBeforePoints_DropsItsNodeInProgress_AndIsCreditedRetroactively()
        {
            Assert.That(GameSaveCodec.TryParse(BeforePointsSave, out GameSave parsed, out string error), Is.True, error);
            Assert.That(parsed.Campaign.CurriculumPoints, Is.Null);
            var prologue = new PrologueRun();
            var campaign = new CampaignRun();
            Assert.That(parsed.TryApply(prologue, campaign, out error), Is.True, error);
            CollectionAssert.AreEqual(new[] { "horizontal-cut", "preparation", "advance" }, campaign.Curriculum.Completed,
                "The completed nodes stay: they are valid in the same tree.");
            Assert.That(campaign.RestoreDroppedCurriculum, Is.False, "Same tree: nothing to tell.");
            Assert.That(campaign.Curriculum.IsCompleted("quick-draw"), Is.False, "The node in progress is dropped…");
            Assert.That(campaign.GetOwnedSkill(42), Is.Null, "…with nothing granted for its battles.");
            // 4 × 2 cleared stages + 1 × 6 missions won − (1 + 1 + 1) for the trunk = 11.
            Assert.That(campaign.CurriculumPoints, Is.EqualTo(11));
            Assert.That(campaign.GetEquippedLane(0)[0].SkillId, Is.EqualTo(14));

            // Once saved, the balance is written and never credited again.
            string saved = GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign));
            StringAssert.Contains("\ncurriculum-done horizontal-cut preparation advance\ncurriculum-points 11\n", saved);
            StringAssert.DoesNotContain("curriculum-active", saved);
            Assert.That(campaign.TryLearnCurriculumNode("quick-draw"), Is.True);
            saved = GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign));
            Assert.That(GameSaveCodec.TryParse(saved, out GameSave again, out error), Is.True, error);
            var reloaded = new CampaignRun();
            Assert.That(again.TryApply(new PrologueRun(), reloaded, out error), Is.True, error);
            Assert.That(reloaded.CurriculumPoints, Is.EqualTo(10));
        }

        [TestCase(0, "", "", 0)]
        [TestCase(4, "", "", 4)]
        [TestCase(9, "1 2 3 4 5 6 7 8", "", 41)]
        [TestCase(8, "1 2 3 4", "horizontal-cut preparation advance quick-draw fighting-spirit estocada", 15)]
        [TestCase(4, "1", "horizontal-cut preparation advance quick-draw fighting-spirit estocada", 0)]
        [TestCase(0, "", "horizontal-cut", 0)]
        public void RetroactiveBalance_IsFourPerClearedStageAndOnePerMissionLessTheKeptNodes_NeverBelowZero(
            int missions, string cleared, string done, int expected)
        {
            int[] stages = cleared.Length == 0 ? new int[0] : cleared.Split(' ').Select(int.Parse).ToArray();
            string[] nodes = done.Length == 0 ? new string[0] : done.Split(' ');
            var save = new CampaignSave(0, stages, nodes, null,
                new IEnumerable<int>[] { new[] { 1, 2, 7 }, new[] { 3, 4, 8 }, new[] { 5, 6, 9 } });
            var campaign = new CampaignRun();
            Assert.That(campaign.CurriculumPointsIn(save, missions), Is.EqualTo(expected), "What the title shows…");
            Assert.That(campaign.CurriculumPoints, Is.Zero, "…changes nothing.");
            Assert.That(campaign.TryRestore(save, missions, out string error), Is.True, error);
            Assert.That(campaign.CurriculumPoints, Is.EqualTo(expected), "…and loading gives the same.");
            Assert.That(campaign.Curriculum.CompletedCount, Is.EqualTo(nodes.Length), "The completed nodes stay even when clamped.");
            Assert.That(new CampaignRun().CurriculumPointsIn(With(save, points: 7), missions), Is.EqualTo(7),
                "A save with a points line keeps it.");
        }

        [TestCase(0, true)]
        [TestCase(15, true)]
        [TestCase(7, false)]
        public void FormerStageFourActStealSave_RemovesOnlyTheRetiredReward_AndRefillsItsExactSlot(int experience, bool equipped)
        {
            string text = FormerStageFourActStealSave(experience, equipped);
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            var prologue = new PrologueRun();
            var campaign = new CampaignRun();
            Assert.That(parsed.TryApply(prologue, campaign, out error), Is.True, error);
            Assert.That(prologue.ClearedCount, Is.EqualTo(4));
            Assert.That(campaign.Currency, Is.EqualTo(333));
            Assert.That(campaign.ClearedStageCount, Is.EqualTo(4));
            Assert.That(campaign.CurriculumPoints, Is.EqualTo(7));
            Assert.That(campaign.TrainingVictoryCount, Is.EqualTo(2));
            Assert.That(campaign.Curriculum.Completed, Is.EqualTo(new[] { "horizontal-cut" }));
            Assert.That(campaign.CollectedFieldPickups, Is.EqualTo(new[] { "forest-pierce" }));
            Assert.That(campaign.GetOwnedSkill(14).Experience, Is.EqualTo(4));
            Assert.That(campaign.GetOwnedSkill(47).Experience, Is.EqualTo(1));
            Assert.That(campaign.GetOwnedSkill(62), Is.Null);
            Assert.That(campaign.IsSkillEquipped(62), Is.False);
            Assert.That(campaign.GetEquippedLane(0).Select(skill => skill.SkillId), Is.EqualTo(new[] { 14, 2, 7 }));
            Assert.That(campaign.GetEquippedLane(1).Select(skill => skill.SkillId), Is.EqualTo(new[] { 47, 4, 8 }));
            Assert.That(campaign.GetEquippedLane(2).Select(skill => skill.SkillId), Is.EqualTo(new[] { 9, 6, 5 }),
                "Only 62's slot is replaced, with the lane's first unused starting technique.");
            Assert.That(campaign.RestoreDroppedCurriculum, Is.False);
            Assert.That(parsed.OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Leave));

            string normalized = GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign, parsed.OpeningChoice));
            Assert.That(normalized, Does.Not.Contain("skill-xp 62 "));
            Assert.That(normalized, Does.Not.Contain("lane 2 9 62 5"));
            Assert.That(GameSaveCodec.TryParse(normalized, out GameSave reread, out error), Is.True, error);
            var again = new CampaignRun();
            Assert.That(reread.TryApply(new PrologueRun(), again, out error), Is.True, error);
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(prologue, again, reread.OpeningChoice)), Is.EqualTo(normalized));
        }

        [TestCase("uncleared")]
        [TestCase("wrong lane")]
        [TestCase("duplicate equipped")]
        [TestCase("negative experience")]
        [TestCase("too much experience")]
        [TestCase("duplicate experience")]
        [TestCase("unrelated damage")]
        public void FormerStageFourActStealMigration_DoesNotMakeInvalidSavesValid(string problem)
        {
            string text = FormerStageFourActStealSave(15, true);
            switch (problem)
            {
                case "uncleared": text = text.Replace("cleared 1 2 3 4", "cleared 1 2 3"); break;
                case "wrong lane": text = text.Replace("lane 0 14 2 7", "lane 0 14 62 7")
                    .Replace("lane 2 9 62 5", "lane 2 9 6 5"); break;
                case "duplicate equipped": text = text.Replace("lane 2 9 62 5", "lane 2 62 62 5"); break;
                case "negative experience": text = text.Replace("skill-xp 62 15", "skill-xp 62 -1"); break;
                case "too much experience": text = text.Replace("skill-xp 62 15", "skill-xp 62 16"); break;
                case "duplicate experience": text += "skill-xp 62 1\n"; break;
                case "unrelated damage": text = text.Replace("lane 0 14 2 7", "lane 0 999 2 7"); break;
            }
            bool parsed = GameSaveCodec.TryParse(text, out GameSave save, out string error);
            Assert.That(!parsed || !save.TryApply(new PrologueRun(), new CampaignRun(), out error), Is.True);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        private static string FormerStageFourActStealSave(int experience, bool equipped)
            => "turn-limbo-save 2\nprologue 4\nopening-choice 1\ncurrency 333\ncleared 1 2 3 4\n" +
                $"curriculum-tree {CampaignCurriculum.TreeVersion}\ncurriculum-done horizontal-cut\ncurriculum-points 7\n" +
                "training-wins 2\nfield-pickups forest-pierce\nlane 0 14 2 7\nlane 1 47 4 8\n" +
                (equipped ? "lane 2 9 62 5\n" : "lane 2 9 6 5\n") +
                $"skill-xp 14 4\nskill-xp 47 1\nskill-xp 62 {experience}\n";

        [Test]
        public void Codec_AcceptsWindowsLineEndingsByteOrderMarkAndBlankLines()
        {
            string valid = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), PlayedCampaign()));
            string windows = "﻿\r\n" + valid.Replace("\n", "\r\n\r\n") + "   \r\n";
            Assert.That(GameSaveCodec.TryParse(windows, out GameSave parsed, out string error), Is.True, error);
            Assert.That(GameSaveCodec.Serialize(parsed), Is.EqualTo(valid));
            Assert.That(valid, Does.StartWith(GameSaveCodec.Header + " " + GameSave.CurrentVersion + "\n"));
        }

        /// <summary>A save from before the keyword curriculum (2026-10-13): no curriculum-tree line, so tree version 1.
        /// It completed the old roots advance and breathing and the old one-stroke, has vital-thrust in progress, equips the
        /// old curriculum's 호흡 (17) and 알티바호 (16), has cleared stage 7, took a field pickup and trained four skills.</summary>
        private const string OlderTreeSave = "turn-limbo-save 2\nprologue 4\ncurrency 480\ncleared 1 2 3 4 5 6 7\n" +
            "curriculum-done advance breathing horizontal-cut diagonal-cut one-stroke\ncurriculum-active vital-thrust 0\n" +
            "field-pickups forest-pierce\n" +
            "lane 0 1 17 7\nlane 1 16 47 8\nlane 2 5 12 9\n" +
            "skill-xp 1 4\nskill-xp 12 2\nskill-xp 16 3\nskill-xp 17 6\nskill-xp 47 1\n";

        [Test]
        public void SaveOfAnOlderTree_KeepsEverythingButItsCurriculum_AndSaysSoOnce()
        {
            Assert.That(GameSaveCodec.TryParse(OlderTreeSave, out GameSave parsed, out string error), Is.True, error);
            Assert.That(parsed.Campaign.CurriculumTreeVersion, Is.EqualTo(1), "No line reads as the tree before versions.");
            var prologue = new PrologueRun();
            var campaign = new CampaignRun();
            Assert.That(parsed.TryApply(prologue, campaign, out error), Is.True, error);
            Assert.That(campaign.RestoreDroppedCurriculum, Is.True, "Progress was dropped, so the field says so once.");

            Assert.That(campaign.Curriculum.Completed, Is.Empty);
            Assert.That(campaign.CurriculumPoints, Is.EqualTo(32),
                "No points line: 4 × 7 cleared stages + 4 missions, with no node left to pay for.");
            Assert.That(prologue.ClearedCount, Is.EqualTo(4), "The story is kept…");
            Assert.That(campaign.Currency, Is.EqualTo(480));
            Assert.That(campaign.ClearedStageCount, Is.EqualTo(7), "…and the stages…");
            CollectionAssert.AreEqual(new[] { "forest-pierce" }, campaign.CollectedFieldPickups, "…and the pickups.");
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 43, 44, 45, 46, 16, 47 },
                campaign.OwnedSkills.Where(owned => !campaign.IsCounterSkill(owned.SkillId) && !LegacySkillDefinitions.IsPrototype(owned.Skill)).Select(owned => owned.SkillId),
                "Stage 7 now grants 16; the old curriculum's skills are gone.");
            CollectionAssert.AreEqual(new[] { 1, 7, 2 }, campaign.GetEquippedLane(0).Select(owned => owned.SkillId),
                "호흡 leaves its lane and the lane's next starting skill fills the gap.");
            CollectionAssert.AreEqual(new[] { 16, 47, 8 }, campaign.GetEquippedLane(1).Select(owned => owned.SkillId),
                "Stage and field skills stay where they were.");
            CollectionAssert.AreEqual(new[] { 5, 9, 6 }, campaign.GetEquippedLane(2).Select(owned => owned.SkillId));
            Assert.That(campaign.GetOwnedSkill(1).Experience, Is.EqualTo(4));
            Assert.That(campaign.GetOwnedSkill(16).Experience, Is.EqualTo(3));
            Assert.That(campaign.GetOwnedSkill(47).Experience, Is.EqualTo(1));
            Assert.That(campaign.GetOwnedSkill(17), Is.Null, "Its experience went with it.");
            Assert.That(campaign.Curriculum.GetState("horizontal-cut"), Is.EqualTo(CurriculumNodeState.Available));

            // The next save records the current tree, so loading it again keeps the (new) curriculum and says nothing.
            Assert.That(campaign.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            string saved = GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign));
            StringAssert.Contains($"\ncurriculum-tree {CampaignCurriculum.TreeVersion}\n", saved);
            StringAssert.DoesNotContain("skill-xp 17 ", saved);
            Assert.That(GameSaveCodec.TryParse(saved, out GameSave again, out error), Is.True, error);
            var reloaded = new CampaignRun();
            Assert.That(again.TryApply(new PrologueRun(), reloaded, out error), Is.True, error);
            Assert.That(reloaded.RestoreDroppedCurriculum, Is.False);
            CollectionAssert.AreEqual(new[] { "horizontal-cut" }, reloaded.Curriculum.Completed);
            Assert.That(reloaded.CurriculumPoints, Is.EqualTo(31));
            reloaded.Reset();
            Assert.That(reloaded.RestoreDroppedCurriculum, Is.False);
        }

        [Test]
        public void SaveOfAnOlderTree_WithOnlyANodeInProgress_HasNothingToDrop()
        {
            string onlyActive = OlderTreeSave
                .Replace("curriculum-done advance breathing horizontal-cut diagonal-cut one-stroke\n", "curriculum-done\n")
                .Replace("lane 0 1 17 7\n", "lane 0 1 2 7\n").Replace("skill-xp 17 6\n", string.Empty);
            Assert.That(GameSaveCodec.TryParse(onlyActive, out GameSave parsed, out string error), Is.True, error);
            parsed.Campaign.ForCurriculumTree(CampaignCurriculum.TreeVersion, out bool dropped);
            Assert.That(dropped, Is.False, "The node in progress is ignored for every save now, so no notice.");
            var campaign = new CampaignRun();
            Assert.That(parsed.TryApply(new PrologueRun(), campaign, out error), Is.True, error);
            Assert.That(campaign.RestoreDroppedCurriculum, Is.False);
            Assert.That(campaign.CurriculumPoints, Is.EqualTo(32));
        }

        [Test]
        public void SaveOfAnotherTree_WithAPointsLine_GetsTheSpentPointsBack()
        {
            // A later tree version empties the curriculum; the balance becomes the larger of the saved one and the credit
            // with no node kept, so what the dropped nodes cost comes back.
            CampaignSave played = PlayedCampaign().CaptureSave();
            Assert.That(played.CurriculumPoints, Is.EqualTo(6));
            CampaignSave other = new CampaignSave(played.Currency, played.ClearedStages, played.CurriculumCompleted,
                played.CurriculumPoints, played.Loadout, curriculumTreeVersion: CampaignCurriculum.TreeVersion + 1);
            var campaign = new CampaignRun();
            Assert.That(campaign.TryRestore(other, 4, out string error), Is.True, error);
            Assert.That(campaign.RestoreDroppedCurriculum, Is.True);
            Assert.That(campaign.Curriculum.CompletedCount, Is.Zero);
            Assert.That(campaign.CurriculumPoints, Is.EqualTo(12), "4 × 2 stages + 4 missions beats the 6 left unspent.");
            Assert.That(new CampaignRun().CurriculumPointsIn(other, 0), Is.EqualTo(8));
            CampaignSave rich = With(other, points: 50);
            Assert.That(new CampaignRun().CurriculumPointsIn(rich, 4), Is.EqualTo(50), "A larger balance is kept.");
        }

        [Test]
        public void ForCurriculumTree_ReturnsTheSameSaveForItsVersion_AndAnEmptyCurriculumForAnother()
        {
            CampaignSave current = PlayedCampaign().CaptureSave();
            Assert.That(current.CurriculumTreeVersion, Is.EqualTo(CampaignCurriculum.TreeVersion));
            Assert.That(current.ForCurriculumTree(CampaignCurriculum.TreeVersion, out bool dropped), Is.SameAs(current));
            Assert.That(dropped, Is.False);

            CampaignSave other = current.ForCurriculumTree(CampaignCurriculum.TreeVersion + 1, out dropped);
            Assert.That(dropped, Is.True);
            Assert.That(other.CurriculumTreeVersion, Is.EqualTo(CampaignCurriculum.TreeVersion + 1));
            Assert.That(other.CurriculumCompleted, Is.Empty);
            Assert.That(other.CurriculumPoints, Is.EqualTo(current.CurriculumPoints), "The points line is copied as it was.");
            Assert.That((other.Currency, other.TrainingVictoryCount), Is.EqualTo((current.Currency, current.TrainingVictoryCount)));
            CollectionAssert.AreEqual(current.ClearedStages, other.ClearedStages);
            CollectionAssert.AreEqual(current.FieldPickups, other.FieldPickups);
            CollectionAssert.AreEqual(current.SkillExperience, other.SkillExperience);
            for (int lane = 0; lane < 3; lane++) CollectionAssert.AreEqual(current.Loadout[lane], other.Loadout[lane]);

            CampaignSave fresh = new CampaignRun().CaptureSave().ForCurriculumTree(1, out dropped);
            Assert.That(dropped, Is.False, "No progress, nothing to tell.");
            Assert.That(fresh.CurriculumTreeVersion, Is.EqualTo(1));
        }

        [Test]
        public void SaveOfTheCurrentTree_KeepsTheStrictCurriculumRules_WhileAnyOtherVersionIsReset()
        {
            string valid = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), new CampaignRun()));
            string broken = valid.Replace("curriculum-done\n", "curriculum-done advance\n");
            Assert.That(GameSaveCodec.TryParse(broken, out GameSave parsed, out string error), Is.True, error);
            Assert.That(parsed.Validate(out error), Is.False, "Real damage in a current save still shows.");
            StringAssert.Contains("advance", error);

            foreach (string version in new[] { "1", "3" })
            {
                string marked = broken.Replace($"curriculum-tree {CampaignCurriculum.TreeVersion}", "curriculum-tree " + version);
                Assert.That(GameSaveCodec.TryParse(marked, out parsed, out error), Is.True, error);
                var campaign = new CampaignRun();
                Assert.That(parsed.TryApply(new PrologueRun(), campaign, out error), Is.True, version + ": " + error);
                Assert.That(campaign.RestoreDroppedCurriculum, Is.True, version);
                Assert.That(campaign.Curriculum.CompletedCount, Is.Zero, version);
            }
        }

        [Test]
        public void TitleSummary_CountAndBalance_ReadTheSaveAsLoadingWill()
        {
            // DuelPrototypeController.DescribeSave counts through ForCurriculumTree and reads the balance through
            // CampaignRun.CurriculumPointsIn, so 이어하기 shows what loading gives (커리큘럼 c / 17  ·  수련 점수 n).
            foreach (string text in new[]
                     {
                         OlderTreeSave, BeforePointsSave,
                         GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), PlayedCampaign())),
                     })
            {
                Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
                int shown = parsed.Campaign.ForCurriculumTree(CampaignCurriculum.TreeVersion, out _).CurriculumCompleted.Count;
                var title = new CampaignRun();
                int points = title.CurriculumPointsIn(parsed.Campaign, parsed.PrologueCleared);
                var campaign = new CampaignRun();
                Assert.That(parsed.TryApply(new PrologueRun(), campaign, out error), Is.True, error);
                Assert.That(shown, Is.EqualTo(campaign.Curriculum.CompletedCount));
                Assert.That(points, Is.EqualTo(campaign.CurriculumPoints));
            }
        }

        [Test]
        public void PrologueRun_RestoresOnlyCountsInsideTheArc()
        {
            var run = new PrologueRun();
            Assert.That(run.TryRestore(3), Is.True);
            Assert.That(run.CurrentMission.Number, Is.EqualTo(4));
            Assert.That(run.TryRestore(PrologueMissions.Count), Is.True);
            Assert.That(run.IsArcComplete, Is.True, "Saves from before the lobby missions still load.");
            Assert.That(run.IsComplete, Is.False);
            Assert.That(run.TryRestore(StoryMissions.Count), Is.True);
            Assert.That(run.IsComplete, Is.True);
            Assert.That(run.TryRestore(-1), Is.False);
            Assert.That(run.TryRestore(StoryMissions.Count + 1), Is.False);
            Assert.That(run.IsComplete, Is.True, "A rejected count changes nothing.");
        }

        /// <summary>Two cleared stages and a lost battle (4 + 4 + 1 = 9 수련 점수); horizontal-cut, preparation and advance
        /// learned in that order (3 points, so 6 are left); the granted skill 14 placed in its lane and saved.</summary>
        private static CampaignRun PlayedCampaign()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryStartNextStage(), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.TryLearnCurriculumNode("preparation"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TryLearnCurriculumNode("advance"), Is.True);
            Assert.That(run.CurriculumPoints, Is.EqualTo(6));
            Assert.That(run.TryPlaceLoadoutSkill(14, 0, 0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            return run;
        }

        private static GameSave Save(int prologue, CampaignSave campaign) => new GameSave(prologue, campaign);

        private static CampaignSave With(CampaignSave basis, int? currency = null, IEnumerable<int> cleared = null,
            IEnumerable<string> done = null, int? points = null,
            IEnumerable<IEnumerable<int>> loadout = null, int? trainingWins = null,
            IEnumerable<KeyValuePair<int, int>> skillXp = null, IEnumerable<string> pickups = null)
            => new CampaignSave(currency ?? basis.Currency, cleared ?? basis.ClearedStages, done ?? basis.CurriculumCompleted,
                points ?? basis.CurriculumPoints, loadout ?? basis.Loadout,
                trainingWins ?? basis.TrainingVictoryCount, skillXp ?? basis.SkillExperience, pickups ?? basis.FieldPickups,
                basis.CurriculumTreeVersion);

        private static IEnumerable<IEnumerable<int>> SwapFirst(List<List<int>> lanes, int lane, int id)
            => lanes.Select((ids, index) => index == lane ? new[] { id }.Concat(ids.Skip(1)).ToList() : ids);

        private static string RemoveLine(string text, string prefix)
            => string.Join("\n", text.Split('\n').Where(line => !line.StartsWith(prefix + " ", StringComparison.Ordinal) && line != prefix));

        /// <summary>Everything a load may change. <paramref name="persistentOnly"/> leaves out what a save does not keep:
        /// the loadout draft and the note about what the last battle earned.</summary>
        private static string Fingerprint(CampaignRun run, bool persistentOnly = false)
        {
            var value = new StringBuilder();
            value.Append(run.Currency).Append('|').Append(run.HighestUnlockedStage).Append('|').Append(run.ClearedStageCount)
                .Append('|').Append(run.TrainingVictoryCount);
            for (int stage = 1; stage <= run.StageCount; stage++) value.Append(run.IsStageCleared(stage) ? 'c' : '-');
            value.Append(";c").Append(string.Join(",", run.Curriculum.Completed))
                .Append(";p").Append(run.CurriculumPoints)
                .Append(";f").Append(string.Join(",", run.CollectedFieldPickups));
            // The save stores completed nodes and cleared stages, not the battle order that interleaved their grants.
            foreach (CampaignOwnedSkill owned in run.OwnedSkills.OrderBy(entry => entry.SkillId))
                value.Append(";o").Append(owned.SkillId).Append(':').Append(owned.Experience);
            for (int lane = 0; lane < 3; lane++)
            {
                foreach (CampaignOwnedSkill owned in run.GetEquippedLane(lane)) value.Append(";e").Append(lane).Append(':').Append(owned.SkillId);
                if (persistentOnly) continue;
                for (int slot = 0; slot < 3; slot++)
                    value.Append(";d").Append(lane).Append(':').Append(run.GetLoadoutSlot(lane, slot)?.SkillId ?? 0);
            }
            if (!persistentOnly) value.Append(";l").Append(run.LastCurriculumPointGain).Append(run.CurriculumNewlyAffordable);
            return value.ToString();
        }
    }
}
