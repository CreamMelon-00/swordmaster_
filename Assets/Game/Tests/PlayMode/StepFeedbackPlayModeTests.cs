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
    public sealed class StepFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator SuccessfulStep_SlowsCustomClock_DarkensOnlyBackdrop_AndFocusesCamera()
        {
            using (var fixture = new ArenaFixture())
            {
                LegacyArenaView arena = fixture.Arena;
                arena.Tick(0f, 1f);
                float baselineZoom = arena.ArenaCamera.orthographicSize;
                Color sky = arena.ArenaCamera.backgroundColor;
                float originalTimeScale = Time.timeScale;
                arena.PerformStep(LegacyStepAction.Dodge, true);
                Assert.That(arena.StepFocusAmount, Is.EqualTo(1f));
                Assert.That(arena.StepPresentationSpeed, Is.EqualTo(fixture.Settings.StepSlowMotionScale));
                Assert.That(arena.ForestBackdrop.Layers[0].Tiles[0].color.r, Is.EqualTo(
                    1f - fixture.Settings.StepBackdropDarkening).Within(.001f));
                Assert.That(arena.ArenaCamera.backgroundColor.r, Is.LessThan(sky.r));
                Assert.That(arena.PlayerRenderer.color, Is.EqualTo(Color.white));
                Assert.That(arena.EnemyRenderer.color, Is.EqualTo(Color.white));

                arena.Tick(0f, .06f);
                Assert.That(arena.ArenaCamera.orthographicSize, Is.LessThan(baselineZoom - .15f));
                Assert.That(arena.ArenaCamera.transform.localPosition.x, Is.LessThan(arena.DuelCenter.x - .3f));
                Assert.That(Quaternion.Angle(arena.ArenaCamera.transform.localRotation, Quaternion.identity),
                    Is.GreaterThan(.1f), "A step carries its own small directional tilt even with random combat tilt disabled.");
                Assert.That(arena.ActiveStepAfterimageCount, Is.EqualTo(3));
                Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissedStep_MovesAndGivesLighterFocus_WithoutSuccessSlowMotion()
        {
            using (var fixture = new ArenaFixture())
            {
                LegacyArenaView arena = fixture.Arena;
                Vector3 start = arena.PlayerRenderer.transform.localPosition;
                float pressureDistance = Mathf.Min(fixture.Settings.StepPressureDistance * fixture.Settings.MovementDistanceMultiplier,
                    arena.Separation - LegacyArenaView.MinimumStepSeparation);
                float stepDuration = LegacyArenaView.StepDuration / fixture.Settings.MovementSpeedMultiplier;
                arena.PerformStep(LegacyStepAction.Pressure, false);
                Assert.That(arena.StepPresentationSpeed, Is.EqualTo(1f));
                Assert.That(arena.StepFocusAmount, Is.EqualTo(.25f));
                Assert.That(arena.ForestBackdrop.Layers[0].Tiles[0].color.r, Is.GreaterThan(.8f));
                arena.Tick(0f, stepDuration);
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(
                    start.x + pressureDistance).Within(.001f));
                Assert.That(arena.ActiveStepAfterimageCount, Is.GreaterThan(0));
                arena.Tick(0f, Mathf.Max(0f, Mathf.Min(.12f, fixture.Settings.StepFocusDuration) - stepDuration) + .01f);
                Assert.That(arena.StepFocusAmount, Is.Zero);
                AssertBackdropWhite(arena);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedInput_DoesNotReplaceOrRenewSuccessPulse_AndKeepsPoolBounded()
        {
            using (var fixture = new ArenaFixture())
            {
                LegacyArenaView arena = fixture.Arena;
                arena.PerformStep(LegacyStepAction.Dodge, true);
                arena.Tick(0f, .1f);
                float focus = arena.StepFocusAmount;
                float speed = arena.StepPresentationSpeed;
                for (int index = 0; index < 20; index++)
                {
                    arena.PerformStep(LegacyStepAction.Pressure, false);
                    arena.PerformStep(LegacyStepAction.Dodge, true);
                }
                Assert.That(arena.StepFocusAmount, Is.EqualTo(focus));
                Assert.That(arena.StepPresentationSpeed, Is.EqualTo(speed));
                Assert.That(arena.ActiveStepAfterimageCount, Is.LessThanOrEqualTo(DuelStepAfterimages.Capacity));
                arena.Tick(0f, .3f);
                Assert.That(arena.StepFocusAmount, Is.Zero);
                Assert.That(arena.StepPresentationSpeed, Is.EqualTo(1f));
                // Even an incorrectly repeated success must not reopen the same
                // skill's full cinematic after its original pulse has finished.
                arena.PerformStep(LegacyStepAction.Dodge, true);
                Assert.That(arena.StepFocusAmount, Is.Zero);
                Assert.That(arena.StepPresentationSpeed, Is.EqualTo(1f));
                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                arena.PerformStep(LegacyStepAction.Pressure, true);
                Assert.That(arena.StepFocusAmount, Is.EqualTo(1f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealTimeExpiry_AndPhaseChanges_RestoreBackdropAndCancelSlowMotionImmediately()
        {
            using (var fixture = new ArenaFixture())
            {
                LegacyArenaView arena = fixture.Arena;
                Color sky = arena.ArenaCamera.backgroundColor;
                arena.PerformStep(LegacyStepAction.Pressure, true);
                arena.Tick(0f, 1f);
                Assert.That(arena.StepFocusAmount, Is.Zero);
                Assert.That(arena.StepPresentationSpeed, Is.EqualTo(1f));
                Assert.That(arena.ArenaCamera.backgroundColor, Is.EqualTo(sky));
                AssertBackdropWhite(arena);

                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                arena.PerformStep(LegacyStepAction.Dodge, true);
                arena.Tick(0f, .05f);
                arena.EndTurn();
                Assert.That(arena.StepFocusAmount, Is.Zero);
                Assert.That(arena.StepPresentationSpeed, Is.EqualTo(1f));
                Assert.That(arena.ArenaCamera.backgroundColor, Is.EqualTo(sky));
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(3.5f));
                Assert.That(arena.IsStepping, Is.False);
                AssertBackdropWhite(arena);

                arena.BeginTurn();
                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                arena.PerformStep(LegacyStepAction.Pressure, true);
                arena.BeginTurn();
                Assert.That(arena.StepFocusAmount, Is.Zero);
                Assert.That(arena.StepPresentationSpeed, Is.EqualTo(1f));
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(6f));
                AssertBackdropWhite(arena);

                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
                arena.PerformStep(LegacyStepAction.Dodge, true);
                arena.Reset();
                Assert.That(arena.StepFocusAmount, Is.Zero);
                Assert.That(arena.StepPresentationSpeed, Is.EqualTo(1f));
                Assert.That(arena.ActiveStepAfterimageCount, Is.Zero);
                Assert.That(arena.ArenaCamera.backgroundColor, Is.EqualTo(sky));
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(6f));
                AssertBackdropWhite(arena);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FocusDarkening_IsMultiplicativeWithInspection_AndDoesNotAccumulate()
        {
            using (var fixture = new ArenaFixture())
            {
                ForestParallaxBackdrop backdrop = fixture.Arena.ForestBackdrop;
                Camera camera = fixture.Arena.ArenaCamera;
                backdrop.Tick(camera, true, 1f);
                Color inspected = backdrop.Layers[0].Tiles[0].color;
                Assert.That(inspected.r, Is.EqualTo(.6f).Within(.001f));
                for (int index = 0; index < 20; index++) backdrop.Tick(camera, true, 0f, .5f);
                Assert.That(backdrop.Layers[0].Tiles[0].color.r, Is.EqualTo(.3f).Within(.001f));
                backdrop.Tick(camera, true, 0f, 0f);
                Assert.That(backdrop.Layers[0].Tiles[0].color, Is.EqualTo(inspected));
                backdrop.Reset(camera);
                AssertBackdropWhite(fixture.Arena);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FatalCameraFocus_HasPrecedenceOverStepPushIn()
        {
            using (var fixture = new ArenaFixture())
            {
                LegacyArenaView arena = fixture.Arena;
                arena.PresentHit(true, 1, 0, false, true, 0);
                arena.Tick(.12f, .4f); // Let the old impact jolt end and settle on the fatal target.
                arena.PerformStep(LegacyStepAction.Pressure, true);
                arena.Tick(0f, .22f);
                Assert.That(arena.IsFatalFocus, Is.True);
                Assert.That(arena.ArenaCamera.orthographicSize, Is.GreaterThan(2.95f).And.LessThan(3.05f),
                    "Step zoom must not subtract another half unit from the fatal target's size of three.");
                Assert.That(arena.ArenaCamera.transform.localPosition.x, Is.EqualTo(
                    arena.EnemyRenderer.transform.localPosition.x).Within(.1f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InspectorDistancesAndFeedbackValues_AreUsedWithoutChangingGlobalTime()
        {
            using (var fixture = new ArenaFixture())
            {
                Set(fixture.Settings, "stepDodgeDistance", 2f);
                Set(fixture.Settings, "stepPressureDistance", .4f);
                Set(fixture.Settings, "stepSlowMotionScale", .4f);
                Set(fixture.Settings, "stepBackdropDarkening", .3f);
                Vector3 start = fixture.Arena.PlayerRenderer.transform.localPosition;
                fixture.Arena.PerformStep(LegacyStepAction.Dodge, true);
                Assert.That(fixture.Arena.StepPresentationSpeed, Is.EqualTo(.4f));
                Assert.That(fixture.Arena.ForestBackdrop.Layers[0].Tiles[0].color.r, Is.EqualTo(.7f).Within(.001f));
                fixture.Arena.Tick(0f, LegacyArenaView.StepDuration / fixture.Settings.MovementSpeedMultiplier);
                Assert.That(fixture.Arena.PlayerRenderer.transform.localPosition.x,
                    Is.EqualTo(start.x - 2f * fixture.Settings.MovementDistanceMultiplier).Within(.001f));
                fixture.Arena.PerformStep(LegacyStepAction.Pressure, false);
                fixture.Arena.Tick(0f, LegacyArenaView.StepDuration / fixture.Settings.MovementSpeedMultiplier);
                Assert.That(fixture.Arena.PlayerRenderer.transform.localPosition.x,
                    Is.EqualTo(start.x - 1.6f * fixture.Settings.MovementDistanceMultiplier).Within(.001f));
            }
            yield return null;
        }

        private static void AssertBackdropWhite(LegacyArenaView arena)
        {
            foreach (ForestParallaxBackdrop.Layer layer in arena.ForestBackdrop.Layers)
                foreach (SpriteRenderer tile in layer.Tiles)
                    if (tile.gameObject.activeSelf) Assert.That(tile.color, Is.EqualTo(Color.white));
        }

        private static void Set(DuelPresentationSettings settings, string field, float value) =>
            typeof(DuelPresentationSettings).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, value);

        private sealed class ArenaFixture : IDisposable
        {
            private readonly GameObject host;
            public DuelPresentationSettings Settings { get; }
            public LegacyArenaView Arena { get; }

            public ArenaFixture()
            {
                host = new GameObject("Step Feedback Test Host");
                Settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), Settings);
                Arena.CloseDistance(1f);
                Arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[0]);
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
