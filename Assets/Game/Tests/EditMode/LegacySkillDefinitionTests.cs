using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacySkillDefinitionTests
    {
        [Test]
        public void Table_SplitsIntoStartingSkillsAndCurriculumSkills()
        {
            IReadOnlyList<LegacySkillDefinition> all = LegacySkillDefinitions.All;
            Assert.That(all.Select(d => d.Skill).ToArray(),
                Is.EqualTo(LegacyInitialSkills.All.Concat(CampaignSkillCatalog.AcquisitionSkills).ToArray()));
            // The curriculum may grow; the starting set stays at nine.
            Assert.That(LegacyInitialSkills.All.Count, Is.EqualTo(9));
            Assert.That(CampaignSkillCatalog.AcquisitionSkills.Count, Is.GreaterThan(0));
            Assert.That(all.Select(d => d.Skill.Id).Distinct().Count(), Is.EqualTo(all.Count));
        }

        [Test]
        public void StartingSkills_FillEachLaneWithThree()
        {
            for (int lane = 0; lane < 3; lane++)
                Assert.That(LegacyInitialSkills.All.Count(s => s.LaneIndex == lane), Is.EqualTo(3), "lane " + lane);
        }

        [Test]
        public void Find_UsesTheIdForCopiesAndIgnoresUnknownSkills()
        {
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                LegacySkill s = definition.Skill;
                var copy = new LegacySkill(s.Id, s.Name + "+1", s.Cost, s.MinPower + 3, s.MaxPower + 3, s.Kind,
                    s.Property, s.AttackCount, s.LaneIndex, s.Description, s.AnimationName, s.IconId);
                Assert.That(LegacySkillDefinitions.Find(s), Is.SameAs(definition));
                Assert.That(LegacySkillDefinitions.Find(copy), Is.SameAs(definition));
            }
            Assert.That(LegacySkillDefinitions.Find(null), Is.Null);
            Assert.That(LegacySkillDefinitions.Find(LegacyCommonActions.Breathe), Is.Null);
            Assert.That(LegacySkillDefinitions.Find(new LegacySkill(950, "unknown", 1, 4, 6,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "")), Is.Null);
        }

        [Test]
        public void EveryTableSkill_HasItsOwnLabelAndDetail()
        {
            // A table id keeps its label and detail even if a copy's shape ever changes.
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                Assert.That(definition.Text.ShortLabel, Is.Not.Null.And.Not.Empty, "label of " + definition.Skill.Id);
                Assert.That(definition.Text.Detail, Is.Not.Null.And.Not.Empty, "detail of " + definition.Skill.Id);
            }
        }

        [Test]
        public void Effects_UseOnlyCombinationsTheInterpreterDefines()
        {
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                LegacySkillEffect effect = definition.Effect;
                string id = "skill " + definition.Skill.Id;
                Assert.That(effect.OpponentProperty.HasValue && effect.OpponentKind.HasValue, Is.False, id);
                Assert.That(effect.HasOpponentCondition && effect.ResistanceRecoveryPercent > 0, Is.False, id);
                if (effect.HasOpponentCondition)
                    Assert.That(effect.ActGain > 0 || effect.OpponentResistanceReduction > 0, Is.True, id);
                Assert.That(effect.HasBuff, Is.EqualTo(effect.BuffPowerPercent != 0 || effect.BuffProtectionPercent != 0), id);
                if (definition.HighPower || definition.VariablePower)
                    Assert.That(definition.Skill.Kind, Is.EqualTo(LegacySkillKind.Attack), id);
            }
        }
    }
}
