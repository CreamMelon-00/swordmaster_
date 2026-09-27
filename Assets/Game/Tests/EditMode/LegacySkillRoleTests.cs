using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacySkillRoleTests
    {
        [TestCase(1, LegacySkillRole.ResistanceOnClash | LegacySkillRole.ActRecovery, "ACT 회복")]
        [TestCase(2, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit | LegacySkillRole.HighPower, "고화력·연타")]
        [TestCase(3, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit | LegacySkillRole.FollowupPower, "후속 위력")]
        [TestCase(4, LegacySkillRole.ResistanceOnClash | LegacySkillRole.HighPower, "고화력")]
        [TestCase(5, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit, "연타")]
        [TestCase(6, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit | LegacySkillRole.HighPower, "고화력·연타")]
        [TestCase(7, LegacySkillRole.ActRecovery, "조건부 ACT")]
        [TestCase(8, LegacySkillRole.DamageReduction, "피해 감소")]
        [TestCase(9, LegacySkillRole.FollowupPower, "후속 위력")]
        [TestCase(10, LegacySkillRole.ResistanceOnClash | LegacySkillRole.FollowupPower, "다음 칸 강화")]
        [TestCase(12, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit | LegacySkillRole.ActRecovery | LegacySkillRole.Vulnerability, "ACT 회복·취약")]
        [TestCase(14, LegacySkillRole.ResistanceOnClash, "단타")]
        [TestCase(15, LegacySkillRole.ResistanceOnClash | LegacySkillRole.MultiHit, "연타")]
        [TestCase(16, LegacySkillRole.ResistanceOnClash | LegacySkillRole.VariablePower, "변동 한방")]
        [TestCase(17, LegacySkillRole.None, "방어")]
        [TestCase(19, LegacySkillRole.ResistanceRecovery, "저항 회복")]
        [TestCase(21, LegacySkillRole.ResistanceOnClash | LegacySkillRole.HighPower, "고화력")]
        [TestCase(32, LegacySkillRole.None, "방어")]
        [TestCase(42, LegacySkillRole.ResistanceOnClash | LegacySkillRole.ActRecovery | LegacySkillRole.DirectResistanceDamage, "방어 대응·저항 감소")]
        public void SupportedSkills_ReportOnlyImplementedRoles(int id, LegacySkillRole expected, string label)
        {
            LegacySkill skill = FindSkill(id);
            Assert.That(LegacySkillRoles.Get(skill), Is.EqualTo(expected));
            Assert.That(LegacySkillRoles.GetShortLabel(skill), Is.EqualTo(label));
            Assert.That(LegacySkillRoles.GetDetail(skill), Is.Not.Empty);
            if (skill.Kind == LegacySkillKind.Attack)
            {
                StringAssert.Contains("상대 공격과 대결: 저항 피해", LegacySkillRoles.GetDetail(skill));
                StringAssert.Contains("방어·빈 행동: 체력 피해", LegacySkillRoles.GetDetail(skill));
                StringAssert.Contains("방어 수치 차감", LegacySkillRoles.GetDetail(skill));
            }
        }

        [TestCase(LegacySkillProperty.Slash)]
        [TestCase(LegacySkillProperty.Hit)]
        [TestCase(LegacySkillProperty.Penetrate)]
        public void AttackProperties_ShareConditionalResistanceRoleWithoutInventingPropertySpecialEffects(LegacySkillProperty property)
        {
            var skill = new LegacySkill(900, "unknown", 1, 6, 8, LegacySkillKind.Attack, property, 1, 0, "");
            Assert.That(LegacySkillRoles.Get(skill), Is.EqualTo(LegacySkillRole.ResistanceOnClash));
            StringAssert.Contains("상대 공격과 대결: 저항 피해", LegacySkillRoles.GetDetail(skill));
            StringAssert.DoesNotContain("관통", LegacySkillRoles.GetDetail(skill));
            StringAssert.DoesNotContain("무시", LegacySkillRoles.GetDetail(skill));
        }

        [Test]
        public void UtilityDetails_DescribeTimingMagnitudeAndConditionsRatherThanUnportedEffects()
        {
            string recovery = LegacySkillRoles.GetDetail(FindSkill(1));
            StringAssert.Contains("다음 턴 ACT 회복량 +1", recovery);
            string followup = LegacySkillRoles.GetDetail(FindSkill(3));
            StringAssert.Contains("이후 3슬롯", followup);
            StringAssert.Contains("공격·방어 위력 +10%", followup);
            string guardedRecovery = LegacySkillRoles.GetDetail(FindSkill(7));
            StringAssert.Contains("상대가 타격 기술이면", guardedRecovery);
            StringAssert.Contains("다음 턴 ACT 회복량 +2", guardedRecovery);
            string reduction = LegacySkillRoles.GetDetail(FindSkill(8));
            StringAssert.Contains("이번 턴의 이후 슬롯", reduction);
            StringAssert.Contains("피해 -30%", reduction);
            StringAssert.Contains("최대 10슬롯", reduction);
            string parry = LegacySkillRoles.GetDetail(FindSkill(9));
            StringAssert.Contains("이번 턴의 이후 슬롯", parry);
            StringAssert.Contains("공격·방어 위력 +3%", parry);
            StringAssert.Contains("최대 10슬롯", parry);
            StringAssert.DoesNotContain("다음 턴", parry);
        }

        [Test]
        public void AddedUtilityDetails_SeparateSlotTimingRecoveryAndConditionalResistanceDamage()
        {
            string preparation = LegacySkillRoles.GetDetail(FindSkill(10));
            StringAssert.Contains("이번 턴의 바로 다음 1슬롯", preparation);
            StringAssert.Contains("공격·방어 위력 +30%", preparation);
            StringAssert.DoesNotContain("ACT", preparation);

            string advance = LegacySkillRoles.GetDetail(FindSkill(12));
            StringAssert.Contains("다음 턴 ACT 회복량 +3", advance);
            StringAssert.Contains("플레이어 전용", advance);
            StringAssert.Contains("이번 턴의 바로 다음 1슬롯", advance);
            StringAssert.Contains("받는 피해 배율 +50%", advance);
            StringAssert.Contains("2회", advance);

            string fightingSpirit = LegacySkillRoles.GetDetail(FindSkill(19));
            StringAssert.Contains("기술 시작 시 최대 저항의 10%", fightingSpirit);
            StringAssert.Contains("최대치까지", fightingSpirit);
            StringAssert.DoesNotContain("피해 -30%", fightingSpirit);

            string draw = LegacySkillRoles.GetDetail(FindSkill(42));
            StringAssert.Contains("기술 시작 시 같은 슬롯 상대가 방어이면", draw);
            StringAssert.Contains("저항을 직접 20", draw);
            StringAssert.Contains("다음 턴 ACT 회복량 +3", draw);
            StringAssert.Contains("체력 피해로 이어지지", draw);
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
        public void UpgradingSkill_KeepsRealIdRolesAndNamesAndDoesNotMutateOriginal()
        {
            var run = new CampaignRun();
            run.TryStartStage(1);
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(run.TryUpgradeSkill(1), Is.True);
            CampaignOwnedSkill owned = Owned(run, 1);
            Assert.That(owned.Skill.Name, Is.EqualTo("베기 +1"));
            Assert.That(owned.BaseSkill.Name, Is.EqualTo("베기"));
            Assert.That(owned.Skill.Id, Is.EqualTo(1));
            Assert.That(owned.Skill.IconId, Is.EqualTo(1));
            Assert.That(LegacySkillRoles.Get(owned.Skill), Is.EqualTo(LegacySkillRoles.Get(owned.BaseSkill)));
            Assert.That(LegacySkillRoles.GetDetail(owned.Skill), Is.EqualTo(LegacySkillRoles.GetDetail(owned.BaseSkill)));
            Assert.That(LegacySkillRoles.GetShortLabel(owned.Skill), Is.EqualTo("ACT 회복"));
            Assert.That(owned.BaseSkill.MinPower, Is.EqualTo(4));
        }

        [Test]
        public void DedicatedShopArtwork_HasSixDistinctMappingsAndUpgradedSnapshotPreservesNewIconId()
        {
            int[] expectedSkillIds = { 14, 15, 16, 17, 21, 32 };
            var artIds = new HashSet<int>();
            for (int i = 0; i < LegacyInitialSkills.All.Count; i++)
            {
                Assert.That(LegacyInitialSkills.All[i].IconId, Is.EqualTo(i + 1));
                Assert.That(artIds.Add(LegacyInitialSkills.All[i].IconId), Is.True);
            }
            for (int i = 0; i < expectedSkillIds.Length; i++)
            {
                LegacySkill shop = FindSkill(expectedSkillIds[i]);
                Assert.That(shop.IconId, Is.EqualTo(10 + i));
                Assert.That(artIds.Add(shop.IconId), Is.True);
            }
            Assert.That(artIds.Count, Is.EqualTo(15));

            var run = new CampaignRun();
            run.TryStartStage(1);
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(run.TryAcquireSkill(14), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            Assert.That(run.TryUpgradeSkill(14), Is.True);
            Assert.That(run.TryUnequipSkill(7), Is.True);
            Assert.That(run.TryEquipSkill(14), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            CampaignOwnedSkill upgraded = Owned(run, 14);
            Assert.That(upgraded.Skill.IconId, Is.EqualTo(10));
            Assert.That(upgraded.BaseSkill.IconId, Is.EqualTo(10));
            Assert.That(upgraded.Skill.Name, Is.EqualTo("가로베기 +1"));
            Assert.That(LegacySkillRoles.Get(upgraded.Skill), Is.EqualTo(LegacySkillRole.ResistanceOnClash));
            LegacyQueuedDuel snapshot = run.CreateDuel();
            Assert.That(snapshot.GetLane(0)[2].Id, Is.EqualTo(14));
            Assert.That(snapshot.GetLane(0)[2].IconId, Is.EqualTo(10));
            Assert.That(snapshot.EnemyQueue[0].IconId, Is.EqualTo(1));
            Assert.That(snapshot.EnemyQueue[1].IconId, Is.EqualTo(2));
        }

        private static LegacySkill FindSkill(int id)
        {
            foreach (LegacySkill skill in LegacyInitialSkills.All) if (skill.Id == id) return skill;
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) if (skill.Id == id) return skill;
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
