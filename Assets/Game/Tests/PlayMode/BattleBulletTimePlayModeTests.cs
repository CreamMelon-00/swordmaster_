using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>결투 and 전투 through the real controller: only 전투 planning is bullet time, and only on screen.</summary>
    public sealed class BattleBulletTimePlayModeTests : InputTestFixture
    {
        private const string Tuning = "{\"hitStopDuration\":0,\"battleDriftSpeed\":1,\"battleDriftMinimumSeparation\":5," +
            "\"battleStagingSeparation\":7," +
            "\"battlePoseSpeed\":0,\"battleDesaturation\":30,\"battleCoolTint\":0.5}";

        [Test]
        public void StagePlanning_EdgesTheFiguresTogetherInAHeldPoseUnderAColdGrade_WithTheClockUntouched()
        {
            using (var scope = new BulletScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                Assert.That(controller.Encounter, Is.EqualTo(EncounterKind.Battle), "Stages are 전투.");
                Assert.That(arena.Separation, Is.EqualTo(10f).Within(1e-4f));
                Assert.That(arena.BulletTimeAmount, Is.Zero);

                scope.Advance(1f);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(9f).Within(1e-4f), "The 10-second planning rule is unchanged.");
                Assert.That(arena.Separation, Is.EqualTo(8f).Within(1e-3f), "Each figure edges in at the drift speed.");
                Assert.That(arena.DuelCenter.x, Is.EqualTo(0f).Within(1e-4f), "…symmetrically, so the planning camera stays put.");
                Assert.That(arena.BulletTimeAmount, Is.EqualTo(1f));
                ColorAdjustments grade = Grade(arena);
                Assert.That(grade.saturation.value, Is.EqualTo(-30f).Within(1e-3f));
                Assert.That(grade.colorFilter.value.b, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(grade.colorFilter.value.r, Is.LessThan(1f), "A cold filter.");
                Sprite held = arena.PlayerRenderer.sprite;
                Assert.That(held.name, Does.StartWith("idle-frame-"), "The held pose is an idle frame.");

                scope.Advance(2f);
                Assert.That(arena.Separation, Is.EqualTo(5f).Within(1e-3f), "They stop at the drift floor…");
                scope.Advance(1f);
                Assert.That(arena.Separation, Is.EqualTo(5f).Within(1e-3f), "…and stay there.");
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(held), "The pose is held throughout.");
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(6f).Within(1e-4f));

                controller.CommitTurn();
                scope.Advance(.01f);
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(arena.BulletTimeAmount, Is.Zero, "The commit releases bullet time at once.");
                Assert.That(Grade(arena).saturation.value, Is.Zero);
                Assert.That(Grade(arena).colorFilter.value, Is.EqualTo(Color.white));
                for (int frame = 0; frame < 200 && arena.Separation > 4.01f; frame++) scope.Advance(.01f);
                Assert.That(arena.Separation, Is.EqualTo(4f).Within(.02f), "The normal approach closes the rest.");
            }
        }

        [Test]
        public void PlanningFromCloseQuarters_StepsBackToTheStagingGapThenEdgesInAgain()
        {
            using (var scope = new BulletScope())
            {
                // Where every turn ends: at contact.
                LegacyArenaView arena = scope.Controller.ArenaView;
                arena.PlayerRenderer.transform.localPosition = new Vector3(-1f, -.5f, 0f);
                arena.EnemyRenderer.transform.localPosition = new Vector3(3f, -.5f, 0f);
                scope.Advance(.1f);
                Assert.That(arena.Separation, Is.EqualTo(5.2f).Within(1e-3f), "They step back at normal speed first…");
                Assert.That(arena.DuelCenter.x, Is.EqualTo(1f).Within(1e-4f), "…around where they stood.");
                scope.Advance(.5f);
                Assert.That(arena.Separation, Is.EqualTo(7f).Within(1e-3f), "…up to the staging gap…");
                scope.Advance(1f);
                Assert.That(arena.Separation, Is.EqualTo(5f).Within(1e-3f), "…then edge in again under bullet time.");
                Assert.That(scope.Controller.TurnTimeRemaining, Is.EqualTo(8.4f).Within(1e-3f), "The clock is untouched.");
            }
        }

        [Test]
        public void AFigureCloserThanTheDriftFloor_NeverPartsWhenStagingIsOff()
        {
            using (var scope = new BulletScope("{\"battleStagingSeparation\":4}"))
            {
                LegacyArenaView arena = scope.Controller.ArenaView;
                // Closer than contact, as a late pressure step can leave them (2.8).
                arena.PlayerRenderer.transform.localPosition = new Vector3(-1.4f, -.5f, 0f);
                arena.EnemyRenderer.transform.localPosition = new Vector3(1.4f, -.5f, 0f);
                scope.Advance(1f);
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(-1.4f), "Staging 4 is off.");
                Assert.That(arena.EnemyRenderer.transform.localPosition.x, Is.EqualTo(1.4f));
                Assert.That(arena.BulletTimeAmount, Is.EqualTo(1f), "The look still plays.");
            }
        }

        [UnityTest]
        public IEnumerator TabInspection_DeepensTheDriftWithThePlanningClock()
        {
            yield return null;
            using (var scope = new BulletScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                scope.Advance(1f, keyboard);
                Release(keyboard.tabKey);
                Assert.That(scope.Controller.TurnTimeRemaining, Is.EqualTo(9.8f).Within(1e-3f), "Tab slows the clock as before.");
                Assert.That(scope.Controller.ArenaView.Separation, Is.EqualTo(9.6f).Within(1e-3f), "…and the drift with it.");
            }
        }

        [Test]
        public void ArcMissions_AreDuels_WithTheOriginalStillPlanning()
        {
            using (var scope = new BulletScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.StartNewGame();
                Assert.That(controller.StartMission(), Is.True);
                for (int line = 0; line < 200 && controller.IsShowingDialogue; line++) controller.ContinueDialogue();
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(1));
                Assert.That(controller.Encounter, Is.EqualTo(EncounterKind.Duel));
                // Leaving mission 1 with it marked won opens mission 2's briefing.
                Assert.That(controller.Prologue.TryRestore(1), Is.True);
                controller.ReturnToLobby();
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.StartMission(), Is.True);
                for (int line = 0; line < 200 && controller.IsShowingDialogue; line++) controller.ContinueDialogue();
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(2));
                Assert.That(controller.Encounter, Is.EqualTo(EncounterKind.Duel), "The 서막 is 결투.");
                LegacyArenaView arena = controller.ArenaView;
                for (int frame = 0; frame < 3; frame++) scope.Advance(1f);
                Assert.That(arena.Separation, Is.EqualTo(10f).Within(1e-4f), "Nobody moves while planning a 결투.");
                Assert.That(arena.BulletTimeAmount, Is.Zero);
                Assert.That(Grade(arena).saturation.value, Is.Zero);
            }
        }

        [Test]
        public void Settings_DefaultClampAndFallBack_WithoutTouchingOtherTuning()
        {
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                Assert.That(settings.BattleDriftSpeed, Is.EqualTo(.15f));
                Assert.That(settings.BattleStagingSeparation, Is.EqualTo(7f));
                Assert.That(settings.BattleDriftMinimumSeparation, Is.EqualTo(5f));
                Assert.That(settings.BattlePoseSpeed, Is.Zero);
                Assert.That(settings.BattleDesaturation, Is.EqualTo(30f));
                Assert.That(settings.BattleCoolTint, Is.EqualTo(.35f));
                float stepSlow = settings.StepSlowMotionScale, playback = settings.AnimationPlaybackSpeed;
                JsonUtility.FromJsonOverwrite("{\"battleDriftSpeed\":9,\"battleStagingSeparation\":20,\"battleDriftMinimumSeparation\":1,\"battlePoseSpeed\":-1," +
                    "\"battleDesaturation\":500,\"battleCoolTint\":2}", settings);
                Assert.That(settings.BattleDriftSpeed, Is.EqualTo(2f));
                Assert.That(settings.BattleStagingSeparation, Is.EqualTo(10f));
                Assert.That(settings.BattleDriftMinimumSeparation, Is.EqualTo(4f), "Never closer than the contact distance.");
                Assert.That(settings.BattlePoseSpeed, Is.Zero);
                Assert.That(settings.BattleDesaturation, Is.EqualTo(100f));
                Assert.That(settings.BattleCoolTint, Is.EqualTo(1f));
                Assert.That(settings.StepSlowMotionScale, Is.EqualTo(stepSlow));
                Assert.That(settings.AnimationPlaybackSpeed, Is.EqualTo(playback));
                foreach (string field in new[] { "battleDriftSpeed", "battleStagingSeparation", "battleDriftMinimumSeparation", "battlePoseSpeed",
                    "battleDesaturation", "battleCoolTint" })
                {
                    FieldInfo info = typeof(DuelPresentationSettings).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert.That(info, Is.Not.Null, field);
                    info.SetValue(settings, float.NaN);
                }
                Assert.That(settings.BattleDriftSpeed, Is.EqualTo(.15f));
                Assert.That(settings.BattleStagingSeparation, Is.EqualTo(7f));
                Assert.That(settings.BattleDriftMinimumSeparation, Is.EqualTo(5f));
                Assert.That(settings.BattleDesaturation, Is.EqualTo(30f));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        private static ColorAdjustments Grade(LegacyArenaView arena)
        {
            Assert.That(arena.ArenaProfile.TryGet(out ColorAdjustments adjustments), Is.True);
            return adjustments;
        }

        private sealed class BulletScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly FieldInfo arenaSettings;
            private readonly bool originallyEnabled;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public BulletScope(string extraTuning = null)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                Assert.That(Controller.AutoSaveEnabled, Is.False, "Tests never write the player's save.");
                originallyEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                settings = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite(Tuning, settings);
                if (extraTuning != null) JsonUtility.FromJsonOverwrite(extraTuning, settings);
                typeof(DuelPrototypeController).GetField("presentationSettings", PrivateInstance).SetValue(Controller, settings);
                arenaSettings.SetValue(Controller.ArenaView, settings);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                Controller.RestartMatch();
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            public void Dispose()
            {
                typeof(DuelPrototypeController).GetField("presentationSettings", PrivateInstance).SetValue(Controller, originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Controller.StartNewGame();
                Controller.RestartJourney();
                Controller.enabled = originallyEnabled;
                Object.Destroy(settings);
            }
        }
    }
}
