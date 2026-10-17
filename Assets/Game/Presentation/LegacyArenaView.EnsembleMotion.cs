using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private const float UpcomingEnemyStandoff = ContactDistance + 1.2f;
        private int upcomingEnemyIndex = -1;
        private LegacySkill upcomingEnemySkill;
        private bool upcomingEnemyReady, rosterPlanningPlayerParting, rosterPlanningStaging;

        /// <summary>The next enemy preparing outside the current exchange, or -1. This never selects a combat target.</summary>
        public int UpcomingEnemyIndex => upcomingEnemyIndex;

        /// <summary>Presentation intent only. A future reservation may approach and show its first preparation cel,
        /// without assigning the actor a skill or advancing its attacks. -1 or null clears the intent.</summary>
        public void SetUpcomingEnemy(int enemyIndex, LegacySkill skill)
        {
            if (disposed) return;
            if (enemyFigures.Count <= 1 || suspension != null || enemyIndex < 0 || skill == null || skill.IsWait ||
                enemyIndex >= enemyFigures.Count || enemyIndex == activeEnemyIndex || FigureAt(enemyIndex).Actor.Defeated)
            {
                ClearUpcomingEnemy();
                return;
            }
            if (upcomingEnemyIndex == enemyIndex && ReferenceEquals(upcomingEnemySkill, skill)) return;
            ClearUpcomingEnemy();
            upcomingEnemyIndex = enemyIndex;
            upcomingEnemySkill = skill;
            EnemyFigure figure = FigureAt(enemyIndex);
            figure.Retiring = false;
        }

        private void ClearUpcomingEnemy()
        {
            int previous = upcomingEnemyIndex;
            upcomingEnemyIndex = -1;
            upcomingEnemySkill = null;
            upcomingEnemyReady = false;
            if ((uint)previous < (uint)enemyFigures.Count) SampleActor(FigureAt(previous).Actor);
        }

        private void ResetEnemyEnsembleIntent()
        {
            ClearUpcomingEnemy();
            rosterPlanningPlayerParting = rosterPlanningStaging = false;
            for (int i = 0; i < enemyFigures.Count; i++) enemyFigures[i].PlanningParting = false;
        }

        private void TickUpcomingEnemy(EnemyFigure figure, float delta)
        {
            Actor actor = figure.Actor;
            if (actor.Defeated || actor.Pushing || suspension != null)
            {
                upcomingEnemyReady = false;
                return;
            }
            Vector2 playerPosition = player.Renderer.transform.localPosition;
            Vector2 position = actor.Renderer.transform.localPosition;
            Vector2 target = new Vector2(playerPosition.x + UpcomingEnemyStandoff, position.y);
            Vector2 error = target - position;
            if (!settings.AutoDepthEnabled) error.y = 0f;
            upcomingEnemyReady = error.sqrMagnitude <= .0001f && actor.GroundVelocity.sqrMagnitude <= .0001f;
            if (!upcomingEnemyReady)
                TravelOnGround(actor, error, ApproachSpeed * .55f * settings.MovementSpeedMultiplier, delta,
                    settings.EnemyHandoffSpeedMultiplier, true);
        }

        private bool TrySampleUpcomingPreparation(Actor actor)
        {
            if (!upcomingEnemyReady || upcomingEnemyIndex < 0 || suspension != null || actor.Moving ||
                actor.ReactionTime > 0f || actor.Skill != null || FigureAt(upcomingEnemyIndex).Actor != actor) return false;
            EnemyStudentAnimationSet animationSet = AnimationsFor(actor);
            if (!animationSet.HasRequiredAssets) return false;
            Sprite preparation = upcomingEnemySkill.Kind == LegacySkillKind.Defence ? animationSet.GetGuard(0)
                : animationSet.GetAttack(upcomingEnemySkill.Property, 0f, 0);
            if (preparation == null) return false;
            actor.Renderer.sprite = preparation;
            return true;
        }

        private float ClosestEnemySeparation
        {
            get
            {
                float closest = float.PositiveInfinity;
                float playerX = player.Renderer.transform.localPosition.x;
                for (int i = 0; i < enemyFigures.Count; i++)
                {
                    Actor actor = enemyFigures[i].Actor;
                    if (!actor.Defeated) closest = Mathf.Min(closest, actor.Renderer.transform.localPosition.x - playerX);
                }
                return float.IsPositiveInfinity(closest) ? Separation : closest;
            }
        }

        private void BeginRosterPlanning()
        {
            ClearUpcomingEnemy();
            float target = settings.BattleStagingSeparation;
            rosterPlanningPlayerParting = ClosestEnemySeparation < target - .001f;
            rosterPlanningStaging = rosterPlanningPlayerParting;
            for (int i = 0; i < enemyFigures.Count; i++)
            {
                EnemyFigure figure = enemyFigures[i];
                figure.Retiring = false;
                figure.PlanningParting = !figure.Actor.Defeated &&
                    figure.Actor.Renderer.transform.localPosition.x - player.Renderer.transform.localPosition.x < target - .001f;
            }
        }

        // The player reads the nearest body; each enemy then closes its own actual gap on the same frame.
        // The original duel branch is kept intact, including its clock and staging behavior.
        private float TickRosterPlanning(float scaledDelta, float realDelta)
        {
            if (rosterPlanningStaging && ClosestEnemySeparation >= settings.BattleStagingSeparation - .001f)
            {
                rosterPlanningStaging = rosterPlanningPlayerParting = false;
                for (int i = 0; i < enemyFigures.Count; i++) enemyFigures[i].PlanningParting = false;
            }
            float sampleDelta = scaledDelta;
            if (!player.Pushing)
            {
                if (rosterPlanningPlayerParting)
                {
                    sampleDelta = realDelta;
                    float shortfall = settings.BattleStagingSeparation - ClosestEnemySeparation;
                    if (shortfall > .001f)
                        TravelOnGround(player, new Vector2(-shortfall * .5f, 0f), BulletTimePartSpeed, realDelta);
                    else rosterPlanningPlayerParting = false;
                }
                else if (!rosterPlanningStaging)
                {
                    float excess = ClosestEnemySeparation - Mathf.Max(ContactDistance, settings.BattleDriftMinimumSeparation);
                    if (excess > 0f)
                        TravelOnGround(player, new Vector2(excess * .5f, 0f), PlanningDriftSpeed, scaledDelta);
                }
            }
            float enemyDelta = TickPlanningEnemy(FigureAt(activeEnemyIndex), activeEnemyIndex, scaledDelta, realDelta);
            return Mathf.Max(sampleDelta, enemyDelta);
        }

        private float PlanningDriftSpeed => settings.BattleDriftSpeed *
            (bulletTimeReleasing ? BulletTimeReleaseDriftScale : 1f);

        private float TickPlanningEnemy(EnemyFigure figure, int index, float scaledDelta, float realDelta)
        {
            Actor actor = figure.Actor;
            bool parting = figure.PlanningParting;
            float delta = parting ? realDelta : scaledDelta;
            if (actor.Defeated || actor.Pushing || actor.Appearance == EnemyAppearance.TrainingDummy) return delta;
            float separation = actor.Renderer.transform.localPosition.x - player.Renderer.transform.localPosition.x;
            if (parting)
            {
                float shortfall = settings.BattleStagingSeparation - separation;
                if (shortfall > .001f)
                    TravelOnGround(actor, new Vector2(shortfall * (rosterPlanningPlayerParting ? .5f : 1f), 0f),
                        BulletTimePartSpeed * settings.EnemyPlanningSpeedFactor(index), realDelta);
                else figure.PlanningParting = false;
            }
            else if (!rosterPlanningStaging)
            {
                float excess = separation - Mathf.Max(ContactDistance, settings.BattleDriftMinimumSeparation);
                if (excess > 0f)
                    TravelOnGround(actor, new Vector2(-excess * .5f, 0f),
                        PlanningDriftSpeed * settings.EnemyPlanningSpeedFactor(index), scaledDelta);
            }
            return delta;
        }
    }
}
