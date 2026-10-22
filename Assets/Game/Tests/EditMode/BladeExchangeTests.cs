using System;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class BladeExchangeTests
    {
        [Test]
        public void NewAndClearedExchange_HasNoPendingContacts()
        {
            var exchange = new LegacyBladeExchange();
            AssertCleared(exchange);
            Assert.That(exchange.Advance(1f), Is.False);
            exchange.Begin(1, 0, 1, 2);
            Assert.That(exchange.IsPlaying, Is.True);
            Assert.That(exchange.PoseShare, Is.Zero);
            exchange.Advance(10f);
            exchange.Clear();
            exchange.Clear();
            AssertCleared(exchange);
            Assert.That(exchange.Advance(10f), Is.False);
        }

        [Test]
        public void SameInputs_ReproduceContactsDurationsAndEveryPose()
        {
            var first = new LegacyBladeExchange();
            var second = new LegacyBladeExchange();
            first.Begin(7, 3, 1, 600);
            second.Begin(7, 3, 1, 600);
            Assert.That(first.ContactCount, Is.EqualTo(second.ContactCount));
            Assert.That(first.TotalDuration, Is.EqualTo(second.TotalDuration));
            for (int frame = 0; frame < 100; frame++)
            {
                Assert.That(first.Advance(.007f), Is.EqualTo(second.Advance(.007f)));
                Assert.That(first.PoseShare, Is.EqualTo(second.PoseShare));
                Assert.That(first.Progress, Is.EqualTo(second.Progress));
                Assert.That(first.RecoilShare, Is.EqualTo(second.RecoilShare));
                Assert.That(first.ContactsPresented, Is.EqualTo(second.ContactsPresented));
                Assert.That(first.IsPlaying, Is.EqualTo(second.IsPlaying));
            }
            Assert.That(first.IsPlaying, Is.False);
        }

        [TestCase(1, 2)]
        [TestCase(-8000, 600)]
        [TestCase(int.MinValue, int.MaxValue)]
        [TestCase(7, 7)]
        public void ReversingTheTwoSkills_ProducesIdenticalMirroredTiming(int left, int right)
        {
            var first = new LegacyBladeExchange();
            var mirrored = new LegacyBladeExchange();
            first.Begin(2, 6, left, right);
            mirrored.Begin(2, 6, right, left);
            Assert.That(first.ContactCount, Is.EqualTo(mirrored.ContactCount));
            Assert.That(first.TotalDuration, Is.EqualTo(mirrored.TotalDuration));
            for (int frame = 0; frame < 100; frame++)
            {
                Assert.That(first.Advance(.01f), Is.EqualTo(mirrored.Advance(.01f)));
                Assert.That(first.PoseShare, Is.EqualTo(mirrored.PoseShare));
                Assert.That(first.Progress, Is.EqualTo(mirrored.Progress));
                Assert.That(first.RecoilShare, Is.EqualTo(mirrored.RecoilShare));
            }
        }

        [Test]
        public void DifferentTurnsAndSlots_KeepOneContactWithVariedHashedDuration()
        {
            var totals = new HashSet<float>();
            for (int turn = 1; turn <= 8; turn++)
                for (int slot = 0; slot < 8; slot++)
                {
                    var exchange = new LegacyBladeExchange();
                    exchange.Begin(turn, slot, 1, 2);
                    Assert.That(exchange.ContactCount, Is.EqualTo(1));
                    totals.Add(exchange.TotalDuration);
                    Assert.That(exchange.TotalDuration, Is.InRange(LegacyBladeExchange.DefaultMinSeconds,
                        LegacyBladeExchange.DefaultMaxSeconds));
                }
            Assert.That(totals.Count, Is.GreaterThan(48));
        }

        [Test]
        public void ContactApproachStaysForward_AndDeflectionReturnsToZeroAtTheAttackHandoff()
        {
            var exchange = new LegacyBladeExchange();
            exchange.Begin(1, 0, 1, 2, 1f, 1f);
            Assert.That(exchange.PoseShare, Is.Zero);
            Assert.That(exchange.RecoilShare, Is.Zero);
            Assert.That(exchange.Advance(.2f), Is.False);
            Assert.That(exchange.PoseShare, Is.EqualTo(.5f).Within(.00001f));
            Assert.That(exchange.RecoilShare, Is.Zero);
            Assert.That(exchange.Advance(.2f), Is.True);
            Assert.That(exchange.ContactsPresented, Is.EqualTo(1));
            Assert.That(exchange.PoseShare, Is.EqualTo(1f).Within(.00001f));
            Assert.That(exchange.RecoilShare, Is.EqualTo(0f).Within(.00001f));
            Assert.That(exchange.Advance(0f), Is.False);
            Assert.That(exchange.PoseShare, Is.EqualTo(1f).Within(.00001f));
            Assert.That(exchange.Advance(.28f), Is.False);
            Assert.That(exchange.PoseShare, Is.EqualTo(1f));
            Assert.That(exchange.RecoilShare, Is.EqualTo(1f).Within(.00001f));
            Assert.That(exchange.Advance(.16f), Is.False);
            Assert.That(exchange.PoseShare, Is.EqualTo(1f));
            Assert.That(exchange.RecoilShare, Is.EqualTo(.5f).Within(.00001f));
            Assert.That(exchange.Advance(.17f), Is.False);
            Assert.That(exchange.IsPlaying, Is.False);
            Assert.That(exchange.Progress, Is.EqualTo(1f));
            Assert.That(exchange.PoseShare, Is.EqualTo(1f));
            Assert.That(exchange.RecoilShare, Is.Zero);
        }

        [TestCase(1f / 240f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 15f)]
        [TestCase(10f)]
        [TestCase(float.MaxValue)]
        public void EveryFrameRate_ReportsOneContactWithoutRewindingOrAddingAReleaseFrame(float delta)
        {
            for (int slot = 0; slot < 32; slot++)
            {
                var exchange = new LegacyBladeExchange();
                exchange.Begin(4, slot, 1, 2);
                int observed = 0;
                float previousPose = 0f, previousProgress = 0f;
                for (int frame = 0; frame < 200 && exchange.IsPlaying; frame++)
                {
                    int before = exchange.ContactsPresented;
                    bool contact = exchange.Advance(delta);
                    Assert.That(exchange.ContactsPresented - before, Is.EqualTo(contact ? 1 : 0));
                    Assert.That(exchange.Progress, Is.InRange(previousProgress, 1f));
                    Assert.That(exchange.PoseShare, Is.InRange(previousPose, 1f));
                    Assert.That(exchange.RecoilShare, Is.InRange(0f, 1f));
                    previousPose = exchange.PoseShare;
                    previousProgress = exchange.Progress;
                    if (!contact) continue;
                    observed++;
                    Assert.That(exchange.PoseShare, Is.EqualTo(1f).Within(.00001f));
                }
                Assert.That(exchange.IsPlaying, Is.False);
                Assert.That(observed, Is.EqualTo(exchange.ContactCount));
                Assert.That(exchange.Advance(delta), Is.False);
                Assert.That(exchange.ContactsPresented, Is.EqualTo(observed));
                Assert.That(exchange.PoseShare, Is.EqualTo(1f));
                Assert.That(exchange.Progress, Is.EqualTo(1f));
                Assert.That(exchange.RecoilShare, Is.Zero);
            }
        }

        [Test]
        public void HugeFrame_CompletesTheWholeTimelineWithoutDiscardingOvershoot()
        {
            var exchange = new LegacyBladeExchange();
            exchange.Begin(3, 4, 1, 2);
            Assert.That(exchange.Advance(100f), Is.True);
            Assert.That(exchange.ContactsPresented, Is.EqualTo(1));
            Assert.That(exchange.IsPlaying, Is.False);
            Assert.That(exchange.Progress, Is.EqualTo(1f));
            Assert.That(exchange.PoseShare, Is.EqualTo(1f));
            Assert.That(exchange.RecoilShare, Is.Zero);
            Assert.That(exchange.Advance(0f), Is.False);
            Assert.That(exchange.Advance(100f), Is.False);
        }

        [Test]
        public void CrossingContact_KeepsAllElapsedTime_AndNeverReportsItTwice()
        {
            var exchange = new LegacyBladeExchange();
            exchange.Begin(3, 4, 1, 2, .1f, .1f);
            Assert.That(exchange.Advance(.075f), Is.True);
            Assert.That(exchange.Progress, Is.EqualTo(.75f).Within(.00001f));
            Assert.That(exchange.PoseShare, Is.EqualTo(1f));
            Assert.That(exchange.RecoilShare, Is.GreaterThan(0f));
            Assert.That(exchange.IsPlaying, Is.True);
            Assert.That(exchange.Advance(.03f), Is.False);
            Assert.That(exchange.IsPlaying, Is.False);
            Assert.That(exchange.ContactsPresented, Is.EqualTo(1));
        }

        [Test]
        public void Restarting_ReplacesTheOldTimerAndPendingContacts()
        {
            var exchange = new LegacyBladeExchange();
            exchange.Begin(1, 0, 1, 2);
            exchange.Advance(100f);
            exchange.Begin(8, 4, 7, 9, .2f, .3f);
            var fresh = new LegacyBladeExchange();
            fresh.Begin(8, 4, 7, 9, .2f, .3f);
            Assert.That(exchange.ContactsPresented, Is.Zero);
            Assert.That(exchange.TotalDuration, Is.EqualTo(fresh.TotalDuration));
            Assert.That(exchange.ContactCount, Is.EqualTo(fresh.ContactCount));
            Assert.That(exchange.PoseShare, Is.Zero);
            Assert.That(exchange.Progress, Is.Zero);
            Assert.That(exchange.RecoilShare, Is.Zero);
        }

        [TestCase(float.NaN, .14f)]
        [TestCase(float.PositiveInfinity, .14f)]
        [TestCase(float.NegativeInfinity, .14f)]
        [TestCase(-.1f, .14f)]
        [TestCase(0f, .14f)]
        [TestCase(.08f, float.NaN)]
        [TestCase(.08f, float.PositiveInfinity)]
        [TestCase(.08f, float.NegativeInfinity)]
        [TestCase(.14f, .08f)]
        [TestCase(.08f, float.MaxValue)]
        public void InvalidDurationRange_IsRejectedBeforeChangingTheRunningExchange(float min, float max)
        {
            var exchange = new LegacyBladeExchange();
            exchange.Begin(1, 0, 1, 2);
            exchange.Advance(.01f);
            float duration = exchange.TotalDuration, pose = exchange.PoseShare;
            int contacts = exchange.ContactCount;
            Assert.Throws<ArgumentOutOfRangeException>(() => exchange.Begin(9, 9, 9, 9, min, max));
            Assert.That(exchange.TotalDuration, Is.EqualTo(duration));
            Assert.That(exchange.PoseShare, Is.EqualTo(pose));
            Assert.That(exchange.ContactCount, Is.EqualTo(contacts));
            Assert.That(exchange.IsPlaying, Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-1f)]
        public void InvalidAdvance_IsRejectedWithoutMovingTime(float delta)
        {
            var exchange = new LegacyBladeExchange();
            exchange.Begin(1, 0, 1, 2);
            float pose = exchange.PoseShare;
            Assert.Throws<ArgumentOutOfRangeException>(() => exchange.Advance(delta));
            Assert.That(exchange.PoseShare, Is.EqualTo(pose));
            Assert.That(exchange.ContactsPresented, Is.Zero);
        }

        [Test]
        public void SmallestPositiveDuration_StillPresentsContactsWithoutInvalidPoses()
        {
            var exchange = new LegacyBladeExchange();
            exchange.Begin(2, 3, 1, 2, float.Epsilon, float.Epsilon);
            while (exchange.IsPlaying)
            {
                exchange.Advance(float.Epsilon);
                Assert.That(float.IsNaN(exchange.PoseShare), Is.False);
                Assert.That(exchange.PoseShare, Is.InRange(0f, 1f));
                Assert.That(exchange.Progress, Is.InRange(0f, 1f));
                Assert.That(exchange.RecoilShare, Is.InRange(0f, 1f));
            }
            Assert.That(exchange.ContactsPresented, Is.EqualTo(exchange.ContactCount));
        }

        [TestCase(LegacySkillKind.Attack, LegacySkillKind.Attack, true)]
        [TestCase(LegacySkillKind.Attack, LegacySkillKind.Defence, true)]
        [TestCase(LegacySkillKind.Defence, LegacySkillKind.Attack, true)]
        [TestCase(LegacySkillKind.Defence, LegacySkillKind.Defence, false)]
        [TestCase(LegacySkillKind.Attack, LegacySkillKind.Wait, false)]
        [TestCase(LegacySkillKind.Wait, LegacySkillKind.Attack, false)]
        [TestCase(LegacySkillKind.Defence, LegacySkillKind.Wait, false)]
        [TestCase(LegacySkillKind.Wait, LegacySkillKind.Wait, false)]
        public void Eligibility_NeedsTwoTechniquesAndAtLeastOneAttack(LegacySkillKind left, LegacySkillKind right, bool expected)
        {
            Assert.That(LegacyBladeExchange.CanExchange(Skill(left), Skill(right)), Is.EqualTo(expected));
        }

        [Test]
        public void NullActions_CannotExchangeBlades()
        {
            Assert.That(LegacyBladeExchange.CanExchange(null, Skill(LegacySkillKind.Attack)), Is.False);
            Assert.That(LegacyBladeExchange.CanExchange(Skill(LegacySkillKind.Attack), null), Is.False);
            Assert.That(LegacyBladeExchange.CanExchange(null, null), Is.False);
        }

        private static LegacySkill Skill(LegacySkillKind kind)
            => new LegacySkill(-12000, "시험", 0, 1, 1, kind,
                kind == LegacySkillKind.Defence ? LegacySkillProperty.Defence : LegacySkillProperty.Slash,
                1, 0, string.Empty);

        private static void AssertCleared(LegacyBladeExchange exchange)
        {
            Assert.That(exchange.IsPlaying, Is.False);
            Assert.That(exchange.ContactCount, Is.Zero);
            Assert.That(exchange.ContactsPresented, Is.Zero);
            Assert.That(exchange.TotalDuration, Is.Zero);
            Assert.That(exchange.PoseShare, Is.Zero);
            Assert.That(exchange.Progress, Is.Zero);
            Assert.That(exchange.RecoilShare, Is.Zero);
        }
    }
}
