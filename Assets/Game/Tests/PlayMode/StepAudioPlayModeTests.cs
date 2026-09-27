using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class StepAudioPlayModeTests
    {
        [UnityTest]
        public IEnumerator DistinctCachedClips_AreNonSilentBoundedAndDoNotConsumeGameplayRandom()
        {
            string randomState = JsonUtility.ToJson(Random.state);
            var host = new GameObject("Step Audio Test Host");
            using (var audio = new DuelStepAudio(host.transform))
            {
                Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(randomState));
                audio.Play(LegacyStepAction.Dodge, true, .7f);
                AudioClip dodge = audio.DodgeSource.clip;
                audio.Play(LegacyStepAction.Pressure, true, .7f);
                AudioClip pressure = audio.PressureSource.clip;
                Assert.That(dodge, Is.Not.SameAs(pressure));
                Assert.That(dodge.name, Is.EqualTo("Step Dodge Success"));
                Assert.That(pressure.name, Is.EqualTo("Step Pressure Success"));
                AssertWave(dodge); AssertWave(pressure);
                audio.Play(LegacyStepAction.Dodge, true, .7f);
                Assert.That(audio.DodgeSource.clip, Is.SameAs(dodge));
                Assert.That(host.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(2));
                foreach (var source in host.GetComponentsInChildren<AudioSource>())
                {
                    Assert.That(source.spatialBlend, Is.Zero);
                    Assert.That(source.pitch, Is.EqualTo(1f));
                    Assert.That(source.loop || source.playOnAwake, Is.False);
                    Assert.That(source.ignoreListenerPause, Is.False);
                }
                Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(randomState));
            }
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissesUseQuieterSeparateClips_AndRepeatedInputsDoNotAllocateMoreVoices()
        {
            var host = new GameObject("Step Audio Test Host");
            using (var audio = new DuelStepAudio(host.transform))
            {
                audio.Play(LegacyStepAction.Dodge, false, .5f);
                AudioClip miss = audio.DodgeSource.clip;
                Assert.That(miss.name, Is.EqualTo("Step Dodge Miss"));
                Assert.That(audio.DodgeSource.volume, Is.EqualTo(.16f).Within(.0001f));
                AssertWave(miss);
                audio.Play(LegacyStepAction.Pressure, false, .5f);
                Assert.That(audio.PressureSource.clip.name, Is.EqualTo("Step Pressure Miss"));
                AssertWave(audio.PressureSource.clip);
                for (int i = 0; i < 100; i++) audio.Play(LegacyStepAction.Dodge, false, .5f);
                Assert.That(audio.DodgeSource.clip, Is.SameAs(miss));
                Assert.That(host.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(2));
                audio.Play(LegacyStepAction.Dodge, true, .5f);
                Assert.That(audio.DodgeSource.volume, Is.EqualTo(.5f));
                if (audio.DodgeSource.isPlaying)
                {
                    AudioClip success = audio.DodgeSource.clip;
                    audio.Play(LegacyStepAction.Dodge, false, .5f);
                    Assert.That(audio.DodgeSource.clip, Is.SameAs(success), "A miss must not truncate its success accent.");
                }
            }
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StopMuteAndDispose_ReleaseOwnedClipsAndVoices()
        {
            var host = new GameObject("Step Audio Test Host");
            var audio = new DuelStepAudio(host.transform);
            audio.Play(LegacyStepAction.Dodge, true, 1f);
            AudioClip clip = audio.DodgeSource.clip;
            AudioSource voice = audio.DodgeSource;
            audio.Play(LegacyStepAction.Pressure, true, 1f);
            audio.Stop();
            Assert.That(audio.DodgeSource.isPlaying || audio.PressureSource.isPlaying, Is.False);
            audio.Play(LegacyStepAction.Dodge, true, 0f);
            Assert.That(audio.DodgeSource.isPlaying, Is.False);
            audio.Dispose(); audio.Dispose();
            Assert.DoesNotThrow(() => audio.Play(LegacyStepAction.Dodge, true, 1f));
            yield return null;
            Assert.That(clip == null, Is.True);
            Assert.That(voice == null, Is.True);
            Assert.That(host.transform.childCount, Is.Zero);
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator AudioVolumeAndGlowSettings_AreSafeAndDoNotChangeTiming()
        {
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var host = new GameObject("Step Audio Test Host");
            using (var audio = new DuelStepAudio(host.transform))
            {
                Assert.That(settings.StepSoundVolume, Is.EqualTo(.7f));
                Assert.That(settings.StepRingGlowIntensity, Is.EqualTo(1.6f));
                foreach (float value in new[] { -100f, 100f, float.NaN, float.PositiveInfinity })
                {
                    Set(settings, "stepSoundVolume", value);
                    Set(settings, "stepRingGlowIntensity", value);
                    Assert.That(settings.StepSoundVolume, Is.InRange(0f, 1f));
                    Assert.That(settings.StepRingGlowIntensity, Is.InRange(0f, 3f));
                    audio.Play(LegacyStepAction.Pressure, true, value);
                    Assert.That(audio.PressureSource.volume, Is.InRange(0f, 1f));
                }
                Assert.That(settings.StepTimingWindow, Is.EqualTo(.1f));
                Assert.That(settings.StepAnticipationDuration, Is.EqualTo(.24f));
            }
            Object.Destroy(settings); Object.Destroy(host);
            yield return null;
        }

        private static void AssertWave(AudioClip clip)
        {
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(24000));
            Assert.That(clip.length, Is.InRange(.1f, .3f));
            var data = new float[clip.samples];
            Assert.That(clip.GetData(data, 0), Is.True);
            float peak = 0f;
            foreach (float sample in data)
            {
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False);
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }
            Assert.That(peak, Is.GreaterThan(.1f).And.LessThanOrEqualTo(.801f));
            Assert.That(data[0], Is.Zero.Within(.0001f));
            Assert.That(data[data.Length - 1], Is.Zero.Within(.0001f));
        }

        private static void Set(DuelPresentationSettings settings, string field, float value) =>
            typeof(DuelPresentationSettings).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, value);
    }
}
