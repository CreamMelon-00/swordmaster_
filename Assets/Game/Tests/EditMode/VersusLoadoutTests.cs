using System;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class VersusLoadoutTests
    {
        [Test]
        public void DefaultRoster_HasThreeOrderedPlayerSkillsPerLane()
        {
            var loadout = new VersusLoadout();
            int[] ids = loadout.ExportIds();
            Assert.That(ids, Has.Length.EqualTo(VersusLoadout.SkillCount));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(VersusLoadout.SkillCount));
            for (int lane = 0; lane < VersusLoadout.LaneCount; lane++)
                for (int slot = 0; slot < VersusLoadout.SlotsPerLane; slot++)
                    Assert.That(loadout.GetSkill(lane, slot).LaneIndex, Is.EqualTo(lane));

            ids[0] = -100;
            Assert.That(loadout.GetSkillId(0, 0), Is.Not.EqualTo(-100),
                "A network snapshot must not alias the mutable draft.");
        }

        [Test]
        public void SelectingAnAcquiredSkillReplacesOneSlot_AndExistingSkillsCanSwapOrder()
        {
            var loadout = new VersusLoadout();
            LegacySkill acquired = LegacySkillDefinitions.AcquisitionSkills
                .First(skill => skill.LaneIndex == 0);
            int oldFirst = loadout.GetSkillId(0, 0);
            int oldThird = loadout.GetSkillId(0, 2);
            Assert.That(loadout.TryPlaceSkill(acquired.Id, 0, 0), Is.True);
            Assert.That(loadout.GetSkillId(0, 0), Is.EqualTo(acquired.Id));
            Assert.That(loadout.IsEquipped(oldFirst), Is.False);
            Assert.That(loadout.ToSkills()[0].Id, Is.EqualTo(acquired.Id));

            Assert.That(loadout.TryPlaceSkill(acquired.Id, 0, 2), Is.True);
            Assert.That(loadout.GetSkillId(0, 2), Is.EqualTo(acquired.Id));
            Assert.That(loadout.GetSkillId(0, 0), Is.EqualTo(oldThird));
            Assert.That(loadout.ToSkills()[2].Id, Is.EqualTo(acquired.Id));
        }

        [Test]
        public void ImportedRoster_RejectsWrongLaneDuplicateEnemyAndUnknownIdsAtomically()
        {
            var loadout = new VersusLoadout();
            int[] original = loadout.ExportIds();
            int[] invalid = (int[])original.Clone();

            invalid[0] = original[3];
            Assert.That(loadout.TryImportIds(invalid), Is.False, "A W skill cannot occupy Q.");
            invalid[0] = original[1];
            Assert.That(loadout.TryImportIds(invalid), Is.False, "A skill cannot appear twice.");
            invalid[0] = LegacySkillDefinitions.EnemySkills[0].Id;
            Assert.That(loadout.TryImportIds(invalid), Is.False, "Enemy skills are not player techniques.");
            invalid[0] = int.MaxValue;
            Assert.That(loadout.TryImportIds(invalid), Is.False);
            Assert.That(loadout.TryImportIds(new[] { original[0] }), Is.False);
            Assert.That(loadout.ExportIds(), Is.EqualTo(original));

            LegacySkill acquired = LegacySkillDefinitions.AcquisitionSkills.First(skill => skill.LaneIndex == 1);
            int[] valid = (int[])original.Clone();
            valid[3] = acquired.Id;
            Assert.That(loadout.TryImportIds(valid), Is.True);
            Assert.That(loadout.ToSkills()[3].Id, Is.EqualTo(acquired.Id));
            Assert.That(loadout.ExportIds(), Is.EqualTo(valid));
        }

        [Test]
        public void BreakthroughAndPursuit_AreAvailableInTheFullPool_AndSurviveImportedLoadouts()
        {
            var loadout = new VersusLoadout();
            Assert.That(VersusLoadout.IsExcluded(60) || VersusLoadout.IsExcluded(61), Is.False);
            Assert.That(loadout.TryPlaceSkill(60, 1, 0), Is.True);
            Assert.That(loadout.TryPlaceSkill(61, 2, 0), Is.True);
            int[] ids = loadout.ExportIds();
            var restored = new VersusLoadout();
            Assert.That(restored.TryImportIds(ids), Is.True);
            Assert.That(restored.GetSkillId(1, 0), Is.EqualTo(60));
            Assert.That(restored.GetSkillId(2, 0), Is.EqualTo(61));
            Assert.That(restored.ToSkills().Where(skill => skill.Id == 60 || skill.Id == 61).Select(skill => skill.Id),
                Is.EqualTo(new[] { 60, 61 }));
        }

        [Test]
        public void Esquive_StaysOutOfTheVersusPool_WhileTheCampaignKeepsIt()
        {
            // 에스키브 dodges a whole slot by itself; in versus the later reserver of a slot sees the other's technique there.
            Assert.That(VersusLoadout.ExcludedSkillIds, Is.EqualTo(new[] { 54 }));
            Assert.That(VersusLoadout.IsExcluded(54), Is.True);
            Assert.That(VersusLoadout.IsExcluded(53), Is.False);
            Assert.That(LegacySkillDefinitions.AcquisitionSkills.Any(skill => skill.Id == 54), Is.True, "It is still a 획득 row.");
            var loadout = new VersusLoadout();
            int[] original = loadout.ExportIds();
            Assert.That(loadout.TryPlaceSkill(54, 2, 0), Is.False);
            int[] withEsquive = (int[])original.Clone();
            withEsquive[6] = 54;
            Assert.That(loadout.TryImportIds(withEsquive), Is.False, "A peer cannot send it either.");
            Assert.That(loadout.ExportIds(), Is.EqualTo(original));
            Assert.That(loadout.TryPlaceSkill(55, 2, 0), Is.True, "The other new E techniques are in the pool.");
            Assert.That(loadout.TryPlaceSkill(53, 2, 1), Is.True);
        }
    }
}
