using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The combat features a duel allows. Story missions open them one at a time; a duel built without
    /// an explicit set allows everything (the long-standing default for stages and tests).</summary>
    [Flags]
    public enum CombatFeature
    {
        None = 0,
        LaneQ = 1,
        LaneW = 2,
        LaneE = 4,
        /// <summary>숨고르기: the common Wait action (S).</summary>
        Breath = 8,
        /// <summary>A step: evade the incoming skill.</summary>
        Dodge = 16,
        /// <summary>D step: press the outgoing skill.</summary>
        Pressure = 32,
        All = LaneQ | LaneW | LaneE | Breath | Dodge | Pressure,
    }

    public static class CombatFeatures
    {
        public static CombatFeature Lane(int laneIndex)
        {
            switch (laneIndex)
            {
                case 0: return CombatFeature.LaneQ;
                case 1: return CombatFeature.LaneW;
                case 2: return CombatFeature.LaneE;
                default: throw new ArgumentOutOfRangeException(nameof(laneIndex));
            }
        }

        public static bool HasLane(this CombatFeature features, int laneIndex)
            => laneIndex >= 0 && laneIndex <= 2 && (features & Lane(laneIndex)) != 0;

        public static bool Has(this CombatFeature features, CombatFeature feature)
            => feature != CombatFeature.None && (features & feature) == feature;

        public static bool AllowsStep(this CombatFeature features, LegacyStepAction action)
            => action == LegacyStepAction.Dodge ? features.Has(CombatFeature.Dodge)
                : action == LegacyStepAction.Pressure && features.Has(CombatFeature.Pressure);

        /// <summary>Whether any step (dodge or pressure) is open.</summary>
        public static bool AllowsAnyStep(this CombatFeature features)
            => (features & (CombatFeature.Dodge | CombatFeature.Pressure)) != 0;

        public static int LaneCount(this CombatFeature features)
        {
            int count = 0;
            for (int lane = 0; lane < 3; lane++) if (features.HasLane(lane)) count++;
            return count;
        }
    }
}
