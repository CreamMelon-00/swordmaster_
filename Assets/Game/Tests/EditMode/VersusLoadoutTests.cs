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
    }
}
