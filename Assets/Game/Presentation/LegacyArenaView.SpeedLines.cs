using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private readonly LegacyReengageMotion reengageMotion = new LegacyReengageMotion();
        private DuelReengageSpeedLines reengageSpeedLines;
        private Actor reengageSampleEnemy;

        private void InitializeReengageSpeedLines() => reengageSpeedLines =
            new DuelReengageSpeedLines(arenaRoot.transform, breakMaterial, ArenaLayer);

        private void NotifyReengageContact() => reengageMotion.NotifyContact();

        private void TickReengageSpeedLines(float scaledDelta, float realDelta)
        {
            if (reengageSampleEnemy != enemy)
            {
                reengageMotion.Clear();
                reengageSampleEnemy = enemy;
            }
            bool eligible = settings.ReengageSpeedLinesEnabled && settings.ReengageSpeedLinesStrength > 0f &&
                suspension == null && resolving && !returning && (approaching || IsPursuing) &&
                !stepping && !HasPendingPush && !IsBladeBinding && !IsResistanceImpactPlaying &&
                !ContrastImpactActive && !finishingFocus && !IsFatalFocus && scaledDelta > 0f && realDelta > 0f;
            float strength = reengageMotion.Sample(player.Renderer.transform.position.x,
                enemy.Renderer.transform.position.x, realDelta, eligible) * settings.ReengageSpeedLinesStrength;
            reengageSpeedLines?.Tick(ArenaCamera, (PlayerGroundPosition + EnemyGroundPosition) * .5f,
                reengageMotion.Direction, strength, realDelta, player.Renderer.sortingLayerID);
        }

        private void ClearReengageSpeedLines()
        {
            reengageMotion.Clear();
            reengageSpeedLines?.Clear();
        }

        private void ResetReengageSpeedLines()
        {
            reengageMotion.Reset();
            reengageSampleEnemy = null;
            reengageSpeedLines?.Clear();
        }

        private void DisposeReengageSpeedLines() => reengageSpeedLines?.Dispose();
    }
}
