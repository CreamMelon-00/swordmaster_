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
        public void RoundTrip_RestoresArcStagesShopUpgradesAndTheSavedLoadoutOnly()
        {
            CampaignRun source = PlayedCampaign(out int acquiredId);
            var prologue = new PrologueRun();
            Assert.That(prologue.TryComplete(1, DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(prologue.TryComplete(2, DuelMatchOutcome.PlayerVictory), Is.True);
            // An unsaved draft edit is not progress and must not reach the file.
            Assert.That(source.TryUnequipSkill(source.GetEquippedLane(1)[0].SkillId), Is.True);
            Assert.That(source.HasLoadoutChanges, Is.True);

            string text = GameSaveCodec.Serialize(GameSave.Capture(prologue, source));
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            var targetPrologue = new PrologueRun();
            var target = new CampaignRun();
            Assert.That(parsed.TryApply(targetPrologue, target, out error), Is.True, error);

            Assert.That(targetPrologue.ClearedCount, Is.EqualTo(2));
            Assert.That(Fingerprint(target, savedLoadoutOnly: true), Is.EqualTo(Fingerprint(source, savedLoadoutOnly: true)));
            Assert.That(target.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(target.HasLoadoutChanges, Is.False, "The saved loadout becomes the draft.");
            Assert.That(target.HighestUnlockedStage, Is.EqualTo(3));
            Assert.That(target.Offers.Any(offer => offer.SkillId == acquiredId), Is.False, "Owned skills leave the shop.");
            CampaignOwnedSkill upgraded = target.OwnedSkills.First(owned => owned.SkillId == LegacyInitialSkills.All[0].Id);
            Assert.That(upgraded.Level, Is.EqualTo(1));
            Assert.That(upgraded.Skill.MinPower, Is.EqualTo(LegacyInitialSkills.All[0].MinPower + 2), "Upgrades are rebuilt.");
            Assert.That(target.CanStartStage(3), Is.True);
            int acquiredLane = CampaignSkillCatalog.AcquisitionSkills.First(skill => skill.Id == acquiredId).LaneIndex;
            Assert.That(target.CreateDuel(1).GetLane(acquiredLane)[0].Id, Is.EqualTo(acquiredId), "The saved loadout fights.");
            Assert.That(GameSaveCodec.Serialize(GameSave.Capture(targetPrologue, target)), Is.EqualTo(text),
                "Loading and saving again changes nothing.");
        }

        [Test]
        public void FreshGame_SavesAndLoadsAsAFreshGame()
        {
            string text = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), new CampaignRun()));
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string error), Is.True, error);
            var prologue = new PrologueRun();
            prologue.CompleteAll();
            CampaignRun campaign = PlayedCampaign(out _);
            Assert.That(parsed.TryApply(prologue, campaign, out error), Is.True, error);
            Assert.That(prologue.ClearedCount, Is.Zero);
            Assert.That(Fingerprint(campaign), Is.EqualTo(Fingerprint(new CampaignRun())));
        }

        [TestCase(new int[0], 1)]
        [TestCase(new[] { 1 }, 2)]
        [TestCase(new[] { 1, 2 }, 3)]
        [TestCase(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 8)]
        public void Restore_DerivesUnlockedStagesFromClearsLikeFirstClearsDo(int[] cleared, int highest)
        {
            CampaignSave fresh = new CampaignRun().CaptureSave();
            var save = new CampaignSave(0, cleared, fresh.OwnedSkills, fresh.Loadout);
            var run = new CampaignRun();
            Assert.That(run.TryRestore(save, out string error), Is.True, error);
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(highest));
            Assert.That(run.ClearedStageCount, Is.EqualTo(cleared.Length));
            foreach (int number in cleared) Assert.That(run.IsStageCleared(number), Is.True);
        }

        [Test]
        public void Apply_RejectsSavesThatBreakTheRules_AndChangesNeitherRun()
        {
            CampaignSave valid = PlayedCampaign(out int acquiredId).CaptureSave();
            int unownedShopSkill = CampaignSkillCatalog.AcquisitionSkills.First(skill => skill.Id != acquiredId).Id;
            int initialFirst = LegacyInitialSkills.All[0].Id;
            var owned = valid.OwnedSkills.ToList();
            var lanes = valid.Loadout.Select(lane => lane.ToList()).ToList();
            var broken = new Dictionary<string, GameSave>
            {
                ["negative currency"] = Save(2, With(valid, currency: -1)),
                ["stage 0"] = Save(2, With(valid, cleared: new[] { 0 })),
                ["stage 9"] = Save(2, With(valid, cleared: new[] { 9 })),
                ["duplicate clear"] = Save(2, With(valid, cleared: new[] { 1, 1 })),
                ["unknown skill"] = Save(2, With(valid, owned: owned.Append(new SavedSkill(9999, 0)))),
                ["duplicate skill"] = Save(2, With(valid, owned: owned.Append(new SavedSkill(initialFirst, 0)))),
                ["level too high"] = Save(2, With(valid, owned: Replace(owned, initialFirst, CampaignOwnedSkill.MaximumLevel + 1))),
                ["negative level"] = Save(2, With(valid, owned: Replace(owned, initialFirst, -1))),
                ["missing starting skill"] = Save(2, With(valid, owned: owned.Where(skill => skill.Id != initialFirst))),
                ["two lanes"] = Save(2, With(valid, loadout: lanes.Take(2))),
                ["short lane"] = Save(2, With(valid, loadout: new[] { lanes[0].Take(2).ToList(), lanes[1], lanes[2] })),
                ["unowned in loadout"] = Save(2, With(valid, loadout: SwapFirst(lanes, LaneOf(unownedShopSkill), unownedShopSkill))),
                ["wrong lane"] = Save(2, With(valid, loadout: new[] { lanes[1], lanes[0], lanes[2] })),
                ["duplicate in loadout"] = Save(2, With(valid, loadout: new[]
                    { new List<int> { lanes[0][0], lanes[0][0], lanes[0][2] }, lanes[1], lanes[2] })),
                ["arc below zero"] = Save(-1, valid),
                ["arc past the end"] = Save(PrologueMissions.Count + 1, valid),
            };
            foreach (KeyValuePair<string, GameSave> entry in broken)
            {
                var prologue = new PrologueRun();
                Assert.That(prologue.TryComplete(1, DuelMatchOutcome.PlayerVictory), Is.True);
                CampaignRun campaign = PlayedCampaign(out _);
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
                ["future version"] = valid.Replace("turn-limbo-save 1", "turn-limbo-save 2"),
                ["no version"] = valid.Replace("turn-limbo-save 1", "turn-limbo-save"),
                ["unknown key"] = valid + "gold 5\n",
                ["missing prologue"] = RemoveLine(valid, "prologue"),
                ["missing currency"] = RemoveLine(valid, "currency"),
                ["missing cleared"] = RemoveLine(valid, "cleared"),
                ["missing lane"] = RemoveLine(valid, "lane 2"),
                ["repeated currency"] = valid + "currency 5\n",
                ["repeated lane"] = valid + "lane 0 1 2 3\n",
                ["lane out of range"] = valid + "lane 3 1 2 3\n",
                ["not a number"] = valid.Replace("currency 0", "currency many"),
                ["prologue pair"] = valid.Replace("prologue 0", "prologue 0 1"),
                ["short skill"] = valid + "skill 5\n",
            };
            foreach (KeyValuePair<string, string> entry in malformed)
            {
                Assert.That(GameSaveCodec.TryParse(entry.Value, out GameSave save, out string error), Is.False, entry.Key);
                Assert.That(save, Is.Null, entry.Key);
                Assert.That(error, Is.Not.Empty, entry.Key);
            }
        }

        [Test]
        public void Codec_AcceptsWindowsLineEndingsByteOrderMarkAndBlankLines()
        {
            string valid = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), PlayedCampaign(out _)));
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
            Assert.That(run.IsComplete, Is.True);
            Assert.That(run.TryRestore(-1), Is.False);
            Assert.That(run.TryRestore(PrologueMissions.Count + 1), Is.False);
            Assert.That(run.IsComplete, Is.True, "A rejected count changes nothing.");
        }

        /// <summary>Two cleared stages, one bought skill placed in its lane and saved, one upgrade.</summary>
        private static CampaignRun PlayedCampaign(out int acquiredId)
        {
            var run = new CampaignRun();
            for (int stage = 1; stage <= 2; stage++)
            {
                Assert.That(run.TryStartStage(stage), Is.True);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            }
            Assert.That(run.ReturnToLobby(), Is.True);
            CampaignSkillOffer offer = run.Offers.OrderBy(candidate => candidate.Price).First();
            Assert.That(run.TryAcquireSkill(offer.SkillId), Is.True);
            acquiredId = offer.SkillId;
            Assert.That(run.TryUpgradeSkill(LegacyInitialSkills.All[0].Id), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(acquiredId, offer.Skill.LaneIndex, 0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            return run;
        }

        private static GameSave Save(int prologue, CampaignSave campaign) => new GameSave(prologue, campaign);

        private static CampaignSave With(CampaignSave basis, int? currency = null, IEnumerable<int> cleared = null,
            IEnumerable<SavedSkill> owned = null, IEnumerable<IEnumerable<int>> loadout = null)
            => new CampaignSave(currency ?? basis.Currency, cleared ?? basis.ClearedStages, owned ?? basis.OwnedSkills,
                loadout ?? basis.Loadout);

        private static IEnumerable<SavedSkill> Replace(IEnumerable<SavedSkill> owned, int id, int level)
            => owned.Select(skill => skill.Id == id ? new SavedSkill(id, level) : skill);

        private static int LaneOf(int skillId)
            => CampaignSkillCatalog.AcquisitionSkills.First(skill => skill.Id == skillId).LaneIndex;

        private static IEnumerable<IEnumerable<int>> SwapFirst(List<List<int>> lanes, int lane, int id)
            => lanes.Select((ids, index) => index == lane ? new[] { id }.Concat(ids.Skip(1)).ToList() : ids);

        private static string RemoveLine(string text, string prefix)
            => string.Join("\n", text.Split('\n').Where(line => !line.StartsWith(prefix + " ", StringComparison.Ordinal) && line != prefix));

        private static string Fingerprint(CampaignRun run, bool savedLoadoutOnly = false)
        {
            var value = new StringBuilder();
            value.Append(run.Currency).Append('|').Append(run.HighestUnlockedStage).Append('|').Append(run.ClearedStageCount);
            for (int stage = 1; stage <= run.StageCount; stage++) value.Append(run.IsStageCleared(stage) ? 'c' : '-');
            foreach (CampaignOwnedSkill owned in run.OwnedSkills)
                value.Append(";o").Append(owned.SkillId).Append(':').Append(owned.Level).Append(':').Append(owned.Skill.MinPower);
            foreach (CampaignSkillOffer offer in run.Offers) value.Append(";s").Append(offer.SkillId);
            for (int lane = 0; lane < 3; lane++)
            {
                foreach (CampaignOwnedSkill owned in run.GetEquippedLane(lane)) value.Append(";e").Append(lane).Append(':').Append(owned.SkillId);
                if (savedLoadoutOnly) continue;
                for (int slot = 0; slot < 3; slot++)
                    value.Append(";d").Append(lane).Append(':').Append(run.GetLoadoutSlot(lane, slot)?.SkillId ?? 0);
            }
            return value.ToString();
        }
    }
}
