using System.Collections;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class StepMotionPlayModeTests
    {
        [UnityTest]
        public IEnumerator DodgeAndPressure_MoveOnRealTime_AndDoNotReturnToTheirStart()
        {
            var host = new GameObject("Step Movement Test Host");
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt());
            float originalTimeScale = Time.timeScale;
            try
            {
                DuelPresentationSettings settings = Resources.Load<DuelPresentationSettings>("DuelPresentationSettings");
                float dodgeDistance = settings.StepDodgeDistance * settings.MovementDistanceMultiplier;
                float pressureDistance = settings.StepPressureDistance * settings.MovementDistanceMultiplier;
                float stepDuration = LegacyArenaView.StepDuration / settings.MovementSpeedMultiplier;
                arena.CloseDistance(1f);
                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                Vector3 original = arena.PlayerRenderer.transform.localPosition;
                Time.timeScale = 0f;
                arena.PerformStep(LegacyStepAction.Dodge);
                Assert.That(arena.IsStepping, Is.True);
                arena.Tick(0f, 0f);
                Assert.That(arena.PlayerRenderer.transform.localPosition, Is.EqualTo(original));
                for (int index = 0; index < 8; index++) arena.Tick(0f, stepDuration / 8f);
                Assert.That(arena.IsStepping, Is.False);
                Assert.That(arena.PlayerRenderer.transform.localPosition.x,
                    Is.EqualTo(original.x - dodgeDistance).Within(.001f));
                Assert.That(arena.ActiveStepAfterimageCount, Is.EqualTo(3));

                Vector3 afterDodge = arena.PlayerRenderer.transform.localPosition;
                arena.PerformStep(LegacyStepAction.Pressure);
                for (int index = 0; index < 8; index++) arena.Tick(0f, stepDuration / 8f);
                Vector3 afterPressure = arena.PlayerRenderer.transform.localPosition;
                Assert.That(afterPressure.x, Is.EqualTo(afterDodge.x + pressureDistance).Within(.001f));
                Assert.That(afterPressure.y, Is.EqualTo(original.y));
                arena.EndTurn();
                arena.Tick(LegacyArenaView.ReturnDuration + .01f, .2f);
                arena.BeginTurn();
                arena.Tick(.1f, .1f);
                Assert.That(arena.PlayerRenderer.transform.localPosition, Is.EqualTo(afterPressure),
                    "Ending a turn must retain actual step displacement, not teleport the actor back.");
                Assert.That(arena.ActiveStepAfterimageCount, Is.Zero);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                arena.Dispose();
                Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedPressure_StopsBeforeCrossing_AndRestartClearsMotionAndTrails()
        {
            var host = new GameObject("Step Boundaries Test Host");
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt());
            try
            {
                float stepDuration = LegacyArenaView.StepDuration /
                    Resources.Load<DuelPresentationSettings>("DuelPresentationSettings").MovementSpeedMultiplier;
                arena.CloseDistance(1f);
                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                for (int index = 0; index < 40; index++)
                {
                    arena.PerformStep(LegacyStepAction.Pressure);
                    arena.Tick(0f, stepDuration);
                    Assert.That(arena.Separation, Is.GreaterThanOrEqualTo(LegacyArenaView.MinimumStepSeparation - .001f));
                    Assert.That(arena.ActiveStepAfterimageCount, Is.LessThanOrEqualTo(DuelStepAfterimages.Capacity));
                }
                Assert.That(arena.Separation, Is.EqualTo(LegacyArenaView.MinimumStepSeparation).Within(.001f));
                arena.PerformStep(LegacyStepAction.Dodge);
                arena.Tick(0f, .03f);
                Assert.That(arena.IsStepping, Is.True);
                Assert.That(arena.ActiveStepAfterimageCount, Is.GreaterThan(0));
                arena.Reset();
                Assert.That(arena.IsStepping, Is.False);
                Assert.That(arena.ActiveStepAfterimageCount, Is.Zero);
                Assert.That(arena.PlayerRenderer.transform.localPosition, Is.EqualTo(new Vector3(-5f, -.5f, 0f)));
                arena.Tick(0f, .1f);
                Assert.That(arena.PlayerRenderer.transform.localPosition, Is.EqualTo(new Vector3(-5f, -.5f, 0f)));
                arena.PerformStep(LegacyStepAction.Dodge);
                Assert.That(arena.IsStepping, Is.False, "The arena must ignore gestures outside resolving presentation.");
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StepDuringKnockback_TranslatesPendingPush_WithoutSnappingBack()
        {
            var host = new GameObject("Step Push Test Host");
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt());
            try
            {
                DuelPresentationSettings settings = Resources.Load<DuelPresentationSettings>("DuelPresentationSettings");
                float dodgeDistance = settings.StepDodgeDistance * settings.MovementDistanceMultiplier;
                float halfStepDuration = LegacyArenaView.StepDuration / settings.MovementSpeedMultiplier * .5f;
                arena.CloseDistance(1f);
                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                arena.PresentHit(false, 6, 0, false, false, 6);
                Vector3 originalEndpoint = arena.PlayerKnockbackTarget;
                arena.Tick(.025f, .025f);
                Assert.That(arena.HasPendingPush, Is.True);
                arena.PerformStep(LegacyStepAction.Dodge);
                arena.Tick(0f, halfStepDuration);
                float moved = dodgeDistance * .75f;
                Assert.That(arena.PlayerKnockbackTarget.x,
                    Is.EqualTo(originalEndpoint.x - moved).Within(.001f));
                float steppedX = arena.PlayerRenderer.transform.localPosition.x;
                arena.Tick(.001f, 0f);
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.LessThanOrEqualTo(steppedX),
                    "Resuming push interpolation must not jump to its old, pre-step position.");
                arena.Tick(LegacyArenaView.PushDuration / settings.MovementSpeedMultiplier + .01f, halfStepDuration);
                Assert.That(arena.HasPendingPush, Is.False);
                Assert.That(arena.IsStepping, Is.False);
                Assert.That(arena.PlayerRenderer.transform.localPosition.x,
                    Is.EqualTo(originalEndpoint.x - dodgeDistance).Within(.001f));
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AfterimageSnapshots_PreserveSpriteMaterialAndDistinctPathPoints()
        {
            var host = new GameObject("Step Trail Sampling Test Host");
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt());
            try
            {
                float stepDuration = LegacyArenaView.StepDuration /
                    Resources.Load<DuelPresentationSettings>("DuelPresentationSettings").MovementSpeedMultiplier;
                arena.CloseDistance(1f);
                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                SpriteRenderer source = arena.PlayerRenderer;
                Vector3 start = source.transform.position;
                arena.PerformStep(LegacyStepAction.Dodge);
                // One large frame still creates samples along the path, not three copies at its end.
                arena.Tick(0f, stepDuration);
                Transform trailRoot = host.transform.Find("Legacy Duel Arena/Duel Step Afterimages");
                Assert.That(trailRoot, Is.Not.Null);
                Assert.That(trailRoot.childCount, Is.EqualTo(DuelStepAfterimages.Capacity));
                SpriteRenderer first = trailRoot.GetChild(0).GetComponent<SpriteRenderer>();
                SpriteRenderer second = trailRoot.GetChild(1).GetComponent<SpriteRenderer>();
                SpriteRenderer third = trailRoot.GetChild(2).GetComponent<SpriteRenderer>();
                Assert.That(first.gameObject.activeSelf, Is.True);
                Assert.That(first.transform.position, Is.EqualTo(start));
                Assert.That(first.transform.position.x, Is.GreaterThan(second.transform.position.x));
                Assert.That(second.transform.position.x, Is.GreaterThan(third.transform.position.x));
                foreach (SpriteRenderer view in new[] { first, second, third })
                {
                    Assert.That(view.sprite, Is.SameAs(source.sprite));
                    Assert.That(view.sharedMaterial, Is.SameAs(source.sharedMaterial));
                    Assert.That(view.gameObject.layer, Is.EqualTo(source.gameObject.layer));
                    Assert.That(view.sortingOrder, Is.LessThan(source.sortingOrder));
                    Assert.That(view.color.a, Is.GreaterThan(0f).And.LessThanOrEqualTo(DuelStepAfterimages.InitialAlpha));
                }
                Vector3 snapshot = second.transform.position;
                source.transform.localPosition += Vector3.right * 2f;
                arena.Tick(0f, 0f);
                Assert.That(second.transform.position, Is.EqualTo(snapshot));
                arena.Tick(0f, DuelStepAfterimages.Lifetime);
                Assert.That(arena.ActiveStepAfterimageCount, Is.Zero,
                    "Trails must fade on real time even while combat animation is stopped.");
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AfterimageSpam_ReusesPrewarmedTwelveSprites_AndDisposeIsIdempotent()
        {
            var host = new GameObject("Step Trail Pool Test Host");
            var sourceObject = new GameObject("Step Trail Source");
            var source = sourceObject.AddComponent<SpriteRenderer>();
            source.sprite = new LegacyDuelArt().GetPlayerSprite(0f);
            source.transform.localScale = new Vector3(1.25f, .8f, 1f);
            source.flipX = true;
            source.sortingOrder = 5;
            var trails = new DuelStepAfterimages(host.transform, source.sharedMaterial, 30);
            try
            {
                SpriteRenderer[] prewarmed = host.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(prewarmed, Has.Length.EqualTo(DuelStepAfterimages.Capacity));
                for (int index = 0; index < 80; index++)
                {
                    source.transform.position = Vector3.right * index;
                    trails.Emit(source, index % 2 == 0 ? LegacyStepAction.Dodge : LegacyStepAction.Pressure);
                }
                Assert.That(trails.ActiveCount, Is.EqualTo(DuelStepAfterimages.Capacity));
                SpriteRenderer[] afterSpam = host.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(afterSpam, Is.EqualTo(prewarmed), "Emission must reuse the same prewarmed renderers.");
                foreach (SpriteRenderer view in afterSpam)
                {
                    Assert.That(view.flipX, Is.True);
                    Assert.That(view.transform.localScale, Is.EqualTo(source.transform.localScale));
                    Assert.That(view.sortingOrder, Is.EqualTo(source.sortingOrder - 1));
                }
                trails.Tick(DuelStepAfterimages.Lifetime * .5f);
                Assert.That(afterSpam[0].color.a, Is.EqualTo(DuelStepAfterimages.InitialAlpha * .5f).Within(.001f));
                trails.Tick(DuelStepAfterimages.Lifetime * .5f);
                Assert.That(trails.ActiveCount, Is.Zero);
                trails.Emit(source, LegacyStepAction.Pressure);
                Assert.That(trails.ActiveCount, Is.EqualTo(1));
                trails.Reset();
                Assert.That(trails.ActiveCount, Is.Zero);
                trails.Dispose();
                trails.Dispose();
                trails.Emit(source, LegacyStepAction.Dodge);
                Assert.That(trails.ActiveCount, Is.Zero);
            }
            finally
            {
                trails.Dispose();
                Object.Destroy(sourceObject);
                Object.Destroy(host);
            }
            yield return null;
        }
    }
}
