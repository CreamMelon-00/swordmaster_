using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ClashAmbienceDuckPlayModeTests
    {
        private const float Tolerance = .00001f;

        [UnityTest]
        public IEnumerator TheProductionArenaEvents_DipOnlyStrongBladeContacts_AndClearOnReset_WithoutOutdoorSoundIndoors()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool originalEnabled = controller.enabled;
            DuelPresentationSettings originalTuning = controller.PresentationSettings;
            var tuning = Object.Instantiate(originalTuning);
            FieldInfo settingsField = typeof(DuelPrototypeController).GetField("presentationSettings",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(settingsField, Is.Not.Null);
            JsonUtility.FromJsonOverwrite("{\"forestAmbienceVolume\":0.4,\"forestAmbienceFadeSeconds\":0," +
                "\"clashAmbienceDuckEnabled\":true,\"clashAmbienceDuckStrength\":0.7}", tuning);
            controller.enabled = false;
            try
            {
                controller.SetCombatDebugMode(false);
                controller.ShowTitle();
                settingsField.SetValue(controller, tuning);
                LegacyArenaView arena = controller.ArenaView;
                DuelForestAmbience forest = controller.ForestAmbience;
                arena.SetBackdrop(ArenaBackdropKind.Forest);
                forest.Tick(1f, true, 1f, 0f);

                arena.PresentHit(true, 0, 4, false, false, 4, LegacyArenaView.HitExchange.MutualClash);
                forest.Tick(.02f, true, 1f, 0f);
                Assert.That(forest.Volume, Is.EqualTo(.4f).Within(Tolerance), "A small blade touch leaves the bed alone.");
                arena.PresentHit(true, 18, 0, false, false, 18);
                forest.Tick(.02f, true, 1f, 0f);
                Assert.That(forest.Volume, Is.EqualTo(.4f).Within(Tolerance), "An ordinary body hit is not a sword collision.");
                arena.PresentHit(true, 0, 18, false, false, 18, LegacyArenaView.HitExchange.MutualClash);
                forest.Tick(.02f, true, 1f, 0f);
                Assert.That(forest.Volume, Is.EqualTo(.12f).Within(Tolerance), "The production arena event reaches its owner's real ambience bed.");
                arena.ClearCombatAccents();
                Assert.That(forest.Volume, Is.EqualTo(.4f).Within(Tolerance));
                arena.PresentHit(false, 0, 18, true, false, 18);
                forest.Tick(.02f, true, 1f, 0f);
                Assert.That(forest.Volume, Is.EqualTo(.12f).Within(Tolerance), "A substantial guarded collision works in either direction.");
                arena.Reset();
                Assert.That(forest.Volume, Is.EqualTo(.4f).Within(Tolerance), "A reset cannot retain the dip.");

                arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                forest.Tick(1f, false, 1f, 0f);
                arena.PresentHit(true, 0, 18, false, false, 18, LegacyArenaView.HitExchange.MutualClash);
                forest.Tick(.02f, false, 1f, 0f);
                Assert.That(forest.Volume, Is.Zero);
                Assert.That(forest.IsPlaying, Is.False, "The cue does not introduce outdoor ambience into a corridor.");
            }
            finally
            {
                controller.ArenaView.ClearCombatAccents();
                controller.ForestAmbience.Stop();
                settingsField.SetValue(controller, originalTuning);
                controller.ShowTitle();
                controller.enabled = originalEnabled;
                Object.Destroy(tuning);
            }
        }

        [Test]
        public void AClash_OnlyDipsTheForestBedAndRestoresTheExistingStoryAndVisibilityMix()
        {
            var root = new GameObject("Clash Ambience Test");
            var tuning = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            JsonUtility.FromJsonOverwrite("{\"forestAmbienceVolume\":0.4,\"forestAmbienceFadeSeconds\":0,\"forestAmbienceSceneDuck\":0.7}", tuning);
            var otherSound = root.AddComponent<AudioSource>();
            otherSound.volume = .8f;
            try
            {
                using (var forest = new DuelForestAmbience(root.transform, () => tuning))
                {
                    Assert.That(forest.Clip, Is.Not.Null);
                    forest.Tick(1f, true, .5f, 1f);
                    float original = forest.Volume;
                    Assert.That(original, Is.EqualTo(.06f).Within(Tolerance));
                    forest.PresentClash();
                    forest.Tick(.02f, true, .5f, 1f);
                    Assert.That(forest.Volume, Is.EqualTo(original * .3f).Within(Tolerance));
                    Assert.That(forest.TargetVolume, Is.EqualTo(original).Within(Tolerance));
                    Assert.That(otherSound.volume, Is.EqualTo(.8f));
                    forest.Tick(.3f, true, .5f, 1f);
                    Assert.That(forest.Volume, Is.EqualTo(original).Within(Tolerance));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tuning);
            }
        }

        [Test]
        public void ClearStopAndLiveDisable_RemoveTheDipBeforeTheBedIsReused()
        {
            var root = new GameObject("Clash Ambience Reset Test");
            var tuning = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            JsonUtility.FromJsonOverwrite("{\"forestAmbienceVolume\":0.4,\"forestAmbienceFadeSeconds\":0}", tuning);
            try
            {
                using (var forest = new DuelForestAmbience(root.transform, () => tuning))
                {
                    forest.Tick(1f, true, 1f, 0f);
                    forest.PresentClash();
                    forest.Tick(.02f, true, 1f, 0f);
                    Assert.That(forest.Volume, Is.EqualTo(.12f).Within(Tolerance));
                    forest.ClearClash();
                    Assert.That(forest.Volume, Is.EqualTo(.4f).Within(Tolerance));
                    forest.PresentClash();
                    forest.Tick(.02f, true, 1f, 0f);
                    JsonUtility.FromJsonOverwrite("{\"clashAmbienceDuckEnabled\":false}", tuning);
                    forest.Tick(0f, true, 1f, 0f);
                    Assert.That(forest.Volume, Is.EqualTo(.4f).Within(Tolerance));
                    JsonUtility.FromJsonOverwrite("{\"clashAmbienceDuckEnabled\":true}", tuning);
                    forest.PresentClash();
                    forest.Tick(.02f, true, 1f, 0f);
                    forest.Stop();
                    Assert.That(forest.Volume, Is.Zero);
                    Assert.That(forest.IsPlaying, Is.False);
                    forest.Tick(1f, true, 1f, 0f);
                    Assert.That(forest.Volume, Is.EqualTo(.4f).Within(Tolerance));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tuning);
            }
        }

        [Test]
        public void SceneFade_ContinuesUnderTheDipAndNonForestScenesClearIt()
        {
            var root = new GameObject("Clash Ambience Fade Test");
            var tuning = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            JsonUtility.FromJsonOverwrite("{\"forestAmbienceVolume\":0.4,\"forestAmbienceFadeSeconds\":1}", tuning);
            try
            {
                using (var forest = new DuelForestAmbience(root.transform, () => tuning))
                {
                    forest.Tick(1f, true, 1f, 0f);
                    forest.PresentClash();
                    forest.Tick(.02f, true, 1f, 0f);
                    forest.Tick(.05f, true, .5f, 0f);
                    Assert.That(forest.Volume, Is.EqualTo(.38f * .3f).Within(Tolerance));
                    forest.Tick(0f, false, 1f, 0f);
                    Assert.That(forest.Volume, Is.EqualTo(.38f).Within(Tolerance), "Leaving restores gain while the established scene fade continues.");
                    Assert.That(forest.TargetVolume, Is.Zero);
                    forest.Tick(float.NaN, false, 1f, 0f);
                    forest.Tick(float.PositiveInfinity, false, 1f, 0f);
                    Assert.That(forest.Volume, Is.EqualTo(.38f).Within(Tolerance));
                    forest.Tick(1f, false, 1f, 0f);
                    Assert.That(forest.IsPlaying, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tuning);
            }
        }
    }
}
