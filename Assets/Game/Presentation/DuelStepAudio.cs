using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Two bounded, unpitched 2D voices, independent of random-pitch impact audio.</summary>
    public sealed class DuelStepAudio : IDisposable
    {
        private const int SampleRate = 24000;
        private readonly GameObject root;
        private readonly AudioClip dodgeSuccess, dodgeMiss, pressureSuccess, pressureMiss;
        private bool dodgePlayingSuccess, pressurePlayingSuccess, disposed;

        public AudioSource DodgeSource { get; }
        public AudioSource PressureSource { get; }

        public DuelStepAudio(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            root = new GameObject("Step Audio");
            root.transform.SetParent(parent, false);
            DodgeSource = CreateVoice("Dodge Sound");
            PressureSource = CreateVoice("Pressure Sound");
            // Original synthesized clips: generated once, no imported hit clip or gameplay RNG dependency.
            dodgeSuccess = CreateClip(LegacyStepAction.Dodge, true);
            dodgeMiss = CreateClip(LegacyStepAction.Dodge, false);
            pressureSuccess = CreateClip(LegacyStepAction.Pressure, true);
            pressureMiss = CreateClip(LegacyStepAction.Pressure, false);
        }

        public void Play(LegacyStepAction action, bool success, float volume)
        {
            if (disposed || (action != LegacyStepAction.Dodge && action != LegacyStepAction.Pressure)) return;
            volume = float.IsNaN(volume) || float.IsInfinity(volume) ? .7f : Mathf.Clamp01(volume);
            bool dodge = action == LegacyStepAction.Dodge;
            AudioSource source = dodge ? DodgeSource : PressureSource;
            if (volume <= 0f)
            {
                source.Stop();
                return;
            }
            // Repeated failed inputs do not cut off an ongoing success accent, or stack unlimited voices.
            if (!success && source.isPlaying && (dodge ? dodgePlayingSuccess : pressurePlayingSuccess)) return;
            source.Stop();
            source.pitch = 1f;
            source.volume = volume * (success ? 1f : .32f);
            source.clip = dodge ? (success ? dodgeSuccess : dodgeMiss) : (success ? pressureSuccess : pressureMiss);
            if (dodge) dodgePlayingSuccess = success;
            else pressurePlayingSuccess = success;
            source.Play();
        }

        public void Stop()
        {
            if (disposed) return;
            if (DodgeSource != null) DodgeSource.Stop();
            if (PressureSource != null) PressureSource.Stop();
            dodgePlayingSuccess = pressurePlayingSuccess = false;
        }

        private AudioSource CreateVoice(string name)
        {
            var voice = new GameObject(name);
            voice.transform.SetParent(root.transform, false);
            var source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.priority = 96;
            return source;
        }

        private static AudioClip CreateClip(LegacyStepAction action, bool success)
        {
            bool dodge = action == LegacyStepAction.Dodge;
            float duration = dodge ? (success ? .24f : .15f) : (success ? .28f : .17f);
            var samples = new float[Mathf.RoundToInt(duration * SampleRate)];
            uint noiseState = dodge ? 0xCAFE1234u : 0xBEEF5678u;
            float lowNoise = 0f, phase = 0f, sum = 0f;
            for (int index = 0; index < samples.Length; index++)
            {
                float time = (float)index / SampleRate;
                float progress = (float)index / (samples.Length - 1);
                noiseState ^= noiseState << 13;
                noiseState ^= noiseState >> 17;
                noiseState ^= noiseState << 5;
                float noise = (noiseState & 0xffff) / 32767.5f - 1f;
                lowNoise += (noise - lowNoise) * .16f;
                float attack = Mathf.Min(1f, time / .008f);
                float release = (1f - progress) * (1f - progress);
                float sample;
                if (dodge)
                {
                    // Air passing the blade, followed by a clear, high metallic success accent.
                    sample = (noise - lowNoise) * .3f * attack * release;
                    if (success && time >= .025f)
                    {
                        float accent = time - .025f;
                        sample += (Mathf.Sin(2f * Mathf.PI * 1320f * accent) +
                            .42f * Mathf.Sin(2f * Mathf.PI * 1980f * accent)) *
                            .28f * Mathf.Min(1f, accent / .004f) * Mathf.Exp(-24f * accent) * release;
                    }
                }
                else
                {
                    // Short descending body, with a separate sword-metal edge only on success.
                    phase += 2f * Mathf.PI * Mathf.Lerp(180f, 62f, Mathf.Min(1f, time / .12f)) / SampleRate;
                    sample = (Mathf.Sin(phase) * .55f + noise * .16f) *
                        attack * Mathf.Exp(-14f * time) * release;
                    if (success && time >= .016f)
                    {
                        float accent = time - .016f;
                        sample += (Mathf.Sin(2f * Mathf.PI * 920f * accent) +
                            .5f * Mathf.Sin(2f * Mathf.PI * 1440f * accent)) *
                            .3f * Mathf.Min(1f, accent / .003f) * Mathf.Exp(-28f * accent) * release;
                    }
                }
                samples[index] = sample;
                sum += sample;
            }
            // Remove DC and cap peaks; taper both ends so stopping/replacing a cue remains quiet.
            float mean = sum / samples.Length, peak = 0f;
            for (int index = 0; index < samples.Length; index++)
            {
                float edgeFade = Mathf.Min(1f, Mathf.Min(index, samples.Length - 1 - index) / 96f);
                samples[index] = (samples[index] - mean) * edgeFade;
                peak = Mathf.Max(peak, Mathf.Abs(samples[index]));
            }
            float gain = peak > .8f ? .8f / peak : 1f;
            if (gain < 1f)
                for (int index = 0; index < samples.Length; index++) samples[index] *= gain;
            string name = "Step " + action + (success ? " Success" : " Miss");
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            Release(dodgeSuccess); Release(dodgeMiss); Release(pressureSuccess); Release(pressureMiss);
            Release(root);
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
