using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The tension lighting (2026-10-15, <see cref="LegacyTensionLight"/>, <see cref="DuelTensionLook"/>): low
    /// health anywhere darkens the scenery and the sky (never the fighters), dims the picture a touch and deepens the edges
    /// toward an ember, easing in and back out on real time; a comeback swells the light a moment (brighter, open, warm)
    /// and a fall does not; <see cref="LegacyArenaView.Reset"/> leaves nothing of either at once; a scene in the middle of
    /// the battle plays under the neutral light and gives the tension back; and in a battle the rules' own health lights it
    /// (switched off, it never does).</summary>
    public sealed class TensionLightingPlayModeTests : InputTestFixture
    {
        private const float Frame = .02f, Tolerance = 1e-4f;

        [UnityTest]
        public IEnumerator LowHealth_DarkensTheSceneryDimsAndEmbersTheEdges_NeverTheFighters_AndHealingEasesItBack()
        {
            yield return null;
            using (var fixture = new ArenaFixture())
            {
                LegacyArenaView arena = fixture.Arena;
                DuelTensionLook look = fixture.Settings.TensionLighting;
                Assert.That(look.Tuning.HealthPercent, Is.EqualTo(LegacyTensionTuning.DefaultHealthPercent));
                Assert.That(look.Vignette, Is.EqualTo(DuelTensionLook.DefaultVignette).Within(Tolerance));
                Baseline calm = Baseline.Of(arena);

                fixture.Read(new LegacyHealthReading(100, 100, 31, 100, false), 1f);
                Assert.That(arena.IsTense, Is.False, "31% is not low enough…");
                calm.AssertCalm(arena);

                fixture.Read(new LegacyHealthReading(100, 100, 30, 100, false), look.Tuning.EaseInSeconds * .5f);
                Assert.That(arena.IsTense, Is.True, "…30% is.");
                Assert.That(arena.TensionAmount, Is.GreaterThan(0f).And.LessThan(1f), "It eases in on real time.");
                fixture.Read(new LegacyHealthReading(100, 100, 30, 100, false), look.Tuning.EaseInSeconds);
                Assert.That(arena.TensionAmount, Is.EqualTo(1f));
                Assert.That(Grade(arena).postExposure.value, Is.EqualTo(-look.Dimming).Within(Tolerance), "Dimmer…");
                Assert.That(Edges(arena).intensity.value, Is.EqualTo(calm.Vignette + look.Vignette).Within(Tolerance),
                    "…deeper edges…");
                Color ember = Edges(arena).color.value;
                Assert.That(ember.r, Is.EqualTo(look.VignetteInk.r).Within(Tolerance), "…of a warm ember…");
                Assert.That(ember.r > ember.g && ember.g > ember.b, Is.True);
                Assert.That(arena.ArenaCamera.backgroundColor.r,
                    Is.EqualTo(calm.Sky.r * (1f - look.BackdropDarkening)).Within(Tolerance), "…a darker sky…");
                Assert.That(FirstTile(arena).color.r, Is.EqualTo(1f - look.BackdropDarkening).Within(.001f), "…and scenery…");
                Assert.That(arena.PlayerRenderer.color, Is.EqualTo(Color.white), "…while the fighters keep their light.");
                Assert.That(arena.EnemyRenderer.color, Is.EqualTo(Color.white));
                Assert.That(Grade(arena).colorFilter.value, Is.EqualTo(Color.white), "The tension never tints the picture.");
                Assert.That(arena.FlowSwellCount, Is.Zero, "Falling behind is no turn of the flow.");

                fixture.Read(new LegacyHealthReading(25, 100, 100, 100, false), 1f);
                Assert.That(arena.TensionAmount, Is.EqualTo(1f), "Either fighter low is tense alike.");

                fixture.Read(new LegacyHealthReading(40, 100, 100, 100, false), look.Tuning.EaseOutSeconds * .5f);
                Assert.That(arena.IsTense, Is.False, "Healed past the share…");
                Assert.That(arena.TensionAmount, Is.GreaterThan(0f).And.LessThan(1f), "…it eases back…");
                fixture.Read(new LegacyHealthReading(40, 100, 100, 100, false), look.Tuning.EaseOutSeconds);
                Assert.That(arena.TensionAmount, Is.Zero);
                calm.AssertCalm(arena, "…all the way.");

                fixture.Read(new LegacyHealthReading(10, 100, 100, 100, false), 2f);
                Assert.That(arena.TensionAmount, Is.EqualTo(1f));
                for (float time = 0f; time < look.Tuning.EaseOutSeconds + .1f; time += Frame) arena.Tick(0f, Frame);
                Assert.That(arena.TensionAmount, Is.Zero, "An arena ticked without a reading lets it go.");
                calm.AssertCalm(arena);
            }
        }

        [UnityTest]
        public IEnumerator AComeback_SwellsTheLightAMoment_AndAFallDoesNot()
        {
            yield return null;
            using (var fixture = new ArenaFixture())
            {
                LegacyArenaView arena = fixture.Arena;
                DuelTensionLook look = fixture.Settings.TensionLighting;
                Baseline calm = Baseline.Of(arena);
                fixture.Read(new LegacyHealthReading(100, 100, 100, 100, false), Frame);
                fixture.Read(new LegacyHealthReading(60, 100, 100, 100, false), Frame);
                Assert.That(arena.IsFlowSwelling, Is.False);
                fixture.Read(new LegacyHealthReading(60, 100, 50, 100, false), look.Tuning.SwellRiseSeconds + Frame);
                Assert.That(arena.LastFlowTurn, Is.EqualTo(LegacyFlowTurn.TookTheLead));
                Assert.That(arena.IsFlowSwelling, Is.True, "From behind to ahead: the light swells…");
                Assert.That(arena.FlowSwellAmount, Is.EqualTo(1f), "…to its height…");
                Assert.That(Grade(arena).postExposure.value, Is.EqualTo(look.SwellExposure).Within(Tolerance), "…brighter…");
                Assert.That(Edges(arena).intensity.value, Is.EqualTo(calm.Vignette - look.SwellVignetteRelief).Within(Tolerance),
                    "…with open edges…");
                Color warm = Grade(arena).colorFilter.value;
                Assert.That(warm.r > warm.g && warm.g > warm.b, Is.True, "…and warm.");
                fixture.Read(new LegacyHealthReading(60, 100, 50, 100, false), look.Tuning.SwellSeconds);
                Assert.That(arena.IsFlowSwelling, Is.False, "A moment only.");
                calm.AssertCalm(arena);
                Assert.That(arena.FlowSwellCount, Is.EqualTo(1));

                // The opponent's break swells it too.
                fixture.Read(new LegacyHealthReading(60, 100, 50, 100, true), Frame);
                Assert.That(arena.IsFlowSwelling, Is.True);
                Assert.That(arena.LastFlowTurn, Is.EqualTo(LegacyFlowTurn.BrokeOpponent));
                fixture.Read(new LegacyHealthReading(60, 100, 50, 100, true), look.Tuning.SwellSeconds);

                // Behind again, then a finishing blow: the finale has the light.
                fixture.Read(new LegacyHealthReading(20, 100, 50, 100, false), Frame);
                fixture.Read(new LegacyHealthReading(20, 100, 0, 100, false), Frame);
                Assert.That(arena.LastFlowTurn, Is.EqualTo(LegacyFlowTurn.TookTheLead));
                Assert.That(arena.IsFlowSwelling, Is.False, "A fall never swells.");
                Assert.That(arena.FlowSwellCount, Is.EqualTo(2));
            }
        }

        [UnityTest]
        public IEnumerator Reset_LeavesNoLightAtOnce_AndTheNextReadingOnlySetsWhereThingsStand()
        {
            yield return null;
            using (var fixture = new ArenaFixture())
            {
                LegacyArenaView arena = fixture.Arena;
                Baseline calm = Baseline.Of(arena);
                fixture.Read(new LegacyHealthReading(100, 100, 100, 100, false), Frame);
                fixture.Read(new LegacyHealthReading(20, 100, 100, 100, false), 2f);
                fixture.Read(new LegacyHealthReading(20, 100, 10, 100, true), Frame);
                Assert.That(arena.TensionAmount, Is.EqualTo(1f));
                Assert.That(arena.IsFlowSwelling, Is.True);

                arena.Reset();
                Assert.That(arena.TensionAmount + arena.FlowSwellAmount, Is.Zero, "Nothing of the light is left…");
                Assert.That(arena.IsTense || arena.IsFlowSwelling, Is.False);
                Assert.That(arena.FlowSwellCount, Is.Zero);
                Assert.That(arena.LastFlowTurn, Is.EqualTo(LegacyFlowTurn.None));
                calm.AssertCalm(arena, "…at once.");

                fixture.Read(new LegacyHealthReading(90, 100, 10, 100, true), Frame);
                Assert.That(arena.IsFlowSwelling, Is.False, "A fresh battle's first reading turns nothing…");
                Assert.That(arena.IsTense, Is.True, "…though it is tense from there.");
            }
        }

        [UnityTest]
        public IEnumerator ASceneInTheMiddleOfTheBattle_PlaysUnderTheNeutralLight_AndGivesTheTensionBack()
        {
            yield return null;
            using (var fixture = new ArenaFixture())
            {
                LegacyArenaView arena = fixture.Arena;
                Baseline calm = Baseline.Of(arena);
                fixture.Read(new LegacyHealthReading(100, 100, 100, 100, false), Frame);
                fixture.Read(new LegacyHealthReading(100, 100, 20, 100, false), 2f);
                float exposure = Grade(arena).postExposure.value, edges = Edges(arena).intensity.value;
                Assert.That(arena.TensionAmount, Is.EqualTo(1f));

                Invoke(arena, "SuspendForCutscene");
                Assert.That(arena.TensionAmount, Is.Zero, "The scene plays under the neutral light.");
                Assert.That(Grade(arena).postExposure.value, Is.Zero);
                Assert.That(Edges(arena).intensity.value, Is.EqualTo(calm.Vignette).Within(Tolerance));
                Assert.That(FirstTile(arena).color, Is.EqualTo(Color.white), "The scenery too.");
                Assert.That(arena.ArenaCamera.backgroundColor, Is.EqualTo(calm.Sky));
                Invoke(arena, "ResumeAfterCutscene");
                Assert.That(arena.TensionAmount, Is.EqualTo(1f), "The battle's tension is back…");
                Assert.That(Grade(arena).postExposure.value, Is.EqualTo(exposure).Within(Tolerance));
                Assert.That(Edges(arena).intensity.value, Is.EqualTo(edges).Within(Tolerance));
                Assert.That(FirstTile(arena).color.r, Is.LessThan(1f), "…the scenery darkened as it was.");
            }
        }

        [UnityTest]
        public IEnumerator InABattle_TheRulesHealthLightsIt_SwitchedOffNothing_AndARestartLeavesNone()
        {
            yield return null;
            float tense, off;
            using (var scope = new BattleScope(null))
            {
                Assume.That(scope.Controller.IsTrainingBattle, Is.False);
                LegacyArenaView arena = scope.Controller.ArenaView;
                Assert.That(arena.TensionAmount, Is.Zero, "A fresh battle at full health is calm.");
                scope.StrikeToAQuarter();
                Assert.That(arena.IsTense, Is.True, "The enemy at a quarter of her health makes it tense…");
                scope.Advance(1f);
                tense = arena.TensionAmount;
                Assert.That(arena.FlowSwellCount, Is.Zero, "…though taking the lead from even is no comeback.");
                Assert.That(arena.ArenaProfile.TryGet(out Vignette vignette), Is.True);
                Assert.That(vignette.intensity.value, Is.GreaterThan(.4f + Tolerance));

                scope.Controller.RestartMatch();
                Assert.That(arena.TensionAmount, Is.Zero, "A restart leaves none of it…");
                Assert.That(vignette.intensity.value, Is.EqualTo(.4f).Within(Tolerance), "…at once.");
                Assert.That(Grade(arena).postExposure.value, Is.Zero);
            }
            using (var scope = new BattleScope("\"tensionLightingEnabled\":false"))
            {
                LegacyArenaView arena = scope.Controller.ArenaView;
                scope.StrikeToAQuarter();
                scope.Advance(1f);
                off = arena.TensionAmount;
                Assert.That(arena.IsTense, Is.False);
            }
            Assert.That(tense, Is.EqualTo(1f), "On, the light is all the way tense after a second.");
            Assert.That(off, Is.Zero, "Switched off, it never shifts.");
        }

        [Test]
        public void TheSettings_HoldTheDocumentedDefaults_AndKeepWildValuesInRange()
        {
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                DuelTensionLook look = settings.TensionLighting;
                Assert.That(look.Tuning.TensionEnabled && look.Tuning.SwellEnabled && look.Tuning.SwellOnBreak, Is.True);
                Assert.That(look.Tuning.HealthPercent, Is.EqualTo(30));
                Assert.That(look.Tuning.LeadMarginPercent, Is.EqualTo(5));
                Assert.That(look.Tuning.EaseInSeconds, Is.EqualTo(.8f).Within(Tolerance));
                Assert.That(look.Tuning.EaseOutSeconds, Is.EqualTo(1.2f).Within(Tolerance));
                Assert.That(look.Tuning.SwellSeconds, Is.EqualTo(.92f).Within(Tolerance));
                Assert.That(look.BackdropDarkening, Is.EqualTo(.3f).Within(Tolerance));
                Assert.That(look.Dimming, Is.EqualTo(.12f).Within(Tolerance));
                Assert.That(look.Vignette, Is.EqualTo(.12f).Within(Tolerance));
                Assert.That(look.VignetteInk, Is.EqualTo(DuelTensionLook.DefaultVignetteInk));
                Assert.That(look.SwellExposure, Is.EqualTo(.3f).Within(Tolerance));
                Assert.That(look.SwellVignetteRelief, Is.EqualTo(.12f).Within(Tolerance));
                Assert.That(look.SwellWarmTint, Is.EqualTo(.25f).Within(Tolerance));

                JsonUtility.FromJsonOverwrite("{\"tensionLightingEnabled\":false,\"tensionHealthPercent\":400," +
                    "\"tensionEaseInSeconds\":-1,\"tensionBackdropDarkening\":3,\"tensionDimming\":-2,\"tensionVignette\":9," +
                    "\"flowSwellEnabled\":false,\"flowLeadMarginPercent\":80,\"flowSwellExposure\":7," +
                    "\"flowSwellFadeSeconds\":0}", settings);
                look = settings.TensionLighting;
                Assert.That(look.Tuning.TensionEnabled || look.Tuning.SwellEnabled, Is.False);
                Assert.That(look.Tuning.HealthPercent, Is.EqualTo(LegacyTensionTuning.MaximumHealthPercent));
                Assert.That(look.Tuning.EaseInSeconds, Is.Zero);
                Assert.That(look.BackdropDarkening, Is.EqualTo(DuelTensionLook.MaximumBackdropDarkening));
                Assert.That(look.Dimming, Is.Zero);
                Assert.That(look.Vignette, Is.EqualTo(DuelTensionLook.MaximumVignette));
                Assert.That(look.Tuning.LeadMarginPercent, Is.EqualTo(LegacyTensionTuning.MaximumLeadMarginPercent));
                Assert.That(look.SwellExposure, Is.EqualTo(1f));
                Assert.That(look.Tuning.SwellFadeSeconds, Is.EqualTo(LegacyTensionTuning.MinimumSwellFadeSeconds));
                Assert.That(DuelTensionLook.Off.Tuning.TensionEnabled || DuelTensionLook.Off.Tuning.SwellEnabled, Is.False);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        private static ColorAdjustments Grade(LegacyArenaView arena)
        {
            Assert.That(arena.ArenaProfile.TryGet(out ColorAdjustments adjustments), Is.True);
            return adjustments;
        }

        private static Vignette Edges(LegacyArenaView arena)
        {
            Assert.That(arena.ArenaProfile.TryGet(out Vignette vignette), Is.True);
            return vignette;
        }

        private static SpriteRenderer FirstTile(LegacyArenaView arena)
        {
            foreach (SpriteRenderer tile in arena.ForestBackdrop.Layers[0].Tiles)
                if (tile.gameObject.activeSelf) return tile;
            Assert.Fail("The forest's far layer must have an active tile.");
            return null;
        }

        private static void Invoke(LegacyArenaView arena, string method)
        {
            MethodInfo info = typeof(LegacyArenaView).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(arena, null);
        }

        /// <summary>The arena's light with no tension and no swell: the grade, the edges, the sky and the scenery.</summary>
        private readonly struct Baseline
        {
            public readonly float Exposure, Vignette;
            public readonly Color Edge, Filter, Sky;

            private Baseline(float exposure, float vignette, Color edge, Color filter, Color sky)
            {
                Exposure = exposure;
                Vignette = vignette;
                Edge = edge;
                Filter = filter;
                Sky = sky;
            }

            public static Baseline Of(LegacyArenaView arena)
            {
                Assert.That(arena.TensionAmount + arena.FlowSwellAmount, Is.Zero);
                return new Baseline(Grade(arena).postExposure.value, Edges(arena).intensity.value, Edges(arena).color.value,
                    Grade(arena).colorFilter.value, arena.ArenaCamera.backgroundColor);
            }

            public void AssertCalm(LegacyArenaView arena, string message = null)
            {
                Assert.That(Grade(arena).postExposure.value, Is.EqualTo(Exposure).Within(Tolerance), message);
                Assert.That(Edges(arena).intensity.value, Is.EqualTo(Vignette).Within(Tolerance), message);
                Assert.That(Edges(arena).color.value, Is.EqualTo(Edge), message);
                Assert.That(Grade(arena).colorFilter.value, Is.EqualTo(Filter), message);
                Assert.That(arena.ArenaCamera.backgroundColor, Is.EqualTo(Sky), message);
                Assert.That(FirstTile(arena).color, Is.EqualTo(Color.white), message);
            }
        }

        /// <summary>An arena of its own (its own settings at their defaults), ticked by hand.</summary>
        private sealed class ArenaFixture : IDisposable
        {
            private readonly GameObject host;
            public DuelPresentationSettings Settings { get; }
            public LegacyArenaView Arena { get; }

            public ArenaFixture()
            {
                host = new GameObject("Tension Lighting Test Host");
                Settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), Settings);
                Arena.Tick(0f, Frame);
            }

            /// <summary>Frames of <paramref name="seconds"/> real time, each reading <paramref name="reading"/>.</summary>
            public void Read(LegacyHealthReading reading, float seconds)
            {
                DuelTensionLook look = Settings.TensionLighting;
                float elapsed = 0f;
                do
                {
                    float step = Mathf.Min(Frame, Mathf.Max(seconds - elapsed, 1e-4f));
                    Arena.SetTensionLighting(reading, look);
                    Arena.Tick(0f, step);
                    elapsed += step;
                }
                while (elapsed < seconds - 1e-6f);
            }

            public void Dispose()
            {
                Arena.Dispose();
                Object.Destroy(host);
                Object.Destroy(Settings);
            }
        }

        /// <summary>The scene's controller on a duel of its own: one strike of 75 on a breathing enemy of 100 health (no
        /// resistance hit, no break), driven frame by frame on a copy of the settings.</summary>
        private sealed class BattleScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public DuelPresentationSettings Settings { get; }

            public BattleScope(string tuning)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                Settings = Object.Instantiate(originalSettings);
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"tensionLightingEnabled\":true,\"tensionHealthPercent\":30," +
                        "\"tensionEaseInSeconds\":0.8,\"tensionEaseOutSeconds\":1.2}", Settings);
                    if (!string.IsNullOrEmpty(tuning)) JsonUtility.FromJsonOverwrite("{" + tuning + "}", Settings);
                    Set("presentationSettings", Settings);
                    arenaSettings.SetValue(Controller.ArenaView, Settings);
                    Set("mission", null);
                    Set("guide", null);
                    var strike = new LegacySkill(995, "Tension Strike", 1, 75, 75, LegacySkillKind.Attack,
                        LegacySkillProperty.Slash, 1, 0, "", iconId: 1);
                    Set("session", new LegacyQueuedDuel(100, 50, 100, 10, new[] { strike },
                        new[] { LegacyCommonActions.Breathe }, new[] { 1 }, 5,
                        features: CombatFeature.LaneQ | CombatFeature.Cycle));
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance)
                        .Invoke(Controller, new object[] { null, null });
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch
                {
                    // A failed setup never reaches Dispose: restore the shared controller here.
                    try { Dispose(); } catch { Controller.enabled = originalEnabled; }
                    throw;
                }
            }

            public void Advance(float seconds)
            {
                if (seconds <= 0f)
                {
                    advance(0f, null);
                    return;
                }
                for (float elapsed = 0f; elapsed < seconds - 1e-6f; elapsed += Frame) advance(Frame, null);
            }

            /// <summary>Queues the strike, commits, and plays on until it has landed (the enemy at 25 of 100).</summary>
            public void StrikeToAQuarter()
            {
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                for (int frame = 0; frame < 2000 && Controller.Session.Enemy.Health > 25; frame++) advance(.005f, null);
                Assert.That(Controller.Session.Enemy.Health, Is.EqualTo(25), "The strike lands on the body.");
                // The frame after the hit reads it.
                advance(.005f, null);
            }

            private void Set(string name, object value)
            {
                FieldInfo field = typeof(DuelPrototypeController).GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null, name);
                field.SetValue(Controller, value);
            }

            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(Settings);
            }
        }
    }
}
