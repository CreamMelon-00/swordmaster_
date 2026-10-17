using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class MovementPunchPlayModeTests
    {
        [UnityTest]
        public IEnumerator UnitMultipliers_RestoreOriginalTravelSpeedsDistancesAndDurations()
        {
            using (var fixture = new ArenaFixture())
            {
                fixture.Configure(1f, 1f);
                fixture.PrepareCombat();
                float start = fixture.PlayerX;
                fixture.Arena.PerformStep(LegacyStepAction.Dodge);
                fixture.Arena.Tick(0f, LegacyArenaView.StepDuration * .5f);
                Assert.That(fixture.Arena.IsStepping, Is.True);
                Assert.That(fixture.PlayerX, Is.EqualTo(start - LegacyArenaView.DodgeDistance * .75f).Within(.001f));
                fixture.Arena.Tick(0f, LegacyArenaView.StepDuration * .5f);
                Assert.That(fixture.Arena.IsStepping, Is.False);
                Assert.That(fixture.PlayerX, Is.EqualTo(start - LegacyArenaView.DodgeDistance).Within(.001f));
                float afterDodge = fixture.PlayerX;
                fixture.Arena.PerformStep(LegacyStepAction.Pressure);
                fixture.Arena.Tick(0f, LegacyArenaView.StepDuration);
                Assert.That(fixture.PlayerX, Is.EqualTo(afterDodge + LegacyArenaView.PressureDistance).Within(.001f));

                fixture.Arena.Reset();
                fixture.Arena.BeginApproach();
                fixture.Arena.Tick(.01f, 0f);
                float approachTravel = fixture.PlayerX + 5f;
                Assert.That(approachTravel, Is.GreaterThan(0f).And.LessThan(LegacyArenaView.ApproachSpeed * .01f),
                    "Approach accelerates from rest instead of starting at its speed cap.");
                Assert.That(5f - fixture.EnemyX, Is.EqualTo(approachTravel).Within(.001f));
                fixture.Arena.Tick(.2f, 0f);
                Assert.That(fixture.Arena.ApproachComplete, Is.True);
                Assert.That(fixture.Arena.Separation, Is.EqualTo(LegacyArenaView.ContactDistance).Within(.001f));
                fixture.Arena.Reset();
                fixture.Arena.CloseDistance(.01f);
                float pursuitTravel = fixture.PlayerX + 5f;
                Assert.That(pursuitTravel, Is.GreaterThan(0f).And.LessThan(LegacyArenaView.PursuitSpeed * .01f));
                Assert.That(5f - fixture.EnemyX, Is.EqualTo(pursuitTravel).Within(.001f));

                fixture.PrepareCombat();
                float targetStart = fixture.EnemyX;
                fixture.Arena.PresentHit(true, 10, 0, false, false, 10);
                Assert.That(fixture.Arena.EnemyKnockbackTarget.x, Is.EqualTo(targetStart + 2.3f).Within(.001f));
                fixture.Arena.Tick(LegacyArenaView.PushDuration * .5f, 0f);
                Assert.That(fixture.Arena.HasPendingPush, Is.True);
                Assert.That(fixture.EnemyX, Is.EqualTo(targetStart + 2.3f * .75f).Within(.001f));
                fixture.Arena.Tick(LegacyArenaView.PushDuration * .5f, 0f);
                Assert.That(fixture.Arena.HasPendingPush, Is.False);
                Assert.That(fixture.EnemyX, Is.EqualTo(targetStart + 2.3f).Within(.001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefaultDodge_TravelsOnePointNineSix_OnItsFasterRealTimeClock()
        {
            using (var fixture = new ArenaFixture())
            {
                Assert.That(fixture.Settings.MovementDistanceMultiplier, Is.EqualTo(1.4f));
                Assert.That(fixture.Settings.MovementSpeedMultiplier, Is.EqualTo(1.2f));
                float start = fixture.PlayerX;
                float duration = LegacyArenaView.StepDuration / 1.2f;
                Assert.That(duration, Is.EqualTo(.0666667f).Within(.000001f));
                fixture.Arena.PerformStep(LegacyStepAction.Dodge);
                fixture.Arena.Tick(.2f, 0f);
                Assert.That(fixture.PlayerX, Is.EqualTo(start), "Combat time cannot advance an actual-time step.");
                Assert.That(fixture.Arena.IsStepping, Is.True);
                fixture.Arena.Tick(0f, duration - .001f);
                Assert.That(fixture.Arena.IsStepping, Is.True);
                fixture.Arena.Tick(0f, .0011f);
                Assert.That(fixture.Arena.IsStepping, Is.False);
                Assert.That(fixture.PlayerX, Is.EqualTo(start - 1.96f).Within(.001f));
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.EqualTo(3));
                float afterDodge = fixture.PlayerX;
                fixture.Arena.EndTurn();
                fixture.Arena.Tick(LegacyArenaView.ReturnDuration + .01f, .2f);
                fixture.Arena.BeginTurn();
                Assert.That(fixture.PlayerX, Is.EqualTo(afterDodge));
                // EndTurn stops the gesture, but existing ghosts retain their
                // short real-time fade instead of disappearing abruptly.
                fixture.Arena.Tick(0f, DuelStepAfterimages.Lifetime);
                Assert.That(fixture.PlayerX, Is.EqualTo(afterDodge));
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefaultKnockback_AddsFortyPercentTravel_AndFinishesAtItsFasterCombatDuration()
        {
            using (var original = new ArenaFixture())
            using (var stronger = new ArenaFixture())
            {
                original.Configure(1f, 1f);
                original.PrepareCombat();
                float originalStart = original.EnemyX;
                float strongerStart = stronger.EnemyX;
                original.Arena.PresentHit(true, 12, 0, false, false, 12);
                stronger.Arena.PresentHit(true, 12, 0, false, false, 12);
                float originalDistance = original.Arena.EnemyKnockbackTarget.x - originalStart;
                float strongerDistance = stronger.Arena.EnemyKnockbackTarget.x - strongerStart;
                Assert.That(originalDistance, Is.EqualTo(2.62f).Within(.001f));
                Assert.That(strongerDistance, Is.EqualTo(originalDistance * 1.4f).Within(.001f));
                stronger.Arena.Tick(0f, 1f);
                Assert.That(stronger.EnemyX, Is.EqualTo(strongerStart));
                Assert.That(stronger.Arena.HasPendingPush, Is.True, "Real time cannot complete combat-clock knockback.");
                float fasterDuration = LegacyArenaView.PushDuration / 1.2f;
                original.Arena.Tick(fasterDuration, 0f);
                stronger.Arena.Tick(fasterDuration, 0f);
                Assert.That(stronger.Arena.HasPendingPush, Is.False);
                Assert.That(stronger.EnemyX, Is.EqualTo(strongerStart + strongerDistance).Within(.001f));
                Assert.That(original.Arena.HasPendingPush, Is.True);
                Assert.That(original.EnemyX, Is.LessThan(originalStart + originalDistance));
                original.Arena.Tick(LegacyArenaView.PushDuration - fasterDuration + .00001f, 0f);
                Assert.That(original.Arena.HasPendingPush, Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StrongerMovement_PreservesGuardRecoilPendingCapAndBothActorSides()
        {
            using (var fixture = new ArenaFixture())
            {
                float normal = fixture.MeasureEnemyTargetDistance(12, 0, false, 12);
                float guarded = fixture.MeasureEnemyTargetDistance(12, 0, true, 12);
                float blocked = fixture.MeasureEnemyTargetDistance(0, 0, true, 12);
                float missed = fixture.MeasureEnemyTargetDistance(0, 0, false, 0);
                Assert.That(guarded, Is.EqualTo(normal * LegacyArenaView.GuardKnockbackMultiplier).Within(.001f));
                Assert.That(blocked, Is.GreaterThan(0f).And.LessThan(guarded));
                Assert.That(missed, Is.Zero);

                fixture.Configure(2.5f, 2f);
                fixture.PrepareCombat();
                float enemyStart = fixture.EnemyX;
                for (int hit = 0; hit < 12; hit++)
                {
                    fixture.Arena.PresentHit(true, 999, 0, false, false, 999);
                    Assert.That(fixture.Arena.EnemyKnockbackTarget.x - enemyStart,
                        Is.LessThanOrEqualTo(LegacyArenaView.MaximumPendingKnockback + .001f));
                }
                Assert.That(fixture.Arena.EnemyKnockbackTarget.x - enemyStart,
                    Is.EqualTo(LegacyArenaView.MaximumPendingKnockback).Within(.001f));
                fixture.Arena.PresentHit(false, 999, 0, false, false, 999);
                for (int frame = 0; frame < 60; frame++)
                {
                    fixture.Arena.Tick(.01f, .01f);
                    Assert.That(fixture.Arena.Separation, Is.GreaterThanOrEqualTo(LegacyArenaView.ContactDistance - .001f),
                        "Simultaneous faster pushes and pursuit must not cross or collapse the actors.");
                }
                Assert.That(fixture.Arena.HasPendingPush, Is.False);
                Assert.That(fixture.Arena.Separation, Is.EqualTo(LegacyArenaView.ContactDistance).Within(.001f));
                for (int input = 0; input < 10; input++)
                {
                    fixture.Arena.PerformStep(LegacyStepAction.Pressure);
                    fixture.Arena.Tick(0f, LegacyArenaView.StepDuration / 2f);
                    Assert.That(fixture.Arena.Separation, Is.GreaterThanOrEqualTo(LegacyArenaView.MinimumStepSeparation - .001f));
                }
                Assert.That(fixture.Arena.Separation, Is.EqualTo(LegacyArenaView.MinimumStepSeparation).Within(.001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LiveTuning_KeepsActiveGesturesStable_AndChangesTheirNextStart()
        {
            using (var fixture = new ArenaFixture())
            using (var unchanged = new ArenaFixture())
            {
                fixture.Configure(1f, 1f);
                fixture.PrepareCombat();
                float start = fixture.PlayerX;
                fixture.Arena.PerformStep(LegacyStepAction.Dodge);
                fixture.Arena.Tick(0f, .02f);
                fixture.Configure(2f, 2f);
                fixture.Arena.Tick(0f, .02f);
                Assert.That(fixture.Arena.IsStepping, Is.True);
                Assert.That(fixture.PlayerX, Is.EqualTo(start - 1.4f * .75f).Within(.001f));
                fixture.Arena.Tick(0f, .04f);
                Assert.That(fixture.PlayerX, Is.EqualTo(start - 1.4f).Within(.001f));
                float nextStart = fixture.PlayerX;
                fixture.Arena.PerformStep(LegacyStepAction.Dodge);
                fixture.Arena.Tick(0f, .04f);
                Assert.That(fixture.Arena.IsStepping, Is.False);
                Assert.That(fixture.PlayerX, Is.EqualTo(nextStart - 2.8f).Within(.001f));

                fixture.Configure(1f, 1f);
                fixture.PrepareCombat();
                float targetStart = fixture.EnemyX;
                fixture.Arena.PresentHit(true, 10, 0, false, false, 10);
                Vector3 firstTarget = fixture.Arena.EnemyKnockbackTarget;
                fixture.Arena.Tick(.025f, 0f);
                fixture.Configure(2f, 2f);
                fixture.Arena.Tick(.025f, 0f);
                Assert.That(fixture.Arena.EnemyKnockbackTarget.x, Is.EqualTo(firstTarget.x),
                    "The committed horizontal impulse stays fixed while ground motion can carry its Y.");
                Assert.That(fixture.Arena.HasPendingPush, Is.True);
                Assert.That(fixture.EnemyX, Is.EqualTo(targetStart + 2.3f * .75f).Within(.001f));
                fixture.Arena.Tick(.05f, 0f);
                Assert.That(fixture.Arena.HasPendingPush, Is.False);
                float nextTargetStart = fixture.EnemyX;
                fixture.Arena.PresentHit(true, 10, 0, false, false, 10);
                fixture.Arena.Tick(.05f, 0f);
                Assert.That(fixture.Arena.HasPendingPush, Is.False);
                Assert.That(fixture.EnemyX, Is.EqualTo(nextTargetStart + 4.6f).Within(.001f));

                fixture.Configure(1f, 1f);
                fixture.Arena.Reset();
                fixture.Arena.BeginApproach();
                unchanged.Configure(1f, 1f);
                unchanged.Arena.Reset();
                unchanged.Arena.BeginApproach();
                fixture.Arena.Tick(.01f, 0f);
                unchanged.Arena.Tick(.01f, 0f);
                fixture.Configure(2f, 2f);
                fixture.Arena.Tick(.01f, 0f);
                unchanged.Arena.Tick(.01f, 0f);
                Assert.That(fixture.PlayerX, Is.EqualTo(unchanged.PlayerX).Within(.001f),
                    "Changing the asset does not alter an approach that already began.");
                fixture.Arena.Reset();
                fixture.Arena.BeginApproach();
                fixture.Arena.Tick(.01f, 0f);
                unchanged.Arena.Reset();
                unchanged.Arena.BeginApproach();
                unchanged.Arena.Tick(.01f, 0f);
                Assert.That(fixture.PlayerX + 5f, Is.GreaterThan(unchanged.PlayerX + 5f),
                    "The next approach accelerates using the new movement multiplier.");

                fixture.Configure(1f, 1f);
                fixture.PrepareCombat();
                fixture.Arena.EnemyRenderer.transform.localPosition += Vector3.right * 10f;
                unchanged.PrepareCombat();
                unchanged.Arena.EnemyRenderer.transform.localPosition += Vector3.right * 10f;
                float pursuitStart = fixture.PlayerX;
                float unchangedPursuitStart = unchanged.PlayerX;
                fixture.Arena.PresentHit(true, 1, 0, false, false, 1);
                unchanged.Arena.PresentHit(true, 1, 0, false, false, 1);
                fixture.Configure(2f, 2f);
                fixture.Arena.Tick(.05f, 0f);
                unchanged.Arena.Tick(.05f, 0f);
                Assert.That(fixture.PlayerX - pursuitStart,
                    Is.EqualTo(unchanged.PlayerX - unchangedPursuitStart).Within(.001f),
                    "The active pursuit retains the multiplier captured by its slot.");
                fixture.Arena.BeginSlot(LegacySkillDefinitions.Skill(1), LegacySkillDefinitions.Skill(1));
                unchanged.Arena.BeginSlot(LegacySkillDefinitions.Skill(1), LegacySkillDefinitions.Skill(1));
                pursuitStart = fixture.PlayerX;
                unchangedPursuitStart = unchanged.PlayerX;
                fixture.Arena.PresentHit(true, 1, 0, false, false, 1);
                unchanged.Arena.PresentHit(true, 1, 0, false, false, 1);
                fixture.Arena.Tick(LegacyArenaView.PursuitDelay, 0f);
                unchanged.Arena.Tick(LegacyArenaView.PursuitDelay, 0f);
                Assert.That(fixture.PlayerX - pursuitStart,
                    Is.GreaterThan(unchanged.PlayerX - unchangedPursuitStart),
                    "A new slot uses the new pursuit multiplier while preserving its current momentum.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoringBoundsAndNonfiniteValues_KeepActualMovementSafeWithoutChangingTempo()
        {
            using (var fixture = new ArenaFixture())
            {
                fixture.Configure(-1f, 99f);
                Assert.That(fixture.Settings.MovementDistanceMultiplier, Is.EqualTo(.5f));
                Assert.That(fixture.Settings.MovementSpeedMultiplier, Is.EqualTo(2f));
                fixture.PrepareCombat();
                float start = fixture.PlayerX;
                fixture.Arena.PerformStep(LegacyStepAction.Dodge);
                fixture.Arena.Tick(0f, LegacyArenaView.StepDuration / 2f);
                Assert.That(fixture.Arena.IsStepping, Is.False);
                Assert.That(fixture.PlayerX, Is.EqualTo(start - .7f).Within(.001f));
                fixture.Configure(99f, -1f);
                Assert.That(fixture.Settings.MovementDistanceMultiplier, Is.EqualTo(2.5f));
                Assert.That(fixture.Settings.MovementSpeedMultiplier, Is.EqualTo(.5f));
                fixture.PrepareCombat();
                start = fixture.PlayerX;
                fixture.Arena.PerformStep(LegacyStepAction.Dodge);
                fixture.Arena.Tick(0f, LegacyArenaView.StepDuration);
                Assert.That(fixture.Arena.IsStepping, Is.True);
                Assert.That(fixture.PlayerX, Is.EqualTo(start - 3.5f * .75f).Within(.001f));
                fixture.Arena.Tick(0f, LegacyArenaView.StepDuration);
                Assert.That(fixture.Arena.IsStepping, Is.False);
                Assert.That(fixture.PlayerX, Is.EqualTo(start - 3.5f).Within(.001f));

                foreach (float[] nonfinite in new[]
                {
                    new[] { float.NaN, float.PositiveInfinity },
                    new[] { float.NegativeInfinity, float.NaN }
                })
                {
                    fixture.Configure(nonfinite[0], nonfinite[1]);
                    Assert.That(fixture.Settings.MovementDistanceMultiplier, Is.EqualTo(1.4f));
                    Assert.That(fixture.Settings.MovementSpeedMultiplier, Is.EqualTo(1.2f));
                    fixture.PrepareCombat();
                    start = fixture.PlayerX;
                    fixture.Arena.PerformStep(LegacyStepAction.Dodge);
                    fixture.Arena.Tick(0f, LegacyArenaView.StepDuration / 1.2f);
                    Assert.That(fixture.Arena.IsStepping, Is.False);
                    Assert.That(fixture.PlayerX, Is.EqualTo(start - 1.96f).Within(.001f));
                }
                Assert.That(fixture.Settings.AnimationPlaybackSpeed, Is.EqualTo(1f));
                Assert.That(fixture.Settings.AttackInterval, Is.EqualTo(.025f));
                Assert.That(fixture.Settings.SkillInterval, Is.EqualTo(.06f));
            }
            yield return null;
        }

        private sealed class ArenaFixture : IDisposable
        {
            private readonly GameObject host;
            public DuelPresentationSettings Settings { get; }
            public LegacyArenaView Arena { get; }
            public float PlayerX => Arena.PlayerRenderer.transform.localPosition.x;
            public float EnemyX => Arena.EnemyRenderer.transform.localPosition.x;

            public ArenaFixture()
            {
                host = new GameObject("Movement Punch Test Host");
                Settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), Settings);
                Arena.ArenaCamera.enabled = false;
                PrepareCombat();
            }

            public void Configure(float distance, float speed)
            {
                Set("movementDistanceMultiplier", distance);
                Set("movementSpeedMultiplier", speed);
            }

            private void Set(string name, float value)
            {
                FieldInfo field = typeof(DuelPresentationSettings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "Missing movement authoring field " + name);
                field.SetValue(Settings, value);
            }

            public void PrepareCombat()
            {
                Arena.Reset();
                Arena.CloseDistance(1f);
                Arena.BeginSlot(LegacySkillDefinitions.Skill(1), LegacySkillDefinitions.Skill(1));
            }

            public float MeasureEnemyTargetDistance(int hp, int resistance, bool guarded, int power)
            {
                PrepareCombat();
                float start = EnemyX;
                Arena.PresentHit(true, hp, resistance, guarded, false, power);
                return Arena.EnemyKnockbackTarget.x - start;
            }

            public void Dispose()
            {
                Arena.Dispose();
                Object.Destroy(host);
                Object.Destroy(Settings);
            }
        }
    }
}
