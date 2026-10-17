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
            "\"battleStagingSeparation\":7,\"battleCycleReleaseSeconds\":0.3," +
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
                Vector3 player = arena.PlayerRenderer.transform.localPosition, enemy = arena.EnemyRenderer.transform.localPosition;

                scope.AdvanceFor(1f);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(9f).Within(1e-4f), "The 10-second planning rule is unchanged.");
                AssertApproaching(arena, player, enemy);
                Assert.That(arena.Separation, Is.GreaterThanOrEqualTo(8f).And.LessThan(9f),
                    "The figures accelerate into the configured slow approach, without exceeding its speed cap.");
                Assert.That(arena.DuelCenter.x, Is.EqualTo(0f).Within(1e-4f), "The approach is symmetric around the camera's subject.");
                Assert.That(arena.BulletTimeAmount, Is.EqualTo(1f));
                ColorAdjustments grade = Grade(arena);
                Assert.That(grade.saturation.value, Is.EqualTo(-30f).Within(1e-3f));
                Assert.That(grade.colorFilter.value.b, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(grade.colorFilter.value.r, Is.LessThan(1f), "A cold filter.");
                Sprite run = arena.PlayerRenderer.sprite;
                Assert.That(run.name, Does.StartWith("move-frame-"), "Slow planning travel holds a movement pose.");

                scope.AdvanceFor(1f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(run), "The planning movement cel stays held while approaching.");
                scope.AdvanceFor(1f);
                Assert.That(arena.Separation, Is.EqualTo(5f).Within(1e-3f), "They brake at the drift floor.");
                scope.AdvanceFor(.5f);
                Assert.That(arena.Separation, Is.EqualTo(5f).Within(1e-3f), "The slow approach never crosses its floor.");
                Sprite held = arena.PlayerRenderer.sprite;
                Assert.That(held.name, Does.StartWith("idle-frame-"), "At the floor, the held pose becomes idle.");
                scope.AdvanceFor(.5f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(held), "The idle motion stays stopped while planning.");
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
        public void PlanningFromCloseQuarters_AcceleratesBackToTheStagingGapThenEdgesInAgain()
        {
            using (var scope = new BulletScope())
            {
                // Where every turn ends: at contact.
                LegacyArenaView arena = scope.Controller.ArenaView;
                arena.PlayerRenderer.transform.localPosition = new Vector3(-1f, -.5f, 0f);
                arena.EnemyRenderer.transform.localPosition = new Vector3(3f, -.5f, 0f);
                scope.Advance(.1f);
                float elapsed = .1f;
                Assert.That(arena.Separation, Is.GreaterThan(4f).And.LessThan(5.2f),
                    "They accelerate into the step back rather than beginning at full speed.");
                Assert.That(arena.DuelCenter.x, Is.EqualTo(1f).Within(1e-4f), "…around where they stood.");
                bool reachedStaging = false;
                for (int frame = 0; frame < 120 && !reachedStaging; frame++)
                {
                    float before = arena.Separation;
                    scope.Advance(.02f);
                    elapsed += .02f;
                    Assert.That(arena.Separation, Is.GreaterThanOrEqualTo(before - .0001f), "The staging step completes before the slow approach resumes.");
                    Assert.That(arena.DuelCenter.x, Is.EqualTo(1f).Within(1e-4f));
                    Assert.That(arena.Separation, Is.LessThanOrEqualTo(7.001f), "The outward movement brakes at the staging gap.");
                    reachedStaging = arena.Separation >= 6.999f;
                }
                Assert.That(reachedStaging, Is.True, "They reach the staging gap before turning inward.");
                Assert.That(arena.Separation, Is.EqualTo(7f).Within(.02f));
                Vector3 player = arena.PlayerRenderer.transform.localPosition, enemy = arena.EnemyRenderer.transform.localPosition;
                scope.AdvanceFor(.5f);
                elapsed += .5f;
                AssertApproaching(arena, player, enemy);
                Assert.That(arena.Separation, Is.GreaterThan(5f).And.LessThan(7f), "They resume the slower approach after stepping back.");
                scope.AdvanceFor(1.5f);
                elapsed += 1.5f;
                Assert.That(arena.Separation, Is.EqualTo(5f).Within(.001f), "The return approach stops at the drift floor.");
                Assert.That(scope.Controller.TurnTimeRemaining, Is.EqualTo(10f - elapsed).Within(1e-3f), "The clock is untouched.");
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

        [Test]
        public void CycleSpendingTime_LetsBulletTimeGoForAMoment_AndTheFiguresCatchUp()
        {
            using (var scope = new BulletScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                scope.AdvanceFor(1f);
                Assert.That(arena.Separation, Is.LessThan(10f).And.GreaterThan(5f), "Ordinary planning already approaches slowly.");
                Assert.That(arena.BulletTimeAmount, Is.EqualTo(1f));
                float beforeNormal = arena.Separation;
                scope.AdvanceFor(.1f);
                float ordinaryTravel = beforeNormal - arena.Separation;
                Assert.That(ordinaryTravel, Is.GreaterThan(0f));
                float beforeRelease = arena.Separation;
                float clockBeforeRelease = controller.TurnTimeRemaining;
                Assert.That(controller.CycleLanes(), Is.True);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(clockBeforeRelease - 1f).Within(1e-3f), "The second is spent…");
                Assert.That(arena.IsBulletTimeReleased, Is.True, "…and bullet time lets go.");

                scope.AdvanceFor(.1f);
                Assert.That(arena.BulletTimeAmount, Is.Zero, "The look drops away at once.");
                Assert.That(beforeRelease - arena.Separation, Is.GreaterThan(ordinaryTravel * 2f),
                    "The release accelerates into a visibly faster catch-up than ordinary planning.");
                Assert.That(arena.Separation, Is.GreaterThanOrEqualTo(5f), "Even catch-up respects the approach floor.");
                float firstCatchup = arena.Separation;
                // Past the 0.3 s release, so float steps cannot leave a sliver of it.
                scope.AdvanceFor(.25f);
                Assert.That(arena.IsBulletTimeReleased, Is.False);
                Assert.That(arena.Separation, Is.GreaterThanOrEqualTo(5f).And.LessThan(firstCatchup), "The release closes the gap without passing its floor.");
                scope.AdvanceFor(.35f);
                Assert.That(arena.BulletTimeAmount, Is.EqualTo(1f), "Then bullet time eases back in.");
                scope.AdvanceFor(1f);
                Assert.That(arena.Separation, Is.EqualTo(5f).Within(.001f), "Normal drift continues after release and brakes at the same floor.");
                scope.AdvanceFor(.5f);
                Assert.That(arena.Separation, Is.EqualTo(5f).Within(.0001f), "After braking at the floor, planning holds the reached position.");
            }
        }

        [UnityTest]
        public IEnumerator TabInspection_SlowsTheApproachWithThePlanningClock()
        {
            yield return null;
            using (var scope = new BulletScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Vector3 player = scope.Controller.ArenaView.PlayerRenderer.transform.localPosition;
                Vector3 enemy = scope.Controller.ArenaView.EnemyRenderer.transform.localPosition;
                scope.AdvanceFor(1f, keyboard);
                Release(keyboard.tabKey);
                Assert.That(scope.Controller.TurnTimeRemaining, Is.EqualTo(9.8f).Within(1e-3f), "Tab slows the clock as before.");
                AssertApproaching(scope.Controller.ArenaView, player, enemy);
                Assert.That(scope.Controller.ArenaView.Separation, Is.GreaterThanOrEqualTo(9.6f).And.LessThan(10f),
                    "The approach uses Tab's slower clock, rather than stopping or running at ordinary speed.");
                Assert.That(scope.Controller.ArenaView.BulletTimeAmount, Is.EqualTo(1f), "The planning look still eases in on real time.");
            }
        }

        [UnityTest]
        public IEnumerator ReadingAHeldExplanation_SlowsTheApproachWithThePlanningClock_LikeTab()
        {
            yield return null;
            using (var scope = new BulletScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                Vector3 player = arena.PlayerRenderer.transform.localPosition, enemy = arena.EnemyRenderer.transform.localPosition;
                // Opening first spends ordinary planning time; after it opens, both the timer and approach slow down.
                scope.Advance(controller.PresentationSettings.ExplanationHoldSeconds, keyboard);
                Assert.That(controller.IsReadingExplanation, Is.True);
                AssertApproaching(arena, player, enemy);
                float beforeReading = arena.Separation;
                float reading = controller.PresentationSettings.ExplanationTimeScale;
                scope.AdvanceFor(1f, keyboard);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(9.65f - reading).Within(1e-3f), "Reading slows the clock…");
                float readingTravel = beforeReading - arena.Separation;
                Assert.That(readingTravel, Is.GreaterThan(0f).And.LessThanOrEqualTo(2f * reading + .001f),
                    "The approach keeps moving on the explanation's slower clock.");
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.IsReadingExplanation, Is.False);
                Assert.That(controller.Session.PlayerQueue, Is.Empty, "Letting go of an explanation never queues.");
                float beforeClosing = arena.Separation;
                scope.AdvanceFor(1f, keyboard);
                Assert.That(beforeClosing - arena.Separation, Is.GreaterThan(readingTravel * 2f),
                    "Closing the card restores ordinary planning travel.");
                AssertApproaching(arena, player, enemy);
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
                Assert.That(controller.SkipScene(), Is.True, "The intro scene comes first.");
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(1));
                Assert.That(controller.Encounter, Is.EqualTo(EncounterKind.Duel));
                // Leaving mission 1 with it marked won opens mission 2's briefing.
                Assert.That(controller.Prologue.TryRestore(1), Is.True);
                controller.ReturnToLobby();
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.StartMission(), Is.True);
                Assert.That(controller.SkipScene(), Is.True, "The intro scene comes first.");
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
                Assert.That(settings.BattleStagingSeparation, Is.EqualTo(5.4f));
                Assert.That(settings.BattleDriftMinimumSeparation, Is.EqualTo(5f));
                Assert.That(settings.BattlePoseSpeed, Is.Zero);
                Assert.That(settings.BattleDesaturation, Is.EqualTo(30f));
                Assert.That(settings.BattleCoolTint, Is.EqualTo(.35f));
                Assert.That(settings.BattleCycleReleaseSeconds, Is.EqualTo(.3f));
                float stepSlow = settings.StepSlowMotionScale, playback = settings.AnimationPlaybackSpeed;
                JsonUtility.FromJsonOverwrite("{\"battleDriftSpeed\":9,\"battleStagingSeparation\":20,\"battleDriftMinimumSeparation\":1,\"battlePoseSpeed\":-1," +
                    "\"battleDesaturation\":500,\"battleCoolTint\":2,\"battleCycleReleaseSeconds\":5}", settings);
                Assert.That(settings.BattleDriftSpeed, Is.EqualTo(2f));
                Assert.That(settings.BattleStagingSeparation, Is.EqualTo(10f));
                Assert.That(settings.BattleDriftMinimumSeparation, Is.EqualTo(4f), "Never closer than the contact distance.");
                Assert.That(settings.BattlePoseSpeed, Is.Zero);
                Assert.That(settings.BattleDesaturation, Is.EqualTo(100f));
                Assert.That(settings.BattleCoolTint, Is.EqualTo(1f));
                Assert.That(settings.BattleCycleReleaseSeconds, Is.EqualTo(1f));
                Assert.That(settings.StepSlowMotionScale, Is.EqualTo(stepSlow));
                Assert.That(settings.AnimationPlaybackSpeed, Is.EqualTo(playback));
                foreach (string field in new[] { "battleDriftSpeed", "battleStagingSeparation", "battleDriftMinimumSeparation", "battlePoseSpeed",
                    "battleDesaturation", "battleCoolTint", "battleCycleReleaseSeconds" })
                {
                    FieldInfo info = typeof(DuelPresentationSettings).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert.That(info, Is.Not.Null, field);
                    info.SetValue(settings, float.NaN);
                }
                Assert.That(settings.BattleDriftSpeed, Is.EqualTo(.15f));
                Assert.That(settings.BattleStagingSeparation, Is.EqualTo(5.4f));
                Assert.That(settings.BattleDriftMinimumSeparation, Is.EqualTo(5f));
                Assert.That(settings.BattleDesaturation, Is.EqualTo(30f));
                Assert.That(settings.BattleCycleReleaseSeconds, Is.EqualTo(.3f));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        private static ColorAdjustments Grade(LegacyArenaView arena)
        {
            Assert.That(arena.ArenaProfile.TryGet(out ColorAdjustments adjustments), Is.True);
            return adjustments;
        }

        private static void AssertApproaching(LegacyArenaView arena, Vector3 playerBefore, Vector3 enemyBefore)
        {
            Vector3 player = arena.PlayerRenderer.transform.localPosition, enemy = arena.EnemyRenderer.transform.localPosition;
            Assert.That(player.x, Is.GreaterThan(playerBefore.x), "The player approaches the opponent during planning.");
            Assert.That(enemy.x, Is.LessThan(enemyBefore.x), "The opponent approaches the player during planning.");
            Assert.That(player.y, Is.EqualTo(playerBefore.y).Within(.0001f), "Planning never invents a Y destination.");
            Assert.That(enemy.y, Is.EqualTo(enemyBefore.y).Within(.0001f));
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

            // The movement solver caps a single hitch at .25 seconds; use ordinary frames for elapsed-time assertions.
            public void AdvanceFor(float seconds, Keyboard keyboard = null)
            {
                int frames = Mathf.CeilToInt(seconds / .02f);
                float delta = seconds / frames;
                for (int frame = 0; frame < frames; frame++) advance(delta, keyboard);
            }

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
