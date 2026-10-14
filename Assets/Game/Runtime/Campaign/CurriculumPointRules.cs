using System;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>How 수련 점수 are earned (Docs/Curriculum.md). Battles and story missions pay points into one balance per
    /// journey (<see cref="CampaignRun.CurriculumPoints"/>), whether or not the curriculum is open yet, and curriculum
    /// nodes cost them (<see cref="CurriculumNode.Cost"/>). Every number is a placeholder the author tunes here.</summary>
    public static class CurriculumPointRules
    {
        /// <summary>A finished stage battle, whatever its outcome; replays and result-screen retries count. Abandoned
        /// battles, training (허수아비) fights and story missions do not.</summary>
        public const int Battle = 1;
        /// <summary>That battle was won.</summary>
        public const int Victory = 1;
        /// <summary>That win was the stage's first clear.</summary>
        public const int FirstClear = 2;
        /// <summary>A story mission's first completion (서막 and 수련 missions, once each).</summary>
        public const int StoryMission = 1;

        /// <summary>What a stage's first clear earns at least (its winning battle): 4. An older save without a points line
        /// is credited this per cleared stage (<see cref="CampaignRun.CurriculumPointsIn"/>).</summary>
        public const int FirstClearedStage = Battle + Victory + FirstClear;

        /// <summary>What a stage battle earns.</summary>
        public static CurriculumPointGain ForStageBattle(bool victory, bool firstClear)
            => new CurriculumPointGain(Battle, victory ? Victory : 0, victory && firstClear ? FirstClear : 0);

        /// <summary>What a story mission's first completion earns.</summary>
        public static CurriculumPointGain ForStoryMission() => new CurriculumPointGain(mission: StoryMission);
    }

    /// <summary>The 수련 점수 one result earned, by source, for the result screen's breakdown
    /// (<c>전투 +1 · 승리 +1 · 첫 클리어 +2</c>).</summary>
    public readonly struct CurriculumPointGain : IEquatable<CurriculumPointGain>
    {
        public CurriculumPointGain(int battle = 0, int victory = 0, int firstClear = 0, int mission = 0)
        {
            if (battle < 0) throw new ArgumentOutOfRangeException(nameof(battle));
            if (victory < 0) throw new ArgumentOutOfRangeException(nameof(victory));
            if (firstClear < 0) throw new ArgumentOutOfRangeException(nameof(firstClear));
            if (mission < 0) throw new ArgumentOutOfRangeException(nameof(mission));
            Battle = battle;
            Victory = victory;
            FirstClear = firstClear;
            Mission = mission;
        }

        public int Battle { get; }
        public int Victory { get; }
        public int FirstClear { get; }
        /// <summary>A story mission's first completion.</summary>
        public int Mission { get; }
        public int Total => checked(Battle + Victory + FirstClear + Mission);
        public bool IsEmpty => Battle == 0 && Victory == 0 && FirstClear == 0 && Mission == 0;

        public bool Equals(CurriculumPointGain other)
            => Battle == other.Battle && Victory == other.Victory && FirstClear == other.FirstClear && Mission == other.Mission;

        public override bool Equals(object obj) => obj is CurriculumPointGain other && Equals(other);

        public override int GetHashCode() => ((Battle * 31 + Victory) * 31 + FirstClear) * 31 + Mission;

        public override string ToString() => $"전투 +{Battle} · 승리 +{Victory} · 첫 클리어 +{FirstClear} · 임무 +{Mission}";
    }
}
