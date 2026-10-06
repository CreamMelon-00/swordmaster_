using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    /// <summary>상대 붕괴 (the sheet's break effect) in combat, through 라우다레 (500), 이아's enemy-only 수훈 technique:
    /// 30~35 over five slashes, so every hit carries 6 or 7.</summary>
    public sealed class OpponentBreakTests
    {
        private const int Laudare = 500;

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Laudare_BreaksTheOpponentAsItsSlotStarts_SoEveryClashHitDealsDoubleHealthDamage(int seed)
        {
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { Probe(900, 4) },
                new[] { LegacySkillDefinitions.Skill(Laudare) }, new[] { 1 }, seed);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(duel.Player.Resistance, Is.Zero, "Broken before any hit.");
            Assert.That(duel.Player.IsResistanceBroken, Is.True);
            Assert.That(slot.EnemyFeedback.OpponentBroken, Is.True);
            Assert.That(slot.EnemyFeedback.OpponentResistanceReduced, Is.EqualTo(50));
            Assert.That(slot.EnemyFeedback.EffectActivated, Is.True);
            Assert.That(slot.EnemyFeedback.ConditionMet, Is.False, "There is no condition to meet.");
            Assert.That(slot.PlayerFeedback.OpponentBroken, Is.False);

            List<LegacyHitResult> hits = ResolveSlot(duel);
            Assert.That(hits.Count, Is.EqualTo(5));
            int power = hits[0].PlayerPushPower;
            Assert.That(power, Is.InRange(6, 7));
            foreach (LegacyHitResult hit in hits)
            {
                Assert.That(hit.EnemyAttacked, Is.True);
                Assert.That(hit.PlayerPushPower, Is.EqualTo(power), "One roll split over the hits.");
                Assert.That(hit.PlayerResistanceDamage, Is.Zero, "A clash against a broken fighter goes to health.");
                Assert.That(hit.PlayerHealthDamage, Is.EqualTo(2 * power));
            }
            Assert.That(duel.Player.Health, Is.EqualTo(100 - 10 * power));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(46), "The player's own slash still clashes.");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Laudare_AgainstAGuard_StillBreaks_AndTheGuardOnlyTrimsEachHit(int seed)
        {
            // A guard of 10 takes 10 / 5 = 2 off each of the five hits; the rest lands doubled.
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { Guard(901, 10) },
                new[] { LegacySkillDefinitions.Skill(Laudare) }, new[] { 1 }, seed);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().EnemyFeedback.OpponentBroken, Is.True);
            foreach (LegacyHitResult hit in ResolveSlot(duel))
            {
                Assert.That(hit.PlayerPushPower, Is.InRange(4, 5));
                Assert.That(hit.PlayerHealthDamage, Is.EqualTo(2 * hit.PlayerPushPower));
            }
            Assert.That(duel.Player.Resistance, Is.Zero);
        }

        [Test]
        public void ABreak_RecoversLikeAHitBreak_AndAnAlreadyBrokenOpponentLosesNothingMore()
        {
            var broken = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Probe(900, 1) },
                new[] { LegacySkillDefinitions.Skill(Laudare) }, new[] { 1 }, 4);
            // The same turns against a plain 60-power slash that breaks the player by a clash.
            var hitBroken = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Probe(900, 1) },
                new[] { Probe(902, 60) }, new[] { 1 }, 4);
            var atTurnStart = new List<int>();
            var hitAtTurnStart = new List<int>();
            for (int turn = 1; turn <= 3; turn++)
            {
                atTurnStart.Add(broken.Player.Resistance);
                hitAtTurnStart.Add(hitBroken.Player.Resistance);
                LegacyCurrentSlot slot = Start(broken);
                Assert.That(broken.Player.Resistance, Is.Zero, "turn " + turn);
                Assert.That(slot.EnemyFeedback.OpponentBroken, Is.EqualTo(turn != 2), "turn " + turn);
                if (turn == 2)
                {
                    Assert.That(slot.EnemyFeedback.OpponentResistanceReduced, Is.Zero, "Still broken from turn 1.");
                    Assert.That(slot.EnemyFeedback.EffectActivated, Is.False);
                }
                Finish(broken);
                Start(hitBroken);
                Finish(hitBroken);
            }
            // Broken in turn 1, still broken through turn 2, restored as turn 3 begins: the hit-break schedule.
            Assert.That(atTurnStart, Is.EqualTo(new[] { 50, 0, 50 }));
            Assert.That(hitAtTurnStart, Is.EqualTo(atTurnStart));
        }

        [Test]
        public void BrokenTargetBonus_OnABreakingSkill_AppliesFromItsFirstHit()
        {
            // A hit that breaks keeps its ordinary overflow; a break at the slot's start leaves no such hit.
            List<string[]> rows = Rows();
            Set(rows, Laudare, Column.BrokenTargetDamage, "25%");
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                // The player may use any skill in a test duel; the enemy stands still.
                var duel = new LegacyQueuedDuel(100, 50, 1000, 50, new[] { LegacySkillDefinitions.Skill(Laudare) },
                    Array.Empty<LegacySkill>(), new[] { 0 }, 5);
                Assert.That(duel.TryQueueLane(0), Is.True, "It costs no ACT.");
                duel.Commit();
                Assert.That(duel.BeginNextSlot().PlayerFeedback.OpponentBroken, Is.True);
                List<LegacyHitResult> hits = ResolveSlot(duel);
                Assert.That(hits.Count, Is.EqualTo(5));
                foreach (LegacyHitResult hit in hits)
                    Assert.That(hit.EnemyHealthDamage, Is.EqualTo((int)Math.Round(hit.EnemyPushPower * 2 * 1.25, MidpointRounding.ToEven)),
                        "hit " + hit.HitIndex);
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        [Test]
        public void AConditionalBreak_FollowsTheOpponentCondition()
        {
            List<string[]> rows = Rows();
            Set(rows, Laudare, Column.OpponentKind, "방어");
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                Assert.That(LegacySkillConditions.HasOpponentCondition(LegacySkillDefinitions.Skill(Laudare)), Is.True);

                var attacked = new LegacyQueuedDuel(100, 50, 100, 50, new[] { Probe(900, 1) },
                    new[] { LegacySkillDefinitions.Skill(Laudare) }, new[] { 1 }, 6);
                LegacyCurrentSlot unmatched = Start(attacked);
                Assert.That(unmatched.EnemyFeedback.ConditionMet || unmatched.EnemyFeedback.OpponentBroken, Is.False);
                Assert.That(attacked.Player.Resistance, Is.EqualTo(50));

                var guarded = new LegacyQueuedDuel(100, 50, 100, 50, new[] { Guard(901, 1) },
                    new[] { LegacySkillDefinitions.Skill(Laudare) }, new[] { 1 }, 6);
                LegacyCurrentSlot matched = Start(guarded);
                Assert.That(matched.EnemyFeedback.ConditionMet && matched.EnemyFeedback.OpponentBroken, Is.True);
                Assert.That(guarded.Player.Resistance, Is.Zero);
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        private static List<string[]> Rows()
            => CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table)).Select(record => record.Fields.ToArray()).ToList();

        private static void Set(List<string[]> rows, int id, string header, string value)
            => rows.Single(row => row[0] == id.ToString())[LegacySkillSheet.Headers.ToList().IndexOf(header)] = value;

        /// <summary>Queues the player's lane front, commits and starts the first slot.</summary>
        private static LegacyCurrentSlot Start(LegacyQueuedDuel duel)
        {
            duel.TryQueueLane(0);
            duel.Commit();
            return duel.BeginNextSlot();
        }

        private static void Finish(LegacyQueuedDuel duel)
        {
            ResolveSlot(duel);
            duel.CompleteCurrentSlot();
            duel.BeginNextTurn();
        }

        private static List<LegacyHitResult> ResolveSlot(LegacyQueuedDuel duel)
        {
            var hits = new List<LegacyHitResult>();
            while (!duel.IsCurrentSlotResolved) hits.Add(duel.ResolveNextHit());
            return hits;
        }

        private static LegacySkill Probe(int id, int power)
            => new LegacySkill(id, "probe", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");
    }
}
