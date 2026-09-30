using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>Progress through the linear opening arc. Winning the current mission opens the next;
    /// clearing the last one completes the arc and opens the lobby. Replays never move progress.</summary>
    public sealed class PrologueRun
    {
        private readonly IReadOnlyList<PrologueMission> missions;

        public PrologueRun() : this(PrologueMissions.All) { }

        public PrologueRun(IReadOnlyList<PrologueMission> missions)
        {
            if (missions == null || missions.Count == 0) throw new ArgumentException("An arc needs missions.", nameof(missions));
            this.missions = missions;
        }

        public IReadOnlyList<PrologueMission> Missions => missions;
        public int MissionCount => missions.Count;
        /// <summary>Missions won in order.</summary>
        public int ClearedCount { get; private set; }
        public bool IsComplete => ClearedCount >= missions.Count;
        /// <summary>The next mission to play, or null once the arc is complete.</summary>
        public PrologueMission CurrentMission => IsComplete ? null : missions[ClearedCount];

        public bool IsCleared(int number) => number >= 1 && number <= ClearedCount;

        /// <summary>Records a finished mission. Returns true only when this win opened the next mission (or completed the arc).</summary>
        public bool TryComplete(int number, DuelMatchOutcome outcome)
        {
            if (outcome != DuelMatchOutcome.PlayerVictory || number != ClearedCount + 1 || IsComplete) return false;
            ClearedCount++;
            return true;
        }

        /// <summary>Treats the whole arc as done, as if every mission had been won.</summary>
        public void CompleteAll() => ClearedCount = missions.Count;

        /// <summary>Restores saved progress. Returns false, changing nothing, when the count is outside 0..MissionCount.</summary>
        public bool TryRestore(int clearedCount)
        {
            if (clearedCount < 0 || clearedCount > missions.Count) return false;
            ClearedCount = clearedCount;
            return true;
        }

        public void Reset() => ClearedCount = 0;
    }
}
