using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Prologue
{
    /// <summary>Progress through the linear story chain. Completing the current mission (winning it, or losing the 서막's
    /// last mission after 이아's 수훈) opens the next; the 서막 missions (those needing no cleared stage) open the lobby,
    /// and each later mission also waits for its stage. Replays never move progress. What the completed missions unlock
    /// is derived from the count, so saving the count is enough.</summary>
    public sealed class PrologueRun
    {
        /// <summary>What the player always has once the story starts: the Q lane.</summary>
        public const CombatFeature BaseFeatures = CombatFeature.LaneQ;
        private readonly IReadOnlyList<PrologueMission> missions;

        public PrologueRun() : this(StoryMissions.All) { }

        public PrologueRun(IReadOnlyList<PrologueMission> missions)
        {
            if (missions == null || missions.Count == 0) throw new ArgumentException("An arc needs missions.", nameof(missions));
            this.missions = missions;
            int arc = 0;
            while (arc < missions.Count && missions[arc].RequiredClearedStage == 0) arc++;
            for (int index = arc; index < missions.Count; index++)
                if (missions[index].RequiredClearedStage == 0)
                    throw new ArgumentException("Missions that need no stage come first.", nameof(missions));
            ArcMissionCount = arc;
        }

        public IReadOnlyList<PrologueMission> Missions => missions;
        public int MissionCount => missions.Count;
        /// <summary>The leading missions that need no cleared stage: the 서막 before the lobby.</summary>
        public int ArcMissionCount { get; }
        /// <summary>Missions won in order.</summary>
        public int ClearedCount { get; private set; }
        /// <summary>Every story mission is won.</summary>
        public bool IsComplete => ClearedCount >= missions.Count;
        /// <summary>The 서막 is over, so the lobby is open.</summary>
        public bool IsArcComplete => ClearedCount >= ArcMissionCount;

        /// <summary>The Q lane plus everything the won missions opened (넘기기 opens with mission 1).</summary>
        public CombatFeature UnlockedFeatures
        {
            get
            {
                CombatFeature features = BaseFeatures;
                for (int index = 0; index < ClearedCount && index < missions.Count; index++) features |= missions[index].Unlocks;
                return features;
            }
        }

        /// <summary>Whether the next mission can be played now: it exists and its stage (if any) is cleared.</summary>
        public bool CanPlayCurrent(Func<int, bool> isStageCleared)
        {
            if (isStageCleared == null) throw new ArgumentNullException(nameof(isStageCleared));
            PrologueMission current = CurrentMission;
            return current != null && (current.RequiredClearedStage == 0 || isStageCleared(current.RequiredClearedStage));
        }

        /// <summary>The highest stage the story allows: 0 during the 서막, then the stage the next unwon mission waits
        /// for (the stages after it wait for that mission), or every stage once the chain is won.</summary>
        public int StageLimit
        {
            get
            {
                if (!IsArcComplete) return 0;
                PrologueMission current = CurrentMission;
                return current == null ? int.MaxValue : current.RequiredClearedStage;
            }
        }
        /// <summary>The next mission to play, or null once the arc is complete.</summary>
        public PrologueMission CurrentMission => IsComplete ? null : missions[ClearedCount];

        public bool IsCleared(int number) => number >= 1 && number <= ClearedCount;

        /// <summary>Records a finished mission. Returns true only when this first completion opened the next mission (or
        /// completed the arc). A win completes a mission; so does a defeat after a forced-loss empowerment was applied
        /// (<paramref name="empowered"/>, <see cref="PrologueMission.Completes"/>), as in the 서막's last mission.</summary>
        public bool TryComplete(int number, DuelMatchOutcome outcome, bool empowered = false)
        {
            if (number != ClearedCount + 1 || IsComplete || !missions[number - 1].Completes(outcome, empowered)) return false;
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
