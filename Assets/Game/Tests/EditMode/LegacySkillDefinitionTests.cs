using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacySkillDefinitionTests
    {
        [Test]
        public void Table_SplitsIntoStartingCurriculumAndEnemySkills()
        {
            IReadOnlyList<LegacySkillDefinition> all = LegacySkillDefinitions.All;
            // Lane starters and counter starters are separate; every sheet row is in exactly one set.
            Assert.That(all.Select(d => d.Skill).ToArray(), Is.EquivalentTo(LegacyInitialSkills.All
                .Concat(CampaignSkillCatalog.AcquisitionSkills).Concat(LegacySkillDefinitions.EnemySkills)
                .Concat(LegacySkillDefinitions.TemporarySkills).Concat(LegacySkillDefinitions.VersusSkills)
                .Concat(LegacySkillDefinitions.PrototypeSkills)
                .Concat(LegacySkillDefinitions.InitialCounterSkills).ToArray()));
            // The curriculum may grow; the starting set stays at nine.
            Assert.That(LegacyInitialSkills.All.Count, Is.EqualTo(9));
            Assert.That(LegacySkillDefinitions.InitialCounterSkills.Select(skill => skill.Id), Is.EqualTo(new[] { 600 }));
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
        public void StartingSkills_TeachBasicQConditionalEAndConcentratedW()
        {
            Assert.That(LegacyInitialSkills.All.Where(s => s.LaneIndex == 0).Select(s => s.Id),
                Is.EqualTo(new[] { 1, 2, 7 }));
            Assert.That(LegacyInitialSkills.All.Where(s => s.LaneIndex == 1).Select(s => s.Id),
                Is.EqualTo(new[] { 3, 4, 8 }));
            Assert.That(LegacyInitialSkills.All.Where(s => s.LaneIndex == 2).Select(s => s.Id),
                Is.EqualTo(new[] { 5, 6, 9 }));

            LegacySkill slash = LegacySkillDefinitions.Skill(1);
            LegacySkill combo = LegacySkillDefinitions.Skill(2);
            LegacySkill guard = LegacySkillDefinitions.Skill(7);
            Assert.That(new[] { slash.Cost, combo.Cost, guard.Cost }, Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(combo.Name, Is.EqualTo("연속 베기"));
            Assert.That(combo.AttackCount, Is.EqualTo(2));
            Assert.That(combo.MaxPower, Is.EqualTo(7));
            Assert.That(LegacySkillDefinitions.Find(combo).HighPower, Is.False,
                "The basic Q combo must leave the concentrated burst role to W.");
            Assert.That(guard.Kind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(LegacySkillDefinitions.Find(slash).Effect.ActGain, Is.EqualTo(1));
            Assert.That(LegacySkillDefinitions.Find(guard).Effect.ActGain, Is.EqualTo(2));
            Assert.That(LegacySkillDefinitions.Find(guard).Effect.OpponentProperty, Is.EqualTo(LegacySkillProperty.Hit));

            LegacySkill deep = LegacySkillDefinitions.Skill(3);
            LegacySkill precise = LegacySkillDefinitions.Skill(4);
            LegacySkill parry = LegacySkillDefinitions.Skill(8);
            Assert.That(new[] { deep.Cost, precise.Cost, parry.Cost }, Is.EqualTo(new[] { 2, 3, 2 }));
            Assert.That(deep.MinPower, Is.GreaterThan(combo.MaxPower));
            Assert.That(precise.MinPower, Is.GreaterThan(deep.MinPower));
            Assert.That(parry.MinPower, Is.GreaterThan(guard.MaxPower));
            Assert.That(deep.AttackCount, Is.EqualTo(1));
            Assert.That(precise.AttackCount, Is.EqualTo(1));
            Assert.That(parry.Kind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(LegacySkillDefinitions.Find(parry).Effect.BuffProtectionPercent, Is.EqualTo(50));
            Assert.That(LegacySkillDefinitions.Find(parry).Effect.BuffSlots, Is.EqualTo(1));

            LegacySkill pressure = LegacySkillDefinitions.Skill(5);
            LegacySkill opening = LegacySkillDefinitions.Skill(6);
            LegacySkill deflect = LegacySkillDefinitions.Skill(9);
            Assert.That(new[] { pressure.Cost, opening.Cost, deflect.Cost }, Is.EqualTo(new[] { 1, 2, 2 }));
            Assert.That(LegacySkillDefinitions.Find(pressure).Effect.OpponentKind, Is.EqualTo(LegacySkillKind.Attack));
            Assert.That(LegacySkillDefinitions.Find(pressure).Effect.OpponentResistanceReduction, Is.EqualTo(10));
            Assert.That(LegacySkillDefinitions.Find(opening).Effect.OpponentKind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(LegacySkillDefinitions.Find(opening).Effect.OpponentResistanceReduction, Is.EqualTo(18));
            Assert.That(LegacySkillDefinitions.Find(deflect).Effect.BuffPowerPercent, Is.EqualTo(50));
            Assert.That(LegacySkillDefinitions.Find(deflect).Effect.BuffSlots, Is.EqualTo(2));
        }

        [Test]
        public void FirstTwoStageQRewards_UseDedicatedArtworkAndTheirOwnRules()
        {
            LegacySkill scout = LegacySkillDefinitions.Skill(43);
            Assert.That((scout.Name, scout.Cost, scout.MinPower, scout.MaxPower, scout.LaneIndex),
                Is.EqualTo(("탐색", 1, 2, 3, 0)));
            Assert.That(scout.Kind, Is.EqualTo(LegacySkillKind.Defence));
            Assert.That(scout.IconId, Is.EqualTo(16));
            Assert.That(LegacySkillDefinitions.Find(scout).Effect.ActGain, Is.EqualTo(1));

            LegacySkill barrage = LegacySkillDefinitions.Skill(44);
            Assert.That((barrage.Name, barrage.Cost, barrage.MinPower, barrage.MaxPower, barrage.AttackCount),
                Is.EqualTo(("몰아치기", 2, 12, 16, 4)));
            Assert.That(barrage.Kind, Is.EqualTo(LegacySkillKind.Attack));
            Assert.That(barrage.Property, Is.EqualTo(LegacySkillProperty.Slash));
            Assert.That(barrage.LaneIndex, Is.Zero);
            Assert.That(barrage.IconId, Is.EqualTo(17));
            Assert.That(LegacySkillDefinitions.Find(barrage).Effect.BrokenTargetDamagePercent, Is.EqualTo(100));
            Assert.That((LegacySkillRoles.Get(barrage) & LegacySkillRole.BrokenTargetDamage) != 0, Is.True);
        }

        [Test]
        public void ThirdStageReward_CancelsEarlierSkills_AndPowersTheNextOne()
        {
            LegacySkill posture = LegacySkillDefinitions.Skill(45);
            Assert.That((posture.Name, posture.Cost, posture.MinPower, posture.MaxPower, posture.AttackCount, posture.LaneIndex),
                Is.EqualTo(("자세 잡기", 3, 3, 5, 1, 2)));
            Assert.That((posture.Kind, posture.Property),
                Is.EqualTo((LegacySkillKind.Defence, LegacySkillProperty.Defence)));
            Assert.That(posture.IconId, Is.EqualTo(21), "Its dedicated focus-and-guard icon.");
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(posture);
            Assert.That(definition.Effect.QueueRemovalMode, Is.EqualTo(LegacyQueueRemovalMode.Cancel));
            Assert.That(definition.Effect.QueuePowerMultiplierCap, Is.EqualTo(5));
            Assert.That((LegacySkillRoles.Get(posture) & LegacySkillRole.QueueCancel) != 0, Is.True);
            Assert.That(definition.Text.Info.Main, Is.EqualTo("취소"));
            Assert.That(definition.Text.Info.Description, Does.StartWith("예약 시"));
            Assert.That(definition.Text.Info.Description, Does.Contain("앞에 예약된 기술 모두 취소"));
            Assert.That(definition.Text.Info.Description,
                Does.Contain("2개 이상 취소하면 이번 턴 다음 기술 1개 위력 ×취소 수 (최대 ×5)"));
            Assert.That(definition.Text.Info.Description, Does.Not.Contain("ACT"));
        }

        [Test]
        public void MultiplayerOnlySkill_StealsNextTurnRecoveryAgainstAnAttack()
        {
            LegacySkill skill = LegacySkillDefinitions.Skill(62);
            Assert.That((skill.Name, skill.Cost, skill.MinPower, skill.MaxPower, skill.AttackCount, skill.LaneIndex),
                Is.EqualTo(("기세 훔치기", 2, 5, 7, 1, 2)));
            Assert.That((skill.Kind, skill.Property), Is.EqualTo((LegacySkillKind.Attack, LegacySkillProperty.Penetrate)));
            Assert.That(skill.IconId, Is.EqualTo(38));
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill).Effect;
            Assert.That(effect.HasActSteal, Is.False);
            Assert.That(effect.HasActRecoverySteal, Is.True);
            Assert.That((effect.ActRecoveryStealAmount, effect.ActGain), Is.EqualTo((1, 0)));
            Assert.That(effect.OpponentKind, Is.Null, "The use-effect phrase itself carries its attack condition.");
        }

        [Test]
        public void FifthStageReward_HasReusableCycleRules()
        {
            LegacySkill honing = LegacySkillDefinitions.Skill(46);
            Assert.That((honing.Name, honing.Cost, honing.MinPower, honing.MaxPower, honing.AttackCount, honing.LaneIndex),
                Is.EqualTo(("연마", 1, 5, 10, 1, 1)));
            Assert.That((honing.Kind, honing.Property), Is.EqualTo((LegacySkillKind.Attack, LegacySkillProperty.Slash)));
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(honing);
            Assert.That((definition.Effect.CyclePowerPerUse, definition.Effect.CycleCostPerUse, definition.Effect.CycleMaxCount),
                Is.EqualTo((6, 1, 9)));
            Assert.That(definition.Effect.HasCycle, Is.True);
            Assert.That((LegacySkillRoles.Get(honing) & LegacySkillRole.Cycle) != 0, Is.True);
            Assert.That(definition.Text.Info.Main, Is.EqualTo("순환"));
        }

        [Test]
        public void FieldPickupSkills_AreAcquisitionRowsWithOneExistingRuleEach()
        {
            LegacySkill pierce = LegacySkillDefinitions.Skill(47);
            Assert.That((pierce.Name, pierce.Cost, pierce.MinPower, pierce.MaxPower, pierce.AttackCount, pierce.LaneIndex),
                Is.EqualTo(("꿰뚫기", 4, 29, 32, 1, 1)));
            Assert.That((pierce.Kind, pierce.Property), Is.EqualTo((LegacySkillKind.Attack, LegacySkillProperty.Penetrate)));
            LegacySkillDefinition pierceDefinition = LegacySkillDefinitions.Find(pierce);
            Assert.That(pierceDefinition.HighPower, Is.True);
            Assert.That(pierceDefinition.VariablePower, Is.False);
            Assert.That(pierceDefinition.Effect, Is.SameAs(LegacySkillEffect.None), "A big plain thrust: no effect cell.");
            Assert.That(pierceDefinition.Text.Info, Is.Null, "W attacks keep the generic 고화력 · 단타 badges.");
            Assert.That(pierce.MaxPower, Is.LessThan(LegacySkillDefinitions.Skill(16).MaxPower),
                "Steadier than 알티바호 at the same ACT, never as high.");

            LegacySkill strikeBack = LegacySkillDefinitions.Skill(48);
            Assert.That((strikeBack.Name, strikeBack.Cost, strikeBack.MinPower, strikeBack.MaxPower, strikeBack.AttackCount, strikeBack.LaneIndex),
                Is.EqualTo(("받아치기", 1, 5, 7, 1, 2)));
            Assert.That((strikeBack.Kind, strikeBack.Property), Is.EqualTo((LegacySkillKind.Attack, LegacySkillProperty.Hit)));
            LegacySkillEffect strikeBackEffect = LegacySkillDefinitions.Find(strikeBack).Effect;
            Assert.That((strikeBackEffect.OpponentProperty, strikeBackEffect.OpponentResistanceReduction, strikeBackEffect.ActGain),
                Is.EqualTo(((LegacySkillProperty?)LegacySkillProperty.Penetrate, 12, 0)));
            Assert.That(LegacySkillDefinitions.Find(strikeBack).Text.Info.Main, Is.EqualTo("저항 -12"));

            LegacySkill sidestep = LegacySkillDefinitions.Skill(49);
            Assert.That((sidestep.Name, sidestep.Cost, sidestep.MinPower, sidestep.MaxPower, sidestep.AttackCount, sidestep.LaneIndex),
                Is.EqualTo(("비껴서기", 1, 6, 9, 1, 2)));
            Assert.That((sidestep.Kind, sidestep.Property), Is.EqualTo((LegacySkillKind.Defence, LegacySkillProperty.Defence)));
            LegacySkillEffect sidestepEffect = LegacySkillDefinitions.Find(sidestep).Effect;
            Assert.That((sidestepEffect.OpponentProperty, sidestepEffect.ActGain, sidestepEffect.OpponentResistanceReduction),
                Is.EqualTo(((LegacySkillProperty?)LegacySkillProperty.Slash, 2, 0)));
            Assert.That(LegacySkillDefinitions.Find(sidestep).Text.Info.Secondary, Is.EqualTo("참격 대응"));

            LegacySkill finisher = LegacySkillDefinitions.Skill(50);
            Assert.That((finisher.Name, finisher.Cost, finisher.MinPower, finisher.MaxPower, finisher.AttackCount, finisher.LaneIndex),
                Is.EqualTo(("끝내기", 2, 9, 11, 1, 2)));
            Assert.That((finisher.Kind, finisher.Property), Is.EqualTo((LegacySkillKind.Attack, LegacySkillProperty.Penetrate)));
            LegacySkillDefinition finisherDefinition = LegacySkillDefinitions.Find(finisher);
            Assert.That((finisherDefinition.Effect.OpponentState, finisherDefinition.Effect.OpponentHealthPercent,
                finisherDefinition.Effect.ConditionalDamagePercent), Is.EqualTo((LegacyOpponentState.HealthAtMost, 40, 200)));
            Assert.That(finisherDefinition.HighPower, Is.False);
            // 프레디카레's condition badge, shortened so it stays on one line in the 334px loadout and curriculum cards.
            Assert.That(finisherDefinition.Text.Info.Secondary, Is.EqualTo("상대 40% 이하"));
            Assert.That(finisherDefinition.Text.Info.EnemySecondary, Is.EqualTo("내 40% 이하"));

            foreach (LegacySkill skill in new[] { pierce, strikeBack, sidestep, finisher })
            {
                LegacySkillDefinition definition = LegacySkillDefinitions.Find(skill);
                Assert.That(LegacySkillDefinitions.Table.GroupOf(definition), Is.EqualTo(LegacySkillGroup.Acquisition), skill.Name);
                Assert.That(skill.IconId, Is.InRange(23, 26), skill.Name + " has its own field-skill icon.");
                Assert.That(definition.Text.Effect, Is.Not.Null, skill.Name);
                Assert.That(CampaignCurriculum.Default.FindGranting(skill.Id), Is.Null, skill.Name + " is not a curriculum reward.");
                Assert.That(CampaignFieldPickups.FindGranting(skill.Id), Is.Not.Null, skill.Name + " lies in the field.");
            }
        }

        [Test]
        public void Laudare_IsIasEnemyOnlyFiveHitSlashThatBreaksItsTarget()
        {
            LegacySkill laudare = LegacySkillDefinitions.Skill(500);
            Assert.That((laudare.Name, laudare.Cost, laudare.MinPower, laudare.MaxPower, laudare.AttackCount, laudare.LaneIndex),
                Is.EqualTo(("라우다레", 0, 30, 35, 5, 0)));
            Assert.That(laudare.Kind, Is.EqualTo(LegacySkillKind.Attack));
            Assert.That(laudare.Property, Is.EqualTo(LegacySkillProperty.Slash));
            Assert.That(laudare.IconId, Is.EqualTo(18), "Its own empowered slash icon.");
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(laudare);
            Assert.That(definition.Effect.BreaksOpponent, Is.True);
            Assert.That(definition.Effect.HasOpponentCondition, Is.False, "It breaks whatever it meets.");
            Assert.That(definition.HighPower, Is.True);
            Assert.That(LegacySkillDefinitions.Table.IsEnemy(definition), Is.True);
            Assert.That(LegacySkillDefinitions.EnemySkills.Select(s => s.Id), Is.EqualTo(new[] { 500, 501, 502, 503, 504, 505, 506 }));
            Assert.That(LegacyInitialSkills.All.Concat(CampaignSkillCatalog.AcquisitionSkills), Has.No.Member(laudare));
            Assert.That(definition.Text.Info.EnemyMain, Is.EqualTo("내 저항 붕괴"), "The player reads it as the enemy's skill.");
            Assert.That(CampaignCurriculum.Default.FindGranting(500), Is.Null);

            // The player can neither equip it nor load a save that does.
            var run = new CampaignRun();
            Assert.That(run.TryEquipSkill(500), Is.False);
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 500), Is.False);
            var save = new CampaignSave(0, new int[0], new string[0], null,
                new[] { new[] { 500, 2, 7 }, new[] { 3, 4, 8 }, new[] { 5, 6, 9 } });
            Assert.That(run.TryRestore(save, out string error), Is.False);
            Assert.That(error, Does.Contain("500"));
        }

        [Test]
        public void BenedicereAndPraedicare_FollowLaudareInIasMottoTurn()
        {
            LegacySkill benedicere = LegacySkillDefinitions.Skill(501);
            Assert.That((benedicere.Name, benedicere.Cost, benedicere.MinPower, benedicere.MaxPower, benedicere.AttackCount, benedicere.LaneIndex),
                Is.EqualTo(("베네디체레", 0, 5, 10, 1, 0)));
            Assert.That((benedicere.Kind, benedicere.Property), Is.EqualTo((LegacySkillKind.Defence, LegacySkillProperty.Defence)));
            Assert.That(benedicere.IconId, Is.EqualTo(19), "Its own empowered helmet icon.");
            LegacySkillDefinition guard = LegacySkillDefinitions.Find(benedicere);
            Assert.That((guard.Effect.ResistanceRecoveryPercent, guard.Effect.OpponentState), Is.EqualTo((25, LegacyOpponentState.Broken)));
            Assert.That(guard.Effect.HasOpponentCondition || guard.HighPower, Is.False);
            Assert.That(guard.Text.Info.Secondary, Is.EqualTo("상대 붕괴 시"));
            Assert.That(guard.Text.Info.EnemySecondary, Is.EqualTo("내 저항 붕괴 시"), "The player reads it as the enemy's skill.");

            LegacySkill praedicare = LegacySkillDefinitions.Skill(502);
            Assert.That((praedicare.Name, praedicare.Cost, praedicare.MinPower, praedicare.MaxPower, praedicare.AttackCount, praedicare.LaneIndex),
                Is.EqualTo(("프레디카레", 0, 24, 32, 1, 0)));
            Assert.That((praedicare.Kind, praedicare.Property), Is.EqualTo((LegacySkillKind.Attack, LegacySkillProperty.Slash)));
            Assert.That(praedicare.IconId, Is.EqualTo(20), "Its own empowered finisher icon.");
            LegacySkillDefinition finisher = LegacySkillDefinitions.Find(praedicare);
            Assert.That((finisher.Effect.OpponentState, finisher.Effect.OpponentHealthPercent, finisher.Effect.ConditionalDamagePercent),
                Is.EqualTo((LegacyOpponentState.HealthAtMost, 30, 200)));
            Assert.That(finisher.HighPower, Is.True);
            Assert.That(finisher.Text.Info.Secondary, Is.EqualTo("상대 체력 30% 이하"));
            Assert.That(finisher.Text.Info.EnemySecondary, Is.EqualTo("내 체력 30% 이하"));
            Assert.That(benedicere.Description,
                Does.Contain("상대가 붕괴 상태").And.Contain("자신의 최대 저항의 25%"));
            Assert.That(praedicare.Description,
                Does.Contain("상대 체력이 30% 이하").And.Contain("체력 피해가 2배"));

            var run = new CampaignRun();
            foreach (LegacySkillDefinition definition in new[] { guard, finisher })
            {
                Assert.That(LegacySkillDefinitions.Table.IsEnemy(definition), Is.True);
                Assert.That(definition.Text.Effect, Is.Null, "Never in the lobby.");
                Assert.That(CampaignCurriculum.Default.FindGranting(definition.Skill.Id), Is.Null);
                Assert.That(run.TryEquipSkill(definition.Skill.Id), Is.False);
            }
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
        public void ShippedSheet_HoldsItsTechniquesInTableOrder()
        {
            string path = Path.Combine(Environment.CurrentDirectory, LegacySkillDefinitions.SheetAssetPath);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Take(3).ToArray(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 with a byte order mark, for Excel.");
            LegacySkillTable table = LegacySkillSheet.Parse("shipped", File.ReadAllText(path));
            Assert.That(table.All.Select(d => d.Skill.Id), Is.EqualTo(new[]
                { 1, 2, 3, 4, 5, 6, 7, 8, 9, 14, 15, 16, 17, 21, 32, 10, 12, 19, 42, 43, 44, 45, 46, 47, 48, 49, 50,
                    51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 500, 501, 502, 503, 504, 505, 506, 600, 601, 602 }));
            Assert.That(table.InitialSkills.Select(s => s.Id), Is.EqualTo(Enumerable.Range(1, 9)));
            Assert.That(table.InitialCounterSkills.Select(s => s.Id), Is.EqualTo(new[] { 600 }));
            Assert.That(table.AcquisitionSkills.Select(s => s.Id),
                Is.EqualTo(new[] { 14, 15, 16, 17, 21, 32, 10, 12, 19, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 60, 61, 63, 64, 65, 66, 67, 68 }));
            Assert.That(table.VersusSkills.Select(s => s.Id), Is.EqualTo(new[] { 62 }));
            Assert.That(table.EnemySkills.Select(s => s.Id), Is.EqualTo(new[] { 500, 501, 502, 503, 504, 505, 506 }));
            Assert.That(table.TemporarySkills.Select(s => s.Id), Is.EqualTo(new[] { 59 }));
            Assert.That(table.PrototypeSkills.Select(s => s.Id), Is.EqualTo(new[] { 601, 602 }));
            Assert.That(LegacySkillDefinitions.All.Select(d => d.Skill.Id), Is.EqualTo(table.All.Select(d => d.Skill.Id)),
                "The runtime reads the same file.");
            Assert.That(LegacySkillDefinitions.Table.SheetId, Is.EqualTo(LegacySkillDefinitions.SheetAssetPath));
            Assert.That(LegacySkillDefinitions.SheetAssetPath, Is.EqualTo("Assets/Game/Resources/" + LegacySkillDefinitions.SheetResourcePath + ".csv"));
        }

        /// <summary>The keyword curriculum's rows (2026-10-13): 51-59 are new (59 갈아내기 is the 일회성 row 58 숫돌 sets), 15, 17,
        /// 19, 21 and 32 revised with their ID, name, lane, kind and property kept. Every number here is the author's to tune
        /// in the sheet. 51-59 draw their own icons, 27-35 (`SkillRoles/role27`…`role35`, sheet of 2026-10-16).</summary>
        [TestCase(51, "에스토카다", 1, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 3, 14, 18, 1, 27)]
        [TestCase(52, "바트망", 0, LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 2, 4, 1, 28)]
        [TestCase(53, "카덴스", 2, LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 5, 7, 1, 29)]
        [TestCase(54, "에스키브", 2, LegacySkillKind.Attack, LegacySkillProperty.Hit, 2, 4, 6, 1, 30)]
        [TestCase(55, "콩트르탕", 2, LegacySkillKind.Attack, LegacySkillProperty.Hit, 2, 5, 7, 1, 31)]
        [TestCase(56, "두블레", 1, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 3, 15, 19, 1, 32)]
        [TestCase(57, "잔걸음", 0, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 5, 7, 1, 33)]
        [TestCase(58, "숫돌", 1, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 4, 6, 1, 34)]
        [TestCase(59, "갈아내기", 0, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 2, 4, 1, 35)]
        [TestCase(60, "돌파", 1, LegacySkillKind.Attack, LegacySkillProperty.Slash, 2, 8, 12, 1, 36)]
        [TestCase(61, "추격", 2, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 2, 5, 7, 1, 37)]
        [TestCase(15, "사선베기", 0, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 4, 5, 2, 11)]
        [TestCase(17, "호흡", 0, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 4, 6, 1, 13)]
        [TestCase(19, "르프리즈", 2, LegacySkillKind.Defence, LegacySkillProperty.Defence, 2, 7, 11, 1, 13)]
        [TestCase(21, "급소 찌르기", 1, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 3, 18, 22, 1, 14)]
        [TestCase(32, "유연함", 1, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 6, 9, 1, 15)]
        public void KeywordCurriculumRows_HoldTheirSheetValues(int id, string name, int lane, LegacySkillKind kind,
            LegacySkillProperty property, int cost, int minPower, int maxPower, int hits, int icon)
        {
            LegacySkill skill = LegacySkillDefinitions.Skill(id);
            Assert.That((skill.Name, skill.LaneIndex, skill.Kind, skill.Property, skill.Cost, skill.MinPower, skill.MaxPower,
                skill.AttackCount, skill.IconId), Is.EqualTo((name, lane, kind, property, cost, minPower, maxPower, hits, icon)));
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(id);
            Assert.That(LegacySkillDefinitions.Table.GroupOf(definition),
                Is.EqualTo(id == 59 ? LegacySkillGroup.Temporary : LegacySkillGroup.Acquisition));
            Assert.That(definition.HighPower || definition.VariablePower, Is.False, "급소 찌르기 lost its 고화력 tag.");
            Assert.That(definition.Text.Effect, Is.Not.Null.And.Not.Empty, "Every new and revised row has its 로비 효과.");
            Assert.That(definition.Text.Info, Is.Not.Null, "and its badges.");
            // The range is verified above; effect copy need not repeat the separately displayed power facts.
            if (kind == LegacySkillKind.Attack)
                Assert.That(definition.Text.Detail, Does.EndWith("공격 대결: 저항부터 피해 / 방어·빈 행동: 체력 피해 (방어 위력 차감)"));
            LegacySkillEffect effect = definition.Effect;
            switch (id)
            {
                case 51:
                    Assert.That((effect.AheadPowerTarget, effect.AheadPowerPerUnit, effect.AheadPowerMax),
                        Is.EqualTo((LegacyAheadTarget.ResistanceReduction, 7, 2)));
                    break;
                case 52:
                    Assert.That((effect.OpponentResistanceReduction, effect.OpponentKind), Is.EqualTo((5, (LegacySkillKind?)LegacySkillKind.Attack)));
                    Assert.That((effect.CyclePowerPerUse, effect.CycleCostPerUse, effect.CycleMaxCount), Is.EqualTo((2, 0, 2)));
                    break;
                case 53:
                    Assert.That(effect.ReserveCycleFeed, Is.True);
                    break;
                case 54:
                    Assert.That(effect.AutoDodge, Is.True);
                    break;
                case 55:
                    Assert.That((effect.AheadPowerTarget, effect.AheadPowerPerUnit, effect.AheadPowerMax),
                        Is.EqualTo((LegacyAheadTarget.Breath, 6, 3)));
                    break;
                case 56:
                    Assert.That((effect.QueueRemovalMode, effect.QueuePowerMultiplierCap), Is.EqualTo((LegacyQueueRemovalMode.Return, 2)));
                    Assert.That(effect.ReservePlanningSecondsCost, Is.EqualTo(4));
                    string description = skill.Description.Replace(" ", string.Empty);
                    Assert.That(description, Does.Contain("2개이상반환"));
                    Assert.That(description, Does.Contain("이번턴다음기술"));
                    Assert.That(description, Does.Contain("2배"));
                    Assert.That(description, Does.Contain("제한시간4초를씁니다"));
                    break;
                case 57:
                    Assert.That((effect.AheadTimeTarget, effect.AheadTimePerUnit, effect.AheadTimeMax),
                        Is.EqualTo((LegacyAheadTarget.CycleSkill, 2, 3)));
                    break;
                case 58:
                    Assert.That((effect.ReserveTemporarySkillId, effect.ReserveTemporaryCount), Is.EqualTo((59, 3)));
                    break;
                case 59:
                    Assert.That((effect.CyclePowerPerUse, effect.CycleCostPerUse, effect.CycleMaxCount), Is.EqualTo((3, 0, 2)));
                    Assert.That(effect.HasReserveEffect || effect.HasUseEffect, Is.False);
                    break;
                case 60:
                    Assert.That(effect.HasProtectionSpend && effect.HasUseEffect, Is.True);
                    Assert.That(effect.ConsumeProtectionPowerBonus, Is.EqualTo(16));
                    Assert.That(effect.HasAheadHitPower, Is.False);
                    break;
                case 61:
                    Assert.That(effect.HasAheadHitPower && effect.HasUseEffect, Is.True);
                    Assert.That((effect.AheadAttackHitsRequired, effect.AheadAttackHitsPowerBonus), Is.EqualTo((4, 14)));
                    Assert.That(effect.HasProtectionSpend, Is.False);
                    break;
                case 15:
                    Assert.That((effect.CyclePowerPerUse, effect.CycleCostPerUse, effect.CycleMaxCount), Is.EqualTo((2, 0, 2)));
                    break;
                case 17:
                    Assert.That(effect.ReserveBreath, Is.True);
                    break;
                case 19:
                    Assert.That((effect.ResistanceRecoveryPercent, effect.OpponentState), Is.EqualTo((20, LegacyOpponentState.Broken)));
                    break;
                case 21:
                    Assert.That((effect.CyclePowerPerUse, effect.CycleCostPerUse, effect.CycleMaxCount), Is.EqualTo((4, 0, 2)));
                    break;
                case 32:
                    Assert.That((effect.QueueRemovalMode, effect.QueuePowerMultiplierCap), Is.EqualTo((LegacyQueueRemovalMode.Return, 0)));
                    break;
            }
        }

        [Test]
        public void ShippedSheet_UsesOnlyImportedIcons()
        {
            // Presentation's LegacyDuelArt.SkillIconCount; the runtime does not know the art source.
            const int skillIconCount = 39;
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
                Assert.That(definition.Skill.IconId, Is.InRange(1, skillIconCount), "icon of " + definition.Skill.Id);
        }

        [Test]
        public void NewSkills_UseDistinctDedicatedIcons_IncludingTheTemporaryCut()
        {
            int[] icons = Enumerable.Range(45, 15)
                .Select(id => LegacySkillDefinitions.Skill(id).IconId).ToArray();
            Assert.That(icons, Is.EqualTo(Enumerable.Range(21, 15).ToArray()),
                "The new skills must not fall back to unrelated attack or guard art.");
            Assert.That(LegacySkillDefinitions.TemporarySkills.Single(s => s.Id == 59).IconId,
                Is.EqualTo(35));
        }

        [TestCase(60, 36)]
        [TestCase(61, 37)]
        [TestCase(62, 38)]
        [TestCase(600, 39)]
        public void ProtectionFollowupActStealAndCounter_UseDedicatedIcons(int skillId, int iconId)
        {
            Assert.That(LegacySkillDefinitions.Skill(skillId).IconId, Is.EqualTo(iconId));
        }

        [TestCase(16, "알티바호")]
        [TestCase(12, "플레슈")]
        [TestCase(19, "르프리즈")]
        [TestCase(42, "쿠페")]
        [TestCase(1, "베기")]
        [TestCase(17, "호흡")]
        [TestCase(43, "탐색")]
        [TestCase(44, "몰아치기")]
        [TestCase(45, "자세 잡기")]
        [TestCase(47, "꿰뚫기")]
        [TestCase(48, "받아치기")]
        [TestCase(49, "비껴서기")]
        [TestCase(50, "끝내기")]
        public void ShippedSheet_NamesTheTechniques(int id, string name)
        {
            Assert.That(LegacySkillDefinitions.Skill(id).Name, Is.EqualTo(name));
        }

        [Test]
        public void FindAndSkill_LookUpById()
        {
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All)
            {
                Assert.That(LegacySkillDefinitions.Find(definition.Skill.Id), Is.SameAs(definition));
                Assert.That(LegacySkillDefinitions.Skill(definition.Skill.Id), Is.SameAs(definition.Skill));
            }
            Assert.That(LegacySkillDefinitions.Find(999), Is.Null);
            Assert.That(LegacySkillDefinitions.Find(LegacyCommonActions.Breathe.Id), Is.Null);
            var error = Assert.Throws<InvalidOperationException>(() => LegacySkillDefinitions.Skill(999));
            Assert.That(error.Message, Does.Contain("ID 999"));
            Assert.That(error.Message, Does.Contain(LegacySkillDefinitions.SheetAssetPath));
        }

        [Test]
        public void Install_AFailingSheetThrowsTheSameErrorUntilAGoodSheetReplacesIt()
        {
            string good = LegacySkillSheet.Write(LegacySkillDefinitions.Table);
            int reads = 0;
            try
            {
                LegacySkillDefinitions.Install(() =>
                {
                    reads++;
                    return "ID,이름\n1,베기\n";
                });
                var first = Assert.Throws<SkillSheetException>(() => _ = LegacySkillDefinitions.All);
                var second = Assert.Throws<SkillSheetException>(() => LegacySkillDefinitions.Find(1));
                Assert.That(second, Is.SameAs(first), "The failure is kept, not re-read.");
                Assert.That(reads, Is.EqualTo(1));
                Assert.That(first.Message, Does.StartWith("기술 시트 '" + LegacySkillDefinitions.SheetAssetPath + "'"));
                Assert.That(LegacySkillDefinitions.Find(null), Is.Null, "Null needs no table.");

                LegacySkillDefinitions.Reload();
                Assert.Throws<SkillSheetException>(() => _ = LegacySkillDefinitions.InitialSkills);
                Assert.That(reads, Is.EqualTo(2), "Reload reads the source again.");

                LegacySkillDefinitions.Install(() => throw new InvalidOperationException("no sheet"));
                Assert.That(Assert.Throws<InvalidOperationException>(() => _ = LegacySkillDefinitions.All).Message, Is.EqualTo("no sheet"));

                LegacySkillDefinitions.Install(() =>
                {
                    reads++;
                    return good.Replace(",베기,", ",새 베기,");
                });
                Assert.That(LegacySkillDefinitions.Skill(1).Name, Is.EqualTo("새 베기"));
                Assert.That(LegacySkillDefinitions.Skill(2).Name, Is.EqualTo("연속 베기"));
                Assert.That(LegacySkillDefinitions.InitialSkills[0].Name, Is.EqualTo("새 베기"));
                Assert.That(reads, Is.EqualTo(3), "A good table is read once.");
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
            Assert.That(LegacySkillDefinitions.Skill(1).Name, Is.EqualTo("베기"), "Null goes back to the sheet file.");
        }

        [Test]
        public void Install_AFileThatCouldNotBeOpenedIsTriedAgainOnTheNextAccess()
        {
            // Excel holding the sheet: closing Excel does not change the file, so nothing would reload a kept failure.
            string good = LegacySkillSheet.Write(LegacySkillDefinitions.Table);
            int reads = 0;
            try
            {
                LegacySkillDefinitions.Install(() =>
                {
                    if (++reads == 1) throw new IOException("used by another process");
                    return good;
                });
                Assert.Throws<IOException>(() => _ = LegacySkillDefinitions.All);
                Assert.That(LegacySkillDefinitions.Skill(1).Name, Is.EqualTo("베기"));
                Assert.That(reads, Is.EqualTo(2));
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        [Test]
        public void ReorderedOrRenamedSheet_ReachesMissionsStagesAndEnemiesById()
        {
            List<string[]> rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            int name = LegacySkillSheet.Headers.ToList().IndexOf(LegacySkillSheet.Column.Name);
            // 막기 moves to the top, the acquisition rows reverse, and 베기 is renamed.
            string[] guard = rows.Single(row => row[0] == "7");
            rows.Remove(guard);
            rows.Insert(1, guard);
            rows.Reverse(10, rows.Count - 10);
            rows.Single(row => row[0] == "1")[name] = "새 베기";
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                Assert.That(LegacySkillDefinitions.All[0].Skill.Id, Is.EqualTo(7));
                Assert.That(LegacyInitialSkills.All.Select(s => s.Id), Is.EqualTo(new[] { 7, 1, 2, 3, 4, 5, 6, 8, 9 }));

                Assert.That(PrologueMissions.Get(1).PlayerSkills.Select(s => s.Name), Is.EqualTo(new[] { "새 베기", "연속 베기" }));
                Assert.That(PrologueMissions.Get(4).EnemySkills.Select(s => s.Id), Is.EqualTo(new[] { 1, 5, 7, 2 }));
                Assert.That(StoryMissions.Get(8).PlayerSkills.Select(s => s.Id), Is.EqualTo(new[] { 1, 2, 7, 3, 4, 8, 5, 6, 9 }));
                Assert.That(new LegacyQueuedDuel().EnemyQueue.Select(s => s.Name), Is.EqualTo(new[] { "새 베기", "연속 베기" }));
                Assert.That(CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Gatekeeper).Turn(1).Select(s => s.Id), Is.EqualTo(new[] { 1, 3 }));

                var run = new CampaignRun();
                Assert.That(run.GetStage(5).EnemyCounterBasis, Is.Null, "The trio reserves its guards in its shared queue.");
                Assert.That(run.GetStage(6).EnemyCounterBasis, Is.SameAs(LegacySkillDefinitions.Skill(7)));
                Assert.That(run.GetStage(7).EnemyCounterBasis, Is.SameAs(LegacySkillDefinitions.Skill(4)));
                Assert.That(run.TryStartStage(1), Is.True);
                Assert.That(run.CreateDuel().EnemyQueue.Select(s => s.Name), Is.EqualTo(new[] { "새 베기", "연속 베기" }));
                Assert.That(run.GetEquippedLane(0).Select(s => s.SkillId), Is.EqualTo(new[] { 7, 1, 2 }),
                    "Only the default loadout follows the starting rows' order.");
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
            Assert.That(PrologueMissions.Get(1).PlayerSkills[0].Name, Is.EqualTo("베기"));
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
                // 상대 상태 조건 stands alone and gates a recovery or a multiplier; the multiplier needs it and an attack.
                if (effect.HasOpponentStateCondition)
                {
                    Assert.That(effect.HasOpponentCondition, Is.False, id);
                    Assert.That(effect.ResistanceRecoveryPercent > 0 || effect.ConditionalDamagePercent > 0, Is.True, id);
                }
                if (effect.OpponentState == LegacyOpponentState.HealthAtMost)
                    Assert.That(effect.OpponentHealthPercent, Is.InRange(1, 99), id);
                else Assert.That(effect.OpponentHealthPercent, Is.Zero, id);
                if (effect.ConditionalDamagePercent != 0)
                {
                    Assert.That(effect.ConditionalDamagePercent, Is.GreaterThan(100), id);
                    Assert.That(effect.HasOpponentStateCondition, Is.True, id);
                    Assert.That(definition.Skill.Kind, Is.EqualTo(LegacySkillKind.Attack), id);
                }
                if (definition.HighPower || definition.VariablePower)
                    Assert.That(definition.Skill.Kind, Is.EqualTo(LegacySkillKind.Attack), id);
            }
        }
    }
}
