using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Core.Tests
{
    /// <summary>맞물림: neighbouring queued player skills of different schools (lanes) form chains whose skills gain power
    /// and take no steps.</summary>
    public sealed class MeshingTests
    {
        // ---- Chains ------------------------------------------------------------------------------------------------

        // Q/W/E queue a skill of that lane, S a 숨고르기; the expected digits are each slot's chain length (0: not meshed).
        [TestCase("Q", "0")]
        [TestCase("QQQ", "000")]
        [TestCase("QE", "22")]
        [TestCase("QQW", "022")]
        [TestCase("QWE", "333")]
        [TestCase("QQWEE", "03330")]
        [TestCase("QEQE", "4444")]
        [TestCase("QWEQWE", "666666")]
        [TestCase("QEEQ", "2222")]
        [TestCase("QSE", "000")]
        [TestCase("QESEQ", "22022")]
        [TestCase("SQWS", "0220")]
        [TestCase("WWEEQQ", "022220")]
        public void Chains_AreMaximalRunsOfNeighboursFromDifferentSchools(string queue, string lengths)
        {
            IReadOnlyList<LegacySkill> skills = Queue(queue);
            for (int slot = 0; slot < skills.Count; slot++)
            {
                LegacyMeshSlot mesh = LegacyMeshing.Find(skills, slot, 20);
                int expected = lengths[slot] - '0';
                Assert.That(mesh.SlotIndex, Is.EqualTo(slot));
                Assert.That(mesh.ChainLength, Is.EqualTo(expected), queue + " slot " + slot);
                Assert.That(mesh.IsMeshed, Is.EqualTo(expected >= 2), queue + " slot " + slot);
                Assert.That(mesh.BonusPercent, Is.EqualTo(expected * 20), "Every skill of an N-skill chain gains N × 20%.");
            }
        }

        [Test]
        public void Chains_KnowWhereTheyStartAndWhichNeighboursTheyTurn()
        {
            // The author's example: 정공1 - 정공2 - 강공1 - 기교1 - 기교2 → 정공2, 강공1 and 기교1 mesh.
            IReadOnlyList<LegacySkill> skills = Queue("QQWEE");
            LegacyMeshSlot[] mesh = Enumerable.Range(0, skills.Count).Select(slot => LegacyMeshing.Find(skills, slot, 20)).ToArray();
            Assert.That(mesh.Select(slot => slot.ChainStart), Is.EqualTo(new[] { 0, 1, 1, 1, 4 }));
            Assert.That(mesh.Select(slot => slot.ChainEnd), Is.EqualTo(new[] { 0, 3, 3, 3, 4 }));
            Assert.That(mesh.Select(slot => slot.MeshesWithPrevious), Is.EqualTo(new[] { false, false, true, true, false }));
            Assert.That(mesh.Select(slot => slot.MeshesWithNext), Is.EqualTo(new[] { false, true, true, false, false }));
            Assert.That(mesh[2].BonusPercent, Is.EqualTo(60));
        }

        [Test]
        public void Chains_IgnoreEmptySlotsAndSwitchOffAtZeroPercent()
        {
            IReadOnlyList<LegacySkill> skills = Queue("QE");
            Assert.That(LegacyMeshing.Find(skills, 2, 20).IsMeshed, Is.False, "A slot past the queue is empty.");
            Assert.That(LegacyMeshing.Find(skills, -1, 20).IsMeshed, Is.False);
            Assert.That(LegacyMeshing.Find(null, 0, 20).IsMeshed, Is.False);
            Assert.That(LegacyMeshing.Find(skills, 0, 0).IsMeshed, Is.False, "0 switches 맞물림 off.");
            Assert.That(LegacyMeshing.Find(skills, 0, 0).BonusPercent, Is.Zero);
            Assert.That(LegacyMeshing.Find(skills, 0, 25).BonusPercent, Is.EqualTo(50), "The percent is per skill in the chain.");
            Assert.That(LegacyMeshing.School(LegacyCommonActions.Breathe), Is.EqualTo(LegacyMeshing.NoSchool));
            Assert.That(LegacyMeshing.School(null), Is.EqualTo(LegacyMeshing.NoSchool));
            Assert.That(LegacyMeshing.Meshes(Skill('W'), Skill('E')), Is.True);
            Assert.That(LegacyMeshing.Meshes(Skill('W'), Skill('W')), Is.False);
            Assert.That(LegacyMeshing.Meshes(Skill('W'), LegacyCommonActions.Breathe), Is.False);
            Assert.That(LegacyMeshing.BonusPercent(1, 20), Is.Zero, "A lone skill is not a chain.");
            Assert.That(LegacyMeshing.BonusPercent(4, 20), Is.EqualTo(80));
        }

        [TestCase("", 0, 0)]
        [TestCase("Q", 0, 0)]
        [TestCase("Q", 2, 2)]
        [TestCase("QE", 0, 3)]
        [TestCase("QE", 2, 0)]
        [TestCase("QQ", 1, 2)]
        [TestCase("QS", 2, 0)]
        [TestCase("QEQW", 2, 5)]
        public void FindIfAppended_IsTheChainTheNextQueuedSkillWouldJoin(string queue, int school, int expectedLength)
        {
            IReadOnlyList<LegacySkill> skills = Queue(queue);
            LegacyMeshSlot forecast = LegacyMeshing.FindIfAppended(skills, school, 20);
            Assert.That(forecast.SlotIndex, Is.EqualTo(skills.Count));
            Assert.That(forecast.ChainLength, Is.EqualTo(expectedLength));
            var appended = new List<LegacySkill>(skills) { Skill("QWE"[school]) };
            Assert.That(forecast, Is.EqualTo(LegacyMeshing.Find(appended, skills.Count, 20)), "The forecast matches queueing it.");
        }

        // ---- Live queue ----------------------------------------------------------------------------------------------

        [Test]
        public void PlanningQueue_MeshesLiveAsSkillsAreQueued_AndTheCommittedQueueKeepsIt()
        {
            var duel = Duel(new[] { Attack(100, 0, 3), Attack(101, 0, 3), Attack(102, 1, 3), Attack(103, 2, 3) }, cost: 0);
            Assert.That(duel.MeshPercent, Is.EqualTo(LegacyMeshing.DefaultPercent).And.EqualTo(20), "The Runtime default.");
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
            Assert.That(Lengths(duel, 2), Is.EqualTo(new[] { 0, 0 }), "One school never meshes.");
            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(Lengths(duel, 3), Is.EqualTo(new[] { 0, 2, 2 }), "정공 - 정공 - 강공: the last two mesh.");
            Assert.That(duel.PlayerMesh(2).BonusPercent, Is.EqualTo(40));
            Assert.That(duel.TryQueueLane(2), Is.True);
            Assert.That(Lengths(duel, 4), Is.EqualTo(new[] { 0, 3, 3, 3 }), "기교 extends the chain; every member updates.");
            Assert.That(duel.TryQueueBreath() && duel.TryQueueLane(0), Is.True);
            Assert.That(Lengths(duel, 6), Is.EqualTo(new[] { 0, 3, 3, 3, 0, 0 }), "숨고르기 breaks the chain.");
            Assert.That(duel.TryCycleLanes(), Is.True);
            Assert.That(Lengths(duel, 6), Is.EqualTo(new[] { 0, 3, 3, 3, 0, 0 }), "넘기기 never touches the queue.");

            duel.Commit();
            Assert.That(Lengths(duel, 6), Is.EqualTo(new[] { 0, 3, 3, 3, 0, 0 }), "Resolution reads the committed queue.");
            LegacySlotResult first = duel.ResolveNextSlot();
            Assert.That(first.SlotIndex, Is.Zero);
            LegacyCurrentSlot second = duel.BeginNextSlot();
            Assert.That(second.PlayerMesh, Is.EqualTo(duel.PlayerMesh(1)));
            Assert.That(duel.IsCurrentSlotMeshed, Is.True);
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            Assert.That(duel.IsCurrentSlotMeshed, Is.False);
            duel.BeginNextTurn();
            Assert.That(duel.PlayerQueue, Is.Empty);
            Assert.That(duel.PlayerMesh(0).IsMeshed, Is.False, "A new turn starts a new queue.");
            Assert.That(duel.TryQueueLane(2) && duel.TryQueueLane(0), Is.True);
            Assert.That(Lengths(duel, 2), Is.EqualTo(new[] { 2, 2 }));
            duel.Reset();
            Assert.That(duel.PlayerMesh(0).IsMeshed, Is.False, "A new match starts empty.");
        }

        [Test]
        public void PlayerMeshIfQueued_ForecastsTheLaneFronts_AndFollowsCyclingAndAct()
        {
            // E holds a cheap skill and a costly one; 넘기기 brings the costly one forward.
            var duel = Duel(new[] { Attack(100, 0, 3, cost: 1), Attack(101, 2, 3, cost: 1), Attack(102, 2, 3, cost: 9) });
            Assert.That(duel.PlayerMeshIfQueued(0).IsMeshed, Is.False, "Nothing to mesh with yet.");
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.PlayerMeshIfQueued(0).IsMeshed, Is.False, "The same school would not mesh.");
            LegacyMeshSlot forecast = duel.PlayerMeshIfQueued(2);
            Assert.That(forecast.IsMeshed, Is.True);
            Assert.That(forecast.SlotIndex, Is.EqualTo(1));
            Assert.That(forecast.ChainStart, Is.Zero);
            Assert.That(forecast.BonusPercent, Is.EqualTo(40));
            Assert.That(duel.PlayerMeshIfQueued(1).IsMeshed, Is.False, "W holds nothing.");
            Assert.That(duel.TryCycleLanes(), Is.True);
            Assert.That(duel.GetLane(2)[0].Id, Is.EqualTo(102));
            Assert.That(duel.PlayerMeshIfQueued(2).IsMeshed, Is.False, "The new front costs more ACT than is left.");
            Assert.That(duel.TryCycleLanes(), Is.True);
            Assert.That(duel.PlayerMeshIfQueued(2), Is.EqualTo(forecast));
            Assert.That(duel.TryQueueLane(2), Is.True);
            Assert.That(duel.PlayerMesh(1), Is.EqualTo(forecast), "Queueing it gives the forecast chain.");
            Assert.That(duel.PlayerMeshIfQueued(0).ChainLength, Is.EqualTo(3));
            duel.Commit();
            Assert.That(duel.PlayerMeshIfQueued(0).IsMeshed, Is.False, "Only while planning.");
        }

        // ---- Power ---------------------------------------------------------------------------------------------------

        [TestCase(20, "QE", 50, 14)]
        [TestCase(20, "QEW", 50, 16)]
        [TestCase(20, "QEQE", 50, 18)]
        [TestCase(25, "QE", 50, 15)]
        [TestCase(0, "QEW", 50, 10)]
        [TestCase(20, "QQ", 50, 10)]
        [TestCase(20, "QE", 0, 28)]
        public void MeshedAttacks_GainChainLengthTimesThePercentOfPower(int percent, string queue, int enemyResistance,
            int damagePerSkill)
        {
            // Power 10 one-sided attacks against a passive enemy reach health; a broken enemy (resistance 0) takes double.
            var player = new List<LegacySkill>();
            for (int i = 0; i < queue.Length; i++) player.Add(Attack(100 + i, Lane(queue[i]), 10));
            var duel = Duel(player.ToArray(), percent: percent, enemyResistance: enemyResistance, cost: 0);
            foreach (char lane in queue) Assert.That(duel.TryQueueLane(Lane(lane)), Is.True);
            duel.Commit();
            for (int slot = 0; slot < queue.Length; slot++)
                Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(damagePerSkill), "slot " + slot);
        }

        [Test]
        public void MeshedMultiHitPower_FloorsTheBoostedTotalAcrossItsHits()
        {
            // 11 × 1.4 = 15.4: the slot's power is floor(15.4 / 3) = 5 a hit, where it was floor(11 / 3) = 3.
            var duel = Duel(new[] { Attack(100, 0, 11, hits: 3), Attack(101, 2, 10) }, cost: 0);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(2), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            var damage = new List<int>();
            while (!duel.IsCurrentSlotResolved) damage.Add(duel.ResolveNextHit().EnemyHealthDamage);
            Assert.That(damage, Is.EqualTo(new[] { 5, 5, 5 }));
            duel.CompleteCurrentSlot();
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(14));
        }

        [TestCase(20, 6)]
        [TestCase(0, 10)]
        public void MeshedGuard_GainsDefencePowerToo(int percent, int damageTaken)
        {
            // A guard of 10 (14 when meshed) against an enemy strike of 20; the guard's partner is a weak 기교 attack.
            var enemy = new[] { Attack(200, 0, 20) };
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Guard(100, 0, 10, cost: 0), Attack(101, 2, 1, cost: 0) },
                enemy, new[] { 1 }, 5, meshPercent: percent);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(2), Is.True);
            duel.Commit();
            LegacySlotResult guarded = duel.ResolveNextSlot();
            Assert.That(guarded.PlayerHealthDamage, Is.EqualTo(damageTaken));
        }

        [TestCase(20, 40)]
        [TestCase(0, 30)]
        public void MeshBonus_AddsIntoTheSamePercentAsPowerBuffs(int percent, int damage)
        {
            // 쳐내기 (E) grants +20% power for two slots; the next 정공 meshes with it: 25 × (100 + 20 + 40)% = 40, not
            // 25 × 1.2 × 1.4 = 42. The feedback keeps reporting the buff alone; the slot reports the mesh beside it.
            LegacySkill parry = LegacySkillDefinitions.Skill(9);
            Assume.That(LegacySkillDefinitions.Find(parry).Effect.BuffPowerPercent, Is.EqualTo(20));
            Assume.That(parry.LaneIndex, Is.EqualTo(2));
            var duel = Duel(new[] { parry, Attack(100, 0, 25, cost: 0) }, percent: percent);
            Assert.That(duel.TryQueueLane(2) && duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.PowerBuffPercent, Is.EqualTo(20));
            Assert.That(slot.PlayerMesh.BonusPercent, Is.EqualTo(percent == 0 ? 0 : 40));
            while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
            Assert.That(duel.CompleteCurrentSlot().EnemyHealthDamage, Is.EqualTo(damage));
        }

        [Test]
        public void CopiesOfTableSkills_MeshByTheirLane_KeepTheirEffects_AndEnemyStageCopiesNeverMesh()
        {
            // Stage copies keep a table id with stronger numbers (CampaignRun builds them for enemies); the player's copies
            // of 베기 (Q, ACT +1) and 기세 꺾기 (E) mesh by their lanes and keep their table effects.
            LegacySkill slash = Copy(LegacySkillDefinitions.Skill(1), 9);
            LegacySkill breaker = Copy(LegacySkillDefinitions.Skill(5), 9);
            Assume.That(slash.LaneIndex, Is.EqualTo(0));
            Assume.That(breaker.LaneIndex, Is.EqualTo(2));
            var enemy = new[] { Copy(LegacySkillDefinitions.Skill(1), 6), Copy(LegacySkillDefinitions.Skill(5), 6) };
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 0, new[] { slash, breaker }, enemy, new[] { 2 }, 5);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(2), Is.True);
            Assert.That(Lengths(duel, 2), Is.EqualTo(new[] { 2, 2 }));
            int nextActGain = duel.NextActGain;
            duel.Commit();
            LegacyCurrentSlot first = duel.BeginNextSlot();
            Assert.That(first.PlayerFeedback.ActGainGranted, Is.EqualTo(1), "베기's effect still applies.");
            Assert.That(duel.NextActGain, Is.EqualTo(nextActGain + 1));
            Assert.That(first.EnemyFeedback.PowerBuffPercent, Is.Zero);
            // The enemy's alternating Q/E copies never mesh: its 6-power strike lands as 6 on the player's resistance,
            // while the player's meshed 9 (12 a hit) breaks through the enemy's broken guard twice over.
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerMesh.IsMeshed, Is.True);
            Assert.That(hit.PlayerResistanceDamage, Is.EqualTo(6));
            Assert.That(hit.EnemyHealthDamage, Is.EqualTo(24), "floor(9 × 1.4) = 12, doubled on the broken enemy.");
        }

        [Test]
        public void EnemyQueue_NeverMeshes()
        {
            // The enemy alternates Q and E; the player queues nothing. Both one-sided strikes land at their plain power.
            var enemy = new[] { Attack(200, 0, 10), Attack(201, 2, 10) };
            foreach (int percent in new[] { 0, 20 })
            {
                var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Attack(100, 0, 1) }, enemy, new[] { 2 }, 5,
                    meshPercent: percent);
                duel.Commit();
                Assert.That(duel.PlayerMesh(0).IsMeshed || duel.PlayerMesh(1).IsMeshed, Is.False);
                Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.EqualTo(10));
                Assert.That(duel.ResolveNextSlot().PlayerHealthDamage, Is.EqualTo(10));
            }
        }

        // ---- Steps ---------------------------------------------------------------------------------------------------

        [Test]
        public void MeshedSlots_IgnoreStepInputs_WithoutAnAttemptAMissOrAnyEffect()
        {
            // 정공 - 기교 mesh; after a 숨고르기 a lone 정공 does not. The enemy strikes in every slot.
            var enemy = new[] { Attack(200, 0, 5) };
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50,
                new[] { Attack(100, 0, 4, cost: 0), Attack(101, 2, 4, cost: 0), Attack(102, 0, 4, cost: 0) },
                enemy, new[] { 4 }, 5);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(2) && duel.TryQueueBreath() && duel.TryQueueLane(0), Is.True);
            duel.Commit();

            LegacyCurrentSlot meshed = duel.BeginNextSlot();
            Assert.That(duel.IsCurrentSlotMeshed, Is.True);
            foreach (bool timing in new[] { true, false })
                foreach (LegacyStepAction action in new[] { LegacyStepAction.Dodge, LegacyStepAction.Pressure })
                {
                    Assert.That(duel.TryStep(action, timing, out bool success), Is.False, action + " is ignored.");
                    Assert.That(success, Is.False);
                }
            Assert.That(duel.StepAttemptsThisTurn, Is.Zero, "Not an attempt: the window does not narrow.");
            Assert.That(duel.StepMissedThisTurn, Is.False, "Not a miss: no ACT penalty.");
            Assert.That(duel.StepSuccessStreak, Is.Zero);
            Assert.That(meshed.DodgeAttempted || meshed.DodgeSucceeded || meshed.PressureSucceeded, Is.False);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerDodged || hit.PlayerPressured, Is.False);
            Assert.That(hit.PlayerHealthDamage + hit.PlayerResistanceDamage, Is.GreaterThan(0), "The strike was not dodged.");
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out _), Is.False, "Still ignored after the first hit.");
            duel.CompleteCurrentSlot();

            duel.BeginNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out _), Is.False, "The whole chain takes no steps.");
            duel.ResolveNextSlot();

            duel.BeginNextSlot();
            Assert.That(duel.IsCurrentSlotMeshed, Is.False, "숨고르기 is not meshed.");
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool dodged), Is.True);
            Assert.That(dodged, Is.True);
            Assert.That(duel.StepAttemptsThisTurn, Is.EqualTo(1));
            duel.ResolveNextSlot();

            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool gap), Is.True, "Between slots a step still counts.");
            Assert.That(gap, Is.False);
            duel.BeginNextSlot();
            Assert.That(duel.IsCurrentSlotMeshed, Is.False, "The lone 정공 after the breath.");
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out bool pressed), Is.True);
            Assert.That(pressed, Is.True);
        }

        [Test]
        public void MeshedSlots_LeaveTheNextTurnsActWhole()
        {
            var enemy = new[] { Attack(200, 0, 1) };
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Attack(100, 0, 1, cost: 1), Attack(101, 2, 1, cost: 1) },
                enemy, new[] { 2 }, 5);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(2), Is.True);
            int act = duel.Act;
            duel.Commit();
            while (!duel.IsTurnResolved)
            {
                duel.BeginNextSlot();
                Assert.That(duel.TryStep(LegacyStepAction.Dodge, false, out _), Is.False, "A mistimed press in a meshed slot.");
                duel.ResolveNextSlot();
            }
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(act + duel.PlayerBaseActGain), "Ignored presses cost no recovery.");
        }

        [Test]
        public void CounterInAnEmptySlot_NeverMeshes_AndKeepsItsSteps()
        {
            // The queue's 정공 - 기교 chain ends before the third slot, which the player's counter fills.
            var counter = new LegacyCounter(Attack(300, 0, 10), 1);
            var enemy = new[] { Attack(200, 0, 1) };
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Attack(100, 0, 1, cost: 0), Attack(101, 2, 1, cost: 0) },
                enemy, new[] { 3 }, 5, playerCounter: counter);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(2), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            duel.ResolveNextSlot();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PendingPlayerCounter, Is.Not.Null);
            Assert.That(duel.IsCurrentSlotMeshed, Is.False);
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out bool pressed), Is.True);
            Assert.That(pressed, Is.True, "Pressure may back the counter as before.");
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.PlayerSkill.Id, Is.EqualTo(300));
            Assert.That(hit.PlayerMesh.IsMeshed, Is.False);
            Assert.That(hit.EnemyResistanceDamage, Is.EqualTo(10), "The counter clashes at its plain 10, not 14.");
        }

        // ---- Switch and availability ---------------------------------------------------------------------------------

        [Test]
        public void PercentZero_SwitchesMeshingOff_StepsAndPowerAsBefore()
        {
            var enemy = new[] { Attack(200, 0, 5) };
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Attack(100, 0, 10, cost: 0), Attack(101, 2, 10, cost: 0) },
                enemy, new[] { 2 }, 5, meshPercent: 0);
            Assert.That(duel.MeshPercent, Is.Zero);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(2), Is.True);
            Assert.That(duel.PlayerMesh(0).IsMeshed || duel.PlayerMesh(1).IsMeshed || duel.PlayerMeshIfQueued(0).IsMeshed, Is.False);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.IsCurrentSlotMeshed, Is.False);
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, false, out _), Is.True, "Steps count as before.");
            Assert.That(duel.StepMissedThisTurn, Is.True);
            Assert.That(duel.ResolveNextSlot().EnemyResistanceDamage, Is.EqualTo(10), "Plain power.");
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyQueuedDuel(100, 50, 100, 50, new[] { Attack(100, 0, 1) },
                enemy, new[] { 1 }, 5, meshPercent: -1));
        }

        [Test]
        public void PercentZero_PlaysEveryHitLikeTheSameSkillsFromOneLane()
        {
            // Table skills (their effects, power rolls and hit counts) queued 정공 - 기교 - 강공 - 숨고르기 - 기교 against a
            // five-action enemy, with steps pressed in every slot. Lanes matter to nothing but 맞물림, so at 0 the mixed
            // queue must play hit for hit like the same skills queued from one lane; at 20 the 3-chain changes it.
            int[] ids = { 1, 5, 3, 9 };
            int[] lanes = { 0, 2, 1, 2 };
            Assume.That(ids.Select(id => LegacySkillDefinitions.Skill(id).LaneIndex), Is.EqualTo(lanes));
            LegacySkill[] mixed = ids.Select((id, i) => InLane(LegacySkillDefinitions.Skill(id), lanes[i])).ToArray();
            LegacySkill[] flat = ids.Select(id => InLane(LegacySkillDefinitions.Skill(id), 0)).ToArray();
            char[] mixedKeys = { 'Q', 'E', 'W', 'S', 'E' };
            char[] flatKeys = { 'Q', 'Q', 'Q', 'S', 'Q' };

            List<string> mixedOff = Play(mixed, mixedKeys, 0);
            Assert.That(mixedOff, Is.EqualTo(Play(flat, flatKeys, 0)), "At 0 nothing meshes: power and steps as before.");
            Assert.That(Play(flat, flatKeys, 20), Is.EqualTo(mixedOff), "One school never meshes, whatever the percent.");
            Assert.That(Play(mixed, mixedKeys, 20), Is.Not.EqualTo(mixedOff), "The 3-chain meshes at 20.");
        }

        [Test]
        public void StoryDuels_MeshFromMissionFive_WhereTheSecondLaneOpens()
        {
            foreach (PrologueMission mission in StoryMissions.All.Where(mission => mission.Number < 5))
            {
                Assert.That(mission.LaneCount, Is.EqualTo(1), mission.Title);
                LegacyQueuedDuel duel = mission.CreateDuel(1);
                Assert.That(duel.MeshPercent, Is.EqualTo(LegacyMeshing.DefaultPercent));
                for (int queued = 0; queued < 10 && duel.TryQueueLane(0); queued++) { }
                for (int slot = 0; slot < duel.PlayerQueue.Count; slot++)
                    Assert.That(duel.PlayerMesh(slot).IsMeshed, Is.False, mission.Title + " has one school only.");
            }
            PrologueMission five = StoryMissions.Get(5);
            Assert.That(five.LaneCount, Is.EqualTo(2));
            LegacyQueuedDuel meshing = five.CreateDuel(1);
            Assert.That(meshing.TryQueueLane(0) && meshing.TryQueueLane(2), Is.True);
            Assert.That(meshing.PlayerMesh(1).BonusPercent, Is.EqualTo(40));
            LegacyQueuedDuel off = five.CreateDuel(1, meshPercent: 0);
            Assert.That(off.TryQueueLane(0) && off.TryQueueLane(2), Is.True);
            Assert.That(off.PlayerMesh(1).IsMeshed, Is.False, "The percent passes through.");
            Assert.That(five.CreateGuide().Beats.Any(beat => beat.Description.Contains("맞물려")), Is.True,
                "Mission 5's coach tells that mixing the lanes meshes the gears.");
        }

        [Test]
        public void CampaignDuels_TakeThePercent()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.CreateDuel(1).MeshPercent, Is.EqualTo(LegacyMeshing.DefaultPercent));
            Assert.That(run.CreateDuel(1, 35).MeshPercent, Is.EqualTo(35));
            Assert.That(run.CreateDuel(1, 0).MeshPercent, Is.Zero);

            // The lobby opens with the 서막's Q lane and 넘기기 only: stage duels have one school until mission 5 opens E.
            run.SetProgression(CombatFeature.LaneQ | CombatFeature.Cycle, 1);
            LegacyQueuedDuel oneLane = run.CreateDuel(1);
            Assert.That(oneLane.TryQueueLane(0), Is.True);
            Assert.That(oneLane.TryQueueLane(2), Is.False);
            Assert.That(oneLane.PlayerMesh(0).IsMeshed || oneLane.PlayerMeshIfQueued(2).IsMeshed, Is.False);
            run.SetProgression(CombatFeature.LaneQ | CombatFeature.Cycle | CombatFeature.LaneE, 1);
            LegacyQueuedDuel twoLanes = run.CreateDuel(1);
            Assert.That(twoLanes.TryQueueLane(0), Is.True);
            Assert.That(twoLanes.PlayerMeshIfQueued(2).BonusPercent, Is.EqualTo(40));
        }

        // ---- Helpers -------------------------------------------------------------------------------------------------

        private static int Lane(char key) => key == 'Q' ? 0 : key == 'W' ? 1 : key == 'E' ? 2 : -1;

        private static LegacySkill Skill(char key) => key == 'S' ? LegacyCommonActions.Breathe : Attack(100, Lane(key), 5);

        private static IReadOnlyList<LegacySkill> Queue(string keys) => keys.Select(Skill).ToArray();

        private static int[] Lengths(LegacyQueuedDuel duel, int count)
            => Enumerable.Range(0, count).Select(slot => duel.PlayerMesh(slot).ChainLength).ToArray();

        // A passive enemy: one-sided attacks reach its health.
        private static LegacyQueuedDuel Duel(LegacySkill[] player, int percent = LegacyMeshing.DefaultPercent,
            int enemyResistance = 50, int cost = -1)
        {
            if (cost >= 0)
                player = player.Select(skill => new LegacySkill(skill.Id, skill.Name, cost, skill.MinPower, skill.MaxPower,
                    skill.Kind, skill.Property, skill.AttackCount, skill.LaneIndex, skill.Description)).ToArray();
            return new LegacyQueuedDuel(1000, 50, 1000, enemyResistance, player, Array.Empty<LegacySkill>(), new[] { 0 }, 5,
                meshPercent: percent);
        }

        private static LegacySkill Attack(int id, int lane, int power, int hits = 1, int cost = 1)
            => new LegacySkill(id, "attack", cost, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, lane, "");

        private static LegacySkill Guard(int id, int lane, int power, int cost = 1)
            => new LegacySkill(id, "guard", cost, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, lane, "");

        // A copy with a table id and fixed, stronger power, as stage copies are.
        private static LegacySkill Copy(LegacySkill skill, int power)
            => new LegacySkill(skill.Id, skill.Name, 0, power, power, skill.Kind, skill.Property, skill.AttackCount,
                skill.LaneIndex, skill.Description, skill.AnimationName, skill.IconId);

        // A free copy of a table skill (its id, power range and hits) placed in another lane.
        private static LegacySkill InLane(LegacySkill skill, int lane)
            => new LegacySkill(skill.Id, skill.Name, 0, skill.MinPower, skill.MaxPower, skill.Kind, skill.Property,
                skill.AttackCount, lane, skill.Description, skill.AnimationName, skill.IconId);

        // Three turns of the same queue (Q/W/E a lane, S a 숨고르기) against table skills, pressing A on even slots and D on
        // odd ones before the first hit: every step answer, hit, effect and the ACT each turn ends with, as text.
        private static List<string> Play(LegacySkill[] player, char[] keys, int percent)
        {
            LegacySkill[] enemy = new[] { 1, 7, 5, 2, 9 }.Select(LegacySkillDefinitions.Skill).ToArray();
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, player, enemy, new[] { 5 }, 11, meshPercent: percent);
            var log = new List<string>();
            for (int turn = 0; turn < 3; turn++)
            {
                foreach (char key in keys)
                    Assert.That(key == 'S' ? duel.TryQueueBreath() : duel.TryQueueLane(Lane(key)), Is.True, key.ToString());
                duel.Commit();
                while (!duel.IsTurnResolved)
                {
                    LegacyCurrentSlot slot = duel.BeginNextSlot();
                    LegacyStepAction action = slot.SlotIndex % 2 == 0 ? LegacyStepAction.Dodge : LegacyStepAction.Pressure;
                    bool tried = duel.TryStep(action, true, out bool success);
                    log.Add($"slot {slot.SlotIndex}: {action} {tried}/{success}, buff {slot.PlayerFeedback.PowerBuffPercent}, " +
                        $"act +{slot.PlayerFeedback.ActGainGranted}, cut {slot.PlayerFeedback.OpponentResistanceReduced}");
                    while (!duel.IsCurrentSlotResolved)
                    {
                        LegacyHitResult hit = duel.ResolveNextHit();
                        log.Add($"  hit {hit.HitIndex}: {hit.PlayerSkill?.Id}/{hit.EnemySkill?.Id} " +
                            $"health {hit.PlayerHealthDamage}/{hit.EnemyHealthDamage} " +
                            $"resistance {hit.PlayerResistanceDamage}/{hit.EnemyResistanceDamage} " +
                            $"shown {hit.PlayerDisplayedDamage}/{hit.EnemyDisplayedDamage} {hit.PlayerDodged} {hit.PlayerPressured}");
                    }
                    duel.CompleteCurrentSlot();
                }
                log.Add($"turn {turn}: steps {duel.StepAttemptsThisTurn}, missed {duel.StepMissedThisTurn}");
                duel.BeginNextTurn();
                log.Add($"act {duel.Act}");
            }
            return log;
        }
    }
}
