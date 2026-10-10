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
            StringAssert.Contains("\ncurriculum-done breathing horizontal-cut diagonal-cut\n", text, "Completion order is kept.");
            StringAssert.Contains("\ncurriculum-active advance 0\n", text);
            StringAssert.DoesNotContain("\nskill ", text, "Owned skills follow from the completed nodes.");
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            var targetPrologue = new PrologueRun();
            var target = new CampaignRun();
            Assert.That(parsed.TryApply(targetPrologue, target, out error), Is.True, error);

            Assert.That(targetPrologue.ClearedCount, Is.EqualTo(2));
            Assert.That(Fingerprint(target, persistentOnly: true), Is.EqualTo(Fingerprint(source, persistentOnly: true)));
            Assert.That(target.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(target.HasLoadoutChanges, Is.False, "The saved loadout becomes the draft.");
            Assert.That(target.HighestUnlockedStage, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { "breathing", "horizontal-cut", "diagonal-cut" }, target.Curriculum.Completed);
            Assert.That(target.Curriculum.Active.Id, Is.EqualTo("advance"));
            Assert.That(target.Curriculum.ActiveBattles, Is.Zero);
            Assert.That(target.LastCompletedCurriculumNode, Is.Null, "A loaded game has no last battle.");
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 17, 14, 15, 43, 44 },
                target.OwnedSkills.Select(owned => owned.SkillId),
                "Owned skills follow completed nodes and first-clear stage rewards, regardless of grant order.");
            Assert.That(target.CanStartStage(3), Is.True);
            Assert.That(target.CreateDuel(1).GetLane(0)[0].Id, Is.EqualTo(14), "The saved loadout fights.");
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(targetPrologue, target)), Is.EqualTo(text),
                "Loading and saving again changes nothing.");

            // The restored node in progress keeps counting battles.
            Assert.That(target.TryStartStage(1), Is.True);
            Assert.That(target.TryCompleteBattle(DuelMatchOutcome.Draw), Is.True);
            Assert.That(target.Curriculum.IsCompleted("advance"), Is.True);
            Assert.That(target.OwnedSkills.Any(owned => owned.SkillId == 12), Is.True);
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
            StringAssert.Contains("\ncurriculum-done\ncurriculum-active\n", text, "Empty curriculum lines are still written.");
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
            Assert.That(campaign.OwnedSkills.Select(entry => entry.SkillId), Is.EqualTo(Enumerable.Range(1, 9)));
            Assert.That(campaign.GetEquippedLane(0)[1].Skill.Name, Is.EqualTo("연속 베기"),
                "A pre-redesign ID now reads its current definition without changing the saved lane.");
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(prologue, campaign)), Is.EqualTo(olderSave));
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
            Assert.That(restored.HighestUnlockedStage, Is.EqualTo(3));
            Assert.That(restored.Curriculum.ActiveBattles, Is.Zero);
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), restored)), Is.EqualTo(text));
        }

        [TestCase(new int[0], 1)]
        [TestCase(new[] { 1 }, 2)]
        [TestCase(new[] { 1, 2 }, 3)]
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
                ["negative training victories"] = Save(2, With(valid, trainingWins: -1)),
                ["training before stage three"] = Save(2, With(valid, cleared: new[] { 1 }, trainingWins: 1)),
                ["stage 0"] = Save(2, With(valid, cleared: new[] { 0 })),
                ["stage 9"] = Save(2, With(valid, cleared: new[] { 9 })),
                ["duplicate clear"] = Save(2, With(valid, cleared: new[] { 1, 1 })),
                ["unknown node"] = Save(2, With(valid, done: done.Append("no-such-node"))),
                ["node done twice"] = Save(2, With(valid, done: done.Append("breathing"))),
                ["node before its prerequisite"] = Save(2, With(valid, done: new[] { "breathing", "diagonal-cut", "horizontal-cut" })),
                ["node before either opener"] = Save(2, With(valid, done: new[] { "breathing", "horizontal-cut", "preparation", "diagonal-cut" })),
                ["both exclusive nodes done"] = Save(2, With(valid, done: done.Concat(new[] { "one-stroke", "quick-draw" }))),
                ["unknown node in progress"] = Save(2, With(valid, active: "no-such-node")),
                ["locked node in progress"] = Save(2, With(valid, active: "vital-thrust")),
                ["excluded node in progress"] = Save(2, With(valid, done: done.Append("one-stroke"), active: "quick-draw")),
                ["completed node in progress"] = Save(2, With(valid, active: "breathing")),
                ["negative battles"] = Save(2, With(valid, battles: -1)),
                ["battles past the node"] = Save(2, With(valid, battles: 1)),
                ["battles without a node"] = Save(2, new CampaignSave(valid.Currency, valid.ClearedStages,
                    valid.CurriculumCompleted, null, 1, valid.Loadout)),
                ["skill of an uncompleted node"] = Save(2, With(valid, done: new[] { "breathing" })),
                ["skill of the node in progress"] = Save(2, With(valid, loadout: SwapFirst(lanes, 2, 12))),
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
                ["missing curriculum-active"] = RemoveLine(valid, "curriculum-active"),
                ["missing lane"] = RemoveLine(valid, "lane 2"),
                ["repeated currency"] = valid + "currency 5\n",
                ["repeated training wins"] = valid + "training-wins 1\ntraining-wins 2\n",
                ["training wins not a number"] = valid + "training-wins many\n",
                ["training wins pair"] = valid + "training-wins 1 2\n",
                ["repeated curriculum-done"] = valid + "curriculum-done horizontal-cut\n",
                ["repeated curriculum-active"] = valid + "curriculum-active\n",
                ["node without battles"] = valid.Replace("curriculum-active\n", "curriculum-active advance\n"),
                ["node with two counts"] = valid.Replace("curriculum-active\n", "curriculum-active advance 0 1\n"),
                ["battles not a number"] = valid.Replace("curriculum-active\n", "curriculum-active advance many\n"),
                ["repeated lane"] = valid + "lane 0 1 2 3\n",
                ["lane out of range"] = valid + "lane 3 1 2 3\n",
                ["skill experience missing count"] = valid + "skill-xp 1\n",
                ["skill experience extra count"] = valid + "skill-xp 1 2 3\n",
                ["skill experience not a number"] = valid + "skill-xp 1 many\n",
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
            Assert.That(parsed.Campaign.CurriculumActive, Is.Null, "An empty curriculum-active line means no node in progress.");
            Assert.That(parsed.Campaign.CurriculumBattles, Is.Zero);

            string progressed = valid.Replace("curriculum-done\n", "curriculum-done  advance   breathing \n")
                .Replace("curriculum-active\n", "curriculum-active vital-thrust 0\n");
            Assert.That(GameSaveCodec.TryParse(progressed, out parsed, out error), Is.True, error);
            CollectionAssert.AreEqual(new[] { "advance", "breathing" }, parsed.Campaign.CurriculumCompleted);
            Assert.That(parsed.Campaign.CurriculumActive, Is.EqualTo("vital-thrust"));
            Assert.That(parsed.Campaign.CurriculumBattles, Is.Zero);
            Assert.That(parsed.Validate(out error), Is.True, error);

            // The format only reads ids and counts; whether they fit the tree is a rule checked on apply.
            foreach (string readable in new[]
            {
                valid.Replace("curriculum-active\n", "curriculum-active advance 7\n"),
                valid.Replace("curriculum-active\n", "curriculum-active advance -1\n"),
                valid.Replace("curriculum-done\n", "curriculum-done no-such-node\n"),
                valid.Replace("curriculum-done\n", "curriculum-done diagonal-cut\n"),
            })
            {
                Assert.That(GameSaveCodec.TryParse(readable, out parsed, out error), Is.True, readable + error);
                Assert.That(parsed.Validate(out error), Is.False, readable);
                Assert.That(error, Is.Not.Empty, readable);
            }
        }

        [Test]
        public void Codec_AcceptsWindowsLineEndingsByteOrderMarkAndBlankLines()
        {
            string valid = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), PlayedCampaign()));
            string windows = "﻿\r\n" + valid.Replace("\n", "\r\n\r\n") + "   \r\n";
            Assert.That(GameSaveCodec.TryParse(windows, out GameSave parsed, out string error), Is.True, error);
            Assert.That(GameSaveCodec.Serialize(parsed), Is.EqualTo(valid));
            Assert.That(valid, Does.StartWith(GameSaveCodec.Header + " " + GameSave.CurrentVersion + "\n"));
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

        /// <summary>Two cleared stages; breathing, horizontal-cut and diagonal-cut completed in that order (the last one
        /// with a lost battle); the granted skill 14 placed in its lane and saved; advance in progress.</summary>
        private static CampaignRun PlayedCampaign()
        {
            var run = new CampaignRun();
            Assert.That(run.TrySelectCurriculumNode("breathing"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryStartNextStage(), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.TrySelectCurriculumNode("diagonal-cut"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TrySelectCurriculumNode("advance"), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(14, 0, 0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            return run;
        }

        private static GameSave Save(int prologue, CampaignSave campaign) => new GameSave(prologue, campaign);

        private static CampaignSave With(CampaignSave basis, int? currency = null, IEnumerable<int> cleared = null,
            IEnumerable<string> done = null, string active = null, int? battles = null,
            IEnumerable<IEnumerable<int>> loadout = null, int? trainingWins = null,
            IEnumerable<KeyValuePair<int, int>> skillXp = null)
            => new CampaignSave(currency ?? basis.Currency, cleared ?? basis.ClearedStages, done ?? basis.CurriculumCompleted,
                active ?? basis.CurriculumActive, battles ?? basis.CurriculumBattles, loadout ?? basis.Loadout,
                trainingWins ?? basis.TrainingVictoryCount, skillXp ?? basis.SkillExperience);

        private static IEnumerable<IEnumerable<int>> SwapFirst(List<List<int>> lanes, int lane, int id)
            => lanes.Select((ids, index) => index == lane ? new[] { id }.Concat(ids.Skip(1)).ToList() : ids);

        private static string RemoveLine(string text, string prefix)
            => string.Join("\n", text.Split('\n').Where(line => !line.StartsWith(prefix + " ", StringComparison.Ordinal) && line != prefix));

        /// <summary>Everything a load may change. <paramref name="persistentOnly"/> leaves out what a save does not keep:
        /// the loadout draft and the note about the last battle's curriculum completion.</summary>
        private static string Fingerprint(CampaignRun run, bool persistentOnly = false)
        {
            var value = new StringBuilder();
            value.Append(run.Currency).Append('|').Append(run.HighestUnlockedStage).Append('|').Append(run.ClearedStageCount)
                .Append('|').Append(run.TrainingVictoryCount);
            for (int stage = 1; stage <= run.StageCount; stage++) value.Append(run.IsStageCleared(stage) ? 'c' : '-');
            value.Append(";c").Append(string.Join(",", run.Curriculum.Completed))
                .Append(";a").Append(run.Curriculum.Active?.Id ?? "-").Append(':').Append(run.Curriculum.ActiveBattles);
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
            if (!persistentOnly) value.Append(";l").Append(run.LastCompletedCurriculumNode?.Id ?? "-");
            return value.ToString();
        }
    }
}
