using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The rules behind the 서막's 수훈 phase presentation: which of the empowered enemy's techniques open with a
    /// cut-in (the enemy-only rows of her empowered script, not names), which shake the camera on every hit (the
    /// several-hit one, 라우다레), the cut-in camera's ease toward her and back, and shakes that decay instead of adding up.</summary>
    public sealed class EmpowermentCueTests
    {
        private const float Tolerance = 1e-4f;

        private static MissionEmpowerment Empowerment => PrologueMissions.Get(4).Empowerment;

        [Test]
        public void SignatureSkills_AreTheEnemyOnlyRowsOfTheEmpoweredScript()
        {
            LegacySkill[] motto = Empowerment.EnemyScript.AllSkills.ToArray();
            Assert.That(motto.Select(skill => skill.Id), Is.EqualTo(new[] { 500, 501, 502 }), "라우다레, 베네디체레, 프레디카레.");
            foreach (LegacySkill skill in motto)
            {
                Assert.That(LegacySkillDefinitions.EnemySkills.Any(row => row.Id == skill.Id), Is.True, skill.Name);
                Assert.That(Empowerment.IsSignatureSkill(skill), Is.True, skill.Name + " opens with a cut-in.");
                // The same technique as the sheet hands it out again (the script reads the sheet when asked).
                Assert.That(Empowerment.IsSignatureSkill(LegacySkillDefinitions.Skill(skill.Id)), Is.True);
            }
        }

        [Test]
        public void OtherSkills_HaveNoCutIn()
        {
            Assert.That(Empowerment.IsSignatureSkill(null), Is.False);
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialSkills.Concat(LegacySkillDefinitions.AcquisitionSkills))
                Assert.That(Empowerment.IsSignatureSkill(skill), Is.False, skill.Name + " is not an enemy-only row.");
            Assert.That(Empowerment.IsSignatureSkill(LegacyCommonActions.Breathe), Is.False);
            // 이아 before her 수훈: what the mission gives her to begin with is never a signature technique.
            Assert.That(PrologueMissions.Get(4).EnemySkills, Is.Not.Empty);
            foreach (LegacySkill skill in PrologueMissions.Get(4).EnemySkills)
                Assert.That(Empowerment.IsSignatureSkill(skill), Is.False, skill.Name);
            Assert.That(PrologueMissions.All.Where(mission => mission.Number != 4).All(mission => mission.Empowerment == null), Is.True,
                "Only the 서막's last mission has a 수훈 phase.");
        }

        [Test]
        public void OnlyTheSeveralHitSignatureAttack_ShakesOnEachHit()
        {
            LegacySkill[] motto = Empowerment.EnemyScript.AllSkills.ToArray();
            LegacySkill laudare = motto.Single(skill => skill.Id == 500);
            Assert.That(laudare.Kind, Is.EqualTo(LegacySkillKind.Attack));
            Assert.That(laudare.AttackCount, Is.EqualTo(5));
            Assert.That(Empowerment.IsSignatureBarrage(laudare), Is.True, "라우다레's five hits each shake the camera.");
            Assert.That(Empowerment.IsSignatureBarrage(motto.Single(skill => skill.Id == 501)), Is.False, "베네디체레 is a guard.");
            Assert.That(Empowerment.IsSignatureBarrage(motto.Single(skill => skill.Id == 502)), Is.False, "프레디카레 is one blow.");
            Assert.That(Empowerment.IsSignatureBarrage(null), Is.False);
            LegacySkill flurry = LegacySkillDefinitions.InitialSkills.Concat(LegacySkillDefinitions.AcquisitionSkills)
                .First(skill => skill.Kind == LegacySkillKind.Attack && skill.AttackCount > 1);
            Assert.That(Empowerment.IsSignatureBarrage(flurry), Is.False, "A player's flurry never shakes this way.");
        }

        [Test]
        public void TheCutInCamera_EasesTowardHerAndBack_WithinItsHold()
        {
            const float hold = .6f;
            Assert.That(LegacyCutIn.CameraAmount(0f, hold), Is.Zero, "It starts from the battle's framing…");
            Assert.That(LegacyCutIn.CameraAmount(hold, hold), Is.Zero, "…and is back by the first hit.");
            Assert.That(LegacyCutIn.CameraAmount(hold * .5f, hold), Is.EqualTo(1f), "All the way in at the middle.");
            Assert.That(LegacyCutIn.CameraAmount(hold * LegacyCutIn.EaseShare, hold), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(LegacyCutIn.CameraAmount(hold * (1f - LegacyCutIn.EaseShare), hold), Is.EqualTo(1f).Within(Tolerance));
            float previous = 0f;
            for (int step = 1; step <= 10; step++)
            {
                float amount = LegacyCutIn.CameraAmount(hold * LegacyCutIn.EaseShare * step / 10f, hold);
                Assert.That(amount, Is.GreaterThanOrEqualTo(previous), "It only eases in on the way in.");
                Assert.That(LegacyCutIn.CameraAmount(hold - hold * LegacyCutIn.EaseShare * step / 10f, hold),
                    Is.EqualTo(amount).Within(Tolerance), "The way back mirrors the way in.");
                previous = amount;
            }
            Assert.That(LegacyCutIn.CameraAmount(-1f, hold) + LegacyCutIn.CameraAmount(hold + 1f, hold), Is.Zero);
            Assert.That(LegacyCutIn.CameraAmount(.3f, 0f) + LegacyCutIn.CameraAmount(.3f, float.NaN) +
                LegacyCutIn.CameraAmount(float.NaN, hold), Is.Zero, "No hold, no cut-in.");
        }

        [Test]
        public void AShake_DiesAwayOverItsSeconds()
        {
            var shake = new LegacyShake();
            Assert.That(shake.IsShaking, Is.False);
            shake.Add(.12f, .25f);
            Assert.That(shake.Amplitude, Is.EqualTo(.12f).Within(Tolerance));
            shake.Advance(.125f);
            Assert.That(shake.Amplitude, Is.EqualTo(.03f).Within(Tolerance), "Quadratically: a quarter at half its time.");
            shake.Advance(.125f);
            Assert.That(shake.Amplitude, Is.Zero);
            Assert.That(shake.IsShaking, Is.False);

            shake.Add(0f, 1f);
            shake.Add(.1f, 0f);
            shake.Add(float.NaN, 1f);
            shake.Add(.1f, float.PositiveInfinity);
            Assert.That(shake.IsShaking, Is.False, "Nothing to shake with.");
            shake.Add(.1f, 1f);
            shake.Advance(float.NaN);
            shake.Advance(-1f);
            Assert.That(shake.Amplitude, Is.EqualTo(.1f).Within(Tolerance));
            shake.Clear();
            Assert.That(shake.IsShaking, Is.False);
        }

        [Test]
        public void StackedShakes_NeverAddUp_AndSettleAfterTheLastHit()
        {
            var shake = new LegacyShake();
            // 라우다레's five hits in quick succession.
            for (int hit = 0; hit < 5; hit++)
            {
                shake.Add(.12f, .25f);
                Assert.That(shake.Amplitude, Is.LessThanOrEqualTo(.12f + Tolerance), "However many hits land, one hit's strength at most.");
                shake.Advance(.08f);
            }
            Assert.That(shake.IsShaking, Is.True);
            shake.Advance(.25f);
            Assert.That(shake.IsShaking, Is.False, "It settles a shake's length after the last hit.");

            shake.Add(.2f, 1f);
            shake.Add(.05f, 1f);
            Assert.That(shake.Amplitude, Is.EqualTo(.2f).Within(Tolerance), "A weaker shake leaves a stronger one alone…");
            shake.Advance(.5f);
            Assert.That(shake.Amplitude, Is.EqualTo(.05f).Within(Tolerance));
            shake.Add(.06f, .5f);
            Assert.That(shake.Amplitude, Is.EqualTo(.06f).Within(Tolerance), "…and takes over once the old one has died below it.");
        }
    }
}
