using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The decisive-moment replay's bookkeeping (<see cref="LegacyReplayRecorder"/>, 결정적 한 수): it records only
    /// candidate slots, keeps the most important (tier, then score; a tie keeps the earlier), never a Normal slot nor the
    /// finishing blow by default, keeps a long slot's last pictures in order, records a tail that the next slot cuts short,
    /// stays within its memory cap, plays at its speed and holds its last picture, and forgets everything when cleared.</summary>
    public sealed class DecisiveReplayTests
    {
        private const float Interval = 1f / LegacyReplayTuning.DefaultFramesPerSecond;

        [Test]
        public void TheDefaults_AreTheDocumentedOnes_AndStayWithinTheMemoryCap()
        {
            LegacyReplayTuning tuning = LegacyReplayTuning.Default;
            Assert.That(tuning.Enabled, Is.True);
            Assert.That(tuning.FramesPerSecond, Is.EqualTo(15f));
            Assert.That(tuning.MaximumClipSeconds, Is.EqualTo(2.5f));
            Assert.That(tuning.TailSeconds, Is.EqualTo(.4f).Within(1e-6f));
            Assert.That(tuning.PlaybackSpeed, Is.EqualTo(.6f).Within(1e-6f));
            Assert.That(tuning.HoldSeconds, Is.EqualTo(.6f).Within(1e-6f));
            Assert.That(tuning.Width, Is.EqualTo(320));
            Assert.That(tuning.IncludesFinishingBlow, Is.False);
            Assert.That(tuning.MinimumTier, Is.EqualTo(LegacySlotTier.Major));
            int height = tuning.HeightFor(16f / 9f);
            Assert.That(height, Is.EqualTo(180));
            Assert.That(tuning.FramesPerClip(height), Is.EqualTo(38), "2.5 s at 15 a second.");
            Assert.That(tuning.MemoryBytes(height), Is.EqualTo(2L * 38 * 320 * 180 * 4), "About 17.5 MB for both clips.");
            Assert.That(tuning.MemoryBytes(height), Is.LessThanOrEqualTo(LegacyReplayTuning.MemoryCapBytes));
            Assert.That(LegacyReplayTuning.Off.Enabled, Is.False);
            Assert.That(new LegacyReplayRecorder(LegacyReplayTuning.Off, 38).CanRecord, Is.False);
        }

        [Test]
        public void TheNumbers_StayInRange_AndAClipIsCutToTheMemoryCap()
        {
            var wild = new LegacyReplayTuning(true, float.NaN, 99f, -1f, 0f, 99f, 9999, minimumTier: LegacySlotTier.Minor);
            Assert.That(wild.FramesPerSecond, Is.EqualTo(LegacyReplayTuning.DefaultFramesPerSecond));
            Assert.That(wild.MaximumClipSeconds, Is.EqualTo(LegacyReplayTuning.MaximumSeconds));
            Assert.That(wild.TailSeconds, Is.Zero);
            Assert.That(wild.PlaybackSpeed, Is.EqualTo(LegacyReplayTuning.MinimumPlaybackSpeed));
            Assert.That(wild.HoldSeconds, Is.EqualTo(LegacyReplayTuning.MaximumHoldSeconds));
            Assert.That(wild.Width, Is.EqualTo(LegacyReplayTuning.MaximumWidth));
            Assert.That(wild.MinimumTier, Is.EqualTo(LegacySlotTier.Normal), "A slot with nothing in it is never a moment.");
            Assert.That(wild.HeightFor(float.NaN), Is.EqualTo(270), "16:9 when the shape is unknown.");

            // The largest pictures at the most pictures a second for the longest: cut to fit both clips in the cap.
            var greedy = new LegacyReplayTuning(true, 30f, 4f, .4f, 1f, 0f, 480);
            int height = greedy.HeightFor(16f / 9f);
            int frames = greedy.FramesPerClip(height);
            Assert.That(frames, Is.LessThan(120));
            Assert.That(greedy.MemoryBytes(height), Is.LessThanOrEqualTo(LegacyReplayTuning.MemoryCapBytes));
            Assert.That(2L * (frames + 1) * 480 * height * 4, Is.GreaterThan(LegacyReplayTuning.MemoryCapBytes),
                "As many as fit, no fewer.");
            Assert.That(greedy.FramesPerClip(0), Is.Zero);
            Assert.That(new LegacyReplayRecorder(greedy, 1).CanRecord, Is.False, "One picture is no clip.");
        }

        [Test]
        public void OnlyCandidateSlots_AreRecorded()
        {
            var recorder = Recorder();
            Assert.That(recorder.BeginSlot(Rank(0, LegacySlotTier.Minor, 0)), Is.False, "Guard against guard: nothing to see.");
            Assert.That(recorder.IsRecording, Is.False);
            Assert.That(recorder.Advance(1f, out int none), Is.False);
            Assert.That(none, Is.EqualTo(-1));
            Assert.That(recorder.BeginSlot(Rank(1, LegacySlotTier.Minor, 0, reads: LegacyReadEvent.OpponentState)), Is.True,
                "A read known at the start may make it a moment.");
            Assert.That(recorder.BeginSlot(Rank(2, LegacySlotTier.Normal, 100)), Is.True, "An attack may still turn Major.");
            Assert.That(recorder.RecordingSlot, Is.EqualTo(2));
            Assert.That(recorder.BeginSlot(LegacySlotRank.None), Is.False);
            Assert.That(new LegacyReplayRecorder(LegacyReplayTuning.Off, 38).BeginSlot(Rank(0, LegacySlotTier.Major, 500)),
                Is.False, "Switched off: nothing.");
        }

        [Test]
        public void ARecording_TakesAPictureAtOnce_ThenOneEveryInterval_NeverTwoAtOnce()
        {
            var recorder = Recorder();
            recorder.BeginSlot(Rank(0, LegacySlotTier.Normal, 100));
            Assert.That(recorder.Advance(0f, out int first), Is.True, "The slot's first picture comes at once.");
            int other = 1 - recorder.KeptClip;
            Assert.That(first, Is.EqualTo(other * recorder.Capacity), "Into the clip not kept.");
            Assert.That(recorder.Advance(Interval * .5f, out _), Is.False);
            Assert.That(recorder.Advance(Interval * .5f, out int second), Is.True);
            Assert.That(second, Is.EqualTo(first + 1));
            Assert.That(recorder.Advance(Interval * 10f, out int third), Is.True, "A long frame takes one picture…");
            Assert.That(recorder.Advance(0f, out _), Is.False, "…and does not catch up.");
            Assert.That(third, Is.EqualTo(first + 2));
            Assert.That(recorder.RecordedCount, Is.EqualTo(3));
        }

        [Test]
        public void TheMostImportantSlot_IsKept_ByTierThenScore_ATieKeepingTheEarlier()
        {
            var recorder = Recorder();
            Record(recorder, Rank(0, LegacySlotTier.Normal, 100), Rank(0, LegacySlotTier.Normal, 140), 6);
            Assert.That(recorder.HasClip, Is.False, "A Normal slot is never the decisive moment.");

            LegacySkill estocada = Skill(51), guard = Skill(7);
            Record(recorder, Rank(1, LegacySlotTier.Normal, 100), Rank(1, LegacySlotTier.Major, 400), 5, estocada, guard);
            Assert.That(recorder.HasClip, Is.True, "Its hit broke the enemy: Major.");
            Assert.That(recorder.ClipRank.SlotIndex, Is.EqualTo(1));
            Assert.That(recorder.ClipFrameCount, Is.EqualTo(5 + TailPictures));
            Assert.That(recorder.ClipPlayerSkill, Is.SameAs(estocada));
            Assert.That(recorder.ClipEnemySkill, Is.SameAs(guard));
            int kept = recorder.KeptClip;

            Record(recorder, Rank(2, LegacySlotTier.Major, 300), Rank(2, LegacySlotTier.Major, 300), 7);
            Assert.That(recorder.ClipRank.SlotIndex, Is.EqualTo(1), "A lower score does not displace it.");
            Assert.That(recorder.KeptClip, Is.EqualTo(kept));
            Record(recorder, Rank(3, LegacySlotTier.Major, 400), Rank(3, LegacySlotTier.Major, 400), 7);
            Assert.That(recorder.ClipRank.SlotIndex, Is.EqualTo(1), "An exact tie keeps the earlier.");
            Record(recorder, Rank(4, LegacySlotTier.Major, 401), Rank(4, LegacySlotTier.Major, 401), 3);
            Assert.That(recorder.ClipRank.SlotIndex, Is.EqualTo(4), "A higher score takes its place…");
            Assert.That(recorder.KeptClip, Is.EqualTo(1 - kept), "…in the other clip, the old one's places recording next.");
            Assert.That(recorder.ClipFrameCount, Is.EqualTo(3 + TailPictures));
        }

        [Test]
        public void TheFinishingBlow_IsNeverTheClip_UnlessAsked()
        {
            var recorder = Recorder();
            Record(recorder, Rank(0, LegacySlotTier.Major, 300), Rank(0, LegacySlotTier.Major, 300), 4);
            Record(recorder, Rank(1, LegacySlotTier.Normal, 100), Rank(1, LegacySlotTier.Climax, 1500), 4);
            Assert.That(recorder.ClipRank.SlotIndex, Is.EqualTo(0), "The finale has already shown the fall.");

            var everything = new LegacyReplayRecorder(new LegacyReplayTuning(true, 15f, 2.5f, .4f, .6f, .6f, 320,
                includesFinishingBlow: true), 38);
            Record(everything, Rank(0, LegacySlotTier.Major, 300), Rank(0, LegacySlotTier.Major, 300), 4);
            Record(everything, Rank(1, LegacySlotTier.Normal, 100), Rank(1, LegacySlotTier.Climax, 1500), 4);
            Assert.That(everything.ClipRank.Tier, Is.EqualTo(LegacySlotTier.Climax));
            Assert.That(everything.IsCandidate(Rank(2, LegacySlotTier.Major, 900)), Is.False, "Nothing outranks a fall.");

            var normal = new LegacyReplayRecorder(new LegacyReplayTuning(true, 15f, 2.5f, 0f, .6f, .6f, 320,
                minimumTier: LegacySlotTier.Normal), 38);
            Record(normal, Rank(0, LegacySlotTier.Normal, 120), Rank(0, LegacySlotTier.Normal, 120), 4);
            Assert.That(normal.HasClip, Is.True, "A lower minimum keeps a Normal slot.");
        }

        [Test]
        public void ALongSlot_KeepsItsLastPictures_InOrder()
        {
            var tuning = new LegacyReplayTuning(true, 15f, 1f, 0f, 1f, 0f, 320);
            var recorder = new LegacyReplayRecorder(tuning, 4);
            recorder.BeginSlot(Rank(0, LegacySlotTier.Major, 300));
            var places = new int[10];
            Assert.That(recorder.Advance(0f, out places[0]), Is.True);
            for (int picture = 1; picture < places.Length; picture++)
                Assert.That(recorder.Advance(Interval, out places[picture]), Is.True);
            recorder.EndSlot(Rank(0, LegacySlotTier.Major, 300), null, null);
            Assert.That(recorder.HasClip, Is.True, "No tail: kept at once.");
            Assert.That(recorder.ClipFrameCount, Is.EqualTo(4));
            for (int index = 0; index < 4; index++)
            {
                Assert.That(recorder.ClipFrame(index), Is.EqualTo(places[6 + index]), "The last four, oldest first.");
                Assert.That(recorder.IsKeptFrame(places[6 + index]), Is.True);
            }
            Assert.That(recorder.IsKeptFrame(recorder.ClipFrame(0) < 4 ? 4 : 0), Is.False, "The other clip's places are free.");
            Assert.That(() => recorder.ClipFrame(4), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ATail_IsRecordedAfterItsSlot_UntilItRunsOutOrTheNextSlotBegins()
        {
            var recorder = Recorder();
            recorder.BeginSlot(Rank(0, LegacySlotTier.Major, 300));
            recorder.Advance(0f, out _);
            recorder.Advance(Interval, out _);
            recorder.EndSlot(Rank(0, LegacySlotTier.Major, 300), null, null);
            Assert.That(recorder.InTail, Is.True);
            Assert.That(recorder.HasClip, Is.False, "Not yet: its tail is still recording.");
            Assert.That(recorder.Advance(Interval, out _), Is.True, "The tail's pictures go on.");
            recorder.BeginSlot(Rank(1, LegacySlotTier.Normal, 100));
            Assert.That(recorder.HasClip, Is.True, "The next slot cut the tail short: kept as far as it got.");
            Assert.That(recorder.ClipFrameCount, Is.EqualTo(3));
            Assert.That(recorder.RecordingSlot, Is.EqualTo(1));

            // A slot that cannot be kept drops its pictures at once: no tail.
            recorder.Advance(0f, out _);
            recorder.EndSlot(Rank(1, LegacySlotTier.Normal, 100), null, null);
            Assert.That(recorder.IsRecording, Is.False);
            Assert.That(recorder.ClipRank.SlotIndex, Is.EqualTo(0));

            // A tail that runs out keeps the clip.
            recorder.BeginSlot(Rank(2, LegacySlotTier.Major, 500));
            recorder.Advance(0f, out _);
            recorder.Advance(Interval, out _);
            recorder.EndSlot(Rank(2, LegacySlotTier.Major, 500), null, null);
            Assert.That(recorder.Advance(Tail + .01f, out _), Is.False);
            Assert.That(recorder.IsRecording, Is.False);
            Assert.That(recorder.ClipRank.SlotIndex, Is.EqualTo(2));
        }

        [Test]
        public void Stopping_KeepsACompletedSlot_ButDropsAnUnfinishedOne()
        {
            var recorder = Recorder();
            recorder.BeginSlot(Rank(0, LegacySlotTier.Major, 300));
            recorder.Advance(0f, out _);
            recorder.Advance(Interval, out _);
            recorder.Stop();
            Assert.That(recorder.HasClip, Is.False, "Its end was never rated: dropped.");
            recorder.BeginSlot(Rank(1, LegacySlotTier.Major, 300));
            recorder.Advance(0f, out _);
            recorder.Advance(Interval, out _);
            recorder.EndSlot(Rank(1, LegacySlotTier.Major, 300), null, null);
            recorder.Stop();
            Assert.That(recorder.HasClip, Is.True, "The battle ended in its tail: kept.");
            Assert.That(recorder.IsRecording, Is.False);
            recorder.BeginSlot(Rank(2, LegacySlotTier.Major, 300));
            recorder.Advance(0f, out _);
            recorder.EndSlot(Rank(2, LegacySlotTier.Major, 300), null, null);
            recorder.Stop();
            Assert.That(recorder.ClipRank.SlotIndex, Is.EqualTo(1), "One picture is no clip.");
            recorder.EndSlot(Rank(3, LegacySlotTier.Major, 900), null, null);
            Assert.That(recorder.ClipRank.SlotIndex, Is.EqualTo(1), "A slot never recorded changes nothing.");
        }

        [Test]
        public void Playback_RunsAtItsSpeed_AndHoldsItsLastPicture()
        {
            LegacyReplayTuning tuning = LegacyReplayTuning.Default;
            Assert.That(LegacyReplayRecorder.PlaybackSeconds(9, tuning), Is.EqualTo(1f).Within(1e-5f), "9 pictures at 15 × 0.6.");
            Assert.That(LegacyReplayRecorder.PlaybackIndex(0f, 9, tuning), Is.Zero);
            Assert.That(LegacyReplayRecorder.PlaybackIndex(1f / 9f - 1e-3f, 9, tuning), Is.Zero);
            Assert.That(LegacyReplayRecorder.PlaybackIndex(1f / 9f + 1e-3f, 9, tuning), Is.EqualTo(1));
            Assert.That(LegacyReplayRecorder.PlaybackIndex(.5f, 9, tuning), Is.EqualTo(4));
            Assert.That(LegacyReplayRecorder.PlaybackIndex(5f, 9, tuning), Is.EqualTo(8), "Held on the last.");
            Assert.That(LegacyReplayRecorder.PlaybackIndex(float.NaN, 9, tuning), Is.Zero);
            Assert.That(LegacyReplayRecorder.PlaybackIndex(1f, 0, tuning), Is.Zero);
            Assert.That(LegacyReplayRecorder.PlaybackSeconds(0, tuning), Is.Zero);
        }

        [Test]
        public void Clear_ForgetsTheClipAndTheRecording()
        {
            var recorder = Recorder();
            Record(recorder, Rank(0, LegacySlotTier.Major, 300), Rank(0, LegacySlotTier.Major, 300), 4, Skill(51), Skill(7));
            recorder.BeginSlot(Rank(1, LegacySlotTier.Normal, 100));
            recorder.Advance(0f, out _);
            recorder.Clear();
            Assert.That(recorder.HasClip || recorder.IsRecording, Is.False);
            Assert.That(recorder.ClipRank, Is.EqualTo(LegacySlotRank.None));
            Assert.That(recorder.ClipPlayerSkill ?? recorder.ClipEnemySkill, Is.Null);
            Assert.That(recorder.ClipFrameCount, Is.Zero);
            Assert.That(recorder.KeptClip, Is.Zero);
        }

        // The tests' tail, 0.45 s at 15 pictures a second, holds 6 more pictures (at 1/15 … 6/15 s; at 7/15 s it has run
        // out), clear of the float rounding an exact multiple of the interval would meet.
        private const float Tail = .45f;
        private const int TailPictures = 6;

        private static LegacyReplayRecorder Recorder()
        {
            var tuning = new LegacyReplayTuning(true, 15f, 2.5f, Tail, .6f, .6f, 320);
            return new LegacyReplayRecorder(tuning, tuning.FramesPerClip(180));
        }

        // A slot recorded from its start (pictures at once and every interval), ended with its final rating, then its tail
        // played to its end in interval steps.
        private static void Record(LegacyReplayRecorder recorder, LegacySlotRank start, LegacySlotRank final, int pictures,
            LegacySkill playerSkill = null, LegacySkill enemySkill = null)
        {
            Assert.That(recorder.BeginSlot(start), Is.True);
            Assert.That(recorder.Advance(0f, out _), Is.True);
            for (int picture = 1; picture < pictures; picture++) Assert.That(recorder.Advance(Interval, out _), Is.True);
            recorder.EndSlot(final, playerSkill, enemySkill);
            for (int step = 0; step < 100 && recorder.IsRecording; step++) recorder.Advance(Interval, out _);
            Assert.That(recorder.IsRecording, Is.False);
        }

        private static LegacySlotRank Rank(int slot, LegacySlotTier tier, int score, LegacyReadEvent reads = LegacyReadEvent.None)
            => new LegacySlotRank(slot, tier, score, LegacySlotHighlight.None, reads, LegacyReadEvent.None, false, false,
                LegacyClashKind.Clash);

        private static LegacySkill Skill(int id)
            => new LegacySkill(id, "재생 " + id, 1, 5, 5, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "검증");
    }
}
