using System;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Presentation
{
    /// <summary>One stable opponent identity for each school stage, shared by combat and its previews.</summary>
    public static class CampaignEnemyVariant
    {
        public static EnemyAppearance ForStage(int stageNumber)
        {
            if (stageNumber < 1) throw new ArgumentOutOfRangeException(nameof(stageNumber));
            return (stageNumber & 1) == 1 ? EnemyAppearance.CadetA : EnemyAppearance.CadetB;
        }

        public static string SilhouetteResource(int stageNumber)
        {
            return (ForStage(stageNumber) == EnemyAppearance.CadetA
                ? EnemyStudentAnimationSet.CadetAResourceRoot : EnemyStudentAnimationSet.CadetBResourceRoot) + "idle/frame-01";
        }
    }
}
