using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class GreenRibbonBossBattleTests
    {
        [TestCase(400, 1)]
        [TestCase(301, 1)]
        [TestCase(300, 2)]
        [TestCase(121, 2)]
        [TestCase(120, 3)]
        [TestCase(1, 3)]
        public void HealthBoundariesHaveNoDamageGate(int health, int expected)
            => Assert.That(GreenRibbonBossBattle.PhaseAtHealth(health, 400), Is.EqualTo(expected));

        [Test]
        public void AuthoredGroupsUseUnmodifiedSheetSkillsAndBothAlternatives()
        {
            int[][][] expected =
            {
                new[] { new[] { 43, 503 }, new[] { 505, 10 }, new[] { 5, 47, 503, 1 } },
                new[] { new[] { -1, 8, 43 }, new[] { 505, 47, 504, 47 }, new[] { 503 }, new[] { -1, 47, 5, 503 } },
                new[] { new[] { -1, 47, 504, 47 }, new[] { 505, 504 }, new[] { 5, 47, 47 }, new[] { -1, 47, 503 } },
            };
            for (int phase = 1; phase <= 3; phase++)
                for (int step = 1; step <= GreenRibbonBossBattle.PatternCount(phase); step++)
                {
                    IReadOnlyList<LegacySkill> skills = GreenRibbonBossBattle.CreatePattern(phase, step);
                    Assert.That(skills.Select(skill => skill.Id), Is.EqualTo(expected[phase - 1][step - 1]));
                    foreach (LegacySkill skill in skills.Where(skill => !skill.IsWait))
                    {
                        LegacySkill sheet = LegacySkillDefinitions.Skill(skill.Id);
                        Assert.That(skill, Is.Not.SameAs(sheet));
                        Assert.That(skill.MinPower, Is.EqualTo(sheet.MinPower));
                        Assert.That(skill.MaxPower, Is.EqualTo(sheet.MaxPower));
                        Assert.That(skill.AnimationName, Is.EqualTo(sheet.AnimationName));
                    }
                }
            Assert.That(Ids(GreenRibbonBossBattle.CreatePattern(1, 2, true)), Is.EqualTo("15,10"));
            Assert.That(Ids(GreenRibbonBossBattle.CreatePattern(2, 3, true)), Is.EqualTo("17,-1"));
            Assert.That(Ids(GreenRibbonBossBattle.CreatePattern(3, 2, true)), Is.EqualTo("17,-1"));
            Assert.That(Ids(GreenRibbonBossBattle.CreatePattern(3, 3, true)), Is.EqualTo("48,47,6"));
        }

        [Test]
        public void OnlyTwoTechniquesAreInitiallyGenerated_AndFirstReservationRevealsTheThird()
        {
            LegacyQueuedDuel duel = Boss();
            Resolve(duel); duel.BeginNextTurn();
            Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.RoundNumber, Is.EqualTo(3));
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("5,47"));
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("5,47"), "A manual breath never requests a technique.");
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("5,47,503"));
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("5,47,503,1"));
            Resolve(duel); duel.BeginNextTurn();
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("43,503"), "The phase loops from its first group.");
        }

        [Test]
        public void PhaseChangesOnTheNextPreparation_AndCanSkipAnEntirePhase()
        {
            LegacyQueuedDuel duel = Boss(player: new[] { Attack(100, 1, 0) });
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TrySetFixedQueues(duel.PlayerQueue.ToArray(), new[] { LegacyCommonActions.Breathe }), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.Enemy.Health, Is.EqualTo(300));
            Assert.That(duel.IiaPhase, Is.EqualTo(1), "A threshold crossing never interrupts a skill or rewrites this turn.");
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            duel.BeginNextTurn();
            Assert.That(duel.IiaPhase, Is.EqualTo(2));
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("-1,8"));

            duel = Boss(player: new[] { Attack(290, 1, 0) });
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TrySetFixedQueues(duel.PlayerQueue.ToArray(), new[] { LegacyCommonActions.Breathe }), Is.True);
            Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.Enemy.Health, Is.EqualTo(110));
            Assert.That(duel.IiaPhase, Is.EqualTo(3));
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("-1,47"));
        }

        [Test]
        public void BranchChoiceIsSeeded_AndForecastsCannotRevealOrChangeTheLiveTail()
        {
            var variants = new HashSet<string>();
            for (int seed = 1; seed <= 12; seed++)
            {
                LegacyQueuedDuel control = Boss(seed), previewed = Boss(seed);
                Resolve(control); Resolve(previewed); control.BeginNextTurn(); previewed.BeginNextTurn();
                variants.Add(Ids(control.EnemyQueue));
                string before = Ids(previewed.EnemyQueue);
                for (int i = 0; i < 5; i++)
                {
                    previewed.ForecastTurn(new List<LegacySlotForecast>());
                    previewed.ForecastIfQueued(0);
                }
                Assert.That(Ids(previewed.EnemyQueue), Is.EqualTo(before));
                Assert.That(previewed.TryQueueLane(0), Is.True);
                Assert.That(control.TryQueueLane(0), Is.True);
                Assert.That(Ids(previewed.EnemyQueue), Is.EqualTo(Ids(control.EnemyQueue)));
                Resolve(control); Resolve(previewed);
                Assert.That(previewed.Player.Health, Is.EqualTo(control.Player.Health));
                Assert.That(previewed.Enemy.Health, Is.EqualTo(control.Enemy.Health));
            }
            Assert.That(variants.Count, Is.EqualTo(2), "Both authored alternatives are reachable.");
        }

        [Test]
        public void AReservationPreviewDoesNotDiscloseAnUncreatedThirdTechnique()
        {
            LegacyQueuedDuel duel = Boss();
            Resolve(duel); duel.BeginNextTurn(); Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.TryQueueBreath(), Is.True);
            Assert.That(duel.TryQueueBreath(), Is.True);
            LegacySlotForecast candidate = duel.ForecastIfQueued(0);
            Assert.That(candidate.SlotIndex, Is.EqualTo(2));
            Assert.That(candidate.EnemySkill, Is.Null, "The third technique appears only after an actual reservation.");
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("5,47"));
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("5,47,503"));
            var forecast = new List<LegacySlotForecast>();
            duel.ForecastTurn(forecast);
            Assert.That(forecast[2].EnemySkill.Id, Is.EqualTo(503));
        }

        [TestCase(32)]
        [TestCase(45)]
        public void ReturningOrCancelingPlayerReservationsNeverRewindsTheGeneratedEnemyQueue(int removerId)
        {
            LegacySkill remover = LegacySkillDefinitions.Skill(removerId);
            LegacyQueuedDuel duel = Boss(player: new[] { Attack(1, 1, 0), remover });
            Resolve(duel); duel.BeginNextTurn(); Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("5,47,503"));
            Assert.That(duel.TryQueueLane(remover.LaneIndex), Is.True);
            Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(removerId.ToString()));
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("5,47,503,1"));
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("5,47,503,1"), "The pattern has no duplicate or refunded tail.");
        }

        [Test]
        public void BreathingIncludesItsAutomaticWaitWithoutRequestingAnExtraTechnique()
        {
            bool reachedAlternative = false;
            for (int seed = 1; seed <= 12 && !reachedAlternative; seed++)
            {
                LegacyQueuedDuel duel = Boss(seed, new[] { Attack(1, 1, 0) });
                Assert.That(duel.TrySetFixedQueues(new[] { Attack(100, 1, 0) }, new[] { LegacyCommonActions.Breathe }), Is.True);
                Resolve(duel); duel.BeginNextTurn();
                Resolve(duel); duel.BeginNextTurn(); Resolve(duel); duel.BeginNextTurn();
                if (duel.EnemyQueue[0].Id != 17) continue;
                reachedAlternative = true;
                Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("17,-1"));
                Assert.That(((ILegacyDuelView)duel).EnemyAutoBreathSlotIndex, Is.EqualTo(1));
                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("17,-1"));
            }
            Assert.That(reachedAlternative, Is.True);
        }

        [Test]
        public void MatchingGuardAndAttackToThePressureComboLosesLessHealthThanLeavingItsSlotsEmpty()
        {
            LegacySkill[] skills = { LegacySkillDefinitions.Skill(7), LegacySkillDefinitions.Skill(3) };
            IReadOnlyList<LegacySkill> pressure = GreenRibbonBossBattle.CreatePattern(1, 3);
            for (int seed = 1; seed <= 8; seed++)
            {
                LegacyQueuedDuel NewPressureDuel() => new LegacyQueuedDuel(GreenRibbonBossBattle.PlayerBaseHealth, 50,
                    GreenRibbonBossBattle.EnemyHealth, GreenRibbonBossBattle.EnemyResistance, skills,
                    GreenRibbonBossBattle.CreateScript(), seed, meshPercent: 0).EnableIiaBossPatterns();
                LegacyQueuedDuel ignored = NewPressureDuel(), answered = NewPressureDuel();
                Assert.That(ignored.TrySetFixedQueues(new[] { LegacyCommonActions.Breathe, LegacyCommonActions.Breathe }, pressure), Is.True);
                Assert.That(answered.TryQueueLane(0), Is.True, "Guard answers the opening blunt attack without its anti-attack reduction.");
                Assert.That(answered.TryQueueLane(1), Is.True, "The next attack clashes with the strong pierce rather than taking it in health.");
                Assert.That(answered.TrySetFixedQueues(answered.PlayerQueue.ToArray(), pressure), Is.True);
                Resolve(ignored); Resolve(answered);
                Assert.That(answered.Player.Health, Is.GreaterThan(ignored.Player.Health), "The same combo rewards matching its slots, seed " + seed);
            }
        }

        [Test]
        public void AttackingARecoveryWindowThenTheNextTurnsTailReachesFiveJealousyThroughRealReservations()
        {
            // Large resistance isolates the two consecutive turn windows from an unrelated break canceling the tail.
            var duel = new LegacyQueuedDuel(100000, 1000, GreenRibbonBossBattle.EnemyHealth,
                GreenRibbonBossBattle.EnemyResistance, new[] { Attack(3, 3, 0), Attack(2, 2, 1), Attack(120, 3, 2) },
                GreenRibbonBossBattle.CreateScript(), 1, meshPercent: 0).EnableIiaBossPatterns();
            Assert.That(duel.TryQueueLane(2), Is.True);
            Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.IiaPhase, Is.EqualTo(2));
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("-1,8"));
            Assert.That(duel.TryQueueLane(0), Is.True);
            Resolve(duel);
            Assert.That(duel.IiaJealousy, Is.EqualTo(3), "Three landed hits exploit the visible recovery slot.");
            duel.BeginNextTurn();
            foreach (int lane in new[] { 1, 1, 0, 0, 1 }) Assert.That(duel.TryQueueLane(lane), Is.True);
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("505,47,504,47"));
            Resolve(duel);
            Assert.That(duel.IiaJealousy, Is.EqualTo(5), "Two more hits beyond the next generated pattern keep jealousy from expiring.");
            duel.BeginNextTurn();
            Assert.That(duel.IiaJealousy, Is.Zero);
            Assert.That(duel.EnemyQueue.Last().Id, Is.EqualTo(GreenRibbonBossBattle.JealousySkillId));
        }

        [Test]
        public void JealousyCountsLandedUnopposedHits_AndTheFifthArmsOneLastReservation()
        {
            LegacyQueuedDuel duel = Boss(player: new[] { Attack(6, 6, 0) });
            Assert.That(duel.TrySetFixedQueues(new[] { LegacyCommonActions.Breathe, LegacyCommonActions.Breathe, Attack(6, 6, 0) },
                new[] { LegacyCommonActions.Breathe }), Is.True);
            duel.Commit(); duel.ResolveNextSlot(); duel.ResolveNextSlot();
            duel.BeginNextSlot();
            for (int hit = 1; hit <= 6; hit++)
            {
                duel.ResolveNextHit();
                Assert.That(duel.IiaJealousy, Is.EqualTo(hit));
            }
            duel.CompleteCurrentSlot(); duel.BeginNextTurn();
            Assert.That(duel.IiaJealousy, Is.Zero);
            Assert.That(duel.EnemyQueue.Last().Id, Is.EqualTo(506));
            Assert.That(duel.EnemyQueue.Count, Is.EqualTo(3));
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.EnemyQueue.Last().Id, Is.EqualTo(506));
            Assert.That(duel.EnemyQueue.Count(skill => skill.Id == 506), Is.EqualTo(1));
        }

        [Test]
        public void ClashesGuardsAndZeroDamageDoNotGrantJealousy_AndAQuietTurnExpiresIt()
        {
            foreach (LegacySkill opposing in new[] { Attack(1, 1, 0), Guard(0), Guard(100) })
            {
                LegacyQueuedDuel duel = Boss();
                Assert.That(duel.TrySetFixedQueues(new[] { Attack(6, 6, 0) }, new[] { opposing }), Is.True);
                Resolve(duel);
                Assert.That(duel.IiaJealousy, Is.Zero);
            }
            LegacyQueuedDuel quiet = Boss();
            Assert.That(quiet.TrySetFixedQueues(new[] { Attack(3, 3, 0) }, new[] { LegacyCommonActions.Breathe }), Is.True);
            Resolve(quiet); quiet.BeginNextTurn();
            Assert.That(quiet.IiaJealousy, Is.EqualTo(3));
            Resolve(quiet); quiet.BeginNextTurn();
            Assert.That(quiet.IiaJealousy, Is.Zero);

            LegacyQueuedDuel protectedEnemy = Boss();
            Assert.That(protectedEnemy.TrySetFixedQueues(new[] { LegacyCommonActions.Breathe, LegacyCommonActions.Breathe, Attack(3, 3, 0) },
                new[] { LegacySkillDefinitions.Skill(8), LegacySkillDefinitions.Skill(8), LegacyCommonActions.Breathe }), Is.True);
            Resolve(protectedEnemy);
            Assert.That(protectedEnemy.IiaJealousy, Is.Zero, "An unopposed attack absorbed entirely by protection did not land damage.");
        }

        [Test]
        public void AThreeHitFixedAttackBlocksOnlyTheNextPreparation_AndACompleteGuardPreventsIt()
        {
            LegacyQueuedDuel duel = Boss(player: new[] { Attack(1, 1, 0), Guard(100) });
            Assert.That(duel.TrySetFixedQueues(new[] { LegacyCommonActions.Breathe }, new[] { LegacySkillDefinitions.Skill(506) }), Is.True);
            Resolve(duel);
            Assert.That(duel.PlayerFixedTurnsRemaining, Is.EqualTo(1));
            duel.BeginNextTurn();
            Assert.That(duel.PlayerFixedTurnsRemaining, Is.EqualTo(1));
            Assert.That(duel.TryCycleLanes(), Is.False);
            Assert.That(duel.TryQueueLane(0), Is.True, "Fixed only prevents 넘기기, not reservations.");
            Assert.That(duel.TrySetFixedQueues(new[] { Guard(100) }, new[] { LegacyCommonActions.Breathe }), Is.True);
            Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.PlayerFixedTurnsRemaining, Is.Zero);
            Assert.That(duel.TryCycleLanes(), Is.True);

            duel = Boss();
            Assert.That(duel.TrySetFixedQueues(new[] { Guard(100) }, new[] { LegacySkillDefinitions.Skill(506) }), Is.True);
            Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.PlayerFixedTurnsRemaining, Is.Zero, "No landed damage means no Fixed.");

            duel = Boss();
            Assert.That(duel.TrySetFixedQueues(new[] { LegacyCommonActions.Breathe }, new[] { LegacySkillDefinitions.Skill(506) }), Is.True);
            duel.Commit(); duel.BeginNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool success), Is.True);
            Assert.That(success, Is.True);
            while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot(); duel.BeginNextTurn();
            Assert.That(duel.PlayerFixedTurnsRemaining, Is.Zero, "Dodging all hits prevents Fixed.");
        }

        [Test]
        public void FollowCountsActualLastTurnTechniques_IncludingDefencesButExcludingBreathsAndCanceledTail()
        {
            LegacyQueuedDuel duel = Boss();
            Assert.That(duel.TrySetFixedQueues(new[] { Guard(1), LegacyCommonActions.Breathe, Guard(1) },
                new[] { LegacyCommonActions.Breathe }), Is.True);
            Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.PlayerSkillsUsedPreviousTurn, Is.EqualTo(2));
            Assert.That(duel.TrySetFixedQueues(new[] { LegacyCommonActions.Breathe }, new[] { LegacySkillDefinitions.Skill(505) }), Is.True);
            duel.Commit(); duel.BeginNextSlot();
            Assert.That(duel.EnemyStatuses.Single().Percent, Is.EqualTo(50));
            Assert.That(duel.EnemyStatuses.Single().RemainingCount, Is.EqualTo(2));
            while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot(); duel.BeginNextTurn();
            Assert.That(duel.PlayerSkillsUsedPreviousTurn, Is.Zero);

            duel = Boss();
            Assert.That(duel.TrySetFixedQueues(new[] { Attack(1, 1, 0), Guard(1) }, new[] { Attack(60, 1, 0) }), Is.True);
            Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.PlayerSkillsUsedPreviousTurn, Is.EqualTo(1), "A break-canceled technique was never used.");
        }

        [Test]
        public void MirrorReadsTheOpposingTechniqueMaximum_AndHasZeroPowerAgainstNoTechnique()
        {
            LegacySkill mirror = LegacySkillDefinitions.Skill(504);
            foreach (LegacySkill opposing in new[] { Attack(17, 1, 0), Guard(17), LegacyCommonActions.Breathe })
            {
                LegacyQueuedDuel duel = Boss();
                Assert.That(duel.TrySetFixedQueues(new[] { opposing }, new[] { mirror }), Is.True);
                ((ILegacyDuelView)duel).EnemyEffectivePowerRange(duel.EnemyQueue[0], out int min, out int max);
                Assert.That(min, Is.EqualTo(opposing.IsWait ? 0 : 17));
                Assert.That(max, Is.EqualTo(min));
                duel.Commit(); duel.BeginNextSlot();
                LegacyHitResult hit = duel.ResolveNextHit();
                if (opposing.IsWait) Assert.That(hit.PlayerDisplayedDamage, Is.Zero);
                else if (opposing.Kind == LegacySkillKind.Attack) Assert.That(hit.PlayerResistanceDamage, Is.EqualTo(17));
                else Assert.That(hit.PlayerDisplayedDamage, Is.Zero, "Equal mirrored guard power absorbs the whole attack.");
            }
        }

        [Test]
        public void RepeatedMirrorsEachReadTheirOwnOpposingSlot()
        {
            LegacyQueuedDuel duel = Boss();
            Assert.That(duel.TrySetFixedQueues(new[] { Guard(0), Attack(4, 1, 0), Attack(9, 1, 0), Attack(16, 1, 0) },
                new[] { LegacySkillDefinitions.Skill(505), LegacySkillDefinitions.Skill(504),
                    LegacySkillDefinitions.Skill(504), LegacySkillDefinitions.Skill(504) }), Is.True);
            for (int slot = 1; slot <= 3; slot++)
            {
                ((ILegacyDuelView)duel).EnemyEffectivePowerRange(duel.EnemyQueue[slot], out int min, out int max);
                Assert.That(min, Is.EqualTo(duel.PlayerQueue[slot].MaxPower));
                Assert.That(max, Is.EqualTo(min));
            }
        }

        [Test]
        public void MirrorIncludesAnActualCountersCycleBonus_AndFollowCountsThatUse()
        {
            LegacySkill counter = LegacySkillDefinitions.Skill(21);
            var duel = new LegacyQueuedDuel(100000, 50, 400, 50, new[] { Attack(1, 1, 0) },
                GreenRibbonBossBattle.CreateScript(), 1, playerCounter: new LegacyCounter(counter)).EnableIiaBossPatterns();
            Assert.That(duel.TrySetFixedQueues(new[] { LegacyCommonActions.Breathe }, new[] { LegacyCommonActions.Breathe, Attack(1, 1, 0) }), Is.True);
            Resolve(duel); duel.BeginNextTurn();
            Assert.That(duel.CycleUses(21), Is.EqualTo(1));
            Assert.That(duel.TrySetFixedQueues(new[] { LegacyCommonActions.Breathe },
                new[] { LegacyCommonActions.Breathe, LegacySkillDefinitions.Skill(504) }), Is.True);
            ((ILegacyDuelView)duel).EnemyEffectivePowerRange(duel.EnemyQueue[1], out int min, out int max);
            Assert.That(min, Is.EqualTo(26));
            Assert.That(max, Is.EqualTo(min));
            duel.Commit(); duel.ResolveNextSlot(); duel.BeginNextSlot();
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerResistanceDamage, Is.EqualTo(26));
            ((ILegacyDuelView)duel).EnemyEffectivePowerRange(duel.EnemyQueue[1], out min, out max);
            Assert.That(max, Is.EqualTo(26), "The mirrored value stays at the counter's count before this use.");
            duel.CompleteCurrentSlot(); duel.BeginNextTurn();
            Assert.That(duel.PlayerSkillsUsedPreviousTurn, Is.EqualTo(1));
        }

        [Test]
        public void MirrorForecastRespectsCounterUses_AndCancelingACounterLeavesNoPowerToCopy()
        {
            LegacySkill mirror = LegacySkillDefinitions.Skill(504);
            var duel = new LegacyQueuedDuel(100000, 50, 400, 50, new[] { Attack(1, 1, 0) },
                GreenRibbonBossBattle.CreateScript(), 1,
                playerCounter: new LegacyCounter(LegacySkillDefinitions.Skill(21))).EnableIiaBossPatterns();
            Assert.That(duel.TrySetFixedQueues(new[] { LegacyCommonActions.Breathe },
                new[] { LegacyCommonActions.Breathe, mirror, mirror }), Is.True);
            ((ILegacyDuelView)duel).EnemyEffectivePowerRange(duel.EnemyQueue[1], out _, out int first);
            ((ILegacyDuelView)duel).EnemyEffectivePowerRange(duel.EnemyQueue[2], out _, out int second);
            Assert.That(first, Is.EqualTo(22));
            Assert.That(second, Is.Zero, "One counter cannot answer both empty slots.");
            duel.Commit(); duel.ResolveNextSlot(); duel.BeginNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, false, out bool success), Is.True);
            Assert.That(success, Is.False);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerDisplayedDamage, Is.Zero, "Even a failed dodge forgoes the pending technique.");
            Assert.That(duel.PlayerCountersRemaining, Is.EqualTo(1));
            duel.CompleteCurrentSlot(); duel.ResolveNextSlot(); duel.BeginNextTurn();
            Assert.That(duel.PlayerSkillsUsedPreviousTurn, Is.EqualTo(1), "Only the later counter actually executed.");
        }

        [Test]
        public void ResetClearsBossStateAndTheThirtyTurnLimitStillEndsAStalledFight()
        {
            LegacyQueuedDuel duel = Boss();
            for (int turn = 1; turn <= 30; turn++)
            {
                Assert.That(duel.TrySetFixedQueues(new[] { Guard(100) }, new[] { Guard(100) }), Is.True);
                Resolve(duel);
                if (turn < 30) duel.BeginNextTurn();
            }
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.Draw));
            duel.Reset();
            Assert.That(duel.IsIiaBossBattle, Is.True);
            Assert.That(duel.IiaPhase, Is.EqualTo(1));
            Assert.That(duel.RoundNumber, Is.EqualTo(1));
            Assert.That(duel.IiaJealousy, Is.Zero);
            Assert.That(duel.PlayerFixedTurnsRemaining, Is.Zero);
            Assert.That(duel.PlayerSkillsUsedPreviousTurn, Is.Zero);
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo("43,503"));
        }

        private static LegacyQueuedDuel Boss(int seed = 1, IReadOnlyList<LegacySkill> player = null)
            => new LegacyQueuedDuel(100000, 50, 400, 50,
                player ?? new[] { Attack(1, 1, 0) }, GreenRibbonBossBattle.CreateScript(), seed,
                roundLimit: GreenRibbonBossBattle.RoundLimit).EnableIiaBossPatterns();

        private static LegacySkill Attack(int power, int hits, int lane)
            => new LegacySkill(999, "검사 공격", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, lane, "");
        private static LegacySkill Guard(int power)
            => new LegacySkill(998, "검사 방어", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");
        private static string Ids(IEnumerable<LegacySkill> skills) => string.Join(",", skills.Select(skill => skill.Id));
        private static void Resolve(LegacyQueuedDuel duel)
        {
            duel.Commit();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
        }
    }
}
