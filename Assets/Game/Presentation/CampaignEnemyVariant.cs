using System;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Presentation
{
    /// <summary>One stable opponent identity for each school stage, shared by combat and its previews.</summary>
    public static class CampaignEnemyVariant
    {
        public static EnemyAppearance ForStage(int stageNumber)
        {
            if (stageNumber < 1) throw new ArgumentOutOfRangeException(nameof(stageNumber));
            if (stageNumber == GreenRibbonBossBattle.StageNumber) return EnemyAppearance.Student;
            return (stageNumber & 1) == 1 ? EnemyAppearance.CadetA : EnemyAppearance.CadetB;
        }

        public static string SilhouetteResource(int stageNumber)
        {
            EnemyAppearance appearance = ForStage(stageNumber);
            string root = appearance == EnemyAppearance.Student ? EnemyStudentAnimationSet.ResourceRoot
                : appearance == EnemyAppearance.CadetA ? EnemyStudentAnimationSet.CadetAResourceRoot
                : EnemyStudentAnimationSet.CadetBResourceRoot;
            return root + "idle/frame-01";
        }
    }
}
