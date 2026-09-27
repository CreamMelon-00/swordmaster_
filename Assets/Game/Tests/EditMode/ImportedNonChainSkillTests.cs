using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class ImportedNonChainSkillTests
    {
        [TestCase(10, "준비", 0, 1, 2, 3, 1, LegacySkillKind.Attack, LegacySkillProperty.Hit)]
        [TestCase(12, "전진", 2, 1, 4, 8, 2, LegacySkillKind.Attack, LegacySkillProperty.Penetrate)]
        [TestCase(19, "투지", 2, 2, 7, 11, 1, LegacySkillKind.Defence, LegacySkillProperty.Defence)]
        [TestCase(42, "발검", 0, 1, 4, 8, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash)]
        public void Catalog_ImportsOriginalLevelZeroRowsWithoutChainSkills(int id, string name, int lane,
            int cost, int min, int max, int hits, LegacySkillKind kind, LegacySkillProperty property)
        {
            LegacySkill skill = FindImported(id);
            Assert.That(skill.Name, Is.EqualTo(name));
            Assert.That(skill.LaneIndex, Is.EqualTo(lane));
            Assert.That(skill.Cost, Is.EqualTo(cost));
            Assert.That(skill.MinPower, Is.EqualTo(min));
            Assert.That(skill.MaxPower, Is.EqualTo(max));
            Assert.That(skill.AttackCount, Is.EqualTo(hits));
            Assert.That(skill.Kind, Is.EqualTo(kind));
            Assert.That(skill.Property, Is.EqualTo(property));

            var ids = new HashSet<int>();
            foreach (LegacySkill imported in CampaignSkillCatalog.AcquisitionSkills)
            {
                Assert.That(ids.Add(imported.Id), Is.True, "Catalog IDs must remain unique.");
                Assert.That(imported.Id < 35 || imported.Id > 40, Is.True, "Legacy chain rows are excluded.");
            }
        }

        [TestCase(10)]
        [TestCase(12)]
        [TestCase(19)]
        [TestCase(42)]
        public void PurchaseEquipAndUpgrade_PreserveImportedSkillIdentityAndLane(int id)
        {
            var run = new CampaignRun();
            CampaignSkillOffer offer = FindOffer(run, id);
            LegacySkill basis = offer.Skill;
            Assert.That(run.TryAcquireSkill(id), Is.False, "A fresh run has no currency.");
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            int currency = run.Currency;
            Assert.That(run.TryAcquireSkill(id), Is.True);
            Assert.That(run.Currency, Is.EqualTo(currency - offer.Price));
            Assert.That(run.TryAcquireSkill(id), Is.False, "An offer can only be bought once.");
            foreach (CampaignSkillOffer remaining in run.Offers) Assert.That(remaining.SkillId, Is.Not.EqualTo(id));

            Assert.That(run.IsSkillEquipped(id), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(id, (basis.LaneIndex + 1) % 3, 0), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(id, basis.LaneIndex, 0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            LegacySkill equipped = run.CreateDuel().GetLane(basis.LaneIndex)[0];
            Assert.That(equipped, Is.SameAs(basis));

            Assert.That(run.TryStartStage(2), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.TryUpgradeSkill(id), Is.True);
            LegacySkill upgraded = run.CreateDuel().GetLane(basis.LaneIndex)[0];
            Assert.That(upgraded.Id, Is.EqualTo(id));
            Assert.That(upgraded.LaneIndex, Is.EqualTo(basis.LaneIndex));
            Assert.That(upgraded.Kind, Is.EqualTo(basis.Kind));
            Assert.That(upgraded.Property, Is.EqualTo(basis.Property));
            Assert.That(upgraded.AttackCount, Is.EqualTo(basis.AttackCount));
            Assert.That(upgraded.Description, Is.EqualTo(basis.Description));
            Assert.That(upgraded.MinPower, Is.EqualTo(basis.MinPower + 2));
            Assert.That(upgraded.MaxPower, Is.EqualTo(basis.MaxPower + 2));
        }

        [Test]
        public void Ready_BoostsOnlyFollowingSkillWithoutBoostingItself()
        {
            var duel = Duel(new[] { Imported(10, 10), Attack(100, 20), Attack(101, 20) },
                new[] { Attack(900, 1) }, new[] { 3 });
            Queue(duel, 0, 3);
            duel.Commit();

            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(10));
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(26));
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(20));
        }

        [TestCase(1, 5)]
        [TestCase(2, 5)]
        [TestCase(3, 7)]
        [TestCase(8, 7)]
        public void Campaign_GuardCounterHasAForecastedTargetFromStageThree(int stageNumber, int fifthSkillId)
        {
            var run = new CampaignRun();
            for (int cleared = 1; cleared < stageNumber; cleared++)
            {
                Assert.That(run.TryStartStage(cleared), Is.True);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            }
            Assert.That(run.TryStartStage(stageNumber), Is.True);
            LegacyQueuedDuel duel = run.CreateDuel();
            Assert.That(duel.EnemyQueue[0].Id, Is.EqualTo(1));
            Assert.That(duel.EnemyQueue[1].Id, Is.EqualTo(2));
            ResolveTurn(duel);
            Assert.That(duel.IsFinished, Is.False);
            duel.BeginNextTurn();
            Assert.That(duel.EnemyQueue.Count, Is.EqualTo(3));
            Assert.That(duel.EnemyQueue[0].Id, Is.EqualTo(3));
            Assert.That(duel.EnemyQueue[1].Id, Is.EqualTo(4));
            Assert.That(duel.EnemyQueue[2].Id, Is.EqualTo(fifthSkillId));
            Assert.That(duel.EnemyQueue[2].Kind, Is.EqualTo(stageNumber >= 3 ? LegacySkillKind.Defence : LegacySkillKind.Attack));
            Assert.That(duel.EnemyQueue[2].MinPower,
                Is.EqualTo((stageNumber >= 3 ? 5 : 6) + stageNumber - 1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Ready_BoostsFollowingGuardForEitherFighter(bool playerUsesReady)
        {
            LegacySkill[] preparing = { Imported(10, 4), Guard(100, 10), Guard(101, 10) };
            LegacySkill[] opposing = { Guard(900, 100), Attack(901, 13), Attack(902, 13) };
            var duel = Duel(playerUsesReady ? preparing : opposing,
                playerUsesReady ? opposing : preparing, new[] { 3 });
            Queue(duel, 0, 3);
            duel.Commit();
            duel.ResolveNextSlot();

            LegacySlotResult boosted = duel.ResolveNextSlot();
            LegacySlotResult ordinary = duel.ResolveNextSlot();
            Assert.That(playerUsesReady ? boosted.PlayerHealthDamage : boosted.EnemyHealthDamage, Is.Zero);
            Assert.That(playerUsesReady ? ordinary.PlayerHealthDamage : ordinary.EnemyHealthDamage, Is.EqualTo(3));
        }

        [TestCase(10, false)]
        [TestCase(10, true)]
        [TestCase(12, false)]
        [TestCase(12, true)]
        public void PendingSlotBuffs_ExpireAtTurnBoundaryAndReset(int id, bool reset)
        {
            var duel = Duel(new[] { Imported(id, 4), Attack(100, 20, lane: 1) },
                new[] { Attack(900, 4) }, new[] { 1 });
            Queue(duel, 0);
            ResolveTurn(duel);
            if (reset) duel.Reset();
            else duel.BeginNextTurn();

            Queue(duel, 1);
            ResolveTurn(duel);
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(1000 - (reset ? 0 : 4) - 20));
            Assert.That(duel.Player.Resistance, Is.EqualTo(1000 - (reset ? 0 : 4) - 4));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
        }

        [Test]
        public void Ready_StacksAdditivelyWithExistingThreeSkillStabBuff()
        {
            var duel = Duel(new[] { Attack(3, 10), Imported(10, 10), Attack(100, 20) },
                new[] { Attack(900, 1) }, new[] { 3 });
            Queue(duel, 0, 3);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(11));
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(28));
        }

        [Test]
        public void Forward_WorsensExactlyTheNextSlotIncludingEveryHitAndAddsNextAct()
        {
            var duel = Duel(new[] { Imported(12, 4, hits: 2), Attack(100, 1), Attack(101, 1) },
                new[] { Attack(900, 8, hits: 2) }, new[] { 3 });
            Queue(duel, 0, 3);
            duel.Commit();

            Assert.That(duel.ResolveNextSlot().PlayerResistanceDamage, Is.EqualTo(8));
            Assert.That(duel.ResolveNextSlot().PlayerResistanceDamage, Is.EqualTo(12));
            Assert.That(duel.ResolveNextSlot().PlayerResistanceDamage, Is.EqualTo(8));
            Assert.That(duel.NextActGain, Is.EqualTo(6));
        }

        [Test]
        public void Forward_CombinesAdditivelyWithExistingReceivedDamageReduction()
        {
            var duel = Duel(new[] { Guard(8, 100), Imported(12, 4), Attack(100, 1) },
                new[] { Attack(900, 10) }, new[] { 3 });
            Queue(duel, 0, 3);
            duel.Commit();
            duel.ResolveNextSlot();

            Assert.That(duel.ResolveNextSlot().PlayerResistanceDamage, Is.EqualTo(7));
            Assert.That(duel.ResolveNextSlot().PlayerResistanceDamage, Is.EqualTo(12));
        }

        [Test]
        public void Forward_KeepsExistingOverflowMultiplierOnAlreadyBrokenResistance()
        {
            var duel = Duel(new[] { Imported(12, 4), Attack(100, 1) },
                new[] { Attack(900, 8) }, new[] { 2 }, playerResistance: 0);
            Queue(duel, 0, 2);
            duel.Commit();

            Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.EqualTo(16));
            // The inherited clash overflow route scales 8 -> 12 -> 18, then
            // doubles health damage for broken resistance. Do not silently
            // rewrite that shared rule when importing this vulnerability.
            Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.EqualTo(36));
        }

        [Test]
        public void EnemyForward_WorsensItsNextSlotWithoutGrantingPlayerAct()
        {
            var duel = Duel(new[] { Attack(100, 8, hits: 2) },
                new[] { Imported(12, 4, hits: 2), Attack(900, 1), Attack(901, 1) }, new[] { 3 });
            Queue(duel, 0, 3);
            duel.Commit();

            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(8));
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(12));
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(8));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
        }

        [TestCase(12)]
        [TestCase(42)]
        public void LastSlotActBonus_SurvivesStepPenaltyAndDoesNotRefundCurrentAct(int id)
        {
            var duel = Duel(new[] { Imported(id, 4, cost: 1) }, new[] { Guard(900, 100) }, new[] { 1 });
            Queue(duel, 0);
            Assert.That(duel.Act, Is.EqualTo(2));
            duel.Commit();
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, false, out bool success), Is.True);
            Assert.That(success, Is.False);
            duel.ResolveNextSlot();

            Assert.That(duel.Act, Is.EqualTo(2));
            Assert.That(duel.NextActGain, Is.EqualTo(6));
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(5));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
        }

        [TestCase(25, 2)]
        [TestCase(35, 4)]
        [TestCase(50, 5)]
        [TestCase(55, 6)]
        public void FightingSpirit_RecoversRoundedMaximumResistanceBeforeHits(int maxResistance, int restored)
        {
            var duel = Duel(new[] { Attack(100, 1), Imported(19, 100) },
                new[] { Attack(900, 10), Guard(901, 1) }, new[] { 2 }, playerResistance: maxResistance);
            Queue(duel, 0, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.Player.Resistance, Is.EqualTo(maxResistance - 10));

            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.HitsResolved, Is.Zero);
            Assert.That(duel.Player.Resistance, Is.EqualTo(maxResistance - 10 + restored));
            while (!slot.IsResolved) duel.ResolveNextHit();
            LegacySlotResult result = duel.CompleteCurrentSlot();
            Assert.That(result.PlayerResistanceDamage, Is.EqualTo(-restored), "Slot deltas include the restoration.");
        }

        [TestCase(0)]
        [TestCase(50)]
        public void FightingSpirit_CannotExceedMaximumOrInventResistanceWhenMaximumIsZero(int resistance)
        {
            var duel = Duel(new[] { Imported(19, 100) }, new[] { Guard(900, 1) },
                new[] { 1 }, playerResistance: resistance);
            Queue(duel, 0);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.Player.Resistance, Is.EqualTo(resistance));
            duel.ResolveNextHit();
            Assert.That(duel.CompleteCurrentSlot().PlayerResistanceDamage, Is.Zero);
        }

        [Test]
        public void FightingSpirit_CapsRestorationWhenLessThanTenPercentIsMissing()
        {
            var duel = Duel(new[] { Attack(100, 1), Imported(19, 100) },
                new[] { Attack(900, 2), Guard(901, 1) }, new[] { 2 }, playerResistance: 50);
            Queue(duel, 0, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.Player.Resistance, Is.EqualTo(48));

            LegacySlotResult restored = duel.ResolveNextSlot();
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
            Assert.That(restored.PlayerResistanceDamage, Is.EqualTo(-2));
        }

        [Test]
        public void FightingSpirit_RestorationHappensOnceBeforeOpposingMultipleHits()
        {
            var duel = Duel(new[] { Attack(100, 1), Imported(19, 0) },
                new[] { Attack(900, 10), Attack(901, 12, hits: 3) }, new[] { 2 }, playerResistance: 50);
            Queue(duel, 0, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            duel.BeginNextSlot();
            Assert.That(duel.Player.Resistance, Is.EqualTo(45));

            for (int hit = 0; hit < 3; hit++)
                Assert.That(duel.ResolveNextHit().PlayerResistanceDamage, Is.Zero, "A guard takes HP damage, not resistance hits.");
            LegacySlotResult result = duel.CompleteCurrentSlot();
            Assert.That(duel.Player.Resistance, Is.EqualTo(45));
            Assert.That(result.PlayerResistanceDamage, Is.EqualTo(-5));
            Assert.That(result.PlayerHealthDamage, Is.EqualTo(12));
        }

        [Test]
        public void FightingSpirit_DoesNotCancelAlreadyPendingAutomaticResistanceRecovery()
        {
            var duel = Duel(new[] { Attack(100, 1), Imported(19, 100, lane: 1) },
                new[] { Attack(900, 60), Guard(901, 1), Guard(902, 1) }, new[] { 1, 1, 1 }, playerResistance: 50);
            Queue(duel, 0);
            ResolveTurn(duel);
            Assert.That(duel.Player.Resistance, Is.Zero);
            duel.BeginNextTurn();
            Assert.That(duel.Player.Resistance, Is.Zero);

            Queue(duel, 1);
            ResolveTurn(duel);
            Assert.That(duel.Player.Resistance, Is.EqualTo(5));
            duel.BeginNextTurn();
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
        }

        [Test]
        public void FightingSpirit_AlsoRestoresEnemyResistance()
        {
            var duel = Duel(new[] { Attack(100, 10), Guard(101, 100) },
                new[] { Attack(900, 1), Imported(19, 1) }, new[] { 2 }, enemyResistance: 50);
            Queue(duel, 0, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(40));
            duel.BeginNextSlot();
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(45));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            duel.ResolveNextHit();
            Assert.That(duel.CompleteCurrentSlot().EnemyResistanceDamage, Is.EqualTo(-5));
        }

        [TestCase(5, 0)]
        [TestCase(20, 0)]
        [TestCase(50, 30)]
        public void DrawSword_ReducesGuardResistanceOnceWithoutHealthOverflowAcrossMultipleHits(int resistance, int remaining)
        {
            var duel = Duel(new[] { Imported(42, 12, hits: 3) }, new[] { Guard(900, 100) },
                new[] { 1 }, enemyResistance: resistance);
            Queue(duel, 0);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(remaining));
            Assert.That(duel.Enemy.Health, Is.EqualTo(1000));
            Assert.That(duel.NextActGain, Is.EqualTo(6));

            for (int hit = 0; hit < 3; hit++)
            {
                LegacyHitResult result = duel.ResolveNextHit();
                Assert.That(result.EnemyResistanceDamage, Is.Zero);
                Assert.That(result.EnemyHealthDamage, Is.Zero);
                Assert.That(duel.Enemy.Resistance, Is.EqualTo(remaining));
                Assert.That(duel.NextActGain, Is.EqualTo(6));
            }
            Assert.That(duel.CompleteCurrentSlot().EnemyResistanceDamage, Is.EqualTo(resistance - remaining));
        }

        [Test]
        public void DrawSword_BreaksResistanceBeforeItsOwnGuardedHitUsesBrokenDamage()
        {
            var duel = Duel(new[] { Imported(42, 10) }, new[] { Guard(900, 4) },
                new[] { 1 }, enemyResistance: 15);
            Queue(duel, 0);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.Enemy.Resistance, Is.Zero);
            Assert.That(duel.Enemy.Health, Is.EqualTo(1000));

            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyHealthDamage, Is.EqualTo(12));
            LegacySlotResult result = duel.CompleteCurrentSlot();
            Assert.That(result.EnemyResistanceDamage, Is.EqualTo(15));
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(12));
        }

        [TestCase(LegacySkillProperty.Slash)]
        [TestCase(LegacySkillProperty.Defence)]
        public void DrawSword_DoesNotCounterAnAttackEvenIfItsPropertyIsDefence(LegacySkillProperty property)
        {
            var opponent = new LegacySkill(900, "attack", 0, 1, 1, LegacySkillKind.Attack, property, 1, 0, "");
            var duel = Duel(new[] { Imported(42, 10) }, new[] { opponent }, new[] { 1 }, enemyResistance: 50);
            Queue(duel, 0);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(50));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            duel.ResolveNextHit();
            Assert.That(duel.CompleteCurrentSlot().EnemyResistanceDamage, Is.EqualTo(10));
        }

        [Test]
        public void DrawSword_DoesNotCounterAnEmptyOpposingSlot()
        {
            var duel = Duel(new[] { Attack(100, 1), Imported(42, 10) },
                new[] { Guard(900, 100) }, new[] { 1 }, enemyResistance: 50);
            Queue(duel, 0, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemySkill, Is.Null);
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(50));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            duel.ResolveNextHit();
            Assert.That(duel.CompleteCurrentSlot().EnemyHealthDamage, Is.EqualTo(10));
        }

        [Test]
        public void EnemyDrawSword_ReducesPlayerGuardWithoutGrantingPlayerAct()
        {
            var duel = Duel(new[] { Guard(100, 100) }, new[] { Imported(42, 12, hits: 3) },
                new[] { 1 }, playerResistance: 50);
            Queue(duel, 0);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.Player.Resistance, Is.EqualTo(30));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
            Assert.That(duel.CompleteCurrentSlot().PlayerResistanceDamage, Is.EqualTo(20));

            duel.Reset();
            Assert.That(duel.Player.Resistance, Is.EqualTo(50));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            Assert.That(duel.CurrentSlot, Is.Null);
        }

        [Test]
        public void ExistingBreath_RemainsNumericOnly()
        {
            var duel = Duel(new[] { Imported(17, 100) }, new[] { Attack(900, 10) }, new[] { 1 });
            Queue(duel, 0);
            ResolveTurn(duel);
            Assert.That(duel.NextActGain, Is.EqualTo(3));
        }

        [Test]
        public void Ready_QueuedBreatheConsumesTheNextSlotBonusInsteadOfPassingItToAnAttack()
        {
            var duel = Duel(new[] { Imported(10, 10), Attack(100, 20) },
                new[] { Attack(900, 1) }, new[] { 3 });
            Queue(duel, 0);
            Assert.That(duel.TryQueueBreath(), Is.True);
            Queue(duel, 0);
            Assert.That(duel.PlayerQueue[1], Is.SameAs(LegacyCommonActions.Breathe));
            duel.Commit();

            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(10));
            LegacySlotResult waiting = duel.ResolveNextSlot();
            Assert.That(waiting.PlayerSkill.IsWait, Is.True);
            Assert.That(waiting.EnemyHealthDamage, Is.Zero);
            Assert.That(waiting.EnemyResistanceDamage, Is.Zero);
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(20));
        }

        [Test]
        public void Forward_QueuedBreatheTakesTheVulnerabilityForItsSlotAndKeepsNextActBonus()
        {
            var duel = Duel(new[] { Imported(12, 4), Attack(100, 1) },
                new[] { Attack(900, 8, hits: 2) }, new[] { 3 });
            Queue(duel, 0);
            Assert.That(duel.TryQueueBreath(), Is.True);
            Queue(duel, 0);
            duel.Commit();

            Assert.That(duel.ResolveNextSlot().PlayerResistanceDamage, Is.EqualTo(8));
            LegacySlotResult waiting = duel.ResolveNextSlot();
            Assert.That(waiting.PlayerSkill, Is.SameAs(LegacyCommonActions.Breathe));
            Assert.That(waiting.PlayerHealthDamage, Is.EqualTo(12));
            Assert.That(waiting.PlayerResistanceDamage, Is.Zero);
            Assert.That(duel.NextActGain, Is.EqualTo(6));
            Assert.That(duel.ResolveNextSlot().PlayerResistanceDamage, Is.EqualTo(8));
            Assert.That(duel.NextActGain, Is.EqualTo(6));
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(9));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
        }

        [Test]
        public void DrawSword_OpposingQueuedBreatheIsUnprotectedButDoesNotTriggerGuardCounter()
        {
            var duel = Duel(new[] { Imported(42, 10) }, new[] { LegacyCommonActions.Breathe },
                new[] { 1 }, enemyResistance: 50);
            Queue(duel, 0);
            Assert.That(duel.EnemyQueue[0], Is.SameAs(LegacyCommonActions.Breathe));
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();

            Assert.That(slot.EnemySkill.IsWait, Is.True);
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(50));
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            duel.ResolveNextHit();
            LegacySlotResult result = duel.CompleteCurrentSlot();
            Assert.That(result.EnemyResistanceDamage, Is.Zero);
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(10));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(50));
        }

        private static LegacySkill FindImported(int id)
        {
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills)
                if (skill.Id == id) return skill;
            Assert.Fail("Missing imported skill " + id);
            return null;
        }

        private static CampaignSkillOffer FindOffer(CampaignRun run, int id)
        {
            foreach (CampaignSkillOffer offer in run.Offers)
                if (offer.SkillId == id) return offer;
            Assert.Fail("Missing skill offer " + id);
            return null;
        }

        private static LegacySkill Imported(int id, int power, int hits = 1, int lane = 0, int cost = 0)
        {
            LegacySkill basis = FindImported(id);
            return new LegacySkill(basis.Id, basis.Name, cost, power, power, basis.Kind, basis.Property,
                hits, lane, basis.Description, basis.AnimationName, basis.IconId);
        }

        private static LegacySkill Attack(int id, int power, int hits = 1, int lane = 0)
            => new LegacySkill(id, "attack", 0, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, lane, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 0, power, power, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 0, "");

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy, int[] counts,
            int playerResistance = 1000, int enemyResistance = 1000)
            => new LegacyQueuedDuel(1000, playerResistance, 1000, enemyResistance, player, enemy, counts, randomSeed: 1);

        private static void Queue(LegacyQueuedDuel duel, int lane, int count = 1)
        {
            for (int i = 0; i < count; i++) Assert.That(duel.TryQueueLane(lane), Is.True);
        }

        private static void ResolveTurn(LegacyQueuedDuel duel)
        {
            duel.Commit();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
        }
    }
}
