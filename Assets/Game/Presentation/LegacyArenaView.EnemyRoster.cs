using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private const int MaximumEnemyFigures = 3;
        private const float DefeatFadeSeconds = .65f;
        private readonly List<EnemyFigure> enemyFigures = new List<EnemyFigure>(MaximumEnemyFigures);
        private readonly List<Transform> enemyActorTransforms = new List<Transform>(MaximumEnemyFigures);
        private int activeEnemyIndex;
        private bool enemyHandoffTravel, enemySideRecovery;

        private sealed class EnemyFigure
        {
            public Actor Actor;
            public DuelBreakAura BreakAura;
            public DuelPowerAura PowerAura;
            public DuelAuraAfterimages Afterimages;
            public SortingGroup DepthGroup;
            public DuelGroundCircle Circle;
            public bool Retiring, VisibleBeforeCutscene;
            public bool PlanningParting;
            public Vector2 RetreatTarget;
        }

        public int EnemyCount => enemyFigures.Count;
        public int ActiveEnemyIndex => activeEnemyIndex;
        /// <summary>Stable, cached body anchors in the same order as the encounter's enemy state.</summary>
        public IReadOnlyList<Transform> EnemyActorTransforms => enemyActorTransforms;

        public SpriteRenderer GetEnemyRenderer(int index) => FigureAt(index).Actor.Renderer;
        public Vector3 GetEnemyGroundPosition(int index) => GroundPosition(FigureAt(index).Actor);
        public Vector2 GetEnemyScreenAnchor(int index) => ScreenAnchor(GetEnemyRenderer(index).transform.position + Vector3.up * 2f);
        public Transform GetEnemyGroundCircle(int index) => FigureAt(index).Circle.Root;
        public int GetEnemyDepthSortingOrder(int index) => FigureAt(index).DepthGroup.sortingOrder;

        private EnemyFigure FigureAt(int index)
        {
            if ((uint)index >= (uint)enemyFigures.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return enemyFigures[index];
        }

        private EnemyFigure FigureFor(Actor actor)
        {
            for (int i = 0; i < enemyFigures.Count; i++)
                if (enemyFigures[i].Actor == actor) return enemyFigures[i];
            return null;
        }

        private EnemyStudentAnimationSet AnimationsFor(Actor actor) => actor.AnimationSet ?? ActiveEnemyAnimations;

        private void InitializeEnemyRoster()
        {
            enemy.Appearance = enemyAppearance;
            enemy.AnimationSet = enemyAnimations;
            enemyFigures.Add(new EnemyFigure
            {
                Actor = enemy, BreakAura = enemyBreakAura, PowerAura = enemyPowerAura,
                Afterimages = enemyAuraAfterimages, DepthGroup = enemyDepthGroup, Circle = enemyGroundCircle,
            });
            enemyActorTransforms.Add(enemy.Renderer.transform);
        }

        /// <summary>Creates an encounter's bodies after Reset. A null or one-member roster restores the original duel.
        /// Reconfiguring a three-member encounter reuses its bodies and floor guides.</summary>
        public void ConfigureEnemyRoster(IReadOnlyList<EnemyAppearance> appearances)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LegacyArenaView));
            if (appearances != null && appearances.Count > MaximumEnemyFigures)
                throw new ArgumentOutOfRangeException(nameof(appearances));
            if (suspension != null) throw new InvalidOperationException("A cutscene is borrowing the arena's figures.");
            SelectActiveEnemy(0);
            int count = appearances != null && appearances.Count > 1 ? appearances.Count : 1;
            while (enemyFigures.Count > count) RemoveLastEnemyFigure();
            while (enemyFigures.Count < count) AddEnemyFigure(enemyFigures.Count);
            for (int i = 0; appearances != null && i < appearances.Count; i++)
            {
                SelectEnemyContext(i);
                SetEnemyAppearance(appearances[i]);
            }
            SelectEnemyContext(0);
            int ghostOrder = count > 1 ? -4 : -2;
            stepAfterimages.GroupedSortingCeiling = ghostOrder;
            ClearSlowMotionAfterimages();
            slowAfterimages.GroupedSortingCeiling = ghostOrder;
            attackEchoes.GroupedSortingCeiling = ghostOrder;
            if (signatureAfterimages != null) signatureAfterimages.GroupedSortingCeiling = ghostOrder;
            for (int i = 0; i < enemyFigures.Count; i++) enemyFigures[i].Afterimages.GroupedSortingCeiling = ghostOrder;
            ResetEnemyRoster();
            RefreshDepthPresentation();
        }

        private void AddEnemyFigure(int index)
        {
            Actor actor = CreateActor("Enemy" + index, enemyAnimations.GetIdle(0f));
            actor.Appearance = EnemyAppearance.Student;
            actor.AnimationSet = enemyAnimations;
            if (attackSpriteMaterial != null) actor.Renderer.sharedMaterial = attackSpriteMaterial;
            var shadowSprites = Resources.LoadAll<Sprite>("LegacyArena/Shadow/Circle");
            SpriteRenderer shadow = CreateShadow(actor, shadowSprites.Length > 0 ? shadowSprites[0] : null, 0f);
            var breakAura = new DuelBreakAura(actor.Renderer, null, shadow, breakMaterial, ArenaLayer);
            var powerAura = new DuelPowerAura(actor.Renderer.transform, spriteMaterial, ArenaLayer, 23 + index * 17);
            var afterimages = new DuelAuraAfterimages(actor.Renderer, arenaRoot.transform,
                breakMaterial != null ? breakMaterial : spriteMaterial, ArenaLayer, settings);
            powerAura.Afterimages = afterimages;
            enemyFigures.Add(new EnemyFigure
            {
                Actor = actor, BreakAura = breakAura, PowerAura = powerAura, Afterimages = afterimages,
                DepthGroup = AddDepthGroup(actor.Renderer),
                Circle = new DuelGroundCircle("Enemy " + index + " Ground Circle", arenaRoot.transform, ArenaLayer,
                    actor.Renderer.sortingLayerID, new Color(.48f, .68f, .81f, .62f)),
            });
            enemyActorTransforms.Add(actor.Renderer.transform);
        }

        private void RemoveLastEnemyFigure()
        {
            int index = enemyFigures.Count - 1;
            if (index <= 0) return;
            EnemyFigure figure = enemyFigures[index];
            figure.BreakAura.Dispose();
            figure.PowerAura.Dispose();
            figure.Afterimages.Dispose();
            figure.Circle.Dispose();
            Object.Destroy(figure.Actor.Renderer.gameObject);
            enemyFigures.RemoveAt(index);
            enemyActorTransforms.RemoveAt(index);
        }

        private void DisposeAdditionalEnemies()
        {
            while (enemyFigures.Count > 1) RemoveLastEnemyFigure();
        }

        private void SelectEnemyContext(int index)
        {
            EnemyFigure figure = FigureAt(index);
            activeEnemyIndex = index;
            enemy = figure.Actor;
            enemyAppearance = enemy.Appearance;
            enemyBreakAura = figure.BreakAura;
            enemyPowerAura = figure.PowerAura;
            enemyAuraAfterimages = figure.Afterimages;
            enemyDepthGroup = figure.DepthGroup;
            enemyGroundCircle = figure.Circle;
        }

        /// <summary>Hands the exchange to an existing body without relocating it. The controller closes the real distance
        /// before beginning the new skill; the outgoing enemy clears a short space beside that approach line.</summary>
        public void SelectActiveEnemy(int index)
        {
            EnemyFigure incoming = FigureAt(index);
            if (index == activeEnemyIndex) return;
            if (suspension != null) throw new InvalidOperationException("A cutscene is borrowing the arena's figures.");
            ClearUpcomingEnemy();
            ClearBladeBind();
            ClearSignatureTrails(false);
            EnemyFigure outgoing = FigureAt(activeEnemyIndex);
            outgoing.Actor.Skill = null;
            outgoing.Actor.Chasing = false;
            ClearAttackMotion(outgoing.Actor, true);
            if (resolving && !outgoing.Actor.Defeated) BeginEnemyRetreat(outgoing, incoming, activeEnemyIndex);
            incoming.Retiring = false;
            player.Chasing = false;
            enemyHandoffTravel = resolving;
            enemySideRecovery = false;
            SelectEnemyContext(index);
            RefreshDepthPresentation();
        }

        private void BeginEnemyRetreat(EnemyFigure outgoing, EnemyFigure incoming, int outgoingIndex)
        {
            Vector2 playerPosition = player.Renderer.transform.localPosition;
            Vector2 current = outgoing.Actor.Renderer.transform.localPosition;
            Vector2 incomingPosition = incoming.Actor.Renderer.transform.localPosition;
            Vector2 line = incomingPosition - playerPosition;
            Vector2 sideways = new Vector2(-line.y, Mathf.Max(.1f, line.x)).normalized;
            float side = Vector2.Dot(current - incomingPosition, sideways);
            if (Mathf.Abs(side) < .05f) side = outgoingIndex % 2 == 0 ? 1f : -1f;
            Vector2 offset = Vector2.right * .95f + sideways * (Mathf.Sign(side) * .7f);
            Vector2 target = current + Vector2.ClampMagnitude(offset, 1.4f);
            target.x = Mathf.Max(target.x, playerPosition.x + ContactDistance);
            target.y = Mathf.Clamp(target.y, ActorGroundY - MaximumGroundDepth, ActorGroundY + MaximumGroundDepth);
            outgoing.RetreatTarget = target;
            outgoing.Retiring = true;
        }

        private void ResetEnemyRoster()
        {
            if (enemyFigures.Count == 0) return;
            ResetEnemyEnsembleIntent();
            enemyHandoffTravel = false;
            enemySideRecovery = false;
            SelectEnemyContext(0);
            for (int i = 0; i < enemyFigures.Count; i++)
            {
                EnemyFigure figure = enemyFigures[i];
                figure.Retiring = false;
                if (enemyFigures.Count > 1)
                {
                    // An opening arrangement only. Later exchanges derive travel from the positions actually reached.
                    Vector3 position = new Vector3(5f + i * 1.55f, ActorGroundY + (i == 1 ? .82f : i == 2 ? -.72f : 0f), 0f);
                    ResetActor(figure.Actor, position);
                    figure.Actor.Renderer.flipX = figure.Actor.Appearance == EnemyAppearance.Elisa;
                }
                else
                {
                    figure.Actor.Defeated = false;
                    figure.Actor.DefeatElapsed = 0f;
                    figure.Actor.Renderer.gameObject.SetActive(true);
                }
                figure.BreakAura.Hidden = false;
                figure.BreakAura.Reset();
                figure.PowerAura.Reset();
                figure.Afterimages.Clear();
                SampleActor(figure.Actor);
            }
            RefreshDepthPresentation();
        }

        public void SetEnemyResistanceBroken(int index, bool broken)
        {
            if (disposed) return;
            EnemyFigure figure = FigureAt(index);
            figure.BreakAura.SetBroken(broken && !figure.Actor.Defeated);
        }

        public void SetEnemyDefeated(int index, bool defeated)
        {
            if (disposed) return;
            EnemyFigure figure = FigureAt(index);
            Actor actor = figure.Actor;
            if (actor.Defeated == defeated) return;
            actor.Defeated = defeated;
            actor.DefeatElapsed = 0f;
            actor.Renderer.gameObject.SetActive(true);
            if (defeated)
            {
                if (upcomingEnemyIndex == index) ClearUpcomingEnemy();
                actor.Skill = null;
                actor.Chasing = false;
                actor.GroundVelocity = Vector2.zero;
                figure.Retiring = false;
                figure.BreakAura.SetBroken(false);
                figure.PowerAura.Reset();
                figure.Afterimages.Clear();
                ClearAttackMotion(actor, true);
            }
            else actor.Renderer.color = Color.white;
            SampleActor(actor);
            RefreshDepthPresentation();
        }

        private void TickInactiveEnemies(float delta, float realDelta, bool planning)
        {
            for (int i = 0; i < enemyFigures.Count; i++)
            {
                if (i == activeEnemyIndex) continue;
                EnemyFigure figure = enemyFigures[i];
                Actor actor = figure.Actor;
                Vector3 start = actor.Renderer.transform.localPosition;
                TickActor(actor, delta);
                float movementDelta = delta;
                if (planning)
                {
                    movementDelta = TickPlanningEnemy(figure, i, delta, realDelta);
                }
                else if (i == upcomingEnemyIndex)
                {
                    TickUpcomingEnemy(figure, delta);
                }
                else if (!actor.Defeated && !actor.Pushing &&
                    actor.Renderer.transform.localPosition.x < player.Renderer.transform.localPosition.x + ContactDistance + .25f)
                {
                    // A finite safety step, only when the player reaches a waiting body's space. It then holds again.
                    figure.RetreatTarget = new Vector2(Mathf.Max(figure.Retiring ? figure.RetreatTarget.x : float.NegativeInfinity,
                        player.Renderer.transform.localPosition.x + ContactDistance + 1.3f),
                        figure.Retiring ? figure.RetreatTarget.y : actor.Renderer.transform.localPosition.y);
                    figure.Retiring = true;
                }
                if (!planning && i != upcomingEnemyIndex && figure.Retiring && !actor.Defeated && !actor.Pushing)
                {
                    Vector2 error = figure.RetreatTarget - (Vector2)actor.Renderer.transform.localPosition;
                    if (error.sqrMagnitude > .0001f)
                        TravelOnGround(actor, error, ApproachSpeed * .55f, delta, settings.EnemyHandoffSpeedMultiplier);
                    else figure.Retiring = false;
                }
                BrakeUnrequestedGroundTravel(actor, movementDelta);
                SampleMovementPose(actor, start, movementDelta);
                figure.BreakAura.Tick(delta);
                figure.PowerAura.Tick(delta);
                TickDefeatedFigure(actor, delta);
            }
        }

        private void TickDefeatedFigure(Actor actor, float delta)
        {
            if (!actor.Defeated) return;
            actor.DefeatElapsed = Mathf.Min(DefeatFadeSeconds, actor.DefeatElapsed + delta);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, actor.DefeatElapsed / DefeatFadeSeconds);
            actor.Renderer.color = new Color(.65f, .60f, .55f, alpha);
            if (actor.GroundShadow != null)
            {
                Color shadow = actor.GroundShadow.color;
                shadow.a = .6117647f * alpha;
                actor.GroundShadow.color = shadow;
            }
            if (actor.DefeatElapsed >= DefeatFadeSeconds) actor.Renderer.gameObject.SetActive(false);
        }

        private float RosterFeetY
        {
            get
            {
                float y = PlayerGroundPosition.y;
                for (int i = 0; i < enemyFigures.Count; i++)
                    if (!enemyFigures[i].Actor.Defeated) y = Mathf.Min(y, GroundPosition(enemyFigures[i].Actor).y);
                return y;
            }
        }

        private void FrameEnemyRoster(ref Vector3 pivot, ref float size)
        {
            if (enemyFigures.Count <= 1 || IsFatalFocus || suspension != null || cutInAmount > .001f || StepFocusAmount > .001f) return;
            float minX = player.Renderer.transform.localPosition.x, maxX = minX;
            for (int i = 0; i < enemyFigures.Count; i++)
            {
                Actor actor = enemyFigures[i].Actor;
                if (actor.Defeated) continue;
                float x = actor.Renderer.transform.localPosition.x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }
            float aspect = Mathf.Max(.5f, ArenaCamera.aspect);
            float overview = 1f - CombatShotMotionWeight;
            size = Mathf.Lerp(size, Mathf.Max(size, (maxX - minX + 5f) / (2f * aspect)), overview);
            pivot.x = Mathf.Lerp(pivot.x, (minX + maxX) * .5f, overview);
        }

        private void SuspendEnemyRoster()
        {
            for (int i = 0; i < enemyFigures.Count; i++)
            {
                if (i == activeEnemyIndex) continue;
                EnemyFigure figure = enemyFigures[i];
                figure.VisibleBeforeCutscene = figure.Actor.Renderer.gameObject.activeSelf;
                figure.Actor.Renderer.gameObject.SetActive(false);
                figure.BreakAura.Hidden = true;
                figure.Afterimages.Root.gameObject.SetActive(false);
            }
        }

        private void ResumeEnemyRoster()
        {
            for (int i = 0; i < enemyFigures.Count; i++)
            {
                if (i == activeEnemyIndex) continue;
                EnemyFigure figure = enemyFigures[i];
                figure.Actor.Renderer.gameObject.SetActive(figure.VisibleBeforeCutscene);
                figure.BreakAura.Hidden = false;
                figure.Afterimages.Root.gameObject.SetActive(true);
            }
            RefreshDepthPresentation();
        }
    }
}
