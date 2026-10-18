using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacySkillRoleTests
    {
        [Test]
        public void SustainedStatusNames_KeepTunedStrengthAndExplainTheirCounters()
        {
            Assert.That(LegacyStatusNames.Name(LegacyStatusKind.PowerUp), Is.EqualTo("강화"));
            Assert.That(LegacyStatusNames.Name(LegacyStatusKind.Protection), Is.EqualTo("보호"));
            Assert.That(LegacyStatusNames.Name(LegacyStatusKind.Vulnerability), Is.EqualTo("취약"));
            Assert.That(LegacyStatusNames.Label(LegacyStatusKind.PowerUp, 20, 2), Is.EqualTo("강화 20% 2"));
            Assert.That(LegacyStatusNames.Label(LegacyStatusKind.PowerUp, 30, 1), Is.EqualTo("강화 30% 1"));
            Assert.That(LegacyStatusNames.Label(LegacyStatusKind.Protection, 25, 1), Is.EqualTo("보호 25% 1"));
            Assert.That(LegacyStatusNames.Label(LegacyStatusKind.Vulnerability, 50, 1), Is.EqualTo("취약 1"));
            Assert.That(LegacyStatusNames.Label(LegacyStatusKind.Vulnerability, 25, 2), Is.EqualTo("취약 25% 2"));
            Assert.That(LegacyStatusNames.Description(LegacyStatusKind.PowerUp, 30),
                Does.Contain("공격·방어").And.Contain("30%").And.Contain("숨고르기·빈 칸은 세지 않으며"));
            Assert.That(LegacyStatusNames.Description(LegacyStatusKind.Vulnerability, 50),
                Does.Contain("50%").And.Contain("연타는 1회").And.Contain("회피하면 소모하지").And.Contain("턴이 지나도 남습니다"));
            Assert.That(LegacyStatusNames.Description(LegacyStatusKind.Vulnerability, 25),
                Does.Contain("25%").And.Not.Contain("50%"));
            Assert.That(LegacyStatusNames.Description(LegacyStatusKind.Protection, 150),
                Does.Contain("보호 비율: 150%").And.Contain("취약").And.Contain("최대 100%").And.Not.Contain("150% 감소"));
            Assert.That(LegacyStatusNames.Description(LegacyStatusKind.Protection, 0),
                Does.Contain("표시된 비율").And.Not.Contain("보호 비율: 0%"));
            Assert.That(LegacyStatusNames.Description(LegacyStatusKind.Vulnerability, 50),
                Does.Contain("보호").And.Contain("상쇄"));
        }

        [TestCase(1, LegacySkillRole.ResistanceOnClash | LegacySkillRole.ActRecovery, "ACT 회복")]
        [TestCase(2, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit, "기본 연타")]
        [TestCase(3, LegacySkillRole.ResistanceOnClash | LegacySkillRole.HighPower, "집중 단타")]
        [TestCase(4, LegacySkillRole.ResistanceOnClash | LegacySkillRole.HighPower, "강한 단타")]
        [TestCase(5, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit | LegacySkillRole.DirectResistanceDamage, "공격 대응")]
        [TestCase(6, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit | LegacySkillRole.DirectResistanceDamage, "방어 대응")]
        [TestCase(7, LegacySkillRole.ActRecovery, "조건부 ACT")]
        [TestCase(8, LegacySkillRole.DamageReduction, "보호 부여")]
        [TestCase(9, LegacySkillRole.FollowupPower, "강화 부여")]
        [TestCase(10, LegacySkillRole.ResistanceOnClash | LegacySkillRole.FollowupPower, "강화 부여")]
        [TestCase(12, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit | LegacySkillRole.ActRecovery | LegacySkillRole.Vulnerability, "무리한 돌진")]
        [TestCase(14, LegacySkillRole.ResistanceOnClash, "단타")]
        [TestCase(15, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit | LegacySkillRole.Cycle, "순환 연타")]
        [TestCase(16, LegacySkillRole.ResistanceOnClash | LegacySkillRole.VariablePower, "변동 한방")]
        [TestCase(17, LegacySkillRole.AutoBreath, "방어·자동 숨")]
        [TestCase(19, LegacySkillRole.ResistanceRecovery, "붕괴 중 저항 회복")]
        [TestCase(21, LegacySkillRole.ResistanceOnClash | LegacySkillRole.Cycle, "순환 관통")]
        [TestCase(32, LegacySkillRole.QueueReturn, "반환 방어")]
        [TestCase(42, LegacySkillRole.ResistanceOnClash | LegacySkillRole.DirectResistanceDamage, "방어 대응·저항 감소")]
        [TestCase(51, LegacySkillRole.ResistanceOnClash | LegacySkillRole.AheadPower, "감소 거둠")]
        [TestCase(52, LegacySkillRole.ResistanceOnClash | LegacySkillRole.DirectResistanceDamage | LegacySkillRole.Cycle, "순환·공격 대응")]
        [TestCase(53, LegacySkillRole.ResistanceOnClash | LegacySkillRole.CycleFeed, "앞 순환 +1")]
        [TestCase(54, LegacySkillRole.ResistanceOnClash | LegacySkillRole.AutoDodge, "자동 회피")]
        [TestCase(55, LegacySkillRole.ResistanceOnClash | LegacySkillRole.AheadPower, "숨 거둠")]
        [TestCase(56, LegacySkillRole.ResistanceOnClash | LegacySkillRole.QueueReturn | LegacySkillRole.PlanningTimeCost, "반환·배율")]
        [TestCase(57, LegacySkillRole.ResistanceOnClash | LegacySkillRole.NextTurnTime, "순환·시간")]
        [TestCase(58, LegacySkillRole.TemporarySpawn, "일회성 생성")]
        [TestCase(59, LegacySkillRole.ResistanceOnClash | LegacySkillRole.Cycle, "일회성 순환")]
        [TestCase(60, LegacySkillRole.ResistanceOnClash | LegacySkillRole.ProtectionSpend, "보호 소모")]
        [TestCase(61, LegacySkillRole.ResistanceOnClash | LegacySkillRole.AheadHitPower, "연타 마무리")]
        [TestCase(500, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit | LegacySkillRole.HighPower | LegacySkillRole.OpponentBreak, "붕괴 연타")]
        [TestCase(501, LegacySkillRole.ResistanceRecovery, "조건부 저항 회복")]
        [TestCase(502, LegacySkillRole.ResistanceOnClash | LegacySkillRole.HighPower | LegacySkillRole.ConditionalDamage, "마무리 일격")]
        public void SupportedSkills_ReportOnlyImplementedRoles(int id, LegacySkillRole expected, string label)
        {
            LegacySkill skill = FindSkill(id);
            Assert.That(LegacySkillRoles.Get(skill), Is.EqualTo(expected));
            Assert.That(LegacySkillRoles.GetShortLabel(skill), Is.EqualTo(label));
            Assert.That(LegacySkillRoles.GetDetail(skill), Is.Not.Empty);
            if (skill.Kind == LegacySkillKind.Attack)
            {
                StringAssert.Contains("공격 대결: 저항부터 피해", LegacySkillRoles.GetDetail(skill));
                StringAssert.Contains("방어·빈 행동: 체력 피해", LegacySkillRoles.GetDetail(skill));
                StringAssert.Contains("방어 위력 차감", LegacySkillRoles.GetDetail(skill));
            }
        }

        [TestCase(LegacySkillProperty.Slash)]
        [TestCase(LegacySkillProperty.Hit)]
        [TestCase(LegacySkillProperty.Penetrate)]
        public void AttackProperties_ShareConditionalResistanceRoleWithoutInventingPropertySpecialEffects(LegacySkillProperty property)
        {
            var skill = new LegacySkill(900, "unknown", 1, 6, 8, LegacySkillKind.Attack, property, 1, 0, "");
            Assert.That(LegacySkillRoles.Get(skill), Is.EqualTo(LegacySkillRole.ResistanceOnClash));
            StringAssert.Contains("공격 대결: 저항부터 피해", LegacySkillRoles.GetDetail(skill));
            StringAssert.DoesNotContain("관통", LegacySkillRoles.GetDetail(skill));
            StringAssert.DoesNotContain("무시", LegacySkillRoles.GetDetail(skill));
        }

        [Test]
        public void UtilityDetails_DescribeTimingMagnitudeAndConditionsRatherThanUnportedEffects()
        {
            string recovery = LegacySkillRoles.GetDetail(FindSkill(1));
            StringAssert.Contains("다음 턴 ACT 회복량 +1", recovery);
            string strongStrike = LegacySkillRoles.GetDetail(FindSkill(3));
            StringAssert.Contains("ACT 2", strongStrike);
            StringAssert.Contains("강한 단타", strongStrike);
            StringAssert.DoesNotContain("후속", strongStrike);
            string heavierStrike = LegacySkillRoles.GetDetail(FindSkill(4));
            StringAssert.Contains("ACT 3", heavierStrike);
            string attackCounter = LegacySkillRoles.GetDetail(FindSkill(5));
            Assert.That(attackCounter, Does.Contain("같은 칸").And.Contain("공격 기술").And.Contain("기술 시작 시"));
            StringAssert.Contains("상대 저항을 10 직접", attackCounter);
            string guardCounter = LegacySkillRoles.GetDetail(FindSkill(6));
            Assert.That(guardCounter, Does.Contain("같은 칸").And.Contain("방어 기술").And.Contain("기술 시작 시"));
            StringAssert.Contains("상대 저항을 15 직접", guardCounter);
            string guardedRecovery = LegacySkillRoles.GetDetail(FindSkill(7));
            StringAssert.Contains("상대가 타격 기술이면", guardedRecovery);
            StringAssert.Contains("다음 턴 ACT 회복량 +2", guardedRecovery);
            string reduction = LegacySkillRoles.GetDetail(FindSkill(8));
            StringAssert.Contains("보호 50% 1 부여", reduction);
            StringAssert.Contains("50%", reduction);
            string parry = LegacySkillRoles.GetDetail(FindSkill(9));
            StringAssert.Contains("강화 50% 2 부여", parry);
            StringAssert.DoesNotContain("이번 턴", parry);
            StringAssert.DoesNotContain("다음 턴", parry);
        }

        [Test]
        public void AddedUtilityDetails_SeparateSlotTimingRecoveryAndConditionalResistanceDamage()
        {
            string preparation = LegacySkillRoles.GetDetail(FindSkill(10));
            StringAssert.Contains("강화 50% 1 부여", preparation);
            StringAssert.DoesNotContain("이번 턴", preparation);
            StringAssert.DoesNotContain("ACT", preparation);

            string advance = LegacySkillRoles.GetDetail(FindSkill(12));
            StringAssert.Contains("다음 턴 ACT 회복량 +2", advance);
            StringAssert.DoesNotContain("플레이어 전용", advance);
            StringAssert.Contains("취약 1 부여", advance);
            StringAssert.DoesNotContain("다음 1슬롯", advance);

            string fightingSpirit = LegacySkillRoles.GetDetail(FindSkill(19));
            StringAssert.Contains("기술 시작 시 상대가 붕괴 상태이면 자신의 최대 저항의 20%", fightingSpirit);
            StringAssert.DoesNotContain("반올림", fightingSpirit);
            StringAssert.DoesNotContain("피해 -30%", fightingSpirit);

            string draw = LegacySkillRoles.GetDetail(FindSkill(42));
            Assert.That(draw, Does.Contain("같은 칸").And.Contain("방어 기술").And.Contain("기술 시작 시"));
            StringAssert.Contains("상대 저항을 15 직접", draw);
            StringAssert.DoesNotContain("ACT", draw);
            StringAssert.Contains("초과 감소는 체력 피해가 되지", draw);
        }

        [TestCase(14, 1, LegacySkillRole.ActRecovery)]
        [TestCase(17, 7, LegacySkillRole.ActRecovery)]
        [TestCase(32, 8, LegacySkillRole.DamageReduction)]
        [TestCase(16, 2, LegacySkillRole.MultiHit | LegacySkillRole.HighPower)]
        [TestCase(10, 3, LegacySkillRole.MultiHit | LegacySkillRole.ActRecovery)]
        [TestCase(12, 1, LegacySkillRole.FollowupPower | LegacySkillRole.DamageReduction)]
        [TestCase(19, 8, LegacySkillRole.DamageReduction | LegacySkillRole.ActRecovery)]
        [TestCase(19, 13, LegacySkillRole.DamageReduction | LegacySkillRole.ActRecovery)]
        [TestCase(42, 10, LegacySkillRole.FollowupPower | LegacySkillRole.ResistanceRecovery)]
        public void ReusingAnArtworkAlias_DoesNotCopyAnotherSkillsEffects(int id, int misleadingIconId, LegacySkillRole prohibited)
        {
            LegacySkill basis = FindSkill(id);
            var aliased = new LegacySkill(basis.Id, "베기 +3", basis.Cost, basis.MinPower, basis.MaxPower,
                basis.Kind, basis.Property, basis.AttackCount, basis.LaneIndex,
                "다음 턴 ACT 회복량 +1", basis.AnimationName, misleadingIconId);
            Assert.That(LegacySkillRoles.Get(aliased), Is.EqualTo(LegacySkillRoles.Get(basis)));
            Assert.That(LegacySkillRoles.Get(aliased) & prohibited, Is.EqualTo(LegacySkillRole.None));
            Assert.That(LegacySkillRoles.GetShortLabel(aliased), Is.EqualTo(LegacySkillRoles.GetShortLabel(basis)));
            Assert.That(LegacySkillRoles.GetDetail(aliased), Is.EqualTo(LegacySkillRoles.GetDetail(basis)));
        }

        [Test]
        public void NullAndUnknownSkills_HaveConservativeKindBasedFallbacks()
        {
            Assert.That(LegacySkillRoles.Get(null), Is.EqualTo(LegacySkillRole.None));
            Assert.That(LegacySkillRoles.GetShortLabel(null), Is.Empty);
            Assert.That(LegacySkillRoles.GetDetail(null), Is.Empty);
            var attack = new LegacySkill(901, "massive ACT recovery", 1, 900, 999,
                LegacySkillKind.Attack, LegacySkillProperty.Hit, 2, 0, "", iconId: 1);
            Assert.That(LegacySkillRoles.Get(attack), Is.EqualTo(LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit));
            Assert.That(LegacySkillRoles.GetShortLabel(attack), Is.EqualTo("연타"));
            StringAssert.DoesNotContain("ACT", LegacySkillRoles.GetDetail(attack));
            var guard = new LegacySkill(902, "guard", 1, 3, 3,
                LegacySkillKind.Defence, LegacySkillProperty.Hit, 3, 0, "", iconId: 8);
            Assert.That(LegacySkillRoles.Get(guard), Is.EqualTo(LegacySkillRole.None));
            Assert.That(LegacySkillRoles.GetShortLabel(guard), Is.EqualTo("방어"));
            StringAssert.DoesNotContain("저항", LegacySkillRoles.GetDetail(guard));
            StringAssert.DoesNotContain("30%", LegacySkillRoles.GetDetail(guard));
        }

        [Test]
        public void CurriculumArtwork_HasSixDistinctMappingsAndStartingAliasesPreserveTheirChosenIcons()
        {
            int[] expectedSkillIds = { 14, 15, 16, 17, 21, 32 };
            int[] expectedStartingIcons = { 1, 11, 14, 4, 5, 6, 7, 8, 9 };
            var curriculumArtIds = new HashSet<int>();
            for (int i = 0; i < LegacyInitialSkills.All.Count; i++)
            {
                Assert.That(LegacySkillDefinitions.Skill(i + 1).IconId, Is.EqualTo(expectedStartingIcons[i]));
            }
            for (int i = 0; i < expectedSkillIds.Length; i++)
            {
                LegacySkill acquired = FindSkill(expectedSkillIds[i]);
                Assert.That(acquired.IconId, Is.EqualTo(10 + i));
                Assert.That(curriculumArtIds.Add(acquired.IconId), Is.True);
            }
            Assert.That(curriculumArtIds.Count, Is.EqualTo(6));

            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryUnequipSkill(7), Is.True);
            Assert.That(run.TryEquipSkill(14), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            CampaignOwnedSkill granted = Owned(run, 14);
            Assert.That(granted.Skill, Is.Not.SameAs(FindSkill(14)));
            Assert.That(granted.Skill.Id, Is.EqualTo(14));
            Assert.That(granted.Skill.IconId, Is.EqualTo(10));
            Assert.That(granted.Skill.Name, Is.EqualTo("가로베기"));
            Assert.That(LegacySkillRoles.Get(granted.Skill), Is.EqualTo(LegacySkillRole.ResistanceOnClash));
            LegacyQueuedDuel snapshot = run.CreateDuel();
            Assert.That(snapshot.GetLane(0)[2].Id, Is.EqualTo(14));
            Assert.That(snapshot.GetLane(0)[2].IconId, Is.EqualTo(10));
            Assert.That(snapshot.EnemyQueue[0].IconId, Is.EqualTo(1));
            Assert.That(snapshot.EnemyQueue[1].IconId, Is.EqualTo(11));
        }

        private static LegacySkill FindSkill(int id)
        {
            foreach (LegacySkill skill in LegacyInitialSkills.All) if (skill.Id == id) return skill;
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) if (skill.Id == id) return skill;
            foreach (LegacySkill skill in LegacySkillDefinitions.EnemySkills) if (skill.Id == id) return skill;
            foreach (LegacySkill skill in LegacySkillDefinitions.TemporarySkills) if (skill.Id == id) return skill;
            Assert.Fail("Missing supported skill " + id);
            return null;
        }

        private static CampaignOwnedSkill Owned(CampaignRun run, int id)
        {
            foreach (CampaignOwnedSkill owned in run.OwnedSkills) if (owned.SkillId == id) return owned;
            Assert.Fail("Missing owned skill " + id);
            return null;
        }
    }
}
