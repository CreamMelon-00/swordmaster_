using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>What a coached beat waits for before the next one appears.</summary>
    public enum MissionGuideStepKind
    {
        /// <summary>An explanation; the continue button or Enter moves on. Queueing and committing are locked.</summary>
        Info,
        /// <summary>Waits for a successful queue from one lane.</summary>
        Queue,
        /// <summary>Waits for the enemy queue to be inspected (Tab).</summary>
        Inspect,
        /// <summary>Waits for the turn to be committed.</summary>
        Commit,
        /// <summary>Waits for the committed turn to resolve and the next one to begin.</summary>
        WatchTurn,
        /// <summary>The last beat: everything the mission allows is open until the duel ends.</summary>
        Free,
    }

    /// <summary>One coached beat of a mission: its copy and the input it waits for.</summary>
    public sealed class MissionGuideBeat
    {
        public MissionGuideBeat(MissionGuideStepKind kind, string title, string description, string inputHint,
            int lane = -1, bool focusAct = false)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A beat needs a title.", nameof(title));
            if (kind == MissionGuideStepKind.Queue && (lane < 0 || lane > 2))
                throw new ArgumentOutOfRangeException(nameof(lane), "A queue beat names the lane it waits for.");
            Kind = kind;
            Title = title;
            Description = description ?? string.Empty;
            InputHint = inputHint ?? string.Empty;
            Lane = kind == MissionGuideStepKind.Queue ? lane : -1;
            FocusAct = focusAct;
        }

        public MissionGuideStepKind Kind { get; }
        public string Title { get; }
        public string Description { get; }
        public string InputHint { get; }
        /// <summary>The lane a queue beat waits for, or -1.</summary>
        public int Lane { get; }
        /// <summary>Highlights the ACT gauge while this beat is shown.</summary>
        public bool FocusAct { get; }
    }

    /// <summary>Walks a mission's coached beats as the player acts. Guidance only follows successful
    /// actions; combat stays authoritative in LegacyQueuedDuel.</summary>
    public sealed class MissionGuide
    {
        private readonly MissionGuideBeat[] beats;

        public MissionGuide(IReadOnlyList<MissionGuideBeat> beats)
        {
            if (beats == null || beats.Count == 0) throw new ArgumentException("A guide needs at least one beat.", nameof(beats));
            this.beats = new MissionGuideBeat[beats.Count];
            for (int i = 0; i < beats.Count; i++)
                this.beats[i] = beats[i] ?? throw new ArgumentException("A guide beat cannot be null.", nameof(beats));
            if (this.beats[this.beats.Length - 1].Kind != MissionGuideStepKind.Free)
                throw new ArgumentException("A guide ends with a free beat.", nameof(beats));
        }

        public IReadOnlyList<MissionGuideBeat> Beats => beats;
        public int StepIndex { get; private set; }
        public bool IsComplete { get; private set; }
        public int StepCount => beats.Length;
        /// <summary>One-based, for the coach's counter.</summary>
        public int StepNumber => Math.Min(StepIndex + 1, beats.Length);
        public MissionGuideBeat Current => beats[Math.Min(StepIndex, beats.Length - 1)];
        public MissionGuideStepKind Kind => Current.Kind;
        public string Title => Current.Title;
        public string Description => Current.Description;
        public string InputHint => Current.InputHint;
        public bool IsFree => Kind == MissionGuideStepKind.Free;
        public bool CanAdvance => !IsComplete && Kind == MissionGuideStepKind.Info;
        public bool AllowsCommit => !IsComplete && (Kind == MissionGuideStepKind.Commit || IsFree);
        public bool AllowsInspect => !IsComplete && Kind == MissionGuideStepKind.Inspect;
        public int ExpectedLane => Kind == MissionGuideStepKind.Queue ? Current.Lane : -1;
        public bool FocusesCommit => Kind == MissionGuideStepKind.Commit;
        public bool FocusesEnemyQueue => Kind == MissionGuideStepKind.Inspect;
        public bool FocusesAct => Current.FocusAct;
        /// <summary>The first beat, before the player has done anything.</summary>
        public bool IsOpening => StepIndex == 0;

        public bool AllowsQueue(int lane)
            => !IsComplete && lane >= 0 && lane <= 2 && (IsFree || ExpectedLane == lane);

        public bool TryAdvance()
        {
            if (!CanAdvance) return false;
            MoveNext();
            return true;
        }

        public void NotifyQueued(int lane)
        {
            if (Kind == MissionGuideStepKind.Queue && lane == ExpectedLane) MoveNext();
        }

        public void NotifyInspected()
        {
            if (Kind == MissionGuideStepKind.Inspect) MoveNext();
        }

        public void NotifyCommitted()
        {
            if (Kind == MissionGuideStepKind.Commit) MoveNext();
        }

        public void NotifyTurnBegan(int round)
        {
            if (Kind == MissionGuideStepKind.WatchTurn && round > 1) MoveNext();
        }

        public void Finish() => IsComplete = true;

        private void MoveNext()
        {
            if (StepIndex < beats.Length - 1) StepIndex++;
        }
    }
}
