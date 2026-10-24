using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class AttackRhythmTests
    {
        private const float Tolerance = .00001f;

        [Test]
        public void Attacks_HoldTheirGatherAndFinish_AndAccelerateIntoTheUnchangedContact()
        {
            float prepared = LegacyAttackRhythm.PosePhase(.16f, 1f);
            float held = LegacyAttackRhythm.PosePhase(.40f, 1f);
            Assert.That(held - prepared, Is.InRange(0f, .01f), "The gathered cel stays readable before release.");
            Assert.That(held, Is.LessThan(.40f));
            Assert.That(.5f - held, Is.GreaterThan(.5f - .40f), "The held preparation releases into the same contact time.");
            float finish = LegacyAttackRhythm.PosePhase(.70f, 1f);
            Assert.That(finish, Is.GreaterThan(.90f));
            Assert.That(LegacyAttackRhythm.PosePhase(.88f, 1f) - finish, Is.InRange(0f, .02f),
                "The extended finish remains readable for the tail of the same attack clip.");
        }

        [TestCase(LegacySkillProperty.Slash)]
        [TestCase(LegacySkillProperty.Penetrate)]
        [TestCase(LegacySkillProperty.Hit)]
        public void EveryShippedAttack_UsesTheSameRhythm_WithoutPerSkillConfiguration(LegacySkillProperty property)
        {
            int count = 0;
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                LegacySkill skill = definition.Skill;
                if (skill.Kind != LegacySkillKind.Attack || skill.Property != property) continue;
                count++;
                foreach (float strength in new[] { 0f, .4f, 1f })
                for (int index = 0; index <= 100; index++)
                {
                    float phase = index / 100f;
                    Assert.That(LegacyAttackRhythm.PosePhase(skill, phase, strength),
                        Is.EqualTo(LegacyAttackRhythm.PosePhase(phase, strength)), skill.Name);
                    Assert.That(LegacyAttackRhythm.LungeShare(skill, phase),
                        Is.EqualTo(LegacyAttackRhythm.LungeShare(phase)), skill.Name);
                }
                Assert.That(LegacyAttackRhythm.TrailStartPhase(skill, 1f), Is.EqualTo(.48f), skill.Name);
                Assert.That(LegacyAttackRhythm.TrailStartPhase(skill, 0f), Is.EqualTo(.44f), skill.Name);
            }
            Assert.That(count, Is.GreaterThan(0), "The shipped sheet must exercise this property.");
        }

        [Test]
        public void DefenceWaitAndEmptySlots_DoNotReceiveAttackPosesOrDisplacement()
        {
            foreach (LegacySkill skill in new[] { null, LegacyCommonActions.Breathe,
                new LegacySkill(3, "수비 검증", 0, 1, 1, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 1, "검증") })
            for (int index = 0; index <= 100; index++)
            {
                float phase = index / 100f;
                Assert.That(LegacyAttackRhythm.PosePhase(skill, phase, 1f), Is.EqualTo(phase));
                Assert.That(LegacyAttackRhythm.LungeShare(skill, phase), Is.Zero);
                Assert.That(LegacyAttackRhythm.TrailStartPhase(skill, 1f), Is.EqualTo(.44f));
            }
        }

        [Test]
        public void NewAndNegativeIdAttacks_GetTheCommonProfile_WithoutAddingToARegistry()
        {
            foreach (int id in new[] { 3, -10001, 999999 })
            foreach (LegacySkillProperty property in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
            {
                var attack = new LegacySkill(id, "공통 동작 검증", 0, 1, 1, LegacySkillKind.Attack, property, 3, 1, string.Empty);
                Assert.That(LegacyAttackRhythm.PosePhase(attack, .40f, 1f), Is.LessThan(.30f));
                Assert.That(LegacyAttackRhythm.PosePhase(attack, .88f, 1f), Is.GreaterThan(.90f));
                Assert.That(LegacyAttackRhythm.PosePhase(attack, .40f, 0f), Is.EqualTo(.40f));
                Assert.That(LegacyAttackRhythm.PosePhase(attack, .5f, 1f), Is.EqualTo(.5f));
                Assert.That(LegacyAttackRhythm.PosePhase(attack, 4f, 1f), Is.EqualTo(1f));
                Assert.That(LegacyAttackRhythm.LungeShare(attack, .5f), Is.EqualTo(1f));
                Assert.That(LegacyAttackRhythm.LungeShare(attack, 1f), Is.Zero);
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(.12f, .28f)]
        [TestCase(.42f, .29f)]
        [TestCase(.5f, .5f)]
        [TestCase(.54f, .53f)]
        [TestCase(.66f, .90f)]
        [TestCase(.90f, .92f)]
        [TestCase(1f, 1f)]
        public void FullStrength_ReachesTheAuthoredAnchors(float phase, float pose)
            => Assert.That(LegacyAttackRhythm.PosePhase(phase, LegacyAttackRhythm.DefaultStrength),
                Is.EqualTo(pose).Within(Tolerance));

        [TestCase(0f)]
        [TestCase(.4f)]
        [TestCase(1f)]
        public void Sampling_IsBoundedMonotonic_AndCannotCrossContactEarly(float strength)
        {
            float previous = 0f;
            for (int index = 0; index <= 1000; index++)
            {
                float phase = index / 1000f;
                float pose = LegacyAttackRhythm.PosePhase(phase, strength);
                Assert.That(pose, Is.InRange(0f, 1f));
                Assert.That(pose, Is.GreaterThanOrEqualTo(previous));
                if (phase < .5f) Assert.That(pose, Is.LessThan(.5f));
                else if (phase > .5f) Assert.That(pose, Is.GreaterThan(.5f));
                previous = pose;
            }
            Assert.That(LegacyAttackRhythm.PosePhase(0f, strength), Is.EqualTo(0f));
            Assert.That(LegacyAttackRhythm.PosePhase(.5f, strength), Is.EqualTo(.5f));
            Assert.That(LegacyAttackRhythm.PosePhase(1f, strength), Is.EqualTo(1f));
        }

        [Test]
        public void SegmentJunctions_AreContinuous()
        {
            const float epsilon = .00001f;
            foreach (float strength in new[] { .4f, 1f })
            foreach (float phase in new[] { .12f, .42f, .5f, .54f, .66f, .90f })
            {
                float before = LegacyAttackRhythm.PosePhase(phase - epsilon, strength);
                float at = LegacyAttackRhythm.PosePhase(phase, strength);
                float after = LegacyAttackRhythm.PosePhase(phase + epsilon, strength);
                Assert.That(at - before, Is.InRange(0f, .00004f));
                Assert.That(after - at, Is.InRange(0f, .00004f));
            }
        }

        [Test]
        public void Rhythm_RaisesEarly_GathersBeforeContact_AndFollowsThroughQuickly()
        {
            float raised = LegacyAttackRhythm.PosePhase(.16f, 1f);
            float gathered = LegacyAttackRhythm.PosePhase(.38f, 1f);
            Assert.That(raised, Is.GreaterThan(.16f), "The weapon reaches its preparation pose early.");
            Assert.That(gathered, Is.LessThan(.38f), "The gathered pose waits before the stroke.");
            Assert.That(gathered - raised, Is.LessThan(.38f - .16f), "The gather moves more slowly than the clock.");
            Assert.That(.5f - gathered, Is.GreaterThan(.5f - .38f), "The last stroke catches up into exact contact.");
            Assert.That(LegacyAttackRhythm.PosePhase(.54f, 1f), Is.LessThan(.54f), "Contact stays visible briefly.");
            float through = LegacyAttackRhythm.PosePhase(.74f, 1f);
            Assert.That(through, Is.GreaterThan(.74f), "The blade follows through ahead of the clock.");
            Assert.That(1f - through, Is.LessThan(1f - .74f), "The final pose settles for the remaining time.");
        }

        [Test]
        public void StrengthZero_IsLinear_AndPartialStrengthBlendsTheSameRhythm()
        {
            for (int index = 0; index <= 1000; index++)
            {
                float phase = index / 1000f;
                Assert.That(LegacyAttackRhythm.PosePhase(phase, 0f), Is.EqualTo(phase));
            }
            Assert.That(LegacyAttackRhythm.PosePhase(.42f, .5f), Is.EqualTo(.355f).Within(Tolerance));
            Assert.That(LegacyAttackRhythm.PosePhase(.66f, .5f), Is.EqualTo(.78f).Within(Tolerance));
            Assert.That(LegacyAttackRhythm.PosePhase(.38f, -10f), Is.EqualTo(.38f));
            Assert.That(LegacyAttackRhythm.PosePhase(.38f, 10f),
                Is.EqualTo(LegacyAttackRhythm.PosePhase(.38f, 1f)));
        }

        [Test]
        public void InvalidInputs_UseFiniteDefaults_AndPhasesClampAtTheEndpoints()
        {
            foreach (float strength in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
                Assert.That(LegacyAttackRhythm.PosePhase(.74f, strength),
                    Is.EqualTo(LegacyAttackRhythm.PosePhase(.74f, LegacyAttackRhythm.DefaultStrength)));
            foreach (float phase in new[] { float.NaN, float.NegativeInfinity, -10f })
                Assert.That(LegacyAttackRhythm.PosePhase(phase, 1f), Is.EqualTo(0f));
            foreach (float phase in new[] { float.PositiveInfinity, 10f })
                Assert.That(LegacyAttackRhythm.PosePhase(phase, 1f), Is.EqualTo(1f));
        }

        [TestCase(0f, 0f)]
        [TestCase(.12f, -.3f)]
        [TestCase(.42f, -.3f)]
        [TestCase(.46f, .35f)]
        [TestCase(.5f, 1f)]
        [TestCase(.66f, 1f)]
        [TestCase(.78f, .15f)]
        [TestCase(.92f, .12f)]
        [TestCase(1f, 0f)]
        public void Lunge_ReachesTheAuthoredPullbackContactAndRecoveryAnchors(float phase, float share)
            => Assert.That(LegacyAttackRhythm.LungeShare(phase), Is.EqualTo(share).Within(Tolerance));

        [Test]
        public void Lunge_PullsBack_StrikesQuickly_HoldsContact_AndRecoversToNeutral()
        {
            Assert.That(LegacyAttackRhythm.DefaultLungeDistance, Is.EqualTo(.6f));
            Assert.That(LegacyAttackRhythm.LungeShare(.08f), Is.LessThan(0f));
            Assert.That(LegacyAttackRhythm.LungeShare(.40f), Is.EqualTo(LegacyAttackRhythm.LungeShare(.12f)), "The drawn body waits with the gathered weapon.");
            Assert.That(LegacyAttackRhythm.LungeShare(.46f), Is.GreaterThan(0f));
            Assert.That(LegacyAttackRhythm.LungeShare(.49f) - LegacyAttackRhythm.LungeShare(.42f),
                Is.GreaterThan(1f), "The last windup rapidly turns the pullback into a visible forward strike.");
            Assert.That(LegacyAttackRhythm.LungeShare(.5f), Is.EqualTo(1f));
            Assert.That(LegacyAttackRhythm.LungeShare(.54f), Is.EqualTo(1f));
            Assert.That(LegacyAttackRhythm.LungeShare(.66f), Is.EqualTo(1f));
            Assert.That(LegacyAttackRhythm.LungeShare(.78f), Is.LessThan(.4f));
            Assert.That(LegacyAttackRhythm.LungeShare(.9f), Is.InRange(0f, .2f));
            Assert.That(LegacyAttackRhythm.LungeShare(1f), Is.Zero);
        }

        [Test]
        public void Lunge_SegmentsAreContinuous_AndAllSamplesRemainWithinTheVisibleRange()
        {
            const float epsilon = .00001f;
            foreach (float phase in new[] { .12f, .42f, .5f, .66f, .78f, .92f })
            {
                float at = LegacyAttackRhythm.LungeShare(phase);
                Assert.That(LegacyAttackRhythm.LungeShare(phase - epsilon), Is.EqualTo(at).Within(.0002f));
                Assert.That(LegacyAttackRhythm.LungeShare(phase + epsilon), Is.EqualTo(at).Within(.0002f));
            }
            for (int index = 0; index <= 1000; index++)
                Assert.That(LegacyAttackRhythm.LungeShare(index / 1000f), Is.InRange(-.3f, 1f));
        }

        [Test]
        public void Lunge_InvalidAndOutOfRangePhasesReturnNeutralAtTheClampedEndpoints()
        {
            foreach (float phase in new[] { float.NaN, float.NegativeInfinity, -10f, float.PositiveInfinity, 10f })
                Assert.That(LegacyAttackRhythm.LungeShare(phase), Is.Zero);
        }
    }
}
