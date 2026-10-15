using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The decisive-moment replay's numbers (결정적 한 수, <see cref="LegacyReplayRecorder"/>). Presentation only:
    /// the rules never read them. The presentation settings hold them; <see cref="Default"/> is the code's defaults and
    /// <see cref="Off"/> records nothing.</summary>
    public readonly struct LegacyReplayTuning
    {
        public const float DefaultFramesPerSecond = 15f, DefaultMaximumSeconds = 2.5f, DefaultTailSeconds = .4f,
            DefaultPlaybackSpeed = .6f, DefaultHoldSeconds = .6f;
        public const int DefaultWidth = 320;
        /// <summary>The ranges the numbers are kept in.</summary>
        public const float MinimumFramesPerSecond = 6f, MaximumFramesPerSecond = 30f, MinimumSeconds = 1f,
            MaximumSeconds = 4f, MaximumTailSeconds = 1f, MinimumPlaybackSpeed = .25f, MaximumHoldSeconds = 2f;
        public const int MinimumWidth = 160, MaximumWidth = 480;
        /// <summary>What both clips' pictures (the one kept and the one recording) may take together: 24 MiB. A picture is
        /// <see cref="BytesPerPixel"/> bytes a pixel (8-bit RGBA); <see cref="FramesPerClip"/> cuts a clip's length to fit.</summary>
        public const long MemoryCapBytes = 24L * 1024L * 1024L;
        public const int BytesPerPixel = 4;

        /// <param name="enabled">Whether slots are recorded and the clip is played on the result at all.</param>
        /// <param name="framesPerSecond">Pictures a second while a candidate slot plays (real time; 6 to 30).</param>
        /// <param name="maximumSeconds">The clip's longest length (real seconds, 1 to 4): a longer slot keeps its last
        /// <paramref name="maximumSeconds"/>.</param>
        /// <param name="tailSeconds">How long the recording goes on after its slot completed (real seconds, 0 to 1): the
        /// fighters spring apart, the numbers rise. The next slot's start cuts it short.</param>
        /// <param name="playbackSpeed">The replay's speed (0.25 to 1; below 1 it plays slower than it was recorded).</param>
        /// <param name="holdSeconds">How long its last picture stays before the replay fades (real seconds, 0 to 2).</param>
        /// <param name="width">The pictures' width in pixels (160 to 480; the height follows the screen's shape).</param>
        /// <param name="includesFinishingBlow">Whether the finishing blow's slot (Climax) may be the clip. Off by default: the
        /// finale has already shown it, slowly, so the replay shows the moment that led there.</param>
        /// <param name="minimumTier">The least a slot must have been rated to be kept (Major by default: a break, a heavy
        /// hit, a 간파, a signature move, a large 합 gap, a deep pierce, a dodge).</param>
        public LegacyReplayTuning(bool enabled, float framesPerSecond, float maximumSeconds, float tailSeconds,
            float playbackSpeed, float holdSeconds, int width, bool includesFinishingBlow = false,
            LegacySlotTier minimumTier = LegacySlotTier.Major)
        {
            Enabled = enabled;
            FramesPerSecond = Clamp(framesPerSecond, MinimumFramesPerSecond, MaximumFramesPerSecond, DefaultFramesPerSecond);
            MaximumClipSeconds = Clamp(maximumSeconds, MinimumSeconds, MaximumSeconds, DefaultMaximumSeconds);
            TailSeconds = Clamp(tailSeconds, 0f, MaximumTailSeconds, 0f);
            PlaybackSpeed = Clamp(playbackSpeed, MinimumPlaybackSpeed, 1f, 1f);
            HoldSeconds = Clamp(holdSeconds, 0f, MaximumHoldSeconds, 0f);
            Width = width < MinimumWidth ? MinimumWidth : width > MaximumWidth ? MaximumWidth : width;
            IncludesFinishingBlow = includesFinishingBlow;
            MinimumTier = minimumTier < LegacySlotTier.Normal ? LegacySlotTier.Normal
                : minimumTier > LegacySlotTier.Climax ? LegacySlotTier.Climax : minimumTier;
        }

        /// <summary>The defaults: 15 pictures a second, at most 2.5 s, 0.4 s after its slot, played at 0.6 times with its last
        /// picture held 0.6 s, 320 pixels wide, a Major slot at least, never the finishing blow.</summary>
        public static LegacyReplayTuning Default => new LegacyReplayTuning(true, DefaultFramesPerSecond, DefaultMaximumSeconds,
            DefaultTailSeconds, DefaultPlaybackSpeed, DefaultHoldSeconds, DefaultWidth);

        /// <summary>Switched off: nothing is recorded or played; the result comes as ever.</summary>
        public static LegacyReplayTuning Off => new LegacyReplayTuning(false, DefaultFramesPerSecond, DefaultMaximumSeconds,
            0f, 1f, 0f, DefaultWidth);

        public bool Enabled { get; }
        public float FramesPerSecond { get; }
        public float MaximumClipSeconds { get; }
        public float TailSeconds { get; }
        public float PlaybackSpeed { get; }
        public float HoldSeconds { get; }
        public int Width { get; }
        public bool IncludesFinishingBlow { get; }
        public LegacySlotTier MinimumTier { get; }
        /// <summary>Real seconds between two pictures.</summary>
        public float FrameInterval => 1f / FramesPerSecond;

        /// <summary>The picture's height for the screen's shape (<paramref name="aspect"/>, width over height): even, at least
        /// 2; a shape that is not a number or out of 1:1 to 3:1 reads as 16:9.</summary>
        public int HeightFor(float aspect)
        {
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect < 1f || aspect > 3f) aspect = 16f / 9f;
            int height = (int)Math.Round(Width / aspect);
            height += height % 2;
            return Math.Max(2, height);
        }

        /// <summary>How many pictures one clip holds at <paramref name="height"/>: enough for
        /// <see cref="MaximumClipSeconds"/>, cut so both clips together keep within <see cref="MemoryCapBytes"/>. 0 when not
        /// even <see cref="LegacyReplayRecorder.MinimumFrames"/> would fit (or the height is not positive).</summary>
        public int FramesPerClip(int height)
        {
            if (height <= 0) return 0;
            int wanted = (int)Math.Ceiling(FramesPerSecond * MaximumClipSeconds - 1e-4);
            long picture = (long)Width * height * BytesPerPixel;
            long fits = MemoryCapBytes / (2L * picture);
            int frames = (int)Math.Min(wanted, fits);
            return frames < LegacyReplayRecorder.MinimumFrames ? 0 : frames;
        }

        /// <summary>The bytes both clips' pictures take at most at <paramref name="height"/>.</summary>
        public long MemoryBytes(int height) => 2L * FramesPerClip(height) * Width * Math.Max(0, height) * BytesPerPixel;

        private static float Clamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) ? fallback : value < min ? min : value > max ? max : value;
    }

    /// <summary>The decisive-moment replay's bookkeeping (결정적 한 수, presentation only; engine-free). During a battle it
    /// keeps the most important slot so far as a short flipbook: the highest tier the director rated
    /// (<see cref="LegacySlotRank.Outranks"/>: tier, then score; an exact tie keeps the earlier), at least
    /// <see cref="LegacyReplayTuning.MinimumTier"/>, never the finishing blow unless
    /// <see cref="LegacyReplayTuning.IncludesFinishingBlow"/>. It records only while a candidate slot plays (one with an
    /// attack, a read or a signature: any of them may still turn Major) and for its tail, into one clip while the other
    /// holds the best so far; a slot that completes outranking the kept clip takes its place, else its pictures are
    /// dropped. The pictures themselves belong to the presentation: the recorder only names which of
    /// <see cref="FrameCount"/> picture places (clip A's 0 to <see cref="Capacity"/> − 1, clip B's after them) to draw into
    /// and when (<see cref="Advance"/>), and which make the kept clip, in order (<see cref="ClipFrame"/>).</summary>
    public sealed class LegacyReplayRecorder
    {
        /// <summary>A clip needs at least this many pictures to be kept.</summary>
        public const int MinimumFrames = 2;
        private int keptBuffer;
        private int keptCount, keptHead;
        private int recordCount, recordHead;
        private float sinceFrame, tailLeft = -1f;
        private bool recording;
        private LegacySlotRank pendingRank = LegacySlotRank.None;
        private LegacySkill pendingPlayerSkill, pendingEnemySkill;

        /// <param name="capacity">Pictures one clip holds (<see cref="LegacyReplayTuning.FramesPerClip"/>); fewer than
        /// <see cref="MinimumFrames"/> records nothing.</param>
        public LegacyReplayRecorder(LegacyReplayTuning tuning, int capacity)
        {
            Tuning = tuning;
            Capacity = Math.Max(0, capacity);
            ClipRank = LegacySlotRank.None;
            RecordingSlot = -1;
        }

        public LegacyReplayTuning Tuning { get; }
        /// <summary>Pictures one clip holds.</summary>
        public int Capacity { get; }
        /// <summary>Picture places both clips have together (<see cref="ClipFrame"/> and <see cref="Advance"/> name them).</summary>
        public int FrameCount => Capacity * 2;
        /// <summary>Whether anything can be recorded at all (switched on and room for a clip).</summary>
        public bool CanRecord => Tuning.Enabled && Capacity >= MinimumFrames;
        /// <summary>Whether a candidate slot (or its tail) is being recorded now.</summary>
        public bool IsRecording => recording;
        /// <summary>Whether the recording is its slot's tail (the slot completed and may still be kept).</summary>
        public bool InTail => recording && tailLeft >= 0f;
        /// <summary>The slot being recorded (-1 while none).</summary>
        public int RecordingSlot { get; private set; }
        /// <summary>Pictures the recording has so far (at most <see cref="Capacity"/>: a longer slot keeps its last ones).</summary>
        public int RecordedCount => recording ? recordCount : 0;
        /// <summary>Whether a clip is kept (a slot that qualified, with at least <see cref="MinimumFrames"/> pictures).</summary>
        public bool HasClip => keptCount >= MinimumFrames;
        /// <summary>The kept clip's slot as the director rated it at its end (<see cref="LegacySlotRank.None"/> while none).</summary>
        public LegacySlotRank ClipRank { get; private set; }
        /// <summary>The two skills of the kept clip's slot, the player's side first (null: none there).</summary>
        public LegacySkill ClipPlayerSkill { get; private set; }
        public LegacySkill ClipEnemySkill { get; private set; }
        /// <summary>Pictures in the kept clip (0 while none).</summary>
        public int ClipFrameCount => HasClip ? keptCount : 0;
        /// <summary>Which clip (0 for A, 1 for B) is kept; the other records.</summary>
        public int KeptClip => keptBuffer;

        /// <summary>The picture place of the kept clip's <paramref name="index"/>-th picture, oldest first.</summary>
        public int ClipFrame(int index)
        {
            if (!HasClip || index < 0 || index >= keptCount) throw new ArgumentOutOfRangeException(nameof(index));
            return keptBuffer * Capacity + (keptHead - keptCount + index + Capacity) % Capacity;
        }

        /// <summary>Whether picture place <paramref name="frame"/> belongs to the kept clip's pictures (the recording's
        /// places may be released at the battle's end).</summary>
        public bool IsKeptFrame(int frame)
        {
            if (!HasClip || frame < keptBuffer * Capacity || frame >= (keptBuffer + 1) * Capacity) return false;
            int start = ((keptHead - keptCount) % Capacity + Capacity) % Capacity;
            int place = frame - keptBuffer * Capacity;
            return (place - start + Capacity) % Capacity < keptCount;
        }

        /// <summary>Whether a slot rated <paramref name="rank"/> at its end may be the clip
        /// (<see cref="LegacyReplayTuning.MinimumTier"/>; the finishing blow only with
        /// <see cref="LegacyReplayTuning.IncludesFinishingBlow"/>).</summary>
        public static bool Qualifies(LegacySlotRank rank, LegacyReplayTuning tuning)
            => rank.SlotIndex >= 0 && rank.Tier >= tuning.MinimumTier &&
               (rank.Tier != LegacySlotTier.Climax || tuning.IncludesFinishingBlow);

        /// <summary>Whether a slot rated <paramref name="start"/> as it starts is worth recording: something in it may still
        /// make it Major (an attack, or a read or a signature already known), and it could still outrank the kept clip (a
        /// kept finishing blow is never outranked).</summary>
        public bool IsCandidate(LegacySlotRank start)
        {
            if (start.SlotIndex < 0) return false;
            bool promising = start.Tier >= LegacySlotTier.Normal || start.HasPlayerRead ||
                start.EnemyReads != LegacyReadEvent.None || start.PlayerSignature || start.EnemySignature;
            return promising && !(HasClip && ClipRank.Tier == LegacySlotTier.Climax);
        }

        /// <summary>A slot begins (rated <paramref name="start"/>): a recording still open (a tail) ends first; a candidate
        /// starts recording into the clip not kept, its first picture due at once. Returns whether it records.</summary>
        public bool BeginSlot(LegacySlotRank start)
        {
            if (recording) Finish();
            if (!CanRecord || !IsCandidate(start)) return false;
            recording = true;
            RecordingSlot = start.SlotIndex;
            recordCount = recordHead = 0;
            sinceFrame = 0f;
            tailLeft = -1f;
            pendingRank = LegacySlotRank.None;
            pendingPlayerSkill = pendingEnemySkill = null;
            return true;
        }

        /// <summary>Moves the recording on by <paramref name="realSeconds"/>: true when a picture is due now, with the place
        /// to draw it into (<paramref name="frame"/>, -1 otherwise). The first picture is due at once, then one every
        /// <see cref="LegacyReplayTuning.FrameInterval"/> (never two in one call: a long frame is not caught up). A tail that
        /// runs out ends the recording (kept or dropped) instead.</summary>
        public bool Advance(float realSeconds, out int frame)
        {
            frame = -1;
            if (!recording) return false;
            realSeconds = float.IsNaN(realSeconds) || float.IsInfinity(realSeconds) || realSeconds < 0f ? 0f : realSeconds;
            if (tailLeft >= 0f)
            {
                tailLeft -= realSeconds;
                if (tailLeft <= 0f)
                {
                    Finish();
                    return false;
                }
            }
            float interval = Tuning.FrameInterval;
            sinceFrame += realSeconds;
            if (recordCount > 0 && sinceFrame + 1e-6f < interval) return false;
            // What is left over carries to the next picture; a frame long enough for several drops the backlog.
            sinceFrame = recordCount == 0 ? 0f : sinceFrame - interval;
            if (sinceFrame >= interval) sinceFrame = 0f;
            frame = (1 - keptBuffer) * Capacity + recordHead;
            recordHead = (recordHead + 1) % Capacity;
            recordCount = Math.Min(recordCount + 1, Capacity);
            return true;
        }

        /// <summary>The slot being recorded completed, rated <paramref name="final"/>, with these skills on the player's side
        /// and the enemy's: if it may be kept and outranks the kept clip, its tail is recorded
        /// (<see cref="LegacyReplayTuning.TailSeconds"/>; none: kept now); otherwise its pictures are dropped at once. Another
        /// slot's end changes nothing.</summary>
        public void EndSlot(LegacySlotRank final, LegacySkill playerSkill, LegacySkill enemySkill)
        {
            if (!recording || tailLeft >= 0f || final.SlotIndex != RecordingSlot) return;
            if (!Qualifies(final, Tuning) || HasClip && !final.Outranks(ClipRank))
            {
                Drop();
                return;
            }
            pendingRank = final;
            pendingPlayerSkill = playerSkill;
            pendingEnemySkill = enemySkill;
            if (Tuning.TailSeconds > 0f) tailLeft = Tuning.TailSeconds;
            else Finish();
        }

        /// <summary>Ends a recording now (the battle ended, or a scene interrupts it): a completed slot in its tail is kept as
        /// far as it got, an unfinished one is dropped. The kept clip stays.</summary>
        public void Stop()
        {
            if (recording) Finish();
        }

        /// <summary>Forgets everything: no recording, no clip (a reset, a new battle, the replay played).</summary>
        public void Clear()
        {
            Drop();
            keptBuffer = keptCount = keptHead = 0;
            ClipRank = LegacySlotRank.None;
            ClipPlayerSkill = ClipEnemySkill = null;
        }

        /// <summary>The kept clip's picture at <paramref name="elapsed"/> real seconds into the replay (its pictures at
        /// <see cref="LegacyReplayTuning.FramesPerSecond"/> times <see cref="LegacyReplayTuning.PlaybackSpeed"/>; the last
        /// one held after its end), as an index into the clip (<see cref="ClipFrame"/>).</summary>
        public static int PlaybackIndex(float elapsed, int count, LegacyReplayTuning tuning)
        {
            if (count <= 0) return 0;
            if (float.IsNaN(elapsed) || elapsed <= 0f) return 0;
            double index = Math.Floor(elapsed * (double)tuning.FramesPerSecond * tuning.PlaybackSpeed + 1e-6);
            return index >= count - 1 ? count - 1 : (int)index;
        }

        /// <summary>Real seconds the replay's pictures take (before its last one is held).</summary>
        public static float PlaybackSeconds(int count, LegacyReplayTuning tuning)
            => count <= 0 ? 0f : count / (tuning.FramesPerSecond * tuning.PlaybackSpeed);

        // The recording ends: a completed slot that may be kept takes the kept clip's place (the old one's places record
        // next); anything else is dropped.
        private void Finish()
        {
            if (recording && pendingRank.SlotIndex >= 0 && recordCount >= MinimumFrames && Qualifies(pendingRank, Tuning) &&
                (!HasClip || pendingRank.Outranks(ClipRank)))
            {
                keptBuffer = 1 - keptBuffer;
                keptCount = recordCount;
                keptHead = recordHead;
                ClipRank = pendingRank;
                ClipPlayerSkill = pendingPlayerSkill;
                ClipEnemySkill = pendingEnemySkill;
            }
            Drop();
        }

        private void Drop()
        {
            recording = false;
            RecordingSlot = -1;
            recordCount = recordHead = 0;
            sinceFrame = 0f;
            tailLeft = -1f;
            pendingRank = LegacySlotRank.None;
            pendingPlayerSkill = pendingEnemySkill = null;
        }
    }
}
