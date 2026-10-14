using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>What the duel screens say about the effects that read the queue (<see cref="LegacyKeywordPreview"/>): the lane
    /// window's line, the held explanation's row, the queued cards' marks and 순환 counts, and the cues. The numbers are the
    /// rules' own (<see cref="LegacyKeywordLinks"/>), read from the duel without changing it.</summary>
    public sealed class KeywordPreviewTests
    {
        private const int Q = 0, W = 1, E = 2;

        [Test]
        public void CycleFeed_TellsTheKindsItWouldRaise_MarksItsCard_AndWritesTheRiseOnTheCounts()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = KeywordLinkTests.Duel(KeywordSheet.CycleQ, KeywordSheet.CycleW, KeywordSheet.Feeder);
                LegacySkill feeder = duel.GetLane(E)[0];
                Assert.That(LegacyKeywordPreview.LaneLine(duel, feeder), Is.EqualTo("앞 순환 0"));
                Assert.That(LegacyKeywordPreview.Note(duel, feeder), Is.EqualTo("순환 +1 · 앞에 올릴 순환 기술 없음"));

                KeywordLinkTests.Queue(duel, Q, W);
                Assert.That(LegacyKeywordPreview.LaneLine(duel, feeder), Is.EqualTo("앞 순환 2"));
                Assert.That(LegacyKeywordPreview.Note(duel, feeder), Is.EqualTo("순환 +1 · 지금 예약하면 시험 901·시험 902 순환 +1"));
                Assert.That(LegacyKeywordPreview.CycleCount(duel, true, 0, true, out bool raised, out bool capped), Is.EqualTo("0/2"),
                    "Nothing raises it yet.");
                Assert.That((raised, capped), Is.EqualTo((false, false)));

                KeywordLinkTests.Queue(duel, E);
                LegacyKeywordMark mark = LegacyKeywordPreview.QueueMark(duel, true, 2, true);
                Assert.That((mark.Text, mark.Kind, mark.Spent), Is.EqualTo(("순환 +1 ×2", LegacyKeywordMarkKind.CycleFeed, false)));
                Assert.That(LegacyKeywordPreview.CycleCount(duel, true, 0, true, out raised, out capped), Is.EqualTo("0→1/2"));
                Assert.That((raised, capped), Is.EqualTo((true, false)));
                Assert.That(LegacyKeywordPreview.CycleCount(duel, true, 1, true, out _, out _), Is.EqualTo("0→1/2"));
                Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 0, true).IsEmpty, Is.True, "A carrier has no mark of its own.");
                Assert.That(LegacyKeywordPreview.LaneLine(duel, duel.GetLane(E)[0]), Is.EqualTo(LegacyKeywordPreview.UsedThisTurn),
                    "A second copy feeds nothing.");
                Assert.That(LegacyKeywordPreview.Note(duel, duel.GetLane(E)[0]), Is.EqualTo("순환 +1 · 턴당 1회 · 사용함"));
                Assert.That(LegacyKeywordPreview.CycleFeedCue(duel.PlayerQueue, duel.CycleUses), Is.EqualTo("순환 +1 · 시험 901·시험 902"));
                Assert.That(LegacyKeywordPreview.FirstCycleFeeder(duel.PlayerQueue).Id, Is.EqualTo(KeywordSheet.Feeder));
                Assert.That((duel.CycleUses(KeywordSheet.CycleQ), duel.CycleUses(KeywordSheet.CycleW)), Is.EqualTo((0, 0)),
                    "Reading never feeds.");

                duel.Commit();
                Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 2, false).IsEmpty, Is.True, "The feed is done once committed.");
                Assert.That(LegacyKeywordPreview.CycleCount(duel, true, 0, false, out raised, out _), Is.EqualTo("1/2"));
                Assert.That(raised, Is.False);
            });
        }

        [Test]
        public void CycleFeed_AtTheCap_SaysMaximumAndRaisesNothing()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = KeywordLinkTests.Duel(KeywordSheet.CycleQ, KeywordSheet.Feeder);
                KeywordLinkTests.Queue(duel, Q, E);
                duel.Commit();
                while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
                duel.BeginNextTurn();
                Assert.That(duel.CycleUses(KeywordSheet.CycleQ), Is.EqualTo(2), "Fed once, cast once: at its cap.");

                KeywordLinkTests.Queue(duel, Q);
                Assert.That(LegacyKeywordPreview.LaneLine(duel, duel.GetLane(E)[0]), Is.EqualTo("앞 순환 0"));
                KeywordLinkTests.Queue(duel, E);
                Assert.That(LegacyKeywordPreview.CycleCount(duel, true, 0, true, out bool raised, out bool capped),
                    Is.EqualTo(LegacyKeywordPreview.CycleCapped));
                Assert.That((raised, capped), Is.EqualTo((false, true)));
                LegacyKeywordMark mark = LegacyKeywordPreview.QueueMark(duel, true, 1, true);
                Assert.That((mark.Text, mark.Spent), Is.EqualTo(("순환 +0", true)));
                Assert.That(LegacyKeywordPreview.CycleFeedCue(duel.PlayerQueue, duel.CycleUses), Is.Null);
            });
        }

        [Test]
        public void BreathPayoff_CountsBreathsAhead_AndItsSecondCopyGainsNothing()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = KeywordLinkTests.Duel(KeywordSheet.BreathPayoff);
                LegacySkill payoff = duel.GetLane(E)[0];
                Assert.That(LegacyKeywordPreview.LaneLine(duel, payoff), Is.EqualTo("숨 0/4 · +0"));
                Assert.That(duel.TryQueueBreath() && duel.TryQueueBreath(), Is.True);
                Assert.That(LegacyKeywordPreview.LaneLine(duel, payoff), Is.EqualTo("숨 2/4 · +8"));
                Assert.That(LegacyKeywordPreview.Note(duel, payoff), Is.EqualTo("숨고르기 2/4 · 지금 예약하면 위력 +8"));

                KeywordLinkTests.Queue(duel, E);
                LegacyKeywordMark mark = LegacyKeywordPreview.QueueMark(duel, true, 2, true);
                Assert.That((mark.Text, mark.Kind, mark.Spent), Is.EqualTo(("숨 +8", LegacyKeywordMarkKind.AheadPower, false)));
                Assert.That(LegacyKeywordPreview.AheadPowerCue(duel.PlayerQueue, 2), Is.EqualTo("숨고르기 2 · 위력 +8"));
                Assert.That(LegacyKeywordPreview.LaneLine(duel, duel.GetLane(E)[0]), Is.EqualTo(LegacyKeywordPreview.UsedThisTurn));
                Assert.That(LegacyKeywordPreview.Note(duel, duel.GetLane(E)[0]), Is.EqualTo("숨고르기마다 위력 · 턴당 1회 · 사용함"));

                KeywordLinkTests.Queue(duel, E);
                mark = LegacyKeywordPreview.QueueMark(duel, true, 3, true);
                Assert.That((mark.Text, mark.Spent), Is.EqualTo(("+0", true)));
                Assert.That(LegacyKeywordPreview.AheadPowerCue(duel.PlayerQueue, 3), Is.Null, "A second copy has nothing to say.");
                Assert.That(LegacyKeywordPreview.AheadPowerCue(duel.PlayerQueue, 0), Is.Null, "A breath is no payoff.");
            });
        }

        [Test]
        public void ReductionPayoff_CountsKindsAhead_WhateverTheirConditions()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = KeywordLinkTests.Duel(KeywordSheet.ReductionQ, KeywordSheet.ReductionPayoff,
                    KeywordSheet.ReductionMissE);
                KeywordLinkTests.Queue(duel, Q, E);
                Assert.That(LegacyKeywordPreview.LaneLine(duel, duel.GetLane(W)[0]), Is.EqualTo("감소 2/3 · +10"));
                KeywordLinkTests.Queue(duel, W);
                Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 2, true).Text, Is.EqualTo("감소 +10"));
                Assert.That(LegacyKeywordPreview.AheadPowerCue(duel.PlayerQueue, 2), Is.EqualTo("저항 감소 2종 · 위력 +10"));
                Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 2, false).Text, Is.EqualTo("감소 +10"),
                    "The count holds through the resolution.");
            });
        }

        [Test]
        public void AutoBreath_TellsItsOnceAvailable_TagsItsBreath_AndNeedsTheBreathOpen()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = KeywordLinkTests.Duel(KeywordSheet.AutoBreathGuard);
                LegacySkill guard = duel.GetLane(Q)[0];
                Assert.That(LegacyKeywordPreview.LaneLine(duel, guard), Is.EqualTo("자동 숨 1/1"));
                Assert.That(LegacyKeywordPreview.Note(duel, guard), Is.EqualTo("숨고르기 +1 · 지금 예약하면 바로 뒤에 숨고르기 (자동 1/1)"));
                KeywordLinkTests.Queue(duel, Q);
                LegacyKeywordMark mark = LegacyKeywordPreview.QueueMark(duel, true, 1, true);
                Assert.That((mark.Text, mark.Kind), Is.EqualTo((LegacyKeywordPreview.AutoBreathTag, LegacyKeywordMarkKind.AutoBreath)));
                Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 0, true).IsEmpty, Is.True, "The tag rides the breath, not the guard.");
                Assert.That(LegacyKeywordPreview.LaneLine(duel, duel.GetLane(Q)[0]), Is.EqualTo(LegacyKeywordPreview.UsedThisTurn));
                Assert.That(LegacyKeywordPreview.Note(duel, duel.GetLane(Q)[0]), Is.EqualTo("숨고르기 +1 · 턴당 1회 · 사용함"));
                Assert.That(duel.TryQueueBreath(), Is.True);
                Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 2, true).IsEmpty, Is.True, "A breath by hand is not tagged.");
                Assert.That(((ILegacyDuelView)duel).EnemyAutoBreathSlotIndex, Is.EqualTo(-1), "The campaign's enemy never has one.");

                var closed = new LegacyQueuedDuel(1000, 50, 1000, 100, new[] { KeywordSheet.Skill(KeywordSheet.AutoBreathGuard) },
                    Array.Empty<LegacySkill>(), new[] { 0 }, randomSeed: 1, features: CombatFeature.All & ~CombatFeature.Breath,
                    playerActGainBonus: 7, meshPercent: 0);
                Assert.That(LegacyKeywordPreview.LaneLine(closed, guard), Is.Null, "With 숨고르기 closed it adds none.");
                Assert.That(LegacyKeywordPreview.Note(closed, guard), Is.Null);
            });
        }

        [Test]
        public void AutoDodge_MarksItsCard_AndItsNoteNamesWhatTheSlotHolds()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = KeywordLinkTests.Duel(KeywordSheet.Dodger, KeywordSheet.CycleQ);
                LegacySkill dodger = duel.GetLane(E)[0];
                Assert.That(LegacyKeywordPreview.LaneLine(duel, dodger), Is.Null, "Nothing to count before it is reserved.");
                Assert.That(LegacyKeywordPreview.Note(duel, dodger), Is.EqualTo("자동 회피 · 이 칸 상대 공격을 모두 피함"));
                Assert.That(LegacyKeywordPreview.LaneLine(duel, duel.GetLane(Q)[0]), Is.Null, "A plain carrier keeps its count instead.");
                KeywordLinkTests.Queue(duel, E, Q);
                LegacyKeywordMark mark = LegacyKeywordPreview.QueueMark(duel, true, 0, true);
                Assert.That((mark.Text, mark.Kind), Is.EqualTo((LegacyKeywordPreview.AutoDodgeTag, LegacyKeywordMarkKind.AutoDodge)));
                Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 1, true).IsEmpty, Is.True);
                Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 5, true).IsEmpty, Is.True, "Past the queue: nothing.");
                Assert.That(LegacyKeywordPreview.QueueMark(null, true, 0, true).IsEmpty, Is.True);

                var attacked = new LegacyQueuedDuel(1000, 50, 1000, 100, new[] { KeywordSheet.Skill(KeywordSheet.Dodger) },
                    new[] { KeywordSheet.Skill(KeywordSheet.ReductionQ) }, new[] { 1 }, randomSeed: 1, playerActGainBonus: 7, meshPercent: 0);
                Assert.That(attacked.EnemyQueue.Count, Is.EqualTo(1));
                Assert.That(LegacyKeywordPreview.Note(attacked, dodger), Is.EqualTo("자동 회피 · 지금 예약하면 시험 911을 피함"));
                var guarded = new LegacyQueuedDuel(1000, 50, 1000, 100, new[] { KeywordSheet.Skill(KeywordSheet.Dodger) },
                    new[] { KeywordSheet.Skill(KeywordSheet.AutoBreathGuard) }, new[] { 1 }, randomSeed: 1, playerActGainBonus: 7,
                    meshPercent: 0);
                Assert.That(LegacyKeywordPreview.Note(guarded, dodger), Is.EqualTo("자동 회피 · 이 칸 상대는 공격하지 않음"));
            });
        }

        [Test]
        public void Versus_EachSideReadsItsOwnQueue_AndTheOpponentsAutomaticBreathIsTagged()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacySkill[] skills = { KeywordSheet.Skill(KeywordSheet.AutoBreathGuard), KeywordSheet.Skill(KeywordSheet.BreathPayoff) };
                var match = new LocalVersusMatch(skills, skills, randomSeed: 3);
                Assert.That(match.TryQueueLane(0, Q), Is.True);
                var left = new LocalVersusPerspective(match, 0);
                var right = new LocalVersusPerspective(match, 1);
                Assert.That(left.AutoBreathSlotIndex, Is.EqualTo(1));
                Assert.That(right.EnemyAutoBreathSlotIndex, Is.EqualTo(1));
                Assert.That(LegacyKeywordPreview.QueueMark(right, false, 1, true).Text, Is.EqualTo(LegacyKeywordPreview.AutoBreathTag));
                Assert.That(LegacyKeywordPreview.QueueMark(left, true, 1, true).Text, Is.EqualTo(LegacyKeywordPreview.AutoBreathTag));
                Assert.That(LegacyKeywordPreview.LaneLine(left, skills[1]), Is.EqualTo("숨 1/4 · +4"));
                Assert.That(LegacyKeywordPreview.LaneLine(right, skills[1]), Is.EqualTo("숨 0/4 · +0"), "The other side's breaths are not its own.");
                Assert.That(LegacyKeywordPreview.LaneLine(right, skills[0]), Is.EqualTo("자동 숨 1/1"));
            });
        }
    }
}
